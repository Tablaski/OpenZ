using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV152, level 7 "Pyro Technics": a robot killed by lava (ZObject.LavaKilled) melts into it. Built on Zod's own melt (UnitView Death.Melt:
    /// the body flattens over the 17 frames x 0.16 s of erobotdeath.cpp), with what lava adds: the knees give (the death_side clip, slowed),
    /// dies to a dark crust as the lava closes over it. Little fire puffs and one light at the start, a last puff when it is gone.
    /// Kid-friendly: glow, sag and sinking only; no parts, no gore. Its sounds: the fire death cry (AudioDirector, the Death event's fire
    /// flag) and the lava melt gloop (View/OzLava.cs, LavaStep / Death). View only; the sim removed the robot on its death tick.
    /// </summary>
    public partial class UnitView
    {
        /// <summary>Seconds of the melt (Zod's melt is 2.7 s; the sink needs a little longer), how deep it sinks (m), how far it spreads.</summary>
        public const float LavaMeltSeconds = 3.2f, LavaMeltSink = 0.9f, LavaMeltSpread = 1.35f;
        float lavaMeltPuffT, lavaMeltPopT, lavaMeltH, lavaMeltTop0; int lavaMeltPuffs, lavaMeltPops; uint lavaMeltRng;
        /// <summary>Heat at the start, the lava's height, the height share the body flattens to, how far it then goes down; it is under at UnderK.</summary>
        float lavaMeltHeat0, lavaMeltY, lavaMeltFlat, lavaMeltDown; bool lavaMeltUnder; Transform lavaHips;
        const float UnderK = 0.95f;

        void BeginLavaMelt(uint fh, bool animated = true)
        {
            if (animated && anim) { anim.speed = 0.5f; anim.CrossFadeInFixedTime("death_side", 0.15f); }
            lavaMeltHeat0 = paint ? paint.Heat : 1f;                                                 // a robot shot dead in lava starts from the heat it has
            float y0 = transform.position.y, h = RobotHeight();
            lavaMeltY = OzLava.I ? OzLava.I.lavaY : y0 + LavaMeltSink;
            lavaMeltFlat = Mathf.Clamp((lavaMeltY - y0 + 0.12f) / h, 0.10f, 1f);                     // it starts sunk to the shins: a low heap stands a little over the lava,
            lavaMeltDown = Mathf.Max(0.16f, y0 + h * lavaMeltFlat - lavaMeltY + 0.04f);              // then goes under
            lavaMeltUnder = false;
            lavaMeltPuffT = 0f; lavaMeltPuffs = 0;
            lavaMeltH = h; lavaMeltRng = fh * 2654435761u | 1u; lavaMeltPops = 3 + (int)((fh >> 4) % 3u); lavaMeltPopT = 0.15f + 0.3f * WreckFx.Rnd(ref lavaMeltRng);   // 3 to 5 small bubbles at uneven gaps
            lavaMeltTop0 = OzLavaWade.BandShare * h * 0.4f;                                         // the band it has now, the shins at the least: no drop on the death frame
            if (animated && OzLavaWade.I && OzLavaWade.I.BandTop(this, out float band)) lavaMeltTop0 = Mathf.Max(lavaMeltTop0, band - lavaMeltY);   // a body thrown in from the bank starts at the shins: its band was left on the bank
            var fx = FxManager.I;
            if (fx) { fx.Burn(transform.position + Vector3.up * 0.2f, 0.5f); fx.AddLight(transform.position + Vector3.up * 1.0f, new Color(1f, 0.42f, 0.12f), 9f, 7f, LavaMeltSeconds, 0.35f); }
            OzDeathTrace.Mode(this, "lava melt " + (fh % 4u));
        }

        void UpdateLavaMelt(float dt)
        {
            float k = Mathf.Clamp01(deathT / LavaMeltSeconds);
            float flat = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.15f));                        // the body flattens first,
            float sink = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.70f) / (UnderK - 0.70f)));    // then runs down into the lava: measured from the lava line, not the feet
            float spread = Mathf.Lerp(1f, LavaMeltSpread, flat);
            transform.localScale = OzRobotScale.Dying(this, new Vector3(spread, Mathf.Lerp(1f, lavaMeltFlat, flat), spread));   // 1.5 R3.25 (View/OzRobotScale.cs): from its drawn size (the same vector at 100 %)
            var p = deathFrom; p.y -= sink * lavaMeltDown; transform.position = p;
            if (paint)
            {
                float red0 = lavaMeltHeat0 * (1f - 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.3f)));                // 1.3 row 17 tail: the body's own flat red gives way to the band's hot-metal ramp over the first second (to 40 %), so the climbing glow never reads pale
                paint.Heat = k < 0.7f ? red0 : Mathf.Lerp(red0, 0f, Mathf.SmoothStep(0f, 1f, (k - 0.7f) / 0.3f));   // it never rises: the crust closes over it, the glow is the legs' ramp below
                if (k > 0.6f) { float c = Mathf.Floor((k - 0.6f) / 0.4f * 0.7f / 0.05f) * 0.05f; if (c > paint.Charred) paint.Charred = c; }   // Charred repaints: steps of 0.05, never every frame
            }
            // the wading ramp, its line on the lava, its top climbing from the band it had (the shins at the least) to the top of the body over the first 70 %: the head lights last, just before he goes under
            float bodyTop = p.y + lavaMeltH * OzRobotScale.Unscaled(this, transform.localScale.y) - lavaMeltY;   // 1.5 R3.25 (View/OzRobotScale.cs): lavaMeltH is already the drawn height (the same number at 100 %)
            if (OzLavaWade.I) OzLavaWade.I.Melting(this, lavaMeltY, Mathf.Lerp(lavaMeltTop0, Mathf.Max(0.2f, bodyTop * 1.1f), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.7f))));
            if (lavaMeltPops > 0 && k < UnderK && (lavaMeltPopT -= dt) <= 0f)
            {
                float a = WreckFx.Rnd(ref lavaMeltRng) * 6.283f, r = 0.45f + 0.5f * WreckFx.Rnd(ref lavaMeltRng);
                var at = new Vector3(deathFrom.x + Mathf.Cos(a) * r, lavaMeltY, deathFrom.z + Mathf.Sin(a) * r);
                if (!OzLava.SurfaceAt(at, out _)) at = new Vector3(deathFrom.x, lavaMeltY, deathFrom.z);
                if (OzLavaMelt.RobotBubble(at)) { lavaMeltPops--; lavaMeltPopT = 0.4f + 0.4f * WreckFx.Rnd(ref lavaMeltRng); } else lavaMeltPopT = 0.1f;
            }
            lavaMeltPuffT -= dt;
            if (lavaMeltPuffT <= 0f && lavaMeltPuffs < 4 && FxManager.I)
            {
                lavaMeltPuffT = 0.6f; lavaMeltPuffs++;
                FxManager.I.Burn(new Vector3(deathFrom.x, lavaMeltY + 0.15f, deathFrom.z), 0.25f + 0.08f * lavaMeltPuffs);
            }
            if (!lavaMeltUnder && k >= UnderK) { lavaMeltUnder = true; LavaMeltGone(); }
            if (k >= 1f) { if (FxManager.I) FxManager.I.Burn(new Vector3(deathFrom.x, lavaMeltY, deathFrom.z), 0.35f); if (OzLavaWade.I) OzLavaWade.I.MeltDone(this); Hide(); }
        }

        /// <summary>The top went under: a smoke column and a last big bubble.</summary>
        void LavaMeltGone()
        {
            var at = new Vector3(deathFrom.x, lavaMeltY, deathFrom.z);
            var fx = OpenZFxPlayer.I;
            if (fx && fx.isActiveAndEnabled)
                for (int i = 0; i < 6; i++) fx.LavaSmoke(at + Vector3.up * (0.2f + i * 0.45f), 0.8f + i * 0.2f);
            if (OzLava.I) OzLava.I.PopAt(at, true);
        }

        /// <summary>A thrown body came down on lava: it melts where it landed. False = no lava here, or it is still over it.</summary>
        bool LavaLanding(Vector3 p)
        {
            if (!OzLava.SurfaceAt(p, out float ly) || p.y > ly + 0.15f) return false;
            StartBodyMelt(new Vector3(p.x, ly - 0.2f, p.z), ly, false);                               // it lies flat: its flight pose stays, a stand-up clip would lift it
            lavaMeltDown = Mathf.Max(lavaMeltDown, 0.75f);                                            // lying, the melt does not flatten it: it has to go down this far to be under
            return true;
        }

        /// <summary>A ragdoll came to the lava: frozen, the root brought under it (the melt scales about the root), and it melts. Each frame of a ragdoll, at once false with no lava.</summary>
        bool RagdollInLava()
        {
            if (!OzLava.I || !ragdoll || popping || deathT < 0.2f) return false;
            if (!lavaHips) { var rb = GetComponentInChildren<Rigidbody>(); lavaHips = rb ? rb.transform : transform; }
            var p = lavaHips.position;
            if (!OzLava.SurfaceAt(p, out float ly) || p.y > ly + 0.4f || game.GroundY(p.x, p.z) > ly - 0.05f) return false;
            ragdoll.Freeze();
            if (TryGetComponent<RagdollFlail>(out var flail)) Destroy(flail);
            var at = new Vector3(p.x, ly - 0.2f, p.z); var shift = at - transform.position;
            var top = lavaHips; while (top != transform && top.parent && top.parent != transform) top = top.parent;
            transform.position = at; if (top != transform) top.position -= shift;               // the corpse stays where it lies
            StartBodyMelt(at, ly, false);
            return true;
        }

        void StartBodyMelt(Vector3 at, float ly, bool animated)
        {
            death = Death.LavaMelt; deathT = 0f; transform.position = at; deathFrom = at;
            BeginLavaMelt(RobotDeathRule.Hash(Obj.Id, -1, 4u), animated);
            var surf = new Vector3(at.x, ly, at.z);
            var fx = OpenZFxPlayer.I; if (fx && fx.isActiveAndEnabled) fx.LavaHit(surf, 0.7f, false);
            AudioDirector.Fire(OzLava.SndMelt, surf, Obj.Id);
        }

        /// <summary>Check: it is melting into the lava now.</summary>
        public bool LavaMelting => death == Death.LavaMelt;
    }
}
