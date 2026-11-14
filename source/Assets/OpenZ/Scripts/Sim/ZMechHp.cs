using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV147 "the mech's health x 0.95" (1.2 owner item 179: the mech carries a second weapon now, its hand gun, DV146).
    /// The mech is Zod's "medium" type; its row in default_settings.txt keeps Zod's number (health 0.675676 = 6756 HP), and while the
    /// switch is on every parse of the game's settings text gives the mech <see cref="Factor"/> of it, rounded to six decimal places like
    /// the other health values: 0.641892 = 6418 HP (6756 x 0.95 = 6418.2). One place, ZSettings.Parse, so every reader of the game's rules
    /// sees the same number: the simulation (every mech made, built, captured, repaired; the player's and the computer's), the Unit
    /// editor's OpenZ set (its default stats; a Custom value reset to OpenZ's), a Custom level's rows (DV142), the HUD and the cards.
    /// Only the mech: the APC and the missile launcher share Zod's 0.675676 and keep it; the medium tank keeps its own 7432 HP
    /// (Sim/ZMTank.cs, an absolute number of its own). A Custom value the player wrote stays exactly his (it is written after the parse).
    /// Switch: <see cref="ZWorld.MechHpDeviation"/> (View/ZGame.cs: on, off in 'Zod exact'; headless default off, so the parity's Zod rows,
    /// the Unit editor's ZodEngine set and every older counted check read Zod's mech). Deterministic: a fixed factor, no random draw.
    /// </summary>
    public static class ZMechHp
    {
        public const string Type = "medium";
        public const double Factor = 0.95;

        /// <summary>The mech's health field under DV147 from the row's own field (Zod's 0.675676 -> 0.641892).</summary>
        public static double Health(double rowHealth) => Math.Round(rowHealth * Factor, 6);

        /// <summary>ZSettings.Parse, after the rows are read: the mech's row x <see cref="Factor"/> while the switch is on. True when applied.</summary>
        public static bool Apply(ZSettings s)
        {
            if (!ZWorld.MechHpDeviation || s == null) return false;
            var u = s.Unit(Type);
            if (u == null) return false;
            u.health = Health(u.health);
            Applied++;
            return true;
        }
        /// <summary>Statistics (the counted check): parses that gave the mech the DV147 health.</summary>
        public static int Applied;

        /// <summary>A settings text parsed with the switch off whatever a Play session left (Zod's mech: the Unit editor's ZodEngine set).</summary>
        public static ZSettings ParseZod(string text)
        {
            using (ZWorld.MechHpOff()) return ZSettings.Parse(text ?? "");
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV147 the mech's health x 0.95 (Sim/ZMechHp.cs). View/ZGame.cs sets it at every start (off in 'Zod exact'); off by
        /// default so headless worlds, the parity's Zod rows and the tests keep Zod's mech.</summary>
        public static bool MechHpDeviation = false;

        /// <summary>The parity's Zod rows and the counted checks: Zod's mech while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable MechHpOff() => new MechHpHold();
        sealed class MechHpHold : IDisposable
        {
            readonly bool was = MechHpDeviation;
            public MechHpHold() { MechHpDeviation = false; }
            public void Dispose() { MechHpDeviation = was; }
        }
    }
}
