using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Level 7, kit LV2: the lava is alive (LAVA_EFFECTS_PLAN.md section 3, PLAN_FIXES fixes 15, 21, 22, 26, 30).
    ///   heat     OzLavaHeat runs the temperature: one value per field, the map-wide value, the travelling wave, the running surge. This file
    ///            hands them to the shaders (_OZLavaField, _OZLavaWave, _OZLavaSurge, _OZLavaMood) and to everything else that breathes with it:
    ///            the lights, the bed sound, the models' glow (_EmissionMapColor on the three shared materials, about 10 times a second), the air
    ///   bubbles  the seeded stream of OzLavaPops, a second stream on the border lakes, clusters and giants from a second generator, PopAt and
    ///            PopBurst for the other kits. Each is a dome that swells, then at its burst sound, droplets, smoke, ring and scar (OzLavaDomes)
    ///   lights   up to 10, big fields split in 2-3 along their long axis, range cut by the root of the parts, so at most 4 reach any ground point
    ///            at night, counted at boot. Low by day, strong at night. They light ground, rocks, units and smoke, never the lake
    ///   bloom    OpenZBloomDirector.I.nightIntensity set once to a level 7 cap, put back at the end
    /// View only: nothing writes sim state; own System.Random generators; UnityEngine.Random only for FX jitter. All clocks stop with the pause.
    /// </summary>
    public partial class OzLava
    {
        /// <summary>Set against the night capture (gate 3): the halo 2-3 m past the banks, no orange fog.</summary>
        public static float NightBloomCap = 2.6f, NightBloomScatter = 0.82f;
        /// <summary>The light level by day and by night (x each light's base); range x NightRange at night on HIGH.</summary>
        public static float DayLight = 0.3f, NightLight = 22f, NightRange = 2.2f, LightUp = 4.8f;
        public const float GiantSize = 2f, BreatheEvery = 0.1f, BedEvery = 0.25f;
        const int Slots = OzLavaIds.FieldSlots, MaxBursts = 4, MaxLightsReach = 4;
        const int BorderFromId = 6;
        const float LakeArea = 1328f;
        static readonly string[] GlowMaterialNames = { "M_RockZ_Volcanic", "M_L07_Volcano", "M_L07_RockDecor", "M_L07_Bank", "M_L07_VolcanoV2", "M_L07_VolcanoV2_T1", "M_L07_VolcanoV2_T2", "M_L07_VolcanoV2_T3" };   // M_L07_Bank: the terrain lane's bank foot glow, the same breath. M_L07_VolcanoV2 (and its tone copies _T1.._T3): row 31 v2's border volcanoes, the same foot glow
        static readonly int EmMapColor = Shader.PropertyToID("_EmissionMapColor");

        struct Cell { public int id; public float x, z; public bool hot; }
        struct Burst { public Vector3 at; public float radius, rate, left, due; }
        struct Glow { public Material m; public Color start; }

        System.Random showRng; float showDue = 2f; Vector3[] showSpots;
        OzLavaHeat heat; OzLavaPops edgePops; OzLavaPopExtras extras; OzLavaDomes domes; OzLavaAir air;
        readonly List<OzLavaPops.Pop> freshEdge = new List<OzLavaPops.Pop>(), later = new List<OzLavaPops.Pop>();
        readonly List<Cell> cells = new List<Cell>();
        readonly int[] fN = new int[Slots], fHot = new int[Slots]; readonly bool[] fMap = new bool[Slots];
        readonly float[] fX = new float[Slots], fZ = new float[Slots];
        readonly Vector4[] fieldVec = new Vector4[Slots];
        readonly Burst[] bursts = new Burst[MaxBursts];
        readonly List<Glow> glows = new List<Glow>();
        string[] bedKeys = new string[0]; int[] bedField = new int[0];
        int bedCount; float breatheDue, bedDue, pollDue; bool lite, calm, hold, fxDone, bloomDone, firstWrite = true, giantOwed; float bloomStart, bloomScatter;

        /// <summary>The check reads these.</summary>
        public static int ExtraPops, GiantPops, BorderCells, ShowPops;
        public int LightReachMax { get; private set; }
        public int LightCount => lights.Count;
        public int BedCount => bedCount;
        public int GlowMaterials => glows.Count;
        /// <summary>The brightest shared glow tint now (the check reads it; a material left black by its import does not breathe).</summary>
        public float GlowNow { get { float g = 0f; for (int i = 0; i < glows.Count; i++) if (glows[i].m) g = Mathf.Max(g, glows[i].m.GetColor(EmMapColor).maxColorComponent); return g; } }
        public OzLavaDomes Domes => domes;
        public OzLavaAir Air => air;
        public OzLavaHeat HeatMachine => heat;

        /// <summary>Map-wide eased heat, 1.0 = the picture.</summary>
        public float HeatNow => heat != null ? (float)heat.Map : 1f;
        /// <summary>Eased heat of one field.</summary>
        public float HeatOf(int field) => heat != null && field >= 0 && field < Slots ? (float)heat.Heat[field] : 1f;

        // ------------------------------------------------------------------ boot
        partial void Lv2Boot()
        {
            if (OzLavaFx.Off) return;
            ExtraPops = GiantPops = 0;
            Vector3 mid = Vector3.zero; int all = 0;
            for (int i = 0; i < Slots; i++) if (fMap[i]) { mid.x += fX[i]; mid.z += fZ[i]; all += fN[i]; }
            if (all > 0) { mid.x /= all; mid.z /= all; }
            heat = new OzLavaHeat(Slots, seed + 11, mid.x, mid.z) { BorderFrom = BorderFromId };
            SurgeAxes();
            extras = new OzLavaPopExtras(seed);
            var edge = new List<(int, int, int)>(); var spots = new List<Vector3>(); var hot = new List<Vector3>();
            foreach (var c in cells)
            {
                var at = new Vector3(c.x, lavaY, c.z); spots.Add(at); if (c.hot) hot.Add(at);
                if (c.id >= BorderFromId || !map.LavaAt(Mathf.FloorToInt(c.x / 2f), Mathf.FloorToInt(-c.z / 2f)))
                    edge.Add((Mathf.FloorToInt(c.x / 2f), Mathf.FloorToInt(-c.z / 2f), 1));
            }
            BorderCells = edge.Count;
            edgePops = new OzLavaPops(edge, seed + 101);                                                       // the border lakes bubble too, by their own stream: the seeded stream of the map stays as the check knows it
            domes = new OzLavaDomes(this);
            showRng = new System.Random(seed + 77); showSpots = spots.ToArray();
            air = new OzLavaAir(this, spots.ToArray(), hot.ToArray());
            lite = OzLavaFx.Lite; calm = OzLavaFx.Calm; hold = OzLavaFx.HoldHeat;
            FindGlows();
            FxReady(); BloomCap();
            Debug.Log($"[OpenZ] lava alive: {lights.Count} lights (at most {LightReachMax} on a ground point), {bedCount} bed loops, {glows.Count} glow materials, {edge.Count} border cells");
        }

        /// <summary>Everything the fields know, once: the map's lava tiles through LV1's field ids, the border lakes on a 2 m raster.</summary>
        void GatherFields()
        {
            cells.Clear();
            for (int i = 0; i < Slots; i++) { fN[i] = fHot[i] = 0; fX[i] = fZ[i] = 0f; fMap[i] = false; }
            int W = map.width, H = map.height;
            for (int ty = -12; ty < H + 12; ty++)
                for (int tx = -12; tx < W + 12; tx++)
                {
                    int kind = map.LavaKind(tx, ty); float x = tx * 2f + 1f, z = -(ty * 2f + 1f); int id;
                    if (kind > 0) id = FieldOfTile(tx, ty);
                    else { if (!SurfaceAt(new Vector3(x, lavaY, z), out _)) continue; id = FieldAt(x, z); }
                    if (id < 0 || id >= Slots) continue;
                    cells.Add(new Cell { id = id, x = x, z = z, hot = kind == 2 });
                    fN[id]++; fX[id] += x; fZ[id] += z; if (kind == 2) fHot[id]++; if (kind > 0) fMap[id] = true;
                }
        }

        /// <summary>Each field's long axis and its ends along it, for the surge.</summary>
        void SurgeAxes()
        {
            for (int id = 0; id < Slots; id++)
            {
                if (fN[id] < 10) continue;
                Axis(id, out float ax, out float az, out float lo, out float hi);
                heat.SetField(id, ax, az, lo, hi, Mathf.Sqrt(fN[id]) * (fMap[id] ? 1f : 0.4f));
            }
        }

        void Axis(int id, out float ax, out float az, out float lo, out float hi)
        {
            float mx = fX[id] / fN[id], mz = fZ[id] / fN[id], xx = 0f, xz = 0f, zz = 0f;
            foreach (var c in cells) if (c.id == id) { float dx = c.x - mx, dz = c.z - mz; xx += dx * dx; xz += dx * dz; zz += dz * dz; }
            float th = 0.5f * Mathf.Atan2(2f * xz, xx - zz); ax = Mathf.Cos(th); az = Mathf.Sin(th);
            lo = float.MaxValue; hi = float.MinValue;
            foreach (var c in cells) if (c.id == id) { float p = c.x * ax + c.z * az; if (p < lo) lo = p; if (p > hi) hi = p; }
        }

        // ------------------------------------------------------------------ the lights (uses LV1's fields, no second flood fill)
        void BuildLights()
        {
            GatherFields();
            var want = new int[Slots]; int sum = 0;
            for (int id = 0; id < Slots; id++) if (fMap[id]) { want[id] = fN[id] >= 100 ? 3 : fN[id] >= 50 ? 2 : 1; sum += want[id]; }
            while (sum > MaxLights)
            {   // over the cap: the biggest split gives way first; only then does the smallest field go without
                int pick = -1; for (int id = 0; id < Slots; id++) if (want[id] > 1 && (pick < 0 || want[id] > want[pick])) pick = id;
                if (pick < 0) { for (int id = Slots - 1; id >= 0; id--) if (want[id] > 0) { pick = id; break; } }
                want[pick]--; sum--;
            }
            var c = OzLavaRampTable.Eval(0.75f).gamma; c /= Mathf.Max(c.r, Mathf.Max(c.g, c.b)); c.a = 1f;   // a Light's colour is sRGB, the ramp's sum is linear
            for (int id = 0; id < Slots; id++)
            {
                if (want[id] <= 0) continue;
                var ax = Vector2.right; if (want[id] > 1) { Axis(id, out float a, out float b, out _, out _); ax = new Vector2(a, b); }
                var mine = new List<(float p, Cell c)>();
                foreach (var q in cells) if (q.id == id && map.LavaAt(Mathf.FloorToInt(q.x / 2f), Mathf.FloorToInt(-q.z / 2f))) mine.Add((q.x * ax.x + q.z * ax.y, q));
                mine.Sort((x, y) => x.p.CompareTo(y.p));
                float range = Mathf.Clamp(8f + Mathf.Sqrt(fN[id]) * 1.6f, 9f, 26f) / Mathf.Sqrt(want[id]);
                for (int part = 0; part < want[id]; part++)
                {
                    int from = mine.Count * part / want[id], to = mine.Count * (part + 1) / want[id], hot = 0; float x = 0f, z = 0f;
                    for (int i = from; i < to; i++) { x += mine[i].c.x; z += mine[i].c.z; if (mine[i].c.hot) hot++; }
                    x /= Mathf.Max(1, to - from); z /= Mathf.Max(1, to - from);
                    var go = new GameObject("LAV_Glow_" + lights.Count); go.transform.position = new Vector3(x, lavaY + LightUp, z);
                    var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range; l.shadows = LightShadows.None;
                    float baseI = 2.4f + 0.12f * hot; l.intensity = baseI * DayLight;
                    lights.Add((l, baseI, range, lights.Count * 1.7f, id));
                }
            }
            CountReach();
        }

        /// <summary>At most MaxLightsReach lights on any ground point at the night range; if not, every range comes down until it holds.</summary>
        void CountReach()
        {
            for (int round = 0; round < 12; round++)
            {
                int most = 0;
                for (int ty = -12; ty < map.height + 12; ty += 2)
                    for (int tx = -12; tx < map.width + 12; tx += 2)
                    {
                        float x = tx * 2f + 1f, z = -(ty * 2f + 1f); int n = 0;
                        foreach (var v in lights)
                        {
                            float dx = v.l.transform.position.x - x, dz = v.l.transform.position.z - z, r = v.baseR * NightRange;
                            if (dx * dx + dz * dz < r * r) n++;
                        }
                        if (n > most) most = n;
                    }
                LightReachMax = most;
                if (most <= MaxLightsReach) return;
                for (int i = 0; i < lights.Count; i++) { var v = lights[i]; v.baseR *= 0.92f; if (v.l) v.l.range = v.baseR; lights[i] = v; }
            }
        }

        /// <summary>Every frame: heat, night, a soft flicker on the lava clock.</summary>
        void Flicker()
        {
            float night = NightGrade.NightK, level = Mathf.Lerp(DayLight, NightLight, night), range = OzQuality.Level == 0 ? Mathf.Lerp(1f, NightRange, night) : 1f, clock = Clock;
            foreach (var x in lights)
            {
                if (!x.l) continue;
                float h = HeatOf(x.field);
                x.l.intensity = x.baseI * level * h * h * (0.9f + 0.2f * Mathf.PerlinNoise(clock * 0.7f + x.phase, x.phase));
                float r = x.baseR * range; if (Mathf.Abs(r - x.l.range) > 0.02f) x.l.range = r;
            }
        }

        void StartBeds()
        {
            if (!playSounds || !AudioDirector.I) return;
            var keys = new List<string>(); var ids = new List<int>();
            for (int id = 0; id < Slots; id++)
            {
                if (!fMap[id] || fN[id] <= 0) continue;
                string key = BedKey + keys.Count; keys.Add(key); ids.Add(id);
                AudioDirector.I.StartLoop(key, SndBed, new Vector3(fX[id] / fN[id], lavaY + 1.6f, fZ[id] / fN[id]));   // the bubbling bed, one real 3D loop per field (Fire would play the loop clip once)
            }
            bedKeys = keys.ToArray(); bedField = ids.ToArray(); bedCount = keys.Count;
        }

        // ------------------------------------------------------------------ every frame
        partial void Lv2Tick(float dt, float t)
        {
            if (heat == null) return;
            pollDue -= Time.unscaledDeltaTime;
            if (pollDue <= 0f)
            {
                pollDue = 1f; lite = OzLavaFx.Lite; calm = OzLavaFx.Calm; hold = OzLavaFx.HoldHeat;
                var f = OpenZFxPlayer.I; if (f) f.LavaLite = lite;
                if (!fxDone) FxReady(); if (!bloomDone) BloomCap();
            }
            heat.Step(dt, pops.Mood, edgePops.Tiles > 0 ? edgePops.Mood : pops.Mood, hold, calm);
            if (dt > 0f || firstWrite || hold) WriteGlobals();
            ShowBubbles(dt);
            domes.Tick(dt);
            air.Tick(dt, HeatNow, lite);
            breatheDue -= dt; if (breatheDue <= 0f) { breatheDue = BreatheEvery; Breathe(); }
            bedDue -= dt; if (bedDue <= 0f) { bedDue = BedEvery; Beds(); }
        }

        void WriteGlobals()
        {
            firstWrite = false;
            for (int i = 0; i < Slots; i++) fieldVec[i] = new Vector4((float)heat.Heat[i], (float)heat.Cover[i], (float)heat.Surge[i], 0f);
            Shader.SetGlobalVectorArray(OzLavaIds.Field, fieldVec);
            Shader.SetGlobalFloat(OzLavaIds.Mood, (float)heat.Map);
            Shader.SetGlobalVector(OzLavaIds.Wave, new Vector4((float)heat.WaveAmp, (float)heat.WaveKx, (float)heat.WaveKz, (float)heat.WavePhase));
            Shader.SetGlobalVector(OzLavaIds.Surge, heat.SurgeField >= 0 ? new Vector4((float)heat.SurgeDirX, (float)heat.SurgeDirZ, (float)heat.SurgeFront, (float)heat.SurgeHalf) : Vector4.zero);
        }

        /// <summary>Every Gen glow breathes with the lava by one map-wide value (fix 12). Shared materials: put back at the end of the level.</summary>
        void FindGlows()
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (!m || !m.HasProperty(EmMapColor) || System.Array.IndexOf(GlowMaterialNames, m.name) < 0) continue;
                glows.Add(new Glow { m = m, start = m.GetColor(EmMapColor) });
            }
        }

        void Breathe()
        {
            float h = HeatNow;
            for (int i = 0; i < glows.Count; i++) if (glows[i].m) { var c = glows[i].start * h; c.a = glows[i].start.a; glows[i].m.SetColor(EmMapColor, c); }
        }

        void Beds()
        {
            if (!AudioDirector.I) return;
            for (int i = 0; i < bedCount; i++)
            {
                float k = Mathf.InverseLerp((float)OzLavaHeat.HeatMin, (float)OzLavaHeat.HeatMax, HeatOf(bedField[i]));
                AudioDirector.I.SetLoop(bedKeys[i], Mathf.Lerp(0.72f, 1.25f, k), Mathf.Lerp(0.97f, 1.03f, k));
            }
        }

        void FxReady() { var f = OpenZFxPlayer.I; if (!f) return; f.EnsureLavaChannels(); f.LavaLite = lite; fxDone = true; }

        void BloomCap()
        {
            var b = OpenZBloomDirector.I; if (!b) return;
            bloomStart = b.nightIntensity; bloomScatter = b.nightScatter; b.nightIntensity = NightBloomCap; b.nightScatter = NightBloomScatter; bloomDone = true;
        }

        partial void Lv2Down()
        {
            for (int i = 0; i < glows.Count; i++) if (glows[i].m) glows[i].m.SetColor(EmMapColor, glows[i].start);
            glows.Clear();
            var b = OpenZBloomDirector.I; if (b && bloomDone) { b.nightIntensity = bloomStart; b.nightScatter = bloomScatter; }
            domes?.Destroy(); air?.Destroy();
        }

        // ------------------------------------------------------------------ bubbles
        void PopStart(in OzLavaPops.Pop p, bool stream)
        {
            var at = new Vector3(p.x, lavaY, p.z);
            if (domes == null) { if (playSounds) AudioDirector.Fire(p.big ? SndPopBig : SndPopSmall, at); return; }   // OzLavaFx.Off: as the sound alone
            if (!stream && !SurfaceAt(at, out _)) return;                                                     // a follower or a border pop that fell on the bank
            bool giant = false;
            if (stream) { giant = extras.Giant() || giantOwed; extras.Offer(p); }
            Bubble(at, p.size, p.big, giant);
        }

        void PopsMore(float dt)
        {
            if (extras == null || dt <= 0f) return;
            later.Clear(); extras.Step(dt, later);
            foreach (var p in later) { ExtraPops++; PopStart(p, false); }
            freshEdge.Clear(); edgePops.Step(dt, freshEdge);
            foreach (var p in freshEdge) { ExtraPops++; PopStart(p, false); }
            for (int i = 0; i < MaxBursts; i++)
            {
                if (bursts[i].left <= 0f) continue;
                bursts[i].left -= dt; bursts[i].due += bursts[i].rate * dt;
                while (bursts[i].due >= 1f)
                {
                    bursts[i].due -= 1f;
                    double a = extras.Next() * 6.2831853, r = bursts[i].radius * System.Math.Sqrt(extras.Next());
                    var at = bursts[i].at + new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
                    if (!SurfaceAt(at, out _)) continue;
                    ExtraPops++; bool big = extras.Next() < OzLavaPops.BigShare;
                    Bubble(at, big ? OzLavaPops.BigSize : OzLavaPops.SmallSize, big, false);
                }
            }
        }

        /// <summary>The bubble the player sees: every 1.4-5 s, no fixed rhythm, somewhere on the lava in view at any zoom a slow dome 1-2 m wide comes up, bright at its foot, and bursts.</summary>
        void ShowBubbles(float dt)
        {
            if (dt <= 0f || showSpots == null || showSpots.Length == 0) return;
            showDue -= dt; if (showDue > 0f) return;
            double a = showRng.NextDouble();
            showDue = 1.4f + 3.6f * (float)(a * a) * (lite ? 2f : 1f);
            for (int i = 0; i < 24; i++)
            {
                var at = showSpots[showRng.Next(showSpots.Length)];
                at.x += (float)(showRng.NextDouble() * 1.6 - 0.8); at.z += (float)(showRng.NextDouble() * 1.6 - 0.8);
                if (!SeenAt(at) || !SurfaceAt(at, out float y)) continue;
                at.y = y;
                float r = Fit(at, 0.7f + 0.35f * (float)showRng.NextDouble());
                if (r < 0.45f) continue;
                ShowPops++;
                if (!domes.Swell(at, r, 1.5f + 0.9f * (float)showRng.NextDouble(), true, false)) DomeBurst(at, r, true, false);
                return;
            }
        }

        /// <summary>One more pop on top of the stream: dome, droplets, sound. Second generator. For the melt's last bubble and any kit.</summary>
        public void PopAt(Vector3 at, bool big)
        {
            if (domes == null) return;
            ExtraPops++;
            Bubble(at, big ? OzLavaPops.BigSize : OzLavaPops.SmallSize, big, false);
        }

        /// <summary>More pops near a point for a while: rateMul x the map's present density inside the radius. Second generator.</summary>
        public void PopBurst(Vector3 at, float radius, float rateMul, float seconds)
        {
            if (domes == null || seconds <= 0f) return;
            int slot = 0; for (int i = 0; i < MaxBursts; i++) { if (bursts[i].left <= 0f) { slot = i; break; } if (bursts[i].left < bursts[slot].left) slot = i; }
            float rate = rateMul * (float)pops.Rate * Mathf.PI * radius * radius / LakeArea;
            bursts[slot] = new Burst { at = at, radius = radius, rate = Mathf.Max(0.3f, rate), left = seconds };
        }

        /// <summary>A ring on the lava with no scar (OpenZFxPlayer.LavaRing).</summary>
        public void RingAt(Vector3 at, float radius, float life)
        {
            if (domes == null) return;
            if (SurfaceAt(at, out float y)) at.y = y; else at.y = lavaY;
            domes.AddRing(at, radius, life, 0f, calm ? 0.45f : 1f);
        }

        /// <summary>The widest radius up to r that stays on the lava, so a dome never stands on a bank.</summary>
        float Fit(Vector3 at, float r)
        {
            for (int i = 0; i < 3; i++, r *= 0.72f)
                if (SurfaceAt(at + new Vector3(r, 0f, 0f), out _) && SurfaceAt(at - new Vector3(r, 0f, 0f), out _) && SurfaceAt(at + new Vector3(0f, 0f, r), out _) && SurfaceAt(at - new Vector3(0f, 0f, r), out _)) return r;
            return Mathf.Max(0.3f, r);
        }

        void Bubble(Vector3 at, float size, bool big, bool giant)
        {
            if (SurfaceAt(at, out float y)) at.y = y; else at.y = lavaY;
            if (giant)
            {   // 2 m fits on only 42 % of the stream's spots (counted on the real map): the giant moves to the next pop, so the 2 % stays 2 %
                if (Fit(at, GiantSize) < GiantSize) { giant = false; giantOwed = true; } else { size = GiantSize; big = true; giantOwed = false; GiantPops++; }
            }
            float r = giant ? GiantSize : Fit(at, size);
            float swell = giant ? 1.2f : big ? Random.Range(0.8f, 1.2f) : Random.Range(0.5f, 0.8f);
            if (!domes.Swell(at, r, swell, big, giant)) DomeBurst(at, r, big, giant);
        }

        /// <summary>The moment a dome goes: everything of the burst sits on this frame.</summary>
        public void DomeBurst(Vector3 at, float r, bool big, bool giant)
        {
            if (playSounds) AudioDirector.Fire(big ? SndPopBig : SndPopSmall, at, -1, giant ? 1.3f : 1f, giant ? 0.72f : 1f);
            domes.AddRing(at, r * (giant ? 2.3f : big ? 2.6f : 1.9f), big ? 1.9f : OzLavaDomes.RingSeconds, OzLavaDomes.ScarSeconds, calm ? 0.45f : 1f);
            if (!SeenAt(at)) return;
            var fx = OpenZFxPlayer.I; if (!fx) return;
            float k = Mathf.InverseLerp(0.3f, OzLavaPops.BigSize, r);
            fx.LavaDroplets(at, Mathf.Lerp(0.7f, 1.25f, k), Mathf.RoundToInt(Mathf.Lerp(6f, 18f, k)) + Random.Range(0, 3));
            if (big) fx.LavaSparks(at, r, 12 + Random.Range(0, 6));
            fx.LavaSmoke(at + Vector3.up * 0.1f, 0.35f + 0.3f * k);
            if (Random.value < 0.5f) fx.LavaEmber(at, big ? 3 : 1);
            if (giant)
            {
                fx.LavaSpout(at, Random.Range(2f, 3f), 34);
                fx.LavaSteam(at + Vector3.up * 0.3f, 1.4f);
                if (!calm && FxManager.I) FxManager.I.AddLight(at + Vector3.up * 1.2f, new Color(1f, 0.5f, 0.16f), 12f, 10f, 0.7f, 0.2f, true);
            }
            else if (big && !calm && FxManager.I) FxManager.I.AddLight(at + Vector3.up * 0.8f, new Color(1f, 0.55f, 0.2f), 6f, 6f, 0.45f, 0.2f, true);
        }

        /// <summary>The same test as OnScreen, for the air: near the camera only.</summary>
        public bool NearAt(Vector3 p) => OnScreen(p);
        /// <summary>In view at any zoom, for what is drawn on the lava: the widest camera still shows a dome.</summary>
        public bool SeenAt(Vector3 p)
        {
            if (!cam) cam = Camera.main; if (!cam) return false;
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.z < 340f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }
    }
}
