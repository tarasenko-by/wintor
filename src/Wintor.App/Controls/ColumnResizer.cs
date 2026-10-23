using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Wintor.App.Services;

namespace Wintor.App.Controls;

/// <summary>
/// The divider on a column header's right edge, as in File Explorer: always drawn, so it reads as something to grab,
/// stronger while hovered or dragged. Reports the horizontal offset from where the drag started (so the owner clamps
/// without drift); double-click asks to reset the width.
/// </summary>
public sealed partial class ColumnResizer : Grid
{
    private readonly Rectangle _hover;
    private bool _dragging;
    private double _start;

    public ColumnResizer()
    {
        Width = 9;
        HorizontalAlignment = HorizontalAlignment.Right;
        // Centered on the boundary between this column and the next.
        Margin = new Thickness(0, 8, -4.5, 8);
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        ToolTipService.SetToolTip(this, Strings.Get("ColumnResizer_Tooltip"));

        // ThemeResource brushes follow the window's own theme; made from XAML so they do.
        Children.Add(Line(1, "DividerStrokeColorDefaultBrush"));
        _hover = Line(2, "ControlStrongStrokeColorDefaultBrush");
        _hover.Opacity = 0;
        Children.Add(_hover);

        PointerEntered += (_, _) => _hover.Opacity = 1;
        PointerExited += (_, _) =>
        {
            if (!_dragging)
            {
                _hover.Opacity = 0;
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

    /// <summary>Horizontal offset from where the drag started; positive is to the right.</summary>
    public event EventHandler<double>? DragDelta;

    public event EventHandler? DragCompleted;

    public event EventHandler? ResetRequested;

    private static Rectangle Line(double width, string brush) => (Rectangle)XamlReader.Load(
        $"<Rectangle xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Width=\"{width}\" " +
        $"HorizontalAlignment=\"Center\" Fill=\"{{ThemeResource {brush}}}\" />");

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!CapturePointer(e.Pointer))
        {
            return;
        }

        _dragging = true;
        _start = e.GetCurrentPoint(null).Position.X;
        DragStarted?.Invoke(this, EventArgs.Empty);
        // Not a click on the header button underneath.
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            DragDelta?.Invoke(this, e.GetCurrentPoint(null).Position.X - _start);
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
        _hover.Opacity = 0;
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }
}
