using UnityEngine;
namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        void BuildConvertedMapsButton(RectTransform parent)
        {
            var button = HudButton.Make(parent, "ConvertedMaps", "Load Shitty maps", new Vector2(300, 58), HudButtonKind.Large, 17);
            button.Rt.Place(new OzRect(Mathf.Max(800, CW - 410), 210, 300, 58));
            button.Label.FillParent(OzH.C, 64, 0, 10, 0);
            var icon = Hud.Img(button.Content, "Poop", Color.white);
            icon.sprite = OzConvertedMapsScreen.PoopSprite();
            icon.rectTransform.Place(new OzRect(16, 9, 40, 40));
            icon.raycastTarget = false;
            AddHot(button.Rt, View.Select, () => OzConvertedMapsScreen.Open(this), null, button, "ui_menu_select");
        }
    }
}
