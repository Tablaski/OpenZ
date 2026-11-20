using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// Zone flags only (a fort's flag is never captured: unchanged).
    /// <list type="bullet">
    /// <item>Neutral, by day: the rings they have today (OzNeutralFlagWaves, and FlagBeacons where it runs), unchanged. At night (idea
    /// 06): a pool of light, a soft round spot of cool light (3 m) round the pole that slowly breathes, drawn the way the street lamps'
    /// pools are (OpenZ/FlagNightPool: the ground, kerbs and the robots at the flag are lit; no real light), and the grey cloth lit by it.</item>
    /// <item>Owned (red or blue), at night (idea 05): the brass ball on the pole's top is a lamp that blinks in the team's colour like a
    /// radio mast's warning light (a flash every 1.6 s, each flag in its own phase, a soft halo round it) and the cloth glows softly from
    /// inside (OpenZ/FlagCloth, OpenZ/FlagMarks). By day: rings only on the human player's ENEMY flags, the capture beacon's look (a
    /// breathing ring and a wave every 3.2 s) in the owner's colour, subtle (OpenZ/FlagMarks); the player's own flags get nothing.
    /// Bot vs bot (both sides CPU, the demo: no human side): every owned flag gets its team's day ring, red and blue alike.</item>
    /// <item>Day and night follow the clock: the flags' night (NightOf: up with the street lamps at dusk, down at dawn, smooth; NIGHT
    /// &amp; RAIN off keeps the day clock = always the day look) goes to the shaders as one global (_OZFlagLook); every night look is
    /// x night and every day ring x (1 - night): no pop at dusk or dawn. The neutral rings fade the same way (FlagBeacons,
    /// OzNeutralFlagWaves read <see cref="NightLook"/>), and the beacon leaves the enemy's flags to their team rings.</item>
    /// <item>Captures: the looks follow the cloth: they go out while the old cloth drops (0.3 s), the new owner's come in as the new
    /// cloth rises (0.8 s); a colour never changes while it shows.</item>
    /// </list>
    /// Cost: one mesh for every flag's rings and halos (1 draw, off when nothing shows), one for the pools (2 draws, night only); the
    /// lamp and the glow inside the flags' own material (each zone flag has its own small copy of the pole mesh: the lamp's colour and
    /// phase live in it, the SRP Batcher keeps batching). No real lights, no per-frame allocations; meshes are written only while a
    /// look fades and when a ring is draped again (one flag every 1.5 s: craters).
    /// </summary>
    public partial class FlagView
    {
        [Header("Night and day looks - owner item 5b")]
        [Tooltip("Neutral flag at night: radius of its pool of light (m)")] public float poolRadius = 3f;
        [Tooltip("Pool of light: the ground x (1 + gain) at its middle")] public float poolGain = 0.55f;
        [Tooltip("Red / blue flag at night: its glow on the ground, in the team's light colour (x the pool's strength)")] public float teamGlow = 0.8f;
        [Tooltip("Pool of light colour (sRGB): cool white")] public Color poolColor = new Color(0.80f, 0.88f, 1f, 1f);
        [Tooltip("Owned flag at night: HDR gain of the lamp on the pole's top")] public float lampGain = 7f;
        [Tooltip("The lamp's halo: gain and half size (m)")] public float haloGain = 2f, haloSize = 0.5f;
        [Tooltip("Owned flag at night: the cloth's glow from inside (x its colour)")] public float clothGlow = 2f;
        [Tooltip("Neutral flag at night: the cloth lit by its pool (x its colour)")] public float clothLit = 1.1f;
        [Tooltip("Enemy flag by day: radius of its breathing ring (m; the capture beacon's 1.9)")] public float enemyRingRadius = 1.9f;
        [Range(0, 1), Tooltip("Enemy ring opacity: subtle (the capture beacon's 0.4)")] public float enemyRingAlpha = 0.35f;
        [Tooltip("Seconds a look takes to come in as the new cloth rises / to go out as the old one drops")] public float lookFadeIn = 0.8f, lookFadeOut = 0.3f;

        /// <summary>The v1.2 looks on. Off = exactly the 1.1 flags (the A/B switch of the render measurement).</summary>
        public static bool Looks = true;
        /// <summary>The flags' night now (0 day .. 1 night) while the looks run, else 0 (FlagBeacons, OzNeutralFlagWaves fade with it).</summary>
        public static float NightLook => I && I.isActiveAndEnabled && Looks ? I.nightLook : 0f;
        /// <summary>The enemy flags carry their team-colour day rings (FlagBeacons then keeps to the neutral flags).</summary>
        public static bool TeamRingsOn => I && I.isActiveAndEnabled && Looks && I.marks;

        /// <summary>The flags' night at clock t, 0 by day .. 1 at night: up with the street lamps at dusk (NightLights switch on from
        /// 0.335; here 0.34 -> 0.47, a little ahead of NightGrade's 0.40 -> 0.52 so the flags light up with the lamps), down at dawn
        /// (0.85 -> 0.91, the lamps go out from 0.895), smooth. The day clock of NIGHT &amp; RAIN off (0.92 -> 0.08 -> 0.22) is always 0.</summary>
        public static float NightOf(float t)
        {
            t = Mathf.Repeat(t, 1f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.47f, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 0.91f, t)));
        }

        sealed class Look
        {
            public Flag f; public Mesh pole; public Vector4[] poleUv, clothUv; public int[] ballVerts;
            public float lamp, pool, ring;                                     // 0..1, eased in time (drawn smoothstepped)
            public Team lampTeam, ringTeam;                                    // the colour each one shows
            public float upLamp = -1f, upRing = -1f, upPool = -1f, upCloth = -1f; public Team upLampTeam, upRingTeam, upPoolTeam;
            public int ringV, haloV, poolV;                                    // first vertex in the shared meshes
            public Vector3 foot, ball; public float footY; public bool draped;
        }

        const int RingSeg = 48; const float RingCell = 0.2f, RingLift = 0.04f, PoolBelow = 1.2f, PoolAbove = 4f;
        static readonly int FlagLookId = Shader.PropertyToID("_OZFlagLook"), RingAlphaId = Shader.PropertyToID("_RingAlpha"),
                            HaloGainId = Shader.PropertyToID("_HaloGain"), PoolGainId = Shader.PropertyToID("_Gain");
        // the cube's 12 triangles, faces outward (OpenZ/FlagNightPool draws the inside: Cull Front); corner c = (x bit 1, y bit 2, z bit 4)
        static readonly int[] BoxTris = { 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6 };

        readonly List<Look> looks = new List<Look>();
        float nightLook, nextRedrape; int redrapeAt = -1, ringRows; float ringR0;
        Mesh marks, pools; MeshRenderer marksR, poolsR; Material marksMat, poolMat;
        Vector3[] mkV, pv; Color32[] mkC, pc; Vector4[] mk1, p0;
        bool marksDirtyV, marksDirtyC, poolsDirtyV, poolsDirtyC;

        /// <summary>Whose flags are "ours" for the day rings: the human player's team. Bot vs bot (both sides CPU) has no human side:
        /// Neutral = every owned flag gets its team's day ring.</summary>
        Team Viewer => !game ? Team.Red : game.blueBot && game.redBot ? Team.Neutral : game.player;

        // ================================================================== build (FlagView.Start, after the flags)
        void LooksStart()
        {
            looks.Clear();
            if (!zonePole) return;
            var uv0 = zonePole.uv; var ball = new List<int>();
            for (int i = 0; i < uv0.Length; i++) if (uv0[i].y > 1.5f) ball.Add(i);
            var ballVerts = ball.ToArray();
            float fr = Mathf.Max(poleRadius * 2.1f, 0.075f), ballY = poleHeight + 0.05f + fr * 0.92f;          // BuildPoleMesh's finial
            var viewer = Viewer;
            foreach (var f in flags)
            {
                if (f.fort) continue;
                var L = new Look { f = f, foot = f.root.position, ballVerts = ballVerts };
                L.ball = f.root.TransformPoint(Vector3.up * ballY); L.footY = L.foot.y;
                var mf = f.poleR ? f.poleR.GetComponent<MeshFilter>() : null;
                if (mf)
                {   // its own copy of the pole: the lamp's colour and phase ride in its uv1 (no per-renderer properties)
                    L.pole = Own(Instantiate(zonePole)); L.pole.name = "FlagPole_" + f.obj.Id; mf.sharedMesh = L.pole;
                    L.poleUv = new Vector4[L.pole.vertexCount];
                }
                L.clothUv = new Vector4[f.mesh.vertexCount];
                for (int k = 0; k < L.clothUv.Length; k++) L.clothUv[k] = new Vector4(f.size.x, f.size.y, f.seed, 0f);   // = BuildClothMesh's uv1
                var t = f.shown; bool owned = t != Team.Neutral;                                   // the level starts in its looks (no fade)
                L.lamp = owned ? 1f : 0f; L.ring = owned && t != viewer ? 1f : 0f; L.pool = 1f;            // every zone flag has its pool at night: cool white when neutral, the team's glow when owned
                L.lampTeam = L.ringTeam = t;
                looks.Add(L);
            }
            if (looks.Count == 0) return;
            BuildMarks(); BuildPools();
            foreach (var L in looks) LookDrape(L);
        }

        MeshRenderer NewLookRenderer(string name, Mesh m, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        /// <summary>Every zone flag's ring grid (a polar grid round the pole, draped on the ground) and its lamp's halo quad, one mesh.</summary>
        void BuildMarks()
        {
            var src = LoadMat(null, "OpenZ/FX/M_FlagMarks", "OpenZ/FlagMarks");
            if (!src) { Debug.LogWarning("[OpenZ] FlagView: shader OpenZ/FlagMarks missing: no enemy day rings, no lamp halos"); return; }
            marksMat = Own(new Material(src) { name = "FlagMarks" });
            marksMat.SetFloat(RingAlphaId, enemyRingAlpha); marksMat.SetFloat(HaloGainId, haloGain);
            ringR0 = Mathf.Max(0.2f, enemyRingRadius * 0.84f - 0.1f);
            float r1 = enemyRingRadius * 1.8f * 1.05f + 0.1f;                                      // the wave's outer edge
            ringRows = Mathf.CeilToInt((r1 - ringR0) / RingCell) + 1;
            int perRing = (RingSeg + 1) * ringRows, per = perRing + 4, n = looks.Count * per;
            mkV = new Vector3[n]; mkC = new Color32[n]; mk1 = new Vector4[n]; var mk0 = new Vector4[n];
            var tri = new List<int>(looks.Count * (RingSeg * (ringRows - 1) * 6 + 6));
            var b = new Bounds(looks[0].foot, Vector3.zero);
            for (int li = 0; li < looks.Count; li++)
            {
                var L = looks[li]; L.ringV = li * per; L.haloV = L.ringV + perRing;
                for (int s = 0; s <= RingSeg; s++)
                {
                    float a = s * Mathf.PI * 2f / RingSeg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    for (int k = 0; k < ringRows; k++)
                    {
                        int q = L.ringV + s * ringRows + k; float r = ringR0 + k * RingCell;
                        mkV[q] = new Vector3(L.foot.x + ca * r, L.foot.y, L.foot.z + sa * r);
                        mk0[q] = new Vector4(L.foot.x, L.foot.z, enemyRingRadius, 0f);
                        mk1[q] = new Vector4(0f, L.f.seed, 0f, 0f);                                  // ground mask 0 until draped
                        if (s < RingSeg && k < ringRows - 1) { tri.Add(q); tri.Add(q + 1); tri.Add(q + ringRows + 1); tri.Add(q); tri.Add(q + ringRows + 1); tri.Add(q + ringRows); }
                    }
                }
                for (int c = 0; c < 4; c++)
                {
                    int q = L.haloV + c;
                    mkV[q] = L.ball; mk0[q] = new Vector4(c == 0 || c == 3 ? -1f : 1f, c < 2 ? -1f : 1f, haloSize, 0f); mk1[q] = new Vector4(1f, L.f.seed, 1f, 0f);
                }
                int h = L.haloV; tri.Add(h); tri.Add(h + 2); tri.Add(h + 1); tri.Add(h); tri.Add(h + 3); tri.Add(h + 2);
                b.Encapsulate(new Bounds(L.foot + Vector3.up * 2f, new Vector3(r1 * 2f + 2f, 14f, r1 * 2f + 2f)));
            }
            marks = Own(new Mesh { name = "FlagMarks" });
            if (n > 65000) marks.indexFormat = IndexFormat.UInt32;
            marks.MarkDynamic();
            marks.SetVertices(mkV); marks.SetColors(mkC); marks.SetUVs(0, mk0); marks.SetUVs(1, mk1); marks.SetTriangles(tri, 0, false);
            marks.bounds = b;                                                                       // the halos grow in the shader
            marksR = NewLookRenderer("FlagMarks", marks, marksMat);
        }

        /// <summary>Every zone flag's pool box (OpenZ/FlagNightPool lights the scene inside it), one mesh.</summary>
        void BuildPools()
        {
            var src = LoadMat(null, "OpenZ/FX/M_FlagNightPool", "OpenZ/FlagNightPool");
            if (!src) { Debug.LogWarning("[OpenZ] FlagView: shader OpenZ/FlagNightPool missing: no pool of light at night"); return; }
            poolMat = Own(new Material(src) { name = "FlagNightPool" });
            poolMat.SetFloat(PoolGainId, poolGain);
            int n = looks.Count * 8;
            pv = new Vector3[n]; pc = new Color32[n]; p0 = new Vector4[n]; var p1 = new Vector4[n]; var tri = new int[looks.Count * 36];
            var b = new Bounds(looks[0].foot, Vector3.zero);
            for (int li = 0; li < looks.Count; li++)
            {
                var L = looks[li]; L.poolV = li * 8;
                for (int c = 0; c < 8; c++) p1[L.poolV + c] = new Vector4(L.f.seed, PoolBelow, PoolAbove, 0f);
                for (int k = 0; k < 36; k++) tri[li * 36 + k] = L.poolV + BoxTris[k];
                PlacePool(L);
                b.Encapsulate(new Bounds(L.foot + Vector3.up * 1.4f, new Vector3(poolRadius * 2f + 2f, PoolBelow + PoolAbove + 4f, poolRadius * 2f + 2f)));
            }
            pools = Own(new Mesh { name = "FlagNightPools" });
            pools.MarkDynamic();
            pools.SetVertices(pv); pools.SetColors(pc); pools.SetUVs(0, p0); pools.SetUVs(1, p1); pools.SetTriangles(tri, 0, false);
            pools.bounds = b;
            poolsR = NewLookRenderer("FlagNightPools", pools, poolMat);
            poolsDirtyV = false;
        }

        void PlacePool(Look L)
        {
            if (pv == null) return;
            float R = poolRadius * 1.02f;
            for (int c = 0; c < 8; c++)
            {
                int q = L.poolV + c;
                pv[q] = new Vector3(L.foot.x + ((c & 1) != 0 ? R : -R), L.footY + ((c & 2) != 0 ? PoolAbove : -PoolBelow), L.foot.z + ((c & 4) != 0 ? R : -R));
                p0[q] = new Vector4(L.foot.x, L.footY, L.foot.z, poolRadius);
            }
            poolsDirtyV = true;
        }

        /// <summary>The flag's ring grid on the rendered ground as it is now (the neutral waves' rule: OzNeutralFlagWaves.Ground, lifted
        /// 4 cm; cells over open water or down a step of more than 1.2 m are masked out) and its pool on the ground under the pole.</summary>
        void LookDrape(Look L)
        {
            L.footY = OzNeutralFlagWaves.Ground(L.foot.x, L.foot.z);
            if (mkV != null)
            {
                for (int s = 0; s <= RingSeg; s++)
                {
                    float a = s * Mathf.PI * 2f / RingSeg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    for (int k = 0; k < ringRows; k++)
                    {
                        int q = L.ringV + s * ringRows + k; float r = ringR0 + k * RingCell, x = L.foot.x + ca * r, z = L.foot.z + sa * r;
                        float y = OzNeutralFlagWaves.Ground(x, z, out bool deck);
                        mkV[q] = new Vector3(x, y + RingLift, z);
                        mk1[q].z = Mathf.Abs(y - L.footY) > OzNeutralFlagWaves.StepMax || (!deck && LookWater(x, z)) ? 0f : 1f;
                    }
                }
                marksDirtyV = true;
            }
            PlacePool(L);
            L.draped = true;
        }

        static bool LookWater(float x, float z)
        {
            var g = ZGame.I; if (g == null || g.World == null) return false;
            var p = ZGame.WorldToPx(new Vector3(x, 0, z));
            var c = g.World.Map.ClassAt(p.x >> 4, p.y >> 4);
            return c == ZClass.Water || c == ZClass.DeepWater;
        }

        // ================================================================== frame (FlagView.LateUpdate, after the flags)
        void LooksFrame(float now, float dt)
        {
            bool on = Looks && looks.Count > 0;
            nightLook = on ? NightOf(NightGrade.TimeOfDay) : 0f;
            Shader.SetGlobalVector(FlagLookId, on ? new Vector4(nightLook, lampGain, clothGlow, clothLit) : Vector4.zero);
            if (!on)
            {
                if (marksR && marksR.enabled) marksR.enabled = false;
                if (poolsR && poolsR.enabled) poolsR.enabled = false;
                return;
            }
            if (now >= nextRedrape)
            {   // the ground changes (craters): one flag after the other is draped again, every 1.5 s, while it shows a ring or a pool
                nextRedrape = now + 1.5f; redrapeAt = (redrapeAt + 1) % looks.Count;
                var R = looks[redrapeAt]; if (R.ring > 0f || R.pool > 0f) LookDrape(R);
            }
            var viewer = Viewer;
            bool anyRing = false, anyHalo = false, anyPool = false;
            for (int i = 0; i < looks.Count; i++)
            {
                var L = looks[i]; var f = L.f;
                bool dropping = f.capT0 >= 0f && !f.swapped;                                  // the old cloth goes down: its looks go with it
                var t = f.shown;
                bool owned = !dropping && t != Team.Neutral, neutral = !dropping && t == Team.Neutral;
                bool ringWasOut = L.ring <= 0f;
                Fade(ref L.lamp, ref L.lampTeam, owned, t, dt);
                Fade(ref L.ring, ref L.ringTeam, owned && t != viewer, t, dt);
                L.pool = Mathf.MoveTowards(L.pool, neutral || owned ? 1f : 0f, dt / Mathf.Max(0.01f, neutral || owned ? lookFadeIn : lookFadeOut));   // out while the old cloth drops: its colour changes unseen
                if (ringWasOut && L.ring > 0f) LookDrape(L);                                      // a ring comes up: on the ground as it is now
                float lamp = Smooth(L.lamp), ring = Smooth(L.ring), pool = Smooth(L.pool), cloth = Mathf.Max(lamp, pool);

                if (Mathf.Abs(cloth - L.upCloth) > 0.004f)
                {   // the cloth's night amount (OpenZ/FlagCloth: red / blue glow from inside, neutral lit by its pool)
                    for (int k = 0; k < L.clothUv.Length; k++) L.clothUv[k].w = cloth;
                    f.mesh.SetUVs(1, L.clothUv); L.upCloth = cloth;
                }
                if (Mathf.Abs(lamp - L.upLamp) > 0.004f || L.lampTeam != L.upLampTeam)
                {   // the lamp: the ball's vertices in the flag's own pole, and its halo
                    var lc = LightColor(L.lampTeam).linear;
                    if (L.pole)
                    {
                        var u = new Vector4(lc.r * lamp, lc.g * lamp, lc.b * lamp, f.seed);
                        for (int k = 0; k < L.ballVerts.Length; k++) L.poleUv[L.ballVerts[k]] = u;
                        L.pole.SetUVs(1, L.poleUv);
                    }
                    if (mkC != null) { var hc = Col32(lc, lamp); for (int k = 0; k < 4; k++) mkC[L.haloV + k] = hc; marksDirtyC = true; }
                    L.upLamp = lamp; L.upLampTeam = L.lampTeam;
                }
                if (mkC != null && (Mathf.Abs(ring - L.upRing) > 0.004f || L.ringTeam != L.upRingTeam))
                {
                    var rc = Col32(RingColor(L.ringTeam).linear, ring);
                    for (int q = L.ringV, end = L.ringV + (RingSeg + 1) * ringRows; q < end; q++) mkC[q] = rc;
                    marksDirtyC = true; L.upRing = ring; L.upRingTeam = L.ringTeam;
                }
                var poolTeam = dropping ? L.upPoolTeam : t;                                       // the glow keeps its colour while it fades out
                if (pc != null && (Mathf.Abs(pool - L.upPool) > 0.004f || poolTeam != L.upPoolTeam))
                {
                    var c = poolTeam == Team.Neutral ? Col32(poolColor.linear, pool) : Col32(LightColor(poolTeam).linear * teamGlow, pool);
                    for (int k = 0; k < 8; k++) pc[L.poolV + k] = c;
                    poolsDirtyC = true; L.upPool = pool; L.upPoolTeam = poolTeam;
                }
                anyRing |= ring > 0f; anyHalo |= lamp > 0f; anyPool |= pool > 0f;
            }
            if (marks)
            {
                if (marksDirtyV) { marks.SetVertices(mkV, 0, mkV.Length, MeshUpdateFlags.DontRecalculateBounds); marks.SetUVs(1, mk1); marksDirtyV = false; }
                if (marksDirtyC) { marks.SetColors(mkC); marksDirtyC = false; }
                bool show = (anyRing && nightLook < 0.999f) || (anyHalo && nightLook > 0.001f);
                if (marksR.enabled != show) marksR.enabled = show;
            }
            if (pools)
            {
                if (poolsDirtyV) { pools.SetVertices(pv, 0, pv.Length, MeshUpdateFlags.DontRecalculateBounds); pools.SetUVs(0, p0); poolsDirtyV = false; }
                if (poolsDirtyC) { pools.SetColors(pc); poolsDirtyC = false; }
                bool show = anyPool && nightLook > 0.001f;
                if (poolsR.enabled != show) poolsR.enabled = show;
            }
        }

        /// <summary>One look's amount toward on / off; a new colour is taken only once the old one is out (never switches while it shows).</summary>
        void Fade(ref float k, ref Team shown, bool want, Team team, float dt)
        {
            if (want && team != shown) { if (k <= 0f) shown = team; else want = false; }
            k = Mathf.MoveTowards(k, want ? 1f : 0f, dt / Mathf.Max(0.01f, want ? lookFadeIn : lookFadeOut));
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>The enemy ring's colour: the team's ground-disc colour (TeamColors.DiscRed / DiscBlue, brighter than the paint so a
        /// </summary>
        static Color RingColor(Team t) => t == Team.Red ? TeamColors.DiscRed : t == Team.Blue ? TeamColors.DiscBlue : t == Team.Green ? OzUi.Mark(t) : TeamColors.DiscEmpty;

        static Color32 Col32(Color c, float a) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.g) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.b) * 255f + 0.5f), (byte)(Mathf.Clamp01(a) * 255f + 0.5f));

        /// <summary>One-line state of the looks (tests, pictures, the build log).</summary>
        public string LooksDiagnostics()
        {
            int pooled = 0, ringed = 0, lit = 0, draped = 0;
            foreach (var L in looks) { if (L.pool > 0.5f) pooled++; if (L.ring > 0.5f) ringed++; if (L.lamp > 0.5f) lit++; if (L.draped) draped++; }
            return $"looks {(Looks ? "on" : "off")}: night {nightLook:0.00} (clock {NightGrade.TimeOfDay:0.000}), viewer {Viewer}, {looks.Count} zone flags: " +
                   $"neutral pools {pooled}, enemy day rings {ringed}, lamps {lit}, draped {draped}, marks {(marksR && marksR.enabled ? "drawn" : "off")}, " +
                   $"pools {(poolsR && poolsR.enabled ? "drawn" : "off")}";
        }
    }
}
