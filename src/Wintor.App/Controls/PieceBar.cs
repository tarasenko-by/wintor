using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Wintor.App.Controls;

/// <summary>
/// A bar of piece buckets, as in µTorrent's "Downloaded" and "Availability": each bucket is drawn in
/// <see cref="Fill"/> with the strength of its value (0 = track, 1 = full color). One gradient brush with hard
/// stops, so a thousand pieces cost one element.
/// </summary>
public sealed partial class PieceBar : Grid
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(PieceBar), new PropertyMetadata(null, (d, _) => ((PieceBar)d).Redraw()));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(PieceBar), new PropertyMetadata(null, (d, _) => ((PieceBar)d).Redraw()));

    private readonly Border _bar = new() { CornerRadius = new CornerRadius(4) };

    public PieceBar()
    {
        Height = 16;
        CornerRadius = new CornerRadius(4);
        Children.Add(_bar);
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    // Grid has no automation peer of its own; without one Narrator and UI tests cannot see the bar.
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private void Redraw()
    {
        if (Values is not { Count: > 0 } values || Fill is not SolidColorBrush fill)
        {
            _bar.Background = null;
            return;
        }

        Color color = fill.Color;
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i < values.Count; i++)
        {
            // Hard edges: each bucket gets a stop at its start and one at its end. A started bucket is drawn at least
            // at 45 % strength: a few pieces of a big bucket would otherwise be an invisible tint.
            double value = Math.Clamp(values[i], 0, 1);
            double strength = value <= 0 ? 0 : 0.45 + 0.55 * value;
            var c = Color.FromArgb((byte)(color.A * strength), color.R, color.G, color.B);
            brush.GradientStops.Add(new GradientStop { Color = c, Offset = (double)i / values.Count });
            brush.GradientStops.Add(new GradientStop { Color = c, Offset = (double)(i + 1) / values.Count });
        }

        _bar.Background = brush;
    }
}
