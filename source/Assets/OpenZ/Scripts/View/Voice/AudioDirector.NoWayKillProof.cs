// OpenZ (GPL-3.0) - version 1.2, items 62 and 63: the Play proof of the no-way answer and of the kill lines
// (menus in Editor/OzNoWayKillLinesMenu.cs; the rules: View/Voice/AudioDirector.NoWayLines.cs, AudioDirector.KillLines.cs).
//   OzNoWayKillProof.Run()      in any level (Play): spawns a small scene at the centre of the screen and counts what the game says:
//     A  the no-way answer: a grunt sent alone at an enemy heavy tank (it answers and goes), a second one inside the gap (the normal
//        answer), a heavy tank at a grunt and two grunts at the tank (never a no-way), a third grunt sent while a robot line speaks
//        (the no-way line waits for it, then plays, another line than the first);
//     B  the kill lines: "Smokin'!", "Boom!" and the second voice's "Boom!" through the game's own path, then a fight on screen
//        (our robots and a light tank against jeeps, a light tank, a gun and robots): kill quips, "Target destroyed.", cheers, and
//        whether one of these lines started over another robot voice;
//     every row also in the sound log (the menu writes both).
//   OzNoWayKillProof.Running / Report / Last
// View only: it never changes a rule of the simulation (it spawns units and gives orders as the cheat spawner and a player do).
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class AudioDirector
    {
        /// <summary>The proof's order: the voice of an order exactly as PollInput gives it after the player's click.</summary>
        internal void NoWayKillProofOrder(bool attack) => OrderVoice(attack);
        internal bool NoWayKillProofQuiet() => NoWayQuiet();
        internal string ProofMainId => alanMain != null && alanMain.src && alanMain.src.isPlaying && alanMain.def != null ? alanMain.def.id : null;
        internal string ProofAlertId => alanAlert != null && alanAlert.src && alanAlert.src.isPlaying && alanAlert.def != null ? alanAlert.def.id : null;
        internal string ProofAlertClip => alanAlert != null && alanAlert.src && alanAlert.src.clip ? alanAlert.src.clip.name : null;
        internal bool ProofRachelPlaying => rachel != null && rachel.src && rachel.src.isPlaying;
        internal float ProofMainLeft => ProofMainId != null ? alanMainClipLeft() : 0f;
    }

    public static class OzNoWayKillProof
    {
        public static bool Running { get; private set; }
        public static string Last { get; private set; } = "";
        public static string Report { get; private set; } = "not run";
        static StringBuilder sb; static int fails, warns;
        static readonly HashSet<string> KillLineEvents = new HashSet<string> { "vo_kill_quip", "vo_boom", "vo_boom_brad" };

        public static string Run()
        {
            var ad = AudioDirector.I;
            if (!ad || !ZGame.I || ZGame.I.World == null) return "no level (enter Play on a level)";
            if (Running) return "already running";
            Running = true; Report = "running";
            ad.StartCoroutine(All(ad));
            return "running (about 2 1/2 minutes: the no-way gap is 25 s, the fight up to 80 s); read OzNoWayKillProof.Report / Last when Running is false";
        }

        static void Row(bool ok, string s) { if (!ok) fails++; string r = "[no_way_kill] " + (ok ? "OK   " : "FAIL ") + s; sb.AppendLine(r); Debug.Log(r); }
        static void Info(string s) { string r = "[no_way_kill] " + s; sb.AppendLine(r); Debug.Log(r); }
        static void Warn(string s) { warns++; string r = "[no_way_kill] WARN " + s; sb.AppendLine(r); Debug.LogWarning(r); }

        static ZObject Spawn(ZGame g, string cat, string type, int tx, int ty, Team team, int amount = -1)
        {
            var W = g.World;
            OzCheatSpawner.FreeTile(W, tx, ty, cat == "robot" ? 1 : 2, cat == "robot", out tx, out ty);
            var o = cat == "robot" ? W.CreateRobotGroup(type, tx * 16, ty * 16, team, amount) : W.CreateObject(cat, type, tx * 16, ty * 16, team);
            if (o == null) return null;
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            return o;
        }

        static void Select(PlayerInput input, params ZObject[] us) { input.Selection.Clear(); foreach (var u in us) if (u != null) input.Selection.Add(u); }

        static bool Attack(ZWorld W, ZObject u, ZObject t) =>
            u != null && t != null && W.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY, attackTo = true, playerGiven = true } });

        static double Dist(ZObject a, ZObject b) { double dx = a.CenterX - b.CenterX, dy = a.CenterY - b.CenterY; return System.Math.Sqrt(dx * dx + dy * dy); }

        static IEnumerator WaitQuiet(AudioDirector ad, float max)
        {
            float t0 = Time.unscaledTime;
            while (!ad.NoWayKillProofQuiet() && Time.unscaledTime - t0 < max) yield return null;
        }

        /// <summary>One order of part A: select, attack, the order's voice as after a click; then up to 5 s for a no-way line.</summary>
        static IEnumerator NoWayStep(AudioDirector ad, PlayerInput input, ZWorld W, ZObject[] sel, ZObject target, System.Action<bool, float, string> done)
        {
            var gate = AudioDirector.NoWayGate; int said0 = gate.Said;
            Select(input, sel);
            foreach (var u in sel) Attack(W, u, target);
            float t0 = Time.unscaledTime;
            ad.NoWayKillProofOrder(true);
            while (Time.unscaledTime - t0 < 5f && gate.Said == said0) yield return null;
            bool spoke = gate.Said > said0;
            done(spoke, spoke ? AudioDirector.NoWayLastStart - t0 : -1f, spoke ? gate.LastLine : null);
        }

        static IEnumerator All(AudioDirector ad)
        {
            sb = new StringBuilder(); fails = 0; warns = 0;
            var g = ZGame.I; var W = g.World; var input = Object.FindFirstObjectByType<PlayerInput>(); var cam = Camera.main;
            Team me = g.player, foe = me == Team.Red ? Team.Blue : Team.Red;
            float T0 = Time.unscaledTime;
            if (!input || !cam || !g.GroundPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var gp))
            { Row(false, "no player input / camera / ground under the centre of the screen"); Finish(T0); yield break; }
            var px = ZGame.WorldToPx(gp); int cx = px.x / 16, cy = px.y / 16;
            var gate = AudioDirector.NoWayGate;
            Info("level " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", we are " + me + ", screen centre tile " + cx + "," + cy
                 + ", robot chatter " + AudioDirector.RobotChatter.Names[AudioDirector.RobotChatter.Level] + ", voices " + (ad.voices ? "on" : "OFF")
                 + "; no-way gap " + gate.Gap.ToString("0") + " s, wait " + gate.Wait.ToString("0.0") + " s");
            if (AudioDirector.RobotChatter.Level != 0) Warn("ROBOT CHATTER is not FULL: that setting leaves the kill quips and the cheers out (set FULL and run again)");

            // ---------------- A: the no-way answer ----------------
            var heavy = Spawn(g, "vehicle", "heavy", cx + 8, cy - 2, foe);
            var gun = Spawn(g, "cannon", "gun", cx + 8, cy + 4, foe);
            var g1 = Spawn(g, "robot", "grunt", cx - 9, cy - 3, me, 1);
            var g2 = Spawn(g, "robot", "grunt", cx - 9, cy + 1, me, 1);
            var g3 = Spawn(g, "robot", "grunt", cx - 9, cy + 5, me, 1);
            var tank = Spawn(g, "vehicle", "heavy", cx - 10, cy + 8, me);
            var eg = Spawn(g, "robot", "grunt", cx + 7, cy + 9, foe, 1);
            if (heavy == null || gun == null || g1 == null || g2 == null || g3 == null || tank == null || eg == null)
            { Row(false, "the scene of part A could not be spawned"); Finish(T0); yield break; }
            yield return null;
            Row(AudioDirector.NoWayPair("grunt", "heavy") && AudioDirector.NoWayPair("grunt", "gun") && !AudioDirector.NoWayPair("heavy", "grunt") && !AudioDirector.NoWayPair("grunt", "grunt"),
                "the game's strength table: grunt at heavy = no way, grunt at gun = no way, heavy at grunt = normal, grunt at grunt = normal");
            yield return WaitQuiet(ad, 10f);
            yield return new WaitForSecondsRealtime(0.5f);

            bool spoke = false; float lag = -1f; string line1 = null;
            double d0 = Dist(g1, heavy);
            yield return NoWayStep(ad, input, W, new[] { g1 }, heavy, (s, l, ln) => { spoke = s; lag = l; line1 = ln; });
            string clip1 = AudioDirector.NoWayLastClip; bool over1 = AudioDirector.NoWayLastOverVoice; float start1 = AudioDirector.NoWayLastStart;
            yield return new WaitForSecondsRealtime(1.5f);
            bool goes = !g1.Destroyed && (g1.AttackObject == heavy || Dist(g1, heavy) < d0 - 4 || (g1.Waypoints.Count > 0 && g1.Waypoints[g1.Waypoints.Count - 1].refId == heavy.Id));
            Row(spoke && !over1 && goes, "no-way 1: a grunt sent alone at the enemy heavy tank: " + (spoke ? "\"" + line1 + "\" (clip " + clip1 + ", " + lag.ToString("0.00") + " s after the order)" : "NO no-way line in 5 s")
                + (over1 ? ", OVER another voice" : ", over no other voice") + "; the grunt goes: " + (goes ? "yes (its attack order stands)" : "NO"));

            int cool0 = gate.Cooling, said0 = gate.Said;
            yield return WaitQuiet(ad, 6f);
            yield return NoWayStep(ad, input, W, new[] { g2 }, gun, (s, l, ln) => { spoke = s; });
            Row(!spoke && gate.Cooling == cool0 + 1 && gate.Said == said0, "inside the gap (" + (Time.unscaledTime - start1).ToString("0.0") + " s after no-way 1): a grunt sent alone at the enemy gun gets its normal answer, no no-way line"
                + " (orders inside the gap " + cool0 + " -> " + gate.Cooling + ")");

            int asked0 = gate.Asked; cool0 = gate.Cooling;
            yield return NoWayStep(ad, input, W, new[] { tank }, eg, (s, l, ln) => { spoke = s; });
            bool ctrl1 = !spoke && gate.Asked == asked0 && gate.Cooling == cool0;
            yield return NoWayStep(ad, input, W, new[] { g2, g3 }, heavy, (s, l, ln) => { spoke = s; });
            bool ctrl2 = !spoke && gate.Asked == asked0 && gate.Cooling == cool0;
            Row(ctrl1 && ctrl2, "never a no-way: our heavy tank sent at an enemy grunt (" + (ctrl1 ? "normal" : "WRONG") + "), two grunts sent together at the heavy tank (" + (ctrl2 ? "normal" : "WRONG") + ")");

            while (Time.unscaledTime - start1 < gate.Gap + 0.5f) yield return null;
            yield return WaitQuiet(ad, 10f);
            yield return new WaitForSecondsRealtime(3.2f);                                     // past the robot line gap: a selection line may start
            AudioDirector.Say("vo_select_generic");
            float ts = Time.unscaledTime; while (ad.ProofMainId != "vo_select_generic" && Time.unscaledTime - ts < 1.5f) yield return null;
            float left = ad.ProofMainLeft; bool selSpeaking = ad.ProofMainId == "vo_select_generic";
            float tOrder = Time.unscaledTime;
            g3 = g3.Destroyed ? Spawn(g, "robot", "grunt", cx - 9, cy + 5, me, 1) : g3;
            yield return NoWayStep(ad, input, W, new[] { g3 }, heavy.Destroyed ? gun : heavy, (s, l, ln) => { spoke = s; });
            string line3 = spoke ? gate.LastLine : null;
            bool waited = !selSpeaking || AudioDirector.NoWayLastStart >= tOrder + left - 0.05f;
            Row(spoke && waited && !AudioDirector.NoWayLastOverVoice && line3 != line1,
                "no-way 3 (" + (AudioDirector.NoWayLastStart - start1).ToString("0.0") + " s after no-way 1), sent while a selection line spoke (" + (selSpeaking ? left.ToString("0.00") + " s of it left" : "it could not start: tested without") + "): "
                + (spoke ? "\"" + line3 + "\" (clip " + AudioDirector.NoWayLastClip + ") " + (AudioDirector.NoWayLastStart - tOrder).ToString("0.00") + " s after the order, " + (waited ? "after that line ended" : "BEFORE that line ended") : "NO no-way line")
                + (line3 != null && line3 == line1 ? ", the SAME line as no-way 1" : ", another line than no-way 1"));

            // ---------------- B: the kill lines ----------------
            foreach (var want in new[] { ("vo_kill_quip", AudioDirector.SmokinLine, "smokin"), ("vo_boom", AudioDirector.BoomLine, "boom_boom"), ("vo_boom_brad", AudioDirector.BoomLine, "boom_boom") })
            {
                string got = null; float t0 = Time.unscaledTime;
                while (got == null && Time.unscaledTime - t0 < 20f)
                {
                    yield return WaitQuiet(ad, 10f);
                    AudioDirector.Say(want.Item1, "=" + want.Item2);
                    float t1 = Time.unscaledTime;
                    while (Time.unscaledTime - t1 < 0.5f && got == null) { if (ad.ProofAlertId == want.Item1) got = ad.ProofAlertClip; else yield return null; }
                    if (got == null) yield return new WaitForSecondsRealtime(2.7f);           // the event's own cooldown
                }
                Row(got != null && got.Contains(want.Item3), want.Item1 + " \"" + want.Item2 + "\" through the game's path: " + (got != null ? "clip " + got + " on the robots' alert channel" : "NOTHING in 20 s"));
                while (ad.ProofAlertId != null) yield return null;
            }

            int q0 = AudioDirector.QuipSaid, qa0 = AudioDirector.QuipAsked, s0 = AudioDirector.Smokins, b0 = AudioDirector.Booms, ba0 = AudioDirector.BoomAsked, bd0 = AudioDirector.BoomDropped;
            int cl0 = AudioDirector.CheerLull, ct0 = AudioDirector.CheerLate, ql0 = AudioDirector.QuipLines.Count;
            int td0 = ad.Plays.TryGetValue("vo_target_destroyed", out var tdn) ? tdn : 0;
            float tf = Time.unscaledTime;
            var foes = new List<ZObject>();
            foreach (var t in new[] { ("vehicle", "jeep", 6, -5), ("vehicle", "jeep", 7, 0), ("vehicle", "light", 6, 5), ("cannon", "gatling", 8, -2), ("robot", "grunt", 7, 8), ("robot", "psycho", 7, -8) })
            { var o = Spawn(g, t.Item1, t.Item2, cx + t.Item3, cy + t.Item4, foe, t.Item1 == "robot" ? 3 : -1); if (o != null) { foes.Add(o); foes.AddRange(o.Minions); } }
            var ours = new List<ZObject>();
            foreach (var t in new[] { ("robot", "grunt", -6, -5), ("robot", "grunt", -6, 0), ("robot", "psycho", -6, 5), ("robot", "tough", -7, -8), ("vehicle", "light", -8, 3), ("vehicle", "medium", -8, -3) })
            { var o = Spawn(g, t.Item1, t.Item2, cx + t.Item3, cy + t.Item4, me, t.Item1 == "robot" ? 3 : -1); if (o != null) ours.Add(o); }
            yield return null;
            foreach (var o in ours) W.SendWaypoints(o.Owner, o, new[] { new ZWaypoint { mode = WpMode.Move, refId = -1, x = (cx + 7) * 16, y = cy * 16, attackTo = true, playerGiven = true } });
            Info("fight: " + ours.Count + " of our groups sent on an attack-move at " + foes.Count + " enemy units (3 vehicles, a gun, 2 robot squads) at the centre of the screen");
            int overRobot = 0, overRachel = 0; string prevAlert = null; var overWhat = new List<string>();
            float end = -1f;
            while (Time.unscaledTime - tf < 80f)
            {
                string a = ad.ProofAlertId;
                if (a != null && a != prevAlert && KillLineEvents.Contains(a))
                {   // one of the kill lines started this frame: was another voice speaking?
                    string m = ad.ProofMainId;
                    if (m != null) { overRobot++; overWhat.Add(a + " over " + m); }
                    if (ad.ProofRachelPlaying && a == "vo_kill_quip") overRachel++;
                }
                prevAlert = a;
                bool left2 = false; foreach (var f in foes) if (f != null && !f.Destroyed && f.KillAt < 0) { left2 = true; break; }
                if (!left2 && end < 0f) end = Time.unscaledTime;
                if (end >= 0f && Time.unscaledTime - end > AudioDirector.BoomWait + 1f) break;     // a cheer still waiting gets its time
                yield return null;
            }
            int killed = 0, killedVeh = 0; foreach (var f in foes) if (f != null && (f.Destroyed || f.KillAt >= 0)) { killed++; if (f.Kind != ZKind.Robot) killedVeh++; }
            int quips = AudioDirector.QuipSaid - q0, cheers = AudioDirector.Booms - b0, smok = AudioDirector.Smokins - s0;
            int td = (ad.Plays.TryGetValue("vo_target_destroyed", out var tdn2) ? tdn2 : 0) - td0;
            var said = new List<string>(); for (int i = ql0; i < AudioDirector.QuipLines.Count; i++) said.Add(AudioDirector.QuipLines[i]);
            int twice = 0; for (int i = 1; i < said.Count; i++) if (said[i] == said[i - 1]) twice++;
            var log = OzSoundLog.Since(tf);
            var cheerTimes = new List<float>(); int boomClips = 0;
            foreach (var e in log)
            {
                if (e.src == "VO" && (e.key == "vo_boom" || e.key == "vo_boom_brad") && e.outcome != null && e.outcome.StartsWith("PLAY")) { cheerTimes.Add(e.t); if (e.clip != null && e.clip.Contains("boom_boom")) boomClips++; }
            }
            float minGap = 1e9f; for (int i = 1; i < cheerTimes.Count; i++) minGap = Mathf.Min(minGap, cheerTimes[i] - cheerTimes[i - 1]);
            Info("fight result after " + (Time.unscaledTime - tf).ToString("0") + " s: " + killed + " of " + foes.Count + " enemy units destroyed (" + killedVeh + " vehicles / guns)"
                 + "; kill quips asked " + (AudioDirector.QuipAsked - qa0) + ", said " + quips + " (" + string.Join(", ", said) + "), \"Smokin'!\" " + smok
                 + "; \"Target destroyed.\" " + td + "; cheers asked " + (AudioDirector.BoomAsked - ba0) + ", said " + cheers + " (in a lull " + (AudioDirector.CheerLull - cl0)
                 + ", after " + AudioDirector.BoomLull.ToString("0.0") + " s while the fight went on " + (AudioDirector.CheerLate - ct0) + "), dropped " + (AudioDirector.BoomDropped - bd0) + ", \"Boom!\" takes " + boomClips);
            if (killed < 3) Warn("the fight killed fewer than 3 enemy units: run it again in an open spot (the counts below are thin)");
            Row(quips + cheers >= 1 || killed < 3, "the fight on screen said kill lines: " + quips + " quip(s), " + cheers + " cheer(s)");
            Row(overRobot == 0, "no kill line started over another robot voice (" + overRobot + (overWhat.Count > 0 ? ": " + string.Join("; ", overWhat) : "") + ")" + (overRachel > 0 ? "; a quip over the announcer " + overRachel + " time(s) (the quip's own rule, as before)" : ""));
            Row(twice == 0, "no kill quip line twice in a row (" + said.Count + " said)");
            Row(cheerTimes.Count < 2 || minGap >= AudioDirector.BoomGap - 0.5f, "cheers at least " + AudioDirector.BoomGap.ToString("0") + " s apart (" + cheerTimes.Count + " cheer(s)" + (cheerTimes.Count >= 2 ? ", closest " + minGap.ToString("0.0") + " s" : "") + ")");
            Info("the sound log of the fight (the kill lines and the no-way line):");
            var mine = new List<OzSoundLog.Entry>();
            foreach (var e in OzSoundLog.Since(T0)) if (e.key == AudioDirector.NoWayEvent || KillLineEvents.Contains(e.key) || e.key == "vo_target_destroyed") mine.Add(e);
            sb.Append(OzSoundLog.Format(mine));
            Finish(T0);
        }

        static void Finish(float T0)
        {
            Report = (fails == 0 ? "PASS" : "FAIL") + " (" + fails + " failed row(s), " + warns + " warning(s), " + (Time.unscaledTime - T0).ToString("0") + " s)";
            sb.AppendLine("[no_way_kill] RESULT: " + Report);
            Last = sb.ToString(); Running = false;
            Debug.Log("[no_way_kill] proof done\n" + Last);
        }
    }
}
