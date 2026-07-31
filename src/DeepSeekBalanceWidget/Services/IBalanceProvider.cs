using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekBalanceWidget.Services;

public interface IBalanceProvider
{
    Task<string> GetBalanceJsonAsync(CancellationToken ct);
}
