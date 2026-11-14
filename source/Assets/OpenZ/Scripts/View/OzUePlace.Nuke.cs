using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 2 (View/OzUePlace.cs Preview, View/OzUeNuke.cs): the NUKE extra's drop preview. No model: the strike's ring on the floor at
    /// its true size (OzUeNuke.KillRadiusM round the pointer: INK 80 %, solid; a dashed inner ring at half of it; a small cross on the
    /// aim point), its metres on the far side as the range ring's, and the words "NUKE STRIKE: EVERYTHING INSIDE IS DESTROYED". While the
    /// last nuke's 30 s run, the ring and the words turn to the no-room colour and say "NUKE READY IN 12 S" (the drop is refused).
    /// </summary>
    internal sealed partial class OzUePlaceDriver
    {
        /// <summary>1.3 final: the solid outer ring = the band's edge (OzUeNuke.DamageRadiusM), the dashed ring = the kill ring (OzUeNuke.KillRadiusM).</summary>
        public const string NukeLine = "NUKE STRIKE: BOTH TEAMS - INNER RING DESTROYED, OUTER RING DAMAGED";
        static readonly List<Vector3> nukeShape = new List<Vector3>(1);
        /// <summary>Checks: the last frame's nuke preview (its ring in m, whether it was the refused look, its words).</summary>
        internal static float NukeRingM; internal static bool NukeRefusedLook; internal static string NukeWords;

        void NukePreview(Vector3 centre, bool ok, string why, bool armed)
        {
            float r = OzUeNuke.DamageRadiusM;
            Color ring = ok ? OzUi.A(OzUi.Ink, 0.8f) : OzUi.A(NoRoom, 0.9f);
            Color text = ok ? NeutralText : NoRoom;
            OzUeDraw.Ring(centre, r, ring, 0f, 0f, 1.5f);
            OzUeDraw.Ring(centre, OzUeNuke.KillRadiusM, OzUi.A(ring, 0.6f), 6f, 5f, 1f);
            OzUeDraw.Cross(centre, 10f, ring);
            OzUeDraw.Label(OzUeMarks.FarTop(centre, r), OzUeMarks.Metres(r), OzUi.A(text, 0.85f), 12f, 3f);
            string line = ok ? NukeLine : string.IsNullOrEmpty(why) ? OzUeNuke.WaitWords : why;
            nukeShape.Clear(); nukeShape.Add(centre);
            Words(centre, nukeShape, r, 0.5f, 0f, 0f, armed, line, out float lineUp, out float hintUp);
            OzUeDraw.Label(centre, line, text, 12f, lineUp, true);
            if (armed) OzUeDraw.Label(centre, NukeHint, OzUi.A(OzUi.Ink2, 0.92f), 12f, hintUp, true);
            NukeRingM = r; NukeRefusedLook = !ok; NukeWords = line;
        }

        public const string NukeHint = "Click to drop  -  Esc cancels";
    }
}
