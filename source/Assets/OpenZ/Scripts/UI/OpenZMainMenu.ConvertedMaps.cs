using System;
using System.Linq;
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        HudButton soloAllowedButton;
        HudButton catalogButton;
        bool aiCampaign;
        int officialCard, aiCard;

        void BuildConvertedMapsButton(RectTransform parent)
        {
            var caption = aiCampaign ? "OpenZ Campaign" : "AI Maps";
            var button = HudButton.Make(parent, "MapCatalog", caption, new Vector2(300, 58), HudButtonKind.Large, 17);
            catalogButton = button;
            button.Rt.Place(new OzRect(Mathf.Max(800, CW - 410), 210, 300, 58));
            AddHot(button.Rt, View.Select, () => SwitchCampaign(!aiCampaign), null, button, "ui_menu_select");

            var allowed = OzSoloAllowedUnits.Read();
            soloAllowedButton = HudButton.Make(parent, "SoloAllowedUnits",
                "Allowed units · " + Multiplayer.DuelUnitRoster.Name(allowed.UnitPreset), new Vector2(340, 58), HudButtonKind.Large, 17);
            soloAllowedButton.Rt.Place(new OzRect(Mathf.Max(440, CW - 780), 210, 340, 58));
            OzForkOptions.Highlight(soloAllowedButton, OzSoloAllowedUnits.Label);
            AddHot(soloAllowedButton.Rt, View.Select, () => OzSoloAllowedUnits.Show(() =>
            {
                if (soloAllowedButton != null && soloAllowedButton.Rt)
                    soloAllowedButton.Label.Set("Allowed units · " + Multiplayer.DuelUnitRoster.Name(OzSoloAllowedUnits.Read().UnitPreset));
            }), null, soloAllowedButton, "ui_menu_select");
        }

        void SetCampaignLevels(bool converted)
        {
            aiCampaign = converted;
            levels = converted ? OzAiMapCatalog.Select(true).Select(OzAiMapGallery.Definition).ToArray() : OzLevelDef.All;
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

        void SwitchCampaign(bool converted)
        {
            if (converted == aiCampaign) return;
            if (aiCampaign) aiCard = focusCard;
            else officialCard = focusCard;
            ReleaseCampaignPreviews();
            SetCampaignLevels(converted);
            focusCard = Mathf.Clamp(converted ? aiCard : officialCard, 0, levels.Length - 1);
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

        internal void ProofSwitchMapCatalog() =>
            ProofClickAt(RectTransformUtility.WorldToScreenPoint(null, catalogButton.Rt.TransformPoint(catalogButton.Rt.rect.center)));

        internal bool ProofAiPreviewPlaying => aiCampaign && previews[focusCard].ShowingVideo &&
            previews[focusCard].Player.isLooping && cards[focusCard].rt.Find("AI stamp");
    }
}
