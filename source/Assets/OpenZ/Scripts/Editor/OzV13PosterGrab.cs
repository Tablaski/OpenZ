#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3: the level 7 poster burst (OzV13PosterCapture). Editor only, never in a build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Added by OzV13PosterCapture once level 7 runs as red. Waits for the briefing's shot list, then at the end of every frame whose
    /// briefing clock has reached the next step (From, From + Step, ... To) renders the game camera into a 2560 x 1600 texture with the
    /// picture's field of view (OzV13PosterCapture.PictureFov), reads it back and hands the pixels to two low-priority threads that write
    /// the PNGs. A long frame skips steps, never repeats one. The camera's own target, field of view and aspect are put back at once, so
    /// the screen never shows a changed frame. One row per frame in capture.csv; summary.txt says what ran.
    /// The target is half float (HDR), as the screen's colour buffer: URP takes a texture target's own format for the camera's colour
    /// buffer (UniversalRenderPipelineCore.CreateRenderTextureDescriptor), so an 8-bit target would clip the sun disc (HDR, DiscGain 9)
    /// at 1 before the bloom (day threshold 1.25, linear 1.63) and the ACES tone mapping: no bloom halo, grey highlights (CaptureKit's
    /// note: its RenderTexture path loses the lamp bloom). One blit then writes it into an sRGB 8-bit texture, the linear to sRGB step
    /// the screen's own final blit does, and that is read back.
    /// </summary>
    public static class OzV13PosterGrab { public sealed class Run : MonoBehaviour   // RUN7 U6: nested, so no MonoScript of its own: Unity adds it from the Editor folder
    {
        sealed class Job { public string path; public byte[] raw; }

        const int W = OzV13PosterCapture.W, H = OzV13PosterCapture.H;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        string dir;
        RenderTexture rt, srgb; Texture2D tex; Camera cam;
        RenderTextureFormat hdr = RenderTextureFormat.ARGBHalf;
        readonly StringBuilder csv = new StringBuilder();
        readonly Queue<Job> todo = new Queue<Job>();
        readonly List<Job> fallback = new List<Job>();
        Thread[] workers;
        bool stop, finished;
        int pending, written, captured;
        string firstStatus = "", encodeNote = "";

        IEnumerator Start()
        {
            float born = Time.realtimeSinceStartup;
            ZodFlyover f;
            while (true)
            {
                f = ZodFlyover.Live;
                if (f != null && f.Active && f.Cine && ZodBriefing.BriefingActive) break;
                if (Time.realtimeSinceStartup - born > 60f) { Finish("stopped: no briefing shot list within 60 s (briefing off or skipped?)"); yield break; }
                yield return null;
            }
            var rts = FindAnyObjectByType<RtsCamera>();
            cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            if (!cam) { Finish("stopped: no game camera"); yield break; }
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3", "l07_poster_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv)));
            Directory.CreateDirectory(dir);
            OzV13PosterCapture.LastDir = dir;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int msaa = urp ? Mathf.Max(1, urp.msaaSampleCount) : 1;                                          // URP takes a texture target's own sample count: the screen's MSAA
            if (!SystemInfo.SupportsRenderTextureFormat(hdr)) hdr = RenderTextureFormat.DefaultHDR;
            if (!SystemInfo.SupportsRenderTextureFormat(hdr)) { Finish("stopped: no HDR render texture on this GPU (the sun's bloom needs one)"); yield break; }
            rt = new RenderTexture(W, H, 24, hdr, RenderTextureReadWrite.Linear) { name = "OZ_PosterBurst", antiAliasing = msaa };   // HDR, as the screen's colour buffer (see the summary above)
            rt.Create();
            srgb = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "OZ_PosterBurst_sRGB" };
            srgb.Create();
            tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);                                    // RGB: the poster has no alpha
            workers = new Thread[2];
            for (int i = 0; i < workers.Length; i++) { workers[i] = new Thread(Work) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "OZ poster png " + i }; workers[i].Start(); }
            firstStatus = ZodFlyover.Status();
            csv.AppendLine("index,clock_s,shot,shot_name,cam_x,cam_y,cam_z,pitch,yaw,roll,game_fov,picture_fov,time_of_day,frame,file");
            OzV13PosterCapture.Status = "capturing";
            Debug.Log(OzV13PosterCapture.Tag + " capturing into " + dir + "; " + firstStatus);
            var eof = new WaitForEndOfFrame();
            float from = OzV13PosterCapture.From, to = OzV13PosterCapture.To, step = Mathf.Max(0.02f, OzV13PosterCapture.Step), next = from;
            string end = "the window ended";
            while (true)
            {
                yield return eof;
                if (!cam) { end = "the game camera went away"; break; }
                if (!f.Active || !f.Cine || ZodFlyover.Live != f) { end = "the shot list ended before " + to.ToString("0.0", Inv) + " s (SPACE?)"; break; }
                float t = f.CineTime;
                if (t > to + 1e-4f) break;
                if (t + 1e-4f < next) continue;
                Grab(f, t);
                while (next <= t + 1e-4f) next += step;
            }
            OzV13PosterCapture.Status = "writing";
            float wait = Time.realtimeSinceStartup;
            while (Volatile.Read(ref pending) > 0 && Time.realtimeSinceStartup - wait < 180f) yield return null;
            Job[] late;
            lock (todo) { late = fallback.ToArray(); fallback.Clear(); }
            foreach (var j in late)
            {   // a format the threads could not write: the main thread writes it the plain way
                tex.LoadRawTextureData(j.raw); tex.Apply(false);
                File.WriteAllBytes(j.path, tex.EncodeToPNG()); Interlocked.Increment(ref written);
                yield return null;
            }
            if (late.Length > 0) encodeNote = late.Length + " written on the main thread";
            Finish("done: " + end);
        }

        void Grab(ZodFlyover f, float t)
        {
            float fov0 = cam.fieldOfView, fov = OzV13PosterCapture.PictureFov(fov0);
            var keep = cam.targetTexture;
            try { cam.targetTexture = rt; cam.aspect = (float)W / H; cam.fieldOfView = fov; cam.Render(); }
            finally { cam.targetTexture = keep; cam.fieldOfView = fov0; cam.ResetAspect(); }
            Graphics.Blit(rt, srgb);                                                                         // linear HDR (MSAA resolved) -> sRGB 8 bit, as the screen's final blit
            var was = RenderTexture.active;
            RenderTexture.active = srgb;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
            RenderTexture.active = was;
            int i = captured++;
            string file = "poster_" + i.ToString("D3", Inv) + "_t" + t.ToString("00.00", Inv) + ".png";
            var job = new Job { path = Path.Combine(dir, file), raw = tex.GetRawTextureData<byte>().ToArray() };
            Interlocked.Increment(ref pending);
            lock (todo) { todo.Enqueue(job); Monitor.Pulse(todo); }
            var tr = cam.transform; var e = tr.rotation.eulerAngles; var dn = FindAnyObjectByType<DayNight>();
            csv.AppendLine(string.Join(",", i.ToString(Inv), t.ToString("0.000", Inv), (f.CineShotIndex + 1).ToString(Inv), f.Current,
                tr.position.x.ToString("0.00", Inv), tr.position.y.ToString("0.00", Inv), tr.position.z.ToString("0.00", Inv),
                e.x.ToString("0.00", Inv), e.y.ToString("0.00", Inv), e.z.ToString("0.00", Inv), fov0.ToString("0.00", Inv), fov.ToString("0.00", Inv),
                dn ? dn.timeOfDay.ToString("0.0000", Inv) : "-", Time.frameCount.ToString(Inv), file));
        }

        void Work()
        {
            while (true)
            {
                Job j;
                lock (todo)
                {
                    while (todo.Count == 0 && !stop) Monitor.Wait(todo);
                    if (todo.Count == 0) return;
                    j = todo.Dequeue();
                }
                try { File.WriteAllBytes(j.path, ImageConversion.EncodeArrayToPNG(j.raw, GraphicsFormat.R8G8B8_SRGB, (uint)W, (uint)H)); Interlocked.Increment(ref written); }
                catch (Exception) { lock (todo) fallback.Add(j); }
                finally { Interlocked.Decrement(ref pending); }
            }
        }

        void Finish(string how)
        {
            if (finished) return;
            finished = true;
            lock (todo) { stop = true; Monitor.PulseAll(todo); }
            if (!string.IsNullOrEmpty(dir))
            {
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                var s = new StringBuilder();
                s.AppendLine("OpenZ 1.3 row 21 POSTER burst, level 7, red briefing");
                s.AppendLine("result: " + how);
                s.AppendLine("frames: " + captured + " captured, " + Volatile.Read(ref written) + " written" + (encodeNote.Length > 0 ? " (" + encodeNote + ")" : ""));
                s.AppendLine("window: briefing clock " + OzV13PosterCapture.From.ToString("0.00", Inv) + " to " + OzV13PosterCapture.To.ToString("0.00", Inv) + " s, step " + OzV13PosterCapture.Step.ToString("0.00", Inv));
                s.AppendLine("picture: " + W + " x " + H + ", MatchWidth " + OzV13PosterCapture.MatchWidth + " (game fov 40 -> picture fov " + OzV13PosterCapture.PictureFov(40f).ToString("0.00", Inv) + ")"
                             + "; target " + hdr + " (HDR, as the screen) -> sRGB 8 bit");
                s.AppendLine("pipeline: " + (urp ? urp.name + ", render scale " + urp.renderScale.ToString("0.00", Inv) + ", MSAA " + urp.msaaSampleCount + ", HDR " + urp.supportsHDR : "none")
                             + "; quality " + QualitySettings.names[QualitySettings.GetQualityLevel()] + "; Game view " + Screen.width + " x " + Screen.height);
                s.AppendLine("at the first frame: " + firstStatus);
                s.AppendLine("plan: " + ZodFlyover.LastPlan);
                File.WriteAllText(Path.Combine(dir, "summary.txt"), s.ToString());
                File.WriteAllText(Path.Combine(dir, "capture.csv"), csv.ToString());
            }
            OzV13PosterCapture.Status = how + (string.IsNullOrEmpty(dir) ? "" : ", " + captured + " frames");
            Debug.Log(OzV13PosterCapture.Tag + " " + OzV13PosterCapture.Status + (string.IsNullOrEmpty(dir) ? "" : " -> " + dir));
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!finished) { finished = true; OzV13PosterCapture.Status = "stopped: Play ended during the burst"; }
            lock (todo) { stop = true; Monitor.PulseAll(todo); }
            if (cam && cam.targetTexture == rt) { cam.targetTexture = null; cam.ResetAspect(); }
            if (rt) { rt.Release(); Destroy(rt); }
            if (srgb) { srgb.Release(); Destroy(srgb); }
            if (tex) Destroy(tex);
        }
    }
    }   // RUN7 U6: end of the holder class
}
#endif
