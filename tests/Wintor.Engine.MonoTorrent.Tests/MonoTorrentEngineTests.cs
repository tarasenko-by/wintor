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

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
