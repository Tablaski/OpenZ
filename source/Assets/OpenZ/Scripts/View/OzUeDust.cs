using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_dust_night (the last Unity pass: "the Unit editor has no dust system (DustTrails / TrackMarks)", so the big tanks' track dust
    /// never shows where the owner looks at them): the levels' own track prints and dust (View/TrackMarks.cs, View/DustTrails.cs, the big tanks'
    /// heavy dust in View/OzBigTanksFx.Tank.cs) on the Unit editor floors that look like ground. View only: no rule, nothing in any level changes
    /// (every line here answers only while the Unit editor is open).
    /// <list type="bullet">
    /// <item><b>Which floors</b>: Desert dunes = loose sand, as the levels' sand (prints 1, dust 1: the same numbers TrackMarks / DustTrails /
    /// the big tanks use on L02's sand, so the dust is scaled like in the levels); Snow field = snow (prints 0.85, powder 0.6). The ten dark
    /// studio floors (grids, dots, blueprint ...) get none: sand-coloured dust and pressed prints over a black line grid read as stains, not
    /// as ground.</item>
    /// <item><b>Colour</b>: the floor's own colour (TrackSurface.DustColor in the levels = the rendered ground's colour).</item>
    /// <item><b>Decks</b>: on a platform's deck or an obstacle (ground over DeckM) no prints and the levels' bridge-deck dust (TrackMarks.deckDust).</item>
    /// <item><b>Clean</b>: switching to another kind of floor, or CLEAR FIELD, wipes the prints and the dust in the air.</item>
    /// </list>
    /// Hooks (one line each): View/TrackMarks.cs Ensure (the Unit editor gets one TrackMarks + DustTrails, the graphics options oz_tracks /
    /// oz_dusttrails respected), TrackSurface.Build (no terrain paint / height read in the playground: it has no TER_ meshes), TrackSurface
    /// MarkStrength / DustStrength / DustColor (the floor's answers).
    /// </summary>
    public static class OzUeDust
    {
        /// <summary>Master switch (false = the Unit editor as before: no prints, no dust).</summary>
        public static bool On = true;
        /// <summary>The floor styles that look like ground (OzUeBackdrop.Names): 11 Desert dunes, 10 Snow field.</summary>
        public const int DunesStyle = 11, SnowStyle = 10;
        public const float SandPrint = 1f, SandDust = 1f, SnowPrint = 0.85f, SnowDust = 0.6f;
        /// <summary>Ground higher than this (m) is a platform deck or an obstacle, not the floor.</summary>
        public const float DeckM = 0.25f;
        /// <summary>Counters for the Play check.</summary>
        public static int Clears, Queries;

        /// <summary>TrackSurface's queries are the Unit editor's (only while the editor is open).</summary>
        public static bool Here => On && OzUnitEditor.Active;
        /// <summary>TrackMarks.Ensure: the playground hosts the prints and the dust (the scene is the playground).</summary>
        public static bool HostsTracks => On && OzUeHooks.NoGroundDressing;

        /// <summary>0 none (a dark floor), 1 sand, 2 snow.</summary>
        public static int Kind(int style) => style == DunesStyle ? 1 : style == SnowStyle ? 2 : 0;
        public static int KindNow => Kind(OzUeBackdrop.Style);

        static bool Deck(float x, float z) { var g = ZGame.I; return g && g.GroundY(x, z) > DeckM; }

        /// <summary>TrackSurface.MarkStrength in the Unit editor.</summary>
        public static float Mark(float x, float z)
        {
            int k = KindNow; Queries++;
            if (k == 0 || Deck(x, z)) return 0f;
            return k == 1 ? SandPrint : SnowPrint;
        }

        /// <summary>TrackSurface.DustStrength in the Unit editor.</summary>
        public static float Dust(float x, float z)
        {
            int k = KindNow; Queries++;
            if (k == 0) return 0f;
            float s = k == 1 ? SandDust : SnowDust;
            return Deck(x, z) ? s * (TrackMarks.I ? TrackMarks.I.deckDust : 0.3f) : s;
        }

        /// <summary>TrackSurface.DustColor in the Unit editor (sRGB): the floor's own colour.</summary>
        public static Color Colour { get { var c = OzUeBackdrop.Current.floor; float k = FloorLitK; return new Color(k * c.r / 255f, k * c.g / 255f, k * c.b / 255f, 1f); } }
        /// <summary>1.5 closing pass 14k (R11 rest): the floor's colour as the floor renders it. The dust puffs are lit (sun + sky) and drew ~6 %
        /// brighter than the Desert dunes floor (frame: floor 187 / 168 / 131, lone puffs 197 / 179 / 144), so each lone puff showed as a pale
        /// disc; in the levels the colour is read off the rendered ground already.</summary>
        public static float FloorLitK = 0.91f;   // 187 / 205 (the rendered Desert dunes floor / its colour value)

        /// <summary>View/TrackMarks.cs Ensure in the playground: one TrackMarks (+ DustTrails), as every level gets (the same options).</summary>
        public static TrackMarks EnsureTracks()
        {
            if (PlayerPrefs.GetInt("oz_tracks", 1) == 0) return null;
            if (TrackMarks.I) return TrackMarks.I;
            var g = ZGame.I; if (!g || g.World == null || g.World.Map == null) return null;
            var found = Object.FindFirstObjectByType<TrackMarks>(); if (found) return found;
            var go = new GameObject("OpenZTrackMarks");
            var tm = go.AddComponent<TrackMarks>();
            if (PlayerPrefs.GetInt("oz_dusttrails", 1) != 0 && !Object.FindFirstObjectByType<DustTrails>()) go.AddComponent<DustTrails>();
            Debug.Log($"{OzUnitEditor.Tag} track prints + dust on the ground-like floors (Desert dunes, Snow field; ue_dust_night)");
            return tm;
        }

        /// <summary>Every print off the floor and every puff out of the air (another kind of floor, CLEAR FIELD).</summary>
        public static void Wipe(string why)
        {
            bool any = false;
            if (TrackMarks.I) { TrackMarks.I.Clear(); any = true; }
            if (DustTrails.I) { DustTrails.I.Clear(); any = true; }
            if (any) { Clears++; Debug.Log($"{OzUnitEditor.Tag} track prints and dust wiped ({why})"); }
        }

        /// <summary>Counted check lines (the Play check): the floors' answers.</summary>
        public static int SelfCheck(System.Text.StringBuilder log)
        {
            int f = 0;
            f += OzUnitEditor.Line(log, OzUeBackdrop.Names[DunesStyle] == "Desert dunes" && OzUeBackdrop.Names[SnowStyle] == "Snow field",
                                   $"ue_dust_night: style {DunesStyle + 1} is Desert dunes, style {SnowStyle + 1} is Snow field");
            int dark = 0; for (int i = 0; i < OzUeBackdrop.StyleCount; i++) if (Kind(i) == 0) dark++;
            f += OzUnitEditor.Line(log, dark == OzUeBackdrop.StyleCount - 2, $"ue_dust_night: {dark} floors without dust (the dark studio floors)");
            return f;
        }
    }
}
