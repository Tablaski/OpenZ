#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of the 1.5 kit vehicle_colour_fill (a vehicle's colour fills over 0.4 s instead of one frame): the Play check
    /// View/OzVehicleColourFillCheck.cs in its three runs (NORMAL: the fill; OFF: 1.4's one-frame change, to show the check sees the owner's
    /// snap; FRAMES: the fill + a picture every 2nd frame through every capture, driver out, back in and snipe), its last report and the
    /// switch for a look by hand (back on by itself at the next Play).
    /// Pictures and reports: the kit's proof/play folder.</summary>
    public static class OzV15VehicleColourFillMenu
    {
        static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/vehicle_colour_fill/proof/play"));

        [MenuItem("OpenZ/V1.5/Vehicle Colour Fill: Play Check (a level playing)")]
        static void Normal() => Run(0);

        [MenuItem("OpenZ/V1.5/Vehicle Colour Fill: Play Check OFF (1.4's one-frame change)")]
        static void Off() => Run(1);

        [MenuItem("OpenZ/V1.5/Vehicle Colour Fill: Play Check FRAMES (a picture every 2nd frame)")]
        static void FramesRun() => Run(2);

        [MenuItem("OpenZ/V1.5/Vehicle Colour Fill: last report")]
        static void Last() => Debug.Log(OzVehicleColourFillCheck.Report + "\n" + OzVehicleColourFill.Status());

        [MenuItem("OpenZ/V1.5/Vehicle Colour Fill: switch ON")]
        static void SwitchOn() { OzVehicleColourFill.On = true; Debug.Log("[OpenZ] vehicle colour fill: OzVehicleColourFill.On = true"); }

        [MenuItem("OpenZ/V1.5/Vehicle Colour Fill: switch OFF (1.4's snap; a look by hand only)")]
        static void SwitchOff() { OzVehicleColourFill.On = false; Debug.Log("[OpenZ] vehicle colour fill: OzVehicleColourFill.On = false (back on at the next Play)"); }

        static void Run(int mode)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] vehicle colour fill check: start a level in Play first (the normal mode, not 'Zod exact'; L02, camera over open ground)"); return; }
            if (OzVehicleColourFillCheck.Running) { Debug.LogWarning("[OpenZ] vehicle colour fill check: already running"); return; }
            OzVehicleColourFillCheck.Folder = PlayFolder; OzVehicleColourFillCheck.Mode = mode;
            OzVehicleColourFillCheck.Begin();
            Debug.Log("[OpenZ] vehicle colour fill check started (two batches of six vehicles and guns: capture, driver out, back in, sniped; about 1.5 minutes of game time); report in " + PlayFolder);
        }
    }
}
#endif
