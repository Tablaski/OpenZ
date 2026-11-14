using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV157 "the computer mans empty guns" (version 1.3, item 11). The engine's computer treats an empty gun like any other
    /// target of its order rounds (zbot.cpp:513-576 collects it, :222-414 pairs units and targets mutually nearest), so a gun that is never
    /// the nearest target of an idle unit (one deep in its own land, one beside a road its squads walk past on their way to something else)
    /// stays empty all game. With DV157, in every order round (after the round's delay, right before DV37's car-park pass and the
    /// engine's random wave), the computer also sends robot squads to man empty guns:
    /// <list type="bullet">
    /// <item><b>ON THE WAY.</b> A squad of ours that walks past an empty gun (its leader within <see cref="ZWorld.CpuGunPassPx"/> px of the gun,
    /// on the same ground) while it is on its way to something that is neither a fight nor a flag (an empty vehicle, a grenade crate, a
    /// plain move) turns off and mans the gun. Never a squad that is fighting (an attack order, a fort entry, or firing at something now)
    /// or on its way to take a flag.</item>
    /// <item><b>AT THE FRONTIER.</b> An empty gun inside a frontier territory of ours (a zone of ours that touches a zone that is not ours),
    /// or within <see cref="ZWorld.CpuGunFrontierPx"/> px of one, gets the nearest IDLE squad of this round that reaches it (at most
    /// <see cref="ZWorld.CpuGunSendPx"/> px away). Not before <see cref="ZWorld.CpuGunFrontierFromSec"/> s (the opening's rush), and on the car
    /// park map only once the side has <see cref="ZWorld.CpuGunCarsFirst"/> vehicles (with the ones its squads walk to).</item>
    /// <item><b>LIMITS.</b> One squad per gun (a gun some unit of ours is already after is skipped); at most
    /// <see cref="ZWorld.CpuGunPerRound"/> gun orders a round (<see cref="ZWorld.CpuGunPerRoundEasy"/> on Easy) and
    /// <see cref="ZWorld.CpuGunMaxEnRoute"/> squads on their way to a gun at once; never the last defender of a flag or fort of ours (a squad
    /// within <see cref="ZWorld.CpuGunGuardPx"/> px of one with no other unit of ours there); never the Electrobot or a hero squad
    /// (NoAutoEnter), a strike group member, a guard of DV136, a unit on a repair trip, a minion.</item>
    /// </list>
    /// Both teams when the computer plays them, every level, Normal and Easy. It acts only through the computer's own order path
    /// (ZBot.Command -> ZWorld.SendWaypoints, the Zod ENTER order), from what the computer knows (its known waypoints, its regions).
    /// Deterministic: the object list's order, whole-number distances, ties by id; no random draw. Switch:
    /// <see cref="ZWorld.CpuGunManDeviation"/> (View/ZGame.cs sets it at every start from PlayerPrefs oz_cpu_man_guns, default on; off in
    /// 'Zod exact'; the static default is off, so headless checks and the parity rows keep the engine's computer unless they set it).
    /// With the switch off GunManRound returns at once: the computer is tick for tick today's.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV157: the one switch. View/ZGame.cs sets it at every start (PlayerPrefs oz_cpu_man_guns, default 1; off in 'Zod exact');
        /// off by default (headless).</summary>
        public static bool CpuGunManDeviation = false;
        /// <summary>"Passes guns on the way": the squad leader's centre within this many px of the gun's centre (160 px = 10 tiles = 20 m,
        /// a little more than a gatling's reach of 128 px: the gun stands by the road it walks).</summary>
        public const int CpuGunPassPx = 160;
        /// <summary>"Guns near its frontier territories": inside a frontier zone of ours or this close to its rectangle (96 px = 6 tiles).</summary>
        public const int CpuGunFrontierPx = 96;
        /// <summary>No frontier order in the first minute: the opening's squads run for flags and cars (a gun order then took a side's only squad off the car park on L17).</summary>
        public const double CpuGunFrontierFromSec = 60;
        /// <summary>On the car park map (DV37) a side has this many vehicles (with the ones its squads walk to) before a frontier gun may take its next idle squad.</summary>
        public const int CpuGunCarsFirst = 3;
        /// <summary>true = a zone of ours is a frontier zone when it touches any zone that is not ours (neutral too: the edge of what it holds);
        /// false = only a zone another team holds (DV136's frontier).</summary>
        public static readonly bool CpuGunFrontierNeutral = true;
        /// <summary>The farthest an idle squad is sent to a frontier gun (960 px = 60 tiles = 120 m).</summary>
        public const int CpuGunSendPx = 960;
        /// <summary>Gun orders per round (Normal / Easy), and the most squads on their way to a gun at once.</summary>
        public const int CpuGunPerRound = 2, CpuGunPerRoundEasy = 1, CpuGunMaxEnRoute = 3;
        /// <summary>The last defenders: a squad within this many px of a flag or fort of ours with no other unit of ours that close stays.</summary>
        public const int CpuGunGuardPx = 160;

        /// <summary>Checks and reports only (never the game): the switch off until the result is disposed, then as it was.</summary>
        public static IDisposable CpuGunManOff() => new CpuGunManHold(false);
        /// <summary>Checks only: the switch on until the result is disposed, then as it was.</summary>
        public static IDisposable CpuGunManOn() => new CpuGunManHold(true);
        sealed class CpuGunManHold : IDisposable
        {
            readonly bool was = CpuGunManDeviation;
            public CpuGunManHold(bool on) { CpuGunManDeviation = on; }
            public void Dispose() { CpuGunManDeviation = was; }
        }
    }

    public sealed partial class ZBot
    {
        public enum GunManKind { Passing, Frontier }
        /// <summary>One gun order: when, which rule, the squad, the gun, how far (px); what the squad was doing before (its known first order's
        /// mode; hadOrder false = it stood idle), whether that was a flag run or the squad was firing (both must stay false: the check counts them).</summary>
        public struct GunManEvent { public double time; public GunManKind kind; public int unitId, gunId, dist; public WpMode fromMode; public bool hadOrder, fromFlag, fighting; }

        /// <summary>DV157: the gun orders this computer gave, in order (checks). At most 2000 entries.</summary>
        public readonly List<GunManEvent> GunManEvents = new List<GunManEvent>();
        /// <summary>DV157 statistics: gun orders given (on the way / at the frontier), candidate squads passed over as a flag's or fort's last defender.</summary>
        public int GunManPassing { get; private set; }
        public int GunManFrontier { get; private set; }
        public int GunManKeptDefenders { get; private set; }

        readonly List<int> gmFrontier = new List<int>();
        readonly List<ZObject> gmPosts = new List<ZObject>(), gmMine = new List<ZObject>();
        /// <summary>The squads this rule sent and their guns (unit id, gun id), until the squad's known first order is no longer that ENTER.</summary>
        readonly List<(int unit, int gun)> gmSent = new List<(int unit, int gun)>();

        /// <summary>DV157, Stage1AI_3 before DV37's car-park pass (that pass took every new squad on L17, so this one saw none until the cars ran out): see the class notes of this file. units = this round's idle units (the ordered
        /// squads leave it), targeted = the objects our units are already after (the manned guns join it), targets = the round's targets.</summary>
        void GunManRound(List<ZObject> units, List<ZObject> targeted, List<ZObject> targets)
        {
            if (!ZWorld.CpuGunManDeviation || W.GameOver) return;
            KeepRuleGunsSingle(targets);
            var guns = new List<ZObject>();
            foreach (var t in targets) if (t.Kind == ZKind.Cannon && t.CanBeEntered() && !targeted.Contains(t)) guns.Add(t);
            if (guns.Count == 0) return;
            int budget = Math.Min(Difficulty == ZBotDifficulty.Easy ? ZWorld.CpuGunPerRoundEasy : ZWorld.CpuGunPerRound, ZWorld.CpuGunMaxEnRoute - GunsEnRoute());
            if (budget <= 0) return;
            gmPosts.Clear(); gmMine.Clear();                                                       // the flags and forts of ours, our units (for the last-defender rule)
            foreach (var p in W.Objects)
            {
                if (p.Owner != Team || p.Destroyed) continue;
                if ((p.Kind == ZKind.MapItem && p.Type == "flag") || (p.Kind == ZKind.Building && p.IsFort)) gmPosts.Add(p);
                else if (p.IsMobile && !p.IsMinion && !Gone(p) && !p.IsShuttle) gmMine.Add(p);
            }
            // 1. on the way: squads walking past a gun (the nearest squad to each gun, guns in object order)
            foreach (var g in guns)
            {
                if (budget <= 0) break;
                if (targeted.Contains(g)) continue;
                ZObject best = null; long bd = long.MaxValue;
                foreach (var o in W.Objects)
                {
                    if (!GunPasser(o)) continue;
                    long d2 = D2(o, g);
                    if (d2 > (long)ZWorld.CpuGunPassPx * ZWorld.CpuGunPassPx || (d2 == bd && best != null && o.Id > best.Id) || d2 > bd) continue;
                    if (!GunReaches(o, g) || LastDefender(o)) continue;
                    best = o; bd = d2;
                }
                if (best != null && GunOrder(best, g, GunManKind.Passing, bd, units, targeted, targets)) budget--;
            }
            if (budget <= 0 || !GunFrontierTime()) return;
            // 2. at the frontier: the nearest idle squad of this round
            GunFrontierZones(gmFrontier);
            if (gmFrontier.Count == 0) return;
            foreach (var g in guns)
            {
                if (budget <= 0) break;
                if (targeted.Contains(g) || !GunNearFrontier(g, gmFrontier)) continue;
                ZObject best = null; long bd = long.MaxValue;
                foreach (var u in units)
                {
                    if (u.Kind != ZKind.Robot || u.IsMinion || u.NoAutoEnter || Gone(u) || u.AttackObject != null || u.HoldPosition) continue;   // never a squad that is firing (a fight of its own looks idle to the computer)
                    long d2 = D2(u, g);
                    if (d2 > (long)ZWorld.CpuGunSendPx * ZWorld.CpuGunSendPx || d2 > bd || (d2 == bd && best != null && u.Id > best.Id)) continue;
                    if (!GunReaches(u, g) || LastDefender(u)) continue;
                    best = u; bd = d2;
                }
                if (best != null && GunOrder(best, g, GunManKind.Frontier, bd, units, targeted, targets)) budget--;
            }
        }

        /// <summary>The frontier pass has its turn: after the opening (the first CpuGunFrontierFromSec seconds are the rush for flags and cars), and on the
        /// car park map only once this side has CpuGunCarsFirst vehicles, the ones its squads walk to included (below that the squads go on filling cars: DV37). The way-past pass has no such wait.</summary>
        bool GunFrontierTime()
        {
            if (W.Time + 1e-6 < ZWorld.CpuGunFrontierFromSec) return false;
            if (!CarParkDrive) return true;
            int cars = 0;
            foreach (var o in W.Objects)
            {
                if (o.Owner != Team || o.Destroyed) continue;
                if (o.Kind == ZKind.Vehicle) { if (!o.IsShuttle) cars++; continue; }
                if (o.Kind != ZKind.Robot || o.IsMinion || Gone(o)) continue;
                var wps = KnownWaypoints(o);
                if (wps.Count == 0 || wps[0].mode != WpMode.Enter) continue;
                var v = BotObject(wps[0].refId);
                if (v != null && v.Kind == ZKind.Vehicle) cars++;                                 // a squad on its way to a vehicle counts as that vehicle
            }
            return cars >= ZWorld.CpuGunCarsFirst;
        }

        /// <summary>One squad per gun also against the engine's own matching: its second pass of a round runs over every target, the targeted
        /// ones too (zbot.cpp:161-169, Stage1AI_3), so it could send a second squad to a gun this rule's squad is walking to. Such a gun leaves
        /// the round's targets while that squad's known first order is still the ENTER of it. Only guns this rule sent a squad to: before the
        /// rule's first order the list is empty and the round is the engine's own.</summary>
        void KeepRuleGunsSingle(List<ZObject> targets)
        {
            for (int i = gmSent.Count - 1; i >= 0; i--)
            {
                var (uid, gid) = gmSent[i];
                var u = W.Get(uid);
                var wps = u != null && !Gone(u) ? KnownWaypoints(u) : null;
                if (wps == null || wps.Count == 0 || wps[0].mode != WpMode.Enter || wps[0].refId != gid) { gmSent.RemoveAt(i); continue; }
                var g = W.Get(gid);
                if (g != null) targets.Remove(g);
            }
        }

        static long D2(ZObject a, ZObject b) { long dx = a.CenterX - b.CenterX, dy = a.CenterY - b.CenterY; return dx * dx + dy * dy; }

        /// <summary>The robot squad can walk to the gun: the engine's same-region check on the bot's own regions (MatchTargets_3).</summary>
        bool GunReaches(ZObject u, ZObject g)
        {
            TargetPoint(g, out int tx, out int ty);
            return InSameRegion(u.LocX + 8, u.LocY + 8, tx, ty, true);
        }

        /// <summary>A squad leader of ours on its way to something that is neither a fight nor a flag (an empty vehicle or gun, a grenade
        /// crate, a plain move), not firing now, not held by another OpenZ rule of the computer. Public for the counted check.</summary>
        public bool GunPasser(ZObject o)
        {
            if (o == null || o.Kind != ZKind.Robot || o.Owner != Team || o.IsMinion || Gone(o) || o.Destroyed) return false;
            if (o.NoAutoEnter || o.StrikeHeld || o.CpuDefendHeld || o.CpuRepairHeld || o.IsFortDefender || o.HoldPosition) return false;
            if (o.AttackObject != null) return false;                                           // fighting now
            var wps = KnownWaypoints(o);
            if (wps.Count == 0 || !o.IsMoving) return false;                                    // not on its way (an idle squad is the frontier pass's)
            var w = wps[0];
            switch (w.mode)
            {
                case WpMode.Enter: { var t = BotObject(w.refId); return t == null || t.Kind != ZKind.Cannon; }   // already going to a gun: stays
                case WpMode.PickupGrenades: return true;
                case WpMode.Move:
                {
                    var t = BotObject(w.refId);
                    return t == null || !(t.Kind == ZKind.MapItem && t.Type == "flag" && t.Owner != Team);  // never off the way to a flag
                }
                default: return false;                                                          // attack, fort entry, agro, dodge, repair, crane ...
            }
        }

        /// <summary>The squad is the last unit of ours at a flag or fort of ours (within CpuGunGuardPx px, nobody else of ours there). Uses the
        /// lists GunManRound filled this round.</summary>
        bool LastDefender(ZObject u)
        {
            long r2 = (long)ZWorld.CpuGunGuardPx * ZWorld.CpuGunGuardPx;
            foreach (var p in gmPosts)
            {
                if (D2(u, p) > r2) continue;
                bool other = false;
                foreach (var o in gmMine) if (o != u && D2(o, p) <= r2) { other = true; break; }
                if (!other) { GunManKeptDefenders++; return true; }
            }
            return false;
        }

        /// <summary>Squads of ours whose known first order is to enter an empty gun.</summary>
        int GunsEnRoute()
        {
            int n = 0;
            foreach (var o in W.Objects)
            {
                if (o.Kind != ZKind.Robot || o.Owner != Team || o.IsMinion || Gone(o)) continue;
                var wps = KnownWaypoints(o);
                if (wps.Count == 0 || wps[0].mode != WpMode.Enter) continue;
                var t = BotObject(wps[0].refId);
                if (t != null && t.Kind == ZKind.Cannon && t.Owner == Team.Neutral) n++;
            }
            return n;
        }

        /// <summary>The frontier zones of ours now (zone list order): ours, touching a zone that is not ours (see CpuGunFrontierNeutral).</summary>
        public void GunFrontierZones(List<int> res)
        {
            res.Clear();
            for (int i = 0; i < W.Zones.Count; i++)
            {
                if (W.Zones[i].Owner != Team) continue;
                foreach (int j in W.CpuDefendNeighbours(i))
                {
                    var o = W.Zones[j].Owner;
                    if (o == Team || (!ZWorld.CpuGunFrontierNeutral && o == Team.Neutral)) continue;
                    res.Add(i); break;
                }
            }
        }

        /// <summary>The gun stands in one of the frontier zones (a list from GunFrontierZones) or within CpuGunFrontierPx px of one. Public for the check.</summary>
        public bool GunNearFrontier(ZObject g, IReadOnlyList<int> zones)
        {
            int x = g.CenterX, y = g.CenterY, m = ZWorld.CpuGunFrontierPx;
            foreach (int i in zones)
            {
                var z = W.Zones[i];
                if (x >= z.X - m && x <= z.X + z.W + m && y >= z.Y - m && y <= z.Y + z.H + m) return true;
            }
            return false;
        }

        /// <summary>The Zod ENTER order for the squad through the computer's own order path; true = given (the squad leaves the round, the
        /// gun is targeted).</summary>
        bool GunOrder(ZObject u, ZObject g, GunManKind kind, long d2, List<ZObject> units, List<ZObject> targeted, List<ZObject> targets)
        {
            var wps = KnownWaypoints(u);
            var ev = new GunManEvent { time = W.Time, kind = kind, unitId = u.Id, gunId = g.Id, dist = (int)Math.Round(Math.Sqrt(d2)), hadOrder = wps.Count > 0, fighting = u.AttackObject != null };
            if (wps.Count > 0)
            {
                ev.fromMode = wps[0].mode;
                var t0 = BotObject(wps[0].refId);
                ev.fromFlag = t0 != null && t0.Kind == ZKind.MapItem && t0.Type == "flag" && t0.Owner != Team;
            }
            int before = Orders;
            Command(u, g);
            if (Orders <= before) return false;
            units.Remove(u);
            if (!targeted.Contains(g)) targeted.Add(g);
            targets.Remove(g);                                                                     // one squad per gun: the engine's own passes of this round never send a second one
            gmSent.Add((u.Id, g.Id));
            if (kind == GunManKind.Passing) GunManPassing++; else GunManFrontier++;
            if (GunManEvents.Count < 2000) GunManEvents.Add(ev);
            Log.Add($"{W.Time:F0}s {Team} DV157 man gun ({kind}): {u.Type}#{u.Id} -> {g.Type}#{g.Id} at {ev.dist} px");
            return true;
        }
    }
}
