namespace Wintor.Core;

/// <summary>
/// Which pieces a torrent asks peers for first. Both are off by default: when everybody downloads in order, the
/// rare pieces at the end stop spreading and the swarm suffers, so these are for watching or checking a file early.
/// </summary>
/// <param name="Sequential">Pieces from the start of the torrent to its end, instead of rarest first.</param>
/// <param name="FirstAndLastPiecesFirst">
/// The first and last pieces of every file before the rest: players read the header at the start and the index at
/// the end (MP4, MKV) before they can play anything.
/// </param>
public sealed record DownloadOrder(bool Sequential, bool FirstAndLastPiecesFirst)
{
    public static DownloadOrder Default { get; } = new(false, false);

    public bool IsDefault => !Sequential && !FirstAndLastPiecesFirst;
}
