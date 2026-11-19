#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.5, kit hit_feedback_sounds_r8 (FAILS A31): the Play check 'OpenZ/1.5/Checks/Hit Feedback Sounds/...' (editor only).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// In the Unit editor (Play), in REAL TIME at the normal RTS camera (height 70 m, its own angle), by day, x1, seven scenes one after the other:
    ///   1 a jeep fires at a grunt squad (hits on robots; the grunts' rounds ping on the jeep)
    ///   2 the jeep's rounds at empty ground (misses; staged: the sim has no fire-at-ground order, so the jeep's own rounds are drawn at the ground
    ///     through the game's shot code, LegacyMatch.Shot with a miss: the same bolt, firing sound and ground impact as any miss)
    ///   3 a grunt squad fight
    ///   5 a gatling on a squad
    ///   6 a busy fight (grunts, psychos, jeeps and a gatling on both sides): the caps
    ///   7 the same busy fight with the camera moved away: every hit is off screen and must stay silent
    /// Counted per scene: what OzHitFeedback was asked, played and dropped (and why), and, read from the game's real AudioSources every frame, how
    /// many voices of the NEW clips (v15_hit_*, v15_miss_*) sound at once, how many started in any one second.
    /// PASS = never more new voices at once than OzHitFeedback.MaxVoices, never more starts in a second than MaxPerSecond, nothing played in scene 7
    /// (the fight off screen; the rounds the game drew and the hits that asked are written beside it), and each scene heard what it shows (hits in
    /// 1 / 3 / 5 / 6, misses in 2, in 4 a sniper's round heard as a robot hit (imp_hit_robot)).
    /// Written: hit_feedback_report.md, hit_feedback_check.wav (the game's mixed sound of the whole run, tapped at the listener), cues.txt (where
    /// each scene starts in the wav), one frame per scene. The A/B run plays scenes 1-3 with the kit OFF (today's sounds), no gate.
    /// </summary>
    public sealed class OzHitFeedbackCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Hit Feedback]";
        public const float CamHeight = 70f, SceneS = 11f, SettleS = 1.2f;
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static bool KitOff { get; private set; }
        public static string Report { get; private set; } = "no run yet";

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input; CombatView cv; Vector3 C;
        readonly StringBuilder md = new StringBuilder(), cues = new StringBuilder(), notes = new StringBuilder();   // notes: written under the table
        int pass, fail; float speed0; bool on0;
        Tap tap; double t0;

        /// <summary>The game's mixed output, copied on the audio thread (after the AudioListener on its GameObject = the final mix).</summary>
        sealed class Tap : MonoBehaviour
        {
            public float[] buf; public int n, ch = 2; public volatile bool on;
            void OnAudioFilterRead(float[] data, int channels)
            {
                if (!on || buf == null) return;
                ch = channels; int c = Mathf.Min(data.Length, buf.Length - n);
                if (c > 0) { System.Array.Copy(data, 0, buf, n, c); n += c; }
            }
        }

        public static void Begin(string folder, bool kitOff = false)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " enter Play and open the Unit editor first"); return; }
            Folder = folder; KitOff = kitOff;
            ZGame.I.gameObject.AddComponent<OzHitFeedbackCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy()
        {
            Running = false;
            if (started) { OzHitFeedback.On = on0; if (G) G.gameSpeed = speed0; if (rts) rts.enabled = true; }
            if (tap) { tap.on = false; Destroy(tap); }
            KitOff = false;
        }
        bool started;

        // ------------------------------------------------------------------------------------------------ the real voices
        static bool IsNew(string n) => n.StartsWith("v15_hit_") || n.StartsWith("v15_miss_");
        static bool IsOld(string n) => n.StartsWith("sd_impact_bullet_sand") || n.StartsWith("sd_impact_bullet_stone") || n.StartsWith("hs_ricochet_") || n.StartsWith("hs_tink_") || n == "v3-armor-ricochet-a";

        AudioSource[] sources = new AudioSource[0]; float sourcesAt = -9f;
        readonly Dictionary<int, int> lastSamples = new Dictionary<int, int>();
        readonly Dictionary<int, string> lastClip = new Dictionary<int, string>();
        readonly List<double> newStarts = new List<double>();
        int maxNew, maxAll, maxStarts, newStarted;

        void ResetReal() { maxNew = maxAll = maxStarts = newStarted = 0; newStarts.Clear(); }

        void Scan()
        {
            if (Time.unscaledTime - sourcesAt > 1f) { sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None); sourcesAt = Time.unscaledTime; }
            int nNew = 0, nAll = 0; double now = AudioSettings.dspTime;
            for (int i = 0; i < sources.Length; i++)
            {
                var s = sources[i]; if (!s) continue;
                int id = s.GetInstanceID();
                if (!s.isPlaying || !s.clip) { lastClip.Remove(id); lastSamples.Remove(id); continue; }
                string n = s.clip.name; bool isNew = IsNew(n);
                if (!isNew && !IsOld(n)) { lastClip.Remove(id); lastSamples.Remove(id); continue; }
                nAll++;
                if (isNew)
                {
                    nNew++;
                    int ts = s.timeSamples;
                    if (!lastClip.TryGetValue(id, out var was) || was != n || !lastSamples.TryGetValue(id, out var ls) || ts < ls) { newStarts.Add(now); newStarted++; }
                    lastSamples[id] = ts;
                }
                lastClip[id] = n;
            }
            newStarts.RemoveAll(t => now - t >= 1.0);
            if (nNew > maxNew) maxNew = nNew; if (nAll > maxAll) maxAll = nAll; if (newStarts.Count > maxStarts) maxStarts = newStarts.Count;
        }

        // ------------------------------------------------------------------------------------------------ staging
        List<ZObject> Units(Team t)
        {
            var l = new List<ZObject>();
            foreach (var o in W.Objects) if (o != null && !o.Destroyed && o.IsUnit && o.Owner == t) l.Add(o);
            return l;
        }

        int Drop(string id, Team team, float dx, float dz, int count)
        {
            var d = OzUePlayground.Drop(id, team, C + new Vector3(dx, 0f, dz), count);
            if (!d.ok) notes.AppendLine($"(drop of {count} x {id} refused: {d.why})");
            return d.made != null ? d.made.Count : 0;
        }

        void OrderAll()
        {
            if (!input) return;
            var red = Units(Team.Red); var blue = Units(Team.Blue);
            foreach (var pair in new[] { (red, blue), (blue, red) })
                foreach (var u in pair.Item1)
                {
                    ZObject best = null; long bd = long.MaxValue;
                    foreach (var e in pair.Item2) { long dx = e.CenterX - u.CenterX, dy = e.CenterY - u.CenterY, d2 = dx * dx + dy * dy; if (d2 < bd) { bd = d2; best = e; } }
                    if (best == null) continue;
                    try { var wp = input.OrderFor(u, best, best.CenterX, best.CenterY); W.SendWaypoints(u.Owner, u, new List<ZWaypoint> { wp }); } catch (System.Exception) { }
                }
        }

        struct Snap { public int asked, played, off, tgt, voices, persec, sAsked, eng, mAsked, mPlayed, mOff, mSkip, mCap, rAsked, rPlayed, rDrop, robot, metal, building, sniper; }
        static Snap Take() => new Snap
        {
            asked = OzHitFeedback.Asked, played = OzHitFeedback.Played, off = OzHitFeedback.DropOffScreen, tgt = OzHitFeedback.DropTarget, voices = OzHitFeedback.DropVoices,
            persec = OzHitFeedback.DropPerSecond, sAsked = OzHitFeedback.SniperAsked, eng = OzHitFeedback.DropEngine, mAsked = OzHitFeedback.MissAsked, mPlayed = OzHitFeedback.MissPlayed,
            mOff = OzHitFeedback.MissOffScreen, mSkip = OzHitFeedback.MissSkipped, mCap = OzHitFeedback.MissDropCap, rAsked = OzHitFeedback.RicochetAsked, rPlayed = OzHitFeedback.RicochetPlayed,
            rDrop = OzHitFeedback.RicochetDropped, robot = OzHitFeedback.PlayedOf[0], metal = OzHitFeedback.PlayedOf[1], building = OzHitFeedback.PlayedOf[2], sniper = OzHitFeedback.SniperPlayed,
        };

        double WavS => tap != null ? tap.n / (double)(AudioSettings.outputSampleRate * Mathf.Max(1, tap.ch)) : 0.0;

        /// <summary>One scene: the field emptied, `stage` puts the units down, the camera on the field's middle, the fight
        /// runs SceneS seconds (camAway: the camera itself is moved that many metres off, so the whole fight is off screen). want: 'r' robot hits, 'm' misses and no hit, 's' a sniper's round heard as a robot hit, 'x' the caps (hits), 'o' off screen: nothing may play.</summary>
        IEnumerator Scene(int no, string title, System.Action stage, char want, float camAway = 0f, System.Action<float> everyFrame = null)
        {
            OzUePlayground.ClearField(); yield return null; stage(); OzUePlayground.MarkChanged(); yield return null; yield return null;
            if (rts) { rts.enabled = true; rts.focus = C; rts.SetHeightImmediate(CamHeight); }
            OzUeControls.SetPlaying(true);
            OrderAll();
            yield return null;
            if (camAway > 0f && rts) { rts.enabled = false; var cm = Camera.main; if (cm) cm.transform.position += new Vector3(camAway, 0f, 0f); }   // the camera itself moved away: the whole fight is off screen
            float until = Time.time + SettleS; while (Time.time < until) yield return null;
            OzHitFeedback.ResetCounts(); ResetReal(); var a = Take(); int drawn0 = cv ? cv.DrawnShots : 0;
            cues.AppendLine($"{WavS:0.00} s  scene {no}: {title}");
            float start = Time.time, end = start + SceneS; bool shot = false; float nextOrder = start + 3f;
            while (Time.time < end)
            {
                yield return null;
                Scan(); everyFrame?.Invoke(Time.time - start);
                if (Time.time >= nextOrder) { nextOrder += 3f; OrderAll(); }
                if (!shot && Time.time - start > SceneS * 0.5f && !string.IsNullOrEmpty(Folder)) { shot = true; ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"scene{no}.png")); }
            }
            var b = Take();
            if (camAway > 0f && rts) rts.enabled = true;
            int asked = b.asked - a.asked, played = b.played - a.played, mPlayed = b.mPlayed - a.mPlayed, mAsked = b.mAsked - a.mAsked;
            int ownV = OzHitFeedback.PeakVoices, ownS = OzHitFeedback.PeakPerSecond;                                  // the cap's own, this scene (ResetCounts at its start)
            bool capsReal = maxNew <= OzHitFeedback.MaxVoices && maxStarts <= OzHitFeedback.MaxPerSecond, capsOwn = ownV <= OzHitFeedback.MaxVoices && ownS <= OzHitFeedback.MaxPerSecond;
            bool caps = capsReal && capsOwn;
            int cAsked = asked + mAsked + (b.rAsked - a.rAsked), cAdmit = played + mPlayed + (b.rPlayed - a.rPlayed);
            int cDropCap = (b.voices - a.voices) + (b.persec - a.persec) + (b.mCap - a.mCap), cDropOther = cAsked - cAdmit - cDropCap;
            bool heard = want == 'r' ? b.robot - a.robot >= 3 : want == 'm' ? mPlayed >= 2 && played == 0 : want == 's' ? b.sniper - a.sniper >= 1 && b.robot - a.robot >= 1 : want == 'x' ? played >= 10
                       : /* 'o' */ played == 0 && mPlayed == 0 && maxNew == 0;
            bool ok = KitOff || (caps && heard);
            if (!KitOff) { if (ok) pass++; else fail++; }
            if (want == 'o') notes.AppendLine($"(scene {no}: the fight went on off screen: {(cv ? cv.DrawnShots : 0) - drawn0} rounds drawn by the game, {asked} hits and {mAsked} misses reached the hit sounds, none played)");
            md.AppendLine($"| {no} {title} | {asked} | {played} (robot {b.robot - a.robot}, vehicle/gun {b.metal - a.metal}, building {b.building - a.building}; sniper rounds among them: asked {b.sAsked - a.sAsked}, played {b.sniper - a.sniper}) | "
                + $"off screen {b.off - a.off}, same target {b.tgt - a.tgt}, voices full {b.voices - a.voices}, a second full {b.persec - a.persec}, engine {b.eng - a.eng} | "
                + $"{mAsked} / {mPlayed} (off screen {b.mOff - a.mOff}, by chance {b.mSkip - a.mSkip}, caps {b.mCap - a.mCap}) | {b.rAsked - a.rAsked} / {b.rPlayed - a.rPlayed} / {b.rDrop - a.rDrop} | "
                + $"{maxNew} (all impact clips {maxAll}) | {maxStarts} ({newStarted} in the scene) | "
                + $"asked {cAsked} / admitted {cAdmit} / dropped {cAsked - cAdmit} (by a cap {cDropCap}: voices {b.voices - a.voices}, a second {b.persec - a.persec}, misses' caps {b.mCap - a.mCap}; ricochets off screen or a cap {b.rDrop - a.rDrop}; other {cDropOther - (b.rDrop - a.rDrop)}); most voices {ownV}, most starts in 1 s {ownS}, edge up to {OzHitFeedback.EdgeUsed * 1000f:0} ms | "
                + $"{(KitOff ? "kit OFF (no gate)" : ok ? "PASS" : "FAIL" + (capsReal ? "" : $" caps (real: voices {maxNew}, starts in 1 s {maxStarts})") + (capsOwn ? "" : $" caps (own: voices {ownV}, starts in 1 s {ownS})") + (heard ? "" : " not heard as wanted"))} |");
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); input = FindFirstObjectByType<PlayerInput>(); cv = FindFirstObjectByType<CombatView>();
            started = true; on0 = OzHitFeedback.On; speed0 = G.gameSpeed; G.gameSpeed = 1f; OzHitFeedback.On = !KitOff;
            C = ZGame.PxToWorld(W.Map.width * 8, W.Map.height * 8); C.y = 0f;
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            OzUnitEditor.SetPanelsHidden(true); OzUeNight.Set(false);
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener) { tap = listener.gameObject.AddComponent<Tap>(); tap.buf = new float[AudioSettings.outputSampleRate * 2 * 150]; tap.on = true; }
            t0 = AudioSettings.dspTime;
            md.AppendLine($"# Hit feedback sounds check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, real time, x1, the RTS camera at {CamHeight:0} m{(KitOff ? ", A/B: THE KIT OFF (today's sounds, no gate)" : "")}").AppendLine();
            md.AppendLine($"Caps: {OzHitFeedback.MaxVoices} impact voices at once, {OzHitFeedback.MaxPerSecond} impact sounds a second, the old ricochets counted in (misses {OzHitFeedback.MaxMissPerSecond}, {OzHitFeedback.MaxMissVoices} at once), "
                + $"one per target in {OzHitFeedback.TargetWindow:0.00} s, on screen only (+{OzHitFeedback.ScreenMargin * 100f:0} % of the view); a sniper's round uses the same hit sounds as any bullet (no bip). "
                + "'real' columns = the game's AudioSources read every frame (clips v15_hit_*, v15_miss_*). 'the cap's own' = OzHitFeedback's counters for the scene: "
                + $"asked, admitted, dropped; its own most voices at once and most starts in exactly 1 s; the per-second window is 1 s + an edge margin ({OzHitFeedback.MinEdge * 1000f:0}-{OzHitFeedback.MaxEdge * 1000f:0} ms: two frames + the audio latency).").AppendLine();
            md.AppendLine("| scene | hits asked | hits played | hits dropped | misses asked / played | old ricochets asked / played / dropped by the cap | real: most new voices at once | real: most new starts in a second | the cap's own (hits + misses + ricochets) | result |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");

            ZObject jeep = null; float nextShot = 0f; int staged = 0;
            var e1 = Scene(1, "a jeep fires at a grunt squad", () => { Drop("jeep", Team.Red, -6f, 0f, 1); Drop("grunt", Team.Blue, 6f, 0f, 2); }, 'r'); while (e1.MoveNext()) yield return e1.Current;
            var e2 = Scene(2, "the jeep's rounds at empty ground (staged misses)", () => { Drop("jeep", Team.Red, -6f, 0f, 1); var r = Units(Team.Red); jeep = r.Count > 0 ? r[0] : null; nextShot = 0.5f; staged = 0; }, 'm', 0f, t =>
            {
                if (jeep == null || jeep.Destroyed || t < nextShot || t > SceneS - 1.5f) return;
                nextShot += 0.28f; staged++;
                UnitView v = null; G.Views.TryGetValue(jeep.Id, out v);
                Vector3 from = v ? v.MuzzlePos() : G.CenterWorld(jeep) + Vector3.up * 1.2f;
                Vector3 to = C + new Vector3(4f + Random.Range(-2.5f, 2.5f), 0f, Random.Range(-3f, 3f)); to.y = OpenZFxPlayer.I ? OpenZFxPlayer.I.SurfaceY(to) : 0f;
                LegacyMatch.Shot(new ZEvent { type = ZEventType.Hitscan, objId = jeep.Id, otherId = -1, flag = false }, null, "jeep", from, to, FxManager.I, CombatAudio.I);
            });
            while (e2.MoveNext()) yield return e2.Current;
            notes.AppendLine($"(scene 2: {staged} staged rounds, drawn by the game's own shot code as misses)");
            var e3 = Scene(3, "a grunt squad fight", () => { Drop("grunt", Team.Red, -6f, 0f, 2); Drop("grunt", Team.Blue, 6f, 0f, 2); }, 'r'); while (e3.MoveNext()) yield return e3.Current;
            if (!KitOff)
            {
                var e4 = Scene(4, "snipers hitting a grunt squad (robot hit sounds, no bip)", () => { Drop("sniper", Team.Red, -9f, 0f, 1); Drop("grunt", Team.Blue, 8f, 0f, 2); }, 's'); while (e4.MoveNext()) yield return e4.Current;
                var e5 = Scene(5, "a gatling on a squad", () => { Drop("gatling", Team.Blue, 7f, 0f, 1); Drop("grunt", Team.Red, -6f, 0f, 2); }, 'r'); while (e5.MoveNext()) yield return e5.Current;
                System.Action busy = () =>
                {
                    Drop("grunt", Team.Red, -8f, -5f, 2); Drop("psycho", Team.Red, -8f, 5f, 1); Drop("jeep", Team.Red, -12f, 0f, 2);
                    Drop("grunt", Team.Blue, 8f, 5f, 2); Drop("psycho", Team.Blue, 8f, -5f, 1); Drop("gatling", Team.Blue, 12f, 0f, 1);
                };
                var e6 = Scene(6, "a busy fight (the caps)", busy, 'x'); while (e6.MoveNext()) yield return e6.Current;
                var e7 = Scene(7, "the same busy fight, the camera moved away (every hit off screen)", busy, 'o', 260f); while (e7.MoveNext()) yield return e7.Current;
            }
            if (rts) rts.focus = C;
            OzUePlayground.ClearField(); OzUnitEditor.SetPanelsHidden(false);
            G.gameSpeed = speed0;
            md.AppendLine().Append(notes).AppendLine(KitOff ? "A/B run (the kit OFF): today's sounds, nothing gated." : $"{pass} PASS, {fail} FAIL.").AppendLine(OzHitFeedback.Status() + " (the last scene)");
            if (tap) { tap.on = false; if (!string.IsNullOrEmpty(Folder)) WriteWav(Path.Combine(Folder, "hit_feedback_check.wav"), tap.buf, tap.n, tap.ch, AudioSettings.outputSampleRate); }
            md.AppendLine().AppendLine("Sound of the whole run: hit_feedback_check.wav (the game's own mix). Scene starts in it: cues.txt.");
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) { File.WriteAllText(Path.Combine(Folder, "hit_feedback_report.md"), Report); File.WriteAllText(Path.Combine(Folder, "cues.txt"), cues.ToString()); }
            Debug.Log(Tag + "\n" + Report);
            OzHitFeedback.On = on0;
            Destroy(this);
        }

        static void WriteWav(string path, float[] buf, int n, int ch, int rate)
        {
            using (var fs = new FileStream(path, FileMode.Create)) using (var bw = new BinaryWriter(fs))
            {
                int bytes = n * 2;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)ch);
                bw.Write(rate); bw.Write(rate * ch * 2); bw.Write((short)(ch * 2)); bw.Write((short)16); bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
                for (int i = 0; i < n; i++) bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(buf[i] * 32767f), -32768, 32767));
            }
        }
    }
}
#endif
