using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// his frame: a Mammoth on the Unit editor floor beside a ramp platform, its nose up). View only; the rules are never read for it beyond
    /// what UnitView already draws, never written.
    /// <para>Causes (the planted running gear, View/UnitView.GroundGear.cs, measured on the Unit editor's 18 deg ramp with the Mammoth's own
    /// link loop):</para>
    /// <list type="bullet">
    /// <item>The belt's ground samples ran along the flat BOTTOM OF THE BOX of the whole link loop (6.1 m on the Mammoth, 7.2 m on the Bizon), but
    /// the real links touch only along their bottom run (4.5 m / 5.2 m): at the sprocket and the idler the loop rises 0.25 to 0.6 m. The box's
    /// corners met a ramp, a crest or a bank 0.8 m before the real track did: on the ramp's foot the hull was pitched by the box (3.5 deg off
    /// the real pose) with the whole track up to 18 cm in the air, the nose lifting early going up and staying high going down.</item>
    /// <item>The pitch was the slope of the ground's upper hull right under the belt's middle, eased at 20 / s: a rigid see-saw. Over a crest
    /// (the ramp's top, a platform's edge, a bank's lip) the hull stayed at the old slope with its nose up to 0.9 m in the air over the deck
    /// until its middle passed the crest, then dropped 18 deg in a few frames; going down the nose hung out level, then dropped.</item>
    /// <item>Beside a Unit editor platform (the owner's frame), belt stations over the platform's side wall within 0.7 m of the floor (and up
    /// to 0.45 m between two probes: GearSlopeRun's "slope") read the platform's top: the belt's end climbed the wall: nose or tail up to
    /// 6 deg and a roll, while the tank stood on the floor.</item>
    /// <item>The ram's drawn lead (OzRamLunge, the DV188 push path, View/OzRamPush.cs) moves the Mammoth up to 1.2 m on along the ram AFTER its
    /// tracks were planted on the ground at its rules' place, for up to 6 s: on a slope its hull was drawn at the height and pitch of the ground
    /// 1.2 m behind it (on 18 deg: 37 cm into the slope uphill, floating downhill).</item>
    /// <item>
    /// centre's height: on the ramp's foot the pitch was half the ramp's (9 deg short), the track 53 cm in the air at one end.</item>
    /// </list>
    /// <para>Now, for the heavy tank (v1 / v2), the Mammoth and the Bizon (every other planted vehicle exactly as before):</para>
    /// <list type="bullet">
    /// <item>CONTACT: the Mammoth's and the Bizon's belt samples follow the real underside of their link loop (the pitch line from the prefab's
    /// baked track paths, offset by the link's half thickness): the flat bottom run touches, the rising ends only where the ground rises into
    /// them (a station past the loop never touches). The heavy tank keeps its box (its track mesh is no link loop). All three: 25 stations
    /// a belt (<see cref="Stations"/>; was 13: a crest's crease reached 4 cm into the track between two).</item>
    /// <item>PITCH: each belt's pitch is the best-fit line of the upper envelope of the ground under its WHOLE contact (front to back,
    /// <see cref="FitPitch"/>): on a constant slope and in a valley (the ramp's foot) exactly the line through its front and back supports; over a
    /// crest it tips over gradually as the crest passes under it (no see-saw, no nose in the air, no drop); a dip it bridges does not tip it.
    /// The roll: the two belts' heights (left / right) as before. Both through a critically damped spring (<see cref="Hz"/>, no jumps, no
    /// overshoot) never more than <see cref="MaxLagDeg"/> off what the ground allows; the height stays exact every frame (the lowest point of
    /// each belt on the ground: never into it).</item>
    /// <item>PLATFORMS (the Unit editor only: no level has one): a station across a platform's wall from the tank (a jump of more than
    /// <see cref="WallStepM"/> between two probes <see cref="ProbeM"/> apart on the way from the centre) keeps the level the belt is on; the ramp's
    /// foot and crest are continuous and read as they are.</item>
    /// <item>THE RAM'S LEAD: the ground under the belts is read where the hull is drawn (the rules' place + the lead), so the lead only moves it
    /// along; when the lead runs out the hull is back on its own ground (never left tilted).</item>
    /// </list>
    /// The hull still rides on the belts with its slight drive wobble, the shot's rock and the R21 start / stop rock (unchanged, on top).
    /// Switches: <see cref="On"/> (off = everything as before), <see cref="ProfileOn"/> (off = the box's bottom as before).
    /// </summary>
    public static class BigTankPitch
    {
        /// <summary>The switch (off = the planted gear exactly as before for every vehicle).</summary>
        public static bool On = true;
        /// <summary>The Mammoth's / Bizon's belt samples on the real underside of the link loop (off = the box's flat bottom).</summary>
        public static bool ProfileOn = true;
        /// <summary>The pitch / roll spring: natural frequency (Hz, critically damped) and the most it may lag what the ground allows (deg).</summary>
        public static float Hz = 2.5f, MaxLagDeg = 2.5f;
        /// <summary>The steepest pitch / roll drawn (deg).</summary>
        public static float MaxDeg = 35f;
        /// <summary>Platforms: the probes' spacing (m) and the largest step between two probes that is no wall (m: 31 deg; the ramp is 18).</summary>
        public static float ProbeM = 0.25f, WallStepM = 0.15f;
        /// <summary>A station of a link loop that lies past the loop's ends (no link under it): this far over the box's bottom (m): it never touches.</summary>
        public const float NoTouchM = 1.5f;
        /// <summary>Ground stations along each belt of a big tank (the planting's TrackGround.Stations is 13: 0.5 m apart on the Mammoth, a crest's
        /// crease reached 4 cm into the track between two; 25: 0.25 m, under 2.5 cm), and the link profile's bottom run (m over the box bottom):
        /// the stations whose ground the pitch line is fitted to.</summary>
        public static int Stations = 25;
        public static float RunDyM = 0.03f;
        /// <summary>Proof: frames drawn by this, belt stations kept on their level at a platform wall, stations read at the ram's lead, belts
        /// given the real link profile, the largest spring lag clamped (deg).</summary>
        public static int Frames, Walls, LungeSamples, ProfileBelts;
        public static float MaxLagSeen;

        /// <summary>The vehicles drawn this way: the heavy tank (v1 / v2), the Mammoth, the Bizon.</summary>
        public static bool Is(string type) => type == OzHeavyTank.Type || type == OzBigTanks.Mammoth || type == OzBigTanks.Bizon;

        public static void ResetStats() { Frames = Walls = LungeSamples = ProfileBelts = 0; MaxLagSeen = 0f; }

        /// <summary>The pitch (deg, the Euler x angle: the model's front is -z, ground rising toward +z = a negative angle) of the best-fit line
        /// of the upper envelope of the ground under a belt: z = the stations along it (ascending, m), g = the ground under each (the higher of
        /// its two edges, the link profile already taken off), use = the stations that can touch (null = all: the envelope is their upper
        /// hull), fit = the stations the line is fitted over (the bottom run; null = use); hz / hg = scratch (length n). On flat ground 0, on a
        /// constant slope its slope, in a valley the line through the front and back supports, over a crest the mean of the envelope.</summary>
        public static float FitPitch(float[] z, float[] g, bool[] use, int n, float[] hz, float[] hg, bool[] fit = null)
        {
            if (fit == null) fit = use;
            int m = 0;
            for (int j = 0; j < n; j++)
            {
                if (use != null && !use[j]) continue;
                float zj = z[j], gz = g[j];
                while (m >= 2 && (hz[m - 1] - hz[m - 2]) * (gz - hg[m - 2]) - (hg[m - 1] - hg[m - 2]) * (zj - hz[m - 2]) >= 0f) m--;   // keep the upper hull (z ascending)
                hz[m] = zj; hg[m] = gz; m++;
            }
            if (m < 2) return 0f;
            double sz = 0, sg = 0, szz = 0, szg = 0; int cnt = 0, k = 0;
            for (int j = 0; j < n; j++)
            {
                if (fit != null && !fit[j]) continue;
                float zj = z[j];
                while (k + 2 < m && zj > hz[k + 1]) k++;
                float a = Mathf.Clamp01((zj - hz[k]) / Mathf.Max(1e-4f, hz[k + 1] - hz[k]));
                double e = hg[k] + (hg[k + 1] - hg[k]) * a;                                              // the envelope over this station
                sz += zj; sg += e; szz += (double)zj * zj; szg += zj * e; cnt++;
            }
            double den = cnt * szz - sz * sz;
            if (cnt < 2 || den < 1e-8) return 0f;
            float slope = (float)((cnt * szg - sz * sg) / den);
            return -Mathf.Atan(slope) * Mathf.Rad2Deg;
        }

        /// <summary>One step of a critically damped spring (exact for any dt) of an angle x (deg) with its rate v (deg/s) toward target, then
        /// held within maxLag of it (the rate toward outside cut).</summary>
        public static void Spring(ref float x, ref float v, float target, float omega, float dt, float maxLag)
        {
            float e = Mathf.DeltaAngle(target, x);
            if (dt > 0f)
            {
                float a = v + omega * e, ex = Mathf.Exp(-omega * dt);
                e = (e + a * dt) * ex; v = (v - omega * a * dt) * ex;
            }
            if (e > maxLag) { e = maxLag; if (v > 0f) v = 0f; }
            else if (e < -maxLag) { e = -maxLag; if (v < 0f) v = 0f; }
            if (Mathf.Abs(e) > MaxLagSeen) MaxLagSeen = Mathf.Abs(e);
            x = target + e;
        }

        /// <summary>The link loop's underside over each station: path = the loop's pitch line in the vehicle's rest space, y0 = the belt's box
        /// bottom there, z = the stations; out: the height of the real underside over y0 at each (m, NoTouchM where no link lies under the
        /// station). The link's half thickness is the pitch line's lowest point over the box bottom. False = no usable profile.</summary>
        public static bool Profile(Vector3[] path, float y0, float[] z, float[] dy)
        {
            if (path == null || path.Length < 8 || z == null || dy == null) return false;
            float ymin = float.MaxValue;
            foreach (var p in path) ymin = Mathf.Min(ymin, p.y);
            float t = Mathf.Clamp(ymin - y0, 0.05f, 0.4f), t2 = t * t;
            int flat = 0;
            for (int j = 0; j < z.Length; j++)
            {
                float low = float.MaxValue;
                foreach (var p in path) { float d = p.z - z[j]; if (d * d <= t2) low = Mathf.Min(low, p.y - Mathf.Sqrt(t2 - d * d)); }
                dy[j] = low == float.MaxValue ? NoTouchM : Mathf.Clamp(low - y0, 0f, NoTouchM);
                if (dy[j] < RunDyM) flat++;
            }
            return flat >= 3;                                                                            // a bottom run under at least three stations
        }
    }

    public partial class UnitView
    {
        // N26 state: per belt the pitch rate, the link profile (height of the real underside over the box bottom per station), the stations
        // that can touch, scratch; the roll rate; this frame's ram lead and the one the samples were read at.
        float[] bpVel; float[][] bpDy; bool[][] bpUse, bpFit; float[] bpZ, bpG; float brVel; bool bpBig; int bpFrame = -1; Vector3 bpLunge, bpLungeRead;

        /// <summary>Proof (N26, the frames check): the belts' mean target pitch this frame (deg, + = front up), the half length of the real track
        /// contact and the half gauge of the belts (m, the root's rest space; -1 = not a big tank or no gear yet).</summary>
        public float BigPitchTargetDeg { get; private set; }
        public float BigContactHalfM { get; private set; } = -1f;
        public float BigGaugeHalfM { get; private set; } = -1f;

        void BigFrame()
        {
            if (bpFrame == Time.frameCount) return;
            bpFrame = Time.frameCount;
            bpBig = BigTankPitch.On && Obj != null && BigTankPitch.Is(Obj.Type);
            bpLunge = Vector3.zero;
            if (bpBig && TryGetComponent<OzRamLunge>(out var lg) && lg) { bpLunge = lg.Offset; bpLunge.y = 0f; }
        }

        /// <summary>GroundGearBefore's "still" test: the ram's drawn lead moved since the samples were read (read them again).</summary>
        bool BigResample()
        {
            BigFrame();
            return bpBig && (bpLunge - bpLungeRead).sqrMagnitude > 1e-4f;
        }

        /// <summary>GroundGearBefore, every belt station: the ground under it (gy as the planting read it, gc the centre's). A big tank: read where
        /// the hull is drawn (the ram's lead on); on or beside a Unit editor platform a wall between the centre and the station keeps the belt's
        /// level. Everything else: gy as it was.</summary>
        float BigStation(Vector3 p0, Vector3 w, float gc, float gy)
        {
            BigFrame();
            if (!bpBig || game == null) return gy;
            bpLungeRead = bpLunge;
            var pw = game.World; var off = bpLunge; bool lunge = off.sqrMagnitude > 1e-6f;
            var c = p0 + off; var q = w + off;
            bool plat = pw != null && (pw.PlatformHeightM(c.x * 8.0, -c.z * 8.0) > 0.0 || pw.PlatformHeightM(q.x * 8.0, -q.z * 8.0) > 0.0);
            if (!plat && !lunge) return gy;
            if (lunge) BigTankPitch.LungeSamples++;
            float g0 = lunge ? game.GroundY(c.x, c.z) : gc, g1 = game.GroundY(q.x, q.z);
            if (!plat) return Mathf.Abs(g1 - g0) > TrackGround.StepIgnoreM && !GearSlopeRun(c, q, g0) ? g0 : g1;   // the planting's own step rule at the drawn place
            if (Mathf.Abs(g1 - g0) <= BigTankPitch.WallStepM) return g1;
            float dx = q.x - c.x, dz = q.z - c.z;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(0.05f, BigTankPitch.ProbeM)), 1, 48);
            float prev = g0;
            for (int k = 1; k <= n; k++)
            {
                float h = k == n ? g1 : game.GroundY(c.x + dx * k / n, c.z + dz * k / n);
                if (Mathf.Abs(h - prev) > BigTankPitch.WallStepM) { BigTankPitch.Walls++; return prev; }   // a platform's wall: the belt stays on its level
                prev = h;
            }
            return g1;
        }

        /// <summary>GroundGearBefore: each belt's pitch on the spring toward the best-fit line of the ground under its whole contact. False = not a
        /// big tank (the planting eases to the line under its middle as before).</summary>
        bool BigPitchStep(float s, float dt)
        {
            BigFrame();
            if (!bpBig || gearBelts == null || gearBelts.Length == 0) return false;
            if (bpVel == null || bpVel.Length != gearBelts.Length) bpVel = new float[gearBelts.Length];
            float om = 2f * Mathf.PI * Mathf.Max(0.2f, BigTankPitch.Hz), sum = 0f;
            for (int i = 0; i < gearBelts.Length; i++)
            {
                var b = gearBelts[i];
                int n = b.pts.Length / 2;
                if (bpZ == null || bpZ.Length < n) { bpZ = new float[n]; bpG = new float[n]; }
                var dy = bpDy != null && i < bpDy.Length ? bpDy[i] : null;
                var use = bpUse != null && i < bpUse.Length ? bpUse[i] : null; var fit = bpFit != null && i < bpFit.Length ? bpFit[i] : null;
                for (int j = 0; j < n; j++)
                {
                    bpZ[j] = b.pts[2 * j].z * s;
                    bpG[j] = Mathf.Max(b.g[2 * j], b.g[2 * j + 1]) - (dy != null ? dy[j] * s : 0f);
                }
                float want = Mathf.Clamp(BigTankPitch.FitPitch(bpZ, bpG, use, n, b.hz, b.hg, fit), -BigTankPitch.MaxDeg, BigTankPitch.MaxDeg);
                sum += want;
                if (gearSnap) { b.pitch = want; bpVel[i] = 0f; continue; }
                float x = b.pitch, v = bpVel[i];
                BigTankPitch.Spring(ref x, ref v, want, om, dt, BigTankPitch.MaxLagDeg);
                b.pitch = x; bpVel[i] = v;
            }
            BigPitchTargetDeg = sum / gearBelts.Length;                                               // the Euler x angle: + = the front (-z) up
            BigTankPitch.Frames++;
            return true;
        }

        /// <summary>GroundGearBefore: the shared roll (from the two belts' heights) on the same spring for a big tank; as before otherwise.</summary>
        float BigRollStep(float want, float k, float dt)
        {
            BigFrame();
            if (!bpBig) return Mathf.LerpAngle(gearRoll, want, k);
            want = Mathf.Clamp(want, -BigTankPitch.MaxDeg, BigTankPitch.MaxDeg);
            if (gearSnap) { brVel = 0f; return want; }
            float x = gearRoll, v = brVel;
            BigTankPitch.Spring(ref x, ref v, want, 2f * Mathf.PI * Mathf.Max(0.2f, BigTankPitch.Hz), dt, BigTankPitch.MaxLagDeg);
            brVel = v;
            return x;
        }

        /// <summary>Proof (N26, the frames check): from the DRAWN belt nodes, the deepest belt sample that can touch (the real underside of a link
        /// loop, else the box bottom) below the ground under it (m, + = below) and the largest air under a belt's lowest such sample (m).</summary>
        public bool BigProofContact(out float sink, out float gap)
        {
            sink = -9f; gap = 0f;
            if (!ProofBelts(out _, out _, out _, out _, out _) || gearNodes == null || gearBelts == null) return false;
            float s = Mathf.Abs(transform.lossyScale.x); if (s < 1e-4f) s = 1f;
            for (int k = 0; k < gearBelts.Length; k++)
            {
                GearNode node = null;
                foreach (var g in gearNodes) if (g.t && !g.spun && g.side == k) { node = g; break; }
                if (node == null) continue;
                var F = node.t.rotation * Quaternion.Inverse(node.chain.rotation * node.restRot);
                var at = node.t.position - F * ((Vector3)(node.chain * Matrix4x4.TRS(node.restPos, node.restRot, node.scale)).GetColumn(3) * s);
                var b = gearBelts[k]; var use = bpUse != null && k < bpUse.Length ? bpUse[k] : null; float low = 9f;
                for (int i = 0; i < b.pts.Length; i++)
                {
                    if (use != null && !use[i / 2]) continue;
                    var w = at + F * (b.pts[i] * s); float d = game.GroundY(w.x, w.z) - w.y;
                    sink = Mathf.Max(sink, d); low = Mathf.Min(low, -d);
                }
                gap = Mathf.Max(gap, low);
            }
            return true;
        }

        /// <summary>GearInit, once the belts are made (a big tank, the switch on): each belt gets BigTankPitch.Stations ground stations (both
        /// edges, along its box as MakeBelt lays them); a Mammoth's / Bizon's belt samples lie on the real underside of its link loop (the baked
        /// pitch line of each side, OzTrackLinks), its pitch line is fitted over the loop's bottom run; the proof's contact half length and half
        /// gauge.</summary>
        void BigProfileInit()
        {
            bpDy = null; bpUse = null; bpFit = null; bpVel = null; brVel = 0f; BigContactHalfM = -1f; BigGaugeHalfM = -1f;
            if (gearBelts == null || gearBelts.Length == 0 || Obj == null || !BigTankPitch.Is(Obj.Type)) return;
            if (gearBelts.Length > 1) BigGaugeHalfM = Mathf.Abs(gearBelts[gearBelts.Length - 1].xc - gearBelts[0].xc) * 0.5f;
            float c0 = float.MaxValue, c1 = float.MinValue;
            foreach (var b in gearBelts) { c0 = Mathf.Min(c0, b.z0); c1 = Mathf.Max(c1, b.z1); }
            BigContactHalfM = (c1 - c0) * 0.5f;
            if (!BigTankPitch.On) return;
            int ns = Mathf.Max(3, BigTankPitch.Stations);
            foreach (var b in gearBelts)
            {   // the denser stations (MakeBelt's layout: along the box's bottom at both edges)
                if (b.pts.Length / 2 == ns) continue;
                b.pts = new Vector3[2 * ns]; b.g = new float[2 * ns]; b.hz = new float[ns]; b.hg = new float[ns];
                for (int j = 0; j < ns; j++)
                {
                    float z = Mathf.Lerp(b.z0, b.z1, j / (float)(ns - 1));
                    b.pts[2 * j] = new Vector3(b.x0, b.y0, z); b.pts[2 * j + 1] = new Vector3(b.x1, b.y0, z);
                }
            }
            gearSnap = true;
            if (!BigTankPitch.ProfileOn || gearNodes == null) return;
            var l = motion ? motion.Links : null;
            if (!l || l.sides == null) return;
            var dys = new float[gearBelts.Length][]; var uses = new bool[gearBelts.Length][]; var fits = new bool[gearBelts.Length][];
            bool any = false; float f0 = float.MaxValue, f1 = float.MinValue;
            foreach (var sd in l.sides)
            {
                if (sd == null || !sd.root || !sd.space || sd.path == null || sd.path.Length < 8) continue;
                GearNode g = null;
                foreach (var x in gearNodes) if (x != null && x.t == sd.root) { g = x; break; }
                if (g == null || g.side < 0 || g.side >= gearBelts.Length || dys[g.side] != null) continue;
                var b = gearBelts[g.side];
                var m = g.chain * Matrix4x4.TRS(g.restPos, g.restRot, g.scale) * (sd.root.worldToLocalMatrix * sd.space.localToWorldMatrix);
                var path = new Vector3[sd.path.Length];
                for (int k = 0; k < path.Length; k++) path[k] = m.MultiplyPoint3x4(sd.path[k]);
                int n = b.pts.Length / 2; var z = new float[n]; var dy = new float[n]; var use = new bool[n]; var fit = new bool[n];
                for (int j = 0; j < n; j++) z[j] = b.pts[2 * j].z;
                if (!BigTankPitch.Profile(path, b.y0, z, dy)) continue;
                int touch = 0, run = 0;
                for (int j = 0; j < n; j++)
                {
                    use[j] = dy[j] < BigTankPitch.NoTouchM - 1e-3f; fit[j] = dy[j] < BigTankPitch.RunDyM;
                    if (use[j]) touch++; if (fit[j]) run++;
                }
                if (touch < 3 || run < 3) continue;
                for (int j = 0; j < n; j++)
                {
                    float y = b.y0 + dy[j];                                                              // the real underside; a station past the loop's ends lies NoTouchM up: it never holds the belt up
                    b.pts[2 * j].y = y; b.pts[2 * j + 1].y = y;
                    if (fit[j]) { f0 = Mathf.Min(f0, z[j]); f1 = Mathf.Max(f1, z[j]); }
                }
                dys[g.side] = dy; uses[g.side] = use; fits[g.side] = fit; any = true; BigTankPitch.ProfileBelts++;
            }
            if (!any) return;
            for (int i = 0; i < gearBelts.Length; i++)
                if (dys[i] == null) { dys[i] = new float[gearBelts[i].pts.Length / 2]; uses[i] = null; fits[i] = null; }   // a belt without a loop: its box
            bpDy = dys; bpUse = uses; bpFit = fits;
            if (f1 > f0) BigContactHalfM = (f1 - f0) * 0.5f;
        }
    }
}
