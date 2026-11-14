// OpenZ (GPL-3.0) - version 1.2, owner items 66 and 64: the blue unit pictures and the Unit editor's loading picture, checked in the editor.
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2 check of owner items 66 (PLAY AS BLUE: every vehicle and gun picture of the blue side in blue paint) and 64 (the Unit
    /// editor's new loading picture); menu OpenZ/V1.2/64 Blue Pictures And Unit Editor Picture Check (edit mode or Play). Counted lines:
    ///  * the production-card icon of every vehicle and gun (UnitArt.Icon: the build menu's tiles, its big picture, the queue, the place
    ///    button, the HUD's factory list and cannon plates, the ready guns, the unit card's type icon) is icon_&lt;type&gt;_blue for the team
    ///    drawn blue and icon_&lt;type&gt;_red for the team drawn red, the blue file as big as the red one and imported the same way;
    ///  * the badge face of every vehicle and gun at every tier x1 .. x4 (OzBadgeArt.FaceFor: the factory / fort badge over the building,
    ///    the round faces) is face_&lt;unit&gt;_blue for blue and face_&lt;unit&gt;_red for red, as big as the red one and imported the same way;
    ///  * the robots keep their own blue portraits and badge faces;
    ///  * an empty (neutral) vehicle's or gun's picture takes the paint the human is drawn in (UnitArt.HumanDrawnBlue: blue with PLAY AS
    ///    BLUE or YOUR TEAM COLOUR = blue, else red as before);
    ///  * the Unit editor's loading picture (OzUeLoadingPicture.Path) is UE_loading_2, 2560 x 1440, imported as UE_loading_1, which stays
    ///    in the project.
    /// Writes the lines to the log and to checks_editor.txt in the kit's proof folder beside the project (else the player's data folder).
    /// </summary>
    public static class OzBluePicturesCheck
    {
        const string T = "[OzBluePicturesCheck] ";
        static readonly string[] Units = { "jeep", "light", "medium", "mtank", "heavy", "apc", "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon" };   // DV145: + the medium tank (Sim/ZMTank.Type)
        static readonly string[] IconNames = { "jeep", "light_tank", "medium_tank", "mtank", "heavy_tank", "apc", "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon" };
        static readonly string[] Robots = { "grunt", "psycho", "sniper", "tough", "pyro", "laser" };
        const string Icons = "Assets/OpenZ/Resources/OpenZ/UI/Icons/", Badge = "Assets/OpenZ/Resources/OpenZ/ProductionBadge/x", Loading = "Assets/OpenZ/Resources/OpenZ/Loading/";

        [MenuItem("OpenZ/V1.2/64 Blue Pictures And Unit Editor Picture Check")]
        public static void Run()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); }
            // the sim teams drawn blue and red now (the colour option may trade them; PLAY AS BLUE draws the sim's own colours)
            Team blue = TeamColors.Shown(Team.Blue) == Team.Blue ? Team.Blue : Team.Red, red = blue == Team.Blue ? Team.Red : Team.Blue;
            sb.AppendLine("drawn blue: sim " + blue + ", drawn red: sim " + red + " (YOUR TEAM COLOUR swapped: " + TeamColors.Swap + ", PLAY AS BLUE active: " + OzPlayBlue.Active + ")");

            for (int i = 0; i < Units.Length; i++)
            {
                string u = Units[i], n = IconNames[i];
                Texture2D tb = UnitArt.Icon(u, blue), tr = UnitArt.Icon(u, red);
                bool names = tb && tr && tb.name == "icon_" + n + "_blue" && tr.name == "icon_" + n + "_red";
                Check(names && tb.width == tr.width && tb.height == tr.height,
                      "icon " + u + ": blue side " + Name(tb) + ", red side " + Name(tr));
                Check(SameImport(Icons + "icon_" + n + "_red.png", Icons + "icon_" + n + "_blue.png", out string iw), "icon " + u + ": the blue file imported as the red one (" + iw + ")");
                string fb = OzBadgeArt.FaceFor(u, blue), fr = OzBadgeArt.FaceFor(u, red);
                Check(fb == "face_" + u + "_blue" && fr == "face_" + u + "_red", "badge face " + u + ": blue side " + fb + ", red side " + fr + " (unit art in the badge: " + (OpenZUnitOverlay.TimerPortrait > 0f) + ")");
                for (int t = 1; t <= OzBadgeArt.MaxTier; t++)
                {
                    var b = Resources.Load<Texture2D>(OzBadgeArt.Root + t + "/face_" + u + "_blue");
                    var r = Resources.Load<Texture2D>(OzBadgeArt.Root + t + "/face_" + u + "_red");
                    bool same = SameImport(Badge + t + "/face_" + u + "_red.png", Badge + t + "/face_" + u + "_blue.png", out string fw);
                    Check(b && r && b.width == r.width && b.height == r.height && same, "badge face " + u + " x" + t + ": blue " + Name(b) + ", red " + Name(r) + ", import " + fw);
                }
            }
            foreach (var rb in Robots)
            {
                var p = UnitArt.Portrait(rb, blue);
                string fb = OzBadgeArt.FaceFor(rb, blue);
                Check(p && p.name.Contains("blue") && fb == "face_" + rb + "_blue", "robot " + rb + ": blue portrait " + Name(p) + ", blue badge face " + fb);
            }
            // an empty (neutral) vehicle / gun: the human's drawn paint (red now in edit mode unless YOUR TEAM COLOUR is blue)
            string hk = UnitArt.HumanDrawnBlue ? "_blue" : "_red"; int nOk = 0; var nBad = new StringBuilder();
            for (int i = 0; i < Units.Length; i++)
            {
                var tn = UnitArt.Icon(Units[i], Team.Neutral);
                if (tn && tn.name == "icon_" + IconNames[i] + hk) nOk++; else nBad.Append(" " + Units[i] + " " + Name(tn));
            }
            Check(nOk == Units.Length, "empty (neutral) vehicles and guns in the human's paint (" + hk.Substring(1) + "): " + nOk + " of " + Units.Length + nBad);

            var pic = Resources.Load<Texture2D>(OzUeLoadingPicture.Path);
            var old = Resources.Load<Texture2D>("OpenZ/Loading/UE_loading_1");
            Check(OzUeLoadingPicture.Path == "OpenZ/Loading/UE_loading_2" && pic && pic.width == 2560 && pic.height == 1440,
                  "the Unit editor's loading picture: " + OzUeLoadingPicture.Path + " " + Name(pic));
            string lw = "-";
            bool sameLoad = old != null && SameImport(Loading + "UE_loading_1.png", Loading + "UE_loading_2.png", out lw);
            Check(sameLoad, "UE_loading_1 still in the project (" + Name(old) + ", not shown) and UE_loading_2 imported as it is (" + lw + ")");

            string head = T + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail) + " checks";
            sb.Insert(0, head + "\n");
            string dir = Folder();
            try { System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks_editor.txt"), sb.ToString()); }
            catch (System.Exception e) { sb.AppendLine("could not write " + dir + ": " + e.Message); }
            if (fail == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
            Debug.Log(head + "; lines in " + System.IO.Path.Combine(dir, "checks_editor.txt"));
        }

        static string Name(Texture2D t) => t ? t.name + " " + t.width + "x" + t.height : "none";

        /// <summary>The two textures are imported alike (type, colour space, mip maps, size cap, compression, filter, wrap, alpha).</summary>
        static bool SameImport(string a, string b, out string what)
        {
            var ia = AssetImporter.GetAtPath(a) as TextureImporter; var ib = AssetImporter.GetAtPath(b) as TextureImporter;
            if (!ia || !ib) { what = "no importer for " + (!ia ? a : b); return false; }
            bool same = ia.textureType == ib.textureType && ia.sRGBTexture == ib.sRGBTexture && ia.mipmapEnabled == ib.mipmapEnabled && ia.isReadable == ib.isReadable
                        && ia.npotScale == ib.npotScale && ia.maxTextureSize == ib.maxTextureSize && ia.textureCompression == ib.textureCompression
                        && ia.filterMode == ib.filterMode && ia.wrapMode == ib.wrapMode && ia.alphaIsTransparency == ib.alphaIsTransparency && ia.alphaSource == ib.alphaSource
                        && ia.anisoLevel == ib.anisoLevel && Mathf.Approximately(ia.mipMapBias, ib.mipMapBias);
            what = (same ? "same" : "DIFFERENT") + ": " + ib.textureType + ", sRGB " + ib.sRGBTexture + ", mips " + ib.mipmapEnabled + ", max " + ib.maxTextureSize + ", " + ib.textureCompression + ", " + ib.filterMode;
            return same;
        }

        /// <summary></summary>
        static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var kit = System.IO.Path.Combine(stage, "k64_blue_icons_comms_ue_picture");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k64_blue_icons_comms_ue_picture");
        }
    }
}
