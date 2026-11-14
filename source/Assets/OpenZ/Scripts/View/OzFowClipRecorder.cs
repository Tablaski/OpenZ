// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR checks (editor only): records the game view to an MP4 at a fixed step, for the owner's reveal clips.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR (round 4, 5 Nov), editor only: the game view as the player sees it (the HUD included, nothing added) recorded
    /// frame by frame at a fixed step (Time.captureFramerate, so the game, the units and the fog advance exactly 1 / fps a frame however long a
    /// frame takes: no hitch, no repeated frame, the real speed), each frame piped raw to ffmpeg (H.264, yuv420p, CRF 20, +faststart, 4
    /// threads, below-normal priority): no frame files on disk. A callback runs before every frame (the clip's cues: the camera held, the
    /// move order). Used by OpenZ/V1.4/Fog of War: Reveal Clips (Editor/OzV14FogOfWarCheck.cs).
    /// </summary>
    public sealed class OzFowClipRecorder : MonoBehaviour
    {
        /// <summary>ffmpeg (on the PATH unless set).</summary>
        public static string FfmpegPath = "ffmpeg";
        public static OzFowClipRecorder Live { get; private set; }
        /// <summary>Running, frames written, the last result line.</summary>
        public bool Running { get; private set; }
        public int Written { get; private set; }
        public string Result { get; private set; } = "";

        string path; int frames, fps, crf; Action<int> onFrame;
        Process ff; Stream stdin; byte[] buf; readonly StringBuilder err = new StringBuilder();

        /// <summary>Starts a recording of `frames` frames at `fps` into `path` (an .mp4); `onFrame(i)` runs before frame i is drawn.</summary>
        public static OzFowClipRecorder Begin(string path, int frames, int fps, int crf, Action<int> onFrame)
        {
            if (Live && Live.Running) { Debug.LogWarning("[OpenZ FogOfWar clip] a recording is already running"); return null; }
            var go = new GameObject("OZ_FowClipRecorder") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            var r = go.AddComponent<OzFowClipRecorder>();
            r.path = path; r.frames = frames; r.fps = fps; r.crf = crf; r.onFrame = onFrame; r.Running = true;
            Live = r;
            r.StartCoroutine(r.Run());
            return r;
        }

        IEnumerator Run()
        {
            int capWas = Time.captureFramerate;
            Time.captureFramerate = fps;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                try { onFrame?.Invoke(i); } catch (Exception e) { Debug.LogError("[OpenZ FogOfWar clip] cue at frame " + i + ": " + e); }
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    if (ff == null && !Open(tex)) { Fail("ffmpeg did not start: " + err); break; }
                    var data = tex.GetRawTextureData<byte>();
                    if (buf == null || buf.Length != data.Length) buf = new byte[data.Length];
                    data.CopyTo(buf);
                    stdin.Write(buf, 0, buf.Length);
                    Written++;
                }
                catch (Exception e) { Fail("frame " + i + ": " + e.Message + " " + err); break; }
                finally { Destroy(tex); }
            }
            Time.captureFramerate = capWas;
            try
            {
                if (stdin != null) { stdin.Flush(); stdin.Close(); }
                if (ff != null) { if (!ff.WaitForExit(120000)) { ff.Kill(); Fail("ffmpeg did not finish in 120 s"); } }
            }
            catch (Exception e) { Fail("closing ffmpeg: " + e.Message); }
            if (string.IsNullOrEmpty(Result))
            {
                long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                Result = (ff != null && ff.ExitCode == 0 && bytes > 0 ? "OK " : "FAILED ") + $"{Written} of {frames} frames at {fps} fps in {sw.Elapsed.TotalSeconds:0} s, {bytes / 1048576.0:0.0} MB, {path}" + (err.Length > 0 ? " | " + err : "");
            }
            Debug.Log("[OpenZ FogOfWar clip] " + Result);
            ff?.Dispose();
            Running = false;
            Destroy(gameObject);
        }

        void Fail(string why) { if (string.IsNullOrEmpty(Result)) Result = "FAILED " + why; }

        bool Open(Texture2D first)
        {
            string pix = first.format == TextureFormat.RGB24 ? "rgb24" : first.format == TextureFormat.RGBA32 ? "rgba" : first.format == TextureFormat.ARGB32 ? "argb" : first.format == TextureFormat.BGRA32 ? "bgra" : null;
            if (pix == null) { err.Append("unexpected capture format " + first.format); return false; }
            string args = $"-y -loglevel error -f rawvideo -pix_fmt {pix} -s {first.width}x{first.height} -r {fps} -i - -vf vflip -c:v libx264 -preset medium -crf {crf} -pix_fmt yuv420p -movflags +faststart -threads 4 \"{Path.GetFullPath(path)}\"";
            var psi = new ProcessStartInfo(FfmpegPath, args) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
            ff = Process.Start(psi);
            if (ff == null) return false;
            try { ff.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            ff.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (err) err.Append(e.Data).Append(' '); };
            ff.OutputDataReceived += (s, e) => { };
            ff.BeginErrorReadLine(); ff.BeginOutputReadLine();
            stdin = ff.StandardInput.BaseStream;
            Debug.Log($"[OpenZ FogOfWar clip] recording {frames} frames {first.width} x {first.height} {first.format} at {fps} fps into {path}");
            return true;
        }

        void OnDestroy()
        {
            if (Live == this) Live = null;
            if (Running)
            {   // stopped from outside (Play left): close the pipe so ffmpeg ends
                try { stdin?.Close(); } catch { }
                try { if (ff != null && !ff.HasExited) ff.Kill(); } catch { }
                Time.captureFramerate = 0;
            }
        }
    }
}
#endif
