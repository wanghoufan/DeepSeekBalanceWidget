using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public interface ICodexUsageProvider
{
    Task<CodexUsageSnapshot> GetUsageAsync(CancellationToken cancellationToken);
}
