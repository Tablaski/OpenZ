// OpenZ (GPL-3.0) - version 1.5, level 18: one Play check at HARD as red and as blue (editor only, never in a build).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.5/L18 Proofs/4 HARD run red + blue (MainMenu, Play). Deploys level 18 at HARD with the briefings off, first as red, then
    /// as blue (the human side stands still), and lets each run <see cref="Secs"/> s of normal play. Every 2 s it reads the world: the
    /// computer's big tanks (Mammoth, Bizon) and heavy tanks: owner, place, health, moving, target; every mobile unit's last real move. A unit
    /// that has an order of its own (a waypoint) but has not moved 4 px in 30 s is listed as STUCK (1.5 kit centre_bridge_wait_r5: a unit
    /// with no order keeps its last order's point as CurrentTarget and is only idle, not stuck). The level of the menu, the PLAY AS BLUE box, the
    /// </summary>
    public static class OzL18HardRun
    {
        public static double Secs = 300;
        public static string Status = "idle";
        static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/unity_pass_20261113_R3")).Replace('\\', '/');
        static IEnumerator<double> run; static double waitUntil; static StringBuilder log;
        static double Now => EditorApplication.timeSinceStartup;

        [MenuItem("OpenZ/V1.5/L18 Proofs/4 HARD run red + blue (MainMenu, Play)", false, 7703)]
        public static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18HardRun] enter Play on the MainMenu scene first"); return; }
            Directory.CreateDirectory(Out + "/frames"); log = new StringBuilder(); Status = "running";
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { L("Play left: stopped"); Done(); return; }
            if (Now < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { L("EXCEPTION " + e); more = false; }
            if (!more) { Done(); return; }
            waitUntil = Now + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null;
            try { File.WriteAllText(Out + "/l18_hard_run.txt", log.ToString()); } catch (Exception) { }
            Status = "done " + Out + "/l18_hard_run.txt"; Debug.Log("[OzL18HardRun] " + Status);
        }

        static void L(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[OzL18HardRun] " + s); }
        static bool Busy => ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive || (ZodFlyover.Live != null && ZodFlyover.Live.Active);
        static bool Big(ZObject o) => o.Type == "mammoth" || o.Type == "bizon" || o.Type == "heavy";

        static IEnumerator<double> Run()
        {
            int prefBrief = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); bool blueWas = OzPlayBlue.Box;
            int diffWas = PlayerPrefs.GetInt(OpenZDifficulty.Key, 2), mayWas = PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0);
            L($"level 18 HARD run, {DateTime.Now:dd MMM yyyy HH:mm}; settings before: briefing {prefBrief}, PLAY AS BLUE {blueWas}, difficulty {diffWas} mayhem {mayWas}");
            foreach (var (name, asBlue) in new[] { ("red", false), ("blue", true) })
            {
                if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
                {
                    OzScenes.MainMenu(); double tm = Now;
                    while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && Now - tm < 60) yield return 0.2;
                    yield return 2.5;
                }
                var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>(); if (!m) { L(name + ": no main menu"); continue; }
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); OzPlayBlue.Box = asBlue;
                OpenZDifficulty.Choice = GameDifficulty.Hard; PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 0);
                int card = Array.FindIndex(OzLevelDef.All, d => d.Id == "L18");
                yield return 0.8; m.ProofSelect(); yield return 1.0; m.ProofFocus(card); yield return 0.8;
                double t0 = Now; m.ProofDeploy();
                while (Now - t0 < 150 && !(ZGame.I && ZGame.I.World != null && ZGame.I.level == "L18")) yield return 0.2;
                if (!(ZGame.I && ZGame.I.World != null && ZGame.I.level == "L18")) { L(name + ": level 18 did not load"); continue; }
                double tb = Now; while (Busy && Now - tb < 150) yield return 0.2;
                yield return 1.0;
                var w = ZGame.I.World; var me = ZGame.I.player;
                L($"{name}: loaded in {tb - t0:F1} s, human {me}, difficulty {OpenZDifficulty.Choice}, briefing over {!Busy}");
                var lastPos = new Dictionary<int, Vector2>(); var lastMove = new Dictionary<int, double>(); var stuckMax = new Dictionary<int, double>();
                var stuckWhat = new Dictionary<int, string>(); var bigSeen = new Dictionary<int, string>();
                var bigMoved = new Dictionary<int, float>(); var bigTarget = new Dictionary<int, int>(); var bigSamples = new Dictionary<int, int>();
                double s0 = w.Time, r0 = Now, nextRow = 0, nextShot = 30;
                while (Now - r0 < Secs)
                {
                    double t = Now - r0;
                    foreach (var o in w.Objects)
                    {
                        if (o == null || o.Destroyed || !o.IsMobile) continue;
                        var p = new Vector2((float)o.PosX, (float)o.PosY);
                        if (!lastPos.TryGetValue(o.Id, out var lp) || (p - lp).magnitude > 4f) { lastPos[o.Id] = p; lastMove[o.Id] = t; }
                        var ct = o.CurrentTarget; float toGo = new Vector2(ct.x - (float)o.PosX, ct.y - (float)o.PosY).magnitude;
                        double still = t - lastMove[o.Id];
                        if (still > 30 && toGo > 24f && o.Owner != Team.Neutral && o.Waypoints.Count > 0)   // 1.5 kit centre_bridge_wait_r5 (ADD5b): an order of its own; idle units are not stuck
                        { if (!stuckMax.TryGetValue(o.Id, out var sm) || still > sm) { stuckMax[o.Id] = still; stuckWhat[o.Id] = $"{o.Owner} {o.Type} #{o.Id} at ({o.PosX / 16:F1},{o.PosY / 16:F1}) tile, wants ({ct.x / 16},{ct.y / 16}), {toGo / 16:F1} tiles to go"; } }
                        if (Big(o))
                        {
                            if (!bigSeen.ContainsKey(o.Id)) { bigSeen[o.Id] = o.Type; bigMoved[o.Id] = 0; bigTarget[o.Id] = 0; bigSamples[o.Id] = 0; }
                            bigSamples[o.Id]++; if (o.AttackObject != null) bigTarget[o.Id]++;
                        }
                    }
                    foreach (var id in bigSeen.Keys.ToList()) { var o = w.Objects.FirstOrDefault(x => x != null && x.Id == id); if (o == null) continue; }
                    if (t >= nextRow)
                    {
                        nextRow += 20;
                        var bigs = w.Objects.Where(o => o != null && Big(o)).ToList();
                        L($"{name} t={t:F0}s sim {w.Time - s0:F0}s: " + string.Join("; ", bigs.Select(o => $"{o.Type}#{o.Id} {o.Owner}{(o.Destroyed ? " DEAD" : "")} hp {o.Health}/{o.MaxHealth} at ({o.PosX / 16:F1},{o.PosY / 16:F1}) {(o.IsMoving ? "moving" : "still")} target {(o.AttackObject != null ? o.AttackObject.Owner + " " + o.AttackObject.Type : "-")}"))
                          + $" | units red {w.Objects.Count(o => o != null && !o.Destroyed && o.IsUnit && o.Owner == Team.Red)} blue {w.Objects.Count(o => o != null && !o.Destroyed && o.IsUnit && o.Owner == Team.Blue)}");
                    }
                    if (t >= nextShot) { nextShot += 90; ScreenCapture.CaptureScreenshot($"{Out}/frames/hard_{name}_{t:F0}s.png"); }
                    yield return 2.0;
                }
                L($"{name}: {Secs:F0} s done (sim {w.Time - s0:F0} s). Big tanks seen: " + string.Join("; ", bigSeen.Keys.Select(id => { var o = w.Objects.FirstOrDefault(x => x != null && x.Id == id); return $"{bigSeen[id]}#{id} {(o != null ? o.Owner.ToString() : "?")}{(o == null || o.Destroyed ? " (destroyed)" : "")}: had a target in {bigTarget[id]} of {bigSamples[id]} samples"; })));
                var st = stuckMax.Where(kv => kv.Value > 30).OrderByDescending(kv => kv.Value).ToList();
                L($"{name}: STUCK (an order of its own, not moved 4 px for over 30 s; idle units without an order are not listed): {st.Count}" + (st.Count > 0 ? "\n   " + string.Join("\n   ", st.Take(20).Select(kv => $"{kv.Value:F0} s: {stuckWhat[kv.Key]}")) : ""));
                OzScenes.MainMenu(); yield return 3.0;
            }
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefBrief); OzPlayBlue.Box = blueWas;
            PlayerPrefs.SetInt(OpenZDifficulty.Key, diffWas); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, mayWas); PlayerPrefs.Save();
            L($"settings put back: briefing {PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1)}, PLAY AS BLUE {OzPlayBlue.Box}, difficulty {PlayerPrefs.GetInt(OpenZDifficulty.Key, 2)} mayhem {PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0)}");
        }
    }
}
