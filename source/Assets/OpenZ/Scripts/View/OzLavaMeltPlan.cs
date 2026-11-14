// OpenZ (GPL-3.0) - version 1.3, level 7, kit LV5: the time line of one melting piece. Plain sums, no Unity types, so it can be counted alone (LV5/tools/plan_count.py).
namespace OpenZ
{
    /// <summary>What happens to a piece over its melt, k = 0 at the touch .. 1 when its top is under. Heat and the hot band come first, then it goes down.</summary>
    public static class OzLavaMeltPlan
    {
        public const int Small = 0, Medium = 1, Large = 2, Hull = 3;
        /// <summary>Seconds of the melt by size: a chip, a part, a turret or a hull half, a whole wreck. Each piece +-10 % by its own number. Starting values, set on the first clip.</summary>
        public static float SmallSeconds = 2.2f, MediumSeconds = 3.4f, LargeSeconds = 4.8f, HullSeconds = 6.5f;
        /// <summary>How far a piece flattens as it softens (share of its height), and how wide it gets.</summary>
        public const float Flatten = 0.42f, Widen = 0.12f;

        public static float Seconds(int size, uint hash)
        {
            float b = size == Small ? SmallSeconds : size == Medium ? MediumSeconds : size == Large ? LargeSeconds : HullSeconds;
            return b * (0.9f + 0.2f * (hash % 101u) / 100f);
        }

        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        public static float Smooth(float a, float b, float x) { float t = Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>Heat 0..1: it takes the lava's temperature in the first third.</summary>
        public static float Heat(float k) => Smooth(0f, 0.32f, k);
        /// <summary>Share of its height the glow has climbed from the lava line.</summary>
        public static float Band(float k) => Smooth(0.02f, 0.55f, k);
        /// <summary>Share of the way down. It starts slowly: first it glows, then it goes.</summary>
        public static float Sink(float k) => Smooth(0.14f, 1f, k);
        /// <summary>Share of the softening.</summary>
        public static float Soft(float k) => Smooth(0.25f, 0.95f, k);
        /// <summary>Height over the pivot of a point of the piece: x y z = the point along the piece's own axes, r = the height row of its turn (how far each axis points up).</summary>
        public static float PointY(float rx, float ry, float rz, float x, float y, float z) => rx * x + ry * y + rz * z;
        /// <summary>Height over the lava of the point that ends highest: from where it is now to under the surface. top0 is read for the end pose and end scale (OzLavaMelt.EndHeights), so at k = 1 nothing stands over the sheet.</summary>
        public static float TopOver(float top0, float under, float k) { float s = Sink(k); return top0 + (-under - top0) * s; }
        /// <summary>Seconds of one rubble chip, by its radius in m.</summary>
        public static float RubbleSeconds(float size) => 1.7f + 1.3f * Clamp01(size / 0.5f);
    }
}
