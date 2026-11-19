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
    /// 1.5 kit ue_explosions_6: the proof recorder of the six test explosions (editor only). Play, the Unit editor open, then the menu:
    /// it reads the take list Logs/ue_blasts_takes.txt and records every take frame-locked (30 frames a second, the game's own sound
    /// rendered beside the frames), from the normal RTS camera or a free one, by day or night, on an empty field or on a group of units.
    /// Line 1 = the output folder (with a trailing slash). Every further line is one take:
    ///   name | frames before the first kept frame | kept frames | night 0/1 | floor style | camera | units | events | mode
    ///   camera: rts:height:dx:dz  or  look:x:y:z:tx:ty:tz (metres from the field's middle)
    ///   units:  id,R or B or N,dx,dz,count[,health left]; ...   events: frame,B,type 0..5,dx,dz; ...  or  frame,C,height,dx,dz,frames
    ///           (C: the RTS camera glides to that height and focus over that many frames)
    ///   mode:   empty = frames + sound; perf = nothing saved, the real frame times are measured (frame cap lifted for the take)
    /// Per take: f_00001.jpg ..., take.wav, soundlog.txt, health.csv, done.txt; log.txt + done.txt in the output folder. A take list
    /// already started (started.txt in the output folder) is never run twice.
    /// </summary>
    public static class OzV15UeBlastsRec
    {
        const string Tag = "[OpenZ UeBlasts]";
        const string ListPath = "Logs/ue_blasts_takes.txt";

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Record takes (Play, the unit editor open)")]
        static void Record()
        {
            var g = ZGame.I; var ed = OzUnitEditor.I;
            if (!Application.isPlaying || !g || !ed || ed.World == null || ed.World.Map == null) { Debug.LogWarning($"{Tag} enter Play and open the Unit editor first"); return; }
            if (!File.Exists(ListPath)) { Debug.LogWarning($"{Tag} no take list at {ListPath}"); return; }
            var lines = File.ReadAllLines(ListPath).Where(s => s.Trim().Length > 0 && !s.StartsWith("#")).ToArray();
            if (lines.Length < 2) { Debug.LogWarning($"{Tag} the take list is empty"); return; }
            string root = lines[0].Trim();
            if (File.Exists(root + "started.txt")) { Debug.LogWarning($"{Tag} this take list was already started ({root})"); return; }
            Directory.CreateDirectory(root); File.WriteAllText(root + "started.txt", "x");
            g.StartCoroutine(Run(g, ed, root, lines));
            Debug.Log($"{Tag} recording {lines.Length - 1} take(s) into {root}");
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static IEnumerator Run(ZGame g, OzUnitEditor ed, string root, string[] L)
        {
            var w = ed.World; var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main;
            var log = new StringBuilder(); var ic = CultureInfo.InvariantCulture;
            Vector3 C = ZGame.PxToWorld(w.Map.width * 8, w.Map.height * 8); C.y = 0f; int style0 = OzUeBackdrop.Style; float h0 = rts.height; Vector3 focus0 = rts.focus;
            var fxz = OpenZFxPlayer.I;
            var hide = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && (c.name == "OpenZ_HUD" || c.name == "OzUe_EyeCanvas" || c.name == "OpenZ_SoftCursor" || c.name == "OpenZ_UnitOverlay") && c.enabled).ToList();
            var pin = Object.FindFirstObjectByType<PlayerInput>(); var hovP = typeof(PlayerInput).GetProperty("Hover");
            var uef = Object.FindFirstObjectByType<OzUeField>(); var pickHov = typeof(OzUePick).GetProperty("Hover");
            var marksT = typeof(OzUnitEditor).Assembly.GetType("OpenZ.OzUeMarks");
            var marks = marksT != null ? Resources.FindObjectsOfTypeAll(marksT).Cast<Behaviour>().ToList() : new List<Behaviour>();
            void Quiet()
            {
                foreach (var c in hide) if (c) c.enabled = false;
                foreach (var mk in marks) if (mk) mk.enabled = false;
                if (pin) { pin.enabled = false; if (hovP != null) hovP.SetValue(pin, null, null); pin.Selection.Clear(); }
                if (uef) uef.enabled = false;
                if (pickHov != null) pickHov.SetValue(null, null, null);
                if (OzUePick.Inspected != null || OzUePick.Kind != UeInspect.None) OzUePick.ClearInspect();
                if (OzUePick.Picked.Count > 0) OzUePick.Unpick();
            }
            OzUnitEditor.SetPanelsHidden(true);
            const int FPS = 30; int rate = AudioSettings.outputSampleRate; const int ch = 2; var eof = new WaitForEndOfFrame();
            int vs0 = QualitySettings.vSyncCount, tf0 = Application.targetFrameRate;

            for (int ci = 1; ci < L.Length; ci++)
            {
                var sh = L[ci].Split('|'); string nm = sh[0].Trim(); int pre = int.Parse(sh[1]), N = int.Parse(sh[2]); string dir = root + nm + "\\";
                bool perf = sh.Length > 8 && sh[8].Trim() == "perf";
                Directory.CreateDirectory(dir);
                var units = new List<ZObject>(); var hp0 = new List<int>(); string[] evs = new string[0];
                try
                {
                    OzUePlayground.ClearField(); OzUeNight.Set(sh[3].Trim() == "1"); OzUeBackdrop.SetStyle(int.Parse(sh[4]));
                    var cc = sh[5].Trim().Split(':');
                    if (cc[0] == "rts") { rts.enabled = true; rts.focus = C + new Vector3(F(cc[2]), 0f, F(cc[3])); rts.SetHeightImmediate(F(cc[1])); }
                    else { rts.enabled = false; cam.transform.position = C + new Vector3(F(cc[1]), F(cc[2]), F(cc[3])); cam.transform.LookAt(C + new Vector3(F(cc[4]), F(cc[5]), F(cc[6]))); }
                    foreach (var us in sh[6].Split(';'))
                    {
                        var u = us.Trim().Split(','); if (u.Length < 5) continue;
                        var d = OzUePlayground.Drop(u[0], u[1] == "R" ? Team.Red : u[1] == "N" ? Team.Neutral : Team.Blue, C + new Vector3(F(u[2]), 0f, F(u[3])), int.Parse(u[4]));
                        if (d.ok && d.made != null) foreach (var o in d.made) { o.Damage = 0; if (u.Length > 5) o.Health = Mathf.Min(o.Health, int.Parse(u[5])); units.Add(o); hp0.Add(o.Health); }
                        else log.Append(nm + " drop " + u[0] + " REFUSED " + d.why + "\n");
                    }
                    evs = sh.Length > 7 ? sh[7].Split(';') : new string[0];
                }
                catch (System.Exception ex) { log.Append(nm + " setup " + ex + "\n"); }

                var samples = new List<float>(perf ? 16 : N * rate / FPS * ch + 4096); float peak = 0f, st0 = 0f; int peakP = 0; bool audioOn = false;
                var times = new List<float>(perf ? N : 0);
                int glide0 = 0, glideN = 0; Vector3 glideA = rts.focus, glideB = rts.focus; float glideHa = rts.height, glideHb = rts.height;
                if (perf) { Time.captureFramerate = 0; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120; }
                else Time.captureFramerate = FPS;
                for (int f0 = 0; ; f0++)
                {
                    Quiet();
                    int f = f0 - pre;
                    if (f == 0) { st0 = Time.unscaledTime; if (!perf) audioOn = AudioRenderer.Start(); }
                    if (f > 0)
                    {
                        if (perf) times.Add(Time.unscaledDeltaTime * 1000f);
                        else
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
                        }
                        int lp = fxz ? fxz.UeParticles("ueb_") : 0; if (lp > peakP) peakP = lp;
                    }
                    if (glideN > 0 && rts.enabled)
                    {
                        float gk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - glide0) / (float)glideN));
                        rts.focus = Vector3.Lerp(glideA, glideB, gk); rts.SetHeightImmediate(Mathf.Lerp(glideHa, glideHb, gk));
                        if (f - glide0 >= glideN) glideN = 0;
                    }
                    foreach (var es in evs)
                    {
                        var e = es.Trim().Split(','); if (e.Length < 5 || int.Parse(e[0]) != f) continue;
                        if (e[1] == "C" && e.Length > 5) { glide0 = f; glideN = Mathf.Max(1, int.Parse(e[5])); glideA = rts.focus; glideHa = rts.height; glideB = C + new Vector3(F(e[3]), 0f, F(e[4])); glideHb = F(e[2]); continue; }
                        try { if (e[1] == "B") { bool ok = OzUeBlasts.TestDrop(int.Parse(e[2]), C.x + F(e[3]), C.z + F(e[4])); log.Append(nm + " f" + f + " blast " + e[2] + " ok=" + ok + "\n"); } }
                        catch (System.Exception ex) { log.Append(nm + " ev " + f + " " + ex.Message + "\n"); }
                    }
                    if (f >= N) break;
                    yield return eof;
                }
                if (audioOn) AudioRenderer.Stop();
                if (perf)
                {
                    QualitySettings.vSyncCount = vs0; Application.targetFrameRate = tf0;
                    times.Sort(); float sum = 0f; foreach (var x in times) sum += x;
                    log.Append(nm + " perf: frames " + times.Count + ", avg " + (sum / Mathf.Max(1, times.Count)).ToString("0.0", ic) + " ms, p95 " + (times.Count > 0 ? times[(int)(times.Count * 0.95f)] : 0f).ToString("0.0", ic)
                        + " ms, worst " + (times.Count > 0 ? times[times.Count - 1] : 0f).ToString("0.0", ic) + " ms, screen " + Screen.width + " x " + Screen.height + "\n");
                }
                else
                {
                    using (var fs = new FileStream(dir + "take.wav", FileMode.Create)) using (var bw = new BinaryWriter(fs))
                    {
                        int bytes = samples.Count * 4;
                        bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch);
                        bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32); bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
                        foreach (var s in samples) bw.Write(s);
                    }
                    var snd = OzSoundLog.Since(st0).Select(e => e.t.ToString("0.00") + " " + e.key + " " + e.outcome).ToArray();
                    File.WriteAllText(dir + "soundlog.txt", string.Join("\n", snd));
                }
                var hs = new StringBuilder();
                for (int k = 0; k < units.Count; k++)
                {
                    var o = units[k]; var p = ZGame.PxToWorld(o.CenterX, o.CenterY);
                    hs.Append(o.Type + "," + o.Owner + "," + hp0[k] + "," + o.Health + "," + new Vector2(p.x - C.x, p.z - C.z).magnitude.ToString("0.0", ic) + "\n");
                }
                File.WriteAllText(dir + "health.csv", "type,team,hp_before,hp_after,dist_m_from_C\n" + hs);
                log.Append(nm + " end: peak live ueb particles " + peakP + " | " + OzUeBlasts.Report() + "\n");
                File.WriteAllText(dir + "done.txt", "frames " + N + " fps " + FPS + " samples " + samples.Count + " peak " + peak.ToString("0.000", ic));
                File.WriteAllText(root + "log.txt", log.ToString());
            }
            Time.captureFramerate = 0;
            OzUePlayground.ClearField(); rts.enabled = true; rts.focus = focus0; rts.SetHeightImmediate(h0); OzUeNight.Set(false); OzUeBackdrop.SetStyle(style0);
            foreach (var c in hide) if (c) c.enabled = true;
            foreach (var mk in marks) if (mk) mk.enabled = true;
            if (pin) pin.enabled = true;
            if (uef) uef.enabled = true;
            OzUnitEditor.SetPanelsHidden(false);
            File.WriteAllText(root + "log.txt", log.ToString()); File.WriteAllText(root + "done.txt", "ok");
            Debug.Log($"{Tag} takes recorded: {root}");
        }
    }
}
