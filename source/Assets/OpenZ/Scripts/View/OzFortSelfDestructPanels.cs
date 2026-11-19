using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// hallway, on the side wall next to the lit inner portal, readable from a camera inside the hallway. Decoration only: no collider, no
    /// gameplay, no sound (nothing on it moves or acts), no shadow caster, no light. View only: the Sim is not read for anything but the fort's
    /// state, nothing is written.
    /// <list type="bullet">
    /// <item>Where: the gate kit's hallway (Models/Gates/Fort_Gate.glb, the nodes GateKitView binds; one prefab Models -> Prefabs/Z_fort.prefab for
    /// fort_front and fort_back). Prefab-root metres (+z = out of the door): the hall's left wall (seen walking in: the right-hand wall) is the
    /// plane x = gate_mark_min.x (-1.50), its floor gate_mark_min.y (1.29), the lit inner portal gate_mark_in.z (3.723). The last bay before the
    /// portal is free wall from the portal post's front (z_end + 0.04) to the first rib's back (z_end + 0.53). The panel's centre: z_end +
    /// <see cref="CentreBack"/> (4.008), floor + <see cref="CentreUp"/> (2.79: about 0.4 m under a robot's eye in the view), its back 4 mm off the wall; 0.40 m along the wall x 0.52 m high x
    /// 0.096 m deep (button top). 4.5 cm to the portal post and to the rib, 0.34 m under the wall light strip (README: every gap, measured on
    /// the kit's own mesh).</item>
    /// <item>The build: one shared mesh (yellow-black hazard frame, gunmetal face plate, SELF / DESTRUCT label, yellow collar, red domed button,
    /// four bolts; 230 triangles) and one material per source material: a copy of the hall's own material (OpenZ/TeamLit, the gate kit's
    /// shading and night grade) with a 256 x 256 atlas made here at the first use (no texture asset; the label's letters are a 5 x 7 pixel
    /// font) and its emission map (the letters and the button glow a little, day and night).</item>
    /// <item>Parent: the fort's FactoryView rig (the sibling of the static model, so TeamPaint, the burn and BuildingBreaker's debris palette never
    /// see it). FactoryView.SetShown hides the rig's children the moment the fort is destroyed in the Sim.</item>
    /// <item>Shown only while the hall is: every LateUpdate the panel's renderer follows the gate_hall renderer (BuildingBreaker's collapse turns
    /// it off), FortFallAtGameOver.Fallen (a fort dropped at the game over still stands in the Sim) and BuildingBreaker.Collapsed. So it goes
    /// down with the fort on every path and never hangs in the air where the hall was.</item>
    /// <item>Not seen from the RTS camera: the game's camera looks down at 55 degrees; from 33 degrees down no line from any point of the panel
    /// to it passes the gate's opening, even with the doors open (README: rays against the fort's own mesh). Only the free look tilted under
    /// about 32 degrees, at a moment the gate stands open, could catch the panel's lower part deep in the hall, as it does the rest of the hall.</item>
    /// </list>
    /// Boots itself in every level scene (ZGame present), like FactoryViews; builds a panel on every fort rig that has the gate kit (both teams,
    /// both fort types, every placement) and again after FactoryViews.Rebuild. Off: <see cref="Enabled"/> = false before the level loads.
    /// </summary>
    [DefaultExecutionOrder(32100)]
    public sealed class OzFortSelfDestructPanels : MonoBehaviour
    {
        public static bool Enabled = true;
        public const string PanelName = "OZ_SelfDestructPanel";
        /// <summary>The panel's centre over the hall floor and in front of the inner portal (m), measured on the gate kit (README).</summary>
        public const float CentreUp = 1.528f /* 1.5 kit self_destruct_v2: the bigger panel's centre; its bottom 1.11 m over the floor (hidden from the RTS camera down to 35 deg through the open gate), its top 1.94 m, the beacon above it */, CentreBack = 0.285f;
        /// <summary>The gate kit the numbers were measured on (prefab-root metres): gate_mark_min and gate_mark_in.z. Another kit (2 cm or more off):
        /// no panel (the gaps were proved on this one only).</summary>
        public static readonly Vector3 KitMin = new Vector3(-1.5f, 1.29f, 3.223f);
        public const float KitInZ = 3.723f, KitTolerance = 0.02f;
        /// <summary>Proof counters: panels built this session, forts skipped (and why, the last one).</summary>
        public static int Built { get; private set; }
        public static int Skipped { get; private set; }
        public static string LastSkip { get; private set; } = "";
        public static OzFortSelfDestructPanels I { get; private set; }

        sealed class Entry
        {
            public FactoryView fv; public Transform panel, building; public MeshRenderer mr; public Renderer hall; public BuildingBreaker br;
            public Vector3 pos; public Quaternion rot; public Matrix4x4 at;
        }
        readonly List<Entry> entries = new List<Entry>();
        readonly List<ZObject> forts = new List<ZObject>();
        readonly HashSet<FactoryView> refused = new HashSet<FactoryView>();
        float nextScan;

        // ------------------------------------------------------------------ boot (the FactoryViews pattern)
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
            TryAttach();
        }

        static void TryAttach()
        {
            if (!Enabled || !ZGame.I || FindFirstObjectByType<OzFortSelfDestructPanels>()) return;
            new GameObject("OZ_FortWallPanels").AddComponent<OzFortSelfDestructPanels>();
        }

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        /// <summary>The panel of a fort rig (the check), or null.</summary>
        public static Transform PanelOf(FactoryView fv) => fv ? fv.transform.Find(PanelName) : null;

        // ------------------------------------------------------------------ build (twice a second: the rigs are made in FactoryViews.Start)
        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            var fvs = FactoryViews.I; var g = ZGame.I;
            if (!fvs || !g || g.World == null) return;
            if (forts.Count == 0)
                for (int i = 0; i < g.World.Objects.Count; i++) { var o = g.World.Objects[i]; if (o != null && o.IsFort) forts.Add(o); }
            for (int i = 0; i < forts.Count; i++)
            {
                var fv = fvs.For(forts[i].Id);
                if (!fv || refused.Contains(fv) || PanelOf(fv)) continue;
                var e = Build(fv);
                if (e != null) entries.Add(e); else refused.Add(fv);
            }
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (!e.panel || !e.fv) { entries.RemoveAt(i); continue; }                    // FactoryViews.Rebuild: the rig went with its panel
                if (!e.br && e.building) e.br = e.building.GetComponent<BuildingBreaker>();     // added at the fort's death (BuildingBreaker.For)
            }
        }

        // ------------------------------------------------------------------ shown only while the hall is
        void LateUpdate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.panel || !e.panel.gameObject.activeInHierarchy) continue;            // FactoryView.SetShown hid the rig: destroyed in the Sim
                bool show = e.building && e.hall && e.hall.enabled && e.hall.gameObject.activeInHierarchy && !e.hall.forceRenderingOff
                            && !FortFallAtGameOver.Fallen(e.fv.B) && !(e.br && e.br.Collapsed);
                if (e.mr.enabled != show) e.mr.enabled = show;
                if (show && e.building.localToWorldMatrix != e.at) Place(e);                    // never moves while it stands; kept exact anyway
            }
        }

        Entry Build(FactoryView fv)
        {
            var b = fv.Building; var gate = fv.Gate;
            if (fv.B == null || !fv.B.IsFort || !b || !gate || gate.Key != "fort") return Refuse(fv, "no fort gate kit");
            if (FortFallAtGameOver.Fallen(fv.B)) return Refuse(fv, "the fort is down");
            var hallT = Find(b, "gate_hall"); var minT = Find(b, "gate_mark_min");
            var hall = hallT ? hallT.GetComponent<Renderer>() : null;
            if (!hall || !minT) return Refuse(fv, "no gate_hall renderer / gate_mark_min");
            Vector3 min = b.InverseTransformPoint(minT.position); float zEnd = gate.InPoint.z;
            if (Mathf.Abs(min.x - KitMin.x) > KitTolerance || Mathf.Abs(min.y - KitMin.y) > KitTolerance || Mathf.Abs(min.z - KitMin.z) > KitTolerance
                || Mathf.Abs(zEnd - KitInZ) > KitTolerance)
                return Refuse(fv, $"the gate kit is not the measured one (min {min.x:0.000}, {min.y:0.000}, {min.z:0.000}, portal z {zEnd:0.000})");
            var mat = MaterialFor(hall.sharedMaterial);
            if (!mat) return Refuse(fv, "the hall has no material");
            var go = new GameObject(PanelName); go.transform.SetParent(fv.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = SharedMesh();
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            var e = new Entry
            {
                fv = fv, panel = go.transform, building = b, mr = mr, hall = hall, br = b.GetComponent<BuildingBreaker>(),
                pos = new Vector3(min.x, min.y + CentreUp, zEnd + CentreBack),                       // on the wall plane; the mesh's back is 4 mm off it
                rot = Quaternion.Euler(0f, -90f, 0f),                                               // the mesh's front (-z) faces +x (into the hall), its text runs +z
            };
            Place(e);
            Built++;
#if !OZ_RELEASE
            Debug.Log($"[OpenZ] fort wall panel: {fv.B.Type} #{fv.B.Id} ({fv.B.Owner}), prefab-root ({e.pos.x:0.000}, {e.pos.y:0.000}, {e.pos.z:0.000})");
#endif
            return e;
        }

        static void Place(Entry e)
        {
            var b = e.building;
            e.panel.SetPositionAndRotation(b.TransformPoint(e.pos), b.rotation * e.rot);
            // review: the mesh is in prefab-root metres, so it takes the fort's own (uniform) scale; every placement today is scale 1 (no change then)
            var ps = e.panel.parent ? e.panel.parent.lossyScale.x : 1f; float s = b.lossyScale.x / (Mathf.Abs(ps) > 1e-4f ? ps : 1f);
            if (Mathf.Abs(e.panel.localScale.x - s) > 1e-5f) e.panel.localScale = new Vector3(s, s, s);
            e.at = b.localToWorldMatrix;
        }

        static Entry Refuse(FactoryView fv, string why)
        {
            Skipped++; LastSkip = (fv && fv.B != null ? fv.B.Type + " #" + fv.B.Id + ": " : "") + why;
#if !OZ_RELEASE
            Debug.LogWarning("[OpenZ] fort wall panel skipped: " + LastSkip);
#endif
            return null;
        }

        static Transform Find(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var f = Find(c, n); if (f) return f; }
            return null;
        }

        // ------------------------------------------------------------------ material: the hall's own (TeamLit) with the panel atlas
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), BaseColorId = Shader.PropertyToID("_BaseColor"),
                            EmissionId = Shader.PropertyToID("_EmissionColor"), EmissionMapId = Shader.PropertyToID("_EmissionMap"),
                            EmissionMapColorId = Shader.PropertyToID("_EmissionMapColor"), EmissionNightId = Shader.PropertyToID("_EmissionNight"),
                            SmoothId = Shader.PropertyToID("_Smoothness"), MetallicId = Shader.PropertyToID("_Metallic"), VcId = Shader.PropertyToID("_UseVertexColor"),
                            TeamModeId = Shader.PropertyToID("_TeamMode"), PfaId = Shader.PropertyToID("_PaintFromAlpha"), TriId = Shader.PropertyToID("_Triplanar");
        static readonly Dictionary<Material, Material> mats = new Dictionary<Material, Material>();
        /// <summary>Emission gain over the emission map (linear): the letters and the button glow softly, day and night alike.</summary>
        public const float GlowGain = 1.2f;

        static Material MaterialFor(Material src)
        {
            if (!src) return null;
            if (mats.TryGetValue(src, out var m) && m) return m;
            MakeTextures();
            m = new Material(src) { name = "OZ_SelfDestructPanel" };
            if (m.HasProperty(BaseMapId)) { m.SetTexture(BaseMapId, atlas); m.SetTextureScale(BaseMapId, Vector2.one); m.SetTextureOffset(BaseMapId, Vector2.zero); }
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, Color.white);
            if (m.HasProperty(VcId)) m.SetFloat(VcId, 0f);
            if (m.HasProperty(TeamModeId)) m.SetFloat(TeamModeId, 0f);
            if (m.HasProperty(PfaId)) m.SetFloat(PfaId, 0f);
            if (m.HasProperty(TriId)) m.SetFloat(TriId, 0f);
            if (m.HasProperty(SmoothId)) m.SetFloat(SmoothId, 0.32f);
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, 0f);
            if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, Color.black);
            if (m.HasProperty(EmissionMapId)) m.SetTexture(EmissionMapId, glow);
            if (m.HasProperty(EmissionMapColorId)) m.SetColor(EmissionMapColorId, new Color(GlowGain, GlowGain, GlowGain, 1f));
            if (m.HasProperty(EmissionNightId)) m.SetFloat(EmissionNightId, 0f);
            mats[src] = m;
            return m;
        }

        // ------------------------------------------------------------------ the atlas (made once, no asset)
        public const int N = 256;
        /// <summary>Texel rectangles of the atlas (x, y from the bottom left, width, height).</summary>
        public static readonly RectInt Label = new RectInt(3, 145, 250, 108), Stripes = new RectInt(3, 3, 104, 136),
                                        Gunmetal = new RectInt(112, 80, 56, 56), Red = new RectInt(176, 80, 56, 56),
                                        Yellow = new RectInt(112, 8, 56, 56), Steel = new RectInt(176, 8, 56, 56), Black = new RectInt(240, 8, 12, 128);
        public static readonly RectInt StripesFrame = new RectInt(3, 3, 70, 136), StripesBand = new RectInt(3, 3, 104, 35);   // 1.5 kit self_destruct_v2: the stripes at the frame's and the band's own proportions (45 deg stays 45 deg)
        static readonly Color32 CBack = new Color32(18, 18, 20, 255), CYellow = new Color32(242, 184, 20, 255), CStripeBlack = new Color32(20, 20, 22, 255),
                                CGunmetal = new Color32(58, 62, 66, 255), CRed = new Color32(205, 22, 16, 255), CSteel = new Color32(120, 124, 128, 255),
                                CBlack = new Color32(12, 12, 14, 255), CText = new Color32(255, 238, 205, 255),
                                GText = new Color32(230, 212, 170, 255), GBorder = new Color32(70, 52, 8, 255), GRed = new Color32(170, 14, 8, 255);
        /// <summary>The stripes: 45 degrees, a period of <see cref="StripePeriod"/> texels (5 cm on the frame), 7 of them yellow.</summary>
        public const int StripePeriod = 13, StripeYellow = 7, FontW = 4, FontH = 5, Advance = 24, LineGap = 12, BorderIn = 4, BorderW = 3;
        /// <summary>5 x 7 pixel letters, the top row first.</summary>
        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
        };
        public static readonly string[] Lines = { "SELF", "DESTRUCT" };
        static Texture2D atlas, glow;

        static void MakeTextures()
        {
            if (atlas && glow) return;
            var a = new Color32[N * N]; var g = new Color32[N * N];
            var off = new Color32(0, 0, 0, 255);
            for (int i = 0; i < a.Length; i++) { a[i] = CBack; g[i] = off; }
            void Fill(RectInt r, Color32 c, Color32[] t) { for (int y = r.y; y < r.yMax; y++) for (int x = r.x; x < r.xMax; x++) t[y * N + x] = c; }
            for (int y = Stripes.y; y < Stripes.yMax; y++)
                for (int x = Stripes.x; x < Stripes.xMax; x++) a[y * N + x] = (x + y) % StripePeriod < StripeYellow ? CYellow : CStripeBlack;
            Fill(Gunmetal, CGunmetal, a); Fill(Red, CRed, a); Fill(Red, GRed, g); Fill(Yellow, CYellow, a); Fill(Steel, CSteel, a); Fill(Black, CBlack, a);
            // the label: black, a thin yellow border, SELF over DESTRUCT in warm white
            Fill(Label, CBlack, a);
            var outer = new RectInt(Label.x + BorderIn, Label.y + BorderIn, Label.width - 2 * BorderIn, Label.height - 2 * BorderIn);
            for (int y = outer.y; y < outer.yMax; y++)
                for (int x = outer.x; x < outer.xMax; x++)
                    if (x < outer.x + BorderW || x >= outer.xMax - BorderW || y < outer.y + BorderW || y >= outer.yMax - BorderW) { a[y * N + x] = CYellow; g[y * N + x] = GBorder; }
            int gh = 7 * FontH, block = 2 * gh + LineGap, y0 = Label.y + (Label.height - block) / 2;
            for (int li = 0; li < Lines.Length; li++)
            {
                string s = Lines[li]; int w = s.Length * Advance - (Advance - 5 * FontW);
                int lx = Label.x + (Label.width - w) / 2, ly = y0 + (Lines.Length - 1 - li) * (gh + LineGap);        // the first line on top
                for (int ci = 0; ci < s.Length; ci++)
                {
                    if (!Font.TryGetValue(s[ci], out var rows)) continue;
                    for (int r = 0; r < 7; r++)
                        for (int c = 0; c < 5; c++)
                        {
                            if (rows[r][c] != '#') continue;
                            int px = lx + ci * Advance + c * FontW, py = ly + (6 - r) * FontH;
                            for (int yy = 0; yy < FontH; yy++) for (int xx = 0; xx < FontW; xx++) { int k = (py + yy) * N + px + xx; a[k] = CText; g[k] = GText; }
                        }
                }
            }
            atlas = Tex("OZ_SelfDestructPanel_Atlas", a); glow = Tex("OZ_SelfDestructPanel_Glow", g);
        }

        static Texture2D Tex(string name, Color32[] px)
        {
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(px); t.Apply(true, true);                                                  // mips made, then no CPU copy kept
            return t;
        }

        static Vector2 UV(RectInt r, float fx, float fy) => new Vector2((r.x + 0.5f + fx * (r.width - 1f)) / N, (r.y + 0.5f + fy * (r.height - 1f)) / N);

        // ------------------------------------------------------------------ the mesh (panel space: x = along the wall, the text's right; y = up;
        // -z = out of the wall; z = 0 is the wall itself). One mesh for every panel.
        public const float BtnX = 0f, BtnY = -0.040f, CollarR = 0.105f, CollarZ0 = -0.041f, CollarZ1 = -0.058f, DomeR = 0.082f, DomeH = 0.056f;   // 1.5 kit self_destruct_v2: the bigger button
        public const int Segs = 16, DomeRings = 4;
        static Mesh mesh;
        static readonly List<Vector3> mv = new List<Vector3>(), mn = new List<Vector3>(); static readonly List<Vector2> mu = new List<Vector2>(); static readonly List<int> mt = new List<int>();

        static Mesh SharedMesh()
        {
            if (mesh) return mesh;
            mv.Clear(); mn.Clear(); mu.Clear(); mt.Clear();
            B(-0.220f, -0.415f, -0.029f, 0.220f, 0.415f, -0.004f, StripesFrame, Black);       // the frame: hazard stripes round the face plate, 4 mm off the wall
            B(-0.185f, -0.380f, -0.041f, 0.185f, 0.380f, -0.029f, Gunmetal, Gunmetal);    // the face plate
            B(-0.170f, 0.205f, -0.045f, 0.170f, 0.352f, -0.041f, Label, Black);           // the label: SELF / DESTRUCT
            B(-0.165f, -0.345f, -0.045f, 0.165f, -0.235f, -0.041f, StripesBand, Black);   // the hazard band under the button (1.5 kit self_destruct_v2)
            B(-0.2105f, 0.3875f, -0.035f, -0.1925f, 0.4055f, -0.029f, Steel, Steel);     // a bolt (four, on the frame's corners)
            B(0.1925f, 0.3875f, -0.035f, 0.2105f, 0.4055f, -0.029f, Steel, Steel);
            B(-0.2105f, -0.4055f, -0.035f, -0.1925f, -0.3875f, -0.029f, Steel, Steel);
            B(0.1925f, -0.4055f, -0.035f, 0.2105f, -0.3875f, -0.029f, Steel, Steel);
            Collar(); Dome();
            mesh = new Mesh { name = "OZ_SelfDestructPanel" };
            mesh.SetVertices(mv); mesh.SetNormals(mn); mesh.SetUVs(0, mu);
            var white = new List<Color32>(mv.Count); for (int i = 0; i < mv.Count; i++) white.Add(new Color32(255, 255, 255, 255));
            mesh.SetColors(white);
            mesh.SetTriangles(mt, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>A box; its front (-z) face takes `front` (the whole rectangle), the four sides `side`; no back face (it is on the wall).</summary>
        static void B(float x0, float y0, float z0, float x1, float y1, float z1, RectInt front, RectInt side)
        {
            Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.back,
                 UV(front, 0, 0), UV(front, 1, 0), UV(front, 1, 1), UV(front, 0, 1));
            Vector2 s0 = UV(side, 0.2f, 0.2f), s1 = UV(side, 0.8f, 0.2f), s2 = UV(side, 0.8f, 0.8f), s3 = UV(side, 0.2f, 0.8f);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.left, s0, s1, s2, s3);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, s0, s1, s2, s3);
            Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down, s0, s1, s2, s3);
            Quad(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.up, s0, s1, s2, s3);
        }

        static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = mv.Count;
            mv.Add(a); mv.Add(b); mv.Add(c); mv.Add(d); mn.Add(n); mn.Add(n); mn.Add(n); mn.Add(n); mu.Add(ua); mu.Add(ub); mu.Add(uc); mu.Add(ud);
            Tri(i, i + 1, i + 2, n); Tri(i, i + 2, i + 3, n);
        }

        /// <summary>One triangle, wound so that its face looks along `n` (Unity: clockwise seen from the front; the OzLathe.Boxes rule).</summary>
        static void Tri(int a, int b, int c, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(mv[b] - mv[a], mv[c] - mv[a]), n) >= 0f) { mt.Add(a); mt.Add(b); mt.Add(c); }
            else { mt.Add(a); mt.Add(c); mt.Add(b); }
        }

        /// <summary>The yellow collar round the button: a short 16-sided ring and its face.</summary>
        static void Collar()
        {
            var u = UV(Yellow, 0.5f, 0.5f);
            for (int k = 0; k < Segs; k++)
            {
                float a0 = k * Mathf.PI * 2f / Segs, a1 = (k + 1) * Mathf.PI * 2f / Segs, am = (a0 + a1) * 0.5f;
                Vector3 p0 = new Vector3(BtnX + CollarR * Mathf.Cos(a0), BtnY + CollarR * Mathf.Sin(a0), 0f), p1 = new Vector3(BtnX + CollarR * Mathf.Cos(a1), BtnY + CollarR * Mathf.Sin(a1), 0f);
                Quad(p0 + Vector3.forward * CollarZ0, p1 + Vector3.forward * CollarZ0, p1 + Vector3.forward * CollarZ1, p0 + Vector3.forward * CollarZ1,
                     new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f), u, u, u, u);
                int i = mv.Count;
                mv.Add(new Vector3(BtnX, BtnY, CollarZ1)); mv.Add(p0 + Vector3.forward * CollarZ1); mv.Add(p1 + Vector3.forward * CollarZ1);
                for (int j = 0; j < 3; j++) { mn.Add(Vector3.back); mu.Add(u); }
                Tri(i, i + 1, i + 2, Vector3.back);
            }
        }

        /// <summary>The red domed button on the collar: a half ellipsoid (radius <see cref="DomeR"/>, height <see cref="DomeH"/>), smooth normals.</summary>
        static void Dome()
        {
            var u = UV(Red, 0.5f, 0.5f);
            int first = mv.Count;
            for (int j = 0; j < DomeRings; j++)
            {
                float ph = j * Mathf.PI * 0.5f / DomeRings, cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
                for (int k = 0; k < Segs; k++)
                {
                    float al = k * Mathf.PI * 2f / Segs, ca = Mathf.Cos(al), sa = Mathf.Sin(al);
                    mv.Add(new Vector3(BtnX + DomeR * cp * ca, BtnY + DomeR * cp * sa, CollarZ1 - DomeH * sp));
                    mn.Add(new Vector3(cp * ca / DomeR, cp * sa / DomeR, -sp / DomeH).normalized); mu.Add(u);
                }
            }
            int pole = mv.Count; mv.Add(new Vector3(BtnX, BtnY, CollarZ1 - DomeH)); mn.Add(Vector3.back); mu.Add(u);
            int V(int j, int k) => first + j * Segs + (k % Segs);
            for (int j = 0; j < DomeRings; j++)
                for (int k = 0; k < Segs; k++)
                {
                    if (j < DomeRings - 1)
                    {
                        Tri(V(j, k), V(j, k + 1), V(j + 1, k + 1), (mn[V(j, k)] + mn[V(j + 1, k + 1)]).normalized);
                        Tri(V(j, k), V(j + 1, k + 1), V(j + 1, k), (mn[V(j, k)] + mn[V(j + 1, k + 1)]).normalized);
                    }
                    else Tri(V(j, k), V(j, k + 1), pole, (mn[V(j, k)] + mn[pole]).normalized);
                }
        }
    }
}
