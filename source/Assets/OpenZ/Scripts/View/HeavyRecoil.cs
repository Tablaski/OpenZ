using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The heavy
    /// tank's shot, view only, the same for model v1 and v2 (the switch stays equal), on top of the barrels' own slides and muzzle climb
    /// (UnitAim) and the suspension's per-barrel side rock (UnitView.HeavyKick.cs):
    ///  * HEAD: the turret kicks straight back along its barrels and rocks back (its front up) about a point behind its turn axis (the
    ///    rear lip stays on the deck), then eases home with one small overshoot (a damped spring: peak at about 0.07 s, home by about
    ///    0.25 s). Size: v1 slides 0.135 m (the shove it had), v2 0.30 m: v1's gun mantlet is modelled in its barrel meshes, which slide
    ///    0.74 m, so its head front visibly kicks; v2's mantlet is part of the turret mesh, so the turret itself must kick to read the
    ///    same from the game camera (0.30 m = about 7 px at the RTS distance, was 3 px).
    ///  * BODY: the whole hull rocks back away from the shot (the side it fires toward lifts, about the far track end, so nothing dips
    ///    into the ground) and slides back 0.12 m on its tracks, then settles with an eased-out wobble: one smaller opposite rock and a
    ///    faint third (a damped spring: peak 2 deg at about 0.09 s, -0.45 deg at about 0.3 s, under 0.1 deg about 0.6 s after the shot).
    ///    Before, the hull jolted 0.18 m and nodded 5.7 deg TOWARD the shot (its front down), then rocked +-2 to 5 deg for over a second.
    /// Springs, not envelopes: each shot adds an impulse, so the second barrel of the pair (0.36 s later) adds to the motion where it is
    /// (the old envelope started again on every barrel and was cut off at 1 s). The springs are solved exactly per frame (no blow-up at a
    /// low frame rate). The sim position is never touched: UnitView places the unit from the sim every frame, this only moves the drawn
    /// model on top. The numbers are measured frame by frame in the kit's tools/recoil_model.py.
    /// Switch: <see cref="Enabled"/> (off = the recoil as before). The body's two sizes keep RecoilFeel's options gains.
    /// </summary>
    [DefaultExecutionOrder(64)]   // after UnitAim (62) turned the head, before BoardingView (70) seats the hatch commander on it
    public partial class HeavyRecoil : MonoBehaviour                                                    // 1.2 v2 units: + View/HeavyRecoil.Types.cs (the light tank too)
    {
        /// <summary>The switch: off = the heavy tank's recoil as before (the envelope shove, jolt and nod in UnitView.ApplyRecoil).</summary>
        public static bool Enabled = true;
        /// <summary>The body: slide back (m) and rock back (deg) at the peak, before RecoilFeel's gains (x1.25 / x1.15: 0.12 m, 2.0 deg).</summary>
        public static float BodyPushM = 0.096f, BodyRockDeg = 1.75f;
        /// <summary>The body spring: natural frequency (Hz) and damping ratio (0.42: each rock about 23 % of the one before).</summary>
        public static float BodyHz = 2.3f, BodyDamp = 0.42f;
        /// <summary>The head: slide back (m) per model and rock back (deg, its front up) at the peak.</summary>
        public static float HeadSlideV1 = 0.135f, HeadSlideV2 = 0.30f, HeadRockDeg = 2.5f;
        /// <summary>The head spring: natural frequency (Hz) and damping ratio (0.6: one overshoot of about 9 %).</summary>
        public static float HeadHz = 2.9f, HeadDamp = 0.6f;
        /// <summary>Statistics (the Play checks): shots kicked, the head writes skipped because nobody re-posed the head since the last write.</summary>
        public static int Kicks, HeadReused;

        struct Spring
        {
            public float x, v;
            /// <summary>Exact damped oscillator step (any dt, no blow-up).</summary>
            public void Step(float omega, float zeta, float dt)
            {
                if (dt <= 0f || (x == 0f && v == 0f)) return;
                float a = zeta * omega, wd = omega * Mathf.Sqrt(1f - zeta * zeta);
                float e = Mathf.Exp(-a * dt), c = Mathf.Cos(wd * dt), s = Mathf.Sin(wd * dt);
                float A = x, B = (v + a * x) / wd;
                x = e * (A * c + B * s);
                v = e * (c * (B * wd - a * A) - s * (A * wd + a * B));
                if (Mathf.Abs(x) < 1e-5f && Mathf.Abs(v) < 1e-4f) { x = 0f; v = 0f; }
            }
        }
        /// <summary>The start speed an impulse needs for its first peak to be `peak` (spring at rest).</summary>
        static float Impulse(float peak, float omega, float zeta)
        {
            float r = Mathf.Sqrt(1f - zeta * zeta);
            return peak * omega / Mathf.Exp(-(zeta / r) * Mathf.Atan2(r, zeta));
        }
        static float Omega(float hz) => 2f * Mathf.PI * Mathf.Max(0.1f, hz);

        UnitView view; ZObject obj; Transform yaw, yawParent;
        Spring pushX, pushZ, rockX, rockZ, headS, headQ;
        float halfLen = 2.6f, halfWid = 2f, headSlide, pivotBack;
        Vector3 fireDir = Vector3.forward;
        bool wrote; Vector3 basePos, writtenPos; Quaternion baseRot, writtenRot;

        /// <summary>The model the view is drawn as (2 = v2), read at the bind.</summary>
        public int Model { get; private set; }
        /// <summary>What is drawn now: the body's slide (m) and its rock toward the last shot (deg, + = the side it fired toward is up);
        /// the head's slide back (m) and rock (deg, + = its front up).</summary>
        public float BodyPushNow { get; private set; }
        public float BodyRockNow { get; private set; }
        public float HeadSlideNow => headS.x;
        public float HeadRockNow => headQ.x;
        /// <summary>The horizontal direction of the last shot (world).</summary>
        public Vector3 LastFireDir => fireDir;

        /// <summary>UnitView (the first frame of a heavy tank): the component, its head (the turret's turn joint UnitView found) and its sizes.</summary>
        public static HeavyRecoil Attach(UnitView v, ZObject o, Transform head)
        {
            if (!v || o == null) return null;
            var h = v.GetComponent<HeavyRecoil>(); if (!h) h = v.gameObject.AddComponent<HeavyRecoil>();
            h.view = v; h.obj = o; h.yaw = head; h.yawParent = head ? head.parent : null; h.wrote = false;
            h.Model = ModelOf(o, v.transform);                                                          // 1.2 v2 units: the light tank too (View/HeavyRecoil.Types.cs)
            h.headSlide = HeadSlideFor(o.Type, h.Model);
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var b = mf.sharedMesh.bounds;
                if (IsHull(mf.name)) { h.halfWid = Mathf.Max(0.5f, b.extents.x); h.halfLen = Mathf.Max(0.5f, b.extents.z); }   // 1.2 v2 units: the light tank's Tank_hull too
                if (IsHead(mf.name)) h.pivotBack = Mathf.Max(0f, b.max.z) * PivotShare(o.Type, h.Model);   // the head's rear half-way (its front is -z); the light tank v2: its rear edge (View/HeavyRecoil.LightV2.cs)
            }
            return h;
        }

        /// <summary>A shot left the gun (UnitView.OnFire, the barrel that fired is known): one impulse into every spring.</summary>
        public void Kick(Vector3 dir)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize(); fireDir = dir; Kicks++;
            string t = obj != null ? obj.Type : OzHeavyTank.Type; var k = obj != null ? obj.Kind : ZKind.Vehicle;
            float wb = Omega(BodyHz), wh = Omega(HeadHz);
            float push = Impulse(BodyPushFor(t) * RecoilFeel.Hull(t, k), wb, BodyDamp), rock = Impulse(BodyRockFor(t) * RecoilFeel.HullTilt(t, k), wb, BodyDamp);   // 1.2 v2 units: the light tank's own sizes
            ShowDirs(dir, t, out var pd, out var rd);                                                   // k93, item 152: along the hull drawn bigger, across as before (View/HeavyRecoil.Show.cs)
            pushX.v -= pd.x * push; pushZ.v -= pd.z * push;                                             // the body slides away from the shot
            rockX.v += rd.x * rock; rockZ.v += rd.z * rock;                                             // the side it fires toward lifts
            headS.v += Impulse(headSlide, wh, HeadDamp);
            headQ.v += Impulse(HeadRockFor(t), wh, HeadDamp);
        }

        /// <summary>UnitView.ApplyRecoil, once per placed frame (the unit was just placed from the sim and its head put back to its rest
        /// place): the springs move on by dt, the head slides back along its own barrels (here, so everything placed on the head this
        /// frame, the hatch commander included, rides with it) and the body's slide and rock go onto the drawn model.</summary>
        public void StepAndPlace(Transform root, float dt)
        {
            float wb = Omega(BodyHz), wh = Omega(HeadHz);
            pushX.Step(wb, BodyDamp, dt); pushZ.Step(wb, BodyDamp, dt); rockX.Step(wb, BodyDamp, dt); rockZ.Step(wb, BodyDamp, dt);
            headS.Step(wh, HeadDamp, dt); headQ.Step(wh, HeadDamp, dt);
            if (yaw && yaw.parent == yawParent && headS.x != 0f)
                yaw.localPosition += (wrote ? baseRot : yaw.localRotation) * Vector3.forward * headS.x;   // back along its barrels (its front is -z)
            float px = pushX.x, pz = pushZ.x, rx = rockX.x, rz = rockZ.x;
            BodyPushNow = Mathf.Sqrt(px * px + pz * pz);
            BodyRockNow = rx * fireDir.x + rz * fireDir.z;
            if (px != 0f || pz != 0f) root.position += new Vector3(px, 0f, pz);
            float deg = Mathf.Sqrt(rx * rx + rz * rz);
            if (deg < 1e-4f) return;
            var dir = new Vector3(rx, 0f, rz) / deg;                                                    // this side goes up
            var dl = root.InverseTransformDirection(dir);
            float reach = (Mathf.Abs(dl.x) * halfWid + Mathf.Abs(dl.z) * halfLen) * 0.9f * Mathf.Abs(root.lossyScale.x);
            var pivot = root.position - dir * reach + root.up * (RollUpNow(dl) * Mathf.Abs(root.lossyScale.x));   // the far track end stays down; k112, item 151: an across rock turns about a raised axis (View/HeavyRecoil.Show.cs)
            var q = Quaternion.AngleAxis(-deg, Vector3.Cross(Vector3.up, dir));
            root.position = pivot + q * (root.position - pivot);
            root.rotation = q * root.rotation;
        }

        /// <summary>The head after UnitAim turned it this frame: its rock (its front up) about a point behind its turn axis. A frame where
        /// nobody re-placed or re-turned the head starts again from the pose this wrote over (never adds up).</summary>
        void LateUpdate()
        {
            if (!view || !yaw || view.Dying || yaw.parent != yawParent) return;
            var lp = yaw.localPosition; var lr = yaw.localRotation;
            bool samePos = wrote && lp == writtenPos, sameRot = wrote && lr == writtenRot;           // still what this wrote: nobody re-posed it
            if (samePos && sameRot) HeadReused++;
            if (samePos) lp = basePos;
            if (sameRot) lr = baseRot;
            basePos = lp; baseRot = lr;
            if (!Enabled) { headQ = default; headS = default; }                                         // switched off while kicked: the head goes back to rest
            float q = headQ.x;
            if (Mathf.Abs(q) < 1e-4f)
            {
                if (wrote) { yaw.localPosition = basePos; yaw.localRotation = baseRot; wrote = false; }
                return;
            }
            var turn = Quaternion.AngleAxis(q, Vector3.right);                                          // + q: the front (-z) up, the back down
            var pivotL = Vector3.forward * pivotBack;                                                   // behind the turn axis, in the head's own space
            yaw.localPosition = basePos + baseRot * (pivotL - turn * pivotL);
            yaw.localRotation = baseRot * turn;
            writtenPos = yaw.localPosition; writtenRot = yaw.localRotation; wrote = true;
        }
    }
}
