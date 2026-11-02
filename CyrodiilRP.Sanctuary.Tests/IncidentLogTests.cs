using System.Text.Json;
using CyrodiilRP.Sanctuary.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CyrodiilRP.Sanctuary.Tests;

public sealed class IncidentLogTests
{
    private static readonly IncidentRecord Sample = new(
        new DateTimeOffset(2026, 11, 2, 23, 30, 0, TimeSpan.FromHours(1)),
        "L_ICMarketDistrict",
        "Market District",
        new PersonRecord("4e658659-426d-4081-b6f6-dbe25666fd42", "Ri'Zahra"),
        42.5f,
        false,
        "suspect",
        [new PersonRecord(null, "Brawler")]);

    [Fact]
    public void ModdataSitsBesideTheModsFolder()
    {
        var server = Path.Combine(Path.GetTempPath(), "obmp-server");
        var modServerHalf = Path.Combine(server, "mods", "CyrodiilRP.Sanctuary", "server");
        Assert.Equal(Path.Combine(server, "moddata", "CyrodiilRP.Sanctuary"), IncidentLog.Resolve(modServerHalf));
    }

    [Fact]
    public void OneIncidentIsOneLineOfJson()
    {
        var line = IncidentLog.LineOf(Sample);
        Assert.DoesNotContain('\n', line);

        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.Equal("Market District", root.GetProperty("areaName").GetString());
        Assert.Equal("Ri'Zahra", root.GetProperty("victim").GetProperty("name").GetString());
        Assert.Equal("suspect", root.GetProperty("verdict").GetString());
        Assert.Equal("Brawler", root.GetProperty("aggressors")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void FilesAreNamedByTheUtcDate()
    {
        var log = new IncidentLog(null!, NullLogger.Instance) { FolderOverride = "logs" };
        Assert.Equal(Path.Combine("logs", "incidents-2026-11-02.jsonl"), log.FileOf(Sample));
    }
}
