using OpenZ.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    public partial class OpenZHud
    {
        HudButton forkMicrophone;
        RectTransform forkMicrophoneMenu;
        Image forkMicrophoneMeter;
        void BuildForkHud()
        {
            var badge = Hud.Node("OpenZ-VK", Root); badge.TL(16, 10, 154, 30);
            var back = Hud.Img(badge, "Plate", OzUi.A(OzUi.GM950, .9f)); back.rectTransform.Fill();
            var wordmark = Hud.Kit(badge, "Wordmark", "Wordmarks/wm_openz_vk_menu", new OzRect(4, 1, 146, 28), null, Image.Type.Simple);
            wordmark.preserveAspect = true; wordmark.enabled = wordmark.sprite != null;
            if (!wordmark.sprite) Hud.Label(badge, "WordmarkFallback", "OpenZ-VK", 21, OzForkOptions.Brand, .08f).At(10, 4);
            if (!DuelRuntime.Active) return;
            var chat = HudButton.Make(cmdBar, "Chat", "CHAT", new Vector2(62, 48), HudButtonKind.Small, 11);
            chat.Rt.TL(718, 6, 62, 48); chat.OnClick = c => DuelRuntime.I.Menus.OpenChat();
            chat.Tooltip = "CHAT|Open text chat. Set its shortcut in OPTIONS > GAME.|";
            AddBlocker(chat.Rt);
            forkMicrophone = HudButton.Make(cmdBar, "Microphone", "", new Vector2(150, 48), HudButtonKind.Small, 10);
            forkMicrophone.Rt.TL(786, 6, 150, 48); AddBlocker(forkMicrophone.Rt);
            forkMicrophone.OnClick = c => forkMicrophoneMenu.gameObject.SetActive(!forkMicrophoneMenu.gameObject.activeSelf);
            var track = Hud.Img(forkMicrophone.Content, "InputTrack", OzUi.GM950); track.rectTransform.TL(8, 38, 134, 5);
            forkMicrophoneMeter = Hud.Img(track.transform, "InputLevel", OzForkOptions.Accent); forkMicrophoneMeter.rectTransform.Fill();
            forkMicrophoneMenu = Hud.Node("MicrophoneModes", cmdBar); forkMicrophoneMenu.TL(786, -154, 150, 154);
            AddBlocker(forkMicrophoneMenu);
            var plate = Hud.Img(forkMicrophoneMenu, "Plate", OzUi.GM950); plate.rectTransform.Fill();
            string[] names = { "ALWAYS ON", "TAP TO SPEAK", "MUTE" };
            DuelVoice.MicrophoneMode[] modes = { DuelVoice.MicrophoneMode.AlwaysOn, DuelVoice.MicrophoneMode.TapToSpeak, DuelVoice.MicrophoneMode.Mute };
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                var b = HudButton.Make(forkMicrophoneMenu, "Mode" + mode, names[i], new Vector2(142, 44), HudButtonKind.Small, 11);
                b.Rt.TL(4, 4 + i * 50, 142, 44); b.LabelColor = OzForkOptions.Accent; b.Refresh();
                b.OnClick = c => { DuelRuntime.I.Voice.SetMode(mode); forkMicrophoneMenu.gameObject.SetActive(false); };
            }
            forkMicrophoneMenu.gameObject.SetActive(false);
        }
        void TickForkHud()
        {
            if (forkMicrophone == null || !DuelRuntime.I) return;
            var voice = DuelRuntime.I.Voice;
            forkMicrophone.Label.Set("MIC: " + voice.ModeName);
            forkMicrophone.Tooltip = "MICROPHONE|Always on: transmit continuously. Tap to speak: tap " + voice.TalkKey + " to start or stop. Mute: no voice is sent. The bar shows local input after gain.|";
            bool visible = forkMicrophone.Rt.gameObject.activeInHierarchy && !OzConfirm.Blocks;
            voice.HudMonitoring = visible;
            if (!visible) forkMicrophoneMenu.gameObject.SetActive(false);
            forkMicrophoneMeter.rectTransform.anchorMax = new Vector2(voice.InputLevel, 1);
            forkMicrophoneMeter.color = voice.InputLevel > .95f ? OzUi.AlertText : OzForkOptions.Accent;
        }
    }
}
