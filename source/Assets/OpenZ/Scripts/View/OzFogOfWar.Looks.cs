// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the three cloud styles and each planet's mood for them.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR, its looks (premium clouds, 5 Nov; round 4 the same day: layered cloud sheets). All three draw the same
    /// reveal map the same way: four stacked cloud sheets over one flat blanket floor, each a flat plane at its own height showing a
    /// high-resolution tileable cloud picture, hit by every view ray at full resolution (parallax between the sheets, no seam on cliff,
    /// trench or mesa outlines), lit by the scene's sun and sky. At the explored border every sheet fades on its own eased ramp of the
    /// distance past the hiding line, staggered (the crowns well inside the covered ground, the deck and the edge mist a few metres inside
    /// the explored ground), so the bank sinks layer by layer and every outline is a soft skin of at least 80 pixels and 2 m.
    /// They differ in shape, weight, light and motion:
    /// <list type="bullet">
    /// <item>CLOUD SEA (the default): a deep rolling sea, cauliflower billows, bright crowns over shaded troughs.</item>
    /// <item>CLOUD BANKS: tall separate cumulus heaps, deep shadowed gaps over a darker haze floor, shadows on the open ground.</item>
    /// <item>MIST LAYERS: low rolling stratus stretched along the wind, a thin broken veil racing above.</item>
    /// </list>
    /// Every style takes the planet's mood, chosen by the level automatically: L02 DESERT warm sunlit and dusty, L16 JUNGLE humid misty
    /// green-grey, L17 CITY dark grey-blue storm (lit from inside by the storm's mid / close flashes), L07 VOLCANO sunset ash lit orange-red
    /// from the lava below with dark smoky tops. The planet's mood leads the colour: only a share of the scene sun's own hue is kept. At
    /// night the sun's and the sky's share follow the night (NightGrade.NightK). The style is the OPTIONS row FOG LOOK
    /// (UI/OzFogLookOption.cs); the testing panel can switch style and planet for the session.
    /// 1.4 colour and see-through ideas:
    /// the cloud's colour idea (<see cref="ColourIdea"/>: TODAY, LAND, AIR, DUSK, each planet's own multipliers in its palette) and the deep
    /// fog's cover (<see cref="Cover"/>: 100 % today, 85, 70, 55 %), set from the testing panel and by the owner's captures, session only:
    /// every Play starts with today's look (TODAY, 100 %) until the owner picks.
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        /// <summary>One cloud sheet. h: height over the blanket floor (m); Tile (m), RotDeg (its frame turned from the wind), Stretch (along
        /// its frame); D0 .. D1: its eased cover ramp in metres past the hiding line (&lt; 0 inside the explored ground); ThrFull: the shape
        /// threshold at full cover; AMax; Thin: the cover over which its density fades in; light: RelH (relief, m), KN (normal gain), Self
        /// (self shadow), Cast (its shadow on the sheets below; only the two upper sheets cast), Tone, LowT (the lowest cloud's tone), Det
        /// (fine tone detail), Strk (streak tone), FineRel (fine relief), RimK (silver lining); motion: Drift (m/s along the wind), Curl (m).
        /// IsMist: the edge mist (the wind streaks' picture; always the fourth sheet).</summary>
        public struct Layer
        {
            public float H, Tile, RotDeg, Stretch, D0, D1, ThrFull, AMax, Thin, RelH, KN, Self, Cast, Tone, LowT, Det, Strk, FineRel, RimK, Drift, Curl;
            public bool IsMist;
        }

        public struct Style
        {
            public string Name, File, Line;
            /// <summary>Four sheets, top to bottom (the fourth is the edge mist).</summary>
            public Layer[] L;
            /// <summary>Soft cloud shadows on the open ground beside the bank (with the planet's own share).</summary>
            public float GroundShadow;
        }

        public struct Palette
        {
            /// <summary>sRGB: sunlit albedo, shade / sky albedo, silver lining, the edge mist's tint.</summary>
            public Color Top, Shade, Rim, MistTint;
            /// <summary>Linear: the planet's sun and sky (times their gains).</summary>
            public Vector3 Sun, Sky;
            public float SunGain, SkyGain, Depth, GroundShadow, MistTintK, MistMul, HueKeep, DriftMul, WetDim, Cap;
            /// <summary>The planet's exposure on the cloud's light (sun and sky), set from measured frames so the covered area sits at the
            /// mood's brightness after the level's own light, grade and tone mapping.</summary>
            public float Exposure;
            /// <summary>L17: the storm flashes' light; L07: the lava glow (sRGB colour, strength, base, fall (m)), the smoky crowns' darkening.</summary>
            public float FlashGain; public Color Glow; public float GlowStr, GlowBase, GlowFall, Smoke;
            /// <summary>At full night (NightGrade.NightK = 1): the scale on the sun's (the moon's) light and on the sky's light.</summary>
            public float NightSun, NightAmb;
            /// <summary>1.4 colour ideas: linear rgb multipliers on every albedo of the cloud (sunlit, shade, silver lining, edge mist), so the
            /// cloud keeps its own light and shade and only its hue and value change ((1, 1, 1) = today). LAND: the planet's ground colour (its
            /// fog-off frame) lifted halfway to today's cloud lightness; AIR: the horizon haze (RenderSettings.fogColor) at today's cloud
            /// lightness; DUSK: a darker, less saturated mix of the two (60 % chroma, 60 % of LAND's value). Fitted in Play (OpenZ/V1.4/Fog of
            /// War: Colour Grid, calibrate) so the deep cloud's mean on screen, after the grade and the tone mapping, lands on its target.</summary>
            public Vector3 Land, Air, Dusk;
        }

        public const int Desert = 0, Jungle = 1, City = 2, Volcano = 3, Arctic = 4;
        public static readonly string[] PlanetNames = { "DESERT", "JUNGLE", "CITY", "VOLCANO", "ARCTIC" };   // 1.4 fog_biome_looks: the palettes are the biomes (View/OzFogBiomeLooks.cs; VOLCANO = LAVA); ARCTIC for the next update
        /// <summary>The minimap's cover colour per planet.</summary>
        public static readonly Color[] MiniOf = { Hex("2B241B"), Hex("1A221C"), Hex("11151C"), Hex("26150F"), Hex("1C232B") };
        /// <summary>The wind (deg from +x towards +z), the field's logistic scale (m), the ground shadows' reach into the explored ground (m).</summary>
        public const float WindDeg = 37f, EdgeK = 4.70f, ShadowReachM = 30f;
        /// <summary>The soft skin: every outline's 0 -> 1 rise spans at least SkinPx pixels and SkinM metres; the slope floor (share of the
        /// 99.5th percentile, so a crest or a valley floor is never a hairline).</summary>
        public const float SkinPx = 80f, SkinM = 2.0f, GFloor = 0.35f;
        /// <summary>The baked sheets' slope pack and 99.5th percentile per tile (bake_sheets.json).</summary>
        public const float SheetGPack = 20.744f, StreakGPack = 42.042f, SheetG995 = 18.34f, StreakG995 = 38.15f;
        /// <summary>The flow field (curl, breathing, edge wander): tile (m), speed along the wind (m/s), the wander (m, only ever opening).</summary>
        public const float FlowTileM = 160f, FlowSpeed = 0.48f, WanderM = 2.5f;
        /// <summary>The edge mist's wind streaks: tile (m) and stretch along the wind.</summary>
        public const float MistTileM = 90f, MistStretch = 3f;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        static Layer Ly(float h, float tile, float rot, float stretch, float d0, float d1, float thr, float amax, float relH, float kN, float self, float cast,
                        float tone, float lowT, float det, float strk, float drift, float curl = 3f, float rimK = 0.8f)
            => new Layer { H = h, Tile = tile, RotDeg = rot, Stretch = stretch, D0 = d0, D1 = d1, ThrFull = thr, AMax = amax, Thin = 0.45f, RelH = relH, KN = kN, Self = self,
                           Cast = cast, Tone = tone, LowT = lowT, Det = det, Strk = strk, FineRel = 1.1f, RimK = rimK, Drift = drift, Curl = curl };

        static Layer Mist(float h, float d0, float d1, float thr, float amax, float tone)
            => new Layer { H = h, Tile = MistTileM, RotDeg = 0f, Stretch = MistStretch, D0 = d0, D1 = d1, ThrFull = thr, AMax = amax, Thin = 0.45f, RelH = 0.5f, KN = 0.3f,
                           Self = 0f, Cast = 0f, Tone = tone, LowT = 0.45f, Det = 0f, Strk = 0f, FineRel = 0f, RimK = 0.3f, Drift = 1.4f, Curl = 3f, IsMist = true };

        public static readonly Style[] Looks =
        {
            new Style { Name = "CLOUD SEA", File = "sea", Line = "a deep rolling sea of clouds, cauliflower billows, bright sunlit crowns over shaded troughs (default)", GroundShadow = 0.12f,
                        L = new[] {
                            //   h     tile  rot  str   d0    d1    thr    aMax relH  kN    self  cast  tone  lowT  det   strk  drift
                            Ly(15f, 150f, 0f, 1f, -1f, 12f, 0.52f, 1f, 7f, 1.3f, 0.70f, 0.65f, 1.00f, 0.28f, 0.12f, 0.02f, 0.90f),
                            Ly(8.5f, 110f, 41f, 1f, -4f, 6f, 0.34f, 1f, 5f, 1.2f, 0.55f, 0.55f, 0.82f, 0.30f, 0.12f, 0.02f, 0.75f),
                            Ly(3f, 120f, -27f, 1f, -5f, 4f, -0.25f, 1f, 3f, 0.9f, 0.40f, 0f, 0.78f, 0.45f, 0.12f, 0.04f, 0.60f),
                            Mist(0.8f, -6f, 1f, 0.38f, 0.30f, 0.92f) } },
            new Style { Name = "CLOUD BANKS", File = "banks", Line = "tall separate cumulus heaps, deep shadowed gaps over a darker haze floor", GroundShadow = 0.28f,
                        L = new[] {
                            Ly(21f, 130f, 12f, 1f, 0f, 14f, 0.60f, 1f, 11f, 1.4f, 0.85f, 0.85f, 1.06f, 0.32f, 0.12f, 0.02f, 0.65f),
                            Ly(11f, 100f, 53f, 1f, -3f, 7f, 0.56f, 1f, 6f, 1.3f, 0.60f, 0.70f, 0.84f, 0.35f, 0.12f, 0.02f, 0.55f),
                            Ly(2.5f, 120f, -31f, 1f, -5f, 4f, -0.25f, 1f, 2f, 0.5f, 0.30f, 0f, 0.66f, 0.55f, 0.12f, 0.08f, 0.45f),
                            Mist(0.8f, -6f, 1f, 0.40f, 0.34f, 0.85f) } },
            new Style { Name = "MIST LAYERS", File = "mist", Line = "low rolling stratus stretched along the wind, a thin broken veil racing above", GroundShadow = 0.15f,
                        L = new[] {
                            Ly(17f, 120f, 0f, 2.0f, 1f, 16f, 0.62f, 0.60f, 2f, 0.5f, 0.30f, 0.45f, 1.02f, 0.60f, 0.12f, 0.06f, 2.00f, 6f),
                            Ly(5f, 110f, 8f, 1.7f, -4f, 6f, 0.30f, 1f, 2.5f, 0.9f, 0.45f, 0.60f, 0.90f, 0.38f, 0.12f, 0.08f, 1.20f, 6f),
                            Ly(1.5f, 120f, -9f, 1.5f, -5f, 4f, -0.30f, 1f, 1.5f, 0.7f, 0.30f, 0f, 0.82f, 0.40f, 0.12f, 0.08f, 1.00f, 6f),
                            Mist(0.5f, -7f, 1f, 0.34f, 0.48f, 0.90f) } },
        };

        static Palette P(string top, string shade, string rim, Vector3 sun, float sunGain, Vector3 sky, float skyGain, float depth, float gshadow)
            => new Palette { Top = Hex(top), Shade = Hex(shade), Rim = Hex(rim), Sun = sun, SunGain = sunGain, Sky = sky, SkyGain = skyGain, Depth = depth, GroundShadow = gshadow,
                             MistTint = Hex(top), MistTintK = 0f, MistMul = 1f, HueKeep = 0.25f, DriftMul = 1f, WetDim = 1f, Cap = 3f, Exposure = 1f, Glow = Color.black, NightSun = 0.55f, NightAmb = 0.6f,
                             Land = Vector3.one, Air = Vector3.one, Dusk = Vector3.one };

        /// <summary>Each planet's mood (the level picks it), shared by the three styles.</summary>
        public static readonly Palette[] Palettes = Build();

        static Palette[] Build()
        {
            var p = new Palette[5];
            // L02 DESERT: warm sunlit and dusty (cream crowns, lilac-grey shade, a dust tint in the edge mist; a strong sky fill so the
            // shaded parts stay soft grey, never smoke, in the level's dawn light)
            p[Desert] = P("FFFAF0", "8E8AA4", "FFE8C4", new Vector3(1.00f, 0.95f, 0.86f), 1.38f, new Vector3(0.80f, 0.85f, 1.00f), 1.3f, 1.2f, 0.22f);
            p[Desert].MistTint = Hex("E8C9A0"); p[Desert].MistTintK = 0.25f; p[Desert].Exposure = 1.1f;
            // 1.4 colour ideas (5 Nov, the reveal clip's 8 s frame; ground 6B4532 (fog-off frame, water left out), today's deep cloud A69C93 on screen, horizon haze D1B59E (day clock 0.950); on-screen targets LAND 956B57, AIR B49982, DUSK 50443C, fitted in Play within dE 0.4 / 0.8 / 0.4)
            p[Desert].Land = new Vector3(0.8101f, 0.4545f, 0.3468f); p[Desert].Air = new Vector3(1.2363f, 0.9341f, 0.7096f); p[Desert].Dusk = new Vector3(0.2583f, 0.2356f, 0.2301f);
            // L16 JUNGLE: humid misty green-grey (more edge mist, darker in rain)
            p[Jungle] = P("EEF4EC", "6F8780", "F2F8EC", new Vector3(0.90f, 1.00f, 0.88f), 1.05f, new Vector3(0.70f, 0.92f, 0.80f), 0.55f, 0.9f, 0.16f);
            p[Jungle].MistTint = Hex("C6D6C8"); p[Jungle].MistTintK = 0.25f; p[Jungle].MistMul = 1.3f; p[Jungle].WetDim = 0.82f; p[Jungle].HueKeep = 0.05f; p[Jungle].Exposure = 1.45f;
            // L17 CITY: dark grey-blue storm clouds, faster, lit from inside by the storm's mid / close flashes; its storm night is dark already
            p[City] = P("D2DAE8", "2A3550", "EEF4FF", new Vector3(0.88f, 0.92f, 1.00f), 1.10f, new Vector3(0.80f, 0.88f, 1.00f), 0.44f, 1.2f, 0.20f);
            p[City].HueKeep = 0.05f; p[City].Exposure = 0.85f; p[City].DriftMul = 1.6f; p[City].WetDim = 0.85f; p[City].NightSun = 1f; p[City].NightAmb = 1f; p[City].FlashGain = 3f;
            // 1.4 colour ideas (5 Nov, the reveal clip's 8 s frame; ground 655448 (fog-off frame), today's deep cloud 808590 on screen, horizon haze CEB6A2 (RenderSettings.fogColor at day clock 0.950); on-screen targets LAND 7C6A5E, AIR 97816E, DUSK 48403B, fitted in Play within dE 0.5 / 0.4 / 0.7)
            p[City].Land = new Vector3(1.0290f, 0.6639f, 0.4296f); p[City].Air = new Vector3(1.5208f, 0.9251f, 0.5328f); p[City].Dusk = new Vector3(0.4291f, 0.3224f, 0.2301f);
            // L07 VOLCANO: ash clouds (grey-mauve crowns, violet-charcoal shade, gold rims, a warm low sun), dark smoky tops away from the
            // lava, lit orange-red from the lava below
            p[Volcano] = P("9A9298", "22182C", "FFB060", new Vector3(1.00f, 0.82f, 0.66f), 1.25f, new Vector3(0.55f, 0.45f, 0.62f), 0.30f, 1.4f, 0.25f);
            p[Volcano].HueKeep = 0.05f; p[Volcano].Exposure = 1.1f; p[Volcano].Glow = Hex("FF5A1E"); p[Volcano].GlowStr = 2.4f; p[Volcano].GlowBase = 0.02f; p[Volcano].GlowFall = 10f; p[Volcano].Smoke = 0.5f;
            // videos run (Colour Grid calibrate, the reveal clip's 8 s frame; until now only in that run's tints)
            p[Jungle].Land = new Vector3(0.6133f, 0.4801f, 0.3020f); p[Jungle].Air = new Vector3(1.6203f, 0.8430f, 0.7266f); p[Jungle].Dusk = new Vector3(0.2579f, 0.1930f, 0.1964f);
            p[Volcano].Land = new Vector3(0.8149f, 0.9450f, 0.9627f); p[Volcano].Air = new Vector3(0.9308f, 1.0354f, 0.8612f); p[Volcano].Dusk = new Vector3(0.2592f, 0.4684f, 0.5111f);
            // ARCTIC (no level yet, the next update): cold white crowns, blue-grey shade, a pale sky fill. PROVISIONAL: its LAND / AIR / DUSK are
            // (1, 1, 1) until its first level is fitted in Play (OpenZ/V1.4/Fog of War: Colour Grid, calibrate) like the four above
            p[Arctic] = P("F2F6FA", "7E8EA6", "FFFFFF", new Vector3(0.94f, 0.97f, 1.00f), 1.20f, new Vector3(0.78f, 0.88f, 1.00f), 1.0f, 1.1f, 0.18f);
            p[Arctic].MistTint = Hex("DCE6F0"); p[Arctic].MistTintK = 0.2f; p[Arctic].HueKeep = 0.10f;
            return p;
        }

        /// <summary>The style ON starts with: CLOUD SEA (OzFogLookOption's default).</summary>
        public const int Recommended = 0;
        static int look = Recommended;
        static int sessionLook = -1;
        static int planetOverride = -1;
        static int lookVersion;
        int planetAuto;

        /// <summary>The style on screen (0 CLOUD SEA, 1 CLOUD BANKS, 2 MIST LAYERS). Set from OPTIONS FOG LOOK at the level start and when it
        /// changes; the testing panel's buttons set it for the session only (<see cref="SessionLook"/>; the saved row is not changed).</summary>
        public static int LookIndex
        {
            get => look;
            set
            {
                int v = ((value % Looks.Length) + Looks.Length) % Looks.Length;
                if (v == look) return;
                look = v; lookVersion++;
                Debug.Log($"[OpenZ FogOfWar] look {Looks[v].Name}: {Looks[v].Line}");
            }
        }
        public static string LookName => Looks[look].Name;

        /// <summary>The testing panel's style for the rest of the session (-1 = the OPTIONS row's); a later level start keeps it.</summary>
        public static int SessionLook
        {
            get => sessionLook;
            set { sessionLook = value < 0 ? -1 : ((value % Looks.Length) + Looks.Length) % Looks.Length; if (sessionLook >= 0) LookIndex = sessionLook; }
        }

        /// <summary>OPTIONS FOG LOOK changed: it leads again (the testing panel's session style is dropped).</summary>
        public static void LookFromOptions(int v) { sessionLook = -1; LookIndex = v; }

        /// <summary>The planet mood on screen (0 DESERT, 1 JUNGLE, 2 CITY, 3 VOLCANO): the level's own unless the testing panel overrides it.</summary>
        public static int PlanetIndex => planetOverride >= 0 ? planetOverride : I ? I.planetAuto : Desert;
        /// <summary>The testing panel's planet: -1 = from the level (AUTO), else 0..3. Session only.</summary>
        public static int PlanetOverride
        {
            get => planetOverride;
            set { int v = value < -1 || value >= Palettes.Length ? -1 : value; if (v == planetOverride) return; planetOverride = v; lookVersion++; Debug.Log($"[OpenZ FogOfWar] planet {PlanetName}"); BiomePlanetChanged(); }   // 1.4 fog_biome_looks: ARCTIC too; in MAYHEM the style follows that biome
        }
        public static string PlanetName => PlanetNames[PlanetIndex] + (planetOverride < 0 ? " (auto)" : " (testing panel)");

        /// <summary>The planet of a level = its biome (1.4 fog_biome_looks: each level names its biome in View/OzFogBiomeLooks.cs: L02 DESERT,
        /// L07 LAVA (VOLCANO), L16 JUNGLE, L17 CITY; a level not in that table DESERT, as before).</summary>
        public static int PlanetOf(string level) => OzFogBiomeLooks.BiomeOf(level);

        /// <summary>1.4 colour ideas: 0 TODAY (the palettes as they are), 1 LAND, 2 AIR, 3 DUSK (the palette's multipliers).</summary>
        public const int ColourToday = 0, ColourLand = 1, ColourAir = 2, ColourDusk = 3;
        public static readonly string[] ColourNames = { "TODAY", "LAND", "AIR", "DUSK" };
        /// <summary>1.4 see-through: the deep fog's cover levels (1 = today: nothing shows through; 0.55: 45 % of the ground shows).</summary>
        public static readonly float[] CoverLevels = { 1f, 0.85f, 0.70f, 0.55f };
        static int colourIdea = ColourToday;
        static float cover = 1f;

        /// <summary>The cloud's colour idea on screen (testing panel, the owner's captures; session only, every Play starts with TODAY).</summary>
        public static int ColourIdea
        {
            get => colourIdea;
            set
            {
                int v = ((value % ColourNames.Length) + ColourNames.Length) % ColourNames.Length;
                if (v == colourIdea) return;
                colourIdea = v; lookVersion++;
                Debug.Log($"[OpenZ FogOfWar] colour {ColourNames[v]}");
            }
        }

        /// <summary>The deep fog's cover, 0.3 .. 1 (testing panel, the owner's captures; session only, every Play starts with 1 = today). The
        /// soft border and the reveal keep their shape (scaled by it); units in covered ground stay held off (HideCovered).</summary>
        public static float Cover
        {
            get => cover;
            set
            {
                float v = Mathf.Clamp(value, 0.3f, 1f);
                if (v == cover) return;
                cover = v; lookVersion++;
                Debug.Log($"[OpenZ FogOfWar] cover {v * 100f:0} %");
            }
        }

        /// <summary>A planet's multiplier for a colour idea ((1, 1, 1) for TODAY).</summary>
        public static Vector3 TintOf(int planet, int idea)
        {
            var c = Palettes[planet];
            return idea == ColourLand ? c.Land : idea == ColourAir ? c.Air : idea == ColourDusk ? c.Dusk : Vector3.one;
        }

        // the material's parameters (OpenZ/FogOfWar, Resources/OpenZ/FX/OzFogOfWar.shader)
        static readonly int IdLay = Shader.PropertyToID("_OzFowLay"), IdRot = Shader.PropertyToID("_OzFowRot"), IdRot2 = Shader.PropertyToID("_OzFowRot2"),
            IdY0 = Shader.PropertyToID("_OzFowY0"), IdSkin = Shader.PropertyToID("_OzFowSkin"), IdGPack = Shader.PropertyToID("_OzFowGPack"),
            IdTop = Shader.PropertyToID("_OzFowTop"), IdShade = Shader.PropertyToID("_OzFowShade"), IdRim = Shader.PropertyToID("_OzFowRim"),
            IdMistTint = Shader.PropertyToID("_OzFowMistTint"), IdSunTint = Shader.PropertyToID("_OzFowSunTint"), IdSkyTint = Shader.PropertyToID("_OzFowSkyTint"),
            IdGlowCol = Shader.PropertyToID("_OzFowGlowCol"), IdGlowP = Shader.PropertyToID("_OzFowGlowP"), IdGround = Shader.PropertyToID("_OzFowGround"),
            IdEdgeK = Shader.PropertyToID("_OzFowEdgeK"), IdSee = Shader.PropertyToID("_OzFowSee");

        static Vector4 Lin(Color c, float a) { var l = c.linear; return new Vector4(l.r, l.g, l.b, a); }
        static Vector4 Tint(Vector4 v, Vector3 t) => new Vector4(v.x * t.x, v.y * t.y, v.z * t.z, v.w);
        static Vector4 Cs(float deg) => new Vector4(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f, 0f);

        static readonly Vector4[] layArr = new Vector4[20], rotArr = new Vector4[4], rot2Arr = new Vector4[4];
        /// <summary>Checks: the style and planet last put on the material.</summary>
        static int appliedStyle = -1, appliedPlanet = -1;

        /// <summary>The style's and the planet's numbers onto the material (the frame's motion, light, night, rain and flashes: SetMaterial).</summary>
        static void ApplyLook(Material m, int style, int planet, float y0)
        {
            if (!m) return;
            var k = Looks[style]; var c = Palettes[planet];
            for (int i = 0; i < 4; i++)
            {
                var l = k.L[i];
                float amax = l.AMax * (l.IsMist ? c.MistMul : 1f);
                layArr[i * 5 + 0] = new Vector4(l.H, 1f / l.Tile, 1f / Mathf.Max(1f, l.Stretch), Mathf.Clamp01(amax));
                layArr[i * 5 + 1] = new Vector4(l.D0, 1f / Mathf.Max(0.5f, l.D1 - l.D0), l.ThrFull, Mathf.Max(0.05f, l.Thin));
                layArr[i * 5 + 2] = new Vector4(l.RelH, l.KN, l.Self, i < 2 ? l.Cast : 0f);
                layArr[i * 5 + 3] = new Vector4(Mathf.Pow(Mathf.Max(0.01f, l.Tone), c.Depth), l.LowT, l.FineRel, l.RimK);
                layArr[i * 5 + 4] = new Vector4(l.Det, l.Strk, l.Curl, l.IsMist ? 1f : 0f);
                float a = WindDeg + l.RotDeg;
                var r = Cs(a); var rf = Cs(a + 71f); var rs = Cs(a - 64f);
                rotArr[i] = new Vector4(r.x, r.y, rf.x, rf.y);
                rot2Arr[i] = new Vector4(rs.x, rs.y, 0f, 0f);
            }
            m.SetVectorArray(IdLay, layArr);
            m.SetVectorArray(IdRot, rotArr);
            m.SetVectorArray(IdRot2, rot2Arr);
            m.SetFloat(IdY0, y0);
            m.SetFloat(IdEdgeK, EdgeK);
            m.SetVector(IdSkin, new Vector4(SkinPx, SkinM, GFloor, OzQuality.Level));
            m.SetVector(IdGPack, new Vector4(SheetGPack, StreakGPack, SheetG995, StreakG995));
            var tn = TintOf(planet, ColourNow);                                                       // 1.4 colour idea ((1, 1, 1) = today; MAYHEM: the biome's LAND, fog_biome_looks)
            m.SetVector(IdTop, Tint(Lin(c.Top, 1f), tn));
            m.SetVector(IdShade, Tint(Lin(c.Shade, 1f), tn));
            m.SetVector(IdRim, Tint(Lin(c.Rim, 1f), tn));
            m.SetVector(IdMistTint, Tint(Lin(c.MistTint, c.MistTintK), tn));
            m.SetFloat(IdSee, 1f - CoverNow);                                                         // 1.4 see-through (0 = today; MAYHEM: the biome's eased cover, set again every frame in SetMaterial)
            float e = c.SunGain * c.Exposure;
            m.SetVector(IdSunTint, new Vector4(c.Sun.x * e, c.Sun.y * e, c.Sun.z * e, c.HueKeep));
            m.SetVector(IdGlowCol, c.GlowStr > 0 ? Vector4.Scale(Lin(c.Glow, c.GlowBase), new Vector4(c.GlowStr, c.GlowStr, c.GlowStr, 1f)) : Vector4.zero);
            m.SetVector(IdGround, new Vector4(0.5f * (k.GroundShadow + c.GroundShadow), ShadowReachM, c.Smoke, c.Cap));
            appliedStyle = style; appliedPlanet = planet; appliedColour = ColourNow;
        }

        /// <summary>Checks: the material holds style i's numbers for the planet on screen (the sheets' heights and thresholds, the sunlit colour).</summary>
        public static bool MaterialHasLook(int i)
        {
            var m = Mat; if (!m || appliedStyle != i || appliedPlanet != PlanetIndex) return false;
            var k = Looks[i]; var c = Palettes[PlanetIndex];
            var arr = m.GetVectorArray(IdLay); var t = m.GetVector(IdTop);
            if (arr == null || arr.Length < 20) return false;
            bool ok = Mathf.Approximately(t.x, c.Top.linear.r * TintOf(PlanetIndex, ColourNow).x);
            for (int j = 0; j < 4; j++) ok &= Mathf.Approximately(arr[j * 5].x, k.L[j].H) && Mathf.Approximately(arr[j * 5 + 1].z, k.L[j].ThrFull);
            return ok;
        }
    }
}
