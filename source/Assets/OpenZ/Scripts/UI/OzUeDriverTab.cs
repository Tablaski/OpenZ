// OpenZ (GPL-3.0) - 1.5 kit ue_exit_button: the EXIT DRIVER tab in the Unit editor.
// 1.5 kit ue_exit_placement: the button moved into the picked-unit card, under REMOVE (no tab over the play bar any more).
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// In a match a picked tank (light, medium, heavy, the mech, the MAMMOTH, the BIZON: every ZObject.IsHatchVehicle) shows the EXIT
    /// DRIVER tab over the unit panel (UI/OpenZHud.VehicleExit.cs, 1.4 DV171); the Unit editor's playground hides that panel on purpose
    /// (OzUeHooks.HideMatchHud), so the editor needs its own button. The E key kept working there (OzUeKeys keeps EXIT CREW's key).
    /// <para>
    /// The tab floated alone over the play bar in the middle of the screen and its HUD tooltip covered the unit dock). The
    /// button is now part of the picked-unit card of the STATS box (UI/OzUeInspector.ExitDriver.cs, UI/OzUeExitColumn.cs): right under
    /// REMOVE, in REMOVE's kit and height, its tooltip the editor's side tip LEFT of the box (as REMOVE's), never over the dock or the play
    /// bar. Nothing is built on the play bar any more.</para>
    /// <para>The rules are still the game's own, mirrored every frame (this component's LateUpdate runs after the HUD's Update, where the
    /// game's hidden tab is refreshed): shown while the game's tab would show (OpenZHud.DriverTabShown: both teams in the playground, never
    /// neutral, a crewed hatch vehicle, held back 0.4 s after the picking click and while EXIT CREW has the place) AND the card shows one of
    /// those picked tanks (<see cref="CardWants"/>); live / greyed as the game's (DriverTabLive); its tooltip the game's (DriverTabTip,
    /// title and text; the keycap part dropped: the title already names the key); a click does the game's click (OpenZHud.ExitDrivers,
    /// else the refusal blip ui_order_invalid); the HUD plays its click sound after it and ui_disabled for a greyed click.</para>
    /// Proof counters: Builds, Shows, Clicks, Refused. Log lines start "[OpenZ UnitEditor] EXIT DRIVER".
    /// </summary>
    public sealed class OzUeDriverTab : MonoBehaviour
    {
        public const string Label = "EXIT DRIVER";

        public static OzUeDriverTab I { get; private set; }
        /// <summary>Proof: buttons built (one per card build), times it appeared, clicks on it, clicks that let nobody out.</summary>
        public static int Builds, Shows, Clicks, Refused;

        /// <summary>The card's button (null until the STATS box is built): checks click its rect.</summary>
        public HudButton Button { get { var ins = Inspector; return ins != null ? ins.ExitButton : null; } }

        /// <summary>The STATS box's inspector now (a new one after every rebuild of the box).</summary>
        internal static OzUeInspector Inspector { get { var r = OzUeStatsRoot.I; return r != null && r.Shell != null ? r.Shell.Inspector : null; } }

        // ------------------------------------------------------------------------------------------------ install: the playground only
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryInstall(); }
            TryInstall();
        }

        static void TryInstall()
        {
            var ed = OzUnitEditor.I;                                                                     // set in its Awake (only with a world: else the editor stays off)
            if (!ed || !OzUnitEditor.IsPlayground(ed.gameObject.scene) || ed.GetComponent<OzUeDriverTab>()) return;
            ed.gameObject.AddComponent<OzUeDriverTab>();
        }

        void Awake() { I = this; Shows = Clicks = Refused = 0; }

        void OnDestroy() { if (I == this) I = null; }

        // ------------------------------------------------------------------------------------------------ every frame, after the HUD's Update
        void LateUpdate()
        {
            var hud = OpenZHud.I;
            if (!hud || !hud.Root || !OzUnitEditor.UiBuilt) return;
            var ins = Inspector;
            if (ins != null) ins.TickExit(hud);
        }

        /// <summary>The card shows the button: the game's tab shows now, and the card shows a picked unit (not a type, a flag or empty
        /// hardware) that is one of the tanks the game's tab is for. A grunt's card in a mixed pick shows none (E still works).</summary>
        public static bool CardWants(OpenZHud hud)
        {
            if (!hud || !hud.DriverTabShown || OzUePick.Kind != UeInspect.Unit) return false;
            var o = OzUePick.Inspected;
            if (o == null || !hud.DriverTabOf(o)) return false;
            foreach (var p in OzUePick.Picked) if (p == o) return true;
            return false;
        }

        /// <summary>The game's tab tooltip (title|text|keycap) as the editor's side tip (title|text).</summary>
        public static string SideTip(string game)
        {
            if (string.IsNullOrEmpty(game)) return "";
            var parts = game.Split('|');
            return parts.Length >= 2 ? parts[0] + "|" + parts[1] : parts[0];
        }

        /// <summary>The game tab's click (BuildDriverTab: if (!ExitDrivers()) ui_order_invalid); the HUD plays its click sound after it.</summary>
        internal static void Click()
        {
            Clicks++;
            var hud = OpenZHud.I;
            bool any = hud && hud.ExitDrivers();
            if (!any) { Refused++; AudioDirector.UI("ui_order_invalid"); }
            Debug.Log("[OpenZ UnitEditor] EXIT DRIVER clicked in the picked-unit card: " + (any ? "the driver gets out" : "nobody could get out (the refusal blip)"));
        }
    }
}
