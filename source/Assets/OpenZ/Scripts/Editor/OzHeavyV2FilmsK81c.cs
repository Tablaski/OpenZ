#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Heavy tank v2, two more Play tests (1.2 items 132 and 137 / 146): menu OpenZ/Heavy Tank v2/13 and 14 (HeavyV2Films, L02, the game's
    /// default play zoom, 1920 x 1080 with sound; each opens the level, enters Play and stops Play itself).
    ///  13 the turret in a fight: a driving v2 among moving grunts that die one by one; the turret code before k81b, k81b's steady turret
    ///     alone, then with the world-heading slew (View/UnitAim.Combat.cs); the heading counted every frame;
    ///  14 the tracks stay on the ground: v1 and v2 driving and firing on open ground and over a river bridge's ramps, the planting
    ///     (View/UnitView.GroundGear.cs: the tracks on the ground, only the hull and turret rock) off as the control, then on; the tracks
    ///     against the ground and the hull over the tracks every frame.
    /// </summary>
    public static class OzHeavyV2FilmsK81c
    {
        const string M = "OpenZ/Heavy Tank v2/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity";

        [MenuItem(M + "13 Test: the turret in a fight never shakes (L02, item 132)", false, 52)] static void Combat() => Film("combat");
        [MenuItem(M + "14 Test: the tracks stay on the ground, only the hull rocks (L02, items 137 / 146)", false, 53)] static void Plant() => Film("plant");

        static void Film(string job)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzHeavyV2] stop Play first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
