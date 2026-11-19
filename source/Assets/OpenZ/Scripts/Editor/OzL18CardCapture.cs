// OpenZ (GPL-3.0) - version 1.5, level 18: the level card's preview clips and card art from the blue briefing's flights. Editor only, never in a build.
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.5/L18 Briefings/6 Card capture. Start it in Play together with the frame-locked blue recorder (1 Record Blue; set
    /// OzBlueCutsceneProof.Fps to 60 first): while the blue briefing's stage runs, every captured frame the game camera (the flight's own pose,
    /// untouched) is rendered into a 2560 x 1440 texture and written as a JPG, and every <see cref="StillEvery"/> frames into a 2560 x 1600
    /// PNG (the 16:10 picture keeps the 16:9 frame's width). A camera render into a texture draws the world with every post effect of that
    /// (frames/, stills/, capture.csv). Nothing in the game changes.
    /// </summary>
    public static class OzL18CardCapture
    {
        public const string Tag = "[OpenZ L18 card capture]";
        public const int W = 2560, H = 1440, SW = 2560, SH = 1600;
        public static int StillEvery = 10, Jpg = 94;
        public static string Status = "idle", LastDir = "";
        public static int Frames;

        [MenuItem("OpenZ/V1.5/L18 Briefings/6 Card capture (Play, with 1 Record Blue)", false, 7621)]
        static void MenuGo() => Go();

        public static string Go()
        {
            if (!Application.isPlaying) { Status = "stopped: not in Play"; Debug.LogError(Tag + " " + Status); return Status; }
            if (Status == "waiting" || Status == "capturing") return Status;
            var go = new GameObject("OzL18CardCapture"); UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Run>(); Status = "waiting"; Frames = 0;
            return Status;
        }

        public sealed class Run : MonoBehaviour
        {
            static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
            IEnumerator Start()
            {
                float born = Time.realtimeSinceStartup;
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; int msaa = urp ? Mathf.Max(1, urp.msaaSampleCount) : 1;
                var hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.DefaultHDR;
                var rt = new RenderTexture(W, H, 24, hdr, RenderTextureReadWrite.Linear) { name = "OZ_L18Card", antiAliasing = msaa }; rt.Create();
                var srgb = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "OZ_L18Card_sRGB" }; srgb.Create();
                var rtS = new RenderTexture(SW, SH, 24, hdr, RenderTextureReadWrite.Linear) { name = "OZ_L18CardStill", antiAliasing = msaa }; rtS.Create();
                var srgbS = new RenderTexture(SW, SH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "OZ_L18CardStill_sRGB" }; srgbS.Create();
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
                var texS = new Texture2D(SW, SH, TextureFormat.RGB24, false, false);
                while (OzL18BriefingStage.Tau < -90f || Time.captureFramerate <= 0)
                {
                    if (Time.realtimeSinceStartup - born > 300f) { Finish("stopped: the blue briefing's stage did not start within 300 s"); yield break; }
                    yield return null;
                }
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/tmp", "l18_card_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv))).Replace('\\', '/');
                Directory.CreateDirectory(dir + "/frames"); Directory.CreateDirectory(dir + "/stills"); LastDir = dir;
                var csv = new StringBuilder("frame,stage_tau_s,file\n");
                Status = "capturing"; Debug.Log(Tag + " capturing into " + dir);
                var eof = new WaitForEndOfFrame(); int n = 0;
                while (true)
                {
                    yield return eof;
                    float t = OzL18BriefingStage.Tau;
                    if (t < -90f || Time.captureFramerate <= 0) break;
                    var rts = FindAnyObjectByType<RtsCamera>(); var cam = rts ? rts.GetComponent<Camera>() : Camera.main;
                    if (!cam) continue;
                    Grab(cam, rt, srgb, tex, (float)W / H);
                    File.WriteAllBytes(dir + "/frames/f_" + n.ToString("00000", Inv) + ".jpg", tex.EncodeToJPG(Jpg));
                    string still = "";
                    if (n % Mathf.Max(1, StillEvery) == 0)
                    {
                        Grab(cam, rtS, srgbS, texS, (float)SW / SH);
                        still = "stills/s_" + n.ToString("00000", Inv) + "_tau" + t.ToString("0.00", Inv) + ".png";
                        File.WriteAllBytes(dir + "/" + still, texS.EncodeToPNG());
                    }
                    csv.AppendLine(n.ToString(Inv) + "," + t.ToString("0.000", Inv) + "," + still);
                    n++; Frames = n;
                }
                File.WriteAllText(dir + "/capture.csv", csv.ToString());
                rt.Release(); srgb.Release(); rtS.Release(); srgbS.Release(); Destroy(rt); Destroy(srgb); Destroy(rtS); Destroy(srgbS); Destroy(tex); Destroy(texS);
                Finish("done: " + n + " frames in " + dir);
            }

            static void Grab(Camera cam, RenderTexture rt, RenderTexture srgb, Texture2D tex, float aspect)
            {
                float f0 = cam.fieldOfView; var keep = cam.targetTexture;
                try
                {
                    float hor = Mathf.Atan(Mathf.Tan(f0 * 0.5f * Mathf.Deg2Rad) * cam.aspect);                   // the screen frame's width is kept
                    cam.targetTexture = rt; cam.aspect = aspect;
                    cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(hor) / aspect) * Mathf.Rad2Deg;
                    cam.Render();
                }
                finally { cam.targetTexture = keep; cam.fieldOfView = f0; cam.ResetAspect(); }
                Graphics.Blit(rt, srgb);
                var was = RenderTexture.active; RenderTexture.active = srgb; tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false); RenderTexture.active = was;
            }

            void Finish(string s) { Status = s; Debug.Log(Tag + " " + s); Destroy(gameObject); }
        }
    }
}
