// OpenZ (GPL-3.0) - version 1.5 kit fps_shuttle_badges: the first-person view (View/OzFpsView.cs) on the playable shuttle. View only: the ship is
// flown with its own move and stop orders (SEND_WAYPOINTS, the rules of Sim/ZWorld.Shuttle.cs and Sim/ZWorld.ShuttleFlight.cs), nothing new in the rules.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 kit fps_shuttle_badges, part 1: the first-person view on the shuttle.
    /// <list type="bullet">
    /// <item>Who: an own crewed shuttle (its pilot sits in the closed cockpit: Drivers.Count &gt; 0) may be looked out of. Before, OzFpsView.Sees
    /// refused every shuttle (the quiet ui_disabled). <see cref="On"/> off = refused again, exactly as before.</item>
    /// <item>The eye: slightly in front of the ship's nose, never in it. At each entry the ship's own drawn parts are measured in the ship
    /// root's own space (each mesh's box through its matrix; opaque and alpha-tested parts only, no beam, halo or other see-through part, no
    /// shadow-only part, nothing farther than <see cref="PartRadiusM"/> from the ship's middle): the nose points along the root's forward turned by
    /// OzPlayableShuttle.noseYaw (the lit window and lamp strip side), its farthest box corner along it is the reach, and the eye sits
    /// <see cref="NoseClearM"/> m past it on the nose's line, at the height and side of the model's own front marker (<see cref="FrontMarker"/>:
    /// the front of the dome at the window's height; the reach is never nearer than that marker), else at the middle height of the front
    /// <see cref="FrontBandM"/> m of the boxes. The eye
    /// rides with the ship's drawn pose (its bank, pitch, bob and crash pose; OzPlayableShuttle poses it in Update 200, the view reads it in
    /// LateUpdate 31900), and is not eased (the ship's pose is eased already; an eased eye would trail into the nose at speed). Never under the
    /// ground (0.9 m over it, as the view keeps every eye). The look is the view's own mouse look; at the entry it looks along the nose.</item>
    /// <item>Flying it: W A S D send the ship's own move order toward where the player looks, as an RTS click on the ground would: the ship takes
    /// off (the ramp shut first), climbs, turns toward the point, cruises, and so on, by its own rules. The point is far enough ahead that the
    /// ship never plans its stop while a key is held (its braking distance from top speed, ZWorld.ShuttleBrakeDistance, plus <see cref="LeadSec"/>
    /// s of top speed), and the next order goes when the ship is within its braking distance plus <see cref="ReissueSec"/> s of it, or the look
    /// turned more than OzFpsView.TurnDeg (the view's own turn rule). Letting go of the keys sends the ship's own STOP: it brakes and lands where
    /// the braking ends (or hovers there when it cannot land, as for an RTS stop). A key during the descent: up again and on (its rules). Shift
    /// changes nothing for the ship (its speed is its own). A left click means nothing: the ship has no weapon.</item>
    /// <item>The ship's own model is held off in the view's pictures as every viewed unit's is (and OzShuttleCamFade dithers a ship this near the
    /// camera away anyway): from the nose nothing of it is in the picture. Its sounds (spool-up, take-off, hover and jet loops, landing thump,
    /// the strafe whoosh, the voices) are the ship's own, heard as the viewed unit's own (OzFpsView.Own).</item>
    /// </list>
    /// Hook lines in View/OzFpsView.cs (each false, or the old value, for every other unit): Sees, Begin (the first look), BuildGun (no gun
    /// copy), Fire (no attack), EyePoint (the nose eye), PlaceCamera (not eased), Walk (the point ahead, the next order).
    /// </summary>
    public static class OzFpsShuttleView
    {
        /// <summary>The shuttle may be looked out of (off: Alt+F on it is refused, as before this kit).</summary>
        public static bool On = true;

        // ------------------------------------------------------------------ the numbers
        /// <summary>How far past the ship's farthest forward part box the eye sits (m), along the nose's line.</summary>
        public const float NoseClearM = 0.8f;
        /// <summary>The front band whose boxes give the eye's height and side place (m back from the reach); the lowest eye height over the root (m).</summary>
        public const float FrontBandM = 1.5f, EyeMinUpM = 1.0f;
        /// <summary>A part counts only with its box middle within this of the ship's middle (m; OzShuttleCamFade.ShipRadiusM, the open ramp
        /// included) and its box no longer than PartMaxM (m): a long beam or a mark on the ground far under the ship is never the nose.</summary>
        public const float PartRadiusM = 8f, PartMaxM = 16f;
        /// <summary>Render queue from which a material is see-through (beams, halos, glows): such a part is not measured.</summary>
        public const int SeeThroughQueue = 2501;
        /// <summary>The move order's point: the ship's braking distance from top speed plus this much of top speed (s) ahead; the next order once
        /// the ship is within its braking distance plus ReissueSec of top speed (s) of the point.</summary>
        public const float LeadSec = 1.5f, ReissueSec = 0.8f;
        /// <summary>The fewest px ahead (the view's own step, 3 tiles).</summary>
        public const int MinLeadPx = 48;

        // ------------------------------------------------------------------ for the checks
        public static int Entries, Measures, Fallbacks, AheadOrders;
        /// <summary>The last measure: parts measured, the reach (m along the nose from the root), the eye in the root's space, the farthest part.</summary>
        public static int LastParts;
        public static float LastReachM;
        public static Vector3 LastEyeLocal, LastNoseLocal;
        public static string LastNosePart = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = true; Entries = Measures = Fallbacks = AheadOrders = FrontMarkerUsed = 0; LastParts = 0; LastReachM = 0f; LastEyeLocal = LastNoseLocal = Vector3.zero; LastNosePart = "";
            measuredFor = null; measuredFrame = -1;
        }

        // ------------------------------------------------------------------ the hooks
        /// <summary>OzFpsView.Sees: an own crewed shuttle can be looked out of (the ownership is the view's own test).</summary>
        public static bool Sees(ZObject u) => On && u != null && u.IsShuttle && !u.IsFortDefender && !u.Destroyed && u.Drivers.Count > 0;

        /// <summary>The viewed unit is a shuttle (every shuttle hook reads this; false for every other unit).</summary>
        public static bool Flies(ZObject u) => u != null && u.IsShuttle;

        /// <summary>OzFpsView.Begin: the first look along the ship's nose (yaw in degrees; other units: the view's own yaw).</summary>
        public static float NoseYaw(ZObject u, UnitView v, float yaw)
        {
            if (!Flies(u) || !v) return yaw;
            var f = v.transform.TransformDirection(NoseLocal(u)); f.y = 0f;
            Entries++;
            return f.sqrMagnitude > 1e-6f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : yaw;
        }

        /// <summary>OzFpsView.EyePoint: the eye in front of the ship's nose (world), true for a shuttle.</summary>
        public static bool Eye(ZObject u, UnitView v, out Vector3 p)
        {
            p = Vector3.zero;
            if (!Flies(u) || !v) return false;
            Measure(u, v);
            p = v.transform.TransformPoint(eyeLocal);
            var g = ZGame.I;
            if (g) { float gy = g.GroundY(p.x, p.z); if (p.y < gy + 0.9f) p.y = gy + 0.9f; }
            return true;
        }

        /// <summary>OzFpsView.Walk: the point the move order flies to (px), the ship's braking distance plus LeadSec of top speed ahead along f
        /// (the wanted way on the ground, world), inside the map (mx, my: the map in px) as the view keeps its points.</summary>
        public static Vector2Int Ahead(ZObject u, Vector3 f, int mx, int my)
        {
            double v = System.Math.Max(8.0, u.RealMoveSpeed);
            float lead = Mathf.Max(MinLeadPx, (float)(ZWorld.ShuttleBrakeDistance(v, 0.0, v) + v * LeadSec));
            AheadOrders++;
            return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(u.CenterX + f.x * lead), 12, Mathf.Max(12, mx - 12)),
                                  Mathf.Clamp(Mathf.RoundToInt(u.CenterY - f.z * lead), 12, Mathf.Max(12, my - 12)));
        }

        /// <summary>OzFpsView.Walk: the ship is near enough the point it was sent to that the next point goes now (its braking distance from top
        /// speed plus ReissueSec of top speed): it never plans its stop while a key is held.</summary>
        public static bool Near(ZObject u, Vector2Int sentTo)
        {
            double v = System.Math.Max(8.0, u.RealMoveSpeed);
            double reissue = ZWorld.ShuttleBrakeDistance(v, 0.0, v) + v * ReissueSec;
            double dx = u.CenterX - sentTo.x, dy = u.CenterY - sentTo.y;
            return dx * dx + dy * dy <= reissue * reissue;
        }

        // ------------------------------------------------------------------ the measure of the nose
        static UnitView measuredFor; static int measuredFrame = -1;
        static Vector3 eyeLocal, noseLocal = Vector3.forward;
        static readonly List<Renderer> rbuf = new List<Renderer>();
        static readonly List<Material> mbuf = new List<Material>();
        static readonly List<Vector3> corners = new List<Vector3>();

        /// <summary>The ship's nose in its root's own space: the root's forward turned by OzPlayableShuttle.noseYaw (the dome, the lit window and
        /// lamp strip side; the nose searchlight's pivot OzShuttleNightLights.NosePivot sits at the root's +z).</summary>
        static Vector3 NoseLocal(ZObject u)
        {
            var ps = OzPlayableShuttle.Of(u);
            return Quaternion.Euler(0f, -(ps ? ps.noseYaw : 0f), 0f) * Vector3.forward;
        }

        /// <summary>Once per entry (OzFpsView.EnteredFrame) and view: the nose, the reach and the eye in the root's own space.</summary>
        static void Measure(ZObject u, UnitView v)
        {
            if (measuredFor == v && measuredFrame == OzFpsView.EnteredFrame) return;
            measuredFor = v; measuredFrame = OzFpsView.EnteredFrame; Measures++;
            noseLocal = NoseLocal(u);
            float sc = Mathf.Max(1e-4f, v.transform.lossyScale.y);                                             // the numbers are world m; the boxes are in the root's own units
            var side = Vector3.Cross(Vector3.up, noseLocal).normalized;
            int parts = Boxes(v.transform, boxes, names);
            float reach = float.MinValue; string far = "";
            for (int i = 0; i < parts; i++)
            {
                corners.Clear(); Corners(boxes[i], Matrix4x4.identity);
                for (int k = 0; k < 8; k++) { float d = Vector3.Dot(corners[k], noseLocal); if (d > reach) { reach = d; far = names[i]; } }
            }
            LastParts = parts; LastNoseLocal = noseLocal; LastNosePart = far;
            if (parts == 0)
            {   // nothing measured (not the drawn ship as it is known): the nose searchlight's pivot (OzShuttleNightLights.NosePivot) and 3 m past it
                Fallbacks++;
                float r0 = OzShuttleNightLights.NosePivot.z + 3f / sc; LastReachM = r0 * sc;
                eyeLocal = noseLocal * (r0 + NoseClearM / sc) + Vector3.up * Mathf.Max(EyeMinUpM / sc, OzShuttleNightLights.NosePivot.y);
                LastEyeLocal = eyeLocal; LastNosePart = "(none measured)";
                return;
            }
            float yLo = float.MaxValue, yHi = float.MinValue, sLo = float.MaxValue, sHi = float.MinValue;
            for (int i = 0; i < parts; i++)
            {
                corners.Clear(); Corners(boxes[i], Matrix4x4.identity);
                for (int k = 0; k < 8; k++)
                {
                    var c = corners[k]; if (Vector3.Dot(c, noseLocal) < reach - FrontBandM / sc) continue;            // the front band only
                    yLo = Mathf.Min(yLo, c.y); yHi = Mathf.Max(yHi, c.y);
                    float s = Vector3.Dot(c, side); sLo = Mathf.Min(sLo, s); sHi = Mathf.Max(sHi, s);
                }
            }
            float up = Mathf.Max(EyeMinUpM / sc, 0.5f * (yLo + yHi)), lat = 0.5f * (sLo + sHi);
            var mk = Named(v.transform, FrontMarker);                                                           // the model's own front marker (the window's height on the nose's line): its height and side, never nearer than the boxes' reach
            if (mk)
            {
                var fl = v.transform.InverseTransformPoint(mk.position);
                up = Mathf.Max(EyeMinUpM / sc, fl.y); lat = Vector3.Dot(fl, side);
                float fr = Vector3.Dot(fl, noseLocal); if (fr > reach) { reach = fr; LastNosePart = FrontMarker; }
                FrontMarkerUsed++;
            }
            LastReachM = reach * sc;
            eyeLocal = noseLocal * (reach + NoseClearM / sc) + side * lat + Vector3.up * up;
            LastEyeLocal = eyeLocal;
        }

        /// <summary>The shuttle model's front marker (L16_ShuttleUnit / the scene ship: MK_Front, under Model, at the root's (0, 3.26, 4.33) m: the
        /// front of the dome at the window's height).</summary>
        public const string FrontMarker = "MK_Front";
        public static int FrontMarkerUsed;

        static Transform Named(Transform root, string n)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != root && t.name == n) return t;
            return null;
        }

        static readonly List<Bounds> boxes = new List<Bounds>();
        static readonly List<string> names = new List<string>();

        /// <summary>The ship's measured parts as boxes in its root's own space (each mesh's own box through its matrix; a skinned part's local box
        /// through its root bone): drawn solid parts (<see cref="Part"/>) whose box middle is within PartRadiusM of the ship's middle and whose box
        /// is no longer than PartMaxM. Returns how many.</summary>
        static int Boxes(Transform root, List<Bounds> into, List<string> named)
        {
            into.Clear(); named.Clear();
            var w2l = root.worldToLocalMatrix;
            var middle = new Vector3(0f, OzShuttleCamFade.ShipMiddleUpM, 0f);
            root.GetComponentsInChildren(true, rbuf);
            for (int i = 0; i < rbuf.Count; i++)
            {
                var r = rbuf[i];
                if (!Part(r)) continue;
                corners.Clear();
                if (r is SkinnedMeshRenderer smr)
                {
                    var basis = smr.rootBone ? smr.rootBone : smr.transform;
                    Corners(smr.localBounds, w2l * basis.localToWorldMatrix);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; if (!mesh) continue;
                    Corners(mesh.bounds, w2l * r.transform.localToWorldMatrix);
                }
                var lb = new Bounds(corners[0], Vector3.zero); for (int k = 1; k < corners.Count; k++) lb.Encapsulate(corners[k]);
                if ((lb.center - middle).magnitude > PartRadiusM || lb.size.magnitude > PartMaxM) continue;
                into.Add(lb); named.Add(r.name);
            }
            return into.Count;
        }

        /// <summary>A drawn solid part of the ship: a mesh or skinned mesh that is on and in the hierarchy, not shadow-only, with no see-through
        /// material (beams, halos, glows) and not named as a beam or a halo.</summary>
        static bool Part(Renderer r)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) return false;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return false;
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return false;
            var n = r.name; if (n.Contains("Beam") || n.Contains("Halo")) return false;
            r.GetSharedMaterials(mbuf);
            for (int i = 0; i < mbuf.Count; i++) if (mbuf[i] && mbuf[i].renderQueue >= SeeThroughQueue) return false;
            return true;
        }

        static void Corners(Bounds b, Matrix4x4 m)
        {
            for (int i = 0; i < 8; i++)
                corners.Add(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        }

        static readonly List<Bounds> checkBoxes = new List<Bounds>();
        static readonly List<string> checkNames = new List<string>();

        /// <summary>For the checks: the nearest distance (m) from the world point p to the ship's measured part boxes, in the ship root's own space
        /// as the ship stands now (the same parts and boxes the eye is placed from; +inf: none).</summary>
        public static float NearestPartM(UnitView v, Vector3 p)
        {
            if (!v) return float.PositiveInfinity;
            var q = v.transform.InverseTransformPoint(p);
            float scale = Mathf.Max(1e-4f, v.transform.lossyScale.y);
            int n = Boxes(v.transform, checkBoxes, checkNames);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++) best = Mathf.Min(best, checkBoxes[i].SqrDistance(q));
            return float.IsInfinity(best) ? best : Mathf.Sqrt(best) * scale;
        }

        /// <summary>For the checks: the eye's place along the nose past the reach (m; NoseClearM by construction), in the root's space now.</summary>
        public static float EyePastReachM(UnitView v, Vector3 eyeWorld) => v ? Vector3.Dot(v.transform.InverseTransformPoint(eyeWorld), LastNoseLocal) * Mathf.Max(1e-4f, v.transform.lossyScale.y) - LastReachM : -1f;

        public static string Status() =>
            $"shuttle view {(On ? "on" : "off")}: entries {Entries}, measures {Measures} (fallbacks {Fallbacks}), last {LastParts} parts, reach {LastReachM:0.00} m along the nose ({LastNosePart}), " +
            $"eye (root space) {LastEyeLocal.x:0.00}, {LastEyeLocal.y:0.00}, {LastEyeLocal.z:0.00}, {NoseClearM:0.0} m past the reach (front marker used {FrontMarkerUsed}); move points worked out {AheadOrders} (every Walk frame with a key held; the orders sent are OzFpsView.MoveOrders)";
    }
}
