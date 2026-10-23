namespace Wintor.Core;

/// <summary>
/// Turns an ever-growing byte counter into bytes per second, averaged over a short window.
/// </summary>
public sealed class RateMeter(TimeSpan window)
{
    private readonly Queue<(TimeSpan Time, long Bytes)> _samples = new();
    private long _lastBytes;

    /// <summary>Records the counter value at <paramref name="now"/> and returns the current rate.</summary>
    public long Sample(long totalBytes, TimeSpan now)
    {
        // Counters start from zero again when a torrent is restarted.
        if (_samples.Count > 0 && totalBytes < _lastBytes)
        {
            _samples.Clear();
        }

        _lastBytes = totalBytes;
        _samples.Enqueue((now, totalBytes));
        while (_samples.Count > 1 && now - _samples.Peek().Time > window)
        {
            _samples.Dequeue();
        }

        (TimeSpan firstTime, long firstBytes) = _samples.Peek();
        double seconds = (now - firstTime).TotalSeconds;
        return seconds > 0 ? (long)((totalBytes - firstBytes) / seconds) : 0;
    }
}
