using System;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        DuelRules lobbyDraft;
        int draftRevision;
        int lastProposalWait;

        void EditLobbyRules(DuelRules rules)
        {
            if (R.IsHost) R.ChangeRules(rules);
            else if (lobbyDraft != null && draftRevision == R.View.Revision) { lobbyDraft = rules; dirty = true; }
        }
        void StartLobbyDraft()
        {
            lobbyDraft = (R.View.ProposedRules ?? R.Rules).Copy(); draftRevision = R.View.Revision;
            pickerGroup = -1; lobbyDialog = LobbyDialog.None; dirty = true;
        }
        void BuildProposalBar(DuelRules official)
        {
            if (R.View.Phase != (int)DuelPhase.Lobby) return;
            bool pending = !string.IsNullOrEmpty(R.View.ProposalJson);
            if (R.IsHost)
            {
                if (!pending) { Label(root, "Your changes are shared immediately. Players can send one grouped proposal.", 80, 145, 1700, 17, OzUi.Muted); return; }
                int id = R.View.ProposalId;
                Label(root, R.View.GuestName + " proposes " + DuelRuleChanges.Describe(official, R.View.ProposedRules).Count + " changes", 80, 145, 820, 19, OzUi.Amber);
                Button(root, "REVIEW", 930, 130, 260, () => { lobbyDialog = LobbyDialog.Proposal; dirty = true; });
                Button(root, "ACCEPT", 1210, 130, 290, () => R.ResolveProposal(id, true), true);
                Button(root, "DECLINE", 1520, 130, 310, () => R.ResolveProposal(id, false));
                return;
            }
            if (lobbyDraft == null)
            {
                Label(root, pending ? "Your proposal is waiting for the host." : "Host settings update live. Prepare a single proposal to suggest changes.", 80, 145, 1080, 17, OzUi.Muted);
                Button(root, pending ? "REPLACE PROPOSAL" : "PREPARE SETTINGS", 1190, 130, 390, StartLobbyDraft);
                if (pending) Button(root, "WITHDRAW", 1600, 130, 230, R.WithdrawProposal);
                return;
            }
            bool stale = draftRevision != R.View.Revision;
            int count = DuelRuleChanges.Describe(official, lobbyDraft).Count;
            Label(root, stale ? "Host settings changed. Refresh to prepare a new proposal." : "YOUR DRAFT · " + count + " changes · nothing sent yet", 80, 145, 1050, 18, stale ? OzUi.AlertText : OzUi.Amber);
            Button(root, stale ? "REFRESH SETTINGS" : R.ProposalWait > 0 ? "WAIT " + Math.Ceiling(R.ProposalWait) + " S" : "SEND PROPOSAL", 1160, 130, 410, () =>
            {
                if (stale) StartLobbyDraft();
                else if (R.ProposalWait <= 0)
                {
                    R.ProposeRules(lobbyDraft, draftRevision); lobbyDraft = null;
                    pickerGroup = -1; lobbyDialog = LobbyDialog.None; dirty = true;
                }
            }, true, stale || count > 0 && R.ProposalWait <= 0);
            Button(root, "CANCEL", 1600, 130, 230, () => { lobbyDraft = null; pickerGroup = -1; lobbyDialog = LobbyDialog.None; dirty = true; });
        }
        RectTransform LobbyPopup(string title, float height = 790)
        {
            var shade = Hud.Img(root, title + " Shade", new Color(0, 0, 0, .94f)); shade.rectTransform.Fill(); shade.raycastTarget = true;
            var panel = Panel(title, 220, (1100 - height) * .5f, 1480, height);
            var background = Hud.Img(panel, "Background", OzUi.GM950); background.rectTransform.Fill(); background.transform.SetAsFirstSibling(); background.raycastTarget = true;
            Button(panel, "CLOSE", 1260, 12, 190, () => { lobbyDialog = LobbyDialog.None; dirty = true; });
            return panel;
        }
        void BuildProposalReview()
        {
            var panel = LobbyPopup("PROPOSED SETTINGS");
            var proposed = R.View.ProposedRules;
            if (proposed == null) { Label(panel, "No proposal pending. The host may have changed the settings.", 24, 100, 1400, 20, OzUi.Muted); return; }
            int id = R.View.ProposalId;
            var changes = DuelRuleChanges.Describe(R.Rules, proposed);
            Label(panel, R.OpponentName + " proposes these rules. Accept applies them and clears READY. Decline keeps the current rules.", 24, 72, 1400, 18, OzUi.Muted);
            var content = Scroll(panel, 24, 120, 1432, 560, false, Math.Max(560, changes.Count * 40));
            for (int i = 0; i < changes.Count; i++) Label(content, changes[i], 12, i * 40 + 8, 1390, 18, OzUi.Ink);
            if (R.IsHost)
            {
                Button(panel, "ACCEPT ALL", 750, 710, 330, () => { R.ResolveProposal(id, true); lobbyDialog = LobbyDialog.None; dirty = true; }, true);
                Button(panel, "DECLINE", 1100, 710, 330, () => { R.ResolveProposal(id, false); lobbyDialog = LobbyDialog.None; dirty = true; });
            }
        }
    }
}
