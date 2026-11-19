#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// (Resources/OpenZ/OzRobotDrawnPoints.bytes, read by View/OzDrawnPoints.cs in the game) and the r11 Play check (View/OzRobotPiecesCheckR11.cs)
    /// from the menu. The bake runs in edit mode (it reads the robots' meshes, which players cannot read); the check needs Play (the Unit editor: flat,
    /// slope and platform edge; a level: flat ground). Report and frames: the kit's proof folder (check_&lt;date_time&gt;_&lt;where&gt;/).</summary>
    public static class OzV15RobotPiecesR11Menu
    {
        public static string ProofDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/robot_pieces_ground_r11b/proof"));   // 1.5 kit robot_pieces_ground_r11b: the fix round's proof folder
        const string Out = "Assets/OpenZ/Resources/OpenZ/OzRobotDrawnPoints.bytes";
        static readonly string[] PrefabDirs = { "Assets/OpenZ/Prefabs", "Assets/OpenZ/Resources/OpenZ/HeroSquad" };
        static readonly string[] ModelDirs = { "Assets/OpenZ/Models/BS01" };

        [MenuItem("OpenZ/1.5/Bake/Robot drawn points (A35 r11)")]
        public static void Bake()
        {
            if (Application.isPlaying) { Debug.LogWarning(OzRobotPiecesCheckR11.Tag + " bake in edit mode (leave Play first)"); return; }
            var rigid = new Dictionary<string, Vector3[]>(); var skins = new Dictionary<string, (int[] bone, Vector3[][] pts)>();
            int prefabs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabDirs))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!go || !go.GetComponentInChildren<SkinnedMeshRenderer>(true) || !go.GetComponentInChildren<Animator>(true)) continue;   // robots and heroes (a skinned body with an Animator)
                prefabs++;
                foreach (var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Skin(sk, skins);
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) Rigid(mf.sharedMesh, rigid);
            }
            int models = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", ModelDirs))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var n = Path.GetFileNameWithoutExtension(path);
                if (n.Contains("Anims") || n.Contains("Clips")) continue;
                models++;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m) Rigid(m, rigid);
            }
            int points = 0;
            using (var w = new BinaryWriter(File.Create(Out)))
            {
                w.Write(OzDrawnPoints.Magic.ToCharArray()); w.Write(OzDrawnPoints.Version); w.Write(rigid.Count + skins.Count);
                foreach (var kv in rigid) { w.Write(kv.Key); w.Write(1); w.Write(-1); w.Write(kv.Value.Length); foreach (var p in kv.Value) { w.Write(p.x); w.Write(p.y); w.Write(p.z); } points += kv.Value.Length; }
                foreach (var kv in skins)
                {
                    w.Write(kv.Key); w.Write(kv.Value.bone.Length);
                    for (int g = 0; g < kv.Value.bone.Length; g++) { w.Write(kv.Value.bone[g]); w.Write(kv.Value.pts[g].Length); foreach (var p in kv.Value.pts[g]) { w.Write(p.x); w.Write(p.y); w.Write(p.z); } points += kv.Value.pts[g].Length; }
                }
            }
            AssetDatabase.ImportAsset(Out);
            Debug.Log($"{OzRobotPiecesCheckR11.Tag} drawn points baked: {Out}: {rigid.Count} rigid meshes + {skins.Count} skins ({points} points) from {prefabs} robot prefabs and {models} BS-01 model files");
        }

        static void Rigid(Mesh m, Dictionary<string, Vector3[]> into)
        {
            if (!m || m.isReadable) return;                                              // a readable mesh is read in the game itself
            var key = OzDrawnPoints.Key(m); if (into.ContainsKey(key)) return;
            var v = new List<Vector3>(m.vertices); var used = new HashSet<int>();
            for (int s = 0; s < m.subMeshCount; s++) if (m.GetTopology(s) == MeshTopology.Triangles) foreach (int i in m.GetIndices(s)) used.Add(i);
            into[key] = OzDrawnPoints.Extremes(v, used.Count > 0 ? new List<int>(used) : null);
        }

        static void Skin(SkinnedMeshRenderer sk, Dictionary<string, (int[] bone, Vector3[][] pts)> into)
        {
            var m = sk.sharedMesh; if (!m) return;
            var key = OzDrawnPoints.Key(m); if (into.ContainsKey(key)) return;
            var v = m.vertices; var bw = m.boneWeights; var bp = m.bindposes;
            if (bw.Length != v.Length || bp.Length == 0) return;
            var used = new HashSet<int>();
            for (int s = 0; s < m.subMeshCount; s++) if (m.GetTopology(s) == MeshTopology.Triangles) foreach (int i in m.GetIndices(s)) used.Add(i);
            var per = new Dictionary<int, List<Vector3>>();
            foreach (int i in used)
            {
                var b = bw[i]; int bi = b.boneIndex0; float best = b.weight0;                            // the bone carrying most of its weight (as the pop death's parts are cut)
                if (b.weight1 > best) { best = b.weight1; bi = b.boneIndex1; }
                if (b.weight2 > best) { best = b.weight2; bi = b.boneIndex2; }
                if (b.weight3 > best) { bi = b.boneIndex3; }
                if (bi < 0 || bi >= bp.Length) continue;
                if (!per.TryGetValue(bi, out var l)) per[bi] = l = new List<Vector3>();
                l.Add(bp[bi].MultiplyPoint3x4(v[i]));                                             // in that bone's space
            }
            var bones = new List<int>(); var pts = new List<Vector3[]>();
            foreach (var kv in per) { bones.Add(kv.Key); pts.Add(OzDrawnPoints.Extremes(kv.Value)); }
            into[key] = (bones.ToArray(), pts.ToArray());
        }

        [MenuItem("OpenZ/1.5/Checks/Robot Pieces r11/Every robot, every death: flat, slope, platform edge (Unit editor; a level: flat)")]
        static void Go()
        {
            if (!Application.isPlaying) { Debug.LogWarning(OzRobotPiecesCheckR11.Tag + " enter Play first (the Unit editor, or a level)"); return; }
            var dir = Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (OzUnitEditor.Active ? "_unit_editor" : "_level"));
            OzRobotPiecesCheckR11.Begin(dir);
            Debug.Log(OzRobotPiecesCheckR11.Tag + " started (grunt, tough, sniper, pyro, psycho, laser, electrobot x9 deaths, heroes x10, EvilZod x2; flat, slope, platform edge; about 6 min): " + dir);
        }

        [MenuItem("OpenZ/1.5/Checks/Robot Pieces r11/Every robot, every death: flat, slope, platform edge (Unit editor; a level: flat)", true)]
        static bool Can() => Application.isPlaying && !OzRobotPiecesCheckR11.Running;

        [MenuItem("OpenZ/1.5/Checks/Robot Pieces r11/A-B: death-clip grounding OFF (the clip bodies must show the old hover)")]
        static void GoAb()
        {
            if (!Application.isPlaying) { Debug.LogWarning(OzRobotPiecesCheckR11.Tag + " enter Play first (the Unit editor, or a level)"); return; }
            var dir = Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (OzUnitEditor.Active ? "_unit_editor" : "_level") + "_AB_ground_off");
            OzRobotPiecesCheckR11.Begin(dir, true);
            Debug.Log(OzRobotPiecesCheckR11.Tag + " A/B started (OzBodyGround OFF; 9 grunts on flat ground, every death; about 20 s; switched back on at its end): " + dir);
        }

        [MenuItem("OpenZ/1.5/Checks/Robot Pieces r11/A-B: death-clip grounding OFF (the clip bodies must show the old hover)", true)]
        static bool CanAb() => Application.isPlaying && !OzRobotPiecesCheckR11.Running;
    }
}
#endif
