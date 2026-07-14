using System.Windows;
using System.Windows.Interop;
using MotionStabilizer.Models;

namespace MotionStabilizer.Services;

/// <summary>
/// Manages global hotkey registration via Win32 RegisterHotKey API.
/// Routes hotkey events to the application via callbacks.
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly Dictionary<int, Action> _bindings = new();
    private readonly Dictionary<int, HotkeyBinding> _bindingInfo = new();
    private HwndSource? _source;
    private int _nextId = 9000;

    /// <summary>Triggered when any hotkey fires. Passes the binding name.</summary>
    public event Action<string>? HotkeyPressed;

    /// <summary>Initialize with the window that will receive WM_HOTKEY messages.</summary>
    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
            helper.EnsureHandle();

        _source = HwndSource.FromHwnd(helper.Handle);
        if (_source != null)
            _source.AddHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_bindings.TryGetValue(id, out var callback))
            {
                callback.Invoke();
                var info = _bindingInfo.GetValueOrDefault(id);
                if (info != null)
                    HotkeyPressed?.Invoke(info.Name);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Register a single hotkey binding.</summary>
    public bool Register(HotkeyBinding binding, Action callback)
    {
        Unregister(binding);

        if (!binding.IsSet) return false;

        uint vk = Win32Interop.KeyNameToVk(binding.Key);
        if (vk == 0) return false;

        uint modifiers = Win32Interop.MOD_NOREPEAT;
        if (binding.Ctrl) modifiers |= Win32Interop.MOD_CONTROL;
        if (binding.Alt) modifiers |= Win32Interop.MOD_ALT;
        if (binding.Shift) modifiers |= Win32Interop.MOD_SHIFT;

        int id = _nextId++;
        var hwnd = _source?.Handle ?? IntPtr.Zero;

        if (Win32Interop.RegisterHotKey(hwnd, id, modifiers, vk))
        {
            _bindings[id] = callback;
            _bindingInfo[id] = binding;
            return true;
        }
        return false;
    }

    /// <summary>Unregister a hotkey by its binding.</summary>
    public void Unregister(HotkeyBinding binding)
    {
        var entry = _bindingInfo.FirstOrDefault(kvp => kvp.Value.Name == binding.Name);
        if (entry.Key != 0)
        {
            var hwnd = _source?.Handle ?? IntPtr.Zero;
            Win32Interop.UnregisterHotKey(hwnd, entry.Key);
            _bindings.Remove(entry.Key);
            _bindingInfo.Remove(entry.Key);
        }
    }

    /// <summary>Unregister all hotkeys.</summary>
    public void UnregisterAll()
    {
        var hwnd = _source?.Handle ?? IntPtr.Zero;
        foreach (var id in _bindings.Keys.ToList())
        {
            Win32Interop.UnregisterHotKey(hwnd, id);
        }
        _bindings.Clear();
        _bindingInfo.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(HwndHook);
        _source = null;
    }
}
