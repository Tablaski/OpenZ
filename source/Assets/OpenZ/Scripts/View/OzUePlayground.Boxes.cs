using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// (Sim/ZWorld.UeBoxes.cs: PlaceBox, BoxSettle, BoxBlastOff); this part is the palette and the drops. A box is an obstacle of the playground
    /// in every list (obstacleIds, Placed, the cap, the pick, Delete, CLEAR FIELD, the death's hologram shatter), with its own id, names, footprint
    /// (2 x 2 tiles = 4 m) and height (4 m), its own drop rule (on top of the boxes under it: the stack's level) and its own block in a saved file
    /// ("boxes": [{"tx", "ty", "level", "hp"}], loaded lowest level first so every box lands on the level it was saved on).
    /// </summary>
    public static partial class OzUePlayground
    {
        /// <summary>The BOX extra's palette id.</summary>
        public const string Box = "box";
        public const int BoxHp = ZWorld.PlaygroundBoxHp;
        /// <summary>The box: 4 m on a side (its 2 x 2 tiles) and 4 m tall (a cube: its 1 m hologram grid 4 x 4 on every face); a stack's level = 4 m.</summary>
        public const float BoxSideM = ZWorld.BoxTiles * 2f, BoxHeightM = 4f;

        /// <summary>A box of the playground's world (standing or not).</summary>
        public static bool IsBox(ZObject o) { var w = W; return o != null && o.Type == ZWorld.WreckType && w != null && w.IsBox(o); }
        /// <summary>Its stack level in the rules (0 = on the floor; a box that is gone keeps its last one), -1 = not a box.</summary>
        public static int BoxLevel(ZObject o) { var w = W; return w != null ? w.BoxLevelOf(o) : -1; }
        /// <summary>The level a box dropped at the floor point p would get (-1 = it cannot stand there).</summary>
        public static int BoxLevelAt(Vector3 p)
        {
            var w = W; if (w == null || w.Map == null) return -1;
            var t = FootTile(w, p, ZWorld.BoxTiles);
            return w.BoxSpotFree(t.x, t.y, out int level, out _) ? level : -1;
        }
        /// <summary>The floor middle (y 0) of the footprint a box dropped at p would take.</summary>
        public static Vector3 BoxFootMiddle(Vector3 p)
        {
            var w = W; if (w == null || w.Map == null) return new Vector3(p.x, 0f, p.z);
            var t = FootTile(w, p, ZWorld.BoxTiles);
            var c = ZGame.PxToWorld((t.x + ZWorld.BoxTiles * 0.5) * 16, (t.y + ZWorld.BoxTiles * 0.5) * 16); c.y = 0f;
            return c;
        }

        static Spot BoxWhy(string why) => why == "obstacle" ? Spot.BoxOverObstacle : why == "flag" ? Spot.BoxOverFlag : why == "empty" ? Spot.BoxOverEmpty
                                        : why == "unit" ? Spot.BoxOverUnits : why == "high" ? Spot.BoxTooHigh : Spot.NoRoom;

        static string BoxWords(Spot s)
        {
            switch (s)
            {
                case Spot.BoxOverUnits: return "NO ROOM: A BOX CANNOT OVERLAP UNITS";
                case Spot.BoxOverFlag: return "NO ROOM: A BOX CANNOT OVERLAP THE FLAG";
                case Spot.BoxOverObstacle: return "NO ROOM: A BOX CANNOT OVERLAP AN OBSTACLE";
                case Spot.BoxOverEmpty: return "NO ROOM: A BOX CANNOT OVERLAP EMPTY HARDWARE";
                case Spot.BoxTooHigh: return "NO ROOM: A STACK IS AT MOST " + ZWorld.BoxLevels + " BOXES HIGH";
            }
            return null;
        }

        /// <summary>Test's BOX: the footprint under p (never on a ramp platform), on top of the boxes it overlaps.</summary>
        static Spot TestBox(ZWorld w, Vector3 p)
        {
            var t = FootTile(w, p, ZWorld.BoxTiles);
            if (OnAPlatform(w, t.x, t.y, ZWorld.BoxTiles)) return Spot.OnPlatform;
            return w.BoxSpotFree(t.x, t.y, out _, out string why) ? Spot.Ok : BoxWhy(why);
        }

        /// <summary>MakeOne's BOX: the Sim's box, an obstacle of the playground, and its hologram view (View/OzUeObstacleView.Box.cs).</summary>
        static ZObject MakeBox(ZWorld w, Vector3 p, out Spot why)
        {
            why = Spot.Ok;
            var t = FootTile(w, p, ZWorld.BoxTiles);
            if (OnAPlatform(w, t.x, t.y, ZWorld.BoxTiles)) { why = Spot.OnPlatform; return null; }
            var o = w.PlaceBox(t.x, t.y, BoxHp, out int level, out string bw);
            if (o == null) { why = BoxWhy(bw); return null; }
            obstacleIds.Add(o.Id);
            OzUeObstacleViews.Add(o);
            Debug.Log($"{OzUnitEditor.Tag} box #{o.Id} at tiles ({t.x}, {t.y}), level {level}");
            return o;
        }

        /// <summary>FieldToTree: the "boxes" block, lowest level first (only when a box stands).</summary>
        static void BoxesToTree(Dictionary<string, object> tree)
        {
            var w = W; if (w == null || w.Boxes.Count == 0) return;
            var list = new List<ZObject>(w.Boxes);
            list.Sort((a, b) => { int la = w.BoxLevelOf(a), lb = w.BoxLevelOf(b); return la != lb ? la.CompareTo(lb) : a.Id.CompareTo(b.Id); });
            var boxes = new List<object>();
            foreach (var o in list)
            {
                if (o.Destroyed || o.KillAt >= 0) continue;
                boxes.Add(new Dictionary<string, object> { ["tx"] = (double)(o.LocX >> 4), ["ty"] = (double)(o.LocY >> 4), ["level"] = (double)w.BoxLevelOf(o), ["hp"] = (double)o.Health });
            }
            if (boxes.Count > 0) tree["boxes"] = boxes;
        }

        /// <summary>FieldFromTree: the saved boxes, lowest level first (a box lands on the boxes placed before it, so it gets its saved level).</summary>
        static int BoxesFromTree(Dictionary<string, object> root, ref int skipped)
        {
            if (!root.TryGetValue("boxes", out var bx) || !(bx is List<object> list)) return 0;
            var rows = new List<(double level, int k, Dictionary<string, object> d)>();
            for (int k = 0; k < list.Count; k++)
            {
                var d = list[k] as Dictionary<string, object>;
                if (d == null || !Num(d, "tx", out _) || !Num(d, "ty", out _)) { skipped++; continue; }
                Num(d, "level", out double lv);
                rows.Add((lv, k, d));
            }
            rows.Sort((a, b) => a.level != b.level ? a.level.CompareTo(b.level) : a.k.CompareTo(b.k));
            int made = 0;
            foreach (var r in rows)
            {
                Num(r.d, "tx", out double tx); Num(r.d, "ty", out double ty);
                var c = ZGame.PxToWorld((tx + ZWorld.BoxTiles * 0.5) * 16, (ty + ZWorld.BoxTiles * 0.5) * 16); c.y = 0f;
                var res = DropCore(Box, Team.Neutral, c, 1, false, -1);
                if (!res.ok) { skipped++; continue; }
                if (Num(r.d, "hp", out double hp)) foreach (var o in res.made) o.Health = Mathf.Clamp((int)Math.Round(hp), 1, o.MaxHealth);
                made += res.groups;
            }
            return made;
        }
    }
}
