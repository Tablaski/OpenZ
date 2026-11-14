using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// Which announcer line a radar capture gets (1.2 item 16). What a radar does in this game: while a team owns at least one
    /// STANDING radar, the enemy robots, vehicles and guns are drawn on its minimap (UI/OpenZMinimap.cs, DV33) and the announcer
    /// warns of big enemy strike groups (OzStrikeWarningRule.cs, DV107). That is the team's radar picture. Per team and per game
    /// (a new world starts over):
    /// <list type="bullet">
    /// <item>the capture that gives the team its radar picture while it has not HAD one yet in this game: <see cref="First"/>
    /// ("Radar online. Enemy units now tracked on the minimap.");</item>
    /// <item>the capture that gives the picture BACK after the team had one and then lost every standing radar (taken by the enemy
    /// or destroyed): <see cref="Back"/> ("Radar is back online.");</item>
    /// <item>a capture while another standing radar of the team already gives the picture: <see cref="More"/> (the plain lines
    /// "Radar online." / "Radar active.");</item>
    /// <item>a captured radar that is a ruin (or lost again before the line): no radar line (null), as the radar does nothing yet.</item>
    /// </list>
    /// A picture counts as HAD once its first line was chosen, when it stood at the level start, or when it stood at least
    /// <see cref="HoldSec"/> s before it ended (announced or not). A short picture nobody was told of does not count: a contested flag
    /// flips between the teams every 0.2 s (the sim's capture check), and the player's first radar after such a tug of war still gets
    /// the first line, never "back online". The CPU teams are followed in the same way (nothing is said for them, as before). Each
    /// first / back line is chosen once for the capture that started the picture. View only: reads the world, writes nothing into it.
    /// No engine types, so a headless check can run it.
    /// </summary>
    public sealed class OzRadarLineRule
    {
        public const string First = "ann_radar_first", Back = "ann_radar_back_online", More = "ann_radar_online";
        public const double RescanSec = 5.0;
        /// <summary>A picture that stood this long (game seconds) counts as had when it ends, announced or not.</summary>
        public const double HoldSec = 10.0;
        /// <summary>A picture seen at the first look of a new world before this game time is the level start's (it counts as had).</summary>
        public const double StartSec = 2.0;
        const int Teams = 3;                                                                   // Team.Neutral, Red, Blue

        ZWorld world;
        double nextScan = double.NegativeInfinity;
        bool firstLook;
        readonly List<ZObject> radars = new List<ZObject>();
        readonly bool[] covered = new bool[Teams];
        readonly bool[] had = new bool[Teams];                                                 // the team has had a picture that counts in this game
        readonly double[] since = new double[Teams];                                           // game time the current picture started
        readonly int[] pictures = new int[Teams];                                              // how often the team's picture started in this game
        readonly HashSet<int>[] starters = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };   // the radars that started the current picture
        readonly List<int> drop = new List<int>();

        /// <summary>Counters of this game for the checks: lines chosen per kind, radar captures with no line (a ruin).</summary>
        public int Firsts, Backs, Mores, Skipped;
        /// <summary>Games followed since the start (a new world = a new game).</summary>
        public int Games;

        public ZWorld World => world;
        public int RadarCount => radars.Count;
        /// <summary>The team owns a standing radar now (as last observed).</summary>
        public bool Covered(Team t) { int i = (int)t; return i > 0 && i < Teams && covered[i]; }
        /// <summary>How many times the team's radar picture started in this game (1 = the first one).</summary>
        public int Pictures(Team t) { int i = (int)t; return i > 0 && i < Teams ? pictures[i] : 0; }
        /// <summary>The team has had a radar picture that counts in this game (the next picture's line is "back online").</summary>
        public bool Had(Team t) { int i = (int)t; return i > 0 && i < Teams && had[i]; }

        void Reset(ZWorld w)
        {
            world = w; nextScan = double.NegativeInfinity; radars.Clear(); drop.Clear(); firstLook = true;
            for (int i = 0; i < Teams; i++) { covered[i] = false; had[i] = false; since[i] = 0; pictures[i] = 0; starters[i].Clear(); }
            Firsts = Backs = Mores = Skipped = 0; Games++;
        }

        ZObject Radar(int id) { foreach (var r in radars) if (r.Id == id) return r; return null; }

        /// <summary>Every frame (and before each choice): who owns a standing radar. No allocations.</summary>
        public void Observe(ZWorld w)
        {
            if (w == null) return;
            if (w != world) Reset(w);
            if (w.Time >= nextScan)
            {   // the map's radars (buildings of the level; a rescan now and then keeps the list right)
                nextScan = w.Time + RescanSec; radars.Clear();
                foreach (var o in w.Objects) if (o != null && o.Kind == ZKind.Building && o.Type == "radar") radars.Add(o);
            }
            for (int t = 1; t < Teams; t++)
            {
                bool c = false;
                foreach (var r in radars) if ((int)r.Owner == t && !r.Destroyed) { c = true; break; }
                var s = starters[t];
                if (c && !covered[t])
                {   // the picture starts: the radars standing now are the ones that started it
                    pictures[t]++; s.Clear(); since[t] = w.Time;
                    if (firstLook && w.Time <= StartSec) had[t] = true;                           // a radar of the team at the level start (no line then)
                    foreach (var r in radars) if ((int)r.Owner == t && !r.Destroyed) s.Add(r.Id);
                }
                else
                {
                    if (!c && covered[t] && w.Time - since[t] >= HoldSec) had[t] = true;        // a picture that stood HoldSec counts, announced or not
                    if (s.Count > 0)
                    {   // a starting radar the team no longer holds standing cannot start this picture again (lost and retaken while
                        // another radar kept the picture = a plain line)
                        drop.Clear();
                        foreach (int id in s) { var r = Radar(id); if (r == null || (int)r.Owner != t || r.Destroyed) drop.Add(id); }
                        foreach (int id in drop) s.Remove(id);
                    }
                }
                covered[t] = c;
            }
            firstLook = false;
        }

        /// <summary>The announcer event for the radar(s) of the zone of flag `flagId` that `team` has just taken: <see cref="First"/>,
        /// <see cref="Back"/> or <see cref="More"/>; null = no radar line (the zone's radar is a ruin, or the team lost it again).</summary>
        public string LineFor(ZWorld w, Team team, int flagId)
        {
            Observe(w);
            int t = (int)team;
            var flag = w != null ? w.GetAny(flagId) : null;
            if (flag == null || t <= 0 || t >= Teams) { Mores++; return More; }               // nothing to look at: the plain lines (never wrong)
            bool standing = false, started = false;
            foreach (var b in flag.LinkedObjects)
            {
                if (b == null || b.Type != "radar" || (int)b.Owner != t || b.Destroyed) continue;
                standing = true;
                if (starters[t].Contains(b.Id)) started = true;
            }
            if (!standing) { Skipped++; return null; }
            if (!started) { Mores++; return More; }
            foreach (var b in flag.LinkedObjects) if (b != null && b.Type == "radar") starters[t].Remove(b.Id);   // said once for this capture
            if (!had[t]) { had[t] = true; Firsts++; return First; }                           // the first picture the team is told of
            Backs++; return Back;
        }
    }
}
