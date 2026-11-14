#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 60 Play proof (UNIT STATS OPENZ | CUSTOM; menu OpenZ/V1.2/Unit Stats: 60 Custom Stats Play Proof, Editor/OzCustomStatsMenu.cs).
    /// Started on the MainMenu scene in Play it walks the whole flow itself and survives the scene loads. The player's own Custom stats and
    /// choices are kept aside first and put back at the end (also when Play is stopped early). The test profile is written where the Unit
    /// editor's SAVE writes the saved profile (OzLevelStats.ProfileKey ...).
    ///  N  no profile: the battlefield screen exactly as before (no selector, the key row without N, the editor's error line at its old
    ///     place), a choice of CUSTOM left saved goes back to OPENZ, the key N answers ui_disabled; a picture.
    ///  P  a test profile (the jeep's health x 1.25, every other value OpenZ's): the selector over DEPLOY, DEPLOY's width, 8 u over the bar,
    ///     meeting nothing (the bar, the cards, the key row, the title, the commander's strip, every block's tooltip on levels 2 and 16);
    ///     OPENZ lit; a click on CUSTOM, the key N twice; its tooltip left of it on the tooltips' line with the plain words and the profile's
    ///     line; pictures (OPENZ, CUSTOM, the tooltip, level 16 with the SHUTTLE tooltip up).
    ///  S  DEPLOY level 2 with CUSTOM: the Custom rows in use, every jeep of every team with the Custom health, the rule table and the build
    ///     menu's unit card with the Custom number (a picture of the card), a jeep selected (a picture), the CUSTOM STATS tag under MENU
    ///     inside the screen and clear of the top strip and the territory bar at every UI SCALE step, the mission banner's foot; pictures.
    ///  W  the red side wins: the end screen says CUSTOM STATS · NOT IN THE TABLE, no table place; a picture.
    ///  R  REPLAY: Custom again. B  MAIN MENU: everything back (no rows, the switch off, the game's rule values), the choice still CUSTOM.
    ///  O  OPENZ picked, DEPLOY level 2: the game's own jeep, no tag, no Custom words; back to the main menu.
    /// "menu only" runs N and P at the Game view's size (the pictures at 1920 x 1080 and 2560 x 1600). Counted PASS / FAIL lines and the
    /// pictures in <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzLevelStatsProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzLevelStatsProof I { get; private set; }
        public const string ProofName = "Proof jeep stats";

        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", "custom_stats_k60");
            return System.IO.Path.Combine(Application.persistentDataPath, "custom_stats_k60");
        }

        public static void Run(bool menuOnly)
        {
            if (I) { Debug.LogWarning("[OzLevelStatsProof] already running"); return; }
            Report = "running"; var go = new GameObject("OzLevelStatsProof"); DontDestroyOnLoad(go); go.AddComponent<OzLevelStatsProof>().menuOnly = menuOnly;
        }

        bool menuOnly, started, putBack;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir, size;
        // the player's own settings, kept aside
        bool hadText, hadName, hadBase, hadChoice; string oldText, oldName; int oldBase, oldChoice; bool oldBlue, oldHead, oldFps;
        int jeepHp, jeepHpOpenZ;

        void Awake() { I = this; }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (started && !putBack) { PutBack(); Debug.Log("[OzLevelStatsProof] stopped before its end: the player's Custom stats and choices put back"); }
        }

        void Start()
        {
            dir = Folder(); System.IO.Directory.CreateDirectory(dir); size = Screen.width + "x" + Screen.height;
            hadText = PlayerPrefs.HasKey(OzLevelStats.ProfileKey); oldText = PlayerPrefs.GetString(OzLevelStats.ProfileKey, "");
            hadName = PlayerPrefs.HasKey(OzLevelStats.ProfileNameKey); oldName = PlayerPrefs.GetString(OzLevelStats.ProfileNameKey, "");
            hadBase = PlayerPrefs.HasKey(OzLevelStats.ProfileBaseKey); oldBase = PlayerPrefs.GetInt(OzLevelStats.ProfileBaseKey, 0);
            hadChoice = PlayerPrefs.HasKey(OzLevelStats.Pref); oldChoice = PlayerPrefs.GetInt(OzLevelStats.Pref, 0);
            oldBlue = OzPlayBlue.Box; oldHead = OpenZDifficulty.HeadStartToggle; oldFps = OzQuality.FpsOn;
            started = true;
            StartCoroutine(All());
        }

        void PutBack()
        {
            if (hadText) PlayerPrefs.SetString(OzLevelStats.ProfileKey, oldText); else PlayerPrefs.DeleteKey(OzLevelStats.ProfileKey);
            if (hadName) PlayerPrefs.SetString(OzLevelStats.ProfileNameKey, oldName); else PlayerPrefs.DeleteKey(OzLevelStats.ProfileNameKey);
            if (hadBase) PlayerPrefs.SetInt(OzLevelStats.ProfileBaseKey, oldBase); else PlayerPrefs.DeleteKey(OzLevelStats.ProfileBaseKey);
            if (hadChoice) PlayerPrefs.SetInt(OzLevelStats.Pref, oldChoice); else PlayerPrefs.DeleteKey(OzLevelStats.Pref);
            OzPlayBlue.Box = oldBlue; OpenZDifficulty.HeadStartToggle = oldHead;
            if (OzQuality.FpsOn != oldFps) OzQuality.FpsOn = oldFps;                                          // the S step turns the FPS COUNTER on for the tag's check
            PlayerPrefs.Save();
            putBack = true;
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzLevelStatsProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            string p = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p); Log("   picture " + name + ".png");
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        IEnumerator All()
        {
            var m = FindFirstObjectByType<OpenZMainMenu>();
            if (!m) Check("started on the MainMenu scene in Play", false);
            else
            {
                if (OzPlayBlue.Box) { OzPlayBlue.Box = false; Log("PLAY AS BLUE was on: off for the proof (put back at the end)"); }
                yield return NoProfile(m);
                yield return WithProfile(m);
                if (!menuOnly)
                {
                    m = FindFirstObjectByType<OpenZMainMenu>();
                    m.ProofFocus(System.Array.FindIndex(OzLevelDef.All, d => d.Id == "L02")); yield return Real(0.5f);
                    if (!OzLevelStats.Box) m.ProofSetUnitStats(true, false);
                    if (!OpenZDifficulty.HeadStartToggle) { OpenZDifficulty.HeadStartToggle = true; Log("HEAD START on for the proof (level 2's jeeps come with it; put back at the end)"); }
                    m.ProofDeploy();
                    Log("L02: DEPLOY with CUSTOM");
                    yield return Until(() => ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 90f);
                    if (!ZGame.I) Check("L02 loaded after DEPLOY", false);
                    else yield return CustomLevel();
                    yield return OpenZLevel();
                }
            }
            PutBack();
            m = FindFirstObjectByType<OpenZMainMenu>();
            if (m) { m.ProofUnitStatsRebuild(); yield return Real(0.3f); }
            Log("the player's Custom stats and choices put back (" + (hadText ? "his profile" : "no profile") + ", choice " + (hadChoice ? oldChoice.ToString() : "unset") + ")");
            Report = $"[OzLevelStatsProof] {(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks{(menuOnly ? " (menu only, " + size + ")" : "")}; pictures in {dir}";
            Log(Report);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, (menuOnly ? "checks_menu_" + size : "checks") + ".txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ N: no profile
        IEnumerator NoProfile(OpenZMainMenu m)
        {
            PlayerPrefs.DeleteKey(OzLevelStats.ProfileKey); OzLevelStats.Box = true;                             // no saved profile; a CUSTOM choice left saved
            PlayerPrefs.Save();
            m.ProofSelect(); yield return Real(0.8f);
            m.ProofUnitStatsRebuild(); yield return Real(0.5f);
            m.ProofFocus(System.Array.FindIndex(OzLevelDef.All, d => d.Id == "L02")); yield return Real(0.5f);
            string keys = Text(m.ProofSelectRoot, "Keys");
            var err = m.ProofErrorLine;
            float errTop = err ? CanvasRect(err).center.y : -1f, errWant = OzDeployBar.ErrorY + OzText.MinSize * 0.3635f;   // At(y = the capitals' top): the box's middle is the capitals' middle
            Check($"N {size} no profile: no selector, the key row without N ({keys.Length} characters), the error line at its old place ({errTop:0.0} u, want {errWant:0.0})",
                  !m.ProofUnitStatsShown && !m.ProofUnitStatsRoot && !keys.Contains("UNIT STATS") && Mathf.Abs(errTop - errWant) < 1f);
            Check($"N {size} the CUSTOM choice left saved went back to OPENZ ({OzLevelStats.Pref} {PlayerPrefs.GetInt(OzLevelStats.Pref, -1)})", !OzLevelStats.Box);
            int r0 = OpenZMainMenu.UnitStatsKeyRefused;
            m.ProofSetUnitStats(true, true); yield return Real(0.2f);
            Check("N the key N without a profile answers ui_disabled and changes nothing", OpenZMainMenu.UnitStatsKeyRefused == r0 + 1 && !OzLevelStats.Box);
            yield return Shot($"menu_{size}_L02_no_profile");
        }

        // ------------------------------------------------------------------------------------------------ P: a profile
        IEnumerator WithProfile(OpenZMainMenu m)
        {
            var r = OzLevelStats.RulesAsLevelStart();
            var oz = OzLevelStatsPlan.OpenZTable(OzLevelStats.SettingsText, r);
            jeepHpOpenZ = (int)oz["jeep"][(int)UeStat.Health].value; jeepHp = Mathf.RoundToInt(jeepHpOpenZ * 1.25f);
            PlayerPrefs.SetString(OzLevelStats.ProfileKey, OzLevelStatsPlan.TextOf(oz, r, new Dictionary<string, double> { ["jeep.hp"] = jeepHp }));   // as the Unit editor's SAVE writes it
            PlayerPrefs.SetString(OzLevelStats.ProfileNameKey, ProofName); PlayerPrefs.SetInt(OzLevelStats.ProfileBaseKey, 0); PlayerPrefs.Save();
            Log($"the test profile \"{ProofName}\": the jeep's health {jeepHp} (OpenZ {jeepHpOpenZ}), every other value OpenZ's");
            m.ProofUnitStatsRebuild(); yield return Real(0.6f);
            m.ProofFocus(System.Array.FindIndex(OzLevelDef.All, d => d.Id == "L02")); yield return Real(0.6f);
            var root = m.ProofUnitStatsRoot; var sel = m.ProofSelectRoot;
            Check($"P {size} with a profile the selector shows (OPENZ lit: {!OzLevelStats.Box})", m.ProofUnitStatsShown && root && root.gameObject.activeInHierarchy && !OzLevelStats.Box);
            if (!root) yield break;
            var deploy = Find(sel, "Deploy"); var bar = Find(sel, "DeployBar");
            Rect pr = CanvasRect(root), dr = deploy ? CanvasRect(deploy) : default, br = bar ? CanvasRect(bar) : default;
            float cw = m.ProofCanvasWidth;
            Check($"P {size} the selector stands over DEPLOY ({R(pr)}; DEPLOY {R(dr)}), its lower edge {br.yMin - pr.yMax:0.0} u over the bar, inside the screen ({cw:0} u wide)",
                  deploy && bar && Mathf.Abs(pr.xMin - dr.xMin) < 0.6f && Mathf.Abs(pr.xMax - dr.xMax) < 0.6f && Mathf.Abs(br.yMin - pr.yMax - OpenZMainMenu.UsGap) < 0.6f && pr.xMin >= 0f && pr.xMax <= cw + 0.5f && pr.yMin >= 0f);
            var meets = new List<string>();
            void Clear(string what, RectTransform rt) { if (rt && rt.gameObject.activeInHierarchy && Meet(pr, CanvasRect(rt))) meets.Add(what); }
            Clear("the deploy bar", bar); Clear("the title", Find(sel, "Title")); Clear("the key row", Find(sel, "Keys")); Clear("BACK", Find(sel, "Back"));
            for (int i = 0; i < m.ProofCardCount; i++) Clear("card " + (i + 1), m.ProofCard(i));
            Clear("the HIGH SCORES button", m.ProofHighScoresButton);
            bool stripClear = pr.xMin >= m.ProofStripRight + 5f;
            Check($"P {size} the selector meets nothing (the bar, the cards, the title, the key row, BACK, HIGH SCORES){(meets.Count > 0 ? "; it MEETS " + string.Join(", ", meets) : "")}; right of the commander's strip ({m.ProofStripRight:0} u)", meets.Count == 0 && stripClear);
            string keys = Text(sel, "Keys");
            Check($"P {size} the key row names the key: \"{OzKeyNames.Name(OpenZMainMenu.UnitStatsKey)}  UNIT STATS\"", keys.Contains(OzKeyNames.Name(OpenZMainMenu.UnitStatsKey) + "  UNIT STATS"));
            var err = m.ProofErrorLine;
            Check($"P {size} the editor's error line moved over the selector ({(err ? R(CanvasRect(err)) : "none")})", err && CanvasRect(err).yMax <= pr.yMin + 0.5f);
            yield return Shot($"menu_{size}_L02_openz");
            int t0 = OpenZMainMenu.UnitStatsToggles;
            m.ProofSetUnitStats(true, false); yield return Real(0.3f);
            Check("P a click on CUSTOM picks it (saved, ui_toggle_on)", OzLevelStats.Box && OpenZMainMenu.UnitStatsToggles == t0 + 1 && PlayerPrefs.GetInt(OzLevelStats.Pref, 0) == 1);
            m.ProofSetUnitStats(!OzLevelStats.Box, true); yield return Real(0.25f); bool offByKey = !OzLevelStats.Box;
            m.ProofSetUnitStats(!OzLevelStats.Box, true); yield return Real(0.25f);
            Check("P the key N picks OPENZ and CUSTOM again", offByKey && OzLevelStats.Box && OpenZMainMenu.UnitStatsToggles == t0 + 3);
            yield return Shot($"menu_{size}_L02_custom");
            // its tooltip
            m.ProofUnitStatsTipHover(true); yield return Real(0.5f);
            var tip = m.ProofUnitStatsTip; Rect tr = tip ? CanvasRect(tip) : default; string tt = m.ProofUnitStatsTipText;
            var tmeets = new List<string>();
            foreach (var n in new[] { "Title", "Keys", "Back" }) { var x = Find(sel, n); if (x && Meet(tr, CanvasRect(x))) tmeets.Add(n); }
            for (int i = 0; i < m.ProofCardCount; i++) { var c = m.ProofCard(i); if (c && Meet(tr, CanvasRect(c))) tmeets.Add("card " + (i + 1)); }
            Check($"P {size} its tooltip ({R(tr)}) is inside the screen, left of the selector, on the tooltips' line, right of the commander's strip, meeting nothing{(tmeets.Count > 0 ? "; it MEETS " + string.Join(", ", tmeets) : "")}",
                  tip && tip.gameObject.activeInHierarchy && tr.xMin >= m.ProofStripRight + 5f && tr.xMax <= pr.xMin - 5f && tr.yMax <= OzDeployBar.TipBottom + 0.5f && tr.yMin >= 0f && tmeets.Count == 0);
            string plain = Plain(tt);
            Check($"P its words: \"{plain.Replace("\n", " / ")}\"", plain.Contains("CUSTOM plays this level with the unit stats") && plain.Contains("OPENZ is the game's own numbers") && plain.Contains("Yours: \"" + ProofName + "\", 1 value changed.") && plain.Contains("not entered in the high scores"));
            yield return Shot($"menu_{size}_L02_custom_tooltip");
            m.ProofUnitStatsTipHover(false); yield return Real(0.4f);
            // every block's tooltip stops left of it: HEAD START and PLAY AS BLUE on level 2, the SHUTTLE on level 16
            var hits = new List<string>();
            m.ProofHeadTip(true); yield return Real(0.5f); if (m.ProofHeadTipRt && m.ProofHeadTipRt.gameObject.activeInHierarchy && Meet(CanvasRect(m.ProofHeadTipRt), pr)) hits.Add("HEAD START (L02)"); m.ProofHeadTip(false);
            m.ProofPlayBlueTipHover(true); yield return Real(0.5f); if (m.ProofPlayBlueTip && m.ProofPlayBlueTip.gameObject.activeInHierarchy && Meet(CanvasRect(m.ProofPlayBlueTip), pr)) hits.Add("PLAY AS BLUE"); m.ProofPlayBlueTipHover(false);
            yield return Real(0.4f);
            m.ProofFocus(System.Array.FindIndex(OzLevelDef.All, d => d.Id == "L16")); yield return Real(0.9f);
            pr = CanvasRect(m.ProofUnitStatsRoot);
            m.ProofShuttleTipHover(true); yield return Real(0.5f);
            bool shuttleUp = m.ProofShuttleTip && m.ProofShuttleTip.gameObject.activeInHierarchy;
            if (shuttleUp && Meet(CanvasRect(m.ProofShuttleTip), pr)) hits.Add("SHUTTLE (L16)");
            yield return Shot($"menu_{size}_L16_custom_shuttle_tooltip");
            m.ProofShuttleTipHover(false);
            m.ProofHeadTip(true); yield return Real(0.5f); if (m.ProofHeadTipRt && m.ProofHeadTipRt.gameObject.activeInHierarchy && Meet(CanvasRect(m.ProofHeadTipRt), pr)) hits.Add("HEAD START (L16)"); m.ProofHeadTip(false);
            yield return Real(0.4f);
            Check($"P {size} every block's tooltip stays off the selector (HEAD START and PLAY AS BLUE on level 2, the SHUTTLE and HEAD START on level 16; SHUTTLE up: {shuttleUp}){(hits.Count > 0 ? "; MEETS: " + string.Join(", ", hits) : "")}", hits.Count == 0);
            yield return Shot($"menu_{size}_L16_custom");
        }

        // ------------------------------------------------------------------------------------------------ S, W, R, B: a Custom level
        IEnumerator CustomLevel()
        {
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 4f, 30f);
            yield return Real(1.5f);
            var g = ZGame.I; var W = g.World; var hud = OpenZHud.I;
            Log("L02: " + OzLevelStatsLevel.Status());
            var st = OzLevelStats.LastStart;
            Check($"S L02 CUSTOM: the Custom rows in use (\"{(W.CustomStatsInUse != null ? W.CustomStatsInUse.Name : "none")}\", rows {(W.CustomStatsInUse != null ? string.Join(", ", W.CustomStatsInUse.Rows.Keys) : "none")}), the DV142 switch on",
                  OzLevelStats.InUse && W.CustomStatsInUse != null && W.CustomStatsInUse.Rows.ContainsKey("jeep") && ZWorld.CustomStatsDeviation && st != null && st.custom);
            var jeeps = W.Objects.Where(o => o.Type == "jeep" && o.Kind == ZKind.Vehicle && !o.Destroyed).ToList();
            int ok = jeeps.Count(o => o.MaxHealth == jeepHp);
            string teams = string.Join(", ", jeeps.GroupBy(o => o.Owner).Select(x => x.Key + " " + x.Count()));
            Check($"S L02 every jeep has the Custom health {jeepHp} (OpenZ {jeepHpOpenZ}): {ok} of {jeeps.Count} ({teams}); the rule table's row {W.S.Unit("jeep").MaxHealth}",
                  jeeps.Count > 0 && ok == jeeps.Count && W.S.Unit("jeep").MaxHealth == jeepHp);
            var card = OzUnitFacts.For(W.S, "jeep");
            Check($"S L02 the unit card's numbers are the Sim's: the jeep's card HP {(card != null ? card.hpValue : -1)}", card != null && card.hpValue == jeepHp);
            // the build menu's card of the jeep (a producer of the player's that lists it)
            bool shown = false; string cardHp = "";
            foreach (var b in W.Objects.Where(o => o.IsProducer && o.Owner == hud.Player && !o.Destroyed).ToList())
            {
                hud.Production.Close(); if (hud.rts) { hud.rts.focus = g.CenterWorld(b); }
                yield return Real(0.4f);
                hud.Production.Open(b); yield return Real(0.5f);
                for (int i = 0; i < OzUnitPopup.TileCount && !shown; i++)
                {
                    if (!OzUnitPopup.Tile(i, out var btn, out var unit, out _, out _) || unit != "jeep") continue;
                    OzUnitPopup.ProofHover = btn; yield return Real(0.4f);
                    var pop = OzUnitPopup.I;
                    if (pop && pop.Visible && pop.ShownCard != null) { shown = true; cardHp = Plain(pop.ShownCard.hp); yield return Shot("level_L02_custom_jeep_card"); }
                    OzUnitPopup.ProofHover = null;
                }
                if (shown) break;
            }
            hud.Production.Close();
            Check(shown ? $"S L02 the build menu's jeep card shows HP {cardHp}" : "S L02 no producer of the player lists the jeep at the start: the card was read from the rule table above (a note, not a fail)", !shown || cardHp.Contains(jeepHp.ToString()));
            // a jeep selected: the unit panel's health bar on the absolute scale
            var input = hud.input; var mine = jeeps.FirstOrDefault(o => o.Owner == hud.Player);
            if (input != null && mine != null)
            {
                input.Selection.Clear(); input.Selection.Add(mine);
                if (hud.rts) { hud.rts.focus = g.CenterWorld(mine); hud.rts.SetHeightImmediate(70f); }
                yield return Real(0.8f); yield return Shot("level_L02_custom_jeep_selected_h70");
                input.Selection.Clear();
            }
            // the tag under MENU at every UI SCALE step, and the banner's words
            var lvl = OzLevelStatsLevel.I; var tag = lvl ? lvl.TagRt : null; var menuBtn = hud.Root.Find("MenuButton") as RectTransform;
            var top = hud.Root.Find("TopStrip") as RectTransform; var terr = hud.Root.Find("Territory") as RectTransform;
            string foot = Texts("Foot", "MissionBanner").FirstOrDefault() ?? "";
            Check($"S L02 the mission banner's foot says CUSTOM STATS (\"{Plain(foot)}\")", Plain(foot).Contains(OzLevelStats.Tag));
            float s0 = OzText.UiScale; var bad = new List<string>(); int steps = 0, fpsSteps = 0;
            if (!OzQuality.FpsOn) { OzQuality.FpsOn = true; Log("FPS COUNTER on for the tag's check (its chip stands under MENU too; put back after it)"); }
            foreach (float s in OzText.ScalePresets)
            {
                OzText.UiScale = s; hud.ApplyUiScale(); yield return Real(0.8f); steps++;                    // the FPS COUNTER chip finds its place twice a second
                if (!tag || !tag.gameObject.activeInHierarchy) { bad.Add(s.ToString("0.00") + " not shown"); continue; }
                Rect t = Px(tag), mb = menuBtn ? Px(menuBtn) : default;                                    // screen pixels, top-left origin: yMin is the top edge
                if (t.xMin < -0.5f || t.yMin < -0.5f || t.xMax > Screen.width + 0.5f || t.yMax > Screen.height + 0.5f) bad.Add(s.ToString("0.00") + " outside the screen");
                if (menuBtn && !(t.yMin >= mb.yMax - 0.5f && Mathf.Abs(t.center.x - mb.center.x) < 1f)) bad.Add(s.ToString("0.00") + " not under MENU");
                if (top && Meet(t, Px(top))) bad.Add(s.ToString("0.00") + " meets the top strip");
                if (terr && Meet(t, Px(terr))) bad.Add(s.ToString("0.00") + " meets the territory bar");
                var fpsHost = FindFirstObjectByType<OzFpsChip>();
                var fpsChip = fpsHost ? fpsHost.transform.Find("OpenZ_FpsCanvas/Root/FpsChip") as RectTransform : null;
                var fpsCanvas = fpsChip ? fpsChip.GetComponentInParent<Canvas>() : null;
                if (fpsChip && fpsCanvas && fpsCanvas.enabled && fpsChip.gameObject.activeInHierarchy)
                {
                    fpsSteps++;
                    if (Meet(t, Px(fpsChip))) bad.Add(s.ToString("0.00") + " meets the FPS COUNTER chip (" + R(Px(fpsChip)) + ")");
                }
                if (Mathf.Approximately(s, 1f) || Mathf.Approximately(s, 1.6f)) yield return Shot($"level_L02_custom_tag_ui{Mathf.RoundToInt(s * 100)}_{size}");
            }
            if (OzQuality.FpsOn != oldFps) OzQuality.FpsOn = oldFps;
            OzText.UiScale = s0; hud.ApplyUiScale(); yield return Real(0.3f);
            Check($"S L02 the CUSTOM STATS tag under MENU, inside the screen, clear of the top strip, the territory bar and the FPS COUNTER chip (shown at {fpsSteps} of them) at the {steps} UI SCALE steps{(bad.Count > 0 ? ": " + string.Join("; ", bad) : "")}", bad.Count == 0 && steps == OzText.ScalePresets.Length);
            // W: the red side wins
            OpenZVictory.DebugFortFalls(true);
            yield return Until(() => hud && hud.EndScreen && hud.EndScreen.IsShown, 60f);
            yield return Real(3.5f);
            Check($"W L02 the end screen: \"{hud.EndScreen.ProofUnranked}\", no table place ({hud.EndScreen.ProofPlace})", hud.EndScreen.ProofUnranked == OzLevelStats.Unranked && hud.EndScreen.ProofPlace == 0);
            yield return Shot("level_L02_custom_end");
            // R: REPLAY
            OzScenes.Restart();
            yield return Until(() => ZGame.I && ZGame.I != g && ZGame.I.World != null, 60f);
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 3f, 30f);
            g = ZGame.I; W = g ? g.World : null;
            Check($"R L02 REPLAY keeps CUSTOM (rows {(W != null && W.CustomStatsInUse != null ? W.CustomStatsInUse.Rows.Count : 0)}, jeep row {(W != null ? W.S.Unit("jeep").MaxHealth : 0)})", OzLevelStats.InUse && W != null && W.S.Unit("jeep").MaxHealth == jeepHp);
            // B: back on the main menu
            OzScenes.MainMenu();
            yield return Until(() => FindFirstObjectByType<OpenZMainMenu>(), 60f);
            yield return Real(0.8f);
            Check($"B the main menu: CUSTOM ended (armed {OzLevelStats.Active}, switch {ZWorld.CustomStatsDeviation}, rows {(ZWorld.CustomStats != null ? "LEFT" : "none")}, rule values held {OzLevelStatsPlan.RulesHeld}), the choice still CUSTOM",
                  !OzLevelStats.Active && !ZWorld.CustomStatsDeviation && ZWorld.CustomStats == null && !OzLevelStatsPlan.RulesHeld && OzLevelStats.Box);
        }

        // ------------------------------------------------------------------------------------------------ O: OPENZ picked
        IEnumerator OpenZLevel()
        {
            var m = FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Check("O back on the main menu", false); yield break; }
            m.ProofSelect(); yield return Real(0.8f);
            m.ProofFocus(System.Array.FindIndex(OzLevelDef.All, d => d.Id == "L02")); yield return Real(0.5f);
            m.ProofSetUnitStats(false, false); yield return Real(0.3f);
            m.ProofDeploy();
            yield return Until(() => ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 90f);
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 4f, 30f);
            yield return Real(1f);
            var W = ZGame.I ? ZGame.I.World : null;
            string foot = Texts("Foot", "MissionBanner").FirstOrDefault() ?? "";
            Check($"O L02 OPENZ with the profile saved: the game's own jeep ({(W != null ? W.S.Unit("jeep").MaxHealth : 0)} HP), no Custom rows, no tag, the banner without CUSTOM (\"{Plain(foot)}\")",
                  W != null && W.CustomStatsInUse == null && W.S.Unit("jeep").MaxHealth == jeepHpOpenZ && !OzLevelStatsLevel.I && !Plain(foot).Contains(OzLevelStats.Tag) && !ZWorld.CustomStatsDeviation);
            yield return Shot("level_L02_openz_start");
            OzScenes.MainMenu();
            yield return Until(() => FindFirstObjectByType<OpenZMainMenu>(), 60f);
            yield return Real(0.8f);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");
        static string R(Rect r) => $"{r.xMin:0}..{r.xMax:0} x {r.yMin:0}..{r.yMax:0}";
        static bool Meet(Rect a, Rect b) => a.xMin < b.xMax - 0.5f && b.xMin < a.xMax - 0.5f && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;
        /// <summary>A rect of the menu canvas in its units (1080 u tall, top-left origin).</summary>
        static Rect CanvasRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            float k = OzLayout.RefH / Mathf.Max(1f, Screen.height);
            return Rect.MinMaxRect(c[0].x * k, (Screen.height - c[1].y) * k, c[2].x * k, (Screen.height - c[0].y) * k);
        }
        /// <summary>A rect in screen pixels (top-left origin; any canvas scale).</summary>
        static Rect Px(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, Screen.height - c[1].y, c[2].x, Screen.height - c[0].y);
        }
        static RectTransform Find(Transform root, string name)
        {
            if (!root) return null;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true)) if (t.name == name) return t;
            return null;
        }
        static string Text(Transform root, string name)
        {
            var t = Find(root, name); if (!t) return "";
            var tmp = t.GetComponent<TMPro.TMP_Text>(); if (tmp) return tmp.text ?? "";
            var leg = t.GetComponent<UnityEngine.UI.Text>(); return leg ? leg.text ?? "" : "";
        }
        static IEnumerable<string> Texts(string name, string parent)
        {
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && Under(t.transform, name, true) && Under(t.transform, parent, false)) yield return t.text ?? "";
            foreach (var t in FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && Under(t.transform, name, true) && Under(t.transform, parent, false)) yield return t.text ?? "";
        }
        static bool Under(Transform t, string n, bool self)
        {
            var p = self ? t : t.parent;
            for (int i = 0; i < 4 && p; i++, p = p.parent) if (p.name == n) return true;
            return false;
        }
    }
}
#endif
