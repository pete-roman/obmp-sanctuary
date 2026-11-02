using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Server;

// The server half's entry point. Services and RPC handlers are found on their own.
[ModEntry]
public sealed partial class Mod(ILogger logger, SanctuaryConfig config)
{
    private void Init()
    {
        logger.LogInformation("Sanctuary: server half loaded from {Folder}{Off}.", ModDirectory,
            config.Enabled ? "" : " (disabled in config.json)");
    }
}
