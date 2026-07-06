using System.IO;
using System.Windows;
using Diabolical.Services;

namespace Diabolical.Views;

public partial class MainWindow : Window
{
    private readonly HotkeyManager? _hotkeyManager;
    private readonly ScreenCaptureService? _captureService;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            var settings = AppSettingsLoader.Load();
            _hotkeyManager = new HotkeyManager();
            _captureService = new ScreenCaptureService(_hotkeyManager, settings.Hotkey);
            _captureService.CaptureCompleted += OnCaptureCompleted;
            _captureService.CaptureCancelled += OnCaptureCancelled;
            StatusText.Text = $"Hotkey {settings.Hotkey.Modifiers}+{settings.Hotkey.Key} registered. Ready to capture.";
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            StatusText.Text = $"Capture unavailable: {ex.Message}";
        }
    }

    private void TestCaptureButton_Click(object sender, RoutedEventArgs e) => _captureService?.BeginCapture();

    private void OnCaptureCompleted(byte[] imageBytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"diabolical_capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        File.WriteAllBytes(path, imageBytes);
        StatusText.Text = $"Captured {imageBytes.Length:N0} bytes -> {path}";
    }

    private void OnCaptureCancelled()
    {
        StatusText.Text = "Capture cancelled.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _captureService?.Dispose();
        _hotkeyManager?.Dispose();
        base.OnClosed(e);
    }
}
