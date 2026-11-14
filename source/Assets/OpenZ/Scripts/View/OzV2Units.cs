using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// A unit drawn by one of two models (1.2 v2 units: the light tank first; the heavy tank keeps its own View/OzHeavyTank.cs, which this
    /// follows line for line). The unit keeps its sim type in the rules (stats, weapons, sounds, voices, the computer's play, the minimap):
    /// only the drawn model and its pictures change.
    ///   v1 = the original model: Prefabs/&lt;V1Key&gt;, its materials and pictures, untouched;
    ///   v2 = the new model: Resources/&lt;Dir&gt;&lt;V2Key&gt; (made by the unit's editor menu), its pictures in the same folder
    ///        (icon_&lt;ArtName&gt;_v2_&lt;team&gt;, portrait_&lt;ArtName&gt;_v2_&lt;team&gt;) and ProductionBadge/x1..x4/face_&lt;FaceName&gt;_v2_&lt;team&gt;.
    /// v2 carries every node name v1's code reads, so turret turn, barrel and head recoil, hatch, ladder, tread, damage effects, death,
    /// break-apart and wreck run on it as on v1. ONE switch per unit: its own class's Version constant (View/OzLightTank.cs). The testing
    /// overlay (P) places either model whatever the switch says (per unit: <see cref="Pin"/>).
    /// </summary>
    public sealed class OzUnitModels
    {
        public readonly string Type, V1Key, V2Key, Dir, FrameNode, HatchPrefix, ArtName, FaceName, Label, MatPrefix, PanelKey;
        readonly System.Func<int> version;

        /// <param name="frameNode">a node only the v2 model has (the frame of its modelled hatch): it marks a v2 view</param>
        /// <param name="hatchPrefix">RoundHatch's node prefix of this type ("tank_"): the v2's modelled hatch is used, nothing is built</param>
        /// <param name="matPrefix">the v2 materials' name prefix (no team roof panel on them: the team colour is in their paint mask)</param>
        /// <param name="panelKey">the renderer key of v1's team roof panel (TeamPaint.PanelRules)</param>
        public OzUnitModels(string type, string v1Key, string v2Key, string dir, string frameNode, string hatchPrefix, string artName, string faceName,
                            string label, string matPrefix, string panelKey, System.Func<int> version)
        {
            Type = type; V1Key = v1Key; V2Key = v2Key; Dir = dir; FrameNode = frameNode; HatchPrefix = hatchPrefix; ArtName = artName; FaceName = faceName; Label = label;
            MatPrefix = matPrefix; PanelKey = panelKey; this.version = version;
        }

        /// <summary>The unit's switch: 2 = v2, 1 = v1.</summary>
        public int Version => version != null ? version() : 1;
        /// <summary>The testing overlay's two tiles of this unit (their drop spawns the sim type drawn as that model).</summary>
        public string TileV1 => Type + "@v1";
        public string TileV2 => Type + "@v2";
        /// <summary>VehicleLights / UnitBlinkers: the data entry of the v2 model ("&lt;type&gt;@v2"; v1 keeps "&lt;type&gt;").</summary>
        public string LightsKey => Type + "@v2";

        GameObject prefabV2; bool looked;
        /// <summary>The v2 view prefab, or null while it is not built.</summary>
        public GameObject PrefabV2
        {
            get
            {
                if (!prefabV2 && !looked) { looked = true; prefabV2 = Resources.Load<GameObject>(Dir + V2Key); }
                return prefabV2;
            }
        }
        /// <summary>Editor: forget what was looked up (the build menu calls it after it made the prefab).</summary>
        public void Forget() { prefabV2 = null; looked = false; art.Clear(); }

        /// <summary>The model drawn when nothing else is asked: the switch (v1 while the v2 prefab is missing).</summary>
        public int Drawn => Version == 2 && PrefabV2 ? 2 : 1;

        // ---------------------------------------------------------------- per unit (the testing overlay)
        readonly Dictionary<int, int> pinned = new Dictionary<int, int>();
        internal void ClearPins() => pinned.Clear();
        /// <summary>The testing overlay: this unit is drawn as model v (1 or 2) whatever the switch says.</summary>
        public void Pin(ZObject o, int v) { if (o != null && o.Type == Type && (v == 1 || v == 2)) pinned[o.Id] = v; }
        /// <summary>The model this unit is drawn as: its pin, else the switch.</summary>
        public int VersionOf(ZObject o) => o == null ? 0 : pinned.TryGetValue(o.Id, out var v) ? v : Version;

        /// <summary>ZGame.SpawnUnitView: the view prefab of this unit (v2 when it is drawn as v2 and the v2 prefab exists; else the level's own).</summary>
        public GameObject ForUnit(ZObject o, GameObject found)
        {
            if (o == null || o.Type != Type) return found;
            return VersionOf(o) == 2 && PrefabV2 ? PrefabV2 : found;
        }

        /// <summary>ZGame.Prefab: "&lt;V1Key&gt;" is the model the switch draws, "&lt;V2Key&gt;" always the new one.</summary>
        public GameObject PrefabFor(string key, GameObject found)
        {
            if (key == V2Key) return PrefabV2 ? PrefabV2 : found;
            if (key == V1Key && Version == 2 && PrefabV2) return PrefabV2;
            return found;
        }

        /// <summary>Units of this type placed in a level scene at the start that ZGame.BindViews took over (see <see cref="SceneView"/>).</summary>
        public int SceneSwaps;

        /// <summary>ZGame.BindViews: a unit placed in the level scene is an instance of v1's prefab, bound as it is (it never passes
        /// SpawnUnitView). While this unit is drawn as v2, a v2 view takes its place: the same parent, sibling place, name, position, turn,
        /// scale and team as the scene's one, which is hidden and removed. Version = 1: the scene's own view, as before.</summary>
        public Transform SceneView(Transform t, ZObject o)
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

        /// <summary>A view made from the v2 model (it has the v2 hatch frame node).</summary>
        public bool IsV2(Transform root)
        {
            if (!root) return false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == FrameNode) return true;
            return false;
        }

        /// <summary>TeamPaint.PanelFor: v1's turret roof gets a team panel; v2 takes the team colour only in its paint mask (the red block), so
        /// its renderers with the v2 materials get none.</summary>
        public bool NoRoofPanel(string key, Material m) => key == PanelKey && m && m.name.StartsWith(MatPrefix, System.StringComparison.Ordinal);

        // ---------------------------------------------------------------- pictures (UnitArt, OzBadgeArt, the testing overlay)
        readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        int artAsk;                                                                                      // a testing overlay tile asks for one model's picture (0 = the switch)
        Texture2D Load(string name)
        {
            if (art.TryGetValue(name, out var t)) return t;                                             // a missing picture is looked up once too
            t = Resources.Load<Texture2D>(Dir + name); art[name] = t;
            return t;
        }
        static string Paint(Team team) { var k = TeamColors.Key(team); return k == "red" || k == "blue" ? k : UnitArt.HumanDrawnBlue ? "blue" : "red"; }

        /// <summary>UnitArt: this unit's v2 picture (kind "icon" = the production card picture, "portrait" = the crewed unit card picture)
        /// while v2 is drawn; null otherwise (UnitArt then gives its own v1 picture).</summary>
        public Texture2D Art(string kind, string type, Team team)
        {
            if (type != Type) return null;
            int v = artAsk != 0 ? artAsk : Drawn;
            return v == 2 ? Load(kind + "_" + ArtName + "_v2_" + Paint(team)) : null;
        }

        /// <summary>OzBadgeArt.FaceFor: the badge face's unit key, "&lt;FaceName&gt;_v2" while v2 is drawn and its faces exist.</summary>
        public string FaceUnit(string unit)
        {
            if (unit != Type || Drawn != 2) return unit;
            string k = "face_" + FaceName + "_v2_red";
            return OzBadgeArt.Get(1, k) != null || OzBadgeArt.Get(2, k) != null ? FaceName + "_v2" : unit;
        }

        /// <summary>The testing overlay: 1 / 2 for this unit's two tiles, 0 for every other tile.</summary>
        public int TileVersion(string tile) => tile == TileV1 ? 1 : tile == TileV2 ? 2 : 0;
        /// <summary>The tile's picture: that model's own art (the crewed portrait for a team, the icon for an empty one); null for other tiles.</summary>
        public Texture TileArt(string tile, Team team)
        {
            int v = TileVersion(tile); if (v == 0) return null;
            artAsk = v;
            try { return team == Team.Neutral ? (Texture)UnitArt.Icon(Type, Team.Red) : ((Texture)UnitArt.VehiclePortrait(Type, team) ?? UnitArt.Icon(Type, team)); }
            finally { artAsk = 0; }
        }
    }

    /// <summary>
    /// The two-model units (<see cref="OzUnitModels"/>) and the one-line hooks the game calls (ZGame, UnitArt, OzBadgeArt, TeamPaint,
    /// RoundHatch, VehicleLights, UnitBlinkers, the testing overlay). A later two-model unit adds its OzUnitModels to <see cref="All"/>.
    /// </summary>
    public static class OzV2Units
    {
        static OzUnitModels[] all;
        /// <summary>Every two-model unit (the heavy tank keeps View/OzHeavyTank.cs).</summary>
        public static OzUnitModels[] All => all ??= new[] { OzLightTank.Models, OzMediumTank.Models, OzMlrs.Models, OzMech.Models, OzGun.Models };   // DV145: + the medium tank (one model: View/OzMediumTank.cs); 1.2 v2 units: + the MLRS v1 / v2 (View/OzMlrs.cs); + the mech v1 / v2 (View/OzMech.cs); + the gun v1 / v2 (View/OzGun.cs)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            foreach (var u in All) { u.ClearPins(); u.SceneSwaps = 0; }
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) foreach (var u in All) u.ClearPins(); }

        /// <summary>The two-model unit of this sim type, or null.</summary>
        public static OzUnitModels Of(string type)
        {
            if (type == null) return null;
            foreach (var u in All) if (u.Type == type) return u;
            return null;
        }

        /// <summary>ZGame.SpawnUnitView.</summary>
        public static GameObject ForUnit(ZObject o, GameObject found) { var u = o != null ? Of(o.Type) : null; return u != null ? u.ForUnit(o, found) : found; }
        /// <summary>ZGame.Prefab.</summary>
        public static GameObject PrefabFor(string key, GameObject found) { foreach (var u in All) found = u.PrefabFor(key, found); return found; }
        /// <summary>ZGame.BindViews.</summary>
        public static Transform SceneView(Transform t, ZObject o) { var u = o != null ? Of(o.Type) : null; return u != null ? u.SceneView(t, o) : t; }
        /// <summary>UnitArt.Icon / UnitArt.VehiclePortrait.</summary>
        public static Texture2D Art(string kind, string type, Team team) { var u = Of(type); return u != null ? u.Art(kind, type, team) : null; }
        /// <summary>OzBadgeArt.FaceFor.</summary>
        public static string FaceUnit(string unit) { var u = Of(unit); return u != null ? u.FaceUnit(unit) : unit; }
        /// <summary>TeamPaint.PanelFor.</summary>
        public static bool NoRoofPanel(string key, Material m) { foreach (var u in All) if (u.NoRoofPanel(key, m)) return true; return false; }
        /// <summary>A view drawn as a v2 model (any two-model unit).</summary>
        public static bool IsV2(UnitView v) { var u = v && v.Obj != null ? Of(v.Obj.Type) : null; return u != null && u.IsV2(v.transform); }
        /// <summary>RoundHatch.UseModelled: the v2 hatch frame node of a hatch spec's prefix ("tank_" -> the light tank v2's), or null.</summary>
        public static string FrameNodeFor(string prefix)
        {
            if (prefix == null) return null;
            foreach (var u in All) if (u.HatchPrefix == prefix) return u.FrameNode;
            return null;
        }
        /// <summary>VehicleLights / UnitBlinkers: the data entry of this view: "&lt;type&gt;@v2" for a v2 view when the data has one, else the type.</summary>
        public static string LightsKey(UnitView v, System.Func<string, bool> has)
        {
            if (!v || v.Obj == null) return null;
            var u = Of(v.Obj.Type);
            if (u != null && has != null && has(u.LightsKey) && u.IsV2(v.transform)) return u.LightsKey;
            return v.Obj.Type;
        }

        // ---------------------------------------------------------------- the testing overlay (P)
        /// <summary>The tile's name ("LIGHT v1" / "LIGHT v2"), null for other tiles.</summary>
        public static string TileLabel(string tile)
        {
            foreach (var u in All) { int v = u.TileVersion(tile); if (v != 0) return u.Label + " v" + v; }
            return null;
        }
        /// <summary>The tile's picture, null for other tiles.</summary>
        public static Texture TileArt(string tile, Team team)
        {
            foreach (var u in All) if (u.TileVersion(tile) != 0) return u.TileArt(tile, team);
            return null;
        }
        /// <summary>The drop: a two-model tile becomes its sim type.</summary>
        public static void FromTile(ref string type)
        {
            foreach (var u in All) if (u.TileVersion(type) != 0) { type = u.Type; return; }
        }
        /// <summary>The drop: the unit made from a two-model tile is drawn as the tile's model whatever the switch says.</summary>
        public static void PinTile(ZObject o, string tile)
        {
            foreach (var u in All) { int v = u.TileVersion(tile); if (v != 0) { u.Pin(o, v); return; } }
        }
    }
}
