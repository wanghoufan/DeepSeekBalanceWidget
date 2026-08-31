using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// OpenRouter 账户 credits 数据源：官方额度接口
/// GET https://openrouter.ai/api/v1/credits（Bearer Management Key）。
/// </summary>
public sealed class OpenRouterUsageProvider : IOpenRouterUsageProvider, IDisposable
{
    public const string ProviderName = "OpenRouter";
    public const string CreditsUrl = "https://openrouter.ai/api/v1/credits";

    private readonly string? _explicitApiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OpenRouterUsageProvider(string? explicitApiKey = null, HttpClient? httpClient = null)
    {
        _explicitApiKey = explicitApiKey;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _ownsHttpClient = httpClient is null;
    }

    public string? ResolveApiKey() =>
        string.IsNullOrWhiteSpace(_explicitApiKey) ? null : _explicitApiKey;

    public async Task<OpenRouterUsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        string? apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return OpenRouterUsageSnapshot.Unavailable("未配置 API Key");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, CreditsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return OpenRouterUsageSnapshot.Unavailable("API Key 无效（401）");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return OpenRouterUsageSnapshot.Unavailable("需 Management Key（403）");
            if (!response.IsSuccessStatusCode)
                return OpenRouterUsageSnapshot.Unavailable($"服务返回状态码 {(int)response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return OpenRouterUsageParser.Parse(json)
                ?? OpenRouterUsageSnapshot.Unavailable("响应格式无法解析");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return OpenRouterUsageSnapshot.Unavailable("网络请求失败：" + ex.Message);
        }
        catch (JsonException)
        {
            return OpenRouterUsageSnapshot.Unavailable("响应格式无法解析");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }
}
