using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_explosions_6: film takes of the six test explosions in a real level (editor only). Play a level, then the menu: the
    /// level's opening is skipped as SPACE does, both computer players rest, and every take of the list Logs/ue_blasts_film.txt is
    /// recorded frame-locked (30 frames a second, the game's own sound rendered beside the frames) from a camera on a slow arc.
    /// Line 1 = the output folder (with a trailing slash). Every further line is one take:
    ///   name | frames before the first kept frame | kept frames | light (DayNight.timeOfDay, -1 = as it is) | dust (a Unit editor floor style: its dust colours) | camera | vehicles | events
    ///   camera:   tx:ty:tz:distance:pitch:yaw:yaw rate (the point looked at, metres from it, degrees down, degrees round, degrees a second)
    ///   vehicles: id,R or B or N,x,z[,health left]; ...   (world metres; stood there by the rules' own CreateObject)
    ///   events:   frame,B,type 0..5,x,z   a test explosion  |  frame,T,tx,ty,tz,distance   the camera's new aim and distance (eased)
    /// Per take: f_00001.jpg ..., take.wav, health.csv, done.txt; log.txt + done.txt in the output folder. A take list already started
    /// (started.txt in the output folder) is never run twice. The music is muted for the takes (this Play session only).
    /// </summary>
    public static class OzV15UeBlastsFilm
    {
        const string Tag = "[OpenZ UeBlasts film]";
        const string ListPath = "Logs/ue_blasts_film.txt";

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Film takes (Play, a level)")]
        static void Film()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || OzUnitEditor.I) { Debug.LogWarning($"{Tag} enter Play in a level first"); return; }
            if (!File.Exists(ListPath)) { Debug.LogWarning($"{Tag} no take list at {ListPath}"); return; }
            var lines = File.ReadAllLines(ListPath).Where(s => s.Trim().Length > 0 && !s.StartsWith("#")).ToArray();
            if (lines.Length < 2) { Debug.LogWarning($"{Tag} the take list is empty"); return; }
            string root = lines[0].Trim();
            if (File.Exists(root + "started.txt")) { Debug.LogWarning($"{Tag} this take list was already started ({root})"); return; }
            Directory.CreateDirectory(root); File.WriteAllText(root + "started.txt", "x");
            g.StartCoroutine(Run(g, root, lines));
            Debug.Log($"{Tag} filming {lines.Length - 1} take(s) into {root}");
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        static IEnumerator Run(ZGame g, string root, string[] L)
        {
            var log = new StringBuilder(); var ic = CultureInfo.InvariantCulture;
            float w0 = Time.realtimeSinceStartup;
            while (g.World == null && Time.realtimeSinceStartup - w0 < 90f) yield return null;
            yield return new WaitForSecondsRealtime(2.5f);
            for (int i = 0; i < 40; i++)
            {   // the level's opening, skipped as the SPACE key does
                bool a = false;
                try { a = ZodBriefing.ProofSkipBriefing() || OzBlueCameo.ProofSkipBriefing(); } catch (System.Exception) { }
                if (a) break;
                if (!ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && !Flying && i > 6) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            w0 = Time.realtimeSinceStartup; while (Flying && Time.realtimeSinceStartup - w0 < 60f) yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(2f);

            var w = g.World; var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main; var dn = Object.FindFirstObjectByType<DayNight>();
            var fxz = OpenZFxPlayer.I;
            var hide = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace).ToList();
            var pin = Object.FindFirstObjectByType<PlayerInput>(); var hovP = typeof(PlayerInput).GetProperty("Hover");
            var music = new List<AudioSource>(); int musicAt = -1000;
            void Quiet()
            {
                if (Time.frameCount - musicAt > 60)
                {   // the music voices (made as they are needed): muted for the takes of this Play session, no setting is touched
                    musicAt = Time.frameCount;
                    foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (s && s.gameObject.name.StartsWith("Music") && !music.Contains(s)) music.Add(s);
                }
                foreach (var s in music) if (s) s.mute = true;
                foreach (var c in hide) if (c) c.enabled = false;
                if (pin) { pin.enabled = false; if (hovP != null) hovP.SetValue(pin, null, null); pin.Selection.Clear(); }
            }
            bool bb = g.blueBot, rb = g.redBot; g.blueBot = false; g.redBot = false;
            float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running; int style0 = OzUeBackdrop.Style;
            float h0 = rts ? rts.height : 70f; Vector3 focus0 = rts ? rts.focus : Vector3.zero;
            const int FPS = 30; int rate = AudioSettings.outputSampleRate; const int ch = 2; var eof = new WaitForEndOfFrame();

            for (int ci = 1; ci < L.Length; ci++)
            {
                var sh = L[ci].Split('|'); string nm = sh[0].Trim(); int pre = int.Parse(sh[1]), N = int.Parse(sh[2]); string dir = root + nm + "\\";
                Directory.CreateDirectory(dir);
                var units = new List<ZObject>(); var hp0 = new List<int>(); string[] evs = new string[0];
                Vector3 tgt = Vector3.zero, goalT = Vector3.zero, velT = Vector3.zero; float dist = 60f, goalD = 60f, velD = 0f, pitch = 35f, yaw = 0f, yawRate = 0f;
                try
                {
                    float tod = F(sh[3]);
                    if (dn && tod >= 0f) { dn.running = false; dn.timeOfDay = tod; dn.Apply(tod); }
                    OzUeBackdrop.SetStyle(int.Parse(sh[4]));
                    var cc = sh[5].Trim().Split(':');
                    tgt = goalT = new Vector3(F(cc[0]), F(cc[1]), F(cc[2])); dist = goalD = F(cc[3]); pitch = F(cc[4]); yaw = F(cc[5]); yawRate = F(cc[6]);
                    if (rts) rts.enabled = false;
                    foreach (var us in sh[6].Split(';'))
                    {
                        var u = us.Trim().Split(','); if (u.Length < 4) continue;
                        var px = ZGame.WorldToPx(new Vector3(F(u[2]), 0f, F(u[3]))); int tx = (px.x >> 4) - 1, ty = (px.y >> 4) - 1;
                        var o = w.CreateObject("vehicle", u[0], tx * 16, ty * 16, u[1] == "R" ? Team.Red : u[1] == "N" ? Team.Neutral : Team.Blue);
                        if (o == null) { log.Append(nm + " vehicle " + u[0] + " not made\n"); continue; }
                        g.SpawnUnitView(o);
                        if (u.Length > 4) o.Health = Mathf.Min(o.Health, int.Parse(u[4]));
                        units.Add(o); hp0.Add(o.Health);
                        log.Append(nm + " vehicle " + u[0] + " health " + o.Health + " of " + o.MaxHealth + "\n");
                    }
                    evs = sh.Length > 7 ? sh[7].Split(';') : new string[0];
                }
                catch (System.Exception ex) { log.Append(nm + " setup " + ex + "\n"); }

                var samples = new List<float>(N * rate / FPS * ch + 4096); float peak = 0f; int peakP = 0; bool audioOn = false;
                Time.captureFramerate = FPS;
                for (int f0 = 0; ; f0++)
                {
                    Quiet();
                    int f = f0 - pre;
                    if (f == 0) audioOn = AudioRenderer.Start();
                    if (f > 0)
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        if (tex)
                        {
                            var raw = tex.GetRawTextureData(); int twd = tex.width, th = tex.height; var fmt = tex.graphicsFormat; string path = dir + string.Format("f_{0:00000}.jpg", f);
                            Object.Destroy(tex);
                            System.Threading.Tasks.Task.Run(() => File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)twd, (uint)th, 0, 92)));
                        }
                        if (audioOn)
                        {
                            int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                            var nb = new Unity.Collections.NativeArray<float>(Mathf.Max(0, avail) * ch, Unity.Collections.Allocator.Temp);
                            AudioRenderer.Render(nb);
                            for (int k = 0; k < nb.Length; k++) { samples.Add(nb[k]); if (Mathf.Abs(nb[k]) > peak) peak = Mathf.Abs(nb[k]); }
                            nb.Dispose();
                        }
                        int lp = fxz ? fxz.UeParticles("ueb_") : 0; if (lp > peakP) peakP = lp;
                    }
                    foreach (var es in evs)
                    {
                        var e = es.Trim().Split(','); if (e.Length < 5 || int.Parse(e[0]) != f) continue;
                        try
                        {
                            if (e[1] == "B") { bool ok = OzUeBlasts.Drop(g, w, int.Parse(e[2]), new Vector3(F(e[3]), 0f, F(e[4])), out string why); log.Append(nm + " f" + f + " blast " + e[2] + " ok=" + ok + " " + why + "\n"); }
                            else if (e[1] == "T" && e.Length > 5) { goalT = new Vector3(F(e[2]), F(e[3]), F(e[4])); goalD = F(e[5]); }
                        }
                        catch (System.Exception ex) { log.Append(nm + " ev " + f + " " + ex.Message + "\n"); }
                    }
                    // the camera of the next picture: its aim and distance ease to their goals, it turns slowly round the aim
                    tgt = Vector3.SmoothDamp(tgt, goalT, ref velT, 0.7f, Mathf.Infinity, 1f / FPS);
                    dist = Mathf.SmoothDamp(dist, goalD, ref velD, 1.6f, Mathf.Infinity, 1f / FPS);
                    yaw += yawRate / FPS;
                    if (cam)
                    {
                        var rot = Quaternion.Euler(pitch, yaw, 0f);
                        cam.transform.SetPositionAndRotation(tgt - rot * Vector3.forward * dist, rot);
                    }
                    if (rts) rts.focus = new Vector3(tgt.x, 0f, tgt.z);                                  // the game's ear stands over the RTS focus: it goes with the film camera's aim
                    if (f >= N) break;
                    yield return eof;
                }
                if (audioOn) AudioRenderer.Stop();
                using (var fs = new FileStream(dir + "take.wav", FileMode.Create)) using (var bw = new BinaryWriter(fs))
                {
                    int bytes = samples.Count * 4;
                    bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch);
                    bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32); bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
                    foreach (var s in samples) bw.Write(s);
                }
                var hs = new StringBuilder();
                for (int k = 0; k < units.Count; k++) hs.Append(units[k].Type + "," + units[k].Owner + "," + hp0[k] + "," + units[k].Health + "\n");
                File.WriteAllText(dir + "health.csv", "type,team,hp_before,hp_after\n" + hs);
                log.Append(nm + " end: peak live ueb particles " + peakP + " | " + OzUeBlasts.Report() + "\n");
                File.WriteAllText(dir + "done.txt", "frames " + N + " fps " + FPS + " samples " + samples.Count + " peak " + peak.ToString("0.000", ic));
                File.WriteAllText(root + "log.txt", log.ToString());
                Time.captureFramerate = 0;
                yield return new WaitForSecondsRealtime(1.5f);
            }
            Time.captureFramerate = 0;
            g.blueBot = bb; g.redBot = rb; OzUeBackdrop.SetStyle(style0);
            if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
            if (rts) { rts.enabled = true; rts.focus = focus0; rts.SetHeightImmediate(h0); }
            foreach (var c in hide) if (c) c.enabled = true;
            foreach (var s in music) if (s) s.mute = false;
            if (pin) pin.enabled = true;
            File.WriteAllText(root + "log.txt", log.ToString()); File.WriteAllText(root + "done.txt", "ok");
            Debug.Log($"{Tag} takes filmed: {root}");
        }
    }
}
