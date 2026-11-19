using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBigTanksFx
    {
        static readonly List<Vector3> canAt = new List<Vector3>(), canVel = new List<Vector3>(), canSpin = new List<Vector3>();
        static readonly List<Quaternion> canRot = new List<Quaternion>();
        static readonly List<bool> canCrushed = new List<bool>();
        static readonly List<Collider> noPieces = new List<Collider>();
        /// <summary>The cans lie with the wreck: its pieces' longest own time (WreckPieces.Lifetime: 9-17 s), then they sink with it.</summary>
        public static float CanStaySeconds = 17f;
        /// <summary>Share of crushed empties among them; their throw (m/s out, up) and spin (rad/s).</summary>
        public static float CanCrushedShare = 0.25f, CanOutMin = 3.5f, CanOutMax = 7.5f, CanUpMin = 4f, CanUpMax = 8f;

        /// <summary>The Bizon's break-up: its fuel cans fly out of the hull (OzBigTanksFx.cs says what). A seeded pick per death (the tank's id).</summary>
        static void ThrowCans(UnitView v)
        {
            if (!OzFuelCan.Enabled || !v || v.Obj == null) return;
            var o = v.Obj; int seed = o.Id * 7919 + 1093;
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Transform hull = null;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == "TankChassis") { hull = t; break; }
            if (!hull) hull = v.transform;
            int n = rnd.Next(CanMin, CanMax + 1);
            canAt.Clear(); canVel.Clear(); canSpin.Clear(); canRot.Clear(); canCrushed.Clear();
            var centre = hull.TransformPoint(new Vector3(0f, 1.8f, 0.2f));
            var g = ZGame.I;
            for (int i = 0; i < n; i++)
            {
                var local = new Vector3(R(-1.1f, 1.1f), R(1.3f, 2.4f), R(-1.9f, 2.3f));                         // inside the hull (TankChassis frame)
                var at = hull.TransformPoint(local);
                if (g) at.y = Mathf.Max(at.y, g.GroundY(at.x, at.z) + 0.3f);
                var dir = at - centre; dir.y = 0f; if (dir.sqrMagnitude < 1e-3f) dir = new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)); dir.Normalize();
                dir = Quaternion.Euler(0f, R(-35f, 35f), 0f) * dir;
                canAt.Add(at);
                float cOut = R(CanOutMin, CanOutMax), cUp = R(CanUpMin, CanUpMax);                                  // the same two seeded draws as before (the picks after them unchanged)
                canVel.Add(OzBigTankWreck.Scatter                                                                  // 1.5 big_tanks_wreck_scatter: the cans fall the pieces' uneven way, no ring
                    ? OzBigTankWreck.CanThrow(o.Id, i, dir, at.y - (g ? g.GroundY(at.x, at.z) : hull.position.y), Mathf.InverseLerp(CanOutMin, CanOutMax, cOut), Mathf.InverseLerp(CanUpMin, CanUpMax, cUp))
                    : dir * cOut + Vector3.up * cUp);
                canSpin.Add(new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)).normalized * R(8f, 20f));
                canRot.Add(Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f)));
                canCrushed.Add(rnd.NextDouble() < CanCrushedShare);
            }
            var bt = OzFuelCan.Throw(o.Id, "bizon", false, centre, noPieces, canAt, canRot, canVel, canSpin, canCrushed, CanStaySeconds, seed);
            CanDeaths++;
            if (bt == null) return;
            CansThrown += bt.count;
            // a few of them burst on landing: a small fire puff (the full ones only, a seeded pick)
            int fires = rnd.Next(CanFireMin, CanFireMax + 1), picked = 0;
            var host = I ? I.gameObject : v.gameObject;
            for (int i = 0; i < bt.cans.Count && picked < fires; i++)
            {
                var c = bt.cans[i];
                if (!c || c.Crushed || rnd.NextDouble() < 0.35) continue;
                var w = host.AddComponent<CanBurst>(); w.can = c; w.batch = bt; w.id = o.Id; picked++;
            }
            Last = $"bizon #{o.Id}: {bt.count} fuel cans thrown ({bt.crushed} crushed), {picked} will burst";
        }

        /// <summary>One can that bursts into a small fire puff at its first landing (once; it gives up when the can is gone or 8 s passed).</summary>
        sealed class CanBurst : MonoBehaviour
        {
            public OzFuelCan can; public OzFuelCan.Batch batch; public int id; float age;

            void Update()
            {
                age += Time.deltaTime;
                if (!can || can.Of != batch || age > 8f || can.state == OzFuelCan.State.Water || can.state == OzFuelCan.State.Sinking) { Destroy(this); return; }
                if (can.state != OzFuelCan.State.Landed && can.state != OzFuelCan.State.Rest) return;
                var p = can.transform.position;
                var fx = OpenZFxPlayer.I;
                if (fx && fx.isActiveAndEnabled)
                {
                    OzFireSource.CanFire(fx, can.transform, p);   /* 1.5 N33 (kit fire_no_source_n33): was fx.WreckFire(p + Vector3.up * 0.15f, 0.45f), the burst of a vehicle: its core 0.85 m over the GROUND under the can whatever the scale; now sized to the can and standing on it */
                    fx.AddLight(p + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), 2.2f, 4f, 0.7f);
                    fx.Dust(p, 0.12f);
                }
                if (AudioDirector.Has(CanFireEvent)) AudioDirector.Fire(CanFireEvent, p, id, 0.5f, Random.Range(1.05f, 1.2f));
                CanFires++;
                Destroy(this);
            }
        }
    }
}
