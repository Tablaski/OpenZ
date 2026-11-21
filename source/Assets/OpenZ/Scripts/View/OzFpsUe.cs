// OpenZ (GPL-3.0) - version 1.5 kit fps_in_ue: the private first-person view (View/OzFpsView.cs) in the Unit editor's playground too. View only:
// no rule, no Sim file, no DV row.
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <para>Why Alt+F did nothing there: OzFpsView.Busy refused every entry while the playground ran (`if (OzUeHooks.Active) return "the Unit
    /// editor";`: the quiet ui_disabled and the log line "[OpenZ FPS] Alt+F: not entered (the Unit editor)"), and three tests asked for the
    /// player's team (Refuse, Leave, LeaveWhy: u.Owner == game.player), so a blue unit there would have been refused as "not an own unit"
    /// even without that line. OzFpsFeel.Interrupt also ended any glide out at once in the playground.</para>
    /// <para>What this kit does: the view goes in in the playground exactly as in a level (the same glide and whoosh, the same eye, gun copy,
    /// moves, Shift sprint, left-click fire, Esc / Alt+F leave), on any unit of either team the owner has picked there (both teams are his there:
    /// <see cref="Mine"/>), as long as no editor moment owns the mouse or the keys (<see cref="Busy"/>). The view hides the HUD canvas, and the
    /// editor's whole interface lives on it (panels, top bar, inspector, toasts, world marks and their labels: OzUeDrawDriver draws only with
    /// the HUD shown), so it goes and comes back with it, exactly as with F10; the eye button (its own canvas) is hidden while the view is on.
    /// While the view is on the editor's own modules wait: the camera module (OzUeCamera: no glide, zoom anchor or double-click centring from
    /// the view's clicks), the field's clicks and hover (OzUeField: no pick, inspect or hint from the view's clicks; this frame's sim events
    /// still go to its toasts as always), and the editor keys (OzUeKeysDriver: Esc is the view's, T B I P H Home Delete 1 2 3 [ ] wait).
    /// SPACE stays the editor's play / pause (it means nothing else in the view): the owner can freeze a fight from inside the view and look
    /// round it; the view itself runs on unscaled time, so it enters, looks and glides while the playground is paused (move orders given
    /// while paused are carried out when it plays again, as every order there). F10 means nothing in the view, as in a level.</para>
    /// <para><see cref="Mine"/><see cref="Busy"/>
    /// editor's files reads OzFpsView.On, false whenever the view is off.</para>
    /// </summary>
    public static class OzFpsUe
    {
        /// <summary>OzFpsView Refuse / Leave / LeaveWhy: the unit may be looked out of by this player. In a level: its owner is the player (the old
        /// test). In the Unit editor's playground: either team (the player orders both there, View/PlayerInput.BothSides.cs), never the neutral side.</summary>
        public static bool Mine(ZObject u, Team player) => u != null && (OzUeHooks.Active ? u.Owner != Team.Neutral : u.Owner == player);

        /// <summary>OzFpsView.Busy: an editor moment that owns the mouse or the keys (null = none; always null outside the playground): a unit
        /// being placed (click-to-place armed, a drag from the dock, the field's input held), a number being typed, an editor dialog open (the
        /// leave question and the HUD's own modal moments are OzFpsView's own tests).</summary>
        public static string Busy()
        {
            if (!OzUeHooks.Active) return null;
            if (OzUePlace.Busy || OzUePick.InputHeld) return "the Unit editor is placing a unit";
            if (OzUeHooks.Typing) return "a number is being typed";
            if (OzUeHooks.AnyModal) return "an editor dialog is open";
            return null;
        }
    }

    public partial class PlayerInput
    {
        /// <summary>fps_in_ue review fix 1: what the first-person view's centre points at (PlayerInput.FpsTargetAtCentre). In a level: the game's own
        /// hover pick, exactly as before (HoverAt: the player's own units and minions are never candidates there). In the Unit editor's playground
        /// HoverAt takes every unit of both teams (BothSides), so the viewed unit itself, its squad mates beside the eye or a unit of its own side in
        /// front won the pick and the click fired at nothing; here the same pass (the pre-cull, the fog, the pick boxes; a unit beats a building)
        /// over the objects that could be its target: units, guns and buildings of the other side, never the neutral side, never a bridge.</summary>
        ZObject FpsHoverAt(ZObject u, Vector2 mp)
        {
            if (!BothSides) return HoverAt(mp);
            if (u == null || W == null) return null;
            Team side = SideOf(u); ZObject h = null, hu = null;
            bool far = HoverPreCull && FarBegin(mp);
            foreach (var o in W.Objects)
            {
                if (o == null || o == u || o.Destroyed || o.Owner == side || o.Owner == Team.Neutral || o.IsBridge) continue;
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon && o.Kind != ZKind.Building) continue;
                if (OzFogOfWar.Covers(o)) continue;
                if (!(far && HoverFar(o, mp)) && ScreenRect(o).Contains(mp)) { if (o.IsUnit) hu = o; else h = o; }
            }
            return hu ?? h;
        }
    }
}
