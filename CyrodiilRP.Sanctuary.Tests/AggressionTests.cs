using CyrodiilRP.Sanctuary.Server;
using Xunit;

namespace CyrodiilRP.Sanctuary.Tests;

public sealed class AggressionTests
{
    [Fact]
    public void AFirstAttackStartsAStreak()
    {
        var aggression = new Aggression();
        Assert.False(aggression.IsOn(10));
        Assert.True(aggression.Mark(10, 300));
        Assert.True(aggression.IsOn(10));
        Assert.True(aggression.IsOn(309));
    }

    [Fact]
    public void AnotherAttackInTheStreakIsNotNewButRestartsTheClock()
    {
        var aggression = new Aggression();
        aggression.Mark(10, 300);
        Assert.False(aggression.Mark(200, 300));
        Assert.True(aggression.IsOn(450));
        Assert.False(aggression.IsOn(500));
    }

    [Fact]
    public void ExpiryIsReportedOnce()
    {
        var aggression = new Aggression();
        aggression.Mark(0, 300);
        Assert.False(aggression.Expired(299));
        Assert.True(aggression.Expired(300));
        Assert.False(aggression.Expired(301));
        Assert.False(aggression.IsOn(301));
    }

    [Fact]
    public void AnAttackAfterExpiryStartsANewStreak()
    {
        var aggression = new Aggression();
        aggression.Mark(0, 300);
        aggression.Expired(300);
        Assert.True(aggression.Mark(400, 300));
    }

    [Fact]
    public void DeathClearsItWithoutAnExpiry()
    {
        var aggression = new Aggression();
        aggression.Mark(0, 300);
        aggression.Clear();
        Assert.False(aggression.IsOn(1));
        Assert.False(aggression.Expired(1000));
        Assert.True(aggression.Mark(2, 300));
    }
}
