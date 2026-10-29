using System.Net;
using System.Net.Sockets;
using System.Text;
using Wintor.Core;

namespace Wintor.Engine.MonoTorrent;

/// <summary>
/// A tiny HTTP server on 127.0.0.1 that hands files to media players while they download.
/// </summary>
/// <remarks>
/// MonoTorrent has one (<c>CreateHttpStreamAsync</c>), but it uses HttpListener on a fixed port, which needs a URL
/// reservation for a normal user and serves one request at a time, while players open a second connection to read the
/// end of the file. This one listens on a free port, only on the loopback address, and answers only links it made:
/// each has a random token, so another program or a web page cannot guess one.
/// </remarks>
internal sealed class StreamServer : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int ChunkSize = 64 * 1024;

    private sealed class Entry(string torrentId, Stream stream, string contentType)
    {
        public string TorrentId { get; } = torrentId;
        public Stream Stream { get; } = stream;
        public string ContentType { get; } = contentType;

        // The torrent's stream is one position for every connection; reads take turns.
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public CancellationTokenSource Closed { get; } = new();
        public int Connections;
        public long LastActivity = Environment.TickCount64;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = [];
    private TcpListener? _listener;
    private CancellationTokenSource? _stopping;

    /// <summary>Serves <paramref name="stream"/> until <see cref="Remove"/>; returns the link for the player.</summary>
    public Uri Add(string torrentId, Stream stream, string fileName)
    {
        int port = EnsureListening();
        string token = Guid.NewGuid().ToString("N");
        lock (_gate)
        {
            _entries[token] = new Entry(torrentId, stream, MediaFiles.ContentType(fileName));
        }

        // The file name at the end is only for the player's title bar and its guess of the format.
        return new Uri($"http://127.0.0.1:{port}/{token}/{Uri.EscapeDataString(Path.GetFileName(fileName))}");
    }

    public bool IsServing(string torrentId)
    {
        lock (_gate)
        {
            return _entries.Values.Any(e => e.TorrentId == torrentId);
        }
    }

    /// <summary>Torrents whose player has had no connection open for <paramref name="idle"/>: it was closed.</summary>
    public IReadOnlyList<string> IdleTorrents(TimeSpan idle)
    {
        long now = Environment.TickCount64;
        lock (_gate)
        {
            return _entries.Values
                .Where(e => Volatile.Read(ref e.Connections) == 0 && now - Interlocked.Read(ref e.LastActivity) > idle.TotalMilliseconds)
                .Select(e => e.TorrentId)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>Ends the links of a torrent; open connections are dropped and the stream is disposed.</summary>
    public void Remove(string torrentId)
    {
        List<Entry> removed;
        lock (_gate)
        {
            removed = _entries.Where(p => p.Value.TorrentId == torrentId).Select(p => p.Value).ToList();
            foreach (string token in _entries.Where(p => p.Value.TorrentId == torrentId).Select(p => p.Key).ToList())
            {
                _entries.Remove(token);
            }
        }

        foreach (Entry entry in removed)
        {
            entry.Closed.Cancel();
            entry.Stream.Dispose();
        }
    }

    public void Dispose()
    {
        List<string> torrents;
        lock (_gate)
        {
            torrents = _entries.Values.Select(e => e.TorrentId).Distinct().ToList();
        }

        foreach (string id in torrents)
        {
            Remove(id);
        }

        _stopping?.Cancel();
        _listener?.Stop();
        _listener = null;
    }

    private int EnsureListening()
    {
        lock (_gate)
        {
            if (_listener is null)
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _stopping = new CancellationTokenSource();
                _ = AcceptAsync(_listener, _stopping.Token);
            }

            return ((IPEndPoint)_listener.LocalEndpoint).Port;
        }
    }

    private async Task AcceptAsync(TcpListener listener, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopping);
            }
            catch (Exception)
            {
                return; // stopped
            }

            _ = ServeAsync(client, stopping);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken stopping)
    {
        using (client)
        {
            try
            {
                NetworkStream network = client.GetStream();
                (string Method, string Path, string? Range)? request = await ReadRequestAsync(network, stopping);
                if (request is not { } r)
                {
                    return;
                }

                Entry? entry = Find(r.Path);
                if (entry is null || r.Method is not ("GET" or "HEAD"))
                {
                    await WriteHeadAsync(network, entry is null ? "404 Not Found" : "405 Method Not Allowed", [], stopping);
                    return;
                }

                Interlocked.Increment(ref entry.Connections);
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, entry.Closed.Token);
                    await ServeFileAsync(network, entry, r.Method == "HEAD", r.Range, linked.Token);
                }
                finally
                {
                    Interlocked.Exchange(ref entry.LastActivity, Environment.TickCount64);
                    Interlocked.Decrement(ref entry.Connections);
                }
            }
            catch (Exception)
            {
                // The player closed the connection (it does on every seek), or the link was ended.
            }
        }
    }

    private Entry? Find(string path)
    {
        string token = path.TrimStart('/').Split('/')[0];
        lock (_gate)
        {
            return _entries.GetValueOrDefault(token);
        }
    }

    private static async Task ServeFileAsync(NetworkStream network, Entry entry, bool headOnly, string? rangeHeader, CancellationToken cancel)
    {
        long length = entry.Stream.Length;
        HttpRange? range = HttpRange.Parse(rangeHeader, length);
        if (range is not { } part)
        {
            await WriteHeadAsync(network, "416 Range Not Satisfiable", [$"Content-Range: bytes */{length}"], cancel);
            return;
        }

        bool partial = rangeHeader is not null;
        var headers = new List<string>
        {
            $"Content-Type: {entry.ContentType}",
            $"Content-Length: {part.Length}",
            "Accept-Ranges: bytes",
        };
        if (partial)
        {
            headers.Add($"Content-Range: bytes {part.Start}-{part.End}/{length}");
        }

        await WriteHeadAsync(network, partial ? "206 Partial Content" : "200 OK", headers, cancel);
        if (headOnly)
        {
            return;
        }

        var buffer = new byte[ChunkSize];
        long position = part.Start;
        while (position <= part.End)
        {
            int wanted = (int)Math.Min(buffer.Length, part.End - position + 1);
            int read;
            await entry.Lock.WaitAsync(cancel);
            try
            {
                // Seeking tells MonoTorrent which pieces are needed now; reading waits until they are downloaded.
                if (entry.Stream.Position != position)
                {
                    entry.Stream.Seek(position, SeekOrigin.Begin);
                }

                read = await entry.Stream.ReadAsync(buffer.AsMemory(0, wanted), cancel);
            }
            finally
            {
                entry.Lock.Release();
            }

            if (read <= 0)
            {
                return;
            }

            await network.WriteAsync(buffer.AsMemory(0, read), cancel);
            position += read;
            Interlocked.Exchange(ref entry.LastActivity, Environment.TickCount64);
        }
    }

    private static async Task<(string Method, string Path, string? Range)?> ReadRequestAsync(NetworkStream network, CancellationToken cancel)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        // Byte by byte up to the blank line: requests are a few hundred bytes and the body (there is none) must not be eaten.
        while (bytes.Count < MaxHeaderBytes)
        {
            if (await network.ReadAsync(one, cancel) == 0)
            {
                return null;
            }

            bytes.Add(one[0]);
            int n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n')
            {
                break;
            }
        }

        string[] lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
        string[] requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2)
        {
            return null;
        }

        string? range = lines
            .Skip(1)
            .Where(l => l.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l["Range:".Length..].Trim())
            .FirstOrDefault();
        return (requestLine[0].ToUpperInvariant(), requestLine[1], range);
    }

    private static async Task WriteHeadAsync(NetworkStream network, string status, IEnumerable<string> headers, CancellationToken cancel)
    {
        var head = new StringBuilder($"HTTP/1.1 {status}\r\n");
        foreach (string header in headers)
        {
            head.Append(header).Append("\r\n");
        }

        // One request per connection keeps the server simple; players open a new one for every seek anyway.
        head.Append("Connection: close\r\n\r\n");
        await network.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), cancel);
    }
}
