using Wintor.Core;

namespace Wintor.Core.Tests;

public class TorrentQueryTests
{
    private static TorrentSnapshot Torrent(TorrentStatus status, double progress, string name = "Big Buck Bunny") =>
        new("ID", name, status, progress, 1000, 0, 0, 0, 0, @"C:\Downloads", null);

    private static TorrentSnapshot Row(string id, long size = 0, long down = 0, double progress = 0, TorrentStatus status = TorrentStatus.Downloading) =>
        new(id, "Torrent " + id, status, progress, size, down, 0, 0, 0, @"C:\Downloads", null);

    [Fact]
    public void Sort_ByQueue_FollowsTheUsersOrderAndPutsNewOnesLast()
    {
        var rows = new[] { Row("a"), Row("b"), Row("c") };

        var sorted = TorrentQuery.Sort(rows, TorrentSort.Queue, descending: false, queue: ["c", "a"]);

        Assert.Equal(["c", "a", "b"], sorted.Select(t => t.Id));
    }

    [Fact]
    public void Sort_BySize_BothDirections()
    {
        var rows = new[] { Row("a", size: 30), Row("b", size: 10), Row("c", size: 20) };

        Assert.Equal(["b", "c", "a"], TorrentQuery.Sort(rows, TorrentSort.Size, false, []).Select(t => t.Id));
        Assert.Equal(["a", "c", "b"], TorrentQuery.Sort(rows, TorrentSort.Size, true, []).Select(t => t.Id));
    }

    [Fact]
    public void Sort_TiesKeepTheQueueOrder()
    {
        var rows = new[] { Row("a", size: 10), Row("b", size: 10), Row("c", size: 5) };

        var sorted = TorrentQuery.Sort(rows, TorrentSort.Size, descending: true, queue: ["b", "a", "c"]);

        Assert.Equal(["b", "a", "c"], sorted.Select(t => t.Id));
    }

    [Fact]
    public void Sort_ByTimeLeft_PutsUnknownLastEitherWay()
    {
        // size 1000, progress 0.5 → 500 bytes left; at 100 B/s that is 5 s, at 10 B/s 50 s.
        var fast = Row("fast", size: 1000, down: 100, progress: 0.5);
        var slow = Row("slow", size: 1000, down: 10, progress: 0.5);
        var paused = Row("paused", size: 1000, progress: 0.5, status: TorrentStatus.Paused);

        Assert.Equal(["fast", "slow", "paused"], TorrentQuery.Sort([paused, slow, fast], TorrentSort.TimeLeft, false, []).Select(t => t.Id));
        Assert.Equal(["slow", "fast", "paused"], TorrentQuery.Sort([paused, slow, fast], TorrentSort.TimeLeft, true, []).Select(t => t.Id));
    }

    [Fact]
    public void Sort_ByStatus_ActiveFirst()
    {
        var rows = new[]
        {
            Row("stopped", status: TorrentStatus.Stopped),
            Row("seeding", status: TorrentStatus.Seeding),
            Row("downloading", status: TorrentStatus.Downloading),
        };

        Assert.Equal(["downloading", "seeding", "stopped"], TorrentQuery.Sort(rows, TorrentSort.Status, false, []).Select(t => t.Id));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("bunny", true)]
    [InlineData("BUCK big", true)]
    [InlineData("bunny 2", false)]
    [InlineData("rabbit", false)]
    public void Search_RequiresEveryWordInAnyOrder(string search, bool expected)
    {
        Assert.Equal(expected, TorrentQuery.MatchesSearch("Big Buck Bunny", search));
    }

    [Fact]
    public void Search_IgnoresAccents()
    {
        Assert.True(TorrentQuery.MatchesSearch("Amélie (2001)", "amelie"));
    }

    [Theory]
    [InlineData(TorrentStatus.Downloading, 0.5, TorrentFilter.Downloading, true)]
    [InlineData(TorrentStatus.FetchingMetadata, 0, TorrentFilter.Downloading, true)]
    [InlineData(TorrentStatus.Paused, 0.5, TorrentFilter.Downloading, false)]
    [InlineData(TorrentStatus.Seeding, 1, TorrentFilter.Completed, true)]
    [InlineData(TorrentStatus.Stopped, 1, TorrentFilter.Completed, true)]
    [InlineData(TorrentStatus.Downloading, 0.9, TorrentFilter.Completed, false)]
    [InlineData(TorrentStatus.Stopped, 0.3, TorrentFilter.Paused, true)]
    [InlineData(TorrentStatus.Seeding, 1, TorrentFilter.Paused, false)]
    [InlineData(TorrentStatus.Error, 0.1, TorrentFilter.All, true)]
    public void Filter_PutsTorrentsInTheRightGroup(TorrentStatus status, double progress, TorrentFilter filter, bool expected)
    {
        Assert.Equal(expected, TorrentQuery.MatchesFilter(Torrent(status, progress), filter));
    }
}
