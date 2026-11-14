#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// k83 Play proof (editor only) of the small look fixes; menu OpenZ/V1.2/Look Fixes: 83 Play Proof (Play), Editor/OzV12LookFixesCheck.cs.
    /// Started on the MainMenu scene in Play it walks by itself (about two minutes), hands off, and ends in the Unit editor (stop Play then):
    ///  F1 the faction screen opens; EvilZod's DOSSIER card first (the first card made on this opening): every row as the owner's mock-up:
    ///     a row wider than its box on two lines broken after its label ("Tendencies:" / "World Hostile Takeover"), the others on one line;
    ///     the two-line rows taller than the one-line rows; the rows clear of the stamp. Picture and close-up.
    ///  F2 Zod's card: the same (his rows 2 and 5 on two lines). Picture and close-up.
    ///  H  the VS sign's halo: drawn with the poster shader's halo mode on its own material, told the screen's colour behind the VS; pictures
    ///     at the pulse's low, middle and high (tools/vs_halo_model.py --pics measures them against the preview's mix).
    ///  F3 a new opening, Zod's card first, then EvilZod's: the same rows. F4 1366 x 768: the same rows, pictures.
    ///  F5 back on the main menu: the halo's material gone with the screen.
    ///  U  the Unit editor (the main menu's UNIT EDITOR): the pointer (real mouse events, routed to the game whatever window has focus, put
    ///     back after) on the top bar's SAVE at 1920 x 1080 and 1366 x 768: the HUD tooltip shows and fades in fully; its canvas sorts over
    ///     the HUD canvas the STATS box is on; pictures with and without the tooltip and the rectangles (ue_rects.txt) for tools/ue_tip_alpha.py.
    /// Pictures and checks.txt go to the kit's proof folder (k83_small_look_fixes/proof/play_n). Test only (editor).
    /// </summary>
    public sealed class OzLookFixesProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzLookFixesProof I { get; private set; }
        const string T = "[OzLookFixesProof] ";

        bool started, putBack; int viewWas = -1;
        string dir; readonly StringBuilder sb = new StringBuilder(); int passes, fails, pics;
        bool routed; InputSettings.EditorInputBehaviorInPlayMode route0; InputSettings.BackgroundBehavior back0; Vector2 lastPos;

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
                    var kit = Path.Combine(stage, "k83_small_look_fixes");
                    if (Directory.Exists(kit)) return Path.Combine(kit, "proof");
                }
            }
            return Path.Combine(Application.persistentDataPath, "k83_small_look_fixes");
        }

        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzLookFixesProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzLookFixesProof>();
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
            viewWas = OzFinalGateView.SelectedIndex();
            started = true;
            Log("k83 small look fixes, Play proof, Game view " + Screen.width + " x " + Screen.height + ", into " + dir);
            StartCoroutine(All());
        }

        void OnDestroy()
        {
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the settings put back"); }
            if (I == this) I = null;
        }

        void PutBack()
        {
            putBack = true;
            Route(false);
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

        IEnumerator Shot(string name)
        {
            string p = Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(p); pics++; Log("   picture " + Path.GetFileName(p));
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        /// <summary>A close-up of a screen rect (a card), from a capture of the frame.</summary>
        IEnumerator Crop(string name, Rect r)
        {
            yield return new WaitForEndOfFrame();
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, full.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, full.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(r.width), 1, full.width - x0), h = Mathf.Clamp(Mathf.CeilToInt(r.height), 1, full.height - y0);
            var c = new Texture2D(w, h, TextureFormat.RGBA32, false);
            c.SetPixels(full.GetPixels(x0, y0, w, h)); c.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png"), c.EncodeToPNG());
            Destroy(c); Destroy(full); pics++;
            Log("   picture " + name + " (a close-up)");
        }

        IEnumerator SetSize(int w, int h)
        {
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            yield return Until(() => Screen.width == w && Screen.height == h, 5f);
            yield return Real(0.8f);
            if (Screen.width != w || Screen.height != h) Check($"the Game view shows {w} x {h} ({info}; it shows {Screen.width} x {Screen.height})", false);
            else Log("Game view " + w + " x " + h + " (" + info + ")");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            if (!rt) return new Rect();
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y));
        }

        static string R(Rect r) => $"{r.xMin:0}..{r.xMax:0} x {r.yMin:0}..{r.yMax:0}";

        // the editor gives mouse events to the game only while a Game view has focus: for the proof's events the input goes to the game
        // whatever window has focus (no window focus needed), put back at the end (as the Unit editor's popups check)
        void Route(bool on)
        {
            var s = InputSystem.settings;
            if (on && !routed) { routed = true; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior; s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
            else if (!on && routed) { routed = false; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0; }
        }

        void Move(Vector2 p)
        {
            var m = Mouse.current; if (m == null) return;
            InputSystem.QueueStateEvent(m, new MouseState { position = p, delta = p - lastPos }); lastPos = p;
        }

        static T Field<T>(object o, string name) where T : class
        {
            var f = o?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? f.GetValue(o) as T : null;
        }

        /// <summary>The lines a TextMeshPro text is laid out on (its characters, tags left out), trimmed.</summary>
        static List<string> Lines(TMP_Text t)
        {
            var ti = t.textInfo; var res = new List<string>();
            for (int l = 0; l < ti.lineCount; l++)
            {
                var li = ti.lineInfo[l]; var s = new StringBuilder();
                for (int c = li.firstCharacterIndex; c <= li.lastCharacterIndex && c < ti.characterCount; c++) s.Append(ti.characterInfo[c].character);
                res.Add(s.ToString().Trim());
            }
            return res;
        }

        /// <summary>A one-line width measured on a fresh, awake text with the row's own font and size (the row itself is not touched).</summary>
        float AwakeWidth(TMP_Text like, string s)
        {
            var go = new GameObject("k83 measure", typeof(RectTransform)); go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = like.font; t.fontSharedMaterial = like.fontSharedMaterial; t.fontSize = like.fontSize; t.characterSpacing = like.characterSpacing;
            t.richText = true; t.textWrappingMode = TextWrappingModes.NoWrap; t.text = s;
            float w = t.GetPreferredValues(s, 99999f, 0f).x;
            DestroyImmediate(go);
            return w;
        }

        IEnumerator Open(string tag)
        {
            var m = Menu();
            if (!m) { Check(tag + " the main menu", false); yield break; }
            if (m.ProofView != "Main") { m.ProofMain(); yield return Real(0.8f); }
            m.ProofFactionPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.06f));
            m.ProofFaction();
            yield return Until(() => m.ProofFactionShown && m.ProofRig && m.ProofRig.Ready && m.ProofDeckReady && m.ProofPrintAlpha(0) > 0.99f && m.ProofPrintAlpha(1) > 0.99f, 15f);
            yield return Real(0.8f);
            Check($"{tag} the faction screen opened ({m.ProofFactionShown}), its cards' fonts and pictures loaded ({m.ProofDeckReady})", m.ProofFactionShown && m.ProofDeckReady);
        }

        IEnumerator Away(OpenZMainMenu m)
        {
            m.ProofFactionPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.06f));
            yield return Until(() => m.ProofDossierState(0) == "Hidden" && m.ProofDossierState(1) == "Hidden", 3f);
            yield return Real(0.3f);
        }

        /// <summary>The pointer on a poster: its card out, every row checked against the mock-up's rule.</summary>
        IEnumerator Card(OpenZMainMenu m, int side, string tag, string pic)
        {
            yield return Away(m);
            var pr = ScreenRect(m.ProofPoster(side));
            m.ProofFactionPointer(new Vector2(pr.center.x, pr.center.y + pr.height * 0.2f));
            yield return Until(() => m.ProofDossierState(side) == "Shown", 3f); yield return Real(0.5f);
            var root = m.ProofDossier(side);
            var words = root ? root.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Words").ToList() : new List<TMP_Text>();
            bool all = words.Count == OzFactionDeck.Rows; var lines = new List<string>();
            float oneMax = 0f, twoMin = 999f, used = OzFactionDeck.RowsY * OzFactionDeck.CardH(side); int twos = 0;
            for (int r = 0; r < words.Count && r < OzFactionDeck.Rows; r++)
            {
                var t = words[r]; t.ForceMeshUpdate();
                var rt = (RectTransform)t.transform; float box = rt.rect.width, rh = rt.rect.height;
                string lab = OzFactionDeck.Label[side][r], val = OzFactionDeck.Value[side][r];
                float w = AwakeWidth(t, OzFactionDeck.RowText(side, r, false));
                bool wide = w > box;
                var got = Lines(t);
                var want = wide ? new List<string> { lab, val } : new List<string> { lab + " " + val };
                bool ok = got.SequenceEqual(want) && m.ProofDossierRow(side, r).Contains("\n") == wide && !t.isTextOverflowing;
                all &= ok;
                if (wide) { twos++; twoMin = Mathf.Min(twoMin, rh); } else oneMax = Mathf.Max(oneMax, rh);
                used += rh;
                lines.Add($"{r + 1} \"{string.Join(" / ", got)}\" ({w:0.0} u {(wide ? ">" : "<=")} {box:0.0}, row {rh:0.0} u) {ok}");
            }
            bool taller = twos == 0 || twoMin > oneMax;
            bool clear = used < OzFactionDeck.CardH(side) - 60f;
            var head = m.ProofDossierLabel(side, "General") as TMP_Text;
            Check($"{tag}: the rows {string.Join("; ", lines)}; two-line rows taller than one-line rows {taller} ({twoMin:0.0} / {oneMax:0.0} u); the rows end {used:0.0} u under the paper's top (of {OzFactionDeck.CardH(side):0.0}: the stamp clear) {clear}; "
                  + $"the general's name at {(head ? head.fontSize : -1f):0.0} u ({OzFactionDeck.HeadingSize:0} unless wider than its line)",
                  all && taller && clear);
            if (pic != null)
            {
                yield return Shot("hover_" + pic);
                yield return Crop(pic, ScreenRect(root));
            }
        }

        // ------------------------------------------------------------------------------------------------ the walk
        IEnumerator All()
        {
            yield return Real(1f);
            yield return SetSize(1920, 1080);
            yield return Open("F1");
            var m = Menu();
            if (!m || !m.ProofDeckReady) { Finish(); yield break; }
            yield return Card(m, 1, "F1 EvilZod's card (the first card made on this opening)", "evilzod_card");
            yield return Card(m, 0, "F2 Zod's card", "zod_card");
            yield return Halo(m);
            m.ProofMain(); yield return Real(1.2f);
            yield return Open("F3");
            yield return Card(m, 0, "F3 a new opening: Zod's card first", null);
            yield return Card(m, 1, "F3 then EvilZod's card", null);
            yield return SetSize(1366, 768);
            yield return Away(m); yield return Real(0.6f);
            yield return Card(m, 1, "F4 1366 x 768: EvilZod's card", "evilzod_card");
            yield return Card(m, 0, "F4 1366 x 768: Zod's card", "zod_card");
            yield return Away(m);
            yield return Shot("faction_halo");
            yield return SetSize(1920, 1080);
            m.ProofFactionPointer(null);
            m.ProofMain(); yield return Real(1.2f);
            Check($"F5 back on the main menu: the VS halo's material gone with the screen ({m.ProofVsHaloMat == null})", m.ProofVsHaloMat == null);
            yield return UnitEditor(m);
            Finish();
        }

        /// <summary>The VS sign's halo: its material and the pictures at the pulse's low, middle and high.</summary>
        IEnumerator Halo(OpenZMainMenu m)
        {
            yield return Away(m); yield return Real(0.8f);
            var halo = m.ProofVsHalo; var mat = m.ProofVsHaloMat; var deck = m.ProofDeck;
            bool on = halo && halo.enabled && mat && halo.material == mat && Mathf.Abs(mat.GetFloat("_Print") - OzFactionLook.HaloMode) < 0.01f && deck && halo.texture == deck.VsHalo;
            bool sign = m.ProofVsSign && m.ProofVsMat && m.ProofVsSign.material == m.ProofVsMat && Mathf.Abs(m.ProofVsMat.GetFloat("_Print") - 2f) < 0.01f;
            Color b = m.ProofVsBehind;
            Check($"H the VS halo drawn with the poster shader's halo mode (_Print {(mat ? mat.GetFloat("_Print") : -1f):0}) on its own material, its picture the halo's {on}; the sign itself as before (_Print 2) {sign}; "
                  + $"the screen's colour behind the VS {(int)(b.r * 255f)},{(int)(b.g * 255f)},{(int)(b.b * 255f)} (the backdrop {OzFaction.PaperBack.r},{OzFaction.PaperBack.g},{OzFaction.PaperBack.b} with the mood); halo alpha now {(halo ? halo.color.a : -1f):0.00}",
                  on && sign);
            string[] names = { "vs_pulse_low", "vs_pulse_mid", "vs_pulse_high" }; float[] want = { 1.0f, 1.02f, 1.04f };
            for (int q = 0; q < 3; q++)
            {
                float target = want[q];
                yield return Until(() => m.ProofVs && Mathf.Abs(m.ProofVs.localScale.x - target) < 0.0015f, 3f);
                var vr = ScreenRect(m.ProofVs);
                yield return Crop(names[q] + "_crop", new Rect(vr.x - 60f, vr.y - 60f, vr.width + 120f, vr.height + 120f));
                yield return Shot(names[q]);
            }
        }

        /// <summary>The Unit editor: the top bar's SAVE tooltip over the STATS box (the HUD's tooltip), at 1920 and 1366.</summary>
        IEnumerator UnitEditor(OpenZMainMenu m)
        {
            var go = typeof(OpenZMainMenu).GetMethod("GoUnitEditor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (!m || go == null) { Check("U the main menu's UNIT EDITOR", false); yield break; }
            if (m.ProofView != "Main") { m.ProofMain(); yield return Real(0.8f); }
            go.Invoke(m, null);
            yield return Until(() => OzUnitEditor.UiBuilt && OzUeShellRoot.I != null && OzUeShellRoot.I.Top != null && OpenZHud.I != null && OzUeStatsRoot.I != null, 45f);
            if (!OzUnitEditor.UiBuilt || OzUeShellRoot.I == null || OpenZHud.I == null) { Check("U the Unit editor opens from the main menu (45 s)", false); yield break; }
            yield return Real(2f);
            Route(true);
            yield return Tip(1920, 1080);
            yield return Tip(1366, 768);
            Move(new Vector2(Screen.width * 0.42f, Screen.height * 0.55f));
            yield return Real(0.3f);
            Route(false);
            yield return SetSize(1920, 1080);
        }

        IEnumerator Tip(int w, int h)
        {
            yield return SetSize(w, h);
            var hud = OpenZHud.I; var shell = OzUeShellRoot.I; var stats = OzUeStatsRoot.I;
            var tipPanel = Field<RectTransform>(hud, "tipPanel"); var tipCg = Field<CanvasGroup>(hud, "tipCg");
            var save = shell != null && shell.Top != null ? shell.Top.save : null;
            var box = stats != null && stats.Shell != null ? stats.Shell.Box : null;
            if (!tipPanel || !tipCg || save == null || !box) { Check($"U{w} the HUD's tooltip ({tipPanel != null}), the top bar's SAVE ({save != null}), the STATS box ({box != null})", false); yield break; }
            Move(new Vector2(Screen.width * 0.42f, Screen.height * 0.55f));
            yield return Until(() => !tipPanel.gameObject.activeSelf, 2f); yield return Real(0.6f);
            yield return Shot("ue_no_tip");
            float t0 = Time.realtimeSinceStartup;
            var c = new Vector3[4]; save.Rt.GetWorldCorners(c);
            Move(RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f));
            yield return Until(() => tipPanel.gameObject.activeSelf, 3f);
            float shown = Time.realtimeSinceStartup - t0;
            yield return Until(() => tipCg.alpha >= 0.999f, 2f);
            float faded = Time.realtimeSinceStartup - t0;
            yield return Real(0.4f);
            var tc = tipPanel.GetComponent<Canvas>(); var hc = hud.Canvas; var bc = box.GetComponentInParent<Canvas>();
            Rect tr = ScreenRect(tipPanel), br = ScreenRect(box);
            bool overlap = tr.Overlaps(br);
            bool order = tc && hc && tc.overrideSorting && tc.sortingOrder > hc.sortingOrder && bc && bc.rootCanvas == hc && (bc == hc || !bc.overrideSorting);
            bool ok = tipPanel.gameObject.activeSelf && tipCg.alpha >= 0.999f && order;
            Check($"U{w} {w} x {h}: the pointer on the top bar's SAVE: its tooltip shows {shown:0.00} s after and is fully in at {faded:0.00} s (alpha {tipCg.alpha:0.00}); its canvas sorts over the HUD canvas "
                  + $"(override {(tc ? tc.overrideSorting : false)}, order {(tc ? tc.sortingOrder : -1)} over {(hc ? hc.sortingOrder : -1)}); the STATS box on the HUD canvas ({(bc ? bc.name : "none")}); "
                  + $"the tooltip {R(tr)} px, the STATS box {R(br)} px: {(overlap ? "the tooltip lies over the box's corner" : "apart")}", ok);
            yield return Shot("ue_save_tip");
            File.AppendAllText(Path.Combine(dir, "ue_rects.txt"), $"{w}x{h} tip {tr.xMin:0} {Screen.height - tr.yMax:0} {tr.xMax:0} {Screen.height - tr.yMin:0} box {br.xMin:0} {Screen.height - br.yMax:0} {br.xMax:0} {Screen.height - br.yMin:0}\n");
            Move(new Vector2(Screen.width * 0.42f, Screen.height * 0.55f));
            yield return Real(0.4f);
        }
    }
}
#endif
