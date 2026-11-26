using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SetupPanel = OpenZ.Multiplayer.DuelRuntime.SetupPanel;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        enum OnlinePopup { None, Invite, Maps, ActivePlayers, Units, AiHelp, Connection, Versions }
        OnlinePopup onlinePopup;
        int onlineMapPage, onlineMapSize, onlinePlayerSeat, onlineUnitSlot, onlineAiIdle, onlineAiCapture;
        bool onlineVoiceModes;
        bool onlineOfficial, onlineShowSettings = true, onlineInviteShown;
        string onlineWorld = "", onlineMapSearch = "", onlineProposedMap;
        string onlineRenderedSearch = "";
        Action<string> onlineUnitChoice;
        bool onlineUnitFort;
        string onlineUnitSelected;
        int onlineDragSeat = -1;
        int undoTeamRevision = -1;
        int[] undoTeams;
        int onlineRoleRevision;
        int onlineSelectedSeat = -1, onlineExtraTeam = -1;
        string onlinePlayerMenu;
        bool? onlineBotModern;
        RectTransform onlinePlayerAnchor;
        OnlinePerson onlineMenuPerson;
        bool onlineEditingName, onlineModerationNoteOpen;
        readonly HashSet<string> onlineActives = new HashSet<string>();
        readonly Dictionary<string, OzText> onlineVoiceLabels = new Dictionary<string, OzText>();
        readonly Dictionary<int, OzText> onlinePingLabels = new Dictionary<int, OzText>();
        readonly Dictionary<int, OzText> onlineDetailPingLabels = new Dictionary<int, OzText>();
        readonly Dictionary<int, OzText> onlineVariationLabels = new Dictionary<int, OzText>();

        bool OnlineEditing => !R.EndLobbyOpen && R.HostedRoom != null && !R.RoomHostReady;
        bool OnlineRoomPage => page == Page.Lobby && R.SkirmishRoom;
        static readonly int[] onlineMapSizes = { 2, 3, 4, 6, 8 };

        internal void ResetInvitation() => onlineInviteShown = false;

        void ResetOnlineLobby()
        {
            ResetSetupFollow();
            selectedFort = draggedFort = -1;
            onlineVoiceModes = false;
            onlineSelectedSeat = onlineExtraTeam = undoTeamRevision = -1;
            undoTeams = null;
            onlinePopup = OnlinePopup.None;
            onlineUnitChoice = null;
            onlineChatPosition = 0;
            CloseOnlinePlayerMenu();
            onlinePlayerSeat = 0;
        }

        void BuildOnlineRoom()
        {
            var hosted = R.HostedRoom;
            var joined = R.JoinedRoom;
            var map = OzAiMapCatalog.FindSkirmishMap(hosted?.Map ?? joined?.Map);
            var rules = hosted?.Rules ?? joined.Rules;
            bool editable = OnlineEditing;
            if (hosted == null && joined == null) { BuildLobby(); return; }
            if (hosted != null && !onlineInviteShown && !R.EndLobbyOpen)
            {
                onlineInviteShown = true;
                onlinePopup = OnlinePopup.Invite;
            }

            var people = hosted != null
                ? hosted.Members.Select(member => new OnlinePerson(member.Id, member.Name, member.Seat.Position, member.Alliance,
                    member.Ready, member.Id == hosted.HostId, member.Id == hosted.HostId, member.Seat.Role == MatchSeatRole.Observer)).ToArray()
                : joined.Humans.Select(member => new OnlinePerson(member.Id, member.Name, member.Position, member.Alliance,
                    member.Ready, member.Host, member.Local, member.Observer)).ToArray();
            var bots = hosted?.Bots ?? joined.Bots.Select(bot => new RoomBot(bot.Position, bot.Alliance, bot.Difficulty)).ToArray();
            var closed = hosted?.ClosedPositions ?? joined.Closed;
            var local = people.First(person => person.Local);
            string[] tabs = { "MAP", "STARTING ARMIES", "BALANCING OPTIONS", "PLAYER ADVANTAGE", "BATTLE RULES" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button(root, tabs[i], 60 + i * 221, 125, 212, () => SelectOnlineSetupTab(tab), i == roomTab);
            }
            if (hosted != null && !R.InMatch)
                Button(root, "INVITE PLAYERS", 1560, 41, 300, () => OpenOnlinePopup(OnlinePopup.Invite), true);
            else
            {
                Label(root, "Joined as " + local.Name, 1380, 52, 225, 18, OzUi.Muted);
                if (!R.EndLobbyOpen) DrawFollowHost();
                if (!FollowingHost && !R.EndLobbyOpen)
                    Label(root, "HOST SETTINGS · READ ONLY", 1180, 138, 340, 16, OzUi.Muted);
            }
            Button(root, "FREE FOR ALL", 1530, 125, 179, () => SetMode(true), rules.FreeForAll, editable, showSelection: true);
            bool teamsSupported = MultiplayerRoom.CanFormTeams(map.Starts - closed.Length);
            Button(root, "TEAMS", 1718, 125, 142, () => SetMode(false), !rules.FreeForAll, editable && teamsSupported, showSelection: true);
            if (!teamsSupported)
                Label(root, "Teams requires at least three positions.", 1136, 137, 380, 15, OzUi.Muted);

            void SetMode(bool freeForAll)
            {
                if (rules.FreeForAll == freeForAll) return;
                rules.FreeForAll = freeForAll;
                CloseOnlinePlayerMenu();
                R.SetRoomRules(rules);
            }
            if (roomTab == 0) DrawOnlineSelectedMap(map, rules, hosted?.Capacity ?? joined.Capacity, editable);
            else if (roomTab == 1) DrawOnlineStartingArmies(rules, editable);
            else if (roomTab == 2) DrawRoomBalance(rules, editable, R.SetRoomRules);
            else if (roomTab == 3) DrawOnlineAdvantage(map.Starts, people, bots, rules, editable);
            else BuildRoomRules(map.Starts, editable, rules, R.SetRoomRules);

            onlinePlayerAnchor = null; onlineMenuPerson = null;
            DrawOnlineRoster(map.Starts, people, bots, closed, rules.FreeForAll, editable);
            DrawOnlineChat();
            Button(root, "LEAVE LOBBY", 60, 998, 216, Leave);
            int active = people.Count(person => person.Seat >= 0);
            int ready = people.Count(person => person.Ready && person.Seat >= 0);
            int open = map.Starts - active - bots.Length - closed.Length;
            string status = R.EndLobbyOpen ? "Waiting for players to return from the match" : local.Observer ? "Ghost · watching this match" :
                (hosted?.StartBlocker ?? joined.StartBlocker) == MatchStartBlocker.NoOpposingTeam ? "Assign at least two opposing teams." :
                people.Any(person => person.Seat < 0 && !person.Observer) ? "Players waiting for a starting position" :
                open > 0 ? $"Fill or close {open} open {(open == 1 ? "position" : "positions")}" :
                ready < active ? $"Waiting for {people.First(person => person.Seat >= 0 && !person.Ready).Name} · {ready} / {active} ready" :
                "All players ready";
            Label(root, status, 291, 1012, 810, 18, OzUi.Amber);
            Button(root, R.TransportLabel + " · CONNECTION", 1130, 998, 285, () => OpenOnlinePopup(OnlinePopup.Connection));
            if (!local.Observer)
                Button(root, local.Ready && !R.EndLobbyOpen ? "CANCEL READY" : "READY", hosted != null ? 1430 : 1636, 998, 212,
                    () => R.SetRoomReady(!local.Ready), !local.Ready, R.Connected && local.Seat >= 0 && !R.EndLobbyOpen);
            if (hosted != null)
                Button(root, "LAUNCH MATCH", 1654, 998, 206, R.LaunchRoom, true, R.RoomCanLaunch);

            if (roomCustomUnits) BuildRoomCustomUnits(rules, R.SetRoomRules, editable);
            if (roomPresets) BuildRoomArmyPresets(rules, R.SetRoomRules, editable);
            DrawOnlinePlayerMenu();
            switch (onlinePopup)
            {
                case OnlinePopup.Invite: DrawOnlineInvitation(); break;
                case OnlinePopup.Maps: DrawOnlineGallery(); break;
                case OnlinePopup.ActivePlayers: DrawOnlineActivePlayers(editable); break;
                case OnlinePopup.Units: DrawOnlineUnitPicker(rules, editable); break;
                case OnlinePopup.AiHelp: DrawOnlineAiHelp(people, rules, editable); break;
                case OnlinePopup.Connection: DrawOnlineConnection(people); break;
                case OnlinePopup.Versions: DrawRoomVersions(people); break;
            }
            DrawSetupMarker();
            TickOnlinePings();
        }

        void DrawVersionWarning(Transform panel, OnlinePerson[] people)
        {
            if (people.Length >= 2 && people.Any(person => VersionStatus(person.Id) != "Match"))
                Button(panel, "CHECK VERSIONS / CLICK TO VIEW", 22, 517, 245,
                    () => OpenOnlinePopup(OnlinePopup.Versions), true, height: 34);
        }

        string VersionStatus(string id)
        {
            if (!Version.TryParse(R.RoomPresence(R.RoomPresenceId(id))?.Version, out var known)) return "Unknown";
            int comparison = known.CompareTo(Version.Parse(OzForkVersion.Number));
            return comparison < 0 ? "Older" : comparison > 0 ? "Newer" : "Match";
        }

        void DrawRoomVersions(OnlinePerson[] people)
        {
            var panel = OnlineDialog("GAME VERSIONS", 530, 230, 860, 580);
            Label(panel, "Starting with different or unknown versions is strongly discouraged.", 24, 76, 800, 19, OzUi.Amber);
            Label(panel, "PLAYER", 25, 125, 360, 16, OzUi.Muted);
            Label(panel, "VERSION", 435, 125, 160, 16, OzUi.Muted);
            Label(panel, "RELATIVE TO YOUR GAME", 605, 125, 239, 16, OzUi.Muted);
            for (int i = 0; i < people.Length; i++)
            {
                var person = people[i];
                int y = 162 + i * 41;
                Label(panel, person.Name + (person.Host ? " / HOST" : ""), 25, y, 380, 19, OzUi.Ink);
                Label(panel, R.RoomPresence(R.RoomPresenceId(person.Id))?.Version ?? "Unknown", 435, y, 160, 19, OzUi.Ink);
                string status = VersionStatus(person.Id);
                Label(panel, status, 605, y, 165, 18, status == "Match" ? OzUi.Go : OzUi.Amber);
            }
        }

        sealed class OnlinePerson
        {
            internal readonly string Id, Name;
            internal readonly int Seat, Team;
            internal readonly bool Ready, Host, Local, Observer;
            internal OnlinePerson(string id, string name, int seat, int team, bool ready, bool host, bool local, bool observer)
            {
                Id = id; Name = name; Seat = seat; Team = team;
                Ready = ready; Host = host; Local = local; Observer = observer;
            }
        }

        void OpenOnlinePopup(OnlinePopup next)
        {
            PauseHostSetup();
            onlinePopup = next;
            if (next != OnlinePopup.Units) onlineUnitChoice = null;
            dirty = true;
        }

        RectTransform OnlineDialog(string title, float x, float y, float w, float h, string close = "CLOSE", Action closeAction = null,
            bool setup = false)
        {
            var shade = Hud.Img(root, "Dialog backdrop", new Color(0, 0, 0, .80f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var panel = Panel(title, x, y, w, h);
            if (setup) FitSetupDialog(shade.rectTransform, panel, w, h);
            Button(panel, close, w - 238, 10, 214, closeAction ?? (() => OpenOnlinePopup(OnlinePopup.None)), height: 36);
            return panel;
        }

        void DrawOnlineSelectedMap(OzAiMap map, SkirmishRules rules, int capacity, bool editable)
        {
            var panel = Panel(map.Name.ToUpperInvariant(), 60, 185, 1092, 568);
            if (!map.IsOfficial) OzAiMapBadge.Head(panel, new OzRect(24, 52, 22, 22));
            Label(panel, (map.IsOfficial ? "OpenZ Maps" : "AI Maps") + $" · {capacity} players",
                map.IsOfficial ? 24 : 53, 54, 660, 17, OzUi.Muted);
            if (!R.EndLobbyOpen)
                Button(panel, editable ? "CHANGE MAP" : "VIEW MAPS", 817, 16, 250, () =>
                {
                    onlineMapPage = 0;
                    onlineMapSize = OnlineGalleryMaps(capacity, map.IsOfficial).Any(entry => entry.Id == map.Id) ? capacity : 6;
                    onlineOfficial = map.IsOfficial;
                    onlineMapSearch = onlineWorld = "";
                    onlineProposedMap = map.Id;
                    OpenOnlinePopup(OnlinePopup.Maps);
                }, editable, height: 39);
            if (roomTechMap != map.Id) UpdateRoomMapData(map.Id);
            var mini = SkirmishMinimap(map);
            if (mini) DrawRoomMap(panel, mini, 24, 83, 592, 452);
            BuildRoomMapTech(panel, 635, 82, roomMapTech, rules, editable, R.SetRoomRules);
            BuildRoomOpeningOrders(panel, 635, 369, 431, rules, editable, R.SetRoomRules);
        }

        void DrawOnlineRoster(int starts, OnlinePerson[] people, RoomBot[] bots, int[] closed, bool freeForAll, bool editable)
        {
            var panel = Panel(freeForAll ? "PLAYERS" : "PLAYERS & TEAMS", 1170, 185, 690, 568);
            DrawVersionWarning(panel, people);
            var playerRules = R.RoomRules;
            var active = people.Where(person => person.Seat >= 0).ToArray();
            var observers = people.Where(person => person.Observer).ToArray();
            var waiting = people.Where(person => person.Seat < 0 && !person.Observer).ToArray();
            var humansBySeat = active.ToDictionary(person => person.Seat);
            var botsBySeat = bots.ToDictionary(bot => bot.Position);
            var positions = Enumerable.Range(0, starts).ToArray();
            var occupied = positions.Where(seat => humansBySeat.ContainsKey(seat) || botsBySeat.ContainsKey(seat)).ToArray();
            var empty = positions.Except(occupied).ToArray();
            var teams = freeForAll ? Array.Empty<int>() :
                occupied.Select(seat => humansBySeat.TryGetValue(seat, out var person) ? person.Team : botsBySeat[seat].Alliance)
                    .Concat(editable && onlineExtraTeam >= 0 && onlineExtraTeam < starts ? new[] { onlineExtraTeam } : Array.Empty<int>()).Distinct().OrderBy(team => team).ToArray();
            var teamMembers = teams.Select(team => occupied.Where(seat =>
                humansBySeat.TryGetValue(seat, out var person) ? person.Team == team : botsBySeat[seat].Alliance == team).ToArray()).ToArray();
            const int rosterWidth = 646, teamGap = 8, regularRow = 40, botRow = 80;
            int RowHeight(int seat) => !freeForAll && botsBySeat.ContainsKey(seat) ? botRow : regularRow;
            int TeamHeight(int index) => 36 + Math.Max(regularRow, teamMembers[index].Sum(RowHeight));
            int teamHeight = Enumerable.Range(0, teams.Length).Sum(index => TeamHeight(index) + teamGap);
            float height = teamHeight + regularRow * (empty.Length + observers.Length + waiting.Length + (freeForAll ? occupied.Length : 0)) +
                29 * ((empty.Length > 0 ? 1 : 0) + (observers.Length > 0 ? 1 : 0) + (waiting.Length > 0 ? 1 : 0));
            var content = Scroll(panel, 22, 58, rosterWidth, 445, false, Mathf.Max(445, height));
            var rosterScroll = content.parent.GetComponent<ScrollRect>();
            skirmishTeamViewport = rosterScroll;
            rosterScroll.verticalNormalizedPosition = onlineRosterPosition;
            rosterScroll.onValueChanged.AddListener(value => onlineRosterPosition = value.y);
            float y = 0;
            if (freeForAll)
                foreach (int seat in occupied) SeatRow(seat);
            else
                for (int index = 0; index < teams.Length; index++) DrawTeam(index);
            if (empty.Length > 0)
            {
                Group("AVAILABLE POSITIONS", OzUi.Muted);
                foreach (int seat in empty) SeatRow(seat);
            }
            if (observers.Length > 0)
            {
                Group("GHOSTS", OzUi.Muted, ghost: true);
                foreach (var observer in observers) Unseated(observer);
            }
            if (waiting.Length > 0)
            {
                Group("WAITING FOR A POSITION", OzUi.Muted);
                foreach (var person in waiting) Unseated(person);
            }
            if (editable)
                fortDragFeedback = Label(panel, freeForAll ? "Drag a fort number to move or swap." :
                    "Drag a row into a team. Drag a fort number to move or swap.", 22, 41, 646, 14, OzUi.Muted);
            if (!freeForAll && editable)
                Button(panel, "+ NEW TEAM", 475, 517, 190, () =>
                {
                    onlineExtraTeam = Enumerable.Range(0, starts).First(team => !teams.Contains(team));
                    dirty = true;
                }, enabled: teams.Length < starts, height: 34);

            if (!freeForAll && editable && undoTeams != null && undoTeamRevision == R.HostedRoom.Revision)
                Button(panel, "UNDO TEAM MOVE", 278, 517, 185, () =>
                {
                    var previous = undoTeams;
                    undoTeams = null;
                    R.ApplyRoomTeams(undoTeamRevision, previous, R.RoomRules.AllForOne);
                }, height: 34);

            void Move(int seat, int team)
            {
                if (!editable || seat < 0 || !occupied.Contains(seat)) return;
                var assignments = positions.Select(position => humansBySeat.TryGetValue(position, out var person) ? person.Team :
                    botsBySeat.TryGetValue(position, out var bot) ? bot.Alliance : position).ToArray();
                if (assignments[seat] == team) return;
                undoTeams = (int[])assignments.Clone();
                assignments[seat] = team;
                onlineSelectedSeat = -1;
                if (onlineExtraTeam == team) onlineExtraTeam = -1;
                R.ApplyRoomTeams(R.HostedRoom.Revision, assignments, R.RoomRules.AllForOne);
                undoTeamRevision = R.HostedRoom.Revision;
            }

            void DrawTeam(int index)
            {
                int team = teams[index];
                float bottom = y + TeamHeight(index);
                var background = Hud.Img(content, "Team " + (team + 1), OzUi.GM900);
                background.rectTransform.TL(0, y, rosterWidth, TeamHeight(index));
                background.raycastTarget = editable;
                var tint = OzUi.TeamText((Team)(team + 1));
                var heading = Hud.Img(content, "Team heading", OzUi.GM800);
                heading.rectTransform.TL(5, y + 4, rosterWidth - 10, 27);
                heading.raycastTarget = false;
                var stripe = Hud.Img(heading.transform, "Team color", tint);
                stripe.rectTransform.TL(5, 4, 4, 19);
                stripe.raycastTarget = false;
                int count = teamMembers[index].Length;
                var header = Label(heading.transform, $"TEAM {team + 1}   ·   {count} {(count == 1 ? "PLAYER" : "PLAYERS")}",
                    17, 4, rosterWidth - 30, 16, OzUi.Ink);
                header.graphic.raycastTarget = false;
                if (editable)
                {
                    AddOnlineEvent(background.rectTransform, EventTriggerType.PointerClick, _ => Move(onlineSelectedSeat, team));
                    AddTeamDrop(background, team);
                }
                y += 34;
                if (count == 0)
                {
                    var hint = Label(content, "Drop a player here", 16, y + 8, rosterWidth - 30, 16, OzUi.Muted);
                    hint.graphic.raycastTarget = false;
                }
                foreach (int seat in teamMembers[index]) SeatRow(seat, 5, rosterWidth - 10);
                y = bottom + teamGap;
            }

            void AddTeamDrop(Image surface, int team)
            {
                Color original = surface.color;
                AddOnlineEvent(surface.rectTransform, EventTriggerType.Drop, _ => Move(onlineDragSeat, team));
                AddOnlineEvent(surface.rectTransform, EventTriggerType.PointerEnter, _ =>
                {
                    if (onlineDragSeat >= 0) surface.color = OzUi.AmberLo;
                });
                AddOnlineEvent(surface.rectTransform, EventTriggerType.PointerExit, _ => surface.color = original);
            }

            void Group(string title, Color tint, bool ghost = false)
            {
                if (ghost) DrawGhostIcon(content, 7, y + 3, 18);
                Label(content, title, ghost ? 32 : 7, y + 4, 598, 15, tint);
                y += 29;
            }

            void SeatRow(int seat, float x = 0, int width = 646)
            {
                int rowHeight = RowHeight(seat);
                var row = Hud.Img(content, "Position " + (seat + 1), OzUi.GM900);
                row.rectTransform.TL(x, y, width, rowHeight - 4);
                row.raycastTarget = false;
                bool canDrag = editable && !freeForAll && occupied.Contains(seat);
                if (canDrag)
                    Button(row.transform, "", 0, 0, width, () => { onlineSelectedSeat = seat; dirty = true; }, height: rowHeight - 4);
                else if (occupied.Contains(seat))
                    Hud.Chip(row.transform, "Player card", new OzRect(0, 0, width, rowHeight - 4), "", OzUi.GM900, OzUi.Ink, outline: OzUi.GM500);
                if (canDrag)
                {
                    int team = humansBySeat.TryGetValue(seat, out var occupant) ? occupant.Team : botsBySeat[seat].Alliance;
                    var handle = Hud.Node("Team drag grip", row.transform);
                    handle.TL(35, 2, 30, 29);
                    DrawGrip(handle);
                    if (onlineSelectedSeat == seat)
                    {
                        var selected = Hud.Img(row.transform, "Selected player", OzUi.Amber);
                        selected.rectTransform.TL(3, 4, 2, rowHeight - 12);
                        selected.raycastTarget = false;
                    }
                    AddOnlineEvent(row.rectTransform, EventTriggerType.BeginDrag, evt =>
                    {
                        if (((PointerEventData)evt).button != PointerEventData.InputButton.Left) return;
                        onlineDragSeat = seat;
                        BeginSkirmishDrag(occupant?.Name ?? "AI", row.rectTransform);
                    });
                    AddOnlineEvent(row.rectTransform, EventTriggerType.Drag, evt => UpdateSkirmishDrag((PointerEventData)evt));
                    AddOnlineEvent(row.rectTransform, EventTriggerType.EndDrag, _ => { onlineDragSeat = -1; ClearSkirmishDrag(); });
                    AddTeamDrop(row, team);
                }
                var badge = Hud.Img(row.transform, "Fort", OzUi.TeamText((Team)(seat + 1)));
                badge.rectTransform.TL(6, 4, 26, 25);
                badge.raycastTarget = false;
                Hud.Label(badge.transform, "Fort", (seat + 1).ToString(), 14, OzUi.GM950).FillParent(OzH.C).graphic.raycastTarget = false;
                if (editable && !closed.Contains(seat)) AddFortHandle(badge, seat, occupied.Contains(seat), R.SwapRoomForts);
                if (humansBySeat.TryGetValue(seat, out var human))
                {
                    Label(row.transform, human.Name + (human.Local ? " / YOU" : human.Host ? " / HOST" : ""), 72, 10, 198, 16, OzUi.Ink);
                    bool waitingForResults = R.WaitingForEndLobby(human.Local, seat);
                    bool ready = !R.EndLobbyOpen && human.Ready;
                    Label(row.transform, waitingForResults ? "WAITING" : ready ? "READY" : "NOT READY", 315, 10, 91, 13, ready ? OzUi.Go : OzUi.Amber);
                    if (!string.IsNullOrEmpty(human.Id))
                        onlineVoiceLabels[human.Id] = Label(row.transform, "", 408, 10, 68, 12, OzUi.Ink);
                    if (!human.Host)
                        onlinePingLabels[seat] = Label(row.transform, "", 479, 10, width - 533, 13, OzUi.Muted);
                    if (!R.EndLobbyOpen && (human.Local || R.IsHost && !human.Host)) AddPlayerMenu(row.transform, 2, human.Local ? "self" : human.Id, human, width);
                }
                else if (botsBySeat.TryGetValue(seat, out var bot))
                {
                    OzAiMapBadge.Head(row.transform, new OzRect(72, 5, 21, 21));
                    Label(row.transform, freeForAll ? "AI · " + BotDifficultyName(bot.Difficulty) : "AI Bot " + (seat + 1),
                        99, 9, 171, 16, OzUi.Ink);
                    if (!freeForAll)
                    {
                        bool modern = ZAiDifficulty.IsModern(bot.Difficulty);
                        BuildLobbyAiGeneration(row.transform, new OzRect(width - 169, 5, 107, 26), modern,
                            value => R.SetRoomBot(seat, ZAiDifficulty.InGeneration(bot.Difficulty, value)), editable);
                        var divider = Hud.Img(row.transform, "Difficulty divider", OzUi.GM500);
                        divider.rectTransform.TL(8, 36, width - 16, 1);
                        divider.raycastTarget = false;
                        BuildLobbyAiDifficultyRow(row.transform, new OzRect(8, 41, width - 16, 29), modern, bot.Difficulty,
                            level => R.SetRoomBot(seat, level), null, enabled: editable);
                    }
                    if (!R.EndLobbyOpen && seat < (R.HostedRoom?.Capacity ?? R.JoinedRoom.Capacity))
                        AddPlayerMenu(row.transform, 2, "seat:" + seat, width: width);
                }
                else
                {
                    bool isClosed = closed.Contains(seat);
                    Label(row.transform, isClosed ? "Closed" : "Open", 43, 8, 350, 17, OzUi.Muted);
                    if (!R.EndLobbyOpen && seat < (R.HostedRoom?.Capacity ?? R.JoinedRoom.Capacity))
                        AddPlayerMenu(row.transform, 2, "seat:" + seat, width: width);
                }
                if (occupied.Contains(seat) && HasPlayerAdvantage(playerRules, seat)) DrawAdvantageIndicator(row.transform, seat);
                y += rowHeight;
            }

            void DrawAdvantageIndicator(Transform row, int seat)
            {
                var indicator = Button(row, "", 277, 5, 28, () =>
                {
                    SelectOnlineSetupTab(3);
                    onlinePlayerSeat = seat;
                }, height: 26);
                indicator.name = "Custom player advantage";
                Hud.Icon(indicator, "Custom player advantage", "wrench", 5, 4, 18, OzUi.Amber).raycastTarget = false;
            }

            void Unseated(OnlinePerson person)
            {
                if (person.Observer) DrawGhostIcon(content, 8, y + 6, 19);
                Label(content, person.Name + (person.Local ? " · YOU" : ""), person.Observer ? 36 : 9, y + 7, 500, 17, OzUi.Ink);
                if (!R.EndLobbyOpen && (person.Local || R.IsHost)) AddPlayerMenu(content, y, person.Local ? "self" : person.Id, person);
                y += regularRow;
            }
        }

        static bool HasPlayerAdvantage(SkirmishRules rules, int seat) => rules.BuildPercent[seat] != 100 ||
            rules.AiIdleSeconds[seat] != 0 || rules.AiFactoryCapture[seat] != 0 ||
            rules.Extras.Skip(seat * 4).Take(4).Any(unit => !string.IsNullOrEmpty(unit));

        static string BotDifficultyName(ZBotDifficulty difficulty) => difficulty == ZBotDifficulty.Normal ? "Medium" : difficulty.ToString();
        static readonly ZBotDifficulty[] LobbyBotDifficulties = { ZBotDifficulty.Easy, ZBotDifficulty.Normal, ZBotDifficulty.Hard };

        void BuildLobbyAiGeneration(Transform parent, OzRect rect, bool modern, Action<bool> select, bool enabled = true,
            bool allowReselect = false)
        {
            var toggle = new OzAiGenerationToggle(parent, rect, 13, OzUi.Amber);
            toggle.Refresh(modern);
            Bind(toggle.Classic, false);
            Bind(toggle.Modern, true);

            void Bind(Image image, bool value)
            {
                image.raycastTarget = true;
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.interactable = enabled;
                button.onClick.AddListener(() => { if (modern != value || allowReselect) select(value); });
            }
        }

        void BuildLobbyAiDifficultyRow(Transform parent, OzRect rect, bool modern, ZBotDifficulty? selected,
            Action<ZBotDifficulty> select, OzText tip, bool enabled = true, string namePrefix = "")
        {
            var levels = modern ? ZAiDifficulty.Modern : LobbyBotDifficulties;
            const float gap = 4;
            float cell = (rect.w - gap * (levels.Length - 1)) / levels.Length;
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                string label = BotDifficultyName(level).ToUpperInvariant();
                var button = Button(parent, label, rect.x + i * (cell + gap), rect.y, cell, () => select(level),
                    level == selected, enabled, height: rect.h, showSelection: true);
                button.name = namePrefix + label;
                if (tip != null) AddLobbyTip(button, tip, ZAiDifficulty.Hint(level));
            }
        }

        void AddPlayerMenu(Transform parent, float y, string key, OnlinePerson person = null, int width = 646)
        {
            var anchor = Button(parent, "...", width - 52, y, 47, () =>
            {
                PauseHostSetup();
                bool opened = onlinePlayerMenu == key;
                CloseOnlinePlayerMenu();
                if (!opened) onlinePlayerMenu = key;
            }, onlinePlayerMenu == key, height: 29);
            if (onlinePlayerMenu != key) return;
            onlinePlayerAnchor = anchor;
            onlineMenuPerson = person;
        }

        void CloseOnlinePlayerMenu()
        {
            onlinePlayerMenu = null;
            onlineBotModern = null;
            onlineEditingName = onlineModerationNoteOpen = false;
            roomModerationNote = "";
            dirty = true;
        }

        void DrawOnlinePlayerMenu()
        {
            if (onlinePlayerMenu == null) return;
            if (!onlinePlayerAnchor) { CloseOnlinePlayerMenu(); return; }
            var anchor = onlinePlayerAnchor;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var bottom = root.InverseTransformPoint(corners[3]);
            var top = root.InverseTransformPoint(corners[2]);
            const int width = 380;
            float x = Mathf.Clamp(bottom.x - root.rect.xMin - width, 24, root.rect.width - width - 24);
            float below = root.rect.yMax - bottom.y + 4;
            float above = root.rect.yMax - top.y - 4;
            var shield = Hud.Img(root, "Dismiss player menu", Color.clear);
            shield.rectTransform.Fill();
            bool positionMenu = IsPositionSetupMenu(out int seat);
            if (positionMenu) shield.rectTransform.TL(0, 117, 1920, 649);
            shield.raycastTarget = true;
            shield.gameObject.AddComponent<Button>().onClick.AddListener(() => { PauseHostSetup(); CloseOnlinePlayerMenu(); });
            var list = Hud.Node("Player menu", root);
            var background = Hud.Img(list, "Menu background", OzUi.GM950);
            background.rectTransform.Fill(); background.raycastTarget = true;
            Hud.Plate(list, "md", 0, OzUi.AmberLo);
            int y = 10;
            var person = onlineMenuPerson;
            string heading = person != null ? person.Name : positionMenu ? "FORT " + (seat + 1) : "PLAYER";
            Label(list, heading, 16, y + 2, width - 32, 21, OzUi.Amber);
            if (person?.Seat >= 0)
                Label(list, "FORT " + (person.Seat + 1), 16, y + 28, width - 32, 14, OzUi.Muted);
            y += person?.Seat >= 0 ? 56 : 39;
            if (onlinePlayerMenu == "self")
            {
                if (onlineEditingName)
                {
                    Label(list, "DISPLAY NAME", 8, y + 2, width - 16, 16, OzUi.Muted);
                    y += 27;
                    var field = Input(list, "", 8, y, width - 16, roomNameDraft, value => roomNameDraft = value, 32);
                    field.onSubmit.AddListener(_ => Rename());
                    y += 49;
                    ActionButton("SAVE NAME", Rename);
                }
                else
                {
                    ActionButton("CHANGE NAME", () => { roomNameDraft = person.Name; onlineEditingName = true; dirty = true; }, close: false);
                    if (OnlineEditing && person.Seat >= 0)
                        ActionButton("CHOOSE STARTING FORT", () => selectedFort = person.Seat);
                }
            }
            else if (person != null && R.IsHost)
            {
                string id = person.Id;
                ActionButton(R.RoomParticipantMuted(id) ? "RESTORE ROOM VOICE" : "MUTE VOICE FOR THE ROOM", () => R.ModerateRoomPlayer(id, null, null));
                if (person.Seat >= 0) ActionButton("MOVE TO GHOSTS", () => R.SetRoomObserver(id), OnlineEditing);
                ActionButton(onlineModerationNoteOpen ? "HIDE MESSAGE FIELD" : "ADD OPTIONAL MESSAGE",
                    () => { if (!onlineModerationNoteOpen) roomModerationNote = ""; onlineModerationNoteOpen = !onlineModerationNoteOpen; dirty = true; }, close: false);
                if (onlineModerationNoteOpen)
                {
                    Input(list, "", 8, y, width - 16, roomModerationNote, value => roomModerationNote = value, 160);
                    y += 49;
                }
                Label(list, "MODERATION", 16, y + 2, width - 32, 15, OzUi.AlertText);
                y += 26;
                ActionButton("KICK PLAYER...", () => Confirm(false), close: false);
                ActionButton("KICK AND BAN...", () => Confirm(true), close: false);
            }
            else if (positionMenu)
            {
                bool editable = OnlineEditing;
                ZBotDifficulty? difficulty = R.HostedRoom != null
                    ? R.HostedRoom.Bots.Where(entry => entry.Position == seat).Select(entry => (ZBotDifficulty?)entry.Difficulty).FirstOrDefault()
                    : R.JoinedRoom.Bots.Where(entry => entry.Position == seat).Select(entry => (ZBotDifficulty?)entry.Difficulty).FirstOrDefault();
                bool bot = difficulty.HasValue;
                bool closed = (R.HostedRoom?.ClosedPositions ?? R.JoinedRoom.Closed).Contains(seat);
                bool modern = (editable || FollowingHost ? onlineBotModern : null) ?? (bot && ZAiDifficulty.IsModern(difficulty.Value));
                Label(list, "BOT DIFFICULTY", 8, y + 6, width - 160, 16, OzUi.Muted);
                BuildLobbyAiGeneration(list, new OzRect(width - 144, y, 136, 28), modern,
                    value =>
                    {
                        if (difficulty.HasValue)
                        {
                            onlineBotModern = null;
                            R.SetRoomBot(seat, ZAiDifficulty.InGeneration(difficulty.Value, value));
                        }
                        else onlineBotModern = value;
                        dirty = true;
                    }, enabled: editable);
                y += 36;
                var tip = Label(list, "", 8, y + 39, width - 16, 12, OzUi.Muted);
                BuildLobbyAiDifficultyRow(list, new OzRect(8, y, width - 16, 36), modern, difficulty,
                    level => { CloseOnlinePlayerMenu(); R.SetRoomBot(seat, level); }, tip, enabled: editable);
                y += 58;
                ActionButton(bot ? "REMOVE BOT" : "OPEN POSITION", () => { if (bot) R.SetRoomBot(seat, null); else R.SetRoomClosed(seat, false); }, editable && (bot || closed));
                ActionButton("CLOSE POSITION", () => R.SetRoomClosed(seat, true), editable && !closed);
                if (editable)
                    foreach (var member in R.HostedRoom.Members.Where(member => member.Seat.Position < 0))
                        ActionButton("PLACE " + member.Name, () => R.BringRoomPlayer(member.Id, seat));
            }
            int height = y + 4;
            float bottomLimit = positionMenu ? 758 : root.rect.height - 20;
            list.TL(x, below + height <= bottomLimit ? below : Math.Max(117, above - height), width, height);

            void ActionButton(string label, Action action, bool enabled = true, bool close = true, bool selected = false)
            {
                Button(list, label, 8, y, width - 16, () => { if (close) CloseOnlinePlayerMenu(); action(); }, selected, enabled, 36, alignLeft: true, rightPadding: 16);
                y += 40;
            }

            void Rename()
            {
                if (!R.RenameRoomPlayer(roomNameDraft)) { R.ReportError("Enter a name with 1 to 32 characters."); return; }
                name = DuelLobby.NormalizeName(roomNameDraft);
                PlayerPrefs.SetString("oz_duel_name", name); PlayerPrefs.Save();
                CloseOnlinePlayerMenu();
            }

            void Confirm(bool ban)
            {
                string message = onlineModerationNoteOpen ? roomModerationNote : "";
                string id = person.Id;
                CloseOnlinePlayerMenu();
                OzConfirm.Ask(ban ? "BAN THIS PLAYER?" : "KICK THIS PLAYER?", person.Name, () => R.ModerateRoomPlayer(id, ban, message), null);
            }
        }

        void DrawOnlineInvitation()
        {
            var panel = OnlineDialog("INVITE PLAYERS", 490, 284, 940, 464, "BACK TO LOBBY");
            Label(panel, "Share the invitation link with the people you want to play with.", 32, 81, 876, 21, OzUi.Ink);
            Label(panel, "Add bots to fill open positions. Form teams for co-op or play free for all.", 32, 127, 876, 18, OzUi.Muted);
            bool hasLink = !R.OnlineMode || !string.IsNullOrEmpty(R.OnlineRoom);
            string Link() => R.Invitation(R.OnlineMode ? "" : address, R.OnlineMode ? 0 : R.TcpPort);
            if (hasLink) Input(panel, "INVITATION LINK", 32, 210, 876, Link(), _ => { }, 1000).readOnly = true;
            else Label(panel, R.ConnectionStatus, 32, 210, 876, 19, OzUi.Muted);
            Button(panel, "COPY INVITATION LINK", 32, 305, 876, () =>
            {
                GUIUtility.systemCopyBuffer = Link();
                invitationNotice = "Invitation link copied.";
                dirty = true;
            }, true, hasLink, 55);
            Label(panel, invitationNotice ?? "", 32, 390, 876, 18, OzUi.Go);
        }

        OzAiMap[] OnlineGalleryMaps(int size, bool official)
        {
            var maps = DuelRuntime.RoomMaps.Where(map => map.IsOfficial == official).ToArray();
            return maps.Where(map => OzAiMapCatalog.LobbyPlayerCount(map, maps) == size).ToArray();
        }

        void DrawOnlineGallery(bool inMatch = false)
        {
            bool canApply = inMatch || OnlineEditing;
            onlineRenderedSearch = new string(onlineMapSearch.Where(c => !char.IsControl(c)).ToArray());
            var panel = inMatch
                ? OnlineDialog("SELECT MAP", 160, 108, 1600, 916, "BACK TO ASK TO CHANGE MAP",
                    () => { choosingRequestMap = false; dirty = true; })
                : OnlineDialog(canApply ? "CHOOSE A MAP" : "BROWSE MAPS", 160, 82, 1600, 916, "BACK TO LOBBY", setup: true);
            Button(panel, "OPENZ MAPS", 24, 67, 250, () => SwitchCatalog(true), onlineOfficial,
                !inMatch || Catalog(2, true).Length > 0);
            Button(panel, "AI MAPS", 284, 67, 250, () => SwitchCatalog(false), !onlineOfficial);
            if (onlineOfficial) Label(panel, "2 PLAYERS", 555, 82, 340, 17, OzUi.Muted);
            else for (int i = 0; i < onlineMapSizes.Length; i++)
            {
                int size = onlineMapSizes[i];
                Button(panel, size.ToString(), 548 + i * 58, 67, 53, () =>
                { PauseHostSetup(); onlineMapSize = size; onlineMapPage = 0; dirty = true; }, onlineMapSize == size,
                    Catalog(size, false).Length > 0);
            }
            string[] worlds = { "", "desert", "volcanic", "arctic", "jungle", "city" };
            for (int i = 0; i < worlds.Length; i++)
            {
                string world = worlds[i];
                Button(panel, world.Length == 0 ? "ALL WORLDS" : world.ToUpperInvariant(), 24 + i * 258, 122, 250,
                    () => { PauseHostSetup(); onlineWorld = world; onlineMapPage = 0; dirty = true; }, onlineWorld == world, height: 40);
            }
            var search = Input(panel, "", 1075, 67, 333, onlineMapSearch, value => { PauseHostSetup(false); onlineMapSearch = value; }, 70);
            search.onSubmit.AddListener(_ => { onlineMapPage = 0; dirty = true; });
            Button(panel, "SEARCH", 1418, 67, 158, () => { PauseHostSetup(); onlineMapPage = 0; dirty = true; });
            var maps = Catalog(onlineMapSize, onlineOfficial).Where(map =>
                (onlineWorld.Length == 0 || map.World == onlineWorld) &&
                (string.IsNullOrWhiteSpace(onlineMapSearch) || map.Name.IndexOf(onlineMapSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
                    map.Id.IndexOf(onlineMapSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            const int perPage = 6;
            int last = Math.Max(0, (maps.Length - 1) / perPage);
            onlineMapPage = Mathf.Clamp(onlineMapPage, 0, last);
            var visible = maps.Skip(onlineMapPage * perPage).Take(perPage).ToArray();
            for (int i = 0; i < visible.Length; i++)
            {
                var map = visible[i];
                bool current = inMatch && map.Id == R.Game.level;
                var card = Button(panel, current ? map.Name + " (CURRENT MAP)" : map.Name, 24 + i % 3 * 520, 173 + i / 3 * 316, 510,
                    () => { PauseHostSetup(); onlineProposedMap = map.Id; dirty = true; }, map.Id == onlineProposedMap, !current, height: 304);
                var texture = SkirmishMinimap(map);
                if (texture)
                {
                    var image = Hud.Node("Complete minimap", card).gameObject.AddComponent<RawImage>();
                    image.texture = texture;
                    float scale = Mathf.Min(470f / texture.width, 260f / texture.height);
                    float w = texture.width * scale, h = texture.height * scale;
                    image.rectTransform.TL((510 - w) / 2, 8 + (260 - h) / 2, w, h);
                    image.raycastTarget = false;
                    if (inMatch && !map.IsOfficial) OzAiMapBadge.Stamp(image.rectTransform, w);
                }
            }
            if (maps.Length == 0) Label(panel, "No maps match these filters.", 32, 193, 1000, 23, OzUi.Ink);
            var candidate = maps.FirstOrDefault(map => map.Id == onlineProposedMap);
            var room = R.HostedRoom;
            var active = inMatch || room == null ? Array.Empty<RoomMember>() :
                room.Members.Where(member => member.Seat.Role != MatchSeatRole.Observer).ToArray();
            int activeCount = inMatch || room != null ? active.Length : R.JoinedRoom.Humans.Count(member => !member.Observer);
            bool needsSelection = !inMatch && activeCount > onlineMapSize;
            Button(panel, "<", 24, 833, 78, () => { PauseHostSetup(); onlineMapPage--; dirty = true; }, enabled: onlineMapPage > 0);
            Label(panel, $"Page {onlineMapPage + 1} / {last + 1}", 115, 850, 280, 18, OzUi.Muted);
            Button(panel, ">", 260, 833, 78, () => { PauseHostSetup(); onlineMapPage++; dirty = true; }, enabled: onlineMapPage < last);
            if (candidate != null)
                Label(panel, needsSelection ? $"Choose {onlineMapSize} active players. The others stay as ghosts." :
                    $"{candidate.Name} · {onlineMapSize} active positions", 360, 847, 850, 17, OzUi.Ink);
            Button(panel, inMatch ? "CONFIRM MAP" : needsSelection ? (canApply ? "CHOOSE ACTIVE PLAYERS" : "VIEW ACTIVE PLAYERS") : "SELECT MAP",
                1206, 829, 370, () =>
            {
                if (inMatch) { SelectMapChangeDraft(candidate.Id); return; }
                if (!canApply) { OpenOnlinePopup(OnlinePopup.ActivePlayers); return; }
                if (active.Length <= onlineMapSize)
                {
                    ApplyOnlineMap(active.Select(member => member.Id).ToArray());
                    return;
                }
                onlineActives.Clear();
                onlineActives.Add(room.HostId);
                foreach (var member in active.Where(member => member.Id != room.HostId)
                    .OrderBy(member => member.Seat.Position < 0 ? 8 : member.Seat.Position).Take(onlineMapSize - 1))
                    onlineActives.Add(member.Id);
                onlineRoleRevision = room.Revision;
                OpenOnlinePopup(OnlinePopup.ActivePlayers);
            }, true, (canApply || needsSelection) && candidate != null && (!inMatch || candidate.Id != R.Game.level), 53);

            OzAiMap[] Catalog(int size, bool official)
            {
                if (!inMatch) return OnlineGalleryMaps(size, official);
                var catalog = DuelRuntime.RoomMaps.Where(map => map.IsOfficial == official && map.Starts == size);
                if (!R.SkirmishRoom) return catalog.Where(map => DuelRules.IsMapAllowed(map.Id)).ToArray();
                int humans = R.IsHost
                    ? R.HostedRoom.Members.Count(member => member.Seat.MayUsePlayerCommands)
                    : R.JoinedRoom.Humans.Count(member => member.Position >= 0 && !member.Observer);
                return catalog.Where(map => map.Starts >= humans).ToArray();
            }

            void SwitchCatalog(bool official)
            {
                PauseHostSetup();
                onlineOfficial = official;
                onlineMapSize = official ? 2 : Catalog(onlineMapSize, false).Length > 0 ? onlineMapSize :
                    inMatch ? onlineMapSizes.FirstOrDefault(size => Catalog(size, false).Length > 0) : 3;
                onlineMapPage = 0;
                onlineWorld = "";
                dirty = true;
            }
        }

        void ApplyOnlineMap(string[] active)
        {
            var room = R.HostedRoom;
            if (room.Map != onlineProposedMap || room.Capacity != onlineMapSize ||
                !room.Members.Where(member => member.Seat.MayUsePlayerCommands).Select(member => member.Id).ToHashSet().SetEquals(active))
                R.ApplyRoomMap(onlineProposedMap, onlineMapSize, active);
            OpenOnlinePopup(OnlinePopup.None);
        }

        void DrawOnlineActivePlayers(bool editable)
        {
            var room = R.HostedRoom;
            if (editable && room.Revision != onlineRoleRevision)
            {
                onlinePopup = OnlinePopup.Maps;
                DrawOnlineGallery();
                return;
            }
            var map = OzAiMapCatalog.FindSkirmishMap(onlineProposedMap);
            if (map == null) { onlinePopup = OnlinePopup.Maps; DrawOnlineGallery(); return; }
            var followed = FollowingHost && displayedSetup?.Panel == SetupPanel.ActivePlayers ? displayedSetup : null;
            var members = room != null
                ? room.Members.Select(member => (member.Id, member.Name, member.Alliance, Host: member.Id == room.HostId,
                    Playing: editable ? onlineActives.Contains(member.Id) : member.Seat.Role != MatchSeatRole.Observer)).ToArray()
                : R.JoinedRoom.Humans.Select((member, index) => (Id: "", member.Name, member.Alliance, member.Host,
                    Playing: followed != null && index < 32 ? (followed.ActivePlayers & (1 << index)) != 0 : !member.Observer)).ToArray();
            var panel = OnlineDialog("CHOOSE WHO PLAYS", 340, 222, 1240, 630, "BACK TO GALLERY",
                () => OpenOnlinePopup(OnlinePopup.Maps), setup: true);
            Label(panel, $"{map.Name} · {onlineMapSize} active positions · {members.Length} people in the lobby", 25, 67, 1190, 21, OzUi.Ink);
            Label(panel, "Ghosts stay connected and can watch. Only active players need to ready up.", 25, 106, 1190, 18, OzUi.Muted);
            int active = members.Count(member => member.Playing);
            Column(true, 25, $"ACTIVE PLAYERS   ·   {active} / {onlineMapSize}");
            Column(false, 631, $"GHOSTS   ·   {members.Length - active} watching");
            Button(panel, "CONFIRM MAP & PLAYERS", 784, 556, 430,
                () => ApplyOnlineMap(onlineActives.ToArray()), true, editable && active == onlineMapSize, 52);

            void Column(bool playing, int x, string title)
            {
                if (!playing) DrawGhostIcon(panel, x, 152, 22);
                Label(panel, title, playing ? x : x + 30, 153, 538, 19, OzUi.Amber);
                float y = 190;
                foreach (var member in members.Where(member => member.Playing == playing))
                {
                    var row = Hud.Img(panel, member.Name, OzUi.GM900);
                    row.rectTransform.TL(x, y, 583, 39);
                    row.raycastTarget = false;
                    if (!playing) DrawGhostIcon(row.transform, 7, 9, 22);
                    Label(row.transform, member.Name, playing ? 7 : 36, 9, 207, 18, OzUi.Ink);
                    Label(row.transform, member.Host ? "HOST · PLAYS" : member.Alliance >= 0 ? "Team " + (member.Alliance + 1) : "",
                        254, 10, 124, 14, OzUi.Muted);
                    if (!member.Host)
                        Button(row.transform, playing ? "MOVE TO GHOSTS →" : "← MAKE ACTIVE", 377, 3, 201, () =>
                        {
                            if (playing) onlineActives.Remove(member.Id);
                            else onlineActives.Add(member.Id);
                            dirty = true;
                        }, enabled: editable && (playing || active < onlineMapSize), height: 33);
                    y += 43;
                }
            }
        }

        void DrawOnlineConnection(OnlinePerson[] people)
        {
            var panel = OnlineDialog("CONNECTION DETAILS", 1005, 530, 760, 462);
            string route = R.TransportLabel == "SFU" ? "Cloudflare relay" : R.TransportLabel == "DIRECT" ? "Direct connection" : R.TransportLabel;
            Label(panel, (R.Connected ? "Connected" : R.ConnectionStatus) + " · " + route, 24, 61, 713, 20, OzUi.Ink);
            Label(panel, "PLAYER", 24, 106, 340, 16, OzUi.Muted);
            Label(panel, "PING TO HOST", 374, 106, 155, 16, OzUi.Muted);
            Label(panel, "PING VARIATION", 551, 106, 181, 16, OzUi.Muted);
            int i = 0;
            foreach (var person in people.Where(person => !person.Host && person.Seat >= 0))
            {
                float y = 137 + i++ * 37;
                Label(panel, person.Name, 24, y, 340, 18, OzUi.Ink);
                onlineDetailPingLabels[person.Seat] = Label(panel, "—", 374, y, 155, 18, OzUi.Ink);
                onlineVariationLabels[person.Seat] = Label(panel, "—", 551, y, 171, 18, OzUi.Ink);
            }
            Label(panel, "Ping measures the round trip to the host, including any relay.", 24, 414, 710, 15, OzUi.Muted);
        }

        void TickOnlinePings()
        {
            foreach (var label in onlineVoiceLabels)
            {
                int? voice = R.RoomPresence(R.RoomPresenceId(label.Key))?.Voice;
                label.Value.Set(voice == 3 ? "MUTED" : voice == 2 ? "SPEAKING" : voice == 0 ? "MIC OFF" : voice == 1 ? "MIC ON" : "");
                label.Value.graphic.color = voice == 2 ? (Color)OzUi.Go : voice == 3 ? (Color)OzUi.AlertText : OzUi.Muted;
            }
            Refresh(onlinePingLabels, false, true);
            Refresh(onlineDetailPingLabels, false, false);
            Refresh(onlineVariationLabels, true, false);

            void Refresh(Dictionary<int, OzText> labels, bool variation, bool showQuality)
            {
                foreach (var label in labels)
                {
                    var reading = R.HasFreshRoomLatency ? R.RoomPings.FirstOrDefault(entry => entry.Seat == label.Key) : null;
                    string quality = reading == null ? "" : reading.Ping >= 160 || reading.Variation >= 35 ? "Poor" :
                        reading.Ping >= 95 || reading.Variation >= 15 ? "Fair" : "Stable";
                    label.Value.Set(reading?.Disconnected == true ? "Offline" :
                        reading == null || reading.Ping <= 0 ? "Measuring" :
                        variation ? $"{reading.Variation} ms" : showQuality ? $"{reading.Ping} ms / {quality}" : $"{reading.Ping} ms");
                }
            }
        }

        void DrawOnlineChat()
        {
            var panel = Panel("LOBBY CHAT", 60, 766, 1800, 228);
            var content = Scroll(panel, 24, 57, 1732, 110, false, 110);
            lobbyChatScroll = content.parent.GetComponent<ScrollRect>();
            var track = Hud.Img(panel, "Chat scrollbar", OzUi.GM850);
            track.rectTransform.TL(1760, 57, 16, 110);
            var handle = Hud.Img(track.transform, "Thumb", OzUi.Muted);
            handle.rectTransform.Fill(3, 0, 3, 0);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            lobbyChatScroll.verticalScrollbar = bar;
            lobbyChatScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            lobbyChatLog = Hud.Node("Chat history", content).gameObject.AddComponent<Text>();
            lobbyChatLog.rectTransform.Fill(10, 4, 10, 4);
            lobbyChatLog.font = HudFonts.Body;
            lobbyChatLog.fontSize = 17;
            lobbyChatLog.color = OzUi.Ink;
            lobbyChatLog.supportRichText = true;
            lobbyChatLog.raycastTarget = false;
            lobbyChatLog.verticalOverflow = VerticalWrapMode.Overflow;
            RefreshChatHistory();
            lobbyChatScroll.verticalNormalizedPosition = onlineChatPosition;
            if (retainedLobbyChatInput)
            {
                chatField = retainedLobbyChatInput;
                retainedLobbyChatInput = null;
                chatField.transform.SetParent(panel, false);
                ((RectTransform)chatField.transform).TL(24, 177, 1599, 46);
            }
            else
            {
                chatField = Input(panel, "", 24, 177, 1599, chat, value => chat = value, 240);
                chatField.onSubmit.AddListener(_ => SendChat());
            }
            Button(panel, "SEND", 1634, 177, 142, SendChat);
            Button(panel, "SETTINGS CHANGES", 760, 12, 208, () =>
            {
                onlineShowSettings = !onlineShowSettings;
                RefreshChatHistory();
                dirty = true;
            }, onlineShowSettings, height: 36);
            var voice = R.Voice;
            bool micOn = voice.Mode != DuelVoice.MicrophoneMode.Mute && !R.RoomVoiceMuted;
            Button(panel, micOn ? "MIC ON" : "MIC OFF", 980, 12, 105,
                () => { voice.ToggleMicrophone(); dirty = true; }, micOn, !R.RoomVoiceMuted, 36);
            chatVoiceStatus = Label(panel, "", 1098, 12, 198, 13, OzUi.Ink);
            chatGain = Label(panel, "", 1289, 12, 83, 12, OzUi.Muted);
            var meter = Hud.Img(panel, "Audio input level", OzUi.GM950);
            meter.rectTransform.TL(1098, 37, 267, 8);
            chatInputMeter = Hud.Img(meter.transform, "Microphone input", OzUi.Go);
            chatInputMeter.rectTransform.Fill();
            string mode = voice.PreferredMode == DuelVoice.MicrophoneMode.AlwaysOn ? "ALWAYS ON" : "TAP TO SPEAK [" + voice.TalkKey + "]";
            Button(panel, mode + "  v", 1380, 12, 205, () => { onlineVoiceModes = !onlineVoiceModes; dirty = true; },
                enabled: !R.RoomVoiceMuted, height: 36);
            Button(panel, "AUDIO SETTINGS", 1597, 12, 180, OpenMicrophone, height: 36);
            if (onlineVoiceModes && !R.RoomVoiceMuted)
            {
                var modes = Hud.Img(panel, "Voice mode choices", OzUi.GM950).rectTransform;
                modes.TL(1377, 52, 400, 115);
                Button(modes, "TAP TO SPEAK [" + voice.TalkKey + "]", 8, 8, 384,
                    () => ChooseVoiceMode(DuelVoice.MicrophoneMode.TapToSpeak), height: 44);
                Button(modes, "ALWAYS ON", 8, 61, 384, () => ChooseVoiceMode(DuelVoice.MicrophoneMode.AlwaysOn), height: 44);
            }

            void ChooseVoiceMode(DuelVoice.MicrophoneMode choice)
            {
                voice.ChooseLiveMode(choice);
                onlineVoiceModes = false;
                dirty = true;
            }
        }

        void DrawRoomBalance(SkirmishRules rules, bool editable, Action<SkirmishRules> save)
        {
            var panel = Panel("BALANCING OPTIONS", 60, 185, 1092, 568);
            var territory = Section("TERRITORY CONSTRUCTION TIME CAP", 65);
            Label(territory, "Holding flags speeds up production. The cap limits how much time flags can save.", 17, 50, 977, 17, OzUi.Ink);
            var territoryExample = Label(territory, "", 17, 149, 977, 16, OzUi.Muted);
            void PreviewTerritory(int value)
            {
                var production = rules.Production;
                production.TerritoryReductionCap = value / 100d;
                double seconds = 100 * (1 - production.TerritoryTimeReduction(1, 25));
                territoryExample.Set(rules.ProgressiveTerritory
                    ? $"Example: with a 25% army lead and all flags, a 100 s build takes {seconds:0.#} s."
                    : $"Example: with all flags, a 100 s build takes {seconds:0.#} s.");
            }
            PreviewTerritory(rules.TerritoryCap);
            LobbySlider(territory, "Territory cap", 17, 88, 1000, 10, 50, rules.TerritoryCap, "10% · Smaller bonus", "50% · Original OpenZ",
                value => value + "%", 925, editable, value => { rules.TerritoryCap = value; save(rules); }, preview: PreviewTerritory);
            Button(territory, "PROGRESSIVE CAP · " + (rules.ProgressiveTerritory ? "ON" : "OFF"), 17, 179, 270, () =>
            { rules.ProgressiveTerritory = !rules.ProgressiveTerritory; save(rules); }, rules.ProgressiveTerritory, editable, 35, showSelection: true);
            Label(territory, "Fixed: same cap for everyone. Progressive: lowers it for the army pulling ahead.", 304, 188, 725, 15, OzUi.Muted);
            var fort = Section("FORT CATCH-UP", 309);
            Label(fort, "A trailing fort can catch up using a multiple of the opponent's speed bonus from flags.", 17, 50, 977, 17, OzUi.Ink);
            var fortExample = Label(fort, "", 17, 149, 977, 16, OzUi.Muted);
            void PreviewFort(int value)
            {
                var production = rules.Production;
                production.FortSpeedup = value / 100d;
                double bonus = 100 * (production.FortCatchupSpeed(10, .2) - 1);
                fortExample.Set(value == 0 ? "Fort catch-up is off." :
                    $"Example: +25% enemy flag speed and a 10% enemy army lead give +{bonus:0.#}% target fort speed.");
            }
            PreviewFort(rules.FortSpeedup);
            LobbySlider(fort, "Fort speedup", 17, 88, 1000, 0, 500, rules.FortSpeedup, "Off", "5× · Maximum",
                value => value == 0 ? "Off" : (value / 100f).ToString("0.##") + "×", 925, editable,
                value => { rules.FortSpeedup = value; save(rules); }, preview: PreviewFort);
            Button(fort, "PROGRESSIVE BONUS · " + (rules.ProgressiveFort ? "ON" : "OFF"), 17, 179, 270, () =>
            { rules.ProgressiveFort = !rules.ProgressiveFort; save(rules); }, rules.ProgressiveFort, editable, 35, showSelection: true);
            Label(fort, "Fixed: full bonus as you fall behind. Progressive: ramps up to a 20% enemy army lead.", 304, 188, 725, 15, OzUi.Muted);

            RectTransform Section(string title, int y)
            {
                var section = Hud.Img(panel, title, OzUi.GM950).rectTransform;
                section.TL(24, y, 1044, 228);
                Label(section, title, 17, 15, 895, 20, OzUi.Amber);
                return section;
            }
        }

        void DrawOnlineAdvantage(int starts, OnlinePerson[] people, RoomBot[] bots, SkirmishRules rules, bool editable)
        {
            var panel = Panel("PLAYER ADVANTAGE", 60, 185, 1092, 568);
            var roster = people.Where(person => person.Seat >= 0).Select(person => (person.Seat, person.Name, person.Team, Bot: false))
                .Concat(bots.Select(bot => (Seat: bot.Position, Name: "AI Bot " + (bot.Position + 1), Team: bot.Alliance, Bot: true)))
                .OrderBy(member => member.Seat).ToArray();
            if (roster.Length == 0) return;
            if (!roster.Any(member => member.Seat == onlinePlayerSeat)) onlinePlayerSeat = roster[0].Seat;
            int selected = onlinePlayerSeat;
            for (int i = 0; i < roster.Length; i++)
            {
                var member = roster[i];
                bool modified = HasPlayerAdvantage(rules, member.Seat);
                bool activeCard = selected == member.Seat;
                var card = Button(panel, "", 24 + i % 4 * 263, 65 + i / 4 * 48, 251,
                    () => { PauseHostSetup(); onlinePlayerSeat = member.Seat; dirty = true; }, activeCard, height: 42);
                card.name = member.Name + " / Fort " + (member.Seat + 1);
                var badge = Hud.Img(card, "Fort", OzUi.TeamText((Team)(member.Seat + 1)));
                badge.rectTransform.TL(10, 8, 26, 26);
                badge.raycastTarget = false;
                Hud.Label(badge.transform, "Fort number", (member.Seat + 1).ToString(), 15, OzUi.GM950)
                    .FillParent(OzH.C).graphic.raycastTarget = false;
                if (member.Bot) OzAiMapBadge.Head(card, new OzRect(43, 11, 20, 20));
                int nameX = member.Bot ? 70 : 44;
                Color ink = activeCard ? OzUi.GM950 : OzUi.Ink;
                Label(card, member.Name, nameX, 12, (modified ? 219 : 241) - nameX, 17, ink).graphic.raycastTarget = false;
                if (modified)
                    Hud.Icon(card, "Custom player advantage", "wrench", 224, 12, 18, activeCard ? ink : OzUi.Amber).raycastTarget = false;
            }
            var chosen = roster.Single(member => member.Seat == selected);
            Label(panel, $"{chosen.Name} · Fort {selected + 1} · Team {chosen.Team + 1}", 24, 170, 600, 22, OzUi.Ink);
            bool hasHelp = rules.AiIdleSeconds[selected] > 0 || rules.AiFactoryCapture[selected] > 0;
            Button(panel, hasHelp ? "AI HELP · ON" : "AI HELP · OFF", 600, 156, 224, () =>
            {
                onlineAiIdle = rules.AiIdleSeconds[selected];
                onlineAiCapture = rules.AiFactoryCapture[selected];
                onlineRoleRevision = R.HostedRoom?.Revision ?? 0;
                OpenOnlinePopup(OnlinePopup.AiHelp);
            }, primary: hasHelp, height: 37);
            Button(panel, "RESET TO STANDARD", 834, 156, 232, () =>
            { rules.BuildPercent[selected] = 100; rules.AiIdleSeconds[selected] = rules.AiFactoryCapture[selected] = 0;
                Array.Clear(rules.Extras, selected * 4, 4); R.SetRoomRules(rules); }, enabled: editable, height: 37);

            var speed = Hud.Img(panel, "Construction time", OzUi.GM950).rectTransform;
            speed.TL(24, 214, 1044, 169);
            Label(speed, "CONSTRUCTION TIME", 16, 16, 500, 20, OzUi.Amber);
            Label(speed, "The selected player builds units in this percentage of the standard time.", 16, 55, 1000, 17, OzUi.Muted);
            var buildExample = Label(speed, "", 16, 148, 1000, 16, OzUi.Muted);
            void PreviewBuild(int value) => buildExample.Set($"Example: before other rules, a 100 s build takes {value} s for {chosen.Name}.");
            PreviewBuild(rules.BuildPercent[selected]);
            LobbySlider(speed, "Construction time", 17, 81, 1000, 50, 100, rules.BuildPercent[selected],
                "50% of standard", "100% · Standard", value => value + "%", 925, editable,
                value => { rules.BuildPercent[selected] = value; R.SetRoomRules(rules); }, preview: PreviewBuild);
            Label(panel, "EXTRA STARTING UNITS · " + chosen.Name, 24, 393, 1040, 19, OzUi.Amber);
            DrawOnlineUnitSlots(panel, rules.Extras, selected * 4, rules, editable, 423, R.SetRoomRules);
        }

        void DrawOnlineUnitSlots(Transform panel, string[] slots, int offset, SkirmishRules rules,
            bool editable, int y, Action<SkirmishRules> save, bool fort = false)
        {
            for (int i = 0; i < 4; i++)
            {
                int index = offset + i;
                int x = 24 + 263 * i;
                string unit = slots[index];
                Action<string> choose = value => { slots[index] = value; if (fort) rules.OverrideFortTurrets = true; save(rules); };
                var card = Button(panel, string.IsNullOrEmpty(unit) ? (fort ? "+ ADD TURRET" : "+ ADD UNIT") : UnitName(unit), x, y, 250, () =>
                {
                    onlineUnitFort = fort;
                    onlineUnitSelected = unit;
                    onlineUnitSlot = index % 4;
                    onlineUnitChoice = editable ? choose : null;
                    onlineRoleRevision = R.HostedRoom?.Revision ?? 0;
                    OpenOnlinePopup(OnlinePopup.Units);
                }, height: 111);
                if (!string.IsNullOrEmpty(unit))
                {
                    Portrait(card, unit, 88, 6, 69);
                    if (editable) Button(card, "×", 207, 4, 38, () => choose(""), height: 32);
                }
            }
        }

        void DrawOnlineUnitPicker(SkirmishRules rules, bool editable)
        {
            if (editable && (R.HostedRoom.Revision != onlineRoleRevision || onlineUnitChoice == null))
            {
                OpenOnlinePopup(OnlinePopup.None);
                return;
            }

            var choose = onlineUnitChoice;
            int revision = onlineRoleRevision;
            BuildArmyGallery(onlineUnitFort, onlineUnitSlot, onlineUnitSelected, rules.Allows, type =>
            {
                OpenOnlinePopup(OnlinePopup.None);
                if (OnlineEditing && R.HostedRoom.Revision == revision) choose(type);
            }, () => OpenOnlinePopup(OnlinePopup.None), editable);
        }

        void DrawOnlineAiHelp(OnlinePerson[] people, SkirmishRules rules, bool editable)
        {
            if (editable && R.HostedRoom.Revision != onlineRoleRevision) { onlinePopup = OnlinePopup.None; return; }
            int seat = onlinePlayerSeat;
            bool showDraft = editable || FollowingHost && displayedSetup?.Panel == SetupPanel.AiHelp;
            int idleValue = showDraft ? onlineAiIdle : rules.AiIdleSeconds[seat];
            int captureValue = showDraft ? onlineAiCapture : rules.AiFactoryCapture[seat];
            string name = people.FirstOrDefault(person => person.Seat == seat)?.Name ?? "Fort " + (seat + 1);
            var panel = OnlineDialog("AI HELP · " + name, 440, 239, 1040, 604, setup: true);
            Label(panel, "IDLE UNITS", 24, 81, 980, 20, OzUi.Amber);
            Label(panel, "The AI can order idle units. A warning appears five seconds before it acts.", 24, 116, 980, 18, OzUi.Ink);
            Label(panel, "AFTER AN ENEMY CAPTURES A FACTORY", 24, 235, 980, 20, OzUi.Amber);
            Label(panel, "Replace restarts a weak build and clears the queue. Cancel stops all production at that factory.", 24, 271, 980, 18, OzUi.Ink);
            for (int i = 0; i < 3; i++)
            {
                int idle = i * 15, capture = i;
                string[] captures = { "OFF", "REPLACE WEAK BUILD", "CANCEL PRODUCTION" };
                Button(panel, idle == 0 ? "OFF" : $"AFTER {idle} s", 24 + i * 332, 157, 318,
                    () => { onlineAiIdle = idle; dirty = true; }, idleValue == idle, editable, showSelection: true);
                Button(panel, captures[i], 24 + i * 332, 319, 318,
                    () => { onlineAiCapture = capture; dirty = true; }, captureValue == capture, editable, showSelection: true);
            }
            if (editable)
                Button(panel, "APPLY AI HELP", 685, 499, 331, () =>
                {
                    rules.AiIdleSeconds[seat] = onlineAiIdle;
                    rules.AiFactoryCapture[seat] = onlineAiCapture;
                    R.SetRoomRules(rules);
                    OpenOnlinePopup(OnlinePopup.None);
                }, true);
        }

        void DrawOnlineStartingArmies(SkirmishRules rules, bool editable)
        {
            var panel = Panel("STARTING ARMIES", 60, 185, 1092, 568);
            Label(panel, "FORT TURRETS", 25, 76, 650, 20, OzUi.Amber);
            Label(panel, rules.OverrideFortTurrets ? "Custom turrets replace the map defaults." : "Using the map's fort turrets.", 25, 111, 650, 17, OzUi.Ink);
            Button(panel, "USE MAP TURRETS", 758, 79, 307, () =>
            {
                rules.OverrideFortTurrets = false;
                Array.Clear(rules.FortTurrets, 0, rules.FortTurrets.Length);
                R.SetRoomRules(rules);
            }, enabled: editable && rules.OverrideFortTurrets);
            DrawOnlineUnitSlots(panel, rules.FortTurrets, 0, rules, editable, 151, R.SetRoomRules, fort: true);
            Label(panel, "ADDITIONAL UNITS NEAR THE FORT", 25, 307, 650, 20, OzUi.Amber);
            Label(panel, "Each participant receives these units. A robot slot adds one squad.", 25, 343, 700, 17, OzUi.Ink);
            Button(panel, editable ? "LOAD / SAVE UNIT PRESET" : "VIEW UNIT PRESETS", 758, 310, 307, () =>
                { PauseHostSetup(); roomPresets = true; onlinePresetScroll = 1; presetStatus = presetName = ""; dirty = true; });
            DrawOnlineUnitSlots(panel, rules.Common, 0, rules, editable, 386, R.SetRoomRules);
        }

        static void AddOnlineEvent(RectTransform target, EventTriggerType type, Action<BaseEventData> callback)
        {
            var trigger = target.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(evt => callback(evt));
            trigger.triggers.Add(entry);
        }
    }
}
