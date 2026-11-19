using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit big_tanks_fx: when a Mammoth's barrels must be clear of a vehicle it is about to ram or is ramming (View/UnitAim.RamClear.cs
    /// moves them). View only: it reads the rules' state (ZObject.Ramming, RamTargetId, RamDoomBy, the hull's heading), it never writes it.
    /// <list type="bullet">
    /// <item><see cref="Clear.Pre"/>: a crewed Mammoth with the ram on (DV180 + DV182) has an enemy crewed vehicle inside the ram's start zone
    /// (the sim's own test with whole numbers: +/- <see cref="ZObject.RamStartArcDeg"/> + <see cref="PreArcMoreDeg"/> deg of its hull heading,
    /// at most <see cref="ZObject.RamStartGapPx"/> + <see cref="PreGapMorePx"/> px hull to hull): the barrels rise.</item>
    /// <item><see cref="Clear.Ram"/>: from the ram's start (Ramming) while it lasts, then while a vehicle it rammed is still flipping or was
    /// destroyed less than <see cref="HoldAfterSeconds"/> s ago: raised and turned aside.</item>
    /// </list>
    /// <see cref="FlipWait"/>: OzMammothRamView asks at the impact how long the heave must wait so the barrels are clear first (a ram that
    /// started close reaches its contact sooner than <see cref="UnitAim.RamClearSeconds"/>).
    /// </summary>
    public static class OzRamBarrels
    {
        public enum Clear { None, Pre, Ram }
        /// <summary>The pre-clear zone is the ram's start zone and this margin (it begins a moment before a ram can).</summary>
        public const int PreGapMorePx = 8, PreArcMoreDeg = 5;
        /// <summary>After the rammed vehicle's death the clear pose is held this long (its wreck pieces fly first).</summary>
        public const float HoldAfterSeconds = 0.6f;

        sealed class Rec { public float lastRam = -99f, rampStart = -99f; public int frame = -1; public Clear state; }
        static readonly Dictionary<int, Rec> recs = new Dictionary<int, Rec>();
        /// <summary>Proof: impacts that held the heave, the longest hold (s).</summary>
        public static int FlipWaits; public static float FlipWaitMax;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { recs.Clear(); FlipWaits = 0; FlipWaitMax = 0f; }

        static Rec RecOf(ZObject m) { if (!recs.TryGetValue(m.Id, out var r)) { if (recs.Count > 64) recs.Clear(); recs[m.Id] = r = new Rec(); } return r; }

        /// <summary>The Mammoth's clear state this frame (worked out once per frame).</summary>
        public static Clear StateOf(ZObject m)
        {
            var g = ZGame.I;
            if (m == null || g == null || g.World == null || m.Type != OzBigTanks.Mammoth || m.Kind != ZKind.Vehicle) return Clear.None;
            var r = RecOf(m);
            if (r.frame == Time.frameCount) return r.state;
            r.frame = Time.frameCount;
            float now = Time.time;
            if (m.Destroyed || m.Owner == Team.Neutral || m.Drivers.Count == 0 || !ZWorld.MammothRamDeviation || !ZWorld.MammothDeviation) { r.rampStart = -99f; return r.state = Clear.None; }
            bool ramming = m.Ramming;
            if (ramming && now - r.lastRam > 0.25f) r.rampStart = now;                                // a new ram began
            if (ramming || RamVictimLive(g.World, m)) r.lastRam = now;
            if (now - r.lastRam <= HoldAfterSeconds) return r.state = Clear.Ram;
            return r.state = InStartZone(g.World, m) ? Clear.Pre : Clear.None;
        }

        /// <summary>A vehicle this Mammoth rammed is still on its spikes or flipping (not yet destroyed).</summary>
        static bool RamVictimLive(ZWorld w, ZObject m)
        {
            var all = w.Objects;
            for (int i = 0; i < all.Count; i++) { var o = all[i]; if (o != null && o.RamDoomBy == m.Id && !o.RamCrushed && !o.Destroyed) return true; }
            return false;
        }

        /// <summary>An enemy crewed vehicle inside the start zone (the sim's whole number test, a little wider).</summary>
        static bool InStartZone(ZWorld w, ZObject m)
        {
            double fx = m.FaceX, fy = m.FaceY, fl = System.Math.Sqrt(fx * fx + fy * fy);
            if (fl < 1e-6) return false;
            long hx = (long)System.Math.Round(fx * 1024.0 / fl), hy = (long)System.Math.Round(fy * 1024.0 / fl);
            long tanMilli = (long)System.Math.Round(System.Math.Tan((ZObject.RamStartArcDeg + PreArcMoreDeg) * System.Math.PI / 180.0) * 1000.0);
            var all = w.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null || t == m || t.Kind != ZKind.Vehicle || t.Destroyed || t.Owner == Team.Neutral || t.Owner == m.Owner || t.Drivers.Count == 0 || t.IsShuttle) continue;
                long dx = t.CenterX - m.CenterX, dy = t.CenterY - m.CenterY, rr = ZPathing.Radius(m) + ZPathing.Radius(t) + ZObject.RamStartGapPx + PreGapMorePx;
                if (dx * dx + dy * dy > rr * rr) continue;
                long dot = dx * hx + dy * hy; if (dot <= 0) continue;
                long cross = dx * hy - dy * hx; if (cross < 0) cross = -cross;
                if (cross * 1000 <= dot * tanMilli) return true;
            }
            return false;
        }

        /// <summary>OzMammothRamView, the ram's start event: the clear begins now (the ram and its contact can fall in one drawn frame).</summary>
        public static void NoteStart(ZObject m)
        {
            if (m == null || m.Type != OzBigTanks.Mammoth) return;
            var r = RecOf(m); r.rampStart = r.lastRam = Time.time;
        }

        /// <summary>OzMammothRamView, at the impact: real seconds the heave waits so the barrels are clear (0 when the ram began long enough
        /// ago); at most <see cref="MaxWaitShare"/> of the flip's own time (flipSeconds, real), so the flip still lands before the rules' death.</summary>
        public static float FlipWait(ZObject m, float flipSeconds)
        {
            if (!UnitAim.RamClearOn || m == null || !recs.TryGetValue(m.Id, out var r) || r.rampStart < 0f) return 0f;
            if (ZWorld.RamFightDeviation && UnitAim.RamFightViewOn) return 0f;   // 1.5 DV196 (kit mammoth_ram_fight_n26c): no turn aside to wait for: the heave starts at the rules' tick (the barrels are kept clear by View/UnitAim.RamFight.cs)
            float since = Time.time - r.rampStart;
            float wait = Mathf.Clamp(UnitAim.RamClearSeconds - since, 0f, MaxWaitShare * Mathf.Max(0f, flipSeconds));
            if (wait > 0f) { FlipWaits++; FlipWaitMax = Mathf.Max(FlipWaitMax, wait); }
            return wait;
        }

        /// <summary>The heave's wait is at most this share of the flip's time (a fast game speed shortens both).</summary>
        public const float MaxWaitShare = 0.4f;
    }
}
