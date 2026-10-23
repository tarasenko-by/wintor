namespace Wintor.Core;

/// <summary>
/// The only way the app talks to a BitTorrent implementation.
/// Keeps the UI independent of the concrete engine (MonoTorrent today, maybe libtorrent later).
/// </summary>
public interface ITorrentEngine : IAsyncDisposable
{
    /// <summary>Loads saved torrents and starts listening for peers.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies changed limits and port while running.</summary>
    Task ApplyOptionsAsync(EngineOptions options);

    /// <summary>Adds a magnet link and starts it. Returns the torrent id; an already added torrent returns its existing id.</summary>
    Task<string> AddMagnetAsync(string magnetLink, string saveDirectory);

    /// <summary>Reads a .torrent file without adding it, so the user can pick files.</summary>
    Task<TorrentMetadata> ReadTorrentFileAsync(string torrentFilePath);

    /// <summary>
    /// Adds a .torrent file and starts it, skipping the files whose indexes are in <paramref name="skippedFiles"/>.
    /// Returns the torrent id; an already added torrent returns its existing id.
    /// </summary>
    Task<string> AddTorrentFileAsync(string torrentFilePath, string saveDirectory, IReadOnlySet<int>? skippedFiles = null);

    Task StartAsync(string id);

    Task PauseAsync(string id);

    /// <summary>Stops and removes a torrent, optionally deleting the downloaded files.</summary>
    Task RemoveAsync(string id, bool deleteFiles);

    Task SetFilePriorityAsync(string id, int fileIndex, FilePriority priority);

    /// <summary>Speed limits for one torrent, in bytes per second; 0 means no limit of its own (the global one still applies).</summary>
    Task SetSpeedLimitsAsync(string id, int maxDownloadRate, int maxUploadRate);

    /// <summary>Current state of every torrent. Cheap enough to call once per second.</summary>
    IReadOnlyList<TorrentSnapshot> GetSnapshots();

    /// <summary>Files, peers and trackers of one torrent, or null if it is gone.</summary>
    Task<TorrentDetails?> GetDetailsAsync(string id);

    /// <summary>Persists the torrent list so it survives a restart.</summary>
    Task SaveStateAsync();
}
