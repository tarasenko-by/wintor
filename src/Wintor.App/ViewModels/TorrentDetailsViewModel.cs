using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.ViewModels;

public partial class FileItemViewModel(TorrentDetailsViewModel owner, int index) : ObservableObject
{
    private bool _updating;

    public int Index { get; } = index;

    [ObservableProperty]
    public partial string Path { get; set; } = "";

    [ObservableProperty]
    public partial string SizeText { get; set; } = "";

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSkipped { get; set; }

    /// <summary>A video or audio file: the row gets a Play button.</summary>
    [ObservableProperty]
    public partial bool IsPlayable { get; set; }

    /// <summary>ComboBox index: 0 skip, 1 low, 2 normal, 3 high (the order of <see cref="FilePriority"/>).</summary>
    [ObservableProperty]
    public partial int PriorityIndex { get; set; }

    public void Update(TorrentFileEntry file)
    {
        _updating = true;
        Path = file.Path;
        SizeText = Formatting.Bytes(file.Size);
        ProgressPercent = file.Progress * 100;
        ProgressText = $"{file.Progress * 100:0.#}%";
        IsSkipped = file.Priority == FilePriority.Skip;
        IsPlayable = MediaFiles.IsPlayable(file.Path);
        PriorityIndex = (int)file.Priority;
        _updating = false;
    }

    partial void OnPriorityIndexChanged(int value)
    {
        if (!_updating && value >= 0)
        {
            _ = owner.SetPriorityAsync(Index, (FilePriority)value);
        }
    }
}

public sealed record PeerRow(string Address, string Client, string ProgressText, string DownloadText, string UploadText, string Flags)
{
    /// <summary>For screen readers: the whole row in one line.</summary>
    public string AccessibleName => Join(Address, Client, ProgressText, DownloadText, UploadText);

    internal static string Join(params string?[] parts) => string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p)));
}

public sealed record TrackerRow(string Url, string StatusText, string? Message, bool IsProblem)
{
    public bool HasMessage => !string.IsNullOrEmpty(Message);

    /// <summary>For screen readers: the whole row in one line.</summary>
    public string AccessibleName => PeerRow.Join(Url, StatusText, Message);
}

/// <summary>One "label: value" pair in the General tab.</summary>
public sealed record InfoRow(string Label, string Value);

/// <summary>The details pane under the torrent table: whichever torrent is selected, refreshed every second.</summary>
public partial class TorrentDetailsViewModel : ObservableObject
{
    private readonly ITorrentEngine _engine;
    private readonly SpeedHistory _speedHistory;
    private readonly DispatcherQueueTimer _timer;
    private readonly Dictionary<int, FileItemViewModel> _files = [];
    private bool _refreshing;

    public TorrentDetailsViewModel(ITorrentEngine engine, SpeedHistory speedHistory)
    {
        _engine = engine;
        _speedHistory = speedHistory;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public string? Id { get; private set; }

    public ObservableCollection<FileItemViewModel> Files { get; } = [];

    public ObservableCollection<PeerRow> Peers { get; } = [];

    public ObservableCollection<TrackerRow> Trackers { get; } = [];

    public ObservableCollection<InfoRow> Transfer { get; } = [];

    public ObservableCollection<InfoRow> General { get; } = [];

    /// <summary>Nothing selected: the pane shows a hint instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty]
    public partial bool HasTorrent { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<double> Downloaded { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<double> Available { get; set; } = [];

    [ObservableProperty]
    public partial string DownloadedText { get; set; } = "";

    [ObservableProperty]
    public partial string AvailabilityText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<long> DownloadHistory { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<long> UploadHistory { get; set; } = [];

    [ObservableProperty]
    public partial string SpeedNowText { get; set; } = "";

    [ObservableProperty]
    public partial bool NoFiles { get; set; }

    [ObservableProperty]
    public partial bool NoPeers { get; set; } = true;

    [ObservableProperty]
    public partial bool NoTrackers { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Shows a torrent (or nothing for null). Keeps refreshing until <see cref="Stop"/>.</summary>
    public async Task ShowAsync(string? id)
    {
        if (id == Id)
        {
            return;
        }

        Id = id;
        Files.Clear();
        _files.Clear();
        Peers.Clear();
        Trackers.Clear();
        Transfer.Clear();
        General.Clear();
        // Recorded for every torrent all along, so the graph is full the moment it is shown.
        DownloadHistory = id is null ? [] : _speedHistory.Downloads(id);
        UploadHistory = id is null ? [] : _speedHistory.Uploads(id);
        Downloaded = [];
        Available = [];
        IsEmpty = id is null;
        HasTorrent = id is not null;
        if (id is null)
        {
            _timer.Stop();
            return;
        }

        await RefreshAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public Task SetPriorityAsync(int index, FilePriority priority) =>
        Id is { } id ? Run(() => _engine.SetFilePriorityAsync(id, index, priority)) : Task.CompletedTask;

    private async Task Run(Func<Task> action)
    {
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
        }
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || Id is not { } id)
        {
            return;
        }

        _refreshing = true;
        try
        {
            TorrentDetails? details = await _engine.GetDetailsAsync(id);
            if (details is null || id != Id)
            {
                if (details is null)
                {
                    await ShowAsync(null);
                }

                return;
            }

            TorrentSnapshot s = details.Summary;
            Name = s.Name;
            Downloaded = details.Downloaded;
            Available = details.Available;
            DownloadedText = s.TotalSize > 0 ? $"{s.Progress * 100:0.#}%" : "";
            // Copies among connected peers: without peers there is nothing to count.
            AvailabilityText = details.Peers.Count > 0 ? details.Availability.ToString("0.000") : "—";

            DownloadHistory = _speedHistory.Downloads(id);
            UploadHistory = _speedHistory.Uploads(id);
            SpeedNowText = $"↓ {Formatting.Rate(s.DownloadRate)}   ↑ {Formatting.Rate(s.UploadRate)}";

            UpdateFiles(details.Files);
            Replace(Peers, details.Peers.OrderByDescending(p => p.DownloadRate + p.UploadRate).Select(ToRow).ToList());
            Replace(Trackers, details.Trackers.Select(ToRow).ToList());
            Replace(Transfer, TransferRows(details));
            Replace(General, GeneralRows(details));
            NoFiles = Files.Count == 0;
            NoPeers = Peers.Count == 0;
            NoTrackers = Trackers.Count == 0;
        }
        catch (Exception)
        {
            // A torrent removed mid-refresh or an engine hiccup: the next tick tries again.
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateFiles(IReadOnlyList<TorrentFileEntry> files)
    {
        if (files.Count != _files.Count)
        {
            Files.Clear();
            _files.Clear();
            foreach (TorrentFileEntry file in files.OrderBy(f => f.Path, StringComparer.CurrentCultureIgnoreCase))
            {
                var item = new FileItemViewModel(this, file.Index);
                _files[file.Index] = item;
                Files.Add(item);
            }
        }

        foreach (TorrentFileEntry file in files)
        {
            _files[file.Index].Update(file);
        }
    }

    /// <summary>Replaces a list's rows only where they changed, so the list keeps its scroll position.</summary>
    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> rows)
    {
        while (target.Count > rows.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (i >= target.Count)
            {
                target.Add(rows[i]);
            }
            else if (!Equals(target[i], rows[i]))
            {
                target[i] = rows[i];
            }
        }
    }

    private static PeerRow ToRow(PeerEntry p) => new(
        p.Address,
        p.Client,
        $"{p.Progress * 100:0.#}%",
        Formatting.Rate(p.DownloadRate),
        Formatting.Rate(p.UploadRate),
        string.Join(" · ", new[]
        {
            p.IsSeeder ? Strings.Get("Peer_Seed") : null,
            p.IsIncoming ? Strings.Get("Peer_Incoming") : null,
            p.IsEncrypted ? Strings.Get("Peer_Encrypted") : null,
        }.Where(f => f is not null)));

    private static TrackerRow ToRow(TrackerEntry t) => new(
        t.Url,
        Strings.Get("Tracker_" + t.Status),
        t.Message,
        t.Status is TrackerStatus.Offline or TrackerStatus.Error);

    private static List<InfoRow> TransferRows(TorrentDetails d)
    {
        TorrentSnapshot s = d.Summary;
        TransferInfo t = d.Transfer;
        return
        [
            new(Strings.Get("Info_TimeActive"), t.TimeActive > TimeSpan.Zero ? Formatting.Duration(t.TimeActive) : "—"),
            new(Strings.Get("Info_TimeLeft"), s.EstimatedTimeLeft is { } eta ? Formatting.Duration(eta) : s.IsComplete ? "—" : "∞"),
            new(Strings.Get("Info_Downloaded"), Formatting.Bytes(t.Downloaded)),
            new(Strings.Get("Info_Uploaded"), Formatting.Bytes(t.Uploaded)),
            new(Strings.Get("Info_Remaining"), Formatting.Bytes(t.Remaining)),
            new(Strings.Get("Info_Ratio"), t.Ratio.ToString("0.000")),
            new(Strings.Get("Info_DownloadSpeed"), Formatting.Rate(s.DownloadRate)),
            new(Strings.Get("Info_UploadSpeed"), Formatting.Rate(s.UploadRate)),
            new(Strings.Get("Info_DownloadLimit"), s.DownloadLimit > 0 ? Formatting.Rate(s.DownloadLimit) : Strings.Get("Info_NoLimit")),
            new(Strings.Get("Info_UploadLimit"), s.UploadLimit > 0 ? Formatting.Rate(s.UploadLimit) : Strings.Get("Info_NoLimit")),
            new(Strings.Get("Info_Seeds"), Strings.Format("Info_Connected", t.ConnectedSeeds)),
            new(Strings.Get("Info_Peers"), Strings.Format("Info_Connected", t.ConnectedLeechers)),
            new(Strings.Get("Info_Wasted"), Formatting.Bytes(t.Wasted)),
        ];
    }

    private static List<InfoRow> GeneralRows(TorrentDetails d)
    {
        TorrentSnapshot s = d.Summary;
        var rows = new List<InfoRow>
        {
            new(Strings.Get("Info_SavedTo"), s.ContentPath),
            new(Strings.Get("Info_Size"), s.TotalSize > 0 ? Formatting.Bytes(s.TotalSize) : "—"),
        };
        if (d.Transfer.PieceCount > 0)
        {
            rows.Add(new(Strings.Get("Info_Pieces"), Strings.Format("Info_PiecesValue", d.Transfer.PieceCount, Formatting.Bytes(d.Transfer.PieceLength))));
        }

        rows.Add(new(Strings.Get("Info_Hash"), d.InfoHash.ToLowerInvariant()));
        if (d.CreatedOn is { } created)
        {
            rows.Add(new(Strings.Get("Info_Created"), created.ToLocalTime().ToString("g")));
        }

        if (d.Comment is { } comment)
        {
            rows.Add(new(Strings.Get("Info_Comment"), comment));
        }

        rows.Add(new(Strings.Get("Info_Private"), Strings.Get(d.IsPrivate ? "Yes" : "No")));
        return rows;
    }
}
