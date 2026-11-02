using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Events;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Server;

// The area each player is in, from the server's own events (e.g. "L_ICMarketDistrict", or a shop's
// "L_PersistentDungeon-..."). A character's scope can come back empty right after the player came in,
// so the events are the source. A player who disconnects leaves their area first, so nothing goes stale.
[Service]
public sealed partial class AreaTracker(IServerEvents events)
{
    private readonly Dictionary<PlayerId, string> _areas = new();

    private void Start()
    {
        events.OnPlayerJoinedArea += OnJoined;
        events.OnPlayerLeftArea += OnLeft;
    }

    private void Stop()
    {
        events.OnPlayerJoinedArea -= OnJoined;
        events.OnPlayerLeftArea -= OnLeft;
    }

    // The player's area, or null while the server has not said.
    public string? AreaOf(PlayerId player) => _areas.TryGetValue(player, out var area) ? area : null;

    private void OnJoined(PlayerId player, AreaId area) => _areas[player] = area.ToManaged();

    private void OnLeft(PlayerId player, AreaId area)
    {
        if (_areas.TryGetValue(player, out var known) && known == area.ToManaged())
            _areas.Remove(player);
    }
}
