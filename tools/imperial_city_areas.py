"""
Writes CyrodiilRP.Sanctuary.Server/ImperialCity.cs: every OB:MP area inside the walls of the Imperial City, and the
Arena's own cells, which are left out of the Sanctuary.

    python tools/imperial_city_areas.py <server content/world folder> <Oblivion.esm>

How it decides:
- Districts: every exterior world space whose area id starts with L_IC (the city's districts, the palace grounds,
  the Arcane University).
- Interiors: every interior reachable through doors from a district without going out to L_Tamriel, kept when the
  cell's editor id starts with IC (the game's own naming for Imperial City cells) or is the Imperial Prison. That
  leaves out the sewers, the Imperial Subterrane and the catacombs under the city, which are dungeons.
- Arena: the cells where the fights and the crowd are, found by editor id in the whole world data (not only the
  ones the doors reach), so none of them can be added back. Excluded from the Sanctuary; the Bloodworks stays in.

The world folder is the one an OB:MP server build ships (content/world: index.json and one folder per area). Names
come from Oblivion.esm, read directly (TES4 records, 20-byte headers).
"""
import json
import os
import struct
import sys
import zlib
from collections import deque

INTERIOR_PREFIX = 'L_PersistentDungeon-'
EXTRA_INTERIORS = {'Dark04Dungeon01'}  # the Imperial Prison, under the Bastion in the Prison District
# ChorrolArena and ICArenaSpectators: the stands
ARENA_CELLS = {'ChorrolArena', 'ICArena', 'ArenaMatchCell', 'ICArenaGrandChampion', 'ICArenaSpectators'}
DISTRICT_NAMES = {
    'L_ICArboretumDistrict': 'Arboretum',
    'L_ICArenaDistrict': 'Arena District',
    'L_ICElvenGardensDistrict': 'Elven Gardens District',
    'L_ICImperialPalace': 'Imperial Palace',
    'L_ICImperialPalaceMQ16': 'Imperial Palace',
    'L_ICImperialPrisonDistrict': 'Prison District',
    'L_ICMarketDistrict': 'Market District',
    'L_ICTalosPlazaDistrict': 'Talos Plaza District',
    'L_ICTempleDistrict': 'Temple District',
    'L_ICTempleDistrictMQ16': 'Temple District',
    'L_ICTheArcaneUniversity': 'Arcane University',
}


def names_of_district(area):
    return DISTRICT_NAMES.get(area, area[2:])


def cell_names(esm_path):
    """Editor id and name of every CELL record, keyed by form id (hex, as in the area ids)."""
    names = {}
    buf = open(esm_path, 'rb').read()

    def walk(pos, end):
        while pos < end:
            kind = buf[pos:pos + 4]
            size, = struct.unpack_from('<I', buf, pos + 4)
            if kind == b'GRUP':
                walk(pos + 20, pos + size)
                pos += size
                continue
            flags, form = struct.unpack_from('<II', buf, pos + 8)
            data = buf[pos + 20:pos + 20 + size]
            pos += 20 + size
            if kind != b'CELL':
                continue
            if flags & 0x00040000:
                data = zlib.decompress(data[4:])
            edid = full = ''
            p = 0
            while p + 6 <= len(data):
                sub = data[p:p + 4]
                length, = struct.unpack_from('<H', data, p + 4)
                value = data[p + 6:p + 6 + length].rstrip(b'\0').decode('cp1252', 'replace').strip()
                if sub == b'EDID':
                    edid = value
                elif sub == b'FULL':
                    full = value
                p += 6 + length
            names['%X' % (form & 0x00FFFFFF)] = (edid, full)

    header, = struct.unpack_from('<I', buf, 4)
    walk(20 + header, len(buf))
    return names


def door_graph(world):
    """Which area each placed reference is in, and the doors (destination reference ids) of every area."""
    ref_area, doors = {}, {}
    for area in os.listdir(world):
        folder = os.path.join(world, area)
        if not os.path.isdir(folder):
            continue
        for file in os.listdir(folder):
            if not file.endswith('.json'):
                continue
            for ref in json.load(open(os.path.join(folder, file), encoding='utf-8')).get('References', []):
                ref_area[ref['RefId']] = area
                target = ref.get('Teleport') or {}
                if target.get('DestinationRefId'):
                    doors.setdefault(area, []).append(target['DestinationRefId'])
    return ref_area, doors


def main(world, esm):
    names = cell_names(esm)
    ref_area, doors = door_graph(world)
    districts = sorted(a for a in os.listdir(world) if a.startswith('L_IC') and os.path.isdir(os.path.join(world, a)))

    reached = set(districts)
    queue = deque(districts)
    while queue:
        for destination in doors.get(queue.popleft(), []):
            area = ref_area.get(destination)
            if area and area not in reached and area.startswith(INTERIOR_PREFIX):
                reached.add(area)
                queue.append(area)

    def cell(area):
        return names.get(area[len(INTERIOR_PREFIX):], ('', ''))

    arena = [(a, *cell(a)) for a in sorted(set(ref_area.values()))
             if a.startswith(INTERIOR_PREFIX) and cell(a)[0] in ARENA_CELLS]
    interiors = []
    for area in sorted(reached - set(districts)):
        edid, full = cell(area)
        if edid not in ARENA_CELLS and (edid.startswith('IC') or edid in EXTRA_INTERIORS):
            interiors.append((area, edid, full))
    interiors.sort(key=lambda row: row[1])
    arena.sort(key=lambda row: row[1])

    def rows(items):
        return '\n'.join('        new("%s", "%s", "%s"),' % (a, e, (f or e).replace('"', '\\"')) for a, e, f in items)

    district_rows = [(d, d[2:], names_of_district(d)) for d in districts]
    out = os.path.join(os.path.dirname(__file__), '..', 'CyrodiilRP.Sanctuary.Server', 'ImperialCity.cs')
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write('''// Generated by tools/imperial_city_areas.py from the server's world data and Oblivion.esm. Do not edit by hand:
// change the script and run it again. Server owners add or remove areas in config.json instead.

namespace CyrodiilRP.Sanctuary.Server;

/// <summary>One area of the city: its OB:MP area id, the game's editor id and the name players see.</summary>
public readonly record struct CityArea(string Id, string EditorId, string Name);

/// <summary>The areas of the Imperial City.</summary>
public static class ImperialCity
{
    /// <summary>The districts: exterior world spaces inside the walls.</summary>
    public static readonly CityArea[] Districts =
    [
%s
    ];

    /// <summary>Interiors inside the walls: shops, houses, inns, guilds, towers, the palace and the prison.</summary>
    public static readonly CityArea[] Interiors =
    [
%s
    ];

    /// <summary>The Arena's fighting floor and stands. Never part of the Sanctuary; the Bloodworks is.</summary>
    public static readonly CityArea[] Arena =
    [
%s
    ];
}
''' % (rows(district_rows), rows(interiors), rows(arena)))
    print('%d districts, %d interiors, %d arena cells -> %s' % (len(districts), len(interiors), len(arena), os.path.normpath(out)))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
