using Wintor.Core;

namespace Wintor.Core.Tests;

public class WhenDoneTests
{
    private static TorrentSnapshot Torrent(string id, TorrentStatus status, double progress = 0) =>
        new(id, "Torrent " + id, status, progress, 1000, 0, 0, 0, 0, @"C:\Downloads", null);

    private static readonly WhenDoneRule ShutDownAfterDownloads = new(WhenDoneAction.ShutDown, WhenDoneCondition.DownloadsFinished);
    private static readonly WhenDoneRule SleepAfterAll = new(WhenDoneAction.Sleep, WhenDoneCondition.AllFinished);

    [Theory]
    [InlineData(TorrentStatus.Downloading, 0.5, true)]
    [InlineData(TorrentStatus.FetchingMetadata, 0, true)]
    [InlineData(TorrentStatus.Starting, 0.2, true)]
    [InlineData(TorrentStatus.Checking, 0.2, true)]
    [InlineData(TorrentStatus.Starting, 1, false)]
    [InlineData(TorrentStatus.Checking, 1, false)]
    [InlineData(TorrentStatus.Seeding, 1, false)]
    [InlineData(TorrentStatus.Paused, 0.5, false)]
    [InlineData(TorrentStatus.Stopped, 0.5, false)]
    [InlineData(TorrentStatus.Error, 0.5, false)]
    public void IsDownloading_CountsOnlyTorrentsThatWillStillDownload(TorrentStatus status, double progress, bool expected) =>
        Assert.Equal(expected, WhenDoneWatch.IsDownloading(Torrent("a", status, progress)));

    [Fact]
    public void IsDone_DownloadsFinished_IgnoresSeeding()
    {
        TorrentSnapshot[] torrents = [Torrent("a", TorrentStatus.Seeding, 1), Torrent("b", TorrentStatus.Paused, 0.3)];

        Assert.True(WhenDoneWatch.IsDone(torrents, WhenDoneCondition.DownloadsFinished));
        Assert.False(WhenDoneWatch.IsDone(torrents, WhenDoneCondition.AllFinished));
    }

    [Fact]
    public void IsDone_WithNoTorrents_IsTrue()
    {
        Assert.True(WhenDoneWatch.IsDone([], WhenDoneCondition.DownloadsFinished));
        Assert.True(WhenDoneWatch.IsDone([], WhenDoneCondition.AllFinished));
    }

    [Fact]
    public void Check_FiresOnceWhenTheLastDownloadFinishes()
    {
        var watch = new WhenDoneWatch();
        watch.Set(ShutDownAfterDownloads);

        Assert.False(watch.Check([Torrent("a", TorrentStatus.Downloading, 0.9), Torrent("b", TorrentStatus.Seeding, 1)]));
        Assert.True(watch.Check([Torrent("a", TorrentStatus.Seeding, 1), Torrent("b", TorrentStatus.Seeding, 1)]));
        Assert.True(watch.Rule.IsOff);
        Assert.False(watch.Check([Torrent("a", TorrentStatus.Seeding, 1)]));
    }

    [Fact]
    public void Check_WithNothingToWaitFor_WaitsForTheNextTorrent()
    {
        var watch = new WhenDoneWatch();
        watch.Set(ShutDownAfterDownloads);

        Assert.False(watch.Check([]));
        Assert.False(watch.Check([Torrent("a", TorrentStatus.Seeding, 1)]));
        Assert.False(watch.Check([Torrent("b", TorrentStatus.FetchingMetadata)]));
        Assert.True(watch.Check([Torrent("b", TorrentStatus.Seeding, 1)]));
    }

    [Fact]
    public void Check_AllFinished_WaitsForSeedingToStop()
    {
        var watch = new WhenDoneWatch();
        watch.Set(SleepAfterAll);

        Assert.False(watch.Check([Torrent("a", TorrentStatus.Downloading, 0.5)]));
        Assert.False(watch.Check([Torrent("a", TorrentStatus.Seeding, 1)]));
        Assert.True(watch.Check([Torrent("a", TorrentStatus.Paused, 1)]));
    }

    [Fact]
    public void Check_PausingTheLastDownload_CountsAsDone()
    {
        var watch = new WhenDoneWatch();
        watch.Set(ShutDownAfterDownloads);

        Assert.False(watch.Check([Torrent("a", TorrentStatus.Downloading, 0.5)]));
        Assert.True(watch.Check([Torrent("a", TorrentStatus.Paused, 0.5)]));
    }

    [Fact]
    public void Set_Again_ForgetsWhatWasSeen()
    {
        var watch = new WhenDoneWatch();
        watch.Set(ShutDownAfterDownloads);
        watch.Check([Torrent("a", TorrentStatus.Downloading, 0.5)]);

        watch.Set(SleepAfterAll);

        Assert.False(watch.Check([]));
    }

    [Fact]
    public void Check_WhenOff_NeverFires()
    {
        var watch = new WhenDoneWatch();

        Assert.False(watch.Check([Torrent("a", TorrentStatus.Downloading, 0.5)]));
        Assert.False(watch.Check([]));
    }
}
