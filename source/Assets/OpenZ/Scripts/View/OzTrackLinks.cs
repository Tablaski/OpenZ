using System;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Mammoth's and the Bizon's tracks are REAL links that move round their loop, not a scrolling texture. View only.
    /// <para>Data (baked on the prefab by the editor menu OpenZ/1.5/Big Tanks/1 from the model's {tank}_track_paths.json and its nodes; nothing is
    /// read at run time): per side the closed pitch line of the loop (<see cref="Side.path"/>, the links' parent's space, ordered so that a forward
    /// drive moves the top run forward), every link's rest place on it (arc + its pose in the line's frame there), the corner wheels (sprocket:
    /// its pitch radius; idler / guides: their rim) and the road wheels (their rim), and the half gauge between the two loops.</para>
    /// <para>Motion (VehicleMotion.Step calls <see cref="Step"/> after its own wheels): a side's belt has run d = travel + yaw x half gauge x side
    /// (the real half gauge: turning on the spot runs the two sides opposite ways at the right speed); link i sits at (its rest arc + d) mod L on
    /// the line, turned to the line's direction there, so the bottom run stands still on the ground (speed matched by construction), the top
    /// run moves forward at twice the ground speed and the links wrap the sprocket and the idler; every wheel turns by d / its radius. Links
    /// no link of the tank is on any camera or while the belt stands (the place is a pure function of d, so nothing is lost).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OzTrackLinks : MonoBehaviour
    {
        [Serializable]
        public sealed class Side
        {
            public string name;
            [Tooltip("the side root (Track_Left / Track_Right): planted on the ground by UnitView.GroundGear")] public Transform root;
            [Tooltip("the links' parent: the line's space")] public Transform space;
            [Tooltip("+1 the tank's left (+X), -1 its right")] public float sign = 1f;
            public Vector3[] path = new Vector3[0];
            public float[] arc = new float[0];
            public float length;
            public Transform[] links = new Transform[0];
            public float[] linkArc = new float[0];
            public Vector3[] linkOffset = new Vector3[0];
            public Quaternion[] linkRot = new Quaternion[0];
            [Tooltip("corner wheels (their axle empties) and road wheels: each turns by d / its radius about its local X")] public Transform[] gear = new Transform[0];
            public Quaternion[] gearRest = new Quaternion[0];
            public float[] gearRadius = new float[0];
            [Tooltip("the loop's box in the side root's space at rest (the planted gear's footprint)")] public Vector3 boxLo, boxHi;
            [NonSerialized] public float lastD = float.NaN;
            [NonSerialized] public Renderer[] probes;
        }

        public Side[] sides = new Side[0];
        [Tooltip("link pitch (m) and half the distance between the two loops (m, the vehicle root's space)")] public float pitch, halfGauge;
        [Tooltip("where the data came from (the menu's note)")] public string source = "";

        /// <summary>Proof: link poses written, Step calls skipped (off screen or standing), the last side's belt travel (m).</summary>
        public static int Writes, Skips;
        public float LastLeftD { get; private set; }
        public float LastRightD { get; private set; }
        /// <summary>Links on both sides.</summary>
        public int LinkCount { get { int n = 0; foreach (var s in sides) if (s != null && s.links != null) n += s.links.Length; return n; }}
        /// <summary>The menu's data is whole (every side has a closed line and links).</summary>
        public bool Valid
        {
            get
            {
                if (sides == null || sides.Length == 0) return false;
                foreach (var s in sides)
                    if (s == null || !s.space || s.path == null || s.path.Length < 8 || s.arc == null || s.arc.Length != s.path.Length || s.length <= 0.1f || s.links == null || s.links.Length == 0
                        || s.linkArc.Length != s.links.Length || s.linkOffset.Length != s.links.Length || s.linkRot.Length != s.links.Length || s.gearRest.Length != s.gear.Length || s.gearRadius.Length != s.gear.Length) return false;
                return true;
            }
        }

        /// <summary>Writes every side for this drive. travel = the vehicle root's metres driven (VehicleMotion), yawTravel = its turn (rad),
        /// root = the vehicle root. Returns false when nothing was written.</summary>
        public bool Step(float travel, float yawTravel, Transform root, bool force = false)
        {
            if (sides == null) return false;
            bool any = false, seen = force || Visible();
            float rs = root ? Mathf.Abs(root.lossyScale.z) : 1f; if (rs < 1e-5f) rs = 1f;
            for (int k = 0; k < sides.Length; k++)
            {
                var s = sides[k]; if (s == null || !s.space || s.length <= 0.1f) continue;
                float ss = Mathf.Abs(s.space.lossyScale.z) / rs; if (ss < 1e-5f) ss = 1f;
                float d = (travel + yawTravel * halfGauge * s.sign) / ss;
                if (s.sign > 0f) LastLeftD = d; else LastRightD = d;
                if (!seen || (!force && !float.IsNaN(s.lastD) && Mathf.Abs(d - s.lastD) < 1e-5f)) { Skips++; continue; }
                s.lastD = d;
                for (int i = 0; i < s.links.Length; i++)
                {
                    var t = s.links[i]; if (!t) continue;
                    Frame(s, s.linkArc[i] + d, out var p, out var q);
                    t.SetLocalPositionAndRotation(p + q * s.linkOffset[i], q * s.linkRot[i]);
                }
                for (int i = 0; i < s.gear.Length; i++)
                {
                    var g = s.gear[i]; if (!g) continue;
                    float r = Mathf.Max(0.05f, s.gearRadius[i]);
                    g.localRotation = s.gearRest[i] * Quaternion.AngleAxis(-d / r * Mathf.Rad2Deg, Vector3.right);   // front = -z, as VehicleMotion's wheels
                }
                Writes += s.links.Length; any = true;
            }
            return any;
        }

        /// <summary>Puts the links back on their rest places (d = 0): the editor menu, a dead tank's wreck.</summary>
        public void Rest() { foreach (var s in sides) if (s != null) s.lastD = float.NaN; Step(0f, 0f, transform, true); }

        bool Visible()
        {
            foreach (var s in sides)
            {
                if (s == null || s.links == null || s.links.Length == 0) continue;
                if (s.probes == null)
                {
                    int n = Mathf.Min(6, s.links.Length); s.probes = new Renderer[n];
                    for (int j = 0; j < n; j++) { var t = s.links[j * s.links.Length / n]; s.probes[j] = t ? t.GetComponent<Renderer>() : null; }
                }
                foreach (var r in s.probes) if (r && r.isVisible) return true;
            }
            return false;
        }

        /// <summary>The line's frame at arc a (any value: wrapped): the point and the turn whose forward is the line's direction there and whose
        /// up is that direction turned +90 deg about the axle (the side's +X); the menu measured every link's rest pose in this same frame.</summary>
        public static void Frame(Side s, float a, out Vector3 p, out Quaternion q)
        {
            p = PointAt(s, a);
            var f = PointAt(s, a + 0.02f) - PointAt(s, a - 0.02f);
            if (f.sqrMagnitude < 1e-10f) f = Vector3.forward;
            f.Normalize();
            var up = Vector3.Cross(Vector3.right, f); if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
            q = Quaternion.LookRotation(f, up);
        }

        /// <summary>The point at arc a of the closed line (wrapped into [0, length)).</summary>
        public static Vector3 PointAt(Side s, float a)
        {
            int n = s.path.Length;
            a %= s.length; if (a < 0f) a += s.length;
            if (a >= s.arc[n - 1])
            {   // the closing segment, last point -> first point
                float seg = s.length - s.arc[n - 1];
                return Vector3.Lerp(s.path[n - 1], s.path[0], seg > 1e-6f ? (a - s.arc[n - 1]) / seg : 0f);
            }
            int lo = 0, hi = n - 1;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (s.arc[m] <= a) lo = m; else hi = m; }
            float l = s.arc[hi] - s.arc[lo];
            return Vector3.Lerp(s.path[lo], s.path[hi], l > 1e-6f ? (a - s.arc[lo]) / l : 0f);
        }

        /// <summary>The arc of the line's point nearest to p (the menu: a link's rest place).</summary>
        public static float NearestArc(Side s, Vector3 p)
        {
            float best = float.MaxValue, at = 0f; int n = s.path.Length;
            for (int i = 0; i < n; i++)
            {
                var a = s.path[i]; var b = s.path[(i + 1) % n]; var ab = b - a;
                float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; float a0 = s.arc[i], a1 = i + 1 < n ? s.arc[i + 1] : s.length; at = a0 + (a1 - a0) * t; }
            }
            return at;
        }

        /// <summary>The menu: the cumulative arc of a closed line (arc[0] = 0) and its whole length.</summary>
        public static float[] Arcs(Vector3[] path, out float length)
        {
            var arc = new float[path.Length]; length = 0f;
            for (int i = 1; i < path.Length; i++) arc[i] = arc[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            length = path.Length > 1 ? arc[path.Length - 1] + Vector3.Distance(path[path.Length - 1], path[0]) : 0f;
            return arc;
        }
    }
}
