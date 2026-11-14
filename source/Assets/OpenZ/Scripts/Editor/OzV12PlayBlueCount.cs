using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 11, EXPERIMENTAL PLAY AS BLUE: the counted check of the side swap, headless (no scene, no Play, nothing saved; menu
    /// OpenZ/V1.2/Play As Blue: 11 Sides Check (headless), Editor/OzPlayBlueMenu.cs; the same class runs outside the editor). It builds
    /// each match the way View/ZGame.cs Awake does (the Play session's rule switches with the default settings, ZWorld.Create, the CPU of
    /// each side the setup names with its seed and difficulty, the tech bonus, the head start), with the sides of View/OzSides.cs, and
    /// gives the human team's orders with a scripted player through the player's own command (ZWorld.SendWaypoints with the human's team).
    ///  A  the side rules: a normal start is Z's (red human, blue CPU: ZGame's scene values), PLAY AS BLUE gives blue to the human and red
    ///     to the CPU with the same difficulty rule, a bot match ignores the box; the end word, the colour words, the start shuttles, the
    ///     start camera's offset.
    ///  B  per level (2, 16, 17), the human blue, MEDIUM (the red CPU the Zod bot, blue's build lists +2), HEAD START on: only red has a
    ///     CPU; blue is the human team (calm start, orders); the head start stands at the blue fort; orders: blue's own units only; the
    ///     SHUTTLE box's ship by the BLUE fort on levels 16 and 17 (DV144; the red fort's spots empty; level 17's two empty car-park
    ///     ships); the red CPU builds, orders and
    ///     attacks; a whole match with the scripted blue player runs to its end (a winner, the loser's fort or army gone, the end word
    ///     right for the blue human, no exception, the same game twice); a match where blue gives no orders ends with red's win (DEFEAT for
    ///     the blue human); the red fort taken down = VICTORY, the blue fort taken down = DEFEAT (the rules' own fort death).
    ///  C  the off state: a normal start (the box off) builds the same world as the setup before the kit, tick for tick (the hash of
    ///     every tick of a whole normal match: the scripted red human against the blue CPU, and a bot match), on each level.
    /// Test only.
    /// </summary>
    public static class OzV12PlayBlueCount
    {
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        /// <summary>The head start of each level as View/ZGame.cs has it (HeadStartTypes, HeadStartL16Types, EasyL17Types; level 17 parked
        /// empty, DV32). The editor menu compares them with ZGame's own lists (a mismatch is a FAIL line there).</summary>
        public static readonly string[] HeadL02 = { "light", "light", "jeep", "jeep", "medium" }, HeadL16 = { "jeep", "jeep", "light" }, HeadL17 = { "missile_launcher", "heavy" };
        public static string[] HeadTypes(string level) => level == "L02" ? HeadL02 : level == "L16" ? HeadL16 : level == "L17" ? HeadL17 : new string[0];
        /// <summary>The longest match (simulation seconds) and the scripted player's rush time (it breaks into the enemy fort from then on,
        /// or as soon as every flag is its own).</summary>
        public const double MatchCap = 3600.0, PassiveCap = 5400.0, RushAt = 300.0, OffSeconds = 600.0;
        public const int Seed = 1, MediumTech = 2;
        const double Tick = ZConst.TICK;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F0(double v) => v.ToString("0", Inv);

        // ================================================================================================ the match set-up
        /// <summary>The rule switches of a Play session with the default settings (View/ZGame.cs Awake: not 'Zod exact', the chosen
        /// difficulty, the start shuttles of the sides), on top of OzV11Kit.GameSwitches (the caller disposes that one). Each switch is set
        /// by name, so a rule that is not in this build is simply left out.</summary>
        public static void PlaySession(string level, bool easy, bool hard, OzSideSetup s)
        {
            string[] on = { "SnipeDeviation", "FortGunDeviation", "ApcCrewDeviation", "DuelDeviation", "VeteranDeviation", "ToughPrepareDeviation", "FormationDeviation",
                "ClearanceDeviation", "LosDeviation", "FortMortarDeviation", "BuildingReachDeviation", "ExposedDriverDeviation", "ApproachDeviation", "NoTankDuelVsJeep",
                "HeavyPairDeviation", "CraneGunDeviation", "HarbourWrecksDeviation", "L16PyramidsDeviation", "L16ShuttleDeviation", "ShuttlePlayableDeviation",
                "L17ShuttlesDeviation", "PyroForceBurnDeviation", "DetailPropsDeviation", "FortRushDeviation", "BoardNearestDeviation", "ParkedPathDeviation",
                "CarParkBotDeviation", "GrabPolicyDeviation", "GrenadeRuleDeviation", "GrenadeBoostDeviation", "FortDefenderDeviation", "PathV3Deviation",
                "ElectrobotDeviation", "ElectrobotBotDeviation", "ElectrobotEscortDeviation", "KnockHoldDeviation", "RunOverDeviation", "JeepChargeDeviation",
                "RocketSpeedDeviation", "HelpDeviation", "CaptureRunDeviation", "CloseGrabDeviation", "BotCommitDeviation", "BotLaserDeviation", "BotHeavyDeviation",
                "BotFinishDeviation", "RepairInPlaceDeviation", "CraneFixDeviation", "CraneAnyRuinDeviation", "RepairBayFireDeviation", "UnmannedStopDeviation",
                "DriverFireDeviation", "HeroSquadDeviation", "HeroBoostDeviation", "HeroPlusDeviation", "GrabCertainDeviation", "ElectroSteadyDeviation", "SnipeSafeSpotDeviation",
                "RiflePairDeviation", "FireOnMoveDeviation", "EjectLockDeviation", "EnterFallbackDeviation", "ExitLanesDeviation", "StrikeGroupDeviation", "CalmMoveDeviation",
                "FortRunDeviation", "RockRoundDeviation", "CalmFollowDeviation", "SniperHeadshotDeviation", "SniperSteadyDeviation", "ToughSteadyDeviation",
                "NoAutoFlagUnderFireDeviation", "HeavyFallDeviation", "GroundFireDeviation", "SniperBalanceDeviation", "ProneTargetDeviation", "SideShuttlesDeviation" };
            foreach (var n in on) OzV11Kit.TrySet(typeof(ZWorld), n, true);
            OzV11Kit.TrySet(typeof(ZWorld), "ArmourChipDeviation", false);
            OzV11Kit.TrySet(typeof(ZWorld), "OvertakeDeviation", false);                     // the Options row FAST UNITS PASS SLOW ONES: off by default
            OzV11Kit.TrySet(typeof(ZWorld), "ElectrobotAllLevels", false);
            OzV11Kit.TrySet(typeof(ZPathing), "VehicleFirst", true);
            ZWorld.SetCrocodileRules(true, true, true, true, true);
            // the start shuttles (UI/OzShuttleStart.cs with the box on, its default; the sides' rule of View/OzSides.cs)
            bool box = !hard;
            ZWorld.ShuttleOn = OzSides.StartShuttle(level, s.PlayBlue, level != "L16" || box);
            ZWorld.L17StartShuttle = OzSides.FortShuttle(s.PlayBlue, level == "L17" && box);
            ZWorld.ShuttleTeam = s.Demo ? Team.Red : s.Human;
            ZWorld.L17CrewsDeviation = easy && !s.Demo; ZWorld.L17CrewsTeam = s.Demo ? Team.Red : s.Human;
            ZWorld.FortDoorGunDeviation = hard && !s.Demo;
        }

        /// <summary>One match as View/ZGame.cs Awake builds it: the world, the CPU of each side the setup names (blue first, seed + 101; red
        /// with the seed), the tech bonus of the human, then the head start at the human's fort.</summary>
        public static ZWorld NewMatch(string settings, string map, string level, OzSideSetup s, ZBotDifficulty chosen, int techBonus, bool headStart, int seed)
        {
            var w = ZWorld.Create(settings, map, seed, level == "L17");
            if (s.BlueBot) w.Bots.Add(new ZBot(w, Team.Blue, seed + 101) { Difficulty = OzSides.BotDifficulty(Team.Blue, s, chosen) });
            if (s.RedBot) w.Bots.Add(new ZBot(w, Team.Red, seed) { Difficulty = OzSides.BotDifficulty(Team.Red, s, chosen) });
            for (int i = 0; i < w.TechBonus.Length; i++) w.TechBonus[i] = 0;
            var tb = OzSides.TechBonusTeam(s); if (tb != Team.Neutral) w.TechBonus[(int)tb] = techBonus;
            if (headStart && !s.Demo)
            {
                if (level == "L02") w.AddHeadStart(s.Human, HeadL02);
                else if (level == "L17") w.AddHeadStart(s.Human, true, HeadL17);
                else if (level == "L16") w.AddHeadStart(s.Human, HeadL16);
            }
            w.Events.Clear();
            return w;
        }

        /// <summary>The setup a normal start had before this kit (ZGame's scene values, written out): the human red, the blue CPU.</summary>
        public static OzSideSetup Before => new OzSideSetup { Human = Team.Red, Cpu = Team.Blue, BlueBot = true, RedBot = false };

        // ================================================================================================ the scripted human
        /// <summary>Every second of the match: each of the human's own idle units (robot squads by their leader, vehicles) gets the order the
        /// player would give with a right click (ZWorld.SendWaypoints, the human's team): the nearest flag that is not ours, and from
        /// <paramref name="rushAt"/> on (or once every flag is ours) the enemy fort (break into it). Deterministic: nearest first, ties by id.
        /// Returns the orders the rules took.</summary>
        public static int ScriptedOrders(ZWorld w, Team me, double rushAt)
        {
            if (w.TickIndex % 100 != 0 || w.GameOver) return 0;
            var enemy = OzSides.EnemyOf(me);
            ZObject fort = null; long fd = long.MaxValue;
            var flags = new List<ZObject>();
            foreach (var o in w.Objects)
            {
                if (o.Destroyed) continue;
                if (o.Kind == ZKind.MapItem && o.Type == "flag" && o.Owner != me) flags.Add(o);
            }
            bool rush = w.Time >= rushAt || flags.Count == 0;
            int sent = 0;
            foreach (var u in w.Objects.ToList())
            {
                if (u.Owner != me || !u.IsMobile || u.IsMinion || u.Destroyed || u.KillAt >= 0 || u.IsFortDefender || u.IsShuttle || u.Waypoints.Count > 0) continue;
                ZObject t = null; long best = long.MaxValue;
                if (!rush)
                    foreach (var f in flags)
                    {
                        long dx = f.CenterX - u.CenterX, dy = f.CenterY - u.CenterY, d = dx * dx + dy * dy;
                        if (d < best || (d == best && t != null && f.Id < t.Id)) { best = d; t = f; }
                    }
                if (t == null)
                {
                    fort = null; fd = long.MaxValue;
                    foreach (var o in w.Objects)
                    {
                        if (!o.IsFort || o.Destroyed || o.Owner != enemy) continue;
                        long dx = o.CenterX - u.CenterX, dy = o.CenterY - u.CenterY, d = dx * dx + dy * dy;
                        if (d < fd || (d == fd && fort != null && o.Id < fort.Id)) { fd = d; fort = o; }
                    }
                    if (fort == null) continue;
                    if (w.SendWaypoints(me, u, new[] { new ZWaypoint { mode = WpMode.EnterFort, refId = fort.Id, x = fort.CenterX, y = fort.CenterY } })) sent++;
                }
                else if (w.SendWaypoints(me, u, new[] { ZWaypoint.Move(t.CenterX, t.CenterY, true) })) sent++;
            }
            return sent;
        }

        // ================================================================================================ the fingerprint
        static ulong Fnv(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }

        /// <summary>A fingerprint of the world after a tick: every object's id, owner, place, health, death time, waypoints and target, and
        /// the tick's events (the counted checks' match hash).</summary>
        public static ulong TickHash(ZWorld w, ulong h)
        {
            h = Fnv(h, w.Objects.Count); h = Fnv(h, BitConverter.DoubleToInt64Bits(w.Time)); h = Fnv(h, w.GameOver ? 1 : 0);
            foreach (var o in w.Objects)
            {
                h = Fnv(h, o.Id); h = Fnv(h, (long)o.Owner); h = Fnv(h, o.LocX); h = Fnv(h, o.LocY); h = Fnv(h, o.Health); h = Fnv(h, BitConverter.DoubleToInt64Bits(o.KillAt));
                h = Fnv(h, o.Waypoints.Count);
                foreach (var p in o.Waypoints) { h = Fnv(h, (long)p.mode); h = Fnv(h, p.refId); h = Fnv(h, p.x); h = Fnv(h, p.y); h = Fnv(h, (p.attackTo ? 1 : 0) | (p.playerGiven ? 2 : 0)); }
                h = Fnv(h, o.AttackObject != null ? o.AttackObject.Id : -1);
            }
            foreach (var e in w.Events) { h = Fnv(h, (long)e.type); h = Fnv(h, e.objId); h = Fnv(h, e.otherId); h = Fnv(h, e.x); h = Fnv(h, e.y); h = Fnv(h, e.flag ? 1 : 0); }
            return h;
        }

        // ================================================================================================ one whole match
        public sealed class Run
        {
            public string Level, Script; public OzSideSetup Sides; public ulong Hash = 1469598103934665603UL;
            public bool Over, Threw; public Team Winner = Team.Neutral; public double End; public string Error = "";
            public int HumanOrders, HumanOrdersTaken, CpuOrders, CpuHostile, CpuBuilt, HumanBuilt, CpuFlags, HumanFlags, Eliminated = -1;
            public bool LoserFortDown, LoserArmyGone;
            public override string ToString() =>
                $"{Level} {Script}: {(Threw ? "THREW " + Error : Over ? $"ended at {F0(End)} s, {Winner} won ({OzSides.Outcome(Winner, Sides.Human).ToUpperInvariant()} for the {Sides.Human} human)" : $"no end within {F0(End)} s")}; " +
                $"human orders {HumanOrdersTaken}; CPU orders {CpuOrders} ({CpuHostile} attack / break-in), CPU built {CpuBuilt}, human built {HumanBuilt}; flags taken CPU {CpuFlags} / human {HumanFlags}; " +
                $"loser's fort down {(LoserFortDown ? "yes" : "no")}, loser's army gone {(LoserArmyGone ? "yes" : "no")}; hash {Hash:x16}";
        }

        /// <summary>A whole match: <paramref name="script"/> "attack" (the scripted human), "none" (the human gives no orders) or "cpu" (a bot
        /// match). Ticks until the game is over or <paramref name="cap"/> simulation seconds; every tick goes into the hash.</summary>
        public static Run Play(string settings, string map, string level, OzSideSetup s, ZBotDifficulty chosen, int tech, bool headStart, string script, double cap, int seed = Seed)
        {
            var r = new Run { Level = level, Script = script, Sides = s };
            try
            {
                var w = NewMatch(settings, map, level, s, chosen, tech, headStart, seed);
                while (!w.GameOver && w.Time < cap)
                {
                    if (script == "attack" && !s.Demo) r.HumanOrdersTaken += ScriptedOrders(w, s.Human, RushAt);
                    w.Tick(Tick);
                    r.Hash = TickHash(w, r.Hash);
                    foreach (var e in w.Events)
                    {
                        if (e.type == ZEventType.UnitBuilt) { var u = w.GetAny(e.objId); if (u != null) { if (u.Owner == s.Cpu) r.CpuBuilt++; else if (u.Owner == s.Human) r.HumanBuilt++; } }
                        else if (e.type == ZEventType.FlagCaptured) { var t = (Team)(int)e.value; if (t == s.Cpu) r.CpuFlags++; else if (t == s.Human) r.HumanFlags++; }
                        else if (e.type == ZEventType.TeamEliminated) r.Eliminated = (int)e.value;
                    }
                    w.Events.Clear();
                }
                r.Over = w.GameOver; r.Winner = w.Winner; r.End = w.Time;
                foreach (var b in w.Bots) if (b.Team == s.Cpu || s.Demo) { r.CpuOrders += b.Orders; r.CpuHostile += Get(b.OrdersByMode, WpMode.Attack) + Get(b.OrdersByMode, WpMode.EnterFort); }
                if (r.Over && r.Winner != Team.Neutral)
                {
                    var loser = OzSides.EnemyOf(r.Winner);
                    r.LoserFortDown = r.Eliminated == (int)loser || !w.Objects.Any(o => o.IsFort && o.Owner == loser && !o.Destroyed);
                    r.LoserArmyGone = !w.Objects.Any(o => o.IsUnit && o.Owner == loser && !o.Destroyed && o.KillAt < 0);
                }
            }
            catch (Exception ex) { r.Threw = true; r.Error = ex.GetType().Name + ": " + ex.Message; }
            return r;
        }

        static int Get(Dictionary<WpMode, int> d, WpMode m) => d != null && d.TryGetValue(m, out int n) ? n : 0;

        /// <summary>The rules' own fort death for every fort part of <paramref name="team"/> (OpenZVictory.DebugFortFalls, the gate battery's
        /// fort_falls stage), then ticks until the game is over (at most 30 s). Returns the winner (Neutral: no end).</summary>
        public static Team FortFalls(ZWorld w, Team team, out bool eliminated)
        {
            eliminated = false;
            foreach (var o in w.Objects.ToList())
                if (o.IsFort && !o.Destroyed && o.Owner == team) { o.DamageHealth(o.Health + 1); w.UpdateObjectHealth(o, -1); }
            double t0 = w.Time;
            while (!w.GameOver && w.Time - t0 < 30.0)
            {
                w.Tick(Tick);
                foreach (var e in w.Events) if (e.type == ZEventType.TeamEliminated && (int)e.value == (int)team) eliminated = true;
                w.Events.Clear();
            }
            return w.GameOver ? w.Winner : Team.Neutral;
        }

        // ================================================================================================ the check
        static int pass, fail;
        static StringBuilder sb;
        static void V(bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); }
        static void Note(string what) => sb.AppendLine("      " + what);

        /// <summary>The whole check; <paramref name="map"/> gives a level's map file. <paramref name="quick"/>: parts A and B without the
        /// whole matches (the parity report's row). Every switch is put back afterwards.</summary>
        public static string Check(string settings, Func<string, string> map, bool quick = false, Action<string> progress = null)
        {
            sb = new StringBuilder(); pass = fail = 0;
            var t0 = DateTime.Now;
            sb.AppendLine("1.2 item 11 PLAY AS BLUE (experimental): the side swap, counted headless (OzV12PlayBlueCount)");
            Team keepShuttle = ZWorld.ShuttleTeam, keepCrews = ZWorld.L17CrewsTeam;
            try
            {
                using (OzV11Kit.GameSwitches())
                {
                    PartA();
                    foreach (var lv in Levels) { progress?.Invoke("B " + lv); PartB(settings, map(lv), lv, quick); }
                    if (!quick) foreach (var lv in Levels) { progress?.Invoke("C " + lv); PartC(settings, map(lv), lv); }
                }
            }
            catch (Exception ex) { V(false, "the check threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { ZWorld.ShuttleTeam = keepShuttle; ZWorld.L17CrewsTeam = keepCrews; }
            sb.AppendLine($"RESULT {pass} PASS, {fail} FAIL ({(DateTime.Now - t0).TotalSeconds:0} s)");
            return sb.ToString();
        }

        static void PartA()
        {
            sb.AppendLine("A  the side rules (View/OzSides.cs)");
            var n = OzSides.For(false, false); var b = OzSides.For(true, false); var d = OzSides.For(true, true);
            V(n.Human == Team.Red && n.Cpu == Team.Blue && n.BlueBot && !n.RedBot && !n.PlayBlue, "a normal start is Z's and the scene's: the human RED, the CPU drives BLUE (blueBot on, redBot off): " + n);
            V(b.Human == Team.Blue && b.Cpu == Team.Red && !b.BlueBot && b.RedBot && b.PlayBlue, "PLAY AS BLUE: the human BLUE, the CPU drives RED (blueBot off, redBot on): " + b);
            V(d.Demo && d.BlueBot && d.RedBot && !d.PlayBlue && OzSides.For(false, true).Demo, "a bot match: both teams CPU, the box never applies: " + d);
            V(OzSides.BotDifficulty(Team.Red, b, ZBotDifficulty.Easy) == ZBotDifficulty.Easy && OzSides.BotDifficulty(Team.Blue, n, ZBotDifficulty.Easy) == ZBotDifficulty.Easy
              && OzSides.BotDifficulty(Team.Blue, b, ZBotDifficulty.Easy) == ZBotDifficulty.Normal && OzSides.BotDifficulty(Team.Red, d, ZBotDifficulty.Easy) == ZBotDifficulty.Normal,
              "the difficulty rule follows the side: the CPU against the human gets the chosen one (red as blue's enemy, blue as red's), any other CPU and a bot match Normal");
            V(OzSides.TechBonusTeam(b) == Team.Blue && OzSides.TechBonusTeam(n) == Team.Red && OzSides.TechBonusTeam(d) == Team.Neutral, "the build-list bonus goes to the human's side (blue as blue, red as red, nobody in a bot match)");
            V(OzSides.Outcome(Team.Blue, Team.Blue) == "victory" && OzSides.Outcome(Team.Red, Team.Blue) == "defeat" && OzSides.Outcome(Team.Neutral, Team.Blue) == "draw"
              && OzSides.Outcome(Team.Red, Team.Red) == "victory" && OzSides.Outcome(Team.Blue, Team.Red) == "defeat", "the end word follows the human's side (blue wins = VICTORY as blue, red wins = DEFEAT as blue, and as before for red)");
            string h = OzSides.FlipColourWords("BREAK INTO THE BLUE FORT"), g = OzSides.FlipColourWords("Capture the flags. Break into the blue fort."), m = OzSides.FlipColourWords("Reds and Blues, red BLUE");
            V(h == "BREAK INTO THE RED FORT" && g == "Capture the flags. Break into the red fort." && m == "Blues and Reds, blue RED" && OzSides.FlipColourWords(h) == "BREAK INTO THE BLUE FORT",
              $"the colour words trade places, case kept: \"{h}\", \"{g}\", \"{m}\"; twice = as before");
            V(OzSides.StartShuttle("L16", true, true) && OzSides.StartShuttle("L16", false, true) && !OzSides.StartShuttle("L16", false, false) && !OzSides.StartShuttle("L16", true, false) && OzSides.StartShuttle("L17", true, true) && OzSides.StartShuttle("L02", true, true)
              && OzSides.FortShuttle(true, true) && OzSides.FortShuttle(false, true) && !OzSides.FortShuttle(true, false),
              "start shuttles: the box decides for both sides (1.2 item 111, DV144: a blue player's ship by the blue fort)");
            OzSides.StartFocus(104, -226.6, 94, -222, 94, -22, out double fx, out double fz);
            V(Math.Abs(fx - 104) < 1e-9 && Math.Abs(fz - (-26.6)) < 1e-9, $"the start camera: the scene's framing of the red fort moved onto the human's fort (offset kept: focus {fx:0.0} / {fz:0.0})");
        }

        static void PartB(string settings, string mapJson, string level, bool quick)
        {
            sb.AppendLine($"B  level {level}, PLAY AS BLUE, MEDIUM (the red CPU is the Zod bot, blue's build lists +{MediumTech}), HEAD START on");
            var s = OzSides.For(true, false);
            PlaySession(level, false, false, s);
            var w = NewMatch(settings, mapJson, level, s, ZBotDifficulty.Normal, MediumTech, true, Seed);
            V(w.Bots.Count == 1 && w.Bots[0].Team == Team.Red && w.Bots[0].Difficulty == ZBotDifficulty.Normal && w.IsHumanTeam(Team.Blue) && !w.IsHumanTeam(Team.Red),
              $"{level}: one CPU, red ({(w.Bots.Count > 0 ? w.Bots[0].Difficulty.ToString() : "none")}); blue is the human team, red is not");
            V(w.TechBonus[(int)Team.Blue] == MediumTech && w.TechBonus[(int)Team.Red] == 0, $"{level}: blue's build lists +{w.TechBonus[(int)Team.Blue]}, red's +{w.TechBonus[(int)Team.Red]}");
            V(w.CalmStart(Team.Blue) && !w.CalmStart(Team.Red), $"{level}: the calm start (the first {ZWorld.CalmStartSec:0} s) holds blue's own units, not the CPU's");
            // the head start at the blue fort
            var bf = Forts(w, Team.Blue); var rf = Forts(w, Team.Red);
            var types = HeadTypes(level); bool parked = level == "L17";
            var head = w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Source == null && !o.IsShuttle && types.Contains(o.Type)).ToList();
            int nearBlue = head.Count(o => Near(o, bf, 20 * 16)), nearRed = head.Count(o => Near(o, rf, 20 * 16));
            bool owners = head.All(o => parked ? o.Owner == Team.Neutral && o.Drivers.Count == 0 : o.Owner == Team.Blue);
            V(head.Count == types.Length && nearBlue == types.Length && nearRed == 0 && owners,
              $"{level}: HEAD START at the blue fort: {head.Count} of {types.Length} ({string.Join(", ", head.Select(o => o.Type))}), {nearBlue} within 20 tiles of the blue fort, {nearRed} of the red; {(parked ? "parked empty (DV32)" : "crewed, blue")}");
            // orders: own units only
            var blueUnit = w.Objects.FirstOrDefault(o => o.Owner == Team.Blue && o.IsMobile && !o.IsMinion && !o.IsFortDefender && !o.Destroyed);
            var redUnit = w.Objects.FirstOrDefault(o => o.Owner == Team.Red && o.IsMobile && !o.IsMinion && !o.IsFortDefender && !o.Destroyed);
            bool ownOk = blueUnit != null && w.SendWaypoints(Team.Blue, blueUnit, new[] { ZWaypoint.Move(blueUnit.CenterX + 32, blueUnit.CenterY, true) });
            bool redRefused = redUnit != null && !w.SendWaypoints(Team.Blue, redUnit, new[] { ZWaypoint.Move(redUnit.CenterX + 32, redUnit.CenterY, true) });
            bool crossRefused = blueUnit != null && !w.SendWaypoints(Team.Red, blueUnit, new[] { ZWaypoint.Move(blueUnit.CenterX - 32, blueUnit.CenterY, true) }) && blueUnit.Waypoints.Count > 0;
            int sel = w.OwnedSelectable(Team.Blue).Count();
            V(ownOk && redRefused && crossRefused && sel > 0, $"{level}: the blue player's order moves a blue unit ({(blueUnit != null ? blueUnit.Type : "none")}), an order for a red unit ({(redUnit != null ? redUnit.Type : "none")}) is refused, red cannot order a blue one; {sel} blue units selectable");
            blueUnit?.Waypoints.Clear();
            // the start shuttles
            int shuttles = w.Objects.Count(o => o.IsShuttle && !o.Destroyed);
            if (level == "L16") V(w.PlayableShuttle != null && w.PlayableShuttle == w.BlueStartShuttle && w.BlueStartShuttle.Owner == Team.Blue && w.L16ShuttleOptionOff && shuttles == 1,
                                  $"L16: the SHUTTLE box's ship stands by the blue fort ({(w.BlueStartShuttle != null ? "#" + w.BlueStartShuttle.Id + " tile " + (w.BlueStartShuttle.CenterX >> 4) + "," + (w.BlueStartShuttle.CenterY >> 4) : "missing")}), the red fort's pocket empty; shuttles on the map {shuttles} (DV144)");
            else if (level == "L17") V(w.L17FortShuttle == null && w.BlueStartShuttle != null && w.BlueStartShuttle.Owner == Team.Blue && w.PlayableShuttle == w.BlueStartShuttle && w.L17CarParkShuttle != null && w.L17CarParkShuttle.Owner == Team.Neutral
                                       && w.L17BlueCarParkShuttle != null && w.L17BlueCarParkShuttle.Owner == Team.Neutral && shuttles == 3,
                                       $"L17: the SHUTTLE box's ship by the blue fort, none by the red fort; the two empty car-park shuttles stand (red side {(w.L17CarParkShuttle != null ? w.L17CarParkShuttle.Owner.ToString() : "missing")}, blue side {(w.L17BlueCarParkShuttle != null ? w.L17BlueCarParkShuttle.Owner.ToString() : "missing")}); shuttles {shuttles} (DV144)");
            else V(shuttles == 0, $"L02: no shuttle ({shuttles})");
            // the forced endings: the rules' own fort death
            {
                PlaySession(level, false, false, s);
                var w1 = NewMatch(settings, mapJson, level, s, ZBotDifficulty.Normal, MediumTech, true, Seed);
                for (int i = 0; i < 500; i++) { w1.Tick(Tick); w1.Events.Clear(); }
                var win = FortFalls(w1, Team.Red, out bool elimR);
                PlaySession(level, false, false, s);
                var w2 = NewMatch(settings, mapJson, level, s, ZBotDifficulty.Normal, MediumTech, true, Seed);
                for (int i = 0; i < 500; i++) { w2.Tick(Tick); w2.Events.Clear(); }
                var lose = FortFalls(w2, Team.Blue, out bool elimB);
                V(win == Team.Blue && elimR && OzSides.Outcome(win, s.Human) == "victory" && lose == Team.Red && elimB && OzSides.Outcome(lose, s.Human) == "defeat",
                  $"{level}: the red fort down = {win} wins, {OzSides.Outcome(win, s.Human).ToUpperInvariant()} for the blue human; the blue fort down = {lose} wins, {OzSides.Outcome(lose, s.Human).ToUpperInvariant()}");
            }
            if (quick) return;
            // a whole match with the scripted blue player, twice (deterministic)
            PlaySession(level, false, false, s);
            var a = Play(settings, mapJson, level, s, ZBotDifficulty.Normal, MediumTech, true, "attack", MatchCap);
            PlaySession(level, false, false, s);
            var a2 = Play(settings, mapJson, level, s, ZBotDifficulty.Normal, MediumTech, true, "attack", MatchCap);
            Note(a.ToString());
            V(!a.Threw && a.Over && a.Winner != Team.Neutral && (a.LoserFortDown || a.LoserArmyGone), $"{level}: the match with the scripted blue player ran to its end without an exception: {a.Winner} won at {F0(a.End)} s ({OzSides.Outcome(a.Winner, s.Human).ToUpperInvariant()} for blue)");
            V(a.HumanOrdersTaken > 0 && a.HumanFlags > 0, $"{level}: the blue player's orders were taken ({a.HumanOrdersTaken}) and took flags for blue ({a.HumanFlags})");
            V(a.Hash == a2.Hash && a.End == a2.End, $"{level}: the same match twice is the same game (hash {a.Hash:x16} / {a2.Hash:x16})");
            // the blue side gives no orders: red wins
            PlaySession(level, false, false, s);
            var p = Play(settings, mapJson, level, s, ZBotDifficulty.Normal, MediumTech, false, "none", PassiveCap);
            Note(p.ToString());
            V(!p.Threw && p.Over && p.Winner == Team.Red && OzSides.Outcome(p.Winner, s.Human) == "defeat" && (p.LoserFortDown || p.LoserArmyGone),
              $"{level}: blue gives no orders: the red CPU wins at {F0(p.End)} s ({(p.Over ? OzSides.Outcome(p.Winner, s.Human).ToUpperInvariant() : "no end")} for blue)");
            V(p.CpuOrders > 0 && p.CpuHostile > 0 && p.CpuBuilt > 0 && p.CpuFlags > 0 && a.CpuOrders > 0,
              $"{level}: the red CPU plays and attacks: {p.CpuOrders} orders ({p.CpuHostile} attacks or fort break-ins), {p.CpuBuilt} units built, {p.CpuFlags} flags taken (against the scripted player: {a.CpuOrders} orders, {a.CpuHostile} attacks)");
            if (level == "L02") return;
            // EASY: the red CPU gets the OpenZ handicap (its first order after ZBot.EASY_START_DELAY); on level 17 blue's start crews (DV63)
            PlaySession(level, true, false, s);
            var we = NewMatch(settings, mapJson, level, s, ZBotDifficulty.Easy, 5, true, Seed);
            double first = -1; var rb = we.Bots.FirstOrDefault(x => x.Team == Team.Red);
            while (we.Time < 120.0 && rb != null) { we.Tick(Tick); we.Events.Clear(); if (first < 0 && rb.Orders > 0) first = we.Time; }
            bool crews = level != "L17" || (we.L17CrewsSent[(int)Team.Blue] > 0 && we.L17CrewsSent[(int)Team.Red] == 0);
            V(rb != null && rb.Difficulty == ZBotDifficulty.Easy && first >= ZBot.EASY_START_DELAY - 1e-6 && we.TechBonus[(int)Team.Blue] == 5 && crews,
              $"{level} EASY: the red CPU is the gentle one ({(rb != null ? rb.Difficulty.ToString() : "none")}, its first order at {(first < 0 ? "none in 120" : F0(first))} s, not before {F0(ZBot.EASY_START_DELAY)} s), blue's build lists +{we.TechBonus[(int)Team.Blue]}" +
              (level == "L17" ? $"; the start crews walk to blue's parked vehicles (blue {we.L17CrewsSent[(int)Team.Blue]}, red {we.L17CrewsSent[(int)Team.Red]})" : ""));
        }

        static void PartC(string settings, string mapJson, string level)
        {
            sb.AppendLine($"C  level {level}, the box OFF: the same world as before the kit, tick for tick ({F0(OffSeconds)} s of a normal match, MEDIUM, HEAD START on)");
            var now = OzSides.For(false, false); var was = Before;
            PlaySession(level, false, false, now);
            var x = Play(settings, mapJson, level, now, ZBotDifficulty.Normal, MediumTech, true, "attack", OffSeconds);
            PlaySession(level, false, false, was);
            var y = Play(settings, mapJson, level, was, ZBotDifficulty.Normal, MediumTech, true, "attack", OffSeconds);
            V(!x.Threw && !y.Threw && x.Hash == y.Hash, $"{level}: the red human against the blue CPU: hash {x.Hash:x16} (the setup of the sides) = {y.Hash:x16} (the setup before the kit)");
            var dn = OzSides.For(false, true); var db = OzSides.For(true, true);
            PlaySession(level, false, false, dn);
            var c1 = Play(settings, mapJson, level, dn, ZBotDifficulty.Normal, 0, false, "cpu", OffSeconds);
            PlaySession(level, false, false, db);
            var c2 = Play(settings, mapJson, level, db, ZBotDifficulty.Normal, 0, false, "cpu", OffSeconds);
            V(!c1.Threw && !c2.Threw && c1.Hash == c2.Hash, $"{level}: a bot match is the same with the box on or off: {c1.Hash:x16} / {c2.Hash:x16}");
            sb.AppendLine($"HASH|{level}|normal|{x.Hash:x16}|bots|{c1.Hash:x16}");
        }

        static List<ZObject> Forts(ZWorld w, Team t) => w.Objects.Where(o => o.IsFort && o.Owner == t && !o.Destroyed).ToList();
        static bool Near(ZObject o, List<ZObject> forts, long px) => forts.Any(f => { long dx = f.CenterX - o.CenterX, dy = f.CenterY - o.CenterY; return dx * dx + dy * dy <= px * px; });
    }
}
