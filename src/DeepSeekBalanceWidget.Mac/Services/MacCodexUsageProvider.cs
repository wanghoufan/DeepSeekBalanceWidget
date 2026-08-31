using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// Uses CC Switch when it is configured, then falls back to the locally logged-in
/// Codex CLI. Most macOS users have the latter but not a CC Switch account file.
/// </summary>
public sealed class MacCodexUsageProvider : ICodexAccountsUsageProvider, IDisposable
{
    private readonly CcSwitchCodexUsageProvider _ccSwitch = new();
    private readonly CodexAppServerClient _codexCli = new();

    public async Task<IReadOnlyList<CodexAccountUsageSnapshot>> GetUsagesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CodexAccountUsageSnapshot> ccSwitchAccounts =
            await _ccSwitch.GetUsagesAsync(cancellationToken);
        if (ccSwitchAccounts.Any(HasUsage)) return ccSwitchAccounts;

        CodexUsageSnapshot localUsage = await _codexCli.GetUsageAsync(cancellationToken);
        if (localUsage.IsAvailable && localUsage.Windows.Count > 0)
        {
            return new[]
            {
                new CodexAccountUsageSnapshot(
                    AccountId: "local-codex-cli",
                    Email: "Codex 登录账号",
                    MiniLabel: "Codex",
                    Usage: localUsage,
                    UpdatedAt: DateTimeOffset.Now,
                    IsStale: false)
            };
        }

        string error = localUsage.Error
            ?? ccSwitchAccounts.FirstOrDefault()?.RefreshError
            ?? "无法读取本机 Codex 登录额度";
        return new[]
        {
            new CodexAccountUsageSnapshot(
                AccountId: "local-codex-cli",
                Email: "Codex 登录账号",
                MiniLabel: "Codex",
                Usage: CodexUsageSnapshot.Unavailable(error),
                UpdatedAt: null,
                IsStale: true,
                RefreshError: error)
        };
    }

    private static bool HasUsage(CodexAccountUsageSnapshot account) =>
        account.Usage.IsAvailable && account.Usage.Windows.Count > 0;

    public void Dispose() => _ccSwitch.Dispose();
}
