using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; owner 6, 6b; spec section 8, E213, E215): the playground's code-drawn floor. One flat quad at y 0
    /// under the 320 x 200 m field (and far past it, so the vignette and the base colour reach every screen pixel at any turn and tilt)
    /// drawn by Resources/OpenZ/UnitEditor/OzUeFloor.shader with the style in use (OzUeBackdrop). Every frame after the camera moved
    /// (LateUpdate) this hands the shader what the prototype's canvas knew: the field's rectangle on the screen (under the top bar), the
    /// free field's middle (its light), the visible floor, the style's slow light effect for this frame (frozen while paused, off with
    /// the motion option), the floor point under the mouse (Blueprint), the far edge's row (the haze), and the post chain's settings, so
    /// the shader can show the design colours through the game's tonemapping. The Blueprint style's metre numbers and the mouse read-out
    /// are HUD labels on the editor's field layer.
    /// </summary>
    public sealed class OzUeFloor : MonoBehaviour
    {
        public const string ShaderRes = "OpenZ/UnitEditor/OzUeFloor", ThumbShaderRes = "OpenZ/UnitEditor/OzUeFloorThumb";
        public const float HX = 160f, HZ = 100f, FD = 20f;
        /// <summary>The quad's side (m): far wider than any view of the 320 x 200 m field.</summary>
        public const float QuadSize = 4000f;
        /// <summary>Checks: the floor was made, its shader, the frames it was drawn with motion running.</summary>
        public static bool Made; public static string ShaderState = "not loaded"; public static int MotionFrames;

        public static OzUeFloor I { get; private set; }
        OzUnitEditor ed; Camera cam; RtsCamera rts; Material mat; GameObject quad;
        float motionT = 3f;                                                                       // the prototype's Backdrop starts at t = 3
        int styleShown = -1; bool gradeWarned;
        // Blueprint labels (UI)
        readonly List<OzText> numbers = new List<OzText>(); OzText mouseLabel; RectTransform labelRoot;
        readonly int[] numberShown = new int[30]; Vector2Int mouseShown = new Vector2Int(int.MinValue, int.MinValue);

        static readonly int pStyle = Shader.PropertyToID("_UeStyleIdx"), pBase = Shader.PropertyToID("_UeBase"), pTint = Shader.PropertyToID("_UeTint"), pHi = Shader.PropertyToID("_UeHi"),
            pC1 = Shader.PropertyToID("_UeC1"), pC2 = Shader.PropertyToID("_UeC2"), pC3 = Shader.PropertyToID("_UeC3"), pOpts = Shader.PropertyToID("_UeOpts"), pTime = Shader.PropertyToID("_UeTime"),
            pMA = Shader.PropertyToID("_UeMotionA"), pMB = Shader.PropertyToID("_UeMotionB"), pMouse = Shader.PropertyToID("_UeMouse"), pHaze = Shader.PropertyToID("_UeHaze"),
            pField = Shader.PropertyToID("_UeField"), pCentre = Shader.PropertyToID("_UeCentre"), pStep = Shader.PropertyToID("_UeContourStep"),
            pGA = Shader.PropertyToID("_UeGradeA"), pGB = Shader.PropertyToID("_UeGradeB"), pGC = Shader.PropertyToID("_UeGradeC"), pV1 = Shader.PropertyToID("_UeVig1"), pV2 = Shader.PropertyToID("_UeVig2"),
            pThumbCam = Shader.PropertyToID("_UeThumbCam"), pThumbSize = Shader.PropertyToID("_UeThumbSize");

        // ------------------------------------------------------------------------------------------------ the quad
        public void Init(OzUnitEditor editor)
        {
            I = this; ed = editor; rts = ed.Rts;
            cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            Made = false; MotionFrames = 0;
            var sh = Resources.Load<Shader>(ShaderRes);
            ShaderState = sh == null ? "MISSING (Resources/" + ShaderRes + ")" : !sh.isSupported ? "NOT SUPPORTED on this graphics card" : "OK";
            if (sh == null || !sh.isSupported) { Debug.LogError("[OpenZ UnitEditor] the floor shader " + ShaderState + ": the playground has no floor"); return; }
            mat = new Material(sh) { name = "UE_Floor (runtime)" };
            quad = new GameObject("UE_Floor");
            quad.transform.position = new Vector3(HX, 0f, -HZ);
            var mf = quad.AddComponent<MeshFilter>(); var mr = quad.AddComponent<MeshRenderer>();
            mf.sharedMesh = QuadMesh();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            Made = true;
            ApplyStyle();
            OzUeBackdrop.Changed += OnBackdrop;
            Debug.Log($"[OpenZ UnitEditor] floor: {QuadSize:0} m quad, style {OzUeBackdrop.Style + 1}. {OzUeBackdrop.Names[OzUeBackdrop.Style]}, lines {(OzUeBackdrop.FloorLines ? "on" : "off")}, motion {(OzUeBackdrop.Motion ? "on" : "off")}");
        }

        static Mesh QuadMesh()
        {
            float h = QuadSize * 0.5f;
            var m = new Mesh { name = "UE_FloorQuad" };
            m.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(QuadSize, 1f, QuadSize));
            return m;
        }

        void OnBackdrop() { styleShown = -1; }

        void OnDestroy()
        {
            OzUeBackdrop.Changed -= OnBackdrop;
            if (mat) Destroy(mat);
            if (quad) Destroy(quad);
            if (I == this) I = null;
        }

        /// <summary>The style's colours and numbers on a floor or thumbnail material.</summary>
        public static void StyleUniforms(Material m, int style)
        {
            if (!m) return;
            var L = OzUeBackdrop.Looks[Mathf.Clamp(style, 0, OzUeBackdrop.StyleCount - 1)];
            m.SetFloat(pStyle, style);
            m.SetVector(pBase, Srgb(L.floor)); m.SetVector(pTint, Srgb(L.tint)); m.SetVector(pHi, Srgb(L.hi));
            m.SetVector(pC1, Srgb(L.c1)); m.SetVector(pC2, Srgb(L.c2)); m.SetVector(pC3, Srgb(L.c3));
            m.SetFloat(pStep, OzUeBackdrop.ContourStep);
        }
        // sRGB values as they are (the shaders composite in display colour)
        static Vector4 Srgb(Color32 c) => new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f);

        void ApplyStyle()
        {
            int s = OzUeBackdrop.Style;
            StyleUniforms(mat, s);
            styleShown = s;
            if (cam) { var b = OzUeBackdrop.Looks[s].floor; cam.backgroundColor = new Color(b.r / 255f, b.g / 255f, b.b / 255f, 1f); }
        }

        // ------------------------------------------------------------------------------------------------ the frame
        void LateUpdate()
        {
            if (!mat || !cam) return;
            if (styleShown != OzUeBackdrop.Style) ApplyStyle();
            int style = OzUeBackdrop.Style;
            bool lines = OzUeBackdrop.FloorLines, motion = OzUeBackdrop.Motion;
            if (motion && OzUeControls.Playing) { motionT += Time.unscaledDeltaTime; MotionFrames++; }   // the effect stands still while paused
            mat.SetVector(pOpts, new Vector4(lines ? 1f : 0f, motion ? 1f : 0f, 0f, 0f));
            mat.SetFloat(pTime, motionT);

            // the field = the screen under the top bar (the prototype's canvas); the free field's middle carries the light
            float k = Mathf.Max(0.01f, OzUnitEditor.PxPerUnit), topPx = Mathf.Min(Screen.height - 1f, TopInset() * k);
            float W = Mathf.Max(1f, Screen.width), H = Mathf.Max(1f, Screen.height), fh = Mathf.Max(1f, H - topPx);
            mat.SetVector(pField, new Vector4(0f, topPx / H, 1f, fh / H));
            var ff = OzUeInsets.FreeFieldScreen;
            Vector2 c = ff.width > 1f && ff.height > 1f ? ff.center : new Vector2(W * 0.5f, H * 0.5f);
            mat.SetVector(pCentre, new Vector4(c.x / W, (H - c.y - topPx) / fh, 0f, 0f));

            var view = GameView(topPx, W, H, fh, c);
            Motion(style, motionT, view, out var mA, out var mB);
            mat.SetVector(pMA, mA); mat.SetVector(pMB, mB);

            // Blueprint: the floor point under the mouse (not over a panel)
            var m = Mouse.current; var hud = OpenZHud.I;
            Vector4 mouse = Vector4.zero; Vector2 mp = Vector2.zero; Vector3 g = default;
            bool mouseOn = style == 7 && lines && m != null && !(hud && hud.PointerOverUi) && Application.isFocused;
            if (mouseOn) { mp = m.position.ReadValue(); mouseOn = mp.x >= 0f && mp.y >= 0f && mp.x <= W && mp.y <= H && GroundAt(mp, out g); }
            if (mouseOn) mouse = new Vector4(g.x - HX, g.z + HZ, 1f, 0f);
            mat.SetVector(pMouse, mouse);

            // the far edge's haze: only when the view is not turned and the edge (Z = 120 m) is on screen
            Vector4 haze = Vector4.zero;
            if (lines && !(rts && rts.UeTurned))
            {
                var far = cam.WorldToScreenPoint(new Vector3(rts ? rts.focus.x : HX, 0f, HZ + FD - HZ));
                float yE = H - far.y - topPx;
                if (far.z > 0f && yE > 2f && yE < fh) haze = new Vector4(yE / fh, Mathf.Max(12f, fh * 0.07f) / fh, 1f, 0f);
            }
            mat.SetVector(pHaze, haze);
            Grade();
            TickLabels(style, lines, mouseOn, mp, g, view, topPx, k);
        }

        float TopInset() { float t = OzUeInsets.Get(UeSide.Top); return t > 0.5f ? t : 40f; }

        bool GroundAt(Vector2 screen, out Vector3 p)
        {
            p = default;
            var r = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (r.direction.y > -1e-4f) return false;
            p = r.origin + r.direction * (-r.origin.y / r.direction.y); p.y = 0f;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the light effect's parameters
        /// <summary>What the effect needs of a view: the visible floor (style metres x0, x1, z0, z1), the view's target, whether it is turned,
        /// a projection of a floor point into the field (pixels from its top-left, the pixels per metre there), the field's size.</summary>
        public struct View
        {
            public Vector4 vis; public Vector2 target; public bool turned; public float sinTilt; public Vector2 field;
            public Func<Vector2, Vector3> project;                   // (X, Z) -> (x, y, px per metre); z <= 0 = not on screen
        }

        // the game view's projection for this frame (set by GameView; one delegate made once: nothing is allocated per frame)
        readonly Vector2[] corners = new Vector2[4];
        Vector3 projRight; float projH, projTop; Func<Vector2, Vector3> projectGame;

        View GameView(float topPx, float W, float H, float fh, Vector2 centre)
        {
            var v = new View { field = new Vector2(W, fh), turned = rts && rts.UeTurned, sinTilt = Mathf.Sin((rts ? rts.Tilt : 55f) * Mathf.Deg2Rad) };
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            corners[0] = new Vector2(0f, H - topPx); corners[1] = new Vector2(W, H - topPx); corners[2] = new Vector2(0f, 0f); corners[3] = new Vector2(W, 0f);
            foreach (var s in corners)
            {
                if (!GroundAt(s, out var p)) { var r = cam.ScreenPointToRay(s); p = r.origin + new Vector3(r.direction.x, 0f, r.direction.z).normalized * 2000f; }
                x0 = Mathf.Min(x0, p.x - HX); x1 = Mathf.Max(x1, p.x - HX); z0 = Mathf.Min(z0, p.z + HZ); z1 = Mathf.Max(z1, p.z + HZ);
            }
            v.vis = new Vector4(x0, x1, z0, z1);
            v.target = GroundAt(centre, out var tg) ? new Vector2(tg.x - HX, tg.z + HZ) : new Vector2(0f, 0f);
            var right = cam.transform.right; right.y = 0f; projRight = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            projH = H; projTop = topPx;
            v.project = projectGame ??= ProjectGame;
            return v;
        }

        /// <summary>A floor point (X, Z) -> its pixel in the field (from the field's top-left) and the pixels per metre sideways there.</summary>
        Vector3 ProjectGame(Vector2 P)
        {
            var w = new Vector3(P.x + HX, 0f, P.y - HZ);
            var s0 = cam.WorldToScreenPoint(w); var s1 = cam.WorldToScreenPoint(w + projRight);
            if (s0.z <= 0f) return new Vector3(0f, 0f, 0f);
            float q = new Vector2(s1.x - s0.x, s1.y - s0.y).magnitude;
            return new Vector3(s0.x, projH - s0.y - projTop, Mathf.Max(q, 1e-4f));
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>The prototype's hash (hash: the same integer mixing as the shader's UeHash).</summary>
        public static float Hash(int i, int j, int s)
        {
            uint h = unchecked((uint)i * 374761393u + (uint)j * 668265263u + (uint)s * 1442695041u);
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            return (h ^ (h >> 16)) / 4294967296f;
        }

        /// <summary>This frame's parameters of the style's slow light effect (THEMES[i].motion), for the floor and the thumbnails.</summary>
        public static void Motion(int style, float t, View v, out Vector4 a, out Vector4 b)
        {
            a = Vector4.zero; b = Vector4.zero;
            float vx0 = v.vis.x, vx1 = v.vis.y, vz0 = v.vis.z, vz1 = v.vis.w;
            bool In(Vector2 P) { var s = v.project(P); return s.z > 0f && s.x >= -24f && s.x <= v.field.x + 24f && s.y >= -24f && s.y <= v.field.y + 24f; }
            switch (style)
            {
                case 0: { float f = Frac(t / 14f); a = new Vector4(vz1 + 8f - f * (vz1 - vz0 + 16f), 4f, 0.15f, 1f); break; }   // a band sweeps far to near
                case 2: case 10:                                                                                     // 1.3 item 26: 11. Snow field = Micro-hex's effect
                {   // a ring of cells round a random cell lights up and fades, one every 2 s
                    int n = Mathf.FloorToInt(t / 2f); float ph = Frac(t / 2f), cs = 1.5f * 0.5773503f;
                    float X = vx0 + (vx1 - vx0) * (0.2f + 0.6f * Hash(n, 1, 3)), Z = vz0 + (vz1 - vz0) * (0.15f + 0.7f * Hash(n, 2, 3));
                    if (v.turned && !In(new Vector2(X, Z))) break;
                    int ci = Mathf.RoundToInt(X / cs); float off = (ci & 1) != 0 ? 0.5f : 0f;
                    float cj = Mathf.Round(Z - off);
                    a = new Vector4(ci * cs, cj + off, 0.07f * Mathf.Sin(Mathf.PI * ph), 1f);
                    break;
                }
                case 3: case 11: a = new Vector4(-Frac(t / 20f) * 60f * 4f, 0f, 0f, 1f); break;                         // the dash offset (px); 12. Desert dunes = Topographic's
                case 4: a = new Vector4(Frac(t / 10f) * Mathf.PI * 2f, 0f, 0f, 1f); break;                               // the sweep's angle
                case 5: { float f = Frac(t / 9f); a = new Vector4(vz0 - 4f + f * (vz1 - vz0 + 8f), 2f, 0.4f, 1f); break; }   // a band runs near to far
                case 6:
                {   // a soft light patch drifts across
                    float f = Frac(t / 18f), X = vx0 - 20f + f * (vx1 - vx0 + 40f), Z = (vz0 + vz1) * 0.5f + (vz1 - vz0) * 0.22f * Mathf.Sin(f * Mathf.PI * 2f + 1f);
                    float r = Mathf.Max(14f, (vx1 - vx0) * 0.16f);
                    if (v.turned && !In(new Vector2(X, Z))) break;
                    var s = v.project(new Vector2(X, Z));
                    if (s.z <= 0f) break;
                    float rx = r * s.z, ry = rx * v.sinTilt;
                    if (s.x + rx < 0f || s.x - rx > v.field.x || s.y + ry < 0f || s.y - ry > v.field.y || rx < 2f) break;
                    a = new Vector4(s.x / v.field.x, s.y / v.field.y, rx / v.field.y, ry / v.field.y);
                    b = new Vector4(0.55f, 0f, 0f, 1f);
                    break;
                }
                case 8: a = new Vector4(0.06f * (0.5f - 0.5f * Mathf.Cos(t / 16f * Mathf.PI * 2f)), 0f, 0f, 1f); break;   // the brackets pulse
                case 9:
                {   // an ember glint along the 75 m line nearest the view's centre
                    float Z = Mathf.Round(v.target.y / 75f) * 75f, f = Frac(t / 12f), X = vx0 - 10f + f * (vx1 - vx0 + 20f);
                    if (Z < vz0 || Z > vz1) break;
                    if (v.turned && (!In(new Vector2(X - 7f, Z)) || !In(new Vector2(X + 7f, Z)))) break;
                    a = new Vector4(Z, X, 1f, 0f);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the thumbnails (E015, spec 8.7)
        /// <summary>A thumbnail material of the popover: the style through the prototype's thumbnail camera, the effect at 4.2 s.</summary>
        public static Material ThumbMaterial(int style)
        {
            var sh = Resources.Load<Shader>(ThumbShaderRes);
            if (sh == null || !sh.isSupported) return null;
            var m = new Material(sh) { name = "UE_FloorThumb_" + style };
            StyleUniforms(m, style);
            const float W = 128f, Hh = 72f, T = 4.2f;
            float pxpm = Mathf.Max(1.6f, W / 58f);
            m.SetVector(pThumbCam, new Vector4(pxpm, 0f, 0f, 0f));
            m.SetVector(pThumbSize, new Vector4(W, Hh, 0f, 0f));
            m.SetVector(pOpts, new Vector4(1f, 1f, 0f, 0f));
            m.SetFloat(pTime, T);
            Motion(style, T, ThumbView(W, Hh, pxpm), out var a, out var b);
            m.SetVector(pMA, a); m.SetVector(pMB, b);
            return m;
        }

        /// <summary>The prototype's camera (Cam, pitch 52, vertical field of view 18, not turned) as a View.</summary>
        public static View ThumbView(float w, float h, float pxpm)
        {
            const float sp = 0.7880108f, cp = 0.6156615f, tan9 = 0.1583844f;
            float F = h * 0.5f / tan9, dist = F / pxpm, Hc = dist * sp, Cz = -dist * cp, D0 = Hc * sp - Cz * cp, U0 = -Hc * cp - Cz * sp, cx = w * 0.5f, cy = h * 0.5f;
            Vector2 U(float x, float y) { float a = (x - cx) / F, b = (y - cy) / F, t = Hc / Mathf.Max(1e-4f, sp + b * cp); return new Vector2(t * a, Cz + t * (cp - b * sp)); }
            var g0 = U(0f, 0f); var g1 = U(w, 0f); var g2 = U(0f, h); var g3 = U(w, h);
            return new View
            {
                vis = new Vector4(Mathf.Min(g0.x, g2.x), Mathf.Max(g1.x, g3.x), g2.y, g0.y), target = Vector2.zero, turned = false, sinTilt = sp, field = new Vector2(w, h),
                project = P => { float q = F / (D0 + P.y * cp); return new Vector3(cx + P.x * q, cy - (U0 + P.y * sp) * q, q); }
            };
        }

        // ------------------------------------------------------------------------------------------------ the post chain (OzUeGradeInverse.hlsl)
        void Grade()
        {
            var cd = cam.GetComponent<UniversalAdditionalCameraData>();
            bool post = cd != null && cd.renderPostProcessing;
            var stack = cd != null && cd.volumeStack != null ? cd.volumeStack : VolumeManager.instance.stack;
            if (!post || stack == null) { mat.SetVector(pGA, new Vector4(1f, 1f, 1f, 0f)); mat.SetVector(pV2, new Vector4(0.5f, 0.5f, 0f, 1f)); return; }
            var ca = stack.GetComponent<ColorAdjustments>(); var wb = stack.GetComponent<WhiteBalance>(); var tm = stack.GetComponent<Tonemapping>(); var vg = stack.GetComponent<Vignette>();
            float expo = ca != null ? Mathf.Pow(2f, ca.postExposure.value) : 1f;
            float con = ca != null ? ca.contrast.value / 100f + 1f : 1f, sat = ca != null ? ca.saturation.value / 100f + 1f : 1f;
            float mode = tm == null ? 3f : tm.mode.value == TonemappingMode.ACES ? 1f : tm.mode.value == TonemappingMode.Neutral ? 2f : 3f;
            mat.SetVector(pGA, new Vector4(expo, con, sat, mode));
            var lms = wb != null ? ColorUtils.ColorBalanceToLMSCoeffs(wb.temperature.value, wb.tint.value) : Vector3.one;
            mat.SetVector(pGB, new Vector4(lms.x, lms.y, lms.z, 0f));
            var cf = ca != null ? ca.colorFilter.value.linear : Color.white;
            mat.SetVector(pGC, new Vector4(cf.r, cf.g, cf.b, 0f));
            if (vg != null && vg.IsActive())
            {
                var vc = vg.color.value;
                mat.SetVector(pV1, new Vector4(vc.r, vc.g, vc.b, vg.rounded.value ? Screen.width / (float)Mathf.Max(1, Screen.height) : 1f));
                mat.SetVector(pV2, new Vector4(vg.center.value.x, vg.center.value.y, vg.intensity.value * 3f, vg.smoothness.value * 5f));
            }
            else mat.SetVector(pV2, new Vector4(0.5f, 0.5f, 0f, 1f));
            if (!gradeWarned)
            {
                gradeWarned = true;
                var asset = UniversalRenderPipeline.asset;
                bool hdr = asset != null && asset.colorGradingMode == ColorGradingMode.HighDynamicRange;
                bool extra = (ca != null && Mathf.Abs(ca.hueShift.value) > 0.01f) || Active<LiftGammaGain>(stack) || Active<ShadowsMidtonesHighlights>(stack)
                             || Active<SplitToning>(stack) || Active<ChannelMixer>(stack) || Active<ColorCurves>(stack);
                Debug.Log($"[OpenZ UnitEditor] floor colours through the post chain: exposure x{expo:0.###}, contrast {con:0.##}, saturation {sat:0.##}, tonemap {(mode == 1f ? "ACES" : mode == 2f ? "Neutral" : "none")}, white balance LMS ({lms.x:0.###}, {lms.y:0.###}, {lms.z:0.###}), vignette {(vg != null && vg.IsActive() ? vg.intensity.value.ToString("0.##") : "off")}, grading {(hdr ? "HDR" : "LDR: the floor's colours are near, not exact")}"
                          + (extra ? "; the profile also uses curves / lift-gamma-gain / split toning / channel mixer / hue shift, which the floor does not take back out" : ""));
            }
        }

        static bool Active<T>(VolumeStack s) where T : VolumeComponent { var c = s.GetComponent<T>(); return c != null && c.active && c.parameters != null && AnyOverride(c); }
        static bool AnyOverride(VolumeComponent c) { foreach (var p in c.parameters) if (p.overrideState) return true; return false; }

        // ------------------------------------------------------------------------------------------------ Blueprint's labels (UI)
        /// <summary>The Blueprint style's metre numbers along the near and far edges and its mouse read-out, as labels of the editor's
        /// field layer (the prototype drew them on its floor canvas, 12 px, c1 at 26 % / 42 %).</summary>
        public void BuildLabels(Transform field)
        {
            labelRoot = Hud.Node("UE_FloorLabels", field); labelRoot.Fill();
            var c1 = (Color)OzUeBackdrop.Looks[7].c1;
            for (int k = 0; k < 30; k++)
            {
                var t = Hud.Label(labelRoot, "M" + k, "", 12f, new Color(c1.r, c1.g, c1.b, 0.26f), 0f, OzStyle.Plain);
                t.SetActive(false); numbers.Add(t);
            }
            mouseLabel = Hud.Label(labelRoot, "Mouse", "", 12f, new Color(c1.r, c1.g, c1.b, 0.42f), 0f, OzStyle.Plain);
            mouseLabel.SetActive(false);
        }

        /// <summary>The rows (Z, m) of Blueprint's metre numbers: inside the near edge and inside the far edge.</summary>
        static readonly float[] EdgeRows = { -HZ + 3f, HZ - 6f };

        void TickLabels(int style, bool lines, bool mouseOn, Vector2 mp, Vector3 g, View v, float topPx, float k)
        {
            if (!labelRoot) return;
            bool on = style == 7 && lines;
            float q0 = on ? v.project(v.target).z : 0f;
            int n = 0;
            if (on && q0 >= 5f)
            {
                foreach (float Z in EdgeRows)
                    for (float X = -HX + 20f; X < HX; X += 20f)
                    {
                        if (n >= numbers.Count) break;
                        if (X < v.vis.x || X > v.vis.y || Z < v.vis.z || Z > v.vis.w) continue;
                        var s = v.project(new Vector2(X, Z));
                        if (s.z <= 0f || s.x < -24f || s.x > v.field.x + 24f || s.y < -24f || s.y > v.field.y + 24f) continue;
                        int val = Mathf.RoundToInt(X + HX);
                        var lab = numbers[n];
                        if (numberShown[n] != val) { numberShown[n] = val; lab.Set(OzText.Tab(val.ToString())); }   // a new text only when it changes
                        n++;
                        lab.At(s.x / k, (s.y + topPx) / k, OzH.C, false, 0f);                       // centred, its top at the point
                        lab.SetActive(true);
                    }
            }
            for (int i = n; i < numbers.Count; i++) numbers[i].SetActive(false);
            if (on && mouseOn)
            {
                int mx = Mathf.RoundToInt(g.x), mz = Mathf.RoundToInt(g.z + 2f * HZ);
                if (mx != mouseShown.x || mz != mouseShown.y) { mouseShown = new Vector2Int(mx, mz); mouseLabel.Set(OzText.Tab(mx + " m  /  " + mz + " m")); }
                float xU = (mp.x + 8f) / k, yU = (Screen.height - mp.y - 6f) / k;                       // its left bottom 8 px right of / 6 px over the mouse
                mouseLabel.At(xU, yU - 12f * 0.5f, OzH.L, true, 0f);
                mouseLabel.SetActive(true);
            }
            else mouseLabel.SetActive(false);
        }

        /// <summary>Counted check: the floor's shader and the thumbnails' shader load and run here.</summary>
        public static int SelfCheckAssets(System.Text.StringBuilder log, Func<System.Text.StringBuilder, bool, string, int> line)
        {
            int f = 0;
            var a = Resources.Load<Shader>(ShaderRes); var b = Resources.Load<Shader>(ThumbShaderRes);
            f += line(log, a != null && a.isSupported && a.name == "OpenZ/UeFloor", $"floor shader Resources/{ShaderRes}: {(a == null ? "missing" : a.name + (a.isSupported ? ", supported" : ", NOT supported"))}");
            f += line(log, b != null && b.isSupported && b.name == "OpenZ/UI/UeFloorThumb", $"thumbnail shader Resources/{ThumbShaderRes}: {(b == null ? "missing" : b.name + (b.isSupported ? ", supported" : ", NOT supported"))}");
            return f;
        }
    }
}
