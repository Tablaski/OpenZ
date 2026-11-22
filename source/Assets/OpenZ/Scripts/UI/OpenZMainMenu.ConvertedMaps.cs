using System;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        HudButton soloAllowedButton, soloGamingButton;
        HudButton catalogButton;
        bool aiCampaign;
        bool[] aiPreviewVisible;
        const float CatalogHeight = 48f;
        float CatalogWidth => CW < 1600f ? 214f : 280f;
        const string OriginalMaps = "Z ORIGINAL", ExpansionMaps = "Z EXPANSION", ZodMaps = "ZOD ENGINE";
        static readonly string[] AiGroups = { OriginalMaps, ExpansionMaps, ZodMaps };
        readonly string[] selectedAiMaps = new string[AiGroups.Length];
        int officialCard, aiGroup;
        internal bool ShowingAiMaps => aiCampaign;

        void BuildSelectionWordmark(RectTransform parent)
        {
            var logo = Hud.Kit(parent, "ForkWordmark", "Wordmarks/wm_openz_vk_menu",
                new OzRect(CW - 226, 10, 200, 66), null, UnityEngine.UI.Image.Type.Simple);
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            if (!logo.sprite) Hud.Label(parent, "ForkWordmarkFallback", "OpenZ-VK", 28, OzForkOptions.Brand)
                .At(CW - 216, 28);
        }

        void BuildConvertedMapsButton(RectTransform parent)
        {
            var back = OzMenuLayout.Back;
            string caption = aiCampaign ? "‹  OPENZ CAMPAIGN" : "AI MAPS";
            var button = HudButton.Make(parent, "MapCatalog", caption, new Vector2(CatalogWidth, CatalogHeight),
                HudButtonKind.Large, 21, 0.08f, HudButtonStyle.Primary);
            catalogButton = button;
            button.Rt.Place(new OzRect(OzAnchor.TR, back.x - HsBtnGap - CatalogWidth,
                back.y + (back.h - CatalogHeight) * 0.5f, CatalogWidth, CatalogHeight));
            if (!aiCampaign)
            {
                button.Label.FillParent(OzH.C, 40, 0, 0, 0);
                OzAiMapBadge.Head(button.Content, new OzRect((CatalogWidth - button.Label.preferredWidth) * 0.5f - 18f, 8, 32, 32));
            }
            AddHot(button.Rt, View.Select, () => SwitchCampaign(!aiCampaign), null, button, "ui_menu_select");
        }

        void BuildSoloSettings(RectTransform parent)
        {
            const float gap = 8f, height = 46f;
            float right = deployLay.X(0, 3) - gap;
            float left = cardRt.anchoredPosition.x + cardRt.rect.width + gap;
            bool extraRow = right - left < 368f;
            float width = extraRow ? 218f : Mathf.Min(218f, (right - left - gap) * 0.5f);
            float x = extraRow ? (CW - 2 * width - gap) * 0.5f : right - 2 * width - gap;
            float y = extraRow ? 914f : OzDeployBar.DeployY;
            if (extraRow)
            {
                var plate = Hud.Kit(parent, "Solo settings plate", "Select/deploy_bar", new OzRect(0, 906, CW, 56), null);
                if (!plate.sprite) plate.color = OzUi.GM800;
                plate.raycastTarget = false;
            }

            const string label = OzBattleOptions.SoloLabel;
            soloGamingButton = HudButton.Make(parent, "SoloGamingOptions", label, new Vector2(width, height), HudButtonKind.Large, 15, .04f);
            soloGamingButton.Rt.Place(new OzRect(x, y, width, height));
            soloGamingButton.Label.FillParent(OzH.C, 16, 0, 16, 0);
            OzForkOptions.Highlight(soloGamingButton, label);
            AddHot(soloGamingButton.Rt, View.Select, OzBehaviorOptions.ShowGaming, null, soloGamingButton, "ui_menu_select");

            soloAllowedButton = HudButton.Make(parent, "SoloAllowedUnits", "ALLOWED UNITS", new Vector2(width, height), HudButtonKind.Large, 15, .04f);
            soloAllowedButton.Rt.Place(new OzRect(x + width + gap, y, width, height));
            soloAllowedButton.Label.FillParent(OzH.C, 16, 0, 16, 0);
            OzForkOptions.Highlight(soloAllowedButton, OzSoloAllowedUnits.Label);
            AddHot(soloAllowedButton.Rt, View.Select, () => OzSoloAllowedUnits.Show(null), null, soloAllowedButton, "ui_menu_select");

            if (!aiCampaign) return;

            float tabWidth = Mathf.Min(216f, CW * 0.16f);
            float tabsX = (CW - AiGroups.Length * tabWidth) * 0.5f;
            float tabsY = extraRow ? 852f : 886f;
            for (int i = 0; i < AiGroups.Length; i++)
            {
                int group = i;
                var tab = HudButton.Make(parent, "AI category " + AiGroups[i], AiGroups[i], new Vector2(tabWidth, height),
                    HudButtonKind.Large, 17, 0.08f, i == aiGroup ? HudButtonStyle.Primary : HudButtonStyle.Normal);
                tab.Rt.Place(new OzRect(tabsX + tabWidth * i, tabsY, tabWidth, height));
                AddHot(tab.Rt, View.Select, () => SwitchCampaign(true, group), null, tab, "ui_menu_select");
            }

            float techWidth = Mathf.Min(230f, tabsX - deployLay.m - gap);
            float techX = deployLay.m;
            float techY = tabsY;
            if (techWidth < 135f)
            {
                techWidth = 160f;
                techX = (CW - techWidth) * 0.5f;
                techY = tabsY - height - gap;
            }
            var tech = HudButton.Make(parent, "AiMapTech", OzSoloMapTech.Label, new Vector2(techWidth, height), HudButtonKind.Large, 15, .04f);
            tech.Rt.Place(new OzRect(techX, techY, techWidth, height));
            OzForkOptions.Highlight(tech, OzSoloMapTech.Label);
            AddHot(tech.Rt, View.Select, () => OzSoloMapTech.Show(levels[focusCard].Id), null, tech, "ui_menu_select");
        }

        static int AiGroupOf(OzAiMap map) => map.ForkOriginal ? 0 : map.Number == 0 ? 2 : map.Number > 20 ? 1 : 0;

        static OzAiMap[] SoloAiGroup(int group) => OzAiMapCatalog.Select(true)
            .Where(map => AiGroupOf(map) == group).ToArray();

        void SetCampaignLevels(bool converted)
        {
            aiCampaign = converted;
            levels = converted ? SoloAiGroup(aiGroup)
                .Select(OzAiMapGallery.Definition).ToArray() : OzLevelDef.All;
            int count = levels.Length;
            aiPreviewVisible = converted ? new bool[count] : null;
            cardPeekK = new float[count];
            liveK = new float[count];
            cardHoverK = new float[count];
            cardLiveK = new float[count];
            lw = new float[count];
            bgPoster = new UnityEngine.UI.RawImage[count];
            bgLive = new UnityEngine.UI.RawImage[count];
            bgBlur = new UnityEngine.UI.RawImage[count];
            cards = new Card[count];
        }

        void InitializeCampaignPreviews()
        {
            infos = new OzLevelInfo[levels.Length];
            previews = new OzLevelPreview[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                infos[i] = OzLevelInfo.Load(levels[i].Id);
                previews[i] = OzLevelPreview.Create(transform, levels[i].Id);
            }
        }

        void ReleaseCampaignPreviews()
        {
            if (previews != null) foreach (var preview in previews) preview?.Release();
            if (infos != null) foreach (var info in infos) if (info != null && info.Mini) Destroy(info.Mini);
        }

        void SwitchCampaign(bool converted, int group = -1)
        {
            if (group < 0) group = aiGroup;
            if (converted == aiCampaign && (!converted || group == aiGroup)) return;
            if (aiCampaign) selectedAiMaps[aiGroup] = levels[focusCard].Id;
            else officialCard = focusCard;
            ReleaseCampaignPreviews();
            aiGroup = group;
            SetCampaignLevels(converted);
            focusCard = converted ? Mathf.Max(0, Array.FindIndex(levels, level => level.Id == selectedAiMaps[aiGroup])) : officialCard;
            hoverCard = bgLevel = bgFrom = -1;
            lastCardClick = -1;
            carFirst = 0;
            hsOpen = false;
            hsBlocks = null;
            InitializeCampaignPreviews();
            if (root) root.gameObject.SetActive(false);
            Build();
            FocusCard(focusCard, true, true);
        }

        void TickAiCardPreview(int i)
        {
            bool visible = view == View.Select && i == focusCard;
            var preview = previews[i];
            if (visible != aiPreviewVisible[i])
            {
                aiPreviewVisible[i] = visible;
                if (!visible) preview.Pause();
                else if (i != bgLevel) preview.PlayFrom(-1, true);
            }
            if (visible) preview.StallGuard();
        }

        public void ProofOpenSoloAllowed() =>
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, soloAllowedButton.Rt.TransformPoint(soloAllowedButton.Rt.rect.center)));

        internal void ProofOpenSoloDynamics(ZBehaviorFamily family)
        {
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, soloGamingButton.Rt.TransformPoint(soloGamingButton.Rt.rect.center)));
            string label = family == ZBehaviorFamily.Tanks ? OzBehaviorLibrary.TanksLabel : OzBehaviorLibrary.RobotsLabel;
            FindFirstObjectByType<OzBehaviorOptions>().GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(button => button.name == label).onClick.Invoke();
        }

        internal void ProofSwitchMapCatalog() =>
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, catalogButton.Rt.TransformPoint(catalogButton.Rt.rect.center)));

        internal bool ProofAiPreviewPlaying => aiCampaign && previews[focusCard].ShowingVideo &&
            previews[focusCard].Player.isLooping && cards[focusCard].rt.Find("AI stamp");
    }
}
