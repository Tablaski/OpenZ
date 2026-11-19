// UnityEngine, no Sim): the kit's off-Unity harness runs this file as it is (kit_tools/harness). VIEW ONLY: it reads the game clock the
// view is given, it never writes a rule, and its random picks come from its own System.Random (never one the rules use).
namespace OpenZ
{
    /// <summary>One turn that comes round at most once every <see cref="Every"/> seconds of game time. An event is OFFERED (a vehicle or
    /// turret death, a turret head at the top of its flight): it takes the turn only when the turn is due, the event is on the screen and
    /// the player can see it (MAYHEM fog). An event that does not take it (off the screen, under the fog) never uses the turn up.</summary>
    public sealed class OzExpTurn
    {
        public readonly double Every;
        double last = double.NegativeInfinity;
        /// <summary>Counts: turns taken; events offered while not due; offered when due but off the screen / under the fog.</summary>
        public int Taken, NotDue, OffScreen, Fogged;
        public OzExpTurn(double every) { Every = every; }
        public double Last => last;
        public bool IsDue(double now, bool force) => force || now - last >= Every;

        /// <summary>True: this event may take the turn now (the caller plays its explosion and then calls <see cref="Take"/>).</summary>
        public bool Offer(double now, bool onScreen, bool visible, bool force = false)
        {
            if (!IsDue(now, force)) { NotDue++; return false; }
            if (!onScreen) { OffScreen++; return false; }
            if (!visible) { Fogged++; return false; }
            return true;
        }

        public void Take(double now) { last = now; Taken++; }
        public void Reset() { last = double.NegativeInfinity; Taken = NotDue = OffScreen = Fogged = 0; }
    }

    /// <summary>The view's own random pick (never shared with the rules): one of n, never the same as the last pick twice in a row.</summary>
    public sealed class OzExpPick
    {
        readonly System.Random r; int last = -1;
        public OzExpPick(int seed) { r = new System.Random(seed); }
        public int Next(int n)
        {
            if (n <= 1) return 0;
            int v = r.Next(n);
            if (v == last) v = (v + 1 + r.Next(n - 1)) % n;
            last = v; return v;
        }
        public int Raw(int n) => r.Next(n);
    }
}
