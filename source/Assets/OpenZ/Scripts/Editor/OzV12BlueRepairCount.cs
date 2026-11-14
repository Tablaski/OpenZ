using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of 1.2 item 67 (the R key's trip that stopped in front of the station; Sim/ZObject.RepairSpot.cs): on the levels 2, 16
    /// and 17, with the human red and with the human blue (PLAY AS BLUE), every repair station of the map is given to the human (its zone's
    /// flag; the cannons beside it are the human's too, as in the owner's picture), and a damaged vehicle of each kind (jeep, light tank, mech,
    /// heavy tank, APC, missile launcher, crane) is sent from several places around it with the R key's own order (View/PlayerInput.cs
    /// SendToRepair: UNIT_REPAIR at the station's centre - 8, attack_to off, through SEND_WAYPOINTS). Each trip in a world of its own: the
    /// level's map without its robots and vehicles, nobody owning its buildings but the station's zone (no fort gun, no defender), the
    /// cannons where the map has them, the computer of the other side present but silent, a still dummy of each side far away (the match never
    /// ends). Judged from the objects alone: the vehicle reaches the pad (within <see cref="PadLimit"/> game seconds), is healed to full on it,
    /// and comes back to within <see cref="BackPx"/> px of where it stood when it was sent (within <see cref="TripLimit"/> s); stuck = any of
    /// the three missing. The way back: DV62's note of the spot to come back to is read every tick (it must never move during a trip), and one
    /// trip per station and side gets a dodge in front of its repair waypoint 4 s after the send (the sim does that when a shell comes), which
    /// started the waypoint again before the kit. Level 2 has no repair station on its map: nothing to send (the R key answers NO REPAIR STATION).
    /// Menu: OpenZ/V1.2/Repair: 67 Blue Repair Check (headless). The quick form: one start per station and kind.
    /// </summary>
    public static class OzV12BlueRepairCount
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        public static readonly string[] Kinds = { "jeep", "light", "medium", "heavy", "apc", "missile_launcher", "crane" };
        public const double PadLimit = 150, TripLimit = 300, DodgeAt = 4;
        public const int BackPx = 24;
        /// <summary>The group trips (all seven kinds sent by one R press): the time allowed and how near home each has to stop (the
        /// movement layer's crowd arrival may stop one a little short of its own spot when another stands near it).</summary>
        public const double GroupLimit = 900;
        public const int GroupBackPx = 96;
        /// <summary>Where the vehicles start, from the station's entrance (px); a point whose tiles are taken or that the vehicle cannot reach
        /// from there is moved to the nearest free one within 4 tiles, else left out.</summary>
        public static readonly (int dx, int dy)[] Starts = { (0, 224), (-224, 48), (224, 48), (-176, 176), (176, 176), (-256, -96), (256, -96) };
        /// <summary>The group trip's starts: those, then the same 1.5 times and 0.75 times as far (more room for seven vehicles).</summary>
        public static readonly (int dx, int dy)[] GroupStarts = Starts.Concat(Starts.Select(p => (p.dx * 3 / 2, p.dy * 3 / 2))).Concat(Starts.Select(p => (p.dx * 3 / 4, p.dy * 3 / 4))).ToArray();

        public sealed class Trip
        {
            public string level, kind; public bool blue, dodge, group; public int station, sx, sy, retX = int.MinValue, retY, retMoves, endX, endY, spotMoves;
            public double padAt = -1, healedAt = -1, backAt = -1; public bool sent; public ulong fp = 1469598103934665603UL;
            public bool Ok => sent && padAt >= 0 && healedAt >= 0 && backAt >= 0 && retMoves == 0;
            public string Line() => $"{level} {(blue ? "blue" : "red ")} repair#{station} {kind,-16} from ({sx},{sy}){(dodge ? " +dodge" : "")}{(group ? " +group" : "")}: " +
                (!sent ? "NOT SENT" : $"pad {T(padAt)} healed {T(healedAt)} back {T(backAt)} end ({endX},{endY}) {Dist(endX, endY, sx, sy):0} px, way back noted ({retX},{retY}) moved {retMoves}, spots moved {spotMoves}") +
                $" fp {fp:x16}{(Ok ? "" : " STUCK")}";
            static string T(double t) => t < 0 ? "-" : t.ToString("0.0", Inv);
        }

        static double Dist(int ax, int ay, int bx, int by) => Math.Sqrt((ax - bx) * (double)(ax - bx) + (ay - by) * (double)(ay - by));
        static readonly FieldInfo RetX = typeof(ZObject).GetField("repairRetX", BindingFlags.NonPublic | BindingFlags.Instance),
                                  RetY = typeof(ZObject).GetField("repairRetY", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>The level's map for one trip: no robots or vehicles, no hero squad, nobody owning a building; the cannons stay.</summary>
        public static string TripMap(string mapJson)
        {
            var m = ZMapData.FromJson(mapJson);
            m.objects.RemoveAll(o => { string c = o.kind.Split(':')[0]; return c == "robot" || c == "vehicle"; });
            if (m.heroes != null) m.heroes.Clear();
            foreach (var o in m.objects) o.owner = "null";
            return UnityEngine.JsonUtility.ToJson(m);
        }

        static ZWorld NewWorld(string settings, string tripMap, string level, OzSideSetup s)
        {
            OzV12PlayBlueCount.PlaySession(level, false, false, s);
            ZWorld.ShuttleOn = false; ZWorld.L17StartShuttle = false; ZWorld.CpuRepairDeviation = false;
            var w = ZWorld.Create(settings, tripMap, 1, level == "L17");
            w.Bots.Add(new ZBot(w, s.Cpu, 102) { Ignored = true });
            foreach (var t in new[] { Team.Red, Team.Blue })
            {
                var d = w.CreateRobotGroup("grunt", t == Team.Red ? 24 : w.Map.WidthPix - 40, w.Map.HeightPix - 40, t, 1);
                d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = 1000000000;
            }
            w.Events.Clear();
            return w;
        }

        static List<ZObject> Stations(ZWorld w) => w.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "repair" && !o.Destroyed).OrderBy(o => o.Id).ToList();
        static ZObject ZoneFlag(ZWorld w, ZObject b) => w.Flags.FirstOrDefault(f => f.LinkedObjects.Contains(b)) ?? (b.ZoneIndex >= 0 ? w.Flags.FirstOrDefault(f => f.ZoneIndex == b.ZoneIndex) : null);

        static bool FreeStart(ZWorld w, ZObject v, int x, int y)
        {
            if (x < 40 || y < 40 || x > w.Map.WidthPix - 40 || y > w.Map.HeightPix - 40) return false;
            if (w.Path.WithinImpassable(x - (v.WidthPix >> 1) + 1, y - (v.HeightPix >> 1) + 1, v.WidthPix - 2, v.HeightPix - 2, out _, out _, false)) return false;
            foreach (var o in w.Objects) if (o != v && o.IsUnit && !o.Destroyed && Dist(o.CenterX, o.CenterY, x, y) < 56) return false;
            return true;
        }

        /// <summary>One R trip, its own world; null = no free reachable start near that offset.</summary>
        public static Trip Run(string settings, string tripMap, string level, bool blue, int stationIndex, string kind, int ox, int oy, bool dodge, Func<ZObject, string> trace = null)
        {
            var s = OzSides.For(blue, false);
            var w = NewWorld(settings, tripMap, level, s);
            var st = Stations(w)[stationIndex];
            var flag = ZoneFlag(w, st);
            if (flag != null) w.AwardZone(flag, s.Human); else st.Owner = s.Human;
            st.GetRepairEntrance(out int ex, out int ey);
            foreach (var c in w.Objects) if (c.Kind == ZKind.Cannon && !c.Destroyed && Dist(c.CenterX, c.CenterY, ex, ey) < 160) c.Owner = s.Human;
            var v = w.CreateObject("vehicle", kind, 0, 0, s.Human);
            int sx = ex + ox, sy = ey + oy; bool found = false;
            for (int r = 0; r <= 64 && !found; r += 8)
                for (int a = 0; a < 16 && !found; a++)
                {
                    int x = sx + (int)Math.Round(r * Math.Cos(a * Math.PI / 8)), y = sy + (int)Math.Round(r * Math.Sin(a * Math.PI / 8));
                    if (FreeStart(w, v, x, y) && w.Path.InSameRegion(x, y, ex, ey, false)) { sx = x; sy = y; found = true; }
                    if (r == 0) break;
                }
            if (!found) return null;
            v.SetCords(sx - (v.WidthPix >> 1), sy - (v.HeightPix >> 1));
            v.SetHealth(Math.Max(1, v.MaxHealth / 2));
            var t = new Trip { level = level, blue = blue, kind = kind, station = st.Id, sx = v.CenterX, sy = v.CenterY, dodge = dodge };
            int moves0 = OzV11Kit.Counter(typeof(ZObject), "RepairSpotMoves");
            var wp = ZWaypoint.Move(st.CenterX, st.CenterY, true); wp.refId = st.Id; wp.mode = WpMode.UnitRepair; wp.attackTo = false; wp.x = st.CenterX - 8; wp.y = st.CenterY;
            t.sent = w.SendWaypoints(s.Human, v, new[] { wp }) && v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.UnitRepair;
            if (!t.sent) return t;
            double t0 = w.Time; bool dodged = false;
            while (w.Time - t0 < TripLimit && !v.Destroyed)
            {
                if (dodge && !dodged && w.Time - t0 >= DodgeAt && v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.UnitRepair && !v.InRepairBay(st.Id))
                {
                    int nx = v.CenterX + 48, ny = v.CenterY;
                    if (w.Path.WithinImpassable(nx - 15, ny - 15, 30, 30, out _, out _, false)) nx = v.CenterX - 48;
                    v.Waypoints.Insert(0, new ZWaypoint { mode = WpMode.Dodge, refId = -1, x = nx, y = ny }); dodged = true;
                }
                w.Tick(ZConst.TICK);
                w.Events.Clear();
                t.fp = OzV11StrikeCount.Fold(t.fp, w);
                double now = w.Time - t0;
                if (RetX != null && v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.UnitRepair)
                {
                    int rx = (int)RetX.GetValue(v), ry = (int)RetY.GetValue(v);
                    if (t.retX == int.MinValue) { t.retX = rx; t.retY = ry; }
                    else if (rx != t.retX || ry != t.retY) { t.retMoves++; t.retX = rx; t.retY = ry; }
                }
                if (trace != null && Math.Abs(now * 2 - Math.Round(now * 2)) < 1e-6) trace(v);
                if (t.padAt < 0 && v.OnRepairPad(st.Id)) t.padAt = now;
                if (t.padAt >= 0 && t.healedAt < 0 && v.Health >= v.MaxHealth) t.healedAt = now;
                if (t.healedAt >= 0 && v.Waypoints.Count == 0 && !v.IsMoving && Dist(v.CenterX, v.CenterY, t.sx, t.sy) <= BackPx) { t.backAt = now; break; }
                if (t.padAt < 0 && now > PadLimit) break;
            }
            t.endX = v.CenterX; t.endY = v.CenterY; t.spotMoves = OzV11Kit.Counter(typeof(ZObject), "RepairSpotMoves") - moves0;
            return t;
        }

        /// <summary>One R press for a group: a damaged vehicle of each kind, each at its own start round the station (the next free one of
        /// <see cref="GroupStarts"/>), all sent in the same tick with the R key's order; they queue for the bay on the one waiting spot. Each
        /// passes as a single trip does (on the pad, healed, the way back never moved), home within <see cref="GroupBackPx"/> px.</summary>
        public static List<Trip> RunGroup(string settings, string tripMap, string level, bool blue, int stationIndex, Action<double, List<ZObject>> trace = null)
        {
            var s = OzSides.For(blue, false);
            var w = NewWorld(settings, tripMap, level, s);
            var st = Stations(w)[stationIndex];
            var flag = ZoneFlag(w, st);
            if (flag != null) w.AwardZone(flag, s.Human); else st.Owner = s.Human;
            st.GetRepairEntrance(out int ex, out int ey);
            foreach (var c in w.Objects) if (c.Kind == ZKind.Cannon && !c.Destroyed && Dist(c.CenterX, c.CenterY, ex, ey) < 160) c.Owner = s.Human;
            var vs = new List<ZObject>(); var trips = new List<Trip>(); var taken = new bool[GroupStarts.Length];
            for (int i = 0; i < Kinds.Length; i++)
            {
                var v = w.CreateObject("vehicle", Kinds[i], 0, 0, s.Human);
                bool found = false; int sx = 0, sy = 0;
                for (int k = 0; k < GroupStarts.Length && !found; k++)
                {
                    int si = (i + k) % GroupStarts.Length; if (taken[si]) continue;
                    for (int r = 0; r <= 64 && !found; r += 8)
                        for (int a = 0; a < 16 && !found; a++)
                        {
                            int x = ex + GroupStarts[si].dx + (int)Math.Round(r * Math.Cos(a * Math.PI / 8)), y = ey + GroupStarts[si].dy + (int)Math.Round(r * Math.Sin(a * Math.PI / 8));
                            if (FreeStart(w, v, x, y) && w.Path.InSameRegion(x, y, ex, ey, false)) { sx = x; sy = y; found = true; taken[si] = true; }
                            if (r == 0) break;
                        }
                }
                if (!found) { w.Objects.Remove(v); continue; }
                v.SetCords(sx - (v.WidthPix >> 1), sy - (v.HeightPix >> 1));
                v.SetHealth(Math.Max(1, v.MaxHealth / 2));
                vs.Add(v); trips.Add(new Trip { level = level, blue = blue, kind = Kinds[i], station = st.Id, sx = v.CenterX, sy = v.CenterY, group = true });
            }
            int moves0 = OzV11Kit.Counter(typeof(ZObject), "RepairSpotMoves");
            for (int i = 0; i < vs.Count; i++)
            {
                var wp = ZWaypoint.Move(st.CenterX, st.CenterY, true); wp.refId = st.Id; wp.mode = WpMode.UnitRepair; wp.attackTo = false; wp.x = st.CenterX - 8; wp.y = st.CenterY;
                trips[i].sent = w.SendWaypoints(s.Human, vs[i], new[] { wp }) && vs[i].Waypoints.Count > 0 && vs[i].Waypoints[0].mode == WpMode.UnitRepair;
            }
            double t0 = w.Time; ulong fp = 1469598103934665603UL;
            while (w.Time - t0 < GroupLimit)
            {
                w.Tick(ZConst.TICK);
                w.Events.Clear();
                fp = OzV11StrikeCount.Fold(fp, w);
                double now = w.Time - t0; bool all = true;
                if (trace != null && Math.Abs(now * 2 - Math.Round(now * 2)) < 1e-6) trace(now, vs);
                for (int i = 0; i < vs.Count; i++)
                {
                    var v = vs[i]; var t = trips[i];
                    if (!t.sent || v.Destroyed || t.backAt >= 0) continue;
                    if (RetX != null && v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.UnitRepair)
                    {
                        int rx = (int)RetX.GetValue(v), ry = (int)RetY.GetValue(v);
                        if (t.retX == int.MinValue) { t.retX = rx; t.retY = ry; }
                        else if (rx != t.retX || ry != t.retY) { t.retMoves++; t.retX = rx; t.retY = ry; }
                    }
                    if (t.padAt < 0 && v.OnRepairPad(st.Id)) t.padAt = now;
                    if (t.padAt >= 0 && t.healedAt < 0 && v.Health >= v.MaxHealth) t.healedAt = now;
                    if (t.healedAt >= 0 && v.Waypoints.Count == 0 && !v.IsMoving && Dist(v.CenterX, v.CenterY, t.sx, t.sy) <= GroupBackPx) t.backAt = now;
                    if (t.backAt < 0) all = false;
                }
                if (all) break;
            }
            for (int i = 0; i < vs.Count; i++) { trips[i].endX = vs[i].CenterX; trips[i].endY = vs[i].CenterY; trips[i].fp = fp; trips[i].spotMoves = OzV11Kit.Counter(typeof(ZObject), "RepairSpotMoves") - moves0; }
            return trips;
        }

        /// <summary>Every trip of the check, in a fixed order (the headless runner prints them for the before / after comparison).</summary>
        public static List<Trip> RunAll(string settings, Func<string, string> map, bool quick, Action<string> progress = null)
        {
            var all = new List<Trip>();
            using (OzV11Kit.GameSwitches())
            {
                foreach (var level in Levels)
                {
                    string tm = TripMap(map(level));
                    foreach (bool blue in new[] { false, true })
                    {
                        progress?.Invoke($"{level} human {(blue ? "blue" : "red")}");
                        var probe = NewWorld(settings, tm, level, OzSides.For(blue, false));
                        int n = Stations(probe).Count;
                        for (int si = 0; si < n; si++)
                        {
                            foreach (var kind in Kinds)
                                for (int k = 0, got = 0; k < Starts.Length && !(quick && got > 0); k++)
                                {
                                    var t = Run(settings, tm, level, blue, si, kind, Starts[k].dx, Starts[k].dy, false);
                                    if (t != null) { all.Add(t); got++; }
                                }
                            for (int k = 0; k < Starts.Length; k++)
                            {
                                var d = Run(settings, tm, level, blue, si, "medium", Starts[k].dx, Starts[k].dy, true);
                                if (d != null) { all.Add(d); break; }
                            }
                            all.AddRange(RunGroup(settings, tm, level, blue, si));                           // one R press for all seven kinds together
                        }
                    }
                }
            }
            return all;
        }

        public static string Check(string settings, Func<string, string> map, bool quick = false, Action<string> progress = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("1.2 item 67 the R key's repair trip, both sides (Sim/ZObject.RepairSpot.cs): " + (quick ? "quick (one start per station and kind)" : "full") +
                          $"; a trip passes when the vehicle is on the pad within {PadLimit:0} s, healed to full there and back within {BackPx} px of where it stood within {TripLimit:0} s, and its noted way back never moved" +
                          $"; per station also one R press for all seven kinds together (each home within {GroupBackPx} px, all within {GroupLimit:0} s)");
            if (RetX == null) sb.AppendLine("FAIL DV62's note of the way back (ZObject.repairRetX) is not in this build");
            var all = RunAll(settings, map, quick, progress);
            foreach (var level in Levels)
                foreach (bool blue in new[] { false, true })
                {
                    var l = all.Where(t => t.level == level && t.blue == blue).ToList();
                    string who = $"{level} human {(blue ? "blue" : "red")}";
                    if (l.Count == 0) { sb.AppendLine($"PASS {who}: no repair station on the map, nothing to send (the R key answers NO REPAIR STATION)"); continue; }
                    int ok = l.Count(t => t.Ok), pad = l.Count(t => t.padAt >= 0), healed = l.Count(t => t.healedAt >= 0), back = l.Count(t => t.backAt >= 0), moved = l.Count(t => t.retMoves > 0);
                    var stations = string.Join("; ", l.GroupBy(t => t.station).Select(g => $"repair#{g.Key}: {g.Count(t => t.Ok)}/{g.Count()} (waiting spot moved off a cannon in {g.Count(t => t.spotMoves > 0)}; slowest pad {g.Max(t => t.padAt):0.0} s)"));
                    sb.AppendLine($"{(ok == l.Count ? "PASS" : "FAIL")} {who}: {l.Count} trips ({l.Count(t => t.dodge)} with a dodge, {l.Count(t => t.group)} in one R press per station, stuck {l.Count(t => t.group && !t.Ok)} of them): on the pad {pad}, healed {healed}, back {back}, way back moved {moved}, stuck {l.Count - ok}; {stations}");
                }
            sb.AppendLine("trips:");
            foreach (var t in all) sb.AppendLine("   " + t.Line());
            return sb.ToString();
        }
    }
}
