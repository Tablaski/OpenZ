using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed class DuelMenus : MonoBehaviour
    {
        enum Page { Entry, Connect, Lobby, Loading, Game, Chat, Error, End }
        Page page;
        DuelRuntime R => DuelRuntime.I;
        Canvas canvas;
        RectTransform root;
        OpenZMainMenu originalMenu;
        bool dirty = true, exceptionEditor;
        int tower = -1;
        string name = "PLAYER", address = "127.0.0.1", port = "27888", invite = "", chat = "";
        OzText ping, loading, voiceStatus;
        InputField chatField;
        bool bindingChat, bindingTalk;
        double endedAt = -1;
        public bool IsTyping => bindingChat || bindingTalk || (EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>());
        Key ChatKey => (Key)PlayerPrefs.GetInt("oz_duel_chat_key", (int)Key.Enter);
        public bool CapturesInput => page != Page.Entry || (R.InMatch && !R.Playing);
        public void Refresh() { dirty = true; }
        public void ShowConnect() { page = Page.Connect; dirty = true; }
        public void SetInvitation(string value) { invite = value; ShowConnect(); }
        public void ShowLobby() { page = Page.Lobby; dirty = true; }
        public void ShowLoading() { page = Page.Loading; dirty = true; }
        public void ShowError() { page = Page.Error; dirty = true; }
        public void ShowInGameMenu() { page = Page.Game; dirty = true; }
        public void CloseOverlay() { page = Page.Entry; dirty = true; }
        void Awake() { name = PlayerPrefs.GetString("oz_duel_name", "PLAYER"); }
        void Update()
        {
            if (R.InMatch && R.Game && R.Game.World.GameOver)
            {
                if (endedAt < 0) endedAt = Time.realtimeSinceStartupAsDouble;
                if (Time.realtimeSinceStartupAsDouble - endedAt > 3 && page == Page.Entry) { page = Page.End; dirty = true; }
            }
            else endedAt = -1;
            bool wasBinding = bindingChat || bindingTalk;
            if (wasBinding && Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (key.wasPressedThisFrame)
                    {
                        if (key.keyCode != Key.Escape) PlayerPrefs.SetInt(bindingTalk ? "oz_duel_talk_key" : "oz_duel_chat_key", (int)key.keyCode);
                        bindingChat = bindingTalk = false; dirty = true; break;
                    }
            if (R.InMatch && R.Playing && Keyboard.current != null && !wasBinding)
            {
                if (page == Page.Entry && Keyboard.current[ChatKey].wasPressedThisFrame) { page = Page.Chat; dirty = true; }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame) { page = page == Page.Entry ? Page.Game : Page.Entry; dirty = true; }
            }
            if (dirty) { dirty = false; Build(); }
            ping?.Set("PING " + R.PingMs.ToString("0") + " MS");
            voiceStatus?.Set(R.Voice.Status);
            if (loading != null) loading.Set(R.Countdown >= 0 ? "STARTING IN " + Math.Ceiling(R.Countdown) : R.Status);
        }
        void Build()
        {
            if (root) Destroy(root.gameObject);
            ping = loading = voiceStatus = null; chatField = null;
            if (!canvas)
            {
                var go = new GameObject("DuelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.layer = 5;
                go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            }
            root = Hud.Node("Duel", canvas.transform); root.Fill();
            if (!EventSystem.current)
            {
                var es = new GameObject("DuelEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (!originalMenu) originalMenu = FindFirstObjectByType<OpenZMainMenu>();
            if (originalMenu) originalMenu.enabled = page == Page.Entry;
            if (page == Page.Entry)
            {
                if (SceneManager.GetActiveScene().name == "MainMenu") Button(root, "1VS1", 1500, 48, 320, ShowConnect, true);
                else if (R.InMatch) { Button(root, "DUEL MENU", 1590, 20, 260, ShowInGameMenu); ping = Label(root, "PING", 1370, 34, 180, 17, OzUi.Go); }
                return;
            }
            if (page == Page.Chat) { BuildChat(); return; }
            var background = Hud.Img(root, "Background", OzUi.GM950); background.rectTransform.Fill(); background.raycastTarget = true;
            var poster = Resources.Load<Texture2D>("OpenZ/Menu/" + (R.View?.Map ?? "L02") + "_poster");
            if (poster) { var raw = Hud.Node("MissionBackground", root).gameObject.AddComponent<RawImage>(); raw.texture = poster; raw.color = new Color(.25f, .25f, .25f, 1); raw.rectTransform.Fill(); raw.raycastTarget = false; }
            Label(root, "OPENZ", 80, 40, 370, 57, OzUi.Amber);
            Label(root, "DUEL · 1VS1", 80, 108, 800, 22, OzUi.Ink);
            ping = Label(root, "PING", 1570, 90, 250, 18, OzUi.Go);
            if (page == Page.Connect) BuildConnect();
            else if (page == Page.Lobby) BuildLobby();
            else if (page == Page.Loading) { loading = Label(root, R.Status, 250, 430, 1400, 38, OzUi.Amber); }
            else if (page == Page.Game) BuildGameMenu();
            else if (page == Page.End)
            {
                bool won = R.Game.World.Winner == (R.IsHost ? Team.Red : Team.Blue);
                Label(root, won ? "VICTORY" : "DEFEAT", 180, 300, 1400, 56, won ? OzUi.Go : OzUi.AlertText);
                Label(root, "THE DUEL HAS ENDED", 180, 390, 1400, 24, OzUi.Ink);
                Button(root, "BACK TO MENU", 180, 500, 600, Leave, true);
            }
            else if (page == Page.Error)
            {
                Label(root, "CONNECTION LOST", 180, 300, 1400, 34, OzUi.AlertText);
                Label(root, R.Status, 180, 370, 1400, 22, OzUi.Ink);
                Button(root, "BACK TO MENU", 180, 470, 400, Leave);
            }
        }
        void BuildConnect()
        {
            var left = Panel("HOST", 80, 210, 850, 650);
            var right = Panel("JOIN", 980, 210, 850, 650);
            Input(left, "PLAYER NAME", 30, 90, 760, name, v => name = v, 32);
            Input(left, "SHARE ADDRESS (PUBLIC IP OR LAN)", 30, 200, 760, address, v => address = v, 200);
            Input(left, "TCP PORT", 30, 310, 300, port, v => port = v, 5);
            Button(left, "CREATE MATCH", 30, 440, 760, () => { SaveName(); R.Host(name, int.Parse(port)); }, true);
            Label(left, "Direct Internet play: the host TCP port must be reachable.", 30, 540, 760, 17, OzUi.Muted);
            Input(right, "INVITATION (INCLUDES ADDRESS AND PORT)", 30, 90, 760, invite, v => invite = v, 1000);
            Button(right, "JOIN MATCH", 30, 230, 760, () => { SaveName(); R.Join(name, invite); }, true);
            Label(right, "Paste the invitation from your opponent.", 30, 340, 760, 19, OzUi.Muted);
            Button(root, "BACK", 80, 950, 300, CloseOverlay);
        }
        void SaveName() { PlayerPrefs.SetString("oz_duel_name", name); PlayerPrefs.Save(); }
        void BuildLobby()
        {
            if (R.View == null) { loading = Label(root, R.Status, 180, 350, 1500, 28, OzUi.Ink); Button(root, "CANCEL", 80, 950, 300, Leave); return; }
            var rules = R.Rules; bool editable = R.IsHost && R.View.Phase == (int)DuelPhase.Lobby;
            if (R.IsHost) Button(root, "COPY INVITATION", 1250, 40, 580, () => GUIUtility.systemCopyBuffer = R.Invitation(address, int.Parse(port)));
            var left = Panel("SHARED RULES", 80, 190, 850, 595);
            int mx = 24;
            foreach (string map in new[] { "L02", "L07", "L16", "L17" })
            {
                string chosen = map;
                Button(left, map, mx, 70, 190, () => { R.ChangeRules(DuelRuntime.Defaults(chosen)); exceptionEditor = false; }, map == rules.Map, editable); mx += 200;
            }
            var poster = Resources.Load<Texture2D>("OpenZ/Menu/" + rules.Map + "_poster");
            if (poster) { var raw = Hud.Node("Mission", left).gameObject.AddComponent<RawImage>(); raw.texture = poster; raw.rectTransform.TL(24, 140, 800, 220); raw.raycastTarget = false; }
            Label(left, "MAX. TERRITORY BONUS: " + (rules.TerritoryBonus * 100).ToString("0") + " %", 24, 390, 620, 20, OzUi.Ink);
            Button(left, "−", 650, 380, 65, () => { rules.TerritoryBonus = Math.Max(0, rules.TerritoryBonus - .1); R.ChangeRules(rules); }, false, editable);
            Button(left, "+", 730, 380, 65, () => { rules.TerritoryBonus = Math.Min(.9, rules.TerritoryBonus + .1); R.ChangeRules(rules); }, false, editable);
            Label(left, "SHARED BUILD SPEED: ×" + (1 / rules.Common.BuildDuration).ToString("0.##"), 24, 468, 620, 20, OzUi.Ink);
            Button(left, "CHANGE", 650, 455, 145, () => { rules.Common.BuildDuration = NextDuration(rules.Common.BuildDuration); R.ChangeRules(rules); }, false, editable);
            Label(left, "Default: the mission red starting army for both teams.", 24, 540, 800, 17, OzUi.Muted);
            BuildArmy(rules, editable);
            var handicap = Panel(rules.HandicapEnabled ? "⚠ HANDICAP ACTIVE" : "OPTIONAL HANDICAP · OFF", 980, 800, 850, 150);
            Button(handicap, rules.HandicapEnabled ? "DISABLE" : "ENABLE", 24, 65, 200, () => { if (rules.HandicapEnabled) { rules.HandicapEnabled = false; exceptionEditor = false; } else rules.EnableHandicap(1); R.ChangeRules(rules); }, false, editable);
            if (rules.HandicapEnabled)
            {
                Label(handicap, rules.HandicapSlot == 0 ? "RED" : "BLUE", 245, 75, 130, 19, OzUi.AlertText);
                Button(handicap, "PLAYER", 370, 65, 130, () => { rules.EnableHandicap(1 - rules.HandicapSlot); R.ChangeRules(rules); }, false, editable);
                Button(handicap, "ARMY", 510, 65, 130, () => { exceptionEditor = true; dirty = true; }, false, editable);
                Button(handicap, "×" + (rules.Common.BuildDuration / rules.Handicap.BuildDuration).ToString("0.##"), 650, 65, 155,
                    () => { rules.Handicap.BuildDuration = NextDuration(rules.Handicap.BuildDuration); R.ChangeRules(rules); }, false, editable);
            }
            BuildChatPanel(80, 800, 850, 180);
            Label(root, "RED · " + R.View.HostName + " · " + (R.View.HostReady ? "READY" : "NOT READY"), 980, 967, 820, 16, rules.HandicapEnabled && rules.HandicapSlot == 0 ? OzUi.AlertText : OzUi.Ink);
            Label(root, "BLUE · " + (R.View.GuestName ?? "WAITING") + " · " + (R.View.GuestReady ? "READY" : "NOT READY"), 980, 990, 820, 16, rules.HandicapEnabled && rules.HandicapSlot == 1 ? OzUi.AlertText : OzUi.Ink);
            Button(root, "LEAVE", 80, 1000, 250, Leave);
            Label(root, R.Status, 360, 1020, 950, 16, OzUi.AlertText);
            Button(root, (R.IsHost ? R.View.HostReady : R.View.GuestReady) ? "NOT READY" : "READY", 1390, 1010, 440, R.Ready, true, R.Connected);
        }
        static double NextDuration(double current) => current > .9 ? .8 : current > .7 ? 2d / 3 : current > .55 ? .5 : 1;
        void BuildArmy(DuelRules rules, bool editable)
        {
            if (!rules.HandicapEnabled) exceptionEditor = false;
            var army = exceptionEditor ? rules.Handicap : rules.Common;
            string title = exceptionEditor ? "⚠ OVERRIDE FOR " + (rules.HandicapSlot == 0 ? "RED" : "BLUE") : "SHARED ARMY · BOTH TEAMS";
            var panel = Panel(title, 980, 190, 850, 595);
            if (exceptionEditor) Button(panel, "SHARED RULES", 540, 15, 280, () => { exceptionEditor = false; dirty = true; });
            Label(panel, "ON THE FORT", 24, 68, 800, 17, OzUi.Muted);
            for (int i = 0; i < 4; i++)
            {
                int slot = i; string type = army.FortTurrets[i];
                var button = Button(panel, string.IsNullOrEmpty(type) ? "EMPTY" : type.ToUpperInvariant(), 24 + i * 200, 100, 185, () => { tower = tower == slot ? -1 : slot; dirty = true; }, tower == i, editable, 90);
                Portrait(button, type, 64, 3, 58);
            }
            Label(panel, "OUTSIDE THE FORT", 24, 205, 800, 17, OzUi.Muted);
            var content = Scroll(panel, 24, 240, 800, 175, false, Math.Max(175, army.StartingUnits.Distinct().Count() * 55));
            int y = 0;
            foreach (var group in army.StartingUnits.GroupBy(t => t).ToArray())
            {
                string type = group.Key; Portrait(content, type, 0, y, 48);
                Label(content, type.ToUpperInvariant(), 65, y + 12, 430, 18, OzUi.Ink);
                Label(content, group.Count().ToString(), 630, y + 12, 60, 20, OzUi.Amber);
                Button(content, "−", 540, y, 65, () => { army.StartingUnits.Remove(type); R.ChangeRules(rules); }, false, editable);
                Button(content, "+", 700, y, 65, () => { army.StartingUnits.Add(type); R.ChangeRules(rules); }, false, editable); y += 55;
            }
            var choices = tower >= 0 ? new[] { "", "gun", "gatling", "howitzer", "missile_cannon" } : DuelRules.AllowedExtras;
            var catalogue = Scroll(panel, 24, 430, 800, 95, true, choices.Length * 110);
            for (int i = 0; i < choices.Length; i++)
            {
                string type = choices[i];
                var button = Button(catalogue, type == "" ? "EMPTY" : type.ToUpperInvariant(), i * 110, 0, 102, () =>
                { if (tower >= 0) { army.FortTurrets[tower] = type; tower = -1; } else army.StartingUnits.Add(type); R.ChangeRules(rules); }, false, editable, 88);
                Portrait(button, type, 27, 0, 48);
            }
            Button(panel, exceptionEditor ? "RESET TO SHARED ARMY" : "RESET TO MISSION RED ARMY", 24, 540, 800, () =>
            { if (exceptionEditor) rules.Handicap = rules.Common.Copy(); else rules.Common = DuelRuntime.Defaults(rules.Map).Common; R.ChangeRules(rules); }, false, editable);
        }
        void BuildGameMenu()
        {
            var panel = Panel("DUEL MENU · MATCH CONTINUES", 480, 170, 960, 780);
            Button(panel, "RESUME", 40, 85, 880, CloseOverlay, true);
            Button(panel, "OPEN CHAT", 40, 175, 880, () => { page = Page.Chat; dirty = true; });
            Button(panel, bindingChat ? "PRESS A KEY…" : "CHAT KEY: " + ChatKey, 40, 265, 880, () => { bindingChat = true; dirty = true; });
            Button(panel, bindingTalk ? "PRESS A KEY…" : "PUSH TO TALK: " + R.Voice.TalkKey, 40, 350, 880, () => { bindingTalk = true; dirty = true; });
            Button(panel, R.Voice.MicrophoneEnabled ? "MUTE MICROPHONE" : "ENABLE MICROPHONE", 40, 435, 880, () => { R.Voice.ToggleMicrophone(); dirty = true; });
            Button(panel, "MICROPHONE: " + R.Voice.Device, 40, 520, 880, () => { R.Voice.NextDevice(); dirty = true; });
            voiceStatus = Label(panel, R.Voice.Status, 40, 585, 880, 19, OzUi.Go);
            Button(panel, "SURRENDER · YOUR FORT WILL BE DESTROYED", 40, 660, 880, () =>
                OzConfirm.Ask("SURRENDER THE DUEL?", "YOUR FORT WILL EXPLODE. YOUR OPPONENT WINS.", () => { R.Surrender(); CloseOverlay(); }, id => OzSfx.Ui(id), 1100));
        }
        void BuildChat() { BuildChatPanel(70, 750, 920, 290); if (chatField) { chatField.ActivateInputField(); EventSystem.current.SetSelectedGameObject(chatField.gameObject); } }
        void BuildChatPanel(float x, float y, float w, float h)
        {
            var panel = Panel("TEXT CHAT", x, y, w, h);
            Label(panel, string.Join("\n", R.Chat.Skip(Math.Max(0, R.Chat.Count - (h > 200 ? 5 : 2)))), 20, 48, w - 40, 17, OzUi.Ink);
            chatField = Input(panel, "", 20, h - 62, w - 210, chat, v => chat = v, 240);
            chatField.onEndEdit.AddListener(_ => { if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) SendChat(); });
            Button(panel, "SEND", w - 175, h - 62, 150, SendChat);
        }
        void SendChat() { R.SendChat(chat); chat = ""; dirty = true; }
        void Leave() { R.Close(); CloseOverlay(); if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadSceneAsync("MainMenu"); }
        RectTransform Panel(string title, float x, float y, float w, float h)
        {
            var panel = Hud.Node(title, root); panel.TL(x, y, w, h); Hud.Plate(panel, "lg", 0, OzUi.Amber);
            Label(panel, title, 24, 20, w - 48, 20, title.Contains("⚠") ? OzUi.AlertText : OzUi.Amber); return panel;
        }
        static OzText Label(Transform parent, string text, float x, float y, float width, float size, Color color) => Hud.Label(parent, text, text, size, color).At(x, y, OzH.L, false, width);
        RectTransform Button(Transform parent, string text, float x, float y, float w, Action action, bool primary = false, bool enabled = true, float height = 46)
        {
            var b = HudButton.Make(parent, text, text, new Vector2(w, height), HudButtonKind.Large, w < 120 ? 12 : 17, .08f, primary ? HudButtonStyle.Primary : HudButtonStyle.Normal);
            b.Rt.TL(x, y, w, height); b.Interactable = enabled; b.Refresh();
            if (height > 70 && b.Label != null) b.Label.rectTransform.TL(3, height - 28, w - 6, 26);
            b.Bg.raycastTarget = true;
            var ui = b.Rt.gameObject.AddComponent<Button>(); ui.targetGraphic = b.Bg; ui.interactable = enabled;
            ui.onClick.AddListener(() => { try { action(); } catch (Exception error) { Debug.LogException(error); R.ReportError(error.Message); } });
            return b.Rt;
        }
        InputField Input(Transform parent, string caption, float x, float y, float w, string value, Action<string> change, int max)
        {
            if (caption != "") Label(parent, caption, x, y - 30, w, 16, OzUi.Muted);
            var box = Hud.Img(parent, "Input", OzUi.GM950); box.rectTransform.TL(x, y, w, 46); box.raycastTarget = true;
            var text = Hud.Node("Text", box.transform).gameObject.AddComponent<Text>(); text.font = HudFonts.Body; text.fontSize = 19; text.color = OzUi.Ink; text.alignment = TextAnchor.MiddleLeft; text.rectTransform.Fill(12, 0, 12, 0); text.supportRichText = false;
            var input = box.gameObject.AddComponent<InputField>(); input.textComponent = text; input.characterLimit = max; input.text = value; input.onValueChanged.AddListener(v => change(v));
            return input;
        }
        static void Portrait(Transform parent, string type, float x, float y, float size)
        {
            if (string.IsNullOrEmpty(type)) return;
            string alias = type == "light" ? "light_tank" : type == "heavy" ? "heavy_tank" : type == "medium" ? "medium_mech" : type;
            var texture = Resources.Load<Texture2D>("OpenZ/UI/Portraits/portrait_" + alias + "_red") ?? Resources.Load<Texture2D>("OpenZ/UI/PortraitsVehicles/portrait_" + alias + "_red");
            if (!texture) return;
            var image = Hud.Node("Portrait", parent).gameObject.AddComponent<RawImage>(); image.texture = texture; image.rectTransform.TL(x, y, size, size); image.raycastTarget = false;
        }
        static RectTransform Scroll(Transform parent, float x, float y, float w, float h, bool horizontal, float extent)
        {
            var viewport = Hud.Node("Viewport", parent); viewport.TL(x, y, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .2f); hit.raycastTarget = true;
            var content = Hud.Node("Content", viewport); content.TL(0, 0, horizontal ? extent : w, horizontal ? h : extent);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = horizontal; scroll.vertical = !horizontal; scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }
    }
}
