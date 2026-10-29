namespace Wintor.Core;

/// <summary>What Wintor does once the torrents are done (the side pane's Actions → "When done").</summary>
public enum WhenDoneAction
{
    None,
    Quit,
    Sleep,
    Hibernate,
    Restart,
    ShutDown,
}

/// <summary>What "done" means for a <see cref="WhenDoneAction"/>.</summary>
public enum WhenDoneCondition
{
    /// <summary>Nothing is downloading any more; seeding may go on.</summary>
    DownloadsFinished,

    /// <summary>Nothing is downloading or seeding.</summary>
    AllFinished,
}

/// <summary>An action and when to take it. <see cref="Off"/> does nothing.</summary>
public sealed record WhenDoneRule(WhenDoneAction Action, WhenDoneCondition Condition)
{
    public static readonly WhenDoneRule Off = new(WhenDoneAction.None, WhenDoneCondition.DownloadsFinished);

    public bool IsOff => Action == WhenDoneAction.None;
}

/// <summary>
/// Watches the torrents for a <see cref="WhenDoneRule"/>. It fires once, and only after it has seen something to
/// wait for: picking "Shut down when downloads finish" with nothing downloading must not shut down at once, it waits
/// for the torrent the user is about to add.
/// </summary>
public sealed class WhenDoneWatch
{
    private bool _sawWork;

    public WhenDoneRule Rule { get; private set; } = WhenDoneRule.Off;

    /// <summary>Starts watching for <paramref name="rule"/> from now on (or stops, with <see cref="WhenDoneRule.Off"/>).</summary>
    public void Set(WhenDoneRule rule)
    {
        Rule = rule;
        _sawWork = false;
    }

    /// <summary>
    /// Call with every refresh. True once, when the rule's condition has just been met; the rule then goes back to
    /// <see cref="WhenDoneRule.Off"/>, so a cancelled countdown does not start again on the next refresh.
    /// </summary>
    public bool Check(IReadOnlyList<TorrentSnapshot> torrents)
    {
        if (Rule.IsOff)
        {
            return false;
        }

        if (!IsDone(torrents, Rule.Condition))
        {
            _sawWork = true;
            return false;
        }

        if (!_sawWork)
        {
            return false;
        }

        Set(WhenDoneRule.Off);
        return true;
    }

    /// <summary>Whether no torrent is left that <paramref name="condition"/> waits for.</summary>
    public static bool IsDone(IReadOnlyList<TorrentSnapshot> torrents, WhenDoneCondition condition) =>
        condition switch
        {
            WhenDoneCondition.AllFinished => !torrents.Any(t => IsDownloading(t) || IsSeeding(t)),
            _ => !torrents.Any(IsDownloading),
        };

    /// <summary>
    /// Still going to download something. Paused, stopped and failed torrents do not count: the user stopped them,
    /// or they need the user (as "Downloading" in the side pane does not wait for them either).
    /// </summary>
    public static bool IsDownloading(TorrentSnapshot t) =>
        t.Status is TorrentStatus.Downloading or TorrentStatus.FetchingMetadata
        || (t.Status is TorrentStatus.Starting or TorrentStatus.Checking && !t.IsComplete);

    /// <summary>Uploading a finished torrent, or about to (a finished one starting or being checked).</summary>
    public static bool IsSeeding(TorrentSnapshot t) =>
        t.Status == TorrentStatus.Seeding
        || (t.Status is TorrentStatus.Starting or TorrentStatus.Checking && t.IsComplete);
}
