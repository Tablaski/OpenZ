// OpenZ-VK (GPL-3.0).
using System;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.Experimental
{
    /// <summary>Rendering policy and bridge clearance driven by the unchanged native map.</summary>
    internal static class OzAiTerrainV4
    {
        static readonly bool UseV3 = Array.IndexOf(Environment.GetCommandLineArgs(), "-oz-ai-maps-v3") >= 0;

        public static int Profile(string world) => world switch
        {
            "desert" => 1,
            "volcanic" => 2,
            "city" => 3,
            "jungle" => 4,
            _ => 0
        };

        public static bool Active(string world) => !UseV3 && Profile(world) != 0;

        // Mark runs in either grid direction without closing gaps or promoting other tile classes.
        public static bool[] StraightRidges(ZMapData map)
        {
            var ridges = new bool[map.width * map.height];
            MarkRuns(map.height, map.width, 1, map.width);
            MarkRuns(map.width, map.height, map.width, 1);
            return ridges;

            void MarkRuns(int lines, int length, int step, int stride)
            {
                for (int line = 0; line < lines; line++)
                {
                    int start = 0;
                    while (start < length)
                    {
                        if (!Solid(line * stride + start * step)) { start++; continue; }
                        int end = start + 1;
                        while (end < length && Solid(line * stride + end * step)) end++;
                        if (end - start >= 5)
                            for (int cell = start; cell < end; cell++) ridges[line * stride + cell * step] = true;
                        start = end;
                    }
                }
            }

            bool Solid(int index) => map.classes[index] == (int)ZClass.Impassable && (!map.HasLava || map.lava[index] == 0);
        }

        public static Bridge[] Bridges(ZMapData map) => map.objects
            .Where(obj => obj.Type == "bridge_vert" || obj.Type == "bridge_horz")
            .Select(obj => new Bridge(obj.x * 2, obj.y * 2,
                (5 + (obj.extra_links < 100 ? obj.extra_links : 0)) * 2, obj.Type == "bridge_vert")).ToArray();

        internal readonly struct Bridge
        {
            readonly float x, z, length;
            readonly bool vertical;

            public Bridge(float x, float z, float length, bool vertical)
            {
                this.x = x;
                this.z = z;
                this.length = length;
                this.vertical = vertical;
            }

            // The two outer tiles are rails/abutments, not a stack of cliff geometry.
            // Keep a short transition at the outer edges and fade out on both approaches.
            public float Clearance(float worldX, float worldZ)
            {
                float dx = worldX - x, dz = worldZ - z;
                float across = vertical ? dx : dz;
                float along = vertical ? dz : dx;
                if (across <= -2.5f || across >= 10.5f || along <= 0 || along >= length) return 0;
                return Smooth(Math.Min(across + 2.5f, 10.5f - across) / 1.5f)
                    * Smooth(Math.Min(along, length - along) * .5f);
            }

            static float Smooth(float value)
            {
                float t = Math.Min(1, value);
                return t * t * (3 - 2 * t);
            }
        }
    }
}
