using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 26 (Unit editor light floors), apply step 1: the BEFORE pictures. Uses only what 1.2 already has (OzUeBackdrop.StyleCount,
    /// OzUeFloor.ThumbMaterial), so it compiles and runs on the 1.2 code alone, before the rest of the package is applied. Edit mode.
    /// Menu: OpenZ/Checks/V1.3/Light floors 1 - capture the 1.2 floor pictures (before item 26). It draws every style's popover picture
    /// with the thumbnail shader at 133 x 75 (the popover at 1920 x 1080) and at 399 x 225 (3x) and saves them as PNG into
    /// again after the change, the same way, and compares them pixel for pixel.
    /// </summary>
    public static class OzV13LightFloorsBefore
    {
        public const string Tag = "[OpenZ light floors]";
        public static readonly Vector2Int[] Sizes = { new Vector2Int(133, 75), new Vector2Int(399, 225) };

        public static string Dir(string sub) => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item26_light_floors", sub));

        [MenuItem("OpenZ/Checks/V1.3/Light floors 1 - capture the 1.2 floor pictures (before item 26)")]
        public static void Capture()
        {
            string dir = Dir("before");
            Directory.CreateDirectory(dir);
            int n = 0;
            for (int i = 0; i < OzUeBackdrop.StyleCount; i++)
                foreach (var s in Sizes)
                {
                    var t = Render(i, s.x, s.y);
                    if (t == null) { Debug.LogError($"{Tag} style {i + 1}: the thumbnail shader is missing or not supported"); return; }
                    File.WriteAllBytes(Path.Combine(dir, Name(i, s)), t.EncodeToPNG());
                    Object.DestroyImmediate(t); n++;
                }
            Debug.Log($"{Tag} BEFORE: {n} pictures of {OzUeBackdrop.StyleCount} styles saved in {dir}");
        }

        public static string Name(int style, Vector2Int s) => $"thumb_{style + 1:00}_{s.x}x{s.y}.png";

        /// <summary>One style's popover picture (OzUeFloor.ThumbMaterial, as the popover makes it) drawn into a w x h sRGB target and read
        /// back (sRGB bytes = the display colours). Null when the shader is missing. lines = false: the floor alone (its base colour, the
        /// middle light and the vignette, the shader's E017 "floor lines off" path), the floor the check's S5 measures the marks against.</summary>
        public static Texture2D Render(int style, int w, int h, bool lines = true)
        {
            var m = OzUeFloor.ThumbMaterial(style);
            if (m == null) return null;
            if (!lines) m.SetVector("_UeOpts", new Vector4(0f, 1f, 0f, 0f));                                // the thumbnail shader's lines flag (x)
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            GL.PushMatrix(); GL.LoadOrtho();
            m.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);                                                                        // the RawImage's vertex colour (the shader's alpha)
            GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(0f, 1f); GL.Vertex3(0f, 1f, 0f);
            GL.TexCoord2(1f, 1f); GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(1f, 0f); GL.Vertex3(1f, 0f, 0f);
            GL.End();
            GL.PopMatrix();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false); tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(m);
            return tex;
        }
    }
}
