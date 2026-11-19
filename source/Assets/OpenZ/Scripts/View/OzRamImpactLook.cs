using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <list type="bullet">
    /// <item>R17 "from opponent aside of sparks must start coming off those small chips also": at the Mammoth's ram and the Bizon's crush the
    /// target sheds the game's own heavy-hit armour chips (RobotPopDeath.HullChips: the struck paint, they land, lie and sink) from the contact
    /// point, a burst on the contact frame and a few small ones while the ram / crush grinds (<see cref="Chips"/>).</item>
    /// <item>R19 "squeezed vehicle should look damaged burning wreck ... transitioning easing from normal to squeezed, not instant": the
    /// Bizon-crushed target's paint burns toward the wreck's char, its hull sags crooked (dented) and small fires with dark smoke break out of
    /// it, all eased with the squash as the tracks press it (<see cref="CrushLook"/>, called by OzCrushSquash every frame).</item>
    /// <item>R20 "the enemy that is being rammed also needs to wobble slightly (not jitter)": the rammed / crushed target rocks on its
    /// suspension from the contact frame: a damped spring about its ground centre, pushed in the ram's direction, decaying
    /// (<see cref="OzImpactWobble"/>).</item>
    /// </list>
    /// Switches: <see cref="ChipsOn"/>, <see cref="WreckLookOn"/>, <see cref="WobbleOn"/> (off = as before).
    /// </summary>
    public static class OzRamImpactLook
    {
        public static bool ChipsOn = true, WreckLookOn = true, WobbleOn = true;
        /// <summary>R17: chips in the contact burst / in each grinding burst, the gap between grinding bursts (s), grinding bursts per target.</summary>
        public static int ChipFirst = 5, ChipGrind = 2, ChipGrindMax = 4;
        public static float ChipGap = 0.12f;
        /// <summary>R19: the paint's char when flat (the wreck's own start: WreckFx.CharStart), the crooked sag when flat (deg), the beat of the
        /// fires (s), the squash at which the smoke starts / the flames start.</summary>
        public static float CrushChar = 0.66f, SagMaxDeg = 2f, BurnBeat = 0.22f, SmokeFrom = 0.12f, FlameFrom = 0.35f;
        // statistics (proof)
        public static int ChipBursts, ChipsThrown, CrushLooks, BurnBeats, Ignites, WobbleKicks;
        public static float LastChipAt = -1f, LastFirstChipAt = -1f, MaxChar;

        struct ChipState { public float at; public int n; }
        static readonly Dictionary<int, ChipState> chips = new Dictionary<int, ChipState>();

        /// <summary>R17: the target's armour chips at the contact (first) or while the contact grinds (rate-limited, a few per target).</summary>
        public static void Chips(ZObject target, Transform root, Vector3 at, Vector3 from, bool first)
        {
            if (!ChipsOn || target == null || !root || !root.gameObject.activeInHierarchy) return;
            if (chips.Count > 64) chips.Clear();
            chips.TryGetValue(target.Id, out var st);
            if (first) { if (st.n > 0) return; }
            else if (st.n == 0 || st.n > ChipGrindMax || Time.time < st.at + ChipGap) return;
            int n = first ? ChipFirst : ChipGrind;
            var col = Color.Lerp(TeamColors.Of(target.Owner), new Color(0.45f, 0.45f, 0.45f), 0.3f);   // as HeavyChips: the struck paint
            var away = at - from; away.y = 0f;
            RobotPopDeath.HullChips(root, at, from, away, col, n, RobotDeathRule.Hash(target.Id, st.n + 1, 47u));
            chips[target.Id] = new ChipState { at = Time.time, n = st.n + 1 };
            ChipBursts++; ChipsThrown += n; LastChipAt = Time.time; if (first) LastFirstChipAt = Time.time;
        }

        /// <summary>R17 + R20 at a drawn contact (every frame it pushes): the first call = the contact burst of chips and the jolt
        /// (kick, deg/s), later calls = the grinding chips (rate-limited).</summary>
        public static void Contact(ZObject target, UnitView tv, Vector3 at, Vector3 from, Vector3 pushW, float kickDegPerSec)
        {
            if (target == null || !tv) return;
            bool firstNow = !chips.TryGetValue(target.Id, out var st) || st.n == 0;
            // the chips burst out of the crevice sideways and up (from the struck armour beside the contact, the side the camera sees),
            // not back into the rammer's own nose where they would be hidden
            var p0 = pushW; p0.y = 0f; p0 = p0.sqrMagnitude > 1e-4f ? p0.normalized : Vector3.forward;
            var lat = Vector3.Cross(Vector3.up, p0); var cam = Camera.main;
            if (cam && Vector3.Dot(cam.transform.position - at, lat) < 0f) lat = -lat;
            // the struck armour: on the contact face at the hull's painted side (not the low track wheels the wedge meets), at the end of
            // the contact face nearer the camera
            float gy = tv.transform.position.y, hh = OzBizonCrushView.HullHeight(tv, gy);
            var cat = at; cat.y = Mathf.Max(at.y, gy + 0.6f * hh);
            cat += lat * Mathf.Min(1.4f, Extent(tv, lat) * 0.55f);
            var side = cat + lat * 2.2f + Vector3.up * 0.9f - p0 * 0.9f;
            Chips(target, tv.transform, cat, side, true);
            Chips(target, tv.transform, cat, side, false);
            if (firstNow && WobbleOn && !tv.Dying)
            {
                var wb = OzImpactWobble.Of(tv);
                if (wb) wb.Kick(pushW, kickDegPerSec, (RobotDeathRule.Hash(target.Id, 20, 203u) & 1u) == 0 ? 1f : -1f);
            }
        }

        /// <summary>R19 + R20 for one crushed target, every frame from OzCrushSquash (after its scale): s = the eased squash (0 whole .. 1 flat),
        /// dk = how much it was pressed further this frame.</summary>
        public static void CrushLook(OzCrushSquash sq, UnitView view, ZObject obj, UnitView bizonView, float s, float dk)
        {
            if (!sq || !view || obj == null || view.Dying) return;
            var st = sq.Look;
            if (st == null)
            {
                st = sq.Look = new CrushState();
                st.paint = view.GetComponent<TeamPaint>(); st.c0 = st.paint ? st.paint.Charred : 0f;
                uint h = RobotDeathRule.Hash(obj.Id, 19, 191u);
                st.sagP = ((h & 1u) == 0 ? 1f : -1f) * Mathf.Lerp(0.45f, 0.8f, ((h >> 4) & 255u) / 255f);
                st.sagR = ((h & 2u) == 0 ? 1f : -1f) * Mathf.Lerp(0.6f, 1f, ((h >> 12) & 255u) / 255f);
                var d = bizonView ? view.transform.position - bizonView.transform.position : view.transform.forward; d.y = 0f;
                st.dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
                st.ext = Extent(view, Vector3.Cross(Vector3.up, st.dir));
                CrushLooks++;
            }
            if (WreckLookOn && st.paint)
            {   // the burn eases in with the press, never lighter than it was
                float want = Mathf.Lerp(st.c0, Mathf.Max(st.c0, CrushChar), s);
                if (want > st.paint.Charred + 0.004f) { st.paint.Charred = want; MaxChar = Mathf.Max(MaxChar, want); }
            }
            if (WobbleOn || WreckLookOn)
            {
                var wob = OzImpactWobble.Of(view);
                if (wob)
                {
                    if (WreckLookOn) { wob.SagPitchDeg = st.sagP * SagMaxDeg * s; wob.SagRollDeg = st.sagR * SagMaxDeg * s; wob.SagDir = st.dir; }
                    if (WobbleOn && dk > 0f) wob.Pump(st.dir, dk);
                }
            }
            if (dk > 0f && s > 0.05f && bizonView)
            {   // R17: the grinding chips burst out sideways from under the tracks while it is pressed
                var lat0 = Vector3.Cross(Vector3.up, st.dir); float sd = (st.chipSide++ & 1) == 0 ? 1f : -1f;
                var cp = view.transform.position + lat0 * sd * st.ext * 0.8f; cp.y += Mathf.Max(0.3f, sq.HullTopNow * 0.7f);
                Chips(obj, view.transform, cp, cp - lat0 * sd * 2f, false);
            }
            if (!WreckLookOn || s < SmokeFrom) return;
            st.burnT -= Time.deltaTime;
            if (st.burnT > 0f) return;
            st.burnT = BurnBeat * Random.Range(0.85f, 1.15f);
            var fxp = OpenZFxPlayer.I;
            if (!fxp || !fxp.isActiveAndEnabled) return;
            // two sites at the hull's ends across the Bizon's path (they stick out beside its tracks), low on the pressed hull
            var p = view.transform.position; var lat = Vector3.Cross(Vector3.up, st.dir);
            float side = (st.beat++ & 1) == 0 ? 1f : -1f;
            float hNow = Mathf.Max(0.3f, sq.HullTopNow * 0.9f);
            var site = p + lat * side * st.ext * 0.72f + st.dir * Random.Range(-0.4f, 0.4f);
            site.y = p.y + hNow;
            if (!OzFireSource.Crush(view, ref site)) return;   /* 1.5 N33 (kit fire_no_source_n33): the site was the half width of the hull measured at the first contact (with its gun) at the MEAN pressed height: beside or over the dented hull. On the top of what is drawn under it (View/OzFireSource.cs) */
            bool flames = s >= FlameFrom;
            fxp.WreckBurn(site, Mathf.Lerp(0.45f, 0.8f, s), flames, 0.3f, site + Vector3.up * 0.05f);
            BurnBeats++;
            if (flames && !st.ignited) { st.ignited = true; Ignites++; OzSoundV3.Ignite("crush_burn_" + obj.Id, site, 0.6f); }   // the flames catch: their sound
        }

        /// <summary>The hull's half extent along a world direction (its renderers, from the ground centre).</summary>
        internal static float Extent(UnitView v, Vector3 axis)
        {
            float e = 1.2f; var c = v.transform.position;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var n = r.name.ToLowerInvariant();
                if (n.Contains("shadow") || n.Contains("label") || n.Contains("barrel") || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450)) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var q = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.center.y, (i & 2) == 0 ? b.min.z : b.max.z);
                    e = Mathf.Max(e, Mathf.Abs(Vector3.Dot(q - c, axis)));
                }
            }
            return Mathf.Min(e, 5f);
        }

        public class CrushState
        {
            public TeamPaint paint; public float c0, sagP, sagR, burnT, ext; public int beat, chipSide; public bool ignited; public Vector3 dir;
        }
    }

    /// <summary>1.5 last pass 14j R20 (and R19's sag): a rammed / crushed target rocks on its suspension: a damped spring (pitch toward the push,
    /// a little roll) about its ground centre on its Model node, kicked at the contact and pumped while it is pressed; decays, never noise.
    /// Writes only the Model node's local pose (rest kept); on the unit's death it stops where it is.</summary>
    [DefaultExecutionOrder(1010)]
    public class OzImpactWobble : MonoBehaviour
    {
        /// <summary>The rock's frequency (Hz), damping ratio, largest swing (deg), the roll's share of a kick.</summary>
        public static float Hz = 2.4f, Damp = 0.24f, MaxDeg = 3.2f, RollShare = 0.35f;
        public static int Made;
        public static float MaxSeen;
        UnitView view; Transform model; Vector3 restPos; Quaternion restRot;
        float pitch, pitchV, roll, rollV, fade = 1f; bool fading;
        Vector3 axisL = Vector3.right, rollL = Vector3.forward;   // in the view's frame (the body's)
        /// <summary>R19: a crooked sag (deg) toward / across SagDir, set by the crush look (static, eased by the squash).</summary>
        public float SagPitchDeg, SagRollDeg; public Vector3 SagDir;
        public float Angle => Mathf.Sqrt(pitch * pitch + roll * roll);

        public static OzImpactWobble Of(UnitView v)
        {
            if (!v) return null;
            var w = v.GetComponent<OzImpactWobble>();
            if (w) return w;
            var m = OzBizonCrushView.ModelOf(v);
            if (!m || m == v.transform) return null;
            w = v.gameObject.AddComponent<OzImpactWobble>();
            w.view = v; w.model = m; w.restPos = m.localPosition; w.restRot = m.localRotation; Made++;
            return w;
        }

        void SetAxes(Vector3 pushW)
        {
            pushW.y = 0f; if (pushW.sqrMagnitude < 1e-6f) return; pushW.Normalize();
            if (axesSet) return;                                                           // the first contact's direction holds (no jumps)
            axesSet = true;
            var latW = Vector3.Cross(Vector3.up, pushW);
            axisL = transform.InverseTransformDirection(latW).normalized;                  // + tips the top toward the push
            rollL = transform.InverseTransformDirection(pushW).normalized;
            pushL = rollL; latL = -axisL;                                                  // body frame: along the push, across it
            extP = Mathf.Clamp(OzRamImpactLook.Extent(view, pushW) * 0.9f, 0.6f, 4f);      // the bottom edges it rocks on
            extR = Mathf.Clamp(OzRamImpactLook.Extent(view, latW) * 0.9f, 0.6f, 4f);
        }
        bool axesSet; Vector3 pushL = Vector3.forward, latL = Vector3.right; float extP = 1.5f, extR = 1.5f;

        /// <summary>A turn about a bottom edge: the edge on the side the top leans to (a rocking block: nothing goes under the ground).</summary>
        Vector3 Edge(Quaternion q, Vector3 along, float ext)
        {
            var t = q * Vector3.up; float s = Vector3.Dot(t, along);
            return along * (s >= 0f ? ext : -ext);
        }

        /// <summary>The contact's jolt: the top swings toward the push (deg/s), a little roll to one side.</summary>
        public void Kick(Vector3 pushW, float degPerSec, float rollSign)
        {
            SetAxes(pushW);
            pitchV += degPerSec; rollV += degPerSec * RollShare * rollSign;
            OzRamImpactLook.WobbleKicks++;
        }

        /// <summary>The press drives the rock (in step with its swing: a smooth shudder that grows while pressed, decays after).</summary>
        public void Pump(Vector3 pushW, float dk)
        {
            if (Mathf.Abs(pitchV) < 1e-3f && Mathf.Abs(pitch) < 1e-3f) { Kick(pushW, 14f, 1f); return; }
            float sgn = pitchV >= 0f ? 1f : -1f;
            pitchV += sgn * dk * 40f; rollV += (rollV >= 0f ? 1f : -1f) * dk * 12f;
        }

        /// <summary>The flip takes over: the rock fades out (0.15 s).</summary>
        public void FadeOut() { fading = true; }

        void LateUpdate()
        {
            if (!view || !model || view.Dying || !view.gameObject.activeInHierarchy) { enabled = false; Destroy(this); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            float w = 2f * Mathf.PI * Hz; int n = Mathf.Max(1, Mathf.CeilToInt(dt * 240f)); float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                pitchV += (-w * w * pitch - 2f * Damp * w * pitchV) * h; pitch += pitchV * h;
                rollV += (-w * w * roll - 2f * Damp * w * rollV) * h; roll += rollV * h;
            }
            if (Mathf.Abs(pitch) > MaxDeg) { pitch = Mathf.Sign(pitch) * MaxDeg; pitchV *= 0.5f; }
            if (Mathf.Abs(roll) > MaxDeg * 0.6f) { roll = Mathf.Sign(roll) * MaxDeg * 0.6f; rollV *= 0.5f; }
            if (fading) fade = Mathf.MoveTowards(fade, 0f, dt / 0.15f);
            MaxSeen = Mathf.Max(MaxSeen, Angle * fade);
            var qp = Quaternion.AngleAxis(pitch * fade, axisL); var qr = Quaternion.AngleAxis(roll * fade, rollL);
            var pr = Edge(qr, latL, extR); var pp = Edge(qp, pushL, extP);
            var qs = Quaternion.identity;
            if (SagPitchDeg != 0f || SagRollDeg != 0f)
            {   // R19: crooked, pressed into the sand about its ground centre (the Bizon's weight)
                var sd = SagDir; sd.y = 0f;
                if (sd.sqrMagnitude > 1e-4f)
                {
                    sd.Normalize();
                    var aP = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, sd)).normalized; var aR = transform.InverseTransformDirection(sd).normalized;
                    qs = Quaternion.AngleAxis(SagPitchDeg, aP) * Quaternion.AngleAxis(SagRollDeg, aR);
                }
            }
            // the rock: the roll about the bottom edge it leans to, then the pitch about its edge (the view's origin is the ground centre)
            var x = pr + qr * (restPos - pr);
            x = pp + qp * (x - pp);
            model.localPosition = qs * x;
            model.localRotation = qs * qp * qr * restRot;
        }
    }
}
