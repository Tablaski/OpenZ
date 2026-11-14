using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUeMarks.cs): the field's object words in two short lines instead of one long one, and only while the pointer is on
    /// the object or it is picked: the obstacle "OBSTACLE  5405 / 10000 HP" over "Ctrl + right-click attacks it" (its bar alone when it was
    /// hit), the flag "FLAG  neutral" over "Robots take it by touch", an empty vehicle or gun "EMPTY LIGHT TANK  3378 / 3378 HP" over "A
    /// robot can climb in". The second line keeps the label's old height over its point, the first stands one line (16 u) over it; with
    /// the Background option "Name labels on every unit" the others show their name alone (one line). The picked unit's name label also
    /// says which stats it runs: "LASER  1868 / 2027 HP  ·  CUSTOM" (View/OzUeSets.Reset.cs LabelTag).
    /// </summary>
    internal sealed partial class OzUeMarks
    {
        /// <summary>Checks (the last frame): the obstacle's / the flag's / an empty vehicle's word lines drawn (2 = both lines of the one
        /// pointed at or picked), the obstacle's and the flag's first line, the picked unit's name label.</summary>
        internal static int ObstacleWords, FlagWords, EmptyWords;
        internal static string LastObstacleTop, LastFlagTop, LastEmptyTop, LastUnitLabel;

        /// <summary>One line (top alone, hint null) at `up` u over the point; two lines: the hint there, the top line 16 u over it.</summary>
        void Words(Vector3 at, string top, string hint, Color c, float up)
        {
            if (hint == null) { OzUeDraw.Label(at, top, c, OzUePlan.WordsSize, up); ObjectLabels++; return; }
            OzUeDraw.Label(at, hint, OzUi.A(c, 0.82f), OzUePlan.WordsSize, up);
            OzUeDraw.Label(at, top, c, OzUePlan.WordsSize, up + OzUePlan.WordsLine);
            ObjectLabels += 2;
        }
    }
}
