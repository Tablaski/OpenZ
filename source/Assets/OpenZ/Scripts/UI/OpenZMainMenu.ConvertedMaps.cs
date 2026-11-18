using System;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        HudButton soloAllowedButton, soloTankDynamicsButton, soloRobotDynamicsButton;
        HudButton catalogButton;
        bool aiCampaign, experimentalCampaign;
        int officialCard, aiCard, experimentalCard;

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
            var caption = aiCampaign ? "OpenZ Campaign" : "AI Maps";
            var button = HudButton.Make(parent, "MapCatalog", caption, new Vector2(300, 58), HudButtonKind.Large, 17);
            catalogButton = button;
            button.Rt.Place(new OzRect(CW - 326, 210, 300, 58));
            AddHot(button.Rt, View.Select, () => SwitchCampaign(!aiCampaign), null, button, "ui_menu_select");

            if (aiCampaign)
            {
                var category = HudButton.Make(parent, "AiMapCategory", experimentalCampaign ? "All other AI Maps" : "Experimental",
                    new Vector2(240, 46), HudButtonKind.Large, 17);
                category.Rt.Place(new OzRect(740, 108, 240, 46));
                AddHot(category.Rt, View.Select, () => SwitchCampaign(true, !experimentalCampaign), null, category, "ui_menu_select");
            }

            var allowed = OzSoloAllowedUnits.Read();
            soloAllowedButton = HudButton.Make(parent, "SoloAllowedUnits",
                "Allowed units · " + Multiplayer.DuelUnitRoster.Name(allowed.UnitPreset), new Vector2(340, 58), HudButtonKind.Large, 17);
            soloAllowedButton.Rt.Place(new OzRect(CW - 682, 210, 340, 58));
            OzForkOptions.Highlight(soloAllowedButton, OzSoloAllowedUnits.Label);
            AddHot(soloAllowedButton.Rt, View.Select, () => OzSoloAllowedUnits.Show(() =>
            {
                if (soloAllowedButton != null && soloAllowedButton.Rt)
                    soloAllowedButton.Label.Set("Allowed units · " + Multiplayer.DuelUnitRoster.Name(OzSoloAllowedUnits.Read().UnitPreset));
            }), null, soloAllowedButton, "ui_menu_select");
            float width = Mathf.Min(304, (CW - 730) / 2);
            soloTankDynamicsButton = BuildSoloDynamicsButton(parent, ZBehaviorFamily.Tanks, CW - 698 - width, width);
            soloRobotDynamicsButton = BuildSoloDynamicsButton(parent, ZBehaviorFamily.Robots, CW - 714 - 2 * width, width);
        }

        HudButton BuildSoloDynamicsButton(RectTransform parent, ZBehaviorFamily family, float x, float width)
        {
            string label = family == ZBehaviorFamily.Tanks ? OzBehaviorLibrary.TanksLabel : OzBehaviorLibrary.RobotsLabel;
            string Caption() => label + " · " + OzBehaviorLibrary.Selected(family).name;
            var button = HudButton.Make(parent, label, Caption(), new Vector2(width, 58), HudButtonKind.Large, 16);
            button.Rt.Place(new OzRect(x, 210, width, 58));
            OzForkOptions.Highlight(button, label);
            AddHot(button.Rt, View.Select, () => OzBehaviorOptions.Show(family, () => button.Label.Set(Caption())),
                null, button, "ui_menu_select");
            return button;
        }

        void SetCampaignLevels(bool converted, bool experimental = false)
        {
            aiCampaign = converted;
            experimentalCampaign = experimental;
            levels = converted ? OzAiMapCatalog.Select(true).Where(map => map.Experimental == experimental)
                .Select(OzAiMapGallery.Definition).ToArray() : OzLevelDef.All;
            int count = levels.Length;
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

        void SwitchCampaign(bool converted, bool experimental = false)
        {
            if (converted == aiCampaign && experimental == experimentalCampaign) return;
            if (experimentalCampaign) experimentalCard = focusCard;
            else if (aiCampaign) aiCard = focusCard;
            else officialCard = focusCard;
            ReleaseCampaignPreviews();
            SetCampaignLevels(converted, experimental);
            focusCard = Mathf.Clamp(experimental ? experimentalCard : converted ? aiCard : officialCard, 0, levels.Length - 1);
            hoverCard = bgLevel = bgFrom = -1;
            carFirst = 0;
            hsOpen = false;
            hsBlocks = null;
            InitializeCampaignPreviews();
            if (root) root.gameObject.SetActive(false);
            Build();
            FocusCard(focusCard, true, true);
        }

        public void ProofOpenSoloAllowed() =>
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, soloAllowedButton.Rt.TransformPoint(soloAllowedButton.Rt.rect.center)));

        internal void ProofOpenSoloDynamics(ZBehaviorFamily family)
        {
            var button = family == ZBehaviorFamily.Tanks ? soloTankDynamicsButton : soloRobotDynamicsButton;
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, button.Rt.TransformPoint(button.Rt.rect.center)));
        }

        internal void ProofSwitchMapCatalog() =>
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, catalogButton.Rt.TransformPoint(catalogButton.Rt.rect.center)));

        internal bool ProofAiPreviewPlaying => aiCampaign && previews[focusCard].ShowingVideo &&
            previews[focusCard].Player.isLooping && cards[focusCard].rt.Find("AI stamp");
    }
}
