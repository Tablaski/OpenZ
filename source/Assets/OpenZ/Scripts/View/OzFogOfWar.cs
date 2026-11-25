// OpenZ (GPL-3.0) - version 1.4: the optional FOG OF WAR (OPTIONS row, default OFF). View only: the rules and the computer never read it.
using System.Collections.Generic;
using System.Diagnostics;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4, the optional FOG OF WAR (OPTIONS, ADVANCED: FOG OF WAR OFF by default / ON, UI/OzFogOfWarOption.cs, read once at the level start).
    /// <list type="bullet">
    /// <item>Rule of the view: the map starts covered. Every unit and building of the player's side (and every flag it holds) opens its
    /// sight circle (<see cref="SightPx"/>), and what has been opened stays open for the rest of the match (explore-once, no grey re-cover).</item>
    /// <item>1.4 radar circle (View/OzFogOfWar.Radar.cs): every standing radar of the player's side opens a circle of 48 m, growing from its building sight over 2.5 s with easing.</item>
    /// <item>The reveal map (<see cref="OzFowMask"/>): a byte a metre over the whole level, stamped every <see cref="StampEvery"/> frames
    /// for the units that moved to another cell, eased up over <see cref="RevealSeconds"/>, uploaded as a tiny R8 texture (56 KB on level 17).</item>
    /// <item>The look (View/OzFogOfWar.Render.cs, Resources/OpenZ/FX/OzFogOfWar.shader; premium clouds since 5 Nov, layered cloud sheets
    /// since round 4): on the player's RTS camera after the atmosphere and before the post effects, four stacked lit cloud sheets hit by
    /// every view ray at full resolution in one pass (the scene depth only takes a sheet off an explored surface standing in front of it),
    /// each fading on its own eased ramp of a smooth signed distance to the hiding line (made again only when the reveal map changed), so
    /// the bank sinks layer by layer and every outline is a soft skin. Three styles (OPTIONS FOG LOOK: CLOUD SEA, CLOUD BANKS, MIST
    /// LAYERS) with a mood for each planet chosen by the level (View/OzFogOfWar.Looks.cs); the testing panel switches style and planet for
    /// the session (Panel.cs); 1.4 (5 Nov) the colour ideas and the see-through of the deep fog likewise (session only, today's by default).</item>
    /// <item>Units of the other sides (enemy and empty ones) standing in ground not explored yet are not drawn on the player's camera (their
    /// renderers held off only while that camera draws, every other value given back right after it); the health bars, hover frames,
    /// production badges and crate markers skip what the fog covers (<see cref="Covers"/>); the minimap is covered the same way
    /// (View/OzFogOfWar.Minimap.cs). Once the ground is explored, everything there shows as today.</item>
    /// <item>Never: in the Unit editor, in 'Zod exact', in a match without a human side, during the briefing, a flyover, the ending
    /// (the match decided) and flyby or capture cameras (<see cref="GateWhy"/>). Off: nothing of it runs or is drawn.</item>
    /// </list>
    /// The computer plays as today (it never reads the fog), so the same fight gives the same result with the fog on or off.
    /// </summary>
    [DefaultExecutionOrder(160)]
    public sealed partial class OzFogOfWar : MonoBehaviour
    {
        public static OzFogOfWar I { get; private set; }

        /// <summary>Frames between two stamp rounds (only units that moved to another cell stamp again).</summary>
        public const int StampEvery = 3;
        /// <summary>A newly seen cell opens over this long (s, real time).</summary>
        public const float RevealSeconds = 0.8f;
        /// <summary>The fog fades in when the player's camera is handed over, and out fast when the match is decided.</summary>
        public const float FadeInSeconds = OzFowGate.FadeInSeconds, FadeOutSeconds = OzFowGate.FadeSeconds;   // 1.4 fog_gameplay_only: smootherstep in real time (View/OzFowGate.cs; 0 at once on a hard cut); fog_fade_in_ease: in 1.6 s, out 0.4 s
        /// <summary>From this strength units in covered ground are hidden, the hover and the bars skip them.</summary>
        public const float HideAt = 0.5f;
        /// <summary>Sight: a unit's attack radius + Zod's agro distance + this pad, at least <see cref="UnitSightMinPx"/> (a grunt 184 px = 23 m);
        /// a building half its longest side + <see cref="BuildingSightPadPx"/>; a flag of ours <see cref="FlagSightPx"/>.</summary>
        public const int UnitSightPadPx = ZBattleSight.UnitSightPadPx, UnitSightMinPx = ZBattleSight.UnitSightMinPx,
            BuildingSightPadPx = ZBattleSight.BuildingSightPadPx, FlagSightPx = ZBattleSight.FlagSightPx;

        /// <summary>This match has the fog (decided at its start; the testing panel can switch it).</summary>
        public static bool Wanted => I && I.wanted;
        /// <summary>The fog on screen now, 0 (not drawn) .. 1.</summary>
        public static float Strength => I ? I.strength : 0f;
        /// <summary>Why this match has no fog (null when it has).</summary>
        public static string WhyOffNow => I ? I.why : "no level";
        /// <summary>The reveal map of this match (null when the fog is off).</summary>
        public static OzFowMask Mask => I ? I.mask : null;
        /// <summary>The uploaded reveal texture (R8, one texel a metre).</summary>
        public static Texture2D MaskTexture => I ? I.tex : null;

        ZGame game; ZWorld W; Team player;
        bool wanted, decided; string why = "not decided yet";
        int forceSession;                                                        // the testing panel: +1 on, -1 off, 0 the OPTIONS row (this match only)
        OzFowMask mask; Texture2D tex;
        OzFowMask campaignMask;
        bool radarFog;
        int radarSightRevision = -1;
        float strength, floorY, blanketY, nextPrune, nextAmbient, nextLog;
        int frame, uploads;
        readonly Dictionary<int, int> lastStamp = new Dictionary<int, int>(256);

        // the hooks' fast state (Covers): set every frame, cleared when the fog goes
        static bool hidingNow; static OzFowMask maskNow; static Team playerNow;

        // ---------------------------------------------------------------- install (every level scene with a ZGame)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null; hidingNow = false; maskNow = null; look = Recommended; sessionLook = -1; lookVersion = 0; planetOverride = -1; DebugMode = 0; LastMsaa = 1; HiddenSkippedOutside = 0;
            mat = null; matTried = false; matWhy = null; pass = null; sheetTex = streakTex = flowTex = noiseBlue = null; BenchNoPass = false; Prewarms = 0; appliedStyle = appliedPlanet = -1;
            GpuTiming = false; GpuReset();
            colourIdea = ColourToday; cover = 1f;                                                       // 1.4 colour / see-through ideas: today's look
            PassesQueued = PassesRecorded = PassesDrawn = 0; HiddenViews = HiddenRenderers = 0; CoversTrue = 0; RestoreSurprises = 0; MinimapPaints = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryInstall();

        static void TryInstall()
        {
            if (!Application.isPlaying) return;
            var g = FindFirstObjectByType<ZGame>();
            if (g && !g.GetComponent<OzFogOfWar>()) g.gameObject.AddComponent<OzFogOfWar>();
            OzRadarLoss.Install(g);
        }

        void Awake() { I = this; game = GetComponent<ZGame>(); }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreHidden();
            RestoreDither();
            if (I == this) { hidingNow = false; maskNow = null; }
        }

        void OnDestroy()
        {
            RestoreHidden();
            RestoreDither();
            if (I == this) { I = null; hidingNow = false; maskNow = null; }
            if (tex) Destroy(tex);
            if (radarGroundMaterial) Destroy(radarGroundMaterial);
            FieldRelease();
            MinimapDestroy();
            LosRelease();                                                           // 1.5 los_fog
        }

        // ---------------------------------------------------------------- the match's choice
        string WhyOff()
        {
            if (OzUeHooks.Active || OzUnitEditor.IsPlayground(SceneManager.GetActiveScene())) return "the Unit editor";
            if (PlayerPrefs.GetInt("oz_zod_exact", 0) != 0 && !(game && game.IsMayhem)) return "'Zod exact'";   // 1.4 MAYHEM (lead, 5 Nov): the player chose the fog: MAYHEM keeps it in 'Zod exact' too
            if (!game || game.blueBot == game.redBot) return "no human side (a computer match)";
            if (forceSession < 0) return "switched off on the testing panel";
            if (forceSession == 0 && !game.IsMayhem) return "not MAYHEM (the fog of war is MAYHEM's: HARD + the fog)";
            return null;
        }

        void Decide()
        {
            decided = true;
            W = game.World; player = game.player;
            if (game && game.IsMayhem && panelSession) OzHighScores.Unranked = PanelUnranked; int want = sessionLook >= 0 ? sessionLook : Recommended; if (want != look) { look = want; lookVersion++; }   // 1.4 MAYHEM: the recommended look at the level start, unless the testing panel chose one for the session (OPTIONS FOG LOOK is gone, oz_fog_look is not read); a MAYHEM match that starts after the panel changed a session setting (a style, PLANET, COLOUR, COVER) is not in the table
            planetAuto = PlanetOf(game.level); lookVersion++;
            why = WhyOff(); wanted = why == null;
            BiomeDecide();                                                                          // 1.4 fog_biome_looks (View/OzFogOfWar.Biomes.cs): MAYHEM takes the level's biome look (style, LAND, eased cover)
            if (wanted) StartFog();
            Debug.Log($"[OpenZ FogOfWar] {(wanted ? "ON" : "off")} for this match ({why ?? "MAYHEM"}){(wanted ? $", player {player}, look {LookName}, planet {PlanetNames[PlanetIndex]} ({(planetOverride < 0 ? "auto" : "testing panel")}, {game.level}), map {mask.W} x {mask.H} cells (1 m), explored at the start {mask.ExploredShare() * 100f:0.0} %" : "")}");
        }

        void StartFog()
        {
            if (mask == null)
            {
                mask = new OzFowMask(W.Map.WidthPix, W.Map.HeightPix);
                StampRound(true);
                RadarStart();                                                       // 1.4 radar circle: radars held at the start open at once (View/OzFogOfWar.Radar.cs)
                mask.ShowAll();                                                     // the start area is open on the first frame
                if (!tex) tex = new Texture2D(mask.W, mask.H, TextureFormat.R8, false, true) { name = "OZ_FogOfWarMask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0, hideFlags = HideFlags.DontSave };
                Upload();
                LosStart();                                                         // 1.5 los_fog (View/OzFogOfWar.Los.cs): the seen-now map from where the player's side stands, shown at once
                floorY = FloorAtCentre();
                blanketY = BlanketBase(floorY);
            }
            if (!MaterialEnsure()) { Debug.LogWarning("[OpenZ FogOfWar] " + matWhy + ": the fog is not drawn (the reveal map still runs)"); return; }
            FieldEnsure();                                                          // 1.4 premium clouds: the soft edge field (and the L07 glow map), run once now
            SetMaterial();
            RunField();
            Prewarm();                                                              // round 4: the sheets pass's pipeline state built now (the briefing), not on the first fog frame
        }

        /// <summary>1.4 (5 Nov): the floor of the level blanket the cloud reads its noise from (the shader's _OzFowOutside.y): the median
        /// ground height of a 32 x 32 grid over the map, once a match. The cover itself lies over every surface as before; only its pattern
        /// comes from this one level blanket, so a cliff or trench wall shows no seam (OzFogOfWar.shader). Falls back to the centre's floor.</summary>
        float BlanketBase(float fallback)
        {
            var ys = new List<float>(1024);
            for (int j = 0; j < 32; j++)
                for (int i = 0; i < 32; i++)
                {
                    var p = ZGame.PxToWorld(W.Map.WidthPix * (i + 0.5) / 32.0, W.Map.HeightPix * (j + 0.5) / 32.0);
                    float y = game.GroundY(p.x, p.z);
                    if (!float.IsNaN(y) && y > -50f && y < 50f) ys.Add(y);
                }
            if (ys.Count < 16) return fallback;
            ys.Sort();
            return ys[ys.Count / 2];
        }

        float FloorAtCentre()
        {
            var c = ZGame.PxToWorld(W.Map.WidthPix * 0.5, W.Map.HeightPix * 0.5);
            float y = game.GroundY(c.x, c.z);
            return float.IsNaN(y) || y < -50f || y > 50f ? 0f : y;
        }

        /// <summary>The testing panel: this match's fog on or off (the OPTIONS row is not changed). Refused where the fog never runs.</summary>
        public bool SetForced(bool on, out string refused)
        {
            refused = null;
            int was = forceSession; forceSession = on ? 1 : -1;
            string w = WhyOff();
            if (on && w != null) { forceSession = was; refused = w; Debug.Log($"[OpenZ FogOfWar] testing panel: ON refused ({w})"); return false; }
            if (!decided && game && game.World != null) Decide();
            wanted = w == null; why = w;
            if (wanted) StartFog(); else { strength = 0f; hidingNow = false; RestoreHidden(); }
            Debug.Log($"[OpenZ FogOfWar] testing panel: {(wanted ? "ON" : "OFF")} for this match");
            return true;
        }

        /// <summary>The testing panel's COVER AGAIN: the whole map covered, then opened again from where the player's side stands now
        /// (to show the reveal). The game itself never covers anything again.</summary>
        public void CoverAgain()
        {
            if (mask == null) return;
            RadarReset();                                                               // 1.4 radar circle: our radars open again, animated (View/OzFogOfWar.Radar.cs)
            mask.Clear(); lastStamp.Clear(); StampRound(true); Upload();
            Debug.Log("[OpenZ FogOfWar] testing panel: COVER AGAIN (the map covered, the units' sight opens again)");
        }

        // ---------------------------------------------------------------- sight
        /// <summary>The sight radius (Zod px) of an object of the player's side, 0 = it opens nothing.</summary>
        public static int SightPx(ZObject o, int agro) => ZBattleSight.Radius(o, agro);

        /// <summary>Stamps every object of the player's side (all = also those that did not move since their last stamp).</summary>
        void StampRound(bool all)
        {
            int agro = W.S != null ? W.S.agro_distance : 40;
            var list = W.Objects;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || !W.AreAllied(o.Owner, player) || o.Destroyed || o.KillAt >= 0) continue;
                int r = SightPx(o, agro); if (r <= 0) continue;
                int key = (((mask.CellY(o.CenterY) << 10) | mask.CellX(o.CenterX)) << 6) | Mathf.Min(63, r / OzFowMask.CellPx);
                if (!all && lastStamp.TryGetValue(o.Id, out var k0) && k0 == key) continue;
                lastStamp[o.Id] = key;
                mask.Stamp(o.CenterX, o.CenterY, r);
            }
        }

        void Upload()
        {
            if (!tex || mask == null) return;
            tex.LoadRawTextureData(mask.Shown);
            tex.Apply(false, false);
            uploads++; miniDirty = true; fieldDirty = true;
        }

        // ---------------------------------------------------------------- frame
        /// <summary>Why the fog is not on screen now (null = it is): the briefing, a flyover, the ending, flyby or capture cameras.</summary>
        public string GateWhy() => GateNow(out _);                                 // 1.4 fog_gameplay_only: the one gate (View/OzFogOfWar.Gate.cs, its rule View/OzFowGate.cs)

        /// <summary>The gate as it was before the kit fog_gameplay_only, kept for its checks (the new gate is closed wherever this one is).</summary>
        public string GateWhyBefore()
        {
            if (ZodBriefing.BriefingActive) return "the briefing";
            if (ZodFlyover.Live != null && ZodFlyover.Live.Active) return "a flyover";
            if (ZodBriefing.EndingHolding) return "the ending";
            if (W != null && PlayerInput.MatchDecided(W)) return "the match is decided (ending)";
#if UNITY_EDITOR
            if (OzEpicFlyby.Busy) return "a flyby recording";                          // the editor's flyby recorder (it exists in the editor only)
#endif
            if (CaptureKit.CineActive) return "a capture camera";
            return null;
        }

        void Update()
        {
            if (hiddenNow.Count > 0 || losLights.Count > 0) RestoreHidden();            // a camera that never reached its end (1.5 los_fog: its held lamps too)
            if (!decided) { if (game && game.World != null) Decide(); else return; }
            var loss = OzRadarLoss.I;
            bool radarNow = loss && loss.Fog;
            if (radarNow != radarFog)
            {
                if (radarNow)
                {
                    campaignMask = mask;
                    mask = loss.Sight;
                    if (!tex) tex = new Texture2D(mask.W, mask.H, TextureFormat.R8, false, true)
                        { name = "OZ_RadarSight", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                    EnsureRadarGroundPass();
                }
                else { mask = campaignMask; campaignMask = null; lastStamp.Clear(); }
                radarFog = radarNow;
                radarSightRevision = -1;
                if (mask != null) Upload();
            }
            if ((!wanted && !radarFog) || mask == null)
            {
                strength = 0f; hidingNow = false; RestoreDither();
                MinimapStep(); PanelStep();
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            if (!radarFog)
            {
                if (++frame % StampEvery == 0) StampRound(false);
                RadarStep();
            }
            long t1 = Stopwatch.GetTimestamp();
            int step = Mathf.Clamp(Mathf.CeilToInt(255f * FowDt / RevealSeconds), 1, 255);          // round 4: the fog's step (the capture step while a clip records)
            bool changed = radarFog ? radarSightRevision != loss.SightRevision : mask.Ease(step);
            if (radarFog) radarSightRevision = loss.SightRevision;
            long t2 = Stopwatch.GetTimestamp();
            if (changed) Upload();
            LosStep();                                                                  // 1.5 los_fog (View/OzFogOfWar.Los.cs): the seen-now map (20 a second), its easing, its upload and the enemy units it hides
            GateStep();                                                                                 // 1.4 fog_gameplay_only (View/OzFogOfWar.Gate.cs): the one gate sets strength (a 0.4 s smootherstep fade, 0 at once on a cut), Covers and the units held off
            maskNow = mask; playerNow = player;                 // nothing is hidden when the fog cannot be drawn
            if (strength <= 0.001f) RestoreDither();
            if (!radarFog)
            {
                SetMaterial();
                if (fieldDirty) RunField();
            }
            GpuStep();
            long t3 = Stopwatch.GetTimestamp();                                                         // (the upload bucket: upload + the material + the field dispatch)
            long t4 = t3;
            MinimapStep();
            long t5 = Stopwatch.GetTimestamp();
            PanelStep();
            CostFrame(t1 - t0, t2 - t1, t3 - t2, t5 - t4);
            if (Time.unscaledTime >= nextPrune) { nextPrune = Time.unscaledTime + 30f; lastStamp.Clear(); PruneRendererCache(); }
            if (Time.unscaledTime >= nextLog) { if (nextLog > 0f) { Debug.Log("[OpenZ FogOfWar] " + CostLine()); CostReset(); } nextLog = Time.unscaledTime + 60f; }
        }

        // ---------------------------------------------------------------- the hooks' question
        /// <summary>Checks: how often <see cref="Covers"/> said yes.</summary>
        public static int CoversTrue;

        /// <summary>The fog covers this object now: it is not of the player's side and the ground under its centre is not explored yet (a
        /// building: none of its corners either). The hover, the health bars, the production badges and the crate markers skip it.
        /// Always false when the fog is not on screen (off, the briefing, a flyover, the ending).</summary>
        internal static bool ContactVisible(int x, int y)
        {
            var fog = I;
            if (!fog || !fog.wanted || fog.mask == null) return true;
            return fog.W.CampaignFogTeam == fog.player && fog.W.TickIndex > 0
                ? fog.W.PlayerCanSeeOrderPoint(fog.player, x, y)
                : fog.mask.ShownAtPx(x, y) >= OzFowMask.Revealed &&
                    (!LosOn || fog.seen == null || fog.LosLifted(fog.seen.ShownAtPx(x, y)) >= LosHideBelow);
        }

        public static bool Covers(ZObject o)
        {
            if (I && I.radarFog) return OzRadarLoss.Covers(o);
            if (!hidingNow || o == null || o.Owner == playerNow) return false;
            if (o.Type == "flag" || o.World?.AreAllied(o.Owner, playerNow) == true) return false;
            if (o.IsUnit || o.Kind == ZKind.Building || o.IsDestroyableImpass)
            {
                if (ContactVisible(o.CenterX, o.CenterY)) return false;
                CoversTrue++;
                return true;
            }
            var m = maskNow; if (m == null) return false;
            if (LosCovers(o)) return LosCountCovered();                                 // 1.5 los_fog (View/OzFogOfWar.Los.cs): an enemy robot or vehicle on explored ground nothing of ours sees now
            if (m.ShownAtPx(o.CenterX, o.CenterY) >= OzFowMask.Revealed) return false;
            if (o.Kind == ZKind.Building)
            {
                int x1 = o.LocX + o.WidthPix - 1, y1 = o.LocY + o.HeightPix - 1;
                if (m.ShownAtPx(o.LocX, o.LocY) >= OzFowMask.Revealed || m.ShownAtPx(x1, o.LocY) >= OzFowMask.Revealed
                    || m.ShownAtPx(o.LocX, y1) >= OzFowMask.Revealed || m.ShownAtPx(x1, y1) >= OzFowMask.Revealed) return false;
            }
            CoversTrue++;
            return true;
        }

        // ---------------------------------------------------------------- cost (log only)
        long cStamp, cEase, cUpload, cMini, cHide, cMax; int cFrames;
        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        void CostFrame(long stamp, long ease, long upload, long mini)
        {
            cStamp += stamp; cEase += ease; cUpload += upload; cMini += mini; cFrames++;
            long tot = stamp + ease + upload + mini + lastHide;
            if (tot > cMax) cMax = tot;
            lastHide = 0;
        }
        long lastHide;

        /// <summary>Checks: the fog's own processor time a frame since the last reset (ms): stamp, reveal, upload, hide, minimap, total, and the worst frame.</summary>
        public static double[] CostMs()
        {
            var f = I; if (!f || f.cFrames == 0) return new double[7];
            double n = f.cFrames;
            double s = Ms(f.cStamp) / n, e = Ms(f.cEase) / n, u = Ms(f.cUpload) / n, h = Ms(f.cHide) / n, m = Ms(f.cMini) / n;
            return new[] { s, e, u, h, m, s + e + u + h + m, Ms(f.cMax) };
        }
        public static int CostFrames => I ? I.cFrames : 0;
        public static void CostReset() { var f = I; if (!f) return; f.cStamp = f.cEase = f.cUpload = f.cMini = f.cHide = f.cMax = 0; f.cFrames = 0; }
        public static int Uploads => I ? I.uploads : 0;

        public static string CostLine()
        {
            var c = CostMs(); var f = I;
            return $"cost a frame (ms, {CostFrames} frames): stamp {c[0]:0.000}, reveal {c[1]:0.000}, upload {c[2]:0.000}, hide {c[3]:0.000}, minimap {c[4]:0.000}, total {c[5]:0.000} (worst frame {c[6]:0.000}); "
                 + $"explored {(f && f.mask != null ? f.mask.ExploredShare() * 100f : 0f):0.0} %, uploads {Uploads}, strength {Strength:0.00}, hidden views {HiddenViews}, look {LookName}, planet {PlanetName}"
                 + (GpuTiming ? $"; GPU ms field {GpuMsField:0.000}, sheets {GpuMsCloud:0.000}" : "") + LosCostText();   // 1.5 los_fog
        }
    }
}
