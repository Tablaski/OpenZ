// OpenZ (GPL-3.0) - version 1.2 Play proofs of the blue cutscenes (kit k75b; editor only, menu OpenZ/V1.2/Blue Cutscenes: 75b ...).
// Started on the MainMenu scene in Play it deploys level 02 as the player with the blue side, lets the blue briefing run (or skips it),
// then stages the ending the job asks for (OpenZVictory.DebugFortFalls: the real chain: strike -> nuke scene -> bomber -> blast -> the
// win / lose scene -> end screen), counts the checks, takes the pictures of the key moments and, in the Record jobs, writes the MP4s
// (1920 x 1080, 30 fps, the game's sound + the voices: frames + AudioRenderer in step, ffmpeg joins) into blue_team/videos/.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzBlueCutsceneProof : MonoBehaviour
    {
        public enum Job { Briefing, Win, Lose, RecordBriefing, RecordWin, RecordLose }
        public static string Report = "";
        public static string Level = "L02";
        public static int Fps = 30, Jpg = 92;
        OzBriefingRecTimes lt;   // 1.5: the level's own row (picture times, scene watch, folders, clip name); null = level 2's values below, unchanged
        Job job; string dir; readonly StringBuilder sb = new StringBuilder(); int passes, fails;
        bool boxWas, boxChanged, recording, audioStarted; string recDir; int frameNo, renderFails, holdFrames, rate, ch, per; readonly List<float> samples = new List<float>(); float peak;
        readonly List<(float t, string name)> wantShots = new List<(float, string)>(); int shotI; string shotScene = ""; readonly Dictionary<string, float> marks = new Dictionary<string, float>();
        float impactSeen = -100f; int holdSamples, holdLapses;
        int endFrames, endWindowFrames, endScreenFrames, gunFrames, crashFrames; float tossBeforeImpact = -1f; bool tossSeen;

        public static void Start(Job j)
        {
            var old = FindFirstObjectByType<OzBlueCutsceneProof>(); if (old) Destroy(old.gameObject);
            var go = new GameObject("OzBlueCutsceneProof"); DontDestroyOnLoad(go);
            var p = go.AddComponent<OzBlueCutsceneProof>(); p.job = j; p.StartCoroutine(p.Run());
        }

        static string Staging()
        {
            string p = Application.dataPath.Replace('\\', '/');
            int i = p.IndexOf("/projects/", StringComparison.Ordinal);
            return i > 0 ? p.Substring(0, i) + "/work/staging/v1_2_20261022" : "";
        }
        void Log(string s) { sb.AppendLine(s); Debug.Log("[OzBlueCutsceneProof] " + s); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }

        IEnumerator Run()
        {
            lt = OzBriefingRecTimes.Blue(Level);
            dir = lt != null ? lt.NewFramesDir(job.ToString()) : Path.Combine(Application.dataPath, "../../../../work/records/milestones/blue_cutscenes_k75b/" + job).Replace('\\', '/');
            Directory.CreateDirectory(dir);
            var m = FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Check("the MainMenu scene is in Play", false); Finish(); yield break; }
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == Level);
            if (card < 0) { Check("level " + Level + " is a level of the game", false); Finish(); yield break; }
            boxWas = OzPlayBlue.Box; if (!boxWas) { OzPlayBlue.Box = true; boxChanged = true; }
            // DEPLOY works only on the battlefield screen with the level's card in focus (OpenZMainMenu.Deploy): as the k75c flyover proof
            yield return Real(0.8f);
            m.ProofSelect(); yield return Real(0.8f);
            m.ProofFocus(card); yield return Real(0.6f);
            bool record = job == Job.RecordBriefing || job == Job.RecordWin || job == Job.RecordLose;
            // the briefing starts on the level's first frame by itself: its take runs from the menu on (cut later by the scene's own marks)
            if (job == Job.RecordBriefing) yield return StartRecording("briefing");
            m.ProofDeploy();
            float t0 = Time.realtimeSinceStartup;
            while (!ZGame.I && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            while (ZGame.I && ZGame.I.World == null && Time.realtimeSinceStartup - t0 < 90f) yield return null;
            Check("the level " + Level + " deployed as blue (human " + (ZGame.I ? ZGame.I.player.ToString() : "none") + ", PLAY AS BLUE " + OzPlayBlue.Active + ")", ZGame.I && OzPlayBlue.Active && ZGame.I.level == Level);
            float tb = Time.realtimeSinceStartup;
            while (!OzBlueCameo.I && Time.realtimeSinceStartup - tb < 5f) yield return null;
            Check("the blue director is installed (ZodBriefing absent)", OzBlueCameo.I && !ZodBriefing.I);
            if (job == Job.Briefing || job == Job.RecordBriefing)
            {
                if (lt != null) WantShots("briefing", lt.Shots); else
                WantShots("briefing", (-0.5f, "opening"), (0.6f, "sidekick_asks"), (3.9f, "evilzod_fist_up"), (5.9f, "sidekick_scared"), (9.0f, "evilzod_points_board"), (11.4f, "now_move"), (12.5f, "sidekick_leaves"));
                yield return WatchScene("briefing", lt != null ? lt.WatchSeconds : 30f);
                Check("the blue briefing played (BriefingsPlayed " + OzBlueCameo.BriefingsPlayed + ")", OzBlueCameo.BriefingsPlayed >= 1);
                Check("the start lines were held through the whole briefing (" + holdLapses + " of " + holdSamples + " briefing frames without AudioDirector.HoldStartLines)", holdSamples > 30 && holdLapses == 0);
                float th = Clock;
                while (OzBlueCameo.BriefingActive && Clock - th < 6f) yield return null;
                Check("the level was handed back (sim running, HUD visible)", !OzBlueCameo.BriefingActive && ZGame.I && ZGame.I.enabled && OpenZHud.I && OpenZHud.I.enabled);
                Check("the start lines were released at the hand-over (AudioDirector.HoldStartLines answers false)", AudioDirector.HoldStartLines != null && !AudioDirector.HoldStartLines());
                if (record)
                {
                    float te = Clock; while (Clock - te < 2.5f) yield return null;
                    StopRecording(lt != null ? lt.ClipName : "cutscene_briefing");
                }
            }
            else
            {
                float ts = Time.realtimeSinceStartup;
                while (OzBlueCameo.BriefingActive && Time.realtimeSinceStartup - ts < 1f) yield return null;
                if (OzBlueCameo.BriefingActive) { OzBlueCameo.ProofSkipBriefing(); while (OzBlueCameo.BriefingActive && Time.realtimeSinceStartup - ts < 8f) yield return null; }
                yield return new WaitForSecondsRealtime(1.5f);
                bool won = job == Job.Win || job == Job.RecordWin;
                if (record) yield return StartRecording(won ? "win_nuke" : "lose");
                int n = OpenZVictory.DebugFortFalls(won);
                Check((won ? "the red" : "our") + " fort falls (" + n + " parts)", n > 0);
                if (won)
                {
                    WantShots("nuke", (0.3f, "behold"), (1.6f, "doors_open_bomb"), (4.3f, "where_switch"), (6.4f, "sets_button"), (8.9f, "wind_up"), (9.7f, "press"));
                    yield return WatchScene("nuke", 25f);
                    Check("the nuke scene played and the fist pressed the button (presses " + OzBlueCameo.NukePresses + ")", OzBlueCameo.NukeScenesPlayed >= 1 && OzBlueCameo.NukePresses >= 1);
                    Check("the strike launched on the press (bomb " + OpenZNukeStrike.BombColour + ")", OpenZNukeStrike.Active || OpenZNukeStrike.LastImpactTime > 0f);
                    Check("the bomb is the blue one", OpenZNukeStrike.BombColour == "blue");
                    Check("the World-Domination-inator launched in the HQ on the press (" + OzEvilStage.BombLaunches + " launch(es))", OzEvilStage.BombLaunches >= 1);
                }
                string sc = won ? "win" : "lose";
                if (won) WantShots("ending", (0.5f, "fort_is_ours"), (4.2f, "arms_wide"), (5.5f, "domination_crash"), (7.2f, "how_many_steps"), (9.4f, "at_your_speed"), (10.9f, "rust_shrug"), (12.8f, "salute_move"), (14.6f, "sidekick_leaves"), (15.4f, "evilzod_alone"));
                else WantShots("ending", (0.4f, "mission_failed"), (3.0f, "send_everyone"), (4.9f, "scream_crash"), (7.5f, "nobody_left"), (9.6f, "nobody_left_2"), (11.75f, "fine_gun"), (12.5f, "pump_back"), (13.5f, "pump_done"));
                yield return WatchScene("ending", 60f);
                Check("the blue " + sc + " ending played (EndingsPlayed " + OzBlueCameo.EndingsPlayed + ")", OzBlueCameo.EndingsPlayed >= 1);
                Check("the ending played in the corner comms window, never full screen (" + endWindowFrames + " window frames, " + endScreenFrames + " full-screen frames of " + endFrames + ")", endFrames > 30 && endWindowFrames > endFrames * 0.9f && endScreenFrames == 0);
                Check("the crash zoom ran in the ending (" + crashFrames + " frames zoomed in)", crashFrames > 5);
                if (!won) Check("the shotgun in EvilZod's hands from the cut on 'Fine' (" + gunFrames + " frames; LOSE only)", gunFrames > 20);
                if (won) Check("the red fort's gun stayed on the fort until the bomb landed (HoldFortToss " + (tossSeen ? "released " + tossBeforeImpact.ToString("F2") + " s before the impact" : "held to the impact") + ")", !tossSeen || tossBeforeImpact <= 0.05f);
                if (won) Check("the red fort's standing gun was blown off before the scene's first line (" + FortGunView.OutroBlastReport() + ")", FortGunView.OutroBlastOk);   // 1.5 outro_megacannon_blast
                float te = Clock;
                while (OpenZVictory.HoldEndScreen && Clock - te < 20f) yield return null;
                Check("the end screen came after the scene", !OpenZVictory.HoldEndScreen);
                if (record) { float tr = Clock; while (Clock - tr < 1.5f) yield return null; StopRecording(won ? "cutscene_win_nuke" : "cutscene_lose", won ? "cutscene_win" : null); }
            }
            Log(OzBlueCameo.Status());
            Finish();
        }

        /// <summary>
        /// go while the strike is still in the air is remembered (seconds before the impact, from the strike's own countdown).</summary>
        void TossWatch()
        {
            if (tossSeen || !OpenZEnding.Running) return;
            if (!OpenZEnding.HoldFortToss && OpenZNukeStrike.Active && OpenZNukeStrike.SecondsToImpact > 0.05f) { tossSeen = true; tossBeforeImpact = OpenZNukeStrike.SecondsToImpact; Log("FORT GUN let go " + tossBeforeImpact.ToString("F2") + " s before the impact"); }
        }
        void Update() { if (!recording) TossWatch(); }

        /// <summary>The proof's clock: while a take records, the take's own time (frames / Fps: the game then runs at a fixed step, slower
        /// than real time because every frame is written, so real-time limits and marks would cut the scenes); real time otherwise.</summary>
        float Clock => recording ? (frameNo + holdFrames) / (float)Fps : Time.realtimeSinceStartup;   // a held frame (no fixed step) still counts: no wait can hang
        /// <summary>A mark for the cut: the take's time of the next written frame while recording, else the proof's clock.</summary>
        float Mark => recording ? frameNo / (float)Fps : Clock;
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        void WantShots(string scene, params (float, string)[] shots) { wantShots.Clear(); wantShots.AddRange(shots); shotI = 0; shotScene = scene; }

        /// <summary>Waits for the scene kind to start and end; takes the wanted pictures at their scene times; remembers its start and end
        /// on the proof's clock (the take's time while recording: the MP4s are cut by these marks).</summary>
        IEnumerator WatchScene(string kind, float maxWait)
        {
            float t0 = Clock;
            while ((OzBlueCameo.LastSceneKind != kind || !OzBlueCameo.SceneShowing) && Clock - t0 < maxWait) yield return null;
            bool started = OzBlueCameo.LastSceneKind == kind && OzBlueCameo.SceneShowing;
            Check("scene " + kind + " started", started);
            if (!started) yield break;
            marks[kind + "_start"] = Mark;
            float t1 = Clock;
            while (OzBlueCameo.SceneShowing && OzBlueCameo.LastSceneKind == kind && Clock - t1 < maxWait)
            {
                if (kind == "briefing" && OzBlueCameo.BriefingActive) { holdSamples++; if (AudioDirector.HoldStartLines == null || !AudioDirector.HoldStartLines()) holdLapses++; }
                var stg = OzBlueCameo.I ? OzBlueCameo.I.Stage : null;
                if (stg && stg.Live) { if (kind == "ending") { endFrames++; if (stg.OnScreen) endScreenFrames++; else endWindowFrames++; if (stg.GunInHand) gunFrames++; } if (stg.CamZoom < 0.9f || stg.CamName == "ez_face" || stg.CamName == "ez_gunclose") crashFrames++; }   // the crash zooms: the fov punch or the push onto his whole head
                float st = OzBlueCameo.LastSceneT;
                while (shotI < wantShots.Count && st >= wantShots[shotI].t) { yield return new WaitForEndOfFrame(); Shot(shotScene + "_" + wantShots[shotI].name); shotI++; }
                yield return null;
            }
            marks[kind + "_end"] = Mark;
            Log($"scene {kind}: {marks[kind + "_end"] - marks[kind + "_start"]:F1} s ({(recording ? "take time" : "real time")}); " + (OzBlueCameo.I ? OzBlueCameo.I.StageStatus : ""));
        }

        void Shot(string name)
        {
            string path = Path.Combine(dir, name + ".png");
            try { var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex); Log("picture " + path); }
            catch (Exception e) { Log("picture failed: " + e.Message); }
        }

        // ================================================================================================ recording (the k75c flyover kit's recipe)
        IEnumerator StartRecording(string label)
        {
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ blue cutscene 1920x1080", out var info); Log("Game view: " + info);
            float tw = Time.realtimeSinceStartup;
            while ((Screen.width != 1920 || Screen.height != 1080) && Time.realtimeSinceStartup - tw < 6f) yield return null;
            Check("the Game view is 1920 x 1080 for the take (it is " + Screen.width + " x " + Screen.height + ")", Screen.width == 1920 && Screen.height == 1080);
            recDir = Path.Combine(dir, "take_frames_" + label); Directory.CreateDirectory(recDir);
            foreach (var f in Directory.GetFiles(recDir, "*.jpg")) File.Delete(f);
            rate = AudioSettings.outputSampleRate; ch = Channels(AudioSettings.speakerMode); per = rate / Fps;
            samples.Clear(); frameNo = 0; holdFrames = 0; renderFails = 0; peak = 0f;
            Time.captureFramerate = Fps; audioStarted = AudioRenderer.Start();
            Check("the sound capture started (" + rate + " Hz, " + ch + " channels)", audioStarted);
            recording = true; impactSeen = OpenZNukeStrike.LastImpactTime;
            StartCoroutine(Grabber());
            yield return null;
        }
        static int Channels(AudioSpeakerMode m)
        {
            switch (m) { case AudioSpeakerMode.Mono: return 1; case AudioSpeakerMode.Quad: return 4; case AudioSpeakerMode.Surround: return 5; case AudioSpeakerMode.Mode5point1: return 6; case AudioSpeakerMode.Mode7point1: return 8; default: return 2; }
        }
        IEnumerator Grabber()
        {
            while (recording)
            {
                yield return new WaitForEndOfFrame();
                if (!recording) break;
                if (Time.captureFramerate <= 0) { holdFrames++; continue; }
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(recDir, $"f_{frameNo:00000}.jpg"), tex.EncodeToJPG(Jpg)); Destroy(tex);
                if (audioStarted)
                {
                    int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                    using (var nb = new NativeArray<float>(Mathf.Max(0, avail) * ch, Allocator.Temp))
                    {
                        if (!AudioRenderer.Render(nb)) renderFails++;
                        for (int k = 0; k < nb.Length; k++) { float s = nb[k]; samples.Add(s); if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s); }
                    }
                }
                if (OpenZNukeStrike.LastImpactTime > impactSeen + 0.01f) { impactSeen = OpenZNukeStrike.LastImpactTime; marks["impact"] = frameNo / (float)Fps; }   // the bomb's impact on the take's clock
                TossWatch();
                frameNo++;
            }
        }
        void StopRecording(string name, string splitName = null)
        {
            recording = false;
            if (audioStarted) { AudioRenderer.Stop(); audioStarted = false; }
            Time.captureFramerate = 0;
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
            Log($"take: {frameNo} frames at {Fps} ({frameNo / (float)Fps:F1} s), sound {wav} (peak {peak:F3}, render fails {renderFails}, held {holdFrames})");
            Check("the take has frames and sound", frameNo > Fps * 5 && peak > 0.01f && renderFails == 0);
            string st = Staging(); string outDir = lt != null ? lt.ClipDir : st.Length > 0 ? st + "/blue_team/videos" : dir; Directory.CreateDirectory(outDir);
            string ff = FfmpegExe();
            if (ff == null) { Check("ffmpeg found (join by hand: python <kit>/tools/mux_mp4.py " + recDir + " " + wav + " <out.mp4>)", false); return; }
            string full = Path.Combine(dir, name + "_full.mp4").Replace('\\', '/');
            bool ok = Ffmpeg(ff, $"-y -framerate {Fps} -i \"{recDir.Replace('\\', '/')}/f_%05d.jpg\" -i \"{wav.Replace('\\', '/')}\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -vf scale=1920:1080:flags=lanczos -c:a aac -b:a 192k -movflags +faststart -shortest \"{full}\"", 300000) && File.Exists(full);
            Check("the take joined: " + full, ok);
            if (!ok) return;
            // the clips: each scene from its start mark minus 0.5 s to its end mark plus its tail (the whole take when no mark): the briefing
            // keeps 2.5 s for the hand-back and the first start line, the nuke scene runs on through the bomber to 2 s after the blue bomb's
            string first = splitName != null ? "nuke" : (name.Contains("briefing") ? "briefing" : "ending");
            Cut(ff, full, outDir, name, first);
            if (splitName != null) Cut(ff, full, outDir, splitName, "ending");
        }
        void Cut(string ff, string full, string outDir, string name, string kind)
        {
            string mp4 = Path.Combine(outDir, name + ".mp4").Replace('\\', '/'), sheet = Path.Combine(outDir, name + "_sheet.jpg").Replace('\\', '/');
            string range = "";
            if (marks.TryGetValue(kind + "_start", out var a) && marks.TryGetValue(kind + "_end", out var b))
            {
                float to = b + (kind == "briefing" ? (lt != null ? lt.TailSeconds : 2.5f) : 1.0f);
                if (kind == "nuke" && marks.TryGetValue("impact", out var hit) && hit > b) { to = hit + 2.0f; if (marks.TryGetValue("ending_start", out var e0) && e0 > hit) to = Mathf.Min(to, e0); }
                var inv = System.Globalization.CultureInfo.InvariantCulture;   // ffmpeg reads a dot, whatever the system's number format
                range = "-ss " + Mathf.Max(0f, a - 0.5f).ToString("F2", inv) + " -to " + to.ToString("F2", inv) + " ";
            }
            bool ok = Ffmpeg(ff, $"-y {range}-i \"{full}\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart \"{mp4}\"", 300000) && File.Exists(mp4) && new FileInfo(mp4).Length > 50000;
            if (ok) Ffmpeg(ff, $"-y -i \"{mp4}\" -vf \"fps=2,scale=320:-1,tile=6x7\" -frames:v 1 -q:v 3 \"{sheet}\"", 120000);
            Check("the MP4 is written " + mp4 + (File.Exists(sheet) ? " (frame sheet " + sheet + ")" : ""), ok);
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
            string head = $"{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks ({job}); pictures and reports in {dir}";
            Report = head + "\n" + sb;
            try { File.WriteAllText(Path.Combine(dir, "report_" + job + ".txt"), Report); } catch (Exception) { }
            Debug.Log("[OzBlueCutsceneProof] " + head);
            Destroy(gameObject);
        }

        /// <summary>A job ended early (an error, Play stopped): the fixed step and the sound capture never stay on.</summary>
        void OnDestroy()
        {
            recording = false;
            if (audioStarted) { AudioRenderer.Stop(); audioStarted = false; }
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
        }

        // ================================================================================================ the counted check (no Play)
        public static string CountedCheck()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void C(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); }
            var lib = OzBlueScenes.Shared;
            C("blue_scenes.json readable (" + lib.Problem + ")", lib.Ok);
            foreach (var id in new[] { OzBlueScenes.Briefing, OzBlueScenes.Win, OzBlueScenes.Lose, OzBlueScenes.WinNuke })
            {
                bool ready = lib.Ready(id, out var why); C("scene " + id + " ready (" + (why ?? "ok") + ")", ready);
                var s = lib.Scene(id); if (s == null) continue;
                var clip = lib.Clip(s);
                C(id + ": voice length " + (clip ? clip.length.ToString("F2") : "none") + " matches the data " + s.length.ToString("F2"), clip && Mathf.Abs(clip.length - s.length) < 0.06f);
                bool ordered = true; for (int i = 1; i < s.beats.Length; i++) if (s.beats[i].t < s.beats[i - 1].t) ordered = false;
                C(id + ": " + s.beats.Length + " beats in order, " + s.lines.Length + " lines, " + (s.words != null ? s.words.Length : 0) + " words", ordered && s.lines.Length >= 3);
                bool inside = true; foreach (var l in s.lines) if (l.start < 0f || l.end > s.length + 0.5f || l.end <= l.start) inside = false;
                C(id + ": every line inside the take", inside);
                int ezB = OzBlueScenes.CountBeats(s, "ez"), skB = OzBlueScenes.CountBeats(s, "sk"), camB = OzBlueScenes.CountBeats(s, "cam");
                C(id + ": EvilZod " + ezB + ", sidekick " + skB + ", camera " + camB + " beats (each > 0)", ezB > 0 && skB > 0 && camB > 0);
                if (id == OzBlueScenes.WinNuke) { float press = -1f; foreach (var b in s.beats) if (b.beat == "press") press = b.t; C("nuke: the press beat lands the fist on 'fireworks' (" + (press + OzEvilStage.PressHit).ToString("F2") + " s)", press > 8.5f && press < 10f); }
                if (id == OzBlueScenes.Lose)
                {
                    bool gunReady = false, pump = false, old = false; foreach (var b in s.beats) { if (b.beat == "gun_ready" && b.t >= 11.3f && b.t <= 11.7f) gunReady = true; if (b.beat == "pump_gun" && b.t > 11.62f && b.t + OzEvilStage.PumpClackA < 13.0f) pump = true; if (b.beat == "to_rack" || b.beat == "grab_gun" || (b.beat == "walk_off" && b.who == "ez")) old = true; }
                    C("lose: the gun in his hands from the cut on 'Fine', one pump on the line (clack " + OzEvilStage.PumpClackA.ToString("F2") + " s after its beat), no walk / rack / grab", gunReady && pump && !old);
                }
                { int crash = 0; foreach (var b in s.beats) if (b.who == "cam" && (b.beat == "crash" || b.beat == "ez_face" || b.beat == "ez_gunclose")) crash++; C(id + ": " + crash + " crash zoom(s) (the fov punch or the push onto his whole head)", crash > 0); }
            }
            foreach (var r in new[] { "EvilZod", "Sidekick", "EvilZod@Walk", "EvilZod@Fist", "EvilZod@GripShotgun", "EvilZod@GripPump", "EvilZod@HandsOpen", "Sidekick@Walk" })
                C("model / take " + r + " in Resources/" + OzEvilStage.Folder, Resources.Load<GameObject>(OzEvilStage.Folder + r) != null);
            foreach (var r in new[] { "M_EvilZod_Body", "M_Shotgun", "M_Shotgun_Rail", "M_Sidekick_Body", "M_Sidekick_Eye", "M_Sidekick_Pupil" })
            { var mt = Resources.Load<Material>(OzEvilStage.Folder + r); C("material " + r + (mt ? " (" + mt.shader.name + ")" : ""), mt && mt.shader && mt.shader.name == "Universal Render Pipeline/Lit"); }
            C("the banner and the emblem pictures", Resources.Load<Texture2D>(OzEvilStage.SetFolder + "evil_banner") && Resources.Load<Texture2D>(OzEvilStage.SetFolder + "evil_emblem"));
            C("the pump sound", Resources.Load<AudioClip>(OzEvilStage.SfxFolder + "blue_shotgun_pump") != null);
            C("the blue nuke lead " + OzBlueCameo.NukeLeadSeconds.ToString("F2") + " s is under the intro's wait " + OzBlueCameo.NukeMaxSeconds.ToString("F2"), OzBlueCameo.NukeLeadSeconds < OzBlueCameo.NukeMaxSeconds);
            string head = $"{(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} (blue cutscenes k75b counted check)";
            string txt = head + "\n" + sb;
            try { string st = Staging(); if (st.Length > 0) { string d = Path.Combine(Application.dataPath, "../../../../work/records/milestones/blue_cutscenes_k75b"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "counted_check.txt"), txt); } } catch (Exception) { }
            Debug.Log("[OzBlueCutsceneProof] " + txt);
            return txt;
        }
    }
}
#endif
