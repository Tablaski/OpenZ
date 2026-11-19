using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Sim/ZWorld.UeBoxGroup.cs): the lines of the DV194 counted check (Editor/OzV15UeBoxesCount.cs) for a GROUP ordered onto a box, headless on the Sim
    /// alone (the playground's map UE_grid). The group stands 20 tiles west of the box, 4 units per column, 56 px apart; every unit gets the same order
    /// in the same tick, as a selection's right-click gives it.
    ///  B17 2 units (a Bizon and a Mammoth, the Unity pass's case), the PLAIN right-click on a lone box: each its own place, both fire from inside
    ///      their own range, neither comes near the box;
    ///  B18 3 units (Bizon, Mammoth, heavy) on the bottom box of a 3-high stack: all three fire, the whole column goes (the box that falls into the
    ///      gap is the next one), then they stand at their places on no order, nobody on the box's place;
    ///  B19 6 units (3 Bizons, 3 Mammoths): all six fire, nobody near the box;
    ///  B20 12 units of 7 kinds (2 Bizons, 2 Mammoths, 2 heavy, 2 medium, 2 light tanks, a Tough squad, a missile launcher): who fires, how near;
    ///  B21 a group with units that cannot blast a box (a jeep, a rifle squad): the four that can fire, the two others wait off the box;
    ///  B22 the ATTACK order (Ctrl + right-click) by 3 and by 6 units: every unit fires at the aimed box from its own place;
    ///  B23 one unit alone: no box group (N29's rule as before); a STOP ends a unit's box order (it never fires at the box again);
    ///  B24 the same scene twice: the same places, ticks and hit points (deterministic);
    ///  B25 a world with no box (60 s of L02): no box group, no place, no box order.
    /// </summary>
    public static partial class OzV15UeBoxesCount
    {
        sealed class GroupRun
        {
            public int units, blasters, fired, groupOrders, places, minFace = int.MaxValue, minFaceIdle = int.MaxValue, outOfRangeShots, goneTick = -1, idleAtEnd, endMin = int.MaxValue, overlapTicks, orderAttacks;
            public List<ZObject> us = new List<ZObject>(); public int[] shots; public int[] firstFace; public string sig;
        }

        static void GroupChecks(string s, string m, string l02)
        {
            B17(s, m); B18(s, m); B19(s, m); B20(s, m); B21(s, m); B22(s, m); B23(s, m); B24(s, m); B25(s, l02);
        }

        static string[] Units(string list)
        {
            var r = new List<string>();
            foreach (var part in list.Split(','))
            {
                var p = part.Split('*'); int n = p.Length > 1 ? int.Parse(p[1]) : 1;
                for (int i = 0; i < n; i++) r.Add(p[0] == "bizon" ? ZBigTanks.Bizon : p[0] == "mammoth" ? ZBigTanks.Mammoth : p[0]);
            }
            return r.ToArray();
        }

        /// <summary>The group's run: `kase` a (one box) or b (a 3-high stack, the bottom box aimed), every box with hp x hpFactor hit points (time for
        /// the slowest unit to arrive); the order (plain MOVE onto the box's middle, or the Ctrl ATTACK) to every unit in the same tick; ticks until
        /// the ordered column is gone (+ `after` ticks) or maxTicks. stopUnit >= 0: that unit gets a STOP at stopTick.</summary>
        static GroupRun Group(string s, string m, string list, string kase, bool attack, int hpFactor, int maxTicks, int after = 600, int stopUnit = -1, int stopTick = 0)
        {
            var w = World(s, m, 29); var all = new List<ZObject>();
            var t = Case(w, kase, all);
            foreach (var b in all) { b.MaxHealth *= hpFactor; b.Health = b.MaxHealth; }
            var types = Units(list); var g = new GroupRun { units = types.Length, shots = new int[types.Length], firstFace = new int[types.Length] };
            for (int i = 0; i < types.Length; i++)
            {
                g.us.Add(OzV11Kit.Place(w, types[i], t.CenterX - FromPx - (i / 4) * 56, t.CenterY - 84 + (i % 4) * 56, Team.Red));
                g.firstFace[i] = -1;
            }
            foreach (var u in g.us) w.SendWaypoints(u.Owner, u, new[] { attack ? CtrlAttack(t) : PlainClick(t) });
            int tx = t.LocX >> 4, ty = t.LocY >> 4, stopAt = -1, stopShots = -1;
            for (int tk = 0; tk < maxTicks; tk++)
            {
                if (tk == stopTick && stopUnit >= 0) { w.SendStop(g.us[stopUnit].Owner, g.us[stopUnit]); stopAt = tk; stopShots = g.shots[stopUnit]; }
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type != ZEventType.MissileFired && e.type != ZEventType.Hitscan) continue;
                    var hit = w.Get(e.otherId); if (hit == null || !w.IsBox(hit)) continue;
                    int i = g.us.FindIndex(x => x.Id == e.objId || x.Minions.Exists(mm => mm != null && mm.Id == e.objId));
                    if (i < 0) continue;
                    g.shots[i]++;
                    int f = Face(hit, g.us[i]);
                    if (g.firstFace[i] < 0) g.firstFace[i] = f;
                    if (f > g.us[i].RangeTo(hit) + 2) g.outOfRangeShots++;
                }
                w.Events.Clear();
                bool standing = w.BoxAt(tx, ty) != null || w.BoxAt(tx + 1, ty + 1) != null;
                for (int i = 0; i < g.us.Count; i++)
                {
                    var u = g.us[i]; if (!OzV11Kit.Alive(u)) continue;
                    foreach (var b in all) if (OzV11Kit.Alive(b)) { int f = Face(b, u); g.minFace = Math.Min(g.minFace, f); if (!u.CanAttackObject(b)) g.minFaceIdle = Math.Min(g.minFaceIdle, f); }
                    for (int j = i + 1; j < g.us.Count; j++)
                        if (g.us[i].Kind == ZKind.Vehicle && g.us[j].Kind == ZKind.Vehicle && Math.Abs(u.CenterX - g.us[j].CenterX) < 20 && Math.Abs(u.CenterY - g.us[j].CenterY) < 20) g.overlapTicks++;
                }
                if (!standing && g.goneTick < 0) { g.goneTick = (int)w.TickIndex; maxTicks = Math.Min(maxTicks, tk + after); }
            }
            var sb = new StringBuilder();
            for (int i = 0; i < g.us.Count; i++)
            {
                var u = g.us[i];
                if (u.HasExplosives()) g.blasters++;
                if (g.shots[i] > 0) g.fired++;
                if (u.Waypoints.Count == 0) g.idleAtEnd++;
                g.endMin = Math.Min(g.endMin, Face(t, u));
                sb.Append(u.CenterX).Append(',').Append(u.CenterY).Append(':').Append(g.shots[i]).Append(' ');
            }
            foreach (var b in all) sb.Append(b.Health).Append('/');
            g.sig = sb.Append(" t").Append(w.TickIndex).ToString();
            g.groupOrders = w.BoxGroupOrders; g.places = w.BoxGroupPlaces; g.orderAttacks = w.BoxOrderAttacks;
            if (stopUnit >= 0) g.sig = (g.shots[stopUnit] - stopShots) + " shot(s) by the stopped unit after its STOP at tick " + stopAt + ", " + (g.us[stopUnit].Waypoints.Count == 0 ? "on no order" : "on an order");
            return g;
        }

        static string Shots(GroupRun g) { var p = new List<string>(); for (int i = 0; i < g.units; i++) p.Add(g.us[i].Type + " " + g.shots[i] + (g.firstFace[i] >= 0 ? " (first at " + g.firstFace[i] + " px of " + g.us[i].AttackRadius + ")" : "")); return string.Join(", ", p); }

        static void B17(string s, string m)
        {
            var g = Group(s, m, "bizon,mammoth", "a", false, 5, 9000);
            Check(g.groupOrders == 1 && g.places == 2 && g.fired == 2 && g.outOfRangeShots == 0 && g.minFace >= 96 && g.overlapTicks == 0,
                  $"B17 a Bizon and a Mammoth, the PLAIN right-click on one box (the fault left of N29): {g.places} places of 2, {g.fired} of 2 fire from inside their own range, the closest ever {g.minFace} px from the box's face (the fault: 20 px, no shot); {Shots(g)}");
        }

        static void B18(string s, string m)
        {
            var g = Group(s, m, "bizon,mammoth,heavy", "b", false, 2, 12000);
            Check(g.places == 3 && g.fired == 3 && g.outOfRangeShots == 0 && g.minFace >= 96,
                  $"B18 a Bizon, a Mammoth and a heavy tank onto the bottom box of a 3-high stack: {g.fired} of 3 fire, the closest ever {g.minFace} px from a box's face; {Shots(g)}");
            Check(g.goneTick > 0 && g.idleAtEnd == 3 && g.endMin >= 96 && g.orderAttacks >= 3,
                  $"B18 the whole column goes, the box that falls into the gap is the next one ({g.orderAttacks} attack orders at the 3 boxes by the 3 units; gone at tick {g.goneTick}); then {g.idleAtEnd} of 3 stand on no order, the nearest {g.endMin} px from where the box stood (nobody on its place)");
        }

        static void B19(string s, string m)
        {
            var g = Group(s, m, "bizon*3,mammoth*3", "a", false, 30, 12000);
            Check(g.places == 6 && g.fired == 6 && g.outOfRangeShots == 0 && g.minFace >= 56,
                  $"B19 3 Bizons and 3 Mammoths onto one box: {g.places} places of 6, {g.fired} of 6 fire, the closest ever {g.minFace} px from the box's face; {Shots(g)}");
        }

        static void B20(string s, string m)
        {
            var g = Group(s, m, "bizon*2,mammoth*2,heavy*2,medium*2,light*2,tough,missile_launcher", "a", false, 30, 12000);
            Check(g.fired >= 10 && g.outOfRangeShots == 0 && g.minFace >= 56,
                  $"B20 12 units of 7 kinds onto one box: {g.places} places of 12, {g.fired} of 12 fire (10 or more asked: the ground in range on the group's side does not hold 12 hulls, who finds no free way waits behind the firing line), the closest ever {g.minFace} px from the box's face (never on it); {Shots(g)}");
        }

        static void B21(string s, string m)
        {
            var g = Group(s, m, "bizon,mammoth,jeep,grunt,tough,light", "a", false, 30, 12000);
            Check(g.fired == 4 && g.shots[2] == 0 && g.shots[3] == 0 && g.outOfRangeShots == 0 && g.minFace >= 56 && g.minFaceIdle >= 56,
                  $"B21 a group with a jeep and a rifle squad (they cannot blast a box): the 4 that can fire ({g.fired}), the jeep and the squad wait off the box (the closest of the two {g.minFaceIdle} px from its face, no shot); {Shots(g)}");
        }

        static void B22(string s, string m)
        {
            var g = Group(s, m, "bizon,mammoth,heavy", "a", true, 5, 9000);
            Check(g.groupOrders == 1 && g.places == 3 && g.fired == 3 && g.outOfRangeShots == 0 && g.minFace >= 96,
                  $"B22 the ATTACK order (Ctrl + right-click) by 3 tanks: {g.fired} of 3 fire from their own places, the closest ever {g.minFace} px from the box's face; {Shots(g)}");
            g = Group(s, m, "light*6", "a", true, 30, 9000);
            Check(g.places == 6 && g.fired == 6 && g.outOfRangeShots == 0 && g.minFace >= 56,
                  $"B22 the ATTACK order by 6 light tanks (before: the first ones fire, the rest queue behind them out of range): {g.fired} of 6 fire, the closest ever {g.minFace} px from the box's face");
        }

        static void B23(string s, string m)
        {
            var g = Group(s, m, "heavy", "a", false, 1, 9000);
            Check(g.groupOrders == 0 && g.places == 0 && g.fired == 1 && g.firstFace[0] >= 144 - 16,
                  $"B23 one unit alone: no box group ({g.groupOrders} groups, {g.places} places), N29's rule as before (first shot {g.firstFace[0]} px from the face, range 144)");
            g = Group(s, m, "mammoth,heavy", "a", false, 30, 4500, 600, 1, 3000);
            Check(g.sig.StartsWith("0 shot(s)") && g.sig.EndsWith("on no order") && g.shots[1] > 0 && g.shots[0] > 0,
                  $"B23 a STOP ends a unit's box order: the heavy fired {g.shots[1]} shots, then {g.sig}; the Mammoth fires on ({g.shots[0]} shots)");
        }

        static void B24(string s, string m)
        {
            string x = Group(s, m, "bizon*2,mammoth*2,heavy*2", "b", false, 3, 9000).sig, y = Group(s, m, "bizon*2,mammoth*2,heavy*2", "b", false, 3, 9000).sig;
            Check(x == y, "B24 the same scene twice (6 tanks onto a stack's foot): identical (" + x + ")");
        }

        static void B25(string s, string l02)
        {
            var w = World(s, l02, 1);
            Run(w, 60.0);
            Check(w.Boxes.Count == 0 && w.BoxGroupOrders == 0 && w.BoxGroupPlaces == 0 && w.BoxOrderAttacks == 0,
                  $"B25 L02, 60 s of the match: no box, no box group ({w.BoxGroupOrders} groups, {w.BoxGroupPlaces} places, {w.BoxOrderAttacks} box-order attacks)");
        }
    }
}
