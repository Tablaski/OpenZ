using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUePlayground.cs): the field's unit cap in every drop (View/OzUeCap.cs) and SAMPLE FIGHT on free ground.
    ///  The cap: DropCore asks before each group (a squad, a vehicle, a gun, a shuttle) whether its units still fit; an x3 / x5 drop places
    ///   the groups that fit and says the rest did not ("RED IS FULL: 300 UNITS, THE EDITOR'S LIMIT (2 OF 5 PLACED)"); the drop preview
    ///   turns to "no room" with the same words when not even one group fits.
    ///  SAMPLE FIGHT (spec E025, the same twelve groups as before: UI/OzUePlan.cs): never over units, flags or obstacles. Its spots are
    ///   tried round the free field's middle first, then on a 44 m grid round it (one fight and its room fits one cell), the ones in view
    ///   first, nearest first; a place counts when every group's spot is inside the map, on open tiles (no obstacle, no flag) and clear
    ///   of everything standing (its own room + the other's: robots 3 m + 1 m, vehicles and guns 3.5 m + 2.5 m). At most
    ///   OzUeCap.SampleFightsMax fights on the field at once (a fight counts while one of its units stands; CLEAR FIELD frees them all), and
    ///   none that would pass the unit cap. A refusal places nothing and says why (a toast, ui_error); a fight placed beside the view says
    ///   so. One drop sound for the whole fight, as before.
    ///  The cap bench (View/OzUeCapBench.cs): its armies placed past the cap, without a log line per group.
    /// </summary>
    public static partial class OzUePlayground
    {
        static readonly List<int[]> fights = new List<int[]>();
        /// <summary>Checks: the last fight's centre, whether it stood in view, the last refusal's words; refused SAMPLE FIGHT clicks.</summary>
        public static Vector3 LastSampleCentre;
        public static bool LastSampleInView;
        public static string LastSampleWhy;
        public static int SamplesRefused;

        /// <summary>Units one group of this drop brings: a squad (amount members, else the squad size now), the heroes 2, a vehicle, gun or
        /// shuttle 1, a flag or an obstacle none.</summary>
        public static int UnitsOfGroup(string id, int amount)
        {
            switch (KindOf(id))
            {
                case UeKind.Robot: return amount > 0 ? Mathf.Clamp(amount, 1, 8) : SquadSize(id);
                case UeKind.Hero: return SquadSize(Heroes);
                case UeKind.Vehicle: case UeKind.Gun: case UeKind.Shuttle: return 1;
            }
            return 0;
        }

        /// <summary>DropCore, before each group: its units fit the cap (counted fresh: the groups before it count).</summary>
        static bool CapRoom(string id, Team team, int amount, out string why)
        {
            why = null;
            if (OzUeCap.Bypass) return true;
            int u = UnitsOfGroup(id, amount);
            if (u <= 0) return true;
            return OzUeCap.Room(AlwaysNeutral(id) ? Team.Neutral : team, u, true, out why);
        }

        /// <summary>CanDrop (the preview, once a frame): not even one group would fit.</summary>
        static bool CapRoomNow(string id, Team team, out string why)
        {
            why = null;
            int u = UnitsOfGroup(id, -1);
            return u <= 0 || OzUeCap.Room(AlwaysNeutral(id) ? Team.Neutral : team, u, false, out why);
        }

        /// <summary>The words of a drop the cap cut short (and the note for LOAD WITH UNITS).</summary>
        static string CapWords(string full, int made, int groups)
        {
            OzUeCap.Note(full);
            return made > 0 ? full + " (" + made + " OF " + groups + " PLACED)" : full;
        }

        /// <summary>SAMPLE FIGHT now on the field (a fight counts while one of its units stands).</summary>
        public static int SampleFightsOnField { get { Prune(); return fights.Count; } }

        static void Prune()
        {
            var w = W;
            if (w == null) { fights.Clear(); return; }
            for (int i = fights.Count - 1; i >= 0; i--)
            {
                bool any = false;
                foreach (int id in fights[i]) { var o = w.Get(id); if (o != null && !o.Destroyed && o.KillAt < 0) { any = true; break; } }
                if (!any) fights.RemoveAt(i);
            }
        }

        static void ResetCap() { fights.Clear(); LastSampleWhy = null; LastSampleInView = false; SamplesRefused = 0; OzUeCap.Reset(); }

        static int RefuseSample(string why)
        {
            LastSampleWhy = why; SamplesRefused++;
            OzUeToast.Show(why, true);
            AudioDirector.UI("ui_error");
            Debug.Log($"{OzUnitEditor.Tag} SAMPLE FIGHT refused: {why}");
            return 0;
        }

        /// <summary>The room a standing object takes (m); 0 = nothing that a fight must keep clear of.</summary>
        static float RoomOf(ZObject o)
        {
            if (o == null || o.Destroyed || o.KillAt >= 0) return 0f;
            if (IsObstacle(o)) return OzUePlan.ObstacleRoom;
            if (IsFlag(o)) return OzUePlan.FlagRoom;
            if (o.IsShuttle) return OzUePlan.ShuttleRoom;
            if (o.Kind == ZKind.Robot) return OzUePlan.RobotRoom;
            if (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) return OzUePlan.HardwareRoom;
            return 0f;
        }

        static readonly List<Vector3> standing = new List<Vector3>(256);   // x, z, room

        /// <summary>Every group's spot of a fight round c is inside the map, on open tiles and clear of everything standing.</summary>
        static bool FightFits(ZWorld w, Vector3 c, Vector3 right, Vector3 fwd)
        {
            float wM = w.Map.width * 2f, hM = w.Map.height * 2f;
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 6; i++)
                {
                    bool blue = s == 0; string id = blue ? OzUePlan.SampleBlue[i] : OzUePlan.SampleRed[i];
                    var k = KindOf(id); bool robot = k == UeKind.Robot;
                    var p = c + right * OzUePlan.SampleAcross(blue, i) + fwd * OzUePlan.SampleAlong(blue, i);
                    if (p.x < 4f || p.x > wM - 4f || p.z > -4f || p.z < -(hM - 4f)) return false;
                    int foot = robot ? 1 : 2;
                    var t = FootTile(w, p, foot);
                    for (int j = 0; j < foot; j++) for (int a = 0; a < foot; a++) if (!w.Path.TilePassable(t.x + a, t.y + j, robot)) return false;
                    if (OnAnObstacle(w, t.x, t.y, foot) || OnAPlatform(w, t.x, t.y, foot) || (robot && FlagOn(w, t.x, t.y))) return false;   // 1.3: on free ground only: never on a ramp platform
                    float room = OzUePlan.SpotRoom(robot);
                    for (int n = 0; n < standing.Count; n++)
                    {
                        var o = standing[n]; float dx = p.x - o.x, dz = p.z - o.y, need = room + o.z;
                        if (dx * dx + dz * dz < need * need) return false;
                    }
                }
            return true;
        }

        /// <summary>SAMPLE FIGHT on free ground (see the class): the groups made, 0 when refused.</summary>
        static int SampleFightFree()
        {
            var g = G; var w = W;
            if (g == null || w == null) return 0;
            LastSampleWhy = null;
            Prune();
            int max = OzUeCap.SampleFightsMax;
            if (fights.Count >= max) return RefuseSample(max + " SAMPLE FIGHTS ARE ON THE FIELD, THE MOST AT ONCE: CLEAR FIELD MAKES ROOM");
            int blueU = 0, redU = 0;
            for (int i = 0; i < 6; i++) { blueU += UnitsOfGroup(OzUePlan.SampleBlue[i], -1); redU += UnitsOfGroup(OzUePlan.SampleRed[i], -1); }
            if (!OzUeCap.Room(Team.Blue, blueU, true, out string why) || !OzUeCap.Room(Team.Red, redU, true, out why) || !OzUeCap.Room(Team.Neutral, blueU + redU, true, out why))
                return RefuseSample("SAMPLE FIGHT: " + why);
            // what stands on the field now
            standing.Clear();
            foreach (var o in w.Objects)
            {
                float r = RoomOf(o); if (r <= 0f) continue;
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY);
                standing.Add(new Vector3(p.x, p.z, r));
            }
            var view = ViewCentre(g);
            var rot = Quaternion.Euler(0f, CamYaw, 0f); Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
            var cam = Ed && Ed.Rts ? Ed.Rts.GetComponent<Camera>() : Camera.main;
            var ff = OzUeInsets.FreeFieldScreen;
            var tries = new List<(Vector3 c, float d, bool seen)>(300);
            for (int i = -8; i <= 8; i++)
                for (int j = -6; j <= 6; j++)
                {
                    var c = view + right * (i * OzUePlan.SampleStep) + fwd * (j * OzUePlan.SampleStep); c.y = 0f;
                    bool seen = false;
                    if (cam && ff.width > 1f) { var sp = cam.WorldToScreenPoint(c); seen = sp.z > 0f && ff.Contains(new Vector2(sp.x, sp.y)); }
                    tries.Add((c, i * i + j * j, seen));
                }
            tries.Sort((a, b) => a.seen != b.seen ? (a.seen ? -1 : 1) : a.d.CompareTo(b.d));
            int at = -1;
            for (int k = 0; k < tries.Count; k++) if (FightFits(w, tries[k].c, right, fwd)) { at = k; break; }
            if (at < 0) return RefuseSample("NO FREE GROUND FOR A SAMPLE FIGHT: CLEAR FIELD OR MAKE ROOM");
            var centre = tries[at].c;
            int made = 0; var ids = new List<int>(48);
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 6; i++)
                {
                    bool blue = s == 0;
                    var p = centre + right * OzUePlan.SampleAcross(blue, i) + fwd * OzUePlan.SampleAlong(blue, i);
                    var d = DropCore(blue ? OzUePlan.SampleBlue[i] : OzUePlan.SampleRed[i], blue ? Team.Blue : Team.Red, p, 1, false, -1);
                    made += d.groups;
                    foreach (var o in d.made) { ids.Add(o.Id); foreach (var m in o.Minions) if (m != null) ids.Add(m.Id); }
                }
            if (made == 0) return RefuseSample("NO FREE GROUND FOR A SAMPLE FIGHT: CLEAR FIELD OR MAKE ROOM");
            fights.Add(ids.ToArray());
            Samples++;
            LastSampleCentre = centre; LastSampleInView = tries[at].seen;
            OzSoundV3.CheatDrop("grunt", true, false, centre); AudioDirector.UI("ui_confirm");
            OzUeToast.Show("SAMPLE FIGHT " + fights.Count + " OF " + max + (LastSampleInView ? "" : ": ON FREE GROUND BESIDE THE VIEW (HOME SHOWS THE WHOLE FIELD)"));
            Debug.Log($"{OzUnitEditor.Tag} SAMPLE FIGHT at ({centre.x:0.0}, {centre.z:0.0}): {made} of 12 groups made, fight {fights.Count} of {max} on the field, {(LastSampleInView ? "in view" : "beside the view")}");
            return made;
        }

        // ------------------------------------------------------------------------------------------------ the cap bench
        static readonly string[] BenchMix = { "grunt", "psycho", "light", "grunt", "sniper", "medium", "tough", "pyro", "jeep", "laser", "gatling", "heavy", "grunt", "psycho", "missile_launcher", "howitzer" };

        /// <summary>The cap bench (View/OzUeCapBench.cs): an army of about `units` units of `team` in columns from `fromCol` on, 6 m apart,
        /// 18 groups a column across the depth, its front 10 m from the middle on the side `sign` (-1 left, +1 right: the fronts 20 m apart,
        /// in each other's range at once); placed past the cap,
        /// no log line per group. Returns the units made; nextCol = the next free column.</summary>
        internal static int BenchArmy(Team team, int units, Vector3 centre, Vector3 right, Vector3 fwd, float sign, int fromCol, out int nextCol)
        {
            var g = G; var w = W; nextCol = fromCol;
            if (g == null || w == null || units <= 0) return 0;
            const int Rows = 18; const float Step = 6f, Front = 10f;
            int made = 0, k = 0, row = 0, col = fromCol;
            while (made < units && k < 4000)
            {
                string id = BenchMix[k++ % BenchMix.Length];
                var p = centre + right * (sign * (Front + col * Step)) + fwd * ((row - (Rows - 1) * 0.5f) * Step); p.y = 0f;
                if (++row >= Rows) { row = 0; col++; }
                var o = MakeOne(g, w, id, team, p, -1, out _);
                if (o == null) continue;
                made += 1 + o.Minions.Count;
                RaiseSpawned(o);
            }
            nextCol = col + (row > 0 ? 1 : 0);
            MarkChanged();
            return made;
        }
    }
}
