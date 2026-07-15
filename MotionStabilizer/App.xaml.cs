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
/// hotkey manager, config manager, and system tray.
/// </summary>
public partial class App : Application
{
    // Core services
    public static HotkeyManager Hotkeys { get; } = new();
    public static ConfigManager ConfigMgr { get; } = new();

    // Config state
    public static OverlayConfig OverlayConfig { get; set; } = new();
    public static CrosshairConfig CrosshairConfig { get; set; } = new();
    public static ClockConfig ClockConfig { get; set; } = new();
    public static HotkeyConfig HotkeyConfig { get; set; } = new();
    public static AppConfig AppConfig { get; set; } = new();

    // Windows
    public static OverlayWindow? OverlayWin { get; private set; }
    public static MainWindow? MainWin { get; private set; }

    // Tray
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Global exception handlers ──
        // Catch unhandled UI thread exceptions
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        // Catch unhandled non-UI thread exceptions
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
        // Load saved configs
        AppConfig = ConfigManager.LoadAppConfig();
        HotkeyConfig = ConfigManager.LoadHotkeys();

        // Try to load default profile
        var defaultProfile = ConfigManager.LoadProfile("Default");
        if (defaultProfile != null)
        {
            OverlayConfig = defaultProfile.Overlay;
            CrosshairConfig = defaultProfile.Crosshair;
            ClockConfig = defaultProfile.Clock;
        }

        // Apply language
        ApplyLanguage(AppConfig.Language);

        // Create overlay window (invisible rendering layer)
        // Wrapped in try-catch so overlay failure doesn't block the main window
        try
        {
            OverlayWin = new OverlayWindow();
            OverlayWin.Show();
            OverlayWin.UpdateConfigs(OverlayConfig, CrosshairConfig, ClockConfig);
        }
        catch (Exception ex)
        {
            // Log but continue — the main window can still function
            System.Diagnostics.Debug.WriteLine($"Overlay window failed: {ex.Message}");
        }

        // Create tray icon — always show it so the app is accessible
        // even when the main window is hidden (minimized to tray)
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
        ApplyUIScale(AppConfig.Scale);

        // Initialize hotkeys with the main window
        try
        {
            Hotkeys.Initialize(MainWin);
            RegisterAllHotkeys();
            Hotkeys.HotkeyPressed += OnHotkeyPressed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hotkey init failed: {ex.Message}");
        }

        // Show main window (or minimize to tray on first run)
        if (AppConfig.MinimizeToTrayOnStart)
        {
            MainWin.Show();
            MainWin.Hide();
        }
        else
        {
            MainWin.Show();
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Prevent the app from crashing on unhandled UI thread exceptions
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
        // Remove existing language dict (last one)
        if (dict.Count > 1)
            dict.RemoveAt(dict.Count - 1);

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

        var hk = HotkeyConfig;
        Hotkeys.Register(hk.ToggleOverlay, () => { OverlayConfig.IsVisible = !OverlayConfig.IsVisible; RefreshOverlay(); });
        Hotkeys.Register(hk.ToggleCrosshair, () => { CrosshairConfig.IsVisible = !CrosshairConfig.IsVisible; RefreshOverlay(); });
        Hotkeys.Register(hk.ToggleClock, () => { ClockConfig.IsVisible = !ClockConfig.IsVisible; RefreshOverlay(); });
        Hotkeys.Register(hk.CycleDisplayMode, () => { OverlayConfig.Mode = OverlayConfig.Mode == DisplayMode.Window ? DisplayMode.Stretch : DisplayMode.Window; RefreshOverlay(); });
        Hotkeys.Register(hk.CycleSplitScreen, CycleSplitScreen);
        Hotkeys.Register(hk.CycleOverlayShape, () => { OverlayConfig.Shape = (OverlayShape)(((int)OverlayConfig.Shape + 1) % 3); RefreshOverlay(); });
        Hotkeys.Register(hk.CycleCrosshairShape, () => { CrosshairConfig.Shape = (CrosshairShape)(((int)CrosshairConfig.Shape + 1) % 3); RefreshOverlay(); });
        Hotkeys.Register(hk.CycleAspectRatio, CycleAspectRatio);
        Hotkeys.Register(hk.ColorRed, () => SetColor(ColorPreset.Red));
        Hotkeys.Register(hk.ColorGreen, () => SetColor(ColorPreset.Green));
        Hotkeys.Register(hk.ColorYellow, () => SetColor(ColorPreset.Yellow));
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
        RefreshOverlay();
    }

    private void CycleAspectRatio()
    {
        var cfg = MainWin?.CurrentPage == "Crosshair" ? (object)CrosshairConfig : OverlayConfig;
        if (cfg is OverlayConfig oc)
            oc.AspectRatio = (AspectRatio)(((int)oc.AspectRatio + 1) % 4);
        else if (cfg is CrosshairConfig cc)
            cc.AspectRatio = (AspectRatio)(((int)cc.AspectRatio + 1) % 4);
        RefreshOverlay();
    }

    private void SetColor(ColorPreset color)
    {
        // Set color on both overlay and crosshair
        OverlayConfig.ColorPreset = color;
        CrosshairConfig.ColorPreset = color;
        RefreshOverlay();
    }

    /// <summary>Refresh the overlay window with current configs.</summary>
    public static void RefreshOverlay()
    {
        OverlayWin?.UpdateConfigs(OverlayConfig, CrosshairConfig, ClockConfig);
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
        // Reset all config objects to fresh defaults
        AppConfig = new AppConfig();
        HotkeyConfig = new HotkeyConfig();
        OverlayConfig = new OverlayConfig();
        CrosshairConfig = new CrosshairConfig();
        ClockConfig = new ClockConfig();

        // Persist to disk
        ConfigManager.SaveAppConfig(AppConfig);
        ConfigManager.SaveHotkeys(HotkeyConfig);
        ConfigManager.SaveProfile(new ProfileData
        {
            ProfileName = "Default",
            Overlay = OverlayConfig,
            Crosshair = CrosshairConfig,
            Clock = ClockConfig
        });

        // Apply language (may have changed)
        ApplyLanguage(AppConfig.Language);

        // Apply UI scale (may have changed)
        ApplyUIScale(AppConfig.Scale);

        // Re-register hotkeys with new defaults
        RegisterAllHotkeys();

        // Refresh overlay rendering
        RefreshOverlay();

        // Notify all pages to refresh their UI
        MainWin!.NotifyConfigChanged();
    }

    private void OnHotkeyPressed(string name)
    {
        // Update UI if main window is visible
        MainWin?.NotifyConfigChanged();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Auto-save if enabled
        if (AppConfig.AutoSaveOnClose)
        {
            ConfigManager.SaveAppConfig(AppConfig);
            ConfigManager.SaveHotkeys(HotkeyConfig);
            ConfigManager.SaveProfile(new ProfileData
            {
                ProfileName = "Default",
                Overlay = OverlayConfig,
                Crosshair = CrosshairConfig,
                Clock = ClockConfig
            });
        }

        Hotkeys.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
