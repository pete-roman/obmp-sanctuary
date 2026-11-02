using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Sanctuary.Server;

// Runs the Sanctuary every tick. Inside the city:
// - NPCs and guards are always untouchable: every drop in their health is put back.
// - Players are untouchable too, until they attack someone (a player or an NPC). An attacker becomes an
//   aggressor: no protection for AggressorSeconds after their last attack, or until they die, and a
//   bounty for the guards. The one they attacked keeps the protection; if that player strikes back, they
//   become an aggressor as well. The way out for a victim is to run to the NPCs, who are untouchable and
//   fight back as the game makes them.
//
// The SDK has no hit event and does not say who hit whom, so the guardian watches: it notes who swings or
// casts, keeps a Ward on the health of everyone inside, and when a health drops, Blame names the players
// who were fighting next to the victim. Each tick first gathers every drop and marks every aggressor, and
// only then decides whose health goes back, so the order players are visited in changes nothing.
[Service]
public sealed partial class Guardian(
    IEntities entities,
    IPlayers players,
    AreaTracker areas,
    SanctuaryConfig config,
    RpcHandlers rpc,
    IncidentLog log,
    ILogger logger)
{
    private const double ShieldedNoticeSeconds = 3;

    // One character as this tick saw it. Taken in a single pass, so nothing queries inside a query.
    private readonly record struct Seen(
        PlayerId Player, string? Area, Vector3 Position, float Hp, float HpBase, bool Dead, bool InCombat);

    // A player inside the city whose health dropped this tick, waiting for the decision.
    private readonly record struct Hurt(Seen Seen, float Lost, Verdict<PlayerId> Verdict);

    private sealed class Watch
    {
        public readonly Ward Ward = new();
        public readonly Aggression Aggression = new();
        public Guid? Account;
        public bool? Inside;
        public double LastAttack = double.NegativeInfinity;
        public double LastCast = double.NegativeInfinity;
        public double ShieldedAt = double.NegativeInfinity;
        public bool DeathCounted;
        public Incident? Open;
    }

    private sealed class NpcWatch
    {
        public readonly Ward Ward = new();
        public bool DeathCounted;
        public Incident? Open;
    }

    // Hits on one victim in one area close together, logged as one.
    private sealed class Incident(PersonRecord victim, string area, double start)
    {
        public readonly PersonRecord Victim = victim;
        public readonly string Area = area;
        public readonly DateTimeOffset Time = DateTimeOffset.UtcNow;
        public double Last = start;
        public float Damage;
        public bool Died;
        public readonly List<PlayerId> Suspects = [];
    }

    private readonly Dictionary<PlayerId, Watch> _watches = new();
    private readonly Dictionary<uint, NpcWatch> _npcs = new();
    private readonly Dictionary<Guid, double> _aggressorsAway = new(); // ReadyM account -> streak end
    private readonly List<Seen> _seen = [];
    private readonly List<Hurt> _hurt = [];
    private readonly HashSet<PlayerId> _present = [];
    private readonly HashSet<uint> _npcsPresent = [];
    private readonly List<PlayerId> _gone = [];
    private readonly List<uint> _npcsGone = [];
    private readonly HashSet<Guid> _staff = [];
    private Zone _zone = null!;
    private double _now;

    private void Start()
    {
        _zone = new Zone(config.ExtraAreas, config.ExcludedAreas);
        foreach (var account in config.StaffAccounts)
        {
            if (Guid.TryParse(account, out var id))
                _staff.Add(id);
            else
                logger.LogWarning("Sanctuary: '{Account}' in StaffAccounts is not a ReadyM account id.", account);
        }

        logger.LogInformation("Sanctuary: {Count} areas, {Staff} staff accounts, aggressors unprotected for {Seconds} s, bounty {Bounty}.",
            _zone.Count, _staff.Count, config.AggressorSeconds, config.Bounty);
    }

    // Incidents still waiting for their quiet time are logged now, not lost with the server.
    private void Stop()
    {
        foreach (var watch in _watches.Values)
            CloseOpen(ref watch.Open);
        foreach (var watch in _npcs.Values)
            CloseOpen(ref watch.Open);
    }

    private void Update()
    {
        if (!config.Enabled)
            return;

        _now += Time.DeltaTime;

        // 1. Who is where and who is fighting. A swing seen in this tick counts for a hit in this tick.
        _seen.Clear();
        _present.Clear();
        foreach (var main in entities.Query<MainCharacter>())
        {
            var player = main.PlayerId;
            var watch = WatchOf(player);
            if (main.AttackingRequest)
                watch.LastAttack = _now;
            if (main.CastingRequest)
                watch.LastCast = _now;

            _present.Add(player);
            _seen.Add(new Seen(player, areas.AreaOf(player), main.Position, main.Hp, main.HpBase, main.IsDead, main.InCombat));
        }

        // 2. Every drop in health, players and NPCs, and every aggressor it makes. NPCs get their health
        //    back here: they are untouchable whoever hit them.
        _hurt.Clear();
        foreach (var seen in _seen)
            WatchPlayer(seen);
        GuardNpcs();

        // 3. Now that every aggressor of this tick is known: whose health goes back.
        foreach (var hurt in _hurt)
            Decide(hurt);

        EndAggression();
        CloseQuietIncidents();
        Forget();
    }

    private void WatchPlayer(Seen seen)
    {
        var watch = _watches[seen.Player];
        if (seen.Area is not { } area)
            return; // not placed yet; keep whatever was known

        var inside = _zone.Contains(area);
        if (watch.Inside != inside)
        {
            if (watch.Inside is not null)
                rpc.SendZone(seen.Player, inside);
            watch.Inside = inside;
        }

        if (seen.Dead)
        {
            // Untouchable players do not die unless one blow takes all their health; aggressors may. Either
            // way the player gets up through the game's own rebirth, as an innocent.
            var lostAll = watch.Ward.Kept ?? seen.HpBase;
            watch.Ward.Accept(0f);
            if (watch.Aggression.IsOn(_now))
            {
                watch.Aggression.Clear();
                TellStaff($"Sanctuary: the aggressor {NameOf(seen.Player)} died in {_zone.NameOf(area)}.");
            }

            if (inside && !watch.DeathCounted)
            {
                watch.DeathCounted = true;
                var verdict = Judge(seen.Position, area, seen.Player);
                MarkAll(verdict, NameOf(seen.Player), area);
                Record(ref watch.Open, PersonOf(seen.Player), area, lostAll, died: true, verdict);
            }

            return;
        }

        watch.DeathCounted = false;
        if (!inside)
        {
            watch.Ward.Accept(seen.Hp);
            return;
        }

        var lost = watch.Ward.Watch(seen.Hp, seen.HpBase);
        if (lost <= 0f)
            return;

        var hit = Judge(seen.Position, area, seen.Player);
        MarkAll(hit, NameOf(seen.Player), area);
        _hurt.Add(new Hurt(seen, lost, hit));
    }

    private void Decide(Hurt hurt)
    {
        var seen = hurt.Seen;
        var watch = _watches[seen.Player];
        var protectedNow = !watch.Aggression.IsOn(_now);
        if (protectedNow && (config.HealAllDamage || hurt.Verdict.Kind != VerdictKind.None))
        {
            if (entities.TryLookup<MainCharacter, PlayerId>(seen.Player, out MainCharacter main))
                main.Hp = watch.Ward.RestoreTo(seen.HpBase);
            if (_now - watch.ShieldedAt >= ShieldedNoticeSeconds)
            {
                watch.ShieldedAt = _now;
                rpc.SendShielded(seen.Player);
            }
        }
        else
        {
            watch.Ward.Accept(seen.Hp); // an aggressor takes the blow
        }

        Record(ref watch.Open, PersonOf(seen.Player), seen.Area!, hurt.Lost, died: false, hurt.Verdict);
    }

    private void GuardNpcs()
    {
        _npcsPresent.Clear();
        foreach (var npc in entities.Query<Npc>())
        {
            // An NPC without a ref id has nothing stable to keep its ward under, so it is not watched.
            var refId = npc.RefId;
            if (refId == 0 || !entities.TryGetScope(npc, out Area scope))
                continue;

            var area = scope.AreaId.ToManaged();
            if (!_zone.Contains(area))
                continue;

            _npcsPresent.Add(refId);
            if (!_npcs.TryGetValue(refId, out var watch))
                _npcs[refId] = watch = new NpcWatch();

            if (npc.IsDead)
            {
                // A single blow took it all; the SDK has no way to bring an NPC back, so it is logged.
                if (!watch.DeathCounted)
                {
                    watch.DeathCounted = true;
                    var verdict = Judge(npc.Position, area, null);
                    MarkAll(verdict, NpcName(refId), area);
                    Record(ref watch.Open, NpcPerson(refId), area, watch.Ward.Kept ?? npc.HpBase, died: true, verdict);
                }

                continue;
            }

            watch.DeathCounted = false;
            var lost = watch.Ward.Watch(npc.Hp, npc.HpBase);
            if (lost <= 0f)
                continue;

            npc.Hp = watch.Ward.RestoreTo(npc.HpBase); // NPCs and guards are always untouchable
            var hit = Judge(npc.Position, area, null);
            MarkAll(hit, NpcName(refId), area);
            Record(ref watch.Open, NpcPerson(refId), area, lost, died: false, hit);
        }
    }

    private Verdict<PlayerId> Judge(Vector3 victim, string area, PlayerId? victimPlayer) =>
        Blame.Judge(victim, _now, BystandersOf(area, victimPlayer), config.MeleeRange, config.RangedRange,
            config.AttackWindowSeconds);

    private IEnumerable<Bystander<PlayerId>> BystandersOf(string area, PlayerId? victim)
    {
        foreach (var other in _seen)
        {
            if (other.Player == victim || other.Dead || other.Area != area)
                continue;
            var watch = _watches[other.Player];
            yield return new Bystander<PlayerId>(other.Player, other.Position, watch.LastAttack, watch.LastCast, other.InCombat);
        }
    }

    // Everyone Blame names gives up the protection: a lone attacker and every one of a group alike.
    private void MarkAll(Verdict<PlayerId> verdict, string victim, string area)
    {
        foreach (var suspect in verdict.Suspects)
            MarkAggressor(suspect, victim, area);
    }

    // Every attack starts the aggressor's clock again; only the first one of a streak is announced.
    private void MarkAggressor(PlayerId aggressor, string victim, string area)
    {
        if (!_watches.TryGetValue(aggressor, out var watch) || !watch.Aggression.Mark(_now, config.AggressorSeconds))
            return;

        rpc.SendAggressor(aggressor, true);
        AddBounty(aggressor);
        TellStaff($"Sanctuary: {NameOf(aggressor)} attacked {victim} in {_zone.NameOf(area)} and lost the Sanctuary's protection.");
    }

    private void Record(ref Incident? open, PersonRecord victim, string area, float lost, bool died, Verdict<PlayerId> verdict)
    {
        // An incident belongs to one area; a hit somewhere else starts a new one.
        if (open is { } previous && previous.Area != area)
            CloseOpen(ref open);

        var incident = open ??= new Incident(victim, area, _now);
        incident.Last = _now;
        incident.Damage += lost;
        incident.Died |= died;
        foreach (var suspect in verdict.Suspects)
        {
            if (!incident.Suspects.Contains(suspect))
                incident.Suspects.Add(suspect);
        }
    }

    private void EndAggression()
    {
        foreach (var (player, watch) in _watches)
        {
            if (watch.Aggression.Expired(_now))
                rpc.SendAggressor(player, false);
        }
    }

    private void AddBounty(PlayerId aggressor)
    {
        if (config.Bounty <= 0f || !entities.TryLookup<MainCharacter, PlayerId>(aggressor, out MainCharacter main))
            return;

        main.TryGetSocialAttributeValue((byte)CharacterSocialAttribute.Bounty, out var bounty);
        main.SetSocialAttribute((byte)CharacterSocialAttribute.Bounty, bounty + config.Bounty);
    }

    private void CloseQuietIncidents()
    {
        foreach (var watch in _watches.Values)
        {
            if (watch.Open is { } incident && _now - incident.Last >= config.IncidentSeconds)
                CloseOpen(ref watch.Open);
        }

        foreach (var watch in _npcs.Values)
        {
            if (watch.Open is { } incident && _now - incident.Last >= config.IncidentSeconds)
                CloseOpen(ref watch.Open);
        }
    }

    private void CloseOpen(ref Incident? open)
    {
        if (open is not { } incident)
            return;
        open = null;

        var verdict = incident.Suspects.Count switch { 0 => "none", 1 => "suspect", _ => "several" };
        var suspects = incident.Suspects.Select(PersonOf).ToList();
        log.Write(new IncidentRecord(
            incident.Time,
            incident.Area,
            _zone.NameOf(incident.Area),
            incident.Victim,
            MathF.Round(incident.Damage, 1),
            incident.Died,
            verdict,
            suspects));

        logger.LogInformation("Sanctuary: {Victim} lost {Damage:0.#} health in {Area}{Died}; verdict {Verdict}, aggressors: {Suspects}.",
            incident.Victim.Name, incident.Damage, _zone.NameOf(incident.Area), incident.Died ? " and died" : "", verdict,
            suspects.Count == 0 ? "none" : string.Join(", ", suspects.Select(person => person.Name)));
    }

    private void TellStaff(string text)
    {
        logger.LogInformation("{Text}", text);
        if (_staff.Count == 0)
            return;

        foreach (var player in _present)
        {
            if (_watches.TryGetValue(player, out var watch) && watch.Account is { } account && _staff.Contains(account))
                rpc.SendStaffNotice(player, text);
        }
    }

    // A new player's watch; an aggressor who disconnected and came back picks up where they left off.
    private Watch WatchOf(PlayerId player)
    {
        if (!_watches.TryGetValue(player, out var watch))
            _watches[player] = watch = new Watch();

        if (watch.Account is null && players.GetReadyMId(player) is { } account)
        {
            watch.Account = account;
            if (_aggressorsAway.Remove(account, out var until) && until > _now)
                watch.Aggression.Resume(until);
        }

        return watch;
    }

    // Players who left and NPCs no longer inside: an open incident is logged, then they are forgotten. An
    // aggressor's streak is kept by their ReadyM account, so reconnecting does not clear it.
    private void Forget()
    {
        _gone.Clear();
        foreach (var player in _watches.Keys)
        {
            if (!_present.Contains(player))
                _gone.Add(player);
        }

        foreach (var player in _gone)
        {
            var watch = _watches[player];
            if (watch.Aggression.IsOn(_now) && watch.Account is { } account)
                _aggressorsAway[account] = watch.Aggression.Until;
            CloseOpen(ref watch.Open);
            _watches.Remove(player);
        }

        _npcsGone.Clear();
        foreach (var refId in _npcs.Keys)
        {
            if (!_npcsPresent.Contains(refId))
                _npcsGone.Add(refId);
        }

        foreach (var refId in _npcsGone)
        {
            CloseOpen(ref _npcs[refId].Open);
            _npcs.Remove(refId);
        }
    }

    private string NameOf(PlayerId player) =>
        entities.TryLookup(player, out Player p) && p.Nickname.ToString() is { Length: > 0 } name ? name : $"player {player}";

    private PersonRecord PersonOf(PlayerId player) =>
        new(_watches.TryGetValue(player, out var watch) ? watch.Account?.ToString() : null, NameOf(player));

    private static string NpcName(uint refId) => $"NPC {refId:X8}";

    private static PersonRecord NpcPerson(uint refId) => new(null, NpcName(refId));
}
