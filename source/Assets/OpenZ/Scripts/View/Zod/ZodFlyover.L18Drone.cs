// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": the blue briefing's flights are flown as a fast camera drone.
// A shot of the level's list whose name has a path here is flown along that path instead of the list's straight move: the camera
// follows a smooth curve through the path's points (place, the point it looks at, field of view, each at its share of the shot),
// always moving from the shot's first frame, and BANKS into its turns (the roll follows the curve's own turn rate and speed, at most
// MaxBank degrees). Wide lens; the list's cuts, anchors and whooshes stay the list's. A path whose last point is the game's start
// camera (`home`) eases its last stretch into that camera exactly: the hand-back has no jump.
// A pure function of the shot's time: a held frame (ZodFlyover.HoldShot / HoldU) shows the running flight's picture.
// Two clearly different paths a flight were blocked in the engine and judged on frame sheets; Variant 0 holds the picked ones (the
// reason stands at each), Variant 1 the others.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>One point of a drone path: its share of the shot (0..1), the camera's place, the point it looks at, the field of view, an extra roll (deg).</summary>
    public sealed class OzDroneKey
    {
        public float u; public Vector3 pos, look; public float fov = 58f, roll;
        public OzDroneKey(float u, float px, float py, float pz, float lx, float ly, float lz, float fov = 58f, float roll = 0f) { this.u = u; pos = new Vector3(px, py, pz); look = new Vector3(lx, ly, lz); this.fov = fov; this.roll = roll; }
    }

    /// <summary>A shot's flight: its points, how strongly it banks (1 = the curve's own), whether its points are written from the game's start
    /// camera (the last shot: place = start camera + pos, look = the start camera's ground point + look) and where the ease into it begins.</summary>
    public sealed class OzDronePath
    {
        public OzDroneKey[] keys; public float bank = 1f; public bool home; public float homeFrom = 0.55f; public float easeOut;
        /// <summary>
        /// (never from a standstill, no step in the speed: the pace's rate of change is 0 where the ease ends); 1 / 0 = none.</summary>
        public float enterK = 1f, enterSeconds;
        /// <summary>Over this last share of the shot the bank levels out (0 = it keeps the curve's own bank to the cut).</summary>
        public float bankOut;
        public OzDronePath(params OzDroneKey[] k) { keys = k; }
    }

    public static class OzL18Drone
    {
        /// <summary>False = the list's own straight moves (a check).</summary>
        public static bool On = true;
        /// <summary>Which of the two blocked variants flies (0 = the chosen one).</summary>
        public static int Variant = 0;
        /// <summary>The roll's limit (deg) and its share of the physical bank angle.</summary>
        public static float MaxBank = 13f, BankGain = 0.3f, BankSeconds = 0.3f;
        /// <summary>
        /// SmoothSeconds apart round this one; the bank goes softly into MaxBank (tanh), never against a stop. 0 = the raw curve.</summary>
        public static float SmoothSeconds = 0.15f;
        /// <summary>Play checks: frames flown, the fastest speed seen (m/s), the steepest roll (deg), the shot flown now.</summary>
        public static int Frames; public static float SpeedMax, RollMax; public static string Now = "";

        static OzDroneKey K(float u, float px, float py, float pz, float lx, float ly, float lz, float fov = 58f, float roll = 0f) => new OzDroneKey(u, px, py, pz, lx, ly, lz, fov, roll);

        // The map: 256 x 240 m, x east, -z south. The river z -108 .. -132 between the west canal (x 64 .. 72) and the east canal (x 180 .. 188);
        // the three long bridges at x 84, 126, 168; our (blue) fort north of it (126, -80), the red fort south of it (126, -164); the sun ESE.
        public static readonly Dictionary<string, OzDronePath[]> Paths = new Dictionary<string, OzDronePath[]>
        {
            // 1. ONE unbroken flight from the window's opening to the press (7.61 s; points at their seconds on the voice clock):
            //    "Three bridges!": the dive out of the dawn sky in the north-west down to the river, levelling off low over the west canal, the three
            //    parades crossing ahead one behind the other, the sun disc right of the middle; "Three armies! One glorious victory!": on over the
            //    west bridge and round the middle bridge's far end past the banner (the parade coming at the lens, our fort behind it);
            //    "Now, where's my coffee?!": on round the bridge's end, over the east bridge and out to the north-east bank, looking back along the
            //    river: all three bridges with their parades in a row (20 to 27 m/s to the cut: it never hangs).
            //    PICKED over the other blocked path (in along the west canal, a pass north of the bridges, a low creep from the east): this one has
            //    all three bridges and the sun in its first second and brings the banner face on with our fort behind it; the other opened on one
            //    column from behind, too near.
            ["parade_run"] = new[]
            {
                new OzDronePath(K(T1(-1.1f), -4f, 76f, -92f, 110f, 0f, -120f, 60f), K(T1(0f), 22f, 40f, -104f, 140f, 0f, -122f, 60f), K(T1(1.25f), 58f, 15f, -116f, 200f, 6f, -128f, 60f),
                                K(T1(2.0f), 84f, 19f, -127f, 150f, 3f, -124f, 60f), K(T1(2.6f), 106f, 20f, -138f, 128f, 4f, -118f, 60f), K(T1(3.2f), 126f, 20f, -146f, 126f, 5f, -114f, 60f),
                                K(T1(3.9f), 146f, 23f, -142f, 124f, 3f, -116f, 59f), K(T1(4.6f), 162f, 24f, -132f, 124f, 2f, -117f, 58f), K(T1(5.5f), 178f, 22f, -118f, 126f, 2f, -119f, 58f),
                                K(T1(6.51f), 186f, 25f, -100f, 128f, 2f, -120f, 57f)) { bankOut = 0.1f },
                new OzDronePath(K(T1(-1.1f), 67f, 40f, -46f, 70f, 0f, -110f, 60f), K(T1(0.2f), 68f, 18f, -88f, 96f, 2f, -124f, 60f), K(T1(1.25f), 84f, 14f, -112f, 190f, 5f, -128f, 60f),
                                K(T1(2.3f), 112f, 26f, -101f, 150f, 0f, -120f, 60f), K(T1(3.3f), 144f, 30f, -98f, 150f, 0f, -126f, 60f), K(T1(4.0f), 166f, 28f, -108f, 132f, 0f, -124f, 60f),
                                K(T1(4.8f), 176f, 20f, -126f, 126f, 2f, -120f, 58f), K(T1(6.51f), 172f, 13f, -131f, 126f, 3f, -119f, 56f)),
            },
            // 5. ONE unbroken flight after the blasts (4.1 s): the wreckage still coming down, low and fast east along the red bank past the three
            //    burning bridges (the banner tumbles past the lens); BS-01 "Sir! You've destroyed all three bridges!": a banking climb out to the
            //    south-east and round, until all three broken bridges, their smoke and both forts stand in one picture.
            //    PICKED: the low run of the first blocked path with the south-east end of the second (the first one's own end, a climb over the east
            //    bridge to the north-east, went through that bridge's smoke and hid the bridges behind it).
            ["aftermath_run"] = new[]
            {
                new OzDronePath(K(T5(9.6f), 70f, 14f, -144f, 150f, 3f, -120f, 62f), K(T5(10.3f), 98f, 12f, -143f, 176f, 4f, -120f, 62f), K(T5(11.0f), 126f, 12f, -143f, 187f, 6f, -108f, 62f),
                                K(T5(11.5f), 148f, 14f, -145f, 162f, 3f, -110f, 61f), K(T5(12.0f), 168f, 22f, -152f, 153f, 0f, -115f, 60f), K(T5(12.5f), 182f, 36f, -166f, 136f, 0f, -118.5f, 59f),
                                K(T5(13.0f), 188f, 50f, -180f, 126f, 0f, -122f, 58f), K(T5(13.7f), 186f, 60f, -191f, 124f, 0f, -124f, 58f)) { easeOut = 0.35f, bankOut = 0.24f, enterK = 0.6f, enterSeconds = 0.5f },
                new OzDronePath(K(T5(9.6f), 60f, 24f, -100f, 130f, 2f, -118f, 62f), K(T5(10.5f), 100f, 22f, -98f, 170f, 2f, -118f, 62f), K(T5(11.4f), 148f, 20f, -100f, 210f, 10f, -120f, 62f),
                                K(T5(12.1f), 184f, 26f, -108f, 200f, 8f, -150f, 60f), K(T5(12.7f), 200f, 42f, -128f, 150f, 0f, -140f, 59f), K(T5(13.2f), 198f, 56f, -148f, 126f, 0f, -124f, 58f),
                                K(T5(13.7f), 190f, 62f, -160f, 120f, 0f, -120f, 58f)) { enterK = 0.6f, enterSeconds = 0.5f },
            },
            // 7. EvilZod "Well, EXACTLY! Now the enemy can NOT cross!": from over our own fort down at the gap, and the drone stops at the water's
            //    edge beside the burning stump (up wind of its smoke): the water, the far stump, the banner on the red bank, the red fort across it.
            //    PICKED over the other blocked path (backward and up from the red bank): the stop at the edge is felt, and the red fort stands
            //    across the water; the other ended high and flat, a map view.
            ["the_gap"] = new[]
            {
                new OzDronePath(K(0f, 118f, 42.9f, -50f, 124f, 0f, -112f, 58f), K(0.4f, 116f, 27.2f, -79f, 126f, 2f, -132f, 58f), K(0.72f, 113.5f, 11.55f, -97f, 127f, 3f, -146f, 58f),
                                K(1f, 112f, 4.55f, -105.5f, 128f, 4f, -152f, 58f)) { easeOut = 1f },
                new OzDronePath(K(0f, 115f, 8f, -127f, 124f, 0f, -152f, 58f), K(0.3f, 113f, 15f, -114f, 126f, 0f, -148f, 58f), K(0.65f, 110f, 29f, -98f, 127f, 0f, -143f, 57f),
                                K(1f, 107f, 44f, -82f, 128f, 0f, -140f, 56f)) { easeOut = 0.5f },
            },
            // 8. BS-01 "Neither can we, sir.": the reverse: from over the red bank forward across the gap and up, our own fort beyond it, and the drone
            //    settles into the game's start camera (points written from that camera: place, and its ground point). The other blocked path came
            //    round from the east, through the bridges' smoke.
            ["blue_home"] = new[]
            {
                new OzDronePath(K(0f, -20f, -46f, -25f, 2f, 0f, -30f, 58f), K(0.45f, -9f, -22f, -13f, 1f, 0f, -14f, 50f), K(1f, 0f, 0f, 0f, 0f, 0f, 0f, 32f)) { home = true, homeFrom = 0.5f, easeOut = 1f },
                new OzDronePath(K(0f, 44f, -40f, -20f, 0f, 0f, -24f, 58f), K(0.5f, 16f, -18f, -10f, 0f, 0f, -10f, 50f), K(1f, 0f, 0f, 0f, 0f, 0f, 0f, 32f)) { home = true, homeFrom = 0.5f, easeOut = 1f },
            },
            // ------------------------------------------------------------------------------------------ the RED list (round 2). Each: [0] the picked path, [1] the other blocked one.
            // R1. the opening (4.7 s): the blasts go off at 0.30 / 0.64 / 0.98 s of it. [0] the dive from over our own fort at the three bridges, then east
            //     along our bank at the east bridge and the sun (PICKED: all three bridges and both forts in its first frame, at the kept view's
            //     distance); [1] in from the south-west along our bank (it ends against our own fort's tower).
            // R2 [0] over the broken middle bridge and west along the far bank (PICKED; [1], along the far bank from the east, passes the blue fort too near).
            // R3 [0] back over the water at our own fort (PICKED; [1], along our bank from the west, shows both forts but not that we are cut off).
            // R5 [0] down our bridge road and out over the broken end (PICKED: the question's own picture; [1], a skim through the west bridge's gap, flies through its smoke).
            ["red_blast_run"] = new[]
            {
                new OzDronePath(K(0f, 118f, 62f, -224f, 126f, 0f, -125f, 60f), K(0.25f, 120f, 48f, -200f, 128f, 0f, -121f, 60f), K(0.45f, 123f, 38f, -182f, 139.9f, 0f, -114.1f, 60f),
                                K(0.6f, 127f, 32f, -169f, 159.5f, 1f, -112.7f, 60f), K(0.75f, 132f, 27f, -158f, 179.3f, 5f, -121f, 60f), K(0.88f, 137f, 24f, -150f, 194.7f, 8f, -133.4f, 60f),
                                K(1f, 142f, 22f, -144f, 202f, 10.3f, -144f, 60f)) { bankOut = 0.12f },
                new OzDronePath(K(0f, -4f, 56f, -172f, 150f, 0f, -124f, 60f), K(0.25f, 28f, 40f, -166f, 170f, 0f, -128f, 60f), K(0.5f, 60f, 27f, -160f, 190f, 2f, -132f, 60f),
                                K(0.75f, 90f, 20f, -155f, 214f, 4f, -136f, 60f), K(1f, 116f, 17f, -151f, 236f, 6f, -140f, 60f)),
            },
            // R2. "EXCELLENT! THEY'RE TRAPPED!"
            ["red_trapped"] = new[]
            {
                new OzDronePath(K(0f, 134f, 38f, -154f, 124f, 0f, -104f, 60f), K(0.36f, 129f, 31f, -131f, 103.6f, 0f, -100.1f, 60f), K(0.7f, 114f, 27f, -114f, 81f, 1f, -99.5f, 60f),
                                K(1f, 96f, 26f, -106f, 64f, 2f, -106f, 60f)) { bank = 0.7f, bankOut = 0.32f },
                new OzDronePath(K(0f, 198f, 28f, -92f, 150f, 0f, -110f, 60f), K(0.5f, 160f, 24f, -93f, 110f, 0f, -112f, 60f), K(1f, 124f, 24f, -91f, 72f, 0f, -112f, 60f)),
            },
            // R3. "Actually, sir, so are we!"
            ["red_cut_off"] = new[]
            {
                new OzDronePath(K(0f, 152f, 44f, -92f, 128f, 0f, -150f, 60f), K(0.5f, 145f, 36f, -114f, 126f, 2f, -164f, 60f), K(1f, 139f, 30f, -133f, 124f, 4f, -178f, 60f)),
                new OzDronePath(K(0f, 58f, 46f, -192f, 126f, 0f, -140f, 60f), K(0.5f, 80f, 40f, -185f, 138f, 0f, -136f, 60f), K(1f, 101f, 34f, -177f, 150f, 0f, -130f, 60f)),
            },
            // R4. "EVEN BETTER! THEY'LL NEVER SEE US COMING!": the rest of the map: [0] fast across the enemy's half from the south-west to the one bridge
            //     that still stands (PICKED: the first blocked path, up the east canal, ran along the border rock for half its length); [1] in from the north-east.
            ["red_way_round"] = new[]
            {
                new OzDronePath(K(0f, 58f, 56f, -152f, 110f, 0f, -100f, 60f), K(0.33f, 98f, 52f, -108f, 150f, 0f, -64f, 60f), K(0.66f, 146f, 48f, -72f, 200f, 0f, -50f, 60f),
                                K(1f, 190f, 38f, -56f, 238f, 2f, -58f, 60f)) { bank = 0.5f, bankOut = 0.15f },
                new OzDronePath(K(0f, 236f, 56f, -36f, 200f, 0f, -70f, 60f), K(0.5f, 214f, 50f, -62f, 170f, 0f, -104f, 60f), K(1f, 190f, 44f, -88f, 140f, 0f, -126f, 60f)),
            },
            // R5. "But sir! How do we get across?!"
            ["red_the_water"] = new[]
            {
                new OzDronePath(K(0f, 112f, 34f, -178f, 118f, 0f, -128f, 58f), K(0.45f, 113f, 20f, -154f, 120f, 1f, -112f, 58f), K(0.8f, 114f, 10f, -137f, 122f, 3f, -98f, 58f),
                                K(1f, 115f, 6.5f, -127f, 124f, 4f, -90f, 58f)) { easeOut = 0.6f },
                new OzDronePath(K(0f, 68f, 26f, -74f, 70f, 0f, -110f, 58f), K(0.35f, 68f, 15f, -100f, 84f, 2f, -120f, 58f), K(0.6f, 76f, 9f, -116f, 120f, 3f, -120f, 58f),
                                K(1f, 110f, 7.5f, -120f, 170f, 4f, -122f, 58f)),
            },
            // R6. "SWIM! NOW GET ON WITH IT": into the start camera (points written from that camera)
            ["red_home"] = new[]
            {
                new OzDronePath(K(0f, 14f, -44f, -34f, 2f, 0f, 16f, 58f), K(0.5f, 6f, -20f, -14f, 0f, 0f, 10f, 50f), K(1f, 0f, 0f, 0f, 0f, 0f, 0f, 32f)) { home = true, homeFrom = 0.5f, easeOut = 1f },
                new OzDronePath(K(0f, 44f, -40f, -20f, 0f, 0f, 24f, 58f), K(0.5f, 16f, -18f, -10f, 0f, 0f, 10f, 50f), K(1f, 0f, 0f, 0f, 0f, 0f, 0f, 32f)) { home = true, homeFrom = 0.5f, easeOut = 1f },
            },
        };
        /// <summary>A second on the voice clock as the share of the first flight (it opens 1.10 s before the first word and ends on the press, 6.51 s)
        /// and of the flight after the blasts (9.60 to 13.70 s).</summary>
        static float T1(float clock) => Mathf.Clamp01((clock + 1.10f) / 7.61f);
        static float T5(float clock) => Mathf.Clamp01((clock - 9.60f) / 4.10f);

        public static OzDronePath PathOf(string shot)
        {
            if (!On || shot == null || !Paths.TryGetValue(shot, out var v) || v == null || v.Length == 0) return null;
            return v[Mathf.Clamp(Variant, 0, v.Length - 1)];
        }

        /// <summary>The curve through the keys at `u` (a Catmull-Rom spline over the keys' own shares): place, look point, field of view, extra roll.</summary>
        public static void Eval(OzDronePath p, float u, out Vector3 pos, out Vector3 look, out float fov, out float roll)
        {
            var k = p.keys; int n = k.Length;
            if (n == 1) { pos = k[0].pos; look = k[0].look; fov = k[0].fov; roll = k[0].roll; return; }
            u = Mathf.Clamp(u, k[0].u, k[n - 1].u);
            int i = 0; while (i < n - 2 && u > k[i + 1].u) i++;
            var a = k[i]; var b = k[i + 1]; float du = Mathf.Max(1e-4f, b.u - a.u), s = (u - a.u) / du;
            var a0 = k[Mathf.Max(0, i - 1)]; var b1 = k[Mathf.Min(n - 1, i + 2)];
            float da = Mathf.Max(1e-4f, b.u - a0.u), db = Mathf.Max(1e-4f, b1.u - a.u);
            Vector3 mpa = (b.pos - a0.pos) / da * du, mpb = (b1.pos - a.pos) / db * du, mla = (b.look - a0.look) / da * du, mlb = (b1.look - a.look) / db * du;
            float s2 = s * s, s3 = s2 * s, h00 = 2f * s3 - 3f * s2 + 1f, h10 = s3 - 2f * s2 + s, h01 = -2f * s3 + 3f * s2, h11 = s3 - s2;
            pos = h00 * a.pos + h10 * mpa + h01 * b.pos + h11 * mpb;
            look = h00 * a.look + h10 * mla + h01 * b.look + h11 * mlb;
            float e = s * s * (3f - 2f * s);
            fov = Mathf.Lerp(a.fov, b.fov, e); roll = Mathf.Lerp(a.roll, b.roll, e);
        }

        /// <summary>
        /// over the first e = enterSeconds / dur of the shot, 1 after it (g = k u + (1 - k) e (x^3 - x^4 / 2), x = u / e): the speed starts at
        /// k / g(1) of the path's own, rises with no step, and the shot still ends on its last point at its last frame.</summary>
        public static float Share(OzDronePath p, float u, float dur)
        {
            u = Mathf.Clamp01(u);
            if (p.enterSeconds > 0f && p.enterK < 1f && dur > 0.05f)
            {
                float e = Mathf.Clamp(p.enterSeconds / dur, 1e-3f, 0.9f), k = Mathf.Clamp01(p.enterK);
                float G(float v) { if (v >= e) return e * (1f + k) * 0.5f + (v - e); float x = v / e; return k * v + (1f - k) * e * (x * x * x - 0.5f * x * x * x * x); }
                u = G(u) / G(1f);
            }
            return Share(p, u);
        }

        /// <summary>The shot's own time share along the path: even, or eased out toward its end (easeOut 1 = it comes to rest there).</summary>
        public static float Share(OzDronePath p, float u)
        {
            u = Mathf.Clamp01(u);
            return p.easeOut > 0f ? Mathf.Lerp(u, 1f - (1f - u) * (1f - u), Mathf.Clamp01(p.easeOut)) : u;
        }

        /// <summary>The drone's whole pose at `u` of a shot of `dur` seconds: a pure function of the shot's time (the game and the checks call
        /// this one function). `ground` (may be null) gives the ground's height: the lens stays 2.5 m over it. A `home` path's points are
        /// written from the game's start camera and its last stretch eases into that camera exactly.</summary>
        public static void Pose(OzDronePath p, float u, float dur, Vector3 homePos, Quaternion homeRot, float homeFov, Vector3 homeGround, System.Func<Vector3, float> ground,
                                out Vector3 P, out Quaternion rot, out float F, out Vector3 L, out float speed, out float bank)
        {
            Quaternion yawH = Quaternion.Euler(0f, homeRot.eulerAngles.y, 0f);
            void At(float uu, out Vector3 pos, out Vector3 look, out float fov, out float roll)
            {
                Eval(p, Share(p, uu, dur), out pos, out look, out fov, out roll);                       // fixes_r5: with the entry ease
                if (p.home) { pos = homePos + yawH * pos; look = homeGround + yawH * look; }
            }
            dur = Mathf.Max(0.2f, dur);
            // the bank at one moment: the turn rate of the flight's own heading over a short stretch round it, with its speed; softly into MaxBank
            float BankAt(float uu, out float sp)
            {
                float d = Mathf.Clamp(BankSeconds / dur, 0.01f, 0.08f), ua = Mathf.Clamp01(uu - d), ub = Mathf.Clamp01(uu + d), um = 0.5f * (ua + ub);
                At(ua, out var Pa, out _, out _, out _); At(um, out var Pm, out _, out _, out _); At(ub, out var Pb, out _, out _, out _);
                float dt = Mathf.Max(0.01f, (ub - ua) * 0.5f * dur);
                Vector3 v1 = Pm - Pa, v2 = Pb - Pm; sp = (v1.magnitude + v2.magnitude) * 0.5f / dt; v1.y = 0f; v2.y = 0f;
                if (v1.sqrMagnitude < 1e-6f || v2.sqrMagnitude < 1e-6f) return 0f;
                float yawRate = Vector3.SignedAngle(v1, v2, Vector3.up) * Mathf.Deg2Rad / dt;             // rad/s, + = turning right
                float flat = (v1.magnitude + v2.magnitude) * 0.5f / dt;
                float raw = Mathf.Atan(flat * yawRate / 9.81f) * Mathf.Rad2Deg * BankGain * p.bank * Mathf.SmoothStep(0f, 1f, flat / 4f);   // a drone that hardly moves does not bank
                float lim = Mathf.Max(1f, MaxBank);
                return lim * (float)System.Math.Tanh(raw / lim);
            }
            At(u, out P, out L, out F, out float R);
            bank = BankAt(u, out speed);
            float h = SmoothSeconds / dur;
            if (h > 1e-5f)
            {
                Vector3 ls = Vector3.zero; float bs = 0f, ws = 0f;
                for (int i = -2; i <= 2; i++)
                {
                    float w = 3f - Mathf.Abs(i), ui = Mathf.Clamp01(u + i * h);
                    At(ui, out _, out var li, out _, out _); ls += li * w; bs += BankAt(ui, out _) * w; ws += w;
                }
                L = ls / ws; bank = bs / ws;
            }
            if (p.bankOut > 0f) bank *= Mathf.SmoothStep(0f, 1f, (1f - u) / p.bankOut);
            if (ground != null) { float gy = ground(P) + 2.5f; if (P.y < gy) P.y = gy; }
            Vector3 dir = L - P; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, -bank + R);
            if (p.home)
            {
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.homeFrom, 1f, u));
                w = w * w * (3f - 2f * w);                                                                // eased twice: the turn into the start camera begins and ends without a kink
                rot = Quaternion.Slerp(rot, homeRot, w); F = Mathf.Lerp(F, homeFov, w);
            }
        }
    }

    public sealed partial class ZodFlyover
    {
        /// <summary>CineApply: the shot `name` at `u` of its `dur` seconds flown as a drone path. False = no path: the list's own move.</summary>
        bool L18DronePut(string name, float u, float dur, bool homeShot)
        {
            if (!camT || (cineLevel != "L18B" && cineLevel != "L18")) return false;                                              // the BLUE list only: the red list has a shot of the same name (the_gap) and flies its own moves
            if ((cineLevel == "L18") != name.StartsWith("red_")) return false;                              // each list flies its own paths only
            var p = OzL18Drone.PathOf(name); if (p == null) return false;
            Vector3 fwdH = homeRot * Vector3.forward, homeGround = homePos + fwdH * (fwdH.y < -0.05f ? (homePos.y - GroundAt(homePos)) / -fwdH.y : 80f);
            OzL18Drone.Pose(p, u, dur, homePos, homeRot, homeFov, homeGround, GroundAt, out var P, out var rot, out float F, out var L, out float speed, out float bank);
            if (p.home && homeShot && u >= 0.9999f) { P = homePos; rot = homeRot; F = homeFov; }
            camT.SetPositionAndRotation(P, rot);
            if (camC) camC.fieldOfView = F;
            if (rts) { rts.focus = new Vector3(L.x, 0f, L.z); rts.height = Mathf.Clamp(P.y, rts.minHeight, rts.maxHeight); }
            OzL18Drone.Frames++; OzL18Drone.Now = name; if (speed > OzL18Drone.SpeedMax) OzL18Drone.SpeedMax = speed; if (Mathf.Abs(bank) > OzL18Drone.RollMax) OzL18Drone.RollMax = Mathf.Abs(bank);
            OzL18DroneEntry.Log(name, u * dur, P, speed);
            return true;
        }
    }
}
