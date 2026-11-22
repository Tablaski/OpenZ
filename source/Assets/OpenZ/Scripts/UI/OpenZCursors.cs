using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// Z's cursor set, redrawn by the UI kit (guide 9.10, Cursors/cur_&lt;name&gt;_&lt;32|48|64&gt;_&lt;0..3&gt;): hardware cursors,
    /// 32 px below 1300 px screen height, 48 px up to 2000 (2560 x 1600), 64 px above; 4 frames x 0.2 s like Zod
    /// (cursor.cpp:204-216). The cursor follows Zod's decision tree (zplayer.cpp:2207-2294) from PlayerInput's own hover
    /// object and the current selection: arrow, move (Zod PLACE, the pulsing star), attack, grab (flags), pickup (grenades: Z's hand), enter, exit, repair, nono,
    /// cannon, place (cannon placement). OrderMark = the "-ED" version of the cursor shown when the last order was given
    /// (PLACED, ATTACKED, GRABBED ...), drawn by OpenZUnitOverlay at the order point. Never changes what an order does.
    /// PlayerPrefs oz_kit_cursor = 0 keeps the OS cursor. ui_quality_v1: drawn by OzSoftCursor (crisp, 256 px sources, sized to the
    /// screen and the UI SCALE); oz_soft_cursor = 0 = the hardware set as before.
    /// </summary>
    [DefaultExecutionOrder(65)]                                                     // before OpenZUnitOverlay (70) reads OrderMark
    public class OpenZCursors : MonoBehaviour
    {
        public string Current { get; private set; } = "arrow";
        public string OrderMark { get; private set; } = "placed";
        /// <summary>1.2 item 17: the cursor over a grenade crate a selected robot can pick up = Z's grab hand (Kit/Cursors/cur_pickup_*,
        /// HD/cur_pickup_256_*; order mark Kit/Cursors/mark_pickedup), no longer the flag-capture cursor "grab" (the flag between brackets,
        /// kept for flags).</summary>
        public const string GrenadeCursor = "pickup";
        OpenZHud hud; float lastMarker = -10f; string previous = "arrow"; bool set, hidLocalGhostPointer;

        public void Build(OpenZHud h) { hud = h; lastMarker = h.input ? h.input.MarkerTime : -10f; }

        void LateUpdate()
        {
            if (hud == null || hud.input == null || hud.W == null) return;
            var mouse = Mouse.current;
            bool hide = hud.IsGhostViewer && mouse != null && Multiplayer.DuelRuntime.I.Menus &&
                Multiplayer.DuelRuntime.I.Menus.HideGhostLocalPointer(mouse.position.ReadValue());
            if (hide && Cursor.visible) { Cursor.visible = false; hidLocalGhostPointer = true; }
            else if (!hide && hidLocalGhostPointer) { Cursor.visible = true; hidLocalGhostPointer = false; }
            string c = OzShuttleOrderAim.Cursor(Decide(), hud);                                   // K256g SHUTTLE ORDERS: NO where only shuttles would take the move and none can ever land there (View/OzShuttleOrderAim.cs)
            if (!Mathf.Approximately(hud.input.MarkerTime, lastMarker)) { lastMarker = hud.input.MarkerTime; OrderMark = Ed(previous); }   // the cursor at order time
            OzSoundV3.CursorChanged(SoundName(previous), SoundName(c), hud.input.Hover);              // sound_v3: lock / nono / world hover (1.2 item 17: the grenade hand sounds as "grab" did)
            previous = c; Current = c;
            if (!OzKit.Available || PlayerPrefs.GetInt("oz_kit_cursor", 1) == 0) { if (set) { OzKit.ResetCursor(); set = false; } return; }
            OzKit.TickCursor(c); set = true;
        }

        void OnDisable()
        {
            if (hidLocalGhostPointer) { Cursor.visible = true; hidLocalGhostPointer = false; }
            if (set) { OzKit.ResetCursor(); set = false; }
        }

        /// <summary>1.2 item 17: OzSoundV3.CursorChanged takes the grenade hand as "grab", so its lock blip is the one it always had.</summary>
        static string SoundName(string c) => c == GrenadeCursor ? "grab" : c;

        static string Ed(string c)
        {
            switch (c)
            {
                case "attack": return "attacked";
                case "grab": return "grabbed";
                case GrenadeCursor: return "pickedup";                                          // 1.2 item 17: Kit/Cursors/mark_pickedup, the grey hand
                case "repair": return "repaired";
                case "enter": return "entered";
                case "exit": return "exited";
                case "cannon": return "cannoned";
            }
            return "placed";
        }

        string Decide()
        {
            var input = hud.input; var W = hud.W; var me = OzUeHooks.CursorTeam(hud); var m = Mouse.current;   // 1.2 item 6: in the playground the team of the first picked unit (both teams pick)
            if (hud.Modal || m == null) return "arrow";
            if ((W.GameOver && ZWorld.EndFreezeDeviation) || PlayerInput.MatchDecided(W)) return "arrow";   // endgame_ui_v1: no orders after the win; 1.2 item 68: from the fort's fall on (View/PlayerInput.MatchEnd.cs)
            var sel = input.Selection;
            if (hud.PointerOverUi) return OzRallyMinimap.MoveCursor(hud.Minimap && hud.Minimap.PointerOverMap, sel.Count, hud.Rally && hud.Rally.Armed) ? "move" : "arrow";   // 1.4 rally_minimap: the armed rally tool over the minimap: the map's armed cursor
            if (input.Placing != null) return input.PlaceValid ? "place" : "nono";
            if (hud.Rally && hud.Rally.Armed) return "move";
            if (m.leftButton.isPressed || sel.Count == 0) return "arrow";

            bool canMove = false, robots = false, takers = false, crane = false, repairable = false, canAttack = false, explosives = false, pickup = false;
            foreach (var u in sel)
            {
                if (u.Destroyed || u.Owner != me) continue;
                if (u.CanMove) canMove = true;
                if (u.Kind == ZKind.Robot) { robots = true; if (!u.IsEvilZod) takers = true; if (u.CanPickupGrenades()) pickup = true; }   // DV161: he alone takes no empty vehicle or gun, no own APC (Sim/ZEvilZod.cs)
                if (u.Type == "crane") crane = true;
                if (u.CanBeRepaired()) repairable = true;
                if (u.CanAttack()) canAttack = true;
                if (u.HasExplosives()) explosives = true;
            }
            Vector2 mp = m.position.ReadValue();
            // Zod: EXIT on the single selected ejectable unit itself
            if (sel.Count == 1 && OzShuttleAirOffers.Exit(sel[0]) && input.ScreenRect(sel[0]).Contains(mp)) return "exit";   // apc_v1 DV15: APC = passengers aboard; K430b: never on a shuttle up in the air (UI/OzShuttleAirOffers.cs)
            var h = input.Hover;
            if (h != null)
            {
                if (crane && h.CanBeRepairedByCrane(me)) return "repair";
                if (repairable && h.CanRepairUnit(me)) return "repair";
                if (h.Destroyed) return canMove ? "move" : "cannon";                           // endgame_ui_v1: rubble is a plain move (PlayerInput.BuildWaypoint), never the red attack
                if (h.Owner != me && h.World?.AreAllied(me, h.Owner) != true)
                {
                    bool door = h.CanEnterFort(me) && (OnDoor(h, mp) || (canMove && input.FortEnterAt(h)));   // 1.4: the gate zone and the fort's body too (View/PlayerInput.FortGate.cs)
                    bool ctrlHeld = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
                    if (h.Kind == ZKind.Building && h.Owner == Team.Neutral && !ctrlHeld && !door) return canMove ? "move" : "cannon";
                    { string emptyCursor = ZWorld.EmptyClickCursor(h, ctrlHeld, takers, canMove, canAttack, explosives); if (emptyCursor != null) return emptyCursor; }   // DV81: over an empty vehicle or gun never ATTACK without Ctrl (ENTER with a robot selected, else MOVE); Ctrl = ATTACK, as for neutral buildings
                    if (!door && ZWorld.PyroBurnCursor(h, sel, me, ctrlHeld)) return "attack";   // DV113: Ctrl over a building or bridge with a Pyro selected = ATTACK (it burns it), where explosives-only showed NO (Sim/ZObject.PyroForceBurn.cs)
                    if (canMove)
                    {
                        if (h.Type == "grenades" && pickup) return GrenadeCursor;                       // 1.2 item 17: Z's hand-grab cursor to pick up grenades (was the flag-capture cursor)
                        if (h.Type == "flag") return "grab";
                        if (h.Owner == Team.Neutral && (h.Kind == ZKind.Vehicle || h.Kind == ZKind.Cannon) && takers) return "enter";
                        if (door) return "enter";                                                       // 1.4: the order there is ENTER_FORT (Zod's door showed the move star)
                        if (!canAttack || (!explosives && h.AttackedByExplosives)) return "nono";
                        return "attack";
                    }
                    if (h.Type == "flag") return "cannon";                                              // cannons only
                    if (door) return "move";
                    if (!canAttack || (!explosives && h.AttackedByExplosives)) return "nono";
                    return "attack";
                }
                if (robots && (takers || h.IsShuttle) && h.CanTakePassengers(me)) return "enter";                                 // apc_v1 DV15: board an own APC as passengers
                return "move";
            }
            return canMove ? "move" : "cannon";
        }

        bool OnDoor(ZObject fort, Vector2 mp)
        {
            var cam = hud.rts ? hud.rts.GetComponent<Camera>() : Camera.main;
            if (!cam || !hud.game.GroundPoint(cam.ScreenPointToRay(mp), out var g)) return false;
            var px = ZGame.WorldToPx(g);
            return PlayerInput.OnFortDoor(fort, px.x, px.y);
        }
    }
}
