using System.Net;
using MonoTorrent;
using MonoTorrent.Client;
using Wintor.Core;

namespace Wintor.Engine.MonoTorrent;

/// <summary>
/// <see cref="ITorrentEngine"/> on top of MonoTorrent (https://github.com/alanmcgovern/monotorrent).
/// </summary>
public sealed class MonoTorrentEngine(EngineOptions options) : ITorrentEngine
{
    private ClientEngine? _engine;

    // MonoTorrent restores every torrent as stopped, so we remember which ones the user paused.
    private HashSet<string> _pausedIds = [];

    private string StatePath => Path.Combine(options.DataDirectory, "engine.state");

    private string PausedPath => Path.Combine(options.DataDirectory, "paused.txt");

    private ClientEngine Engine =>
        _engine ?? throw new InvalidOperationException("Call InitializeAsync first.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_engine is not null)
        {
            return;
        }

        Directory.CreateDirectory(options.DataDirectory);
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

        TorrentManager manager = await Engine.AddAsync(magnet, saveDirectory);
        await manager.StartAsync();
        await SaveStateAsync();
        return IdOf(manager);
    }

    public async Task<string> AddTorrentFileAsync(string torrentFilePath, string saveDirectory)
    {
        Torrent torrent = await Torrent.LoadAsync(torrentFilePath);
        TorrentManager? existing = Find(torrent.InfoHashes);
        if (existing is not null)
        {
            return IdOf(existing);
        }

        TorrentManager manager = await Engine.AddAsync(torrent, saveDirectory);
        await manager.StartAsync();
        await SaveStateAsync();
        return IdOf(manager);
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

        return _engine.Torrents.Select(ToSnapshot).ToList();
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
            CacheDirectory = Path.Combine(options.DataDirectory, "cache"),
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            AutoSaveLoadDhtCache = true,
            AllowPortForwarding = true,
            MaximumDownloadRate = options.MaxDownloadRate,
            MaximumUploadRate = options.MaxUploadRate,
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, options.ListenPort),
                ["ipv6"] = new(IPAddress.IPv6Any, options.ListenPort),
            },
            DhtEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort),
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

    private static TorrentSnapshot ToSnapshot(TorrentManager manager) =>
        new(
            Id: IdOf(manager),
            Name: manager.Name,
            Status: ToStatus(manager.State),
            Progress: manager.Progress / 100.0,
            TotalSize: manager.Torrent?.Size ?? manager.MagnetLink?.Size ?? 0,
            DownloadRate: manager.Monitor.DownloadRate,
            UploadRate: manager.Monitor.UploadRate,
            Seeds: manager.Peers.Seeds,
            Leechers: manager.Peers.Leechs,
            SavePath: manager.SavePath,
            ErrorMessage: manager.Error?.Exception?.Message);

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
