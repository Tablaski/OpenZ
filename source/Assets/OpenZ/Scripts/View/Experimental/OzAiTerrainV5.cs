// OpenZ-VK (GPL-3.0).
using System;

namespace OpenZ.Experimental
{
    internal static class OzAiTerrainV5
    {
        static readonly bool Requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-oz-ai-maps-v5") >= 0;

        public static bool HasExample(string id) => id is "SM02" or "SM08" or "SM11" or "SM16" or "SM17";
        public static bool Active(string id) => Requested && HasExample(id);

        // Fractured-ice artwork in the Arctic atlas. These identifiers carry no movement rule.
        public static bool ArcticIceArt(int tile) => tile is >= 246 and <= 248 or >= 264 and <= 268 or 284 or 285 or 288 or 289
            or >= 449 and <= 453 or 459 or >= 468 and <= 477;

        // World metres measured outward from the native map. The V4 collision mesh never uses this.
        public static float RimDistance(float x, float z, int width, int height) =>
            Math.Max(Math.Max(-x, x - width * 2), Math.Max(-z, z - height * 2));

        public static float RimWeight(float distance)
        {
            float t = Math.Max(0, Math.Min(1, (distance - 1) / 6));
            return t * t * (3 - 2 * t);
        }
    }
}
