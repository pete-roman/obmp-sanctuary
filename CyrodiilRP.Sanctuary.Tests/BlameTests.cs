using System.Numerics;
using CyrodiilRP.Sanctuary.Server;
using Xunit;

namespace CyrodiilRP.Sanctuary.Tests;

public sealed class BlameTests
{
    private const float Melee = 400f;
    private const float Ranged = 4000f;
    private const float Window = 1.5f;
    private const double Now = 100;
    private const double Never = double.NegativeInfinity;

    private static readonly Vector3 Victim = new(1000f, 2000f, 300f);

    private static Bystander<string> At(string name, float meters, double attack = Never, double cast = Never, bool combat = false) =>
        new(name, Victim + new Vector3(meters * 100f, 0f, 0f), attack, cast, combat);

    private static Verdict<string> Judge(params Bystander<string>[] others) =>
        Blame.Judge(Victim, Now, others, Melee, Ranged, Window);

    [Fact]
    public void NobodyFightingMeansNoSuspect()
    {
        var verdict = Judge(At("Idle", 1f), At("Far swinger", 80f, attack: Now, combat: true));
        Assert.Equal(VerdictKind.None, verdict.Kind);
        Assert.Empty(verdict.Suspects);
    }

    [Fact]
    public void OneSwingerInReachIsTheSuspect()
    {
        var verdict = Judge(At("Brawler", 2f, attack: Now - 0.2), At("Idle", 1f));
        Assert.Equal(VerdictKind.Suspect, verdict.Kind);
        Assert.Equal(["Brawler"], verdict.Suspects);
    }

    [Fact]
    public void ASwingInReachOutweighsACasterFurtherAway()
    {
        var verdict = Judge(At("Brawler", 3f, attack: Now), At("Mage", 20f, cast: Now, combat: true));
        Assert.Equal(["Brawler"], verdict.Suspects);
    }

    [Fact]
    public void EverySwingerInReachIsASuspect()
    {
        var verdict = Judge(At("One", 2f, attack: Now), At("Two", 3f, attack: Now - 1));
        Assert.Equal(VerdictKind.Several, verdict.Kind);
        Assert.Equal(["One", "Two"], verdict.Suspects);
    }

    [Fact]
    public void ACasterInCombatIsTheSuspect()
    {
        var verdict = Judge(At("Mage", 30f, cast: Now - 0.5, combat: true));
        Assert.Equal(VerdictKind.Suspect, verdict.Kind);
        Assert.Equal(["Mage"], verdict.Suspects);
    }

    [Fact]
    public void ACasterOutOfCombatIsNot()
    {
        Assert.Equal(VerdictKind.None, Judge(At("Healer", 3f, cast: Now)).Kind);
        Assert.Equal(VerdictKind.None, Judge(At("Healer", 30f, cast: Now)).Kind);
    }

    [Fact]
    public void AnArcherCountsOnlyInCombat()
    {
        Assert.Equal(VerdictKind.Suspect, Judge(At("Archer", 25f, attack: Now, combat: true)).Kind);
        Assert.Equal(VerdictKind.None, Judge(At("Archer", 25f, attack: Now)).Kind);
    }

    [Fact]
    public void TwoInCombatAtRangeAreBothSuspects()
    {
        var verdict = Judge(At("Archer", 25f, attack: Now, combat: true), At("Mage", 15f, cast: Now, combat: true));
        Assert.Equal(VerdictKind.Several, verdict.Kind);
        Assert.Equal(2, verdict.Suspects.Count);
    }

    [Fact]
    public void BeyondRangedRangeDoesNotCount()
    {
        Assert.Equal(VerdictKind.None, Judge(At("Mage", 41f, cast: Now, combat: true)).Kind);
    }

    [Fact]
    public void ASwingOutsideTheWindowDoesNotCount()
    {
        Assert.Equal(VerdictKind.None, Judge(At("Brawler", 1f, attack: Now - 2)).Kind);
    }

    [Fact]
    public void TheEdgesOfRangeAndWindowCount()
    {
        Assert.Equal(VerdictKind.Suspect, Judge(At("Brawler", 4f, attack: Now - Window)).Kind);
    }
}
