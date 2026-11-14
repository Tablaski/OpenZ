#if !OPENZ_HARNESS
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 21 check (the wrench key's own spanner glyph, UI/OpenZHud.RepairKeyIcon.cs). Edit mode or Play:
    /// the nine glyph files Kit/Icons/ic_spanner_a / _b / _c at 48 / 96 / 192 px load as sprites of their size and are imported as
    /// their sibling ic_wrench_&lt;size&gt; (the importer's settings and the imported texture: mip maps, filter, aniso, mip bias,
    /// format, wrap), and the set's ic_wrench files are still there (the bridge badge, the crane's mark). In Play with the HUD built:
    /// the wrench key draws the picked spanner (OpenZHud.RepairKeyIconVariant). Counted PASS / FAIL lines in the console and in
    /// icon_check.txt of the kit's proof folder (found from the project's place, else the player's data folder).</summary>
    public static class OzRepairKeyIconCheck
    {
        const string KitName = "k10b_wrench_icon";
        const string IconsPath = "Assets/OpenZ/Resources/OpenZ/Kit/Icons/";
        static readonly string[] Variants = { "spanner_a", "spanner_b", "spanner_c" };
        static readonly int[] Tiers = { 48, 96, 192 };

        [MenuItem("OpenZ/V1.2/HUD: 21 Repair Key Icon Check (edit mode or Play)")]
        static void Check()
        {
            var log = new StringBuilder();
            int pass = 0, fail = 0;
            void Line(string what, bool ok)
            {
                if (ok) pass++; else fail++;
                log.Append(ok ? "PASS " : "FAIL ").Append(what).Append('\n');
            }

            foreach (var v in Variants)
                foreach (int t in Tiers)
                {
                    string name = "ic_" + v + "_" + t, sib = "ic_wrench_" + t;
                    var s = Resources.Load<Sprite>("OpenZ/Kit/Icons/" + name);
                    var w = Resources.Load<Sprite>("OpenZ/Kit/Icons/" + sib);
                    Line($"{name}: a sprite of {(s ? s.texture.width + " x " + s.texture.height + " px, rect " + s.rect.width + " x " + s.rect.height : "(none)")}",
                         s && s.texture.width == t && s.texture.height == t && Mathf.Approximately(s.rect.width, t) && Mathf.Approximately(s.rect.height, t));
                    var ti = AssetImporter.GetAtPath(IconsPath + name + ".png") as TextureImporter;
                    var tw = AssetImporter.GetAtPath(IconsPath + sib + ".png") as TextureImporter;
                    string d = ti == null || tw == null ? "importer missing" : ImporterDiff(ti, tw);
                    Line($"{name}: importer as {sib}'s ({(d.Length == 0 ? "the same settings" : d)})", d.Length == 0);
                    string td = s && w ? TextureDiff(s.texture, w.texture) : "texture missing";
                    Line($"{name}: imported texture as {sib}'s ({(td.Length == 0 ? "mips " + s.texture.mipmapCount + ", " + s.texture.filterMode + ", aniso " + s.texture.anisoLevel + ", bias " + s.texture.mipMapBias.ToString("0.00") + ", " + s.texture.format : td)})", td.Length == 0);
                }

            int kept = 0;
            foreach (int t in Tiers) if (Resources.Load<Sprite>("OpenZ/Kit/Icons/ic_wrench_" + t)) kept++;
            Line($"the set's ic_wrench_48 / 96 / 192 are still there for the bridge badge and the crane's mark ({kept} of 3)", kept == 3);

            if (Application.isPlaying)
            {
                var hud = OpenZHud.I;
                var g = hud ? hud.RepairKeyGlyph : null;
                string sp = g && g.sprite ? g.sprite.name : "(none)";
                Line($"Play: the wrench key draws '{sp}' (variant {OpenZHud.RepairKeyIconVariant}: {OpenZHud.RepairKeyIcon}; the tier for {OpenZHud.RepairGlyph:0} u at {OzPx.K:0.00} px per unit: {Hud.IconRel(OpenZHud.RepairKeyIcon, OpenZHud.RepairGlyph)})",
                     g && g.sprite && sp.StartsWith("ic_" + OpenZHud.RepairKeyIcon + "_"));
            }
            else log.Append("note: the Play line (the key's own glyph) runs only in Play with a level loaded\n");

            string head = $"1.2 item 21 repair key icon check: {pass} PASS, {fail} FAIL";
            log.Insert(0, head + "\n");
            Debug.Log("[OpenZ] " + head + "\n" + log);
            try
            {
                var dir = Folder();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "icon_check.txt"), log.ToString());
                Debug.Log("[OpenZ] 1.2 item 21: " + Path.Combine(dir, "icon_check.txt"));
            }
            catch (IOException e) { Debug.LogWarning("[OpenZ] 1.2 item 21: the log could not be written: " + e.Message); }
        }

        static string ImporterDiff(TextureImporter a, TextureImporter b)
        {
            var sb = new StringBuilder();
            void D<T>(string n, T x, T y) { if (!Equals(x, y)) sb.Append(n).Append(' ').Append(x).Append(" vs ").Append(y).Append("; "); }
            D("type", a.textureType, b.textureType);
            D("sprite mode", a.spriteImportMode, b.spriteImportMode);
            D("ppu", a.spritePixelsPerUnit, b.spritePixelsPerUnit);
            D("mip maps", a.mipmapEnabled, b.mipmapEnabled);
            D("mip filter", a.mipmapFilter, b.mipmapFilter);
            D("filter", a.filterMode, b.filterMode);
            D("aniso", a.anisoLevel, b.anisoLevel);
            D("mip bias", a.mipMapBias, b.mipMapBias);
            D("compression", a.textureCompression, b.textureCompression);
            D("wrap", a.wrapMode, b.wrapMode);
            D("sRGB", a.sRGBTexture, b.sRGBTexture);
            D("alpha is transparency", a.alphaIsTransparency, b.alphaIsTransparency);
            D("npot", a.npotScale, b.npotScale);
            D("readable", a.isReadable, b.isReadable);
            D("max size", a.maxTextureSize, b.maxTextureSize);
            var sa = new TextureImporterSettings(); a.ReadTextureSettings(sa);
            var sb2 = new TextureImporterSettings(); b.ReadTextureSettings(sb2);
            D("sprite mesh", sa.spriteMeshType, sb2.spriteMeshType);
            D("pivot", sa.spritePivot, sb2.spritePivot);
            D("border", sa.spriteBorder, sb2.spriteBorder);
            return sb.ToString();
        }

        static string TextureDiff(Texture2D a, Texture2D b)
        {
            var sb = new StringBuilder();
            void D<T>(string n, T x, T y) { if (!Equals(x, y)) sb.Append(n).Append(' ').Append(x).Append(" vs ").Append(y).Append("; "); }
            D("mips", a.mipmapCount, b.mipmapCount);
            D("filter", a.filterMode, b.filterMode);
            D("aniso", a.anisoLevel, b.anisoLevel);
            D("mip bias", a.mipMapBias, b.mipMapBias);
            D("format", a.format, b.format);
            D("wrap", a.wrapMode, b.wrapMode);
            return sb.ToString();
        }

        /// <summary></summary>
        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                {
                    var k = Path.Combine(stage, KitName);
                    if (Directory.Exists(k)) return Path.Combine(k, "proof");
                }
            }
            return Path.Combine(Application.persistentDataPath, "repair_key_icon_check");
        }
    }
}
#endif
