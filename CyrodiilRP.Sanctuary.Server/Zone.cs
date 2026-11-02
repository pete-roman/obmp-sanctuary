namespace CyrodiilRP.Sanctuary.Server;

// Which areas are the Sanctuary: the Imperial City inside its walls, without the Arena, plus what the
// server owner adds and minus what they take out in config.json.
public sealed class Zone
{
    private readonly HashSet<string> _areas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    public Zone(IEnumerable<string> extra, IEnumerable<string> excluded)
    {
        foreach (var area in ImperialCity.Districts.Concat(ImperialCity.Interiors))
        {
            _areas.Add(area.Id);
            _names[area.Id] = area.Name;
        }

        foreach (var area in ImperialCity.Arena)
        {
            _names[area.Id] = area.Name;
        }

        _areas.UnionWith(extra);
        _areas.ExceptWith(ImperialCity.Arena.Select(area => area.Id));
        _areas.ExceptWith(excluded);
    }

    public int Count => _areas.Count;

    public bool Contains(string? area) => area is not null && _areas.Contains(area);

    // The name players know the area by, or its id when the city list does not have it.
    public string NameOf(string? area) =>
        area is null ? "unknown area" : _names.TryGetValue(area, out var name) ? name : area;
}
