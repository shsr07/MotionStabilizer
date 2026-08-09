using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MotionStabilizer.Models;
using MotionStabilizer.Services;

namespace MotionStabilizer.Views;

/// <summary>
/// Settings page for global options (全局选项).
/// </summary>
public partial class OptionsPage : Page
{
    private bool _isLoading = false;

    public OptionsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        RefreshFromConfig();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => RefreshMonitorList());
    }

    public void RefreshFromConfig()
    {
        if (!IsLoaded) return;
        _isLoading = true;

        var cfg = App.AppConfig;

        ChkMinimizeToTray.IsChecked = cfg.MinimizeToTrayOnStart;
        ChkAutoSave.IsChecked = cfg.AutoSaveOnClose;
        ChkConfirmClose.IsChecked = cfg.ConfirmBeforeClose;

        RefreshMonitorList();
        CbUIScale.SelectedIndex = (int)cfg.Scale;
        CbLanguage.SelectedIndex = (int)cfg.Language;

        _isLoading = false;
    }

    /// <summary>
    /// Populate the target monitor ComboBox with all detected monitors.
    /// If the saved TargetMonitor is not found, clears the config and
    /// shows a fallback hint.
    /// </summary>
    private void RefreshMonitorList()
    {
        CbTargetMonitor.Items.Clear();

        var allLabel = FindResource("Options_TargetMonitorAll") as string ?? "All Monitors";
        CbTargetMonitor.Items.Add(new ComboBoxItem { Content = allLabel, Tag = "" });

        var labelFormat = FindResource("Options_MonitorLabel") as string ?? "Monitor {0} ({1}x{2})";
        int index = 1;
        foreach (var mon in Win32Interop.GetAllMonitors())
        {
            var label = string.IsNullOrEmpty(mon.FriendlyName)
                ? string.Format(labelFormat, index, mon.Width, mon.Height)
                : $"{index}. {mon.FriendlyName} ({mon.Width}x{mon.Height})";
            CbTargetMonitor.Items.Add(new ComboBoxItem { Content = label, Tag = mon.DeviceId });
            index++;
        }

        // Try to select the saved target monitor
        string target = App.AppConfig.TargetMonitor;
        CbTargetMonitor.SelectedIndex = 0;
        bool matched = false;
        for (int i = 0; i < CbTargetMonitor.Items.Count; i++)
        {
            var tag = ((ComboBoxItem)CbTargetMonitor.Items[i]).Tag as string ?? "";
            if (string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
            {
                CbTargetMonitor.SelectedIndex = i;
                matched = true;
                break;
            }
        }

        // If saved target was not empty but not found → clear config + show hint
        bool fallback = !string.IsNullOrEmpty(target) && !matched;
        TargetMonitorFallbackHint.Visibility = fallback ? Visibility.Visible : Visibility.Collapsed;
        if (fallback)
        {
            App.AppConfig.TargetMonitor = "";
            ConfigManager.SaveAppConfig(App.AppConfig);
        }
    }

    private void MinimizeToTray_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.AppConfig.MinimizeToTrayOnStart = ChkMinimizeToTray.IsChecked == true;
        ConfigManager.SaveAppConfig(App.AppConfig);
    }

    private void AutoSave_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.AppConfig.AutoSaveOnClose = ChkAutoSave.IsChecked == true;
        ConfigManager.SaveAppConfig(App.AppConfig);
    }

    private void ConfirmClose_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.AppConfig.ConfirmBeforeClose = ChkConfirmClose.IsChecked == true;
        ConfigManager.SaveAppConfig(App.AppConfig);
    }

    private void UIScale_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.AppConfig.Scale = (UIScale)CbUIScale.SelectedIndex;
        ConfigManager.SaveAppConfig(App.AppConfig);
        App.ApplyUIScale(App.AppConfig.Scale);
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.AppConfig.Language = (Language)CbLanguage.SelectedIndex;
        App.ApplyLanguage(App.AppConfig.Language);
        ConfigManager.SaveAppConfig(App.AppConfig);
    }

    private void TargetMonitor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        var item = CbTargetMonitor.SelectedItem as ComboBoxItem;
        App.AppConfig.TargetMonitor = item?.Tag as string ?? "";
        ConfigManager.SaveAppConfig(App.AppConfig);
        TargetMonitorFallbackHint.Visibility = Visibility.Collapsed;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
        => ProfileService.SaveProfile();

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var title = (string)FindResource("Options_ResetConfirmTitle");
        var msg = (string)FindResource("Options_ResetConfirmMsg");
        var yesText = (string)FindResource("Options_ResetConfirmYes");
        var noText = (string)FindResource("Options_ResetConfirmNo");

        var result = CustomMessageBox.Show(title, msg, noText, yesText);
        if (result != CustomMessageBox.Result.Option2)
            return;

        // Perform the reset
        if (App.Current is App appInst)
        {
            appInst.ResetAllDefaults();
        }

        // Refresh this page's UI
        RefreshFromConfig();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        => ProfileService.DeleteProfile();

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
        => ProfileService.LoadProfile();
}
