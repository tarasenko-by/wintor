using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Wintor.Core;

namespace Wintor.App.Controls;

/// <summary>Download and upload speed over the last minutes, as two lines scaled to the highest value.</summary>
public sealed partial class SpeedGraph : Grid
{
    public static readonly DependencyProperty DownloadProperty = Register(nameof(Download));
    public static readonly DependencyProperty UploadProperty = Register(nameof(Upload));
    public static readonly DependencyProperty DownloadBrushProperty = Register(nameof(DownloadBrush), typeof(Brush));
    public static readonly DependencyProperty UploadBrushProperty = Register(nameof(UploadBrush), typeof(Brush));

    private readonly Polyline _down = new() { StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _up = new() { StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };

    public SpeedGraph()
    {
        Children.Add(_up);
        Children.Add(_down);
        SizeChanged += (_, _) => Redraw();
    }

    /// <summary>Samples in bytes per second, oldest first.</summary>
    public IReadOnlyList<long>? Download
    {
        get => (IReadOnlyList<long>?)GetValue(DownloadProperty);
        set => SetValue(DownloadProperty, value);
    }

    public IReadOnlyList<long>? Upload
    {
        get => (IReadOnlyList<long>?)GetValue(UploadProperty);
        set => SetValue(UploadProperty, value);
    }

    public Brush? DownloadBrush
    {
        get => (Brush?)GetValue(DownloadBrushProperty);
        set => SetValue(DownloadBrushProperty, value);
    }

    public Brush? UploadBrush
    {
        get => (Brush?)GetValue(UploadBrushProperty);
        set => SetValue(UploadBrushProperty, value);
    }

    /// <summary>How many samples the width stands for.</summary>
    public int Capacity { get; set; } = SpeedHistory.DefaultLength;

    // Grid has no automation peer of its own; without one Narrator and UI tests cannot see the graph.
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private static DependencyProperty Register(string name, Type? type = null) =>
        DependencyProperty.Register(name, type ?? typeof(IReadOnlyList<long>), typeof(SpeedGraph),
            new PropertyMetadata(null, (d, _) => ((SpeedGraph)d).Redraw()));

    private void Redraw()
    {
        _down.Stroke = DownloadBrush;
        _up.Stroke = UploadBrush;
        double max = Math.Max(1, Math.Max(Download?.DefaultIfEmpty().Max() ?? 0, Upload?.DefaultIfEmpty().Max() ?? 0));
        Fill(_down, Download, max);
        Fill(_up, Upload, max);
    }

    private void Fill(Polyline line, IReadOnlyList<long>? samples, double max)
    {
        var points = new PointCollection();
        if (samples is { Count: > 1 } && ActualWidth > 0 && ActualHeight > 0)
        {
            double step = ActualWidth / Math.Max(Capacity - 1, 1);
            double left = ActualWidth - step * (samples.Count - 1);
            for (int i = 0; i < samples.Count; i++)
            {
                // Keep the 2 px line inside the box at both extremes.
                double y = 1 + (ActualHeight - 2) * (1 - samples[i] / max);
                points.Add(new Point(left + step * i, y));
            }
        }

        line.Points = points;
    }
}
