using System.Runtime.InteropServices;

namespace MotionStabilizer.Services;

/// <summary>
/// Win32 API interop for overlay window management, hotkey registration, and screen info.
/// This is a PURE EXTERNAL approach — no DLL injection, no process modification.
/// The overlay simply draws on top using a transparent topmost window.
/// </summary>
public static class Win32Interop
{
    #region Window Styles

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TOPMOST = 0x00000008;

    // SetWindowPos flags
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOOWNERZORDER = 0x0200;
    public const uint SWP_NOREDRAW = 0x0008;

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
        // Explicitly set z-order to topmost — setting WS_EX_TOPMOST via
        // SetWindowLong alone is not always sufficient, especially after
        // a fullscreen game has changed the z-order.
        ReassertTopmost(hwnd);
    }

    /// <summary>
    /// Re-assert the window's z-order to HWND_TOPMOST. Games like Cyberpunk 2077
    /// continuously push their own window to the top, so the overlay must
    /// periodically re-assert its topmost position.
    /// </summary>
    public static void ReassertTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

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

    [DllImport("user32.dll")]
    public static extern short VkKeyScan(char ch);

    /// <summary>Convert a key string like "F1", "R", "," to a virtual key code.</summary>
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
            // OEM characters (, . ; ' [ ] - / \ ` =) via VkKeyScan
            short vks = VkKeyScan(c);
            if (vks != -1) return (uint)(vks & 0xFF);
            // Try lowercase too
            vks = VkKeyScan(keyName[0]);
            if (vks != -1) return (uint)(vks & 0xFF);
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

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int HORZRES = 8;
    public const int VERTRES = 10;
    public const int DESKTOPHORZRES = 118;
    public const int DESKTOPVERTRES = 117;

    // Virtual screen metrics (span all monitors)
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    // Per-monitor DPI
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    public enum MONITOR_DPI_TYPE : int
    {
        MDT_EFFECTIVE_DPI = 0,
        MDT_RAW_DPI = 1,
        MDT_DEFAULT = MDT_EFFECTIVE_DPI
    }

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY);

    // ── EnumDisplayDevices (PnP device identification) ──────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;   // GDI name: \\.\DISPLAY1
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString; // Model: DELL S2721DGF
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;    // PnP path: \\?\DISPLAY#DELF0F81#...
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    /// <summary>
    /// Build a map from GDI device name (\\.\DISPLAY1) to PnP DeviceID and
    /// friendly name (model string) via EnumDisplayDevices. This provides a
    /// more stable identifier than the GDI name alone.
    /// </summary>
    private static Dictionary<string, (string deviceId, string friendlyName)> BuildPnpDeviceMap()
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        uint adapterIdx = 0;
        var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        while (EnumDisplayDevices(null, adapterIdx, ref adapter, 0))
        {
            uint monitorIdx = 0;
            var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            while (EnumDisplayDevices(adapter.DeviceName, monitorIdx, ref mon, 0))
            {
                if ((mon.StateFlags & 0x1) != 0 || !string.IsNullOrEmpty(mon.DeviceID))
                {
                    var friendly = string.IsNullOrEmpty(mon.DeviceString) ? "" : mon.DeviceString;
                    map[adapter.DeviceName] = (mon.DeviceID, friendly);
                }
                monitorIdx++;
            }
            adapterIdx++;
        }
        return map;
    }

    /// <summary>Enumerates all display monitors with bounds, DPI, and PnP device info.</summary>
    public static List<MonitorInfo> GetAllMonitors()
    {
        var pnpMap = BuildPnpDeviceMap();
        var monitors = new List<MonitorInfo>();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var b = screen.Bounds;
            double dpiScale = GetDpiScaleForRect(b.X, b.Y, b.Right, b.Bottom);
            pnpMap.TryGetValue(screen.DeviceName, out var pnp);
            monitors.Add(new MonitorInfo(
                screen.DeviceName, pnp.deviceId ?? "", pnp.friendlyName ?? "",
                b.X, b.Y, b.Width, b.Height, dpiScale));
        }
        return monitors;
    }

    // ── Target monitor filtering ─────────────────────────────────────────

    /// <summary>
    /// Returns all monitors, or only the monitor whose PnP DeviceID matches
    /// <paramref name="targetDeviceId"/>. Uses layered matching: exact PnP
    /// path first, then EDID vendor+product fallback, then all monitors.
    /// </summary>
    public static List<MonitorInfo> GetTargetMonitors(string? targetDeviceId)
        => SelectTargetMonitors(GetAllMonitors(), targetDeviceId);

    /// <summary>Pure monitor filtering with layered matching — unit-testable.</summary>
    public static List<MonitorInfo> SelectTargetMonitors(
        IReadOnlyList<MonitorInfo> monitors,
        string? targetDeviceId)
    {
        if (string.IsNullOrWhiteSpace(targetDeviceId))
            return new List<MonitorInfo>(monitors);

        // Layer 1: exact PnP DeviceID match (same port, same monitor)
        var exact = monitors
            .Where(m => string.Equals(m.DeviceId, targetDeviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count > 0) return exact;

        // Layer 2: EDID vendor+product match (handles port change)
        var targetEdid = ExtractEdidPart(targetDeviceId);
        if (!string.IsNullOrEmpty(targetEdid))
        {
            var edid = monitors
                .Where(m => string.Equals(ExtractEdidPart(m.DeviceId), targetEdid,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (edid.Count == 1) return edid;
        }

        // Layer 3: safe fallback — all monitors
        return new List<MonitorInfo>(monitors);
    }

    /// <summary>
    /// Extract the EDID vendor+product segment from a PnP DeviceID path.
    /// Example: "\\?\DISPLAY#DELF0F81#5&..." → "DELF0F81"
    /// </summary>
    public static string ExtractEdidPart(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return "";
        // PnP path format: \\?\DISPLAY#VENDOR_PRODUCT#INSTANCE#UID#{GUID}
        var segments = deviceId.Split('#');
        if (segments.Length >= 2)
            return segments[1]; // VENDOR_PRODUCT segment
        return "";
    }

    /// <summary>Per-monitor DPI scale for the monitor containing the given rect.</summary>
    public static double GetDpiScaleForRect(int left, int top, int right, int bottom)
    {
        var rc = new RECT { Left = left, Top = top, Right = right, Bottom = bottom };
        IntPtr hmon = MonitorFromRect(ref rc, MONITOR_DEFAULTTONEAREST);
        if (hmon != IntPtr.Zero && GetDpiForMonitor(hmon, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
            return dpiX == 0 ? 1.0 : dpiX / 96.0;
        return GetDpiScale();
    }

    public readonly struct MonitorInfo
    {
        public readonly string DeviceName;   // GDI name: \\.\DISPLAY1
        public readonly string DeviceId;     // PnP path: \\?\DISPLAY#DELF0F81#...
        public readonly string FriendlyName; // Model: DELL S2721DGF
        public readonly int X, Y, Width, Height;
        public readonly double DpiScale;

        /// <summary>Legacy constructor (no PnP info) — for tests and fallbacks.</summary>
        public MonitorInfo(int x, int y, int w, int h, double dpiScale = 0)
            : this("", "", "", x, y, w, h, dpiScale) { }

        public MonitorInfo(string deviceName, string deviceId, string friendlyName,
            int x, int y, int w, int h, double dpiScale = 0)
        {
            DeviceName = deviceName ?? "";
            DeviceId = deviceId ?? "";
            FriendlyName = friendlyName ?? "";
            X = x; Y = y; Width = w; Height = h;
            DpiScale = dpiScale > 0 ? dpiScale : 1.0;
        }
    }

    /// <summary>System DPI scale factor (1.0 at 100%, 1.25 at 125%, etc.).</summary>
    public static double GetDpiScale()
    {
        uint dpi = GetDpiForSystem();
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    /// <summary>Per-window DPI scale factor — correct for mixed-DPI multi-monitor setups.</summary>
    public static double GetDpiScaleForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return GetDpiScale();
        uint dpi = GetDpiForWindow(hwnd);
        return dpi == 0 ? GetDpiScale() : dpi / 96.0;
    }

    /// <summary>Virtual screen width in pixels (covers all monitors).</summary>
    public static int GetScreenWidth()
    {
        int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        if (w > 0) return w;
        // Fallback to GDI for older systems or single-monitor
        IntPtr hdc = GetDC(IntPtr.Zero);
        w = GetDeviceCaps(hdc, DESKTOPHORZRES);
        if (w == 0) w = GetDeviceCaps(hdc, HORZRES);
        ReleaseDC(IntPtr.Zero, hdc);
        return w;
    }

    /// <summary>Virtual screen height in pixels (covers all monitors).</summary>
    public static int GetScreenHeight()
    {
        int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (h > 0) return h;
        // Fallback to GDI for older systems or single-monitor
        IntPtr hdc = GetDC(IntPtr.Zero);
        h = GetDeviceCaps(hdc, DESKTOPVERTRES);
        if (h == 0) h = GetDeviceCaps(hdc, VERTRES);
        ReleaseDC(IntPtr.Zero, hdc);
        return h;
    }

    /// <summary>Virtual screen origin X in pixels (can be negative if secondary monitors are to the left).</summary>
    public static int GetVirtualScreenX() => GetSystemMetrics(SM_XVIRTUALSCREEN);

    /// <summary>Virtual screen origin Y in pixels (can be negative if secondary monitors are above).</summary>
    public static int GetVirtualScreenY() => GetSystemMetrics(SM_YVIRTUALSCREEN);

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
    public const int VK_W = 0x57;
    public const int VK_S = 0x53;
    public const int VK_A = 0x41;
    public const int VK_D = 0x44;

    #endregion

    #region Raw Mouse Input

    public const int WM_INPUT = 0x00FF;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIM_TYPEMOUSE = 0;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RIDEV_REMOVE = 0x00000001;
    public const ushort MOUSE_MOVE_ABSOLUTE = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct RAWMOUSE
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public uint Buttons;
        [FieldOffset(8)] public uint RawButtons;
        [FieldOffset(12)] public int LastX;
        [FieldOffset(16)] public int LastY;
        [FieldOffset(20)] public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWMOUSE Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] devices,
        uint numberDevices,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        out RAWINPUT data,
        ref uint size,
        uint headerSize);

    public static bool RegisterRawMouseInput(IntPtr target)
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RIDEV_INPUTSINK,
                Target = target
            }
        };

        return RegisterRawInputDevices(
            devices,
            1,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    /// <summary>
    /// Unregister raw mouse input. Should be called before the target window
    /// is destroyed to ensure clean system-level cleanup.
    /// </summary>
    public static bool UnregisterRawMouseInput()
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RIDEV_REMOVE,
                Target = IntPtr.Zero
            }
        };

        return RegisterRawInputDevices(
            devices,
            1,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    public static bool TryGetRawMouseDelta(IntPtr lParam, out int deltaX, out int deltaY)
    {
        deltaX = 0;
        deltaY = 0;

        uint size = (uint)Marshal.SizeOf<RAWINPUT>();
        uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        uint copied = GetRawInputData(lParam, RID_INPUT, out var input, ref size, headerSize);
        if (copied == uint.MaxValue || copied == 0)
            return false;

        if (input.Header.Type != RIM_TYPEMOUSE ||
            (input.Mouse.Flags & MOUSE_MOVE_ABSOLUTE) != 0)
            return false;

        deltaX = input.Mouse.LastX;
        deltaY = input.Mouse.LastY;
        return deltaX != 0 || deltaY != 0;
    }

    #endregion
}
