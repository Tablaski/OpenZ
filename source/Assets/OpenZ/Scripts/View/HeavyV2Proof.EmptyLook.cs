#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Measured in the Play proof on two day frames of the two empty (neutral) heavies, v1 and v2 side by side: the RTS camera
    /// at the game's default zoom (70 m) and the close one:
    /// the screen pixels under each tank's upward faces (its own mesh vertices projected into the frame), their mean value (sRGB
    /// luminance 0-255) and the share near white (over 200). Written as one picture per frame (&lt;level&gt;_hv2_F_empty_side_by_side_&lt;rts|close&gt;.png:
    /// v1 left, v2 right, the frame's own pixels, x2) and one line each in the log's EMPTY LOOK section. Pass: v2's mean no lighter than v1's + 10 %,
    /// and under 5 % of v2 near white (v1 keeps its pale panels and skull). The cause and the fix: the kit's tools/empty_look.py and
    /// SPEC (v2's camo armour sits in the shader's pale-panel class: 0.2 x its shading, about sRGB 123 before the light; now 0.045).
    /// </summary>
    public partial class HeavyV2Proof
    {
        readonly List<string> emptyLook = new List<string>();

        /// <summary>Up to 600 upward-facing vertices of a heavy view's hull, turret and barrels, in world space.</summary>
        static List<Vector3> TopPoints(UnitView v)
        {
            var pts = new List<Vector3>();
            if (!v) return pts;
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; var r = mf.GetComponent<Renderer>();
                if (!m || !r || !r.enabled || !m.isReadable) continue;
                string n = mf.name;
                if (!(n.StartsWith("HeavyTankHull") || n.StartsWith("HeavyTankTurret") || n.StartsWith("HeavyTankBarrel"))) continue;
                var vs = m.vertices; var ns = m.normals; if (ns == null || ns.Length != vs.Length) continue;
                int step = Mathf.Max(1, vs.Length / 300);
                for (int i = 0; i < vs.Length; i += step)
                    if (mf.transform.TransformDirection(ns[i]).y > 0.35f) pts.Add(mf.transform.TransformPoint(vs[i]));
            }
            if (pts.Count < 30)
            {   // meshes that cannot be read: a 12 x 12 grid over the hull's top (its world bounds, 12 % in from each side)
                foreach (var r in v.GetComponentsInChildren<Renderer>())
                {
                    if (!r.name.StartsWith("HeavyTankHull") || !r.enabled) continue;
                    var b = r.bounds;
                    for (int i = 0; i < 12; i++)
                        for (int j = 0; j < 12; j++)
                            pts.Add(new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.12f + 0.76f * i / 11f), b.max.y, Mathf.Lerp(b.min.z, b.max.z, 0.12f + 0.76f * j / 11f)));
                    break;
                }
            }
            return pts;
        }

        static float Lum(Color c)
        {
            var l = c.linear; float y = 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b;
            return 255f * (y <= 0.0031308f ? y * 12.92f : 1.055f * Mathf.Pow(y, 1f / 2.4f) - 0.055f);
        }

        IEnumerator EmptyLook(ZObject n1, ZObject n2, string frame)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var res = new List<(string tag, float mean, float white, int n, Rect box)>();
                foreach (var (o, tag) in new[] { (n1, "v1"), (n2, "v2") })
                {
                    var v = View(o); float sum = 0f; int n = 0, white = 0;
                    float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
                    foreach (var p in TopPoints(v))
                    {
                        var s = ucam.WorldToScreenPoint(p);
                        if (s.z <= 0f || s.x < 0 || s.y < 0 || s.x >= tex.width || s.y >= tex.height) continue;
                        float L = Lum(tex.GetPixel((int)s.x, (int)s.y)); sum += L; n++; if (L > 200f) white++; OzV2EmptyLook.Tally(tag, L);   // k98: the two-tone rule
                        x0 = Mathf.Min(x0, s.x); y0 = Mathf.Min(y0, s.y); x1 = Mathf.Max(x1, s.x); y1 = Mathf.Max(y1, s.y);
                    }
                    res.Add((tag, n > 0 ? sum / n : -1f, n > 0 ? 100f * white / n : -1f, n, n > 0 ? Rect.MinMaxRect(x0 - 12, y0 - 12, x1 + 12, y1 + 12) : new Rect(0, 0, 8, 8)));
                }
                // the picture: both boxes from the frame itself, side by side, x2
                int w0 = 0, h0 = 0; foreach (var r in res) { w0 += Mathf.RoundToInt(r.box.width) * 2 + 8; h0 = Mathf.Max(h0, Mathf.RoundToInt(r.box.height) * 2); }
                var pic = new Texture2D(Mathf.Max(8, w0), Mathf.Max(8, h0), TextureFormat.RGB24, false);
                var fill = new Color32[pic.width * pic.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(0, 0, 0, 255); pic.SetPixels32(fill);
                int ox = 0;
                foreach (var r in res)
                {
                    int bw = Mathf.RoundToInt(r.box.width), bh = Mathf.RoundToInt(r.box.height);
                    for (int y = 0; y < bh * 2; y++)
                        for (int x = 0; x < bw * 2; x++)
                        {
                            int sx = Mathf.Clamp((int)r.box.x + x / 2, 0, tex.width - 1), sy = Mathf.Clamp((int)r.box.y + y / 2, 0, tex.height - 1);
                            if (ox + x < pic.width && y < pic.height) pic.SetPixel(ox + x, y, tex.GetPixel(sx, sy));
                        }
                    ox += bw * 2 + 8;
                }
                pic.Apply(false);
                string f = Path.Combine(Dir, $"{g.level}_hv2_F_empty_side_by_side_{frame}.png");
                File.WriteAllBytes(f, pic.EncodeToPNG()); Destroy(pic);
                var a = res[0]; var b = res[1];
                bool ok = OzV2EmptyLook.VerdictFamily(frame == "rts") && a.n > 20 && b.n > 20;   // item 170b: the heavy v2 against the empty light / MLRS / mech v1 family (View/OzV2EmptyLook.cs)
                string line = $"empty_look_{frame}: {(ok ? "PASS" : "FAIL")} - the empty heavies at the {(frame == "rts" ? "RTS camera's day frame (the game's default zoom)" : "close day frame")} (pixels under their upward faces): v1 mean value {a.mean:0} (sRGB 0-255), near white {a.white:0.0} % ({a.n} px); " +
                              $"v2 mean value {b.mean:0}, near white {b.white:0.0} % ({b.n} px) (pass: {OzV2EmptyLook.LastRule}); picture {Path.GetFileName(f)} (v1 left, v2 right)";
                emptyLook.Add(line); Mark(line);
            }
            finally { Destroy(tex); }
        }
    }
}
#endif
