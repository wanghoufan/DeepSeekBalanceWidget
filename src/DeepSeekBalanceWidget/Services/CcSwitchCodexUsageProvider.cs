using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public sealed class CcSwitchCodexUsageProvider : ICodexAccountsUsageProvider, IDisposable
{
    private const string OAuthTokenUrl = "https://auth.openai.com/oauth/token";
    private const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";
    private const string CodexClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    private static readonly TimeSpan AccessTokenRefreshBuffer = TimeSpan.FromMinutes(1);
    private static readonly SemaphoreSlim StoreWriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _storePath;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<string, CachedAccessToken> _accessTokens = new();
    private readonly ConcurrentDictionary<string, CodexAccountUsageSnapshot> _lastSuccessful = new();

    public CcSwitchCodexUsageProvider(
        string? storePath = null,
        HttpClient? httpClient = null)
    {
        _storePath = storePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cc-switch",
            "codex_oauth_auth.json");
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _ownsHttpClient = httpClient is null;
    }

    public async Task<IReadOnlyList<CodexAccountUsageSnapshot>> GetUsagesAsync(
        CancellationToken cancellationToken)
    {
        CcSwitchOAuthStore store;
        try
        {
            store = await ReadStoreAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return MarkAllStale("无法读取 CC Switch 账号文件");
        }

        var accounts = store.Accounts.Values
            .Where(account => !string.IsNullOrWhiteSpace(account.AccountId)
                && !string.IsNullOrWhiteSpace(account.Email)
                && !string.IsNullOrWhiteSpace(account.RefreshToken))
            .OrderBy(account => account.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (accounts.Length == 0)
            return MarkAllStale("CC Switch 中没有可用的 ChatGPT 账号");

        var tasks = accounts.Select(account => RefreshAccountAsync(account, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<CodexAccountUsageSnapshot> RefreshAccountAsync(
        CcSwitchAccount account,
        CancellationToken cancellationToken)
    {
        try
        {
            string accessToken = await GetAccessTokenAsync(account, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("codex-cli");
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Add("ChatGPT-Account-Id", account.AccountId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("CC Switch 登录已过期");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"额度接口返回 HTTP {(int)response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            var usage = ParseUsage(json);
            if (!usage.IsAvailable || usage.Windows.Count == 0)
                throw new InvalidOperationException(usage.Error ?? "未返回额度窗口");

            var snapshot = new CodexAccountUsageSnapshot(
                account.AccountId,
                account.Email!,
                CreateMiniLabel(account.Email!),
                usage,
                DateTimeOffset.Now,
                IsStale: false);
            _lastSuccessful[account.AccountId] = snapshot;
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or InvalidOperationException
            or JsonException
            or TaskCanceledException)
        {
            if (_lastSuccessful.TryGetValue(account.AccountId, out var previous))
                return previous with { IsStale = true, RefreshError = SafeError(ex) };

            return new CodexAccountUsageSnapshot(
                account.AccountId,
                account.Email!,
                CreateMiniLabel(account.Email!),
                CodexUsageSnapshot.Unavailable(SafeError(ex)),
                UpdatedAt: null,
                IsStale: true,
                RefreshError: SafeError(ex));
        }
    }

    private async Task<string> GetAccessTokenAsync(
        CcSwitchAccount account,
        CancellationToken cancellationToken)
    {
        if (_accessTokens.TryGetValue(account.AccountId, out var cached)
            && cached.ExpiresAt - DateTimeOffset.UtcNow > AccessTokenRefreshBuffer)
        {
            return cached.Token;
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = account.RefreshToken,
            ["client_id"] = CodexClientId,
            ["scope"] = "openid profile email"
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUrl) { Content = content };
        request.Headers.UserAgent.ParseAdd("cc-switch-codex-oauth");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("CC Switch 登录已过期");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"登录刷新返回 HTTP {(int)response.StatusCode}");

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokens = JsonSerializer.Deserialize<OAuthTokenResponse>(json, JsonOptions);
        if (string.IsNullOrWhiteSpace(tokens?.AccessToken))
            throw new JsonException("登录刷新未返回访问令牌");

        int expiresIn = tokens.ExpiresIn.GetValueOrDefault(3600);
        _accessTokens[account.AccountId] = new CachedAccessToken(
            tokens.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn)));

        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken)
            && !string.Equals(tokens.RefreshToken, account.RefreshToken, StringComparison.Ordinal))
        {
            await UpdateRefreshTokenAsync(
                account.AccountId,
                account.RefreshToken,
                tokens.RefreshToken,
                cancellationToken);
        }

        return tokens.AccessToken;
    }

    private async Task<CcSwitchOAuthStore> ReadStoreAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            _storePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        return await JsonSerializer.DeserializeAsync<CcSwitchOAuthStore>(
            stream, JsonOptions, cancellationToken)
            ?? throw new JsonException("CC Switch 账号文件为空");
    }

    private async Task UpdateRefreshTokenAsync(
        string accountId,
        string oldToken,
        string newToken,
        CancellationToken cancellationToken)
    {
        await StoreWriteLock.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreAsync(cancellationToken);
            if (!store.Accounts.TryGetValue(accountId, out var storedAccount)) return;
            if (!string.Equals(storedAccount.RefreshToken, oldToken, StringComparison.Ordinal)) return;

            storedAccount.RefreshToken = newToken;
            string directory = Path.GetDirectoryName(_storePath)!;
            string tempPath = Path.Combine(directory, $".{Path.GetFileName(_storePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(
                    tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                File.Move(tempPath, _storePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        finally
        {
            StoreWriteLock.Release();
        }
    }

    internal static CodexUsageSnapshot ParseUsage(string json)
    {
        var response = JsonSerializer.Deserialize<CodexUsageResponse>(json, JsonOptions);
        var windows = new List<CodexUsageWindow>();
        AddWindow(response?.RateLimit?.PrimaryWindow, windows);
        AddWindow(response?.RateLimit?.SecondaryWindow, windows);
        return windows.Count == 0
            ? CodexUsageSnapshot.Unavailable("未返回 ChatGPT 额度窗口")
            : new CodexUsageSnapshot(true, null, windows, null);
    }

    internal static string CreateMiniLabel(string email)
    {
        char label = email.Trim().FirstOrDefault(char.IsLetterOrDigit);
        return label == default ? "?" : char.ToUpperInvariant(label).ToString();
    }

    private static void AddWindow(CodexRateLimitWindow? source, ICollection<CodexUsageWindow> target)
    {
        if (source?.UsedPercent is not double used) return;
        int normalizedUsed = Math.Clamp((int)Math.Round(used), 0, 100);
        int? durationMinutes = source.LimitWindowSeconds is long seconds
            ? (int)Math.Clamp(seconds / 60, 1, int.MaxValue)
            : null;
        DateTimeOffset? resetsAt = null;
        if (source.ResetAt is long timestamp)
        {
            try { resetsAt = DateTimeOffset.FromUnixTimeSeconds(timestamp); }
            catch (ArgumentOutOfRangeException) { }
        }
        target.Add(new CodexUsageWindow(
            normalizedUsed,
            100 - normalizedUsed,
            durationMinutes,
            resetsAt));
    }

    private IReadOnlyList<CodexAccountUsageSnapshot> MarkAllStale(string error)
        => _lastSuccessful.Values
            .OrderBy(snapshot => snapshot.Email, StringComparer.OrdinalIgnoreCase)
            .Select(snapshot => snapshot with { IsStale = true, RefreshError = error })
            .ToArray();

    private static string SafeError(Exception ex) => ex switch
    {
        TaskCanceledException => "ChatGPT 额度读取超时",
        _ => ex.Message
    };

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresAt);

    private sealed class CcSwitchOAuthStore
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("accounts")]
        public Dictionary<string, CcSwitchAccount> Accounts { get; set; } = new();

        [JsonPropertyName("default_account_id")]
        public string? DefaultAccountId { get; set; }
    }

    private sealed class CcSwitchAccount
    {
        [JsonPropertyName("account_id")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("authenticated_at")]
        public long AuthenticatedAt { get; set; }
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }

    private sealed class CodexUsageResponse
    {
        [JsonPropertyName("rate_limit")]
        public CodexRateLimit? RateLimit { get; set; }
    }

    private sealed class CodexRateLimit
    {
        [JsonPropertyName("primary_window")]
        public CodexRateLimitWindow? PrimaryWindow { get; set; }

        [JsonPropertyName("secondary_window")]
        public CodexRateLimitWindow? SecondaryWindow { get; set; }
    }

    private sealed class CodexRateLimitWindow
    {
        [JsonPropertyName("used_percent")]
        public double? UsedPercent { get; set; }

        [JsonPropertyName("limit_window_seconds")]
        public long? LimitWindowSeconds { get; set; }

        [JsonPropertyName("reset_at")]
        public long? ResetAt { get; set; }
    }
}
