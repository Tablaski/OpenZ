#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit fort_self_destruct: the Play check of the fort wall panel (View/OzFortSelfDestructPanels.cs), one level per run (menu OpenZ/V1.5/Fort:
    /// Wall Panel Check (Play, a level playing)). Not in the release exe. For every fort of the level (both teams):
    /// <list type="number">
    /// <item>P1 built on the fort's FactoryView rig, no shadow, drawn; P2 placed at the measured point (1 mm); P3 its box (prefab-root metres) inside
    /// the free wall of the last bay (off the wall, under the strip light, between the portal post and the first rib);</item>
    /// <item>P4 clips into nothing: every triangle of the fort's gate_* meshes (hall, light strips, doors, lenses) and FortBase tested against the
    /// panel's box grown 3 mm (exact triangle / box test) - 0 touching (a mesh that is not readable is reported, not counted);</item>
    /// <item>P5 follows the hall: the hall renderer switched off for one frame - the panel is off; on again - the panel is on;</item>
    /// <item>P6 seen inside: four eyes in the hallway (walking in, close, straight at it, the owner's frame down the hall): the picture with the panel
    /// against the same picture without it differs in at least <see cref="MinSeen"/> pixels (not for the frame down the hall: reported);</item>
    /// <item>P7 never seen from the RTS camera: the door leaves held off (= fully open), the game camera at 45 m from the door, tilted 55 / 45 / 40 / 35
    /// degrees below the horizon, turned -60 .. 60 degrees round the door's axis: the panel adds no pixel over the noise of the same picture drawn
    /// twice without it (tilt 25, the free look's low end, reported only: the off-Unity ray test sees the panel's lower part through the
    /// open gate from about 32 degrees down); P8 the RTS camera as it stands now.</item>
    /// </list>
    /// Pictures (the panel shown) and fort_panel_report.txt in <see cref="Folder"/>. The game camera is borrowed for each picture (drawn twice so its
    /// history is settled) and given back exactly; the door leaves and the hall renderer are given back as they were.
    /// </summary>
    public sealed class OzFortSelfDestructCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.5 fort wall panel]";
        public static string Report = "not run", Folder;
        public static bool Running;
        public const int W = 1280, H = 720, MinSeen = 300, Thr = 12;
        public const float RtsDist = 45f, RtsFov = 32f, EyeFov = 60f, EyeNear = 0.05f;
        public static readonly float[] Tilts = { 55f, 45f, 40f, 35f }, InfoTilts = { 25f }, Yaws = { -60f, -30f, 0f, 30f, 60f };

        RenderTexture rt; Texture2D read; Camera cam;
        Vector3 camPos; Quaternion camRot; float camFov, camNear;
        readonly StringBuilder sb = new StringBuilder();
        int pass, fail;

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_FortWallPanelCheck").AddComponent<OzFortSelfDestructCheck>();
        }

        IEnumerator Start()
        {
            Running = true;
            try { yield return Main(); }
            finally { Running = false; if (rt) { rt.Release(); Destroy(rt); } if (read) Destroy(read); Destroy(gameObject); }
        }

        void Row(string id, bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine($"  {(ok ? "PASS" : "FAIL")} {id} {what}"); }
        void Info(string id, string what) => sb.AppendLine($"  INFO {id} {what}");

        IEnumerator Main()
        {
            yield return null; yield return new WaitForSecondsRealtime(1.2f);                                   // the panels are built twice a second
            var g = ZGame.I; var rts = FindFirstObjectByType<RtsCamera>();
            cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            if (!g || g.World == null || !cam || !FactoryViews.I) { Report = Tag + " FAIL: no level playing (ZGame, the game camera, FactoryViews)"; Debug.LogWarning(Report); yield break; }
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "OZ_FortPanelCheck" }; rt.Create();
            read = new Texture2D(W, H, TextureFormat.RGBA32, false);
            sb.AppendLine($"{Tag} level {g.level}, {System.DateTime.Now:yyyy-MM-dd HH:mm}, panels built {OzFortSelfDestructPanels.Built}, skipped {OzFortSelfDestructPanels.Skipped} {OzFortSelfDestructPanels.LastSkip}");
            var list = new List<ZObject>();                                                                     // taken first: units come and go while the check waits frames
            foreach (var o in g.World.Objects) if (o != null && o.IsFort) list.Add(o);
            int forts = 0;
            foreach (var o in list)
            {
                var fv = FactoryViews.I.For(o.Id); if (!fv) continue;
                forts++;
                sb.AppendLine($" {o.Type} #{o.Id} ({o.Owner}){(FortFallAtGameOver.Fallen(o) ? " DOWN" : "")}");
                yield return Fort(g, fv, o);
            }
            Row("P0", forts > 0, $"forts with a rig: {forts}");
            yield return RtsNow(g);
            Report = $"{Tag} {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} ({g.level})";
            sb.Insert(0, Report + "\n");
            if (!string.IsNullOrEmpty(Folder)) { Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder, "fort_panel_report.txt"), sb + "\n"); }
            Debug.Log(sb.ToString());
        }

        IEnumerator Fort(ZGame g, FactoryView fv, ZObject o)
        {
            var p = OzFortSelfDestructPanels.PanelOf(fv);
            var mr = p ? p.GetComponent<MeshRenderer>() : null; var mf = p ? p.GetComponent<MeshFilter>() : null;
            Row("P1", p && mr && mf && mf.sharedMesh && mr.shadowCastingMode == ShadowCastingMode.Off && mr.enabled && p.parent == fv.transform,
                p && mr ? $"on the rig {fv.name}, shadows {mr.shadowCastingMode}, drawn {mr.enabled}" : "no panel: " + OzFortSelfDestructPanels.LastSkip);
            if (!p || !mr || !mf || !mf.sharedMesh) yield break;
            var b = fv.Building; var gate = fv.Gate;
            var minT = Find(b, "gate_mark_min"); Vector3 min = minT ? b.InverseTransformPoint(minT.position) : OzFortSelfDestructPanels.KitMin;
            float x0 = min.x, floor = min.y, zEnd = gate ? gate.InPoint.z : OzFortSelfDestructPanels.KitInZ;
            var want = new Vector3(x0, floor + OzFortSelfDestructPanels.CentreUp, zEnd + OzFortSelfDestructPanels.CentreBack);
            float off = (b.InverseTransformPoint(p.position) - want).magnitude;
            Row("P2", off < 0.001f, $"at prefab-root ({want.x:0.000}, {want.y:0.000}, {want.z:0.000}), off by {off * 1000f:0.0} mm");
            // P3: the panel's box in prefab-root metres
            var mb = mf.sharedMesh.bounds; Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var r = b.InverseTransformPoint(p.TransformPoint(c)); lo = Vector3.Min(lo, r); hi = Vector3.Max(hi, r);
            }
            bool inBox = lo.x >= x0 + 0.003f && hi.x <= x0 + 0.13f && lo.y >= floor + 1.05f && hi.y <= floor + 2.06f /* 1.5 kit self_destruct_v2: the bigger panel */ && lo.z >= zEnd + 0.06f && hi.z <= zEnd + 0.51f;
            Row("P3", inBox, $"box x {lo.x:0.000}..{hi.x:0.000}, y {lo.y:0.000}..{hi.y:0.000}, z {lo.z:0.000}..{hi.z:0.000} inside the free wall (x {x0 + 0.003f:0.000}..{x0 + 0.13f:0.000}, y {floor + 1.05f:0.000}..{floor + 2.06f:0.000}, z {zEnd + 0.06f:0.000}..{zEnd + 0.51f:0.000})");
            // P4: exact triangle / box test against every gate_* mesh and the body, in the panel's own space
            Vector3 bc = mb.center, bh = mb.extents + Vector3.one * 0.003f;
            int touch = 0, tested = 0; var notReadable = new List<string>(); var touched = new List<string>();
            foreach (var m in b.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!m.sharedMesh || !(m.name.StartsWith("gate_") || m.name == "FortBase")) continue;
                if (!m.sharedMesh.isReadable) { notReadable.Add(m.name); continue; }
                var M = p.worldToLocalMatrix * m.transform.localToWorldMatrix; var v = m.sharedMesh.vertices; int hits = 0;
                for (int s = 0; s < m.sharedMesh.subMeshCount; s++)
                {
                    var t = m.sharedMesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        tested++;
                        if (TriBox(bc, bh, M.MultiplyPoint3x4(v[t[i]]), M.MultiplyPoint3x4(v[t[i + 1]]), M.MultiplyPoint3x4(v[t[i + 2]]))) hits++;
                    }
                }
                if (hits > 0) { touch += hits; touched.Add($"{m.name} {hits}"); }
            }
            Row("P4", touch == 0, $"{tested} triangles of the gate kit and the body tested: {touch} touch the panel's box (+3 mm){(touched.Count > 0 ? ": " + string.Join(", ", touched) : "")}"
                + (notReadable.Count > 0 ? $"; not readable here (the build's own numbers in the README stand for them): {string.Join(", ", notReadable)}" : ""));
            // P5: it follows the hall
            Renderer hall = null; var hallT = Find(b, "gate_hall"); if (hallT) hall = hallT.GetComponent<Renderer>();
            if (hall)
            {
                bool was = hall.enabled; hall.enabled = false; yield return null;
                bool offOk = !mr.enabled; hall.enabled = was; yield return null;
                Row("P5", offOk && mr.enabled == was, $"the hall off: panel {(offOk ? "off" : "STILL ON")}; the hall back: panel {(mr.enabled ? "on" : "off")}");
            }
            else Row("P5", false, "no gate_hall renderer");
            // P6: seen from eyes inside the hallway (prefab-root metres, the hall's floor and portal)
            var target = b.TransformPoint(want + new Vector3(0.05f, 0f, 0f));
            var shots = new (string name, Vector3 eye, Vector3 look, bool must)[]
            {
                ("walk_in", new Vector3(0.35f, floor + 2.00f, zEnd + 2.60f), target, true),                       // a robot's eye in the view: 1.5 .. 2.1 m over the ground
                ("close", new Vector3(-0.45f, floor + 1.95f, zEnd + 1.35f), target, true),
                ("facing", new Vector3(x0 + 1.40f, floor + 1.80f, want.z), target, true),
                ("down_the_hall", new Vector3(0f, floor + 2.00f, zEnd + 2.60f), b.TransformPoint(new Vector3(0f, floor + 2.00f, zEnd - 0.5f)), false),
            };
            foreach (var s in shots)
            {
                var eye = b.TransformPoint(s.eye);
                var rot = Quaternion.LookRotation(s.look - eye, Vector3.up);
                yield return Signal(mr, eye, rot, EyeFov, EyeNear, Pic(g, o, s.name)); int seen = sigValue;
                if (s.must) Row("P6", seen >= MinSeen, $"{s.name}: the panel covers {seen} pixels of {W}x{H} (at least {MinSeen})");
                else Info("P6", $"{s.name}: the panel covers {seen} pixels");
                yield return null;
            }
            // P7: never from the RTS camera, the doors open
            var doors = new List<(Renderer r, bool off)>();
            foreach (var r in b.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("gate_door_")) { doors.Add((r, r.forceRenderingOff)); r.forceRenderingOff = true; }
            try
            {
                var door = b.TransformPoint(new Vector3(0f, floor + 1.5f, zEnd + 3.30f));                     // the frame's front, mid height
                var outward = b.TransformDirection(Vector3.forward); outward.y = 0f; outward.Normalize();
                foreach (var tilt in Tilts) yield return RtsRow(g, o, mr, door, outward, tilt, true);
                foreach (var tilt in InfoTilts) yield return RtsRow(g, o, mr, door, outward, tilt, false);
            }
            finally { foreach (var d in doors) if (d.r) d.r.forceRenderingOff = d.off; }
        }

        IEnumerator RtsRow(ZGame g, ZObject o, MeshRenderer mr, Vector3 door, Vector3 outward, float tilt, bool must)
        {
            int worst = 0, worstNoise = 0; string at = "";
            foreach (var yaw in Yaws)
            {
                var dir = Quaternion.AngleAxis(yaw, Vector3.up) * outward;
                var eye = door + (dir * Mathf.Cos(tilt * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(tilt * Mathf.Deg2Rad)) * RtsDist;
                var rot = Quaternion.LookRotation(door - eye, Vector3.up);
                string pic = tilt == 55f && yaw == 0f ? Pic(g, o, "rts_tilt55") : null;
                yield return Signal(mr, eye, rot, RtsFov, 0.3f, pic); int sig = sigValue, noise = sigNoise;
                if (sig - noise > worst - worstNoise || at == "") { worst = sig; worstNoise = noise; at = $"turn {yaw:+0;-0;0}"; }
            }
            if (must) Row("P7", worst <= worstNoise, $"tilt {tilt:0}, doors open: the panel adds {worst} pixels against a noise of {worstNoise} (worst at {at})");
            else Info("P7", $"tilt {tilt:0} (free look only), doors open: the panel adds {worst} pixels against a noise of {worstNoise} (worst at {at})");
        }

        IEnumerator RtsNow(ZGame g)
        {
            yield return null;
            var t = cam.transform; int total = 0, totalNoise = 0;
            var pose = (t.position, t.rotation, cam.fieldOfView, cam.nearClipPlane);                              // the RTS camera as it stands now (frames pass while it is measured)
            foreach (var o in new List<ZObject>(g.World.Objects))
            {
                if (o == null || !o.IsFort) continue;
                var p = OzFortSelfDestructPanels.PanelOf(FactoryViews.I.For(o.Id)); var mr = p ? p.GetComponent<MeshRenderer>() : null;
                if (!mr) continue;
                yield return Signal(mr, pose.position, pose.rotation, pose.fieldOfView, pose.nearClipPlane, Pic(g, o, "rts_as_it_is")); total += sigValue; totalNoise += sigNoise;
            }
            Row("P8", total <= totalNoise, $"the RTS camera as it stands now: the panels add {total} pixels against a noise of {totalNoise}");
        }

        string Pic(ZGame g, ZObject o, string what) => string.IsNullOrEmpty(Folder) ? null : Path.Combine(Folder, $"fort_panel_{g.level}_{o.Owner}_{o.Type}_{what}.png");

        /// <summary>The game camera at a pose, drawn without the panel (A), with it (B), without it again (C): returns the pixels B differs from C,
        /// noise = A against C. Writes B to `png` when given. The camera's pose, field of view, near plane and aspect are given back.</summary>
        // Unity pass 1.5: with the GPU Resident Drawer on (PC_RPAsset: Instanced Drawing) a renderer switched on or off is drawn so only from the
        // next frame on, and the panel's own LateUpdate sets mr.enabled every frame: the panel is hidden by forceRenderingOff with one frame
        // between the switch and each picture (measured: 0 pixels changed without the frame, about 2250 of 320 x 180 with it).
        int sigValue, sigNoise;
        IEnumerator Signal(MeshRenderer mr, Vector3 pos, Quaternion rot, float fov, float near, string png)
        {
            bool was = mr.forceRenderingOff;
            try
            {
                mr.forceRenderingOff = true; yield return null; var a = ShotAt(pos, rot, fov, near);
                mr.forceRenderingOff = false; yield return null; var bb = ShotAt(pos, rot, fov, near);
                if (png != null) { read.SetPixels32(bb); read.Apply(false); Directory.CreateDirectory(Path.GetDirectoryName(png)); File.WriteAllBytes(png, read.EncodeToPNG()); }
                mr.forceRenderingOff = true; yield return null; var c = ShotAt(pos, rot, fov, near);
                var box = PanelBox(mr, pos, rot, fov, near);                                                    // frames pass between the pictures: count only where the panel can be
                sigNoise = Diff(a, c, box);
                sigValue = Diff(bb, c, box);
            }
            finally { mr.forceRenderingOff = was; }
        }

        /// <summary>The panel's screen box (its bounds' 8 corners, 3 px margin) in the W x H picture from that pose; empty when it is not in front of the camera.</summary>
        RectInt PanelBox(MeshRenderer mr, Vector3 pos, Quaternion rot, float fov, float near)
        {
            var t = cam.transform; var p0 = t.position; var r0 = t.rotation; float f0 = cam.fieldOfView, n0 = cam.nearClipPlane;
            try
            {
                t.SetPositionAndRotation(pos, rot); cam.fieldOfView = fov; cam.nearClipPlane = near; cam.aspect = W / (float)H;
                var bo = mr.bounds; float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue; bool front = false;
                for (int i = 0; i < 8; i++)
                {
                    var c = bo.center + Vector3.Scale(bo.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var v = cam.WorldToViewportPoint(c);
                    if (v.z <= 0f) { x0 = 0; y0 = 0; x1 = W; y1 = H; front = true; break; }                       // a corner behind the eye: the whole picture
                    front = true; x0 = Mathf.Min(x0, v.x * W); x1 = Mathf.Max(x1, v.x * W); y0 = Mathf.Min(y0, v.y * H); y1 = Mathf.Max(y1, v.y * H);
                }
                if (!front) return new RectInt(0, 0, 0, 0);
                int ix0 = Mathf.Clamp(Mathf.FloorToInt(x0) - 3, 0, W), iy0 = Mathf.Clamp(Mathf.FloorToInt(y0) - 3, 0, H);
                int ix1 = Mathf.Clamp(Mathf.CeilToInt(x1) + 3, 0, W), iy1 = Mathf.Clamp(Mathf.CeilToInt(y1) + 3, 0, H);
                return new RectInt(ix0, iy0, Mathf.Max(0, ix1 - ix0), Mathf.Max(0, iy1 - iy0));
            }
            finally { t.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.nearClipPlane = n0; cam.ResetAspect(); }
        }

        static int Diff(Color32[] a, Color32[] b, RectInt box)
        {
            int n = 0;
            for (int y = box.yMin; y < box.yMax; y++)
                for (int x = box.xMin, i = y * W + box.xMin; x < box.xMax; x++, i++)
                    if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > Thr) n++;
            return n;
        }

        /// <summary>One picture from the game camera at a pose; the camera's pose, field of view, near plane and aspect are given back at once.</summary>
        Color32[] ShotAt(Vector3 pos, Quaternion rot, float fov, float near)
        {
            var t = cam.transform; camPos = t.position; camRot = t.rotation; camFov = cam.fieldOfView; camNear = cam.nearClipPlane;
            try { t.SetPositionAndRotation(pos, rot); cam.fieldOfView = fov; cam.nearClipPlane = near; cam.aspect = W / (float)H; return Shot(); }
            finally { t.SetPositionAndRotation(camPos, camRot); cam.fieldOfView = camFov; cam.nearClipPlane = camNear; cam.ResetAspect(); }
        }

        /// <summary>Drawn twice (the second with the history of the first: no motion blur from the jump), read back.</summary>
        Color32[] Shot()
        {
            for (int k = 0; k < 2; k++)
            {
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old; }
            }
            var prev = RenderTexture.active; RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, W, H), 0, 0, false); read.Apply(false);
            RenderTexture.active = prev;
            return read.GetPixels32();
        }

        static int Diff(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > Thr) n++;
            return n;
        }

        static Transform Find(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var f = Find(c, n); if (f) return f; }
            return null;
        }

        /// <summary>Triangle against an axis-aligned box (centre c, half size h): the separating axis test (the box's 3 axes, the triangle's normal,
        /// the 9 edge crosses). True = they touch.</summary>
        static bool TriBox(Vector3 c, Vector3 h, Vector3 a, Vector3 b, Vector3 d)
        {
            Vector3 v0 = a - c, v1 = b - c, v2 = d - c, e0 = v1 - v0, e1 = v2 - v1, e2 = v0 - v2;
            if (Sep(Vector3.right, v0, v1, v2, h) || Sep(Vector3.up, v0, v1, v2, h) || Sep(Vector3.forward, v0, v1, v2, h)) return false;
            if (Sep(Vector3.Cross(e0, e1), v0, v1, v2, h)) return false;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward }; var edges = new[] { e0, e1, e2 };
            foreach (var u in axes) foreach (var e in edges) if (Sep(Vector3.Cross(u, e), v0, v1, v2, h)) return false;
            return true;
        }

        static bool Sep(Vector3 L, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 h)
        {
            if (L.sqrMagnitude < 1e-12f) return false;
            float p0 = Vector3.Dot(L, v0), p1 = Vector3.Dot(L, v1), p2 = Vector3.Dot(L, v2);
            float r = h.x * Mathf.Abs(L.x) + h.y * Mathf.Abs(L.y) + h.z * Mathf.Abs(L.z);
            return Mathf.Min(p0, Mathf.Min(p1, p2)) > r || Mathf.Max(p0, Mathf.Max(p1, p2)) < -r;
        }
    }
}
#endif
