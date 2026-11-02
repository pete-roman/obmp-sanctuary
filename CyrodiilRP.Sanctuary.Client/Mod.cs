using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Client;

// The client half's entry point. It only shows what the server sends, so there is nothing to set up.
[ModEntry]
public sealed partial class Mod(ILogger logger)
{
    private void Init()
    {
        logger.LogInformation("Sanctuary: client half loaded.");
    }
}
