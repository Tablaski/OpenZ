using System;
using System.Linq;
using OpenZ.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OpenZ
{
    public sealed class OzSoloMapTech : MonoBehaviour
    {
        public const string Label = "FACTORY TECH";
        internal const string Preference = "oz_solo_map_tech_v1";
        static readonly string[] Levels = { "—", "I", "II", "III", "IV", "V", "VI" };
        static OzSoloMapTech instance;
        static int closedFrame = -1;
        internal static bool CapturesInput => instance || closedFrame == Time.frameCount;

        [Serializable] sealed class SavedSettings { public Selection[] maps; }
        [Serializable] internal sealed class Selection
        {
            public string Map;
            public int Vehicle, Robot;
            public bool Native;
        }

        Selection selection;
        string mapName;
        (int Vehicles, int Robots, int Fort) mapTech;
        RectTransform sheet;

        static Selection[] Load()
        {
            try
            {
                return JsonUtility.FromJson<SavedSettings>(PlayerPrefs.GetString(Preference, ""))?.maps ?? Array.Empty<Selection>();
            }
            catch (ArgumentException) { return Array.Empty<Selection>(); }
        }

        internal static Selection Read(string map) =>
            Load().FirstOrDefault(entry => entry?.Map == map && entry.Vehicle >= 0 && entry.Vehicle <= 6 &&
                entry.Robot >= 0 && entry.Robot <= 6) ?? new Selection { Map = map };

        internal static void Show(string id)
        {
            if (instance || DuelRuntime.Active) return;
            var converted = OzAiMapCatalog.Find(id);
            string name = converted?.Solo == true ? converted.Name : Array.Find(OzLevelDef.All, level => level.Id == id)?.Name;
            if (name == null) return;
            var go = new GameObject("Solo factory technology", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            instance = go.AddComponent<OzSoloMapTech>();
            instance.selection = Read(id);
            instance.mapName = name;
            instance.mapTech = DuelMenus.ReadMapTech(id);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1150;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            if (!EventSystem.current)
            {
                var input = new GameObject("Factory technology input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.transform.SetParent(go.transform, false);
                input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            instance.Build();
        }

        void Save()
        {
            var maps = Load().Where(entry => entry != null && entry.Map != selection.Map);
            if (selection.Native || selection.Vehicle != 0 || selection.Robot != 0) maps = maps.Append(selection);
            PlayerPrefs.SetString(Preference, JsonUtility.ToJson(new SavedSettings { maps = maps.ToArray() }));
            PlayerPrefs.Save();
            Build();
        }

        void Build()
        {
            if (sheet) { sheet.gameObject.SetActive(false); Destroy(sheet.gameObject); }
            sheet = Hud.Node("Factory technology sheet", transform);
            sheet.Fill();
            var shade = Hud.Img(sheet, "Shade", new Color(0, 0, 0, .86f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var panel = Hud.Node("Factory technology", sheet);
            panel.TL(510, 292, 900, 496);
            var background = Hud.Img(panel, "Background", OzUi.GM950);
            background.rectTransform.Fill();
            Hud.Plate(panel, "lg", 0, OzForkOptions.Accent);
            Hud.Label(panel, "Title", Label + " · " + mapName, 24, OzForkOptions.Accent).At(30, 24);
            AddButton(panel, "CLOSE", 714, 15, 155, Close);
            Hud.Label(panel, "Original", "ORIGINAL", 16, OzUi.Muted).At(352, 89);
            Hud.Label(panel, "Active", "ACTIVE", 16, OzUi.Muted).At(604, 89);
            AddRow("VEHICLE FACTORIES", 120, mapTech.Vehicles, selection.Vehicle, value => selection.Vehicle = value);
            AddRow("ROBOT FACTORIES", 180, mapTech.Robots, selection.Robot, value => selection.Robot = value);
            int fort = selection.Vehicle == 0 ? mapTech.Fort : selection.Vehicle;
            Hud.Label(panel, "Fort", "FORT TECH", 18, OzUi.Ink).At(30, 252);
            Hud.Label(panel, "Original fort", Levels[mapTech.Fort], 20, OzUi.Muted).At(365, 252);
            Hud.Label(panel, "Active fort", Levels[fort], 20, OzUi.Ink).At(614, 252);
            AddButton(panel, selection.Native ? "DIFFICULTY TECH BONUS · OFF" : "DIFFICULTY TECH BONUS · ON",
                30, 307, 410, () => { selection.Native = !selection.Native; Save(); });
            Hud.Label(panel, "Help", "Turn off the difficulty tech bonus to play with the levels shown here.", 17, OzUi.Muted).At(30, 367);
            AddButton(panel, "RESTORE ORIGINAL TECH", 30, 418, 410, () =>
            { selection.Native = true; selection.Vehicle = selection.Robot = 0; Save(); });
            AddButton(panel, "RESTORE CAMPAIGN DEFAULTS", 457, 418, 410, () =>
            { selection.Native = false; selection.Vehicle = selection.Robot = 0; Save(); });

            void AddRow(string name, int y, int original, int setting, Action<int> change)
            {
                int active = setting == 0 ? original : setting;
                Hud.Label(panel, name, name, 18, OzUi.Ink).At(30, y + 9);
                Hud.Label(panel, "Original " + name, Levels[original], 20, OzUi.Muted).At(365, y + 9);
                Hud.Label(panel, "Active " + name, Levels[active], 20, OzForkOptions.Accent).At(614, y + 9);
                AddButton(panel, "−", 540, y, 46, () => Set(active - 1), active > 1);
                AddButton(panel, "+", 678, y, 46, () => Set(active + 1), active > 0 && active < 6);
                void Set(int value) { change(value == original ? 0 : value); selection.Native = true; Save(); }
            }
        }

        static void AddButton(RectTransform panel, string text, int x, int y, int width, Action action, bool enabled = true)
        {
            var button = HudButton.Make(panel, text, text, new Vector2(width, 43), HudButtonKind.Large, 17);
            button.Rt.TL(x, y, width, 43);
            button.Bg.raycastTarget = true;
            var control = button.Rt.gameObject.AddComponent<Button>();
            control.targetGraphic = button.Bg;
            control.interactable = enabled;
            control.onClick.AddListener(() => action());
        }

        void Update() { if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close(); }
        void Close() { instance = null; closedFrame = Time.frameCount; gameObject.SetActive(false); Destroy(gameObject); }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
