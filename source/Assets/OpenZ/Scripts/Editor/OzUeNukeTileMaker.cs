using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// from the levels' own bomb model, the way the GRENADES tile came from the crate's own render (k74): Resources/OpenZ/FX/legacy_nuke (red,
    /// its own paint) in a preview scene, lying across the picture nose up to the right (pitch NosePitch, yaw NoseYaw), the unit portraits'
    /// three lamps (warm key from the front left, cool rim from behind right, soft fill), rendered at 1024 with 8x anti-aliasing twice, once
    /// on black and once on white: the difference gives the exact cut-out (alpha = 1 - (white - black), colour = black / alpha, no keyed
    /// fringe). Cropped round the bomb (longest side Fill of the picture, centred), averaged down to 256 x 256 RGBA and written to
    /// Resources/OpenZ/UnitEditor/ue_nuke_tile.png with the GRENADES tile's import settings (same texture type, sRGB, transparency, mip maps,
    /// filter, platform sizes). Also writes the picture laid on the dock's dark well to Temp/ue_nuke_tile_on_well.png for a look. A second run overwrites it.
    /// </summary>
    public static class OzUeNukeTileMaker
    {
        const string Model = "OpenZ/FX/legacy_nuke";
        const string Out = "Assets/OpenZ/Resources/OpenZ/UnitEditor/ue_nuke_tile.png";
        const string Sibling = "Assets/OpenZ/Resources/OpenZ/UnitEditor/ue_grenades_tile.png";
        public const int Res = 1024, Size = 256;
        public static float NosePitch = -38f, NoseYaw = 62f, Fill = 0.86f, Fov = 18f;

        public static void Make()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ NukeTile] leave Play first"); return; }
            var lib = Resources.Load<GameObject>(Model);
            if (!lib) { Debug.LogError("[OpenZ NukeTile] Resources/" + Model + " missing"); return; }
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject bomb = null, camGo = null; var lamps = new GameObject[3];
            RenderTexture rt = null; Texture2D black = null, white = null, cut = null;
            try
            {
                bomb = Object.Instantiate(lib); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bomb, scene);
                bomb.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, NoseYaw, 0f) * Quaternion.Euler(NosePitch, 0f, 0f));   // nose = local +Z
                var b = new Bounds(bomb.transform.position, Vector3.zero); bool any = false;
                foreach (var r in bomb.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                if (!any) { Debug.LogError("[OpenZ NukeTile] the model has no renderer"); return; }
                camGo = new GameObject("NukeTileCam"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.fieldOfView = Fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                float dist = b.extents.magnitude / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * 1.15f;
                var view = new Vector3(0.18f, 0.32f, -1f).normalized;                                          // a little from above and the left
                cam.transform.position = b.center - view * dist;
                cam.transform.LookAt(b.center, Vector3.up);
                Lamp(scene, lamps, 0, "Key", new Color(1f, 0.92f, 0.82f), 1.5f, new Vector3(28f, 210f, 0f));
                Lamp(scene, lamps, 1, "Rim", new Color(0.55f, 0.72f, 1f), 1.4f, new Vector3(18f, 30f, 0f));
                Lamp(scene, lamps, 2, "Fill", new Color(0.75f, 0.80f, 0.95f), 0.35f, new Vector3(5f, 160f, 0f));
                rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                black = Shot(cam, rt, Color.black); white = Shot(cam, rt, Color.white);
                cut = CutOut(black, white, out RectInt box, out int px);
                if (cut == null) { Debug.LogError("[OpenZ NukeTile] nothing of the bomb in the picture: check NosePitch / NoseYaw / Fov"); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(Out));
                File.WriteAllBytes(Out, cut.EncodeToPNG());
                AssetDatabase.ImportAsset(Out, ImportAssetOptions.ForceUpdate);
                CopyImport(Sibling, Out);
                WritePreview(cut);
                Debug.Log($"[OpenZ NukeTile] DONE {Out}: {Size} x {Size} RGBA, the bomb {box.width} x {box.height} px of the {Res} render, {px} px opaque in the tile, the import of {Path.GetFileName(Sibling)}");
            }
            finally
            {
                if (bomb) Object.DestroyImmediate(bomb);
                if (camGo) Object.DestroyImmediate(camGo);
                foreach (var l in lamps) if (l) Object.DestroyImmediate(l);
                if (rt) { rt.Release(); Object.DestroyImmediate(rt); }
                if (black) Object.DestroyImmediate(black);
                if (white) Object.DestroyImmediate(white);
                if (cut) Object.DestroyImmediate(cut);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Lamp(UnityEngine.SceneManagement.Scene scene, GameObject[] into, int k, string n, Color c, float intensity, Vector3 euler)
        {
            var l = new GameObject(n); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(l, scene); into[k] = l;
            var li = l.AddComponent<Light>(); li.type = LightType.Directional; li.color = c; li.intensity = intensity; li.shadows = LightShadows.None;
            l.transform.rotation = Quaternion.Euler(euler);
        }

        static Texture2D Shot(Camera cam, RenderTexture rt, Color bg)
        {
            cam.backgroundColor = bg; cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(Res, Res, TextureFormat.RGBA32, false, false);
            t.ReadPixels(new Rect(0, 0, Res, Res), 0, 0); t.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            return t;
        }

        /// <summary>Difference matte, crop round the bomb (longest side Fill of the tile, centred), area average down to Size.</summary>
        static Texture2D CutOut(Texture2D black, Texture2D white, out RectInt box, out int opaque)
        {
            var kb = black.GetPixels32(); var kw = white.GetPixels32();
            var a = new float[Res * Res]; var col = new Color[Res * Res];
            int x0 = Res, y0 = Res, x1 = -1, y1 = -1;
            for (int i = 0; i < a.Length; i++)
            {
                float d = ((kw[i].r - kb[i].r) + (kw[i].g - kb[i].g) + (kw[i].b - kb[i].b)) / (3f * 255f);
                float al = Mathf.Clamp01(1f - d);
                a[i] = al;
                col[i] = al > 1e-3f ? new Color(Mathf.Clamp01(kb[i].r / 255f / al), Mathf.Clamp01(kb[i].g / 255f / al), Mathf.Clamp01(kb[i].b / 255f / al), al) : new Color(0f, 0f, 0f, 0f);
                if (al > 0.02f) { int x = i % Res, y = i / Res; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            }
            opaque = 0; box = default;
            if (x1 < 0) return null;
            box = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            float side = Mathf.Max(box.width, box.height) / Fill, cx = box.x + box.width * 0.5f, cy = box.y + box.height * 0.5f;
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp };
            var outPx = new Color[Size * Size];
            const int S = 4;                                                                                     // 4 x 4 samples a tile pixel, premultiplied
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float r = 0f, g = 0f, bl = 0f, al = 0f;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            float u = cx - side * 0.5f + (x + (sx + 0.5f) / S) / Size * side, v = cy - side * 0.5f + (y + (sy + 0.5f) / S) / Size * side;
                            int ix = Mathf.FloorToInt(u), iy = Mathf.FloorToInt(v);
                            if (ix < 0 || iy < 0 || ix >= Res || iy >= Res) continue;
                            var c = col[iy * Res + ix];
                            r += c.r * c.a; g += c.g * c.a; bl += c.b * c.a; al += c.a;
                        }
                    float n = S * S;
                    var o = al > 1e-4f ? new Color(r / al, g / al, bl / al, al / n) : new Color(0f, 0f, 0f, 0f);
                    outPx[y * Size + x] = o;
                    if (o.a > 0.5f) opaque++;
                }
            t.SetPixels(outPx); t.Apply(true, false);
            return t;
        }

        /// <summary>The GRENADES tile's import settings on the new picture (texture settings and every platform's size / compression).</summary>
        static void CopyImport(string from, string to)
        {
            var src = AssetImporter.GetAtPath(from) as TextureImporter;
            var dst = AssetImporter.GetAtPath(to) as TextureImporter;
            if (!src || !dst) { Debug.LogWarning("[OpenZ NukeTile] import settings not copied (" + from + " / " + to + ")"); return; }
            var ts = new TextureImporterSettings(); src.ReadTextureSettings(ts); dst.SetTextureSettings(ts);
            dst.SetPlatformTextureSettings(src.GetDefaultPlatformTextureSettings());
            foreach (var p in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var s = src.GetPlatformTextureSettings(p);
                if (s != null && s.overridden) dst.SetPlatformTextureSettings(s);
            }
            dst.SaveAndReimport();
        }

        static void WritePreview(Texture2D cut)
        {
            try
            {
                var well = (Color)new Color32(0x15, 0x18, 0x1A, 255);                                            // OzUeArt.ExtraWell
                var t = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                var px = cut.GetPixels(); var o = new Color[px.Length];
                for (int i = 0; i < px.Length; i++) o[i] = Color.Lerp(well, new Color(px[i].r, px[i].g, px[i].b, 1f), px[i].a);
                t.SetPixels(o); t.Apply();
                string p = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ue_nuke_tile_on_well.png");
                File.WriteAllBytes(p, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                Debug.Log("[OpenZ NukeTile] a look on the dock's well: " + p);
            }
            catch (System.Exception e) { Debug.LogWarning("[OpenZ NukeTile] no preview: " + e.Message); }
        }
    }
}
