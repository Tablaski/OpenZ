using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>A dropdown shared by the main and pause options. Match behavior is read only at world creation.</summary>
    public sealed class OzBehaviorOptions : MonoBehaviour
    {
        static OzBehaviorOptions instance;
        public static bool CapturesInput => instance && instance.open;
        bool open;
        ZBehaviorFamily family;
        Vector2 scroll;
        GUIStyle label, button, plate;
        public static void Show(ZBehaviorFamily f)
        {
            if (!instance)
            {
                var go = new GameObject("BehaviorProfileDropdown"); DontDestroyOnLoad(go);
                instance = go.AddComponent<OzBehaviorOptions>();
            }
            instance.family = f; instance.open = true; instance.scroll = Vector2.zero;
        }
        void Update()
        {
            if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) open = false;
        }
        void OnGUI()
        {
            if (!open) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 8, 4, 4) };
                plate = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 16, 16) };
            }
            GUI.depth = -150;
            float s = Mathf.Clamp(Screen.width / 1280f, .75f, 1.5f);
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s, h = Screen.height / s;
            GUI.Box(new Rect(0, 0, w, h), GUIContent.none);
            Rect r = new Rect((w - 600) / 2, Mathf.Max(15, (h - 480) / 2), 600, Mathf.Min(480, h - 30));
            GUI.Box(r, GUIContent.none, plate);
            GUI.Label(new Rect(r.x + 20, r.y + 16, 530, 30), family == ZBehaviorFamily.Tanks ? "TANK BEHAVIOR" : "ROBOT BEHAVIOR", label);
            GUI.Label(new Rect(r.x + 20, r.y + 52, 550, 55), new GUIContent("Choose the behavior for your next match. Tank and robot choices are independent. Save more presets in Unit Editor > BEHAVIOR.", "The current match is unchanged. Both teams use the same profiles. In multiplayer the host's profiles are replicated to the guest."), label);
            var choices = OzBehaviorLibrary.Choices(family);
            Rect list = new Rect(r.x + 20, r.y + 116, 560, r.height - 180);
            scroll = GUI.BeginScrollView(list, scroll, new Rect(0, 0, 535, choices.Count * 42));
            for (int i = 0; i < choices.Count; i++)
            {
                var p = choices[i]; bool chosen = p.id == OzBehaviorLibrary.SelectedId(family);
                if (GUI.Button(new Rect(0, i * 42, 530, 36), new GUIContent((chosen ? "SELECTED  " : "") + p.name,
                    p.id == "native" ? "Use the unmodified native OpenZ code path, including its current dodge restrictions and 3D steering." :
                    p.id == "zod-classic" ? "Classic dodge logic before fenio commit 37af18e. Tanks can interrupt attacks; engaged robots cannot dodge. Removes mechanical 3D following constraints. Does not enable global Zod exact mode." :
                    p.id == "zod-local" ? "Reference for the current local fork's idle-only dodge restriction, with Zod movement presentation. Active move, attack and entry orders prevent dodging." :
                    "Use the saved " + p.name + " profile for this family only, starting with the next match. The laboratory draft is not used."), button))
                { OzBehaviorLibrary.Select(p); open = false; }
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(r.x + 20, r.yMax - 48, 560, 30), new GUIContent("CLOSE", "Close the dropdown without changing your selection. Escape also closes it."), button)) open = false;
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUI.Label(new Rect(r.x + 10, r.yMax + 6, 580, 100), GUI.tooltip, label);
            GUI.matrix = matrix;
        }
    }
}
