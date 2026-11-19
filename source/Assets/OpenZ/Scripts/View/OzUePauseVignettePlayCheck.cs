#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>
    /// about 10 s. Run it in the UNIT EDITOR (Play, the playground up, a few units on the field help the frames) and once in a LEVEL (the match
    /// running, briefing over).
    /// UNIT EDITOR:
    ///  U0 running: no pause look (OZ_UePause weight 0 or not made yet); the camera's stack read (vignette / saturation / exposure = the base)
    ///  U1 PAUSE (OzUeControls.TogglePlay: what SPACE and the PLAY / PAUSE button call): the game paused and time scale 0 at once (as before);
    ///     the look EASES in (weight between 0.02 and 0.98 about 0.12 s after), then weight 1, the camera's stack vignette 0.42, saturation -30,
    ///     exposure -0.18 (the tactical pause's numbers); the floor takes back only the base grade (its _UeVig2.z = the base vignette x 3, its
    ///     _UeGradeA.z = 1 + the base saturation / 100), so the floor is dimmed too
    ///  U2 the picture: the game camera rendered without the HUD, paused vs running: the corners darker by 15 % or more and darker than the middle
    ///  U3 PLAY again: the look eases out (in between after 0.12 s), then weight 0, the stack and the floor back to the base, time scale back
    ///  U4 the same pause / play once more (the second toggle way back and forth: nothing left over)
    /// LEVEL (unchanged): no OZ_UePause volume, no OzUePauseLook; the tactical pause (if its option is on: OzTacticalPause.TestPress) still
    ///  gives OZ_TacticalPause weight 1 and vignette 0.42, and the pause menu (Esc's OpenZPauseMenu.Open / Close) still gives OZ_Pause and 0.4.
    /// Frames (the screen as the player sees it, HUD included): ue_running.jpg, ue_paused.jpg (or level_running.jpg, level_tactical_paused.jpg,
    /// level_menu_paused.jpg). Errors and exceptions logged during the run fail it. Report: <see cref="Folder"/>/report_ue_pause_vignette_UE.txt
    /// or _LEVEL.txt, first line PASS or FAIL.
    /// </summary>
    public sealed class OzUePauseVignettePlayCheck : MonoBehaviour
    {
        public static string Report = "idle";

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_5", "kits", "ue_pause_vignette_r8");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "ue_pause_vignette_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzUePauseVignettePlayCheck").AddComponent<OzUePauseVignettePlayCheck>(); }

        int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        readonly StringBuilder log = new StringBuilder();
        Camera cam;

        void Start() { Application.logMessageReceived += OnLog; StartCoroutine(All()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ ue pause vignette check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        // ------------------------------------------------------------------ reads
        static float VolW(string name)
        {
            // r8 Unity pass: OzPost makes its volume object with HideFlags.DontSave, which FindObjectsByType never returns (the first run read -1
            // with the look on screen): every loaded volume, scene objects only (no assets)
            foreach (var v in Resources.FindObjectsOfTypeAll<Volume>()) if (v && v.gameObject.scene.IsValid() && v.gameObject.name == name) return v.weight;
            return -1f;   // not made
        }

        struct Grade { public float vig, sat, exp; public override string ToString() => $"vignette {vig:0.000}, saturation {sat:0.0}, exposure {exp:0.000}"; }

        Grade StackNow()
        {
            var cd = cam ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
            var stack = cd != null && cd.volumeStack != null ? cd.volumeStack : VolumeManager.instance.stack;
            var g = new Grade();
            var vg = stack != null ? stack.GetComponent<Vignette>() : null; var ca = stack != null ? stack.GetComponent<ColorAdjustments>() : null;
            g.vig = vg != null && vg.IsActive() ? vg.intensity.value : 0f;
            g.sat = ca != null ? ca.saturation.value : 0f; g.exp = ca != null ? ca.postExposure.value : 0f;
            return g;
        }

        static Material FloorMat()
        {
            var f = OzUeFloor.I; if (!f) return null;
            var fi = typeof(OzUeFloor).GetField("mat", BindingFlags.NonPublic | BindingFlags.Instance);
            return fi != null ? fi.GetValue(f) as Material : null;
        }

        /// <summary>Mean luminance of the four corner boxes (10 % of each side) and of the middle box (20 %) of the game camera's own picture
        /// (rendered into a texture: no HUD).</summary>
        Vector2 Lum()
        {
            if (!cam) return Vector2.zero;
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(64, Screen.height / 2);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var old = cam.targetTexture; var act = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            int bw = w / 10, bh = h / 10; double c = 0; int cn = 0;
            void Box(int x0, int y0, int bx, int by, ref double sum, ref int n) { for (int y = y0; y < y0 + by; y += 2) for (int x = x0; x < x0 + bx; x += 2) { var p = tex.GetPixel(x, y); sum += 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b; n++; } }
            Box(0, 0, bw, bh, ref c, ref cn); Box(w - bw, 0, bw, bh, ref c, ref cn); Box(0, h - bh, bw, bh, ref c, ref cn); Box(w - bw, h - bh, bw, bh, ref c, ref cn);
            double m = 0; int mn = 0; Box(w * 2 / 5, h * 2 / 5, w / 5, h / 5, ref m, ref mn);
            Destroy(tex);
            return new Vector2(cn > 0 ? (float)(c / cn) : 0f, mn > 0 ? (float)(m / mn) : 0f);
        }

        IEnumerator Shot(string file)
        {
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            if (t) { File.WriteAllBytes(Path.Combine(Folder(), file), t.EncodeToJPG(92)); Destroy(t); Log("frame " + file); }
        }

        static bool Near(float a, float b, float tol) => Mathf.Abs(a - b) <= tol;

        // ------------------------------------------------------------------ the run
        IEnumerator All()
        {
            Directory.CreateDirectory(Folder());
            var rts = FindFirstObjectByType<RtsCamera>(); cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            bool ue = OzUnitEditor.Active;
            Log($"start {System.DateTime.Now:dd MMM yyyy HH:mm:ss}, {(ue ? "UNIT EDITOR" : "LEVEL " + (ZGame.I ? ZGame.I.level : "?"))}, camera {(cam ? cam.name : "MISSING")}");
            if (ue) yield return UnitEditor(); else yield return Level();
            if (errors > 0) Check($"no errors / exceptions logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            else Check("no errors / exceptions logged during the run", true);
            string head = (fail == 0 && pass > 0 ? "PASS" : "FAIL") + $" ue_pause_vignette_r8 {(ue ? "UNIT EDITOR" : "LEVEL")}: {pass} pass, {fail} fail";
            Report = head;
            File.WriteAllText(Path.Combine(Folder(), "report_ue_pause_vignette_" + (ue ? "UE" : "LEVEL") + ".txt"), head + "\n" + log);
            Debug.Log("[OpenZ ue pause vignette check] " + head + " (" + Folder() + ")");
            Destroy(gameObject);
        }

        IEnumerator UnitEditor()
        {
            Check("the Unit editor's pause look is there (OzUePauseLook)", OzUePauseLook.I != null);
            if (!OzUeControls.Playing) { OzUeControls.SetPlaying(true); yield return Real(1f); }
            float scale0 = Time.timeScale;
            yield return Real(0.5f);
            var g0 = StackNow(); var m = FloorMat();
            Check($"U0 running: no pause look (OZ_UePause weight {VolW(OzUePauseLook.VolumeName):0.00}, -1 = not made yet); base {g0}", VolW(OzUePauseLook.VolumeName) <= 0f && OzUePauseLook.Weight <= 0f);
            Check("U0 the floor's material found (its grade uniforms are read)", m != null);
            var l0 = Lum();
            yield return Shot("ue_running.jpg");

            for (int round = 1; round <= 2; round++)
            {
                string r = round == 1 ? "U1" : "U4";
                OzUeControls.TogglePlay();
                Check($"{r} PAUSE: the game paused and time scale 0 at once (as before)", !OzUeControls.Playing && Time.timeScale == 0f && OzUnitEditor.I.Game.paused);
                // r8 Unity pass: one sample at 0.12 s missed the ease on slow editor frames (the look steps by min(frame, 0.1 s) / 0.3 s and the
                // frame after a screenshot is long): every frame for 0.25 s is read, the ease = at least one frame strictly in between
                float wm = -1f; int fm = 0;
                for (float until = Time.realtimeSinceStartup + 0.25f; Time.realtimeSinceStartup < until;) { yield return null; fm++; float w = OzUePauseLook.Weight; if (w > 0.02f && w < 0.98f && wm < 0f) wm = w; }
                Check($"{r} the look eases in (an in-between weight {wm:0.00} seen in the {fm} frames of 0.25 s after the pause)", wm > 0.02f && wm < 0.98f);
                yield return Real(0.6f);
                var g1 = StackNow();
                Check($"{r} paused: OZ_UePause weight {VolW(OzUePauseLook.VolumeName):0.00} = 1; stack {g1}",
                      VolW(OzUePauseLook.VolumeName) >= 0.999f && Near(g1.vig, OzTacticalPause.VignetteIntensity, 0.02f) && Near(g1.sat, OzTacticalPause.VignetteSaturation, 1f) && Near(g1.exp, OzTacticalPause.VignetteExposure, 0.02f));
                if (m != null)
                {
                    var v2 = m.GetVector("_UeVig2"); var ga = m.GetVector("_UeGradeA");
                    Check($"{r} the floor takes back only the base grade (_UeVig2.z {v2.z:0.000} = base vignette x 3 {g0.vig * 3f:0.000}; _UeGradeA.z {ga.z:0.000} = {1f + g0.sat / 100f:0.000})",
                          Near(v2.z, g0.vig * 3f, 0.01f) && Near(ga.z, 1f + g0.sat / 100f, 0.01f));
                }
                if (round == 1)
                {
                    var l1 = Lum();
                    float rc = l0.x > 1e-4f ? l1.x / l0.x : 1f, rm = l0.y > 1e-4f ? l1.y / l0.y : 1f;
                    if (l0.x < 0.004f) Log($"U2 the picture: the corners are black already while running ({l0.x:0.0000}): not measurable, judged by U1 and the two frames by eye");
                    else Check($"U2 the picture (no HUD): corners {l0.x:0.000} -> {l1.x:0.000} (x{rc:0.00}), middle {l0.y:0.000} -> {l1.y:0.000} (x{rm:0.00}): corners at most x0.85 and darker than the middle", rc <= 0.85f && rc < rm - 0.03f);
                    yield return Shot("ue_paused.jpg");
                }
                OzUeControls.TogglePlay();
                Check($"{r} PLAY: running again, time scale back ({Time.timeScale:0.00} = {scale0:0.00})", OzUeControls.Playing && Near(Time.timeScale, scale0, 0.001f));
                float wo = -1f; int fo = 0;                                                                     // r8 Unity pass: as the ease in
                for (float until = Time.realtimeSinceStartup + 0.25f; Time.realtimeSinceStartup < until;) { yield return null; fo++; float w = OzUePauseLook.Weight; if (w > 0.02f && w < 0.98f && wo < 0f) wo = w; }
                Check($"{r} the look eases out (an in-between weight {wo:0.00} seen in the {fo} frames of 0.25 s after)", wo > 0.02f && wo < 0.98f);
                yield return Real(0.6f);
                var g2 = StackNow();
                Check($"{r} running: weight {OzUePauseLook.Weight:0.00} = 0; stack back to the base ({g2})", OzUePauseLook.Weight <= 0f && Near(g2.vig, g0.vig, 0.005f) && Near(g2.sat, g0.sat, 0.2f) && Near(g2.exp, g0.exp, 0.005f));
                if (m != null) Check($"{r} the floor's grade back to the base (_UeVig2.z {m.GetVector("_UeVig2").z:0.000})", Near(m.GetVector("_UeVig2").z, g0.vig * 3f, 0.01f));
            }
            Log(OzUePauseLook.Report());
        }

        IEnumerator Level()
        {
            var g = ZGame.I; var hud = OpenZHud.I;
            Check("a level is running (ZGame, HUD)", g && g.World != null && hud);
            if (!(g && g.World != null && hud)) yield break;
            Check("no Unit editor pause look in a level (no OzUePauseLook, no OZ_UePause volume)", OzUePauseLook.I == null && VolW(OzUePauseLook.VolumeName) < 0f);
            yield return Real(0.3f);
            var g0 = StackNow();
            Log("running: " + g0);
            yield return Shot("level_running.jpg");
            if (OzTacticalPause.On)
            {
                OzTacticalPause.TestPress();
                yield return Real(0.8f);
                var g1 = StackNow();
                Check($"tactical pause: OZ_TacticalPause weight {VolW("OZ_TacticalPause"):0.00} = 1, stack {g1} (vignette {OzTacticalPause.VignetteIntensity})", VolW("OZ_TacticalPause") >= 0.999f && Near(g1.vig, OzTacticalPause.VignetteIntensity, 0.02f));
                yield return Shot("level_tactical_paused.jpg");
                OzTacticalPause.TestPress();
                yield return Real(0.8f);
                Check($"tactical pause off: weight {VolW("OZ_TacticalPause"):0.00} = 0, stack {StackNow()}", VolW("OZ_TacticalPause") <= 0f && Near(StackNow().vig, g0.vig, 0.005f));
            }
            else Log("the TACTICAL PAUSE option is off: its part skipped (not a fail)");
            if (hud.Pause)
            {
                hud.Pause.Open();
                yield return Real(0.8f);
                var g2 = StackNow();
                Check($"pause menu: OZ_Pause weight {VolW("OZ_Pause"):0.00} >= 0.9, stack {g2} (vignette 0.4)", VolW("OZ_Pause") >= 0.9f && Near(g2.vig, 0.4f, 0.03f));
                yield return Shot("level_menu_paused.jpg");
                hud.Pause.Close();
                yield return Real(1.0f);
                Check($"pause menu closed: running ({!g.paused}), stack {StackNow()}", !g.paused && Near(StackNow().vig, g0.vig, 0.01f));
            }
            Check("still no OZ_UePause volume in the level", VolW(OzUePauseLook.VolumeName) < 0f);
        }
    }
}
#endif
