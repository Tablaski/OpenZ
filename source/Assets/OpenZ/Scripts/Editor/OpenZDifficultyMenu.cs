using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>Editor switch for the difficulty (PlayerPrefs "oz_difficulty"), same values as the menu: Easy / Medium / Hard.</summary>
    public static class OpenZDifficultyMenu
    {
        const string E = "OpenZ/Difficulty/Easy", M = "OpenZ/Difficulty/Medium", H = "OpenZ/Difficulty/Hard (exactly Z)", X = "OpenZ/Difficulty/Mayhem (Hard + fog of war)";   // 1.4 MAYHEM

        static void Set(GameDifficulty d) { OpenZDifficulty.Choice = d; Debug.Log($"OpenZ difficulty: {OpenZDifficulty.Label(d)} ({OpenZDifficulty.Hint(d)})"); }
        [MenuItem(E)] static void SetEasy() => Set(GameDifficulty.Easy);
        [MenuItem(M)] static void SetMedium() => Set(GameDifficulty.Medium);
        [MenuItem(H)] static void SetHard() => Set(GameDifficulty.Hard);
        [MenuItem(X)] static void SetMayhem() => Set(GameDifficulty.Mayhem);
        [MenuItem(E, true)] static bool CheckEasy() { Menu.SetChecked(E, OpenZDifficulty.Level == GameDifficulty.Easy); return true; }
        [MenuItem(M, true)] static bool CheckMedium() { Menu.SetChecked(M, OpenZDifficulty.Level == GameDifficulty.Medium); return true; }
        [MenuItem(H, true)] static bool CheckHard() { Menu.SetChecked(H, OpenZDifficulty.Choice == GameDifficulty.Hard); return true; }
        [MenuItem(X, true)] static bool CheckMayhem() { Menu.SetChecked(X, OpenZDifficulty.Choice == GameDifficulty.Mayhem); return true; }
    }
}
