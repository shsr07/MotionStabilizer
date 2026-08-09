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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Global exception Handlers ──
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;

        try
        {
            StartupInternal();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"启动失败:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
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

        // Show main window (or minimize to tray on first run)
        if (Config.App.MinimizeToTrayOnStart)
        {
            MainWin.Show();
            MainWin.Hide();
        }
        else
        {
            MainWin.Show();
        }
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
        MessageBox.Show(
            $"发生未处理异常:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "Motion Stabilizer - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"发生致命异常:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "Motion Stabilizer - Fatal Error",
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
        Hotkeys.Register(hk.CycleOverlayShape, () => { OverlayConfig.Shape = (OverlayShape)(((int)OverlayConfig.Shape + 1) % 4); });
        Hotkeys.Register(hk.CycleCrosshairShape, () => { CrosshairConfig.Shape = (CrosshairShape)(((int)CrosshairConfig.Shape + 1) % 3); });
        Hotkeys.Register(hk.CycleAspectRatio, CycleAspectRatio);
        Hotkeys.Register(hk.CycleOpacityMode, () => { OverlayConfig.OpacityMode = (EdgeOpacityMode)(((int)OverlayConfig.OpacityMode + 1) % 2); });
        Hotkeys.Register(hk.CycleTargetMonitor, CycleTargetMonitor);
        Hotkeys.Register(hk.ColorRed, () => SetColor(ColorPreset.Red));
        Hotkeys.Register(hk.ColorGreen, () => SetColor(ColorPreset.Green));
        Hotkeys.Register(hk.ColorBlue, () => SetColor(ColorPreset.Blue));
        Hotkeys.Register(hk.ColorCustom, () => SetColor(ColorPreset.Custom));
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

    private void SetColor(ColorPreset color)
    {
        OverlayConfig.ColorPreset = color;
        CrosshairConfig.ColorPreset = color;
    }

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
                MessageBox.Show($"{tip}\n\n{msg}",
                    (string)Current.Resources["Hotkeys_RegFail_Title"],
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        _failedHotkeyTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Unsubscribe from config changes
        Config.Changed -= OnConfigChanged;

        // Always reset keyboard motion control on exit for safety
        Config.Overlay.MotionKeyboardEnabled = false;

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
