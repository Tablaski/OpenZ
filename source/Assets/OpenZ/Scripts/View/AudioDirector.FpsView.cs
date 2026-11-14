// OpenZ (GPL-3.0) - version 1.4 kit fps_view, fix round 2: the walk of the unit the hidden first-person view (View/OzFpsView.cs) moves may be heard.
using OpenZ.Sim;

namespace OpenZ
{
    public partial class AudioDirector
    {
        static readonly ZObject[] fpsOne = new ZObject[1];

        /// <summary>1.4 FPS VIEW: a move order of the first-person view (View/PlayerInput.FpsView.cs FpsMove) marks the unit's walk as heard, as the
        /// order marker of a right-click does (NoteWalkOrder: its steps for the next WalkSoundHold s). The order cue, the voice and the engine rev
        /// stay out (these orders go again and again while a key is held). Fix round 3: the view also calls it once a second while a move key is
        /// held between its orders (one per 2 tiles), so the walk stays heard. Nothing else of the director changes.</summary>
        internal static void FpsWalkOrder(ZObject u)
        {
            var a = I; if (!a || u == null) return;
            fpsOne[0] = u; a.NoteWalkOrder(fpsOne); fpsOne[0] = null;
        }
    }
}
