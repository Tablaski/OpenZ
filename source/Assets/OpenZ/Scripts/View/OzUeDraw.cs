using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): world marks drawn for THIS frame only, in the game's world-overlay style
    /// (UI_STYLE_GUIDE rule 1 / 2: INK or team ring colours with a dark keyline, never amber): dashed rings, lines and crosses ON THE
    /// FLOOR (one dynamic mesh with the game's OpenZ/Overlay material, depth-tested so units cover them, as SelectionOverlay's rings),
    /// labels and small bars over the field (OzText / Images on the editor's Field layer, under every panel). Sizes are canvas units
    /// (u = px at 1080 lines, the HUD scale included): a 1 u line stays one pixel wide at every zoom and every turn of the camera.
    /// Calls come from Update / LateUpdate (marks, drop preview); OzUeDrawDriver (order 900, after the camera moved) builds the frame.
    /// </summary>
    public static class OzUeDraw
    {
        internal struct Stroke { public int start, count; public bool closed; public Color c; public float dash, gap, width; }
        internal struct Text { public Vector3 at; public string text; public Color c; public float size, up; public bool plate; }
        internal struct Bar { public Vector3 at; public float up, w, h, k; public Color fill; }
        internal static readonly List<Vector3> Pts = new List<Vector3>(2048);
        internal static readonly List<Stroke> Strokes = new List<Stroke>(64);
        internal static readonly List<Text> Texts = new List<Text>(32);
        internal static readonly List<Bar> Bars = new List<Bar>(16);
        /// <summary>Checks: strokes, labels and bars drawn in the last frame.</summary>
        public static int LastStrokes, LastLabels, LastBars;
        /// <summary>Checks (the frame-cost check, View/OzUeStallCheck.cs): the dash walk's steps in the last frame and their bound (the
        /// sum of View/OzUeDash.cs Bound over the parts on the screen), the frames whose dashes ran out of the step budget (drawn solid
        /// from there on), the mark mesh's vertices in the last frame.</summary>
        public static int LastDashSteps, LastDashBound, DashBudgetFrames, LastVertices;
        /// <summary>The most dash steps of one frame: a stroke past it is drawn solid, so no world mark can ever hold a frame.</summary>
        public const int MaxDashSteps = 40000;

        /// <summary>A dashed (gap 0 = solid) circle on the floor round `centre`, radius in metres.</summary>
        public static void Ring(Vector3 centre, float radiusM, Color c, float dashPx, float gapPx, float widthPx)
        {
            if (!(radiusM > 0.01f) || c.a <= 0.003f || !OzUnitEditor.Active) return;
            float px = RadiusPx(centre, radiusM);
            int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * px / 5f), 24, 256);
            int s = Pts.Count;
            for (int i = 0; i < n; i++) { float a = i * (2f * Mathf.PI / n); Pts.Add(new Vector3(centre.x + Mathf.Cos(a) * radiusM, centre.y, centre.z + Mathf.Sin(a) * radiusM)); }
            Strokes.Add(new Stroke { start = s, count = n, closed = true, c = c, dash = dashPx, gap = gapPx, width = widthPx });
        }

        /// <summary>A dashed (gap 0 = solid) line on the floor from a to b.</summary>
        public static void Line(Vector3 a, Vector3 b, Color c, float dashPx, float gapPx, float widthPx)
        {
            if (c.a <= 0.003f || !OzUnitEditor.Active) return;
            int s = Pts.Count;
            int n = Mathf.Clamp(Mathf.CeilToInt((b - a).magnitude / 4f), 1, 64);                              // long lines in short pieces (the dash walk is in screen space)
            for (int i = 0; i <= n; i++) Pts.Add(Vector3.Lerp(a, b, i / (float)n));
            Strokes.Add(new Stroke { start = s, count = n + 1, closed = false, c = c, dash = dashPx, gap = gapPx, width = widthPx });
        }

        /// <summary>A small solid x of `sizeU` canvas units lying on the floor at `at`.</summary>
        public static void Cross(Vector3 at, float sizeU, Color c)
        {
            if (c.a <= 0.003f || !OzUnitEditor.Active) return;
            var cam = Cam; if (!cam) return;
            Vector3 sp = cam.WorldToScreenPoint(at); if (sp.z <= 0.05f) return;
            float h = 0.5f * sizeU * OzUnitEditor.PxPerUnit;
            if (!Floor(cam, new Vector2(sp.x - h, sp.y - h), at.y, out var a0) || !Floor(cam, new Vector2(sp.x + h, sp.y + h), at.y, out var a1)
                || !Floor(cam, new Vector2(sp.x - h, sp.y + h), at.y, out var b0) || !Floor(cam, new Vector2(sp.x + h, sp.y - h), at.y, out var b1)) return;
            Line(a0, a1, c, 0f, 0f, 1f); Line(b0, b1, c, 0f, 0f, 1f);
        }

        /// <summary>A world label (12 u caps in the guide): `upU` canvas units above the point, the game's outline text style.</summary>
        public static void Label(Vector3 world, string text, Color c, float sizeU, float upU) => Label(world, text, c, sizeU, upU, false);

        /// <summary>A world label; plate = on a dark plate as wide as the text (the drop preview's words: readable over the see-through
        /// models and the fight). upU = canvas units from the world point's screen point to the label's box bottom (the plate reaches
        /// 2 u past the box over and under it: PlateH).</summary>
        public static void Label(Vector3 world, string text, Color c, float sizeU, float upU, bool plate)
        {
            if (string.IsNullOrEmpty(text) || c.a <= 0.003f || !OzUnitEditor.Active) return;
            Texts.Add(new Text { at = world, text = text, c = c, size = Mathf.Max(sizeU, OzText.MinSize), up = upU, plate = plate });
        }

        /// <summary>A plate label's height (canvas u): its text box (1.5 x the size) and 2 u over and under it.</summary>
        public static float PlateH(float sizeU) => Mathf.Max(sizeU, OzText.MinSize) * 1.5f + 4f;

        /// <summary>A small health bar (w x h u, dark 1 u frame) `upU` above a world point; k = 0..1 filled.</summary>
        public static void HealthBar(Vector3 world, float upU, float wU, float hU, float k, Color fill)
        {
            if (!OzUnitEditor.Active) return;
            Bars.Add(new Bar { at = world, up = upU, w = wU, h = hU, k = Mathf.Clamp01(k), fill = fill });
        }

        /// <summary>A dashed rectangle on the floor (the obstacle's outline): half sizes in metres.</summary>
        public static void RectOutline(Vector3 centre, float halfX, float halfZ, Color c, float dashPx, float gapPx, float widthPx)
        {
            if (c.a <= 0.003f || !OzUnitEditor.Active) return;
            int s = Pts.Count;
            for (int k = 0; k < 4; k++)
            {
                Vector3 a = centre + Corner(k, halfX, halfZ), b = centre + Corner((k + 1) % 4, halfX, halfZ);
                int n = Mathf.Clamp(Mathf.CeilToInt((b - a).magnitude / 4f), 1, 32);
                for (int i = 0; i < n; i++) Pts.Add(Vector3.Lerp(a, b, i / (float)n));
            }
            Strokes.Add(new Stroke { start = s, count = Pts.Count - s, closed = true, c = c, dash = dashPx, gap = gapPx, width = widthPx });
        }

        static Vector3 Corner(int k, float hx, float hz) => k == 0 ? new Vector3(-hx, 0, -hz) : k == 1 ? new Vector3(hx, 0, -hz) : k == 2 ? new Vector3(hx, 0, hz) : new Vector3(-hx, 0, hz);

        /// <summary>A vertical edge (the drop preview's box outline): from the floor point up `heightM` metres, solid or dashed.</summary>
        public static void Up(Vector3 floor, float heightM, Color c, float widthPx)
        {
            if (c.a <= 0.003f || !OzUnitEditor.Active) return;
            int s = Pts.Count;
            Pts.Add(floor); Pts.Add(floor + Vector3.up * heightM);
            Strokes.Add(new Stroke { start = s, count = 2, closed = false, c = c, dash = 0f, gap = 0f, width = widthPx });
        }

        /// <summary>Metres per screen pixel at a world point (perspective depth).</summary>
        public static float MetresPerPixel(Camera cam, Vector3 p)
        {
            if (!cam) return 0.1f;
            float depth = Mathf.Max(0.05f, Vector3.Dot(p - cam.transform.position, cam.transform.forward));
            if (cam.orthographic) return 2f * cam.orthographicSize / Mathf.Max(1, Screen.height);
            return 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
        }

        static float RadiusPx(Vector3 c, float r) { var cam = Cam; return cam ? r / Mathf.Max(0.0005f, MetresPerPixel(cam, c)) : 60f; }

        /// <summary>The floor point (plane y = h) under a screen point.</summary>
        public static bool Floor(Camera cam, Vector2 screen, float h, out Vector3 p)
        {
            p = default;
            if (!cam) return false;
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = (h - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return false;
            p = ray.origin + ray.direction * t; return true;
        }

        internal static Camera Cam { get { var ed = OzUnitEditor.I; var c = ed && ed.Rts ? ed.Rts.GetComponent<Camera>() : null; return c ? c : Camera.main; } }

        internal static void Clear() { Pts.Clear(); Strokes.Clear(); Texts.Clear(); Bars.Clear(); }
    }

    /// <summary>Builds OzUeDraw's frame after everything moved (order 900, LateUpdate): the floor mesh and the label / bar pools.</summary>
    [DefaultExecutionOrder(900)]
    internal sealed class OzUeDrawDriver : MonoBehaviour
    {
        Mesh mesh; Material mat;
        readonly List<Vector3> v = new List<Vector3>(4096);
        readonly List<Color> col = new List<Color>(4096);
        readonly List<int> keyTris = new List<int>(8192), colTris = new List<int>(8192), all = new List<int>(16384);
        readonly List<Vector3> sp = new List<Vector3>(512);
        sealed class LabelView { public RectTransform rt; public OzText t; public float size; public RectTransform plate; public string plateFor; public float plateSize; }
        readonly List<float> pieces = new List<float>(256);                                                // one segment's dash parts (View/OzUeDash.cs Walk)
        const float ScreenMargin = 64f, PlatePadX = 8f;
        static readonly Color PlateFill = OzUi.A(OzUi.GM950, 0.88f), PlateEdge = OzUi.A(OzUi.GM500, 0.9f);
        sealed class BarView { public RectTransform rt; public Image frame, fill; }
        readonly List<LabelView> labels = new List<LabelView>();
        readonly List<BarView> bars = new List<BarView>();
        int labelsOn, barsOn;
        RectTransform layer;

        void Awake()
        {
            mesh = new Mesh { name = "OzUe_WorldMarks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            Material baseMat = null;
            var so = FindFirstObjectByType<SelectionOverlay>();
            if (so && so.overlayMaterial) baseMat = so.overlayMaterial;
            if (baseMat) mat = new Material(baseMat) { name = "OzUe_WorldMarks" };
            else { var sh = Shader.Find("OpenZ/Overlay"); if (sh) mat = new Material(sh) { name = "OzUe_WorldMarks" }; }
            if (mat) { mat.SetColor("_Color", Color.white); mat.SetFloat("_ZTest", 4f); mat.SetFloat("_Scan", 0f); }
            else Debug.LogWarning("[OpenZ UnitEditor] the OpenZ/Overlay material is missing: world marks are not drawn (labels still are)");
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (mat) Destroy(mat);
            OzUeDraw.Clear();
        }

        void LateUpdate()
        {
            var ed = OzUnitEditor.I;
            bool show = ed && OzUnitEditor.UiBuilt && (!ed.Hud || ed.Hud.hudVisible);
            var cam = OzUeDraw.Cam;
            if (!show || !cam) { HideFrom(0, 0); OzUeDraw.Clear(); return; }
            if (!layer) layer = OzUnitEditor.Layer(UeLayer.Field);
            BuildMesh(cam);
            PlaceLabels(cam, ed);
            OzUeDraw.LastStrokes = OzUeDraw.Strokes.Count; OzUeDraw.LastLabels = OzUeDraw.Texts.Count; OzUeDraw.LastBars = OzUeDraw.Bars.Count;
            OzUeDraw.Clear();
        }

        // ------------------------------------------------------------------ the floor strokes
        void BuildMesh(Camera cam)
        {
            v.Clear(); col.Clear(); keyTris.Clear(); colTris.Clear();
            Vector3 camPos = cam.transform.position;
            float k = OzUnitEditor.PxPerUnit;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var pts = OzUeDraw.Pts;
            bool light = OzUeBackdrop.LightFloor;                                                         // 1.3 item 26: a light floor's dark ink
            int budget = OzUeDraw.MaxDashSteps, steps = 0; long bound = 0;
            float sw = Screen.width, sh = Screen.height;
            foreach (var s in OzUeDraw.Strokes)
            {
                Color c = s.c, kc = new Color(5f / 255f, 6f / 255f, 6f / 255f, Mathf.Min(0.6f, s.c.a * 0.8f));
                if (light) { c = OzUeBackdrop.Ink(s.c); kc = OzUeBackdrop.InkKeyline(s.c.a); }                // the mark dark, its keyline pale
                if (linear) { c = Lin(c); kc = Lin(kc); }
                float w = Mathf.Max(1f, s.width * k), dash = s.dash * k, gap = s.gap * k;
                bool solid = gap <= 0.01f || dash <= 0.01f;
                int segs = s.closed ? s.count : s.count - 1;
                sp.Clear();
                for (int i = 0; i < s.count; i++) sp.Add(cam.WorldToScreenPoint(pts[s.start + i]));
                double phase = 0.0;                                                                       // the dash pattern's place along the stroke (View/OzUeDash.cs)
                for (int i = 0; i < segs; i++)
                {
                    int i0 = i, i1 = (i + 1) % s.count;
                    Vector3 a = pts[s.start + i0], b = pts[s.start + i1], sa = sp[i0], sb = sp[i1];
                    if (sa.z <= cam.nearClipPlane || sb.z <= cam.nearClipPlane) { phase = 0.0; continue; }       // nothing behind the camera is drawn
                    float len = Vector2.Distance(sa, sb);
                    if (len < 0.001f) continue;
                    if (solid) { Ribbon(cam, camPos, a, b, w + 2f, kc, keyTris); Ribbon(cam, camPos, a, b, w, c, colTris); continue; }
                    // the dashes walk only the part on the screen (and a margin), with OzUeDash's bounded walk: the walk before stalled on
                    // float rounding when the dash had a fractional length (0.85 px per u at 1366 x 768: minutes per frame with a fight)
                    if (!OnScreen(sa, sb, sw, sh, out float c0, out float c1)) { phase = OzUeDash.Advance(phase, len, dash, gap); continue; }
                    phase = OzUeDash.Advance(phase, c0 * len, dash, gap);
                    pieces.Clear();
                    steps += OzUeDash.Walk(c0 * len, c1 * len, dash, gap, ref phase, pieces, ref budget);
                    bound += OzUeDash.Bound((c1 - c0) * len, dash, gap);
                    phase = OzUeDash.Advance(phase, (1f - c1) * len, dash, gap);
                    for (int j = 0; j + 1 < pieces.Count; j += 2)
                    {
                        Vector3 p0 = Vector3.Lerp(a, b, pieces[j] / len), p1 = Vector3.Lerp(a, b, pieces[j + 1] / len);
                        Ribbon(cam, camPos, p0, p1, w + 2f, kc, keyTris); Ribbon(cam, camPos, p0, p1, w, c, colTris);
                    }
                }
            }
            OzUeDraw.LastDashSteps = steps; OzUeDraw.LastDashBound = bound > int.MaxValue ? int.MaxValue : (int)bound;
            if (budget <= 0) OzUeDraw.DashBudgetFrames++;
            OzUeDraw.LastVertices = v.Count;
            mesh.Clear();
            if (v.Count == 0 || !mat) return;
            all.Clear(); all.AddRange(keyTris); all.AddRange(colTris);                                         // every keyline under every coloured line
            mesh.SetVertices(v); mesh.SetColors(col); mesh.SetTriangles(all, 0, true);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, cam, 0, null, false, false, false);           // the field's camera only (never a thumbnail camera)
        }

        static Color Lin(Color c) { var l = c.linear; l.a = c.a; return l; }

        /// <summary>The part c0 .. c1 (of its length) of the screen segment sa .. sb that lies on the screen or within a margin of it
        /// (the segment clipped edge by edge); false when none does.</summary>
        static bool OnScreen(Vector3 sa, Vector3 sb, float sw, float sh, out float c0, out float c1)
        {
            c0 = 0f; c1 = 1f;
            float dx = sb.x - sa.x, dy = sb.y - sa.y;
            return Clip(-dx, sa.x + ScreenMargin, ref c0, ref c1) && Clip(dx, sw + ScreenMargin - sa.x, ref c0, ref c1)
                && Clip(-dy, sa.y + ScreenMargin, ref c0, ref c1) && Clip(dy, sh + ScreenMargin - sa.y, ref c0, ref c1) && c1 - c0 > 1e-6f;
        }

        static bool Clip(float p, float q, ref float c0, ref float c1)
        {
            if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > c1) return false; if (r > c0) c0 = r; }
            else { if (r < c0) return false; if (r < c1) c1 = r; }
            return true;
        }

        /// <summary>A camera-facing strip from a to b, w screen pixels wide, lifted so its lower edge stays on the floor.</summary>
        void Ribbon(Camera cam, Vector3 camPos, Vector3 a, Vector3 b, float wPx, Color c, List<int> tris)
        {
            Vector3 dir = b - a, mid = (a + b) * 0.5f;
            Vector3 side = Vector3.Cross(dir, mid - camPos);
            float m = side.magnitude; if (m < 1e-6f) return;
            side /= m;
            float ha = 0.5f * wPx * OzUeDraw.MetresPerPixel(cam, a), hb = 0.5f * wPx * OzUeDraw.MetresPerPixel(cam, b);
            float up = Mathf.Abs(side.y);
            Vector3 la = a + Vector3.up * (0.06f + ha * up), lb = b + Vector3.up * (0.06f + hb * up);
            int i = v.Count;
            v.Add(la - side * ha); v.Add(la + side * ha); v.Add(lb + side * hb); v.Add(lb - side * hb);
            col.Add(c); col.Add(c); col.Add(c); col.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // ------------------------------------------------------------------ labels and bars (screen space, on the Field layer)
        void PlaceLabels(Camera cam, OzUnitEditor ed)
        {
            var hud = ed.Hud;
            int nl = 0, nb = 0;
            if (layer && hud)
            {
                foreach (var t in OzUeDraw.Texts)
                {
                    Vector3 s = cam.WorldToScreenPoint(t.at);
                    if (s.z <= cam.nearClipPlane) continue;
                    var lv = nl < labels.Count ? labels[nl] : NewLabel();
                    nl++;
                    if (!lv.rt.gameObject.activeSelf) lv.rt.gameObject.SetActive(true);
                    if (!Mathf.Approximately(lv.size, t.size)) { lv.size = t.size; lv.t.FontSize = t.size; lv.rt.sizeDelta = new Vector2(900f, t.size * 1.5f); }
                    lv.t.Set(t.text);
                    lv.t.color = t.c;
                    Plate(lv, t);
                    Vector2 p = hud.ScreenToCanvas(s);
                    lv.rt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y + t.up));
                }
                foreach (var b in OzUeDraw.Bars)
                {
                    Vector3 s = cam.WorldToScreenPoint(b.at);
                    if (s.z <= cam.nearClipPlane) continue;
                    var bv = nb < bars.Count ? bars[nb] : NewBar();
                    nb++;
                    if (!bv.rt.gameObject.activeSelf) bv.rt.gameObject.SetActive(true);
                    Vector2 p = hud.ScreenToCanvas(s);
                    bv.rt.sizeDelta = new Vector2(b.w + 2f, b.h + 2f);
                    bv.rt.anchoredPosition = new Vector2(Mathf.Round(p.x - b.w * 0.5f - 1f), Mathf.Round(p.y + b.up));
                    var fr = bv.fill.rectTransform;
                    fr.offsetMin = new Vector2(1f, 1f); fr.offsetMax = new Vector2(-1f - b.w * (1f - b.k), -1f);
                    if (bv.fill.color != b.fill) bv.fill.color = b.fill;
                }
            }
            HideFrom(nl, nb);
        }

        void HideFrom(int nl, int nb)
        {
            for (int i = nl; i < labels.Count; i++) if (labels[i].rt && labels[i].rt.gameObject.activeSelf) labels[i].rt.gameObject.SetActive(false);
            for (int i = nb; i < bars.Count; i++) if (bars[i].rt && bars[i].rt.gameObject.activeSelf) bars[i].rt.gameObject.SetActive(false);
            labelsOn = nl; barsOn = nb;
        }

        LabelView NewLabel()
        {
            var rt = Hud.Node("OzUe_WorldLabel", layer);
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(900f, 18f);
            var plate = Hud.Node("Plate", rt);                                                             // made before the text: drawn under it
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f); plate.pivot = new Vector2(0.5f, 0.5f); plate.anchoredPosition = Vector2.zero; plate.sizeDelta = new Vector2(40f, 22f);
            var fill = Hud.Img(plate, "Fill", PlateFill); fill.rectTransform.Fill(); fill.raycastTarget = false;
            foreach (var e in Hud.Border(plate, PlateEdge, 1f)) e.raycastTarget = false;
            plate.gameObject.SetActive(false);
            var t = Hud.Label(rt, "Text", "", 12, OzUi.Ink, 0f, OzStyle.Outline).FillParent(OzH.C);
            var lv = new LabelView { rt = rt, t = t, size = 12, plate = plate };
            labels.Add(lv); return lv;
        }

        /// <summary>The label's dark plate (OzUeDraw.Label with plate): its text's width + 8 u each side, PlateH tall, centred on the
        /// label's box; measured again only when the text or its size changed. 1.3 item 26: on a light floor every field label has it (the
        /// light words read on it; plain outlined words on a light floor do not).</summary>
        static void Plate(LabelView lv, OzUeDraw.Text t)
        {
            bool on = t.plate || OzUeBackdrop.LightFloor;
            if (lv.plate.gameObject.activeSelf != on) lv.plate.gameObject.SetActive(on);
            if (!on || (lv.plateFor == t.text && Mathf.Approximately(lv.plateSize, t.size))) return;
            lv.plateFor = t.text; lv.plateSize = t.size;
            lv.plate.sizeDelta = new Vector2(Mathf.Ceil(lv.t.WidthAt(t.text)) + 2f * PlatePadX, OzUeDraw.PlateH(t.size));
        }

        BarView NewBar()
        {
            var rt = Hud.Node("OzUe_WorldBar", layer);
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero; rt.sizeDelta = new Vector2(32f, 5f);
            var frame = Hud.Img(rt, "Frame", new Color(8f / 255f, 10f / 255f, 9f / 255f, 0.85f)); frame.rectTransform.Fill();
            var fill = Hud.Img(rt, "Fill", OzUi.Go); fill.rectTransform.Fill(1, 1, 1, 1);
            var bv = new BarView { rt = rt, frame = frame, fill = fill };
            bars.Add(bv); return bv;
        }

        /// <summary>Checks: labels / bars on screen in the last frame.</summary>
        internal int LabelsOn => labelsOn;
        internal int BarsOn => barsOn;
    }
}
