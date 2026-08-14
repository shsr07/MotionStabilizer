using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MotionStabilizer.Views;

/// <summary>
/// Dialog for selecting a saved profile from a list.
/// </summary>
public class ProfileSelectDialog : Window
{
    public string SelectedProfile { get; private set; } = "";
    private readonly ListBox _listBox;

    private static readonly string FontFam = "Segoe UI Variable, Segoe UI, Microsoft YaHei UI";

    public ProfileSelectDialog(List<string> profiles, string? dialogTitle = null, string? confirmText = null)
    {
        // Localized defaults: title and confirm button come from the resource
        // dictionaries unless the caller passes explicit values.
        string title = dialogTitle ??
            (string)(System.Windows.Application.Current.TryFindResource("ProfileLoad_Title") ?? "Load Profile");
        string confirm = confirmText ??
            (string)(System.Windows.Application.Current.TryFindResource("ProfileLoad_Confirm") ?? "Load");

        // Ensure theme styles are available on this Window instance.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/FluentLight.xaml", UriKind.Absolute)
        });

        Title = title;
        Width = 360;
        Height = 320;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily(FontFam);

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
            Text = title,
            FontFamily = new FontFamily(FontFam),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A)),
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

        var primaryStyle = this.TryFindResource("PrimaryButton") as Style;
        var btnLoad = new Button
        {
            Content = confirm,
            FontFamily = new FontFamily(FontFam),
            Width = 80,
            Height = 36,
            Margin = new Thickness(0, 0, 12, 0),
            Style = primaryStyle
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

        var secondaryStyle = this.TryFindResource("SecondaryButton") as Style;
        var btnCancel = new Button
        {
            Content = (string)System.Windows.Application.Current.TryFindResource("Common_Cancel") ?? "Cancel",
            FontFamily = new FontFamily(FontFam),
            Width = 80,
            Height = 36,
            Style = secondaryStyle
        };
        btnCancel.Click += (_, _) => { DialogResult = false; };
        btnPanel.Children.Add(btnCancel);

        stack.Children.Add(btnPanel);
        border.Child = stack;
        Content = border;
    }
}
