using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The MAMMOTH RAM's look and sound (1.5 DV182, the rule: Sim/ZObject.MammothRam.cs). CombatView hands it the sim's MammothRam events:
    /// <list type="bullet">
    /// <item>START (flag off): the boost: the Mammoth's engine revs up (veh_mammoth_ram_rev at its hull).</item>
    /// <item>IMPACT (flag on): at the spikes the metal crunch + scrape (veh_mammoth_ram), a spark fountain and metal sparks, a short camera
    /// shake (RtsCamera.Shake through OzShakeGate: on screen, the global pause between kicks), the Mammoth's ram line (vo_ram_x_mammoth: "Flatten
    /// them!" / "Pound them hard!" / "Unstoppable!", the player's own Mammoth only, on Alan's main channel, which never talks over another
    /// line); and the rammed vehicle's FLIP (<see cref="OzRamFlip"/>): lifted on the spikes, it tumbles over away from the Mammoth with gravity,
    /// its corners meeting the ground, a bounce and a rock onto its roof, upside down when the rules destroy it (ZObject.RamFlipTicks later).
    /// Then the normal vehicle death plays from that pose (UnitView.Die: the turret toss, the explosion, scorch, fire, and here always the
    /// break into its wreck pieces at once, UnitView.Deaths.cs BeginWreck).</item>
    /// </list>
    /// While it flips, UnitView does not place the hull (<see cref="Holds"/>): the flip owns its transform until the death is drawn.
    /// </summary>
    public static class OzMammothRamView
    {
        static readonly HashSet<ZObject> rammed = new HashSet<ZObject>();
        static readonly Dictionary<ZObject, OzRamFlip> flips = new Dictionary<ZObject, OzRamFlip>();
        static RtsCamera rts;

        /// <summary>The impact's camera kick (RtsCamera.Shake amount; a heavy shell's landing is about 0.8).</summary>
        public const float ShakeAmount = 0.9f;

        // statistics (the Play check)
        public static int Starts, Impacts, Flips, Landings, Lines, Shakes, Released, WreckBreaks;

        /// <summary>UnitView.Update / LateUpdate: this vehicle's hull is placed by its flip now (the rules hold it until its death).</summary>
        public static bool Holds(ZObject o) => (o != null && flips.Count > 0 && flips.TryGetValue(o, out var f) && f && f.Holding) || OzRamPush.Holds(o) || OzRamLever.Holds(o);   // 1.5 DV188 (kit ram_polish): also while pushed (View/OzRamPush.cs); 1.5 rework 14i: while turned over (View/OzRamLever.cs)

        /// <summary>UnitView.Deaths.cs BeginWreck: this vehicle was rammed (it breaks into its pieces at once, no second flip, no cook-off).</summary>
        public static bool Rammed(ZObject o) => o != null && rammed.Count > 0 && rammed.Contains(o);

        /// <summary>BeginWreck's hook (counted for the Play check).</summary>
        public static void NoteWreck() => WreckBreaks++;

        public static void OnEvent(ZGame game, ZEvent e, ZObject mammoth, ZObject target)
        {
            if (game == null || game.World == null || mammoth == null) return;
            var mv = game.Views.TryGetValue(mammoth.Id, out var v) && v ? v : null;
            var mp = mv ? mv.transform.position : game.CenterWorld(mammoth);
            if (!e.flag)
            {   // the start of the ram: the boost
                Starts++;
                OzRamBarrels.NoteStart(mammoth);                                          // 1.5 kit big_tanks_fx: the barrels' clear begins (View/OzRamBarrels.cs)
                AudioDirector.Fire("veh_mammoth_ram_rev", mp, mammoth.Id);
                return;
            }
            Impacts++;
            var hit = ZGame.PxToWorld(e.x, e.y);
            hit.y = game.GroundY(hit.x, hit.z) + 0.9f;
            var dir = target != null ? new Vector3(target.RamDirX, 0f, -target.RamDirY) : (mv ? mv.transform.forward : Vector3.forward);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            AudioDirector.Fire("veh_mammoth_ram", hit, mammoth.Id);
            uint seed = (uint)(mammoth.Id * 7919 + (target != null ? target.Id : 0) * 104729);
            WreckFx.SparkFountain(hit, 28, 2.4f, seed);
            OpenZHitSparks.Burst(hit, -dir, ZWorld.RamPolishDeviation ? FxWeapon.MachineGun : FxWeapon.Cannon /* 1.5 DV188: metal sparks, no blast */, FxSurface.Metal, 1.5f, false);
            if (!rts) rts = Object.FindFirstObjectByType<RtsCamera>();
            if (rts && rts.Shake(ShakeAmount, hit, "mammoth_ram")) Shakes++;
            if (mammoth.Owner == game.player) { AudioDirector.Say("vo_ram_x_mammoth"); Lines++; }
            if (target == null) return;
            if (rammed.Count > 256) { var w = game.World; rammed.RemoveWhere(o => o == null || w.GetAny(o.Id) != o); }
            rammed.Add(target);
            if (!game.Views.TryGetValue(target.Id, out var tv) || !tv) return;
            if (flips.TryGetValue(target, out var old) && old) return;
            if (ZWorld.RamPolishDeviation && OzRamLever.On) { OzRamLever.Begin(game, target, tv, mammoth, dir); Flips++; return; }   // 1.5 rework 14i: the Mammoth's wedge turns it over its far track, then it falls onto its roof (View/OzRamLever.cs)
            if (ZWorld.RamPolishDeviation) { float fw = OpenZFxPlayer.RealFlight(ZObject.RamFlipTicks * (float)ZConst.TICK, game.gameSpeed); OzRamPush.Begin(game, target, tv, mammoth, dir, OzRamBarrels.FlipWait(mammoth, fw)); return; }   // 1.5 DV188 (kit ram_polish): hold until the drawn contact, push, then the flip (View/OzRamPush.cs)
            var f = tv.gameObject.AddComponent<OzRamFlip>();
            { float fs = OpenZFxPlayer.RealFlight(ZObject.RamFlipTicks * (float)ZConst.TICK, game.gameSpeed); f.Begin(game, target, tv, dir, fs, OzRamBarrels.FlipWait(mammoth, fs)); }   // 1.5 kit big_tanks_fx: the heave waits until the barrels are clear (View/OzRamBarrels.cs)
            flips[target] = f;
            Flips++;
        }

        internal static void Forget(ZObject o) { if (o != null) flips.Remove(o); }
        internal static void Adopt(ZObject o, OzRamFlip f) { if (o == null || !f) return; flips[o] = f; Flips++; }   // 1.5 DV188 (kit ram_polish): the flip OzRamPush starts
    }

    /// <summary>
    /// One rammed vehicle's flip (view only; the rules hold the vehicle meanwhile). A rigid box of the hull's own size, its centre in the middle
    /// of the hull: launched up and away from the Mammoth, turning over about the axis across the ram (the near side, where the spikes bite,
    /// goes up), gravity pulling it down; the lowest point of the turning box meets the ground (game.GroundY, an open water surface where there
    /// is one), it bounces a little, slides to a stop and rocks onto its roof with a damped spring. Timed so it lies upside down when the rules
    /// destroy it; when the hull's death is drawn (UnitView.Dying) the pose stays and the normal wreck takes over from it.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class OzRamFlip : MonoBehaviour
    {
        ZGame game; ZObject obj; UnitView view;
        Vector3 p0, c0, axis, c, vel; Quaternion r0;
        float h, len, g, theta, omega, t, total, flight; int contacts; bool landedSound;
        float flipWait; Vector3 flipDir;                                                    // 1.5 kit big_tanks_fx: the heave's wait (Begin)
        readonly List<Renderer> shadows = new List<Renderer>();

        /// <summary>The flip owns the hull's transform (until the drawn death, or a safety end 2.5 s after the planned one).</summary>
        public bool Holding { get; private set; }
        /// <summary>1.5 DV188 (kit ram_polish): the rules' roll (m); negative = the flip's own travel.</summary>
        public float TravelOverride = -1f;

        /// <summary>Its first ground contact (the Play check).</summary>
        public float LandedAt { get; private set; } = -1f;

        public void Begin(ZGame g0, ZObject o, UnitView v, Vector3 dir, float seconds, float wait = 0f /* 1.5 kit big_tanks_fx */)
        {
            game = g0; obj = o; view = v; total = Mathf.Max(0.4f, seconds - Mathf.Max(0f, wait)); if (wait > 0f) { flipWait = wait; flipDir = dir; Holding = true; return; }   // 1.5 kit big_tanks_fx: held on the spikes until the Mammoth's barrels are clear, then the heave (shortened by the wait: it still lands before the rules' death)
            p0 = transform.position; r0 = transform.rotation;
            float ground = game.GroundY(p0.x, p0.z);
            var b = Bounds(dir, out len);
            h = Mathf.Clamp(b.max.y - ground, 0.8f, 4f);
            c0 = new Vector3(p0.x, ground + h * 0.5f, p0.z);
            axis = Vector3.Cross(Vector3.up, dir).normalized;
            flight = 0.72f * total;
            float apex = Mathf.Clamp(0.9f + 0.35f * h, 1.0f, 2.2f);                  // the centre's rise
            g = 8f * apex / (flight * flight);                                          // the fall that brings it down in the flight time
            float travel = TravelOverride > 0f ? TravelOverride : Mathf.Clamp(0.55f * len + 0.6f, 1.0f, 3.2f);   // 1.5 DV188: the rules' roll
            vel = dir * (travel / flight) + Vector3.up * (4f * apex / flight);
            omega = (Mathf.PI + 0.12f) / flight;                                         // a little past the roof, the rock brings it back
            theta = 0f; t = 0f; c = c0; contacts = 0;
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.enabled && r.name.ToLowerInvariant().Contains("shadow")) { r.enabled = false; shadows.Add(r); }   // a blob shadow quad would turn over with it
            if (FxManager.I) FxManager.I.Dust(new Vector3(p0.x, ground + 0.2f, p0.z) - dir * 0.8f, 0.7f);
            AudioDirector.Fire("dst_ragdoll_whoosh", c0, o.Id, 1.2f, 0.62f);              // the hull heaved off the ground
            Holding = true;
            Place();
        }

        Bounds Bounds(Vector3 dir, out float along)
        {
            bool any = false; var b = new Bounds(transform.position + Vector3.up, Vector3.one);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var nm = r.name.ToLowerInvariant();
                if (nm.Contains("shadow") || nm.Contains("label") || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var e = b.extents;
            along = Mathf.Clamp(2f * (Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.z) * e.z), 1.5f, 8f);
            return b;
        }

        float FloorAt(Vector3 p)
        {
            float y = game.GroundY(p.x, p.z);
            if (OzWaterLanding.OpenWaterAt(new Vector3(p.x, y, p.z), y, out float wy)) y = wy;
            return y;
        }

        void LateUpdate()
        {
            if (!Holding) return;
            if (obj == null || game == null || game.World == null || !view || view.Dying || t > total + 2.5f)
            {   // the death is drawn (it takes over from this pose), or the vehicle is gone, or the safety end
                if (t > total + 2.5f) { OzMammothRamView.Released++; Debug.LogWarning("[OpenZ] ram flip: no death drawn 2.5 s after the planned one; the hull goes back to its rules place"); }
                End();
                return;
            }
            if (flipWait > 0f) { flipWait -= Time.deltaTime; if (flipWait > 0f) return; flipWait = 0f; Begin(game, obj, view, flipDir, total, 0f); return; }   // 1.5 kit big_tanks_fx: the wait is over: the heave
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 240f)));
            float s = dt / n;
            for (int i = 0; i < n; i++) Step(s);
            t += dt;
            Place();
        }

        void Step(float dt)
        {
            vel += Vector3.down * g * dt;
            c += vel * dt;
            theta += omega * dt;
            // the lowest point of the turning box (its height h, its length along the ram len) meets the floor
            float half = Mathf.Abs(Mathf.Cos(theta)) * h * 0.5f + Mathf.Abs(Mathf.Sin(theta)) * len * 0.5f;
            float floor = FloorAt(c) + half;
            bool onGround = c.y <= floor + 1e-3f;
            if (c.y < floor)
            {
                c.y = floor;
                if (vel.y < 0f)
                {
                    float hitSpeed = -vel.y;
                    if (contacts == 0) { LandedAt = t; OzMammothRamView.Landings++; }
                    contacts++;
                    vel.y = hitSpeed > 1.2f ? hitSpeed * 0.22f : 0f;                     // a small bounce, then it lies
                    vel.x *= 0.45f; vel.z *= 0.45f;
                    if (!landedSound)                                                         // Unity pass 9 Nov: the turning box touches the floor while still rising, so the first counted contact is slow: the crash plays at the first landing whatever its speed
                    {
                        landedSound = true;
                        AudioDirector.Fire("dst_turret_crash", c, obj.Id, Mathf.Lerp(0.75f, 1.15f, Mathf.InverseLerp(0.2f, 2.5f, hitSpeed)), 0.8f);   // the hull crashes down on its roof
                        if (FxManager.I) FxManager.I.Dust(new Vector3(c.x, floor - half + 0.2f, c.z), 1.0f);
                    }
                }
            }
            if (contacts > 0)
            {   // on the ground: friction, and the rock onto its roof (a damped spring toward upside down)
                if (onGround) { float k = Mathf.Exp(-6f * dt); vel.x *= k; vel.z *= k; }
                omega += (-70f * (theta - Mathf.PI) - 10f * omega) * dt;
            }
        }

        void Place()
        {
            var q = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axis);
            transform.SetPositionAndRotation(c + q * (p0 - c0), q * r0);
        }

        void End()
        {
            Holding = false;
            OzMammothRamView.Forget(obj);
            Destroy(this);
        }

        void OnDestroy() { if (Holding) { Holding = false; OzMammothRamView.Forget(obj); } }
    }
}
