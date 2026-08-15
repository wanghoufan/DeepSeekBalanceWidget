using System.Runtime.InteropServices;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// A native NSStatusItem with a text title. Avalonia's cross-platform tray API
/// supports an icon and tooltip, but macOS needs NSStatusItem directly to show a
/// live value in the menu bar like Weather does.
/// </summary>
public sealed class MacMenuBarBalance : IDisposable
{
    private const double VariableStatusItemLength = -1d;
    private readonly IntPtr _statusBar;
    private readonly IntPtr _statusItem;
    private readonly IntPtr _button;
    private bool _disposed;

    private MacMenuBarBalance(IntPtr statusBar, IntPtr statusItem, IntPtr button)
    {
        _statusBar = statusBar;
        _statusItem = statusItem;
        _button = button;
    }

    public static MacMenuBarBalance? Create()
    {
        if (!OperatingSystem.IsMacOS()) return null;

        IntPtr statusBar = SendIntPtr(GetClass("NSStatusBar"), GetSelector("systemStatusBar"));
        IntPtr statusItem = SendIntPtrDouble(
            statusBar, GetSelector("statusItemWithLength:"), VariableStatusItemLength);
        IntPtr button = SendIntPtr(statusItem, GetSelector("button"));
        return statusBar == IntPtr.Zero || statusItem == IntPtr.Zero || button == IntPtr.Zero
            ? null
            : new MacMenuBarBalance(statusBar, statusItem, button);
    }

    public void Update(string title, string tooltip)
    {
        if (_disposed) return;
        SendVoidIntPtr(_button, GetSelector("setTitle:"), CreateString(title));
        SendVoidIntPtr(_button, GetSelector("setToolTip:"), CreateString(tooltip));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SendVoidIntPtr(_statusBar, GetSelector("removeStatusItem:"), _statusItem);
    }

    private static IntPtr CreateString(string value)
    {
        IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return SendIntPtrIntPtr(
                GetClass("NSString"), GetSelector("stringWithUTF8String:"), utf8);
        }
        finally { Marshal.FreeCoTaskMem(utf8); }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr GetSelector(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtrIntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtrDouble(IntPtr receiver, IntPtr selector, double argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendVoidIntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);
}
