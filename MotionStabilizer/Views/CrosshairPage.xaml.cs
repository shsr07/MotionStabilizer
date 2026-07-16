using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotionStabilizer.Models;

namespace MotionStabilizer.Views;

/// <summary>
/// Settings page for the center crosshair (中心准星).
/// </summary>
public partial class CrosshairPage : Page
{
    private bool _isLoading = true;

    public CrosshairPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => RefreshFromConfig();

    public void RefreshFromConfig()
    {
        if (!IsLoaded) return;
        _isLoading = true;

        var cfg = App.CrosshairConfig;

        ToggleCrosshair.IsChecked = cfg.IsVisible;
        UpdateShapeSelection(cfg.Shape);

        CbAspectRatio.SelectedIndex = (int)cfg.AspectRatio;

        SliderSize.Value = (int)cfg.Size;
        SizeLabel.Text = SizeToText(cfg.Size);

        SliderThickness.Value = (int)cfg.Thickness;
        ThicknessLabel.Text = "+" + (int)cfg.Thickness;

        TxtPosX.Text = cfg.PositionX.ToString();
        TxtPosY.Text = cfg.PositionY.ToString();

        CbSplit.SelectedIndex = (int)cfg.Split;

        UpdateColorSelection(cfg.ColorPreset);
        if (cfg.ColorPreset == ColorPreset.Custom)
            SwatchCustom.Background = new SolidColorBrush(cfg.GetColor());

        SliderOpacity.Value = cfg.Opacity;
        OpacityLabel.Text = cfg.Opacity + "%";

        var hk = App.HotkeyConfig.ToggleCrosshair;
        HotkeyLabel.Text = hk.IsSet ? $"[{hk.DisplayString}]" : "";

        _isLoading = false;
    }

    private void UpdateShapeSelection(CrosshairShape shape)
    {
        BtnShapeCircle.Tag = shape == CrosshairShape.Circle ? "Selected" : "";
        BtnShapeCross.Tag = shape == CrosshairShape.Cross ? "Selected" : "";
        BtnShapeDiamond.Tag = shape == CrosshairShape.Diamond ? "Selected" : "";
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
        SizePreset.XXL => "2XL", _ => "S"
    };

    private void ToggleCrosshair_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.CrosshairConfig.IsVisible = ToggleCrosshair.IsChecked == true;
        App.RefreshOverlay();
    }

    private void Shape_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender == BtnShapeCircle) App.CrosshairConfig.Shape = CrosshairShape.Circle;
        else if (sender == BtnShapeCross) App.CrosshairConfig.Shape = CrosshairShape.Cross;
        else if (sender == BtnShapeDiamond) App.CrosshairConfig.Shape = CrosshairShape.Diamond;
        UpdateShapeSelection(App.CrosshairConfig.Shape);
        App.RefreshOverlay();
    }

    private void AspectRatio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.CrosshairConfig.AspectRatio = (AspectRatio)CbAspectRatio.SelectedIndex;
        App.RefreshOverlay();
    }

    private void Size_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || SizeLabel == null) return;
        App.CrosshairConfig.Size = (SizePreset)(int)SliderSize.Value;
        SizeLabel.Text = SizeToText(App.CrosshairConfig.Size);
        App.RefreshOverlay();
    }

    private void Thickness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || ThicknessLabel == null) return;
        App.CrosshairConfig.Thickness = (OffsetLevel)(int)SliderThickness.Value;
        ThicknessLabel.Text = "+" + (int)App.CrosshairConfig.Thickness;
        App.RefreshOverlay();
    }

    private void PosX_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || TxtPosX == null) return;
        if (int.TryParse(TxtPosX.Text, out int x))
        {
            App.CrosshairConfig.PositionX = x;
            App.RefreshOverlay();
        }
    }

    private void PosY_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || TxtPosY == null) return;
        if (int.TryParse(TxtPosY.Text, out int y))
        {
            App.CrosshairConfig.PositionY = y;
            App.RefreshOverlay();
        }
    }

    private void ResetCenter_Click(object sender, RoutedEventArgs e)
    {
        App.CrosshairConfig.PositionX = 0;
        App.CrosshairConfig.PositionY = 0;
        TxtPosX.Text = "0";
        TxtPosY.Text = "0";
        App.RefreshOverlay();
    }

    private void Split_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        App.CrosshairConfig.Split = (SplitScreen)CbSplit.SelectedIndex;
        App.RefreshOverlay();
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender == SwatchRed) App.CrosshairConfig.ColorPreset = ColorPreset.Red;
        else if (sender == SwatchGreen) App.CrosshairConfig.ColorPreset = ColorPreset.Green;
        else if (sender == SwatchBlue) App.CrosshairConfig.ColorPreset = ColorPreset.Blue;
        UpdateColorSelection(App.CrosshairConfig.ColorPreset);
        App.RefreshOverlay();
    }

    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var dialog = new ColorDialog();
        var color = App.CrosshairConfig.GetColor();
        dialog.Color = System.Windows.Media.Color.FromArgb(255, color.R, color.G, color.B);

        if (dialog.ShowDialog() == true)
        {
            App.CrosshairConfig.ColorPreset = ColorPreset.Custom;
            App.CrosshairConfig.CustomColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            SwatchCustom.Background = new SolidColorBrush(dialog.Color);
            UpdateColorSelection(ColorPreset.Custom);
            App.RefreshOverlay();
        }
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || OpacityLabel == null) return;
        App.CrosshairConfig.Opacity = (int)SliderOpacity.Value;
        OpacityLabel.Text = App.CrosshairConfig.Opacity + "%";
        App.RefreshOverlay();
    }
}
