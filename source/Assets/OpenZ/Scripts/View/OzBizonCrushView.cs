using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The BIZON CRUSH's look and sound (1.5 DV183, the rule: Sim/ZObject.BizonCrush.cs). CombatView hands it the sim's BizonCrush events:
    /// <list type="bullet">
    /// <item>START (flag off): the boost: the Bizon's diesel revs up (veh_bizon_crush_rev at its hull; its exhaust bursts by itself on the speed-up).</item>
    /// <item>CONTACT (flag on): at its front the metal crumpling and grinding under its tracks (veh_bizon_crush), metal sparks and dust, a short
    /// camera shake (RtsCamera.Shake through its own gate: on screen only), the Bizon's crush line (vo_crush_x_bizon, the player's own Bizon only);
    /// then <see cref="OzCrushSquash"/> on the target and <see cref="OzCrushRide"/> on the Bizon.</item>
    /// </list>
    /// The target is
    /// no longer scaled flat. Its own meshes are dented where the Bizon's tracks and belly really press them, progressively along the Bizon's
    /// path (<see cref="OzCrushSquash"/>); its gun bends down at the mantlet instead of sinking in; its turret stays on until the Bizon reaches
    /// it, then it is torn off, shoved ahead, tips off and falls; the Bizon rides the crushed hull through eased springs
    /// (<see cref="OzCrushRide"/>). The crushed hull stays the dented wreck (no flat pieces).
    /// </summary>
    public static class OzBizonCrushView
    {
        /// <summary>The contact's camera kick (the Mammoth's ram is 0.9).</summary>
        public const float ShakeAmount = 0.6f;
        /// <summary>Before kit bizon_crush_v3: the squashed target kept this much of its height (the v3 crush dents the meshes instead; kept for
        /// old references only).</summary>
        public const float FlatHeight = 0.25f;

        static RtsCamera rts;

        // statistics (the Play check)
        public static int Starts, Contacts, Lines, Shakes, Squashes, Climbs, WreckBreaks;

        /// <summary>UnitView.Deaths.cs: this unit was crushed by a Bizon (its own death look).</summary>
        public static bool Crushed(ZObject o) => o != null && o.RamCrushed;

        /// <summary>UnitView.BreakApart: a crushed wreck's pieces are thrown low (they lie under the tracks).</summary>
        public static float Power(ZObject o, float normal) => Crushed(o) ? 0.3f : normal;

        /// <summary>BeginWreck's hook (counted for the Play check).</summary>
        public static void NoteWreck() => WreckBreaks++;

        /// <summary>1.5 kit bizon_crush_v3: UnitView.BeginWreck of a crushed unit: when its hull was dented by the crush it stays that dented,
        /// burning wreck (-1: no break into pieces; it burns, smoulders and sinks like every wreck); else (nothing could be dented) the break
        /// at once as before (0.05 s).</summary>
        public static float CrushedBreakAt(UnitView v) => OzCrushSquash.KeepsWreck(v) ? -1f : 0.05f;

        /// <summary>1.5 kit bizon_crush_v3: UnitView.BeginWreck: a crushed unit whose hull was dented: the wheeled tear-off (parts thrown up)
        /// and the tank break-up cut are skipped (it stays the dented wreck; the crush broke its parts off itself).</summary>
        public static bool KeepsDentedWreck(UnitView v) => v && Crushed(v.Obj) && OzCrushSquash.KeepsWreck(v);

        /// <summary>Metres per sim pixel (ZGame.PxToWorld).</summary>
        public static float M(float px) => px / 8f;

        public static void OnEvent(ZGame game, ZEvent e, ZObject bizon, ZObject target)
        {
            if (game == null || game.World == null || bizon == null) return;
            var bv = game.Views.TryGetValue(bizon.Id, out var v) && v ? v : null;
            var bp = bv ? bv.transform.position : game.CenterWorld(bizon);
            if (!e.flag)
            {   // the start: the boost
                Starts++;
                AudioDirector.Fire("veh_bizon_crush_rev", bp, bizon.Id);
                return;
            }
            Contacts++;
            var hit = ZGame.PxToWorld(e.x, e.y);
            hit.y = game.GroundY(hit.x, hit.z) + 0.5f;
            var dir = target != null ? new Vector3(target.RamDirX, 0f, -target.RamDirY) : (bv ? bv.transform.forward : Vector3.forward);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            AudioDirector.Fire("veh_bizon_crush", hit, bizon.Id);
            OpenZHitSparks.Burst(hit, -dir, FxWeapon.Cannon, FxSurface.Metal, 1.1f, false);
            if (FxManager.I) FxManager.I.Dust(new Vector3(hit.x, hit.y - 0.3f, hit.z), 0.8f);
            if (!rts) rts = Object.FindFirstObjectByType<RtsCamera>();
            if (rts && rts.Shake(ShakeAmount, hit, "bizon_crush")) Shakes++;
            if (bizon.Owner == game.player) { AudioDirector.Say("vo_crush_x_bizon"); Lines++; }
            if (target == null) return;
            UnitView tv = game.Views.TryGetValue(target.Id, out var t0) && t0 ? t0 : null;
            OzCrushSquash sq = null;
            if (tv)
            {
                sq = tv.GetComponent<OzCrushSquash>();
                if (!sq) { sq = tv.gameObject.AddComponent<OzCrushSquash>(); sq.Begin(game, target, tv, bizon, bv); Squashes++; }
            }
            if (bv && tv && OzCrushRide.On)
            {
                var oldRide = bv.GetComponent<OzCrushRide>(); if (oldRide) { oldRide.RestoreNow(); Object.Destroy(oldRide); }
                var ride = bv.gameObject.AddComponent<OzCrushRide>();
                ride.Begin(game, bizon, bv, target, tv, sq);                                         // binds the target's crush (OzCrushSquash.Bind)
                Climbs++;
            }
        }

        /// <summary>The model node of a unit view (its "Model" child, else the view itself).</summary>
        internal static Transform ModelOf(UnitView v) { var m = v ? v.transform.Find("Model") : null; return m ? m : v ? v.transform : null; }

        /// <summary>The height of a view's visible hull above the ground (m).</summary>
        internal static float HullHeight(UnitView v, float ground)
        {
            float top = ground + 1f; bool any = false;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var n = r.name.ToLowerInvariant();
                if (n.Contains("shadow") || n.Contains("label") || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450)) continue;
                top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y; any = true;
            }
            return Mathf.Clamp(top - ground, 0.6f, 4f);
        }
    }
}
