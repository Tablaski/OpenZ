// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the sidekick BS-01 on the stage (OzEvilStage).
// every syllable, the lids grow over them for the moods (worried, scared, confused, happy, angry, suspicious; blinks), the pupils look,
// the whole body squashes, bounces, shakes, leans and shrugs. Always confused and scared: his default mood is Worried. The eye rig and
// its presets are the characters step's (README section 6 / SidekickEyeRig): every axis is measured on the model at build, so the
// axis conversion of the import does not matter. His mitten hands carry the giant blue button in (walk take + the arms held on the box)
// and set it down on its stand before EvilZod (nothing changes hands: the box never turns).
// Hand targets are metres in his own frame (x = his right, y = up from his feet, z = forward), measured against this rig: the shoulders
// 0.79 m up and 0.21 m out, 0.396 / 0.364 m from the shoulder to the wrist, the body front 0.20-0.235 m ahead of the root at 0.75-1.30 m
// (his eyes at 1.00-1.08 m); every target sits at 72-89 % of that reach, scaled by k = his height / 1.45. The box (0.44 m deep) is hugged at
// the chest (its back face 0.25 m ahead of the root, 5 cm clear of his body, the dome top at 0.93 m under his eyes), the hands on its rear
// top corners (the right hand on its right side); it goes down onto the stand's top (0.48 m) with the same grip (scratch/fix2/sim_after.txt).
// and his head at most HeadYawMax deg, so his face stays within 45 deg of the lens in every framing (the two-shots 28-33 deg, his own
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        enum SkHand { Side, Up, Shrug, Salute, Carry, SetDown, Present, Cover, Fidget }
        enum SkLook { Ez, Cam, Away, Back }
        public enum SkMood { Neutral, Blink, Scared, Worried, Angry, Happy, Suspicious, Confused }
        struct EyeShape
        {
            public float top, bottom, tilt, scale, squash, pupil, lookX, lookY;
            public static EyeShape Open => new EyeShape { scale = 1f, squash = 1f, pupil = 1f };
            public static EyeShape Lerp(EyeShape a, EyeShape b, float t) => new EyeShape
            {
                top = Mathf.Lerp(a.top, b.top, t), bottom = Mathf.Lerp(a.bottom, b.bottom, t), tilt = Mathf.Lerp(a.tilt, b.tilt, t), scale = Mathf.Lerp(a.scale, b.scale, t),
                squash = Mathf.Lerp(a.squash, b.squash, t), pupil = Mathf.Lerp(a.pupil, b.pupil, t), lookX = Mathf.Lerp(a.lookX, b.lookX, t), lookY = Mathf.Lerp(a.lookY, b.lookY, t)
            };
        }
        sealed class Eye
        {
            public Transform root, glow, pupil, lidTop, lidBot; public bool left;
            public Vector3 rootScale0, pupilPos0, pupilScale0, topScale0, botScale0; public Quaternion topRot0, botRot0;
            public int topAxis, botAxis, upAxis; public float topUnit, botUnit, radius = 0.039f, tiltSign = 1f;
            public Vector3 viewL, upL, rightL; public EyeShape cur = EyeShape.Open;
        }

        /// <summary>The facing cheat: the share of the turn toward EvilZod his BODY takes (the rest is head and pupils), the head's yaw clamp
        /// (deg) and how much of the look the head could not take goes into the pupils (1 = all of 60 deg).</summary>
        public static float FaceShare = 0.15f, HeadYawMax = 30f, GazeShare = 0.9f;

        Robot sk; bool skOnStage = true; Eye eyeL, eyeR; Material skEyeMat;
        SkHand skHand = SkHand.Side; float skHandAt, skHandHold; Vector3 skRPos, skLPos, skRVel, skLVel; bool skHandsInit;
        SkLook skLook = SkLook.Ez; SkMood skMood = SkMood.Worried; float skBlinkT = -1f, skNextBlink = 3f;
        float skYaw, skYawGoal, skYawVel, skLean, skLeanGoal, skSquash = 1f, skSquashGoal = 1f, skTilt, skTiltGoal, skTiltUntil = -1f, skLeanUntil = -1f, skSquashUntil = -1f;
        float skShakeUntil = -1f, skBounceAt = -10f, skBounceN, skStartleAt = -10f, skRecoilUntil = -1f, skLookAwayUntil = -1f, skWalkAt = -10f, skWalkFor, skWalkRate = 1f, skBreath;
        float skMouth, skMouthSmooth, skLastMouth, skNextOnset, skNodT = 9f, skGazeX; Vector3 skWalkDir = Vector3.right, skWalkTo; int skStepParity = -1; bool skCarry, skWalkHasTarget;
        float skSetAt = -10f, skAfterWalkYaw, skPresentHold; bool skSetWanted, skBoxDown, skPresentWanted;
        Transform skBoxHolder;
        static readonly string[] LightSteps = { "OpenZ/Audio/Sfx/sd_step_robot_concrete_02", "OpenZ/Audio/Sfx/sd_step_robot_concrete_04", "OpenZ/Audio/Sfx/sd_step_robot_concrete_06", "OpenZ/Audio/Sfx/sd_step_robot_concrete_08" };
        static readonly string[] SkServos = { "OpenZ/Audio/Sfx/sd_robot_servo_a", "OpenZ/Audio/Sfx/sd_robot_servo_b", "OpenZ/Audio/Sfx/sd_robot_servo_c" };

        public Vector3 SkPos => sk != null && sk.Ok ? sk.Root.localPosition : new Vector3(0.9f, 0f, 0.3f);
        /// <summary>His height: the rig of the hand numbers (1.45 m) at his stage scale (the renderer bounds read 1.76 m with the dish).</summary>
        public float SkHeight => sk != null && sk.Ok ? 1.45f * sk.Rig.Scale : 1.45f * SkScale;
        /// <summary>His eyes' world point (EvilZod looks here, the camera's singles aim here).</summary>
        public Vector3 SkHeadWorld => sk != null && sk.Ok && sk.Rig.Head ? sk.Rig.Head.position + Vector3.up * 0.04f : transform.TransformPoint(SkPos + new Vector3(0f, 1.15f, 0f));
        public SkMood Mood => skMood;
        /// <summary>The Play checks: the box stands on its stand (the set-down is done).</summary>
        public bool BoxOnStand => skBoxDown;

        // ------------------------------------------------------------------------------------------------ build
        void BuildSidekick()
        {
            sk = LoadRobot("Sidekick", "Sidekick_Body", "M_Sidekick_Body", SkScale);
            if (!sk.Ok) return;
            sk.Rig.LoadWalk(Folder, "Sidekick");
            var eyeMatAsset = Resources.Load<Material>(Folder + "M_Sidekick_Eye"); var pupilMat = Resources.Load<Material>(Folder + "M_Sidekick_Pupil");
            if (eyeMatAsset) { skEyeMat = new Material(eyeMatAsset) { hideFlags = HideFlags.DontSave }; madeMats.Add(skEyeMat); }
            foreach (var rd in sk.Go.GetComponentsInChildren<Renderer>(true))
            {
                if (rd.name.EndsWith("_Glow") && skEyeMat) rd.sharedMaterial = skEyeMat;
                else if (rd.name.EndsWith("_Pupil") && pupilMat) rd.sharedMaterial = pupilMat;
                else if (rd.name.Contains("_Lid") && sk.BodyMat) rd.sharedMaterial = sk.BodyMat;
                if (rd.name.StartsWith("Eye_")) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            eyeL = BuildEye("Eye_L", true); eyeR = BuildEye("Eye_R", false);
            if (eyeL != null && eyeR != null) { SetupEye(eyeL, eyeR); SetupEye(eyeR, eyeL); }
            else Debug.LogWarning("[EvilZod] the sidekick's Eye_L / Eye_R are missing: his eyes stay still");
            skBoxHolder = new GameObject("BoxHolderSidekick").transform; skBoxHolder.SetParent(transform, false);
            sk.SetActive(false);
        }

        Eye BuildEye(string n, bool left)
        {
            var r = sk.Rig.Find(n); if (!r) return null;
            var e = new Eye { root = r, left = left, glow = sk.Rig.Find(n + "_Glow"), pupil = sk.Rig.Find(n + "_Pupil"), lidTop = sk.Rig.Find(n + "_LidTop"), lidBot = sk.Rig.Find(n + "_LidBottom") };
            e.rootScale0 = r.localScale;
            if (e.pupil) { e.pupilPos0 = e.pupil.localPosition; e.pupilScale0 = e.pupil.localScale; }
            if (e.lidTop) { e.topScale0 = e.lidTop.localScale; e.topRot0 = e.lidTop.localRotation; e.topAxis = Collapsed(e.topScale0, out e.topUnit); }
            if (e.lidBot) { e.botScale0 = e.lidBot.localScale; e.botRot0 = e.lidBot.localRotation; e.botAxis = Collapsed(e.botScale0, out e.botUnit); }
            return e;
        }
        /// <summary>The lid's height axis is the one collapsed to 0.1 % at rest; unit = the scale of its other axes.</summary>
        static int Collapsed(Vector3 s, out float unit)
        {
            int k = 0; float m = Mathf.Abs(s.x);
            if (Mathf.Abs(s.y) < m) { k = 1; m = Mathf.Abs(s.y); }
            if (Mathf.Abs(s.z) < m) k = 2;
            unit = 0f; for (int i = 0; i < 3; i++) if (i != k) unit = Mathf.Max(unit, Mathf.Abs(s[i]));
            if (unit <= 0f) unit = 1f;
            return k;
        }
        void SetupEye(Eye e, Eye other)
        {
            Vector3 n = sk.Rig.Fwd;
            var mf = e.glow ? e.glow.GetComponent<MeshFilter>() : null;
            if (mf && mf.sharedMesh)
            {   // the glow disc's facing: its thinnest axis (the mesh bounds are readable without Read/Write; reading the normals of a mesh
                // imported without Read/Write logged "Not allowed to access normals on mesh 'Eye_L_Glow'" every time the stage was built)
                var ext = mf.sharedMesh.bounds.extents; int thin = ext.x <= ext.y && ext.x <= ext.z ? 0 : ext.y <= ext.z ? 1 : 2;
                Vector3 axis = Vector3.zero; axis[thin] = 1f;
                Vector3 wn = e.glow.TransformDirection(axis).normalized;
                if (Vector3.Dot(wn, sk.Rig.Fwd) < 0f) wn = -wn;
                if (Vector3.Angle(wn, sk.Rig.Fwd) < 75f) n = wn;
                e.radius = Mathf.Max(0.02f, ext.magnitude * 0.7f * e.glow.lossyScale.x);
            }
            e.viewL = e.root.InverseTransformDirection(n).normalized;
            e.upL = e.root.InverseTransformDirection(Vector3.up).normalized;
            e.rightL = e.root.InverseTransformDirection(sk.Rig.Right).normalized;
            Vector3 a = new Vector3(Mathf.Abs(e.upL.x), Mathf.Abs(e.upL.y), Mathf.Abs(e.upL.z));
            e.upAxis = a.x > a.y && a.x > a.z ? 0 : (a.y > a.z ? 1 : 2);
            Vector3 inner = e.root.InverseTransformDirection(other.root.position - e.root.position).normalized;
            e.tiltSign = Vector3.Dot(Vector3.Cross(e.viewL, inner), e.upL) > 0f ? -1f : 1f;
        }

        /// <summary></summary>
        static EyeShape Preset(SkMood m, bool left)
        {
            var s = EyeShape.Open;
            switch (m)
            {
                case SkMood.Blink: s.top = 1f; s.bottom = 1f; break;
                case SkMood.Scared: s.scale = 1.18f; s.pupil = 0.55f; break;
                case SkMood.Worried: s.top = 0.30f; s.tilt = -22f; break;
                case SkMood.Angry: s.top = 0.38f; s.tilt = 24f; break;
                case SkMood.Happy: s.bottom = 0.62f; break;
                case SkMood.Suspicious: s.top = left ? 0.55f : 0.40f; s.bottom = left ? 0.30f : 0.25f; break;
                case SkMood.Confused: if (left) s.scale = 1.10f; else { s.top = 0.42f; s.tilt = -15f; s.scale = 0.95f; } s.lookX = 0.5f; s.lookY = 0.4f; break;
            }
            return s;
        }
        static SkMood MoodOf(string w)
        {
            switch ((w ?? "").ToLowerInvariant())
            {
                case "neutral": return SkMood.Neutral;
                case "scared": return SkMood.Scared;
                case "angry": return SkMood.Angry;
                case "happy": return SkMood.Happy;
                case "suspicious": return SkMood.Suspicious;
                case "confused": return SkMood.Confused;
                default: return SkMood.Worried;
            }
        }

        void ResetSidekick(Vector3 at, bool onStage)
        {
            if (sk == null || !sk.Ok) return;
            sk.Rig.StopWalk(0.01f); sk.Rig.ResetToRest();
            sk.Root.localPosition = at; sk.Root.localScale = Vector3.one * SkScale; skYaw = skYawGoal = 0f; skYawVel = 0f; ApplyYaw(sk, 0f);
            skHand = SkHand.Side; skHandAt = clock; skHandHold = 0f; skHandsInit = false; skLook = SkLook.Ez; skMood = SkMood.Worried; skBlinkT = -1f; skNextBlink = 2.5f;
            skLean = skLeanGoal = skTilt = skTiltGoal = 0f; skSquash = skSquashGoal = 1f; skTiltUntil = skLeanUntil = skSquashUntil = skShakeUntil = skRecoilUntil = skLookAwayUntil = -1f;
            skBounceAt = skStartleAt = skWalkAt = skSetAt = -10f; skWalkRate = 1f; skCarry = false; skWalkHasTarget = false; skSetWanted = false; skBoxDown = false; skGazeX = 0f; skPresentWanted = false;
            skMouth = skMouthSmooth = skLastMouth = 0f; skNodT = 9f; skLookInit = false; skPoleInit = false; skLeanV = skTiltV = skSquashV = 0f;
            if (eyeL != null) eyeL.cur = Preset(SkMood.Worried, true); if (eyeR != null) eyeR.cur = Preset(SkMood.Worried, false);
            skOnStage = onStage; sk.SetActive(onStage && Live); skDrawOff = Vector3.zero;
        }

        // ------------------------------------------------------------------------------------------------ beats
        void SidekickBeat(OzBlueBeat b)
        {
            if (sk == null || !sk.Ok) return;
            switch (b.beat)
            {
                case "face_ez": { Vector3 to = EzPos - SkPos; to.y = 0f; skYawGoal = Vector3.SignedAngle(Vector3.forward, to.normalized, Vector3.up) * FaceShare; break; }   // the body stays 3/4 to the lens
                case "look_ez": skLook = SkLook.Ez; break;
                case "look_cam": skLook = SkLook.Cam; break;
                case "look_away": skLook = SkLook.Away; skLookAwayUntil = b.hold > 0f ? clock + b.hold : -1f; break;
                case "look_back": skLook = SkLook.Back; skLookAwayUntil = b.hold > 0f ? clock + b.hold : -1f; break;
                case "mood": skMood = MoodOf(b.word); break;
                case "blink": skBlinkT = 0f; break;
                case "lean": skLeanGoal = 9f * b.a; skLeanUntil = b.hold > 0f ? clock + b.hold : -1f; break;
                case "shrink": skSquashGoal = 0.90f; skLeanGoal = -4f; skSquashUntil = clock + Mathf.Max(0.8f, b.hold > 0f ? b.hold : 1.6f); skLeanUntil = skSquashUntil; SetSkHand(SkHand.Fidget, 1.6f); SkServo(); break;
                case "shrug": SetSkHand(SkHand.Shrug, b.hold > 0f ? b.hold : 1.3f); skTiltGoal = 10f; skTiltUntil = clock + (b.hold > 0f ? b.hold : 1.3f); SkServo(); break;
                case "arms_up": SetSkHand(SkHand.Up, b.hold > 0f ? b.hold : 1.2f); SkServo(); break;
                case "salute": SetSkHand(SkHand.Salute, b.hold > 0f ? b.hold : 1.1f); SkServo(); Sfx("OpenZ/Audio/Sfx/sd_crate_pickup", -18f, 1.4f, "sk_salute_tap", sfxB); break;
                case "cover": SetSkHand(SkHand.Cover, b.hold > 0f ? b.hold : 1.4f); skSquashGoal = 0.9f; skSquashUntil = clock + (b.hold > 0f ? b.hold : 1.4f); SkServo(); break;
                case "fidget": SetSkHand(SkHand.Fidget, b.hold > 0f ? b.hold : 2.4f); break;
                case "present":                                                                            // both mittens open toward the box: "Right here, sir!"
                    if ((skHand == SkHand.SetDown || skSetWanted) && !skBoxDown) { skPresentWanted = true; skPresentHold = b.hold > 0f ? b.hold : 1.2f; }   // the box is not down yet: right after it is
                    else { SetSkHand(SkHand.Present, b.hold > 0f ? b.hold : 1.2f); SkServo(); }
                    break;
                case "tilt": skTiltGoal = 12f * b.a; skTiltUntil = b.hold > 0f ? clock + b.hold : -1f; break;
                case "startle": skStartleAt = clock; skMood = SkMood.Scared; Sfx("OpenZ/Audio/Sfx/sd_robot_servo_c", -16f, 1.3f, "sk_startle", sfxB); break;
                case "shake": skShakeUntil = clock + (b.hold > 0f ? b.hold : 1.5f); break;
                case "recoil": skRecoilUntil = clock + (b.hold > 0f ? b.hold : 1.0f); skLeanGoal = -12f; skLeanUntil = skRecoilUntil; skSquashGoal = 0.92f; skSquashUntil = skRecoilUntil; SkServo(); break;
                case "wince": skSquashGoal = 0.95f; skSquashUntil = clock + (b.hold > 0f ? b.hold : 0.5f); skBlinkT = -0.001f; skBlinkHold = b.hold > 0f ? b.hold : 0.5f; break;
                case "bounce": skBounceAt = clock; skBounceN = Mathf.Max(1f, b.a); break;
                case "turn_go": skYawGoal = 90f; SkServo(); break;
                case "walk_off":
                    {   // off to the wings (screen left, +X) for a seconds; b = the metres he must cover in that time (out of the locked frame)
                        skWalkAt = clock; skWalkFor = Mathf.Max(0.8f, b.a); skWalkDir = Vector3.right; skWalkHasTarget = false; skYawGoal = 90f; skLook = SkLook.Away;
                        skWalkRate = b.b > 0f ? Mathf.Clamp(b.b / skWalkFor / Mathf.Max(0.2f, sk.Rig.WalkSpeed), 0.9f, 2.2f) : 1.25f;
                        break;
                    }
                case "offstage": skOnStage = false; sk.SetActive(false); break;
                case "walk_in_button":
                    {   // from the wing (SkEntry, screen left and upstage: out of the bomb and EvilZod framings) straight to his mark behind the
                        // button stand (SkStandMark), carrying the box; the take runs faster when the way is longer; he stops ON the mark and
                        // turns three-quarter to the lens (SetYaw), the box in front of him over the stand
                        skWalkTo = SkStandMark; skWalkHasTarget = true;
                        skOnStage = true; sk.SetActive(Live); sk.Root.localPosition = SkEntry; skDrawOff = Vector3.zero;
                        Vector3 way = skWalkTo - SkEntry; way.y = 0f; float dist = way.magnitude;
                        skYaw = skYawGoal = dist > 1e-3f ? Vector3.SignedAngle(Vector3.forward, way / dist, Vector3.up) : -90f; ApplyYaw(sk, skYaw); skAfterWalkYaw = SetYaw;
                        skWalkAt = clock; skWalkFor = Mathf.Max(0.8f, b.a); skWalkDir = dist > 1e-3f ? way / dist : Vector3.left;
                        skWalkRate = Mathf.Clamp(dist / skWalkFor / Mathf.Max(0.2f, sk.Rig.WalkSpeed), 0.7f, 2.4f);
                        skCarry = true; skBoxDown = false; SetSkHand(SkHand.Carry, 0f); ButtonFollow(skBoxHolder); skMood = SkMood.Worried; skLook = SkLook.Ez;
                        break;
                    }
                case "set_button": skSetWanted = true; break;                                              // runs when he stands turned on his mark: the box goes down onto the stand
                default: Debug.LogWarning("[EvilZod] unknown sidekick beat " + b.beat); break;
            }
        }
        float skBlinkHold;
        Vector3 skLookPt, skLookVel, skPoleR, skPoleL; bool skLookInit, skPoleInit; float skLeanV, skTiltV, skSquashV, skTalkSq, skTalkSqV;
        void SetSkHand(SkHand h, float hold) { skHand = h; skHandAt = clock; skHandHold = hold; skHandsInit = skHandsInit && true; }
        void SkServo() { SfxRandom(SkServos, -24f, 1.1f, 1.3f, "sk_servo", sfxB); }

        // ------------------------------------------------------------------------------------------------ frame
        void TickSidekick(float dt)
        {
            if (!sk.Ok || !sk.Go.activeInHierarchy) return;
            if (skDrawOff != Vector3.zero) { sk.Root.localPosition -= skDrawOff; skDrawOff = Vector3.zero; }   // last frame's bounce / jitter off: the root is his travel again
            var rig = sk.Rig; float H = SkHeight;
            if (skHandHold > 0f && clock - skHandAt > skHandHold && skHand != SkHand.Side) { skHand = SkHand.Side; skHandAt = clock; skHandHold = 0f; }
            if (skTiltUntil > 0f && clock > skTiltUntil) { skTiltGoal = 0f; skTiltUntil = -1f; }
            if (skLeanUntil > 0f && clock > skLeanUntil) { skLeanGoal = 0f; skLeanUntil = -1f; }
            if (skSquashUntil > 0f && clock > skSquashUntil) { skSquashGoal = 1f; skSquashUntil = -1f; }
            if (skLookAwayUntil > 0f && clock > skLookAwayUntil) { skLook = SkLook.Ez; skLookAwayUntil = -1f; }
            if (skRecoilUntil > 0f && clock > skRecoilUntil) skRecoilUntil = -1f;
            bool walking = rig.Walking;
            // the set-down is a chain step: it starts when he stands on his mark, turned to SetYaw (the box then hangs over the stand's top)
            bool turned = Mathf.Abs(Mathf.DeltaAngle(skYaw, skYawGoal)) < 12f;
            if (skSetWanted && skCarry && skWalkAt < 0f && !walking && turned) { skSetWanted = false; skSetAt = clock; SetSkHand(SkHand.SetDown, 0f); skLeanGoal = 6f; skLeanUntil = clock + SetDownSeconds + 0.2f; SkServo(); }
            if (skHand == SkHand.SetDown && skSetAt >= 0f && clock - skSetAt >= SetDownSeconds && !skBoxDown)
            {   // the box is on the stand: it stays there (the stand's holder), his hands let go
                skBoxDown = true; skCarry = false; ButtonFollow(standHolder); SetSkHand(SkHand.Side, 0f);
                Sfx("OpenZ/Audio/Sfx/sd_crate_pickup", -6f, 0.75f, "button_set_down"); Sfx("OpenZ/Audio/Sfx/sd_seat_thud_a", -10f, 0.9f, "button_set_thud", sfxB);
                if (skPresentWanted) { skPresentWanted = false; SetSkHand(SkHand.Present, skPresentHold); SkServo(); }
            }
            if (skWalkAt >= 0f && !walking && clock - skWalkAt >= (skCarry ? 0.02f : 0.3f)) { rig.StartWalk(0.3f); walking = true; skStepParity = -1; }
            if (skWalkAt >= 0f && walking && !skWalkHasTarget && clock - skWalkAt > skWalkFor + 0.3f)
            {   // a walk-off ends after its seconds: he is gone
                rig.StopWalk(0.3f); walking = false; skWalkAt = -10f;
                skOnStage = false; sk.SetActive(false); return;
            }
            if (dt > 0f)
            {
                skYaw = Mathf.SmoothDampAngle(skYaw, skYawGoal, ref skYawVel, 0.28f, 720f, dt);
                skLean = Mathf.SmoothDamp(skLean, skLeanGoal, ref skLeanV, 0.2f, 400f, dt); skTilt = Mathf.SmoothDamp(skTilt, skTiltGoal, ref skTiltV, 0.22f, 400f, dt);
                skSquash = Mathf.SmoothDamp(skSquash, skSquashGoal, ref skSquashV, 0.15f, 20f, dt);
                skMouthSmooth += (skMouth - skMouthSmooth) * (1f - Mathf.Exp(-dt / (skMouth > skMouthSmooth ? 0.05f : 0.11f)));
                skBreath += dt; skNodT += dt;
            }
            ApplyYaw(sk, skYaw);
            if (walking && dt > 0f)
            {
                float step = rig.WalkSpeed * skWalkRate * dt;
                if (skWalkHasTarget)
                {   // the walk-in ends ON the mark (a last step of at most one frame's travel), then he turns to his set-down facing
                    Vector3 to = skWalkTo - sk.Root.localPosition; to.y = 0f;
                    bool late = clock - skWalkAt > skWalkFor + 2.5f;
                    if (to.magnitude <= step + 0.02f || late)
                    {
                        sk.Root.localPosition = new Vector3(skWalkTo.x, sk.Root.localPosition.y, skWalkTo.z);
                        rig.StopWalk(0.3f); walking = false; skWalkAt = -10f; skWalkHasTarget = false;
                        skYawGoal = skAfterWalkYaw;
                        if (late) Debug.LogWarning("[EvilZod] the sidekick's walk-in ran long: placed on his mark");
                    }
                    else sk.Root.localPosition += to.normalized * step;
                }
                else sk.Root.localPosition += skWalkDir * step;
                int parity = rig.WalkPhase < 0.5f ? 0 : 1;
                if (parity != skStepParity) { skStepParity = parity; SfxRandom(LavaWalkOn ? walk.stepClips : LightSteps, -14f, 1.25f, 1.4f, "sk_step", StepSource); }
            }
            // the body: squash / bounce / startle / shake on the root
            if (dt > 0f) skTalkSq = Mathf.SmoothDamp(skTalkSq, skMouthSmooth, ref skTalkSqV, 0.1f, 20f, dt);   // the syllables' squash spread over 3-4 frames
            float sq = skSquash * (1f - 0.05f * skTalkSq), bounceY = 0f;
            if (skBounceAt >= 0f)
            {
                float u = clock - skBounceAt, per = 0.42f; int n = Mathf.FloorToInt(u / per);
                if (n < skBounceN) { float p = Mathf.Repeat(u, per) / per; bounceY = Mathf.Sin(p * Mathf.PI) * 0.09f; sq *= p < 0.15f ? 1f - 0.08f * (1f - p / 0.15f) : p > 0.85f ? 1f - 0.08f * ((p - 0.85f) / 0.15f) : 1.03f; if (p < 0.08f && skStepParity != n + 100) { skStepParity = n + 100; SfxRandom(LightSteps, -16f, 1.3f, 1.45f, "sk_hop", StepSource); } }
                else skBounceAt = -10f;
            }
            if (skStartleAt >= 0f)
            {
                float u = clock - skStartleAt;
                if (u < 0.36f) { float p = u / 0.36f; bounceY += Mathf.Sin(p * Mathf.PI) * 0.12f; sq *= p < 0.5f ? 1.08f : 0.94f; }
                else skStartleAt = -10f;
            }
            Vector3 jitter = Vector3.zero;
            if (clock < skShakeUntil) jitter = new Vector3(Mathf.Sin(clock * 113f), Mathf.Sin(clock * 97f + 1f), Mathf.Sin(clock * 131f + 2f)) * 0.006f;
            sk.Root.localScale = new Vector3(1f + (1f - sq) * 0.5f, sq, 1f + (1f - sq) * 0.5f) * SkScale;
            Vector3 basePos = sk.Root.localPosition; skDrawOff = Vector3.up * bounceY + jitter; sk.Root.localPosition = basePos + skDrawOff;
            rig.WalkRate = skWalkRate;
            bool fromTake = rig.TickWalk(dt);
            if (!fromTake) rig.ResetToRest();
            float breath = Mathf.Sin(skBreath * 1.6f);
            if (!fromTake) rig.Lean(skLean + 0.5f * breath + 2.0f * skMouthSmooth, 0f, 0f);
            rig.ChestUp(0.6f * breath);
            TickSidekickHands(dt, H, fromTake);
            TickSidekickHead(dt);
            TickEyes(dt);
            rig.FinishWalkBlend();
            sk.Root.localPosition = basePos + skDrawOff;                                                  // drawn with the bounce / jitter; taken off again at the next frame's start (never summed)
        }
        /// <summary>This frame's drawn-only offset of the root (the hop, the startle, the shake): removed before the next frame moves him.</summary>
        Vector3 skDrawOff;

        /// <summary>The box's bottom centre in his frame while he carries it (hugged at the chest: its back face 0.25 m ahead of the root,
        /// 5 cm clear of his body, the dome top at 0.93 m under his eyes at 1.00 m) and where it lands: on the stand's top, StandTop up, the
        /// same 0.47 m ahead (the stand's column 0.26 m and its plate 0.12 m clear of his front). The set-down takes SetDownSeconds.</summary>
        public static readonly Vector3 CarryBox = new Vector3(0f, 0.60f, 0.47f);
        public static float SetDownSeconds = 0.45f;

        void TickSidekickHands(float dt, float H, bool walking)
        {
            var rig = sk.Rig; float k = H / 1.45f;
            if (skBoxHolder)
            {   // the box rides his frame: at the chest, or going down onto the stand (eased), level (its front stays his front: it never turns)
                Vector3 carry = rig.Local(CarryBox.x * k, CarryBox.y * k, CarryBox.z * k);
                var carryRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(rig.Fwd, Vector3.up).normalized, Vector3.up);
                if (skHand == SkHand.SetDown && skSetAt >= 0f && standHolder)
                {   // down onto the stand's top (world: his scaled carry height is not the stand's), level, it never turns
                    float u = Mathf.Clamp01((clock - skSetAt) / SetDownSeconds); u = u * u * (3f - 2f * u);
                    skBoxHolder.position = Vector3.Lerp(carry, standHolder.position, u); skBoxHolder.rotation = Quaternion.Slerp(carryRot, standHolder.rotation, u);
                }
                else { skBoxHolder.position = carry; skBoxHolder.rotation = carryRot; }
            }
            if (walking && !skCarry && !LavaWalkOn) return;                                                           // the take swings his arms
            Vector3 wantR, wantL, poleR, poleL; float smooth = 0.2f;
            Vector3 fwd = rig.Fwd, rgt = rig.Right;
            switch (skHand)
            {
                case SkHand.Up: wantR = rig.Local(0.34f * k, 1.02f * k, 0.06f * k); wantL = rig.Local(-0.34f * k, 1.02f * k, 0.06f * k); poleR = rgt + Vector3.down * 0.3f; poleL = -rgt + Vector3.down * 0.3f; smooth = 0.14f; break;        // 72 / 80 %, clear of the head
                case SkHand.Shrug: wantR = rig.Local(0.44f * k, 0.68f * k, 0.14f * k); wantL = rig.Local(-0.44f * k, 0.68f * k, 0.14f * k); poleR = rgt * 0.4f + Vector3.down; poleL = -rgt * 0.4f + Vector3.down; break;                   // 81 / 88 %
                case SkHand.Salute: wantR = (rig.HeadFront ? rig.HeadFront.position : SkHeadWorld) + (LavaWalkOn ? rgt * 0.37f + Vector3.up * 0.14f - fwd * 0.21f : rgt * 0.12f + Vector3.up * 0.14f - fwd * 0.02f); wantL = rig.Local(-0.26f * k, 0.48f * k, 0f); poleR = rgt + Vector3.down * 0.2f; poleL = -rgt + Vector3.down; smooth = 0.12f; break;
                case SkHand.Carry:
                case SkHand.SetDown:
                    {   // the hands on the box's rear top corners, the wrists 0.11 m outside its sides so the mittens' inner faces rest ON the side
                        // faces at the top edge (the left one touching, the right one 2-3 cm in): 76 / 84 % carrying, 80 / 87 % with the box down on
                        // the stand; the right hand on the box's right side; the arms outside the box
                        Vector3 c = skBoxHolder.position, bx = skBoxHolder.right, bu = skBoxHolder.up, bf = skBoxHolder.forward;
                        Vector3 grip = bu * (BoxH * 0.80f) - bf * (BoxD * 0.5f + 0.02f);
                        wantR = c + bx * (BoxW * 0.5f + 0.11f) + grip; wantL = c - bx * (BoxW * 0.5f + 0.11f) + grip;
                        poleR = rgt * 0.8f + Vector3.down * 0.6f; poleL = -rgt * 0.8f + Vector3.down * 0.6f; smooth = 0.1f; break;
                    }
                case SkHand.Present: wantR = rig.Local(0.36f * k, 0.68f * k, 0.18f * k); wantL = rig.Local(-0.36f * k, 0.68f * k, 0.18f * k); poleR = rgt * 0.6f + Vector3.down * 0.8f; poleL = -rgt * 0.6f + Vector3.down * 0.8f; smooth = 0.14f; break;   // 75 / 82 %: both mittens open beside the box on its stand (1.8 cm clear of its sides)
                case SkHand.Cover: wantR = rig.Local(0.20f * k, 0.90f * k, 0.25f * k); wantL = rig.Local(-0.20f * k, 0.90f * k, 0.25f * k); poleR = rgt + Vector3.down * 0.4f; poleL = -rgt + Vector3.down * 0.4f; smooth = 0.1f; break;   // 81 / 89 %: the mittens up over the eyes, outside the visor
                case SkHand.Fidget:
                    {
                        float w = Mathf.Sin(clock * 5.5f) * 0.02f;
                        wantR = rig.Local(0.16f * k + w, 0.63f * k, 0.20f * k); wantL = rig.Local(-0.16f * k - w, 0.63f * k, 0.20f * k); poleR = rgt + Vector3.down; poleL = -rgt + Vector3.down; break;   // 77 / 82 %, the mittens side by side (0.17 m apart)
                    }
                default: wantR = rig.Local(0.26f * k, 0.48f * k, 0f); wantL = rig.Local(-0.26f * k, 0.48f * k, 0f); poleR = rgt + Vector3.down * 0.5f - fwd * 0.3f; poleL = -rgt + Vector3.down * 0.5f - fwd * 0.3f; break;   // hanging: 80 / 87 %
            }
            if (!skHandsInit) { skRPos = wantR; skLPos = wantL; skRVel = skLVel = Vector3.zero; skHandsInit = true; }
            if (!skPoleInit) { skPoleR = poleR; skPoleL = poleL; skPoleInit = true; }
            if (dt > 0f)
            {
                float carrySmooth = skHand == SkHand.Carry || skHand == SkHand.SetDown ? smooth : Mathf.Max(0.13f, smooth);   // on the box the mittens follow it closely
                skRPos = Vector3.SmoothDamp(skRPos, wantR, ref skRVel, carrySmooth, 10f, dt); skLPos = Vector3.SmoothDamp(skLPos, wantL, ref skLVel, carrySmooth, 10f, dt);
                float kp = 1f - Mathf.Exp(-dt / 0.15f); skPoleR = Vector3.Slerp(skPoleR, poleR, kp); skPoleL = Vector3.Slerp(skPoleL, poleL, kp);
            }
            if (LavaWalkOn && rig.HasHinges) { rig.IkArmHinge(true, skRPos, skPoleR, 1f); rig.IkArmHinge(false, skLPos, skPoleL, 1f); }   // level 7: his elbows on their hinges (gap 26); the HQ scenes keep IkArm
            else { rig.IkArm(true, skRPos, skPoleR, 1f); rig.IkArm(false, skLPos, skPoleL, 1f); }
        }

        void TickSidekickHead(float dt)
        {
            var rig = sk.Rig;
            Vector3 target;
            switch (skLook)
            {
                case SkLook.Cam: target = CamPos; break;
                case SkLook.Away: target = rig.Local(0.5f, 0.35f * SkHeight, 1.0f); break;
                case SkLook.Back: target = rig.Local(-1.2f, 0.9f * SkHeight, -0.8f); break;
                default: target = ez != null && ez.Ok ? ez.Rig.Head.position : transform.TransformPoint(EzPos + Vector3.up * 2f); break;
            }
            // the look point moves to a new target in about 0.18 s (no snap)
            if (!skLookInit) { skLookPt = target; skLookVel = Vector3.zero; skLookInit = true; }
            else if (dt > 0f) skLookPt = Vector3.SmoothDamp(skLookPt, target, ref skLookVel, 0.18f, 30f, dt);
            target = skLookPt;
            // the head turns at most HeadYawMax toward the target (the face stays to the lens); what it cannot turn, the pupils show
            Vector3 flat = Vector3.ProjectOnPlane(target - rig.Head.position, Vector3.up);
            float want = flat.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(rig.Fwd, flat.normalized, Vector3.up) : 0f;
            float gaze = Mathf.Clamp((want - Mathf.Clamp(want, -HeadYawMax, HeadYawMax)) / 60f, -1f, 1f) * GazeShare;
            skGazeX = dt > 0f ? Mathf.Lerp(skGazeX, -gaze, 1f - Mathf.Exp(-dt * 8f)) : skGazeX;        // lookX + = toward his left (-right)
            rig.LookAt(target, 1f, HeadYawMax, 45f, 0.4f);
            float nod = skNodT < 0.3f ? Mathf.Sin(skNodT / 0.3f * Mathf.PI) * 3.5f : 0f;
            rig.HeadAdd(nod, skTilt, 0f);
        }

        void TickEyes(float dt)
        {
            if (eyeL == null || eyeR == null) return;
            if (dt > 0f)
            {
                skNextBlink -= dt;
                if (skNextBlink <= 0f && skBlinkT < 0f) { skBlinkT = 0f; skNextBlink = Random.Range(2.2f, 5f); skBlinkHold = 0f; }
            }
            float blink = 0f;
            if (skBlinkT >= 0f || skBlinkT == -0.001f)
            {
                if (skBlinkT == -0.001f) skBlinkT = 0f;
                skBlinkT += dt; float close = 0.07f, hold = skBlinkHold, u = skBlinkT;
                blink = u < close ? u / close : u < close + hold ? 1f : Mathf.Max(0f, 1f - (u - close - hold) / 0.08f);
                if (u >= close + hold + 0.08f) { skBlinkT = -1f; skBlinkHold = 0f; }
            }
            ApplyEye(eyeL, dt, blink); ApplyEye(eyeR, dt, blink);
            if (skEyeMat) skEyeMat.SetVector("_EmissionColor", (Vector4)(new Color(2.2f, 1.1f, 0.35f) * (0.85f + 0.5f * skMouthSmooth + (skMood == SkMood.Scared ? 0.3f : 0f))));
        }

        void ApplyEye(Eye e, float dt, float blink)
        {
            var want = Preset(skMood, e.left);
            if (dt > 0f) e.cur = EyeShape.Lerp(e.cur, want, 1f - Mathf.Exp(-dt * 10f));
            var s = e.cur;
            float top = Mathf.Max(s.top, blink), bot = Mathf.Max(s.bottom, blink);
            float squash = s.squash * (1f - 0.22f * skMouthSmooth);
            Vector3 sc = e.rootScale0 * s.scale; sc[e.upAxis] *= squash; e.root.localScale = sc;
            if (e.lidTop)
            {
                Vector3 ls = e.topScale0; ls[e.topAxis] = Mathf.Max(0.001f, top * e.topUnit); e.lidTop.localScale = ls;
                e.lidTop.localRotation = e.topRot0;
                e.lidTop.rotation = Quaternion.AngleAxis(e.tiltSign * s.tilt, e.root.TransformDirection(e.viewL)) * e.lidTop.rotation;
            }
            if (e.lidBot) { Vector3 ls = e.botScale0; ls[e.botAxis] = Mathf.Max(0.001f, bot * e.botUnit); e.lidBot.localScale = ls; }
            if (e.pupil)
            {
                Vector3 ps = e.pupilScale0 * s.pupil; e.pupil.localScale = ps;
                float lookX = Mathf.Clamp(s.lookX + skGazeX, -1f, 1f);
                e.pupil.localPosition = e.pupilPos0 + (-e.rightL * lookX + e.upL * s.lookY) * (0.3f * e.radius / Mathf.Max(1e-4f, e.root.lossyScale.x));
            }
        }

        void SidekickTalk(float m)
        {
            skMouth = m;
            if (m - skLastMouth > 0.3f && clock > skNextOnset) { skNodT = 0f; skNextOnset = clock + 0.3f; }
            skLastMouth = m;
        }
        public float SkMouth => skMouthSmooth;
    }
}
