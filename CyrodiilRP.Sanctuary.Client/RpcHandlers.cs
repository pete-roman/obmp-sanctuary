using CyrodiilRP.Sanctuary.Common;
using OblivionMp.Sdk;
using OblivionMpCSharpMod;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Client;

// The client end of SanctuaryRpc: the server decides, the client shows it in the game's own message widgets.
[RpcHandlersFor(typeof(SanctuaryRpc))]
public sealed partial class RpcHandlers(IUserInterface ui)
{
    partial void OnZone(bool inside) =>
        ui.ShowMessage(inside
            ? "You enter the Sanctuary of the Imperial City. Blood may not be spilled here."
            : "You leave the Sanctuary of the Imperial City.", MessagePosition.TopLeft, 5);

    partial void OnShielded() =>
        ui.ShowMessage("The Sanctuary shields you.", MessagePosition.Center, 2);

    partial void OnAggressor(bool on) =>
        ui.ShowMessage(on
            ? "You attacked someone in the Imperial City. The Sanctuary no longer protects you."
            : "The Sanctuary protects you again.", MessagePosition.Center, 5);

    partial void OnStaffNotice(string text) =>
        ui.ShowMessage(text, MessagePosition.TopLeft, 8);
}
