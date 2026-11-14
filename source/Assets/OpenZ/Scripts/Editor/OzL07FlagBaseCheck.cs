// OpenZ (GPL-3.0) - version 1.3, level 7: the counted Play check of the flag stones. Every line starts [FLAGBASE]; the last line reads "[FLAGBASE] PASS n/n" when all passed.
//   OpenZ/L07 Lava/Checks/Flag bases (Play)   in a match on L07: each zone flag has its one stone, in size, with no collider. On any other level: none at all.
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL07FlagBaseCheck
    {
        [MenuItem("OpenZ/L07 Lava/Checks/Flag bases (Play)")]
        public static void Run()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null) { Debug.LogError("[FLAGBASE] FAIL enter Play in a level first"); return; }
            int n = 0, pass = 0;
            void Line(bool ok, string s) { n++; if (ok) pass++; Debug.Log((ok ? "[FLAGBASE] ok   " : "[FLAGBASE] FAIL ") + s); }
            var all = OzL07FlagBase.All; int flags = g.World.Flags.Count;
            if (!OzVolcanic.Here)
            {
                Line(all.Count == 0, $"{g.level}: {all.Count} stones (0)");
                Line(Object.FindObjectsByType<OzL07FlagBase>(FindObjectsSortMode.None).Length == 0, $"{g.level}: no component in the scene");
            }
            else
            {
                var bare = g.World.Flags.Where(f => !all.Any(b => b.flagId == f.Id)).Select(f => f.Id).ToList();     // by sim id, so a flag dropped in the Unit editor counts too
                Line(bare.Count == 0 && all.Count == flags, $"{all.Count} stones on {flags} flags" + (bare.Count > 0 ? $", no stone on flag id {string.Join(", ", bare)}" : ""));
                float wMax = 0f, wMin = 99f, tMax = 0f; foreach (var b in all) { b.Measure(out float w, out float t); wMax = Mathf.Max(wMax, w); wMin = Mathf.Min(wMin, w); tMax = Mathf.Max(tMax, t); }
                Line(wMin >= 0.9f && wMax <= 1.4f, $"width {wMin:0.00} to {wMax:0.00} m (0.9 to 1.4, the sunk foot counted)");
                Line(tMax > 0.08f && tMax < 0.18f, $"top {tMax:0.00} m over the ground (0.08 to 0.18)");
                Line(all.All(b => b.GetComponentsInChildren<Collider>(true).Length == 0), "no collider on any stone or mark");
                Line(all.All(b => b.mark), "every stone has its mark");
                var m = all.Count > 0 ? all[0].body.sharedMaterial : null; var map = m ? m.GetTexture("_BaseMap") : null;   // TeamLit has no main-texture property: m.mainTexture would be null
                Line(m && m.GetFloat("_SeeThru") < 0.5f && map && map.name == "rockz2_atlas_Volcanic", $"material {(m ? m.name : "none")}: rock atlas, no see-through cut");
            }
            Debug.Log(pass == n ? $"[FLAGBASE] PASS {pass}/{n}" : $"[FLAGBASE] FAIL {pass}/{n}");
        }
    }
}
