// OpenZ (GPL-3.0)
// In Play from the MAIN MENU: Deploy() puts level 02 on as blue (the briefing skipped or kept); then Shoot(kind, label, times) plays one
// scene through the real director (OzBlueCameo.DebugBriefing / DebugNuke / DebugEnding) and writes, at each scene time, the screen
// (1920 x 1080) and the raw comms feed (the window's picture before the window's own finish) as PNGs, plus a per-frame motion log of the
// Nothing here runs in a build.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzBlueLab : MonoBehaviour
    {
        public static string Status = "idle";
        /// <summary>Shoot: the raw feed rendered this many times larger for the stills (1 = as in the game).</summary>
        public static int FeedScale = 1;
        public static string OutRoot
        {
            get
            {
                string p = Application.dataPath.Replace('\\', '/');
                int i = p.IndexOf("/projects/", StringComparison.Ordinal);
                return (i > 0 ? p.Substring(0, i) : p) + "/work/staging/v1_2_20261022/k75b_blue_cutscenes/proof/redo";
            }
        }
        static OzBlueLab Ensure()
        {
            var l = FindFirstObjectByType<OzBlueLab>();
            if (!l) { var go = new GameObject("OzBlueLab"); DontDestroyOnLoad(go); l = go.AddComponent<OzBlueLab>(); }
            return l;
        }

        /// <summary>From the main menu in Play: level 02 (or `level`) as blue; skip = the briefing is skipped at once.</summary>
        public static string Deploy(bool skip, string level = "L02")
        {
            var l = Ensure(); l.StopAllCoroutines(); l.StartCoroutine(l.DeployRun(skip, level)); return "deploying " + level;
        }
        IEnumerator DeployRun(bool skip, string level)
        {
            Status = "deploy: start";
            var m = FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Status = "deploy: no main menu in this scene"; yield break; }
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == level);
            OzPlayBlue.Box = true;
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ blue cutscene 1920x1080", out _);
            yield return Real(0.8f); m.ProofSelect(); yield return Real(0.8f); m.ProofFocus(card); yield return Real(0.6f);
            m.ProofDeploy();
            float t0 = Time.realtimeSinceStartup;
            while ((!ZGame.I || ZGame.I.World == null) && Time.realtimeSinceStartup - t0 < 90f) yield return null;
            while (!OzBlueCameo.I && Time.realtimeSinceStartup - t0 < 95f) yield return null;
            if (skip)
            {
                float ts = Time.realtimeSinceStartup;
                while (!OzBlueCameo.BriefingActive && Time.realtimeSinceStartup - ts < 2f) yield return null;
                if (OzBlueCameo.BriefingActive) OzBlueCameo.ProofSkipBriefing();
                while (OzBlueCameo.BriefingActive && Time.realtimeSinceStartup - ts < 8f) yield return null;
            }
            OzPlayBlue.Box = false;                                                                      // the menu's side box back off (the level keeps its side)
            Status = "deploy: done " + (ZGame.I ? ZGame.I.level + " " + ZGame.I.player : "NO LEVEL") + ", director " + (OzBlueCameo.I ? "yes" : "NO") + ", " + Screen.width + "x" + Screen.height;
        }

        /// <summary>Plays one scene (briefing / nuke / win / lose) and writes the stills at the scene times and the motion log.</summary>
        public static string Shoot(string kind, string label, float[] times, bool stills = true)
        {
            if (!OzBlueCameo.I) return "no blue director in this scene";
            var l = Ensure(); l.StopAllCoroutines(); l.StartCoroutine(l.ShootRun(kind, label, times ?? new float[0], stills)); return "shooting " + kind;
        }
        IEnumerator ShootRun(string kind, string label, float[] times, bool stills)
        {
            string dir = OutRoot + "/" + label; Directory.CreateDirectory(dir);
            Status = "shoot " + kind + ": starting";
            string r = kind == "briefing" ? OzBlueCameo.DebugBriefing() : kind == "nuke" ? OzBlueCameo.DebugNuke() : OzBlueCameo.DebugEnding(kind == "win");
            string want = kind == "briefing" ? "briefing" : kind == "nuke" ? "nuke" : "ending";
            float t0 = Time.realtimeSinceStartup;
            while ((OzBlueCameo.LastSceneKind != want || !OzBlueCameo.SceneShowing) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            if (!OzBlueCameo.SceneShowing) { Status = "shoot " + kind + ": the scene did not start (" + r + ")"; yield break; }
            {   // the hand checks: the raw feed rendered FeedScale x larger (the window shows it scaled down; the stills show the hands in detail)
                var st0 = OzBlueCameo.I ? OzBlueCameo.I.Stage : null;
                if (FeedScale > 1 && st0 && st0.Feed && !st0.OnScreen) st0.EnsureFeed(st0.Feed.width * FeedScale, st0.Feed.height * FeedScale);
            }
            var csv = new StringBuilder("t,clock,dt,cam,ezHx,ezHy,ezHz,ezRx,ezRy,ezRz,ezLx,ezLy,ezLz,ezCx,ezCy,ezCz,ezHfx,ezHfy,ezHfz,skHx,skHy,skHz,skRx,skRy,skRz,skLx,skLy,skLz,camx,camy,camz,camfx,camfy,camfz,fov,ezFaceCam\n");
            var inv = CultureInfo.InvariantCulture; int si = 0, shots = 0; float lastClock = -1f;
            Array.Sort(times);
            while (OzBlueCameo.SceneShowing)
            {
                yield return new WaitForEndOfFrame();
                var st = OzBlueCameo.I ? OzBlueCameo.I.Stage : null;
                float t = OzBlueCameo.LastSceneT;
                if (st)
                {
                    var ez = st.EzRig; var sk = st.SkRig; var cam = st.StageCam;
                    float clock = st.Clock;
                    csv.Append(t.ToString("F4", inv)).Append(',').Append(clock.ToString("F4", inv)).Append(',').Append((lastClock < 0f ? 0f : clock - lastClock).ToString("F4", inv)).Append(',').Append(st.CamName);
                    lastClock = clock;
                    void P(Vector3 v) { csv.Append(',').Append(v.x.ToString("F4", inv)).Append(',').Append(v.y.ToString("F4", inv)).Append(',').Append(v.z.ToString("F4", inv)); }
                    P(ez != null && ez.Head ? ez.Head.position : Vector3.zero); P(ez != null ? ez.HandR.position : Vector3.zero); P(ez != null ? ez.HandL.position : Vector3.zero);
                    P(ez != null && ez.Spine2 ? ez.Spine2.position : Vector3.zero); P(ez != null && ez.Head ? ez.Head.position + ez.Head.forward * 0.3f : Vector3.zero);
                    P(sk != null && sk.Head ? sk.Head.position : Vector3.zero); P(sk != null ? sk.HandR.position : Vector3.zero); P(sk != null ? sk.HandL.position : Vector3.zero);
                    P(cam ? cam.transform.position : Vector3.zero); P(cam ? cam.transform.position + cam.transform.forward : Vector3.zero);
                    csv.Append(',').Append(cam ? cam.fieldOfView.ToString("F3", inv) : "0");
                    float fc = -1f;                                                                      // his face's turn away from the lens (deg, on the ground plane)
                    if (ez != null && ez.HeadFront && cam && ez.Root.gameObject.activeInHierarchy)
                    {
                        Vector3 hf = ez.HeadFront.position - ez.Head.position, tc = cam.transform.position - ez.Head.position; hf.y = 0f; tc.y = 0f;
                        if (hf.sqrMagnitude > 1e-6f && tc.sqrMagnitude > 1e-6f) fc = Vector3.Angle(hf, tc);
                    }
                    csv.Append(',').Append(fc.ToString("F1", inv)).Append('\n');
                }
                while (stills && si < times.Length && t >= times[si])
                {
                    string nm = label + "_" + times[si].ToString("00.00", inv);
                    try
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(dir, nm + "_screen.png"), tex.EncodeToPNG()); Destroy(tex);
                        if (st && st.Feed && !st.OnScreen)
                        {
                            var prev = RenderTexture.active; var rt = st.Feed;
                            var tmp = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                            Graphics.Blit(rt, tmp); RenderTexture.active = tmp;
                            var t2 = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); t2.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t2.Apply();
                            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
                            File.WriteAllBytes(Path.Combine(dir, nm + "_feed.png"), t2.EncodeToPNG()); Destroy(t2);
                        }
                        shots++;
                    }
                    catch (Exception e) { Debug.LogWarning("[OzBlueLab] still failed: " + e.Message); }
                    si++;
                }
                Status = "shoot " + kind + ": t " + t.ToString("F2", inv) + ", stills " + shots;
                yield return null;
            }
            File.WriteAllText(Path.Combine(dir, label + "_motion.csv"), csv.ToString());
            Status = "shoot " + kind + ": DONE, stills " + shots + " in " + dir + "; " + (OzBlueCameo.I ? OzBlueCameo.I.StageStatus : "");
        }

        /// <summary>Plays a scene to scene time t, then holds it there (AudioListener.pause: the director and the stage freeze) for the
        /// lighting and framing work; Grab writes the picture now; Release lets it run on.</summary>
        public static string HoldAt(string kind, float t)
        {
            if (!OzBlueCameo.I) return "no blue director in this scene";
            var l = Ensure(); l.StopAllCoroutines(); AudioListener.pause = false; l.StartCoroutine(l.HoldRun(kind, t)); return "holding " + kind + " at " + t;
        }
        IEnumerator HoldRun(string kind, float at)
        {
            string r = kind == "briefing" ? OzBlueCameo.DebugBriefing() : kind == "nuke" ? OzBlueCameo.DebugNuke() : OzBlueCameo.DebugEnding(kind == "win");
            string want = kind == "briefing" ? "briefing" : kind == "nuke" ? "nuke" : "ending";
            float t0 = Time.realtimeSinceStartup;
            while ((OzBlueCameo.LastSceneKind != want || !OzBlueCameo.SceneShowing) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            while (OzBlueCameo.SceneShowing && OzBlueCameo.LastSceneT < at) { Status = "hold " + kind + ": t " + OzBlueCameo.LastSceneT.ToString("F2"); yield return null; }
            AudioListener.pause = true;
            Status = "hold " + kind + ": HELD at " + OzBlueCameo.LastSceneT.ToString("F2") + " (" + r + ")";
        }
        public static string Release() { AudioListener.pause = false; return "released"; }
        public static string Grab(string label)
        {
            var l = Ensure(); l.StartCoroutine(l.GrabRun(label)); return "grabbing " + label;
        }
        IEnumerator GrabRun(string label)
        {
            yield return null; yield return new WaitForEndOfFrame();
            string dir = OutRoot + "/grabs"; Directory.CreateDirectory(dir);
            var st = OzBlueCameo.I ? OzBlueCameo.I.Stage : null;
            var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(dir, label + "_screen.png"), tex.EncodeToPNG()); Destroy(tex);
            if (st && st.Feed && !st.OnScreen)
            {
                var prev = RenderTexture.active; var rt = st.Feed;
                var tmp = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rt, tmp); RenderTexture.active = tmp;
                var t2 = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); t2.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t2.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
                File.WriteAllBytes(Path.Combine(dir, label + "_feed.png"), t2.EncodeToPNG()); Destroy(t2);
            }
            Status = "grabbed " + label + " in " + dir;
        }

        // ================================================================================================ the battlefield card's video
        /// <summary>From the MAIN MENU in Play: the blue battlefield screen, the level's card focused, EvilZod's card open; a take at a fixed
        /// 30 fps (frames + the mixed sound) with the given line of his (by id) from 1 s in, to 1.5 s after it; joined by ffmpeg into
        /// blue_team/videos/outName.mp4 + _sheet.jpg. The side box is put back off.</summary>
        public static string RecordCard(string level, string lineId, string outName)
        {
            var l = Ensure(); l.StopAllCoroutines(); l.StartCoroutine(l.CardRun(level, lineId, outName)); return "recording the card " + level + " " + lineId;
        }
        IEnumerator CardRun(string level, string lineId, string outName)
        {
            Status = "card: start";
            var m = FindFirstObjectByType<OpenZMainMenu>();
            OzEvilCardLine line = null; foreach (var x in OzEvilCardLines.All) if (x.Id == lineId) line = x;
            if (!m || line == null) { Status = "card: no main menu / no line " + lineId; yield break; }
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == level);
            OzPlayBlue.Box = true;
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ evil card 1920x1080", out _);
            yield return Real(0.8f); m.ProofSelect(); yield return Real(1.0f); m.ProofFocus(card);
            float t0 = Time.realtimeSinceStartup;
            while (!ZodBriefing.EvilCardShown && Time.realtimeSinceStartup - t0 < 6f) yield return null;
            yield return Real(1.5f);                                                                   // the card open, the menu settled
            string root = OutRoot + "/card_" + outName, frames = root + "/frames"; Directory.CreateDirectory(frames);
            foreach (var f in Directory.GetFiles(frames, "*.jpg")) File.Delete(f);
            int rate = AudioSettings.outputSampleRate, ch = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2, fps = 30, n = 0;
            var samples = new List<float>();
            Time.captureFramerate = fps; bool audio = AudioRenderer.Start(); bool started = false, ended = false; int endAt = -1;
            var start = typeof(ZodBriefing).GetMethod("EvilLineStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            while (true)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(frames, "f_" + n.ToString("00000") + ".jpg"), tex.EncodeToJPG(92)); Destroy(tex);
                if (audio)
                {
                    int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                    using (var nb = new Unity.Collections.NativeArray<float>(Mathf.Max(0, avail) * ch, Unity.Collections.Allocator.Temp)) { AudioRenderer.Render(nb); for (int k = 0; k < nb.Length; k++) samples.Add(nb[k]); }
                }
                n++;
                if (!started && n >= fps) { started = true; if (ZodBriefing.I && start != null) start.Invoke(ZodBriefing.I, new object[] { line }); }
                if (started && !ended && n > fps + 10 && !ZodBriefing.EvilLineOn) { ended = true; endAt = n; }
                if (ended && n >= endAt + (int)(1.5f * fps)) break;
                if (n > fps * 20) break;
                Status = "card: frame " + n + ", line on " + ZodBriefing.EvilLineOn + ", camera " + ZodBriefing.EvilCardCamera;
            }
            if (audio) AudioRenderer.Stop();
            Time.captureFramerate = 0;
            string wav = root + "/take.wav"; int per = rate / fps, want = n * per * ch;
            while (samples.Count < want) samples.Add(0f);
            if (samples.Count > want) samples.RemoveRange(want, samples.Count - want);
            using (var fs = new FileStream(wav, FileMode.Create)) using (var bw = new BinaryWriter(fs))
            {
                int bytes = samples.Count * 4;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch); bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes); foreach (var s in samples) bw.Write(s);
            }
            OzPlayBlue.Box = false;
            string vids = OutRoot.Replace("/k75b_blue_cutscenes/proof/redo", "/blue_team/videos");
            string mp4 = vids + "/" + outName + ".mp4", sheet = vids + "/" + outName + "_sheet.jpg";
            bool ok = Ff("-y -framerate " + fps + " -i \"" + frames + "/f_%05d.jpg\" -i \"" + wav + "\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart -shortest \"" + mp4 + "\"");
            if (ok) Ff("-y -i \"" + mp4 + "\" -vf \"fps=2,scale=320:-1,tile=6x4\" -frames:v 1 -q:v 3 \"" + sheet + "\"");
            Status = "card: DONE " + (ok ? mp4 : "ffmpeg FAILED") + ", " + n + " frames, line " + lineId + (OzEvilCardLines.StaysOnHim(line) ? " (stays on him)" : " (back to the two-shot)");
        }
        static bool Ff(string args)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            try { using (var p = System.Diagnostics.Process.Start(psi)) { p.StandardError.ReadToEnd(); p.WaitForExit(120000); return p.ExitCode == 0; } }
            catch (Exception e) { Debug.LogWarning("[OzBlueLab] ffmpeg: " + e.Message); return false; }
        }

        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
    }
}
#endif
