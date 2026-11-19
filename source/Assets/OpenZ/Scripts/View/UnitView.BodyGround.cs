// OpenZ (GPL-3.0)
// "NO piece hovering on any robot").
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit robot_pieces_ground_r11 (A35 remainder): a dead robot whose body is drawn by a DEATH CLIP lies ON the ground. The death clips
    /// (death_back / death_side, Models/BS01/LEGACY_Robot_Clips.fbx: humanoid, the body's height baked into the pose from the source rig) are played
    /// at the robot's root, which stays where its feet stood; retargeted onto the BS-01 bodies the lying end pose is not where the drawn body is.
    /// Bodies drawn by a clip: <c>Death.Anim</c> (every death whose ragdoll cannot start), <c>Death.Airborne</c> after its landing (a blast kill
    /// without a free ragdoll), <c>Death.Melt</c> (a flame: death_side while it melts down). Round 11: the body's lowest DRAWN point (OzDrawnPoints)
    /// against ZGame.GroundY, the root moved down (1 g) or up so that point lies <see cref="OzBodyGround.LiftM"/> over the ground.
    /// <para>Fix round r11b (the round 11 Play check: hero bodies 0.11-0.30 m up or 0.9-1.1 m sunk, bodies lying level 0.11 m over the 18 degree
    /// ramp, blast landings 0.75 to over 1.5 m in the ground or hanging over a ramp's side, the melt read as sunk):</para>
    /// <list type="bullet">
    /// <item>the measure and the move run AFTER every pose writer of the frame (<see cref="OzBodyGroundLate"/>, order 32000): Alan's and Brad's hips and
    /// legs are written by OzHeroLegs (61) after UnitView (60), so round 11 grounded a pose that was never drawn;</item>
    /// <item>the support is the real surface under each part of the body (<see cref="OzRestSurface.Gap"/>: rays down to the colliders, per cell), not
    /// GroundY at each point: rails, kerbs, deck edges;</item>
    /// <item>the body lies ALONG the ground's slope: its root turns to the ground's normal under its middle while it falls (0.3-1.4 s of the clip; the melt
    /// at its 0.7 speed), about its drawn middle;</item>
    /// <item>a body whose middle hangs over an edge (a deck, a ramp's side, a rail) slides off it (up to 1.6 m/s) and falls (1 g) to what is under it;</item>
    /// <item>the blast landing without a free ragdoll (Airborne): it flew with its root tipped 100 degrees and then played death_back from standing on that
    /// tipped root (the body folded into the ground, 0.75 to over 1.5 m deep): now at the landing the root turns upright (0.25 s), facing the blast, while
    /// the Animator blends into death_back's lying end (the body lies on its back, head away from the blast, as it flew), then it is grounded as the
    /// others; its landing point is kept inside the map.</item>
    /// </list>
    /// Left as before: a robot that did not die standing on that ground (a fort roof, a tank deck: more than OnGroundM off GroundY), one with a rock, a
    /// gun or a building within NearSolidM, ragdolls, the lava melt. Switches <see cref="OzBodyGround.On"/> (off = before round 11) and
    /// <see cref="OzRestSurface.On"/> (off = round 11 exactly).
    /// </summary>
    public partial class UnitView
    {
        float bgOff, bgVy, bgTarget, bgNext; int bgState;                     // applied vertical offset (m), fall speed, wanted offset, next measure (deathT), 0 undecided / 1 on / -1 off
        Vector3 bgSlide, bgSlideDir, bgMid; float bgSlideV, bgAirT; bool bgEdge, bgAir, bgMidSet;   // r11b: applied flat offset (slide + turn about the middle), the slide, the drawn middle, the Airborne landing
        Quaternion bgBaseRot = Quaternion.identity, bgFromRot = Quaternion.identity, bgTilt = Quaternion.identity;
        OzBodyGroundLate bgLate;
        static readonly List<Vector3> bgPts = new List<Vector3>(512);
        static readonly List<Renderer> bgRs = new List<Renderer>(32);
        // r11b fix round 1 (Play look 17 Nov: every death-clip body lay at hip height on its gun planted in the ground, 0.5-1.1 m in the air): the gun
        // leaves the hands, the arms come level with the shoulders as the body lies down, the body is measured as drawn (every skin vertex) once at rest
        Transform bgArmL, bgArmR, bgHandL, bgHandR; bool bgArmsSet, bgGunTried; OzDroppedGun bgGun;
        static Mesh bgBake; static readonly List<Vector3> bgVb = new List<Vector3>(8192);
        /// <summary>Checks: the body is popping away (its dropped gun pops with it).</summary>
        internal bool BgPopping => popping || !gameObject.activeInHierarchy;

        /// <summary>UpdateDeath, after the death's own pose of this frame (Death.Anim, Death.Melt; Death.Airborne once landed). baseReset: the death put the
        /// root back on its own path this frame (Airborne): the offsets are laid on again. The measure and the move: <see cref="BodyGroundLate"/>.</summary>
        void BodyGround(float dt, bool baseReset)
        {
            if (baseReset && (bgOff != 0f || bgSlide != Vector3.zero)) transform.position += Vector3.up * bgOff + bgSlide;
            if (!OzBodyGround.On || popping || Obj == null || !game) return;
            if (bgState == 0)
            {
                var r = transform.position; float gy = game.GroundY(r.x, r.z);
                bool near = OzRockSolid.RockWithin(r, OzBodyGround.NearSolidM);                         // a rock, a gun or a building beside it: left as before
                bgState = Mathf.Abs(r.y - gy) <= OzBodyGround.OnGroundM && !near ? 1 : -1;
                if (bgState > 0) OzBodyGround.Bodies++; else if (near) OzBodyGround.NearSolid++; else OzBodyGround.LeftAsBefore++;
                bgNext = deathT; bgBaseRot = transform.rotation;
                if (bgState > 0 && death == Death.Airborne && OzRestSurface.On) AirLand();
            }
            if (bgState < 0) return;
            if (!OzRestSurface.On) { if (bgLate) bgLate.enabled = false; BodyGroundLate(dt); return; }  // round 11 exactly: measured and moved here
            if (!bgLate && !TryGetComponent(out bgLate)) bgLate = gameObject.AddComponent<OzBodyGroundLate>();
            bgLate.v = this; if (!bgLate.enabled) bgLate.enabled = true;
        }

        /// <summary>r11b, the Airborne landing: the root turns upright facing the blast (from the flight's 100 degree tip, 0.25 s) while the Animator blends
        /// into death_back's lying end (round 11: death_back from standing on the tipped root).</summary>
        void AirLand()
        {
            bgAir = true; bgAirT = deathT; bgFromRot = transform.rotation;
            var away = Vector3.Cross(spinAxis, Vector3.up); away.y = 0f;
            float y = away.sqrMagnitude > 1e-4f ? Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg : yaw;    // facing the blast: it lies on its back, head away from it
            bgBaseRot = Quaternion.Euler(0f, y + modelYaw, 0f);
            if (anim) { anim.speed = 1f; anim.CrossFadeInFixedTime("death_back", OzBodyGround.AirBlendS, -1, OzBodyGround.ClipEndS(anim, "death_back")); }
            OzBodyGround.AirLandings++;
        }

        /// <summary>r11b fix round 1: the death clips' lying end (LEGACY death_back / death_side on the BS-01 and hero rigs) has the arms reaching down
        /// under the body (they held the gun that was planted in the ground). As the body lies down each upper arm turns, the smallest way, until the
        /// line shoulder - hand lies along the ground (weight w); an arm whose hand is not under its shoulder is left as the clip has it. After the
        /// Animator every frame (it writes the bones again each frame: nothing adds up).</summary>
        void LayArms(float w)
        {
            if (!bgArmsSet)
            {
                bgArmsSet = true;
                if (anim && anim.isHuman)
                {
                    bgArmL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); bgArmR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    bgHandL = anim.GetBoneTransform(HumanBodyBones.LeftHand); bgHandR = anim.GetBoneTransform(HumanBodyBones.RightHand);
                }
            }
            var n = bgTilt * Vector3.up;
            LayArm(bgArmL, bgHandL, n, w); LayArm(bgArmR, bgHandR, n, w);
        }

        void LayArm(Transform up, Transform hand, Vector3 n, float w)
        {
            if (!up || !hand) return;
            var d = hand.position - up.position;
            if (-Vector3.Dot(d, n) <= OzBodyGround.ArmDownM) return;                                  // the hand not under its shoulder
            var flat = Vector3.ProjectOnPlane(d, n);
            if (flat.sqrMagnitude < 1e-4f) { flat = Vector3.ProjectOnPlane(up.position - (bgMidSet ? bgMid : transform.position), n); if (flat.sqrMagnitude < 1e-4f) return; }   // straight down: out to its side
            var q = Quaternion.FromToRotation(d, flat.normalized * d.magnitude);
            up.rotation = Quaternion.Slerp(Quaternion.identity, q, w) * up.rotation;
            OzBodyGround.ArmsLaid++;
        }

        /// <summary>r11b fix round 1: the drawn points as drawn now: every skin vertex (BakeMesh with useScale = the renderer's own space, its
        /// localToWorldMatrix gives the world: as the Play check measures), the rigid parts by OzDrawnPoints.</summary>
        static int CollectExact(Transform root, List<Vector3> into, List<Renderer> scratch)
        {
            into.Clear(); if (!root) return 0;
            scratch.Clear(); root.GetComponentsInChildren(false, scratch);
            foreach (var r in scratch)
            {
                if (!OzDrawnPoints.Solid(r)) continue;
                if (r is SkinnedMeshRenderer sk)
                {
                    if (!sk.sharedMesh) continue;
                    if (!bgBake) bgBake = new Mesh { name = "OzBodyGround exact" };
                    sk.BakeMesh(bgBake, true); bgVb.Clear(); bgBake.GetVertices(bgVb);
                    var m = sk.transform.localToWorldMatrix;
                    for (int i = 0; i < bgVb.Count; i++) into.Add(m.MultiplyPoint3x4(bgVb[i]));
                    continue;
                }
                if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var p = OzDrawnPoints.Rigid(mf.sharedMesh); if (p == null) continue;
                var mt = r.transform.localToWorldMatrix;
                for (int i = 0; i < p.Length; i++) into.Add(mt.MultiplyPoint3x4(p[i]));
            }
            OzBodyGround.ExactMeasures++;
            return into.Count;
        }

        /// <summary>The grounding of this frame, after every pose writer (OzBodyGroundLate; round 11 / OzRestSurface off: from BodyGround).</summary>
        internal void BodyGroundLate(float dt)
        {
            bool clip = death == Death.Anim || death == Death.Melt || (death == Death.Airborne && deathT >= airT);
            if (!OzBodyGround.On || popping || bgState <= 0 || Obj == null || !game || !clip || !gameObject.activeInHierarchy) { if (bgLate) bgLate.enabled = false; return; }
            bool rest = OzRestSurface.On;
            var oldPos = transform.position;
            if (rest)
            {   // the body along the ground's slope (and the Airborne landing upright), turned about its drawn middle
                float w;
                if (bgAir) w = Mathf.SmoothStep(0f, 1f, (deathT - bgAirT) / Mathf.Max(0.05f, OzBodyGround.AirBlendS));
                else { float sp = death == Death.Melt ? 0.7f : 1f; w = Mathf.SmoothStep(0f, 1f, (deathT - OzBodyGround.TiltFromS / sp) / Mathf.Max(0.05f, (OzBodyGround.TiltToS - OzBodyGround.TiltFromS) / sp)); }
                var was = transform.rotation;
                var want = bgAir ? Quaternion.Slerp(bgFromRot, bgTilt * bgBaseRot, w) : Quaternion.Slerp(bgBaseRot, bgTilt * bgBaseRot, w);
                if (Quaternion.Angle(was, want) > 0.01f)
                {
                    transform.rotation = want;
                    if (bgMidSet) { var np = bgMid + (want * Quaternion.Inverse(was)) * (oldPos - bgMid); var d = np - oldPos; bgOff += d.y; bgTarget += d.y; bgSlide += new Vector3(d.x, 0f, d.z); transform.position = np; oldPos = np; }
                }
            }
            if (rest && death != Death.Melt && OzBodyGround.ArmsOn)
            {   // r11b fix round 1: the gun out of the hands (a clip death a moment after it is hit; the blast landing as it lands), the arms laid level
                if (!bgGunTried && (bgAir || deathT >= OzBodyGround.GunDropS)) { bgGunTried = true; bgGun = OzDroppedGun.Drop(this); }
                float wa = bgAir ? Mathf.SmoothStep(0f, 1f, (deathT - bgAirT) / Mathf.Max(0.05f, OzBodyGround.AirArmsS))
                                 : Mathf.SmoothStep(0f, 1f, (deathT - OzBodyGround.ArmsFromS) / Mathf.Max(0.05f, OzBodyGround.ArmsToS - OzBodyGround.ArmsFromS));
                if (wa > 0f) LayArms(wa);
            }
            if (deathT >= bgNext && OzBodyGround.TakeMeasure())
            {
                bool fast = deathT < OzBodyGround.ClipS || death == Death.Melt || bgEdge || bgSlideV > 0f || bgVy > 0f || (bgAir && deathT - bgAirT < 1f);
                bgNext = deathT + (death == Death.Melt && rest ? 0f : fast ? OzBodyGround.EveryS : OzBodyGround.SlowS);   // r11b: the melt every frame (it squashes every frame)
                bool exact = rest && (death == Death.Melt || !fast);                                    // r11b fix round 1: the melt and a body at rest by every skin vertex as drawn (the bake's points follow one bone each)
                if ((exact ? CollectExact(transform, bgPts, bgRs) : OzDrawnPoints.Collect(transform, Obj.Type, bgPts, true, bgRs)) > 0)
                {
                    Vector3 low; float lowS = 0f;
                    float gap = rest ? OzRestSurface.Gap(bgPts, transform, OzBodyGround.WallM, out low, out lowS)
                                     : OzDrawnPoints.Gap(bgPts, game, false, out low, OzBodyGround.WallM);   // round 11: a point deep under the ground there is in a deck's side
                    if (!rest) lowS = low.y - gap;
                    if (!float.IsNaN(gap) && gap < 1e6f)
                    {
                        float want = bgOff - (gap - OzBodyGround.LiftM);
                        bgTarget = Mathf.Clamp(want, -OzBodyGround.MaxDownM, rest ? OzBodyGround.MaxUpM : OzBodyGround.MaxUpR11M);
                        OzBodyGround.Measures++;
                        if (rest)
                        {
                            var mid = OzRestSurface.Mid(bgPts); bgMid = mid; bgMidSet = true;
                            var n = OzRestSurface.Normal(game, mid.x, mid.z, OzBodyGround.TiltR);
                            bgTilt = Quaternion.Slerp(bgTilt, Quaternion.FromToRotation(Vector3.up, n), 0.5f);
                            var dir = Vector3.zero;
                            bgEdge = gap < OzBodyGround.EdgeRestM && OzRestSurface.OverEdge(mid, low, lowS, n, transform, out dir);
                            if (bgEdge) { bgSlideDir = dir; OzBodyGround.EdgeSlides++; }
                        }
                    }
                }
            }
            float old = bgOff; var oldSlide = bgSlide;
            if (rest && death == Death.Melt && Mathf.Abs(bgTarget - bgOff) < OzBodyGround.MeltSnapM) { bgOff = bgTarget; bgVy = 0f; }   // r11b: the melt follows its squash at once (a small step, never a drop over an edge)
            else if (bgTarget < bgOff - OzBodyGround.DeadM) { bgVy = Mathf.Min(bgVy + OzBodyGround.Gravity * dt, OzBodyGround.MaxFallMs); bgOff = Mathf.Max(bgTarget, bgOff - bgVy * dt); }
            else if (bgTarget > bgOff + OzBodyGround.DeadM) { bgVy = 0f; bgOff = Mathf.Min(bgTarget, bgOff + OzBodyGround.RiseMs * dt); }
            else bgVy = 0f;
            if (bgEdge) bgSlideV = Mathf.Min(OzBodyGround.SlideMaxMs, bgSlideV + OzBodyGround.SlideAcc * dt);
            else bgSlideV = Mathf.Max(0f, bgSlideV - 2f * OzBodyGround.SlideAcc * dt);
            if (bgSlideV > 0f) bgSlide += bgSlideDir * (bgSlideV * dt);
            var delta = Vector3.up * (bgOff - old) + (bgSlide - oldSlide);
            if (delta != Vector3.zero)
            {
                transform.position += delta;
                if (-bgOff > OzBodyGround.MaxLoweredM) OzBodyGround.MaxLoweredM = -bgOff;
                if (bgOff > OzBodyGround.MaxRaisedM) OzBodyGround.MaxRaisedM = bgOff;
            }
        }
    }

    /// <summary>1.5 kit robot_pieces_ground_r11 / r11b (A35): the switch, the numbers and the counters of the death-clip body's grounding
    /// (View/UnitView.BodyGround.cs) and of the run-over pancake's seat (<see cref="Pancake"/>, <see cref="PancakeSeat"/>).</summary>
    public static class OzBodyGround
    {
        /// <summary>false = exactly as before round 11 (the clip body where the clip puts it; the pancake 4 cm over the ground, level).</summary>
        public static bool On = true;
        /// <summary>The lowest drawn point this much over the ground (m); a robot that died more than OnGroundM over or under GroundY is left as before (a roof,
        /// a hull), and one with a rock / gun / building within NearSolidM (flat, m); the most the body goes down / up (m; round 11 rose 0.35 m at most);
        /// a drawn point more than WallM under its support is inside a deck's side (left out); measures while the clip plays (s, for ClipS s) and after; its
        /// fall (m/s2) and fastest fall, its rise (m/s); no move within DeadM (m).</summary>
        public static float LiftM = 0.01f, OnGroundM = 0.35f, NearSolidM = 4f, MaxDownM = 12f, MaxUpM = 1f, MaxUpR11M = 0.35f, WallM = 0.45f, EveryS = 0.05f, SlowS = 0.3f, ClipS = 2.6f, Gravity = 13f, MaxFallMs = 9f, RiseMs = 1.5f, DeadM = 0.003f;
        /// <summary>r11b: the turn onto the ground's slope (from / to s of the clip at speed 1; the ground's normal from samples TiltR m around the middle),
        /// the Airborne landing's turn upright and blend (s), the slide off an edge (m/s2, m/s; only while the body lies within EdgeRestM of its support), a melting
        /// body's step to its target taken at once when smaller than MeltSnapM (m).</summary>
        public static float TiltFromS = 0.3f, TiltToS = 1.4f, TiltR = 0.8f, AirBlendS = 0.25f, SlideAcc = 3f, SlideMaxMs = 1.6f, EdgeRestM = 0.1f, MeltSnapM = 0.15f;
        /// <summary>r11b fix round 1 (switch ArmsOn): a clip death's gun leaves the hands GunDropS s after the death (the blast landing: as it lands);
        /// the arms come level from ArmsFromS to ArmsToS s of the clip (the blast landing: over AirArmsS s from its landing); a hand more than ArmDownM
        /// under its shoulder counts as reaching down.</summary>
        public static bool ArmsOn = true;
        public static float GunDropS = 0.35f, ArmsFromS = 0.45f, ArmsToS = 1.35f, AirArmsS = 0.35f, ArmDownM = 0.05f;
        /// <summary>r11b fix round 1 counters: arm turns written, exact measures (every skin vertex).</summary>
        public static int ArmsLaid, ExactMeasures;
        /// <summary>At most this many bodies measure in one frame (the rest a frame later).</summary>
        public static int MaxMeasuresPerFrame = 6;
        /// <summary>Counters (Play checks): clip bodies grounded, left as before (not on the ground at the death; a rock / gun / building beside), measures; the most any went down / up (m);
        /// pancakes seated; r11b: Airborne landings turned upright, measures that found the body over an edge.</summary>
        public static int Bodies, LeftAsBefore, NearSolid, Measures, Pancakes, AirLandings, EdgeSlides; public static float MaxLoweredM, MaxRaisedM;
        static int frame = -1, inFrame;
        static readonly List<Vector3> pts = new List<Vector3>(4096), vb = new List<Vector3>(8192);
        static readonly List<MeshFilter> mfs = new List<MeshFilter>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Statics() { Bodies = LeftAsBefore = NearSolid = Measures = Pancakes = AirLandings = EdgeSlides = ArmsLaid = ExactMeasures = 0; MaxLoweredM = MaxRaisedM = 0f; On = true; ArmsOn = true; frame = -1; inFrame = 0; }

        internal static bool TakeMeasure()
        {
            if (frame != Time.frameCount) { frame = Time.frameCount; inFrame = 0; }
            if (inFrame >= MaxMeasuresPerFrame) return false;
            inFrame++; return true;
        }

        public static string Status() =>
            $"death-clip bodies (A35 r11 / r11b): {(On ? "ON" : "OFF")}, grounded {Bodies}, left as before: not on the ground {LeftAsBefore}, a rock / gun / building beside {NearSolid}; measures {Measures}, most lowered {MaxLoweredM:0.00} m, most raised {MaxRaisedM:0.00} m; pancakes seated {Pancakes}; blast landings turned upright {AirLandings}; over an edge {EdgeSlides}; fix round 1: arms {(ArmsOn ? "ON" : "OFF")}, guns out of the hands {OzDroppedGun.Dropped} (on the ground {OzDroppedGun.Landed}), arm turns {ArmsLaid}, exact measures {ExactMeasures}; {OzRestSurface.Status()}";

        /// <summary>r11b: the length (s, x 0.97) of the controller's clip named like `name` (the lying end of a death clip); not found: 3 s (a death state
        /// does not loop: the blend then ends on its last frame).</summary>
        public static float ClipEndS(Animator a, string name)
        {
            var c = a ? a.runtimeAnimatorController : null;
            if (c) foreach (var clip in c.animationClips) if (clip && clip.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) return clip.length * 0.97f;
            return 3f;
        }

        /// <summary>SquashedRemains.Make (DV65 run over): the pancake's root on the ground's slope under the robot (four samples, as the chips; a step
        /// under it: level), so the flattened copy (x flatY along the slope's normal) lies with its lowest point LiftM over the ground. Was: 4 cm over
        /// the ground at the robot's root, level (on a slope its downhill side in the air).</summary>
        public static void Pancake(Transform root, ZGame g, Vector3 at, float gy, Vector3 dir, float flatY)
        {
            if (!On || !root || !g) return;
            const float d = 0.6f;
            var n = new Vector3(g.GroundY(at.x - d, at.z) - g.GroundY(at.x + d, at.z), 2f * d, g.GroundY(at.x, at.z - d) - g.GroundY(at.x, at.z + d)).normalized;
            if (n.y < 0.85f) n = Vector3.up;
            root.SetPositionAndRotation(new Vector3(at.x, gy, at.z) + n * (LiftM / Mathf.Max(0.5f, 1f - flatY)), Quaternion.FromToRotation(Vector3.up, n) * Quaternion.LookRotation(dir, Vector3.up));
            Pancakes++;
        }

        /// <summary>r11b, SquashedRemains.Make once its flattened copies are made: the pancake as it will lie (x wideXZ, x flatY) measured by its drawn
        /// points (every vertex of its baked skins, the rigid parts' drawn points) against the real surface under each part (OzRestSurface.Gap), and moved
        /// up or down so its lowest point lies LiftM over it (round 11: the root 1 cm over GroundY at the robot's feet along a 1.2 m slope sample: on the
        /// ramp's foot 4-12 cm into the floor, over the deck's break 6-10 cm up).</summary>
        public static void PancakeSeat(Transform root, float flatY, float wideXZ)
        {
            if (!On || !OzRestSurface.On || !root) return;
            var keep = root.localScale; root.localScale = new Vector3(wideXZ, flatY, wideXZ);
            pts.Clear(); mfs.Clear(); root.GetComponentsInChildren(false, mfs);
            foreach (var mf in mfs)
            {
                var m = mf ? mf.sharedMesh : null; if (!m) continue;
                var r = mf.GetComponent<MeshRenderer>(); if (!OzDrawnPoints.Solid(r)) continue;
                var mt = mf.transform.localToWorldMatrix;
                if (m.isReadable) { vb.Clear(); m.GetVertices(vb); for (int i = 0; i < vb.Count; i++) pts.Add(mt.MultiplyPoint3x4(vb[i])); }   // the baked skins (pool meshes: never cached)
                else { var p = OzDrawnPoints.Rigid(m); if (p != null) for (int i = 0; i < p.Length; i++) pts.Add(mt.MultiplyPoint3x4(p[i])); }
            }
            root.localScale = keep;
            float gap = OzRestSurface.Gap(pts, root, WallM, out _, out _);
            if (float.IsNaN(gap)) return;
            float dy = Mathf.Clamp(LiftM - gap, -OzRestSurface.MaxRaiseM, OzRestSurface.MaxRaiseM);
            if (Mathf.Abs(dy) > 0.001f) root.position += Vector3.up * dy;
            OzRestSurface.PancakesSeated++;
        }
    }
}
