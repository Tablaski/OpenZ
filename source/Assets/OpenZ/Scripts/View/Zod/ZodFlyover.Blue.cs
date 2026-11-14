// OpenZ (GPL-3.0)
// commands the blue fort, the CPU red) flies its own cinematic shot list at the start, under the blue briefing dialog, exactly as a red
// level flies Zod's: ZodShotList.For(level) answers the BLUE list of the level while the side is blue (B02 / B16 / B17), and everything of
// ZodFlyover (Begin, PlanCine, the cut anchors, the dip, the hand-back, the skip, the painted sky, the jungle's low look) runs unchanged.
// Every blue list has the same four beats, cut on the breaks of the dialog (the sidekick asks, the general answers, the sidekick asks
// where, the general names the red fort):
//   1 blue_base_dawn  from OUR BLUE BASE: a low camera on the sun-opposite side of the blue fort looking across it into the rising sun
//                     (the disc with its bloom in the top part of the frame: the sun's place is in the frame from the first frame,
//                     the painted sky that draws the disc loads and fades in with the flight, about 1 s), one slow push-in
//   2 the_world       "TAKE OVER THE WORLD!": the whole map high from the blue side, the red fort far at the top, a slow push and drift
//                     (the jungle: a high glide south from over the north edge down the middle road to the red fort)
//   3 red_fort        "Where do we start?" - "THAT RED FORT! PAINT IT BLUE!": the red fort from a low three-quarter angle, a slow push-in
//                     with a 12 deg orbit, a short dip and wind swell into it
//   4 blue_home       "NOW MOVE!": our blue fort from behind, craning up into the RTS start camera of the blue level (lands at rest a
//                     moment after the last word; the hand-back has no jump)
// New here: the subject OwnFortDawn (shot 1: it resolves itself on the map at Begin, like EnemyFort and OwnBaseHome, so it fits every
// level and the real sun), the blue lists in the data file (B02 / B16 / B17, BOpen / BLastMin / BMaxTotal: tune in Play, ZodShotList.ToJson),
// BlueBriefingLine (the approved recording's line breaks and anchor words, for the briefing that drives the flight and for the proofs)
// and PlanBlue. The red lists and their flights are not touched.
// THE ENTRY POINT for the blue briefing (the cutscene kit): do what ZodBriefing.BriefingRoutine does, with the blue line:
//   fly = new ZodFlyover(rts, game); fly.VoiceOn = () => voice.isPlaying; fly.Begin("overview");          // the blue list by itself
//   ZodShotList.BriefingRoom(game.level, line, ref preroll, voiceEnd, ref end);                           // the quiet opening, the last shot's room
//   fly.PlanCine(preroll, end, line, clock);   or   fly.PlanBlue(preroll, end, clock);                  // the cuts on the line breaks
//   every frame: fly.Tick(dt);  at the end: fly.HandBack(skipped); wait until fly.HomeError <= 0.04 (at most 1.6 s); fly.End();
// `line` = ZodShotList.BlueBriefingLine() (the approved take's cue starts and the words "where" / "now"), or the briefing's own ZodLine with
// FIVE cues (line 4 split before "NOW MOVE!") or word times for "where" and "now". Begin must come after the blue level has placed its
// camera (OzPlayBlueLevel.Placed, execution order -55); begun earlier (a component of order -60 that flies in its Start), Begin places it
// itself the same way and OzPlayBlueLevel's own Start is switched off (BlueBeforeBegin), so the home pose is the blue start view.
// Play checks: ZodFlyover.Status(), ZodFlyover.LastPlan, ZodShotList.LastRoom, ZodCinePreview.Play() in a blue level, the menu
// OpenZ/V1.2/Blue Flyovers (View/OzBlueFlyoverProof.cs).
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class ZodCineShotDef
    {
        /// <summary>OwnFortDawn: the camera's height over the fort's ground at the start / end of the shot (m).</summary>
        public float heightA = 24f, heightB = 22f;
    }

    public sealed partial class ZodBriefingLookFile
    {
        /// <summary>The blue shot lists of the three levels (empty = the code lists) and their open / lastMin / maxTotal (0 = the code numbers).</summary>
        public ZodCineShotDef[] B02, B16, B17;
        public float BOpen, BLastMin, BMaxTotal;
    }

    public sealed partial class ZodShotList
    {
        /// <summary>The side the shot list is resolved for: -1 = the level's own side (OzPlayBlue.Active), 0 = red, 1 = blue (checks, previews).</summary>
        public static int ForceBlueList = -1;
        /// <summary>For(level) answers the blue list of the level.</summary>
        public static bool BlueSide => ForceBlueList < 0 ? OzPlayBlue.Active : ForceBlueList == 1;

        /// <summary>The blue list of a level (any other level: the Desert's blue list).</summary>
        public static ZodShotList ForBlue(string level) => level == "L17" ? B17 : level == "L16" ? B16 : OzVolcanic.Is(level) ? B07 : B02;

        /// <summary>The names of the four blue beats, in order (checks).</summary>
        public static readonly string[] BlueShotNames = { "blue_base_dawn", "the_world", "red_fort", "blue_home" };

        // the three shots every blue list shares (the red fort, our fort into the start camera) and the opening of each list
        static ZodCineShotDef RedFortShot() => new ZodCineShotDef { name = "red_fort", subject = ZodCineSubject.EnemyFort, seconds = 4.2f, yawA = 34f, yawB = 22f,
            pitchA = 18f, pitchB = 14f, distA = 96f, distB = 74f, fovA = 28f, fovB = 28f, frameYaw = 5f, aimUp = 4f, dip = 0.3f, whoosh = true, cutWord = "where", cutAt = 0, cutCue = 4 };
        static ZodCineShotDef BlueHomeShot() => new ZodCineShotDef { name = "blue_home", subject = ZodCineSubject.OwnBaseHome, seconds = 2.0f, yawA = 16f, pitchA = 32f,
            distA = 76f, fovA = 30f, aimUp = 3f, ease = ZodCineEase.InOut, cutWord = "now", cutAt = 0, cutCue = 5 };
        const float BlueOpen = 0.9f, BlueLastMin = 2.0f, BlueMaxTotal = 20f;

        /// <summary>Desert, 144 x 180 m, as blue: our fort top-left (38, -26), the red fort at the bottom (106, -155), the sun ESE. Every pose
        /// below was resolved off-line on the map's numbers (k75c tools/shot_math.py) before it came into the game.</summary>
        public static ZodShotList B02 = new ZodShotList
        {
            level = "L02B", open = BlueOpen, lastMin = BlueLastMin, maxTotal = BlueMaxTotal,
            shots = new[]
            {
                // 1. our base at dawn: from the north-west corner (the map's edge, 22 m up) across our fort into the low sun, a slow push-in
                new ZodCineShotDef { name = "blue_base_dawn", subject = ZodCineSubject.OwnFortDawn, seconds = 3.2f, yawA = -10f, yawB = -10f, distA = 40f, distB = 27f,
                    heightA = 22f, heightB = 20f, pitchA = 15f, pitchB = 16f, fovA = 52f, fovB = 50f, floor = 6f },
                //    the red fort, one slow glide south and down; the whole picture between the bars stays on the map (k75c2 tools/shot_math2.py)
                new ZodCineShotDef { name = "the_world", subject = ZodCineSubject.Free, seconds = 4.7f, camA = new Vector3(71f, 110f, -55f), camB = new Vector3(71f, 103.4f, -75f),
                    lookA = new Vector3(71f, 0f, -118.5f), lookB = new Vector3(71f, 0f, -134.7f), fovA = 28f, fovB = 28f, floor = 10f, cutWord = "same", cutAt = 1, cutCue = 2 },
                // 3. the red fort from the west, a high three-quarter with a 12 deg orbit and a push-in (the fort stands 25 m from the south edge:
                //    the picture's edge reaches at most 20 m past the map's edge, into the canyon wall that closes the map), the dip and the wind swell
                new ZodCineShotDef { name = "red_fort", subject = ZodCineSubject.Point, seconds = 4.2f, point = new Vector2(106f, -155f), yawA = 80f, yawB = 92f,
                    pitchA = 48f, pitchB = 44f, distA = 96f, distB = 74f, fovA = 28f, fovB = 28f, frameYaw = 5f, aimUp = 4f, dip = 0.3f, whoosh = true, cutWord = "where", cutAt = 0, cutCue = 4 },
                // 4. our fort from behind, craning up into the start camera (no farther past the map's edge than that camera itself shows)
                new ZodCineShotDef { name = "blue_home", subject = ZodCineSubject.OwnBaseHome, seconds = 2.0f, yawA = -5f, pitchA = 40f, distA = 72f, fovA = 30f, aimUp = 3f,
                    ease = ZodCineEase.InOut, cutWord = "now", cutAt = 0, cutCue = 5 },
            }
        };

        /// <summary>Jungle, 208 x 256 m, as blue: our fort at the top (104, -30), the red fort at the bottom (104, -229), the middle road between
        /// them; the north-west pool west of our fort, the north-east pool with the pyramid east of it.</summary>
        public static ZodShotList B16 = new ZodShotList
        {
            level = "L16B", open = BlueOpen, lastMin = BlueLastMin, maxTotal = BlueMaxTotal,
            shots = new[]
            {
                // 1. our base at dawn: from the north-west pool's west bank (26 m up) across our fort into the low sun over the east swamp
                //    (k75c2: turned 8 deg further round the fort and 1 deg down, so the picture's bottom edge stays on the map; the fort and the sun where they were)
                new ZodCineShotDef { name = "blue_base_dawn", subject = ZodCineSubject.OwnFortDawn, seconds = 3.2f, yawA = -20f, yawB = -20f, distA = 60f, distB = 52f,
                    heightA = 26f, heightB = 23f, pitchA = 13f, pitchB = 14f, fovA = 48f, fovB = 48f, floor = 6f },
                // 2. the world: high over the middle road looking south: the canyons, the red fort at the top; one slow glide south and down, the whole
                //    picture on the map (the land beyond the rim, held back under the flight, is never in it)
                new ZodCineShotDef { name = "the_world", subject = ZodCineSubject.Free, seconds = 4.7f, camA = new Vector3(104f, 125f, -97f), camB = new Vector3(104f, 117.5f, -117f),
                    lookA = new Vector3(104f, 0f, -178.2f), lookB = new Vector3(104f, 0f, -193.3f), fovA = 34f, fovB = 34f, floor = 10f, cutWord = "same", cutAt = 1, cutCue = 2 },
                // 3. the red fort from the east, front-lit by the sunrise, a three-quarter with a 12 deg orbit and a push-in (the fort stands 27 m from
                //    the south edge: the picture's edge reaches at most 20 m past the map's edge, onto the rim's own jungle floor; the fort left of
                //    centre, the view turned away from that edge), the dip and the wind swell
                new ZodCineShotDef { name = "red_fort", subject = ZodCineSubject.Point, seconds = 4.2f, point = new Vector2(104f, -229f), yawA = 285f, yawB = 273f,
                    pitchA = 36f, pitchB = 32f, distA = 104f, distB = 82f, fovA = 28f, fovB = 28f, frameYaw = -5f, aimUp = 4f, dip = 0.3f, whoosh = true, cutWord = "where", cutAt = 0, cutCue = 4 },
                // 4. our fort from behind, craning up into the start camera
                new ZodCineShotDef { name = "blue_home", subject = ZodCineSubject.OwnBaseHome, seconds = 2.0f, yawA = 0f, pitchA = 44f, distA = 72f, fovA = 30f, aimUp = 3f,
                    ease = ZodCineEase.InOut, cutWord = "now", cutAt = 0, cutCue = 5 },
            }
        };

        /// <summary>Car Park, 220 x 256 m, as blue: our fort on the left (48, -52), the red fort on the right (172, -83), the canal between.</summary>
        public static ZodShotList B17 = new ZodShotList
        {
            level = "L17B", open = BlueOpen, lastMin = BlueLastMin, maxTotal = BlueMaxTotal,
            shots = new[]
            {
                // 1. our base at dawn: from the north-west corner of the city (30 m up) across our fort into the low sun, a slow push-in
                new ZodCineShotDef { name = "blue_base_dawn", subject = ZodCineSubject.OwnFortDawn, seconds = 3.2f, yawA = 20f, yawB = 20f, distA = 58f, distB = 52f,
                    heightA = 30f, heightB = 27f, pitchA = 13f, pitchB = 14f, fovA = 50f, fovB = 50f, floor = 6f },
                //    A slow cinematic turn, 15 deg round the middle of the map (yaw 97 -> 82, pitch 59 -> 57)
                //    with a push-in (138 -> 120 m) and a drift east, moving from the first frame, the Soft ease; the whole picture on the map at every
                //    moment (at least 6 m inside its edges; W/run10_l17_world/tools/world_math.py). Was a 20 m glide at one heading (camA 43,120,-128).
                new ZodCineShotDef { name = "the_world", subject = ZodCineSubject.Free, seconds = 4.7f, camA = new Vector3(41.5f, 118.3f, -123.3f), camB = new Vector3(61.3f, 100.6f, -133.1f),
                    lookA = new Vector3(112f, 0f, -132f), lookB = new Vector3(126f, 0f, -124f), fovA = 40f, fovB = 40f, floor = 10f, cutWord = "same", cutAt = 1, cutCue = 2 },
                // 3. the red fort from the south-south-east looking up the map, front-lit, a three-quarter with a 12 deg orbit and a push-in; the whole
                //    picture on the map; the dip and the wind swell
                new ZodCineShotDef { name = "red_fort", subject = ZodCineSubject.Point, seconds = 4.2f, point = new Vector2(172f, -83f), yawA = 345f, yawB = 333f,
                    pitchA = 38f, pitchB = 34f, distA = 96f, distB = 74f, fovA = 28f, fovB = 28f, frameYaw = 5f, aimUp = 4f, dip = 0.3f, whoosh = true, cutWord = "where", cutAt = 0, cutCue = 4 },
                // 4. our fort from behind, craning up into the start camera
                new ZodCineShotDef { name = "blue_home", subject = ZodCineSubject.OwnBaseHome, seconds = 2.0f, yawA = 5f, pitchA = 38f, distA = 76f, fovA = 30f, aimUp = 3f,
                    ease = ZodCineEase.InOut, cutWord = "now", cutAt = 0, cutCue = 5 },
            }
        };

        /// <summary>EnsureFile: the blue part of the data file.</summary>
        static void LoadBlue(ZodBriefingLookFile f)
        {
            if (f == null) return;
            if (f.B02 != null && f.B02.Length > 0) B02.shots = f.B02;
            if (f.B16 != null && f.B16.Length > 0) B16.shots = f.B16;
            if (f.B17 != null && f.B17.Length > 0) B17.shots = f.B17;
            foreach (var l in new[] { B02, B16, B17 })
            {
                if (f.BOpen > 0f) l.open = f.BOpen;
                if (f.BLastMin > 0f) l.lastMin = f.BLastMin;
                if (f.BMaxTotal > 0f) l.maxTotal = f.BMaxTotal;
            }
            Debug.Log($"[ZodFlyover] briefing data: blue shots L02 {Count(B02)} L16 {Count(B16)} L17 {Count(B17)}, opening {B02.open:F2} s, last shot at least {B02.lastMin:F2} s, at most {B02.maxTotal:F1} s in all");
        }

        static int Count(ZodShotList l) => l != null && l.shots != null ? l.shots.Length : 0;

        /// <summary>ToJson: the blue part.</summary>
        static void SaveBlue(ZodBriefingLookFile f)
        {
            if (f == null) return;
            f.B02 = B02.shots; f.B16 = B16.shots; f.B17 = B17.shots; f.BOpen = B02.open; f.BLastMin = B02.lastMin; f.BMaxTotal = B02.maxTotal;
        }

        // ---------------------------------------------------------------------------------------- the blue briefing's line breaks
        /// <summary>The approved blue briefing take (one file, the four lines with 0.4 s pauses), measured: the start of each spoken part (s from
        /// the voice start), the words the cuts hang on, and the end of the last word. Cues: 1 the sidekick's question, 2 the general's answer,
        /// 3 the sidekick's two questions (the cut on "where" inside it), 4 "THAT RED FORT! PAINT IT BLUE!" (9.00 - 11.21 s), 5 "NOW MOVE!"
        /// (11.42 - 11.97 s; "PAINT" starts at 10.33, "NOW" at 11.42: read from the take's loudness and its word times).</summary>
        public static readonly float[] BlueCueStarts = { 0.10f, 2.48f, 5.58f, 9.00f, 11.42f };
        public static readonly string[] BlueCueTexts =
        {
            "Sidekick: What's the mission today, sir?", "EvilZod: SAME AS ALWAYS! TAKE OVER THE WORLD!", "Sidekick: The whole world? Where do we start?",
            "EvilZod: THAT RED FORT! PAINT IT BLUE!", "EvilZod: NOW MOVE!",
        };
        public const float BlueWordWhere = 7.24f, BlueWordNow = 11.42f, BlueVoiceEnd = 11.98f, BlueTakeLength = 12.19f;

        /// <summary>The blue briefing as a ZodLine for the flyover's cut anchors: five cues and the two anchor words at the approved take's
        /// times (duration = the end of the last word). A briefing with its own clip passes its own line to PlanCine instead.</summary>
        public static ZodLine BlueBriefingLine() => BlueLine(BlueCueStarts, BlueVoiceEnd, BlueCueTexts, new[] { "where", "now" }, new[] { BlueWordWhere, BlueWordNow });

        /// <summary>A ZodLine from cue starts (s from the voice start, ascending), the voice end, optional cue texts and word times.</summary>
        public static ZodLine BlueLine(float[] cueStarts, float voiceEnd, string[] texts = null, string[] words = null, float[] wordTimes = null)
        {
            var line = new ZodLine { id = "blue_brief", group = "briefing", duration = voiceEnd };
            int n = cueStarts != null ? cueStarts.Length : 0;
            line.cues = new ZodCue[n];
            for (int i = 0; i < n; i++)
                line.cues[i] = new ZodCue { text = texts != null && i < texts.Length ? texts[i] : "", start = cueStarts[i], end = i + 1 < n ? cueStarts[i + 1] : voiceEnd };
            int w = words != null && wordTimes != null ? Mathf.Min(words.Length, wordTimes.Length) : 0;
            line.words = new ZodWord[w];
            for (int i = 0; i < w; i++) line.words[i] = new ZodWord { word = words[i], start = wordTimes[i], end = wordTimes[i] + 0.3f };
            return line;
        }
    }

    public sealed partial class ZodFlyover
    {
        /// <summary>OwnFortDawn: the camera never nearer the map's edge than this (m), and never nearer the fort's centre than this (m).</summary>
        public static float FortDawnMargin = 8f, FortDawnMinDist = 12f;
        /// <summary>Checks: the times Begin placed a blue level's camera itself (the flight began before OzPlayBlueLevel.Start).</summary>
        public static int BluePlacedEarly { get; private set; }

        /// <summary>PlanCine with the approved blue briefing's line breaks (ZodShotList.BlueBriefingLine).</summary>
        public void PlanBlue(float preroll, float end, Func<float> clock) => PlanCine(preroll, end, ZodShotList.BlueBriefingLine(), clock);

        /// <summary>Shot 1 of a blue list: the camera on the sun-opposite side of the player's fort (its bearing from the fort = the anti-sun heading
        /// + yaw), dist m out but never past the map's inset edge (FortDawnMargin), height m over the fort's ground; the view heading midway
        /// between the fort and the sun (+ frameYaw), so the fort sits at +yaw/2 and the sun disc at -yaw/2 of the frame, pitch deg under the
        /// horizon (the sun disc, drawn 1.4 deg over it, lands pitch + 1.4 deg over the frame's centre line).</summary>
        CinePose FortDawn(ZodCineShotDef d, bool end, Vector3 myFort, float sunYaw, Vector2 size)
        {
            float side = end ? d.yawB : d.yawA, want = end ? d.distB : d.distA, height = end ? d.heightB : d.heightA, down = end ? d.pitchB : d.pitchA, fov = end ? d.fovB : d.fovA;
            float bearing = sunYaw + 180f + side;
            var dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
            float dist = Mathf.Max(FortDawnMinDist, Mathf.Min(want, EdgeDistance(myFort, dir, size, FortDawnMargin)));
            var cam = myFort + dir * dist; cam.y = GroundAt(myFort) + height;
            float toFort = bearing + 180f, heading = toFort + 0.5f * Mathf.DeltaAngle(toFort, sunYaw) + d.frameYaw;
            var look = cam + Quaternion.Euler(down, heading, 0f) * Vector3.forward * 200f;
            return new CinePose { pos = cam, look = look, focus = new Vector3(myFort.x, 0f, myFort.z), fov = fov };
        }

        /// <summary>Begin, before the home pose is read: a blue level whose camera placement (OzPlayBlueLevel.Start, order -55) has not run yet
        /// gets it now, the same way (OzSides.StartFocus on the forts' middles), and that Start is switched off so it is not applied twice.
        /// Nothing happens on a red level or once the blue level is placed.</summary>
        void BlueBeforeBegin()
        {
            BlueHoldBegan();                                                                           // a blue flight begins: its End releases the start lines (ZodFlyover.BlueHold.cs)
            if (!OzPlayBlue.Active || !rts || game == null || game.World == null) return;
            var lvl = OzPlayBlueLevel.I;
            if (!lvl || lvl.Placed) return;
            double rx = 0, ry = 0, hx = 0, hy = 0; int nr = 0, nh = 0;
            foreach (var o in game.World.Objects)
            {
                if (!o.IsFort) continue;
                if (o.Owner == Team.Red) { rx += o.CenterX; ry += o.CenterY; nr++; }
                if (o.Owner == game.player) { hx += o.CenterX; hy += o.CenterY; nh++; }
            }
            if (nr == 0 || nh == 0) return;
            lvl.RedFort = ZGame.PxToWorld(rx / nr, ry / nr); lvl.HumanFort = ZGame.PxToWorld(hx / nh, hy / nh);
            lvl.SceneFocus = rts.focus;
            OzSides.StartFocus(lvl.SceneFocus.x, lvl.SceneFocus.z, lvl.RedFort.x, lvl.RedFort.z, lvl.HumanFort.x, lvl.HumanFort.z, out double x, out double z);
            rts.focus = new Vector3((float)x, 0f, (float)z);
            rts.Place();
            lvl.StartFocus = rts.focus; lvl.Placed = true; lvl.enabled = false;                    // its own Start would move the focus a second time
            BluePlacedEarly++;
            Debug.Log($"[ZodFlyover] blue level: the flight begins before the level placed its camera: placed now over the {game.player} fort, focus {lvl.StartFocus.x:0.0} / {lvl.StartFocus.z:0.0} (the scene's {lvl.SceneFocus.x:0.0} / {lvl.SceneFocus.z:0.0}); the level's own placement is switched off");
        }
    }
}
