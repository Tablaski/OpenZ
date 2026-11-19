#if !OPENZ_HARNESS
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit ram_throw_fix: DV186's counted check from the menu (headless, the simulation only, nothing saved).</summary>
    public static class OzV15RamThrowMenu
    {
        [MenuItem("OpenZ/V1.5/Ram Throw Fix: Ram Debris Count (headless, DV186)")]
        static void Count()
        {
            var settings = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!settings) { Debug.LogError("[OpenZ V1.5] ram debris count: no Resources/OpenZ/default_settings"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamDebrisCount.Run(settings.text, true);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] ram debris count (DV186): {pass} PASS, {fail} FAIL\n{r}");
        }
    }
}
#endif
