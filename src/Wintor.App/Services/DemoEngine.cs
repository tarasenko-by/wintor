using System.Security.Cryptography;
using System.Text;
using Wintor.Core;

namespace Wintor.App.Services;

/// <summary>
/// The engine behind "wintor --demo": a fixed set of legal torrents (Linux images, Blender Studio's open films, open
/// data) in every state, with speeds that move like real ones. Nothing touches the network or the disk, so screenshots
/// for the README and the Store never show anyone's real downloads.
/// </summary>
public sealed class DemoEngine : ITorrentEngine
{
    private const long MiB = 1024 * 1024;
    private const long GiB = 1024 * MiB;
    private const string Folder = @"C:\Downloads";

    private sealed class Item(string name, long size, double progress, TorrentStatus status, long down, long up, int seeds, int leechers)
    {
        public string Id { get; } = IdOf(name);
        public string Name { get; } = name;
        public long Size { get; } = size;
        public double Progress { get; set; } = progress;
        public TorrentStatus Status { get; set; } = status;
        public long BaseDown { get; } = down;
        public long BaseUp { get; } = up;
        public int Seeds { get; } = seeds;
        public int Leechers { get; } = leechers;
        public double Phase { get; } = name.Length % 7;
        public (string Path, double Share)[] Files { get; init; } = [(name, 1)];
        public string[] Trackers { get; init; } = ["udp://tracker.opentrackr.org:1337/announce"];
        public string? Label { get; init; }
        public TimeSpan Active { get; init; } = TimeSpan.FromHours(2);
    }

    private readonly List<Item> _items =
    [
        new("ubuntu-24.04.1-desktop-amd64.iso", 5_920 * MiB, 0.62, TorrentStatus.Downloading, 11_200_000, 640_000, 148, 23)
        {
            Label = "Linux",
            Trackers = ["https://torrent.ubuntu.com/announce", "https://ipv6.torrent.ubuntu.com/announce"],
            Active = TimeSpan.FromMinutes(14),
        },
        new("Sintel (2010) 4K", 6_140 * MiB, 1, TorrentStatus.Seeding, 0, 1_240_000, 0, 31)
        {
            Label = "Movies",
            Files = [("sintel-2010-4k.mkv", 0.97), ("subtitles/sintel_en.srt", 0.01), ("subtitles/sintel_de.srt", 0.01), ("poster.jpg", 0.01)],
            Active = TimeSpan.FromHours(26),
        },
        new("debian-12.7.0-amd64-DVD-1.iso", 3_780 * MiB, 0.18, TorrentStatus.Downloading, 4_300_000, 210_000, 64, 12)
        {
            Label = "Linux",
            Trackers = ["http://bttracker.debian.org:6969/announce"],
            Active = TimeSpan.FromMinutes(6),
        },
        new("Big Buck Bunny (2008) 1080p", 885 * MiB, 1, TorrentStatus.Stopped, 0, 0, 0, 0)
        {
            Label = "Movies",
            Files = [("bbb_sunflower_1080p_60fps_normal.mp4", 0.99), ("poster.jpg", 0.01)],
            Active = TimeSpan.FromHours(3),
        },
        new("enwiki-20241001-pages-articles-multistream.xml.bz2", 22_400 * MiB, 0.07, TorrentStatus.Downloading, 2_100_000, 90_000, 37, 9)
        {
            Label = "Open data",
            Active = TimeSpan.FromMinutes(21),
        },
        new("Fedora-Workstation-Live-x86_64-41", 2_250 * MiB, 0.41, TorrentStatus.Paused, 0, 0, 0, 0)
        {
            Label = "Linux",
            Files = [("Fedora-Workstation-Live-x86_64-41-1.4.iso", 0.999), ("Fedora-Workstation-41-1.4-x86_64-CHECKSUM", 0.001)],
            Trackers = ["http://torrent.fedoraproject.org:6969/announce"],
        },
        new("archlinux-2024.10.01-x86_64.iso", 1_150 * MiB, 1, TorrentStatus.Seeding, 0, 340_000, 0, 8)
        {
            Label = "Linux",
            Active = TimeSpan.FromHours(9),
        },
        new("Tears of Steel (2012) 4K", 6_310 * MiB, 0.73, TorrentStatus.Checking, 0, 0, 0, 0)
        {
            Label = "Movies",
            Files = [("tears_of_steel_4k.mov", 0.98), ("subtitles/tos_en.srt", 0.01), ("subtitles/tos_fr.srt", 0.01)],
        },
        new("LibreOffice_24.8.2_Win_x86-64.msi", 345 * MiB, 1, TorrentStatus.Seeding, 0, 95_000, 0, 3)
        {
            Active = TimeSpan.FromHours(5),
        },
        new("Spring (2019) — Blender Studio", 0, 0, TorrentStatus.FetchingMetadata, 0, 0, 2, 0) { Label = "Movies" },
    ];

    private readonly DateTime _start = DateTime.UtcNow;
    private double _lastSeconds;

    /// <summary>The labels, their colors and which torrent has which, for the demo's own settings.</summary>
    public static IReadOnlyList<(string Name, string Color)> Labels { get; } =
        [("Linux", "Blue"), ("Movies", "Purple"), ("Open data", "Green")];

    public IEnumerable<(string Id, string Label)> TorrentLabels =>
        _items.Where(i => i.Label is not null).Select(i => (i.Id, i.Label!));

    public IEnumerable<string> Order => _items.Select(i => i.Id);

    /// <summary>Five minutes of speeds already behind every torrent, so the speed graph is full from the start.</summary>
    public void Prefill(SpeedHistory history)
    {
        for (int t = -history.Length; t < 0; t++)
        {
            history.Record(_items.Select(i => ToSnapshot(i, t)).ToList());
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ApplyOptionsAsync(EngineOptions options) => Task.CompletedTask;

    public Task<string> AddMagnetAsync(string magnetLink, string saveDirectory) =>
        throw new InvalidOperationException("The demo does not add torrents.");

    public Task<TorrentMetadata> ReadTorrentFileAsync(string torrentFilePath) =>
        throw new InvalidOperationException("The demo does not add torrents.");

    public Task<string> AddTorrentFileAsync(string torrentFilePath, string saveDirectory, IReadOnlySet<int>? skippedFiles = null) =>
        throw new InvalidOperationException("The demo does not add torrents.");

    public Task StartAsync(string id)
    {
        Item item = Get(id);
        item.Status = item.Progress >= 1 ? TorrentStatus.Seeding : TorrentStatus.Downloading;
        return Task.CompletedTask;
    }

    public Task PauseAsync(string id)
    {
        Get(id).Status = TorrentStatus.Paused;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string id, bool deleteFiles)
    {
        _items.Remove(Get(id));
        return Task.CompletedTask;
    }

    public Task SetFilePriorityAsync(string id, int fileIndex, FilePriority priority) => Task.CompletedTask;

    public Task SetSpeedLimitsAsync(string id, int maxDownloadRate, int maxUploadRate) => Task.CompletedTask;

    public IReadOnlyList<TorrentSnapshot> GetSnapshots()
    {
        double now = (DateTime.UtcNow - _start).TotalSeconds;
        double elapsed = Math.Clamp(now - _lastSeconds, 0, 5);
        _lastSeconds = now;
        foreach (Item item in _items.Where(i => i.Status == TorrentStatus.Downloading))
        {
            // Downloads creep forward but never finish: a "download finished" notification would spoil a screenshot.
            item.Progress = Math.Min(0.985, item.Progress + Rate(item.BaseDown, item, now) * elapsed / item.Size);
        }

        return _items.Select(i => ToSnapshot(i, now)).ToList();
    }

    public Task<TorrentDetails?> GetDetailsAsync(string id)
    {
        Item? item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return Task.FromResult<TorrentDetails?>(null);
        }

        double now = (DateTime.UtcNow - _start).TotalSeconds;
        TorrentSnapshot summary = ToSnapshot(item, now);
        var random = new Random(item.Id.GetHashCode());
        bool running = item.Status is TorrentStatus.Downloading or TorrentStatus.Seeding;
        long pieceLength = item.Size > 4 * GiB ? 8 * MiB : 4 * MiB;
        int pieces = item.Size > 0 ? (int)Math.Max(1, item.Size / pieceLength) : 0;

        var files = item.Size > 0
            ? item.Files.Select((f, i) => new TorrentFileEntry(i, f.Path, (long)(item.Size * f.Share), item.Progress, FilePriority.Normal)).ToList()
            : [];
        var peers = running
            ? Enumerable.Range(0, 7 + random.Next(6)).Select(_ => Peer(random, item)).OrderByDescending(p => p.DownloadRate + p.UploadRate).ToList()
            : [];
        var trackers = item.Trackers.Select(url => new TrackerEntry(url, running ? TrackerStatus.Working : TrackerStatus.Unknown, null)).ToList();

        return Task.FromResult<TorrentDetails?>(new TorrentDetails(
            summary,
            item.Id,
            $"magnet:?xt=urn:btih:{item.Id.ToLowerInvariant()}&dn={Uri.EscapeDataString(item.Name)}",
            null,
            new DateTime(2024, 10, 1),
            false,
            files,
            peers,
            trackers)
        {
            Transfer = new TransferInfo(
                item.Active,
                (long)(item.Size * item.Progress),
                (long)(item.Size * item.Progress * (item.Progress >= 1 ? 1.8 : 0.12)),
                (long)(item.Size * (1 - item.Progress)),
                item.Size / 2000,
                pieces,
                pieceLength,
                running ? item.Seeds : 0,
                running ? item.Leechers : 0),
            Downloaded = PieceMap.Fold(pieces, p => p < pieces * item.Progress ? random.NextDouble() > 0.04 : random.NextDouble() < 0.03),
            Available = PieceMap.Fold(pieces, _ => running && random.NextDouble() > 0.02),
            Availability = running ? 6 + random.NextDouble() * 20 : 0,
        });
    }

    public Task SaveStateAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Item Get(string id) => _items.First(i => i.Id == id);

    private static TorrentSnapshot ToSnapshot(Item item, double seconds)
    {
        bool downloading = item.Status == TorrentStatus.Downloading;
        bool transferring = downloading || item.Status == TorrentStatus.Seeding;
        return new TorrentSnapshot(
            item.Id,
            item.Name,
            item.Status,
            item.Progress,
            item.Size,
            downloading ? Rate(item.BaseDown, item, seconds) : 0,
            transferring ? Rate(item.BaseUp, item, seconds + 40) : 0,
            transferring ? item.Seeds : 0,
            transferring ? item.Leechers : 0,
            Folder,
            null)
        {
            ContentPath = Path.Combine(Folder, item.Name),
        };
    }

    /// <summary>A rate that drifts around its base like a real swarm: two slow waves, never below a third.</summary>
    private static long Rate(long baseRate, Item item, double seconds) =>
        (long)(baseRate * Math.Max(0.35, 0.8 + 0.15 * Math.Sin(seconds / 11 + item.Phase) + 0.08 * Math.Sin(seconds / 3.7 + 2 * item.Phase)));

    private static PeerEntry Peer(Random random, Item item)
    {
        // Documentation address ranges (RFC 5737): they belong to nobody.
        string[] networks = ["192.0.2", "198.51.100", "203.0.113"];
        string[] clients = ["qBittorrent 5.0.1", "Transmission 4.0.6", "Deluge 2.1.1", "libtorrent 2.0.10", "BiglyBT 3.7", "Wintor 0.2"];
        bool seeder = random.NextDouble() < 0.6;
        return new PeerEntry(
            $"{networks[random.Next(networks.Length)]}.{random.Next(2, 254)}:{random.Next(10000, 65000)}",
            clients[random.Next(clients.Length)],
            seeder ? 1 : random.NextDouble(),
            item.Status == TorrentStatus.Downloading && seeder ? item.BaseDown / (4 + random.Next(8)) : 0,
            random.NextDouble() < 0.4 ? item.BaseUp / (2 + random.Next(6)) : 0,
            seeder,
            random.NextDouble() < 0.3,
            random.NextDouble() < 0.7);
    }

    private static string IdOf(string name) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(name)));
}
