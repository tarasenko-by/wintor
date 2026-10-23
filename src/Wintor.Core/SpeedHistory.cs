namespace Wintor.Core;

/// <summary>
/// The last few minutes of download and upload speed for every torrent, one sample per tick. Recorded for all
/// torrents all the time, so the speed graph is already full when the user opens it.
/// </summary>
public sealed class SpeedHistory(int length = SpeedHistory.DefaultLength)
{
    /// <summary>Five minutes of one-second samples.</summary>
    public const int DefaultLength = 300;

    private readonly Dictionary<string, (Queue<long> Down, Queue<long> Up)> _samples = [];

    public int Length { get; } = length;

    /// <summary>Adds one sample per torrent and forgets torrents that are gone.</summary>
    public void Record(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        foreach (TorrentSnapshot s in snapshots)
        {
            if (!_samples.TryGetValue(s.Id, out (Queue<long> Down, Queue<long> Up) series))
            {
                series = (new Queue<long>(Length), new Queue<long>(Length));
                _samples[s.Id] = series;
            }

            Add(series.Down, s.DownloadRate);
            Add(series.Up, s.UploadRate);
        }

        if (_samples.Count > snapshots.Count)
        {
            var alive = snapshots.Select(s => s.Id).ToHashSet();
            foreach (string id in _samples.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                _samples.Remove(id);
            }
        }
    }

    /// <summary>Download speeds in bytes per second, oldest first; empty for an unknown torrent.</summary>
    public long[] Downloads(string id) => _samples.TryGetValue(id, out var series) ? series.Down.ToArray() : [];

    /// <summary>Upload speeds in bytes per second, oldest first; empty for an unknown torrent.</summary>
    public long[] Uploads(string id) => _samples.TryGetValue(id, out var series) ? series.Up.ToArray() : [];

    private void Add(Queue<long> samples, long value)
    {
        if (samples.Count == Length)
        {
            samples.Dequeue();
        }

        samples.Enqueue(value);
    }
}
