#if !OZ_RELEASE
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 152: a check of the hull's rock on each shot that runs in a BUILT development player (and in the editor's Play). Off unless the game
    /// is started with the command line argument -ozrockprobe (or Run() is called). Then for every tank / MLRS that kicks on screen (HeavyRecoil),
    /// one log line per shot 0.6 s after it: its type and model, the hull's rock (the drawn root's tilt against its pose the frame before the shot,
    /// deg) and slide (m), the spring's own peaks, and how far the top of its hull moved on the screen (pixels, the game camera, this resolution).
    /// Lines go to the player log and to rock_probe/rock_probe_log.txt in the game's persistent data folder. Nothing in the game changes. A
    /// development path: compiled out of the release build (OZ_RELEASE), as the tread probe.
    /// </summary>
    public class OzRockProbe : MonoBehaviour
    {
        public const string Arg = "-ozrockprobe";
        static OzRockProbe live;
        public static int Lines;

        sealed class Watch { public HeavyRecoil h; public UnitView v; public bool on; public float t0, rock, slide, spring, push, px, calmAt = -9f; public Quaternion rot0, lastRot, calmRot; public Vector3 pos0, scr0, lastPos, lastScr, calmPos, calmScr; }
        readonly List<Watch> watch = new List<Watch>();
        float scanAt; string logPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (live) return;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == Arg) { Run(); break; }
        }

        /// <summary>Starts the probe (once).</summary>
        public static void Run()
        {
            if (live) return;
            var go = new GameObject("OzRockProbe"); DontDestroyOnLoad(go);
            live = go.AddComponent<OzRockProbe>();
            Debug.Log("[OzRockProbe] on: one line per shot of a tank / MLRS on screen; log in " + Path.Combine(Application.persistentDataPath, "rock_probe"));
        }

        void Start()
        {
            var dir = Path.Combine(Application.persistentDataPath, "rock_probe");
            try { Directory.CreateDirectory(dir); logPath = Path.Combine(dir, "rock_probe_log.txt"); File.AppendAllText(logPath, "--- " + System.DateTime.Now.ToString("s") + " " + RecoilShow.Report()); } catch (System.Exception) { logPath = null; }
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam) return;
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + 1f;
                foreach (var h in FindObjectsByType<HeavyRecoil>(FindObjectsSortMode.None))
                {
                    bool known = false; foreach (var w in watch) if (w.h == h) { known = true; break; }
                    if (!known) watch.Add(new Watch { h = h, v = h.GetComponent<UnitView>() });
                }
                watch.RemoveAll(w => !w.h || !w.v);
            }
            foreach (var w in watch)
            {
                if (!w.h || !w.v || w.v.Obj == null) continue;
                var root = w.v.transform; var top = root.position + root.up * 2.2f;
                var scr = cam.WorldToScreenPoint(top);
                if (!w.on && w.h.BodyRockNow > 0.25f && scr.z > 0f && scr.x >= 0f && scr.x <= Screen.width && scr.y >= 0f && scr.y <= Screen.height)
                {   // a shot started: the reference is its calm pose before it (a standing unit's last frame with no rock, under 0.1 s old), else last frame's
                    bool calm = !w.v.Obj.IsMoving && Time.time - w.calmAt < 0.1f;
                    w.on = true; w.t0 = Time.time; w.rot0 = calm ? w.calmRot : w.lastRot; w.pos0 = calm ? w.calmPos : w.lastPos; w.scr0 = calm ? w.calmScr : w.lastScr;
                    w.rock = w.slide = w.spring = w.push = w.px = 0f;
                }
                if (w.on)
                {
                    w.rock = Mathf.Max(w.rock, Quaternion.Angle(w.rot0, root.rotation));
                    var d = root.position - w.pos0; d.y = 0f; w.slide = Mathf.Max(w.slide, d.magnitude);
                    w.spring = Mathf.Max(w.spring, w.h.BodyRockNow); w.push = Mathf.Max(w.push, w.h.BodyPushNow);
                    w.px = Mathf.Max(w.px, ((Vector2)scr - (Vector2)w.scr0).magnitude);
                    if (Time.time - w.t0 > 0.6f)
                    {
                        w.on = false; Lines++;
                        var o = w.v.Obj;
                        string line = $"[OzRockProbe] #{Lines} {o.Type} #{o.Id} ({(OzV2Units.IsV2(w.v) || OzHeavyTank.IsV2(root) ? "v2" : "v1")}{(o.IsMoving ? ", moving" : "")}): the hull rocked {w.rock:0.00} deg and slid {w.slide:0.000} m (spring peaks {w.spring:0.00} deg / {w.push:0.000} m); its top moved {w.px:0.0} px on screen ({Screen.width}x{Screen.height}, camera {Vector3.Distance(cam.transform.position, root.position):0} m)";
                        Debug.Log(line);
                        if (logPath != null) try { File.AppendAllText(logPath, line + "\n"); } catch (System.Exception) { }
                    }
                }
                w.lastRot = root.rotation; w.lastPos = root.position; w.lastScr = scr;
                if (!w.on && Mathf.Abs(w.h.BodyRockNow) < 0.02f) { w.calmRot = root.rotation; w.calmPos = root.position; w.calmScr = scr; w.calmAt = Time.time; }   // the last calm pose (the next shot's reference)
            }
        }
    }
}
#endif
