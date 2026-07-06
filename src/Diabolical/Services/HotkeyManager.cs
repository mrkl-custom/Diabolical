using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Registers a single global hotkey via the Win32 RegisterHotKey API, using a hidden
/// message-only window to receive WM_HOTKEY — this fires even while another window
/// (the game) has focus, which is the whole point of a global hotkey.
/// </summary>
public class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xB00C;

    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _messageWindow;
    private bool _registered;
    private bool _disposed;

    public event Action? HotkeyPressed;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("DiabolicalHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE: message-only window, never visible
        };
        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WndProc);
    }

    public void Register(HotkeySettings settings)
    {
        var modifiers = ParseModifiers(settings.Modifiers);
        var key = (Key)Enum.Parse(typeof(Key), settings.Key, ignoreCase: true);
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (!RegisterHotKey(_messageWindow.Handle, HotkeyId, (uint)modifiers, vk))
        {
            throw new InvalidOperationException(
                $"Failed to register hotkey '{settings.Modifiers}+{settings.Key}' — it may already be in use by another application.");
        }

        _registered = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    internal static ModifierKeys ParseModifiers(string modifiers)
    {
        var result = ModifierKeys.None;
        foreach (var part in modifiers.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part.ToLowerInvariant() switch
            {
                "control" or "ctrl" => ModifierKeys.Control,
                "alt" => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win" or "windows" => ModifierKeys.Win,
                _ => throw new ArgumentException($"Unknown hotkey modifier '{part}'.")
            };
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_registered)
        {
            UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        }

        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
    }
}
