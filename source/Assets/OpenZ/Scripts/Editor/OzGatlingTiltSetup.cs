#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// new meshes in Models/Generated/gatling_tilt_parts.asset (the old gatling_turret_split.asset stays as it is) and in the prefab TurretYaw > BarrelPitch > BarrelSpin > Muzzle, plus
    /// TurretYaw > GatlingSkirt and TurretYaw > GatlingBreech. The rule and its counts: OzGatlingTiltRule. Nothing moves at rest (step 2 counts it). The yaw part keeps its 10438 vertices
    /// (GunChairDebris reads them): only triangles leave it. Every part carries the whole model's bounds in its own space, as OpenZTurretSplit does, so TeamPaint's band and panel stay where they were.
    /// Scene gatlings are prefab instances: they update through the prefab. Step 1b puts the prefab back and leaves the parts asset; step 1 fills that same asset again, also after a run that stopped half way.
    /// </summary>
    public static class OzGatlingTiltSetup
    {
        const string Tag = "[OpenZ GatlingTilt]", PrefabPath = "Assets/OpenZ/Prefabs/Z_gatling.prefab", PartsPath = "Assets/OpenZ/Models/Generated/gatling_tilt_parts.asset", OldAsset = "Assets/OpenZ/Models/Generated/gatling_turret_split.asset", OldMesh = "gatling_yaw", PitchNode = "BarrelPitch";
        static readonly Vector2 CoverUv = new Vector2(0.9182f, 0.0706f);                                  // the dark steel of the hub bolt, in the gatling's own paint
        static readonly Vector3 MuzzleRoot = new Vector3(2.02f, 2.003f, 0f);

        static Vector3 Hub => new Vector3(OzGatlingTiltRule.HubX, OzGatlingTiltRule.HubY, 0f);

        static byte[] Labels(Mesh src, Vector3 yawPos)
        {
            var v = src.vertices; int n = v.Length; float[] x = new float[n], y = new float[n], z = new float[n];
            for (int i = 0; i < n; i++) { x[i] = v[i].x + yawPos.x; y[i] = v[i].y + yawPos.y; z[i] = v[i].z + yawPos.z; }
            return OzGatlingTiltRule.Split(x, y, z, src.triangles);
        }

        static List<int> Pick(int[] tri, byte[] lab, int part)
        {
            var o = new List<int>();
            for (int t = 0; t < lab.Length; t++) if (lab[t] == part) { o.Add(tri[t * 3]); o.Add(tri[t * 3 + 1]); o.Add(tri[t * 3 + 2]); }
            return o;
        }

        static Mesh Part(Mesh src, List<int> tris, Vector3 origin, string name)
        {
            var v = src.vertices; var n = src.normals; var tg = src.tangents; var uv = src.uv;
            var map = new Dictionary<int, int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pt = new List<Vector4>(); var pu = new List<Vector2>(); var idx = new List<int>();
            foreach (int o in tris)
            {
                if (!map.TryGetValue(o, out int j)) { j = pv.Count; map[o] = j; pv.Add(v[o] - origin); pn.Add(n[o]); pt.Add(tg[o]); pu.Add(uv[o]); }
                idx.Add(j);
            }
            var m = new Mesh { name = name };
            m.SetVertices(pv); m.SetNormals(pn); m.SetTangents(pt); m.SetUVs(0, pu); m.SetTriangles(idx, 0, false);
            var b = src.bounds; b.center -= origin; m.bounds = b;
            return m;
        }

        /// <summary>The breech (OzGatlingTiltRule.Breech): the sheet under the head's roof and the plate in the drum's mouth. Under the head at rest; the head tilted down shows it in the wedge over the drum's flange.</summary>
        static Mesh Cover(Mesh src, Vector3 hubYaw)
        {
            var f = OzGatlingTiltRule.Breech(); int nv = f.Length / 6; var v = new Vector3[nv]; var n = new Vector3[nv]; var idx = new int[nv];
            for (int i = 0; i < nv; i++) { int j = i * 6; v[i] = new Vector3(f[j], f[j + 1], f[j + 2]); n[i] = new Vector3(f[j + 3], f[j + 4], f[j + 5]); idx[i] = i; }
            var m = new Mesh { name = "gatling_breech" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, Enumerable.Repeat(CoverUv, nv).ToArray()); m.SetTriangles(idx, 0, false);
            m.RecalculateTangents();
            var b = src.bounds; b.center -= hubYaw; m.bounds = b;
            return m;
        }

        /// <summary>Into the parts asset: a new sub-mesh, or over the one a run before left there, which keeps its place and name. Nothing is deleted.</summary>
        static Mesh Store(Mesh fresh, Mesh old)
        {
            if (!old) { AssetDatabase.AddObjectToAsset(fresh, PartsPath); return fresh; }
            string keep = old.name; EditorUtility.CopySerialized(fresh, old); old.name = keep; EditorUtility.SetDirty(old); Object.DestroyImmediate(fresh);
            return old;
        }

        [MenuItem("OpenZ/V1.3/Gatling tilt/1 Apply to the prefab (edit mode)")]
        public static void Apply()
        {
            if (Application.isPlaying) { Debug.LogError(Tag + " out of Play first"); return; }
            var asset = AssetDatabase.LoadMainAssetAtPath(PartsPath);                                     // left by 1b or by a run that stopped: filled again below
            if (asset && !(asset is Mesh)) { Debug.LogError(Tag + " " + PartsPath + " is not a mesh asset: nothing done"); return; }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var yaw = root.transform.Find("TurretYaw"); var spin = yaw ? yaw.Find("BarrelSpin") : null; var muzzle = spin ? spin.Find("Muzzle") : null;
                if (!yaw || !spin || !muzzle) { Debug.LogError(Tag + " Z_gatling is not TurretYaw > BarrelSpin > Muzzle: nothing done" + (yaw && yaw.Find(PitchNode) ? " (it has its " + PitchNode + " already)" : "")); return; }
                var mf = yaw.GetComponent<MeshFilter>(); var mr = yaw.GetComponent<MeshRenderer>(); var src = mf ? mf.sharedMesh : null;
                if (!src || src.name != OldMesh || !src.isReadable || src.subMeshCount != 1 || src.vertexCount != OzGatlingTiltRule.Verts || src.triangles.Length != OzGatlingTiltRule.Tris * 3)
                { Debug.LogError(Tag + " the yaw mesh is not the measured one (" + (src ? src.name + ", " + src.vertexCount + " vertices, readable " + src.isReadable : "none") + "): nothing done"); return; }
                var tri = src.triangles; var yawPos = yaw.localPosition; var lab = Labels(src, yawPos);
                List<int> stay = Pick(tri, lab, OzGatlingTiltRule.Stay), head = Pick(tri, lab, OzGatlingTiltRule.Head), skirt = Pick(tri, lab, OzGatlingTiltRule.Skirt);
                if (stay.Count != OzGatlingTiltRule.StayTris * 3 || head.Count != OzGatlingTiltRule.HeadTris * 3 || skirt.Count != OzGatlingTiltRule.SkirtTris * 3)
                { Debug.LogError(Tag + " the split gave stay " + stay.Count / 3 + ", head " + head.Count / 3 + ", skirt " + skirt.Count / 3 + " triangles, measured " + OzGatlingTiltRule.StayTris + ", " + OzGatlingTiltRule.HeadTris + ", " + OzGatlingTiltRule.SkirtTris + ": nothing done"); return; }
                var hubYaw = Hub - yawPos;
                var mStay = Object.Instantiate(src); mStay.name = "gatling_yaw_stay"; mStay.SetTriangles(stay, 0, false); mStay.bounds = src.bounds;
                var mHead = Part(src, head, hubYaw, "gatling_pitch"); var mSkirt = Part(src, skirt, hubYaw, "gatling_skirt"); var mCover = Cover(src, hubYaw);
                var kept = AssetDatabase.LoadAllAssetsAtPath(PartsPath).OfType<Mesh>().ToList();
                Mesh Old(string name) => kept.FirstOrDefault(k => k.name == name && k != asset);
                if (asset) mStay = Store(mStay, (Mesh)asset); else AssetDatabase.CreateAsset(mStay, PartsPath);   // CreateAsset names the main mesh after the file
                mHead = Store(mHead, Old("gatling_pitch")); mSkirt = Store(mSkirt, Old("gatling_skirt")); mCover = Store(mCover, Old("gatling_breech"));
                Transform Node(string name, Mesh m)
                {
                    var go = new GameObject(name) { layer = yaw.gameObject.layer };
                    go.transform.SetParent(yaw, false); go.transform.localPosition = hubYaw; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = mr.sharedMaterials; r.shadowCastingMode = mr.shadowCastingMode; r.receiveShadows = mr.receiveShadows;
                    return go.transform;
                }
                var pitch = Node(PitchNode, mHead);
                Node(UnitAim.GatlingSkirtNode, mSkirt); Node(UnitAim.GatlingBreechNode, mCover);
                spin.SetParent(pitch, true);
                mf.sharedMesh = mStay;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(Tag + " applied: stay " + stay.Count / 3 + ", head " + head.Count / 3 + ", skirt " + skirt.Count / 3 + " triangles, breech " + OzGatlingTiltRule.BreechTris + (asset ? "; the parts asset filled again" : "") + "; now run step 2");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("OpenZ/V1.3/Gatling tilt/1b Undo: the prefab as it was (edit mode)")]
        public static void Undo()
        {
            if (Application.isPlaying) { Debug.LogError(Tag + " out of Play first"); return; }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var yaw = root.transform.Find("TurretYaw"); var pitch = yaw ? yaw.Find(PitchNode) : null; var spin = pitch ? pitch.Find("BarrelSpin") : null;
                var old = AssetDatabase.LoadAllAssetsAtPath(OldAsset).OfType<Mesh>().FirstOrDefault(m => m.name == OldMesh);
                if (!yaw || !pitch || !spin || !old) { Debug.LogError(Tag + " nothing to undo"); return; }
                spin.SetParent(yaw, true);
                Object.DestroyImmediate(pitch.gameObject);
                foreach (var n in new[] { UnitAim.GatlingSkirtNode, UnitAim.GatlingBreechNode }) { var t = yaw.Find(n); if (t) Object.DestroyImmediate(t.gameObject); }
                yaw.GetComponent<MeshFilter>().sharedMesh = old;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(Tag + " the prefab is as it was; " + PartsPath + " stays, unused, step 1 fills it again");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("OpenZ/V1.3/Gatling tilt/2 Count (no Play)")]
        public static void Count()
        {
            var ok = new List<string>(); int pass = 0, fail = 0;
            void Row(bool c, string what) { if (c) pass++; else fail++; ok.Add((c ? "PASS  " : "FAIL  ") + what); }
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); var r = pf ? pf.transform : null;
            var yaw = r ? r.Find("TurretYaw") : null; var pitch = yaw ? yaw.Find(PitchNode) : null; var spin = pitch ? pitch.Find("BarrelSpin") : null; var muzzle = spin ? spin.Find("Muzzle") : null;
            var skirt = yaw ? yaw.Find(UnitAim.GatlingSkirtNode) : null; var cover = yaw ? yaw.Find(UnitAim.GatlingBreechNode) : null;
            var src = AssetDatabase.LoadAllAssetsAtPath(OldAsset).OfType<Mesh>().FirstOrDefault(m => m.name == OldMesh);
            Row(pitch, "E1 TurretYaw > BarrelPitch");
            Row(spin && muzzle, "E2 BarrelPitch > BarrelSpin > Muzzle");
            if (!pitch || !spin || !muzzle || !skirt || !cover || !src) { Row(false, "E3 the rest cannot be counted: run step 1"); Debug.LogError(Tag + " edit: FAIL\n" + string.Join("\n", ok)); return; }
            Vector3 P(Transform t) => r.InverseTransformPoint(t.position);
            Row((P(pitch) - Hub).magnitude < 0.001f, "E3 the hinge on the hub bolt: root " + P(pitch).ToString("F3"));
            Row((P(skirt) - Hub).magnitude < 0.001f && (P(cover) - Hub).magnitude < 0.001f, "E4 skirt and breech on the same hub");
            Row((P(muzzle) - MuzzleRoot).magnitude < 0.001f, "E5 the Muzzle stands where it stood: root " + P(muzzle).ToString("F3"));
            Row(Vector3.Angle(r.InverseTransformDirection(muzzle.TransformDirection(Vector3.back)), Vector3.right) < 0.1f, "E6 it fires along the barrels (+x)");
            Mesh M(Transform t) => t.GetComponent<MeshFilter>().sharedMesh;
            Mesh ms = M(yaw), mh = M(pitch), mk = M(skirt), mc = M(cover);
            Row(AssetDatabase.GetAssetPath(ms) == PartsPath && ms.triangles.Length == OzGatlingTiltRule.StayTris * 3 && mh.triangles.Length == OzGatlingTiltRule.HeadTris * 3 && mk.triangles.Length == OzGatlingTiltRule.SkirtTris * 3 && mc.triangles.Length == OzGatlingTiltRule.BreechTris * 3,
                "E7 triangles: stay " + ms.triangles.Length / 3 + ", head " + mh.triangles.Length / 3 + ", skirt " + mk.triangles.Length / 3 + ", breech " + mc.triangles.Length / 3);
            int kit = 0; var st = ms.triangles; for (int i = 0; i < st.Length; i += 3) if (st[i] >= OzGatlingTiltRule.KitFrom && st[i + 1] >= OzGatlingTiltRule.KitFrom && st[i + 2] >= OzGatlingTiltRule.KitFrom) kit++;
            Row(ms.vertexCount == OzGatlingTiltRule.Verts && ms.isReadable && kit == OzGatlingTiltRule.KitTris, "E8 the yaw part keeps " + ms.vertexCount + " vertices and the " + kit + " seat kit triangles (GunChairDebris, the gunner's seat)");
            var hubYaw = Hub - yaw.localPosition; var sv = src.vertices; var lab = Labels(src, yaw.localPosition); var srcT = src.triangles; float worst = 0f;
            void Same(int part, Vector3[] v, int[] idx) { var pick = Pick(srcT, lab, part); for (int i = 0; i < pick.Count && i < idx.Length; i++) worst = Mathf.Max(worst, (sv[pick[i]] - (v[idx[i]] + hubYaw)).magnitude); }
            Same(OzGatlingTiltRule.Head, mh.vertices, mh.triangles); Same(OzGatlingTiltRule.Skirt, mk.vertices, mk.triangles);
            Row(worst < 0.0001f, "E9 nothing moved at rest: worst vertex of head and skirt " + worst.ToString("F6") + " m from the old mesh");
            Row((ms.bounds.size - src.bounds.size).magnitude < 0.0001f && (mh.bounds.size - src.bounds.size).magnitude < 0.0001f && (mk.bounds.size - src.bounds.size).magnitude < 0.0001f && (mc.bounds.size - src.bounds.size).magnitude < 0.0001f,
                "E10 every part keeps the whole model's bounds (TeamPaint's band and panel)");
            Row(Mathf.Abs(mh.bounds.min.y + hubYaw.y - src.bounds.min.y) < 0.0001f && Mathf.Abs(mc.bounds.min.y + hubYaw.y - src.bounds.min.y) < 0.0001f, "E11 and in its own space: the band falls on the same height");
            var mats = yaw.GetComponent<MeshRenderer>().sharedMaterials;
            Row(new[] { pitch, skirt, cover }.All(t => t.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(mats)), "E12 the same material on every part");
            var s = UnitAim.SlewFor("gatling", Sim.ZKind.Cannon);
            Row(s.pitchMin == UnitAim.GatlingPitchMin && s.pitchMax == UnitAim.GatlingPitchMax, "E13 the stops " + s.pitchMin + " .. " + s.pitchMax + " deg");
            Row(!r.Find("Model") || !r.Find("Model").gameObject.activeSelf, "E14 the old single mesh stays off");
            string rep = Tag + " edit: " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + "/" + (pass + fail) + "\n" + string.Join("\n", ok);
            if (fail == 0) Debug.Log(rep); else Debug.LogError(rep);
        }

        static void Folder() { if (string.IsNullOrEmpty(OzGatlingTiltCheck.Folder)) OzGatlingTiltCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/open_fixes_31oct/gatling_tilt/proof")); }

        [MenuItem("OpenZ/V1.3/Gatling tilt/3 Field Check (Play, the unit editor open)")]
        static void Field() { if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; } Folder(); OzGatlingTiltCheck.Begin(false); }

        [MenuItem("OpenZ/V1.3/Gatling tilt/4 Tower Check (Play, a level playing)")]
        static void Tower() { if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play on level 2, 16 or 17 first"); return; } Folder(); OzGatlingTiltCheck.Begin(true); }

        [MenuItem("OpenZ/V1.3/Gatling tilt/5 Last report")]
        static void Last() => Debug.Log(OzGatlingTiltCheck.Running ? Tag + " running" : OzGatlingTiltCheck.Report);
    }
}
#endif
