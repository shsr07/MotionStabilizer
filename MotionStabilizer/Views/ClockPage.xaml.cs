using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MotionStabilizer.Models;

namespace MotionStabilizer.Views;

/// <summary>
/// Settings page for the floating clock (悬浮时钟).
/// </summary>
public partial class ClockPage : Page
{
    // _isLoading defaults to true to prevent event handlers from firing
    // during XAML initialization (Slider.ValueChanged fires when Minimum is set).
    // It is set to false in RefreshFromConfig() after the page is loaded.
    private bool _isLoading = true;
    private DispatcherTimer? _previewTimer;
    private bool _isDragging = false;

    public ClockPage()
    {
        InitializeComponent();
        Loaded += ClockPage_Loaded;
        Unloaded += ClockPage_Unloaded;
    }

    private void ClockPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshFromConfig();
        StartPreviewTimer();
    }

    private void ClockPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _previewTimer?.Stop();
    }

    private void StartPreviewTimer()
    {
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _previewTimer.Tick += (_, _) => UpdatePreview();
        _previewTimer.Start();
    }

    /// <summary>Format the clock text according to the selected format.</summary>
    private static string FormatClock(DateTime now, ClockFormat format)
    {
        return format switch
        {
            ClockFormat.HHmm => now.ToString("HH:mm"),
            ClockFormat.HHmmss => now.ToString("HH:mm:ss"),
            // am/pm h:mm — 12-hour clock, midnight/noon shown as 12
            ClockFormat.hmmAmPm => $"{now:tt} {now:hh}:{now:mm}",
            _ => now.ToString("HH:mm")
        };
    }

    private void UpdatePreview()
    {
        if (_isLoading || PreviewLabel == null) return;
        var now = DateTime.Now;
        var cfg = App.ClockConfig;

        PreviewLabel.Text = FormatClock(now, cfg.Format);
        PreviewLabel.FontFamily = new FontFamily(cfg.GetRenderFontFamily());
        PreviewLabel.FontSize = cfg.FontSize;
        PreviewLabel.Foreground = new SolidColorBrush(cfg.GetColor());

        // Apply outline effect to preview when the Outline pseudo-font is selected
        PreviewLabel.Effect = cfg.IsOutlineFont
            ? new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 4,
                ShadowDepth = 0,
                Opacity = 1
              }
            : null;
    }

    public void RefreshFromConfig()
    {
        if (!IsLoaded) return;
        _isLoading = true;

        var cfg = App.ClockConfig;

        ToggleClock.IsChecked = cfg.IsVisible;

        switch (cfg.Format)
        {
            case ClockFormat.HHmm: RbFormat1.IsChecked = true; break;
            case ClockFormat.HHmmss: RbFormat2.IsChecked = true; break;
            case ClockFormat.hmmAmPm: RbFormat3.IsChecked = true; break;
        }

        // Select font family in combo
        for (int i = 0; i < CbFontFamily.Items.Count; i++)
        {
            if (CbFontFamily.Items[i] is ComboBoxItem item && item.Content?.ToString() == cfg.FontFamily)
            {
                CbFontFamily.SelectedIndex = i;
                break;
            }
        }

        SliderFontSize.Value = cfg.FontSize;
        if (FontSizeLabel != null)
            FontSizeLabel.Text = cfg.FontSize.ToString();

        if (BtnColor != null)
            BtnColor.Background = new SolidColorBrush(cfg.GetColor());

        if (TxtPosX != null)
            TxtPosX.Text = cfg.PositionX.ToString();
        if (TxtPosY != null)
            TxtPosY.Text = cfg.PositionY.ToString();

        SliderOpacity.Value = cfg.Opacity;
        if (OpacityLabel != null)
            OpacityLabel.Text = cfg.Opacity + "%";

        var hk = App.HotkeyConfig.ToggleClock;
        if (HotkeyLabel != null)
            HotkeyLabel.Text = hk.IsSet ? $"[{hk.DisplayString}]" : "";

        _isLoading = false;
        UpdatePreview();
    }

    private void ToggleClock_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.ClockConfig.IsVisible = ToggleClock.IsChecked == true;
        App.RefreshOverlay();
    }

    private void Format_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (RbFormat1.IsChecked == true) App.ClockConfig.Format = ClockFormat.HHmm;
        else if (RbFormat2.IsChecked == true) App.ClockConfig.Format = ClockFormat.HHmmss;
        else if (RbFormat3.IsChecked == true) App.ClockConfig.Format = ClockFormat.hmmAmPm;
        App.RefreshOverlay();
        UpdatePreview();
    }

    private void FontFamily_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (CbFontFamily.SelectedItem is ComboBoxItem item)
        {
            App.ClockConfig.FontFamily = item.Content?.ToString() ?? "Outline";
            App.RefreshOverlay();
            UpdatePreview();
        }
    }

    private void FontSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || FontSizeLabel == null) return;
        App.ClockConfig.FontSize = (int)SliderFontSize.Value;
        FontSizeLabel.Text = App.ClockConfig.FontSize.ToString();
        App.RefreshOverlay();
        UpdatePreview();
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var dialog = new ColorDialog();
        dialog.Color = App.ClockConfig.GetColor();

        if (dialog.ShowDialog() == true)
        {
            App.ClockConfig.ColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            BtnColor.Background = new SolidColorBrush(dialog.Color);
            App.RefreshOverlay();
            UpdatePreview();
        }
    }

    private void PosX_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || TxtPosX == null) return;
        if (int.TryParse(TxtPosX.Text, out int x))
        {
            App.ClockConfig.PositionX = x;
            App.RefreshOverlay();
        }
    }

    private void PosY_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || TxtPosY == null) return;
        if (int.TryParse(TxtPosY.Text, out int y))
        {
            App.ClockConfig.PositionY = y;
            App.RefreshOverlay();
        }
    }

    private void Drag_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _isDragging = !_isDragging;
        if (_isDragging)
        {
            BtnDrag.Content = FindResource("Clock_LockDrag");
            App.OverlayWin?.EnableClockDrag();
        }
        else
        {
            BtnDrag.Content = FindResource("Clock_UnlockDrag");
            App.OverlayWin?.DisableClockDrag();
            TxtPosX.Text = App.ClockConfig.PositionX.ToString();
            TxtPosY.Text = App.ClockConfig.PositionY.ToString();
        }
    }

    /// <summary>
    /// Called by overlay window when clock drag is confirmed via left-click.
    /// Restores the button to "Unlock" state and updates position text boxes.
    /// </summary>
    public void OnClockDragConfirmed()
    {
        _isDragging = false;
        if (BtnDrag != null)
            BtnDrag.Content = FindResource("Clock_UnlockDrag");
        if (TxtPosX != null)
            TxtPosX.Text = App.ClockConfig.PositionX.ToString();
        if (TxtPosY != null)
            TxtPosY.Text = App.ClockConfig.PositionY.ToString();
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || OpacityLabel == null) return;
        App.ClockConfig.Opacity = (int)SliderOpacity.Value;
        OpacityLabel.Text = App.ClockConfig.Opacity + "%";
        App.RefreshOverlay();
    }
}
