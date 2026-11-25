// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the cloud sheets on the player's camera, the soft edge field and the units held off under the fog.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR, drawing (see View/OzFogOfWar.cs; premium clouds since 5 Nov, layered cloud sheets since round 4).
    /// <list type="bullet">
    /// <item>The soft edge field: the reveal map averaged onto a 2 m grid (a 32 m pad round the map), blurred (Gaussian, 8 m) into an R16
    /// render texture, then turned into the signed distance to the hiding contour at 0.5 m a texel (a cubic B-spline of the field, its
    /// logistic inverse; R16 float) by four shader passes in one command buffer ("OZ Fow Field"), run only on frames where the reveal map
    /// changed. On L07 the same blur makes the lava glow map once at the start (from the map's lava tiles, never from the scene's colour).</item>
    /// <item>The pass: <see cref="FowPass"/>, put into the player's camera from <see cref="RenderPipelineManager.beginCameraRendering"/> (no
    /// renderer asset is changed), after the atmosphere (BeforeRenderingPostProcessing + 1), so bloom, grading and the vignette still come after
    /// it. One full-resolution raster pass, "OZ Fow Sheets": four stacked cloud sheets hit by every view ray, drawn over the camera colour
    /// (Blend One SrcAlpha) with the scene depth only for an explored surface standing in front of a sheet and the ground shadows; pixels
    /// nothing draws on are discarded. Only while the fog is on screen. While it is drawn the camera's own dithering is on (given back when
    /// the fog goes). The pass's pipeline state is built once during the briefing (<see cref="Prewarm"/>): no first-use hitch.</item>
    /// <item>The player's camera only: a game camera that has an enabled RtsCamera and draws to the screen (or is the main camera). The
    /// comms windows, previews, the scene view, flybys and the Unit editor's cameras never get it.</item>
    /// <item>Hidden units: while that camera draws, the renderers of every unit not of the player's side whose centre stands in ground not
    /// explored yet are held off (Renderer.forceRenderingOff, so no picture and no shadow), and right after it every one of them is given
    /// back exactly as it was. Nothing else ever sees the change (other scripts set that flag in their own updates).</item>
    /// </list>
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        public const string ShaderRes = "OpenZ/FX/OzFogOfWar", ShaderName = "OpenZ/FogOfWar", NoiseRes = "OpenZ/FX/FowNoise/", SheetsRes = "OpenZ/FX/FowSheets/";
        /// <summary>The soft edge field: texels of pad round the map, metres a texel; the signed distance's texels per field texel.</summary>
        public const int FieldPad = 16, DistPerField = 4; public const float FieldTexelM = 2f;
        static Material mat; static bool matTried; static string matWhy; static FowPass pass;
        static Texture2D sheetTex, streakTex, flowTex, noiseBlue;
        static int passSheets = -1, passDown = -1, passBlurH = -1, passBlurV = -1, passDist = -1;
        /// <summary>The fog's material (null until the first match with the fog, or when the shader or its textures are missing).</summary>
        public static Material Mat => mat;
        /// <summary>Why there is no material (null when there is one).</summary>
        public static string MatWhy => matWhy;
        /// <summary>Checks: passes put into the player's camera, recorded by the render graph, drawn on the GPU.</summary>
        public static int PassesQueued, PassesRecorded, PassesDrawn;
        /// <summary>Checks: views and renderers held off on the last player camera frame.</summary>
        public static int HiddenViews, HiddenRenderers;
        /// <summary>
        /// A unit of another side (an enemy, an empty vehicle) whose centre is still in ground
        /// not explored yet but within EarlyDrawM metres of explored ground (any reveal cell at or over Revealed within that radius) is DRAWN under the
        /// cloud instead of held off, so it comes into view with the cloud clearing over it. 3 m: the fog's soft edge measured off Unity
        /// (kits/mayhem_headstart/proof/proof_fog_edge.txt): such a unit is first drawn where the field distance is +3.3 m or more, where the cloud
        /// over a 2.5 m tank is 0.99-1.00 (its thinnest 5 % 0.94+), so nothing of it is readable early (cover 100 %; MAYHEM's lowest cover 85 / 90 %
        /// leaves the deep fog's own see-through, as for the ground under it). Its shadows are held off while it is drawn early (a shadow could fall
        /// on explored ground) and given back right after the camera. View only: Covers (hover, click, orders, bars, badges), the minimap, the radar,
        /// selection, sounds and the score read the reveal map as before. 0 = as before.</summary>
        public static float EarlyDrawM = 3f;
        /// <summary>Checks: units drawn early under the fog's soft edge on the player's camera's last frame, and their renderers whose shadows were held off.</summary>
        public static int EarlyViews, EarlyShadowsHeld;
        /// <summary>The early band in reveal cells (public: the Editor assembly's check F1 reads it).</summary>
        public static int EarlyDrawCells() => EarlyDrawM <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(EarlyDrawM * 8f / OzFowMask.CellPx));   // 8 Zod px = 1 m
        /// <summary>A reveal cell at or over Revealed within <paramref name="r"/> cells of the Zod px point (a round disc).</summary>
        public static bool NearRevealed(OzFowMask m, int px, int py, int r)
        {
            if (m == null || r <= 0) return false;
            int cx = m.CellX(px), cy = m.CellY(py), w = m.W, h = m.H, r2 = r * r; var s = m.Shown;
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy; if (y < 0 || y >= h) continue;
                int row = y * w;
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = cx + dx; if (x < 0 || x >= w) continue;
                    if (s[row + x] >= OzFowMask.Revealed) return true;
                }
            }
            return false;
        }
        /// <summary>Checks: the unit of another side is held off under the fog now (covered ground, not in the early band); false when the fog has no map.</summary>
        public static bool HeldUnder(OpenZ.Sim.ZObject o) => o != null && maskNow != null && maskNow.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed && !NearRevealed(maskNow, o.CenterX, o.CenterY, EarlyDrawCells());
        readonly List<(Renderer r, ShadowCastingMode m)> earlyNow = new List<(Renderer r, ShadowCastingMode m)>(64);
        void RestoreEarly()
        {
            for (int i = 0; i < earlyNow.Count; i++) { var e = earlyNow[i]; if (e.r) e.r.shadowCastingMode = e.m; }
            earlyNow.Clear();
        }
        /// <summary>Checks: the renderers given back that were found changed in between (must stay 0).</summary>
        public static int RestoreSurprises;
        /// <summary>Editor checks only: 0 the look, 1 opacity, 2 the edge map (R 1 - T, G 0.5 + d / 64 at the surface, B the deck's cover at
        /// the surface), 3 each sheet's alpha (R crowns, G billows, B deck + half the edge mist), 4 the cloud's own opacity (no explored
        /// surface in front).</summary>
        public static int DebugMode;
        /// <summary>Cost Bench B6 only: the fog runs (units held off, the camera's dithering) but its pass is not put into the camera.</summary>
        public static bool BenchNoPass;
        /// <summary>Checks: the times the pass's pipeline state was built ahead of the first frame (<see cref="Prewarm"/>).</summary>
        public static int Prewarms;
        /// <summary>Cost Bench: the player camera's colour target on the last recorded frame (MSAA samples and format), so the pass is timed
        /// into the same kind of target.</summary>
        public static int LastMsaa = 1; public static GraphicsFormat LastColorFormat = GraphicsFormat.B10G11R11_UFloatPack32;

        public static readonly ProfilingSampler SamplerField = new ProfilingSampler("OZ Fow Field"), SamplerCloud = new ProfilingSampler("OZ Fow Sheets"),
                                                SamplerComposite = new ProfilingSampler("OZ Fow Composite (unused)");

        /// <summary>The time step of the fog's motion and reveal: the capture step while a recording runs at a fixed rate (so a clip shows the
        /// real speed), else the real-time step (at most 0.25 s).</summary>
        public static float FowDt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.25f);

        public static bool MaterialEnsure()
        {
            if (mat) return true;
            if (matTried) return false;
            matTried = true;
            var sh = Resources.Load<Shader>(ShaderRes);
            if (!sh) sh = Shader.Find(ShaderName);
            if (!sh || !sh.isSupported) { matWhy = "shader " + ShaderName + (sh ? " not supported on this GPU" : " not found"); return false; }
            sheetTex = Resources.Load<Texture2D>(SheetsRes + "OzFowSheet"); streakTex = Resources.Load<Texture2D>(SheetsRes + "OzFowStreak");
            flowTex = Resources.Load<Texture2D>(SheetsRes + "OzFowFlow"); noiseBlue = Resources.Load<Texture2D>(NoiseRes + "OzFowBlue");
            if (!sheetTex || !streakTex || !flowTex || !noiseBlue)
            { matWhy = "the cloud sheet textures are missing in Resources/" + SheetsRes + " (or OzFowBlue in " + NoiseRes + ")"; return false; }
            var m = new Material(sh) { name = "OZ_FogOfWar", hideFlags = HideFlags.DontSave };
            passSheets = m.FindPass("Sheets"); passDown = m.FindPass("FieldDown"); passBlurH = m.FindPass("FieldBlurH"); passBlurV = m.FindPass("FieldBlurV"); passDist = m.FindPass("FieldDist");
            if (passSheets < 0 || passDown < 0 || passBlurH < 0 || passBlurV < 0 || passDist < 0)
            { matWhy = "shader " + ShaderName + " lacks a pass (Sheets, FieldDown, FieldBlurH, FieldBlurV, FieldDist)"; UnityEngine.Object.DestroyImmediate(m); return false; }
            mat = m;
            MaterialBind();
            pass = new FowPass { mat = mat };
            return true;
        }

        /// <summary>The textures (and the field, distance and glow of the match running) onto the material.</summary>
        static void MaterialBind()
        {
            if (!mat) return;
            mat.SetTexture(IdSheet, sheetTex); mat.SetTexture(IdStreak, streakTex); mat.SetTexture(IdFlow, flowTex); mat.SetTexture(IdBlue, noiseBlue);
            var f = I;
            mat.SetTexture(IdGlow, f && f.glowRT ? f.glowRT : (Texture)Texture2D.blackTexture);
            if (f && f.fieldA) mat.SetTexture(IdField, f.fieldA);
            if (f && f.fieldD) mat.SetTexture(IdDist, f.fieldD);
        }

        static readonly int IdMask = Shader.PropertyToID("_OzFowMask"), IdMap = Shader.PropertyToID("_OzFowMap"), IdNight = Shader.PropertyToID("_OzFowNight"),
                            IdOn = Shader.PropertyToID("_OzFowOn"), IdOutside = Shader.PropertyToID("_OzFowOutsideM"), IdDebug = Shader.PropertyToID("_OzFowDebug"),
                            IdField = Shader.PropertyToID("_OzFowField"), IdFieldRect = Shader.PropertyToID("_OzFowFieldRect"), IdFieldTexel = Shader.PropertyToID("_OzFowFieldTexel"),
                            IdSrc = Shader.PropertyToID("_OzFowSrc"), IdGlow = Shader.PropertyToID("_OzFowGlow"), IdDist = Shader.PropertyToID("_OzFowDist"),
                            IdSheet = Shader.PropertyToID("_OzFowSheet"), IdStreak = Shader.PropertyToID("_OzFowStreak"), IdFlow = Shader.PropertyToID("_OzFowFlow"),
                            IdBlue = Shader.PropertyToID("_OzFowBlue"), IdOfs = Shader.PropertyToID("_OzFowOfs"), IdOfs2 = Shader.PropertyToID("_OzFowOfs2"),
                            IdFlowP = Shader.PropertyToID("_OzFowFlowP"),
                            IdAmbUp = Shader.PropertyToID("_OzFowAmbUp"), IdFlash = Shader.PropertyToID("_OzFowFlash"),
                            IdBlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        int matLook = -1; float matY0 = float.NaN;
        Texture matTex;                                                                                  // the reveal texture last put on the material
        readonly Vector3[] ambDir = { Vector3.up }; readonly Color[] ambCol = new Color[1];
        // the motion: every offset integrated in double from the fog's time step and wrapped to 0..1 on the tileable textures (no jump, no
        // pulsing, also when the speed changes with rain): per sheet its shape, its fine octave and its streak tone; the flow field
        readonly double[] oShape = new double[8], oFine = new double[8], oStreak = new double[8]; double oFlowU, oFlowV, oGlow;
        readonly Vector4[] ofsArr = new Vector4[4], ofs2Arr = new Vector4[4];
#if UNITY_EDITOR
        float nextRebind;                                                                                // the editor: values put back after a shader re-import
#endif

        static double Frac(double v) => v - Math.Floor(v);
        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>A sheet's uv offset step for a world displacement (dx, dz): its frame (turned deg from +x), the stretch and the tile.</summary>
        static void Drift(double[] o, int k, double dx, double dz, double deg, double stretch, double tile)
        {
            double a = deg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            double u = (dx * c + dz * s) / (Math.Max(1.0, stretch) * tile), v = (-dx * s + dz * c) / tile;
            o[2 * k] = Frac(o[2 * k] - u); o[2 * k + 1] = Frac(o[2 * k + 1] - v);                       // the picture moves with the wind
        }

        /// <summary>The frame's numbers onto the material: the style and planet (when they changed), the reveal map's rectangle, the field,
        /// the fade, the sheets' drift, the sky's light, the night, rain and storm flashes.</summary>
        void SetMaterial()
        {
            if (!mat || mask == null) return;
            if (matTex != tex) { mat.SetTexture(IdMask, tex); matTex = tex; }                                // (5 Nov) not GetTexture: the shader has no Properties block, so reading it logged an error every frame
            BiomeStep();                                                                                     // 1.4 fog_biome_looks: the biome clock (View/OzFogOfWar.Biomes.cs)
            int planet = PlanetIndex, ver = lookVersion * 4 + OzQuality.Level;
#if UNITY_EDITOR
            // a shader re-import in the editor wipes a Properties-less material's values: put them back twice a second (never read back from the
            // material, so no console error; the built game never re-imports)
            if (Time.unscaledTime >= nextRebind) { nextRebind = Time.unscaledTime + 0.5f; matLook = -1; mat.SetTexture(IdMask, tex); matTex = tex; MaterialBind(); FieldUniforms(); }
#endif
            if (matLook != ver || matY0 != blanketY) { ApplyLook(mat, look, planet, blanketY); matLook = ver; matY0 = blanketY; }
            // Keep terrain, landmarks and remembered contacts legible even in the densest clouds.
            mat.SetFloat(IdSee, Mathf.Max(.4f, 1f - CoverNow));
            float wm = mask.W * OzFowMask.CellPx / 8f, hm = mask.H * OzFowMask.CellPx / 8f;                 // 8 Zod px = 1 m (ZGame.PxToWorld: x = px / 8, z = -py / 8)
            mat.SetVector(IdMap, new Vector4(0f, 0f, 1f / wm, -1f / hm));                                   // u = x / width, v = -z / height (v grows to the south, the mask's rows)
            mat.SetFloat(IdOn, strength);
            LosMaterial();                                                                                   // 1.5 los_fog (View/OzFogOfWar.Los.cs): the seen-now texture and the line-of-sight fog's look
            mat.SetFloat(IdDebug, DebugMode);
            mat.SetFloat(IdOutside, 20f);                                                                     // closed over 20 m outside the map

            // rain (L16 jungle, L17 storms): darker and a bit faster
            var k = Looks[look]; var pal = Palettes[planet];
            float rain = 0f;
            if (OzJungleWeather.I) rain = Mathf.Max(rain, OzJungleWeather.I.Rain);
            if (OzWeather.I) rain = Mathf.Max(rain, OzWeather.I.Rain);
            rain = Mathf.Clamp01(rain);
            float skyK = pal.SkyGain * pal.Exposure;
            mat.SetVector(IdSkyTint, new Vector4(pal.Sky.x * skyK, pal.Sky.y * skyK, pal.Sky.z * skyK, Mathf.Lerp(1f, pal.WetDim, rain)));
            // the night (NightGrade.NightK 0 day .. 1 night): the moon's and the sky's share on the cloud follow the scene, so the cloud keeps
            // about the same brightness against the ground by day and by night (never a day cloud pasted on the night)
            float nk = Mathf.Clamp01(NightGrade.NightK);
            mat.SetVector(IdNight, new Vector4(Mathf.Lerp(1f, pal.NightSun, nk), Mathf.Lerp(1f, pal.NightAmb, nk), 0f, 0f));

            // the drift: each sheet along the wind at its own speed (the fine octave 1.3 x, the streak tone 1.6 x); the flow field (curl,
            // breathing, wander) slower than the sheets it bends, so their shapes swirl, grow and dissolve
            double dt = FowDt, speed = pal.DriftMul * Mathf.Lerp(1f, 1.3f, rain);
            double wx = Math.Cos(WindDeg * Math.PI / 180.0), wz = Math.Sin(WindDeg * Math.PI / 180.0);
            for (int i = 0; i < 4; i++)
            {
                var l = k.L[i]; double v = l.Drift * speed * dt, a = WindDeg + l.RotDeg;
                Drift(oShape, i, wx * v, wz * v, a, l.Stretch, l.Tile);
                Drift(oFine, i, wx * v * 1.3, wz * v * 1.3, a + 71.0, l.Stretch, l.Tile / 3.7);
                Drift(oStreak, i, wx * v * 1.6, wz * v * 1.6, a - 64.0, l.Stretch, 14.0);
                ofsArr[i] = new Vector4((float)oShape[2 * i], (float)oShape[2 * i + 1], (float)oFine[2 * i], (float)oFine[2 * i + 1]);
                ofs2Arr[i] = new Vector4((float)oStreak[2 * i], (float)oStreak[2 * i + 1], 0f, 0f);
            }
            double fv = FlowSpeed * speed * dt / FlowTileM;
            oFlowU = Frac(oFlowU - wx * fv); oFlowV = Frac(oFlowV - wz * fv);
            oGlow = Frac(oGlow + 0.45 * dt);
            mat.SetVectorArray(IdOfs, ofsArr);
            mat.SetVectorArray(IdOfs2, ofs2Arr);
            mat.SetVector(IdFlowP, new Vector4(1f / FlowTileM, (float)oFlowU, (float)oFlowV, WanderM));
            mat.SetVector(IdGlowP, new Vector4(pal.GlowFall > 0 ? 1f / pal.GlowFall : 0f, pal.GlowStr > 0 ? 1f : 0f, 0.12f, (float)oGlow));

            // the storm's flashes (L17): only a mid or close flash that has a point this frame lights the cloud round it (a far flash has no
            // point: the sky's light, read every frame during any flash, brightens the cloud evenly); the sky's light every 0.5 s otherwise
            var wth = OzWeather.I; bool flash = wth && wth.FlashNow > 0.01f; float near = wth ? wth.FlashPointNow : 0f;
            mat.SetVector(IdFlash, near > 0.01f && pal.FlashGain > 0f ? new Vector4(wth.FlashPoint.x, wth.FlashPoint.y, wth.FlashPoint.z, near * pal.FlashGain) : Vector4.zero);
            if (Time.unscaledTime >= nextAmbient || flash)
            {
                nextAmbient = Time.unscaledTime + 0.5f;
                RenderSettings.ambientProbe.Evaluate(ambDir, ambCol);
                Color u = ambCol[0];
                mat.SetVector(IdAmbUp, new Vector4(Mathf.Max(0f, u.r), Mathf.Max(0f, u.g), Mathf.Max(0f, u.b), Mathf.Max(0f, Luma(u))));
            }
        }

        // ---------------------------------------------------------------- the soft edge field and the signed distance
        RenderTexture fieldA, fieldB, fieldD, glowRT; Texture2D glowSrc; CommandBuffer fieldCmd; bool fieldDirty; int fieldW, fieldH;
        /// <summary>Checks: field runs (frames where the reveal map changed, and the start).</summary>
        public static int FieldRuns;
        /// <summary>The soft edge field of this match (R16, 2 m a texel; null with the fog off).</summary>
        public static RenderTexture FieldTexture => I ? I.fieldA : null;
        /// <summary>The signed distance of this match (R16 float, 0.5 m a texel; null with the fog off).</summary>
        public static RenderTexture DistTexture => I ? I.fieldD : null;

        void FieldUniforms()
        {
            if (fieldW <= 0 || !mat) return;
            float pad = FieldPad * FieldTexelM;
            mat.SetVector(IdFieldRect, new Vector4(pad, pad, 1f / (fieldW * FieldTexelM), 1f / (fieldH * FieldTexelM)));
            mat.SetVector(IdFieldTexel, new Vector4(1f / fieldW, 1f / fieldH, fieldW, fieldH));
        }

        static GraphicsFormat FieldFormat() =>
            SystemInfo.IsFormatSupported(GraphicsFormat.R16_UNorm, GraphicsFormatUsage.Render) ? GraphicsFormat.R16_UNorm : GraphicsFormat.R16_SFloat;

        static GraphicsFormat DistFormat() =>
            SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.Render) ? GraphicsFormat.R16_SFloat : GraphicsFormat.R32_SFloat;

        static RenderTexture NewField(string name, int w, int h, GraphicsFormat fmt)
        {
            var rt = new RenderTexture(w, h, fmt, GraphicsFormat.None)
            { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.DontSave };
            rt.Create();
            return rt;
        }

        void FieldEnsure()
        {
            if (fieldA || mask == null || !mat) return;
            fieldW = (mask.W + 1) / 2 + 2 * FieldPad; fieldH = (mask.H + 1) / 2 + 2 * FieldPad;
            fieldA = NewField("OZ_FowFieldA", fieldW, fieldH, FieldFormat()); fieldB = NewField("OZ_FowFieldB", fieldW, fieldH, FieldFormat());
            fieldD = NewField("OZ_FowDist", fieldW * DistPerField, fieldH * DistPerField, DistFormat());
            mat.SetTexture(IdField, fieldA); mat.SetTexture(IdDist, fieldD);
            FieldUniforms();
            fieldCmd = new CommandBuffer { name = "OZ Fow Field" };
            using (new ProfilingScope(fieldCmd, SamplerField))
            {
                FieldBlit(fieldCmd, tex, fieldA, passDown);
                FieldBlit(fieldCmd, fieldA, fieldB, passBlurH);
                FieldBlit(fieldCmd, fieldB, fieldA, passBlurV);
                FieldBlit(fieldCmd, fieldA, fieldD, passDist);
            }
            mat.SetTexture(IdGlow, Texture2D.blackTexture);
            if (planetAuto == Volcano) GlowBuild();
            fieldDirty = true;
        }

        static void FieldBlit(CommandBuffer cmd, Texture src, RenderTexture dst, int p)
        {
            var b = new MaterialPropertyBlock();
            b.SetTexture(IdSrc, src); b.SetVector(IdBlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
            cmd.SetRenderTarget(dst);
            cmd.DrawProcedural(Matrix4x4.identity, mat, p, MeshTopology.Triangles, 3, 1, b);
        }

        /// <summary>L07: the lava tiles (one texel a tile, the field's grid and pad) blurred like the field, once at the start.</summary>
        void GlowBuild()
        {
            int tw = W.Map.WidthPix / 16, th = W.Map.HeightPix / 16, lava = 0;
            var data = new byte[fieldW * fieldH];
            for (int j = 0; j < fieldH; j++)
                for (int i = 0; i < fieldW; i++)
                {
                    int tx = i - FieldPad, ty = j - FieldPad;
                    if (tx >= 0 && ty >= 0 && tx < tw && ty < th && W.Map.LavaAt(tx, ty)) { data[j * fieldW + i] = 255; lava++; }
                }
            glowSrc = new Texture2D(fieldW, fieldH, TextureFormat.R8, false, true) { name = "OZ_FowGlowSrc", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            glowSrc.LoadRawTextureData(data); glowSrc.Apply(false, false);
            glowRT = NewField("OZ_FowGlow", fieldW, fieldH, FieldFormat());
            var cmd = new CommandBuffer { name = "OZ Fow Glow" };
            FieldBlit(cmd, glowSrc, fieldB, passBlurH);
            FieldBlit(cmd, fieldB, glowRT, passBlurV);
            Graphics.ExecuteCommandBuffer(cmd); cmd.Release();
            mat.SetTexture(IdGlow, glowRT);
            Debug.Log($"[OpenZ FogOfWar] lava glow map: {lava} lava tiles under the cloud");
        }

        void RunField()
        {
            if (fieldCmd == null || !mat) return;
            mat.SetTexture(IdMask, tex); matTex = tex;
            Graphics.ExecuteCommandBuffer(fieldCmd);
            fieldDirty = false; FieldRuns++;
        }

        /// <summary>Cost Bench only: the soft edge field's four passes submitted n times now (each run makes the same field from the same
        /// reveal map, so the picture does not change). False when the field is not running.</summary>
        public bool BenchFieldRun(int n)
        {
            if (fieldCmd == null || !mat) return false;
            mat.SetTexture(IdMask, tex); matTex = tex;
            for (int i = 0; i < n; i++) Graphics.ExecuteCommandBuffer(fieldCmd);
            return true;
        }

        /// <summary>Cost Bench only: a reveal map that is soft band almost everywhere (everything covered, then sight circles of radiusPx on a
        /// staggered grid every spacingPx), shown at once and uploaded; RestoreMask puts the match's own map back.</summary>
        public void BenchPattern(int spacingPx, int radiusPx)
        {
            if (mask == null) return;
            mask.Clear();
            int wpx = mask.W * OzFowMask.CellPx, hpx = mask.H * OzFowMask.CellPx;
            for (int y = spacingPx / 2, row = 0; y < hpx; y += spacingPx, row++)
                for (int x = row % 2 == 0 ? spacingPx / 2 : spacingPx; x < wpx; x += spacingPx) mask.Stamp(x, y, radiusPx);
            mask.ShowAll(); lastStamp.Clear(); Upload(); RunField();
        }

        /// <summary>Cost Bench: put a reveal map saved before COVER AGAIN back (the match continues as it was; cells that were still easing
        /// up when it was saved go on easing).</summary>
        public void RestoreMask(byte[] target, byte[] shown)
        {
            if (mask == null || !mask.RestoreFrom(target, shown)) return;
            lastStamp.Clear(); Upload();
            Debug.Log("[OpenZ FogOfWar] the reveal map put back (Cost Bench)");
        }

        void FieldRelease()
        {
            if (fieldCmd != null) { fieldCmd.Release(); fieldCmd = null; }
            if (fieldA) { fieldA.Release(); Destroy(fieldA); }
            if (fieldB) { fieldB.Release(); Destroy(fieldB); }
            if (fieldD) { fieldD.Release(); Destroy(fieldD); }
            if (glowRT) { glowRT.Release(); Destroy(glowRT); }
            if (glowSrc) Destroy(glowSrc);
            fieldW = fieldH = 0;
        }

        /// <summary>Editor checks (no Play): the soft edge field of a reveal map, read back (row 0 = the field's first row, the north pad).
        /// Returns null when the material cannot be made.</summary>
        public static float[] FieldOf(OzFowMask m, out int fw, out int fh, int repeats = 1)
        {
            fw = (m.W + 1) / 2 + 2 * FieldPad; fh = (m.H + 1) / 2 + 2 * FieldPad;
            if (!MaterialEnsure()) return null;
            var t = new Texture2D(m.W, m.H, TextureFormat.R8, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            t.LoadRawTextureData(m.Shown); t.Apply(false, false);
            var a = NewField("OZ_FowTestA", fw, fh, FieldFormat()); var b = NewField("OZ_FowTestB", fw, fh, FieldFormat());
            var dd = NewField("OZ_FowTestD", fw * DistPerField, fh * DistPerField, DistFormat());
            float wm = m.W * OzFowMask.CellPx / 8f, hm = m.H * OzFowMask.CellPx / 8f, pad = FieldPad * FieldTexelM;
            mat.SetVector(IdMap, new Vector4(0f, 0f, 1f / wm, -1f / hm));
            mat.SetFloat(IdOutside, 20f);
            mat.SetFloat(Shader.PropertyToID("_OzFowEdgeK"), EdgeK);
            mat.SetVector(IdFieldRect, new Vector4(pad, pad, 1f / (fw * FieldTexelM), 1f / (fh * FieldTexelM)));
            mat.SetVector(IdFieldTexel, new Vector4(1f / fw, 1f / fh, fw, fh));
            mat.SetTexture(IdMask, t);
            var cmd = new CommandBuffer { name = "OZ Fow Field test" };
            for (int r = 0; r < Math.Max(1, repeats); r++) { FieldBlit(cmd, t, a, passDown); FieldBlit(cmd, a, b, passBlurH); FieldBlit(cmd, b, a, passBlurV); FieldBlit(cmd, a, dd, passDist); }
            Graphics.ExecuteCommandBuffer(cmd); cmd.Release();
            var req = AsyncGPUReadback.Request(a, 0, TextureFormat.RFloat);
            req.WaitForCompletion();
            float[] o = null;
            if (!req.hasError) o = req.GetData<float>().ToArray();
            a.Release(); b.Release(); dd.Release(); UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(dd); UnityEngine.Object.DestroyImmediate(t);
            return o;
        }

        // ---------------------------------------------------------------- the pipeline state ahead of the first frame
        /// <summary>Draws the pass once into a 4 x 4 target of the camera's colour format and MSAA (during the briefing), so the graphics API
        /// builds the pass's exact pipeline state before the first frame that shows the fog (no first-use hitch in the exe).</summary>
        public static void Prewarm()
        {
            if (!mat || passSheets < 0) return;
            int msaa = LastMsaa > 1 ? LastMsaa : 1;
            if (msaa <= 1 && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset ua) msaa = Mathf.Max(1, ua.msaaSampleCount);
            var fmt = SystemInfo.IsFormatSupported(LastColorFormat, GraphicsFormatUsage.Render) ? LastColorFormat : GraphicsFormat.R16G16B16A16_SFloat;
            var desc = new RenderTextureDescriptor(4, 4, fmt, 0) { msaaSamples = msaa, useMipMap = false };
            var rt = RenderTexture.GetTemporary(desc);
            var cmd = new CommandBuffer { name = "OZ Fow Prewarm" };
            var b = new MaterialPropertyBlock();
            b.SetVector(IdBlitScaleBias, new Vector4(1f, 1f, 0f, 0f)); b.SetVector(FowPass.IdSize, new Vector4(4f, 4f, 0.25f, 0.25f)); b.SetFloat(FowPass.IdFmt, fmt == GraphicsFormat.B10G11R11_UFloatPack32 ? 1f : 0f);
            cmd.SetRenderTarget(rt);
            cmd.DrawProcedural(Matrix4x4.identity, mat, passSheets, MeshTopology.Triangles, 3, 1, b);
            Graphics.ExecuteCommandBuffer(cmd); cmd.Release();
            RenderTexture.ReleaseTemporary(rt);
            Prewarms++;
        }

        // ---------------------------------------------------------------- the camera's dithering (on while the fog is drawn)
        UniversalAdditionalCameraData ditherCam; bool ditherWas;

        void DitherOn(UniversalAdditionalCameraData d)
        {
            if (ditherCam == d) { if (!d.dithering) d.dithering = true; return; }
            RestoreDither();
            ditherCam = d; ditherWas = d.dithering; d.dithering = true;
        }

        void RestoreDither()
        {
            if (ditherCam) ditherCam.dithering = ditherWas;
            ditherCam = null;
        }

        // ---------------------------------------------------------------- GPU timing (checks)
        static bool gpuTiming;
        /// <summary>Checks: record the GPU time of the passes (editor and development builds).</summary>
        public static bool GpuTiming
        {
            get => gpuTiming;
            set { gpuTiming = value; SamplerField.enableRecording = value; SamplerCloud.enableRecording = value; SamplerComposite.enableRecording = value; GpuReset(); }
        }
        static double gSumField, gSumCloud; static int gNField, gNCloud;
        /// <summary>Checks: the average GPU time (ms) of the field (frames where it ran) and of the sheets since the last reset; the composite
        /// no longer exists (0).</summary>
        public static double GpuMsField => gNField > 0 ? gSumField / gNField : 0;
        public static double GpuMsCloud => gNCloud > 0 ? gSumCloud / gNCloud : 0;
        public static double GpuMsComposite => 0;
        public static int GpuFrames => gNCloud;
        public static void GpuReset() { gSumField = gSumCloud = 0; gNField = gNCloud = 0; }

        void GpuStep()
        {
            if (!gpuTiming) return;
            float f = SamplerField.gpuElapsedTime, c = SamplerCloud.gpuElapsedTime;
            if (f > 0f) { gSumField += f; gNField++; }
            if (c > 0f) { gSumCloud += c; gNCloud++; }
        }

        /// <summary>A camera the player plays through: a game camera with an enabled RtsCamera, drawing to the screen or the main camera.</summary>
        static bool PlayerView(Camera c) =>
            c && c.cameraType == CameraType.Game && (c.targetTexture == null || c.CompareTag("MainCamera")) && c.TryGetComponent<RtsCamera>(out var r) && r.enabled;

        void BeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if ((!wanted && !radarFog) || mask == null || !PlayerView(cam) || !GateCamera(cam)) return;
            if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var d) && d.scriptableRenderer != null)
            {
                if (radarFog && radarGroundPass != null)
                {
                    radarGroundMaterial.SetTexture(RadarGroundPass.Sight, tex);
                    radarGroundMaterial.SetVector(RadarGroundPass.MapScale, new Vector4(8f / W.Map.WidthPix, -8f / W.Map.HeightPix, 0, 0));
                    radarGroundMaterial.SetFloat(RadarGroundPass.Strength, strength);
                    radarGroundMaterial.SetVector(RadarGroundPass.Interference,
                        new Vector4(Time.unscaledTime, OzMotion.Reduced ? .25f : 1f, 0, 0));
                    d.scriptableRenderer.EnqueuePass(radarGroundPass);
                }
                else if (!radarFog && mat && pass != null && fieldA)
                {
                    if (!BenchNoPass) { d.scriptableRenderer.EnqueuePass(pass); PassesQueued++; }
                    DitherOn(d);
                }
            }
            if (gateClock.HoldOff)                                                                      // 1.4 fog_gameplay_only: units held off whenever the player plays, from the first frame of play (View/OzFowGate.cs HoldsOff; with no cloud drawn yet GateCamera holds them off)
            {
                long t = Stopwatch.GetTimestamp();
                HideCovered(cam);
                long dt = Stopwatch.GetTimestamp() - t; cHide += dt; lastHide += dt;
            }
        }

        Material radarGroundMaterial;
        RadarGroundPass radarGroundPass;
        void EnsureRadarGroundPass()
        {
            if (radarGroundPass != null) return;
            var shader = Resources.Load<Shader>("OzRadarGroundFog");
            if (!shader || !shader.isSupported) { Debug.LogError("Radar ground fog shader is unavailable."); return; }
            radarGroundMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            radarGroundPass = new RadarGroundPass(radarGroundMaterial);
        }

        sealed class RadarGroundPass : ScriptableRenderPass
        {
            internal static readonly int Sight = Shader.PropertyToID("_RadarSight"), MapScale = Shader.PropertyToID("_RadarMapScale"),
                Strength = Shader.PropertyToID("_RadarSignalStrength"), Interference = Shader.PropertyToID("_RadarInterference");
            static readonly ProfilingSampler Sampler = new ProfilingSampler("OpenZ Radar Ground Fog");
            readonly Material material;
            sealed class PassData { public Material Material; public TextureHandle Source; }
            internal RadarGroundPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 2;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
            {
                var resources = context.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;
                var source = resources.activeColorTexture;
                var description = graph.GetTextureDesc(source);
                description.name = "Radar interference color";
                description.clearBuffer = false;
                description.msaaSamples = MSAASamples.None;
                description.bindTextureMS = false;
                var result = graph.CreateTexture(description);
                using var builder = graph.AddRasterRenderPass<PassData>("OpenZ Radar Ground Fog", out var data, Sampler, file: "OzFogOfWar.Render.cs");
                data.Material = material;
                data.Source = source;
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(result, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext drawing) =>
                    Blitter.BlitTexture(drawing.cmd, (RTHandle)pass.Source, new Vector4(1, 1, 0, 0), pass.Material, 0));
                resources.cameraColor = result;
            }
        }

        void EndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (hiddenNow.Count > 0 || earlyNow.Count > 0 || losLights.Count > 0) RestoreHidden();  // 1.4 mayhem_headstart: the early units' shadows too; 1.5 los_fog: the held lamps
        }

        // ---------------------------------------------------------------- units under the fog
        sealed class RCache { public readonly List<Renderer> list = new List<Renderer>(16); public float at = -1f; }
        readonly Dictionary<int, RCache> rcache = new Dictionary<int, RCache>(64);
        readonly List<Renderer> hiddenNow = new List<Renderer>(256);
        /// <summary>Checks: units in covered ground left alone on the last player camera frame because they stand outside its view (and the
        /// reach of their shadows), so the camera never draws them anyway.</summary>
        public static int HiddenSkippedOutside;

        /// <summary>(fix round 2, 5 Nov: the hide cost was 0.17-0.27 ms with 50-80 units under the fog) The player's camera's view on the
        /// floor plane as an xz box, grown by the reach of a unit's shadow (6 m tall at the main light's height, 10 to 60 m). A unit outside it
        /// is drawn neither in the picture nor as a shadow in it, so it needs no hiding on this frame. False (no box: every covered unit is
        /// hidden) when a corner of the view looks at or over the horizon.</summary>
        bool ViewBox(Camera cam, out Vector4 box)
        {
            box = default;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                var r = cam.ViewportPointToRay(new Vector3(k & 1, k >> 1, 0f));
                if (r.direction.y > -0.05f) return false;
                var p = r.origin + r.direction * ((r.origin.y - floorY) / -r.direction.y);
                if (p.x < x0) x0 = p.x; if (p.x > x1) x1 = p.x; if (p.z < z0) z0 = p.z; if (p.z > z1) z1 = p.z;
            }
            var sun = RenderSettings.sun;
            float up = sun ? -sun.transform.forward.y : 0.5f;
            float m = up > 0.02f ? Mathf.Clamp(6f * Mathf.Sqrt(Mathf.Max(0f, 1f - up * up)) / up, 10f, 60f) : 60f;
            box = new Vector4(x0 - m, z0 - m, x1 + m, z1 + m);
            return true;
        }

        void HideCovered(Camera cam)
        {
            if (!game) return;
            var views = game.LiveViewValues;
            float now = Time.unscaledTime; int nv = 0, outside = 0, refreshed = 0;
            bool boxed = ViewBox(cam, out var box);
            if (earlyNow.Count > 0) RestoreEarly();                                                         // 1.4 mayhem_headstart: a camera that never reached its end gives its shadows back first
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i]; if (!v) continue;
                var o = v.Obj; if (o == null || !radarFog && !o.IsUnit) continue;
                if (!Covers(o)) continue;
                bool los = radarFog || mask.ShownAtPx(o.CenterX, o.CenterY) >= OzFowMask.Revealed;
                float wx = o.CenterX / 8f, wz = -o.CenterY / 8f;                                                // 8 Zod px = 1 m (ZGame.PxToWorld)
                if (boxed && (wx < box.x || wx > box.z || wz < box.y || wz > box.w)) { if (!los) outside++; continue; }
                int id = v.GetInstanceID();
                if (!rcache.TryGetValue(id, out var c)) { c = new RCache(); rcache[id] = c; }
                // parts added later (a gun, a crew) join within a second; at most 3 lists made again a frame (the rest wait a frame or two)
                if (c.at < 0f || (now - c.at > 1f && refreshed < 3)) { v.GetComponentsInChildren(true, c.list); c.at = now; refreshed++; BoardingView.AddCrewRenderers(v, c.list); }   // final pass 14h: its crew figure (jeep driver, hatch commander, boarding robot: View/BoardingView.FogHide.cs) held off with it
                for (int k = 0; k < c.list.Count; k++)
                {
                    var r = c.list[k];
                    if (r && !r.forceRenderingOff) { r.forceRenderingOff = true; hiddenNow.Add(r); }
                }
                LosHeldView(v);
                if (!los) nv++;
            }
            if (radarFog || wanted)
            {
                var rules = game.GetComponent<RulesView>();
                foreach (var entry in game.StaticViews)
                {
                    var obj = W.GetAny(entry.Key);
                    if (!Covers(obj)) continue;
                    var sceneObject = obj?.IsBridge == true && rules ? rules.ActiveBridgeModel(entry.Key) : entry.Value;
                    HideStaticView(sceneObject, now, ref refreshed);
                    if (sceneObject && sceneObject.TryGetComponent<BuildingBreaker>(out var breaker))
                        HideStaticView(breaker.FogDebrisModel, now, ref refreshed);
                }
            }
            HiddenViews = nv; HiddenRenderers = hiddenNow.Count; HiddenSkippedOutside = outside;
            EarlyViews = EarlyShadowsHeld = 0;
        }

        void HideStaticView(GameObject view, float now, ref int refreshed)
        {
            if (!view) return;
            int id = view.GetInstanceID();
            if (!rcache.TryGetValue(id, out var cache)) rcache[id] = cache = new RCache();
            if (cache.at < 0 || now - cache.at > 1f && refreshed < 3)
            {
                view.GetComponentsInChildren(true, cache.list);
                cache.at = now;
                refreshed++;
            }
            foreach (var renderer in cache.list)
                if (renderer && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    hiddenNow.Add(renderer);
                }
        }

        void RestoreHidden()
        {
            for (int i = 0; i < hiddenNow.Count; i++)
            {
                var r = hiddenNow[i]; if (!r) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!r.forceRenderingOff) RestoreSurprises++;                                                   // checks: someone changed it while the camera drew (never expected)
#endif
                r.forceRenderingOff = false;
            }
            hiddenNow.Clear();
            if (losLights.Count > 0) LosRestoreLights();                                                   // 1.5 los_fog: the lamps held off with the units it hides
            if (earlyNow.Count > 0) RestoreEarly();                                                         // 1.4 mayhem_headstart
        }

        void PruneRendererCache()
        {
            if (!game) return;
            if (rcache.Count > 2 * game.LiveViewValues.Count + game.StaticViews.Count + 64) rcache.Clear();
        }

        // ---------------------------------------------------------------- the render graph pass
        /// <summary>The fog's pass (render graph): "OZ Fow Sheets", one full-resolution raster pass over the camera colour (read and written:
        /// Blend One SrcAlpha) with the scene depth (an explored surface in front of a sheet, the ground shadows). No intermediate targets.</summary>
        public sealed class FowPass : ScriptableRenderPass
        {
            public Material mat;
            public static readonly int IdSize = Shader.PropertyToID("_OzFowSize"), IdFmt = Shader.PropertyToID("_OzFowFmt");
            static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

            public FowPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1;                          // after the atmosphere's full-screen pass, before the post effects
                requiresIntermediateTexture = true;
                profilingSampler = SamplerCloud;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            sealed class PassData { public Material mat; public Vector4 size; public float fmt; }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
            {
                if (!mat) return;
                var res = frame.Get<UniversalResourceData>();
                if (res.isActiveTargetBackBuffer || !res.cameraDepthTexture.IsValid()) return;
                var cd = frame.Get<UniversalCameraData>();
                var td = cd.cameraTargetDescriptor;
                int w = Mathf.Max(1, td.width), h = Mathf.Max(1, td.height);
                LastMsaa = Mathf.Max(1, td.msaaSamples); LastColorFormat = td.graphicsFormat;                     // (the Cost Bench times the pass into the same kind of target)
                using (var b = rg.AddRasterRenderPass<PassData>("OZ Fow Sheets", out var d, SamplerCloud, file: "OzFogOfWar.Render.cs"))   // final set A1: the file name given, so no build folder path is baked into the game code
                {
                    d.mat = mat; d.size = new Vector4(w, h, 1f / w, 1f / h); d.fmt = td.graphicsFormat == GraphicsFormat.B10G11R11_UFloatPack32 ? 1f : 0f;
                    b.UseTexture(res.cameraDepthTexture, AccessFlags.Read);
                    b.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
                    b.SetRenderFunc((PassData x, RasterGraphContext c) =>
                    {
                        block.Clear();
                        block.SetVector(BlitScaleBias, new Vector4(1f, 1f, 0f, 0f)); block.SetVector(IdSize, x.size); block.SetFloat(IdFmt, x.fmt);
                        c.cmd.DrawProcedural(Matrix4x4.identity, x.mat, passSheets, MeshTopology.Triangles, 3, 1, block);
                        PassesDrawn++;
                    });
                }
                PassesRecorded++;
            }
        }
    }
}
