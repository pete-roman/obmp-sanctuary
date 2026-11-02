namespace CyrodiilRP.Sanctuary.Server;

// Keeps one character's health inside the Sanctuary. Health may go up (potions, spells, rest) and the
// maximum may change; any drop is reported, so the guardian can put the health back.
public sealed class Ward
{
    // Replicated decimals ignore changes of 0.1 or less, so a drop counts once it is bigger than this.
    // Smaller steps down do not move the baseline, so they add up until they count.
    private const float Tolerance = 0.5f;

    // The health this ward stands for, or null before the first look.
    public float? Kept { get; private set; }

    // How much health was lost since the baseline; 0 when none. After a loss, Kept stays where it was.
    public float Watch(float hp, float hpBase)
    {
        if (Kept is not float kept)
        {
            Kept = Math.Min(hp, hpBase);
            return 0f;
        }

        kept = Math.Min(kept, hpBase);
        Kept = kept;
        if (hp >= kept)
        {
            Kept = Math.Min(hp, hpBase);
            return 0f;
        }

        return kept - hp > Tolerance ? kept - hp : 0f;
    }

    // Takes the new health as it is, for a loss that is not put back.
    public void Accept(float hp) => Kept = hp;

    // The health to give back: what was kept, or the full bar when nothing worth keeping was seen.
    public float RestoreTo(float hpBase) => Kept is > 0f and var kept ? Math.Min(kept, hpBase) : hpBase;
}
