#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The mech v2's glowing eyes (View/OzMechV2Eyes.cs), the editor side:
    ///  - SameButEyes: the parity check's lamp line (Editor/OzMechV2bSetup.cs menu 2) accepts the v2's lamp entry when it is v1's but for the
    ///    two eye lamps (and the headlight's centre), which sit OzMechV2Eyes.SocketDrop lower, in the painted eyes;
    ///  - Parity: one counted line for menu 2 (the three eye pictures, the material without its own emission, the visor slot on the v2 prefab);
    ///  - menu 26 (Play): every v2 mech's eyes now (crewed / empty / wreck, lamp power, tint, one eye) -> the proof folder's eyes_status.txt.
    /// </summary>
    public static class OzMechV2EyesCheck
    {
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/mech/proof"));

        static float R4(float x) => Mathf.Round(x * 10000f) / 10000f;

        /// <summary>v1's lamp entry and the v2's (the parity check's text, type / prefab / label taken out) are the same but for the two eye
        /// lamps' height (pos.y = v1's - SocketDrop, root.y = v1's - SocketDrop x nodeScale) and the headlight centre's (root.y).</summary>
        public static bool SameButEyes(string v1Entry, string v2Entry)
        {
            if (string.IsNullOrEmpty(v1Entry) || string.IsNullOrEmpty(v2Entry)) return false;
            VLVehicle a, b;
            try { a = JsonUtility.FromJson<VLVehicle>(v1Entry); b = JsonUtility.FromJson<VLVehicle>(v2Entry); }
            catch (System.Exception) { return false; }
            if (a?.sockets == null || b?.sockets == null || a.sockets.Length != b.sockets.Length || a.headlight == null || b.headlight == null) return false;
            int moved = 0; float rootDrop = 0f;
            for (int i = 0; i < b.sockets.Length; i++)
            {
                var s = b.sockets[i];
                if (s == null || s.kind != "eye" || s.pos == null || s.root == null || s.pos.Length < 3 || s.root.Length < 3) continue;
                var t = a.sockets[i];
                if (t == null || t.id != s.id || t.pos == null || t.root == null || t.pos.Length < 3 || t.root.Length < 3) return false;
                float pd = OzMechV2Eyes.SocketDrop, rd = OzMechV2Eyes.SocketDrop * s.nodeScale;
                if (Mathf.Abs(t.pos[1] - s.pos[1] - pd) > 2e-4f || Mathf.Abs(t.root[1] - s.root[1] - rd) > 2e-4f) return false;
                s.pos[1] = t.pos[1] = R4(t.pos[1]); s.root[1] = t.root[1] = R4(t.root[1]);   // compared as v1's from here on
                rootDrop = rd; moved++;
            }
            if (moved != 2) return false;
            var ha = a.headlight.root; var hb = b.headlight.root;
            if (ha == null || hb == null || ha.Length < 3 || hb.Length < 3 || Mathf.Abs(ha[1] - hb[1] - rootDrop) > 3e-4f) return false;
            hb[1] = ha[1] = R4(ha[1]);
            a.type = b.type = null; a.prefab = b.prefab = null; a.label = b.label = null;
            return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        }

        /// <summary>The parity check's eye line: (ok, text).</summary>
        public static (bool ok, string what) Parity(GameObject v2Prefab, Material toy2)
        {
            int n = 0; string sizes = "";
            foreach (var p in new[] { OzMechV2Eyes.Both, OzMechV2Eyes.OnlyPx, OzMechV2Eyes.OnlyNx })
            {
                var t = Resources.Load<Texture2D>(OzMech.Dir + p);
                if (t && t.width == 1024 && t.height == 1024) n++;
                sizes += (sizes.Length > 0 ? ", " : "") + p + " " + (t ? t.width + "x" + t.height : "MISSING");
            }
            bool matOff = toy2 && toy2.HasProperty("_EmissionMapColor") && toy2.GetColor("_EmissionMapColor").maxColorComponent < 1e-4f && toy2.GetColor("_EmissionColor").maxColorComponent < 1e-4f;
            int slots = 0;
            if (v2Prefab)
                foreach (var r in v2Prefab.GetComponentsInChildren<Renderer>(true))
                    if (r.name == OzMechV2Eyes.Surface) foreach (var m in r.sharedMaterials) if (m && m == toy2) slots++;
            bool ok = n == 3 && matOff && slots == 1;
            return (ok, $"the eyes (View/OzMechV2Eyes.cs): pictures {sizes} (Resources/{OzMech.Dir}); {(toy2 ? toy2.name : "-")} has no emission of its own ({(matOff ? "tint black: wrecks and copies stay dark" : "NOT black")}); the visor slot {OzMechV2Eyes.Surface} / {(toy2 ? toy2.name : "-")} on the v2 prefab: {slots} (1 expected); the eye lamps of 'medium@v2' {OzMechV2Eyes.SocketDrop:0.000} lower than v1's (the lamp line)");
        }

        [MenuItem("OpenZ/Mech v2/26 Eyes status (Play: crewed / empty / wreck)", false, 80)]
        static void Status()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OzMechV2Eyes] enter Play first (a level with mech v2s)"); return; }
            string s = OzMechV2Eyes.I ? OzMechV2Eyes.I.Status() : "[OpenZ] OzMechV2Eyes: not running in this scene";
            Directory.CreateDirectory(ProofDir);
            File.AppendAllText(Path.Combine(ProofDir, "eyes_status.txt"), $"--- t {Time.time:F1} s\n{s}\n");
            Debug.Log(s);
        }
    }
}
#endif
