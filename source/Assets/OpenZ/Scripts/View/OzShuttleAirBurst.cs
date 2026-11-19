// VIEW ONLY: when a playable shuttle is
// destroyed IN THE AIR (its feet more than 0.5 m over the ground, the break-up's own test: flying, taking off or landing; every level, both
// teams), the seventh test explosion exp_v2_7 (BURST, the white air burst, View/OzUeBlastFx.Airburst.cs) plays at the ship's centre in the
// same frame, ON TOP of the ship's own death (OzShuttleBreakup), which is not changed, moved or re-timed. No rule, no damage, no Sim call:
// the eight shard hits of its picture are drawn at the ground points its rule beats would have (0.35 s after the burst), but no blast is
// sent to the rules. A ship destroyed on the ground keeps only its own death.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzShuttleAirBurst : MonoBehaviour
    {
        /// <summary>The switch (on): off = only the ship's own death, as before.</summary>
        public static bool On = true;
        /// <summary>Proof counters: bursts played (in the air), ship deaths on the ground left alone, refused (no effect player).</summary>
        public static int Played, GroundLeft, Refused;
        public static Vector3 LastAt; public static float LastAtTime = -1f; public static int LastShipId = -1;

        struct Pending { public OzUeBlastFx fx; public int stage; public Vector3 at; public float due; }
        readonly List<Pending> pending = new List<Pending>(8);
        float clock;
        static OzShuttleAirBurst runner;

        /// <summary>OzShuttleBreakup.TakesDeath hook, the moment the ship's death is taken: air = the break-up's own in-the-air test.</summary>
        public static void AtShipDeath(ZObject o, Vector3 centre, bool air)
        {
            if (!On || o == null) return;
            if (!air) { GroundLeft++; return; }
            var g = ZGame.I;
            float ground = g ? g.GroundY(centre.x, centre.z) : 0f;
            var origin = centre - Vector3.up * 9f;                                                     // the burst stands 9 m over its origin (OzUeBlastFx.AirAt): at the ship
            int seed = unchecked(o.Id * 73856093 ^ 19349663);
            int turn = Mathf.Abs(o.Id * 7) % 12;
            var st = OzUeBlasts.StagesOf(OzUeBlasts.Airburst, turn);
            var fx = OzUeBlastFx.Begin(OzUeBlasts.Airburst, origin, seed, st);
            if (!fx) { Refused++; return; }
            if (!runner) runner = new GameObject("OZ_ShuttleAirBurst").AddComponent<OzShuttleAirBurst>();
            float t0 = st.Length > 0 ? st[0].tick : 0;
            var c = ZGame.WorldToPx(new Vector3(centre.x, 0f, centre.z));
            for (int k = 0; k < st.Length; k++)
            {
                var p = ZGame.PxToWorld(c.x + st[k].dx, c.y + st[k].dy);
                p.y = k == 0 ? origin.y : (g ? g.GroundY(p.x, p.z) : ground);
                var e = new Pending { fx = fx, stage = k, at = p, due = runner.clock + (float)((st[k].tick - t0) * ZConst.TICK) };
                if (k == 0) fx.Beat(0, p); else runner.pending.Add(e);                                  // the burst in the same frame as the ship's death
            }
            Played++; LastAt = centre; LastAtTime = Time.time; LastShipId = o.Id;
            Debug.Log($"[OpenZ] shuttle {o.Id} destroyed in the air: exp_v2_7 (white air burst) at ({centre.x:0.0}, {centre.y:0.0}, {centre.z:0.0}), {ground:0.0} m ground; view only, no damage");
        }

        void Update()
        {
            var g = ZGame.I;
            clock += g && g.paused ? 0f : Time.deltaTime;                                              // a paused field holds the shard hits, as the explosion's own clock
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var e = pending[i];
                if (!e.fx) { pending.RemoveAt(i); continue; }
                if (clock < e.due) continue;
                e.fx.Beat(e.stage, e.at); pending.RemoveAt(i);
            }
        }

        void OnDestroy() { if (runner == this) runner = null; }
    }
}
