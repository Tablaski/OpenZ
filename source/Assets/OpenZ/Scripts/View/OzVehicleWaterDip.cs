// OpenZ (GPL-3.0)
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 review R8 (view only, no rule): a vehicle or gun that stands or drives with a track (a wheel, a corner) over open water dips that side
    /// slightly under the water line instead of standing level on the water.
    /// <para>Why it stood on the water: UnitView sits a vehicle on four ground samples round its hull (1.5 m ahead / behind, 1.1 m either side)
    /// and drops a sample that lies more than 0.7 m off the middle one ("a deck edge / bank step"). Over the river that sample is the river bed,
    /// far under the bank, so it was dropped and the hull stood level, its outer track drawn over the water as if on a floor.</para>
    /// <para>Now (<see cref="Sides"/>, called by UnitView between the samples and that rule): a sample over open water (OzWaterLanding: under a
    /// drawn water surface, the bed at least 8 cm down; never lava, never a bridge deck) is taken <see cref="DipM"/> under the water's surface,
    /// at most <see cref="MaxDropM"/> under the middle sample and never under the bed. The hull tilts toward the water through UnitView's own
    /// eased tilt (about 3-7 degrees), its middle stays on the bank: the track on that side goes into the water up to the water line.
    /// While it dips (and while its wreck stays there, unmoved and not sinking), its hull footprint is handed to the water shaders' contacts
    /// (OzWaterLanding.PublishContacts: the soft water line, the part under it faintly seen, the thin foam / ripple ring round its outline).</para>
    /// Off (<see cref="On"/> false) = as before. Reads the drawn water and the walk surface, writes nothing to the rules.
    /// </summary>
    public static class OzVehicleWaterDip
    {
        /// <summary>The switch (false = 1.5 before the review: level on the water).</summary>
        public static bool On = true;
        /// <summary>A sample over water: this far under the water's surface (m); never more than MaxDropM under the middle sample; a middle that
        /// stands DeckAboveM or more over the water under it is a bridge deck (its sides never dip); UnderM = the contact's depth for the shaders.</summary>
        public static float DipM = 0.12f, MaxDropM = 0.25f, DeckAboveM = 0.6f, UnderM = 0.18f;

        /// <summary>Counted proof: frames a vehicle dipped, contacts handed to the water shaders, the last dip.</summary>
        public static int Dips, ContactsDrawn;
        public static string Last = "-";

        sealed class Wet { public UnitView v; public int frame; public Vector3 pos, lc, half; public Renderer hull; public float waterY; }
        static readonly List<Wet> wets = new List<Wet>();
        static readonly List<Renderer> rbuf = new List<Renderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { wets.Clear(); Dips = ContactsDrawn = 0; Last = "-"; }

        /// <summary>UnitView's vehicle branch: p = the drawn place (x, z), fwd / right its axes, hc the middle sample, hf / hb / hr / hl the
        /// raw samples ahead / behind / right / left (before the bank-step rule).</summary>
        public static void Sides(UnitView v, Vector3 p, Vector3 fwd, Vector3 right, float hc, ref float hf, ref float hb, ref float hr, ref float hl)
        {
            if (!On || !v || v.Obj == null || v.Obj.IsShuttle) return;
            var fxp = OpenZFxPlayer.I; if (!fxp || !fxp.isActiveAndEnabled || float.IsNegativeInfinity(fxp.WaterY)) return;   // no drawn water on this level
            if (OzWaterLanding.OpenWaterAt(p, float.NegativeInfinity, out float under) && hc > under + DeckAboveM) return;    // a bridge deck over the water
            float wy = 0f; int n = 0;
            if (One(p + fwd * 1.5f, hc, ref hf, ref wy)) n++;
            if (One(p - fwd * 1.5f, hc, ref hb, ref wy)) n++;
            if (One(p + right * 1.1f, hc, ref hr, ref wy)) n++;
            if (One(p - right * 1.1f, hc, ref hl, ref wy)) n++;
            if (n == 0) return;
            Dips++;
            Note(v, wy);
            if ((Dips & 63) == 1) Last = $"{v.Obj.Type} #{v.Obj.Id}: {n} side(s) over water, water y {wy:F2}, middle {hc:F2}";
        }

        /// <summary>1.5 kit big_tank_wake_r3 (A2): a wading Bizon (OzWadeWake.Feed, every frame in water) is a contact like a dipping hull: the
        /// water shaders draw its organic waterline ring and the clearer water round it.</summary>
        public static void Wader(UnitView v, float waterY) { if (On && v && v.Obj != null) Note(v, waterY); }

        static bool One(Vector3 at, float hc, ref float h, ref float wy)
        {
            if (!OzWaterLanding.OpenWaterAt(at, h, out float w)) return false;                     // h = the walk surface there: the bed under open water
            if (OzLava.SurfaceAt(at, out _)) return false;                                          // lava has its own hull rule (UnitView.LavaHull)
            float want = Mathf.Max(Mathf.Max(w - DipM, hc - MaxDropM), h);                         // under the line, a little; never under the bed
            if (want >= hc - 0.005f) return false;                                                  // water as high as the bank: nothing to dip
            h = want; wy = w;
            return true;
        }

        static void Note(UnitView v, float wy)
        {
            Wet w = null;
            for (int i = 0; i < wets.Count; i++) if (wets[i].v == v) { w = wets[i]; break; }
            if (w == null)
            {
                if (wets.Count >= 32) return;
                w = new Wet { v = v };
                if (!Footprint(v, w)) return;
                wets.Add(w);
            }
            w.frame = Time.frameCount; w.waterY = wy;
        }

        /// <summary>The hull's box in its own frame, from what it draws (once per vehicle).</summary>
        static bool Footprint(UnitView v, Wet w)
        {
            var t = v.transform; rbuf.Clear(); v.GetComponentsInChildren(false, rbuf);
            var box = new Bounds(); bool any = false; float best = -1f;
            for (int i = 0; i < rbuf.Count; i++)
            {
                var r = rbuf[i];
                if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var m = r.sharedMaterial; if (m && m.renderQueue >= 2450) continue;                     // glows, cones, decals
                var b = r.bounds; float vol = b.size.x * b.size.y * b.size.z;
                if (vol > best) { best = vol; w.hull = r; }
                for (int k = 0; k < 8; k++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    var q = t.InverseTransformPoint(c);
                    if (!any) { box = new Bounds(q, Vector3.zero); any = true; } else box.Encapsulate(q);
                }
            }
            rbuf.Clear();
            if (!any) return false;
            w.lc = box.center; w.half = box.extents * 0.92f;                                          // the world boxes of turned parts are a little larger than the hull
            return true;
        }

        /// <summary>OzWaterLanding.PublishContacts: the dipping hulls (alive this frame or the last one; a wreck that stays where it dipped, not
        /// sinking, its hull still drawn) after the pieces the rule holds, while there is room in the shaders' list.</summary>
        public static int Contacts(Vector4[] boxA, Vector4[] boxB, int n, int max)
        {
            int f = Time.frameCount;
            for (int i = wets.Count - 1; i >= 0; i--)
            {
                var w = wets[i];
                if (!w.v || !w.v.gameObject.activeInHierarchy) { wets.RemoveAt(i); continue; }
                var t = w.v.transform;
                bool wreck = w.v.IsWreckNow;
                bool alive = !wreck && f - w.frame <= 1;
                bool still = wreck && !w.v.WreckSinking && (t.position - w.pos).sqrMagnitude < 1e-4f && w.hull && w.hull.enabled && w.hull.gameObject.activeInHierarchy;
                if (!alive && !still) { if (wreck || f - w.frame > 1) wets.RemoveAt(i); continue; }
                if (alive) w.pos = t.position;
                if (n >= max) continue;
                var c = t.TransformPoint(w.lc);
                if (!WreckFx.OnScreen(c, 0.2f)) continue;
                var u = t.right; u.y = 0f; float ul = u.magnitude; if (ul < 1e-3f) continue; u /= ul;
                var s = t.lossyScale;
                boxA[n] = new Vector4(c.x, c.z, Mathf.Max(0.05f, w.half.x * Mathf.Abs(s.x)), Mathf.Max(0.05f, w.half.z * Mathf.Abs(s.z)));
                boxB[n] = new Vector4(u.x, u.z, w.waterY, UnderM);
                n++; ContactsDrawn++;
            }
            return n;
        }

        public static string Status() => $"vehicle water dip {(On ? "on" : "off")} (dip {DipM:0.00} m, max {MaxDropM:0.00} m): dips {Dips}, contacts {ContactsDrawn}, held {wets.Count}; last: {Last}";
    }
}
