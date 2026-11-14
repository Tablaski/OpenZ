#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 39 menus (the CHROMATIC ABERRATION · LENS EDGES switch, View/OzLensEdges.cs): the Play proof View/OzLensEdgesProof.cs
    /// in a running level (L02; the counted checks and the pictures, about five minutes: it restarts L02, loads L16, the main menu and the
    /// Unit editor), and its menus-only part for a second window size (the GRAPHICS rows of the pause menu and of the main menu, the clicks).
    /// The base intensity can also be set by hand in Play: OpenZ.OzLensEdges.Base = 0.3f (session only).</summary>
    public static class OzLensEdgesMenu
    {
        public const string ProofMenu = "OpenZ/V1.2/Graphics: 39 Lens Edges Proof (Play)";
        public const string MenusMenu = "OpenZ/V1.2/Graphics: 39b Lens Edges Menus Check (Play)";

        [MenuItem(ProofMenu)]
        public static void Proof() => OzLensEdgesProof.Run(false);

        [MenuItem(ProofMenu, true)]
        static bool ProofReady() => Application.isPlaying;

        [MenuItem(MenusMenu)]
        public static void Menus() => OzLensEdgesProof.Run(true);

        [MenuItem(MenusMenu, true)]
        static bool MenusReady() => Application.isPlaying;
    }
}
#endif
