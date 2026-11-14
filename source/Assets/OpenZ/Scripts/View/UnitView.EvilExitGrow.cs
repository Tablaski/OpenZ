using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// the sim, his walk, his timing and his pick box are unchanged. Cause: the door fit that shrinks a new vehicle in a factory or fort gate and grows it back as it
    /// clears the door (View/UnitView.ExitGrow.cs, ExitFit.cs, ExitFit.K102.cs; row 44's seen-only profile) was for vehicles only: a robot leaves a door at full
    /// size. Every robot fits the fort's gate; EvilZod (DV165's last defense, made inside the blue fort's gate) is about 5 m tall at full size, more than the gate's
    /// outer mouth, so his head and gun went through its lintel and frame.
    /// Now EvilZod, drawn inside a fort's modelled gate, takes the vehicles' gate fit as it is: the same profile (ExitDoors.Of: what the game camera sees, row 44),
    /// his real drawn box (every renderer: his body's skinned box is measured as posed every frame, the gun and the cloak with it), the start just small enough
    /// for every part of the gate ahead of him, the ceiling every frame at the place he is drawn, grown at the vehicles' rate (ExitDoors.GrowRate) as his tail clears
    /// the mouth and the ramp walls, eased down if a ceiling ever falls under his drawn size (ExitFitK102), full size once he is out (at the latest after
    /// ExitMaxSeconds). Only him, only out of a fort's modelled gate; every other robot and every vehicle exactly as before.
    /// A start and a finish line go to the log (in a built exe: Player.log). Switch <see cref="On"/>: off = he walks out at full size as before.
    /// </summary>
    public static class EvilExitGrow
    {
        public static bool On = true;
        /// <summary>Play check: grows started and finished, the smallest start scale, the last start scale, the last grow's seconds, frames a ceiling fell under his drawn
        /// size (it eased down there), frames held at the ceiling.</summary>
        public static int Started, Done, CeilingUnder;
        public static float MinStart = 1f, LastStart = 1f, LastSeconds;
        public static string Report() => "EvilExitGrow: on " + On + "; started " + Started + ", full size " + Done + ", smallest start " + MinStart.ToString("0.000") + ", last start " + LastStart.ToString("0.000")
                                         + ", last grow " + LastSeconds.ToString("0.0") + " s, frames with a ceiling under his drawn size " + CeilingUnder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Started = Done = CeilingUnder = 0; MinStart = 1f; LastStart = 1f; LastSeconds = 0f; }
    }

    public partial class UnitView
    {
        float evilExitT0 = -1f; int evilExitUnder0;

        /// <summary>ExitGrowBind, a robot born inside a building: EvilZod in a fort's modelled gate gets the vehicles' gate fit (ExitFitBind). The door axis is read as the
        /// vehicles' (creation point -> exit point); exitStraight is set only while the fit binds: a robot's Sync never steers by it.</summary>
        void EvilExitBind()
        {
            evilExitT0 = -1f;
            if (!EvilExitGrow.On || Obj == null || !Obj.IsEvilZod || exitFrom == null || !exitFrom.IsFort) return;
            if (!exitFrom.GetCreationPoint(out int ax, out int ay) || !exitFrom.GetCreationMovePoint(out int bx, out int by) || (ax == bx && ay == by)) return;
            exitYaw = Mathf.Atan2(bx - ax, -(by - ay)) * Mathf.Rad2Deg;                          // px (x right, y down) -> world yaw, as ExitStraightBind
            exitBase = transform.localScale; exitAge = 0f; exitCur = 1f; exitFit = 1f;
            exitDepth0 = Mathf.Max(0.5f, Depth(transform.position));
            exitStraight = true;
            bool fitted = ExitFitBind();
            exitStraight = false;
            if (!fitted) { exitCur = 1f; transform.localScale = exitBase; return; }
            exitGrowing = true; evilExitT0 = Time.time; evilExitUnder0 = ExitDoors.CeilingUnder;
            EvilExitGrow.Started++; EvilExitGrow.LastStart = exitFit; EvilExitGrow.MinStart = Mathf.Min(EvilExitGrow.MinStart, exitFit);
            Debug.Log("[OpenZ EvilZod] fort exit grow (row 78): #" + Obj.Id + " starts at " + exitFit.ToString("0.00") + " of his size in the gate of " + exitFrom.Type + " #" + exitFrom.Id);
        }

        /// <summary>ExitGrowStep, a gate-fitted exit, before ExitFitStep: his drawn place this frame (a vehicle's comes from its hull step, ExitHull).</summary>
        void EvilExitFrame()
        {
            if (evilExitT0 >= 0f) exitDrawn = transform.position;
        }

        /// <summary>ExitGrowStep, after ExitFitStep: the frames a ceiling fell under him, and the finish line once he is full size.</summary>
        void EvilExitDone()
        {
            if (evilExitT0 < 0f) return;
            EvilExitGrow.CeilingUnder += ExitDoors.CeilingUnder - evilExitUnder0; evilExitUnder0 = ExitDoors.CeilingUnder;
            if (exitGrowing) return;
            EvilExitGrow.Done++; EvilExitGrow.LastSeconds = Time.time - evilExitT0;
            Debug.Log("[OpenZ EvilZod] fort exit grow (row 78): #" + (Obj != null ? Obj.Id : -1) + " full size " + EvilExitGrow.LastSeconds.ToString("0.0") + " s after the start (start " + EvilExitGrow.LastStart.ToString("0.00")
                      + "), frames with the gate's ceiling under his drawn size " + EvilExitGrow.CeilingUnder);
            evilExitT0 = -1f;
        }
    }
}
