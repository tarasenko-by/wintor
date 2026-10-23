namespace Wintor.Core;

public enum TorrentStatus
{
    Stopped,
    Paused,
    Starting,
    FetchingMetadata,
    Checking,
    Downloading,
    Seeding,
    Stopping,
    Error,
}

/// <summary>
/// Immutable point-in-time view of a torrent.
/// </summary>
/// <param name="Id">Info hash in hex; stable across restarts.</param>
/// <param name="Progress">0.0 to 1.0.</param>
/// <param name="TotalSize">In bytes; 0 until the metadata of a magnet link is known.</param>
/// <param name="DownloadRate">Bytes per second.</param>
/// <param name="UploadRate">Bytes per second.</param>
public sealed record TorrentSnapshot(
    string Id,
    string Name,
    TorrentStatus Status,
    double Progress,
    long TotalSize,
    long DownloadRate,
    long UploadRate,
    int Seeds,
    int Leechers,
    string SavePath,
    string? ErrorMessage)
{
    public long DownloadedBytes => (long)(TotalSize * Progress);

    /// <summary>Estimated time left, or null when it cannot be estimated.</summary>
    public TimeSpan? EstimatedTimeLeft =>
        Status == TorrentStatus.Downloading && DownloadRate > 0 && TotalSize > 0
            ? TimeSpan.FromSeconds((TotalSize - DownloadedBytes) / (double)DownloadRate)
            : null;
}
