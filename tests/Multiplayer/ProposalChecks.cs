using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class ProposalChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var lobby = new DuelLobby(); lobby.Join(0, "Host"); lobby.Join(1, "Guest");
        lobby.SetReady(0, lobby.Revision, true);
        int revision = lobby.Revision;
        var proposed = lobby.Rules; proposed.Map = "L07"; proposed.SetFortTurret(1, 3, "gatling");
        check(lobby.Propose(1, revision, proposed), "Guest submits one grouped proposal");
        check(lobby.Rules.Map == "L02" && lobby.Ready(0) && lobby.Revision == revision, "Proposal cannot change official rules, revision or readiness");
        int first = lobby.ProposalId;
        proposed.Map = "L16";
        check(lobby.Proposal.Map == "L07", "Proposal owns its input snapshot");
        var leaked = lobby.Proposal; leaked.BlueFortTurrets[3] = "gun";
        check(lobby.Proposal.BlueFortTurrets[3] == "gatling", "Proposal inspection cannot mutate stored mounts");
        check(lobby.Propose(1, revision, proposed) && lobby.ProposalId != first, "New proposal replaces previous proposal");
        check(!lobby.ResolveProposal(0, first, true) && lobby.Rules.Map == "L02", "An old ACCEPT click cannot accept a replacement");
        reject(() => lobby.ResolveProposal(1, lobby.ProposalId, true), "Guest cannot accept proposals");
        check(lobby.ResolveProposal(0, lobby.ProposalId, true), "Host accepts current proposal");
        check(lobby.Rules.Map == "L16" && lobby.Rules.EffectiveFor(1).FortTurrets[3] == "gatling" && !lobby.Ready(0) && lobby.Revision == revision + 1 && lobby.Proposal == null,
            "Acceptance applies whole proposal atomically and invalidates readiness");
        check(!lobby.Propose(1, revision, proposed), "Late proposal cannot overwrite newer official rules");
        proposed = lobby.Rules; proposed.FortLoserMaximum = .3;
        lobby.Propose(1, lobby.Revision, proposed); int beforeDecline = lobby.Revision;
        lobby.ResolveProposal(0, lobby.ProposalId, false);
        check(lobby.Proposal == null && lobby.Revision == beforeDecline && lobby.Rules.FortLoserMaximum == 0, "Decline keeps rules and revision unchanged");
        lobby.Propose(1, lobby.Revision, proposed); lobby.ChangeRules(0, lobby.Rules);
        check(lobby.Proposal == null, "Host edit automatically expires pending proposals");
        check(!lobby.Propose(1, lobby.Revision, lobby.Rules), "No-op proposals are ignored");
        lobby.Propose(1, lobby.Revision, proposed); lobby.CancelProposal(1);
        check(lobby.Proposal == null, "Guest can withdraw without changing readiness");
        var bad = lobby.Rules; bad.BlueFortTurrets = new[] { "gun" };
        reject(() => lobby.Propose(1, lobby.Revision, bad), "Proposal rejects malformed mount counts");
        bad.BlueFortTurrets = new[] { "light", "", "", "" };
        reject(bad.Validate, "Team mounts reject vehicles");
        var mounts = new DuelRules(); mounts.Common.FortTurrets[0] = "howitzer";
        check(mounts.EffectiveFor(0).FortTurrets[0] == "howitzer" && mounts.EffectiveFor(1).FortTurrets[0] == "howitzer", "Legacy shared mounts remain compatible");
        mounts.SetFortTurret(0, 1, "gun"); mounts.SetFortTurret(1, 2, "gatling");
        check(mounts.EffectiveFor(0).FortTurrets[2] == "" && mounts.EffectiveFor(1).FortTurrets[1] == "", "Team mount edits stay independent");
        mounts.SetUnitAllowed("gun", false); mounts.Validate();
        check(mounts.RedFortTurrets[1] == "", "Roster exclusion clears independent team mounts");
        check(DuelRuleChanges.Describe(mounts, mounts.Copy()).Count == 0, "Unchanged team mounts produce no false differences");
        lobby.SetReady(0, lobby.Revision, true); lobby.SetReady(1, lobby.Revision, true);
        reject(() => lobby.Propose(1, lobby.Revision, proposed), "Proposals are frozen when loading starts");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        foreach (string map in new[] { "L02", "L07", "L16", "L17" })
        {
            var configured = new DuelRules { Map = map };
            for (int i = 0; i < 4; i++) { configured.SetFortTurret(0, i, DuelRules.AllowedFortTurrets[i]); configured.SetFortTurret(1, i, DuelRules.AllowedFortTurrets[3 - i]); }
            var world = ZWorld.Create(settings, File.ReadAllText(Path.Combine(root, "Maps", map + "_grid.json")), 672);
            world.ConfigureDuel(configured, false); world.DuelApplyStartingArmies();
            for (int seat = 0; seat < 2; seat++)
            {
                var team = seat == 0 ? Team.Red : Team.Blue;
                var fort = world.Objects.First(o => o.IsFort && o.Owner == team);
                for (int i = 0; i < 4; i++)
                {
                    int x = fort.LocX + (i % 2 == 0 ? 16 : 112), y = fort.LocY + (i < 2 ? 0 : 48);
                    check(world.Objects.Any(o => o.Owner == team && o.Type == configured.FortTurretsFor(seat)[i] && o.LocX == x && o.LocY == y && !o.Ejectable), map + ": exact team-specific mount " + seat + ":" + i);
                }
            }
        }
    }
}
