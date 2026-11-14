using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner items 77 / 78 / 78b / 81 / 88-90 (k75a): the FACTION screen before the level selection. UI/OpenZMainMenu.Faction.cs draws
    /// it, View/OzFactionPosters.cs renders the two posters' real 3D characters. This class holds its words, the side choice and the screen's
    /// pure motion rules (counted headless by Editor/OzV12FactionCheck.cs).
    ///  * The choice IS the old PLAY AS BLUE box's own setting (<see cref="OzPlayBlue.Box"/>, PlayerPrefs oz_play_blue, saved at once):
    ///    GENERAL ZOD = red (the default), GENERAL EVILZOD = blue. The player's own DEPLOY reads it exactly as before (OzPlayBlue.Arm), so
    ///    the blue side plays every level in the same blue mode (the human blue from the blue fort, the CPU red). Not offered in 'Zod
    ///    exact' (as the box: OzPlayBlue.Offered): there the EvilZod poster is locked.
    ///  * Mood (item 90): a soft warm red glow round Zod's poster as the pointer goes toward it, a soft cool blue one round EvilZod's,
    ///    none half way, eased (no jumps); the screen keeps its own dark backdrop (k75a2: far subtler than the first full-screen wash).
    ///    With the keys, a gamepad's D-pad or a finger it follows the poster in focus.
    ///  * The print's paper (k75a2): its light paper is aged a little and takes a share of the screen's colour behind the poster, so it no
    ///    longer reads as flat bright white (OzPosterFigure's print mode); the 3D characters, the ink and the titles stay as they are.
    ///  * Sway (items 88 / 89): each poster's camera orbits the poster's own pivot on a slow loop (+-4 deg yaw, +-1.5 deg pitch, 7.2 s, a
    ///    sine: eased, forward and back, never a jump); with the pointer over a poster the camera follows the pointer (+-8 deg yaw, +-3 deg
    ///    pitch), eased. The light models are complete for +-12 / +-5 deg; the screen never turns past +-8.5 / +-3.
    ///  * The 2D layers slide with the camera for depth: the back plate most, the mid layer (the star, the skyline) less; the frame and the
    ///    title band stay fixed to the print.
    /// </summary>
    public static class OzFaction
    {
        public const int Red = 0, Blue = 1;
        /// <summary>The posters' titles, drawn by the game on each poster's lower band.</summary>
        public static readonly string[] General = { "GENERAL ZOD", "GENERAL EVILZOD" };
        public static readonly string[] Company = { "ZODCORP", "EVIL TAKEOVER INDUSTRIES" };
        /// <summary>Item 81: the pick's joke as a tip while pointing at (or focused on) a poster; text only (the voice plays on the pick).</summary>
        public static readonly string[] Joke = { "Wise choice!", "You chose... poorly." };
        public const string TipWord = "TIP:", Speaker = "ZOD:";
        /// <summary>The small line under each poster while no tip shows.</summary>
        public static readonly string[] SideLine = { "RED FORT · THE CPU COMMANDS BLUE", "BLUE FORT · THE CPU COMMANDS RED" };
        /// <summary>Item 78b: Zod's voice on the pick (he says both), on his own voice bus: Resources/OpenZ/Zod/Voice.</summary>
        public static readonly string[] Voice = { "OpenZ/Zod/Voice/zod_pick_red", "OpenZ/Zod/Voice/zod_pick_blue" };
        /// <summary>Zod's level on the voice bus (dB), as his briefing and menu lines (ZodBriefing.voiceDb).</summary>
        public const float VoiceDb = -2f;
        /// <summary>After a pick the screen holds for Zod's line (its length + PickTail s, at least PickHoldMin s), then the level selection
        /// of that side opens; ENTER or a click goes on at once (the line plays on).</summary>
        public const float PickHoldMin = 0.9f, PickTail = 0.25f;

        // ---------------------------------------------------------------- 1.3 item 6: Zod's line only the first time
        // The FIRST pick on
        // this computer plays Zod's line and holds for it, as in 1.2; it is remembered (PlayerPrefs, so across restarts). From then on a
        // pick plays the stamp, the plate shows Zod's words, and the level selection opens on the next frame: no voice, no hold.
        /// <summary>The saved mark: Zod's pick line was heard on this computer.</summary>
        public const string HeardPref = "oz_faction_line_heard";
        /// <summary>
        /// </summary>
        public static bool OncePerSide = false;
        static string HeardKey(int side) => OncePerSide ? HeardPref + (side == Blue ? "_blue" : "_red") : HeardPref;
        /// <summary>Zod's line for this side was heard before (the pick then goes straight on).</summary>
        public static bool LineHeard(int side) => PlayerPrefs.GetInt(HeardKey(side), 0) == 1;
        /// <summary>Marks the line as heard (once it really played).</summary>
        public static void MarkHeard(int side)
        {
            if (LineHeard(side)) return;
            PlayerPrefs.SetInt(HeardKey(side), 1); PlayerPrefs.Save();
            Debug.Log($"[OpenZ Menu] Zod's pick line heard: from now on a pick goes straight to the level selection ({HeardKey(side)} 1, saved)");
        }
        /// <summary>How long the screen holds after a pick: for Zod's line (its length + PickTail, at least PickHoldMin) when it plays,
        /// else 0 (the level selection on the next frame).</summary>
        public static float PickHold(bool line, float lineLen) => line ? Mathf.Max(PickHoldMin, lineLen + PickTail) : 0f;
        /// <summary>Checks and proofs: the marks as they are now (-1 = not saved), so a run can put them back.</summary>
        public static int[] ProofHeardKeep()
        {
            string[] k = { HeardPref, HeardPref + "_red", HeardPref + "_blue" };
            var v = new int[k.Length];
            for (int i = 0; i < k.Length; i++) v[i] = PlayerPrefs.HasKey(k[i]) ? PlayerPrefs.GetInt(k[i], 0) : -1;
            return v;
        }
        public static void ProofHeardPutBack(int[] v)
        {
            if (v == null || v.Length < 3) return;
            string[] k = { HeardPref, HeardPref + "_red", HeardPref + "_blue" };
            for (int i = 0; i < k.Length; i++) { if (v[i] < 0) PlayerPrefs.DeleteKey(k[i]); else PlayerPrefs.SetInt(k[i], v[i]); }
            PlayerPrefs.Save();
        }
        /// <summary>Checks and proofs: a clean profile for the line (every mark cleared, nothing else).</summary>
        public static void ProofForgetLine()
        {
            PlayerPrefs.DeleteKey(HeardPref); PlayerPrefs.DeleteKey(HeardPref + "_red"); PlayerPrefs.DeleteKey(HeardPref + "_blue"); PlayerPrefs.Save();
        }
        public const string Eyebrow = "SELECT FACTION", Title = "CHOOSE YOUR GENERAL";
        public const string LockedChip = "NOT IN 'ZOD EXACT'", LockedLine = "THE BLUE SIDE IS OFF IN 'ZOD EXACT'";
        /// <summary>The level selection's tag line: the side picked on the faction screen.</summary>
        public static string SideName(bool blue) => General[blue ? Blue : Red];

        /// <summary>The side the faction screen shows as picked last (the box's saved state).</summary>
        public static int Chosen => OzPlayBlue.Box ? Blue : Red;
        public static string Tip(int side) => "<color=" + OzUi.Hex(OzUi.Amber) + ">" + TipWord + "</color> " + Joke[Side(side)];
        public static string Caption(int side) => "<color=" + OzUi.Hex(OzUi.Amber) + ">" + Speaker + "</color> " + Joke[Side(side)];
        static int Side(int s) => s == Blue ? Blue : Red;

        /// <summary>Checks: picks since the game started.</summary>
        public static int Picks { get; private set; }
        public static string LastPick { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { Picks = 0; LastPick = ""; }

        /// <summary>The pick: exactly what the PLAY AS BLUE box set (OzPlayBlue.Box, saved at once); DEPLOY arms the mode from it.</summary>
        public static void Choose(int side, string how)
        {
            bool blue = side == Blue;
            OzPlayBlue.Box = blue;
            Picks++; LastPick = blue ? "blue" : "red";
            Debug.Log($"[OpenZ Menu] faction picked ({how}): {General[Side(side)]} / {Company[Side(side)]}: {(blue ? "BLUE (the blue fort; the CPU commands red)" : "RED (the red fort; the CPU commands blue)")} ({OzPlayBlue.Pref} {(blue ? 1 : 0)}, saved)");
        }

        // ---------------------------------------------------------------- layout (reference units of the 1920 x 1080 canvas)
        public const float PosterTop = 196f, PosterH = 772f, PosterGap = 120f, TipGap = 16f, TipH = 34f;
        /// <summary>The posters' print: the layers' own aspect (1152 x 2048 = 9:16; the measured camera is 941:1672, 0.05 % apart).</summary>
        public const float PosterAspect = 9f / 16f;
        /// <summary>The titles on the lower band (fractions of the poster: the band starts at 0.806, its empty paper at 0.84 .. 0.96).</summary>
        public const float GeneralY = 0.878f, CompanyY = 0.937f, TitleW = 0.82f, GeneralSize = 34f, CompanySize = 14f;
        public static readonly Color32[] GeneralInk = { new Color32(168, 24, 28, 255), new Color32(24, 40, 104, 255) };
        public static readonly Color32[] CompanyInk = { new Color32(30, 44, 100, 255), new Color32(168, 28, 30, 255) };
        /// <summary>The paper rim round the 3D characters (the print outline of the owner's posters): its width as a share of the render's
        /// height (0 = none).</summary>
        public const float RimFrac = 0.0028f;
        /// <summary>The 1440 x 2560 grid the layers were designed on (their shifts and the grain tile are in its pixels).</summary>
        public const float GridW = 1440f, GridH = 2560f, GrainTile = 1024f;

        // ---------------------------------------------------------------- mood (item 90; k75a2: subtle)
        /// <summary>The mood's colours: a warm red, and a cool blue a little toward cyan (over the warm dark backdrop it reads blue, not
        /// violet).</summary>
        public static readonly Color32 MoodRed = new Color32(255, 70, 38, 255), MoodBlue = new Color32(0, 170, 255, 255);
        /// <summary>Each at full mood (the UI blends in linear light, where a few per cent already show clearly on the dark backdrop): the
        /// even wash under the posters, and no wash over them (the characters and the ink keep their colours; the paper takes the tint
        /// through <see cref="PaperSee"/>); the soft round glow behind the poster the mood points at is <see cref="Glow"/>. The ease time
        /// (s, a critically damped step: it starts gently and settles without overshoot); the dead zone round the middle (share of the
        /// half distance between the posters' middles).</summary>
        public const float WashUnder = 0.004f, WashOver = 0f, MoodTime = 0.35f, MoodDead = 0.1f;
        /// <summary>The glow at its middle, per side (Zod's red, EvilZod's blue: about the same felt strength), and its size (canvas units:
        /// an ellipse centred on the poster the mood points at, behind it).</summary>
        public static readonly float[] Glow = { 0.075f, 0.05f };
        public const float GlowW = 1500f, GlowH = 1250f;

        /// <summary>The glow's shape at (dx, dy) canvas units from its middle: 1 there, falling smoothly (a squared smoothstep) to 0 at the
        /// ellipse's edge; no hard edge anywhere.</summary>
        public static float GlowAt(float dx, float dy)
        {
            float x = dx / (GlowW * 0.5f), y = dy / (GlowH * 0.5f);
            float t = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            float s = 1f - t * t * (3f - 2f * t);
            return s * s;
        }

        // ---------------------------------------------------------------- the print's paper (k75a2)
        /// <summary>The print's light paper (OzPosterFigure in its print mode, on the back plate, the mid layer, the frame border and the band):
        /// a texel is paper by how light (sRGB luma from PaperLum.x to .y) and how colourless (chroma from PaperChroma.x to .y: none above) it
        /// is, so the ink, the colours, the 3D characters and the titles are never paper. The paper is aged (PaperTone multiplies it) and
        /// takes PaperSee of the screen's colour behind the poster (in linear light; on this dark backdrop it reads as about 17 % see-through,
        /// k105: a touch more than the 0.22 before, the paper only). PaperSeeInk: the mid layer's white print (EvilZod's flags' white stars,
        /// the army's highlights: print, not paper) keeps the earlier share. The stars, rays, frame lines, figures and titles take none.
        /// PaperBack: the backdrop beside the posters as measured on the screen (the bed over the blurred flyby); the menu adds the mood's
        /// wash and glow to it each frame (<see cref="PaperBehind"/>).</summary>
        public const float PaperSee = 0.30f, PaperSeeInk = 0.22f;
        public static readonly Color PaperTone = new Color(1f, 0.98f, 0.94f, 1f);
        public static readonly Vector2 PaperLum = new Vector2(0.55f, 0.80f), PaperChroma = new Vector2(0.10f, 0.28f);
        public static readonly Color32 PaperBack = new Color32(48, 33, 23, 255);

        /// <summary>The screen's colour behind a poster (sRGB, for the material) at a mood: the backdrop, the even wash and the glow at the
        /// poster's middle (glowAt: <see cref="GlowAt"/> of the poster's middle from the glow's; 1 on the poster the mood points at), blended
        /// in linear light as the UI does.</summary>
        public static Color PaperBehind(float mood, float glowAt)
        {
            Color k = (mood < 0f ? (Color)MoodRed : (Color)MoodBlue).linear, c = ((Color)PaperBack).linear;
            float m = Mathf.Clamp01(Mathf.Abs(mood));
            c = Color.Lerp(c, k, WashUnder * m);
            c = Color.Lerp(c, k, Glow[mood < 0f ? Red : Blue] * m * Mathf.Clamp01(glowAt));
            c.a = 1f;
            return c.gamma;
        }

        /// <summary>A copy of the OzPosterFigure material set for the faction screen: print = a print layer (straight alpha, the aged paper,
        /// no rim), else the 3D render (its paper rim aged the same).</summary>
        public static void SetPaper(Material m, bool print)
        {
            if (!m) return;
            m.SetFloat("_Print", print ? 1f : 0f);
            m.SetFloat("_PaperSee", PaperSee);
            m.SetFloat("_PaperSeeInk", PaperSeeInk);                                                       // k105: the mid layer's white print
            m.SetColor("_PaperTone", PaperTone);
            m.SetColor("_PaperBehind", PaperBehind(0f, 0f));
            m.SetVector("_PaperLum", new Vector4(PaperLum.x, PaperLum.y, 0f, 0f));
            m.SetVector("_PaperChroma", new Vector4(PaperChroma.x, PaperChroma.y, 0f, 0f));
            if (print) m.SetFloat("_RimPx", 0f);
        }

        /// <summary>The mood the pointer asks for: -1 (red) at Zod's poster middle and beyond, +1 (blue) at EvilZod's, 0 half way, a smooth
        /// step between (no jump anywhere).</summary>
        public static float MoodTarget(float x, float redX, float blueX)
        {
            float half = (blueX - redX) * 0.5f;
            if (Mathf.Abs(half) < 1f) return 0f;
            float t = Mathf.Clamp((x - (redX + blueX) * 0.5f) / half, -1f, 1f);
            float a = Mathf.Clamp01((Mathf.Abs(t) - MoodDead) / (1f - MoodDead));
            a = a * a * (3f - 2f * a);
            return t < 0f ? -a : a;
        }

        /// <summary>One frame of a critically damped ease toward the target (time = about the time to get there; it starts from rest, so a
        /// target that jumps never makes the value jump; no overshoot).</summary>
        public static float Damp(float v, float target, ref float vel, float time, float dt)
            => dt <= 0f ? v : Mathf.SmoothDamp(v, target, ref vel, Mathf.Max(0.01f, time), Mathf.Infinity, dt);
        public static Vector2 Damp(Vector2 v, Vector2 target, ref Vector2 vel, float time, float dt)
            => dt <= 0f ? v : Vector2.SmoothDamp(v, target, ref vel, Mathf.Max(0.01f, time), Mathf.Infinity, dt);

        /// <summary>The wash colour of a mood (-1 red .. +1 blue) at a strength (alpha at full mood).</summary>
        public static Color Wash(float mood, float strength)
        {
            Color c = mood < 0f ? (Color)MoodRed : MoodBlue;
            c.a = Mathf.Clamp01(Mathf.Abs(mood)) * strength;
            return c;
        }

        // ---------------------------------------------------------------- sway (items 88 / 89)
        public const float SwayYaw = 4f, SwayPitch = 1.5f, SwayPeriod = 7.2f, FollowYaw = 8f, FollowPitch = 3f, YawLimit = 8.5f, PitchLimit = 3f;
        /// <summary>Ease times (s, Damp) of the pointer follow and of the hover weight; the loop's share kept while following; CALM MODE
        /// keeps this share of the loop.</summary>
        public const float FollowTime = 0.3f, HoverTime = 0.4f, LoopUnderFollow = 0.3f, CalmLoop = 0.25f;

        /// <summary>The loop at time t (s): yaw = 4 sin, pitch = 1.5 cos of the same 7.2 s phase (a slow ellipse: eased, forward and back,
        /// never a jump); phase01 offsets one poster from the other.</summary>
        public static Vector2 Loop(float t, float phase01)
        {
            float a = (t / SwayPeriod + phase01) * Mathf.PI * 2f;
            return new Vector2(SwayYaw * Mathf.Sin(a), SwayPitch * Mathf.Cos(a));
        }

        /// <summary>The camera's yaw / pitch (deg): the loop, and with the pointer over the poster (hover 0..1) the pointer follow (follow =
        /// the pointer's place on the poster, x -1..1 left..right, y -1..1 bottom..top) with a third of the loop on top; clamped to the
        /// screen's limits. Pointer right = the camera moves right (yaw +); pointer up = the camera moves up (pitch -).</summary>
        public static Vector2 Angles(Vector2 loop, Vector2 follow, float hover)
        {
            var f = new Vector2(Mathf.Clamp(follow.x, -1f, 1f) * FollowYaw, -Mathf.Clamp(follow.y, -1f, 1f) * FollowPitch) + loop * LoopUnderFollow;
            var v = Vector2.Lerp(loop, f, Mathf.Clamp01(hover));
            return new Vector2(Mathf.Clamp(v.x, -YawLimit, YawLimit), Mathf.Clamp(v.y, -PitchLimit, PitchLimit));
        }

        /// <summary>The orbit of the measured poster camera: yaw about the world up through the pivot (yaw + = the camera moves to the
        /// viewer's right: a negative turn about +Y), then pitch about the camera's own right axis through the pivot (pitch + = the camera
        /// moves down and looks up: a negative turn about its right axis). Positions are local to the poster model's root.</summary>
        public static void Orbit(Vector3 pos0, Quaternion rot0, Vector3 pivot, float yaw, float pitch, out Vector3 pos, out Quaternion rot)
        {
            var qy = Quaternion.AngleAxis(-yaw, Vector3.up);
            Vector3 off = qy * (pos0 - pivot);
            rot = qy * rot0;
            var qp = Quaternion.AngleAxis(-pitch, rot * Vector3.right);
            off = qp * off; rot = qp * rot;
            pos = pivot + off;
        }

        /// <summary>The 2D layers' depth slide (pixels of the 1440 x 2560 grid; + x = the picture moves right, + y = down) for the
        /// camera's yaw / pitch: the layers lie behind the characters, so they slide with the camera, the back plate most.</summary>
        public const float BackSlidePx = 8f, MidSlidePx = 4.5f, BackLiftPx = 4f, MidLiftPx = 2.5f;
        public static Vector2 LayerSlide(float yaw, float pitch, bool back)
            => new Vector2(yaw / YawLimit * (back ? BackSlidePx : MidSlidePx), pitch / PitchLimit * (back ? BackLiftPx : MidLiftPx));

        /// <summary>The RawImage uv rect that slides a layer's picture by px (grid pixels).</summary>
        public static Rect SlideUv(Vector2 px) => new Rect(-px.x / GridW, px.y / GridH, 1f, 1f);

        // ---------------------------------------------------------------- the measured poster cameras (the posters' camera files)
        /// <summary>One poster: its resources, its measured camera (local to the model's root: position, rotation, vertical field of view),
        /// the orbit pivot and the loop's phase.</summary>
        public struct PosterCam
        {
            public string id; public Vector3 pos; public Quaternion rot; public Vector3 pivot; public float fov, phase;
        }

        public static readonly PosterCam[] Cams =
        {
            new PosterCam { id = "zod", pos = new Vector3(-0.31953f, -0.03926f, -2.74171f), rot = new Quaternion(-0.038985f, 0.041495f, -0.000355f, 0.998378f),
                            pivot = new Vector3(-0.09246f, 0.17392f, -0.01991f), fov = 30.9428f, phase = 0f },
            // the measured fit (a -10.9 deg roll, from the owner's picture where the sidekick sits lower right); EvilUpright: the upright fit
            new PosterCam { id = "evil", pos = new Vector3(0.06118f, 0.08603f, -3.54923f), rot = new Quaternion(-0.006124f, -0.015409f, -0.095244f, 0.995316f),
                            pivot = new Vector3(-0.04348f, 0.13969f, -0.00419f), fov = 30.9428f, phase = 0.37f },
        };
        /// <summary>The evil poster's upright camera (roll 0; the owner may pick it): the same lens, its pivot on its own central ray at the
        /// measured pivot's distance.</summary>
        public static bool EvilUpright = false;
        static readonly Vector3 EvilUpPos = new Vector3(-0.16036f, 0.16131f, -3.58036f);
        static readonly Quaternion EvilUpRot = new Quaternion(0.005536f, 0.023059f, -0.000128f, 0.999719f);

        public static PosterCam Cam(int side)
        {
            var c = Cams[side == Blue ? Blue : Red];
            if (side == Blue && EvilUpright)
            {
                float d = (c.pivot - c.pos).magnitude;
                c.pos = EvilUpPos; c.rot = EvilUpRot; c.pivot = EvilUpPos + EvilUpRot * Vector3.forward * d;
            }
            return c;
        }

        /// <summary>The light models' bounds in their roots (metres; the check compares the imported meshes with them).</summary>
        public static readonly Bounds[] ModelBounds =
        {
            new Bounds(new Vector3(0.00205f, -0.00025f, -0.0329f), new Vector3(1.1239f, 1.6883f, 1.8670f)),
            new Bounds(new Vector3(0.0043f, 0.00025f, -0.0041f), new Vector3(1.0172f, 1.5233f, 1.8890f)),
        };
    }
}
