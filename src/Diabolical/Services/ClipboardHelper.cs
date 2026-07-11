using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace Diabolical.Services;

/// <summary>
/// Clipboard.SetText occasionally throws a COMException (CLIPBRD_E_CANT_OPEN) when another
/// process briefly holds the clipboard open — a classic WPF flake, not a real failure. Retries
/// a few times before giving up so a single flake doesn't surface as an error to the user.
/// </summary>
public static class ClipboardHelper
{
    public static void SetTextWithRetry(string text, int maxAttempts = 3, int delayMs = 50)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }
    }
}
