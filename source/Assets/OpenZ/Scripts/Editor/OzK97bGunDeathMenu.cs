#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// k97b, item 176 (View/OzGunBreakup.cs: the gun falls apart at its death): the counted check and film of a v1 and a v2 gun dying side by
    /// side (View/HeavyV2Films.K97b.cs, L02, 1920 x 1080 with the game's sound, the RTS camera at the game's play zoom or at 32 m). Report,
    /// </summary>
    public static class OzK97bGunDeathMenu
    {
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity";

        [MenuItem("OpenZ/Gun v2/16 Test + film: v1 and v2 die side by side, their parts fly at the death (L02)", false, 55)] static void Death() => Film("gun_death");
        [MenuItem("OpenZ/Gun v2/16 Test + film: the same, close camera 32 m (L02)", false, 56)] static void DeathClose() => Film("gun_death_close");

        static void Film(string job)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzGunV2] stop Play first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzGun.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
