using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// PlayerInput for BOTH teams while the Unit editor's playground runs. The game's input is one team (every test compares with
    /// game.player); the kit's one-line hooks in View/PlayerInput.cs ask these helpers instead, and each gives the game's own answer
    /// while no playground runs (BothSides false):
    ///  * Mine(team): a unit of that team may stay picked (both teams in the playground, never the neutral side);
    ///  * SideOf(unit): the team an order is sent as = the unit's own owner (the Sim refuses a foreign order: SendWaypoints, SendStop,
    ///    SendPatrol and Eject check team == owner; nothing in the Sim changes). A mixed red + blue pick works per unit: right-click a
    ///    blue unit = the red units attack it, the blue ones move to it (the game's own-unit row);
    ///  * Pickable(o): the box and the click pick robots, vehicles and guns of either team;
    ///  * UePick: the editor's own picks (a right click with nothing picked, owner 6g) look and sound exactly like a click's (the
    ///    selection time the sound and the range rings follow).
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>The playground runs: picking and orders for both teams.</summary>
        static bool BothSides => OzUeHooks.Active;

        bool Mine(Team t) => BothSides ? t != Team.Neutral : t == game.player;

        Team SideOf(ZObject u) => BothSides && u != null ? u.Owner : game.player;

        bool Pickable(ZObject o) => BothSides ? o.Owner != Team.Neutral && OwnPickable(o, o.Owner) : OwnPickable(o, game.player);

        /// <summary>The Unit editor picks units of either team as a click does: their heads (a member = its squad's leader), the selection
        /// time set (AudioDirector plays the selection cue and voice, AttackRangeRings shows the rings). add = keep the current pick.</summary>
        public void UePick(IEnumerable<ZObject> units, bool add)
        {
            if (game == null || game.World == null) return;
            if (!add) Selection.Clear();
            if (units != null)
                foreach (var u in units)
                {
                    var h = PickHead(u);
                    if (h != null && Pickable(h) && !Selection.Contains(h)) Selection.Add(h);
                }
            if (Selection.Count > 0) SelectTime = Time.unscaledTime;
        }
    }
}
