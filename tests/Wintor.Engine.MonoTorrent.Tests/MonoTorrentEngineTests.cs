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

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
