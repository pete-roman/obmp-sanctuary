using System.Numerics;

namespace CyrodiilRP.Sanctuary.Server;

// Another player near a victim: where they stand, when they last swung or cast (seconds on the guardian's
// clock, or NegativeInfinity for never), and whether the game has them in combat.
public readonly record struct Bystander<T>(T Player, Vector3 Position, double LastAttack, double LastCast, bool InCombat);

public enum VerdictKind
{
    None,    // nobody near was fighting: a fall, an NPC
    Suspect, // one player was fighting next to the victim
    Several, // more than one was, and all of them count
}

public readonly record struct Verdict<T>(VerdictKind Kind, IReadOnlyList<T> Suspects);

// Who hurt the victim. The SDK does not say who hit a player, so this goes by who was fighting next to them
// just before the health dropped. Ranges in centimeters, the window in seconds.
public static class Blame
{
    // A swing within melee range counts on its own: everyone swinging that close is a suspect. Further away,
    // only a player the game has in combat counts, swinging (a bow) or casting, so a healer or a passer-by
    // casting a light spell is not taken for an attacker.
    public static Verdict<T> Judge<T>(Vector3 victim, double now, IEnumerable<Bystander<T>> others,
        float meleeRange, float rangedRange, float window)
    {
        var close = new List<T>();
        var far = new List<T>();
        foreach (var other in others)
        {
            var distance = Vector3.Distance(victim, other.Position);
            var swung = now - other.LastAttack <= window;
            var cast = now - other.LastCast <= window;
            if (swung && distance <= meleeRange)
                close.Add(other.Player);
            else if ((swung || cast) && other.InCombat && distance <= rangedRange)
                far.Add(other.Player);
        }

        var suspects = close.Count > 0 ? close : far;
        return suspects.Count switch
        {
            0 => new Verdict<T>(VerdictKind.None, []),
            1 => new Verdict<T>(VerdictKind.Suspect, suspects),
            _ => new Verdict<T>(VerdictKind.Several, suspects),
        };
    }
}
