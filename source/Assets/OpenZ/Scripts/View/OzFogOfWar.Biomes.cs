// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the MAYHEM fog's look by biome (the owner's cloud pick, 6 Nov 2026). View only.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// the level's biome at the level start (<see cref="BiomeDecide"/>): its cloud style, the colour idea LAND and the eased cover, which drifts
    /// slowly between its two values for the whole match (<see cref="BiomeStep"/>, every frame on the fog's clock). The testing panel still
    /// leads where it is used (a style, PLANET = that biome's look, COLOUR, COVER: session only); a check that sets a colour other than TODAY or a
    /// cover under 100 % leads too. Every other match (HARD with the panel's FOG ON, the checks' forced fog) keeps today's look exactly. The
    /// fog's rules (what is explored, what is hidden, Covers, the hold-off) never read the cover.
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        /// <summary>The testing panel's COLOUR / COVER buttons were used in this Play session: their values lead in MAYHEM too.</summary>
        static bool panelColour, panelCover;
        /// <summary>Checks only (the owner's time-lapse clip): the biome clock's speed. 1 in the game; reset at every Play.</summary>
        public static float BiomeClockScale = 1f;
        /// <summary>Checks: the colour idea last put on the material.</summary>
        static int appliedColour = -1;
        double biomeClock, biomePhase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BiomeReset() { panelColour = panelCover = false; BiomeClockScale = 1f; appliedColour = -1; }

        /// <summary>This match takes its biome's look: a MAYHEM match with the fog.</summary>
        public static bool BiomeOn => I && I.wanted && I.game && I.game.IsMayhem;
        /// <summary>The biome on screen: the level's (OzFogBiomeLooks.BiomeOf), or the testing panel's PLANET.</summary>
        public static int BiomeIndex => OzFogBiomeLooks.Clamp(PlanetIndex);
        public static OzFogBiomeLooks.Look BiomeLook => OzFogBiomeLooks.Biomes[BiomeIndex];
        /// <summary>The cover on screen eases (MAYHEM, no panel or check cover).</summary>
        public static bool BiomeEases => BiomeOn && !panelCover && cover >= 1f;
        /// <summary>The biome's eased cover now (1 without a match).</summary>
        public static float CoverEased => I ? OzFogBiomeLooks.Cover(BiomeIndex, I.biomeClock, I.biomePhase) : 1f;
        /// <summary>The deep fog's cover on screen (0.3 .. 1): MAYHEM's eased biome cover, else <see cref="Cover"/> as before.</summary>
        public static float CoverNow => OzFogBiomeLooks.CoverFor(BiomeOn, panelCover, cover, BiomeOn ? CoverEased : 1f);
        /// <summary>The colour idea on screen: MAYHEM's biome colour (LAND), else <see cref="ColourIdea"/> as before.</summary>
        public static int ColourNow => OzFogBiomeLooks.ColourFor(BiomeOn, panelColour, colourIdea, ColourByName(BiomeLook.Colour), ColourToday);
        /// <summary>Checks: the biome clock (s) and this level's phase (0..1).</summary>
        public static double BiomeClock { get => I ? I.biomeClock : 0.0; set { if (I) I.biomeClock = value; } }
        public static double BiomePhase => I ? I.biomePhase : 0.0;
        public static int AppliedColour => appliedColour;

        public static int StyleByName(string n) { for (int i = 0; i < Looks.Length; i++) if (Looks[i].Name == n) return i; return Recommended; }
        public static int ColourByName(string n) { for (int i = 0; i < ColourNames.Length; i++) if (ColourNames[i] == n) return i; return ColourToday; }

        /// <summary>The level start (Decide, after the match's choice): the clock from 0 at the level's phase; in MAYHEM the biome's style
        /// (unless the testing panel chose a style for the session).</summary>
        void BiomeDecide()
        {
            biomeClock = 0.0; biomePhase = OzFogBiomeLooks.PhaseOf(game ? game.level : null);
            planetAuto = OzFogBiomeLooks.BiomeOf(game ? game.level : null, W != null && W.Map != null ? W.Map.planet : null);   // review: a level not in the table takes its map's biome (L02 / L07 / L16 / L17: the table, = PlanetOf)
            if (!BiomeOn) return;
            if (sessionLook < 0) { int s = StyleByName(BiomeLook.Style); if (s != look) look = s; }
            lookVersion++;
            Debug.Log($"[OpenZ FogOfWar] MAYHEM biome {BiomeLook.Biome} ({game.level}): {Looks[look].Name}, colour {ColourNames[ColourNow]}, cover {BiomeLook.CoverMin * 100f:0}-{BiomeLook.CoverMax * 100f:0} % easing "
                      + $"(one swing {OzFogBiomeLooks.PeriodS:0} s, phase {biomePhase:0.000}, now {CoverNow * 100f:0.0} %)");
        }

        /// <summary>Every frame (SetMaterial, before the look's version is read): the biome clock on the fog's clock; a changed colour idea
        /// (MAYHEM in or out, the panel) puts the look on the material again.</summary>
        void BiomeStep()
        {
            biomeClock += FowDt * BiomeClockScale;
            if (ColourNow != appliedColour) lookVersion++;
        }

        /// <summary>The testing panel's PLANET changed: in MAYHEM the style follows that biome (unless a session style was chosen).</summary>
        static void BiomePlanetChanged()
        {
            if (BiomeOn && sessionLook < 0) LookIndex = StyleByName(BiomeLook.Style);
        }
    }
}
