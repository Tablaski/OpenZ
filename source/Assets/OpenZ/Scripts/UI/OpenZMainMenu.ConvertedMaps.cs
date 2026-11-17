using UnityEngine;
namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        HudButton soloAllowedButton;
        void BuildConvertedMapsButton(RectTransform parent)
        {
            var button = HudButton.Make(parent, "ConvertedMaps", "Shitty Maps", new Vector2(300, 58), HudButtonKind.Large, 17);
            button.Rt.Place(new OzRect(Mathf.Max(800, CW - 410), 210, 300, 58));
            button.Label.FillParent(OzH.C, 64, 0, 10, 0);
            var icon = Hud.Img(button.Content, "Poop", Color.white);
            icon.sprite = OzConvertedMapsScreen.PoopSprite();
            icon.rectTransform.Place(new OzRect(16, 9, 40, 40));
            icon.raycastTarget = false;
            AddHot(button.Rt, View.Select, () => OzConvertedMapsScreen.Open(this), null, button, "ui_menu_select");
            soloAllowedButton=HudButton.Make(parent,"SoloAllowedUnits","Allowed units · "+Multiplayer.DuelUnitRoster.Name(OzSoloAllowedUnits.Read().UnitPreset),new Vector2(340,58),HudButtonKind.Large,17);
            soloAllowedButton.Rt.Place(new OzRect(Mathf.Max(440,CW-780),210,340,58));
            OzForkOptions.Highlight(soloAllowedButton,OzSoloAllowedUnits.Label);
            AddHot(soloAllowedButton.Rt,View.Select,()=>OzSoloAllowedUnits.Show(()=>{if(soloAllowedButton!=null&&soloAllowedButton.Rt)soloAllowedButton.Label.Set("Allowed units · "+Multiplayer.DuelUnitRoster.Name(OzSoloAllowedUnits.Read().UnitPreset));}),null,soloAllowedButton,"ui_menu_select");
        }
        public void ProofOpenSoloAllowed()=>ProofClickAt(RectTransformUtility.WorldToScreenPoint(null,soloAllowedButton.Rt.TransformPoint(soloAllowedButton.Rt.rect.center)));
    }
}
