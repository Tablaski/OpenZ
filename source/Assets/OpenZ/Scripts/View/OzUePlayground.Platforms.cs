using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// the EXTRAS. Dragged or click-placed like the obstacle; the drop point is the footprint's middle; the ramp's foot looks toward the viewer
    /// (the camera's turn snapped to four sides: turn the view to choose). Always neutral, one per drop, no unit of the cap.
    /// Units and guns dropped on a platform stand on its deck or ramp, never inside it: FreeSpot keeps to the level the drop started on, guns
    /// only on the deck (they are drawn flat). A crate and an obstacle are refused there; a flag stands on the deck or the ramp (1.5 ue_flag_anywhere,
    /// View/FlagView.Deck.cs), goes with the platform on a removal and comes down to the floor when the platform is destroyed.
    /// Delete and CLEAR FIELD never kill: a platform removed takes what stands on it off the field too, quietly, and what is dead or loose on it
    /// (OzUePlatformViews.Remove), so nothing is left in the air.
    /// Saved in the field block as "platforms": [{"id", "tx", "ty", "dir", "hp"}], read first (the units then land on the decks).
    /// </summary>
    public static partial class OzUePlayground
    {
        public const string PlatformMid = "platform_mid", PlatformHigh = "platform_high";

        public static bool IsPlatform(ZObject o) => o != null && o.Type == ZWorld.PlatformType;
        public static bool IsPlatformId(string id) => id == PlatformMid || id == PlatformHigh;
        static int PlatformKind(string id) => id == PlatformHigh ? ZWorld.PlatformHigh : ZWorld.PlatformMid;
        /// <summary>Medium / high palette id of a platform on the field.</summary>
        public static string PlatformIdOf(ZObject o)
        {
            var p = o != null && o.World != null ? o.World.PlatformOf(o.Id) : null;
            return p != null && p.Kind == ZWorld.PlatformHigh ? PlatformHigh : PlatformMid;
        }

        /// <summary>The ramp side a drop takes now: its foot toward the viewer (0 +ty at no turn).</summary>
        public static int PlatformDirNow => ((Mathf.RoundToInt(CamYaw / 90f) % 4) + 4) % 4;

        /// <summary>The rectangle a drop at p would take (no object is made); Kind and Dir set, O null.</summary>
        internal static ZWorld.PlatformInfo PlatformPlan(ZWorld w, string id, Vector3 p, int dir)
        {
            int kind = PlatformKind(id);
            ZWorld.PlatformSize(kind, dir, out int wide, out int deep);
            var px = Px(p);
            int tx = Mathf.RoundToInt(px.x / 16f - wide * 0.5f), ty = Mathf.RoundToInt(px.y / 16f - deep * 0.5f);
            tx = Mathf.Clamp(tx, 1, Mathf.Max(1, w.Map.width - 1 - wide)); ty = Mathf.Clamp(ty, 1, Mathf.Max(1, w.Map.height - 1 - deep));
            return new ZWorld.PlatformInfo { Kind = kind, Dir = dir, TileX = tx, TileY = ty, Wide = wide, Deep = deep };
        }

        static Spot PlatformWhy(string why) => why == "obstacle" ? Spot.PlatformOverObstacle : why == "flag" ? Spot.PlatformOverFlag : why == "empty" ? Spot.PlatformOverEmpty
                                             : why == "unit" ? Spot.PlatformOverUnits : Spot.NoRoom;

        static Spot TestPlatform(ZWorld w, string id, Vector3 p)
        {
            var q = PlatformPlan(w, id, p, PlatformDirNow);
            return w.PlatformSpotFree(q.TileX, q.TileY, q.Wide, q.Deep, out string why) ? Spot.Ok : PlatformWhy(why);
        }

        static ZObject MakePlatform(ZWorld w, string id, Vector3 p, out Spot why)
        {
            var q = PlatformPlan(w, id, p, PlatformDirNow);
            var o = w.PlacePlatform(q.TileX, q.TileY, q.Kind, q.Dir, -1, out string pw);
            if (o == null) { why = PlatformWhy(pw); return null; }
            why = Spot.Ok;
            OzUePlatformViews.Add(o);
            return o;
        }

        /// <summary>Any tile of a square footprint on a platform.</summary>
        static bool OnAPlatform(ZWorld w, int tx, int ty, int foot)
        {
            for (int j = 0; j < foot; j++) for (int i = 0; i < foot; i++) if (w.PlatformAtTile(tx + i, ty + j) != 0) return true;
            return false;
        }

        /// <summary>A standing gun's tiles are taken: a drop onto a gun stands beside it, never inside it.</summary>
        static bool OnAGun(ZWorld w, int tx, int ty, int foot)
        {
            var probe = new ZObject { WidthPix = foot * 16, HeightPix = foot * 16 }; probe.SetCords(tx * 16, ty * 16);
            foreach (var o in w.Objects) if (o.Kind == ZKind.Cannon && !o.Destroyed && o.KillAt < 0 && probe.IntersectsObject(o)) return true;
            return false;
        }

        /// <summary>A platform goes off the field: what stands on it first (Delete and CLEAR FIELD never kill), then its tiles.</summary>
        static void PlatformLeaves(ZGame g, ZWorld w, ZObject o)
        {
            foreach (var u in w.PlatformLoad(o)) RemoveQuiet(u);
            foreach (var f in new List<ZObject>(w.Flags)) if (f != null && !f.Destroyed && f.KillAt < 0 && o.Contains(f.CenterX, f.CenterY)) RemoveQuiet(f);   // ue_flag_anywhere: its flags too
            w.PlatformClearTiles(o);
        }

        /// <summary>The loose pieces (OzUePlayground.SweepDebris' three lists) whose place passes the test: a platform's fall takes those on its deck.</summary>
        internal static int SweepDebrisIn(Func<Vector3, bool> on)
        {
            var gone = new HashSet<GameObject>();
            foreach (var wp in UnityEngine.Object.FindObjectsByType<WreckPieces>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (wp && on(wp.transform.position)) gone.Add(wp.gameObject);
            foreach (var sp in UnityEngine.Object.FindObjectsByType<OzShuttleBreakup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (sp && on(sp.transform.position)) gone.Add(sp.gameObject);
            int layer = BuildingBreaker.DebrisLayer;
            foreach (var rb in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!rb || gone.Contains(rb.gameObject) || rb.GetComponentInParent<UnitView>(true) || !on(rb.transform.position)) continue;
                bool debris = rb.gameObject.layer == layer;
                if (!debris) foreach (var c in rb.GetComponentsInChildren<Collider>(true)) if (c.gameObject.layer == layer) { debris = true; break; }
                if (debris) gone.Add(rb.gameObject);
            }
            foreach (var go in gone) if (go) { go.SetActive(false); UnityEngine.Object.Destroy(go); }
            return gone.Count;
        }

        // ------------------------------------------------------------------------------------------------ the saved file
        static Dictionary<string, object> PlatformToTree(ZObject o)
        {
            var p = o.World.PlatformOf(o.Id);
            return new Dictionary<string, object> { ["id"] = PlatformIdOf(o), ["tx"] = (double)p.TileX, ["ty"] = (double)p.TileY, ["dir"] = (double)p.Dir, ["hp"] = (double)o.Health };
        }

        /// <summary>The "platforms" block, first of all: by the saved tile and side, not through DropCore (the saved side wins over the camera).</summary>
        static int PlatformsFromTree(ZWorld w, Dictionary<string, object> root, ref int skipped)
        {
            if (!root.TryGetValue("platforms", out var v) || !(v is List<object> list)) return 0;
            int made = 0;
            foreach (var e in list)
            {
                var d = e as Dictionary<string, object>;
                string id = d != null && d.TryGetValue("id", out var iv) ? iv as string : null;
                if (d == null || !IsPlatformId(id) || !Num(d, "tx", out double tx) || !Num(d, "ty", out double ty) || !Num(d, "dir", out double dir)) { skipped++; continue; }
                int hp = Num(d, "hp", out double h) ? (int)Math.Round(h) : -1;
                var o = w.PlacePlatform((int)tx, (int)ty, PlatformKind(id), (int)dir, hp, out _);
                if (o == null) { skipped++; continue; }
                OzUePlatformViews.Add(o);
                made++;
            }
            return made;
        }
    }
}
