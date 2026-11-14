// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR checks (editor only): the MAYHEM fog's look by biome, counted without Play.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 fog_biome_looks, the counted check (no Play; the same text runs off Unity on the editor's Mono in the kit's proof):
    /// <list type="bullet">
    /// <item>T1 each level picks its biome (L02 DESERT, L07 LAVA, L16 JUNGLE, L17 CITY; a level not in the table: DESERT, 1.4's rule).</item>
    /// <item></item>
    /// <item>T3 the easing over 2 hours of the fog's clock per biome and level, at 240 / 144 / 60 / 30 / 4 fps and on a random clock of hitches:
    /// the cover always inside its range, both ends reached, never fixed (every 10 s it moves), no step bigger than
    /// <see cref="OzFogBiomeLooks.MaxStepPerFrame"/> or than the sine's own speed allows, the direction turns only at the two ends.</item>
    /// <item>T4 every level has its own phase; T5 the period is slow (40 .. 70 s).</item>
    /// <item>T6 non-MAYHEM unchanged: the cover and the colour on screen are today's exactly (1, TODAY) and any session value as before; MAYHEM
    /// takes the biome's, the testing panel's or a check's own values lead.</item>
    /// <item>S1-S7 the source (the kit tree): the palette numbers and names, the L16 / L07 LAND multipliers, the level start, the per-frame cover,
    /// the material's colour, the panel, the rules untouched (no Sim file, no gate or mask file reads the cover).</item>
    /// </list>
    /// </summary>
    public static class OzV14FogBiomeCount
    {
        static readonly StringBuilder sb = new StringBuilder();
        static int pass, fail;

        static void Row(string id, bool ok, string text) { if (ok) pass++; else fail++; sb.Append(ok ? "ok    " : "FAIL  ").Append(id).Append(' ').Append(text).Append('\n'); }

        static string Src(string root, string rel) { var p = Path.Combine(root, "Assets/OpenZ/Scripts/" + rel); return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : ""; }

        static string Body(string code, string head)
        {
            int i = code.IndexOf(head, StringComparison.Ordinal); if (i < 0) return null;
            int b = code.IndexOf('{', i); if (b < 0) return null;
            int d = 0;
            for (int j = b; j < code.Length; j++) { if (code[j] == '{') d++; else if (code[j] == '}' && --d == 0) return code.Substring(b, j - b + 1); }
            return null;
        }

        static int Count(string t, string s) { int n = 0, i = 0; while ((i = t.IndexOf(s, i, StringComparison.Ordinal)) >= 0) { n++; i += s.Length; } return n; }

        /// <summary>root: the project root (holding Assets/OpenZ). The report; its last line says PASS or FAIL.</summary>
        public static string Run(string root)
        {
            sb.Clear(); pass = fail = 0;
            sb.Append("OpenZ 1.4 fog_biome_looks count (no Play), project ").Append(root.Replace('\\', '/')).Append('\n');
            var B = OzFogBiomeLooks.Biomes;

            // ---- T1 each level's biome
            string[] lv = { "L02", "L07", "L16", "L17", "L99", "", null };
            string[] want = { "DESERT", "LAVA", "JUNGLE", "CITY", "DESERT", "DESERT", "DESERT" };
            bool t1 = true; var s1 = new StringBuilder();
            for (int i = 0; i < lv.Length; i++) { string got = B[OzFogBiomeLooks.Clamp(OzFogBiomeLooks.BiomeOf(lv[i]))].Biome; t1 &= got == want[i]; s1.Append(lv[i] ?? "(none)").Append(' ').Append(got).Append("; "); }
            t1 &= OzFogBiomeLooks.LevelIds.Length == OzFogBiomeLooks.LevelBiome.Length;
            // review: the level start reads the table, then the biome the map names ("planet" in Resources/OpenZ/Maps/<id>_grid.json, as read 6 Nov:
            // L02 desert, L07 volcanic, L16 jungle, L17 city): the four levels give the table's biome with their own maps' names, the table wins
            // over a map's name, a level not in the table takes its map's biome, an unknown or missing name is DESERT
            string[,] lp = { { "L02", "desert", "DESERT" }, { "L07", "volcanic", "LAVA" }, { "L16", "jungle", "JUNGLE" }, { "L17", "city", "CITY" }, { "L16", "desert", "JUNGLE" },
                             { "L99", "jungle", "JUNGLE" }, { "L99", "City", "CITY" }, { "L99", "volcanic", "LAVA" }, { "L99", "arctic", "ARCTIC" }, { "L99", "snow", "ARCTIC" },
                             { "L99", "desert", "DESERT" }, { "L99", "moon", "DESERT" }, { "L99", null, "DESERT" }, { null, null, "DESERT" } };
            for (int i = 0; i < lp.GetLength(0); i++)
            {
                string got = B[OzFogBiomeLooks.Clamp(OzFogBiomeLooks.BiomeOf(lp[i, 0], lp[i, 1]))].Biome; bool ok = got == lp[i, 2]; t1 &= ok;
                if (i < 4) t1 &= OzFogBiomeLooks.BiomeOf(lp[i, 0], lp[i, 1]) == OzFogBiomeLooks.BiomeOf(lp[i, 0]);
                if (!ok || i == 5 || i == 8) s1.Append($"{lp[i, 0] ?? "(none)"}+map {lp[i, 1] ?? "(none)"} {got}{(ok ? "" : " WRONG")}; ");
            }
            Row("T1", t1, "each level picks its biome: " + s1 + "(the table first; a level not in it takes the biome its map names, else DESERT as before)");

            var owner = new (string b, string s, float lo, float hi)[]
            { ("DESERT", "MIST LAYERS", 0.85f, 1f), ("JUNGLE", "CLOUD BANKS", 0.85f, 1f), ("CITY", "CLOUD BANKS", 0.90f, 1f), ("LAVA", "CLOUD BANKS", 0.90f, 1f), ("ARCTIC", "MIST LAYERS", 0.90f, 1f) };
            bool t2 = B.Length == owner.Length; var s2 = new StringBuilder();
            for (int i = 0; i < owner.Length && i < B.Length; i++)
            {
                bool ok = B[i].Biome == owner[i].b && B[i].Style == owner[i].s && B[i].Colour == "LAND" && B[i].CoverMin == owner[i].lo && B[i].CoverMax == owner[i].hi;
                t2 &= ok; s2.Append($"{B[i].Biome} {B[i].Style} {B[i].Colour} {B[i].CoverMin * 100f:0}-{B[i].CoverMax * 100f:0} %{(ok ? "" : " WRONG")}; ");
            }
            Row("T2", t2, "every biome's look as the owner wrote it (colour LAND everywhere): " + s2);

            // ---- T3 the easing
            double[] fps = { 240, 144, 60, 30, 4 };
            bool t3 = true; var s3 = new StringBuilder(); var rnd = new Random(20261106);
            double worstStep = 0, worstRatio = 0, leastMove10 = 1; int frames = 0;
            for (int b = 0; b < B.Length; b++)
            {
                double lo = B[b].CoverMin, hi = B[b].CoverMax, rate = OzFogBiomeLooks.MaxRatePerS(b);
                for (int l = 0; l < OzFogBiomeLooks.LevelIds.Length + 1; l++)
                {
                    double ph = OzFogBiomeLooks.PhaseOf(l < OzFogBiomeLooks.LevelIds.Length ? OzFogBiomeLooks.LevelIds[l] : "L05");
                    for (int f = 0; f <= fps.Length; f++)
                    {
                        bool hitch = f == fps.Length;
                        double t = 0, seen0 = 2, seen1 = -1; float prev = OzFogBiomeLooks.Cover(b, 0, ph); int turns = 0, dirPrev = 0;
                        bool inside = prev >= lo && prev <= hi;
                        double nextWin = 10.0;
                        while (t < 7200)
                        {
                            double dt = hitch ? (rnd.NextDouble() < 0.02 ? 0.25 : 0.001 + rnd.NextDouble() * 0.05) : 1.0 / fps[f];
                            t += dt; float v = OzFogBiomeLooks.Cover(b, t, ph); frames++;
                            inside &= v >= lo && v <= hi; seen0 = Math.Min(seen0, v); seen1 = Math.Max(seen1, v);
                            double step = Math.Abs(v - prev); worstStep = Math.Max(worstStep, step);
                            worstRatio = Math.Max(worstRatio, step / (rate * dt) - 1.0);
                            if (step > OzFogBiomeLooks.MaxStepPerFrame || step > rate * dt * 1.001 + 1e-6) inside = false;
                            int dir = v > prev + 1e-7f ? 1 : v < prev - 1e-7f ? -1 : 0;
                            if (dir != 0) { if (dirPrev != 0 && dir != dirPrev) turns++; dirPrev = dir; }
                            if (t >= nextWin)                                                         // once a second of clock: the cover's span over the last 10 s (21 points)
                            {
                                nextWin += 1.0; float a = float.MaxValue, z = float.MinValue;
                                for (int q = 0; q <= 20; q++) { float w = OzFogBiomeLooks.Cover(b, t - 10.0 + q * 0.5, ph); a = Math.Min(a, w); z = Math.Max(z, w); }
                                leastMove10 = Math.Min(leastMove10, z - a);
                            }
                            prev = v;
                        }
                        double turnsWanted = 2.0 * 7200 / OzFogBiomeLooks.PeriodS;
                        bool ends = seen0 - lo < 0.0005 && hi - seen1 < 0.0005;
                        bool ok = inside && ends && Math.Abs(turns - turnsWanted) <= 2.0;
                        t3 &= ok;
                        if (!ok) s3.Append($"{B[b].Biome} phase {ph:0.000} {(hitch ? "hitches" : fps[f] + " fps")}: inside+steps {inside}, low {seen0:0.0000} high {seen1:0.0000}, turns {turns} (about {turnsWanted:0}); ");
                    }
                }
            }
            t3 &= leastMove10 > 0.005;
            Row("T3", t3, $"the easing, 2 h of the fog's clock for each biome x 5 levels x 6 clocks ({frames} frames): always inside its range, both ends reached (within 0.05 %), "
                + $"the direction turns only at the ends (2 a swing), largest step {worstStep * 100:0.0000} % a frame (bar {OzFogBiomeLooks.MaxStepPerFrame * 100:0.00} %, "
                + $"and never above the sine's own speed x dt: worst {worstRatio * 100:+0.000;-0.000} %), every 10 s it moves at least {leastMove10 * 100:0.00} % (never fixed) " + s3);

            // ---- T4 each level its own phase; T5 slow
            var ph4 = new HashSet<double>(); var s4 = new StringBuilder(); bool t4 = true;
            foreach (var id in OzFogBiomeLooks.LevelIds) { double p = OzFogBiomeLooks.PhaseOf(id); t4 &= ph4.Add(p) && p >= 0 && p < 1 && p == OzFogBiomeLooks.PhaseOf(id); s4.Append($"{id} {p:0.000} (starts at {OzFogBiomeLooks.Cover(OzFogBiomeLooks.BiomeOf(id), 0, p) * 100f:0.0} %); "); }
            Row("T4", t4, "every level starts at its own point of the swing, the same every match: " + s4);
            Row("T5", OzFogBiomeLooks.PeriodS >= 40 && OzFogBiomeLooks.PeriodS <= 70, $"one swing (low, high, low) takes {OzFogBiomeLooks.PeriodS:0} s of the fog's clock (slow: 40 .. 70 s); low to high {OzFogBiomeLooks.PeriodS / 2:0} s");

            // ---- T6 who leads
            const int Today = 0, Land = 1, Air = 2;
            bool t6 = true;
            foreach (float e in new[] { 0.85f, 0.9f, 0.9731f, 1f }) t6 &= OzFogBiomeLooks.CoverFor(false, false, 1f, e) == 1f && OzFogBiomeLooks.CoverFor(false, true, 1f, e) == 1f;
            t6 &= OzFogBiomeLooks.CoverFor(false, false, 0.85f, 0.93f) == 0.85f && OzFogBiomeLooks.ColourFor(false, false, Today, Land, Today) == Today && OzFogBiomeLooks.ColourFor(false, true, Air, Land, Today) == Air;
            bool m6 = OzFogBiomeLooks.CoverFor(true, false, 1f, 0.9312f) == 0.9312f && OzFogBiomeLooks.ColourFor(true, false, Today, Land, Today) == Land
                   && OzFogBiomeLooks.CoverFor(true, true, 1f, 0.9312f) == 1f && OzFogBiomeLooks.CoverFor(true, false, 0.7f, 0.9312f) == 0.7f
                   && OzFogBiomeLooks.ColourFor(true, true, Today, Land, Today) == Today && OzFogBiomeLooks.ColourFor(true, false, Air, Land, Today) == Air;
            Row("T6", t6 && m6, $"not MAYHEM: the cover and colour on screen are the session's exactly (today: 100 %, TODAY) {t6}; MAYHEM: the biome's eased cover and LAND, "
                + $"the testing panel's or a check's own cover / colour lead {m6}");

            // ---- S the source
            string looks = Src(root, "View/OzFogOfWar.Looks.cs"), fow = Src(root, "View/OzFogOfWar.cs"), ren = Src(root, "View/OzFogOfWar.Render.cs"), pan = Src(root, "View/OzFogOfWar.Panel.cs"),
                   mini = Src(root, "View/OzFogOfWar.Minimap.cs"), bio = Src(root, "View/OzFogOfWar.Biomes.cs");
            bool sNames = looks.Contains("public const int Desert = 0, Jungle = 1, City = 2, Volcano = 3, Arctic = 4;") && looks.Contains("{ \"DESERT\", \"JUNGLE\", \"CITY\", \"VOLCANO\", \"ARCTIC\" }")
                       && looks.Contains("ColourNames = { \"TODAY\", \"LAND\", \"AIR\", \"DUSK\" }") && looks.Contains("var p = new Palette[5];") && looks.Contains("p[Arctic] = P(")
                       && Count(looks, "Name = \"CLOUD SEA\"") == 1 && Count(looks, "Name = \"CLOUD BANKS\"") == 1 && Count(looks, "Name = \"MIST LAYERS\"") == 1;
            Row("S1", sNames, "the palettes are the biomes (Desert 0, Jungle 1, City 2, Volcano 3 = LAVA, Arctic 4: five palettes, five minimap colours, five names); the styles and colour ideas the table names exist");
            bool sLand = looks.Contains("p[Jungle].Land = new Vector3(0.6133f, 0.4801f, 0.3020f);") && looks.Contains("p[Volcano].Land = new Vector3(0.8149f, 0.9450f, 0.9627f);")
                      && looks.Contains("p[Desert].Land = new Vector3(0.8101f, 0.4545f, 0.3468f);") && looks.Contains("p[City].Land = new Vector3(1.0290f, 0.6639f, 0.4296f);");
            Row("S2", sLand, "LAND multipliers: L02 and L17 as fitted on 5 Nov, L16 0.6133 0.4801 0.3020 and L07 0.8149 0.9450 0.9627 (the 23:00 fog opening videos run, TODO_1_4.md)");
            string decide = Body(fow, "void Decide()") ?? "", whyOff = Body(fow, "string WhyOff()") ?? "";
            int iW = decide.IndexOf("wanted = why == null;", StringComparison.Ordinal), iB = decide.IndexOf("BiomeDecide();", StringComparison.Ordinal), iS = decide.IndexOf("if (wanted) StartFog();", StringComparison.Ordinal);
            bool sDecide = iW >= 0 && iB > iW && iS > iB && decide.Contains("sessionLook >= 0 ? sessionLook : Recommended") && !whyOff.Contains("Biome")
                        && looks.Contains("PlanetOf(string level) => OzFogBiomeLooks.BiomeOf(level);") && bio.Contains("public static bool BiomeOn => I && I.wanted && I.game && I.game.IsMayhem;")
                        && (Body(bio, "void BiomeDecide()") ?? "").IndexOf("planetAuto = OzFogBiomeLooks.BiomeOf(game ? game.level : null, W != null && W.Map != null ? W.Map.planet : null);", StringComparison.Ordinal) >= 0
                        && (Body(bio, "void BiomeDecide()") ?? "").IndexOf("planetAuto =", StringComparison.Ordinal) < (Body(bio, "void BiomeDecide()") ?? "").IndexOf("if (!BiomeOn) return;", StringComparison.Ordinal);
            Row("S3", sDecide, "the level start: the level's biome (PlanetOf = the table), the biome look only in a MAYHEM match with the fog, after the match's choice and before the fog starts; WhyOff (when the fog is on) untouched");
            string setMat = Body(ren, "void SetMaterial()") ?? "", apply = Body(looks, "static void ApplyLook(") ?? "";
            int iStep = setMat.IndexOf("BiomeStep();", StringComparison.Ordinal), iVer = setMat.IndexOf("ver = lookVersion", StringComparison.Ordinal), iApply = setMat.IndexOf("ApplyLook(mat,", StringComparison.Ordinal),
                iSee = setMat.IndexOf("mat.SetFloat(IdSee, 1f - CoverNow);", StringComparison.Ordinal);
            bool sFrame = iStep >= 0 && iVer > iStep && iApply > iVer && iSee > iApply && apply.Contains("TintOf(planet, ColourNow)") && apply.Contains("m.SetFloat(IdSee, 1f - CoverNow);")
                       && !apply.Contains("1f - cover)") && !apply.Contains("TintOf(planet, colourIdea)") && Count(mini, "look * 8 + PlanetIndex") == 2;
            Row("S4", sFrame, "every frame: the biome clock steps, the cover on screen goes onto the material (_OzFowSee = 1 - CoverNow), the colour on screen into the look; the minimap's key fits five planets");
            bool sPanel = pan.Contains("planetOverride >= Palettes.Length - 1 ? -1") && pan.Contains("panelColour = true;") && pan.Contains("panelCover = true;") && pan.Contains("ColourNames[ColourNow]") && pan.Contains("CoverNow * 100f");
            Row("S5", sPanel, "the testing panel: PLANET goes round the five biomes, its COLOUR / COVER lead once used, its labels show what is on screen");
            var reads = new List<string>(); var simHits = new List<string>();
            string scripts = Path.Combine(root, "Assets/OpenZ/Scripts");
            if (Directory.Exists(scripts))
                foreach (var p in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
                {
                    string n = p.Replace('\\', '/'), t = File.ReadAllText(p);
                    bool mine = n.EndsWith("/View/OzFogOfWar.Looks.cs") || n.EndsWith("/View/OzFogOfWar.Render.cs") || n.EndsWith("/View/OzFogOfWar.Panel.cs") || n.EndsWith("/View/OzFogOfWar.Biomes.cs")
                             || n.Contains("/Editor/OzV14FogBiome");
                    if (!mine && (t.Contains("CoverNow") || t.Contains("ColourNow") || t.Contains("BiomeEases"))) reads.Add(Path.GetFileName(p));
                    if (n.Contains("/Scripts/Sim/") && (t.Contains("OzFogBiome") || t.Contains("Biome"))) simHits.Add(Path.GetFileName(p));
                }
            Row("S6", reads.Count == 0, "the cover and colour on screen are read only by the fog's look (Looks / Render / Panel / Biomes): the gate, the reveal map, Covers and the hold-off never read them"
                + (reads.Count > 0 ? ": also in " + string.Join(", ", reads) : ""));
            Row("S7", simHits.Count == 0 && Directory.Exists(scripts), "no Sim file names a biome (view only: the rules, the computer and float_check see nothing new)" + (simHits.Count > 0 ? ": " + string.Join(", ", simHits) : ""));

            sb.Append(fail == 0 ? $"PASS: {pass} rows\n" : $"FAIL: {fail} of {pass + fail} rows\n");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Fog Biome Looks: Count (no Play)")]
        static void Menu()
        {
            string r = Run(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")));
            try { Directory.CreateDirectory(Path.Combine(UnityEngine.Application.dataPath, "../Logs")); File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "../Logs/oz_v14_fog_biome_count.txt"), r); } catch (Exception) { }
            if (r.Contains("\nPASS:")) UnityEngine.Debug.Log("[OpenZ V1.4 fog biome looks] " + r); else UnityEngine.Debug.LogError("[OpenZ V1.4 fog biome looks] " + r);
        }
#endif
    }
}
