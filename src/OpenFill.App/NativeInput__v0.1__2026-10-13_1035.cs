// OpenFill - Metadata: wersja 0.1, data 2026-10-13 10:35
using System.Runtime.InteropServices;

namespace OpenFill.App;

/// <summary>Win32 helpers: bring a window to the front and press real keys in it (used for the browser's saved-password list).</summary>
internal static class NativeInput
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    public static IntPtr Foreground => GetForegroundWindow();

    /// <summary>Brings the window to the front. True when it is the foreground window afterwards.</summary>
    public static bool BringToFront(IntPtr h)
    {
        if (h == IntPtr.Zero) return false;
        if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
        if (GetForegroundWindow() == h) return true;
        var fg = GetForegroundWindow();
        var fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
        var me = GetCurrentThreadId();
        var attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try
        {
            keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero); // an Alt tap lets the call through
            SetForegroundWindow(h);
        }
        finally { if (attached) AttachThreadInput(me, fgThread, false); }
        return GetForegroundWindow() == h;
    }

    public static bool TryVk(string key, out byte vk)
    {
        vk = key switch { "ArrowDown" => 0x28, "ArrowUp" => 0x26, "Enter" => 0x0D, "Tab" => 0x09, "Escape" => 0x1B, _ => (byte)0 };
        return vk != 0;
    }

    public static void Press(byte vk)
    {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
    }
}