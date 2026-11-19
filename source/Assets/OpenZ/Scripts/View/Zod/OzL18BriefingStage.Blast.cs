// OpenZ (GPL-3.0) - version 1.5, level 18 briefing stage: the three long bridges, their blasts, the thrown tanks, the smoke.
// THE BRIDGES are the level's own: every bridge object of the rules that wears the long city family (OzBridgeKit.Long), west to east.
// As blue (with the press shot in the list) each gets a copy of its INTACT model at the real one's place; the real destroyed model's
// renderers are off until its blast. A blast = three charges along the deck (north end, middle, south end, ChargeGap apart), each the
// game's own explosion + its bridge break-up (FxManager.Explosion / Breakup, as RulesView.Booms) + two water columns
// (OpenZFxPlayer.Splash) + chunks of the bridge kit thrown with real physics (as RulesView.BridgeChunks) + a light; the camera kick
// of a bridge death on the first one; then the game's ruin fire on the broken ends (OzRuinFires.Bridge) and the span's smoke pillar.
// THE TANKS ON A DECK: each is thrown by the charge nearest to it: up and to one side of its deck (alternating), always into the
// river's middle band (never onto a bank), tumbling; its turret comes off on the launch and flies higher, spinning (the classic Z
// death); hull and turret hit the water (splash, steam, sizzle). The river is shallow (robots wade it): a hull does not sink out of
// sight in it, so SinkBlastAfter seconds after it meets the water its ammunition goes: a blast at the surface, a water column, scrap,
// and the hull is gone in it. All of it is a formula of the clock: the same picture for a held frame. (The tanks BEHIND a deck are
// never thrown: OzL18BriefingStage.Convoy.cs.)
// THE SHAKE of each bridge's blast in the flight's picture, and the blasts' own look on this stage: OzL18BriefingStage.Look.cs.
// THE CLOUD: the game's own mushroom (OpenZNukeMushroom) at MushroomScale over the middle bridge, between the west and east pillars.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        /// <summary>Metres a tile (the map's own), the deck's top over the bridge model's pivot (the city kit's 0.3 m deck + its 0.05 m lift).</summary>
        public const float TileM = 2f;
        public static float DeckTop = 0.35f;
        /// <summary>The blasts: strength of a charge's explosion (the game's own scale; a bridge death's are 1.2), chunks a charge, the
        /// water columns' size and their distance from the deck's middle line (m).</summary>
        public static float ChargeStrength = 1.0f, WaterColumn = 1.0f, WaterSide = 6.5f; public static int ChunksPerCharge = 6;
        /// <summary>The throw: gravity of the stage (m/s2; over 9.81: fast action, not slow motion), the hull's lift-off speed (m/s) and
        /// how far to the side of its deck it comes down (m), the turret's lift-off speed, the spins (deg/s), the hull's middle over its
        /// feet (m), how fast a wreck goes under (m/s) and for how long it is still drawn.</summary>
        public static float Gravity = 14f, HullUp = 8.0f, HullUpMore = 2.6f, HullSide = 7.5f, HullSideMore = 4.5f, TurretUp = 9.0f, TurretUpMore = 2.0f, TurretSide = 6f, TurretSideMore = 3f,
                            HullSpin = 140f, HullSpinMore = 120f, TurretSpin = 420f, TurretSpinMore = 200f, HullMid = 1.2f, SinkSpeed = 1.0f, SinkSeconds = 3.6f, TurretSinkSeconds = 0.9f;
        /// <summary>A hull in the water goes up this many seconds after it met it (0 = it only sinks, SinkSeconds long): the blast's strength, and how
        /// long after the blast the hull is still drawn inside its fireball.</summary>
        public static float SinkBlastAfter = 0.45f, SinkBlastStrength = 0.36f, SinkBlastHide = 0.12f;
        /// <summary>Play check: intact decks that are a stretched model (never an owner picture). (The blasts' camera shake: Look.cs.)</summary>
        public static int StretchedDecks;
        /// <summary>The smoke pillars: puffs a second, puff size (m), life (s), rise (m/s), colour.</summary>
        public static float PillarRate = 7f, PillarSize = 5.5f, PillarLife = 9f, PillarRise = 4.6f;
        public static Color PillarColor = new Color(0.15f, 0.14f, 0.14f, 0.55f); public static Vector3 PillarWind = new Vector3(3.0f, 0f, -0.6f);
        /// <summary>The cloud: the game's mushroom at this scale (0 = none), this long after the middle bridge's first charge; as red it lives this long (s).</summary>
        public static float MushroomScale = 0f, BlueMushroomScale = 0f, MushroomDelay = 0.30f, RedMushroomLife = 24f;
        /// <summary>Red: the cloud the three look at: the approved COLUMN explosion on the middle bridge's far (north) stump, this long after the flight's first frame (below 0 = none).</summary>
        public static float RedCloudAt = 0.30f;
        /// <summary>The paint of a thrown hull: scorched this much at the launch, this much a second later (it burns as it flies; never a black slab).</summary>
        public static float CharAtLaunch = 0.12f, CharLater = 0.6f;

        sealed class Span
        {
            public ZObject o; public Vector3 c; public float half, deckY, waterY, blastAt, stopShort; public GameObject real, intact; public Renderer[] realRends;
            public bool blown = true, fired; public ParticleSystem pillar; public Vector3[] path; public float[] pathLen; public float total, smokeAcc; public bool filled;
        }
        readonly List<Span> spans = new List<Span>();
        readonly List<GameObject> chunks = new List<GameObject>();
        ParticleSystem steam; OpenZNukeMushroom mush; float lifeSaved = -1f;

        // ------------------------------------------------------------------------------------------------ build
        void BuildSpans()
        {
            var W = game.World; if (W == null) return;
            float water = DetailKit.WaterY(game.level);
            foreach (var o in W.Objects)
            {
                if (o == null || !o.IsBridge || o.Type != "bridge_vert") continue;
                int tiles = OzBridgeKit.Tiles(o);
                if (!OzBridgeKit.Long(game.level, tiles)) continue;
                var sp = new Span { o = o, c = game.CenterWorld(o), half = tiles * TileM * 0.5f, waterY = water };
                game.StaticViews.TryGetValue(o.Id, out sp.real);
                sp.deckY = (sp.real ? sp.real.transform.position.y : sp.c.y) + DeckTop; sp.c.y = sp.deckY;
                spans.Add(sp);
            }
            spans.Sort((a, b) => a.c.x.CompareTo(b.c.x));                                              // west to east
            for (int i = 0; i < spans.Count; i++) spans[i].blastAt = blue ? PressToBlast + i * BlastGap : RedBlastAt + i * RedBlastGap;
        }

        /// <summary>Blue: a copy of the intact model on each long bridge, at the real (destroyed) one's place and size.</summary>
        void BuildDecks()
        {
            foreach (var sp in spans)
            {
                var pf = OzBridgeKit.Prefab(game, sp.o, "intact");
                if (!pf || !sp.real) { Warn("no intact bridge model or no bridge in the scene for the bridge at x " + sp.c.x.ToString("F0") + ": that span shows its ruin from the start, no column on it"); continue; }
                var rt = sp.real.transform;
                var go = Instantiate(pf, rt.parent); go.name = "L18Stage_" + sp.real.name + "_intact";
                go.transform.SetLocalPositionAndRotation(rt.localPosition, rt.localRotation); go.transform.localScale = rt.localScale;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                var ls = go.transform.lossyScale; float lo = Mathf.Min(Mathf.Abs(ls.x), Mathf.Min(Mathf.Abs(ls.y), Mathf.Abs(ls.z))), hi = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
                if (lo > 1e-4f && hi / lo > 1.05f)
                {   // the long bridge family is not in the project: this deck is a shorter model pulled long (fine for timing work, NEVER an owner picture)
                    StretchedDecks++;
                    Debug.LogWarning($"[OpenZ L18 briefing] STRETCHED DECK at x {sp.c.x:F0}: the intact bridge model is scaled {ls.x:F2} x {ls.y:F2} x {ls.z:F2} (the long city bridge family is missing): do not record the hero shot before level18_assets has the long bridge");
                }
                go.SetActive(false);
                sp.intact = go;
                var on = new List<Renderer>(); foreach (var r in sp.real.GetComponentsInChildren<Renderer>(true)) if (r.enabled) on.Add(r);
                sp.realRends = on.ToArray();
            }
        }

        void BuildPillars()
        {
            foreach (var sp in spans)
            {
                sp.pillar = OzL07CutFx.Smoke(root, "L18Pillar_" + sp.c.x.ToString("F0"), 0, 1u, new Vector3(sp.c.x, sp.deckY + 0.5f, sp.c.z), 3.2f, PillarRate, PillarSize, PillarLife, PillarRise, PillarColor);
                var main = sp.pillar.main; main.maxParticles = 160;
                var pv = sp.pillar.velocityOverLifetime;                                                 // the pillars lean with the dawn wind (east, a little south): off the bridge's own line, where the last shots and the start camera look
                pv.x = new ParticleSystem.MinMaxCurve(PillarWind.x * 0.8f, PillarWind.x * 1.2f); pv.z = new ParticleSystem.MinMaxCurve(PillarWind.z * 0.8f, PillarWind.z * 1.2f);
            }
            steam = OzL07CutFx.Smoke(root, "L18Steam", 0, 1u, Vector3.zero, 0.9f, 0f, 2.4f, 2.6f, 1.7f, new Color(0.93f, 0.94f, 0.95f, 0.5f));
            steam.Play();
            if (cars.Count == 0) for (int i = 0; i < spans.Count; i++) { if (RedCloudAt >= 0f && i == spans.Count / 2) continue; spans[i].pillar.Simulate(7f, true, true); spans[i].pillar.Play(); }   // red: the briefing opens after the blasts, the side pillars stand (the middle span wears the cloud)
        }

        /// <summary>A span shows its real destroyed model (true) or the stage's intact copy (false). No copy: always the real one.</summary>
        void SetSpan(Span sp, bool blownNow)
        {
            if (!sp.intact) blownNow = true;
            if (sp.blown == blownNow) return;
            sp.blown = blownNow;
            if (sp.realRends != null) foreach (var r in sp.realRends) if (r) r.enabled = blownNow;
            sp.intact.SetActive(!blownNow);
        }

        void ShowReal(bool on) { foreach (var sp in spans) { if (sp.realRends != null) foreach (var r in sp.realRends) if (r) r.enabled = true; if (sp.intact) sp.intact.SetActive(false); sp.blown = true; } }

        // ------------------------------------------------------------------------------------------------ blue
        void TickBlue(float t, bool forward, float duck)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                var sp = spans[i];
                SetSpan(sp, t >= sp.blastAt);
                for (int k = 0; k < 3; k++) if (Crossed(sp.blastAt + k * ChargeGap, forward)) Charge(sp, k, 1f, false);   // the kick is the stage's own, in the flight's pose (FlightKick)
                if (Crossed(sp.blastAt, forward))
                {
                    sp.fired = true; BlastsFired++;
                    OzRuinFires.Bridge(sp.o);
                    Play("brief_l18_blast_" + i, "explosion-heavy-2", GainBlast, 1f, false, 0.92f + 0.05f * i);
                    Play("brief_l18_collapse_" + i, i % 2 == 0 ? "sd_bridge_collapse_steel_a" : "sd_bridge_collapse_steel_b", GainCollapse, 1f);
                }
                if (Crossed(sp.blastAt + 0.18f, forward)) Play("brief_l18_water_" + i, i % 2 == 0 ? "sd_explosion_water_x_a" : "sd_explosion_water_x_b", GainWater, 1f);
                if (Crossed(sp.blastAt + 0.50f, forward)) Play("brief_l18_groan_" + i, i % 2 == 0 ? "sd_structure_groan_a" : "sd_structure_groan_b", GainGroan, duck);
                if (Crossed(sp.blastAt + 0.85f, forward)) Play("brief_l18_rain_" + i, i % 2 == 0 ? "sd_debris_rain_a" : "sd_debris_rain_b", GainRain, duck);
                SmokeColumn(sp, t - sp.blastAt, forward ? Mathf.Max(0f, t - prevTau) : 0f, false);
                if (sp.pillar)
                {   // the pillar begins with its blast; a held frame after it shows it standing
                    bool want = t >= sp.blastAt;
                    if (want && !sp.pillar.isPlaying) { if (!forward) sp.pillar.Simulate(Mathf.Min(8f, t - sp.blastAt), true, true); sp.pillar.Play(); }
                    else if (!want && sp.pillar.isPlaying) sp.pillar.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            var last = spans[spans.Count - 1]; var mid = spans[spans.Count / 2];
            if (Crossed(last.blastAt + 0.2f, forward)) Play("brief_l18_rumble", "sd_nuke_rumble_tail", GainRumble, duck);
            if (Crossed(mid.blastAt + MushroomDelay, forward)) SpawnMushroom(mid, false);
            PlaceCars(t, forward);
        }

        // ------------------------------------------------------------------------------------------------ red (and blue without the press shot)
        void TickRed(float t, bool forward, float duck, bool asBlue)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                var sp = spans[i];
                SetSpan(sp, true);
                bool cloudSpan = RedCloudAt >= 0f && i == spans.Count / 2;                                // the middle span wears the cloud: its own smoke begins when the cloud has risen
                if (!cloudSpan) SmokeColumn(sp, t + 2.5f, forward ? Mathf.Max(0f, t - prevTau) : 0f, false);
                else
                {
                    if (Crossed(RedCloudAt, forward))
                    {   // the cloud: a ball of fire that climbs into a tall column of rolling heads with fire inside (the approved explosion, its own sound)
                        var at = StumpPoint(sp, true);
                        var cloud = OzUeBlastFx.Begin(OzUeBlasts.Column, at, 1807, OzUeBlasts.StagesOf(OzUeBlasts.Column, 0));
                        if (cloud) cloud.Beat(0, at);
                    }
                    SmokeColumn(sp, t - RedCloudAt - 3.5f, forward ? Mathf.Max(0f, t - prevTau) : 0f, false);
                    if (sp.pillar && t > RedCloudAt + 5f && !sp.pillar.isPlaying) sp.pillar.Play();
                }
                if (Crossed(sp.blastAt, forward))
                {   // a fresh fireball on the broken span: the red briefing opens a moment after the blasts
                    sp.fired = true; BlastsFired++;
                    Charge(sp, 1, 0.8f, i == 0);
                    OzRuinFires.Bridge(sp.o);
                    Play("brief_l18_blast_" + i, "explosion-v2-20260909", GainBlast * 0.55f, duck, false, 0.9f + 0.06f * i);   // under the cloud's own blast: the opening stays under -1 dBFS
                }
                if (Crossed(sp.blastAt + 0.20f, forward)) Play("brief_l18_water_" + i, i % 2 == 0 ? "sd_explosion_water_x_a" : "sd_explosion_water_x_b", GainWater * 0.6f, duck);
                if (Crossed(sp.blastAt + 0.70f, forward)) Play("brief_l18_rain_" + i, i % 2 == 0 ? "sd_debris_rain_a" : "sd_debris_rain_b", GainRain, duck);
                // the last of what went into the river: bubbles and a little steam beside the span for the first seconds
                for (int j = 0; j < 4; j++)
                    if (Crossed(sp.blastAt + 0.9f + 0.8f * j + 0.13f * i, forward))
                    {
                        float side = (j + i) % 2 == 0 ? 1f : -1f;
                        var at = new Vector3(sp.c.x + side * (7f + 1.5f * j), sp.waterY, sp.c.z + (j - 1.5f) * 3.2f);
                        Foam(at, 1.4f);
                        Steam(at, 4);
                    }
            }
            var mid = spans[spans.Count / 2]; var last = spans[spans.Count - 1];
            if (Crossed(mid.blastAt + 0.12f, forward)) SpawnMushroom(mid, true);
            if (Crossed(last.blastAt + 0.25f, forward)) { Play("brief_l18_rumble", "sd_nuke_rumble_tail", GainRumble, duck); Play("brief_l18_groan", "sd_structure_groan_a", GainGroan, duck); }
        }

        // ------------------------------------------------------------------------------------------------ a charge
        Vector3 ChargePoint(Span sp, int k) => new Vector3(sp.c.x, sp.deckY + 0.6f, sp.c.z + (1 - k) * (sp.half - 2f));

        void Charge(Span sp, int k, float strength, bool kick)
        {
            Vector3 p = ChargePoint(sp, k);
            var fx = FxManager.I;
            Fireball(p, ChargeStrength * strength, true);
            if (fx)
            {
                fx.Breakup(p, "metal", 5f, 14);
                fx.AddLight(p + Vector3.up * 5f, new Color(1f, 0.55f, 0.22f), 70f * strength, 80f, 1.4f, 0.25f, true);
            }
            WaterJet(new Vector3(sp.c.x - WaterSide, sp.waterY, p.z), WaterColumn * strength, 0.10f);
            WaterJet(new Vector3(sp.c.x + WaterSide, sp.waterY, p.z + 1.5f), WaterColumn * strength * 0.85f, 0.22f);
            if (kick) OpenZFxShake.I?.Kick(p, OpenZFxCatalog.ShakeBridgePx, "bridge", true);
            Chunks(sp, p, strength >= 1f ? ChunksPerCharge : Mathf.Max(1, ChunksPerCharge / 3));
        }

        /// <summary>The bridge kit's own chunks, thrown with real physics (the recipe of RulesView.BridgeChunks); the stage keeps their
        /// list: whatever has not gone under by the hand-back is destroyed there.</summary>
        void Chunks(Span sp, Vector3 at, int n)
        {
            var set = OzBridgeKit.Prefab(game, sp.o, "chunks"); if (!set) return;
            var parts = new List<Transform>(); foreach (Transform t in set.transform) parts.Add(t);
            if (parts.Count == 0) return;
            for (int i = 0; i < n; i++)
            {
                var src = parts[Random.value < 0.7f ? Random.Range(0, Mathf.Min(6, parts.Count)) : Random.Range(0, parts.Count)];
                var pos = new Vector3(at.x + Random.Range(-2f, 2f), at.y + Random.Range(0.6f, 2.4f), at.z + Random.Range(-2.5f, 2.5f));
                var go = Instantiate(src.gameObject, pos, Random.rotation);
                go.layer = BuildingBreaker.DebrisLayer;
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf && mf.sharedMesh) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = true; }
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 60f; rb.linearDamping = 0.28f; rb.angularDamping = 0.95f;
                rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var away = new Vector3(pos.x - sp.c.x, 0f, (pos.z - at.z) * 0.4f); if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere; away.y = 0f; away.Normalize();
                rb.linearVelocity = away * Random.Range(3f, 8f) + Vector3.up * Random.Range(7f, 14f);
                rb.angularVelocity = Random.insideUnitSphere * Random.Range(2f, 7f);
                AudioDirector.AttachImpacts(go, "dst_debris_metal", "dst_debris_splash");
                go.AddComponent<RockChunkLife>();
                chunks.Add(go);
            }
            chunks.RemoveAll(g => !g);
        }

        // ------------------------------------------------------------------------------------------------ fire, smoke, water (the approved explosion channels)
        static Vector3 RingDir() { float a = Random.value * Mathf.PI * 2f; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
        static readonly Color WaterWhite = new Color(0.95f, 0.97f, 0.98f), WaterGrey = new Color(0.74f, 0.82f, 0.85f), SootC = new Color(0.16f, 0.15f, 0.14f), BurntC = new Color(0.46f, 0.38f, 0.31f);
        /// <summary>Black smoke of burning fuel (the approved explosions' soot): k 0 = the darkest.</summary>
        static Color Soot(float k) => Color.Lerp(new Color(0.105f, 0.098f, 0.092f), new Color(0.27f, 0.25f, 0.23f), k);

        /// <summary>The smoke that stands over a broken span: dark heads rolling up from its two stumps, leaning with the wind (a formula of the
        /// seconds since its blast: thick at first, thinner later). `fill` = the column already stands (the red briefing opens after the blasts).</summary>
        public static float ColumnRate = 7f, ColumnSeconds = 12f; public static Vector3 ColumnWind = new Vector3(0.9f, 0f, -0.2f);
        public static float ColumnLateFrom = 2.6f, ColumnLateTo = 6.0f;
        void SmokeColumn(Span sp, float since, float dt, bool fill)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || !z.HasChannel("ueb_soot")) return;
            if (fill)
            {   // a standing column at once: heads placed up its height, the higher the bigger and the further downwind
                for (int i = 0; i < 30; i++)
                {
                    float k = (i + Random.value) / 30f, h = 1.5f + 24f * k; var at = StumpPoint(sp, i % 2 == 0) + Vector3.up * h + ColumnWind * (h * 0.22f) + Random.insideUnitSphere * (1.0f + 1.6f * k);
                    z.EmitChannel(i % 3 == 0 ? "ueb_cauli" : "ueb_soot", at, Vector3.up * Random.Range(1.2f, 2.6f) + ColumnWind * 0.6f, Random.Range(5f, 8f), Random.Range(2.6f, 3.4f) * (1f + 1.1f * k), Soot(Random.value * 0.7f), 1f);
                }
                return;
            }
            if (since < 0.35f || since > ColumnSeconds || dt <= 0f) return;
            // seconds only. After ColumnLateFrom they give way (by ColumnLateTo) to plain smoke that is thinner, lives half as long and goes with
            // the wind: every puff rises, thins and is gone; nothing dense hangs over a stump.
            float late = Mathf.InverseLerp(ColumnLateFrom, ColumnLateTo, since); bool plain = z.HasChannel("ueb_dust");
            sp.smokeAcc += dt * ColumnRate * Mathf.Lerp(1f, 0.35f, since / ColumnSeconds) * Mathf.Lerp(1f, 0.6f, late);
            while (sp.smokeAcc >= 1f)
            {
                sp.smokeAcc -= 1f; bool north = Random.value < 0.5f;
                bool thin = plain && Random.value < late;
                z.EmitChannel(thin ? "ueb_dust" : "ueb_soot", StumpPoint(sp, north) + Random.insideUnitSphere * 1.2f + Vector3.up * Random.Range(0.5f, 2f),
                    Vector3.up * Random.Range(4.5f, 7.5f) + ColumnWind * Mathf.Lerp(1f, 3.4f, late) + RingDir() * Random.Range(0f, 0.8f),
                    Random.Range(3.6f, 5.2f) * Mathf.Lerp(1f, 0.5f, late), Random.Range(2.0f, 2.9f), Soot(Random.value * 0.6f + 0.25f * late), Mathf.Lerp(1f, 0.5f, late));
                if (Random.value < 0.35f * (1f - late)) z.EmitChannel("ueb_fuel", StumpPoint(sp, north) + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f, Vector3.up * Random.Range(2.5f, 5f), Random.Range(0.5f, 0.85f), Random.Range(1.8f, 2.8f), Color.white, 1f);
            }
        }
        /// <summary>The broken end of a span on our bank (north) or theirs: where its fire and its smoke rise.</summary>
        static Vector3 StumpPoint(Span sp, bool north) => new Vector3(sp.c.x, sp.deckY + 0.4f, sp.c.z + (north ? 1f : -1f) * (sp.half - 1.2f));

        /// <summary>A charge's ball of fire (s 1 = about 12 m across: two and a half deck widths): the flash, the rolling fire, the red flames low
        /// round it, the dark head that rolls up after it, white-hot shards and steel scrap thrown out, embers. No ring is drawn anywhere.
        /// Without the explosion channels (a scene with no effect player) the game's own explosion stands in.</summary>
        void Fireball(Vector3 p, float s, bool scrap)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || !z.HasChannel("ueb_hot")) { var f = FxManager.I; if (f) f.Explosion(p, 2.2f * s); return; }
            float q = Mathf.Sqrt(Mathf.Max(0.05f, s)); int n = Mathf.Max(3, Mathf.RoundToInt(15f * s));
            z.EmitChannel("ueb_flash", p + Vector3.up * 1.4f, Vector3.zero, 0.13f, 17f * q, new Color(1f, 0.93f, 0.78f), 1f);
            z.EmitChannel("ueb_flash", p + Vector3.up * 2.4f, Vector3.zero, 0.38f, 10f * q, new Color(1f, 0.68f, 0.36f), 1f);
            z.EmitChannel("ueb_flash", p + Vector3.up * 2.0f * q, Vector3.up * 3f * q, 0.75f, 15f * q, new Color(1f, 0.52f, 0.2f), 0.8f);   // the fire's own glow, as long as the ball burns
            z.EmitChannel("ueb_flash", p + Vector3.up * 3.5f * q, Vector3.up * 4f * q, 1.15f, 11f * q, new Color(1f, 0.42f, 0.14f), 0.6f, 0.2f);
            for (int i = 0; i < n; i++)                                                                  // the ball of fire: it rolls up and out
                z.EmitChannel("ueb_hot", p + Random.insideUnitSphere * (1.5f * q) + Vector3.up * Random.Range(0.6f, 2.4f) * q, Vector3.up * Random.Range(4f, 15f) * q + RingDir() * Random.Range(1f, 6.5f) * q,
                    Random.Range(1.5f, 2.3f), Random.Range(3.3f, 4.9f) * q, Color.Lerp(BurntC, SootC, Random.value * 0.5f), 1f);
            for (int i = 0; i < n * 2 / 3; i++)
            {   // red flames licking out low round it
                var d = RingDir();
                z.EmitChannel("ueb_fuel", p + d * Random.Range(0.5f, 2.6f) * q + Vector3.up * Random.Range(0.3f, 1.2f), d * Random.Range(2f, 6.5f) * q + Vector3.up * Random.Range(4f, 10f) * q, Random.Range(0.55f, 0.95f), Random.Range(3.0f, 4.6f) * q, Color.white, 1f);
            }
            for (int i = 0; i < n * 4 / 5; i++)                                                          // the dark head that rolls up out of the fire and stands
                z.EmitChannel("ueb_soot", p + Random.insideUnitSphere * (2f * q) + Vector3.up * Random.Range(1.5f, 4f) * q, Vector3.up * Random.Range(4.5f, 10.5f) * q + RingDir() * Random.Range(0.4f, 2.4f) + new Vector3(0.5f, 0f, 0.2f),
                    Random.Range(4.2f, 6.2f), Random.Range(2.6f, 3.8f) * q, Soot(Random.value * 0.6f), 1f, Random.Range(0.12f, 0.5f));
            for (int i = 0; i < n * 2 / 3; i++)                                                          // grey-brown heads beside it (the deck's concrete)
            {
                var d = RingDir();
                z.EmitChannel("ueb_cauli", p + d * Random.Range(0.8f, 3f) * q + Vector3.up * Random.Range(0.6f, 2f), d * Random.Range(2f, 5.5f) * q + Vector3.up * Random.Range(1.5f, 5f) * q, Random.Range(3.0f, 4.4f), Random.Range(2.0f, 2.9f) * q,
                    Color.Lerp(new Color(0.30f, 0.27f, 0.24f), new Color(0.17f, 0.16f, 0.15f), Random.value), 1f);
            }
            if (scrap)
            {
                for (int i = 0; i < 16; i++)
                {   // steel scrap thrown out, and white-hot shards
                    float el = Random.Range(0.4f, 1.15f), sp = Random.Range(12f, 24f) * q; var v = RingDir() * (Mathf.Cos(el) * sp) + Vector3.up * (Mathf.Sin(el) * sp);
                    z.EmitChannel("ueb_dirt", p + Vector3.up * 0.8f, v, Random.Range(1.2f, 2.0f), Random.Range(0.24f, 0.44f), new Color(0.16f, 0.16f, 0.17f), 1f);
                    if (i % 2 == 0) z.EmitChannel("ueb_shard", p + Vector3.up, v * 1.25f, Random.Range(0.35f, 0.7f), Random.Range(0.1f, 0.16f), new Color(1f, 0.9f, 0.7f), 1f);
                }
            }
            for (int i = 0; i < n * 3; i++)
                z.EmitChannel("ueb_ember", p + Random.insideUnitSphere * q + Vector3.up * 2f * q, Random.onUnitSphere * Random.Range(2f, 8f) * q + Vector3.up * Random.Range(1f, 5f), Random.Range(1.4f, 3.0f), Random.Range(0.14f, 0.3f), new Color(1f, Random.Range(0.55f, 0.85f), 0.3f), 1f);
            z.GroundLight(new Vector3(p.x, p.y - 0.5f, p.z), 3.2f * q, false, true);
        }

        /// <summary>A white column of water thrown up from the surface (s 1 = 11 to 13 m tall, in the air for about a second and a half), with its
        /// falling spray, a little mist and ONE soft, broken ring of foam that widens and is gone in under a second. No line circle.</summary>
        void WaterJet(Vector3 at, float s, float delay = 0f)
        {
            if (OzWaterSplash.Jet(at, s, delay, OpenZFxCatalog.RippleColorFor(OzCity.BridgeGame))) { Foam(at, 1.3f * Mathf.Sqrt(Mathf.Max(0.05f, s)), delay); return; }
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || !z.HasChannel("ueb_spray")) return;
            float q = Mathf.Sqrt(Mathf.Max(0.05f, s)); int n = Mathf.Max(4, Mathf.RoundToInt(26f * s));
            for (int i = 0; i < n; i++)
            {   // the jet: white streaks in a narrow cone
                float tilt = Random.Range(0f, 0.24f), sp = Random.Range(10f, 16.5f) * q; var d = Vector3.up * Mathf.Cos(tilt) + RingDir() * Mathf.Sin(tilt);
                z.EmitChannel("ueb_dirt", at + RingDir() * Random.Range(0f, 0.9f) * q + Vector3.up * 0.2f, d * sp, Random.Range(1.0f, 1.7f), Random.Range(0.28f, 0.5f) * q, Color.Lerp(WaterWhite, WaterGrey, Random.value * 0.6f), 1f, delay);
            }
            for (int i = 0; i < n / 4; i++)
            {   // a few sheets of water thrown up as fans; they hang and fall back
                float tilt = Random.Range(0f, 0.3f), sp = Random.Range(7f, 14f) * q; var d = Vector3.up * Mathf.Cos(tilt) + RingDir() * Mathf.Sin(tilt);
                z.EmitChannel("ueb_spray", at + Vector3.up * Random.Range(0.4f, 1.8f) * q + Random.insideUnitSphere * (0.6f * q), d * sp, Random.Range(0.8f, 1.25f), Random.Range(1.1f, 1.8f) * q, Color.Lerp(WaterWhite, WaterGrey, Random.value * 0.5f), 0.8f, delay);
            }
            Foam(at, 1.3f * q, delay);
        }

        /// <summary>One soft, broken ring of foam on the water that widens and is gone (never a drawn circle).</summary>
        void Foam(Vector3 at, float r, float delay = 0f)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || !z.HasChannel("ueb_skirt")) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(7f * r), 5, 16); float a0 = Random.value * 6.283f;
            for (int i = 0; i < n; i++)
            {
                if (Random.value < 0.22f) continue;                                                      // broken, not a closed ring
                float a = a0 + i * 6.283f / n + Random.Range(-0.2f, 0.2f); var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                z.EmitChannel("ueb_skirt", at + d * (r * Random.Range(0.5f, 0.9f)) + Vector3.up * 0.12f, d * Random.Range(1.6f, 3.2f), Random.Range(0.55f, 0.85f), Random.Range(0.6f, 0.9f) * Mathf.Max(0.7f, r * 0.6f), WaterWhite, 0.2f, delay);
            }
        }

        void Steam(Vector3 at, int puffs)
        {
            if (!steam) return;
            steam.transform.position = at + Vector3.up * 0.3f;
            steam.Emit(puffs);
        }

        // ------------------------------------------------------------------------------------------------ the cloud
        void SpawnMushroom(Span sp, bool red)
        {
            float scale = red ? MushroomScale : BlueMushroomScale;
            if (scale <= 0f) return;
            if (red && RedMushroomLife > OpenZNukeMushroom.Life && lifeSaved < 0f) { lifeSaved = OpenZNukeMushroom.Life; OpenZNukeMushroom.Life = RedMushroomLife; }
            mush = OpenZNukeMushroom.Spawn(new Vector3(sp.c.x, sp.waterY, sp.c.z), sp.waterY);
            if (mush) OpenZNukeMushroom.SceneScale = scale;                                            // Spawn starts every cloud at 1
        }

        /// <summary>The red cloud's longer life is the game's own number again once that cloud is gone.</summary>
        void TickMushroomLife() { if (lifeSaved > 0f && (!mush || mush.Age >= OpenZNukeMushroom.Life)) RestoreMushroomLife(); }
        void RestoreMushroomLife() { if (lifeSaved > 0f) { OpenZNukeMushroom.Life = lifeSaved; lifeSaved = -1f; } }

        // ------------------------------------------------------------------------------------------------ the throw of a tank
        /// <summary>Written when the column is built: when this tank is thrown (the charge nearest to where it is at its bridge's blast),
        /// from where, how fast and where it comes down: in the river's middle band, HullSide..HullSide+HullSideMore m beside its deck.</summary>
        void PlanThrow(Car c, Span sp)
        {
            uint s = c.seed;
            RoadPose(sp, RoadAt(c, sp, sp.blastAt), out var at, out _);
            int k = 0; float best = float.MaxValue;
            for (int j = 0; j < 3; j++) { float d = Mathf.Abs(ChargePoint(sp, j).z - at.z); if (d < best) { best = d; k = j; } }
            c.launchAt = sp.blastAt + k * ChargeGap + 0.03f + 0.04f * Rnd(ref s);
            RoadPose(sp, RoadAt(c, sp, c.launchAt), out var p, out var r);
            c.r0 = r; c.p0 = p + r * new Vector3(c.lateral, c.mid, 0f);                                 // the body's middle: it tumbles about it
            float sign = (c.slot + c.span) % 2 == 0 ? 1f : -1f;                                         // one east of its deck, the next west
            if (c.lateral != 0f) sign = (c.r0 * Vector3.right).x * c.lateral >= 0f ? 1f : -1f;           // a squad's two files part: each to its own side
            float up = (HullUp + HullUpMore * Rnd(ref s)) * c.upK;
            float yw = sp.waterY + (c.robot ? 0.3f : 0.9f);
            c.waterAt = (up + Mathf.Sqrt(up * up + 2f * Gravity * Mathf.Max(0f, c.p0.y - yw))) / Gravity;
            float landX = sp.c.x + sign * (HullSide + HullSideMore * Rnd(ref s)) * c.sideK;
            float landZ = Mathf.Clamp(c.p0.z, sp.c.z - sp.half + 6f, sp.c.z + sp.half - 6f) + (Rnd(ref s) - 0.5f) * 3f;   // the river's middle band: never a bank
            c.v0 = new Vector3((landX - c.p0.x) / c.waterAt, up, (landZ - c.p0.z) / c.waterAt);
            c.axis = new Vector3(0.25f * (Rnd(ref s) - 0.5f), 0.15f * (Rnd(ref s) - 0.5f), -sign).normalized;        // over its side, away from the deck
            c.spin = (HullSpin + HullSpinMore * Rnd(ref s)) * c.spinK;
            float tup = TurretUp + TurretUpMore * Rnd(ref s);
            c.turretWaterAt = (tup + Mathf.Sqrt(tup * tup + 2f * Gravity * Mathf.Max(0f, c.p0.y + 1.2f - sp.waterY))) / Gravity;
            float tx = sp.c.x - sign * (TurretSide + TurretSideMore * Rnd(ref s)), tz = landZ + (Rnd(ref s) - 0.5f) * 6f;
            c.tV0 = new Vector3((tx - c.p0.x) / c.turretWaterAt, tup, (tz - c.p0.z) / c.turretWaterAt);
            c.tAxis = new Vector3(1f, 0.2f * (Rnd(ref s) - 0.5f), 0.4f * (Rnd(ref s) - 0.5f)).normalized;
            c.tSpin = (TurretSpin + TurretSpinMore * Rnd(ref s)) * (Rnd(ref s) < 0.5f ? 1f : -1f);
            c.hasThrow = true;
        }

        /// <summary>The clock went back before its launch: the turret sits on its ring again, the paint is clean, nothing is under.</summary>
        void Unthrow(Car c)
        {
            c.launched = c.splashed = c.sunk = c.turretSplashed = c.blown = false; c.animState = "";
            if (c.turret && c.turretHome) { c.turret.SetParent(c.turretHome, false); c.turret.localPosition = c.turretPos; c.turret.localRotation = c.turretRot; c.turret.gameObject.SetActive(true); }
            if (c.paint) c.paint.Charred = 0f;
            if (c.root && !c.root.gameObject.activeSelf) c.root.gameObject.SetActive(true);
        }

        /// <summary>A hull in the water: bubbles and steam, and SinkBlastAfter seconds in its ammunition goes: a blast at the surface, a water
        /// column, scrap; SinkBlastHide later the hull is no longer drawn (the river is shallow: no wreck may stand in it at the hand-back).
        /// SinkBlastAfter 0: it only sinks, SinkSeconds long.</summary>
        void SinkOrBlow(Car c, Span sp, Vector3 surface, float w, bool forward)
        {
            var fx = FxManager.I; var pl = OpenZFxPlayer.I; bool blast = SinkBlastAfter > 0f;
            if (c.robot)
            {   // a robot goes under where it fell: no blast of its own
                bool under = w > 0.35f;
                if (under && !c.sunk) { c.sunk = true; if (forward) HullsSunk++; } else if (!under) c.sunk = false;
                return;
            }
            if (blast && w >= SinkBlastAfter)
            {
                if (!c.blown)
                {
                    c.blown = true;
                    if (forward)
                    {
                        Fireball(surface + Vector3.up * 0.6f, SinkBlastStrength, true);
                        if (fx) { fx.Breakup(surface + Vector3.up * 0.5f, "metal", 4f, 10); fx.AddLight(surface + Vector3.up * 3f, new Color(1f, 0.55f, 0.22f), 16f, 36f, 0.8f, 0.25f, true); }
                        WaterJet(surface, 0.75f);
                        Steam(surface, 12);
                        Play("brief_l18_sink_blast", "explosion-v2-20260909", GainSinkBlast, 1f, false, 1.08f + 0.05f * (c.slot % 4));
                    }
                }
            }
            else
            {
                c.blown = false;
                if (forward && Mathf.FloorToInt(w / 0.3f) != Mathf.FloorToInt((w - (tau - prevTau)) / 0.3f)) { Foam(surface, 1.2f); Steam(surface, 3); }   // foam and steam as it goes down
            }
            bool gone = blast ? w >= SinkBlastAfter + SinkBlastHide : w > SinkSeconds;
            if (gone && !c.sunk) { c.sunk = true; if (forward) HullsSunk++; }
            else if (!gone) c.sunk = false;
        }

        /// <summary>Hulls and turrets of the deck's tanks that are in the air now (launched, not yet in the water).</summary>
        int InAir(float t)
        {
            int n = 0;
            foreach (var c in cars)
            {
                if (c.behind) { if (t >= c.edgeAt && t < c.rollWaterAt) n++; continue; }
                if (!c.hasThrow || t < c.launchAt) continue;
                float u = t - c.launchAt; if (u < c.waterAt) n++; if (c.turret && u < c.turretWaterAt) n++;
            }
            return n;
        }

        void PlaceThrown(Car c, Span sp, float t, bool forward)
        {
            float u = t - c.launchAt;
            var fx = FxManager.I; var pl = OpenZFxPlayer.I;
            if (!c.launched)
            {
                c.launched = true;
                if (!c.root.gameObject.activeSelf) c.root.gameObject.SetActive(true);
                if (c.turret) c.turret.SetParent(root, true);                                           // off its ring: from here it flies its own way
                if (forward && !c.robot) Fireball(c.p0, c.kind == Kind.Jeep ? 0.3f : 0.42f, false);     // the vehicle's own death blast (a robot is thrown by its bridge's charge alone)
                if (c.robot && c.anim) { c.anim.speed = 1.3f; c.anim.CrossFadeInFixedTime("blast_airborne", 0.05f, 0, 0f); c.animState = "blast_airborne"; }   // arms up, legs kicking: the game's own thrown pose
            }
            // ---- the hull (its paint scorches as it flies: it stays a tank to the eye)
            if (c.paint) { float ch = Mathf.Lerp(CharAtLaunch, CharLater, Mathf.Clamp01(u / 1.1f)); if (Mathf.Abs(c.paint.Charred - ch) > 0.02f) c.paint.Charred = ch; }
            Vector3 pw = c.p0 + c.v0 * c.waterAt + Vector3.down * (0.5f * Gravity * c.waterAt * c.waterAt);
            Vector3 p; float turned;
            if (u < c.waterAt)
            {
                p = c.p0 + c.v0 * u + Vector3.down * (0.5f * Gravity * u * u); turned = c.spin * u;
                if (forward && fx && !c.robot && Mathf.FloorToInt(u / 0.16f) != Mathf.FloorToInt((u - (t - prevTau)) / 0.16f)) fx.Burn(p, 0.9f);   // it burns as it flies
            }
            else
            {
                float w = u - c.waterAt, drift = (1f - Mathf.Exp(-3f * w)) / 3f;
                p = pw + new Vector3(c.v0.x, 0f, c.v0.z) * (drift * 0.4f);
                p.y = pw.y - 1.0f * (1f - Mathf.Exp(-7f * w)) - SinkSpeed * w;                             // it lies low in the water at once (never a hull standing on the surface)
                turned = c.spin * (c.waterAt + (1f - Mathf.Exp(-2f * w)) / 2f);
                Vector3 surface = new Vector3(p.x, sp.waterY, p.z);
                if (!c.splashed)
                {
                    c.splashed = true;
                    if (forward && c.robot)
                    {   // a robot's own small splash
                        WaterJet(surface, 0.22f);
                        Play("brief_l18_robot_splash", cars.IndexOf(c) % 2 == 0 ? "sd_explosion_water_small_a" : "sd_explosion_water_small_b", GainTurret * 0.7f, 1f, false, 1.15f + 0.06f * (c.slot % 3));
                    }
                    else if (forward)
                    {
                        WaterJet(surface, c.kind == Kind.Jeep ? 0.4f : 0.6f);
                        Steam(surface, 9);
                        int n = cars.IndexOf(c);
                        Play("brief_l18_hull_splash", n % 2 == 0 ? "sd_body_splash_x_a" : "sd_body_splash_x_b", GainHullSplash, 1f);   // every hull that hits the water is heard
                        if (n % 3 == 0) Play("brief_l18_sizzle", "sd_robot_death_fire_sizzle", GainSizzle, 1f);                     // the hot metal's hiss: one in three (they overlap)
                    }
                }
                SinkOrBlow(c, sp, surface, w, forward);
            }
            bool show = !c.sunk;
            if (c.root.gameObject.activeSelf != show) c.root.gameObject.SetActive(show);
            if (show)
            {
                var r = Quaternion.AngleAxis(turned, c.axis) * c.r0;
                c.root.SetPositionAndRotation(p - r * new Vector3(0f, c.mid, 0f), r);
            }
            // ---- the turret: higher, spinning, down on the other side
            if (!c.turret) return;
            Vector3 t0 = c.p0 - c.r0 * new Vector3(0f, c.mid, 0f) + c.r0 * c.tRelPos, tp;             // where it sat on the hull at the launch
            bool tShow = true;
            if (u < c.turretWaterAt) tp = t0 + c.tV0 * u + Vector3.down * (0.5f * Gravity * u * u);
            else
            {
                float w = u - c.turretWaterAt;
                tp = t0 + c.tV0 * c.turretWaterAt + Vector3.down * (0.5f * Gravity * c.turretWaterAt * c.turretWaterAt);
                Vector3 surface = new Vector3(tp.x, sp.waterY, tp.z);
                tp.y -= 0.3f * (1f - Mathf.Exp(-5f * w)) + SinkSpeed * w;
                if (!c.turretSplashed)
                {
                    c.turretSplashed = true;
                    if (forward) { WaterJet(surface, 0.3f); Steam(surface, 4); Play("brief_l18_turret_splash", cars.IndexOf(c) % 2 == 0 ? "sd_explosion_water_small_a" : "sd_explosion_water_small_b", GainTurret, 1f); }
                }
                tShow = w <= TurretSinkSeconds;
            }
            if (c.turret.gameObject.activeSelf != tShow) c.turret.gameObject.SetActive(tShow);
            if (tShow) c.turret.SetPositionAndRotation(tp, Quaternion.AngleAxis(c.tSpin * Mathf.Min(u, c.turretWaterAt + 0.4f), c.tAxis) * c.r0 * c.tRel);
        }
    }
}
