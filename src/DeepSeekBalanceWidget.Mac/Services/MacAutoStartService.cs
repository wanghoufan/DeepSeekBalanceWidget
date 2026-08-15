using System.Xml.Linq;

namespace DeepSeekBalanceWidget.Services;

public static class MacAutoStartService
{
    private const string Label = "com.deepseekbalancewidget";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", Label + ".plist");

    public static bool IsEnabled() => File.Exists(PlistPath);

    public static void Set(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(PlistPath)) File.Delete(PlistPath);
            return;
        }

        string? applicationPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(applicationPath))
            throw new InvalidOperationException("无法确定应用程序路径，不能启用登录时启动。");

        Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
        var plist = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN",
                "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "Label"), new XElement("string", Label),
                    new XElement("key", "ProgramArguments"), new XElement("array",
                        new XElement("string", applicationPath)),
                    new XElement("key", "RunAtLoad"), new XElement("true"))));
        plist.Save(PlistPath);
    }
}
