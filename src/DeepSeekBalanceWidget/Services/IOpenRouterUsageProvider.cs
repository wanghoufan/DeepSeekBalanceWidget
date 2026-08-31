using System.Threading;
using System.Threading.Tasks;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>OpenRouter account credits data source.</summary>
public interface IOpenRouterUsageProvider
{
    Task<OpenRouterUsageSnapshot> GetUsageAsync(CancellationToken cancellationToken);
}
