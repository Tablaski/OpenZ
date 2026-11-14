// OpenZ (GPL-3.0) - version 1.3, level 7, lava kit LV1: the counted Play checks of the surface (LV_CONTRACT.md 10). Menu OpenZ/L07 Lava/LV1/...; run in Play on the level named in LV1/README.md.
// Reads and logs. The one thing it changes is OzLavaFx.HoldHeat for the picture's moment and a tuning push of the ramp gains, both only on the menu's word.
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    public static class OzLavaCheckLV1
    {
        const string T = "[LV1] ";

        [MenuItem("OpenZ/L07 Lava/LV1/Check surface (Play, L07)")]
        static void Surface()
        {
            if (!Application.isPlaying) { Debug.LogError(T + "enter Play on L07 first"); return; }
            var z = OzLava.I;
            bool ok = z != null;
            Debug.Log(T + "OzLava on the level: " + (ok ? "PASS" : "FAIL (is this L07?)"));
            if (!ok) return;
            int bad = 0;
            void Line(string what, bool pass, string val = "") { if (!pass) bad++; Debug.Log(T + what + " " + val + ": " + (pass ? "PASS" : "FAIL")); }
            var sheet = GameObject.Find("LAV_Lava"); var mr = sheet ? sheet.GetComponent<MeshRenderer>() : null; var m = mr ? mr.sharedMaterial : null;
            Line("sheet and material", m != null && m.shader != null && m.shader.name == "OpenZ/Lava", m ? m.shader.name : "none");
            Line("maps bound", m != null && m.GetTexture("_LumpMap") && m.GetTexture("_LumpNrm") && m.GetTexture("_FieldMap"));
            Line("maps imported linear (Make lava material was run)", m != null && m.GetTexture("_LumpMap") && !m.GetTexture("_LumpMap").isDataSRGB && m.GetTexture("_LumpNrm") && !m.GetTexture("_LumpNrm").isDataSRGB && m.GetTexture("_FieldMap") && !m.GetTexture("_FieldMap").isDataSRGB);
            var field = m ? m.GetTexture("_FieldMap") as Texture2D : null;
            Line("field map 432 x 536, clamp, no mips", field && field.width == 432 && field.height == 536 && field.wrapMode == TextureWrapMode.Clamp && field.mipmapCount == 1, field ? field.width + " x " + field.height + ", " + field.mipmapCount + " mips" : "none");
            Line("fields 6, biggest first 126 91 64 38 10 3", z.FieldCount == 6 && z.FieldTiles(0) == 126 && z.FieldTiles(1) == 91 && z.FieldTiles(2) == 64 && z.FieldTiles(3) == 38 && z.FieldTiles(4) == 10 && z.FieldTiles(5) == 3, string.Join(" ", Enumerable.Range(0, 6).Select(z.FieldTiles)));
            int n = 0, wrong = 0; var map = ZGame.I.World.Map;
            for (int ty = 0; ty < map.height; ty++)
                for (int tx = 0; tx < map.width; tx++)
                {
                    bool lava = map.LavaAt(tx, ty);
                    bool drawn = OzLava.SurfaceAt(new Vector3(tx * 2 + 1, 0, -(ty * 2 + 1)), out float y);
                    if (lava) { n++; if (!drawn || Mathf.Abs(y - z.lavaY) > 1e-4f || z.FieldOfTile(tx, ty) < 0) wrong++; }
                }
            Line("every lava tile is drawn lava, 332", n == 332 && wrong == 0, n + " tiles, " + wrong + " wrong");
            int b = 0; foreach (var s in new[] { "LAV_Border_N", "LAV_Border_E", "LAV_Border_S", "LAV_Border_W" }) if (GameObject.Find(s)) b++;
            Line("border sheets, at least 3 sides", b >= 3, b.ToString());
            int old = 0; for (int i = 0; i < 32; i++) if (GameObject.Find("LAV_Border_" + i.ToString("00"))) old++;
            Line("terrain's own sheets hidden", old == 0, old.ToString());
            int wat = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("WAT_"));
            Line("WAT_ renderers on L07: 0", wat == 0, wat.ToString());
            float a = Shader.GetGlobalFloat(OzLavaIds.Clock);
            Line("lava clock runs (unpaused, a few seconds into the level)", z.Clock > 0.01f && Mathf.Abs(a - z.Clock) < 0.2f, z.Clock.ToString("0.0") + " / global " + a.ToString("0.0"));
            Line("ramp core stop 253 168 41 at gain 3.4", OzLavaRampTable.Stops[4].g == 168 && Mathf.Approximately(OzLavaRampTable.Gains[4], 3.4f));
            Debug.Log(T + "SURFACE " + (bad == 0 ? "PASS" : "FAIL " + bad));
        }

        [MenuItem("OpenZ/L07 Lava/LV1/Hold heat at the picture (Play)")]
        static void Hold() { OzLavaFx.HoldHeat = !OzLavaFx.HoldHeat; OzLavaIds.Neutral(); OzLavaRampTable.Push(); Debug.Log(T + "HoldHeat " + OzLavaFx.HoldHeat); }

        [MenuItem("OpenZ/L07 Lava/LV1/Check no lava here (Play, L02 L16 L17)")]
        static void Other()
        {
            bool clean = OzLava.I == null && !OzLava.SurfaceAt(new Vector3(40, 0, -40), out _) && GameObject.Find("LAV_Lava") == null;
            Debug.Log(T + "no lava object on this level, SurfaceAt false: " + (clean ? "PASS" : "FAIL"));
            Debug.Log(T + "batches " + UnityStats.batches + ", draw calls " + UnityStats.drawCalls + ": compare by hand with the same camera before the kit (not judged here)");
        }
    }
}
