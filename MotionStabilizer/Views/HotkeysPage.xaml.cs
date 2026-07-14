using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MotionStabilizer.Models;
using MotionStabilizer.Services;

namespace MotionStabilizer.Views;

/// <summary>
/// Settings page for hotkey bindings (快捷键绑定).
/// </summary>
public partial class HotkeysPage : Page
{
    public class HotkeyItem
    {
        public string DisplayName { get; set; } = "";
        public string HotkeyText { get; set; } = "—";
        public string BindingName { get; set; } = "";
        public HotkeyBinding? Binding { get; set; }
    }

    private readonly ObservableCollection<HotkeyItem> _displayItems = new();
    private readonly ObservableCollection<HotkeyItem> _colorItems = new();
    private HotkeyItem? _capturingItem;
    private TextBox? _capturingTextBox;

    public HotkeysPage()
    {
        InitializeComponent();
        DisplayHotkeyList.ItemsSource = _displayItems;
        ColorHotkeyList.ItemsSource = _colorItems;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => RefreshFromConfig();

    public void RefreshFromConfig()
    {
        if (!IsLoaded) return;

        _displayItems.Clear();
        _colorItems.Clear();

        var hk = App.HotkeyConfig;

        // Display mode hotkeys (8) — reordered per user request
        AddItem(_displayItems, hk.ToggleOverlay, "HK_ToggleOverlay");
        AddItem(_displayItems, hk.ToggleCrosshair, "HK_ToggleCrosshair");
        AddItem(_displayItems, hk.ToggleClock, "HK_ToggleClock");
        AddItem(_displayItems, hk.CycleOverlayShape, "HK_CycleOverlayShape");
        AddItem(_displayItems, hk.CycleCrosshairShape, "HK_CycleCrosshairShape");
        AddItem(_displayItems, hk.CycleDisplayMode, "HK_CycleDisplayMode");
        AddItem(_displayItems, hk.CycleAspectRatio, "HK_CycleAspectRatio");
        AddItem(_displayItems, hk.CycleSplitScreen, "HK_CycleSplitScreen");

        // Color hotkeys (4)
        AddItem(_colorItems, hk.ColorRed, "HK_ColorRed");
        AddItem(_colorItems, hk.ColorGreen, "HK_ColorGreen");
        AddItem(_colorItems, hk.ColorYellow, "HK_ColorYellow");
        AddItem(_colorItems, hk.ColorCustom, "HK_ColorCustom");
    }

    private void AddItem(ObservableCollection<HotkeyItem> list, HotkeyBinding binding, string resKey)
    {
        var displayName = App.Current.Resources[resKey]?.ToString() ?? binding.Name;
        list.Add(new HotkeyItem
        {
            DisplayName = displayName,
            HotkeyText = binding.DisplayString,
            BindingName = binding.Name,
            Binding = binding
        });
    }

    /// <summary>When a hotkey TextBox gets focus, start capturing.</summary>
    private void HotkeyField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is HotkeyItem item)
        {
            _capturingItem = item;
            _capturingTextBox = tb;
            tb.Text = (string)App.Current.Resources["Hotkeys_PressKeys"];
            tb.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC9, 0x97, 0x00));
            tb.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        }
    }

    /// <summary>When a hotkey TextBox loses focus, stop capturing and restore display.</summary>
    private void HotkeyField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is HotkeyItem item)
        {
            tb.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x6E, 0x6E));
            tb.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xD1, 0xD1));
            // Restore the actual hotkey text
            tb.Text = item.Binding?.DisplayString ?? "—";
        }
        _capturingItem = null;
        _capturingTextBox = null;
    }

    /// <summary>Capture key presses when a hotkey field is focused.</summary>
    private void HotkeyField_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingItem == null || sender is not TextBox tb) return;

        e.Handled = true;
        var key = e.Key;

        // Ignore modifier-only presses (Alt is no longer supported as a combo key)
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return;

        // Handle Escape to unbind
        if (key == Key.Escape)
        {
            if (_capturingItem.Binding != null)
            {
                _capturingItem.Binding.Key = "";
                _capturingItem.Binding.Ctrl = false;
                _capturingItem.Binding.Alt = false;
                _capturingItem.Binding.Shift = false;
            }
            // Re-register all hotkeys
            if (App.Current is App appInst)
            {
                appInst.RegisterAllHotkeys();
            }
            ConfigManager.SaveHotkeys(App.HotkeyConfig);
            _capturingItem = null;
            _capturingTextBox = null;
            Keyboard.ClearFocus();
            RefreshFromConfig();
            return;
        }

        // Convert WPF key to display name
        string keyName = KeyToString(key);

        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        // Alt is no longer supported as a combination key
        // Check for conflicts
        bool conflict = CheckConflict(_capturingItem.BindingName, keyName, ctrl, false, shift);

        if (conflict)
        {
            tb.Text = (string)App.Current.Resources["Hotkeys_Conflict"];
            tb.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x3B, 0x3B));
            // Reset after a brief delay
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _capturingItem = null;
                _capturingTextBox = null;
                Keyboard.ClearFocus();
                RefreshFromConfig();
            }), System.Windows.Threading.DispatcherPriority.Background);
            return;
        }

        // Update the binding
        if (_capturingItem.Binding != null)
        {
            _capturingItem.Binding.Key = keyName;
            _capturingItem.Binding.Ctrl = ctrl;
            _capturingItem.Binding.Alt = false;
            _capturingItem.Binding.Shift = shift;
        }

        _capturingItem = null;
        _capturingTextBox = null;

        // Re-register all hotkeys
        if (App.Current is App app)
        {
            app.RegisterAllHotkeys();
        }

        ConfigManager.SaveHotkeys(App.HotkeyConfig);

        // Remove focus from the text box
        Keyboard.ClearFocus();
        RefreshFromConfig();
    }

    private bool CheckConflict(string excludeName, string key, bool ctrl, bool alt, bool shift)
    {
        foreach (var b in App.HotkeyConfig.AllBindings)
        {
            if (b.Name == excludeName) continue;
            if (!b.IsSet) continue;
            if (b.Key == key && b.Ctrl == ctrl && b.Alt == alt && b.Shift == shift)
                return true;
        }
        return false;
    }

    private static string KeyToString(Key key)
    {
        if (key >= Key.F1 && key <= Key.F24)
            return key.ToString();

        string s = key.ToString();
        if (s.Length == 1) return s.ToUpper();
        if (s.Length == 2 && s.StartsWith("D")) return s[1].ToString();
        if (s.StartsWith("NumPad")) return s;

        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            _ => s
        };
    }
}
