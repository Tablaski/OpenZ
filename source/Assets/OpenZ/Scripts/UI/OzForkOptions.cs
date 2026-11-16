using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>Shared presentation of OpenZ-VK additions in every options menu.</summary>
    public static class OzForkOptions
    {
        public static readonly Color Accent = new Color32(213, 112, 255, 255);
        public const string ChatLabel = "CHAT SHORTCUT";
        public static bool BindingChat { get; private set; }
        static int bindingFrame = -1, closedFrame = -1;
        public static Key ChatKey => (Key)PlayerPrefs.GetInt("oz_duel_chat_key", (int)Key.Enter);
        public static string ChatName => BindingChat ? "PRESS A KEY · ESC CANCELS" : ChatKey.ToString().ToUpperInvariant();
        public static void BindChat(int unused = 0) { BindingChat = true; bindingFrame = Time.frameCount; }
        public static bool CaptureChatBinding()
        {
            if (closedFrame == Time.frameCount) return true;
            if (!BindingChat) return false;
            if (bindingFrame == Time.frameCount || Keyboard.current == null) return true;
            foreach (var key in Keyboard.current.allKeys)
                if (key.wasPressedThisFrame)
                {
                    if (key.keyCode != Key.Escape)
                    { PlayerPrefs.SetInt("oz_duel_chat_key", (int)key.keyCode); PlayerPrefs.Save(); }
                    BindingChat = false; closedFrame = Time.frameCount; break;
                }
            return true;
        }
        static bool IsCrippling(string label)
        {
            if (label == "CRIPPLING") return true;
            foreach (var def in Sim.ZBehaviorSchema.All) if (def.Group == "CRIPPLING" && def.Label == label) return true;
            return false;
        }
        public static bool IsFork(string label) => IsCrippling(label) || label == OzMapReadabilityOptions.FlagLabel || label == OzMapReadabilityOptions.BorderLabel ||
            label == OzBehaviorLibrary.TanksLabel || label == OzBehaviorLibrary.RobotsLabel || label == OzForceBarOption.Label || label == ChatLabel;
        public static int Compare(string a, string b)
        {
            int group = IsFork(a).CompareTo(IsFork(b));
            return group != 0 ? group : OzSettingTips.TabOrder(a).CompareTo(OzSettingTips.TabOrder(b));
        }
        public static void Highlight(HudButton row, string label)
        {
            if (!IsFork(label)) return;
            var stripe = Hud.Img(row.Rt, "OpenZ-VK", Accent); stripe.rectTransform.Fill(0, 3, 0, 3);
            stripe.rectTransform.anchorMax = new Vector2(0, 1); stripe.rectTransform.sizeDelta = new Vector2(3, -6);
            stripe.raycastTarget = false;
            var text = row.Content.Find("Label")?.GetComponent<UnityEngine.UI.Graphic>(); if (text) text.color = Accent;
        }
    }
}
