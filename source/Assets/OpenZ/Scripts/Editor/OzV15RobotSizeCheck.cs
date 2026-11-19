#if !OPENZ_HARNESS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of the OPTIONS row ROBOT SIZE (UI/OzRobotSizeOption.cs) and its drawing (View/OzRobotScale.cs).
    /// Menu OpenZ/Checks/V1.5/R3.25 Robot size:
    ///  * Edit mode (no Play), also the batch entry <see cref="Batch"/>: R1 .. R10 (the row's numbers, the steps, the exact 100 % on 200
    ///    transforms x 120 frames, the rule at a smaller size, the readers at 100 %, the tab and tip, the source, the robot prefabs' root at the ground).
    ///  * Play in a level: P1 .. P4 on the level's own robots (the game held while it measures, put back at the end; nothing saved).
    /// One report per part, in Logs/robot_size_check_edit.txt / Logs/robot_size_check_play.txt (the project's Logs folder).
    /// </summary>
    public static class OzV15RobotSizeCheck
    {
        const string Tag = "[OpenZ V1.5 R3.25 robot size]";
        public static string Report = "not run";

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzRobotSizeCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Begin() { ok = 0; bad = 0; notes.Length = 0; }
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
        static void Info(string what) => notes.Append("\n  [info] ").Append(what);
        static bool End(string part)
        {
            Report = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            try
            {
                var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs"); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "robot_size_check_" + part + ".txt"), Report + "\n");
            }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
            return bad == 0;
        }

        [MenuItem("OpenZ/Checks/V1.5/R3.25 Robot size (Edit: numbers, exact 100 %, source; Play: the level's robots)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { EditCheck(); return; }
            if (!ZGame.I || ZGame.I.World == null) { Debug.LogError(Tag + " start a level first"); return; }
            Report = "running"; R().StartCoroutine(PlayRoutine());
        }

        /// <summary>Batch: -executeMethod OpenZ.EditorTools.OzV15RobotSizeCheck.Batch (edit mode; exit 0 = PASS).</summary>
        public static void Batch()
        {
            bool pass = false;
            try { pass = EditCheck(); }
            catch (Exception e) { Debug.LogError(Tag + " batch: " + e); }
            EditorApplication.Exit(pass ? 0 : 1);
        }

        // ------------------------------------------------------------------ helpers
        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        static int Count(string s, string what) { int n = 0, i = 0; while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }
        static bool Bits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static bool Bits(Vector3 a, Vector3 b) => Bits(a.x, b.x) && Bits(a.y, b.y) && Bits(a.z, b.z);

        static readonly string[] RobotPrefabs =
        {
            "Assets/OpenZ/Prefabs/Z_bs01_grunt.prefab", "Assets/OpenZ/Prefabs/Z_bs01_psycho.prefab", "Assets/OpenZ/Prefabs/Z_bs01_tough.prefab",
            "Assets/OpenZ/Prefabs/Z_bs01_sniper.prefab", "Assets/OpenZ/Prefabs/Z_bs01_pyro.prefab", "Assets/OpenZ/Prefabs/Z_bs01_laser.prefab",
            "Assets/OpenZ/Prefabs/Z_bs01_electrobot.prefab", "Assets/OpenZ/Prefabs/Z_robot.prefab", "Assets/OpenZ/Prefabs/Z_tough.prefab",
            "Assets/OpenZ/Resources/OpenZ/HeroSquad/Z_hero_alan.prefab", "Assets/OpenZ/Resources/OpenZ/HeroSquad/Z_hero_brad.prefab",
            "Assets/OpenZ/Resources/OpenZ/HeroSquad/Z_bs01_evilzod.prefab",
        };

        // ------------------------------------------------------------------ edit mode
        public static bool EditCheck()
        {
            Begin();
            var S = OzRobotScale.Steps; var V = OpenZ.OzVehicleScaleTest.Steps;

            // R1 the row's numbers
            bool same = S.Length == V.Length; for (int i = 0; same && i < S.Length; i++) same = Bits(S[i], V[i]);
            Check(same && S.Length == 9 && Bits(S[0], 1f) && Mathf.RoundToInt(S[8] * 100f) == 60,
                  $"R1a the steps are the vehicle size row's: {string.Join(" / ", Array.ConvertAll(S, s => Mathf.RoundToInt(s * 100f) + " %"))}");
            Check(OzRobotSizeOption.Label == "ROBOT SIZE" && OzRobotSizeOption.Pref == "oz_robot_size" && OzRobotSizeOption.DefaultPercent == 100
                  && OzRobotScale.StepOf(OzRobotSizeOption.DefaultPercent) == 0,
                  "R1b ROBOT SIZE, saved as oz_robot_size, default 100 % = step 0 (full size)");
            Check(OpenZ.OzVehicleSizeOption.DefaultPercent == 90 && OpenZ.OzVehicleSizeOption.Label == "VEHICLE SIZE", "R1c the vehicle size row is unchanged (default 90 %)");

            // R2 a saved value off the steps reads as the default
            int[] tryP = { 100, 95, 60, 85, 55, 105, 97, 0, -5, 61 }; int[] wantP = { 100, 95, 60, 85, 100, 100, 100, 100, 100, 100 };
            bool vOk = true; var vs = new StringBuilder();
            for (int i = 0; i < tryP.Length; i++) { int g = OzRobotSizeOption.Valid(tryP[i]); vOk &= g == wantP[i]; vs.Append(tryP[i]).Append("->").Append(g).Append(' '); }
            Check(vOk, "R2 saved values as the row reads them: " + vs);

            // R3 the steps stop at both ends
            int st = 0; var down = new List<int>(); var up = new List<int>();
            for (int i = 0; i < 11; i++) { st = OzRobotScale.StepAfter(st, -1); down.Add(Mathf.RoundToInt(S[st] * 100f)); }
            for (int i = 0; i < 11; i++) { st = OzRobotScale.StepAfter(st, +1); up.Add(Mathf.RoundToInt(S[st] * 100f)); }
            bool stepsOk = down[0] == 95 && down[7] == 60 && down[10] == 60 && up[0] == 65 && up[7] == 100 && up[10] == 100 && OzRobotScale.StepAfter(3, 0) == 3;
            for (int i = 0; i < S.Length; i++) stepsOk &= OzRobotScale.StepOf(Mathf.RoundToInt(S[i] * 100f)) == i;
            Check(stepsOk, $"R3 smaller: {string.Join(",", down)}; bigger: {string.Join(",", up)}");

            // R4 the scale rule: its own size bit for bit at 100 %
            var rnd = new System.Random(325); int wantOk = 0, wantN = 0;
            for (int i = 0; i < 500; i++)
            {
                var b = new Vector3((float)(0.2 + rnd.NextDouble() * 3), (float)(0.2 + rnd.NextDouble() * 3), (float)(0.2 + rnd.NextDouble() * 3));
                wantN++; if (Bits(OzRobotScale.Want(b, 1f), b)) wantOk++;
                foreach (var k in S) { wantN++; if (Bits(OzRobotScale.Want(b, k), k == 1f ? b : b * k)) wantOk++; }
            }
            Check(wantOk == wantN, $"R4 Want(base, k): {wantOk} of {wantN} exact (k = 1: the base itself)");

            // R5 at 100 %: 200 transforms x 120 frames, with other writers (a hull grow, a melt) on some frames: the tool writes NOTHING
            var hold = new GameObject("OzRobotSizeCheck_hold") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                int n = 200, frames = 120, toolChanged = 0, calls = 0, foreignWrites = 0; int w0 = OzRobotScale.Writes;
                var ts = new Transform[n]; var ps = new OzRobotScale.Probe[n];
                for (int i = 0; i < n; i++)
                {
                    var c = new GameObject("t" + i) { hideFlags = HideFlags.HideAndDontSave }; c.transform.SetParent(hold.transform, false);
                    c.transform.localScale = new Vector3((float)(0.5 + rnd.NextDouble()), (float)(0.5 + rnd.NextDouble()), (float)(0.5 + rnd.NextDouble()));
                    ts[i] = c.transform; ps[i] = new OzRobotScale.Probe();
                }
                for (int f = 0; f < frames; f++)
                    for (int i = 0; i < n; i++)
                    {
                        if ((i + f) % 17 == 0) { ts[i].localScale = ts[i].localScale * 0.97f; foreignWrites++; }        // another script's write this frame
                        var before = ts[i].localScale;
                        OzRobotScale.ApplyForCheck(ts[i], ps[i], 1f, (i % 5) == 0); calls++;
                        if (!Bits(before, ts[i].localScale)) toolChanged++;
                    }
                bool applied1 = true; for (int i = 0; i < n; i++) applied1 &= Bits(ps[i].Applied, 1f);
                Check(toolChanged == 0 && OzRobotScale.Writes == w0 && applied1,
                      $"R5 100 %: {calls} frames of the rule on {n} transforms ({foreignWrites} writes by other scripts in between): {toolChanged} scales changed by the tool, {OzRobotScale.Writes - w0} writes counted, every drawn size 1");
                Check(OzRobotScale.TakesIn(true, true, 1f) == false && OzRobotScale.TakesIn(true, false, 1f) == false && OzRobotScale.TakesIn(false, true, 0.8f) == false
                      && OzRobotScale.TakesIn(true, false, 0.8f) == false && OzRobotScale.TakesIn(true, true, 0.8f),
                      "R5b at 100 % no robot view is ever taken in (only a shown robot view, only below 100 %)");

                // R6 at a smaller size: the rule on fresh transforms
                int r6 = 0, r6n = 0;
                for (int i = 0; i < 40; i++)
                {
                    var t = ts[i]; var p = new OzRobotScale.Probe(); var baseS = t.localScale; float k = S[1 + i % 8];
                    OzRobotScale.ApplyForCheck(t, p, k, false); r6n++; if (Bits(t.localScale, baseS * k) && Bits(p.Applied, k)) r6++;
                    OzRobotScale.ApplyForCheck(t, p, k, false); r6n++; if (Bits(t.localScale, baseS * k)) r6++;                       // steady: no change
                    var other = baseS * 0.93f; t.localScale = other;                                                                    // another script writes a new full size (off the steps)
                    OzRobotScale.ApplyForCheck(t, p, k, false); r6n++; if (Bits(t.localScale, other * k)) r6++;
                    OzRobotScale.ApplyForCheck(t, p, 1f, false); r6n++; if (Bits(t.localScale, other) && Bits(p.Applied, 1f)) r6++;   // back to 100 %: its exact own size
                    var dp = new OzRobotScale.Probe(); var dt = ts[i + 40]; var dBase = dt.localScale;                                // a second robot that dies
                    OzRobotScale.ApplyForCheck(dt, dp, k, false);
                    var melt = OzRobotScale.Want(new Vector3(1f, 0.5f, 1f), k); dt.localScale = melt;                                 // its own death write (made with Dying)
                    OzRobotScale.ApplyForCheck(dt, dp, k, true); r6n++; if (Bits(dt.localScale, melt) && !Bits(dBase, melt)) r6++;
                    var keep = t.localScale; int changed = 0;
                    for (int f = 0; f < 60; f++) { OzRobotScale.ApplyForCheck(t, p, 1f, false); if (!Bits(keep, t.localScale)) changed++; }
                    r6n++; if (changed == 0) r6++;
                }
                Check(r6 == r6n, $"R6 below 100 %: drawn = own size x k, another script's write = the new own size, a dying robot's own write kept, back at 100 % the exact own size, then nothing written: {r6}/{r6n}");
            }
            finally { UnityEngine.Object.DestroyImmediate(hold); }

            // R7 the readers at 100 % (edit mode: nothing applied) give back exactly what they are given
            int r7 = 0, r7n = 0;
            for (int i = 0; i < 300; i++)
            {
                float x = (float)(rnd.NextDouble() * 5.0); var vv = new Vector3(x, x * 0.37f, x + 1f);
                r7n += 4;
                if (Bits(OzRobotScale.Times(x, null), x)) r7++;
                if (Bits(OzRobotScale.Dying(null, vv), vv)) r7++;
                if (Bits(OzRobotScale.Unscaled(null, x), x)) r7++;
                if (Bits(OzRobotScale.Of((UnitView)null), 1f) && Bits(OzRobotScale.Of((ZObject)null), 1f)) r7++;
            }
            Check(!OzRobotScale.AnyApplied && r7 == r7n && Bits(OzRobotScale.ExitScaleX(null), 1f), $"R7 at 100 % every reader returns its input / 1 exactly: {r7}/{r7n}");
            int fs0 = OzRobotScale.FiguresScaled; var fig = new GameObject("fig") { hideFlags = HideFlags.HideAndDontSave }; fig.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            int was = OzRobotScale.Step; OzRobotScale.SetStepForCheck(0); OzRobotScale.NewFigure(fig); bool figSame = Bits(fig.transform.localScale, new Vector3(1.3f, 1.3f, 1.3f)) && OzRobotScale.FiguresScaled == fs0;
            OzRobotScale.SetStepForCheck(4); OzRobotScale.NewFigure(fig); bool figSmall = Bits(fig.transform.localScale, new Vector3(1.3f, 1.3f, 1.3f) * S[4]);
            OzRobotScale.SetStepForCheck(was); UnityEngine.Object.DestroyImmediate(fig);
            Check(figSame && figSmall, "R7b a new robot figure (BoardingView.SpawnFigure): untouched at 100 %, x 0.8 at 80 %");

            // R8 tab, place, tip
            string tip = OzSettingTips.For(OzRobotSizeOption.Label) ?? "";
            Check(OzSettingTips.TabOf(OzRobotSizeOption.Label) == OzSettingTips.TabGame && OzSettingTips.TabOrder(OzRobotSizeOption.Label) == OzSettingTips.TabOrder(OpenZ.OzVehicleSizeOption.Label) + 1,
                  $"R8a GAME tab, directly under VEHICLE SIZE (place {OzSettingTips.TabOrder(OzRobotSizeOption.Label)})");
            Check(tip == OzRobotSizeOption.Tip && tip.StartsWith("ROBOT SIZE|") && tip.Contains("Default 100 %") && tip.Contains("Only the look changes"), "R8b its tip: " + tip);
            Check(OzSettingTips.TabOrder(OzTacticalPause.Label) == OzSettingTips.TabOrder(OzRobotSizeOption.Label) + 1, "R8c TACTICAL PAUSE is still the GAME tab's last row, under ROBOT SIZE");

            // R9 the source
            string mexe = Src("UI/OpenZMainMenu.MenuExe.cs"), pm = Src("UI/OpenZPauseMenu.cs"), tool = Src("View/OzRobotScale.cs"), opt = Src("UI/OzRobotSizeOption.cs");
            Check(Count(mexe, "OzRobotSizeOption.Label") == 1 && mexe.IndexOf("OzRobotSizeOption.Label", StringComparison.Ordinal) < mexe.IndexOf("AddCycle(p, toggles.Count, OzVehicleSizeOption.Label", StringComparison.Ordinal)
                  && Count(pm, "OzRobotSizeOption.Label") == 2 && Count(pm, "OzRobotSizeOption.Step(") == 2
                  && pm.IndexOf("AddToggle(p, toggles.Count, OzRobotSizeOption.Label", StringComparison.Ordinal) > pm.IndexOf("AddToggle(p, toggles.Count, OzVehicleSizeOption.Label", StringComparison.Ordinal),
                  "R9a the row in both menus (main: one cycle row; pause: the row + its value text, the vehicle size row's clicks)");
            Check(tool.Length > 0 && Count(tool, "PlayerPrefs") == 0 && Count(tool, ".position =") == 0 && Count(tool, "RuntimeInitializeLoadType.AfterSceneLoad") == 1
                  && Count(opt, "PlayerPrefs.SetInt(Pref, percent)") == 1 && Count(tool, "OzVehicleScaleTest") == 0 && Count(opt, "OzVehicleScaleTest") == 0,
                  "R9b the size is saved only through the row, the tool writes no position, and neither file names the vehicle tool (its own check counts its readers)");
            int simRefs = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts/Sim"), "*.cs", SearchOption.AllDirectories))
            { var s = File.ReadAllText(f); if (s.Contains("OzRobotScale") || s.Contains("OzRobotSizeOption") || s.Contains("oz_robot_size")) simRefs++; }
            Check(simRefs == 0, $"R9c no rules file (Scripts/Sim) reads the row or the tool ({simRefs})");
            var hooks = new (string file, string text, int n)[]
            {
                ("View/OzHeroSquadView.cs", "OzRobotScale.Times(TallOwn(o), o)", 1),
                ("View/UnitView.cs", "1.25f * OzRobotScale.Of(this)", 1), ("View/UnitView.cs", "0.55f * OzRobotScale.Of(this)", 1), ("View/UnitView.cs", "0.95f * OzRobotScale.Of(this)", 1),
                ("View/UnitView.cs", "OzRobotScale.Dying(this, new Vector3(1, Mathf.Lerp(1f, 0.12f, k), 1))", 1),
                ("View/UnitView.cs", "float k = OzVehicleScaleTest.Of(this), h = ", 1), ("View/UnitView.cs", "1.0f * OzVehicleScaleTest.Of(this)", 1),
                ("View/UnitView.LavaMelt.cs", "OzRobotScale.Dying(this, new Vector3(spread,", 1), ("View/UnitView.LavaMelt.cs", "OzRobotScale.Unscaled(this, transform.localScale.y)", 1),
                ("View/BoardingView.cs", "OzRobotScale.NewFigure(go);", 1), ("View/BoardingView.cs", "g.baseScale = OzRobotScale.ExitScaleX(live);", 1), ("View/BoardingView.cs", "1.1f * OzRobotScale.Of(v)", 1),
                ("View/BoardingView.ShuttleDoorway.cs", "walker.s1 = OzRobotScale.ExitScaleX(live);", 1),
                ("View/OpenZWading.cs", "w.heightK = OzRobotScale.Of(w.v);", 1), ("View/OpenZWading.cs", "if (rsNow != rsAt) H = H / rsAt * rsNow;", 1),
                ("View/VeteranMarks.cs", "OzRobotScale.Times(2.4f, o)", 1),
            };
            var hs = new StringBuilder(); bool hOk = true;
            foreach (var h in hooks) { int c = Count(Src(h.file), h.text); hOk &= c == h.n; if (c != h.n) hs.Append(h.file).Append(": '").Append(h.text).Append("' x").Append(c).Append("; "); }
            Check(hOk, $"R9d the {hooks.Length} view hooks, each once, in the form that gives the old number at 100 % {hs}");

            // R10 every robot prefab has its feet at its root: drawn smaller about the root it stays on the ground
            var feet = new StringBuilder(); bool feetOk = true; int found = 0;
            foreach (var path in RobotPrefabs)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!pf) { feet.Append(Path.GetFileNameWithoutExtension(path)).Append(" (not in this project) "); continue; }
                found++;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf); inst.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    float lo = float.PositiveInfinity, hi = float.NegativeInfinity; int nb = 0;   // the foot / toe bones in the prefab's own pose (a skinned mesh's edit-mode bounds are padded)
                    foreach (var b in inst.GetComponentsInChildren<Transform>(true))
                    {
                        string bn = b.name.ToLowerInvariant();
                        if (bn.Contains("foot") || bn.Contains("toe")) { lo = Mathf.Min(lo, b.position.y); nb++; }
                        if (bn.Contains("head")) hi = Mathf.Max(hi, b.position.y);
                    }
                    bool okFeet = nb > 0 && lo >= -0.1f && lo <= 0.6f && hi > 0.8f;   // the foot joints sit at the ankles just above the root (big boots: up to 0.5 m), never below it
                    feetOk &= okFeet;
                    feet.Append(pf.name).Append(" lowest foot bone ").Append(nb > 0 ? lo.ToString("0.00") : "none").Append(" m, head ").Append(hi.ToString("0.00")).Append(" m").Append(okFeet ? "" : " (FAIL)").Append("; ");
                }
                finally { UnityEngine.Object.DestroyImmediate(inst); }
            }
            Check(feetOk && found >= 7, $"R10 {found} robot prefabs, the root is the ground point (the lowest foot joint 0 .. 0.6 m above it, none below): scaled about the root, a robot stays on the ground: {feet}");
            return End("edit");
        }

        // ------------------------------------------------------------------ play: the level's own robots
        sealed class Snap { public UnitView v; public Vector3 scale; public float tall, aimUp; }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static List<Snap> Robots(ZGame g)
        {
            var list = new List<Snap>();
            foreach (var kv in g.Views)
            {
                var v = kv.Value;
                if (!OzRobotScale.Includes(v) || !v.gameObject.activeInHierarchy || v.Dying || v.Obj.Destroyed) continue;
                list.Add(new Snap { v = v, scale = v.transform.localScale, tall = OzHeroSquadView.Tall(v.Obj), aimUp = v.AimPos().y - v.transform.position.y });
            }
            return list;
        }

        static IEnumerator PlayRoutine()
        {
            Begin();
            var g = ZGame.I; bool wasPaused = g.paused; int stepAtStart = OzRobotScale.Step;
            g.paused = true;
            // P1 at 100 %: the tool takes nothing in and writes nothing
            OzRobotScale.SetStepForCheck(0); yield return Frames(30);
            var snap = Robots(g); int w0 = OzRobotScale.Writes, t0 = OzRobotScale.Taken;
            yield return Frames(120);
            int moved = 0, alive = 0; foreach (var s in snap) { if (!s.v || !s.v.gameObject.activeInHierarchy || s.v.Dying) continue; alive++; if (!Bits(s.v.transform.localScale, s.scale)) moved++; }
            Check(snap.Count > 0 && OzRobotScale.Writes == w0 && OzRobotScale.Taken == t0 && !OzRobotScale.AnyApplied && moved == 0,
                  $"P1 100 %: {snap.Count} robots, 120 frames: {OzRobotScale.Writes - w0} writes, {OzRobotScale.Taken - t0} taken in, {moved} of {alive} root scales changed");
            // P2 80 %: every robot drawn at 0.8 of its own size; its boxes, ring, bar and aim point with it
            OzRobotScale.SetStepForCheck(4); float k = OzRobotScale.Factor; yield return Frames(10);
            int sOk = 0, tOk = 0, aOk = 0, n2 = 0;
            foreach (var s in snap)
            {
                if (!s.v || !s.v.gameObject.activeInHierarchy || s.v.Dying) continue; n2++;
                if (Bits(s.v.transform.localScale, s.scale * k)) sOk++;
                if (Mathf.Abs(OzHeroSquadView.Tall(s.v.Obj) - s.tall * k) < 1e-5f) tOk++;
                if (Mathf.Abs((s.v.AimPos().y - s.v.transform.position.y) - s.aimUp * k) < 1e-4f) aOk++;
            }
            Check(n2 > 0 && sOk == n2 && tOk == n2 && aOk == n2 && OzRobotScale.AnyApplied,
                  $"P2 80 %: root scale = own x 0.8 {sOk}/{n2}; pick box / frame / ring / bar size (Tall) x 0.8 {tOk}/{n2}; aim point x 0.8 {aOk}/{n2}");
            // P3 back to 100 %: the exact own size, nothing held
            OzRobotScale.SetStepForCheck(0); yield return Frames(10);
            int back = 0, n3 = 0;
            foreach (var s in snap) { if (!s.v || !s.v.gameObject.activeInHierarchy || s.v.Dying) continue; n3++; if (Bits(s.v.transform.localScale, s.scale) && Bits(OzHeroSquadView.Tall(s.v.Obj), s.tall)) back++; }
            Check(n3 > 0 && back == n3 && !OzRobotScale.AnyApplied, $"P3 back at 100 %: {back}/{n3} robots at their exact own size and boxes, nothing held");
            // P4 the rules never saw it
            Info($"robots measured {snap.Count}; size at the start {OzRobotScale.Steps[stepAtStart] * 100f:0} % (put back, nothing saved)");
            OzRobotScale.SetStepForCheck(stepAtStart); g.paused = wasPaused;
            yield return Frames(2);
            End("play");
        }
    }
}
#endif
