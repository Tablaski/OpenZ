using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// The heavy tank's two models (1.2 items 110, 116, 117, 118). The unit stays type "heavy" in the rules (stats, weapons, sounds, voices,
    /// the computer's play): only the drawn model and its pictures change.
    ///   v1 = the original model: Prefabs/Z_heavy, its materials and pictures, untouched.
    ///   v2 = the new model: Resources/OpenZ/HeavyV2/Z_heavy_v2 (made by the editor menu OpenZ/Heavy Tank v2/1 from
    ///        Models/Legacy/heavy_tank_v2.glb), its pictures in the same folder and ProductionBadge/x1..x4/face_heavy_v2_*.
    /// v2 carries every node name v1's code reads (TankSuspension, *TurretYaw, *BarrelPitch, *Recoil_L/_R, *Muzzle_L/_R, *Track_L/_R,
    /// heavy_HatchHinge, heavy_Board_*), so turret turn, barrel and turret recoil, hatch, ladder, track scroll, damage effects, death,
    /// break-apart and wreck run on it exactly as on v1.
    /// ONE switch: <see cref="Version"/>. 2 = the new model everywhere (units, the heavy tanks placed in a level at the start, unit cards,
    /// build menu, badges, Unit editor), 1 = back to the original everywhere. The testing overlay (P) places either model whatever the switch says (per unit: <see cref="Pin"/>).
    /// </summary>
    public static class OzHeavyTank
    {
        /// <summary>THE switch: 2 = heavy tank v2 (the new model), 1 = v1 (the original model). One line to go back.</summary>
        public const int Version = 2;

        public const string Type = "heavy", V1Key = "Z_heavy", V2Key = "Z_heavy_v2", Dir = "OpenZ/HeavyV2/";
        /// <summary>A node only the v2 model has: the frame of its modelled real hatch (RoundHatch uses it; it marks a v2 view).</summary>
        public const string FrameNode = "heavy_v2_HatchFrame";
        /// <summary>The testing overlay's two heavy tank tiles (their drop spawns type "heavy" drawn as that model).</summary>
        public const string TileV1 = "heavy@v1", TileV2 = "heavy@v2";

        static GameObject prefabV2; static bool looked;
        /// <summary>The v2 view prefab, or null while it is not built.</summary>
        public static GameObject PrefabV2
        {
            get
            {
                if (!prefabV2 && !looked) { looked = true; prefabV2 = Resources.Load<GameObject>(Dir + V2Key); }
                return prefabV2;
            }
        }
        /// <summary>Editor: forget what was looked up (the build menu calls it after it made the prefab).</summary>
        public static void Forget() { prefabV2 = null; looked = false; art.Clear(); }

        /// <summary>The model drawn when nothing else is asked: the switch (v1 while the v2 prefab is missing).</summary>
        public static int Drawn => Version == 2 && PrefabV2 ? 2 : 1;

        // ---------------------------------------------------------------- per unit (the testing overlay)
        static readonly Dictionary<int, int> pinned = new Dictionary<int, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() { pinned.Clear(); SceneSwaps = 0; SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) pinned.Clear(); }

        /// <summary>The testing overlay: this heavy tank is drawn as model v (1 or 2) whatever the switch says.</summary>
        public static void Pin(ZObject o, int v) { if (o != null && o.Type == Type && (v == 1 || v == 2)) pinned[o.Id] = v; }
        /// <summary>The model this unit is drawn as: its pin, else the switch.</summary>
        public static int VersionOf(ZObject o) => o == null ? 0 : pinned.TryGetValue(o.Id, out var v) ? v : Version;

        /// <summary>ZGame.SpawnUnitView: the view prefab of this unit (v2 when it is drawn as v2 and the v2 prefab exists; else the level's own).</summary>
        public static GameObject ForUnit(ZObject o, GameObject found)
        {
            if (o == null || o.Type != Type) return found;
            return VersionOf(o) == 2 && PrefabV2 ? PrefabV2 : found;
        }

        /// <summary>ZGame.Prefab: "Z_heavy" is the model the switch draws, "Z_heavy_v2" always the new one.</summary>
        public static GameObject PrefabFor(string key, GameObject found)
        {
            if (key == V2Key) return PrefabV2 ? PrefabV2 : found;
            if (key == V1Key && Version == 2 && PrefabV2) return PrefabV2;
            return found;
        }

        /// <summary>Heavy tanks placed in a level scene at the start (instances of v1's Z_heavy under ZodObjects: L16 and L17 have two each)
        /// that ZGame.BindViews took over (see <see cref="SceneView"/>).</summary>
        public static int SceneSwaps;

        /// <summary>ZGame.BindViews: a heavy tank placed in the level scene is an instance of v1's Z_heavy, bound as it is (it never passes
        /// SpawnUnitView). While this unit is drawn as v2, a v2 view takes its place: the same parent, sibling place, name, position, turn,
        /// scale and team as the scene's one, which is hidden and removed (as a factory-built heavy tank gets the v2 view in SpawnUnitView).
        /// Version = 1: the scene's own view, as before.</summary>
        public static Transform SceneView(Transform t, ZObject o)
        {
            if (!t || o == null || o.Type != Type || VersionOf(o) != 2 || !PrefabV2 || IsV2(t)) return t;
            var go = UnityEngine.Object.Instantiate(PrefabV2, t.parent);
            var n = go.transform;
            n.SetSiblingIndex(t.GetSiblingIndex());
            n.localPosition = t.localPosition; n.localRotation = t.localRotation; n.localScale = t.localScale;
            go.name = t.name; go.layer = t.gameObject.layer;
            var tp = go.GetComponent<TeamPaint>(); var tp0 = t.GetComponent<TeamPaint>(); if (tp) tp.Team = tp0 ? tp0.Team : o.Owner;
            t.name += "_v1"; t.gameObject.SetActive(false); UnityEngine.Object.Destroy(t.gameObject);
            SceneSwaps++;
            return n;
        }

        /// <summary>A view made from the v2 model.</summary>
        public static bool IsV2(Transform root)
        {
            if (!root) return false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == FrameNode) return true;
            return false;
        }

        // ---------------------------------------------------------------- footprint (UnitView.Bind)
        /// <summary>UnitView.Bind's spacing radius ("no touching"): v2's barrels reach 1.1 m past the hull front, which would widen the
        /// radius from v1's 2.08 m to 2.54 m. The heavy tank v2 measures the same way without its guns and antennas (barrels, recoil
        /// slides, muzzles, antennas): its hull is v1's 5.2 m long, so the radius is v1's.</summary>
        public static float ViewRadius(UnitView v, ZObject o, float radius)
        {
            if (!v || o == null || o.Type != Type || !IsV2(v.transform)) return radius;
            Bounds bb = default; bool any = false;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) || Gun(r.transform)) continue;
                if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
            }
            if (!any) return radius;
            return Mathf.Clamp(Mathf.Max(bb.extents.x, bb.extents.z) * 0.8f, 1.95f, 3f);
        }
        static bool Gun(Transform t)
        {
            for (; t; t = t.parent)
            {
                string n = t.name;
                if (n.Contains("Barrel") || n.Contains("Recoil") || n.Contains("Muzzle") || n.Contains("Antenna")) return true;
                if (n.Contains("TurretYaw") || n == "TankSuspension") return false;
            }
            return false;
        }

        // ---------------------------------------------------------------- team panel (TeamPaint)
        /// <summary>TeamPaint.PanelFor: v1's turret roof gets a team panel; v2 takes the team colour only on its stripes (as the original
        /// game's heavy tank), so its turret (materials heavy_tank_v2_*) gets none.</summary>
        public static bool NoRoofPanel(string key, Material m) => key == "HeavyTankTurret" && m && m.name.StartsWith("heavy_tank_v2_", System.StringComparison.Ordinal);

        // ---------------------------------------------------------------- pictures (UnitArt, OzBadgeArt, the testing overlay)
        static readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        static int artAsk;                                                                                 // the testing overlay's tiles ask for one model's picture (0 = the switch)
        static Texture2D Load(string name)
        {
            if (art.TryGetValue(name, out var t)) return t;                                               // a missing picture is looked up once too
            t = Resources.Load<Texture2D>(Dir + name); art[name] = t;
            return t;
        }
        static string Paint(Team team) { var k = TeamColors.Key(team); return k == "red" || k == "blue" ? k : UnitArt.HumanDrawnBlue ? "blue" : "red"; }

        /// <summary>UnitArt: the heavy tank's v2 picture (kind "icon" = the production card picture, "portrait" = the crewed unit card
        /// picture) while v2 is drawn; null otherwise (UnitArt then gives its own v1 picture).</summary>
        public static Texture2D Art(string kind, string type, Team team)
        {
            if (type != Type) return null;
            int v = artAsk != 0 ? artAsk : Drawn;
            return v == 2 ? Load(kind + "_heavy_tank_v2_" + Paint(team)) : null;
        }

        /// <summary>OzBadgeArt.FaceFor: the badge face's unit key, "heavy_v2" (face_heavy_v2_red / _blue) while v2 is drawn and its faces exist.</summary>
        public static string FaceUnit(string unit)
        {
            if (unit != Type || Drawn != 2) return unit;
            return OzBadgeArt.Get(1, "face_heavy_v2_red") != null || OzBadgeArt.Get(2, "face_heavy_v2_red") != null ? "heavy_v2" : unit;
        }

        /// <summary>The testing overlay: 1 / 2 for its two heavy tank tiles, 0 for every other tile.</summary>
        public static int TileVersion(string tile) => tile == TileV1 ? 1 : tile == TileV2 ? 2 : 0;
        /// <summary>The tile's name ("HEAVY v1" / "HEAVY v2"), null for other tiles.</summary>
        public static string TileLabel(string tile) { int v = TileVersion(tile); return v == 0 ? null : "HEAVY v" + v; }
        /// <summary>The tile's picture: that model's own art (the crewed portrait for a team, the icon for an empty one); null for other tiles.</summary>
        public static Texture TileArt(string tile, Team team)
        {
            int v = TileVersion(tile); if (v == 0) return null;
            artAsk = v;
            try { return team == Team.Neutral ? (Texture)UnitArt.Icon(Type, Team.Red) : ((Texture)UnitArt.VehiclePortrait(Type, team) ?? UnitArt.Icon(Type, team)); }
            finally { artAsk = 0; }
        }
        /// <summary>The testing overlay's drop: a heavy tank tile becomes type "heavy"; returns the model it asks for (0 = not a heavy tile).</summary>
        public static int FromTile(ref string type) { int v = TileVersion(type); if (v != 0) type = Type; return v; }
    }
}
