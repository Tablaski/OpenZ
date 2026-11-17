using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        enum LobbyDialog { None, Units, Dynamics, Forts, Proposal }
        LobbyDialog lobbyDialog;
        ZBehaviorFamily dynamicsFamily;
        int dynamicsType;
        float allowedScroll = 1, dynamicsScroll = 1;

        void BuildLobbyRules(DuelRules rules, bool editable)
        {
            if (lobbyDialog == LobbyDialog.Forts) { BuildFortPlacement(rules, editable); return; }
            if (lobbyDialog == LobbyDialog.Proposal) { BuildProposalReview(); return; }
            var shade = Hud.Img(root, "LobbyRulesShade", new Color(0, 0, 0, .94f)); shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel(lobbyDialog == LobbyDialog.Units ? "ALLOWED UNITS" : "SERVER DYNAMICS", 220, 155, 1480, 790);
            var background = Hud.Img(panel, "LobbyRulesBackground", OzUi.GM950); background.rectTransform.Fill(); background.transform.SetAsFirstSibling(); background.raycastTarget = true;
            Button(panel, "CLOSE", 1260, 12, 190, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            Label(panel, editable ? R.IsHost ? "HOST RULES · changes clear both READY confirmations" : "YOUR PROPOSAL · edits stay local until you send them" : "CURRENT HOST RULES · use PREPARE SETTINGS to propose changes", 24, 58, 1390, 18, OzUi.Muted);
            if (lobbyDialog == LobbyDialog.Units) BuildAllowedUnits(panel, rules, editable);
            else BuildDynamics(panel, rules, editable);
        }
        void BuildAllowedUnits(RectTransform panel, DuelRules rules, bool editable)
        {
            OzAllowedUnitsPanel.Build(panel,rules,editable,()=>EditLobbyRules(rules),allowedScroll,p=>allowedScroll=p,false);
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
                var choices = OzBehaviorLibrary.Choices(dynamicsFamily);
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
