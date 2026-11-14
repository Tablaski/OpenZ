using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// cursor set, in EDIT mode (no Play, no scene). Menu: OpenZ/Checks/V1.3/Blue cursor on the blue side (edit mode).
    ///  C1  every red cursor picture has its blue twin: 10 states (move is green on both sides) x 32 / 48 / 64 px x 4 frames (Kit/Cursors)
    ///      and x 256 px x 4 frames (Kit/Cursors/HD): both load, same width and height, same import settings (type, Read/Write, mips,
    ///      filter, size, compression, sRGB, alpha), a GUID of its own.
    ///  C2  same shapes: the alpha of every pixel is the same, every pixel that is not red paint (outline, white core, flag, shading) is
    ///      the same; no red paint is left in the blue picture (red hue, some colour, not black).
    ///  C3  the names, so the hotspots and the frame timing (worked out from the state name and the size in OzKit / OzSoftCursor): the
    ///      red set's file name is the state name itself (the same string), the blue set's is "blue_" + the name, move stays "move".
    ///  C4  the side rule (OzKit.BlueCursorFor): blue only with PLAY AS BLUE active, a match HUD and the human on blue; red for the main
    ///      menu, its loading screen, the red side, the playground and a bot match.
    ///  C5  in edit mode (the main menu's state: PLAY AS BLUE not active) every state reads the red file, and the red files are byte for
    ///      byte the ones of 30 Oct 2026 (MD5 list check/red_md5.txt of the package, when it is there).
    /// Reads only; writes the report file only.
    /// </summary>
    public static class OzV13BlueCursorCheck
    {
        const string Tag = "[OpenZ blue cursor]";
        const string Res = "Assets/OpenZ/Resources/OpenZ/Kit/";
        static int ok, bad;
        static readonly StringBuilder lines = new StringBuilder(), fails = new StringBuilder();

        [MenuItem("OpenZ/Checks/V1.3/Blue cursor on the blue side (edit mode)")]
        public static void Run()
        {
            ok = bad = 0; lines.Clear(); fails.Clear();
            var names = OzSoftCursor.Names;
            int twins = 0, twinsWant = 0, sameSize = 0, sameImport = 0, ownGuid = 0, sameAlpha = 0, sameRest = 0, noRed = 0, pics = 0;
            var guids = new HashSet<string>();
            foreach (var n in names)
            {
                if (OzKit.GreenCursors.Contains(n)) continue;
                foreach (var (red, blue) in Pairs(n))
                {
                    twinsWant++;
                    string rp = Res + red + ".png", bp = Res + blue + ".png";
                    bool exists = File.Exists(rp) && File.Exists(bp);
                    var rt = Resources.Load<Texture2D>(OzKit.Root + red); var bt = Resources.Load<Texture2D>(OzKit.Root + blue);
                    if (exists && rt && bt) twins++; else { Fail($"twin missing: {blue} (red {(rt ? "loads" : "missing")}, blue {(bt ? "loads" : "missing")})"); continue; }
                    if (rt.width == bt.width && rt.height == bt.height) sameSize++; else Fail($"size {blue}: {bt.width}x{bt.height} vs red {rt.width}x{rt.height}");
                    var ri = AssetImporter.GetAtPath(rp) as TextureImporter; var bi = AssetImporter.GetAtPath(bp) as TextureImporter;
                    string rs = ImportText(ri), bs = ImportText(bi);
                    if (ri && bi && rs == bs) sameImport++; else Fail($"import {blue}: {bs} vs red {rs}");
                    string rg = AssetDatabase.AssetPathToGUID(rp), bg = AssetDatabase.AssetPathToGUID(bp);
                    if (!string.IsNullOrEmpty(bg) && bg != rg && guids.Add(bg)) ownGuid++; else Fail($"guid {blue}: {bg} (red {rg})");
                    var a = Png(rp); var b = Png(bp);
                    if (a == null || b == null || a.width != b.width || a.height != b.height) { Fail("png read " + blue); continue; }
                    pics++;
                    var pa = a.GetPixels32(); var pb = b.GetPixels32();
                    bool alpha = true, rest = true; int redLeft = 0;
                    for (int i = 0; i < pa.Length; i++)
                    {
                        if (pa[i].a != pb[i].a) alpha = false;
                        if (!Same(pa[i], pb[i]) && !IsRedPaint(pa[i], 0.01f)) rest = false;                     // a changed pixel must be red paint (a hair of slack at the rule's edges: float32 here, float64 in the tool)
                        if (IsRedPaint(pb[i], 0f)) redLeft++;
                    }
                    Object.DestroyImmediate(a); Object.DestroyImmediate(b);
                    if (alpha) sameAlpha++; else Fail("alpha differs " + blue);
                    if (rest) sameRest++; else Fail("a non-red pixel changed " + blue);
                    if (redLeft == 0) noRed++; else Fail($"red paint left in {blue}: {redLeft} px");
                }
            }
            Check($"C1.1 blue twins: {twins} of {twinsWant} (10 states x (3 sizes x 4 frames + 4 HD frames) = 160)", twins == twinsWant && twinsWant == 160);
            Check($"C1.2 same width and height: {sameSize} of {twinsWant}", sameSize == twinsWant);
            Check($"C1.3 same import settings: {sameImport} of {twinsWant}", sameImport == twinsWant);
            Check($"C1.4 a GUID of its own: {ownGuid} of {twinsWant}", ownGuid == twinsWant);
            Check($"C2.1 same alpha in every pixel: {sameAlpha} of {pics}", sameAlpha == twinsWant);
            Check($"C2.2 every non-red pixel the same: {sameRest} of {pics}", sameRest == twinsWant);
            Check($"C2.3 no red paint left: {noRed} of {pics}", noRed == twinsWant);

            int redSame = 0, blueName = 0;
            foreach (var n in names)
            {
                if (ReferenceEquals(OzKit.CursorArt(n, false), n)) redSame++;
                string want = OzKit.GreenCursors.Contains(n) ? n : OzKit.BlueCursorArt + n;
                if (OzKit.CursorArt(n, true) == want) blueName++;
            }
            Check($"C3.1 red set = the state name itself: {redSame} of {names.Length}", redSame == names.Length);
            Check($"C3.2 blue set = blue_ + the name, move unchanged: {blueName} of {names.Length}", blueName == names.Length);

            var cases = new (string what, bool pb, bool hud, Team p, bool want)[]
            {
                ("main menu", false, false, Team.Red, false), ("loading screen after a blue DEPLOY", true, false, Team.Red, false),
                ("red side match", false, true, Team.Red, false), ("blue side match", true, true, Team.Blue, true),
                ("playground / bot match (PLAY AS BLUE off)", false, true, Team.Blue, false), ("PLAY AS BLUE armed, human red", true, true, Team.Red, false),
            };
            int rules = 0; var rs2 = new StringBuilder();
            foreach (var c in cases) { bool got = OzKit.BlueCursorFor(c.pb, c.hud, c.p); if (got == c.want) rules++; rs2.Append(c.what).Append(got ? "=blue " : "=red ").Append("; "); }
            Check($"C4 the side rule: {rules} of {cases.Length} ({rs2})", rules == cases.Length);

            int menuRed = 0;
            foreach (var n in names) if (ReferenceEquals(OzKit.CursorArt(n), n)) menuRed++;
            Check($"C5.1 edit mode (PLAY AS BLUE active: {OzPlayBlue.Active}): every state reads the red file: {menuRed} of {names.Length}", !OzPlayBlue.Active && menuRed == names.Length);
            string list = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/item34_blue_cursor/check/red_md5.txt"));
            if (File.Exists(list))
            {
                int same = 0, all = 0;
                foreach (var row in File.ReadAllLines(list))
                {
                    var p = row.Split('\t'); if (p.Length != 2) continue;
                    all++;
                    string f = Res + "Cursors/" + p[0];
                    if (File.Exists(f) && Md5(f) == p[1]) same++; else Fail("red file changed: " + p[0]);
                }
                Check($"C5.2 red pictures byte for byte as: {same} of {all}", all > 0 && same == all);
            }
            else lines.AppendLine("C5.2 skipped: " + list + " not found");

            string report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}" + (bad > 0 ? " | " + fails : "");
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item34_blue_cursor"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "checks.txt"), lines + report + "\n");
            }
            catch (IOException e) { Debug.LogWarning(Tag + " report file: " + e.Message); }
            if (bad == 0) Debug.Log(report); else Debug.LogError(report);
        }

        static IEnumerable<(string red, string blue)> Pairs(string n)
        {
            foreach (int s in new[] { 32, 48, 64 })
                for (int f = 0; f < 4; f++) yield return ($"Cursors/cur_{n}_{s}_{f}", $"Cursors/cur_{OzKit.BlueCursorArt}{n}_{s}_{f}");
            for (int f = 0; f < 4; f++) yield return ($"{OzSoftCursor.HdPrefix}{n}_{OzSoftCursor.HdSize}_{f}", $"{OzSoftCursor.HdPrefix}{OzKit.BlueCursorArt}{n}_{OzSoftCursor.HdSize}_{f}");
        }

        static string ImportText(TextureImporter t) => t == null ? "none" :
            $"{t.textureType}/{t.textureShape}/rw {t.isReadable}/mips {t.mipmapEnabled} {t.mipmapFilter}/{t.filterMode}/aniso {t.anisoLevel}/max {t.maxTextureSize}/{t.textureCompression}/srgb {t.sRGBTexture}/alpha {t.alphaIsTransparency} {t.alphaSource}/npot {t.npotScale}/wrap {t.wrapMode}";

        static Texture2D Png(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return ImageConversion.LoadImage(t, File.ReadAllBytes(path), false) ? t : null;
        }

        /// <summary>The red paint (tools/make_blue_cursors.py red_mask): not transparent, not near black, some colour, red hue.</summary>
        static bool IsRedPaint(Color32 c, float slack)
        {
            if (c.a == 0) return false;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return v >= 0.04f - slack && s >= 0.05f - slack && (h < 0.1f + slack || h > 0.9f - slack);
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        static string Md5(string path)
        {
            using (var m = MD5.Create()) { var h = m.ComputeHash(File.ReadAllBytes(path)); var sb = new StringBuilder(); foreach (var x in h) sb.Append(x.ToString("x2")); return sb.ToString(); }
        }

        static void Check(string what, bool pass) { lines.AppendLine((pass ? "ok   " : "FAIL ") + what); if (pass) ok++; else { bad++; fails.Append(what).Append(" | "); } }
        static void Fail(string what) { if (fails.Length < 2000) fails.Append(what).Append(" | "); }
    }
}
