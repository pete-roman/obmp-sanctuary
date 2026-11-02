using CyrodiilRP.Sanctuary.Server;
using Xunit;

namespace CyrodiilRP.Sanctuary.Tests;

public sealed class ZoneTests
{
    private const string Market = "L_ICMarketDistrict";
    private const string Bloodworks = "L_PersistentDungeon-37868";
    private const string Prison = "L_PersistentDungeon-2D941";
    private const string ArenaFloor = "L_PersistentDungeon-37867";
    private const string ArenaStands = "L_PersistentDungeon-1C60F";
    private const string ArenaSpectators = "L_PersistentDungeon-3D61C";
    private const string MarketSewers = "L_PersistentDungeon-96AC5";

    private static readonly Zone City = new([], []);

    [Theory]
    [InlineData(Market)]
    [InlineData("L_ICTempleDistrict")]
    [InlineData("L_ICArenaDistrict")]
    [InlineData("L_ICTheArcaneUniversity")]
    [InlineData(Bloodworks)]
    [InlineData(Prison)]
    public void TheCityInsideTheWallsIsTheSanctuary(string area) => Assert.True(City.Contains(area));

    [Theory]
    [InlineData(ArenaFloor)]
    [InlineData(ArenaStands)]
    [InlineData(ArenaSpectators)]
    [InlineData("L_Tamriel")] // the Waterfront and everything outside the walls
    [InlineData("L_ChorrolWorld")]
    [InlineData(null)]
    public void TheArenaAndTheRestOfCyrodiilAreNot(string? area) => Assert.False(City.Contains(area));

    [Fact]
    public void TheSewersUnderTheCityAreNot()
    {
        Assert.DoesNotContain(ImperialCity.Interiors, area => area.EditorId.Contains("Sewer"));
        Assert.False(City.Contains(MarketSewers));
    }

    [Fact]
    public void TheConfigAddsAndRemovesAreas()
    {
        var zone = new Zone(["L_Tamriel"], [Market]);
        Assert.True(zone.Contains("L_Tamriel"));
        Assert.False(zone.Contains(Market));
        Assert.Equal(City.Count, zone.Count);
    }

    [Fact]
    public void TheArenaCannotBeAdded()
    {
        Assert.False(new Zone([ArenaFloor, ArenaSpectators], []).Contains(ArenaFloor));
        Assert.False(new Zone([ArenaFloor, ArenaSpectators], []).Contains(ArenaSpectators));
    }

    [Fact]
    public void AreasHaveTheirNames()
    {
        Assert.Equal("Market District", City.NameOf(Market));
        Assert.Equal("Arena Bloodworks", City.NameOf(Bloodworks));
        Assert.Equal("Imperial City Arena", City.NameOf(ArenaFloor));
        Assert.Equal("L_Tamriel", City.NameOf("L_Tamriel"));
    }

    [Fact]
    public void TheCityListHasNoDuplicates()
    {
        var all = ImperialCity.Districts.Concat(ImperialCity.Interiors).Concat(ImperialCity.Arena).Select(area => area.Id).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(11, ImperialCity.Districts.Length);
        Assert.Equal(5, ImperialCity.Arena.Length);
    }
}
