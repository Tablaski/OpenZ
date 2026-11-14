using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 20 (View/Voice/OpenZVoiceV3.cs, OpenZVoiceV3.StealLotOnly): Rachel's "Warning! The enemy is stealing vehicles from the
    /// car park!" may only come after an enemy robot really climbs into a vehicle OF THE CAR PARK. Counted headless on L17 (ZWorld.Create
    /// + Tick, no scene, nothing saved; the game's rules via OzV11StrikeCount.Game; DV32 parked start on; the player's L17 head start
    /// parked empty at the red fort as in a game), computer against computer, several seeds. Per match and per enemy side:
    ///  S1 the car-park lot (OpenZVoiceV3.InCarParkLot) holds exactly the map's 20 empty vehicles, every one inside the car-park box
    ///     (tiles x 24-86, y 88-114); the 1.2 set (every vehicle empty at the start) is larger (L17: 31 with the head start);
    ///  S2 the NEW trigger (the first enemy boarding that passes the new rule) = the first enemy boarding of a lot vehicle, and its
    ///     vehicle started inside the car-park box: false triggers 0;
    ///  S3 the OLD trigger (the 1.2 rule) for comparison: its time, and how many enemy boardings of vehicles OUTSIDE the car park
    ///     came before the first real car-park boarding (each one could have set the old warning off early);
    ///  S4 deterministic: the first match played twice gives the same trigger times.
    /// The game adds 1.5 s and the tip gaps on top of the trigger (the line is never earlier than the trigger); the Play check in
    /// SOLUTION.md reads OpenZVoiceV3.Status() for the real line time.
    /// </summary>
    public static class OzL17StealWarningCount
    {
        public static int Seeds = 6;
        public static double Minutes = 15;
        /// <summary>The car park rows of L17 in tiles (the 20 empty vehicles stand at x 27-81, y 91-111; a vehicle is 2x2 tiles).</summary>
        public const int BoxX0 = 24, BoxX1 = 86, BoxY0 = 88, BoxY1 = 114;

        public sealed class Side
        {
            public Team enemy;
            public double oldFirst = -1, newFirst = -1, lotFirst = -1;
            public int outsideBeforeLot, newOutside, enemyBoardings;
            public string oldWhat = "-", newWhat = "-";
        }

        public sealed class MatchResult
        {
            public int seed, oldSet, lot, lotOutsideBox; public double end; public string error;
            public readonly Dictionary<Team, Side> sides = new Dictionary<Team, Side>();
        }

        static string F(double v) => v < 0 ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Clock(double s) => s < 0 ? "-" : $"{(int)(s / 60)}:{(int)(s % 60):00}";
        static bool InBox(int tx, int ty) => tx >= BoxX0 && tx <= BoxX1 && ty >= BoxY0 && ty <= BoxY1;

        public static MatchResult Match(string settings, string map, int seed, double seconds)
        {
            var R = new MatchResult { seed = seed };
            using (OzV11StrikeCount.Game(true))
            {
                try
                {
                    var w = ZWorld.Create(settings, map, seed, true);                                  // DV32 as in a game on L17
                    w.AddHeadStart(Team.Red, true, ZGame.EasyL17Types);                                // the player's L17 head start, parked empty
                    w.Bots.Add(new ZBot(w, Team.Red, seed)); w.Bots.Add(new ZBot(w, Team.Blue, seed + 101));
                    var oldSet = new HashSet<int>(); var lot = new HashSet<int>(); var startTile = new Dictionary<int, (int, int)>();
                    var owner = new Dictionary<int, Team>();
                    foreach (var o in w.Objects)
                        if (o.Kind == ZKind.Vehicle && o.Owner == Team.Neutral && !o.Destroyed)
                        {
                            oldSet.Add(o.Id); owner[o.Id] = o.Owner; startTile[o.Id] = (o.LocX / 16, o.LocY / 16);
                            if (OpenZVoiceV3.InCarParkLot(o)) { lot.Add(o.Id); if (!InBox(o.LocX / 16, o.LocY / 16)) R.lotOutsideBox++; }
                        }
                    R.oldSet = oldSet.Count; R.lot = lot.Count;
                    foreach (var t in new[] { Team.Blue, Team.Red }) R.sides[t] = new Side { enemy = t };
                    w.Events.Clear();
                    while (w.Time < seconds && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events)
                        {
                            if (e.type != ZEventType.Entered || !oldSet.Contains(e.otherId)) continue;
                            var v = w.GetAny(e.otherId); var r = w.GetAny(e.objId);
                            Team team = r != null ? r.Owner : v != null ? v.Owner : Team.Neutral;
                            Team before = owner.TryGetValue(e.otherId, out var b) ? b : Team.Neutral;
                            if (team == Team.Neutral || before != Team.Neutral || !R.sides.TryGetValue(team, out var s)) continue;
                            s.enemyBoardings++;
                            var (tx, ty) = startTile[e.otherId];
                            string what = $"{v?.Type} #{e.otherId} at ({tx},{ty})";
                            bool isLot = lot.Contains(e.otherId);
                            if (s.oldFirst < 0) { s.oldFirst = w.Time; s.oldWhat = what; }                                     // the 1.2 rule
                            if (s.newFirst < 0 && (!OpenZVoiceV3.StealLotOnly || isLot))                                      // the 1.3 rule (as the game asks it)
                            { s.newFirst = w.Time; s.newWhat = what; if (!InBox(tx, ty)) s.newOutside++; }
                            if (isLot) { if (s.lotFirst < 0) s.lotFirst = w.Time; }
                            else if (s.lotFirst < 0) s.outsideBeforeLot++;
                        }
                        w.Events.Clear();
                        foreach (var id in oldSet) { var o = w.GetAny(id); if (o != null) owner[id] = o.Owner; }
                    }
                    R.end = w.Time;
                }
                catch (Exception ex) { R.error = ex.GetType().Name + ": " + ex.Message; }
            }
            return R;
        }

        public static string Run(string settings, string map, int seeds, double minutes)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# L17 steal warning count (1.3 item 20): {seeds} computer matches of {minutes:0} min, StealLotOnly = {OpenZVoiceV3.StealLotOnly}{OzV11StrikeCount.RulesNote()}");
            sb.AppendLine("| seed | enemy | 1.2 set / lot / lot outside box | OLD trigger (1.2 rule) | NEW trigger (1.3) | first lot boarding | outside boardings before it | new triggers outside | verdict |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            bool all = true; int earlyOld = 0, sidesWithLot = 0; MatchResult first = null;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var R = Match(settings, map, seed, minutes * 60);
                if (first == null) first = R;
                if (R.error != null) { all = false; sb.AppendLine($"| {seed} | - | - | - | - | - | - | - | FAIL {R.error} |"); continue; }
                foreach (var s in R.sides.Values)
                {
                    bool ok = R.lot == 20 && R.lotOutsideBox == 0 && R.oldSet > R.lot && s.newOutside == 0 && s.newFirst == s.lotFirst;
                    if (s.lotFirst >= 0) sidesWithLot++;
                    if (s.oldFirst >= 0 && (s.lotFirst < 0 || s.oldFirst < s.lotFirst)) earlyOld++;
                    all &= ok;
                    sb.AppendLine($"| {seed} | {s.enemy} | {R.oldSet} / {R.lot} / {R.lotOutsideBox} | {Clock(s.oldFirst)} {s.oldWhat} | {Clock(s.newFirst)} {s.newWhat} | {Clock(s.lotFirst)} | {s.outsideBeforeLot} | {s.newOutside} | {(ok ? "ok" : "FAIL")} |");
                }
            }
            if (first != null && first.error == null)
            {   // S4: the same match again
                var again = Match(settings, map, first.seed, minutes * 60);
                bool same = again.error == null && first.sides.All(kv => again.sides.TryGetValue(kv.Key, out var a) && a.newFirst == kv.Value.newFirst && a.oldFirst == kv.Value.oldFirst);
                all &= same;
                sb.AppendLine($"- S4 deterministic (seed {first.seed} twice): {(same ? "same trigger times" : "FAIL: different")}");
            }
            sb.AppendLine($"- sides that boarded a car-park vehicle: {sidesWithLot} of {seeds * 2}; the 1.2 rule would have warned EARLY (before any car-park boarding) in {earlyOld}");
            sb.AppendLine(all ? "PASS: the new trigger is always a car-park boarding, never earlier, 0 false triggers" : "FAIL: see the rows above");
            return sb.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.3/Count L17 Steal Warning (item 20)")]
        static void Menu() => OzV11Kit.RunMenu("1.3 item 20 L17 steal warning (6 computer matches of 15 min on L17 + 1 repeat; a few minutes)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText("L17"), Seeds, Minutes));
#endif
    }
}
