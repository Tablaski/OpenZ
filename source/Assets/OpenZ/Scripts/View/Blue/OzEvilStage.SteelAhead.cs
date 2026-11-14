// OpenZ (GPL-3.0) - version 1.2, the evil HQ: its steel pictures (OzEvilStage.SteelTex.cs) drawn ahead, off the game's frames.
// The HQ stage (the blue scenes and the battlefield screen's comms card) drew its five steel pictures (the deck, the grating, the wall
// panels, the console front, the hazard stripes) on the frame it was first built: about 0.6 s of work, so the first blue start of a session
// thread from the first scene of the game on (the pixel maths needs no engine object); when the stage is built, only the pictures are made
// from those pixels (a few milliseconds). The pictures are the same, pixel for pixel (the same surfaces, the same noise seed). If the
// ahead (switched off, or it failed), it draws them itself as before. Switch: <see cref="SteelAhead"/>.
using System.Threading;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary></summary>
        public static bool SteelAhead = true;
        /// <summary></summary>
        public static float SteelAheadMs => aheadMs;
        static SteelPx[] aheadPx;
        static volatile int aheadState;                                                                 // 0 not started, 1 drawing, 2 drawn, 3 failed, 4 taken
        static float aheadMs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SteelAheadBoot() { if (SteelAhead) StartSteelAhead(); }

        static bool SteelOk(SteelMaps m) => m != null && m.albedo && m.normal && m.metal;
        static bool SteelAllOk() => SteelOk(mapsDeck) && SteelOk(mapsGrating) && SteelOk(mapsWall) && SteelOk(mapsConsole) && texHazard;

        static void StartSteelAhead()
        {
            if (aheadState == 1 || aheadState == 2 || SteelAllOk()) return;
            aheadState = 1;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var nz = new TileNoise(1306);                                                       // the seed EnsureSteelMaps uses: the same pictures
                    var px = new SteelPx[5];
                    px[0] = PaintPx("evil_hq_deck", (x, y) => DeckSurf(nz, x, y), 2.4f, 2.4f, 768, 768, true, true, true, 8);
                    px[1] = PaintPx("evil_hq_grating", (x, y) => GratingSurf(nz, x, y), 0.4f, 0.4f, 256, 256, false, true, false, 8);
                    px[2] = PaintPx("evil_hq_wall", (x, y) => WallSurf(nz, x, y), 1.3f, 1.3f, 512, 512, true, true, true, 4);
                    px[3] = PaintPx("evil_hq_console", (x, y) => ConsoleSurf(nz, x, y), 1.8f, 0.9f, 512, 256, false, false, false, 4);
                    px[4] = PaintColourPx("evil_hq_hazard", (x, y) => HazardSurf(nz, x, y), 0.2f, 0.2f, 128, 128);
                    aheadMs = (float)sw.Elapsed.TotalMilliseconds;
                    aheadPx = px;
                    aheadState = 2;
                }
                catch (System.Exception) { aheadPx = null; aheadState = 3; }
            });
        }

        /// <summary>
        /// True: every picture is there now. False: nothing drawn ahead to take (the caller draws what is missing, as before).</summary>
        static bool SteelFromAhead()
        {
            if (!SteelAhead) return false;
            if (aheadState == 0) return false;
            if (aheadState == 1) SpinWait.SpinUntil(() => aheadState != 1, 3000);
            var px = aheadState == 2 ? aheadPx : null;
            if (px == null || px.Length < 5) return false;
            if (!SteelOk(mapsDeck)) mapsDeck = UploadMaps(px[0]);
            if (!SteelOk(mapsGrating)) mapsGrating = UploadMaps(px[1]);
            if (!SteelOk(mapsWall)) mapsWall = UploadMaps(px[2]);
            if (!SteelOk(mapsConsole)) mapsConsole = UploadMaps(px[3]);
            if (!texHazard) texHazard = UploadColour(px[4]);
            aheadPx = null; aheadState = 4;                                                             // the pixels are let go (the pictures keep them)
            return SteelAllOk();
        }
    }
}
