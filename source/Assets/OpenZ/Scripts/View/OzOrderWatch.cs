using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.3 item 29 (units that answer an order but do not move): the player's move / attack click (PlayerInput.Flush) is looked
    /// at again WaitSec of battle time later. Every unit it was sent to that has not got under way (2 px) by then is named once in
    /// Player.log with its reason (ZObject.OrderStandWhy: a gun, stunned, waiting in traffic, the order refused by Zod's rules ...),
    /// "SILENT" when it has none. One line per such order; nothing at all for an order every unit started or carries out from where it
    /// stands (fires at its target in range, is at its spot already). View only: it reads the simulation and changes nothing. The time is the simulation's (a paused game waits).
    /// </summary>
    public static class OzOrderWatch
    {
        public const double WaitSec = 1.0;
        const int MaxOrders = 16, MaxNamed = 8;

        sealed class Unit { public ZObject o; public ZObject.OrderSnap snap; public bool moved; }
        sealed class Order { public ZWorld w; public double at; public readonly List<Unit> units = new List<Unit>(); }
        static readonly List<Order> orders = new List<Order>();

        /// <summary>Statistics (checks / proofs): orders watched, orders that left units standing (logged), silent units among them.</summary>
        public static int Watched, Logged, SilentUnits;

        /// <summary>PlayerInput.Flush: the units the order was sent to (shuttles fly by their own plan: not watched).</summary>
        public static void Watch(ZWorld w, List<ZObject> units)
        {
            if (w == null || units == null || units.Count == 0) return;
            var ord = new Order { w = w, at = w.Time };
            foreach (var o in units) if (o != null && !o.Destroyed && !o.IsShuttle) ord.units.Add(new Unit { o = o, snap = o.OrderSnapshot() });
            if (ord.units.Count == 0) return;
            if (orders.Count >= MaxOrders) orders.RemoveAt(0);
            orders.Add(ord); Watched++;
        }

        /// <summary>PlayerInput.Update, every frame: notes who moved; an order WaitSec old is looked at once and dropped.</summary>
        public static void Tick(ZWorld w)
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                var ord = orders[i];
                if (ord.w != w) { orders.RemoveAt(i); continue; }                                     // a new level / world
                foreach (var u in ord.units) if (!u.moved && u.o.OrderMovedFrom(u.snap)) u.moved = true;
                if (w.Time - ord.at < WaitSec) continue;
                orders.RemoveAt(i);
                Report(ord);
            }
        }

        static void Report(Order ord)
        {
            int standing = 0, silent = 0; StringBuilder sb = null;
            foreach (var u in ord.units)
            {
                if (u.moved) continue;
                string why = u.o.OrderStandWhy(u.snap, out bool s, out bool working);
                if (working) continue;
                standing++; if (s) silent++;
                if (standing > MaxNamed) continue;
                (sb ??= new StringBuilder()).Append(standing > 1 ? "; " : "").Append(u.o.Type).Append('#').Append(u.o.Id).Append(s ? " SILENT: " : ": ").Append(why);
            }
            if (standing == 0) return;
            Logged++; SilentUnits += silent;
            Debug.Log($"[OpenZ Orders] order at {ord.at:F1} s: {standing} of {ord.units.Count} unit(s) not under way after {WaitSec:0.0} s{(silent > 0 ? $" ({silent} SILENT)" : "")}: {sb}{(standing > MaxNamed ? $"; and {standing - MaxNamed} more" : "")}");
        }
    }
}
