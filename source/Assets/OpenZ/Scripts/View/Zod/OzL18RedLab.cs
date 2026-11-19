#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.5, level 18: stills for tuning the red briefing in Play (editor only; no menu, called from the Play checks).
//  OzL18RedLab.Flights(dir, variant): every shot of the red list held at five moments (ZodCinePreview.Hold), the game camera's picture of each
//    as dir/f_<shot>_<k>.png (640 x 360), with the stage's actors at that second.
//  OzL18RedLab.Window(dir, "t1,t2,..."): the corner window's picture at those seconds of the line (OzL18WatchSet.LabSet / LabRender) as dir/w_<n>.png.
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzL18RedLab : MonoBehaviour
    {
        public static string Report = "idle";
        static OzL18RedLab Host()
        {
            var old = FindFirstObjectByType<OzL18RedLab>(); if (old) return old;
            var go = new GameObject("OzL18RedLab"); return go.AddComponent<OzL18RedLab>();
        }

        public static string Flights(string dir, int variant, int w = 640, int h = 360, string us = "0.02,0.25,0.5,0.75,0.98", int onlyShot = 0)
        {
            System.IO.Directory.CreateDirectory(dir); Report = "running"; Host().StartCoroutine(Host().RunFlights(dir, variant, w, h, us, onlyShot)); return "started";
        }

        IEnumerator RunFlights(string dir, int variant, int w, int h, string us, int onlyShot)
        {
            int was = OzL18Drone.Variant; OzL18Drone.Variant = variant;
            var parts = us.Split(','); int n = 0;
            ZodCinePreview.Stop(); yield return null;
            string r = ZodCinePreview.Hold(1, 0f); yield return null; yield return null;
            var fly = ZodFlyover.Live; int shots = 0; if (fly != null) foreach (var cs in fly.CineShots) shots++;
            for (int s = 1; s <= shots; s++)
            {
                if (onlyShot > 0 && s != onlyShot) continue;
                for (int k = 0; k < parts.Length; k++)
                {
                    float u = float.Parse(parts[k], CultureInfo.InvariantCulture);
                    ZodCinePreview.Hold(s, u);
                    for (int i = 0; i < 4; i++) yield return null;
                    yield return new WaitForEndOfFrame();
                    Grab(Camera.main, dir + "/f_" + variant + "_" + s + "_" + k + ".png", w, h); n++;
                }
            }
            ZodCinePreview.Stop(); OzL18Drone.Variant = was;
            Report = "flights done: " + n + " pictures, " + shots + " shots (" + r + "); " + ZodFlyover.LastPlan;
        }

        static void Grab(Camera c, string path, int w, int h)
        {
            if (!c) return;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.antiAliasing = 2;
            var was = c.targetTexture; c.targetTexture = rt; c.Render(); c.targetTexture = was;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
        }

        public static string Window(string dir, string times, int w = 640, int h = 480, int shot = -1)
        {
            System.IO.Directory.CreateDirectory(dir); Report = "running"; Host().StartCoroutine(Host().RunWindow(dir, times, w, h, shot)); return "started";
        }

        IEnumerator RunWindow(string dir, string times, int w, int h, int shot)
        {
            var parts = times.Split(','); int n = 0; string last = "";
            for (int k = 0; k < parts.Length; k++)
            {
                float t = float.Parse(parts[k], CultureInfo.InvariantCulture);
                OzL18WatchSet.LabSet(t, shot);
                for (int i = 0; i < 10; i++) yield return null;
                yield return new WaitForEndOfFrame();
                last = OzL18WatchSet.LabRender(dir + "/w_" + k.ToString("00") + ".png", w, h); n++;
            }
            Report = "window done: " + n + " pictures; " + last + "; feet: " + OzL18WatchSet.LabFeet();
        }
    }
}
#endif
