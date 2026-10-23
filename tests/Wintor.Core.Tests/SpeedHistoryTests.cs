using Wintor.Core;

namespace Wintor.Core.Tests;

public class SpeedHistoryTests
{
    private static TorrentSnapshot Snapshot(string id, long down, long up) =>
        new(id, id, TorrentStatus.Downloading, 0.5, 100, down, up, 0, 0, "C:\\", null);

    [Fact]
    public void RecordsEveryTorrent_OldestFirst()
    {
        var history = new SpeedHistory();

        history.Record([Snapshot("a", 10, 1), Snapshot("b", 20, 2)]);
        history.Record([Snapshot("a", 11, 3), Snapshot("b", 21, 4)]);

        Assert.Equal([10, 11], history.Downloads("a"));
        Assert.Equal([1, 3], history.Uploads("a"));
        Assert.Equal([20, 21], history.Downloads("b"));
    }

    [Fact]
    public void KeepsOnlyTheLastSamples()
    {
        var history = new SpeedHistory(length: 3);

        for (int i = 1; i <= 5; i++)
        {
            history.Record([Snapshot("a", i, 0)]);
        }

        Assert.Equal([3, 4, 5], history.Downloads("a"));
    }

    [Fact]
    public void ForgetsRemovedTorrents()
    {
        var history = new SpeedHistory();
        history.Record([Snapshot("a", 10, 0), Snapshot("b", 20, 0)]);

        history.Record([Snapshot("a", 11, 0)]);

        Assert.Empty(history.Downloads("b"));
        Assert.Empty(history.Uploads("unknown"));
    }
}
