using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotionStabilizer.Models;

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
        BtnShapeBox.Tag = shape == OverlayShape.Box ? "Selected" : "";
        BtnShapeDome.Tag = shape == OverlayShape.Dome ? "Selected" : "";
        BtnShapeFlag.Tag = shape == OverlayShape.Flag ? "Selected" : "";
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
        App.RefreshOverlay();
    }

    private void Shape_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender == BtnShapeBox) App.OverlayConfig.Shape = OverlayShape.Box;
        else if (sender == BtnShapeDome) App.OverlayConfig.Shape = OverlayShape.Dome;
        else if (sender == BtnShapeFlag) App.OverlayConfig.Shape = OverlayShape.Flag;
        UpdateShapeSelection(App.OverlayConfig.Shape);
        App.RefreshOverlay();
    }

    private void AspectRatio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.AspectRatio = (AspectRatio)CbAspectRatio.SelectedIndex;
        App.RefreshOverlay();
    }

    private void Size_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || SizeLabel == null) return;
        App.OverlayConfig.Size = (SizePreset)(int)SliderSize.Value;
        SizeLabel.Text = SizeToText(App.OverlayConfig.Size);
        App.RefreshOverlay();
    }

    private void Length_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || LengthLabel == null) return;
        App.OverlayConfig.Length = (OffsetLevel)(int)SliderLength.Value;
        LengthLabel.Text = "+" + (int)App.OverlayConfig.Length;
        App.RefreshOverlay();
    }

    private void DisplayMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.Mode = (DisplayMode)CbDisplayMode.SelectedIndex;
        App.RefreshOverlay();
    }

    private void Split_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.Split = (SplitScreen)CbSplit.SelectedIndex;
        App.RefreshOverlay();
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender == SwatchRed) App.OverlayConfig.ColorPreset = ColorPreset.Red;
        else if (sender == SwatchGreen) App.OverlayConfig.ColorPreset = ColorPreset.Green;
        else if (sender == SwatchBlue) App.OverlayConfig.ColorPreset = ColorPreset.Blue;
        UpdateColorSelection(App.OverlayConfig.ColorPreset);
        App.RefreshOverlay();
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
            App.RefreshOverlay();
        }
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || OpacityLabel == null) return;
        App.OverlayConfig.Opacity = (int)SliderOpacity.Value;
        OpacityLabel.Text = App.OverlayConfig.Opacity + "%";
        App.RefreshOverlay();
    }

    private void UpdateOpacityPanels()
    {
        bool perEdge = App.OverlayConfig.OpacityMode == EdgeOpacityMode.PerEdge;
        PanelUniformOpacity.Visibility = perEdge ? Visibility.Collapsed : Visibility.Visible;
        PanelPerEdgeOpacity.Visibility = perEdge ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EdgeVisible_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var cfg = App.OverlayConfig;
        cfg.EdgeTopVisible = ChkEdgeTop.IsChecked == true;
        cfg.EdgeBottomVisible = ChkEdgeBottom.IsChecked == true;
        cfg.EdgeLeftVisible = ChkEdgeLeft.IsChecked == true;
        cfg.EdgeRightVisible = ChkEdgeRight.IsChecked == true;
        App.RefreshOverlay();
    }

    private void OpacityMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.OverlayConfig.OpacityMode = (EdgeOpacityMode)CbOpacityMode.SelectedIndex;
        UpdateOpacityPanels();
        App.RefreshOverlay();
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
        App.RefreshOverlay();
    }
}
