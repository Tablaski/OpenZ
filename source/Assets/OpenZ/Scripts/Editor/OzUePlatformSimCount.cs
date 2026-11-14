using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// no scene, nothing saved): menu OpenZ/V1.3/Unit Editor/3a Platforms Rules Count (no Play), and off-project on plain .NET (the kit's tools/harness).
    ///  S0 a world with no platform has no tables and every call answers nothing;
    ///  S1 both kinds on all four ramp sides: the ramp's foot is the only way on, 8 tiles wide, the rest closed, not destroyable, the sizes;
    ///  S2 a squad and every vehicle drive onto the deck by the ramp and back, the height never jumps, no closed tile entered;
    ///  S3 sight: ground to ground across it blocked (direct), free for arcing fire; deck to ground and ground to deck clear; the ramp's lower half not exempt;
    ///  S4 range and hit chance on a deck = on the ground: the same shots, the same damage;
    ///  S5 a blast at the wall's foot: nothing on the deck; aimed at the deck unit: it is hit, the ground beside the wall not;
    ///  S6 destroyed: everything on it dies the same tick, a unit past the ramp's foot lives, the tiles open, the height 0, no table entry left;
    ///  S7 a heavy tank on the attack order brings each kind down, over the rules' own damage, in the time the design says;
    ///  S8 a removal opens the tiles; the shuttle cannot land on it; another platform, a unit or a flag under it are refused;
    ///  S9 a parked shuttle counts as drawn: every drop hard against it, on four headings: the wall 0.5 m off the hull, the ship's own spot still a landing spot;
    ///  S10 the mouse's ray (PlatformInfo.RayOnBody), both kinds, all four sides, a camera on eight bearings: every point of the rails and kerbs, near and far, is a walkable tile of the platform; a ray to the free
    ///      floor on either side, or on the deck itself, is left alone;
    ///  S11 burning ground on a platform goes with it, fallen or removed; a patch on the ground beside it burns on.
    /// </summary>
    public static class OzUePlatformSimCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        static ZWorld World(string settings, string map, int seed)
        {
            var w = ZWorld.Create(settings, map, seed);
            w.NoGameEnd = true; w.FlagHoldSec = 1.0;
            w.Events.Clear();
            return w;
        }

        static void Run(ZWorld w, double sec) { int n = (int)Math.Round(sec / ZConst.TICK); for (int i = 0; i < n; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); } }
        static ZObject Plat(ZWorld w, int tx, int ty, int kind, int dir) { var o = w.PlacePlatform(tx, ty, kind, dir, -1, out string why); if (o == null) throw new Exception("no platform: " + why); return o; }
        /// <summary>The middle of a platform point in px (for a unit's centre).</summary>
        static (int x, int y) At(ZWorld.PlatformInfo p, double across, double along) { p.Map(across, along, out double x, out double y); return ((int)x, (int)y); }
        static ZObject Place(ZWorld w, string type, (int x, int y) c, Team t) => OzV11Kit.Place(w, type, c.x, c.y, t, 1);

        public static string Check(string settings, string map)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool los = ZWorld.LosDeviation; ZWorld.LosDeviation = true;
            try
            {
                S0(settings, map); S1(settings, map); S2(settings, map); S3(settings, map); S4(settings, map); S5(settings, map); S6(settings, map); S7(settings, map); S8(settings, map); S9(settings, map); S10(settings, map); S11(settings, map);
            }
            catch (Exception e) { Check(false, "the run stopped: " + e); }
            finally { ZWorld.LosDeviation = los; }
            return "[OpenZ PlatformRules] " + (fail == 0 ? "PASS " : "FAIL ") + pass + " of " + (pass + fail) + "\n" + sb;
        }

        static void S0(string s, string m)
        {
            var w = World(s, m, 1);
            var g = w.CreateRobotGroup("grunt", 800, 800, Team.Red, 1);
            bool none = !w.PlatformTablesMade && w.Platforms.Count == 0 && w.PlatformHeightM(808, 808) == 0 && w.PlatformSeatId(g) == 0 && w.PlatformAtTile(50, 50) == 0 && w.PlatformOf(g.Id) == null && !w.PlatformFallen(g.Id);
            w.PlatformClearTiles(g);
            Run(w, 1.0);
            Check(none && !w.PlatformTablesMade, "S0 no platform: no tables, the calls answer nothing, 1 s of ticks makes none");
        }

        static void S1(string s, string m)
        {
            for (int kind = 0; kind < 2; kind++)
                for (int dir = 0; dir < 4; dir++)
                {
                    var w = World(s, m, 1);
                    var o = Plat(w, 60, 30, kind, dir); var p = w.PlatformOf(o.Id);
                    int open = 0, closed = 0, ways = 0, footTiles = 0, bad = 0, destroyable = 0;
                    var inside = new HashSet<(int, int)>();
                    for (int y = p.TileY; y < p.TileY + p.Deep; y++) for (int x = p.TileX; x < p.TileX + p.Wide; x++) inside.Add((x, y));
                    foreach (var (x, y) in inside)
                    {
                        bool pass1 = w.Path.TilePassable(x, y, true), pass2 = w.Path.TilePassable(x, y, false); byte part = w.PlatformPartAt(x, y);
                        bool shut = part == ZWorld.PlatPartRail || part == ZWorld.PlatPartRailLow;
                        if (shut == pass1 || shut == pass2) bad++;
                        if (shut) closed++; else open++;
                        if (w.EngageBarrierBetween(x * 16 + 8, y * 16 + 8, x * 16 + 8, y * 16 + 8)) destroyable++;
                        if (shut) continue;
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            if (inside.Contains((x + dx, y + dy))) continue;
                            ways++;
                            p.Local(x * 16 + 8, y * 16 + 8, out _, out double al);
                            if ((int)al == p.Rows - 1) footTiles++;
                        }
                    }
                    int rows = 1 + 8 + ZWorld.PlatformRampRows[kind];
                    Check(ways == 8 && footTiles == 8 && bad == 0 && destroyable == 0 && open == 8 * (8 + ZWorld.PlatformRampRows[kind]) && closed == 10 * rows - open && inside.Count == 10 * rows,
                          $"S1 kind {kind} ramp side {dir}: {p.Wide} x {p.Deep} tiles, {open} open, {closed} closed (none destroyable), {ways} ways on, all {footTiles} over the ramp's foot row");
                    double top = w.PlatformHeightM(At(p, 5, 5).x, At(p, 5, 5).y), mid = w.PlatformHeightM(At(p, 5, 9 + p.RampRows * 0.5).x, At(p, 5, 9 + p.RampRows * 0.5).y), foot = w.PlatformHeightM(At(p, 5, p.Rows - 0.05).x, At(p, 5, p.Rows - 0.05).y);
                    Check(Math.Abs(top - p.DeckM) < 1e-6 && Math.Abs(mid - p.DeckM * 0.5) < 0.05 && foot < 0.1, $"S1 kind {kind} ramp side {dir}: height {top:0.00} m on the deck, {mid:0.00} m half way down the ramp, {foot:0.00} m at its foot");
                }
        }

        static void S2(string s, string m)
        {
            var types = new[] { "grunt", "jeep", "light", "medium", ZMTank.Type, "heavy", "apc", "missile_launcher", "crane" };
            for (int kind = 0; kind < 2; kind++)
                foreach (int dir in new[] { 0, 3 })
                    foreach (var type in types)
                    {
                        var w = World(s, m, 3);
                        var o = Plat(w, 60, 30, kind, dir); var p = w.PlatformOf(o.Id);
                        var start = At(p, 5, p.Rows + 6);
                        var u = Place(w, type, start, Team.Red);
                        var deck = At(p, 5, 5);
                        OzV11Kit.PlayerMove(w, u, deck.x, deck.y, false);
                        double jump = 0, last = 0, topSeen = 0, t = 0; int shut = 0; bool arrived = false;
                        while (t < 120.0)
                        {
                            w.Tick(ZConst.TICK); w.Events.Clear(); t += ZConst.TICK;
                            double h = w.PlatformHeightM(u.CenterX, u.CenterY);
                            jump = Math.Max(jump, Math.Abs(h - last)); last = h; topSeen = Math.Max(topSeen, h);
                            byte part = w.PlatformPartAt(u.CenterX >> 4, u.CenterY >> 4);
                            if (part == ZWorld.PlatPartRail || part == ZWorld.PlatPartRailLow) shut++;
                            if (h >= p.DeckM - 1e-6 && !u.IsMoving) { arrived = true; break; }
                        }
                        double up = t;
                        OzV11Kit.PlayerMove(w, u, start.x, start.y, false);
                        bool back = false; t = 0;
                        while (t < 120.0)
                        {
                            w.Tick(ZConst.TICK); w.Events.Clear(); t += ZConst.TICK;
                            double h = w.PlatformHeightM(u.CenterX, u.CenterY);
                            jump = Math.Max(jump, Math.Abs(h - last)); last = h;
                            byte part = w.PlatformPartAt(u.CenterX >> 4, u.CenterY >> 4);
                            if (part == ZWorld.PlatPartRail || part == ZWorld.PlatPartRailLow) shut++;
                            if (h <= 0.0 && o.Contains(u.CenterX, u.CenterY) == false && !u.IsMoving) { back = true; break; }
                        }
                        Check(arrived && back && jump < 1.0 && shut == 0 && Math.Abs(topSeen - p.DeckM) < 1e-6,
                              $"S2 kind {kind} side {dir} {type}: up in {up:0.0} s, back in {t:0.0} s, biggest height step in a tick {jump:0.000} m, {shut} ticks on a closed tile");
                    }
        }

        static void S3(string s, string m)
        {
            var w = World(s, m, 1);
            var o = Plat(w, 60, 30, ZWorld.PlatformHigh, 0); var p = w.PlatformOf(o.Id);
            var left = Place(w, "light", At(p, -3, 5), Team.Red);
            var right = Place(w, "light", At(p, 13, 5), Team.Blue);
            var deck = Place(w, "light", At(p, 5, 5), Team.Red);
            var rampHi = Place(w, "grunt", At(p, 5, 10), Team.Red);
            var rampLo = Place(w, "grunt", At(p, 5, p.Rows - 2), Team.Red);
            var otherSide = Place(w, "grunt", At(p, 13, p.Rows - 2), Team.Blue);
            var lowLeft = Place(w, "grunt", At(p, -3, p.Rows - 2), Team.Blue);
            var how = Place(w, "howitzer", At(p, -3, 9), Team.Red);
            w.Tick(ZConst.TICK);
            var sg = w.Sight;
            Check(!sg.Clear(left, right), "S3 ground to ground across the deck: blocked");
            Check(sg.Clear(deck, right) && sg.Clear(right, deck), "S3 deck to ground and ground to deck: clear both ways");
            Check(sg.Clear(rampHi, right) && sg.Clear(right, rampHi), "S3 the ramp's upper half: clear, as the deck");
            Check(w.PlatformSeatId(rampLo) == 0 && w.PlatformSeatId(rampHi) == o.Id && w.PlatformSeatId(deck) == o.Id, "S3 seat: the ramp's lower half is not on the platform, the upper half and the deck are");
            Check(sg.Clear(lowLeft, otherSide), "S3 across the ramp's lower half: clear (it stands under 4.6 m and is not a wall)");
            Check(how.ArcingFire && !left.ArcingFire, "S3 the howitzer's fire arcs (it needs no line), the tank's does not");
            Check(!w.EngageBarrierBetween(deck.CenterX, deck.CenterY, right.CenterX, right.CenterY) && !w.EngageBarrierBetween(right.CenterX, right.CenterY, deck.CenterX, deck.CenterY), "S3 no destroyable barrier stops shots from or to the deck");
            Check(sg.Clear(left, o) && sg.Clear(right, o), "S3 the platform itself can be fired at from both sides");
            o.SetHealth(0); w.UpdateObjectHealth(o); w.Tick(ZConst.TICK); w.Tick(ZConst.TICK);
            var sg2 = w.Sight;
            Check(sg2.Clear(left, right), "S3 it is gone: ground to ground across its place is clear");
        }

        static int Fight(string s, string m, bool onDeck, string shooter)
        {
            var w = World(s, m, 7);
            var o = Plat(w, onDeck ? 60 : 120, 30, ZWorld.PlatformMid, 0); var q = new ZWorld.PlatformInfo { Kind = ZWorld.PlatformMid, Dir = 0, TileX = 60, TileY = 30 };
            var a = Place(w, shooter, At(q, 5, 2), Team.Red);
            var t = Place(w, "heavy", At(q, 5, -5), Team.Blue);                                        // 14 m: in every weapon's range, no walk
            t.MoveSpeed = 0; t.Damage = 0; t.MaxHealth = t.Health = 900000000;
            OzV11Kit.PlayerAttack(w, a, t);
            Run(w, 25.0);
            return t.MaxHealth - t.Health;
        }

        static void S4(string s, string m)
        {
            foreach (var shooter in new[] { "light", "heavy", "gun" })
            {
                int low = Fight(s, m, false, shooter), high = Fight(s, m, true, shooter);
                Check(low > 0 && low == high, $"S4 {shooter}: {high} HP in 25 s from a deck, {low} HP from the ground at the same place: range, hit chance and damage do not change");
            }
        }

        static readonly FieldInfo NewM = typeof(ZWorld).GetField("newMissiles", BindingFlags.NonPublic | BindingFlags.Instance);
        static void Blast(ZWorld w, int x, int y, int target)
        {
            var l = (List<ZMissile>)NewM.GetValue(w);
            l.Add(new ZMissile { x = x, y = y, damage = 500, radius = 90, team = Team.Neutral, targetId = target, explodeTime = w.Time });
        }

        static void S5(string s, string m)
        {
            var w = World(s, m, 1);
            var o = Plat(w, 60, 30, ZWorld.PlatformHigh, 0); var p = w.PlatformOf(o.Id);
            var onDeck = Place(w, "tough", At(p, 2, 5), Team.Red);
            var below = Place(w, "tough", At(p, -1.5, 5), Team.Blue);
            int d0 = onDeck.Health, b0 = below.Health;
            var foot = At(p, -0.5, 5);
            Blast(w, foot.x, foot.y, -1); Run(w, 0.2);
            Check(onDeck.Health == d0 && below.Health < b0, $"S5 a blast at the wall's foot: the deck unit lost {d0 - onDeck.Health} HP, the unit on the ground {b0 - below.Health} HP");
            d0 = onDeck.Health; b0 = below.Health;
            Blast(w, foot.x, foot.y, onDeck.Id); Run(w, 0.2);
            Check(onDeck.Health < d0 && below.Health == b0, $"S5 aimed at the deck unit, the shot's scatter at the wall's foot: it lost {d0 - onDeck.Health} HP, the unit on the ground {b0 - below.Health} HP");
            d0 = onDeck.Health; b0 = below.Health;
            var deck = At(p, 3, 5);
            Blast(w, deck.x, deck.y, -1); Run(w, 0.2);
            Check(onDeck.Health < d0 && below.Health == b0, $"S5 a blast on the deck: the deck unit lost {d0 - onDeck.Health} HP, the unit on the ground {b0 - below.Health} HP");
        }

        static void S6(string s, string m)
        {
            for (int kind = 0; kind < 2; kind++)
            {
                var w = World(s, m, 1);
                var o = Plat(w, 60, 30, kind, 0); var p = w.PlatformOf(o.Id);
                var on = new List<ZObject>
                {
                    Place(w, "grunt", At(p, 3, 3), Team.Red), Place(w, "heavy", At(p, 6, 4), Team.Red), Place(w, "gun", At(p, 3, 7), Team.Blue),
                    w.CreateObject("vehicle", "jeep", At(p, 6, 8).x - 16, At(p, 6, 8).y - 16, Team.Neutral), Place(w, "grunt", At(p, 4, p.Rows - 2), Team.Blue),
                };
                var off = new List<ZObject> { Place(w, "grunt", At(p, 4, p.Rows + 2), Team.Red), Place(w, "jeep", At(p, -3, 5), Team.Red) };
                int id = o.Id;
                var rail = (p.TileX, p.TileY + 3);
                bool shutBefore = !w.Path.TilePassable(rail.Item1, rail.Item2, true);
                o.SetHealth(0); w.UpdateObjectHealth(o, -1);
                bool all = on.All(x => x.Destroyed && x.KillAt >= 0), same = on.All(x => w.PlatformFallen(x.Id)), live = off.All(x => !x.Destroyed && !w.PlatformFallen(x.Id));
                bool open = w.Path.TilePassable(rail.Item1, rail.Item2, true) && w.Path.TilePassable(rail.Item1, rail.Item2, false);
                bool table = w.PlatformAtTile(p.TileX + 3, p.TileY + 3) == 0 && w.PlatformHeightM(At(p, 5, 5).x, At(p, 5, 5).y) == 0 && w.Platforms.Count == 0;
                int deaths = w.Events.Count(e => e.type == ZEventType.Death), first = w.Events.FindIndex(e => e.type == ZEventType.Death);
                bool order = first >= 0 && w.Events[first].objId == id;
                w.Tick(ZConst.TICK); w.Events.Clear();
                w.Tick(ZConst.TICK); w.Events.Clear();
                Check(shutBefore && all && same && live && open && table, $"S6 kind {kind}: {on.Count} on it dead the same tick, {off.Count} beside it alive, the tiles open, height 0, table empty");
                Check(order && deaths >= on.Count + 1 && w.Get(id) == null, $"S6 kind {kind}: the platform's own Death event comes first ({deaths} deaths in the frame): its deck is gone before the units die; the object is off the world after the tick");
            }
        }

        static void S7(string s, string m)
        {
            for (int kind = 0; kind < 2; kind++)
            {
                var w = World(s, m, 11);
                var o = Plat(w, 60, 30, kind, 0); var p = w.PlatformOf(o.Id);
                var robot = Place(w, "grunt", At(p, 5, 5), Team.Red);
                var tank = Place(w, "heavy", At(p, 13, 5), Team.Blue);
                bool can = tank.CanAttackObject(o);
                var rifle = Place(w, "grunt", At(p, 14, 7), Team.Blue);
                bool rifleCan = rifle.CanAttackObject(o);
                OzV11Kit.PlayerAttack(w, tank, o);
                double t = 0; while (t < 120.0 && !o.Destroyed) { w.Tick(ZConst.TICK); w.Events.Clear(); t += ZConst.TICK; }
                Check(can && !rifleCan && o.Destroyed && robot.Destroyed, $"S7 kind {kind}: a heavy tank's attack order brings it down in {t:0.0} s ({o.MaxHealth} HP), a rifle cannot attack it, the robot on it dies with it");
            }
        }

        static void S8(string s, string m)
        {
            var w = World(s, m, 1);
            var o = Plat(w, 60, 30, ZWorld.PlatformHigh, 0); var p = w.PlatformOf(o.Id);
            var deck = At(p, 5, 5);
            bool landing = w.ShuttleLandSpot(deck.x, deck.y, null, 0.0, out _, out string lw);
            Check(!landing, "S8 the shuttle cannot land on a deck (" + lw + ")");
            var twice = w.PlacePlatform(62, 32, ZWorld.PlatformMid, 0, -1, out string why);
            Check(twice == null && why == "obstacle", "S8 a platform on a platform is refused (" + why + ")");
            var g = w.CreateRobotGroup("grunt", 100 * 16, 60 * 16, Team.Red, 1);
            var on = w.PlacePlatform(96, 50, ZWorld.PlatformMid, 0, -1, out why);
            Check(on == null && why == "unit", "S8 a platform over a unit is refused (" + why + ")");
            var f = w.CreateObject("map_item", "flag", 130 * 16, 60 * 16, Team.Neutral);
            on = w.PlacePlatform(126, 50, ZWorld.PlatformMid, 0, -1, out why);
            Check(on == null && why == "flag", "S8 a platform over a flag is refused (" + why + ")");
            var load = Place(w, "grunt", At(p, 5, 5), Team.Red);
            Check(w.PlatformLoad(o).Contains(load), "S8 PlatformLoad lists what stands on it");
            w.RemoveNow(load); w.PlatformClearTiles(o); int n = w.RemoveNow(o);                        // as View/OzUePlayground.Platforms.cs PlatformLeaves
            Check(n == 1 && w.Path.TilePassable(p.TileX, p.TileY, true) && w.PlatformHeightM(deck.x, deck.y) == 0 && w.Platforms.Count == 0, "S8 a removal: the object goes, the tiles open, no height left");
            var again = w.PlacePlatform(p.TileX, p.TileY, ZWorld.PlatformHigh, 2, 1234, out why);
            Check(again != null && again.Health == 1234 && w.PlatformOf(again.Id).Dir == 2, "S8 placed again on the same tiles with a saved side and HP");
        }

        static void S9(string s, string m)
        {
            bool play0 = ZWorld.ShuttlePlayableDeviation, on0 = ZWorld.ShuttleOn;
            ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true;
            try
            {
                foreach (float hd in new[] { 0f, 45f, 90f, 200f })
                {
                    var w = World(s, m, 1);
                    var ship = w.SpawnShuttle(80 * 16, 50 * 16, Team.Red, hd, out string sw);
                    if (ship == null) { Check(false, "S9 no ship: " + sw); continue; }
                    int tried = 0, lost = 0; double least = double.MaxValue;
                    for (int kind = 0; kind < 2; kind++)
                        for (int dir = 0; dir < 4; dir++)
                        {
                            ZWorld.PlatformSize(kind, dir, out int wide, out int deep);
                            int x0 = 80 - wide - 8, y0 = 50 - deep - 8, nx = wide + 18, ny = deep + 18;
                            var free = new bool[nx, ny];
                            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) free[x, y] = w.PlatformSpotFree(x0 + x, y0 + y, wide, deep, out _);
                            for (int y = 1; y < ny - 1; y++)
                                for (int x = 1; x < nx - 1; x++)
                                {
                                    if (!free[x, y] || (free[x - 1, y] && free[x + 1, y] && free[x, y - 1] && free[x, y + 1])) continue;   // hard against the ship: one tile nearer is refused
                                    var o = w.PlacePlatform(x0 + x, y0 + y, kind, dir, -1, out _);
                                    tried++;
                                    if (!w.ShuttlePadFreeAt(ship.CenterX, ship.CenterY, ship, ship.ShuttleHeadingDeg, out _)) lost++;
                                    for (int k = 0; k <= wide * 8; k++) least = Math.Min(least, Math.Min(ship.ShuttleOutlineGapM(o.LocX + k * 2.0, o.LocY), ship.ShuttleOutlineGapM(o.LocX + k * 2.0, o.LocY + o.HeightPix)));
                                    for (int k = 0; k <= deep * 8; k++) least = Math.Min(least, Math.Min(ship.ShuttleOutlineGapM(o.LocX, o.LocY + k * 2.0), ship.ShuttleOutlineGapM(o.LocX + o.WidthPix, o.LocY + k * 2.0)));
                                    w.PlatformClearTiles(o); w.RemoveNow(o);
                                }
                        }
                    Check(tried > 0 && lost == 0 && least >= 0.45, $"S9 a ship on heading {hd:0}: {tried} drops hard against it, the ship's own spot lost {lost} times, least wall to hull {least:0.00} m");
                    if (hd == 0f)
                    {
                        var near = w.PlacePlatform(69, 45, ZWorld.PlatformMid, 0, -1, out string why);
                        Check(near == null && why == "unit", "S9 the wall 3.0 m from the ship's middle, inside the hull, is refused (" + why + ")");
                        ship.Owner = Team.Neutral;
                        Check(!w.PlatformSpotFree(69, 45, 10, 16, out why) && why == "empty", "S9 an empty ship the same, in its own words (" + why + ")");
                        ship.Owner = Team.Red;
                    }
                }
            }
            finally { ZWorld.ShuttlePlayableDeviation = play0; ZWorld.ShuttleOn = on0; }
        }

        // the view's numbers: View/OzUePlatformView.cs KerbM, KerbWide (this file also runs on the Sim alone)
        const double KerbM = 0.4, KerbTiles = 0.2;

        /// <summary>A ray from the camera (cx, cy px; ch m) through the point (px, py px; ph m), as the view hands it over: direction per metre of ray, px for x and y, metres up.
        /// before: metres to what the ray has already, under 0 = nothing yet.</summary>
        static bool Ray(ZWorld.PlatformInfo p, double cx, double cy, double ch, double px, double py, double ph, double before, out double t, out double a, out double l)
        {
            double mx = (px - cx) / 8.0, my = (py - cy) / 8.0, mh = ph - ch, len = Math.Sqrt(mx * mx + my * my + mh * mh);
            return p.RayOnBody(cx, cy, ch, mx / len * 8.0, my / len * 8.0, mh / len, before < 0 ? len + 40.0 : before, KerbM, KerbTiles, out t, out a, out l);
        }

        static void S10(string s, string m)
        {
            for (int kind = 0; kind < 2; kind++)
                for (int dir = 0; dir < 4; dir++)
                {
                    var w = World(s, m, 1);
                    var o = Plat(w, 60, 30, kind, dir); var p = w.PlatformOf(o.Id);
                    p.Map(5, p.Rows * 0.5, out double mx, out double my);
                    int rim = 0, bad = 0, floor = 0, wrong = 0, deck = 0, took = 0;
                    for (int b = 0; b < 8; b++)
                    {
                        double ang = b * Math.PI / 4.0 + 0.1, cx = mx + Math.Cos(ang) * 45.0 * 8.0, cy = my + Math.Sin(ang) * 45.0 * 8.0, ch = 55.0;
                        void Rim(double a, double l, double h)
                        {
                            p.Map(a, l, out double x, out double y); rim++;
                            bool hit = Ray(p, cx, cy, ch, x, y, h, -1, out _, out double ra, out double rl);
                            p.Map(ra, rl, out double qx, out double qy);
                            int tx = (int)Math.Floor(Math.Round(qx) / 16.0), ty = (int)Math.Floor(Math.Round(qy) / 16.0); byte part = w.PlatformPartAt(tx, ty);   // rounded as ZGame.WorldToPx rounds
                            if (!hit || w.PlatformAtTile(tx, ty) != o.Id || part == ZWorld.PlatPartRail || part == ZWorld.PlatPartRailLow || part == ZWorld.PlatPartNone) bad++;
                        }
                        for (double l = 0.05; l < p.Rows - 0.05; l += 0.45)
                            foreach (var (a, kerb) in new[] { (0.05, true), (0.15, true), (0.3, false), (0.45, false), (9.55, false), (9.7, false), (9.85, true), (9.95, true) })
                                Rim(a, l, p.HeightAt(l) + (kerb ? KerbM : 0.0));
                        for (double a = 0.3; a < 9.8; a += 0.45) { Rim(a, 0.05, p.DeckM + KerbM); Rim(a, 0.15, p.DeckM + KerbM); Rim(a, 0.3, p.DeckM); Rim(a, 0.45, p.DeckM); }
                        for (double a = 2.0; a < 8.5; a += 1.5)
                            for (double l = 2.0; l < p.Rows - 1.0; l += 1.5)
                            {
                                p.Map(a, l, out double x, out double y); deck++;
                                double h = p.HeightAt(l), dm = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / 8.0, len = Math.Sqrt((ch - h) * (ch - h) + dm * dm);
                                if (Ray(p, cx, cy, ch, x, y, h, len, out _, out _, out _)) took++;
                            }
                    }
                    // the free floor: cameras square to a side, so nothing of the body stands between: 0.5 m off the wall on the camera's side, 14 m or more behind it (the ray then clears the kerb)
                    void Free(double ca, double cl, double a, double l)
                    {
                        p.Map(ca, cl, out double cx, out double cy); p.Map(a, l, out double x, out double y); floor++;
                        double dm = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / 8.0;
                        if (Ray(p, cx, cy, 55.0, x, y, 0.0, Math.Sqrt(55.0 * 55.0 + dm * dm), out _, out _, out _)) wrong++;
                    }
                    for (double l = 0.5; l < p.Rows; l += 1.5)
                    {
                        Free(-17.5, p.Rows * 0.5, -0.25, l); Free(-17.5, p.Rows * 0.5, 17.0, l);
                        Free(27.5, p.Rows * 0.5, 10.25, l); Free(27.5, p.Rows * 0.5, -7.0, l);
                    }
                    for (double a = 0.5; a < 10.0; a += 1.5) { Free(5.0, p.Rows + 22.0, a, p.Rows + 0.25); Free(5.0, p.Rows + 22.0, a, -14.0); }
                    Check(rim > 0 && bad == 0 && wrong == 0 && took == 0, $"S10 kind {kind} ramp side {dir}: {rim} rays on rails and kerbs from 8 bearings, {bad} off the walkable tiles; {floor} rays to the free floor, {wrong} taken; {deck} rays the walk surface answers, {took} taken");
                }
        }

        static void S11(string s, string m)
        {
            foreach (bool fall in new[] { true, false })
            {
                var w = World(s, m, 1);
                var o = Plat(w, 60, 30, ZWorld.PlatformHigh, 0); var p = w.PlatformOf(o.Id);
                ZWorld.GroundFire Lit((int x, int y) c) { var f = new ZWorld.GroundFire { x = c.x, y = c.y, team = Team.Red, by = -1, lit = w.Time, until = w.Time + 7.0 }; w.GroundFires.Add(f); return f; }
                var deck = Lit(At(p, 5, 5)); var ramp = Lit(At(p, 5, p.Rows - 2)); var beside = Lit(At(p, -2, 5));
                if (fall) { o.SetHealth(0); w.UpdateObjectHealth(o, -1); } else { w.PlatformClearTiles(o); w.RemoveNow(o); }
                Check(!w.GroundFires.Contains(deck) && !w.GroundFires.Contains(ramp) && w.GroundFires.Contains(beside), $"S11 {(fall ? "destroyed" : "removed")}: the patches on the deck and the ramp are out, the one on the ground beside the wall burns on");
            }
        }
    }
}
