using System;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Video;

namespace OpenZ
{
    /// <summary>One playable level as the front end shows it (guide 9.15 card data).</summary>
    public sealed class OzLevelDef
    {
        public readonly string Id, Scene, Number, Name, Mood, Wordmark, Objective, Goal;
        public readonly string[] FallbackChips;
        public OzLevelDef(string id, string scene, string number, string name, string mood, string wordmark, string objective, string goal, string[] chips)
        { Id = id; Scene = scene; Number = number; Name = name; Mood = mood; Wordmark = wordmark; Objective = objective; Goal = goal; FallbackChips = chips; }

        /// <summary>The levels in menu order: Z level 2 (desert), Z level 16 (jungle), Z level 17 "Car Park" (city).</summary>
        public static readonly OzLevelDef[] All =
        {
            new OzLevelDef("L02", "L02_Desert", "02", "DESERT", "DAY · DUST HAZE", "Wordmarks/mask_desert",
                "CAPTURE THE FLAGS TO TAKE TERRITORY. BREAK INTO THE BLUE FORT TO WIN.", "Capture the flags. Break into the blue fort.",
                new[] { "9 ZONES", "7 FLAGS", "2 FORTS", "72×90 TILES" }),
            OzLevelL07.Def,
            new OzLevelDef("L16", "L16_Jungle", "16", "JUNGLE", "DAY · SWAMP STEAM", "Wordmarks/mask_jungle",
                "CAPTURE THE FLAGS TO TAKE TERRITORY. BREAK INTO THE BLUE FORT TO WIN.", "Capture the flags. Break into the blue fort.",
                new[] { "15 ZONES", "13 FLAGS", "2 FORTS", "104×128 TILES" }),
            new OzLevelDef("L17", "L17_CarPark", "17", "CAR PARK", "NIGHT · CANAL FOG", "Wordmarks/mask_carpark",
                "CAPTURE THE FLAGS TO TAKE TERRITORY. BREAK INTO THE BLUE FORT TO WIN.", "Capture the flags. Break into the blue fort.",
                new[] { "16 ZONES", "14 FLAGS", "2 FORTS", "110×128 TILES" }),
            OzLevelL18.Def,                                                                                     // 1.5 level 18 "Bridge Game", the fifth card, after the Car Park by number (kit level18_map, register_l18.py; UI/OzLevelL18.cs)
        };
    }

    /// <summary>
    /// Card facts and the minimap inset, read from the level's own Zod map (Resources/OpenZ/Maps/&lt;id&gt;_grid): zone,
    /// flag and fort counts, map size, and a small tactical map at the start ownership (terrain palette and zone tint of
    /// OpenZMinimap, forts / buildings as footprints, flags as dots). Built once per level; any failure keeps the fallback chips.
    /// </summary>
    public sealed class OzLevelInfo
    {
        public int Zones = -1, Flags = -1, Forts = -1, Width, Height;
        public Texture2D Mini;
        public bool Ok => Zones >= 0;

        public string[] Chips(OzLevelDef d) => !Ok ? d.FallbackChips : new[] { $"{Zones} ZONES", $"{Flags} FLAGS", $"{Forts} FORTS", $"{Width}×{Height} TILES" };

        public static OzLevelInfo Load(string level, bool fixedTeamColors = false)
        {
            var info = new OzLevelInfo();
            try
            {
                var st = Resources.Load<TextAsset>("OpenZ/default_settings");
                var map = Resources.Load<TextAsset>($"OpenZ/Maps/{level}_grid");
                if (st == null || map == null) { Debug.LogWarning($"[OpenZ Menu] {level}: map or settings missing in Resources, card uses its fallback facts"); return info; }
                var w = ZWorld.Create(st.text, map.text, 1);
                info.Width = w.Map.width; info.Height = w.Map.height;
                info.Zones = w.Zones.Count; info.Flags = w.Flags.Count;
                int forts = 0;
                foreach (var o in w.Objects) if (o.Kind == ZKind.Building && (o.Type == "fort_front" || o.Type == "fort_back")) forts++;
                info.Forts = forts;
                info.Mini = Bake(w, level, fixedTeamColors: fixedTeamColors);
            }
            catch (Exception e) { Debug.LogWarning($"[OpenZ Menu] {level}: could not read the map ({e.Message}), card uses its fallback facts"); info.Zones = -1; }
            return info;
        }

        static Color32 Mul(Color32 c, float f) => new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), 255);
        static Color32 Mix(Color32 a, Color c, float t) => new Color32((byte)Mathf.Lerp(a.r, c.r * 255f, t), (byte)Mathf.Lerp(a.g, c.g * 255f, t), (byte)Mathf.Lerp(a.b, c.b * 255f, t), 255);
        static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 1023) / 1023f; } }

        /// <summary>Same palette and zone tint as the in-game minimap (OpenZMinimap.Palette / RebuildZones), 3 texels per tile.</summary>
        public static Texture2D Bake(ZWorld W, string level, bool includeUnits = false, bool fixedTeamColors = false)
        {
            const int k = 3; const float zoneTint = 0.3f;
            Color Mark(Team team) => fixedTeamColors ? OzUi.OriginalMark(team) : OzUi.Mark(team);
            Color Paint(Team team) => fixedTeamColors ? TeamColors.OriginalPaint(team) : TeamColors.Of(team);
            int mw = W.Map.width, mh = W.Map.height, tw = mw * k, th = mh * k;
            bool city = W.Map.planet == "city";
            Color32 ground = city ? new Color32(86, 86, 84, 255) : new Color32(158, 120, 80, 255), road = city ? new Color32(118, 116, 110, 255) : new Color32(126, 124, 118, 255),
                    water = city ? new Color32(92, 104, 58, 255) : new Color32(78, 168, 160, 255), deep = city ? new Color32(70, 82, 44, 255) : new Color32(56, 128, 128, 255),
                    cliff = city ? new Color32(172, 168, 158, 255) : new Color32(190, 136, 86, 255), cliffDark = city ? new Color32(138, 134, 126, 255) : new Color32(158, 106, 64, 255);
            if (W.Map.planet == "jungle")
            {   // level 16: grass, grey road, brown swamp, brown rock (the in-game minimap's colours)
                ground = new Color32(104, 122, 54, 255); road = new Color32(124, 122, 116, 255); water = new Color32(110, 96, 50, 255);
                deep = new Color32(86, 74, 38, 255); cliff = new Color32(132, 104, 70, 255); cliffDark = new Color32(104, 80, 52, 255);
            }
            if (W.Map.planet == "volcanic")
            {   // level 7: ash ground, grey rock; lava tiles below in lava orange
                ground = OzVolcanic.MiniGround; road = OzVolcanic.MiniRoad; cliff = OzVolcanic.MiniCliff; cliffDark = OzVolcanic.MiniCliffDark;
            }
            if (W.Map.planet == "arctic")
            {
                ground=new Color32(226,237,242,255); road=new Color32(167,202,221,255); water=new Color32(52,134,194,255);
                deep=new Color32(28,75,132,255); cliff=new Color32(177,207,226,255); cliffDark=new Color32(112,150,180,255);
            }
            var px = new Color32[tw * th];
            int Idx(int x, int yTop) => (th - 1 - yTop) * tw + x;
            var zoneOf = new int[mw * mh];
            for (int ty = 0; ty < mh; ty++) for (int tx = 0; tx < mw; tx++) zoneOf[ty * mw + tx] = W.GetZoneIndex(tx * 16 + 8, ty * 16 + 8);
            int ZoneAt(int tx, int ty) => tx < 0 || ty < 0 || tx >= mw || ty >= mh ? -1 : zoneOf[ty * mw + tx];
            bool Solid(int tx, int ty) => tx < 0 || ty < 0 || tx >= mw || ty >= mh || W.Map.ClassAt(tx, ty) == ZClass.Impassable;
            for (int ty = 0; ty < mh; ty++)
                for (int tx = 0; tx < mw; tx++)
                {
                    var cls = W.Map.ClassAt(tx, ty); bool solid = cls == ZClass.Impassable;
                    bool lava = W.Map.LavaAt(tx, ty);
                    Color32 c = lava ? OzVolcanic.MiniLava(W.Map.LavaKind(tx, ty)) : cls == ZClass.Road ? road : cls == ZClass.Water ? water : cls == ZClass.DeepWater ? deep : solid ? cliff : ground;
                    float n = 0.95f + 0.1f * Hash(tx, ty);
                    bool eN = solid && !Solid(tx, ty - 1), eS = solid && !Solid(tx, ty + 1), eW = solid && !Solid(tx - 1, ty), eE = solid && !Solid(tx + 1, ty);
                    int z = zoneOf[ty * mw + tx]; var owner = z >= 0 ? W.Zones[z].Owner : Team.Neutral;
                    bool zN = ZoneAt(tx, ty - 1) != z, zS = ZoneAt(tx, ty + 1) != z, zW = ZoneAt(tx - 1, ty) != z, zE = ZoneAt(tx + 1, ty) != z;
                    for (int sy = 0; sy < k; sy++)
                        for (int sx = 0; sx < k; sx++)
                        {
                            Color32 q = c;
                            if (solid && !lava && !city && (ty * k + sy) % 3 == 0) q = cliffDark;
                            if (city && (sx == 0 || sy == 0) && cls == ZClass.Ground) q = Mul(q, 0.9f);
                            float f = n;
                            if ((eN && sy == 0) || (eS && sy == k - 1) || (eW && sx == 0) || (eE && sx == k - 1)) f *= 0.62f;
                            q = Mul(q, f);
                            if (owner != Team.Neutral)
                            {
                                bool border = (zN && sy == 0) || (zS && sy == k - 1) || (zW && sx == 0) || (zE && sx == k - 1);
                                q = border ? Mix(q, Mark(owner), 0.85f) : Mix(q, Paint(owner), zoneTint);
                            }
                            px[Idx(tx * k + sx, ty * k + sy)] = q;
                        }
                }
            foreach (var o in W.Objects)                                                                  // building footprints: forts / factories / radar / repair
            {
                if (o.Kind != ZKind.Building || o.Type.StartsWith("bridge")) continue;
                Color32 bc = o.Owner == Team.Neutral ? new Color32(200, 194, 180, 255) : (Color32)Mark(o.Owner);
                int x0 = o.LocX * k / 16, y0 = o.LocY * k / 16, x1 = (o.LocX + o.WidthPix) * k / 16, y1 = (o.LocY + o.HeightPix) * k / 16;
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(th, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(tw, x1); x++)
                        px[Idx(x, y)] = (x == x0 || y == y0 || x == x1 - 1 || y == y1 - 1) ? Mul(bc, 0.55f) : bc;
            }
            foreach (var f in W.Flags)                                                                    // flags: INK dot with a dark rim (owned: team mark)
            {
                int cx = (f.LocX + 8) * k / 16, cy = (f.LocY + 8) * k / 16;
                Color32 fc = f.Owner == Team.Neutral ? (Color32)OzUi.Ink : (Color32)Mark(f.Owner);
                for (int y = cy - 2; y <= cy + 2; y++)
                    for (int x = cx - 2; x <= cx + 2; x++)
                        if (x >= 0 && y >= 0 && x < tw && y < th) px[Idx(x, y)] = Mathf.Abs(x - cx) == 2 || Mathf.Abs(y - cy) == 2 ? new Color32(11, 13, 12, 255) : fc;
            }
            if (includeUnits)
                foreach (var o in W.Objects)
                {
                    if (!o.IsUnit || o.Destroyed) continue;
                    int cx = o.CenterX * k / 16, cy = o.CenterY * k / 16;
                    Color32 mark = o.Owner == Team.Neutral ? (Color32)OzUi.NeutralText : (Color32)Mark(o.Owner);
                    for (int y = cy - 1; y <= cy + 1; y++)
                        for (int x = cx - 1; x <= cx + 1; x++)
                            if (x >= 0 && y >= 0 && x < tw && y < th) px[Idx(x, y)] = mark;
                }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, name = "OpenZ_MenuMini_" + level };
            tex.SetPixels32(px); tex.Apply(true, false);
            return tex;
        }
    }

    /// <summary>
    /// Animated level preview (guide 9.14 / 9.15): a muted VideoPlayer rendering into a RenderTexture, from
    /// Resources/OpenZ/Menu/&lt;id&gt;_preview (VideoClip) or StreamingAssets/menu_&lt;id&gt;.mp4; the still
    /// Resources/OpenZ/Menu/&lt;id&gt;_preview (Texture2D) is the poster frame and the fallback (slow Ken Burns pan, see
    /// <see cref="KenBurns"/>). A cheap two-step downsample of the current frame gives the blurred level-select backdrop.
    /// 1.3 item 23: every clip may have a LIGHT twin &lt;name&gt;_1080p30 (1920x1080, 30 fps, the same frames and cuts); UI/OzMenuVideo.cs
    /// picks which copies the menu opens with, and <see cref="ToLight"/> switches a running menu to them without a stall.
    /// 1.3 item 23, the very first start: the flybys are prepared ONE AT A TIME (<see cref="PumpPrepares"/>), the level the menu shows first
    /// going first; every level shows its still until its video is ready.
    /// </summary>
    public sealed class OzLevelPreview
    {
        /// <summary>Where the poster sits in the clip; hovering a card starts the video from here.</summary>
        public const double PosterTime = 5.0;
        public readonly string Level;
        public VideoPlayer Player { get; private set; }
        public RenderTexture Frame { get; private set; }
        public Texture Poster { get; private set; }
        public bool HasVideo { get; private set; }
        public bool Prepared { get; private set; }
        public bool Failed { get; private set; }
        RenderTexture[] blurChain; long blurredFrame = -2; bool posterBlurred;
        /// <summary>select_blur_fix: sizes of the level-select blur chain. Down by halves (each bilinear blit averages 2x2, so
        /// no pixel is skipped: the old 1920 -> 320 -> 160 jump sampled one pixel in six and shimmered in big blocks), then back
        /// The chain stops at 240x135 (was 120x68), so the backdrop keeps twice the detail and stays soft.</summary>
        static readonly Vector2Int[] BlurSizes = { new Vector2Int(960, 540), new Vector2Int(480, 270), new Vector2Int(240, 135),
                                                   new Vector2Int(480, 270), new Vector2Int(960, 540) };
        float playCalledAt = -10f;
        public string LastError { get; private set; }
        float prepareStartAt = -1f;
        public string StillReason()
        {
            if (!HasVideo) return OzMenuVideo.NoH264 ? "no Linux copy of this level's flybys (Resources OpenZ/Menu/" + Level + "_preview*" + OzMenuVideo.LinuxSuffix + "; this player plays no H.264)"   // item 31
                                                     : "no video clip (Resources OpenZ/Menu/" + Level + "_preview*, StreamingAssets menu_" + Level + ".mp4)";
            if (Failed) return "VideoPlayer error: " + LastError;
            if (Player == null) return "no player";
            string c = Player.clip ? Player.clip.name + " " + Player.clip.width + "x" + Player.clip.height + " " + Player.clip.frameRate.ToString("0.##") + " fps" : Player.url;
            if (!Prepared) return $"{c}: not prepared {Time.unscaledTime - prepareStartAt:0.0} s after Prepare()";
            if (!Player.isPlaying) return $"{c}: prepared but not playing";
            if (Player.frame < 0) return $"{c}: playing but no frame decoded {Time.unscaledTime - playCalledAt:0.0} s after Play (decoder / codec / H.264 level?)";
            return $"{c}: frame {Player.frame}, within the 0.12 s grace after a seek";
        }
        readonly List<VideoClip> clips = new List<VideoClip>(); int clipIx;
        static readonly System.Random Rng = new System.Random();                                          // seeded from the clock: a new order at every start
        /// <summary>The flybys in their play order (names), for the log.</summary>
        public string ClipOrder { get { var sb = new System.Text.StringBuilder(); foreach (var c in clips) { if (sb.Length > 0) sb.Append(" > "); sb.Append(c.name); } return sb.ToString(); } }
        /// <summary>A new random play order; notFirst (the flyby that just played) never opens it.</summary>
        void Shuffle(VideoClip notFirst)
        {
            for (int i = clips.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); var c = clips[i]; clips[i] = clips[j]; clips[j] = c; }
            if (clips.Count > 1 && clips[0] == notFirst) { clips[0] = clips[clips.Count - 1]; clips[clips.Count - 1] = notFirst; }
        }
        public int ClipCount => clips.Count;
        public int ClipIndex => clipIx;

        // ---- 1.3 item 23: the light copies (UI/OzMenuVideo.cs)
        const string MenuPath = "OpenZ/Menu/";
        static readonly List<OzLevelPreview> live = new List<OzLevelPreview>();
        /// <summary>The menu's live previews (one per level), for the AUTO pick (View/OzMenuPerf.cs).</summary>
        public static List<OzLevelPreview> Live { get { live.RemoveAll(p => p == null || p.Player == null); return live; } }
        /// <summary>The clip list holds the light copies (1920x1080, 30 fps).</summary>
        public bool IsLight { get; private set; }
        bool lightLoading;
        float pausedAt = -10f;                                                                            // item 23: a just-paused player may still show (a card's live picture fades out)

        // ---- 1.3 item 31 (GitHub issue 2, Linux): a failed flyby is never prepared again, says so in one clear line, and is counted
        /// <summary>Prepare() calls of every menu flyby player since the start (the [OpenZ MenuPerf] line, the checks).</summary>
        public static int PrepareCalls { get; private set; }
        /// <summary>VideoPlayer error events of every menu flyby player since the start (counted even after the first, which alone is logged).</summary>
        public static int ErrorEvents { get; private set; }
        /// <summary>Levels whose video failed: each shows its still from then on.</summary>
        public static int FailedLevels { get; private set; }
        /// <summary>Checks: the three counts from zero.</summary>
        public static void ProofResetCounts() { PrepareCalls = ErrorEvents = FailedLevels = 0; }

        /// <summary>The one way a menu flyby is prepared: counted, and never on a player that failed (it keeps its still).</summary>
        void StartPrepare()
        {
            if (!VideoOk) return;
            Prepared = false; prepareStartAt = Time.unscaledTime; PrepareCalls++; Player.Prepare();
        }

        /// <summary>The VideoPlayer's error: this level shows its still for good. One line per level; the engine's text is not used
        /// (the 1.2 Linux log printed an Options tooltip there: "CAMERA SHAKE ON EXPLOSIONS|..."), the clip, platform and a fixed reason are.</summary>
        void OnVideoError(VideoPlayer v, string engineText)
        {
            ErrorEvents++;
            if (Failed) return;
            Failed = true; FailedLevels++;
            LastError = FixedReason(v, engineText);
            Debug.LogWarning($"[OpenZ Menu] preview video {Level}: {LastError}. The still shows for this level from now on; its video is not prepared again.");
            try { if (v) v.Stop(); } catch (Exception) { }                                                // the decoder lets go at once
        }

        string FixedReason(VideoPlayer v, string engineText)
        {
            var c = v && v.clip ? v.clip : clips.Count > 0 ? clips[Mathf.Clamp(clipIx, 0, clips.Count - 1)] : null;
            bool vp8 = c != null && c.name.EndsWith(OzMenuVideo.LinuxSuffix);
            string what = c != null ? $"{c.name} ({c.width}x{c.height}, {c.frameRate:0.##} fps, {(vp8 ? "VP8 .webm" : "H.264 .mp4")})" : "url " + (v ? Path.GetFileName(v.url) : "?");
            string why = OzMenuVideo.ForceFail ? "failed on purpose for a check (the player points at a missing file)"
                       : OzMenuVideo.NoH264 && !vp8 ? "this player has no H.264 decoder"
                       : "the video could not be opened or decoded on this PC";
            return $"the VideoPlayer could not play {what} on {Application.platform}, {SystemInfo.graphicsDeviceType}: {why} (the engine's error text is not used, {(engineText ?? "").Length} chars)";
        }

        // ---- 1.3 item 23, the very first start (a player, 30 Oct 2026:
        // Create used to Prepare() all three at
        // once. Now each player waits in this queue: the first one starts at the menu's first request (PlayFrom, the level the menu shows
        // first), the others one after the other (PumpPrepares: every menu frame from View/OzMenuPerf.cs, and at once when a prepare ends
        // or fails). Until its video is ready a level shows its still (Sharp / IsStill, as before). The clips, frames, order and cuts are
        // the same.
        static readonly List<OzLevelPreview> waiting = new List<OzLevelPreview>();
        static OzLevelPreview preparing;
        static float queueStartAt = -1f; static bool queueLogged;
        static readonly System.Text.StringBuilder queueOrder = new System.Text.StringBuilder(96);
        /// <summary>No first prepare starts while this is set: the loading screen (View/OzMenuPerf.cs), so no decoder opens while a level loads.</summary>
        public static bool QueueHold;
        /// <summary>A first prepare that has not finished after this many seconds no longer holds the next one back (it goes on by itself).</summary>
        public const float QueueTimeout = 12f;
        /// <summary>Checks only (Editor/OzV13MenuPerfCheck.cs P9): a first prepare counts as running at least this long, a slow first open
        /// emulated (0 = off, never saved).</summary>
        public static float ProofOpenDelaySec;
        bool deferred, queued, wantPlay, wantLoop, firstRunning; double wantT; float queuedAt;
        /// <summary>A first prepare still waits or runs (View/OzMenuPerf.cs holds the shader warm-up meanwhile; the checks).</summary>
        public static bool PreparesPending { get { Prune(); return waiting.Count > 0 || preparing != null; } }
        /// <summary>Checks: the most first prepares that ran at the same time in this menu (1 = one at a time).</summary>
        public static int ProofMaxAtOnce { get; private set; }
        /// <summary>Checks and the log: the levels in the order their first prepare started, each with its wait ("L02 0.00 s > L16 0.21 s").</summary>
        public static string ProofOrder => queueOrder.ToString();
        /// <summary>Checks: seconds from the first first-prepare to the last one ready (-1 = not yet).</summary>
        public static float ProofAllReadySec { get; private set; } = -1f;

        static void Prune()
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                var w = waiting[i];
                if (w == null || w.Player == null || !w.VideoOk) { if (w != null) w.queued = false; waiting.RemoveAt(i); }   // the menu went, or the flyby failed: it keeps its still
            }
            if (preparing != null && (preparing.Player == null || !preparing.VideoOk || Time.unscaledTime - preparing.prepareStartAt > QueueTimeout
                                      || (preparing.Prepared && Time.unscaledTime - preparing.prepareStartAt >= ProofOpenDelaySec)))
            { preparing.firstRunning = false; preparing = null; }
        }

        /// <summary>Starts the next waiting first prepare when none runs (never while <see cref="QueueHold"/>). Called every menu frame by
        /// View/OzMenuPerf.cs, when a prepare ends or fails, and when the menu asks for a flyby.</summary>
        public static void PumpPrepares()
        {
            Prune();
            if (preparing == null && waiting.Count == 0)
            {
                if (queueStartAt >= 0f && !queueLogged)
                {
                    queueLogged = true; ProofAllReadySec = Time.unscaledTime - queueStartAt;
                    Debug.Log($"[OpenZ Menu] flybys prepared one at a time, the level shown first going first: {queueOrder}; all ready {ProofAllReadySec:0.00} s after the first one started (each level showed its still until then)");
                }
                return;
            }
            if (preparing != null || QueueHold || waiting.Count == 0) return;
            var p = waiting[0]; waiting.RemoveAt(0); p.queued = false;
            p.BeginFirstPrepare();
        }

        /// <summary>This player's first prepare (the clip the menu opens with); a play the menu asked for meanwhile starts with it.</summary>
        void BeginFirstPrepare()
        {
            int atOnce = 1;
            for (int i = 0; i < live.Count; i++) { var q = live[i]; if (q != null && q != this && q.firstRunning && !q.Prepared && q.VideoOk) atOnce++; }
            if (atOnce > ProofMaxAtOnce) ProofMaxAtOnce = atOnce;
            preparing = this; firstRunning = true;
            if (queueStartAt < 0f) queueStartAt = Time.unscaledTime;
            queueOrder.Append(queueOrder.Length > 0 ? " > " : "").Append(Level).Append(' ').Append((Time.unscaledTime - queuedAt).ToString("0.00")).Append(" s");
            StartPrepare();                                                                                    // item 31: counted, never on a failed player (item 23's first prepare, one at a time)
            if (wantPlay) { wantPlay = false; PlayFrom(wantT, wantLoop); }
        }

        /// <summary>A flyby by name: its light twin when the light copies are wanted and it exists, else the full clip (null = none).</summary>
        static VideoClip LoadClip(string name, bool light)
        {
            if (OzMenuVideo.LinuxSet)                                                                         // item 31: Linux opens only its VP8 copies; an H.264 clip is never opened there
            {
                var x = Resources.Load<VideoClip>(MenuPath + name + OzMenuVideo.LinuxSuffix);
                if (x != null || OzMenuVideo.NoH264) return x;
            }
            if (light) { var l = Resources.Load<VideoClip>(MenuPath + name + OzMenuVideo.Suffix); if (l != null) return l; }
            return Resources.Load<VideoClip>(MenuPath + name);
        }

        /// <summary>
        /// (Resources.LoadAsync); the list then holds them; a hidden player takes its copy at its next <see cref="TakePending"/> (it shows
        /// nothing meanwhile), the player on screen keeps its clip and takes the copy at its next cut (<see cref="NextClip"/>), so no
        /// frame waits and the picture never jumps. A missing copy keeps its full clip.</summary>
        public void ToLight()
        {
            if (IsLight || lightLoading || !VideoOk || clips.Count == 0) return;
            lightLoading = true;
            var names = new string[clips.Count]; var got = new VideoClip[clips.Count]; int left = clips.Count;
            for (int i = 0; i < names.Length; i++) names[i] = clips[i].name;
            for (int i = 0; i < names.Length; i++)
            {
                int k = i; var req = Resources.LoadAsync<VideoClip>(MenuPath + names[i] + OzMenuVideo.Suffix);
                req.completed += _ => { got[k] = req.asset as VideoClip; if (--left == 0) LightLoaded(names, got); };
            }
        }

        void LightLoaded(string[] names, VideoClip[] got)
        {
            lightLoading = false;
            if (Player == null) return;                                                                       // the menu went meanwhile
            int n = 0;
            for (int j = 0; j < clips.Count; j++)                                                             // by name: the order may have been shuffled meanwhile
            {
                int i = Array.IndexOf(names, clips[j].name);
                if (i >= 0 && got[i] != null) { clips[j] = got[i]; n++; }
            }
            if (n == 0) { Debug.LogWarning($"[OpenZ Menu] flyby {Level}: no light copy found (Resources {MenuPath}<clip>{OzMenuVideo.Suffix}); the full flybys stay"); return; }
            IsLight = true;
            Debug.Log($"[OpenZ Menu] flyby {Level}: light copies ready ({n}/{clips.Count}: {ClipOrder}); {(Player.isPlaying ? "the flyby on screen keeps its clip until its next cut" : "the hidden player takes its copy now")}");
        }

        /// <summary>A hidden (not playing) player whose clip is not the list's current one, because a light copy waits, takes it now:
        /// Stop, the copy, the frame's size, Prepare. Called by the menu's View/OzMenuPerf.cs in the main view and the level selection
        /// only (never while a level loads).</summary>
        public void TakePending()
        {
            if (!IsLight || !VideoOk || clips.Count == 0 || Player.isPlaying || Player.clip == clips[clipIx]) return;
            if (Time.unscaledTime - playCalledAt < 1f || Time.unscaledTime - pausedAt < 1f) return;          // the menu just asked it to play (it may still be starting), or just paused it (its picture may still fade out): never in its way
            if (queued) { Player.clip = clips[clipIx]; SizeFrame(Player.clip); ApplyAudio(); return; }       // item 23 (first start): its first prepare has not started: it prepares the copy at its turn
            Player.Stop(); Player.clip = clips[clipIx]; SizeFrame(Player.clip); ApplyAudio(); StartPrepare();   // item 31: StartPrepare (counted)
            Debug.Log($"[OpenZ Menu] flyby {Level}: hidden player takes {Player.clip.name} ({Player.clip.width}x{Player.clip.height}, {Player.clip.frameRate:0.##} fps)");
        }

        /// <summary>The frame texture follows the clip's size (1920x1080 for a light copy): the same RenderTexture object, so every
        /// picture that shows it keeps it. Only on a hidden player.</summary>
        void SizeFrame(VideoClip c)
        {
            if (Frame == null || c == null || c.width == 0 || c.height == 0 || (Frame.width == (int)c.width && Frame.height == (int)c.height)) return;
            Frame.Release(); Frame.width = (int)c.width; Frame.height = (int)c.height; Frame.Create();
        }

        /// <summary>Loads the level's next flyby into the (hidden, paused) player, prepared for its next turn in the menu.</summary>
        public void NextClip()
        {
            if (!VideoOk || clips.Count < 2) { Pause(); return; }
            if (++clipIx >= clips.Count) { Shuffle(clips[clips.Count - 1]); clipIx = 0; }   // a new random order every round, never the same flyby twice in a row
            Player.Stop(); Player.clip = clips[clipIx]; SizeFrame(Player.clip); ApplyAudio(); StartPrepare();   // item 23: SizeFrame (a light copy is 1920x1080); item 31: StartPrepare (counted, never on a failed player)
            Debug.Log($"[OpenZ Menu] flyby {Level} next: #{clipIx + 1}/{clips.Count} {clips[clipIx].name} ({clips[clipIx].width}x{clips[clipIx].height})");
        }

        /// <summary>How loud a flyby's own sound track plays in the menu (1 = the clip's full level), before the effects volume setting.</summary>
        public const float MenuFlybyVolume = 0.22f;
        /// <summary>The flybys are heard quietly in the menu: the clip's own sound track (battle and world sounds) at a low level,
        /// scaled by the effects volume setting. Silent when the clip has no sound track. Call before Prepare.</summary>
        readonly bool silent;
        void ApplyAudio()
        {
            if (!Player) return;
            var c = Player.clip;
            if (silent || c == null || c.audioTrackCount == 0) { Player.audioOutputMode = VideoAudioOutputMode.None; return; }
            Player.audioOutputMode = VideoAudioOutputMode.Direct;
            Player.controlledAudioTrackCount = 1;
            Player.EnableAudioTrack(0, true);
            Player.SetDirectAudioVolume(0, Mathf.Clamp01(MenuFlybyVolume * OzAudio.EffectsLin(PlayerPrefs.GetFloat("oz_vol_effects", 1f))));
        }

        OzLevelPreview(string level, bool silent) { Level = level; this.silent = silent; }

        public static OzLevelPreview Create(Transform host, string level, bool mute = false)
        {
            bool aiMap = OzAiMapCatalog.Find(level) != null;
            var p = new OzLevelPreview(level, aiMap || mute);
            Prune(); if (waiting.Count == 0 && preparing == null) { queueStartAt = -1f; queueLogged = false; queueOrder.Clear(); ProofMaxAtOnce = 0; ProofAllReadySec = -1f; }   // item 23: a new menu, a new queue
            // the level card / select still = the first of the level's chosen loading stills (L02: sunrise canyon red fort, L17: car park
            // war machines at dusk), which replaced the old posters; the old poster is the fallback
            // 1.5: a level whose card has its own still (Menu/<id>_card, level 18: a frame of its blue briefing's flight) shows that one; its loading picture stays
            p.Poster = Resources.Load<Texture2D>($"OpenZ/Menu/{level}_card") ?? Resources.Load<Texture2D>($"OpenZ/Loading/{level}_loading_1") ??Resources.Load<Texture2D>($"OpenZ/Menu/{level}_preview") ?? Resources.Load<Texture2D>($"OpenZ/Menu/{level}_poster");
            if (aiMap) p.Poster = Resources.Load<Texture2D>($"OpenZ/AiMaps/Posters/{level}_poster");
            bool light = OzMenuVideo.UseLight;                                                                // item 23: the light twins on a PC that needs them (a missing twin = its full clip)
            var clip = aiMap ? Resources.Load<VideoClip>($"OpenZ/AiMaps/Videos/{level}_preview") : LoadClip($"{level}_preview", light);
            if (clip != null) { p.clips.Add(clip); for (int k = 2; k <= 9; k++) { var more = LoadClip($"{level}_preview_{k}", light); if (more == null) break; p.clips.Add(more); } p.Shuffle(null); clip = p.clips[0]; }
            p.IsLight = clip != null && (clip.name.EndsWith(OzMenuVideo.Suffix) || clip.name.EndsWith(OzMenuVideo.LinuxSuffix));   // item 31: the Linux copies are light too (ToLight then does nothing)
            string url = null;
            if (clip == null && !OzMenuVideo.NoH264)                                                          // item 31: no .mp4 on a player that cannot decode it
            {
                var sa = Path.Combine(Application.streamingAssetsPath, $"menu_{level}.mp4");
                if (File.Exists(sa)) url = sa;
            }
            if (clip != null || url != null)
            {
                var go = new GameObject("Preview_" + level);
                go.transform.SetParent(host, false);
                var vp = go.AddComponent<VideoPlayer>();
                vp.playOnAwake = false; vp.waitForFirstFrame = true; vp.skipOnDrop = true; vp.isLooping = true;
                vp.audioOutputMode = VideoAudioOutputMode.None;                                          // previews are muted
                vp.renderMode = VideoRenderMode.RenderTexture; vp.aspectRatio = VideoAspectRatio.FitOutside;
                int w = clip != null && clip.width > 0 ? (int)clip.width : 1280, h = clip != null && clip.height > 0 ? (int)clip.height : 720;
                p.Frame = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "OpenZ_Preview_" + level, useMipMap = false, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                p.Frame.Create();
                vp.targetTexture = p.Frame;
                if (clip != null) { vp.source = VideoSource.VideoClip; vp.clip = clip; }
                else { vp.source = VideoSource.Url; vp.url = url; }
                if (OzMenuVideo.ForceFail)                                                                    // item 31: the check of the still path: the real error event, from a missing file
                {
                    vp.source = VideoSource.Url; vp.url = Path.Combine(Application.temporaryCachePath, "openz_no_such_flyby_" + level + ".webm");
                    Debug.Log($"[OpenZ Menu] preview {level}: forced to fail for a check (url {vp.url})");
                }
                vp.prepareCompleted += v => { p.Prepared = true; Debug.Log($"[OpenZ Menu] preview {level}: prepared {(v.clip ? v.clip.name : v.url)} in {Time.unscaledTime - p.prepareStartAt:0.00} s ({v.width}x{v.height}, {v.frameRate:0.##} fps, {v.length:0.0} s)"); OzBootTime.Once("flyby " + level, "menu flyby " + level + " prepared"); PumpPrepares(); };
                vp.errorReceived += (v, msg) => p.OnVideoError(v, msg);                                          // item 31: one fixed line, never prepared again
                vp.errorReceived += (v, msg) => PumpPrepares();                                                // item 23: a failed flyby lets the next one prepare
                p.Player = vp; p.HasVideo = true; p.ApplyAudio();
                p.deferred = aiMap;
                if (!p.deferred) { p.queued = true; p.queuedAt = Time.unscaledTime; waiting.Add(p); }                                // item 23: prepared one at a time (PumpPrepares), the level the menu shows first going first
                live.Add(p);                                                                                  // item 23: the AUTO pick sees it
            }
            Debug.Log($"[OpenZ Menu] preview {level}: video {(clip != null ? "Resources clip x" + p.clips.Count + " in the order " + p.ClipOrder + " (" + clip.width + "x" + clip.height + (p.IsLight ? ", light copies: " + OzMenuVideo.Why : "") + ")" : url != null ? "StreamingAssets" : "none")}, still {(p.Poster != null ? p.Poster.width + "x" + p.Poster.height : "none")}");
            return p;
        }

        /// <summary>The video can be shown (prepared, no error).</summary>
        public bool VideoOk => HasVideo && !Failed && Player != null;
        public bool IsPlaying => VideoOk && Player.isPlaying;
        /// <summary>A real frame of the running clip is in <see cref="Frame"/> (a short grace after Play for the first decode).</summary>
        public bool ShowingVideo => IsPlaying && Player.frame >= 0 && Time.unscaledTime - playCalledAt > 0.12f;
        public double Length => VideoOk && Player.length > 0 ? Player.length : VideoOk && Player.clip != null ? Player.clip.length : 0;
        public float Progress => Length > 0 ? Mathf.Clamp01((float)(Player.time / Length)) : 0f;
        public double TimeLeft => Length > 0 ? Length - Player.time : double.MaxValue;
        /// <summary>The texture to show sharp: the video when it has a frame, else the still (null = neither).</summary>
        public Texture Sharp => StallGuard() && ShowingVideo ? Frame : Poster != null ? Poster : VideoOk && Player.frame >= 0 ? Frame : null;
        public bool IsStill => !ShowingVideo;

        public void PlayFrom(double t, bool loop)
        {
            if (!VideoOk) return;
            if (deferred) { deferred = false; queued = true; queuedAt = Time.unscaledTime; }
            if (queued)                                                                                       // item 23 (first start): its first prepare still waits
            {
                waiting.Remove(this); waiting.Insert(0, this);                                                // the flyby the menu wants now goes first
                wantPlay = true; wantT = t; wantLoop = loop; Player.isLooping = loop;
                PumpPrepares();                                                                               // at once when no other first prepare runs, else right after it
                return;
            }
            Player.isLooping = loop;
            if (t >= 0 && Player.canSetTime) Player.time = Length > 0 ? Math.Min(t, Math.Max(0, Length - 0.5)) : t;
            Player.Play();
            if (t >= 0) playCalledAt = Time.unscaledTime;                                                   // a seek may show one stale frame; a resume does not
        }

        public void Pause() { wantPlay = false; if (VideoOk && Player.isPlaying) { Player.Pause(); pausedAt = Time.unscaledTime; } }   // item 23: pausedAt for TakePending; a waiting first prepare no longer starts playing

        /// <summary>Blurred copy (select_blur_fix: halving chain to 240x135 and back up to 960x540, see BlurSizes): the level-select backdrop. Refreshes only when a new frame
        /// arrived; the still is blurred once.</summary>
        long stallFrame = -1; float stallFor, stallPlayAt = -10f; int stallTick = -1;                     // 1.4: the wait is summed from menu frames, never read from the clock
        /// <summary>A decoder that stops handing out frames while the engine still calls it playing (Media Foundation's "sample allocator is empty") would hold a frozen picture: after 2.5 s on screen without a new frame the clip starts again. Always true.
        /// 1.4: the menu calls it once a frame for the flyby on screen (UI/OpenZMainMenu.cs TickBackground). The wait adds each menu frame's time, capped at 0.1 s:
        /// one long frame (a dragged window, a warm-up hitch) adds 0.1 s at most, time off screen adds nothing, and a seek or start by the menu (PlayFrom sets
        /// playCalledAt) begins a new wait (the engine keeps the old frame number until a seek lands). A real freeze still restarts after 2.5 s of menu frames.</summary>
        public bool StallGuard()
        {
            if (stallTick == Time.frameCount) return true;                                                  // 1.4: once a menu frame (the 1.3 editor check's Sharp calls it too)
            stallTick = Time.frameCount;
            if (playCalledAt != stallPlayAt) { stallPlayAt = playCalledAt; stallFrame = -1; }              // 1.4: a new seek or start by the menu: a new wait
            if (!IsPlaying || Player.frame < 0) { stallFrame = -1; stallFor = 0f; return true; }
            if (Player.frame != stallFrame) { stallFrame = Player.frame; stallFor = 0f; return true; }
            stallFor += Mathf.Min(Time.unscaledDeltaTime, 0.1f);                                            // 1.4: one long frame counts 0.1 s at most
            if (stallFor < 2.5f) return true;
            stallFor = 0f; stallFrame = -1; Debug.Log($"[OpenZ Menu] preview video {Level}: no new frame for 2.5 s, started again");
            try { Player.Stop(); Player.Play(); playCalledAt = Time.unscaledTime; } catch (Exception) { }
            return true;
        }

        public Texture Blurred
        {
            get
            {
                var src = ShowingVideo ? (Texture)Frame : Poster;
                if (src == null) return null;
                if (blurChain == null)
                {
                    blurChain = new RenderTexture[BlurSizes.Length];
                    for (int k = 0; k < BlurSizes.Length; k++)
                        blurChain[k] = new RenderTexture(BlurSizes[k].x, BlurSizes[k].y, 0, RenderTextureFormat.ARGB32) { name = "OpenZ_PreviewBlur" + k + "_" + Level, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                }
                bool video = ShowingVideo;
                long f = video ? Player.frame : -1;
                if ((video && f != blurredFrame) || (!video && !posterBlurred))
                {
                    Graphics.Blit(src, blurChain[0]);
                    for (int k = 1; k < blurChain.Length; k++) Graphics.Blit(blurChain[k - 1], blurChain[k]);
                    blurredFrame = f; posterBlurred = !video;
                    if (video) posterBlurred = false;
                }
                return blurChain[blurChain.Length - 1];
            }
        }

        public void Release()
        {
            live.Remove(this); waiting.Remove(this); queued = false; if (preparing == this) preparing = null;   // item 23
            if (Player)
            {
                Player.Stop();
                Player.targetTexture = null;
                UnityEngine.Object.Destroy(Player.gameObject);
                Player = null;
            }
            if (Frame) { Frame.Release(); UnityEngine.Object.Destroy(Frame); }
            if (blurChain != null) { foreach (var b in blurChain) if (b) { b.Release(); UnityEngine.Object.Destroy(b); } blurChain = null; }
        }

        /// <summary>uvRect that covers a rect of aspect `rect` with a texture of aspect `tex` (crop, never stretch).</summary>
        public static Rect Cover(float tex, float rect)
        {
            if (tex <= 0f || rect <= 0f) return new Rect(0, 0, 1, 1);
            if (tex > rect) { float w = rect / tex; return new Rect((1f - w) * 0.5f, 0, w, 1); }
            float h = tex / rect; return new Rect(0, (1f - h) * 0.5f, 1, h);
        }

        /// <summary>Slow Ken Burns move inside a cover rect: zoom 1.05..1.13 and a drifting centre (periods 28 / 37 / 41 s,
        /// unscaled time), so a still reads as a live camera. Reduced motion: static 1.05 crop.</summary>
        public static Rect KenBurns(Rect cover, float t, float seed)
        {
            float z = OzMotion.Reduced ? 1.05f : 1.09f + 0.04f * Mathf.Sin(t * 2f * Mathf.PI / 28f + seed);
            float w = cover.width / z, h = cover.height / z;
            float cx = cover.center.x + (OzMotion.Reduced ? 0f : (cover.width - w) * 0.45f * Mathf.Sin(t * 2f * Mathf.PI / 37f + seed * 1.7f));
            float cy = cover.center.y + (OzMotion.Reduced ? 0f : (cover.height - h) * 0.45f * Mathf.Cos(t * 2f * Mathf.PI / 41f + seed * 0.6f));
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR

        // ---- 1.5 kit mac_perf_1_5b (GitHub issue 4; Mac player only, called by View/OzMacPerf.cs): a flyby that plays without one decoded
        // picture (the reporter's iMac: "playing but no frame decoded 3.6 s after Play") gives up for this run: its level shows its still as
        // after a VideoPlayer error (the menu moves on by the still's timer) and its decoder stops. Windows, Linux, editor: not built.
        /// <summary>Seconds this flyby has played without a decoded picture (View/OzMacPerf.cs counts them, each menu frame capped).</summary>
        internal float MacNoFrameSec;

        /// <summary>This flyby shows its still from now on (as after a VideoPlayer error, without one); the next first prepare may start.</summary>
        internal void MacGiveUp(string why)
        {
            if (Failed) return;
            Failed = true; FailedLevels++; LastError = why; MacNoFrameSec = 0f;
            try { if (Player) Player.Stop(); } catch (Exception) { }                                   // the decoder lets go at once
            PumpPrepares();
        }
#endif
    }
}
