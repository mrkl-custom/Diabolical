using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Diabolical.Services;

/// <summary>
/// Best-effort switch for the OS's immersive dark mode on a window's native title bar
/// (Win10 1809+/Win11), so the chrome matches the app's dark WPF theme instead of showing
/// a jarring white title bar. Silently no-ops on older Windows builds or windows with no
/// native chrome (e.g. SelectionOverlayWindow, which is WindowStyle="None").
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }
}
