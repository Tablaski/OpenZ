#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// Needs Play with the Unit editor open; the frames, numbers and the report go to the kit's proof folder (frames_&lt;date_time&gt;).</summary>
    public static class OzV15BigTankPitchFramesMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/mammoth_pitch_n26/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Big Tank Pitch Frames")]
        static void Run()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzBigTankPitchFramesCheck.Tag + " enter Play in the Unit editor first (the check stages its own tanks, targets and platforms)."); return; }
            var dir = System.IO.Path.Combine(ProofDir, "frames_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzBigTankPitchFramesCheck.Begin(dir);
            UnityEngine.Debug.Log(OzBigTankPitchFramesCheck.Tag + " started (Mammoth / Bizon / heavy x ramp up / down / along the side wall + Mammoth rams on flat and slope, low side + RTS, every 2nd frame): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Big Tank Pitch Frames", true)]
        static bool Can() => UnityEngine.Application.isPlaying;
    }
}
#endif
