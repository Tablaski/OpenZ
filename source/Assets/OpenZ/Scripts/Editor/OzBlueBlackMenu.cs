#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 items 49 / 48 / 56 Play proof (View/OzBlueBlackProof.cs): started on the MainMenu scene in Play, about five minutes,
    /// hands off. The comms window on the battlefield screen with PLAY AS BLUE off / on / off; the flyby globals zeroed (a new process)
    /// and PLAY AS BLUE on levels 2, 16 and 17, the boot's globals and level 16 again, a red level 2 with its briefing skipped at once
    /// (the world drawn at 1 s and 6 s each); the match-state lines in that level and the frame rate bench's black-world rule; the Unit
    /// </summary>
    public static class OzBlueBlackMenu
    {
        public const string ProofMenu = "OpenZ/V1.2/Play As Blue: 11b Black World, Comms And Lines Proof (Play)";

        [MenuItem(ProofMenu)]
        public static void Proof() => OzBlueBlackProof.Run();

        [MenuItem(ProofMenu, true)]
        static bool ProofReady() => Application.isPlaying;
    }
}
#endif
