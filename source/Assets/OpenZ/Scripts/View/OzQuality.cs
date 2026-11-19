// OpenZ (GPL-3.0)
// Graphics quality presets: lighter settings for older PCs, HIGH keeps the full look. Two Options rows:
//  * GRAPHICS QUALITY: HIGH (default) / MEDIUM / LOW (oz_gfx_quality, saved, live). HIGH = Assets/Settings/PC_RPAsset exactly as before
//    (nothing changes). MEDIUM / LOW are their own URP asset copies (Resources/OpenZ/Quality, made by OpenZ > Perf > Make quality presets;
//    they share PC_Renderer, so Forward+, decals, depth + opaque textures for the water, SSAO and the post stack all stay) switched in
//    with QualitySettings.renderPipeline; the shared PC_RPAsset is never written. What they lower: MSAA (2x / off; SMAA on the camera
//    stays), main-light shadows (2048 / 1024, 2 cascades, 180 / 120 m), the additional-light shadow atlas (2048 / 1024), soft-shadow
//    quality (medium / low) and the atmosphere ray-march (12 / 9 steps instead of 16; the per-pixel jitter hides the step count).
//    Render scale stays 1.0 on every preset (full quality, never downscaled by a preset).
//    1.5 kit issue8_resolution_low (GitHub issue 8): LOW draws the 3D picture within a pixel budget on big screens (View/OzLowScale.cs; on a Mac View/OzMacPerf.cs).
//    Gameplay never changes. NIGHT LIGHT SHADOWS stays its own row (the one knob for night-light shadows).
//    is on, OzLitePresets.cs lowers numbers and switches the game already had (sun shadow reach and steps, night lights,
//    night-light shadows, rain with its depth layers; on LOW also the lamps' glow and beams; the table is OzLiteTable.cs) and
//    puts them back on HIGH. On HIGH none of it runs: Apply below reads one flag.
//  * FPS COUNTER: OFF (default) / ON / DETAILED (oz_fps_counter 0 / 1 / 2): a small HUD chip (top right) with fps, ms and the preset;
//    hidden in recorded takes (-take, CaptureKit cine). A real uGUI element, not OnGUI. Version 1.3 item 25: DETAILED shows the processor's
//    time apart: MAIN (the game's main thread), RENDER (the drawing thread), GPU and RULES (the rule ticks of the frame).
//    The per-minute perf line of Player.log: View/OzPerfLine.cs.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OpenZ
{
    public static class OzQuality
    {
        public const string Pref = "oz_gfx_quality";
        public const string FpsPref = "oz_fps_counter";
        public const string Label = "GRAPHICS QUALITY", FpsLabel = "FPS COUNTER";
        // version 1.1: one clause per thing MEDIUM / LOW give up (a cut that is taken out of OzLiteTable.cs takes its clause with it)
        public const string Tip = "GRAPHICS QUALITY|High (default): the full look. Medium and Low are for slower PCs: softer shadows that end sooner when zoomed out, less anti-aliasing (2x / off; edge smoothing stays), lighter fog and ambient occlusion (half / off), fewer night lights and their shadows, thinner rain. Low gives up the most: lamps lose glow and beams, water its sun and fire glints, and big screens get a lighter 3D view (text stays sharp). Units and rules never change.|";
        public const string FpsTip = "FPS COUNTER|On shows the frame rate, the time of one frame for the processor and for the graphics chip, and the graphics preset in the top right corner. Detailed also splits the processor's time into the game's main work, the drawing work and the game's rules. Off is the default. Hidden in recordings.|";
        /// <summary>1.3 item 25: the three values of FPS COUNTER (oz_fps_counter 0 / 1 / 2).</summary>
        public static readonly string[] FpsNames = { "OFF", "ON", "DETAILED" };
        public static readonly string[] Names = { "HIGH", "MEDIUM", "LOW" };
        /// <summary>Atmosphere ray-march steps per preset (AtmosphereFullscreen _AtmoSteps; HIGH = the shader's 16).</summary>
        public static readonly float[] AtmoSteps = { 16f, 12f, 9f };
        public const string ResDir = "OpenZ/Quality/", MediumAsset = "OZ_URP_Medium", LowAsset = "OZ_URP_Low";
        static readonly int IdSteps = Shader.PropertyToID("_AtmoSteps");
        static int level = -1;
        static RenderPipelineAsset original; static bool originalSaved;

        public static int Level
        {
            get
            {
                if (level < 0)
                {
                    level = Mathf.Clamp(PlayerPrefs.GetInt(Pref, 0), 0, 2);
                    int dev = DevQuality(); if (dev >= 0) level = dev;
                }
                return level;
            }
            set
            {
                level = Mathf.Clamp(value, 0, 2); PlayerPrefs.SetInt(Pref, level); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] graphics quality: {Names[level]} ({Pref} {level}, saved, live)");
                Apply();
            }
        }
        public static string Name => Names[Level];

        // (or 0 / 1 / 2) picks the preset for this run; -ozrenderscale 0.5..1 measures a lower render scale (the shipped presets stay 1.0).
        static string DevVal(string a)
        {
            if (Application.isEditor || OzRelease.IsRelease) return null;
            var v = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < v.Length - 1; i++) if (v[i].Equals(a, System.StringComparison.OrdinalIgnoreCase)) return v[i + 1];
            return null;
        }
        static int DevQuality()
        {
            var q = DevVal("-ozquality"); if (string.IsNullOrEmpty(q)) return -1;
            int i = System.Array.IndexOf(Names, q.ToUpperInvariant()); if (i >= 0) return i;
            return int.TryParse(q, out i) && i >= 0 && i <= 2 ? i : -1;
        }
        static float DevRenderScale()
        {
            var r = DevVal("-ozrenderscale");
            return !string.IsNullOrEmpty(r) && float.TryParse(r, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? Mathf.Clamp(f, 0.5f, 1f) : -1f;
        }
        public static void Step(int dir) => Level = (Level + (dir < 0 ? 2 : 1)) % 3;

        /// <summary>FPS COUNTER: 0 OFF (default), 1 ON, 2 DETAILED (1.3 item 25). Any other saved number reads as OFF.</summary>
        public static int FpsMode
        {
            get { int v = PlayerPrefs.GetInt(FpsPref, 0); return v == 1 || v == 2 ? v : 0; }
            set
            {
                int v = value == 1 || value == 2 ? value : 0;
                PlayerPrefs.SetInt(FpsPref, v); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] fps counter: {FpsNames[v]} ({FpsPref} {v}, saved, live)");
            }
        }
        public static string FpsName => FpsNames[FpsMode];
        /// <summary>The row's arrows: OFF -> ON -> DETAILED -> OFF (dir &lt; 0 the other way).</summary>
        public static void FpsStep(int dir) => FpsMode = (FpsMode + (dir < 0 ? 2 : 1)) % 3;
        public static bool FpsDetailed => FpsMode == 2;
        /// <summary>The chip is drawn (ON or DETAILED). Setting it: true = ON, false = OFF (the checks' switch, as before).</summary>
        public static bool FpsOn
        {
            get => FpsMode != 0;
            set => FpsMode = value ? 1 : 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Apply();
            Application.quitting -= Restore; Application.quitting += Restore;                               // the editor: Play stop puts the project asset back
            var go = new GameObject("OzFpsChip"); Object.DontDestroyOnLoad(go); go.AddComponent<OzFpsChip>();
            go.AddComponent<OzPerfLine>();                                                                  // 1.3 item 25: the per-minute perf line of Player.log (log only, View/OzPerfLine.cs)
        }

        /// <summary>Switches the active URP asset for the preset (HIGH = the project's own asset, untouched) and the fog step count.</summary>
        public static void Apply()
        {
            if (!originalSaved)
            {
                original = QualitySettings.renderPipeline; originalSaved = true;
                if (original && original.name.StartsWith("OZ_URP_")) original = GraphicsSettings.defaultRenderPipeline;   // a killed editor session left a preset in
            }
            int l = Level;
            RenderPipelineAsset want = original;
            if (l > 0)
            {
                var a = Resources.Load<UniversalRenderPipelineAsset>(ResDir + (l == 1 ? MediumAsset : LowAsset));
                if (a) want = a;
                else Debug.LogWarning($"[OpenZ Quality] {Names[l]}: Resources/{ResDir}{(l == 1 ? MediumAsset : LowAsset)} missing (run OpenZ > Perf > Make quality presets): HIGH stays");
            }
            if (QualitySettings.renderPipeline != want) QualitySettings.renderPipeline = want;
            Shader.SetGlobalFloat(IdSteps, AtmoSteps[l]);
            var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float drs = DevRenderScale();
            if (cur && drs > 0f && Mathf.Abs(cur.renderScale - drs) > 0.001f) { cur.renderScale = drs; Debug.Log($"[OpenZ Quality] dev run: render scale {drs:0.##} (session only, -ozrenderscale)"); }
            Debug.Log($"[OpenZ Quality] {Names[l]}: {(cur ? cur.name : "?")}, MSAA {(cur ? cur.msaaSampleCount : 0)}x, shadows {(cur ? cur.mainLightShadowmapResolution : 0)} / {(cur ? cur.shadowCascadeCount : 0)} cascades / {(cur ? cur.shadowDistance : 0):0} m, fog steps {AtmoSteps[l]:0}, render scale {(cur ? cur.renderScale : 1f):0.##}");
            if (l > 0 || OzLite.Active) OzLite.Apply(l);                                                 // version 1.1, the lighter presets (OzLitePresets.cs): on HIGH this reads one flag and calls nothing
            OzSunSteps.Apply();                                                                            // v1.3 SUN SHADOW STEPS (Options, default 4 = nothing is changed; View/OzWeakPcOptions.cs)
            OzLightRows.ApplyAo();                                                                         // 1.3 item 40 AMBIENT OCCLUSION (Options, default ON = nothing is changed; View/OzLightRows.cs)
        }

        /// <summary>Never leave a MEDIUM / LOW asset in the editor's quality level after Play (the shared settings stay HIGH).</summary>
        static void Restore()
        {
            if (originalSaved && QualitySettings.renderPipeline != original) QualitySettings.renderPipeline = original;
        }

        public static string Describe() => $"graphics {Name}, fps counter {FpsName.ToLowerInvariant()}";
    }

    /// <summary>FRAME RATE LIMIT (Options, GRAPHICS tab): 30, 40, 60 or UNLIMITED (VSYNC), 60 unless the player chose another (oz_fps_cap,
    /// saved, applied at once; 40 = 1.3 item 25 step h). 30, 40 and 60 are the game's own cap (GameBoot.CapFps) with vsync off: the heat guard and a recording
    /// UNLIMITED (VSYNC) has no cap of its own (GameBoot.CapFps -1) and turns vsync on, so the game runs at the screen's own rate; a
    /// step back to 60 or 30 turns vsync off again. The heat guard still holds 30 fps at 90 C on it (vsync off while it holds, so
    /// the 30 is not ignored) and gives it back below 85 C. The rules do not change with any of them (fixed 10 ms rule ticks,
    /// ZGame.Advance).</summary>
    public static class OzFpsCap
    {
        public const string CapPref = "oz_fps_cap", Label = "FRAME RATE LIMIT";                     // was Pref: OzQuality above has its own Pref (the defaults check reads one key constant per name in a file)
        /// <summary>The old name of the key (the editor checks read it).</summary>
        public static string Pref => CapPref;
        /// <summary>1.3 item 5: the saved value of UNLIMITED (VSYNC) (a 1.2 game reads it as 60: no harm going back) and its name on the row.</summary>
        public const int VsyncValue = 0;
        public const string VsyncName = "UNLIMITED (VSYNC)";
        public const string Tip = "FRAME RATE LIMIT|60 is the default. 40 and 30 run more evenly on a slower computer and keep it cooler. UNLIMITED (VSYNC) has no limit of its own: the game follows your screen's refresh rate (for example 120 or 144 frames a second), smoother on a fast screen, but the computer works harder and gets warmer. The game's speed and rules are the same on every choice.|";
        /// <summary>The player's limit: 60 with no saved value; -1 = no limit of its own (UNLIMITED (VSYNC): the screen paces the frames).</summary>
        public static int Saved { get { int v = PlayerPrefs.GetInt(CapPref, 60); return v == 30 || v == 40 ? v : v == VsyncValue ? -1 : 60; } }
        /// <summary>1.3 item 5: the player chose UNLIMITED (VSYNC).</summary>
        public static bool Vsync => Saved < 0;
        public static string Name => Vsync ? VsyncName : Saved == 30 ? "30" : Saved == 40 ? "40" : "60";
        /// <summary>The row's values in order: 30, 40, 60, UNLIMITED (VSYNC); the right arrow goes up, the left arrow down, both wrap round.</summary>
        static readonly int[] Order = { 30, 40, 60, VsyncValue };
        public static void Step(int dir)
        {
            int at = Vsync ? 3 : Saved == 30 ? 0 : Saved == 40 ? 1 : 2;
            Set(Order[(at + (dir < 0 ? Order.Length - 1 : 1)) % Order.Length]);
        }
        public static void Set(int v)
        {
            v = v == 30 || v == 40 ? v : v == VsyncValue ? VsyncValue : 60;
            PlayerPrefs.SetInt(CapPref, v); PlayerPrefs.Save();
            string name = v == VsyncValue ? VsyncName : v.ToString();
            if (GameBoot.CapFps > 60) { Debug.Log($"[OpenZ Options] frame rate limit: {name} ({CapPref} {v}, saved; this capture session keeps its own cap of {GameBoot.CapFps})"); return; }
            GameBoot.CapFps = v == VsyncValue ? -1 : v;
            int held = HeatGuardRuntime.HeldFps;                                                             // the heat guard holds a lower cap right now: it gives the player's value back when it lets go
            if (!GameBoot.TestMode) ApplyLimit(held);                                                        // test mode keeps its own 30 (GameBoot.Init)
            Debug.Log($"[OpenZ Options] frame rate limit: {name} ({CapPref} {v}, saved, live; target {Application.targetFrameRate}, vsync {QualitySettings.vSyncCount}{(held > 0 ? ", the heat guard holds " + held + " for now" : "")})");
        }

        /// <summary>1.3 item 5: puts the frame limit in force (GameBoot at the start, the row, the heat guard). held > 0 = the heat guard's
        /// lower cap: vsync off (with vsync on Unity ignores the frame-rate target) and that cap (never above the game's own cap). Else
        /// the game's own cap GameBoot.CapFps with vsync off, or, on UNLIMITED (VSYNC) (CapFps -1 and the player's choice), no target
        /// and vsync on (the heat guard calls it as it did set the target before, also in test mode).</summary>
        public static void ApplyLimit(int held)
        {
            int cap = GameBoot.CapFps;
            if (held > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = cap > 0 ? Mathf.Min(held, cap) : held;
                return;
            }
            bool vs = cap <= 0 && Vsync;
            QualitySettings.vSyncCount = vs ? 1 : 0;
            Application.targetFrameRate = vs ? -1 : cap;
        }
    }

    /// <summary>The FPS COUNTER chip: fps (0.5 s average), the frame's time split into the processor's and the graphics chip's part
    /// (the engine's frame timing figures; a figure the machine does not give is left out) and the preset, top right, over the HUD;
    /// hidden in takes. With the counter off nothing is drawn. DETAILED (1.3 item 25): MAIN, RENDER, GPU and RULES ms apart, on a wider
    /// chip (DetailW). The figures come from OzFrameTimes (View/OzPerfLine.cs), captured once a frame for the chip and the perf line.</summary>
    public sealed class OzFpsChip : MonoBehaviour
    {
        /// <summary>Top-right anchored (canvas units from the right edge / top), under the MENU button.</summary>
        public static float Right = 12f, Y = 64f, W = 268f, H = 22f, DetailW = 430f;
        Canvas canvas; OzText text; float acc; int frames; string shown = "";
        double cpuAcc, gpuAcc; int cpuN, gpuN;
        double mainAcc, renderAcc, rulesAcc; int mainN, renderN; bool builtDetailed;          // DETAILED (1.3 item 25)
        float CurW => builtDetailed ? DetailW : W;
        /// <summary>The last half second's figures (checks): frames a second, the processor's and the graphics chip's milliseconds (0 = not given).</summary>
        public static float ShownFps, ShownCpuMs, ShownGpuMs; public static string ShownText = "";

        void Build()
        {
            var go = new GameObject("OpenZ_FpsCanvas", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var cs = go.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080); cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; cs.matchWidthOrHeight = 1f;
            var root = Hud.Node("Root", go.transform); root.Fill(); root.pivot = Vector2.zero;
            builtDetailed = OzQuality.FpsDetailed;
            var c = Hud.Chip(root, "FpsChip", new OzRect(OzAnchor.TR, OzLayout.RefW - Right - CurW, Y, CurW, H), "", OzUi.A(OzUi.GM900, 0.86f), OzUi.Amber, 10, 0.12f, OzUi.A(OzUi.AmberLo, 0.9f));
            text = c.text; chipRt = root.Find("FpsChip") as RectTransform; placedY = Y; placedRight = Right; shown = "";   // 1.5 kit fps_chip_place_r3: built at home
        }

        RectTransform chipRt; float placedY, placedRight; int placedW, placedH; float placedScale = -1f;
        /// <summary>Checks: canvas units from the top at which the chip stands now (Y unless a panel lay there).</summary>
        public static float PlacedY;
        /// <summary>Checks (1.5 kit fps_chip_place_r3): canvas units from the screen's right edge to the chip's right edge now (Right at home).</summary>
        public static float PlacedRight;
        /// <summary>Checks (1.5 kit fps_chip_place_r3): the spot of the last look: home, band, corner top, corner bottom, slid, home (none clear),
        /// editor bar, editor field, editor field slid, held (a dialog, the pause menu, the end screen or ARRANGE INTERFACE: it stays).</summary>
        public static string PlacedSpot = "home";
        /// <summary>1.5 kit fps_chip_place_r3: how far under MENU the chip may walk in a match (canvas units; MENU ends at 80 u at 160 %), the
        /// corners' gap from the screen's edge, the slide step.</summary>
        public const float BandDown = 120f, CornerGap = 8f, SlideStep = 24f;
        readonly Vector3[] barCorners = new Vector3[4];

        /// <summary>The chip's place, looked at twice a second. It NEVER floats mid-screen (1.5 kit fps_chip_place_r3, owner exe 13c;
        /// before, a right side covered from top to bottom, as the Unit editor's STATS
        /// box and TABLE, walked it 60 steps down to y 424, onto the unit list, and a dialog / the pause menu / the end screen did the same).
        /// The Unit editor: in its top bar (EditorSpot). A match (MatchSpot): the first spot clear of every panel of the interface: (1) under
        /// MENU, Y down to Y + BandDown in 6 u steps (a large interface scale, panels the player arranged); (2) the top-right corner; (3) the
        /// bottom-right corner (and up to BandDown over it); (4) at Y slid left in SlideStep steps, never past the screen's middle; (5) none
        /// clear: home (Y under MENU). A dialog, the pause menu, the end screen or ARRANGE INTERFACE (the HUD Modal) leaves it where it
        /// stood; no HUD (the main menu, a loading screen): home.</summary>
        void Place()
        {
            if (!chipRt) return;
            var hud = OpenZHud.I;
            float k = Screen.height / 1080f, y = Y, right = Right; string spot = "home";
            if (hud && hud.Canvas)
            {
                if (hud.Modal) { PlacedY = placedY; PlacedRight = placedRight; PlacedSpot = "held"; return; }
                if (!EditorSpot(hud, k, ref right, ref y, ref spot)) MatchSpot(hud, k, ref right, ref y, ref spot);
            }
            if (Mathf.Abs(y - placedY) > 0.01f || Mathf.Abs(right - placedRight) > 0.01f)
            { placedY = y; placedRight = right; chipRt.Place(new OzRect(OzAnchor.TR, OzLayout.RefW - right - CurW, y, CurW, H)); }
            PlacedY = placedY; PlacedRight = placedRight; PlacedSpot = spot;
        }

        /// <summary>A match's spots (Place): under MENU, the two right corners, slid left; none clear: home.</summary>
        void MatchSpot(OpenZHud hud, float k, ref float right, ref float y, ref string spot)
        {
            float foot = OzLayout.RefH - CornerGap - H, half = Screen.width / k * 0.5f;
            for (float t = Y; t <= Y + BandDown + 0.01f; t += 6f) if (Clear(hud, Right, t, k)) { y = t; spot = t > Y + 0.01f ? "band" : "home"; return; }
            if (Clear(hud, Right, CornerGap, k)) { y = CornerGap; spot = "corner top"; return; }
            for (float t = foot; t >= foot - BandDown - 0.01f; t -= 6f) if (Clear(hud, Right, t, k)) { y = t; spot = "corner bottom"; return; }
            for (float r = Right + SlideStep; r + CurW <= half; r += SlideStep) if (Clear(hud, r, Y, k)) { right = r; spot = "slid"; return; }
            spot = "home (none clear)";
        }

        /// <summary>The Unit editor (1.5 kit fps_chip_place_r3): the chip stands in the top bar's empty stretch between the crumb and RESET
        /// STATS (OzUeTopBar.FreeStretch: nothing ever opens there; the TABLE, the STATS box, the help card and the windows all lie under the
        /// bar), its right edge OzUeTopBar.Gap before RESET STATS, centred on the bar's buttons. Where the stretch is narrower than the chip (a
        /// large interface scale on a small screen) it takes the free field's top-right corner (Right u from the field's right edge = the
        /// STATS box, CornerGap u under the bar), slid left in SlideStep steps while a panel lies there, never past the field's middle.
        /// False outside the editor.</summary>
        bool EditorSpot(OpenZHud hud, float k, ref float right, ref float y, ref string spot)
        {
            var shell = OzUnitEditor.Active ? OzUeShellRoot.I : null;
            var top = shell != null ? shell.Top : null;
            if (top == null || !top.Root) return false;
            float ppu = OzUnitEditor.PxPerUnit;
            if (BarSpot(top.FreeStretch, CurW * k / ppu, H * k / ppu, out Rect r))
            {
                top.Root.GetWorldCorners(barCorners);                                                          // an overlay canvas: world = screen pixels; [1] = top-left
                right = (Screen.width - (barCorners[1].x + r.xMax * ppu)) / k; y = (Screen.height - (barCorners[1].y - r.y * ppu)) / k; spot = "editor bar";
                return true;
            }
            var f = OzUeInsets.FreeFieldScreen;                                                                 // pixels, origin bottom-left
            float r0 = (Screen.width - f.xMax) / k + Right, y0 = (Screen.height - f.yMax) / k + CornerGap, mid = (Screen.width - f.center.x) / k;
            right = r0; y = y0; spot = "editor field";
            if (Clear(hud, r0, y0, k)) return true;
            for (float rr = r0 + SlideStep; rr + CurW <= mid + 0.01f; rr += SlideStep) if (Clear(hud, rr, y0, k)) { right = rr; spot = "editor field slid"; return true; }
            return true;
        }

        /// <summary>Pure (the plan check runs it at every size, UI/OzFpsChipProof.cs): the chip's rect (editor canvas units, top-left origin) in
        /// the bar's empty stretch `free` for a chip w x h units: its right edge at the stretch's right end, centred on the stretch's height;
        /// false when the stretch is narrower or lower than the chip.</summary>
        internal static bool BarSpot(Rect free, float w, float h, out Rect r)
        {
            r = new Rect(free.xMax - w, free.y + (free.height - h) * 0.5f, w, h);
            return w > 0f && h > 0f && free.width >= w && free.height >= h;
        }

        /// <summary>The chip's rect (its right edge `right` u from the screen's right, its top `y` u from the top) is clear: none of 15 points
        /// (5 across its top, middle and bottom rows) nor the 2 points 5 u over it lies on a panel; the whole chip inside the screen.</summary>
        bool Clear(OpenZHud hud, float right, float y, float k)
        {
            float r = Screen.width - right * k, l = r - CurW * k, top = Screen.height - y * k, bottom = Screen.height - (y + H) * k;
            if (l < 0f || bottom < 0f || top > Screen.height) return false;
            for (int i = 0; i <= 4; i++)
            {
                float x = Mathf.Lerp(l + 1f, r - 1f, i * 0.25f);
                if (hud.IsOverUi(new Vector2(x, top - 1f)) || hud.IsOverUi(new Vector2(x, (top + bottom) * 0.5f)) || hud.IsOverUi(new Vector2(x, bottom + 1f))) return false;
            }
            return !hud.IsOverUi(new Vector2(l + 1f, Mathf.Min(Screen.height - 1f, top + 5f * k))) && !hud.IsOverUi(new Vector2(r - 1f, Mathf.Min(Screen.height - 1f, top + 5f * k)));   // and 5 units of air above it
        }

        static bool Hidden => !OzQuality.FpsOn || OzSettings.TakeArg || CaptureKit.CineActive;

        void LateUpdate()
        {
            bool show = !Hidden;
            if (!show) { if (canvas && canvas.enabled) canvas.enabled = false; acc = 0f; frames = 0; cpuAcc = gpuAcc = mainAcc = renderAcc = rulesAcc = 0.0; cpuN = gpuN = mainN = renderN = 0; ShownText = ""; return; }
            if (canvas && builtDetailed != OzQuality.FpsDetailed) { canvas.enabled = false; Destroy(canvas.gameObject); canvas = null; }   // ON <-> DETAILED: the chip is built again at its width (the old one hidden at once: Destroy waits for the frame's end)
            if (!canvas) Build();
            if (!canvas.enabled) canvas.enabled = true;
            acc += Time.unscaledDeltaTime; frames++;
            rulesAcc += ZGame.RuleMsLastFrame;                                                              // the rule ticks of this frame (View/ZGame.cs, 0 outside a level)
            OzFrameTimes.Sample();
            if (OzFrameTimes.Have)
            {   // the processor's part: the longer of the main and the render thread, without the wait for the frame limit
                double c = System.Math.Max(OzFrameTimes.Main, OzFrameTimes.Render), g = OzFrameTimes.Gpu;
                if (c > 0.0) { cpuAcc += c; cpuN++; }
                if (g > 0.0) { gpuAcc += g; gpuN++; }
                if (OzFrameTimes.Main > 0.0) { mainAcc += OzFrameTimes.Main; mainN++; }
                if (OzFrameTimes.Render > 0.0) { renderAcc += OzFrameTimes.Render; renderN++; }
            }
            if (acc < 0.5f) return;
            Place();
            float fps = frames / acc, cpu = cpuN > 0 ? (float)(cpuAcc / cpuN) : 0f, gpu = gpuN > 0 ? (float)(gpuAcc / gpuN) : 0f;
            string s;
            if (builtDetailed)
            {
                float main = mainN > 0 ? (float)(mainAcc / mainN) : 0f, render = renderN > 0 ? (float)(renderAcc / renderN) : 0f, rules = (float)(rulesAcc / frames);
                s = main > 0f
                    ? $"{Mathf.RoundToInt(fps)} FPS · MAIN {Mathf.RoundToInt(main)} · RENDER {Mathf.RoundToInt(render)}{(gpu > 0f ? " · GPU " + Mathf.RoundToInt(gpu) : "")} · RULES {Mathf.RoundToInt(rules)} MS · {OzQuality.Name}"
                    : $"{Mathf.RoundToInt(fps)} FPS · {acc * 1000f / frames:0.0} MS · RULES {Mathf.RoundToInt(rules)} MS · {OzQuality.Name}";
            }
            else
                s = cpu > 0f
                    ? $"{Mathf.RoundToInt(fps)} FPS · CPU {Mathf.RoundToInt(cpu)} MS{(gpu > 0f ? " · GPU " + Mathf.RoundToInt(gpu) + " MS" : "")} · {OzQuality.Name}"
                    : $"{Mathf.RoundToInt(fps)} FPS · {acc * 1000f / frames:0.0} MS · {OzQuality.Name}";
            if (s != shown) { shown = s; text.Set(s); }
            ShownFps = fps; ShownCpuMs = cpu; ShownGpuMs = gpu; ShownText = s;
            acc = 0f; frames = 0; cpuAcc = gpuAcc = mainAcc = renderAcc = rulesAcc = 0.0; cpuN = gpuN = mainN = renderN = 0;
        }
    }
}
