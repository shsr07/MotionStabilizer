using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MotionStabilizer.Views;

/// <summary>
/// Custom message box with two options (e.g. Close / Minimize to Tray).
/// Matches the light Fluent theme.
/// </summary>
public class CustomMessageBox : Window
{
    public enum Result { None, Option1, Option2 }

    private static readonly string FontFam = "Segoe UI Variable, Segoe UI, Microsoft YaHei UI";

    /// <summary>Single-button message box (informational). Returns <see cref="Result.Option1"/> when dismissed.</summary>
    public static Result Show(string title, string message, string okText)
    {
        var result = Result.None;

        var win = new Window
        {
            Title = title,
            Width = 600,
            Height = 240,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            FontFamily = new FontFamily(FontFam)
        };

        win.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/FluentLight.xaml", UriKind.Absolute)
        });

        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

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

        var msgBlock = new TextBlock
        {
            Text = message.Replace("\\n", "\n"),
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x6E, 0x6E)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };
        stack.Children.Add(msgBlock);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okBtn = new Button
        {
            Content = okText,
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Padding = new Thickness(24, 10, 24, 10),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)),
            BorderThickness = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White
        };
        okBtn.Click += (_, _) => { result = Result.Option1; win.Close(); };
        btnPanel.Children.Add(okBtn);

        stack.Children.Add(btnPanel);
        border.Child = stack;
        win.Content = border;

        win.ShowDialog();
        return result;
    }

    public static Result Show(string title, string message, string option1Text, string option2Text)
    {
        var result = Result.None;

        var win = new Window
        {
            Title = title,
            Width = 600,
            Height = 240,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            FontFamily = new FontFamily(FontFam)
        };

        // Ensure theme styles are available even if Application.Resources lookup
        // has any edge-case issue on this Window instance.
        win.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/FluentLight.xaml", UriKind.Absolute)
        });

        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

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

        var msgBlock = new TextBlock
        {
            Text = message.Replace("\\n", "\n"),
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x6E, 0x6E)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };
        stack.Children.Add(msgBlock);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var secondaryStyle = win.TryFindResource("SecondaryButton") as Style;
        var primaryStyle = win.TryFindResource("PrimaryButton") as Style;

        var btn1 = new Button
        {
            Content = option1Text,
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Padding = new Thickness(24, 10, 24, 10),
            Margin = new Thickness(0, 0, 12, 0),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A))
        };
        btn1.Click += (_, _) => { result = Result.Option1; win.Close(); };
        btnPanel.Children.Add(btn1);

        var btn2 = new Button
        {
            Content = option2Text,
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Padding = new Thickness(24, 10, 24, 10),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)),
            BorderThickness = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White
        };
        btn2.Click += (_, _) => { result = Result.Option2; win.Close(); };
        btnPanel.Children.Add(btn2);

        stack.Children.Add(btnPanel);
        border.Child = stack;
        win.Content = border;

        win.ShowDialog();
        return result;
    }
}
