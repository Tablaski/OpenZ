using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// gives exactly the answer the 1.2 code gives, in the same order, with the same random draws, so a match is identical tick for
    /// tick (proven by Editor/OzPerfSimSameCheck.cs: the same matches with PerfFastRules off and on, the world's fingerprint every tick,
    /// and by the headless harness of the item's package against the 1.2 build). Not a deviation: no rule, number or timing changes.
    /// <list type="bullet">
    /// <item><b>Parked units of the DV28 approach walk</b> (ZObject.CombatNB.cs NearestInRangeSpot): the walk asked, for every tile it
    /// reached, every parked unit "are you in my way here?" (ParkedAt). Now the parked units stamp the tiles where that answer is yes into a
    /// tile map once per walk (StampParked, ParkedAt's own test per tile), and the walk reads one cell. Each unit's spacing radius is read
    /// once per walk instead of once per tile.</item>
    /// <item><b>Zod's A* open list</b> (ZPathFinder.cs): the pop scanned the whole open list for the first lowest f. A heap of list places
    /// keyed by (f, place) returns that same entry; Zod's swap-remove, the closed-list quirk and the work counter of the DV44 budget stay
    /// as they were.</item>
    /// <item><b>Loops over every unit</b> (CheckAttackTo, SwitchToExposed, the DV28 parked list, the DV65 run-over pass): they walked
    /// World.Objects (every rock, tree, prop and building as well) and skipped what is not a unit. UnitObjects is the units of Objects in
    /// the same order, rebuilt whenever an object was added or deleted (or a check edited Objects itself), so every loop sees the same units
    /// in the same order.</item>
    /// </list>
    /// Also here (log only, no rule effect): the walk counters for the per-minute perf line and the world fingerprint for the player's
    /// -oz-simprint run (View/OzPerfLine.cs), which compares two builds of the exe (Mono and IL2CPP) tick for tick.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>1.3 item 25: the faster code paths of the rules (the same results, bit for bit). False = the 1.2 code, kept for the
        /// A/B proof only (Editor/OzPerfSimSameCheck.cs); the game never sets it.</summary>
        public static bool PerfFastRules = true;

        /// <summary>Log only (View/OzPerfLine.cs -oz-simprint): called after every rule tick when set. Reads the world, writes nothing.</summary>
        public static Action<ZWorld> AfterTickHook;

        // ------------------------------------------------------------------ the units of Objects, in object order
        int objAdds, objRemoves;                                                         // CreateObject / DeleteObject (ZWorld.cs)
        internal int ObjectListRevision => unchecked(objAdds + objRemoves);
        internal int RockPositionRevision { get; private set; }
        internal void RockMoved() => RockPositionRevision++;
        readonly List<ZObject> unitObjs = new List<ZObject>();
        int unitObjsCount = -1, unitObjsAdds = -1, unitObjsRemoves = -1;

        /// <summary>Every robot, vehicle and cannon of Objects (dead ones too, exactly the objects a loop over Objects that skips
        /// non-units would look at), in object order. Rebuilt when Objects changed since the last call: an object made or deleted, or a
        /// check that edited Objects itself (its count differs). Kind never changes after CreateObject. Never make or delete an object
        /// while looping over it (a loop over Objects could not either: its foreach would throw).</summary>
        internal List<ZObject> UnitObjects
        {
            get
            {
                if (unitObjsCount != Objects.Count || unitObjsAdds != objAdds || unitObjsRemoves != objRemoves)
                {
                    unitObjs.Clear();
                    foreach (var o in Objects) if (o.IsUnit) unitObjs.Add(o);
                    unitObjsCount = Objects.Count; unitObjsAdds = objAdds; unitObjsRemoves = objRemoves;
                }
                return unitObjs;
            }
        }

        // ------------------------------------------------------------------ DV65 run-over pass: the robots of Objects[0..n)
        readonly List<ZObject> roRobots = new List<ZObject>();
        int roRemoves;

        /// <summary>RunOverTick (ZWorld.RunOver.cs), its first line: the robots among the first n objects, in object order.</summary>
        void ListRunOverRobots(int n)
        {
            roRobots.Clear();
            for (int j = 0; j < n && j < Objects.Count; j++) if (Objects[j].Kind == ZKind.Robot) roRobots.Add(Objects[j]);
            roRemoves = objRemoves;
        }

        // ------------------------------------------------------------------ DV28 approach walk: parked units stamped once per walk
        /// <summary>Approach walks run (DV28 NearestInRangeSpot) and the tiles they reached (log only: the per-minute perf line).</summary>
        public long ApproachWalks { get; private set; }
        public long ApproachTiles { get; private set; }

        /// <summary>NearestInRangeSpot, after apParked is listed: every footprint tile whose centre ParkedAt(centre, half, pad) would
        /// answer true for gets apBlock = apStamp. Per parked unit only the tiles near it are tested, each with ParkedAt's own test, so
        /// apBlock[tile] == apStamp is exactly ParkedAt at that tile's FootCentre.</summary>
        void StampParked(int w, int h, int half, int pad, bool robot)
        {
            int off = robot ? 8 : 16;                                                    // ZReach.FootCentre: tile * 16 + off
            foreach (var o in apParked)
            {
                int r = ZPathing.Radius(o) + pad, lim = r + half - pad, cx = o.CenterX, cy = o.CenterY;
                int tx0 = Math.Max(0, (cx - lim - off) >> 4), tx1 = Math.Min(w - 1, ((cx + lim - off) >> 4) + 1);   // (a superset of the tiles in reach: each is tested below)
                int ty0 = Math.Max(0, (cy - lim - off) >> 4), ty1 = Math.Min(h - 1, ((cy + lim - off) >> 4) + 1);
                for (int ty = ty0; ty <= ty1; ty++)
                    for (int tx = tx0; tx <= tx1; tx++)
                    {
                        var (x, y) = ZReach.FootCentre(tx, ty, robot);
                        if (Math.Abs(o.CenterX - x) < r + half - pad && Math.Abs(o.CenterY - y) < r + half - pad) apBlock[ty * w + tx] = apStamp;   // ParkedAt's test
                    }
            }
        }

        /// <summary>NearestInRangeSpot: one walk ran and reached `tiles` tiles (counters only).</summary>
        void NoteApproachWalk(int tiles) { ApproachWalks++; ApproachTiles += tiles; }

        // ------------------------------------------------------------------ the world's fingerprint (log only)
        static ulong PerfMix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }

        /// <summary>Folds every object's id, owner, place, sub-pixel carry, health and orders into h: the same fields, in the same order,
        /// as the counted checks' fingerprint (Editor/OzV11StrikeCount.Fold), so a player log and a headless run can be compared.</summary>
        public ulong RulesFingerprint(ulong h)
        {
            h = PerfMix(h, Objects.Count);
            foreach (var o in Objects)
            {
                h = PerfMix(h, o.Id); h = PerfMix(h, (long)o.Owner); h = PerfMix(h, o.LocX); h = PerfMix(h, o.LocY);
                h = PerfMix(h, BitConverter.DoubleToInt64Bits(o.XOver)); h = PerfMix(h, BitConverter.DoubleToInt64Bits(o.YOver));
                h = PerfMix(h, o.Health); h = PerfMix(h, o.Waypoints.Count);
                foreach (var p in o.Waypoints) { h = PerfMix(h, (long)p.mode); h = PerfMix(h, p.refId); h = PerfMix(h, p.x); h = PerfMix(h, p.y); }
            }
            return h;
        }
    }
}
