#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.2 run 10, editor only: the Play proof of the MLRS v2 hull body (and the heavy / light v2 plates put back).
// In a running level (Play), OzRun10HullProof.Run(): poses an MLRS v2 red, blue and empty (neutral), a heavy v2 and a light v2 (red) on
// open ground (they cannot die and do not fire), then renders each from an off-screen camera that copies the game camera (the game's
// light, post and sky): front / rear / side / the owner's 3/4 and close views of the hatch; and the see-through count of the MLRS v2:
// selected (its green ring drawn), a magenta sheet on the ground under it, 4 headings x pitch 80 / 55 / 40 + the owner framing: the
// pixels inside the outline of the hull box between the tracks (|x| 0.93 m, y 0.17-1.60 m, z -2.27..2.28 m, MLRS_hull's own frame) that
// show no part of the vehicle (the mask: the vehicle and the magenta sheet alone on their own layer, no terrain, no post; a pixel that
// is the sheet inside the outline) = the ground / ring seen through it.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    public class OzRun10HullProof : MonoBehaviour
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        public static string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/k85e_mlrs_v2_v1_retextured/proof/run10");
        public static string Report = "not run";
        public static bool Running;
        readonly StringBuilder log = new StringBuilder(), counts = new StringBuilder();
        const int MaskLayer = 31, W = 1280, H = 960;
        Camera cam; Material white, magenta; Transform rackYaw; GameObject sheetGo;

        public static string Run(int tx = 20, int ty = 46)
        {
            if (!ZGame.I || ZGame.I.World == null) return "no level";
            if (Running) return "running";
            Running = true; Report = "running";
            var go = new GameObject("OzRun10HullProof"); go.AddComponent<OzRun10HullProof>().StartCoroutine(go.GetComponent<OzRun10HullProof>().All(tx, ty));
            return "started";
        }

        void Log(string s) { log.AppendLine(s); Debug.Log("[run10 hull] " + s); }

        ZObject Pose(ZGame g, string type, int tx, int ty, Team team)
        {
            OzCheatSpawner.FreeTile(g.World, tx, ty, 3, false, out tx, out ty);
            var o = g.World.CreateObject("vehicle", type, tx * 16, ty * 16, team);
            g.SpawnUnitView(o);
            o.MaxHealth = 100000000; o.SetHealth(o.MaxHealth); o.Damage = 0; o.AttackRadius = 0; o.MoveSpeed = 0; o.RealMoveSpeed = 0;
            Log($"posed {type} {team} #{o.Id} at tile {tx},{ty}");
            return o;
        }

        static Transform Node(Component v, string name)
        {
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        IEnumerator All(int tx, int ty)
        {
            Directory.CreateDirectory(Dir);
            var g = ZGame.I;
            white = new Material(Shader.Find("Universal Render Pipeline/Unlit")); white.SetColor("_BaseColor", Color.white); white.color = Color.white;
            magenta = new Material(Shader.Find("Universal Render Pipeline/Unlit")); magenta.SetColor("_BaseColor", Color.magenta); magenta.color = Color.magenta;
            var units = new List<(ZObject o, string tag)>
            {
                (Pose(g, "missile_launcher", tx, ty, Team.Red), "MLRS_V2_RED"),
                (Pose(g, "missile_launcher", tx + 12, ty, Team.Blue), "MLRS_V2_BLUE"),
                (Pose(g, "missile_launcher", tx + 24, ty, Team.Neutral), "MLRS_V2_EMPTY"),
                (Pose(g, OzHeavyTank.Type, tx, ty + 12, Team.Red), "HEAVY_V2_RED"),
                (Pose(g, "light", tx + 12, ty + 12, Team.Red), "LIGHT_V2_RED"),
            };
            float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 3f) yield return null;   // views built, suspension settled
            // the off-screen camera jumps between poses: depth of field and motion blur off for these renders (put back at the end)
            var offFx = new List<UnityEngine.Rendering.VolumeComponent>();
            foreach (var vol in FindObjectsOfType<UnityEngine.Rendering.Volume>())
                if (vol.sharedProfile) foreach (var vc in vol.sharedProfile.components)
                    if (vc.active && (vc is DepthOfField || vc is MotionBlur)) { vc.active = false; offFx.Add(vc); }
            Log("off for the renders: " + offFx.Count + " depth of field / motion blur override(s)");
            var main = Camera.main;
            var go = new GameObject("run10_cam"); cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.enabled = false;
            var ad = go.AddComponent<UniversalAdditionalCameraData>(); var mad = main.GetComponent<UniversalAdditionalCameraData>();
            if (mad) { ad.renderPostProcessing = mad.renderPostProcessing; ad.antialiasing = mad.antialiasing; }
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 600f; cam.aspect = (float)W / H;

            foreach (var (o, tag) in units)
            {
                if (!g.Views.TryGetValue(o.Id, out var v) || !v) { Log(tag + ": NO VIEW"); continue; }
                var hull = Node(v, "MLRS_hull");
                Transform frame = hull ? hull : v.transform;
                Vector3 fwd = hull ? -hull.forward : v.transform.forward; fwd.y = 0; fwd.Normalize();             // the MLRS hull's front is its -Z
                if (!hull) fwd = ModelFront(v);
                Vector3 c = Centre(v);
                Log($"{tag}: centre {c:F2}, front {fwd:F2}, hull node {(hull ? "yes" : "no")}");
                bool mlrs = hull != null;
                rackYaw = mlrs ? Node(v, "MLRSRackYaw") : null;                                    // the rack turned to the front for the pictures (view only, this render)
                var hinge = mlrs ? Node(v, "mlrs_HatchHinge") : null;
                var shots = new List<(string n, float az, float pitch, float dist)>
                {
                    ("front", 0f, 12f, 13f), ("rear", 180f, 12f, 13f), ("side", 90f, 8f, 14f), ("ownerview", -35f, 48f, 11f),
                };
                if (mlrs) { shots.Add(("side_left", -90f, 8f, 14f)); shots.Add(("hatch_side_close", 90f, 3f, 4.5f)); shots.Add(("hatch_side_left_close", -90f, 3f, 4.5f)); shots.Add(("hatch_34_close", -35f, 24f, 4.5f)); }
                else shots.Add(("spike_34_frontright", 40f, 50f, 11f));
                foreach (var s in shots)
                {
                    var aim = s.n.StartsWith("hatch") && hinge ? hinge.position : c;
                    var tex = Render(v, aim, fwd, s.az, s.pitch, s.dist, false, null);
                    Save(tex, $"{tag}_{s.n}.png"); Destroy(tex);
                    yield return null;
                }
            }
            // the see-through count: MLRS v2 red, selected, a magenta sheet under it
            var red = units[0].o;
            if (g.Views.TryGetValue(red.Id, out var rv) && rv)
            {
                var input = FindObjectOfType<PlayerInput>();
                if (input) { input.Selection.Clear(); input.Selection.Add(red); }
                yield return null; yield return null;
                var hull = Node(rv, "MLRS_hull"); rackYaw = Node(rv, "MLRSRackYaw");
                Vector3 fwd = -hull.forward; fwd.y = 0; fwd.Normalize();
                Vector3 c = Centre(rv);
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(sheet.GetComponent<Collider>());
                sheet.name = "run10_magenta"; sheet.transform.position = new Vector3(c.x, rv.transform.position.y + 0.012f, c.z);
                sheet.transform.rotation = Quaternion.Euler(90f, 0f, 0f); sheet.transform.localScale = new Vector3(400f, 400f, 1f);
                sheet.GetComponent<MeshRenderer>().sharedMaterial = magenta; sheetGo = sheet;
                yield return null;
                counts.AppendLine("MLRS v2 see-through count (red, selected, magenta sheet on the ground): px inside the hull-box outline showing no vehicle part | magenta or ring-green px inside it");
                int total = 0, worst = 0;
                var views = new List<(string n, float az, float p)>();
                foreach (float hd in new[] { 35f, 145f, 215f, 325f }) foreach (float p in new[] { 80f, 55f, 40f }) views.Add(($"h{hd:0}_p{p:0}", hd, p));
                views.Add(("owner", -35f, 48f));
                foreach (var (n, az, p) in views)
                {
                    var tex = Render(rv, c, fwd, az, p, 11f, false, null);
                    var mask = Render(rv, c, fwd, az, p, 11f, true, null);
                    var poly = HullPoly(hull);
                    int holes = 0, coloured = 0, inside = 0;
                    var tp = tex.GetPixels32(); var mp = mask.GetPixels32();
                    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                    {
                        if (!Inside(poly, x + 0.5f, y + 0.5f)) continue;
                        inside++;
                        int i = y * W + x;
                        if (mp[i].r < 128) { holes++; var q = tp[i]; if ((q.r > 150 && q.b > 150 && q.g < 90) || (q.g > 150 && q.r < 140 && q.b < 160)) coloured++; }
                    }
                    total += holes; worst = Mathf.Max(worst, holes);
                    counts.AppendLine($"{n,-10} inside-outline px {inside,7}  see-through px {holes,5}  magenta/green {coloured,5}  poly " + string.Join(";", poly.ConvertAll(q => $"{q.x:0.0},{q.y:0.0}")));
                    Save(tex, $"count_{n}.png"); Save(mask, $"count_{n}_mask.png"); Destroy(tex); Destroy(mask);
                    yield return null;
                }
                counts.AppendLine($"TOTAL see-through px {total} over {views.Count} views (worst {worst})");
                Destroy(sheet);
                if (input) input.Selection.Clear();
            }
            foreach (var vc in offFx) vc.active = true;
            File.WriteAllText(Path.Combine(Dir, "counts.txt"), counts.ToString());
            File.WriteAllText(Path.Combine(Dir, "log.txt"), log.ToString());
            Destroy(go);
            Report = "DONE\n" + counts; Running = false;
            Destroy(gameObject);
        }

        static Vector3 ModelFront(Component v) { var f = v.transform.forward; f.y = 0; return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; }

        static Vector3 Centre(Component v)
        {
            bool any = false; var b = new Bounds();
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b.center : v.transform.position + Vector3.up;
        }

        Texture2D Render(Component v, Vector3 c, Vector3 fwd, float az, float pitch, float dist, bool maskPass, Material over)
        {
            var dir = Quaternion.AngleAxis(az, Vector3.up) * fwd;                       // from the vehicle toward the camera (0 = in front)
            var pos = c + new Vector3(dir.x * Mathf.Cos(pitch * Mathf.Deg2Rad), Mathf.Sin(pitch * Mathf.Deg2Rad), dir.z * Mathf.Cos(pitch * Mathf.Deg2Rad)) * dist;
            cam.transform.position = pos; cam.transform.LookAt(c);
            if (rackYaw) rackYaw.localRotation = Quaternion.identity;
            var ad = cam.GetComponent<UniversalAdditionalCameraData>(); bool keepPost = ad && ad.renderPostProcessing;
            if (!maskPass) { var a = Grab(); Destroy(a); return Grab(); }                // twice: the first primes the motion vectors (no blur from the jump)
            // the mask: the vehicle and the magenta ground sheet alone (their own layer, no terrain, no post): every pixel that is not the
            // magenta sheet (or the black clear) is a part of the vehicle
            var moved = new List<(GameObject go, int layer)>();
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                moved.Add((r.gameObject, r.gameObject.layer)); r.gameObject.layer = MaskLayer;
            }
            if (sheetGo) { moved.Add((sheetGo, sheetGo.layer)); sheetGo.layer = MaskLayer; }
            int keepMask = cam.cullingMask; var keepClear = cam.clearFlags; var keepBg = cam.backgroundColor;
            cam.cullingMask = 1 << MaskLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            if (ad) ad.renderPostProcessing = false;
            var shot = Grab(); Destroy(shot); shot = Grab();
            foreach (var (go, layer) in moved) go.layer = layer;
            cam.cullingMask = keepMask; cam.clearFlags = keepClear; cam.backgroundColor = keepBg;
            if (ad) ad.renderPostProcessing = keepPost;
            var a1 = shot.GetPixels32(); var o = new Color32[a1.Length];
            for (int i = 0; i < a1.Length; i++)
            {
                var q = a1[i];
                bool bg = (q.r > 140 && q.b > 140 && q.g < 110 && q.g < q.r - 60 && q.g < q.b - 60) || (q.r < 6 && q.g < 6 && q.b < 6);
                o[i] = bg ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            }
            Destroy(shot);
            var mt = new Texture2D(W, H, TextureFormat.RGB24, false); mt.SetPixels32(o); mt.Apply();
            return mt;
        }

        Texture2D Grab()
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        void Save(Texture2D t, string name) => File.WriteAllBytes(Path.Combine(Dir, name), t.EncodeToPNG());

        /// <summary>The hull box's screen outline (convex hull of its 8 corners; texture rows from the bottom, as ReadPixels).</summary>
        List<Vector2> HullPoly(Transform hull)
        {
            var pts = new List<Vector2>();
            foreach (float x in new[] { -0.93f, 0.93f }) foreach (float y in new[] { 0.17f, 1.60f }) foreach (float z in new[] { -2.27f, 2.28f })
            {
                var s = cam.WorldToViewportPoint(hull.TransformPoint(new Vector3(x, y, z)));       // cam.aspect is held at W / H (the render's own)
                pts.Add(new Vector2(s.x * W, s.y * H));
            }
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hullPts = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hullPts.Count;
                for (int k = 0; k < pts.Count; k++)
                {
                    var p = pass == 0 ? pts[k] : pts[pts.Count - 1 - k];
                    while (hullPts.Count >= start + 2 && Cross(hullPts[hullPts.Count - 2], hullPts[hullPts.Count - 1], p) <= 0) hullPts.RemoveAt(hullPts.Count - 1);
                    hullPts.Add(p);
                }
                hullPts.RemoveAt(hullPts.Count - 1);
            }
            return hullPts;
        }
        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        static bool Inside(List<Vector2> poly, float x, float y)
        {
            var p = new Vector2(x, y);
            for (int i = 0; i < poly.Count; i++) if (Cross(poly[i], poly[(i + 1) % poly.Count], p) < 0) return false;
            return poly.Count >= 3;
        }
    }
}
#endif
