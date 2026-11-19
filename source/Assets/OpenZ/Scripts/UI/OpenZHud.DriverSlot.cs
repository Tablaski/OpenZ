using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 (Discord report 10 Nov;
    /// driver should be clearly listed in the selection box of the tank. we cant
    /// change icons but at least we can say Driver: Pscyco... ensure nothing overlaps and it fits correctly in the selection box where avatar
    /// The rules were right (DriverType is the robot that got in), but the card showed the vehicle's own picture, whose art always has
    /// a grunt at the controls, and named the driver only in the small muted line under the name. The card of a vehicle or gun (the shuttle
    /// keeps its PILOT slot, UI/OpenZHud.ShuttleAboard.cs, which is the model here) now names its driver in the stat slot that read CREW:
    /// "DRIVER" (GUNNER for a gun) over the driver's round face (<see cref="OzRoundFace"/>, the production badge's face; the Electrobot,
    /// who has none, his card portrait cut round) and his name
    /// ("PSYCHO", a hero's own name, "GRUNT ×4" for several), in the slot's own fonts, sizes and colours; a long name shrinks inside the
    /// slot's box (OzText's fit), never past the panel's right edge. An empty one: a muted "NONE" beside the dim robot glyph. The crew count
    /// moves to the line under the name ("CREW · 1 ROBOT"; an APC's passengers as before), so nothing shows twice. The portrait stays the
    /// vehicle's own picture. Faces and words are looked up only when the driver changes: no new string while he stays.
    /// </summary>
    public partial class OpenZHud
    {
        OzRoundFace driverFace; int driverUnitId = -1, driverKey; bool driverCard;
        string driverLabel = "DRIVER", driverValue = "NONE";
        /// <summary>The weapon value's box on a vehicle's / gun's card (118 elsewhere): from UnitWeaponX it ends 6 u before the driver's face
        /// (UnitNadeLabelX); a longer weapon ("MACHINE GUN") shrinks a little inside it instead of touching the face.</summary>
        public const float DriverWeaponW = OzLayout.UnitNadeLabelX - 6f - OzLayout.UnitWeaponX;
        /// <summary>Proof: the slot's words ("DRIVER PSYCHO") and its face ("psycho", "hero:brad", "" when none).</summary>
        public static string DriverSlotText = "", DriverSlotFace = "";

        /// <summary>A vehicle or gun whose card shows the DRIVER / GUNNER slot (not the shuttle: its PILOT slot).</summary>
        static bool DriverCardOf(ZObject u) => u != null && !u.Destroyed && (u.Kind == ZKind.Vehicle || u.Kind == ZKind.Cannon) && !u.IsShuttle;

        /// <summary>RefreshUnitPanel hook, every frame: any other card hides the driver's face (cheap: a flag).</summary>
        void DriverCardReset(ZObject u)
        {
            if (DriverCardOf(u) || !driverCard) return;
            driverCard = false; driverUnitId = -1;
            if (driverFace != null) driverFace.Off();
            uWeapon.At(OzLayout.UnitWeaponX, OzLayout.UnitValueY, OzH.L, false, 118);                    // the weapon's box as every other card has it
            DriverSlotText = DriverSlotFace = "";
        }

        /// <summary>The line under the name of a crewed vehicle or gun: how many robots are aboard (the slot names the driver); an APC's
        /// passengers as before (apc_v1 DV15).</summary>
        static string CrewSub(ZObject u)
        {
            if (u.IsApc && ZWorld.ApcCrewDeviation)
                return u.Passengers.Count > 0 ? $"PASSENGERS {OzHeroSquadView.CrewNameOr(u, true, UnitInfo.Name(u.PassengerType))} ×{u.Passengers.Count}" : "ROOM FOR ONE SQUAD";
            int n = u.CrewAboard;
            return n == 1 ? "CREW · 1 ROBOT" : $"CREW · {n} ROBOTS";
        }

        /// <summary>The stat slot of a vehicle's or gun's card (CREW before): its driver, by face and name.</summary>
        void DriverSlot(ZObject u)
        {
            driverCard = true;
            int n = u.Drivers.Count; var seats = u.HeroDriverSeats;
            int key = ((n * 8 + (int)u.Owner) * 64 + Mathf.RoundToInt(OzPx.K * 8f)) * 31 + (u.DriverType != null ? u.DriverType.GetHashCode() : 0);   // OzPx.K: the face's size tier follows the screen and the HUD scale
            if (seats != null) for (int i = 0; i < n && i < seats.Count; i++) { var s = seats[i]; if (s != null && s.Kind != null) key = key * 31 + s.Kind.GetHashCode() * 7 + s.Member + 1; }
            if (u.Id != driverUnitId || key != driverKey)
            {
                driverUnitId = u.Id; driverKey = key;
                if (driverFace == null) driverFace = new OzRoundFace(uStats, "DriverFace", OzLayout.UnitNadeIconSize);
                driverFace.At(OzLayout.UnitNadeLabelX, OzLayout.UnitWeaponIconY);                         // where the slot's icon sits, its left edge under the label's
                uWeapon.At(OzLayout.UnitWeaponX, OzLayout.UnitValueY, OzH.L, false, DriverWeaponW);      // the jeep's MACHINE GUN ended on the face: its box ends 6 u before it
                driverLabel = u.Kind == ZKind.Cannon ? "GUNNER" : "DRIVER";
                if (n == 0) { driverFace.Off(); driverValue = "NONE"; }
                else
                {
                    ZHeroSeat hs = null;
                    if (seats != null) for (int i = 0; i < n && i < seats.Count && hs == null; i++) if (seats[i] != null && seats[i].Kind != null) hs = seats[i];
                    if (hs != null)
                    {   // DV108: a hero by his own name (ALAN & BRAD when both drive) and his own face (the pair's picture for both)
                        driverValue = OzHeroSquadView.CrewNameOr(u, false, hs.Kind.Name);
                        bool pair = hs.Kind.Members != null && hs.Kind.Members.Length > 1 && driverValue == hs.Kind.Name;
                        driverFace.Hero(hs.Kind, pair ? -1 : hs.Member, u.Owner);
                    }
                    else
                    {
                        driverValue = n > 1 ? OzText.Tab(UnitInfo.Name(u.DriverType) + " ×" + n) : UnitInfo.Name(u.DriverType);
                        driverFace.RobotOrPortrait(u.DriverType, u.Owner);                                    // the Electrobot (no badge face): his card portrait cut round
                    }
                }
                DriverSlotFace = driverFace.Shows; DriverSlotText = driverLabel + " " + driverValue;
            }
            uNadeLabel.Set(driverLabel);
            nadeIcon.enabled = n == 0;
            if (nadeIcon.enabled) { Hud.SetIcon(nadeIcon, "robot", 24); nadeIcon.color = OzUi.Dim; }
            uNades.Set(driverValue); uNades.color = n > 0 ? (Color)OzUi.Ink : (Color)OzUi.Muted;
        }
    }
}
