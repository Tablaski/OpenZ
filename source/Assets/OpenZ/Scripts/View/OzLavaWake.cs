// OpenZ (GPL-3.0) - version 1.3, level 7 (LV_CONTRACT.md, kit LV3): the wake in the lava.
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A small heat map over the lake box (R8, 64 x 80, about 1 m a texel, bilinear, clamp, no mips): the surface shader adds it to its
    /// temperature, so the crust opens round the legs and a glowing track closes behind a robot over FadeSeconds. Written and uploaded
    /// TickHz times a second, only while something was stamped and until every texel is back at 0; fades by subtraction. Border sheets never
    /// read it (vertex a = 0). This class alone writes _OZLavaWake and _OZLavaWakeRect.
    /// </summary>
    public sealed class OzLavaWake
    {
        public const int W = 64, H = 80, TickHz = 10;
        public const float FadeSeconds = 6f;   // a full stamp; the legs' own stamp (0.7) closes in about 4.5 s
        /// <summary>Check: uploads so far.</summary>
        public static int Uploads;

        readonly byte[] px = new byte[W * H];
        readonly Texture2D tex;
        readonly float minX, minZ, cell;
        readonly byte fade = (byte)Mathf.Max(1, Mathf.RoundToInt(255f / (FadeSeconds * TickHz)));
        float clock; bool dirty, live;

        public OzLavaWake(ZMapData map)
        {
            int x0 = map.width, y0 = map.height, x1 = -1, y1 = -1;
            for (int ty = 0; ty < map.height; ty++)
                for (int tx = 0; tx < map.width; tx++)
                    if (map.LavaAt(tx, ty)) { if (tx < x0) x0 = tx; if (tx > x1) x1 = tx; if (ty < y0) y0 = ty; if (ty > y1) y1 = ty; }
            float bx0 = x0 * 2f - 1f, bx1 = (x1 + 1) * 2f + 1f, bz0 = -(y1 + 1) * 2f - 1f, bz1 = -y0 * 2f + 1f;   // a metre round the tiles
            cell = Mathf.Max(1f, Mathf.Max((bx1 - bx0) / W, (bz1 - bz0) / H));
            minX = bx0; minZ = bz0;
            tex = new Texture2D(W, H, TextureFormat.R8, false, true) { name = "OzLavaWake", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.LoadRawTextureData(px); tex.Apply(false, false);
            Shader.SetGlobalTexture(OzLavaIds.Wake, tex);
            Shader.SetGlobalVector(OzLavaIds.WakeRect, new Vector4(minX, minZ, 1f / (cell * W), 1f / (cell * H)));
        }

        /// <summary>Opens the crust round p: radius in metres, strength 0..1, the hotter value wins. The outer texels stay 0, the map clamps there.</summary>
        public void Stamp(Vector3 p, float radius, float strength)
        {
            float fx = (p.x - minX) / cell - 0.5f, fz = (p.z - minZ) / cell - 0.5f, r = radius / cell;
            int ix0 = Mathf.Max(1, Mathf.FloorToInt(fx - r)), ix1 = Mathf.Min(W - 2, Mathf.CeilToInt(fx + r)), iz0 = Mathf.Max(1, Mathf.FloorToInt(fz - r)), iz1 = Mathf.Min(H - 2, Mathf.CeilToInt(fz + r));
            for (int iz = iz0; iz <= iz1; iz++)
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    float dx = (ix - fx) / r, dz = (iz - fz) / r, q = 1f - (dx * dx + dz * dz);
                    if (q <= 0f) continue;
                    byte v = (byte)(255f * strength * q);
                    int i = iz * W + ix;
                    if (v > px[i]) { px[i] = v; dirty = true; }
                }
        }

        /// <summary>dt 0 = paused: the track holds.</summary>
        public void Tick(float dt)
        {
            if (!dirty && !live) return;
            clock += dt;
            if (clock < 1f / TickHz) return;
            clock = 0f; live = false;
            for (int i = 0; i < px.Length; i++)
            {
                byte b = px[i]; if (b == 0) continue;
                b = b > fade ? (byte)(b - fade) : (byte)0; px[i] = b;
                if (b != 0) live = true;
            }
            dirty = false;
            tex.LoadRawTextureData(px); tex.Apply(false, false); Uploads++;
        }

        public void Dispose()
        {
            Shader.SetGlobalTexture(OzLavaIds.Wake, Texture2D.blackTexture); Shader.SetGlobalVector(OzLavaIds.WakeRect, Vector4.zero);
            if (tex) Object.Destroy(tex);
        }
    }
}
