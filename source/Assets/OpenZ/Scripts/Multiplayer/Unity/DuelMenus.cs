using System;
using System.Collections;
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
        enum Page { Entry, Connect, Lobby, Loading, Game, Chat, Microphone, Error, End, Diagnostics, MatchRequest, Reconnect, Skirmish, RoomVote, RoomPlayers, SkirmishLoading }
        Page page, builtPage;
        Page microphoneReturn;
        bool deviceList;
        bool pauseWasOpen;
        Image inputMeter;
        OzText meterValue, gainValue, speakerValue, windowsOutput;
        readonly List<(string text, double expires, bool chat)> notices = new List<(string, double, bool)>();
        DuelRuntime R => DuelRuntime.I;
        Canvas canvas;
        RectTransform root, overlayRoot;
        OpenZMainMenu originalMenu;
        bool dirty = true;
        int pickerGroup = -1, pickerSlot;
        string previewKey;
        Texture2D mapPreview;
        string name = "PLAYER", address = "127.0.0.1", port = "27888", invite = "", chat = "";
        OzText ping, lobbyConnectionStatus, voiceStatus, planningTime;
        Image planningProgress;
        InputField chatField;
        OzText chatHistory;
        int chatHistoryLines;
        Image chatMicrophoneCapsule, chatMicrophoneMuted, chatInputMeter;
        OzText chatVoiceStatus, chatGain;
        ScrollRect lobbyChatScroll;
        Text lobbyChatLog;
        float onlineChatPosition;
        bool bindingChat, bindingTalk;
        bool advanced;
        string lastConnectionStatus, lastRoom;
        double endedAt = -1;
        public bool IsTyping => OzForkOptions.BindingChat || bindingChat || bindingTalk ||
            (OpenZHud.I && OpenZHud.I.Pause && OpenZHud.I.Pause.MicrophoneBinding) ||
            (EventSystem.current && EventSystem.current.currentSelectedGameObject &&
             EventSystem.current.currentSelectedGameObject.TryGetComponent<InputField>(out var input) && input.isFocused);
        Key ChatKey => (Key)PlayerPrefs.GetInt("oz_duel_chat_key", (int)Key.Enter);
        public bool CapturesInput => page != Page.Entry || (R.InMatch && !R.Playing && !R.Planning);
        public void Refresh() { dirty = true; }
        public void ShowConnect() { page = Page.Connect; dirty = true; }
        public void SetInvitation(string value) { invite = value; advanced = value.IndexOf("address=", StringComparison.OrdinalIgnoreCase) >= 0; ShowConnect(); }
        string invitationNotice;
        public void ShowLobby() { ResetOnlineLobby(); invitationNotice = null; lobbyTab = LobbyTab.Map; roomTab = 0; roomModeration = null; roomFullMap = roomCustomUnits = combatDropdown = roomPresets = false; page = Page.Lobby; dirty = true; }
        internal void ShowEndLobby()
        {
            invitationNotice = null;
            lobbyTab = LobbyTab.Map;
            roomTab = 0;
            pickerGroup = -1;
            lobbyDialog = LobbyDialog.None;
            CloseOnlinePlayerMenu();
            OpenOnlinePopup(OnlinePopup.None);
            page = Page.Lobby;
            dirty = true;
        }

        internal void ShowRoomPlayers() { FindFirstObjectByType<OpenZPauseMenu>()?.Close(); roomModeration = null; page = Page.RoomPlayers; dirty = true; }
        public void ShowError() { page = Page.Error; dirty = true; }
        public void ShowInGameMenu() { if (R.MatchMenusBlocked) return; CloseOverlay(); FindFirstObjectByType<OpenZPauseMenu>()?.Open(); }
        public void ShowCommunications() { OpenChat(); }
        internal void ShowRoomVote()
        {
            FindFirstObjectByType<OpenZPauseMenu>()?.Close();
            if (OzConfirm.Blocks) OzConfirm.Dismiss();
            ResetMapChangeDraft();
            page = Page.RoomVote;
            dirty = true;
        }
        internal void CloseRoomVote() { if (page == Page.RoomVote) CloseOverlay(); }
        public void CloseOverlay() { FindFirstObjectByType<OpenZPauseMenu>()?.Close(); page = Page.Entry; dirty = true; }
        public void NotifyChat(string message) => NotifyMatch(message, chat: true);

        internal void NotifyMatch(string message, bool chat = false)
        {
            if (!R.Playing) return;
            notices.Add((message, Time.realtimeSinceStartupAsDouble + 10, chat));
            if (notices.Count > 4) notices.RemoveAt(0);
            if (page == Page.Entry) dirty = true;
        }
        public void OpenChat() { if (R.MatchMenusBlocked) return; FindFirstObjectByType<OpenZPauseMenu>()?.Close(); page = Page.Chat; dirty = true; }
        void OpenMicrophone() { if (R.Playing) { CloseOverlay(); FindFirstObjectByType<OpenZPauseMenu>()?.OpenOptions(OpenZPauseMenu.MicrophoneTab); return; } microphoneReturn = page; deviceList = false; page = Page.Microphone; dirty = true; }
        void Awake() { name = PlayerPrefs.GetString("oz_duel_name", "PLAYER"); }
        void Update()
        {
            int proposalWait = (int)Math.Ceiling(R.ProposalWait);
            if (proposalWait != lastProposalWait) { lastProposalWait = proposalWait; if (page == Page.Lobby) dirty = true; }
            var nativePause = OpenZHud.I ? OpenZHud.I.Pause : null;
            bool pauseOpen = nativePause && nativePause.IsOpen;
            if (pauseOpen != pauseWasOpen) { pauseWasOpen = pauseOpen; dirty = true; }
            if (notices.RemoveAll(n => n.expires <= Time.realtimeSinceStartupAsDouble) > 0) dirty = true;
            if (!R.InMatch && notices.Count > 0) { notices.Clear(); dirty = true; }
            if (!OzAiMapGallery.IsOpen && (page == Page.Lobby || page == Page.Skirmish) && (pickerGroup >= 0 || lobbyDialog != LobbyDialog.None || combatDropdown) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { pickerGroup = -1; lobbyDialog = LobbyDialog.None; combatDropdown = false; dirty = true; }
            if (OnlineRoomPage && onlinePopup != OnlinePopup.None && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame && !IsTyping)
                OpenOnlinePopup(OnlinePopup.None);
            if (OnlineRoomPage && onlinePlayerMenu != null && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame && !IsTyping) CloseOnlinePlayerMenu();
            if (page == Page.Lobby && (lastConnectionStatus != R.ConnectionStatus || lastRoom != R.OnlineRoom))
            { lastConnectionStatus = R.ConnectionStatus; lastRoom = R.OnlineRoom; dirty = true; }
            if (R.InMatch && R.Game && R.Game.World.GameOver)
            {
                if (endedAt < 0) endedAt = Time.realtimeSinceStartupAsDouble;
                // The standard end screen owns the statistics and return-to-lobby controls.
            }
            else endedAt = -1;
            if (page == Page.Diagnostics && !R.Playing) { page = Page.Entry; dirty = true; }
            bool wasBinding = bindingChat || bindingTalk;
            if (wasBinding && Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (key.wasPressedThisFrame)
                    {
                        if (key.keyCode != Key.Escape) PlayerPrefs.SetInt(bindingTalk ? "oz_duel_talk_key" : "oz_duel_chat_key", (int)key.keyCode);
                        bindingChat = bindingTalk = false; PlayerPrefs.Save(); dirty = true; break;
                    }
            if (!R.EndLobbyOpen && !R.MatchMenusBlocked && ((R.InMatch && R.Playing) || page == Page.Microphone) && Keyboard.current != null && !wasBinding)
            {
                if (page == Page.Entry && !pauseOpen && !OzConfirm.Blocks && !IsTyping && DiagnosticsGesture())
                { page = Page.Diagnostics; dirty = true; }
                else if (page == Page.Entry && !pauseOpen && !OzConfirm.Blocks && !IsTyping && Keyboard.current[ChatKey].wasPressedThisFrame) OpenChat();
                else if (page != Page.Entry && !OzConfirm.Blocks && Keyboard.current.escapeKey.wasPressedThisFrame)
                { page = page == Page.Microphone ? microphoneReturn : Page.Entry; dirty = true; }
            }
            if (dirty) { dirty = false; Build(); }
            R.Voice.ChatMonitoring = chatInputMeter && R.Connected;
            if (page == Page.Diagnostics) UpdateDiagnosticsPanel();
            if ((page == Page.MatchRequest || page == Page.RoomVote) && R.MapChangeActive)
                requestTime?.Set(Math.Ceiling(R.MapChangeSeconds) + " s");
            else if (page == Page.MatchRequest) requestTime?.Set(Math.Ceiling(R.MatchRequestSeconds) + " s");
            if (BattlefieldLoading) UpdateBattlefieldLoading();
            if (page == Page.Reconnect) requestTime?.Set(Math.Ceiling(R.ReconnectSeconds).ToString());
            ping?.Set("PING " + R.PingMs.ToString("0") + " MS");
            lobbyConnectionStatus?.Set(R.ConnectionStatus);
            voiceStatus?.Set(R.Voice.Status);
            if (inputMeter) { inputMeter.rectTransform.anchorMax = new Vector2(R.Voice.InputLevel, 1); inputMeter.color = R.Voice.InputLevel > .95f ? OzUi.AlertText : OzUi.Go; }
            meterValue?.Set("INPUT LEVEL: " + Mathf.RoundToInt(R.Voice.InputLevel * 100) + "%");
            gainValue?.Set("MICROPHONE GAIN: " + Mathf.RoundToInt(R.Voice.Gain * 100) + "%");
            speakerValue?.Set("VOICE PLAYBACK: " + Mathf.RoundToInt(R.Voice.SpeakerVolume * 100) + "%");
            windowsOutput?.Set(OzWindowsSound.OutputStatus);
            if (chatInputMeter) UpdateChatMicrophone();
            if (planningTime != null)
            {
                var game = ZGame.I;
                double left = R.Planning ? R.Countdown : game ? game.SkirmishPlanningLeft : 0;
                int seconds = R.Planning ? R.View?.PlanningSeconds ?? 0 : game ? game.SkirmishPlanningSeconds : 0;
                planningTime.Set(Math.Max(0, Math.Ceiling(left)) + " s");
                planningProgress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)left / Math.Max(1, seconds)), 1);
            }
            if ((page == Page.Skirmish || OnlineRoomPage) && builtPage == page) FitSkirmishFrame();
            if (OnlineRoomPage) TickOnlinePings();
            HandleGhostKeys();
            TickLobbyFlyby();
        }
        void Build()
        {
            // Keep the active chat field, including its caret and selection, through refreshes.
            if (page == Page.Chat && builtPage == Page.Chat && chatField)
            {
                return;
            }
            builtPage = page;
            if (page != Page.Lobby)
            {
                lobbyDraft = null;
                pickerGroup = -1;
                lobbyDialog = LobbyDialog.None;
            }
            if (lobbyChatScroll && builtPage == Page.Lobby && R.SkirmishRoom)
                onlineChatPosition = lobbyChatScroll.verticalNormalizedPosition;
            if (overlayRoot) { overlayRoot.gameObject.SetActive(false); Destroy(overlayRoot.gameObject); }
            if (page != Page.Lobby && page != Page.Skirmish) ReleaseLobbyFlyby();
            lobbyFlybyImage = null;
            ping = lobbyConnectionStatus = voiceStatus = meterValue = gainValue = planningTime = null; inputMeter = planningProgress = null; chatField = null;
            speakerValue = windowsOutput = chatVoiceStatus = null;
            chatHistory = chatGain = null; lobbyChatScroll = null; lobbyChatLog = null;
            chatMicrophoneCapsule = chatMicrophoneMuted = chatInputMeter = null;
            R.Voice.Monitoring = page == Page.Microphone;
            if (!canvas)
            {
                var go = new GameObject("DuelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.layer = 5;
                go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            }
            root = Hud.Node("Duel", canvas.transform); root.Fill();
            overlayRoot = root;
            if (page == Page.Skirmish || OnlineRoomPage) root = CreateSkirmishFrame(overlayRoot);
            onlinePingLabels.Clear();
            onlineVariationLabels.Clear();
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
                if (R.MatchPaused && !pauseWasOpen) BuildPausedMatch();
                if (R.Playing && R.MatchGhost) BuildGhostView();
                if (R.Planning || ZGame.I && ZGame.I.SkirmishPlanningSeconds > 0)
                {
                    var banner = Panel("OPENING ORDERS", 24, 922, 600, 74);
                    var countdownBacking = Hud.Img(banner, "Countdown backing", new Color(.1f, .12f, .09f, .92f));
                    countdownBacking.rectTransform.Fill(); countdownBacking.transform.SetAsFirstSibling(); countdownBacking.raycastTarget = false;
                    planningTime = Hud.Label(banner, "Countdown", "", 28, OzUi.Amber).At(576, 14, OzH.R, false, 190);
                    planningTime.graphic.raycastTarget = false;
                    var track = Hud.Img(banner, "Countdown track", OzUi.GM950);
                    track.rectTransform.TL(24, 62, 552, 4); track.raycastTarget = false;
                    planningProgress = Hud.Img(track.transform, "Time remaining", OzUi.Amber);
                    planningProgress.rectTransform.Fill(); planningProgress.raycastTarget = false;
                }
                if (R.Playing && !FindObjectsByType<OpenZPauseMenu>(FindObjectsSortMode.None).Any(p => p.IsOpen))
                    for (int i = 0; i < notices.Count; i++)
                    {
                        float y = 922 - (notices.Count - 1 - i) * 78;
                        var entry = notices[i];
                        var notice = entry.chat
                            ? Button(root, "", 24, y, 1040, OpenChat, false, true, 74)
                            : Panel("", 24, y, 1040, 74);
                        notice.name = entry.chat ? "Incoming chat" : "Match notice";
                        Label(notice, entry.text, 14, 10, 1010, 17, OzUi.Ink).Body(14, 10, 1010);
                        if (entry.chat) FindFirstObjectByType<OpenZHud>()?.AddBlocker(notice);
                    }
                return;
            }
            if (page == Page.Chat) { BuildChat(); return; }
            if (page == Page.RoomVote) { BuildMapChange(); return; }
            if (page == Page.Diagnostics) { BuildDiagnosticsPanel(); return; }
            if (page == Page.MatchRequest || page == Page.Reconnect) { BuildMatchRequestPage(); return; }
            if (BattlefieldLoading) { BuildBattlefieldLoading(); return; }
            var background = Hud.Img(overlayRoot, "Background", OzUi.GM950); background.rectTransform.Fill(); background.raycastTarget = true;
            if (page == Page.Skirmish || OnlineRoomPage) background.transform.SetAsFirstSibling();
            var poster = Resources.Load<Texture2D>("OpenZ/Menu/" + (R.View?.Map ?? "L02") + "_poster");
            if (poster) { var raw = Hud.Node("MissionBackground", overlayRoot).gameObject.AddComponent<RawImage>(); raw.texture = poster; raw.color = new Color(.25f, .25f, .25f, 1); raw.rectTransform.Fill(); raw.raycastTarget = false; if (page == Page.Skirmish || OnlineRoomPage) raw.transform.SetSiblingIndex(1); }
            var wordmark = Hud.Kit(root, "ForkWordmark", "Wordmarks/wm_openz_vk_menu", new OzRect(80, 4, 300, 100), null, Image.Type.Simple);
            wordmark.preserveAspect = true; wordmark.enabled = wordmark.sprite != null;
            if (!wordmark.sprite) Label(root, "OpenZ-VK", 80, 40, 370, 57, OzForkOptions.Brand);
            bool roomLobby = page == Page.Skirmish || page == Page.Lobby && R.SkirmishRoom;
            Label(root, page == Page.Skirmish ? "Skirmish" : roomLobby ? "Multiplayer lobby" : page == Page.Connect ? "MULTIPLAYER" : "DUEL · 1 VS 1",
                roomLobby ? 395 : 80, roomLobby ? 43 : 108, 800, roomLobby ? 29 : 22, OzUi.Ink);
            if (page != Page.Skirmish && !OnlineRoomPage) ping = Label(root, "PING", 1570, 90, 250, 18, OzUi.Go);
            if (page == Page.Connect) BuildConnect();
            else if (page == Page.Skirmish) BuildSkirmish();
            else if (page == Page.Lobby) { if (R.SkirmishRoom) BuildOnlineRoom(); else BuildLobby(); }
            else if (page == Page.Game) BuildGameMenu();
            else if (page == Page.RoomPlayers) BuildInMatchRoomPlayers();
            else if (page == Page.Microphone) BuildMicrophone();
            else if (page == Page.End)
            {
                bool won = R.Game.World.IsWinningSeat(R.LocalTeam);
                Label(root, won ? "VICTORY" : "DEFEAT", 180, 300, 1400, 56, won ? OzUi.Go : OzUi.AlertText);
                Label(root, "THE DUEL HAS ENDED", 180, 390, 1400, 24, OzUi.Ink);
                Button(root, "BACK TO MENU", 180, 500, 600, Leave, true);
            }
            else if (page == Page.Error)
            {
                Label(root, "CONNECTION LOST", 180, 300, 1400, 34, OzUi.AlertText);
                Label(root, R.Status, 180, 370, 1400, 22, OzUi.Ink);
                Button(root, "BACK TO MENU", 180, 470, 400, Leave);
                Button(root, "ADVANCED TCP/IP", 180, 565, 600, () => { Leave(); advanced = true; ShowConnect(); });
            }
        }


        void BuildPausedMatch()
        {
            var panel = Hud.Img(root, "Paused match", OzUi.GM950);
            panel.rectTransform.TL(710, 457, 500, 166);
            Label(panel.transform, "MATCH PAUSED", 24, 16, 452, 28, OzUi.Ink);
            Label(panel.transform, R.OwnsMatchPause ? $"Hold Esc for {OpenZPauseMenu.DuelEscapeHoldSeconds} seconds to resume." : R.MatchPausePlayer + " paused the match.",
                24, 60, 452, 18, OzUi.Muted).Body(24, 60, 452);
            Button(panel.transform, "MATCH MENU", 24, 104, 145, ShowInGameMenu);
            Button(panel.transform, "CHAT", 179, 104, 102, OpenChat);
            if (R.OwnsMatchPause) Button(panel.transform, "RESUME MATCH", 291, 104, 185, R.ResumePausedMatch, true);
        }
        void BuildConnect()
        {
            if (!advanced)
            {
                BuildOnlineConnect();
                return;
            }
            Label(root, "TCP/IP (1 VS 1)", 80, 154, 850, 23, OzUi.Ink);
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
            Button(root, "BACK", 80, 950, 300, () => { advanced = false; dirty = true; });
        }
        void BuildOnlineConnect()
        {
            var left = Panel("CREATE A GAME", 80, 200, 940, 680);
            var right = Panel("JOIN AN ONLINE GAME", 1045, 200, 785, 315);
            Input(left, "YOUR NAME", 30, 99, 880, name, v => name = v, 32);
            Button(left, "CREATE ONLINE GAME", 30, 177, 880, () => { SaveName(); R.ConnectOnline(name, true, "", skirmish: true); },
                true, DuelRuntime.IsRoomMap(OzAiMapCatalog.ThreePlayerSkirmish), height: 65);
            Label(left, "Invite players by link. Add bots and set up teams.", 34, 252, 870, 19, OzUi.Muted);
            Button(left, "SKIRMISH", 30, 301, 880, ShowSkirmish, enabled: HasSkirmishScene(OzAiMapCatalog.ThreePlayerSkirmish), height: 65);
            Label(left, "Play with bots, make teams, or go free for all.", 34, 376, 870, 19, OzUi.Muted);
            Button(left, "TCP/IP (1 VS 1)", 30, 425, 880, () => { advanced = true; dirty = true; }, height: 65);
            Label(left, "Host or join a direct connection for two players.", 34, 500, 870, 19, OzUi.Muted);
            Button(left, "CLASSIC ONLINE DUEL", 30, 571, 420, () => { SaveName(); R.ConnectOnline(name, true, ""); });
            Input(right, "INVITATION LINK OR ROOM CODE", 30, 99, 725, invite, v => invite = v, 1000);
            Button(right, "PASTE", 30, 174, 190, () => { invite = GUIUtility.systemCopyBuffer.Trim(); dirty = true; });
            Button(right, "JOIN ONLINE GAME", 232, 174, 523, () => { SaveName(); R.ConnectOnline(name, false, invite); }, true);
            Button(root, "BACK", 80, 950, 300, CloseOverlay);
        }
        const string LocalSkirmishHost = "local";
        MultiplayerRoom localSkirmish;
        internal MultiplayerRoom LocalSkirmishMatch(string map) =>
            localSkirmish?.Launched == true && localSkirmish.Map == map ? localSkirmish : null;
        internal void ClearLocalSkirmishMatch()
        {
            if (localSkirmish?.Launched == true) localSkirmish.ReturnToLobby(LocalSkirmishHost);
        }

        int roomTab, roomSelectedSeat;
        bool roomArmyTotal;
        string roomModeration, roomModerationNote = "";
        string roomNameDraft;

        void BuildRoomRules(int starts, bool editable, SkirmishRules rules, Action<SkirmishRules> save)
        {
            string title = roomTab == 1 ? "STARTING ARMIES" : roomTab == 2 ? "BALANCING OPTIONS" : roomTab == 3 ? "PLAYER ADVANTAGE" : "BATTLE RULES";
            if (roomTab == 2) { DrawRoomBalance(rules, editable, save); return; }
            var panel = page == Page.Skirmish ? Panel(title, 60, 185, 1116, 780) : Panel(title, 60, 185, 1092, 568);
            if (roomTab == 4)
            {
                Label(panel, "ALLOWED UNITS", 30, 81, 480, 20, OzUi.Amber);
                Button(panel, "OPENZ", 30, 121, 155, () => SetRoster(false), !rules.ClassicUnits && !rules.CustomRoster, editable);
                Button(panel, "Z CLASSIC", 196, 121, 155, () => SetRoster(true), rules.ClassicUnits, editable);
                Button(panel, "CUSTOM", 362, 121, 155, () => { roomCustomUnits = true; dirty = true; }, rules.CustomRoster, editable);
                string tanks = rules.ClassicDynamics ? "zod-classic" : ZWorld.DuelProfile(rules.TankDynamics, ZBehaviorFamily.Tanks).id;
                string robots = rules.ClassicDynamics ? "zod-classic" : ZWorld.DuelProfile(rules.RobotDynamics, ZBehaviorFamily.Robots).id;
                var profileChoices = BuildCombatProfileChoices(panel, 554, 81, 505, tanks, robots, editable, (profile, kind) =>
                {
                    if (!TryEncodeCombatProfile(profile, kind, out string tankData, out string robotData)) return;
                    rules.ClassicDynamics = kind == 1;
                    rules.TankDynamics = tankData;
                    rules.RobotDynamics = robotData;
                    save(rules);
                });
                Label(panel, "WITHOUT A WORKING RADAR", 30, 191, 960, 20, OzUi.Amber);
                for (int i = 0; i <= (int)RadarLossRule.FogAndZoom; i++)
                {
                    var radar = (RadarLossRule)i;
                    Button(panel, OzBattleOptions.RadarName(radar), 30 + i * 259, 228, 247,
                        () => { rules.RadarLoss = radar; save(rules); }, rules.RadarLoss == radar, editable);
                    string[] help = { "Standard OpenZ behavior.", "Hide enemies outside your\nterritory and units’ sight.",
                        "Lock the camera in a\nwider overhead view.", "Hide unseen enemies and\nlock the camera wide." };
                    Label(panel, help[i], 32 + i * 259, 281, 235, 16, OzUi.Muted).Body(32 + i * 259, 281, 235);
                }
                Label(panel, "WIDER HOWITZER SPREAD", 30, 330, 750, 20, OzUi.Amber);
                Label(panel, "On: only howitzers fire with wider spread, on the ground or mounted on a fort.", 30, 365, 790, 16, OzUi.Muted);
                Button(panel, "OFF", 851, 329, 97, () => { rules.NerfedHowitzers = false; save(rules); }, !rules.NerfedHowitzers, editable);
                Button(panel, "ON", 959, 329, 97, () => { rules.NerfedHowitzers = true; save(rules); }, rules.NerfedHowitzers, editable);
                if (!rules.FreeForAll)
                {
                    Label(panel, "FRIENDLY FIRE", 30, 412, 740, 20, OzUi.Amber);
                    Label(panel, "On: explosions can damage your units and your allies.", 30, 448, 740, 16, OzUi.Muted);
                    Button(panel, "OFF", 851, 411, 97, () => { rules.FriendlyFire = false; save(rules); }, !rules.FriendlyFire, editable);
                    Button(panel, "ON", 959, 411, 97, () => { rules.FriendlyFire = true; save(rules); }, rules.FriendlyFire, editable);
                }
                if (page == Page.Skirmish || starts == 2) Button(panel, page == Page.Skirmish ? "SWAP STARTING SIDES" : "SWAP FORTS 1 / 2", 554, 488, 502, () =>
                {
                    if (page == Page.Skirmish) { localSkirmish.SwapStartingSides(LocalSkirmishHost); dirty = true; }
                    else R.SwapRoomStartingSides();
                }, enabled: editable);
                profileChoices?.SetAsLastSibling();
                return;

                void SetRoster(bool classic)
                {
                    rules.ClassicUnits = classic;
                    rules.CustomRoster = false;
                    RemoveDisallowedExtras(rules);
                    save(rules);
                }

            }
            if (roomTab == 1)
            {
                BuildSharedRoomArmy(panel, rules, editable, save);
                return;
            }
            bool individual = roomTab == 3;
            int seat = roomSelectedSeat = Mathf.Clamp(roomSelectedSeat, 0, starts - 1);
            if (individual)
            {
                for (int i = 0; i < starts; i++)
                {
                    int index = i;
                    Button(panel, TeamColors.Name((Team)(i + 1)).ToUpperInvariant(), 30 + i % 4 * 200, 55 + i / 4 * 42, 190,
                        () => { roomSelectedSeat = index; roomArmyTotal = false; dirty = true; }, seat == i, height: 35);
                }
                Label(panel, "Construction time", 30, 146, 450, 19, OzUi.Ink);
                var example = Label(panel, "", 30, 295, 790, 15, OzUi.Muted);
                void Preview(int value) => example.Set($"Example: a 100 s build takes {value} s before other rules.");
                Preview(rules.BuildPercent[seat]);
                LobbySlider(panel, "Construction time", 30, 175, 780, 50, 100, rules.BuildPercent[seat], "50%", "100% · standard",
                    value => value + "%", 600, editable, value => { rules.BuildPercent[seat] = value; save(rules); }, preview: Preview);
            }
            else Label(panel, "Each starting position receives these additions to its map army.", 30, 82, 790, 19, OzUi.Ink);
            var slots = individual ? rules.Extras : rules.Common;
            for (int i = 0; i < 4; i++)
            {
                int slot = individual ? seat * 4 + i : i;
                Button(panel, UnitName(slots[slot]), 30 + i % 2 * 400, (individual ? 318 : 132) + i / 2 * 54, 388,
                    () => OpenArmyPicker(individual ? 2 : 1, slot), enabled: editable);
            }
            if (!individual) return;
            Button(panel, roomArmyTotal ? "HIDE STARTING ARMY" : "VIEW STARTING ARMY", 30, 438, 450, () => { roomArmyTotal = !roomArmyTotal; dirty = true; });
            Button(panel, "RESET PLAYER", 495, 438, 315, () =>
            { Array.Clear(rules.Extras, seat * 4, 4); rules.BuildPercent[seat] = 100; save(rules); }, enabled: editable);
            if (roomArmyTotal)
            {
                var totals = rules.StartingUnits(seat).GroupBy(unit => unit).Select(group => group.Count() + " × " + group.Key.Replace('_', ' '));
                Label(panel, "Map army + " + (totals.Any() ? string.Join(", ", totals) : "no additions"), 30, 506, 790, 18, OzUi.Ink);
            }
        }

        void SelectRoomModeration(string id) { roomModeration = id; roomModerationNote = ""; dirty = true; }

        bool roomCustomUnits;
        bool combatDropdown;
        bool roomPresets;


        void BuildSharedRoomArmy(RectTransform panel, SkirmishRules rules, bool editable, Action<SkirmishRules> save)
        {
            var turrets = Hud.Img(panel, "Fort turrets", OzUi.GM950).rectTransform;
            turrets.TL(24, 69, 1044, 203);
            Label(turrets, "FORT TURRETS", 18, 17, 580, 20, OzUi.Amber);
            Label(turrets, rules.OverrideFortTurrets ? "Each occupied fort receives the same turrets." : "Using the map's original fort turrets.", 18, 50, 650, 17, OzUi.Muted);
            Button(turrets, "USE MAP TURRETS", 744, 12, 281, () =>
            {
                rules.OverrideFortTurrets = false;
                Array.Clear(rules.FortTurrets, 0, rules.FortTurrets.Length);
                save(rules);
            }, enabled: editable && rules.OverrideFortTurrets, height: 40);
            ArmyCards(turrets, rules.FortTurrets, 0);

            var units = Hud.Img(panel, "Additional units", OzUi.GM950).rectTransform;
            units.TL(24, 284, 1044, 248);
            Label(units, "ADDITIONAL UNITS NEAR THE FORT", 18, 17, 650, 20, OzUi.Amber);
            Label(units, "Each participant receives these units beside their starting fort.", 18, 50, 690, 17, OzUi.Muted);
            Button(units, "LOAD / SAVE UNIT PRESET", 712, 12, 313, () => { roomPresets = true; presetStatus = presetName = ""; dirty = true; },
                enabled: editable, height: 40);
            ArmyCards(units, rules.Common, 1);
            Label(units, "One robot slot adds one squad.", 18, 199, 850, 16, OzUi.Muted);

            void ArmyCards(RectTransform parent, string[] slots, int group)
            {
                for (int i = 0; i < 4; i++)
                {
                    int slot = i;
                    string unit = slots[i] ?? "";
                    var card = Button(parent, UnitName(unit), 18 + i * 254, 83, 246,
                        () => OpenArmyPicker(group, slot), enabled: editable, height: 102);
                    if (unit != "") Portrait(card, unit, 94, 8, 58);
                }
            }
        }

        void BuildRoomArmyPresets(SkirmishRules rules, Action<SkirmishRules> save)
        {
            var starting = new DuelRules();
            starting.Common.StartingUnits.AddRange(rules.Common);
            BuildArmyPresets(starting, true, next =>
            {
                rules.Common = next.Common.StartingUnits.Select(unit => rules.Allows(unit) ? unit : "").ToArray();
                save(rules);
            }, () => { roomPresets = false; dirty = true; });
        }

        static void RemoveDisallowedExtras(SkirmishRules rules)
        {
            foreach (var units in new[] { rules.Common, rules.Extras, rules.FortTurrets })
                for (int i = 0; i < units.Length; i++)
                    if (!string.IsNullOrEmpty(units[i]) && !rules.Allows(units[i])) units[i] = "";
        }

        void BuildRoomCustomUnits(SkirmishRules rules, Action<SkirmishRules> save)
        {
            var shade = Hud.Img(root, "Allowed units backdrop", new Color(0, 0, 0, .94f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var panel = Panel("ALLOWED UNITS", 220, 155, 1480, 790);
            Button(panel, "CLOSE", 1260, 12, 190, () => { roomCustomUnits = false; dirty = true; });
            var roster = new DuelRules();
            roster.SetUnitPreset(rules.CustomRoster ? DuelUnitPreset.Custom : rules.ClassicUnits ? DuelUnitPreset.Z1996 : DuelUnitPreset.OpenZ);
            if (rules.CustomRoster)
            {
                roster.AllowedUnits = rules.CustomUnits.ToList();
                roster.ExcludeMammoth = rules.ExcludeMammoth;
                roster.ExcludeBizon = rules.ExcludeBizon;
            }
            OzAllowedUnitsPanel.Build(panel, roster, true, () =>
            {
                rules.CustomRoster = true;
                rules.ClassicUnits = false;
                rules.CustomUnits = roster.AllowedUnits.ToArray();
                rules.ExcludeMammoth = roster.ExcludeMammoth;
                rules.ExcludeBizon = roster.ExcludeBizon;
                RemoveDisallowedExtras(rules);
                save(rules);
            }, roomRosterScroll, value => roomRosterScroll = value, false);
        }

        float roomRosterScroll = 1;

        void BuildRoomModeration(int x, int y, int width)
        {
            var member = R.HostedRoom.Members.FirstOrDefault(player => player.Id == roomModeration);
            var panel = Panel("MANAGE PLAYER", x, y, width, 465);
            Button(panel, "CLOSE", width - 146, 11, 125, () => { roomModeration = null; dirty = true; }, height: 36);
            if (member.Id == null) return;
            Label(panel, member.Name, 30, 55, width - 60, 24, OzUi.Ink);
            Button(panel, R.RoomParticipantMuted(member.Id) ? "RESTORE VOICE" : "MUTE VOICE", 30, 103, 380,
                () => R.ModerateRoomPlayer(member.Id, null, null));
            Input(panel, "Optional message to this player", 30, 175, width - 60, roomModerationNote, value => roomModerationNote = value, 160);
            Button(panel, "KICK PLAYER", 30, 244, (width - 75) / 2, () => Confirm(false));
            Button(panel, "KICK AND BAN", (width - 75) / 2 + 45, 244, (width - 75) / 2, () => Confirm(true));
            Label(panel, "A ban applies to this lobby and blocks rejoins from the same network.", 30, 320, width - 60, 16, OzUi.Muted);
            if (OnlineRoomPage && member.Seat.Role == MatchSeatRole.Human)
                Button(panel, "MOVE TO GHOSTS", 30, 368, width - 60, () =>
                {
                    R.SetRoomObserver(member.Id);
                    roomModeration = null;
                    dirty = true;
                }, enabled: OnlineEditing);
            void Confirm(bool ban) => OzConfirm.Ask(ban ? "BAN THIS PLAYER?" : "KICK THIS PLAYER?", member.Name, () =>
            { R.ModerateRoomPlayer(member.Id, ban, roomModerationNote); roomModeration = null; dirty = true; }, null);
        }

        void BuildInMatchRoomPlayers()
        {
            if (R.HostedRoom == null) { CloseOverlay(); return; }
            var panel = Panel("MATCH PLAYERS", 980, 222, 840, 545);
            int y = 55;
            foreach (var player in R.HostedRoom.Members.Where(member => member.Id != R.HostedRoom.HostId))
            {
                string id = player.Id;
                Button(panel, player.Name, 30, y, 770, () => SelectRoomModeration(id), height: 40);
                y += 52;
            }
            if (roomModeration != null) BuildRoomModeration(80, 222, 850);
            Button(root, "BACK TO MATCH", 980, 795, 320, CloseOverlay);
        }

        void SaveLocalRules(SkirmishRules rules)
        {
            localSkirmish.SetRules(LocalSkirmishHost, rules);
            dirty = true;
        }

        void LaunchLocalSkirmish()
        {
            var map = OzAiMapCatalog.FindSkirmishMap(localSkirmish.Map);
            localSkirmish.SetReady(LocalSkirmishHost, localSkirmish.Revision, true);
            if (!localSkirmish.CanLaunch) return;
            localSkirmish.Launch(LocalSkirmishHost);
            localSkirmish.MarkLoaded(LocalSkirmishHost, localSkirmish.Revision);
            OzPlayBlue.Disarm("local skirmish");
            ShowBattlefieldLoading(map.Id, Page.SkirmishLoading);
            StartCoroutine(LoadSkirmishMap(map));
        }

        IEnumerator LoadSkirmishMap(OzAiMap map)
        {
            // Let the loading screen render before Unity begins scene I/O.
            yield return null;
            yield return null;
            if (page != Page.SkirmishLoading) yield break;
            try { TrackBattlefieldLoad(map.ScenePath); }
            catch (Exception error)
            {
                Debug.LogException(error);
                localSkirmish.ReturnToLobby(LocalSkirmishHost);
                R.ReportError("Unable to load " + map.Name + ".");
            }
            if (battlefieldLoad == null) yield break;
            yield return battlefieldLoad;
            yield return null;
            if (page == Page.SkirmishLoading) CloseOverlay();
        }

        string roomTechMap;
        (int Vehicles, int Robots, int Fort) roomMapTech;
        ZMapData roomGrid;
        Texture2D roomGeneratedPreview;
        bool roomFullMap;

        void UpdateRoomMapData(string mapId)
        {
            roomGrid = ReadRoomGrid(mapId);
            roomMapTech = roomGrid?.FactoryTech() ?? default;
            roomTechMap = mapId;
            if (roomGeneratedPreview) Destroy(roomGeneratedPreview);
            roomGeneratedPreview = null;
        }

        void BuildRoomMapTech(Transform panel, float x, float y, (int Vehicles, int Robots, int Fort) mapTech,
            SkirmishRules rules, bool editable, Action<SkirmishRules> save, bool compact = false, bool proposal = false)
        {
            var tech = Hud.Img(panel, "Maximum factory tech", OzUi.GM950).rectTransform;
            tech.TL(x, y, 431, compact ? 225 : 275);
            Label(tech, "MAXIMUM FACTORY TECH", 16, 16, 399, 20, OzUi.Amber);
            int headings = compact ? 44 : 54;
            Label(tech, "FACTORY", 16, headings, 126, 15, OzUi.Muted);
            Label(tech, "MAP", 163, headings, 80, 15, OzUi.Muted);
            Label(tech, proposal && !editable ? "PROPOSED" : "ACTIVE", 277, headings, 125, 15, OzUi.Muted);
            RoomTechRow(tech, "VEHICLES", compact ? 65 : 83, mapTech.Vehicles, rules.VehicleTech, editable,
                value => { rules.VehicleTech = value; save(rules); }, !proposal || editable);
            RoomTechRow(tech, "ROBOTS", compact ? 105 : 128, mapTech.Robots, rules.RobotTech, editable,
                value => { rules.RobotTech = value; save(rules); }, !proposal || editable);
            int fortTech = rules.VehicleTech > 0 && (!proposal || mapTech.Vehicles > 0) ? rules.VehicleTech : mapTech.Fort;
            int fortY = compact ? 149 : 179;
            if (proposal)
            {
                Label(tech, "FORT", 16, fortY, 140, 17, OzUi.Ink);
                Label(tech, TechLabel(mapTech.Fort), 165, fortY, 62, 17, OzUi.Muted);
                Label(tech, TechLabel(fortTech), 284, fortY, 63, 17, fortTech == mapTech.Fort ? OzUi.Ink : OzUi.Amber);
            }
            else Label(tech, "Fort tech " + TechLabel(fortTech), 16, fortY, 399, 17, OzUi.Ink);
            if (!proposal || editable) Button(tech, "RESET TO MAP DEFAULTS", 16, compact ? 177 : 218, 354, () =>
            {
                rules.VehicleTech = rules.RobotTech = 0;
                save(rules);
            }, enabled: editable && (rules.VehicleTech != 0 || rules.RobotTech != 0), height: 36);
        }

        void BuildRoomOpeningOrders(Transform panel, float x, float y, float width, SkirmishRules rules, bool editable, Action<SkirmishRules> save)
        {
            var opening = Hud.Img(panel, "Opening orders", OzUi.GM950).rectTransform;
            opening.TL(x, y, width, 173);
            bool narrow = width < 300;
            Label(opening, "OPENING ORDERS", 16, 16, width - 115, narrow ? 15 : 20, OzUi.Amber);
            Label(opening, narrow ? "Queue orders before play." : "Queue orders before units start moving.", 16, 58, width - 32, 17, OzUi.Muted);
            LobbySlider(opening, "Opening orders seconds", 16, 97, width - 32, 0, DuelRules.MaxPlanningSeconds,
                rules.PlanningSeconds, "Off · 0 s", "20 s", value => value + " s", width - 104, editable,
                value => { rules.PlanningSeconds = value; save(rules); });
        }

        Texture2D RoomTacticalPreview(string id)
        {
            var preview = Resources.Load<Texture2D>("OpenZ/AiMaps/Previews/" + id);
            if (preview) return preview;
            if (!roomGeneratedPreview) roomGeneratedPreview = OzLevelInfo.Load(id).Mini;
            return roomGeneratedPreview;
        }

        void DrawRoomMap(RectTransform panel, Texture2D preview, int x, int y, int width, int height)
        {
            var image = Hud.Node("Tactical map", panel).gameObject.AddComponent<RawImage>();
            image.texture = preview;
            float scale = Mathf.Min((float)width / preview.width, (float)height / preview.height);
            image.rectTransform.TL(x + (width - preview.width * scale) / 2, y, preview.width * scale, preview.height * scale);
            image.raycastTarget = false;
            if (roomGrid == null) return;
            int pinSize = height > 300 ? 27 : 16;
            var forts = roomGrid.objects.Where(obj => obj.kind.StartsWith("building:fort", StringComparison.Ordinal))
                .GroupBy(obj => ZMapData.ParseOwner(obj.owner));
            foreach (var team in forts.Where(team => team.Key != Team.Neutral))
            {
                var fort = team.First();
                var pin = Hud.Img(image.transform, "Fort " + (int)team.Key, OzUi.TeamText(team.Key));
                float px = (fort.x + 4f) / roomGrid.width * preview.width * scale;
                float py = (fort.y + 4f) / roomGrid.height * preview.height * scale;
                pin.rectTransform.TL(px - pinSize / 2f, py - pinSize / 2f, pinSize, pinSize);
                pin.raycastTarget = false;
                Hud.Label(pin.transform, "Fort number", ((int)team.Key).ToString(), pinSize - 4, OzUi.GM950).FillParent(OzH.C);
            }
        }

        void BuildFullRoomMap(OzAiMap map)
        {
            var shade = Hud.Img(root, "Map backdrop", new Color(0, 0, 0, .85f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var panel = Panel(map.Name, 500, 100, 920, 880);
            var preview = RoomTacticalPreview(map.Id);
            if (preview) DrawRoomMap(panel, preview, 35, 90, 850, 736);
            Button(panel, "CLOSE", 708, 19, 180, () => { roomFullMap = false; dirty = true; });
        }

        internal static (int Vehicles, int Robots, int Fort) ReadMapTech(string mapId) => ReadRoomGrid(mapId)?.FactoryTech() ?? default;

        static ZMapData ReadRoomGrid(string mapId)
        {
            var grid = Resources.Load<TextAsset>("OpenZ/Maps/" + mapId + "_grid");
            return grid ? ZMapData.FromJson(grid.text) : null;
        }

        static bool HasSkirmishScene(OzAiMap map) =>
            SceneUtility.GetBuildIndexByScenePath(map.ScenePath) >= 0 && Resources.Load<TextAsset>("OpenZ/Maps/" + map.Id + "_grid");

        void RoomTechRow(Transform panel, string label, int y, int original, int level, bool editable, Action<int> save, bool showButtons = true)
        {
            int active = level == 0 ? original : level;
            Label(panel, label, 16, y + 8, 140, 17, OzUi.Ink);
            Label(panel, TechLabel(original), 165, y + 8, 62, 17, OzUi.Muted);
            if (showButtons) Button(panel, "−", 223, y, 43, () => save(active - 1 == original ? 0 : active - 1),
                enabled: editable && active > 1, height: 36);
            Label(panel, TechLabel(active), 284, y + 8, 63, 17, level == 0 ? OzUi.Ink : OzUi.Amber);
            if (showButtons) Button(panel, "+", 359, y, 43, () => save(active + 1 == original ? 0 : active + 1),
                enabled: editable && active > 0 && active < 6, height: 36);
        }

        void SaveName() { PlayerPrefs.SetString("oz_duel_name", name); PlayerPrefs.Save(); }
        enum LobbyTab { Map, StartingArmies, Balancing, BattleRules }
        LobbyTab lobbyTab;
        int mapVehicleTech, mapRobotTech, mapFortTech;

        void BuildLobby()
        {
            if (R.View == null)
            {
                lobbyConnectionStatus = Label(root, R.ConnectionStatus, 180, 350, 1500, 28, OzUi.Ink);
                Button(root, "CANCEL", 80, 1000, 250, Leave);
                return;
            }
            var official = R.Rules;
            var rules = lobbyDraft ?? official;
            bool inLobby = R.View.Phase == (int)DuelPhase.Lobby;
            bool editable = inLobby && (R.IsHost || lobbyDraft != null && draftRevision == R.View.Revision);
            bool waiting = inLobby && R.IsHost && string.IsNullOrEmpty(R.View.GuestName);
            // Serve both clients from the same host-owned revision, including when a guest is preparing a draft.
            BuildPlayerCards(inLobby);
            BuildProposalBar(official);
            string[] labels = { "MAP", "STARTING ARMIES", "BALANCING OPTIONS", "BATTLE RULES" };
            for (int i = 0; i < labels.Length; i++)
            {
                var next = (LobbyTab)i;
                Button(root, labels[i], 80 + 440 * i, 183, 426, () =>
                {
                    lobbyTab = next; lobbyDialog = LobbyDialog.None; pickerGroup = -1; dirty = true;
                }, next == lobbyTab, !waiting, 52);
            }
            if (lobbyTab != LobbyTab.Map) ReleaseLobbyFlyby();
            switch (lobbyTab)
            {
                case LobbyTab.Map: BuildLobbyMap(rules, editable, inLobby); break;
                case LobbyTab.StartingArmies: BuildArmy(rules, editable); break;
                case LobbyTab.Balancing: BuildBalancing(rules, editable); break;
                case LobbyTab.BattleRules: BuildLobbyBattleRules(rules, editable); break;
            }
            BuildLobbyChat();
            Button(root, "LEAVE", 80, 1008, 200, Leave);
            Label(root, R.ReleaseWarning.Length > 0 ? R.ReleaseWarning : rules.HasStartingArmies ? R.Status :
                "Each team needs a starting squad, vehicle or turret before READY.", 310, 1020, 1000, 16, OzUi.AlertText);
            Button(root, (R.IsHost ? R.View.HostReady : R.View.GuestReady) ? "NOT READY" : "READY",
                1440, 1000, 380, R.Ready, true, R.Connected && inLobby && official.HasStartingArmies && lobbyDraft == null, 56);
            if (waiting) { BuildInvitation(); return; }
            if (pickerGroup >= 0) BuildUnitPicker(rules, editable);
            if (lobbyDialog == LobbyDialog.OpenZMaps)
                BuildOpenZMapPicker(rules, inLobby && (R.IsHost || lobbyDraft == null || editable), R.View.Revision);
            else if (lobbyDialog != LobbyDialog.None) BuildLobbyRules(rules, editable);
        }

        void BuildPlayerCards(bool inLobby)
        {
            for (int team = 0; team < 2; team++)
            {
                bool hostTeam = R.View.TeamSlot(0) == team;
                string player = R.View.NameForTeam(team);
                var teamColor = team == 0 ? new Color(1f, .59f, .47f) : new Color(.59f, .77f, 1f);
                var card = Panel((team == 0 ? "RED" : "BLUE") + " TEAM · " + (hostTeam ? "HOST" : "GUEST"), 680 + team * 580, 12, 560, 112);
                var accent = Hud.Img(card, "Team accent", teamColor);
                accent.rectTransform.TL(0, 0, 5, 112); accent.raycastTarget = false;
                Label(card, player ?? "Waiting for player", 24, 58, 325, 26, OzUi.Ink);
                bool waitingForResults = R.WaitingForEndLobby(hostTeam == R.IsHost, team);
                bool ready = !R.EndLobbyOpen && R.View.ReadyForTeam(team);
                var status = Hud.Img(card, "Ready background", ready ? new Color(.13f, .26f, .18f) : OzUi.GM950);
                status.rectTransform.TL(355, 15, 181, 46); status.raycastTarget = false;
                var label = Hud.Label(status.transform, "Ready status", player == null || waitingForResults ? "WAITING" : ready ? "READY" : "NOT READY",
                    23, waitingForResults ? (Color)OzUi.Amber : ready ? (Color)OzUi.Go : OzUi.Ink).FillParent(OzH.C);
                label.graphic.raycastTarget = false;
                if (hostTeam == R.IsHost && player != null)
                    Button(card, "CHANGE NAME", 355, 67, 181, () =>
                    {
                        nameDraft = R.LocalLobbyName; renameStatus = "";
                        pickerGroup = -1; lobbyDialog = LobbyDialog.Rename; dirty = true;
                    }, false, inLobby && (R.IsHost || R.Connected), 32);
            }
        }

        void BuildInvitation()
        {
            foreach (Transform child in root)
            {
                var group = child.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
            }
            var shade = Hud.Img(root, "Invitation shade",  new Color(0, 0, 0, .78f));
            shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel("INVITE A PLAYER", 510, 278, 900, 484);
            var backing = Hud.Img(panel, "Invitation background", OzUi.GM950);
            backing.rectTransform.Fill(); backing.transform.SetAsFirstSibling(); backing.raycastTarget = true;
            Label(panel, "WAITING FOR YOUR OPPONENT", 38, 78, 820, 24, OzUi.Amber);
            Label(panel, "Copy your invitation link and send it to your opponent.", 38, 135, 824, 23, OzUi.Ink);
            Label(panel, R.OnlineMode ? "They open the link and choose Join in OpenZ." : "They open the invitation in OpenZ-VK to join.",
                38, 178, 824, 22, OzUi.Ink);
            Label(panel, "Both players must join and be ready before the match can start.", 38, 240, 824, 19, OzUi.Muted);
            var copied = Label(panel, invitationNotice ?? "", 38, 282, 824, 18, OzUi.Go);
            bool canCopy = !R.OnlineMode || !string.IsNullOrEmpty(R.OnlineRoom);
            Button(panel, canCopy ? "COPY INVITATION" : "CREATING INVITATION…", 38, 320, 824, () =>
            {
                GUIUtility.systemCopyBuffer = R.Invitation(address, R.OnlineMode ? 0 : int.Parse(port));
                invitationNotice = "Link copied. Send it to your opponent.";
                copied.Set(invitationNotice);
            }, true, canCopy, 61);
            Button(panel, "LEAVE LOBBY", 38, 412, 200, Leave);
        }

        void BuildLobbyMap(DuelRules rules, bool editable, bool inLobby)
        {
            var left = Panel("MAP", 80, 250, 1040, 515);
            int revision = R.View.Revision;
            bool mapEditable = inLobby && (R.IsHost || lobbyDraft == null || editable);
            Label(left, OzAiMapGallery.Definition(rules.Map)?.Name ?? rules.Map, 24, 62, 580, 27, OzUi.Ink);
            Button(left, "OPENZ MAPS", 640, 37, 176, () =>
            {
                openZMapId = OzLevelDef.All.FirstOrDefault(d => d.Id == rules.Map)?.Id ?? OzLevelDef.All[0].Id;
                lobbyDialog = LobbyDialog.OpenZMaps; dirty = true;
            });
            Button(left, "AI MAPS", 829, 37, 186, () =>
                OzAiMapGallery.Open(false, mapEditable, rules.Map, definition => ApplyLobbyMap(rules, revision, definition.Id)));
            if (lobbyDialog != LobbyDialog.OpenZMaps)
                BuildLobbyFlyby(left, rules.Map, new OzRect(24, 112, 992, 378), mute: true);
            BuildMapPreview(left, rules);

            var tech = Panel("MAXIMUM FACTORY TECH", 1140, 250, 680, 310);
            Button(tech, "RESET TO MAP DEFAULTS", 415, 12, 240, () =>
            {
                rules.VehicleTech = rules.RobotTech = 0; EditLobbyRules(rules);
            }, false, editable && (rules.VehicleTech != 0 || rules.RobotTech != 0), 40);
            Label(tech, "FACTORY TYPE", 24, 68, 220, 14, OzUi.Muted);
            Label(tech, "MAP DEFAULT", 265, 68, 155, 14, OzUi.Muted);
            Label(tech, "ACTIVE", 492, 68, 110, 14, OzUi.Muted);
            FactoryTechRow(tech, "VEHICLES", 95, mapVehicleTech, rules.VehicleTech, editable,
                value => { rules.VehicleTech = value; EditLobbyRules(rules); });
            FactoryTechRow(tech, "ROBOTS", 151, mapRobotTech, rules.RobotTech, editable,
                value => { rules.RobotTech = value; EditLobbyRules(rules); });
            Label(tech, "FORT TECH  " + TechLabel(rules.VehicleTech > 0 && mapVehicleTech > 0 ? rules.VehicleTech : mapFortTech),
                24, 219, 625, 18, OzUi.Ink);
            Label(tech, "Each factory in the group shifts by the same number of levels.", 24, 253, 638, 16, OzUi.Muted);
            Label(tech, "Fort tech follows the vehicle override.", 24, 278, 630, 16, OzUi.Muted);

            var opening = Panel("OPENING ORDERS", 1140, 574, 680, 191);
            Label(opening, "Queue unit orders before the battle starts.", 24, 64, 630, 18, OzUi.Ink);
            Label(opening, "Units move when the timer reaches zero.", 24, 91, 630, 16, OzUi.Muted);
            LobbySlider(opening, "Opening orders seconds", 24, 130, 625, 0, DuelRules.MaxPlanningSeconds, rules.PlanningSeconds,
                "OFF · 0 s", "20 s", value => value == 0 ? "OFF" : value + " s", 560, editable,
                value => { rules.PlanningSeconds = value; EditLobbyRules(rules); });
        }

        static string TechLabel(int level) => new[] { "—", "I", "II", "III", "IV", "V", "VI" }[level];

        void FactoryTechRow(Transform parent, string label, int y, int mapLevel, int setting, bool editable, Action<int> apply)
        {
            int active = setting == 0 ? mapLevel : setting;
            Label(parent, label, 24, y + 10, 220, 18, OzUi.Ink);
            Label(parent, TechLabel(mapLevel), 282, y + 10, 115, 19, OzUi.Muted);
            Button(parent, "−", 424, y, 50, () => apply(active - 1 == mapLevel ? 0 : active - 1), false, editable && mapLevel > 0 && active > 1, 40);
            var value = Hud.Img(parent, label + " Tech", OzUi.GM950);
            value.rectTransform.TL(483, y, 102, 40); value.raycastTarget = false;
            Hud.Label(value.transform, "Level", TechLabel(active), 20, setting == 0 ? OzUi.Ink : (Color)OzUi.Amber).FillParent(OzH.C);
            Button(parent, "+", 596, y, 50, () => apply(active + 1 == mapLevel ? 0 : active + 1), false, editable && mapLevel > 0 && active < 6, 40);
        }

        static void LobbySlider(Transform panel, string name, float x, float y, float w, int min, int max, int initial,
            string left, string right, Func<int, string> format, float readoutX, bool editable, Action<int> commit,
            bool reverse = false, Action<int> preview = null)
        {
            var value = Hud.Img(panel, name + " current value", OzUi.GM950);
            value.rectTransform.TL(readoutX, 10, 92, 38); value.raycastTarget = false;
            var readout = Hud.Label(value.transform, "Value", format(initial), 20, OzUi.Amber).FillParent(OzH.C);
            var hit = Hud.Img(panel, name, new Color(0, 0, 0, .004f));
            hit.rectTransform.TL(x, y, w, 32); hit.raycastTarget = true;
            var track = Hud.Img(hit.transform, "Track", new Color(.29f, .30f, .26f, 1f));
            track.rectTransform.TL(7, 12, w - 14, 8); track.raycastTarget = false;
            var fill = Hud.Img(track.transform, "Fill", OzUi.Amber);
            fill.rectTransform.Fill(); fill.raycastTarget = false;
            var handleArea = Hud.Node("Handle area", hit.transform); handleArea.Fill(7, 0, 7, 0);
            var handle = Hud.Img(handleArea, "Handle", OzUi.Amber);
            handle.rectTransform.sizeDelta = new Vector2(15, -8); handle.raycastTarget = false;
            var slider = hit.gameObject.AddComponent<DuelLobbySlider>();
            slider.SetDirection(reverse ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight, false);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = true; slider.value = initial; slider.interactable = editable;
            slider.onValueChanged.AddListener(v => { readout.Set(format((int)v)); preview?.Invoke((int)v); });
            slider.Commit = v => { if ((int)v != initial) commit((int)v); };
            Label(panel, left, x, y + 37, 240, 14, OzUi.Muted);
            Hud.Label(panel, right, right, 14, OzUi.Muted).At(x + w, y + 37, OzH.R, false, 240);
        }

        static double NextDuration(double current) => current > .9 ? .8 : current > .7 ? 2d / 3 : current > .55 ? .5 : 1;

        void BuildArmy(DuelRules rules, bool editable)
        {
            var turrets = Panel("FORT TURRETS", 80, 250, 1740, 230);
            Label(turrets, "Both forts receive the same turrets. Mounts are assigned automatically.", 24, 62, 1660, 18, OzUi.Muted);
            ArmySlots(turrets, rules.Common.FortTurrets, 0, 24, 104, 1694, editable);
            var units = Panel("ADDITIONAL UNITS NEAR THE FORT", 80, 496, 1740, 269);
            Label(units, "Both teams receive these units near their fort at the start of the match.", 24, 65, 1660, 18, OzUi.Muted);
            Button(units, "LOAD / SAVE UNIT PRESET", 1426, 15, 290, () =>
            {
                lobbyDialog = LobbyDialog.ArmyPresets; presetName = presetStatus = ""; dirty = true;
            });
            ArmySlots(units, rules.Common.StartingUnits, 1, 24, 110, 1694, editable);
            Label(units, "One robot slot adds one squad. Unit presets apply to these four slots.", 24, 216, 1660, 17, OzUi.Muted);
        }

        void ArmySlots(Transform parent, IList<string> units, int group, float x, float y, float width, bool editable)
        {
            float step = width / 4;
            for (int i = 0; i < 4; i++)
            {
                int slot = i; string type = i < units.Count ? units[i] : "";
                var button = Button(parent, UnitName(type), x + i * step, y, step - 10, () => OpenArmyPicker(group, slot), false, editable, 76);
                button.name = "ARMY SLOT " + group + ":" + slot;
                Portrait(button, type, (step - 54) / 2, 2, 44);
            }
        }
        internal static string UnitName(string type) => string.IsNullOrEmpty(type) ? "EMPTY" : type == "light" ? "LIGHT TANK" : type == "medium" ? "MEDIUM MECH" : type == "mtank" ? "MEDIUM TANK" : type == "heavy" ? "HEAVY TANK" : type == "pyro" ? "PYROS" : type.Replace('_', ' ').ToUpperInvariant();
        void BuildBalancing(DuelRules rules, bool editable)
        {
            var territory = Panel("TERRITORY CONSTRUCTION TIME BONUS CAP", 80, 250, 858, 292);
            Label(territory, "Limits how much flag ownership can shorten construction time.", 24, 63, 800, 17, OzUi.Ink);
            LobbySlider(territory, "Territory cap percent", 24, 101, 805, 10, 50,
                Mathf.Clamp(Mathf.RoundToInt((float)rules.TerritoryReductionCap * 100), 10, 50),
                "50% · ORIGINAL", "10%", value => value + "%", 742, editable,
                value => { rules.TerritoryReductionCap = value / 100d; EditLobbyRules(rules); }, reverse: true);
            Button(territory, (rules.ProgressiveTerritoryCap ? "[X] " : "[ ] ") + "DECREASING PROGRESSIVELY", 24, 164, 420,
                () => { rules.ProgressiveTerritoryCap = !rules.ProgressiveTerritoryCap; EditLobbyRules(rules); },
                rules.ProgressiveTerritoryCap, editable, 38);
            Label(territory, "Off: the same fixed cap applies to both teams.", 24, 211, 805, 16, OzUi.Muted);
            Label(territory, "On: the stronger side’s cap drops from 50% to your setting at a +50% army lead.", 24, 234, 805, 16, OzUi.Muted);
            Label(territory, "See the army gap in the Attacking Power display at the top of the battlefield.", 24, 257, 805, 15, OzUi.Muted);

            var fort = Panel("FORT CONSTRUCTION SPEEDUP WHEN LOSING", 956, 250, 864, 292);
            Label(fort, "The losing fort gains a multiple of the opponent’s territorial speed bonus.", 24, 63, 806, 17, OzUi.Ink);
            LobbySlider(fort, "Fort speedup percent", 24, 101, 813, 0, 500,
                Mathf.RoundToInt((float)rules.FortSpeedup * 100), "OFF · 0%", "500%",
                value => value == 0 ? "OFF" : value + "%", 748, editable,
                value => { rules.FortSpeedup = value / 100d; rules.FortLoserMaximum = 0; EditLobbyRules(rules); });
            Button(fort, (rules.ProgressiveFortSpeedup ? "[X] " : "[ ] ") + "INCREASING PROGRESSIVELY", 24, 164, 420,
                () => { rules.ProgressiveFortSpeedup = !rules.ProgressiveFortSpeedup; EditLobbyRules(rules); },
                rules.ProgressiveFortSpeedup, editable, 38);
            Label(fort, "On: starts as soon as you trail and reaches full strength at a −20% army gap.", 24, 211, 808, 16, OzUi.Muted);
            Label(fort, "Off: the full boost applies as soon as you trail. Check the same army score above the field.", 24, 234, 808, 15, OzUi.Muted);
            Label(fort, "Example: 500% × an enemy’s +25% territorial speed gives the fort +125% speed.", 24, 257, 808, 15, OzUi.Muted);

            var artillery = Panel("NERFED HOWITZERS", 80, 553, 1740, 59);
            Label(artillery, "On: only howitzers fire with wider spread, on the ground or mounted on a fort.", 310, 22, 1150, 16, OzUi.Muted);
            Button(artillery, rules.NerfedHowitzers ? "ON" : "OFF", 1560, 10, 155,
                () => { rules.NerfedHowitzers = !rules.NerfedHowitzers; EditLobbyRules(rules); }, rules.NerfedHowitzers, editable, 38);

            var player = Panel("PLAYER ADVANTAGE", 80, 623, 1740, 142);
            Label(player, "Give one side extra units, shorter build times, or both.", 24, 55, 730, 16, OzUi.Muted);
            Button(player, "OFF", 24, 85, 110, () => { rules.HandicapEnabled = false; EditLobbyRules(rules); },
                !rules.HandicapEnabled, editable, 42).name = "PLAYER ADVANTAGE OFF";
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                Button(player, i == 0 ? "RED" : "BLUE", 145 + 162 * i, 85, 149,
                    () => { rules.EnableHandicap(slot); EditLobbyRules(rules); },
                    rules.HandicapEnabled && rules.HandicapSlot == slot, editable, 42).name = "PLAYER ADVANTAGE " + slot;
            }
            Button(player, "BUILD TIME · " + (rules.Handicap.BuildDuration * 100).ToString("0.#") + "%", 465, 85, 320,
                () => { rules.Handicap.BuildDuration = NextDuration(rules.Handicap.BuildDuration); EditLobbyRules(rules); },
                false, editable && rules.HandicapEnabled, 42);
            Label(player, "EXTRA STARTING UNITS" + (rules.HandicapEnabled ? (rules.HandicapSlot == 0 ? " FOR RED" : " FOR BLUE") : ""),
                825, 19, 670, 17, OzUi.Amber);
            Button(player, "AI HELP", 1505, 9, 209, () => { lobbyDialog = LobbyDialog.AiHelp; dirty = true; }, false, true, 36);
            ArmySlots(player, rules.Handicap.StartingUnits, 2, 825, 53, 890, editable && rules.HandicapEnabled);
        }

        void BuildLobbyBattleRules(DuelRules rules, bool editable)
        {
            var units = Panel("ALLOWED UNITS", 80, 250, 680, 515);
            Label(units, "Choose which units can be used in this match.", 24, 67, 630, 18, OzUi.Ink);
            Button(units, "OPENZ", 24, 126, 630, () => { rules.SetUnitPreset(DuelUnitPreset.OpenZ); EditLobbyRules(rules); },
                rules.UnitPreset == DuelUnitPreset.OpenZ, editable, 58);
            Button(units, "Z CLASSIC", 24, 202, 630, () => { rules.SetUnitPreset(DuelUnitPreset.Z1996); EditLobbyRules(rules); },
                rules.UnitPreset == DuelUnitPreset.Z1996, editable, 58);
            Button(units, "CUSTOM", 24, 278, 630, () => { lobbyDialog = LobbyDialog.Units; dirty = true; },
                rules.UnitPreset == DuelUnitPreset.Custom, true, 58).name = "CUSTOM ALLOWED UNITS";

            var battle = Panel("BATTLE RULES", 778, 250, 1042, 515);
            string tanks = ZWorld.DuelProfile(rules.TankDynamics, ZBehaviorFamily.Tanks).id;
            string robots = ZWorld.DuelProfile(rules.RobotDynamics, ZBehaviorFamily.Robots).id;
            var profileChoices = BuildCombatProfileChoices(battle, 24, 62, 990, tanks, robots, editable, (profile, kind) =>
            {
                rules.TankDynamics = kind == 0 ? "" : profile.Tanks.Encode();
                rules.RobotDynamics = kind == 0 ? "" : profile.Robots.Encode();
                EditLobbyRules(rules);
            });
            Label(battle, "WHEN YOUR LAST RADAR IS LOST", 24, 304, 991, 18, OzUi.Ink);
            var tip = Label(battle, "", 24, 394, 990, 16, OzUi.Muted);
            for (int i = 0; i <= (int)RadarLossRule.FogAndZoom; i++)
            {
                var option = (RadarLossRule)i;
                var choice = Button(battle, OzBattleOptions.RadarName(option), 24 + 248 * i, 335, 239,
                    () => { rules.RadarLoss = option; EditLobbyRules(rules); }, rules.RadarLoss == option, editable);
                AddLobbyTip(choice, tip, OzBattleOptions.RadarTip(option));
            }
            if (R.IsHost) Button(battle, "SWAP RED / BLUE", 705, 444, 310, R.SwapTeams, false, R.Connected && R.View.GuestName != null);
            profileChoices?.SetAsLastSibling();
        }

        void BuildMapPreview(Transform parent, DuelRules rules)
        {
            string key = JsonUtility.ToJson(DuelArmyData.From(rules.Common)) + JsonUtility.ToJson(DuelArmyData.From(rules.Handicap)) + rules.Map + rules.HandicapEnabled + rules.HandicapSlot + string.Join(",", rules.AllowedUnits)
                + "|red:" + string.Join(",", rules.FortTurretsFor(0)) + "|blue:" + string.Join(",", rules.FortTurretsFor(1));
            if (!mapPreview || previewKey != key)
            {
                var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
                var map = Resources.Load<TextAsset>("OpenZ/Maps/" + rules.Map + "_grid");
                var world = ZWorld.Create(settings.text, map.text, 1);
                mapVehicleTech = world.MapFactoryTech("vehicle_factory");
                mapRobotTech = world.MapFactoryTech("robot_factory");
                mapFortTech = world.Objects.Where(o => o.IsFort).Select(o => o.Level + 1).DefaultIfEmpty(0).Max();
                world.ConfigureDuel(rules, false); world.DuelApplyStartingArmies();
                if (mapPreview) Destroy(mapPreview);
                mapPreview = OzLevelInfo.Bake(world, rules.Map, true); previewKey = key;
            }
            var inset = Hud.Img(parent, "Tactical map backing", OzUi.GM950);
            inset.rectTransform.TL(692, 189, 309, 285); inset.raycastTarget = false;
            Label(inset.transform, "TACTICAL MAP", 14, 12, 280, 16, OzUi.Amber);
            var raw = Hud.Node("TacticalMap", inset.transform).gameObject.AddComponent<RawImage>(); raw.texture = mapPreview;
            float scale = Math.Min(280f / mapPreview.width, 227f / mapPreview.height);
            float width = mapPreview.width * scale, height = mapPreview.height * scale;
            raw.rectTransform.TL((309 - width) / 2, 43 + (227 - height) / 2, width, height); raw.raycastTarget = false;
        }

        void BuildUnitPicker(DuelRules rules, bool editable)
        {
            if (!editable || (pickerGroup == 2 && !rules.HandicapEnabled)) { pickerGroup = -1; return; }
            bool fort = pickerGroup == 0;
            var slots = fort ? (IList<string>)rules.Common.FortTurrets :
                pickerGroup == 1 ? rules.Common.StartingUnits : rules.Handicap.StartingUnits;
            BuildArmyGallery(fort, pickerSlot, slots.ElementAtOrDefault(pickerSlot), rules.Allows,
                type => SetArmySlot(rules, type), CloseArmyPicker);
        }

        void OpenArmyPicker(int group, int slot)
        {
            pickerGroup = group;
            pickerSlot = slot;
            dirty = true;
        }

        void CloseArmyPicker()
        {
            pickerGroup = -1;
            dirty = true;
        }

        void BuildSkirmishUnitPicker(SkirmishRules rules)
        {
            bool fort = pickerGroup == 0;
            var slots = fort ? rules.FortTurrets : pickerGroup == 1 ? rules.Common : rules.Extras;
            int slot = pickerSlot;
            BuildArmyGallery(fort, slot, slots[slot], rules.Allows, type =>
            {
                slots[slot] = type;
                if (fort) rules.OverrideFortTurrets = true;
                CloseArmyPicker();
                SaveLocalRules(rules);
            }, CloseArmyPicker);
        }

        void BuildArmyGallery(bool fort, int slot, string selected, Func<string, bool> allows, Action<string> choose, Action close)
        {
            var shade = Hud.Img(root, "UnitPickerShade", new Color(0, 0, 0, .85f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            string title = fort ? "ON THE FORT · SELECT CANNON" : "OUTSIDE THE FORT · SELECT UNIT";
            var panel = Panel(title + " · SLOT " + (slot % 4 + 1), 340, 180, 1240, 720);
            var backing = Hud.Img(panel, "UnitPickerBackground", OzUi.GM950);
            backing.rectTransform.Fill();
            backing.transform.SetAsFirstSibling();
            backing.raycastTarget = true;
            Button(panel, "CLOSE", 1030, 14, 180, close);
            Label(panel, fort ? "Fort mounts accept cannons only." : "One robot squad or vehicle per slot.", 24, 72, 1150, 18, OzUi.Muted);
            if (fort) Category("TURRETS", DuelRules.AllowedFortTurrets.Where(allows).ToArray(), 112);
            else
            {
                var units = DuelRules.AllowedExtras.Where(allows);
                Category("ROBOTS", units.Where(ZBuildLists.IsRobot).ToArray(), 112);
                Category("VEHICLES", units.Where(t => !ZBuildLists.IsRobot(t)).ToArray(), 292);
            }
            Button(panel, "EMPTY SLOT", 24, 625, 360, () => choose(""), primary: string.IsNullOrEmpty(selected));

            void Category(string name, string[] types, float y)
            {
                var category = Hud.Node(name, panel); category.TL(24, y, 1190, 310);
                Label(category, name, 0, 0, 1150, 18, OzUi.Amber);
                for (int i = 0; i < types.Length; i++)
                {
                    string type = types[i];
                    var button = Button(category, UnitName(type), i % 8 * 148, 30 + i / 8 * 140, 138,
                        () => choose(type), type == selected, height: 130);
                    button.name = "SELECT " + type;
                    Portrait(button, type, 29, 12, 80);
                }
            }
        }
        void SetArmySlot(DuelRules rules, string type)
        {
            if (pickerGroup == 0)
            {
                rules.Common.FortTurrets[pickerSlot] = type;
                rules.RedFortTurrets = rules.BlueFortTurrets = null;
            }
            else
            {
                var units = pickerGroup == 1 ? rules.Common.StartingUnits : rules.Handicap.StartingUnits;
                while (units.Count <= pickerSlot) units.Add("");
                units[pickerSlot] = type;
            }
            pickerGroup = -1; EditLobbyRules(rules);
        }
        void OnDestroy()
        {
            foreach (var minimap in skirmishGeneratedMinimaps.Values)
                if (minimap) Destroy(minimap);
            if (roomGeneratedPreview) Destroy(roomGeneratedPreview);
            ReleaseLobbyFlyby();
            if (mapPreview) Destroy(mapPreview);
        }
        void BuildGameMenu()
        {
            OpenChat();
        }
        void BuildMicrophone()
        {
            var panel = Panel(OzForkOptions.AudioDevicesLabel, 420, 143, 1080, 790);
            Button(panel, R.RoomVoiceMuted ? "VOICE MUTED BY HOST" : "MICROPHONE: " + R.Voice.ModeName + " · CLICK TO CHANGE", 30, 70, 1020,
                () => { R.Voice.CycleMode(); dirty = true; }, enabled: !R.RoomVoiceMuted);
            Button(panel, "MICROPHONE: " + R.Voice.Device + "  v", 30, 140, 1020, () => { deviceList = !deviceList; dirty = true; });
            gainValue = Label(panel, "", 30, 214, 1020, 20, OzUi.Ink);
            AudioSlider(panel, "MicrophoneGain", 245, 4, R.Voice.Gain, v => R.Voice.Gain = v);
            meterValue = Label(panel, "INPUT LEVEL", 30, 291, 1020, 20, OzUi.Ink);
            var meter = Hud.Img(panel, "InputMeterTrack", OzUi.GM950); meter.rectTransform.TL(30, 320, 1020, 20);
            inputMeter = Hud.Img(meter.transform, "InputMeter", OzUi.Go); inputMeter.rectTransform.Fill(); inputMeter.rectTransform.anchorMax = new Vector2(0, 1); inputMeter.raycastTarget = false;
            voiceStatus = Label(panel, R.Voice.Status, 30, 354, 1020, 18, OzUi.Muted);
            Button(panel, bindingTalk ? "PRESS A KEY… (ESC TO CANCEL)" : "TAP TO SPEAK: " + R.Voice.TalkKey, 30, 390, 1020, () => { bindingTalk = true; dirty = true; });
            speakerValue = Label(panel, "", 30, 471, 1020, 20, OzUi.Ink);
            AudioSlider(panel, "SpeakerVolume", 502, 1, R.Voice.SpeakerVolume, v => R.Voice.SpeakerVolume = v);
            if (OzWindowsSound.Available)
            {
                windowsOutput = Label(panel, "", 30, 552, 1020, 18, OzUi.Muted);
                Button(panel, "−", 30, 590, 75, () => OzWindowsSound.StepVolume(-.1f));
                Button(panel, "+", 115, 590, 75, () => OzWindowsSound.StepVolume(.1f));
                Button(panel, "TOGGLE WINDOWS MUTE", 200, 590, 340, OzWindowsSound.ToggleMute);
                Button(panel, "CHOOSE OUTPUT IN WINDOWS", 550, 590, 300, OzWindowsSound.OpenSettings);
                Button(panel, "WINDOWS MIXER", 860, 590, 190, OzWindowsSound.OpenMixer);
            }
            Button(panel, "BACK", 30, 695, 1020, () => { PlayerPrefs.Save(); page = microphoneReturn; dirty = true; });
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
        static void AudioSlider(RectTransform panel, string name, float y, float max, float value, Action<float> changed)
        {
            var track = Hud.Img(panel, name, OzUi.GM950); track.rectTransform.TL(30, y, 1020, 20); track.raycastTarget = true;
            var fill = Hud.Img(track.transform, "Fill", OzUi.Amber); fill.rectTransform.Fill(); fill.raycastTarget = false;
            var handle = Hud.Img(track.transform, "Handle", OzUi.Ink); handle.rectTransform.sizeDelta = new Vector2(20, 28); handle.raycastTarget = true;
            var slider = track.gameObject.AddComponent<Slider>(); slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.maxValue = max; slider.value = value; slider.onValueChanged.AddListener(v => changed(v));
        }
        void BuildChat()
        {
            BuildChatPanel(70, 750, 920, 290);
            Button(root, "CLOSE CHAT", 1020, 960, 240, CloseOverlay);
            FocusChatField();
        }
        void FocusChatField()
        {
            EventSystem.current.SetSelectedGameObject(chatField.gameObject);
            chatField.ActivateInputField();
        }
        void BuildLobbyChat()
        {
            var panel = Panel("LOBBY CHAT", 80, 776, 1740, 211);
            var content = Scroll(panel, 24, 52, 903, 86, false, 86);
            lobbyChatScroll = content.parent.GetComponent<ScrollRect>();
            lobbyChatLog = Hud.Node("Chat history", content).gameObject.AddComponent<Text>();
            lobbyChatLog.rectTransform.Fill(10, 4, 10, 4);
            lobbyChatLog.font = HudFonts.Body; lobbyChatLog.fontSize = 17;
            lobbyChatLog.color = OzUi.Ink; lobbyChatLog.supportRichText = false; lobbyChatLog.raycastTarget = false;
            lobbyChatLog.verticalOverflow = VerticalWrapMode.Overflow;
            RefreshChatHistory();
            chatField = Input(panel, "", 24, 151, 746, chat, value => chat = value, 240);
            chatField.onSubmit.AddListener(_ => SendChat());
            Button(panel, "SEND", 785, 151, 142, SendChat);

            var line = Hud.Img(panel, "Voice separator", new Color(.43f, .44f, .39f, .5f));
            line.rectTransform.TL(952, 20, 1, 173); line.raycastTarget = false;
            Label(panel, "MICROPHONE", 976, 20, 250, 19, OzUi.Amber);
            chatVoiceStatus = Label(panel, "", 1408, 20, 310, 16, OzUi.Ink);
            Label(panel, "INPUT LEVEL", 976, 62, 158, 15, OzUi.Muted);
            chatGain = Label(panel, "", 976, 99, 220, 15, OzUi.Muted);
            var meter = Hud.Img(panel, "ChatInputTrack", OzUi.GM950);
            meter.rectTransform.TL(1133, 66, 580, 15); meter.raycastTarget = false;
            chatInputMeter = Hud.Img(meter.transform, "ChatInputLevel", OzUi.Amber);
            chatInputMeter.rectTransform.Fill(); chatInputMeter.raycastTarget = false;
            var mode = R.Voice.Mode;
            MicrophoneModeButton("MUTE", 976, 118, DuelVoice.MicrophoneMode.Mute);
            MicrophoneModeButton("ALWAYS ON", 1107, 144, DuelVoice.MicrophoneMode.AlwaysOn);
            MicrophoneModeButton("TAP TO SPEAK  [" + R.Voice.TalkKey + "]", 1264, 202, DuelVoice.MicrophoneMode.TapToSpeak);
            Button(panel, OzForkOptions.AudioDevicesLabel, 1480, 151, 235, OpenMicrophone);
            UpdateChatMicrophone();

            void MicrophoneModeButton(string title, int x, int width, DuelVoice.MicrophoneMode next) =>
                Button(panel, title, x, 151, width, () =>
                {
                    if (mode == next) return;
                    R.Voice.SetMode(next); dirty = true;
                }, mode == next, !R.RoomVoiceMuted);
        }

        void BuildChatPanel(float x, float y, float w, float h)
        {
            var panel = Panel("TEXT CHAT", x, y, w, h);
            var mic = Button(panel, "", w - 276, 5, 65, () => { R.Voice.ToggleMicrophone(); UpdateChatMicrophone(); }, enabled: !R.RoomVoiceMuted);
            mic.name = "Toggle microphone";
            // A microphone silhouette uses UI primitives so it needs no external font or texture.
            chatMicrophoneCapsule = Hud.Img(mic, "Microphone capsule", OzUi.Muted); chatMicrophoneCapsule.rectTransform.TL(27, 8, 10, 18); chatMicrophoneCapsule.raycastTarget = false;
            var baseLine = Hud.Img(mic, "Microphone base", OzUi.Ink); baseLine.rectTransform.TL(21, 33, 22, 3); baseLine.raycastTarget = false;
            var stem = Hud.Img(mic, "Microphone stem", OzUi.Ink); stem.rectTransform.TL(30, 26, 4, 9); stem.raycastTarget = false;
            chatMicrophoneMuted = Hud.Img(mic, "Muted", OzUi.AlertText); chatMicrophoneMuted.rectTransform.TL(15, 22, 35, 3); chatMicrophoneMuted.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); chatMicrophoneMuted.raycastTarget = false;
            chatVoiceStatus = Label(panel, "", w - 460, 10, 174, 12, OzUi.Muted);
            var meter = Hud.Img(panel, "ChatInputTrack", OzUi.GM950);
            meter.rectTransform.TL(w - 460, 31, 165, 6);
            chatInputMeter = Hud.Img(meter.transform, "ChatInputLevel", OzUi.Amber);
            chatInputMeter.rectTransform.Fill();
            Button(panel, OzForkOptions.AudioDevicesLabel, w - 201, 5, 186, OpenMicrophone);
            chatHistoryLines = h > 200 ? 5 : 2;
            float historyY = 48;
            if (R.CanChooseChatAudience)
            {
                Button(panel, "TO TEAM", 20, 48, 130, () => { R.ChatToAll = false; dirty = true; }, !R.ChatToAll, height: 30);
                Button(panel, "TO ALL", 158, 48, 130, () => { R.ChatToAll = true; dirty = true; }, R.ChatToAll, height: 30);
                historyY = 86;
                chatHistoryLines = Math.Max(1, chatHistoryLines - 1);
            }
            chatHistory = Label(panel, "", 20, historyY, w - 40, 17, OzUi.Ink);
            RefreshChatHistory();
            chatField = Input(panel, "", 20, h - 62, w - 210, chat, v => chat = v, 240);
            chatField.onSubmit.AddListener(_ => SendChat());
            Button(panel, "SEND", w - 175, h - 62, 150, SendChat);
        }
        void UpdateChatMicrophone()
        {
            var voice = R.Voice;
            bool enabled = !R.RoomVoiceMuted && voice.Mode != DuelVoice.MicrophoneMode.Mute;
            if (chatMicrophoneCapsule) chatMicrophoneCapsule.color = voice.Transmitting ? OzUi.Go : enabled ? (Color)OzUi.Amber : OzUi.Muted;
            if (chatMicrophoneMuted) chatMicrophoneMuted.enabled = !enabled;
            chatInputMeter.rectTransform.anchorMax = new Vector2(voice.InputLevel, 1);
            chatInputMeter.color = voice.InputLevel > .95f ? OzUi.AlertText : voice.Transmitting ? (Color)OzUi.Go : OzUi.Amber;
            string state = R.RoomVoiceMuted ? "VOICE MUTED BY HOST" : voice.Transmitting ? "TRANSMITTING" : !enabled ? "MIC MUTED" :
                !voice.MicrophoneEnabled ? "MIC UNAVAILABLE" : voice.Mode == DuelVoice.MicrophoneMode.TapToSpeak ? "TAP " + voice.TalkKey : "MIC ON";
            int gain = Mathf.RoundToInt(voice.Gain * 100);
            chatVoiceStatus.Set(chatGain != null ? state : state + " · " + gain + "%");
            if (chatGain != null)
            {
                chatGain.Set("GAIN " + gain + "%");
                chatVoiceStatus.graphic.color = voice.Transmitting ? (Color)OzUi.Go : enabled ? (Color)OzUi.Amber : OzUi.Muted;
            }
        }

        internal void RefreshChatHistory()
        {
            chatHistory?.Set(string.Join("\n", R.Chat.Skip(Math.Max(0, R.Chat.Count - chatHistoryLines))));
            if (!lobbyChatLog || !lobbyChatScroll) return;
            var content = lobbyChatScroll.content;
            float viewport = lobbyChatScroll.viewport.rect.height;
            float oldExtent = Math.Max(0, content.rect.height - viewport);
            float position = lobbyChatScroll.verticalNormalizedPosition;
            bool follow = oldExtent <= 0 || position < .05f;
            lobbyChatLog.text = string.Join("\n", OnlineRoomPage ? R.Chat
                .Where(line => onlineShowSettings || !line.StartsWith(DuelRuntime.RoomSettingsChatPrefix, StringComparison.Ordinal))
                .Select(line => line.StartsWith(DuelRuntime.RoomSettingsChatPrefix, StringComparison.Ordinal)
                    ? "<color=#aab9a3>◆ " + line.Substring(DuelRuntime.RoomSettingsChatPrefix.Length) + "</color>" : line) : R.Chat);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(viewport, lobbyChatLog.preferredHeight + 8));
            lobbyChatScroll.verticalNormalizedPosition = follow ? 0 : position;
        }

        internal bool MergeVisibleSettings(List<string> history, string next)
        {
            const string prefix = DuelRuntime.RoomSettingsChatPrefix;
            if (!OnlineRoomPage || !onlineShowSettings || !lobbyChatLog || !lobbyChatScroll ||
                !next.StartsWith(prefix, StringComparison.Ordinal)) return false;
            int index = history.FindLastIndex(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (index < 0) return false;
            var dimensions = lobbyChatLog.rectTransform.rect.size;
            var generator = new TextGenerator();
            var settings = lobbyChatLog.GetGenerationSettings(dimensions);
            float before = index == 0 ? 0 : generator.GetPreferredHeight(string.Join("\n", history.Take(index)), settings) / lobbyChatLog.pixelsPerUnit;
            float end = generator.GetPreferredHeight(string.Join("\n", history.Take(index + 1)), settings) / lobbyChatLog.pixelsPerUnit;
            float offset = lobbyChatScroll.content.anchoredPosition.y;
            if (end + 4 <= offset || before + 4 >= offset + lobbyChatScroll.viewport.rect.height) return false;
            string[] separator = { " | " };
            var changes = history[index].Substring(prefix.Length).Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Concat(next.Substring(prefix.Length).Split(separator, StringSplitOptions.RemoveEmptyEntries))
                .GroupBy(line => line.Split(new[] { " → " }, 2, StringSplitOptions.None)[0]).Select(group => group.Last());
            history[index] = prefix + string.Join(" | ", changes);
            return true;
        }

        void SendChat()
        {
            R.SendChat(chat);
            chat = "";
            chatField.SetTextWithoutNotify(chat);
            if (page == Page.Chat) FocusChatField();
        }
        void Leave() { R.Close(); CloseOverlay(); if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadSceneAsync("MainMenu"); }
        public void LeaveMatch() { Leave(); }
        RectTransform Panel(string title, float x, float y, float w, float h)
        {
            var panel = Hud.Node(title, root); panel.TL(x, y, w, h); Hud.Plate(panel, "lg", 0, LobbyStyle ? (Color)OzUi.AmberLo : OzForkOptions.Accent);
            Label(panel, title, 24, 20, w - 48, 20, title.Contains("⚠") ? OzUi.AlertText : LobbyStyle ? (Color)OzUi.Amber : OzForkOptions.Accent); return panel;
        }
        bool LobbyStyle => page is Page.Lobby or Page.Skirmish || page == Page.Microphone && microphoneReturn == Page.Lobby ||
            R.Planning || ZGame.I && ZGame.I.SkirmishPlanningSeconds > 0;
        static OzText Label(Transform parent, string text, float x, float y, float width, float size, Color color) => Hud.Label(parent, text, text, size, color).At(x, y, OzH.L, false, width);
        RectTransform Button(Transform parent, string text, float x, float y, float w, Action action, bool primary = false, bool enabled = true, float height = 46, bool centered = false, bool alignLeft = false)
        {
            var b = HudButton.Make(parent, text, text, new Vector2(w, height), HudButtonKind.Large, w < 120 ? 12 : 17, .08f, primary ? HudButtonStyle.Primary : HudButtonStyle.Normal);
            if (!primary) b.LabelColor = LobbyStyle ? (Color)OzUi.Ink : OzForkOptions.Accent;
            b.Rt.TL(x, y, w, height); b.Interactable = enabled; b.Refresh();
            if (b.Label?.graphic is TMPro.TMP_Text fitted) { fitted.enableAutoSizing = true; fitted.fontSizeMin = 10; fitted.fontSizeMax = w < 120 ? 12 : 17; }
            if (text == "AI MAPS")
            {
                b.Label.FillParent(OzH.C, 30, 0, 0, 0);
                OzAiMapBadge.Head(b.Content, new OzRect(w * 0.5f - 57, (height - 24) * 0.5f, 24, 24));
            }
            if (height > 70 && b.Label != null && !centered) b.Label.rectTransform.TL(3, height - 28, w - 6, 26);
            if (alignLeft) b.Label.FillParent(OzH.L, 16, 0, 114, 0);
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
        internal static void Portrait(Transform parent, string type, float x, float y, float size, Team team = Team.Red)
        {
            if (string.IsNullOrEmpty(type)) return;
            var texture = OzUeArt.Picture(type, team);
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
    internal sealed class DuelLobbySlider : Slider
    {
        internal Action<float> Commit;
        bool dragging;

        public override void OnPointerDown(PointerEventData eventData)
        {
            dragging = IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;
            base.OnPointerDown(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (!dragging || eventData.button != PointerEventData.InputButton.Left) return;
            dragging = false;
            Commit?.Invoke(value);
        }

        public override void OnMove(AxisEventData eventData)
        {
            float before = value;
            base.OnMove(eventData);
            if (!dragging && value != before) Commit?.Invoke(value);
        }
    }

}
