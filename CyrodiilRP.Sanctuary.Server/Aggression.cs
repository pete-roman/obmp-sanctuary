namespace CyrodiilRP.Sanctuary.Server;

// Whether a player has given up the Sanctuary's protection by attacking someone. Every new attack starts
// the clock again; the protection comes back once the clock runs out, or when the player dies.
public sealed class Aggression
{
    private double _until = double.NegativeInfinity;
    private bool _on;

    // When the protection comes back, for carrying the streak over a reconnect.
    public double Until => _until;

    public bool IsOn(double now) => _on && now < _until;

    // Marks an attack; true when the player was not an aggressor a moment ago.
    public bool Mark(double now, double seconds)
    {
        var started = !IsOn(now);
        _until = Math.Max(_until, now + seconds);
        _on = true;
        return started;
    }

    // Picks up a streak the player had before they reconnected.
    public void Resume(double until)
    {
        _until = until;
        _on = true;
    }

    // True once, on the first look after the clock ran out.
    public bool Expired(double now)
    {
        if (!_on || now < _until)
            return false;
        _on = false;
        return true;
    }

    public void Clear()
    {
        _on = false;
        _until = double.NegativeInfinity;
    }
}
