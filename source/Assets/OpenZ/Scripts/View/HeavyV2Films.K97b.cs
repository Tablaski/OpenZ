#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k97b, item 176 (the gun falls apart at its death, View/OzGunBreakup.cs), its counted check and film (editor only; menus OpenZ/Gun v2/16):
    ///  "gun_death" / "gun_death_close": a red v2 and a red v1 side by side on the recoil test's fixed spots of L02, the RTS camera at the game's
    ///  play zoom (close: 32 m), 1920 x 1080, 30 fps, the game's sound; 1.5 s into the take both are destroyed at once, then 12 s more are filmed.
    ///  Counted for each gun: the frames from the kill to its view's death; the parts thrown (OzGunBreakup.Recent: at least 5); the time from the
    ///  view's death to the first part moving (under 0.1 s); every part's rise, its landing, its rest (still for a third of a second, or put to
    ///  sleep by the physics, before the wreck pieces' 8 s freeze) and its gap to the ground then; the rest of the turret tossed at Zod's
    ///  2-4 s; the base's break (one in two by the game's seed); the sounds from the sound log (whoosh, snap, the clanks and clangs of the
    ///  landings); the break's cost (ms, the turret split once per model). The rules are not touched (both guns are destroyed by health 0).
    /// </summary>
    public partial class HeavyV2Films
    {
        static readonly FieldInfo TossedK97b = typeof(UnitView).GetField("tossed", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo BreakDoneK97b = typeof(UnitView).GetField("breakDone", BindingFlags.NonPublic | BindingFlags.Instance);

        sealed class PartK97b
        {
            public OzGunBreakup.Part p; public float firstMove = -1f, rise, landedAt = -1f, restAt = -1f, frozeAt = -1f, gap = float.NaN, restGap = float.NaN; public int still; public bool gone;
        }

        IEnumerator GunDeathTakeK97b(bool close)
        {
            if (!GunJob()) yield break;
            if (WreckFx.Force == 2) { WreckFx.Force = 0; Note("WreckFx.Force was 2 (plain wrecks, a test aid): set to 0 for this take"); }
            if (!Spot("v2", out int ax2, out int ay2, "recoil") || !Spot("v1", out int ax1, out int ay1, "recoil")) yield break;
            var v2 = GunUnit(ax2, ay2, Team.Red, 2, "red v2"); var v1 = GunUnit(ax1, ay1, Team.Red, 1, "red v1");
            if (!CheckHeavy(v2, "red v2 (gun)", false) || !CheckHeavy(v1, "red v1 (gun)", false)) yield break;
            CombatStageK87b(v2, "gun death test");                                                     // the level's own enemies near it unarmed
            hero = v2; follow.Add(v2); follow.Add(v1);
            if (close) h0 = CloseHeight;
            Note($"OzGunBreakup: Enabled {OzGunBreakup.Enabled}, V1 {OzGunBreakup.V1}; WreckFx.Force {WreckFx.Force} (0 = the game's seeded chances); the camera at {h0:0} m");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.5f);
            var guns = new[] { v2, v1 }; var tags = new[] { "v2", "v1" }; var dv = new[] { View(v2), View(v1) };
            int killF = frameNo; float snd0 = Time.unscaledTime; int breaks0 = OzGunBreakup.Breaks;
            foreach (var o in guns) { o.SetHealth(0); W.UpdateObjectHealth(o); }
            Note("both guns destroyed at once (health 0)");
            var diedF = new[] { -1, -1 }; var diedT = new[] { -1f, -1f }; var tossT = new[] { -1f, -1f }; var tossReal = new[] { -1f, -1f }; var brokeT = new[] { -1f, -1f };
            var parts = new List<PartK97b>(); var seen = new HashSet<OzGunBreakup.Part>();
            int endF = frameNo + 12 * Fps;
            while (!done && recording && frameNo < endF)
            {
                for (int i = 0; i < 2; i++)
                {
                    var v = dv[i]; if (!v) continue;
                    if (diedF[i] < 0 && v.Dying) { diedF[i] = frameNo; diedT[i] = Time.time; }
                    if (tossT[i] < 0f && TossedK97b != null && TossedK97b.GetValue(v) is Transform tt && tt) { tossT[i] = Time.time; tossReal[i] = Time.unscaledTime; }
                    if (brokeT[i] < 0f && BreakDoneK97b != null && BreakDoneK97b.GetValue(v) is bool bd && bd) brokeT[i] = Time.time;
                }
                foreach (var p in OzGunBreakup.Recent)
                    if ((p.gun == v2.Id || p.gun == v1.Id) && seen.Add(p)) parts.Add(new PartK97b { p = p });
                foreach (var q in parts) SamplePartK97b(q);
                yield return null;
            }
            yield return TakeWait(0.3f);
            StopTake(); WriteCrops();

            // ---- the counts
            for (int i = 0; i < 2; i++)
            {
                int gid = guns[i].Id; var mine = parts.FindAll(x => x.p.gun == gid);
                float first = -1f; foreach (var q in mine) if (q.firstMove >= 0f && (first < 0f || q.firstMove < first)) first = q.firstMove;
                int rested = 0, landed = 0, low = 0; var sb = new StringBuilder();
                foreach (var q in mine)
                {
                    if (q.restAt >= 0f) rested++;
                    if (q.landedAt >= 0f) landed++;
                    if (!float.IsNaN(q.restGap) && q.restGap > -0.35f && q.restGap < 0.45f) low++;
                    sb.Append($"{q.p.kind}: out {new Vector2(q.p.v0.x, q.p.v0.z).magnitude:0.0} up {q.p.v0.y:0.0} m/s, rose {q.rise:0.00} m, landed {(q.landedAt >= 0f ? q.landedAt.ToString("0.00") + " s" : "NO")}, at rest {(q.restAt >= 0f ? q.restAt.ToString("0.00") + " s" : "NO")}{(q.frozeAt >= 0f ? $" (frozen {q.frozeAt:0.00} s)" : "")}, ground gap at rest {(float.IsNaN(q.restGap) ? "-" : q.restGap.ToString("0.00") + " m")}, at the end {(float.IsNaN(q.gap) ? "-" : q.gap.ToString("0.00") + " m")}{(q.gone ? " (gone)" : "")}; ");
                }
                string tag = tags[i];
                Fact($"{tag}: its view died {(diedF[i] >= 0 ? (diedF[i] - killF) + " frame(s)" : "NEVER")} after the kill; parts thrown {mine.Count}: {(mine.Count >= 5 ? "PASS" : "FAIL")} (at least 5)");
                Fact($"{tag}: the first part moved {(first >= 0f ? first.ToString("0.000") + " s" : "NEVER")} after its death: {(first >= 0f && first < 0.1f ? "PASS" : "FAIL")} (under 0.1 s)");
                Fact($"{tag}: parts landed {landed}/{mine.Count}, at rest {rested}/{mine.Count} before the 8 s freeze, on the ground when at rest {low}/{mine.Count} (its lowest point -0.35..0.45 m from the ground): {(mine.Count > 0 && rested == mine.Count && low == mine.Count ? "PASS" : "FAIL")}");
                Fact($"{tag} parts: {sb}");
                float dtToss = tossT[i] >= 0f && diedT[i] >= 0f ? tossT[i] - diedT[i] : -1f;
                Fact($"{tag}: the rest of its turret tossed {(dtToss >= 0f ? dtToss.ToString("0.00") + " s" : "NEVER")} after its death (Zod's 2-4 s): {(dtToss >= 1.9f && dtToss <= 4.1f ? "PASS" : "FAIL")}; its base {(brokeT[i] >= 0f ? $"fell apart {brokeT[i] - diedT[i]:0.00} s after its death" : "stayed as the burnt wreck")} (one in two by the game's seed; either is right)");
            }
            // the parts land before anything else of the two guns moves (the tosses come 2-4 s after the deaths): the clanks and clangs between
            // the break's frame (its own snap) and the first toss are the parts' landings
            float brk = float.PositiveInfinity, toss1 = float.PositiveInfinity;
            foreach (var q in parts) brk = Mathf.Min(brk, q.p.real0);
            foreach (var t in tossReal) if (t >= 0f) toss1 = Mathf.Min(toss1, t);
            int drop = 0, landings = 0; var keys = new Dictionary<string, int>();
            foreach (var e in OzSoundLog.Since(snd0))
            {
                if (e.key != "dst_debris_metal" && e.key != "turret" && e.key != "dst_turret_toss" && e.key != "tank_impact") continue;
                bool ok = e.outcome != null && e.outcome.StartsWith("PLAY");
                if (!ok) { drop++; continue; }
                keys.TryGetValue(e.key, out int n); keys[e.key] = n + 1;
                if ((e.key == "dst_debris_metal" || e.key == "turret") && e.t > brk + 1e-4f && e.t < toss1) landings++;
            }
            var ks = new StringBuilder(); foreach (var kv in keys) ks.Append(kv.Key).Append(' ').Append(kv.Value).Append(", ");
            Fact($"sounds after the kill (sound log, played): {ks}not played {drop}; the parts' landings (dst_debris_metal + turret after the break's frame, before the first toss): {landings}: {(landings >= 4 ? "PASS" : "FAIL")} (at least 4 for the two guns)");
            Fact($"cost: {OzGunBreakup.Breaks - breaks0} break(s); the last {OzGunBreakup.LastMs:0.00} ms, the worst so far {OzGunBreakup.MaxMs:0.00} ms (the turret split, once per model: {OzGunBreakup.SplitMs:0.00} ms, {OzGunBreakup.Splits} made): {(OzGunBreakup.MaxMs < 5f ? "PASS" : "FAIL")} (under 5 ms); afterwards nothing runs but the wreck pieces' own physics; last: {OzGunBreakup.LastBreak}");
            Fact("pass rule (MP4 + sheet): at the blast both guns fly apart in the same frames: the barrel tumbles out forward, the collar and the front panel go forward, the side frames peel off sideways, the v2's top with its hex block flies up and off; every part falls, bounces a little with a clank and lies still on the ground (none in the base, none in the air, none through the ground); what is left on the base burns and 2-4 s later is tossed as before; no gun stands whole after its death");
        }

        /// <summary>Every frame of the take: one part's movement (its first move, rise, landing on the ground, rest, freeze, end gap).</summary>
        void SamplePartK97b(PartK97b q)
        {
            var p = q.p;
            if (!p.t) { q.gone = true; return; }
            float age = Time.time - p.t0;
            var pos = p.t.position;
            if (q.firstMove < 0f && (pos - p.p0).sqrMagnitude > 1e-4f) q.firstMove = age;
            q.rise = Mathf.Max(q.rise, pos.y - p.p0.y);
            float lowY = float.PositiveInfinity;
            foreach (var r in p.t.GetComponentsInChildren<Renderer>()) if (r.enabled) lowY = Mathf.Min(lowY, r.bounds.min.y);
            float gy = g.GroundY(pos.x, pos.z);
            if (!float.IsInfinity(lowY)) q.gap = lowY - gy;
            if (q.landedAt < 0f && age > 0.15f && !float.IsNaN(q.gap) && q.gap < 0.45f) q.landedAt = age;
            var rb = p.rb;
            if (rb && rb.isKinematic) { if (q.frozeAt < 0f) q.frozeAt = age; if (q.restAt < 0f && q.landedAt >= 0f && age < 7.9f) { q.restAt = age; q.restGap = q.gap; } return; }   // put to sleep (WreckPieces freezes a sleeping piece; at 8 s it freezes every piece)
            if (!rb) return;
            bool still = rb.linearVelocity.sqrMagnitude < 0.15f * 0.15f && rb.angularVelocity.sqrMagnitude < 0.5f * 0.5f;
            q.still = still && q.landedAt >= 0f ? q.still + 1 : 0;
            if (q.restAt < 0f && q.still >= 10) { q.restAt = age - 9f / Fps; q.restGap = q.gap; }
        }
    }
}
#endif
