// OpenZ (GPL-3.0) - version 1.5, level 18 briefing stage: the look both briefings share.
// THE SHAKE: every bridge's blast kicks the camera INSIDE the flight's picture: a hard kick on the blast's first frame, then a strong
// shake that dies away (ShakeSeconds), renewed by each of the three bridges. It is a turn of the lens (pitch, yaw, a little roll), so
// the picture moves by the same share of its height at any distance and any field of view: ShakeHeight of the frame's height in the
// first frames. The red briefing opens a moment after the blasts: its picture shakes from its first frame and is still by
// RedShakeSeconds. A pure function of the stage clock (a held frame shows the same offset); the flight writes the camera every frame, the
// offset of the last frame is taken off first when nobody wrote the camera since (a paused flight), so it never adds up. The game's
// SCREEN SHAKE switch scales it; a quarter with reduced motion.
// FIRE THAT GLOWS, SMOKE WITH SOFT EDGES: while a briefing is staged the blasts' own channels (the game's explosion kit) are lifted for
// this stage only: the fire's gain goes up until the balls of fire are the hottest thing in the picture and bloom at dawn, the smoke's
// silhouettes are feathered and fade softly into water, deck and ground. Every number goes back to the channel's own at the release:
// the Unit editor's explosions, which share these channels, are untouched.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        // ------------------------------------------------------------------------------------------------ the shake
        /// <summary>Blue, per bridge blast: the first frames' share of the picture's height, how fast it dies (s to 37 %), how long it lasts (s),
        /// the roll (deg at full strength), the two shake rates (Hz).</summary>
        public static float ShakeHeight = 0.030f, ShakeTau = 0.34f, ShakeSeconds = 1.4f, ShakeRoll = 1.3f, ShakeHzA = 9.7f, ShakeHzB = 16.3f;
        /// <summary>Red, the opening: the same from the flight's RedShakeAt second: share of the height, decay, length (still by then).</summary>
        public static float RedShakeHeight = 0.036f, RedShakeTau = 0.50f, RedShakeSeconds = 1.75f, RedShakeAt = 0f;
        /// <summary>The shake of this frame as shares of a picture's height (x right, y down) and its roll (deg): the corner window's camera jolts with it.</summary>
        public static Vector3 KickShare;
        /// <summary>Play checks: frames the shake moved the picture, its size now (degrees of lens turn).</summary>
        public static int KickFrames; public static float KickNow;

        Vector3 kickPosBase, kickPosLeft; Quaternion kickRotBase = Quaternion.identity, kickRotLeft = Quaternion.identity; bool kickOn; Transform kickCam;

        static float ShakeEnv(float u, float tau, float len) => u < 0f || u > len ? 0f : Mathf.Exp(-u / Mathf.Max(0.05f, tau)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, u / Mathf.Max(0.1f, len))));

        static void ShakeWave(float u, int i, float a, ref float x, ref float y, ref float r)
        {
            float ph = i * 2.3f, w = 6.2832f * u;
            y += a * (-0.8f * Mathf.Cos(w * ShakeHzA) - 0.3f * Mathf.Cos(w * ShakeHzB + ph));          // the first frame: the lens thrown down hard
            x += a * (0.7f * Mathf.Sin(w * (ShakeHzA - 1.4f) + 1.1f + ph) + 0.35f * Mathf.Sin(w * (ShakeHzB - 1.4f) + ph * 1.7f));
            r += a * Mathf.Sin(w * 6.1f + 0.6f + ph);
        }

        /// <summary>After the flight has put this frame's camera (see the file's head).</summary>
        void FlightKick(float t)
        {
            KickOff();
            var cam = Camera.main; if (!cam) return;
            float k = OpenZFxShake.I != null ? OpenZFxShake.I.scale : 1f; if (OzMotion.Reduced) k *= 0.25f;
            if (k <= 0f) return;
            float x = 0f, y = 0f, r = 0f, share = ShakeHeight; bool any = false;
            if (Parades)
            {
                float tauS = blue ? ShakeTau : RedShakeTau, lenS = blue ? ShakeSeconds : RedShakeSeconds; if (!blue) share = RedShakeHeight;
                for (int i = 0; i < spans.Count; i++) { float u = t - spans[i].blastAt, a = ShakeEnv(u, tauS, lenS); if (a > 0f) { ShakeWave(u, i, a, ref x, ref y, ref r); any = true; } }
            }
            else if (!blue)
            {
                float u = t - RedShakeAt, a = ShakeEnv(u, RedShakeTau, RedShakeSeconds); share = RedShakeHeight;
                if (a > 0f) { ShakeWave(u, 0, a, ref x, ref y, ref r); any = true; }
            }
            if (!any || share <= 0f) return;
            x = Mathf.Clamp(x, -1.2f, 1.2f); y = Mathf.Clamp(y, -1.2f, 1.2f); r = Mathf.Clamp(r, -1.2f, 1.2f);
            float deg = share * cam.fieldOfView * k;
            KickShare = new Vector3(x * share * k * 0.75f, y * share * k, r * ShakeRoll * k);
            var ct = cam.transform;
            kickPosBase = ct.position; kickRotBase = ct.rotation;
            ct.rotation = kickRotBase * Quaternion.Euler(y * deg, x * deg * 0.75f, r * ShakeRoll * k);
            kickPosLeft = ct.position; kickRotLeft = ct.rotation; kickCam = ct; kickOn = true;
            KickNow = Mathf.Sqrt(x * x + y * y) * deg; KickFrames++;
        }

        /// <summary>Our own offset comes off the camera again (only when the camera still stands where we left it).</summary>
        void KickOff()
        {
            if (kickOn && kickCam && (kickCam.position - kickPosLeft).sqrMagnitude < 1e-8f && Quaternion.Angle(kickCam.rotation, kickRotLeft) < 0.001f) kickCam.SetPositionAndRotation(kickPosBase, kickRotBase);
            kickOn = false; KickNow = 0f; KickShare = Vector3.zero;
        }

        // ------------------------------------------------------------------------------------------------ the blasts' look on this stage
        /// <summary>The stage's own gain on the blasts' flames (fire, fuel) and on the fire inside the smoke heads, the smoke's silhouette
        /// softness (the channels' own is 0.14 to 0.3) and its soft fade into what it meets (m). StageLook false = the channels as they are.</summary>
        public static float GlowFlame = 1.14f, GlowHeart = 1.33f, SmokeEdge = 0.55f, SmokeSoftFade = 3.2f, FlameSoftFade = 1.4f;   // on top of the channels' own gain: since the explosion set's round 4 most of the glow (x 2.3 / x 2.0 before) is in the channels themselves (x 2.02 / x 1.5); the stage's picture is the same
        public static bool StageLook = true;
        static readonly string[] FlameCh = { "ueb_fire", "ueb_fuel" }, SmokeCh = { "ueb_hot", "ueb_cauli", "ueb_soot", "ueb_pillar", "ueb_dust", "ueb_haze", "ueb_wall" };
        sealed class MatWas { public Material m; public bool flame; public float intensity, edge, soft; }
        readonly List<MatWas> lookWas = new List<MatWas>(); bool lookOn; float lookAt;
        /// <summary>Play check: channels lifted now.</summary>
        public static int LookChannels;

        /// <summary>Each frame of a staged flight: the channels' numbers for this stage (statics: tuned in Play).</summary>
        void LookTick()
        {
            if (!StageLook) { LookOff(); return; }
            if (!lookOn)
            {
                lookOn = true; lookWas.Clear(); colSys.Clear();
                foreach (var r in FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string n = r.gameObject.name; if (!n.StartsWith("FxCh_ueb_")) continue;
                    string ch = n.Substring(5); var m = r.sharedMaterial; if (!m || !m.HasProperty("_Intensity")) continue;
                    bool flame = System.Array.IndexOf(FlameCh, ch) >= 0, smoke = System.Array.IndexOf(SmokeCh, ch) >= 0;
                    if (!flame && !smoke) continue;
                    if (smoke) { var ps = r.GetComponent<ParticleSystem>(); if (ps) colSys.Add(ps); }
                    lookWas.Add(new MatWas { m = m, flame = flame, intensity = m.GetFloat("_Intensity"), edge = m.HasProperty("_EdgeSoft") ? m.GetFloat("_EdgeSoft") : -1f, soft = m.HasProperty("_SoftFade") ? m.GetFloat("_SoftFade") : -1f });
                }
                LookChannels = lookWas.Count; lookAt = 0f;
                if (lookWas.Count == 0) lookOn = false;                                                 // the channels are not made yet: again next frame
            }
            if (Time.unscaledTime < lookAt) return;
            lookAt = Time.unscaledTime + 0.25f;
            foreach (var w in lookWas)
            {
                if (!w.m) continue;
                w.m.SetFloat("_Intensity", w.intensity * (w.flame ? GlowFlame : GlowHeart));
                if (w.flame) { if (w.soft >= 0f) w.m.SetFloat("_SoftFade", Mathf.Max(w.soft, FlameSoftFade)); }
                else { if (w.edge >= 0f) w.m.SetFloat("_EdgeSoft", Mathf.Max(w.edge, SmokeEdge)); if (w.soft >= 0f) w.m.SetFloat("_SoftFade", Mathf.Max(w.soft, SmokeSoftFade)); }
            }
        }

        // ------------------------------------------------------------------------------------------------ smoke against the far land
        /// <summary>Owner addition 10 point 3: see-through smoke writes no depth, so the fog pass hazed it by what stood BEHIND it: in front of a far
        /// tower the tower's heavy haze, in front of the open sky beside it the sky's: the tower cut its shape out of the smoke. While one of
        /// FarShots flies, the far land (from FarFrom to FarTo metres outside the map) takes the sky's own haze treatment at its height (the fog
        /// pass's own rule, made for the level 16 ending: _AtmoFarRect / _AtmoFarSky): the same behind the smoke on both sides of a tower's edge.
        /// The kept wide view and the flights without smoke keep the pass exactly as it is. FarAsSky false = never.</summary>
        public static bool FarAsSky = true; public static float FarFrom = 120f, FarTo = 280f;
        public static string[] FarShots = { "aftermath_run", "the_gap" };
        static readonly int IdFarRect = Shader.PropertyToID("_AtmoFarRect"), IdFarSky = Shader.PropertyToID("_AtmoFarSky");
        /// <summary>The gap flight's own smoke column (the middle bridge's south stump): inside its outline the haze runs only to the smoke's
        /// front (the fog pass's column rule), so a near block behind it cannot cut its shape out of the smoke either. Radius and height (m),
        /// the outline's shift down wind (m). ColumnShot "" = never.</summary>
        public static string ColumnShot = "the_gap"; public static float ColumnFogRadius = 3.0f, ColumnFogTop = 19f, ColumnFogLean = 0.8f;
        /// <summary>The outline ends where the smoke does: the highest living puff over that stump, read each frame (ColumnFogTop is the most;
        /// taller than the smoke the outline showed as a pale streak over it, lower it let a block cut the smoke's head: takes 11 to 13).</summary>
        public static bool ColumnFollowsSmoke = true; public static float ColumnTopNow;
        readonly List<ParticleSystem> colSys = new List<ParticleSystem>(); static ParticleSystem.Particle[] colBuf;
        static readonly int IdColumn = Shader.PropertyToID("_AtmoColumn"), IdColumnTop = Shader.PropertyToID("_AtmoColumnTop");
        Vector4 farColumn; bool farColumnOn;
        bool farOn, farHooked; Camera farCam; Vector4 farRect, farSky;
        /// <summary>Play checks: frames flown with the far land hazed as the sky, and pictures of the game camera drawn with it.</summary>
        public static int FarFrames, FarDrawn;

        void FarTick()
        {
            bool want = FarAsSky && shotNow != null && (blue ? System.Array.IndexOf(FarShots, shotNow) >= 0 : FarRedAll);
            if (want) FarFrames++;
            if (want == farOn) return;
            farOn = want;
            if (!want) { Shader.SetGlobalVector(IdFarSky, Vector4.zero); return; }
            var rts = FindFirstObjectByType<RtsCamera>(); Vector2 map = rts ? rts.mapSizeMeters : new Vector2(256f, 240f);
            farCam = rts ? rts.GetComponent<Camera>() : Camera.main;
            farRect = new Vector4(0f, -map.y, map.x, 0f); farSky = new Vector4(Mathf.Max(0f, FarFrom), Mathf.Max(FarFrom + 1f, FarTo), 0f, 1f);
            // the rule is the GAME camera's own: every other camera of the frame (the corner window's room, a preview) draws with it off
            if (!farHooked) { farHooked = true; UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += FarBegin; UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += FarEnd; }
        }
        void FarBegin(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            bool mine = farOn && farCam && cam == farCam;
            if (mine) { Shader.SetGlobalVector(IdFarRect, farRect); FarDrawn++; }
            Shader.SetGlobalVector(IdFarSky, mine ? farSky : Vector4.zero);
            bool col = mine && shotNow == ColumnShot && spans.Count > 0;
            if (col)
            {
                var sp = spans[spans.Count / 2]; var foot = StumpPoint(sp, false) + new Vector3(Mathf.Sign(ColumnWind.x) * ColumnFogLean, 0f, 0f);
                farColumn = new Vector4(foot.x, foot.y, foot.z, Mathf.Max(1f, ColumnFogRadius));
                float top = ColumnFogTop;
                if (ColumnFollowsSmoke)
                {
                    float want = Mathf.Clamp(SmokeTopOver(foot, ColumnFogRadius * 1.6f), 1.5f, ColumnFogTop);
                    ColumnTopNow = ColumnTopNow <= 0f ? want : Mathf.MoveTowards(ColumnTopNow, want, 0.5f);      // at most half a metre a picture: never a jump
                    top = ColumnTopNow;
                }
                Shader.SetGlobalVector(IdColumn, farColumn); Shader.SetGlobalFloat(IdColumnTop, top); farColumnOn = true;
            }
            else if (farColumnOn) { Shader.SetGlobalVector(IdColumn, Vector4.zero); Shader.SetGlobalFloat(IdColumnTop, 0f); farColumnOn = false; }
        }
        /// <summary>How high over `foot` the blasts' smoke stands within `r` metres of its upright line (the highest puff that still has a
        /// fifth of its life: the dying ones are nearly clear).</summary>
        float SmokeTopOver(Vector3 foot, float r)
        {
            float top = 0f, r2 = r * r;
            foreach (var ps in colSys)
            {
                if (!ps) continue;
                int max = ps.main.maxParticles; if (colBuf == null || colBuf.Length < max) colBuf = new ParticleSystem.Particle[Mathf.Max(max, 512)];
                int n = ps.GetParticles(colBuf); bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World; var tr = ps.transform;
                for (int i = 0; i < n; i++)
                {
                    if (colBuf[i].remainingLifetime < colBuf[i].startLifetime * 0.2f) continue;
                    Vector3 w = world ? colBuf[i].position : tr.TransformPoint(colBuf[i].position);
                    float dx = w.x - foot.x, dz = w.z - foot.z; if (dx * dx + dz * dz > r2) continue;
                    float h = w.y + colBuf[i].GetCurrentSize(ps) * 0.3f - foot.y; if (h > top) top = h;
                }
            }
            return top;
        }
        void FarEnd(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != farCam) return;
            Shader.SetGlobalVector(IdFarSky, Vector4.zero);
            if (farColumnOn) { Shader.SetGlobalVector(IdColumn, Vector4.zero); Shader.SetGlobalFloat(IdColumnTop, 0f); farColumnOn = false; }
        }
        void FarOff()
        {
            farOn = false; Shader.SetGlobalVector(IdFarSky, Vector4.zero);
            if (farColumnOn) { Shader.SetGlobalVector(IdColumn, Vector4.zero); Shader.SetGlobalFloat(IdColumnTop, 0f); farColumnOn = false; }
            ColumnTopNow = 0f;
            if (farHooked) { farHooked = false; UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= FarBegin; UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= FarEnd; }
        }

        /// <summary>The channels' own numbers again (the release, a skip, the scene going).</summary>
        void LookOff()
        {
            if (!lookOn) return;
            foreach (var w in lookWas)
            {
                if (!w.m) continue;
                w.m.SetFloat("_Intensity", w.intensity); if (w.edge >= 0f) w.m.SetFloat("_EdgeSoft", w.edge); if (w.soft >= 0f) w.m.SetFloat("_SoftFade", w.soft);
            }
            lookWas.Clear(); colSys.Clear(); lookOn = false; LookChannels = 0;
        }
    }
}
