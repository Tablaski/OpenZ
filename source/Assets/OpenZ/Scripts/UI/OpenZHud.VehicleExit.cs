using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 rule DV171 EXIT DRIVER (Sim/ZObject.VehicleExit.cs), the control: a tab EXIT DRIVER in the place of the gun's EXIT CREW tab, about
    /// 80 % of its size (106 x 26 u against 132 x 32 u), with the same right and bottom edge (x 462, y -6 over the unit panel's top), so it
    /// never covers the map more than EXIT CREW does and stays clear of the under-attack tab (x 18..268) and the command bar.
    /// <list type="bullet">
    /// <item>Shown while EXIT CREW is not (no selected gun / APC / landed shuttle with a crew to let out) and the selection holds at least one
    /// of the player's own crewed light, medium or heavy tanks or mechs. Live when at least one of them may let its driver out now
    /// (ZObject.CanExitDriver); else greyed with the reason in its tooltip, and a click gives the disabled blip.</item>
    /// <item>A click, the EXIT CREW key (E, its touch key, the gamepad's L3: OpenZHud.ExitSelected) lets out the driver of every selected
    /// one that may (PlayerInput.EjectUnit, the gun's own order path). With a gun and a tank selected together EXIT CREW shows and the key
    /// exits the gun only. <see cref="KeyExitsDrivers"/> off: the key and the touch key are the gun's only (the tab still works, its tooltip
    /// then names no key).</item>
    /// <item>No accident from a click on the tank: the right-click on the hull and the EXIT cursor stay Zod's (only what ZObject.CanEjectCrew
    /// offers: APCs, guns, a landed shuttle), so ordering a tank to move or fight cannot let its driver out. And the tab appears only
    /// <see cref="DriverTabShowAfterSec"/> after the click that selected the tank (longer than the 0.35 s double-click window of
    /// PlayerInput.CollectClick): a double-click, or a quick second click, on a tank that stands where the tab appears lands on the tank,
    /// not on the tab (fix round 1). A tab that already shows stays (a click on it is a click on a button the player sees).</item>
    /// <item>Fix round 2: nor after EXIT CREW. With a gun and a tank selected, the gun's crew leaves, the gun turns neutral and drops out of
    /// the selection (PlayerInput), EXIT CREW hides and EXIT DRIVER would show at once in its place (its rect lies inside EXIT CREW's): the
    /// second click of a double-click on EXIT CREW, or a second press of its key, let the tank's driver out too. Now the tab also waits
    /// <see cref="DriverTabShowAfterSec"/> after the last frame EXIT CREW showed or was used (<see cref="CrewTabRecent"/>), and the key, the
    /// touch key and the gamepad (ExitSelected) let no driver out in that time either.</item>
    /// </list>
    /// The tab's state is asked from the rules at most every 0.2 s of real time (and at once when the selection changes): a read only.
    /// </summary>
    public partial class OpenZHud
    {
        /// <summary>The EXIT DRIVER tab in unit-panel units (the EXIT CREW tab: 330, -38, 132 x 32).</summary>
        public static readonly OzRect DriverTabRect = new OzRect(356, -32, 106, 26);
        public const float DriverTabFont = 11f, DriverTabTracking = 0.16f, DriverTabRefreshSec = 0.2f;
        /// <summary>A hidden tab appears this long (real time) after the click that made the selection (PlayerInput.SelectTime): longer than
        /// the 0.35 s double-click window, so the second click of a double-click never lands on it.</summary>
        public const float DriverTabShowAfterSec = 0.4f;
        /// <summary>The EXIT CREW key also lets drivers out when no gun / carrier is selected (off: the key is the gun's only).</summary>
        public static bool KeyExitsDrivers = true;
        /// <summary>Proof: frames the tab stayed hidden because the selection, or EXIT CREW shown or used, was newer than DriverTabShowAfterSec;
        /// EXIT CREW key presses (and touch / gamepad) that let no driver out for the same reason (fix round 2).</summary>
        public static int DriverTabHeldBack, DriverKeyHeldBack;

        HudButton driverBtn; float driverTabAt = -10f, crewTabAt = -10f; int driverTabHash;
        /// <summary>Fix round 2: EXIT CREW showed or was used less than DriverTabShowAfterSec ago (real time).</summary>
        public bool CrewTabRecent => Time.unscaledTime - crewTabAt < DriverTabShowAfterSec;
        /// <summary>Fix round 2, ExitSelected: a gun's / carrier's crew was let out (or EXIT CREW was pressed with one selected) now.</summary>
        void CrewExitUsed() => crewTabAt = Time.unscaledTime;
        /// <summary>Fix round 2, ExitSelected with no gun / carrier selected: the drivers, unless EXIT CREW was shown or used just now.</summary>
        bool KeyDrivers()
        {
            if (!KeyExitsDrivers) return false;
            if (CrewTabRecent) { DriverKeyHeldBack++; return false; }
            return ExitDrivers();
        }
        /// <summary>The checks: the tab's rect, shown and live now, the reason it is greyed.</summary>
        public RectTransform DriverExitButtonRect => driverBtn != null ? driverBtn.Rt : null;
        public bool DriverTabShown => driverBtn != null && driverBtn.Rt && driverBtn.Rt.gameObject.activeSelf;
        public bool DriverTabLive => driverBtn != null && driverBtn.Interactable;
        public string DriverTabTip => driverBtn != null ? driverBtn.Tooltip : null;

        void BuildDriverTab()
        {
            driverBtn = HudButton.Make(unitPanel, "DriverExit", "EXIT DRIVER", new Vector2(DriverTabRect.w, DriverTabRect.h), HudButtonKind.Small, DriverTabFont, DriverTabTracking);
            driverBtn.Rt.Place(DriverTabRect);
            driverBtn.Tooltip = DriverTip(true, false, null);
            driverBtn.OnClick = c => { if (!ExitDrivers()) AudioDirector.UI("ui_order_invalid"); };
            AddBlocker(driverBtn.Rt);
            driverBtn.Rt.gameObject.SetActive(false);
        }

        /// <summary>One of the player's own crewed tanks or mechs, alive (the tab is offered for it; CanExitDriver says whether now).</summary>
        public bool DriverTabOf(ZObject o) => o != null && ZWorld.VehicleExitDeviation && W != null && !W.GameOver && !o.Destroyed && o.KillAt < 0 && o.SupportsDriverExit && o.Drivers.Count > 0
                                              && (o.Owner == Player || OzUeHooks.BothTeams(o.Owner));

        /// <summary>The driver of every selected tank / mech that may let him out now climbs out. True: at least one did.</summary>
        public bool ExitDrivers()
        {
            bool any = false;
            foreach (var o in new List<ZObject>(input.Selection)) if (DriverTabOf(o) && o.CanExitDriver(out _)) any |= input.EjectUnit(o);
            driverTabAt = -10f;                                                                              // the tab asks again at once
            return any;
        }

        /// <summary>RefreshUnitPanel, every frame after EXIT CREW's own line.</summary>
        void RefreshDriverTab(List<ZObject> sel, bool crewShown)
        {
            if (driverBtn == null || !driverBtn.Rt) return;
            bool show = false;
            if (crewShown) crewTabAt = Time.unscaledTime;                                                  // fix round 2: EXIT CREW in this place now
            else foreach (var o in sel) if (DriverTabOf(o)) { show = true; break; }
            float since = Mathf.Max(input ? input.SelectTime : -10f, crewTabAt);
            if (show && !DriverTabShown && Time.unscaledTime - since < DriverTabShowAfterSec) { show = false; DriverTabHeldBack++; }   // a double-click's second click lands on the tank (or where EXIT CREW was), not on a tab that just appeared
            driverBtn.Rt.gameObject.SetActiveFast(show);
            if (!show) return;
            int h = sel.Count; foreach (var o in sel) h = h * 31 + (o != null ? o.Id : 0);
            float now = Time.unscaledTime;
            if (h == driverTabHash && now - driverTabAt < DriverTabRefreshSec) return;
            driverTabHash = h; driverTabAt = now;
            int tanks = 0, live = 0; string why = null;
            foreach (var o in sel)
            {
                if (!DriverTabOf(o)) continue;
                tanks++;
                if (o.CanExitDriver(out var w)) live++; else if (why == null) why = w;
            }
            bool on = live > 0;
            if (driverBtn.Interactable != on) { driverBtn.Interactable = on; driverBtn.Refresh(); }
            driverBtn.Tooltip = DriverTip(on, on && live < tanks, why);
        }

        /// <summary>The tooltip (title|text|keycap): live, with the key only while the key lets drivers out; greyed, with CanExitDriver's
        /// reason in plain words.</summary>
        static string DriverTip(bool live, bool someStay, string why)
        {
            if (live)
            {
                string key = KeyExitsDrivers ? OzBindings.Cap(OzAction.ExitCrew) : null;
                return (key != null ? key + " · " : "") + "EXIT DRIVER|The driver gets out. For 30 seconds no robot climbs in by itself: right-click a robot onto it to put in a new driver."
                       + (someStay ? " A selected tank that cannot let its driver out now keeps him." : "") + "|" + (key ?? "");
            }
            switch (why)
            {
                case ZWorld.DriverWhyRoom: return "EXIT DRIVER|No free ground beside the vehicle: drive it to open ground first.|";
                case ZWorld.DriverWhyBridge: return "EXIT DRIVER|Not on a bridge: drive it off the bridge first.|";
                case ZWorld.DriverWhyFactory: return "EXIT DRIVER|Not while it drives out of a factory or a repair bay.|";
                default: return "EXIT DRIVER|The driver cannot get out now.|";
            }
        }
    }
}
