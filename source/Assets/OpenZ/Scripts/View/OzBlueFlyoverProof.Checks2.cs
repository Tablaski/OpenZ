#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// beyond the play area, the frozen units). Hooked into OzBlueFlyoverProof (k75c): Begin2 when the level has loaded, Watch2 every frame
// of the flight, HandedBack2 at the hand-back, Tail2 (a take runs on until the start lines have played), Checks2 before the report.
//  START LINES  the sound log (OzSoundLog) from the level load: NO start sting, announcer start line or robot game-start line plays
//               before the hand-back; after it the three come in order (sting, announcer, robot), as the red start has them.
//  FRAMING      every frame: rays through the edge of the VISIBLE picture (between the letterbox bars: 16 points) meet the ground (a
//               real ray cast on the level's ground colliders, ZGame.GroundPoint: the canyon walls and the rim floor stop a ray where
//               the picture shows them) and that ground lies inside the play bounds (RtsCamera.mapSizeMeters): shot 2 (the world) and
//               shot 3 (the red fort) strictly (0 frames with ground outside the map or the horizon in the picture); shot 4 no farther
//               outside than the game's own start view shows at the landing; shot 1 (the sunrise: the horizon is in the picture by the
//               sun's nature) keeps its foreground on the map and reports the band. Shot 2 brings the red fort into the picture by the
//               end of its glide (in the frame through its last quarter).
//  UNITS ALIVE  every robot the camera shows advances its animation during the flight (the Animator's state time, counted per robot),
//               and the antics director starts antics under the hold (AnticDirector.Flyover.cs; the starts and the robots that did
//               one are counted and named).
// Test only (editor). The checks join the proof's own PASS / FAIL list; the numbers go to the report.
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBlueFlyoverProof
    {
        public const string Part2 = "k75c2";
        /// <summary>The start sounds of a level (AudioDirector.PlayStartLines): the sting, the announcer's start line (one of two), the robot's line.</summary>
        static readonly string[] StartKeys = { "ter_game_start_sfx", "ann_level_start", "ann_battle_start", "vo_game_start" };
        /// <summary>Shot 2 (and shot 3 where the band below is 0): every edge ray's ground at least this far inside the map (m).</summary>
        public static float InsideMargin = 2f;
        /// <summary>Shot 3: how far the picture's edge may reach past the map's edge (m): the legacy's own boundary band where the red fort stands near
        /// the edge (the Desert's canyon wall, the Jungle's 24 m rim floor with its jungle); 0 = the whole picture on the map (the Car Park).</summary>
        public static float RimBandOf(string level) => level == "L02" ? 20f : level == "L16" ? 20f : 0f;
        /// <summary>How long the checks wait for the three start lines after the hand-back (s of real time).</summary>
        public static float LinesWait = 14f;
        /// <summary>A take runs on this long after the robot's game-start line began, counted in the take's own frames (the longest robot start
        /// line is 1.5 s; the take runs slower than real time, so real seconds would cut it short).</summary>
        public static float LineTail = 2.2f;

        float t02 = -1f, handBackAt2 = -1f, homeReach = -1f, scan2 = -1f; int log02, released02, timedOut02, early2;
        readonly List<bool> red2Seq = new List<bool>();
        readonly HashSet<string> earlySeen = new HashSet<string>(); readonly StringBuilder earlyPlays = new StringBuilder();
        readonly int[] frames2 = new int[8], bad2 = new int[8], sky2 = new int[8]; readonly float[] worstOut2 = new float[8], minMargin2 = new float[8];
        readonly Dictionary<int, (float first, float last, int frames, int moved)> animSeen = new Dictionary<int, (float, float, int, int)>();
        readonly HashSet<int> anticRobots = new HashSet<int>(); readonly List<string> anticLines = new List<string>();
        int anticLog0, flyoverStarts0; readonly int[] anticsInShot = new int[8];
        static readonly Vector2[] EdgePts = EdgePoints();

        static Vector2[] EdgePoints()
        {
            float b = BarBottom + 0.004f, t = 1f - BarTop - 0.004f;
            var l = new List<Vector2>();
            foreach (float x in new[] { 0.004f, 0.25f, 0.5f, 0.75f, 0.996f }) { l.Add(new Vector2(x, b)); l.Add(new Vector2(x, t)); }
            foreach (float y in new[] { 0.25f, 0.5f, 0.75f }) { l.Add(new Vector2(0.004f, Mathf.Lerp(b, t, y))); l.Add(new Vector2(0.996f, Mathf.Lerp(b, t, y))); }
            return l.ToArray();
        }

        // ================================================================================================================ hooks
        /// <summary>The level has loaded (All): the clocks of the sound log and the counters.</summary>
        void Begin2()
        {
            t02 = Time.unscaledTime; log02 = OzSoundLog.Count; handBackAt2 = -1f; homeReach = -1f; scan2 = -1f; early2 = 0; earlySeen.Clear(); earlyPlays.Length = 0;
            for (int i = 0; i < 8; i++) { frames2[i] = bad2[i] = sky2[i] = 0; worstOut2[i] = 0f; minMargin2[i] = 1e9f; anticsInShot[i] = 0; }
            animSeen.Clear(); anticRobots.Clear(); anticLines.Clear(); red2Seq.Clear();
            anticLog0 = AnticDirector.Log.Count; flyoverStarts0 = AnticDirector.FlyoverStarts;
            released02 = ZodFlyover.BlueHoldsReleasedByFlight; timedOut02 = ZodFlyover.BlueHoldsTimedOut;     // this run's own releases (the counters live for the Play session)
            Log(Part2 + ": " + ZodFlyover.BlueHoldStatus());
        }

        /// <summary>Every frame of the flight (Watch): the picture's ground rays and the robots' animation.</summary>
        void Watch2(int shot)
        {
            ScanEarly2();                                                                                  // a start sound under the flight is caught on its frame (the sound log is a ring)
            if (!cam || !game || shot < 0 || shot >= 8) return;
            frames2[shot]++;
            // the picture's edge on the ground
            bool sky = false; float worst = 0f, margin = 1e9f;
            foreach (var e in EdgePts)
            {
                var ray = cam.ViewportPointToRay(new Vector3(e.x, e.y, 0f));
                if (!GroundOf(ray, out var p)) { sky = true; continue; }
                worst = Mathf.Max(worst, OutsideBy(p)); margin = Mathf.Min(margin, EdgeMargin(p));
            }
            if (sky) sky2[shot]++;
            worstOut2[shot] = Mathf.Max(worstOut2[shot], worst); if (!sky) minMargin2[shot] = Mathf.Min(minMargin2[shot], margin);
            if (shot == 1 && (sky || worst > 0f || margin < InsideMargin)) bad2[shot]++;
            if (shot == 2)
            {
                float allow = RimBandOf(lv);
                if (sky || worst > allow || (allow <= 0f && (worst > 0f || margin < InsideMargin))) bad2[shot]++;
            }
            if (shot == 0) Foreground0();
            if (shot == 1) red2Seq.Add(RedFortInFrame2());
            // the robots on screen: their animation time
            foreach (var v in game.Views.Values)
            {
                if (!v || v.Obj == null || v.Obj.Kind != ZKind.Robot || v.Obj.Destroyed) continue;
                var vp = cam.WorldToViewportPoint(v.transform.position);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < BarBottom || vp.y > 1f - BarTop) continue;
                var an = v.GetComponentInChildren<Animator>(); if (!an || !an.isInitialized) continue;
                float nt = an.GetCurrentAnimatorStateInfo(0).normalizedTime;
                if (animSeen.TryGetValue(v.Obj.Id, out var s)) animSeen[v.Obj.Id] = (s.first, nt, s.frames + 1, s.moved + (Mathf.Abs(nt - s.last) > 1e-4f ? 1 : 0));
                else animSeen[v.Obj.Id] = (nt, nt, 1, 0);
                if (v.AnticActive && anticRobots.Add(v.Obj.Id)) anticsInShot[shot]++;
            }
        }

        /// <summary>The hand-back (the flight ended, the game object on again): the clock the start lines are judged against.</summary>
        void HandedBack2()
        {
            ScanEarly2();
            handBackAt2 = Time.unscaledTime;
            if (rts && cam) homeReach = ViewReach();                                                       // the start view itself, measured where it stands at the hand-back
            Log(Part2 + $": hand-back at {handBackAt2:F2} s (unscaled), {handBackAt2 - t02:F2} s after the level loaded; " + ZodFlyover.BlueHoldStatus());
        }

        /// <summary>A take runs on after the hand-back until the robot's game-start line has played (so the MP4 carries the start lines in
        /// their place): LineTail s of the take's own frames after that line began; at most three times LinesWait s of real time.</summary>
        IEnumerator Tail2()
        {
            float t0 = Time.realtimeSinceStartup; int lineFrame = -1, want = Mathf.CeilToInt(LineTail * Fps);
            while (Time.realtimeSinceStartup - t0 < LinesWait * 3f)
            {
                if (lineFrame < 0 && StartLinePlays().ContainsKey("vo_game_start")) lineFrame = frameNo;
                if (lineFrame >= 0 && frameNo - lineFrame >= want) break;
                yield return null;
            }
            Log(Part2 + (lineFrame >= 0 ? $": the take ran on {frameNo - lineFrame} frames ({(frameNo - lineFrame) / (float)Fps:F2} s of the take) after the robot's start line began" : ": the robot's start line did not come within the take's tail"));
            yield return Real(0.2f);
        }

        // ================================================================================================================ the checks
        IEnumerator Checks2()
        {
            if (t02 < 0f || !game) yield break;
            // ---- the start lines
            if (handBackAt2 >= 0f)
            {
                float w0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - w0 < LinesWait && StartLinePlays().Count < 3) yield return null;
            }
            var plays = StartLinePlays();
            var all = OzSoundLog.Since(t02);
            var sb = new StringBuilder();
            foreach (var e in all) if (IsStartKey(e.key) || e.key == "ann_briefing_start") sb.Append($" | {e.t:F2} {e.src} {e.key} {e.outcome}");
            Log(Part2 + ": start sounds in the sound log since the level loaded (" + t02.ToString("F2") + " s):" + (sb.Length > 0 ? sb.ToString() : " none"));
            if (handBackAt2 >= 0f)
            {
                foreach (var e in all) if (IsStartKey(e.key) && e.outcome != null && e.outcome.StartsWith("PLAY") && e.t < handBackAt2 - 0.01f && earlySeen.Add(e.key + "@" + e.t.ToString("F4"))) { early2++; earlyPlays.Append($" {e.key} at {e.t - t02:F2} s"); }
                int before = early2;
                Check(lv + $": no start sting or start line played before the hand-back ({handBackAt2 - t02:F2} s after the level load){(before > 0 ? ": PLAYED EARLY" + earlyPlays : "")}", before == 0);
                plays.TryGetValue("ter_game_start_sfx", out float ts); plays.TryGetValue("vo_game_start", out float ta);
                float tr = 0f; bool rachel = plays.TryGetValue("ann_battle_start", out tr) || plays.TryGetValue("ann_level_start", out tr);
                bool sting = plays.ContainsKey("ter_game_start_sfx"), alan = plays.ContainsKey("vo_game_start");
                bool order = sting && rachel && alan && ts >= handBackAt2 - 0.01f && tr >= ts - 0.15f && ta > tr;
                Check(lv + $": after the hand-back the start sting ({(sting ? (ts - handBackAt2).ToString("+0.00") : "-")} s), the announcer's start line ({(rachel ? (tr - handBackAt2).ToString("+0.00") : "-")} s) and the robot's game-start line ({(alan ? (ta - handBackAt2).ToString("+0.00") : "-")} s) played in that order, as the red start has them", order);
                int rel = ZodFlyover.BlueHoldsReleasedByFlight - released02, tout = ZodFlyover.BlueHoldsTimedOut - timedOut02;
                Check(lv + $": the blue hold released the lines by the flight's end (this run: released by the flight {rel}, timed out {tout}; " + ZodFlyover.BlueHoldStatus() + ")", rel > 0 && tout == 0);
            }
            // ---- the framing
            if (rts && cam && homeReach < 0f) homeReach = ViewReach();                                    // the start view's own picture at the landing
            string[] names = ZodShotList.BlueShotNames;
            for (int i = 0; i < 4; i++)
            {
                if (frames2[i] == 0) continue;
                string mm = minMargin2[i] < 1e8f ? minMargin2[i].ToString("F1") + " m inside" : "-";
                string line = $"{lv}: shot {i + 1} {names[i]}: {frames2[i]} frames, ground outside the map in {bad2[i]} frames (farthest {worstOut2[i]:F1} m out), the horizon in the picture in {sky2[i]} frames, least edge margin {mm}";
                if (i == 1) Check(line + " (must be 0 frames outside, no horizon)", bad2[i] == 0 && sky2[i] == 0);
                else if (i == 2) Check(line + (RimBandOf(lv) > 0f ? $" (the red fort stands near the map's edge here: at most {RimBandOf(lv):0} m past it, the legacy's own boundary band; no horizon)" : " (must be 0 frames outside, no horizon)"), bad2[i] == 0 && sky2[i] == 0);
                else if (i == 3) Check(line + $" (at most what the game's own start view shows: {homeReach:F1} m beyond the edge)", sky2[i] == 0 && worstOut2[i] <= Mathf.Max(0f, homeReach) + 2f);
                else Log("INFO " + line + " (the sunrise shot: the horizon is in the picture for the sun; its foreground is the map)");
            }
            if (frames2[0] > 0) Check(lv + ": shot 1 keeps its foreground on the map (the bottom edge's ground inside the map in every frame)", bottomOk0 == 0);
            // ---- the units
            int seen = 0, moved = 0; var still = new StringBuilder();
            foreach (var kv in animSeen)
            {
                if (kv.Value.frames < 10) continue;
                seen++;
                bool ok = kv.Value.moved >= kv.Value.frames / 2 && (Mathf.Abs(kv.Value.last - kv.Value.first) > 0.01f || kv.Value.moved >= kv.Value.frames * 9 / 10);   // a clip time that advanced on 90 %+ of the frames is advancing (its first and last values can meet by chance on a looping segment)
                if (ok) moved++; else still.Append(" #" + kv.Key);
            }
            Check(lv + $": every robot the camera showed advanced its animation during the flight ({moved} of {seen} robots seen 10+ frames{(still.Length > 0 ? "; STILL:" + still : "")})", seen > 0 && moved == seen);
            int starts = AnticDirector.FlyoverStarts - flyoverStarts0;
            var al = AnticDirector.Log; var kinds = new StringBuilder();
            for (int i = anticLog0; i < al.Count; i++) if (al[i].Contains("start ")) kinds.Append(" | " + al[i]);
            Log(Part2 + ": antics under the flyover:" + (kinds.Length > 0 ? kinds.ToString() : " none") + $"; per shot {anticsInShot[0]}/{anticsInShot[1]}/{anticsInShot[2]}/{anticsInShot[3]} robots seen in one; " + AnticDirector.FlyoverStatus());
            if (job != Job.SkipTest) Check(lv + $": the antics director started antics under the flyover's hold ({starts} starts, {anticRobots.Count} robots seen in one; at least 2 starts)", starts >= 2);
            else Log(Part2 + $": the skip test's short flight: {starts} antic start(s) under the hold (not judged)");
            Check(lv + ": the red start's hold, lines and shots are untouched, its robots play the flyover antics too now (FlyoverAnticsRed on, item 100), and the simulation did not tick under the flight", AnticDirector.FlyoverAnticsRed && game.World != null);
        }

        int bottomOk0;

        /// <summary>The frame trace jobs (Trace): the start sounds of this start so far. A RED start: none within the trace's window (Commander
        /// Zod's briefing holds them until it hands over: the red start is as it was). A BLUE start without a flyover driver: they play at once, as
        /// before (logged); with one, the hold applies.</summary>
        void TraceLines2()
        {
            if (t02 < 0f) return;
            var plays = StartLinePlays(); var sb = new StringBuilder();
            foreach (var kv in plays) sb.Append($" {kv.Key} at {kv.Value - t02:F2} s");
            Log(Part2 + ": start sounds within the trace (" + (Time.unscaledTime - t02).ToString("F1") + " s since the level load):" + (sb.Length > 0 ? sb.ToString() : " none"));
            if (job == Job.TraceRed)
                Check("RED start: no start sting or start line played while the red briefing held them (" + (ZodBriefing.BriefingActive ? "the briefing is still on" : "the briefing is over") + "): the red start unchanged" + (plays.Count > 0 ? ": PLAYED" + sb : ""),
                      plays.Count == 0 || !ZodBriefing.BriefingActive);
            else Log(Part2 + ": BLUE start without a flight driver: " + (plays.Count > 0 ? "the start sounds played as before (no flyover to wait for)" : "no start sound yet") + "; " + ZodFlyover.BlueHoldStatus());
        }

        // ================================================================================================================ helpers
        static bool IsStartKey(string k) { foreach (var s in StartKeys) if (s == k) return true; return false; }

        /// <summary>Until the hand-back: every start sound that PLAYED since the level load (from half a second before the proof's clock) is
        /// noted on the frame it comes, so a long sound log (a ring of its last entries) cannot drop it before the checks read it.</summary>
        void ScanEarly2()
        {
            if (t02 < 0f || handBackAt2 >= 0f) return;
            foreach (var e in OzSoundLog.Since(scan2 < 0f ? t02 - 0.5f : scan2))
                if (IsStartKey(e.key) && e.outcome != null && e.outcome.StartsWith("PLAY") && earlySeen.Add(e.key + "@" + e.t.ToString("F4"))) { early2++; earlyPlays.Append($" {e.key} at {e.t - t02:F2} s"); }
            scan2 = Time.unscaledTime;
        }

        /// <summary>The start sounds that PLAYED since the level loaded: key -> unscaled time of the first play.</summary>
        Dictionary<string, float> StartLinePlays()
        {
            var d = new Dictionary<string, float>();
            foreach (var e in OzSoundLog.Since(t02))
                if (IsStartKey(e.key) && e.outcome != null && e.outcome.StartsWith("PLAY") && !d.ContainsKey(e.key)) d[e.key] = e.t;
            return d;
        }

        /// <summary>Where a ray meets the level's ground: a ray cast on the ground colliders (ZGame.GroundPoint: the walk layer, the terrain,
        /// then the flat plane beyond them), so a canyon wall or the rim floor stops it where the picture shows it; false = the sky.</summary>
        bool GroundOf(Ray ray, out Vector3 p)
        {
            p = default;
            if (ray.direction.y >= -1e-3f) return false;
            return game.GroundPoint(ray, out p);
        }

        /// <summary>Watch2, shot 2: the red fort's middle (3 m over its ground, as Watch counts it) inside the visible picture.</summary>
        bool RedFortInFrame2()
        {
            Vector3 red = Vector3.zero; int n = 0;
            foreach (var o in game.World.Objects)
            {
                if (o == null || !o.IsFort || o.Owner != Team.Red) continue;
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY); p.y = game.TerrainY(p.x, p.z) + 3f;
                red += p; n++;
            }
            return n > 0 && InFrame(red / n);
        }

        /// <summary>The shot 2 rule of the glide over the map: the red fort comes into the picture by the end of the glide, in the frame in at
        /// least 90 % of the shot's last quarter (the whole-map shot it replaces held it in 90 % of all its frames).</summary>
        bool Shot2RedFort(out string says)
        {
            int n = red2Seq.Count, last = n > 0 ? Mathf.Max(1, n / 4) : 0, inAll = 0, inLast = 0;
            for (int i = 0; i < n; i++) if (red2Seq[i]) { inAll++; if (i >= n - last) inLast++; }
            says = $"the glide brings it in by its end: in {inLast} of the last quarter's {last} frames, {inAll} of {n} in all; at least 90 % of the last quarter";
            return n > 0 && inLast >= 0.9f * last;
        }

        float OutsideBy(Vector3 p)
        {
            var s = rts ? rts.mapSizeMeters : new Vector2(144f, 180f);
            float dx = Mathf.Max(0f, -p.x, p.x - s.x), dz = Mathf.Max(0f, p.z, -s.y - p.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        float EdgeMargin(Vector3 p)
        {
            var s = rts ? rts.mapSizeMeters : new Vector2(144f, 180f);
            return Mathf.Min(p.x, s.x - p.x, -p.z, s.y + p.z);
        }

        /// <summary>The game's own start view (the camera where it is now): how far beyond the map's edge its picture reaches (m; 0 = inside).</summary>
        float ViewReach()
        {
            float worst = 0f;
            foreach (var e in EdgePts)
            {
                var ray = cam.ViewportPointToRay(new Vector3(e.x, Mathf.Lerp(0.004f, 0.996f, (e.y - BarBottom) / (1f - BarTop - BarBottom)), 0f));   // the start view has no bars
                if (GroundOf(ray, out var p)) worst = Mathf.Max(worst, OutsideBy(p));
            }
            return worst;
        }

        /// <summary>Watch2's foreground rule of shot 1: the bottom edge's ground inside the map.</summary>
        void Foreground0()
        {
            if (!cam) return;
            foreach (var e in EdgePts)
            {
                if (e.y > BarBottom + 0.01f) continue;
                var ray = cam.ViewportPointToRay(new Vector3(e.x, e.y, 0f));
                if (!GroundOf(ray, out var p) || OutsideBy(p) > 0f) { bottomOk0++; return; }
            }
        }
    }
}
#endif
