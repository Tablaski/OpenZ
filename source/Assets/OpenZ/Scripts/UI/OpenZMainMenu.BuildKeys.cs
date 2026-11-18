using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    internal static class OzBuildKeys
    {
        internal const string Label = "BUILD KEYS";
        internal static readonly string[] Units =
        {
            "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot",
            "jeep", "light", ZMTank.Type, "heavy", "apc", "missile_launcher", "crane", "medium",
            "gatling", "gun", "howitzer", "missile_cannon"
        };
        static readonly string DefaultLetters = "GPS TYL EJIMH AOUN KRWC".Replace(" ", "");
        internal static string Preference(string type) => "oz_build_key_" + type;
        internal static Key Get(string type)
        {
            int index = Array.IndexOf(Units, type);
            if (index < 0) return Key.None;
            string letter = DefaultLetters[index].ToString();
            var defaultKey = Keyboard.current?.FindKeyOnCurrentKeyboardLayout(letter)?.keyCode ??
                (Key)((int)Key.A + DefaultLetters[index] - 'A');
            return (Key)PlayerPrefs.GetInt(Preference(type), (int)defaultKey);
        }
        internal static void Set(string type, Key key)
        {
            foreach (string unit in Units)
                if (unit != type && key != Key.None && Get(unit) == key) PlayerPrefs.SetInt(Preference(unit), (int)Key.None);
            PlayerPrefs.SetInt(Preference(type), (int)key);
            PlayerPrefs.Save();
        }
        internal static bool Modifier(Keyboard keyboard) => keyboard.shiftKey.isPressed && !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed;
        internal static bool IsLetter(Key key)
        {
            if (key == Key.None || OzBindings.Reserved(key) != null) return false;
            string name = Keyboard.current?[key]?.displayName;
            return name != null && name.Length == 1 && char.IsLetter(name[0]);
        }
    }

    public partial class OpenZMainMenu
    {
        RectTransform buildKeysPage;
        string bindingBuildUnit;
        int buildBindingFrame;
        OzText buildKeyHelp;
        readonly System.Collections.Generic.List<(string type, HudButton button)> buildKeyButtons = new();

        void BuildConstructionOptions(RectTransform panel)
        {
            buildKeyButtons.Clear();
            buildKeysPage = Hud.Node("Build Keys", panel);
            buildKeysPage.Fill();
            float width = (OptW - 3 * OzLayout.OptX) / 2;
            for (int i = 0; i < OzBuildKeys.Units.Length; i++)
            {
                string type = OzBuildKeys.Units[i];
                float x = OzLayout.OptX + i / 10 * (width + OzLayout.OptX);
                var row = SmallButton(buildKeysPage, "Build " + type, "", new OzRect(x, 110 + i % 10 * 37, width, 32),
                    View.Main, Side.Options, () => { bindingBuildUnit = type; buildBindingFrame = Time.frameCount; RefreshBuildKeys(); }, 11, .02f);
                OzForkOptions.Highlight(row, OzBuildKeys.Label);
                buildKeyButtons.Add((type, row));
            }
            buildKeyHelp = Hud.Label(buildKeysPage, "Help", "", 13, OzUi.Muted, .02f).At(OzLayout.OptX, 492, OzH.L, false, OptW - 2 * OzLayout.OptX);
            RefreshBuildKeys();
        }

        void RefreshBuildKeys()
        {
            foreach (var (type, button) in buildKeyButtons)
            {
                var key = OzBuildKeys.Get(type);
                button.Label.Set(UnitInfo.Name(type) + "   ·   " + (key == Key.None ? "NONE" : "SHIFT + " + OzBindings.Cap(key)));
            }
            buildKeyHelp?.Set(bindingBuildUnit == null ? "Open a factory to use its build keys." :
                UnitInfo.Name(bindingBuildUnit) + " · press a letter, Backspace to clear, or Esc to cancel.");
        }

        bool CaptureBuildBinding()
        {
            if (bindingBuildUnit == null) return false;
            var keyboard = Keyboard.current;
            if (keyboard == null || buildBindingFrame == Time.frameCount) return true;
            foreach (var control in keyboard.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                var key = control.keyCode;
                if (key != Key.Escape && key != Key.Backspace && !OzBuildKeys.IsLetter(key)) continue;
                if (key != Key.Escape) OzBuildKeys.Set(bindingBuildUnit, key == Key.Backspace ? Key.None : key);
                bindingBuildUnit = null;
                RefreshBuildKeys();
                break;
            }
            return true;
        }
    }
}
