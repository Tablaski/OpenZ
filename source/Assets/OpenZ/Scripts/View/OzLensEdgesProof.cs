#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2 Play proof of the CHROMATIC ABERRATION · LENS EDGES switch (View/OzLensEdges.cs; menus OpenZ/V1.2/Graphics: 39 Lens Edges
    /// Proof (Play) and 39b Lens Edges Menus Check (Play), Editor/OzLensEdgesMenu.cs). Started in a running level (L02 is loaded first when
    /// another one runs). Counted checks: OFF on a fresh setting; the GRAPHICS tab of the pause menu (7 rows, the row under MOTION BLUR,
    /// inside the plate, its tip) switched ON / OFF / ON by real clicks with the menu's toggle sounds, saved, drawn on the next frame;
    /// the full-screen picture sets at the normal 70 m RTS camera on L02 and L16 (OFF and ON at 0.08 / 0.12 / 0.16 / 0.25, with the HUD
    /// and without it, the clock and the battle held for the set), each value drawn as set, the colour change of the middle third against
    /// the corners; max with a test volume (0.80, half weight, 0.10, removed, the switch off), a nuke flash; a level restart with the
    /// level-start flyover forced (the switch survives, the flyover and the start vignette drawn with it, back to the base after the
    /// vignette); the main menu's GRAPHICS tab (rows, place, tip, two real clicks); the Unit editor (drawn with it, OFF and ON live);
    /// 0 console errors. The player's own setting is put back at the end. Lines and pictures in <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzLensEdgesProof : MonoBehaviour
    {
        public static string Report = "idle";
        /// <summary>The intensities of the picture sets (the base among them).</summary>
        public static readonly float[] Steps = { 0.08f, 0.12f, 0.16f, 0.25f };                              // 1.2 item 50: around the new base 0.12 (0.25 = the first base)
        const string MotionBlurLabel = "MOTION BLUR · FAST DEBRIS AND SHELLS";
        static readonly string[] GraphicsRows = { OzQuality.Label, OzFpsCap.Label, NightShadows.Label, OzNightRain.Label, MotionBlurLabel, OzLensEdges.Label, OzQuality.FpsLabel,
                                                  OzSmoothFrames.Label, OzSunSteps.Label, OzEndingDebris.Label, OzGfxApi.Label, OzMassLod.Label,
                                                  OzLightRows.LightestLabel, OzLightRows.ShadowsLabel, OzLightRows.AoLabel, OzLightRows.TreesLabel, OzLightRows.WrecksLabel, OzLowPicture.Label };   // v1.3 item 25 (GRAPHICS API: Windows only, as the proof runs); 1.5 issue 8: 3D PICTURE last

        /// <summary></summary>
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var kit = System.IO.Path.Combine(stage, "k39_chromatic_aberration");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k39_chromatic_aberration");
        }

        bool menusOnly;

        /// <summary>menusOnly: only the two menus' rows and clicks (for a second window size), no pictures, no level changes but the main menu.</summary>
        public static void Run(bool menusOnly)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzLensEdgesProof] enter Play first (a level, L02)"); return; }
            if (FindFirstObjectByType<OzLensEdgesProof>()) { Debug.LogWarning("[OzLensEdgesProof] already running"); return; }
            if (!menusOnly && (ZGame.I == null || ZGame.I.World == null)) { Debug.LogWarning("[OzLensEdgesProof] start a level (L02) in Play first"); return; }
            Report = "running";
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);
            var go = new GameObject("OzLensEdgesProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzLensEdgesProof>().menusOnly = menusOnly;
        }

        // ------------------------------------------------------------------------------------------------ the proof mouse (as MinimalUiProof)
        static Mouse proofMouse, realMouse;
        void ProofMouseOn()
        {
            realMouse = Mouse.current;
            proofMouse = InputSystem.AddDevice<Mouse>("OzProofMouse");
            if (realMouse != null) InputSystem.DisableDevice(realMouse);
            proofMouse.MakeCurrent();
        }
        static void ProofMouseOff()
        {
            if (realMouse != null) { InputSystem.EnableDevice(realMouse); realMouse.MakeCurrent(); realMouse = null; }
            if (proofMouse != null) InputSystem.RemoveDevice(proofMouse);
            proofMouse = null;
        }
        static void MouseAt(Vector2 p, bool down = false)
        {
            var st = new MouseState { position = p }; if (down) st = st.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(proofMouse != null ? proofMouse : Mouse.current, st);
        }
        IEnumerator Click(Vector2 p)
        {
            MouseAt(p); yield return null; yield return null;
            MouseAt(p, true); yield return null; yield return null;
            MouseAt(p, false); yield return null; yield return null;
        }
        void Park() => MouseAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f));

        // ------------------------------------------------------------------------------------------------ book-keeping
        readonly StringBuilder sb = new StringBuilder();
        readonly List<string> pictures = new List<string>();
        int fails, passes, errors; string dir;
        bool chromaHad, briefHad, testHad; int chromaVal, briefVal, testVal;
        static float stackVignette = -1f, stackExposure;

        void Log(string s) { sb.AppendLine(s); Debug.Log("[OzLensEdgesProof] " + s); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static bool Near(float a, float b, float tol = 0.002f) => Mathf.Abs(a - b) <= tol;
        static string F3(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        static string Tag(float v) => v <= 0f ? "off" : Mathf.RoundToInt(v * 100f).ToString("000");
        static object F(object o, string n) { if (o == null) return null; var t = o.GetType(); while (t != null) { var f = t.GetField(n, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance); if (f != null) return f.GetValue(o); t = t.BaseType; } return null; }
        static Vector2 Mid(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return (c[0] + c[2]) * 0.5f; }
        static Rect ScreenRect(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
        static string Sounds(float t0) => string.Join(", ", OzSoundLog.Since(t0).Select(e => e.key + " " + e.outcome));
        static bool Heard(float t0, string key) => OzSoundLog.Since(t0).Any(e => e.key != null && e.key.Contains(key) && e.outcome != null && e.outcome.StartsWith("PLAY"));
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        /// <summary>Waits until a player-view camera was drawn twice after this call (the switch's value is on the screen).</summary>
        static IEnumerator Drawn()
        {
            int f = Time.frameCount; float t = Time.realtimeSinceStartup + 3f;
            while (OzLensEdges.SeenFrame <= f + 1 && Time.realtimeSinceStartup < t) yield return null;
        }

        void Start()
        {
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            chromaHad = PlayerPrefs.HasKey(OzLensEdges.Pref); chromaVal = PlayerPrefs.GetInt(OzLensEdges.Pref, 0);
            briefHad = PlayerPrefs.HasKey(ZodBriefing.PrefBriefing); briefVal = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1);
            testHad = PlayerPrefs.HasKey("oz_testmode"); testVal = PlayerPrefs.GetInt("oz_testmode", 0);
            StartCoroutine(All());
        }

        void OnDestroy()
        {
            ProofMouseOff();
            Application.logMessageReceived -= OnLog;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        void OnLog(string msg, string st, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) { errors++; if (errors < 8) sb.AppendLine("CONSOLE " + type + ": " + msg); }
        }

        /// <summary>The vignette and exposure the player-view camera was drawn with (for the start vignette and the nuke flash).</summary>
        static void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!cam || cam.cameraType != CameraType.Game || !(cam.targetTexture == null || cam.CompareTag("MainCamera"))) return;
            var st = VolumeManager.instance.stack; if (st == null) return;
            var vg = st.GetComponent<Vignette>(); var ca = st.GetComponent<ColorAdjustments>();
            stackVignette = vg != null ? vg.intensity.value : 0f; stackExposure = ca != null ? ca.postExposure.value : 0f;
        }

        void RestorePrefs()
        {
            if (chromaHad) PlayerPrefs.SetInt(OzLensEdges.Pref, chromaVal); else PlayerPrefs.DeleteKey(OzLensEdges.Pref);
            if (briefHad) PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, briefVal); else PlayerPrefs.DeleteKey(ZodBriefing.PrefBriefing);
            if (testHad) PlayerPrefs.SetInt("oz_testmode", testVal); else PlayerPrefs.DeleteKey("oz_testmode");
            PlayerPrefs.Save();
            OzLensEdges.Base = OzLensEdges.DefaultBase; OzLensEdges.Reload();
        }

        IEnumerator Shot(string name)
        {
            string p = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p); pictures.Add(p); Log("   picture " + name + ".png");
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        // ------------------------------------------------------------------------------------------------ the run
        IEnumerator All()
        {
            yield return Real(0.5f);
            Log($"1.2 item 39 lens edges proof ({(menusOnly ? "menus only" : "full")}), scene {SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, saved oz_chroma {(chromaHad ? chromaVal.ToString() : "none")}, test mode {GameBoot.TestMode}");
            Log("   " + OzLensEdges.Describe());
            ProofMouseOn(); Park();
            bool inLevel = ZGame.I != null && ZGame.I.World != null;
            if (!menusOnly && SceneManager.GetActiveScene().name != "L02_Desert") { SceneManager.LoadScene("L02_Desert"); inLevel = true; }
            Table();
            if (inLevel) yield return LevelReady(menusOnly ? SceneManager.GetActiveScene().name : "L02_Desert", 90f);
            if (!menusOnly)
            {
                yield return FreshDefault();
                yield return PauseRow();
                yield return Pictures("L02");
                yield return Combine();
                yield return RestartWithFlyover();
                SceneManager.LoadScene("L16_Jungle");
                yield return LevelReady("L16_Jungle", 180f);
                yield return Drawn();
                Check($"L16 (loaded with the switch left ON): still ON, drawn at the base ({OzLensEdges.Describe()})", OzLensEdges.On && Near(OzLensEdges.Seen, OzLensEdges.Base));
                yield return Pictures("L16");
            }
            else if (inLevel) yield return PauseRow();
            yield return MainMenuRow(!menusOnly);
            if (!menusOnly) yield return UnitEditor();
            Finish();
        }

        IEnumerator LevelReady(string scene, float max)
        {
            float w0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != scene || ZGame.I == null || ZGame.I.World == null || OpenZHud.I == null) && Time.realtimeSinceStartup - w0 < max) yield return null;
            while (ZodBriefing.BriefingActive && Time.realtimeSinceStartup - w0 < max + 120f) yield return null;
            yield return Real(4f);                                                                         // the HUD entrance and the mission banner end
            Log($"{scene} ready after {Time.realtimeSinceStartup - w0:0.0} s (level {(ZGame.I ? ZGame.I.level : "-")})");
        }

        /// <summary>The options table: the row's tab, its place under MOTION BLUR, its tip.</summary>
        void Table()
        {
            int t = OzSettingTips.TabOf(OzLensEdges.Label), o = OzSettingTips.TabOrder(OzLensEdges.Label);
            Check($"the options table: the row is on GRAPHICS ({t}), right under MOTION BLUR and over FPS COUNTER (places {OzSettingTips.TabOrder(MotionBlurLabel)} / {o} / {OzSettingTips.TabOrder(OzQuality.FpsLabel)}), its tip is the switch's",
                  t == OzSettingTips.TabGraphics && o == OzSettingTips.TabOrder(MotionBlurLabel) + 1 && OzSettingTips.TabOrder(OzQuality.FpsLabel) == o + 1 && OzSettingTips.For(OzLensEdges.Label) == OzLensEdges.Tip);
            // a player build keeps the effect's shader variant only while a VolumeProfile asset of the project holds a ChromaticAberration
            // component (URP strips the post-processing variants no profile uses): Settings/DefaultVolumeProfile.asset does today
            var holders = new List<string>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:VolumeProfile"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets")) continue;
                var vp = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (vp && vp.Has<ChromaticAberration>()) holders.Add(path);
            }
            Check($"the exe keeps the effect: a VolumeProfile asset holds a ChromaticAberration component ({(holders.Count > 0 ? string.Join(", ", holders) : "none")})", holders.Count > 0);
        }

        IEnumerator FreshDefault()
        {
            PlayerPrefs.DeleteKey(OzLensEdges.Pref); OzLensEdges.Reload();
            yield return Drawn();
            var v = OzLensEdges.Vol;
            Check($"a fresh setting (no oz_chroma) is OFF: the volume disabled, drawn {F3(OzLensEdges.Seen)} (the level's own); the switch object kept across scenes ({(OzLensEdges.I ? OzLensEdges.I.gameObject.scene.name : "missing")})",
                  !OzLensEdges.On && v && !v.enabled && OzLensEdges.Seen >= 0f && OzLensEdges.Seen < 0.001f && OzLensEdges.I && OzLensEdges.I.gameObject.scene.name == "DontDestroyOnLoad");
        }

        /// <summary>The rows of a menu's toggles list that are shown now, top first: (label, row).</summary>
        static List<(string label, HudButton row)> ShownRows(object menu)
        {
            var list = new List<(string, HudButton)>();
            if (!(F(menu, "toggles") is IList ts)) return list;
            foreach (var t in ts) { var row = F(t, "row") as HudButton; if (row != null && row.Rt && row.Rt.gameObject.activeInHierarchy) list.Add(((string)F(t, "label"), row)); }
            list.Sort((a, b) => Mid(b.Item2.Rt).y.CompareTo(Mid(a.Item2.Rt).y));
            return list;
        }

        void CheckRows(string where, List<(string label, HudButton row)> rows, RectTransform plate)
        {
            var labels = rows.Select(r => r.label).ToList();
            Log($"{where} GRAPHICS rows, top first: {string.Join(" | ", labels)}");
            Check($"{where}: GRAPHICS shows {labels.Count} rows in the table's order, the new row right under MOTION BLUR", labels.SequenceEqual(GraphicsRows));
            int i = labels.IndexOf(OzLensEdges.Label);
            if (i < 0 || !plate) { Check($"{where}: the row and its plate are there", false); return; }
            Rect r = ScreenRect(rows[i].row.Rt), p = ScreenRect(plate), last = ScreenRect(rows[rows.Count - 1].row.Rt);
            Log($"   row x {r.xMin:0}-{r.xMax:0} y {r.yMin:0}-{r.yMax:0} px; plate x {p.xMin:0}-{p.xMax:0} y {p.yMin:0}-{p.yMax:0} px; the last row ends {last.yMin - p.yMin:0} px above the plate's bottom");
            Check($"{where}: the new row and the tab's last row lie inside the plate", p.Contains(r.min) && p.Contains(r.max) && last.yMin >= p.yMin && last.xMax <= p.xMax);
        }

        IEnumerator PauseRow()
        {
            var hud = OpenZHud.I; var pause = hud ? hud.Pause : null;
            if (!pause) { Check("the pause menu is there", false); yield break; }
            pause.Open(); yield return Real(0.5f); pause.ProofOptTab(OzSettingTips.TabGraphics); yield return Real(0.9f);
            var rows = ShownRows(pause);
            CheckRows("pause menu", rows, F(pause, "sidePanel") as RectTransform);
            var mine = rows.FirstOrDefault(r => r.label == OzLensEdges.Label).row;
            if (mine == null) { pause.Close(); yield break; }
            Check("pause menu: the row's tip is the switch's (" + (mine.Tooltip ?? "none").Split('|')[0] + ")", mine.Tooltip == OzLensEdges.Tip);
            MouseAt(Mid(mine.Rt)); yield return Real(1.2f);
            if (!menusOnly) yield return Shot("k39_1_pause_graphics_row_tip");
            bool start = OzLensEdges.On;
            for (int k = 0; k < 3; k++)
            {
                bool want = k == 1 ? start : !start;
                float t0 = Time.unscaledTime;
                yield return Click(Mid(mine.Rt)); yield return Real(0.3f); yield return Drawn();
                int saved = PlayerPrefs.GetInt(OzLensEdges.Pref, -1);
                Log($"   click {k + 1}: {OzLensEdges.Describe()}, saved {saved}, sounds: {Sounds(t0)}");
                Check($"pause menu click {k + 1}: the switch {(want ? "ON" : "OFF")}, saved ({saved}), the menu's {(want ? "ui_toggle_on" : "ui_toggle_off")} sound, drawn at once in the paused battle ({F3(OzLensEdges.Seen)} on {(OzLensEdges.SeenCamera ? OzLensEdges.SeenCamera.name : "-")})",
                      OzLensEdges.On == want && saved == (want ? 1 : 0) && Heard(t0, want ? "toggle_on" : "toggle_off") && Near(OzLensEdges.Seen, want ? OzLensEdges.Base : 0f)
                      && OzLensEdges.SeenCamera && OzLensEdges.SeenCamera.GetComponent<RtsCamera>());
                yield return Real(0.3f);
            }
            if (!OzLensEdges.On) OzLensEdges.On = true;
            if (!menusOnly) yield return Shot("k39_2_pause_graphics_row_on");
            pause.Close(); Park(); yield return Real(0.8f);
        }

        // ------------------------------------------------------------------------------------------------ pictures
        IEnumerator Pictures(string lvl)
        {
            var g = ZGame.I; var rts = FindFirstObjectByType<RtsCamera>(); var hud = OpenZHud.I;
            var fort = g.World.Objects.FirstOrDefault(o => o.IsFort && o.Owner == g.player && !o.Destroyed);
            if (rts) { if (fort != null) { var c = g.CenterWorld(fort); rts.focus = new Vector3(c.x, 0f, c.z); } rts.SetHeightImmediate(70f); }
            var dn = FindFirstObjectByType<DayNight>(); bool dnRun = dn && dn.running; float dnT = dn ? dn.timeOfDay : 0f;
            if (dn) { dn.running = false; dn.timeOfDay = 0.10f; dn.Apply(0.10f); }
            float ts = Time.timeScale; Time.timeScale = 0f;                                                // the battle held: every picture of the set is the same moment
            Log($"{lvl} pictures: the normal RTS camera at 70 m over the player's fort ({(fort != null ? "#" + fort.Id : "none")}), daylight held (0.10), the battle held");
            yield return Real(1.2f);
            var tags = new List<string>();
            for (int pass = 0; pass < 2; pass++)
            {
                bool clean = pass == 1; bool hudWas = hud && hud.hudVisible;
                if (clean && hud && hudWas) hud.SetVisible(false);
                yield return Real(0.3f);
                foreach (float v in new[] { 0f }.Concat(Steps))
                {
                    if (v <= 0f) OzLensEdges.On = false; else { OzLensEdges.Base = v; OzLensEdges.On = true; }
                    yield return Drawn(); yield return null;
                    Check($"{lvl} {(clean ? "without the HUD" : "with the HUD")}: {(v <= 0f ? "OFF" : "ON at " + v.ToString("0.00"))} is drawn as set ({F3(OzLensEdges.Seen)})", Near(OzLensEdges.Seen, Mathf.Max(0f, v)));
                    yield return Shot($"k39_{lvl}_h70_{(clean ? "clean_" : "")}{Tag(v)}");
                }
                if (clean) { OzLensEdges.On = false; yield return Drawn(); yield return Shot($"k39_{lvl}_h70_clean_off_twin"); }
                if (clean && hud && hudWas) hud.SetVisible(true);
            }
            OzLensEdges.Base = OzLensEdges.DefaultBase; OzLensEdges.On = true;
            Time.timeScale = ts > 0f ? ts : 1f;
            if (dn) { dn.timeOfDay = dnT; dn.Apply(dnT); dn.running = dnRun; }
            yield return Real(0.3f);
            // 2x crops (a quarter of the picture at its own pixels): the top-left corner and the centre, OFF beside the base and 0.25 (the first base)
            string P(string n) => System.IO.Path.Combine(dir, n);
            int W = Screen.width, H = Screen.height;
            foreach (var k in new[] { Tag(OzLensEdges.DefaultBase), "025" }.Distinct())
                foreach (var (name, centre) in new[] { ("corner", new Vector2(W * 0.25f, H * 0.75f)), ("centre", new Vector2(W * 0.5f, H * 0.5f)) })
                {
                    string a = P($"k39_{lvl}_crop2x_{name}_off.jpg"), b = P($"k39_{lvl}_crop2x_{name}_{k}.jpg"), pair = P($"k39_{lvl}_crop2x_{name}_off_vs_{k}.jpg");
                    bool ok = OzProofPictures.Crop(P($"k39_{lvl}_h70_clean_off.png"), centre, W / 4, H / 4, a) && OzProofPictures.Crop(P($"k39_{lvl}_h70_clean_{k}.png"), centre, W / 4, H / 4, b)
                              && OzProofPictures.Pair(a, b, pair, H / 2);
                    Log($"   crop {System.IO.Path.GetFileName(pair)}: {(ok ? "written" : "FAILED")}");
                }
            OzProofPictures.Sheet(new[] { "off" }.Concat(Steps.Select(Tag)).Select(t => P($"k39_{lvl}_h70_clean_{t}.png")).ToList(), 3, 640, P($"k39_{lvl}_sheet_clean_off_{string.Join("_", Steps.Select(Tag))}.jpg"));
            OzProofPictures.Sheet(new[] { "off" }.Concat(Steps.Select(Tag)).Select(t => P($"k39_{lvl}_h70_{t}.png")).ToList(), 3, 640, P($"k39_{lvl}_sheet_hud_off_{string.Join("_", Steps.Select(Tag))}.jpg"));   // 1.2 item 50: the sets of Steps
            // the colour change against OFF (mean |green| + |blue| change, 0..510): the middle third against the four corner sixths
            var off = Load(P($"k39_{lvl}_h70_clean_off.png")); var twin = Load(P($"k39_{lvl}_h70_clean_off_twin.png"));
            var mids = new List<float>(); var cors = new List<float>();
            if (off != null && twin != null)
            {
                var (nm, nc) = Change(off, twin);
                Log($"   noise (OFF against its twin a moment later): middle third {nm:0.00}, corners {nc:0.00}");
                foreach (float v in Steps)
                {
                    var on = Load(P($"k39_{lvl}_h70_clean_{Tag(v)}.png")); if (on == null) continue;
                    var (m, c) = Change(off, on); mids.Add(m); cors.Add(c);
                    Log($"   {v:0.00}: colour change, middle third {m:0.00}, corners {c:0.00}");
                    Object.Destroy(on);
                }
            }
            if (off) Object.Destroy(off); if (twin) Object.Destroy(twin);
            int bi = System.Array.IndexOf(Steps, OzLensEdges.DefaultBase);
            bool rising = cors.Count == Steps.Length && cors.Zip(cors.Skip(1), (a, b) => b > a).All(x => x);
            Check($"{lvl}: the fringe sits at the edges and grows with the value: corners {(cors.Count > bi && bi >= 0 ? cors[bi].ToString("0.00") : "-")} against the middle third {(mids.Count > bi && bi >= 0 ? mids[bi].ToString("0.00") : "-")} at the base (at least 4 x), the corners rise {string.Join(" < ", Steps.Select(s => s.ToString("0.00")))}",
                  bi >= 0 && cors.Count == Steps.Length && cors[bi] >= 4f * Mathf.Max(0.05f, mids[bi]) && rising);
            var dups = OzProofPictures.Duplicates(pictures.Where(p => System.IO.Path.GetFileName(p).StartsWith($"k39_{lvl}_h70_") && !p.EndsWith("_twin.png")));
            Check($"{lvl}: no two pictures of the set are the same file{(dups.Count > 0 ? " (" + string.Join(", ", dups) + ")" : "")}", dups.Count == 0);
        }

        static Texture2D Load(string path)
        {
            try { if (!System.IO.File.Exists(path)) return null; var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); return t.LoadImage(System.IO.File.ReadAllBytes(path)) ? t : null; }
            catch { return null; }
        }

        static (float mid, float corners) Change(Texture2D a, Texture2D b)
        {
            if (a.width != b.width || a.height != b.height) return (-1f, -1f);
            var A = a.GetPixels32(); var B = b.GetPixels32(); int w = a.width, h = a.height;
            double sm = 0, sc = 0; long nm = 0, nc = 0;
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    bool mid = u > 1f / 3f && u < 2f / 3f && v > 1f / 3f && v < 2f / 3f, cor = (u < 1f / 6f || u > 5f / 6f) && (v < 1f / 6f || v > 5f / 6f);
                    if (!mid && !cor) continue;
                    int i = y * w + x; float d = Mathf.Abs(A[i].g - B[i].g) + Mathf.Abs(A[i].b - B[i].b);
                    if (mid) { sm += d; nm++; } else { sc += d; nc++; }
                }
            return ((float)(sm / Mathf.Max(1, nm)), (float)(sc / Mathf.Max(1, nc)));
        }

        // ------------------------------------------------------------------------------------------------ max with the other effects
        IEnumerator Combine()
        {
            float b = OzLensEdges.Base;
            OzLensEdges.On = true; yield return Drawn();
            var go = new GameObject("OzLensEdgesProof_TestVolume") { layer = 0 };
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.hideFlags = HideFlags.DontSave;
            var tc = prof.Add<ChromaticAberration>(false); tc.intensity.Override(0.8f);
            var tv = go.AddComponent<Volume>(); tv.isGlobal = true; tv.priority = 60f; tv.weight = 1f; tv.sharedProfile = prof;
            yield return Real(OzLensEdges.Rescan + 0.25f); yield return Drawn();
            Check($"max: an effect volume asking 0.80 (priority 60, weight 1) wins over the base {b:0.00}: drawn {F3(OzLensEdges.Seen)}, the others {F3(OzLensEdges.Others)}, {OzLensEdges.Users} effect volume(s) found", Near(OzLensEdges.Seen, 0.8f) && OzLensEdges.Users >= 1);
            tv.weight = 0.5f; yield return Drawn();
            Check($"max: the same at half weight gives 0.40, still over the base: drawn {F3(OzLensEdges.Seen)}", Near(OzLensEdges.Seen, Mathf.Max(b, 0.4f)));
            tv.weight = 1f; tc.intensity.Override(0.1f); yield return Drawn();
            Check($"max: an effect asking 0.10 does not pull the base down: drawn {F3(OzLensEdges.Seen)}", Near(OzLensEdges.Seen, b));
            tc.intensity.Override(0.8f); yield return Drawn();
            Destroy(go); yield return null; yield return Drawn();
            Check($"the effect's end falls back to the base, not to 0: drawn {F3(OzLensEdges.Seen)} two frames after its volume went", Near(OzLensEdges.Seen, b));
            OzLensEdges.On = false;
            var go2 = new GameObject("OzLensEdgesProof_TestVolume2") { layer = 0 };
            var tv2 = go2.AddComponent<Volume>(); tv2.isGlobal = true; tv2.priority = 60f; tv2.weight = 1f; tv2.sharedProfile = prof;
            yield return Real(0.2f); yield return Drawn();
            float offWith = OzLensEdges.Seen;
            Destroy(go2); yield return null; yield return Drawn();
            Check($"switch OFF: the effect keeps its own value ({F3(offWith)}) and its end gives the level's own ({F3(OzLensEdges.Seen)})", Near(offWith, 0.8f) && Near(OzLensEdges.Seen, 0f));
            Destroy(tc); Destroy(prof);
            OzLensEdges.On = true; yield return Drawn();
            // the nuke flash (the exposure + bloom flash of a nuke strike)
            yield return Real(0.5f);
            float e0 = stackExposure, eMax = e0, sMin = 9f, sMax = -9f; int frames = 0;
            OpenZBloomDirector.Flash(1f);
            float t0 = Time.realtimeSinceStartup; bool shot = false;
            while (Time.realtimeSinceStartup - t0 < 1.5f)
            {
                yield return null;
                eMax = Mathf.Max(eMax, stackExposure); sMin = Mathf.Min(sMin, OzLensEdges.Seen); sMax = Mathf.Max(sMax, OzLensEdges.Seen); frames++;
                if (!shot && Time.realtimeSinceStartup - t0 > 0.08f) { shot = true; yield return Shot("k39_L02_nuke_flash_on" + Tag(b)); }
            }
            Log($"   nuke flash: exposure {e0:0.00} -> peak {eMax:0.00}, chromatic aberration {F3(sMin)} .. {F3(sMax)} over {frames} frames (calm mode {OzMotion.Reduced})");
            Check($"a nuke flash: the flash is drawn (exposure up {eMax - e0:0.00}) and the lens edges stay at the base the whole time ({F3(sMin)} .. {F3(sMax)})",
                  (eMax - e0 > 0.3f || OzMotion.Reduced) && Near(sMin, b) && Near(sMax, b));
            yield return Real(3f); yield return Drawn();
            Check($"after the flash: exposure back ({stackExposure:0.00} against {e0:0.00}), the lens edges at the base ({F3(OzLensEdges.Seen)})", Mathf.Abs(stackExposure - e0) < 0.15f && Near(OzLensEdges.Seen, b));
        }

        // ------------------------------------------------------------------------------------------------ a restart with the level-start flyover
        IEnumerator RestartWithFlyover()
        {
            OzLensEdges.On = true;
            var keep = OzLensEdges.I; var oldGame = ZGame.I;
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 2); PlayerPrefs.SetInt("oz_testmode", 0); PlayerPrefs.Save();
            Log("restart: oz_zod_briefing 2 (forces the briefing flyover), test mode off for this start (both put back afterwards)");
            OzScenes.Restart();
            float w0 = Time.realtimeSinceStartup;
            while ((ZGame.I == null || ZGame.I == oldGame || ZGame.I.World == null) && Time.realtimeSinceStartup - w0 < 90f) yield return null;
            yield return Drawn();
            Check($"after the restart the same switch object runs, ON, drawn at the base from the start ({OzLensEdges.Describe()})", OzLensEdges.I && OzLensEdges.I == keep && OzLensEdges.On && Near(OzLensEdges.Seen, OzLensEdges.Base));
            float t1 = Time.realtimeSinceStartup;
            while (!ZodBriefing.BriefingActive && Time.realtimeSinceStartup - t1 < 20f) yield return null;
            bool flew = ZodBriefing.BriefingActive;
            float sMin = 9f, sMax = -9f, vMax = 0f, wMin = 9f; int n = 0; bool shot = false; float t2 = Time.realtimeSinceStartup;
            while (ZodBriefing.BriefingActive && Time.realtimeSinceStartup - t2 < 150f)
            {
                yield return null;
                if (OzLensEdges.SeenFrame < Time.frameCount - 2) continue;
                sMin = Mathf.Min(sMin, OzLensEdges.Seen); sMax = Mathf.Max(sMax, OzLensEdges.Seen); vMax = Mathf.Max(vMax, stackVignette); wMin = Mathf.Min(wMin, OzStartVignette.Weight); n++;
                if (!shot && Time.realtimeSinceStartup - t2 > 4f) { shot = true; yield return Shot("k39_L02_flyover_on" + Tag(OzLensEdges.Base)); }
            }
            Log($"   flyover: {(flew ? "flown" : "NOT started (see the console's [ZodCameo] lines)")} {Time.realtimeSinceStartup - t2:0.0} s, {n} frames: chromatic aberration {F3(sMin)} .. {F3(sMax)}, vignette up to {vMax:0.00}, start vignette weight at least {wMin:0.00}");
            Check($"the level-start flyover is drawn with the lens edges at the base ({F3(sMin)} .. {F3(sMax)}) and with the start vignette at its own strength (weight {wMin:0.00}, vignette up to {vMax:0.00}, its own {OzStartVignette.Intensity:0.00})",
                  flew && n > 10 && Near(sMin, OzLensEdges.Base) && Near(sMax, OzLensEdges.Base) && wMin > 0.99f && vMax >= OzStartVignette.Intensity - 0.01f);
            float t3 = Time.realtimeSinceStartup;
            while (OzStartVignette.GoneAt < 0f && Time.realtimeSinceStartup - t3 < 45f) yield return null;
            yield return Real(0.3f); yield return Drawn();
            Check($"after the start vignette ({(OzStartVignette.GoneAt >= 0f ? "gone" : "still on")}): the lens edges stay at the base ({F3(OzLensEdges.Seen)}), the vignette is the level's own again ({stackVignette:0.00} under {vMax:0.00})",
                  OzStartVignette.GoneAt >= 0f && Near(OzLensEdges.Seen, OzLensEdges.Base) && stackVignette < vMax - 0.01f);
            if (briefHad) PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, briefVal); else PlayerPrefs.DeleteKey(ZodBriefing.PrefBriefing);
            if (testHad) PlayerPrefs.SetInt("oz_testmode", testVal); else PlayerPrefs.DeleteKey("oz_testmode");
            PlayerPrefs.Save();
            yield return Real(2f);
        }

        // ------------------------------------------------------------------------------------------------ the main menu and the Unit editor
        IEnumerator MainMenuRow(bool pictures)
        {
            OpenZMainMenu menu = FindFirstObjectByType<OpenZMainMenu>();
            if (!menu)
            {
                OzScenes.MainMenu();
                float w0 = Time.realtimeSinceStartup;
                while (!(menu = FindFirstObjectByType<OpenZMainMenu>()) && Time.realtimeSinceStartup - w0 < 60f) yield return null;
            }
            if (!menu) { Check("the main menu loaded", false); yield break; }
            float w1 = Time.realtimeSinceStartup;
            while (!menu.ProofOnMainList && Time.realtimeSinceStartup - w1 < 10f) yield return null;
            yield return Real(3.5f);
            menu.ProofOpen("options"); yield return Real(0.8f);
            menu.ProofOptTab(OzSettingTips.TabGraphics); yield return Real(0.6f);
            var rows = ShownRows(menu);
            CheckRows("main menu", rows, menu.ProofPanel("options"));
            var mine = rows.FirstOrDefault(r => r.label == OzLensEdges.Label).row;
            string tip = menu.ProofOptTip(OzLensEdges.Label);
            Check("main menu: the row's tip is the switch's (" + (tip ?? "none").Split('|')[0] + ")", tip == OzLensEdges.Tip);
            if (mine == null) { menu.ProofOpen("none"); yield break; }
            MouseAt(Mid(mine.Rt)); yield return Real(1.2f);
            if (pictures) yield return Shot("k39_3_menu_graphics_row_tip");
            bool start = OzLensEdges.On;
            for (int k = 0; k < 2; k++)
            {
                bool want = k == 0 ? !start : start;
                float t0 = Time.unscaledTime;
                yield return Click(Mid(mine.Rt)); yield return Real(0.3f);
                int saved = PlayerPrefs.GetInt(OzLensEdges.Pref, -1);
                Log($"   menu click {k + 1}: {OzLensEdges.Describe()}, saved {saved}, sounds: {Sounds(t0)}");
                Check($"main menu click {k + 1}: the switch {(want ? "ON" : "OFF")}, saved ({saved}), the menu's {(want ? "ui_toggle_on" : "ui_toggle_off")} sound, the volume {(want ? "enabled" : "disabled")}",
                      OzLensEdges.On == want && saved == (want ? 1 : 0) && Heard(t0, want ? "toggle_on" : "toggle_off") && OzLensEdges.Vol && OzLensEdges.Vol.enabled == want);
            }
            Log("   the main menu's camera draws no post-processing: the switch shows in the levels, the flyovers and the Unit editor");
            if (!OzLensEdges.On) OzLensEdges.On = true;
            menu.ProofOpen("none"); Park(); yield return Real(0.8f);
        }

        IEnumerator UnitEditor()
        {
            var menu = FindFirstObjectByType<OpenZMainMenu>();
            if (!menu || !menu.ProofUnitEditorItem(out var at, out _, out _, out _)) { Check("the main menu's UNIT EDITOR item", false); yield break; }
            yield return Click(at);
            float w0 = Time.realtimeSinceStartup;
            while (!(OzUnitEditor.UiBuilt && SceneManager.GetActiveScene().name == OzUnitEditor.SceneName) && Time.realtimeSinceStartup - w0 < 60f) yield return null;
            yield return Real(3f); Park(); yield return Real(0.5f);
            OzLensEdges.On = true; yield return Drawn();
            var cam = OzLensEdges.SeenCamera;
            Check($"the Unit editor ({SceneManager.GetActiveScene().name}, its UI {(OzUnitEditor.UiBuilt ? "built" : "missing")}) is drawn with the lens edges at the base ({F3(OzLensEdges.Seen)} on {(cam ? cam.name : "-")})",
                  OzUnitEditor.UiBuilt && Near(OzLensEdges.Seen, OzLensEdges.Base) && cam && cam.GetComponent<RtsCamera>());
            yield return Shot("k39_UE_on" + Tag(OzLensEdges.Base));
            OzLensEdges.On = false; yield return Drawn();
            bool off = Near(OzLensEdges.Seen, 0f);
            yield return Shot("k39_UE_off");
            OzLensEdges.On = true; yield return Drawn();
            Check($"the Unit editor follows the switch live: OFF {(off ? "drawn 0" : "NOT 0")}, ON again {F3(OzLensEdges.Seen)}", off && Near(OzLensEdges.Seen, OzLensEdges.Base));
            OzScenes.MainMenu();
            float w1 = Time.realtimeSinceStartup;
            while (!FindFirstObjectByType<OpenZMainMenu>() && Time.realtimeSinceStartup - w1 < 60f) yield return null;
            yield return Real(2f);
        }

        void Finish()
        {
            RestorePrefs();
            Log("   the player's setting put back: " + OzLensEdges.Describe());
            Check($"0 console errors during the proof ({errors})", errors == 0);
            Report = $"[OzLensEdgesProof] {(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks, {pictures.Count} pictures in {dir}";
            Log(Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, menusOnly ? $"checks_menus_{Screen.width}x{Screen.height}.txt" : "checks.txt"), sb.ToString()); }
            catch (System.Exception ex) { Debug.LogWarning("[OzLensEdgesProof] " + ex.Message); }
            Destroy(gameObject);
        }
    }
}
#endif
