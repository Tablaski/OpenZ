// OpenZ (GPL-3.0)
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 vehicle_colour_fill (view only, no rule): every change of a vehicle's or a gun's drawn owner fills over <see cref="Seconds"/>
    /// instead of one frame. The one road every such change takes is UnitView.SetOwner (the sim's OwnerChanged event through CombatView, and
    /// UnitView.Sync every frame with CaptureHold.Shown: the capture shown when the robot sits inside, the driver out, the driver sniped, a
    /// vehicle given to a team by a level). It hands the change to <see cref="Begin"/>; TeamPaint.Fill.cs draws the fill.
    /// <list type="bullet">
    /// <item>Every vehicle and gun: jeep, light tank, medium tank, heavy tank, APC, missile launcher, crane, mech, the four field guns, and any
    /// later vehicle type (the rule keys on the kind, not on a list). Every part TeamPaint paints moves with it: hull, turret, hatch lid,
    /// decals, team stripes and panels, the team rim and the night team glow. Its lamps already fade (VehicleLights / UnitBlinkers: on over
    /// 0.2 / 0.25 s, off over 0.4 / 0.35 s); with this kit a lamp fading out keeps its own team colour to the end instead of turning grey in
    /// the first frame.</item>
    /// <item>The shuttle: its capture keeps K389's own 0.6 s ease under the capture flash (View/OzShuttleCaptureFx.cs, the owner's); its crew
    /// leaving now fills to the grey too (before: at once).</item>
    /// <item>Not here: robots (they never change owner), buildings (their capture is the flag's zone sweep with the building flash), a first
    /// paint (a new view, a level start, a view back from hiding: drawn at once), a wreck (its burn owns the paint), the dashed grey "empty"
    /// ring on the ground (a marker drawn by TeamReadability from the sim's owner, unchanged).</item>
    /// </list>
    /// The sim's team changes at once as before; nothing else seen or played changes. Off (<see cref="On"/> false) = 1.4's one-frame change.
    /// </summary>
    public static class OzVehicleColourFill
    {
        /// <summary>Off = the change in one frame (1.4). The Play check's OFF run turns it off for its run only.</summary>
        public static bool On = true;
        /// <summary>The fill's length (s), eased in and out (Mathf.SmoothStep). 0.4 s, the length of a lamp fading out
        /// (VehicleLights 0.4 s) and half the flag cloth's look fading in (FlagView.Looks lookFadeIn 0.8 s).</summary>
        public static float Seconds = 0.4f;

        /// <summary>Check counters: fills started, binds (the one renderer walk a fill makes), frames written, ends, turned back half way,
        /// started again from a third team, changes drawn at once because the view was not drawn just before.</summary>
        public static int Fills, Binds, Steps, Ends, Reversals, Restarts, Snaps;
        public static string Last = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = true; Seconds = 0.4f; Fills = Binds = Steps = Ends = Reversals = Restarts = Snaps = 0; Last = ""; }

        /// <summary>UnitView.SetOwner, when the drawn owner changes: the fill starts (true) or the caller sets the team at once (false).</summary>
        public static bool Begin(UnitView v, TeamPaint p, Team to)
        {   // 1.5 review R10 (kit review_r8_r12): the snipe's scale pop starts with every fill: capture, boarding, driver out (View/PlacementPop.FillPop.cs)
            bool on = p && p.BeginFill(to, v);
            if (on) PlacementPop.FillPop(v);
            return on;
        }

        internal static void Started(ZObject o, Team from, Team to)
        {
            Fills++;
            Last = $"{o.Type} #{o.Id}: {from} -> {to} over {Seconds:0.00} s at {Time.time:F2}";
        }

        /// <summary>VehicleLights.Step / UnitBlinkers.Step: a vehicle's lamps take its new owner's colour now, or (false) keep the old one while
        /// they fade out (dark: no crew any more, power still above 0). With this kit off: always now, as before.</summary>
        public static bool LampTeamNow(bool lit, float power) => !On || lit || power <= 0f;

        public static string Status() => $"vehicle colour fill {(On ? "on" : "off")}, {Seconds:0.00} s: fills {Fills}, binds {Binds}, frames written {Steps}, ends {Ends}, "
                                         + $"turned back {Reversals}, restarted {Restarts}, drawn at once (view not drawn just before) {Snaps}; last: {(Last.Length > 0 ? Last : "-")}";
    }
}
