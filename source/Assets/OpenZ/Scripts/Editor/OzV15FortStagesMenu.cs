#if !OPENZ_HARNESS
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit fort_damage_stronger: the owner's stage preview before the exe (Play mode, any level with forts). "Show enemy fort at N %" frames
    /// the enemy fort with the normal RTS camera (60 m up, the camera's own pitch) and sets its health to N % (the sim's SetHealth: a test tool,
    /// never in a game): the view eases into that stage, parts fall off on the way. The whole series with one Game view frame per stage is the
    /// fort_damage kit's "Preview stages" item (100 / 75 / 50 / 25 / 5 %, frames in &lt;project&gt;/Captures/FortDamage/); "Stage sheet" puts the
    /// newest frame of each stage side by side in one picture (Captures/FortDamage/STAGES_SHEET_&lt;fort&gt;.png) for the owner.
    /// </summary>
    public static class OzV15FortStagesMenu
    {
        const string Root = "OpenZ/V1.5/Fort Damage/";
        static readonly int[] Pcts = { 100, 75, 50, 25, 5 };

        [MenuItem(Root + "Show enemy fort at 100 % (Play)", false, 200)] static void S100() => Show(100);
        [MenuItem(Root + "Show enemy fort at 75 % (Play)", false, 201)] static void S75() => Show(75);
        [MenuItem(Root + "Show enemy fort at 50 % (Play)", false, 202)] static void S50() => Show(50);
        [MenuItem(Root + "Show enemy fort at 25 % (Play)", false, 203)] static void S25() => Show(25);
        [MenuItem(Root + "Show enemy fort at 5 % (Play)", false, 204)] static void S5() => Show(5);

        static void Show(int pct)
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || !OzFortDamage.I) { Debug.LogWarning("[OpenZ V1.5] fort stages: enter Play mode on a level first"); return; }
            var f = g.World.Objects.Where(o => o.IsFort && !o.Destroyed && o.Owner != g.player && o.Owner != Team.Neutral).OrderBy(o => o.Id).FirstOrDefault();
            if (f == null) { Debug.LogWarning("[OpenZ V1.5] fort stages: no enemy fort standing"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (cam) { var c = g.CenterWorld(f); cam.focus = new Vector3(c.x, 0f, c.z); cam.height = 60f; }
            f.SetHealth(Mathf.Max(1, Mathf.RoundToInt(f.MaxHealth * pct / 100f)));
            Debug.Log($"[OpenZ V1.5] fort stages: {f.Type} #{f.Id} set to {pct} % ({f.Health}/{f.MaxHealth}); it eases into the stage in about a second\n{OzFortDamage.Report()}");
        }

        [MenuItem(Root + "Stage sheet from the newest preview frames", false, 210)]
        static void Sheet()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "FortDamage"));
            if (!Directory.Exists(dir)) { Debug.LogWarning("[OpenZ V1.5] fort stages: no frames yet (run 'Preview stages: enemy fort' first): " + dir); return; }
            var files = Directory.GetFiles(dir, "fort_*pct.png");
            var forts = files.Select(p => Path.GetFileName(p)).Select(n => n.Substring(0, n.LastIndexOf('_'))).Distinct().ToList();
            int made = 0;
            foreach (var fort in forts)
            {
                var pics = Pcts.Select(p => files.Where(x => Path.GetFileName(x) == $"{fort}_{p:000}pct.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()).ToList();
                if (pics.Any(p => p == null)) continue;
                var tex = pics.Select(p => { var t = new Texture2D(2, 2, TextureFormat.RGB24, false); t.LoadImage(File.ReadAllBytes(p)); return t; }).ToList();
                int w = 800, h = Mathf.RoundToInt(800f * tex[0].height / tex[0].width), gap = 8;
                var sheet = new Texture2D(w * tex.Count + gap * (tex.Count - 1), h, TextureFormat.RGB24, false);
                var fill = new Color32[sheet.width * sheet.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(16, 16, 16, 255);
                sheet.SetPixels32(fill);
                for (int i = 0; i < tex.Count; i++)
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            sheet.SetPixel(i * (w + gap) + x, y, tex[i].GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h));
                sheet.Apply();
                var outPath = Path.Combine(dir, $"STAGES_SHEET_{fort}.png");
                File.WriteAllBytes(outPath, sheet.EncodeToPNG());
                foreach (var t in tex) Object.DestroyImmediate(t);
                Object.DestroyImmediate(sheet);
                Debug.Log($"[OpenZ V1.5] fort stages: sheet 100 / 75 / 50 / 25 / 5 % (left to right): {outPath}");
                made++;
            }
            if (made == 0) Debug.LogWarning("[OpenZ V1.5] fort stages: no fort has all five stage frames in " + dir);
        }
    }
}
#endif
