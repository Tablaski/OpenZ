// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": its two fly-over shot lists (the full-screen flight behind the corner window).
// L18 = Zod's side, B18 = the blue side. The pictures of both are staged by OzL18BriefingStage (the columns on the three whole bridges,
// the blasts, the thrown tanks, the smoke), which reads the shot on screen and the seconds into it; the names below are its cues.
// The rules of every briefing flight hold: ONE slow move per shot, hard cuts, no dip, cuts on the line breaks; every cut is ANCHORED
// (a cue of the briefing line + seconds, written by kit_tools/make_shots.py from the voice clips' clock): the cut into `three_blasts` IS
// the press. The first shot of each list looks east down the river into the rising sun: at the list's first use it is turned about its
// own camera so that the level's real sun stands DawnSunRight degrees right of the picture's middle (at most DawnTurnMax degrees from
// the written look: the three bridges stay in the picture). Each list's last shot is our fort easing into the RTS start camera.
// Every hand-composed shot was checked off-line at nine moments of its move at 16:9: the picture's corners on the ground inside the map
// + its 12 m border band, a tank at most a tenth of the picture's height, every subject of the shot (bridge ends, forts, the crane) inside
// the picture, clear of the corner window (top left) and of the letterbox bars, and the move itself (a shot over 2 s travels 15 m or turns
// 8 degrees): kit_tools/make_shots.py, proof/shot_math.txt.
// <numbers: written by kit_tools/make_shots.py from clock.json, do not edit by hand; tune in Play through ZodShotList.L18 / B18>
// Tuned on engine frames (12 Nov 2026): the numbers below are what the recorded takes show.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class ZodBriefingLookFile
    {
        /// <summary>The Level 18 shot lists (empty = the code lists).</summary>
        public ZodCineShotDef[] L18, B18;
    }

    public sealed partial class ZodShotList
    {
        /// <summary>The dawn shots' aim: the real sun this many degrees right of the picture's middle; the look is turned at most this far from the written one.</summary>
        public static float DawnSunRight = 15f, DawnTurnMax = 24f;

        /// <summary>L18 city, 256 x 240 m. Enemy (blue) fort north of the river (126, -80), ours (red) south of it (126, -164); the river across the
        /// middle (z -108 to -132) with the three long bridges at x 84, 126, 168; the one standing bridge in the north-east (230, -58), the empty
        /// crane of our half at (167, -221). Sun at the start: east-south-east.</summary>
        public static ZodShotList L18 = new ZodShotList
        {
            // path's): forward, banking, wide lens, units at RTS distance, never a side scroll, never zoomed in. The camA / camB / lookA / lookB
            // written here are each path's first and last point (what flies if the paths are off). The stage (OzL18BriefingStage, RedParade) shows the
            // blue parades on the three whole bridges in the first frames and blows them in the first second.
            level = "L18", open = 1.2f, lastMin = 2.0f, maxTotal = 26f,
            shots = new[]
            {
                // 1. the opening and Alan "Sir! The enemy blew up all three bridges!": from high over our own fort forward and down at the river: the three
                //    bridges with the blue parades across the picture, they go up one after another (the shake), the drone banks east along the burning
                //    river into the sunrise.
                new ZodCineShotDef { name = "red_blast_run", subject = ZodCineSubject.Free, seconds = 4.6f, camA = new Vector3(118f, 62f, -224f), camB = new Vector3(142f, 22f, -144f),
                    lookA = new Vector3(126f, 0f, -125f), lookB = new Vector3(202f, 10.3f, -144f), fovA = 60f, fovB = 60f, floor = 2f },
                // 2. Zod "EXCELLENT! THEY'RE TRAPPED!": over the broken middle bridge to the far bank and west along it: what is left of the three parades
                //    stands at the broken ends.
                new ZodCineShotDef { name = "red_trapped", subject = ZodCineSubject.Free, seconds = 3f, camA = new Vector3(134f, 38f, -154f), camB = new Vector3(96f, 26f, -106f),
                    lookA = new Vector3(124f, 0f, -104f), lookB = new Vector3(64f, 2f, -106f), fovA = 60f, fovB = 60f, floor = 2f, whoosh = true, cutCue = 2, cutOffset = 0f },
                // 3. Alan "Actually, sir, so are we!": back over the water toward our own fort: our bank, our fort, cut off.
                new ZodCineShotDef { name = "red_cut_off", subject = ZodCineSubject.Free, seconds = 2.6f, camA = new Vector3(152f, 42f, -94f), camB = new Vector3(135f, 25f, -141f),
                    lookA = new Vector3(128f, 0f, -150f), lookB = new Vector3(124f, 4f, -180f), fovA = 60f, fovB = 60f, floor = 2f, whoosh = true, cutCue = 3, cutOffset = 0f },
                // 4. Zod "EVEN BETTER! THEY'LL NEVER SEE US COMING!": : fast across the enemy's half from the south-west to the one
                //    bridge that still stands: the way round.
                new ZodCineShotDef { name = "red_way_round", subject = ZodCineSubject.Free, seconds = 4f, camA = new Vector3(58f, 56f, -152f), camB = new Vector3(190f, 38f, -56f),
                    lookA = new Vector3(110f, 0f, -100f), lookB = new Vector3(238f, 2f, -58f), fovA = 60f, fovB = 60f, floor = 2f, whoosh = true, cutCue = 4, cutOffset = 0f },
                // 5. Brad "But sir! How do we get across?!": the question's own picture: down our bridge road and out over the broken end, the open water
                //    and the far stump ahead.
                new ZodCineShotDef { name = "red_the_water", subject = ZodCineSubject.Free, seconds = 2.9f, camA = new Vector3(112f, 34f, -178f), camB = new Vector3(115f, 6.5f, -127f),
                    lookA = new Vector3(118f, 0f, -128f), lookB = new Vector3(124f, 4f, -90f), fovA = 58f, fovB = 58f, floor = 2f, whoosh = true, cutCue = 5, cutOffset = 0f },
                // 6. Zod "SWIM! NOW GET ON WITH IT": from low behind our fort forward and up, settling into the start camera (the hand-back has no jump).
                new ZodCineShotDef { name = "red_home", subject = ZodCineSubject.OwnBaseHome, seconds = 3.2f, yawA = 10f, pitchA = 38f, distA = 80f, fovA = 30f, aimUp = 3f, ease = ZodCineEase.InOut, whoosh = true, cutCue = 6, cutOffset = 0f },
            }
        };

        /// <summary>The same map as blue: our fort north of the river (126, -80), the red fort south of it. The press is the cut into three_blasts.
        /// camera drone along its own path (ZodFlyover.L18Drone.cs, the shot's name is the path's): forward, banking, wide lens, units at
        /// RTS distance. The camA / camB / lookA / lookB written here are each path's first and last point (what flies if the paths are off).
        /// Cuts on the voice clock of scene_L18_start (v2): the press 6.51, 9.60, 13.70, 17.95 s.</summary>
        public static ZodShotList B18 = new ZodShotList
        {
            level = "L18B", open = 1.1f, lastMin = 2.0f, maxTotal = 27f,
            shots = new[]
            {
                // 1. ONE flight from the window's opening to the press: the dive out of the dawn sky down to the river ("Three bridges!"), over the west
                //    bridge and round the middle bridge's far end past the banner ("Three armies! One glorious victory!"), the climb out and the hang
                //    over the middle parade ("Now, where's my coffee?!"). The cut out of it is ON the press.
                new ZodCineShotDef { name = "parade_run", subject = ZodCineSubject.Free, seconds = 7.61f, camA = new Vector3(-4f, 76f, -92f), camB = new Vector3(186f, 25f, -100f),
                    lookA = new Vector3(110f, 0f, -120f), lookB = new Vector3(128f, 2f, -120f), fovA = 60f, fovB = 57f, floor = 2f },
                // 4. the press, BS-01's scream, the three blasts: THE VIEW THE OWNER KEPT (from the south over the red bank, a track west to east, all three
                //    bridges across the middle of the picture), exactly as it was; the blasts' shake sits on it.
                new ZodCineShotDef { name = "three_blasts", subject = ZodCineSubject.Free, seconds = 3.09f, camA = new Vector3(108f, 64f, -186f), camB = new Vector3(142f, 59f, -186f),
                    lookA = new Vector3(116f, 0f, -96f), lookB = new Vector3(128f, 0f, -92f), fovA = 48f, fovB = 48f, floor = 10f, whoosh = true, cutCue = 3, cutOffset = 0.37f },
                // 5. ONE flight after the blasts: the wreckage still coming down, low and fast east along the red bank past the three burning bridges, then
                //    (BS-01 "Sir! You've destroyed all three bridges!") a banking climb out to the south-east and round: all three broken bridges, their smoke and
                //    both forts in one picture.
                new ZodCineShotDef { name = "aftermath_run", subject = ZodCineSubject.Free, seconds = 4.1f, camA = new Vector3(70f, 14f, -144f), camB = new Vector3(186f, 60f, -191f),
                    lookA = new Vector3(150f, 3f, -120f), lookB = new Vector3(124f, 0f, -124f), fovA = 62f, fovB = 58f, floor = 2f, whoosh = true, cutCue = 3, cutOffset = 3.46f },
                // 7. EvilZod "Well, EXACTLY! Now the enemy can NOT cross!": from over our own fort down the bridge road at the gap; the drone stops at the
                //    broken edge: the water, the far stump, the banner on the red bank, the red fort across it.
                new ZodCineShotDef { name = "the_gap", subject = ZodCineSubject.Free, seconds = 4.25f, camA = new Vector3(118f, 42.9f, -50f), camB = new Vector3(112f, 4.55f, -105.5f),
                    lookA = new Vector3(124f, 0f, -112f), lookB = new Vector3(128f, 4f, -152f), fovA = 58f, fovB = 58f, floor = 2f, whoosh = true, cutCue = 5, cutOffset = 0.10f },
                // 8. BS-01 "Neither can we, sir.": across the gap toward our own fort and up, settling into the start camera (the hand-back has no jump).
                new ZodCineShotDef { name = "blue_home", subject = ZodCineSubject.OwnBaseHome, seconds = 2.77f, yawA = 0f, pitchA = 44f, distA = 72f, fovA = 30f, aimUp = 3f, ease = ZodCineEase.InOut, whoosh = true, cutCue = 6, cutOffset = -0.03f },
            }
        };

        static Vector3[] dawnLooks;
        /// <summary>ZodShotList.For / ForBlue: level 18's list of the side, its dawn shot aimed at the level's real sun (once per list and sun heading).</summary>
        public static ZodShotList BridgeGame(bool blueSide)
        {
            var list = blueSide ? B18 : L18;
            AimDawn(list);
            return list;
        }

        /// <summary>The first shot of a list looks into the sunrise: its two look points are turned about their cameras so that the sun stands
        /// DawnSunRight degrees right of the middle. The written looks are kept (the turn is always made from them, never from a turned one).</summary>
        static void AimDawn(ZodShotList list)
        {
            if (list == null || list.shots == null || list.shots.Length == 0 || list.shots[0] == null || list.shots[0].subject != ZodCineSubject.Free) return;
            var sun = RenderSettings.sun; if (!sun) return;
            Vector3 s = -sun.transform.forward; s.y = 0f; if (s.sqrMagnitude < 1e-4f) return;
            float sunYaw = Mathf.Atan2(s.x, s.z) * Mathf.Rad2Deg;
            var d = list.shots[0];
            dawnLooks ??= new Vector3[4];
            int k = list == B18 ? 0 : 2;
            if (dawnLooks[k] == Vector3.zero && dawnLooks[k + 1] == Vector3.zero) { dawnLooks[k] = d.lookA; dawnLooks[k + 1] = d.lookB; }
            d.lookA = Turned(d.camA, dawnLooks[k], sunYaw); d.lookB = Turned(d.camB, dawnLooks[k + 1], sunYaw);
        }

        static Vector3 Turned(Vector3 cam, Vector3 look, float sunYaw)
        {
            Vector3 f = look - cam; float flat = new Vector2(f.x, f.z).magnitude; if (flat < 1f) return look;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float turn = Mathf.Clamp(Mathf.DeltaAngle(yaw, sunYaw - DawnSunRight), -DawnTurnMax, DawnTurnMax);
            float a = (yaw + turn) * Mathf.Deg2Rad;
            return new Vector3(cam.x + Mathf.Sin(a) * flat, look.y, cam.z + Mathf.Cos(a) * flat);
        }

        /// <summary>EnsureFile: the Level 18 lists of the data file (Play tuning writes them there).</summary>
        static void LoadL18(ZodBriefingLookFile f)
        {
            if (f == null) return;
            if (f.L18 != null && f.L18.Length > 0) { L18.shots = f.L18; dawnLooks = null; }
            if (f.B18 != null && f.B18.Length > 0) { B18.shots = f.B18; dawnLooks = null; }
        }
        /// <summary>ToJson: the Level 18 part (the dawn shots with their written looks).</summary>
        static void SaveL18(ZodBriefingLookFile f)
        {
            if (f == null) return;
            f.L18 = L18.shots; f.B18 = B18.shots;
        }
    }
}
