using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// lines of the DV194 counted check (Editor/OzV15UeBoxesCount.cs) for the BOX's attack range, headless on the Sim alone (the playground's map UE_grid).
    ///  B11 the ATTACK order (Ctrl + right-click, as View/PlayerInput.BuildWaypoint makes it) by a Bizon, a Mammoth and a heavy tank from 20 tiles at
    ///      (a) a lone box, (b) the bottom box of a 3-high stack, (c) its top box, (d) a box behind another box: each stops at its weapon range from
    ///      the aimed box's near face (+- one tile), never closer, fires at it; the holo obstacle as before (its centre within the range);
    ///  B12 the PLAIN right-click on a box (a MOVE into its footprint, OzUeHooks.PlainObstacleClick: the owner's case): the box stands in the way and
    ///      is attacked from the weapon range (never at the contact); the holo obstacle as before (Zod's contact rule: it is not a box);
    ///  B13 never a ram / crush / shove / chain target: a Bizon and a Mammoth with every big-tank rule on (DV180-DV196) driven into a stack: no ram
    ///      or crush ever starts, no box is a RAM-FIRST target, none is a crushable wreck (DV193), they fire from range;
    ///  B14 no box blocks a shot at a box: from a spot in range, each box of a 3-high column and a box behind another box: in range, a clear line;
    ///  B15 the same scene twice: the same tick, place and hit points (deterministic);
    ///  B16 a world with no box (60 s of L02): the way scan never ran (the hooks return at their first test).
    /// </summary>
    public static partial class OzV15UeBoxesCount
    {
        const int FromPx = 320;                                                              // 20 tiles

        static void RangeChecks(string s, string m, string l02)
        {
            B11(s, m); B12(s, m); B13(s, m); B14(s, m); B15(s, m); B16(s, l02);
        }

        static ZWaypoint CtrlAttack(ZObject t) => new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY, attackTo = true, fightOnWay = true, forceFire = true, playerGiven = true };
        static ZWaypoint PlainClick(ZObject t) { var wp = ZWaypoint.Move(t.CenterX, t.CenterY, true); wp.refId = -1; return wp; }

        static int Face(ZObject b, ZObject u) => (int)Math.Round(Math.Sqrt(ZWorld.BoxFaceD2(b, u.CenterX, u.CenterY)));
        static int Centre(ZObject b, ZObject u) => (int)Math.Round(OzV11Kit.Dist(b, u));

        /// <summary>The boxes of a case on tiles (60, 50); the target returned. a: one box; b / c: the bottom / top of three; d: the box at (63, 50)
        /// behind the box at (60, 50) for a unit coming from the west; o: the holo obstacle (3 x 3 tiles at (60, 49)).</summary>
        static ZObject Case(ZWorld w, string kase, List<ZObject> all)
        {
            if (kase == "a") { all.Add(Box(w, 60, 50)); return all[0]; }
            if (kase == "b" || kase == "c") { for (int k = 0; k < 3; k++) all.Add(Box(w, 60, 50)); return kase == "b" ? all[0] : all[2]; }
            if (kase == "d") { all.Add(Box(w, 60, 50)); all.Add(Box(w, 63, 50)); return all[1]; }
            var o = w.PlaceObstacle(60, 49, 3, ZWorld.PlaygroundObstacleHp, out string why);
            if (o == null) throw new Exception("no obstacle: " + why);
            all.Add(o); return o;
        }

        sealed class Run1 { public int tick = -1, face = -1, centre = -1, minFace = int.MaxValue, minAny = int.MaxValue, shots, atColumn, rams; public string end; }

        /// <summary>Ticks the world until the unit has fired twice at the target's column (then 3 s more for the shells; or maxTicks): where it fired
        /// first, how close it came.</summary>
        static Run1 Drive(ZWorld w, ZObject u, ZObject target, List<ZObject> all, int maxTicks, params ZObject[] watchRams)
        {
            var r = new Run1();
            for (int i = 0; i < maxTicks; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.objId == u.Id && (e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan))
                    {
                        r.shots++;
                        var hit = w.Get(e.otherId);
                        if (hit != null && (hit == target || (w.IsBox(hit) && w.IsBox(target) && w.BoxSameColumn(target, hit)))) r.atColumn++;
                        if (r.tick < 0) { r.tick = (int)w.TickIndex; r.face = target.Kind == ZKind.MapItem ? Face(target, u) : -1; r.centre = Centre(target, u); }
                    }
                w.Events.Clear();
                foreach (var x in watchRams) if (x.Ramming) r.rams++;
                if (OzV11Kit.Alive(target)) r.minFace = Math.Min(r.minFace, Face(target, u));
                foreach (var b in all) if (OzV11Kit.Alive(b)) r.minAny = Math.Min(r.minAny, Face(b, u));
                if (r.atColumn >= 2) { for (int k = 0; k < 300; k++) { w.Tick(ZConst.TICK); w.Events.Clear(); } break; }   // 3 s more: the shells land
            }
            r.end = $"t={w.TickIndex} at ({u.CenterX},{u.CenterY}) hp {string.Join("/", all.ConvertAll(b => b.Health))} shots {r.shots}";
            return r;
        }

        static readonly string[] Tanks = { ZBigTanks.Bizon, ZBigTanks.Mammoth, "heavy" };

        static void B11(string s, string m)
        {
            foreach (var un in Tanks)
            {
                var parts = new List<string>(); bool ok = true;
                foreach (var kase in new[] { "a", "b", "c", "d" })
                {
                    var w = World(s, m, 29); var all = new List<ZObject>();
                    var t = Case(w, kase, all);
                    var u = OzV11Kit.Place(w, un, t.CenterX - FromPx, t.CenterY, Team.Red);
                    w.SendWaypoints(u.Owner, u, new[] { CtrlAttack(t) });
                    var r = Drive(w, u, t, all, 9000);
                    int R = u.RangeTo(t);
                    bool good = r.atColumn >= 2 && t.Health < t.MaxHealth && r.face >= R - 16 && r.face <= R + 16 && r.minFace >= R - 16 && r.minAny > 24;
                    ok &= good;
                    parts.Add($"{kase} stop {r.face} px from the face (range {R}, closest {r.minFace}, any box {r.minAny}), {r.atColumn} shots at it");
                }
                Check(ok, $"B11 {un}, ATTACK order from 20 tiles: " + string.Join("; ", parts));
            }
            foreach (var un in Tanks)
            {
                var w = World(s, m, 29); var all = new List<ZObject>();
                var t = Case(w, "o", all);
                var u = OzV11Kit.Place(w, un, t.CenterX - FromPx, t.CenterY, Team.Red);
                w.SendWaypoints(u.Owner, u, new[] { CtrlAttack(t) });
                var r = Drive(w, u, t, all, 9000);
                int R = u.RangeTo(t);
                Check(r.atColumn >= 2 && r.centre <= R && r.centre >= R - 16,
                      $"B11 {un}, ATTACK order at the holo obstacle: as before, its CENTRE within the range ({r.centre} px, range {R}; {r.face} px from its face), {r.atColumn} shots");
            }
        }

        static void B12(string s, string m)
        {
            foreach (var un in Tanks)
            {
                var parts = new List<string>(); bool ok = true;
                foreach (var kase in new[] { "a", "b" })
                {
                    var w = World(s, m, 29); var all = new List<ZObject>();
                    var t = Case(w, kase, all);
                    var u = OzV11Kit.Place(w, un, t.CenterX - FromPx, t.CenterY, Team.Red);
                    w.SendWaypoints(u.Owner, u, new[] { PlainClick(t) });
                    var r = Drive(w, u, t, all, 9000);
                    int R = u.RangeTo(t);
                    bool good = r.atColumn >= 2 && r.face >= R - 16 && r.face <= R + 16 && r.minAny > 24 && w.BoxWayAttacks >= 1;
                    ok &= good;
                    parts.Add($"{kase} first shot {r.face} px from the face (range {R}), closest {r.minAny}, {r.atColumn} shots at the column, {w.BoxWayAttacks} attack(s) from range");
                }
                Check(ok, $"B12 {un}, PLAIN right-click on the box (a move into it): attacked from range, never at the contact: " + string.Join("; ", parts));
            }
            {
                var w = World(s, m, 29); var all = new List<ZObject>();
                var t = Case(w, "o", all);
                var u = OzV11Kit.Place(w, "heavy", t.CenterX - FromPx, t.CenterY, Team.Red);
                w.SendWaypoints(u.Owner, u, new[] { PlainClick(t) });
                var r = Drive(w, u, t, all, 9000);
                Check(r.atColumn >= 2 && r.face <= 16 && w.BoxWayAttacks == 0,
                      $"B12 heavy, PLAIN right-click on the holo obstacle: as before (Zod's contact rule, the obstacle is not a box): first shot {r.face} px from its face, {r.atColumn} shots");
            }
        }

        static readonly string[] BigRules = { "MammothDeviation", "BizonDeviation", "MammothRamDeviation", "BizonCrushDeviation", "RamDuelDeviation", "CrushRollOffDeviation", "RamDebrisDeviation",
                                              "RamPolishDeviation", "BigTankRamFirstDeviation", "BizonAmphibDeviation", "BigTankWreckDeviation", "RamChainDeviation", "RamFightDeviation" };

        static void B13(string s, string m)
        {
            var was = new Dictionary<string, object>();
            foreach (var n in BigRules) { var f = typeof(ZWorld).GetField(n); if (f != null) { was[n] = f.GetValue(null); f.SetValue(null, true); } }
            try
            {
                var parts = new List<string>(); bool ok = true;
                foreach (var un in new[] { ZBigTanks.Bizon, ZBigTanks.Mammoth })
                {
                    var w = World(s, m, 29); var all = new List<ZObject>();
                    var t = Case(w, "b", all); all.Add(Box(w, 60, 56));
                    var u = OzV11Kit.Place(w, un, t.CenterX - FromPx, t.CenterY, Team.Red);
                    int crushes = ZWorld.BigTankWreckCrushes;
                    bool rf = false; foreach (var b in all) rf |= u.RamFirstTarget(b);
                    w.SendWaypoints(u.Owner, u, new[] { PlainClick(t) });
                    var r = Drive(w, u, t, all, 9000, u);
                    int crushable = 0; foreach (var c in w.CrushableWrecks) if (w.IsBox(c)) crushable++;
                    int R = u.RangeTo(t);
                    bool good = r.rams == 0 && !rf && crushable == 0 && ZWorld.BigTankWreckCrushes == crushes && r.atColumn >= 2 && r.face >= R - 16 && r.minAny > 24;
                    ok &= good;
                    parts.Add($"{un}: ram / crush ticks {r.rams}, a RAM-FIRST box {rf}, boxes among the crushable wrecks {crushable}, wrecks crushed {ZWorld.BigTankWreckCrushes - crushes}, first shot {r.face} px from the face (range {R})");
                }
                Check(ok, "B13 every big-tank rule on (DV180-DV196), a stack driven into: never a ram / crush / shove / chain target; " + string.Join("; ", parts));
            }
            finally { foreach (var kv in was) typeof(ZWorld).GetField(kv.Key).SetValue(null, kv.Value); }
        }

        static void B14(string s, string m)
        {
            var w = World(s, m, 29);
            var col = new[] { Box(w, 60, 50), Box(w, 60, 50), Box(w, 60, 50) };
            var front = Box(w, 57, 50);                                                       // between the tank and the column
            var behind = Box(w, 63, 50);                                                      // behind the column
            var u = OzV11Kit.Place(w, "heavy", 60 * 16 - 90, 50 * 16 + 16, Team.Red);
            Step(w);
            bool ok = true; var sb2 = new StringBuilder();
            foreach (var b in new[] { col[0], col[1], col[2], behind })
            {
                bool inR = u.WithinAttackRadius(b), line = u.HasLineOfSight(b);
                ok &= inR && line;
                sb2.Append($"level {w.BoxLevelOf(b)} at ({b.LocX >> 4},{b.LocY >> 4}): in range {inR}, line {line}; ");
            }
            Check(ok, $"B14 a heavy tank {Face(col[0], u)} px from the column's face, the box at (57, 50) in between: no box blocks a shot at a box: " + sb2.ToString().TrimEnd(' ', ';'));
        }

        static string Scene15(string s, string m)
        {
            var w = World(s, m, 41); var all = new List<ZObject>();
            var t = Case(w, "b", all); all.Add(Box(w, 61, 50)); all.Add(Box(w, 55, 52));
            var u1 = OzV11Kit.Place(w, ZBigTanks.Bizon, t.CenterX - FromPx, t.CenterY, Team.Red);
            var u2 = OzV11Kit.Place(w, "heavy", t.CenterX - FromPx, t.CenterY + 48, Team.Red);
            w.SendWaypoints(u1.Owner, u1, new[] { PlainClick(t) }); w.SendWaypoints(u2.Owner, u2, new[] { CtrlAttack(all[2]) });
            var r = Drive(w, u1, t, all, 6000);
            return r.end + $" first {r.tick}/{r.face} heavy ({u2.CenterX},{u2.CenterY}) way attacks {w.BoxWayAttacks} scans {w.BoxWayScans} rand {w.Rng.Next(1 << 30)}";
        }

        static void B15(string s, string m)
        {
            string x = Scene15(s, m), y = Scene15(s, m);
            Check(x == y, "B15 the same scene twice (a Bizon's plain click into a stack, a heavy's attack on its top box): identical (" + x + ")");
        }

        static void B16(string s, string l02)
        {
            var w = World(s, l02, 1);
            Run(w, 60.0);
            Check(w.Boxes.Count == 0 && w.BoxWayScans == 0 && w.BoxWayAttacks == 0, $"B16 L02, 60 s of the match: no box, the way scan never ran ({w.BoxWayScans} scans, {w.BoxWayAttacks} attacks)");
        }
    }
}
