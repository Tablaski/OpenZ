// OpenZ (GPL-3.0) - version 1.3, item 12 part B7: level 7's fly-over shot lists (the full-screen lava flight behind the corner window).
// L07 = Zod's side, B07 = the blue side. Without them the level would fly the Desert's lists, whose blue shots are absolute Desert
// coordinates. Every shot below was tested off-line at every frame (l07_finish/cutscene/tools/shot_math.py). The corners of the picture land on the map at 16:9, as
// the blue lists' rule asks, with one allowance: enemy_fort's corner goes up to 10.7 m and red_fort's up to 11.7 m past the map's edge, into the 24 m border band (the tool's limit is 12 m), and the sunrise shot looks at the sun on purpose.
// its target, was 24-43 m; the enemy fort half the picture, was 82-88 %), shot 1 untouched; every frame of them stays within 9.6 m of the map
// edge (open_fixes_2nov/l07_flyby_wide/tools/flyby_math.py: the same pose maths, checked against the recorded clip).
// target, was 24-30 m; the red fort half the picture, was 82-88 %), the other three blue shots untouched; every frame of it stays within
// 9.6 m of the map edge (open_fixes_2nov/l07_blue_flyby_wide/tools/blue_math.py: the same pose maths, checked against the recorded clip).
using UnityEngine;

namespace OpenZ
{
    public sealed partial class ZodBriefingLookFile
    {
        /// <summary>The Level 7 shot list (empty = the code list).</summary>
        public ZodCineShotDef[] L07;
    }

    public sealed partial class ZodShotList
    {
        /// <summary>L07 volcano, 168 x 220 m. Enemy (blue) fort at the top (84, -24), ours (red) at the bottom (84, -197). Lava lakes: north
        /// (101, -80), middle west (68, -107), east (107, -106), south (93, -141). Sun at the 0.935 start: heading 116.6 deg, as level 16.</summary>
        public static ZodShotList L07 = new ZodShotList
        {
            level = "L07", open = 0.8f, lastMin = 2.0f, maxTotal = 24f,
            shots = new[]
            {
                // 1. sunrise over the east lake: the low sun disc with its bloom, the lake and the volcano rim under it
                new ZodCineShotDef { name = "dawn_lava", subject = ZodCineSubject.Sunrise, seconds = 5.0f, point = new Vector2(107f, -106f),
                    yawA = 0f, yawB = -1f, pitchA = 8f, pitchB = 9f, distA = 34f, distB = 38f, slide = 8f, fovA = 40f, fovB = 40f, floor = 2f },
                // 2. high over the middle lake, one slow glide north-east along it (row 62: the same lens and angle, 1.85 x as far back)
                new ZodCineShotDef { name = "lake_middle", subject = ZodCineSubject.Free, seconds = 4.2f, camA = new Vector3(55.2f, 70.6f, -174.9f),
                    camB = new Vector3(62.9f, 66.9f, -166.9f), lookA = new Vector3(70f, -0.3f, -112f), lookB = new Vector3(74f, -0.3f, -104f), fovA = 38f, fovB = 38f, floor = 2f, dip = 0.3f, whoosh = true },
                // 3. the north lake and the east lake from the south-east, sinking a little (row 62: 1.6 x as far back, 4 deg steeper: the north edge stays out)
                new ZodCineShotDef { name = "lake_north", subject = ZodCineSubject.Free, seconds = 3.6f, camA = new Vector3(116.9f, 68.8f, -154.7f),
                    camB = new Vector3(114.5f, 62.2f, -143.9f), lookA = new Vector3(102f, -0.3f, -92f), lookB = new Vector3(101f, -0.3f, -84f), fovA = 40f, fovB = 40f, floor = 2f },
                // 4. the enemy fort from the east, front-lit by the sunrise, a steep three-quarter with a 12 deg orbit and a push-in: this fort stands 24 m from the north edge, so the
                //    picture is turned along that edge and looks down, or its top runs off the map (the Desert's orbit showed 273 to 756 m of bare land past it: gap 8)
                new ZodCineShotDef { name = "enemy_fort", subject = ZodCineSubject.EnemyFort, seconds = 4.0f, yawA = 252f, yawB = 264f,
                //    row 62: 112 > 100.8 m (was 60 > 48), 3 deg steeper, the view turned 12 deg so the fort sits right of centre and the north edge stays off the picture
                    pitchA = 39f, pitchB = 36f, distA = 112f, distB = 100.8f, fovA = 28f, fovB = 28f, frameYaw = 12f, aimUp = 4f },
                // 5. our fort from behind and above, craning up into the start camera (level 16's framing: the fort has the same place on its map)
                new ZodCineShotDef { name = "our_base_home", subject = ZodCineSubject.OwnBaseHome, seconds = 3.2f, yawA = 6f, pitchA = 38f,
                    distA = 90f, fovA = 30f, aimUp = 3f, ease = ZodCineEase.InOut },   // row 62: 90 m (was 74): never closer to the fort than the start camera it lands on
            }
        };

        /// <summary>Volcano, 168 x 220 m, as blue: our fort at the top (84, -24), the red fort at the bottom (84, -197), the lakes between
        /// them. The same four beats as the other blue lists (BlueShotNames).</summary>
        public static ZodShotList B07 = new ZodShotList
        {
            level = "L07B", open = BlueOpen, lastMin = BlueLastMin, maxTotal = BlueMaxTotal,
            shots = new[]
            {
                // 1. our base at dawn: across our fort into the low sun (level 16's framing, a little nearer: this fort stands 12 m from the top edge)
                new ZodCineShotDef { name = "blue_base_dawn", subject = ZodCineSubject.OwnFortDawn, seconds = 3.2f, yawA = -9f, yawB = -9f, distA = 52f, distB = 45f,
                    heightA = 24f, heightB = 21f, pitchA = 13f, pitchB = 14f, fovA = 48f, fovB = 48f, floor = 6f },   // 1.3 RUN7: yaw -20 to -9: with the rim drawn whole (l07_volcano_pop) a spire stood in front of the sun disc the whole shot; at -9 the disc is clear from A to B
                // 2. the world: high over the middle looking south over the lakes to the red fort; one slow glide south and down, the whole picture on the map
                new ZodCineShotDef { name = "the_world", subject = ZodCineSubject.Free, seconds = 4.7f, camA = new Vector3(84f, 92f, -70f), camB = new Vector3(84f, 86f, -86f),
                    lookA = new Vector3(84f, 0f, -124f), lookB = new Vector3(84f, 0f, -136f), fovA = 34f, fovB = 34f, floor = 10f, cutWord = "same", cutAt = 1, cutCue = 2 },
                // 3. the red fort from the east, front-lit by the sunrise, a three-quarter with a 12 deg orbit and a push-in, turned away from the south edge
                //    row 68: 114 > 102.6 m (was 60 > 48), 5 deg steeper, the view turned 14 deg right (frameYaw -14: the fort sits left of centre, the
                //    south edge stays off the picture) and aimed 2 m higher (aimUp 6: the fort sits under the corner window, not behind it)
                new ZodCineShotDef { name = "red_fort", subject = ZodCineSubject.Point, seconds = 4.2f, point = new Vector2(84f, -197f), yawA = 288f, yawB = 276f,
                    pitchA = 41f, pitchB = 38f, distA = 114f, distB = 102.6f, fovA = 28f, fovB = 28f, frameYaw = -14f, aimUp = 6f, dip = 0.3f, whoosh = true, cutWord = "where", cutAt = 0, cutCue = 4 },
                // 4. our fort from behind, craning up into the start camera. The cut is on "Then" of the level's own scene ("Then walk faster."): its
                //    "Now" stands right at the start of the cut before this one and would leave the red fort 0.8 s. The shared briefing has no "then": its cue 5
                new ZodCineShotDef { name = "blue_home", subject = ZodCineSubject.OwnBaseHome, seconds = 2.0f, yawA = 0f, pitchA = 44f, distA = 72f, fovA = 30f, aimUp = 3f,
                    ease = ZodCineEase.InOut, cutWord = "then", cutAt = 0, cutCue = 5 },
            }
        };

        /// <summary>EnsureFile: the Level 7 list of the data file (Play tuning writes it there).</summary>
        static void LoadL07(ZodBriefingLookFile f) { if (f != null && f.L07 != null && f.L07.Length > 0) L07.shots = f.L07; }
        /// <summary>ToJson: the Level 7 part.</summary>
        static void SaveL07(ZodBriefingLookFile f) { if (f != null) f.L07 = L07.shots; }
    }
}
