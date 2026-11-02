#!powershell.exe -ExecutionPolicy Bypass -File

# Edit these lists to match your mod. MakeModFolder.ps1 uses them to build
#
#   Output/mods/<modName>/manifest.json
#   Output/mods/<modName>/client/...
#   Output/mods/<modName>/server/...
#
# The server hands the client folder to connecting players and keeps the server folder to itself.

# Folder name of the packaged mod. Conventionally the uniqueId from manifest.json.
$modName = "CyrodiilRP.Sanctuary"

# The projects the two halves build from.
$clientProject = "CyrodiilRP.Sanctuary.Client"
$serverProject = "CyrodiilRP.Sanctuary.Server"

# --- Client half, from <clientProject>/bin/<Configuration>/net10.0 ---
$clientBuildFiles = @(
    "CyrodiilRP.Sanctuary.Client.dll",
    "CyrodiilRP.Sanctuary.Common.dll"   # shared assembly both halves need
)

# --- Server half, from <serverProject>/bin/<Configuration>/net10.0 ---
$serverBuildFiles = @(
    "CyrodiilRP.Sanctuary.Server.dll",
    "CyrodiilRP.Sanctuary.Common.dll"
)

# Copied from "Content" into the mod root, alongside the two halves.
$manifestFiles = @(
    "manifest.json"
)

# Copied from "Content" into client/ and server/. Add any non-code files your mod ships.
$clientContentFiles = @(
    # "icon.png"
)

$serverContentFiles = @(
    "config.json"
)

# Added only in Debug builds.
$clientDebugBuildFiles = @(
    "CyrodiilRP.Sanctuary.Client.pdb",
    "CyrodiilRP.Sanctuary.Common.pdb"
)

$serverDebugBuildFiles = @(
    "CyrodiilRP.Sanctuary.Server.pdb",
    "CyrodiilRP.Sanctuary.Common.pdb"
)
