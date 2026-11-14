// OpenZ (GPL-3.0) - version 1.2, run 10 item 1d: the Play proof of the "Boom!" cheer after a big kill on camera
// (the rule: View/AudioDirector.cs BoomCandidate / BoomTick, Voice/AudioDirector.KillLines.cs BoomLull).
//   OzBoomCheerProof.Run(seconds)   in any level (Play): waves of enemy MEDIUM / BIG vehicles and guns (medium tank, mech, heavy, MLRS,
//   APC, crane, gun) with robot squads at the centre of the screen, and our heavy / medium tanks and squads sent at them; counts the big
//   kills on camera, the cheers asked / taken / said / dropped (with why), the delay from the kill to each cheer, the chatter held for
//   a waiting cheer, and whether a cheer ever started over the announcer, the commander or another robot voice.
//   OzBoomCheerProof.Running / Report / Last
// View only: it never changes a rule of the simulation (it spawns units and gives orders as the cheat spawner and a player do).
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public static class OzBoomCheerProof
    {
        public static bool Running { get; private set; }
        public static string Last { get; private set; } = "";
        public static string Report { get; private set; } = "not run";
        static StringBuilder sb;
        static readonly HashSet<string> Cheers = new HashSet<string> { "vo_boom", "vo_boom_brad" };

        public static string Run(float seconds = 210f)
        {
            var ad = AudioDirector.I;
            if (!ad || !ZGame.I || ZGame.I.World == null) return "no level (enter Play on a level)";
            if (Running) return "already running";
            Running = true; Report = "running";
            ad.StartCoroutine(All(ad, seconds));
            return "running " + seconds.ToString("0") + " s";
        }

        static void Info(string s) { string r = "[boom_cheer] " + s; sb.AppendLine(r); Debug.Log(r); }

        static ZObject Spawn(ZGame g, string cat, string type, int tx, int ty, Team team, int amount = -1)
        {
            var W = g.World;
            OzCheatSpawner.FreeTile(W, tx, ty, cat == "robot" ? 1 : 2, cat == "robot", out tx, out ty);
            var o = cat == "robot" ? W.CreateRobotGroup(type, tx * 16, ty * 16, team, amount) : W.CreateObject(cat, type, tx * 16, ty * 16, team);
            if (o == null) return null;
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            return o;
        }

        static IEnumerator All(AudioDirector ad, float seconds)
        {
            sb = new StringBuilder();
            var g = ZGame.I; var W = g.World; var cam = Camera.main;
            Team me = g.player, foe = me == Team.Red ? Team.Blue : Team.Red;
            float T0 = Time.unscaledTime;
            if (!cam || !g.GroundPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var gp)) { Info("FAIL no camera / ground under the centre of the screen"); Finish(T0, false); yield break; }
            var px = ZGame.WorldToPx(gp); int cx = px.x / 16, cy = px.y / 16;
            Info("level " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", we are " + me + ", screen centre tile " + cx + "," + cy
                 + ", robot chatter " + AudioDirector.RobotChatter.Names[AudioDirector.RobotChatter.Level] + ", voices " + (ad.voices ? "on" : "OFF")
                 + "; BoomGap " + AudioDirector.BoomGap + " s, BoomChance " + AudioDirector.BoomChance + ", BoomLull " + AudioDirector.BoomLull + " s, BoomWait " + AudioDirector.BoomWait + " s");
            int bk0 = AudioDirector.BoomBigKills, ba0 = AudioDirector.BoomAsked, bt0 = AudioDirector.BoomTaken, b0 = AudioDirector.Booms, bd0 = AudioDirector.BoomDropped, bh0 = AudioDirector.BoomHeld, dl0 = AudioDirector.BoomDelays.Count;
            int qa0 = AudioDirector.QuipAsked, qs0 = AudioDirector.QuipSaid;
            var foeWaves = new[]
            {
                new[] { ("vehicle", ZMTank.Type, 7, -4), ("vehicle", "heavy", 8, 2), ("cannon", "gun", 9, -1), ("robot", "grunt", 7, 7) },
                new[] { ("vehicle", "medium", 7, -3), ("vehicle", "missile_launcher", 9, 3), ("vehicle", "apc", 8, -7), ("robot", "psycho", 7, 7) },
                new[] { ("vehicle", "crane", 7, 4), ("vehicle", ZMTank.Type, 8, -3), ("cannon", "gun", 9, 1), ("robot", "tough", 7, -8) },
                new[] { ("vehicle", "heavy", 7, -2), ("vehicle", "medium", 8, 4), ("vehicle", "missile_launcher", 9, -6), ("robot", "grunt", 7, 8) },
            };
            var ours = new List<ZObject>(); var foes = new List<ZObject>();
            int wave = 0, overAnn = 0, overCmd = 0, overRobot = 0, annDuring = 0; string prevAlert = null; bool cheerOn = false, rachelPrev = false;
            var overWhat = new List<string>();
            float waveAt = -100f;
            while (Time.unscaledTime - T0 < seconds)
            {
                bool foeLeft = false; foreach (var f in foes) if (f != null && !f.Destroyed && f.KillAt < 0) { foeLeft = true; break; }
                if (!foeLeft || Time.unscaledTime - waveAt > 45f)
                {
                    foes.Clear();
                    foreach (var t in foeWaves[wave % foeWaves.Length])
                    { var o = Spawn(g, t.Item1, t.Item2, cx + t.Item3, cy + t.Item4, foe, t.Item1 == "robot" ? 3 : -1); if (o != null) { foes.Add(o); foes.AddRange(o.Minions); } }
                    ours.RemoveAll(o => o == null || o.Destroyed || o.KillAt >= 0);
                    int veh = 0; foreach (var o in ours) if (o.Kind == ZKind.Vehicle) veh++;
                    var add = new List<(string, string, int, int)>();
                    if (veh < 3) { add.Add(("vehicle", "heavy", -8, -3)); add.Add(("vehicle", ZMTank.Type, -8, 3)); add.Add(("vehicle", "heavy", -9, 0)); }
                    add.Add(("robot", "grunt", -6, -6)); add.Add(("robot", "psycho", -6, 6));
                    foreach (var t in add) { var o = Spawn(g, t.Item1, t.Item2, cx + t.Item3, cy + t.Item4, me, t.Item1 == "robot" ? 3 : -1); if (o != null) ours.Add(o); }
                    yield return null;
                    foreach (var o in ours) if (o != null && !o.Destroyed) W.SendWaypoints(o.Owner, o, new[] { new ZWaypoint { mode = WpMode.Move, refId = -1, x = (cx + 8) * 16, y = cy * 16, attackTo = true, playerGiven = false } });
                    Info("wave " + (wave + 1) + " at " + (Time.unscaledTime - T0).ToString("0") + " s: " + foes.Count + " enemy units, " + ours.Count + " of our groups sent at them");
                    wave++; waveAt = Time.unscaledTime;
                }
                string a = ad.ProofAlertId;
                bool rach = ad.ProofRachelPlaying;
                if (a != null && a != prevAlert && Cheers.Contains(a))
                {   // a cheer started this frame
                    cheerOn = true;
                    if (rach) { overAnn++; overWhat.Add(a + " over the announcer"); }
                    if (AudioDirector.ExternalVoiceBusy != null && AudioDirector.ExternalVoiceBusy()) { overCmd++; overWhat.Add(a + " over the commander"); }
                    if (ad.ProofMainId != null) { overRobot++; overWhat.Add(a + " over " + ad.ProofMainId); }
                }
                if (a == null || !Cheers.Contains(a)) cheerOn = false;
                if (cheerOn && rach && !rachelPrev) annDuring++;
                prevAlert = a; rachelPrev = rach;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(AudioDirector.BoomWait + 0.5f);
            int bk = AudioDirector.BoomBigKills - bk0, ba = AudioDirector.BoomAsked - ba0, bt = AudioDirector.BoomTaken - bt0, b = AudioDirector.Booms - b0, bd = AudioDirector.BoomDropped - bd0, bh = AudioDirector.BoomHeld - bh0;
            var delays = new List<string>(); float maxDelay = 0f;
            for (int i = dl0; i < AudioDirector.BoomDelays.Count; i++) { delays.Add(AudioDirector.BoomDelays[i].ToString("0.00")); maxDelay = Mathf.Max(maxDelay, AudioDirector.BoomDelays[i]); }
            var log = OzSoundLog.Since(T0);
            var heard = new List<string>(); var drops = new List<string>();
            foreach (var e in log)
            {
                if (e.src == "VO" && (Cheers.Contains(e.key) || e.key == "vo_kill_quip") && e.outcome != null && e.outcome.StartsWith("PLAY"))
                    heard.Add((e.t - T0).ToString("0.0") + " s " + e.key + " " + e.clip);
                if (e.src == "dir" && Cheers.Contains(e.key) && e.outcome != null && e.outcome.StartsWith("cheer dropped")) drops.Add((e.t - T0).ToString("0.0") + " s " + e.outcome);
            }
            Info("RESULT " + (Time.unscaledTime - T0).ToString("0") + " s, " + wave + " waves: big kills on camera by our units " + bk + "; cheers asked (gap free) " + ba + ", taken (BoomChance) " + bt
                 + ", SAID " + b + ", dropped " + bd + "; said / asked " + (ba > 0 ? (100f * b / ba).ToString("0") : "-") + " %, said / taken " + (bt > 0 ? (100f * b / bt).ToString("0") : "-") + " %");
            Info("delay kill -> cheer (s): " + string.Join(", ", delays) + (delays.Count > 0 ? "; longest " + maxDelay.ToString("0.00") : ""));
            Info("chatter / quip lines held for a waiting cheer: " + bh + "; kill quips asked " + (AudioDirector.QuipAsked - qa0) + ", said " + (AudioDirector.QuipSaid - qs0));
            Info("cheers started over the announcer " + overAnn + ", over the commander " + overCmd + ", over another robot line " + overRobot + (overWhat.Count > 0 ? " (" + string.Join("; ", overWhat) + ")" : "") + "; the announcer started during a cheer " + annDuring);
            Info("drops: " + (drops.Count > 0 ? string.Join(" | ", drops) : "none"));
            Info("kill lines heard (sound log):");
            foreach (var h in heard) Info("  " + h);
            Finish(T0, overAnn == 0 && overCmd == 0 && overRobot == 0 && bt > 0 && b * 2 > ba);
        }

        static void Finish(float T0, bool ok)
        {
            Report = (ok ? "PASS" : "FAIL") + " (" + (Time.unscaledTime - T0).ToString("0") + " s)";
            sb.AppendLine("[boom_cheer] VERDICT: " + Report);
            Last = sb.ToString(); Running = false;
            Debug.Log("[boom_cheer] proof done\n" + Last);
        }
    }
}
