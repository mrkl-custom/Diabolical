using System.Windows;
using Diabolical.Services;

namespace Diabolical;

public partial class App : Application
{
    public App()
    {
        // Applies to every window app-wide (main window + dialogs) as they load, so the
        // native title bar matches the dark WPF theme without repeating this per window.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            DarkTitleBar.Apply(window);
        }
    }
}
