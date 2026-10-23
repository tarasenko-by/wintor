using System.Net;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using MonoTorrent.Trackers;
using Wintor.Core;

namespace Wintor.Engine.MonoTorrent;

/// <summary>
/// <see cref="ITorrentEngine"/> on top of MonoTorrent (https://github.com/alanmcgovern/monotorrent).
/// </summary>
public sealed class MonoTorrentEngine(EngineOptions options) : ITorrentEngine
{
    private EngineOptions _options = options;
    private ClientEngine? _engine;

    // MonoTorrent restores every torrent as stopped, so we remember which ones the user paused.
    private HashSet<string> _pausedIds = [];

    // Monitor.DownloadRate/UploadRate include protocol overhead (handshakes, keep-alives, "have"
    // messages), so a finished torrent would show traffic. We measure payload bytes only.
    private readonly Dictionary<string, (RateMeter Down, RateMeter Up)> _rates = [];

    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(5);

    private string StatePath => Path.Combine(_options.DataDirectory, "engine.state");

    private string PausedPath => Path.Combine(_options.DataDirectory, "paused.txt");

    private ClientEngine Engine =>
        _engine ?? throw new InvalidOperationException("Call InitializeAsync first.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_engine is not null)
        {
            return;
        }

        Directory.CreateDirectory(_options.DataDirectory);
        EngineSettings settings = CreateSettings();

        ClientEngine engine;
        if (File.Exists(StatePath))
        {
            try
            {
                engine = await ClientEngine.RestoreStateAsync(StatePath);
                await engine.UpdateSettingsAsync(settings);
            }
            catch (Exception)
            {
                // A corrupt state file must not prevent the app from starting; keep it for diagnosis.
                File.Move(StatePath, StatePath + ".corrupt", overwrite: true);
                engine = new ClientEngine(settings);
            }
        }
        else
        {
            engine = new ClientEngine(settings);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _engine = engine;
        _pausedIds = await LoadPausedIdsAsync();

        foreach (TorrentManager manager in engine.Torrents)
        {
            if (manager.State == TorrentState.Stopped && !_pausedIds.Contains(IdOf(manager)))
            {
                await manager.StartAsync();
            }
        }
    }

    public async Task<string> AddMagnetAsync(string magnetLink, string saveDirectory)
    {
        MagnetLink magnet = MagnetLink.Parse(magnetLink.Trim());
        TorrentManager? existing = Find(magnet.InfoHashes);
        if (existing is not null)
        {
            return IdOf(existing);
        }

        Directory.CreateDirectory(saveDirectory);
        TorrentManager manager = await Engine.AddAsync(magnet, saveDirectory);
        await manager.StartAsync();
        await SaveStateAsync();
        return IdOf(manager);
    }

    public async Task ApplyOptionsAsync(EngineOptions options)
    {
        _options = options;
        if (_engine is not null)
        {
            await _engine.UpdateSettingsAsync(CreateSettings());
        }
    }

    public async Task<TorrentMetadata> ReadTorrentFileAsync(string torrentFilePath)
    {
        Torrent torrent = await Torrent.LoadAsync(torrentFilePath);
        return new TorrentMetadata(
            torrent.Name,
            ContentSize(torrent),
            torrent.Files.Select((f, i) => new TorrentMetadataFile(i, f.Path, f.Length)).ToList());
    }

    public async Task<string> AddTorrentFileAsync(string torrentFilePath, string saveDirectory, IReadOnlySet<int>? skippedFiles = null)
    {
        Torrent torrent = await Torrent.LoadAsync(torrentFilePath);
        TorrentManager? existing = Find(torrent.InfoHashes);
        if (existing is not null)
        {
            return IdOf(existing);
        }

        Directory.CreateDirectory(saveDirectory);
        TorrentManager manager = await Engine.AddAsync(torrent, saveDirectory);
        if (skippedFiles is { Count: > 0 })
        {
            for (int i = 0; i < manager.Files.Count; i++)
            {
                if (skippedFiles.Contains(i))
                {
                    await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
                }
            }
        }

        await manager.StartAsync();
        await SaveStateAsync();
        return IdOf(manager);
    }

    public async Task SetFilePriorityAsync(string id, int fileIndex, FilePriority priority)
    {
        TorrentManager manager = Get(id);
        if (fileIndex < 0 || fileIndex >= manager.Files.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fileIndex));
        }

        await manager.SetFilePriorityAsync(manager.Files[fileIndex], priority switch
        {
            FilePriority.Skip => Priority.DoNotDownload,
            FilePriority.Low => Priority.Low,
            FilePriority.High => Priority.High,
            _ => Priority.Normal,
        });
        await SaveStateAsync();
    }

    public async Task SetSpeedLimitsAsync(string id, int maxDownloadRate, int maxUploadRate)
    {
        TorrentManager manager = Get(id);
        var settings = new TorrentSettingsBuilder(manager.Settings)
        {
            MaximumDownloadRate = Math.Max(maxDownloadRate, 0),
            MaximumUploadRate = Math.Max(maxUploadRate, 0),
        };
        await manager.UpdateSettingsAsync(settings.ToSettings());
        // Torrent settings are part of the engine state, so the limits come back after a restart.
        await SaveStateAsync();
    }

    public async Task<TorrentDetails?> GetDetailsAsync(string id)
    {
        TorrentManager? manager = _engine?.Torrents.FirstOrDefault(t => IdOf(t) == id);
        if (manager is null)
        {
            return null;
        }

        List<PeerId> peers = manager.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Metadata
            ? await manager.GetPeersAsync()
            : [];
        List<PeerId> connected = peers.Where(p => p.IsConnected).ToList();

        // Piece maps only exist once the metadata is known.
        int pieceCount = manager.HasMetadata ? manager.Bitfield.Length : 0;
        ReadOnlyBitField own = manager.Bitfield;
        var copies = new int[pieceCount];
        foreach (PeerId peer in connected)
        {
            if (peer.BitField.Length != pieceCount)
            {
                continue;
            }

            for (int i = 0; i < pieceCount; i++)
            {
                if (peer.BitField[i])
                {
                    copies[i]++;
                }
            }
        }

        TorrentSnapshot summary = ToSnapshot(manager);
        bool running = manager.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Metadata or TorrentState.Hashing;
        long pieceLength = manager.Torrent?.PieceLength ?? 0;

        return new TorrentDetails(
            Summary: summary,
            InfoHash: id,
            MagnetLink: manager.MagnetLink?.ToV1String(),
            Comment: string.IsNullOrWhiteSpace(manager.Torrent?.Comment) ? null : manager.Torrent.Comment,
            CreatedOn: manager.Torrent is { CreationDate: var created } && created > DateTime.UnixEpoch ? created : null,
            IsPrivate: manager.Torrent?.IsPrivate ?? false,
            Files: manager.Files.Select((f, i) => new TorrentFileEntry(
                i,
                f.Path,
                f.Length,
                f.BitField.Length == 0 ? 0 : f.BitField.PercentComplete / 100.0,
                f.Priority switch
                {
                    Priority.DoNotDownload => FilePriority.Skip,
                    Priority.Lowest or Priority.Low => FilePriority.Low,
                    Priority.High or Priority.Highest or Priority.Immediate => FilePriority.High,
                    _ => FilePriority.Normal,
                })).ToList(),
            Peers: connected.Select(p => new PeerEntry(
                p.Uri.Host + ":" + p.Uri.Port,
                p.ClientApp.ToString(),
                p.BitField.Length == 0 ? 0 : p.BitField.PercentComplete / 100.0,
                p.Monitor.DownloadRate,
                p.Monitor.UploadRate,
                p.IsSeeder,
                p.ConnectionDirection == Direction.Incoming,
                p.EncryptionType is not EncryptionType.PlainText)).ToList(),
            Trackers: manager.TrackerManager.Tiers
                .SelectMany(tier => tier.Trackers)
                .Select(t => new TrackerEntry(
                    t.Uri.ToString(),
                    t.Status switch
                    {
                        TrackerState.Connecting => TrackerStatus.Connecting,
                        TrackerState.Ok => TrackerStatus.Working,
                        TrackerState.Offline => TrackerStatus.Offline,
                        TrackerState.InvalidResponse => TrackerStatus.Error,
                        _ => TrackerStatus.Unknown,
                    },
                    string.IsNullOrEmpty(t.FailureMessage) ? (string.IsNullOrEmpty(t.WarningMessage) ? null : t.WarningMessage) : t.FailureMessage))
                .ToList())
        {
            Transfer = new TransferInfo(
                TimeActive: running && manager.StartTime > DateTime.MinValue ? DateTime.Now - manager.StartTime : TimeSpan.Zero,
                Downloaded: manager.Monitor.DataBytesReceived,
                Uploaded: manager.Monitor.DataBytesSent,
                Remaining: Math.Max(summary.TotalSize - summary.DownloadedBytes, 0),
                Wasted: manager.HashFails * pieceLength,
                PieceCount: pieceCount,
                PieceLength: pieceLength,
                ConnectedSeeds: connected.Count(p => p.IsSeeder),
                ConnectedLeechers: connected.Count(p => !p.IsSeeder)),
            Downloaded = PieceMap.Fold(pieceCount, i => own[i]),
            Available = PieceMap.Fold(pieceCount, i => own[i] || copies[i] > 0),
            Availability = PieceMap.DistributedCopies(copies),
        };
    }

    public async Task StartAsync(string id)
    {
        TorrentManager manager = Get(id);
        if (manager.State is TorrentState.Paused or TorrentState.Stopped or TorrentState.Error or TorrentState.HashingPaused)
        {
            await manager.StartAsync();
        }

        if (_pausedIds.Remove(id))
        {
            await SavePausedIdsAsync();
        }
    }

    public async Task PauseAsync(string id)
    {
        TorrentManager manager = Get(id);
        if (manager.State is not (TorrentState.Paused or TorrentState.Stopped or TorrentState.HashingPaused))
        {
            await manager.PauseAsync();
        }

        if (_pausedIds.Add(id))
        {
            await SavePausedIdsAsync();
        }
    }

    public async Task RemoveAsync(string id, bool deleteFiles)
    {
        TorrentManager manager = Get(id);
        if (manager.State != TorrentState.Stopped)
        {
            await manager.StopAsync();
        }

        await Engine.RemoveAsync(manager, deleteFiles ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly);
        if (_pausedIds.Remove(id))
        {
            await SavePausedIdsAsync();
        }

        await SaveStateAsync();
    }

    public IReadOnlyList<TorrentSnapshot> GetSnapshots()
    {
        if (_engine is null)
        {
            return [];
        }

        var snapshots = _engine.Torrents.Select(ToSnapshot).ToList();
        foreach (string id in _rates.Keys.Except(snapshots.Select(s => s.Id)).ToList())
        {
            _rates.Remove(id);
        }

        return snapshots;
    }

    public async Task SaveStateAsync()
    {
        if (_engine is not null)
        {
            await _engine.SaveStateAsync(StatePath);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_engine is null)
        {
            return;
        }

        // Save before stopping so torrents that were running restart automatically next launch.
        await SaveStateAsync();
        await _engine.StopAllAsync(TimeSpan.FromSeconds(5));
        _engine.Dispose();
        _engine = null;
    }

    private EngineSettings CreateSettings() =>
        new EngineSettingsBuilder
        {
            CacheDirectory = Path.Combine(_options.DataDirectory, "cache"),
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            AutoSaveLoadDhtCache = true,
            AllowPortForwarding = true,
            MaximumDownloadRate = _options.MaxDownloadRate,
            MaximumUploadRate = _options.MaxUploadRate,
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, _options.ListenPort),
                ["ipv6"] = new(IPAddress.IPv6Any, _options.ListenPort),
            },
            DhtEndPoint = new IPEndPoint(IPAddress.Any, _options.ListenPort),
        }.ToSettings();

    private TorrentManager? Find(InfoHashes infoHashes) =>
        Engine.Torrents.FirstOrDefault(t => t.InfoHashes.V1OrV2.Equals(infoHashes.V1OrV2));

    private async Task<HashSet<string>> LoadPausedIdsAsync()
    {
        if (!File.Exists(PausedPath))
        {
            return [];
        }

        try
        {
            return (await File.ReadAllLinesAsync(PausedPath)).Where(l => l.Length > 0).ToHashSet();
        }
        catch (IOException)
        {
            return [];
        }
    }

    private Task SavePausedIdsAsync() => File.WriteAllLinesAsync(PausedPath, _pausedIds);

    private TorrentManager Get(string id) =>
        Engine.Torrents.FirstOrDefault(t => IdOf(t) == id)
        ?? throw new KeyNotFoundException($"Torrent {id} is not in the engine.");

    private static string IdOf(TorrentManager manager) => manager.InfoHashes.V1OrV2.ToHex();

    // Torrent.Size includes the padding hybrid v1/v2 torrents insert between files; users care about real files.
    private static long ContentSize(Torrent torrent) => torrent.Files.Sum(f => f.Length);

    private TorrentSnapshot ToSnapshot(TorrentManager manager)
    {
        string id = IdOf(manager);
        if (!_rates.TryGetValue(id, out (RateMeter Down, RateMeter Up) rate))
        {
            rate = (new RateMeter(RateWindow), new RateMeter(RateWindow));
            _rates[id] = rate;
        }

        var now = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return new(
            Id: id,
            Name: manager.Name,
            Status: ToStatus(manager.State),
            Progress: manager.Progress / 100.0,
            TotalSize: manager.Torrent is { } torrent ? ContentSize(torrent) : manager.MagnetLink?.Size ?? 0,
            DownloadRate: rate.Down.Sample(manager.Monitor.DataBytesReceived, now),
            UploadRate: rate.Up.Sample(manager.Monitor.DataBytesSent, now),
            Seeds: manager.Peers.Seeds,
            Leechers: manager.Peers.Leechs,
            SavePath: manager.SavePath,
            ErrorMessage: manager.Error?.Exception?.Message)
        {
            ContentPath = manager.ContainingDirectory,
            DownloadLimit = manager.Settings.MaximumDownloadRate,
            UploadLimit = manager.Settings.MaximumUploadRate,
        };
    }

    private static TorrentStatus ToStatus(TorrentState state) => state switch
    {
        TorrentState.Stopped => TorrentStatus.Stopped,
        TorrentState.Paused => TorrentStatus.Paused,
        TorrentState.Starting => TorrentStatus.Starting,
        TorrentState.Metadata => TorrentStatus.FetchingMetadata,
        TorrentState.FetchingHashes => TorrentStatus.FetchingMetadata,
        TorrentState.Hashing => TorrentStatus.Checking,
        TorrentState.HashingPaused => TorrentStatus.Paused,
        TorrentState.Downloading => TorrentStatus.Downloading,
        TorrentState.Seeding => TorrentStatus.Seeding,
        TorrentState.Stopping => TorrentStatus.Stopping,
        TorrentState.Error => TorrentStatus.Error,
        _ => TorrentStatus.Stopped,
    };
}
