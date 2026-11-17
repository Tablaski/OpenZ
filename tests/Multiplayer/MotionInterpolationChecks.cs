using System;
using OpenZ.Multiplayer;

static class MotionInterpolationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var buffer = new DuelMotionBuffer();
        void Push(double simulation, double arrival, float x, int identity = 1)
        {
            if (!buffer.BeginSnapshot(simulation, arrival)) throw new Exception("Expected a new motion snapshot");
            buffer.AddUnit(1, identity, x, 0, 100);
        }
        bool Read(float x, out float displayed) => buffer.TryPosition(1, 1, x, 0, out displayed, out _);

        Push(10, 100, 0);
        buffer.Select(100, .1);
        check(!Read(0, out _), "One snapshot should use the current position while warming up");
        Push(10.09, 100.09, 9);
        buffer.Select(100.145, .1);
        check(Read(9, out float half) && Math.Abs(half - 4.5f) < .001, "11 Hz snapshots should interpolate on host time");
        buffer.Select(100.15, .2);
        check(Read(9, out float held) && Math.Abs(held - half) < .001, "Increasing delay must not rewind motion");
        buffer.Select(100.5, .1);
        check(buffer.Starved && Read(9, out float latest) && latest == 9, "No future snapshot means hold without extrapolating");
        check(!buffer.BeginSnapshot(10.09, 100.51), "Duplicate simulation timestamps must not form a zero-length interval");

        Push(10.18, 100.51, 18);
        buffer.Select(100.52, .1);
        check(Read(18, out latest) && latest <= 18, "Motion should remain bounded after delayed packets");
        Push(10.27, 100.60, 100);
        buffer.Select(100.61, .1);
        check(!Read(100, out _), "A teleport should use the confirmed position directly");

        buffer.Reset();
        Push(1, 1, 0);
        Push(1.1, 1.1, 5, 2);
        buffer.Select(1.15, .1);
        check(!Read(5, out _), "A changed unit identity must skip interpolation");
        check(!buffer.TryPosition(2, 1, 0, 0, out _, out _), "A newly visible unit must use its current position");

        buffer.Reset();
        for (int i = 0; i < 24; i++) Push(i * .05, i * .05, i * 5);
        buffer.Select(1.225, .1);
        check(Read(115, out float wrapped) && Math.Abs(wrapped - 112.5f) < .001, "Snapshot history should wrap without losing recent positions");
        Push(.05, 2, 0);
        buffer.Select(2, .1);
        check(!Read(0, out _), "New simulation time should clear the previous match history");
    }
}
