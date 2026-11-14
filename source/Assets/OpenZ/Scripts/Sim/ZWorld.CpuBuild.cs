namespace OpenZ.Sim
{
    /// <summary>
    /// side builds every item in 90 % of the time the same item takes the human player's side.
    /// The Sim does not know the difficulty. View/ZGame.cs (ApplyTechBonus: at the level start and on a live change of the difficulty) names
    /// the side that builds faster through <see cref="SetCpuBuildFaster(bool, Team, bool, bool)"/>: on MAYHEM, in a match of a human against
    /// the computer, the computer's side (blue, or red when the human plays blue); never the human's side, never in a computer-only match.
    /// EASY, MEDIUM, HARD, the headless checks, the parity report and the gate battles name no side: every build time is Zod's, bit for bit.
    /// The rule: ZObject.BuildTimeModified (Zod's T = base x (1 - 0.5 z) x (1 + 1.25 (1 - hp/max)), zbuilding.cpp:633-667) x
    /// <see cref="CpuBuildFasterScale"/> for a building of that side, so every recalculation (a hit, a flag change) keeps it and it always
    /// counts from the item's ORIGINAL start time, as Zod's. The item is done on the first 100 Hz tick at or after BInit + that time
    /// (ProcessBuilding's own compare, the same for both sides: the game's rounding of a build time to ticks). Turning a side on or off
    /// recalculates the items its buildings are building at once; a building that changes hands between a faster and a plain side takes
    /// its new owner's time for its running item at once (ZWorld.Rules.cs SetOwnerZod). Deterministic: double maths, no random draw.
    /// Checks: Editor/OzV14MayhemBuildCount.cs (menu OpenZ/V1.4/Mayhem Build: Count (headless, DV175)); the Deviations row DV175.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV175: the factor on the build time of every item of the faster side (90 % of the player's time for the same item).</summary>
        public const double CpuBuildFasterScale = 0.9;
        readonly bool[] cpuBuildFaster = new bool[3];                                         // Neutral, Red, Blue; all false = Zod
        /// <summary>DV175: buildings recalculated because the faster side changed (SetCpuBuildFaster, a building changing hands). Checks.</summary>
        public int CpuBuildFasterRecalcs { get; private set; }

        /// <summary>DV175: does this side build in <see cref="CpuBuildFasterScale"/> of the time? (never Neutral)</summary>
        public bool CpuBuildFaster(Team t) { int i = (int)t; return i > 0 && i < cpuBuildFaster.Length && cpuBuildFaster[i]; }

        /// <summary>DV175: one side on or off; a change recalculates the running item of every producer of that side (from its start).</summary>
        public void SetCpuBuildFaster(Team t, bool on)
        {
            int i = (int)t;
            if (i <= 0 || i >= cpuBuildFaster.Length || cpuBuildFaster[i] == on) return;
            cpuBuildFaster[i] = on;
            foreach (var b in Objects)
                if (b.Owner == t && b.IsProducer && b.CurUnit != null) { b.RecalcBuildTime(); CpuBuildFasterRecalcs++; }
        }

        /// <summary>DV175, View/ZGame.cs: on = the difficulty asks for it (MAYHEM). Only a match of a human against the computer (exactly one
        /// side the computer's): that side builds faster, the human's never; anything else (on false, both or no side the computer's): none.</summary>
        public void SetCpuBuildFaster(bool on, Team human, bool blueCpu, bool redCpu)
        {
            bool vsCpu = on && blueCpu != redCpu;
            SetCpuBuildFaster(Team.Red, vsCpu && redCpu && human != Team.Red);
            SetCpuBuildFaster(Team.Blue, vsCpu && blueCpu && human != Team.Blue);
        }
    }
}
