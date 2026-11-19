#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit cloud_shadows: the Play check of the cloud shadows (View/OzCloudShadowsCheck.cs: noon before / after frames from the RTS
    /// camera, night, drift, the fog of war's pair, the graphics chip's time off / on), its last report, and a session switch for looking at the
    /// level with and without them. The reports and pictures go to kit cloud_shadows_visible's proof/play (1.5 kit cloud_shadows_visible:
    /// the first numbers' reports stay in cloud_shadows/proof/play).</summary>
    public static class OzV15CloudShadowsMenu
    {
        public static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/cloud_shadows_visible/proof/play"));

        [MenuItem("OpenZ/V1.5/Cloud Shadows: Play Check (a level playing, RTS camera)")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning(OzCloudShadowsCheck.Tag + " start a level in Play first (L02, the RTS camera at its default angle and zoom over open ground; or the Unit editor on Desert dunes / Snow field)"); return; }
            OzCloudShadowsCheck.Folder = PlayFolder;
            OzCloudShadowsCheck.Begin();
            Debug.Log(OzCloudShadowsCheck.Tag + " started, about 40 s, hands off the keyboard and the mouse: report and pictures in " + PlayFolder);
        }

        [MenuItem("OpenZ/V1.5/Cloud Shadows: Play Check, last report")]
        static void Last() => Debug.Log(OzCloudShadowsCheck.Running ? OzCloudShadowsCheck.Tag + " running" : OzCloudShadowsCheck.Report);

        [MenuItem("OpenZ/V1.5/Cloud Shadows: Toggle On / Off (this session)")]
        static void Toggle()
        {
            OzCloudShadows.Master = OzCloudShadows.Master > 0.5f ? 0f : 1f;
            Debug.Log("[OpenZ] cloud shadows " + (OzCloudShadows.Master > 0.5f ? "ON" : "OFF") + " for this session (nothing saved)");
        }
    }
}
#endif
