using ReadyM.Api.Multiplayer;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Common;

// Every message the mod sends. The server decides everything; the client only shows it.
[RpcContracts]
public static partial class SanctuaryRpc
{
    // The player walked into the Sanctuary (true) or out of it (false).
    [ServerToClient] public static partial void Zone(bool inside);

    // The player was hurt inside the Sanctuary and got the health back.
    [ServerToClient] public static partial void Shielded();

    // The player attacked someone inside the Sanctuary and lost its protection (true), or got it back (false).
    [ServerToClient] public static partial void Aggressor(bool on);

    // A line for staff about something that happened in the Sanctuary.
    [ServerToClient] public static partial void StaffNotice(string text);
}
