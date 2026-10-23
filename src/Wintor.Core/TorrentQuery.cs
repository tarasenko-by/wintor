using System.Globalization;

namespace Wintor.Core;

public enum TorrentFilter
{
    All,
    Downloading,
    Completed,
    Paused,
}

/// <summary>Columns of the torrent table that can be sorted on. <see cref="Queue"/> is the user's own order (№).</summary>
public enum TorrentSort
{
    Queue,
    Name,
    Size,
    Progress,
    Status,
    DownloadSpeed,
    UploadSpeed,
    TimeLeft,
    Seeds,
    Peers,
}

/// <summary>Filter, search and sort for the torrent list.</summary>
public static class TorrentQuery
{
    /// <summary>
    /// Orders torrents by a column. Ties keep the user's order, so equal rows never jump around between refreshes.
    /// Torrents missing from <paramref name="queue"/> go last, in the order given.
    /// </summary>
    public static List<TorrentSnapshot> Sort(
        IReadOnlyList<TorrentSnapshot> torrents,
        TorrentSort sort,
        bool descending,
        IReadOnlyList<string> queue)
    {
        var position = new Dictionary<string, int>();
        for (int i = 0; i < queue.Count; i++)
        {
            position.TryAdd(queue[i], i);
        }

        int Queue(TorrentSnapshot t) => position.TryGetValue(t.Id, out int p) ? p : queue.Count + IndexIn(torrents, t);

        IOrderedEnumerable<TorrentSnapshot> ordered = sort switch
        {
            TorrentSort.Name => Order(t => t.Name, StringComparer.CurrentCultureIgnoreCase),
            TorrentSort.Size => Order(t => t.TotalSize),
            TorrentSort.Progress => Order(t => t.Progress),
            TorrentSort.Status => Order(t => StatusRank(t)),
            TorrentSort.DownloadSpeed => Order(t => t.DownloadRate),
            TorrentSort.UploadSpeed => Order(t => t.UploadRate),
            // "Unknown" sorts after every real estimate, whichever the direction.
            TorrentSort.TimeLeft => descending
                ? torrents.OrderBy(t => t.EstimatedTimeLeft is null ? 1 : 0).ThenByDescending(t => t.EstimatedTimeLeft)
                : torrents.OrderBy(t => t.EstimatedTimeLeft ?? TimeSpan.MaxValue),
            TorrentSort.Seeds => Order(t => t.Seeds),
            TorrentSort.Peers => Order(t => t.Leechers),
            _ => Order(Queue),
        };
        return ordered.ThenBy(Queue).ToList();

        IOrderedEnumerable<TorrentSnapshot> Order<TKey>(Func<TorrentSnapshot, TKey> key, IComparer<TKey>? comparer = null) =>
            descending ? torrents.OrderByDescending(key, comparer) : torrents.OrderBy(key, comparer);
    }

    /// <summary>Active first: downloading, getting metadata, checking, seeding, then paused, stopped, errors.</summary>
    private static int StatusRank(TorrentSnapshot t) => t.Status switch
    {
        TorrentStatus.Downloading => 0,
        TorrentStatus.FetchingMetadata or TorrentStatus.Starting => 1,
        TorrentStatus.Checking => 2,
        TorrentStatus.Seeding => 3,
        TorrentStatus.Paused => 4,
        TorrentStatus.Stopped or TorrentStatus.Stopping => 5,
        _ => 6,
    };

    private static int IndexIn(IReadOnlyList<TorrentSnapshot> list, TorrentSnapshot t)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], t))
            {
                return i;
            }
        }

        return list.Count;
    }

    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth;

    public static bool Matches(TorrentSnapshot torrent, TorrentFilter filter, string? search) =>
        MatchesFilter(torrent, filter) && MatchesSearch(torrent.Name, search);

    public static bool MatchesFilter(TorrentSnapshot torrent, TorrentFilter filter) => filter switch
    {
        TorrentFilter.Downloading => !torrent.IsComplete && torrent.Status is not (TorrentStatus.Paused or TorrentStatus.Stopped),
        TorrentFilter.Completed => torrent.IsComplete,
        TorrentFilter.Paused => torrent.Status is TorrentStatus.Paused or TorrentStatus.Stopped,
        _ => true,
    };

    /// <summary>
    /// Every word of the query must occur in the name, in any order, ignoring case, accents and width
    /// ("ubuntu 24" finds "Ubuntu-24.04-desktop-amd64.iso").
    /// </summary>
    public static bool MatchesSearch(string name, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
        return search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(word => compare.IndexOf(name, word, Loose) >= 0);
    }
}
