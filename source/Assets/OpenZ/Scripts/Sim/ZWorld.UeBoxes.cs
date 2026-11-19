using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// The Unit editor's BOX: the holo obstacle's rule (<see cref="PlaceObstacle"/>, the harbour-wreck rule DV39) on a
    /// <see cref="BoxTiles"/> x <see cref="BoxTiles"/> tile footprint, plus a stack LEVEL (whole numbers, no float, no random draw):
    /// <list type="bullet">
    /// <item><see cref="PlaceBox"/>: a box dropped where boxes stand goes on top of the highest standing box its footprint overlaps (level = that
    /// box's + 1; 0 on the floor), at most <see cref="BoxLevels"/> high. The footprint tiles no standing box covers must be free floor as for the
    /// obstacle (an overhang, brick style, is allowed: the box rests on the highest box under it).</item>
    /// <item>Every box is a neutral map item of <see cref="WreckType"/>, a destroyable impassable with its whole footprint impassable: units path
    /// round a stack as round the obstacle, only explosives attack it, any splash of its level hurts it, EngageBarrierBetween stops shots across
    /// it.</item>
    /// <item>A blast hurts the boxes of ONE level (<see cref="BoxBlastOff"/>, hook in ProcessMissileDamage): the level of the box it was aimed at
    /// (that box's last level when it is gone), else the floor level 0. The NUKE hurts every box.</item>
    /// <item>A box destroyed or taken out: in the same tick every box above settles under gravity, lowest first (<see cref="BoxSettle"/>, hook at
    /// the end of Tick; the Unit editor's Delete calls it at once, also while paused): its new level = 1 + the highest settled box its footprint
    /// overlaps, else 0. The fall is instant in the rules: nothing can stand under a box (its tiles stay closed for the whole stack), so no rule
    /// depends on the fall's time; the view draws it with gravity (View/OzUeObstacleView.Box.cs).</item>
    /// <item>Tiles: a box that died this tick opens its tiles in the settle (not one tick later in DeleteObject) and every box left closes its own
    /// again in the same call, so a tile under a standing box is never open for a tick.</item>
    /// </list>
    /// Never called in a level: every hook returns at its first line while no box was ever placed in this world (no table, no field written).
    /// Pure C# like the rest of the Sim folder.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>The box's footprint side in tiles (2 = 4 m), the most levels of a stack, the default hit points (the obstacle's).</summary>
        public const int BoxTiles = 2, BoxLevels = 6, PlaygroundBoxHp = 10000;

        List<ZObject> boxes;
        Dictionary<int, int> boxLevel;
        /// <summary>Checks: boxes placed, settles that found a box gone, boxes that fell (levels lost counted once per box and settle).</summary>
        public int BoxesPlaced, BoxSettles, BoxFalls;
        static readonly ZObject[] noBoxes = new ZObject[0];

        /// <summary>The standing boxes of this world (lowest level first after the last settle).</summary>
        public IReadOnlyList<ZObject> Boxes => boxes != null ? (IReadOnlyList<ZObject>)boxes : noBoxes;
        /// <summary>A box of this world (standing or not).</summary>
        public bool IsBox(ZObject o) => o != null && boxLevel != null && boxLevel.ContainsKey(o.Id);
        /// <summary>The box's stack level (0 = on the floor; a box that is gone keeps its last one), -1 = not a box.</summary>
        public int BoxLevelOf(ZObject o) => o != null ? BoxLevelOf(o.Id) : -1;
        public int BoxLevelOf(int id) => boxLevel != null && boxLevel.TryGetValue(id, out int l) ? l : -1;

        bool BoxStanding(ZObject b) => b != null && Get(b.Id) == b && !b.Destroyed && b.KillAt < 0;

        static bool BoxOverlaps(ZObject a, int tx, int ty, int tiles)
        {
            int ax = a.LocX >> 4, ay = a.LocY >> 4, aw = Math.Max(1, a.WidthPix >> 4), ah = Math.Max(1, a.HeightPix >> 4);
            return ax < tx + tiles && tx < ax + aw && ay < ty + tiles && ty < ay + ah;
        }

        /// <summary>A standing box covers tile (i, j).</summary>
        public bool BoxCovers(int i, int j)
        {
            if (boxes == null) return false;
            foreach (var b in boxes) if (BoxStanding(b) && BoxOverlaps(b, i, j, 1)) return true;
            return false;
        }

        /// <summary>Can a box stand with its top-left tile at (tx, ty), and on which level? why (when not): "edge", "high" (the stack is
        /// <see cref="BoxLevels"/> high), and for a footprint tile no box covers: "ground", "obstacle", "flag", "empty", "unit" as
        /// <see cref="ObstacleSpotFree"/> says them.</summary>
        public bool BoxSpotFree(int tx, int ty, out int level, out string why)
        {
            level = 0; why = null; const int n = BoxTiles;
            if (Map == null) { why = "edge"; return false; }
            if (tx < 1 || ty < 1 || tx + n > Map.width - 1 || ty + n > Map.height - 1) { why = "edge"; return false; }
            int top = -1;
            if (boxes != null) foreach (var b in boxes) if (BoxStanding(b) && BoxOverlaps(b, tx, ty, n)) top = Math.Max(top, boxLevel[b.Id]);
            level = top + 1;
            if (level >= BoxLevels) { why = "high"; return false; }
            for (int j = ty; j < ty + n; j++)
                for (int i = tx; i < tx + n; i++)
                {
                    if (BoxCovers(i, j)) continue;                                               // under a standing box: the stack holds it
                    var probe = new ZObject { WidthPix = 16, HeightPix = 16 };
                    probe.SetCords(i * 16, j * 16);
                    foreach (var o in Objects)
                        if (!o.Destroyed && o.KillAt < 0 && o.Type == WreckType && probe.IntersectsObject(o)) { why = "obstacle"; return false; }
                    foreach (var o in Objects)
                        if (!o.Destroyed && o.KillAt < 0 && o.Type == "flag" && probe.IntersectsObject(o)) { why = "flag"; return false; }
                    foreach (var o in Objects)
                        if (!o.Destroyed && o.KillAt < 0 && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner == Team.Neutral && probe.IntersectsObject(o)) { why = "empty"; return false; }
                    foreach (var o in Objects)
                        if (!o.Destroyed && o.KillAt < 0 && o.IsUnit && probe.IntersectsObject(o)) { why = "unit"; return false; }
                    var c = Map.ClassAt(i, j);
                    if (c != ZClass.Ground && c != ZClass.Road) { why = "ground"; return false; }
                    if (!Path.TilePassable(i, j, true)) { why = "obstacle"; return false; }
                }
            return true;
        }

        /// <summary>The playground's BOX: made as <see cref="PlaceObstacle"/> makes the obstacle (a neutral map item of <see cref="WreckType"/>,
        /// <see cref="BoxTiles"/> x <see cref="BoxTiles"/> tiles, hp hit points, a destroyable impassable over its whole footprint) on the level
        /// <see cref="BoxSpotFree"/> gives. Null (why = its word) when it cannot stand there.</summary>
        public ZObject PlaceBox(int tx, int ty, int hp, out int level, out string why)
        {
            if (!BoxSpotFree(tx, ty, out level, out why)) return null;
            var o = CreateObject("map_item", WreckType, tx * 16, ty * 16, Team.Neutral);
            o.WidthPix = o.HeightPix = BoxTiles * 16; o.SetCords(tx * 16, ty * 16);
            o.IsDestroyableImpass = true;                                                    // SetClassFlags gave AttackedByExplosives
            o.MaxHealth = o.Health = Math.Max(1, hp);
            SetMapImpassables(o);                                                            // the whole footprint, destroyable (a tile a box below closed already stays closed)
            (boxes ??= new List<ZObject>()).Add(o);
            (boxLevel ??= new Dictionary<int, int>())[o.Id] = level;
            BoxesPlaced++;
            return o;
        }

        /// <summary>ProcessMissileDamage hook (after PlatformBlastOff): true = this blast does not reach box o. A blast reaches the boxes of one
        /// level: the level of the box it was aimed at (its last level if it is gone), else the floor (0). The NUKE reaches all. False for
        /// anything that is not a box, and at once in every world without a box.</summary>
        internal bool BoxBlastOff(ZMissile m, ZObject o)
        {
            if (boxLevel == null || m.nuke || m.targetId == o.Id || !boxLevel.TryGetValue(o.Id, out int lv)) return false;
            int at = m.targetId >= 0 && boxLevel.TryGetValue(m.targetId, out int tl) ? tl : 0;
            return lv != at;
        }

        /// <summary>Tick hook (after HeavyFallTick): the stacks settle when a box is gone. Returns at once while no box stands.</summary>
        void BoxSettleTick()
        {
            if (boxes == null || boxes.Count == 0) return;
            BoxSettle();
        }

        /// <summary>The boxes that are gone (destroyed, dying or taken out) leave the stacks; a dying one opens its tiles now (its DeleteObject
        /// then leaves them); every box left settles under gravity, lowest first, and closes its tiles again. Returns the boxes that fell.</summary>
        public int BoxSettle()
        {
            if (boxes == null || boxes.Count == 0) return 0;
            List<ZObject> gone = null;
            for (int i = boxes.Count - 1; i >= 0; i--)
                if (!BoxStanding(boxes[i])) { (gone ??= new List<ZObject>()).Add(boxes[i]); boxes.RemoveAt(i); }
            if (gone == null) return 0;
            for (int i = gone.Count - 1; i >= 0; i--)
            {
                var g = gone[i];
                if (Get(g.Id) == g && g.IsDestroyableImpass) { SetMapImpassables(g, false); g.IsDestroyableImpass = false; }   // dead this tick, still in the world
            }
            boxes.Sort((a, b) => { int la = boxLevel[a.Id], lb = boxLevel[b.Id]; return la != lb ? la.CompareTo(lb) : a.Id.CompareTo(b.Id); });
            int falls = 0;
            for (int k = 0; k < boxes.Count; k++)
            {
                var b = boxes[k]; int tx = b.LocX >> 4, ty = b.LocY >> 4, nl = 0;
                for (int j = 0; j < k; j++) if (BoxOverlaps(boxes[j], tx, ty, BoxTiles)) nl = Math.Max(nl, boxLevel[boxes[j].Id] + 1);
                if (nl < boxLevel[b.Id]) { boxLevel[b.Id] = nl; falls++; }
            }
            foreach (var b in boxes) SetMapImpassables(b);                                   // a gone box opened tiles the boxes left share
            BoxSettles++; BoxFalls += falls;
            return falls;
        }
    }
}
