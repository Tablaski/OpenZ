using UnityEngine;
namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        void BuildConvertedMapsButton(RectTransform parent)
        {
            var button = HudButton.Make(parent, "ConvertedMaps", "Load Shitty maps", new Vector2(300, 58), HudButtonKind.Large, 17);
            button.Rt.Place(new OzRect(Mathf.Max(800, CW - 410), 110, 300, 58));
            var icon = Hud.Img(button.Rt, "Poop", Color.white);
            icon.sprite = OzConvertedMapsScreen.PoopSprite();
            icon.rectTransform.Place(new OzRect(8, 4, 48, 48));
            AddHot(button.Rt, View.Select, () => OzConvertedMapsScreen.Open(this), null, button, "ui_menu_select");
        }
    }
}
