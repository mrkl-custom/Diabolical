using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diabolical.Views;

/// <summary>
/// Full-virtual-screen transparent window that lets the user drag a rectangle over the
/// item tooltip. No fixed coordinates or window lookups — the user marks the region
/// themselves each time, since the tooltip's on-screen position varies (see CLAUDE.md).
/// </summary>
public partial class SelectionOverlayWindow : Window
{
    private Point? _startPoint;

    public event EventHandler<Int32Rect>? SelectionCompleted;
    public event EventHandler? SelectionCancelled;

    public SelectionOverlayWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => Keyboard.Focus(this);

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(RootCanvas);
        Canvas.SetLeft(SelectionRectangle, _startPoint.Value.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Value.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        SelectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_startPoint is null)
        {
            return;
        }

        var current = e.GetPosition(RootCanvas);
        var x = Math.Min(current.X, _startPoint.Value.X);
        var y = Math.Min(current.Y, _startPoint.Value.Y);
        var width = Math.Abs(current.X - _startPoint.Value.X);
        var height = Math.Abs(current.Y - _startPoint.Value.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPoint is null)
        {
            return;
        }

        ReleaseMouseCapture();
        var selection = new Rect(_startPoint.Value, e.GetPosition(RootCanvas));
        _startPoint = null;
        Close();

        if (selection.Width < 2 || selection.Height < 2)
        {
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        var transformToDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var deviceRect = ToDeviceRect(Left, Top, selection, transformToDevice);
        SelectionCompleted?.Invoke(this, deviceRect);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _startPoint = null;
        Close();
        SelectionCancelled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Converts a selection rectangle from window-local DIPs to absolute device-pixel
    /// screen coordinates, so CopyFromScreen captures the right region under DPI scaling.
    /// </summary>
    internal static Int32Rect ToDeviceRect(double windowLeft, double windowTop, Rect selection, Matrix transformToDevice)
    {
        var topLeftDips = new Point(windowLeft + selection.Left, windowTop + selection.Top);
        var bottomRightDips = new Point(windowLeft + selection.Right, windowTop + selection.Bottom);

        var topLeftDevice = transformToDevice.Transform(topLeftDips);
        var bottomRightDevice = transformToDevice.Transform(bottomRightDips);

        return new Int32Rect(
            (int)Math.Round(topLeftDevice.X),
            (int)Math.Round(topLeftDevice.Y),
            (int)Math.Round(bottomRightDevice.X - topLeftDevice.X),
            (int)Math.Round(bottomRightDevice.Y - topLeftDevice.Y));
    }
}
