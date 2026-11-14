#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// k75a Play proof of the faction screen (1.2 owner items 77, 78, 78b, 81, 88, 89, 90); menu OpenZ/V1.2/Faction Screen: 75a Play Proof
    /// (Play), Editor/OzV12FactionCheck.cs. Started on the MainMenu scene in Play it walks by itself (about three minutes), hands off:
    ///  P1  the main menu's own PLAY action opens the faction screen.
    ///  P2  the posters load (async): both prints' layers, both light models (170 000 triangles), two cameras into render textures of the
    ///      posters' size, the posters' sun, the alpha global on.
    ///  P3  real 3D: both renders read back: characters drawn (coverage), transparent round them, lit (not black); 1 s later the pixels
    ///      have changed and the cameras have turned. The raw renders are saved.
    ///  P4  the sway: 8 s of the cameras' yaw / pitch (pointer away): inside +-4 / +-1.5 deg, at most 4 deg a second, forward and back;
    ///      six pictures over one loop (sway_1..6).
    ///  P5  a simulated pointer: over Zod's poster the screen turns red, the TIP "TIP: Wise choice!" shows under it (none under EvilZod's),
    ///      his camera follows the pointer; half way: no tint, no tip; over EvilZod's: blue, "TIP: You chose... poorly."; the jump from one
    ///      poster to the other eases (no jump in the tint). Pictures.
    ///  P6  keys (the gamepad's D-pad sends the same keys): LEFT / RIGHT focus, the tip and the tint follow the focus; RIGHT again answers
    ///      with ui_disabled.
    ///  P7  pick GENERAL ZOD (a click): the stamp sound, Zod's "Wise choice!" (zod_pick_red) on his voice bus, the words on the plate,
    ///      the box's setting red; the screen holds for the line, then the level selection names GENERAL ZOD, red's objective; the posters
    ///      are unloaded (no texture, render texture or camera of them left; the sun and the alpha global back).
    ///  P8  the level selection's BACK: the faction screen again, the posters load again.
    ///  P9  pick GENERAL EVILZOD (keys): "You chose... poorly." (zod_pick_blue), the box's setting blue, the level selection names GENERAL
    ///      EVILZOD with blue's objective (the red fort).
    ///  P10 DEPLOY level 2 from there: the level plays in the blue mode exactly as the old box did (OzPlayBlue.Active, the human blue, the
    ///      camera over the blue fort); nothing of the faction screen exists in the level. Back to the main menu.
    ///  P11 PLAY, then BACK to the main menu: the posters unloaded and swept, the texture memory back.
    ///  P12 'Zod exact': the EvilZod poster is locked (ui_disabled, the side stays).
    ///  P13 1366 x 768: the screen fits (posters, tips, titles, the BACK button apart); pictures. Back to 1920 x 1080 and the Game view's own size.
    /// Pictures-only (menu ... 75a Pictures Only): P1, P2, the pictures of P4 / P5 / P6 at the Game view's current size, back.
    /// The player's settings are put back (the side, 'Zod exact', the Game view's size), also when Play stops first. Lines and pictures in
    /// the kit's proof/play_n (a new folder each run). Test only (editor).
    /// </summary>
    public sealed class OzFactionProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzFactionProof I { get; private set; }
        const string T = "[OzFactionProof] ";
        const BindingFlags IF = BindingFlags.NonPublic | BindingFlags.Instance;

        bool picturesOnly, started, putBack;
        bool boxWas, hadExact; int exactWas, viewWas = -1; int[] heardWas;                              // 1.3 item 6: Zod's pick-line marks, put back at the end
        string dir; readonly StringBuilder sb = new StringBuilder(); int passes, fails, pics;
        float levelLoadedAt = -1f;

        /// <summary></summary>
        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                {
                    var kit = Path.Combine(stage, "k75a_faction_screen");
                    if (Directory.Exists(kit)) return Path.Combine(kit, "proof");
                }
            }
            return Path.Combine(Application.persistentDataPath, "k75a_faction_screen");
        }

        public static void Run(bool picturesOnly)
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzFactionProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzFactionProof>().picturesOnly = picturesOnly;
        }

        public static void Stop()
        {
            if (!I) { Debug.Log(T + "not running"); return; }
            I.StopAllCoroutines(); I.Log("stopped by the menu"); I.Finish();
        }

        void Awake() { I = this; }

        void Start()
        {
            string root = Folder(); int n = 1;
            while (Directory.Exists(Path.Combine(root, "play_" + n))) n++;
            dir = Path.Combine(root, "play_" + n); Directory.CreateDirectory(dir);
            boxWas = OzPlayBlue.Box; hadExact = PlayerPrefs.HasKey("oz_zod_exact"); exactWas = PlayerPrefs.GetInt("oz_zod_exact", 0);
            heardWas = OzFaction.ProofHeardKeep();
            viewWas = OzFinalGateView.SelectedIndex();
            SceneManager.sceneLoaded += OnLoaded;
            started = true;
            Log("k75a faction screen Play proof" + (picturesOnly ? " (pictures only)" : "") + ", Game view " + Screen.width + " x " + Screen.height + ", into " + dir);
            StartCoroutine(All());
        }

        void OnLoaded(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) levelLoadedAt = Time.realtimeSinceStartup; }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the player's settings put back"); }
            if (I == this) I = null;
        }

        void PutBack()
        {
            putBack = true;
            OzPlayBlue.Box = boxWas;
            if (hadExact) PlayerPrefs.SetInt("oz_zod_exact", exactWas); else PlayerPrefs.DeleteKey("oz_zod_exact");
            OzFaction.ProofHeardPutBack(heardWas);                                                           // 1.3 item 6
            PlayerPrefs.Save();
            var m = Menu(); if (m) m.ProofFactionPointer(null);
            if (viewWas >= 0) OzFinalGateView.Select(viewWas);
        }

        void Finish()
        {
            if (!putBack) PutBack();
            Report = $"{T}{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks, {pics} pictures; {dir.Replace('\\', '/')}";
            Log(Report);
            File.WriteAllText(Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Log(string t) { sb.AppendLine(t); Debug.Log(T + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !SafeTrue(c)) yield return null; }
        static bool SafeTrue(System.Func<bool> c) { try { return c(); } catch (System.Exception) { return false; } }
        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();
        static bool InMenu => Menu() && SceneManager.GetActiveScene().name == OzPlayBlue.MenuScene;
        /// <summary>The last frame's draw numbers (the editor's frame statistics) and the cameras.</summary>
        static string Stats() => $"{UnityEditor.UnityStats.batches} batches, {UnityEditor.UnityStats.setPassCalls} set-pass calls, {UnityEditor.UnityStats.triangles} triangles, {Camera.allCamerasCount} camera(s)";
        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");
        static float LogNow => Time.unscaledTime;
        static int Sounds(float l0, string key) => OzSoundLog.Since(l0).Count(e => e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY"));

        IEnumerator Shot(string name)
        {
            string p = Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(p); pics++; Log("   picture " + Path.GetFileName(p));
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        IEnumerator SetSize(int w, int h)
        {
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            yield return Until(() => Screen.width == w && Screen.height == h, 5f);
            yield return Real(0.8f);                                                                        // the menu rebuilds for the new size
            if (Screen.width != w || Screen.height != h) Check($"the Game view shows {w} x {h} ({info}; it shows {Screen.width} x {Screen.height})", false);
            else Log("Game view " + w + " x " + h + " (" + info + ")");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>The main menu's own PLAY action (the item a click on PLAY runs).</summary>
        static bool Play(OpenZMainMenu m)
        {
            var items = typeof(OpenZMainMenu).GetField("items", IF)?.GetValue(m) as IList;
            if (items == null) return false;
            foreach (var it in items)
            {
                var ft = it.GetType();
                if (ft.GetField("label")?.GetValue(it) as string != "PLAY") continue;
                var act = ft.GetField("act")?.GetValue(it) as System.Action;
                if (act == null) return false;
                act(); return true;
            }
            return false;
        }

        IEnumerator OpenByPlay(string tag)
        {
            var m = Menu();
            if (!m) { Check(tag + " the main menu", false); yield break; }
            if (m.ProofView != "Main") { m.ProofMain(); yield return Real(0.6f); }
            int opens = OpenZMainMenu.FactionOpens;
            bool ran = Play(m);
            yield return Until(() => m.ProofFactionShown, 2.5f);
            Check($"{tag} the main menu's PLAY opens the faction screen: ran {ran}, view {m.ProofView}, opened {OpenZMainMenu.FactionOpens - opens} x, its posters' rig {(m.ProofRig ? "made" : "MISSING")}",
                  ran && m.ProofView == "Faction" && OpenZMainMenu.FactionOpens == opens + 1 && m.ProofRig);
            yield return Until(() => m.ProofRig && m.ProofRig.Ready && m.ProofPrintAlpha(0) > 0.99f && m.ProofPrintAlpha(1) > 0.99f, 12f);
            yield return Real(0.6f);
        }

        /// <summary>A render read back (through a plain copy: the render has 4x MSAA).</summary>
        static Color32[] ReadBack(RenderTexture rt)
        {
            if (!rt) return null;
            var tmp = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(rt, tmp);
            var prev = RenderTexture.active; RenderTexture.active = tmp;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
            var px = tex.GetPixels32(); Destroy(tex);
            return px;
        }

        void SavePng(Color32[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG()); Destroy(tex); pics++;
            Log("   picture " + name + ".png (the raw render with its alpha)");
        }

        static readonly string[] AssetNames =
        {
            "zod_poster_BaseColor", "zod_poster_Normal", "zod_poster_Emission", "zod_poster_MetallicSmoothness", "evil_poster_BaseColor", "evil_poster_Normal", "evil_poster_Emission", "evil_poster_MetallicSmoothness",
            "zod_background", "zod_mid", "zod_frame_border", "zod_frame_band", "evil_background", "evil_mid", "evil_frame_border", "evil_frame_band", "paper_grain_tile",
        };

        /// <summary>The light models' mesh names as the rig loaded them (for the leftover count).</summary>
        static readonly HashSet<string> MeshNames = new HashSet<string>();

        static void NoteMeshes(OzFactionPosters rig)
        {
            if (!rig) return;
            foreach (var p in rig.P)
                if (p != null && p.model)
                    foreach (var mf in p.model.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) MeshNames.Add(mf.sharedMesh.name);
        }

        /// <summary>What of the faction screen is in memory: its textures, meshes, render textures and cameras (counts).</summary>
        static string Leftovers(out int n)
        {
            var tex = Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => AssetNames.Contains(t.name)).Select(t => t.name).ToList();
            var mesh = Resources.FindObjectsOfTypeAll<Mesh>().Where(x => MeshNames.Contains(x.name) || x.name.Contains("_poster_light")).Select(x => x.name).ToList();
            var rts = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(r => r.name.StartsWith("OzFactionPoster_")).Select(r => r.name).ToList();
            var cams = Resources.FindObjectsOfTypeAll<Camera>().Where(c => c && c.gameObject.scene.IsValid() && (c.name == "Camera_zod" || c.name == "Camera_evil")).Select(c => c.name).ToList();
            n = tex.Count + mesh.Count + rts.Count + cams.Count;
            return $"{tex.Count} texture(s){(tex.Count > 0 ? " (" + string.Join(", ", tex.Take(6)) + ")" : "")}, {mesh.Count} mesh(es), {rts.Count} render texture(s), {cams.Count} camera(s)";
        }

        // ------------------------------------------------------------------------------------------------ the walk
        IEnumerator All()
        {
            yield return Real(1f);
            if (!picturesOnly) yield return SetSize(1920, 1080);
            var sunBefore = RenderSettings.sun; float alphaBefore = Shader.GetGlobalFloat("_AtmoKeepAlpha");
            long memBefore = (long)Texture.currentTextureMemory;
            string statsMenu = Stats();
            yield return OpenByPlay("P1");
            var m = Menu();
            yield return Loaded(m);
            Log($"   the frame's draw numbers: the main menu {statsMenu}; the faction screen {Stats()}; texture memory {memBefore / 1048576f:0} MB -> {(long)Texture.currentTextureMemory / 1048576f:0} MB");
            if (picturesOnly)
            {
                yield return Pictures(m);
                m.ProofFactionBack(); yield return Real(1.5f);
                Finish(); yield break;
            }
            yield return Renders(m);
            yield return Sway(m);
            yield return Pointer(m);
            yield return KeysFocus(m);
            yield return PickRed(m, sunBefore, alphaBefore);
            yield return BackAndBlue();
            yield return BlueLevel();
            yield return BackToMain(memBefore);
            yield return ZodExact();
            yield return Small();
            Finish();
        }

        IEnumerator Loaded(OpenZMainMenu m)
        {
            var rig = m.ProofRig;
            bool ready = rig && rig.Ready;
            NoteMeshes(rig);
            Log("   the light models' meshes: " + (MeshNames.Count > 0 ? string.Join(", ", MeshNames) : "none found"));
            int layers = 0;
            for (int i = 0; i < 2; i++) for (int k = 0; k < 5; k++) { var r = m.ProofLayer(i, k); if (r && r.texture && r.enabled) layers++; }
            int cams = 0; string sizes = "";
            if (rig) for (int i = 0; i < 2; i++) { var c = rig.P[i].cam; if (c && c.enabled && c.targetTexture) { cams++; sizes += (sizes.Length > 0 ? ", " : "") + c.targetTexture.width + " x " + c.targetTexture.height; } }
            int wantH = Mathf.Clamp(Mathf.RoundToInt(OzFaction.PosterH * Screen.height / OzLayout.RefH * 1.04f), 256, 2048);
            int rtH = OzFactionPosters.RtSize(Mathf.RoundToInt(wantH * OzFaction.PosterAspect), wantH).y;           // k75a4: the render supersampled
            bool sizeOk = rig && rig.P[0].rt && rig.P[0].rt.height == rtH && rig.P[1].rt && rig.P[1].rt.height == rtH;
            bool figs = m.ProofFigure(0) && m.ProofFigure(0).texture == (rig ? rig.P[0].rt : null) && m.ProofFigure(1) && m.ProofFigure(1).texture == (rig ? rig.P[1].rt : null);
            int prints = 0;                                                                                     // k75a2: the print layers draw with their poster's aged-paper material
            if (rig) for (int i = 0; i < 2; i++) for (int k = 0; k < 4; k++) { var r = m.ProofLayer(i, k); if (r && rig.Print[i] && r.material == rig.Print[i] && rig.Print[i].GetFloat("_Print") > 0.5f && rig.Print[i].GetFloat("_PaperSee") > 0.05f) prints++; }
            Check($"P2 the posters loaded in {(rig ? rig.LoadSeconds : -1f):0.00} s: models {(rig ? rig.Triangles : 0)} triangles (170 000 wanted), the prints' layers shown {layers} of 10 ({prints} of 8 with the aged paper), the renders on screen {figs}, "
                  + $"cameras rendering {cams} ({sizes}; {wantH} px tall wanted), the posters' sun {(rig && RenderSettings.sun == rig.Key)}, _AtmoKeepAlpha {Shader.GetGlobalFloat("_AtmoKeepAlpha"):0}",
                  ready && rig.Triangles == 170000 && layers == 10 && prints == 8 && figs && cams == 2 && sizeOk && RenderSettings.sun == rig.Key && Shader.GetGlobalFloat("_AtmoKeepAlpha") > 0.5f);
            yield return Shot("faction_idle");
        }

        IEnumerator Renders(OpenZMainMenu m)
        {
            var rig = m.ProofRig; if (!rig) { Check("P3 the rig", false); yield break; }
            yield return new WaitForEndOfFrame();
            var a = new Color32[2][]; var yaw0 = new float[2];
            for (int i = 0; i < 2; i++) { a[i] = ReadBack(rig.P[i].rt); yaw0[i] = rig.P[i].yaw; }
            yield return Real(1f);
            yield return new WaitForEndOfFrame();
            for (int i = 0; i < 2; i++)
            {
                var rt = rig.P[i].rt; var b = ReadBack(rt);
                if (a[i] == null || b == null || a[i].Length != b.Length) { Check($"P3 {rig.P[i].id}: the render read back", false); continue; }
                int w = rt.width, h = rt.height, cover = 0, changed = 0; double lum = 0; int lumN = 0;
                for (int k = 0; k < b.Length; k++)
                {
                    var c = b[k];
                    if (c.a > 127) { cover++; lum += (c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722) / Mathf.Max(1, c.a) * 255.0; lumN++; }
                    if (Mathf.Abs(c.r - a[i][k].r) + Mathf.Abs(c.g - a[i][k].g) + Mathf.Abs(c.b - a[i][k].b) + Mathf.Abs(c.a - a[i][k].a) > 24) changed++;
                }
                float coverK = cover / (float)b.Length, changedK = changed / (float)b.Length, mean = lumN > 0 ? (float)(lum / lumN) : 0f;
                int clear = 0; foreach (var idx in new[] { 0, w - 1, (h - 1) * w, h * w - 1 }) if (b[idx].a < 8) clear++;
                // the top corners: the frame's empty paper above the hat in both prints
                Check($"P3 {rig.P[i].id}: real 3D drawn: {coverK * 100f:0} % of the render covered (25..92 %), {clear} of 4 corners transparent, the characters lit (mean light {mean:0} of 255)",
                      coverK > 0.25f && coverK < 0.92f && clear >= 1 && mean > 30f);
                Check($"P3 {rig.P[i].id}: the camera moves: yaw {yaw0[i]:0.00} -> {rig.P[i].yaw:0.00} deg in 1 s, {changedK * 100f:0.0} % of the pixels changed", Mathf.Abs(rig.P[i].yaw - yaw0[i]) > 0.2f && changedK > 0.005f);
                SavePng(b, w, h, "render_" + rig.P[i].id + "_" + Screen.width + "x" + Screen.height);
            }
        }

        IEnumerator Sway(OpenZMainMenu m)
        {
            var rig = m.ProofRig; if (!rig) yield break;
            m.ProofFactionPointer(new Vector2(4f, 4f));                                                      // away from both posters
            yield return Real(1.5f);
            float ymax = 0f, pmax = 0f, rate = 0f; int turns = 0; float t0 = Time.realtimeSinceStartup, last = t0, py = rig.P[0].yaw, pd = 0f; int shot = 0;
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                yield return null;
                float now = Time.realtimeSinceStartup, dt = Mathf.Max(1e-4f, now - last); last = now;
                for (int i = 0; i < 2; i++) { ymax = Mathf.Max(ymax, Mathf.Abs(rig.P[i].yaw)); pmax = Mathf.Max(pmax, Mathf.Abs(rig.P[i].pitch)); }
                float d = rig.P[0].yaw - py; py = rig.P[0].yaw;
                rate = Mathf.Max(rate, Mathf.Abs(d) / dt);
                if (Mathf.Abs(d) > 1e-5f) { if (pd != 0f && Mathf.Sign(d) != Mathf.Sign(pd)) turns++; pd = d; }
                if (shot < 6 && now - t0 >= shot * (OzFaction.SwayPeriod / 6f)) { shot++; yield return Shot("sway_" + shot); last = Time.realtimeSinceStartup; py = rig.P[0].yaw; }
            }
            Check($"P4 the sway (pointer away): yaw within +-{ymax:0.00} deg, pitch +-{pmax:0.00} deg (4 / 1.5 wanted), at most {rate:0.00} deg a second, {turns} turn(s) in 8 s (forward and back); pictures sway_1..6",
                  ymax <= OzFaction.SwayYaw + 0.05f && pmax <= OzFaction.SwayPitch + 0.05f && ymax > 2f && rate < 5f && turns >= 1);
        }

        IEnumerator Pointer(OpenZMainMenu m)
        {
            var r0 = ScreenRect(m.ProofPoster(0)); var r1 = ScreenRect(m.ProofPoster(1));
            m.ProofFactionPointer(r0.center); yield return Real(1.6f);
            var w = m.ProofWashUnder; float mood = m.ProofMood;
            var gw = m.ProofGlow; var gr = m.ProofGlowRt ? ScreenRect(m.ProofGlowRt) : new Rect();               // k75a2: the soft round glow, centred on Zod's poster
            bool glowRed = gw.r > gw.b && Mathf.Abs(gw.a - OzFaction.Glow[OzFaction.Red] * -mood) < 0.005f && Vector2.Distance(gr.center, r0.center) < 3f;
            Check($"P5 the pointer over Zod's poster: mood {mood:0.00}, the wash red ({w.r:0.00}, {w.g:0.00}, {w.b:0.00}, alpha {w.a:0.000}), the glow alpha {gw.a:0.000} centred on his poster {glowRed}, the tip \"{Plain(m.ProofTipText(0))}\" shown {m.ProofTipAlpha(0):0.00}, none under EvilZod's {m.ProofTipAlpha(1):0.00}",
                  mood < -0.95f && w.r > w.b && Mathf.Abs(w.a - OzFaction.WashUnder * -mood) < 0.002f && glowRed && Plain(m.ProofTipText(0)) == "TIP: Wise choice!" && m.ProofTipAlpha(0) > 0.95f && m.ProofTipAlpha(1) < 0.05f);
            m.ProofFactionPointer(new Vector2(r0.xMax - r0.width * 0.08f, r0.center.y)); yield return Real(1.6f);
            var rig = m.ProofRig;
            Check($"P5 the pointer at the poster's right edge: Zod's camera follows it, yaw {(rig ? rig.P[0].yaw : 0f):0.0} deg (over 5 wanted; the limit {OzFaction.YawLimit})", rig && rig.P[0].yaw > 5f && rig.P[0].yaw <= OzFaction.YawLimit + 0.01f);
            m.ProofFactionPointer(r0.center); yield return Real(1.2f);
            yield return Shot("hover_zod");
            m.ProofFactionPointer(new Vector2((r0.center.x + r1.center.x) * 0.5f, r0.center.y)); yield return Real(1.6f);
            Check($"P5 the pointer half way: mood {m.ProofMood:0.00}, wash alpha {m.ProofWashUnder.a:0.0000}, glow alpha {m.ProofGlow.a:0.0000}, tips {m.ProofTipAlpha(0):0.00} / {m.ProofTipAlpha(1):0.00}",
                  Mathf.Abs(m.ProofMood) < 0.05f && m.ProofWashUnder.a < 0.0005f && m.ProofGlow.a < 0.004f && m.ProofTipAlpha(0) < 0.05f && m.ProofTipAlpha(1) < 0.05f);   // k75a2: + the glow
            yield return Shot("pointer_middle");
            // the jump from Zod's poster to EvilZod's: the tint eases
            m.ProofFactionPointer(r0.center); yield return Real(1.6f);
            float prev = m.ProofMood, first = -1f, big = 0f; bool mono = true; float t0 = Time.realtimeSinceStartup, settle = -1f;
            m.ProofFactionPointer(r1.center);
            while (Time.realtimeSinceStartup - t0 < 1.8f)
            {
                yield return null;
                float v = m.ProofMood, step = v - prev; if (first < 0f) first = Mathf.Abs(step);
                big = Mathf.Max(big, Mathf.Abs(step)); if (step < -1e-4f) mono = false; prev = v;
                if (settle < 0f && v > 0.95f) settle = Time.realtimeSinceStartup - t0;
            }
            Check($"P5 the pointer jumps from Zod's poster to EvilZod's: the tint eases (the first frame {first:0.000}, the largest step {big:0.000}, only toward blue {mono}, blue in {settle:0.00} s)",
                  first < 0.1f && big < 0.3f && mono && settle > 0f && settle < 1.6f);
            w = m.ProofWashUnder; mood = m.ProofMood;
            gw = m.ProofGlow; gr = m.ProofGlowRt ? ScreenRect(m.ProofGlowRt) : new Rect();                     // k75a2: the glow moved to EvilZod's poster
            bool glowBlue = gw.b > gw.r && Mathf.Abs(gw.a - OzFaction.Glow[OzFaction.Blue] * mood) < 0.005f && Vector2.Distance(gr.center, r1.center) < 3f;
            Check($"P5 the pointer over EvilZod's poster: mood {mood:0.00}, the wash blue ({w.r:0.00}, {w.g:0.00}, {w.b:0.00}, alpha {w.a:0.000}), the glow alpha {gw.a:0.000} centred on his poster {glowBlue}, the tip \"{Plain(m.ProofTipText(1))}\" shown {m.ProofTipAlpha(1):0.00}, none under Zod's {m.ProofTipAlpha(0):0.00}",
                  mood > 0.95f && w.b > w.r && glowBlue && Plain(m.ProofTipText(1)) == "TIP: You chose... poorly." && m.ProofTipAlpha(1) > 0.95f && m.ProofTipAlpha(0) < 0.05f);
            yield return Shot("hover_evilzod");
        }

        IEnumerator KeysFocus(OpenZMainMenu m)
        {
            m.ProofFactionPointer(null);
            m.ProofFactionKeys(true, false, false, false); yield return Real(1.2f);
            bool left = m.ProofActive == OzFaction.Red && m.ProofTipAlpha(0) > 0.95f && m.ProofMood < -0.9f;
            float l0 = LogNow;
            m.ProofFactionKeys(false, true, false, false); yield return Real(1.2f);
            bool right = m.ProofActive == OzFaction.Blue && m.ProofTipAlpha(1) > 0.95f && m.ProofTipAlpha(0) < 0.05f && m.ProofMood > 0.9f;
            yield return Shot("keys_focus_evilzod");
            float l1 = LogNow;
            m.ProofFactionKeys(false, true, false, false); yield return Real(0.4f);
            Check($"P6 keys (the gamepad's D-pad sends them): LEFT focuses Zod (tip, red) {left}, RIGHT EvilZod (tip, blue) {right} with the hover sound ({Sounds(l0, "ui_hover")} x), RIGHT again answers ui_disabled ({Sounds(l1, "ui_disabled")} x)",
                  left && right && Sounds(l0, "ui_hover") >= 1 && Sounds(l1, "ui_disabled") >= 1);
        }

        IEnumerator PickRed(OpenZMainMenu m, Light sunBefore, float alphaBefore)
        {
            var r0 = ScreenRect(m.ProofPoster(0));
            m.ProofFactionPointer(r0.center); yield return Real(0.8f);
            float l0 = LogNow, t0 = Time.realtimeSinceStartup; int voices = OpenZMainMenu.FactionVoices, picks = OzFaction.Picks, unl = OzFactionPosters.Unloaded;
            OzFaction.ProofForgetLine();                                                                     // 1.3 item 6: P7 is a first pick (the line and its hold)
            m.ProofFactionClick(OzFaction.Red);
            var src = m.ProofFactionVoice;
            bool playing = src && src.isPlaying && src.clip && src.clip.name == "zod_pick_red";
            float len = src && src.clip ? src.clip.length : 0f;
            yield return null; yield return null;
            Check($"P7 the pick GENERAL ZOD (a click): Zod's line {OpenZMainMenu.FactionClip} playing {playing} ({len:0.00} s, volume {OpenZMainMenu.FactionVolume:0.00}), the stamp ({Sounds(l0, "ui_stamp")} x), the side {(OzPlayBlue.Box ? "BLUE" : "RED")} saved, the words \"{Plain(m.ProofTipText(0))}\"",
                  playing && OpenZMainMenu.FactionVoices == voices + 1 && OzFaction.Picks == picks + 1 && Sounds(l0, "ui_stamp") >= 1 && !OzPlayBlue.Box && Plain(m.ProofTipText(0)) == "ZOD: Wise choice!" && OpenZMainMenu.FactionVolume > 0.05f);
            yield return Real(0.25f);
            yield return Shot("pick_zod");
            yield return Until(() => m.ProofView == "Select", len + 3f);
            float held = Time.realtimeSinceStartup - t0;
            yield return Real(0.8f);
            string tag = m.ProofSideTag, card = Plain(m.ProofCardText); var tc = m.ProofSideTagColor;
            Check($"P7 the screen held for the line ({held:0.00} s, the line {len:0.00} s), then the level selection: its tag line \"{tag}\" (red {tc.r > tc.b}), the card \"{card}\", not blue {!OzPlayBlue.BoxOn}",
                  m.ProofView == "Select" && held >= len && held < len + 1.5f && tag == "GENERAL ZOD" && tc.r > tc.b && card.Contains("BLUE FORT") && !OzPlayBlue.BoxOn);
            yield return Until(() => OzFactionPosters.Unloaded > unl, 4f);
            string left = Leftovers(out int n);
            Check($"P7 the posters unloaded when the screen was left: closed {OzFactionPosters.Unloaded - unl} x, {OzFactionPosters.LastUnloadCount} assets unloaded; left in memory: {left}; the scene's sun back {RenderSettings.sun == sunBefore}, _AtmoKeepAlpha back {Mathf.Approximately(Shader.GetGlobalFloat("_AtmoKeepAlpha"), alphaBefore)}",
                  OzFactionPosters.Unloaded > unl && !OzFactionPosters.I && OzFactionPosters.LastUnloadCount >= 20 && n == 0 && RenderSettings.sun == sunBefore && Mathf.Approximately(Shader.GetGlobalFloat("_AtmoKeepAlpha"), alphaBefore));
            yield return Shot("select_zod");
        }

        IEnumerator BackAndBlue()
        {
            var m = Menu(); int backs = OpenZMainMenu.FactionBacks;
            m.ProofBackFromSelect();
            yield return Until(() => m.ProofFactionShown, 2f);
            yield return Until(() => m.ProofRig && m.ProofRig.Ready && m.ProofPrintAlpha(1) > 0.99f, 12f);
            Check($"P8 the level selection's BACK: the faction screen again (view {m.ProofView}, {OpenZMainMenu.FactionBacks - backs} back), the posters loaded again {(m.ProofRig && m.ProofRig.Ready)}",
                  m.ProofView == "Faction" && OpenZMainMenu.FactionBacks == backs + 1 && m.ProofRig && m.ProofRig.Ready);
            yield return Real(0.5f);
            m.ProofFactionKeys(false, true, false, false); yield return Real(0.5f);
            float t0 = Time.realtimeSinceStartup;
            OzFaction.ProofForgetLine();                                                                     // 1.3 item 6: P9 hears EvilZod's side's line as a first pick too
            m.ProofFactionKeys(false, false, true, false);
            var src = m.ProofFactionVoice;
            bool playing = src && src.isPlaying && src.clip && src.clip.name == "zod_pick_blue";
            float len = src && src.clip ? src.clip.length : 0f;
            yield return Real(0.3f);
            yield return Shot("pick_evilzod");
            Check($"P9 the pick GENERAL EVILZOD (RIGHT, ENTER): Zod's line {OpenZMainMenu.FactionClip} playing {playing} ({len:0.00} s), the words \"{Plain(m.ProofTipText(1))}\", the side {(OzPlayBlue.Box ? "BLUE" : "RED")} saved",
                  playing && OzPlayBlue.Box && Plain(m.ProofTipText(1)) == "ZOD: You chose... poorly.");
            yield return Until(() => m.ProofView == "Select", len + 3f);
            yield return Real(0.8f);
            string tag = m.ProofSideTag, card = Plain(m.ProofCardText); var tc = m.ProofSideTagColor;
            Check($"P9 the level selection of the blue side ({Time.realtimeSinceStartup - t0:0.00} s after the pick): its tag line \"{tag}\" (blue {tc.b > tc.r}), the card \"{card}\", the box's setting on {OzPlayBlue.BoxOn}",
                  m.ProofView == "Select" && tag == "GENERAL EVILZOD" && tc.b > tc.r && card.Contains("RED FORT") && OzPlayBlue.BoxOn);
            yield return Shot("select_evilzod");
        }

        IEnumerator BlueLevel()
        {
            var m = Menu(); int card = System.Array.FindIndex(OzLevelDef.All, d => d.Id == "L02");
            m.ProofFocus(card); yield return Real(0.8f);
            levelLoadedAt = -1f;
            m.ProofDeploy();
            Log("P10 DEPLOY level 2 as the player does (the blue side picked on the faction screen)");
            yield return Until(() => levelLoadedAt > 0f && ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 120f);
            yield return Real(4f);
            var g = ZGame.I; var lv = OzPlayBlueLevel.I;
            string left = Leftovers(out int n);
            bool rigGone = !OzFactionPosters.I && !FindFirstObjectByType<OzFactionPosters>();
            Check($"P10 level 2 plays the blue mode as the old box did: PLAY AS BLUE active {OzPlayBlue.Active} ({OzPlayBlue.ActiveLevel}), the human {(g ? g.player.ToString() : "-")}, the camera over the blue fort {(lv && lv.Placed)}",
                  OzPlayBlue.Active && OzPlayBlue.ActiveLevel == "L02" && g && g.player == Team.Blue && lv && lv.Placed);
            Check($"P10 nothing of the faction screen in the level: its rig {(rigGone ? "gone" : "STILL THERE")}, left in memory: {left}", rigGone && n == 0);
            yield return Shot("L02_blue_start");
            OzScenes.MainMenu();
            yield return Until(() => InMenu, 60f);
            yield return Real(2f);
        }

        IEnumerator BackToMain(long memBefore)
        {
            yield return OpenByPlay("P11");
            var m = Menu();
            long memOn = (long)Texture.currentTextureMemory;
            int unl = OzFactionPosters.Unloaded;
            m.ProofFactionBack();
            yield return Until(() => m.ProofView == "Main", 1.5f);
            yield return Until(() => OzFactionPosters.Unloaded > unl, 6f);
            yield return Real(1.5f);
            long memAfter = (long)Texture.currentTextureMemory;
            string left = Leftovers(out int n);
            Check($"P11 BACK on the faction screen: the main menu (view {m.ProofView}); the posters closed and swept ({OzFactionPosters.LastUnloadCount} assets); left in memory: {left}; "
                  + $"texture memory {memOn / 1048576f:0} MB on the screen -> {memAfter / 1048576f:0} MB after (the first menu: {memBefore / 1048576f:0} MB)",
                  m.ProofView == "Main" && OzFactionPosters.Unloaded > unl && n == 0 && memAfter < memOn);
        }

        IEnumerator ZodExact()
        {
            PlayerPrefs.SetInt("oz_zod_exact", 1);
            yield return OpenByPlay("P12");
            var m = Menu(); bool box = OzPlayBlue.Box; int locked = OpenZMainMenu.FactionLocked; float l0 = LogNow;
            var lockRt = m.ProofPoster(1) ? m.ProofPoster(1).Find("Locked") : null;
            m.ProofFactionClick(OzFaction.Blue); yield return Real(0.6f);
            Check($"P12 'Zod exact': the EvilZod poster locked (shade {(lockRt && lockRt.gameObject.activeInHierarchy)}), a click answers ui_disabled ({Sounds(l0, "ui_disabled")} x), the screen stays ({m.ProofView}), the side unchanged",
                  lockRt && lockRt.gameObject.activeInHierarchy && OpenZMainMenu.FactionLocked == locked + 1 && Sounds(l0, "ui_disabled") >= 1 && m.ProofView == "Faction" && OzPlayBlue.Box == box);
            yield return Shot("zod_exact_locked");
            if (hadExact) PlayerPrefs.SetInt("oz_zod_exact", exactWas); else PlayerPrefs.DeleteKey("oz_zod_exact");
            m.ProofFactionBack(); yield return Real(1.5f);
        }

        IEnumerator Small()
        {
            yield return SetSize(1366, 768);
            yield return OpenByPlay("P13");
            var m = Menu();
            var s = new Rect(0, 0, Screen.width, Screen.height);
            var r0 = ScreenRect(m.ProofPoster(0)); var r1 = ScreenRect(m.ProofPoster(1)); var t0 = ScreenRect(m.ProofTipRt(0)); var t1 = ScreenRect(m.ProofTipRt(1));
            var back = m.ProofFactionBackRt ? ScreenRect(m.ProofFactionBackRt) : new Rect();
            bool inside = s.Contains(r0.min) && s.Contains(r0.max) && s.Contains(r1.min) && s.Contains(r1.max) && t0.yMin > 0f && t1.yMin > 0f;
            bool apart = !r0.Overlaps(r1) && !back.Overlaps(r0) && !back.Overlaps(r1) && !t0.Overlaps(r0) && !t1.Overlaps(r1);
            float shrink = 1f;
            for (int i = 0; i < 2; i++)
                foreach (var g in new[] { true, false })
                {
                    var t = m.ProofTitle(i, g); if (t == null) continue;
                    var tmp = t.graphic as TMPro.TMP_Text; if (tmp) shrink = Mathf.Min(shrink, tmp.fontSize / Mathf.Max(1f, t.Size));
                }
            Check($"P13 1366 x 768: both posters inside the screen {inside} (Zod {r0.xMin:0}..{r0.xMax:0}, EvilZod {r1.xMin:0}..{r1.xMax:0} px), apart from each other, the BACK button and their tips {apart}, the titles at {shrink * 100f:0} % of their size (85 % at least)",
                  inside && apart && shrink >= 0.85f);
            yield return Shot("faction");
            m.ProofFactionPointer(r0.center); yield return Real(1.6f);
            yield return Shot("hover_zod");
            m.ProofFactionPointer(null);
            m.ProofFactionBack(); yield return Real(1.5f);
            yield return SetSize(1920, 1080);
        }

        IEnumerator Pictures(OpenZMainMenu m)
        {
            var r0 = ScreenRect(m.ProofPoster(0)); var r1 = ScreenRect(m.ProofPoster(1));
            m.ProofFactionPointer(new Vector2(4f, 4f)); yield return Real(1.2f);
            for (int k = 1; k <= 6; k++) { yield return Shot("sway_" + k); yield return Real(OzFaction.SwayPeriod / 6f - 0.2f); }
            m.ProofFactionPointer(r0.center); yield return Real(1.6f); yield return Shot("hover_zod");
            m.ProofFactionPointer(r1.center); yield return Real(1.6f); yield return Shot("hover_evilzod");
            m.ProofFactionPointer(null); m.ProofFactionKeys(true, false, false, false); yield return Real(1.2f); yield return Shot("keys_focus_zod");
            var rig = m.ProofRig;
            if (rig) { yield return new WaitForEndOfFrame(); for (int i = 0; i < 2; i++) { var px = ReadBack(rig.P[i].rt); if (px != null) SavePng(px, rig.P[i].rt.width, rig.P[i].rt.height, "render_" + rig.P[i].id + "_" + Screen.width + "x" + Screen.height); } }
            Check("pictures taken at " + Screen.width + " x " + Screen.height, true);
        }
    }
}
#endif
