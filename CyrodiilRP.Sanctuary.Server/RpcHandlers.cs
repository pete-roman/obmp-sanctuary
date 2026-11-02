using CyrodiilRP.Sanctuary.Common;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Server;

// The server end of SanctuaryRpc. Every message goes from the server to a client, so there is nothing
// to handle here; the class gives the guardian its Send methods.
[RpcHandlersFor(typeof(SanctuaryRpc))]
public sealed partial class RpcHandlers;
