using CyrodiilRP.Sanctuary.Server;
using Xunit;

namespace CyrodiilRP.Sanctuary.Tests;

public sealed class WardTests
{
    [Fact]
    public void FirstLookTakesTheHealthAsItIs()
    {
        var ward = new Ward();
        Assert.Equal(0f, ward.Watch(60f, 100f));
        Assert.Equal(60f, ward.Kept);
    }

    [Fact]
    public void ADropIsReportedAndTheKeptHealthStays()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        Assert.Equal(30f, ward.Watch(50f, 100f));
        Assert.Equal(80f, ward.Kept);
        Assert.Equal(80f, ward.RestoreTo(100f));
    }

    [Fact]
    public void TheSameDropSeenAgainBeforeTheWriteLandsIsReportedAgain()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        ward.Watch(50f, 100f);
        Assert.Equal(30f, ward.Watch(50f, 100f));
    }

    [Fact]
    public void HealingRaisesTheKeptHealth()
    {
        var ward = new Ward();
        ward.Watch(40f, 100f);
        Assert.Equal(0f, ward.Watch(70f, 100f));
        Assert.Equal(70f, ward.Kept);
    }

    [Fact]
    public void ASmallStepDownIsNotADropYet()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        Assert.Equal(0f, ward.Watch(79.7f, 100f));
        Assert.Equal(80f, ward.Kept);
    }

    [Fact]
    public void SmallStepsDownAddUpToADrop()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        Assert.Equal(0f, ward.Watch(79.6f, 100f));
        Assert.Equal(0f, ward.Watch(79.6f, 100f));
        Assert.Equal(0.8f, ward.Watch(79.2f, 100f), 3);
        Assert.Equal(80f, ward.RestoreTo(100f));
    }

    [Fact]
    public void ALowerMaximumIsNotADrop()
    {
        var ward = new Ward();
        ward.Watch(100f, 100f);
        Assert.Equal(0f, ward.Watch(90f, 90f));
        Assert.Equal(90f, ward.Kept);
    }

    [Fact]
    public void AcceptTakesALossThatIsNotPutBack()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        ward.Watch(50f, 100f);
        ward.Accept(50f);
        Assert.Equal(0f, ward.Watch(50f, 100f));
    }

    [Fact]
    public void NothingWorthKeepingRestoresTheFullBar()
    {
        Assert.Equal(120f, new Ward().RestoreTo(120f));

        var dead = new Ward();
        dead.Watch(0f, 120f);
        Assert.Equal(120f, dead.RestoreTo(120f));
    }
}
