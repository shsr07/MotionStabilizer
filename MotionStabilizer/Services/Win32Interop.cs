using System.Runtime.InteropServices;

namespace MotionStabilizer.Services;

/// <summary>
/// Win32 API interop for overlay window management, hotkey registration, and screen info.
/// This is a PURE EXTERNAL approach — no DLL injection, no process modification.
/// The overlay simply draws on top using a transparent topmost window.
/// </summary>
internal static class Win32Interop
{
    #region Window Styles

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TOPMOST = 0x00000008;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    /// <summary>
    /// Makes a WPF window click-through (mouse events pass to the window below),
    /// non-activating, and a tool window (no taskbar entry).
    /// 
    /// IMPORTANT: Do NOT call SetLayeredWindowAttributes here. WPF's
    /// AllowsTransparency="True" already manages per-pixel alpha via
    /// UpdateLayeredWindow. Calling SetLayeredWindowAttributes with LWA_ALPHA
    /// would override WPF's per-pixel alpha, making transparent areas opaque
    /// and hiding all drawn shapes.
    /// </summary>
    public static void MakeOverlayWindow(IntPtr hwnd)
    {
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        // Only add the style flags we need. WS_EX_LAYERED is already set by
        // WPF when AllowsTransparency="True", but OR-ing it again is harmless.
        extendedStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
    }

    #endregion

    #region Hotkey Registration

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Convert a key string like "F1", "R" to a virtual key code.</summary>
    public static uint KeyNameToVk(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return 0;

        if (keyName.StartsWith("F") && int.TryParse(keyName[1..], out int fn))
            return (uint)(0x6F + fn);

        if (keyName.Length == 1)
        {
            char c = keyName.ToUpper()[0];
            if (c >= 'A' && c <= 'Z') return (uint)(0x41 + (c - 'A'));
            if (c >= '0' && c <= '9') return (uint)(0x30 + (c - '0'));
        }

        if (keyName.StartsWith("NumPad") && int.TryParse(keyName[6..], out int np))
            return (uint)(0x60 + np);

        return keyName.ToUpper() switch
        {
            "SPACE" => 0x20,
            "ENTER" or "RETURN" => 0x0D,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "HOME" => 0x24,
            "END" => 0x23,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            _ => 0
        };
    }

    #endregion

    #region Screen Info

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();

    public const int HORZRES = 8;
    public const int VERTRES = 10;
    public const int DESKTOPHORZRES = 118;
    public const int DESKTOPVERTRES = 117;

    /// <summary>System DPI scale factor (1.0 at 100%, 1.25 at 125%, etc.).</summary>
    public static double GetDpiScale()
    {
        uint dpi = GetDpiForSystem();
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    /// <summary>Physical screen width in pixels (correct under DPI scaling).</summary>
    public static int GetScreenWidth()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        int w = GetDeviceCaps(hdc, DESKTOPHORZRES);
        if (w == 0) w = GetDeviceCaps(hdc, HORZRES);
        ReleaseDC(IntPtr.Zero, hdc);
        return w;
    }

    public static int GetScreenHeight()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        int h = GetDeviceCaps(hdc, DESKTOPVERTRES);
        if (h == 0) h = GetDeviceCaps(hdc, VERTRES);
        ReleaseDC(IntPtr.Zero, hdc);
        return h;
    }

    #endregion

    #region Foreground Window

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    public static RECT? GetForegroundWindowRect()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        var rect = new RECT();
        if (!GetWindowRect(hwnd, ref rect)) return null;

        if (rect.Right - rect.Left <= 0 || rect.Bottom - rect.Top <= 0)
            return null;

        return rect;
    }

    #endregion

    #region Cursor Tracking

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public const int VK_LBUTTON = 0x01;
    public const int VK_ESCAPE = 0x1B;

    #endregion
}
