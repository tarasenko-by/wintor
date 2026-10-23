using Wintor.Core;

namespace Wintor.Core.Tests;

public class ColumnFitTests
{
    [Fact]
    public void EnoughRoom_KeepsWidths()
    {
        Assert.Equal([100, 50], ColumnFit.Shrink([(100, 40), (50, 40)], 200));
    }

    [Fact]
    public void ShrinksInProportionToWidth()
    {
        // 400 + 100 into 250: everything at half, so the wide column gives up four times as much.
        Assert.Equal([200, 50], ColumnFit.Shrink([(400, 40), (100, 40)], 250));
    }

    [Fact]
    public void StopsAtMinimum_AndTheRestKeepShrinking()
    {
        // At half the narrow column would be 40 < 60, so it stays at 60 and the wide one takes the rest.
        double[] widths = ColumnFit.Shrink([(400, 80), (80, 60)], 240);

        Assert.Equal([180, 60], widths);
    }

    [Fact]
    public void NeverExceedsTheRoom()
    {
        double[] widths = ColumnFit.Shrink([(137, 50), (91, 50), (73, 50)], 250);

        Assert.True(widths.Sum() <= 250);
        Assert.All(widths, w => Assert.True(w >= 50));
    }

    [Fact]
    public void BelowAllMinimums_GivesMinimums()
    {
        Assert.Equal([40, 30], ColumnFit.Shrink([(100, 40), (80, 30)], 50));
    }
}
