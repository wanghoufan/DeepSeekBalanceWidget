using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public interface ICodexAccountsUsageProvider
{
    Task<IReadOnlyList<CodexAccountUsageSnapshot>> GetUsagesAsync(
        CancellationToken cancellationToken);
}

