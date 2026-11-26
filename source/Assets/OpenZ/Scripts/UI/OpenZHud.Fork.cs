using OpenZ.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    public partial class OpenZHud
    {
        RectTransform forkWordmark;
        void BuildForkHud()
        {
            var badge = forkWordmark = Hud.Node("OpenZ-VK", Root); badge.TL(16, 10, 112, 40);
            var wordmark = Hud.Kit(badge, "Wordmark", "Wordmarks/wm_openz_vk_menu", new OzRect(0, 0, 112, 40), null, Image.Type.Simple);
            wordmark.preserveAspect = true;
            wordmark.raycastTarget = false;
            wordmark.enabled = wordmark.sprite != null;
            if (!wordmark.sprite) Hud.Label(badge, "WordmarkFallback", "OpenZ-VK", 21, OzForkOptions.Brand, .08f).At(10, 4);
            if (!DuelRuntime.Active) return;
            var chat = HudButton.Make(cmdBar, "Chat", "CHAT", new Vector2(62, 48), HudButtonKind.Small, 11);
            chat.Rt.TL(718, 6, 62, 48); chat.OnClick = c => DuelRuntime.I.Menus.OpenChat();
            chat.Tooltip = "CHAT|Open text chat. Set its shortcut in OPTIONS > GAME.|";
            AddBlocker(chat.Rt);
        }
        void TickForkHud()
        {
            if (!IsGhostViewer && forkWordmark && forkWordmark.anchoredPosition != new Vector2(16, -10))
                forkWordmark.TL(16, 10, 112, 40);
        }

        internal void PlaceGhostWordmark(bool besideScore, float scoreRight, float bandBottom)
        {
            if (!forkWordmark) return;
            float right = ScreenToCanvas(new Vector2(scoreRight, 0)).x + 14;
            bool rightFits = right + 112 < CanvasWidth - 130;
            float x = besideScore && rightFits ? right : 16;
            float y = besideScore && !rightFits ? CanvasSize.y - ScreenToCanvas(new Vector2(0, bandBottom)).y + 8 : 10;
            if (forkWordmark.anchoredPosition != new Vector2(x, -y)) forkWordmark.TL(x, y, 112, 40);
        }
    }
}
