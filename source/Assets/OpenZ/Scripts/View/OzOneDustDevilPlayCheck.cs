#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The Play check of "the L02 desert has ONE dust devil" (View/OzDustDevils.cs Count; the old
    /// used. The game camera follows the devil at the normal height (RtsCamera.follow, like a player keeping it in view).
    ///  W  a devil is there within 20 s of the start (the lit devils wait for their material, at most 6 s)
    ///  N  for 60 s of game time, every frame: exactly ONE devil (an active "DustDevil" + number) under the level's Atmosphere, and it is
    ///     DustDevil0 (never DustDevil1); the lit devils' keeper holds one; three scans of every loaded object (start, middle, end) find no
    ///     other devil anywhere
    ///  D  it is drawn every frame (a funnel on; the old devils' path: its particles playing); the lit devils' draws at most Layers + 1
    ///     (one devil: 4, two were 8), their live wisps + grit at most WispCap + GritCap (one devil: 80)
    ///  S  the wander as before: its path over the game time = OzDustDevils.Speed (2.5 m/s) within 4 %, never out of the old box
    ///     (x 10..134, z -170..-10, one step of slack)
    ///  T  its ground trace still runs (OzDustDevilTrace: swept marks + off-sand steps grew) while the trace is on
    /// Frames (no helper marks, the game camera as the player has it): one_dust_devil_L02_game_camera.jpg (the normal height 70 m, the devil in
    /// the middle) and one_dust_devil_L02_wide.jpg (the widest height over the middle of its box). Errors and exceptions logged during the run
    /// fail it. Report: <see cref="Folder"/>/report_one_dust_devil.txt, first line PASS or FAIL. Test only: the camera's follow, focus and height
    /// are put back.
    /// </summary>
    public sealed class OzOneDustDevilPlayCheck : MonoBehaviour
    {
        public static string Report = "idle";

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_5", "kits", "one_dust_devil_r7");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "one_dust_devil_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzOneDustDevilPlayCheck").AddComponent<OzOneDustDevilPlayCheck>(); }

        static readonly Regex RootName = new Regex(@"^DustDevil\d+$");
        const float RunS = 60f, WaitS = 20f, RealCapS = 240f, NormalHeight = 70f, Slack = 1f;
        const float BoxX0 = 10f, BoxX1 = 134f, BoxZ0 = -170f, BoxZ1 = -10f;

        RtsCamera rts; Transform follow0; Vector3 focus0; float height0; bool camTaken;
        int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        readonly StringBuilder log = new StringBuilder();

        void Start() { Application.logMessageReceived += OnLog; StartCoroutine(All()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; CameraBack(); }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ one dust devil check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }

        void CameraBack()
        {
            if (!camTaken || !rts) return;
            camTaken = false;
            rts.follow = follow0; rts.focus = focus0; rts.ZoomTo(height0);
        }

        /// <summary>The active devils under the level's Atmosphere objects (the lit devils and the old ones are both its children).</summary>
        static List<Transform> Devils(out int atmospheres)
        {
            var list = new List<Transform>(); atmospheres = 0;
            foreach (var a in FindObjectsByType<Atmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!a || !a.gameObject.scene.IsValid()) continue;
                atmospheres++;
                foreach (Transform c in a.transform) if (c && c.gameObject.activeInHierarchy && RootName.IsMatch(c.name)) list.Add(c);
            }
            return list;
        }

        /// <summary>Every active devil in the loaded scenes, wherever it hangs (DontSave objects included: Resources.FindObjectsOfTypeAll).</summary>
        static List<string> ScanAll()
        {
            var names = new List<string>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t && t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy && RootName.IsMatch(t.name))
                    names.Add(t.name + " under " + (t.parent ? t.parent.name : "(root)"));
            return names;
        }

        static OzDustDevils Keeper() => Resources.FindObjectsOfTypeAll<OzDustDevils>().FirstOrDefault(k => k && k.gameObject.scene.IsValid() && k.gameObject.activeInHierarchy);

        /// <summary>The lit devils' keeper's own list (private "devils", as the owner film's DD command reads it): -1 when it cannot be read.</summary>
        static int KeeperCount(OzDustDevils k)
        {
            var f = typeof(OzDustDevils).GetField("devils", BindingFlags.NonPublic | BindingFlags.Instance);
            return k && f != null && f.GetValue(k) is IList l ? l.Count : -1;
        }

        static bool Drawn(Transform d)
        {
            foreach (var r in d.GetComponentsInChildren<MeshRenderer>(false)) if (r.enabled && r.name.Contains("_Funnel")) return true;
            var ps = d.GetComponent<ParticleSystem>();                                                   // the old devils (Atmosphere.BuildDevils)
            return ps && ps.isPlaying;
        }

        IEnumerator Still(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (!tex) { Log("frame " + name + ": no picture"); yield break; }
            try { Directory.CreateDirectory(Folder()); File.WriteAllBytes(Path.Combine(Folder(), name + ".jpg"), tex.EncodeToJPG(95)); Log($"frame {name}.jpg ({tex.width} x {tex.height})"); }
            catch (IOException ex) { Log("frame " + name + ": not written (" + ex.Message + ")"); }
            Destroy(tex);
        }

        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }

        IEnumerator All()
        {
            var g = ZGame.I;
            Log($"one_dust_devil_r7 Play check, {System.DateTime.Now:dd MMM yyyy HH:mm}, level {(g ? g.level : "none")}, OzDustDevils.Count {OzDustDevils.Count}, On {OzDustDevils.On}, trace {(OzDustDevilTrace.On ? "on" : "off")}");
            if (!g) { Check("a level is playing (ZGame)", false); Finish(); yield break; }
            if (g.level != "L02") Log("NOTE: the level is " + g.level + ", not L02 (only the desert has dust devils)");

            // ---- W
            float t0 = Time.unscaledTime; var now = Devils(out int atmos);
            while (now.Count == 0 && Time.unscaledTime - t0 < WaitS) { yield return null; now = Devils(out atmos); }
            bool lit = !OzDustDevils.Failed;
            Check($"W a devil is there {Time.unscaledTime - t0:0.0} s after the start (Atmosphere objects {atmos}; {(lit ? "the lit devils" : "the lit devils' material never came: the OLD devils' path")})", now.Count > 0);
            if (now.Count == 0) { Finish(); yield break; }

            rts = FindFirstObjectByType<RtsCamera>();
            if (rts) { follow0 = rts.follow; focus0 = rts.focus; height0 = rts.TargetHeight; camTaken = true; rts.follow = now[0]; rts.ZoomTo(NormalHeight); }
            else Log("NOTE: no RtsCamera: no camera follow, no frames");

            // ---- N, D, S, T over 60 s of game time
            var keeper = Keeper();
            var scans = new List<string>(); int scansOne = 0;
            var first = ScanAll(); scans.Add("start: " + (first.Count == 0 ? "none" : string.Join(", ", first))); if (first.Count == 1) scansOne++;
            int trace0 = OzDustDevilTrace.Sweeps + OzDustDevilTrace.OffSand, puffs0 = OzDustDevilTrace.DustPuffs;
            Transform dev = now[0]; Vector3 last = dev.position;
            float game = 0f, path = 0f, real0 = Time.unscaledTime, nextNote = 0f;
            int frames = 0, one = 0, more = 0, none = 0, wrongName = 0, drawn = 0, keeperOne = 0, keeperSeen = 0, maxDraws = 0, maxLive = 0, outBox = 0, swaps = 0;
            bool midScan = false;
            while (game < RunS && Time.unscaledTime - real0 < RealCapS)
            {
                yield return null;
                float dt = Time.deltaTime;
                now = Devils(out _); frames++;
                if (now.Count == 1) one++; else if (now.Count == 0) none++; else more++;
                foreach (var d in now) if (d.name != "DustDevil0") wrongName++;
                if (now.Count == 0) continue;
                var cur = now[0];
                if (cur != dev) { swaps++; dev = cur; last = cur.position; if (rts) rts.follow = cur; continue; }
                if (Drawn(cur)) drawn++;
                var p = cur.position;
                if (dt > 0f) { var m = p - last; m.y = 0f; path += m.magnitude; game += dt; }
                last = p;
                if (p.x < BoxX0 - Slack || p.x > BoxX1 + Slack || p.z < BoxZ0 - Slack || p.z > BoxZ1 + Slack) outBox++;
                if (lit)
                {
                    maxDraws = Mathf.Max(maxDraws, OzDustDevils.Draws); maxLive = Mathf.Max(maxLive, OzDustDevils.LiveNow);
                    if (frames % 30 == 0) { if (!keeper) keeper = Keeper(); keeperSeen++; if (KeeperCount(keeper) == 1) keeperOne++; }
                }
                if (game >= nextNote) { nextNote += 10f; Log($"  t {game:00.0} s: {cur.name} at ({p.x:0.0}, {p.z:0.0}), path {path:0.0} m, devils now {now.Count}, draws {OzDustDevils.Draws}, live {OzDustDevils.LiveNow}"); }
                if (!midScan && game >= RunS * 0.5f)
                {
                    midScan = true;
                    var mid = ScanAll(); scans.Add("middle: " + (mid.Count == 0 ? "none" : string.Join(", ", mid))); if (mid.Count == 1) scansOne++;
                }
            }
            var end = ScanAll(); scans.Add("end: " + (end.Count == 0 ? "none" : string.Join(", ", end))); if (end.Count == 1) scansOne++;
            int withOne = one + more;                                                                    // frames with a devil to look at

            Check($"N {game:0.0} s of game time watched ({frames} frames; {Time.unscaledTime - real0:0.0} s real; at least 55 s and 100 frames)", game >= RunS - 5f && frames >= 100);
            Check($"N exactly ONE devil every frame: one {one}, two or more {more}, none {none} of {frames} frames", frames > 0 && one == frames);
            Check($"N it is DustDevil0 every frame (another name seen {wrongName} times; the devil changed {swaps} times)", wrongName == 0 && swaps == 0);
            Check("N the scans of every loaded object find one devil each time (" + string.Join("; ", scans) + ")", scansOne == scans.Count);
            if (lit)
            {
                Check($"N the lit devils' keeper holds one devil ({keeperOne} of {keeperSeen} reads; keeper {(keeper ? "found" : "NOT found")})", keeperSeen > 0 && keeperOne == keeperSeen);
                Check($"D the lit devils' draws at most {OzDustDevils.Layers + 1} (max {maxDraws}; two devils were up to {2 * (OzDustDevils.Layers + 1)}) and at least {OzDustDevils.Layers} (its funnels on)", maxDraws <= OzDustDevils.Layers + 1 && maxDraws >= OzDustDevils.Layers);
                Check($"D live wisps + grit at most {OzDustDevils.WispCap + OzDustDevils.GritCap} (max {maxLive}; one devil's caps)", maxLive <= OzDustDevils.WispCap + OzDustDevils.GritCap);
            }
            else Log("NOTE: the old devils' path (OzDustDevils.Failed): the keeper, draws and live counts are the lit devils' only; N above counts the old devils");
            Check($"D it is drawn every frame ({drawn} of {withOne - swaps} frames)", withOne > 0 && drawn >= withOne - swaps);
            float speed = game > 0f ? path / game : 0f;
            Check($"S the wander as before: {speed:0.000} m/s over the game time (OzDustDevils.Speed {OzDustDevils.Speed:0.0}, within 4 %; path {path:0.0} m in {game:0.0} s)", game > 0f && Mathf.Abs(speed - OzDustDevils.Speed) <= OzDustDevils.Speed * 0.04f);
            Check($"S inside the old box x 10..134, z -170..-10 every frame (out {outBox})", outBox == 0);
            int traceGrew = OzDustDevilTrace.Sweeps + OzDustDevilTrace.OffSand - trace0, puffs = OzDustDevilTrace.DustPuffs - puffs0;
            if (OzDustDevilTrace.On) Check($"T its ground trace runs: {traceGrew} swept marks + off-sand steps, {puffs} dust puffs in the run ({OzDustDevilTrace.Status()})", traceGrew > 0);
            else Log("NOTE: OzDustDevilTrace.On is off: T not checked");

            // ---- the frames: the normal height on the devil, then the widest height over its box
            if (rts && dev)
            {
                rts.follow = dev; rts.ZoomTo(NormalHeight);
                yield return Real(1.5f);
                yield return Still("one_dust_devil_L02_game_camera");
                rts.follow = null; rts.focus = new Vector3((BoxX0 + BoxX1) * 0.5f, 0f, (BoxZ0 + BoxZ1) * 0.5f); rts.ZoomTo(rts.maxHeight);
                yield return Real(2.5f);
                Log($"  wide frame: the camera at {rts.height:0} m over ({rts.focus.x:0}, {rts.focus.z:0}); devils now {Devils(out _).Count}");
                yield return Still("one_dust_devil_L02_wide");
            }
            CameraBack();
            if (errors > 0) Check($"no error or exception logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            Finish();
        }

        void Finish()
        {
            string head = $"{(fail == 0 && pass > 0 ? "PASS" : "FAIL")}: {pass} passed, {fail} failed";
            Report = head;
            log.Insert(0, head + "\n");
            try { Directory.CreateDirectory(Folder()); File.WriteAllText(Path.Combine(Folder(), "report_one_dust_devil.txt"), log.ToString()); } catch (IOException) { }
            Debug.Log("[OpenZ one dust devil check] " + head);
            Destroy(gameObject);
        }
    }
}
#endif
