namespace Wintor.Core;

public enum FilePriority
{
    Skip,
    Low,
    Normal,
    High,
}

public enum TrackerStatus
{
    Unknown,
    Connecting,
    Working,
    Offline,
    Error,
}

/// <param name="Index">Position in the torrent; used to change the priority.</param>
/// <param name="Path">Relative to the torrent's folder.</param>
/// <param name="Progress">0.0 to 1.0.</param>
public sealed record TorrentFileEntry(int Index, string Path, long Size, double Progress, FilePriority Priority)
{
    /// <summary>Where the file is on disk now (an unfinished file may have another name until it completes).</summary>
    public string FullPath { get; init; } = "";

    public bool IsComplete => Progress >= 1;
}

/// <param name="Progress">How much of the torrent the peer has, 0.0 to 1.0.</param>
public sealed record PeerEntry(
    string Address,
    string Client,
    double Progress,
    long DownloadRate,
    long UploadRate,
    bool IsSeeder,
    bool IsIncoming,
    bool IsEncrypted);

public sealed record TrackerEntry(string Url, TrackerStatus Status, string? Message);

/// <summary>Everything the details pane shows about one torrent.</summary>
public sealed record TorrentDetails(
    TorrentSnapshot Summary,
    string InfoHash,
    string? MagnetLink,
    string? Comment,
    DateTime? CreatedOn,
    bool IsPrivate,
    IReadOnlyList<TorrentFileEntry> Files,
    IReadOnlyList<PeerEntry> Peers,
    IReadOnlyList<TrackerEntry> Trackers)
{
    public TransferInfo Transfer { get; init; } = TransferInfo.Empty;

    /// <summary>Which parts are downloaded: <see cref="PieceMap.Buckets"/> values, 0.0 to 1.0 each.</summary>
    public IReadOnlyList<double> Downloaded { get; init; } = [];

    /// <summary>Which parts someone has (us or a connected peer), same buckets as <see cref="Downloaded"/>.</summary>
    public IReadOnlyList<double> Available { get; init; } = [];

    /// <summary>Distributed copies among connected peers, as in µTorrent: 1.5 = every part once, half of them twice.</summary>
    public double Availability { get; init; }
}

/// <summary>The "Transfer" block: what happened in this session and overall.</summary>
public sealed record TransferInfo(
    TimeSpan TimeActive,
    long Downloaded,
    long Uploaded,
    long Remaining,
    long Wasted,
    int PieceCount,
    long PieceLength,
    int ConnectedSeeds,
    int ConnectedLeechers)
{
    public static TransferInfo Empty { get; } = new(TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Uploaded divided by downloaded this session; 0 before anything was downloaded.</summary>
    public double Ratio => Downloaded > 0 ? (double)Uploaded / Downloaded : 0;
}

/// <summary>Folds per-piece facts into a fixed number of buckets for drawing a bar of any width.</summary>
public static class PieceMap
{
    public const int Buckets = 240;

    /// <summary>Fraction of pieces in each bucket for which <paramref name="has"/> is true.</summary>
    public static double[] Fold(int pieceCount, Func<int, bool> has, int buckets = Buckets)
    {
        if (pieceCount <= 0)
        {
            return [];
        }

        int count = Math.Min(buckets, pieceCount);
        var result = new double[count];
        for (int b = 0; b < count; b++)
        {
            int first = (int)((long)b * pieceCount / count);
            int last = (int)((long)(b + 1) * pieceCount / count);
            int hits = 0;
            for (int p = first; p < last; p++)
            {
                if (has(p))
                {
                    hits++;
                }
            }

            result[b] = last > first ? (double)hits / (last - first) : 0;
        }

        return result;
    }

    /// <summary>µTorrent-style distributed copies from how many peers have each piece.</summary>
    public static double DistributedCopies(IReadOnlyList<int> copiesPerPiece)
    {
        if (copiesPerPiece.Count == 0)
        {
            return 0;
        }

        int min = copiesPerPiece.Min();
        int above = copiesPerPiece.Count(c => c > min);
        return min + (double)above / copiesPerPiece.Count;
    }
}

/// <summary>What a .torrent file contains, read before it is added (to pick files).</summary>
public sealed record TorrentMetadata(string Name, long TotalSize, IReadOnlyList<TorrentMetadataFile> Files);

public sealed record TorrentMetadataFile(int Index, string Path, long Size);
