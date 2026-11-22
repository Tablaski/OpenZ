using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        enum OnlinePopup { None, Invite, Maps, ActivePlayers, Units, AiHelp, Connection }
        OnlinePopup onlinePopup;
        int onlineMapPage, onlineMapSize, onlinePlayerSeat, onlineUnitSlot, onlineAiIdle, onlineAiCapture;
        bool onlineOfficial, onlineShowSettings = true, onlineInviteShown;
        string onlineWorld = "", onlineMapSearch = "", onlineProposedMap;
        Action<string> onlineUnitChoice;
        string[] onlineUnitOptions;
        int[] onlineTeams;
        int onlineTeamsRevision, onlineDragSeat = -1, onlineTeamCount;
        int onlineRoleRevision;
        readonly HashSet<string> onlineActives = new HashSet<string>();
        readonly Dictionary<int, OzText> onlinePingLabels = new Dictionary<int, OzText>();
        readonly Dictionary<int, OzText> onlineVariationLabels = new Dictionary<int, OzText>();

        bool OnlineEditing => R.HostedRoom != null && !R.RoomHostReady;
        bool OnlineRoomPage => page == Page.Lobby && R.SkirmishRoom;
        static readonly int[] onlineMapSizes = { 2, 3, 4, 6, 8 };

        void ResetOnlineLobby()
        {
            onlinePopup = OnlinePopup.None;
            onlineInviteShown = false;
            onlineTeams = null;
            onlinePosition = -1;
            onlinePlayerSeat = 0;
        }

        void BuildOnlineRoom()
        {
            var hosted = R.HostedRoom;
            var joined = R.JoinedRoom;
            var map = OzAiMapCatalog.FindSkirmishMap(hosted?.Map ?? joined?.Map);
            var rules = hosted?.Rules ?? joined.Rules;
            bool editable = OnlineEditing;
            if (hosted != null && !onlineInviteShown)
            {
                onlineInviteShown = true;
                onlinePopup = OnlinePopup.Invite;
            }
            if (onlineTeams != null && (!editable || hosted.Revision != onlineTeamsRevision)) onlineTeams = null;

            var people = hosted != null
                ? hosted.Members.Select(member => new OnlinePerson(member.Id, member.Name, member.Seat.Position, member.Alliance,
                    member.Ready, member.Id == hosted.HostId, member.Id == hosted.HostId, member.Seat.Role == MatchSeatRole.Observer)).ToArray()
                : joined.Humans.Select(member => new OnlinePerson(null, member.Name, member.Position, member.Alliance,
                    member.Ready, member.Host, member.Local, member.Observer)).ToArray();
            var bots = hosted?.Bots ?? joined.Bots.Select(bot => new RoomBot(bot.Position, bot.Alliance, bot.Difficulty)).ToArray();
            var closed = hosted?.ClosedPositions ?? joined.Closed;
            var local = people.First(person => person.Local);
            string[] tabs = { "MAP", "STARTING ARMIES", "BALANCING OPTIONS", "PLAYER ADVANTAGE", "BATTLE RULES" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button(root, tabs[i], 60 + i * 221, 125, 212, () => { roomTab = tab; dirty = true; }, i == roomTab);
            }
            if (hosted != null && !R.InMatch)
                Button(root, "INVITE PLAYERS", 1560, 41, 300, () => OpenOnlinePopup(OnlinePopup.Invite), true);
            else Label(root, "Joined as " + local.Name, 1460, 52, 400, 18, OzUi.Muted);
            Button(root, rules.FreeForAll ? "FREE FOR ALL" : "TEAMS", 1530, 125, 330, () =>
            {
                rules.FreeForAll = !rules.FreeForAll;
                R.SetRoomRules(rules);
            }, enabled: editable);

            if (roomTab == 0) DrawOnlineSelectedMap(map, rules, hosted?.Capacity ?? joined.Capacity, editable);
            else if (roomTab == 1) DrawOnlineStartingArmies(rules, editable);
            else if (roomTab == 2) DrawOnlineBalance(rules, editable);
            else if (roomTab == 3) DrawOnlineAdvantage(map.Starts, people, bots, rules, editable);
            else BuildRoomRules(map.Starts, editable, rules, R.SetRoomRules);

            if (roomModeration != null && hosted != null)
                BuildRoomModeration(1170, 185, 690);
            else if (onlineTeams != null) DrawOnlineEditTeams(people, bots, hosted.Starts);
            else DrawOnlineRoster(map.Starts, people, bots, closed, rules.FreeForAll, editable);

            DrawOnlineChat();
            Button(root, "LEAVE LOBBY", 60, 1007, 216, Leave);
            int active = people.Count(person => person.Seat >= 0);
            int ready = people.Count(person => person.Ready && person.Seat >= 0);
            int open = map.Starts - active - bots.Length - closed.Length;
            string status = local.Observer ? "Ghost · watching this match" :
                people.Any(person => person.Seat < 0 && !person.Observer) ? "Players waiting for a starting position" :
                open > 0 ? $"Fill or close {open} open {(open == 1 ? "position" : "positions")}" :
                ready < active ? $"Waiting for {people.First(person => person.Seat >= 0 && !person.Ready).Name} · {ready} / {active} ready" :
                "All players ready";
            Label(root, status, 291, 1021, 810, 18, OzUi.Amber);
            Button(root, R.TransportLabel + " · CONNECTION", 1130, 1007, 285, () => OpenOnlinePopup(OnlinePopup.Connection));
            if (!local.Observer)
                Button(root, local.Ready ? "CANCEL READY" : "READY", hosted != null ? 1430 : 1636, 1007, 212,
                    () => R.SetRoomReady(!local.Ready), !local.Ready, R.Connected && local.Seat >= 0);
            if (hosted != null)
                Button(root, "LAUNCH MATCH", 1654, 1007, 206, R.LaunchRoom, true, R.RoomCanLaunch);

            if (roomCustomUnits && editable) BuildRoomCustomUnits(rules, R.SetRoomRules);
            if (roomDynamics) BuildRoomDynamics(rules, editable ? R.SetRoomRules : null);
            if (roomPresets && editable) BuildRoomArmyPresets(rules, R.SetRoomRules);
            if (roomRename) BuildRoomInfo();
            if (onlinePosition >= 0) DrawOnlinePosition();
            switch (onlinePopup)
            {
                case OnlinePopup.Invite: DrawOnlineInvitation(); break;
                case OnlinePopup.Maps: if (editable) DrawOnlineGallery(); break;
                case OnlinePopup.ActivePlayers: if (editable) DrawOnlineActivePlayers(); break;
                case OnlinePopup.Units: if (editable) DrawOnlineUnitPicker(); break;
                case OnlinePopup.AiHelp: DrawOnlineAiHelp(people, rules, editable); break;
                case OnlinePopup.Connection: DrawOnlineConnection(people); break;
            }
            TickOnlinePings();
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
            onlinePopup = next;
            dirty = true;
        }

        RectTransform OnlineDialog(string title, float x, float y, float w, float h, string close = "CLOSE", Action closeAction = null)
        {
            var shade = Hud.Img(root, "Dialog backdrop", new Color(0, 0, 0, .80f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var panel = Panel(title, x, y, w, h);
            Button(panel, close, w - 238, 10, 214, closeAction ?? (() => OpenOnlinePopup(OnlinePopup.None)), height: 36);
            return panel;
        }

        void DrawOnlineSelectedMap(OzAiMap map, SkirmishRules rules, int capacity, bool editable)
        {
            var panel = Panel(map.Name.ToUpperInvariant(), 60, 185, 1092, 568);
            if (!map.IsOfficial) OzAiMapBadge.Head(panel, new OzRect(24, 52, 22, 22));
            Label(panel, (map.IsOfficial ? "OpenZ Maps" : "AI Maps") + $" · {capacity} players",
                map.IsOfficial ? 24 : 53, 54, 660, 17, OzUi.Muted);
            if (editable)
                Button(panel, "CHANGE MAP", 817, 16, 250, () =>
                {
                    onlineMapPage = 0;
                    onlineMapSize = capacity;
                    onlineOfficial = map.IsOfficial;
                    onlineMapSearch = onlineWorld = "";
                    onlineProposedMap = map.Id;
                    OpenOnlinePopup(OnlinePopup.Maps);
                }, true, height: 39);
            if (roomTechMap != map.Id) UpdateRoomMapData(map.Id);
            var mini = SkirmishMinimap(map);
            if (mini) DrawRoomMap(panel, mini, 24, 83, 592, 452);
            BuildRoomMapTech(panel, 635, 82, roomMapTech, rules, editable, R.SetRoomRules);
            BuildRoomOpeningOrders(panel, 635, 369, 431, rules, editable, R.SetRoomRules);
        }

        void DrawOnlineRoster(int starts, OnlinePerson[] people, RoomBot[] bots, int[] closed, bool freeForAll, bool editable)
        {
            var panel = Panel(freeForAll ? "STARTING POSITIONS" : "PLAYERS & TEAMS", 1170, 185, 690, 568);
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
                    .Distinct().OrderBy(team => team).ToArray();
            float height = 36 * (starts + observers.Length + waiting.Length) + 29 * (teams.Length +
                (empty.Length > 0 ? 1 : 0) + (observers.Length > 0 ? 1 : 0) + (waiting.Length > 0 ? 1 : 0));
            var content = Scroll(panel, 22, 58, 646, 445, false, Mathf.Max(445, height));
            float y = 0;
            if (freeForAll)
                foreach (int seat in occupied) SeatRow(seat);
            else
                foreach (int team in teams)
                {
                    Group("TEAM " + (team + 1), OzUi.TeamText((Team)(team + 1)));
                    foreach (int seat in occupied.Where(seat => humansBySeat.TryGetValue(seat, out var p) ? p.Team == team : botsBySeat[seat].Alliance == team))
                        SeatRow(seat);
                }
            if (empty.Length > 0)
            {
                Group("AVAILABLE POSITIONS", OzUi.Muted);
                foreach (int seat in empty) SeatRow(seat);
            }
            if (observers.Length > 0)
            {
                Group("👻 GHOSTS", OzUi.Muted);
                foreach (var observer in observers) Unseated(observer, "👻");
            }
            if (waiting.Length > 0)
            {
                Group("WAITING FOR A POSITION", OzUi.Muted);
                foreach (var person in waiting) Unseated(person, "");
            }
            var local = people.First(person => person.Local);
            Button(panel, "CHANGE NAME", 254, 516, 195, () => { roomRename = true; roomNameDraft = local.Name; dirty = true; }, height: 35);
            if (!freeForAll && R.IsHost)
                Button(panel, "EDIT TEAMS", 459, 516, 206, () => StartOnlineTeamEdit(starts, active, bots), enabled: editable, height: 35);

            void Group(string title, Color tint)
            {
                Label(content, title, 7, y + 4, 623, 15, tint);
                y += 29;
            }

            void SeatRow(int seat)
            {
                var row = Hud.Img(content, "Position " + (seat + 1), OzUi.GM900);
                row.rectTransform.TL(0, y, 646, 33);
                row.raycastTarget = false;
                var badge = Hud.Img(row.transform, "Fort", OzUi.TeamText((Team)(seat + 1)));
                badge.rectTransform.TL(6, 4, 26, 25);
                badge.raycastTarget = false;
                Hud.Label(badge.transform, "Fort", (seat + 1).ToString(), 14, OzUi.GM950).FillParent(OzH.C);
                if (humansBySeat.TryGetValue(seat, out var human))
                {
                    Label(row.transform, human.Name + (human.Local ? " · YOU" : human.Host ? " · HOST" : ""), 43, 8, 343, 17, OzUi.Ink);
                    Label(row.transform, human.Ready ? "READY" : "NOT READY", 387, 8, 100, 14, human.Ready ? OzUi.Go : OzUi.Amber);
                    if (!human.Host)
                        onlinePingLabels[seat] = Label(row.transform, "", 494, 8, 88, 14, OzUi.Muted);
                    if (R.IsHost && !human.Host)
                        Button(row.transform, "···", 594, 2, 47, () => SelectRoomModeration(human.Id), height: 29);
                }
                else if (botsBySeat.TryGetValue(seat, out var bot))
                {
                    OzAiMapBadge.Head(row.transform, new OzRect(43, 5, 21, 21));
                    Label(row.transform, "AI  ·  " + bot.Difficulty, 70, 7, 350, 17, OzUi.Ink);
                    if (editable && seat < R.HostedRoom.Capacity)
                        Button(row.transform, "···", 594, 2, 47, () => ManageOnlinePosition(seat), height: 29);
                }
                else
                {
                    bool isClosed = closed.Contains(seat);
                    Label(row.transform, isClosed ? "Closed" : "Open", 43, 8, 350, 17, OzUi.Muted);
                    if (editable && seat < R.HostedRoom.Capacity)
                        Button(row.transform, "···", 594, 2, 47, () => ManageOnlinePosition(seat), height: 29);
                }
                y += 36;
            }

            void Unseated(OnlinePerson person, string icon)
            {
                Label(content, icon + "  " + person.Name + (person.Local ? " · YOU" : ""), 9, y + 7, 500, 17, OzUi.Ink);
                if (R.IsHost) Button(content, "···", 594, y, 47, () => SelectRoomModeration(person.Id), height: 29);
                y += 36;
            }
        }

        int onlinePosition = -1;
        void ManageOnlinePosition(int seat)
        {
            onlinePosition = seat;
            dirty = true;
        }

        void DrawOnlinePosition()
        {
            int seat = onlinePosition;
            var room = R.HostedRoom;
            if (!OnlineEditing || seat < 0 || seat >= room.Capacity || room.Members.Any(member => member.Seat.Position == seat))
            {
                onlinePosition = -1;
                return;
            }
            var panel = OnlineDialog("POSITION " + (seat + 1), 590, 260, 740, 490, closeAction: () => { onlinePosition = -1; dirty = true; });
            bool bot = room.Bots.Any(entry => entry.Position == seat);
            bool closed = room.ClosedPositions.Contains(seat);
            Action<Action> update = change => { onlinePosition = -1; change(); dirty = true; };
            Button(panel, "NORMAL AI", 25, 66, 335, () => update(() => R.SetRoomBot(seat, ZBotDifficulty.Normal)), height: 40);
            Button(panel, "EASY AI", 380, 66, 335, () => update(() => R.SetRoomBot(seat, ZBotDifficulty.Easy)), height: 40);
            Button(panel, bot ? "REMOVE BOT" : "OPEN POSITION", 25, 118, 335,
                () => update(() => { if (bot) R.SetRoomBot(seat, null); else R.SetRoomClosed(seat, false); }), enabled: bot || closed, height: 40);
            Button(panel, "CLOSE POSITION", 380, 118, 335, () => update(() => R.SetRoomClosed(seat, true)), enabled: !closed, height: 40);
            var unseated = room.Members.Where(member => member.Seat.Position < 0).ToArray();
            if (unseated.Length == 0) return;
            Label(panel, "PLACE A PLAYER HERE", 25, 179, 690, 18, OzUi.Amber);
            for (int i = 0; i < unseated.Length; i++)
            {
                var member = unseated[i];
                Button(panel, member.Name + (member.Seat.Role == MatchSeatRole.Observer ? " · GHOST" : ""), 25 + i % 2 * 355, 215 + i / 2 * 54, 335,
                    () => update(() => R.BringRoomPlayer(member.Id, seat)), height: 42);
            }
        }

        void StartOnlineTeamEdit(int starts, OnlinePerson[] active, RoomBot[] bots)
        {
            onlineTeams = Enumerable.Range(0, starts).ToArray();
            foreach (var person in active) onlineTeams[person.Seat] = person.Team;
            foreach (var bot in bots) onlineTeams[bot.Position] = bot.Alliance;
            onlineTeamCount = active.Select(person => person.Team).Concat(bots.Select(bot => bot.Alliance)).DefaultIfEmpty(0).Max() + 1;
            onlineTeamCount = Math.Max(2, onlineTeamCount);
            onlineTeamsRevision = R.HostedRoom.Revision;
            onlineDragSeat = -1;
            dirty = true;
        }

        void DrawOnlineEditTeams(OnlinePerson[] people, RoomBot[] bots, int starts)
        {
            var roster = people.Where(person => person.Seat >= 0).Select(person => (person.Seat, person.Name))
                .Concat(bots.Select(bot => (Seat: bot.Position, Name: "AI · " + bot.Difficulty))).ToArray();
            var panel = Panel("EDIT TEAMS", 1170, 185, 690, 568);
            Label(panel, "Drag a participant onto a team, or select a row and then a team.", 24, 53, 640, 16, OzUi.Muted);
            var teams = Scroll(panel, 24, 82, 640, 413, false, Mathf.Max(413, onlineTeamCount * 48 + roster.Length * 37));
            float y = 0;
            for (int team = 0; team < onlineTeamCount; team++)
            {
                int destination = team;
                var assigned = roster.Where(person => onlineTeams[person.Seat] == destination).ToArray();
                var target = Button(teams, "TEAM " + (team + 1), 0, y, 640, () => Move(destination), height: 39);
                AddOnlineEvent(target, EventTriggerType.Drop, _ => Move(destination));
                y += 43;
                foreach (var person in assigned)
                {
                    int moving = person.Seat;
                    var card = Button(teams, "⋮⋮  " + person.Name, 22, y, 596, () => { onlineDragSeat = moving; dirty = true; },
                        onlineDragSeat == moving, height: 33);
                    AddOnlineEvent(card, EventTriggerType.BeginDrag, evt =>
                    {
                        if (((PointerEventData)evt).button == PointerEventData.InputButton.Left) onlineDragSeat = moving;
                    });
                    AddOnlineEvent(card, EventTriggerType.Drag, _ => { });
                    AddOnlineEvent(card, EventTriggerType.Drop, _ => Move(destination));
                    AddOnlineEvent(card, EventTriggerType.EndDrag, _ => { onlineDragSeat = -1; dirty = true; });
                    y += 37;
                }
                y += 5;
            }
            Button(panel, "+ ADD TEAM", 24, 506, 161, () => { onlineTeamCount++; dirty = true; }, enabled: onlineTeamCount < starts, height: 43);
            Button(panel, "CANCEL", 283, 506, 145, () => { onlineTeams = null; dirty = true; }, height: 43);
            Button(panel, "APPLY TEAMS", 439, 506, 225, () =>
            {
                var next = onlineTeams;
                onlineTeams = null;
                R.ApplyRoomTeams(onlineTeamsRevision, next);
                dirty = true;
            }, true, height: 43);

            void Move(int destination)
            {
                if (onlineDragSeat < 0) return;
                onlineTeams[onlineDragSeat] = destination;
                onlineDragSeat = -1;
                dirty = true;
            }
        }


        void DrawOnlineInvitation()
        {
            var panel = OnlineDialog("INVITE PLAYERS", 490, 284, 940, 464, "BACK TO LOBBY");
            Label(panel, "Share the invitation link with the people you want to play with.", 32, 81, 876, 21, OzUi.Ink);
            Label(panel, "Add bots to fill open positions. Form teams for co-op or play free for all.", 32, 127, 876, 18, OzUi.Muted);
            bool hasLink = !string.IsNullOrEmpty(R.OnlineRoom);
            if (hasLink) Input(panel, "INVITATION LINK", 32, 210, 876, R.Invitation("", 0), _ => { }, 1000).readOnly = true;
            else Label(panel, R.ConnectionStatus, 32, 210, 876, 19, OzUi.Muted);
            Button(panel, "COPY INVITATION LINK", 32, 305, 876, () =>
            {
                GUIUtility.systemCopyBuffer = R.Invitation("", 0);
                invitationNotice = "Invitation link copied.";
                dirty = true;
            }, true, hasLink, 55);
            Label(panel, invitationNotice ?? "", 32, 390, 876, 18, OzUi.Go);
        }

        OzAiMap[] OnlineGalleryMaps(int size, bool official)
        {
            var maps = DuelRuntime.RoomMaps.Where(map => map.IsOfficial == official).ToArray();
            return maps.Where(map => map.Starts == size || !official && size == 6 && map.Starts == 8 && !maps.Any(entry => entry.Starts == 6))
                .ToArray();
        }

        void DrawOnlineGallery()
        {
            var panel = OnlineDialog("CHOOSE A MAP", 160, 82, 1600, 916, "BACK TO LOBBY");
            Button(panel, "OPENZ MAPS", 24, 67, 250, () => SwitchCatalog(true), onlineOfficial);
            Button(panel, "AI MAPS", 284, 67, 250, () => SwitchCatalog(false), !onlineOfficial);
            if (onlineOfficial) Label(panel, "2 PLAYERS", 555, 82, 340, 17, OzUi.Muted);
            else for (int i = 0; i < onlineMapSizes.Length; i++)
            {
                int size = onlineMapSizes[i];
                Button(panel, size.ToString(), 548 + i * 58, 67, 53, () =>
                { onlineMapSize = size; onlineMapPage = 0; dirty = true; }, onlineMapSize == size,
                    OnlineGalleryMaps(size, false).Length > 0);
            }
            string[] worlds = { "", "desert", "volcanic", "arctic", "jungle", "city" };
            Button(panel, onlineWorld.Length == 0 ? "ALL WORLDS ▾" : onlineWorld.ToUpperInvariant() + " ▾", 858, 67, 203,
                () => { onlineWorld = worlds[(Array.IndexOf(worlds, onlineWorld) + 1) % worlds.Length]; onlineMapPage = 0; dirty = true; });
            var search = Input(panel, "", 1075, 67, 333, onlineMapSearch, value => onlineMapSearch = value, 70);
            search.onSubmit.AddListener(_ => { onlineMapPage = 0; dirty = true; });
            Button(panel, "SEARCH", 1418, 67, 158, () => { onlineMapPage = 0; dirty = true; });
            var maps = OnlineGalleryMaps(onlineMapSize, onlineOfficial).Where(map =>
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
                var card = Button(panel, map.Name, 24 + i % 3 * 520, 125 + i / 3 * 337, 510,
                    () => { onlineProposedMap = map.Id; dirty = true; }, map.Id == onlineProposedMap, height: 324);
                var texture = SkirmishMinimap(map);
                if (texture)
                {
                    var image = Hud.Node("Complete minimap", card).gameObject.AddComponent<RawImage>();
                    image.texture = texture;
                    float scale = Mathf.Min(470f / texture.width, 280f / texture.height);
                    float w = texture.width * scale, h = texture.height * scale;
                    image.rectTransform.TL((510 - w) / 2, 8 + (280 - h) / 2, w, h);
                    image.raycastTarget = false;
                }
            }
            if (maps.Length == 0) Label(panel, "No maps match these filters.", 32, 154, 1000, 23, OzUi.Ink);
            var candidate = maps.FirstOrDefault(map => map.Id == onlineProposedMap);
            var room = R.HostedRoom;
            var active = room.Members.Where(member => member.Seat.Role != MatchSeatRole.Observer).ToArray();
            Button(panel, "◀", 24, 833, 78, () => { onlineMapPage--; dirty = true; }, enabled: onlineMapPage > 0);
            Label(panel, $"Page {onlineMapPage + 1} / {last + 1}", 115, 850, 280, 18, OzUi.Muted);
            Button(panel, "▶", 260, 833, 78, () => { onlineMapPage++; dirty = true; }, enabled: onlineMapPage < last);
            if (candidate != null)
                Label(panel, active.Length > onlineMapSize ? $"Choose {onlineMapSize} active players. The others stay as ghosts." :
                    $"{candidate.Name} · {onlineMapSize} active positions", 360, 847, 850, 17, OzUi.Ink);
            Button(panel, active.Length > onlineMapSize ? "CHOOSE ACTIVE PLAYERS" : "SELECT MAP", 1206, 829, 370, () =>
            {
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
            }, true, candidate != null, 53);

            void SwitchCatalog(bool official)
            {
                onlineOfficial = official;
                onlineMapSize = official ? 2 : OnlineGalleryMaps(onlineMapSize, false).Length > 0 ? onlineMapSize : 3;
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

        void DrawOnlineActivePlayers()
        {
            var room = R.HostedRoom;
            if (room.Revision != onlineRoleRevision)
            {
                onlinePopup = OnlinePopup.Maps;
                DrawOnlineGallery();
                return;
            }
            var map = OzAiMapCatalog.FindSkirmishMap(onlineProposedMap);
            var panel = OnlineDialog("CHOOSE WHO PLAYS", 340, 222, 1240, 630, "BACK TO GALLERY",
                () => OpenOnlinePopup(OnlinePopup.Maps));
            Label(panel, $"{map.Name} · {onlineMapSize} active positions · {room.Members.Length} people in the lobby", 25, 67, 1190, 21, OzUi.Ink);
            Label(panel, "Ghosts stay connected and can watch. Only active players need to ready up.", 25, 106, 1190, 18, OzUi.Muted);
            int active = onlineActives.Count;
            Column(true, 25, $"ACTIVE PLAYERS   ·   {active} / {onlineMapSize}");
            Column(false, 631, $"👻 GHOSTS   ·   {room.Members.Length - active} watching");
            Button(panel, "CONFIRM MAP & PLAYERS", 784, 556, 430,
                () => ApplyOnlineMap(onlineActives.ToArray()), true, active == onlineMapSize, 52);

            void Column(bool playing, int x, string title)
            {
                Label(panel, title, x, 153, 568, 19, OzUi.Amber);
                float y = 190;
                foreach (var member in room.Members.Where(member => onlineActives.Contains(member.Id) == playing))
                {
                    var row = Hud.Img(panel, member.Name, OzUi.GM900);
                    row.rectTransform.TL(x, y, 583, 39);
                    row.raycastTarget = false;
                    Label(row.transform, (!playing ? "👻  " : "") + member.Name, 7, 9, 240, 18, OzUi.Ink);
                    Label(row.transform, member.Id == room.HostId ? "HOST · PLAYS" : member.Alliance >= 0 ? "Team " + (member.Alliance + 1) : "",
                        254, 10, 124, 14, OzUi.Muted);
                    if (member.Id != room.HostId)
                        Button(row.transform, playing ? "MOVE TO GHOSTS →" : "← MAKE ACTIVE", 377, 3, 201, () =>
                        {
                            if (playing) onlineActives.Remove(member.Id);
                            else onlineActives.Add(member.Id);
                            dirty = true;
                        }, enabled: playing || onlineActives.Count < onlineMapSize, height: 33);
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
                onlinePingLabels[person.Seat] = Label(panel, "—", 374, y, 155, 18, OzUi.Ink);
                onlineVariationLabels[person.Seat] = Label(panel, "—", 551, y, 171, 18, OzUi.Ink);
            }
            Label(panel, "Ping measures the round trip to the host, including any relay.", 24, 414, 710, 15, OzUi.Muted);
        }

        void TickOnlinePings()
        {
            foreach (var label in onlinePingLabels)
            {
                var reading = R.RoomPings.FirstOrDefault(entry => entry.Seat == label.Key);
                label.Value.Set(reading == null ? "—" : $"{reading.Ping} ms");
            }
            foreach (var label in onlineVariationLabels)
            {
                var reading = R.RoomPings.FirstOrDefault(entry => entry.Seat == label.Key);
                label.Value.Set(reading == null ? "—" : $"{reading.Variation} ms");
            }
        }

        void DrawOnlineChat()
        {
            var panel = Panel("LOBBY CHAT", 60, 766, 1800, 228);
            var content = Scroll(panel, 24, 57, 1752, 110, false, 110);
            lobbyChatScroll = content.parent.GetComponent<ScrollRect>();
            lobbyChatLog = Hud.Node("Chat history", content).gameObject.AddComponent<Text>();
            lobbyChatLog.rectTransform.Fill(10, 4, 10, 4);
            lobbyChatLog.font = HudFonts.Body;
            lobbyChatLog.fontSize = 17;
            lobbyChatLog.color = OzUi.Ink;
            lobbyChatLog.supportRichText = true;
            lobbyChatLog.raycastTarget = false;
            lobbyChatLog.verticalOverflow = VerticalWrapMode.Overflow;
            RefreshChatHistory();
            chatField = Input(panel, "", 24, 177, 1599, chat, value => chat = value, 240);
            chatField.onSubmit.AddListener(_ => SendChat());
            Button(panel, "SEND", 1634, 177, 142, SendChat);
            var tip = Label(panel, "", 655, 22, 695, 16, OzUi.Muted);
            Button(panel, (onlineShowSettings ? "✓ " : "") + "SETTINGS CHANGES", 1350, 12, 240, () =>
            {
                onlineShowSettings = !onlineShowSettings;
                RefreshChatHistory();
                dirty = true;
            }, height: 36);
            var mic = Button(panel, "🎙", 1598, 12, 51, () => { R.Voice.ToggleMicrophone(); dirty = true; },
                R.Voice.MicrophoneEnabled, !R.RoomVoiceMuted, 36);
            AddLobbyTip(mic, tip, R.RoomVoiceMuted ? "Microphone muted by host" : R.Voice.MicrophoneEnabled ? "Mute microphone" : "Unmute microphone");
            var mode = Button(panel, "◖))", 1657, 12, 51, () => { R.Voice.CycleMode(); dirty = true; }, enabled: !R.RoomVoiceMuted, height: 36);
            AddLobbyTip(mode, tip, R.Voice.Mode == DuelVoice.MicrophoneMode.TapToSpeak ? "Tap " + R.Voice.TalkKey + " to speak · click to change" :
                R.Voice.Mode + " · click to change");
            var devices = Button(panel, "⚙", 1716, 12, 60, OpenMicrophone, height: 36);
            AddLobbyTip(devices, tip, "Microphone and speakers");
        }

        void DrawOnlineBalance(SkirmishRules rules, bool editable)
        {
            var panel = Panel("BALANCING OPTIONS", 60, 185, 1092, 568);
            var territory = Section("TERRITORY CONSTRUCTION TIME CAP", 65);
            Label(territory, "Holding flags speeds up production. The cap limits how much time flags can save.", 17, 50, 977, 17, OzUi.Ink);
            LobbySlider(territory, "Territory cap", 17, 88, 1000, 10, 50, rules.TerritoryCap, "10% · Smaller bonus", "50% · Original OpenZ",
                value => value + "%", 925, editable, value => { rules.TerritoryCap = value; R.SetRoomRules(rules); });
            Label(territory, $"Example: a 100 s build can take as little as {100 - rules.TerritoryCap} s at this cap.", 17, 149, 977, 16, OzUi.Muted);
            Button(territory, (rules.ProgressiveTerritory ? "✓ " : "") + "PROGRESSIVE CAP", 17, 179, 270, () =>
            { rules.ProgressiveTerritory = !rules.ProgressiveTerritory; R.SetRoomRules(rules); }, enabled: editable, height: 35);
            Label(territory, "Fixed: same cap for everyone. Progressive: lowers it for the army pulling ahead.", 304, 188, 725, 15, OzUi.Muted);
            var fort = Section("FORT CATCH-UP", 309);
            Label(fort, "A trailing fort can catch up using a multiple of the opponent's speed bonus from flags.", 17, 50, 977, 17, OzUi.Ink);
            LobbySlider(fort, "Fort speedup", 17, 88, 1000, 0, 500, rules.FortSpeedup, "Off", "5× · Maximum",
                value => value == 0 ? "Off" : (value / 100f).ToString("0.##") + "×", 925, editable,
                value => { rules.FortSpeedup = value; R.SetRoomRules(rules); });
            Label(fort, "Example: enemy flag bonus +25%, setting 2× → +50% target fort speed at full strength.", 17, 149, 977, 16, OzUi.Muted);
            Button(fort, (rules.ProgressiveFort ? "✓ " : "") + "PROGRESSIVE BONUS", 17, 179, 270, () =>
            { rules.ProgressiveFort = !rules.ProgressiveFort; R.SetRoomRules(rules); }, enabled: editable, height: 35);
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
            var roster = people.Where(person => person.Seat >= 0).Select(person => (person.Seat, person.Name, person.Team))
                .Concat(bots.Select(bot => (Seat: bot.Position, Name: "AI · " + bot.Difficulty, Team: bot.Alliance))).OrderBy(member => member.Seat).ToArray();
            if (roster.Length == 0) return;
            if (!roster.Any(member => member.Seat == onlinePlayerSeat)) onlinePlayerSeat = roster[0].Seat;
            int selected = onlinePlayerSeat;
            for (int i = 0; i < roster.Length; i++)
            {
                var member = roster[i];
                bool modified = rules.BuildPercent[member.Seat] != 100 || rules.AiIdleSeconds[member.Seat] != 0 ||
                    rules.AiFactoryCapture[member.Seat] != 0 || rules.Extras.Skip(member.Seat * 4).Take(4).Any(unit => !string.IsNullOrEmpty(unit));
                Button(panel, $"{member.Seat + 1} · {member.Name} · {(modified ? "Modified" : "Standard")}",
                    24 + i % 4 * 263, 65 + i / 4 * 48, 251,
                    () => { onlinePlayerSeat = member.Seat; dirty = true; }, selected == member.Seat, height: 42);
            }
            var chosen = roster.Single(member => member.Seat == selected);
            Label(panel, $"{chosen.Name} · Fort {selected + 1} · Team {chosen.Team + 1}", 24, 170, 600, 22, OzUi.Ink);
            bool hasHelp = rules.AiIdleSeconds[selected] > 0 || rules.AiFactoryCapture[selected] > 0;
            Button(panel, hasHelp ? "🤖 AI HELP · ON" : "🤖 AI HELP · OFF", 600, 156, 224, () =>
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
            speed.TL(24, 214, 1044, 150);
            Label(speed, "CONSTRUCTION TIME", 16, 16, 500, 20, OzUi.Amber);
            Label(speed, "The selected player builds units in this percentage of the standard time.", 16, 55, 1000, 17, OzUi.Muted);
            LobbySlider(speed, "Construction time", 17, 81, 1000, 50, 100, rules.BuildPercent[selected],
                "50% of standard", "100% · Standard", value => value + "%", 925, editable,
                value => { rules.BuildPercent[selected] = value; R.SetRoomRules(rules); });
            Label(panel, "EXTRA STARTING UNITS · " + chosen.Name, 24, 393, 1040, 19, OzUi.Amber);
            DrawOnlineUnitSlots(panel, rules.Extras, selected * 4, DuelRules.AllowedExtras, rules, editable, 423, R.SetRoomRules);
        }

        void DrawOnlineUnitSlots(Transform panel, string[] slots, int offset, string[] available, SkirmishRules rules,
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
                    onlineUnitOptions = available.Where(rules.Allows).ToArray();
                    onlineUnitSlot = index % 4;
                    onlineUnitChoice = choose;
                    onlineRoleRevision = R.HostedRoom.Revision;
                    OpenOnlinePopup(OnlinePopup.Units);
                }, enabled: editable, height: 111);
                if (!string.IsNullOrEmpty(unit))
                {
                    Portrait(card, unit, 88, 6, 69);
                    if (editable) Button(card, "×", 207, 4, 38, () => choose(""), height: 32);
                }
            }
        }

        void DrawOnlineUnitPicker()
        {
            if (R.HostedRoom.Revision != onlineRoleRevision) { onlinePopup = OnlinePopup.None; return; }
            var panel = OnlineDialog("CHOOSE UNIT · SLOT " + (onlineUnitSlot + 1), 400, 175, 1120, 734);
            var scroll = Scroll(panel, 24, 64, 1072, 640, false,
                Math.Max(640, (onlineUnitOptions.Length + 3) / 4 * 144));
            for (int i = 0; i < onlineUnitOptions.Length; i++)
            {
                string type = onlineUnitOptions[i];
                var card = Button(scroll, UnitName(type), i % 4 * 268, i / 4 * 144, 253, () =>
                    { onlineUnitChoice(type); OpenOnlinePopup(OnlinePopup.None); }, height: 130);
                Portrait(card, type, 83, 6, 83);
            }
        }

        void DrawOnlineAiHelp(OnlinePerson[] people, SkirmishRules rules, bool editable)
        {
            if (editable && R.HostedRoom.Revision != onlineRoleRevision) { onlinePopup = OnlinePopup.None; return; }
            int seat = onlinePlayerSeat;
            string name = people.FirstOrDefault(person => person.Seat == seat)?.Name ?? "Fort " + (seat + 1);
            var panel = OnlineDialog("AI HELP · " + name, 440, 239, 1040, 604);
            Label(panel, "IDLE UNITS", 24, 81, 980, 20, OzUi.Amber);
            Label(panel, "The AI can order idle units. A warning appears five seconds before it acts.", 24, 116, 980, 18, OzUi.Ink);
            Label(panel, "AFTER AN ENEMY CAPTURES A FACTORY", 24, 235, 980, 20, OzUi.Amber);
            Label(panel, "Replace restarts a weak build and clears the queue. Cancel stops all production at that factory.", 24, 271, 980, 18, OzUi.Ink);
            for (int i = 0; i < 3; i++)
            {
                int idle = i * 15, capture = i;
                string[] captures = { "OFF", "REPLACE WEAK BUILD", "CANCEL PRODUCTION" };
                Button(panel, idle == 0 ? "OFF" : $"AFTER {idle} s", 24 + i * 332, 157, 318,
                    () => { onlineAiIdle = idle; dirty = true; }, onlineAiIdle == idle, editable);
                Button(panel, captures[i], 24 + i * 332, 319, 318,
                    () => { onlineAiCapture = capture; dirty = true; }, onlineAiCapture == capture, editable);
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
            DrawOnlineUnitSlots(panel, rules.FortTurrets, 0, DuelRules.AllowedFortTurrets, rules, editable, 151, R.SetRoomRules, fort: true);
            Label(panel, "ADDITIONAL UNITS NEAR THE FORT", 25, 307, 650, 20, OzUi.Amber);
            Label(panel, "Each participant receives these units. A robot slot adds one squad.", 25, 343, 700, 17, OzUi.Ink);
            Button(panel, "LOAD / SAVE UNIT PRESET", 758, 310, 307, () =>
                { roomPresets = true; presetStatus = presetName = ""; dirty = true; }, enabled: editable);
            DrawOnlineUnitSlots(panel, rules.Common, 0, DuelRules.AllowedExtras, rules, editable, 386, R.SetRoomRules);
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
