using UnityEngine;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// The MLRS's two models (1.2 v2 units). The unit stays type "missile_launcher" in the rules (stats, the 8-rocket salvo, sounds, voices,
    /// the computer's play, the minimap): only the drawn model and its pictures change.
    ///   v1 = the original model: Prefabs/Z_missile_launcher, its materials and pictures, untouched.
    ///   v2 = the SAME model with its own texture, the v2 road wheels and rolling belts: Resources/OpenZ/MlrsV2/Z_missile_launcher_v2, a
    ///        copy of Prefabs/Z_missile_launcher (every node, mesh, part, neck, launcher, rocket tube, wreck piece and component of v1; the
    ///        editor menu OpenZ/MLRS v2/1 makes it again) with v1's toy material swapped for Materials/Units/mlrs_v2b_toy (v1's settings; its
    ///        albedo Models/Legacy/Toy/mlrs_v2b_toy.png = v1's atlas a little more detailed and worn, the dark metal a military dark grey;
    ///        its team texels exactly v1's), both belts one continuous tread belt each along v1's link loop on <see cref="BeltMaterial"/>
    ///        (it rolls: View/VehicleMotion.Tread.cs), the hull without its side skirts inside the belt loops, a dark side plate in each loop,
    ///        the v2 model's road wheels (TankWheel_1 .. 10, spun by VehicleMotion, planted with the belts) and the empty marker node
    ///        <see cref="FrameNode"/>; its pictures in the same folder (the unit card: v1's avatar) and ProductionBadge/x1..x4/face_missile_launcher_v2_*.
    /// So the launcher's turn, pitch and limits, the rockets from v1's tubes, the reload, the salvo, the launcher kick, the hull rock (once a
    /// salvo, along and across), the planted tracks, boarding, the hatch, the lamps (the "missile_launcher@v2" entries = v1's), damage,
    /// death, the track break, break-apart and wreck are v1's. The first v2 model's own values (<see cref="PitchMinV2"/>, <see cref="PitchMaxV2"/>,
    /// <see cref="KickBackV2"/>, <see cref="KickRollV2"/>) serve only a view that has that model's launcher box (<see cref="BoxModel"/>):
    /// its files stay in the project, unused.
    /// ONE switch: <see cref="Version"/>. 2 = the new model everywhere (units, the MLRS placed in a level at the start, unit cards, build
    /// menu, badges, Unit editor), 1 = back to the original everywhere. The testing overlay (P) places either model whatever the switch says.
    /// </summary>
    public static class OzMlrs
    {
        /// <summary>THE switch: 2 = MLRS v2 (the new model), 1 = v1 (the original model). One line to go back.</summary>
        public const int Version = 2;

        public const string Type = "missile_launcher", V1Key = "Z_missile_launcher", V2Key = "Z_missile_launcher_v2", Dir = "OpenZ/MlrsV2/";
        /// <summary>A node only the v2 model has: an empty marker under its root (it marks a v2 view; the MLRS has no RoundHatch, so no hatch prefix).</summary>
        public const string FrameNode = "mlrs_v2b_Look";
        /// <summary>The first v2 model's launcher box: a view with it takes that model's launcher limits, kick sizes and spacing measure.</summary>
        public const string BoxNode = "MLRS_v2_box";
        /// <summary>The testing overlay's two MLRS tiles (their drop spawns type "missile_launcher" drawn as that model).</summary>
        public const string TileV1 = "missile_launcher@v1", TileV2 = "missile_launcher@v2";
        /// <summary>v2's belt material: its own slightly dark grey tread picture along the belt, scrolled on screen with the ground speed
        /// (View/VehicleMotion.Tread.cs).</summary>
        public const string BeltMaterial = "mlrs_v2b_track";

        /// <summary>True for a view of the first v2 model (it has <see cref="BoxNode"/>): its own launcher limits, kick sizes and spacing measure.
        /// The v2 drawn now is v1's model: false, so it takes v1's values everywhere.</summary>
        public static bool BoxModel(Transform root)
        {
            if (!root) return false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == BoxNode) return true;
            return false;
        }

        /// <summary>v2's launcher elevation (deg about its drawn rest). The box is modelled raised 11.35 deg; item 184 rests it at v1's tilt
        /// (<see cref="OzMlrsLauncher.RestRaiseV2"/> more: 31.92 deg), so both limits move down by the same and every pose the box takes over
        /// the hull is the one measured before. About the MODELLED rest: v1's -20 would put the box's front into the hull and the fenders; the
        /// model step measured the box, its rockets and the red block / brackets / antennas clear over -7..+57 deg. On top of the aim come
        /// UnitAim's shot kick (8 deg x its envelope's 1.133 peak = 9.07 deg) and the launcher kick (MlrsSideKick: 5 deg x its 1.2 cap = 6 deg),
        /// so the aim stops at 57 - 15.07 = 41.5 deg over the modelled rest (its tubes at most 68.4 deg over the ground) and its lowest aim is 0
        /// there (with the kick's undershoot -1.5: the bigger launcher's box stays clear of the hull). About the drawn rest: -20.57 .. +20.93.</summary>
        public const float PitchMinV2 = 0f - OzMlrsLauncher.RestRaiseV2, PitchMaxV2 = 41.5f - OzMlrsLauncher.RestRaiseV2;   // items 151b / 184: the limits of k93 kept over the ground, the rest raised to v1's tilt (View/OzMlrs.Launcher.cs)
        /// <summary>v2's launcher kick per rocket (MlrsSideKick): no slide back (v1 0.22 m: the box is hinged at its rear-top, 7 cm in front of
        /// the red block, so a back slide would push it 0.19 m into the block) and a 2.5 deg roll toward the firing side (v1 6 deg: the 2.7 m
        /// wide box would dip its rear corners into the side decks); the tip-up (MlrsSideKick.UpDeg 5 deg about the hinge) as v1.</summary>
        public const float KickBackV2 = 0f, KickRollV2 = 2.5f;

        /// <summary>The MLRS's two models (View/OzV2Units.cs).</summary>
        public static readonly OzUnitModels Models = new OzUnitModels(Type, V1Key, V2Key, Dir, FrameNode, null, "missile_launcher", "missile_launcher", "MLRS",
                                                                     "mlrs_v2_", "MLRS_rack", () => Version);

        /// <summary>UnitView.Bind's spacing radius ("no touching"): v2's rear plate reaches 0.155 m past its tracks, which would widen the
        /// radius from v1's 1.95 m to 2.02 m. v2 measures in its own frame along its tracks (as long as v1's: 4.92 m) and across its whole
        /// width (antennas left out), so the radius is v1's whatever way it faces; every other unit keeps the radius it was given.</summary>
        public static float ViewRadius(UnitView v, ZObject o, float radius)
        {
            if (!v || o == null || o.Type != Type || !BoxModel(v.transform)) return radius;                     // the first v2 model only; v1's model keeps its radius
            var root = v.transform; float along = 0f, across = 0f; bool any = false;
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (!r || !mf.sharedMesh || mf.name.Contains("Antenna")) continue;
                bool belt = mf.name.StartsWith("MLRS_RegularTread", System.StringComparison.Ordinal);
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = root.InverseTransformPoint(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
                    across = Mathf.Max(across, Mathf.Abs(c.x)); if (belt) along = Mathf.Max(along, Mathf.Abs(c.z));
                    any = true;
                }
            }
            if (!any || along <= 0f) return radius;
            float s = Mathf.Abs(root.lossyScale.x);
            return Mathf.Clamp(Mathf.Max(along, across) * s * 0.8f, 1.95f, 3f);
        }
    }
}
