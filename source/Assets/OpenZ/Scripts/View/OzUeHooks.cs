using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs): the targets of the one-line hooks the Unit editor kit adds to existing game files, plus the small
    /// input state the editor's modules share. Every target gives the game's own behaviour while no playground runs (Active false), so a
    /// match is unchanged. Hook lines (each calls exactly one member):
    ///  View/PlayerInput.cs `over` (OrdersHeld) and BuildWaypoint (PlainObstacleClick); View/OzBindings.cs Control (SilenceAction);
    ///  UI/OpenZHud.cs Modal (HudModal), TickMinimal (HideMatchHud) and CanExit (BothTeams); View/OzTrackpadScroll.cs Wheel (WheelBlocked);
    ///  UI/OzCheatSpawner.cs TryInstall (NoCheatPanel); View/Zod/ZodBriefing.cs Install (NoCameo); View/AudioDirector.cs AnnounceImpl
    ///  (MuteAnnouncer) and SwitchMusic (MenuMusic); UI/OpenZPauseMenu.cs Update (EscTaken); UI/OpenZCursors.cs Decide (CursorTeam); UI/OpenZUnitOverlay.cs BracketColor
    ///  (TeamBrackets); View/CombatView.cs the death of a wreck-type object (ObstacleDied); View/TrackMarks.cs Ensure and View/CraterField.cs
    ///  OnBlast (NoGroundDressing). P2 adds two RtsCamera lines that read Active and Typing. 1.3 item 26: View/SelectionOverlay.cs LateUpdate
    ///  and View/AttackRangeRings.cs LateUpdate (DarkMarks), UI/OpenZUnitOverlay.cs BracketColor (DarkMarks, DeepMark). 1.3 ramp platforms: View/CombatView.cs
    ///  the death of a platform (PlatformDied), View/PlayerInput.cs HoverAt (PlatformUnder); the others call View/OzUePlatformView.cs directly.
    /// </summary>
    public static class OzUeHooks
    {
        public static bool Active => OzUnitEditor.Active;

        static readonly HashSet<object> typingOwners = new HashSet<object>(), modalOwners = new HashSet<object>();
        /// <summary></summary>
        public static bool Typing => Active && typingOwners.Count > 0;
        public static void SetTyping(object owner, bool on)
        {
            if (owner == null) return;
            bool was = typingOwners.Count > 0;
            if (on) typingOwners.Add(owner); else typingOwners.Remove(owner);
            if (was != typingOwners.Count > 0) Debug.Log($"{OzUnitEditor.Tag} typing {(typingOwners.Count > 0 ? "on: the game keys are silent" : "off")}");
        }
        /// <summary></summary>
        public static bool AnyModal => Active && modalOwners.Count > 0;
        public static void SetModal(object owner, bool open)
        {
            if (owner == null) return;
            bool was = modalOwners.Count > 0;
            if (open) modalOwners.Add(owner); else modalOwners.Remove(owner);
            if (was != modalOwners.Count > 0) Debug.Log($"{OzUnitEditor.Tag} dialog {(modalOwners.Count > 0 ? "open: the field takes no input" : "closed")}");
        }
        /// <summary></summary>
        public static bool RightDragged;
        /// <summary>The frame a right release picked a unit while nothing was picked (OzUeField): that release gives no order.</summary>
        internal static int RightPickFrame = -1;

        /// <summary>D13: the game actions that clash with the editor's keys or mean nothing in the playground (one place: OzBindings.Control).
        /// Kept: W A S D, NEAREST ONLY (Z), EXIT CREW (E), PATROL (Q), STOP (X), HIDE THE HUD (F10).</summary>
        static readonly HashSet<OzAction> silenced = new HashSet<OzAction>
        {
            OzAction.NextRobot, OzAction.NextVehicle, OzAction.NextGun, OzAction.AllRobots, OzAction.AllVehicles, OzAction.AllCannons, OzAction.AllUnits,
            OzAction.FactoryList, OzAction.PlaceCannon, OzAction.LastMessage, OzAction.MinimalUi, OzAction.RepairVehicles, OzAction.HoldPosition,
            OzAction.ArrangeHud, OzAction.HelpOverlay,
        };

        // ------------------------------------------------------------------------------------------------ hook targets
        /// <summary>View/OzBindings.cs Control(): the bound key reads as not there (D13 list; every key while typing or a dialog is open).</summary>
        public static bool SilenceAction(OzAction a) => Active && (Typing || AnyModal || silenced.Contains(a));
        /// <summary>UI/OpenZHud.cs Modal: an editor dialog or the leave question owns the mouse.</summary>
        public static bool HudModal => Active && (AnyModal || OzConfirm.IsOpen);
        /// <summary>UI/OpenZHud.cs TickMinimal: the match HUD (top strip, territory tab, unit panel, command bar, banner, toasts) is hidden
        /// through the HUD's own MINIMAL UI mechanism; the player's saved option is never written.</summary>
        public static bool HideMatchHud => Active;
        /// <summary>View/OzTrackpadScroll.cs Wheel (where the RTS camera reads the wheel, both scroll settings): the wheel never zooms or pans
        /// the camera over an editor panel (its lists scroll) or while a number is typed (the wheel steps it).</summary>
        public static bool WheelBlocked()
        {
            if (!Active) return false;
            if (Typing || AnyModal) return true;
            var hud = OpenZHud.I;
            return hud && hud.PointerOverUi;
        }
        /// <summary>UI/OzCheatSpawner.cs TryInstall: no testing panel P in the playground (P is the dock key there).</summary>
        public static bool NoCheatPanel => Active || OzUnitEditor.IsPlayground(SceneManager.GetActiveScene());
        /// <summary>View/Zod/ZodBriefing.cs Install: Commander Zod's cameo (briefing, transmissions, idle barbs) never comes to the playground.</summary>
        public static bool NoCameo(Scene s) => OzUnitEditor.IsPlayground(s);
        /// <summary>View/AudioDirector.cs AnnounceImpl: the announcer is silent in the playground (no "game paused" on Space, no territory
        /// lines for one side); unit voices and every world sound stay.</summary>
        public static bool MuteAnnouncer => Active;
        /// <summary>View/AudioDirector.cs SwitchMusic (Q11): the playground plays the main menu's music playlist, never the level's calm /
        /// battle music (OzUnitEditor switches this scene's MusicCore off, so the director's own music voices play it).</summary>
        public static bool MenuMusic => Active;
        /// <summary>UI/OpenZPauseMenu.cs: Esc belongs to the editor (its close chain); the pause menu never opens there (D13, Q9).</summary>
        public static bool EscTaken => Active;
        /// <summary>UI/OpenZCursors.cs Decide: the cursor's team = the team of the first live picked unit (both teams can be picked).</summary>
        public static Team CursorTeam(OpenZHud hud)
        {
            var me = hud != null ? hud.Player : Team.Red;
            if (!Active || hud == null || !hud.input) return me;
            var sel = hud.input.Selection;
            for (int i = 0; i < sel.Count; i++)
            {
                var u = sel[i];
                if (u != null && !u.Destroyed && u.Owner != Team.Neutral) return u.Owner;
            }
            return me;
        }
        /// <summary>UI/OpenZHud.cs CanExit: in the playground a picked carrier of either team lets its crew out on EXIT CREW (E).</summary>
        public static bool BothTeams(Team t) => Active && t != Team.Neutral;
        /// <summary>UI/OpenZUnitOverlay.cs BracketColor: the selection brackets in each unit's team colour (spec E209).</summary>
        public static bool TeamBrackets => Active;
        /// <summary>View/CombatView.cs: a dying wreck-type object that is a playground obstacle breaks as a hologram (no stone burst).
        /// True = handled.</summary>
        public static bool ObstacleDied(ZObject o, Vector3 at) => Active && OzUePlayground.IsObstacle(o) && OzUeObstacleViews.Died(o, at);
        /// <summary>View/CombatView.cs: a ramp platform destroyed: its deck goes first, then the deaths of what stood on it. True = handled.</summary>
        public static bool PlatformDied(ZObject o, Vector3 at) => Active && OzUePlatformViews.Died(o);
        /// <summary>View/PlayerInput.cs HoverAt: with Ctrl held, the ramp platform under the pointer (an attack order on it); else nothing, so a
        /// plain right-click on a deck or a ramp is a move there.</summary>
        public static ZObject PlatformUnder(Vector2 mp)
        {
            if (!Active) return null;
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.ctrlKey.isPressed ? OzUePlatformViews.Under(mp) : null;
        }
        /// <summary>View/PlayerInput.cs `over`: no order this frame (a right drag panned the view, or this right release picked a unit).</summary>
        public static bool OrdersHeld => Active && (RightDragged || RightPickFrame == Time.frameCount);
        /// <summary>View/PlayerInput.cs BuildWaypoint: a plain right-click on the obstacle = a move toward it (Ctrl + right-click attacks it;
        /// OzUeField shows the hint toast).</summary>
        public static bool PlainObstacleClick(ZObject target, bool ctrl) => Active && !ctrl && OzUePlayground.IsObstacle(target);
        /// <summary>View/TrackMarks.cs Ensure and View/CraterField.cs OnBlast (D19): the ground dressing made for a terrain (track prints,
        /// dust trails, sand craters) is not laid on the playground's code-drawn floor.</summary>
        public static bool NoGroundDressing => Active || OzUnitEditor.IsPlayground(SceneManager.GetActiveScene());
        /// <summary>View/SelectionOverlay.cs LateUpdate (1.3 item 26): the playground shows a light floor (Snow field, Desert dunes), so the
        /// selection rings and the order dots / markers are drawn in their dark ink. False in every level.</summary>
        public static bool DarkMarks => Active && OzUeBackdrop.LightFloor;
        /// <summary>UI/OpenZUnitOverlay.cs BracketColor and View/AttackRangeRings.cs (1.3 item 26, only while DarkMarks): a team's mark in
        /// its deep tone on a light floor (RED_DEEP / BLUE_DEEP / NEUTRAL_DEEP, by the SHOWN colour), not the light RED_HI / BLUE_HI.</summary>
        public static Color DeepMark(Team t) => (t = TeamColors.Shown(t)) == Team.Red ? OzUi.RedDeep : t == Team.Blue ? OzUi.BlueDeep : OzUi.NeutralDeep;

        /// <summary>OzUnitEditor.OnDestroy: nothing is left typing, modal or dragged for a later session.</summary>
        internal static void Reset()
        {
            typingOwners.Clear(); modalOwners.Clear(); RightDragged = false; RightPickFrame = -1;
        }
    }
}
