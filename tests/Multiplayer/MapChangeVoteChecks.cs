using System;
using OpenZ.Multiplayer;

static class MapChangeVoteChecks
{
    public static void Run(Action<bool, string> check)
    {
        var even = new MapChangeVote(new[] { 0, 1, 2, 3 }, 100);
        check(even.Cast(0, MapChangeBallot.Veto, 101) == MapChangeResult.Pending, "One of four votes is not half");
        check(even.Cast(0, MapChangeBallot.Veto, 102) == MapChangeResult.Pending && even.VetoCount == 1, "Duplicate map ballots do not count");
        check(even.Cast(4, MapChangeBallot.Veto, 103) == MapChangeResult.Pending && even.VetoCount == 1, "Ineligible seat did not vote");
        check(even.Cast(1, MapChangeBallot.Go, 104) == MapChangeResult.Pending, "One Go and one Veto leaves the vote open");
        check(even.Cast(2, MapChangeBallot.Veto, 105) == MapChangeResult.Refuse, "First half of an even electorate wins");
        check(even.Cast(3, MapChangeBallot.Go, 106) == MapChangeResult.Refuse && even.GoCount == 1, "Resolved vote cannot be overturned");

        var odd = new MapChangeVote(new[] { 0, 3, 6 }, 0);
        check(odd.Cast(0, MapChangeBallot.Go, 0) == MapChangeResult.Pending, "One of three votes is not a majority");
        check(odd.Cast(3, MapChangeBallot.Go, 10) == MapChangeResult.Apply, "Two of three Go votes apply immediately");
        var timeout = new MapChangeVote(new[] { 0, 3, 6 }, 10);
        timeout.Cast(0, MapChangeBallot.Veto, 15);
        check(timeout.Advance(39.99) == MapChangeResult.Pending, "Map vote lasts 30 seconds");
        check(timeout.Cast(3, MapChangeBallot.Veto, 40) == MapChangeResult.Apply && timeout.VetoCount == 1, "Timeout wins over ballots received at the deadline");
        check(new MapChangeVote(new[] { 0 }, 10).Advance(40) == MapChangeResult.Apply, "An unopposed map vote applies at timeout");
    }
}
