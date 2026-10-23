namespace Wintor.Core;

/// <summary>
/// The only way the app talks to a BitTorrent implementation.
/// Keeps the UI independent of the concrete engine (MonoTorrent today, maybe libtorrent later).
/// </summary>
public interface ITorrentEngine : IAsyncDisposable
{
    /// <summary>Loads saved torrents and starts listening for peers.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a magnet link and starts it. Returns the torrent id; an already added torrent returns its existing id.</summary>
    Task<string> AddMagnetAsync(string magnetLink, string saveDirectory);

    /// <summary>Adds a .torrent file and starts it. Returns the torrent id; an already added torrent returns its existing id.</summary>
    Task<string> AddTorrentFileAsync(string torrentFilePath, string saveDirectory);

    Task StartAsync(string id);

    Task PauseAsync(string id);

    /// <summary>Stops and removes a torrent, optionally deleting the downloaded files.</summary>
    Task RemoveAsync(string id, bool deleteFiles);

    /// <summary>Current state of every torrent. Cheap enough to call once per second.</summary>
    IReadOnlyList<TorrentSnapshot> GetSnapshots();

    /// <summary>Persists the torrent list so it survives a restart.</summary>
    Task SaveStateAsync();
}
