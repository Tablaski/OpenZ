using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 items 32-34 counted check (rule DV141, Sim/ZObject.CraneSnipe.cs: the crane's driver can be sniped while he is up in the cab hatch;
    /// while the crane works he stays inside and cannot be sniped).
    /// Headless sim, flat maps, no scene, no Play mode (menu OpenZ/V1.2/Combat: 32 Crane Driver Snipe Check (headless), Editor/OzCraneSnipeMenu.cs;
    /// the off-editor harness runs the same code). The rule switch is set by name, so the same file runs on the rules without the kit too
    /// (its "off" rows must then be the same). Every switch is put back afterwards.
    /// <list type="bullet">
    /// <item>R rules: when the crane can be sniped (driver inside, up, back in, resting, empty, 'Zod exact'), the HP of one driver hit on it
    /// per attacker, every other target's driver hit unchanged.</item>
    /// <item>F fights: one attacker unit (a full squad for robots) against one crewed blue crane that stands and fires back (DV31), everything
    /// mortal, the switches of a game in Play; 40 fights per attacker, the rule off (the game before) and on. 80 px apart (inside both
    /// ranges), and a walk-in from 300 px (the attacker stops where its own range lets it fire).</item>
    /// <item>E the emptied crane: neutral, no driver, no orders, stopped, enterable; a red grunt squad takes it.</item>
    /// <item>J a crane mends a damaged building under sniper fire: while it works its driver stays inside and cannot be hit, the job ends
    /// as it should, the building is mended; after the job he comes up and is sniped as before.</item>
    /// <item>B a crane attacked on a standing bridge: its driver stays inside (DV82 would turn every driver hit there into a hull hit); a
    /// crane rebuilding a destroyed bridge, radar, repair station or factory under sniper fire: no pop-out, no shot, no driver hit while
    /// it works, every job finished; after the job, outside a ruin and a DV82 spot, as before.</item>
    /// <item>W the driver up and firing when the job begins drops back in at once; a move order given in the middle of a job waits for
    /// its end (Zod), and the crane is never emptied inside the ruin.</item>
    /// <item>D deterministic: a fight run twice gives the same events.</item>
    /// </list>
    /// </summary>
    public static class OzV12CraneSnipeCount
    {
        public const int Fights = 40, SeedBase = 3200;
        const int CX = 640, CY = 640, Gap = 80, FarGap = 300, Big = 1000000000;
        const double FightCap = 90;
        const string Rule = "CraneSnipeDeviation";

        /// <summary>The attackers counted: every robot, the jeep and the gatling (Zod's list of who can snipe: grunt, psycho, sniper, pyro, laser,
        /// jeep, gatling; the Tough cannot).</summary>
        public static readonly string[] Attackers = { "sniper", "jeep", "grunt", "psycho", "laser", "gatling", "pyro", "tough" };

        /// <summary>The rules of a game in Play that OzV11Kit.GameSwitches leaves off for the older checks and that act in a fight.</summary>
        public static readonly string[] PlayRules =
        {
            "DriverFireDeviation", "SniperBalanceDeviation", "SniperSteadyDeviation", "SniperHeadshotDeviation", "ProneTargetDeviation", "JeepChargeDeviation",
            "ToughSteadyDeviation", "GroundFireDeviation", "GrenadeBoostDeviation", "CalmMoveDeviation", "CalmFollowDeviation", "NoAutoFlagUnderFireDeviation",
            "VeteranHealDeviation", "FortRunDeviation", "RockRoundDeviation",
        };

        /// <summary>The switches of a game in Play (OzV11Kit.GameSwitches + PlayRules), the rule as asked. Dispose to put everything back.</summary>
        public static OzV11Kit.Switches Game(bool rule)
        {
            var s = OzV11Kit.GameSwitches();
            foreach (var n in PlayRules) OzV11Kit.TrySet(typeof(ZWorld), n, true);
            OzV11Kit.TrySet(typeof(ZWorld), Rule, rule);
            return s;
        }

        public static bool RuleInBuild => typeof(ZWorld).GetField(Rule) != null;
        static void SetRule(bool on) => OzV11Kit.TrySet(typeof(ZWorld), Rule, on);

        static string flat, bridge, radar;
        static string Flat => flat ??= OzV11Kit.Plain(96, 96);
        static string BridgeMap => bridge ??= OzV11Kit.Plain(96, 96, ("building:bridge_vert", 20, 20, "null"));
        static string RadarMap => radar ??= OzV11Kit.Plain(96, 96, ("building:radar", 40, 30, "null"));

        /// <summary>A world in which blue is a CPU team that gives no orders (red = the player), with a blue and a red grunt far off that
        /// cannot move, fire or die: the game is never over (DV10 would stop every unit) while one side has nothing else on the map.</summary>
        static ZWorld NewWorld(string settings, string map, int seed)
        {
            var w = OzV11Kit.World(settings, map, seed);
            OzV11Kit.Dummy(w, 1440, 1440);
            var red = w.CreateRobotGroup("grunt", 120 - 8, 1440 - 8, Team.Red, 1);
            red.MoveSpeed = 0; red.RealMoveSpeed = 0; red.Damage = 0; red.MaxHealth = red.Health = Big;
            w.Events.Clear();
            return w;
        }

        static ulong Mix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }

        // ================================================================================================ F: fights
        public sealed class Tally
        {
            public int fights, sniped, hull, lost, none, upFights, emptyOk, insideHits, craneShots, driverHits, hits;
            public double time, minDist;
            public ulong hash = 1469598103934665603UL;
            public double Mean => sniped > 0 ? time / sniped : 0;
        }

        /// <summary>One attacker unit (a squad for robots) against one crewed blue crane gap px east of it, an attack order, everything mortal.</summary>
        public static Tally Fight(string settings, string attacker, int gap, int fights, int seedBase)
        {
            var r = new Tally { fights = fights, minDist = 1e9 };
            for (int s = 1; s <= fights; s++)
            {
                var w = NewWorld(settings, Flat, seedBase + s);
                var crane = OzV11Kit.Place(w, "crane", CX + gap, CY, Team.Blue);
                var a = OzV11Kit.Place(w, attacker, CX, CY, Team.Red);
                var squad = OzV11Kit.Squad(a).ToList();
                OzV11Kit.PlayerAttack(w, a, crane);
                int outcome = 3; bool up = false;
                while (w.Time < FightCap && outcome == 3)
                {
                    bool before = crane.CanBeSniped();
                    w.Tick(ZConst.TICK);
                    bool after = crane.CanBeSniped();
                    up |= crane.CraneGunUp;
                    foreach (var m in squad) if (OzV11Kit.Alive(m)) r.minDist = Math.Min(r.minDist, OzV11Kit.Dist(m, crane));
                    foreach (var e in w.Events)
                    {
                        r.hash = Mix(Mix(Mix(r.hash, (long)e.type), e.objId), e.otherId);
                        if (e.type == ZEventType.Hitscan && e.objId == crane.Id) r.craneShots++;
                        if (e.type == ZEventType.Hitscan && e.otherId == crane.Id && e.flag) r.hits++;
                        if ((e.type == ZEventType.DriverHit || e.type == ZEventType.Sniped) && e.objId == crane.Id) { r.driverHits++; if (!before && !after) r.insideHits++; }
                        if (e.objId != crane.Id || outcome != 3) continue;
                        if (e.type == ZEventType.Sniped) outcome = 0;
                        else if (e.type == ZEventType.Death) outcome = 1;
                    }
                    w.Events.Clear();
                    if (outcome == 0)
                    {
                        w.Tick(ZConst.TICK); w.Events.Clear();                                                  // the server pass of the snipe has run: look at the hull now
                        if (crane.Owner == Team.Neutral && crane.Drivers.Count == 0 && crane.Waypoints.Count == 0 && !crane.IsMoving && crane.CanBeEntered() && !crane.CanBeSniped()) r.emptyOk++;
                        break;
                    }
                    if (outcome != 3) break;
                    bool lost = a.Kind == ZKind.Robot ? squad.All(m => m.Destroyed) : a.Destroyed || a.Owner == Team.Neutral;
                    if (lost) outcome = 2;
                }
                if (up) r.upFights++;
                if (outcome == 0) { r.sniped++; r.time += w.Time; }
                else if (outcome == 1) r.hull++;
                else if (outcome == 2) r.lost++;
                else r.none++;
                r.hash = Mix(r.hash, outcome);
            }
            return r;
        }

        static string Row(string a, int gap, bool on, Tally t) =>
            $"{a} -> crane | {gap} px | {(on ? "on " : "off")} | {t.sniped}/{t.fights} | {t.Mean:0.0} | {t.hull}/{t.fights} | {t.lost}/{t.fights} | {t.none}/{t.fights} | {t.upFights}/{t.fights} | {t.driverHits} | {t.insideHits} | {t.craneShots} | {t.hits} | {t.minDist:0}";

        // ================================================================================================ the whole check
        /// <summary>The whole check (settings = default_settings). fights per row; quick = the 80 px rows of sniper, jeep and grunt only.</summary>
        public static string Check(string settings, int fights = Fights, bool quick = false, Action<string> progress = null)
        {
            var sb = new StringBuilder();
            bool inBuild = RuleInBuild;
            using (Game(false))
            {
                sb.AppendLine($"[OpenZ 1.2 item 32] the crane's driver can be sniped while he is up (DV141): rule switch in this build {(inBuild ? "yes" : "NO (only the 'off' rows run)")}; {fights} fights per row (seeds {SeedBase + 1}-{SeedBase + fights}), cap {FightCap:0} s, the switches of a game in Play");
                var res = new Dictionary<(string, int, bool), Tally>();
                var rows = quick ? new[] { "sniper", "jeep", "grunt" } : Attackers;
                sb.AppendLine();
                sb.AppendLine("F fights: attacker -> target | start gap | rule | driver sniped | mean s to the snipe | hull destroyed first | attackers lost | no result | fights with the driver up | driver hits (incl. the last) | of them while he was inside | crane shots | hits on the crane | nearest attacker px");
                foreach (int gap in quick ? new[] { Gap } : new[] { Gap, FarGap })
                    foreach (var a in rows)
                        foreach (bool on in inBuild ? new[] { false, true } : new[] { false })
                        {
                            if (gap == FarGap && a != "sniper" && a != "jeep" && a != "grunt" && a != "laser") continue;
                            SetRule(on);
                            var t = Fight(settings, a, gap, fights, SeedBase);
                            res[(a, gap, on)] = t;
                            sb.AppendLine(Row(a, gap, on, t));
                            progress?.Invoke($"{a} {gap} px rule {(on ? "on" : "off")}: sniped {t.sniped}/{fights}");
                        }
                SetRule(false);
                sb.AppendLine();
                Tally T(string a, int gap, bool on) => res.TryGetValue((a, gap, on), out var t) ? t : null;
                int offSnipes = res.Where(kv => !kv.Key.Item3).Sum(kv => kv.Value.sniped);
                sb.AppendLine(OzV11Kit.Verdict(offSnipes == 0, $"F0 before (rule off, the game until now): no crane driver is ever sniped ({offSnipes} in {res.Count(kv => !kv.Key.Item3) * fights} fights)"));
                if (inBuild)
                {
                    var want = new Dictionary<string, int> { ["sniper"] = 36, ["jeep"] = 32, ["grunt"] = 24, ["psycho"] = 32, ["laser"] = 32, ["gatling"] = 26 };
                    foreach (var a in rows.Where(want.ContainsKey))
                    {
                        var t = T(a, Gap, true); int need = (int)Math.Ceiling(want[a] * fights / 40.0);
                        sb.AppendLine(OzV11Kit.Verdict(t != null && t.sniped >= need, $"F1 {a} at {Gap} px, rule on: the driver sniped in {t?.sniped}/{fights} fights (at least {need}), mean {t?.Mean:0.0} s"));
                    }
                    foreach (var a in rows.Where(x => x == "pyro" || x == "tough"))
                    {
                        var t = T(a, Gap, true);
                        sb.AppendLine(OzV11Kit.Verdict(t != null && t.sniped == 0, $"F2 {a}, rule on: never snipes ({t?.sniped}/{fights}; {(a == "pyro" ? "Zod's attack_snipe_chance 0" : "Zod: cannot snipe")})"));
                    }
                    var sn = T("sniper", Gap, true);
                    var others = rows.Where(x => x != "sniper" && want.ContainsKey(x)).Select(x => (x, t: T(x, Gap, true))).Where(x => x.t != null && x.t.sniped > 0).ToList();
                    sb.AppendLine(OzV11Kit.Verdict(sn != null && sn.sniped > 0 && others.All(x => x.t.Mean > sn.Mean), $"F3 snipers are the fastest crane-driver killers: {sn?.Mean:0.0} s; " + string.Join(", ", others.Select(x => $"{x.x} {x.t.Mean:0.0} s"))));
                    int inside = res.Where(kv => kv.Key.Item3).Sum(kv => kv.Value.insideHits), driverOn = res.Where(kv => kv.Key.Item3).Sum(kv => kv.Value.driverHits);
                    sb.AppendLine(OzV11Kit.Verdict(inside == 0 && driverOn > 0, $"F4 rule on: a driver hit only while the driver is up ({driverOn} driver hits, {inside} while he was inside)"));
                    int snOn = res.Where(kv => kv.Key.Item3).Sum(kv => kv.Value.sniped), okOn = res.Where(kv => kv.Key.Item3).Sum(kv => kv.Value.emptyOk);
                    sb.AppendLine(OzV11Kit.Verdict(snOn > 0 && okOn == snOn, $"F5 every sniped crane is empty: neutral, no driver, no orders, stopped, enterable, not snipable ({okOn} of {snOn})"));
                    if (!quick)
                    {
                        var far = T("sniper", FarGap, true);
                        sb.AppendLine(OzV11Kit.Verdict(far != null && far.insideHits == 0, $"F6 snipers sent from {FarGap} px stop where they can fire ({far?.minDist:0} px, the driver's rifle reaches 120 px): the driver up in {far?.upFights}/{fights} fights, sniped {far?.sniped}, hull first {far?.hull}, no driver hit while he was inside ({far?.insideHits})"));
                        var jf = T("jeep", FarGap, true);
                        sb.AppendLine(OzV11Kit.Verdict(jf != null && jf.sniped >= (int)Math.Ceiling(30 * fights / 40.0), $"F7 a jeep sent from {FarGap} px drives into its range and snipes the driver in {jf?.sniped}/{fights} fights, mean {jf?.Mean:0.0} s"));
                    }
                }
                if (!quick)
                {
                    sb.AppendLine();
                    Rules(settings, sb, inBuild);
                    if (inBuild) { Empty(settings, sb); Job(settings, sb); Bridge(settings, sb); Ruins(settings, sb); UpAtJobStart(settings, sb); OrderMidJob(settings, sb); Twice(settings, sb); }
                }
            }
            return sb.ToString();
        }

        /// <summary>The quick form for the parity report's row: the 80 px rows of sniper, jeep and grunt, 10 fights each, the rule lines and
        /// W1 (the driver up as a job begins drops back in; while the crane works he stays inside).</summary>
        public static string Quick(string settings)
        {
            var sb = new StringBuilder(Check(settings, 10, true));
            using (Game(false)) { Rules(settings, sb, RuleInBuild); if (RuleInBuild) UpAtJobStart(settings, sb); }
            return sb.ToString();
        }

        // ================================================================================================ R: rules
        static void Rules(string settings, StringBuilder sb, bool inBuild)
        {
            sb.AppendLine("R rules:");
            bool snipeWas = ZWorld.SnipeDeviation;
            try
            {
                foreach (bool on in inBuild ? new[] { false, true } : new[] { false })
                {
                    SetRule(on);
                    var w = NewWorld(settings, Flat, 7);
                    var crane = OzV11Kit.Place(w, "crane", CX + Gap, CY, Team.Blue);
                    bool fresh = crane.CanBeSniped();
                    var g = OzV11Kit.Place(w, "grunt", CX, CY, Team.Red);
                    foreach (var m in OzV11Kit.Squad(g)) m.MaxHealth = m.Health = Big;                             // they keep attacking, nobody dies
                    crane.MaxHealth = crane.Health = Big; crane.Drivers[0] = Big;
                    OzV11Kit.PlayerAttack(w, g, crane);
                    double upAt = -1; bool snipableUp = false;
                    while (w.Time < 6) { w.Tick(ZConst.TICK); w.Events.Clear(); if (crane.CraneGunUp && upAt < 0) upAt = w.Time; if (crane.CraneGunUp && w.Time > upAt + 0.2 && upAt >= 0) snipableUp |= crane.CanBeSniped(); }
                    foreach (var m in OzV11Kit.Squad(g)) { m.SetHealth(0); w.UpdateObjectHealth(m); }               // the attackers are gone: he goes back in
                    double downAt = -1;
                    while (w.Time < 14) { w.Tick(ZConst.TICK); w.Events.Clear(); if (!crane.CraneGunUp && downAt < 0) downAt = w.Time; }
                    bool afterDown = crane.CanBeSniped();
                    if (!on)
                        sb.AppendLine(OzV11Kit.Verdict(!fresh && upAt >= 0 && !snipableUp && !afterDown, $"R1 rule off: the driver comes up ({upAt:0.00} s) and fires, the crane is never snipable (fresh {fresh}, up {snipableUp}, back in {afterDown})"));
                    else
                    {
                        sb.AppendLine(OzV11Kit.Verdict(!fresh && upAt >= 0 && snipableUp, $"R2 rule on: a crewed crane with the driver inside is not snipable ({fresh}); attacked from {Gap} px the driver is up at {upAt:0.00} s and snipable ({snipableUp})"));
                        sb.AppendLine(OzV11Kit.Verdict(downAt >= 0 && !afterDown, $"R3 rule on: the attackers gone, he drops back in ({downAt:0.00} s) and the crane is not snipable again ({afterDown})"));
                        ZWorld.SnipeDeviation = false;
                        var w2 = NewWorld(settings, Flat, 8);
                        var c2 = OzV11Kit.Place(w2, "crane", CX + Gap, CY, Team.Blue); c2.MaxHealth = c2.Health = Big; c2.Drivers[0] = Big;
                        var g2 = OzV11Kit.Place(w2, "grunt", CX, CY, Team.Red); foreach (var m in OzV11Kit.Squad(g2)) m.MaxHealth = m.Health = Big;
                        OzV11Kit.PlayerAttack(w2, g2, c2);
                        bool zodSnipable = false, zodUp = false;
                        while (w2.Time < 6) { w2.Tick(ZConst.TICK); w2.Events.Clear(); zodUp |= c2.CraneGunUp; zodSnipable |= c2.CanBeSniped(); }
                        ZWorld.SnipeDeviation = snipeWas;
                        sb.AppendLine(OzV11Kit.Verdict(!zodSnipable, $"R4 the rule switch on but driver sniping as in Zod (SnipeDeviation off): never snipable (driver up {zodUp}, snipable {zodSnipable})"));
                    }
                }
                // DV75: a resting crew shows its driver but the crane stays not snipable
                if (inBuild)
                {
                    SetRule(true);
                    var w = NewWorld(settings, Flat, 9);
                    var crane = OzV11Kit.Place(w, "crane", CX, CY, Team.Blue);
                    double until = ZWorld.IdleRestSeconds + ZWorld.IdleRestSpreadSeconds + 2;
                    while (w.Time < until) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    sb.AppendLine(OzV11Kit.Verdict(crane.Resting && !crane.CanBeSniped(), $"R5 rule on: a crane whose crew rests in the hatch (DV75) stays not snipable (resting {crane.Resting}, snipable {crane.CanBeSniped()})"));
                    var empty = OzV11Kit.Place(w, "crane", CX + 200, CY, Team.Neutral);
                    sb.AppendLine(OzV11Kit.Verdict(!empty.CanBeSniped(), $"R6 an empty crane is never snipable ({empty.CanBeSniped()})"));
                }
                // the HP of one driver hit on the crane, and every other target unchanged
                var dw = NewWorld(settings, Flat, 11);
                var cr = OzV11Kit.Place(dw, "crane", CX + Gap, CY, Team.Blue);
                var line = new StringBuilder();
                bool tableOk = true;
                foreach (var a in new[] { "grunt", "psycho", "sniper", "laser", "pyro", "jeep", "gatling" })
                {
                    var u = OzV11Kit.Place(dw, a, CX, CY + 200, Team.Red, 1);
                    SetRule(false); int off = dw.DriverHitDamage(u, cr);
                    SetRule(true); int on = inBuild ? dw.DriverHitDamage(u, cr) : off;
                    int zodDmg = u.Damage, expect = Math.Max(zodDmg, Floor) * (a == "sniper" ? 3 : 1);
                    if (inBuild && on != expect) tableOk = false;
                    line.Append($"{a} {off} -> {on}; ");
                }
                SetRule(false);
                if (inBuild) sb.AppendLine(OzV11Kit.Verdict(tableOk, $"R7 HP of one driver hit on the crane, rule off -> on (at least {Floor}, snipers x3): {line}"));
                int pairs = 0, same = 0;
                var targets = new[] { "jeep", "light", "medium", "heavy", "apc", "missile_launcher", "gatling", "gun", "howitzer", "missile_cannon" };
                foreach (var a in new[] { "grunt", "psycho", "sniper", "laser", "pyro", "jeep", "gatling" })
                    foreach (var tt in targets)
                    {
                        var u = OzV11Kit.Place(dw, a, CX, CY + 300, Team.Red, 1); var t = OzV11Kit.Place(dw, tt, CX + 300, CY + 300, Team.Blue);
                        SetRule(false); int off = dw.DriverHitDamage(u, t); double coff = ZWorld.SnipeChanceOn(u, t);
                        SetRule(true); int on = dw.DriverHitDamage(u, t); double con = ZWorld.SnipeChanceOn(u, t);
                        pairs++; if (on == off && Math.Abs(con - coff) < 1e-12) same++;
                    }
                SetRule(false);
                sb.AppendLine(OzV11Kit.Verdict(same == pairs, $"R8 every other target: the HP of a driver hit and the snipe chance are the same with the rule on and off ({same} of {pairs} attacker / target pairs)"));
            }
            finally { ZWorld.SnipeDeviation = snipeWas; SetRule(false); }
        }

        // ================================================================================================ E: the emptied crane is taken
        static void Empty(string settings, StringBuilder sb)
        {
            SetRule(true);
            var w = NewWorld(settings, Flat, 12);
            var crane = OzV11Kit.Place(w, "crane", CX + Gap, CY, Team.Blue); crane.MaxHealth = crane.Health = Big;
            var sn = OzV11Kit.Place(w, "sniper", CX, CY, Team.Red);
            foreach (var m in OzV11Kit.Squad(sn)) m.MaxHealth = m.Health = Big;
            OzV11Kit.PlayerAttack(w, sn, crane);
            double at = -1; bool neutralEvent = false;
            while (w.Time < 30 && at < 0)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type == ZEventType.Sniped && e.objId == crane.Id) at = w.Time;
                    if (e.type == ZEventType.OwnerChanged && e.objId == crane.Id && (Team)(int)e.value == Team.Neutral) neutralEvent = true;
                }
                w.Events.Clear();
            }
            w.Tick(ZConst.TICK); w.Events.Clear();
            bool empty = crane.Owner == Team.Neutral && crane.Drivers.Count == 0 && !crane.CraneGunUp && crane.CanBeEntered();
            bool disengaged = OzV11Kit.Squad(sn).All(m => m.AttackObject != crane);
            foreach (var m in OzV11Kit.Squad(sn)) m.Damage = 0;
            var g = OzV11Kit.Place(w, "grunt", crane.CenterX, crane.CenterY + 90, Team.Red, 1);
            OzV11Kit.PlayerEnter(w, g, crane);
            bool entered = false; double t0 = w.Time;
            while (w.Time < t0 + 30 && !entered)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.Entered && e.otherId == crane.Id) entered = true;
                w.Events.Clear();
            }
            SetRule(false);
            sb.AppendLine(OzV11Kit.Verdict(at >= 0 && neutralEvent && empty && disengaged && entered && crane.Owner == Team.Red && crane.Drivers.Count == 1,
                $"E1 a sniper squad empties the crane at {at:0.00} s: OwnerChanged -> neutral {neutralEvent}, empty and enterable {empty}, every sniper let go of it {disengaged}; a red grunt walks in {entered}: the crane is {crane.Owner} with {crane.Drivers.Count} driver"));
        }

        // ================================================================================================ a crane's job, watched
        /// <summary>A crane watched tick by tick, its own events in their order: phase 0 before its job (until its CraneAnim on), 1 while
        /// it works (to its CraneAnim off), 2 after the job. Its shots, the driver hits on it, the ticks it ends with the driver up and
        /// the ticks it ends snipable, per phase (an event counts in the phase it falls in, a tick's state in the phase the tick ends in).</summary>
        sealed class JobWatch
        {
            readonly ZObject crane;
            public int phase, offEvents, tickDriverHits;
            public double on = -1, off = -1, sniped = -1;
            public Team ownerAtOff = Team.Neutral;
            public readonly int[] shots = new int[3], driverHits = new int[3], upTicks = new int[3], snipableTicks = new int[3];
            public JobWatch(ZObject c) { crane = c; }

            public void Tick(ZWorld w)
            {
                w.Tick(ZConst.TICK);
                tickDriverHits = 0;
                foreach (var e in w.Events)
                {
                    if (e.objId != crane.Id) continue;
                    if (e.type == ZEventType.CraneAnim)
                    {
                        if (e.flag) { if (phase == 0) { phase = 1; on = w.Time; } }
                        else { offEvents++; if (phase == 1) { phase = 2; off = w.Time; ownerAtOff = crane.Owner; } }
                    }
                    else if (e.type == ZEventType.Hitscan) shots[phase]++;
                    else if (e.type == ZEventType.DriverHit || e.type == ZEventType.Sniped)
                    {
                        driverHits[phase]++; tickDriverHits++;
                        if (e.type == ZEventType.Sniped && sniped < 0) sniped = w.Time;
                    }
                }
                w.Events.Clear();
                if (crane.CraneGunUp) upTicks[phase]++;
                if (crane.CanBeSniped()) snipableTicks[phase]++;
            }

            /// <summary>While it worked: no driver up, no shot, never snipable, no driver hit.</summary>
            public bool QuietAtWork => upTicks[1] == 0 && shots[1] == 0 && snipableTicks[1] == 0 && driverHits[1] == 0;
            public string AtWork => $"driver up {upTicks[1]} ticks, crane shots {shots[1]}, snipable {snipableTicks[1]} ticks, driver hits {driverHits[1]}";
        }

        /// <summary>A red sniper squad that cannot die, dx px east of the crane, attacking it (the player's order).</summary>
        static ZObject Snipers(ZWorld w, ZObject crane, int dx)
        {
            var sn = OzV11Kit.Place(w, "sniper", crane.CenterX + dx, crane.CenterY, Team.Red);
            foreach (var m in OzV11Kit.Squad(sn)) m.MaxHealth = m.Health = Big;
            OzV11Kit.PlayerAttack(w, sn, crane);
            return sn;
        }

        /// <summary>The emptied crane: its snipers silenced, a red grunt sent to walk in (40 s at most). True = he got in.</summary>
        static bool Taken(ZWorld w, ZObject crane, ZObject sn, int gx, int gy)
        {
            if (sn != null) foreach (var m in OzV11Kit.Squad(sn)) { m.Damage = 0; w.SendWaypoints(Team.Red, m, new ZWaypoint[0]); }
            var g = OzV11Kit.Place(w, "grunt", gx, gy, Team.Red, 1);
            OzV11Kit.PlayerEnter(w, g, crane);
            double t1 = w.Time;
            while (w.Time < t1 + 40)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.Entered && e.otherId == crane.Id) { w.Events.Clear(); return true; }
                w.Events.Clear();
            }
            return false;
        }

        /// <summary>The crane's box reaches into the ruin b: a bridge's own width, its deck or up to 88 px (DV82's reach) before either end
        /// along its axis; any other building's box. Positions only (the check's own copy of the geometry).</summary>
        static bool InRuin(ZObject c, ZObject b)
        {
            int ax = b.Type == "bridge_horz" ? 88 : 0, ay = b.Type == "bridge_vert" ? 88 : 0;
            return c.LocX < b.LocX + b.WidthPix + ax && b.LocX - ax < c.LocX + c.WidthPix && c.LocY < b.LocY + b.HeightPix + ay && b.LocY - ay < c.LocY + c.HeightPix;
        }

        static void Ruin(ZWorld w, ZObject b) { w.Tick(ZConst.TICK); b.SetHealth(0); w.UpdateObjectHealth(b); w.Tick(ZConst.TICK); w.Events.Clear(); }
        static void CraneOrder(ZWorld w, ZObject crane, ZObject b) =>
            w.SendWaypoints(Team.Blue, crane, new[] { new ZWaypoint { mode = WpMode.CraneRepair, refId = b.Id, x = b.CenterX, y = b.CenterY } });

        // ================================================================================================ J: mending a building under fire
        /// <summary>A crane mends a damaged standing radar (DV61: from its entrance) while a red sniper squad 80 px off attacks it from the
        /// job's start; its driver is down to one hit. Rule off: he fires while it works and cannot be hit (the game until now). Rule on:
        /// while it works he stays inside, the job ends as it should and the radar is mended; after the job he comes up and is sniped as
        /// before.</summary>
        static void Job(string settings, StringBuilder sb)
        {
            foreach (bool on in new[] { false, true })
            {
                SetRule(on);
                var w = NewWorld(settings, RadarMap, 13);
                var radar = w.Objects.First(o => o.Kind == ZKind.Building && o.Type == "radar");
                radar.SetHealth(radar.MaxHealth / 2);
                var crane = OzV11Kit.Place(w, "crane", radar.CenterX, radar.LocY + radar.HeightPix + 120, Team.Blue); crane.MaxHealth = crane.Health = Big;
                CraneOrder(w, crane, radar);
                var jw = new JobWatch(crane); ZObject sn = null;
                while (w.Time < 40 && (jw.phase < 2 || (on && jw.sniped < 0)))
                {
                    jw.Tick(w);
                    if (jw.phase >= 1 && sn == null)
                    {   // the job has begun: a sniper squad 80 px off attacks the crane; its driver is down to one hit (one driver hit empties it)
                        sn = Snipers(w, crane, -Gap);
                        if (crane.Drivers.Count > 0) crane.Drivers[0] = 1;
                    }
                }
                for (int i = 0; i < 100; i++) jw.Tick(w);
                bool mended = radar.Health >= radar.MaxHealth;
                if (!on)
                    sb.AppendLine(OzV11Kit.Verdict(jw.on >= 0 && jw.sniped < 0 && jw.off >= 0 && mended,
                        $"J1 rule off: the crane mends the radar under fire (job {jw.on:0.0}-{jw.off:0.0} s; while it works {jw.AtWork}), never sniped, mended {mended}"));
                else
                    sb.AppendLine(OzV11Kit.Verdict(jw.on >= 0 && jw.off > jw.on && jw.offEvents == 1 && jw.QuietAtWork && mended && jw.ownerAtOff == Team.Blue &&
                                                   jw.sniped > jw.off && crane.Owner == Team.Neutral && crane.Waypoints.Count == 0,
                        $"J2 rule on: the crane mends the radar under sniper fire, the driver at one hit (job {jw.on:0.00}-{jw.off:0.00} s): while it works {jw.AtWork}; " +
                        $"the job ends as it should (CraneAnim off {jw.offEvents}x), the radar mended {mended}, the crane {jw.ownerAtOff} at the job's end; " +
                        $"after the job (outside work, as before) he comes up and is sniped at {jw.sniped:0.00} s: the crane {crane.Owner}, orders {crane.Waypoints.Count}"));
            }
            SetRule(false);
        }

        // ================================================================================================ B: on a standing bridge
        static void Bridge(string settings, StringBuilder sb)
        {
            bool safeWas = ZWorld.SnipeSafeSpotDeviation;
            try
            {
                ZWorld.SnipeSafeSpotDeviation = true;
                foreach (var (name, onBridge) in new[] { ("on the deck of a standing bridge", true), ("on the road 200 px from it (control)", false) })
                    foreach (bool on in new[] { false, true })
                    {
                        SetRule(on);
                        var w = NewWorld(settings, BridgeMap, 14);
                        var br = w.Objects.First(o => o.IsBridge);
                        int bx = br.LocX + br.WidthPix / 2, by = onBridge ? br.LocY + br.HeightPix / 2 : br.LocY - 200;
                        var crane = OzV11Kit.Place(w, "crane", bx, by, Team.Blue); crane.MaxHealth = crane.Health = Big;
                        var g = OzV11Kit.Place(w, "grunt", bx + Gap, by, Team.Red);
                        foreach (var m in OzV11Kit.Squad(g)) m.MaxHealth = m.Health = Big;
                        OzV11Kit.PlayerAttack(w, g, crane);
                        int shots = 0, driverHits = 0; bool up = false, snipable = false;
                        while (w.Time < 20)
                        {
                            w.Tick(ZConst.TICK);
                            up |= crane.CraneGunUp; snipable |= crane.CanBeSniped();
                            foreach (var e in w.Events)
                            {
                                if (e.type == ZEventType.Hitscan && e.objId == crane.Id) shots++;
                                if ((e.type == ZEventType.DriverHit || e.type == ZEventType.Sniped) && e.objId == crane.Id) driverHits++;
                            }
                            w.Events.Clear();
                        }
                        bool spot = w.SnipeSafeSpot(crane);
                        string what = $"{name}, rule {(on ? "on" : "off")}: DV82 spot {spot}, driver up {up}, crane shots {shots}, snipable {snipable}, driver hits {driverHits}";
                        if (onBridge && on) sb.AppendLine(OzV11Kit.Verdict(spot && !up && shots == 0 && !snipable && driverHits == 0, "B1 " + what + " (he stays inside: no driver shown who cannot be hit)"));
                        else if (onBridge) sb.AppendLine(OzV11Kit.Verdict(spot && up && shots > 0 && driverHits == 0, "B0 " + what + " (the game until now: he fires from the bridge and cannot be hit)"));
                        else if (on) sb.AppendLine(OzV11Kit.Verdict(!spot && up && shots > 0 && driverHits > 0, "B2 " + what));
                        else sb.AppendLine(OzV11Kit.Verdict(!spot && up && shots > 0 && driverHits == 0, "B3 " + what));
                    }
            }
            finally { ZWorld.SnipeSafeSpotDeviation = safeWas; SetRule(false); }
        }

        // ================================================================================================ B4-B6: a crane rebuilding a ruin
        /// <summary>A crane rebuilds a destroyed bridge, radar, repair station or factory (it drives into the ruin and back out,
        /// ProcessCraneRepairWP) while a red sniper squad attacks it. Rule off: its driver fires while it works and cannot be hit (the game
        /// until now). Rule on: while it works he stays inside (no pop-out, no shot, no driver hit) and every job is finished; after the
        /// job, outside a ruin and a DV82 spot, he comes up and can be sniped as before.</summary>
        static void Ruins(string settings, StringBuilder sb)
        {
            bool safeWas = ZWorld.SnipeSafeSpotDeviation;
            try
            {
                ZWorld.SnipeSafeSpotDeviation = true;
                foreach (bool on in new[] { false, true })
                {
                    SetRule(on);
                    var w = NewWorld(settings, BridgeMap, 15);
                    var br = w.Objects.First(o => o.IsBridge);
                    Ruin(w, br);
                    br.GetCraneEntrance(out int ex, out int ey, out _, out _);
                    var crane = OzV11Kit.Place(w, "crane", ex, ey - 150, Team.Blue); crane.MaxHealth = crane.Health = Big;
                    CraneOrder(w, crane, br);
                    var jw = new JobWatch(crane); ZObject sn = null;
                    while (w.Time < 60 && (jw.phase < 2 || w.Time < jw.off + 2))
                    {
                        jw.Tick(w);
                        if (jw.phase >= 1 && sn == null) sn = Snipers(w, crane, Gap);                         // the job has begun: snipers 80 px east of the crane
                    }
                    bool rebuilt = !br.Destroyed, spot = w.SnipeSafeSpot(crane);
                    string what = $"a crane rebuilds a destroyed bridge (job {jw.on:0.0}-{jw.off:0.0} s) while a sniper squad attacks it from {Gap} px, rule {(on ? "on" : "off")}: while it works {jw.AtWork}; the bridge rebuilt {rebuilt}, the crane {jw.ownerAtOff} at the job's end";
                    if (!on) sb.AppendLine(OzV11Kit.Verdict(jw.on >= 0 && jw.upTicks[1] > 0 && jw.shots[1] > 0 && jw.driverHits[1] == 0 && rebuilt,
                        "B4 " + what + " (the game until now: he fires from the ruin while it works and cannot be hit)"));
                    else sb.AppendLine(OzV11Kit.Verdict(jw.on >= 0 && jw.off > jw.on && jw.QuietAtWork && rebuilt && jw.ownerAtOff == Team.Blue &&
                                                        spot && jw.upTicks[2] == 0 && jw.shots[2] == 0 && jw.driverHits[2] == 0 && crane.Owner == Team.Blue,
                        "B5 " + what + $"; the 2 s after it at the end of the standing bridge (DV82 spot {spot}): driver up {jw.upTicks[2]} ticks, shots {jw.shots[2]}, driver hits {jw.driverHits[2]}, the crane {crane.Owner} (no pop-out while it works, never emptied on the deck)"));
                }
                SetRule(true);
                int runs = 0, early = 0, done = 0, rebuiltN = 0, up1 = 0, shots1 = 0, snip1 = 0, hits1 = 0, sniped = 0, inBox = 0, taken = 0, offOnce = 0;
                var per = new StringBuilder();
                foreach (var kind in new[] { "radar", "repair", "vehicle_factory", "robot_factory" })
                {
                    int kSn = 0, kRuns = 0, kDone = 0; double jobLen = 0;
                    string map = OzV11Kit.Plain(96, 96, ("building:" + kind, 40, 30, "null"));
                    for (int k = 0; k < 6; k++)
                    {   // the snipers come 0-2.5 s into the job; every crane emptied after its job must be one a robot can walk to and take
                        var w = NewWorld(settings, map, 16 + k);
                        var bld = w.Objects.First(o => o.Kind == ZKind.Building && o.Type == kind);
                        Ruin(w, bld);
                        var crane = OzV11Kit.Place(w, "crane", bld.CenterX, bld.LocY + bld.HeightPix + 140, Team.Blue); crane.MaxHealth = crane.Health = Big;
                        CraneOrder(w, crane, bld);
                        var jw = new JobWatch(crane); ZObject sn = null;
                        while (w.Time < 60 && jw.sniped < 0 && (jw.phase < 2 || w.Time < jw.off + 20))
                        {
                            jw.Tick(w);
                            if (jw.phase >= 1 && sn == null && w.Time >= jw.on + 0.5 * k) { sn = Snipers(w, crane, Gap); if (jw.phase == 1) early++; }
                        }
                        if (jw.on < 0) continue;
                        runs++; kRuns++;
                        for (int i = 0; i < 4; i++) jw.Tick(w);
                        if (jw.phase == 2) { done++; kDone++; jobLen += jw.off - jw.on; }
                        if (jw.offEvents == 1) offOnce++;
                        if (!bld.Destroyed) rebuiltN++;
                        up1 += jw.upTicks[1]; shots1 += jw.shots[1]; snip1 += jw.snipableTicks[1]; hits1 += jw.driverHits[1];
                        if (jw.sniped < 0) continue;
                        sniped++; kSn++;
                        if (crane.CenterX > bld.LocX && crane.CenterX < bld.LocX + bld.WidthPix && crane.CenterY > bld.LocY && crane.CenterY < bld.LocY + bld.HeightPix) inBox++;
                        if (Taken(w, crane, sn, bld.CenterX - 60, bld.LocY + bld.HeightPix + 70)) taken++;
                    }
                    per.Append($"{kind}: job {jobLen / Math.Max(1, kDone):0.0} s, sniped after it {kSn}/{kRuns}; ");
                }
                sb.AppendLine(OzV11Kit.Verdict(runs == 24 && early >= 18 && done == runs && offOnce == runs && rebuiltN == runs && up1 == 0 && shots1 == 0 && snip1 == 0 && hits1 == 0 &&
                                               sniped > 0 && inBox == 0 && taken == sniped,
                    $"B6 rule on: a crane rebuilds a destroyed radar, repair station, vehicle factory and robot factory, 6 runs each, snipers 0-2.5 s into the job ({runs} runs, the snipers there while it worked in {early}): " +
                    $"while it works driver up {up1} ticks, crane shots {shots1}, snipable {snip1} ticks, driver hits {hits1}; every job finished {done} / {runs} (CraneAnim off once {offOnce}), the building rebuilt {rebuiltN} / {runs}; " +
                    $"after the job (outside work, as before) sniped {sniped} times, {inBox} of them inside the ruin, a red grunt took every emptied crane ({taken} of {sniped}); {per}"));
            }
            finally { ZWorld.SnipeSafeSpotDeviation = safeWas; SetRule(false); }
        }

        // ================================================================================================ W: the driver at the job's start, an order in the middle of a job
        /// <summary>W1: a grunt squad attacks a crane on its way to mend a damaged radar: before the job the driver is up, fires and can be
        /// sniped (as before); the tick the job begins he is down and the crane not snipable; while it works never up again; the job ends.
        /// Also in the quick form (the parity report's row).</summary>
        static void UpAtJobStart(string settings, StringBuilder sb)
        {
            bool safeWas = ZWorld.SnipeSafeSpotDeviation;
            try
            {
                ZWorld.SnipeSafeSpotDeviation = true;
                SetRule(true);
                {
                    var w = NewWorld(settings, RadarMap, 30);
                    var radar = w.Objects.First(o => o.Kind == ZKind.Building && o.Type == "radar");
                    radar.SetHealth(radar.MaxHealth / 2);
                    var crane = OzV11Kit.Place(w, "crane", radar.CenterX, radar.LocY + radar.HeightPix + 120, Team.Blue); crane.MaxHealth = crane.Health = Big; crane.Drivers[0] = Big;
                    var g = OzV11Kit.Place(w, "grunt", crane.CenterX + Gap, crane.CenterY - 40, Team.Red);
                    foreach (var m in OzV11Kit.Squad(g)) m.MaxHealth = m.Health = Big;
                    OzV11Kit.PlayerAttack(w, g, crane);
                    CraneOrder(w, crane, radar);
                    var jw = new JobWatch(crane);
                    bool wasUp = false, upAtStart = false, downAtStart = false, snipableAtStart = true;
                    while (w.Time < 40 && jw.phase < 2)
                    {
                        int before = jw.phase;
                        jw.Tick(w);
                        if (before == 0 && jw.phase >= 1) { upAtStart = wasUp; downAtStart = !crane.CraneGunUp; snipableAtStart = crane.CanBeSniped(); }
                        wasUp = crane.CraneGunUp;
                    }
                    for (int i = 0; i < 4; i++) jw.Tick(w);
                    bool mended = radar.Health >= radar.MaxHealth;
                    sb.AppendLine(OzV11Kit.Verdict(jw.upTicks[0] > 0 && jw.shots[0] > 0 && jw.snipableTicks[0] > 0 && upAtStart && downAtStart && !snipableAtStart &&
                                                   jw.QuietAtWork && jw.phase == 2 && mended && crane.Owner == Team.Blue,
                        $"W1 rule on: a grunt squad attacks a crane on its way to mend a damaged radar: before the job (outside work, as before) driver up {jw.upTicks[0]} ticks, crane shots {jw.shots[0]}, snipable {jw.snipableTicks[0]} ticks, driver hits {jw.driverHits[0]}; " +
                        $"the job begins at {jw.on:0.00} s with the driver up {upAtStart}: in that tick he is down {downAtStart}, the crane snipable {snipableAtStart}; while it works {jw.AtWork}; the job ends at {jw.off:0.00} s, the radar mended {mended}"));
                }
            }
            finally { ZWorld.SnipeSafeSpotDeviation = safeWas; SetRule(false); }
        }

        /// <summary>W2: a crane rebuilding a destroyed bridge or radar under sniper fire is sent back (a move to where it came from) in the
        /// middle of the job: the order waits for the job's end (a begun crane job is not overwritten: ZObject.CanOverwriteWP, Zod), the
        /// job is finished; while its box is still in the ruin (the deck and 88 px before a bridge's ends, a building's box) the driver
        /// stays inside and is never hit; out of it he comes up and is sniped as before, and a red grunt takes the empty crane.</summary>
        static void OrderMidJob(string settings, StringBuilder sb)
        {
            bool safeWas = ZWorld.SnipeSafeSpotDeviation;
            try
            {
                ZWorld.SnipeSafeSpotDeviation = true;
                SetRule(true);
                int calls = 0, mid = 0, queued = 0, sniped = 0, inRuin = 0, hitsInRuin = 0, upInRuin = 0, taken = 0, done = 0;
                var where = new List<string>();
                foreach (var kind in new[] { "bridge", "radar" })
                    for (int k = 0; k < 6; k++)
                    {
                        var w = NewWorld(settings, kind == "bridge" ? BridgeMap : RadarMap, 40 + k);
                        var b = kind == "bridge" ? w.Objects.First(o => o.IsBridge) : w.Objects.First(o => o.Kind == ZKind.Building && o.Type == "radar");
                        Ruin(w, b);
                        int sx, sy;
                        if (kind == "bridge") { b.GetCraneEntrance(out int ex, out int ey, out _, out _); sx = ex; sy = ey - 150; }
                        else { sx = b.CenterX; sy = b.LocY + b.HeightPix + 140; }
                        var crane = OzV11Kit.Place(w, "crane", sx, sy, Team.Blue); crane.MaxHealth = crane.Health = Big;
                        CraneOrder(w, crane, b);
                        var jw = new JobWatch(crane); ZObject sn = null; double callAt = -1, callIn = kind == "bridge" ? 1.0 + k : 0.25 + 0.4 * k;
                        bool snipedInRuin = false;
                        while (w.Time < 80 && jw.sniped < 0 && (callAt < 0 || w.Time < callAt + 30))
                        {
                            jw.Tick(w);
                            if (callAt >= 0)
                            {
                                bool inside = InRuin(crane, b);
                                if (inside) { hitsInRuin += jw.tickDriverHits; if (crane.CraneGunUp) upInRuin++; if (jw.sniped >= 0) snipedInRuin = true; }
                            }
                            if (jw.phase >= 1 && sn == null) sn = Snipers(w, crane, Gap);
                            if (callAt < 0 && jw.phase >= 1 && w.Time >= jw.on + callIn)
                            {   // sent back to where it came from: the order waits for the job's end
                                callAt = w.Time; calls++; if (jw.phase == 1) mid++;
                                w.SendWaypoints(Team.Blue, crane, new[] { ZWaypoint.Move(sx, sy, false) });
                                if (crane.Waypoints.Count == 2 && crane.Waypoints[0].mode == WpMode.CraneRepair && crane.Waypoints[1].mode == WpMode.Move) queued++;
                            }
                        }
                        if (jw.phase == 2) done++;
                        if (jw.sniped < 0) continue;
                        sniped++;
                        if (snipedInRuin) inRuin++;
                        where.Add($"{kind} {crane.CenterX - b.CenterX:+0;-0},{crane.CenterY - b.CenterY:+0;-0}");
                        if (Taken(w, crane, sn, crane.CenterX - 60, crane.CenterY)) taken++;
                    }
                sb.AppendLine(OzV11Kit.Verdict(calls == 12 && mid == calls && queued == calls && done == calls && sniped > 0 && inRuin == 0 && hitsInRuin == 0 && upInRuin == 0 && taken == sniped,
                    $"W2 rule on: a crane rebuilding a destroyed bridge / radar under sniper fire is sent back in the middle of the job ({calls} runs, {mid} of them while it worked): the order waits for the job's end ({queued} queued after the job, {done} jobs finished); " +
                    $"while its box is in the ruin driver up {upInRuin} ticks, driver hits {hitsInRuin}, emptied there {inRuin}; out of it he comes up and is sniped as before ({sniped} times; offsets from the ruin's centre {string.Join(" ", where)}), a red grunt took every emptied crane ({taken} of {sniped})"));
            }
            finally { ZWorld.SnipeSafeSpotDeviation = safeWas; SetRule(false); }
        }

        // ================================================================================================ D: deterministic
        static void Twice(string settings, StringBuilder sb)
        {
            SetRule(true);
            var a = Fight(settings, "jeep", Gap, 3, SeedBase + 500);
            var b = Fight(settings, "jeep", Gap, 3, SeedBase + 500);
            SetRule(false);
            sb.AppendLine(OzV11Kit.Verdict(a.hash == b.hash && a.sniped == b.sniped && Math.Abs(a.time - b.time) < 1e-9,
                $"D1 deterministic: three jeep fights run twice, the same events ({a.hash:x16} / {b.hash:x16}), the same snipes ({a.sniped} / {b.sniped}) at the same times"));
        }

        /// <summary>For the menu: the verdict in one line ("PASS 30 / 30 lines", or "FAIL" with the failed lines).</summary>
        public static string Summary(string report)
        {
            var lines = (report ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("PASS  ") || l.StartsWith("FAIL  ")).ToList();
            var failed = lines.Where(l => l.StartsWith("FAIL")).ToList();
            return lines.Count == 0 ? "FAIL no verdict line" : failed.Count == 0 ? $"PASS {lines.Count} / {lines.Count} lines" : $"FAIL {failed.Count} of {lines.Count} lines: " + string.Join("; ", failed.Select(l => l.Substring(6)));
        }

        /// <summary>ZWorld.CraneDriverHitFloor read by name (0 = the rule is not in this build).</summary>
        static int Floor => typeof(ZWorld).GetField("CraneDriverHitFloor") is System.Reflection.FieldInfo f ? Convert.ToInt32(f.GetValue(null)) : 0;
    }
}
