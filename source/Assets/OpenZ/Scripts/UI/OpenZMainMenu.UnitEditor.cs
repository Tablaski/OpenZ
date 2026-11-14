using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// UNIT EDITOR item, right after PLAY (the one line in BuildMain, hint "PLAYGROUND · ALL UNITS · STATS", Q10), and its own loading
    /// screen in the menu's style (guide 9.16, as for a level: the eyebrow "PREPARING THE PLAYGROUND", the name, the line, the bar and
    /// its sounds). The playground is not a level card (it is not in OzLevelDef.All), so this partial builds its own loading root and
    /// hands it to the menu's TickLoading (view / loadOp / loadStartAt / loadShown and the loading fields): the bar, the percentage,
    /// the stage sounds and the scene activation stay the menu's. TickLoading writes the level words (TERRAIN / FORCES / COMMANDERS,
    /// the group-key tips) into two hidden labels; LateUpdate shows the playground's own words. oz_botmatch is cleared as DEPLOY does.
    /// </summary>
    public partial class OpenZMainMenu
    {
        static readonly string[] UeStages = { "LOADING THE PLAYGROUND", "LOADING THE UNITS", "LOADING THE STATS" };
        static readonly string[] UeTips = { "DRAG UNITS UP FROM THE DOCK", "PICK UNITS OF EITHER TEAM · RIGHT-CLICK GIVES ORDERS", "HOLD THE MIDDLE BUTTON TO TURN THE CAMERA" };
        const string UeHint = "PLAYGROUND · ALL UNITS · STATS";
        bool ueLoading; OzText ueStage, ueTip, ueError;
        float ueErrorUntil = -1f; string ueHintShown;
        /// <summary>Checks: UNIT EDITOR clicks that started the playground's loading.</summary>
        public static int UnitEditorStarts { get; private set; }

        void GoUnitEditor()
        {
            if (view != View.Main) return;
            string scene = OzUnitEditor.SceneName;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Sfx("ui_error");
                UeMenuError("THE PLAYGROUND IS MISSING");                                                    // fits the hint's 360 u; the cause goes to the log
                Debug.LogError("[OpenZ Menu] the scene " + scene + " is not in the build settings (menu OpenZ/V1.2/Unit Editor/1 Build Playground Scene)");
                return;
            }
            if (PlayerPrefs.GetInt("oz_botmatch", 0) == 1) { PlayerPrefs.SetInt("oz_botmatch", 0); Debug.Log("[OpenZ Menu] oz_botmatch was left on by a test: cleared, the playground has no CPU commander"); }
            PlayerPrefs.Save();
            CloseSide();
            Sfx("ui_level_start");
            Debug.Log("[OpenZ Menu] UNIT EDITOR: loading the playground (" + scene + ")");
            UnitEditorStarts++;
            view = View.Loading; viewAt = OzMotion.Now; ueLoading = true;
            if (hotOver?.btn != null) { hotOver.btn.Hovered = false; hotOver.btn.Refresh(); }
            BuildUnitEditorLoading();
            if (previews != null) foreach (var p in previews) p?.Pause();
            loadStartAt = OzMotion.Now; loadShown = 0f;
            loadOp = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (loadOp != null) loadOp.allowSceneActivation = false;
            else { Debug.LogWarning("[OpenZ Menu] async load refused: loading " + scene + " directly"); SceneManager.LoadScene(scene); }
        }

        /// <summary>The loading screen of guide 9.16 for the playground: the same block, bar and type as a level's, on the menu's dark bed.</summary>
        void BuildUnitEditorLoading()
        {
            loadingRoot = Hud.Node("Loading", root); loadingRoot.Fill();
            loadingCg = loadingRoot.gameObject.AddComponent<CanvasGroup>(); loadingCg.alpha = 0f;
            var bed = Hud.Img(loadingRoot, "Bed", OzUi.GM950); bed.rectTransform.Fill();
            var vg = Hud.Kit(loadingRoot, "Vignette", "Backdrops/bd_vignette", new OzRect(0, 0, 10, 10), null, UnityEngine.UI.Image.Type.Simple); vg.rectTransform.Fill();
            vg.enabled = OzKit.GetSprite("Backdrops/bd_vignette") != null;
            float x = OzMenuLayout.LoadX, w = OzMenuLayout.LoadW;
            OzAnniversaryBadge.Build(loadingRoot, x, OzMenuLayout.LoadTop - 46, 11f);                         // Z turns 30, as on every loading screen
            Hud.Label(loadingRoot, "Eyebrow", "PREPARING THE PLAYGROUND", 12, OzUi.Amber, 0.24f).At(x, OzMenuLayout.LoadTop);
            Hud.Label(loadingRoot, "Wordmark", "UNIT EDITOR", 132, OzUi.Ink, -0.02f).At(x, OzMenuLayout.LoadWordY);   // the level name's place and size (cap 96 u)
            Hud.Label(loadingRoot, "Goal", UeHint, 16, OzUi.Ink2, 0.02f).At(x, OzMenuLayout.LoadGoalY, OzH.L, false, 700);
            ueStage = Hud.Label(loadingRoot, "Stage", UeStages[0], 14, OzUi.Ink2, 0.12f).At(x, OzMenuLayout.LoadMeterY, OzH.L, false, w);
            loadPct = Hud.Label(loadingRoot, "Pct", "0%", 14, OzUi.Amber, 0.04f).At(x + w, OzMenuLayout.LoadMeterY, OzH.R, false, 120);
            var track = Hud.Node("Bar", loadingRoot).Place(new OzRect(x, OzMenuLayout.LoadBarY, w, OzMenuLayout.LoadBarH));
            var tb = Hud.Kit(track, "Track", "Bars/bar_track", new OzRect(0, 0, w, OzMenuLayout.LoadBarH), null); tb.rectTransform.Fill();
            if (!OzKit.Available) tb.color = OzUi.GM950;
            loadFill = OzKit.Available ? OzKit.FillBar(track, "Fill", "amber") : Hud.Img(track, "Fill", OzUi.Amber);
            loadFill.rectTransform.Fill(); loadFill.type = UnityEngine.UI.Image.Type.Filled; loadFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; loadFill.fillAmount = 0f;
            ueTip = Hud.Label(loadingRoot, "Tip", UeTips[0], 14, OzUi.Muted, 0.12f).At(x, OzMenuLayout.LoadTipY, OzH.L, false, 900);
            var sink = Hud.Node("LevelWords", loadingRoot); sink.gameObject.SetActive(false);                // TickLoading's level words land here, unseen
            loadStage = Hud.Label(sink, "Stage", "", 14, OzUi.Ink2, 0.12f);
            loadTip = Hud.Label(sink, "Tip", "", 14, OzUi.Muted, 0.12f);
            loadingRoot.SetAsLastSibling();
        }

        void LateUpdate()
        {
            if (ueLoading && view == View.Loading && ueStage != null)
            {
                int st = loadShown < 0.48f ? 0 : loadShown < 0.85f ? 1 : 2;                                   // the same steps as TickLoading's
                ueStage.Set(UeStages[st]); ueTip.Set(UeTips[st]);
            }
            if (ueErrorUntil > 0f && OzMotion.Now > ueErrorUntil)
            {
                ueErrorUntil = -1f;
                if (ueError != null && ueError.rectTransform) { ueError.Set(ueHintShown ?? UeHint); ueError.color = OzUi.Amber; }   // a menu rebuilt since then made a new hint already
                ueError = null;
            }
        }

        /// <summary>The item's amber hint shows the problem for 6 s (the main menu has no error line of its own).</summary>
        void UeMenuError(string text)
        {
            var it = items.Find(i => i.label == "UNIT EDITOR");
            if (it == null || it.sub == null) return;
            ueError = it.sub; ueHintShown = it.hint;
            ueError.Set(text); ueError.color = OzUi.AlertText;
            ueErrorUntil = OzMotion.Now + 6f;
        }

        // ------------------------------------------------------------------ Play proof (View/OzUeProof.cs)
        /// <summary>The UNIT EDITOR item: its centre on the screen, its index and PLAY's, and its hit rect's bottom (canvas u from the top).</summary>
        public bool ProofUnitEditorItem(out Vector2 screen, out int index, out int playIndex, out float bottom)
        {
            screen = default; index = items.FindIndex(i => i.label == "UNIT EDITOR"); playIndex = items.FindIndex(i => i.label == "PLAY"); bottom = -1f;
            if (index < 0) return false;
            var hit = items[index].hit;
            if (!hit) return false;
            var c = new Vector3[4]; hit.GetWorldCorners(c);
            screen = RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f);
            bottom = OzMenuLayout.ItemY0 + OzMenuLayout.ItemStep * index + OzMenuLayout.ItemHitDY + OzMenuLayout.ItemHitH;
            return true;
        }

        /// <summary>The menu is showing its main list (not the level select, not loading).</summary>
        public bool ProofOnMainList => view == View.Main;
    }
}
