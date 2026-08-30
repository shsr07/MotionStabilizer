using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MotionStabilizer.Models;
using MotionStabilizer.Overlay;
using MotionStabilizer.Services;
using MotionStabilizer.Views;

namespace MotionStabilizer;

/// <summary>
/// Application entry point. Manages the overlay window, main settings window,
/// hotkey manager, config store, and system tray.
/// 
/// Architecture: All config state lives in <see cref="Config"/> (a <see cref="ConfigStore"/>).
/// Config objects implement <see cref="INotifyPropertyChanged"/>, so any property
/// change automatically fires <see cref="ConfigStore.Changed"/>, which triggers
/// <see cref="RefreshOverlay"/> and <see cref="ScheduleAutoSave"/>.
/// Pages and hotkey handlers only need to set config properties — no explicit
/// RefreshOverlay() calls are needed.
/// </summary>
public partial class App : Application
{
    // Core services
    public static HotkeyManager Hotkeys { get; } = new();
    public static ConfigManager ConfigMgr { get; } = new();

    // ── Config Store (single source of truth) ──
    /// <summary>
    /// Centralized config store. All config objects are observable — setting any
    /// property automatically triggers overlay re-render and debounced auto-save.
    /// </summary>
    public static ConfigStore Config { get; } = new();

    // ── Backward-compatible proxy properties (delegate to ConfigStore) ──
    // These exist so that existing code using App.OverlayConfig etc. continues
    // to work. New code should prefer App.Config.Overlay directly.
    public static OverlayConfig OverlayConfig
    {
        get => Config.Overlay;
        set => Config.Overlay = value;
    }
    public static CrosshairConfig CrosshairConfig
    {
        get => Config.Crosshair;
        set => Config.Crosshair = value;
    }
    public static ClockConfig ClockConfig
    {
        get => Config.Clock;
        set => Config.Clock = value;
    }
    public static HotkeyConfig HotkeyConfig
    {
        get => Config.Hotkeys;
        set => Config.Hotkeys = value;
    }
    public static AppConfig AppConfig
    {
        get => Config.App;
        set => Config.App = value;
    }

    // Windows
    public static OverlayWindow? OverlayWin { get; private set; }
    public static MainWindow? MainWin { get; private set; }

    // Tray
    private TrayService? _tray;

    // ── Single instance ──
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private const string SingleInstanceMutexName = @"Local\MotionStabilizer.SingleInstance";

    /// <summary>
    /// System-wide registered message id. When a second instance starts, it posts
    /// this message to all top-level windows; the running instance's MainWindow
    /// responds by showing and activating itself.
    /// </summary>
    public static int ShowMainWindowMessage { get; private set; }

    /// <summary>
    /// Look up a resource string, falling back to a hardcoded value so that
    /// critical error dialogs never crash on a missing resource key.
    /// </summary>
    private static string Res(string key, string fallback) =>
        Current.TryFindResource(key) as string ?? fallback;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Single-instance guard: must run before any window is created ──
        ShowMainWindowMessage =
            unchecked((int)Win32Interop.RegisterWindowMessage("MotionStabilizer.ShowMainWindow"));
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running: ask it to show its main window, then exit.
            if (ShowMainWindowMessage != 0)
                Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST,
                    (uint)ShowMainWindowMessage, IntPtr.Zero, IntPtr.Zero);
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }
        _ownsSingleInstanceMutex = true;

        // ── Global exception Handlers ──
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;

        try
        {
            StartupInternal();
        }
        catch (Exception ex)
        {
            string msg = string.Format(
                    Res("Error_StartupFail", "Startup failed:\n\n{0}: {1}\n\n{2}"),
                    ex.GetType().Name, ex.Message, ex.StackTrace)
                .Replace("\\n", "\n");
            MessageBox.Show(
                msg,
                "Motion Stabilizer - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void StartupInternal()
    {
        // Subscribe to config changes: auto-refresh overlay + debounced auto-save
        Config.Changed += OnConfigChanged;

        // Load saved configs
        Config.App = ConfigManager.LoadAppConfig();
        Config.Hotkeys = ConfigManager.LoadHotkeys();

        // Try to load default profile
        var defaultProfile = ConfigManager.LoadProfile("Default");
        if (defaultProfile != null)
        {
            Config.ApplyProfile(defaultProfile);
        }

        // Apply language
        ApplyLanguage(Config.App.Language);

        // Create overlay window (invisible rendering layer)
        try
        {
            OverlayWin = new OverlayWindow();
            OverlayWin.Show();
            OverlayWin.UpdateConfigs(Config.Overlay, Config.Crosshair, Config.Clock);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Overlay window failed: {ex.Message}");
        }

        // Create tray icon
        try
        {
            _tray = new TrayService();
            _tray.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray service failed: {ex.Message}");
        }

        // Create main settings window
        MainWin = new MainWindow();

        // Apply saved UI scale
        ApplyUIScale(Config.App.Scale);

        // Initialize hotkeys with the main window
        try
        {
            Hotkeys.Initialize(MainWin);
            RegisterAllHotkeys();
            Hotkeys.HotkeyPressed += OnHotkeyPressed;
            Hotkeys.RegistrationFailed += OnHotkeyRegistrationFailed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hotkey init failed: {ex.Message}");
        }

        // Show main window. With tray-start requested the window is created but
        // deliberately never shown — a Show-then-Hide pair here flashed a
        // taskbar button on every launch. Tray "Show" brings it up later
        // (Loaded fires at that point; the hotkey hook uses EnsureHandle, so it
        // does not depend on the window being visible).
        if (!Config.App.MinimizeToTrayOnStart)
        {
            MainWin.Show();
        }

        ShowHotkeyOccupancyNoticeOnce();
    }

    /// <summary>
    /// First-run notice: the default hotkeys (F1–F7, F9, F10) carry no modifier
    /// keys, and RegisterHotKey intercepts rather than observes — while the app
    /// runs, in-game F1 help / F5 quicksave etc. are silently swallowed. Show a
    /// one-time acknowledgment dialog (persisted via AppConfig) and point users
    /// at the Hotkeys page. Deferred to ApplicationIdle so the main window
    /// paints first.
    /// </summary>
    private void ShowHotkeyOccupancyNoticeOnce()
    {
        if (Config.App.HotkeyWarningAcknowledged) return;

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            CustomMessageBox.Show(
                Res("Hotkeys_GlobalWarn_Title", "Global Hotkey Notice"),
                Res("Hotkeys_GlobalWarn_Msg", ""),
                Res("Hotkeys_GlobalWarn_Ack", "I Understand"));

            Config.App.HotkeyWarningAcknowledged = true;
            // Persist immediately — a crash right after the dialog must not
            // bring the notice back, and the debounced auto-save may lag.
            ConfigManager.SaveAppConfig(Config.App);
        });
    }

    /// <summary>
    /// Handler for ConfigStore.Changed: refreshes overlay and schedules auto-save.
    /// Called automatically when any config property changes — no manual calls needed.
    /// </summary>
    private static void OnConfigChanged()
    {
        RefreshOverlay();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        AppendErrorLog("Unhandled", e.Exception);

        // A recurring fault must not spam modal dialogs — each one steals focus
        // from a fullscreen game. Throttle to 3 dialogs per minute; the log
        // keeps recording every occurrence either way.
        if (DateTime.UtcNow - _errorDialogWindowStart > TimeSpan.FromMinutes(1))
        {
            _errorDialogWindowStart = DateTime.UtcNow;
            _errorDialogCount = 0;
        }
        if (++_errorDialogCount > 3)
            return;

        string msg = string.Format(
                Res("Error_Short_Msg", "An internal error occurred. Details were written to error.log.\n\n{0}: {1}"),
                e.Exception.GetType().Name, e.Exception.Message)
            .Replace("\\n", "\n");
        MessageBox.Show(
            msg,
            Res("Error_Short_Title", "Motion Stabilizer - Error"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private readonly object _errorLogLock = new();
    private int _errorDialogCount;
    private DateTime _errorDialogWindowStart = DateTime.MinValue;

    /// <summary>Append one timestamped entry to error.log next to the config files. Never throws.</summary>
    private void AppendErrorLog(string kind, Exception ex)
    {
        try
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {kind}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n---\n";
            lock (_errorLogLock)
            {
                File.AppendAllText(Path.Combine(ConfigManager.DataDirectory, "error.log"), line);
            }
        }
        catch
        {
            // Logging must never become a second crash source
        }
    }

    private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppendErrorLog("Fatal", ex);
            string msg = string.Format(
                    Res("Error_Short_Msg", "An internal error occurred. Details were written to error.log.\n\n{0}: {1}"),
                    ex.GetType().Name, ex.Message)
                .Replace("\\n", "\n");
            MessageBox.Show(
                msg,
                Res("Error_Short_Title", "Motion Stabilizer - Fatal Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>Apply language resource dictionary.</summary>
    public static void ApplyLanguage(Language lang)
    {
        var dict = Current.Resources.MergedDictionaries;
        var langSource = lang == Language.English
            ? "resources/strings.en-us.xaml"
            : "resources/strings.zh-cn.xaml";

        // Remove any existing language dictionary by matching its Source path
        for (int i = dict.Count - 1; i >= 0; i--)
        {
            var src = dict[i].Source?.OriginalString;
            if (src != null && src.Contains("strings.", StringComparison.OrdinalIgnoreCase)
                && (src.Contains("en-us", StringComparison.OrdinalIgnoreCase)
                    || src.Contains("zh-cn", StringComparison.OrdinalIgnoreCase)))
            {
                dict.RemoveAt(i);
            }
        }

        var langDict = new ResourceDictionary
        {
            Source = new Uri(lang == Language.English
                ? "Resources/Strings.en-US.xaml"
                : "Resources/Strings.zh-CN.xaml",
                UriKind.Relative)
        };
        dict.Add(langDict);
    }

    /// <summary>Register all hotkeys from config.</summary>
    public void RegisterAllHotkeys()
    {
        Hotkeys.UnregisterAll();

        var hk = Config.Hotkeys;
        // Note: No explicit RefreshOverlay() calls needed here — setting any
        // config property fires ConfigStore.Changed, which automatically
        // triggers overlay re-render and debounced auto-save.
        Hotkeys.Register(hk.ToggleOverlay, () => { OverlayConfig.IsVisible = !OverlayConfig.IsVisible; });
        Hotkeys.Register(hk.ToggleCrosshair, () => { CrosshairConfig.IsVisible = !CrosshairConfig.IsVisible; });
        Hotkeys.Register(hk.ToggleClock, () => { ClockConfig.IsVisible = !ClockConfig.IsVisible; });
        Hotkeys.Register(hk.CycleDisplayMode, () => { OverlayConfig.Mode = OverlayConfig.Mode == DisplayMode.Window ? DisplayMode.Stretch : DisplayMode.Window; });
        Hotkeys.Register(hk.CycleSplitScreen, CycleSplitScreen);
        Hotkeys.Register(hk.CycleOverlayShape, () => { OverlayConfig.Shape = (OverlayShape)(((int)OverlayConfig.Shape + 1) % 5); });
        Hotkeys.Register(hk.CycleCrosshairShape, () => { CrosshairConfig.Shape = (CrosshairShape)(((int)CrosshairConfig.Shape + 1) % 3); });
        Hotkeys.Register(hk.CycleAspectRatio, CycleAspectRatio);
        Hotkeys.Register(hk.CycleOpacityMode, () => { OverlayConfig.OpacityMode = (EdgeOpacityMode)(((int)OverlayConfig.OpacityMode + 1) % 2); });
        Hotkeys.Register(hk.CycleTargetMonitor, CycleTargetMonitor);
        Hotkeys.Register(hk.CycleOverlayColor, () => { OverlayConfig.ColorPreset = NextColorPreset(OverlayConfig.ColorPreset); });
        Hotkeys.Register(hk.CycleCrosshairColor, () => { CrosshairConfig.ColorPreset = NextColorPreset(CrosshairConfig.ColorPreset); });
    }

    private void CycleSplitScreen()
    {
        var cfg = MainWin?.CurrentPage == "Crosshair" ? (object)CrosshairConfig : OverlayConfig;
        if (cfg is OverlayConfig oc)
        {
            oc.Split = oc.Split == SplitScreen.None ? SplitScreen.Vertical :
                       oc.Split == SplitScreen.Vertical ? SplitScreen.Horizontal : SplitScreen.None;
        }
        else if (cfg is CrosshairConfig cc)
        {
            cc.Split = cc.Split == SplitScreen.None ? SplitScreen.Vertical :
                       cc.Split == SplitScreen.Vertical ? SplitScreen.Horizontal : SplitScreen.None;
        }
    }

    private void CycleAspectRatio()
    {
        var cfg = MainWin?.CurrentPage == "Crosshair" ? (object)CrosshairConfig : OverlayConfig;
        if (cfg is OverlayConfig oc)
            oc.AspectRatio = (AspectRatio)(((int)oc.AspectRatio + 1) % 4);
        else if (cfg is CrosshairConfig cc)
            cc.AspectRatio = (AspectRatio)(((int)cc.AspectRatio + 1) % 4);
    }

    /// <summary>
    /// Cycle a color preset in the order Red → Green → Blue → Custom → Red.
    /// Enum order defines the cycle, so no explicit mapping is needed.
    /// </summary>
    private static ColorPreset NextColorPreset(ColorPreset current) =>
        (ColorPreset)(((int)current + 1) % 4);

    /// <summary>
    /// Cycle through available monitors (All → Monitor 1 → Monitor 2 → … → All).
    /// Uses SelectTargetMonitors for current-position detection so that EDID
    /// fallback (port change) is handled consistently with rendering.
    /// </summary>
    private void CycleTargetMonitor()
    {
        var monitors = Win32Interop.GetAllMonitors();
        if (monitors.Count <= 1) return;

        // Build cycle list: "All" first, then each monitor's DeviceId
        var ids = new List<string> { "" };
        ids.AddRange(monitors.Select(m => m.DeviceId));

        // Use the same layered matching as rendering to find current position
        string current = AppConfig.TargetMonitor;
        int idx = 0; // default to "All"
        if (!string.IsNullOrEmpty(current))
        {
            var matched = Win32Interop.SelectTargetMonitors(monitors, current);
            if (matched.Count == 1)
            {
                // Found a specific monitor — find its position in the cycle list
                idx = ids.FindIndex(id =>
                    string.Equals(id, matched[0].DeviceId, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) idx = 0;
            }
            // If matched.Count > 1 (fallback to all), idx stays 0
        }

        int next = (idx + 1) % ids.Count;
        AppConfig.TargetMonitor = ids[next];
        ConfigManager.SaveAppConfig(AppConfig);
    }

    /// <summary>
    /// Refresh the overlay window with current configs and schedule auto-save.
    /// Called automatically by <see cref="ConfigStore.Changed"/> — pages and
    /// hotkey handlers no longer need to call this explicitly.
    /// </summary>
    public static void RefreshOverlay()
    {
        OverlayWin?.UpdateConfigs(Config.Overlay, Config.Crosshair, Config.Clock);
        ScheduleAutoSave();
    }

    // ── Debounced auto-save for overlay/crosshair/clock configs ──
    private static DispatcherTimer? _autoSaveTimer;
    private static void ScheduleAutoSave()
    {
        // Respect the user's AutoSaveOnClose setting — if disabled, don't auto-save on changes
        if (!Config.App.AutoSaveOnClose) return;

        if (_autoSaveTimer == null)
        {
            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _autoSaveTimer.Tick += (_, _) =>
            {
                _autoSaveTimer!.Stop();
                ConfigManager.SaveProfile(new ProfileData
                {
                    ProfileName = "Default",
                    Overlay = Config.Overlay,
                    Crosshair = Config.Crosshair,
                    Clock = Config.Clock
                });
            };
        }
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    /// <summary>
    /// Apply UI scale to the main window's root content.
    /// Works with AllowsTransparency=True windows by applying LayoutTransform
    /// to the root FrameworkElement instead of the Window itself.
    /// </summary>
    public static void ApplyUIScale(UIScale scale)
    {
        double factor = scale switch
        {
            UIScale.Percent75 => 0.75,
            UIScale.Percent100 => 1.0,
            UIScale.Percent125 => 1.25,
            _ => 1.0 // Auto
        };

        if (MainWin?.Content is FrameworkElement root)
        {
            root.LayoutTransform = scale != UIScale.Auto
                ? new ScaleTransform(factor, factor)
                : null;
        }
    }

    /// <summary>
    /// Reset ALL settings to their factory defaults, persist to disk,
    /// re-register hotkeys, and refresh the UI.
    /// </summary>
    public void ResetAllDefaults()
    {
        // Reset all config objects to fresh defaults via ConfigStore
        Config.ResetToDefaults();

        // Persist to disk
        ConfigManager.SaveAppConfig(Config.App);
        ConfigManager.SaveHotkeys(Config.Hotkeys);
        ConfigManager.SaveProfile(new ProfileData
        {
            ProfileName = "Default",
            Overlay = Config.Overlay,
            Crosshair = Config.Crosshair,
            Clock = Config.Clock
        });

        // Apply language (may have changed)
        ApplyLanguage(Config.App.Language);

        // Apply UI scale (may have changed)
        ApplyUIScale(Config.App.Scale);

        // Re-register hotkeys with new defaults
        RegisterAllHotkeys();

        // Refresh overlay rendering (also triggered by ConfigStore.Changed,
        // but called here to ensure immediate refresh before UI notification)
        RefreshOverlay();

        // Notify all pages to refresh their UI
        MainWin!.NotifyConfigChanged();
    }

    private void OnHotkeyPressed(string name)
    {
        // Update UI if main window is visible
        MainWin?.NotifyConfigChanged();

        // OSD shows unconditionally — it is the primary feedback that a hotkey
        // fired, and the only feedback when the settings window is hidden to
        // the tray. Over the settings UI it is a harmless 1s hint that also
        // reflects the change.
        string? text = OsdTextBuilder.Build(
            name,
            OverlayConfig,
            CrosshairConfig,
            ClockConfig,
            crosshairPageActive: MainWin?.CurrentPage == "Crosshair",
            res: ResolveString,
            targetMonitorLabel: TargetMonitorOsdLabel());
        if (text != null)
            OverlayWin?.ShowOsd(text);
    }

    /// <summary>Localized resource lookup; falls back to the key when missing.</summary>
    private static string ResolveString(string key) =>
        (string?)Current.TryFindResource(key) ?? key;

    /// <summary>
    /// Localized label of the current target monitor for OSD feedback —
    /// "All monitors" (incl. fallback) or "Monitor N (WxH)". Mirrors the
    /// layered matching used by CycleTargetMonitor.
    /// </summary>
    private static string TargetMonitorOsdLabel()
    {
        string all = ResolveString("Options_TargetMonitorAll");
        string target = AppConfig.TargetMonitor;
        if (string.IsNullOrWhiteSpace(target)) return all;

        var monitors = Win32Interop.GetAllMonitors();
        var matched = Win32Interop.SelectTargetMonitors(monitors, target);
        if (matched.Count == 1)
        {
            int idx = monitors.FindIndex(m =>
                string.Equals(m.DeviceId, matched[0].DeviceId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                return string.Format(ResolveString("Options_MonitorLabel"),
                    idx + 1, matched[0].Width, matched[0].Height);
        }
        return all;
    }

    private readonly List<string> _failedHotkeys = new();
    private DispatcherTimer? _failedHotkeyTimer;

    private void OnHotkeyRegistrationFailed(string displayString)
    {
        _failedHotkeys.Add(displayString);
        _failedHotkeyTimer?.Stop();
        _failedHotkeyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _failedHotkeyTimer.Tick += (_, _) =>
        {
            _failedHotkeyTimer?.Stop();
        if (_failedHotkeys.Count > 0)
        {
            var msg = string.Join("\n", _failedHotkeys.Distinct());
            _failedHotkeys.Clear();
            var tip = (string)Current.Resources["Hotkeys_RegFail_Msg"];
            // CustomMessageBox (not the raw Win32 MessageBox): gains Enter/Esc
            // handling, an owner, and — when the main window is hidden in a
            // game — the 2s overlay hint that the dialog is waiting.
            CustomMessageBox.Show(
                (string)Current.Resources["Hotkeys_RegFail_Title"],
                $"{tip}\n\n{msg}",
                (string)Current.Resources["Common_OK"]);
        }
        };
        _failedHotkeyTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Release the single-instance mutex if this instance owns it
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            _ownsSingleInstanceMutex = false;
        }

        // Unsubscribe from config changes
        Config.Changed -= OnConfigChanged;

        // Always reset keyboard/gamepad motion control on exit for safety
        Config.Overlay.MotionKeyboardEnabled = false;
        Config.Overlay.MotionGamepadEnabled = false;

        // Auto-save if enabled
        if (Config.App.AutoSaveOnClose)
        {
            ConfigManager.SaveAppConfig(Config.App);
            ConfigManager.SaveHotkeys(Config.Hotkeys);
            ConfigManager.SaveProfile(new ProfileData
            {
                ProfileName = "Default",
                Overlay = Config.Overlay,
                Crosshair = Config.Crosshair,
                Clock = Config.Clock
            });
        }

        Hotkeys.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
