using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A robot getting out of a carrier is picked where its figure is DRAWN.
    /// The rules stand every robot of a squad let out of the shuttle (or an APC; a tank's hatch, a gun's seat) on its own spot at once; its own
    /// model is spawned there but hidden (BoardingView.HideLive) while an exit figure walks out to it. K431 (View/BoardingView.ExitRings.cs)
    /// put the rings, the frame, the health bar and the mouse-over box on that figure, but the pick box (<see cref="ScreenRect"/>: the click,
    /// the box, the double-click, a tap, the HUD's own-unit-under-the-pointer test) still stood on the hidden model: on the empty back slot
    /// behind the ship while the figure walked down the ramp and on to it (about 2.5 s in the bay and on the ramp, then the walk to the slot).
    /// A click on a walking robot hit nothing (or the ship), so the squad could not be selected until its walk was over.
    /// Now (switch <see cref="ExitPickOn"/>, on), with the overlays' own answer (BoardingView.ExitFigureState):
    ///  * the figure is out (from the ramp's sill on; the APC's door; a hatch, a seat): the robot's pick box is a robot's box at the figure's
    ///    feet (its hero size, 16 px at least, as any robot), so a click on it picks its squad and a box over it takes the squad;
    ///  * the figure is still inside the hull (bench, aisle, doorway; a hatch / seat figure waiting its turn): no pick box (nothing is drawn
    ///    on its spot; K431 draws no ring or frame there either);
    ///  * a click on a robot walking out picks among the robots walking out under the pointer only (<see cref="ExitPickFirst"/>): the ship's
    ///    own frame (its hull's silhouette) covers its ramp from the RTS camera, and Zod's random pick among everything under the pointer would
    ///    hand the click to the ship about every second time.
    /// Orders are not changed: rule DV132 (Sim/ZObject.ExitWalk.cs) takes an order given meanwhile at once (SendWaypoints, Alan's
    /// acknowledgement) and the robot starts it from its spot the moment its figure stands there, the same for the APC's passengers and (1.4
    /// DV171) the driver let out of a tank or the mech; a gun's crew (Zod's own EJECT) acts at once, as before. View only: nothing of the rules changes.
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>On: robots getting out of a carrier are picked at their walking figures (off: on their hidden models' spots, as before).</summary>
        public static bool ExitPickOn = true;
        /// <summary>Proof counters: pick boxes given at a walking figure, robots left without a pick box (still inside), clicks narrowed to
        /// the robots walking out.</summary>
        public static int ExitPickAtFigure, ExitPickInside, ExitPickClicks;

        static readonly Rect NoPickRect = new Rect(-9999f, -9999f, 0f, 0f);

        /// <summary>ScreenRect, a robot: true = it is getting out of a carrier and r is its pick box (at its figure; none while inside).</summary>
        bool ExitPick(ZObject o, Camera c, out Rect r)
        {
            r = default;
            if (!ExitPickOn || !c) return false;
            int st = BoardingView.ExitFigureState(o, out var fig);
            if (st == 0) return false;                                                                      // no exit walk: picked where its model is, as before
            if (st == 1) { r = NoPickRect; ExitPickInside++; return true; }                               // still inside the hull: nothing drawn on its spot
            float tall = OzHeroSquadView.Tall(o);
            r = MinSize(BoxRect(c, fig, 0.6f * tall, 1.85f * tall), 16f * UiK); ExitPickAtFigure++;
            return true;
        }

        /// <summary>A robot walking out of a carrier, marked and picked at its figure now.</summary>
        public static bool WalkingOut(ZObject o) => ExitPickOn && o != null && o.Kind == ZKind.Robot && BoardingView.ExitFigureState(o, out _) == 2;

        /// <summary>CollectClick: when the click hits a robot walking out, only the robots walking out under the pointer count (not the ship
        /// it leaves, whose frame covers the ramp).</summary>
        void ExitPickFirst(List<ZObject> hits)
        {
            if (!ExitPickOn || hits.Count < 2) return;
            bool any = false;
            foreach (var o in hits) if (WalkingOut(o)) { any = true; break; }
            if (!any) return;
            hits.RemoveAll(o => !WalkingOut(o)); ExitPickClicks++;
        }

        /// <summary>Proof: the left click's own pick at mp, as Update does on a left release without a drag (no Shift, a single click).</summary>
        public void ProofClick(Vector2 mp)
        {
            if (game == null || game.World == null) return;
            additive = false; lastClickType = null;
            CollectClick(mp, false);
            if (Selection.Count > 0) SelectTime = Time.unscaledTime;
        }
    }
}
