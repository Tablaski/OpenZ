using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: the sim's hits, damage, times and random stream are untouched.
    /// <para>
    /// under them, drew the round as two straight legs over a waypoint up to 9 m high: up the wall, then bent over the edge. Rockets and shells
    /// did the same through ShotClearance.LiftGravity: their arc grew as much as it had to, up to 9 m.</para>
    /// <para>The rule now: a direct shot flies one line from the muzzle to its impact. <see cref="Stop"/> finds the first thing on that line
    /// that stops it. Walls always do: a platform's drawn body (<see cref="InBody"/>: the walk surface stands 1 m inside it, so the walk layer
    /// is not its face), the real top of a live fort's mesh, a standing L16 pyramid. The walk layer (terrain, banks, bridge decks) stops a line
    /// only where it rises <see cref="LowBankM"/> or more over it: under that the line stays straight and passes. A shot at a target on a fort
    /// (a tower gun, a roof defender) is not stopped by that fort (FortGunView.MeshTopAt). The round ends there, with that surface's impact.
    /// A clear line reaches its target exactly as before. Rockets and shells (<see cref="Direct"/>) keep their own arc and speed and end where
    /// that arc first meets something (<see cref="Cut(ref ZEvent, ZObject, FxWeapon, Vector3, ref Vector3)"/>): they burst there on the frame
    /// they arrive (<see cref="Step"/>) and the sim's blast later draws nothing more (<see cref="Take"/>), though the target still takes it.</para>
    /// <para>What the player sees when the sim counts a hit and the line ended on the wall: sparks on the wall, the target's bar still falls and
    /// it can die. The sim has no height (ZSight.ExemptOf: a unit on a deck or a tower can be fired at from below); the view does not hide that.</para>
    /// <para>Not here: howitzers, MLRS, grenades and the fort's big gun keep their arcs (ShotClearance.LiftGravity, as before), and so do shells and
    /// rockets out of a gun on a fort tower (<see cref="OnFort"/>: their arc clears their own fort). A flame stream keeps its small arc over a bank
    /// up to <see cref="FlameLiftM"/> and stops at anything higher (<see cref="FlameArc"/>). A fort never stops a shooter standing on it
    /// (FortGunView.MeshTopAt).</para>
    /// <para>shots_over_walls: one end of a shot stands on a platform or a fort and its line runs through that same body. In that one case the shot is not stopped and
    /// every part of it draws with the depth test off, so it stays visible the whole way, as in a 2D game. <see cref="OverWalls"/> false = the path before this rule.</para>
    /// </summary>
    public static class ShotLine
    {
        /// <summary>A round: the last metres of the line are not tested (m: its target stands there), the first ones not by height (m: the muzzle).</summary>
        public const float RoundEndM = 0.3f, MuzzleM = 0.5f;
        /// <summary>A rocket or shell: its blast covers this much (m), a stop this near its end is no stop.</summary>
        public const float MissileEndM = 1.5f;
        /// <summary>Under a height by this much (m) counts as inside it.</summary>
        public const float Tol = 0.03f;
        /// <summary>A shell's arc may still grow by this much (m at its top): the flat-ground lift, kept to the pixel.</summary>
        public const float SkimM = 0.15f;
        /// <summary>The highest arc (m over the straight stream) a flame keeps over a bank.</summary>
        public const float FlameLiftM = 1.5f;
        /// <summary>The walk layer stops a flight only where it rises this much (m) over it. A lower bank is passed, straight.</summary>
        public const float LowBankM = 1.2f;
        /// <summary>A muzzle inside a platform's drawn wall (a robot is drawn up to 2 m off his sim spot, 1 m of that past the walk surface): the
        /// line counts as going into the wall when it is still inside this far on (m); the wall's face is looked for this far behind him.</summary>
        public const float MuzzleDeepM = 1.5f, FaceBehindM = 2f;
        public const int Samples = 14, ArcChords = 8;
        const float BankStepM = 0.3f, LeastShare = 0.02f;
        const int BankSteps = 48, Banks = 4;
        /// <summary>Counts for the checks: rounds tested, rounds stopped, rockets and shells cut, bursts drawn at a cut.</summary>
        public static int Rounds, Stopped, Cuts, WallBlasts;
        /// <summary>The last Stop ended on something (a hit it was drawing is no hit).</summary>
        public static bool LastStopped;
        /// <summary>The check's ear: every tested round (from, to, end, stopped). Null in play.</summary>
        public static Action<Vector3, Vector3, Vector3, bool> Seen;
        /// <summary>The check's ear: where a rocket or shell was cut. Null in play.</summary>
        public static Action<Vector3> CutSeen;
        /// <summary>shots_over_walls, THE SWITCH: false = every shot takes the path it took before this rule, to the pixel. Editor row: OpenZ/V1.3/Shots Over Walls: On.</summary>
        public static bool OverWalls = true;
        /// <summary>The last Stop, Cut or FlameArc went on through the body one of its ends stands on: every part of that shot draws over everything.</summary>
        public static bool LastOver;
        /// <summary>StandsOn: how far past a deck's rim a muzzle still stands on it, how far under the deck past that rim it can hang (m).</summary>
        public const float StandEdgeM = 1.5f, StandDropM = 0.5f;
        /// <summary>Crosses: the samples on a platform's body and on a fort's mesh (m).</summary>
        public const float BodyStepM = 0.25f, FortStepM = 0.5f;
        /// <summary>Counts for the checks: second walks run, lines and flights that went over, misses lifted onto a deck.</summary>
        public static int SecondWalks, OverLines, OverFlights, OverLifts;
        /// <summary>The check's ear: every line that went over (from, end). Null in play.</summary>
        public static Action<Vector3, Vector3> OverSeen;

        // ------------------------------------------------------------------ the path (pure: the off-project proof runs these)
        /// <summary>The first thing on a straight piece from a, dist m along it; wall = it stops a flight whatever its height.</summary>
        public delegate bool RayFn(Vector3 a, Vector3 dir, float len, out float dist, out bool wall);

        /// <summary>A point of the flight: s = share of the flight, lift = g T^2 / 2 (0 = the straight line).</summary>
        public static Vector3 Point(Vector3 from, Vector3 d, float lift, float s)
        {
            var p = from + d * s; p.y += lift * s * (1f - s); return p;
        }

        /// <summary>A world point inside a platform's drawn body: its footprint, under its deck or ramp, the kerb's height over the kerb's band.</summary>
        public static bool InBody(ZWorld.PlatformInfo i, Vector3 p)
        {
            i.Local(p.x * 8.0, -p.z * 8.0, out double a, out double l);
            double w = ZWorld.PlatformWideTiles, k = OzUePlatformView.KerbWide;
            if (a < 0.0 || a > w || l < 0.0 || l > i.Rows) return false;
            double top = i.HeightAt(l) + (a < k || a > w - k || l < k ? OzUePlatformView.KerbM : 0.0);
            return p.y < top - Tol;
        }

        /// <summary>A ray against a platform's drawn body (the rules' own walk through it, ZWorld.PlatformInfo.RayOnBody): exact on a wall's face,
        /// within its 0.1 m step on a deck or ramp.</summary>
        public static bool RayBody(ZWorld.PlatformInfo i, Vector3 a, Vector3 dir, float len, out float dist)
        {
            bool hit = i.RayOnBody(a.x * 8.0, -a.z * 8.0, a.y, dir.x * 8.0, -dir.z * 8.0, dir.y, len, OzUePlatformView.KerbM, OzUePlatformView.KerbWide, out double t, out _, out _);
            dist = (float)t; return hit;
        }

        /// <summary>The muzzle is inside this platform's drawn wall and the line goes on into it: share (under 0) = the wall's face behind the muzzle
        /// on the same line, so the impact shows on the face. False = it is not, or the line comes straight out.</summary>
        public static bool FaceBehind(ZWorld.PlatformInfo i, Vector3 from, Vector3 to, out float share)
        {
            share = 0f;
            var d = to - from; float len = d.magnitude;
            if (len < 1e-3f || !InBody(i, from)) return false;
            var n = d / len;
            if (!InBody(i, from + n * Mathf.Min(MuzzleDeepM, len)) || !RayBody(i, from - n * FaceBehindM, n, FaceBehindM, out float t)) return false;
            share = (t - FaceBehindM) / len; return true;
        }

        /// <summary>The first point of a flight from -> to that stops it, as a share of it. A wall stops it at once; the walk layer only when it rises
        /// LowBankM over the flight, measured from where the flight goes in to where it comes out: else the flight passes and goes on. The last
        /// endM are left out, the first MuzzleM out of the height samples. False = nothing stops it.</summary>
        public static bool Walk(Vector3 from, Vector3 to, float lift, float endM, RayFn ray, Func<Vector3, bool> wall, Func<Vector3, float> ground, out float share, out bool onWall)
        {
            share = 1f; onWall = false;
            var d = to - from; float h = Mathf.Sqrt(d.x * d.x + d.z * d.z), len = d.magnitude;
            if (len < endM + 0.1f) return false;
            float end = 1f - endM / len, near = h > 1e-3f ? Mathf.Min(MuzzleM / h, end) : 0f, start = 0f;
            float step = Mathf.Max(BankStepM / len, 1f / BankSteps);
            for (int bank = 0; bank < Banks; bank++)
            {
                if (!First(from, d, lift, start, Mathf.Max(near, start), end, ray, wall, ground, out float s, out onWall)) return false;
                if (onWall) { share = s; return true; }
                float q = s + step;
                for (; q < end; q += step)
                {
                    var p = Point(from, d, lift, q);
                    if (wall(p)) { share = q; onWall = true; return true; }
                    float over = ground(p) - p.y;
                    if (over >= LowBankM) { share = s; return true; }
                    if (over <= 0f) break;
                }
                if (q >= end) return false;
                start = q;
            }
            return false;
        }

        /// <summary>The first solid point of a flight from `start` on: rays along it first (exact, thin edges), then height samples up to there (a
        /// bisection finds the edge).</summary>
        static bool First(Vector3 from, Vector3 d, float lift, float start, float near, float end, RayFn ray, Func<Vector3, bool> wall, Func<Vector3, float> ground, out float share, out bool onWall)
        {
            share = 1f; onWall = false;
            float best = 2f; int chords = lift > 0f ? ArcChords : 1;
            bool In(float s) { var p = Point(from, d, lift, s); return wall(p) || p.y < ground(p) - Tol; }
            var a = Point(from, d, lift, start);
            for (int k = 0; k < chords; k++)
            {
                float s0 = start + (end - start) * k / chords, s1 = start + (end - start) * (k + 1) / chords;
                var b = Point(from, d, lift, s1); var seg = b - a; float m = seg.magnitude;
                if (m > 1e-3f && ray(a, seg / m, m, out float dist, out bool w)) { best = s0 + (s1 - s0) * dist / m; onWall = w; break; }
                a = b;
            }
            float stop = Mathf.Min(best, end), prev = near;
            for (int i = 1; i <= Samples && stop > near; i++)
            {
                float s = near + (stop - near) * i / Samples;
                if (!In(s)) { prev = s; continue; }
                float lo = prev, hi = s;
                for (int j = 0; j < 10; j++) { float mid = (lo + hi) * 0.5f; if (In(mid)) hi = mid; else lo = mid; }
                best = hi; onWall = wall(Point(from, d, lift, hi)); break;
            }
            if (best > 1f) return false;
            share = best; return true;
        }

        // ------------------------------------------------------------------ over a wall (pure: the off-project proof runs these)
        /// <summary>A point of a unit standing on this platform: over its footprint and on its surface, up to the tallest thing that stands there. A barrel can
        /// reach StandEdgeM past a deck's rim and hang StandDropM under it. Past a ramp's side a muzzle is over the floor and fires out, so it cannot cross that ramp.
        /// Over a ramp's rail (the walk surface starts WalkInset in) stands nobody: a muzzle there is a floor robot's beside the ramp, whose slope can be lower than his barrel.</summary>
        public static bool StandsOn(ZWorld.PlatformInfo i, Vector3 p)
        {
            i.Local(p.x * 8.0, -p.z * 8.0, out double a, out double l);
            double deckEnd = ZWorld.PlatformBackRows + ZWorld.PlatformDeckRows, m = StandEdgeM / 2.0, side = l <= deckEnd ? m : 0.0;
            if (a < -side || a >= ZWorld.PlatformWideTiles + side || l < -m || l >= i.Rows) return false;
            if (l > deckEnd && (a <= OzUePlatformShape.WalkInset || a >= ZWorld.PlatformWideTiles - OzUePlatformShape.WalkInset)) return false;
            double f = i.HeightAt(l);
            return p.y >= f - StandDropM && p.y <= f + OzUePlatformViews.TallM;                         // RUN7 U4: over the deck too (a psycho's or pyro's muzzle is drawn up to 0.13 m under it)
        }

        /// <summary>The platforms a shot may pass: a = the one its shooter stands on, b = the one its end stands on. known: targetOn = the platform under the
        /// target (0 = none), so a miss that lands on a deck does not make a floor target stand there. No target: `to` itself.</summary>
        public static void MaskOf(IReadOnlyList<ZWorld.PlatformInfo> ps, Vector3 from, Vector3 to, bool known, int targetOn, out ZWorld.PlatformInfo a, out ZWorld.PlatformInfo b)
        {
            a = b = null;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (a == null && StandsOn(p, from)) a = p;
                if (b == null && (known ? p.O.Id == targetOn : StandsOn(p, to))) b = p;
            }
        }

        static IReadOnlyList<ZWorld.PlatformInfo> overPs; static ZWorld.PlatformInfo overA, overB, overEnd; static Vector3 overFrom;
        static Func<Vector3, bool> overRest; static Func<Vector3, float> overGround;
        static bool Masked(ZWorld.PlatformInfo p) => ReferenceEquals(p, overA) || ReferenceEquals(p, overB);
        static bool InFootprint(ZWorld.PlatformInfo i, Vector3 p)
        {
            i.Local(p.x * 8.0, -p.z * 8.0, out double a, out double l);
            return a >= 0.0 && a <= ZWorld.PlatformWideTiles && l >= 0.0 && l <= i.Rows;
        }

        static readonly RayFn RayOver = (Vector3 a, Vector3 dir, float len, out float dist, out bool wall) =>
        {
            dist = len; wall = false;
            for (int i = 0; i < overPs.Count; i++)
                if (!Masked(overPs[i]) && !InBody(overPs[i], a) && RayBody(overPs[i], a, dir, dist, out float t)) { dist = t; wall = true; }
            return wall;
        };

        static readonly Func<Vector3, bool> WallOver = p =>
        {
            for (int i = 0; i < overPs.Count; i++)
                if (!Masked(overPs[i]) && InBody(overPs[i], p) && !(InBody(overPs[i], overFrom) && (p - overFrom).sqrMagnitude < MuzzleDeepM * MuzzleDeepM)) return true;
            return overRest(p);
        };

        static readonly Func<Vector3, float> GroundOver = p =>
        {
            for (int i = 0; i < overPs.Count; i++) if (Masked(overPs[i]) && InFootprint(overPs[i], p)) return float.MinValue;
            return overGround(p);
        };

        /// <summary>The second walk, on a line the first one stopped, with the bodies a and b left out. No physics ray: a masked walk mesh needs none, and the height
        /// samples still see every bank. True = something else stops it (another platform, a tall bank, a fort, a pyramid): share, onWall as Walk gives them.</summary>
        public static bool StopsAgain(IReadOnlyList<ZWorld.PlatformInfo> ps, ZWorld.PlatformInfo a, ZWorld.PlatformInfo b, Vector3 from, Vector3 to, float lift, float endM,
                                      Func<Vector3, bool> rest, Func<Vector3, float> ground, out float share, out bool onWall)
        {
            overPs = ps; overA = a; overB = b; overFrom = from; overRest = rest; overGround = ground;
            share = 1f; onWall = false; bool again = false;
            for (int i = 0; i < ps.Count && !again; i++)
                if (!Masked(ps[i]) && FaceBehind(ps[i], from, to, out share)) again = onWall = true;
            if (!again) again = Walk(from, to, lift, endM, RayOver, WallOver, GroundOver, out share, out onWall);
            overA = overB = null;
            return again;
        }

        /// <summary>The first walk stopped this line. True = it stays stopped (not the situation: share and onWall as they were; or something else stops it). False = it
        /// goes on to its end, LastOver true. targetOn under 0: the shot is at a platform itself, which masks nothing.</summary>
        public static bool Decide(IReadOnlyList<ZWorld.PlatformInfo> ps, Vector3 from, Vector3 to, float lift, float endM, bool known, int targetOn,
                                  Func<Vector3, bool> rest, Func<Vector3, float> ground, ref float share, ref bool onWall)
        {
            LastOver = false; overEnd = null;
            if (!OverWalls || ps.Count == 0 || targetOn < 0) return true;
            MaskOf(ps, from, to, known, targetOn, out var a, out var b);
            if (a == null && b == null) return true;
            SecondWalks++;
            if (StopsAgain(ps, a, b, from, to, lift, endM, rest, ground, out float s, out bool w)) { share = s; onWall = w; return true; }
            overEnd = b; share = 1f; onWall = false; LastOver = true;
            return false;
        }

        /// <summary>The end of a shot that went over a wall, at the platform its end stands on (null = none, so a shooter's own platform never moves it). A miss aimed at
        /// a deck's outer metre reads the floor and comes out under the deck: it goes up onto the deck, true. onIt: `at` lies on that platform.</summary>
        public static bool OnDeck(ZWorld.PlatformInfo end, ref Vector3 at, out bool onIt)
        {
            onIt = end != null && InFootprint(end, at);
            if (!onIt) return false;
            end.Local(at.x * 8.0, -at.z * 8.0, out _, out double l);
            float deck = (float)end.HeightAt(l);
            if (at.y >= deck - Tol) return false;
            at.y = deck;
            return true;
        }

        /// <summary>Samples stepM apart along a flight, up to RoundEndM before its end: one of them is inside. No allocation with a static delegate.</summary>
        public static bool Crosses(Vector3 from, Vector3 to, float lift, float stepM, Func<Vector3, bool> inside)
        {
            var d = to - from; float len = d.magnitude, end = len - RoundEndM;
            for (float m = stepM; m < end; m += stepM) if (inside(Point(from, d, lift, m / len))) return true;
            return false;
        }

        static readonly Func<Vector3, bool> InMasked = p => (overA != null && InBody(overA, p)) || (overB != null && InBody(overB, p));

        /// <summary>The same question with no walk, for a shot that has no Stop: an end stands on a platform and the line runs through that body.</summary>
        public static bool ThroughBody(IReadOnlyList<ZWorld.PlatformInfo> ps, Vector3 from, Vector3 to, float lift, bool known, int targetOn)
        {
            if (ps.Count == 0 || targetOn < 0) return false;
            MaskOf(ps, from, to, known, targetOn, out overA, out overB);
            bool on = (overA != null || overB != null) && Crosses(from, to, lift, BodyStepM, InMasked);
            overA = overB = null;
            return on;
        }

        const int EndSlots = 16;
        static readonly Vector3[] overEnds = new Vector3[EndSlots]; static readonly bool[] overEndSet = new bool[EndSlots]; static int overEndAt;
        /// <summary>Cut passed this flight over a wall: its end is kept for its Launch, which can come later (the Tough) and from another muzzle.</summary>
        public static void Remember(Vector3 to) { overEnds[overEndAt] = to; overEndSet[overEndAt] = true; overEndAt = (overEndAt + 1) % EndSlots; }
        /// <summary>The Launch of a flight Cut passed: its end is on the list, and leaves it.</summary>
        public static bool Recall(Vector3 to)
        {
            for (int i = 0; i < EndSlots; i++)
                if (overEndSet[i] && (overEnds[i] - to).sqrMagnitude < 1e-6f) { overEndSet[i] = false; return true; }
            return false;
        }

        // ------------------------------------------------------------------ the world
        static Vector3 muzzle, aimEnd, hardAt; static ZObject aimed; static bool hard, hardSet;
        static readonly ZWorld.PlatformInfo[] noPlatforms = new ZWorld.PlatformInfo[0];
        static IReadOnlyList<ZWorld.PlatformInfo> Platforms => ZGame.I && ZGame.I.World != null ? ZGame.I.World.Platforms : noPlatforms;

        static readonly RayFn Ray = (Vector3 a, Vector3 dir, float len, out float dist, out bool wall) =>
        {
            dist = len; wall = false;
            var ps = Platforms;
            for (int i = 0; i < ps.Count; i++)
                if (!InBody(ps[i], a) && RayBody(ps[i], a, dir, dist, out float t)) { dist = t; wall = true; }
            if (ZGame.WalkLayer < 0 || !Physics.Raycast(a, dir, out var h, dist, 1 << ZGame.WalkLayer, QueryTriggerInteraction.Ignore)) return wall;
            dist = h.distance; wall = ps.Count > 0 && h.collider.GetComponentInParent<OzUePlatformView>();
            return true;
        };

        static readonly Func<Vector3, bool> Wall = p =>
        {
            var ps = Platforms;
            for (int i = 0; i < ps.Count; i++)
                if (InBody(ps[i], p) && !(InBody(ps[i], muzzle) && (p - muzzle).sqrMagnitude < MuzzleDeepM * MuzzleDeepM)) return true;
            return p.y < FortGunView.MeshTopAt(p, muzzle, aimEnd, aimed) - Tol || L16Pyramid.Inside(p);
        };

        static readonly Func<Vector3, float> Ground = p => ZGame.I ? ZGame.I.GroundY(p.x, p.z) : float.MinValue;

        static readonly Func<Vector3, bool> Rest = p => p.y < FortGunView.MeshTopAt(p, muzzle, aimEnd, aimed) - Tol || L16Pyramid.Inside(p);

        /// <summary>The platform under a known target, 0 = none, under 0 = the target is a platform. A flying ship over a deck stands on nothing.</summary>
        static int TargetOn(ZObject t)
        {
            if (t == null) return 0;
            if (t.Type == ZWorld.PlatformType) return -1;
            return !t.ShuttleAirborne && ZGame.I && ZGame.I.World != null ? ZGame.I.World.PlatformAtTile(t.CenterX >> 4, t.CenterY >> 4) : 0;
        }

        static bool Walk(Vector3 from, Vector3 to, float lift, float endM, ZObject target, ZObject on, out float share)
        {
            LastOver = false; overEnd = null;
            if (!WalkToday(from, to, lift, endM, target, out share)) return false;
            var t = target ?? on;
            return Decide(Platforms, from, to, lift, endM, t != null, TargetOn(t), Rest, Ground, ref share, ref hard);
        }

        static bool WalkToday(Vector3 from, Vector3 to, float lift, float endM, ZObject target, out float share)
        {
            muzzle = from; aimEnd = to; aimed = target; hard = false; share = 1f;
            var ps = Platforms;
            for (int i = 0; i < ps.Count; i++) if (FaceBehind(ps[i], from, to, out share)) { hard = true; return true; }
            return Walk(from, to, lift, endM, Ray, Wall, Ground, out share, out hard);
        }

        /// <summary>OpenZFxPlayer.SurfaceAt: this point is the last stop, and it was on a platform, a fort or a pyramid (concrete and stone: rock's
        /// chips and sparks; the map tile under it says sand).</summary>
        public static bool HardAt(Vector3 p) => hardSet && (p - hardAt).sqrMagnitude < 1e-6f;

        // ------------------------------------------------------------------ rounds
        /// <summary>The straight round from -> to at `target`: the first solid thing on its line. False = the line is clear, `to` stands.</summary>
        public static bool Stop(Vector3 from, Vector3 to, ZObject target, out Vector3 at) => Stop(from, to, target, null, out at);

        /// <summary>on: who the line is at when the caller keeps `target` null for the fort test: read by the situation only.</summary>
        static bool Stop(Vector3 from, Vector3 to, ZObject target, ZObject on, out Vector3 at)
        {
            at = to; Rounds++; float s = 1f; LastOver = false;
            LastStopped = ZGame.I && Walk(from, to, 0f, RoundEndM, target, on, out s);
            if (LastStopped) { at = Vector3.LerpUnclamped(from, to, s); Stopped++; hardAt = at; hardSet = hard; }
            else if (LastOver)
            {
                if (OnDeck(overEnd, ref at, out bool onIt)) { LastStopped = true; OverLifts++; }
                if (onIt) { hardAt = at; hardSet = true; }
            }
            else if (OverWalls && ZGame.I) LastOver = FortGunView.CrossesOwn(from, to, 0f, target);
            if (LastOver) { OverLines++; OverSeen?.Invoke(from, at); }
            Seen?.Invoke(from, to, at, LastStopped);
            return LastStopped;
        }

        /// <summary>FxManager.Flame: its pools have no twin that draws over a wall, so a hatch flame stops as it always did.</summary>
        public static bool StopPlain(Vector3 from, Vector3 to, ZObject target, out Vector3 at)
        {
            bool on = OverWalls; OverWalls = false;
            bool stopped = Stop(from, to, target, out at);
            OverWalls = on;
            return stopped;
        }

        /// <summary>A shot with no Stop of its own (the flame, the pellets): an end stands on a platform or a fort and the line runs through that body.</summary>
        public static bool Through(Vector3 from, Vector3 to, float lift, ZObject target)
        {
            if (!OverWalls || !ZGame.I) return false;
            return ThroughBody(Platforms, from, to, lift, target != null, TargetOn(target)) || FortGunView.CrossesOwn(from, to, lift, target);
        }

        /// <summary>A twin rocket's end when the main round went over a wall: at the main end's height, never at the wall's foot. Else `y` unchanged. atGun: the main end is at a
        /// fort tower gun, where OzTowerAim.SalvoY has already put the twin on the fort's top or beside the gun: it stays there.</summary>
        public static float TwinY(Vector3 to, float y, bool atGun) => LastOver && !atGun ? Mathf.Max(y, to.y) : y;

        /// <summary>The twin's end as OzShuttleAim.SalvoEndY gives it, then TwinY. SalvoY counts each end it placed at a tower gun: that count is its own near test.</summary>
        public static float TwinY(Vector3 to, Vector3 p, float groundY)
        {
            int up = OzTowerAim.SalvoUp;
            float y = OzShuttleAim.SalvoEndY(to, p, groundY);
            return TwinY(to, y, OzTowerAim.SalvoUp != up);
        }

        /// <summary>OpenZFxPlayer.Launch, once per flight: Cut passed it over a platform, or it runs under the mesh of a fort one of its ends stands on.</summary>
        public static bool FlightOver(Vector3 from, Vector3 to, float lift)
        {
            if (!OverWalls) return false;
            if (!Recall(to) && (OzAirShots.AimedAloft(to) || !FortGunView.CrossesOwn(from, to, lift, null))) return false;
            OverFlights++;
            return true;
        }

        /// <summary>OpenZPyroFire, 7 x a second: the flame stream's arc over a low bank (ShotClearance.ArcHeight, as before); when it would have to
        /// climb more than FlameLiftM the stream stays straight and stopM = how far it reaches (else -1).</summary>
        public static float FlameArc(Vector3 from, Vector3 to, ZObject target, out float stopM)
        {
            stopM = -1f;
            LastOver = Through(from, to, 0f, target);
            if (LastOver) { OverLines++; OverSeen?.Invoke(from, to); return 0f; }                     // shots_over_walls: straight to the target, before any arc
            float h = ShotClearance.ArcHeight(from, to);
            if (h <= FlameLiftM) return h;
            if (Stop(from, to, null, target, out var at)) stopM = (at - from).magnitude;
            return 0f;
        }

        // ------------------------------------------------------------------ rockets and shells
        /// <summary>Direct fire by the rules (ZObject.ArcingFire). The fort's big gun is drawn as "howitzer".</summary>
        public static bool Direct(string type) => type != "howitzer" && type != "missile_launcher";

        /// <summary>This muzzle is on a fort: its shells and rockets keep their arc over their own fort (ShotClearance.LiftGravity).</summary>
        public static bool OnFort(Vector3 from) => FortGunView.StandsOnFort(from);

        /// <summary>OpenZFxPlayer.Launch raises an arc for arcing fire only, and keeps the old skim of a low shell over its own ground point (a few cm
        /// at the top of the arc, ground it clears anyway): lifted = the raised gravity, T = the real flight.</summary>
        public static bool RaisesArc(FxWeapon w, Vector3 from, float g, float lifted, float T) =>
            w == FxWeapon.Grenade || w == FxWeapon.Mlrs || (lifted - g) * T * T / 8f <= SkimM || OnFort(from);

        /// <summary>A rocket or shell from -> to over simSeconds, on its own arc: when the arc meets something, `to` becomes that point and
        /// simSeconds its part of the flight. The same g through the same two points in that time is the same arc, so its first part flies
        /// unchanged. False = clear.</summary>
        public static bool Cut(Vector3 from, ref Vector3 to, ref float simSeconds, FxWeapon w, ZObject target, ZObject on = null)
        {
            LastOver = false;
            if (!ZGame.I || OnFort(from)) return false;
            float T = OpenZFxPlayer.RealFlight(simSeconds, ZGame.I.gameSpeed), lift = OpenZFxCatalog.GravityOf(w) * T * T * 0.5f;
            if (!Walk(from, to, lift, MissileEndM, target, on, out float s))
            {
                if (LastOver) Remember(to);
                else if (OverWalls) LastOver = FortGunView.CrossesOwn(from, to, lift, target);
                if (LastOver) { OverLines++; OverSeen?.Invoke(from, to); }
                return false;
            }
            to = Point(from, to - from, lift, s); simSeconds *= Mathf.Max(s, LeastShare); Cuts++;
            CutSeen?.Invoke(to);
            return true;
        }

        struct Walled { public ZEvent e; public ZWorld world; public double due, simEnd; public Vector3 at; public bool drawn; }
        static readonly List<Walled> walled = new List<Walled>();

        /// <summary>The sim's round: as above, and its burst goes on the list. e.value and e.end become those of the shortened flight, so all that
        /// reads the event after this (the launch, its sounds, a rocket's end frame) follows the cut.</summary>
        public static void Cut(ref ZEvent e, ZObject t, FxWeapon w, Vector3 from, ref Vector3 to)
        {
            LastOver = false;
            var world = ZGame.I ? ZGame.I.World : null;
            if (world == null || world.Time >= e.end) return;
            float left = e.value;
            if (!Cut(from, ref to, ref left, w, t)) return;
            double simEnd = e.end;
            e.value = left; e.end = world.Time + left;
            walled.Add(new Walled { e = e, world = world, due = e.end, simEnd = simEnd, at = to });
        }

        /// <summary>CombatView.LateUpdate: a cut round that arrived bursts where it stopped.</summary>
        public static void Step(ZWorld w, FxManager fx, CombatAudio au)
        {
            for (int i = 0; i < walled.Count; i++)
            {
                var s = walled[i];
                if (s.world != w || w.Time > s.simEnd + 1.0) { walled.RemoveAt(i--); continue; }
                if (s.drawn || w.Time < s.due) continue;
                s.drawn = true; walled[i] = s; Burst(s, fx, au);
            }
        }

        /// <summary>CombatView, MissileExploded: true = this was a cut round, its burst was drawn where it stopped (the blast's other work goes on).</summary>
        public static bool Take(ZEvent e, FxManager fx, CombatAudio au)
        {
            for (int i = 0; i < walled.Count; i++)
            {
                var s = walled[i];
                if (s.e.objId != e.objId || s.e.x != e.x || s.e.y != e.y) continue;
                walled.RemoveAt(i);
                if (!s.drawn) Burst(s, fx, au);
                return true;
            }
            return false;
        }

        static void Burst(Walled s, FxManager fx, CombatAudio au)
        {
            var b = s.e; b.type = ZEventType.MissileExploded; b.otherId = -1; b.flag = false; b.flag2 = false;
            LegacyMatch.MissileBlast(b, s.world.GetAny(b.objId), null, s.at, fx, au);
            WallBlasts++;
        }
    }
}
