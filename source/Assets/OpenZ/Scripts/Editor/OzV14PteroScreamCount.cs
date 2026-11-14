#if !OPENZ_HARNESS
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted checks. View only, no rule. One line per fact, the first line PASS n/n.
    /// Headless, in edit mode (menu OpenZ/V1.4/Ptero Scream: Count (headless)), nothing saved, nothing left in the scene:
    ///  L  the three screams as a build loads them: the sound table's events amb_l07_dragon_attack_1/2/3 (bus, range, level, one clip each), each clip
    ///     through Resources (as AudioDirector.Clip does): mono (Force To Mono), 48 kHz, 1.0 s, Decompress On Load, Normalize off in its .meta; its decoded
    ///     RMS within 1 dB of the averaged mono of the owner's stereo file (the channels averaged as the levels assume; a summed downmix is +6 dB) and its
    ///     decoded peak after its own gain at -1 dBFS or lower; the old scream's decoded RMS says whether Unity's Normalize lifted it (+3.2 dB), the level
    ///     the three are matched to;
    ///  P  the picker, 300 picks: never the same scream twice in a row, all three used;
    ///  V  on screen = inside the camera view (a camera made for the count);
    ///  A  attacks through AmbientLife's own scream code (AmbientLife.DragonScream, the AudioEvent the director plays): on screen at the lunge one scream
    ///     at once, on screen only later one scream then, off screen the whole pass none (not even after it), no camera none.
    /// In Play on level 7 (menu OpenZ/V1.4/Ptero Scream: Play Check (Play, level 7)): three attacks by the dragons' own pass (AmbientLife.ProofPass) at a
    /// grunt at the player's camera height, each with one scream heard (event, clip, level, distance logged) and no call or old scream at its start,
    /// </summary>
    public static class OzV14PteroScreamCount
    {
        const string Tag = "[OpenZ V1.4 ptero scream] ";
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        static string F(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        static string Report() => (fail == 0 ? "PASS " + pass + "/" + pass : "FAIL " + fail + " of " + (pass + fail)) + "\n" + sb;

        /// <summary>The kit's levels (tools/levels.py, proof/proof_levels.txt): each clip's gain, the events' volume, the RMS of the averaged mono of each
        /// owner file before its gain (dBFS; RMS moves under 0.5 dB through the Vorbis encode, sample peaks up to 0.9 dB), the old scream's RMS as stored,
        /// the lift its Normalize is assumed to give, and the events' volume that would match it without that lift.</summary>
        public static readonly float[] Gains = { 0.4f, 1.0f, 1.9f };
        public const float VolDb = -7.3f;   // E1 (6 Nov 2026): the editor measured no Normalize lift on the old scream (-0.2 dB), so the three rows went -4.1 -> -7.3 (VolDbNoLift)
        static readonly float[] MonoRmsDb = { -17.09f, -17.57f, -18.05f };
        const float OldRmsDb = -17.23f, OldLiftDb = 3.2f, VolDbLift = -4.1f, VolDbNoLift = -7.3f;

        /// <summary>The clip as the game plays it, decoded: its sample peak (linear) and RMS (dBFS) over all its samples.</summary>
        static bool Decode(AudioClip clip, out float peak, out float rmsDb)
        {
            peak = 0f; rmsDb = -999f;
            if (!clip || !(clip.LoadAudioData() || clip.loadState == AudioDataLoadState.Loaded)) return false;
            var s = new float[clip.samples * clip.channels];
            if (s.Length == 0 || !clip.GetData(s, 0)) return false;
            double sq = 0.0;
            foreach (var x in s) { peak = Mathf.Max(peak, Mathf.Abs(x)); sq += (double)x * x; }
            rmsDb = sq > 0.0 ? (float)(10.0 * System.Math.Log10(sq / s.Length)) : -999f;
            return peak > 0f;
        }

        [System.Serializable] class ClipRef { public string res; public float gainDb; public bool standin; }
        [System.Serializable] class Ev { public string id, bus; public bool is3D, loop, inViewOnly; public float volDb, minDist, maxDist; public ClipRef[] clips; }
        [System.Serializable] class Table { public Ev[] events; }

        /// <summary>The headless count; returns the report.</summary>
        public static string Run()
        {
            pass = fail = 0; sb = new StringBuilder();
            Clips();
            Picker();
            Camera c = null; GameObject cgo = null, lgo = null;
            try
            {
                cgo = new GameObject("oz_v14_scream_cam") { hideFlags = HideFlags.HideAndDontSave };
                c = cgo.AddComponent<Camera>(); c.enabled = false;
                c.transform.position = new Vector3(0f, 70f, -40f); c.transform.LookAt(Vector3.zero); c.fieldOfView = 40f; c.aspect = 16f / 9f;
                Check(OzDragonScream.OnScreen(c, Vector3.zero) && OzDragonScream.OnScreen(c, new Vector3(0f, 15f, 0f)), "V  the middle of the view and 15 m over it: on screen");
                Check(!OzDragonScream.OnScreen(c, new Vector3(500f, 0f, 0f)) && !OzDragonScream.OnScreen(c, new Vector3(0f, 70f, -200f)) && !OzDragonScream.OnScreen(null, Vector3.zero),
                    "V  500 m to the side, behind the lens, no camera: off screen");
                if (Application.isPlaying) Info("A  skipped in Play (it adds a scene object): run it in edit mode; in Play use the Play Check");
                else { lgo = new GameObject("oz_v14_scream_life") { hideFlags = HideFlags.HideAndDontSave }; Attacks(lgo.AddComponent<AmbientLife>(), c); }
            }
            finally
            {
                if (lgo) Object.DestroyImmediate(lgo);
                if (cgo) Object.DestroyImmediate(cgo);
                OzDragonScream.Reset();
            }
            return Report();
        }

        static void Clips()
        {
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            Check(ta, "L  the sound table " + AudioDirector.TablePath + " loads");
            if (!ta) return;
            var t = JsonUtility.FromJson<Table>(ta.text);
            for (int i = 0; i < OzDragonScream.Ids.Length; i++)
            {
                string id = OzDragonScream.Ids[i], res = OzDragonScream.Clips[i];
                var evs = t.events.Where(x => x.id == id).ToArray();
                Check(evs.Length == 1, "L  " + id + ": " + evs.Length + " row(s) in the sound table");
                if (evs.Length != 1) continue;
                var e = evs[0];
                Check(e.bus == "Ambience" && e.is3D && e.inViewOnly && !e.loop && Mathf.Approximately(e.minDist, 40f) && Mathf.Approximately(e.maxDist, 320f) && Mathf.Abs(e.volDb - VolDb) < 1e-4f,
                    "L  " + id + ": " + e.bus + ", 3D " + e.is3D + ", in view only " + e.inViewOnly + ", " + F(e.minDist, "0") + "-" + F(e.maxDist, "0") + " m, volDb " + F(e.volDb, "0.0") + " (want " + F(VolDb, "0.0") + ": heard as the old scream)");
                Check(e.clips != null && e.clips.Length == 1 && e.clips[0].res == res && Mathf.Abs(e.clips[0].gainDb - Gains[i]) < 1e-4f && !e.clips[0].standin,
                    "L  " + id + ": one clip " + (e.clips != null && e.clips.Length > 0 ? e.clips[0].res + " gainDb " + F(e.clips[0].gainDb, "+0.0;-0.0") : "none") + " (want " + res + " " + F(Gains[i], "+0.0") + ")");
                var clip = Resources.Load<AudioClip>(res);
                Check(clip, "L  " + res + " loads through Resources, as the director loads it");
                if (!clip) continue;
                Check(clip.channels == 1 && clip.frequency == 48000 && Mathf.Abs(clip.length - 1f) < 0.02f && clip.loadType == AudioClipLoadType.DecompressOnLoad,
                    "L  " + clip.name + ": " + clip.channels + " channel(s), " + clip.frequency + " Hz, " + F(clip.length, "0.000") + " s, " + clip.loadType + " (want mono 48000 Hz 1.0 s DecompressOnLoad)");
                var path = AssetDatabase.GetAssetPath(clip);
                var meta = File.Exists(path + ".meta") ? File.ReadAllText(path + ".meta") : "";
                Check(Regex.IsMatch(meta, @"^\s*forceToMono: 1\s*$", RegexOptions.Multiline) && Regex.IsMatch(meta, @"^\s*normalize: 0\s*$", RegexOptions.Multiline),
                    "L  " + clip.name + ".meta: Force To Mono on, Normalize off (the project's rule for mono clips: the level is the sound table's)");
                bool dec = Decode(clip, out float peak, out float rms);
                float db = dec ? 20f * Mathf.Log10(peak) : -999f, after = db + Gains[i];
                Check(dec && Mathf.Abs(rms - MonoRmsDb[i]) <= 1f,
                    "L  " + clip.name + ": decoded RMS " + F(rms) + " dBFS, the averaged mono of the owner's file " + F(MonoRmsDb[i]) + " (within 1.0; summed channels would be +6)");
                Check(dec && after <= -1f,
                    "L  " + clip.name + ": decoded peak " + F(db) + " dBFS, after its gain " + F(Gains[i], "+0.0") + " at " + F(after) + " dBFS (-1 or lower)");
            }
            // The level the three are matched to: the old scream as heard. Its source is mono already, its .meta has Normalize on; Unity documents Normalize
            // for the Force To Mono mixdown, so whether it lifted this clip (+3.2 dB to full scale) is read here from the decoded clip's RMS.
            var old = Resources.Load<AudioClip>("OpenZ/Audio/Ambience/sd_lava_dragon_scream_a");
            bool od = Decode(old, out float opk, out float orms);
            float lift = od ? orms - OldRmsDb : -999f, opkDb = od ? 20f * Mathf.Log10(opk) : -999f;
            bool lifted = od && Mathf.Abs(lift - OldLiftDb) <= 1f, flat = od && Mathf.Abs(lift) <= 1f;
            Check(lifted ? Mathf.Abs(VolDb - VolDbLift) < 1e-4f : flat && Mathf.Abs(VolDb - VolDbNoLift) < 1e-4f,   // E1: the rows must fit the lift as measured, either way
                "L  the old scream sd_lava_dragon_scream_a (amb_l07_dragon_scream, no longer played at an attack): decoded peak " + F(opkDb) + " dBFS, RMS " + F(orms)
                + " against " + F(OldRmsDb) + " stored: lift " + F(lift, "+0.0;-0.0") + " dB. "
                + (lifted ? "Normalize lifted it (+" + F(OldLiftDb, "0.0") + "): volDb " + F(VolDbLift, "0.0") + " matches it, the rows have " + F(VolDb, "0.0")
                   : flat ? "Normalize did NOT lift it: volDb " + F(VolDbNoLift, "0.0") + " in the 3 amb_l07_dragon_attack rows matches it, the rows have " + F(VolDb, "0.0")
                   : (od ? "neither the assumed lift nor none: re-measure" : "the clip did not load or decode")));
        }

        static void Picker()
        {
            OzDragonScream.Reset();
            int prev = -1, repeats = 0; var n = new int[OzDragonScream.Ids.Length];
            for (int i = 0; i < 300; i++) { int k = OzDragonScream.Pick(); if (k == prev) repeats++; n[k]++; prev = k; }
            Check(repeats == 0 && n.All(x => x > 0), "P  300 picks: " + repeats + " twice in a row, used " + string.Join(" / ", n) + " (each of the three, never the same one twice in a row)");
            OzDragonScream.Reset();
        }

        /// <summary>A frame of AmbientLife's own scream code for a dragon (the private DragonScream, as DragonAsk and DragonStep call it).</summary>
        static void Attacks(AmbientLife life, Camera c)
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            var birdT = typeof(AmbientLife).GetNestedType("Bird", BindingFlags.NonPublic);
            var scream = typeof(AmbientLife).GetMethod("DragonScream", NP);
            var camF = typeof(AmbientLife).GetField("cam", NP);
            Check(birdT != null && scream != null && camF != null, "A  AmbientLife's dragon (Bird), its scream code (DragonScream) and its camera found");
            if (birdT == null || scream == null || camF == null) return;
            camF.SetValue(life, c);
            var b = System.Activator.CreateInstance(birdT, true);
            void Set(string f, object v) => birdT.GetField(f).SetValue(b, v);
            var heard = new List<string>();
            System.Action<string, Vector3, float> ear = (id, p, v) => heard.Add(id);
            AmbientLife.AudioEvent += ear;
            try
            {
                OzDragonScream.Reset();
                Set("dragon", true);
                int Attack(int id, Vector3 at, int framesBefore, Vector3 later, int framesAfter)
                {
                    int h0 = heard.Count;
                    Set("id", id); Set("simPass", true); Set("pos", at);
                    OzDragonScream.Start(id); scream.Invoke(life, new[] { b });                    // DragonAsk: the rules said yes
                    for (int f = 0; f < framesBefore; f++) scream.Invoke(life, new[] { b });       // DragonStep, frame by frame
                    Set("pos", later);
                    for (int f = 0; f < framesAfter; f++) scream.Invoke(life, new[] { b });
                    Set("simPass", false); scream.Invoke(life, new[] { b });                        // the pass is over
                    Set("simPass", true); Set("pos", Vector3.zero); scream.Invoke(life, new[] { b });   // a frame after it, on screen: nothing late
                    return heard.Count - h0;
                }
                Vector3 on = new Vector3(0f, 15f, 0f), side = new Vector3(500f, 30f, 0f), behind = new Vector3(0f, 70f, -200f);
                int a1 = Attack(1, on, 300, on, 0);
                Check(a1 == 1 && OzDragonScream.LastDelay < 0.05f, "A  an attack on screen at the lunge: " + a1 + " scream (want 1), at once");
                string first = heard.LastOrDefault();
                int a2 = Attack(2, side, 60, on, 120);
                Check(a2 == 1 && heard.LastOrDefault() != first, "A  an attack that comes on screen after 60 frames: " + a2 + " scream (want 1), then; not the one before (" + first + " -> " + heard.LastOrDefault() + ")");
                int m0 = OzDragonScream.Missed;
                int a3 = Attack(3, behind, 300, side, 120);
                Check(a3 == 0 && OzDragonScream.Missed == m0 + 1, "A  an attack off screen the whole pass: " + a3 + " screams (want 0), none after it either");
                camF.SetValue(life, null);
                int a4 = Attack(4, on, 60, on, 0);
                Check(a4 == 0, "A  no camera: " + a4 + " screams (want 0)");
                Check(heard.All(h => OzDragonScream.Ids.Contains(h)) && OzDragonScream.Started == 4 && OzDragonScream.Screams == 2,
                    "A  heard only the owner's screams (" + string.Join(", ", heard) + "); passes " + OzDragonScream.Started + ", screams " + OzDragonScream.Screams + ", passes without one " + OzDragonScream.Missed);
            }
            finally { AmbientLife.AudioEvent -= ear; }
        }

        // ------------------------------------------------------------------ Play, level 7
        static IEnumerator play; static float t0; static RtsCamera heldRts;

        [MenuItem("OpenZ/V1.4/Ptero Scream: Count (headless)")]
        static void Menu() => Debug.Log(Tag + Run());

        [MenuItem("OpenZ/V1.4/Ptero Scream: Play Check (Play, level 7)")]
        static void PlayMenu()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || !AmbientLife.I) { Debug.LogError(Tag + "FAIL enter Play on level 7 first"); return; }
            if (!g.World.Map.HasLava) { Debug.LogError(Tag + "FAIL this one runs on level 7"); return; }
            if (play != null) { Debug.LogError(Tag + "FAIL the check is still running"); return; }
            pass = fail = 0; sb = new StringBuilder(); t0 = Time.unscaledTime; play = PlayRun();
            EditorApplication.update -= Pump; EditorApplication.update += Pump;
        }

        static void Stop()
        {
            EditorApplication.update -= Pump; play = null;
            if (heldRts) heldRts.enabled = true; heldRts = null;
            OzDragonProof.Recording = false; OzDragonProof.NoAsks = false;
            var g = ZGame.I;
            if (g) { g.gameSpeed = 1f; if (g.World != null) g.World.DragonProofForce = false; }
        }

        static void Pump()
        {
            if (!Application.isPlaying) { Stop(); Debug.LogError(Tag + "FAIL Play ended during the check"); return; }
            bool more;
            try { more = play.MoveNext(); }
            catch (System.Exception ex) { Stop(); Debug.LogError(Tag + "FAIL the check threw: " + ex); return; }
            if (more) return;
            Stop();
            var path = Path.Combine(OpenZPaths.Records, "l07_ptero_scream_sound_log.txt");
            File.WriteAllText(path, OzSoundLog.Format(OzSoundLog.Since(t0)));
            Info("sound log " + path);
            Debug.Log(Tag + Report());
        }

        static IEnumerator Wait(float s) { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) yield return null; }
        static bool Heard(OzSoundLog.Entry e) => e.outcome == null || !e.outcome.StartsWith("DROP");

        static IEnumerator PlayRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            g.gameSpeed = 4f;                                                                            // the rules' first minute, fast
            while (w.Time < ZWorld.DragonFirstSeconds + 1) yield return null;
            g.gameSpeed = 1f;
            OzDragonProof.NoAsks = true; OzDragonProof.Recording = true; w.DragonProofForce = true;          // as the dragon Play checks: no pass but the check's
            var (x, y) = OzDragonCount.Open(w);
            var r = w.CreateRobotGroup("grunt", x - 8, y - 8, g.player, 1); g.SpawnUnitView(r); w.SendHold(g.player, r);
            var at = ZGame.PxToWorld(x, y);
            var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main;
            float h = rts ? rts.height : 70f;                                                            // the player's own camera height
            if (rts) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(h); }
            Info("a grunt at (" + F(at.x, "0") + ", " + F(at.z, "0") + "), the RTS camera over it at " + F(h, "0") + " m");
            for (var e = Wait(1f); e.MoveNext();) yield return null;
            OzDragonScream.Reset();
            string prev = null; int repeats = 0, heardN = 0;
            for (int i = 0; i < 3; i++)
            {
                if (r.Destroyed) { r = w.CreateRobotGroup("grunt", x - 8, y - 8, g.player, 1); g.SpawnUnitView(r); w.SendHold(g.player, r); }   // E1: the flame killed the last one: a fresh target on the same spot
                else r.SetHealth(r.MaxHealth);
                int ans = -1; float t = Time.unscaledTime; int s0 = OzDragonScream.Screams;   // E1: counted before the pass: a scream at the lunge plays inside ProofPass itself
                for (float until = Time.unscaledTime + 20f; ;)
                {
                    ans = life.ProofPass(r, 30f + 120f * i);
                    if (ans >= 0 || Time.unscaledTime >= until) break;
                    for (var e = Wait(0.5f); e.MoveNext();) yield return null;
                }
                Check(ans == 0, "attack " + (i + 1) + " on screen: " + (ans >= 0 ? ZWorld.DragonWhyName[ans] : "no dragon free or no clear path in 20 s"));
                if (ans != 0) continue;
                float order = Time.unscaledTime;
                while (w.DragonPass != null) yield return null;
                for (var e = Wait(1f); e.MoveNext();) yield return null;
                var log = OzSoundLog.Since(t);
                var mine = log.Where(e => OzDragonScream.Ids.Contains(e.key)).ToList(); var heard = mine.Where(Heard).ToList();
                int old = log.Count(e => (e.key == AmbientLife.SndDragonCall || e.key == AmbientLife.SndDragonScream) && Mathf.Abs(e.t - order) < 0.1f);
                string what = heard.Count > 0 ? heard[0].key + " (" + heard[0].clip + "), vol " + F(heard[0].vol, "0.000") + ", " + F(heard[0].dist, "0.0") + " m from the listener, "
                    + F(OzDragonScream.LastDelay) + " s after the lunge" : "none" + (mine.Count > 0 ? " (" + mine[0].outcome + ")" : "");
                Check(OzDragonScream.Screams - s0 == 1 && heard.Count == 1 && old == 0, "attack " + (i + 1) + ": one scream heard: " + what + "; call or old scream at its start: " + old + " (E1: screams asked " + (OzDragonScream.Screams - s0) + ", heard " + heard.Count + ", in the log " + mine.Count + ")");
                if (heard.Count == 1) { heardN++; if (heard[0].key == prev) repeats++; prev = heard[0].key; }
            }
            Check(heardN >= 2 && repeats == 0, "never the same scream twice in a row: " + heardN + " heard, " + repeats + " repeats");
            // the camera turned away from the grunt: the attack is off screen
            if (r.Destroyed) { r = w.CreateRobotGroup("grunt", x - 8, y - 8, g.player, 1); g.SpawnUnitView(r); w.SendHold(g.player, r); }   // E1: as above
            else r.SetHealth(r.MaxHealth);
            if (rts && cam)
            {
                heldRts = rts; rts.enabled = false;
                var away = cam.transform.position - new Vector3(at.x, cam.transform.position.y, at.z);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(away.x, -0.3f * away.magnitude, away.z).normalized, Vector3.up);
            }
            for (var e = Wait(0.5f); e.MoveNext();) yield return null;
            Check(cam && !OzDragonScream.OnScreen(cam, at) && !OzDragonScream.OnScreen(cam, at + Vector3.up * 30f), "the camera turned away: the grunt and the air over it off screen");
            int a2 = -1, m0 = OzDragonScream.Missed, s1 = OzDragonScream.Screams; float t2 = Time.unscaledTime;
            for (float until = Time.unscaledTime + 20f; ;)
            {
                a2 = life.ProofPass(r, 210f);
                if (a2 >= 0 || Time.unscaledTime >= until) break;
                for (var e = Wait(0.5f); e.MoveNext();) yield return null;
            }
            if (a2 == 0)
            {
                while (w.DragonPass != null) yield return null;
                for (var e = Wait(1f); e.MoveNext();) yield return null;
                int any = OzSoundLog.Since(t2).Count(e => OzDragonScream.Ids.Contains(e.key));
                Check(OzDragonScream.Screams == s1 && OzDragonScream.Missed == m0 + 1 && any == 0, "attack off screen: " + (OzDragonScream.Screams - s1) + " screams asked, " + any + " in the sound log (want 0 and 0)");
            }
            else Check(false, "attack off screen: " + (a2 >= 0 ? ZWorld.DragonWhyName[a2] : "no dragon free or no clear path in 20 s"));
            if (heldRts) { heldRts.enabled = true; heldRts = null; }
            if (rts) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(h); }
        }
    }
}
#endif
