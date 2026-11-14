using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// The unit panel's second line for the playable shuttle (DV118, Sim/ZWorld.Shuttle.cs), called by OpenZHud where the driver /
    /// passengers line of a vehicle is made: the pilot (he never gets out), who is aboard (type, or the heroes' names, and how many),
    /// and what it is doing (on the ground, up, hovering because it cannot land, and why).
    /// ABOARD is the ones who get out on EXIT (<see cref="Who"/>):
    /// the passengers, and a hero pilot (he gets out on EXIT, View/OzShuttleHeroesView.cs); any other pilot is never counted, the line
    /// says "PILOT STAYS IN". The same count and faces show once on the card, in its type chip (UI/OpenZHud.ShuttleAboard.cs, round 3)
    /// and on the name chip over the ship (UI/OpenZUnitOverlay.ShuttleAboard.cs). The card's line comes in four lengths (<see cref="Lines"/>:
    /// the panel shows the longest that fits, the old one ended in "..."), kept until something in it changes (no new string while it
    /// stays the same).
    /// </summary>
    public static class OzShuttleHud
    {
        /// <summary>1.2 item 2: the neutral ship's line (no pilot, nobody aboard), the words for no passengers, and the strip's / pill's EMPTY.</summary>
        public const string EmptyLine = "EMPTY · NO PILOT · ORDER A SQUAD IN", RoomWord = "ROOM FOR ONE SQUAD", EmptyWord = "EMPTY";

        /// <summary>1.2 item 2: who is aboard and gets out on EXIT, counted without allocating: the plain robots (the passengers'
        /// type) and the hero members (one bit per member of <see cref="Hero"/>: hero passengers and a hero pilot).</summary>
        public struct Aboard
        {
            public int Plain, Heroes, HeroMask; public string PlainType; public ZHeroKind Hero; public Team Team;
            public int Count => Plain + Heroes;
            public bool Same(Aboard o) => Plain == o.Plain && Heroes == o.Heroes && HeroMask == o.HeroMask && ReferenceEquals(PlainType, o.PlainType) && Hero == o.Hero && Team == o.Team;
        }

        /// <summary>1.2 item 2: the ones aboard who get out on EXIT: every passenger, and the pilot only when he is a hero (any other
        /// pilot stays in and is never counted). heroPilot false: the passengers only (the line names a hero pilot on its own).</summary>
        public static Aboard Who(ZObject u, bool heroPilot = true)
        {
            var a = new Aboard();
            if (u == null || !u.IsShuttle) return a;
            a.Team = u.Owner; a.PlainType = u.PassengerType;
            int n = u.Passengers.Count; a.Plain = n;
            var ps = u.HeroPassengerSeats;
            if (ps != null)
                for (int i = 0; i < n && i < ps.Count; i++)
                {
                    var s = ps[i]; if (s == null || s.Kind == null) continue;
                    if (a.Hero == null) a.Hero = s.Kind;
                    if (s.Kind != a.Hero) continue;
                    a.Plain--; a.Heroes++; a.HeroMask |= 1 << s.Member;
                }
            if (heroPilot && u.ShuttleHeroPilot)
            {
                var s = u.HeroDriverSeats[0];
                if (a.Hero == null) a.Hero = s.Kind;
                if (s.Kind == a.Hero) { a.Heroes++; a.HeroMask |= 1 << s.Member; }
            }
            return a;
        }

        /// <summary>The names of who is aboard: "2 GRUNTS", "ALAN &amp; BRAD", "BRAD + 2 GRUNTS" ("" = nobody).</summary>
        public static string Names(Aboard a)
        {
            string heroes = HeroNames(a.Hero, a.HeroMask);
            string plain = a.Plain <= 0 ? null : a.Plain + " " + UnitInfo.Name(a.PlainType) + (a.Plain > 1 ? "S" : "");
            if (heroes == null) return plain ?? "";
            return plain == null ? heroes : heroes + " + " + plain;
        }

        /// <summary>The hero members in the mask: the kind's name when all of them ("ALAN &amp; BRAD"), else theirs in the kind's order
        /// ("BRAD"); null = none.</summary>
        public static string HeroNames(ZHeroKind k, int mask)
        {
            if (k == null || mask == 0 || k.Members == null) return null;
            int all = (1 << k.Members.Length) - 1;
            if ((mask & all) == all) return k.Name;
            string r = null;
            for (int i = 0; i < k.Members.Length; i++) if ((mask & (1 << i)) != 0) r = r == null ? OzHeroPair.MemberName(k, i) : r + " & " + OzHeroPair.MemberName(k, i);
            return r ?? k.Name;
        }

        // the last lines made (kept while nothing in them changes: the panel asks ten times a second)
        static int lineId = -1, lineKey; static Aboard lineWho; static string lineNoLand, lineFull = "";
        static readonly string[] lines = { "", "", "", "" };

        /// <summary>The whole line, the pilot first: "PILOT STAYS IN · 3 GRUNTS ABOARD · LANDED · EXIT READY", "PILOT BRAD CAN EXIT · ALAN
        /// ABOARD · LANDED" (the checks and logs read it; the card shows the pilot in its own slot and <see cref="Lines"/> under the name).</summary>
        public static string Line(ZObject u) { if (u == null) return ""; Lines(u); return lineFull; }

        /// <summary>1.2 item 2: the card's line in four lengths, longest first, for the panel to show the first that fits its width without
        /// shrinking out of reach (UI/OpenZHud.ShuttleAboard.cs): who is aboard · what the ship does; the same with the short state
        /// ("LANDED", "CANNOT LAND (WATER)"); what it does; the short state. Round 3: the pilot is the card's PILOT slot, so the line does
        /// not say him again; who is aboard counts a hero pilot (he gets out too: "ALAN &amp; BRAD ABOARD"), as the type chip does. Shared
        /// and rebuilt only on a change: read, never keep or change the array.</summary>
        public static string[] Lines(ZObject u)
        {
            if (u == null) { lines[0] = lines[1] = lines[2] = lines[3] = lineFull = ""; lineId = -1; return lines; }
            var who = Who(u, false);
            int key = (int)u.ShuttleState | (u.ShuttleOnRepairPad ? 1 << 4 : 0) | (u.ShuttleExitPending ? 1 << 5 : 0) | (u.ShuttleRampPhase != ZShuttleRamp.Shut ? 1 << 6 : 0)
                    | (u.ShuttleHasLeavers ? 1 << 7 : 0) | (u.ShuttleExitOnLanding ? 1 << 8 : 0) | (u.ShuttleHeroPilot ? 1 << 9 : 0) | (u.Drivers.Count > 0 ? 1 << 10 : 0) | (u.ShuttleIsEmpty ? 1 << 11 : 0);
            if (u.Id == lineId && key == lineKey && who.Same(lineWho) && ReferenceEquals(u.ShuttleNoLand, lineNoLand)) return lines;
            lineId = u.Id; lineKey = key; lineWho = who; lineNoLand = u.ShuttleNoLand;
            if (u.ShuttleIsEmpty) { lines[0] = lines[1] = lines[2] = lines[3] = lineFull = EmptyLine; return lines; }   // K256f (Sim/ZWorld.ShuttleEmpty.cs); 1.2 item 2: shorter
            string pilot = u.Drivers.Count > 0 ? OzShuttleHeroesView.PilotWord(u) : "NO PILOT";                    // K256f2: a hero pilot by name, he gets out on EXIT (View/OzShuttleHeroesView.cs)
            string seats = u.Passengers.Count > 0 ? Names(who) + " ABOARD" : RoomWord;                               // the passengers' seats (a hero pilot named in the pilot part)
            string aboard = u.Passengers.Count > 0 ? Names(Who(u)) + " ABOARD" : RoomWord;                           // everyone who gets out
            string state = u.ShuttleExitOnLanding ? "LANDING TO LET THEM OUT" : State(u), brief = u.ShuttleExitOnLanding ? "LANDING" : StateShort(u);   // K256f: EXIT given in the air
            lineFull = pilot + " · " + seats + " · " + state;
            lines[0] = aboard + " · " + state;
            lines[1] = aboard + " · " + brief;
            lines[2] = state;
            lines[3] = brief;
            return lines;
        }

        /// <summary>1.2 item 2: the state in fewer words for a long line: "LANDED" (EXIT READY shows as the EXIT CREW button too),
        /// "CANNOT LAND (WATER)".</summary>
        public static string StateShort(ZObject u)
        {
            if (u.ShuttleState == ZShuttleState.Landed && !u.ShuttleOnRepairPad && !u.ShuttleExitPending && u.ShuttleRampPhase == ZShuttleRamp.Shut) return "LANDED";
            if (u.ShuttleState == ZShuttleState.Hover) return string.IsNullOrEmpty(u.ShuttleNoLand) ? "HOVERING" : "CANNOT LAND (" + Why(u.ShuttleNoLand) + ")";
            return State(u);
        }

        public static string State(ZObject u)
        {
            switch (u.ShuttleState)
            {
                case ZShuttleState.Landed: return u.ShuttleOnRepairPad ? "REPAIRING" : u.ShuttleExitPending ? "UNLOADING" : u.ShuttleRampPhase != ZShuttleRamp.Shut ? "RAMP OPEN" : u.ShuttleHasLeavers ? "LANDED · EXIT READY" : "LANDED";   // item 271
                case ZShuttleState.Climb: return "TAKING OFF";
                case ZShuttleState.Cruise: return "FLYING";
                case ZShuttleState.Descend: return "LANDING";
                case ZShuttleState.Hover: return "HOVERING · CANNOT LAND HERE" + (string.IsNullOrEmpty(u.ShuttleNoLand) ? "" : " (" + Why(u.ShuttleNoLand) + ")");
            }
            return "";
        }

        /// <summary>The count aboard (who gets out on EXIT, <see cref="Who"/>) as text ("" when nobody).</summary>
        public static string Badge(ZObject u) { int n = u == null ? 0 : Who(u).Count; return n == 0 ? "" : Count(n); }

        static readonly string[] counts = new string[16];
        /// <summary>A count as text, made once per number.</summary>
        public static string Count(int n) => n < 0 || n >= counts.Length ? n.ToString() : counts[n] ??= n.ToString();

        public static string Why(string why)
        {
            switch (why)
            {
                case "water": return "WATER";
                case "rocks": return "ROCKS";
                case "blocked": return "NO ROOM";
                case "units": return "UNITS IN THE WAY";
                case "building": return "A BUILDING";
                case "flag": return "A FLAG";
                case "bridge": return "A BRIDGE";
                case "item": return "SOMETHING IN THE WAY";
                case "map edge": return "MAP EDGE";
                case "repair bay busy": return "REPAIR BAY BUSY";
            }
            return why.ToUpperInvariant();
        }
    }
}
