using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.3 item 29, counted check (a player: "They did answer by their voice lines but they simply did not move sometimes usually
    /// it happens when I have multiple units selected"). Headless simulation on Desert L02, Jungle L16 and Car Park L17, each with its
    /// start units and a blue CPU that gives no orders. The red side (the player's) gets a mixed pool round the anchor of the b16 check
    /// (Editor/OzV11MassOrderCount.cs: vehicles of every kind, every fourth unit a robot squad of every kind) plus two guns and a crane,
    /// topped up when units are lost. Then <see cref="Orders"/> seeded random orders, one after the other, each to a random group of 5,
    /// 10, 20 or 40 of the red units (in turn), built per unit exactly like the player's right-click (<see cref="Click"/>, a copy of
    /// View/PlayerInput.cs BuildWaypoint without modifier keys) and sent with SEND_WAYPOINTS:
    /// a move to open ground (45 %), an attack on an enemy unit or building (25 %), a click on a neutral map object (rock, hut, cactus /
    /// boulder, wreck, palm / sign / lamp: what the pointer meets near the ground, 20 %), a flag, a grenade crate or an empty vehicle
    /// (10 %). 1 s of battle time after each order every selected unit and squad member is looked at: under way (2 px or more from
    /// where the order found it), carrying the order out from where it stands (firing at its target in range, at its spot already),
    /// standing with a reason (ZObject.OrderStandWhy: refused by Zod's rules, a gun, stunned, waiting in traffic, given up, gone),
    /// or SILENT (it has its order and stands, or the order ended without a step and without a reason). The next order follows at once
    /// (players re-click while units still walk). Every 10th order is the previous click again, to the same units (a player who sees
    /// units stand clicks the same spot again): the case of a waypoint equal to the old one (cause B).
    /// Cause A is counted on its own: a unit that can move whose ATTACK was refused by Zod's rule (it then stands, the refusal noted).
    /// Such a unit is "standing with a reason" in the SILENT column (the note is the reason), so the SILENT column alone cannot show
    /// cause A; the column "attack refused although it can move" does (empty vehicles and guns as targets left out: DV81's rows).
    /// Each level runs twice with the same seed: "after" = the game's switches with the item 29 fixes (DV158 on, the restart on, DV166 on);
    /// "before" = both off (Zod's refusal, the old ClearPending). PASS = 0 silent selected units, 0 silent squad members and 0 refused
    /// attacks of units that can move in "after" on every level. The "before" rows are the measure of the bug.
    /// </summary>
    public static class OzV13GroupOrderCount
    {
        public const int Orders = 200, SettleTicks = 300, WaitTicks = 100, PoolUnits = 40, Seed = 29;
        public static readonly int[] GroupSizes = { 5, 10, 20, 40 };
        public static readonly string[] LevelList = { "L02", "L16", "L17" };
        public const string ReportPath = "counts.txt";

        /// <summary>The rule switches of a Play session that act on orders and walking (GameSwitches leaves them off for other checks).</summary>
        static readonly string[] PlaySwitches = { "CalmMoveDeviation", "CalmFollowDeviation", "SniperSteadyDeviation", "ToughSteadyDeviation", "NoAutoFlagUnderFireDeviation",
                                                  "JeepChargeDeviation", "SniperBalanceDeviation", "ProneTargetDeviation", "GroundFireDeviation", "HeavyFallDeviation" };

        public sealed class Result
        {
            public string level, name; public bool fixes, gameOver; public string setup;
            public int orders, units, started, working, reasoned, silent, members, membersStarted, membersSilent, fallbacks, restarts, aheadWalks;
            /// <summary></summary>
            public int refusedMovable, repeats;
            public readonly int[] silentBySize = new int[GroupSizes.Length];
            public readonly Dictionary<string, int> reasons = new Dictionary<string, int>();
            public readonly Dictionary<string, int> silentByKind = new Dictionary<string, int>();
            /// <summary>3 Nov (DV166 kit): the reasons of squad members that stand while their leader walks (the report shows them).</summary>
            public readonly Dictionary<string, int> memberReasons = new Dictionary<string, int>();
            public readonly List<string> silentLines = new List<string>();
        }

        // ------------------------------------------------------------------ the player's click, headless
        /// <summary>View/PlayerInput.cs NearHostiles: a hostile unit within attack radius + agro distance (zplayer.cpp:2726-2744).</summary>
        static bool NearHostiles(ZWorld w, ZObject u)
        {
            if (u.U == null) return false;
            double r = u.U.attack_radius + w.S.agro_distance;
            foreach (var o in w.Objects)
                if ((o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner != Team.Neutral && o.Owner != u.Owner && !o.Destroyed
                    && (o.CenterX - u.CenterX) * (double)(o.CenterX - u.CenterX) + (o.CenterY - u.CenterY) * (double)(o.CenterY - u.CenterY) <= r * r)
                    return true;
            return false;
        }

        /// <summary>View/PlayerInput.cs BuildWaypoint for a right-click without Ctrl / Alt on `h` (null = open ground) at (x, y). The
        /// repair, crane, fort-door and own-APC rows are left out: this check never clicks an own building, a fort door or an own unit.</summary>
        public static ZWaypoint Click(ZWorld w, ZObject u, ZObject h, int x, int y)
        {
            var wp = ZWaypoint.Move(x, y, !NearHostiles(w, u));
            if (h == null) return wp;
            wp.refId = h.Id;
            if (h.Type == "flag") return wp;
            bool notOurs = h.Owner != u.Owner;
            if (h.Destroyed) { wp.refId = -1; return wp; }
            if (h.Kind == ZKind.Building && h.Owner == Team.Neutral) { wp.refId = -1; return wp; }
            if (ZWorld.EmptyClickWaypoint(u, h, false, ref wp)) return wp;
            switch (u.Kind)
            {
                case ZKind.Cannon: case ZKind.Vehicle: if (notOurs) wp.mode = WpMode.Attack; return wp;
                case ZKind.Robot:
                    if (h.Type == "grenades" && u.Type != "tough" && u.CanPickupGrenades()) { wp.mode = WpMode.PickupGrenades; wp.attackTo = false; wp.x = h.CenterX; wp.y = h.CenterY; return wp; }
                    if ((h.Owner == Team.Neutral && (h.Kind == ZKind.Vehicle || h.Kind == ZKind.Cannon)) || h.CanTakePassengers(u)) { wp.mode = WpMode.Enter; wp.attackTo = false; wp.x = h.CenterX; wp.y = h.CenterY; return wp; }
                    if (notOurs) wp.mode = WpMode.Attack;
                    return wp;
            }
            return wp;
        }

        /// <summary>What the pointer meets near the ground besides units (View/PlayerInput.cs HoverAt): rocks, huts, cacti / boulders, wrecks, DV67 props.</summary>
        static bool MapThing(ZObject o) => o.Kind == ZKind.MapItem && !o.Destroyed && o.KillAt < 0 &&
            (o.Type == "rock" || o.Type == "hut" || o.Type.StartsWith("map_object", StringComparison.Ordinal) || o.Type == ZWorld.WreckType || o.Type == ZWorld.DetailPropType);

        // ------------------------------------------------------------------ pool
        static bool Selectable(ZObject o, Team t) => OzV11Kit.Alive(o) && o.Owner == t && o.Selectable && !o.IsShuttle;

        /// <summary>A free 2x2 spot of dry ground near the anchor tile (nothing within 40 px), nearest first; false = none.</summary>
        static bool FreeSpot(ZWorld w, int ax, int ay, out int cx, out int cy)
        {
            for (int r = 2; r < 30; r++)
                for (int ty = ay - r; ty <= ay + r; ty++)
                    for (int tx = ax - r; tx <= ax + r; tx++)
                    {
                        if (Math.Abs(tx - ax) != r && Math.Abs(ty - ay) != r) continue;
                        if (tx < 2 || ty < 2 || tx >= w.Map.width - 3 || ty >= w.Map.height - 3) continue;
                        bool ok = true;
                        for (int j = -1; j <= 2 && ok; j++) for (int i = -1; i <= 2; i++) { var k = w.Map.ClassAt(tx + i, ty + j); if (k != ZClass.Ground && k != ZClass.Road) { ok = false; break; } }
                        if (!ok) continue;
                        int px = tx * 16 + 16, py = ty * 16 + 16;
                        if (w.Objects.Any(o => !o.Destroyed && Math.Abs(o.CenterX - px) < 40 + o.WidthPix / 2 && Math.Abs(o.CenterY - py) < 40 + o.HeightPix / 2)) continue;
                        cx = px; cy = py; return true;
                    }
            cx = cy = 0; return false;
        }

        static void Pool(ZWorld w, int ax, int ay, bool first)
        {
            int have = w.Objects.Count(o => Selectable(o, Team.Red) && o.CanMove);
            if (have < PoolUnits) OzV11MassOrderCount.Spawn(w, PoolUnits - have, ax, ay, Team.Red);
            if (!first) return;
            foreach (var t in new[] { "gun", "gatling", "crane" })
                if (FreeSpot(w, ax, ay, out int cx, out int cy)) OzV11Kit.Place(w, t, cx, cy, Team.Red);
        }

        static void Count(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }

        /// <summary>The reason without its numbers and ids (the report's reason table).</summary>
        static string Kind(string why)
        {
            if (why.StartsWith("order refused", StringComparison.Ordinal)) return why;                    // (Zod's refusals carry no ids: kept whole)
            int c = why.IndexOf(':'); string k = c > 0 ? why.Substring(0, c) : why;
            int p = k.IndexOf('('); if (p > 0) k = k.Substring(0, p).TrimEnd();
            int h = k.IndexOf('#'); if (h > 0) k = k.Substring(0, k.LastIndexOf(' ', h) > 0 ? k.LastIndexOf(' ', h) : h);
            return k;
        }

        // ------------------------------------------------------------------ one level
        public static Result One(string settings, string map, int levelIndex, bool fixes)
        {
            var lv = OzV11MassOrderCount.Levels.First(l => l.level == LevelList[levelIndex]);
            var r = new Result { level = lv.level, name = lv.name, fixes = fixes };
            ZWorld.OrderMoveFallbackDeviation = fixes; ZWorld.OrderPendingRestart = fixes; ZWorld.AheadWalkDeviation = fixes;
            var w = OzV11MassOrderCount.World(settings, map, lv.level, Seed);
            Pool(w, lv.ax, lv.ay, true);
            for (int t = 0; t < SettleTicks; t++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            int fb0 = ZWorld.OrderMoveFallbacks, rs0 = ZWorld.OrderPendingRestarts, aw0 = ZWorld.AheadWalks;
            var rng = new Random(Seed * 31 + levelIndex);
            var inv = CultureInfo.InvariantCulture;
            List<ZObject> lastGroup = null; ZObject lastH = null; int lastX = 0, lastY = 0, lastSi = 0; string lastKind = null;
            for (int n = 0; n < Orders; n++)
            {
                if (w.GameOver) { r.gameOver = true; break; }
                Pool(w, lv.ax, lv.ay, false);
                ZObject h = null; int x = 0, y = 0, si; string kind; List<ZObject> group;
                var again = n % 10 == 9 && lastGroup != null ? lastGroup.Where(o => Selectable(o, Team.Red)).ToList() : null;
                if (again != null && again.Count > 0)
                {   // the previous click again, to the same units (cause B: a waypoint equal to the old one)
                    group = again; h = lastH != null && OzV11Kit.Alive(lastH) ? lastH : null; x = lastX; y = lastY; si = lastSi; kind = "repeat " + lastKind; r.repeats++;
                }
                else
                {
                    si = n % GroupSizes.Length;
                    int size = GroupSizes[si];
                    var all = w.Objects.Where(o => Selectable(o, Team.Red)).OrderBy(o => o.Id).ToList();
                    for (int i = all.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = all[i]; all[i] = all[j]; all[j] = tmp; }
                    group = all.Take(size).ToList();
                    if (group.Count == 0) { r.setup = "no red unit left"; break; }
                    double mx = group.Average(o => (double)o.CenterX), my = group.Average(o => (double)o.CenterY);
                    bool Near(ZObject o, int tiles) => Math.Abs(o.CenterX - mx) <= tiles * 16 && Math.Abs(o.CenterY - my) <= tiles * 16;
                    int roll = rng.Next(100);
                    List<ZObject> pick = null;
                    if (roll >= 45 && roll < 70) pick = w.Objects.Where(o => OzV11Kit.Alive(o) && o.Owner == Team.Blue && (o.IsUnit || (o.Kind == ZKind.Building && !o.Type.StartsWith("fort", StringComparison.Ordinal))) && !o.IsFortDefender && Near(o, 30)).OrderBy(o => o.Id).ToList();
                    else if (roll >= 70 && roll < 90) pick = w.Objects.Where(o => MapThing(o) && Near(o, 25)).OrderBy(o => o.Id).ToList();
                    else if (roll >= 90) pick = w.Objects.Where(o => OzV11Kit.Alive(o) && Near(o, 30) && ((o.Kind == ZKind.MapItem && (o.Type == "flag" || o.Type == "grenades")) || (o.Owner == Team.Neutral && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && !o.IsShuttle))).OrderBy(o => o.Id).ToList();
                    if (pick != null && pick.Count > 0)
                    {
                        h = pick[rng.Next(pick.Count)];
                        x = h.CenterX + rng.Next(9) - 4; y = h.CenterY + rng.Next(9) - 4;
                        kind = (roll < 70 ? "attack " : "click on ") + h.Type;
                    }
                    else
                    {
                        int tx = (int)(mx / 16), ty = (int)(my / 16), k = 0;
                        do { tx = (int)(mx / 16) + rng.Next(51) - 25; ty = (int)(my / 16) + rng.Next(51) - 25; }
                        while (++k < 40 && (tx < 2 || ty < 2 || tx >= w.Map.width - 2 || ty >= w.Map.height - 2 || (w.Map.ClassAt(tx, ty) != ZClass.Ground && w.Map.ClassAt(tx, ty) != ZClass.Road)));
                        x = tx * 16 + rng.Next(16); y = ty * 16 + rng.Next(16);
                        kind = "move";
                    }
                    lastGroup = group; lastH = h; lastX = x; lastY = y; lastSi = si; lastKind = kind;
                }
                foreach (var u in group)
                {
                    var wp = Click(w, u, h, x, y);
                    w.SendWaypoints(u.Owner, u, new[] { wp });
                    if (wp.mode == WpMode.Attack && u.CanMove && !ZWorld.IsEmptyUnit(h) && Math.Abs(u.OrderRefusedAt - w.Time) < 1e-9) r.refusedMovable++;
                }
                var snaps = group.Select(u => u.OrderSnapshot()).ToList();
                var bodies = group.SelectMany(u => u.Minions.Where(m => m != null).Select(m => (m, u))).ToList();
                var bodySnaps = bodies.Select(b => b.m.OrderSnapshot()).ToList();
                var moved = new bool[group.Count]; var bodyMoved = new bool[bodies.Count];
                for (int t = 0; t < WaitTicks; t++)
                {
                    w.Tick(ZConst.TICK); w.Events.Clear();
                    for (int i = 0; i < group.Count; i++) if (!moved[i] && group[i].OrderMovedFrom(snaps[i])) moved[i] = true;
                    for (int i = 0; i < bodies.Count; i++) if (!bodyMoved[i] && bodies[i].m.OrderMovedFrom(bodySnaps[i])) bodyMoved[i] = true;
                }
                r.orders++;
                for (int i = 0; i < group.Count; i++)
                {
                    r.units++;
                    if (moved[i]) { r.started++; continue; }
                    string why = group[i].OrderStandWhy(snaps[i], out bool silent, out bool working);
                    if (working) { r.working++; Count(r.reasons, "carries it out where it stands: " + Kind(why)); continue; }
                    if (!silent) { r.reasoned++; Count(r.reasons, Kind(why)); continue; }
                    r.silent++; r.silentBySize[si]++; Count(r.silentByKind, kind.StartsWith("repeat", StringComparison.Ordinal) ? "repeated click" : kind.StartsWith("move", StringComparison.Ordinal) ? "move" : kind.StartsWith("attack", StringComparison.Ordinal) ? "attack" : "click on an object");
                    if (r.silentLines.Count < 40) r.silentLines.Add(string.Format(inv, "order {0} ({1}, group of {2}) at {3:F2} s: {4}#{5} at ({6},{7}): {8}", n + 1, kind, group.Count, snaps[i].at, group[i].Type, group[i].Id, snaps[i].x, snaps[i].y, why));
                }
                for (int i = 0; i < bodies.Count; i++)
                {
                    r.members++;
                    if (bodyMoved[i]) { r.membersStarted++; continue; }
                    int li = group.IndexOf(bodies[i].u);
                    string why = bodies[i].m.OrderStandWhy(bodySnaps[i], out bool silent, out bool working);
                    if ((working || !silent) && li >= 0 && moved[li]) Count(r.memberReasons, (working ? "carries it out where it stands: " : "") + Kind(why));
                    if (working || !silent || li < 0 || !moved[li]) continue;                         // a member whose leader stands is counted with its leader
                    r.membersSilent++;
                    if (r.silentLines.Count < 40) r.silentLines.Add(string.Format(inv, "order {0} ({1}, group of {2}) at {3:F2} s: squad member {4}#{5} of #{6} (the leader walks): {7}", n + 1, kind, group.Count, bodySnaps[i].at, bodies[i].m.Type, bodies[i].m.Id, bodies[i].u.Id, why));
                }
            }
            r.fallbacks = ZWorld.OrderMoveFallbacks - fb0; r.restarts = ZWorld.OrderPendingRestarts - rs0; r.aheadWalks = ZWorld.AheadWalks - aw0;
            return r;
        }

        static string Row(Result r)
        {
            var inv = CultureInfo.InvariantCulture;
            string reasons = string.Join(", ", r.reasons.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " " + kv.Value));
            string bySize = string.Join(", ", GroupSizes.Select((s, i) => s + ": " + r.silentBySize[i]));
            string byKind = r.silentByKind.Count == 0 ? "-" : string.Join(", ", r.silentByKind.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " " + kv.Value));
            return string.Format(inv, "{0} {1} | {2} | {3} ({17} repeated clicks) | {4} | {5} | {6} | {7} | {18} | {8} | {9} of {10} ({11} silent) | {12} | {13} | {14} | {15}{16}",
                r.name, r.level, r.fixes ? "after (item 29 fixes on)" : "before (fixes off)", r.orders, r.units, r.started, r.working, r.reasoned, r.silent,
                r.membersStarted, r.members, r.membersSilent, bySize, byKind, r.fallbacks + " / " + r.restarts + " / " + r.aheadWalks, reasons, r.gameOver ? " | GAME OVER before the last order" : "",
                r.repeats, r.refusedMovable);
        }

        /// <summary>The whole count. settings = default_settings, maps = the grid of a level ("L02", "L16", "L17").</summary>
        public static string Run(string settings, Func<string, string> maps)
        {
            foreach (var s in PlaySwitches) OzV11Kit.TrySet(typeof(ZWorld), s, true);
            var sb = new StringBuilder();
            sb.AppendLine($"1.3 item 29: selected units that answer an order but do not move. Headless, each level with its start units and a blue CPU that gives no orders; the red pool ({PoolUnits} units round the b16 anchor, every fourth a robot squad, plus two guns and a crane, topped up), {SettleTicks} ticks to settle, then {Orders} seeded orders (seed {Seed}) to groups of {string.Join(" / ", GroupSizes)} in turn, built like the player's right-click (every 10th the previous click again, to the same units); each looked at {WaitTicks * ZConst.TICK:0.0} s of battle time later.");
            sb.AppendLine("level | run | orders | selected units ordered | under way within 1 s | carry it out where they stand | stand with a reason | of them: attack refused although it can move (cause A) | SILENT | squad members under way (silent while their leader walks) | silent by group size | silent by order kind | attack orders turned into moves (DV158) / units restarted / squad members ahead of their leader sent on their own path (DV166) | reasons");
            var after = new List<Result>(); var before = new List<Result>();
            for (int li = 0; li < LevelList.Length; li++)
            {
                string map; try { map = maps(LevelList[li]); } catch (Exception) { map = null; }
                if (string.IsNullOrEmpty(map)) { sb.AppendLine(OzV11Kit.Verdict(false, $"{LevelList[li]}: no grid found")); continue; }
                bool fb = ZWorld.OrderMoveFallbackDeviation, pr = ZWorld.OrderPendingRestart, aw = ZWorld.AheadWalkDeviation;
                try
                {
                    var b = One(settings, map, li, false); before.Add(b); sb.AppendLine(Row(b));
                    var a = One(settings, map, li, true); after.Add(a); sb.AppendLine(Row(a));
                }
                finally { ZWorld.OrderMoveFallbackDeviation = fb; ZWorld.OrderPendingRestart = pr; ZWorld.AheadWalkDeviation = aw; }
            }
            sb.AppendLine();
            foreach (var a in after.Concat(before).Where(x => x.silentLines.Count > 0))
            {
                sb.AppendLine($"{a.name} {a.level}, {(a.fixes ? "after" : "before")}: the first silent stand-stills");
                foreach (var l in a.silentLines) sb.AppendLine("  " + l);
            }
            foreach (var a in after.Concat(before).Where(x => x.memberReasons.Count > 0))
                sb.AppendLine($"{a.name} {a.level}, {(a.fixes ? "after" : "before")}: squad members standing with a reason while their leader walks: " +
                              string.Join(", ", a.memberReasons.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " " + kv.Value)));
            sb.AppendLine();
            foreach (var a in after)
            {
                var b = before.FirstOrDefault(x => x.level == a.level);
                sb.AppendLine(OzV11Kit.Verdict(a.orders == Orders && !a.gameOver && a.setup == null, $"{a.name} {a.level}: all {Orders} orders given ({a.orders}{(a.gameOver ? ", the game ended first" : "")}{(a.setup != null ? ", " + a.setup : "")})"));
                sb.AppendLine(OzV11Kit.Verdict(a.silent == 0, $"{a.name} {a.level}: 0 silent stand-stills of selected units after 1 s ({a.silent} of {a.units}; before the fixes {(b != null ? b.silent.ToString(CultureInfo.InvariantCulture) : "-")})"));
                sb.AppendLine(OzV11Kit.Verdict(a.membersSilent == 0, $"{a.name} {a.level}: 0 silent squad members while their leader walks ({a.membersSilent} of {a.members}; before the fixes {(b != null ? b.membersSilent.ToString(CultureInfo.InvariantCulture) : "-")})"));
                sb.AppendLine(OzV11Kit.Verdict(a.refusedMovable == 0, $"{a.name} {a.level}: 0 units that can move left standing by a refused attack (cause A, DV158) ({a.refusedMovable}; before the fixes {(b != null ? b.refusedMovable.ToString(CultureInfo.InvariantCulture) : "-")}: the measure of cause A, expected above 0)"));
            }
            return sb.ToString();
        }

#if UNITY_EDITOR
        const string SessionKey = "OpenZ.V13.GroupOrderCount";   // 0 = not run in this editor session, 1 = running, 2 = ran

        [UnityEditor.MenuItem("OpenZ/V1.3/Count Group Orders Not Moving (item 29)")]
        static void Menu()
        {
            if (UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.3] item 29 group orders: leave Play mode first (the check sets the simulation's static switches)."); return; }
            int state = UnityEditor.SessionState.GetInt(SessionKey, 0);
            if (state != 0) { UnityEngine.Debug.Log("[OpenZ V1.3] item 29 group orders: " + (state == 1 ? "is running" : "has run") + " in this editor session, nothing done. Its report: " + ReportPath); return; }
            UnityEditor.SessionState.SetInt(SessionKey, 1);
            try
            {
                OzV11Kit.RunMenu("item 29 group orders not moving", () =>
                {
                    string report = Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ReportPath));
                    System.IO.File.WriteAllText(ReportPath, report);
                    return report + "written to " + ReportPath;
                });
            }
            finally { UnityEditor.SessionState.SetInt(SessionKey, 2); }
        }
#endif
    }
}
