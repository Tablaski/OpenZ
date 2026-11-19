// OpenZ (GPL-3.0) - version 1.5, level 18: the briefing's loading picture as a real game frame. Editor only, never in a build.
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
    /// Menu OpenZ/V1.5/L18 Briefings/5 Loading picture burst. Level 18's loading picture (Resources/OpenZ/Loading/L18_loading_1.png, found by
    /// the menu's own LoadingStill as levels 2 and 17 are) is a real frame of the blue briefing's stage: the three bridges going up with the
    /// parades on them. Start it in Play while (or before) the blue briefing runs: while the stage clock passes From..To (seconds from the
    /// press; the middle bridge goes at 1.75) it renders the game camera from each of the picture's own low three-quarter places into a
    /// 3840 x 2400 HDR texture every Step s and writes it down-sampled to 2560 x 1600 (the size of the other levels' loading pictures).
    /// A camera render into a texture draws the world with every post effect of that moment and none of the screen overlays: no briefing
    /// bars, corner window, subtitles or HUD. The flight's own camera pose is put back at once. PNGs + capture.csv go to
    /// </summary>
    public static class OzL18LoadingPicture
    {
        public const string Tag = "[OpenZ L18 loading picture]";
        public const int W = 2560, H = 1600, Super = 3840, SuperH = 2400;
        /// <summary>The stage clock window (s from the press) and its step.</summary>
        public static float From = 2.30f, To = 2.62f, Step = 0.10f;
        /// <summary>The picture's own cameras: place, look point, vertical field of view at 16:9 (the 16:10 picture keeps that width).</summary>
        // red bank, south-east of the middle bridge, toward the city), pulled back and up with a wide lens at three distances: the river across
        // the frame, all three bridges, both banks, the blue fort beyond and the red fort this side. (Round 2's close places: (148, 5.5, -141) ->
        // (123, 4.5, -118) at 50 deg, (152, 9, -146) -> (122, 3.5, -117) at 46, (105, 6.5, -143) -> (129, 4.5, -119) at 50.)
        // The picked place is the second of the first set ((190, 22, -172) -> (118, 0, -112) at 58 deg). Re-aimed a little left and down, so the red
        // fort stands clear of the loading screen's title (lower left): three aims from that place and a step behind it.
        public static Vector3[] Pos = { new Vector3(190f, 22f, -172f), new Vector3(190f, 22f, -172f), new Vector3(195f, 24f, -177f) };
        public static Vector3[] Look = { new Vector3(112f, -6f, -118f), new Vector3(109f, -9f, -121f), new Vector3(112f, -7f, -119f) };
        public static float[] Fov = { 58f, 58f, 56f };
        public static string Status = "idle", LastDir = "";

        [MenuItem("OpenZ/V1.5/L18 Briefings/5 Loading picture burst (Play, blue briefing)", false, 7620)]
        static void MenuGo() => Go();

        public static string Go()
        {
            if (!Application.isPlaying) { Status = "stopped: not in Play"; Debug.LogError(Tag + " " + Status); return Status; }
            if (Status == "waiting" || Status == "capturing") return Status;
            var go = new GameObject("OzL18LoadingPicture"); UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Run>(); Status = "waiting";
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
                var rt = new RenderTexture(Super, SuperH, 24, hdr, RenderTextureReadWrite.Linear) { name = "OZ_L18Loading", antiAliasing = msaa }; rt.Create();
                var srgb = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "OZ_L18Loading_sRGB", filterMode = FilterMode.Bilinear }; srgb.Create();
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
                while (OzL18BriefingStage.Tau < -90f || OzL18BriefingStage.Tau < From)
                {
                    if (Time.realtimeSinceStartup - born > 240f) { Finish("stopped: the blue briefing's stage did not reach the window within 240 s"); yield break; }
                    yield return null;
                }
                var rts = FindAnyObjectByType<RtsCamera>(); var cam = rts ? rts.GetComponent<Camera>() : Camera.main;
                if (!cam) { Finish("stopped: no game camera"); yield break; }
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/tmp", "l18_loading_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv))).Replace('\\', '/');
                Directory.CreateDirectory(dir); LastDir = dir;
                var csv = new StringBuilder("index,stage_clock_s,camera,file\n");
                Status = "capturing"; Debug.Log(Tag + " capturing into " + dir);
                var eof = new WaitForEndOfFrame(); float next = From; int n = 0;
                while (true)
                {
                    yield return eof;
                    float t = OzL18BriefingStage.Tau;
                    if (t < -90f || t > To + 1e-4f || !cam) break;
                    if (t + 1e-4f < next) continue;
                    while (next <= t + 1e-4f) next += Mathf.Max(0.03f, Step);
                    for (int c = 0; c < Pos.Length; c++)
                    {
                        var tr = cam.transform; Vector3 p0 = tr.position; Quaternion r0 = tr.rotation; float f0 = cam.fieldOfView; var keep = cam.targetTexture;
                        try
                        {
                            tr.SetPositionAndRotation(Pos[c], Quaternion.LookRotation(Look[c] - Pos[c], Vector3.up));
                            cam.targetTexture = rt; cam.aspect = (float)W / H;
                            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(Fov[c] * 0.5f * Mathf.Deg2Rad) * (16f / 9f) / ((float)W / H)) * Mathf.Rad2Deg;
                            cam.Render();
                        }
                        finally { cam.targetTexture = keep; cam.fieldOfView = f0; cam.ResetAspect(); tr.SetPositionAndRotation(p0, r0); }
                        Graphics.Blit(rt, srgb);                                                         // linear HDR (MSAA resolved, 1.5 x down) -> sRGB 8 bit, as the screen's final blit
                        var was = RenderTexture.active; RenderTexture.active = srgb; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false); RenderTexture.active = was;
                        string file = "loading_c" + c + "_t" + t.ToString("0.00", Inv) + ".png";
                        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
                        csv.AppendLine(n.ToString(Inv) + "," + t.ToString("0.000", Inv) + "," + c + "," + file); n++;
                    }
                }
                File.WriteAllText(Path.Combine(dir, "capture.csv"), csv.ToString());
                rt.Release(); srgb.Release(); Destroy(rt); Destroy(srgb); Destroy(tex);
                Finish("done: " + n + " pictures in " + dir);
            }

            void Finish(string s) { Status = s; Debug.Log(Tag + " " + s); Destroy(gameObject); }
        }
    }
}
