using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// macOS configuration store. The JSON file contains ordinary preferences only;
/// the API key itself is stored in the user's login Keychain.
/// </summary>
public sealed class MacConfigService
{
    private const string KeychainService = "com.deepseekbalancewidget.api-key";
    private const string KeychainAccount = "default";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "DeepSeekBalanceWidget");
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private AppConfig? _lastConfig;

    public MacConfigService() => _filePath = Path.Combine(_directory, "config.json");

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return _lastConfig = new AppConfig();
            var value = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_filePath), JsonOptions);
            return _lastConfig = value ?? new AppConfig();
        }
        catch
        {
            TryBackupCorruptFile();
            return _lastConfig = new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        lock (_writeLock)
        {
            Directory.CreateDirectory(_directory);
            string temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(config, JsonOptions), new UTF8Encoding(false));
            File.Move(temporaryPath, _filePath, overwrite: true);
            _lastConfig = config;
        }
    }

    public string? GetApiKey()
    {
        if (!string.Equals(_lastConfig?.ApiKeyEncrypted, "keychain", StringComparison.Ordinal))
            return null;

        return RunSecurity("find-generic-password", "-s", KeychainService, "-a", KeychainAccount, "-w")
            ?.TrimEnd('\r', '\n');
    }

    public void SetApiKey(AppConfig config, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            RunSecurity("delete-generic-password", "-s", KeychainService, "-a", KeychainAccount);
            config.ApiKeyEncrypted = null;
            return;
        }

        // `security` is the supported Keychain command-line client. It never writes
        // the password to stdout/stderr; passing it as an argument also avoids a shell.
        if (RunSecurity("add-generic-password", "-U", "-s", KeychainService,
            "-a", KeychainAccount, "-w", value) is null)
        {
            throw new InvalidOperationException("无法写入 macOS 钥匙串，请在“钥匙串访问”中检查权限。");
        }

        config.ApiKeyEncrypted = "keychain";
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Move(_filePath, _filePath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak");
        }
        catch { }
    }

    private static string? RunSecurity(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("/usr/bin/security")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null) return null;
            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }
}
