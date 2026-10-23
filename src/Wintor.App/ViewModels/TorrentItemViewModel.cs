using CommunityToolkit.Mvvm.ComponentModel;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.ViewModels;

/// <summary>One row of the torrent table. Updated in place from engine snapshots so the list does not flicker.</summary>
public partial class TorrentItemViewModel(string id) : ObservableObject
{
    public string Id { get; } = id;

    /// <summary>The latest engine state behind this row.</summary>
    public TorrentSnapshot? Snapshot { get; private set; }

    [ObservableProperty]
    public partial int QueueNumber { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string? Label { get; set; }

    [ObservableProperty]
    public partial bool HasLabel { get; set; }

    /// <summary>The label's color for the dot on its pill.</summary>
    [ObservableProperty]
    public partial Microsoft.UI.Xaml.Media.Brush? LabelBrush { get; set; }

    private string? _labelColor;

    [ObservableProperty]
    public partial TorrentStatus Status { get; set; }

    /// <summary>0–100 for the ProgressBar.</summary>
    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // The progress bar's color tells the state at a glance: green downloading, blue seeding,
    // grey when nothing moves (paused, stopped, error). One of the three is shown.
    [ObservableProperty]
    public partial bool ShowDownloadingBar { get; set; }

    [ObservableProperty]
    public partial bool ShowSeedingBar { get; set; }

    [ObservableProperty]
    public partial bool ShowInactiveBar { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string SizeText { get; set; } = "";

    [ObservableProperty]
    public partial string DownText { get; set; } = "";

    [ObservableProperty]
    public partial string UpText { get; set; } = "";

    [ObservableProperty]
    public partial string TimeLeftText { get; set; } = "";

    [ObservableProperty]
    public partial string SeedsText { get; set; } = "";

    [ObservableProperty]
    public partial string PeersText { get; set; } = "";

    /// <summary>For screen readers: the whole row in one line.</summary>
    [ObservableProperty]
    public partial string AccessibleName { get; set; } = "";

    public bool CanPause => Status is not (TorrentStatus.Paused or TorrentStatus.Stopped or TorrentStatus.Stopping);

    public bool IsComplete => Snapshot?.IsComplete == true;

    public string ContentPath => Snapshot?.ContentPath ?? "";

    public void Update(TorrentSnapshot s, int queueNumber, string? label, string? labelColor)
    {
        Snapshot = s;
        QueueNumber = queueNumber;
        Name = s.Name;
        Label = label;
        HasLabel = !string.IsNullOrEmpty(label);
        // A new brush only when the color changes, not on every refresh.
        if (labelColor != _labelColor || LabelBrush is null)
        {
            _labelColor = labelColor;
            LabelBrush = LabelColors.BrushOf(labelColor);
        }
        Status = s.Status;
        ProgressPercent = s.Progress * 100;
        ProgressText = s.TotalSize > 0 ? $"{s.Progress * 100:0.#}%" : "";
        IsIndeterminate = s.Status is TorrentStatus.FetchingMetadata or TorrentStatus.Starting;
        IsError = s.Status == TorrentStatus.Error;

        bool moving = s.Status is TorrentStatus.Downloading or TorrentStatus.Seeding or TorrentStatus.FetchingMetadata
            or TorrentStatus.Starting or TorrentStatus.Checking;
        ShowSeedingBar = moving && s.IsComplete;
        ShowDownloadingBar = moving && !s.IsComplete;
        ShowInactiveBar = !moving;

        StatusText = s.Status switch
        {
            TorrentStatus.Error when s.ErrorMessage is not null => Strings.Format("Status_ErrorDetails", s.ErrorMessage),
            TorrentStatus.Stopped or TorrentStatus.Paused when s.IsComplete => Strings.Get("Status_Done"),
            _ => Strings.Get("Status_" + s.Status),
        };

        SizeText = s.TotalSize > 0 ? Formatting.Bytes(s.TotalSize) : "";
        bool transferring = s.Status is TorrentStatus.Downloading or TorrentStatus.Seeding;
        DownText = transferring && s.DownloadRate > 0 ? Formatting.Rate(s.DownloadRate) : "";
        UpText = transferring && s.UploadRate > 0 ? Formatting.Rate(s.UploadRate) : "";
        TimeLeftText = s.EstimatedTimeLeft is { } eta ? Formatting.Duration(eta) : s.IsComplete ? "" : s.Status == TorrentStatus.Downloading ? "∞" : "";
        SeedsText = transferring || s.Status == TorrentStatus.FetchingMetadata ? s.Seeds.ToString() : "";
        PeersText = transferring || s.Status == TorrentStatus.FetchingMetadata ? s.Leechers.ToString() : "";

        AccessibleName = string.Join(", ", new[] { Name, Label, StatusText, ProgressText, SizeText, DownText, UpText }
            .Where(p => !string.IsNullOrEmpty(p)));
        OnPropertyChanged(nameof(CanPause));
    }
}
