using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekBalanceWidget.Services;

public sealed class MockBalanceService : IBalanceProvider
{
    private readonly string _scenario;
    private int _call;

    public MockBalanceService(string scenario) { _scenario = scenario; }

    public Task<string> GetBalanceJsonAsync(CancellationToken ct)
    {
        string json;
        if (_scenario == "sequence")
        {
            int i = _call++;
            json = i switch
            {
                0 => Json(110m, 10m, 100m, true),
                1 => Json(107.5m, 7.5m, 100m, true),
                2 => Json(8m, 3m, 5m, true),
                3 => Json(110m, 10m, 100m, false),
                _ => throw new HttpRequestException("模拟网络失败（序列末）")
            };
        }
        else
        {
            json = _scenario switch
            {
                "normal" => Json(110m, 10m, 100m, true),
                "drop" => Json(107.5m, 7.5m, 100m, true),
                "low" => Json(8m, 3m, 5m, true),
                "unavailable" => Json(110m, 10m, 100m, false),
                "error" => throw new HttpRequestException("模拟网络失败"),
                _ => Json(110m, 10m, 100m, true)
            };
        }
        return Task.FromResult(json);
    }

    private static string Json(decimal total, decimal granted, decimal topped, bool available)
    {
        string a = available ? "true" : "false";
        return "{\"is_available\":" + a + ",\"balance_infos\":[{\"currency\":\"CNY\","
             + "\"total_balance\":\"" + total.ToString("0.00") + "\","
             + "\"granted_balance\":\"" + granted.ToString("0.00") + "\","
             + "\"topped_up_balance\":\"" + topped.ToString("0.00") + "\"}]}";
    }
}
