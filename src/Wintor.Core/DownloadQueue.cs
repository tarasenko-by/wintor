namespace Wintor.Core;

/// <summary>What the download queue does next: torrents to start, and running ones to hold back as Queued.</summary>
public sealed record QueuePlan(IReadOnlyList<string> Start, IReadOnlyList<string> Hold)
{
    public static readonly QueuePlan Nothing = new([], []);

    public bool IsEmpty => Start.Count == 0 && Hold.Count == 0;
}

/// <summary>
/// The automatic download queue. At most <c>limit</c> torrents download at a time; the rest wait as
/// <see cref="TorrentStatus.Queued"/> and start in the user's order (the № column) as downloads finish, are paused
/// or removed. Seeding does not take a slot.
/// <para>
/// A torrent the user started by hand ("forced") runs even when every slot is taken and is never held back, but it
/// takes a slot while it downloads, so the queue waits until the total is under the limit again. Nobody else is paused
/// to make room for it: a download the user did not touch never stops because of another one.
/// </para>
/// <para>
/// A running download is held back only when, not counting the forced ones, more run than the limit allows (the limit
/// was lowered, or Wintor started everything after a restart): the lowest in the order first. One that is starting or
/// checking its files is left to finish that, since stopping it would throw the check away.
/// </para>
/// </summary>
public static class DownloadQueue
{
    /// <summary>The most a user can set; more than this is not a queue any more.</summary>
    public const int MaxLimit = 100;

    /// <summary>Downloading now, so it takes a slot. A queued torrent does not: it is waiting for one.</summary>
    public static bool IsActive(TorrentSnapshot t) =>
        t.Status is TorrentStatus.Downloading or TorrentStatus.FetchingMetadata
        || (t.Status is TorrentStatus.Starting or TorrentStatus.Checking && !t.IsComplete);

    /// <summary>
    /// What to start and what to hold back so that at most <paramref name="limit"/> torrents download; 0 or less
    /// means no limit (everything queued starts). Torrents missing from <paramref name="order"/> come last, in the order
    /// given.
    /// </summary>
    public static QueuePlan Plan(
        IReadOnlyList<TorrentSnapshot> torrents,
        IReadOnlyList<string> order,
        int limit,
        IReadOnlySet<string> forced)
    {
        List<TorrentSnapshot> ordered = TorrentQuery.Sort(torrents, TorrentSort.Queue, descending: false, order);
        var start = new List<string>();
        var hold = new List<string>();

        // A finished torrent that somehow ended up queued only seeds: it never needs a slot.
        start.AddRange(ordered.Where(t => t.Status == TorrentStatus.Queued && (t.IsComplete || limit <= 0)).Select(t => t.Id));
        if (limit <= 0)
        {
            return start.Count == 0 ? QueuePlan.Nothing : new QueuePlan(start, hold);
        }

        int active = ordered.Count(IsActive);
        int unforced = ordered.Count(t => IsActive(t) && !forced.Contains(t.Id));
        if (unforced > limit)
        {
            foreach (TorrentSnapshot t in Enumerable.Reverse(ordered))
            {
                if (unforced <= limit)
                {
                    break;
                }

                if (t.Status is TorrentStatus.Downloading or TorrentStatus.FetchingMetadata && !forced.Contains(t.Id))
                {
                    hold.Add(t.Id);
                    unforced--;
                }
            }
        }
        else
        {
            foreach (TorrentSnapshot t in ordered.Where(t => t.Status == TorrentStatus.Queued && !t.IsComplete))
            {
                if (active >= limit)
                {
                    break;
                }

                start.Add(t.Id);
                active++;
            }
        }

        return start.Count == 0 && hold.Count == 0 ? QueuePlan.Nothing : new QueuePlan(start, hold);
    }
}
