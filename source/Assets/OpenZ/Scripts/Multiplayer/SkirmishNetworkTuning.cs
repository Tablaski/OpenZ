using System;

namespace OpenZ.Multiplayer
{
    internal sealed class SkirmishNetworkTuning
    {
        internal const int InitialHz = 15, MinHz = 6, MaxHz = 20;
        // Target aggregate paid egress for state, leaving room for control and protocol overhead.
        internal const int SfuStateTarget = 96 * 1024;
        internal int Hz { get; private set; } = InitialHz;
        int clearWindows;

        internal void Update(int queued, double jitterMs, double rttGrowthMs, double simulationMs,
            double stateBytesPerSecond, int receivers, bool sfu)
        {
            double egress = stateBytesPerSecond * receivers;
            bool overTarget = sfu && egress > SfuStateTarget;
            bool congested = queued > 48 * 1024 || jitterMs > 30 || rttGrowthMs > 35 || simulationMs > 15 || overTarget;
            if (congested)
            {
                int byEgress = overTarget ? (int)Math.Floor(Hz * SfuStateTarget / egress) : Hz - 2;
                Hz = Math.Max(MinHz, Math.Min(Hz - 2, byEgress));
                clearWindows = 0;
            }
            else if (queued < 16 * 1024 && jitterMs < 15 && rttGrowthMs < 15 && simulationMs < 10 &&
                (!sfu || egress < SfuStateTarget * .7))
            {
                if (++clearWindows >= 4) { Hz = Math.Min(MaxHz, Hz + 1); clearWindows = 0; }
            }
            else clearWindows = 0;
        }
    }
}
