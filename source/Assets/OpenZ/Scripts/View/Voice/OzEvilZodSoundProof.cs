#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV161 EvilZod's sound proof, Play mode, any level, about 6 minutes. He stands on the human's team beside dummies that do not fire back; the sim drives everything and
    /// every row is read from OzSoundLog:
    ///  S  24 shots: 24 plays of the shot row, every round of six uses all six takes, never the same take twice in a row;
    ///  R  each shot has its reload 0.28 s later (the rack's take) and its shell-in OzEvilZodBody.ShellSoundAt later (row 82);
    ///  D  a long and a short dash: kick, whoosh, stop, landing, then the shot, in that order, nothing dropped; no sound when the dash is ready again;
    ///  W  a walk: a surface step, a stomp on each, armour on every second one, the step rate of his stride;
    ///  F  the radio filter off during his line, back for the next other line, 8 of 8;
    ///  L  every line of every event starts and is heard;
    ///  N  shot at, under a quarter of his health and killed: nothing of the robot voice, his low line once.
    /// The report and the sound log go to <see cref="Folder"/>. Test only: the units it made stay.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OzEvilZodSoundProof : MonoBehaviour
    {
        public static string Report = "idle";
        const float ReloadAfter = 0.28f, ReloadTol = 0.08f, Cooldown = (float)(ZEvilZod.CooldownTicks * ZConst.TICK);   // 1.4: the rules' cooldown (7.5 s; 15 s before)
        ZGame g; ZWorld W; AudioDirector ad; RtsCamera rts; Team me, foe; int pass, fail; float tAll;
        readonly StringBuilder log = new StringBuilder();

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_3", "open_fixes_31oct", "evilzod_hero", "sound");
                if (Directory.Exists(k)) return Path.Combine(k, "proof");
            }
            return Path.Combine(Application.persistentDataPath, "evilzod_sound_proof");
        }

        public static void Run() { Report = "running"; new GameObject("OzEvilZodSoundProof").AddComponent<OzEvilZodSoundProof>(); }

        void Start() { StartCoroutine(All()); }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ EvilZod sound] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        IEnumerator Free() { yield return WaitFor(() => AudioDirector.RobotChannelFree, 15f); yield return Real(0.2f); }

        static List<OzSoundLog.Entry> Plays(float t0, string key) =>
            OzSoundLog.Since(t0).Where(e => e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY")).ToList();
        static int Drops(float t0, string key) =>
            OzSoundLog.Since(t0).Count(e => e.key == key && e.outcome != null && e.outcome.StartsWith("DROP"));

        bool Walkable(int x, int y) => !W.Path.WithinImpassable(x - 8, y - 8, 16, 16, out _, out _, true) && !W.Map.LavaAt(x >> 4, y >> 4)
                                       && !W.UnitObjects.Any(o => !o.Destroyed && System.Math.Abs(o.CenterX - x) < 48 && System.Math.Abs(o.CenterY - y) < 48);

        /// <summary>A free line along x near the camera, for his dashes, with room at right angles for the first dummy.</summary>
        bool FindRow(out int x, out int y)
        {
            int fx = (int)(rts.focus.x * 8f), fy = (int)(-rts.focus.z * 8f);
            for (int r = 0; r < 900; r += 32)
                for (int a = 0; a < 16; a++)
                {
                    x = fx + (int)(r * Mathf.Cos(a * 0.3927f)); y = fy + (int)(r * Mathf.Sin(a * 0.3927f));
                    bool ok = Walkable(x, y + 64);
                    for (int k = -140; ok && k <= 260; k += 16) ok = Walkable(x + k, y);
                    if (ok) return true;
                }
            x = y = 0; return false;
        }

        ZObject Dummy(int cx, int cy)
        {
            var t = W.CreateObject("vehicle", "heavy", cx - 16, cy - 16, foe);
            t.Damage = 0; t.MaxHealth = 10000000; t.Health = 10000000;
            return t;
        }

        void Attack(ZObject u, ZObject t) => W.SendWaypoints(me, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY } });

        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; ad = AudioDirector.I; rts = FindFirstObjectByType<RtsCamera>();
            if (W == null || !ad || !rts) { Log("no level in Play with the AudioDirector"); fail++; Finish(); yield break; }
            if (!ZWorld.EvilZodDeviation) { Log("EvilZod is switched off in this game"); fail++; Finish(); yield break; }
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);
            W.Bots.Clear(); tAll = Time.unscaledTime;
            float speed = Mathf.Max(0.01f, g.gameSpeed);
            Log($"level {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, game speed {speed:0.00}, voices {ad.voices}, chatter {AudioDirector.RobotChatter.Names[AudioDirector.RobotChatter.Level]}");
            if (!FindRow(out int x, out int y)) { Check("a free line near the camera", false); Finish(); yield break; }
            rts.focus = ZGame.PxToWorld(x + 60, y); rts.height = 70f;
            yield return Real(1.5f);
            var e = W.CreateRobotGroup(ZEvilZod.Type, x - 8, y - 8, me, 1); g.SpawnUnitView(e);                     // the walk's steps are read from his view
            Check("he stands on the human's team", AudioDirector.EzIs(e));
            yield return Real(0.5f);

            // ---- S, R: 24 shots
            var d1 = Dummy(x, y + 60); float t0 = Time.unscaledTime; int s0 = W.EvilShots;
            Attack(e, d1);
            yield return WaitFor(() => W.EvilShots - s0 >= 24, 90f);
            yield return Real(0.6f);
            var shots = Plays(t0, AudioDirector.EzShotId); var reloads = Plays(t0, AudioDirector.EzReloadId);
            Check($"S 24 shots: {W.EvilShots - s0} fired, {shots.Count} played, {Drops(t0, AudioDirector.EzShotId)} dropped", W.EvilShots - s0 >= 24 && shots.Count >= 24 && Drops(t0, AudioDirector.EzShotId) == 0);
            int full = 0, rounds = Mathf.Min(4, shots.Count / 6), same = 0;
            for (int r = 0; r < rounds; r++) if (shots.Skip(r * 6).Take(6).Select(c => c.clip).Distinct().Count() == 6) full++;
            for (int i = 1; i < shots.Count; i++) if (shots[i].clip == shots[i - 1].clip) same++;
            Check($"S {full} of {rounds} rounds use all six takes", rounds == 4 && full == 4);
            Check($"S {same} equal neighbours", same == 0);
            int timed = shots.Count(s => reloads.Any(q => Mathf.Abs(q.t - s.t - ReloadAfter / speed) <= ReloadTol));
            Check($"R {timed} of {shots.Count} shots have their reload {ReloadAfter:0.00} s after them", timed == shots.Count && shots.Count > 0);
            Check($"R the reload is the rack's take every time ({string.Join(" ", reloads.Select(c => c.clip).Distinct())}; row 82: the clip reload's clicks are the shell-in now)", reloads.Count > 0 && reloads.All(c => c.clip == "evilzod_reload_1"));
            var shells = Plays(t0, AudioDirector.EzShellId);
            int shelled = shots.Count(s => shells.Any(q => Mathf.Abs(q.t - s.t - OzEvilZodBody.ShellSoundAt / speed) <= ReloadTol + 0.04f));
            Check($"R {shelled} of {shots.Count} shots have their shell-in {OzEvilZodBody.ShellSoundAt:0.00} s after them (row 82; the last may still be on its way)", shots.Count > 0 && shelled >= shots.Count - 1);
            d1.DamageHealth(d1.Health);
            yield return Real(3.2f);

            // ---- D: a long dash and a short one
            var d2 = Dummy(x + 180, y); t0 = Time.unscaledTime; int n0 = W.EvilDashes; s0 = W.EvilShots;
            Attack(e, d2);
            yield return WaitFor(() => W.EvilDashes > n0 && W.EvilShots > s0, 6f);
            yield return Real(0.3f);
            DashRow("long", t0);
            float dashAt = t0;
            d2.DamageHealth(d2.Health);
            yield return Real(Cooldown / speed + 1f);
            Check("D no sound when the dash is ready again (owner 1 Nov)", !OzSoundLog.Since(dashAt).Any(c => c.key == "wpn_evilzod_dash_ready"));
            var d3 = Dummy(e.CenterX - 130, e.CenterY); t0 = Time.unscaledTime; n0 = W.EvilDashes; s0 = W.EvilShots;
            Attack(e, d3);
            yield return WaitFor(() => W.EvilDashes > n0 && W.EvilShots > s0, 6f);
            yield return Real(0.3f);
            DashRow("short", t0);
            d3.DamageHealth(d3.Health);
            yield return Real(0.5f);

            // ---- W: the walk
            t0 = Time.unscaledTime; int tx = e.CenterX + 120;
            W.SendWaypoints(me, e, new[] { ZWaypoint.Move(tx, e.CenterY, false) });
            yield return Real(10f / speed);
            var steps = OzSoundLog.Since(t0).Where(s => s.key != null && s.key.StartsWith("rob_step_") && s.outcome != null && s.outcome.StartsWith("PLAY")).ToList();
            var stomps = Plays(t0, AudioDirector.EzStompId); var armour = Plays(t0, AudioDirector.EzArmourId);
            float rate = steps.Count > 1 ? (steps[steps.Count - 1].t - steps[0].t) / (steps.Count - 1) * speed : -1f;
            Log($"W {steps.Count} steps, {stomps.Count} stomps, {armour.Count} armour, one step every {rate:0.00} s");
            Check("W a stomp on each step, the same number within 1", steps.Count >= 5 && Mathf.Abs(stomps.Count - steps.Count) <= 1);
            Check("W armour on every second step, within 1", Mathf.Abs(armour.Count - steps.Count / 2) <= 1);

            // ---- F: the radio filter
            int fok = 0;
            for (int i = 0; i < 8; i++)
            {
                yield return Free();
                ad.EzProofLine(AudioDirector.EzSelectId, 0);
                yield return Real(0.2f);
                bool off = AudioDirector.EzFilterOff && !ad.EzProofFilterOn;
                yield return Free();
                ad.EzProofOtherLine("vo_select_generic");
                yield return Real(0.2f);
                if (off && !AudioDirector.EzFilterOff && ad.EzProofFilterOn) fok++;
            }
            Check($"F {fok} of 8: off for his line, on for the next", fok == 8);

            // ---- L: every line of every event
            var cats = new[] { (AudioDirector.EzSelectId, 0, 4), (AudioDirector.EzMoveId, 1, 4), (AudioDirector.EzAttackId, 2, 4), (AudioDirector.EzDashId, 5, 3), (AudioDirector.EzKillId, 6, 2), (AudioDirector.EzLowId, 7, 2), (AudioDirector.EzBoardId, 3, 1) };
            foreach (var (id, cat, lines) in cats)
            {
                float tl = Time.unscaledTime;
                for (int i = 0; i < lines; i++)
                {
                    yield return Free();
                    ad.EzProofLine(id, cat);
                    yield return Real(id == AudioDirector.EzDashId ? 20.2f : id == AudioDirector.EzKillId ? 8.2f : id == AudioDirector.EzLowId ? 10.2f : 1f);
                }
                var said = OzSoundLog.Since(tAll).Where(c => c.key == id && c.outcome == "HERO LINE").Select(c => c.clip).Distinct().Count();
                Check($"L {id}: {said} of {lines} lines", said == lines);
            }

            // ---- N: nothing of the robot voice out of him
            yield return Free();
            t0 = Time.unscaledTime;
            var shooter = W.CreateRobotGroup("grunt", e.CenterX + 90 - 8, e.CenterY - 8, foe, 1); shooter.MaxHealth = 10000000; shooter.Health = 10000000;
            e.Health = e.MaxHealth / 5;
            yield return Real(14f);
            e.DamageHealth(e.Health);
            yield return Real(3f);
            var all = OzSoundLog.Since(t0);
            int alan = all.Count(c => c.outcome != null && c.outcome.StartsWith("PLAY") && c.src == "AD" && c.clip != null && (c.key.StartsWith("death_cry") || c.key.StartsWith("death_hit") || c.key == "vo_hurt"));
            Check($"N shot at and killed: {alan} robot voice cries", alan == 0);
            Check($"N the robot voice's low line 0 times ({Plays(t0, "vo_low_health").Count}), his own {Plays(t0, AudioDirector.EzLowId).Count}", Plays(t0, "vo_low_health").Count == 0 && Plays(t0, AudioDirector.EzLowId).Count == 1);

            // ---- H: on the other side, on screen, he still speaks (the human is Red, he is Blue, in a default game and in the Unit editor)
            yield return Free();
            yield return Real(8.5f);                                                                // the kill row's cooldown
            var ef = W.CreateRobotGroup(ZEvilZod.Type, x - 8, y - 8, foe, 1);
            var prey = W.CreateRobotGroup("grunt", x + 172, y - 8, me, 1);
            yield return Real(0.5f);
            Check("H the other side's EvilZod on screen counts as heard", ad.EzProofHeard(ef));
            t0 = Time.unscaledTime; n0 = W.EvilDashes; s0 = W.EvilShots;
            W.SendWaypoints(foe, ef, new[] { new ZWaypoint { mode = WpMode.Attack, refId = prey.Id, x = prey.CenterX, y = prey.CenterY } });
            yield return WaitFor(() => W.EvilDashes > n0 && W.EvilShots > s0, 6f);
            yield return Real(1f);
            int gap = OzSoundLog.Since(t0).Count(c => c.key == AudioDirector.EzKillId && c.outcome != null && c.outcome.Contains("robot line gap"));     // his dash line just before holds it back: the shared rule of the robot voices
            Check($"H he killed a robot on the human's side: his kill line {Lines(t0, AudioDirector.EzKillId)} time(s), held by the line gap {gap}", prey.Destroyed && Lines(t0, AudioDirector.EzKillId) + gap == 1);
            Finish();
        }

        static int Lines(float t0, string id) => OzSoundLog.Since(t0).Count(c => c.key == id && c.outcome == "HERO LINE");

        void DashRow(string what, float t0)
        {
            float T(string id) { var p = Plays(t0, id); return p.Count == 1 ? p[0].t : -1f; }
            float kick = T(AudioDirector.EzKickId), wh = T(AudioDirector.EzWhooshId), stop = T(AudioDirector.EzStopId), land = Mathf.Max(T(AudioDirector.EzLandSandId), T(AudioDirector.EzLandHardId));
            var sh = Plays(t0, AudioDirector.EzShotId); float shot = sh.Count > 0 ? sh[0].t : -1f;
            int drops = Drops(t0, AudioDirector.EzKickId) + Drops(t0, AudioDirector.EzWhooshId) + Drops(t0, AudioDirector.EzStopId) + Drops(t0, AudioDirector.EzShotId);
            bool order = kick >= 0f && wh >= kick && stop > wh && land >= stop && shot > land;
            Log($"D {what}: kick {kick - t0:0.00}, whoosh {wh - t0:0.00}, stop {stop - t0:0.00}, land {land - t0:0.00}, shot {shot - t0:0.00}");
            Check($"D {what} dash: kick, whoosh, stop, landing, shot, in order, {drops} dropped", order && drops == 0);
            Check($"D {what} dash: the shot {shot - stop:0.00} s after the stop", shot - stop > 0.08f && shot - stop < 0.4f);
        }

        void Finish()
        {
            Report = $"RESULT: {(fail == 0 ? "PASS" : "FAIL")} {pass} of {pass + fail}";
            Log(Report);
            try
            {
                var dir = Folder(); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "sound_proof.txt"), log.ToString());
                File.WriteAllText(Path.Combine(dir, "sound_log.txt"), OzSoundLog.Format(OzSoundLog.Since(tAll)));
            }
            catch (IOException ex) { Debug.LogWarning(ex.Message); }
            Destroy(gameObject);
        }
    }
}
#endif
