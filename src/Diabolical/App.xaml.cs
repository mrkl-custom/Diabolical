using System.Windows;
using System.Windows.Threading;
using Diabolical.Services;
using Diabolical.Views;

namespace Diabolical;

public partial class App : Application
{
    public App()
    {
        // Applies to every window app-wide (main window + dialogs) as they load, so the
        // native title bar matches the dark WPF theme without repeating this per window.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

        // Safety net for the app's many `async void` handlers: anything that escapes them
        // (corrupted JSON, a locked clipboard, a slow provider) would otherwise crash the
        // whole process. Surface it to the status list instead of dying.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            DarkTitleBar.Apply(window);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ReportUnhandledException(e.Exception);
        }

        e.Handled = true;
    }
}
