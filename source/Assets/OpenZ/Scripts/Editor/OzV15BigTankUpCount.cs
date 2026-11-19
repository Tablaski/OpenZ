using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit bizon_mammoth_driver_up_r7: the counted check of DV211 "the big tanks' driver is up while they fight" (Sim/ZObject.BigTankUp.cs).
    /// Headless: the simulation only (ZWorld.Create + Tick), no scene, no Play mode, nothing saved. The fight's switches are set here (the
    /// game's for this fight: the big tanks, the hatch driver's gun and spots, driver sniping, the sniper rules) and put back by the caller's
    /// OzV11Kit.GameSwitches(). Every verdict line is counted:
    ///  A. the up share: scripted fights of a blue Bizon / Mammoth (its driver and hull cannot die) that drives on an attack-move order through
    ///     four red sniper squads (in range, firing, moving between targets), DV211 off (today) and on, one fight per seed. "Fighting" is the
    ///     lead's: it has a target (or drives in to one in its agro reach on an attack / agro order), or it fired in the last 8 s. "Up" = its
    ///     driver can be sniped (ZObject.CanBeSniped: the open lid, or DV75's rest). Needs: with DV211 every fight 85 % or more.
    ///  B. (Check only) the same fights with a driver that can be shot out and the real hull: how often and how soon the snipers empty it,
    ///     their shots at the tank while the driver was up, the driver hits, and the hatch driver's own shots (DV87: he fires while up).
    ///  C. after the fight: the lid shuts 8 s after the last fighting tick, stays shut, and DV75's rest comes 25-33 s after the calm as before.
    ///  D. a bridge (DV87 part B, kit b1): fighting on the deck the lid stays shut every tick; on the road beside it (control) it is up.
    ///  F. every other vehicle: a heavy and a light tank in the same fight: lid ticks and world hash the same with DV211 on and off.
    ///  G. deterministic: the same fight twice with DV211 on gives the same hash. With DV211 off the rule never holds a lid (part A).
    /// </summary>
    public static class OzV15BigTankUpCount
    {
        public const int Runs = 20, QuickRuns = 4;
        public const double FightSeconds = 60, MinShare = 0.85;
        const int Big = 1000000000;
        static readonly string[] Tanks = { ZBigTanks.Bizon, ZBigTanks.Mammoth };
        static string Name(string t) => t == ZBigTanks.Bizon ? "BIZON" : t == ZBigTanks.Mammoth ? "MAMMOTH" : t;
        static string F1(double v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        static string P0(double v) => (100.0 * v).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " %";

        /// <summary>The game's switches this fight touches (View/ZGame.cs: on at every start with default OPTIONS), set by the check itself.</summary>
        public static readonly string[] FightSwitches = {
            "MammothDeviation", "BizonDeviation", "DriverFireDeviation", "SnipeDeviation", "SnipeSafeSpotDeviation", "SniperBalanceDeviation",
            "SniperHeadshotDeviation", "SniperSteadyDeviation", "ProneTargetDeviation", "ExposedDriverDeviation", "RiflePairDeviation",
            "HeavyPairDeviation", "VeteranDeviation", "UnmannedStopDeviation", "EmptiedTargetsDeviation", "DuelDeviation", "HelpDeviation",
            "PremiumDodgeDeviation", "RocketSpeedDeviation", "LosDeviation", "ClearanceDeviation" };

        static void Switches() { foreach (var s in FightSwitches) OzV11Kit.TrySet(typeof(ZWorld), s, true); }

        // the open ground of the scripted fight (px): the tank's start, its attack-move goal, the four sniper squads on its way
        const int TX = 320, TY = 640, GX = 1240;
        static readonly (int x, int y)[] SquadsAt = { (500, 640), (600, 470), (760, 790), (940, 600) };

        public sealed class Run
        {
            public string tank; public bool on, canDie; public int seed;
            public double fightSec, upFightSec, upSec, firstFight = -1, snipedAt = -1, endAt, downNoTarget, downTarget;
            public int sniperShots, sniperShotsUp, sniperHits, driverHits, driverShots, gunShots, killed, squads;
            public bool sniped, heldEver; public ulong hash;
            public double Share => fightSec > 0 ? upFightSec / fightSec : 0;
        }

        static bool Up(ZObject t) => t != null && !t.Destroyed && t.Owner != Team.Neutral && t.Drivers.Count > 0 && t.CanBeSniped();

        static ZWorld NewWorld(string settings, string map, int seed)
        {
            var w = OzV11Kit.World(settings, map, seed);
            OzV11Kit.Dummy(w, 60, 1480);                                                                      // blue: the game is never over
            var d = w.CreateRobotGroup("grunt", 1432, 1472, Team.Red, 1); d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = Big;   // red, the same
            return w;
        }

        static ZObject Tank(ZWorld w, string type, int cx, int cy, bool canDie)
        {
            var t = OzV11Kit.Place(w, type, cx, cy, Team.Blue);
            if (!canDie) { t.Drivers[0] = Big; t.MaxHealth = t.Health = Big; }
            return t;
        }

        static ulong Mix(ulong h, long v) { unchecked { h ^= (ulong)v; h *= 1099511628211UL; } return h; }

        /// <summary>One scripted fight (part A / B): the tank on an attack-move order through the four sniper squads.</summary>
        public static Run Fight(string settings, int seed, string type, bool on, bool canDie, Func<ZWorld, ZObject> extra = null, Action<ZWorld, ZObject, ZObject> perTick = null)
        {
            var r = new Run { tank = type, on = on, canDie = canDie, seed = seed };
            bool was = ZWorld.BigTankUpDeviation;
            ZWorld.BigTankUpDeviation = on;
            try
            {
                var w = NewWorld(settings, OzV11Kit.Plain(96, 96), seed);
                var t = Tank(w, type, TX, TY, canDie);
                var squads = SquadsAt.Select(p => OzV11Kit.Place(w, "sniper", p.x, p.y, Team.Red)).ToList();
                var reds = squads.SelectMany(OzV11Kit.Squad).ToList(); r.squads = squads.Count;
                var other = extra?.Invoke(w);
                w.SendWaypoints(Team.Blue, t, new[] { ZWaypoint.Move(GX, TY, true) });
                double lastShot = -100; ulong h = 1469598103934665603UL;
                while (w.Time < 150)
                {
                    bool upBefore = Up(t);
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        if (e.type == ZEventType.Hitscan && e.otherId == t.Id)
                        {
                            var a = w.Get(e.objId);
                            if (a != null && a.Type == "sniper") { r.sniperShots++; if (upBefore) r.sniperShotsUp++; if (e.flag) r.sniperHits++; if (e.flag2) r.driverHits++; }
                        }
                        else if (e.type == ZEventType.DriverShot && e.objId == t.Id) r.driverShots++;
                        else if (e.type == ZEventType.MissileFired && e.objId == t.Id) { r.gunShots++; lastShot = w.Time; }
                        else if (e.type == ZEventType.Sniped && e.objId == t.Id && !r.sniped) { r.sniped = true; r.snipedAt = r.firstFight >= 0 ? w.Time - r.firstFight : 0; }
                    }
                    w.Events.Clear();
                    perTick?.Invoke(w, t, other);
                    bool owned = !t.Destroyed && t.Owner == Team.Blue && t.Drivers.Count > 0, up = Up(t);
                    bool fight = owned && (t.BigTankFighting() || w.Time - lastShot < ZWorld.BigTankUpLingerSeconds);
                    if (t.BigTankUpHeld) r.heldEver = true;
                    if (fight)
                    {
                        if (r.firstFight < 0) r.firstFight = w.Time;
                        r.fightSec += ZConst.TICK; if (up) r.upFightSec += ZConst.TICK;
                        else if (t.AttackObject == null) r.downNoTarget += ZConst.TICK; else r.downTarget += ZConst.TICK;   // why he was down: no target (the drive in, between targets) or a target with the lid shut
                    }
                    if (up) r.upSec += ZConst.TICK;
                    h = Mix(h, t.LidOpen ? 1 : 0);
                    if (other != null) h = Mix(h, other.LidOpen ? 3 : 2);
                    if (r.sniped || t.Destroyed) break;
                    if (r.firstFight >= 0 && w.Time - r.firstFight >= FightSeconds) break;
                    if (r.firstFight >= 0 && !fight && reds.All(m => !OzV11Kit.Alive(m))) break;           // every sniper down and the fight's 8 s over
                }
                r.killed = reds.Count(m => !OzV11Kit.Alive(m)); r.endAt = w.Time;
                r.hash = w.RulesFingerprint(h);
            }
            finally { ZWorld.BigTankUpDeviation = was; }
            return r;
        }

        sealed class Sum
        {
            public readonly List<Run> runs = new List<Run>();
            public double Fight => runs.Sum(x => x.fightSec); public double UpF => runs.Sum(x => x.upFightSec);
            public double Share => Fight > 0 ? UpF / Fight : 0;
            public double Min => runs.Count == 0 ? 0 : runs.Min(x => x.Share); public double Max => runs.Count == 0 ? 0 : runs.Max(x => x.Share);
        }

        // ------------------------------------------------------------------ the whole check / the parity row's probe
        public static string Check(string settings) => Count(settings, Runs, true);
        public static string Quick(string settings) => Count(settings, QuickRuns, false);

        static string Count(string settings, int runs, bool full)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# DV211 the big tanks' driver up while they fight, counted ({(full ? "the whole check" : "the parity row's probe")}; {runs} fights per tank and setting)");
            bool was = ZWorld.BigTankUpDeviation;
            int opens0 = ZWorld.BigTankUpOpens, downs0 = ZWorld.BigTankUpDowns;
            try
            {
                Switches();
                ZWorld.BigTankUpDeviation = false;
                PartA(sb, settings, runs, opens0, downs0);
                if (full) PartB(sb, settings, runs);
                PartC(sb, settings);
                PartD(sb, settings);
                PartE(sb, settings);
                PartF(sb, settings);
                PartG(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace)); }
            finally { ZWorld.BigTankUpDeviation = was; }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ A. the up share
        static void PartA(StringBuilder sb, string settings, int runs, int opens0, int downs0)
        {
            sb.AppendLine($"A. the up share: a blue big tank (driver and hull cannot die) on an attack-move order through four red sniper squads; fighting = a target, the drive in to one, or a shot in the last {ZWorld.BigTankUpLingerSeconds:0} s; up = its driver can be sniped");
            sb.AppendLine("tank | DV211 | fights | seconds fighting | seconds up while fighting | share | per fight min..max | seconds up per minute of fighting | down: no target (driving in, between targets) | down: a target, the lid shut | tank shots | hatch driver shots | snipers down");
            var sums = new Dictionary<(string, bool), Sum>();
            foreach (var type in Tanks)
                foreach (bool on in new[] { false, true })
                {
                    var s = new Sum();
                    for (int seed = 1; seed <= runs; seed++) s.runs.Add(Fight(settings, seed, type, on, false));
                    sums[(type, on)] = s;
                    sb.AppendLine($"{Name(type)} | {(on ? "on" : "off")} | {s.runs.Count} | {F1(s.Fight)} | {F1(s.UpF)} | {P0(s.Share)} | {P0(s.Min)}..{P0(s.Max)} | {F1(60 * s.Share)} | {F1(s.runs.Sum(x => x.downNoTarget))} s | {F1(s.runs.Sum(x => x.downTarget))} s | {s.runs.Sum(x => x.gunShots)} | {s.runs.Sum(x => x.driverShots)} | {s.runs.Sum(x => x.killed)} of {s.runs.Sum(x => x.squads * 3)}");
                    if (!on) { opens0 = ZWorld.BigTankUpOpens; downs0 = ZWorld.BigTankUpDowns; }
                    if (!on) sb.AppendLine(OzV11Kit.Verdict(s.runs.All(x => !x.heldEver) && ZWorld.BigTankUpOpens == opens0, $"{Name(type)}, DV211 off: the rule never held a lid (Zod's lid alone)"));
                }
            foreach (var type in Tanks)
            {
                var off = sums[(type, false)]; var on = sums[(type, true)];
                sb.AppendLine($"NOTE  {Name(type)} today (DV211 off): its driver is up {F1(60 * off.Share)} s of a minute of fighting ({P0(off.Share)}; {F1(off.UpF)} of {F1(off.Fight)} s in {off.runs.Count} fights)");
                sb.AppendLine(OzV11Kit.Verdict(on.runs.All(x => x.fightSec > 5 && x.Share >= MinShare) && on.Share > off.Share,
                    $"{Name(type)} with DV211: up {F1(60 * on.Share)} s of a minute of fighting ({P0(on.Share)}, every fight {P0(on.Min)} or more; needs {P0(MinShare)} or more in every fight, and more than today's {P0(off.Share)})"));
            }
        }

        // ------------------------------------------------------------------ B. the snipers against a driver that can be shot out
        static void PartB(StringBuilder sb, string settings, int runs)
        {
            sb.AppendLine($"B. the same fights, the driver can be shot out (his own pool) and the hull is the tank's own: {runs} fights per tank and setting, {FightSeconds:0} s at most from the first fighting tick");
            sb.AppendLine("tank | DV211 | sniped (fights) | mean seconds to the snipe | sniper shots at the tank | of them while the driver was up | sniper hits | driver hits | snipers down | tank shots | hatch driver shots");
            var res = new Dictionary<(string, bool), List<Run>>();
            foreach (var type in Tanks)
                foreach (bool on in new[] { false, true })
                {
                    var l = new List<Run>();
                    for (int seed = 1; seed <= runs; seed++) l.Add(Fight(settings, 100 + seed, type, on, true));
                    res[(type, on)] = l;
                    var sn = l.Where(x => x.sniped).ToList();
                    sb.AppendLine($"{Name(type)} | {(on ? "on" : "off")} | {sn.Count} of {l.Count} | {(sn.Count > 0 ? F1(sn.Average(x => x.snipedAt)) : "-")} | {l.Sum(x => x.sniperShots)} | {l.Sum(x => x.sniperShotsUp)} | {l.Sum(x => x.sniperHits)} | {l.Sum(x => x.driverHits)} | {l.Sum(x => x.killed)} of {l.Sum(x => x.squads * 3)} | {l.Sum(x => x.gunShots)} | {l.Sum(x => x.driverShots)}");
                }
            foreach (var type in Tanks)
            {
                var off = res[(type, false)]; var on = res[(type, true)];
                int a = off.Count(x => x.sniped), b = on.Count(x => x.sniped);
                sb.AppendLine(OzV11Kit.Verdict(b >= a && on.Sum(x => x.driverHits) >= off.Sum(x => x.driverHits),
                    $"{Name(type)}: easier to snipe with DV211: its driver shot out in {b} of {on.Count} fights (today {a} of {off.Count}), driver hits {on.Sum(x => x.driverHits)} (today {off.Sum(x => x.driverHits)})"));
            }
        }

        // ------------------------------------------------------------------ C. after the fight
        static void PartC(StringBuilder sb, string settings)
        {
            foreach (var type in Tanks)
            {
                bool was = ZWorld.BigTankUpDeviation; ZWorld.BigTankUpDeviation = true;
                try
                {
                    var w = NewWorld(settings, OzV11Kit.Plain(96, 96), 7);
                    var t = Tank(w, type, TX, TY, false); t.MoveSpeed = 0; t.RealMoveSpeed = 0;
                    var sq = OzV11Kit.Place(w, "sniper", TX + 100, TY, Team.Red);
                    var reds = OzV11Kit.Squad(sq).ToList();
                    double lastFight = -1, shutAt = -1, restAt = -1, calmFrom = -1; bool reopened = false, upAll = true; int fightTicks = 0;
                    while (w.Time < 120 && restAt < 0)
                    {
                        w.Tick(ZConst.TICK); w.Events.Clear();
                        bool fight = t.BigTankFighting();
                        if (fight) { lastFight = w.Time; fightTicks++; if (!t.LidOpen) upAll = false; }
                        if (t.AttackObject != null || t.Waypoints.Count > 0 || t.IsMoving) calmFrom = w.Time;
                        if (lastFight >= 0 && !fight && reds.All(m => !OzV11Kit.Alive(m)))
                        {
                            if (shutAt < 0 && !t.LidOpen) shutAt = w.Time;
                            else if (shutAt >= 0 && t.LidOpen && !t.Resting) reopened = true;
                        }
                        if (t.Resting && restAt < 0) restAt = w.Time;
                    }
                    double after = shutAt - lastFight;
                    sb.AppendLine(OzV11Kit.Verdict(fightTicks > 0 && upAll && shutAt > 0 && Math.Abs(after - ZWorld.BigTankUpLingerSeconds) <= ZConst.TICK + 1e-6 && !reopened,
                        $"C {Name(type)}: up every tick of its fight ({fightTicks} ticks), the lid shut {F1(after)} s after the last fighting tick (needs {ZWorld.BigTankUpLingerSeconds:0} s), and it stayed shut"));
                    double rest = restAt - calmFrom;
                    sb.AppendLine(OzV11Kit.Verdict(restAt > 0 && rest >= ZWorld.IdleRestSeconds - 0.02 && rest <= ZWorld.IdleRestSeconds + ZWorld.IdleRestSpreadSeconds + 0.02,
                        $"C {Name(type)}: DV75's rest as before: the crew rests {F1(rest)} s after the calm began (DV75: {ZWorld.IdleRestSeconds:0}-{ZWorld.IdleRestSeconds + ZWorld.IdleRestSpreadSeconds:0} s)"));
                }
                finally { ZWorld.BigTankUpDeviation = was; }
            }
        }

        // ------------------------------------------------------------------ D. a bridge (DV87 part B)
        static void PartD(StringBuilder sb, string settings)
        {
            string map = OzV11Kit.Plain(96, 96, ("building:bridge_vert", 20, 20, "null"));
            foreach (var type in Tanks)
            {
                bool was = ZWorld.BigTankUpDeviation; ZWorld.BigTankUpDeviation = true;
                try
                {
                    var res = new List<string>(); bool ok = true;
                    foreach (bool deck in new[] { true, false })
                    {
                        var w = NewWorld(settings, map, 3);
                        var br = w.Objects.First(o => o.IsBridge);
                        int bx = br.LocX + br.WidthPix / 2, y = deck ? br.LocY + br.HeightPix / 2 : br.LocY - 176;
                        var t = Tank(w, type, bx, y, false); t.MoveSpeed = 0; t.RealMoveSpeed = 0;
                        var sq = OzV11Kit.Place(w, "grunt", bx + 92, y, Team.Red);
                        foreach (var m in OzV11Kit.Squad(sq)) { m.MaxHealth = m.Health = Big; m.Damage = 0; m.MoveSpeed = 0; m.RealMoveSpeed = 0; }
                        int fight = 0, up = 0;
                        while (w.Time < 12) { w.Tick(ZConst.TICK); w.Events.Clear(); if (t.BigTankFighting()) { fight++; if (t.LidOpen) up++; } }
                        bool safe = w.SnipeSafeSpot(t);
                        if (deck) ok &= safe && fight > 500 && up == 0; else ok &= !safe && fight > 500 && up >= fight - 1;
                        res.Add($"{(deck ? "on the deck" : "on the road 176 px before it")}: {(safe ? "a protected spot" : "open ground")}, up {up} of {fight} fighting ticks");
                    }
                    sb.AppendLine(OzV11Kit.Verdict(ok, $"D {Name(type)}: no driver up on a bridge (DV87 part B as before): " + string.Join("; ", res)));
                }
                finally { ZWorld.BigTankUpDeviation = was; }
            }
        }

        // ------------------------------------------------------------------ E. a veteran crew
        static void PartE(StringBuilder sb, string settings)
        {
            foreach (var type in Tanks)
            {
                bool vetAll = true;
                Action<ZWorld, ZObject, ZObject> vet = (w, t, o) => { if (!t.Veteran) t.Veteran = true; };   // a veteran crew from the first tick (long before the fight)
                var off = Fight(settings, 5, type, false, false, null, vet);
                var on = Fight(settings, 5, type, true, false, null, (w, t, o) => { vet(w, t, o); if (!t.VeteranActive) vetAll = false; });
                sb.AppendLine(OzV11Kit.Verdict(vetAll && on.heldEver && on.fightSec > 5 && on.Share >= MinShare && on.driverShots == 0,
                    $"E {Name(type)}, a veteran crew (DV12): with DV211 up {F1(on.upFightSec)} of {F1(on.fightSec)} s fighting ({P0(on.Share)}; today Zod's lid: {F1(off.upFightSec)} of {F1(off.fightSec)} s), " +
                    $"his hatch gun silent as for every veteran (DV87: {on.driverShots} driver shots), DV12's snipe factor on him untouched (driver hits {on.driverHits} of {on.sniperShotsUp} sniper shots while up)"));
            }
        }

        // ------------------------------------------------------------------ F. every other vehicle
        static void PartF(StringBuilder sb, string settings)
        {
            foreach (var other in new[] { "heavy", "light", ZMTank.Type })
            {
                var a = Fight(settings, 9, other, false, false); var b = Fight(settings, 9, other, true, false);
                sb.AppendLine(OzV11Kit.Verdict(a.hash == b.hash && !b.heldEver && a.fightSec > 5,
                    $"F the {(other == ZMTank.Type ? "medium" : other)} tank in the same fight: lid ticks and world hash the same with DV211 on and off (up {F1(a.upFightSec)} of {F1(a.fightSec)} s fighting both times; the rule is for the Mammoth and the Bizon only)"));
            }
        }

        // ------------------------------------------------------------------ G. deterministic, H. off
        static void PartG(StringBuilder sb, string settings)
        {
            foreach (var type in Tanks)
            {
                var a = Fight(settings, 13, type, true, true); var b = Fight(settings, 13, type, true, true);
                sb.AppendLine(OzV11Kit.Verdict(a.hash == b.hash && a.sniped == b.sniped && Math.Abs(a.upFightSec - b.upFightSec) < 1e-9,
                    $"G {Name(type)}: deterministic: the same fight twice with DV211 on gives the same world hash ({a.hash:x16}), the same up time and the same snipe"));
            }
        }
    }
}
