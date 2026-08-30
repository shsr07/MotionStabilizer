using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
// WPF/WinForms dual-framework ambiguities not covered by GlobalUsings.cs
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;

namespace MotionStabilizer.Views;

/// <summary>
/// Custom message box (title + message + one or two buttons + optional checkable
/// option). Matches the light Fluent theme.
/// </summary>
public class CustomMessageBox : Window
{
    public enum Result { None, Option1, Option2 }

    private static readonly string FontFam = "Segoe UI Variable, Segoe UI, Microsoft YaHei UI";

    /// <summary>Single-button message box (informational). Returns <see cref="Result.Option1"/> when dismissed.</summary>
    public static Result Show(string title, string message, string okText)
        => ShowCore(title, message, new[] { okText }, null, out _);

    public static Result Show(string title, string message, string option1Text, string option2Text)
        => ShowCore(title, message, new[] { option1Text, option2Text }, null, out _);

    /// <summary>
    /// Two-button message box with a checkable option (e.g. "don't show this
    /// warning again"). <paramref name="checkBoxChecked"/> is false unless the
    /// user ticked the box before pressing a button.
    /// </summary>
    public static Result Show(string title, string message, string option1Text, string option2Text,
        string checkBoxText, out bool checkBoxChecked)
    {
        var result = ShowCore(title, message, new[] { option1Text, option2Text }, checkBoxText, out var ticked);
        checkBoxChecked = ticked;
        return result;
    }

    private static Result ShowCore(string title, string message, string[] buttonTexts,
        string? checkBoxText, out bool checkBoxChecked)
    {
        var result = Result.None;
        var ticked = false; // local — out params cannot be assigned from lambdas

        var win = new Window
        {
            Title = title,
            Width = 600,
            SizeToContent = SizeToContent.Height, // long messages must not clip
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            FontFamily = new FontFamily(FontFam)
        };

        // Owned dialogs stay above their owner and close with it — but only
        // when the settings window is actually on screen; an owner that is
        // hidden in the tray would drag this dialog down with it.
        if (App.MainWin is { IsVisible: true })
            win.Owner = App.MainWin;

        // Ensure theme styles are available even if Application.Resources lookup
        // has any edge-case issue on this Window instance.
        win.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/FluentLight.xaml", UriKind.Absolute)
        });

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD1)),
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
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(titleBlock);

        var msgBlock = new TextBlock
        {
            Text = message.Replace("\\n", "\n"),
            FontFamily = new FontFamily(FontFam),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };
        stack.Children.Add(msgBlock);

        CheckBox? checkBox = null;
        if (checkBoxText != null)
        {
            checkBox = new CheckBox
            {
                Content = checkBoxText,
                FontFamily = new FontFamily(FontFam),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(checkBox);
        }

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var primaryStyle = win.TryFindResource("PrimaryButton") as Style;
        var secondaryStyle = win.TryFindResource("SecondaryButton") as Style;

        Button? primaryBtn = null;

        Button MakeButton(string text, bool primary, Result value)
        {
            var btn = new Button
            {
                Content = text,
                FontFamily = new FontFamily(FontFam),
                FontSize = 13,
                Padding = new Thickness(24, 10, 24, 10),
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White
            };
            if (primary)
            {
                // Same solid blue whether or not the theme style resolves
                btn.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                if (primaryStyle != null) btn.Style = primaryStyle;
                primaryBtn = btn;
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD1));
                btn.BorderThickness = new Thickness(1);
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                if (secondaryStyle != null) btn.Style = secondaryStyle;
            }
            btn.Click += (_, _) =>
            {
                result = value;
                ticked = checkBox?.IsChecked == true;
                win.Close();
            };
            return btn;
        }

        if (buttonTexts.Length == 1)
        {
            btnPanel.Children.Add(MakeButton(buttonTexts[0], primary: true, Result.Option1));
        }
        else
        {
            var btn1 = MakeButton(buttonTexts[0], primary: false, Result.Option1);
            btn1.Margin = new Thickness(0, 0, 12, 0);
            btnPanel.Children.Add(btn1);
            btnPanel.Children.Add(MakeButton(buttonTexts[1], primary: true, Result.Option2));
        }

        stack.Children.Add(btnPanel);
        border.Child = stack;
        win.Content = border;

        // Enter activates the primary choice (single-button: the OK)
        if (primaryBtn != null)
        {
            primaryBtn.IsDefault = true;
            win.Loaded += (_, _) => primaryBtn.Focus();
        }

        // Esc picks the non-destructive side: the secondary button when there
        // is one (cancel / decline / minimize-to-tray), plain dismiss otherwise
        win.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            result = buttonTexts.Length == 2 ? Result.Option2 : Result.None;
            win.Close();
        };

        // A modal over a hidden main window (user in-game) can end up behind a
        // borderless-fullscreen surface — flag it on the topmost overlay so the
        // user knows something is waiting.
        if (App.MainWin is not { IsVisible: true })
        {
            string waiting = App.Current.TryFindResource("Osd_DialogWaiting") as string
                ?? "A dialog is waiting — it may be hidden behind the game";
            App.OverlayWin?.ShowOsd(waiting, holdMs: 2000);
        }

        win.ShowDialog();
        checkBoxChecked = ticked;
        return result;
    }
}
