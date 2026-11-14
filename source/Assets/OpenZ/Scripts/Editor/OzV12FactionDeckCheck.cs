#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// k75a3, the faction screen's VS sign, live prints and DOSSIER cards (1.2 owner items 101 / 102): the counted check in edit mode (no
    /// Play, nothing saved; menu OpenZ/V1.2/Faction Screen: 75a3 Counted Check) and the Play proof menus (View/OzFactionDeckProof.cs).
    ///  A  the assets: the two VS signs and their halos (sizes, premultiplied: no colour bleed), the two cards, the icon atlas, the two
    ///     motion maps (data: linear, no mipmaps), the three fonts (whole font data, their OFL texts with each copyright line, named in
    ///     the credits and the licences), the paper sounds (form, lengths, their two sound events and clip levels), the shader's live print
    ///     and VS properties (no errors), the memory the screen adds while it shows.
    ///  B  the words (the owner's ten lines word for word, the icons that fit them), the VS pulse and light band, the card's slide, tilt
    ///     and rows, the one-card-at-a-time rule (a simulated pointer), the live prints' motion (every move stays inside the motion maps'
    ///     margins and never shows the plates' edges under the frame).
    ///  C  the wiring: the hooks in UI/OpenZMainMenu.Faction.cs, once each.
    ///  D  the layout at 1920 x 1080, 1366 x 768, 1280 x 1024, 1024 x 768, 2560 x 1600 and 3440 x 1440: inside the screen, nothing
    ///     overlapping (the VS at its largest pulse, the posters at their focus size, a card out), under the title and the keys row.
    /// Lines in the kit's proof/counted_check.txt; one line "[OzV12FactionDeckCheck] PASS n/n" (or FAIL).
    /// </summary>
    public static class OzV12FactionDeckCheck
    {
        public const string CheckMenu = "OpenZ/V1.2/Faction Screen: 75a3 Counted Check";
        public const string ProofMenu = "OpenZ/V1.2/Faction Screen: 75a3 Play Proof (Play)";
        public const string FramesMenu = "OpenZ/V1.2/Faction Screen: 75a3 Video Frames (Play)";
        public const string StopMenu = "OpenZ/V1.2/Faction Screen: 75a3 Play Proof: stop";
        const string T = "[OzV12FactionDeckCheck] ";
        const string Root = "Assets/OpenZ/Resources/";

        [MenuItem(ProofMenu)]
        static void Proof() { FocusGame(); OzFactionDeckProof.Run(false); }
        [MenuItem(ProofMenu, true)] static bool ProofReady() => Application.isPlaying;
        [MenuItem(FramesMenu)]
        static void Frames() { FocusGame(); OzFactionDeckProof.Run(true); }
        [MenuItem(FramesMenu, true)] static bool FramesReady() => Application.isPlaying;
        [MenuItem(StopMenu)]
        static void Stop() => OzFactionDeckProof.Stop();

        static void FocusGame()
        {
            var gv = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
        }

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
            Line("k75a3 the faction screen's VS sign, live prints and DOSSIER cards: the counted check (edit mode)");
            try { Assets(); } catch (Exception e) { V(false, "A the assets threw " + e.GetType().Name + ": " + e.Message); }
            try { Rules(); } catch (Exception e) { V(false, "B the rules threw " + e.GetType().Name + ": " + e.Message); }
            try { Wiring(); } catch (Exception e) { V(false, "C the wiring threw " + e.GetType().Name + ": " + e.Message); }
            try { Layout(); } catch (Exception e) { V(false, "D the layout threw " + e.GetType().Name + ": " + e.Message); }
            string head = T + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail);
            sb.Insert(0, head + "\n");
            try
            {
                string dir = OzFactionDeckProof.Folder();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "counted_check.txt"), sb.ToString());
                head += " (" + Path.Combine(dir, "counted_check.txt").Replace('\\', '/') + ")";
            }
            catch (Exception e) { head += " (the lines could not be written: " + e.Message + ")"; }
            Debug.Log(sb.ToString());
            return head;
        }

        // ================================================================================================================ A: the assets
        static Texture2D Tex(string res, out TextureImporter ti)
        {
            string p = Root + res + ".png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p); ti = AssetImporter.GetAtPath(p) as TextureImporter;
            return t && Resources.Load<Texture2D>(res) == t ? t : null;
        }

        static void Assets()
        {
            long bytes = 0;
            for (int v = 0; v < 2; v++)
            {
                var t = Tex(OzFactionDeck.VsTex[v], out var ti); var h = Tex(OzFactionDeck.VsHaloTex[v], out var hi);
                var px = OzFactionDeck.VsPx[v];
                bool ok = t && ti != null && t.width == px.x && t.height == px.y && !ti.alphaIsTransparency && ti.sRGBTexture && ti.mipmapEnabled && t.wrapMode == TextureWrapMode.Clamp;
                float aspect = OzFactionDeck.VsSize[v].x / OzFactionDeck.VsSize[v].y, pa = px.x / (float)px.y;
                V(ok && Mathf.Abs(aspect - pa) < 0.01f, $"A the {(v == 0 ? "painted (item 101)" : "glowing (item 102)")} VS sign {(t ? t.width + " x " + t.height + " " + t.format : "MISSING")}: premultiplied (no colour bleed into the clear pixels) {(ti != null && !ti.alphaIsTransparency)}, sRGB, mipmaps, clamp; drawn {OzFactionDeck.VsSize[v].x:0} x {OzFactionDeck.VsSize[v].y:0} u (its own aspect: {aspect:0.000} / {pa:0.000})");
                int hw = Mathf.RoundToInt((px.x + 2 * OzFactionDeck.HaloMarginPx) / 4f), hh = Mathf.RoundToInt((px.y + 2 * OzFactionDeck.HaloMarginPx) / 4f);
                V(h && Mathf.Abs(h.width - hw) <= 1 && Mathf.Abs(h.height - hh) <= 1, $"A its halo {(h ? h.width + " x " + h.height : "MISSING")} (a quarter of the sign with {OzFactionDeck.HaloMarginPx:0} px round it: {hw} x {hh})");
                if (t) bytes += Profiler.GetRuntimeMemorySizeLong(t);
                if (h) bytes += Profiler.GetRuntimeMemorySizeLong(h);
            }
            for (int i = 0; i < 2; i++)
            {
                var t = Tex(OzFactionDeck.CardTex[i], out var ti);
                V(t && t.width == OzFactionDeck.CardPx[i].x && t.height == OzFactionDeck.CardPx[i].y && ti.alphaIsTransparency && ti.mipmapEnabled && ti.npotScale == TextureImporterNPOTScale.None,
                  $"A the {(i == 0 ? "Zod" : "EvilZod")} DOSSIER card {(t ? t.width + " x " + t.height + " " + t.format : "MISSING")} (wanted {OzFactionDeck.CardPx[i].x} x {OzFactionDeck.CardPx[i].y}), its paper drawn {OzFactionDeck.CardW:0} x {OzFactionDeck.CardH(i):0} u");
                if (t) bytes += Profiler.GetRuntimeMemorySizeLong(t);
                var m = Tex(OzFactionDeck.MotionTex[i], out var mi);
                V(m && mi != null && m.width == 288 && m.height == 512 && !mi.sRGBTexture && !mi.mipmapEnabled && !mi.alphaIsTransparency && mi.alphaSource == TextureImporterAlphaSource.FromInput,
                  $"A the {(i == 0 ? "Zod" : "EvilZod")} poster's motion map {(m ? m.width + " x " + m.height + " " + m.format : "MISSING")}: linear {(mi != null && !mi.sRGBTexture)}, no mipmaps {(mi != null && !mi.mipmapEnabled)}, the four channels kept {(mi != null && !mi.alphaIsTransparency)}");
                if (m) bytes += Profiler.GetRuntimeMemorySizeLong(m);
            }
            {
                var t = Tex(OzFactionDeck.IconTex, out var ti);
                V(t && t.width == 128 * OzFactionDeck.IconCols && t.height == 128 * OzFactionDeck.IconRowsInAtlas && ti.mipmapEnabled, $"A the row icons' atlas {(t ? t.width + " x " + t.height : "MISSING")} (5 x 2 cells of 128 px: {string.Join(", ", OzFactionDeck.IconName)})");
                if (t) bytes += Profiler.GetRuntimeMemorySizeLong(t);
            }
            // the fonts and their licences
            var fonts = new[] { (OzFactionDeck.FontHeading, "Staatliches", "OFL_Staatliches", "Copyright 2018 The Staatliches Project Authors"),
                                (OzFactionDeck.FontBody, "Courier Prime", "OFL_CourierPrime", "Copyright 2015 The Courier Prime Project Authors"), (OzFactionDeck.FontBold, "Courier Prime", "OFL_CourierPrime", "Copyright 2015 The Courier Prime Project Authors") };   // k75a4: the rows in Courier Prime
            foreach (var f in fonts)
            {
                string p = Root + f.Item1 + ".ttf";
                var font = AssetDatabase.LoadAssetAtPath<Font>(p); var fi = AssetImporter.GetAtPath(p) as TrueTypeFontImporter;
                var lic = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + OzFactionDeck.Folder + "Fonts/" + f.Item3 + ".txt");
                bool ok = font && Resources.Load<Font>(f.Item1) == font && fi != null && fi.includeFontData && fi.fontNames != null && fi.fontNames.Contains(f.Item2)
                          && lic && lic.text.Contains("SIL Open Font License, Version 1.1") && lic.text.Contains(f.Item4);
                V(ok, $"A the font {Path.GetFileName(p)}: {(font ? "loads" : "MISSING")}, whole font data {(fi != null && fi.includeFontData)}, named {f.Item2}; its licence {f.Item3}.txt: OFL 1.1 with its copyright line {(lic && lic.text.Contains(f.Item4))}");
            }
            string credits = File.Exists(Root + "OpenZ/Menu/credits.txt") ? File.ReadAllText(Root + "OpenZ/Menu/credits.txt") : "";
            string licences = File.Exists("Assets/StreamingAssets/LICENSES.txt") ? File.ReadAllText("Assets/StreamingAssets/LICENSES.txt").Replace("\r\n", "\n") : "";   // the file is CRLF
            V(credits.Contains("Staatliches") && credits.Contains("Source Code Pro") && licences.Contains("Staatliches font") && licences.Contains("Source Code\n   Pro font"),
              $"A the credits name the two new fonts {credits.Contains("Staatliches") && credits.Contains("Source Code Pro")}; the licences {licences.Contains("Staatliches font")}");
            // the paper sounds
            var lens = new[] { ("s3_ui_paper_slide_a", 0.42f), ("s3_ui_paper_slide_b", 0.38f), ("s3_ui_paper_back_a", 0.26f) };
            var refImp = AssetImporter.GetAtPath(Root + "OpenZ/Audio/Sfx/SoundV3/s3_ui_band_whoosh.wav") as AudioImporter;
            string table = File.Exists(Root + "OpenZ/Audio/openz_sound_events.json") ? File.ReadAllText(Root + "OpenZ/Audio/openz_sound_events.json") : "";
            string levels = File.Exists(Root + "OpenZ/Audio/openz_clip_levels.json") ? File.ReadAllText(Root + "OpenZ/Audio/openz_clip_levels.json") : "";
            foreach (var l in lens)
            {
                string p = Root + "OpenZ/Audio/Sfx/SoundV3/" + l.Item1 + ".wav";
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p); var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                bool same = ai != null && refImp != null && ai.defaultSampleSettings.loadType == refImp.defaultSampleSettings.loadType && ai.forceToMono == refImp.forceToMono;
                V(c && c.channels == 1 && c.frequency == 44100 && Mathf.Abs(c.length - l.Item2) < 0.01f && same && levels.Contains("\"n\": \"" + l.Item1 + "\""),
                  $"A the paper sound {l.Item1}: {(c ? c.length.ToString("0.000") + " s, " + c.channels + " ch, " + c.frequency + " Hz" : "MISSING")} (wanted {l.Item2:0.00} s, mono 44.1 kHz), imported as the UI's other clips {same}, in the clip levels {levels.Contains("\"n\": \"" + l.Item1 + "\"")}");
            }
            V(table.Contains("\"id\": \"" + OzFactionDeck.SoundIn + "\"") && table.Contains("\"id\": \"" + OzFactionDeck.SoundOut + "\"") && table.Contains("OpenZ/Audio/Sfx/SoundV3/s3_ui_paper_slide_b") && table.Contains("OpenZ/Audio/Sfx/SoundV3/s3_ui_paper_back_a"),
              $"A the sound table has the card's {OzFactionDeck.SoundIn} and {OzFactionDeck.SoundOut} (k75a4: the owner's takes) and this kit's paper sounds (kept, not played)");
            // the shader
            {
                var mat = Resources.Load<Material>(OzFactionPosters.Folder + OzFactionPosters.FigureMaterial);
                var sh = mat ? mat.shader : null;
                string[] props = { "_Live", "_LiveT", "_MotionMap", "_LayerPx", "_Spin", "_SpinW", "_SpinMirror", "_Breathe", "_BreatheW", "_Wave", "_WaveW", "_Drift", "_DriftW", "_Glow", "_GlowW", "_Sweep", "_SweepTone" };
                var missing = props.Where(p => !mat || !mat.HasProperty(p)).ToArray();
                V(sh && !ShaderUtil.ShaderHasError(sh) && missing.Length == 0 && mat.HasProperty("_PaperSee"),
                  $"A OzPosterFigure (the print and the VS sign): errors {(sh ? ShaderUtil.ShaderHasError(sh).ToString() : "MISSING")}, the live print and light band properties {(missing.Length == 0 ? "all " + props.Length : "missing " + string.Join(", ", missing))}");
            }
            V(bytes < 40L * 1024 * 1024, $"A the memory the screen adds while it shows (the editor's count of its pictures; the fonts' atlases are 3 x 1 MB more): {bytes / 1048576f:0.0} MB (under 40 MB)");
        }

        // ================================================================================================================ B: the rules
        static readonly string[][] Owner =
        {
            new[] { "Grumpiness: 100%", "Rocket Fuel Tolerance: Legendary", "Patience: 2%", "Mood Forecast: Stormy", "Tolerance for nonsense - none" },
            new[] { "Tendencies: World Hostile Takeover", "Diplomacy: Not an option", "Ego levels: Critical", "Boardroom Villainy: 100%", "Treachery Potential: Peak" },
        };
        static readonly string[][] Icons = { new[] { "angry face", "rocket", "hourglass", "storm cloud", "no-sign" }, new[] { "globe", "handshake", "crown", "briefcase", "dagger" } };

        static void Rules()
        {
            // the words
            for (int s = 0; s < 2; s++)
            {
                var got = Enumerable.Range(0, OzFactionDeck.Rows).Select(r => OzFactionDeck.Line(s, r)).ToArray();
                bool words = got.SequenceEqual(Owner[s]);
                bool bold = Enumerable.Range(0, 5).All(r => OzFactionDeck.RowText(s, r, false) == OzFactionDeck.Label[s][r] + " <b>" + OzFactionDeck.Value[s][r] + "</b>"
                                                           && OzFactionDeck.RowText(s, r, true) == OzFactionDeck.Label[s][r] + "\n<b>" + OzFactionDeck.Value[s][r] + "</b>");
                var icons = Enumerable.Range(0, 5).Select(r => OzFactionDeck.IconName[OzFactionDeck.Icon[s][r]]).ToArray();
                V(words && bold && icons.SequenceEqual(Icons[s]) && OzFaction.General[s] == (s == 0 ? "GENERAL ZOD" : "GENERAL EVILZOD"),
                  $"B the {(s == 0 ? "Zod" : "EvilZod")} card's words, word for word: \"{OzFactionDeck.DossierWord} {OzFaction.General[s]}\" / {string.Join(" / ", got)}; the value bold {bold}; the icons {string.Join(", ", icons)}");
            }
            // the VS pulse and its light
            {
                float lo = 9f, hi = -9f, step = 0f, prev = OzFactionDeck.VsScale(0f, false), hLo = 9f, hHi = -9f; bool loop = true;
                for (int k = 1; k <= 600; k++)
                {
                    float t = k / 120f, s = OzFactionDeck.VsScale(t, false), h = OzFactionDeck.HaloAlpha(t, false);
                    lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); hLo = Mathf.Min(hLo, h); hHi = Mathf.Max(hHi, h);
                    step = Mathf.Max(step, Mathf.Abs(s - prev)); prev = s;
                    if (Mathf.Abs(OzFactionDeck.VsScale(t + OzFactionDeck.PulsePeriod, false) - s) > 1e-4f) loop = false;
                }
                float calm = OzFactionDeck.VsScale(OzFactionDeck.PulsePeriod * 0.5f, true);
                V(Mathf.Abs(lo - 1f) < 1e-3f && Mathf.Abs(hi - 1.04f) < 1e-3f && step < 0.0012f && loop && hLo >= OzFactionDeck.HaloLo - 1e-3f && hHi <= OzFactionDeck.HaloHi + 1e-3f && Mathf.Abs(calm - 1.01f) < 1e-3f,
                  $"B the VS pulse: scale {lo:0.000} .. {hi:0.000} over {OzFactionDeck.PulsePeriod} s (a cosine: the largest step in 1/120 s {step:0.0000}, it loops {loop}), its glow {hLo:0.00} .. {hHi:0.00}; CALM MODE to {calm:0.000}");
                int passes = 0; bool inPass = false, smooth = true; float last = -1f, minPos = 9f, maxPos = -9f;
                for (int k = 0; k <= 3600; k++)
                {
                    float t = k / 200f, p = OzFactionDeck.SweepPos(t);
                    bool on = p > -0.5f;
                    if (on && !inPass) passes++;
                    if (on && inPass && Mathf.Abs(p - last) > 0.03f) smooth = false;
                    if (on) { minPos = Mathf.Min(minPos, p); maxPos = Mathf.Max(maxPos, p); }
                    inPass = on; last = p;
                }
                V(passes == Mathf.CeilToInt(18f / OzFactionDeck.SweepPeriod) && smooth && minPos >= -0.3f - 1e-3f && maxPos <= 1.3f + 1e-3f && OzFactionDeck.VsSweep(1f, true).z == 0f,
                  $"B the VS light band: {passes} passes in 18 s (one every {OzFactionDeck.SweepPeriod} s, each {OzFactionDeck.SweepTime} s), from {minPos:0.00} to {maxPos:0.00} along the sign's diagonal (outside it at both ends: it never pops), smooth {smooth}; none in CALM MODE");
            }
            // the card's slide, tilt and rows
            {
                bool mono = true; float peak = 0f, prev = 0f, tPeak = 0f;
                for (int k = 1; k <= 200; k++) { float t = k / 200f * OzFactionDeck.CardIn, s = OzFactionDeck.SlideIn(t); if (s > peak) { peak = s; tPeak = t; } if (t < tPeak + 1e-6f && s + 1e-5f < prev) mono = false; prev = s; }
                bool ends = Mathf.Abs(OzFactionDeck.SlideIn(0f)) < 1e-5f && Mathf.Abs(OzFactionDeck.SlideIn(OzFactionDeck.CardIn) - 1f) < 1e-4f;
                bool outOk = Mathf.Abs(OzFactionDeck.SlideOut(1f, 0f) - 1f) < 1e-5f && OzFactionDeck.SlideOut(1f, OzFactionDeck.CardOut) == 0f && OzFactionDeck.SlideOut(1f, OzFactionDeck.CardOut * 0.5f) > 0.5f;
                float inv = 0f; foreach (var kk in new[] { 0.1f, 0.4f, 0.8f, 0.97f }) inv = Mathf.Max(inv, Mathf.Abs(OzFactionDeck.SlideIn(OzFactionDeck.SlideInTime(kk)) - kk));
                V(ends && mono && peak <= 1.1f && peak > 1f && outOk && inv < 1e-3f,
                  $"B the card's slide: 0 -> 1 in {OzFactionDeck.CardIn} s, rising to {peak:0.000} (a small overshoot) at {tPeak:0.00} s; back in {OzFactionDeck.CardOut} s {outOk}; turned again on its way back it goes on from where it is (off by {inv:0.0000})");
                for (int s = 0; s < 2; s++)
                {
                    float rest = OzFactionDeck.RestTilt[s], t0 = OzFactionDeck.TiltAt(s, 0f), tEnd = OzFactionDeck.TiltAt(s, OzFactionDeck.InDone), over = 0f;
                    for (int k = 1; k <= 300; k++) over = Mathf.Max(over, Mathf.Abs(OzFactionDeck.TiltAt(s, k / 300f * OzFactionDeck.InDone) - rest));
                    V(Mathf.Abs(t0 - rest - (s == 0 ? 1f : -1f) * OzFactionDeck.TiltIn) < 1e-3f && Mathf.Abs(tEnd - rest) < 0.25f && over <= OzFactionDeck.TiltIn + 1e-3f,
                      $"B the {(s == 0 ? "Zod" : "EvilZod")} card's tilt: from {t0:0.0} deg (turned outward) swinging to its rest {rest:0.00} deg, {Mathf.Abs(tEnd - rest):0.00} deg off it at {OzFactionDeck.InDone} s, never past the start");
                }
                bool order = true; for (int r = 1; r < OzFactionDeck.Rows; r++) order &= OzFactionDeck.RowAt + r * OzFactionDeck.RowStagger > OzFactionDeck.RowAt + (r - 1) * OzFactionDeck.RowStagger;
                float lastIn = OzFactionDeck.RowAt + (OzFactionDeck.Rows - 1) * OzFactionDeck.RowStagger + OzFactionDeck.RowFade;
                V(order && lastIn <= OzFactionDeck.InDone && OzFactionDeck.RowIn(-1, OzFactionDeck.HeadAt) == 0f && OzFactionDeck.RowIn(0, OzFactionDeck.RowAt + OzFactionDeck.RowFade) >= 0.999f,
                  $"B the rows appear one after another: the heading at {OzFactionDeck.HeadAt} s, row 1 at {OzFactionDeck.RowAt} s, then every {OzFactionDeck.RowStagger} s ({OzFactionDeck.RowFade} s each): all in by {lastIn:0.00} s");
            }
            // one card at a time: a simulated pointer (Zod 1 s, EvilZod 1 s, away, EvilZod, Zod at once)
            {
                var st = new[] { OzFactionDeck.CardState.Hidden, OzFactionDeck.CardState.Hidden }; var since = new float[2];
                int both = 0, starts = 0, turns = 0; float dt = 1f / 60f, firstEvil = -1f;
                int Want(float t) => t < 1f ? 0 : t < 2f ? 1 : t < 2.5f ? -1 : t < 3.2f ? 1 : 0;
                for (int k = 0; k < 4.5f / dt; k++)
                {
                    float t = k * dt; int a = Want(t);
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < 2; i++)
                        {
                            bool want = a == i; if ((pass == 0) == want) continue;
                            var s = OzFactionDeck.Step(st[i], want, st[1 - i] != OzFactionDeck.CardState.Hidden, t - since[i], out bool started, out bool turned);
                            if (started) { starts++; if (i == 1 && firstEvil < 0f) firstEvil = t; }
                            if (turned) turns++;
                            if (s != st[i] || started || turned) since[i] = t;
                            st[i] = s;
                        }
                    if (st[0] != OzFactionDeck.CardState.Hidden && st[1] != OzFactionDeck.CardState.Hidden) both++;
                }
                V(both == 0 && starts == 4 && turns == 3 && firstEvil > 1f && firstEvil <= 1f + OzFactionDeck.CardOut + 3f * dt,
                  $"B one card at a time (a pointer: Zod, EvilZod, away, EvilZod, Zod): frames with both cards out {both}; {starts} cards out, {turns} back (a paper sound each); EvilZod's card starts {(firstEvil - 1f) * 1000f:0} ms after the pointer moves (Zod's goes back first, {OzFactionDeck.CardOut * 1000f:0} ms)");
            }
            // the live prints: every move inside its motion map's margin, never into the plates' extended edges
            for (int s = 0; s < 2; s++)
            {
                var map = LoadMap(OzFactionDeck.MotionTex[s]);
                if (map == null) { V(false, $"B the {(s == 0 ? "Zod" : "EvilZod")} motion map could not be read"); continue; }
                for (int layer = 0; layer < 2; layer++)
                {
                    var mo = OzFactionDeck.MotionOf(s, layer);
                    float maxMove = 0f, maxOut = 0f; int weighted = 0;
                    Rect open = s == 0 ? Rect.MinMaxRect(56f, 54f, 1096f, 1990f) : Rect.MinMaxRect(53f, 32f, 1112f, 2006f);   // the frames' openings (layer px, from the top left)
                    for (int y = 0; y < 512; y += 2)
                        for (int x = 0; x < 288; x += 2)
                        {
                            var w = map[(511 - y) * 288 + x];
                            float any = Vector4.Dot(w, mo.spinW) + Vector4.Dot(w, mo.breatheW) + Vector4.Dot(w, mo.waveW) + Vector4.Dot(w, mo.driftW);
                            if (any < 0.01f) continue;
                            weighted++;
                            var p = new Vector2((x + 0.5f) * 4f, (y + 0.5f) * 4f);                                         // layer px from the top left
                            for (int k = 0; k < 24; k++)
                            {
                                float t = k * 0.37f;
                                var q = SampleAt(mo, w, p, t);
                                maxMove = Mathf.Max(maxMove, (q - p).magnitude);
                                if (open.Contains(p) && !open.Contains(q)) maxOut = Mathf.Max(maxOut, Mathf.Max(Mathf.Max(open.xMin - q.x, q.x - open.xMax), Mathf.Max(open.yMin - q.y, q.y - open.yMax)));
                            }
                        }
                    float margin = s == 0 ? (layer == 0 ? 30f : 26f) : (layer == 0 ? 30f : 14f);
                    if (s == 1 && layer == 1) margin = Mathf.Max(margin, 22f * (mo.driftW.z > 0f ? 1f : 0f));
                    V(weighted > 0 && maxMove <= margin && maxOut <= 0.5f,
                      $"B the {(s == 0 ? "Zod" : "EvilZod")} {(layer == 0 ? "back plate" : "mid layer")} in focus: {weighted} weighted map points, the largest move {maxMove:0.0} px (its map reaches {margin:0} px round what moves), nothing inside the frame takes its picture from under the frame (by {maxOut:0.0} px)");
                }
            }
        }

        /// <summary>The motion map's pixels (read from the file: the imported texture is not readable); y from the bottom, as uv.</summary>
        static Vector4[] LoadMap(string res)
        {
            string p = Root + res + ".png";
            if (!File.Exists(p)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!t.LoadImage(File.ReadAllBytes(p), false) || t.width != 288 || t.height != 512) return null;
                return t.GetPixels32().Select(c => new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f)).ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        /// <summary>Where the shader's live print samples (layer px from the top left): OzPosterFigure's LiveUV, written again here.</summary>
        static Vector2 SampleAt(OzFactionDeck.Motion m, Vector4 w, Vector2 pTop, float t)
        {
            float L = OzFactionDeck.LayerH, tau = Mathf.PI * 2f;
            var p = new Vector2(pTop.x, L - pTop.y);                                                                 // y up, as uv
            var uv = new Vector2(p.x / OzFactionDeck.LayerW, p.y / L);
            float ws = Vector4.Dot(w, m.spinW);
            if (m.spin.w > 0.001f && ws > 0.0001f)
            {
                var c = new Vector2(m.spin.x * OzFactionDeck.LayerW, (1f - m.spin.y) * L); var r = p - c;
                float side = m.spinMirror > 0.5f ? Mathf.Clamp(r.x / 40f, -1f, 1f) : 1f;
                float a = -m.spin.z * Mathf.Deg2Rad * Mathf.Sin(tau * t / m.spin.w) * side * ws;
                float sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                p = c + new Vector2(ca * r.x - sa * r.y, sa * r.x + ca * r.y);
            }
            float wb = Vector4.Dot(w, m.breatheW);
            if (m.breathe.w > 0.001f && wb > 0.0001f)
            {
                var c = new Vector2(m.breathe.x * OzFactionDeck.LayerW, (1f - m.breathe.y) * L);
                float k = 1f + m.breathe.z * (0.5f - 0.5f * Mathf.Cos(tau * t / m.breathe.w)) * wb;
                p = c + (p - c) / k;
            }
            float ww = Vector4.Dot(w, m.waveW);
            if (m.wave.y > 0.001f && ww > 0.0001f) p.x -= m.wave.x * ww * Mathf.Sin(tau * ((L - p.y) / m.wave.y - m.wave.z * t) + m.wave.w * uv.x);
            float wd = Vector4.Dot(w, m.driftW);
            if (m.drift.z > 0.001f && wd > 0.0001f)
                p -= new Vector2(m.drift.x * Mathf.Sin(tau * t / m.drift.z + m.drift.w * uv.x), m.drift.y * Mathf.Sin(tau * t / (m.drift.z * 1.37f) + 1.3f + m.drift.w * uv.x)) * wd;
            return new Vector2(p.x, L - p.y);
        }

        // ================================================================================================================ C: the wiring
        static void Wiring()
        {
            string f = "Assets/OpenZ/Scripts/UI/OpenZMainMenu.Faction.cs";
            string src = File.Exists(f) ? File.ReadAllText(f) : "";
            string[] hooks = { "BuildFactionGroup();", "BuildFactionVs();", "Hud.Node(\"Poster_\" + name, fcGroup ? fcGroup : fcRoot)", "Hud.Node(\"TipSlot_\" + name, fcGroup ? fcGroup : fcRoot)",
                               "OpenFactionDeck();", "CloseFactionDeck(sweep);", "|| FcCardHit(i, ptr))) over = i;", "fcLayout.ToCanvas(new Vector2(gp.x + gp.w * 0.5f, gp.y + gp.h * 0.5f))",
                               "UsePrint(p.back, FcLiveMat(i, 0, pm)); UsePrint(p.mid, FcLiveMat(i, 1, pm));", "FcLivePaper(i, behind);", "TickFactionDeck(dt, active);" };
            var bad = hooks.Where(h => Count(src, h) != 1).ToArray();
            V(src.Length > 0 && bad.Length == 0, $"C the faction screen's {hooks.Length} hooks, once each{(bad.Length > 0 ? ": wrong " + string.Join(" | ", bad) : "")}");
            V(OzFactionDeck.VsSign == OzFactionDeck.VsGrunge, $"C the VS sign shown: {(OzFactionDeck.VsSign == OzFactionDeck.VsGrunge ? "the painted one of the owner's item 101 picture (the default)" : "the glowing one of his item 102 picture")} (OzFactionDeck.VsSign)");
        }

        static int Count(string s, string what) { int n = 0, i = 0; while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }

        // ================================================================================================================ D: the layout
        static void Layout()
        {
            var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 1024), new Vector2Int(1024, 768), new Vector2Int(2560, 1600), new Vector2Int(3440, 1440) };
            foreach (var sz in sizes)
                for (int v = 0; v < 2; v++)
                {
                    if (v == 1 && sz.x != 1920) continue;                                                                          // the other sign: once, at 1920
                    float cw = sz.x * OzLayout.RefH / sz.y;
                    var L = OzFactionDeck.Group(cw, v);
                    Rect Grow(Rect r, float k) => new Rect(r.center.x - r.width * k * 0.5f, r.center.y - r.height * k * 0.5f, r.width * k, r.height * k);
                    Rect p0 = L.ToCanvas(Grow(L.poster0, 1.025f)), p1 = L.ToCanvas(Grow(L.poster1, 1.025f));               // the focused poster grows 2.5 %
                    Rect vs = L.ToCanvas(Grow(L.vs, 1f + OzFactionDeck.PulseScale));
                    // the sign's own ink is narrower than its picture: its picture may touch the posters' focus glow, its ink may not
                    float ink = v == 0 ? 0.97f : 0.80f;
                    Rect vsInk = new Rect(vs.center.x - vs.width * ink * 0.5f, vs.y, vs.width * ink, vs.height);
                    Rect a0 = L.ToCanvas(L.art0), a1 = L.ToCanvas(L.art1), c0 = L.ToCanvas(L.card0), c1 = L.ToCanvas(L.card1);
                    Rect t0 = L.ToCanvas(new Rect(L.poster0.x, L.poster0.yMax + OzFaction.TipGap, L.poster0.width, OzFaction.TipH)), t1 = L.ToCanvas(new Rect(L.poster1.x, L.poster1.yMax + OzFaction.TipGap, L.poster1.width, OzFaction.TipH));
                    float titleBottom = OzMenuLayout.SelTitleY + OzMenuLayout.SelTitleSize, hintBottom = OzMenuLayout.SelHintY + 14f;
                    float backBottom = OzMenuLayout.Back.y + OzMenuLayout.Back.h, backLeft = cw - (OzLayout.RefW - OzMenuLayout.Back.x);
                    bool inside = a0.xMin >= 8f && a1.xMax <= cw - 8f && p0.xMin >= 8f && p1.xMax <= cw - 8f && t0.yMax <= OzLayout.RefH - 16f && t1.yMax <= OzLayout.RefH - 16f;
                    bool vsFree = !vsInk.Overlaps(p0) && !vsInk.Overlaps(p1) && vs.yMin > hintBottom;
                    float tuck0 = c0.xMax - L.ToCanvas(L.poster0).xMin, tuck1 = L.ToCanvas(L.poster1).xMax - c1.xMin;
                    bool cards = !c0.Overlaps(p1) && !c1.Overlaps(p0) && !c0.Overlaps(vs) && !c1.Overlaps(vs) && tuck0 <= OzFactionDeck.CardTuck * L.scale + 0.5f && tuck1 <= OzFactionDeck.CardTuck * L.scale + 0.5f
                                 && a0.yMin > titleBottom && a1.yMin > Mathf.Max(hintBottom, backBottom) && a0.yMax < t0.yMin && a1.yMax < t1.yMin;
                    bool head = p0.yMin > titleBottom + 8f && p0.yMin > hintBottom && (a1.xMax < backLeft || a1.yMin > backBottom);
                    V(inside && vsFree && cards && head && L.scale >= 0.7f,
                      $"D {sz.x} x {sz.y} (canvas {cw:0} u){(v == 1 ? ", the glowing VS" : "")}: the group at {L.scale * 100f:0} %; posters {p0.xMin:0}..{p0.xMax:0} and {p1.xMin:0}..{p1.xMax:0} u (focus size), "
                      + $"the VS {vs.xMin:0}..{vs.xMax:0} u at its largest (its ink clear of the posters {vsFree}), the cards' pictures {a0.xMin:0}..{a0.xMax:0} and {a1.xMin:0}..{a1.xMax:0} u from {Mathf.Min(a0.yMin, a1.yMin):0} to {Mathf.Max(a0.yMax, a1.yMax):0} u "
                      + $"(tucked {tuck0:0} / {tuck1:0} u behind their posters; clear of the VS, the other poster, the title ({titleBottom:0}), the BACK button ({backBottom:0}) and the tip slots ({t0.yMin:0}) {cards}); inside the screen {inside}");
                }
        }
    }
}
#endif
