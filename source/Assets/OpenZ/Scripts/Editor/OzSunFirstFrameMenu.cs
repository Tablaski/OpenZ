#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// counted check (edit mode) and the Play proofs (View/OzSunFirstFrameProof.cs).
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    public static class OzSunFirstFrameMenu
    {
        const string Root = "OpenZ/V1.2/Sun First Frame: 78 ";
        static readonly string[] Levels = { "L02", "L07", "L16", "L17" };

        // ------------------------------------------------------------------------------------------------ the counted check
        [MenuItem(Root + "1 Counted Check (no Play)")]
        static void Counted()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Check(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); }
            bool was = FlybySky.SunFirstFrame;
            try
            {
                Check("the sun from the first frame is on by default (FlybySky.SunFirstFrame)", was);
                FlybySky.SunFirstFrame = true;
                foreach (var L in Levels)
                {
                    var e = FlybySky.Entry(L);
                    Check(L + ": its level-start flyover preloads the panorama with the level (" + (e != null ? e.textureHi : "NO sky entry") + ")", FlybySky.PreloadWanted(L, true));
                    Check(L + ": a start without a level-start flyover (a bot match, a test start, the briefing off) loads nothing", !FlybySky.PreloadWanted(L, false));
                }
                Check("a level without a sky entry loads nothing (L03, an empty name, no name, the playground)",
                      !FlybySky.PreloadWanted("L03", true) && !FlybySky.PreloadWanted("", true) && !FlybySky.PreloadWanted(null, true) && !FlybySky.PreloadWanted("UE", true));
                FlybySky.SunFirstFrame = false;
                Check("the earlier way (the A/B measure's switch off) preloads nothing", !FlybySky.PreloadWanted("L16", true));
            }
            finally { FlybySky.SunFirstFrame = was; }

            foreach (var L in Levels)
            {   // the map the preload reads: present, on the import rule (8192 x 4096 BC7 on Standalone, no mips) = 32 MiB, the size the log states
                var e = FlybySky.Entry(L);
                string p = e != null ? "Assets/OpenZ/Resources/" + e.textureHi + ".png" : "-";
                var ti = e != null ? AssetImporter.GetAtPath(p) as TextureImporter : null;
                int w = 0, h = 0; TextureImporterPlatformSettings ps = null;
                if (ti) { ti.GetSourceTextureWidthAndHeight(out w, out h); ps = ti.GetPlatformTextureSettings("Standalone"); }
                bool rule = ti && !ti.mipmapEnabled && ps != null && ps.overridden && ps.format == TextureImporterFormat.BC7 && ps.maxTextureSize >= Mathf.Max(w, h);
                float mb = rule ? FlybySky.EstimateMB(w, h, TextureFormat.BC7) : 0f;
                Check(L + ": the panorama " + p + " is there, " + w + " x " + h + (rule ? " BC7 without mips = " + mb.ToString("0.0") + " MB resident while the flyover needs it" : " NOT on the import rule (BC7, no mips)"),
                      rule && Mathf.Abs(mb - 32f) < 0.01f);
            }
            Check("a preload no flyover takes is held " + FlybySky.PreloadHoldSeconds.ToString("0") + " s at most (3-30 s), then released by the usual rule 2 s later",
                  FlybySky.PreloadHoldSeconds >= 3f && FlybySky.PreloadHoldSeconds <= 30f);
            Check("the release after the flyover is as before: the fade out " + FlybySky.FadeSeconds.ToString("0.00") + " s at the last shot, " + FlybySky.LateFadeSeconds.ToString("0.00") + " s after the hand-back, unloaded 2 s after it",
                  Mathf.Approximately(FlybySky.FadeSeconds, 1f) && Mathf.Approximately(FlybySky.LateFadeSeconds, 0.25f));
            Check("the size rule the log uses: 8192 x 4096 BC7 = 32.0 MB", Mathf.Abs(FlybySky.EstimateMB(8192, 4096, TextureFormat.BC7) - 32f) < 0.01f);

            string head = "[OpenZ V1.2] Sun First Frame 78 counted check: " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + "/" + (pass + fail);
            Debug.Log(head + "\n" + sb);
            try
            {
                string dir = OzSunFirstFrameProof.Folder();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "counted_check.txt"), head + "\n" + sb);
            }
            catch (System.Exception) { }
        }

        // ------------------------------------------------------------------------------------------------ Play
        static void Go(OzSunFirstFrameProof.Job job, string level)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] Sun First Frame 78: start the MainMenu scene in Play first"); return; }
            OzSunFirstFrameProof.Run(job, level);
            Debug.Log("[OpenZ] Sun First Frame 78 " + job + " " + level + " started; its report: OzSunFirstFrameProof.Report, the pictures in " + OzSunFirstFrameProof.Folder());
        }

        [MenuItem(Root + "2 Play Proof RED L16 (main menu, Play)")] static void RedL16() => Go(OzSunFirstFrameProof.Job.Red, "L16");
        [MenuItem(Root + "3 Play Proof BLUE L16 (main menu, Play)")] static void BlueL16() => Go(OzSunFirstFrameProof.Job.Blue, "L16");
        [MenuItem(Root + "3 Play Proof BLUE L02 (main menu, Play)")] static void BlueL02() => Go(OzSunFirstFrameProof.Job.Blue, "L02");
        [MenuItem(Root + "3 Play Proof BLUE L17 (main menu, Play)")] static void BlueL17() => Go(OzSunFirstFrameProof.Job.Blue, "L17");
        [MenuItem(Root + "4 Status (Play)")] static void Status() => Debug.Log("[OpenZ FlybySky] " + FlybySky.PreloadStatus() + " | " + FlybySky.Status());
    }
}
#endif
