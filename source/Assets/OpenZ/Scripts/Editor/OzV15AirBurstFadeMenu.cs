#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// smoke): the Play check (View/OzAirBurstFadeCheck.cs) from the menu. Needs Play in a level (a fresh run: case 4 kills one vehicle of ours).
    /// Report + frames go to the kit's proof folder (check_&lt;date_time&gt;/).</summary>
    public static class OzV15AirBurstFadeMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/air_burst_smoke_fade_r11/proof"));
        const string Root = "OpenZ/1.5/Checks/Air Burst Smoke Fade/";

        [UnityEditor.MenuItem(Root + "Run: in game short, scene camera + switch off long, a real head burst (Play, a level)")]
        static void Run()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzAirBurstFadeCheck.Tag + " enter Play in a level first"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzAirBurstFadeCheck.Begin(dir);
            UnityEngine.Debug.Log(OzAirBurstFadeCheck.Tag + " started (about 1 minute): " + dir);
        }
        [UnityEditor.MenuItem(Root + "Run: in game short, scene camera + switch off long, a real head burst (Play, a level)", true)]
        static bool CanRun() => UnityEngine.Application.isPlaying && !OzAirBurstFadeCheck.Running;

        [UnityEditor.MenuItem(Root + "Fire one FLAK in front of the camera now (a look; short in game, long in a scene)")]
        static void Fire()
        {
            var cam = UnityEngine.Camera.main;
            if (!UnityEngine.Application.isPlaying || !cam || !OzExpInGame.InLevel()) { UnityEngine.Debug.LogWarning(OzAirBurstFadeCheck.Tag + " enter Play in a level first"); return; }
            var g = ZGame.I; var p = cam.transform.position + cam.transform.forward * 30f; p.y = g.GroundY(p.x, p.z);
            bool ok = OzExpInGame.Play(OzUeBlasts.Flak, p, 1f, 1f);
            UnityEngine.Debug.Log(OzAirBurstFadeCheck.Tag + " FLAK fired " + ok + "; scene now: " + (OzExpInGame.SceneNow() ?? "none, the player plays (short smoke)"));
        }
        [UnityEditor.MenuItem(Root + "Fire one FLAK in front of the camera now (a look; short in game, long in a scene)", true)]
        static bool CanFire() => UnityEngine.Application.isPlaying;
    }
}
#endif
