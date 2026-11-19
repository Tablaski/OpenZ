#if UNITY_EDITOR && !OZ_RELEASE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kits cloud_shadows + cloud_shadows_visible: the Play check of the cloud shadows (View/OzCloudShadows.cs), on a level playing (or the
    /// Unit editor on a light floor: Desert dunes or Snow field; it has no clock, so NIGHT is skipped there), from the player's RTS camera as
    /// it stands (leave it at the default angle and zoom over open ground). Menu: OpenZ/V1.5/Cloud Shadows: Play Check (Editor/OzV15CloudShadowsMenu.cs);
    /// report and pictures in <see cref="Folder"/>. About 40 s; hands off the keyboard and the mouse. Everything it changes is put back at the end
    /// (the clock, the clock's run, the game speed, the clouds' switches), also when a row fails.
    ///  TABLE  the level's row, the picture loaded, the globals written.
    ///  NOON   the clock at noon (0.05), the game held (time scale 0) and the clouds held still: two frames with the clouds off (the noise floor
    ///         of the edge smoothing between two frames), one with them on. PASS (cloud_shadows_visible numbers): on 20 to 80 % of the screen
    ///         the picture got darker; the 95th percentile of that darkening (screen luma, sRGB) is 22 to 38 % (a cloud's core about 30 %; the
    ///         first numbers' 8 to 18 % passed and could not be seen); no more pixels got brighter than between the two "off" frames (+ 0.2 % of
    ///         the screen). Pictures: noon_clouds_off.png, noon_clouds_on.png, noon_change_x5.png (white = unchanged).
    ///  VISIBLE the same pair: the darkening map in 40 x 40 px blocks (each block's median: units, the HUD and edge noise drop out). PASS: the
    ///         blocks' std >= 0.075 and their 95th percentile >= 22 % (distinct dark patches, not an even dimming or a faint gradient), dark
    ///         blocks (>= 15 %) on 15 to 75 % of the screen, clear blocks (<= 3 %) on at least 15 %, at least one patch (4-connected dark
    ///         blocks, >= 3). The owner's frames of the first numbers score std 0.056 / p95 16 % (FAIL), the new numbers' prediction 0.088 /
    ///         27 % (kit cloud_shadows_visible tools/visible_metric.py: the same measure as a script). Picture: noon_blocks.png.
    ///  NIGHT  the clock at night (0.65): the strength written is 0 and a frame with the clouds on equals one with them off (within the noise
    ///         floor). SKIP when the game keeps it day (NIGHT &amp; RAIN off).
    ///  DRIFT  noon, the game running: 3 x 10 s; layer A's offset in the _OzCloudA global moves the picture its speed x the game's time
    ///         (within 10 %) along the fog of war's wind (within 5 deg); drift_0..3.png for the eye.
    ///  FOG    only when this match has the fog of war (MAYHEM): the pair fog_clouds_off.png / fog_clouds_on.png for the eye (informational).
    ///  GPU    6 blocks of 2 s, the clouds off and on in turn: the graphics chip's frame time (FrameTimingManager) per state. Informational in the
    ///         editor (its own drawing is in every frame); the proof is the player's frame rate bench with -oz-clouds off / on (README).
    /// </summary>
    public sealed class OzCloudShadowsCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ CloudShadows check]";
        public static string Folder = "";
        public static string Report = "not run";
        public static bool Running { get; private set; }

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OzCloudShadowsCheck") { hideFlags = HideFlags.DontSave }.AddComponent<OzCloudShadowsCheck>();
        }

        readonly StringBuilder rep = new StringBuilder();
        int fails;

        void Line(string row, bool? pass, string text)
        {
            string v = pass == null ? "INFO" : pass.Value ? "PASS" : "FAIL";
            if (pass == false) fails++;
            rep.AppendLine($"{row,-6} {v,-4} {text}");
        }

        Texture2D grabbed;
        IEnumerator Grab()
        {
            yield return new WaitForEndOfFrame();
            grabbed = ScreenCapture.CaptureScreenshotAsTexture();                // the screen as drawn (no blit: no double gamma)
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static float[] Luma(Texture2D t)
        {
            var px = t.GetPixels32(); var l = new float[px.Length];
            for (int i = 0; i < px.Length; i++) l[i] = (0.2126f * px[i].r + 0.7152f * px[i].g + 0.0722f * px[i].b) / 255f;
            return l;
        }

        struct Cmp { public float darker, brighter, p95, mean; public int n; }

        // ---------------- VISIBLE (cloud_shadows_visible): the darkening map in blocks; the same numbers as the kit's tools/visible_metric.py
        public const int VisBlock = 40;
        public const float VisStdMin = 0.075f, VisP95Min = 0.22f, VisDark = 0.15f, VisClear = 0.03f, VisDarkMin = 0.15f, VisDarkMax = 0.75f, VisClearMin = 0.15f;
        public const int VisPatchMinBlocks = 3;
        struct Vis { public float std, p95, dark, clear; public int patches, bx, by; public float[] k; }

        static Vis Visible(float[] off, float[] on, int w, int h)
        {
            var r = new Vis { bx = w / VisBlock, by = h / VisBlock };
            r.k = new float[r.bx * r.by];
            var cell = new float[VisBlock * VisBlock];
            for (int by = 0; by < r.by; by++)
                for (int bx = 0; bx < r.bx; bx++)
                {
                    int n = 0;
                    for (int y = 0; y < VisBlock; y++)
                    {
                        int row = (by * VisBlock + y) * w + bx * VisBlock;                               // GetPixels32: row 0 = the bottom (the order does not matter for a median)
                        for (int x = 0; x < VisBlock; x++) { int i = row + x; cell[n++] = Mathf.Clamp01((off[i] - on[i]) / Mathf.Max(off[i], 1f / 255f)); }
                    }
                    Array.Sort(cell); r.k[by * r.bx + bx] = 0.5f * (cell[n / 2 - 1] + cell[n / 2]);
                }
            int nb = r.k.Length; double s = 0, s2 = 0; int dark = 0, clear = 0;
            foreach (var v in r.k) { s += v; s2 += v * v; if (v >= VisDark) dark++; if (v <= VisClear) clear++; }
            double m = s / nb; r.std = (float)Math.Sqrt(Math.Max(0.0, s2 / nb - m * m)); r.dark = dark / (float)nb; r.clear = clear / (float)nb;
            var sorted = (float[])r.k.Clone(); Array.Sort(sorted);
            float pos = 0.95f * (nb - 1); int lo = (int)pos; r.p95 = sorted[lo] + (sorted[Math.Min(lo + 1, nb - 1)] - sorted[lo]) * (pos - lo);   // numpy's percentile (linear)
            var seen = new bool[nb]; var st = new Stack<int>();
            for (int i = 0; i < nb; i++)
            {
                if (seen[i] || r.k[i] < VisDark) continue;
                int count = 0; seen[i] = true; st.Push(i);
                while (st.Count > 0)
                {
                    int c = st.Pop(); count++; int cx = c % r.bx, cy = c / r.bx;
                    if (cx > 0) Visit(c - 1); if (cx < r.bx - 1) Visit(c + 1); if (cy > 0) Visit(c - r.bx); if (cy < r.by - 1) Visit(c + r.bx);
                }
                if (count >= VisPatchMinBlocks) r.patches++;
            }
            return r;
            void Visit(int j) { if (!seen[j] && r.k[j] >= VisDark) { seen[j] = true; st.Push(j); } }
        }

        void SaveBlocks(Vis v, string name)
        {
            const int S = 20; var t = new Texture2D(v.bx * S, v.by * S, TextureFormat.RGBA32, false); var px = new Color32[t.width * t.height];
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                {
                    byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - v.k[(y / S) * v.bx + x / S] / 0.4f) * 255f);     // white = unchanged, black = 40 % darker
                    px[y * t.width + x] = new Color32(g, g, g, 255);
                }
            t.SetPixels32(px); t.Apply(); Save(t, name); Destroy(t);
        }

        static Cmp Compare(float[] a, float[] b)
        {
            var c = new Cmp(); var d = new List<float>(a.Length / 4); int br = 0;
            for (int i = 0; i < a.Length; i++)
            {
                float x = a[i] - b[i];
                if (x > 3f / 255f) d.Add(x / Mathf.Max(a[i], 1f / 255f));
                else if (-x > 3f / 255f) br++;
            }
            c.n = a.Length; c.darker = d.Count / (float)a.Length; c.brighter = br / (float)a.Length;
            if (d.Count > 0)
            {
                d.Sort(); c.p95 = d[Mathf.Min(d.Count - 1, (int)(d.Count * 0.95f))];
                double s = 0; foreach (var v in d) s += v; c.mean = (float)(s / d.Count);
            }
            return c;
        }

        void Save(Texture2D t, string name) { if (t) File.WriteAllBytes(Path.Combine(Folder, name), t.EncodeToPNG()); }

        void SaveChange(Texture2D off, Texture2D on, string name)
        {
            var a = off.GetPixels32(); var b = on.GetPixels32(); var o = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                float la = (0.2126f * a[i].r + 0.7152f * a[i].g + 0.0722f * a[i].b) / 255f, lb = (0.2126f * b[i].r + 0.7152f * b[i].g + 0.0722f * b[i].b) / 255f;
                float d = Mathf.Clamp01(1f - 5f * Mathf.Max(0f, la - lb) / Mathf.Max(la, 1f / 255f));
                byte g = (byte)Mathf.RoundToInt(d * 255f);
                o[i] = lb > la + 3f / 255f ? new Color32(255, 0, 0, 255) : new Color32(g, g, g, 255);           // red = got brighter (should be only edge noise)
            }
            var t = new Texture2D(off.width, off.height, TextureFormat.RGBA32, false); t.SetPixels32(o); t.Apply();
            Save(t, name); Destroy(t);
        }

        IEnumerator Start()
        {
            Running = true;
            rep.Clear(); fails = 0;
            if (string.IsNullOrEmpty(Folder)) Folder = Path.Combine(Application.temporaryCachePath, "cloud_shadows_check");
            Directory.CreateDirectory(Folder);
            var g = ZGame.I; var cs = OzCloudShadows.I;
            var dn = FindFirstObjectByType<DayNight>();
            float master0 = OzCloudShadows.Master, ts0 = Time.timeScale, tod0 = dn ? dn.timeOfDay : 0f; bool freeze0 = OzCloudShadows.Freeze, run0 = dn && dn.running;
            var keep = new List<Texture2D>();
            rep.AppendLine($"{Tag} {DateTime.Now:yyyy-MM-dd HH:mm:ss}, level {(g ? g.level : "none")}, screen {Screen.width} x {Screen.height}, camera {(Camera.main ? Camera.main.transform.position.ToString("F1") + " pitch " + Camera.main.transform.eulerAngles.x.ToString("F1") : "none")}");
            try
            {
                // ---------------- TABLE
                bool ue = g && g.level == OzUnitEditor.LevelId;                                       // the Unit editor: no clock (fixed noon)
                bool ok = g && cs && (dn || ue) && cs.Picture;
                Line("TABLE", ok, ok ? $"{cs.Now.Level}: strength {cs.Now.Strength:0.00}, cut {cs.Now.Threshold:0.00} / {cs.Now.Softness:0.00}, tiles {cs.Now.TileA:0} + {cs.Now.TileB:0} m, drift {cs.Now.SpeedA:0.0} / {cs.Now.SpeedB:0.0} m/s; picture {cs.Picture.width} x {cs.Picture.height} {cs.Picture.format}; {(OzCloudShadows.ArgOff || cs.PrefOff ? "SWITCHED OFF (-oz-clouds off or the pref)" : "on")}"
                                     : $"needs a level playing with cloud shadows: ZGame {(g ? "yes" : "no")}, OzCloudShadows {(cs ? "yes" : "no (no row for this level?)")}, DayNight {(dn ? "yes" : ue ? "none (the Unit editor)" : "no")}, picture {(cs && cs.Picture ? "yes" : "no")}");
                if (!ok) yield break;

                // ---------------- NOON
                if (dn && !ue) { dn.running = false; dn.timeOfDay = 0.05f; }
                yield return new WaitForSecondsRealtime(0.5f);
                Time.timeScale = 0f; OzCloudShadows.Freeze = true;
                OzCloudShadows.Master = 0f; yield return Frames(4);
                yield return Grab(); var a1 = grabbed; keep.Add(a1);
                yield return Frames(1);
                yield return Grab(); var a2 = grabbed; keep.Add(a2);
                OzCloudShadows.Master = 1f; yield return Frames(4);
                yield return Grab(); var b1 = grabbed; keep.Add(b1);
                var v = Shader.GetGlobalVector("_OzCloudParams");
                var la1 = Luma(a1); var noise = Compare(la1, Luma(a2)); var on = Compare(la1, Luma(b1));
                Save(a1, "noon_clouds_off.png"); Save(b1, "noon_clouds_on.png"); SaveChange(a1, b1, "noon_change_x5.png");
                bool noonOk = on.darker >= 0.20f && on.darker <= 0.80f && on.p95 >= 0.22f && on.p95 <= 0.38f && on.brighter <= noise.brighter + noise.darker + 0.002f;
                Line("NOON", noonOk, $"strength written {v.x:0.000} (NightK {NightGrade.NightK:0.00}{(ue ? (OzUeBackdrop.LightFloor ? "; Unit editor, light floor" : "; Unit editor on a DARK floor: none by design, choose Desert dunes or Snow field") : "")}); darker on {on.darker * 100f:0.0} % of the screen (wanted 20 to 80), by {on.mean * 100f:0.0} % on average, 95th percentile {on.p95 * 100f:0.0} % (wanted 22 to 38; out of band: the row's strength x 30 / this %, once); "
                                     + $"brighter {on.brighter * 100f:0.00} % (noise floor off vs off: {noise.darker * 100f:0.00} % darker, {noise.brighter * 100f:0.00} % brighter)");

                // ---------------- VISIBLE (cloud_shadows_visible)
                var vis = Visible(la1, Luma(b1), a1.width, a1.height);
                bool visOk = vis.std >= VisStdMin && vis.p95 >= VisP95Min && vis.dark >= VisDarkMin && vis.dark <= VisDarkMax && vis.clear >= VisClearMin && vis.patches >= 1;
                SaveBlocks(vis, "noon_blocks.png");
                Line("VISIBLE", visOk, $"darkening map in {VisBlock} px blocks ({vis.bx} x {vis.by}): std {vis.std:0.000} (wanted >= {VisStdMin:0.000}), 95th percentile {vis.p95 * 100f:0.0} % (wanted >= {VisP95Min * 100f:0}); "
                                       + $"dark blocks {vis.dark * 100f:0.0} % (wanted {VisDarkMin * 100f:0} to {VisDarkMax * 100f:0}), clear blocks {vis.clear * 100f:0.0} % (wanted >= {VisClearMin * 100f:0}); {vis.patches} patch(es) (wanted >= 1)");

                // ---------------- NIGHT
                if (dn && !ue) dn.timeOfDay = 0.65f;
                yield return new WaitForSecondsRealtime(0.5f);
                if (!dn || ue) Line("NIGHT", null, "SKIP: the Unit editor has no clock (fixed noon)");
                else if (NightGrade.NightK < 0.9f) Line("NIGHT", null, $"SKIP: the clock stays day (NightK {NightGrade.NightK:0.00}; NIGHT & RAIN off in the options?)");
                else
                {
                    OzCloudShadows.Master = 0f; yield return Frames(4);
                    yield return Grab(); var n0 = grabbed; keep.Add(n0);
                    OzCloudShadows.Master = 1f; yield return Frames(4);
                    yield return Grab(); var n1 = grabbed; keep.Add(n1);
                    var nv = Shader.GetGlobalVector("_OzCloudParams");
                    var nc = Compare(Luma(n0), Luma(n1));
                    bool nightOk = nv.x < 0.002f && cs.StrengthNow < 0.002f && nc.darker <= noise.darker + noise.brighter + 0.002f;
                    Line("NIGHT", nightOk, $"NightK {NightGrade.NightK:0.00}: strength written {nv.x:0.0000} (wanted 0); off vs on: darker {nc.darker * 100f:0.00} %, brighter {nc.brighter * 100f:0.00} % (the noise floor)");
                    Save(n1, "night_clouds_on.png");
                }

                // ---------------- DRIFT
                if (dn && !ue) dn.timeOfDay = 0.05f;
                Time.timeScale = ts0 > 0f ? ts0 : 1f; OzCloudShadows.Freeze = false;
                yield return new WaitForSecondsRealtime(0.5f);
                bool driftOk = true; var sb = new StringBuilder();
                for (int i = 0; i <= 3; i++)
                {
                    if (i > 0)
                    {   // review fix: measured on the offsets the shaders really get (the _OzCloudA global), not on the component's own counter
                        float g0 = Time.time; Vector4 dA0 = Shader.GetGlobalVector("_OzCloudA");
                        yield return new WaitForSecondsRealtime(10f);
                        Vector4 dA1 = Shader.GetGlobalVector("_OzCloudA");
                        float du = dA1.z - dA0.z, dv = dA1.w - dA0.w;
                        du -= Mathf.Round(du); dv -= Mathf.Round(dv);                                     // the offsets wrap in 0..1 (24 m of 200 m: no ambiguity)
                        var moved = new Vector2(-du, -dv) * cs.Now.TileA;                                 // the picture moves against its offset
                        float want = cs.Now.SpeedA * (Time.time - g0), got = moved.magnitude;
                        float dirErr = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg, OzFogOfWar.WindDeg));
                        bool r = want > 0.5f && Mathf.Abs(got - want) <= 0.1f * want && dirErr <= 5f;
                        driftOk &= r; sb.Append($" {got:0.0} m at {dirErr:0.0} deg off the fog's wind in {Time.time - g0:0.0} s of game time (wanted {want:0.0} m, within 5 deg);");
                    }
                    yield return Grab(); Save(grabbed, $"drift_{i}.png"); Destroy(grabbed);
                }
                Line("DRIFT", driftOk, $"layer A, 3 x 10 s:{sb} offsets now A {cs.OffsetA.ToString("F3")}, B {cs.OffsetB.ToString("F3")}");

                // ---------------- FOG
                if (OzFogOfWar.Wanted)
                {
                    Time.timeScale = 0f; OzCloudShadows.Freeze = true;
                    OzCloudShadows.Master = 0f; yield return Frames(4); yield return Grab(); Save(grabbed, "fog_clouds_off.png"); Destroy(grabbed);
                    OzCloudShadows.Master = 1f; yield return Frames(4); yield return Grab(); Save(grabbed, "fog_clouds_on.png"); Destroy(grabbed);
                    Line("FOG", null, $"this match has the fog of war (on screen {OzFogOfWar.Strength:0.00}): fog_clouds_off.png / fog_clouds_on.png for the eye (the sheets the same, the shadows on the open ground beside them)");
                    Time.timeScale = ts0 > 0f ? ts0 : 1f; OzCloudShadows.Freeze = false;
                }
                else Line("FOG", null, "SKIP: no fog of war in this match (" + OzFogOfWar.WhyOffNow + "); run it once more on a MAYHEM match");

                // ---------------- GPU
                var ft = new FrameTiming[1]; var off = new List<double>(); var onT = new List<double>();
                for (int blk = 0; blk < 6; blk++)
                {
                    bool st = (blk & 1) == 1;
                    OzCloudShadows.Master = st ? 1f : 0f;
                    yield return new WaitForSecondsRealtime(0.5f);
                    float end = Time.realtimeSinceStartup + 2f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, ft) > 0 && ft[0].gpuFrameTime > 0.0) (st ? onT : off).Add(ft[0].gpuFrameTime);
                        yield return null;
                    }
                }
                OzCloudShadows.Master = 1f;
                if (off.Count < 20 || onT.Count < 20) Line("GPU", null, $"no graphics chip timings in this editor ({off.Count} / {onT.Count} frames); use the player's bench (README)");
                else
                {
                    double mo = 0, mn = 0; foreach (var x in off) mo += x; foreach (var x in onT) mn += x; mo /= off.Count; mn /= onT.Count;
                    Line("GPU", null, $"graphics chip frame time, clouds off {mo:0.000} ms ({off.Count} frames), on {mn:0.000} ms ({onT.Count} frames): {mn - mo:+0.000;-0.000} ms (editor: indicative only)");
                }
            }
            finally
            {
                OzCloudShadows.Master = master0; OzCloudShadows.Freeze = freeze0; Time.timeScale = ts0;
                if (dn) { dn.timeOfDay = tod0; dn.running = run0; }
                foreach (var t in keep) if (t) Destroy(t);
                rep.AppendLine(fails == 0 ? "RESULT: no FAIL" : $"RESULT: {fails} FAIL");
                rep.AppendLine("put back: the clock, its run, the game speed, the clouds' switches");
                Report = rep.ToString();
                try { File.WriteAllText(Path.Combine(Folder, "report.txt"), Report); } catch (Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
                Debug.Log(Tag + "\n" + Report + "(" + Folder + ")");
                Running = false;
                Destroy(gameObject);
            }
        }
    }
}
#endif
