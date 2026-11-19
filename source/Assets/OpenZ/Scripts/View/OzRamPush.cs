using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ram_polish (DV188, the rule: Sim/ZObject.RamPolish.cs): the Mammoth ram's CONTACT and PUSH before the flip (view only).
    /// <list type="bullet">
    /// <item>HOLD: from the impact event the rammed hull stays where it stands (UnitView does not place it: <see cref="Holds"/>) until the
    /// Mammoth's DRAWN front (its hull and spikes, the turret and barrels left out: their local renderer boxes) meets the target's drawn hull
    /// along the ram.</item>
    /// <item>PUSH: from then on the target's hull is moved along the ram so it stays against the spikes while the Mammoth drives on (the rules'
    /// push, ZObject.RamPushPx), its tracks scraping: a shower of bright metal sparks at the contact and bursts every
    /// <see cref="SparkEvery"/> s while it moves. No explosion, no fire, no big smoke.</item>
    /// <item>HEAVE: at the rules' heave tick (and after the barrels' clear, OzRamBarrels.FlipWait) the flip (OzRamFlip) starts from the pushed
    /// pose, its travel the rules' roll (ZObject.RamRollPx, so the hull lands where the rules' sweep squashes the robots behind it), timed to
    /// end at the rules' kill: scrape sparks where it grinds until it lands. The death explosion is the normal one at the kill, after the
    /// landing.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(999)]
    public class OzRamPush : MonoBehaviour
    {
        static readonly Dictionary<ZObject, OzRamPush> live = new Dictionary<ZObject, OzRamPush>();
        /// <summary>A burst of metal sparks every this many seconds while the hull moves.</summary>
        public const float SparkEvery = 0.09f;
        /// <summary>The longest push drawn (m).</summary>
        public const float MaxPush = 1.3f;
        // statistics (the Play check): pushes, sparks bursts, heaves, the drawn gap at the heave by target type (m, worst), the push drawn
        public static int Pushes, Bursts, Heaves, ForcedHeaves;
        /// <summary>1.5 pass 5 (pass 4 Play stats: push drawn 0.00 m, gap at the heave 0.16 m; UE 10 Nov frame log: drawn gap 0.80 m at the
        /// rules' impact, 0.26 m when the 5 px push drive ended, push drawn 0): the rules' contact (vehicle_lights boxes) fires before the drawn
        /// hulls touch and the push drive (0.63 m) never closed it, so the spikes never met the hull. LUNGE: from the impact the Mammoth's
        /// DRAWN hull surges ahead of its rules' place along the ram (at most <see cref="LungeMax"/> m at <see cref="LungeRate"/> m/s) until
        /// its spikes touch the target; the rules' push drive then shoves the drawn target along (the push). After the heave the lead is never
        /// drawn backwards: the Mammoth's own driving on eats it, or it eases off at <see cref="LungeEase"/> m/s once it has stood
        /// <see cref="LungeHold"/> s (View/OzRamPush.cs OzRamLunge).</summary>
        public static float LungeMax = 1.2f, LungeRate = 6f, LungeHold = 2.5f, LungeEase = 0.3f;
        public static int Lunges; public static float LungeMaxSeen;
        public static readonly Dictionary<string, float> GapAtHeave = new Dictionary<string, float>(), PushDrawn = new Dictionary<string, float>();

        ZGame game; ZObject obj, mammoth; UnitView view; Transform mt;
        Vector3 p0, dir; float offset, waitUntil, nextSpark, lastMove; bool touched; OzRamFlip flip; uint seed;
        OzRamLunge lunge;                                                                            // 1.5 pass 5: the Mammoth's drawn lead (OzRamLunge below)
        readonly List<Renderer> mr = new List<Renderer>(), tr = new List<Renderer>();

        public static bool Holds(ZObject o) => o != null && live.Count > 0 && live.TryGetValue(o, out var p) && p && p.flip == null;

        /// <summary>OzMammothRamView.OnEvent (DV188 on): the push owns the hull from the impact.</summary>
        public static void Begin(ZGame g, ZObject target, UnitView tv, ZObject m, Vector3 d, float barrelsWait)
        {
            if (live.TryGetValue(target, out var old) && old) return;
            var p = tv.gameObject.AddComponent<OzRamPush>();
            p.game = g; p.obj = target; p.view = tv; p.mammoth = m; p.dir = d; p.p0 = tv.transform.position;
            p.mt = g.Views.TryGetValue(m.Id, out var mv) && mv ? mv.transform : null;
            p.waitUntil = Time.time + Mathf.Max(0f, barrelsWait);
            p.seed = (uint)(m.Id * 7919 + target.Id * 104729);
            if (p.mt) Collect(p.mt, p.mr, true);
            if (p.mt) { p.lunge = p.mt.GetComponent<OzRamLunge>() ?? p.mt.gameObject.AddComponent<OzRamLunge>(); p.lunge.Arm(d); Lunges++; }   // 1.5 pass 5
            Collect(tv.transform, p.tr, false);
            live[target] = p; Pushes++;
        }

        static void Collect(Transform root, List<Renderer> into, bool hullOnly)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) continue;
                bool skip = false;
                for (var t = r.transform; t && t != root.parent; t = t.parent)
                {
                    var n = t.name.ToLowerInvariant();
                    if (n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring")) { skip = true; break; }
                    if (hullOnly && (n.Contains("turret") || n.Contains("barrel") || n.Contains("mantlet") || n.Contains("recoil") || n.Contains("antenn") || n.Contains("hatch") || n.Contains("lamp") || n.Contains("light"))) { skip = true; break; }
                }
                if (!skip) into.Add(r);
            }
        }

        /// <summary>The extreme of the renderers' local boxes along d from o (max = the front reach, else the near face).</summary>
        static float Reach(List<Renderer> rs, Vector3 o, Vector3 d, bool max)
        {
            float best = max ? float.MinValue : float.MaxValue; bool any = false;
            foreach (var r in rs)
            {
                if (!r) continue;
                var b = r.localBounds; var t = r.transform;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float v = Vector3.Dot(t.TransformPoint(c) - o, d);
                    if (max ? v > best : v < best) best = v; any = true;
                }
            }
            return any ? best : (max ? 0f : 1e3f);
        }

        float Gap()
        {
            if (!mt) return 0f;
            var o = mt.position;
            return Reach(tr, o, dir, false) - Reach(mr, o, dir, true);
        }

        void Spark(Vector3 at, float k)
        {
            WreckFx.SparkFountain(at, Mathf.RoundToInt(14 * k), 1.6f * k, seed++);
            OpenZHitSparks.Burst(at, -dir, FxWeapon.MachineGun, FxSurface.Metal, 1.1f * k, false);
            Bursts++;
        }

        void LateUpdate()
        {
            var w = game != null ? game.World : null;
            if (obj == null || w == null || !view || view.Dying || obj.Destroyed) { End(); return; }
            if (flip == null)
            {   // the hold and the push: the hull against the drawn spikes
                transform.position = p0 + dir * offset;
                float gap = Gap();
                if (gap > 0f && lunge && !touched)
                {   // 1.5 pass 5: the Mammoth's drawn hull surges on until its spikes touch (the rules' contact came before the drawn hulls met)
                    float add = lunge.Grow(Mathf.Min(gap, LungeRate * Time.deltaTime));
                    if (add > 0f) { if (lunge.Lead > LungeMaxSeen) LungeMaxSeen = lunge.Lead; gap = Gap(); }
                }
                if (gap < 0f) { float add = Mathf.Min(-gap, MaxPush - offset); if (add > 1e-4f) { offset += add; lastMove = Time.time; transform.position = p0 + dir * offset; gap += add; } }
                bool contact = gap <= 0.1f;
                var at = (mt ? mt.position : p0) + dir * (mt ? Reach(mr, mt.position, dir, true) : 0f); at.y = game.GroundY(at.x, at.z) + 0.8f;
                if (contact && !touched) { touched = true; Spark(at, 1.4f); nextSpark = Time.time + SparkEvery; }
                else if (touched && Time.time >= nextSpark && Time.time - lastMove < 0.12f) { Spark(at, 0.7f); nextSpark = Time.time + SparkEvery; }
                bool heaveTick = obj.RamHeaveTick >= 0 && w.TickIndex >= obj.RamHeaveTick;
                bool late = obj.RamDoomTick >= 0 && obj.RamDoomTick - w.TickIndex <= ZObject.RamFlipTicks * 3 / 4;   // safety: the kill is near (the push was cut)
                if (!((heaveTick && Time.time >= waitUntil) || late)) return;
                if (late && !heaveTick) ForcedHeaves++;
                string ty = obj.Type ?? "?";
                if (!GapAtHeave.TryGetValue(ty, out var g0) || gap > g0) GapAtHeave[ty] = gap;
                if (!PushDrawn.TryGetValue(ty, out var p1) || offset < p1) PushDrawn[ty] = offset;
                long left = obj.RamDoomTick - w.TickIndex; if (left < 10) left = 10;
                float secs = OpenZFxPlayer.RealFlight(left * (float)ZConst.TICK, game.gameSpeed);
                if (lunge) lunge.Release();                                                           // 1.5 pass 5: from the heave the lead runs out (never drawn backwards)
                flip = gameObject.AddComponent<OzRamFlip>();
                flip.TravelOverride = obj.RamRollPx > 0 ? obj.RamRollPx / 8f : -1f;
                flip.Begin(game, obj, view, dir, secs, 0f);
                OzMammothRamView.Adopt(obj, flip);
                Heaves++; nextSpark = Time.time;
                return;
            }
            if (!flip || flip.LandedAt >= 0f) { if (flip) { var p = transform.position; p.y = game.GroundY(p.x, p.z) + 0.3f; Spark(p, 0.9f); } End(); return; }
            if (Time.time >= nextSpark)
            {   // it grinds over its edge: sparks where it touches
                var p = transform.position - dir * 0.6f; p.y = game.GroundY(p.x, p.z) + 0.25f;
                Spark(p, 0.6f); nextSpark = Time.time + SparkEvery * 1.4f;
            }
        }

        void End() { live.Remove(obj); if (lunge) lunge.Release(); Destroy(this); }
        void OnDestroy() { if (obj != null && live.TryGetValue(obj, out var p) && p == this) live.Remove(obj); if (lunge) lunge.Release(); }
    }

    /// <summary>
    /// 1.5 pass 5: the Mammoth's DRAWN lead along the ram (view only, on the Mammoth's view; added by OzRamPush.Begin). UnitView places the
    /// hull from the rules in Update; this moves it on by <see cref="Lead"/> m along the ram every LateUpdate (order 998: before OzRamPush
    /// measures the gap, before the big-tank lamps / neon follow the hull). Armed: the lead only grows (OzRamPush.Grow, until the spikes
    /// touch). Released (the heave or the ram's end): never drawn backwards: whatever the rules' Mammoth drives on along the ram eats the lead
    /// (any other move eats it too, hidden in the motion); standing still it eases off at OzRamPush.LungeEase m/s after OzRamPush.LungeHold s.
    /// </summary>
    [DefaultExecutionOrder(998)]
    public class OzRamLunge : MonoBehaviour
    {
        Vector3 dir; float lead, releasedAt = -1f; Vector3 lastSim; bool hasLast; Vector3 appliedPos; float appliedLead; bool applied;
        public float Lead => lead;
        /// <summary>1.5 N26 (kit mammoth_pitch_n26): the drawn lead as a world offset (the planted gear reads the ground there: View/OzBigTankPitch.cs).</summary>
        public Vector3 Offset => dir * lead;

        public void Arm(Vector3 d) { dir = new Vector3(d.x, 0f, d.z).normalized; releasedAt = -1f; enabled = true; }
        public void Release() { if (releasedAt < 0f) releasedAt = Time.time; }

        /// <summary>Adds up to `want` m of lead now (capped by OzRamPush.LungeMax); moves the hull at once; returns what was added.</summary>
        public float Grow(float want)
        {
            if (releasedAt >= 0f || want <= 0f) return 0f;
            float add = Mathf.Min(want, OzRamPush.LungeMax - lead); if (add <= 0f) return 0f;
            lead += add; transform.position += dir * add;
            appliedPos = transform.position; appliedLead += add; applied = true;
            return add;
        }

        void LateUpdate()
        {
            var cur = transform.position;                                                            // where UnitView put it this frame (the rules' place)
            var sim = applied && (cur - appliedPos).sqrMagnitude < 1e-10f ? cur - dir * appliedLead : cur;   // not placed again this frame: take our lead off first
            if (releasedAt >= 0f && hasLast)
            {
                var mv = sim - lastSim; mv.y = 0f;
                float along = Vector3.Dot(mv, dir);
                float eat = along > 0f ? along : mv.magnitude;
                lead = Mathf.Max(0f, lead - eat);
                if (eat < 1e-4f && Time.time - releasedAt > OzRamPush.LungeHold) lead = Mathf.MoveTowards(lead, 0f, OzRamPush.LungeEase * Time.deltaTime);
            }
            lastSim = sim; hasLast = true;
            if (lead <= 1e-4f && releasedAt >= 0f) { lead = 0f; transform.position = sim; Destroy(this); return; }
            transform.position = sim + dir * lead;
            appliedPos = transform.position; appliedLead = lead; applied = true;
        }
    }
}
