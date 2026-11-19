// OpenZ (GPL-3.0)
// The rule is DV193 (Sim/ZBigTanks.Ground.cs: the rules' car wrecks); this is
// the picture for the burning hulls the fights leave (the rules delete a destroyed vehicle at once, its hull is drawn only). View only.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// A heavy tank (v1 / v2), a Mammoth or a Bizon that drives into a drawn vehicle wreck (a dead vehicle's hull, burning or burnt out, while it
    /// is still drawn) shoves it: the hull is pushed out of the tank's way (along the tank's motion and out to the side it lies on) and turned a
    /// little, eased (no jump), never into the ground; on the first contact sparks, a metal crash (dst_turret_crash, 4 takes) and a burst of the
    /// existing armour chips from the contact point (OzRamImpactLook.Chips); while it grinds on, small sparks, a scrape or a clank (shuttle_skid_scrape /
    /// dst_debris_metal, varied) and a few chips. Effects and sounds only on screen. A wreck still flying (its body not yet at rest) or sinking is
    /// left alone. The rules' own car wrecks (DV193) are crushed by the rules and break as before (HarbourDetails).
    /// Switch <see cref="On"/> (off = the tanks drive through the drawn hulls as before).
    /// </summary>
    [DefaultExecutionOrder(75)]   // after UnitView (60): its death drawing has placed the wreck this frame
    public sealed class OzBigTankWreckShove : MonoBehaviour
    {
        public static bool On = true;
        public static OzBigTankWreckShove I { get; private set; }
        /// <summary>The most a wreck moves in one second (m), the turn per metre pushed (deg), the grind's beat (s).</summary>
        public static float MaxPushMs = 3.5f, TurnPerM = 9f, GrindEvery = 0.4f;

        /// <summary>Counted proof: shoves (frames), first contacts, grinds, metres pushed.</summary>
        public static int Shoves, Contacts, Grinds;
        public static float PushedM;
        public static string Last = "-";

        sealed class W { public UnitView v; public Vector3 off, lastPos; public float yawOff; public Quaternion lastRot; public bool wrote; public float grindAt; public int touchedBy = -1; public bool sided; public int sideBy = -1; public float sideSign = 1f; }   /* 1.5 N30 (kit rammed_wreck_rest_n30): + the side of the first contact */
        readonly Dictionary<int, Vector4> drawnAt = new Dictionary<int, Vector4>();   /* 1.5 N30: each big tank's drawn place on the last frame (x, y, z, frame) */
        /// <summary>1.5 N30 proof: frames the "keeps the offset" pass wrote a wreck again (SteadyShove off only).</summary>
        public static int Reapplied;
        readonly Dictionary<int, W> wrecks = new Dictionary<int, W>();
        readonly List<UnitView> tanks = new List<UnitView>(), dead = new List<UnitView>();
        float scanT;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }

        static void Loaded(Scene s, LoadSceneMode m)
        {
            if (I || (!ZGame.I && !FindFirstObjectByType<ZGame>())) return;
            new GameObject("OzBigTankWreckShove").AddComponent<OzBigTankWreckShove>();
        }

        void Awake() { if (I && I != this) { Destroy(gameObject); return; } I = this; }
        void OnDestroy() { if (I == this) I = null; }

        static bool Crusher(UnitView v) => v && v.Obj != null && !v.Dying && !v.Obj.Destroyed && v.Obj.Kind == ZKind.Vehicle && ZWorld.WreckCrusherType(v.Obj.Type) && ZWorld.BigTankWreckDeviation;

        void LateUpdate()
        {
            if (!On || !ZGame.I) return;
            float dt = Time.deltaTime;
            if ((scanT -= Time.unscaledDeltaTime) <= 0f)
            {   // the lists: every 0.25 s (views come and go slowly)
                scanT = 0.25f; tanks.Clear(); dead.Clear();
                foreach (var v in FindObjectsByType<UnitView>(FindObjectsSortMode.None))
                {
                    if (!v || !v.gameObject.activeInHierarchy || v.Obj == null) continue;
                    if (Crusher(v)) tanks.Add(v);
                    else if (v.Dying && v.IsWreckNow && v.Obj.Kind == ZKind.Vehicle) dead.Add(v);
                }
            }
            // a wreck the death drawing moved this frame keeps the offset it was given
            foreach (var w in wrecks.Values)
            {
                if (!w.v || !w.wrote || OzRammedWreckRest.SteadyShove) continue;   /* 1.5 N30 (kit rammed_wreck_rest_n30): not run: on every frame anything else wrote the wreck it added the WHOLE offset and turn once more (an endless spin); the wreck's place is held by OzRammedWreckRest */
                var t = w.v.transform;
                if ((t.position - w.lastPos).sqrMagnitude > 1e-8f || Quaternion.Angle(t.rotation, w.lastRot) > 0.01f)
                {
                    t.position += w.off; t.rotation = Quaternion.Euler(0f, w.yawOff, 0f) * t.rotation; Reapplied++;   /* 1.5 N30: counted (the check's "before" rows) */
                    w.lastPos = t.position; w.lastRot = t.rotation;
                }
            }
            if (tanks.Count == 0 || dead.Count == 0 || dt <= 0f) return;
            foreach (var tv in tanks)
            {
                if (!tv || !Crusher(tv)) continue;
                var tp = tv.transform.position; var o = tv.Obj;
                var mv = new Vector3(o.Dx, 0f, -o.Dy); float sp = mv.magnitude / 8f;                      // px/s -> m/s
                if (OzRammedWreckRest.SteadyShove && !DrawnMove(tv, dt, ref mv, ref sp)) continue;          /* 1.5 N30: the hull's DRAWN motion (was the rules' speed: it shoved on while the tank stood pressed against the wreck) */
                if (sp < 0.15f) continue;                                                                   // it shoves only while it drives
                mv /= mv.magnitude;
                float rt = ZPathing.Radius(o) / 8f;
                foreach (var dv in dead)
                {
                    if (!dv || !dv.gameObject.activeInHierarchy || !dv.Dying || dv.WreckSinking) continue;
                    var rb = dv.GetComponent<Rigidbody>(); if (rb && !rb.isKinematic) continue;              // still flying: physics has it
                    var dp = dv.transform.position;
                    float rw = Mathf.Max(1.2f, ZPathing.Radius(dv.Obj) / 8f);
                    var d = new Vector3(dp.x - tp.x, 0f, dp.z - tp.z); float dist = d.magnitude;
                    float over = rt + rw - dist;
                    if (over <= 0f || dist < 1e-3f) continue;
                    if (Vector3.Dot(d, mv) < -0.2f * dist) continue;                                         // behind the tank: it is driving away from it
                    if (OzRammedWreckRest.SteadyShove && Vector3.Dot(d, mv) / dist * sp < 0.15f) continue;   /* 1.5 N30: it pushes only while it drives INTO the wreck (the push ends when it stops or turns away) */
                    Shove(tv, dv, d / dist, mv, over, sp, dt);
                }
            }
        }

        void Shove(UnitView tv, UnitView dv, Vector3 n, Vector3 mv, float over, float sp, float dt)
        {
            int key = dv.GetInstanceID();
            if (!wrecks.TryGetValue(key, out var w)) { if (wrecks.Count > 64) Prune(); w = new W { v = dv }; wrecks[key] = w; }
            var t = dv.transform;
            var dir = (n * 0.65f + mv * 0.35f); dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = n; dir.Normalize();
            if (OzRammedWreckRest.SteadyShove)
            {   /* 1.5 N30 (kit rammed_wreck_rest_n30): the side it lies on is taken once, at this tank's first contact (for a wreck dead ahead it flipped from frame to frame: the turn jittered left / right); it is pushed out of the tank's way to that side */
                if (!w.sided || w.sideBy != tv.Obj.Id) { w.sided = true; w.sideBy = tv.Obj.Id; w.sideSign = Vector3.Cross(mv, n).y >= 0f ? 1f : -1f; }
                dir += Vector3.Cross(Vector3.up, mv) * (0.45f * w.sideSign); dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = n; dir.Normalize();
            }
            float step = Mathf.Min(over, MaxPushMs * dt + over * 0.25f);                                    // eased: most of the overlap goes, never a jump
            var push = dir * step;
            float side = OzRammedWreckRest.SteadyShove ? w.sideSign : Vector3.Cross(mv, n).y;                // which side of the tank's line it lies on: it turns away from the tank (1.5 N30: the first contact's side)
            float turn = (side >= 0f ? -1f : 1f) * TurnPerM * step;
            var p = t.position + push;
            var g = ZGame.I; if (g) { float gy = g.GroundY(p.x, p.z); if (p.y < gy - 0.05f) p.y = gy - 0.05f; }   // never into the ground
            t.position = p; t.rotation = Quaternion.Euler(0f, turn, 0f) * t.rotation;
            w.off += push; w.yawOff += turn; w.lastPos = t.position; w.lastRot = t.rotation; w.wrote = true;
            Shoves++; PushedM += step;
            if (OzRammedWreckRest.SteadyShove) OzRammedWreckRest.Shoved(dv);                                /* 1.5 N30: laid on the ground at its new place, then held still */
            var at = Vector3.Lerp(tv.transform.position, t.position, 0.55f) + Vector3.up * 0.6f;
            bool seen = WreckFx.OnScreen(at, 0.1f);
            int id = tv.Obj.Id;
            if (w.touchedBy != id)
            {   // the first contact: sparks, the crash, a burst of chips
                w.touchedBy = id; w.grindAt = Time.time + GrindEvery; Contacts++;
                Last = $"{tv.Obj.Type} #{id} shoves the wreck of {dv.Obj.Type} #{dv.Obj.Id}";
                if (!seen) return;
                OpenZHitSparks.Burst(at, -n + Vector3.up * 0.4f, FxWeapon.MachineGun, FxSurface.Metal, 1.8f, false);
                WreckFx.SparkFountain(at, 14, 1.6f, (uint)(dv.Obj.Id * 977 + id));
                AudioDirector.Fire("dst_turret_crash", at, dv.Obj.Id, 1f, Random.Range(0.8f, 0.95f));
                OzRamImpactLook.Chips(dv.Obj, t, at, tv.transform.position, true);
                return;
            }
            if (Time.time < w.grindAt || sp < 0.3f) return;
            w.grindAt = Time.time + GrindEvery * Random.Range(0.8f, 1.3f); Grinds++;
            if (!seen) return;
            OpenZHitSparks.Burst(at, -n + Vector3.up * 0.3f, FxWeapon.MachineGun, FxSurface.Metal, 1.1f, false);
            AudioDirector.Fire(Random.value < 0.5f ? "shuttle_skid_scrape" : "dst_debris_metal", at, dv.Obj.Id, 0.9f, Random.Range(0.85f, 1.05f));
            OzRamImpactLook.Chips(dv.Obj, t, at, tv.transform.position, false);
        }

        /// <summary>1.5 N30 (kit rammed_wreck_rest_n30): the big tank's drawn ground motion this frame (m/s); false on its first frame seen or while
        /// the rules have it standing.</summary>
        bool DrawnMove(UnitView tv, float dt, ref Vector3 mv, ref float sp)
        {
            var p = tv.transform.position; int key = tv.GetInstanceID(), f = Time.frameCount;
            bool had = drawnAt.TryGetValue(key, out var last) && (int)last.w == f - 1;
            if (drawnAt.Count > 64 && !had) drawnAt.Clear();
            drawnAt[key] = new Vector4(p.x, p.y, p.z, f);
            if (!had || !tv.Obj.IsMoving || dt <= 0f) return false;
            mv = new Vector3(p.x - last.x, 0f, p.z - last.z) / dt; sp = mv.magnitude;
            return true;
        }

        void Prune()
        {
            var gone = new List<int>();
            foreach (var kv in wrecks) if (!kv.Value.v || !kv.Value.v.Dying) gone.Add(kv.Key);
            foreach (var k in gone) wrecks.Remove(k);
        }

        public static string Status() => $"big tank wreck shove {(On ? "on" : "off")}: shoves {Shoves}, contacts {Contacts}, grinds {Grinds}, pushed {PushedM:0.0} m; last: {Last}";
    }
}
