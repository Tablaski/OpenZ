using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// Version 1.4 speed-up 5 (the rules' who-is-near loops). The same rules, less work: a neighbour grid of the units of Objects, so a
    /// loop that only wants units within a reach looks at the cells around it instead of walking every object of the map. Every answer
    /// is the one the old loop gives, in the same order, with the same random draws, so a match is identical tick for tick (proven by the
    /// kit's headless harness against the 1.3 rules and by Editor/OzV14NeighbourGridCount.cs). Not a deviation: no rule, number or timing
    /// changes. Switch <see cref="PerfNeighbourGrid"/> (on); off = the old loops, kept for the A/B proof.
    /// <para>Why the answers cannot change:</para>
    /// <list type="bullet">
    /// <item>Each loop asks <see cref="NearUnits"/> for the units whose centre is within a square of half side r around a point, r being
    /// the farthest any test of that loop can reach (<see cref="ZObject.NbRangeReach"/> and the others below). A unit outside that square
    /// fails the loop's own distance test, because a centre distance is never shorter than its larger axis part; and every test the old
    /// loop made on such a unit before its distance test reads state only (no draw, no counter, no field written). So skipping it changes
    /// nothing. The loop body is left as it was and still makes every test.</item>
    /// <item>The units come back in object order: CreateObject gives ids in the order it appends to Objects, and DeleteObject keeps the
    /// order, so object order is id order. Every rebuild checks it; if Objects were ever out of id order the grid answers nothing and the
    /// old loops run.</item>
    /// <item>The cells follow every move: the three places that write a centre (SetCords, ZObject.ProcessMoveCore, ZObject.PathNudge)
    /// call <see cref="NbMoved"/>. A unit made or deleted, or a check that edited Objects itself, rebuilds the grid at the next question
    /// (as UnitObjects does).</item>
    /// <item>None of these loops moves, makes or deletes an object while it walks, so the list taken at its first line is the list the
    /// old loop would have walked.</item>
    /// </list>
    /// Counted check: <see cref="NbVerify"/> makes every question walk Objects as well and compare (Editor/OzV14NeighbourGridCount.cs).
    /// Pure C# like the rest of the Sim folder: integers, object order, no Unity, no clock.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>1.4 speed-up 5: the rules' who-is-near loops read the neighbour grid (the same answers, bit for bit). False = the old
        /// loops over Objects, kept for the A/B proof only; the game never sets it.</summary>
        public static bool PerfNeighbourGrid = true;

        /// <summary>Counted check only (default off): every question also walks Objects and compares; a difference is counted in
        /// <see cref="NbVerifyFails"/> and the walked answer is used.</summary>
        public static bool NbVerify;

        /// <summary>Questions the grid answered, and (with <see cref="NbVerify"/>) the ones whose answer differed from the walk.</summary>
        public long NbQueries { get; private set; }
        public long NbVerifyFails { get; private set; }
        public string NbVerifyFirst { get; private set; }

        const int NbShift = 6;                                                            // cells of 64 px = 4 tiles
        List<ZObject>[] nbCells;
        int nbW, nbH, nbGen, nbCount = -1, nbAdds = -1, nbRemoves = -1;
        bool nbOrdered;
        readonly Stack<List<ZObject>> nbPool = new Stack<List<ZObject>>();
        static readonly IdOrder NbById = new IdOrder();

        sealed class IdOrder : IComparer<ZObject> { public int Compare(ZObject a, ZObject b) => a.Id.CompareTo(b.Id); }

        int NbCellOf(int x, int y)
        {
            int cx = x >> NbShift, cy = y >> NbShift;
            if (cx < 0) cx = 0; else if (cx >= nbW) cx = nbW - 1;
            if (cy < 0) cy = 0; else if (cy >= nbH) cy = nbH - 1;
            return cy * nbW + cx;
        }

        /// <summary>The grid can answer now (switch on, a map, Objects in id order); rebuilt first when Objects changed since the last build.</summary>
        bool NbReady()
        {
            if (!PerfNeighbourGrid || Map == null) return false;
            if (nbCells == null || nbCount != Objects.Count || nbAdds != objAdds || nbRemoves != objRemoves) NbRebuild();
            return nbOrdered;
        }

        void NbRebuild()
        {
            int w = Math.Max(1, (Map.WidthPix >> NbShift) + 1), h = Math.Max(1, (Map.HeightPix >> NbShift) + 1);
            if (nbCells == null || w != nbW || h != nbH)
            {
                nbW = w; nbH = h; nbCells = new List<ZObject>[w * h];
                for (int i = 0; i < nbCells.Length; i++) nbCells[i] = new List<ZObject>(4);
            }
            else foreach (var c in nbCells) c.Clear();
            nbGen++;
            nbOrdered = true;
            int last = int.MinValue;
            foreach (var o in Objects)
            {
                if (o.Id <= last) nbOrdered = false;
                last = o.Id;
                if (!o.IsUnit) continue;
                var cell = nbCells[o.nbCell = NbCellOf(o.CenterX, o.CenterY)];
                o.nbGen = nbGen; o.nbSlot = cell.Count; cell.Add(o);
            }
            nbCount = Objects.Count; nbAdds = objAdds; nbRemoves = objRemoves;
        }

        /// <summary>A centre was written (SetCords, ProcessMoveCore, PathNudge): a unit of the grid that left its cell moves to the new one.</summary>
        internal void NbMoved(ZObject o)
        {
            if (o.nbGen != nbGen || o.nbCell < 0 || nbCells == null) return;
            int c = NbCellOf(o.CenterX, o.CenterY);
            if (c == o.nbCell) return;
            var from = nbCells[o.nbCell];
            int s = o.nbSlot, lastSlot = from.Count - 1;
            if (s > lastSlot || from[s] != o) { nbCount = -1; return; }                  // (never: the next question rebuilds)
            if (s != lastSlot) { var m = from[lastSlot]; from[s] = m; m.nbSlot = s; }
            from.RemoveAt(lastSlot);
            var to = nbCells[c];
            o.nbCell = c; o.nbSlot = to.Count; to.Add(o);
        }

        /// <summary>The units (robots, vehicles, guns; dead ones too, as Objects holds them) whose centre is within the square of half side
        /// r around (x, y) and whose id is at most maxId, in object order; to be walked by a foreach in place of <paramref name="all"/>.
        /// When the grid cannot answer it walks <paramref name="all"/> itself, as the old loop did.</summary>
        internal ZNearUnits NearUnits(int x, int y, int r, List<ZObject> all, int maxId = int.MaxValue)
        {
            if (!NbReady()) return new ZNearUnits(this, null, all);
            NbQueries++;
            var l = nbPool.Count > 0 ? nbPool.Pop() : new List<ZObject>(64);
            if (r >= 0)
            {
                int c0 = NbCellOf(x - r, y - r), c1 = NbCellOf(x + r, y + r);
                int cx0 = c0 % nbW, cy0 = c0 / nbW, cx1 = c1 % nbW, cy1 = c1 / nbW;
                for (int cy = cy0; cy <= cy1; cy++)
                    for (int cx = cx0; cx <= cx1; cx++)
                        foreach (var o in nbCells[cy * nbW + cx])
                            if (o.Id <= maxId && Math.Abs(o.CenterX - x) <= r && Math.Abs(o.CenterY - y) <= r) l.Add(o);
                if (l.Count > 1) l.Sort(NbById);
            }
            if (NbVerify) NbCheck(l, x, y, r, maxId);
            return new ZNearUnits(this, l, all);
        }

        /// <summary><see cref="NbVerify"/>: the same question by walking Objects; a difference is counted and the walk's answer used.</summary>
        void NbCheck(List<ZObject> l, int x, int y, int r, int maxId)
        {
            var walk = new List<ZObject>();
            foreach (var o in Objects)
                if (o.IsUnit && o.Id <= maxId && r >= 0 && Math.Abs(o.CenterX - x) <= r && Math.Abs(o.CenterY - y) <= r) walk.Add(o);
            bool same = walk.Count == l.Count;
            for (int i = 0; same && i < walk.Count; i++) same = walk[i] == l[i];
            if (same) return;
            NbVerifyFails++;
            if (NbVerifyFirst == null) NbVerifyFirst = $"tick {TickIndex}: around ({x}, {y}) r {r}: grid {l.Count} units, walk {walk.Count}";
            l.Clear(); l.AddRange(walk);
        }

        internal void NbGive(List<ZObject> l) { l.Clear(); nbPool.Push(l); }

        /// <summary>Counted checks: the grid's answer as a new list (the units within the square of half side r around (x, y), object
        /// order), or null when the grid cannot answer (switch off, no map, Objects out of id order).</summary>
        public List<ZObject> NearUnitsList(int x, int y, int r)
        {
            var e = NearUnits(x, y, r, null).TakeList();
            if (e == null) return null;
            var copy = new List<ZObject>(e); NbGive(e);
            return copy;
        }

        /// <summary>ScuffleUnits' box: two idle units nudge when |dx| and |dy| are under 6 px, so at most 5 (ZWorld.cs).</summary>
        public const int ScuffleBoxPx = 5;

        // ------------------------------------------------------------------ grenade crates (CheckWalkOverCrate)
        readonly List<ZObject> nbCrates = new List<ZObject>();
        int nbCratesCount = -1, nbCratesAdds = -1, nbCratesRemoves = -1;

        /// <summary>The grenade crates of Objects (Type grenades; a type never changes after CreateObject) in object order, to be walked in
        /// place of <paramref name="all"/> by a loop that only wants crates; <paramref name="all"/> itself when the switch is off.</summary>
        internal List<ZObject> GrenadeCratesOr(List<ZObject> all)
        {
            if (!PerfNeighbourGrid) return all;
            if (nbCratesCount != Objects.Count || nbCratesAdds != objAdds || nbCratesRemoves != objRemoves)
            {
                nbCrates.Clear();
                foreach (var o in Objects) if (o.Type == "grenades") nbCrates.Add(o);
                nbCratesCount = Objects.Count; nbCratesAdds = objAdds; nbCratesRemoves = objRemoves;
            }
            return nbCrates;
        }

        // ------------------------------------------------------------------ DV65 run-over: the robots near one vehicle
        readonly List<ZObject> nbRunOver = new List<ZObject>();

        /// <summary>RunOverTick: the units within 40 px (the pass's own box) of vehicle v among Objects[0..n) (ids up to that of
        /// Objects[n - 1]: nothing made during the pass is looked at, as before), in object order; null = the pass walks its list as before.
        /// One list of the world, refilled for each vehicle (the pass never asks while it walks it).</summary>
        internal List<ZObject> NbRunOverNear(ZObject v, int n)
        {
            if (n <= 0 || n > Objects.Count || !NbReady()) return null;
            var g = NearUnits(v.CenterX, v.CenterY, RunOverBoxPx, null, Objects[n - 1].Id).TakeList();
            if (g == null) return null;
            nbRunOver.Clear(); nbRunOver.AddRange(g); NbGive(g);
            return nbRunOver;
        }

        /// <summary>The run-over pass's box: |dx| and |dy| at most this (ZWorld.RunOver.cs).</summary>
        public const int RunOverBoxPx = 40;
    }

    /// <summary>What <see cref="ZWorld.NearUnits"/> gives a foreach: the grid's list (handed back to the pool when the loop ends, a
    /// return or break included) or the old list walked as before.</summary>
    internal struct ZNearUnits
    {
        readonly ZWorld w; List<ZObject> grid; readonly List<ZObject> all;
        internal ZNearUnits(ZWorld w, List<ZObject> grid, List<ZObject> all) { this.w = w; this.grid = grid; this.all = all; }
        internal List<ZObject> TakeList() { var g = grid; grid = null; return g; }
        public Enumerator GetEnumerator() => new Enumerator(w, grid, all);

        public struct Enumerator : IDisposable
        {
            readonly ZWorld w; List<ZObject> grid; List<ZObject>.Enumerator e; int i;
            internal Enumerator(ZWorld w, List<ZObject> grid, List<ZObject> all) { this.w = w; this.grid = grid; e = grid == null ? all.GetEnumerator() : default; i = 0; Current = null; }
            public ZObject Current { get; private set; }
            public bool MoveNext()
            {
                if (grid != null) { if (i < grid.Count) { Current = grid[i++]; return true; } return false; }
                if (e.MoveNext()) { Current = e.Current; return true; }
                return false;
            }
            public void Dispose() { if (grid != null) { w.NbGive(grid); grid = null; } }
        }
    }

    public partial class ZObject
    {
        // the neighbour grid's place of this object (ZWorld.NeighbourGrid.cs): its build, cell and slot; -1 = not in the grid
        internal int nbGen, nbCell = -1, nbSlot;

        /// <summary>1.4 speed-up 5: the farthest this unit's attack range reaches at any target (WithinAttackRadius: RangeTo, DV162's high
        /// ground included), px.</summary>
        internal int NbRangeReach() => Math.Max(AttackRadius, ZWorld.HighGroundRange(AttackRadius));

        /// <summary>The farthest its agro reach goes (WithinAgroRadius: AgroReachTo = range + agro_distance, or the range if longer), px.</summary>
        internal int NbAgroReach() => Math.Max(AttackRadius + World.S.agro_distance, NbRangeReach());

        /// <summary>CheckPassiveEngage's loop, px: an enemy in range; when it may start a chase (agroOn = its idle and no calm start) an
        /// enemy in agro reach too, and when it may also take an empty vehicle / gun by itself (a robot, not a squad member, not one that
        /// boards only on an order) one within auto_grab_vehicle_distance. Every other test of that loop needs one of these.</summary>
        internal int NbPassiveReach(bool agroOn) =>
            !agroOn ? NbRangeReach() : !IsMinion && IsRobot && !NoAutoEnter ? Math.Max(NbAgroReach(), World.S.auto_grab_vehicle_distance) : NbAgroReach();
    }
}
