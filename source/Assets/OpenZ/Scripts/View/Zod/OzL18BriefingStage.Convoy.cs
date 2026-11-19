// OpenZ (GPL-3.0) - version 1.5, level 18 briefing stage: the three blue PARADES (PLAY AS BLUE only).
// One clean single file per long bridge, smallest first, every unit centred on its lane, a clear gap to the next (half the smaller
// hull, never under MinGap):
//   west bridge:   a jeep, 4 robots in two files, a light tank, a medium tank, an MLRS, a heavy tank
//   east bridge:   a light tank, an APC, a medium tank, an MLRS, the Bizon last
// ONE PARADE PACE for all three (ConvoySpeed, a march: robots walk in step, the vehicles hold it). The pace is what the map gives: a
// deck is 24 m and a parade longer than that, so at any faster pace no unit could be both ON its deck in the first picture and still on
// it nine seconds later when its bridge goes. Each head is on its deck in the first frame of the flight and StopShort m short of the far
// (south) end when its bridge goes; every unit whose middle is on the deck then is THROWN by the charge nearest to it
// (OzL18BriefingStage.Blast.cs); the ONE unit behind the deck (each column's last and heaviest) brakes too late, slides over the broken
// north end nose first and goes up in the water (the Bizon never brakes: it never backs off). Nothing of a parade is left on a bank.
// NO COLUMN COMES OUT OF THE FORT: the west and east parades come straight down their own roads from the north; the middle one comes
// along the road that runs south of our fort (from the east) and wheels onto the bridge road through a rounded corner.
// A unit's place is a pure function of the clock. Wheels and road wheels turn with the metres driven, the Mammoth's and the Bizon's real
// track links run with them (OzTrackLinks.Step), the medium tank walks its own legs (the classic cycle: its planted-foot walk reads the
// rules' ground, and the rules hold these bridges destroyed), robots walk their own clip at the pace's rate. DUST AND EXHAUST: the
// game's own road dust behind every driving vehicle and its exhaust channel, paced by the speed; only while the clock runs forward.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        /// <summary>The parades: pace (m/s), how far short of the far (south) end the head's nose is when its bridge goes (m), the least gap
        /// between two units (m), the gap as a share of the smaller hull, a squad's rows apart and its files' half distance (m).</summary>
        public static float ConvoySpeed = 1.8f, StopShort = 0.5f, MinGap = 1.0f, GapShare = 0.5f, RowGap = 1.35f, FileHalf = 0.8f;
        /// <summary>The three parades, west to east, head first (see the file's head). grunt4 / grunt6 = a squad in two files.</summary>
        public static string[][] Parade =
        {
            new[] { "jeep", "grunt4", "light", "medium", "mlrs", "heavy" },
            new[] { "banner", "jeep", "grunt6", "medium", "heavy", "mammoth" },
            new[] { "light", "apc", "medium", "mlrs", "bizon" },
        };
        /// <summary>The model's nose: every unit model of the game faces -Z (UnitView.modelYaw). 180 turns the stage's units if a model says otherwise.</summary>
        public static float ModelYaw = 180f;
        /// <summary>Where a parade's road begins north of its deck (m, west and east: straight north). The middle parade: the east-west road's
        /// middle line this far north of the deck's north end, the parade comes along it from the east (false: from the west), the corner's
        /// radius, the length of road before the corner (m).</summary>
        public static float RunIn = 100f, MidRoadNorth = 10f, MidCorner = 4.5f, MidRunIn = 46f; public static bool MidFromEast = true;
        /// <summary>The small rock of a hull on the move (deg).</summary>
        public static float HullRock = 0.5f;
        /// <summary>The unit behind the deck: seconds from the blast to the brake (nobody reacts faster), the braking (m/s2), the least speed it
        /// keeps under locked tracks (share of the pace), how far the broken stump stands out over the water at the north end (m), the nose
        /// dip under the brakes (deg).</summary>
        public static float ReactSeconds = 0.3f, BrakeDecel = 1.5f, BrakeMin = 0.9f, StumpOut = 1.5f, BrakeDip = 3.5f;
        /// <summary>Dust and exhaust of a driving vehicle: puffs a metre driven (both tracks), a puff's final size (m), its strength, the ground
        /// colour, the share of it on a steel deck; exhaust puffs a second, size (m), strength.</summary>
        public static float DustPerMetre = 0.6f, DustSize = 1.9f, DustAlpha = 0.42f, DeckDust = 0.25f, SmokeRate = 4f, SmokeSize = 0.42f, SmokeAlpha = 0.3f;
        public static Color DustColor = new Color(0.47f, 0.44f, 0.40f, 1f), SmokeColor = new Color(0.34f, 0.33f, 0.32f, 1f);
        /// <summary>The robots' walking clip at the parade pace: its own ground speed at 1x (m/s; a humanoid uses the game's measured stride x its scale).</summary>
        public static float WalkNative = 1.23f;
        /// <summary>Play checks: units that went over a broken end, dust puffs and exhaust puffs emitted, the measured models, the per-unit table of the last flight.</summary>
        public static int RolledOff, DustPuffs, SmokePuffs; public static string ModelSizes = "", UnitTable = "";

        enum Kind { Grunt, Banner, Jeep, Light, Medium, Heavy, Mammoth, Mlrs, Apc, Bizon }

        sealed class Car
        {
            public Transform root, model, turret, turretHome; public Vector3 turretPos, tRelPos; public Quaternion turretRot = Quaternion.identity, tRel = Quaternion.identity;
            public Kind kind; public string label = ""; public bool robot, big;
            public int span, slot; public float offset, lateral; public bool mammoth, driving, launched, splashed, sunk, turretSplashed, blown;
            public TeamPaint paint; public OzTrackLinks links; public Animator anim; public MechWalk walk; public string animState = "";
            public readonly List<Transform> wheels = new List<Transform>(); public readonly List<Quaternion> wheelRest = new List<Quaternion>(); public readonly List<float> wheelR = new List<float>();
            public Renderer[] rends; public float travelled; public uint seed;
            // the model's own size, in its holder's frame (the holder's +Z is the way it drives): nose, tail, half width, height (m)
            public float nose = 3.3f, tail = -3.3f, halfW = 1.7f, high = 2.6f;
            /// <summary>The body's middle over its feet (m): it tumbles about it. The throw's own numbers by kind: lift-off, spin, side.</summary>
            public float mid = 1.2f, upK = 1f, spinK = 1f, sideK = 1f;
            // the throw (set when its column is built: a pure function of the clock needs them from the start)
            public float launchAt, waterAt, turretWaterAt; public Vector3 p0, v0, axis, tAxis, tV0; public Quaternion r0; public float spin, tSpin; public bool hasThrow;
            // the unit behind the deck: when it brakes, the road metres there, when its middle passes the broken end, its speed there, when it meets the water
            public bool behind, squealed, crumbled, lives, streak; public float brakeAt, sBrake, edgeAt, edgeSpeed, sEdge, rollWaterAt, dustAcc, smokeAcc, brakeMin, sStop;
            // the per-unit table: where it is when its bridge goes, what happens to it
            public float zAtBlast; public string fate = "";
            public float Len => nose - tail; public float Mid => (nose + tail) * 0.5f;
        }
        readonly List<Car> cars = new List<Car>();

        static float Rnd(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }

        GameObject PrefabOf(Kind k)
        {
            switch (k)
            {
                case Kind.Banner: { var p = game.Prefab("Z_bs01_psycho"); if (!p) p = game.Prefab("Z_bs01_grunt"); return p ? p : OldRobot("banner carrier"); }   // owner addition 10: a blue Psycho carries the banner
                case Kind.Grunt: { var p = game.Prefab("Z_bs01_grunt"); return p ? p : OldRobot("squad robot"); }
                case Kind.Jeep: return game.Prefab("Z_jeep");
                case Kind.Light: return game.Prefab("Z_light");
                case Kind.Medium: return game.Prefab("Z_medium");
                case Kind.Heavy: { var p = game.Prefab(OzHeavyTank.V1Key); return p ? p : game.Prefab(OzHeavyTank.V2Key); }
                case Kind.Mlrs: return game.Prefab("Z_missile_launcher");
                case Kind.Apc: return game.Prefab("Z_apc");
                case Kind.Mammoth: { var p = game.Prefab(OzBigTanks.MammothKey); return p ? p : Resources.Load<GameObject>(OzBigTanks.Dir + OzBigTanks.MammothKey); }
                case Kind.Bizon: { var p = game.Prefab(OzBigTanks.BizonKey); return p ? p : Resources.Load<GameObject>(OzBigTanks.Dir + OzBigTanks.BizonKey); }
            }
            return null;
        }

        static bool KindOf(string name, out Kind k, out int count)
        {
            count = 1; k = Kind.Heavy;
            switch (name)
            {
                case "banner": k = Kind.Banner; return true;
                case "jeep": k = Kind.Jeep; return true;
                case "light": k = Kind.Light; return true;
                case "medium": k = Kind.Medium; return true;
                case "heavy": k = Kind.Heavy; return true;
                case "mammoth": k = Kind.Mammoth; return true;
                case "mlrs": k = Kind.Mlrs; return true;
                case "apc": k = Kind.Apc; return true;
                case "bizon": k = Kind.Bizon; return true;
            }
            if (name.StartsWith("grunt")) { k = Kind.Grunt; if (!int.TryParse(name.Substring(5), out count)) count = 4; count = Mathf.Clamp(count, 1, 12); return true; }
            return false;
        }

        void BuildConvoy()
        {
            BuildDecks();
            string sizes = ""; var seen = new HashSet<Kind>();
            for (int si = 0; si < spans.Count && si < Parade.Length; si++)
            {
                var sp = spans[si];
                if (!sp.intact) continue;                                                              // no deck to drive on: no parade on this bridge
                BuildPath(sp, si);
                var column = new List<Car>(); var groups = new List<List<Car>>(); int slot = 0;
                foreach (string name in Parade[si])
                {
                    if (!KindOf(name, out Kind kind, out int count)) { Warn("unknown parade unit '" + name + "'"); continue; }
                    if (!blue && kind == Kind.Banner) kind = Kind.Grunt;                                // the banner and its pay-off are the blue briefing's: in the red one its carrier marches as a Grunt
                    var pf = PrefabOf(kind);
                    if (!pf) { Warn("no model for the parade unit '" + name + "' among the game's prefabs: the parade goes without it"); continue; }
                    var group = new List<Car>();
                    for (int n = 0; n < count; n++)
                    {
                        var car = MakeCar(kind, name, si, slot++, pf);
                        if (kind == Kind.Grunt) car.label = "robot " + (n + 1) + " of " + count;
                        group.Add(car); column.Add(car);
                        if (seen.Add(kind)) sizes += (sizes.Length > 0 ? "; " : "") + $"{kind} {car.Len:F2} m long (nose {car.nose:F2}, tail {car.tail:F2}), {car.halfW * 2f:F2} wide, {car.high:F2} high";
                    }
                    groups.Add(group);
                }
                PlaceColumn(sp, groups);
                bool firstBehind = true;
                foreach (var car in column)
                {
                    RoadPose(sp, RoadAt(car, sp, sp.blastAt), out var at, out _);
                    car.zAtBlast = at.z;
                    if (car.behind) { car.lives = !firstBehind; firstBehind = false; PlanRollOff(car, sp); } else PlanThrow(car, sp);
                    car.root.gameObject.SetActive(false);
                    cars.Add(car);
                }
            }
            ModelSizes = sizes;
            if (sizes.Length > 0) Debug.Log("[OpenZ L18 briefing] the parades' models, measured: " + sizes);
            WriteUnitTable();
        }

        Car MakeCar(Kind kind, string name, int si, int slot, GameObject pf)
        {
            var holder = new GameObject("L18Parade_" + si + "_" + slot + "_" + name).transform; holder.SetParent(root, false);
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var go = Instantiate(pf, holder, false); go.name = "Model";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) Destroy(a);
            var car = new Car { root = holder, model = go.transform, kind = kind, label = kind == Kind.Banner ? "banner Psycho" : name, span = si, slot = slot, seed = (uint)(977 + si * 131 + slot * 17) };
            car.robot = kind == Kind.Grunt || kind == Kind.Banner; car.mammoth = kind == Kind.Mammoth; car.big = kind == Kind.Mammoth || kind == Kind.Bizon;
            car.paint = go.GetComponent<TeamPaint>(); if (!car.paint) car.paint = go.AddComponent<TeamPaint>();
            car.paint.Team = Team.Blue;
            if (kind == Kind.Apc) WheelSplit.Apply(go, "apc");                                           // its wheels cut out to spin, as the game's own APC
            car.links = go.GetComponentInChildren<OzTrackLinks>(true); if (car.links && !car.links.Valid) car.links = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!car.turret && !car.robot && kind != Kind.Medium && n.EndsWith("TurretYaw")) car.turret = t;
                if (n.StartsWith("WheelSpin_") || n.StartsWith("Wheel_") || System.Text.RegularExpressions.Regex.IsMatch(n, "^TankWheel_\\d+$"))
                {
                    var mf = t.GetComponentInChildren<MeshFilter>();
                    float r = mf && mf.sharedMesh ? Mathf.Max(mf.sharedMesh.bounds.size.y, mf.sharedMesh.bounds.size.z) * 0.5f * t.lossyScale.y : 0.5f;
                    car.wheels.Add(t); car.wheelRest.Add(t.localRotation); car.wheelR.Add(Mathf.Max(0.2f, r));
                }
            }
            if (car.turret)
            {
                car.turretHome = car.turret.parent; car.turretPos = car.turret.localPosition; car.turretRot = car.turret.localRotation;
                car.tRelPos = holder.InverseTransformPoint(car.turret.position); car.tRel = Quaternion.Inverse(holder.rotation) * car.turret.rotation;   // its place and turn in the unit's own frame
            }
            car.rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in car.rends) { r.allowOcclusionWhenDynamic = false; r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; }   // a living unit: crisp (Streak below)
            if (car.robot)
            {
                car.anim = go.GetComponentInChildren<Animator>(true);
                if (car.anim) { car.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; car.anim.applyRootMotion = false; }
                if (kind == Kind.Grunt) WeaponHold.Attach(go);                                           // the rifle in his hands, as every robot of the game
                car.nose = 0.5f; car.tail = -0.5f; car.halfW = 0.6f; car.high = 3.3f; car.mid = 1.5f; car.upK = 1.35f; car.spinK = 2.4f; car.sideK = 1.35f;
                if (kind == Kind.Banner) BuildBanner(car);
            }
            else
            {
                Measure(car);
                car.walk = go.GetComponentInChildren<MechWalk>(true); if (car.walk) car.walk.StageClassic = true;   // the medium tank's legs: the classic cycle (see the file's head)
                switch (kind)
                {
                    case Kind.Jeep: car.mid = 1.0f; car.upK = 1.3f; car.spinK = 1.7f; car.sideK = 1.2f; break;
                    case Kind.Apc: case Kind.Light: car.mid = 1.1f; car.upK = 1.1f; car.spinK = 1.25f; break;
                    case Kind.Medium: car.mid = 1.7f; car.upK = 1.15f; car.spinK = 1.3f; break;
                    case Kind.Mlrs: car.mid = 1.4f; break;
                    case Kind.Mammoth: case Kind.Bizon: car.mid = 1.35f; car.upK = 0.88f; car.spinK = 0.8f; break;
                }
            }
            return car;
        }

        /// <summary>The model's box in its holder's frame (the holder stands at the origin, unturned, while it is measured): the nose is the
        /// far end along the way it drives. Effects' renderers are not the hull.</summary>
        static void Measure(Car c)
        {
            bool any = false; var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in c.rends)
            {
                if (!r || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any || b.size.z < 0.5f) return;                                                       // nothing measurable: the default hull
            c.nose = b.max.z; c.tail = b.min.z; c.halfW = Mathf.Max(0.6f, b.extents.x); c.high = Mathf.Max(1f, b.max.y);
        }

        /// <summary>The parade nose to tail: every unit's place behind the head's nose (m along the road), which of them have their middle on
        /// the deck when the bridge goes (those are thrown) and which is behind it (it goes over the broken end). A squad marches in two files.</summary>
        void PlaceColumn(Span sp, List<List<Car>> groups)
        {
            float deck = sp.half * 2f; sp.stopShort = StopShort;
            float at = 0f, prevLen = 0f;                                                                // metres behind the head's nose where the next group's front is
            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g]; var first = grp[0]; float len;
                if (first.kind == Kind.Grunt && grp.Count > 1)
                {
                    int rows = (grp.Count + 1) / 2; len = (rows - 1) * RowGap + first.Len;
                    if (g > 0) at += Mathf.Max(MinGap, GapShare * Mathf.Min(prevLen, len));
                    for (int n = 0; n < grp.Count; n++)
                    {
                        var c = grp[n]; int row = n / 2;
                        c.offset = at + row * RowGap + c.nose; c.lateral = (n % 2 == 0 ? -1f : 1f) * FileHalf;
                    }
                }
                else
                {
                    len = first.Len;
                    if (g > 0) at += Mathf.Max(MinGap, GapShare * Mathf.Min(prevLen, len));
                    first.offset = at + first.nose; first.lateral = 0f;
                }
                foreach (var c in grp)
                {
                    float midFromSouth = sp.stopShort + c.offset - c.Mid;                               // the body's middle, metres north of the deck's south end at the blast
                    c.behind = midFromSouth > deck - 0.1f;
                }
                at += len; prevLen = len;
            }
            int thrown = 0, all = 0; foreach (var grp in groups) foreach (var c in grp) { all++; if (!c.behind) thrown++; }
            Debug.Log($"[OpenZ L18 briefing] parade at x {sp.c.x:F0}: {all} units, {thrown} with their middle on the {deck:F1} m deck when it goes (the head's nose {sp.stopShort:F1} m short of the far end), {all - thrown} behind it, {at:F1} m nose to tail");
        }

        // ------------------------------------------------------------------------------------------------ the road of a parade
        /// <summary>The parade's road as a line of points ending at the deck's south end (the red bank). West and east: straight south. The
        /// middle one: along the east-west road south of our fort, then a rounded corner onto the bridge road (nothing comes out of the fort).</summary>
        void BuildPath(Span sp, int index)
        {
            var pts = new List<Vector3>();
            float zN = sp.c.z + sp.half, zS = sp.c.z - sp.half;
            bool middle = spans.Count >= 3 && index == spans.Count / 2;
            if (middle)
            {
                float zc = zN + MidRoadNorth, dir = MidFromEast ? 1f : -1f, r = Mathf.Min(MidCorner, MidRoadNorth - 1f);
                pts.Add(new Vector3(sp.c.x + dir * (r + MidRunIn), 0f, zc));
                Vector3 centre = new Vector3(sp.c.x + dir * r, 0f, zc - r);
                for (int i = 0; i <= 8; i++) { float a = i / 8f * Mathf.PI * 0.5f; pts.Add(centre + new Vector3(-dir * r * Mathf.Sin(a), 0f, r * Mathf.Cos(a))); }
            }
            else pts.Add(new Vector3(sp.c.x, 0f, zN + RunIn));
            pts.Add(new Vector3(sp.c.x, 0f, zS));
            sp.path = pts.ToArray(); sp.pathLen = new float[pts.Count]; sp.total = 0f;
            for (int i = 1; i < pts.Count; i++) { sp.total += Vector3.Distance(pts[i - 1], pts[i]); sp.pathLen[i] = sp.total; }
        }

        static Vector3 OnLine(Span sp, float s)
        {
            if (s <= 0f) return sp.path[0] + (sp.path[0] - sp.path[1]).normalized * -s;                   // before the road's first point: straight on backward
            if (s >= sp.total) { int n = sp.path.Length; return sp.path[n - 1] + (sp.path[n - 1] - sp.path[n - 2]).normalized * (s - sp.total); }
            for (int i = 1; i < sp.path.Length; i++)
                if (s <= sp.pathLen[i] || i == sp.path.Length - 1)
                {
                    float a = sp.pathLen[i - 1], b = sp.pathLen[i];
                    return Vector3.Lerp(sp.path[i - 1], sp.path[i], b > a ? (s - a) / (b - a) : 0f);
                }
            return sp.path[sp.path.Length - 1];
        }

        /// <summary>The place and the heading on the road at `s` metres: the height is the ground's, the deck's over the water, eased over
        /// the last metre and a half at each deck end.</summary>
        void RoadPose(Span sp, float s, out Vector3 pos, out Quaternion rot)
        {
            Vector3 a = OnLine(sp, s - 1f), b = OnLine(sp, s + 1f);
            pos = OnLine(sp, s);
            Vector3 dir = b - a; dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.back;
            pos.y = RoadY(sp, pos.x, pos.z);
            rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        float RoadY(Span sp, float x, float z)
        {
            float outN = z - (sp.c.z + sp.half), outS = (sp.c.z - sp.half) - z;                           // metres past the deck's north / south end (below 0 = over the deck)
            float past = Mathf.Max(outN, outS);
            if (past <= 0f) return sp.deckY;
            float g = game.GroundY(x, z);
            return past >= 1.5f ? g : Mathf.Lerp(sp.deckY, Mathf.Max(g, sp.deckY - 0.5f), Mathf.SmoothStep(0f, 1f, past / 1.5f));
        }

        /// <summary>Metres along the road of this unit's own point at the clock's second (it holds the pace until it is thrown or brakes).</summary>
        float RoadAt(Car c, Span sp, float t) => sp.total - sp.stopShort - ConvoySpeed * (sp.blastAt - t) - c.offset;

        // ------------------------------------------------------------------------------------------------ the unit behind the deck
        /// <summary>Written when the parade is built: when this unit brakes (ReactSeconds after the blast; the Bizon never does), when its
        /// middle passes the end of the stump, how fast it still is there, when it meets the water. A second unit behind (none in the
        /// parades as written) stops short of the end and lives.</summary>
        void PlanRollOff(Car c, Span sp)
        {
            float v0 = Mathf.Max(0.5f, ConvoySpeed); c.brakeMin = c.kind == Kind.Bizon ? 1f : Mathf.Clamp01(BrakeMin);
            float vmin = v0 * c.brakeMin, a = Mathf.Max(0.1f, BrakeDecel);
            float sEnd = sp.total - sp.half * 2f + StumpOut;                                            // the road metres where the broken north stump ends
            c.sEdge = sEnd - c.Mid;                                                                     // its own point there when its middle is over the end
            c.brakeAt = sp.blastAt + ReactSeconds;
            c.sBrake = RoadAt(c, sp, c.brakeAt);
            c.hasThrow = false;
            if (c.lives)
            {   // it has room: full brakes, it stands with its nose two metres short of the broken end
                c.sStop = Mathf.Min(sEnd - c.nose - 2f, c.sBrake + v0 * v0 / (2f * 3.5f));
                c.edgeAt = c.rollWaterAt = float.MaxValue; c.fate = "brakes and stands short of the broken end, alive";
                return;
            }
            float d = c.sEdge - c.sBrake, tau;
            if (d <= 0f) { tau = 0f; c.edgeSpeed = v0; c.brakeAt = sp.blastAt + (c.sEdge - RoadAt(c, sp, sp.blastAt)) / v0; c.sBrake = c.sEdge; }   // already over: no brake at all
            else
            {
                float t1 = (v0 - vmin) / a, d1 = v0 * t1 - 0.5f * a * t1 * t1;
                if (vmin >= v0 - 1e-3f) { tau = d / v0; c.edgeSpeed = v0; }
                else if (d <= d1) { tau = (v0 - Mathf.Sqrt(Mathf.Max(0f, v0 * v0 - 2f * a * d))) / a; c.edgeSpeed = v0 - a * tau; }
                else { tau = t1 + (d - d1) / vmin; c.edgeSpeed = vmin; }
            }
            c.edgeAt = c.brakeAt + tau;
            float drop = Mathf.Max(0.2f, sp.deckY + c.mid - (sp.waterY + 0.6f));
            c.rollWaterAt = c.edgeAt + Mathf.Sqrt(2f * drop / Gravity);
            c.fate = (c.kind == Kind.Bizon ? "never brakes, drives over the broken end" : "brakes too late, slides over the broken end") + $" at {c.edgeAt + BluePressClock:F2} s, in the water {c.rollWaterAt + BluePressClock:F2} s, goes up in it";
        }

        /// <summary>The road metres of a braking unit's own point `tau` seconds after its brake (it never goes under its least speed).</summary>
        float BrakeRun(Car c, float tau, out float speed)
        {
            float v0 = Mathf.Max(0.5f, ConvoySpeed), vmin = v0 * c.brakeMin, a = Mathf.Max(0.1f, BrakeDecel), t1 = (v0 - vmin) / a;
            if (vmin >= v0 - 1e-3f) { speed = v0; return c.sBrake + v0 * tau; }
            if (tau <= t1) { speed = v0 - a * tau; return c.sBrake + v0 * tau - 0.5f * a * tau * tau; }
            speed = vmin; return c.sBrake + v0 * t1 - 0.5f * a * t1 * t1 + vmin * (tau - t1);
        }

        void PlaceBehind(Car c, Span sp, float t, bool forward, float dt)
        {
            if (t < c.edgeAt)
            {   // on the road: at the pace, then under the brakes
                if (c.launched || c.blown || c.sunk) { c.launched = c.splashed = c.sunk = c.blown = false; if (c.paint) c.paint.Charred = 0f; }
                bool braking = t >= c.brakeAt; float speed = ConvoySpeed, s;
                if (!braking) s = RoadAt(c, sp, t);
                else if (c.lives)
                {   // the full stop: an even brake from the pace to rest at its stopping point
                    float dist = Mathf.Max(0.05f, c.sStop - c.sBrake), T = 2f * dist / Mathf.Max(0.5f, ConvoySpeed), u = Mathf.Clamp01((t - c.brakeAt) / T);
                    s = c.sBrake + dist * (2f * u - u * u); speed = ConvoySpeed * (1f - u);
                }
                else s = BrakeRun(c, t - c.brakeAt, out speed);
                c.driving = speed > 0.05f;
                bool locked = braking && c.brakeMin < 0.999f;
                if (!c.root.gameObject.activeSelf) c.root.gameObject.SetActive(true);
                RoadPose(sp, s, out var p, out var r);
                float rock = Mathf.Sin(s * 1.9f + c.seed % 7) * HullRock;
                float dip = locked ? BrakeDip * Mathf.SmoothStep(0f, 1f, (t - c.brakeAt) / 0.18f) * Mathf.Clamp01(speed / Mathf.Max(0.3f, ConvoySpeed) + 0.25f) : 0f;   // the nose goes down under the brakes
                c.root.SetPositionAndRotation(p, r * Quaternion.Euler(rock + dip, 0f, rock * 0.4f));
                if (!locked) Roll(c, speed * dt);                                                        // locked tracks do not turn
                if (forward && dt > 0f)
                {
                    if (c.driving) Wake(c, sp, p, r, speed, dt, locked ? 2.6f : 1f);                     // skid dust under the locked tracks
                    if (locked && !c.squealed) { c.squealed = true; Play("brief_l18_brake", (c.slot + c.span) % 2 == 0 ? "sd_tracks_squeal_a" : "sd_tracks_squeal_b", GainBrake, 1f, false, 0.86f + 0.05f * (c.span % 3)); }
                }
                if (!braking) c.squealed = false;
                c.crumbled = false;
                return;
            }
            // over the broken end: the middle is past the stump, the nose goes down, the fall, the water
            c.driving = false;
            float uu = t - c.edgeAt, fall = Mathf.Min(uu, c.rollWaterAt - c.edgeAt);
            RoadPose(sp, c.sEdge, out var pe, out var re);
            Vector3 fwd = re * Vector3.forward;
            Vector3 mid = pe + Vector3.up * c.mid + fwd * (c.Mid + c.edgeSpeed * fall) + Vector3.down * (0.5f * Gravity * fall * fall);
            float pitch = Mathf.Atan2(Gravity * fall, Mathf.Max(0.5f, c.edgeSpeed)) * Mathf.Rad2Deg;      // the nose follows the fall (an arrow's flight)
            if (!c.launched) { c.launched = true; if (forward) RolledOff++; }
            if (!c.crumbled)
            {   // the stump's lip breaks under the hull as it tips: scrap, dust, the groan
                c.crumbled = true;
                if (forward)
                {
                    var lip = new Vector3(sp.c.x, sp.deckY, sp.c.z + sp.half - StumpOut);
                    var fx = FxManager.I; if (fx) { fx.Breakup(lip, "metal", 3.5f, 9); fx.Dust(lip + Vector3.up * 0.4f, 0.8f); }
                    Chunks(sp, lip, 2);
                    Play("brief_l18_lip_" + c.span, c.span % 2 == 0 ? "sd_structure_groan_b" : "sd_structure_groan_a", GainGroan * GainLipK, 1f, false, 1.12f);
                }
            }
            bool inWater = t >= c.rollWaterAt; float w = t - c.rollWaterAt;
            if (inWater)
            {
                mid += fwd * (c.edgeSpeed * 0.35f * (1f - Mathf.Exp(-3f * w)) / 3f) + Vector3.down * (0.4f * (1f - Mathf.Exp(-5f * w)) + SinkSpeed * w);
                pitch = Mathf.Min(82f, pitch + 20f * (1f - Mathf.Exp(-3f * w)));
                Vector3 surface = new Vector3(mid.x, sp.waterY, mid.z);
                if (!c.splashed)
                {
                    c.splashed = true;
                    if (forward)
                    {
                        WaterJet(surface, c.big ? 1.0f : 0.75f);
                        Steam(surface, 10);
                        Play("brief_l18_roll_splash", (c.slot + c.span) % 2 == 0 ? "sd_body_splash_x_a" : "sd_body_splash_x_b", GainHullSplash * 1.25f, 1f, false, c.big ? 0.82f : 0.95f);
                        Play("brief_l18_roll_sizzle", "sd_robot_death_fire_sizzle", GainSizzle, 1f);
                    }
                }
                SinkOrBlow(c, sp, surface, w, forward);
            }
            else if (c.splashed || c.sunk || c.blown) { c.splashed = c.sunk = c.blown = false; }       // the clock went back before its splash
            bool show = !c.sunk;
            if (c.root.gameObject.activeSelf != show) c.root.gameObject.SetActive(show);
            if (!show) return;
            var rot = re * Quaternion.Euler(pitch, 0f, 0f);
            c.root.SetPositionAndRotation(mid - rot * new Vector3(0f, c.mid, c.Mid), rot);
        }

        // ------------------------------------------------------------------------------------------------ wheels, tracks, legs, dust, exhaust
        void Roll(Car c, float moved)
        {
            c.travelled += moved;
            if (c.links) c.links.Step(c.travelled, 0f, c.model);
            for (int i = 0; i < c.wheels.Count; i++) if (c.wheels[i]) c.wheels[i].localRotation = c.wheelRest[i] * Quaternion.AngleAxis(-c.travelled / c.wheelR[i] * Mathf.Rad2Deg, Vector3.right);
        }

        /// <summary>A robot's legs: the walking clip at the pace's rate (the game's own rule: the clip's rate follows the drawn speed), the
        /// rifle held forward over them. The banner Psycho's arms are his banner's (Banner.cs).</summary>
        void Walk(Car c, float speed)
        {
            if (!c.anim) return;
            float native = c.anim.isHuman ? UnitView.WalkStrideHu * c.anim.humanScale * Mathf.Max(0.1f, c.anim.transform.lossyScale.y) : WalkNative;
            string want = speed > 0.1f ? "walking" : "idle";
            if (c.animState != want)
            {
                c.animState = want; c.anim.CrossFadeInFixedTime(want, 0.12f, 0, (c.seed % 97) / 97f * 0.6f);
                if (c.anim.layerCount > 1 && c.kind == Kind.Grunt) c.anim.CrossFadeInFixedTime("aim_hold", 0.1f, 1, 0f);
            }
            c.anim.speed = speed > 0.1f ? Mathf.Clamp(speed / Mathf.Max(0.2f, native), 0.35f, 3.2f) * (1f + ((c.seed * 0.618f) % 1f - 0.5f) * 0.04f) : 1f;
        }

        /// <summary>The game's own road dust behind both tracks and its exhaust over the engine deck, paced by the metres driven.</summary>
        void Wake(Car c, Span sp, Vector3 p, Quaternion r, float speed, float dt, float dustK)
        {
            if (c.robot) return;
            Vector3 fwd = r * Vector3.forward, right = r * Vector3.right;
            bool deck = Mathf.Abs(p.z - sp.c.z) <= sp.half && Mathf.Abs(p.x - sp.c.x) < 4f;
            float sizeK = c.big ? 1.15f : c.kind == Kind.Heavy || c.kind == Kind.Mlrs ? 1f : c.kind == Kind.Medium ? 0.6f : 0.75f;
            c.dustAcc += speed * dt * DustPerMetre * dustK;
            while (c.dustAcc >= 1f)
            {
                c.dustAcc -= 1f;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Vector3 at = p + fwd * (c.tail + Random.Range(-0.4f, 0.2f)) + right * (sgn * c.halfW * Random.Range(0.6f, 0.85f)) + Vector3.up * 0.25f;
                    Vector3 vel = -fwd * Random.Range(0.3f, 0.9f) + right * (sgn * Random.Range(0.1f, 0.45f)) + Vector3.up * Random.Range(0.35f, 0.7f);
                    var col = DustColor; col.a = Mathf.Clamp01(DustAlpha * (deck ? DeckDust : 1f) * Random.Range(0.75f, 1.05f));
                    float size = DustSize * sizeK * Random.Range(0.85f, 1.1f);
                    if (!OzLevelDust.BigTankPuff(at, vel, Random.Range(1.5f, 2.3f), size, col)) { if (sgn < 0 && FxManager.I) FxManager.I.Dust(at, 0.28f * (deck ? DeckDust : 1f)); }
                    DustPuffs++;
                }
            }
            var pl = OpenZFxPlayer.I;
            if (!pl || !pl.HasChannel("leg_smoke") || c.kind == Kind.Medium) return;
            c.smokeAcc += dt * SmokeRate * Mathf.Clamp01(speed / Mathf.Max(1f, ConvoySpeed) + 0.25f) * (c.big ? 1.2f : 0.8f);
            while (c.smokeAcc >= 1f)
            {
                c.smokeAcc -= 1f;
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 mouth = p + fwd * (c.tail * 0.72f) + right * (side * c.halfW * 0.45f) + Vector3.up * (c.high * 0.78f);
                pl.EmitChannel("leg_smoke", mouth, -fwd * Random.Range(0.4f, 0.9f) + Vector3.up * Random.Range(0.7f, 1.2f), Random.Range(1.1f, 1.7f), SmokeSize * (c.big ? 1.2f : 0.85f), SmokeColor, SmokeAlpha);
                SmokePuffs++;
            }
        }

        /// <summary>Living units write no object motion (the game's rule, UnitLookMotionGate: the motion blur smears a walking robot's skin
        /// into a dithered zipper: the parade's robots were grey dotted shapes); a hull or turret thrown by its blast streaks like every
        /// flying part only with StreakThrown (off: on a hull this big and this near the blur's dither reads as a fault, take 7). Robots never.</summary>
        public static bool StreakThrown = false;
        void Streak(Car c, bool on)
        {
            if (c.robot || !StreakThrown) on = false;
            if (c.streak == on || c.rends == null) return;
            c.streak = on;
            foreach (var r in c.rends) if (r) r.motionVectorGenerationMode = on ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.Camera;
        }

        // ------------------------------------------------------------------------------------------------ frame
        void PlaceCars(float t, bool forward)
        {
            float dt = forward ? Mathf.Max(0f, t - prevTau) : 0f;
            foreach (var c in cars)
            {
                var sp = spans[c.span];
                if (c.behind) { PlaceBehind(c, sp, t, forward, dt); continue; }
                bool thrown = c.hasThrow && t >= c.launchAt;
                c.driving = !thrown; Streak(c, thrown);
                if (!thrown)
                {
                    if (c.launched) Unthrow(c);                                                            // the clock went back before its blast (a held frame of an earlier shot)
                    float s = RoadAt(c, sp, t);
                    if (!c.root.gameObject.activeSelf) c.root.gameObject.SetActive(true);
                    RoadPose(sp, s, out var p, out var r);
                    if (c.robot)
                    {
                        c.root.SetPositionAndRotation(p + r * new Vector3(c.lateral, 0f, 0f), r);
                        Walk(c, ConvoySpeed);
                        if (c.kind == Kind.Banner) CarryBanner(c, t);
                        continue;
                    }
                    float rock = Mathf.Sin(s * 1.9f + c.seed % 7) * HullRock * (c.kind == Kind.Medium ? 0f : 1f);
                    c.root.SetPositionAndRotation(p, r * Quaternion.Euler(rock, 0f, rock * 0.4f));
                    Roll(c, ConvoySpeed * dt);
                    if (forward && dt > 0f) Wake(c, sp, p, r, ConvoySpeed, dt, 1f);
                    continue;
                }
                PlaceThrown(c, sp, t, forward);
            }
            TickBanner(t, forward);
        }

        /// <summary>The per-unit table of the flight just built (every death has a visible cause): where each unit is when its bridge
        /// goes, what happens to it. Logged, and kept in UnitTable for the Play check.</summary>
        void WriteUnitTable()
        {
            var sb = new System.Text.StringBuilder(); string[] names = { "WEST", "MIDDLE", "EAST" };
            sb.AppendLine("level 18 blue briefing, the parades (voice clock = stage clock + " + BluePressClock.ToString("F2") + " s):");
            foreach (var c in cars)
            {
                var sp = spans[c.span]; float zN = sp.c.z + sp.half, zS = sp.c.z - sp.half;
                string where = c.zAtBlast <= zN && c.zAtBlast >= zS ? $"ON the deck, {zN - c.zAtBlast:F1} m from its north end" : c.zAtBlast > zN ? $"{c.zAtBlast - zN:F1} m NORTH of the deck (on the bank road)" : $"{zS - c.zAtBlast:F1} m past the south end";
                if (!c.behind) c.fate = $"thrown at {c.launchAt + BluePressClock:F2} s by its bridge's charge, in the water {c.launchAt + c.waterAt + BluePressClock:F2} s" + (c.robot ? "" : ", goes up in it") + (c.turret ? $"; turret off, in the water {c.launchAt + c.turretWaterAt + BluePressClock:F2} s" : "") + (c.kind == Kind.Banner ? "; his banner comes down alone, face up on the red bank" : "");
                sb.AppendLine($"  {(c.span < names.Length ? names[c.span] : c.span.ToString())} {c.slot + 1}. {c.label}: at the blast ({sp.blastAt + BluePressClock:F2} s) {where}: {c.fate}");
            }
            UnitTable = sb.ToString();
            Debug.Log("[OpenZ L18 briefing] " + UnitTable);
        }

        /// <summary>No Play needed: the second after the press at which the last thing of the stage is under water, from the stage's own
        /// numbers (the unit behind a 24 m deck: a hull of `tankLen` m whose nose is `noseShort` m north of the deck's end when it goes).</summary>
        public static float LastUnderAfterPress(float tankLen = 7.1f, float noseShort = 2.6f, float deckOverWater = 2.2f)
        {
            float v0 = Mathf.Max(0.5f, ConvoySpeed), vmin = v0 * BrakeMin, a = Mathf.Max(0.1f, BrakeDecel);
            float d = Mathf.Max(0f, noseShort + StumpOut + tankLen * 0.5f - v0 * ReactSeconds), t1 = (v0 - vmin) / a, d1 = v0 * t1 - 0.5f * a * t1 * t1;
            float tau = d <= d1 ? (v0 - Mathf.Sqrt(Mathf.Max(0f, v0 * v0 - 2f * a * d))) / a : t1 + (d - d1) / vmin;
            float gone = SinkBlastAfter > 0f ? SinkBlastAfter + 0.3f : SinkSeconds;
            float roll = ReactSeconds + tau + Mathf.Sqrt(2f * (deckOverWater + 1.35f) / Gravity) + gone;
            float tup = TurretUp + TurretUpMore, hup = (HullUp + HullUpMore) * 1.35f;
            float turret = 2f * ChargeGap + 0.07f + (tup + Mathf.Sqrt(tup * tup + 2f * Gravity * (deckOverWater + 2.4f))) / Gravity + TurretSinkSeconds;
            float hull = 2f * ChargeGap + 0.07f + (hup + Mathf.Sqrt(hup * hup + 2f * Gravity * (deckOverWater + 1.5f))) / Gravity + gone;
            return PressToBlast + 2f * BlastGap + Mathf.Max(roll, Mathf.Max(turret, hull));
        }

        /// <summary>No Play needed: the second after the press at which the last turret or thrown body meets the water.</summary>
        public static float LastSplashAfterPress(float deckOverWater = 2.2f)
        {
            float tup = TurretUp + TurretUpMore, hup = (HullUp + HullUpMore) * 1.35f;
            float turret = (tup + Mathf.Sqrt(tup * tup + 2f * Gravity * (deckOverWater + 2.4f))) / Gravity, hull = (hup + Mathf.Sqrt(hup * hup + 2f * Gravity * (deckOverWater + 1.5f))) / Gravity;
            return PressToBlast + 2f * BlastGap + 2f * ChargeGap + 0.07f + Mathf.Max(turret, hull);
        }
    }
}
