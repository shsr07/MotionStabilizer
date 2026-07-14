using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MotionStabilizer.Views;

/// <summary>
/// Dialog for selecting a saved profile from a list.
/// </summary>
public class ProfileSelectDialog : Window
{
    public string SelectedProfile { get; private set; } = "";
    private readonly ListBox _listBox;

    private static readonly string FontFam = "Segoe UI Variable, Segoe UI, Microsoft YaHei UI";

    public ProfileSelectDialog(List<string> profiles, string dialogTitle = "Load Profile", string confirmText = "Load")
    {
        Title = dialogTitle;
        Width = 360;
        Height = 320;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily(FontFam);

        var outlineEffect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 3,
            ShadowDepth = 0,
            Opacity = 1
        };

        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel();

        var titleBlock = new TextBlock
        {
            Text = dialogTitle,
            FontFamily = new FontFamily(FontFam),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Effect = outlineEffect,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(titleBlock);

        _listBox = new ListBox
        {
            Height = 160,
            Margin = new Thickness(0, 0, 0, 16),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            FontFamily = new FontFamily(FontFam),
            FontSize = 13
        };

        foreach (var p in profiles)
            _listBox.Items.Add(p);

        if (_listBox.Items.Count > 0)
            _listBox.SelectedIndex = 0;

        stack.Children.Add(_listBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnLoad = new Button
        {
            Content = confirmText,
            FontFamily = new FontFamily(FontFam),
            Width = 80,
            Height = 36,
            Margin = new Thickness(0, 0, 12, 0),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };
        btnLoad.Click += (_, _) =>
        {
            if (_listBox.SelectedItem != null)
            {
                SelectedProfile = _listBox.SelectedItem.ToString()!;
                DialogResult = true;
            }
        };
        btnPanel.Children.Add(btnLoad);

        var btnCancel = new Button
        {
            Content = "Cancel",
            FontFamily = new FontFamily(FontFam),
            Width = 80,
            Height = 36,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            BorderThickness = new Thickness(1)
        };
        btnCancel.Click += (_, _) => { DialogResult = false; };
        btnPanel.Children.Add(btnCancel);

        stack.Children.Add(btnPanel);
        border.Child = stack;
        Content = border;
    }
}
