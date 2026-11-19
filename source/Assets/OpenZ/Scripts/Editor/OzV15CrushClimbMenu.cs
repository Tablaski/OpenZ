#if !OPENZ_HARNESS
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit bizon_crush_climb: DV187's counted check from the menu (headless, the simulation only, nothing saved).</summary>
    public static class OzV15CrushClimbMenu
    {
        [MenuItem("OpenZ/V1.5/Bizon Crush Climb: Roll Off Count (headless, DV187)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] roll off count: no Resources/OpenZ/default_settings"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15CrushRollOffCount.Run(settings.text, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] roll off count (DV187): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
