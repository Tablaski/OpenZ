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
        const float SkirmishDifficultyWidth = 240, SkirmishGenerationWidth = 84, SkirmishDifficultyGap = 8;
        static readonly int[] SkirmishCapacities = { 2, 3, 4, 6, 8 };
        readonly Dictionary<(string MapId, bool FixedTeamColors), Texture2D> skirmishGeneratedMinimaps =
            new Dictionary<(string, bool), Texture2D>();
        OzAiMap[] skirmishOfficialMaps, skirmishAiMaps;
        string skirmishOfficialSelection, skirmishAiSelection;
        bool skirmishShowFlyby;
        int skirmishAiSize, skirmishMapPage, skirmishSelectedSeat = -1, skirmishExtraTeam = -1;
        int skirmishDragFrame = -1;
        float skirmishTeamScroll = 1;
        ScrollRect skirmishTeamViewport;
        RectTransform skirmishDragGhost;
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
            if (!canLaunch) Label(root, "Assign at least two opposing teams.", 400, 1003, 1040, 20, OzUi.Amber);
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
                var texture = map.IsOfficial ? GeneratedSkirmishMinimap(map.Id, fixedTeamColors: true) : SkirmishMinimap(map);
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

        Texture2D GeneratedSkirmishMinimap(string mapId, bool fixedTeamColors = false)
        {
            if (string.IsNullOrEmpty(mapId)) return null;
            var key = (mapId, fixedTeamColors);
            if (!skirmishGeneratedMinimaps.TryGetValue(key, out var texture))
                skirmishGeneratedMinimaps[key] = texture = OzLevelInfo.Load(mapId, fixedTeamColors).Mini;
            return texture;
        }

        void BuildSkirmishPreview(OzAiMap map, SkirmishRules rules)
        {
            var preview = Panel("MAP", 620, 185, 748, 780);
            Button(preview, "MINIMAP", 350, 14, 182, () => { skirmishShowFlyby = false; dirty = true; },
                !skirmishShowFlyby, height: 36);
            Button(preview, "3D PREVIEW", 542, 14, 182, () => { skirmishShowFlyby = true; dirty = true; },
                skirmishShowFlyby, height: 36);
            if (roomTechMap != map.Id) UpdateRoomMapData(map.Id);
            if (skirmishShowFlyby)
                BuildLobbyFlyby(preview, map.Id, new OzRect(24, 60, 700, 394), mute: true);
            else
            {
                ReleaseLobbyFlyby();
                var texture = GeneratedSkirmishMinimap(map.Id, fixedTeamColors: true);
                if (texture) DrawRoomMap(preview, texture, 24, 60, 700, 394, localSkirmish.ClosedPositions);
            }
            Label(preview, map.Name, 24, 463, 700, 28, OzUi.Ink);
            int active = map.Starts - localSkirmish.ClosedPositions.Length;
            string size = active < map.Starts ? $"{active} players · {map.Starts - active} starts closed" : $"{active} players";
            if (!map.IsOfficial) OzAiMapBadge.Head(preview, new OzRect(24, 499, 22, 22));
            Label(preview, (map.IsOfficial ? "OpenZ Maps" : "AI Maps") + " · " + size,
                map.IsOfficial ? 24 : 52, 501, 650, 17, OzUi.Muted);
            BuildRoomMapTech(preview, 24, 535, roomMapTech, rules, true, SaveLocalRules, compact: true);
            BuildRoomOpeningOrders(preview, 467, 535, 257, rules, true, SaveLocalRules);
        }

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

            bool hasBots = bots.Length > 0;
            bool modern = hasBots && bots.All(bot => ZAiDifficulty.IsModern(bot.Difficulty));
            ZBotDifficulty? commonDifficulty = hasBots && bots.All(bot => bot.Difficulty == bots[0].Difficulty)
                ? bots[0].Difficulty : null;
            float levelsX = width - 24 - SkirmishDifficultyWidth;
            var difficultyTip = Label(panel, "", 24, 138, width - 48, 13, OzUi.Muted);
            Label(panel, "ALL BOTS", 24, 108, levelsX - SkirmishDifficultyGap - SkirmishGenerationWidth - 24, 15, OzUi.Muted);
            BuildLobbyAiGeneration(panel, new OzRect(levelsX - SkirmishDifficultyGap - SkirmishGenerationWidth, 103, SkirmishGenerationWidth, 27),
                modern, value =>
                {
                    foreach (var bot in bots)
                    {
                        var level = ZAiDifficulty.InGeneration(bot.Difficulty, value);
                        if (level != bot.Difficulty) localSkirmish.SetBot(LocalSkirmishHost, bot.Position, bot.Alliance, level);
                    }
                    dirty = true;
                }, hasBots, allowReselect: !modern && bots.Any(bot => ZAiDifficulty.IsModern(bot.Difficulty)));
            BuildLobbyAiDifficultyRow(panel, new OzRect(levelsX, 98, SkirmishDifficultyWidth, 37), modern, commonDifficulty,
                SetSkirmishBotDifficulty, difficultyTip, hasBots, "ALL BOTS ");
            fortDragFeedback = Label(panel, rules.FreeForAll ? "Drag a fort number to swap starting positions." :
                "Drag a row into a team. Drag a fort number to swap.", 24, 160, width - 48, 16, OzUi.Muted);
            int listTop = 187;
            const int humanHeight = 48, botHeight = 84;
            float contentHeight = humanHeight + bots.Length * botHeight + (rules.FreeForAll ? 0 : alliances.Length * 55);
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
                    y += member.Level.HasValue ? botHeight : humanHeight;
                }
                return;
            }

            foreach (int team in alliances)
            {
                int count = roster.Count(member => member.Team == team);
                float groupHeight = 47 + roster.Where(member => member.Team == team)
                    .Sum(member => member.Level.HasValue ? botHeight : humanHeight);
                var group = Hud.Img(list, $"Team {team + 1}", OzUi.GM900);
                group.rectTransform.TL(0, y, width - 48, groupHeight);
                group.raycastTarget = true;
                group.gameObject.AddComponent<SkirmishTeamDrop>().Configure(team, group, this);
                var heading = Button(group.transform, $"TEAM {team + 1}   ·   {count} {(count == 1 ? "PLAYER" : "PLAYERS")}", 6, 5, width - 60,
                    () => MoveSkirmishMember(skirmishSelectedSeat, team), height: 34);
                var stripe = Hud.Img(heading, "Team color", OzUi.OriginalTeamText((Team)(team + 1)));
                stripe.rectTransform.TL(5, 5, 4, 24);
                stripe.raycastTarget = false;
                float row = 41;
                foreach (var member in roster.Where(member => member.Team == team))
                {
                    BuildSkirmishMember(group.transform, member.Seat, member.Label, member.Level, width - 48, row, false, difficultyTip);
                    row += member.Level.HasValue ? botHeight : humanHeight;
                }
                y += groupHeight + 8;
            }

            float actionsY = listTop + 10 + listHeight;
            Button(panel, "+ ADD TEAM", 24, actionsY, 160, () =>
            {
                skirmishExtraTeam = Enumerable.Range(0, active).First(team => !alliances.Contains(team));
                dirty = true;
            }, enabled: skirmishExtraTeam < 0 && Enumerable.Range(0, active).Any(team => !alliances.Contains(team)), height: 40);
            DrawAllForOneButton(panel, 196, actionsY, 40, rules, SaveLocalRules);
        }

        void BuildSkirmishMember(Transform parent, int seat, string name, ZBotDifficulty? difficulty, float width, float y, bool freeForAll, OzText tip)
        {
            const float nameX = 40, rightInset = 8;
            float cardWidth = width - 12;
            float cardHeight = difficulty.HasValue ? 80 : 38;
            float generationX = cardWidth - rightInset - SkirmishGenerationWidth;
            var card = Hud.Node(name, parent);
            card.TL(6, y, cardWidth, cardHeight);
            if (freeForAll)
            {
                var background = Hud.Img(card, "Background", OzUi.GM900);
                background.rectTransform.Fill();
                background.raycastTarget = false;
            }
            else
            {
                Button(card, "", 0, 0, cardWidth, () =>
                {
                    if (skirmishDragGhost || skirmishDragFrame == Time.frameCount) return;
                    skirmishSelectedSeat = skirmishSelectedSeat == seat ? -1 : seat;
                    dirty = true;
                }, skirmishSelectedSeat == seat, height: cardHeight);
                card.gameObject.AddComponent<SkirmishCardDrag>().Configure(this, seat, name);
            }
            var caption = Label(card, freeForAll ? name : "::   " + name, nameX, 9, generationX - nameX - 8, 17,
                !freeForAll && skirmishSelectedSeat == seat ? (Color)OzUi.GM950 : OzUi.Ink);
            caption.graphic.raycastTarget = false;
            var fort = Hud.Img(card, "Starting fort", OzUi.OriginalTeamText((Team)(seat + 1)));
            fort.rectTransform.TL(7, 4, 28, 29);
            Hud.Label(fort.transform, "Fort number", (seat + 1).ToString(), 15, OzUi.GM950).FillParent(OzH.C).graphic.raycastTarget = false;
            AddFortHandle(fort, seat, true, (first, second) =>
            {
                localSkirmish.SwapForts(LocalSkirmishHost, first, second);
                dirty = true;
            });
            if (!difficulty.HasValue) return;
            bool modern = ZAiDifficulty.IsModern(difficulty.Value);
            BuildLobbyAiGeneration(card, new OzRect(generationX, 6, SkirmishGenerationWidth, 26), modern,
                value => ApplyLevel(ZAiDifficulty.InGeneration(difficulty.Value, value)));
            var divider = Hud.Img(card, "Difficulty divider", OzUi.GM500);
            divider.rectTransform.TL(rightInset, 36, cardWidth - 2 * rightInset, 1);
            divider.raycastTarget = false;
            BuildLobbyAiDifficultyRow(card, new OzRect(rightInset, 40, cardWidth - 2 * rightInset, 32), modern, difficulty, ApplyLevel, tip);

            void ApplyLevel(ZBotDifficulty level)
            {
                var bot = localSkirmish.Bots.Single(entry => entry.Position == seat);
                localSkirmish.SetBot(LocalSkirmishHost, seat, bot.Alliance, level);
                dirty = true;
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

        static void DrawGrip(Transform parent)
        {
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 2; column++)
                {
                    var dot = Hud.Img(parent, "Drag grip", OzUi.Muted);
                    dot.rectTransform.TL(10 + column * 6, 7 + row * 6, 3, 3);
                    dot.raycastTarget = false;
                }
        }

        OzText fortDragFeedback;
        int selectedFort = -1;
        int draggedFort = -1;

        void AddFortHandle(Image badge, int seat, bool occupied, Action<int, int> move)
        {
            badge.raycastTarget = true;
            var original = badge.color;
            if (selectedFort == seat) badge.color = OzUi.Amber;
            AddOnlineEvent(badge.rectTransform, EventTriggerType.PointerClick, _ =>
            {
                int previous = selectedFort;
                selectedFort = occupied && previous < 0 ? seat : -1;
                if (previous >= 0 && previous != seat) move(previous, seat);
                else dirty = true;
            });
            AddOnlineEvent(badge.rectTransform, EventTriggerType.PointerEnter, _ =>
            {
                if (draggedFort < 0 || draggedFort == seat) return;
                badge.color = OzUi.Amber;
                fortDragFeedback?.Set(occupied ? $"Swap Fort {draggedFort + 1} and Fort {seat + 1}" :
                    $"Move from Fort {draggedFort + 1} to Fort {seat + 1}");
            });
            AddOnlineEvent(badge.rectTransform, EventTriggerType.PointerExit, _ =>
            {
                badge.color = original;
                if (draggedFort >= 0) fortDragFeedback?.Set("Drop on the destination fort.");
            });
            AddOnlineEvent(badge.rectTransform, EventTriggerType.Drop, _ =>
            {
                if (draggedFort >= 0 && draggedFort != seat) move(draggedFort, seat);
            });
            if (!occupied) return;
            AddOnlineEvent(badge.rectTransform, EventTriggerType.BeginDrag, evt =>
            {
                if (((PointerEventData)evt).button != PointerEventData.InputButton.Left) return;
                draggedFort = seat;
                selectedFort = -1;
                BeginSkirmishDrag("Fort " + (seat + 1));
                fortDragFeedback?.Set("Drop on the destination fort.");
            });
            AddOnlineEvent(badge.rectTransform, EventTriggerType.Drag, evt => UpdateSkirmishDrag((PointerEventData)evt));
            AddOnlineEvent(badge.rectTransform, EventTriggerType.EndDrag, _ => { draggedFort = -1; ClearSkirmishDrag(); dirty = true; });
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

        internal void BeginSkirmishDrag(string name, RectTransform card = null)
        {
            ClearSkirmishDrag();
            if (card)
            {
                skirmishDragGhost = Instantiate(card, root, false);
                var group = skirmishDragGhost.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                return;
            }
            var badge = Hud.Img(root, "Dragged fort", OzUi.GM900);
            skirmishDragGhost = badge.rectTransform;
            skirmishDragGhost.sizeDelta = new Vector2(190, 45);
            badge.raycastTarget = false;
            var label = Hud.Label(skirmishDragGhost, "Fort", name, 18, OzUi.Ink).FillParent(OzH.C);
            label.graphic.raycastTarget = false;
        }

        internal void UpdateSkirmishDrag(PointerEventData pointer)
        {
            if (!skirmishDragGhost) return;
            skirmishDragFrame = Time.frameCount;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer.position, pointer.pressEventCamera, out var point))
                skirmishDragGhost.TL(point.x - root.rect.xMin + 15, root.rect.yMax - point.y + 15,
                    skirmishDragGhost.sizeDelta.x, skirmishDragGhost.sizeDelta.y);
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
            menu.BeginSkirmishDrag(participant, (RectTransform)transform);
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
