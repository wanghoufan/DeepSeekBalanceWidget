using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public sealed class CodexAppServerClient : ICodexUsageProvider
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly TimeSpan _timeout;

    public CodexAppServerClient(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task<CodexUsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        var ct = timeoutCts.Token;
        Process? process = null;

        try
        {
            process = StartAppServer();

            await WriteAsync(process, new
            {
                id = 1,
                method = "initialize",
                @params = new
                {
                    clientInfo = new
                    {
                        name = "DeepSeekBalanceWidget",
                        title = "DeepSeek Balance Widget",
                        version = "0.2.0"
                    }
                }
            }, ct);

            string initializeResponse = await ReadResponseAsync(process, 1, ct);
            if (HasError(initializeResponse))
                return CodexUsageSnapshot.Unavailable("Codex 初始化失败");

            await WriteAsync(process, new { method = "initialized" }, ct);
            await WriteAsync(process, new { id = 2, method = "account/rateLimits/read" }, ct);

            string usageResponse = await ReadResponseAsync(process, 2, ct);
            return CodexUsageParser.Parse(usageResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CodexUsageSnapshot.Unavailable("Codex 用量读取超时");
        }
        catch (Win32Exception)
        {
            return CodexUsageSnapshot.Unavailable("未找到 Codex CLI");
        }
        catch (Exception)
        {
            return CodexUsageSnapshot.Unavailable("Codex 用量读取失败");
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    process.StandardInput.Close();
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
            }
        }
    }

    private static Process StartAppServer()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveCodexExecutable(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8WithoutBom,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom
        };
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--stdio");

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Codex app-server 启动失败");
        }
        process.BeginErrorReadLine();
        return process;
    }

    private static string ResolveCodexExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        string localPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "OpenAI", "Codex", "bin", "codex.exe");
        return File.Exists(localPath) ? localPath : "codex.exe";
    }

    private static async Task WriteAsync(
        Process process,
        object message,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(message);
        await process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadResponseAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new IOException("Codex app-server 已退出");

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("id", out var id)
                    && id.TryGetInt32(out int value)
                    && value == expectedId)
                {
                    return line;
                }
            }
            catch (JsonException) { }
        }
    }

    private static bool HasError(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("error", out _);
    }
}
