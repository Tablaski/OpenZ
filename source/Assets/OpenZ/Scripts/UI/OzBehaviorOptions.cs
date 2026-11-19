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
        static OzBehaviorOptions instance;
        static int closedFrame = -1;
        Action returnToGaming;
        public static bool CapturesInput => instance || closedFrame == Time.frameCount;
        public static void Show(ZBehaviorFamily family, Action changed = null)
            => Open(options => options.Build(family, changed));

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
        void Close() { Dismiss(); returnToGaming?.Invoke(); }
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
            foreach (var family in new[] { ZBehaviorFamily.Tanks, ZBehaviorFamily.Robots })
            {
                string label = family == ZBehaviorFamily.Tanks ? OzBehaviorLibrary.TanksLabel : OzBehaviorLibrary.RobotsLabel;
                int y = family == ZBehaviorFamily.Tanks ? 158 : 222;
                var row = MakeButton(panel, label, label + " · " + OzBehaviorLibrary.Selected(family).name, 28, y, 1044, 52,
                    () => Open(options =>
                    {
                        options.returnToGaming = ShowGaming;
                        options.Build(family, ShowGaming);
                    }));
                OzForkOptions.Highlight(row, label);
            }
            Hud.Label(panel, "Classic note", "Z Classic is experimental and still far from finished. Give it a try!", 19, OzUi.Ink)
                .At(28, 300, OzH.L, false, 1044);
            var feedback = MakeButton(panel, "Discord feedback", "SHARE YOUR FEEDBACK ON OPENZ / ZZONE DISCORD", 28, 337, 1044, 52,
                () => Application.OpenURL("https://discord.gg/WjFqg35PsE"));
            if (feedback.Label.graphic is TMPro.TMP_Text tmp) tmp.fontStyle |= TMPro.FontStyles.Bold;
            else if (feedback.Label.graphic is Text legacy) legacy.fontStyle = FontStyle.Bold;
            Hud.Label(panel, "Radar", "WHEN YOU HAVE NO WORKING RADAR", 22, OzForkOptions.Accent).At(28, 427, OzH.L, false, 1044);
            for (int i = 0; i < 4; i++)
            {
                var rule = (RadarLossRule)i;
                int y = 467 + i * 65;
                var row = MakeButton(panel, "Radar " + rule, (OzBattleOptions.SoloRadar == rule ? "✓ " : "") + OzBattleOptions.RadarName(rule),
                    28, y, 275, 46, () => { OzBattleOptions.SoloRadar = rule; ShowGaming(); });
                OzForkOptions.Highlight(row, OzBattleOptions.RadarLabel);
                Hud.Label(panel, "Radar help " + rule, OzBattleOptions.RadarTip(rule), 17, OzUi.Ink).At(320, y + 12, OzH.L, false, 750);
            }
            MakeButton(panel, "CLOSE", "CLOSE", 28, 757, 1044, 44, Dismiss);
        }

        void Build(ZBehaviorFamily family, Action changed)
        {
            var panel = Sheet();
            Hud.Label(panel, "Title", family == ZBehaviorFamily.Tanks ? "TANK DYNAMICS" : "ROBOT DYNAMICS", 30, OzForkOptions.Accent).At(275, 35, OzH.L, false, 785);
            Hud.Label(panel, "Help", "Choose a profile for the next solo match. Tanks and robots are independent.", 18, OzUi.Ink).At(28, 113, OzH.L, false, 1044);
            Hud.Label(panel, "Lobby rule", "Multiplayer profiles are selected by the host in the lobby and stay fixed for the match.", 16, OzUi.Muted).At(28, 150, OzH.L, false, 1044);
            var viewport = Hud.Node("Profiles viewport", panel); viewport.TL(28, 203, 1044, 495);
            viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<Image>().color = OzUi.GM950;
            var choices = OzBehaviorLibrary.Choices(family);
            var content = Hud.Node("Profiles", viewport); content.TL(0, 0, 1044, Mathf.Max(495, choices.Count * 102));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            for (int i = 0; i < choices.Count; i++)
            {
                var profile = choices[i]; bool selected = profile.id == OzBehaviorLibrary.SelectedId(family);
                var row = MakeButton(content, "PROFILE " + profile.id, (selected ? "SELECTED  ·  " : "") + profile.name, 0, i * 102, 1028, 62,
                    () => { OzBehaviorLibrary.Select(profile); OzSfx.Ui("ui_confirm"); Dismiss(); changed?.Invoke(); });
                OzForkOptions.Highlight(row, family == ZBehaviorFamily.Tanks ? OzBehaviorLibrary.TanksLabel : OzBehaviorLibrary.RobotsLabel);
                Hud.Label(content, "Help " + profile.id, Description(profile), 15, OzUi.Muted).At(14, i * 102 + 72, OzH.L, false, 1000);
            }
            Hud.Label(panel, "Save hint", "Create and save custom profiles in Unit Editor > Dynamics.", 16, OzUi.Muted).At(28, 718, OzH.L, false, 1044);
            MakeButton(panel, "CLOSE", "CLOSE", 28, 757, 1044, 44, () => { OzSfx.Ui("ui_cancel"); Close(); });
        }
        static string Description(ZBehaviorProfile profile)
        {
            if (profile.id == "native") return "Original OpenZ behavior and movement.";
            if (profile.id == "zod-classic") return "VK reference: defensive boost x5; dodge and boost travel limited to 6.25 m.";
            if (profile.id == "zod-local") return "Zod movement with dodging limited to idle units.";
            if (profile.id == "retreat-dodge") return "Robots may dodge during retreat orders.";
            return "Your saved profile. Laboratory drafts stay separate until saved.";
        }
        static HudButton MakeButton(Transform parent, string name, string label, float x, float y, float width, float height, Action action)
        {
            var kit = HudButton.Make(parent, name, label, new Vector2(width, height), HudButtonKind.Large, 19, .05f, HudButtonStyle.Normal);
            kit.Rt.TL(x, y, width, height); kit.Bg.raycastTarget = true;
            var button = kit.Rt.gameObject.AddComponent<Button>(); button.targetGraphic = kit.Bg;
            button.onClick.AddListener(() => action());
            return kit;
        }
    }
}
