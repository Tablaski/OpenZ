// OpenZ (GPL-3.0) - version 1.5, kit big_tank_wake_r3 (FAILS_LIST A2, 13 Nov 2026 on exe 13c
// redone by 1.5 kit bizon_ripple_r6 (A2, 14 Nov 2026 on exe 14b.
// View only: reads the drawn water and the unit, writes nothing to the rules.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The water round a wading Bizon (DV192). 1.5 kit bizon_ripple_r6: NOTHING is drawn round the hull any more (r3's pale foam skirt, its
    /// waterline, its two standing loops, its two lines along the hull and the water shaders' contact ring round it all made one closed pale
    /// outline: a selection frame). The water itself now moves instead, the usual way of game water (a disturbance is DATA the water shader
    /// reads, not paint over the water):
    /// <para>Wake stamps. Where the hull really disturbs the water (its four track ends = the hull corners, and the track sides when it
    /// stands) this script drops small stamps into four global shader lists (_OZWakeA / B / C / Hull, Shaders/OpenZWaterContact.hlsl
    /// OzWaterWake). A stamp stays where it was dropped in the WATER (world space; the tank drives on) and the water shaders work out its age:</para>
    /// <list type="bullet">
    /// <item>a ripple arc / a wake wavelet = a short train of two or three wave crests that travels and fades, read by the water as a change
    /// of its SLOPE (normal): it shows as the water's own light, reflection and glints bending, in the water's own colours, never as a pale
    /// line. A ripple is always an open arc with gaps, a wavelet a short feather: nothing is ever a closed ring.</item>
    /// <item>a churn stamp = a soft patch of churned water that grows a little and dies: the water's small waves are calmer inside it and a
    /// few broken foam flecks (the water's own foam colour through moving noise, eroding as the patch dies) lie on it.</item>
    /// </list>
    /// <para>Moving: each track end (hull corner) drops a WAVELET every <see cref="StepM"/> m or more it travels (uneven steps): a short
    /// packet of two or three crests, turned about 55 deg off the path (each its own angle, length, bend and gaps: never a straight bar),
    /// that drifts away from the path at tan(19.5 deg) of the speed. So the wavelets of one corner lie by themselves on the arm of the wake
    /// wedge of a real hull (19.5 deg each side), as separate feathers that open behind it, stay in the water along the path it drove (they
    /// curve when it turns) and fade ~<see cref="WaveLife"/> s later; bow corners and stern corners each make their own pair of arms, as a
    /// real hull does. A small churn patch at each leading corner is the water the bow pushes; the TRAILING corners drop larger churn
    /// patches (the wash of the tracks: uneven, stretched along the path) that stay behind in the water. Turning on the spot: ONE track end
    /// at a time throws a short narrow ripple arc sideways, from a point along the track, never the four corners together. Standing (the
    /// idling engine shakes the hull): a small ripple arc every ~1 s from one of six points along the tracks, never the same point twice
    /// running, two or three alive at once at different ages, no foam. Into the water: one ripple arc at a bow corner, a second smaller one
    /// on the other side a moment later, and a churn patch (the plunge sound and the spray as before).</para>
    /// <para>The hull itself (critic pass): its footprint goes to the water each frame it wades (_OZWakeBoxA / B, OzWaterWakeHull): the water
    /// the water's shore-foam line is held back at its flanks. No ring and no line comes with any of the three.</para>
    /// <para>Kept from r3 (not part of the outline): the small spray + foam patch particles at the track fronts while it moves, the drips,
    /// the dark wet band on the hull sides that dries on land (the only mesh this script still draws).</para>
    /// Cost: no mesh round the hull, no extra draw call, no texture; a stamp is written when it is dropped (not per frame; per frame only the
    /// four hull circles and footprints, 12 vectors, while a Bizon wades); the water shader
    /// loops 24 stamps only inside a circle round each wading Bizon (at most <see cref="MaxHulls"/> at once; elsewhere one test per Bizon, and
    /// nothing at all while no Bizon is in water). GRAPHICS QUALITY LOW: ripples only (no churn patches), fewer standing ripples, no drips, no mist.
    /// Off (<see cref="On"/> false) = the old rings of exe 13c (before / after proof).
    /// </summary>
    [DefaultExecutionOrder(80)]   // after UnitView (60) and OpenZWading (70)
    public sealed class OzWadeWake : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>The wake while it moves: each track end drops one wavelet per StepM m it travels (never more than WavesBow / WavesStern alive
        /// per leading / trailing end). A wavelet drifts away from the path at WedgeTan x the speed (tan 19.5 deg = the wake wedge of a real
        /// hull; limits m/s), lives WaveLife / SternLife s, its crests WaveLam0..1 m apart (slow .. fast) and turned FeatherDeg off the path;
        /// WaveSlope = its slope at full speed, a trailing end x SternShare.</summary>
        public static float StepM = 0.9f, WedgeTan = 0.3536f, DriftMin = 0.3f, DriftMax = 0.9f, WaveLife = 6f, SternLife = 5f, WaveLam0 = 0.75f, WaveLam1 = 1.05f, FeatherDeg = 55f,
            WaveSlope = 0.38f, SternShare = 0.75f;
        /// <summary>Every wavelet its own: its angle +- FeatherJitDeg, the bend of its crest line BendMin..BendMax (1 / m; + = hollow side to the hull).</summary>
        public static float FeatherJitDeg = 16f, BendMin = -0.02f, BendMax = 0.10f;
        public static int WavesBow = 4, WavesStern = 3;
        /// <summary>Turning on the spot (a track end moving across the hull, |lead| under TurnLead): ONE short ripple arc at a time, from a point
        /// along a track (never the same end twice running), the next TurnGap0..1 s later; life s, half width of the arc ArcDeg0..1 (deg).</summary>
        public static float TurnLead = 0.35f, TurnLife = 2.5f, ArcDeg0 = 32f, ArcDeg1 = 54f, TurnGap0 = 1.1f, TurnGap1 = 1.7f;
        /// <summary>Churn patches: the wash behind a trailing corner (step m, radius m, life s, strength) and the push at a leading corner.</summary>
        public static float WashStepM = 1.0f, WashR = 1.2f, WashLife = 2.6f, WashA = 0.7f, PushStepM = 1.1f, PushR = 0.65f, PushLife = 0.9f, PushA = 0.5f;
        /// <summary>Standing (the idling engine shakes the hull): a ripple every StandEvery0..1 s (two or three alive at once), its front speed
        /// (m/s), life (s), crest spacing (m), slope (the level's own small waves are ~0.10-0.15 on level 18).</summary>
        public static float StandEvery0 = 0.8f, StandEvery1 = 1.5f, StandFront = 0.5f, StandLife = 3.4f, StandLam = 0.62f, StandSlope = 0.42f;
        /// <summary>The hull's footprint in the water (OzWaterWakeHull): FlankFoamHold = how much of the water's shore-foam line is held back
        /// at its flanks (1 = none of it shows there; 0 = the level's foam line at the hull, as on every other object), ClearShare = the
        /// share of level 18's clear band the soft zone round it gets (0 = none), UnderTrimM = the clearer water reaches this much less deep
        /// than the waterline's height over the hull's bottom (so the bed beside the hull keeps the water's own curve).</summary>
        public static float FlankFoamHold = 1f, ClearShare = 0.8f, UnderTrimM = 0.08f;
        /// <summary>Wet band on the hull sides: height (m), opacity, drying time on land (s), drip time after leaving (s).</summary>
        public static float WetH = 0.42f, WetA = 0.30f, DryS = 9f, DripS = 4f;
        /// <summary>Speed (m/s) under which it counts as standing; the speed that counts as full is its own peak in water (at least MinFullMps).</summary>
        public static float StandMps = 0.25f, MinFullMps = 0.8f;
        /// <summary>The unlit wet band's light: by day / at full night (DetailKit.Night).</summary>
        public static float DayLight = 0.95f, NightLight = 0.32f;

        /// <summary>Counted proof.</summary>
        public static int Hosts, Builds, Wavelets, TurnRipples, Washes, Pushes, StandRipples, EntryStamps, Starved, CutShort, Uploads, Puffs, Drips, Entries, Exits, OldRingsHeld, BoxFrames;
        /// <summary>CutShortWorst = the largest share of its life a stamp still had when its place was taken (near 0 = it was already faded).</summary>
        public static float WidestX, WidestZ, PeakMps, FarthestReach, CutShortWorst;
        static float hullW = 5f, hullL = 7.3f;

        static readonly Dictionary<int, OzWadeWake> hosts = new Dictionary<int, OzWadeWake>();
        static Material mat;
        static readonly List<Vector3> V = new List<Vector3>(256);
        static readonly List<Color32> C = new List<Color32>(256);
        static readonly List<int> T = new List<int>(512);
        static string colLevel; static Color cFoam, cLine, cRip, cWet, cWater; static OpenZFxPlayer chMade;
        /// <summary>The water's own colours after <see cref="Colours"/> (OzWadeRipples draws the robots' ripples in them).</summary>
        internal static Color FoamTint => cFoam; internal static Color LineTint => cLine; internal static Color RippleTint => cRip;

        // ------------------------------------------------------------------ the wake stamps (Shaders/OpenZWaterContact.hlsl OzWaterWake)
        /// <summary>The shader's list sizes (OZ_WAKE_HULLS, OZ_WAKE_PER).</summary>
        public const int MaxHulls = 4, PerHull = 24;
        static readonly int WakeAId = Shader.PropertyToID("_OZWakeA"), WakeBId = Shader.PropertyToID("_OZWakeB"), WakeCId = Shader.PropertyToID("_OZWakeC"),
            WakeHullId = Shader.PropertyToID("_OZWakeHull"), WakeCountId = Shader.PropertyToID("_OZWakeCount"),
            BoxAId = Shader.PropertyToID("_OZWakeBoxA"), BoxBId = Shader.PropertyToID("_OZWakeBoxB"), BoxCountId = Shader.PropertyToID("_OZWakeBoxCount"), LookId = Shader.PropertyToID("_OZWakeLook");
        // A: x, z, the time it was dropped (Time.time = the shaders' _Time.y), strength (the slope of a ripple / wavelet, 0..1 of a patch)
        // B: x, z = the way a ripple arc faces / a wavelet drifts / a patch is stretched; its speed m/s: > 0 the front of a ripple arc, < 0 the drift of a wavelet, 0 = a churn patch; its life s
        // C: a ripple arc: start radius m, crest spacing m, cos of the half width of its arc, its seed; a wavelet: its bend (1 / m), crest spacing m, the way its crests face (x, z);
        //    a patch: its stretch along B.xy, its radius m, -, its seed
        // Box A: the hull's centre x, z, half width, half length; Box B: its right axis (x, z), water y, how deep it reaches x its fade (0 = none)
        static readonly Vector4[] gA = new Vector4[MaxHulls * PerHull], gB = new Vector4[MaxHulls * PerHull], gC = new Vector4[MaxHulls * PerHull], gH = new Vector4[MaxHulls],
            gBoxA = new Vector4[MaxHulls], gBoxB = new Vector4[MaxHulls];
        static readonly float[] gDie = new float[MaxHulls * PerHull], gReach = new float[MaxHulls * PerHull], gLife = new float[MaxHulls * PerHull];
        static readonly OzWadeWake[] slotOf = new OzWadeWake[MaxHulls];
        static bool dirty, dirtyH; static int shown, shownBox;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hosts.Clear(); mat = null; colLevel = null; chMade = null;
            Hosts = Builds = Wavelets = TurnRipples = Washes = Pushes = StandRipples = EntryStamps = Starved = CutShort = Uploads = Puffs = Drips = Entries = Exits = OldRingsHeld = 0;
            WidestX = WidestZ = PeakMps = FarthestReach = 0f;
            BoxFrames = 0; CutShortWorst = 0f;
            for (int i = 0; i < gA.Length; i++) { gA[i] = gB[i] = gC[i] = Vector4.zero; gDie[i] = 0f; gReach[i] = 0f; gLife[i] = 0f; }
            for (int i = 0; i < MaxHulls; i++) { gH[i] = gBoxA[i] = gBoxB[i] = Vector4.zero; slotOf[i] = null; }
            dirty = dirtyH = true; shown = shownBox = 1;                                                                        // the first wader's first Publish clears whatever an earlier Play left in the shaders' lists
        }

        // ------------------------------------------------------------------ per wader
        UnitView v; Mesh mesh; MeshRenderer mr;
        float halfX = 2.5f, halfZ = 3.65f, cz = -0.25f;
        int fedFrame = -10, kc; Vector3 p, fwd = Vector3.forward, lastP; float sc, dt; bool seen, hasLast, wasWater;
        float speed, peak, speedK, wetK, wlLocal = 1f, dryT = 99f, dripLeft, dripT, standT = 1f, turnT, entry2T = -1f, boundAt;
        int turnLast = -1, standLast = -1, entry2Side;
        readonly float[] puffT = new float[2];
        int slot = -1;
        readonly Vector3[] kLast = new Vector3[4]; readonly float[] kOdo = new float[4], kRip = new float[4], kChurn = new float[4], kSpeed = new float[4]; bool kInit;

        static bool Low => OzLite.Active && OzLite.Level >= 2;

        /// <summary>OzBizonWade.Fx, every frame for a living Bizon (kc 0 dry, 1 water, 2 lava; sc the surface; seen = on screen).</summary>
        public static void Feed(UnitView v, Vector3 p, Vector3 fwd, int kc, float sc, float dt, bool seen)
        {
            if (!On || !v) return;
            int id = v.GetInstanceID();
            if (!hosts.TryGetValue(id, out var h) || !h)
            {
                if (kc != 1) return;                                                                        // made the first time it is in water
                var go = new GameObject("OZ_WadeWake_" + (v.Obj != null ? v.Obj.Id : 0));
                h = go.AddComponent<OzWadeWake>(); h.Bind(v); hosts[id] = h; Hosts++;
            }
            h.fedFrame = Time.frameCount; h.p = p; h.kc = kc; h.sc = sc; h.dt = Mathf.Max(0f, dt); h.seen = seen;
            fwd.y = 0f; if (fwd.sqrMagnitude > 1e-6f) h.fwd = fwd.normalized;
            // 1.5 kit bizon_ripple_r6: the wading Bizon is NOT handed to the water shaders as a contact any more (r3 did, OzVehicleWaterDip.Wader):
            // their contact ring was the crisp closed line round the hull in the owner's picture, with the clearer rounded-box halo inside it.
        }

        /// <summary>OzBizonWade: true = the old round rings / ring splashes are left out (this kit moves the water instead).</summary>
        public static bool HoldsRings() { if (!On) return false; OldRingsHeld++; return true; }

        /// <summary>Into the water: the plunge sound (as before: wtr_vehicle_plunge, the sound of the old Large splash), a soft spray at the bow
        /// and at both tracks, no rings.</summary>
        public static void Entry(Vector3 bow, Vector3 fwd, int id)
        {
            AudioDirector.Fire("wtr_vehicle_plunge", bow, id, 0.9f, Random.Range(0.94f, 1.04f));
            var z = OpenZFxPlayer.I; if (!z || !z.isActiveAndEnabled) return;
            Colours(); Entries++;
            OzWaterSplash.Drops(bow, 1.0f, 12, cWater);
            var right = Vector3.Cross(Vector3.up, fwd);
            for (int s = -1; s <= 1; s += 2) Spray(z, bow + right * (2.0f * s) - fwd * 0.6f, right * s, fwd, 1f, false);
        }

        /// <summary>Out on the bank: drops at the stern (no ring); the drips and the wet band follow from Feed.</summary>
        public static void Exit(Vector3 rear) { Exits++; Colours(); OzWaterSplash.Drops(rear, 0.5f, 8, cWater); }

        void Bind(UnitView u)
        {
            v = u;
            mesh = new Mesh { name = "OzWadeWake" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mat();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.enabled = false;
            Footprint();
            puffT[0] = 0.1f; puffT[1] = 0.25f; standT = Random.Range(0.6f, 1.6f);
        }

        void OnDestroy() { Release(true); Publish(); }

        /// <summary>The hull and tracks in the unit's own frame (the parts whose bottom is under 1.2 m), from their meshes; else the measured
        /// Bizon (5.0 x 7.3 m, its middle 0.25 m behind the unit's point).</summary>
        void Footprint()
        {
            var t = v.transform; var w2l = t.worldToLocalMatrix; bool any = false; var box = new Bounds();
            foreach (var f in v.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = f.GetComponent<Renderer>(); if (!f.sharedMesh || !r || !r.enabled) continue;
                var m = r.sharedMaterial; if (m && m.renderQueue >= 2450) continue;
                var b = f.sharedMesh.bounds; var M = w2l * f.transform.localToWorldMatrix; var lb = new Bounds(); bool first = true;
                for (int k = 0; k < 8; k++)
                {
                    var c = M.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f)));
                    if (first) { lb = new Bounds(c, Vector3.zero); first = false; } else lb.Encapsulate(c);
                }
                if (lb.min.y > 1.2f || lb.size.x > 9f || lb.size.z > 12f) continue;
                if (!any) { box = lb; any = true; } else box.Encapsulate(lb);
            }
            if (any && box.extents.x > 1f && box.extents.z > 1.5f) { halfX = box.extents.x; halfZ = box.extents.z; cz = box.center.z; }
            hullW = halfX * 2f; hullL = halfZ * 2f;
        }

        /// <summary>The soft unlit wake material (shared with OzWadeRipples, the robots' ripples).</summary>
        internal static Material Mat()
        {
            if (mat) return mat;
            var src = Resources.Load<Material>(L16AnimalKit.ModelRes + CrocodileView.WakeMaterial);          // URP Particles/Unlit, alpha blend, no depth write, both sides
            if (src) mat = new Material(src);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (!sh) return null;
                mat = new Material(sh); mat.SetFloat("_Surface", 1f); mat.SetFloat("_SrcBlend", 5f); mat.SetFloat("_DstBlend", 10f); mat.SetFloat("_ZWrite", 0f); mat.SetFloat("_Cull", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            mat.name = "OzWadeWake";
            mat.SetTexture("_BaseMap", Texture2D.whiteTexture); mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = 3004;                                                                         // after the water (Transparent-10), with the game's ripples
            return mat;
        }

        /// <summary>The water's own colours (its material under WAT_*), once per level; else the level's ripple tint.</summary>
        internal static void Colours()
        {
            string lv = ZGame.I ? ZGame.I.level : "";
            if (colLevel == lv) return;
            colLevel = lv;
            Color tint = OpenZFxCatalog.RippleColorFor(lv), foam = tint, mid = tint * 0.35f, shallow = tint * 0.5f;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r || !r.name.StartsWith("WAT_") || !r.gameObject.activeInHierarchy) continue;
                var m = r.sharedMaterial; if (!m || !m.HasProperty("_FoamColor")) continue;
                foam = m.GetColor("_FoamColor"); if (m.HasProperty("_MidColor")) mid = m.GetColor("_MidColor"); if (m.HasProperty("_ShallowColor")) shallow = m.GetColor("_ShallowColor");
                break;
            }
            foam.a = mid.a = shallow.a = 1f;
            cFoam = Color.Lerp(foam, mid, 0.12f); cLine = Color.Lerp(mid, foam, 0.72f); cRip = Color.Lerp(shallow, foam, 0.55f);
            cWet = mid * 0.22f; cWet.a = 1f; cWater = Color.Lerp(shallow, foam, 0.35f);
        }

        // ------------------------------------------------------------------ the stamps
        /// <summary>This wader's 24 places in the lists: its own, or a free one (its owner gone or released). False = four other Bizons hold them all.</summary>
        bool Slot(bool count = true)
        {
            if (slot >= 0 && slotOf[slot] == this) return true;
            slot = -1;
            for (int s = 0; s < MaxHulls; s++)
                if (!slotOf[s]) { slot = s; slotOf[s] = this; Clear(s); return true; }
            if (count) Starved++;
            return false;
        }

        static void Clear(int s)
        {
            for (int i = s * PerHull; i < (s + 1) * PerHull; i++) { gA[i] = gB[i] = gC[i] = Vector4.zero; gDie[i] = 0f; gReach[i] = 0f; gLife[i] = 0f; }
            gH[s] = gBoxA[s] = gBoxB[s] = Vector4.zero; dirty = true;
        }

        /// <summary>Gives the places back: at once (the unit is gone), or when its last stamp has died.</summary>
        void Release(bool now)
        {
            if (slot < 0 || slotOf[slot] != this) { slot = -1; return; }
            if (!now)
            {
                float t = Time.time;
                for (int i = slot * PerHull; i < (slot + 1) * PerHull; i++) if (gA[i].w > 0f && gDie[i] > t) return;
            }
            Clear(slot); slotOf[slot] = null; slot = -1;
        }

        /// <summary>One stamp into this wader's places: a dead place, else the one with the smallest share of its life left (every stamp fades
        /// to nothing with that share, so the one taken is the faintest; counted: CutShort, CutShortWorst). front > 0 a ripple arc (r0 = its
        /// start radius, c3 = cos of its half width, c4 = its seed), front < 0 a wake wavelet that drifts along `face` at -front m/s (r0 = its
        /// bend, c3, c4 = the way its crests face), 0 = a churn patch stretched r0 times along `face` (c4 = its seed).</summary>
        void Drop(Vector3 at, Vector2 face, float front, float life, float r0, float size, float c3, float c4, float amp)
        {
            if (amp <= 0.005f || life <= 0.05f || !Slot()) return;
            float t = Time.time; int best = slot * PerHull; float bd = float.MaxValue;
            for (int i = slot * PerHull; i < (slot + 1) * PerHull; i++)
            {
                float d = gA[i].w > 0f && gLife[i] > 0f ? (gDie[i] - t) / gLife[i] : -1f;
                if (d < bd) { bd = d; best = i; }
            }
            if (bd > 0f) { CutShort++; if (bd > CutShortWorst) CutShortWorst = bd; }
            gA[best] = new Vector4(at.x, at.z, t, amp);
            gB[best] = new Vector4(face.x, face.y, front, life);
            gC[best] = new Vector4(r0, size, c3, c4);
            gDie[best] = t + life; gLife[best] = life;
            gReach[best] = front > 0f ? r0 + front * life + size * 0.75f                                    // the farthest it ever shows from its point
                : front < 0f ? 0.25f - front * life + size * 5.6f : size * 1.3f * Mathf.Max(1f, r0);
            Bound(); dirty = true;
        }

        /// <summary>The circle round this wader's living stamps (the water shader tests it first) and the water's height: the middle of
        /// the box round what they can still reach (tighter than their mean), worked out again when a stamp is dropped and twice a second
        /// (dead stamps shrink it).</summary>
        void Bound()
        {
            float t = Time.time; int n = 0; float x0 = 0f, x1 = 0f, z0 = 0f, z1 = 0f;
            for (int i = slot * PerHull; i < (slot + 1) * PerHull; i++)
                if (gA[i].w > 0f && gDie[i] > t)
                {
                    float r = gReach[i];
                    if (n++ == 0) { x0 = gA[i].x - r; x1 = gA[i].x + r; z0 = gA[i].y - r; z1 = gA[i].y + r; }
                    else { x0 = Mathf.Min(x0, gA[i].x - r); x1 = Mathf.Max(x1, gA[i].x + r); z0 = Mathf.Min(z0, gA[i].y - r); z1 = Mathf.Max(z1, gA[i].y + r); }
                }
            var was = gH[slot];
            if (n == 0) gH[slot] = Vector4.zero;
            else
            {
                var c = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f); float rr = 0f;
                for (int i = slot * PerHull; i < (slot + 1) * PerHull; i++)
                    if (gA[i].w > 0f && gDie[i] > t) rr = Mathf.Max(rr, (new Vector2(gA[i].x, gA[i].y) - c).magnitude + gReach[i]);
                gH[slot] = new Vector4(c.x, c.y, rr + 0.05f, sc);
            }
            if (gH[slot] != was) dirtyH = true;
            boundAt = t + 0.5f;
        }

        /// <summary>The hull's footprint at the waterline for the water (OzWaterWakeHull): while it wades, fading with the wet band.</summary>
        void Box(bool wading)
        {
            if (!wading || wetK <= 0f || !Slot(false))
            {
                if (slot >= 0 && slotOf[slot] == this && gBoxB[slot].w != 0f) { gBoxA[slot] = gBoxB[slot] = Vector4.zero; dirtyH = true; }
                return;
            }
            var c = new Vector3(p.x, sc, p.z) + fwd * cz; var r = Vector3.Cross(Vector3.up, fwd);
            gBoxA[slot] = new Vector4(c.x, c.z, halfX, halfZ);
            gBoxB[slot] = new Vector4(r.x, r.z, sc, Mathf.Clamp(wlLocal - UnderTrimM, 0.18f, 2.5f) * wetK);
            dirtyH = true; BoxFrames++;
        }

        /// <summary>The stamp lists go to the shaders only when a stamp was dropped or a wader's places were given back (never per frame);
        /// the four hull circles and footprints (12 vectors) whenever one changed (each frame while a Bizon wades).</summary>
        static void Publish()
        {
            if (dirty)
            {
                dirty = false; dirtyH = true;
                Shader.SetGlobalVectorArray(WakeAId, gA); Shader.SetGlobalVectorArray(WakeBId, gB); Shader.SetGlobalVectorArray(WakeCId, gC);
                Uploads++;
            }
            if (!dirtyH) return;
            dirtyH = false;
            int n = 0, nb = 0;
            for (int s = 0; s < MaxHulls; s++) { if (gH[s].z > 0f) n = s + 1; if (gBoxB[s].w > 0f) nb = s + 1; }
            if (n == 0 && shown == 0 && nb == 0 && shownBox == 0) return;
            Shader.SetGlobalVectorArray(WakeHullId, gH); Shader.SetGlobalFloat(WakeCountId, n);
            Shader.SetGlobalVectorArray(BoxAId, gBoxA); Shader.SetGlobalVectorArray(BoxBId, gBoxB); Shader.SetGlobalFloat(BoxCountId, nb);
            Shader.SetGlobalVector(LookId, new Vector4(Mathf.Clamp01(FlankFoamHold), Mathf.Clamp01(ClearShare), 0f, 0f));
            shown = n; shownBox = nb;
        }

        /// <summary>The hull corner k (0 front left, 1 front right, 2 rear left, 3 rear right) on the water: the ends of its tracks.</summary>
        Vector3 Corner(int k, Vector3 right)
        {
            float sx = (k & 1) == 0 ? -1f : 1f, sz = k < 2 ? 1f : -1f;
            return new Vector3(p.x, sc, p.z) + fwd * (cz + sz * (halfZ - 0.2f)) + right * (sx * (halfX + 0.05f));
        }

        static Vector2 Rot(Vector2 a, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(a.x * c - a.y * s, a.x * s + a.y * c); }

        void Stamps(Vector3 right)
        {
            bool water = kc == 1, low = Low;
            var mid = new Vector3(p.x, sc, p.z) + fwd * cz;
            if (!kInit || !water)
            {   // not in water (or the first frame): only remember where the corners are
                for (int k = 0; k < 4; k++) { kLast[k] = Corner(k, right); kSpeed[k] = 0f; kRip[k] = kChurn[k] = kOdo[k]; }
                kInit = true;
                if (!water) { wasWater = false; entry2T = -1f; return; }
            }
            float full = Mathf.Max(MinFullMps, peak * 0.9f);
            bool any = false;
            var f2 = new Vector2(fwd.x, fwd.z);
            turnT -= dt;
            int turnPick = -1, turnSeen = 0; Vector2 turnC = Vector2.zero, turnFace = Vector2.zero; float turnSk = 0f, turnK01 = 0f;
            for (int k = 0; k < 4; k++)
            {
                var c = Corner(k, right); var d = new Vector2(c.x - kLast[k].x, c.z - kLast[k].z); float dist = d.magnitude;
                kLast[k] = c; kOdo[k] += dist;
                kSpeed[k] = Mathf.Lerp(kSpeed[k], Mathf.Min(dist / dt, 12f), 1f - Mathf.Exp(-dt * 5f));
                float sk = kSpeed[k];
                if (sk <= StandMps || dist < 1e-5f) continue;
                any = true;
                if (!seen) continue;
                var vh = d / dist; var off = new Vector2(c.x - mid.x, c.z - mid.z); float ol = Mathf.Max(0.01f, off.magnitude);
                float lead = Vector2.Dot(vh, off) / ol;                                                     // +1 this corner leads, -1 it trails, ~0 it turns round the middle
                float k01 = Mathf.Clamp01((sk - StandMps) / full);
                var perp = off - vh * Vector2.Dot(off, vh); if (perp.sqrMagnitude < 0.04f) perp = off; perp.Normalize();   // away from the hull, across its path
                if (Mathf.Abs(lead) < TurnLead)
                {   // turning on the spot: this track end is ready to throw a ripple arc sideways; ONE of the ready ends is picked below
                    // (all four move at one speed in a spot turn: four arcs at the four corners at once would read as a frame's corners)
                    float step = Mathf.Max(StepM * 1.4f, sk * TurnLife / 2f);
                    if (kOdo[k] - kRip[k] < step || turnT > 0f || k == turnLast) continue;
                    if (Random.value * ++turnSeen < 1f)
                    {
                        turnPick = k; turnSk = sk; turnK01 = k01;
                        turnC = new Vector2(c.x, c.z) - f2 * ((k < 2 ? 1f : -1f) * Random.Range(0.1f, 1.3f)) + perp * Random.Range(0.05f, 0.3f);   // a point along the track, not the corner itself
                        turnFace = Rot((perp + vh * 0.5f).normalized, Random.Range(-28f, 28f));
                    }
                    continue;
                }
                bool bow = lead > 0f;
                float life = bow ? WaveLife : SternLife;
                float wstep = Mathf.Max(StepM, sk * life / (bow ? WavesBow : WavesStern));
                if (kOdo[k] - kRip[k] >= wstep)
                {   // one wavelet of the wake: born at the track end, it drifts away from the path at tan(19.5 deg) of the speed (so the wavelets
                    // of one corner lie on the arm of the wake wedge behind it), its crests turned ~55 deg off the path like the feathers of a
                    // real wake; every one its own step, angle, bend, spacing and strength (never a row of equal bars)
                    kRip[k] = kOdo[k] + wstep * Random.Range(-0.35f, 0.5f);
                    float turn = (FeatherDeg + Random.Range(-FeatherJitDeg, FeatherJitDeg)) * Mathf.Deg2Rad;
                    var n = vh * Mathf.Sin(turn) + perp * Mathf.Cos(turn);
                    var jit = Random.insideUnitCircle * 0.2f;
                    Drop(new Vector3(c.x + jit.x, sc, c.z + jit.y), perp, -Mathf.Clamp(sk * WedgeTan, DriftMin, DriftMax) * Random.Range(0.88f, 1.12f), life * Random.Range(0.8f, 1.1f),
                         Random.Range(BendMin, BendMax), Mathf.Lerp(WaveLam0, WaveLam1, k01) * Random.Range(0.8f, 1.25f), n.x, n.y,
                         WaveSlope * (bow ? 1f : SternShare) * (0.45f + 0.55f * k01) * Random.Range(0.6f, 1.1f));
                    Wavelets++;
                }
                if (low) continue;
                if (bow && kOdo[k] - kChurn[k] >= Mathf.Max(PushStepM, sk * PushLife))
                {   // the water the bow pushes: a small short patch just outside the leading corner (at most one alive per end)
                    kChurn[k] = kOdo[k] + PushStepM * Random.Range(-0.2f, 0.4f);
                    var at = new Vector2(c.x, c.z) + perp * Random.Range(0.15f, 0.45f) + vh * 0.35f + Random.insideUnitCircle * 0.2f;
                    Drop(new Vector3(at.x, sc, at.y), Rot(vh, Random.Range(-35f, 35f)), 0f, PushLife * Random.Range(0.8f, 1.2f), Random.Range(1.2f, 1.7f), PushR * Random.Range(0.75f, 1.25f), 0f, Random.value,
                         PushA * k01 * Random.Range(0.6f, 1f));
                    Pushes++;
                }
                else if (!bow && kOdo[k] - kChurn[k] >= Mathf.Max(WashStepM, sk * WashLife / 2.5f))
                {   // the wash of the track: a larger uneven patch, stretched along the path, left in the water behind a trailing corner
                    kChurn[k] = kOdo[k] + Mathf.Max(WashStepM, sk * WashLife / 2.5f) * Random.Range(-0.35f, 0.5f);
                    var at = new Vector2(c.x, c.z) - vh * Random.Range(0.3f, 0.9f) - perp * Random.Range(0.3f, 1.1f) + Random.insideUnitCircle * 0.25f;
                    Drop(new Vector3(at.x, sc, at.y), Rot(vh, Random.Range(-20f, 20f)), 0f, WashLife * Random.Range(0.75f, 1.2f), Random.Range(1.4f, 2.2f), WashR * Random.Range(0.6f, 1.05f), 0f, Random.value,
                         WashA * k01 * Random.Range(0.6f, 1f));
                    Washes++;
                }
            }
            if (turnPick >= 0 && seen)
            {   // the one ripple arc of this turn step: short, narrow, from a point along that track; the next one TurnGap0..1 s later from another end
                kRip[turnPick] = kOdo[turnPick]; turnLast = turnPick; turnT = Random.Range(TurnGap0, TurnGap1);
                Drop(new Vector3(turnC.x, sc, turnC.y), turnFace, Mathf.Clamp(turnSk * 0.5f, 0.4f, 0.9f) * Random.Range(0.85f, 1.15f), TurnLife * Random.Range(0.8f, 1.1f), Random.Range(0.1f, 0.35f),
                     StandLam * Random.Range(0.95f, 1.35f), Mathf.Cos(Random.Range(ArcDeg0, ArcDeg1) * Mathf.Deg2Rad), Random.value, WaveSlope * 0.85f * (0.5f + 0.5f * turnK01) * Random.Range(0.6f, 1f));
                TurnRipples++;
            }
            if (!wasWater)
            {   // into the water: ONE ripple arc near a bow corner now, a second smaller one on the other side a moment later (never two equal
                // arcs at both corners in one frame), and a churn patch at the bow (the plunge sound and the spray: Entry)
                wasWater = true;
                if (seen)
                {
                    entry2Side = Random.value < 0.5f ? 0 : 1; entry2T = Random.Range(0.35f, 0.7f);
                    EntryArc(1 - entry2Side, right, mid, 1f);
                    var bowAt = mid + fwd * (halfZ - 0.2f) + right * Random.Range(-0.8f, 0.8f);
                    if (!low) Drop(bowAt, Rot(f2, Random.Range(-25f, 25f)), 0f, 1.8f, Random.Range(1.2f, 1.6f), 1.5f, 0f, Random.value, 1f);
                    EntryStamps += low ? 1 : 2;
                }
            }
            if (entry2T > 0f && (entry2T -= dt) <= 0f)
            {
                entry2T = -1f;
                if (seen) { EntryArc(entry2Side, right, mid, 0.6f); EntryStamps++; }
            }
            if (any) { standT = Random.Range(0.5f, 1.1f); return; }
            if ((standT -= dt) > 0f) return;
            standT = Random.Range(StandEvery0, StandEvery1) * (low ? 1.8f : 1f);
            if (!seen) return;
            {   // standing (the idling engine shakes the hull): a small ripple arc from one of six stretches of the tracks (the four ends, the
                // two middles), never the same one twice running, from a point along it, facing away from the hull; two or three are alive
                // at once at different ages and places, so the water round the standing tank is never dead calm and never a ring
                int k = Random.Range(0, 6); if (k == standLast) k = (k + Random.Range(1, 6)) % 6;
                standLast = k;
                Vector3 c = k < 4 ? Corner(k, right) - fwd * ((k < 2 ? 1f : -1f) * Random.Range(0f, 1.1f)) : mid + right * ((k == 4 ? -1f : 1f) * (halfX + 0.05f)) + fwd * (Random.Range(-0.5f, 0.5f) * halfZ);
                var off = k < 4 ? new Vector2(c.x - mid.x, c.z - mid.z).normalized : new Vector2(right.x, right.z) * (k == 4 ? -1f : 1f);
                Drop(c, Rot(off, Random.Range(-28f, 28f)), StandFront * Random.Range(0.8f, 1.2f), StandLife * Random.Range(0.85f, 1.15f), Random.Range(0.1f, 0.3f), StandLam * Random.Range(0.85f, 1.25f),
                     Mathf.Cos(Random.Range(40f, 68f) * Mathf.Deg2Rad), Random.value, StandSlope * Random.Range(0.7f, 1f));
                StandRipples++;
            }
        }

        /// <summary>One ripple arc of the plunge: from a point a little inside bow corner `side` (0 left, 1 right), facing out and ahead.</summary>
        void EntryArc(int side, Vector3 right, Vector3 mid, float k)
        {
            var c = Corner(side, right) + right * ((side == 0 ? 1f : -1f) * Random.Range(0.2f, 1.2f));
            var off = new Vector2(c.x - mid.x, c.z - mid.z).normalized;
            Drop(c, Rot(off, Random.Range(-22f, 22f)), Random.Range(0.65f, 0.95f), Random.Range(2.2f, 2.9f), Random.Range(0.1f, 0.35f), Random.Range(0.75f, 1.05f),
                 Mathf.Cos(Random.Range(42f, 62f) * Mathf.Deg2Rad), Random.value, WaveSlope * 0.9f * k * Random.Range(0.8f, 1f));
        }

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!v)
            {   // the unit is gone (destroyed, removed): its stamps in the water die by themselves (no pop), then this goes too
                mr.enabled = false; wetK = 0f; Box(false); Release(!On); Publish();
                if (slot < 0) { if (mesh) Destroy(mesh); Destroy(gameObject); }
                return;
            }
            bool fed = Time.frameCount - fedFrame <= 2 && On;
            if (slot >= 0 && slotOf[slot] == this && Time.time >= boundAt) Bound();
            if (!fed)
            {   // not fed (dead, removed, switched off): the stamps in the water die by themselves, the wet band fades, then hide
                wetK = Mathf.MoveTowards(wetK, 0f, Time.deltaTime * 2f); dryT = 99f; kInit = false; wasWater = false; entry2T = -1f;
                Box(false); Release(!On); Publish();
                if (wetK <= 0f) { mr.enabled = false; return; }
            }
            else { Step(); Box(kc == 1); if (kc != 1) Release(false); Publish(); }
            if (!seen) { mr.enabled = false; return; }
            Build();
        }

        void Step()
        {
            if (dt <= 0f) return;                                                                           // paused: the same picture
            float inst = hasLast ? new Vector2(p.x - lastP.x, p.z - lastP.z).magnitude / dt : 0f;
            lastP = p; hasLast = true;
            speed = Mathf.Lerp(speed, Mathf.Min(inst, 12f), 1f - Mathf.Exp(-dt * 5f));
            bool water = kc == 1;
            if (water && speed > peak) peak = speed; if (peak > PeakMps) PeakMps = peak;
            speedK = water ? Mathf.Clamp01((speed - StandMps) / Mathf.Max(MinFullMps, peak * 0.9f) ) : 0f;
            wetK = Mathf.MoveTowards(wetK, water ? 1f : 0f, dt / (water ? 0.4f : 0.8f));
            if (water) { wlLocal = v.transform.InverseTransformPoint(new Vector3(p.x, sc, p.z)).y; dryT = 0f; dripLeft = DripS; }
            else { dryT += dt; dripLeft -= dt; }
            var right = Vector3.Cross(Vector3.up, fwd);
            Stamps(right);
            var z = OpenZFxPlayer.I; if (!seen || !z || !z.isActiveAndEnabled) return;
            Colours();
            if (water && speedK > 0.1f)
                for (int s = 0; s < 2; s++)
                    if ((puffT[s] -= dt) <= 0f)
                    {   // spray at the front of each track
                        puffT[s] = Mathf.Lerp(0.42f, 0.22f, speedK) * (Low ? 2f : 1f) * Random.Range(0.85f, 1.15f);
                        float side = s == 0 ? -1f : 1f;
                        var at = new Vector3(p.x, sc, p.z) + fwd * (cz + halfZ - 0.6f) + right * (side * (halfX - 0.45f));
                        Spray(z, at, right * side, fwd * speed, speedK, Low);
                    }
            float dripK = water ? 0.35f * (0.4f + speedK) : Mathf.Clamp01(dripLeft / DripS);
            if (!Low && dripK > 0.01f && (dripT -= dt) <= 0f)
            {   // drips off the hull sides
                dripT = Random.Range(0.12f, 0.3f) / dripK;
                float side = Random.value < 0.5f ? -1f : 1f;
                var at = v.transform.TransformPoint(new Vector3(side * (halfX + 0.02f), wlLocal + Random.Range(0.25f, 0.7f), cz + Random.Range(-0.85f, 0.85f) * halfZ));
                z.EmitChannel(OzWaterSplash.Drop, at, right * (side * 0.15f) + Vector3.down * 0.3f, Random.Range(0.5f, 0.8f), Random.Range(0.05f, 0.075f), OzWaterSplash.Tint(cWater, 0.6f), 0.8f);
                Drips++;
            }
        }

        static void Channels(OpenZFxPlayer z)
        {
            if (chMade == z && z.HasChannel(OzWaterSplash.Drop)) return;
            foreach (var d in OzWaterSplash.Defs()) if (!z.HasChannel(d.name)) z.UeAddChannel(d);
            chMade = z;
        }

        /// <summary>A small spray off a track front: a few round drops, a soft mist puff, a flat foam patch left on the water.</summary>
        static void Spray(OpenZFxPlayer z, Vector3 at, Vector3 outward, Vector3 carry, float k, bool low)
        {
            Channels(z);
            int n = low ? 2 : Random.Range(3, 5);
            for (int i = 0; i < n; i++)
                z.EmitChannel(OzWaterSplash.Drop, at + Vector3.up * 0.1f + Random.insideUnitSphere * 0.15f,
                    Vector3.up * (Random.Range(1.4f, 2.4f) * (0.6f + 0.4f * k)) + outward * Random.Range(0.4f, 0.9f) + carry * 0.5f,
                    Random.Range(0.45f, 0.7f), Random.Range(0.07f, 0.13f), OzWaterSplash.Tint(cWater, OzWaterSplash.DropWhite), 0.85f);
            if (!low) z.EmitChannel(OzWaterSplash.Mist, at + Vector3.up * 0.25f, Vector3.up * 0.5f + outward * 0.3f, Random.Range(0.9f, 1.3f), Random.Range(0.6f, 0.9f),
                OzWaterSplash.Tint(cWater, OzWaterSplash.MistWhite), 0.14f * (0.5f + 0.5f * k));
            z.EmitChannel(OzWaterSplash.FoamChannel, at + outward * 0.35f + Vector3.up * 0.03f, Vector3.zero, Random.Range(1.4f, 2.0f), Random.Range(0.45f, 0.6f),
                OzWaterSplash.Tint(cWater, 0.55f), 0.32f * k);
            Puffs++;
        }

        // ------------------------------------------------------------------ the mesh (the wet band on the hull sides only)
        float lightK;
        Vector3 centre, rgt;

        void Add(Vector3 at, Color c, float a)
        {
            a = Mathf.Clamp01(a);
            if (a > 0.05f)
            {
                var d = at - centre; float lx = Mathf.Abs(Vector3.Dot(d, rgt)), lz = Mathf.Abs(Vector3.Dot(d, fwd));
                if (lx > WidestX) WidestX = lx; if (lz > WidestZ) WidestZ = lz;
            }
            var lc = (c * lightK).linear;
            V.Add(at); C.Add(new Color32((byte)(Mathf.Clamp01(lc.r) * 255f), (byte)(Mathf.Clamp01(lc.g) * 255f), (byte)(Mathf.Clamp01(lc.b) * 255f), (byte)(a * 255f)));
        }

        /// <summary>Quads between the rows of a strip of `cols` columns x `rows` rows (vertices added column by column).</summary>
        static void Strip(int first, int cols, int rows)
        {
            for (int i = 0; i < cols - 1; i++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = first + i * rows + r, b = a + rows;
                    T.Add(a); T.Add(b); T.Add(a + 1); T.Add(a + 1); T.Add(b); T.Add(b + 1);
                }
        }

        void Build()
        {
            V.Clear(); C.Clear(); T.Clear();
            lightK = Mathf.Lerp(DayLight, NightLight, Mathf.Clamp01(DetailKit.Night));
            rgt = Vector3.Cross(Vector3.up, fwd); centre = new Vector3(p.x, sc, p.z) + fwd * cz;
            if (kc != 1) centre.y = v.transform.TransformPoint(new Vector3(0f, wlLocal, 0f)).y;
            WetBand();
            mesh.Clear();
            if (T.Count == 0) { mr.enabled = false; return; }
            mesh.SetVertices(V); mesh.SetColors(C); mesh.SetTriangles(T, 0, true);
            mr.enabled = true; Builds++;
        }

        void WetBand()
        {   // a dark wet band with a streaky top on both hull sides, from the waterline up; it dries on land
            float dry = kc == 1 ? 1f : 1f - Mathf.Clamp01(dryT / DryS);
            float A = WetA * Mathf.Max(wetK, dry) * (kc == 1 ? 1f : dry);
            if (A < 0.01f) return;
            var t = v.transform; const int cols = 11;
            for (int side = -1; side <= 1; side += 2)
            {
                int first = V.Count;
                for (int i = 0; i < cols; i++)
                {
                    float z = cz + (i / (cols - 1f) * 2f - 1f) * halfZ * 0.85f, x = side * (halfX + 0.03f);
                    float top = WetH * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(i * 2.17f + side)));                // streaks of different length
                    float edge = i == 0 || i == cols - 1 ? 0f : 1f;
                    Add(t.TransformPoint(new Vector3(x, wlLocal - 0.06f, z)), cWet, A * edge);
                    Add(t.TransformPoint(new Vector3(x, wlLocal + 0.12f, z)), cWet, A * 0.85f * edge);
                    Add(t.TransformPoint(new Vector3(x, wlLocal + top, z)), cWet, 0f);
                }
                Strip(first, cols, 3);
            }
        }

        public static string Status()
        {
            int live = 0, slots = 0; float t = Time.time;
            for (int i = 0; i < gA.Length; i++) if (gA[i].w > 0f && gDie[i] > t) live++;
            for (int s = 0; s < MaxHulls; s++) if (slotOf[s]) slots++;
            float far = 0f;
            for (int i = 0; i < gReach.Length; i++) if (gReach[i] > far) far = gReach[i];
            if (far > FarthestReach) FarthestReach = far;
            return $"wade wake {(On ? "on" : "off")} (kit bizon_ripple_r6: water stamps, no outline): hosts {Hosts}, places held {slots} of {MaxHulls}, stamps alive {live}, shader hull count {shown}, hull footprints {shownBox} ({BoxFrames} frames), uploads {Uploads}; "
                + $"dropped: wake wavelets {Wavelets}, turning ripples {TurnRipples}, wash patches {Washes}, bow push patches {Pushes}, standing ripples {StandRipples}, entry stamps {EntryStamps}; cut short {CutShort} (the most life one still had: {CutShortWorst * 100f:0} %), no place {Starved}; "
                + $"farthest a stamp reaches from its own point {FarthestReach:0.00} m (hull {hullW:0.0} x {hullL:0.0} m); wet band builds {Builds}, widest wet band {2f * WidestX:0.0} x {2f * WidestZ:0.0} m; "
                + $"spray puffs {Puffs}, drips {Drips}, entries {Entries}, exits {Exits}, old rings held {OldRingsHeld}; peak speed in water {PeakMps:0.00} m/s";
        }
    }
}
