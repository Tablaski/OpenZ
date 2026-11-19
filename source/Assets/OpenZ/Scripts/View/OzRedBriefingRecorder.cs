#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.5, level 18: the red twin of the blue cutscene recorder (OzBlueCutsceneProof, kit k75b), editor only.
// Started on the MainMenu scene in Play it deploys the level as red (the PLAY AS BLUE box off for the take, put back after), records from the menu
// on by the 75b method (30 fps frame-locked: Time.captureFramerate, one JPG a frame of the 1920 x 1080 Game view, the game's sound through
// AudioRenderer in step), lets the red level-start briefing run with its full-screen flyover to the hand-back, keeps the tail, joins the take with
// ffmpeg and cuts the clip from the briefing's start mark (minus 0.5 s) to its end mark plus the tail. Per frame it writes the briefing line's
// clock and the flyover's shot (frame_log.csv); after the take the sound log and the hand-back checks. Menu OpenZ/V1.5/L18 Briefings.
// PACED: the red side's director, watch set and animators run on real (unscaled) time, the blue director on the take's step. So each frame is
// passed: real time and take time then move together while the editor keeps up; frame_log.csv logs each frame's real seconds (real_dt).
// Also here: OzBriefingFrameLog (the same per-frame log beside the blue 75b take) and OzL18SkipMatrix (SPACE at many moments, both sides).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace OpenZ
{
    public sealed class OzRedBriefingRecorder : MonoBehaviour
    {
        public static string Report = "";
        public static string Level = "L18";
        public static int Fps = 30, Jpg = 92;
        OzBriefingRecTimes lt; string dir, recDir; readonly StringBuilder sb = new StringBuilder(); int passes, fails;
        bool boxWas, boxChanged, recording, audioStarted; int frameNo, holdFrames, renderFails, rate, ch, per; float peak, sound0;
        readonly List<float> samples = new List<float>(); readonly Dictionary<string, float> marks = new Dictionary<string, float>();
        readonly List<(string name, int frame)> pics = new List<(string, int)>(); int shotI; StreamWriter frameLog;
        int holdSamples, holdLapses;
        /// <summary>Async readbacks come upside down on this project's graphics API (as the 1.3 lab recorder's): the join flips them.</summary>
        public static bool FlipFrames = true;
        RenderTexture rt; int pending; readonly System.Collections.Concurrent.ConcurrentBag<byte[]> pool = new System.Collections.Concurrent.ConcurrentBag<byte[]>();
        readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch(); double lastReal; int lateFrames; double realSum, realMax;

        public static void Start(string level)
        {
            var old = FindFirstObjectByType<OzRedBriefingRecorder>(); if (old) Destroy(old.gameObject);
            Level = level;
            var go = new GameObject("OzRedBriefingRecorder"); DontDestroyOnLoad(go);
            var p = go.AddComponent<OzRedBriefingRecorder>(); p.StartCoroutine(p.Run());
        }

        void Log(string s) { sb.AppendLine(s); Debug.Log("[OzRedBriefingRecorder] " + s); }
        /// <summary>The level 18 stage's state of this frame up to its build line, without commas (one csv cell).</summary>
        static string StageShort() { string st = OzL18BriefingStage.Status(); int j = st.IndexOf(", built", StringComparison.Ordinal); if (j > 0) st = st.Substring(0, j); return st.Replace(',', ' '); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        float Clock => recording ? (frameNo + holdFrames) / (float)Fps : Time.realtimeSinceStartup;
        float Mark => recording ? frameNo / (float)Fps : Clock;
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        IEnumerator Run()
        {
            lt = OzBriefingRecTimes.Red(Level);
            if (lt == null) { Check("level " + Level + " has a red row in OzBriefingRecTimes", false); Finish(); yield break; }
            dir = lt.NewFramesDir("RecordRed");
            var m = FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Check("the MainMenu scene is in Play", false); Finish(); yield break; }
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == Level);
            if (card < 0) { Check("level " + Level + " is a level of the game", false); Finish(); yield break; }
            boxWas = OzPlayBlue.Box; if (boxWas) { OzPlayBlue.Box = false; boxChanged = true; }
            yield return Real(0.8f);
            m.ProofSelect(); yield return Real(0.8f);
            m.ProofFocus(card); yield return Real(0.6f);
            yield return StartRecording();
            m.ProofDeploy();
            float t0 = Time.realtimeSinceStartup;
            while (!ZGame.I && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            while (ZGame.I && ZGame.I.World == null && Time.realtimeSinceStartup - t0 < 90f) yield return null;
            Check("the level " + Level + " deployed as red (human " + (ZGame.I ? ZGame.I.player.ToString() : "none") + ", PLAY AS BLUE " + OzPlayBlue.Active + ")", ZGame.I && !OzPlayBlue.Active && ZGame.I.level == Level);
            float tb = Clock;
            while (!ZodBriefing.BriefingActive && Clock - tb < 20f) yield return null;
            Check("the red briefing started (ZodBriefing active, line " + ZodBriefing.DebugLineId + ")", ZodBriefing.BriefingActive);
            if (!ZodBriefing.BriefingActive) { yield return StopRecording(lt.ClipName); Finish(); yield break; }
            marks["briefing_start"] = Mark;
            float t1 = Clock; bool flewSeen = false;
            while ((ZodBriefing.BriefingActive || Flying) && Clock - t1 < lt.WatchSeconds)
            {
                if (Flying) flewSeen = true;
                if (ZodBriefing.BriefingActive) { holdSamples++; if (AudioDirector.HoldStartLines == null || !AudioDirector.HoldStartLines()) holdLapses++; }
                float lineT = ZodBriefing.DebugLineT;
                while (shotI < lt.Shots.Length && lineT > -98f && lineT >= lt.Shots[shotI].t) { pics.Add((lt.Shots[shotI].name, frameNo)); shotI++; }
                yield return null;
            }
            marks["briefing_end"] = Mark;
            Check("the briefing ended inside the watch (" + (marks["briefing_end"] - marks["briefing_start"]).ToString("F2") + " s of take time, watch " + lt.WatchSeconds + " s)", !ZodBriefing.BriefingActive && !Flying);
            Check("the level-start flyover flew under the briefing", flewSeen);
            Check("the start lines were held through the briefing (" + holdLapses + " of " + holdSamples + " frames without the hold)", holdSamples > 30 && holdLapses == 0);
            Check("the level was handed back (sim running, HUD visible)", ZGame.I && ZGame.I.enabled && OpenZHud.I && OpenZHud.I.enabled);
            float te = Clock; while (Clock - te < lt.TailSeconds) yield return null;
            Log(HandBackState("after the tail"));
            yield return StopRecording(lt.ClipName);
            Finish();
        }

        /// <summary>The hand-back state the kit's skip matrix asks for: stage leftovers, the watch set drawable by the game camera, the bridges.</summary>
        public static string HandBackState(string when)
        {
            int left = OzL18BriefingStage.Leftovers(), draw = OzL18WatchSet.MainCanDraw();
            int br = 0, brDown = 0; var w = ZGame.I ? ZGame.I.World : null;
            if (w != null) foreach (var o in w.Objects) if (o.IsBridge) { br++; if (o.Destroyed) brDown++; }
            return $"hand-back state {when}: stage leftovers {left} (0 wanted), watch set renderers the game camera can draw {draw} (0 wanted), bridges in the rules {br}, destroyed {brDown}, flyover {(Flying ? "FLYING" : "landed")}, briefing {(ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive ? "ACTIVE" : "over")}";
        }

        IEnumerator StartRecording()
        {
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ red briefing 1920x1080", out var info); Log("Game view: " + info);
            float tw = Time.realtimeSinceStartup;
            while ((Screen.width != 1920 || Screen.height != 1080) && Time.realtimeSinceStartup - tw < 6f) yield return null;
            Check("the Game view is 1920 x 1080 for the take (it is " + Screen.width + " x " + Screen.height + ")", Screen.width == 1920 && Screen.height == 1080);
            recDir = Path.Combine(dir, "take_frames_briefing").Replace('\\', '/'); Directory.CreateDirectory(recDir);
            rate = AudioSettings.outputSampleRate; ch = Channels(AudioSettings.speakerMode); per = rate / Fps;
            samples.Clear(); frameNo = 0; holdFrames = 0; renderFails = 0; peak = 0f;
            frameLog = new StreamWriter(Path.Combine(dir, "frame_log.csv")); frameLog.WriteLine("frame,take_s,real_dt,line_id,line_t,flyover,stage,voice_s,jaw_alan,jaw_brad,jaw_zod");
            rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); pending = 0; lateFrames = 0; realSum = 0; realMax = 0; watch.Restart(); lastReal = 0;
            Time.captureFramerate = Fps; audioStarted = AudioRenderer.Start();
            Check("the sound capture started (" + rate + " Hz, " + ch + " channels)", audioStarted);
            sound0 = Time.unscaledTime; Log("sound log zero (unscaled time at the take's first frame) " + sound0.ToString("F3"));
            recording = true;
            StartCoroutine(Grabber());
            yield return null;
        }
        static int Channels(AudioSpeakerMode m)
        {
            switch (m) { case AudioSpeakerMode.Mono: return 1; case AudioSpeakerMode.Quad: return 4; case AudioSpeakerMode.Surround: return 5; case AudioSpeakerMode.Mode5point1: return 6; case AudioSpeakerMode.Mode7point1: return 8; default: return 2; }
        }
        static string FlyShort() { string s = ZodFlyover.Status(); int k = s.IndexOf("; plan:", StringComparison.Ordinal); if (k > 0) s = s.Substring(0, k); return s.Replace(',', ' '); }
        IEnumerator Grabber()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            while (recording)
            {
                yield return new WaitForEndOfFrame();
                if (!recording) break;
                if (Time.captureFramerate <= 0) { holdFrames++; continue; }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
                int me = frameNo; string path = Path.Combine(recDir, $"f_{me:00000}.jpg"); System.Threading.Interlocked.Increment(ref pending);
                UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, q =>
                {
                    if (q.hasError) { System.Threading.Interlocked.Increment(ref renderFails); System.Threading.Interlocked.Decrement(ref pending); return; }
                    if (!pool.TryTake(out var raw)) raw = new byte[1920 * 1080 * 4];
                    q.GetData<byte>().CopyTo(raw); int jq = Jpg;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, GraphicsFormat.R8G8B8A8_SRGB, 1920u, 1080u, 0, jq)); }
                        finally { pool.Add(raw); System.Threading.Interlocked.Decrement(ref pending); }
                    });
                });
                if (audioStarted)
                {
                    int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                    using (var nb = new NativeArray<float>(Mathf.Max(0, avail) * ch, Allocator.Temp))
                    {
                        if (!AudioRenderer.Render(nb)) renderFails++;
                        for (int k = 0; k < nb.Length; k++) { float s = nb[k]; samples.Add(s); if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s); }
                    }
                }
                // the pacing: this frame is held until 1/30 s of real time has passed since the last one (never shorter; a slow frame stays slow)
                double now = watch.Elapsed.TotalSeconds, wait = lastReal + 1.0 / Fps - now;
                if (wait > 0.001) System.Threading.Thread.Sleep((int)(wait * 1000.0));
                now = watch.Elapsed.TotalSeconds; double dt = frameNo == 0 ? 1.0 / Fps : now - lastReal; lastReal = now;
                if (frameNo > 0) { realSum += dt; if (dt > realMax) realMax = dt; if (dt > 1.25 / Fps) lateFrames++; }
                frameLog?.WriteLine(frameNo + "," + (frameNo / (float)Fps).ToString("F3", inv) + "," + dt.ToString("F4", inv) + "," + (ZodBriefing.DebugLineId ?? "-") + "," + ZodBriefing.DebugLineT.ToString("F3", inv) + "," + FlyShort() + "," + StageShort() + "," + OzL18WatchSet.JawLog);
                frameNo++;
            }
        }
        IEnumerator StopRecording(string name)
        {
            recording = false;
            if (audioStarted) { AudioRenderer.Stop(); audioStarted = false; }
            Time.captureFramerate = 0;
            float tw = Time.realtimeSinceStartup; while (System.Threading.Volatile.Read(ref pending) > 0 && Time.realtimeSinceStartup - tw < 60f) yield return null;   // the readbacks land on the main thread
            if (rt) { rt.Release(); Destroy(rt); rt = null; }
            Log($"pacing: {frameNo} frames, real seconds a frame avg {(frameNo > 1 ? realSum / (frameNo - 1) : 0):F4} (take 1/{Fps} = {1.0 / Fps:F4}), max {realMax:F3}, frames over 1.25 x the step {lateFrames}; readbacks still pending {pending}");
            if (frameLog != null) { frameLog.Close(); frameLog = null; }
            try { File.WriteAllText(Path.Combine(dir, "sound_log.txt"), "times are unscaled game time; the take's frame 0 = " + sound0.ToString("F3") + "\n" + OzSoundLog.Format(OzSoundLog.Since(sound0))); } catch (Exception e) { Log("sound log failed: " + e.Message); }
            string wav = Path.Combine(dir, name + "_take.wav");
            int want = frameNo * per * ch;
            while (samples.Count < want) samples.Add(0f);
            if (samples.Count > want) samples.RemoveRange(want, samples.Count - want);
            using (var fs = new FileStream(wav, FileMode.Create)) using (var bw = new BinaryWriter(fs))
            {
                int bytes = samples.Count * 4;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch); bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes); foreach (var s in samples) bw.Write(s);
            }
            samples.Clear();
            Log($"take: {frameNo} frames at {Fps} ({frameNo / (float)Fps:F1} s), sound {wav} (peak {peak:F3}, render fails {renderFails}, held {holdFrames})");
            Check("the take has frames and sound", frameNo > Fps * 5 && peak > 0.01f && renderFails == 0);
            foreach (var p in pics)
            {
                string src = Path.Combine(recDir, $"f_{p.frame:00000}.jpg");
                try { if (File.Exists(src)) File.Copy(src, Path.Combine(dir, "pic_" + p.name + ".jpg"), true); } catch (Exception) { }
                Log($"picture {p.name}: take frame {p.frame} ({p.frame / (float)Fps:F2} s)");
            }
            string ff = FfmpegExe();
            if (ff == null) { Check("ffmpeg found", false); yield break; }
            string full = Path.Combine(dir, name + "_full.mp4").Replace('\\', '/');
            bool ok = Ffmpeg(ff, $"-y -framerate {Fps} -i \"{recDir}/f_%05d.jpg\" -i \"{wav.Replace('\\', '/')}\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -vf {(FlipFrames ? "vflip," : "")}scale=1920:1080:flags=lanczos -c:a aac -b:a 192k -movflags +faststart -shortest \"{full}\"", 300000) && File.Exists(full);
            Check("the take joined: " + full, ok);
            if (!ok) yield break;
            Directory.CreateDirectory(lt.ClipDir);
            string mp4 = Path.Combine(lt.ClipDir, name + ".mp4").Replace('\\', '/');
            string range = "";
            if (marks.TryGetValue("briefing_start", out var a) && marks.TryGetValue("briefing_end", out var b))
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                range = "-ss " + Mathf.Max(0f, a - 0.5f).ToString("F2", inv) + " -to " + (b + lt.TailSeconds).ToString("F2", inv) + " ";
                Log("cut: take " + Mathf.Max(0f, a - 0.5f).ToString("F2") + " - " + (b + lt.TailSeconds).ToString("F2") + " s");
            }
            ok = Ffmpeg(ff, $"-y {range}-i \"{full}\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart \"{mp4}\"", 300000) && File.Exists(mp4) && new FileInfo(mp4).Length > 50000;
            Check("the MP4 is written " + mp4, ok);
        }
        static string FfmpegExe()
        {
            var env = Environment.GetEnvironmentVariable("OPENZ_FFMPEG"); if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) { try { var p = Path.Combine(d.Trim('"'), "ffmpeg.exe"); if (File.Exists(p)) return p; } catch (Exception) { } }
            var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var link = Path.Combine(la, "Microsoft", "WinGet", "Links", "ffmpeg.exe"); if (File.Exists(link)) return link;
            var pk = Path.Combine(la, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(pk)) foreach (var d in Directory.GetDirectories(pk, "*FFmpeg*")) foreach (var f in Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories)) return f;
            return null;
        }
        bool Ffmpeg(string exe, string args, int waitMs)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(waitMs)) { p.Kill(); Log("ffmpeg timed out: " + args); return false; }
                    if (p.ExitCode != 0) { Log("ffmpeg exit " + p.ExitCode + ": " + (err.Length > 500 ? err.Substring(err.Length - 500) : err)); return false; }
                    return true;
                }
            }
            catch (Exception e) { Log("ffmpeg failed: " + e.Message); return false; }
        }

        void Finish()
        {
            if (boxChanged) { OzPlayBlue.Box = boxWas; Log("the side box put back " + (boxWas ? "ON" : "OFF")); }
            string head = $"{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks (red briefing {Level}); take files in {dir}";
            Report = head + "\n" + sb;
            try { if (dir != null) File.WriteAllText(Path.Combine(dir, "report_RecordRed.txt"), Report); } catch (Exception) { }
            Debug.Log("[OzRedBriefingRecorder] " + head);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            recording = false;
            if (audioStarted) { AudioRenderer.Stop(); audioStarted = false; }
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            if (frameLog != null) { frameLog.Close(); frameLog = null; }
            if (rt) { rt.Release(); Destroy(rt); rt = null; }
        }
    }

    /// <summary>The per-frame log beside the blue 75b take: while the fixed step runs (Time.captureFramerate over 0) one row a frame (the scene's
    /// clock, the flyover's shot), then the sound log of the take; both into OzBriefingRecTimes.LastFramesDir. Its frame count starts on the first
    /// frame with the fixed step on: it can lead the take's own frame numbers by one frame.</summary>
    public sealed class OzBriefingFrameLog : MonoBehaviour
    {
        StreamWriter w; int n; bool seen; float sound0; string dirAt = "";
        public static void Begin()
        {
            var old = FindFirstObjectByType<OzBriefingFrameLog>(); if (old) Destroy(old.gameObject);
            var go = new GameObject("OzBriefingFrameLog"); DontDestroyOnLoad(go); go.AddComponent<OzBriefingFrameLog>();
        }
        void LateUpdate()
        {
            if (Time.captureFramerate > 0)
            {
                if (!seen) { seen = true; dirAt = OzBriefingRecTimes.LastFramesDir; sound0 = Time.unscaledTime; w = new StreamWriter(Path.Combine(dirAt, "frame_log.csv")); w.WriteLine("frame,scene,scene_t,flyover,stage"); }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string fly = ZodFlyover.Status(); int k = fly.IndexOf("; plan:", StringComparison.Ordinal); if (k > 0) fly = fly.Substring(0, k);
                string st = OzL18BriefingStage.Status(); int j = st.IndexOf(", built", StringComparison.Ordinal); if (j > 0) st = st.Substring(0, j);
                w.WriteLine(n + "," + (OzBlueCameo.LastSceneKind ?? "-") + "," + OzBlueCameo.LastSceneT.ToString("F3", inv) + "," + fly.Replace(',', ' ') + "," + st.Replace(',', ' '));
                n++;
            }
            else if (seen)
            {
                w.Close(); w = null;
                try { File.WriteAllText(Path.Combine(dirAt, "sound_log.txt"), "times are unscaled game time; the log's frame 0 = " + sound0.ToString("F3") + "\n" + OzSoundLog.Format(OzSoundLog.Since(sound0))); } catch (Exception) { }
                try { File.WriteAllText(Path.Combine(dirAt, "hand_back_state.txt"), OzRedBriefingRecorder.HandBackState("after the blue take") + "\n" + OzL18BriefingStage.Status() + "\n" + (OzBlueCameo.I && OzBlueCameo.I.Stage ? OzBlueCameo.I.Stage.L18Status() : "no blue stage") + "\n"); } catch (Exception) { }
                Debug.Log("[OzBriefingFrameLog] " + n + " frames logged into " + dirAt);
                Destroy(gameObject);
            }
        }
        void OnDestroy() { if (w != null) { w.Close(); w = null; } }
    }

    /// <summary>The kit's skip matrix (README Unity pass step 6), real time: from the main menu, each case deploys level 18 on its side, waits for
    /// the briefing's clock to reach its second, skips it as the SPACE key does, waits for the hand-back and writes the hand-back state;
    /// "restart" reloads the level after the case before it and skips again; "retry" reloads it as after a loss. Report: skip_matrix.txt.</summary>
    public sealed class OzL18SkipMatrix : MonoBehaviour
    {
        public static string Report = "";
        public static string OutDir = "";
        readonly StringBuilder sb = new StringBuilder();
        static readonly (string side, float at, string extra)[] Cases =
        {
            ("blue", 3.0f, ""), ("blue", 6.51f, ""), ("blue", 7.8f, ""), ("blue", 9.6f, ""), ("blue", 11.5f, ""), ("blue", 14.5f, ""), ("blue", 17.8f, ""), ("blue", 4.0f, "restart"), ("blue", 4.0f, "retry"),
            ("blue", 99f, "full"), ("red", 99f, "full"),
            ("red", -0.5f, ""), ("red", 2.0f, ""), ("red", 5.0f, ""), ("red", 8.0f, ""), ("red", 11.0f, ""), ("red", 14.5f, ""), ("red", 17.5f, ""), ("red", 4.0f, "restart"), ("red", 4.0f, "retry"),
        };
        public static void Begin()
        {
            var old = FindFirstObjectByType<OzL18SkipMatrix>(); if (old) Destroy(old.gameObject);
            var go = new GameObject("OzL18SkipMatrix"); DontDestroyOnLoad(go); var s = go.AddComponent<OzL18SkipMatrix>(); s.StartCoroutine(s.Run());
        }
        void Log(string s) { sb.AppendLine(s); Debug.Log("[OzL18SkipMatrix] " + s); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static bool Active(bool blue) => blue ? OzBlueCameo.BriefingActive : ZodBriefing.BriefingActive;
        static float BriefT(bool blue) => blue ? OzBlueCameo.LastSceneT : ZodBriefing.DebugLineT;
        static bool Skip(bool blue) => blue ? OzBlueCameo.ProofSkipBriefing() : ZodBriefing.ProofSkipBriefing();
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        IEnumerator Run()
        {
            if (OutDir.Length == 0) OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/tmp/l18_brief_rec")).Replace('\\', '/');
            Directory.CreateDirectory(OutDir);
            bool boxWas = OzPlayBlue.Box;
            Log("skip matrix, level 18, " + Cases.Length + " cases, real time; shuttle row: " + "level 18 has no optional shuttle row in this project yet: shuttle ON not run");
            for (int i = 0; i < Cases.Length; i++)
            {
                var c = Cases[i]; bool blue = c.side == "blue";
                if (c.extra == "" || c.extra == "full")
                {
                    if (!FindFirstObjectByType<OpenZMainMenu>()) { OzScenes.MainMenu(); float tm = Time.realtimeSinceStartup; while (!FindFirstObjectByType<OpenZMainMenu>() && Time.realtimeSinceStartup - tm < 30f) yield return null; yield return Real(2.0f); }
                    var m = FindFirstObjectByType<OpenZMainMenu>(); if (!m) { Log($"case {i + 1}: no main menu, matrix stopped"); break; }
                    int card = Array.FindIndex(OzLevelDef.All, d => d.Id == "L18");
                    OzPlayBlue.Box = blue;
                    yield return Real(0.8f); m.ProofSelect(); yield return Real(0.8f); m.ProofFocus(card); yield return Real(0.6f); m.ProofDeploy();
                }
                else if (c.extra == "restart") OzScenes.Restart();
                else OzScenes.RestartAfterLoss();
                float t0 = Time.realtimeSinceStartup;
                yield return Real(1.0f);
                while ((!ZGame.I || ZGame.I.World == null) && Time.realtimeSinceStartup - t0 < 90f) yield return null;
                if (!ZGame.I || ZGame.I.level != "L18") { Log($"case {i + 1} {c.side} {c.extra}: level 18 did not load"); continue; }
                float tb = Time.realtimeSinceStartup; while (!Active(blue) && Time.realtimeSinceStartup - tb < 15f) yield return null;
                bool ran = Active(blue); float maxT = -99f;   // maxT: the last clock second the briefing reached (a "full" case never skips)
                if (ran) { float ta = Time.realtimeSinceStartup; while (Active(blue) && BriefT(blue) < c.at && Time.realtimeSinceStartup - ta < 30f) { maxT = Mathf.Max(maxT, BriefT(blue)); yield return null; } }
                float at = BriefT(blue); bool sk = ran && Active(blue) && Skip(blue);
                float th = Time.realtimeSinceStartup; while ((Active(blue) || Flying) && Time.realtimeSinceStartup - th < 12f) yield return null;
                float hand = Time.realtimeSinceStartup - th;
                yield return Real(1.5f);
                Log($"case {i + 1} {c.side}{(c.extra.Length > 0 ? " " + c.extra : "")} SPACE wanted at {c.at:F2} s: briefing ran {ran}, skipped {sk} at {at:F2} s, hand-back {hand:F2} s later, last briefing clock {maxT:F2} s; HUD {(OpenZHud.I && OpenZHud.I.enabled)}, sim {(ZGame.I && ZGame.I.enabled)}; " + OzRedBriefingRecorder.HandBackState("1.5 s after"));
            }
            OzPlayBlue.Box = boxWas;
            Report = sb.ToString();
            try { File.WriteAllText(Path.Combine(OutDir, "skip_matrix_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), Report); } catch (Exception) { }
            Debug.Log("[OzL18SkipMatrix] DONE " + Cases.Length + " cases");
            Destroy(gameObject);
        }
    }
}
#endif
