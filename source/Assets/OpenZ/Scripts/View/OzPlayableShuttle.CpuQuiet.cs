using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// DV138 (the computer flies its shuttles, Sim/ZBot.CpuShuttle.cs), the view's side: a ship of the other side never talks to the player.
    /// The ship's crew lines ("All aboard!", "Everybody out!", the landing lines, "Can't land there!") and Rachel's landing line are the
    /// player's own crew and announcer speaking to the commander; the player's error beep belongs to the player's own orders. For a ship
    /// that is neither the player's nor empty, while the rule is on, they are left out (the line is still noted in LastVoice for the proofs).
    /// Every sound of the ship itself (engines, take-off, ramp, landing, strafe, crash) plays as for the player's ship: every action keeps its
    /// sound.
    /// </summary>
    public sealed partial class OzPlayableShuttle
    {
        /// <summary>Crew lines, Rachel's landing line and the error beep left out for this ship (another side's, DV138 on).</summary>
        public int CpuQuietLines { get; private set; }

        /// <summary>This ship belongs to another side than the player's (not the null team) and the computer may fly it: it stays quiet.</summary>
        bool CpuShipQuiet
        {
            get
            {
                if (!ZWorld.CpuShuttleDeviation || game == null || Obj == null || Obj.Owner == Team.Neutral || Obj.Owner == game.player) return false;
                CpuQuietLines++;
                return true;
            }
        }
    }
}
