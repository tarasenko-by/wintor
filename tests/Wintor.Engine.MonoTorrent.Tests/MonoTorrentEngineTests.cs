using System.Net;
using System.Net.Sockets;
using Wintor.Core;

namespace Wintor.Engine.MonoTorrent.Tests;

/// <summary>
/// Exercises the adapter against the real MonoTorrent engine. No peers are needed:
/// a magnet link stays in the metadata phase, which is enough to test add/pause/remove/persistence.
/// </summary>
public sealed class MonoTorrentEngineTests : IDisposable
{
    // Big Buck Bunny (Blender Foundation, CC BY 3.0) — a legal, well-known test torrent.
    private const string Magnet = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny";
    private const string Id = "DD8255ECDC7CA55FB0BBF81323D87062DB1F6D1C";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wintor-tests", Guid.NewGuid().ToString("N"));

    private string DownloadDir => Path.Combine(_root, "downloads");

    [Fact]
    public async Task AddMagnet_AppearsInSnapshots()
    {
        await using var engine = await CreateEngineAsync();

        string id = await engine.AddMagnetAsync(Magnet, DownloadDir);

        Assert.Equal(Id, id, ignoreCase: true);
        TorrentSnapshot snapshot = Assert.Single(engine.GetSnapshots());
        Assert.Equal(id, snapshot.Id);
        Assert.Equal("Big Buck Bunny", snapshot.Name);
    }

    [Fact]
    public async Task AddingSameMagnetTwice_ReturnsSameTorrent()
    {
        await using var engine = await CreateEngineAsync();

        string first = await engine.AddMagnetAsync(Magnet, DownloadDir);
        string second = await engine.AddMagnetAsync("  " + Magnet + "  ", DownloadDir);

        Assert.Equal(first, second);
        Assert.Single(engine.GetSnapshots());
    }

    [Fact]
    public async Task Remove_DeletesTorrentFromList()
    {
        await using var engine = await CreateEngineAsync();
        string id = await engine.AddMagnetAsync(Magnet, DownloadDir);

        await engine.RemoveAsync(id, deleteFiles: true);

        Assert.Empty(engine.GetSnapshots());
    }

    [Fact]
    public async Task Restart_RestoresTorrentsAndKeepsPausedOnesPaused()
    {
        int port = GetFreePort();
        string id;
        await using (var engine = await CreateEngineAsync(port))
        {
            id = await engine.AddMagnetAsync(Magnet, DownloadDir);
            await engine.PauseAsync(id);
        }

        await using var restarted = await CreateEngineAsync(port);

        TorrentSnapshot snapshot = Assert.Single(restarted.GetSnapshots());
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(TorrentStatus.Stopped, snapshot.Status);
    }

    [Fact]
    public async Task ReadTorrentFile_ListsFilesWithoutAdding()
    {
        string torrentPath = await CreateTorrentFileAsync();
        await using var engine = await CreateEngineAsync();

        TorrentMetadata metadata = await engine.ReadTorrentFileAsync(torrentPath);

        Assert.Equal("payload", metadata.Name);
        Assert.Equal(["a.txt", "b.txt"], metadata.Files.Select(f => f.Path).Order());
        Assert.Equal(metadata.Files.Sum(f => f.Size), metadata.TotalSize);
        Assert.Empty(engine.GetSnapshots());
    }

    [Fact]
    public async Task AddTorrentFile_SkipsChosenFiles()
    {
        string torrentPath = await CreateTorrentFileAsync();
        await using var engine = await CreateEngineAsync();
        TorrentMetadata metadata = await engine.ReadTorrentFileAsync(torrentPath);
        int skipped = metadata.Files.Single(f => f.Path == "b.txt").Index;

        string id = await engine.AddTorrentFileAsync(torrentPath, DownloadDir, new HashSet<int> { skipped });

        TorrentDetails details = (await engine.GetDetailsAsync(id))!;
        Assert.Equal(FilePriority.Skip, details.Files.Single(f => f.Path == "b.txt").Priority);
        Assert.Equal(FilePriority.Normal, details.Files.Single(f => f.Path == "a.txt").Priority);
    }

    [Fact]
    public async Task SetFilePriority_ChangesOneFile()
    {
        string torrentPath = await CreateTorrentFileAsync();
        await using var engine = await CreateEngineAsync();
        string id = await engine.AddTorrentFileAsync(torrentPath, DownloadDir);
        int index = (await engine.GetDetailsAsync(id))!.Files.Single(f => f.Path == "a.txt").Index;

        await engine.SetFilePriorityAsync(id, index, FilePriority.High);

        TorrentDetails details = (await engine.GetDetailsAsync(id))!;
        Assert.Equal(FilePriority.High, details.Files.Single(f => f.Path == "a.txt").Priority);
        Assert.Equal(FilePriority.Normal, details.Files.Single(f => f.Path == "b.txt").Priority);
    }

    [Fact]
    public async Task Details_OfMagnet_HaveMagnetLinkAndNoFilesYet()
    {
        await using var engine = await CreateEngineAsync();
        string id = await engine.AddMagnetAsync(Magnet, DownloadDir);

        TorrentDetails details = (await engine.GetDetailsAsync(id))!;

        Assert.Equal(id, details.InfoHash);
        Assert.Contains("dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c", details.MagnetLink, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await engine.GetDetailsAsync("0000000000000000000000000000000000000000"));
    }

    [Fact]
    public async Task SpeedLimits_ApplyToOneTorrentAndSurviveARestart()
    {
        int port = GetFreePort();
        string torrentPath = await CreateTorrentFileAsync();
        string limited, other;
        await using (var engine = await CreateEngineAsync(port))
        {
            limited = await engine.AddMagnetAsync(Magnet, DownloadDir);
            other = await engine.AddTorrentFileAsync(torrentPath, DownloadDir);

            await engine.SetSpeedLimitsAsync(limited, 512 * 1024, 64 * 1024);

            TorrentSnapshot snapshot = engine.GetSnapshots().Single(s => s.Id == limited);
            Assert.Equal(512 * 1024, snapshot.DownloadLimit);
            Assert.Equal(64 * 1024, snapshot.UploadLimit);
            Assert.False(engine.GetSnapshots().Single(s => s.Id == other).HasSpeedLimit);
        }

        await using var restarted = await CreateEngineAsync(port);
        TorrentSnapshot restored = restarted.GetSnapshots().Single(s => s.Id == limited);
        Assert.Equal(512 * 1024, restored.DownloadLimit);
        Assert.Equal(64 * 1024, restored.UploadLimit);

        await restarted.SetSpeedLimitsAsync(limited, 0, 0);
        Assert.False(restarted.GetSnapshots().Single(s => s.Id == limited).HasSpeedLimit);
    }

    [Fact]
    public async Task DeletedFiles_AreReportedAfterRestart_AndResumeRechecks()
    {
        int port = GetFreePort();
        string torrentPath = await CreateTorrentFileAsync();
        string seedDir = Path.Combine(_root, "source");
        string id;
        await using (var engine = await CreateEngineAsync(port))
        {
            // Saved where the files already are, so the hash check finds them all.
            id = await engine.AddTorrentFileAsync(torrentPath, seedDir);
            await WaitUntilAsync(() => engine.GetSnapshots().Single().IsComplete);
        }

        Directory.Delete(Path.Combine(seedDir, "payload"), recursive: true);
        await using var restarted = await CreateEngineAsync(port);

        TorrentSnapshot missing = restarted.GetSnapshots().Single();
        Assert.Equal(TorrentStatus.Error, missing.Status);
        Assert.Equal(TorrentProblem.FilesMissing, missing.Problem);

        await restarted.StartAsync(id);
        await WaitUntilAsync(() => restarted.GetSnapshots().Single().Status != TorrentStatus.Checking);

        TorrentSnapshot resumed = restarted.GetSnapshots().Single();
        Assert.Equal(TorrentProblem.None, resumed.Problem);
        Assert.Equal(0, resumed.Progress);
    }

    [Fact]
    public async Task DownloadOrder_KeepsTorrentRunning_AndSurvivesARestart()
    {
        int port = GetFreePort();
        string torrentPath = await CreateTorrentFileAsync();
        string id;
        await using (var engine = await CreateEngineAsync(port))
        {
            id = await engine.AddTorrentFileAsync(torrentPath, DownloadDir);

            await engine.SetDownloadOrderAsync(id, new DownloadOrder(Sequential: true, FirstAndLastPiecesFirst: true));

            TorrentSnapshot snapshot = engine.GetSnapshots().Single();
            Assert.Equal(new DownloadOrder(true, true), snapshot.Order);
            Assert.NotEqual(TorrentStatus.Stopped, snapshot.Status);
        }

        await using var restarted = await CreateEngineAsync(port);
        Assert.Equal(new DownloadOrder(true, true), restarted.GetSnapshots().Single().Order);

        await restarted.SetDownloadOrderAsync(id, DownloadOrder.Default);
        Assert.True(restarted.GetSnapshots().Single().Order.IsDefault);
    }

    [Fact]
    public async Task DownloadOrder_OfPausedTorrent_LeavesItPaused()
    {
        string torrentPath = await CreateTorrentFileAsync();
        await using var engine = await CreateEngineAsync();
        string id = await engine.AddTorrentFileAsync(torrentPath, DownloadDir);
        await engine.PauseAsync(id);

        await engine.SetDownloadOrderAsync(id, new DownloadOrder(Sequential: true, FirstAndLastPiecesFirst: false));

        Assert.Contains(engine.GetSnapshots().Single().Status, new[] { TorrentStatus.Paused, TorrentStatus.Stopped });
    }

    [Fact]
    public async Task StreamFile_ServesTheFileWithRanges_AndOneFileAtATime()
    {
        string torrentPath = await CreateTorrentFileAsync();
        await using var engine = await CreateEngineAsync();
        // Saved where the files already are: complete, so the stream does not wait for peers.
        string id = await engine.AddTorrentFileAsync(torrentPath, Path.Combine(_root, "source"));
        await WaitUntilAsync(() => engine.GetSnapshots().Single().IsComplete);
        TorrentDetails details = (await engine.GetDetailsAsync(id))!;
        int a = details.Files.Single(f => f.Path == "a.txt").Index;
        int b = details.Files.Single(f => f.Path == "b.txt").Index;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        Uri link = await engine.StreamFileAsync(id, b);

        Assert.True(link.IsLoopback);
        Assert.Equal(70_000, (await http.GetByteArrayAsync(link)).Length);

        using var request = new HttpRequestMessage(HttpMethod.Get, link);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(100, 199);
        using HttpResponseMessage partial = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal("bytes 100-199/70000", partial.Content.Headers.ContentRange?.ToString());
        Assert.Equal(new string('b', 100), await partial.Content.ReadAsStringAsync());

        Uri other = await engine.StreamFileAsync(id, a);
        Assert.Equal(new string('a', 40_000), await http.GetStringAsync(other));
        using HttpResponseMessage ended = await http.GetAsync(link);
        Assert.Equal(HttpStatusCode.NotFound, ended.StatusCode);
    }

    [Fact]
    public async Task Snapshot_PointsPlayAtTheLargestMediaFile()
    {
        string source = Path.Combine(_root, "source", "film");
        Directory.CreateDirectory(source);
        await File.WriteAllBytesAsync(Path.Combine(source, "sample.mkv"), new byte[20_000]);
        await File.WriteAllBytesAsync(Path.Combine(source, "film.mkv"), new byte[90_000]);
        await File.WriteAllBytesAsync(Path.Combine(source, "poster.jpg"), new byte[200_000]);
        var torrent = await new global::MonoTorrent.TorrentCreator().CreateAsync(new global::MonoTorrent.TorrentFileSource(source));
        string path = Path.Combine(_root, "film.torrent");
        await File.WriteAllBytesAsync(path, torrent.Encode());
        await using var engine = await CreateEngineAsync();

        string id = await engine.AddTorrentFileAsync(path, DownloadDir);

        int? index = engine.GetSnapshots().Single().MediaFileIndex;
        Assert.Equal("film.mkv", (await engine.GetDetailsAsync(id))!.Files.Single(f => f.Index == index).Path);
    }

    [Fact]
    public async Task ApplyOptions_WhileRunning_KeepsTorrents()
    {
        await using var engine = await CreateEngineAsync();
        await engine.AddMagnetAsync(Magnet, DownloadDir);

        await engine.ApplyOptionsAsync(new EngineOptions
        {
            DataDirectory = Path.Combine(_root, "data"),
            ListenPort = GetFreePort(),
            MaxDownloadRate = 100 * 1024,
            MaxUploadRate = 50 * 1024,
        });

        Assert.Single(engine.GetSnapshots());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; the OS cleans the temp folder eventually.
        }
    }

    private async Task<MonoTorrentEngine> CreateEngineAsync(int? port = null)
    {
        var engine = new MonoTorrentEngine(new EngineOptions
        {
            DataDirectory = Path.Combine(_root, "data"),
            ListenPort = port ?? GetFreePort(),
        });
        await engine.InitializeAsync();
        return engine;
    }

    /// <summary>A two-file torrent made from our own temporary files, so no network or third-party content is needed.</summary>
    private async Task<string> CreateTorrentFileAsync()
    {
        string source = Path.Combine(_root, "source", "payload");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), new string('a', 40_000));
        await File.WriteAllTextAsync(Path.Combine(source, "b.txt"), new string('b', 70_000));

        var creator = new global::MonoTorrent.TorrentCreator();
        var torrent = await creator.CreateAsync(new global::MonoTorrent.TorrentFileSource(source));
        string path = Path.Combine(_root, "payload.torrent");
        await File.WriteAllBytesAsync(path, torrent.Encode());
        return path;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the engine.");
            await Task.Delay(50);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
