using System.Linq;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SetupPanel = OpenZ.Multiplayer.DuelRuntime.SetupPanel;
using SetupView = OpenZ.Multiplayer.DuelRuntime.RoomSetupView;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        const string FollowHostPreference = "oz_duel_follow_host";
        bool followHost, followPaused;
        float onlineProfileScroll = 1;
        float onlinePresetScroll = 1;
        float onlineRosterPosition = 1;
        string[] setupProfileNames, setupPresetNames, setupPresetFormations;
        float nextSetupSampleAt;
        int setupClick, observedSetupClick;
        SetupView displayedSetup;
        InputField retainedLobbyChatInput;
        RectTransform setupMarker;
        RawImage setupPulse;
        float setupPulseUntil;
        Vector2 setupMarkerPosition;
        static Texture2D setupRing;

        bool FollowingHost => followHost && !followPaused && R.HostedRoom == null && !R.EndLobbyOpen;

        void ResetSetupFollow()
        {
            followHost = PlayerPrefs.GetInt(FollowHostPreference, 1) != 0;
            followPaused = false;
            onlineProfileScroll = onlinePresetScroll = onlineRosterPosition = 1;
            nextSetupSampleAt = 0;
            displayedSetup = null;
            observedSetupClick = setupClick = 0;
            setupPulseUntil = 0;
        }

        void PauseHostSetup(bool refresh = true)
        {
            if (OnlineRoomPage && FollowingHost)
            {
                followPaused = true;
                dirty |= refresh;
            }
        }

        void DrawFollowHost()
        {
            string state = followPaused ? "PAUSED" : followHost ? "ON" : "OFF";
            Button(root, "FOLLOW HOST · " + state, 1617, 41, 243, () =>
            {
                if (followPaused) followPaused = false;
                else followHost = !followHost;
                displayedSetup = null;
                PlayerPrefs.SetInt(FollowHostPreference, followHost ? 1 : 0);
                PlayerPrefs.Save();
                dirty = true;
            }, FollowingHost, height: 46);
        }

        void SelectOnlineSetupTab(int tab)
        {
            PauseHostSetup();
            roomTab = tab;
            combatDropdown = roomCustomUnits = roomPresets = false;
            OpenOnlinePopup(OnlinePopup.None);
        }

        void FollowSetupNavigation()
        {
            if (!OnlineRoomPage || !FollowingHost) return;
            var view = R.HostSetup;
            if (view == null)
            {
                displayedSetup = null;
                return;
            }
            if (view.SameNavigation(displayedSetup)) return;
            roomTab = view.Tab;
            onlinePlayerSeat = view.Seat;
            onlinePopup = view.Panel switch
            {
                SetupPanel.Maps => OnlinePopup.Maps,
                SetupPanel.ActivePlayers => OnlinePopup.ActivePlayers,
                SetupPanel.Units => OnlinePopup.Units,
                SetupPanel.AiHelp => OnlinePopup.AiHelp,
                _ => OnlinePopup.None
            };
            roomCustomUnits = view.Panel == SetupPanel.AllowedUnits;
            roomPresets = view.Panel == SetupPanel.Presets;
            combatDropdown = view.Panel == SetupPanel.CombatProfiles;
            if (combatDropdown) onlineProfileScroll = view.Scroll / SetupView.ScrollScale;
            if (roomPresets) onlinePresetScroll = view.Scroll / SetupView.ScrollScale;
            if (view.Panel == SetupPanel.Position)
            {
                onlinePlayerMenu = "seat:" + view.PositionSeat;
                onlineBotModern = view.BotGeneration == 0 ? null : view.BotGeneration == 2;
                onlineRosterPosition = view.Scroll / SetupView.ScrollScale;
            }
            else if (IsPositionSetupMenu(out _)) CloseOnlinePlayerMenu();
            if (roomCustomUnits) roomRosterScroll = view.Scroll / SetupView.ScrollScale;
            onlineUnitFort = view.Fort;
            onlineUnitSlot = view.Slot;
            onlineUnitSelected = view.Unit;
            if (view.Panel is SetupPanel.Maps or SetupPanel.ActivePlayers)
            {
                onlineMapPage = view.MapPage;
                onlineMapSize = view.MapSize;
                onlineOfficial = view.Official;
                onlineWorld = view.World ?? "";
                onlineMapSearch = view.Search ?? "";
                onlineProposedMap = view.Map;
            }
            if (view.Panel == SetupPanel.AiHelp)
            {
                onlineAiIdle = view.AiIdle;
                onlineAiCapture = view.AiCapture;
            }
            if (displayedSetup == null) observedSetupClick = view.Click;
            displayedSetup = view;
            dirty = true;
        }

        void UpdateHostSetup()
        {
            if (!OnlineRoomPage || !root || R.EndLobbyOpen) return;
            if (R.HostedRoom != null)
            {
                ShareHostSetup();
                return;
            }
            if (!setupMarker) return;
            var view = FollowingHost ? R.HostSetup : null;
            if (view?.Pointer != true)
            {
                setupMarker.gameObject.SetActive(false);
                if (view != null) observedSetupClick = view.Click;
                return;
            }
            var target = new Vector2(view.X, -view.Y);
            if (!setupMarker.gameObject.activeSelf)
            {
                setupMarkerPosition = target;
                setupMarker.gameObject.SetActive(true);
            }
            setupMarkerPosition = Vector2.Lerp(setupMarkerPosition, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 30f));
            setupMarker.anchoredPosition = setupMarkerPosition;
            if (observedSetupClick != view.Click)
            {
                observedSetupClick = view.Click;
                setupPulseUntil = Time.unscaledTime + .28f;
            }
            float pulse = Mathf.Clamp01((setupPulseUntil - Time.unscaledTime) / .28f);
            setupPulse.color = new Color(.42f, .92f, .97f, pulse * .8f);
            setupPulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.95f, 1f, pulse);
        }

        void ShareHostSetup()
        {
            bool positionMenu = IsPositionSetupMenu(out int setupSeat);
            var panel = onlinePopup switch
            {
                OnlinePopup.Maps => SetupPanel.Maps,
                OnlinePopup.ActivePlayers => SetupPanel.ActivePlayers,
                OnlinePopup.Units => SetupPanel.Units,
                OnlinePopup.AiHelp => SetupPanel.AiHelp,
                _ => roomCustomUnits ? SetupPanel.AllowedUnits : roomPresets ? SetupPanel.Presets :
                    combatDropdown ? SetupPanel.CombatProfiles :
                    positionMenu ? SetupPanel.Position : SetupPanel.Rules
            };
            var mouse = Mouse.current;
            Vector2 point = default;
            bool privatePanel = onlinePlayerMenu != null && !positionMenu || roomModeration != null || OzConfirm.Blocks ||
                onlinePopup == OnlinePopup.Invite || onlinePopup == OnlinePopup.Connection || onlinePopup == OnlinePopup.Versions;
            bool pointer = Application.isFocused && !privatePanel && mouse != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, mouse.position.ReadValue(), null, out point);
            point -= new Vector2(root.rect.xMin, root.rect.yMax);
            point.y = -point.y;
            bool tabs = point.y >= 125 && point.y < 172 && point.x >= 60 && point.x <= 1860;
            bool setupArea = point.y >= 185 && point.y < 766 && point.x >= 60 &&
                point.x <= (panel == SetupPanel.Rules || panel == SetupPanel.CombatProfiles ? 1152 : 1860);
            pointer &= tabs || setupArea;
            if (pointer && mouse.leftButton.wasPressedThisFrame) setupClick = setupClick == int.MaxValue ? 1 : setupClick + 1;
            if (Time.unscaledTime < nextSetupSampleAt) return;
            nextSetupSampleAt = Time.unscaledTime + .045f;
            var view = new SetupView
            {
                Tab = roomTab, Panel = panel, Seat = roomTab == 3 ? onlinePlayerSeat : 0, Pointer = pointer,
                X = pointer ? Mathf.Clamp(Mathf.RoundToInt(point.x), 0, 1920) : 0,
                Y = pointer ? Mathf.Clamp(Mathf.RoundToInt(point.y), 0, 765) : 0,
                Click = setupClick
            };
            if (panel is SetupPanel.Maps or SetupPanel.ActivePlayers)
            {
                view.MapPage = onlineMapPage;
                view.MapSize = onlineMapSize;
                view.Official = onlineOfficial;
                view.World = onlineWorld;
                view.Search = onlineRenderedSearch;
                view.Map = onlineProposedMap;
                if (panel == SetupPanel.ActivePlayers)
                {
                    var members = R.HostedRoom.Members;
                    for (int i = 0; i < members.Length && i < 32; i++)
                        if (onlineActives.Contains(members[i].Id)) view.ActivePlayers |= 1 << i;
                }
            }
            else if (panel == SetupPanel.Units)
            {
                view.Fort = onlineUnitFort;
                view.Slot = onlineUnitSlot;
                view.Unit = onlineUnitSelected;
            }
            else if (panel == SetupPanel.AllowedUnits) view.Scroll = QuantizeSetupScroll(roomRosterScroll);
            else if (panel == SetupPanel.AiHelp)
            {
                view.AiIdle = onlineAiIdle;
                view.AiCapture = onlineAiCapture;
            }
            else if (panel == SetupPanel.CombatProfiles)
                ShareSetupChoices(view, setupProfileNames, null, onlineProfileScroll, 42, 222, 10);
            else if (panel == SetupPanel.Presets)
                ShareSetupChoices(view, setupPresetNames, setupPresetFormations, onlinePresetScroll, 64, 490, 0);
            else if (panel == SetupPanel.Position)
            {
                view.PositionSeat = setupSeat;
                view.BotGeneration = onlineBotModern == null ? 0 : onlineBotModern.Value ? 2 : 1;
                view.Scroll = QuantizeSetupScroll(onlineRosterPosition);
            }
            R.PublishRoomSetup(view);
        }

        static string SetupChoiceLabel(string value, int length) =>
            new string((value ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(length).ToArray());

        static int QuantizeSetupScroll(float scroll) => Mathf.RoundToInt(Mathf.Clamp01(scroll) * SetupView.ScrollScale);

        static void ShareSetupChoices(SetupView view, string[] choices, string[] formations, float scroll, int row, int viewport, int padding)
        {
            view.Scroll = QuantizeSetupScroll(scroll);
            if (choices == null) return;
            view.ChoiceCount = choices.Length;
            float offset = Mathf.Max(0, choices.Length * row + padding - viewport) * (1 - scroll);
            view.ChoiceStart = Mathf.Max(0, Mathf.FloorToInt(offset / row) - 1);
            view.Choices = choices.Skip(view.ChoiceStart).Take(12).ToArray();
            view.Formations = formations?.Skip(view.ChoiceStart).Take(12).ToArray();
        }

        bool IsPositionSetupMenu(out int seat)
        {
            seat = -1;
            return onlinePlayerMenu != null && onlinePlayerMenu.StartsWith("seat:", StringComparison.Ordinal) &&
                int.TryParse(onlinePlayerMenu.Substring(5), out seat);
        }

        void DrawSetupMarker()
        {
            if (!FollowingHost) return;
            setupMarker = Hud.Node("Host setup pointer", root);
            setupMarker.TL(0, 0, 0, 0);
            var group = setupMarker.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            if (!setupRing)
            {
                const int size = 40;
                setupRing = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Host pointer ring" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float radius = Vector2.Distance(new Vector2(x, y), new Vector2(19.5f, 19.5f));
                        float opacity = Mathf.Clamp01(1.5f - Mathf.Abs(radius - 16f));
                        pixels[y * size + x] = new Color(1, 1, 1, opacity);
                    }
                setupRing.SetPixels32(pixels);
                setupRing.Apply(false, true);
            }
            setupPulse = Ring(34, new Color(.42f, .92f, .97f, 0));
            Ring(27, new Color(.42f, .92f, .97f));
            var badge = Hud.Img(setupMarker, "Host label", new Color(.07f, .12f, .11f, .96f));
            badge.rectTransform.TL(16, -31, 57, 23);
            Hud.Label(badge.transform, "Host", "HOST", 13, new Color(.54f, .95f, .98f)).FillParent(OzH.C);
            setupMarker.gameObject.SetActive(false);

            RawImage Ring(float size, Color color)
            {
                var ring = Hud.Node("Ring", setupMarker).gameObject.AddComponent<RawImage>();
                ring.texture = setupRing;
                ring.color = color;
                ring.raycastTarget = false;
                ring.rectTransform.TL(-size / 2, -size / 2, size, size);
                ring.rectTransform.pivot = new Vector2(.5f, .5f);
                ring.rectTransform.anchoredPosition = Vector2.zero;
                return ring;
            }
        }

        void FitSetupDialog(RectTransform shade, RectTransform panel, float width, float height)
        {
            shade.TL(0, 117, 1920, 649);
            float scale = Mathf.Min(1, 603 / height);
            panel.TL((1920 - width * scale) / 2, 151, width, height);
            panel.localScale = Vector3.one * scale;
        }
    }
}
