#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 181, editor side: menu OpenZ/Medium Tank/18, a short film of the light tank v2 and the medium tank side by side turning onto
    /// the same targets and firing (View/HeavyV2Films.K87d.cs: the take "turn_k87d" on L02, the RTS camera at the game's play height, the
    /// sound captured), with the counted turn times of both tanks. The medium's slower turret: View/UnitAim.K87d.cs; its bigger head kick:
    /// View/HeavyRecoil.K87d.cs.
    /// </summary>
    public static class OzMediumTankK87d
    {
        const string M = "OpenZ/Medium Tank/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity";

        [MenuItem(M + "18 Film: the light and the medium tank turn onto targets and fire (L02, RTS camera, sound)", false, 63)]
        static void FilmTurn()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzMediumTank] stop Play first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzMediumTank.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, "turn_k87d"); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
