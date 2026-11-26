using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        enum LobbyDialog { None, Units, Proposal, OpenZMaps, ArmyPresets, Rename, AiHelp }
        LobbyDialog lobbyDialog;
        float allowedScroll = 1;
        string openZMapId, presetName, presetStatus;
        string nameDraft, renameStatus;
        OzLevelPreview lobbyFlyby;
        RawImage lobbyFlybyImage;
        bool lobbyFlybySuspended, lobbyFlybyMuted;
        public const string ArmyPresetsPreference = "oz_duel_outside_army_presets";

        [Serializable] sealed class ArmyPreset { public string name; public string[] units; }
        [Serializable] sealed class ArmyPresets { public List<ArmyPreset> entries = new List<ArmyPreset>(); }

        void ApplyLobbyMap(DuelRules rules, int revision, string id)
        {
            if (R.View == null || R.View.Revision != revision || R.View.Phase != (int)DuelPhase.Lobby || rules.Map == id) return;
            // Keep edits made to a guest draft while a separate map gallery was open.
            rules = (lobbyDraft ?? rules).Copy();
            rules.SelectMap(id);
            if (!R.IsHost && lobbyDraft == null) R.ProposeRules(rules, revision);
            else EditLobbyRules(rules);
        }

        void BuildOpenZMapPicker(DuelRules rules, bool editable, int revision)
        {
            var shade = Hud.Img(root, "OpenZ maps shade", new Color(0, 0, 0, .94f));
            shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel("OPENZ MAPS", 180, 146, 1560, 788);
            panel.name = "OpenZ map picker";
            var logo = Hud.Kit(panel, "ForkWordmark", "Wordmarks/wm_openz_vk_menu",
                new OzRect(620, 5, 230, 76), null, Image.Type.Simple);
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            if (!logo.sprite) Label(panel, "OpenZ-VK", 630, 22, 210, 26, OzForkOptions.Brand);
            Button(panel, "CLOSE", 1325, 12, 210, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            for (int i = 0; i < OzLevelDef.All.Length; i++)
            {
                var level = OzLevelDef.All[i];
                var card = Button(panel, level.Number + " · " + level.Name, 26, 102 + i * 134, 410,
                    () => { openZMapId = level.Id; dirty = true; }, openZMapId == level.Id, true, 115, true);
                card.name = "MAP " + level.Id;
            }
            var selected = OzLevelDef.All.First(d => d.Id == openZMapId);
            BuildLobbyFlyby(panel, selected.Id, new OzRect(466, 102, 1068, 600), mute: false);
            Label(panel, selected.Name + " · " + selected.Mood, 466, 724, 735, 21, OzUi.Ink);
            Button(panel, "APPLY MAP", 1224, 715, 310, () =>
            {
                ApplyLobbyMap(rules, revision, selected.Id);
                lobbyDialog = LobbyDialog.None;
                dirty = true;
            }, true, editable);
        }

        void BuildLobbyFlyby(Transform parent, string mapId, OzRect rect, bool mute)
        {
            if (lobbyFlyby?.Level != mapId || lobbyFlybyMuted != mute)
            {
                ReleaseLobbyFlyby();
                lobbyFlyby = OzLevelPreview.Create(transform, mapId, mute);
                lobbyFlybyMuted = mute;
                lobbyFlyby.PlayFrom(OzLevelPreview.PosterTime, true);
            }
            lobbyFlybyImage = Hud.Node("Map flyby", parent).gameObject.AddComponent<RawImage>();
            lobbyFlybyImage.rectTransform.Place(rect);
            lobbyFlybyImage.raycastTarget = false;
            if (OzAiMapCatalog.Find(mapId) != null) OzAiMapBadge.Stamp(lobbyFlybyImage.rectTransform, rect.w);
        }

        void TickLobbyFlyby()
        {
            if (!lobbyFlybyImage || lobbyFlyby == null) return;
            bool galleryOpen = OzAiMapGallery.IsOpen;
            if (galleryOpen != lobbyFlybySuspended)
            {
                lobbyFlybySuspended = galleryOpen;
                if (galleryOpen) lobbyFlyby.Pause();
                else lobbyFlyby.PlayFrom(OzLevelPreview.PosterTime, true);
            }
            if (galleryOpen) return;
            OzLevelPreview.PumpPrepares();
            var texture = lobbyFlyby.Sharp;
            lobbyFlybyImage.texture = texture;
            lobbyFlybyImage.enabled = texture;
            if (!texture) return;
            var size = lobbyFlybyImage.rectTransform.sizeDelta;
            var cover = OzLevelPreview.Cover((float)texture.width / texture.height, size.x / size.y);
            lobbyFlybyImage.uvRect = lobbyFlyby.VideoOk ? cover : OzLevelPreview.KenBurns(cover, OzMotion.Now, 0);
        }

        void ReleaseLobbyFlyby()
        {
            if (lobbyFlyby == null) return;
            lobbyFlyby.Release();
            lobbyFlyby = null;
            lobbyFlybySuspended = false;
        }

        static ArmyPresets ReadArmyPresets()
        {
            var json = PlayerPrefs.GetString(ArmyPresetsPreference, "");
            if (json.Length == 0) return new ArmyPresets();
            try { return JsonUtility.FromJson<ArmyPresets>(json) ?? new ArmyPresets(); }
            catch (ArgumentException) { return new ArmyPresets(); }
        }

        void BuildArmyPresets(DuelRules rules, bool editable, Action<DuelRules> commit = null, Action dismiss = null)
        {
            var shade = Hud.Img(root, "Army presets shade", new Color(0, 0, 0, .94f));
            shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel("OUTSIDE THE FORT · PRESETS", 320, 165, 1280, 750);
            if (OnlineRoomPage) FitSetupDialog(shade.rectTransform, panel, 1280, 750);
            dismiss ??= () => { lobbyDialog = LobbyDialog.None; dirty = true; };
            Button(panel, "CLOSE", 1030, 12, 225, dismiss);
            Label(panel, "Load a four-slot formation for both teams. Units disabled by the lobby roster leave their slots empty.", 24, 70, 1220, 17, OzUi.Muted);
            Label(panel, editable ? "SAVE CURRENT FOUR SLOTS" : "CURRENT FOUR SLOTS · READ ONLY", 24, 123, 470, 17, OzUi.Ink);
            if (editable) Input(panel, "", 24, 155, 430, presetName, value => presetName = value, 32);
            else Label(panel, string.Join("  /  ", rules.Common.StartingUnits.Select(UnitName)), 24, 164, 1080, 17, OzUi.Ink);
            Button(panel, "SAVE AS", 474, 155, 175, () =>
            {
                string name = presetName.Trim();
                var presets = ReadArmyPresets();
                presets.entries ??= new List<ArmyPreset>();
                if (name.Length == 0 || name.Any(char.IsControl) || name.Contains("<") || name.Contains(">"))
                    presetStatus = "Enter a preset name without angle brackets.";
                else if (presets.entries.Any(p => string.Equals(p?.name, name, StringComparison.OrdinalIgnoreCase)))
                    presetStatus = "A preset with this name already exists.";
                else
                {
                    var units = Enumerable.Range(0, 4).Select(i => rules.Common.StartingUnits.ElementAtOrDefault(i) ?? "").ToArray();
                    presets.entries.Add(new ArmyPreset { name = name, units = units });
                    PlayerPrefs.SetString(ArmyPresetsPreference, JsonUtility.ToJson(presets)); PlayerPrefs.Save();
                    presetStatus = "Saved " + name;
                    presetName = "";
                }
                dirty = true;
            }, enabled: editable);
            if (editable) Label(panel, presetStatus ?? "", 680, 167, 570, 16, OzUi.Amber);
            var saved = (editable ? ReadArmyPresets().entries : null)?.Where(p => p != null && !string.IsNullOrWhiteSpace(p.name) &&
                !p.name.Contains("<") && !p.name.Contains(">") && DuelMissionDefaults.IsOutsidePreset(p.units)).ToList() ?? new List<ArmyPreset>();
            saved.Insert(0, new ArmyPreset { name = "OpenZ-VK default", units = DuelMissionDefaults.OutsideUnits() });
            saved.Insert(1, new ArmyPreset { name = "Light and heavy assault", units = new[] { "light", "heavy", "pyro", "laser" } });
            var followed = OnlineRoomPage && FollowingHost && displayedSetup?.Panel == DuelRuntime.SetupPanel.Presets &&
                displayedSetup.Formations != null ? displayedSetup : null;
            if (OnlineRoomPage && editable)
            {
                setupPresetNames = saved.Take(4096).Select(preset => SetupChoiceLabel(preset.name, 50)).ToArray();
                setupPresetFormations = saved.Take(4096)
                    .Select(preset => SetupChoiceLabel(string.Join("  /  ", preset.units.Select(UnitName)), 90)).ToArray();
            }
            var rows = Scroll(panel, 24, 230, 1232, 490, false, Math.Max(490, (followed?.ChoiceCount ?? saved.Count) * 64));
            if (OnlineRoomPage)
            {
                var scroll = rows.parent.GetComponent<ScrollRect>();
                scroll.verticalNormalizedPosition = onlinePresetScroll;
                scroll.onValueChanged.AddListener(value =>
                {
                    if (Mathf.Abs(value.y - onlinePresetScroll) > .007f) PauseHostSetup();
                    onlinePresetScroll = value.y;
                });
            }
            if (followed != null)
            {
                for (int i = 0; i < followed.Choices.Length; i++)
                    PresetRow(followed.Choices[i], followed.Formations[i], followed.ChoiceStart + i, null);
                return;
            }
            for (int i = 0; i < saved.Count; i++)
            {
                var preset = saved[i];
                PresetRow(preset.name, string.Join("  /  ", preset.units.Select(UnitName)), i, () =>
                {
                    DuelMissionDefaults.ApplyOutsidePreset(rules, preset.units);
                    if (commit == null) EditLobbyRules(rules); else commit(rules);
                    dismiss();
                });
            }

            void PresetRow(string title, string units, int index, Action load)
            {
                float y = index * 64;
                Label(rows, title, 8, y + 8, 380, 17, OzUi.Ink);
                Label(rows, units, 8, y + 30, 920, 16, OzUi.Muted);
                Button(rows, "LOAD", 990, y + 5, 205, load, false, editable && load != null);
            }
        }

        void BuildLobbyRules(DuelRules rules, bool editable)
        {
            if (lobbyDialog == LobbyDialog.Rename) { BuildRename(); return; }
            if (lobbyDialog == LobbyDialog.Proposal) { BuildProposalReview(); return; }
            if (lobbyDialog == LobbyDialog.ArmyPresets) { BuildArmyPresets(rules, editable); return; }
            if (lobbyDialog == LobbyDialog.AiHelp) { BuildAiHelp(rules, editable); return; }
            var shade = Hud.Img(root, "LobbyRulesShade", new Color(0, 0, 0, .94f)); shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel("ALLOWED UNITS", 220, 155, 1480, 790);
            var background = Hud.Img(panel, "LobbyRulesBackground", OzUi.GM950); background.rectTransform.Fill(); background.transform.SetAsFirstSibling(); background.raycastTarget = true;
            Button(panel, "CLOSE", 1260, 12, 190, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            Label(panel, editable ? R.IsHost ? "HOST RULES · changes clear both READY confirmations" : "YOUR PROPOSAL · edits stay local until you send them" : "CURRENT HOST RULES · use PREPARE SETTINGS to propose changes", 24, 58, 1390, 18, OzUi.Muted);
            BuildAllowedUnits(panel, rules, editable);
        }

        void BuildAiHelp(DuelRules rules, bool editable)
        {
            var panel = LobbyPopup("AI HELP · PLAYER ADVANTAGE", 490);
            Label(panel, rules.HandicapEnabled ? "Applies to " + (R.View.NameForTeam(rules.HandicapSlot) ?? (rules.HandicapSlot == 0 ? "RED" : "BLUE")) + "."
                : "Select a player under Player Advantage to activate AI Help.", 30, 71, 1380, 20, OzUi.Ink);
            Check("Manage idle units · 15 s", rules.AiIdleSeconds == 15, 30, 112, 690,
                () => rules.AiIdleSeconds = rules.AiIdleSeconds == 15 ? 0 : 15);
            Check("Manage idle units · 30 s", rules.AiIdleSeconds == 30, 740, 112, 690,
                () => rules.AiIdleSeconds = rules.AiIdleSeconds == 30 ? 0 : 30);
            Label(panel, "Warns 5 s before giving an order. Pauses after the order for 10 s or 20 s, respectively.", 30, 170, 1380, 18, OzUi.Muted);
            Check("Manage constructions to avoid building grunts, jeeps or gatlings", rules.AiManageProduction, 30, 213, 1400,
                () => rules.AiManageProduction = !rules.AiManageProduction);
            Label(panel, "On enemy capture, replaces a weak build with the AI's preferred unit. Clears the queue.", 30, 271, 1380, 18, OzUi.Muted);
            Check("Cancel constructions when factories are lost", rules.AiCancelProduction, 30, 314, 1400,
                () => rules.AiCancelProduction = !rules.AiCancelProduction);
            Label(panel, "Stops production and clears the queue on enemy capture. Takes priority over replacement.", 30, 372, 1380, 18, OzUi.Muted);

            void Check(string name, bool selected, int x, int y, int width, Action toggle)
            {
                Button(panel, (selected ? "[X]  " : "[ ]  ") + name, x, y, width,
                    () => { toggle(); EditLobbyRules(rules); }, selected, editable);
            }
        }

        void BuildRename()
        {
            var shade = Hud.Img(root, "Change name shade", new Color(0, 0, 0, .94f));
            shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel("CHANGE YOUR NAME", 630, 350, 660, 285);
            Button(panel, "CANCEL", 428, 210, 202, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            Input(panel, "PLAYER NAME", 28, 119, 602, nameDraft, value => nameDraft = value, 32);
            Label(panel, renameStatus, 28, 181, 602, 15, OzUi.AlertText);
            Button(panel, "SAVE NAME", 28, 210, 245, () =>
            {
                try
                {
                    var next = DuelLobby.NormalizeName(nameDraft);
                    if (R.RenameLocalPlayer(next))
                    {
                        name = next;
                        SaveName();
                        lobbyDialog = LobbyDialog.None;
                    }
                    else renameStatus = "You can change your name while the lobby is open.";
                }
                catch (ArgumentException) { renameStatus = "Enter a name with 1 to 32 characters."; }
                dirty = true;
            }, true);
        }
        void BuildAllowedUnits(RectTransform panel, DuelRules rules, bool editable)
        {
            OzAllowedUnitsPanel.Build(panel,rules,editable,()=>EditLobbyRules(rules),allowedScroll,p=>allowedScroll=p,false);
        }
        static void AddLobbyTip(RectTransform target, OzText label, string text)
        {
            target.gameObject.AddComponent<DuelLobbyTooltip>().Configure(label, text);
        }

        RectTransform BuildCombatProfileChoices(RectTransform panel, int x, int y, int width,
            string tanks, string robots, bool editable, Action<OzBehaviorLibrary.CombatProfile, int> save)
        {
            bool native = tanks == "native" && robots == "native";
            bool classic = tanks == "zod-classic" && robots == "zod-classic";
            var profiles = OzBehaviorLibrary.CustomProfiles();
            var current = profiles.Find(profile => profile.Matches(tanks, robots));
            int buttonWidth = (width - 16) / 3;
            int customX = x + 2 * (buttonWidth + 8);
            bool guestView = OnlineRoomPage && R.HostedRoom == null && !R.EndLobbyOpen;
            Label(panel, "DYNAMICS PROFILE", x, y, width, 19, OzUi.Amber);
            Button(panel, "OPENZ", x, y + 40, buttonWidth, () => Choose(OzBehaviorLibrary.Standard(false), 0), native, editable, 40, showSelection: true);
            var classicButton = Button(panel, "Z CLASSIC", x + buttonWidth + 8, y + 40, buttonWidth, () => Choose(OzBehaviorLibrary.Standard(true), 1),
                classic, editable, 40, showSelection: true);
            OzBehaviorLibrary.ExperimentalIcon(classicButton, 12, 10, classic ? OzUi.GM950 : OzUi.Ink);
            bool canChooseCustom = editable && profiles.Count > 0;
            bool canOpenCustom = canChooseCustom || guestView;
            var custom = Button(panel, guestView ? "VIEW CUSTOM" : "CUSTOM", customX, y + 40, buttonWidth,
                () => { PauseHostSetup(); combatDropdown = !combatDropdown; onlineProfileScroll = 1; dirty = true; },
                !native && !classic, canOpenCustom, 40);
            if (canOpenCustom) Label(custom, "v", buttonWidth - 20, 20, 17, 12, !native && !classic ? OzUi.GM950 : OzUi.Ink);
            string description = combatDropdown && canChooseCustom ? "Choose a saved Unit Editor profile." :
                native ? "Standard OpenZ combat behavior." :
                classic ? "Experimental: Z95-inspired dodging, speed boosts and combat movement." :
                current == null ? "The lobby uses a custom combat profile." : "Saved combat profile: " + current.Name;
            Label(panel, description, x, y + 95, width, 15, OzUi.Muted);
            if (!combatDropdown || !canOpenCustom) return null;
            if (guestView)
            {
                var followed = FollowingHost ? displayedSetup : null;
                if (followed?.Choices == null || followed.Choices.Length == 0)
                {
                    var summary = Hud.Img(panel, "Read-only profile", OzUi.GM950);
                    summary.rectTransform.TL(x, y + 114, width, 42);
                    Label(summary.transform, !native && !classic ? "CUSTOM PROFILE ACTIVE" : "NO CUSTOM PROFILE ACTIVE",
                        10, 7, width - 20, 15, OzUi.Muted);
                    return summary.rectTransform;
                }
                var menu = BuildProfileList(followed.ChoiceCount);
                for (int i = 0; i < followed.Choices.Length; i++)
                    Button(menu, followed.Choices[i], 5, 5 + (followed.ChoiceStart + i) * 42, width - 10, null, enabled: false, height: 38);
                return menu.parent as RectTransform;
            }
            if (OnlineRoomPage)
                setupProfileNames = profiles.Take(4096).Select(profile => SetupChoiceLabel(profile.Name, 50)).ToArray();
            // The dropdown spans both columns so saved names have room even at the smaller lobby width.
            var list = BuildProfileList(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                Button(list, profile.Name, 5, 5 + i * 42, width - 10, () => Choose(profile, 2), profile == current, height: 38);
            }
            return list.parent as RectTransform;

            RectTransform BuildProfileList(int count)
            {
                float contentHeight = count * 42 + 10;
                var content = Scroll(panel, x, y + 114, width, Math.Min(222, contentHeight), false, contentHeight);
                if (OnlineRoomPage)
                {
                    var scroll = content.parent.GetComponent<ScrollRect>();
                    scroll.verticalNormalizedPosition = onlineProfileScroll;
                    scroll.onValueChanged.AddListener(value =>
                    {
                        if (Mathf.Abs(value.y - onlineProfileScroll) > .007f) PauseHostSetup();
                        onlineProfileScroll = value.y;
                    });
                }
                return content;
            }

            void Choose(OzBehaviorLibrary.CombatProfile profile, int kind)
            {
                combatDropdown = false;
                dirty = true;
                save(profile, kind);
            }
        }

        bool TryEncodeCombatProfile(OzBehaviorLibrary.CombatProfile profile, int kind, out string tanks, out string robots)
        {
            tanks = kind == 2 ? profile.Tanks.Encode() : "";
            robots = kind == 2 ? profile.Robots.Encode() : "";
            if (SkirmishRules.ValidDynamics(tanks, ZBehaviorFamily.Tanks) && SkirmishRules.ValidDynamics(robots, ZBehaviorFamily.Robots))
                return true;
            R.ReportError("This combat profile is too large for a Skirmish lobby.");
            return false;
        }
    }
}
