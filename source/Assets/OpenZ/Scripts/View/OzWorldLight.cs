// OpenZ (GPL-3.0)
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>
    /// one reading of the game camera's picture, the world only (the HUD is a screen overlay the camera never draws). The camera draws
    /// one frame into a small float picture of its own (<see cref="W"/> x <see cref="H"/>) through the pipeline as always (every renderer
    /// feature, the atmosphere pass, post-processing), so a black pass shows there as on the screen. The reading: the mean of the red,
    /// green and blue values as an 8-bit sRGB picture stores them (0..255; a pixel that is not a number counts as black, as the screen
    /// shows it) and the number of such pixels.
    /// <see cref="Now"/> reads at once (the frame waits for the graphics chip: Play proofs); <see cref="Request"/> answers a few frames
    /// later without a wait (the frame rate bench, outside its measured frames).
    /// </summary>
    public static class OzWorldLight
    {
        /// <summary>Under this mean (of 255) the world is called black. A drawn level at the RTS camera reads about 55-110.</summary>
        public const float BlackBelow = 30f;
        /// <summary>The size of the reading's picture (16:9; the mean of a picture this small is the mean of the screen's within a few steps).</summary>
        public const int W = 96, H = 54;

        public struct Reading
        {
            /// <summary>The picture was drawn and read.</summary>
            public bool ok;
            /// <summary>Mean of R, G and B as 8-bit sRGB (0..255).</summary>
            public float mean;
            /// <summary>Pixels that are not a number, and all pixels.</summary>
            public int nan, pixels;
            /// <summary>Why there is no reading (ok false).</summary>
            public string why;
            public bool Black => ok && mean < BlackBelow;
            public override string ToString() => ok
                ? "mean " + mean.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " of 255, " + nan + " NaN of " + pixels + " px"
                : "not read (" + (why ?? "?") + ")";
        }

        /// <summary>The camera's picture now: drawn and read back in this frame (the frame waits for the graphics chip).</summary>
        public static Reading Now(Camera cam)
        {
            var r = new Reading();
            if (!cam) { r.why = "no camera"; return r; }
            var rt = Target();
            if (!Draw(cam, rt, out r.why)) { RenderTexture.ReleaseTemporary(rt); return r; }
            var tex = new Texture2D(W, H, TextureFormat.RGBAFloat, false, true) { name = "OzWorldLight" };
            var prev = RenderTexture.active; RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false); tex.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            r = Measure(tex.GetPixelData<Color>(0));
            Object.Destroy(tex);
            return r;
        }

        /// <summary>The camera's picture drawn now and read back without a wait; <paramref name="done"/> gets the reading a few frames
        /// later on the main thread (at once when the reading cannot be taken). False: no reading will come but the one handed over.</summary>
        public static bool Request(Camera cam, System.Action<Reading> done)
        {
            if (!cam) { done?.Invoke(new Reading { why = "no camera" }); return false; }
            if (!SystemInfo.supportsAsyncGPUReadback) { done?.Invoke(Now(cam)); return true; }
            var rt = Target();
            if (!Draw(cam, rt, out var why)) { RenderTexture.ReleaseTemporary(rt); done?.Invoke(new Reading { why = why }); return false; }
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBAFloat, q =>
            {
                var r = q.hasError ? new Reading { why = "the read-back failed" } : Measure(q.GetData<Color>());
                RenderTexture.ReleaseTemporary(rt);
                done?.Invoke(r);
            });
            return true;
        }

        static RenderTexture Target()
        {
            var d = new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGBFloat, 24) { sRGB = false, msaaSamples = 1 };
            var rt = RenderTexture.GetTemporary(d);
            rt.name = "OzWorldLight";
            return rt;
        }

        /// <summary>The camera draws one frame into rt (the project's own pattern: a single camera request, else Camera.Render).</summary>
        static bool Draw(Camera cam, RenderTexture rt, out string why)
        {
            why = null;
            try
            {
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old; }
                return true;
            }
            catch (System.Exception e) { why = e.GetType().Name + ": " + e.Message; return false; }
        }

        static Reading Measure(NativeArray<Color> px)
        {
            var r = new Reading { ok = true, pixels = px.Length };
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;                     // the picture holds linear values: stored as sRGB on the screen
            double sum = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b)) { r.nan++; continue; }   // not a number: black on the screen
                sum += Enc(c.r, linear) + Enc(c.g, linear) + Enc(c.b, linear);
            }
            r.mean = (float)(sum / (3.0 * Mathf.Max(1, px.Length)) * 255.0);
            return r;
        }

        static float Enc(float v, bool linear)
        {
            v = Mathf.Clamp01(v);
            return linear ? Mathf.LinearToGammaSpace(v) : v;
        }
    }
}
