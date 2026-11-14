using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>The light tank v2 hatch driver check (View/LightV2HatchCheck.cs): in Play, the first click starts the count, the next writes the
    /// </summary>
    public static class OzLightV2HatchMenu
    {
        [MenuItem("OpenZ/Light Tank v2/16 Driver clip count (Play: start, again: report)", false, 16)]
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[LightV2HatchCheck] enter Play first (Unit editor or a level with a light tank v2)"); return; }
            if (!LightV2HatchCheck.I) { LightV2HatchCheck.Begin(); return; }
            Debug.Log("[LightV2HatchCheck] " + LightV2HatchCheck.I.Report());
        }
    }
}
