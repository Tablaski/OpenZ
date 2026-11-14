using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// One component on his view,
    /// next to OzEvilZodFx (which adds it in Bind). It reads the rules' dash phase and the drawn brake every frame and draws, view only:
    /// <list type="bullet">
    /// <item>the trail: a bright white-blue core streak behind him with a soft halo, two or three energy ribbons twisting round it (a helix that opens behind him
    /// and turns), thin speed lines, sparks and soft motes shed along it; HDR over the bloom threshold, faded over about half a second; by day each glow
    /// also covers part of what is behind it (premultiplied), so blue reads blue on orange sand;</item>
    /// <item>the motion blur: up to eight afterimages of his own body (baked meshes, edge-lit, the same occlusion) overlapping along the path, fading;</item>
    /// <item>the ground trace: a blue glowing streak under his path that cools within a second and leaves a scorched scuff that fades over a few seconds;
    /// dust kicked from his feet, faint blue wisps; at the start a fan of dust kicked back (no spark: row 72);</item>
    /// <item>the front half-shield: a curved shell ahead of him (140 degrees, wider than his body) with a bright rim, a faint hex pattern and a light-bending band
    /// (the look's own smooth bend, or the game's heat distortion wave mode: a knob; the push shrinks with the camera's distance); it charges in the windup,
    /// holds in the burst, fades with his speed in the brake and breaks into a short ripple when he stops (as bright as a near-full shield's), with sparks
    /// and a sound.</item>
    /// </list>
    /// Polish round 2 (the first game frames): the ground's hot line is thin, short-lived and covers the sand by day; the shield's face and hex gather at a
    /// thin blue rim (the middle clear) with bright ends and bent flow streaks round its ends; the afterimages are his outline; sparks drift blue along the
    /// ribbons; the occlusion fades slower than the glow and the colours deepen with age; what is made only on screen asks every game camera drawing to
    /// the screen (the break's sound plays whether or not); the red eye lines are shorter.
    /// The brake and skid of row 8 (UnitView, OzEvilZodFx.BrakeFrame / Plant) are not touched. Every number is in OzEvilZodDashKnobs (one asset). LOW / LIGHTEST:
    /// fewer ribbons, lines, afterimages and particles, no bend band; CALM SCREEN: no flicker, a soft break, half the bend and the bloom. Nothing reads a random
    /// number of the rules (its own xorshift, seeded per dash).
    /// </summary>
    [DefaultExecutionOrder(83)]   // after OzEvilZodFx (82)
    public sealed class OzEvilZodDashLook : MonoBehaviour
    {
        /// <summary>OzEvilZodFx: the new look is on (its old chest ribbon, quick ghosts and +0.5 bloom stay off).</summary>
        public static bool On => OzEvilZodDashKnobs.I.on && !ProofBefore;
        /// <summary>Proof only (the capture helper's "before" take): round 2's look for a moment, without touching the knobs asset.</summary>
        public static bool ProofBefore;
        /// <summary>OzEvilZodFx: the round 2 eye lines run with the new look.</summary>
        public static bool EyeLines => OzEvilZodDashKnobs.I.eyeLines;

        /// <summary>Counters for the Play proof and the capture helper.</summary>
        public static int Dashes, Breaks, BreakSounds, Bakes, Sparks, Motes, DustPuffs, GroundPoints, MaxTrailVerts;
        public static float LastShieldPeak, LastDashMetres;
        public static string LastTier = "";
        /// <summary>The capture helper: moving frames on / off screen by the look's own test, the camera that said yes, the bend's largest push and the
        /// flow streaks' largest vertex count (reset by the helper before each dash).</summary>
        public static int GateSeen, GateMissed, FlowVertsMax;
        public static float BendPushMax;
        public static string LastGateCam = "";
        /// <summary>Proof only (the capture helper): -1 = the player's settings; 0 full, 1 lighter, 2 calm, 3 lighter and calm.</summary>
        public static int ForceTier = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Dashes = Breaks = BreakSounds = Bakes = Sparks = Motes = DustPuffs = GroundPoints = MaxTrailVerts = 0; LastShieldPeak = LastDashMetres = 0f; LastTier = ""; ForceTier = -1; ProofBefore = false;
            GateSeen = GateMissed = FlowVertsMax = 0; BendPushMax = 0f; LastGateCam = ""; camBuf = new Camera[8];
            baseMat = null; ghostMat = null; heatMat = null; looked = false;
        }

        /// <summary>OzEvilZodFx.Bind: once per EvilZod view.</summary>
        public static void Attach(OzEvilZodFx fx, UnitView v, OzEvilZodBody b)
        {
            if (!fx || !v || fx.GetComponent<OzEvilZodDashLook>()) return;
            var l = fx.gameObject.AddComponent<OzEvilZodDashLook>(); l.view = v; l.Init();                // b: his body is ready (OzEvilZodFx only binds a ready one)
        }

        const float PathStep = 0.15f, TwoPi = Mathf.PI * 2f;
        const int MaxPath = 640, AzSeg = 28, ElSeg = 12;
        static readonly int IdGain = Shader.PropertyToID("_Gain"), IdColor = Shader.PropertyToID("_Color"), IdFlicker = Shader.PropertyToID("_Flicker"),
            IdStreakAmt = Shader.PropertyToID("_StreakAmt"), IdStreakFreq = Shader.PropertyToID("_StreakFreq"), IdStreakSpeed = Shader.PropertyToID("_StreakSpeed"),
            IdLife = Shader.PropertyToID("_Life"), IdBreak = Shader.PropertyToID("_Break"), IdRim = Shader.PropertyToID("_Rim"), IdRimPow = Shader.PropertyToID("_RimPow"),
            IdFill = Shader.PropertyToID("_Fill"), IdHex = Shader.PropertyToID("_Hex"), IdHexSize = Shader.PropertyToID("_HexSize"), IdArcLen = Shader.PropertyToID("_ArcLen"),
            IdHeight = Shader.PropertyToID("_Height"), IdEdgeSoft = Shader.PropertyToID("_EdgeSoft"), IdRingGain = Shader.PropertyToID("_RingGain"), IdSeed = Shader.PropertyToID("_Seed"),
            IdScorchColor = Shader.PropertyToID("_ScorchColor"), IdScorchDark = Shader.PropertyToID("_ScorchDark"), IdStrength = Shader.PropertyToID("_Strength"),
            IdAlpha = Shader.PropertyToID("_Alpha"), IdBright = Shader.PropertyToID("_Bright"), IdPower = Shader.PropertyToID("_Power"),
            IdOcclude = Shader.PropertyToID("_Occlude"), IdBendCells = Shader.PropertyToID("_BendCells"),
            IdRimMinPx = Shader.PropertyToID("_RimMinPx"), IdFillToRim = Shader.PropertyToID("_FillToRim"), IdFillMul = Shader.PropertyToID("_FillMul"),
            IdHexMinPx = Shader.PropertyToID("_HexMinPx"), IdBracket = Shader.PropertyToID("_Bracket"), IdBracketW = Shader.PropertyToID("_BracketW"),
            IdGlowSharp = Shader.PropertyToID("_GlowSharp"), IdGlowBreak = Shader.PropertyToID("_GlowBreak"), IdBody = Shader.PropertyToID("_Body");

        static Material baseMat, ghostMat, heatMat; static bool looked;
        static void LoadMats()
        {
            if (looked) return; looked = true;
            baseMat = Resources.Load<Material>("OpenZ/FX/M_OzEzDash");
            if (!baseMat || !baseMat.shader || !baseMat.shader.isSupported) { var sh = Shader.Find("OpenZ/EzDash"); baseMat = sh && sh.isSupported ? new Material(sh) { hideFlags = HideFlags.DontSave } : null; }
            if (!baseMat) Debug.LogWarning("[OpenZ EvilZod] OpenZ/EzDash is missing or not supported: the dash draws its afterimages, dust and sparks only");
            ghostMat = Resources.Load<Material>(OzHeroSquadView.PrefabDir + "M_EvilZod_Ghost");
            heatMat = Resources.Load<Material>("OpenZ/FX/M_HeatDistort");
        }

        struct PathPt { public Vector3 p; public float t, s, e; }
        struct GPt { public Vector3 l, r; public float t, s, flare; public int strip; }
        struct Ghost { public Mesh mesh; public Matrix4x4 at, gunAt, slideAt; public float born; public bool on; }

        UnitView view; SkinnedMeshRenderer skin; Mesh gunMesh, slideMesh; Transform gunT, slideT;
        readonly List<PathPt> path = new List<PathPt>(MaxPath);
        readonly List<GPt> gpts = new List<GPt>(256);
        Ghost[] ghosts = new Ghost[0]; int ghostNext;
        Material trailMat, groundMat, shieldMat, afterMat, bendOwn, bendHeat; MaterialPropertyBlock mpb;
        Mesh trailMesh, groundMesh, shieldMesh, bendMesh, flowMesh; float meshAng = -1f, meshElMin, meshElMax;
        TrailRenderer eyeL, eyeR; float eyeT0 = -1f;                                               // round 2's red eye lines (OzEvilZodFx makes them on the first dash)
        readonly List<Vector3> vs = new List<Vector3>(4096), ns = new List<Vector3>(512);
        readonly List<Color> cs = new List<Color>(4096);
        readonly List<Vector2> u0 = new List<Vector2>(4096), u1 = new List<Vector2>(4096);
        readonly List<int> tris = new List<int>(8192);
        Vector3[] tp = new Vector3[MaxPath]; float[] tw = new float[MaxPath], tg = new float[MaxPath], ts = new float[MaxPath], ta = new float[MaxPath];
        Color[] tc = new Color[MaxPath];
        float stripMinK, stripOrtho;                                                               // the strips' least width: metres a pixel per metre away (or flat, orthographic)

        ZEvilPhase lastPhase; bool wasMoving, lastFeetOk, dead, footFlip; Vector3 lastFeet, fwd = Vector3.forward, side = Vector3.right;
        float speedSm, speed01, stopAt = -99f, ghostLeft, groundLeft, sparkLeft, moteLeft, dustLeft, wispLeft, dashMetres;
        float shield, breakAge = -1f, breakLife; Vector3 breakAt, breakFwd; int strip; uint rng = 1u;

        /// <summary>For the capture helper: the shield now (0..1), the trail points, the ground points, the dash running.</summary>
        public float ShieldNow => breakAge >= 0f ? breakLife : shield;
        public int TrailPoints => path.Count;
        public int TracePoints => gpts.Count;
        public bool Moving => wasMoving;

        void Init()
        {
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (var r in GetComponentsInChildren<MeshFilter>(true))
            {
                if (r.name == "Shotgun") { gunMesh = r.sharedMesh; gunT = r.transform; }
                else if (r.name == "Shotgun_Pump") { slideMesh = r.sharedMesh; slideT = r.transform; }
            }
            mpb = new MaterialPropertyBlock();
            LoadMats();
            if (baseMat)
            {
                trailMat = Inst("EvilZodDashTrail", 0f, 3021, 0f, 0f);
                shieldMat = Inst("EvilZodDashShield", 1f, 3022, 0f, 0f);
                groundMat = Inst("EvilZodDashGround", 2f, 3001, -2f, -4f);   // over the ground and the water's 2990, under the dust (3008)
                afterMat = Inst("EvilZodDashAfterimage", 3f, 3015, 0f, 0f); afterMat.SetFloat("_Cull", 2f);   // round 2's ghosts' queue, back faces off
                bendOwn = Inst("EvilZodDashBend", 4f, 2985, 0f, 0f); bendOwn.SetFloat(IdStrength, 0f);        // the heat distortion's queue: before the water
            }
            if (heatMat && heatMat.shader && heatMat.shader.isSupported)
            {
                bendHeat = new Material(heatMat) { name = "EvilZodDashBendHeat (runtime)", hideFlags = HideFlags.DontSave };
                bendHeat.SetFloat("_Mode", 1f); bendHeat.SetFloat(IdStrength, 0f);
            }
            trailMesh = NewMesh("EvilZod dash trail"); groundMesh = NewMesh("EvilZod dash ground trace"); flowMesh = NewMesh("EvilZod dash bent flow streaks");
        }

        static Camera[] camBuf = new Camera[8];
        /// <summary>Is this point on screen, give or take `margin` of the screen: the main camera, or else any other game camera drawing to the screen
        /// (the first game takes of 3 Nov had a whole dash where the main camera's test said no while he was in the picture: no afterimage, spark or dust,
        /// and the break silent). No far-plane test (one hero, cheap).</summary>
        public static bool Seen(Vector3 at, float margin)
        {
            var m = Camera.main;
            if (m && In(m, at, margin)) { LastGateCam = m.name; return true; }
            int n = Camera.allCamerasCount; if (camBuf.Length < n) camBuf = new Camera[n + 4];
            n = Camera.GetAllCameras(camBuf);
            for (int i = 0; i < n; i++)
            {
                var c = camBuf[i];
                if (!c || c == m || c.targetTexture || c.cameraType != CameraType.Game) continue;
                if (In(c, at, margin)) { LastGateCam = c.name; return true; }
            }
            LastGateCam = m ? m.name + " (no)" : "no main camera";
            return false;
        }
        static bool In(Camera c, Vector3 at, float mg)
        {
            var vp = c.WorldToViewportPoint(at);
            return vp.z > c.nearClipPlane && vp.x > -mg && vp.x < 1f + mg && vp.y > -mg && vp.y < 1f + mg;
        }

        /// <summary>Round 2's red eye lines (OzEvilZodFx.EnsureTrails, on the first dash): found by name once they exist.</summary>
        void FindEyes()
        {
            if (eyeL) return;
            foreach (var r in GetComponentsInChildren<TrailRenderer>(true))
            {
                if (r.name == "EvilZodEyeL") eyeL = r; else if (r.name == "EvilZodEyeR") eyeR = r;
            }
            if (eyeL) eyeT0 = eyeL.time;
        }
        void EyeTime(float want)
        {
            if (eyeL && want > 0f && eyeL.time != want) eyeL.time = want;
            if (eyeR && want > 0f && eyeR.time != want) eyeR.time = want;
        }

        static Material Inst(string n, float mode, int queue, float offF, float offU)
        {
            var m = new Material(baseMat) { name = n + " (runtime)", renderQueue = queue, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Mode", mode); m.SetFloat("_OffF", offF); m.SetFloat("_OffU", offU);
            return m;
        }

        static Mesh NewMesh(string n) { var m = new Mesh { name = n, hideFlags = HideFlags.DontSave }; m.MarkDynamic(); return m; }

        // ---------------------------------------------------------------------------- frame
        void LateUpdate()
        {
            if (!view) return;
            var k = OzEvilZodDashKnobs.I; float now = Time.time, dt = Time.deltaTime;
            if (!On) { if (path.Count > 0 || gpts.Count > 0 || shield > 0f || breakAge >= 0f) ClearAll(); EyeTime(eyeT0); return; }
            EyeTime(k.eyeLineLife > 0f ? k.eyeLineLife : eyeT0);
            var o = view.Obj;
            if (view.EvilShocked || view.Dying || o == null)
            {   // his death: the shield goes at once, what was laid down fades as it would
                if (!dead) { dead = true; shield = 0f; breakAge = -1f; wasMoving = false; }
                Draw(k, now, Tier(k));
                return;
            }
            var tier = Tier(k);
            var phase = o.EvilPhase; bool burst = phase == ZEvilPhase.Burst, move = burst || view.EvilBraking;
            Vector3 feet = transform.position;
            float gs = ZGame.I ? Mathf.Max(0.25f, ZGame.I.gameSpeed) : 1f;
            if (dt > 0f && lastFeetOk) { float v = (feet - lastFeet).magnitude / dt / gs; speedSm = Mathf.Lerp(speedSm, v, 1f - Mathf.Exp(-dt * 25f)); }
            speed01 = Mathf.Clamp01(speedSm / Mathf.Max(1f, UnitView.EvilBurstMps));

            if (phase == ZEvilPhase.Windup && lastPhase != ZEvilPhase.Windup) { OnWindup(o); FindEyes(); }
            if (move && !wasMoving) OnGo(k, o, feet, now, tier);
            if (!move && wasMoving) OnStop(k, o, feet, now, tier);
            if (move) MoveFrame(k, o, feet, now, tier);

            float want = phase == ZEvilPhase.Windup ? k.shieldWindup : burst ? 1f : move ? Mathf.Lerp(k.shieldBrakeFloor, 1f, speed01) : 0f;
            if (!k.shieldOn) want = 0f;
            float rate = want > shield ? Mathf.Max(0.01f, k.shieldIn) : Mathf.Max(0.01f, k.shieldOut);
            shield = Mathf.MoveTowards(shield, want, dt / rate);
            if (move) LastShieldPeak = Mathf.Max(LastShieldPeak, shield);

            Draw(k, now, tier);
            lastPhase = phase; wasMoving = move; lastFeet = feet; lastFeetOk = true;
        }

        /// <summary>The lighter setting in force: 0 full, 1 lighter (LOW, LIGHTEST, MEDIUM when asked); calm = CALM SCREEN.</summary>
        struct TierInfo { public bool lite, calm; }
        static readonly string[] TierNames = { "full", "lighter", "full, calm", "lighter, calm" };
        static TierInfo Tier(OzEvilZodDashKnobs k)
        {
            int q = OzQuality.Level;
            var t = new TierInfo { lite = OzLightRows.Lightest || q >= 2 || (k.liteOnMedium && q == 1), calm = OzMotion.Reduced };
            if (ForceTier >= 0) { t.lite = (ForceTier & 1) != 0; t.calm = (ForceTier & 2) != 0; }
            LastTier = TierNames[(t.lite ? 1 : 0) + (t.calm ? 2 : 0)];
            return t;
        }

        Vector3 DashDir(ZObject o)
        {
            var d = new Vector3((o.EvilDashToX - o.EvilDashFromX) / 8f, 0f, -(o.EvilDashToY - o.EvilDashFromY) / 8f);
            if (d.sqrMagnitude > 0.25f) return d.normalized;
            var f = -transform.forward; f.y = 0f;                                                   // every robot model faces its root's -z
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        float R01() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng & 0xFFFFFF) / 16777216f; }
        float RR(float a, float b) => a + (b - a) * R01();

        void OnWindup(ZObject o)
        {
            fwd = DashDir(o); side = Vector3.Cross(Vector3.up, fwd); breakAge = -1f;
        }

        void OnGo(OzEvilZodDashKnobs k, ZObject o, Vector3 feet, float now, TierInfo tier)
        {
            Dashes++; LastShieldPeak = 0f; dead = false; breakAge = -1f; FindEyes();                     // once a dash: OzEvilZodFx makes the eye lines on the first one
            rng = ((uint)(o.Id * 7919 + Dashes * 104729) | 1u); R01();
            fwd = DashDir(o); side = Vector3.Cross(Vector3.up, fwd);
            var from = lastFeetOk ? lastFeet : feet;                                              // the trail starts where he stood
            path.Clear(); dashMetres = 0f;
            path.Add(new PathPt { p = from + Vector3.up * k.coreHeight, t = now - Time.deltaTime, s = 0f, e = 1f });
            ghostLeft = k.afterimageSpacing * 0.5f; groundLeft = 0f; sparkLeft = 0.3f; moteLeft = 0.5f; dustLeft = 0.2f; wispLeft = 0.4f;
            strip++;
            if (k.groundOn) AddGround(k, from, now, k.groundStartFlare);
            var z = OpenZFxPlayer.I;
            if (!z || !Seen(feet, k.screenMargin)) return;
            float pm = (tier.lite ? k.liteParticles : 1f) * k.dustAmount; var dirt = OzVolcanic.Dust(k.dustColor);
            int n = Mathf.RoundToInt(k.dustStart * pm);
            for (int i = 0; i < n; i++)
            {   // the push-off: a fan of dust kicked back
                float a = (n > 1 ? Mathf.Lerp(-75f, 75f, i / (float)(n - 1)) : 0f) + RR(-6f, 6f);
                var d = Quaternion.AngleAxis(a, Vector3.up) * -fwd;
                z.EmitChannel("m12_dust", from + d * 0.5f + Vector3.up * 0.2f, d * (k.dustStartSpeed * RR(0.6f, 1f)) + Vector3.up * RR(0.5f, 1.4f), RR(0.8f, 1.2f), RR(0.8f, 1.1f), dirt, Mathf.Clamp01(k.dustAlpha + 0.04f));
                DustPuffs++;
            }
            int w = Mathf.RoundToInt(5f * pm);
            for (int i = 0; i < w; i++)
            {
                var d = Quaternion.AngleAxis(RR(0f, 360f), Vector3.up) * Vector3.forward;
                z.EmitChannel("wv2_glow", from + d * RR(0.3f, 0.9f) + Vector3.up * 0.25f, d * RR(0.4f, 1f) + Vector3.up * 0.3f, RR(0.35f, 0.55f), RR(0.35f, 0.6f), new Color(0.25f, 0.45f, 1f), k.wispAlpha);
                Motes++;
            }
        }

        void MoveFrame(OzEvilZodDashKnobs k, ZObject o, Vector3 feet, float now, TierInfo tier)
        {
            var d = DashDir(o); if (Vector3.Dot(d, fwd) > 0.2f) { fwd = d; side = Vector3.Cross(Vector3.up, fwd); }
            float e = o.EvilPhase == ZEvilPhase.Burst ? 1f : speed01;
            var at = feet + Vector3.up * k.coreHeight;
            float s0 = path.Count > 0 ? path[path.Count - 1].s : 0f;
            AddPath(at, now, e);
            float step = (path.Count > 0 ? path[path.Count - 1].s : 0f) - s0; dashMetres += step; LastDashMetres = dashMetres;
            if (step <= 0f) return;
            bool seen = Seen(feet, k.screenMargin); if (seen) GateSeen++; else GateMissed++;
            var prev = lastFeetOk ? lastFeet : feet; float seg = Vector3.Distance(prev, feet);
            // a point b metres back along this frame's move: what is laid down by distance lies evenly, however far one frame goes
            Vector3 Back(float b) => seg > 1e-4f ? Vector3.Lerp(feet, prev, Mathf.Clamp01(b / seg)) : feet;
            // afterimages every afterimageSpacing metres, laid back along this frame's move (his pose of this frame), oldest first; a frame that went
            // further than afterimageMaxPerFrame of them drops the rest (no backlog)
            ghostLeft -= step;
            float spacing = Mathf.Max(0.3f, k.afterimageSpacing), frameDt = Time.deltaTime;
            for (int g = 0; ghostLeft <= 0f && g < Mathf.Max(1, k.afterimageMaxPerFrame); g++)
            {
                float b = -ghostLeft; ghostLeft += spacing;
                if (seen && speed01 > 0.2f) Bake(k, tier, Back(b) - feet, seg > 1e-4f ? Mathf.Clamp01(b / seg) * frameDt : 0f);
            }
            if (ghostLeft <= 0f) ghostLeft = Mathf.Min(0.1f, spacing);
            // the ground trace
            if (k.groundOn)
            {
                groundLeft -= step;
                for (int g = 0; groundLeft <= 0f && g < 64; g++) { AddGround(k, Back(-groundLeft), now, 1f); groundLeft += Mathf.Max(0.15f, k.groundStep); }
            }
            var z = OpenZFxPlayer.I; if (!z || !seen) return;
            float pm = tier.lite ? k.liteParticles : 1f;
            int nRib = Mathf.Max(1, tier.lite ? Mathf.Min(k.liteRibbons, k.ribbonCount) : k.ribbonCount);
            float sHead = path[path.Count - 1].s, up = k.coreHeight;
            // sparks off the ribbons, motes left in the air
            if (k.sparksPerMetre > 0f && pm > 0f)
            {
                sparkLeft -= step * e;
                for (int g = 0; sparkLeft <= 0f && g < 32; g++) { float b = -sparkLeft / Mathf.Max(0.05f, e); Spark(z, k, Back(b) + Vector3.up * up, nRib, now, sHead - b); sparkLeft += 1f / Mathf.Max(0.05f, k.sparksPerMetre * pm); }
            }
            if (k.motesPerMetre > 0f && pm > 0f)
            {
                moteLeft -= step * e;
                for (int g = 0; moteLeft <= 0f && g < 32; g++)
                {
                    var r = side * RR(-1f, 1f) * k.ribbonRadius * 0.6f + Vector3.up * RR(-0.6f, 0.6f) * k.ribbonRadius;
                    z.EmitChannel("wv2_glow", Back(-moteLeft / Mathf.Max(0.05f, e)) + Vector3.up * up + r, -fwd * RR(0.5f, 1.5f) + Vector3.up * RR(0.1f, 0.5f), k.moteLife * RR(0.8f, 1.2f), k.moteSize * RR(0.7f, 1.3f), k.sparkColor * 0.55f, 0.45f);
                    Motes++; moteLeft += 1f / Mathf.Max(0.05f, k.motesPerMetre * pm);
                }
            }
            // dust from his feet, faint blue wisps
            float dm = pm * k.dustAmount; var dirt = OzVolcanic.Dust(k.dustColor);
            if (dm > 0f && k.footDustEvery > 0f)
            {
                dustLeft -= step;
                for (int g = 0; dustLeft <= 0f && g < 32; g++)
                {
                    footFlip = !footFlip; float sg = footFlip ? 1f : -1f;
                    var heel = Back(-dustLeft) + side * (0.38f * sg) + Vector3.up * 0.15f;
                    z.EmitChannel("m12_dust", heel, -fwd * RR(1.5f, 3.2f) * (0.4f + 0.6f * e) + side * (sg * RR(0.3f, 1f)) + Vector3.up * RR(0.4f, 1.1f), RR(0.7f, 1.1f), k.footDustSize * RR(0.8f, 1.2f), dirt, k.dustAlpha);
                    DustPuffs++; dustLeft += Mathf.Max(0.2f, k.footDustEvery) / dm;
                }
            }
            if (k.wispEvery > 0f && dm > 0f)
            {
                wispLeft -= step;
                for (int g = 0; wispLeft <= 0f && g < 32; g++)
                {
                    z.EmitChannel("wv2_glow", Back(-wispLeft) + side * RR(-0.5f, 0.5f) + Vector3.up * 0.2f, -fwd * RR(0.5f, 1.2f) + Vector3.up * 0.3f, RR(0.35f, 0.55f), RR(0.4f, 0.6f), new Color(0.25f, 0.45f, 1f), k.wispAlpha);
                    Motes++; wispLeft += Mathf.Max(0.3f, k.wispEvery) / dm;
                }
            }
        }

        void Spark(OpenZFxPlayer z, OzEvilZodDashKnobs k, Vector3 at, int nRib, float now, float s)
        {
            int j = Mathf.Min(nRib - 1, (int)(R01() * nRib));
            float ph = TwoPi * (j / (float)nRib + k.ribbonTwist * s) + k.ribbonSpin * now;
            var radial = side * Mathf.Cos(ph) + Vector3.up * (Mathf.Sin(ph) * k.ribbonUpScale);
            float r = k.ribbonRadius * k.ribbonRadiusHead * RR(0.9f, 1.4f);
            // out from the ribbon a little, back along the trail and up against the channel's gravity: they stream along the ribbons (his picture 1)
            var vel = radial.normalized * (k.sparkSpeed * RR(0.6f, 1.4f)) - fwd * (k.sparkDrift * RR(0.6f, 1.4f)) + Vector3.up * (k.sparkLift * RR(0.7f, 1.3f));
            z.EmitChannel("wv2_spark", at + radial * r - fwd * RR(0.3f, 1.2f), vel, k.sparkLife * RR(0.7f, 1.3f), k.sparkSize, k.sparkColor, 1f);
            Sparks++;
        }

        void AddPath(Vector3 at, float now, float e)
        {
            if (path.Count == 0) { path.Add(new PathPt { p = at, t = now, s = 0f, e = e }); return; }
            var last = path[path.Count - 1]; var d = at - last.p; float len = d.magnitude;
            if (len < 1e-3f) return;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / PathStep), 1, 96);
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                path.Add(new PathPt { p = last.p + d * u, t = Mathf.Lerp(last.t, now, u), s = last.s + len * u, e = Mathf.Lerp(last.e, e, u) });
            }
            if (path.Count > MaxPath) path.RemoveRange(0, path.Count - MaxPath);
        }

        void AddGround(OzEvilZodDashKnobs k, Vector3 feet, float now, float flare)
        {
            var g = ZGame.I; if (!g) return;
            var z = OpenZFxPlayer.I;
            if (z && z.WetAt(feet)) { strip++; return; }                                         // never on water or lava: the trace breaks there
            float hw = k.groundWidth * 0.5f * flare;
            Vector3 a = feet - side * hw, b = feet + side * hw;
            a.y = g.GroundY(a.x, a.z) + k.groundLift; b.y = g.GroundY(b.x, b.z) + k.groundLift;
            float s = 0f;
            if (gpts.Count > 0) { var p = gpts[gpts.Count - 1]; if (p.strip == strip) s = p.s + Vector3.Distance((p.l + p.r) * 0.5f, (a + b) * 0.5f); }
            gpts.Add(new GPt { l = a, r = b, t = now, s = s, flare = flare, strip = strip });
            GroundPoints++;
        }

        void OnStop(OzEvilZodDashKnobs k, ZObject o, Vector3 feet, float now, TierInfo tier)
        {
            stopAt = now;
            if (!k.shieldOn || shield < 0.05f) { shield = 0f; return; }
            Breaks++; breakAge = 0f; breakLife = shield; shield = 0f; breakFwd = fwd;
            breakAt = feet + Vector3.up * k.shieldHeight + fwd * k.shieldForward;
            // its sound first, on screen or not (positional; every visible action has its sound, and the on-screen test once said no while he was seen)
            if (!string.IsNullOrEmpty(k.breakSound) && k.breakVolume > 0f) { AudioDirector.Fire(k.breakSound, breakAt, o.Id, k.breakVolume, k.breakPitch); BreakSounds++; }
            if (!Seen(feet, k.screenMargin)) return;
            var z = OpenZFxPlayer.I;
            if (z)
            {
                int n = Mathf.RoundToInt(k.shieldBreakSparks * (tier.lite ? k.liteParticles : 1f));
                float ang = k.shieldAngle * Mathf.Deg2Rad, r = k.shieldRadius;
                for (int i = 0; i < n; i++)
                {   // the shell's pieces fly off outwards
                    float az = (R01() - 0.5f) * ang, el = Mathf.Lerp(k.shieldElevMin, k.shieldElevMax, RR(0.15f, 0.85f)) * Mathf.Deg2Rad;
                    var dir = Quaternion.LookRotation(fwd, Vector3.up) * new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el) * k.shieldSquash, Mathf.Cos(az) * Mathf.Cos(el));
                    z.EmitChannel("wv2_spark", breakAt + dir * r, dir * (k.shieldBreakSparkSpeed * RR(0.6f, 1.4f)) + Vector3.up * RR(0.3f, 1f), RR(0.2f, 0.4f), k.sparkSize * k.shieldBreakSparkSize, k.sparkColor, 1f);
                    Sparks++;
                }
                if (z.pointLights && !tier.calm) z.AddLight(breakAt, new Color(0.35f, 0.6f, 1f), 9f, 7f, 0.16f);
            }
        }

        void ClearAll()
        {
            path.Clear(); gpts.Clear(); shield = 0f; breakAge = -1f; wasMoving = false;
            for (int i = 0; i < ghosts.Length; i++) ghosts[i].on = false;
        }

        // ---------------------------------------------------------------------------- afterimages
        /// <summary>One afterimage: his pose now, moved by `off` (back along this frame's move), born `ago` seconds ago.</summary>
        void Bake(OzEvilZodDashKnobs k, TierInfo tier, Vector3 off, float ago)
        {
            if (!skin || (!afterMat && !ghostMat)) return;
            int want = Mathf.Clamp(tier.lite ? Mathf.Min(k.liteAfterimages, k.afterimageCount) : k.afterimageCount, 0, 12);
            if (want == 0) return;
            if (ghosts.Length != want)
            {
                foreach (var old in ghosts) if (old.mesh) Destroy(old.mesh);
                ghosts = new Ghost[want]; ghostNext = 0;
            }
            ref var g = ref ghosts[ghostNext]; ghostNext = (ghostNext + 1) % ghosts.Length;
            if (!g.mesh) { g.mesh = new Mesh { name = "EvilZod afterimage", hideFlags = HideFlags.DontSave }; g.mesh.MarkDynamic(); }
            skin.BakeMesh(g.mesh, true);
            g.at = Matrix4x4.TRS(skin.transform.position + off, skin.transform.rotation, Vector3.one);
            var move = Matrix4x4.Translate(off);
            if (gunT) g.gunAt = move * gunT.localToWorldMatrix;
            if (slideT) g.slideAt = move * slideT.localToWorldMatrix;
            g.born = Time.time - ago; g.on = true; Bakes++;
        }

        // ---------------------------------------------------------------------------- drawing
        void Draw(OzEvilZodDashKnobs k, float now, TierInfo tier)
        {
            float dn = Mathf.Lerp(1f, k.nightGain, NightGrade.NightK), hdr = k.hdrMul * dn;
            int layer = gameObject.layer;
            float maxLife = Mathf.Max(k.coreLife, Mathf.Max(k.ribbonLife, Mathf.Max(k.glowLife, k.lineLife))) + 0.05f;
            int cut = 0; while (cut < path.Count - 1 && now - path[cut].t > maxLife) cut++;
            if (cut > 0) path.RemoveRange(0, cut);
            if (path.Count > 0 && !wasMoving && now - path[path.Count - 1].t > maxLife) path.Clear();
            float active = wasMoving ? 1f : Mathf.Clamp01(1f - (now - stopAt) / 0.5f);
            if (active > 0f && (path.Count > 1 || wasMoving) && Seen(transform.position, 0f))   // the dash's only bloom hold (round 2's +0.5 is off with the look on); on screen only, as round 2's
                OpenZBloomDirector.Sustain(Mathf.Lerp(k.bloomBoostDay, k.bloomBoostNight, NightGrade.NightK) * active * (tier.calm ? k.calmBloom : 1f));
            float occ = Mathf.Max(0f, Mathf.Lerp(k.occludeDay, k.occludeNight, NightGrade.NightK));      // day: the glow also covers what is behind it
            var camPos = PrepStrips(k);
            if (trailMat)
            {   // the strips' material (the trail and the shield's bent flow streaks)
                trailMat.SetFloat(IdGain, 1f); trailMat.SetFloat(IdFlicker, tier.calm ? k.calmFlicker : k.flicker); trailMat.SetFloat(IdOcclude, occ);
                trailMat.SetFloat(IdStreakAmt, k.streakAmt); trailMat.SetFloat(IdStreakFreq, k.streakFreq); trailMat.SetFloat(IdStreakSpeed, k.streakSpeed);
            }
            if (trailMat && path.Count > 1) DrawTrail(k, now, tier, hdr, layer, camPos);
            DrawGhosts(k, now, dn, occ, layer);
            if (groundMat) DrawGround(k, now, hdr, occ, layer);
            if (shieldMat) DrawShield(k, now, tier, hdr, occ, layer, camPos);
        }

        /// <summary>Once a frame: the camera's place and the strips' least width (minStripPx pixels on screen: a thinner one crawls or vanishes at the RTS
        /// camera; it widens and dims instead).</summary>
        Vector3 PrepStrips(OzEvilZodDashKnobs k)
        {
            var cam = Camera.main; Vector3 camPos = cam ? cam.transform.position : transform.position + Vector3.up * 50f;
            stripMinK = stripOrtho = 0f;
            if (cam && k.minStripPx > 0f)
            {
                float px = Mathf.Max(1, cam.pixelHeight);
                if (cam.orthographic) stripOrtho = k.minStripPx * 2f * cam.orthographicSize / px;
                else stripMinK = k.minStripPx * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / px;
            }
            return camPos;
        }

        static float Fade(float age, float life) { float x = Mathf.Clamp01(age / Mathf.Max(0.01f, life)); x = 1f - x; return x * Mathf.Sqrt(x); }

        void DrawTrail(OzEvilZodDashKnobs k, float now, TierInfo tier, float hdr, int layer, Vector3 camPos)
        {
            vs.Clear(); cs.Clear(); u0.Clear(); u1.Clear(); tris.Clear();
            int n = path.Count; float sHead = path[n - 1].s, op = Mathf.Max(0.05f, k.occludeFadePow);
            // the halo and the core; what each covers fades slower than its glow (fade^occludeFadePow), the halo deepens with age
            int c = 0;
            for (int i = 0; i < n; i++)
            {
                var q = path[i]; float age = now - q.t, f = Fade(age, k.glowLife); if (f <= 0f && c == 0) continue;
                float h = Mathf.SmoothStep(0f, 1f, (sHead - q.s) / Mathf.Max(0.01f, k.headGap)), fe = f * q.e * h;
                tp[c] = q.p; ts[c] = q.s; tw[c] = k.glowWidth * (0.6f + 0.4f * f); tg[c] = k.glowIntensity * hdr * fe; ta[c] = k.occludeHalo * Mathf.Pow(f, op) * q.e * h;
                tc[c] = Color.Lerp(k.glowColor, k.glowColorOld, Mathf.Clamp01(age / Mathf.Max(0.01f, k.glowLife))); c++;
            }
            Strip(c, 0f, camPos);
            c = 0;
            for (int i = 0; i < n; i++)
            {
                var q = path[i]; float age = now - q.t, f = Fade(age, k.coreLife); if (f <= 0f && c == 0) continue;
                float h = Mathf.SmoothStep(0f, 1f, (sHead - q.s) / Mathf.Max(0.01f, k.headGap)), fe = f * q.e * h;
                tp[c] = q.p; ts[c] = q.s; tw[c] = Mathf.Lerp(k.coreTailWidth, k.coreWidth, f); tg[c] = k.coreIntensity * hdr * fe; ta[c] = k.occludeCore * Mathf.Pow(f, op) * q.e * h;
                tc[c] = k.coreColor; c++;
            }
            Strip(c, 0f, camPos);
            // the ribbons: a helix round the core that opens behind him and turns
            int nRib = Mathf.Max(0, tier.lite ? Mathf.Min(k.liteRibbons, k.ribbonCount) : k.ribbonCount);
            for (int j = 0; j < nRib; j++)
            {
                c = 0; float ph0 = TwoPi * j / nRib;
                for (int i = 0; i < n; i++)
                {
                    var q = path[i]; float age = now - q.t, f = Fade(age, k.ribbonLife); if (f <= 0f && c == 0) continue;
                    float dh = sHead - q.s, h = Mathf.SmoothStep(0f, 1f, dh / Mathf.Max(0.01f, k.headGap));
                    float r = k.ribbonRadius * Mathf.Lerp(k.ribbonRadiusHead, 1f, Mathf.SmoothStep(0f, 1f, dh / Mathf.Max(0.01f, k.ribbonSpread))) * (1f + k.ribbonAgeGrow * age);
                    float ph = ph0 + TwoPi * k.ribbonTwist * q.s + k.ribbonSpin * now, co = Mathf.Cos(ph), si = Mathf.Sin(ph);
                    tp[c] = q.p + side * (co * r) + Vector3.up * (si * r * k.ribbonUpScale); ts[c] = q.s + j * 3.7f;
                    float face = Mathf.Abs(si);
                    tw[c] = k.ribbonWidth * (1f - k.ribbonFlat + k.ribbonFlat * face);
                    tg[c] = k.ribbonIntensity * hdr * f * q.e * h * (1f + 0.5f * (1f - face)); ta[c] = k.occludeRibbon * Mathf.Pow(f, op) * q.e * h;
                    tc[c] = Color.Lerp(k.ribbonColor, k.ribbonColorOld, Mathf.Clamp01(age / Mathf.Max(0.01f, k.ribbonLife))); c++;
                }
                Strip(c, k.ribbonEdge, camPos);
            }
            // speed lines: thin, short-lived, at fixed places round the core
            int nLines = Mathf.Max(0, tier.lite ? Mathf.Min(k.liteLines, k.lineCount) : k.lineCount);
            for (int l = 0; l < nLines; l++)
            {
                c = 0; float a = TwoPi * (l + 0.5f) / nLines + 0.37f, rl = k.lineRadius * (0.55f + 0.45f * Hash01(l * 13 + 5));
                var off = side * (Mathf.Cos(a) * rl) + Vector3.up * (Mathf.Sin(a) * rl * 0.8f);
                for (int i = 0; i < n; i++)
                {
                    var q = path[i]; float age = now - q.t, f = Fade(age, k.lineLife); if (f <= 0f && c == 0) continue;
                    float h = Mathf.SmoothStep(0f, 1f, (sHead - q.s - 0.3f) / Mathf.Max(0.01f, k.headGap));
                    tp[c] = q.p + off; ts[c] = q.s + l * 1.9f; tw[c] = k.lineWidth; tg[c] = k.lineIntensity * hdr * f * q.e * h; ta[c] = k.occludeLines * Mathf.Pow(f, op) * q.e * h;
                    tc[c] = k.lineColor; c++;
                }
                Strip(c, 0f, camPos);
            }
            if (vs.Count < 4) return;
            MaxTrailVerts = Mathf.Max(MaxTrailVerts, vs.Count);
            trailMesh.Clear(); trailMesh.SetVertices(vs); trailMesh.SetColors(cs); trailMesh.SetUVs(0, u0); trailMesh.SetUVs(1, u1); trailMesh.SetTriangles(tris, 0, true);
            Graphics.DrawMesh(trailMesh, Matrix4x4.identity, trailMat, layer, null, 0, null, false, false);
        }

        static float Hash01(int i) { uint h = (uint)i * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; return (h & 0xFFFF) / 65535f; }

        /// <summary>A camera-facing strip through tp[0..n): colour tc, width tw (never under the least width on screen: then wider and dimmer by as much),
        /// HDR gain tg (uv1.x), edge mix (uv1.y), metres along (uv0.x), occlusion x fade ta (vertex alpha; the shader times it by _Occlude and the profile).</summary>
        void Strip(int n, float edge, Vector3 camPos)
        {
            if (n < 2) return;
            int b = vs.Count; var last = Vector3.right;
            for (int i = 0; i < n; i++)
            {
                var p = tp[i]; var t = tp[Mathf.Min(n - 1, i + 1)] - tp[Mathf.Max(0, i - 1)];
                var cp = camPos - p; var sd = Vector3.Cross(t, cp); float m = sd.magnitude;
                sd = m > 1e-5f ? sd / m : last; last = sd;
                float w0 = Mathf.Max(1e-4f, tw[i]), w = Mathf.Max(w0, stripOrtho + stripMinK * cp.magnitude), keep = w0 / w;
                vs.Add(p - sd * (w * 0.5f)); vs.Add(p + sd * (w * 0.5f));
                var col = tc[i]; col.a = Mathf.Clamp01(ta[i] * keep);
                cs.Add(col); cs.Add(col);
                u0.Add(new Vector2(ts[i], 0f)); u0.Add(new Vector2(ts[i], 1f));
                u1.Add(new Vector2(tg[i] * keep, edge)); u1.Add(new Vector2(tg[i] * keep, edge));
                if (i > 0) { int a = b + (i - 1) * 2; tris.Add(a); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3); }
            }
        }

        void DrawGhosts(OzEvilZodDashKnobs k, float now, float dn, float occ, int layer)
        {
            var mat = afterMat ? afterMat : ghostMat;                                           // the look's own (with the day occlusion), else round 2's additive ghost
            if (!mat || ghosts.Length == 0) return;
            float life = Mathf.Max(0.02f, k.afterimageLife), fp = Mathf.Max(0.1f, k.afterimageFadePow), op = Mathf.Max(0.05f, k.occludeFadePow);
            mpb.SetFloat(IdBody, Mathf.Max(0f, k.afterimageBody));
            for (int i = 0; i < ghosts.Length; i++)
            {
                ref var g = ref ghosts[i]; if (!g.on) continue;
                float u = (now - g.born) / life;
                if (u >= 1f || !g.mesh) { g.on = false; continue; }
                var col = Color.Lerp(k.afterimageNew, k.afterimageOld, u) * (k.hdrMul * dn);
                mpb.SetVector(IdColor, new Vector4(col.r, col.g, col.b, 1f));                      // linear: SetColor would gamma-convert the HDR value
                float left = Mathf.Max(1e-4f, 1f - u), glowFade = Mathf.Pow(left, fp);
                mpb.SetFloat(IdAlpha, k.afterimageAlpha * glowFade);
                // what it covers fades slower than its glow: the shader covers _Occlude x the glow, so the ratio of the two fades goes in here
                mpb.SetFloat(IdOcclude, occ * k.occludeAfterimage * Mathf.Min(50f, Mathf.Pow(left, fp * op) / glowFade));
                mpb.SetFloat(IdPower, k.afterimagePower);
                Graphics.DrawMesh(g.mesh, g.at, mat, layer, null, 0, mpb, false, false);
                if (gunMesh) for (int s = 0; s < gunMesh.subMeshCount; s++) Graphics.DrawMesh(gunMesh, g.gunAt, mat, layer, null, s, mpb, false, false);
                if (slideMesh) Graphics.DrawMesh(slideMesh, g.slideAt, mat, layer, null, 0, mpb, false, false);
            }
        }

        void DrawGround(OzEvilZodDashKnobs k, float now, float hdr, float occ, int layer)
        {
            float life = Mathf.Max(0.1f, k.groundFadeTime);
            int cut = 0; while (cut < gpts.Count && now - gpts[cut].t > life) cut++;
            if (cut > 0) gpts.RemoveRange(0, cut);
            if (gpts.Count < 2) return;
            vs.Clear(); ns.Clear(); cs.Clear(); u0.Clear(); u1.Clear(); tris.Clear();
            int n = gpts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = gpts[i]; float age = now - p.t;
                bool first = i == 0 || gpts[i - 1].strip != p.strip, lastOne = i == n - 1 || gpts[i + 1].strip != p.strip;
                float ends = first ? (p.flare > 1f ? 0.6f : 0f) : lastOne ? (wasMoving && p.strip == strip ? 0.35f : 0f) : 1f;   // soft ends; the push-off mark shows
                float glow = k.groundGlow * hdr * (1f - Mathf.SmoothStep(0f, 1f, age / Mathf.Max(0.05f, k.groundGlowTime))) * ends;
                float scorch = k.groundScorch * (1f - Mathf.SmoothStep(0f, 1f, age / life)) * ends * Mathf.Lerp(1f, 1.25f, p.flare - 1f);
                var col = new Color(1f, 1f, 1f, Mathf.Clamp01(scorch));
                vs.Add(p.l); vs.Add(p.r); ns.Add(Vector3.up); ns.Add(Vector3.up); cs.Add(col); cs.Add(col);
                u0.Add(new Vector2(p.s, 0f)); u0.Add(new Vector2(p.s, 1f));
                u1.Add(new Vector2(glow, 0f)); u1.Add(new Vector2(glow, 0f));
                if (!first) { int a = (i - 1) * 2; tris.Add(a); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3); }
            }
            if (tris.Count == 0) return;
            groundMesh.Clear(); groundMesh.SetVertices(vs); groundMesh.SetNormals(ns); groundMesh.SetColors(cs); groundMesh.SetUVs(0, u0); groundMesh.SetUVs(1, u1); groundMesh.SetTriangles(tris, 0, true);
            groundMat.SetFloat(IdGain, 1f);
            groundMat.SetVector(IdColor, Lin(k.groundColor));
            groundMat.SetVector(IdScorchColor, Lin(k.groundScorchColor));
            groundMat.SetFloat(IdScorchDark, 1f);
            groundMat.SetFloat(IdOcclude, occ * k.occludeGround);                                   // the hot line covers the sand by day: blue, not white
            groundMat.SetFloat(IdGlowSharp, Mathf.Max(1f, k.groundGlowSharp)); groundMat.SetFloat(IdGlowBreak, Mathf.Clamp01(k.groundGlowBreak));
            Graphics.DrawMesh(groundMesh, Matrix4x4.identity, groundMat, layer, null, 0, null, false, false);
        }

        static Vector4 Lin(Color c) => new Vector4(c.r, c.g, c.b, 1f);

        void DrawShield(OzEvilZodDashKnobs k, float now, TierInfo tier, float hdr, float occ, int layer, Vector3 camPos)
        {
            bool breaking = breakAge >= 0f;
            if (breaking) { breakAge += Time.deltaTime; if (breakAge >= Mathf.Max(0.02f, k.shieldBreakTime)) { breakAge = -1f; breaking = false; } }
            float life = breaking ? breakLife : shield;
            if (life <= 0.003f || !k.shieldOn) return;
            if (!shieldMesh || meshAng != k.shieldAngle || meshElMin != k.shieldElevMin || meshElMax != k.shieldElevMax) BuildShield(k);
            float bu = breaking ? Mathf.Clamp01(breakAge / Mathf.Max(0.02f, k.shieldBreakTime)) : 0f;
            var f = breaking ? breakFwd : fwd;
            var at = breaking ? breakAt : transform.position + Vector3.up * k.shieldHeight + f * k.shieldForward;
            float R = Mathf.Max(0.3f, k.shieldRadius) * (1f + k.shieldBreakGrow * bu);
            var rot = Quaternion.LookRotation(f, Vector3.up); var scale = new Vector3(R, R * k.shieldSquash, R);
            float ang = k.shieldAngle * Mathf.Deg2Rad, el = (k.shieldElevMax - k.shieldElevMin) * Mathf.Deg2Rad;
            shieldMat.SetFloat(IdGain, k.shieldIntensity * hdr);
            shieldMat.SetVector(IdColor, Lin(k.shieldColor));
            shieldMat.SetFloat(IdLife, life); shieldMat.SetFloat(IdBreak, bu);
            shieldMat.SetFloat(IdRim, k.shieldRim); shieldMat.SetFloat(IdRimPow, k.shieldRimPow); shieldMat.SetFloat(IdFill, k.shieldFill);
            shieldMat.SetFloat(IdHex, k.shieldHex); shieldMat.SetFloat(IdHexSize, Mathf.Max(0.05f, k.shieldHexSize));
            // round 2: a thin rim never under shieldRimMinPx on screen, the face and hex gathered at the rim (the middle clear), less of both at night,
            // the hex gone where its cells get too small to read, bright ends of the arc
            shieldMat.SetFloat(IdRimMinPx, Mathf.Max(0f, k.shieldRimMinPx)); shieldMat.SetFloat(IdFillToRim, Mathf.Clamp01(k.shieldFillToRim));
            shieldMat.SetFloat(IdFillMul, Mathf.Lerp(1f, Mathf.Max(0f, k.shieldNightFill), NightGrade.NightK)); shieldMat.SetFloat(IdHexMinPx, Mathf.Max(0f, k.shieldHexMinPx));
            shieldMat.SetFloat(IdBracket, Mathf.Max(0f, k.shieldBracket)); shieldMat.SetFloat(IdBracketW, Mathf.Clamp(k.shieldBracketWidth, 0.01f, 0.5f));
            shieldMat.SetFloat(IdArcLen, R * ang); shieldMat.SetFloat(IdHeight, R * el * k.shieldSquash);
            // the break's ripple: at least shieldBreakMin of a full shield's, however far the shield faded in the brake (the face stays as faded)
            float ring = k.shieldRingGain * (tier.calm ? k.calmRing : 1f) * (breaking ? Mathf.Max(1f, k.shieldBreakMin / Mathf.Max(0.05f, life)) : 1f);
            shieldMat.SetFloat(IdEdgeSoft, k.shieldEdgeSoft); shieldMat.SetFloat(IdRingGain, ring); shieldMat.SetFloat(IdOcclude, occ * k.occludeShield);
            shieldMat.SetFloat(IdFlicker, tier.calm ? k.calmFlicker : k.flicker); shieldMat.SetFloat(IdSeed, (Dashes % 97) * 1.618f);
            Graphics.DrawMesh(shieldMesh, Matrix4x4.TRS(at, rot, scale), shieldMat, layer, null, 0, null, false, false);
            var bm = k.bendSmooth && bendOwn ? bendOwn : bendHeat;
            if (bm && k.bendOn && (!tier.lite || k.liteBend))
            {   // the light-bending band on a shell just outside the shield, pushing the picture outwards: the look's own smooth bend, or the game's heat
                // distortion (wave mode); the push is a share of the screen, so it shrinks with the camera's distance past bendRefDistance (same size
                // against the shield at every zoom)
                var cam = Camera.main; float dist = cam ? Vector3.Distance(cam.transform.position, at) : k.bendRefDistance;
                float zoom = Mathf.Clamp01(Mathf.Max(1f, k.bendRefDistance) / Mathf.Max(1f, dist));
                float push = k.bendStrength * zoom * life * (1f - bu) * (tier.calm ? k.calmBend : 1f); BendPushMax = Mathf.Max(BendPushMax, push);
                bm.SetFloat(IdStrength, push);
                bm.SetFloat(IdBright, k.bendBright); bm.SetFloat(IdAlpha, 1f);
                if (bm == bendOwn) bm.SetFloat(IdBendCells, Mathf.Max(1f, k.bendCells));
                Graphics.DrawMesh(bendMesh, Matrix4x4.TRS(at, rot, scale * Mathf.Max(1f, k.bendRadiusMul)), bm, layer, null, 0, null, false, false);
            }
            if (trailMat && k.bendOn) DrawFlow(k, tier, hdr, layer, camPos, at, rot, R, life * (1f - bu), now);
        }

        /// <summary>The bent flow streaks (his picture 2): thin bright lines just outside the arc, from near its front round its ends, then sweeping back
        /// past his sides; their streaks run from the front backwards (the trail's own streak pattern along the line). Camera-facing strips of the trail's
        /// material; they go with the shield (and fade in its break).</summary>
        void DrawFlow(OzEvilZodDashKnobs k, TierInfo tier, float hdr, int layer, Vector3 camPos, Vector3 at, Quaternion rot, float R, float life, float now)
        {
            int per = Mathf.Clamp(tier.lite ? Mathf.Min(k.liteBendStreaks, k.bendStreaks) : k.bendStreaks, 0, 6);
            if (per <= 0 || life <= 0.003f || k.bendStreakIntensity <= 0f) return;
            vs.Clear(); cs.Clear(); u0.Clear(); u1.Clear(); tris.Clear();
            const int ArcPts = 12, TailPts = 7;
            float half = Mathf.Clamp(k.shieldAngle, 10f, 300f) * 0.5f * Mathf.Deg2Rad, a0 = half * Mathf.Clamp01(k.bendStreakStart);
            for (int sgi = 0; sgi < 2; sgi++)
            {
                float sg = sgi == 0 ? 1f : -1f;
                for (int i = 0; i < per; i++)
                {
                    float r = R * (Mathf.Max(1f, k.bendRadiusMul) + 0.04f + k.bendStreakGap * i), el = (i - (per - 1) * 0.5f) * 0.12f;
                    int c = 0; float s = 0f; Vector3 last = Vector3.zero;
                    for (int j = 0; j <= ArcPts; j++)
                    {   // round the arc, from near its front to its end
                        float a = sg * Mathf.Lerp(a0, half, j / (float)ArcPts);
                        var p = at + rot * new Vector3(Mathf.Sin(a) * Mathf.Cos(el) * r, Mathf.Sin(el) * r * k.shieldSquash, Mathf.Cos(a) * Mathf.Cos(el) * r);
                        if (c > 0) s += Vector3.Distance(p, last);
                        tp[c] = p; ts[c] = s; last = p; c++;
                    }
                    var dir0 = rot * new Vector3(sg * Mathf.Cos(half), 0f, -Mathf.Sin(half)); var back = rot * new Vector3(sg * 0.12f, 0f, -1f).normalized;
                    float tail = Mathf.Max(0f, k.bendStreakTail) * (1f + 0.25f * i), stp = tail / TailPts;
                    for (int j = 1; j <= TailPts && stp > 1e-3f; j++)
                    {   // then back past his side, bending from the arc's tangent toward straight back
                        var d = Vector3.Slerp(dir0, back, j / (float)TailPts); var p = last + d * stp; s += stp;
                        tp[c] = p; ts[c] = s; last = p; c++;
                    }
                    float total = Mathf.Max(0.01f, s), seed = sgi * 5.3f + i * 2.1f;
                    for (int j = 0; j < c; j++)
                    {
                        float u = ts[j] / total, prof = Mathf.SmoothStep(0f, 1f, u / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
                        tw[j] = k.bendStreakWidth; tg[j] = k.bendStreakIntensity * hdr * life * prof; ta[j] = k.occludeLines * life * prof; tc[j] = k.bendStreakColor; ts[j] += seed;
                    }
                    Strip(c, 0f, camPos);
                }
            }
            if (vs.Count < 4) return;
            FlowVertsMax = Mathf.Max(FlowVertsMax, vs.Count);
            flowMesh.Clear(); flowMesh.SetVertices(vs); flowMesh.SetColors(cs); flowMesh.SetUVs(0, u0); flowMesh.SetUVs(1, u1); flowMesh.SetTriangles(tris, 0, true);
            Graphics.DrawMesh(flowMesh, Matrix4x4.identity, trailMat, layer, null, 0, null, false, false);
        }

        /// <summary>The shell (unit sphere, +z ahead): uv.x across the arc, uv.y bottom to top, normals outward; the bend band the same shell with its height
        /// tapered to nothing at the arc's ends, so the distortion has no hard edge (its profile is sin(pi uv.y)).</summary>
        void BuildShield(OzEvilZodDashKnobs k)
        {
            meshAng = k.shieldAngle; meshElMin = k.shieldElevMin; meshElMax = k.shieldElevMax;
            if (!shieldMesh) shieldMesh = new Mesh { name = "EvilZod dash shield", hideFlags = HideFlags.DontSave };
            if (!bendMesh) bendMesh = new Mesh { name = "EvilZod dash bend", hideFlags = HideFlags.DontSave };
            float ang = Mathf.Clamp(k.shieldAngle, 10f, 300f) * Mathf.Deg2Rad, e0 = k.shieldElevMin * Mathf.Deg2Rad, e1 = k.shieldElevMax * Mathf.Deg2Rad;
            var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var bv = new List<Vector3>(); var t = new List<int>();
            for (int a = 0; a <= AzSeg; a++)
            {
                float u = a / (float)AzSeg, az = (u - 0.5f) * ang, taper = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
                for (int b = 0; b <= ElSeg; b++)
                {
                    float w = b / (float)ElSeg, el = Mathf.Lerp(e0, e1, w);
                    var d = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
                    v.Add(d); nn.Add(d); uv.Add(new Vector2(u, w));
                    float elB = (e0 + e1) * 0.5f + (w - 0.5f) * (e1 - e0) * taper;
                    bv.Add(new Vector3(Mathf.Sin(az) * Mathf.Cos(elB), Mathf.Sin(elB), Mathf.Cos(az) * Mathf.Cos(elB)));
                }
            }
            for (int a = 0; a < AzSeg; a++)
                for (int b = 0; b < ElSeg; b++)
                {
                    int i = a * (ElSeg + 1) + b, j = i + ElSeg + 1;
                    t.Add(i); t.Add(i + 1); t.Add(j); t.Add(j); t.Add(i + 1); t.Add(j + 1);
                }
            shieldMesh.Clear(); shieldMesh.SetVertices(v); shieldMesh.SetNormals(nn); shieldMesh.SetUVs(0, uv); shieldMesh.SetTriangles(t, 0); shieldMesh.RecalculateBounds();
            var bn = new List<Vector3>(bv.Count); foreach (var p in bv) bn.Add(p);
            bendMesh.Clear(); bendMesh.SetVertices(bv); bendMesh.SetNormals(bn); bendMesh.SetUVs(0, uv); bendMesh.SetTriangles(t, 0); bendMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            foreach (var g in ghosts) if (g.mesh) Destroy(g.mesh);
            foreach (var m in new Object[] { trailMesh, groundMesh, shieldMesh, bendMesh, flowMesh, trailMat, groundMat, shieldMat, afterMat, bendOwn, bendHeat }) if (m) Destroy(m);
        }
    }
}
