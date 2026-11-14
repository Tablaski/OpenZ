#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.4 kit fps_glow: the glow Play check of the hidden first-person view (View/OzFpsViewPlayCheck.Glow.cs): night on L02 with 3 lit
    /// light tanks beside the viewed one (the white blobs at most 2 % of the screen in the view; the leave compared state by state and by pixels),
    /// the owner's repro (promoted in the view, fire, Esc, the game running: pass A with the kit's fixes off, pass B with them on) and the view's
    /// sun by day (the disc over the sky round it). Editor only: the game has no word of the view.</summary>
    public static class OzV14FpsGlowMenu
    {
        [MenuItem("OpenZ/V1.4/FPS View: Glow + Sun Check (short)")]
        static void Glow()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ FPS check] start L02 in Play first"); return; }
            OzFpsViewPlayCheck.RunGlow();
            Debug.Log("[OpenZ FPS check] glow + sun check started, about 2 minutes, hands off the keyboard and the mouse: report_glow.txt and pictures in " + OzFpsViewPlayCheck.GlowFolder());
        }
    }
}
#endif
