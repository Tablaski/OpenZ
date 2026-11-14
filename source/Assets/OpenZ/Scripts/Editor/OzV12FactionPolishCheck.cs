#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// k75a4, the faction screen's polish (1.2 owner items 102, 104, 108, 112, 113, 115): the counted check in edit mode (no Play, nothing
    /// saved; menu OpenZ/V1.2/Faction Screen: 75a4 Counted Check) and the Play proof menus (View/OzFactionPolishProof.cs).
    ///  A  the assets: Courier Prime (regular, bold: whole font data, named, its OFL text with its copyright line, a monospace advance,
    ///     the credits and the licences), the owner's three dossier sounds (form, lengths, imported as the UI's clips, in the clip levels,
    ///     their three events on the UI bus, the card's sound names), the shader's new properties (no errors).
    ///  B  the rules: the rows in Courier Prime (which wrap, the spare), the supersampled render sizes, EvilZod's figure (scale, the band
    ///     kept), the cape's fill strip, the glint and glow loops (smooth, never popping, CALM MODE), the spots (the ellipse map; projected
    ///     through the measured cameras they land on the lenses / eyes measured on the light models' renders), the row ticks (one per
    ///     row, never closer than 45 ms, at 30 / 60 / 144 frames a second), the card shade (light, narrow).
    ///  C  the wiring: the hooks once each (and k75a3's still once each), the render textures' supersampling, the shader's calls.
    /// Lines in the kit's proof/counted_check.txt; one line "[OzV12FactionPolishCheck] PASS n/n" (or FAIL).
    /// </summary>
    public static class OzV12FactionPolishCheck
    {
        public const string CheckMenu = "OpenZ/V1.2/Faction Screen: 75a4 Counted Check";
        public const string ProofMenu = "OpenZ/V1.2/Faction Screen: 75a4 Play Proof (Play)";
        public const string StopMenu = "OpenZ/V1.2/Faction Screen: 75a4 Play Proof: stop";
        const string T = "[OzV12FactionPolishCheck] ";
        const string Root = "Assets/OpenZ/Resources/";

        [MenuItem(ProofMenu)]
        static void Proof()
        {
            var gv = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            OzFactionPolishProof.Run();
        }
        [MenuItem(ProofMenu, true)] static bool ProofReady() => Application.isPlaying;
        [MenuItem(StopMenu)]
        static void Stop() => OzFactionPolishProof.Stop();

        static readonly StringBuilder sb = new StringBuilder();
        static int pass, fail;
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
            Line("k75a4 the faction screen's polish: the counted check (edit mode)");
            try { Assets(); } catch (Exception e) { V(false, "A the assets threw " + e.GetType().Name + ": " + e.Message); }
            try { Rules(); } catch (Exception e) { V(false, "B the rules threw " + e.GetType().Name + ": " + e.Message); }
            try { Wiring(); } catch (Exception e) { V(false, "C the wiring threw " + e.GetType().Name + ": " + e.Message); }
            string head = T + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail);
            sb.Insert(0, head + "\n");
            try
            {
                string dir = OzFactionPolishProof.Folder();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "counted_check.txt"), sb.ToString());
                head += " (" + Path.Combine(dir, "counted_check.txt").Replace('\\', '/') + ")";
            }
            catch (Exception e) { head += " (the lines could not be written: " + e.Message + ")"; }
            Debug.Log(sb.ToString());
            return head;
        }

        // ================================================================================================================ A: the assets
        /// <summary>A TrueType file's numbers: units per em, the line height (ascender - descender + line gap, em), the most common advance
        /// (em) and its share of the glyphs.</summary>
        static bool FontNumbers(string path, out float lineEm, out float advEm, out float share)
        {
            lineEm = advEm = share = 0f;
            if (!File.Exists(path)) return false;
            var b = File.ReadAllBytes(path);
            int U16(int o) => (b[o] << 8) | b[o + 1];
            int S16(int o) { int v = U16(o); return v >= 32768 ? v - 65536 : v; }
            long U32(int o) => ((long)b[o] << 24) | ((long)b[o + 1] << 16) | ((long)b[o + 2] << 8) | b[o + 3];
            int n = U16(4); var tab = new Dictionary<string, int>();
            for (int i = 0; i < n; i++) { int r = 12 + 16 * i; tab[Encoding.ASCII.GetString(b, r, 4)] = (int)U32(r + 8); }
            if (!tab.ContainsKey("head") || !tab.ContainsKey("hhea") || !tab.ContainsKey("hmtx")) return false;
            float upem = U16(tab["head"] + 18);
            int hh = tab["hhea"];
            lineEm = (S16(hh + 4) - S16(hh + 6) + S16(hh + 8)) / upem;
            int nh = U16(hh + 34), hm = tab["hmtx"];
            var count = new Dictionary<int, int>();
            for (int i = 0; i < nh; i++) { int a = U16(hm + 4 * i); count[a] = count.TryGetValue(a, out int c) ? c + 1 : 1; }
            var top = count.OrderByDescending(kv => kv.Value).First();
            advEm = top.Key / upem; share = top.Value / (float)Mathf.Max(1, nh);
            return true;
        }

        static float advance, lineH;

        static void Assets()
        {
            // Courier Prime
            foreach (var res in new[] { OzFactionDeck.FontBody, OzFactionDeck.FontBold })
            {
                string p = Root + res + ".ttf";
                var font = AssetDatabase.LoadAssetAtPath<Font>(p); var fi = AssetImporter.GetAtPath(p) as TrueTypeFontImporter;
                bool num = FontNumbers(p, out float le, out float adv, out float share);
                if (res == OzFactionDeck.FontBody) { advance = adv; lineH = le; }
                V(font && Resources.Load<Font>(res) == font && fi != null && fi.includeFontData && fi.fontNames != null && fi.fontNames.Contains("Courier Prime") && num && Mathf.Abs(adv - 0.5996f) < 0.0005f && share > 0.9f,
                  $"A the font {Path.GetFileName(p)}: {(font ? "loads" : "MISSING")}, whole font data {(fi != null && fi.includeFontData)}, named Courier Prime {(fi != null && fi.fontNames != null && fi.fontNames.Contains("Courier Prime"))}; a typewriter's advance {adv:0.0000} em ({share * 100f:0} % of its glyphs), line height {le:0.000} em");
            }
            var lic = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + OzFactionDeck.Folder + "Fonts/OFL_CourierPrime.txt");
            V(lic && lic.text.Contains("SIL Open Font License, Version 1.1") && lic.text.Contains("Copyright 2015 The Courier Prime Project Authors"),
              "A its licence OFL_CourierPrime.txt beside it: the SIL Open Font License 1.1 with its copyright line " + (lic != null));
            string credits = File.Exists(Root + "OpenZ/Menu/credits.txt") ? File.ReadAllText(Root + "OpenZ/Menu/credits.txt") : "";
            string licences = File.Exists("Assets/StreamingAssets/LICENSES.txt") ? File.ReadAllText("Assets/StreamingAssets/LICENSES.txt").Replace("\r\n", "\n") : "";
            V(credits.Contains("Courier Prime by The Courier Prime Project Authors") && licences.Contains("Courier Prime font (Copyright 2015 The Courier Prime Project Authors"),
              $"A the credits name Courier Prime {credits.Contains("Courier Prime")}, the licences {licences.Contains("Courier Prime font")}");
            // the owner's dossier sounds
            string table = File.Exists(Root + "OpenZ/Audio/openz_sound_events.json") ? File.ReadAllText(Root + "OpenZ/Audio/openz_sound_events.json") : "";
            string levels = File.Exists(Root + "OpenZ/Audio/openz_clip_levels.json") ? File.ReadAllText(Root + "OpenZ/Audio/openz_clip_levels.json") : "";
            var refImp = AssetImporter.GetAtPath(Root + "OpenZ/Audio/Sfx/SoundV3/s3_ui_band_whoosh.wav") as AudioImporter;
            var clips = new[] { ("s3_ui_dossier_in_a", 1.237f, OzFactionDeck.SoundIn), ("s3_ui_dossier_out_a", 0.612f, OzFactionDeck.SoundOut), ("s3_ui_dossier_row_a", 0.153f, OzFactionPolish.SoundRow) };
            foreach (var c in clips)
            {
                string p = Root + "OpenZ/Audio/Sfx/SoundV3/" + c.Item1 + ".wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p); var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                bool same = ai != null && refImp != null && ai.defaultSampleSettings.loadType == refImp.defaultSampleSettings.loadType && ai.forceToMono == refImp.forceToMono;
                bool ev = EventHas(table, c.Item3, "OpenZ/Audio/Sfx/SoundV3/" + c.Item1);
                V(clip && clip.channels == 1 && clip.frequency == 44100 && Mathf.Abs(clip.length - c.Item2) < 0.01f && same && levels.Contains("\"n\": \"" + c.Item1 + "\"") && ev,
                  $"A the sound {c.Item1}: {(clip ? clip.length.ToString("0.000") + " s, " + clip.channels + " ch, " + clip.frequency + " Hz" : "MISSING")} (wanted {c.Item2:0.000} s, mono 44.1 kHz), imported as the UI's clips {same}, in the clip levels {levels.Contains("\"n\": \"" + c.Item1 + "\"")}, its event {c.Item3} on the UI bus {ev}");
            }
            V(OzFactionDeck.SoundIn == "ui_dossier_in" && OzFactionDeck.SoundOut == "ui_dossier_out" && OzFactionPolish.SoundRow == "ui_dossier_row" && table.Contains("\"id\": \"ui_paper_slide\"") && table.Contains("\"id\": \"ui_paper_back\""),
              $"A the card plays {OzFactionDeck.SoundIn} / {OzFactionDeck.SoundOut} and a {OzFactionPolish.SoundRow} per row; k75a3's paper sounds stay in the table (not played)");
            // the shader
            var mat = Resources.Load<Material>(OzFactionPosters.Folder + OzFactionPosters.FigureMaterial);
            var sh = mat ? mat.shader : null;
            var props = new[] { "_FxMode", "_SpotC", "_SpotM1", "_SpotM2", "_Glint", "_GlintSpan", "_EyeGlow", "_EyeTone", "_Fill" };
            var missing = props.Where(pn => !mat || !mat.HasProperty(pn)).ToArray();
            V(sh && !ShaderUtil.ShaderHasError(sh) && missing.Length == 0,
              $"A OzPosterFigure (the renders' glint, glow and fill): errors {(sh ? ShaderUtil.ShaderHasError(sh).ToString() : "MISSING")}, its new properties {(missing.Length == 0 ? "all " + props.Length : "missing " + string.Join(", ", missing))}");
        }

        /// <summary>The sound table has the event id on the UI bus with the clip.</summary>
        static bool EventHas(string table, string id, string res)
        {
            int i = table.IndexOf("\"id\": \"" + id + "\"", StringComparison.Ordinal);
            if (i < 0) return false;
            int j = table.IndexOf("\"id\": \"", i + 8, StringComparison.Ordinal); if (j < 0) j = table.Length;
            string ev = table.Substring(i, j - i);
            return ev.Contains("\"bus\": \"UI\"") && ev.Contains("\"res\": \"" + res + "\"");
        }

        // ================================================================================================================ B: the rules
        static void Rules()
        {
            // the rows in Courier Prime: the same rows on two lines as before (Zod's 2nd and 5th, EvilZod's 1st), the others with room to spare
            float W = OzFactionDeck.CardW, box = (OzFactionDeck.RowTextRight - OzFactionDeck.RowTextX) * W, adv = advance > 0f ? advance : 0.5996f;
            var two = new List<string>(); float spare = 999f; string tight = "";
            for (int s = 0; s < 2; s++)
                for (int r = 0; r < OzFactionDeck.Rows; r++)
                {
                    float w = OzFactionDeck.Line(s, r).Length * adv * OzFactionDeck.RowTextSize;
                    if (w > box) two.Add((s == 0 ? "Zod " : "EvilZod ") + (r + 1));
                    else if (box - w < spare) { spare = box - w; tight = OzFactionDeck.Line(s, r); }
                }
            bool same = two.SequenceEqual(new[] { "Zod 2", "Zod 5", "EvilZod 1" });
            V(same && spare >= 2f, $"A/B the rows at {OzFactionDeck.RowTextSize} u in Courier Prime ({adv:0.0000} em a letter) in a {box:0.0} u box: on two lines {string.Join(", ", two)} (as before: Zod 2, Zod 5, EvilZod 1) {same}; the tightest one-line row \"{tight}\" with {spare:0.0} u to spare (2 wanted)");
            for (int s = 0; s < 2; s++)
            {
                float y = OzFactionDeck.RowsY * OzFactionDeck.CardH(s);
                for (int r = 0; r < OzFactionDeck.Rows; r++)
                {
                    bool tw = OzFactionDeck.Line(s, r).Length * adv * OzFactionDeck.RowTextSize > box;
                    float th = (tw ? 2f : 1f) * (lineH > 0f ? lineH : 1.123f) * OzFactionDeck.RowTextSize;
                    y += Mathf.Max(OzFactionDeck.RowMinH, Mathf.Max(OzFactionDeck.RowIconSize, th) + 2f * OzFactionDeck.RowPad);
                }
                V(y < OzFactionDeck.CardH(s) - 60f, $"B the {(s == 0 ? "Zod" : "EvilZod")} card's rows end {y:0.0} u under its paper's top (the paper {OzFactionDeck.CardH(s):0.0} u: the stamp's corner stays clear)");
            }
            // the supersampled renders
            var r1 = OzFactionPosters.RtSize(452, 803); var r4 = OzFactionPosters.RtSize(903, 1606); var r0 = OzFactionPosters.RtSize(64, 64);
            V(r1 == new Vector2Int(904, 1606) && r4.y <= OzFactionPosters.MaxRt && r4.y >= 1606 && r0 == new Vector2Int(128, 128) && Mathf.Abs(OzFactionPosters.Supersample - 2f) < 1e-4f,
              $"B the renders supersampled: 1080p poster 452 x 803 -> {r1.x} x {r1.y}; 4K 903 x 1606 -> {r4.x} x {r4.y} (at most {OzFactionPosters.MaxRt}, never under the screen's); 64 -> {r0.x}");
            // EvilZod a little bigger, the band kept
            var uv = OzFactionPolish.FigureUv(OzFaction.Blue); var a = OzFactionPolish.EvilAnchor;
            Vector2 back = new Vector2(uv.x + a.x * uv.width, uv.y + a.y * uv.height);
            bool inside = uv.xMin >= 0f && uv.yMin >= 0f && uv.xMax <= 1f && uv.yMax <= 1f;
            V(OzFactionPolish.EvilScale >= 1.03f && OzFactionPolish.EvilScale <= 1.06f && (back - a).magnitude < 1e-5f && inside && Mathf.Abs(a.y - OzFactionPolish.BandV) < 1e-6f && OzFactionPolish.FigureUv(OzFaction.Red) == new Rect(0f, 0f, 1f, 1f),
              $"B EvilZod's render {OzFactionPolish.EvilScale:0.00} x (the owner: a tiny bit, 3 .. 6 %) about the band's top edge three quarters across ({a.x:0.000}, {a.y:0.0000}): that point stays ({back.x:0.0000}, {back.y:0.0000}), uv {uv.x:0.0000} {uv.y:0.0000} {uv.width:0.0000} (inside the render {inside}); Zod's render whole");
            var f = OzFactionPolish.Fill(uv);
            float band = uv.y + OzFactionPolish.BandV * uv.height;
            V(f.x < band && f.y > band + 0.05f && f.z > 0.3f && f.z < 0.5f && f.w > 0f,
              $"B the cape's strip (render uv): from {f.x:0.0000} (under the band's edge {band:0.0000}) up to its source row {f.y:0.0000}, the poster's left to {f.z:0.000}, fading over {f.w:0.000}; mip {OzFactionPolish.FillMip:0}, coverage x {OzFactionPolish.FillGrow:0.0}");
            // the glint loop
            {
                int passes = 0; bool inPass = false; float lo = 9f, hi = -9f, step = 0f, prev = -9f; bool pops = false;
                for (int k = 0; k < 23 * 60; k++)
                {
                    float t = k / 60f, p = OzFactionPolish.GlintPos(t);
                    if (p > -1f) { if (!inPass) { passes++; inPass = true; if (p > OzFactionPolish.GlintFrom + 0.05f) pops = true; } lo = Mathf.Min(lo, p); hi = Mathf.Max(hi, p); if (prev > -1f) step = Mathf.Max(step, Mathf.Abs(p - prev)); }
                    else if (inPass) { inPass = false; if (prev < OzFactionPolish.GlintTo - 0.05f) pops = true; }
                    prev = p;
                }
                float edge = Mathf.Exp(-Mathf.Pow(-OzFactionPolish.GlintFrom / OzFactionPolish.GlintWidth, 2f));
                bool calmNone = OzFactionPolish.Glint(1.7f + OzFactionPolish.GlintDelay, true).x < -1f;
                V(passes == 5 && !pops && lo <= OzFactionPolish.GlintFrom + 0.01f && hi >= OzFactionPolish.GlintTo - 0.01f && step < 0.06f && edge < 0.001f && calmNone,
                  $"B the glint: {passes} passes in 23 s (every {OzFactionPolish.GlintPeriod} s, {OzFactionPolish.GlintTime} s each), the band from {lo:0.00} to {hi:0.00} across the lenses (0 .. 1), the largest step in a frame {step:0.000} (smooth), "
                  + $"at its ends {edge:0.000000} of its light on the lenses (never pops); CALM MODE none {calmNone}");
            }
            // the eyes' glow
            {
                float lo = 9f, hi = -9f, step = 0f, prev = OzFactionPolish.GlowK(0f, false), clo = 9f, chi = -9f;
                for (int k = 1; k < 6 * 60; k++)
                {
                    float g = OzFactionPolish.GlowK(k / 60f, false), c = OzFactionPolish.GlowK(k / 60f, true);
                    lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g); step = Mathf.Max(step, Mathf.Abs(g - prev)); prev = g; clo = Mathf.Min(clo, c); chi = Mathf.Max(chi, c);
                }
                V(Mathf.Abs(lo - OzFactionPolish.GlowBase) < 0.01f && hi > 0.99f && step < 0.02f && chi <= OzFactionPolish.GlowBase + (1f - OzFactionPolish.GlowBase) * OzFactionPolish.CalmShare + 0.01f,
                  $"B the eyes' glow: a {OzFactionPolish.GlowPeriod} s pulse from {lo:0.00} to {hi:0.00} (never off), the largest step in a frame {step:0.000} (smooth); CALM MODE {clo:0.00} .. {chi:0.00}");
            }
            // the ellipse map
            {
                var rng = new System.Random(75);
                float worst = 0f;
                for (int k = 0; k < 200; k++)
                {
                    var a1 = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.1f; var a2 = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.1f;
                    if (!OzFactionPolish.Inverse(a1, a2, out var m)) continue;
                    var e1 = new Vector2(m.x * a1.x + m.y * a1.y, m.z * a1.x + m.w * a1.y); var e2 = new Vector2(m.x * a2.x + m.y * a2.y, m.z * a2.x + m.w * a2.y);
                    worst = Mathf.Max(worst, (e1 - Vector2.right).magnitude, (e2 - Vector2.up).magnitude);
                }
                V(worst < 1e-3f, $"B the spots' ellipse map: an axis maps to 1 on its edge (200 random spots, the worst error {worst:0.000000})");
            }
            Spots();
            // the row ticks
            foreach (float fps in new[] { 30f, 60f, 144f })
            {
                var ticks = SimTicks(fps, 0f);
                bool ok = ticks.Count == OzFactionDeck.Rows && ticks.Zip(ticks.Skip(1), (x, y) => y - x).All(d => d >= OzFactionPolish.RowTickGap - 1e-4f) && ticks[0] >= OzFactionDeck.RowAt - 1e-4f;
                var resumed = SimTicks(fps, 0.3f);
                V(ok && resumed.Count == OzFactionDeck.Rows - OzFactionPolish.RowsIn(0.3f),
                  $"B the row ticks at {fps:0} frames a second: {ticks.Count} (one per row) at {string.Join(", ", ticks.Select(x => x.ToString("0.000")))} s, never closer than {OzFactionPolish.RowTickGap * 1000f:0} ms; a card turned again at 0.30 s ticks {resumed.Count} (its {OzFactionPolish.RowsIn(0.3f)} rows already in do not)");
            }
            // the shade on the card
            bool mono = true; for (int k = 1; k <= 20; k++) if (OzFactionPolish.ShadeAt(k / 20f) > OzFactionPolish.ShadeAt((k - 1) / 20f) + 1e-6f) mono = false;
            V(OzFactionPolish.ShadeAlpha <= 0.2f && OzFactionPolish.ShadeW <= 8f && Mathf.Abs(OzFactionPolish.ShadeAt(0f) - 1f) < 1e-4f && OzFactionPolish.ShadeAt(1f) < 1e-4f && mono,
              $"B the poster's shade on its card: {OzFactionPolish.ShadeW:0} u wide, {OzFactionPolish.ShadeAlpha:0.00} black at the poster's edge falling smoothly to 0 (was the hard shadow: 0.50 over 10 .. 16 u plus the focus glow)");
        }

        /// <summary>The menu's tick rule (UI/OpenZMainMenu.FactionPolish.cs FcRowTick) frame by frame: the times of the ticks after a card
        /// (re)started at t0 s into its clock.</summary>
        static List<float> SimTicks(float fps, float t0)
        {
            var o = new List<float>(); int next = OzFactionPolish.RowsIn(t0); float last = -10f;
            for (int k = 0; k < (int)(fps * 1.2f); k++)
            {
                float now = k / fps, t = t0 + now;
                if (OzFactionPolish.RowDue(next, t) && now - last >= OzFactionPolish.RowTickGap) { o.Add(t); last = now; next++; }
            }
            return o;
        }

        /// <summary>The lenses and eyes through the measured cameras at rest (yaw 0, pitch 0) land where they were measured on the light
        /// models' renders (941 x 1672 px from the top left).</summary>
        static void Spots()
        {
            var want = new[] { new Vector2(426.4f, 507.5f), new Vector2(554.5f, 522.1f), new Vector2(443.4f, 558.0f), new Vector2(528.9f, 552.5f) };
            var go = new GameObject("OzV12FactionPolishCheck_cam") { hideFlags = HideFlags.HideAndDontSave };
            var root = new GameObject("OzV12FactionPolishCheck_root") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var c = go.AddComponent<Camera>(); c.enabled = false; c.aspect = 941f / 1672f;
                go.transform.SetParent(root.transform, false);
                float worst = 0f; var lines = new List<string>();
                for (int k = 0; k < 4; k++)
                {
                    int side = k < 2 ? OzFaction.Red : OzFaction.Blue;
                    var cam = OzFaction.Cam(side);
                    c.fieldOfView = cam.fov; go.transform.localPosition = cam.pos; go.transform.localRotation = cam.rot;
                    var spot = (side == OzFaction.Red ? OzFactionPolish.Lenses : OzFactionPolish.Eyes)[k % 2];
                    if (!OzFactionPolish.Project(c, root.transform, spot, 1f, out var s)) { worst = 999f; continue; }
                    var px = new Vector2(s.c.x * 941f, (1f - s.c.y) * 1672f);
                    worst = Mathf.Max(worst, (px - want[k]).magnitude);
                    lines.Add($"{(side == 0 ? "Zod's lens" : "EvilZod's eye")} {k % 2 + 1} at {px.x:0.0}, {px.y:0.0} px (measured {want[k].x:0.0}, {want[k].y:0.0}; half-axes {(s.a1 * new Vector2(941f, 1672f)).magnitude:0.0} / {(s.a2 * new Vector2(941f, 1672f)).magnitude:0.0} px)");
                }
                V(worst < 1.5f, "B the spots through the measured cameras: " + string.Join("; ", lines) + $"; the worst {worst:0.00} px");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(root); }
        }

        // ================================================================================================================ C: the wiring
        static void Wiring()
        {
            string fac = Read("Assets/OpenZ/Scripts/UI/OpenZMainMenu.Faction.cs"), pos = Read("Assets/OpenZ/Scripts/View/OzFactionPosters.cs"), shd = Read("Assets/OpenZ/Shaders/OzPosterFigure.shader");
            string[] hooks = { "BuildFactionPolish();", "CloseFactionPolish();", "var fm = FcFigureMat(i);", "TickFactionPolish(dt, active);",
                               "BuildFactionGroup();", "BuildFactionVs();", "OpenFactionDeck();", "CloseFactionDeck(sweep);", "TickFactionDeck(dt, active);", "FcLivePaper(i, behind);" };
            var bad = hooks.Where(h => Count(fac, h) != 1).ToArray();
            V(fac.Length > 0 && bad.Length == 0 && !fac.Contains("p.figure.material = fcRig.Figure;"),
              $"C the faction screen's hooks once each (k75a4's 4, k75a3's still there){(bad.Length > 0 ? ": wrong " + string.Join(" | ", bad) : "")}; the render's material is the poster's own copy");
            V(Count(pos, "var sz = RtSize(w, h); bool ss = sz.y > h;") == 1 && pos.Contains("useMipMap = ss, autoGenerateMips = ss") && pos.Contains("antiAliasing = ss ? 1 : Msaa"),
              "C the posters' render textures supersampled with mipmaps (the downsample), MSAA only when there is no room");
            V(Count(shd, "rgb += FigureFx(i.uv, c);") == 1 && Count(shd, "if (_Fill.w > 0) c = Filled(i.uv, c);") == 1 && Count(shd, "rim = max(rim, FigA(") == 8 && Count(shd, "tex2Dlod(_MainTex, float4(uv.x, _Fill.y, 0, 3))") == 1
              && Count(shd, "max(s.a, b.a * 1.6)") == 1 && Mathf.Abs(OzFactionPolish.FillMip - 3f) < 1e-4f && Mathf.Abs(OzFactionPolish.FillGrow - 1.6f) < 1e-4f,
              "C the shader's render path: the fill first, the paper rim on the filled coverage (8 taps), the glint / glow added, the fill's colour from mip 3, its coverage x 1.6 (as OzFactionPolish.FillMip / FillGrow)");
        }

        static string Read(string p) => File.Exists(p) ? File.ReadAllText(p) : "";
        static int Count(string s, string what) { int n = 0, i = 0; while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }
    }
}
#endif
