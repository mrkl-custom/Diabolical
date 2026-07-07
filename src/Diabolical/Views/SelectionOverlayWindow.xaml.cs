using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Diabolical.Views;

/// <summary>
/// Full-virtual-screen transparent window that lets the user drag a rectangle over the
/// item tooltip. No fixed coordinates or window lookups — the user marks the region
/// themselves each time, since the tooltip's on-screen position varies (see CLAUDE.md).
///
/// Set WS_EX_NOACTIVATE so showing this window never steals OS focus/foreground from the
/// game — many games (Diablo 4 included) hide item tooltips the instant they lose focus,
/// which would defeat the whole capture flow. A non-activating window still receives mouse
/// input normally (that's independent of keyboard/activation focus), so drag-select keeps
/// working; it just means the window can never receive keyboard input, hence right-click
/// (not Escape) to cancel.
/// </summary>
public partial class SelectionOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate);
    }

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

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
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
