using Wintor.Core;

namespace Wintor.Core.Tests;

public class RateMeterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    [Fact]
    public void FirstSample_IsZero()
    {
        var meter = new RateMeter(Window);
        Assert.Equal(0, meter.Sample(1000, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void SteadyGrowth_GivesBytesPerSecond()
    {
        var meter = new RateMeter(Window);
        meter.Sample(0, TimeSpan.FromSeconds(0));
        meter.Sample(2000, TimeSpan.FromSeconds(1));
        Assert.Equal(2000, meter.Sample(4000, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void NoNewBytes_DropsToZeroAfterWindow()
    {
        var meter = new RateMeter(Window);
        meter.Sample(0, TimeSpan.FromSeconds(0));
        meter.Sample(10_000, TimeSpan.FromSeconds(1));
        for (int s = 2; s <= 7; s++)
        {
            meter.Sample(10_000, TimeSpan.FromSeconds(s));
        }

        Assert.Equal(0, meter.Sample(10_000, TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void CounterReset_StartsOver()
    {
        var meter = new RateMeter(Window);
        meter.Sample(0, TimeSpan.FromSeconds(0));
        meter.Sample(50_000, TimeSpan.FromSeconds(1));
        Assert.Equal(0, meter.Sample(100, TimeSpan.FromSeconds(2)));
        Assert.Equal(400, meter.Sample(500, TimeSpan.FromSeconds(3)));
    }
}
