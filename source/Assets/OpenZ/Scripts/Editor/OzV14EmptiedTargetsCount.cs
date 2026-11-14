using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted rows, headless on the Sim alone, nothing saved:
    /// menu OpenZ/V1.4/Vehicle Exit Gun: Emptied Targets Count (headless, DV176), the DV176 row's probe (Quick, Editor/OpenZParity.V14EmptiedTargets.cs).
    /// The counted checks' game switches (OzV11Kit.GameSwitches) with DV171 on; a plain 64 x 40-tile map, red the player, blue a CPU that gives
    /// no orders of its own (its units get the computer's own attack order, ZBot.Command's waypoint). Every row needs its samples.
    ///  E1  EXIT: a red light tank (its grunt driver in by the player's ENTER, the hull made very tough so it outlives the row), an enemy jeep and
    ///      an enemy grunt squad on the computer's attack order on it, a fourth enemy grunt with an attack on it QUEUED behind a long move; once
    ///      the jeep and the squad have both fired at it, EXIT DRIVER (the player's, between two ticks): right after it no unit of any team has
    ///      the tank as its target or an attack / agro waypoint on it (first or queued); then 10 s: shots STARTED at the tank by those enemies
    ///      (their Hitscan / MissileFired events on it) = 0; what they do instead is noted.
    ///  E1z the same scene with the rule off: the shots at the empty tank after the EXIT (INFO: the fault the rule removes).
    ///  E2  CREW SHOT OUT: the same, its driver at 1 health, three enemy sniper squads on the computer's attack order on it (the seeds 3 .. 14 in
    ///      turn until a snipe lands within 60 s); right after the snipe's tick: no target and no attack / agro waypoint on it in any unit (the
    ///      queued one of the computer's grunt included: Zod and DV81 kept it); then 10 s: shots started at it = 0.
    ///  E2z the same with the rule off (INFO: the computer's queued attack kept).
    ///  E3  after E1's 10 s: a red grunt's explicit attack order on the empty tank (DV81's Ctrl attack) is carried out (it fires at it).
    ///  E4  E1 twice on the same seed: the same events, bit for bit (the rule is deterministic).
    /// </summary>
    public static class OzV14EmptiedTargetsCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        const int GroundW = 64, GroundH = 40;
        static readonly int[] Seeds = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
        static string groundMap;

        sealed class Scene { public ZWorld w; public int ox, oy; public ZObject v, jeep, squad, queued; public readonly List<ZObject> foes = new List<ZObject>(); public ulong hash = 1469598103934665603UL; }

        static Scene New(string set, int seed)
        {
            var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
            w.Events.Clear();
            var s = new Scene { w = w, ox = GroundW * 8, oy = GroundH * 8 };
            Tick(s, 2, null);
            return s;
        }
        static void Tick(Scene s, int n, Action<ZEvent> each)
        {
            for (int i = 0; i < n; i++)
            {
                s.w.Tick(ZConst.TICK);
                foreach (var e in s.w.Events) { Hash(s, e); each?.Invoke(e); }
                s.w.Events.Clear();
            }
        }
        static void Hash(Scene s, ZEvent e)
        {
            unchecked
            {
                ulong h = s.hash;
                foreach (long x in new long[] { (long)e.type, e.objId, e.otherId, e.x, e.y }) { h ^= (ulong)x; h *= 1099511628211UL; }
                s.hash = h;
            }
        }
        static ZObject Robot(Scene s, string type, int cx, int cy, Team t, int n = 1) => s.w.CreateRobotGroup(type, cx - 8, cy - 8, t, n);
        static bool ShotAt(ZEvent e, ZObject v) => e.otherId == v.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired);

        /// <summary>The red light tank with its grunt driver (the player's ENTER), very tough (it must outlive the row).</summary>
        static ZObject Crewed(Scene s, int x, int y)
        {
            var v = s.w.CreateObject("vehicle", "light", x - 16, y - 16, Team.Neutral);
            var d = Robot(s, "grunt", x - 64, y, Me);
            OzV11Kit.PlayerEnter(s.w, d, v);
            for (int i = 0; i < 2000 && !(v.Owner == Me && v.Drivers.Count > 0); i++) Tick(s, 1, null);
            v.MaxHealth = v.Health = 1000000000;
            return v;
        }
        static void Tough(ZObject o) { foreach (var m in OzV11Kit.Squad(o)) { m.MaxHealth = m.Health = 1000000000; } }

        /// <summary>The enemies on the computer's attack order on v: a jeep, a grunt squad, and a grunt with the attack queued behind a long move.</summary>
        static void Foes(Scene s)
        {
            var v = s.v;
            s.jeep = s.w.CreateObject("vehicle", "jeep", s.ox + 140 - 16, s.oy - 16, Cpu); Tough(s.jeep);
            s.squad = Robot(s, "grunt", s.ox + 90, s.oy + 70, Cpu, 3); Tough(s.squad);
            s.queued = Robot(s, "grunt", s.ox + 200, s.oy - 120, Cpu); Tough(s.queued);
            OzV11Kit.CpuAttack(s.w, s.jeep, v); OzV11Kit.CpuAttack(s.w, s.squad, v);
            s.w.SendWaypoints(Cpu, s.queued, new[] { ZWaypoint.Move(s.ox + 420, s.oy - 260, false), new ZWaypoint { mode = WpMode.Attack, refId = v.Id, attackTo = true, playerGiven = true, x = v.LocX + 8, y = v.LocY + 8 } });
            s.foes.Clear(); s.foes.Add(s.jeep); s.foes.AddRange(OzV11Kit.Squad(s.squad)); s.foes.Add(s.queued);
        }

        /// <summary>Units of any team that still hold v as their target or have an attack / agro waypoint on it (first, queued).</summary>
        static void Holders(ZWorld w, ZObject v, out int targets, out int first, out int queued, out string who)
        {
            targets = first = queued = 0; var names = new List<string>();
            foreach (var o in w.Objects)
            {
                if (o == v || o.Destroyed) continue;
                bool t = o.AttackObject == v; if (t) targets++;
                for (int i = 0; i < o.Waypoints.Count; i++)
                {
                    var q = o.Waypoints[i];
                    if ((q.mode == WpMode.Attack || q.mode == WpMode.Agro) && q.refId == v.Id) { if (i == 0) first++; else queued++; t = true; }
                }
                if (t) names.Add($"{o.Type} #{o.Id} {o.Owner}");
            }
            who = names.Count == 0 ? "none" : string.Join(", ", names);
        }

        static string Doing(Scene s)
        {
            var l = new List<string>();
            foreach (var f in s.foes)
                l.Add($"{f.Type} #{f.Id}: {(f.AttackObject != null ? $"target {f.AttackObject.Type} #{f.AttackObject.Id} {f.AttackObject.Owner}" : "no target")}, {f.Waypoints.Count} waypoint(s){(f.Waypoints.Count > 0 ? " (" + f.Waypoints[0].mode + ")" : "")}");
            return string.Join("; ", l);
        }

        sealed class Run { public bool exited; public int before, beforeJeep, beforeSquad, after, targets = -1, first = -1, queued = -1, ticks; public string who = "", doing = "", note = ""; public ulong hash; public Scene s; }

        /// <summary>E1: the EXIT while a jeep and a squad shoot the tank; rule = DV176 on / off for this run.</summary>
        static Run Exit(string set, int seed, bool rule)
        {
            var r = new Run(); bool was = ZWorld.EmptiedTargetsDeviation; ZWorld.EmptiedTargetsDeviation = rule;
            try
            {
                var s = New(set, seed); r.s = s;
                s.v = Crewed(s, s.ox - 120, s.oy);
                if (s.v.Owner != Me) { r.note = "the driver did NOT get in"; return r; }
                Foes(s);
                for (int i = 0; i < 6000 && (r.beforeJeep == 0 || r.beforeSquad == 0); i++)
                    Tick(s, 1, e => { if (!ShotAt(e, s.v)) return; r.before++; if (e.objId == s.jeep.Id) r.beforeJeep++; else if (s.foes.Exists(f => f.Id == e.objId)) r.beforeSquad++; });
                if (r.beforeJeep == 0 || r.beforeSquad == 0) { r.note = $"the jeep fired {r.beforeJeep}, the squad {r.beforeSquad} times at the tank in 60 s: no EXIT"; return r; }
                r.exited = s.w.Eject(Me, s.v); s.w.Events.Clear();
                if (!r.exited) { r.note = "the EXIT was refused: " + (s.v.CanExitDriver(out string why) ? "?" : why); return r; }
                Holders(s.w, s.v, out r.targets, out r.first, out r.queued, out r.who);
                int n = (int)Math.Round(10.0 / ZConst.TICK);
                for (int i = 0; i < n; i++) { Tick(s, 1, e => { if (ShotAt(e, s.v) && s.foes.Exists(f => f.Id == e.objId)) r.after++; }); r.ticks++; }
                r.doing = Doing(s); r.hash = s.hash;
                return r;
            }
            finally { ZWorld.EmptiedTargetsDeviation = was; }
        }

        /// <summary>E2: the crew SHOT OUT by enemy snipers while the jeep, the squad and the queued grunt are on it.</summary>
        static Run ShotOut(string set, bool rule)
        {
            var r = new Run(); bool was = ZWorld.EmptiedTargetsDeviation; ZWorld.EmptiedTargetsDeviation = rule;
            try
            {
                foreach (int seed in Seeds)
                {
                    var s = New(set, seed); r.s = s;
                    s.v = Crewed(s, s.ox - 120, s.oy);
                    if (s.v.Owner != Me) { r.note = "the driver did NOT get in"; continue; }
                    s.v.Drivers[0] = 1;
                    Foes(s);
                    for (int k = 0; k < 3; k++) { var sn = Robot(s, "sniper", s.ox + 60 + 40 * k, s.oy - 90 + 30 * k, Cpu, 3); Tough(sn); OzV11Kit.CpuAttack(s.w, sn, s.v); }
                    bool sniped = false;
                    for (int i = 0; i < 6000 && !sniped; i++) Tick(s, 1, e => { if (e.type == ZEventType.Sniped && e.objId == s.v.Id) sniped = true; });
                    if (!sniped) { r.note = $"seed {seed}: no snipe in 60 s"; continue; }
                    r.exited = true;
                    Holders(s.w, s.v, out r.targets, out r.first, out r.queued, out r.who);
                    int n = (int)Math.Round(10.0 / ZConst.TICK);
                    for (int i = 0; i < n; i++)
                    {
                        if (s.v.Owner != Team.Neutral || s.v.Drivers.Count > 0) break;                                  // a robot climbed in: a new crew, the count ends
                        Tick(s, 1, e => { if (ShotAt(e, s.v) && s.foes.Exists(f => f.Id == e.objId)) r.after++; }); r.ticks++;
                    }
                    r.doing = Doing(s); r.note = $"seed {seed}";
                    return r;
                }
                return r;
            }
            finally { ZWorld.EmptiedTargetsDeviation = was; }
        }

        static string Body(string set)
        {
            pass = fail = 0; sb = new StringBuilder();
            int t0 = ZWorld.EmptiedTargetsDropped, o0 = ZWorld.EmptiedOrdersDropped, q0 = ZWorld.EmptiedQueuedDropped;
            using (OzV11Kit.GameSwitches())
            {
                bool dv171 = ZWorld.VehicleExitDeviation; ZWorld.VehicleExitDeviation = true;
                try
                {
                    var a = Exit(set, 1, true);
                    Check(a.exited && a.ticks > 0 && a.targets == 0 && a.first == 0 && a.queued == 0 && a.after == 0,
                          $"E1 EXIT DRIVER while an enemy jeep and grunt squad shoot the light tank (shots at it before {a.before}: the jeep {a.beforeJeep}, the squad {a.beforeSquad}): right after the EXIT, units with it as target {a.targets}, "
                          + $"with an attack on it first {a.first} / queued {a.queued} ({a.who}); shots started at the empty tank in the next {a.ticks * ZConst.TICK:0.0} s: {a.after} (want 0); after: {a.doing}{(a.note != "" ? "; " + a.note : "")}");
                    var z = Exit(set, 1, false);
                    Info($"E1z the same with DV176 off: units still on the empty tank after the EXIT {z.targets} target(s), {z.first} first / {z.queued} queued attack(s); shots started at it in {z.ticks * ZConst.TICK:0.0} s: {z.after}{(z.note != "" ? "; " + z.note : "")}");
                    var b = ShotOut(set, true);
                    Check(b.exited && b.ticks > 0 && b.targets == 0 && b.first == 0 && b.queued == 0 && b.after == 0,
                          $"E2 its crew SHOT OUT by enemy snipers while a jeep, a squad and a grunt with a queued attack are on it ({b.note}): right after the snipe's tick, units with it as target {b.targets}, "
                          + $"attack on it first {b.first} / queued {b.queued} ({b.who}); shots started at it in the next {b.ticks * ZConst.TICK:0.0} s: {b.after} (want 0); after: {b.doing}");
                    var bz = ShotOut(set, false);
                    Info($"E2z the same with DV176 off ({bz.note}): {bz.targets} target(s), {bz.first} first / {bz.queued} queued attack(s) on the empty tank (the computer's queued one: Zod's and DV81's rule keep it); shots started at it in {bz.ticks * ZConst.TICK:0.0} s: {bz.after}");
                    // E3: an explicit attack order on the empty tank after the EXIT is carried out
                    int e3 = 0;
                    if (a.s != null && a.exited)
                    {
                        var s = a.s; var g = Robot(s, "grunt", s.v.CenterX - 70, s.v.CenterY + 40, Me); Tough(g);
                        OzV11Kit.PlayerAttack(s.w, g, s.v);
                        Tick(s, (int)Math.Round(15.0 / ZConst.TICK), e => { if (ShotAt(e, s.v) && e.objId == g.Id) e3++; });
                    }
                    Check(e3 > 0, $"E3 after the EXIT a red grunt's explicit attack order on the empty tank (DV81's Ctrl attack) is carried out: its shots at it in 15 s {e3}");
                    var a2 = Exit(set, 1, true);
                    Check(a.exited && a2.exited && a.hash == a2.hash && a.after == a2.after, $"E4 E1 twice on seed 1: the same events bit for bit ({a.hash:x16} / {a2.hash:x16})");
                }
                finally { ZWorld.VehicleExitDeviation = dv171; }
                Info($"DV176 counters in this run (read before the game switches are put back): targets dropped {ZWorld.EmptiedTargetsDropped - t0}, attack waypoints dropped at the EXIT {ZWorld.EmptiedOrdersDropped - o0}, queued ones dropped after a shot-out crew {ZWorld.EmptiedQueuedDropped - q0}");
            }
            return $"OzV14EmptiedTargetsCount (DV176): {(fail == 0 && pass > 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}\n" + sb;
        }

        /// <summary>The whole count (the menu, and the DV176 row's probe).</summary>
        public static string Quick(string settings) => Body(settings);

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Gun: Emptied Targets Count (headless, DV176)")]
        static void Menu() => OzV11Kit.RunMenu("emptied targets: count (DV176)", () => Quick(OzV11Kit.SettingsText));
#endif
    }
}
