using Wintor.Core;

namespace Wintor.Core.Tests;

public class DownloadQueueTests
{
    private static TorrentSnapshot Torrent(string id, TorrentStatus status, double progress = 0) =>
        new(id, "Torrent " + id, status, progress, 1000, 0, 0, 0, 0, @"C:\Downloads", null);

    private static readonly HashSet<string> NoneForced = [];

    private static QueuePlan Plan(TorrentSnapshot[] torrents, int limit, IReadOnlySet<string>? forced = null) =>
        DownloadQueue.Plan(torrents, torrents.Select(t => t.Id).ToList(), limit, forced ?? NoneForced);

    [Theory]
    [InlineData(TorrentStatus.Downloading, 0.5, true)]
    [InlineData(TorrentStatus.FetchingMetadata, 0, true)]
    [InlineData(TorrentStatus.Checking, 0.2, true)]
    [InlineData(TorrentStatus.Starting, 0.2, true)]
    [InlineData(TorrentStatus.Checking, 1, false)]
    [InlineData(TorrentStatus.Seeding, 1, false)]
    [InlineData(TorrentStatus.Queued, 0.2, false)]
    [InlineData(TorrentStatus.Paused, 0.5, false)]
    [InlineData(TorrentStatus.Stopped, 0.5, false)]
    [InlineData(TorrentStatus.Error, 0.5, false)]
    public void IsActive_CountsOnlyTorrentsThatTakeASlot(TorrentStatus status, double progress, bool expected) =>
        Assert.Equal(expected, DownloadQueue.IsActive(Torrent("a", status, progress)));

    [Fact]
    public void Plan_StartsQueuedTorrentsInTheUsersOrderUntilTheLimit()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Queued),
            Torrent("c", TorrentStatus.Queued),
            Torrent("d", TorrentStatus.Queued),
        ];

        QueuePlan plan = DownloadQueue.Plan(torrents, ["a", "d", "c", "b"], limit: 3, NoneForced);

        Assert.Equal(["d", "c"], plan.Start);
        Assert.Empty(plan.Hold);
    }

    [Fact]
    public void Plan_WhenADownloadFinishes_StartsTheNextOne()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Seeding, 1),
            Torrent("b", TorrentStatus.Downloading),
            Torrent("c", TorrentStatus.Queued),
            Torrent("d", TorrentStatus.Queued),
        ];

        QueuePlan plan = Plan(torrents, limit: 2);

        Assert.Equal(["c"], plan.Start);
    }

    [Fact]
    public void Plan_PausedErrorAndSeedingTorrentsDoNotTakeASlot()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("seeding", TorrentStatus.Seeding, 1),
            Torrent("paused", TorrentStatus.Paused, 0.5),
            Torrent("error", TorrentStatus.Error, 0.5),
            Torrent("queued", TorrentStatus.Queued),
        ];

        Assert.Equal(["queued"], Plan(torrents, limit: 1).Start);
    }

    [Fact]
    public void Plan_WithEverySlotTaken_DoesNothing()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.FetchingMetadata),
            Torrent("c", TorrentStatus.Queued),
        ];

        Assert.Same(QueuePlan.Nothing, Plan(torrents, limit: 2));
    }

    [Fact]
    public void Plan_OverTheLimit_HoldsBackTheLastInTheOrder()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Downloading),
            Torrent("c", TorrentStatus.FetchingMetadata),
            Torrent("d", TorrentStatus.Downloading),
        ];

        QueuePlan plan = Plan(torrents, limit: 2);

        Assert.Equal(["d", "c"], plan.Hold);
        Assert.Empty(plan.Start);
    }

    [Fact]
    public void Plan_OverTheLimit_LeavesStartingAndCheckingTorrentsAlone()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Downloading),
            Torrent("checking", TorrentStatus.Checking, 0.4),
            Torrent("starting", TorrentStatus.Starting, 0.1),
        ];

        Assert.Equal(["b"], Plan(torrents, limit: 3).Hold);
    }

    [Fact]
    public void Plan_ForcedTorrentRunsPastTheLimitWithoutHoldingAnyoneBack()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Downloading),
            Torrent("forced", TorrentStatus.Downloading),
        ];

        Assert.Same(QueuePlan.Nothing, Plan(torrents, limit: 2, forced: new HashSet<string> { "forced" }));
    }

    [Fact]
    public void Plan_LimitLoweredWithAForcedTorrent_HoldsBackOnlyTheOthers()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("forced", TorrentStatus.Downloading),
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Downloading),
            Torrent("c", TorrentStatus.Downloading),
        ];

        QueuePlan plan = Plan(torrents, limit: 2, forced: new HashSet<string> { "forced" });

        Assert.Equal(["c"], plan.Hold);
    }

    [Fact]
    public void Plan_WhenAnotherDownloadFinishes_ForcedTorrentStillTakesItsSlot()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Seeding, 1),
            Torrent("b", TorrentStatus.Downloading),
            Torrent("forced", TorrentStatus.Downloading),
            Torrent("queued", TorrentStatus.Queued),
        ];

        Assert.Same(QueuePlan.Nothing, Plan(torrents, limit: 2, forced: new HashSet<string> { "forced" }));
    }

    [Fact]
    public void Plan_ForcedTorrentTakesASlot()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("forced", TorrentStatus.Downloading),
            Torrent("queued", TorrentStatus.Queued),
        ];

        Assert.Same(QueuePlan.Nothing, Plan(torrents, limit: 2, forced: new HashSet<string> { "forced" }));
    }

    [Fact]
    public void Plan_OnlyForcedTorrentsOverTheLimit_HoldsNothingBack()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Downloading),
        ];

        Assert.Same(QueuePlan.Nothing, Plan(torrents, limit: 1, forced: new HashSet<string> { "a", "b" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Plan_WithoutALimit_StartsEverythingQueued(int limit)
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("b", TorrentStatus.Queued),
            Torrent("c", TorrentStatus.Queued),
        ];

        QueuePlan plan = Plan(torrents, limit);

        Assert.Equal(["b", "c"], plan.Start);
        Assert.Empty(plan.Hold);
    }

    [Fact]
    public void Plan_QueuedFinishedTorrentStartsWithoutASlot()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("a", TorrentStatus.Downloading),
            Torrent("done", TorrentStatus.Queued, 1),
            Torrent("b", TorrentStatus.Queued),
        ];

        Assert.Equal(["done"], Plan(torrents, limit: 1).Start);
    }

    [Fact]
    public void Plan_TorrentsMissingFromTheOrderComeLast()
    {
        TorrentSnapshot[] torrents =
        [
            Torrent("new", TorrentStatus.Queued),
            Torrent("old", TorrentStatus.Queued),
        ];

        Assert.Equal(["old"], DownloadQueue.Plan(torrents, ["old"], limit: 1, NoneForced).Start);
    }

    [Fact]
    public void WhenDone_WaitsForQueuedDownloads()
    {
        TorrentSnapshot[] torrents = [Torrent("a", TorrentStatus.Seeding, 1), Torrent("b", TorrentStatus.Queued)];

        Assert.True(WhenDoneWatch.IsDownloading(torrents[1]));
        Assert.False(WhenDoneWatch.IsDone(torrents, WhenDoneCondition.DownloadsFinished));
    }

    [Fact]
    public void Queued_ShowsInTheDownloadingView()
    {
        TorrentSnapshot queued = Torrent("a", TorrentStatus.Queued, 0.3);

        Assert.True(TorrentQuery.MatchesFilter(queued, TorrentFilter.Downloading));
        Assert.False(TorrentQuery.MatchesFilter(queued, TorrentFilter.Paused));
    }
}
