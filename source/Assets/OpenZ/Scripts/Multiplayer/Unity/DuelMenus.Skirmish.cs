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
        const int SkirmishCardsPerPage = 6;
        static readonly int[] SkirmishCapacities = { 2, 3, 4, 6, 8 };
        readonly Dictionary<string, Texture2D> skirmishGeneratedMinimaps = new Dictionary<string, Texture2D>();
        OzAiMap[] skirmishOfficialMaps, skirmishAiMaps;
        string skirmishOfficialSelection, skirmishAiSelection;
        int skirmishAiSize, skirmishMapPage, skirmishSelectedSeat = -1, skirmishExtraTeam = -1;
        int skirmishDragFrame = -1;
        float skirmishTeamScroll = 1;
        ScrollRect skirmishTeamViewport;
        Image skirmishDragGhost;
        SkirmishTeamDrop skirmishDropTarget;

        RectTransform CreateSkirmishFrame(RectTransform parent)
        {
            var frame = Hud.Node("Skirmish frame", parent);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(1920, 1080);
            return frame;
        }

        void FitSkirmishFrame()
        {
            var available = overlayRoot.rect.size;
            if (available.x <= 0 || available.y <= 0) return;
            root.localScale = Vector3.one * Mathf.Min(available.x / 1920, available.y / 1080);
        }

        void ShowSkirmish()
        {
            roomTab = 0;
            skirmishSelectedSeat = -1;
            pickerGroup = -1;
            roomFullMap = roomCustomUnits = combatDropdown = roomPresets = false;
            page = Page.Skirmish;
            dirty = true;
        }

        internal void ReturnToLocalSkirmishLobby()
        {
            ShowSkirmish();
            OzScenes.MainMenu();
        }

        void BuildSkirmish()
        {
            if (localSkirmish == null)
            {
                skirmishOfficialMaps = OzAiMapCatalog.OfficialSkirmishMaps.Where(HasSkirmishScene).ToArray();
                skirmishAiMaps = OzAiMapCatalog.SkirmishMaps.Where(HasSkirmishScene).ToArray();
                var initial = OzAiMapCatalog.ThreePlayerSkirmish;
                localSkirmish = new MultiplayerRoom(LocalSkirmishHost, "Commander", initial.Id, allowSoloConversions: true);
                SelectSkirmishMap(initial, initial.Starts);
            }
            ClearSkirmishDrag();
            skirmishTeamViewport = null;
            var map = OzAiMapCatalog.FindSkirmishMap(localSkirmish.Map);
            var rules = localSkirmish.Rules;
            string[] tabs = { "MAP & TEAMS", "STARTING ARMIES", "BALANCING OPTIONS", "PLAYER ADVANTAGE", "BATTLE RULES" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button(root, tabs[i], 60 + i * 241, 123, 230, () => { roomTab = tab; combatDropdown = false; dirty = true; }, roomTab == tab);
            }

            if (roomTab == 0)
            {
                BuildSkirmishCatalog(map);
                BuildSkirmishPreview(map, rules);
                BuildSkirmishTeams(rules, 1380, 480);
            }
            else
            {
                ReleaseLobbyFlyby();
                BuildRoomRules(localSkirmish.Starts, true, rules, SaveLocalRules);
                BuildSkirmishTeams(rules, 1188, 672);
            }

            Button(root, "BACK TO MULTIPLAYER", 60, 988, 320, ShowConnect, height: 52);
            bool canLaunch = localSkirmish.CanLaunchWithHostReady;
            if (!canLaunch) Label(root, localSkirmish.StartBlocker == MatchStartBlocker.UnequalTeams
                ? MultiplayerRoom.EqualTeamSizeWarning : "Assign at least two opposing teams.", 400, 1003, 1040, 20, OzUi.Amber);
            Button(root, "START GAME", 1580, 988, 280, LaunchLocalSkirmish, true, canLaunch, height: 52);
            if (roomCustomUnits) BuildRoomCustomUnits(rules, SaveLocalRules);
            if (roomPresets) BuildRoomArmyPresets(rules, SaveLocalRules);
            if (pickerGroup >= 0) BuildSkirmishUnitPicker(rules);
        }

        OzAiMap[] SkirmishAiMapsFor(int size) =>
            skirmishAiMaps.Where(map => map.Starts == size).ToArray();

        void SelectSkirmishCatalog(bool official)
        {
            var entries = official ? skirmishOfficialMaps : SkirmishAiMapsFor(skirmishAiSize);
            var saved = official ? skirmishOfficialSelection : skirmishAiSelection;
            int index = Array.FindIndex(entries, map => map.Id == saved);
            if (index < 0) index = 0;
            skirmishMapPage = index / SkirmishCardsPerPage;
            var chosen = entries[index];
            SelectSkirmishMap(chosen, official ? chosen.Starts : skirmishAiSize);
        }

        void SelectSkirmishMap(OzAiMap map, int seats)
        {
            localSkirmish.SelectMap(LocalSkirmishHost, map.Id);
            if (map.IsOfficial) skirmishOfficialSelection = map.Id;
            else
            {
                skirmishAiSelection = map.Id;
                skirmishAiSize = seats;
            }

            int host = localSkirmish.HostPosition;
            var activePositions = Enumerable.Range(0, map.Starts).Where(position => position != host).Take(seats - 1).ToArray();
            var hostMember = localSkirmish.Members.Single();
            var bots = localSkirmish.Bots.ToDictionary(bot => bot.Position);
            var rules = localSkirmish.Rules;
            if (!rules.FreeForAll && hostMember.Alliance >= seats)
                localSkirmish.AssignHuman(LocalSkirmishHost, LocalSkirmishHost, host, 0);
            for (int position = 0; position < map.Starts; position++)
            {
                if (position == host) continue;
                if (!activePositions.Contains(position))
                {
                    localSkirmish.SetClosed(LocalSkirmishHost, position, true);
                    continue;
                }
                bool hadBot = bots.TryGetValue(position, out var bot);
                int team = hadBot && bot.Alliance < seats ? bot.Alliance : Array.IndexOf(activePositions, position) + 1;
                localSkirmish.SetBot(LocalSkirmishHost, position, rules.FreeForAll ? position : team,
                    hadBot ? bot.Difficulty : ZBotDifficulty.Normal);
            }
            skirmishSelectedSeat = skirmishExtraTeam = -1;
            skirmishTeamScroll = 1;
            dirty = true;
        }

        void BuildSkirmishCatalog(OzAiMap selected)
        {
            var catalog = Panel("SELECT A MAP", 60, 185, 548, 780);
            Button(catalog, "OPENZ MAPS", 24, 62, 244, () => SelectSkirmishCatalog(true),
                selected.IsOfficial, skirmishOfficialMaps.Length > 0, height: 50);
            Button(catalog, "AI MAPS", 280, 62, 244, () => SelectSkirmishCatalog(false),
                !selected.IsOfficial, height: 50);
            var entries = selected.IsOfficial ? skirmishOfficialMaps : SkirmishAiMapsFor(skirmishAiSize);
            if (selected.IsOfficial) Label(catalog, "2 PLAYERS", 24, 132, 500, 18, OzUi.Muted);
            else
            {
                Label(catalog, "PLAYERS", 24, 135, 95, 17, OzUi.Muted);
                for (int i = 0; i < SkirmishCapacities.Length; i++)
                {
                    int size = SkirmishCapacities[i];
                    var maps = SkirmishAiMapsFor(size);
                    var sizeButton = Button(catalog, size.ToString(), 126 + i * 72, 122, 63, () =>
                    {
                        skirmishAiSize = size;
                        SelectSkirmishCatalog(false);
                    }, skirmishAiSize == size, maps.Length > 0, height: 44);
                    sizeButton.name = size + " players";
                }
            }

            int maxPage = Mathf.Max(0, (entries.Length - 1) / SkirmishCardsPerPage);
            skirmishMapPage = Mathf.Clamp(skirmishMapPage, 0, maxPage);
            var visible = entries.Skip(skirmishMapPage * SkirmishCardsPerPage).Take(SkirmishCardsPerPage).ToArray();
            for (int i = 0; i < visible.Length; i++)
            {
                var map = visible[i];
                var card = Button(catalog, map.Name, 24 + i % 2 * 254, 182 + i / 2 * 170, 246,
                    () => SelectSkirmishMap(map, selected.IsOfficial ? map.Starts : skirmishAiSize),
                    map.Id == selected.Id, height: 158);
                card.name = "MAP " + map.Id;
                var texture = SkirmishMinimap(map);
                if (texture)
                {
                    var mini = Hud.Node("Complete minimap", card).gameObject.AddComponent<RawImage>();
                    mini.texture = texture;
                    float scale = Mathf.Min(220f / texture.width, 120f / texture.height);
                    float width = texture.width * scale, height = texture.height * scale;
                    mini.rectTransform.TL((246 - width) / 2, 5 + (120 - height) / 2, width, height);
                    mini.raycastTarget = false;
                }
            }
            if (maxPage == 0) return;
            Button(catalog, "<", 24, 710, 74, () => { skirmishMapPage--; dirty = true; }, enabled: skirmishMapPage > 0);
            Label(catalog, $"Page {skirmishMapPage + 1} / {maxPage + 1}", 124, 723, 320, 18, OzUi.Muted);
            Button(catalog, ">", 450, 710, 74, () => { skirmishMapPage++; dirty = true; }, enabled: skirmishMapPage < maxPage);
        }

        Texture2D SkirmishMinimap(OzAiMap map)
        {
            if (!map.IsOfficial)
            {
                var generatedAsset = Resources.Load<Texture2D>("OpenZ/AiMaps/Previews/" + map.Id);
                if (generatedAsset) return generatedAsset;
            }
            return GeneratedSkirmishMinimap(map.Id);
        }

        Texture2D GeneratedSkirmishMinimap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return null;
            if (!skirmishGeneratedMinimaps.TryGetValue(mapId, out var texture))
                skirmishGeneratedMinimaps[mapId] = texture = OzLevelInfo.Load(mapId).Mini;
            return texture;
        }

        void BuildSkirmishPreview(OzAiMap map, SkirmishRules rules)
        {
            var preview = Panel("3D PREVIEW", 620, 185, 748, 780);
            BuildLobbyFlyby(preview, map.Id, new OzRect(24, 60, 700, 394), mute: true);
            Label(preview, map.Name, 24, 463, 700, 28, OzUi.Ink);
            int active = map.Starts - localSkirmish.ClosedPositions.Length;
            string size = active < map.Starts ? $"{active} players · {map.Starts - active} starts closed" : $"{active} players";
            if (!map.IsOfficial) OzAiMapBadge.Head(preview, new OzRect(24, 499, 22, 22));
            Label(preview, (map.IsOfficial ? "OpenZ Maps" : "AI Maps") + " · " + size,
                map.IsOfficial ? 24 : 52, 501, 650, 17, OzUi.Muted);
            if (roomTechMap != map.Id) UpdateRoomMapData(map.Id);
            BuildRoomMapTech(preview, 24, 535, roomMapTech, rules, true, SaveLocalRules, compact: true);
            BuildRoomOpeningOrders(preview, 467, 535, 257, rules, true, SaveLocalRules);
        }

        static string AllForOneLabel(bool enabled) => $"💪 ALL FOR ONE · {(enabled ? "ON" : "OFF")}";
        const string AllForOneDescription = "When an ally loses their fort, all allies lose immediately.";

        void BuildSkirmishTeams(SkirmishRules rules, float x, float width)
        {
            var panel = Panel("TEAMS", x, 185, width, 780);
            Button(panel, "TEAMS", width - 317, 54, 124, () => SetSkirmishFreeForAll(false), !rules.FreeForAll,
                localSkirmish.SupportsTeams, height: 37);
            Button(panel, "FREE FOR ALL", width - 184, 54, 160, () => SetSkirmishFreeForAll(true), rules.FreeForAll, height: 37);
            var bots = localSkirmish.Bots;
            int active = bots.Length + 1;
            int host = localSkirmish.HostPosition;
            var roster = bots.Select(bot => (Seat: bot.Position, Team: bot.Alliance,
                Label: $"AI Bot {bot.Position + (bot.Position < host ? 1 : 0)}", Level: (ZBotDifficulty?)bot.Difficulty))
                .Prepend((Seat: host, Team: localSkirmish.Members.Single().Alliance, Label: "You", Level: (ZBotDifficulty?)null)).ToArray();
            int[] alliances = roster.Select(member => member.Team).Append(skirmishExtraTeam).Where(team => team >= 0)
                .Distinct().OrderBy(team => team).ToArray();

            var difficultyTip = Label(panel, "", 24, 184, width - 48, 13, OzUi.Muted);
            AllBotsRow(98, "ALL BOTS · 1996", LobbyBotDifficulties);
            AllBotsRow(139, "ALL BOTS · 2026", ZAiDifficulty.Modern);
            void AllBotsRow(float y, string title, ZBotDifficulty[] levels)
            {
                Label(panel, title, 24, y + 10, 160, 15, OzUi.Muted);
                for (int i = 0; i < levels.Length; i++)
                {
                    var level = levels[i];
                    string difficultyName = BotDifficultyName(level).ToUpperInvariant();
                    var button = Button(panel, difficultyName, width - 24 - (levels.Length - i) * 73, y, 69,
                        () => SetSkirmishBotDifficulty(level), bots.Length > 0 && bots.All(bot => bot.Difficulty == level),
                        bots.Length > 0, height: 37);
                    button.name = "ALL BOTS " + difficultyName;
                    AddLobbyTip(button, difficultyTip, ZAiDifficulty.Hint(level));
                }
            }
            Label(panel, rules.FreeForAll ? localSkirmish.SupportsTeams ? $"{active} players · each plays for their own side" :
                "This player count cannot form equal teams." :
                "Drag a card onto a team, or select a card and team.", 24, 207, width - 48, 16, OzUi.Muted);
            int listTop = 233;
            if (!rules.FreeForAll)
            {
                Button(panel, AllForOneLabel(rules.AllForOne), 24, 233, 248, () =>
                {
                    rules.AllForOne = !rules.AllForOne;
                    SaveLocalRules(rules);
                }, rules.AllForOne, height: 35);
                Label(panel, AllForOneDescription, 24, 272, width - 48, 15, OzUi.Ink);
                listTop = 302;
            }
            const int memberHeight = 70;
            float contentHeight = active * memberHeight + (rules.FreeForAll ? 0 : alliances.Length * 55);
            float listHeight = Mathf.Min(709 - listTop, contentHeight);
            var list = Scroll(panel, 24, listTop, width - 48, listHeight, false, contentHeight);
            skirmishTeamViewport = list.parent.GetComponent<ScrollRect>();
            skirmishTeamViewport.verticalNormalizedPosition = skirmishTeamScroll;
            skirmishTeamViewport.onValueChanged.AddListener(position => skirmishTeamScroll = position.y);
            float y = 0;
            if (rules.FreeForAll)
            {
                foreach (var member in roster)
                {
                    BuildSkirmishMember(list, member.Seat, member.Label, member.Level, width - 48, y, true, difficultyTip);
                    y += memberHeight;
                }
                return;
            }

            foreach (int team in alliances)
            {
                int count = roster.Count(member => member.Team == team);
                float groupHeight = 47 + count * memberHeight;
                var group = Hud.Img(list, $"Team {team + 1}", OzUi.GM900);
                group.rectTransform.TL(0, y, width - 48, groupHeight);
                group.raycastTarget = true;
                group.gameObject.AddComponent<SkirmishTeamDrop>().Configure(team, group, this);
                var heading = Button(group.transform, $"TEAM {team + 1}   ·   {count} {(count == 1 ? "PLAYER" : "PLAYERS")}", 6, 5, width - 60,
                    () => MoveSkirmishMember(skirmishSelectedSeat, team), height: 34);
                var stripe = Hud.Img(heading, "Team color", OzUi.TeamText((Team)(team + 1)));
                stripe.rectTransform.TL(5, 5, 4, 24);
                stripe.raycastTarget = false;
                float row = 41;
                foreach (var member in roster.Where(member => member.Team == team))
                {
                    BuildSkirmishMember(group.transform, member.Seat, member.Label, member.Level, width - 48, row, false, difficultyTip);
                    row += memberHeight;
                }
                y += groupHeight + 8;
            }

            Button(panel, "+ ADD TEAM", 24, listTop + 10 + listHeight, 160, () =>
            {
                skirmishExtraTeam = Enumerable.Range(0, active).First(team => !alliances.Contains(team));
                dirty = true;
            }, enabled: skirmishExtraTeam < 0 && Enumerable.Range(0, active).Any(team => !alliances.Contains(team)), height: 40);
        }

        void BuildSkirmishMember(Transform parent, int seat, string name, ZBotDifficulty? difficulty, float width, float y, bool freeForAll, OzText tip)
        {
            const int choicesWidth = 270;
            if (freeForAll)
            {
                var row = Hud.Img(parent, name, OzUi.GM900);
                row.rectTransform.TL(6, y, width - 12, difficulty.HasValue ? 64 : 38);
                row.raycastTarget = false;
                Label(row.transform, name, 16, 9, width - (difficulty.HasValue ? choicesWidth : 45), 17, OzUi.Ink);
            }
            else
            {
                var card = Button(parent, "::   " + name, 6, y, width - (difficulty.HasValue ? choicesWidth : 12), () =>
                {
                    if (skirmishDragGhost || skirmishDragFrame == Time.frameCount) return;
                    skirmishSelectedSeat = skirmishSelectedSeat == seat ? -1 : seat;
                    dirty = true;
                }, skirmishSelectedSeat == seat, height: 38, alignLeft: true);
                card.gameObject.AddComponent<SkirmishCardDrag>().Configure(this, seat, name);
            }
            if (!difficulty.HasValue) return;
            Row("1996", LobbyBotDifficulties, y + 1);
            Row("2026", ZAiDifficulty.Modern, y + 33);
            void Row(string generation, ZBotDifficulty[] levels, float top)
            {
                Label(parent, generation, width - choicesWidth + 4, top + 7, 38, 11, OzUi.Muted);
                for (int i = 0; i < levels.Length; i++)
                {
                    var level = levels[i];
                    var option = Button(parent, BotDifficultyName(level).ToUpperInvariant(), width - 222 + i * 54, top, 51, () =>
                    {
                        var bot = localSkirmish.Bots.Single(entry => entry.Position == seat);
                        localSkirmish.SetBot(LocalSkirmishHost, seat, bot.Alliance, level);
                        dirty = true;
                    }, level == difficulty.Value, height: 30);
                    AddLobbyTip(option, tip, ZAiDifficulty.Hint(level));
                }
            }
        }

        void SetSkirmishBotDifficulty(ZBotDifficulty difficulty)
        {
            foreach (var bot in localSkirmish.Bots)
                localSkirmish.SetBot(LocalSkirmishHost, bot.Position, bot.Alliance, difficulty);
            dirty = true;
        }

        void SetSkirmishFreeForAll(bool enabled)
        {
            var rules = localSkirmish.Rules;
            if (rules.FreeForAll == enabled) return;
            rules.FreeForAll = enabled;
            skirmishExtraTeam = skirmishSelectedSeat = -1;
            skirmishTeamScroll = 1;
            SaveLocalRules(rules);
        }

        internal void MoveSkirmishMember(int seat, int team)
        {
            if (seat < 0 || localSkirmish.Rules.FreeForAll) return;
            if (seat == localSkirmish.HostPosition)
                localSkirmish.AssignHuman(LocalSkirmishHost, LocalSkirmishHost, seat, team);
            else
            {
                var bot = localSkirmish.Bots.Single(entry => entry.Position == seat);
                localSkirmish.SetBot(LocalSkirmishHost, seat, team, bot.Difficulty);
            }
            skirmishExtraTeam = skirmishExtraTeam == team ? -1 : skirmishExtraTeam;
            skirmishSelectedSeat = -1;
            dirty = true;
        }

        internal void BeginSkirmishDrag(string name)
        {
            ClearSkirmishDrag();
            skirmishDragGhost = Hud.Img(root, "Dragged participant", OzUi.GM900);
            skirmishDragGhost.raycastTarget = false;
            var label = Hud.Label(skirmishDragGhost.transform, "Participant", name, 18, OzUi.Ink).FillParent(OzH.C);
            label.graphic.raycastTarget = false;
        }

        internal void UpdateSkirmishDrag(PointerEventData pointer)
        {
            if (!skirmishDragGhost) return;
            skirmishDragFrame = Time.frameCount;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer.position, pointer.pressEventCamera, out var point))
                skirmishDragGhost.rectTransform.TL(point.x - root.rect.xMin + 15, root.rect.yMax - point.y + 15, 190, 45);
            var target = pointer.pointerCurrentRaycast.gameObject?.GetComponentInParent<SkirmishTeamDrop>();
            if (skirmishDropTarget != target)
            {
                if (skirmishDropTarget) skirmishDropTarget.Highlight(false);
                skirmishDropTarget = target;
                if (skirmishDropTarget) skirmishDropTarget.Highlight(true);
            }
            if (!skirmishTeamViewport || skirmishTeamViewport.content.rect.height <= skirmishTeamViewport.viewport.rect.height) return;
            var viewport = skirmishTeamViewport.viewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer.position, pointer.pressEventCamera, out var local) ||
                !viewport.rect.Contains(local)) return;
            const float edge = 36;
            if (local.y < viewport.rect.yMin + edge)
                skirmishTeamViewport.verticalNormalizedPosition -= 1.6f * Time.unscaledDeltaTime;
            else if (local.y > viewport.rect.yMax - edge)
                skirmishTeamViewport.verticalNormalizedPosition += 1.6f * Time.unscaledDeltaTime;
        }

        internal void ClearSkirmishDrag()
        {
            if (skirmishDropTarget) skirmishDropTarget.Highlight(false);
            if (skirmishDragGhost)
            {
                Destroy(skirmishDragGhost.gameObject);
                skirmishDragFrame = Time.frameCount;
            }
            skirmishDropTarget = null;
            skirmishDragGhost = null;
        }
    }

    internal sealed class SkirmishCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        DuelMenus menu;
        string participant;
        internal int Seat { get; private set; }

        internal void Configure(DuelMenus owner, int seat, string name)
        {
            menu = owner;
            Seat = seat;
            participant = name;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            menu.BeginSkirmishDrag(participant);
            menu.UpdateSkirmishDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData) => menu.UpdateSkirmishDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => menu.ClearSkirmishDrag();
    }

    internal sealed class SkirmishTeamDrop : MonoBehaviour, IDropHandler
    {
        int team;
        Image background;
        DuelMenus menu;

        internal void Configure(int alliance, Image image, DuelMenus owner)
        {
            team = alliance;
            background = image;
            menu = owner;
        }

        internal void Highlight(bool active) => background.color = active ? (Color)OzUi.AmberLo : OzUi.GM900;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                eventData.pointerDrag && eventData.pointerDrag.TryGetComponent<SkirmishCardDrag>(out var card))
                menu.MoveSkirmishMember(card.Seat, team);
        }
    }
}
