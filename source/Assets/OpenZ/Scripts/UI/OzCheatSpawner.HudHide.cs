using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// While the overlay is open the rest of the
    /// interface is hidden exactly as the hide key does it (OpenZHud.SetVisible(false): the HUD canvas, the unit overlay, the tooltip, an open
    /// production window), so a click reaches only the overlay; when it closes the interface comes back exactly as it was (hidden before with
    /// the hide key: it stays hidden).
    /// <list type="bullet">
    /// <item>The overlay's own panels (every "Cheat..." node under the HUD root: both team columns, the empty row, the dragged icon, the fog
    /// panel) get a nested canvas of their own, so they stay drawn while the HUD canvas is off.</item>
    /// <item>OpenZHud.IsOverUi answers for these panels while the HUD is hidden (<see cref="Over"/>), so a press on them never reaches the map
    /// (PlayerInput) and no hidden HUD button or the hidden minimap takes it (OpenZHud's hover pass and OpenZMinimap read hudVisible).</item>
    /// <item>A menu (pause, end screen, a question) or the first-person view closes the overlay as before; the interface it hid comes back
    /// once that is gone (the pause menu leaves an interface it did not hide as it was). The hide key while the overlay is open shows the
    /// interface and closes the overlay.</item>
    /// </list>
    /// <see cref="HidesHud"/> off = as before (the overlay over the HUD; it closes when the HUD is hidden).
    /// </summary>
    public partial class OzCheatSpawner
    {
        /// <summary>N16: the rest of the interface hidden while the overlay is open (on).</summary>
        public static bool HidesHud = true;
        /// <summary>N16 proof counters: the interface hidden by the overlay, given back by it.</summary>
        public static int HudHides, HudRestores;
        bool hidHud, hudWas, hudRestorePending;
        const string CheatPrefix = "Cheat", GhostName = "CheatGhost";

        /// <summary>Show: open = the overlay's panels on their own canvases, the interface hidden (if shown); closed = the interface back as it was
        /// (now, or once a menu / the first-person view that closed the overlay is gone).</summary>
        void HudForPanel(bool on)
        {
            if (!HidesHud || !hud) return;
            if (on)
            {
                OwnCanvases();
                hudWas = hud.hudVisible; hudRestorePending = false;
                if (hud.hudVisible) { hud.SetVisible(false); hidHud = true; HudHides++; }
                return;
            }
            if (!hidHud) return;
            hidHud = false;
            if (hud.hudVisible || !hudWas) return;                                                       // shown again meanwhile (the hide key), or hidden before the overlay opened: left as it is
            if (hud.Modal || OzFpsView.On) { hudRestorePending = true; return; }                        // a menu / the first-person view took over: back when it is gone
            hud.SetVisible(true); HudRestores++;
        }

        /// <summary>Update, every frame: the interface the overlay hid comes back once the menu / the first-person view that closed it is gone.</summary>
        void HudRestoreTick()
        {
            if (!hudRestorePending || !hud) return;
            if (hud.Modal || OzFpsView.On) return;
            hudRestorePending = false;
            if (!hud.hudVisible) { hud.SetVisible(true); HudRestores++; }
        }

        /// <summary>Update while open: true = the overlay closes now. N16 on: a menu, the first-person view, or the interface shown again (the
        /// hide key); off: as before (the interface hidden, or a menu).</summary>
        bool HudCloses() => HidesHud ? (hud.Modal || OzFpsView.On || hud.hudVisible) : (!hud.hudVisible || hud.Modal);

        /// <summary>Every "Cheat..." panel under the HUD root on a nested canvas of its own (drawn while the HUD canvas is off).</summary>
        void OwnCanvases()
        {
            if (!HidesHud || !hud || !hud.Root) return;
            var root = hud.Root;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!c.name.StartsWith(CheatPrefix)) continue;
                var cv = c.GetComponent<Canvas>();
                if (!cv) cv = c.gameObject.AddComponent<Canvas>();
                if (!cv.overrideSorting) cv.overrideSorting = true;
                int order = hud.sortingOrder + (c.name == GhostName ? 31 : 30);                         // the dragged icon over the panels
                if (cv.sortingOrder != order) cv.sortingOrder = order;
            }
        }

        /// <summary>OpenZHud.IsOverUi while the interface is hidden: the pointer is over one of the open overlay's panels.</summary>
        public static bool Over(Vector2 screen)
        {
            var s = I; if (!HidesHud || !s || !s.shown || !s.hud || !s.hud.Root) return false;
            var root = s.hud.Root;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i) as RectTransform;
                if (!c || !c.name.StartsWith(CheatPrefix) || c.name == GhostName) continue;
                if (OpenZHud.Contains(c, screen)) return true;
            }
            return false;
        }
    }
}
