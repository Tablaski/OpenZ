using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ
{
    // ============================================================================================================================
    // 1.2 items 25 / 25a: "play new lines like 'going to repairs', 'be right back', 'over and out'... when repair clicked, 15 new
    // lines about repairs" + "and same when pressed R" + "and same when clicked to go repair manually on repair station".
    //
    // A repair order (the R key, the wrench key over the command bar, the touch / gamepad REPAIR key, or a right-click on an own repair
    // station with damaged vehicles selected) makes ONE of the vehicles it sends say one of 15 repair lines, in the voice and colour of
    // that vehicle's own pack: the sound table events vo_order_x_<type>_repair (one take per line). AudioDirector.RepairLines.cs asks
    // this class which vehicles the order sent and which line comes next; the line goes through Alan's main channel like every unit
    // line (never over another robot line, the quiet gap after a line, the voice options). A refused repair order (no own station,
    // nothing damaged) sets no order marker, so it keeps its refusal blip and says no line.
    // Plain C# (no UnityEngine): the editor check simulates the picker with the same code.
    // ============================================================================================================================
    public static class OzRepairLines
    {
        /// <summary>Every vehicle type a repair station can repair (ZObject.CanBeRepaired: any vehicle) and that has a repair pool.</summary>
        public static readonly string[] Types = { "jeep", "light", "medium", ZMTank.Type, "heavy", "missile_launcher", "apc", "crane", "shuttle" };   // DV145: + the medium tank (its 15 lines: the mech's takes in its own voice colour)
        /// <summary>The lines of every pool, in the table's order.</summary>
        public static readonly string[] Lines =
        {
            "Going to repairs!", "Be right back!", "Over and out!", "Off to the shop!", "Patch me up!", "Spare parts, please!", "Tighten my bolts!",
            "Time for a tune-up!", "To the repair station!", "Limping home!", "Just a scratch, honest!", "Don't start without me!",
            "Save some for me!", "Fresh paint, coming up!", "Hammer out the dents!",
        };

        public static string EventFor(string type) => "vo_order_x_" + type + "_repair";
        public static bool Known(string type) => type != null && Array.IndexOf(Types, type) >= 0;

        /// <summary>The order just given sends this vehicle to a repair station (or onto it): its last order is a repair order (the R
        /// order and a right-click on the station give it as the only one; the sim may put a short order in front of it, never behind),
        /// or it is a shuttle flying to a station's deck (the shuttle has no driving waypoint: ShuttleRepairTarget; any other order
        /// calls that trip off). A vehicle still on an older trip that the new order did not change counts too: it is going to repairs.</summary>
        public static bool Sent(ZObject o)
        {
            if (o == null || o.Destroyed || o.Kind != ZKind.Vehicle) return false;
            var w = o.Waypoints;
            if (w.Count > 0 && w[w.Count - 1].mode == WpMode.UnitRepair) return true;
            return o.IsShuttle && o.ShuttleRepairTarget >= 0;
        }

        // ---- the picker: one history for all the repair pools (the same 15 words in every voice), so two vehicles sent one after the
        //      other never say the same line; a line counts as said when it STARTS
        public static int NoRepeat = 6;
        static readonly List<string> hist = new List<string>();
        public static int Said, Silent, Orders, Waited;

        public static void Reset() { hist.Clear(); Said = Silent = Orders = Waited = 0; }

        /// <summary>A random line of the pool that is not one of the last NoRepeat lines said; when every line is that recent, the least
        /// recently said one. null = empty pool.</summary>
        public static string Pick(IList<string> pool, Func<double> rnd)
        {
            int n = pool?.Count ?? 0;
            if (n == 0) return null;
            if (n == 1) return pool[0];
            var recent = new HashSet<string>();
            for (int i = hist.Count - 1; i >= 0 && recent.Count < NoRepeat; i--) recent.Add(hist[i]);
            var free = new List<string>(n);
            foreach (var l in pool) if (!recent.Contains(l)) free.Add(l);
            if (free.Count > 0)
            {
                int k = (int)(rnd() * free.Count); if (k >= free.Count) k = free.Count - 1;
                return free[k];
            }
            string best = pool[0]; int bestAt = int.MaxValue;
            foreach (var l in pool) { int at = hist.LastIndexOf(l); if (at < bestAt) { bestAt = at; best = l; } }
            return best;
        }

        /// <summary>The line STARTED: it enters the history.</summary>
        public static void Commit(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            hist.Add(line); if (hist.Count > 32) hist.RemoveAt(0);
            Said++;
        }

        public static string Last => hist.Count > 0 ? hist[hist.Count - 1] : null;

        public static string Summary() => $"repair orders {Orders}: {Said} lines said ({Waited} of them after waiting for the quiet gap), {Silent} kept silent (a newer order, the trip called off, the wait over, right-click spam or the voice options)";

        /// <summary>Editor check: the picker on a pool of `lines` for `orders` repair orders: never the line said just before, never one
        /// of the last NoRepeat while a fresher one is free, and every line comes up.</summary>
        public static string Simulate(IList<string> pool, int seed, int orders, out bool pass)
        {
            var saved = new List<string>(hist); int s0 = Said, q0 = Silent, o0 = Orders, w0 = Waited;
            hist.Clear();
            var rng = new Random(seed); int twice = 0, recentPicked = 0; var seen = new HashSet<string>();
            for (int i = 0; i < orders; i++)
            {
                var recent = new HashSet<string>();
                for (int k = hist.Count - 1; k >= 0 && recent.Count < NoRepeat; k--) recent.Add(hist[k]);
                bool anyFree = false; foreach (var l in pool) if (!recent.Contains(l)) anyFree = true;
                string prev = hist.Count > 0 ? hist[hist.Count - 1] : null;               // the line said last
                var line = Pick(pool, () => rng.NextDouble());
                if (line == prev) twice++;
                if (anyFree && recent.Contains(line)) recentPicked++;
                seen.Add(line);
                if (rng.NextDouble() < 0.8) Commit(line);                          // a line that was asked but did not start is not said
            }
            hist.Clear(); hist.AddRange(saved); Said = s0; Silent = q0; Orders = o0; Waited = w0;
            pass = twice == 0 && recentPicked == 0 && seen.Count == (pool?.Count ?? 0);
            return $"seed {seed}, {orders} repair orders on {pool?.Count ?? 0} lines: the same line twice in a row {twice}, a recent line picked while a fresher one was free {recentPicked}, lines that came up {seen.Count} of {pool?.Count ?? 0}";
        }
    }
}
