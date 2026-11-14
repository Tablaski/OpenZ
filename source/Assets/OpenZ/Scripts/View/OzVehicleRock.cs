using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// with easing so there is no conflict with other moves and recoils). View only: nothing of the rules is read or written, no sim random.
    /// A ground vehicle (tanks, jeep, APC, MLRS; a gun on wheels; never a robot, a building or a fort's tower gun) that EvilZod's pellets land on rocks away from the
    /// shot on its hull node: the suspension node that carries hull and turret (belts, road wheels and side plates sit beside it and stay planted); a gun on wheels
    /// rocks its turning top, the carriage stays. A damped spring: about PeakDeg at the top, one or two small swings back, settled in about 0.7 s.
    /// Its own small turn on top of what this frame's view already wrote on that node (motion squat / lean / bob, firing kick, aim): a frame where nobody re-posed
    /// the node starts again from the pose this wrote over, so it never adds up. Several hits add, clamped to PeakDeg. Asleep (disabled) when settled.
    /// Order 990: after every view script that poses a vehicle (UnitView 60, UnitAim 62, HeavyRecoil 64, BoardingView 70, FortShockwave 900, MissileCannonSalvo 950).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(990)]
    public sealed class OzVehicleRock : MonoBehaviour
    {
        public static bool On = true;
        public static float PeakDeg = 4f, Hz = 2.3f, Damp = 0.3f, ShotGap = 0.25f;
        /// <summary>Checks: shots that rocked a vehicle; the largest rock drawn so far (deg).</summary>
        public static int Kicks; public static float MaxDeg;

        UnitView view; Transform node; Quaternion baseRot, wroteRot; bool wrote, found;
        Vector2 x, v; float lastKick = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Kicks = 0; MaxDeg = 0f; }

        /// <summary>OzEvilZodFx.Land: one of his pellets landed on object id, flying along dir.</summary>
        public static void Hit(int id, Vector3 dir)
        {
            var g = ZGame.I; if (!On || !g || g.World == null) return;
            var o = g.World.GetAny(id);
            if (o == null || !(o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon && !o.IsTowerGun)) return;
            if (!g.Views.TryGetValue(id, out var view) || !view || view.Dying) return;
            var r = view.GetComponent<OzVehicleRock>(); if (!r) { r = view.gameObject.AddComponent<OzVehicleRock>(); r.view = view; }
            r.Kick(dir);
        }

        public static Transform HullNode(Transform root, bool gun)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "TankSuspension" || n == "Suspension" || n == "MLRSSuspension" || n == "Chassis" || n == "BuggyChassis") return t;
                if (gun && n.EndsWith("TurretYaw")) return t;
            }
            return null;
        }

        void Kick(Vector3 dir)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) return;
            if (!found) { found = true; node = HullNode(transform, view && view.Obj != null && view.Obj.Kind == ZKind.Cannon); }
            if (!node || Time.time - lastKick < ShotGap) return;                                       // one rock a shot (his pellets land over a few frames)
            lastKick = Time.time; Kicks++;
            dir.Normalize();
            float w = Omega, s = Mathf.Sqrt(1f - Damp * Damp);
            float v0 = PeakDeg * w / Mathf.Exp(-Damp / s * Mathf.Atan2(s, Damp));                    // the speed that tops out at PeakDeg from rest
            v += new Vector2(dir.x, dir.z) * v0;
            if (v.magnitude > v0) v = v.normalized * v0;
            enabled = true;
        }

        static float Omega => 2f * Mathf.PI * Hz;

        void LateUpdate()
        {
            if (!node) { enabled = false; return; }
            var lr = node.localRotation;
            if (wrote && Same(lr, wroteRot)) lr = baseRot;                                             // nobody re-posed it this frame: start from the pose this wrote over
            baseRot = lr;
            if (!On || !view || view.Dying) { x = v = Vector2.zero; }
            else
            {
                float dt = Time.deltaTime, w = Omega; int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 8); float h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    v += (-w * w * x - 2f * Damp * w * v) * h; x += v * h;
                    float m = x.magnitude;
                    if (m > PeakDeg) { var u = x / m; x = u * PeakDeg; float out1 = Vector2.Dot(v, u); if (out1 > 0f) v -= u * out1; }   // several hits: never past the peak
                }
            }
            if (x.sqrMagnitude < 1e-4f && v.sqrMagnitude < 2.5e-3f)
            {
                x = v = Vector2.zero;
                if (wrote) node.localRotation = baseRot;
                wrote = false; enabled = false; return;
            }
            float deg = x.magnitude; if (deg > MaxDeg) MaxDeg = deg;
            var lean = new Vector3(x.x, 0f, x.y) / Mathf.Max(deg, 1e-6f);                            // the top goes this way: away from him
            node.localRotation = baseRot;
            node.rotation = Quaternion.AngleAxis(deg, Vector3.Cross(Vector3.up, lean)) * node.rotation;
            wroteRot = node.localRotation; wrote = true;
        }

        static bool Same(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

        /// <summary>Checks: this vehicle's rock now (deg) and whether a hull node was found.</summary>
        public float NowDeg => x.magnitude;
        public bool HasNode => node;
    }
}
