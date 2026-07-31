using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekBalanceWidget.Services;

public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public bool IsAuthFailure { get; }
    public ApiException(HttpStatusCode code, string message, bool isAuth) : base(message)
    { StatusCode = code; IsAuthFailure = isAuth; }
}

public sealed class DeepSeekApiClient : IBalanceProvider
{
    private const string Url = "https://api.deepseek.com/user/balance";
    private HttpClient _http;

    public DeepSeekApiClient(string apiKey)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> GetBalanceJsonAsync(CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage? resp = null;
            try
            {
                resp = await _http.GetAsync(Url, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (resp.StatusCode == HttpStatusCode.OK) return body;

                if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new ApiException(resp.StatusCode, "认证/权限失败", true);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var ra = resp.Headers.RetryAfter;
                    if (ra is not null && attempt < 2)
                    {
                        TimeSpan? wait = ra.Delta
                            ?? (ra.Date.HasValue ? ra.Date.Value - DateTimeOffset.UtcNow : null);
                        if (wait is TimeSpan w && w > TimeSpan.Zero && w <= TimeSpan.FromSeconds(30))
                        {
                            await Task.Delay(w, ct);
                            continue;
                        }
                    }
                    throw new ApiException(resp.StatusCode, "请求过于频繁，请稍后再试", false);
                }

                if ((resp.StatusCode >= HttpStatusCode.InternalServerError
                     || resp.StatusCode == HttpStatusCode.RequestTimeout) && attempt < 2)
                {
                    await Task.Delay(Random.Shared.Next(500, 1001), ct);
                    continue;
                }

                throw new ApiException(resp.StatusCode, $"服务返回状态码 {(int)resp.StatusCode}", false);
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(Random.Shared.Next(500, 1001), ct);
            }
            catch (HttpRequestException)
            {
                throw new ApiException(0, "网络请求失败", false);
            }
            finally { resp?.Dispose(); }
        }
    }
}
