using System;
using System.Collections.Generic;

namespace OpenZ.Multiplayer
{
    /// <summary>Bounded history of confirmed presentation positions on the host simulation clock.</summary>
    internal sealed class DuelMotionBuffer
    {
        const int Capacity = 16;
        sealed class Frame
        {
            public readonly Dictionary<int, Point> Units = new Dictionary<int, Point>();
            public double Time, Arrival;
        }
        readonly struct Point
        {
            public readonly int Identity;
            public readonly float X, Y, Speed;
            public Point(int identity, float x, float y, float speed)
            { Identity = identity; X = x; Y = y; Speed = speed; }
        }
        readonly Frame[] frames = new Frame[Capacity];
        int next, count;
        Frame older, newer;
        double playback = double.NegativeInfinity, fraction;
        public bool Starved { get; private set; }

        Frame At(int age) => frames[(next + Capacity - 1 - age) % Capacity];
        public void Reset()
        {
            count = next = 0;
            older = newer = null;
            playback = double.NegativeInfinity;
            Starved = false;
        }
        public bool BeginSnapshot(double time, double arrival)
        {
            if (count > 0)
            {
                if (time == At(0).Time) return false;
                if (time < At(0).Time) Reset();
            }
            var frame = frames[next] ?? (frames[next] = new Frame());
            frame.Units.Clear();
            frame.Time = time;
            frame.Arrival = arrival;
            next = (next + 1) % Capacity;
            count = Math.Min(count + 1, Capacity);
            return true;
        }
        public void AddUnit(int id, int identity, float x, float y, float speed)
            => At(0).Units.Add(id, new Point(identity, x, y, speed));

        public void Select(double localTime, double delay)
        {
            older = newer = null;
            Starved = false;
            if (count < 2) return;

            // The largest offset corresponds to the least-delayed sample in this history.
            double offset = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
                offset = Math.Max(offset, At(i).Time - At(i).Arrival);

            var latest = At(0);
            double target = Math.Max(playback, localTime + offset - delay);
            Starved = target > latest.Time;
            playback = Math.Min(target, latest.Time);
            for (int i = 0; i < count - 1; i++)
            {
                newer = At(i);
                older = At(i + 1);
                if (playback >= older.Time) break;
            }
            fraction = Math.Max(0, (playback - older.Time) / (newer.Time - older.Time));
        }

        public bool TryPosition(int id, int identity, float currentX, float currentY, out float x, out float y)
        {
            x = y = 0;
            if (older == null || !older.Units.TryGetValue(id, out var a) ||
                !newer.Units.TryGetValue(id, out var b) || !At(0).Units.TryGetValue(id, out var latest) ||
                a.Identity != identity || b.Identity != identity || latest.Identity != identity)
                return false;

            // Skip discontinuities, including a jump received ahead of the playback cursor.
            double span = newer.Time - older.Time;
            if (span > .3 || IsJump(a.X, a.Y, b.X, b.Y, Math.Max(a.Speed, b.Speed), span) ||
                IsJump(b.X, b.Y, currentX, currentY, Math.Max(b.Speed, latest.Speed), At(0).Time - newer.Time))
                return false;

            x = a.X + (b.X - a.X) * (float)fraction;
            y = a.Y + (b.Y - a.Y) * (float)fraction;
            return true;
        }
        static bool IsJump(float x0, float y0, float x1, float y1, float speed, double duration)
        {
            double limit = Math.Min(48, Math.Max(6, speed * duration * 1.75 + 2));
            double x = x1 - x0, y = y1 - y0;
            return x * x + y * y > limit * limit;
        }
    }
}
