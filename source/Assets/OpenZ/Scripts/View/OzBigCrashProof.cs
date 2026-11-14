using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 7 Play proof: the shuttle's BIG crash landing (rule DV154, Sim/ZWorld.ShuttleBigCrash.cs; the show View/OzPlayableShuttle.BigCrash.cs)
    /// on level 16 in Play (menu OpenZ/V1.3/Shuttle: 7 Big Crash Proof (Level 16, Play), Editor/OzV13BigCrashMenu.cs). Counted PASS / FAIL
    /// lines, a picture at fixed moments of each crash (for the frame sheets: s before / after the contact on the rules' clock), the sound
    /// log of each crash (every row from 1 s before the contact to 4 s after it) and the camera shake every frame (a CSV per stage).
    /// Set-up as the K402 proof (View/OzCrashShakeProof.cs): the CPU teams removed, morning (day clock 0.10, held), 60 fps, the normal RTS
    /// camera at 70 m on the ship. Orders through the rules (SendWaypoints; the rules' proof switches), no desktop input. Stages:
    ///  P1 TODAY's crash (DV154 off for this one landing): the reference (its sparks, its sounds, its shake);
    ///  P2 a BIG crash on screen at full health: more spark sprays than P1, the harder sound (wreck_crash twice, dst_turret_crash,
    ///     dst_fortgun_break_metal), a stronger shake peak than P1, a quarter of the full health lost, the banter asked (said once voiced);
    ///  P3 a big crash with the ship at 15 % health: it ends at the floor (10 %) and stands, not destroyed;
    ///  P4 a big crash with CAMERA SHAKE ON EXPLOSIONS off and P5 with CALM MODE on: no kick, no shake (the same gate);
    ///  P6 three more big crashes: the banter line never the same twice in a row, nor "Dang it!" and "Dammit! I told you I should drive!"
    ///     back to back (counted once the lines are voiced; before that a NOTE).
    /// <see cref="Folder"/>
    /// time). Test only.
    /// </summary>
    public sealed class OzBigCrashProof : MonoBehaviour
    {
        public static string Report = "idle";
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        /// <summary>The picture moments (s after the contact on the rules' clock; the first is before it).</summary>
        public static readonly float[] ShotAt = { -0.6f, 0.0f, 0.12f, 0.35f, 0.6f, 1.0f, 1.7f, 2.8f };

        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", "v13_item07_big_crash");
            return System.IO.Path.Combine(Application.persistentDataPath, "v13_item07_big_crash");
        }
        public static void Run()
        {
            Report = "running";
#if UNITY_EDITOR
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);
#endif
            new GameObject("OzBigCrashProof").AddComponent<OzBigCrashProof>();
        }

        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; Team me = Team.Red;
        ZObject s; OzPlayableShuttle ship;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        int shakePref0; bool calm0, dev0;

        /// <summary>One crash as seen: spark bursts, sound rows, shake peak, health.</summary>
        sealed class Seen { public bool drawn, big; public int bursts, wreck, turret, tear, kicks, asked, hp0, hp1, max, banterRows, banterPlays; public float peak; public string banter = ""; }

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>();
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            shakePref0 = PlayerPrefs.GetInt("oz_shake", 1); calm0 = OzMotion.Reduced; dev0 = ZWorld.ShuttleBigCrashDeviation;
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzBigCrashProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        void Note(string what) => Log("NOTE " + what);
        IEnumerator WaitFor(System.Func<bool> c, float max) { float t = 0f; while (t < max && !c()) { t += Time.deltaTime; yield return null; } }
        IEnumerator Wait(float s0) { float t = 0f; while (t < s0) { t += Time.deltaTime; yield return null; } }
        void Cam(Vector3 focus, float h) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(focus.x, 0f, focus.z); rts.SetHeightImmediate(h); }
        void Select(params ZObject[] os) { input.Selection.Clear(); foreach (var o in os) if (o != null) input.Selection.Add(o); }
        bool Move(int x, int y) => W.SendWaypoints(me, s, new[] { new ZWaypoint { mode = WpMode.Move, x = x, y = y, playerGiven = true } });
        static float AmountNow() { var sh = OpenZFxShake.I; return sh != null ? sh.AmountPx(Camera.main) : 0f; }
        static int BurstsNow() => OpenZHitSparks.I != null ? OpenZHitSparks.I.Bursts : 0;

        int[] Free(int cx, int cy, int minR, int maxR)
        {
            long best = long.MaxValue; int[] r = null; int e = ZWorld.ShuttleEdgeTiles;
            for (int ty = e; ty < W.Map.height - e; ty++)
                for (int tx = e; tx < W.Map.width - e; tx++)
                {
                    int x = tx * 16 + 8, y = ty * 16 + 8; long d = (long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy);
                    if (d >= best || d < (long)minR * minR || d > (long)maxR * maxR) continue;
                    if (W.ShuttleNeverLands(x, y, s, out _) || !W.ShuttlePadFree(x, y, s, out _)) continue;
                    best = d; r = new[] { x, y };
                }
            return r;
        }

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            if (W == null || input == null) { Log("no game (level 16 in Play)"); fails++; Finish(); yield break; }
            if (!ZWorld.ShuttlePlayableDeviation || W.PlayableShuttle == null) { Log("no playable shuttle (level 16, the shuttle rule on, the option SHUTTLE on)"); fails++; Finish(); yield break; }
            me = g.player; s = W.PlayableShuttle; ship = OzPlayableShuttle.Of(s);
            if (!ship) { Log("the shuttle has no view"); fails++; Finish(); yield break; }
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            PlayerPrefs.SetInt("oz_shake", 1); OzMotion.Reduced = false;
            Log($"1.3 item 7 big crash proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}; CPU removed {bots}; DV154 {(dev0 ? "on" : "OFF")}, reading {(ZWorld.ShuttleBigCrashOfAllLandings ? "1 in 3 of all landings" : "1 in 3 of the crash landings")}; " +
                $"shake today {OzCrashLandingShake.SlamPx:F1} / {OzCrashLandingShake.BouncePx:F1} px, big {OzCrashLandingShake.BigSlamPx:F1} / {OzCrashLandingShake.BigBouncePx:F1} px; the banter {(AudioDirector.Has(OzPlayableShuttle.BigCrashBanterId) ? "voiced" : "NOT voiced yet")}");
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Landed && !s.ShuttleAirborne && s.ShuttleLevelled, 30f); yield return Wait(0.8f);

            ZWorld.ShuttleBigCrashDeviation = false;
            var today = new Seen(); yield return Crash("P1", "p1_today_crash", false, -1, today);
            ZWorld.ShuttleBigCrashDeviation = true;
            var big = new Seen(); yield return Crash("P2", "p2_big_crash", true, -1, big);
            if (today.drawn && big.drawn)
            {
                int loss = ZWorld.ShuttleBigCrashLoss(big.max);
                Check($"P2 the big crash: more spark sprays ({big.bursts} vs today's {today.bursts}), the harder sound (wreck_crash {big.wreck} vs {today.wreck}, dst_turret_crash {big.turret}, dst_fortgun_break_metal {big.tear}), a stronger shake (peak {big.peak:F2} px vs {today.peak:F2} px)",
                      big.big && !today.big && big.bursts > today.bursts && big.wreck >= 2 && big.turret >= 1 && big.tear >= 1 && big.peak > today.peak);
                Check($"P2 the health: {big.hp0} -> {big.hp1} of {big.max} (a quarter = {loss} lost); today's crash: {today.hp0} -> {today.hp1} (nothing lost)", big.hp0 - big.hp1 == loss && today.hp0 == today.hp1);
                bool voiced = AudioDirector.Has(OzPlayableShuttle.BigCrashBanterId);
                if (voiced) Check($"P2 the banter said once after the crash ({big.banter})", big.banterPlays == 1);
                else Note($"P2 the banter is not voiced yet: asked and left out with its reason in the sound log ({big.banterRows} rows: {big.banter})");
            }
            else Check("P1 / P2 both crashes drawn", false);
            var low = new Seen(); yield return Crash("P3", "p3_big_crash_at_15pct", true, 15, low);
            int floor = ZWorld.ShuttleBigCrashFloor(s.MaxHealth);
            Check($"P3 a big crash at 15 % health: {low.hp0} -> {low.hp1} HP (the floor {floor}), the ship stands (destroyed {s.Destroyed})", low.drawn && low.hp1 == floor && !s.Destroyed && s.KillAt < 0);
            PlayerPrefs.SetInt("oz_shake", 0);
            var off = new Seen(); yield return Crash("P4", "p4_big_crash_shake_off", true, -1, off);
            PlayerPrefs.SetInt("oz_shake", 1); OzMotion.Reduced = true;
            var calm = new Seen(); yield return Crash("P5", "p5_big_crash_calm", true, -1, calm);
            OzMotion.Reduced = false;
            Check($"P4 / P5 the shake option off and calm mode: no kick ({off.kicks} / {calm.kicks} of {off.asked} / {calm.asked} asked), no camera shake (peaks {off.peak:F2} / {calm.peak:F2} px)", off.drawn && calm.drawn && off.kicks == 0 && calm.kicks == 0 && off.peak < 0.05f && calm.peak < 0.05f);
            var lines = new List<int> { OzPlayableShuttle.BigBanterLast };
            for (int k = 0; k < 3; k++) { int said0 = OzPlayableShuttle.BigCrashBanters; var x = new Seen(); yield return Crash("P6." + (k + 1), "p6_big_crash_" + (k + 1), true, -1, x); if (OzPlayableShuttle.BigCrashBanters > said0) lines.Add(OzPlayableShuttle.BigBanterLast); }   // only a line really picked: a banter left out (a voice setting, a cooldown) is no repeat
            if (AudioDirector.Has(OzPlayableShuttle.BigCrashBanterId))
            {
                bool never = true; for (int i = 1; i < lines.Count; i++) never &= !OzPlayableShuttle.BigBanterSameJoke(lines[i], lines[i - 1]);   // nor "Dang it!" / "Dammit! I told you I should drive!" back to back
                Check($"P6 the banter never the same (nor the other wording of 'I told you I should drive!') twice in a row: lines {string.Join(", ", lines.ConvertAll(i => (i + 1).ToString()))}, {lines.Count - 1} of 3 said (at least 2) (repeats counted {OzPlayableShuttle.BigCrashBanterRepeats})", never && lines.Count - 1 >= 2 && OzPlayableShuttle.BigCrashBanterRepeats == 0);
            }
            else Note("P6 the banter is not voiced yet: the never-twice rule is counted headless in the pick (repeats " + OzPlayableShuttle.BigCrashBanterRepeats + ")");
            Finish();
        }

        /// <summary>One crash landing (the rules' proof switches), the camera on the ship at 70 m; hpPct > 0 = the ship's health first set to it.</summary>
        IEnumerator Crash(string S, string tag, bool big, int hpPct, Seen r)
        {
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Landed && s.ShuttleLevelled && !ship.CrashOn, 30f); yield return Wait(1.0f);
            s.SetHealth(hpPct > 0 ? s.MaxHealth * hpPct / 100 : s.MaxHealth);                       // the proof's own starting health (a repair, not a rule)
            var q = Free(s.CenterX, s.CenterY, 320, 700); if (q == null) { Check($"{S} a landing place", false); yield break; }
            OzPlayableShuttle.ProofRoughNext = true; OzPlayableShuttle.ProofBigNext = big;
            Select(s); Move(q[0], q[1]);
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Descend, 60f);
            Cam(ship.transform.position, 70f);
            Log($"{S} {(big ? "a BIG crash" : "today's crash (DV154 off)")}, health {s.Health} of {s.MaxHealth}, shake option {(PlayerPrefs.GetInt("oz_shake", 1) == 1 ? "on" : "OFF")}, calm mode {(OzMotion.Reduced ? "ON" : "off")} ({tag}); >>> OBS: a 60 fps take now");
            OzCrashLandingShake.ResetCounts(); OzShakeGate.ResetCounts();
            int b0 = BurstsNow(); float t0 = Time.unscaledTime; int shot = 0; r.hp0 = s.Health; r.max = s.MaxHealth;
            var csv = new StringBuilder("t_s,shake_px\n");
            float waited = 0f;
            while (!ship.CrashOn && waited < 8f)
            {   // the way down: the picture before the contact
                yield return null; waited += Time.deltaTime;
                float tau = (float)s.ShuttleLift * (float)ZWorld.ShuttleDescendSec;
                if (shot == 0 && tau <= -ShotAt[0]) { Shot(tag, ShotAt[0]); shot = 1; }
            }
            r.drawn = ship.CrashOn; r.big = ship.CrashBig;
            if (shot == 0) shot = 1;
            float tc = Time.unscaledTime;
            while (true)
            {
                float t = s.ShuttleTouchdownAt >= 0 ? (float)(W.Time - s.ShuttleTouchdownAt) : 0f;    // the rules' clock from the contact (the crash's own clock stops when it ends)
                if (t > ShotAt[ShotAt.Length - 1] + 0.05f) break;
                if (shot < ShotAt.Length && t >= ShotAt[shot]) { Shot(tag, ShotAt[shot]); shot++; }
                float a = AmountNow(); csv.Append(t.ToString("F3", Inv)).Append(',').Append(a.ToString("F3", Inv)).Append('\n');
                if (a > r.peak) r.peak = a;
                if (Time.unscaledTime - tc > 6f) break;
                yield return null;
            }
            yield return Wait(Mathf.Max(0f, 4f - (Time.unscaledTime - tc)));                          // the banter's time (the sound log to +4 s)
            r.hp1 = s.Health; r.bursts = BurstsNow() - b0; r.kicks = OzCrashLandingShake.Kicks; r.asked = OzCrashLandingShake.Asked;
            var rows = new StringBuilder("t_s_from_contact,src,key,clip,outcome\n");
            foreach (var e in OzSoundLog.Since(t0))
            {
                rows.Append((e.t - tc).ToString("F3", Inv)).Append(',').Append(e.src).Append(',').Append(e.key).Append(',').Append(e.clip ?? "").Append(',').Append((e.outcome ?? "").Replace(',', ';')).Append('\n');
                bool play = e.outcome != null && e.outcome.StartsWith("PLAY");
                if (play && e.key == "wreck_crash") r.wreck++;
                if (play && (e.key == "dst_turret_crash" || (e.src == "CA" && e.key == "debris"))) r.turret++;
                if (play && e.key == "dst_fortgun_break_metal") r.tear++;
                if (e.key == OzPlayableShuttle.BigCrashBanterId) { r.banterRows++; if (play) r.banterPlays++; else r.banter = e.outcome; }
            }
            if (r.banterPlays > 0 && OzPlayableShuttle.BigBanterLast >= 0) r.banter = "line " + (OzPlayableShuttle.BigBanterLast + 1) + ": " + OzPlayableShuttle.BigCrashLines[OzPlayableShuttle.BigBanterLast];
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, tag + "_sound_log.csv"), rows.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, tag + "_shake.csv"), csv.ToString());
            foreach (var l in OzCrashLandingShake.Log) Log("   " + l);
            Log($"   {S}: drawn {r.drawn} (big {r.big}); spark bursts {r.bursts}; wreck_crash {r.wreck}, dst_turret_crash {r.turret}, dst_fortgun_break_metal {r.tear}; kicks {r.kicks} of {r.asked}; shake peak {r.peak:F2} px; health {r.hp0} -> {r.hp1}; banter rows {r.banterRows} ({r.banter}); pictures {shot}");
            OzPlayableShuttle.ProofRoughNext = false; OzPlayableShuttle.ProofBigNext = false;
        }

        void Shot(string tag, float at)
        {
            string name = tag + "_" + (at < 0f ? "m" : "p") + Mathf.Abs(at).ToString("0.00", Inv).Replace('.', '_') + "s";
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
        }

        void Finish()
        {
            OzPlayableShuttle.ProofRoughNext = false; OzPlayableShuttle.ProofBigNext = false; ZWorld.ShuttleBigCrashDeviation = dev0;
            PlayerPrefs.SetInt("oz_shake", shakePref0); OzMotion.Reduced = calm0;
            Log($"1.3 item 7 big crash proof: {passes} PASS, {fails} FAIL (switches put back: DV154 {(dev0 ? "on" : "off")}, shake {(shakePref0 == 1 ? "on" : "off")}, calm {(calm0 ? "on" : "off")})");
            Report = fails == 0 ? $"PASS {passes}" : $"FAIL {fails} of {passes + fails}";
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "big_crash_proof_log.txt"), sb.ToString());
            Destroy(gameObject);
        }
    }
}
