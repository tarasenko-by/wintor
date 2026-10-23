using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Wintor.App.Services;

namespace Wintor.App.Controls;

/// <summary>
/// Thin horizontal drag handle between two panes one above the other. Reports the vertical offset from where the
/// drag started, so the owner can clamp the height without drift. Double-click asks to reset the height.
/// </summary>
public sealed partial class PaneSplitter : Grid
{
    private readonly Rectangle _line;
    private bool _dragging;
    private double _start;

    public PaneSplitter()
    {
        Height = 8;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        ToolTipService.SetToolTip(this, Strings.Get("Splitter_Tooltip"));

        _line = new Rectangle
        {
            Height = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            Fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
        };
        Children.Add(_line);

        PointerEntered += (_, _) => _line.Opacity = 0.6;
        PointerExited += (_, _) =>
        {
            if (!_dragging)
            {
                _line.Opacity = 0;
            }
        };
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += (_, e) => EndDrag(e.Pointer);
        PointerCaptureLost += (_, e) => EndDrag(e.Pointer);
        DoubleTapped += (_, e) =>
        {
            e.Handled = true;
            ResetRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? DragStarted;

    /// <summary>Vertical offset from where the drag started; positive is down.</summary>
    public event EventHandler<double>? DragDelta;

    public event EventHandler? DragCompleted;

    public event EventHandler? ResetRequested;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!CapturePointer(e.Pointer))
        {
            return;
        }

        _dragging = true;
        _start = e.GetCurrentPoint(null).Position.Y;
        _line.Opacity = 1;
        DragStarted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            DragDelta?.Invoke(this, e.GetCurrentPoint(null).Position.Y - _start);
            e.Handled = true;
        }
    }

    private void EndDrag(Pointer pointer)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleasePointerCapture(pointer);
        _line.Opacity = 0;
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }
}
