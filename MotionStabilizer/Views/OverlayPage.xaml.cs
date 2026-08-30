using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotionStabilizer.Models;
using MotionStabilizer.Services;

namespace MotionStabilizer.Views;

/// <summary>
/// Settings page for the edge overlay (边缘叠加).
/// </summary>
public partial class OverlayPage : Page
{
    private bool _isLoading = true;

    public OverlayPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => RefreshFromConfig();

    public void RefreshFromConfig()
    {
        if (!IsLoaded) return;
        _isLoading = true;

        var cfg = App.OverlayConfig;

        ToggleOverlay.IsChecked = cfg.IsVisible;

        // Shape buttons
        UpdateShapeSelection(cfg.Shape);
        PanelMotionSettings.Visibility = cfg.Shape == OverlayShape.MotionDots
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Dynamic motion cue settings
        SliderMotionDotColumns.Value = Math.Clamp(cfg.MotionDotColumns, 1, 6);
        MotionDotColumnsLabel.Text = cfg.MotionDotColumns.ToString();
        SliderMotionDotSpacingV.Value = Math.Clamp(cfg.MotionDotSpacingV, 0.5, 3.0);
        MotionDotSpacingVLabel.Text = cfg.MotionDotSpacingV.ToString("0.0") + "x";
        SliderMotionDotSpacingH.Value = Math.Clamp(cfg.MotionDotSpacingH, 0.5, 3.0);
        MotionDotSpacingHLabel.Text = cfg.MotionDotSpacingH.ToString("0.0") + "x";
        // MotionSensitivity is persisted on the legacy internal scale (pre-2.8.0 default 1.5);
        // the UI displays value / 1.5 so the old default shows as 1.0x with unchanged feel.
        double mouseSensDisplay = Math.Round(cfg.MotionSensitivity / 1.5, 1);
        SliderMotionSensitivity.Value = Math.Clamp(mouseSensDisplay, 0.1, 2.0);
        MotionSensitivityLabel.Text = mouseSensDisplay.ToString("0.0") + "x";
        SliderMotionKeyboardSensitivity.Value = Math.Clamp(cfg.MotionKeyboardSensitivity, 0.1, 3.0);
        MotionKeyboardSensitivityLabel.Text = cfg.MotionKeyboardSensitivity.ToString("0.0") + "x";
        PanelKeyboardSensitivity.Visibility = cfg.MotionKeyboardEnabled ? Visibility.Visible : Visibility.Collapsed;
        SliderMotionGamepadSensitivity.Value = Math.Clamp(cfg.MotionGamepadSensitivity, 0.1, 3.0);
        MotionGamepadSensitivityLabel.Text = cfg.MotionGamepadSensitivity.ToString("0.0") + "x";
        PanelGamepadSensitivity.Visibility = cfg.MotionGamepadEnabled ? Visibility.Visible : Visibility.Collapsed;
        SliderMotionGamepadDeadzone.Value = Math.Clamp(cfg.MotionGamepadDeadzone, 0.0, 0.5);
        MotionGamepadDeadzoneLabel.Text = cfg.MotionGamepadDeadzone.ToString("0%");
        PanelGamepadDeadzone.Visibility = cfg.MotionGamepadEnabled ? Visibility.Visible : Visibility.Collapsed;
        SliderMotionRefreshRate.Value = Math.Clamp(cfg.MotionRefreshRate, 30, 360);
        MotionRefreshRateLabel.Text = Math.Clamp(cfg.MotionRefreshRate, 30, 360) + " Hz";
        ChkMotionKeyboard.IsChecked = cfg.MotionKeyboardEnabled;
        ChkMotionGamepad.IsChecked = cfg.MotionGamepadEnabled;
        ChkMotionInverted.IsChecked = cfg.MotionInverted;
        ChkMotionParallax.IsChecked = cfg.MotionParallaxScale;
        int parallaxPct = (int)Math.Round(cfg.MotionParallaxAmount * 100);
        SliderMotionParallaxAmount.Value = Math.Clamp(parallaxPct, 0, 100);
        MotionParallaxAmountLabel.Text = Math.Clamp(parallaxPct, 0, 100) + "%";
        PanelParallaxAmount.Visibility = cfg.MotionParallaxScale ? Visibility.Visible : Visibility.Collapsed;

        // Aspect ratio
        CbAspectRatio.SelectedIndex = (int)cfg.AspectRatio;

        // Size
        SliderSize.Value = (int)cfg.Size;
        SizeLabel.Text = SizeToText(cfg.Size);

        // Length
        SliderLength.Value = (int)cfg.Length;
        LengthLabel.Text = "+" + (int)cfg.Length;

        // Display mode
        CbDisplayMode.SelectedIndex = (int)cfg.Mode;

        // Split
        CbSplit.SelectedIndex = (int)cfg.Split;

        // Color
        UpdateColorSelection(cfg.ColorPreset);
        if (cfg.ColorPreset == ColorPreset.Custom)
        {
            SwatchCustom.Background = new SolidColorBrush(cfg.GetColor());
        }

        // Opacity
        SliderOpacity.Value = cfg.Opacity;
        OpacityLabel.Text = cfg.Opacity + "%";

        // Edge visibility
        bool isMotion = cfg.Shape == OverlayShape.MotionDots;
        ChkEdgeTop.Visibility = isMotion ? Visibility.Collapsed : Visibility.Visible;
        ChkEdgeBottom.Visibility = isMotion ? Visibility.Collapsed : Visibility.Visible;
        ChkEdgeTop.IsChecked = cfg.EdgeTopVisible;
        ChkEdgeBottom.IsChecked = cfg.EdgeBottomVisible;
        ChkEdgeLeft.IsChecked = cfg.EdgeLeftVisible;
        ChkEdgeRight.IsChecked = cfg.EdgeRightVisible;

        // Opacity mode
        CbOpacityMode.SelectedIndex = (int)cfg.OpacityMode;
        UpdateOpacityPanels();

        // Per-edge opacity
        SliderOpacityTop.Value = cfg.EdgeTopOpacity;
        OpacityTopLabel.Text = cfg.EdgeTopOpacity + "%";
        SliderOpacityBottom.Value = cfg.EdgeBottomOpacity;
        OpacityBottomLabel.Text = cfg.EdgeBottomOpacity + "%";
        SliderOpacityLeft.Value = cfg.EdgeLeftOpacity;
        OpacityLeftLabel.Text = cfg.EdgeLeftOpacity + "%";
        SliderOpacityRight.Value = cfg.EdgeRightOpacity;
        OpacityRightLabel.Text = cfg.EdgeRightOpacity + "%";

        // Hotkey label
        var hk = App.HotkeyConfig.ToggleOverlay;
        HotkeyLabel.Text = hk.IsSet ? $"[{hk.DisplayString}]" : "";

        _isLoading = false;
    }

    private void UpdateShapeSelection(OverlayShape shape)
    {
        BtnShapePole.Tag = shape == OverlayShape.Pole ? "Selected" : "";
        BtnShapeBox.Tag = shape == OverlayShape.Box ? "Selected" : "";
        BtnShapeDome.Tag = shape == OverlayShape.Dome ? "Selected" : "";
        BtnShapeFlag.Tag = shape == OverlayShape.Flag ? "Selected" : "";
        BtnShapeMotion.Tag = shape == OverlayShape.MotionDots ? "Selected" : "";
    }

    private void UpdateColorSelection(ColorPreset color)
    {
        SwatchRed.Tag = color == ColorPreset.Red ? "Selected" : "";
        SwatchGreen.Tag = color == ColorPreset.Green ? "Selected" : "";
        SwatchBlue.Tag = color == ColorPreset.Blue ? "Selected" : "";
        SwatchCustom.Tag = color == ColorPreset.Custom ? "Selected" : "";
    }

    private static string SizeToText(SizePreset s) => s switch
    {
        SizePreset.XXS => "2XS", SizePreset.XS => "XS", SizePreset.S => "S",
        SizePreset.M => "M", SizePreset.L => "L", SizePreset.XL => "XL",
        SizePreset.XXL => "2XL", _ => "M"
    };

    private void ToggleOverlay_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.IsVisible = ToggleOverlay.IsChecked == true;
    }

    private void Shape_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender == BtnShapePole) App.OverlayConfig.Shape = OverlayShape.Pole;
        else if (sender == BtnShapeBox) App.OverlayConfig.Shape = OverlayShape.Box;
        else if (sender == BtnShapeDome) App.OverlayConfig.Shape = OverlayShape.Dome;
        else if (sender == BtnShapeFlag) App.OverlayConfig.Shape = OverlayShape.Flag;
        else if (sender == BtnShapeMotion) App.OverlayConfig.Shape = OverlayShape.MotionDots;
        UpdateShapeSelection(App.OverlayConfig.Shape);
        bool isMotion = App.OverlayConfig.Shape == OverlayShape.MotionDots;
        PanelMotionSettings.Visibility = isMotion ? Visibility.Visible : Visibility.Collapsed;
        ChkEdgeTop.Visibility = isMotion ? Visibility.Collapsed : Visibility.Visible;
        ChkEdgeBottom.Visibility = isMotion ? Visibility.Collapsed : Visibility.Visible;
        // In per-edge opacity mode, hide Top/Bottom sliders for MotionDots
        UpdateOpacityPanels();
    }

    private void MotionDotColumns_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionDotColumnsLabel == null) return;
        App.OverlayConfig.MotionDotColumns = (int)SliderMotionDotColumns.Value;
        MotionDotColumnsLabel.Text = App.OverlayConfig.MotionDotColumns.ToString();
    }

    private void MotionDotSpacingV_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionDotSpacingVLabel == null) return;
        App.OverlayConfig.MotionDotSpacingV = Math.Round(SliderMotionDotSpacingV.Value, 1);
        MotionDotSpacingVLabel.Text = App.OverlayConfig.MotionDotSpacingV.ToString("0.0") + "x";
    }

    private void MotionDotSpacingH_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionDotSpacingHLabel == null) return;
        App.OverlayConfig.MotionDotSpacingH = Math.Round(SliderMotionDotSpacingH.Value, 1);
        MotionDotSpacingHLabel.Text = App.OverlayConfig.MotionDotSpacingH.ToString("0.0") + "x";
    }

    private void MotionSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionSensitivityLabel == null) return;
        // Store on the legacy internal scale: displayed 1.0x = stored 1.5 = pre-2.8.0 default feel
        double display = Math.Round(SliderMotionSensitivity.Value, 1);
        App.OverlayConfig.MotionSensitivity = Math.Round(display * 1.5, 3);
        MotionSensitivityLabel.Text = display.ToString("0.0") + "x";
    }

    private void MotionKeyboardSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionKeyboardSensitivityLabel == null) return;
        App.OverlayConfig.MotionKeyboardSensitivity = Math.Round(SliderMotionKeyboardSensitivity.Value, 1);
        MotionKeyboardSensitivityLabel.Text = App.OverlayConfig.MotionKeyboardSensitivity.ToString("0.0") + "x";
    }

    private void MotionRefreshRate_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionRefreshRateLabel == null) return;
        App.OverlayConfig.MotionRefreshRate = (int)SliderMotionRefreshRate.Value;
        MotionRefreshRateLabel.Text = App.OverlayConfig.MotionRefreshRate + " Hz";
    }

    private void MotionKeyboard_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        bool wantEnabled = ChkMotionKeyboard.IsChecked == true;

        // If trying to enable, show the mandatory red warning (skippable after a
        // previous confirmation with "don't show again" ticked)
        if (wantEnabled && !App.OverlayConfig.MotionKeyboardEnabled &&
            !ConfirmMotionRisk("Motion_WarningMsg",
                () => App.AppConfig.MotionKeyboardWarningAcknowledged,
                () => App.AppConfig.MotionKeyboardWarningAcknowledged = true))
        {
            // User declined or dismissed — keep disabled
            _isLoading = true;
            ChkMotionKeyboard.IsChecked = false;
            _isLoading = false;
            return;
        }

        App.OverlayConfig.MotionKeyboardEnabled = wantEnabled;
        PanelKeyboardSensitivity.Visibility = wantEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MotionGamepad_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        bool wantEnabled = ChkMotionGamepad.IsChecked == true;
        bool wasEnabled = App.OverlayConfig.MotionGamepadEnabled;

        // Mandatory red warning on enable — same conservative gate as keyboard control
        if (wantEnabled && !wasEnabled &&
            !ConfirmMotionRisk("Motion_GamepadWarningMsg",
                () => App.AppConfig.MotionGamepadWarningAcknowledged,
                () => App.AppConfig.MotionGamepadWarningAcknowledged = true))
        {
            // User declined or dismissed — keep disabled
            _isLoading = true;
            ChkMotionGamepad.IsChecked = false;
            _isLoading = false;
            return;
        }

        App.OverlayConfig.MotionGamepadEnabled = wantEnabled;
        PanelGamepadSensitivity.Visibility = wantEnabled ? Visibility.Visible : Visibility.Collapsed;
        PanelGamepadDeadzone.Visibility = wantEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Enable-time connectivity probe: TryGetSticks fails silently when no pad
        // is attached or XInput is unavailable — the top "it doesn't work" report
        // for this feature. Surface the reason immediately instead. Enabling stays
        // allowed either way, so a pad plugged in later starts working on the spot.
        if (wantEnabled && !wasEnabled)
        {
            var probe = XInputInterop.ProbeConnection();
            if (probe == GamepadProbeResult.NotConnected)
                CustomMessageBox.Show(
                    (string)FindResource("Motion_GamepadNotFound_Title"),
                    (string)FindResource("Motion_GamepadNotFound_Msg"),
                    (string)FindResource("Common_OK"));
            else if (probe == GamepadProbeResult.XInputUnavailable)
                CustomMessageBox.Show(
                    (string)FindResource("Motion_XInputUnavailable_Title"),
                    (string)FindResource("Motion_XInputUnavailable_Msg"),
                    (string)FindResource("Common_OK"));
        }
    }

    /// <summary>
    /// Mandatory red "ban risk" confirmation before enabling keyboard/gamepad
    /// motion control. Returns true when enabling may proceed. The safety gate
    /// itself (MotionXxxEnabled) intentionally resets on every restart; only the
    /// user's informed consent to the warning TEXT is persistent — confirming
    /// once with "don't show again" ticked silences the dialog (a factory reset
    /// re-arms it), cutting the per-restart friction down to a single checkbox.
    /// </summary>
    private bool ConfirmMotionRisk(string msgResourceKey, Func<bool> warningAcknowledged, Action setWarningAcknowledged)
    {
        if (warningAcknowledged()) return true;

        var title = (string)FindResource("Motion_WarningTitle");
        var msg = (string)FindResource(msgResourceKey);
        var yesText = (string)FindResource("Motion_WarningYes");
        var noText = (string)FindResource("Motion_WarningNo");
        var dontShowText = (string)FindResource("Motion_WarnDontShowAgain");

        var result = CustomMessageBox.Show(title, msg, yesText, noText, dontShowText, out bool dontShowAgain);

        // Enabling requires an explicit yes — declining AND dismissing (Alt+F4)
        // both keep the feature off
        if (result != CustomMessageBox.Result.Option1)
            return false;

        if (dontShowAgain)
        {
            setWarningAcknowledged();
            // Persist immediately — the debounced auto-save may lag behind
            ConfigManager.SaveAppConfig(App.AppConfig);
        }
        return true;
    }

    private void MotionGamepadSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionGamepadSensitivityLabel == null) return;
        App.OverlayConfig.MotionGamepadSensitivity = Math.Round(SliderMotionGamepadSensitivity.Value, 1);
        MotionGamepadSensitivityLabel.Text = App.OverlayConfig.MotionGamepadSensitivity.ToString("0.0") + "x";
    }

    private void MotionGamepadDeadzone_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionGamepadDeadzoneLabel == null) return;
        // UI shows a percentage; the config stores the fraction the deadzone math consumes
        App.OverlayConfig.MotionGamepadDeadzone = Math.Round(SliderMotionGamepadDeadzone.Value, 2);
        MotionGamepadDeadzoneLabel.Text = App.OverlayConfig.MotionGamepadDeadzone.ToString("0%");
    }

    private void MotionInverted_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.MotionInverted = ChkMotionInverted.IsChecked == true;
    }

    private void MotionParallax_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.MotionParallaxScale = ChkMotionParallax.IsChecked == true;
        PanelParallaxAmount.Visibility = App.OverlayConfig.MotionParallaxScale ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MotionParallaxAmount_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || MotionParallaxAmountLabel == null) return;
        int pct = (int)SliderMotionParallaxAmount.Value;
        App.OverlayConfig.MotionParallaxAmount = pct / 100.0;
        MotionParallaxAmountLabel.Text = pct + "%";
    }

    private void AspectRatio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.AspectRatio = (AspectRatio)CbAspectRatio.SelectedIndex;
    }

    private void Size_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || SizeLabel == null) return;
        App.OverlayConfig.Size = (SizePreset)(int)SliderSize.Value;
        SizeLabel.Text = SizeToText(App.OverlayConfig.Size);
    }

    private void Length_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || LengthLabel == null) return;
        App.OverlayConfig.Length = (OffsetLevel)(int)SliderLength.Value;
        LengthLabel.Text = "+" + (int)App.OverlayConfig.Length;
    }

    private void DisplayMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.Mode = (DisplayMode)CbDisplayMode.SelectedIndex;
    }

    private void Split_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.Split = (SplitScreen)CbSplit.SelectedIndex;
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender == SwatchRed) App.OverlayConfig.ColorPreset = ColorPreset.Red;
        else if (sender == SwatchGreen) App.OverlayConfig.ColorPreset = ColorPreset.Green;
        else if (sender == SwatchBlue) App.OverlayConfig.ColorPreset = ColorPreset.Blue;
        UpdateColorSelection(App.OverlayConfig.ColorPreset);
    }

    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var dialog = new ColorDialog();
        var color = App.OverlayConfig.GetColor();
        dialog.Color = System.Windows.Media.Color.FromArgb(255, color.R, color.G, color.B);

        if (dialog.ShowDialog() == true)
        {
            App.OverlayConfig.ColorPreset = ColorPreset.Custom;
            App.OverlayConfig.CustomColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            SwatchCustom.Background = new SolidColorBrush(dialog.Color);
            UpdateColorSelection(ColorPreset.Custom);
        }
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || OpacityLabel == null) return;
        App.OverlayConfig.Opacity = (int)SliderOpacity.Value;
        OpacityLabel.Text = App.OverlayConfig.Opacity + "%";
    }

    private void UpdateOpacityPanels()
    {
        bool perEdge = App.OverlayConfig.OpacityMode == EdgeOpacityMode.PerEdge;
        bool isMotion = App.OverlayConfig.Shape == OverlayShape.MotionDots;
        PanelUniformOpacity.Visibility = perEdge ? Visibility.Collapsed : Visibility.Visible;
        PanelPerEdgeOpacity.Visibility = perEdge ? Visibility.Visible : Visibility.Collapsed;
        // For MotionDots, hide Top/Bottom opacity sliders
        if (perEdge && isMotion)
        {
            // Hide top and bottom rows within PanelPerEdgeOpacity
            if (PanelPerEdgeOpacity.Children.Count > 0 &&
                PanelPerEdgeOpacity.Children[0] is StackPanel sp0)
                sp0.Visibility = Visibility.Collapsed;
            if (PanelPerEdgeOpacity.Children.Count > 1 &&
                PanelPerEdgeOpacity.Children[1] is StackPanel sp1)
                sp1.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (PanelPerEdgeOpacity.Children.Count > 0 &&
                PanelPerEdgeOpacity.Children[0] is StackPanel sp0)
                sp0.Visibility = Visibility.Visible;
            if (PanelPerEdgeOpacity.Children.Count > 1 &&
                PanelPerEdgeOpacity.Children[1] is StackPanel sp1)
                sp1.Visibility = Visibility.Visible;
        }
    }

    private void EdgeVisible_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var cfg = App.OverlayConfig;
        cfg.EdgeTopVisible = ChkEdgeTop.IsChecked == true;
        cfg.EdgeBottomVisible = ChkEdgeBottom.IsChecked == true;
        cfg.EdgeLeftVisible = ChkEdgeLeft.IsChecked == true;
        cfg.EdgeRightVisible = ChkEdgeRight.IsChecked == true;
    }

    private void OpacityMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.OpacityMode = (EdgeOpacityMode)CbOpacityMode.SelectedIndex;
        UpdateOpacityPanels();
    }

    private void EdgeOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        var cfg = App.OverlayConfig;
        if (sender == SliderOpacityTop)
        {
            cfg.EdgeTopOpacity = (int)SliderOpacityTop.Value;
            OpacityTopLabel.Text = cfg.EdgeTopOpacity + "%";
        }
        else if (sender == SliderOpacityBottom)
        {
            cfg.EdgeBottomOpacity = (int)SliderOpacityBottom.Value;
            OpacityBottomLabel.Text = cfg.EdgeBottomOpacity + "%";
        }
        else if (sender == SliderOpacityLeft)
        {
            cfg.EdgeLeftOpacity = (int)SliderOpacityLeft.Value;
            OpacityLeftLabel.Text = cfg.EdgeLeftOpacity + "%";
        }
        else if (sender == SliderOpacityRight)
        {
            cfg.EdgeRightOpacity = (int)SliderOpacityRight.Value;
            OpacityRightLabel.Text = cfg.EdgeRightOpacity + "%";
        }
    }
}
