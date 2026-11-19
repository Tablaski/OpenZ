// OpenZ (GPL-3.0) - 1.5 kit issue8_slow_chip_picture: the counted check of its rules, without Play (Editor only; no saved setting, no
// asset and no scene is changed: the settings side runs on a table, the hull side on made-up point sets and a hidden camera that is
// destroyed again).
// Menu: OpenZ/Checks/V1.5/Issue 8 slow chip picture (in Play it also starts the hover replay, View/OzHoverReplayCheck.cs), or
// OpenZ.EditorTools.OzV15Issue8PictureCheck.Run(). One log line "[OpenZ issue 8 picture] PASS n/n ..." (Report); every row follows.
//  R1  the row: label 3D PICTURE, saved as oz_lite_picture, SHARP / SOFT / SOFTEST, its tip registered, on the GRAPHICS tab right
//      after WRECKS AND DEBRIS, plain words
//  R2  budgets -> render scales and drawn sizes for 1280x720, 1920x1080, 2560x1440, 3840x2160 on every step (the table is logged)
//  R3  SHARP is LOW as before, bit for bit: For(w, h, SHARP) == For(w, h) on 25 000+ screen sizes, the LOW scale with the preset's own
//      scale 1.0 and 0.7; HIGH and MEDIUM never touched on any step, nor LOW when its asset is missing
//  R4  the upscale filter: the LOW asset's own filter (read from Resources), kept on SHARP, bilinear on SOFT / SOFTEST; the screen
//      sizes where the asset's own "Automatic" filter would pick nearest-neighbour (logged)
//  R5  the first start on a slow chip picks 3D PICTURE SOFTEST with LOW, the four lightest rows and FRAME RATE LIMIT 30
//  R6  back to SHARP by itself above LOW (MEDIUM / HIGH) unless the player set it himself; on LOW nothing; no row bit = never touched
//  R7  Mac: the row is not built in the Mac player (OzMacPerf owns the scale there)
//  R8  a fast chip: nothing chosen, nothing saved, so the row reads SHARP and LOW is as before (R3)
//  R9  the silhouette short pass on made-up point sets: the same box bit for bit as all points at 400 camera poses a set; flat or
//      tiny sets are refused (all points kept)
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    public static class OzV15Issue8PictureCheck
    {
        const string Tag = "[OpenZ issue 8 picture]";
        public static string Report = "not run";

        sealed class Table : OzPrefsMirror.IStore
        {
            public readonly Dictionary<string, object> d = new Dictionary<string, object>();
            public bool Has(string key) => d.ContainsKey(key);
            public int GetInt(string key, int fallback) => d.TryGetValue(key, out var v) && v is int i ? i : fallback;
            public float GetFloat(string key, float fallback) => d.TryGetValue(key, out var v) && v is float f ? f : fallback;
            public string GetString(string key, string fallback) => d.TryGetValue(key, out var v) && v is string s ? s : fallback;
            public void SetInt(string key, int v) => d[key] = v;
            public void SetFloat(string key, float v) => d[key] = v;
            public void SetString(string key, string v) => d[key] = v;
        }

        [MenuItem("OpenZ/Checks/V1.5/Issue 8 slow chip picture")]
        public static void Menu()
        {
            Run();
            if (EditorApplication.isPlaying) OzHoverReplayCheck.Run();
            else Debug.Log(Tag + " the hover replay needs Play: enter Play (the main menu) and run this menu again, or OpenZ/Checks/V1.5/Issue 8 hover replay (Play)");
        }

        [MenuItem("OpenZ/Checks/V1.5/Issue 8 hover replay (Play)")]
        static void Replay()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode first (the main menu or a level)"); return; }
            OzHoverReplayCheck.Run();
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-4f;

        public static void Run()
        {
            int ok = 0, n = 0; var rows = new StringBuilder(); var failed = new List<string>();
            void Check(bool c, string what) { n++; if (c) ok++; else failed.Add(what.Split(' ')[0]); rows.AppendLine((c ? "PASS " : "FAIL ") + what); }

            // ---- R1 the row
            int at = OzSettingTips.TabOrder(OzLowPicture.Label), wr = OzSettingTips.TabOrder(OzLightRows.WrecksLabel);
            string tip = OzSettingTips.For(OzLowPicture.Label) ?? "";
            bool plain = tip.StartsWith("3D PICTURE|") && !tip.Contains("Alt") && !tip.Contains("render") && !tip.Contains("scale") && !tip.Contains("pixel budget") && !tip.Contains("FSR");
            Check(OzLowPicture.Label == "3D PICTURE" && OzLowPicture.Pref == "oz_lite_picture" && string.Join("/", OzLowPicture.Names) == "SHARP/SOFT/SOFTEST" && tip == OzLowPicture.Tip
                  && OzSettingTips.TabOf(OzLowPicture.Label) == OzSettingTips.TabGraphics && at == wr + 1 && plain,
                  $"R1 the row: {OzLowPicture.Label} ({OzLowPicture.Pref}) {string.Join(" / ", OzLowPicture.Names)}, GRAPHICS tab place {at} (WRECKS AND DEBRIS {wr}), tip: {tip}");

            // ---- R2 budgets -> scales
            var sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) };
            var want = new float[,] { { 1f, 0.75f, 0.5625f, 0.375f }, { 0.8f, 1024f / 1920f, 0.4f, 1024f / 3840f }, { 0.625f, 800f / 1920f, 0.3125f, 800f / 3840f } };
            bool r2 = true; var table = new StringBuilder();
            for (int s = 0; s < 3; s++)
            {
                table.Append($"     {OzLowPicture.Names[s],-8} at most {OzLowPicture.BudgetName(s),-9}:");
                for (int k = 0; k < sizes.Length; k++)
                {
                    float sc = OzLowScale.For(sizes[k].x, sizes[k].y, s); r2 &= Near(sc, want[s, k]);
                    table.Append($"  {sizes[k].x}x{sizes[k].y} -> {sc:0.####} ({Mathf.RoundToInt(sizes[k].x * sc)}x{Mathf.RoundToInt(sizes[k].y * sc)})");
                }
                table.AppendLine();
            }
            Check(r2, "R2 budgets -> render scales (drawn size), the interface always at the screen's size:\n" + table.ToString().TrimEnd());

            // ---- R3 SHARP = LOW as before, bit for bit; HIGH / MEDIUM never touched
            int sizesTried = 0, same = 0, never = 0, neverTried = 0;
            var hs = new List<int> { 240, 360, 450, 480, 540, 576, 600, 720, 768, 800, 810, 864, 900, 960, 1012, 1050, 1080, 1152, 1200, 1350, 1440, 1600, 1800, 2160, 2880, 4320 };
            for (int w = 160; w <= 7680; w += 8)
            {
                hs.Add(w * 9 / 16);
                foreach (int h in hs)
                {
                    sizesTried++;
                    float a = OzLowScale.For(w, h), b = OzLowScale.For(w, h, OzLowPicture.Sharp);
                    float l1 = OzLowScale.ScaleFor(2, true, 1f, w, h, OzLowPicture.Sharp), l7 = OzLowScale.ScaleFor(2, true, 0.7f, w, h, OzLowPicture.Sharp);
                    if (a.Equals(b) && l1.Equals(Mathf.Min(1f, a)) && l7.Equals(Mathf.Min(0.7f, a))) same++;
                    for (int st = 0; st < 3; st++)
                    {
                        neverTried += 3;
                        if (OzLowScale.ScaleFor(0, true, 1f, w, h, st) < 0f) never++;
                        if (OzLowScale.ScaleFor(1, true, 1f, w, h, st) < 0f) never++;
                        if (OzLowScale.ScaleFor(2, false, 1f, w, h, st) < 0f) never++;
                    }
                }
                hs.RemoveAt(hs.Count - 1);
            }
            Check(same == sizesTried && never == neverTried, $"R3 SHARP is LOW as before, bit for bit: {same} of {sizesTried} screen sizes (preset scale 1.0 and 0.7); HIGH, MEDIUM and LOW without its asset left alone {never} of {neverTried}");

            // ---- R4 the upscale filter
            var lowAsset = Resources.Load<UniversalRenderPipelineAsset>(OzQuality.ResDir + OzQuality.LowAsset);
            var own = lowAsset ? lowAsset.upscalingFilter : UpscalingFilterSelection.Auto;
            var autoPoint = new List<string>();
            foreach (var sz in new[] { new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2048, 1152), new Vector2Int(2400, 1350), new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160) })
                for (int st = 1; st < 3; st++)
                {
                    float sc = OzLowScale.For(sz.x, sz.y, st); if (sc >= 1f) continue;
                    float ps = 1f / sc; bool integer = Mathf.Approximately(ps - Mathf.Floor(ps), 0f);   // URP's Automatic rule (UniversalRenderPipeline.ResolveUpscalingFilterSelection)
                    if (integer && Mathf.Approximately(sz.x / ps - Mathf.Floor(sz.x / ps), 0f) && Mathf.Approximately(sz.y / ps - Mathf.Floor(sz.y / ps), 0f)) autoPoint.Add($"{OzLowPicture.Names[st]} at {sz.x}x{sz.y} (scale {sc:0.###})");
                }
            Check(lowAsset && OzLowScale.FilterFor(OzLowPicture.Sharp, own) == own && OzLowScale.FilterFor(OzLowPicture.Soft, own) == UpscalingFilterSelection.Linear && OzLowScale.FilterFor(OzLowPicture.Softest, own) == UpscalingFilterSelection.Linear,
                  $"R4 the upscale filter: the LOW asset ({(lowAsset ? lowAsset.name : "missing")}) has {own}; SHARP keeps it, SOFT and SOFTEST use Linear (bilinear); its own Automatic filter would pick nearest-neighbour (hard square pixels) at: {(autoPoint.Count > 0 ? string.Join(", ", autoPoint) : "none of the sizes tried")}");

            // ---- R5 the first start
            var keys = OzSlowChipStart.Keys; var pick = OzSlowChipStart.Pick; int pi = System.Array.IndexOf(keys, OzLowPicture.Pref);
            Check(keys.Length == pick.Length && pi > 0 && pick[pi] == OzLowPicture.Softest && pick[0] == 2 && System.Array.IndexOf(OzSlowChipStart.RowKeys, OzLowPicture.Pref) == pi - 1
                  && OzSlowChipStart.Chosen.Contains("3D PICTURE SOFTEST") && OzSlowChipStart.Decide(0, true, false, false) == OzSlowChipStart.Step.Pick,
                  $"R5 the first start on a slow chip picks: {string.Join(", ", keys)} = {string.Join(", ", pick)} ({OzSlowChipStart.Chosen})");

            // ---- R6 back to SHARP above LOW unless the player set it
            Table T(int quality, int bits, int picture)
            {
                var x = new Table();
                x.SetInt(OzQuality.Pref, quality); x.SetInt(OzFpsCap.CapPref, 30); x.SetInt(OzSlowChipStart.MarkKey, 1);
                x.SetInt(OzLightRows.ShadowsPref, 1); x.SetInt(OzLightRows.AoPref, 1); x.SetInt(OzLightRows.TreesPref, 2); x.SetInt(OzLightRows.WrecksPref, 1); x.SetInt(OzLowPicture.Pref, picture);
                if (bits >= 0) x.SetInt(OzSlowChipStart.RowsKey, bits);
                return x;
            }
            int all = OzSlowChipStart.AllRows, pb = 1 << (pi - 1);
            var low = T(2, all, 2); int bLow = OzSlowChipStart.Settle(low, out int lLow);
            var med = T(1, all, 2); int bMed = OzSlowChipStart.Settle(med, out int lMed);
            var soft = T(0, all, 1); int bSoft = OzSlowChipStart.Settle(soft, out int lSoft);
            var mine = T(0, all & ~pb, 2); int bMine = OzSlowChipStart.Settle(mine, out _);
            var none = T(1, -1, 2); none.d.Remove(OzSlowChipStart.MarkKey); int nNone = none.d.Count; int bNone = OzSlowChipStart.Settle(none, out _);
            string line = OzSlowChipStart.BackLine(1, pb);
            Check(all == 31 && bLow == 0 && lLow == all && low.GetInt(OzLowPicture.Pref, -1) == 2
                  && bMed == all && lMed == 0 && med.GetInt(OzLowPicture.Pref, -1) == 0 && med.GetInt(OzQuality.Pref, -1) == 1
                  && (bSoft & pb) == 0 && soft.GetInt(OzLowPicture.Pref, -1) == 1 && lSoft == 0
                  && (bMine & pb) == 0 && mine.GetInt(OzLowPicture.Pref, -1) == 2
                  && bNone == 0 && none.GetInt(OzLowPicture.Pref, -1) == 2 && none.d.Count == nNone && line.Contains("3D PICTURE SHARP"),
                  $"R6 back to SHARP: LOW keeps SOFTEST (back {bLow}); MEDIUM puts it back (back {bMed}, 3D PICTURE {med.GetInt(OzLowPicture.Pref, -1)}); HIGH with the player's SOFT keeps SOFT ({soft.GetInt(OzLowPicture.Pref, -1)}); "
                  + $"the player's own SOFTEST (no bit) kept ({mine.GetInt(OzLowPicture.Pref, -1)}); no row bit at all: nothing touched ({bNone}); the log line: {line}");

            // ---- R7 Mac / R8 a fast chip
            Check(!OzLowPicture.Shown(RuntimePlatform.OSXPlayer) && OzLowPicture.Shown(RuntimePlatform.WindowsPlayer) && OzLowPicture.Shown(RuntimePlatform.LinuxPlayer) && OzLowPicture.Shown(RuntimePlatform.WindowsEditor),
                  "R7 Mac: the row is not built in the Mac player (OzMacPerf owns the render scale there, OzLowScale stands aside); Windows and Linux players: built");
            string chip = SystemInfo.graphicsDeviceName;
            bool fast = !OzSlowChipStart.IsSlowChip("NVIDIA GeForce RTX 5070 Laptop GPU") && OzSlowChipStart.Decide(0, false, false, false) == OzSlowChipStart.Step.Nothing && OzSlowChipStart.Decide(0, false, true, false) == OzSlowChipStart.Step.Nothing;
            var fresh = new Table(); int bFresh = OzSlowChipStart.Settle(fresh, out _);
            Check(fast && bFresh == 0 && fresh.d.Count == 0, $"R8 a fast chip: nothing is chosen or saved (no row bit, the table stays empty), so 3D PICTURE reads SHARP and LOW is as before (R3); this PC: {chip} -> slow {OzSlowChipStart.IsSlowChip(chip)}");

            // ---- R9 the silhouette short pass on made-up point sets
            Check(Hull(out string hr), "R9 the silhouette short pass: " + hr);

            Report = $"{Tag} {(failed.Count == 0 ? "PASS" : "FAIL")} {ok}/{n} checks{(failed.Count > 0 ? " (failed: " + string.Join(" ", failed) + ")" : "")}";
            if (failed.Count == 0) Debug.Log(Report + "\n" + rows); else Debug.LogError(Report + "\n" + rows);
        }

        /// <summary>R9: made-up building-like point sets; at 400 seeded camera poses each the box from the kept points must equal the box from
        /// every point, bit for bit, whenever the short pass says it is proven.</summary>
        static bool Hull(out string what)
        {
            var rng = new System.Random(8812);
            var sets = new List<(string name, Vector3[] pts, bool refuse)>();
            // a factory-like block: 24 x 24 cells, highest and lowest point each (roof steps, a tower, the floor)
            for (int v = 0; v < 2; v++)
            {   // v 0: flat floor and roofs (as a model's own vertices are); v 1: everything a few millimetres uneven
                var a = new List<Vector3>(); float noise = v == 0 ? 0f : 0.004f;
                for (int i = 0; i < 24; i++) for (int j = 0; j < 24; j++)
                {
                    float x = -10f + i * 20f / 23f, z = -8f + j * 16f / 23f;
                    float top = (i > 16 && j > 16) ? 14f : (i < 6 ? 6f : 9f);
                    a.Add(new Vector3(x, top + (float)rng.NextDouble() * noise, z)); a.Add(new Vector3(x, (float)rng.NextDouble() * noise, z));
                }
                sets.Add((v == 0 ? "factory block" : "uneven factory block", a.ToArray(), false));
            }
            var b = new List<Vector3>(); for (int i = 0; i < 900; i++) b.Add(new Vector3((float)(rng.NextDouble() * 2 - 1) * 12f, (float)rng.NextDouble() * 10f, (float)(rng.NextDouble() * 2 - 1) * 12f));
            sets.Add(("random cloud", b.ToArray(), false));
            var c = new List<Vector3>(); for (int i = 0; i < 600; i++) { var v = Random(rng).normalized; c.Add(v * 8f + Vector3.up * 8f); }
            sets.Add(("round tower (every point on the hull)", c.ToArray(), false));
            var d = new List<Vector3>(); for (int i = 0; i < 300; i++) d.Add(new Vector3((float)rng.NextDouble() * 10f, 2f, (float)rng.NextDouble() * 10f));
            sets.Add(("flat set", d.ToArray(), true));
            sets.Add(("ten points", b.GetRange(0, 10).ToArray(), true));

            var go = new GameObject("OzIssue8HullCheckCam") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(1920, 1080, 0) { hideFlags = HideFlags.HideAndDontSave };
            var sb = new StringBuilder(); bool good = true; int poses = 0, proven = 0, sameBox = 0; long projectedSum = 0, allSum = 0;
            try
            {
                var cam = go.AddComponent<Camera>(); cam.enabled = false; cam.targetTexture = rt; cam.fieldOfView = 40f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 3000f;
                sb.Append($"camera {cam.pixelWidth}x{cam.pixelHeight}; ");
                if (cam.pixelWidth < 64) { what = sb + "the hidden camera has no picture size"; return false; }
                foreach (var s in sets)
                {
                    var pk = OzSilhouetteHull.Reduce(s.pts, 1f); string why = pk.why;
                    if (s.refuse) { good &= pk.keep == null; sb.Append($"{s.name}: {(pk.keep == null ? "refused (" + why + ")" : "NOT refused")}; "); continue; }
                    if (pk.keep == null) { good = false; sb.Append($"{s.name}: refused ({why}); "); continue; }
                    int p0 = proven, s0 = sameBox;
                    for (int k = 0; k < 400; k++)
                    {
                        float scale = new[] { 1f, 0.5f, 2.5f, 0.01f * 100f }[k % 4];
                        var l2w = Matrix4x4.TRS(new Vector3((float)rng.NextDouble() * 200f, (float)rng.NextDouble() * 5f, -(float)rng.NextDouble() * 200f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(scale, scale * (0.8f + 0.4f * (float)rng.NextDouble()), scale));
                        var target = l2w.MultiplyPoint3x4(Vector3.zero);
                        float h = Mathf.Lerp(25f, 220f, (float)rng.NextDouble()), tilt = Mathf.Lerp(22f, 89f, (float)rng.NextDouble()), yaw = (float)rng.NextDouble() * 360f, back = h / Mathf.Tan(tilt * Mathf.Deg2Rad);
                        var off = new Vector3((float)(rng.NextDouble() * 2 - 1) * 120f, 0f, (float)(rng.NextDouble() * 2 - 1) * 120f);
                        cam.transform.SetPositionAndRotation(target + off + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, h, -back), Quaternion.Euler(tilt, yaw, 0f));
                        poses++;
                        Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
                        foreach (var q in s.pts) { var sp = cam.WorldToScreenPoint(l2w.MultiplyPoint3x4(q)); if (sp.z < 0) continue; mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp); }
                        if (!OzSilhouetteHull.Short(pk, cam, l2w, out var kmn, out var kmx, out int projected)) continue;
                        projectedSum += projected; allSum += s.pts.Length;
                        proven++;
                        if (kmn.x.Equals(mn.x) && kmn.y.Equals(mn.y) && kmx.x.Equals(mx.x) && kmx.y.Equals(mx.y)) sameBox++;
                        else if (sb.Length < 3000) sb.Append($"[{s.name} pose {k}: all {mn.x:R},{mn.y:R},{mx.x:R},{mx.y:R} / kept {kmn.x:R},{kmn.y:R},{kmx.x:R},{kmx.y:R}] ");
                    }
                    sb.Append($"{s.name}: {why}; proven at {proven - p0} of 400 poses, the same box {sameBox - s0}; ");
                }
            }
            finally { Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt); }
            good &= proven > 0 && sameBox == proven;
            what = sb.ToString().TrimEnd(' ', ';') + $" | total {sameBox} of {proven} proven boxes the same ({poses} poses); points projected on the proven poses {projectedSum} of {allSum} ({(allSum > 0 ? 100.0 * projectedSum / allSum : 0):0.0} %)";
            return good;
        }

        static Vector3 Random(System.Random r) { Vector3 v; do { v = new Vector3((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1)); } while (v.sqrMagnitude < 0.01f || v.sqrMagnitude > 1f); return v; }
    }
}
