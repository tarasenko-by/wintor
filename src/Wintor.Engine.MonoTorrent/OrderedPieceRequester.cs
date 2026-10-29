using MonoTorrent;
using MonoTorrent.PiecePicking;
using Wintor.Core;

namespace Wintor.Engine.MonoTorrent;

/// <summary>
/// MonoTorrent's <see cref="StandardPieceRequester"/> with a <see cref="DownloadOrder"/>: in order instead of rarest
/// first, and/or the first and last pieces of every file before the rest. MonoTorrent builds its picker chain inside
/// the requester and offers no way to add a link to it, so this is that class (MIT, MonoTorrent 3.0.2) with one more
/// link; the request logic is unchanged.
/// </summary>
internal sealed class OrderedPieceRequester(DownloadOrder order) : IPieceRequester
{
    private IReadOnlyList<ReadOnlyBitField> _ignorable = [];
    private Memory<PieceSegment> _requestBuffer;
    private ReadOnlyBitField[] _otherAvailable = [];
    private PieceSegment[] _cancellations = [];
    private BitField? _temp;
    private IMessageEnqueuer? _enqueuer;
    private IPieceRequesterData? _torrent;
    private IPiecePicker? _picker;

    public DownloadOrder Order { get; } = order;

    public bool InEndgameMode { get; private set; }

    public void Initialise(IPieceRequesterData torrentData, IMessageEnqueuer enqueuer, ReadOnlySpan<ReadOnlyBitField> ignorableBitfields)
    {
        _ignorable = ignorableBitfields.ToArray();
        _enqueuer = enqueuer;
        _torrent = torrentData;
        _temp = new BitField(torrentData.PieceCount);

        IPiecePicker picker = new StandardPicker();
        if (!Order.Sequential)
        {
            picker = new RarestFirstPicker(new RandomisedPicker(picker));
        }

        if (Order.FirstAndLastPiecesFirst)
        {
            picker = new FirstAndLastPiecesPicker(picker);
        }

        _picker = new PriorityPicker(picker);
        _picker.Initialise(torrentData);
    }

    public void AddRequests(ReadOnlySpan<(IRequester Peer, ReadOnlyBitField Available)> peers)
    {
        if (_otherAvailable.Length < peers.Length)
        {
            _otherAvailable = new ReadOnlyBitField[peers.Length];
        }

        Span<ReadOnlyBitField> otherAvailable = _otherAvailable.AsSpan(0, peers.Length);
        for (int i = 0; i < otherAvailable.Length; i++)
        {
            otherAvailable[i] = peers[i].Available;
        }

        foreach ((IRequester peer, ReadOnlyBitField available) in peers)
        {
            if (peer.SuggestedPieces.Count > 0 || (!peer.IsChoking && peer.AmRequestingPiecesCount == 0))
            {
                AddRequests(peer, available, otherAvailable);
            }
        }
    }

    public void AddRequests(IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> allPeers)
    {
        if (!peer.CanRequestMorePieces || _picker is null || _torrent is null || _enqueuer is null)
        {
            return;
        }

        // A choking peer can only continue a piece from its allowed-fast set.
        if (!peer.IsChoking || peer.SupportsFastPeer)
        {
            while (peer.CanRequestMorePieces && _picker.ContinueExistingRequest(peer, 0, available.Length - 1, out PieceSegment segment))
            {
                _enqueuer.EnqueueRequest(peer, segment);
            }
        }

        int count = peer.PreferredRequestAmount(_torrent.PieceLength);
        if (_requestBuffer.Length < count)
        {
            _requestBuffer = new PieceSegment[count];
        }

        Span<PieceSegment> buffer = _requestBuffer.Span[..count];
        ReadOnlyBitField? filtered = null;
        if (!peer.IsChoking || (peer.SupportsFastPeer && peer.IsAllowedFastPieces.Count > 0))
        {
            while (peer.CanRequestMorePieces)
            {
                filtered ??= WithoutIgnorable(available);
                int requests = _picker.PickPiece(peer, filtered, allPeers, 0, _torrent.PieceCount - 1, buffer);
                if (requests == 0)
                {
                    break;
                }

                _enqueuer.EnqueueRequests(peer, buffer[..requests]);
            }
        }

        if (!peer.IsChoking && peer.AmRequestingPiecesCount == 0)
        {
            while (peer.CanRequestMorePieces)
            {
                filtered ??= WithoutIgnorable(available);
                if (_picker.ContinueAnyExistingRequest(peer, filtered, 0, _torrent.PieceCount - 1, 1, out PieceSegment segment))
                {
                    _enqueuer.EnqueueRequest(peer, segment);
                }
                else if ((InEndgameMode || available.AllTrue)
                    && _picker.ContinueAnyExistingRequest(peer, filtered, 0, _torrent.PieceCount - 1, 2, out segment))
                {
                    InEndgameMode = true;
                    _enqueuer.EnqueueRequest(peer, segment);
                }
                else
                {
                    break;
                }
            }
        }
    }

    public bool ValidatePiece(IRequester peer, PieceSegment pieceSegment, out bool pieceComplete, HashSet<IRequester> peersInvolved)
    {
        pieceComplete = false;
        return _picker is not null && _picker.ValidatePiece(peer, pieceSegment, out pieceComplete, peersInvolved);
    }

    public bool IsInteresting(IRequester peer, ReadOnlyBitField bitField) =>
        _picker is not null && _picker.IsInteresting(peer, bitField);

    public void CancelRequests(IRequester peer, int startIndex, int endIndex)
    {
        if (_picker is null || _enqueuer is null)
        {
            return;
        }

        if (_cancellations.Length < peer.AmRequestingPiecesCount)
        {
            _cancellations = new PieceSegment[peer.AmRequestingPiecesCount];
        }

        Span<PieceSegment> cancellations = _cancellations.AsSpan(0, peer.AmRequestingPiecesCount);
        int cancelled = _picker.CancelRequests(peer, startIndex, endIndex, cancellations);
        _enqueuer.EnqueueCancellations(peer, cancellations[..cancelled]);
    }

    public void RequestRejected(IRequester peer, PieceSegment pieceRequest) => _picker?.RequestRejected(peer, pieceRequest);

    public int CurrentRequestCount() => _picker?.CurrentRequestCount() ?? 0;

    /// <summary>What the peer has minus what we have, are hashing or wait for hashes of.</summary>
    private ReadOnlyBitField WithoutIgnorable(ReadOnlyBitField available)
    {
        _temp!.From(available);
        foreach (ReadOnlyBitField ignorable in _ignorable)
        {
            _temp.NAnd(ignorable);
        }

        return _temp;
    }
}

/// <summary>Asks for the first and the last piece of every wanted file before anything else.</summary>
internal sealed class FirstAndLastPiecesPicker(IPiecePicker next) : PiecePickerFilter(next)
{
    // Sorted and distinct: neighbouring small files share pieces.
    private int[] _edges = [];

    public override void Initialise(IPieceRequesterData torrentData)
    {
        base.Initialise(torrentData);
        _edges = torrentData.Files
            .Where(f => f.Length > 0)
            .SelectMany(f => new[] { f.StartPieceIndex, f.EndPieceIndex })
            .Distinct()
            .Order()
            .ToArray();
    }

    public override int PickPiece(IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
    {
        // "available" is already without the pieces we have and without skipped files, so a piece we do not need is
        // never asked for here, and once the edges are in this loop only reads bits.
        foreach (int piece in _edges)
        {
            if (piece < startIndex || piece > endIndex || !available[piece])
            {
                continue;
            }

            int picked = Next.PickPiece(peer, available, otherPeers, piece, piece, requests);
            if (picked > 0)
            {
                return picked;
            }
        }

        return Next.PickPiece(peer, available, otherPeers, startIndex, endIndex, requests);
    }
}
