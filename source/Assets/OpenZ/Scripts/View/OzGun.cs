using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The gun's two models (1.2 v2 units, owner item 140: "Gun v2 update (again just model look)"). The unit stays type "gun" in the rules
    /// (the defensive gun cannon: placement, footprint, capture, crew, range, damage, fire rate, sounds, voices, the computer's play, the
    /// minimap): only the drawn model and its pictures change.
    ///   v1 = the original model: Prefabs/Z_gun, its materials and pictures, untouched.
    ///   v2 = the new model: Resources/OpenZ/GunV2/Z_gun_v2 (made by the editor menu OpenZ/Gun v2/1 from Models/Legacy/gun_v2.glb), its
    ///        pictures in the same folder (icon_gun_v2_*, portrait_gun_v2_*) and ProductionBadge/x1..x4/face_gun_v2_*.
    /// v2 carries every node name v1's code reads (CannonRoot as "Model" at v1's place, CannonPedestal_Surface, CannonTurretYaw with
    /// CannonTurretYaw_Surface, CannonGunnerSeat, CannonBarrelPitch, CannonBarrelRecoil, CannonMuzzle, the exits, the label anchor), so the
    /// turret's turn, the barrel's aim, kick and slide, the gunner (BoardingView: he walks in behind and sits on CannonGunnerSeat, turning with
    /// the turret), the snipe and capture, damage effects, the turret toss at the death with the chair breaking off (GunChairDebris), the base
    /// breaking apart and the wreck run on it as on v1 (View/OzV2Units.cs does the rest).
    /// What differs on v2 (measured on the model, v2_units/gun/V1_PARITY.md): its elevation stops at <see cref="PitchMaxV2"/> (its octagonal
    /// collar would sweep over the box's front bar above 33 deg), its gunner's chair sits behind the red block (v2's red block stands where
    /// perch: v2's gunner sits in view over the red block), and its spacing radius keeps robots clear of the chair's sweep
    /// (<see cref="ViewRadius"/>). Only the red block and the tags' red stripes take the team colour; empty = the v2 family's dark grey (item 136).
    /// ONE switch: <see cref="Version"/>. 2 = the new model everywhere (units, the guns placed in a level at the start, the guns on the fort
    /// towers, unit cards, build menu, badges, Unit editor), 1 = back to the original everywhere. The testing overlay (P) places either model
    /// whatever the switch says.
    /// </summary>
    public static class OzGun
    {
        /// <summary>THE switch: 2 = gun v2 (the new model), 1 = v1 (the original model). One line to go back.</summary>
        public const int Version = 2;   // k92: back on 2 (the chair gone, the gunner in the box hatch-up, the towers measured)

        public const string Type = "gun", V1Key = "Z_gun", V2Key = "Z_gun_v2", Dir = "OpenZ/GunV2/";
        /// <summary>A node only the v2 model has (an empty marker the build menu puts under its Model): it marks a v2 view.</summary>
        public const string FrameNode = "gun_v2_Frame";
        /// <summary>The testing overlay's two gun tiles (their drop spawns type "gun" drawn as that model).</summary>
        public const string TileV1 = "gun@v1", TileV2 = "gun@v2";
        /// <summary>v2's barrel elevation stop (deg about its rest; v1 40): above about 33 deg (28 + the shot's 3.4 deg climb + its overshoot)
        /// gun's shell flies its own arc from the muzzle; at the game's ranges it needs about 6 deg, and a barrel at its stop still fires.</summary>
        public const float PitchMaxV2 = 28f;
        /// <summary>The view spacing (UnitView's "no touching" push) keeps a robot's body this far inside its own radius clear of the turret's
        /// sweep (a robot's view radius 0.8 m against a body half-width of about 0.4 m).</summary>
        public const float SweepKeepClear = 0.4f;

        /// <summary>The gun's two models (View/OzV2Units.cs). Pictures: icon_gun_v2_&lt;team&gt; (the production card), portrait_gun_v2_&lt;team&gt;
        /// (the crewed unit card), face_gun_v2_&lt;team&gt; (the badge); v1's own pictures (icon_gun_*, portrait_gun_*, face_gun_*) stay as they
        /// are. v1's team roof panel sits on CannonTurretYaw_Surface: v2 takes the team colour on its red block and tag stripes only.</summary>
        public static readonly OzUnitModels Models = new OzUnitModels(Type, V1Key, V2Key, Dir, FrameNode, null, "gun", "gun", "GUN",
                                                                     "gun_v2_", "CannonTurretYaw_Surface", () => Version);

        /// <summary>This sim object is a gun cannon (both models).</summary>
        public static bool Is(ZObject o) => o != null && o.Kind == ZKind.Cannon && o.Type == Type;
        /// <summary>
        /// octagonal muzzle (the collar would sit 0.1 m in front of it and move the muzzle socket off its bore): none on v2.</summary>
        public static bool NoNose(GameObject unit) => unit && Models.IsV2(unit.transform);

        /// <summary>BoardingView (a gun on a fort tower): v1's tower gunner gets the raised perch over its tall housing (GunnerPerch); v2's gunner
        /// sits in view over its red block on v2's own chair (seen from the RTS camera at any heading), so no perch is built on v2.</summary>
        public static bool NoPerch(UnitView v) => v && Models.IsV2(v.transform);

        static readonly System.Collections.Generic.Dictionary<Mesh, float> sweeps = new System.Collections.Generic.Dictionary<Mesh, float>();
        /// <summary>UnitView.Bind's spacing radius ("no touching"): v2's chair behind the red block sweeps a 2.62 m circle round the turn axis as
        /// the turret turns (v1's chair 1.6 m, inside its radius), and the turn axis sits 0.41 m behind the view's place. v2's radius is its own
        /// (UnitView's formula on its renderers, as v1's) and at least the chair's sweep from the view's place less <see cref="SweepKeepClear"/>,
        /// so a robot standing by the gun is never drawn inside the chair or the seated gunner at any turret heading (at most 3 m, UnitView's
        /// cap). View only: the sim's footprint (2 x 2 tiles), placement and paths are the rule's, unchanged; v1 keeps its radius.</summary>
        public static float ViewRadius(UnitView v, ZObject o, float radius)
        {
            if (!v || !Is(o) || !Models.IsV2(v.transform)) return radius;
            float keep = KeepClear(v.transform);
            return keep > 0f ? Mathf.Clamp(Mathf.Max(radius, keep), 1.5f, 3f) : radius;
        }
        /// <summary>The radius (m) that keeps a robot's body clear of the turret's sweep round its turn axis: the farthest point of the turret
        /// mesh from the axis + the axis' distance from the view's place - <see cref="SweepKeepClear"/>; 0 when the nodes are missing.</summary>
        public static float KeepClear(Transform root)
        {
            if (!root) return 0f;
            Transform yaw = null; MeshFilter turret = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!yaw && t.name == "CannonTurretYaw") yaw = t;
                else if (!turret && t.name == "CannonTurretYaw_Surface") turret = t.GetComponent<MeshFilter>();
            }
            if (!yaw || !turret || !turret.sharedMesh) return 0f;
            if (!sweeps.TryGetValue(turret.sharedMesh, out float sweep)) sweeps[turret.sharedMesh] = sweep = Sweep(turret.sharedMesh, turret.transform, yaw);   // once per turret mesh
            var off = root.InverseTransformPoint(yaw.position); off.y = 0f;
            return sweep * Mathf.Abs(yaw.lossyScale.x) + off.magnitude * Mathf.Abs(root.lossyScale.x) - SweepKeepClear;   // metres, as UnitView's radius
        }
        /// <summary>The turret mesh's farthest point from the turn axis (in the turret's own units): its vertices when readable, else its box.</summary>
        static float Sweep(Mesh m, Transform mesh, Transform yaw)
        {
            float r = 0f;
            if (m.isReadable)
            {
                foreach (var p in m.vertices) { var l = yaw.InverseTransformPoint(mesh.TransformPoint(p)); r = Mathf.Max(r, new Vector2(l.x, l.z).magnitude); }
                return r;
            }
            var b = m.bounds;
            for (int i = 0; i < 8; i++)
            {
                var l = yaw.InverseTransformPoint(mesh.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
                r = Mathf.Max(r, new Vector2(l.x, l.z).magnitude);
            }
            return r;
        }
    }
}
