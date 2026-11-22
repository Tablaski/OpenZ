using System;
using OpenZ.Sim;
using OpenZ.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>OpenZ kit profile picker for standalone tools; multiplayer profiles remain lobby-owned.</summary>
    public sealed class OzBehaviorOptions : MonoBehaviour
    {
        const string ClassicGoal = "Z Classic aims to recreate Z95 combat dynamics, including dodging, speed boosts and combat movement.";
        static OzBehaviorOptions instance;
        static int closedFrame = -1;
        bool choosingCustom;
        public static bool CapturesInput => instance || closedFrame == Time.frameCount;
        public static void Show(ZBehaviorFamily family, Action changed = null)
            => ShowGaming();

        internal static void ShowGaming() => Open(options => options.BuildGaming());

        static void Open(Action<OzBehaviorOptions> build)
        {
            if (Multiplayer.DuelRuntime.Active) return;
            Dismiss();
            var go = new GameObject("OpenZ-VK profile picker", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            instance = go.AddComponent<OzBehaviorOptions>();
            DontDestroyOnLoad(go);
            build(instance);
            OzSfx.Ui("ui_panel_open");
        }
        public static void Dismiss()
        {
            if (!instance) return;
            closedFrame = Time.frameCount;
            instance.gameObject.SetActive(false);
            Destroy(instance.gameObject);
            instance = null;
        }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { OzSfx.Ui("ui_cancel"); Close(); }
        }
        void Close() { if (choosingCustom) ShowGaming(); else Dismiss(); }
        void OnDestroy() { if (instance == this) instance = null; }
        RectTransform Sheet()
        {
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1150;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            if (!EventSystem.current)
            {
                var events = new GameObject("Profile picker input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var scrim = Hud.Img(transform, "Scrim", OzUi.A(OzUi.GM950, .9f)); scrim.rectTransform.Fill(); scrim.raycastTarget = true;
            var panel = Hud.Node("Profile panel", transform); panel.TL(410, 130, 1100, 820);
            var back = Hud.Img(panel, "Background", OzUi.GM950); back.rectTransform.Fill(); back.raycastTarget = true;
            Hud.Plate(panel, "md", 0, OzForkOptions.Brand);
            var logo = Resources.Load<Sprite>("OpenZ/Kit/Wordmarks/wm_openz_vk_menu");
            if (logo) { var image = Hud.Img(panel, "OpenZ-VK logo", Color.white); image.sprite = logo; image.preserveAspect = true; image.rectTransform.TL(26, 18, 220, 72); image.raycastTarget = false; }
            return panel;
        }

        void BuildGaming()
        {
            var panel = Sheet();
            Hud.Label(panel, "Title", "GAMING OPTIONS · SOLO", 30, OzForkOptions.Accent).At(275, 35, OzH.L, false, 785);
            Hud.Label(panel, "Help", "These settings apply to your next solo mission.", 18, OzUi.Ink).At(28, 113, OzH.L, false, 1044);
            string tanks = OzBehaviorLibrary.SelectedId(ZBehaviorFamily.Tanks);
            string robots = OzBehaviorLibrary.SelectedId(ZBehaviorFamily.Robots);
            var customs = OzBehaviorLibrary.CustomProfiles();
            const int width = 310;
            ProfileButton("OPENZ", "native", 158, "Standard OpenZ combat behavior.", false);
            ProfileButton("Z CLASSIC", "zod-classic", 222, ClassicGoal, true);
            bool legacySelection = (tanks != "native" || robots != "native") &&
                (tanks != "zod-classic" || robots != "zod-classic");
            var activeCustom = customs.Find(profile => profile.Matches(tanks, robots));
            string current = activeCustom?.Name;
            MakeButton(panel, "Custom profiles", "CUSTOM" +
                (current == null ? "" : " · " + current) + (customs.Count > 0 ? "  v" : ""), 28, 286, width, 50,
                () => Open(options => { options.choosingCustom = !choosingCustom; options.BuildGaming(); }),
                selected: activeCustom != null, enabled: customs.Count > 0);
            string help = customs.Count == 0 ? "Save a profile in Unit Editor > Dynamics to enable Custom." :
                "Choose a combat profile saved in the Unit Editor.";
            Hud.Label(panel, "Custom help", help, 17, OzUi.Ink).At(360, 303, OzH.L, false, 708);
            if (legacySelection && activeCustom == null)
                Hud.Label(panel, "Legacy selection", "Your earlier combat settings remain active.", 17, OzUi.Ink)
                    .At(360, 331, OzH.L, false, 708);

            void ProfileButton(string name, string id, int y, string helpText, bool classic)
            {
                bool selected = tanks == id && robots == id;
                MakeButton(panel, name, name, 28, y, width, 50,
                    () => { OzBehaviorLibrary.SelectCombat(OzBehaviorLibrary.Standard(classic)); ShowGaming(); }, selected: selected);
                Hud.Label(panel, name + " help", helpText, 17, OzUi.Ink).At(360, y + 17, OzH.L, false, 708);
            }
            Hud.Label(panel, "Radar", "WHEN YOU HAVE NO WORKING RADAR", 22, OzForkOptions.Accent).At(28, 427, OzH.L, false, 1044);
            for (int i = 0; i < 4; i++)
            {
                var rule = (RadarLossRule)i;
                int y = 467 + i * 65;
                MakeButton(panel, "Radar " + rule, OzBattleOptions.RadarName(rule),
                    28, y, 275, 46, () => { OzBattleOptions.SoloRadar = rule; ShowGaming(); }, selected: OzBattleOptions.SoloRadar == rule);
                Hud.Label(panel, "Radar help " + rule, OzBattleOptions.RadarTip(rule), 17, OzUi.Ink).At(320, y + 12, OzH.L, false, 750);
            }
            MakeButton(panel, "CLOSE", "CLOSE", 28, 757, 1044, 44, Dismiss);
            if (!choosingCustom || customs.Count == 0) return;
            var viewport = Hud.Node("Saved combat profiles", panel);
            float height = Mathf.Min(240, customs.Count * 52 + 8);
            viewport.TL(28, 339, width + 8, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = OzUi.GM950;
            var content = Hud.Node("Profiles", viewport);
            content.TL(0, 0, width + 8, customs.Count * 52 + 8);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < customs.Count; i++)
            {
                var profile = customs[i];
                MakeButton(content, "Profile " + i, profile.Name, 4, 4 + i * 52, width, 46,
                    () => { OzBehaviorLibrary.SelectCombat(profile); ShowGaming(); }, selected: profile == activeCustom);
            }
        }

        static HudButton MakeButton(Transform parent, string name, string label, float x, float y, float width, float height, Action action,
            bool selected = false, bool enabled = true)
        {
            var kit = HudButton.Make(parent, name, label, new Vector2(width, height), HudButtonKind.Large, 19, .05f,
                selected ? HudButtonStyle.Primary : HudButtonStyle.Normal);
            kit.Rt.TL(x, y, width, height); kit.Bg.raycastTarget = true;
            var button = kit.Rt.gameObject.AddComponent<Button>();
            button.targetGraphic = kit.Bg;
            button.transition = Selectable.Transition.None;
            button.interactable = kit.Interactable = enabled;
            kit.Refresh();
            button.onClick.AddListener(() => action());
            return kit;
        }
    }
}
