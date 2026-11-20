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
        enum LobbyDialog { None, Units, Dynamics, Forts, Proposal, OpenZMaps, ArmyPresets, Rename, BattleOptions, AiHelp }
        LobbyDialog lobbyDialog;
        ZBehaviorFamily dynamicsFamily;
        bool customDynamicsOnly;
        int dynamicsType;
        float allowedScroll = 1, dynamicsScroll = 1;
        string openZMapId, presetName, presetStatus;
        string nameDraft, renameStatus;
        OzLevelPreview openZMapPreview;
        RawImage openZMapImage;
        public const string ArmyPresetsPreference = "oz_duel_outside_army_presets";

        [Serializable] sealed class ArmyPreset { public string name; public string[] units; }
        [Serializable] sealed class ArmyPresets { public List<ArmyPreset> entries = new List<ArmyPreset>(); }

        void ApplyLobbyMap(DuelRules rules, int revision, string id)
        {
            if (R.View == null || R.View.Revision != revision || R.View.Phase != (int)DuelPhase.Lobby || rules.Map == id) return;
            // Keep edits made to a guest draft while a separate map gallery was open.
            rules = (lobbyDraft ?? rules).Copy();
            rules.Map = id;
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
            if (openZMapPreview?.Level != selected.Id)
            {
                ReleaseOpenZMapPreview();
                openZMapPreview = OzLevelPreview.Create(transform, selected.Id);
                openZMapPreview.PlayFrom(OzLevelPreview.PosterTime, true);
            }
            openZMapImage = Hud.Node("OpenZ flyby", panel).gameObject.AddComponent<RawImage>();
            openZMapImage.rectTransform.TL(466, 102, 1068, 600);
            openZMapImage.raycastTarget = false;
            Label(panel, selected.Name + " · " + selected.Mood, 466, 724, 735, 21, OzUi.Ink);
            Button(panel, "APPLY MAP", 1224, 715, 310, () =>
            {
                ApplyLobbyMap(rules, revision, selected.Id);
                lobbyDialog = LobbyDialog.None;
                dirty = true;
            }, true, editable);
        }

        void TickOpenZMapPreview()
        {
            if (!openZMapImage || openZMapPreview == null) return;
            OzLevelPreview.PumpPrepares();
            var texture = openZMapPreview.Sharp;
            openZMapImage.texture = texture;
            openZMapImage.enabled = texture;
            if (!texture) return;
            var cover = OzLevelPreview.Cover((float)texture.width / texture.height, 1068f / 600);
            openZMapImage.uvRect = openZMapPreview.VideoOk ? cover : OzLevelPreview.KenBurns(cover, OzMotion.Now, 0);
        }

        void ReleaseOpenZMapPreview()
        {
            if (openZMapPreview == null) return;
            var video = openZMapPreview.Player;
            openZMapPreview.Release();
            if (video) Destroy(video.gameObject);
            openZMapPreview = null;
        }

        static ArmyPresets ReadArmyPresets()
        {
            var json = PlayerPrefs.GetString(ArmyPresetsPreference, "");
            if (json.Length == 0) return new ArmyPresets();
            try { return JsonUtility.FromJson<ArmyPresets>(json) ?? new ArmyPresets(); }
            catch (ArgumentException) { return new ArmyPresets(); }
        }

        void BuildArmyPresets(DuelRules rules, bool editable)
        {
            var shade = Hud.Img(root, "Army presets shade", new Color(0, 0, 0, .94f));
            shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel("OUTSIDE THE FORT · PRESETS", 320, 165, 1280, 750);
            Button(panel, "CLOSE", 1030, 12, 225, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            Label(panel, "Load a four-slot formation for both teams. Units disabled by the lobby roster leave their slots empty.", 24, 70, 1220, 17, OzUi.Muted);
            Label(panel, "SAVE CURRENT FOUR SLOTS", 24, 123, 470, 17, OzUi.Ink);
            Input(panel, "", 24, 155, 430, presetName, value => presetName = value, 32);
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
            });
            Label(panel, presetStatus ?? "", 680, 167, 570, 16, OzUi.Amber);
            var saved = ReadArmyPresets().entries?.Where(p => p != null && !string.IsNullOrWhiteSpace(p.name) &&
                !p.name.Contains("<") && !p.name.Contains(">") && DuelMissionDefaults.IsOutsidePreset(p.units)).ToList() ?? new List<ArmyPreset>();
            saved.Insert(0, new ArmyPreset { name = "OpenZ-VK default", units = DuelMissionDefaults.OutsideUnits() });
            saved.Insert(1, new ArmyPreset { name = "Light and heavy assault", units = new[] { "light", "heavy", "pyro", "laser" } });
            var rows = Scroll(panel, 24, 230, 1232, 490, false, Math.Max(490, saved.Count * 64));
            for (int i = 0; i < saved.Count; i++)
            {
                var preset = saved[i];
                float y = i * 64;
                Label(rows, preset.name, 8, y + 8, 380, 17, OzUi.Ink);
                Label(rows, string.Join("  /  ", preset.units.Select(UnitName)), 8, y + 30, 920, 16, OzUi.Muted);
                Button(rows, "LOAD", 990, y + 5, 205, () =>
                {
                    DuelMissionDefaults.ApplyOutsidePreset(rules, preset.units);
                    EditLobbyRules(rules);
                    lobbyDialog = LobbyDialog.None;
                    dirty = true;
                }, false, editable);
            }
        }

        void BuildLobbyRules(DuelRules rules, bool editable)
        {
            if (lobbyDialog == LobbyDialog.Rename) { BuildRename(); return; }
            if (lobbyDialog == LobbyDialog.Forts) { BuildFortPlacement(rules, editable); return; }
            if (lobbyDialog == LobbyDialog.Proposal) { BuildProposalReview(); return; }
            if (lobbyDialog == LobbyDialog.ArmyPresets) { BuildArmyPresets(rules, editable); return; }
            if (lobbyDialog == LobbyDialog.BattleOptions) { BuildBattleOptions(rules, editable); return; }
            if (lobbyDialog == LobbyDialog.AiHelp) { BuildAiHelp(rules, editable); return; }
            var shade = Hud.Img(root, "LobbyRulesShade", new Color(0, 0, 0, .94f)); shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel(lobbyDialog == LobbyDialog.Units ? "ALLOWED UNITS" : "SERVER DYNAMICS", 220, 155, 1480, 790);
            var background = Hud.Img(panel, "LobbyRulesBackground", OzUi.GM950); background.rectTransform.Fill(); background.transform.SetAsFirstSibling(); background.raycastTarget = true;
            Button(panel, "CLOSE", 1260, 12, 190, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            Label(panel, editable ? R.IsHost ? "HOST RULES · changes clear both READY confirmations" : "YOUR PROPOSAL · edits stay local until you send them" : "CURRENT HOST RULES · use PREPARE SETTINGS to propose changes", 24, 58, 1390, 18, OzUi.Muted);
            if (lobbyDialog == LobbyDialog.Units) BuildAllowedUnits(panel, rules, editable);
            else BuildDynamics(panel, rules, editable);
        }

        void BuildBattleOptions(DuelRules rules, bool editable)
        {
            var panel = LobbyPopup("BATTLE OPTIONS", 340);
            var oil = Button(panel, OzBattleOptions.DamageLabel + " · " + (rules.TankDamageEffects ? "ON" : "OFF"), 30, 110, 685,
                () => { rules.TankDamageEffects = !rules.TankDamageEffects; EditLobbyRules(rules); }, rules.TankDamageEffects, editable);
            var shake = Button(panel, OzBattleOptions.ShakeLabel + " · " + (rules.TankDamageShake ? "ON" : "OFF"), 730, 110, 685,
                () => { rules.TankDamageShake = !rules.TankDamageShake; EditLobbyRules(rules); }, rules.TankDamageShake, editable);
            var help = Label(panel, "", 30, 270, 1380, 19, OzUi.Muted);
            AddLobbyTip(oil, help, OzBattleOptions.DamageTip);
            AddLobbyTip(shake, help, OzBattleOptions.ShakeTip);
            Label(panel, "WHEN YOUR LAST RADAR IS LOST", 30, 179, 1350, 21, OzUi.Ink);
            for (int i = (int)RadarLossRule.Off; i <= (int)RadarLossRule.FogAndZoom; i++)
            {
                var option = (RadarLossRule)i;
                var choice = Button(panel, OzBattleOptions.RadarName(option), 30 + i * 350, 210, 335,
                    () => { rules.RadarLoss = option; EditLobbyRules(rules); }, rules.RadarLoss == option, editable);
                AddLobbyTip(choice, help, OzBattleOptions.RadarTip(option));
            }
        }

        void BuildAiHelp(DuelRules rules, bool editable)
        {
            var panel = LobbyPopup("AI HELP · HANDICAP", 490);
            Label(panel, rules.HandicapEnabled ? "Applies to " + (R.View.NameForTeam(rules.HandicapSlot) ?? (rules.HandicapSlot == 0 ? "RED" : "BLUE")) + "."
                : "Select a handicap player in the lobby to activate AI Help.", 30, 71, 1380, 20, OzUi.Ink);
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
        void BuildLobbyDynamics(DuelRules rules, bool editable)
        {
            var tanks = ZWorld.DuelProfile(rules.TankDynamics, ZBehaviorFamily.Tanks);
            var robots = ZWorld.DuelProfile(rules.RobotDynamics, ZBehaviorFamily.Robots);
            bool classic = tanks.id == "zod-classic" && robots.id == "zod-classic";
            bool native = tanks.id == "native" && robots.id == "native";
            var toggle = Button(root, classic ? "Z CLASSIC DYNAMICS" : native ? "OPENZ DYNAMICS" : "CUSTOM DYNAMICS", 845, 40, 235, () =>
            {
                rules.TankDynamics = classic ? "" : ZBehaviorProfile.Classic(ZBehaviorFamily.Tanks).Encode();
                rules.RobotDynamics = classic ? "" : ZBehaviorProfile.Classic(ZBehaviorFamily.Robots).Encode();
                EditLobbyRules(rules);
            }, classic, editable);
            var help = Label(root, "", 845, 173, 980, 15, OzUi.Muted);
            AddLobbyTip(toggle, help, "OpenZ uses its standard combat. Z Classic brings back classic dodges and gives lighter vehicles quicker escapes.");
            var custom = Button(root, "CUSTOM", 1090, 40, 105, () =>
            {
                dynamicsFamily = OzBehaviorLibrary.SavedProfiles.Any(p => p.family == ZBehaviorFamily.Tanks) ? ZBehaviorFamily.Tanks : ZBehaviorFamily.Robots;
                dynamicsType = 0;
                customDynamicsOnly = true;
                lobbyDialog = LobbyDialog.Dynamics;
                pickerGroup = -1;
                dirty = true;
            }, !native && !classic, editable && OzBehaviorLibrary.SavedProfiles.Count > 0);
            AddLobbyTip(custom, help, "Choose saved tank and robot profiles from the Unit Editor.");
            Button(root, "DYNAMICS", 1595, 91, 235, () =>
            {
                customDynamicsOnly = false;
                lobbyDialog = LobbyDialog.Dynamics;
                pickerGroup = -1;
                dirty = true;
            }, false, true, 37);
        }

        static void AddLobbyTip(RectTransform target, OzText label, string text)
        {
            var trigger = target.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => label.Set(text));
            trigger.triggers.Add(enter);
            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => label.Set(""));
            trigger.triggers.Add(exit);
        }

        void BuildDynamics(RectTransform panel, DuelRules rules, bool editable)
        {
            Button(panel, "TANKS", 24, 92, 340, () => { dynamicsFamily = ZBehaviorFamily.Tanks; dynamicsType = 0; dynamicsScroll = 1; dirty = true; }, dynamicsFamily == ZBehaviorFamily.Tanks);
            Button(panel, "ROBOTS", 384, 92, 340, () => { dynamicsFamily = ZBehaviorFamily.Robots; dynamicsType = 0; dynamicsScroll = 1; dirty = true; }, dynamicsFamily == ZBehaviorFamily.Robots);
            var profile = ZWorld.DuelProfile(dynamicsFamily == ZBehaviorFamily.Tanks ? rules.TankDynamics : rules.RobotDynamics, dynamicsFamily);
            Label(panel, "FIXED FOR THE MATCH · " + profile.name, 24, 153, 1400, 22, OzUi.Amber);
            Label(panel, "Prepare custom profiles in Unit Editor > Dynamics and SAVE AS. Choose them here before READY. The pause menu cannot change them.", 24, 188, 1400, 16, OzUi.Muted);
            if (editable)
            {
                var choices = customDynamicsOnly
                    ? OzBehaviorLibrary.SavedProfiles.Where(p => p.family == dynamicsFamily).ToList()
                    : OzBehaviorLibrary.Choices(dynamicsFamily);
                if (choices.Count == 0) Label(panel, "No custom profiles for this group.", 24, 250, 440, 17, OzUi.Muted);
                var content = Scroll(panel, 24, 235, 440, 520, false, Math.Max(520, choices.Count * 58));
                for (int index = 0; index < choices.Count; index++)
                {
                    var next = choices[index];
                    Button(content, next.name, 0, index * 58, 420, () =>
                    {
                        var frozen = next.Clone(); frozen.FreezeReferences();
                        if (dynamicsFamily == ZBehaviorFamily.Tanks) rules.TankDynamics = frozen.Encode(); else rules.RobotDynamics = frozen.Encode();
                        EditLobbyRules(rules);
                    }, profile.id == next.id);
                }
            }
            else Label(panel, "The selected server profile is shown on the right. Your personal saved profiles do not affect this match.", 24, 250, 410, 18, OzUi.Muted).Body(24, 250, 410);
            var types = dynamicsFamily == ZBehaviorFamily.Tanks ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes;
            dynamicsType = Math.Min(dynamicsType, types.Length - 1);
            string type = types[dynamicsType];
            Button(panel, "INSPECT: " + UnitName(type), 500, 235, 930, () => { dynamicsType = (dynamicsType + 1) % types.Length; dynamicsScroll = 1; dirty = true; });
            var values = Scroll(panel, 500, 295, 930, 460, false, ZBehaviorSchema.All.Length * 46);
            var valueScroll = values.parent.GetComponent<ScrollRect>(); valueScroll.verticalNormalizedPosition = dynamicsScroll;
            valueScroll.onValueChanged.AddListener(p => dynamicsScroll = p.y);
            int row = 0;
            foreach (var definition in ZBehaviorSchema.All)
            {
                Label(values, definition.Label, 12, row * 46 + 3, 700, 16, OzUi.Ink);
                Label(values, definition.Format(profile.Get(type, definition.Key)), 710, row * 46 + 3, 200, 16, OzUi.Amber);
                row++;
            }
        }
    }
}
