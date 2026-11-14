namespace OpenZ.EditorTools
{
    /// <summary>1.4 vehicle_exit_gun: the Play check (View/OzVehicleExitGunCheck.cs: the gun back in his hands after the leap for every driver
    /// and hatch vehicle, nobody shooting an emptied tank, the hit reaction never fighting the death) and its last report. The report goes to
    /// the kit's proof/play. The headless DV176 count: OpenZ/V1.4/Vehicle Exit Gun: Emptied Targets Count (headless, DV176).</summary>
    public static class OzV14VehicleExitGunMenu
    {
        public static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/vehicle_exit_gun/proof/play"));

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Gun: Play Check (a level playing)")]
        static void Play()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzVehicleExitGunCheck.Tag + " start a level in Play first (level 17, the normal mode)"); return; }
            OzVehicleExitGunCheck.Folder = PlayFolder;
            OzVehicleExitGunCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Gun: Play Check, last report")]
        static void Last() => UnityEngine.Debug.Log(OzVehicleExitGunCheck.Running ? OzVehicleExitGunCheck.Tag + " running" : OzVehicleExitGunCheck.Report);
    }
}
