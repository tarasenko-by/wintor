using CommunityToolkit.Mvvm.ComponentModel;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.ViewModels;

/// <summary>One row in the torrent list. Updated in place from engine snapshots so the list does not flicker.</summary>
public partial class TorrentItemViewModel(string id) : ObservableObject
{
    public string Id { get; } = id;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial TorrentStatus Status { get; set; }

    /// <summary>0–100 for the ProgressBar.</summary>
    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string SizeText { get; set; } = "";

    [ObservableProperty]
    public partial string SpeedText { get; set; } = "";

    [ObservableProperty]
    public partial string PeersText { get; set; } = "";

    public bool CanPause => Status is not (TorrentStatus.Paused or TorrentStatus.Stopped or TorrentStatus.Stopping);

    public void Update(TorrentSnapshot s)
    {
        Name = s.Name;
        Status = s.Status;
        ProgressPercent = s.Progress * 100;
        IsIndeterminate = s.Status is TorrentStatus.FetchingMetadata or TorrentStatus.Starting;
        IsPaused = s.Status is TorrentStatus.Paused or TorrentStatus.Stopped;
        IsError = s.Status == TorrentStatus.Error;

        StatusText = s.Status switch
        {
            TorrentStatus.Downloading when s.EstimatedTimeLeft is { } eta =>
                Strings.Format("Status_DownloadingEta", Formatting.Duration(eta)),
            TorrentStatus.Error when s.ErrorMessage is not null =>
                Strings.Format("Status_ErrorDetails", s.ErrorMessage),
            _ => Strings.Get("Status_" + s.Status),
        };

        SizeText = s.TotalSize > 0
            ? Strings.Format("Size_Progress", Formatting.Bytes(s.DownloadedBytes), Formatting.Bytes(s.TotalSize), (int)(s.Progress * 100))
            : "";

        SpeedText = s.Status is TorrentStatus.Downloading or TorrentStatus.Seeding
            ? $"↓ {Formatting.Rate(s.DownloadRate)}   ↑ {Formatting.Rate(s.UploadRate)}"
            : "";

        PeersText = s.Status is TorrentStatus.Downloading or TorrentStatus.Seeding or TorrentStatus.FetchingMetadata
            ? Strings.Format("Peers", s.Seeds, s.Leechers)
            : "";

        OnPropertyChanged(nameof(CanPause));
    }
}
