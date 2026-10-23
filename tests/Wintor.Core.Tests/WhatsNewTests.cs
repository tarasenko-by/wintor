using Wintor.Core;

namespace Wintor.Core.Tests;

public class WhatsNewTests
{
    private static readonly Version Latest = WhatsNew.Releases[0].Version;

    [Fact]
    public void ReleasesAreNewestFirst()
    {
        Assert.Equal(WhatsNew.Releases.OrderByDescending(r => r.Version), WhatsNew.Releases);
    }

    [Fact]
    public void FirstLaunch_OnlyRemembers()
    {
        Assert.Equal(WhatsNewStep.Remember, WhatsNew.Decide(Latest, null, enabled: true));
        Assert.Equal(WhatsNewStep.Remember, WhatsNew.Decide(Latest, "", enabled: true));
    }

    [Fact]
    public void SameOrOlderVersion_DoesNothing()
    {
        Assert.Equal(WhatsNewStep.None, WhatsNew.Decide(Latest, WhatsNew.Stored(Latest), enabled: true));
        Assert.Equal(WhatsNewStep.None, WhatsNew.Decide(new Version(0, 1, 0), WhatsNew.Stored(Latest), enabled: true));
    }

    [Fact]
    public void UpdateWithEntry_Shows_UnlessSwitchedOff()
    {
        Assert.Equal(WhatsNewStep.Show, WhatsNew.Decide(Latest, "0.1.0", enabled: true));
        Assert.Equal(WhatsNewStep.Remember, WhatsNew.Decide(Latest, "0.1.0", enabled: false));
    }

    [Fact]
    public void UpdateWithoutEntry_OnlyRemembers()
    {
        Assert.Equal(WhatsNewStep.Remember, WhatsNew.Decide(new Version(9, 9, 9), "0.1.0", enabled: true));
    }

    [Fact]
    public void RevisionDoesNotCount()
    {
        Assert.Equal(WhatsNewStep.None, WhatsNew.Decide(new Version(0, 2, 0, 7), "0.2.0", enabled: true));
        Assert.Equal("0.2", WhatsNew.Display(new Version(0, 2, 0, 0)));
        Assert.Equal("0.2.1", WhatsNew.Display(new Version(0, 2, 1)));
    }
}
