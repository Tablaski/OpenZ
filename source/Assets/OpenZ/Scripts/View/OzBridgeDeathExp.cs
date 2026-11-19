// OpenZ (GPL-3.0) - version 1.5, kit bridge_death_exp_r3 (round 3 R3.43): the explosion of a bridge that dies in normal play, every level,
// every bridge kind. VIEW ONLY: no Sim file, no rule, no damage, no DV row (the rules' bridge death, its health, its repair are untouched).
// Called by RulesView.BuildingDeath right after the bridge has swapped to its destroyed model (SwapBridge: its chunks, its ruin fire); the
// camera kick and the collapse sound stay as they are (AudioDirector.BuildingDeath), the restore animation of kit bridge_restore_r3 too.
//  - SHORT bridge (under LongFromTiles tiles along its axis): exp_v2_3 CHAIN at the span's centre on the deck plane, its scale fitted to
//    the span (the spiral of pops reaching half the span + ChainMarginM), through OzExpInGame.Play (its pick, its beats, its fog shape).
//  - LONG bridge (LongFromTiles tiles or more, any biome): the level 18 briefing's charges along the deck (OzL18BriefingStage.Blast.cs:
//    one charge at each end and in the middle, (half - 2 m) from the centre, ChargeGap s apart, from the +axis end to the -axis end), each
//    the briefing's ball of fire (its recipe below, on the same explosion channels), its break-up, its light and its two water columns;
//    a longer bridge gets more charges (about one every ChargeSpacingM m, never fewer than three).
//  - Refused (no effect player, channels missing, the Unit editor, switched off): false, and the caller plays its old booms.
// A log line per bridge ('[OpenZ FX] bridge: ...'), counts for the checks.
// Kit long_bridge_blast_vary_r6 (14 Nov 2026): the charges of a LONG bridge no longer look pasted. Each charge of one death draws its own
// facing about the vertical axis (spread over the full circle, no two within 60 degrees), size (+-15 %), beat, mirrored twin, place
// along the span and water columns (View/OzBridgeBlastVary.cs: its own generator, never the rules' random stream). The ball keeps its
// recipe (channels, counts, colours, times); a facing shows on it as a slightly oval ring and a head that leans that way.
// OzBridgeBlastVary.On = false draws every charge the same, as before. The short bridge's exp_v2_3 and the level 18 briefing's own
// blasts (OzL18BriefingStage.Blast.cs, which never calls this file) are untouched.
using System.Collections;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public static class OzBridgeDeathExp
    {
        public const string Tag = "[OpenZ FX]";
        /// <summary>On / off (off = the old six booms, everything as before the kit); KeepOldBooms = the old booms play as well (on top).</summary>
        public static bool On = true, KeepOldBooms = false;
        /// <summary>A bridge of this many tiles (2 m each) along its axis or more is LONG (level 16's jungle bridge, level 18's three centre bridges).</summary>
        public static int LongFromTiles = OzBridgeKit.LongFromTiles;
        /// <summary>SHORT: CHAIN's pops reach ChainReachM at scale 1; the scale makes them reach half the span + ChainMarginM, held in ChainMin..ChainMax.</summary>
        public static float ChainReachM = 14f, ChainMarginM = 3f, ChainMin = 0.75f, ChainMax = 1.0f, ChainFlash = 1f;
        /// <summary>LONG (the briefing's numbers): a charge's strength (1 = about 12 m across), s between charges, the end charges this far in
        /// from the span's ends (m), about this far between charges (m), the water columns this far to each side of the deck's line and their size.</summary>
        public static float ChargeStrength = 1.0f, ChargeGap = 0.12f, EndInsetM = 2f, ChargeSpacingM = 10f, WaterSide = 6.5f, WaterColumn = 1.0f;
        /// <summary>Metres a tile; the deck's top over the rules' ground at the bridge's centre (RulesView.BridgeChunks' own numbers).</summary>
        public const float TileM = 2f;
        /// <summary>Counts for the checks.</summary>
        public static int Shorts, Longs, Charges, Refused, Off;
        /// <summary>Kit long_bridge_blast_vary_r6: long bridge deaths drawn with each charge its own turn, and the last one's draws.</summary>
        public static int Varied; public static string LastVary = "";
        public static string LastLine = "";

        public static int Tiles(ZObject o) => OzBridgeKit.Tiles(o);
        public static bool IsLong(ZObject o) => o != null && Tiles(o) >= LongFromTiles;

        /// <summary>RulesView.BuildingDeath for a bridge: true = drawn here (the caller leaves its old booms out unless KeepOldBooms).</summary>
        public static bool Play(MonoBehaviour host, ZGame game, ZObject o)
        {
            if (!On || !host || !game || o == null || !o.IsBridge || !OzExpInGame.InLevel()) { Off++; return false; }
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled) { Refused++; Say($"bridge #{o.Id}: no effect player, the old booms play"); return false; }
            var c = game.CenterWorld(o);
            int tiles = Tiles(o); float len = tiles * TileM, half = len * 0.5f;
            float water = Water(game.level);
            float deck = Mathf.Max(c.y + (game.level == "L02" ? 1.2f : 0.3f), water + 0.4f);
            bool vert = o.Type == "bridge_vert";
            bool drawn = IsLong(o) ? Long(host, game, o, c, tiles, half, deck, water, vert) : Short(o, c, tiles, half, deck);
            return drawn && !KeepOldBooms;
        }

        // ------------------------------------------------------------------------------------------------ short: exp_v2_3 CHAIN
        static bool Short(ZObject o, Vector3 c, int tiles, float half, float deck)
        {
            float k = Mathf.Clamp((half + ChainMarginM) / ChainReachM, ChainMin, ChainMax);
            var O = new Vector3(c.x, deck, c.z);
            if (!OzExpInGame.Play(OzUeBlasts.Chain, O, k, ChainFlash, deck)) { Refused++; Say($"bridge #{o.Id} {o.Type} {tiles} tiles: exp_v2_3 refused, the old booms play"); return false; }
            Shorts++;
            Say($"bridge: exp_v2_3 at t={Now():0.0} s on {o.Type} #{o.Id}, {tiles} tiles ({tiles * TileM:0} m, short), ({O.x:0.0}, {O.y:0.0}, {O.z:0.0}), scale {k:0.00}");
            return true;
        }

        // ------------------------------------------------------------------------------------------------ long: the briefing's charges
        static bool Long(MonoBehaviour host, ZGame game, ZObject o, Vector3 c, int tiles, float half, float deck, float water, bool vert)
        {
            var z = OpenZFxPlayer.I;
            if (!z.HasChannel("ueb_hot")) OzUeBlastFx.Warm(c + Vector3.down * 40f);                        // the explosion channels, made under the ground if the level has none yet
            if (!z.HasChannel("ueb_hot")) { Refused++; Say($"bridge #{o.Id} {o.Type} {tiles} tiles: no explosion channels, the old booms play"); return false; }
            float reach = Mathf.Max(0f, half - EndInsetM);
            int n = Mathf.Max(3, Mathf.RoundToInt(2f * reach / ChargeSpacingM) + 1);
            Longs++;
            Say($"bridge: {n} charges at t={Now():0.0} s on {o.Type} #{o.Id}, {tiles} tiles ({tiles * TileM:0} m, long), centre ({c.x:0.0}, {deck:0.0}, {c.z:0.0}), {2f * reach / (n - 1):0.0} m and {ChargeGap:0.00} s apart, strength {ChargeStrength:0.00}");
            host.StartCoroutine(Run(game, o, c, n, reach, deck, water, vert));
            return true;
        }

        static IEnumerator Run(ZGame game, ZObject o, Vector3 c, int n, float reach, float deck, float water, bool vert)
        {
            var along = vert ? Vector3.forward : Vector3.right; var across = vert ? Vector3.right : Vector3.forward;
            bool city = OzCity.Is(game.level);
            bool vary = OzBridgeBlastVary.On;                                                                // kit long_bridge_blast_vary_r6: each charge its own turn
            var turn = vary ? OzBridgeBlastVary.Pick(n, ChargeGap) : null;
            if (vary) { Varied++; LastVary = $"{Tag} bridge: #{o.Id} each charge its own turn: {OzBridgeBlastVary.Line(turn)}"; Debug.Log(LastVary); }
            for (int i = 0; i < n; i++)
            {
                var t = vary ? turn[i] : default(OzBridgeBlastVary.Turn);
                float d = reach * (1f - 2f * i / (n - 1)) + t.along;                                         // the +axis end, on through the middle, to the -axis end
                var p = new Vector3(c.x, deck + 0.6f, c.z) + along * d;
                Charge(game, p, i, city, water, across, along, c, reach, vary, t);
                if (i == 0 && AudioDirector.Has("exp_large")) AudioDirector.I.Play("exp_large", p, -1, 1f, 0.92f);
                Charges++;
                if (i < n - 1) yield return new WaitForSeconds(vary ? t.gap : ChargeGap);
            }
        }

        /// <summary>One charge (OzL18BriefingStage.Charge without its kick and its chunks: the bridge death has its own of both). Kit
        /// long_bridge_blast_vary_r6: on = this charge's own facing, size, twin and water columns (t); off = the plain charge, as before.</summary>
        static void Charge(ZGame game, Vector3 p, int i, bool city, float water, Vector3 across, Vector3 along, Vector3 c, float reach, bool on, OzBridgeBlastVary.Turn t)
        {
            var fx = FxManager.I;
            float s = on ? ChargeStrength * t.size * t.size : ChargeStrength;                                // the ball's width goes with the root of its strength
            if (on)
            {
                float yaw = t.yawDeg * Mathf.Deg2Rad;
                fbDir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)); fbOval = OzBridgeBlastVary.Oval; fbLean = fbDir * OzBridgeBlastVary.Lean;
                fbBend = new Vector3(-fbDir.z, 0f, fbDir.x) * (t.side * OzBridgeBlastVary.Lean * 4f);
            }
            Fireball(p, s);
            fbOval = 0f; fbLean = Vector3.zero; fbBend = Vector3.zero;                                       // back to the plain ball for every other caller
            if (fx)
            {
                fx.Breakup(p, city || i % 3 != 0 ? "metal" : "stone", 5f, 14);
                fx.AddLight(p + Vector3.up * 5f, new Color(1f, 0.55f, 0.22f), 70f * s, 80f, 1.4f, 0.25f, true);
            }
            var wc = OpenZFxCatalog.RippleColorFor(game.level);
            var w0 = new Vector3(p.x, water, p.z) - across * WaterSide; var w1 = new Vector3(p.x, water, p.z) + across * WaterSide + Vector3.Cross(across, Vector3.up) * 1.5f;
            float k0 = WaterColumn * s, k1 = WaterColumn * s * 0.85f, l0 = 0.10f, l1 = 0.22f;
            if (on)
            {   // the two columns: the twin picks the side of the early, bigger one; each stands at its own angle and distance (never nearer the deck than the plain one, never past the end charges)
                float d = Vector3.Dot(p - c, along);
                w0 = Column(c, along, across, water, d, reach, -t.side, t.w0Far, t.w0Turn); w1 = Column(c, along, across, water, d, reach, t.side, t.w1Far, t.w1Turn);
                k0 *= t.w0Size; k1 *= t.w1Size; l0 += t.w0Late; l1 += t.w1Late;
            }
            bool a = OzWaterSplash.Jet(w0, k0, l0, wc), b = OzWaterSplash.Jet(w1, k1, l1, wc);
            if ((a || b) && i % 2 == 0 && AudioDirector.Has("exp_water")) AudioDirector.I.Play("exp_water", a ? w0 : w1, -1, 0.7f);
        }

        /// <summary>Kit long_bridge_blast_vary_r6: a water column's place: WaterSide x far out to one side of the deck's line, turned by turnDeg
        /// off the straight-across line, held between the two end charges along the span (d = the charge's place along the span).</summary>
        static Vector3 Column(Vector3 c, Vector3 along, Vector3 across, float water, float d, float reach, float side, float far, float turnDeg)
        {
            float lat = WaterSide * far, a = Mathf.Clamp(d + lat * Mathf.Tan(turnDeg * Mathf.Deg2Rad), -reach, reach);
            return new Vector3(c.x, water, c.z) + along * a + across * (side * lat);
        }

        // ------------------------------------------------------------------------------------------------ the briefing's ball of fire
        /// <summary>Kit long_bridge_blast_vary_r6, set by Charge for one Fireball only (all zero = the plain round, upright ball): the charge's
        /// facing, how oval its ring is along that facing, the lean of what rises (m sideways per m up), the sideways bend of the dark head (m/s).</summary>
        static Vector3 fbDir, fbLean, fbBend; static float fbOval;
        static Vector3 RingDir()
        {
            float a = Random.value * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            return fbOval == 0f ? d : d * (1f - 0.5f * fbOval) + fbDir * (Vector3.Dot(d, fbDir) * 1.5f * fbOval);
        }
        /// <summary>h metres up (or m/s up), leaning with the charge's facing (no lean: Vector3.up * h).</summary>
        static Vector3 Up(float h) => new Vector3(fbLean.x * h, h, fbLean.z * h);
        static readonly Color SootC = new Color(0.16f, 0.15f, 0.14f), BurntC = new Color(0.46f, 0.38f, 0.31f);
        static Color Soot(float k) => Color.Lerp(new Color(0.105f, 0.098f, 0.092f), new Color(0.27f, 0.25f, 0.23f), k);

        /// <summary>OzL18BriefingStage.Fireball, the same recipe (s 1 = about 12 m across): the flash, the rolling fire, the red flames low round
        /// it, the dark head that rolls up after it, grey-brown heads, steel scrap and white-hot shards, embers, the ground light.</summary>
        static void Fireball(Vector3 p, float s)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || !z.HasChannel("ueb_hot")) { var f = FxManager.I; if (f) f.Explosion(p, 2.2f * s); return; }
            float q = Mathf.Sqrt(Mathf.Max(0.05f, s)); int n = Mathf.Max(3, Mathf.RoundToInt(15f * s));
            z.EmitChannel("ueb_flash", p + Up(1.4f), Vector3.zero, 0.13f, 17f * q, new Color(1f, 0.93f, 0.78f), 1f);
            z.EmitChannel("ueb_flash", p + Up(2.4f), Vector3.zero, 0.38f, 10f * q, new Color(1f, 0.68f, 0.36f), 1f);
            z.EmitChannel("ueb_flash", p + Up(2.0f * q), Vector3.up * 3f * q, 0.75f, 15f * q, new Color(1f, 0.52f, 0.2f), 0.8f);
            z.EmitChannel("ueb_flash", p + Up(3.5f * q), Vector3.up * 4f * q, 1.15f, 11f * q, new Color(1f, 0.42f, 0.14f), 0.6f, 0.2f);
            for (int i = 0; i < n; i++)
                z.EmitChannel("ueb_hot", p + Random.insideUnitSphere * (1.5f * q) + Up(Random.Range(0.6f, 2.4f) * q), Up(Random.Range(4f, 15f) * q) + RingDir() * Random.Range(1f, 6.5f) * q,
                    Random.Range(1.5f, 2.3f), Random.Range(3.3f, 4.9f) * q, Color.Lerp(BurntC, SootC, Random.value * 0.5f), 1f);
            for (int i = 0; i < n * 2 / 3; i++)
            {
                var d = RingDir();
                z.EmitChannel("ueb_fuel", p + d * Random.Range(0.5f, 2.6f) * q + Vector3.up * Random.Range(0.3f, 1.2f), d * Random.Range(2f, 6.5f) * q + Vector3.up * Random.Range(4f, 10f) * q, Random.Range(0.55f, 0.95f), Random.Range(3.0f, 4.6f) * q, Color.white, 1f);
            }
            for (int i = 0; i < n * 4 / 5; i++)
                z.EmitChannel("ueb_soot", p + Random.insideUnitSphere * (2f * q) + Up(Random.Range(1.5f, 4f) * q), Up(Random.Range(4.5f, 10.5f) * q) + RingDir() * Random.Range(0.4f, 2.4f) + new Vector3(0.5f, 0f, 0.2f) + fbBend,
                    Random.Range(4.2f, 6.2f), Random.Range(2.6f, 3.8f) * q, Soot(Random.value * 0.6f), 1f, Random.Range(0.12f, 0.5f));
            for (int i = 0; i < n * 2 / 3; i++)
            {
                var d = RingDir();
                z.EmitChannel("ueb_cauli", p + d * Random.Range(0.8f, 3f) * q + Vector3.up * Random.Range(0.6f, 2f), d * Random.Range(2f, 5.5f) * q + Vector3.up * Random.Range(1.5f, 5f) * q, Random.Range(3.0f, 4.4f), Random.Range(2.0f, 2.9f) * q,
                    Color.Lerp(new Color(0.30f, 0.27f, 0.24f), new Color(0.17f, 0.16f, 0.15f), Random.value), 1f);
            }
            for (int i = 0; i < 16; i++)
            {
                float el = Random.Range(0.4f, 1.15f), sp = Random.Range(12f, 24f) * q; var v = RingDir() * (Mathf.Cos(el) * sp) + Vector3.up * (Mathf.Sin(el) * sp);
                z.EmitChannel("ueb_dirt", p + Vector3.up * 0.8f, v, Random.Range(1.2f, 2.0f), Random.Range(0.24f, 0.44f), new Color(0.16f, 0.16f, 0.17f), 1f);
                if (i % 2 == 0) z.EmitChannel("ueb_shard", p + Vector3.up, v * 1.25f, Random.Range(0.35f, 0.7f), Random.Range(0.1f, 0.16f), new Color(1f, 0.9f, 0.7f), 1f);
            }
            for (int i = 0; i < n * 3; i++)
                z.EmitChannel("ueb_ember", p + Random.insideUnitSphere * q + Vector3.up * 2f * q, Random.onUnitSphere * Random.Range(2f, 8f) * q + Vector3.up * Random.Range(1f, 5f), Random.Range(1.4f, 3.0f), Random.Range(0.14f, 0.3f), new Color(1f, Random.Range(0.55f, 0.85f), 0.3f), 1f);
            z.GroundLight(new Vector3(p.x, p.y - 0.5f, p.z), 3.2f * q, false, true);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        static float waterY; static int waterScene = -1; static string waterLevel;
        /// <summary>The level's water surface (DetailKit.WaterY, read once per loaded scene: it walks every renderer).</summary>
        static float Water(string level)
        {
            int h = SceneManager.GetActiveScene().handle;
            if (h != waterScene || level != waterLevel) { waterY = DetailKit.WaterY(level); waterScene = h; waterLevel = level; }
            return waterY;
        }

        static double Now() { var g = ZGame.I; return g && g.World != null ? g.World.Time : 0.0; }
        static void Say(string s) { LastLine = Tag + " " + s; Debug.Log(LastLine); }

        /// <summary>One line of every count, for the checks.</summary>
        public static string Report() => $"{Tag} bridge deaths: short (exp_v2_3) {Shorts}, long {Longs} ({Charges} charges), refused {Refused}, off {Off}; long from {LongFromTiles} tiles, old booms on top {(KeepOldBooms ? "yes" : "no")}; each charge its own turn {(OzBridgeBlastVary.On ? "on" : "OFF")}, {Varied} long death(s) so drawn{(LastVary.Length > 0 ? "\n" + LastVary : "")}";
    }
}
