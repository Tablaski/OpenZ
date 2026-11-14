#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Heavy tank v2, the films and the old checks on v2 (1.2 items 110, 126, 129, 130, 132, 132b): menu OpenZ/Heavy Tank v2/4 .. 12.
    ///  4 .. 8 the films (HeavyV2Films, L02, the RTS camera, 1920 x 1080 with sound): the tread ends, a battle, boarding an empty v2
    ///    (and once with the camera close, 32 m, for the climb), one v2 fighting to its death, the driver up against snipers;
    ///  9 the heavy tank's older Play proof (HeavyLegacyProof) on L02 or L17, now on v2 (the switch);
    ///  11 the turret steadiness test (item 132.1: the heading every frame, v2 with the old code as the control, then v2 and v1 with the new);
    ///  12 the head and hull recoil test (items 132.2 / 132b: a red v2 and v1 firing, measured every frame round each shot pair, crops 1/30 s apart).
    /// The menus open the level and enter Play (Play stopped first); the films and the proofs stop Play themselves.
    /// </summary>
    public static class OzHeavyV2Films
    {
        const string M = "OpenZ/Heavy Tank v2/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity", L17 = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/heavy_v2/proof"));

        [MenuItem(M + "4 Film: the tread at the track ends (L02, RTS camera, crops 0.1 s apart)", false, 20)] static void Tread() => Film("tread");
        [MenuItem(M + "5 Film: a battle of v2 heavies (L02)", false, 21)] static void Battle() => Film("battle");
        [MenuItem(M + "6 Film: a robot boards an empty v2 (L02)", false, 22)] static void Board() => Film("board");
        [MenuItem(M + "6 Film: a robot boards an empty v2, close camera 32 m (L02, picture a)", false, 22)] static void BoardClose() => Film("board_close");
        [MenuItem(M + "7 Film: one v2 fights to its death (L02)", false, 23)] static void Fight() => Film("fight");
        [MenuItem(M + "8 Film: the driver up against snipers (L02)", false, 24)] static void Snipe() => Film("snipe");
        [MenuItem(M + "11 Test: the turret never shakes between targets (L02, item 132)", false, 50)] static void Turret() => Film("turret");
        [MenuItem(M + "12 Test: head kick and hull rock at each shot (L02, items 132 / 132b)", false, 51)] static void Recoil() => Film("recoil");

        static bool Ready()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzHeavyV2] stop Play first"); return false; }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        static void Film(string job)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "9 Older heavy tank proof on v2 (HeavyLegacyProof, L02)", false, 30)] static void LegacyL02() => Legacy("L02", L02);
        [MenuItem(M + "9 Older heavy tank proof on v2 (HeavyLegacyProof, L17)", false, 31)] static void LegacyL17() => Legacy("L17", L17);
        static void Legacy(string job, string scene)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString("oz_htproof_auto", job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "10 Testing panel self-test (Play, a level running)", false, 40)]
        static void SelfTest()
        {
            if (!Application.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning("[OzHeavyV2] enter Play on a level first"); return; }
            OzCheatSpawner.SelfTest(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f));
            double until = EditorApplication.timeSinceStartup + 30.0;
            void Poll()
            {
                string rep = OzCheatSpawner.TestReport;
                bool end = rep != "running" || EditorApplication.timeSinceStartup > until || !Application.isPlaying;
                if (!end) return;
                EditorApplication.update -= Poll;
                Directory.CreateDirectory(ProofDir);
                string txt = (rep == "running" ? "TIMEOUT (30 s) " : "") + rep;
                File.WriteAllText(Path.Combine(ProofDir, "cheat_selftest.txt"), txt + "\n");
                Debug.Log("[OzHeavyV2] testing panel self-test: " + txt);
            }
            EditorApplication.update += Poll;
        }
    }
}
#endif
