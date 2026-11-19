using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// parts as View/OzUeObstacleView.cs draws them: dark body, faint far faces, lit near faces with the 1 m grid, neon edges, corner dots and the
    /// scan line) on the box's own 4 m cube, three boxes stacked brick style (two on the floor, one on top half over each: the picture says
    /// "stack them"), seen from above front as the obstacle's tile. Rendered twice (on black and on white) so the kit's tools/make_tile.py can
    /// take the light and its cover apart and lay it on the obstacle tile's own blue backdrop with its glow (the family of the EXTRAS pictures).
    /// Batch: -executeMethod OpenZ.EditorTools.OzV15UeBoxIcon.RenderBatch [-oz_box_icon_out &lt;folder&gt;] (default Logs/ue_box_icon); writes
    /// box_on_black.png and box_on_white.png (1024 x 1024). Menu: OpenZ/V1.5/Unit Editor/Boxes: Render the BOX tile picture (no Play).
    /// Nothing in the project changes (a preview scene; the objects and materials are destroyed after).
    /// </summary>
    public static class OzV15UeBoxIcon
    {
        const int Size = 1024;
        /// <summary>The hologram's colour in the picture: the obstacle tile's cyan edges.</summary>
        static readonly Color Cyan = new Color(0.56f, 0.90f, 1f, 1f);

        public static void RenderBatch()
        {
            int code = 1;
            try { code = Render(OutDir()) ? 0 : 1; }
            catch (Exception e) { Debug.LogError("[OpenZ UeBoxIcon] threw " + e); }
            EditorApplication.Exit(code);
        }

        [MenuItem("OpenZ/V1.5/Unit Editor/Boxes: Render the BOX tile picture (no Play)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ UeBoxIcon] leave Play mode first"); return; }
            Render(OutDir());
        }

        static string OutDir()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++) if (a[i] == "-oz_box_icon_out") return a[i + 1];
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ue_box_icon"));
        }

        public static bool Render(string dir)
        {
            Directory.CreateDirectory(dir);
            var src = Resources.Load<Material>("OpenZ/UnitEditor/M_UeHolo");
            var sh = src && src.shader ? src.shader : Shader.Find("OpenZ/UeHolo");
            if (!sh || !sh.isSupported) { Debug.LogError("[OpenZ UeBoxIcon] the hologram shader OpenZ/UeHolo is missing or not supported"); return false; }
            var scene = EditorSceneManager.NewPreviewScene();
            var made = new List<Object>();
            RenderTexture rt = null; Texture2D tex = null;
            try
            {
                var mats = new Material[4];
                for (int i = 0; i < 4; i++)
                {
                    var m = src && src.shader == sh ? new Material(src) : new Material(sh);
                    m.SetFloat("_Part", i);
                    m.SetFloat("_Cull", i == 0 ? (float)CullMode.Off : i == 2 ? (float)CullMode.Front : (float)CullMode.Back);
                    m.renderQueue = (int)RenderQueue.Transparent + 38 + i;
                    m.SetColor("_HoloColor", Cyan);
                    m.SetFloat("_HoloTime", 1.35f);                                       // the scan line a third of the way up the near faces
                    mats[i] = m; made.Add(m);
                }
                const float H = 4f, W = 4f;
                var mesh = BoxMesh(W, H, W); made.Add(mesh);
                var spots = new[] { new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(0f, H, 0f) };   // brick style: the top box half over each
                var mpb = new MaterialPropertyBlock();
                for (int b = 0; b < spots.Length; b++)
                {
                    for (int i = 1; i < 4; i++)
                    {
                        var go = new GameObject("Box" + b + "_" + i); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); made.Add(go);
                        go.transform.position = spots[b];
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mats[i];
                        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        mpb.Clear();
                        mpb.SetVector("_BoxSize", new Vector4(W, H, W, 0f)); mpb.SetVector("_BoxCentre", new Vector4(spots[b].x, spots[b].y, spots[b].z, 0f));
                        mpb.SetFloat("_Hit", 0f); mpb.SetFloat("_Alpha", 1f); mpb.SetFloat("_Rise", 0f); mpb.SetFloat("_Seed", b * 0.29f);
                        r.SetPropertyBlock(mpb);
                    }
                }
                var camGo = new GameObject("BoxIconCam"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene); made.Add(camGo);
                var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.fieldOfView = 26f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.allowHDR = false; cam.allowMSAA = true;
                var aim = new Vector3(0f, H * 0.95f, 0f);
                cam.transform.position = aim + Quaternion.Euler(24f, -32f, 0f) * new Vector3(0f, 0f, -31f);
                cam.transform.LookAt(aim, Vector3.up);
                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                foreach (var (name, bg) in new[] { ("box_on_black.png", Color.black), ("box_on_white.png", Color.white) })
                {
                    cam.backgroundColor = bg; cam.targetTexture = rt; cam.Render(); cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply();
                    RenderTexture.active = prev; cam.targetTexture = null;
                    File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
                }
                Debug.Log("[OpenZ UeBoxIcon] PASS rendered 3 boxes (OpenZ/UeHolo) on black and on white, " + Size + " x " + Size + ": " + dir.Replace('\\', '/'));
                return true;
            }
            finally
            {
                foreach (var o in made) if (o) Object.DestroyImmediate(o);
                if (rt) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex) Object.DestroyImmediate(tex);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>View/OzUeObstacleView.cs OzUeObstacleViews.BoxMesh (internal to the game's assembly), the same five faces: each with its own
        /// coordinates in metres (uv0) and (width, height, face id) in uv1.</summary>
        static Mesh BoxMesh(float w, float h, float d)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv0 = new List<Vector4>(); var uv1 = new List<Vector4>(); var t = new List<int>();
            float x = w * 0.5f, z = d * 0.5f;
            Face(v, n, uv0, uv1, t, new Vector3(x, h * 0.5f, 0f), Vector3.forward * z, Vector3.up * (h * 0.5f), Vector3.right, d, h, 0);
            Face(v, n, uv0, uv1, t, new Vector3(-x, h * 0.5f, 0f), Vector3.back * z, Vector3.up * (h * 0.5f), Vector3.left, d, h, 1);
            Face(v, n, uv0, uv1, t, new Vector3(0f, h * 0.5f, z), Vector3.left * x, Vector3.up * (h * 0.5f), Vector3.forward, w, h, 2);
            Face(v, n, uv0, uv1, t, new Vector3(0f, h * 0.5f, -z), Vector3.right * x, Vector3.up * (h * 0.5f), Vector3.back, w, h, 3);
            Face(v, n, uv0, uv1, t, new Vector3(0f, h, 0f), Vector3.right * x, Vector3.forward * z, Vector3.up, w, d, 4);
            var m = new Mesh { name = "UeHoloBoxIcon" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv0); m.SetUVs(1, uv1); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static void Face(List<Vector3> v, List<Vector3> n, List<Vector4> uv0, List<Vector4> uv1, List<int> t, Vector3 c, Vector3 right, Vector3 up, Vector3 normal, float fw, float fh, int id)
        {
            int b = v.Count;
            var corners = new[] { c - right - up, c - right + up, c + right + up, c + right - up };
            var uvs = new[] { new Vector2(0f, 0f), new Vector2(0f, fh), new Vector2(fw, fh), new Vector2(fw, 0f) };
            for (int i = 0; i < 4; i++) { v.Add(corners[i]); n.Add(normal); uv0.Add(new Vector4(uvs[i].x, uvs[i].y, 0f, 0f)); uv1.Add(new Vector4(fw, fh, id, 0f)); }
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
    }
}
