# CyrodiilRP Sanctuary

An OblivionMP (OB:MP 1.0) server mod for the CyrodiilRP roleplay server. The Imperial City inside its walls is a sanctuary: NPCs, guards and peaceful players get every hit undone, and a player who attacks anyone there loses that protection. The Arena is left out, so fights by agreement still happen on its sand.

**Status: prototype, 0.2.0.** Unit-tested. Version 0.1.1 (players only) was tested in game with one client on a local OB:MP 1.0.1 server; the NPC and aggressor rules of 0.2.0 are not tested in game yet. See [Tested in game](#tested-in-game) and [Limits](#limits).

## The rules

Inside the walls of the Imperial City (not in the Arena):

- **NPCs and guards are always untouchable.** Every drop in their health is put back at once.
- **Players are untouchable** until they attack someone, a player or an NPC.
- **An attacker becomes an aggressor:** no protection for 5 minutes after their last attack (or until they die), and a bounty of 40 for the guards. They see *"You attacked someone in the Imperial City. The Sanctuary no longer protects you."*
- **The one attacked keeps the protection.** If they strike back, they become an aggressor too.
- **The way out for a victim is to run to the NPCs:** the NPCs fight back as the game makes them, and they can't be hurt.
- Walking in and out shows *"You enter the Sanctuary of the Imperial City. Blood may not be spilled here."* and *"You leave the Sanctuary of the Imperial City."* A protected player who gets hurt sees *"The Sanctuary shields you."*

## Where the Sanctuary is

Everything inside the walls of the Imperial City, from `CyrodiilRP.Sanctuary.Server/ImperialCity.cs`:

- **Districts (11):** Arboretum, Arena District, Elven Gardens, Imperial Palace, Prison District, Market District, Talos Plaza, Temple District, Arcane University (plus the main quest's versions of the palace and the Temple District).
- **Interiors (313):** every shop, house, inn, guild hall, watch tower, the palace, the Bloodworks and the Imperial Prison.
- **Not the Sanctuary:** the Arena's fighting floor and stands (5 cells, found by name in the whole world data, so none can be added back), the sewers, the Imperial Subterrane and the catacombs under the city, the Waterfront and everything else outside the walls.

The list is generated, not typed: `tools/imperial_city_areas.py` follows the doors from the city's districts through the server's world data, names every cell from `Oblivion.esm`, and keeps the cells the game itself names as Imperial City (`IC...`) plus the Imperial Prison.

```powershell
python tools/imperial_city_areas.py <server>\content\world "<Oblivion Remastered>\OblivionRemastered\Content\Dev\ObvData\Data\Oblivion.esm"
```

Server owners change the zone in `config.json` (`extraAreas`, `excludedAreas`) instead.

## How it works

The SDK has no hit event and does not say who hit a player, so the server watches every tick:

1. **Who fights:** for every player, when they last swung (`AttackingRequest`) or cast (`CastingRequest`), and whether the game has them in combat (`InCombat`).
2. **Ward:** for every protected player and every NPC inside the Sanctuary, the health it should have. A drop of more than 0.5 is put back with a server write, which the owning game applies; smaller steps down add up until they count. An aggressor takes the blow. Nothing is revived: a player who dies anyway gets up through OB:MP's own rebirth (a reload of the world save, at its starting point), and dying ends their aggressor streak. An NPC killed by one blow stays dead (the SDK cannot revive NPCs) and is logged.
3. **Blame:** who caused the drop, for any victim, protected or not. Every player who swung within 4 m in the last 1.5 s counts; if nobody did, every player within 40 m who swung (a bow) or cast **while in combat** counts, so a healer or a passer-by casting a light spell does not. Everyone who counts becomes an aggressor, a lone attacker and each one of a group alike. Nobody: a fall or an NPC.

   Each tick first gathers every drop and marks every aggressor, and only then decides whose health goes back, so the order players are processed in changes nothing. An aggressor who disconnects keeps the streak (by ReadyM account) when they come back.
4. **Incident log:** hits on one victim in one area close together make one incident, one JSON line in `<server>/moddata/CyrodiilRP.Sanctuary/incidents-<UTC date>.jsonl` (verdict `suspect`, `several` or `none`, with the aggressors), plus a line in the server log. Open incidents are written when the server stops.

## Limits

What OB:MP 1.0 does not let a mod do yet:

- **No real invulnerability.** The mod puts health back after the hit. A single blow that takes all of it still kills: a player then gets up through OB:MP's rebirth (a reload of the world save, at its starting point, outside the city); an NPC stays dead and the death is logged.
- **No attack order for NPCs.** The server can't tell guards to attack an aggressor. NPCs who are hit fight back as the game makes them, and the bounty is there for the guards.
- **Only health is protected.** Fatigue (knock-downs), paralysis, drained magicka and pickpocketing work as usual.
- **Blame is a guess** from who was fighting next to the victim, not proof of who hit whom.
- **NPCs without a ref id** (some spawned ones) are not watched.

## Config

`server/config.json` in the mod folder. Keys are not case-sensitive and every key is optional.

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | Turns the mod off without removing it. |
| `extraAreas` | `[]` | Area ids added to the Sanctuary. |
| `excludedAreas` | `[]` | Area ids taken out of it. The Arena can't be added. |
| `healAllDamage` | `true` | For protected players. `false`: only drops a player is suspected of are put back, so hunger or poison mods still bite inside the city. NPCs always get every drop back. |
| `meleeRange` | `400` | Centimeters. |
| `rangedRange` | `4000` | Centimeters. |
| `attackWindowSeconds` | `1.5` | How long before the drop a swing or cast still counts. |
| `incidentSeconds` | `3` | Hits on one victim this close together are one incident. |
| `aggressorSeconds` | `300` | How long an aggressor stays unprotected after their last attack. Dying ends it sooner. |
| `bounty` | `40` | Bounty added when a player becomes an aggressor. `0` adds none. |
| `staffAccounts` | `[]` | ReadyM account ids (GUIDs) of staff who get a line on screen for every new aggressor. |

## Tested in game

On a local OB:MP 1.0.1 server with ReadyM Launcher 0.34.0 and one client, through the game console (2 Nov 2026):

- **Loads:** the server loads the mod, reads `config.json` including the list keys, and reports 324 areas.
- **Area ids match:** The Merchants Inn reports `L_PersistentDungeon-2C171` and the Market District `L_ICMarketDistrict`, both in the list; the Arena floor (`coc ICArena`) is outside the Sanctuary.
- **Messages:** entering and leaving the Sanctuary show their lines.
- **Health put back:** an 11 m fall in the Market District took 81.5 of 83 health; the incident was logged and the health read 83.00 afterwards. The same fall in the Arena left 21.78 and nothing was put back.
- **Death in the city:** detected and logged. OB:MP's rebirth reloads the world save and puts the character at the save's starting point outside the city.

## Not verified yet

- **Who hurt whom:** the blame logic needs a second client.
- **The shield line:** "The Sanctuary shields you." was not caught on a screenshot (the health was back before the shot).
- **0.2.0 rules in game:** NPCs and guards get their health back, a player who hits an NPC becomes an aggressor, the bounty reaches the game and the guards react to it.
- **Mods that hurt on purpose:** with `healAllDamage: true`, hunger damage (e.g. a Starving status) is undone inside the city.

## Build and test

Needs the .NET 10 SDK. The OB:MP packages come from NuGet.

```powershell
dotnet test CyrodiilRP.Sanctuary.sln -c Release      # unit tests: Ward, Blame, Aggression, Zone, the city list, the log
.\MakeModFolder.ps1 Release -NoExplorer               # Output\mods\CyrodiilRP.Sanctuary\
```

Copy `Output\mods\CyrodiilRP.Sanctuary\` into the server's `mods\` folder and restart the server. Players get the client half from the launcher.

## Layout

| Path | What |
|---|---|
| `CyrodiilRP.Sanctuary.Common/SanctuaryRpc.cs` | Every message, all server to client. |
| `CyrodiilRP.Sanctuary.Server/Guardian.cs` | The per-tick loop: who fights, wards for players and NPCs, blame, aggressors, incidents, staff lines. |
| `CyrodiilRP.Sanctuary.Server/Ward.cs`, `Blame.cs`, `Zone.cs`, `Aggression.cs` | The logic, free of the game, covered by the tests. |
| `CyrodiilRP.Sanctuary.Server/ImperialCity.cs` | The generated city list. |
| `CyrodiilRP.Sanctuary.Server/IncidentLog.cs` | The JSON lines log, written off the game thread. |
| `CyrodiilRP.Sanctuary.Client/RpcHandlers.cs` | On-screen messages. |
| `CyrodiilRP.Sanctuary.Tests/` | xUnit tests. |
| `tools/imperial_city_areas.py` | Generates the city list. |

## Credits and licence

Made by Piotr 'Pete' Romanczyk in collaboration with ReadyM, for the CyrodiilRP project on OB:MP. Built with AI coding assistants (Claude Code and OpenAI Codex), from [readycodeio/oblivionmp-mod-template](https://github.com/readycodeio/oblivionmp-mod-template) (SDK 1.0.0).

Licensed under the [MIT License](LICENSE).

Not affiliated with Bethesda Softworks or ZeniMax. The Elder Scrolls and Oblivion are trademarks of ZeniMax Media Inc. The area and cell names and ids in `ImperialCity.cs` come from the game, which the mod needs to run. No game files or assets are included.
