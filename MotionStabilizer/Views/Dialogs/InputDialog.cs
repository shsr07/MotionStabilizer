using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MotionStabilizer.Views;

/// <summary>
/// Simple input dialog for entering a profile name.
/// </summary>
public class InputDialog : Window
{
    public string InputText { get; private set; } = "";
    private readonly TextBox _textBox;

    private static readonly string FontFam = "Segoe UI Variable, Segoe UI, Microsoft YaHei UI";

    public InputDialog(string title, string label, string defaultValue = "")
    {
        // Ensure theme styles are available on this Window instance.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/FluentLight.xaml", UriKind.Absolute)
        });

        Title = title;
        Width = 380;
        Height = 200;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        InputText = defaultValue;

        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1)),
            BorderThickness = new Thickness(1),
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

        var labelBlock = new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x6E, 0x6E)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(labelBlock);

        _textBox = new TextBox
        {
            Text = defaultValue,
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Height = 36,
            Margin = new Thickness(0, 0, 0, 16)
        };
        stack.Children.Add(_textBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var primaryStyle = this.TryFindResource("PrimaryButton") as Style;
        var btnOk = new Button
        {
            Content = "OK",
            FontFamily = new FontFamily(FontFam),
            Width = 80,
            Height = 36,
            Margin = new Thickness(0, 0, 12, 0),
            Style = primaryStyle
        };
        btnOk.Click += (_, _) => { InputText = _textBox.Text; DialogResult = true; };
        btnPanel.Children.Add(btnOk);

        var secondaryStyle = this.TryFindResource("SecondaryButton") as Style;
        var btnCancel = new Button
        {
            Content = "Cancel",
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

        _textBox.Focus();
        _textBox.SelectAll();
    }
}
