// Editor only. Play on the MainMenu scene, then the menu:
// the Unit editor opens, its SPECIAL tab is shown and photographed at 1920 x 1080, 1600 x 900 and 1280 x 720 (the Game view's fixed sizes),
// then the counted drop test: for each of the seven test explosions, on a clear field, three neutral vehicles stand 4 to 5 m from the
// drop point (no side, so nothing fights), the explosion goes off, 8 s later every vehicle's health is read. PASS = every drop hurt all
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.EditorTools
{
    public static class OzV15UeBlastsPalette
    {
        static IEnumerator<double> run; static double waitUntil; static string outDir; static System.Text.StringBuilder log; static int fails;

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Seven palette shots + drop test (MainMenu, Play)")]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzV15UeBlastsPalette] enter Play on the MainMenu scene first"); return; }
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/owner_frames/explosions6/palette_r6")).Replace('\\', '/');
            Directory.CreateDirectory(outDir); log = new System.Text.StringBuilder(); fails = 0;
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); Done(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION " + e); fails++; more = false; }
            if (!more) { Done(); return; }
            waitUntil = EditorApplication.timeSinceStartup + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null;
            Log(fails == 0 ? "ALL PASS" : fails + " FAIL");
            try { File.WriteAllText(Path.Combine(outDir, "palette_drop_test_log.txt"), log.ToString()); } catch (Exception) { }
            Debug.Log("[OzV15UeBlastsPalette] DONE\n" + log);
        }

        static void Log(string s) { log.AppendLine(s); Debug.Log("[OzV15UeBlastsPalette] " + s); }

        /// <summary>Selects (adds when missing) the Game view's fixed resolution w x h.</summary>
        public static bool GameViewSize(int w, int h)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var inst = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null, null);
            var group = sizesType.GetMethod("GetGroup").Invoke(inst, new object[] { (int)GameViewSizeGroupType.Standalone });
            var gt = group.GetType(); int total = (int)gt.GetMethod("GetTotalCount").Invoke(group, null), idx = -1;
            for (int i = 0; i < total; i++)
            {
                var sz = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)sz.GetType().GetProperty("width").GetValue(sz, null) == w && (int)sz.GetType().GetProperty("height").GetValue(sz, null) == h
                    && sz.GetType().GetProperty("sizeType").GetValue(sz, null).ToString() == "FixedResolution") idx = i;
            }
            if (idx < 0)
            {
                var gvs = asm.GetType("UnityEditor.GameViewSize"); var gvst = asm.GetType("UnityEditor.GameViewSizeType");
                var ns = gvs.GetConstructor(new[] { gvst, typeof(int), typeof(int), typeof(string) }).Invoke(new object[] { Enum.Parse(gvst, "FixedResolution"), w, h, "OZ " + w });
                gt.GetMethod("AddCustomSize").Invoke(group, new object[] { ns }); idx = total;
            }
            var gvType = asm.GetType("UnityEditor.GameView"); var gv = EditorWindow.GetWindow(gvType, false, null, false);
            gvType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gv, new object[] { idx, null });
            return true;
        }

        static IEnumerator<double> Run()
        {
            var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Log("FAIL no main menu"); fails++; yield break; }
            typeof(OpenZMainMenu).GetMethod("GoUnitEditor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(m, null);
            double t0 = EditorApplication.timeSinceStartup;
            while (!(OzUnitEditor.UiBuilt && SceneManager.GetActiveScene().name == OzUnitEditor.SceneName) && EditorApplication.timeSinceStartup - t0 < 90) yield return 0.25;
            yield return 2.5;
            var ed = OzUnitEditor.I;
            if (!ed || ed.World == null) { Log("FAIL the Unit editor did not open"); fails++; yield break; }
            OzUePlayground.ClearField();
            OzUeControls.SetTab(OzUeControls.Tabs[3]); yield return 1.0;
            Log("SPECIAL tab tiles: " + string.Join(", ", OzUeDock.TileLabels()[3]));
            foreach (var s in new[] { (1920, 1080), (1600, 900), (1280, 720) })
            {
                GameViewSize(s.Item1, s.Item2); yield return 2.0;
                OzUeControls.SetTab(OzUeControls.Tabs[3]); yield return 1.0;
                string f = Path.Combine(outDir, $"palette_special_{s.Item1}.png").Replace('\\', '/');
                ScreenCapture.CaptureScreenshot(f); yield return 1.0;
                Log($"shot {s.Item1} x {s.Item2}: {f} (screen {Screen.width} x {Screen.height})");
            }
            GameViewSize(1920, 1080); yield return 1.5;

            var W = ed.World; var mid = ZGame.PxToWorld(W.Map.width * 8, W.Map.height * 8);
            OzUeControls.SetPlaying(true);
            for (int t = 0; t < OzUeBlasts.Count; t++)
            {
                OzUePlayground.ClearField(); yield return 0.5;
                var made = new List<ZObject>();
                foreach (var p in new[] { new Vector3(-4f, 0f, 1f), new Vector3(4f, 0f, 1.5f), new Vector3(0.5f, 0f, -4.5f) })
                {
                    var d = OzUePlayground.Drop(t == 6 ? "heavy" : "light", Team.Neutral, mid + p, 1);
                    if (d.ok && d.made != null) foreach (var o in d.made) { o.Damage = 0; made.Add(o); }
                    else Log($"  {OzUeBlasts.Ids[t]}: a vehicle REFUSED ({d.why})");
                }
                var hp0 = made.Select(o => o.Health).ToList();
                yield return 0.5;
                bool ok = OzUeBlasts.TestDrop(t, mid.x, mid.z);
                yield return 8.0;
                int hurt = 0; var sb = new System.Text.StringBuilder();
                for (int k = 0; k < made.Count; k++) { int h = made[k].Destroyed ? 0 : made[k].Health; if (h < hp0[k]) hurt++; sb.Append($" {made[k].Type} {hp0[k]}->{h}{(made[k].Destroyed ? " destroyed" : "")};"); }
                bool pass = ok && made.Count == 3 && hurt == 3;
                if (!pass) fails++;
                Log($"{(pass ? "PASS" : "FAIL")} drop {t + 1} {OzUeBlasts.Ids[t]} (reach {OzUeBlasts.ReachM(t):0.#} m): set off {ok}, vehicles hurt {hurt} of {made.Count}:{sb}");
            }
            OzUePlayground.ClearField();
            Log(OzUeBlasts.Report());
        }
    }
}
