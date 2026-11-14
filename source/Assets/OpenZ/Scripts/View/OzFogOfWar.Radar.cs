// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the radar circle (a standing radar of ours opens a big circle, animated). View only.
using System.Collections.Generic;
using System.Diagnostics;
using OpenZ.Sim;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// radar of the player's side (a radar building of his team, not a ruin) opens a circle of 48 m round its centre, like Zod's scan circle.
    /// It grows from the radar's own building sight (<see cref="SightPx"/>, 20 m, which the stamp round opens with the zone's flag and
    /// buildings: the game's normal reveal of a capture) to 48 m over 2.5 s of real time with smootherstep easing, stamped every frame with a
    /// fractional radius; the per-cell reveal (<see cref="RevealSeconds"/>) still softens behind the edge. Where the open ground round the
    /// radar already reaches further all round, it grows from there (View/OzFowRadar.cs StartPx).
    /// <list type="bullet">
    /// <item>Hooks in View/OzFogOfWar.cs: <see cref="RadarStart"/> (StartFog, before the first frame: radars held at the start open at once,
    /// the start area rule), <see cref="RadarStep"/> (Update, next to the stamp round, every frame), <see cref="RadarReset"/> (COVER AGAIN:
    /// every radar of ours opens again, animated); the growth steps on <see cref="FogStep"/>, the fog's own clock (the reveal's).</item>
    /// <item>When it plays: a radar gained with its zone, a ruined radar of ours rebuilt (a crane or the automatic rebuild: it stands again),
    /// after COVER AGAIN. Lost or ruined: the growth is forgotten (a regain begins again; explore-once keeps the open ground open).</item>
    /// <item>While a circle grows the minimap cover is repainted up to <see cref="RadarMinimapPace"/> apart (otherwise 0.2 s,
    /// View/OzFogOfWar.Minimap.cs), so it eases with the fog; the soft edge field and its signed distance (FieldDist) are made again on
    /// every frame the reveal map changed (the fog's own Upload / RunField), and the units held off under the fog (HideCovered, Covers) read
    /// the same reveal map, so the circle shows them as it opens.</item>
    /// <item>Only while the fog is on (never in 'Zod exact', the Unit editor, a computer match): RadarStep runs on the fog's own path only.
    /// The rules and the computer never read it.</item>
    /// </list>
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        /// <summary>The minimap cover's repaint pace while a radar circle grows (s; about 30 a second).</summary>
        public const float RadarMinimapPace = 0.03f;

        /// <summary>The fog's frame clock (s): <see cref="FowDt"/> (View/OzFogOfWar.Render.cs, the fog's own since its round 4): the fixed
        /// capture step while a take records frame-locked (Time.captureDeltaTime; Time.unscaledDeltaTime stays real time then), else the real
        /// frame time (at most 0.25 s). The radar circle's growth (at most 0.1 s a frame, View/OzFowRadar.cs) and the fog's per-cell reveal and
        /// fade (OzFogOfWar.Update) all step on it, so a recorded clip shows them as the game plays them.</summary>
        public static float FogStep => FowDt;

        readonly OzFowRadar radar = new OzFowRadar();
        int radarMiniSeen = -1; float radarMiniAt;
        struct RadarNote { public int Id, Sight; public float From, Edge; public OzFowRadar.Step S; }
        readonly List<RadarNote> radarNotes = new List<RadarNote>(8);

        /// <summary>Checks: the radar circle's processor time a frame (ms): the last frame, the worst since <see cref="RadarCostReset"/>, frames.
        /// Its log lines are written after the time is taken (they are not part of it).</summary>
        public static double RadarMsLast, RadarMsMax; public static int RadarFrames;
        public static void RadarCostReset() { RadarMsLast = RadarMsMax = 0.0; RadarFrames = 0; }
        /// <summary>Checks: the fog's clock (<see cref="FogStep"/>) summed over the frames RadarStep ran, and those frames.</summary>
        public static double FogClockSeconds; public static int FogClockFrames;
        /// <summary>Checks: a radar's circle radius now (Zod px); -1 = none (not ours, a ruin, the fog off).</summary>
        public static float RadarRadiusPx(int id) => I && I.mask != null ? I.radar.RadiusOf(id) : -1f;
        /// <summary>Checks: the seconds a radar's circle has grown; -1 = none.</summary>
        public static float RadarAge(int id) => I && I.mask != null ? I.radar.AgeOf(id) : -1f;
        /// <summary>Checks: the radius a radar's circle began from, and the open ground's edge measured then (Zod px); -1 = none.</summary>
        public static float RadarFromPx(int id) => I && I.mask != null ? I.radar.FromOf(id) : -1f;
        public static float RadarEdgePx(int id) => I && I.mask != null ? I.radar.EdgeOf(id) : -1f;
        /// <summary>Checks: radar circles growing now.</summary>
        public static int RadarGrowing => I ? I.radar.Growing : 0;
        /// <summary>Checks: this match's radar circles (null with no level).</summary>
        public static OzFowRadar Radar => I ? I.radar : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RadarStatics() { RadarCostReset(); FogClockSeconds = 0.0; FogClockFrames = 0; }

        /// <summary>StartFog, before mask.ShowAll(): the radars the player holds now open their whole circle at once (no animation).</summary>
        void RadarStart()
        {
            radar.Reset();
            radar.Begin(0f);
            int n = RadarFeed(true);
            radar.End();
            radarNotes.Clear();
            if (n > 0) Debug.Log($"[OpenZ FogOfWar] radar circle: {n} radar(s) of ours at the start, {OzFowRadar.CirclePx / 8} m open at once (the start area)");
        }

        /// <summary>Update, every frame with the fog on: new radars of ours start their opening, growing ones grow, lost ones forget it.</summary>
        void RadarStep()
        {
            long t0 = Stopwatch.GetTimestamp();
            float step = FogStep;
            radar.Begin(step);                                                          // real time (the fixed step while a clip records)
            RadarFeed(false);
            radar.End();
            if (radar.Growing > 0)
            {
                if (MinimapPaints != radarMiniSeen) { radarMiniSeen = MinimapPaints; radarMiniAt = Time.unscaledTime - Time.unscaledDeltaTime; }   // the last repaint was last frame
                float next = radarMiniAt + RadarMinimapPace;
                if (miniNext > next) miniNext = next;
            }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            RadarMsLast = ms; if (ms > RadarMsMax) RadarMsMax = ms; RadarFrames++;
            FogClockSeconds += step; FogClockFrames++;
            if (radarNotes.Count > 0) RadarLog();                                       // after the time is taken
        }

        /// <summary>COVER AGAIN: every radar of ours opens its circle again, animated, from its building sight.</summary>
        void RadarReset() => radar.Reset();

        /// <summary>Feeds the player's standing radars to the growth; returns how many there are. A start or an end is noted (logged later).</summary>
        int RadarFeed(bool instant)
        {
            int agro = W.S != null ? W.S.agro_distance : 40, n = 0;
            var list = W.Objects;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o.Kind != ZKind.Building || o.Type != "radar" || o.Owner != player || o.Destroyed || o.KillAt >= 0) continue;
                n++;
                int sight = SightPx(o, agro);
                var s = radar.See(mask, o.Id, o.CenterX, o.CenterY, sight, instant);
                if (s == OzFowRadar.Step.Started || s == OzFowRadar.Step.Opened || s == OzFowRadar.Step.AlreadyOpen)
                    radarNotes.Add(new RadarNote { Id = o.Id, Sight = sight, From = radar.FromOf(o.Id), Edge = radar.EdgeOf(o.Id), S = s });
            }
            return n;
        }

        void RadarLog()
        {
            for (int i = 0; i < radarNotes.Count; i++)
            {
                var r = radarNotes[i];
                if (r.S == OzFowRadar.Step.Started)
                    Debug.Log($"[OpenZ FogOfWar] radar circle: radar #{r.Id} stands and is ours, its circle opens from {r.From / 8f:0.#} m to {OzFowRadar.CirclePx / 8} m over {OzFowRadar.GrowSeconds:0.0} s "
                              + $"(its building sight {r.Sight / 8f:0.#} m, open ground round it {r.Edge / 8f:0.#} m)");
                else if (r.S == OzFowRadar.Step.Opened) Debug.Log($"[OpenZ FogOfWar] radar circle: radar #{r.Id} open ({OzFowRadar.CirclePx / 8} m)");
                else Debug.Log($"[OpenZ FogOfWar] radar circle: radar #{r.Id} stands and is ours, its {OzFowRadar.CirclePx / 8} m are all open already (nothing to animate)");
            }
            radarNotes.Clear();
        }
    }
}
