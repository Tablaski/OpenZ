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
    public sealed partial class DuelMenus : MonoBehaviour
    {
        enum Page { Entry, Connect, Lobby, Loading, Game, Chat, Microphone, Error, End }
        Page page;
        Page microphoneReturn;
        bool deviceList;
        bool pauseWasOpen;
        bool microphoneWasEnabled;
        Image inputMeter;
        OzText meterValue, gainValue;
        readonly List<(string text, double expires)> notices = new List<(string, double)>();
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
        string serviceUrl, setupStatus = "";
        string lastConnectionStatus, lastRoom;
        double endedAt = -1;
        public bool IsTyping => OzForkOptions.BindingChat || bindingChat || bindingTalk || (FindFirstObjectByType<OpenZPauseMenu>()?.MicrophoneBinding ?? false) || (EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>());
        Key ChatKey => (Key)PlayerPrefs.GetInt("oz_duel_chat_key", (int)Key.Enter);
        public bool CapturesInput => page != Page.Entry || (R.InMatch && !R.Playing);
        public void Refresh() { dirty = true; }
        public void ShowConnect() { page = Page.Connect; dirty = true; }
        public void SetInvitation(string value) { invite = value; advanced = value.IndexOf("address=", StringComparison.OrdinalIgnoreCase) >= 0; ShowConnect(); }
        public void ShowLobby() { page = Page.Lobby; dirty = true; }
        public void ShowLoading() { page = Page.Loading; dirty = true; }
        public void ShowError() { page = Page.Error; dirty = true; }
        public void ShowInGameMenu() { CloseOverlay(); FindFirstObjectByType<OpenZPauseMenu>()?.Open(); }
        public void ShowCommunications() { OpenChat(); }
        public void CloseOverlay() { FindFirstObjectByType<OpenZPauseMenu>()?.Close(); page = Page.Entry; dirty = true; }
        public void NotifyChat(string message)
        {
            if (!R.Playing) return;
            notices.Add((message, Time.realtimeSinceStartupAsDouble + 10));
            if (notices.Count > 4) notices.RemoveAt(0);
            dirty = true;
        }
        public void OpenChat() { FindFirstObjectByType<OpenZPauseMenu>()?.Close(); page = Page.Chat; dirty = true; }
        void OpenMicrophone() { if (R.Playing) { CloseOverlay(); FindFirstObjectByType<OpenZPauseMenu>()?.OpenOptions(OpenZPauseMenu.MicrophoneTab); return; } microphoneReturn = page; deviceList = false; page = Page.Microphone; dirty = true; }
        void Awake() { name = PlayerPrefs.GetString("oz_duel_name", "PLAYER"); }
        void Update()
        {
            var nativePause = FindFirstObjectByType<OpenZPauseMenu>();
            bool pauseOpen = nativePause && nativePause.IsOpen;
            if (pauseOpen != pauseWasOpen) { pauseWasOpen = pauseOpen; dirty = true; }
            if (microphoneWasEnabled != R.Voice.MicrophoneEnabled) { microphoneWasEnabled = R.Voice.MicrophoneEnabled; dirty = true; }
            if (notices.RemoveAll(n => n.expires <= Time.realtimeSinceStartupAsDouble) > 0) dirty = true;
            if (!R.InMatch && notices.Count > 0) { notices.Clear(); dirty = true; }
            if (!OzShittyMapGallery.IsOpen && page == Page.Lobby && (pickerGroup >= 0 || lobbyDialog != LobbyDialog.None) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { pickerGroup = -1; lobbyDialog = LobbyDialog.None; dirty = true; }
            if (page == Page.Lobby && (lastConnectionStatus != R.ConnectionStatus || lastRoom != R.OnlineRoom))
            { lastConnectionStatus = R.ConnectionStatus; lastRoom = R.OnlineRoom; dirty = true; }
            if (R.InMatch && R.Game && R.Game.World.GameOver)
            {
                if (endedAt < 0) endedAt = Time.realtimeSinceStartupAsDouble;
                // The standard end screen owns the statistics and return-to-lobby controls.
            }
            else endedAt = -1;
            bool wasBinding = bindingChat || bindingTalk;
            if (wasBinding && Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (key.wasPressedThisFrame)
                    {
                        if (key.keyCode != Key.Escape) PlayerPrefs.SetInt(bindingTalk ? "oz_duel_talk_key" : "oz_duel_chat_key", (int)key.keyCode);
                        bindingChat = bindingTalk = false; PlayerPrefs.Save(); dirty = true; break;
                    }
            if (((R.InMatch && R.Playing) || page == Page.Microphone) && Keyboard.current != null && !wasBinding)
            {
                var pause = FindFirstObjectByType<OpenZPauseMenu>();
                if (page == Page.Entry && !(pause && pause.IsOpen) && !OzConfirm.Blocks && !IsTyping && Keyboard.current[ChatKey].wasPressedThisFrame) OpenChat();
                else if (page != Page.Entry && !OzConfirm.Blocks && Keyboard.current.escapeKey.wasPressedThisFrame)
                { page = page == Page.Microphone ? microphoneReturn : Page.Entry; dirty = true; }
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
            if (inputMeter) { inputMeter.rectTransform.anchorMax = new Vector2(R.Voice.InputLevel, 1); inputMeter.color = R.Voice.InputLevel > .95f ? OzUi.AlertText : OzUi.Go; }
            meterValue?.Set("INPUT LEVEL: " + Mathf.RoundToInt(R.Voice.InputLevel * 100) + "%");
            gainValue?.Set("MICROPHONE GAIN: " + Mathf.RoundToInt(R.Voice.Gain * 100) + "%");
            if (loading != null) loading.Set(R.Countdown >= 0 ? "STARTING IN " + Math.Ceiling(R.Countdown) : R.ConnectionStatus);
        }
        void Build()
        {
            if (page != Page.Lobby)
            {
                pickerGroup = -1;
                lobbyDialog = LobbyDialog.None;
                if (generals) { generals.Close(false); generals = null; }
            }
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            ping = loading = voiceStatus = meterValue = gainValue = null; inputMeter = null; chatField = null;
            R.Voice.Monitoring = page == Page.Microphone;
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
                if (SceneManager.GetActiveScene().name != "MainMenu" && R.InMatch) ping = Label(root, "PING", 1370, 34, 180, 17, OzUi.Go);
                if (R.Playing && !FindObjectsByType<OpenZPauseMenu>(FindObjectsSortMode.None).Any(p => p.IsOpen))
                    for (int i = 0; i < notices.Count; i++)
                    {
                        var notice = Button(root, "", 24, 922 - (notices.Count - 1 - i) * 78, 1040, OpenChat, false, true, 74);
                        notice.name = "Incoming chat";
                        Label(notice, notices[i].text, 14, 10, 1010, 17, OzUi.Ink).Body(14, 10, 1010);
                        FindFirstObjectByType<OpenZHud>()?.AddBlocker(notice);
                    }
                return;
            }
            if (page == Page.Chat) { BuildChat(); return; }
            var background = Hud.Img(root, "Background", OzUi.GM950); background.rectTransform.Fill(); background.raycastTarget = true;
            var poster = Resources.Load<Texture2D>("OpenZ/Menu/" + (R.View?.Map ?? "L02") + "_poster");
            if (poster) { var raw = Hud.Node("MissionBackground", root).gameObject.AddComponent<RawImage>(); raw.texture = poster; raw.color = new Color(.25f, .25f, .25f, 1); raw.rectTransform.Fill(); raw.raycastTarget = false; }
            Label(root, "OpenZ-VK", 80, 40, 370, 57, OzForkOptions.Brand);
            Label(root, "DUEL · 1VS1", 80, 108, 800, 22, OzUi.Ink);
            ping = Label(root, "PING", 1570, 90, 250, 18, OzUi.Go);
            if (page == Page.Connect) BuildConnect();
            else if (page == Page.Lobby) BuildLobby();
            else if (page == Page.Loading) { loading = Label(root, R.Status, 250, 430, 1400, 38, OzUi.Amber); }
            else if (page == Page.Game) BuildGameMenu();
            else if (page == Page.Microphone) BuildMicrophone();
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
            Label(left, "Direct connection first; relay within the shared allowance.", 30, 420, 760, 18, OzUi.Muted);
            Input(right, "PASTE INVITATION OR ROOM CODE", 30, 90, 760, invite, v => invite = v, 1000);
            Button(right, "PASTE", 30, 205, 210, () => { invite = GUIUtility.systemCopyBuffer; dirty = true; });
            Button(right, "JOIN FRIEND", 30, 300, 760, () => { SaveName(); R.ConnectOnline(name, false, invite); }, true);
            Label(right, "Automatic relay for everyone while allowance is available.", 30, 420, 760, 17, OzUi.Muted);
            Button(root, "BACK", 80, 950, 300, CloseOverlay);
        }
        void BuildOnlineSettings()
        {
            if (serviceUrl == null) serviceUrl = DuelOnlineSettings.CurrentService;
            var panel = Panel("ONE-TIME ONLINE SETUP", 280, 250, 1360, 610);
            Input(panel, "INVITATION SERVICE", 40, 80, 1280, serviceUrl, v => serviceUrl = v, 500);
            Label(panel, "OpenZ-VK connects automatically. No relay password or account is required.", 40, 230, 1280, 19, OzUi.Muted);
            Label(panel, "If the shared relay allowance is unavailable, direct connections still work where supported.", 40, 285, 1280, 19, OzUi.Muted);
            Button(panel, "SAVE", 40, 385, 390, () => {
                try {
                    string normalized = DuelOnlineSettings.ValidateService(serviceUrl);
                    PlayerPrefs.SetString("oz_duel_service", normalized); PlayerPrefs.Save();
                    setupStatus = "Online settings saved.";
                } catch (Exception error) { setupStatus = error.Message; }
                dirty = true;
            }, true);
            Label(panel, setupStatus, 40, 490, 1280, 19, OzUi.Ink);
            Button(root, "BACK", 80, 950, 300, () => { onlineSettings = false; dirty = true; });
        }
        void SaveName() { PlayerPrefs.SetString("oz_duel_name", name); PlayerPrefs.Save(); }
        void BuildLobby()
        {
            if (R.View == null) { loading = Label(root, R.ConnectionStatus, 180, 350, 1500, 28, OzUi.Ink); Button(root, "CANCEL", 80, 950, 300, Leave); return; }
            var rules = R.Rules; bool editable = R.IsHost && R.View.Phase == (int)DuelPhase.Lobby;
            Button(root, "Allowed units · " + DuelUnitRoster.Name(rules.UnitPreset), 470, 40, 355, () => { lobbyDialog = LobbyDialog.Units; pickerGroup = -1; dirty = true; });
            Button(root, "DYNAMICS", 845, 40, 350, () => { lobbyDialog = LobbyDialog.Dynamics; pickerGroup = -1; dirty = true; });
            if (R.IsHost) Button(root, "COPY INVITATION", 1250, 40, 580, () => GUIUtility.systemCopyBuffer = R.Invitation(address, R.OnlineMode ? 0 : int.Parse(port)), false, !R.OnlineMode || !string.IsNullOrEmpty(R.OnlineRoom));
            if (R.OnlineMode) Label(root, R.Connected ? "CONNECTED" : R.ConnectionStatus, 80, 135, 1450, 20, OzUi.Ink);
            var left = Panel("MAP", 80, 190, 700, 595);
            var chosenMap=OzShittyMapGallery.Definition(rules.Map);
            var choose=Button(left, (editable ? "SELECT MAP · " : "INSPECT MAP · ")+(chosenMap?.Name ?? rules.Map), 24, 65, 650, () =>
                OzShittyMapGallery.Open(false, editable, rules.Map, definition => { rules.Map=definition.Id; pickerGroup=-1; R.ChangeRules(rules); }));
            var poop=Hud.Img(choose,"PoopEmoji",Color.white); poop.sprite=OzConvertedMapsScreen.PoopSprite(); poop.rectTransform.TL(12,9,28,28); poop.raycastTarget=false;
            var poster = Resources.Load<Texture2D>("OpenZ/Menu/" + rules.Map + "_poster");
            if (poster) { var raw = Hud.Node("Mission", left).gameObject.AddComponent<RawImage>(); raw.texture = poster; raw.rectTransform.TL(24, 125, 650, 195); raw.raycastTarget = false; raw.uvRect = OzLevelPreview.Cover((float)poster.width / poster.height, 650f / 195); }
            BuildMapPreview(left, rules);
            BuildArmy(rules, editable);
            BuildHandicap(rules, editable);
            var construction = Panel("CONSTRUCTION · CURRENT RULES", 820, 800, 1010, 150);
            Label(construction, "VK TERRITORY CAP: " + (rules.TerritoryReductionCap >= .5 ? "OFF — ORIGINAL" : "MAX. −" + (rules.TerritoryReductionCap * 100).ToString("0") + "% TIME"), 24, 48, 755, 18, OzUi.Ink);
            Label(construction, "Original rule: time −50% × owned flag share. VK cap only limits that reduction.", 24, 73, 755, 14, OzUi.Muted);
            Button(construction, "CHANGE", 810, 40, 155, () => { rules.TerritoryReductionCap = rules.TerritoryReductionCap >= .5 ? .4 : rules.TerritoryReductionCap > .35 ? .3 : rules.TerritoryReductionCap > .27 ? .25 : rules.TerritoryReductionCap > .22 ? .2 : 1; rules.TerritoryBonus = .5; R.ChangeRules(rules); }, false, editable);
            Label(construction, "FORT LOSER BONUS: " + (rules.FortLoserMaximum == 0 ? "OFF" : "MAX. −" + (rules.FortLoserMaximum * 100).ToString("0") + "% TIME"), 24, 109, 760, 18, OzUi.Ink);
            Label(construction, "Offensive value gap: starts at +30% enemy lead; full bonus at +90%. Fort only.", 24, 132, 760, 14, OzUi.Muted);
            Button(construction, "CHANGE", 810, 102, 155, () => { rules.FortLoserMaximum = rules.FortLoserMaximum == 0 ? .3 : rules.FortLoserMaximum < .4 ? .5 : rules.FortLoserMaximum < .6 ? .75 : 0; R.ChangeRules(rules); }, false, editable);
            BuildChatPanel(80, 800, 700, 180);
            Label(root, "RED · " + R.View.HostName + " · " + (R.View.HostReady ? "READY" : "NOT READY"), 820, 967, 540, 16, rules.HandicapEnabled && rules.HandicapSlot == 0 ? OzUi.AlertText : OzUi.Ink);
            Label(root, "BLUE · " + (R.View.GuestName ?? "WAITING") + " · " + (R.View.GuestReady ? "READY" : "NOT READY"), 820, 990, 540, 16, rules.HandicapEnabled && rules.HandicapSlot == 1 ? OzUi.AlertText : OzUi.Ink);
            Button(root, "LEAVE", 80, 1000, 250, Leave);
            Label(root, R.ReleaseWarning.Length > 0 ? R.ReleaseWarning : rules.HasStartingArmies ? R.Status : "Each team needs a starting squad, vehicle or turret before READY.", 360, 1020, 950, 16, OzUi.AlertText);
            Button(root, (R.IsHost ? R.View.HostReady : R.View.GuestReady) ? "NOT READY" : "READY", 1390, 980, 440, R.Ready, true, R.Connected && rules.HasStartingArmies);
            if (pickerGroup >= 0) BuildUnitPicker(rules, editable);
            if (lobbyDialog != LobbyDialog.None) BuildLobbyRules(rules, editable);
        }
        static double NextDuration(double current) => current > .9 ? .8 : current > .7 ? 2d / 3 : current > .55 ? .5 : 1;
        void BuildArmy(DuelRules rules, bool editable)
        {
            var panel = Panel("SHARED ARMY · BOTH TEAMS", 820, 190, 1010, 310);
            Label(panel, "ON THE FORT · same starting turrets for both teams", 24, 68, 950, 17, OzUi.Muted);
            ArmySlots(panel, rules.Common.FortTurrets, 0, 24, 95, 960, editable);
            Label(panel, "OUTSIDE THE FORT · same starting squads / vehicles for both teams", 24, 190, 950, 17, OzUi.Muted);
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
                var b = Button(panel, (i == 0 ? R.View.HostName : R.View.GuestName ?? "WAITING FOR PLAYER"), x, 65, 215, () => { rules.EnableHandicap(seat); R.ChangeRules(rules); }, rules.HandicapEnabled && rules.HandicapSlot == i, editable, 150);
                generalImages[i] = Hud.Node("GeneralPortrait", b).gameObject.AddComponent<RawImage>();
                generalImages[i].rectTransform.TL(47, 0, 120, 118); generalImages[i].raycastTarget = false; generalImages[i].enabled = false;
            }
            Label(panel, "ADDITIONALLY OUTSIDE THE FORT", 510, 68, 480, 16, OzUi.Muted);
            ArmySlots(panel, rules.Handicap.StartingUnits, 2, 510, 100, 480, editable && rules.HandicapEnabled);
            Label(panel, "BUILD TIME: " + (rules.Handicap.BuildDuration * 100).ToString("0.#") + "%  (×" + (1 / rules.Handicap.BuildDuration).ToString("0.##") + " speed)", 510, 190, 350, 16, OzUi.Ink);
            Button(panel, "CHANGE TIME", 865, 183, 120, () => { rules.Handicap.BuildDuration = NextDuration(rules.Handicap.BuildDuration); R.ChangeRules(rules); }, false, editable && rules.HandicapEnabled, 34);
            Label(panel, "Selected team only. Lower time = faster. One robot slot = one squad.", 510, 218, 475, 13, OzUi.Muted);
            Label(panel, rules.HandicapEnabled ? "Only " + (rules.HandicapSlot == 0 ? "RED" : "BLUE") + " receives these extras. The other team keeps the shared army." : "OFF: no bonus for either team. Select the player who should receive it.", 24, 240, 960, 16, OzUi.Muted);
        }
        void BuildMapPreview(Transform parent, DuelRules rules)
        {
            string key = JsonUtility.ToJson(DuelArmyData.From(rules.Common)) + JsonUtility.ToJson(DuelArmyData.From(rules.Handicap)) + rules.Map + rules.HandicapEnabled + rules.HandicapSlot + string.Join(",", rules.AllowedUnits);
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
            Label(panel, pickerGroup == 0 ? "Fort mounts accept cannons only." : "One robot squad or vehicle per slot.", 24, 72, 1150, 18, OzUi.Muted);
            if (pickerGroup == 0) BuildPickerCategory(panel, rules, "TURRETS", DuelRules.AllowedFortTurrets.Where(rules.Allows).ToArray(), 112);
            else
            {
                BuildPickerCategory(panel, rules, "ROBOTS", DuelRules.AllowedExtras.Where(t => ZBuildLists.IsRobot(t) && rules.Allows(t)).ToArray(), 112);
                BuildPickerCategory(panel, rules, "VEHICLES", DuelRules.AllowedExtras.Where(t => !ZBuildLists.IsRobot(t) && rules.Allows(t)).ToArray(), 292);
            }
            Button(panel, "EMPTY SLOT", 24, 625, 360, () => SetArmySlot(rules, ""));
        }
        void BuildPickerCategory(Transform panel, DuelRules rules, string title, string[] types, float y)
        {
            var category = Hud.Node(title, panel); category.TL(24, y, 1190, 310);
            Label(category, title, 0, 0, 1150, 18, OzUi.Amber);
            for (int i = 0; i < types.Length; i++)
            {
                string type = types[i];
                var button = Button(category, UnitName(type), i % 8 * 148, 30 + i / 8 * 140, 138, () => SetArmySlot(rules, type), false, true, 130);
                button.name = "SELECT " + type;
                Portrait(button, type, 29, 12, 80);
            }
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
            OpenChat();
        }
        void BuildMicrophone()
        {
            var panel = Panel("MICROPHONE SETTINGS", 420, 220, 1080, 650);
            Button(panel, R.Voice.MicrophoneEnabled ? "MUTE MICROPHONE" : "ENABLE MICROPHONE", 30, 70, 1020, () => { R.Voice.ToggleMicrophone(); dirty = true; });
            Button(panel, "MICROPHONE: " + R.Voice.Device + "  ▼", 30, 140, 1020, () => { deviceList = !deviceList; dirty = true; });
            gainValue = Label(panel, "", 30, 222, 1020, 20, OzUi.Ink);
            var track = Hud.Img(panel, "MicrophoneGain", OzUi.GM950); track.rectTransform.TL(30, 260, 1020, 28); track.raycastTarget = true;
            var fill = Hud.Img(track.transform, "Fill", OzUi.Amber); fill.rectTransform.Fill(); fill.raycastTarget = false;
            var handle = Hud.Img(track.transform, "Handle", OzUi.Ink); handle.rectTransform.sizeDelta = new Vector2(20, 36); handle.raycastTarget = true;
            var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 4; slider.value = R.Voice.Gain; slider.onValueChanged.AddListener(v => R.Voice.Gain = v);
            meterValue = Label(panel, "INPUT LEVEL", 30, 325, 1020, 20, OzUi.Ink);
            var meter = Hud.Img(panel, "InputMeterTrack", OzUi.GM950); meter.rectTransform.TL(30, 365, 1020, 28);
            inputMeter = Hud.Img(meter.transform, "InputMeter", OzUi.Go); inputMeter.rectTransform.Fill(); inputMeter.rectTransform.anchorMax = new Vector2(0, 1); inputMeter.raycastTarget = false;
            voiceStatus = Label(panel, R.Voice.Status, 30, 410, 1020, 18, OzUi.Muted);
            Button(panel, bindingTalk ? "PRESS A KEY… (ESC TO CANCEL)" : "PUSH TO TALK: " + R.Voice.TalkKey, 30, 460, 1020, () => { bindingTalk = true; dirty = true; });
            Button(panel, "BACK", 30, 560, 1020, () => { PlayerPrefs.Save(); page = microphoneReturn; dirty = true; });
            if (deviceList)
            {
                var devices = new[] { "System default" }.Concat(Microphone.devices).ToArray();
                var content = Scroll(panel, 30, 190, 1020, 330, false, Math.Max(330, devices.Length * 52));
                for (int i = 0; i < devices.Length; i++)
                {
                    string selected = i == 0 ? null : devices[i];
                    Button(content, devices[i], 0, i * 52, 1000, () => { R.Voice.SelectDevice(selected); deviceList = false; dirty = true; });
                }
            }
        }
        void BuildChat() { BuildChatPanel(70, 750, 920, 290); Button(root, "CLOSE CHAT", 1020, 960, 240, CloseOverlay); if (chatField) { chatField.ActivateInputField(); EventSystem.current.SetSelectedGameObject(chatField.gameObject); } }
        void BuildChatPanel(float x, float y, float w, float h)
        {
            var panel = Panel("TEXT CHAT", x, y, w, h);
            var mic = Button(panel, "", w - 170, 5, 65, () => { R.Voice.ToggleMicrophone(); dirty = true; });
            mic.name = "Toggle microphone";
            // A microphone silhouette uses UI primitives so it needs no external font or texture.
            var capsule = Hud.Img(mic, "Microphone capsule", R.Voice.MicrophoneEnabled ? OzUi.Go : OzUi.Muted); capsule.rectTransform.TL(27, 8, 10, 18); capsule.raycastTarget = false;
            var baseLine = Hud.Img(mic, "Microphone base", OzUi.Ink); baseLine.rectTransform.TL(21, 33, 22, 3); baseLine.raycastTarget = false;
            var stem = Hud.Img(mic, "Microphone stem", OzUi.Ink); stem.rectTransform.TL(30, 26, 4, 9); stem.raycastTarget = false;
            if (!R.Voice.MicrophoneEnabled) { var slash = Hud.Img(mic, "Muted", OzUi.AlertText); slash.rectTransform.TL(15, 22, 35, 3); slash.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); slash.raycastTarget = false; }
            Button(panel, "SETUP", w - 95, 5, 80, OpenMicrophone);
            Label(panel, string.Join("\n", R.Chat.Skip(Math.Max(0, R.Chat.Count - (h > 200 ? 5 : 2)))), 20, 48, w - 40, 17, OzUi.Ink);
            chatField = Input(panel, "", 20, h - 62, w - 210, chat, v => chat = v, 240);
            chatField.onEndEdit.AddListener(_ => { if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) SendChat(); });
            Button(panel, "SEND", w - 175, h - 62, 150, SendChat);
        }
        void SendChat() { R.SendChat(chat); chat = ""; dirty = true; }
        void Leave() { R.Close(); CloseOverlay(); if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadSceneAsync("MainMenu"); }
        public void LeaveMatch() { Leave(); }
        RectTransform Panel(string title, float x, float y, float w, float h)
        {
            var panel = Hud.Node(title, root); panel.TL(x, y, w, h); Hud.Plate(panel, "lg", 0, page == Page.Lobby ? (Color)OzUi.AmberLo : OzForkOptions.Accent);
            Label(panel, title, 24, 20, w - 48, 20, title.Contains("⚠") ? OzUi.AlertText : page == Page.Lobby ? (Color)OzUi.Amber : OzForkOptions.Accent); return panel;
        }
        static OzText Label(Transform parent, string text, float x, float y, float width, float size, Color color) => Hud.Label(parent, text, text, size, color).At(x, y, OzH.L, false, width);
        RectTransform Button(Transform parent, string text, float x, float y, float w, Action action, bool primary = false, bool enabled = true, float height = 46)
        {
            var b = HudButton.Make(parent, text, text, new Vector2(w, height), HudButtonKind.Large, w < 120 ? 12 : 17, .08f, primary ? HudButtonStyle.Primary : HudButtonStyle.Normal);
            if (!primary) b.LabelColor = page == Page.Lobby ? (Color)OzUi.Ink : OzForkOptions.Accent;
            b.Rt.TL(x, y, w, height); b.Interactable = enabled; b.Refresh();
            if (b.Label?.graphic is TMPro.TMP_Text fitted) { fitted.enableAutoSizing = true; fitted.fontSizeMin = 10; fitted.fontSizeMax = w < 120 ? 12 : 17; }
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
            var texture = OzUeArt.Picture(type, Team.Red);
            if (!texture) return;
            var image = Hud.Node("Portrait", parent).gameObject.AddComponent<RawImage>(); image.texture = texture; image.rectTransform.TL(x, y, size, size); image.raycastTarget = false;
        }
        static RectTransform Scroll(Transform parent, float x, float y, float w, float h, bool horizontal, float extent)
        {
            var viewport = Hud.Node("Viewport", parent); viewport.TL(x, y, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = OzUi.GM950; hit.raycastTarget = true;
            var content = Hud.Node("Content", viewport); content.TL(0, 0, horizontal ? extent : w, horizontal ? h : extent);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = horizontal; scroll.vertical = !horizontal; scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }
    }
}
