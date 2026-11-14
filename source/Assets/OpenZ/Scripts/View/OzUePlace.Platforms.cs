using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// hologram colour at true size: the deck at its height, its uprights, the ramp's two sloped edges down to the footprint on the floor.
    /// No room = #FF7A5E and the toast's words. The ramp's foot looks toward the viewer: the words say so.
    /// </summary>
    internal sealed partial class OzUePlaceDriver
    {
        static readonly List<Vector3> platShape = new List<Vector3>(4);
        /// <summary>Checks: the last frame's platform preview: its ramp side, whether it was the refused look.</summary>
        internal static int PlatformDirShown; internal static bool PlatformRefusedLook;

        void PlatformPreview(string id, Vector3 centre, bool ok, string why, bool armed)
        {
            var ed = OzUnitEditor.I; var w = ed ? ed.World : null; if (w == null) return;
            var p = OzUePlayground.PlatformPlan(w, id, centre, OzUePlayground.PlatformDirNow);
            float W = ZWorld.PlatformWideTiles, top = ZWorld.PlatformBackRows + ZWorld.PlatformDeckRows, L = p.Rows, H = (float)p.DeckM;
            Vector3 P(float across, float along, float y) => OzUePlatformViews.Point(p, across, along, y);
            Color holo = ok ? Color.Lerp(OzUeBackdrop.HiColor, HoloPale, 0.42f) : NoRoom;
            Color lo = OzUi.A(holo, 0.9f), hi = OzUi.A(holo, 0.7f);
            OzUeDraw.Line(P(0f, 0f, 0f), P(W, 0f, 0f), lo, 0f, 0f, 1f); OzUeDraw.Line(P(W, 0f, 0f), P(W, L, 0f), lo, 0f, 0f, 1f);
            OzUeDraw.Line(P(W, L, 0f), P(0f, L, 0f), lo, 0f, 0f, 1f); OzUeDraw.Line(P(0f, L, 0f), P(0f, 0f, 0f), lo, 0f, 0f, 1f);
            OzUeDraw.Line(P(0f, 0f, H), P(W, 0f, H), hi, 0f, 0f, 1f); OzUeDraw.Line(P(W, 0f, H), P(W, top, H), hi, 0f, 0f, 1f);
            OzUeDraw.Line(P(W, top, H), P(0f, top, H), hi, 0f, 0f, 1f); OzUeDraw.Line(P(0f, top, H), P(0f, 0f, H), hi, 0f, 0f, 1f);
            OzUeDraw.Line(P(0f, top, H), P(0f, L, 0f), hi, 0f, 0f, 1f); OzUeDraw.Line(P(W, top, H), P(W, L, 0f), hi, 0f, 0f, 1f);
            foreach (var (a, l) in new[] { (0f, 0f), (W, 0f), (0f, top), (W, top) }) OzUeDraw.Up(P(a, l, 0f), H, hi, 1f);
            string line = ok ? OzUePlayground.FullName(id) + ":  RAMP TOWARD THE VIEW" : string.IsNullOrEmpty(why) ? "NO ROOM" : why;
            platShape.Clear(); platShape.Add(P(0f, 0f, 0f)); platShape.Add(P(W, 0f, 0f)); platShape.Add(P(W, L, 0f)); platShape.Add(P(0f, L, 0f));
            Words(centre, platShape, 0.5f, H, 0f, 0f, armed, line, out float lineUp, out float hintUp);
            OzUeDraw.Label(centre, line, ok ? NeutralText : NoRoom, 12f, lineUp, true);
            if (armed) OzUeDraw.Label(centre, ArmedHint, OzUi.A(OzUi.Ink2, 0.92f), 12f, hintUp, true);
            PlatformDirShown = p.Dir; PlatformRefusedLook = !ok;
        }
    }
}
