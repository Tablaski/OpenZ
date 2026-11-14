using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Round 3: "this is logically correct but visually poor. why you duplicate on left
    /// side who is abord twice and one time with black backround ... improve quality !"). The unit panel of a selected shuttle, view only;
    /// who is aboard shows ONCE:
    ///  * the type chip at the top right (every other unit: its type's icon and how many of that type are selected) shows who is aboard and
    ///    gets out on EXIT (<see cref="OzShuttleHud.Who"/>: the passengers, a hero pilot): their round face (the production badge's own face
    ///    art, <see cref="OzRoundFace"/>; Brad and Alan both, the second overlapping the first) and the count in the chip's own "×3", in the
    ///    chip's own well and size (no box of its own); muted EMPTY when nobody is aboard;
    ///  * the stat slot that reads CREW for the other vehicles reads PILOT: the pilot's round face and what he does: "STAYS IN" (he never
    ///    gets out), "CAN EXIT" for a hero pilot (the label names him: "PILOT · BRAD"), a muted "NONE" for a ship without one;
    ///  * the WEAPON slot: "NONE" (the shuttle carries none, DV118) from the column's left edge, without a weapon glyph;
    ///  * the line under the name: who is aboard and what the ship does, the longest of <see cref="OzShuttleHud.Lines"/> that fits the
    ///    panel without shrinking under <see cref="ShuttleLineMaxW"/> ("3 GRUNTS ABOARD · LANDED · EXIT READY"); the pilot is the slot's.
    /// The portrait keeps the ship's own picture (no strip of faces on it: that would be a third copy). Faces and words are looked up only
    /// when who is aboard (or the pilot) changes: no new object or string while they stay the same.
    /// </summary>
    public partial class OpenZHud
    {
        /// <summary>The widest shuttle line (at the sub line's own size) the panel takes: the fit shrinks it to no less than 78 %.</summary>
        public static float ShuttleLineMaxW = 360f;
        /// <summary>
        /// type chip's well, so for a shuttle the line sits this much lower (px), clear of the well's bottom; other units keep theirs.</summary>
        public static float ShuttleSubDrop = 4f;
        bool subDropped;
        /// <summary>The type chip's faces (one centred where the type's icon sits; two overlapping, the count right of them) and the
        /// PILOT slot's face (where the slot's icon sits).</summary>
        public const float AboardChipFace = 30f, AboardChipStep = 15f, PilotFace = 26f;
        string shuttleLine0, shuttleMore, shuttlePicked;
        readonly OzRoundFace[] chipFaces = new OzRoundFace[2]; OzRoundFace pilotFace;
        int chipShipId = -1, pilotShipId = -1, pilotKey; OzShuttleHud.Aboard chipShown; bool shuttleCard;
        string pilotLabel = "PILOT", pilotValue = "STAYS IN";
        static readonly string[] chipTab = new string[16];
        /// <summary>Proof: which of OzShuttleHud.Lines the panel shows (0 = the longest), the type chip's faces and count text, the PILOT
        /// slot's face and words.</summary>
        public static int ShuttleLineShown = -1, AboardChipFacesShown;
        public static string AboardChipText = "", AboardChipShows = "", PilotShows = "", PilotText = "";

        /// <summary>RefreshUnitPanel hook, every frame: any other unit gets the type chip and the CREW slot as before (cheap: a flag).</summary>
        void ShuttleCardReset(ZObject u)
        {
            if (u != null && u.IsShuttle && !u.Destroyed) return;
            if (!shuttleCard) return;
            shuttleCard = false; chipShipId = pilotShipId = -1;
            if (subDropped) { uSub.At(OzLayout.UnitNameX, OzLayout.UnitSubY, OzH.L, false, 280); subDropped = false; }   // the line back where every unit has it
            for (int i = 0; i < chipFaces.Length; i++) if (chipFaces[i] != null) chipFaces[i].Off();
            if (pilotFace != null) pilotFace.Off();
            uCount.color = OzUi.Ink; uNades.color = OzUi.Ink;
            uWeapon.At(OzLayout.UnitWeaponX, OzLayout.UnitValueY, OzH.L, false, 118);                       // the weapon's value back right of its glyph
            AboardChipFacesShown = 0; AboardChipText = AboardChipShows = PilotShows = PilotText = "";
        }

        /// <summary>RefreshUnitPanel: the shuttle's line under its name (more = the " · +N MORE SELECTED" that follows it): the longest
        /// of OzShuttleHud.Lines that fits; measured only when the line or that tail changed.</summary>
        string ShuttleSub(ZObject u, string more)
        {
            var ls = OzShuttleHud.Lines(u);
            if (ReferenceEquals(ls[0], shuttleLine0) && more == shuttleMore) return shuttlePicked;
            shuttleLine0 = ls[0]; shuttleMore = more;
            ShuttleLineShown = ls.Length - 1; shuttlePicked = ls[ls.Length - 1];
            for (int i = 0; i < ls.Length; i++)
                if (uSub.WidthAt(more.Length > 0 ? ls[i] + more : ls[i]) <= ShuttleLineMaxW) { ShuttleLineShown = i; shuttlePicked = ls[i]; break; }
            return shuttlePicked;
        }

        /// <summary>RefreshUnitPanel, for a shuttle in place of the type's icon and count: who is aboard, once.</summary>
        void ShuttleTypeChip(ZObject u)
        {
            shuttleCard = true;
            if (!subDropped) { uSub.At(OzLayout.UnitNameX, OzLayout.UnitSubY + ShuttleSubDrop, OzH.L, false, 280); subDropped = true; }   // round 3b: clear of the well
            var who = OzShuttleHud.Who(u); int n = who.Count;
            if (u.Id == chipShipId && who.Same(chipShown)) return;
            chipShipId = u.Id; chipShown = who;
            var tw = OzLayout.UnitTypeWell;
            if (chipFaces[0] == null)
            {   // the second first, so the first (the leader) lies over it where they overlap
                chipFaces[1] = new OzRoundFace(unitPanel, "TypeFace1", AboardChipFace);
                chipFaces[0] = new OzRoundFace(unitPanel, "TypeFace0", AboardChipFace);
            }
            int f = 0;
            if (who.Hero != null && who.Hero.Members != null)                                             // the heroes first (Brad, Alan), then the passengers' type
                for (int i = 0; i < who.Hero.Members.Length && f < chipFaces.Length; i++)
                    if ((who.HeroMask & (1 << i)) != 0) chipFaces[f++].Hero(who.Hero, i, who.Team);
            if (who.Plain > 0 && f < chipFaces.Length) chipFaces[f++].Robot(who.PlainType, who.Team);
            for (int i = f; i < chipFaces.Length; i++) chipFaces[i].Off();
            float y = tw.y + (tw.h - AboardChipFace) * 0.5f;
            if (f == 1) chipFaces[0].At(tw.x + 9f, y);                                                        // where the type's icon sits
            else if (f == 2) { chipFaces[0].At(tw.x + 2f, y); chipFaces[1].At(tw.x + 2f + AboardChipStep, y); }
            AboardChipFacesShown = f;
            AboardChipShows = f == 0 ? "" : f == 1 ? chipFaces[0].Shows : chipFaces[0].Shows + "+" + chipFaces[1].Shows;
            uCount.color = n > 0 ? (Color)OzUi.Ink : (Color)OzUi.Muted;
            uCount.Set(n <= 0 ? OzShuttleHud.EmptyWord : n < chipTab.Length ? (chipTab[n] ??= OzText.Tab("×" + n)) : OzText.Tab("×" + n));
            AboardChipText = uCount.text;
        }

        /// <summary>The stat slot of the panel for a shuttle (CREW for the other vehicles): its PILOT, never a passenger.</summary>
        void ShuttlePilotSlot(ZObject u)
        {
            shuttleCard = true;
            bool hero = u.ShuttleHeroPilot;
            int key = (u.Drivers.Count > 0 ? 1 : 0) | (hero ? 2 : 0) | ((int)u.Owner << 2) | ((u.DriverType != null ? u.DriverType.GetHashCode() : 0) << 5);
            if (u.Id != pilotShipId || key != pilotKey)
            {
                pilotShipId = u.Id; pilotKey = key;
                if (pilotFace == null) pilotFace = new OzRoundFace(uStats, "PilotFace", PilotFace);
                pilotFace.At(OzLayout.UnitNadeIconX - 1f, OzLayout.UnitWeaponIconY - 1f);
                if (u.Drivers.Count == 0) { pilotFace.Off(); pilotLabel = "PILOT"; pilotValue = "NONE"; }
                else if (hero)
                {
                    var seat = u.HeroDriverSeats[0];
                    pilotFace.Hero(seat.Kind, seat.Member, u.Owner);
                    pilotLabel = "PILOT · " + OzHeroSquadView.CrewNameOr(u, false, "HERO"); pilotValue = "CAN EXIT";   // K256f2: a hero pilot gets out on EXIT
                }
                else { pilotFace.Robot(u.DriverType, u.Owner); pilotLabel = "PILOT"; pilotValue = "STAYS IN"; }   // DV118: he never gets out
                PilotShows = pilotFace.Shows; PilotText = pilotLabel + " " + pilotValue;
                uWeapon.At(OzLayout.UnitNameX, OzLayout.UnitValueY, OzH.L, false, 148);                     // no weapon, no glyph: "NONE" from the column's left edge
            }
            uNadeLabel.Set(pilotLabel);
            nadeIcon.enabled = u.Drivers.Count == 0;
            if (nadeIcon.enabled) { Hud.SetIcon(nadeIcon, "robot", 24); nadeIcon.color = OzUi.Dim; }
            uNades.Set(pilotValue); uNades.color = u.Drivers.Count > 0 ? (Color)OzUi.Ink : (Color)OzUi.Muted;
        }
    }
}
