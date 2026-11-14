using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.3 shots_over_walls: the one switch (ShotLine.OverWalls) and the counted checks (View/ShotsOverWallsCheck.cs). Play reloads the scripts here, so the row keeps its
    /// value in SessionState and sets it back after each reload. Until the row is used, the word in ShotLine.cs decides, in the Editor as in a build. Nothing in Options,
    /// nothing players can see: to ship it off, change that word to false.</summary>
    static class OzShotsOverWallsMenu
    {
        const string Row = "OpenZ/V1.3/Shots Over Walls: On", Key = "OpenZ.ShotsOverWalls.On", Tag = "[OpenZ ShotsOverWalls]";

        [InitializeOnLoadMethod]
        static void Restore() => ShotLine.OverWalls = SessionState.GetBool(Key, ShotLine.OverWalls);

        [MenuItem(Row)]
        static void Flip()
        {
            bool on = !ShotLine.OverWalls;
            ShotLine.OverWalls = on; SessionState.SetBool(Key, on);
            Debug.Log(Tag + " " + (on ? "ON: shots over a platform or fort draw over it" : "OFF: every shot takes the old path"));
        }

        [MenuItem(Row, true)]
        static bool FlipState() { Menu.SetChecked(Row, ShotLine.OverWalls); return true; }

        static void SetFolder()
        {
            if (string.IsNullOrEmpty(ShotsOverWallsCheck.Folder))
                ShotsOverWallsCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/open_fixes_31oct/shots_over_walls/proof"));
        }

        [MenuItem("OpenZ/V1.3/Shots Over Walls: Check (Play, the unit editor open)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            SetFolder(); ShotsOverWallsCheck.Begin();
        }

        [MenuItem("OpenZ/V1.3/Shots Over Walls: fort line (Play, a level with a fort)")]
        static void FortLine()
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play on a level with a fort first"); return; }
            SetFolder(); ShotsOverWallsCheck.FortLine();
        }

        [MenuItem("OpenZ/V1.3/Shots Over Walls: fight count (Play, a level with a fight)")]
        static void Fight()
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play on a level with a fight first"); return; }
            SetFolder(); ShotsOverWallsCheck.BeginFight();
        }

        [MenuItem("OpenZ/V1.3/Shots Over Walls: last report")]
        static void Last() => Debug.Log(ShotsOverWallsCheck.Running ? Tag + " running" : ShotsOverWallsCheck.Report);
    }
}
