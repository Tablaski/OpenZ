using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// MODELLED wreck pieces. View only. The model carries the wreck set (an inactive OZ_WreckPieces node: 16-26 closed pieces that overlay the
    /// hull and both tracks exactly, burnt inside, in the tank's own paint: hull plates, the side plates with the emblem, deck, belly, track
    /// runs and loose links, sprockets and idlers, the Bizon's pipes and vent boxes, the Mammoth's ram).
    /// <list type="bullet">
    /// <item>Its turret flies up spinning first (the classic Z moment, UnitView.TossTurret as for every tank).</item>
    /// <item>Every big tank breaks (never left whole): at its death (UnitView.BeginWreck: the break 0.05 s after, or at the end of its burn
    /// when the cook-off draw picked that).</item>
    /// <item>At the break (<see cref="Prepare"/>, UnitView.BreakApart, before WreckPieces.Break): the intact hull, side plates and links are
    /// hidden, the wreck set is shown in their place, and the barrels (each *Recoil* subtree) are taken off the flying turret, so WreckPieces
    /// makes a body of every piece (<see cref="OwnPiece"/>: each its own piece, never grouped by its parent) and of every road wheel (a wheel
    /// flies whole) and barrel: box collider, mass by size, the launch out of the hull's middle (barrels thrown higher), the three heaviest
    /// burning with smoke, metal clanks on their first contacts (at most 5 a piece), the whoosh of the big parts, the heavy landing crash; then
    /// they settle and sink away (WreckPieces' rules). The piece budget for these two types is <see cref="GroupCap"/> (WreckPieces' 12 for every
    /// other wreck; 2 big wrecks stay inside its 72 live pieces).</item>
    /// </list>
    /// A model without the wreck set breaks the old way (WreckPieces' grouping, 12 groups) and the menu's check says so.
    /// <para>
    /// The models carry finer sets (kits/new_tanks/blender
    /// nt_wreck_set_fine.py): OZ_WreckPieces in about twice as many pieces (glacis and rear plate in 3, deck 3 and belly 2 a side, skirts 4 a side,
    /// the ladder, every corner wheel, the links in short chains and single links, the Bizon's vent boxes and pipes in 2, the Mammoth's ram in 4),
    /// plus OZ_WreckTurret under the turret (Wreck_Core + six shell plates) and OZ_WreckBarrel under each recoil slide (the barrel in 3 sections).
    /// At the break (<see cref="TurretBreak"/>) the turret still flies up spinning (its core: the classic Z toss and the rules' turret debris point
    /// stay), but its shell plates, the barrel sections, the mantlet, the antennas and the hatch lid fly off it as pieces of their own. Bizon 83,
    /// Mammoth 78 bodies a break (was 39 / 37), cap <see cref="Cap"/>; smaller pieces are thrown a little wider (<see cref="Spread"/>, still
    /// beside the wreck) and the break rattles with small metal sounds (<see cref="Rattle"/>). <see cref="Finer"/> = false: turret and barrels
    /// whole as before (A/B).</para>
    /// <para>
    /// WreckPieces.Break threw every piece straight out of the hull's middle at nearly one speed and lift, so they all landed 5-9 m
    /// out, a clean ring with nothing inside. Now each piece of a big tank's break gets its own throw (<see cref="Launch"/>): an uneven heap
    /// in and round the hull, thinning out, clumps toward the death's blast side; the Bizon's fuel cans the same way (<see cref="CanThrow"/>).
    /// <see cref="Scatter"/> = false: the ring throw as before (A/B).</para>
    /// </summary>
    public static class OzBigTankWreck
    {
        public const string SetNode = "OZ_WreckPieces";
        /// <summary>The most pieces one big tank's break makes (WreckPieces merges the smallest into the largest beyond it).</summary>
        public const int Cap = 88;
        public static bool On = true;
        /// <summary>Proof: breaks made from the wreck set, pieces shown, barrels taken off the turret, breaks without a set.</summary>
        public static int Breaks, PiecesShown, BarrelsOff, NoSet;

        public static bool IsBig(UnitView v) => v && v.Obj != null && OzBigTanks.Is(v.Obj.Type);

        /// <summary>The wreck set of this view (inactive is fine), or null.</summary>
        public static Transform SetOf(UnitView v)
        {
            if (!IsBig(v)) return null;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == SetNode) return t;
            return null;
        }

        /// <summary>UnitView.BeginWreck: this wreck must break (a big tank with its set).</summary>
        public static bool Applies(UnitView v) => On && SetOf(v) != null;

        /// <summary>WreckPieces.Break's budget for this view.</summary>
        // The cap never passes WreckPieces' live budget (72, or 36 with WRECKS AND DEBRIS on LIGHT), so a break never retires its own pieces.
        public static int GroupCap(UnitView v, int normal) => IsBig(v) ? Mathf.Max(normal, Mathf.Min(Cap, WreckPieces.MaxLive)) : normal;

        /// <summary>A name WreckPieces' launcher rule would throw high (barrel|turret|cannon|launcher|pod|rocket|gun|spin|rack|boom|arm): the
        /// set's "Sprocket" holds "rocket". The prefab menu renames such set pieces (<see cref="PlainName"/>).</summary>
        public static bool LauncherName(string n) => System.Text.RegularExpressions.Regex.IsMatch(n ?? "", "barrel|turret|cannon|launcher|pod|rocket|gun|spin|rack|boom|arm", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public static string PlainName(string n) => System.Text.RegularExpressions.Regex.Replace(n ?? "", "Sprocket", "DriveWheel", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>WreckPieces.Break: a renderer of the shown wreck set is its own piece.</summary>
        public static bool OwnPiece(MeshRenderer mr)
        {
            if (!mr) return false;
            for (var p = mr.transform.parent; p; p = p.parent) { if (p.name == SetNode) return true; if (p.GetComponent<UnitView>()) return false; }
            return false;
        }

        /// <summary>A road wheel's mesh (under a TankWheel_N axle): it flies whole as its own piece.</summary>
        static bool RoadWheel(Transform t) => t && (t.name.StartsWith("TankWheel_") || (t.parent && t.parent.name.StartsWith("TankWheel_")));

        /// <summary>UnitView.BreakApart, right before WreckPieces.Break: the set in place of the intact hull and tracks; the barrels off the turret.</summary>
        public static void Prepare(UnitView v, Transform tossed)
        {
            if (!On || !IsBig(v)) return;
            var set = SetOf(v);
            if (!set) { NoSet++; return; }
            var root = v.transform;
            var keep = new HashSet<Renderer>();
            foreach (var r in set.GetComponentsInChildren<Renderer>(true)) keep.Add(r);
            // the barrels: each recoil slide's subtree off the flying turret (each its own piece, thrown high: WreckPieces' launcher rule)
            if (tossed && !(Finer && TurretBreak(root, set, tossed, keep)))                                      // 1.5 big_tanks_wreck_finer: the turret's shell, barrel sections and loose parts (else the barrels whole, as before)
            {
                var slides = new List<Transform>();
                foreach (var t in tossed.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Recoil") && t.GetComponentInChildren<MeshRenderer>(true)) slides.Add(t);
                foreach (var t in slides) { t.SetParent(root, true); BarrelsOff++; }
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr || keep.Contains(mr)) continue;
                if (tossed && mr.transform.IsChildOf(tossed)) continue;                                  // the turret flies whole
                if (mr.transform.parent && mr.transform.parent.name.Contains("Recoil")) continue;        // a barrel taken off: its own piece
                if (RoadWheel(mr.transform)) continue;                                                    // a road wheel flies whole (its own piece)
                mr.enabled = false;                                                                       // hull, plates, pipes, links: the set takes their place
            }
            set.gameObject.SetActive(true);
            foreach (var r in set.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = true; PiecesShown++; }
            var links = root.GetComponentInChildren<OzTrackLinks>(true); if (links) links.enabled = false;
            Breaks++;
            OzBigTanksFx.OnBreak(v);                                                                  // 1.5 kit big_tanks_fx: the code-built parts go with the hull, the Bizon's fuel cans fly (View/OzBigTanksFx.Cans.cs)
        }

        public const string TurretSet = "OZ_WreckTurret", BarrelSet = "OZ_WreckBarrel", CoreName = "Wreck_Core";
        /// <summary>false = the turret flies whole and the barrels come off whole (as before), even on a model with the finer sets (A/B).</summary>
        public static bool Finer = true;
        /// <summary>Sideways throw of the smallest pieces (x WreckPieces' own); 1 for pieces of 2.4 m and more.</summary>
        public static float SpreadSmall = 1.3f;
        /// <summary>Proof: turret shell plates, barrel sections, loose turret parts (mantlet, antennas, lid) taken off at the breaks; rattles made.</summary>
        public static int ShellsOff, SectionsOff, LooseOff, Rattles;

        static bool Under(Transform t, string prefix) { for (var p = t ? t.parent : null; p; p = p.parent) if (p.name.StartsWith(prefix)) return true; return false; }
        static Transform Child(Transform t, string prefix) { foreach (Transform c in t) if (c.name.StartsWith(prefix)) return c; return null; }
        static Transform Deep(Transform t, string prefix) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c != t && c.name.StartsWith(prefix)) return c; return null; }
        /// <summary>The turret's own loose parts that fly alone when it bursts: the mantlet, the antennas, the hatch lid.</summary>
        public static bool LoosePart(string n) => n != null && (n.EndsWith("_mantlet") || n.Contains("Antennas") || n.Contains("HatchLid"));

        static void Move(Transform t, Transform set, HashSet<Renderer> keep)
        {
            t.SetParent(set, true);                                                                   // under the set: WreckPieces makes it its own piece (OwnPiece)
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) keep.Add(r);
        }

        /// <summary>Prepare, on a model with OZ_WreckTurret: each barrel's sections (its OZ_WreckBarrel) in place of the barrel, the turret's
        /// shell plates in place of the turret mesh (Wreck_Core stays on the flying turret), the mantlet, antennas and lid: all moved into the set
        /// at their pose in flight, so each is its own piece. False (nothing done) when the model has no such set.</summary>
        static bool TurretBreak(Transform root, Transform set, Transform tossed, HashSet<Renderer> keep)
        {
            var ts = Deep(tossed, TurretSet);
            if (!ts) return false;
            var slides = new List<Transform>();
            foreach (var t in tossed.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Recoil") && t.GetComponentInChildren<MeshRenderer>(true)) slides.Add(t);
            foreach (var t in slides)
            {
                var secs = Child(t, BarrelSet);
                if (!secs) { t.SetParent(root, true); BarrelsOff++; continue; }                     // no sections: the barrel whole, as before
                foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(true)) if (!mr.transform.IsChildOf(secs)) mr.enabled = false;
                secs.gameObject.SetActive(true);
                var list = new List<Transform>(); foreach (Transform c in secs) list.Add(c);
                foreach (var c in list) { Move(c, set, keep); SectionsOff++; }
                BarrelsOff++;
            }
            foreach (var mr in tossed.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name.EndsWith("_turret") && !mr.transform.IsChildOf(ts)) mr.enabled = false;
            ts.gameObject.SetActive(true);
            var shells = new List<Transform>(); foreach (Transform c in ts) if (c.name != CoreName) shells.Add(c);
            foreach (var c in shells) { Move(c, set, keep); ShellsOff++; }
            var loose = new List<Transform>();
            foreach (var mr in tossed.GetComponentsInChildren<MeshRenderer>(true)) if (mr.enabled && LoosePart(mr.name)) loose.Add(mr.transform);
            foreach (var t in loose) { Move(t, set, keep); LooseOff++; }
            return true;
        }

        /// <summary>WreckPieces.Break: the sideways throw for a piece of this size (1 = as before): a big tank's smaller pieces spread a little
        /// wider (x <see cref="SpreadSmall"/> at 0.4 m, x 1 from 2.4 m), still around the wreck.</summary>
        public static float Spread(UnitView v, Vector3 size)
        {
            if (!Finer || !IsBig(v)) return 1f;
            float s = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return Mathf.Lerp(SpreadSmall, 1f, Mathf.InverseLerp(0.4f, 2.4f, s));
        }

        /// <summary>WreckPieces.Break, after a big tank's break: its many small pieces rattle (View/OzBigTankWreckRattle.cs).</summary>
        public static void Rattle(UnitView v, List<Transform> made, Vector3 centre, int id)
        {
            if (!Finer || !IsBig(v) || made == null || made.Count == 0) return;
            var go = new GameObject("OZ_WreckRattle"); go.transform.position = centre;
            go.AddComponent<OzBigTankWreckRattle>().Begin(made, id);
            Rattles++;
        }

        /// <summary>false = WreckPieces.Break's ring throw for the big tanks (and the cans' old throw), as before (A/B).</summary>
        public static bool Scatter = true;
        /// <summary>Mean throw length (m, from where the piece starts) of the biggest pieces (2.6 m and more), of the smallest (0.5 m and less), of
        /// the belly halves and track chains (they drop and slide); the longest throw; the blast side's extra length (x 1 + SideBias there, x 1 -
        /// SideBias opposite); a cook-off's (power 0.55) length factor; the share pulled toward the vents and the share falling back in over the wreck,
        /// for the biggest .. the smallest pieces; a piece starting this far from the hull's middle (on the turret lying where it landed) is thrown any way.</summary>
        public static float HeavyMean = 2.2f, LightMean = 5.2f, SlideMean = 1.8f, MaxThrow = 12f, SideBias = 0.3f, CookOff = 0.72f,
                            VentHeavy = 0.35f, VentLight = 0.6f, InHeavy = 0.35f, InLight = 0.1f, FarStart = 5.5f, MaxSpeed = 11f;
        /// <summary>The Bizon's fuel cans: mean throw length (m), the share pulled toward the vents, the lift range (m/s), their body's damping (OzFuelCan).</summary>
        public static float CanMean = 3f, CanVent = 0.45f, CanLiftMin = 2.4f, CanLiftMax = 8f, CanDamping = 0.05f;
        /// <summary>Proof: pieces and cans thrown by <see cref="Launch"/> / <see cref="CanThrow"/>.</summary>
        public static int Scattered, CansScattered;
        static readonly System.Text.RegularExpressions.Regex SlideNames = new System.Text.RegularExpressions.Regex("belly|linkrun", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex RollNames = new System.Text.RegularExpressions.Regex("wheel|idler|guide", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>WreckPieces.Break: this break's pieces get <see cref="Launch"/>'s throw (a big tank, not crushed flat: a crushed wreck keeps
        /// bizon_crush's low throw under the tracks).</summary>
        public static bool Scatters(UnitView v) => Scatter && IsBig(v) && !OzBizonCrushView.Crushed(v.Obj);

        static float U(int id, int i, uint salt) => RobotDeathRule.Unit(id, i, salt);
        /// <summary>The side this death's blast throws most to (radians in the ground plane, x = cos, z = sin), seeded by the unit id.</summary>
        public static float BlastSide(int id) => U(id, 0, 61u) * 6.2831853f;
        /// <summary>The blast's three vents: its side and two more within +-110 deg of it (seeded per death).</summary>
        public static float Vent(int id, int k) => BlastSide(id) + (k == 0 ? 0f : (U(id, k, 62u) * 2f - 1f) * 1.92f);

        /// <summary>A throw length (m): a falling spread of this mean (truncated exponential: most short, a thinning tail up to MaxThrow).</summary>
        static float ThrowLength(float mean, float u)
        {
            mean = Mathf.Max(0.05f, mean);
            float k = 1f - Mathf.Exp(-MaxThrow / mean);
            return -mean * Mathf.Log(1f - u * k);
        }

        /// <summary>A share of the pieces (seeded pick) turned 50-90 % of the way toward the nearest vent: clumps and gaps, never a fan.</summary>
        static float ToVent(int id, int i, float ang, uint salt, float share)
        {
            if (U(id, i, salt) >= share) return ang;
            float bd = 99f;
            for (int k = 0; k < 3; k++) { float d = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, Vent(id, k) * Mathf.Rad2Deg) * Mathf.Deg2Rad; if (Mathf.Abs(d) < Mathf.Abs(bd)) bd = d; }
            return ang + bd * Mathf.Lerp(0.5f, 0.9f, U(id, i, salt + 1u));
        }

        /// <summary>The sideways speed that lands `d` m away for a lift `vy` from `h` m over the ground with linear damping `k` (gravity g).</summary>
        static float SideSpeed(float d, float vy, float h, float k, float g)
        {
            float t = (vy + Mathf.Sqrt(vy * vy + 2f * g * Mathf.Max(0f, h))) / g;
            return k > 1e-3f ? d * k / Mathf.Max(1e-3f, 1f - Mathf.Exp(-k * t)) : d / Mathf.Max(0.05f, t);
        }

        /// <summary>The throw of piece `i` (Break's index) of death `id`: its centre `at`, its bottom `h` m over the ground, its size, the blast centre,
        /// the break's power (1, a cook-off 0.55), its names (WreckPieces' launcher names = `launcher`: the barrel sections), its body's damping.
        /// Pure (no engine call): tools/parity.py runs these lines on .NET against tools/scatter_model.py.</summary>
        public static Vector3 ThrowVelocity(Vector3 at, float h, Vector3 size, Vector3 centre, float power, int id, int i, string names, bool launcher, float damping, float g, out Vector3 spin)
        {
            float ox = at.x - centre.x, oz = at.z - centre.z, r0 = Mathf.Sqrt(ox * ox + oz * oz);
            float light = 1f - Mathf.InverseLerp(0.5f, 2.6f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));    // 1 = 0.5 m and smaller, 0 = 2.6 m and bigger
            bool slide = SlideNames.IsMatch(names ?? ""), roll = RollNames.IsMatch(names ?? "");
            // where to: out from where it sits (a piece far off, on the lying turret: any way), some fall back in, jittered, a share toward the vents
            float ang = r0 > 0.15f && r0 < FarStart ? Mathf.Atan2(oz, ox) : U(id, i, 63u) * 6.2831853f;
            if (!launcher && U(id, i, 74u) < Mathf.Lerp(InHeavy, InLight, light)) ang += Mathf.PI;
            ang += (U(id, i, 64u) + U(id, i, 65u) - 1f) * (slide ? 1.5f : 0.8f);
            ang = ToVent(id, i, ang, 66u, Mathf.Lerp(VentHeavy, VentLight, light));
            // how far: a falling spread, heavier shorter, the blast side further, a cook-off shorter
            float mean = slide ? SlideMean : Mathf.Lerp(HeavyMean, LightMean, light);
            mean *= 1f + SideBias * Mathf.Cos(ang - BlastSide(id));
            mean *= Mathf.Lerp(CookOff, 1f, power);
            float d = ThrowLength(mean, U(id, i, 68u));
            // lift: wide ranges per piece (a cook-off lower, as before)
            float u = U(id, i, 69u);
            float vy = launcher ? Mathf.Lerp(8f, 13f, u) : slide ? Mathf.Lerp(0.6f, 2.8f, u) : roll ? Mathf.Lerp(1.2f, 4.5f, u)
                     : Mathf.Lerp(Mathf.Lerp(1.4f, 3.4f, light), Mathf.Lerp(4.2f, 8.6f, light), u);
            vy *= Mathf.Lerp(0.7f, 1f, power);
            float vh = Mathf.Min(MaxSpeed, SideSpeed(d, vy, h, damping, g));
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            // spin: wheels roll along the throw (on landing they roll on), 30 % of the rest tumble end over end along it, the others any axis
            var along = Vector3.Cross(Vector3.up, dir);
            float w = Mathf.Lerp(Mathf.Lerp(1f, 4f, light), Mathf.Lerp(4f, 12f, light), U(id, i, 70u));
            if (roll) spin = along * Mathf.Min(14f, vh / Mathf.Max(0.2f, 0.5f * size.y));
            else if (U(id, i, 71u) < 0.3f) spin = along * w;
            else
            {
                float z = U(id, i, 72u) * 2f - 1f, a = U(id, i, 73u) * 6.2831853f, r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                spin = new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a)) * w;
            }
            return dir * vh + Vector3.up * vy;
        }

        /// <summary>WreckPieces.Break, right after its own throw of a big tank's piece `i` (bounds `b`, its names): this throw instead (velocity and spin).</summary>
        public static void Launch(UnitView v, Rigidbody body, Bounds b, Vector3 centre, float power, int id, int i, string names, bool launcher)
        {
            if (!body) return;
            var gm = ZGame.I;
            float ground = gm ? gm.GroundY(b.center.x, b.center.z) : centre.y - 1f;
            body.linearVelocity = ThrowVelocity(b.center, b.min.y - ground, b.size, centre, power, id, i, names, launcher, body.linearDamping, Mathf.Max(1f, -Physics.gravity.y), out var spin);
            body.angularVelocity = spin;
            Scattered++;
        }

        /// <summary>A fuel can's throw (pure): out along `dir` (OzBigTanksFx's own jittered direction), turned toward the vents for a share, a falling
        /// length (uOut, uUp = the can's two old seeded draws, 0..1) from `h` m over the ground.</summary>
        public static Vector3 CanVelocity(int id, int i, Vector3 dir, float h, float uOut, float uUp, float g)
        {
            float ang = ToVent(id, 200 + i, Mathf.Atan2(dir.z, dir.x), 76u, CanVent);
            float d = ThrowLength(CanMean * (1f + SideBias * Mathf.Cos(ang - BlastSide(id))), uOut);
            float vy = Mathf.Lerp(CanLiftMin, CanLiftMax, uUp);
            float vh = SideSpeed(d, vy, h, CanDamping, g);
            return new Vector3(Mathf.Cos(ang) * vh, vy, Mathf.Sin(ang) * vh);
        }

        /// <summary>OzBigTanksFx.ThrowCans: can `i` of the Bizon `id` (see <see cref="CanVelocity"/>).</summary>
        public static Vector3 CanThrow(int id, int i, Vector3 dir, float h, float uOut, float uUp)
        {
            CansScattered++;
            return CanVelocity(id, i, dir, h, uOut, uUp, Mathf.Max(1f, -Physics.gravity.y));
        }

        /// <summary>Menu 2's count of the bodies one break makes from this model.</summary>
        public static int BreakPieces(Transform r, out string how)
        {
            int set = 0, wheels = 0, secs = 0, bars = 0, shells = 0, loose = 0;
            foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("TankWheel_")) wheels++;
            bool fine = Deep(r, TurretSet);
            foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(true))
            {
                var t = mr.transform;
                if (Under(t, SetNode)) set++;
                else if (Under(t, BarrelSet)) secs++;
                else if (Under(t, TurretSet)) { if (t.name != CoreName) shells++; }
                else if (fine && LoosePart(t.name)) loose++;
                else if (t.parent && t.parent.name.Contains("Recoil") && !Child(t.parent, BarrelSet)) bars++;
            }
            how = $"set {set} + road wheels {wheels} + barrel sections {secs} + whole barrels {bars} + turret shells {shells} + mantlet / antennas / lid {loose}";
            return set + wheels + secs + bars + shells + loose;
        }
    }
}
