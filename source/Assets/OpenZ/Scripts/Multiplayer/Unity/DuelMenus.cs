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
        bool dirty = true;
        int pickerGroup = -1, pickerSlot;
        string previewKey;
        Texture2D mapPreview;
        OzFactionPosters generals;
        readonly RawImage[] generalImages = new RawImage[2];
        string name = "PLAYER", address = "127.0.0.1", port = "27888", invite = "", chat = "";
        OzText ping, loading, voiceStatus;
        InputField chatField;
        bool bindingChat, bindingTalk;
        bool advanced, onlineSettings;
        string serviceUrl, accessCode = "", setupStatus = "";
        string lastConnectionStatus, lastRoom;
        double endedAt = -1;
        public bool IsTyping => bindingChat || bindingTalk || (EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>());
        Key ChatKey => (Key)PlayerPrefs.GetInt("oz_duel_chat_key", (int)Key.Enter);
        public bool CapturesInput => page != Page.Entry || (R.InMatch && !R.Playing);
        public void Refresh() { dirty = true; }
        public void ShowConnect() { page = Page.Connect; dirty = true; }
        public void SetInvitation(string value) { invite = value; advanced = value.IndexOf("address=", StringComparison.OrdinalIgnoreCase) >= 0; ShowConnect(); }
        public void ShowLobby() { page = Page.Lobby; dirty = true; }
        public void ShowLoading() { page = Page.Loading; dirty = true; }
        public void ShowError() { page = Page.Error; dirty = true; }
        public void ShowInGameMenu() { page = Page.Game; dirty = true; }
        public void CloseOverlay() { page = Page.Entry; dirty = true; }
        void Awake() { name = PlayerPrefs.GetString("oz_duel_name", "PLAYER"); }
        void Update()
        {
            if (page == Page.Lobby && pickerGroup >= 0 && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { pickerGroup = -1; dirty = true; }
            if (page == Page.Lobby && (lastConnectionStatus != R.ConnectionStatus || lastRoom != R.OnlineRoom))
            { lastConnectionStatus = R.ConnectionStatus; lastRoom = R.OnlineRoom; dirty = true; }
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
            if (generals)
            {
                generals.SetSize(120, 140);
                generals.SetLive(page == Page.Lobby);
                for (int i = 0; i < 2; i++)
                    if (generalImages[i] && generals.P[i] != null && generals.P[i].modelReady)
                    { generalImages[i].texture = generals.P[i].rt; generalImages[i].material = generals.Figure; generalImages[i].enabled = generals.P[i].rt; }
            }
            ping?.Set("PING " + R.PingMs.ToString("0") + " MS");
            voiceStatus?.Set(R.Voice.Status);
            if (loading != null) loading.Set(R.Countdown >= 0 ? "STARTING IN " + Math.Ceiling(R.Countdown) : R.ConnectionStatus);
        }
        void Build()
        {
            if (page != Page.Lobby)
            {
                pickerGroup = -1;
                if (generals) { generals.Close(false); generals = null; }
            }
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
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
                if (SceneManager.GetActiveScene().name != "MainMenu" && R.InMatch) { Button(root, "DUEL MENU", 1590, 20, 260, ShowInGameMenu); ping = Label(root, "PING", 1370, 34, 180, 17, OzUi.Go); }
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
                Button(root, "ADVANCED TCP/IP", 180, 565, 600, () => { Leave(); advanced = true; onlineSettings = false; ShowConnect(); });
            }
        }
        void BuildConnect()
        {
            Button(root, "PLAY ONLINE", 80, 140, 550, () => { advanced = onlineSettings = false; dirty = true; }, !advanced && !onlineSettings);
            Button(root, "ADVANCED TCP/IP", 660, 140, 550, () => { advanced = true; onlineSettings = false; dirty = true; }, advanced);
            Button(root, "ONLINE SETTINGS", 1240, 140, 590, () => { onlineSettings = true; dirty = true; }, onlineSettings);
            if (onlineSettings) { BuildOnlineSettings(); return; }
            if (!advanced) { BuildOnlineConnect(); return; }
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
        void BuildOnlineConnect()
        {
            var left = Panel("CREATE A MATCH", 80, 250, 850, 610);
            var right = Panel("JOIN A FRIEND", 980, 250, 850, 610);
            Input(left, "YOUR NAME", 30, 90, 760, name, v => name = v, 32);
            Button(left, "CREATE INVITATION", 30, 240, 760, () => { SaveName(); R.ConnectOnline(name, true, ""); }, true);
            Label(left, "Send the invitation to your friend. That's it.", 30, 355, 760, 20, OzUi.Ink);
            Label(left, "No IP address, router setup or VPN needed.", 30, 420, 760, 18, OzUi.Muted);
            Input(right, "PASTE INVITATION OR ROOM CODE", 30, 90, 760, invite, v => invite = v, 1000);
            Button(right, "PASTE", 30, 205, 210, () => { invite = GUIUtility.systemCopyBuffer; dirty = true; });
            Button(right, "JOIN FRIEND", 30, 300, 760, () => { SaveName(); R.ConnectOnline(name, false, invite); }, true);
            Label(right, "Private relay is available only to authorized players.", 30, 420, 760, 17, OzUi.Muted);
            Button(root, "BACK", 80, 950, 300, CloseOverlay);
        }
        void BuildOnlineSettings()
        {
            if (serviceUrl == null) serviceUrl = DuelOnlineSettings.CurrentService;
            var panel = Panel("ONE-TIME ONLINE SETUP", 280, 250, 1360, 610);
            Input(panel, "INVITATION SERVICE", 40, 80, 1280, serviceUrl, v => serviceUrl = v, 500);
            Input(panel, "PRIVATE ACCESS CODE (OPTIONAL)", 40, 210, 1280, accessCode, v => accessCode = v, 256);
            var secretField = panel.GetComponentsInChildren<InputField>().Last(); secretField.contentType = InputField.ContentType.Password; secretField.ForceLabelUpdate();
            Label(panel, DuelPrivateAccess.Enabled ? "Private access saved for this Windows user." : "No private access saved. Direct connections still work.", 40, 315, 1280, 19, OzUi.Muted);
            Button(panel, "SAVE", 40, 385, 390, () => {
                try {
                    string normalized = DuelOnlineSettings.ValidateService(serviceUrl);
                    if (!string.IsNullOrWhiteSpace(accessCode)) DuelPrivateAccess.Save(accessCode);
                    PlayerPrefs.SetString("oz_duel_service", normalized); PlayerPrefs.Save();
                    accessCode = ""; setupStatus = "Online settings saved.";
                } catch (Exception error) { setupStatus = error.Message; }
                dirty = true;
            }, true);
            Button(panel, "REMOVE PRIVATE ACCESS", 470, 385, 760, () => { DuelPrivateAccess.Save(""); accessCode = ""; setupStatus = "Private access removed."; dirty = true; });
            Label(panel, setupStatus, 40, 490, 1280, 19, OzUi.Ink);
            Button(root, "BACK", 80, 950, 300, () => { onlineSettings = false; dirty = true; });
        }
        void SaveName() { PlayerPrefs.SetString("oz_duel_name", name); PlayerPrefs.Save(); }
        void BuildLobby()
        {
            if (R.View == null) { loading = Label(root, R.ConnectionStatus, 180, 350, 1500, 28, OzUi.Ink); Button(root, "CANCEL", 80, 950, 300, Leave); return; }
            var rules = R.Rules; bool editable = R.IsHost && R.View.Phase == (int)DuelPhase.Lobby;
            if (R.IsHost) Button(root, "COPY INVITATION", 1250, 40, 580, () => GUIUtility.systemCopyBuffer = R.Invitation(address, R.OnlineMode ? 0 : int.Parse(port)), false, !R.OnlineMode || !string.IsNullOrEmpty(R.OnlineRoom));
            if (R.OnlineMode) Label(root, R.Connected ? "CONNECTED" : R.ConnectionStatus, 80, 135, 1450, 20, OzUi.Ink);
            var left = Panel("MAP", 80, 190, 700, 595);
            int mx = 24;
            foreach (var map in OzLevelDef.All)
            {
                string chosen = map.Id;
                Button(left, map.Name, mx, 65, 157, () => { rules.Map = chosen; pickerGroup = -1; R.ChangeRules(rules); }, chosen == rules.Map, editable); mx += 165;
            }
            var poster = Resources.Load<Texture2D>("OpenZ/Menu/" + rules.Map + "_poster");
            if (poster) { var raw = Hud.Node("Mission", left).gameObject.AddComponent<RawImage>(); raw.texture = poster; raw.rectTransform.TL(24, 125, 650, 195); raw.raycastTarget = false; raw.uvRect = OzLevelPreview.Cover((float)poster.width / poster.height, 650f / 195); }
            BuildMapPreview(left, rules);
            BuildArmy(rules, editable);
            BuildHandicap(rules, editable);
            var construction = Panel("CONSTRUCTION · CURRENT RULES", 820, 800, 1010, 150);
            Label(construction, "MAX. TERRITORY BONUS: " + (rules.TerritoryBonus * 100).ToString("0") + " %", 24, 62, 650, 18, OzUi.Ink);
            Button(construction, "−", 810, 50, 65, () => { rules.TerritoryBonus = Math.Max(0, rules.TerritoryBonus - .1); R.ChangeRules(rules); }, false, editable);
            Button(construction, "+", 900, 50, 65, () => { rules.TerritoryBonus = Math.Min(.9, rules.TerritoryBonus + .1); R.ChangeRules(rules); }, false, editable);
            Label(construction, "SHARED BUILD SPEED: ×" + (1 / rules.Common.BuildDuration).ToString("0.##"), 24, 113, 650, 18, OzUi.Ink);
            Button(construction, "CHANGE", 810, 102, 155, () => { rules.Common.BuildDuration = NextDuration(rules.Common.BuildDuration); R.ChangeRules(rules); }, false, editable);
            BuildChatPanel(80, 800, 700, 180);
            Label(root, "RED · " + R.View.HostName + " · " + (R.View.HostReady ? "READY" : "NOT READY"), 820, 967, 1000, 16, rules.HandicapEnabled && rules.HandicapSlot == 0 ? OzUi.AlertText : OzUi.Ink);
            Label(root, "BLUE · " + (R.View.GuestName ?? "WAITING") + " · " + (R.View.GuestReady ? "READY" : "NOT READY"), 820, 990, 1000, 16, rules.HandicapEnabled && rules.HandicapSlot == 1 ? OzUi.AlertText : OzUi.Ink);
            Button(root, "LEAVE", 80, 1000, 250, Leave);
            Label(root, R.Status, 360, 1020, 950, 16, OzUi.AlertText);
            Button(root, (R.IsHost ? R.View.HostReady : R.View.GuestReady) ? "NOT READY" : "READY", 1390, 1010, 440, R.Ready, true, R.Connected);
            if (pickerGroup >= 0) BuildUnitPicker(rules, editable);
        }
        static double NextDuration(double current) => current > .9 ? .8 : current > .7 ? 2d / 3 : current > .55 ? .5 : 1;
        void BuildArmy(DuelRules rules, bool editable)
        {
            var panel = Panel("SHARED ARMY · BOTH TEAMS", 820, 190, 1010, 310);
            Label(panel, "ON THE FORT", 24, 68, 800, 17, OzUi.Muted);
            ArmySlots(panel, rules.Common.FortTurrets, 0, 24, 95, 960, editable);
            Label(panel, "OUTSIDE THE FORT", 24, 190, 800, 17, OzUi.Muted);
            ArmySlots(panel, rules.Common.StartingUnits, 1, 24, 217, 960, editable);
        }
        void ArmySlots(Transform parent, IList<string> units, int group, float x, float y, float width, bool editable)
        {
            float step = width / 4;
            for (int i = 0; i < 4; i++)
            {
                int slot = i; string type = i < units.Count ? units[i] : "";
                var button = Button(parent, UnitName(type), x + i * step, y, step - 10, () => { pickerGroup = group; pickerSlot = slot; dirty = true; }, false, editable, 76);
                button.name = "ARMY SLOT " + group + ":" + slot;
                Portrait(button, type, (step - 54) / 2, 2, 44);
            }
        }
        static string UnitName(string type) => string.IsNullOrEmpty(type) ? "EMPTY" : type == "light" ? "LIGHT TANK" : type == "medium" ? "MEDIUM MECH" : type == "mtank" ? "MEDIUM TANK" : type == "heavy" ? "HEAVY TANK" : type.Replace('_', ' ').ToUpperInvariant();
        void BuildHandicap(DuelRules rules, bool editable)
        {
            var panel = Panel("HANDICAP", 820, 520, 1010, 265);
            Button(panel, rules.HandicapEnabled ? "DISABLE" : "DISABLED", 790, 12, 195, () => { if (rules.HandicapEnabled) rules.HandicapEnabled = false; else rules.EnableHandicap(rules.HandicapSlot); R.ChangeRules(rules); }, false, editable);
            if (!generals) generals = OzFactionPosters.Open(transform);
            for (int i = 0; i < 2; i++)
            {
                int seat = i; float x = 24 + i * 230;
                var b = Button(panel, i == 0 ? "GENERAL ZOD · RED" : "EVIL ZOD · BLUE", x, 65, 215, () => { rules.EnableHandicap(seat); R.ChangeRules(rules); }, rules.HandicapEnabled && rules.HandicapSlot == i, editable, 150);
                generalImages[i] = Hud.Node("GeneralPortrait", b).gameObject.AddComponent<RawImage>();
                generalImages[i].rectTransform.TL(47, 0, 120, 118); generalImages[i].raycastTarget = false; generalImages[i].enabled = false;
            }
            Label(panel, "ADDITIONALLY OUTSIDE THE FORT", 510, 68, 480, 16, OzUi.Muted);
            ArmySlots(panel, rules.Handicap.StartingUnits, 2, 510, 100, 480, editable && rules.HandicapEnabled);
            Label(panel, rules.HandicapEnabled ? "Four extra units for " + (rules.HandicapSlot == 0 ? "RED" : "BLUE") + " only." : "Select a general to enable the handicap.", 24, 235, 960, 17, OzUi.Muted);
        }
        void BuildMapPreview(Transform parent, DuelRules rules)
        {
            string key = JsonUtility.ToJson(DuelArmyData.From(rules.Common)) + JsonUtility.ToJson(DuelArmyData.From(rules.Handicap)) + rules.Map + rules.HandicapEnabled + rules.HandicapSlot;
            if (!mapPreview || previewKey != key)
            {
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
                var map = Resources.Load<TextAsset>("OpenZ/Maps/" + rules.Map + "_grid");
                var world = ZWorld.Create(settings.text, map.text, 1);
                world.ConfigureDuel(rules, false); world.DuelApplyStartingArmies();
                if (mapPreview) Destroy(mapPreview);
                mapPreview = OzLevelInfo.Bake(world, rules.Map, true); previewKey = key;
            }
            Label(parent, "TACTICAL MAP", 24, 370, 230, 19, OzUi.Ink);
            Label(parent, "Full map\nStarting positions\nBoth armies", 24, 415, 230, 17, OzUi.Muted);
            var raw = Hud.Node("TacticalMap", parent).gameObject.AddComponent<RawImage>(); raw.texture = mapPreview;
            float scale = Math.Min(380f / mapPreview.width, 240f / mapPreview.height);
            raw.rectTransform.TL(285 + (380 - mapPreview.width * scale) / 2, 335, mapPreview.width * scale, mapPreview.height * scale); raw.raycastTarget = false;
        }
        void BuildUnitPicker(DuelRules rules, bool editable)
        {
            if (!editable || (pickerGroup == 2 && !rules.HandicapEnabled)) { pickerGroup = -1; return; }
            var shade = Hud.Img(root, "UnitPickerShade", new Color(0, 0, 0, .85f)); shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel(pickerGroup == 0 ? "ON THE FORT · SELECT CANNON" : "OUTSIDE THE FORT · SELECT UNIT", 340, 180, 1240, 720);
            var backing = Hud.Img(panel, "UnitPickerBackground", OzUi.GM950); backing.rectTransform.Fill(); backing.transform.SetAsFirstSibling(); backing.raycastTarget = true;
            Button(panel, "CLOSE", 1030, 14, 180, () => { pickerGroup = -1; dirty = true; });
            Label(panel, pickerGroup == 0 ? "Fort mounts accept cannons only." : "One vehicle, cannon or robot squad per slot.", 24, 72, 1150, 18, OzUi.Muted);
            for (int i = 0; i < DuelRules.AllowedExtras.Length; i++)
            {
                string type = DuelRules.AllowedExtras[i];
                bool allowed = pickerGroup != 0 || type == "gun" || type == "gatling" || type == "howitzer" || type == "missile_cannon";
                var button = Button(panel, UnitName(type), 24 + i % 6 * 200, 115 + i / 6 * 155, 190, () => SetArmySlot(rules, type), false, allowed, 140);
                button.name = "SELECT " + type;
                Portrait(button, type, 55, 12, 80);
            }
            Button(panel, "EMPTY SLOT", 24, 625, 360, () => SetArmySlot(rules, ""));
        }
        void SetArmySlot(DuelRules rules, string type)
        {
            if (pickerGroup == 0) rules.Common.FortTurrets[pickerSlot] = type;
            else
            {
                var units = pickerGroup == 1 ? rules.Common.StartingUnits : rules.Handicap.StartingUnits;
                while (units.Count <= pickerSlot) units.Add("");
                units[pickerSlot] = type;
            }
            pickerGroup = -1; R.ChangeRules(rules);
        }
        void OnDestroy()
        {
            if (mapPreview) Destroy(mapPreview);
            if (generals && generals.isActiveAndEnabled) generals.Close(false);
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
