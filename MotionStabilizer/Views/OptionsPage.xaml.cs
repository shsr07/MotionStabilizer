using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => RefreshFromConfig();

    public void RefreshFromConfig()
    {
        if (!IsLoaded) return;
        _isLoading = true;

        var cfg = App.AppConfig;

        ChkMinimizeToTray.IsChecked = cfg.MinimizeToTrayOnStart;
        ChkAutoSave.IsChecked = cfg.AutoSaveOnClose;
        ChkConfirmClose.IsChecked = cfg.ConfirmBeforeClose;

        CbUIScale.SelectedIndex = (int)cfg.Scale;
        CbLanguage.SelectedIndex = (int)cfg.Language;

        _isLoading = false;
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
