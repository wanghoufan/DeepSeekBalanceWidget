using System;
using System.Threading;
using System.Threading.Tasks;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// OpenRouter provider placeholder. The settings card and shared contracts are
/// deliberately in place before a production credits endpoint is enabled.
/// </summary>
public sealed class OpenRouterUsageProvider : IOpenRouterUsageProvider, IDisposable
{
    public const string ProviderName = "OpenRouter";
    public const string CreditsUrl = "https://openrouter.ai/api/v1/credits";

    private readonly string? _explicitApiKey;

    public OpenRouterUsageProvider(string? explicitApiKey = null)
    {
        _explicitApiKey = explicitApiKey;
    }

    public string? ResolveApiKey() =>
        string.IsNullOrWhiteSpace(_explicitApiKey) ? null : _explicitApiKey;

    public Task<OpenRouterUsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OpenRouterUsageSnapshot.Unavailable("OpenRouter 额度监测尚未接入"));
    }

    public void Dispose() { }
}
