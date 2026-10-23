using Wintor.Core;

namespace Wintor.Core.Tests;

public class PieceMapTests
{
    [Fact]
    public void Fold_GroupsPiecesIntoBuckets()
    {
        // 8 pieces, first half done, into 4 buckets of 2.
        double[] buckets = PieceMap.Fold(8, i => i < 4, buckets: 4);

        Assert.Equal([1.0, 1.0, 0.0, 0.0], buckets);
    }

    [Fact]
    public void Fold_PartialBucketIsAFraction()
    {
        double[] buckets = PieceMap.Fold(4, i => i == 0, buckets: 2);

        Assert.Equal([0.5, 0.0], buckets);
    }

    [Fact]
    public void Fold_FewerPiecesThanBuckets_UsesOneBucketPerPiece()
    {
        Assert.Equal(3, PieceMap.Fold(3, _ => true).Length);
        Assert.Empty(PieceMap.Fold(0, _ => true));
    }

    [Theory]
    [InlineData(new[] { 1, 1, 1, 1 }, 1.0)]
    [InlineData(new[] { 1, 2, 1, 2 }, 1.5)]
    [InlineData(new[] { 0, 1, 1, 1 }, 0.75)]
    [InlineData(new int[0], 0.0)]
    public void DistributedCopies_LikeUTorrent(int[] copies, double expected)
    {
        Assert.Equal(expected, PieceMap.DistributedCopies(copies), precision: 3);
    }
}
