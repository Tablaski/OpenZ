using System;
using System.Linq;
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
        internal const string Stop = "stop", Clear = "stop_and_clear", Restart = "restart";
        internal static readonly string[] Actions = { Stop, Clear, Restart };
        internal static string Name(string type) => type switch
        {
            Stop => "Stop production",
            Clear => "Stop + clear queue",
            Restart => OzForkOptions.RestartProductionLabel,
            _ => UnitInfo.Name(type)
        };
        static readonly string DefaultLetters = "GPS TYL EJIMH AOUN KRWC".Replace(" ", "");
        internal static string Preference(string type) => "oz_build_key_" + type;
        internal static Key Get(string type)
        {
            int index = Array.IndexOf(Units, type);
            if (type == Restart) return OzBindings.KeyOf(OzAction.RestartProduction);
            if (index < 0) return (Key)PlayerPrefs.GetInt(Preference(type));
            string letter = DefaultLetters[index].ToString();
            var defaultKey = Keyboard.current?.FindKeyOnCurrentKeyboardLayout(letter)?.keyCode ??
                (Key)((int)Key.A + DefaultLetters[index] - 'A');
            return (Key)PlayerPrefs.GetInt(Preference(type), (int)defaultKey);
        }
        internal static void Set(string type, Key key)
        {
            foreach (string other in Units.Concat(Actions))
                if (other != type && key != Key.None && key != OzBindings.LongLeftClick && Get(other) == key)
                    Save(other, Key.None);
            Save(type, key);
            PlayerPrefs.Save();
        }
        static void Save(string type, Key key)
        {
            if (type == Restart) OzBindings.Set(OzAction.RestartProduction, key, out _);
            else PlayerPrefs.SetInt(Preference(type), (int)key);
        }

        internal static bool Modifier(Keyboard keyboard) => !keyboard.shiftKey.isPressed &&
            !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed &&
            !keyboard.leftMetaKey.isPressed && !keyboard.rightMetaKey.isPressed;
    }

    internal sealed class OzBuildKeyOptions
    {
        readonly RectTransform page;
        string bindingBuildUnit;
        int buildBindingFrame;
        readonly OzText help;
        readonly System.Collections.Generic.List<(string type, HudButton button)> buildKeyButtons = new();

        public OzBuildKeyOptions(RectTransform panel, float panelWidth, Func<RectTransform, string, OzRect, Action, HudButton> makeButton)
        {
            page = Hud.Node("Build Keys", panel);
            page.Fill();
            float width = (panelWidth - 3 * OzLayout.OptX) / 2;
            var entries = OzBuildKeys.Units.Concat(OzBuildKeys.Actions).ToArray();
            for (int i = 0; i < entries.Length; i++)
            {
                string type = entries[i];
                float x = OzLayout.OptX + i / 11 * (width + OzLayout.OptX);
                var row = makeButton(page, "Build " + type, new OzRect(x, 90 + i % 11 * 32, width, 29),
                    () => { bindingBuildUnit = type; buildBindingFrame = Time.frameCount; Refresh(); });
                OzForkOptions.Highlight(row, OzBuildKeys.Label);
                buildKeyButtons.Add((type, row));
            }
            var gesture = makeButton(page, "RestartGesture", new OzRect(OzLayout.OptX, 448, width, 29), () =>
            {
                var key = OzBuildKeys.Get(OzBuildKeys.Restart);
                OzBuildKeys.Set(OzBuildKeys.Restart, key == OzBindings.LongLeftClick ? Key.None : OzBindings.LongLeftClick);
                Cancel();
                Refresh();
            });
            gesture.Label.Set("Toggle hold left click to restart");
            OzForkOptions.Highlight(gesture, OzBuildKeys.Label);
            help = Hud.Label(page, "Help", "", 12, OzUi.Muted, .02f).Body(OzLayout.OptX, 488, panelWidth - 2 * OzLayout.OptX);
            Refresh();
        }

        public void Show(bool visible)
        {
            Cancel();
            page.gameObject.SetActiveFast(visible);
            if (visible) Refresh();
        }
        public void Cancel()
        {
            if (bindingBuildUnit == null) return;
            bindingBuildUnit = null;
            Refresh();
        }
        public void Refresh()
        {
            foreach (var (type, button) in buildKeyButtons)
            {
                var key = OzBuildKeys.Get(type);
                button.Label.Set(OzBuildKeys.Name(type) + "   ·   " + (key == Key.None ? "NONE" : OzBindings.Cap(key)));
            }
            help.Set(bindingBuildUnit == null ? "Build keys only work in an open factory menu, where they take priority over OpenZ shortcuts." :
                OzBuildKeys.Name(bindingBuildUnit) + " · press a key, Backspace to clear, or Esc to cancel.");
        }

        void Bind(string action, Key key)
        {
            if (key == OzBuildKeys.Get(action)) return;
            var build = OzBuildKeys.Units.Concat(OzBuildKeys.Actions)
                .Where(other => other != action && OzBuildKeys.Get(other) == key)
                .Select(OzBuildKeys.Name).ToArray();
            var openZ = OzBindings.Defs.Where(def => def.action != OzAction.RestartProduction &&
                !def.ctrl && OzBindings.KeyOf(def.action) == key).Select(def => def.label).ToArray();
            void Save() { OzBuildKeys.Set(action, key); Refresh(); }
            if (key != Key.None && (build.Length > 0 || openZ.Length > 0))
                OzConfirm.Ask("ASSIGN THIS KEY?",
                    $"{OzBindings.Cap(key)} is assigned to {string.Join(", ", build.Concat(openZ))}.\n" +
                    (build.Length > 0 ? "The previous build assignment will be cleared.\n" : "") +
                    "Build keys take priority only in an open factory menu.", Save, id => OzSfx.Ui(id));
            else Save();
        }

        public bool Capture()
        {
            if (bindingBuildUnit == null) return false;
            var keyboard = Keyboard.current;
            if (keyboard == null || buildBindingFrame == Time.frameCount) return true;
            foreach (var control in keyboard.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                var key = control.keyCode;
                if (key != Key.Escape && key != Key.Backspace && OzBindings.Reserved(key) != null) continue;
                string action = bindingBuildUnit;
                Cancel();
                if (key != Key.Escape) Bind(action, key == Key.Backspace ? Key.None : key);
                break;
            }
            return true;
        }
    }

    public partial class OpenZMainMenu
    {
        OzBuildKeyOptions buildKeyOptions;
    }
}
