#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// k83, small look fixes on the faction screen (UI/OzFactionLook.cs): the counted check in edit mode (no Play, nothing saved; menu
    /// OpenZ/V1.2/Look Fixes: 83 Counted Check) and the Play proof menus (View/OzLookFixesProof.cs).
    ///  A  the DOSSIER rows: the cause reproduced on hidden probe texts made as the card makes them (Courier Prime, the card's sizes): a text
    ///     made on a card that is still off measures at a tenth of its size, so EvilZod's first row "fitted" its box and TextMeshPro broke
    ///     it at the last word that fitted; marked as a UI text from the start it measures as awake; awake, the rows wider than the box are
    ///     exactly the mock-up's two-line rows, laid out label / value; their heights; the hooks in the card's code.
    ///  B  the VS halo: the poster shader's halo mode (no errors, its branch), the mix over the measured backdrop with the halo picture's
    ///     own colours at the pulse's low and high (the preview's sRGB mix against k83's and against the linear blend before), over a
    ///     lighter and a darker backdrop, the material's settings, the hooks.
    /// </summary>
    public static class OzV12LookFixesCheck
    {
        public const string CheckMenu = "OpenZ/V1.2/Look Fixes: 83 Counted Check";
        public const string ProofMenu = "OpenZ/V1.2/Look Fixes: 83 Play Proof (Play)";
        public const string StopMenu = "OpenZ/V1.2/Look Fixes: 83 Play Proof: stop";
        const string T = "[OzV12LookFixesCheck] ";
        const string DeckCs = "Assets/OpenZ/Scripts/UI/OpenZMainMenu.FactionDeck.cs";
        const string ShaderFile = "Assets/OpenZ/Shaders/OzPosterFigure.shader";
        const string HaloPng = "Assets/OpenZ/Resources/OpenZ/Faction/Vs/vs_grunge_halo.png";

        [MenuItem(ProofMenu)]
        static void Proof() => OzLookFixesProof.Run();
        [MenuItem(ProofMenu, true)] static bool ProofReady() => Application.isPlaying;
        [MenuItem(StopMenu)]
        static void Stop() => OzLookFixesProof.Stop();

        static readonly StringBuilder sb = new StringBuilder();
        static int pass, fail;
        static readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();
        static void Line(string s) { sb.AppendLine(s); }
        static void V(bool ok, string what) { if (ok) pass++; else fail++; Line((ok ? "PASS " : "FAIL ") + what); }

        [MenuItem(CheckMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning(T + "run it in edit mode (no Play)"); return; }
            Debug.Log(Check());
        }

        public static string Check()
        {
            sb.Clear(); pass = fail = 0;
            Line("k83 small look fixes: the counted check (edit mode, nothing saved)");
            try { Rows(); } catch (Exception e) { V(false, "A the rows threw " + e.GetType().Name + ": " + e.Message); }
            finally { CleanUp(); }
            try { Halo(); } catch (Exception e) { V(false, "B the halo threw " + e.GetType().Name + ": " + e.Message); }
            finally { CleanUp(); }
            string head = T + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail);
            sb.Insert(0, head + "\n");
            try
            {
                string dir = OzLookFixesProof.Folder();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "counted_check.txt"), sb.ToString());
                head += " (" + Path.Combine(dir, "counted_check.txt").Replace('\\', '/') + ")";
            }
            catch (Exception e) { head += " (the lines could not be written: " + e.Message + ")"; }
            Debug.Log(sb.ToString());
            return head;
        }

        static void CleanUp()
        {
            for (int i = made.Count - 1; i >= 0; i--)                                                     // the probe texts before their fonts
            {
                var o = made[i];
                if (!o) continue;
                if (o is TMP_FontAsset fa)
                {
                    if (fa.material) UnityEngine.Object.DestroyImmediate(fa.material);
                    if (fa.atlasTextures != null) foreach (var t in fa.atlasTextures) if (t) UnityEngine.Object.DestroyImmediate(t);
                }
                UnityEngine.Object.DestroyImmediate(o);
            }
            made.Clear();
        }

        static GameObject Hidden(string name, Transform parent, params Type[] parts)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, parts);
            if (parent) go.transform.SetParent(parent, false);
            return go;
        }

        // ================================================================================================================ A: the rows
        /// <summary>The card's text fonts as View/OzFactionDeckAssets.cs makes them (dynamic SDF, the bold one as the regular one's bold weight).</summary>
        static TMP_FontAsset MakeFont(Font f, string name)
        {
            var fa = TMP_FontAsset.CreateFontAsset(f, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            if (!fa) return null;
            fa.name = name; fa.hideFlags = HideFlags.HideAndDontSave; made.Add(fa);
            return fa;
        }

        /// <summary>A row's text as the card's FcLabel makes it (TextMeshPro, the card's settings), under a parent that is on or off.</summary>
        static TextMeshProUGUI RowText(Transform parent, TMP_FontAsset fa, string text, float boxW)
        {
            var go = Hidden("Words", parent, typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(boxW, 10f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = fa; t.fontSharedMaterial = fa.material; t.fontSize = OzFactionDeck.RowTextSize; t.color = Color.black; t.richText = true; t.raycastTarget = false;
            t.characterSpacing = 0f; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.alignment = TextAlignmentOptions.Left;
            t.text = text;
            return t;
        }

        static float Width(TMP_Text t, string s) => t.GetPreferredValues(s, 99999f, 0f).x;

        static List<string> Lines(TMP_Text t)
        {
            var ti = t.textInfo; var res = new List<string>();
            for (int l = 0; l < ti.lineCount; l++)
            {
                var li = ti.lineInfo[l]; var s = new StringBuilder();
                for (int c = li.firstCharacterIndex; c <= li.lastCharacterIndex && c < ti.characterCount; c++) s.Append(ti.characterInfo[c].character);
                res.Add(s.ToString().Trim());
            }
            return res;
        }

        static string Who(int s, int r) => (s == OzFaction.Red ? "Zod " : "EvilZod ") + (r + 1);

        static void Rows()
        {
            var body = Resources.Load<Font>(OzFactionDeck.FontBody); var bold = Resources.Load<Font>(OzFactionDeck.FontBold);
            if (!body || !bold) { V(false, "A the card's typewriter fonts load (Resources/" + OzFactionDeck.FontBody + ", " + OzFactionDeck.FontBold + ")"); return; }
            var fa = MakeFont(body, "k83 probe (typewriter)"); var fb = MakeFont(bold, "k83 probe (typewriter bold)");
            if (!fa || !fb) { V(false, "A the card's text fonts can be made from them"); return; }
            if (fa.fontWeightTable != null && fa.fontWeightTable.Length > 7) fa.fontWeightTable[7].regularTypeface = fb;
            float W = OzFactionDeck.CardW, box = (OzFactionDeck.RowTextRight - OzFactionDeck.RowTextX) * W;
            var root = Hidden("k83 rows probe", null, typeof(RectTransform), typeof(Canvas)); made.Add(root);
            var awakeCard = Hidden("card on", root.transform, typeof(RectTransform));
            var sleepCard = Hidden("card off", root.transform, typeof(RectTransform)); sleepCard.SetActive(false);

            // A1 the cause, reproduced on EvilZod's first row
            string evil1 = OzFactionDeck.RowText(OzFaction.Blue, 0, false);
            var asleep = RowText(sleepCard.transform, fa, evil1, box);
            bool flagAsleep = asleep.isOrthographic; float wAsleep = Width(asleep, evil1);
            var awake = RowText(awakeCard.transform, fa, evil1, box);
            float wAwake = Width(awake, evil1); float ratio = wAsleep / Mathf.Max(0.01f, wAwake);
            awake.ForceMeshUpdate(true, true);
            var broke = Lines(awake);
            V(ratio > 0.08f && ratio < 0.12f && !flagAsleep && wAsleep <= box && wAwake > box,
              $"A1 the cause, reproduced: EvilZod's first row \"{OzFactionDeck.Line(OzFaction.Blue, 0)}\" made on a card that is still off measures {wAsleep:0.0} u (its UI flag {flagAsleep}: set only when it wakes), "
              + $"awake {wAwake:0.0} u ({ratio:0.000} x): off, it 'fits' the {box:0.00} u box, so the card kept it on one line, and laid out in that box TextMeshPro breaks it as \"{string.Join(" / ", broke)}\" (the game's picture)");

            // A2 the belt: a card text marked as a UI text from the start measures as awake while the card is still off
            var marked = RowText(sleepCard.transform, fa, evil1, box);
            marked.isOrthographic = true;
            float wMarked = Width(marked, evil1);
            V(Mathf.Abs(wMarked - wAwake) < 0.5f, $"A2 a card text marked as a UI text from its start (k83 FcUiText) measures {wMarked:0.0} u while its card is still off: as awake ({wAwake:0.0} u)");

            // A3 awake: which rows are wider than the box
            var two = new List<string>(); float spare = 999f; string tight = "";
            var wide = new bool[2, OzFactionDeck.Rows];
            for (int s = 0; s < 2; s++)
                for (int r = 0; r < OzFactionDeck.Rows; r++)
                {
                    string one = OzFactionDeck.RowText(s, r, false);
                    var t = RowText(awakeCard.transform, fa, one, box);
                    float w = Width(t, one);
                    wide[s, r] = w > box;
                    if (w > box) two.Add(Who(s, r));
                    else if (box - w < spare) { spare = box - w; tight = OzFactionDeck.Line(s, r); }
                }
            bool same = two.SequenceEqual(new[] { "Zod 2", "Zod 5", "EvilZod 1" });
            V(same && spare > 0.5f, $"A3 awake, in Courier Prime at {OzFactionDeck.RowTextSize} u: the rows wider than the {box:0.00} u box (two lines, broken after the label) {string.Join(", ", two)}; "
              + $"the mock-up's two-line rows: Zod 2, Zod 5, EvilZod 1 {same}; the tightest one-line row \"{tight}\" with {spare:0.0} u to spare");

            // A4 laid out as the card now makes them: label / value on two lines, the others on one; their heights (FcBuildContent's rule)
            var bad = new List<string>(); var heights = new StringBuilder(); bool taller = true, clear = true;
            for (int s = 0; s < 2; s++)
            {
                float y = OzFactionDeck.RowsY * OzFactionDeck.CardH(s), oneMax = 0f, twoMin = 999f;
                heights.Append(s == 0 ? "Zod" : "; EvilZod");
                for (int r = 0; r < OzFactionDeck.Rows; r++)
                {
                    string words = OzFactionDeck.RowText(s, r, wide[s, r]);
                    var t = RowText(awakeCard.transform, fa, words, box);
                    ((RectTransform)t.transform).sizeDelta = new Vector2(box, 80f);
                    t.ForceMeshUpdate(true, true);
                    var got = Lines(t);
                    string lab = OzFactionDeck.Label[s][r], val = OzFactionDeck.Value[s][r];
                    var want = wide[s, r] ? new List<string> { lab, val } : new List<string> { lab + " " + val };
                    if (!got.SequenceEqual(want)) bad.Add(Who(s, r) + " \"" + string.Join(" / ", got) + "\"");
                    float th = t.GetPreferredValues(words, box, 0f).y;
                    float rh = Mathf.Max(OzFactionDeck.RowMinH, Mathf.Max(OzFactionDeck.RowIconSize, th) + 2f * OzFactionDeck.RowPad);
                    if (wide[s, r]) twoMin = Mathf.Min(twoMin, rh); else oneMax = Mathf.Max(oneMax, rh);
                    heights.Append(" ").Append(rh.ToString("0.0"));
                    y += rh;
                }
                taller &= twoMin > oneMax;
                clear &= y < OzFactionDeck.CardH(s) - 60f;
                heights.Append($" (ends {y:0.0} of {OzFactionDeck.CardH(s):0.0} u)");
            }
            V(bad.Count == 0 && taller && clear, $"A4 laid out as the card now makes them: every two-line row reads label / value and every other row one line ({(bad.Count == 0 ? "all 10" : "not: " + string.Join("; ", bad))}); "
              + $"the rows' heights (u) {heights}: the two-line rows taller {taller}, the stamp clear {clear} (before: every row {OzFactionDeck.RowMinH:0} u, measured asleep)");

            // A5 the hooks in the card's code
            string src = File.Exists(DeckCs) ? File.ReadAllText(DeckCs) : "";
            int build = src.IndexOf("void FcBuildContent(FcDossier d)", StringComparison.Ordinal);
            int wake = build >= 0 ? src.IndexOf("FcWake(d.root);", build, StringComparison.Ordinal) : -1;
            int firstLabel = build >= 0 ? src.IndexOf("FcLabel(", build, StringComparison.Ordinal) : -1;
            int add = src.IndexOf("AddComponent<TextMeshProUGUI>();", StringComparison.Ordinal);
            int ui = src.IndexOf("FcUiText(t);", StringComparison.Ordinal);
            bool hooks = wake > 0 && firstLabel > wake && add > 0 && ui > add && ui - add < 120;
            V(hooks, $"A5 the hooks: FcBuildContent switches the card on (FcWake) before its first label {wake > 0 && firstLabel > wake}; FcLabel marks its TextMeshPro text a UI text right after making it {ui > add && add > 0}");
        }

        // ================================================================================================================ B: the halo
        static void Halo()
        {
            // B1 the shader
            var sh = Shader.Find("OpenZ/UI/PosterFigure");
            string src = File.Exists(ShaderFile) ? File.ReadAllText(ShaderFile) : "";
            int mode = src.IndexOf("if (_Print > 2.5) return HaloMix(c, i.color);", StringComparison.Ordinal), vs = src.IndexOf("if (_Print > 1.5)", StringComparison.Ordinal);
            bool fn = src.Contains("float4 HaloMix(float4 c, float4 tint)");
            bool clean = sh && !ShaderUtil.ShaderHasError(sh);
            int msgs = sh ? ShaderUtil.GetShaderMessageCount(sh) : -1;
            V(clean && msgs == 0 && fn && mode > 0 && vs > mode, $"B1 the poster shader OpenZ/UI/PosterFigure: no errors {clean}, {msgs} message(s); its halo mode (_Print 3: HaloMix) {fn}, checked before the VS sign's mode {mode > 0 && vs > mode}");

            // B2 the mix over the measured backdrop with the halo picture's own colours (the PNG read as a file: the texture is not readable)
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave }; made.Add(tex);
            if (!File.Exists(HaloPng) || !tex.LoadImage(File.ReadAllBytes(HaloPng))) { V(false, "B2 the halo picture " + HaloPng + " reads"); return; }
            var px = tex.GetPixels32();
            Color b0 = (Color)(Color32)OzFaction.PaperBack;
            string Mix(Color b, out double k83Mean, out double k83Max, out double oldMean, out double liftK, out double liftOld)
            {
                var parts = new List<string>(); k83Mean = k83Max = oldMean = 0; liftK = liftOld = 0;
                foreach (var hk in new[] { (OzFactionDeck.HaloLo, "low"), (OzFactionDeck.HaloHi, "high") })
                {
                    double dk = 0, dkMax = 0, dold = 0, lg = 0, lk = 0, lo = 0; int n = 0;
                    foreach (var p in px)
                    {
                        if (p.a < 3) continue;
                        Color c = new Color(p.r / 255f, p.g / 255f, p.b / 255f, 1f); float a = p.a / 255f * hk.Item1;
                        Color g = OzFactionLook.GammaOver(c, a, b), k = OzFactionLook.HaloOver(c, a, b, b0), o = OzFactionLook.LinearOver(c, a, b);
                        double e = (Mathf.Abs(k.r - g.r) + Mathf.Abs(k.g - g.g) + Mathf.Abs(k.b - g.b)) / 3.0 * 255.0;
                        dk += e; dkMax = Math.Max(dkMax, Mathf.Max(Mathf.Abs(k.r - g.r), Mathf.Abs(k.g - g.g), Mathf.Abs(k.b - g.b)) * 255.0);
                        dold += (Mathf.Abs(o.r - g.r) + Mathf.Abs(o.g - g.g) + Mathf.Abs(o.b - g.b)) / 3.0 * 255.0;
                        lg += (Mathf.Abs(g.r - b.r) + Mathf.Abs(g.g - b.g) + Mathf.Abs(g.b - b.b)); lk += (Mathf.Abs(k.r - b.r) + Mathf.Abs(k.g - b.g) + Mathf.Abs(k.b - b.b));
                        lo += (Mathf.Abs(o.r - b.r) + Mathf.Abs(o.g - b.g) + Mathf.Abs(o.b - b.b)); n++;
                    }
                    n = Math.Max(1, n);
                    k83Mean = Math.Max(k83Mean, dk / n); k83Max = Math.Max(k83Max, dkMax); oldMean = Math.Max(oldMean, dold / n);
                    liftK = Math.Max(liftK, lk / Math.Max(1e-6, lg)); liftOld = Math.Max(liftOld, lo / Math.Max(1e-6, lg));
                    parts.Add($"at the pulse's {hk.Item2} (alpha {hk.Item1:0.00} x the picture, {n} pixels): k83 {dk / n:0.00} steps from the preview's mix (worst {dkMax:0.0}), lifting {lk / Math.Max(1e-6, lg):0.00} x as much; before {dold / n:0.0} steps, {lo / Math.Max(1e-6, lg):0.00} x");
                }
                return string.Join("; ", parts);
            }
            string at0 = Mix(b0, out double m0, out double x0, out double o0, out double l0, out double lo0);
            V(m0 < 0.5 && x0 < 3.0 && o0 > 5.0 && lo0 > 1.8 && l0 > 0.95 && l0 < 1.05,
              $"B2 over the measured backdrop {OzFaction.PaperBack.r},{OzFaction.PaperBack.g},{OzFaction.PaperBack.b} (the screen's colour behind the VS), with the halo picture's own colours: {at0}");

            // B3 the live backdrop moves (the blurred flyby behind): a darker and a lighter one, the material still told b0
            var lines = new List<string>(); bool ok3 = true;
            foreach (float f in new[] { 0.6f, 1.6f })
            {
                Color b = new Color(b0.r * f, b0.g * f, b0.b * f, 1f);
                Mix(b, out double m, out double x, out double o, out double l, out double lo);
                ok3 &= m < 6.0 && m < o * 0.35;
                lines.Add($"x {f:0.0}: k83 {m:0.0} steps from the preview's mix (lift {l:0.00} x), before {o:0.0} steps (lift {lo:0.00} x)");
            }
            V(ok3, "B3 over a darker and a lighter backdrop (the flyby behind moves; the material still told the measured one): " + string.Join("; ", lines));

            // B4 the material's settings and the hooks
            var mat = sh ? new Material(sh) { hideFlags = HideFlags.HideAndDontSave } : null; if (mat) made.Add(mat);
            OzFactionLook.SetHalo(mat);
            bool set = mat && Mathf.Abs(mat.GetFloat("_Print") - 3f) < 0.01f && mat.GetFloat("_RimPx") == 0f && mat.GetFloat("_FxMode") == 0f && mat.GetVector("_Sweep").x < -0.5f;
            string deck = File.Exists(DeckCs) ? File.ReadAllText(DeckCs) : "";
            int col = deck.IndexOf("fcVsHalo.color = new Color(1f, 1f, 1f, k * OzFactionDeck.HaloAlpha(now, calm));", StringComparison.Ordinal);
            int look = deck.IndexOf("FcHaloLook();", StringComparison.Ordinal);
            int close = deck.IndexOf("void CloseFactionDeck(bool sweep)", StringComparison.Ordinal), closeHook = deck.IndexOf("FcHaloLookClose();", StringComparison.Ordinal);
            bool hooks = col > 0 && look > col && look - col < 200 && close > 0 && closeHook > close;
            V(set && hooks, $"B4 the halo's material: _Print 3, no rim, no effect, no light band {set}; TickFactionVs draws the halo with it right after its colour, CloseFactionDeck lets it go {hooks}");
        }
    }
}
#endif
