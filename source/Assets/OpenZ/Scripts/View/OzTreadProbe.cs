#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 items 143 / 146: a check of the rolling tread that runs in the BUILT game (and in the editor's Play). Off unless the game is
    /// started with the command line argument -oztreadprobe. Then, every 2 s, for the moving tank with scrolling belts
    /// (VehicleMotion.ScrollMaterials) nearest the screen centre, it writes one line to the log: the scroll each belt's draw uses now (the
    /// value in the belt's material slot block, VehicleMotion.DrawnScroll) and 0.1 s later, and how the drawn pixels round that tank changed
    /// in those 0.1 s (two screen grabs of the tank's box: the mean change of the belt pixels, i.e. the dark ones, and of the rest), and it
    /// saves both grabs as PNG (the game's persistent data folder, tread_probe/). Nothing in the game changes. A development path: compiled
    /// out of the release build (OZ_RELEASE), as every other check; a development player (the soak build) or the editor runs it.
    /// </summary>
    public class OzTreadProbe : MonoBehaviour
    {
        public const string Arg = "-oztreadprobe";
        static OzTreadProbe live;
        int shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (live) return;
            bool on = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == Arg) on = true;
            if (on) Run();
        }

        /// <summary>Starts the probe (once).</summary>
        public static void Run()
        {
            if (live) return;
            var go = new GameObject("OzTreadProbe"); DontDestroyOnLoad(go);
            live = go.AddComponent<OzTreadProbe>();
            Debug.Log("[OzTreadProbe] on: every 2 s one line per moving tank with scrolling belts; grabs in " + Path.Combine(Application.persistentDataPath, "tread_probe"));
        }

        IEnumerator Start()
        {
            var rs = new List<Renderer>();
            while (true)
            {
                yield return new WaitForSecondsRealtime(2f);
                var cam = Camera.main; if (!cam) continue;
                VehicleMotion best = null; float bestD = 1e9f;
                foreach (var vm in FindObjectsByType<VehicleMotion>(FindObjectsSortMode.None))
                {
                    vm.TrackRenderers(rs);
                    bool belts = false; foreach (var r in rs) if (VehicleMotion.ScrollsBelt(r) && r.isVisible) belts = true;
                    if (!belts) continue;
                    var s = cam.WorldToViewportPoint(vm.transform.position);
                    if (s.z <= 0f || s.x < 0f || s.x > 1f || s.y < 0f || s.y > 1f) continue;
                    float d = (new Vector2(s.x, s.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = vm; }
                }
                if (!best) continue;
                best.TrackRenderers(rs);
                string v0 = Scrolls(rs);
                yield return new WaitForEndOfFrame();
                if (!best) continue;
                var a = Grab(cam, best, out var box);
                yield return new WaitForSecondsRealtime(0.1f);
                yield return new WaitForEndOfFrame();
                if (!best) { if (a) Destroy(a); continue; }
                string v1 = Scrolls(rs);
                var b = Grab(cam, best, out _);
                if (!a || !b) { if (a) Destroy(a); if (b) Destroy(b); continue; }
                Compare(a, b, out float beltChange, out float restChange, out int beltPx);
                shot++;
                string dir = Path.Combine(Application.persistentDataPath, "tread_probe");
                try
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, $"probe_{shot:000}_a.png"), a.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(dir, $"probe_{shot:000}_b.png"), b.EncodeToPNG());
                }
                catch (System.Exception e) { Debug.LogWarning("[OzTreadProbe] could not save the grabs: " + e.Message); }
                Debug.Log($"[OzTreadProbe] #{shot} {best.name}: drawn scroll L/R {v0} -> {v1} (0.1 s); box {box.width:0}x{box.height:0} px; " +
                          $"belt pixels {beltPx}: mean change {beltChange:0.0} levels; other pixels {restChange:0.0} levels; grabs {dir}/probe_{shot:000}_a/_b.png");
                Destroy(a); Destroy(b);
            }
        }

        static string Scrolls(List<Renderer> rs)
        {
            var sb = new StringBuilder();
            foreach (var r in rs) { if (sb.Length > 0) sb.Append('/'); float w = VehicleMotion.DrawnScroll(r); sb.Append(float.IsNaN(w) ? "none" : w.ToString("0.0000")); }
            return sb.ToString();
        }

        /// <summary>The tank's box on screen (all its renderers' bounds), grabbed from the frame just drawn (call at the end of a frame).</summary>
        static Texture2D Grab(Camera cam, VehicleMotion vm, out Rect box)
        {
            box = default;
            bool any = false; var bb = new Bounds();
            foreach (var r in vm.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
            }
            if (!any) return null;
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            for (int i = 0; i < 8; i++)
            {
                var c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1));
                var s = cam.WorldToScreenPoint(c);
                x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
            }
            int ix = Mathf.Clamp(Mathf.FloorToInt(x0), 0, Screen.width - 1), iy = Mathf.Clamp(Mathf.FloorToInt(y0), 0, Screen.height - 1);
            int w = Mathf.Min(Mathf.CeilToInt(x1) - ix, Screen.width - ix), h = Mathf.Min(Mathf.CeilToInt(y1) - iy, Screen.height - iy);
            if (w < 8 || h < 8) return null;
            box = new Rect(ix, iy, w, h);
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.ReadPixels(box, 0, 0, false); t.Apply(false);
            return t;
        }

        /// <summary>Mean luminance change (0..255) of the dark pixels (the belts: under 70 in the first grab) and of the others.</summary>
        static void Compare(Texture2D a, Texture2D b, out float belt, out float rest, out int beltPx)
        {
            var pa = a.GetPixels32(); var pb = b.GetPixels32();
            int wa = a.width, wb = b.width, h = Mathf.Min(a.height, b.height), w = Mathf.Min(wa, wb);
            double sb = 0, sr = 0; int nb = 0, nr = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c0 = pa[y * wa + x]; var c1 = pb[y * wb + x];
                    float l0 = 0.2126f * c0.r + 0.7152f * c0.g + 0.0722f * c0.b, l1 = 0.2126f * c1.r + 0.7152f * c1.g + 0.0722f * c1.b;
                    if (l0 < 70f) { sb += Mathf.Abs(l1 - l0); nb++; } else { sr += Mathf.Abs(l1 - l0); nr++; }
                }
            belt = nb > 0 ? (float)(sb / nb) : 0f; rest = nr > 0 ? (float)(sr / nr) : 0f; beltPx = nb;
        }
    }
}
#endif
