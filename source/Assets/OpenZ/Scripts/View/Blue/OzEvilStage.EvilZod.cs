// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: General EvilZod on the stage (OzEvilStage).
// Confident and strong like the commander: fists on the hips, the raised fist and its pump on "TAKE OVER THE WORLD", the arm thrown
// out at the board on "THAT RED FORT", the fist at the lens on "PAINT IT BLUE", the shooing sweep on "NOW MOVE", the arms spread on
// "WORLD DOMINATION", the lean down at the sidekick, the shrug, the two fists and the lean into the scream, the presentation of the
// World-Domination-inator, the bow over the button stand and the fist slammed onto the dome.
//  - a little BIGGER (EzScale), more menacing; he never walks (the LOSE walk to the rack, the grab and the walk-off are gone: on
//    "Fine, I'll do it myself!" the cut finds him with the shotgun already in his hands; one small pump on the line, its sound);
//  - NO SNAPS: every switch eases (the look point, the elbow poles, the palm turns and their weights, the finger poses cross-fade, the
//    hands SmoothDamp with at least 0.14 s), the socket-offset contact targets move slowly (no feedback loop), the talk nods are small
//    and spaced. Before (proof/redo/base_*_motion.csv): the head's forward tip jumped 250-380 m/s^2 on every look change (an instant
//    retarget), the elbows flipped on every hand mode change (an instant pole), the fingers popped between fist and open.
// Every pose is written from the rest pose each frame (OzPoseRig: world-space two-bone arms, head look, spine, the crouch), with the
// model's own finger takes; his jaw plate slides on the live level of his words and his red eyes glow with them.
// Hand targets are metres in his own frame (x = his right, y = up from his feet, z = forward), measured on the rig at height 2.30 and
// scaled by k = his height / 2.30 (so the same numbers hold at EzScale).
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>One of the two robots: the instance, its rig, its materials.</summary>
        sealed class Robot
        {
            public GameObject Go; public Transform Root; public OzPoseRig Rig; public bool Ok; public float YawOffset; public Material BodyMat; public string Name;
            public void SetActive(bool on) { if (Go && Go.activeSelf != on) Go.SetActive(on); }
            public string Status() => Ok ? $"{Name} at {Root.localPosition.x:F2}/{Root.localPosition.z:F2} yaw {Root.localEulerAngles.y:F0} scale {Root.localScale.y:F2}{(Rig.Walking ? " walking" : "")}" : Name + " MISSING";
            public void Dispose() { Rig?.Dispose(); }
        }

        enum HandMode { Side, Hip, FistUp, FistCam, PointBoard, PointSk, Shoo, Wide, Present, Shrug, FistUpBoth, WindUp, Press, Gun, Binoc, BinocLow, BinocSide, PatConsole, PointOut }
        enum LookMode { Cam, Sk, Board, Doors, Button, Away, Gun, Out }

        /// <summary></summary>
        public static float EzScale = 1.08f, SkScale = 0.88f;

        Robot ez; bool ezOnStage = true;
        HandMode ezR = HandMode.Hip, ezL = HandMode.Hip; float ezRAt, ezLAt; Vector3 ezRPos, ezLPos, ezRVel, ezLVel; bool ezHandsInit;
        Vector3 ezPoleR, ezPoleL;
        string ezFingR = "Fist", ezFingL = "Fist", ezFingRPrev = "Fist", ezFingLPrev = "Fist"; float ezFingRK = 1f, ezFingLK = 1f;
        LookMode ezLook = LookMode.Cam; Vector3 ezLookPt, ezLookVel; bool ezLookInit;
        float ezYaw, ezYawGoal, ezYawVel, ezLean, ezLeanGoal, ezTwist, ezTwistGoal, ezChest, ezChestGoal, ezShoulder, ezShoulderGoal;
        float ezCrouch, ezCrouchGoal, ezBack, ezBackGoal, ezBow, ezBowGoal;
        float ezLeanV, ezTwistV, ezChestV, ezShoulderV, ezCrouchV, ezBackV, ezBowV;        // SmoothDamp velocities (a beat starts a move from rest, no velocity jump)
        Vector3 ezOffR, ezOffL; bool ezOffInit;                                            // the hands' socket offsets (slowly followed)
        Vector3 ezHipsRestPos; Quaternion ezHipsRestRot = Quaternion.identity;            // the hips bone in the root's frame at rest (the trunk's anchor)
        float ezNodT = 9f, ezNodAmp = 6f, ezShakeAt = -10f, ezAroundAt = -10f, ezPumpAt = -10f, ezJabAt = -10f, ezPressAt = -10f;
        float ezMouth, ezMouthSmooth, ezLastMouth, ezNextOnset, ezBreath, ezShoulderOut = 0.346f;
        Vector3 ezPressFrom; bool ezPressHit;
        Transform ezJaw, ezJawOpen; Vector3 ezJawRestPos, ezJawDrop;
        static readonly string[] Servos = { "OpenZ/Audio/Sfx/mech-servo-1", "OpenZ/Audio/Sfx/mech-servo-2", "OpenZ/Audio/Sfx/sd_robot_servo_a", "OpenZ/Audio/Sfx/sd_robot_servo_b" };

        public Vector3 EzPos => ez != null && ez.Ok ? ez.Root.localPosition : new Vector3(-0.55f, 0f, 0f);
        /// <summary>His standing height to the hat top: the rig of the hand numbers (2.30 m) at his stage scale. (The renderer bounds the rig
        /// measures include the shotgun, 2.72 m: k75b scaled every hand target by that, 18 % too far: straight, over-reaching arms.)</summary>
        public float EzHeight => ez != null && ez.Ok ? 2.3f * ez.Rig.Scale : 2.3f * EzScale;
        float EzH => EzHeight;
        /// <summary>The bow over the button stand for the press: the hips down and back, the trunk bowed (deg).</summary>
        public static float CrouchDrop = 0.40f, CrouchBack = 0.05f, CrouchBow = 48f;
        /// <summary>The most his face turns away from the shot's camera (deg): in his own shots / in the wide and two-shots.</summary>
        public static float FaceCamCap = 12f, FaceCamCapWide = 28f;

        // ------------------------------------------------------------------------------------------------ build
        Robot LoadRobot(string name, string bodyRenderer, string bodyMat, float scale)
        {
            var r = new Robot { Name = name };
            var src = Resources.Load<GameObject>(Folder + name);
            if (!src) { Debug.LogWarning("[EvilZod] Resources/" + Folder + name + " missing: " + name + " is not on the stage"); return r; }
            r.Go = Instantiate(src, transform); r.Go.name = name; r.Root = r.Go.transform;
            r.Root.localPosition = Vector3.zero; r.Root.localRotation = Quaternion.identity; r.Root.localScale = Vector3.one;
            foreach (var c in r.Go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var t in r.Go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var rd in r.Go.GetComponentsInChildren<Renderer>(true))
            {
                rd.renderingLayerMask = LightMask; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; rd.receiveShadows = true;
                rd.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; rd.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
                if (rd is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            r.BodyMat = AssignMaterial(r.Go, bodyRenderer, bodyMat);
            r.Rig = new OzPoseRig(r.Root);
            r.Ok = r.Rig.Ok;
            if (!r.Ok) { Debug.LogWarning("[EvilZod] " + name + ": " + r.Rig.Problem); return r; }
            r.Rig.SetScale(scale);
            // the model's forward measured at rest: the yaw that makes it face the stage's +Z
            r.YawOffset = -Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(transform.InverseTransformDirection(r.Rig.Fwd), Vector3.up), Vector3.up);
            return r;
        }

        /// <summary>The kit's material asset on the renderer of that name (an instance, so the stage can drive its emission).</summary>
        Material AssignMaterial(GameObject go, string rendererName, string matName)
        {
            var mat = Resources.Load<Material>(Folder + matName);
            Material inst = null;
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
            {
                if (rd.name != rendererName) continue;
                if (!mat) { Debug.LogWarning("[EvilZod] material " + matName + " missing in Resources/" + Folder); return rd.material; }
                var mats = rd.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = mat; rd.sharedMaterials = mats;
                inst = rd.material; madeMats.Add(inst);
                var all = rd.materials; for (int i = 1; i < all.Length; i++) all[i] = inst; rd.materials = all;
                return inst;
            }
            return inst;
        }
        /// <summary>The shotgun's two slots: the bigger submesh is the gun, the small one the dark rail under the pump.</summary>
        void AssignGunMaterials(GameObject go)
        {
            var gunMat = Resources.Load<Material>(Folder + "M_Shotgun"); var railMat = Resources.Load<Material>(Folder + "M_Shotgun_Rail");
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
            {
                if (rd.name != "Shotgun" && rd.name != "Shotgun_Pump") continue;
                var mf = rd.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : (rd as SkinnedMeshRenderer)?.sharedMesh;
                var mats = rd.sharedMaterials; int small = -1; int smallest = int.MaxValue;
                if (mesh && mats.Length > 1) for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++) { int n = mesh.GetSubMesh(i).indexCount; if (n < smallest) { smallest = n; small = i; } }
                for (int i = 0; i < mats.Length; i++) mats[i] = i == small && railMat ? railMat : gunMat ? gunMat : mats[i];
                rd.sharedMaterials = mats;
            }
        }

        void BuildEvilZod()
        {
            ez = LoadRobot("EvilZod", "EvilZod_Body", "M_EvilZod_Body", EzScale);
            if (!ez.Ok) return;
            AssignGunMaterials(ez.Go);
            ez.Rig.LoadFingerPoses(Folder, "EvilZod", "Fist", "GripShotgun", "GripPump", "HandsOpen");
            ez.Rig.MeasureHinges(Folder, "EvilZod", "Walk", "GripShotgun", "GripPump", "Fist", "HandsOpen");        // redo 2: the elbows bend about their own hinges
            Debug.Log("[EvilZod] " + ez.Rig.HingeReport);
            ezJaw = ez.Rig.Find("Jaw"); ezJawOpen = ez.Rig.Find("JawOpen");
            if (ezJaw) { ezJawRestPos = ezJaw.localPosition; ezJawDrop = ezJawOpen && ezJawOpen.parent == ezJaw.parent ? ezJawOpen.localPosition - ezJawRestPos : new Vector3(0f, -0.022f, 0.003f); }
            else Debug.LogWarning("[EvilZod] no Jaw bone: his mouth plate stays still");
            if (!ez.Rig.HasLegs) Debug.LogWarning("[EvilZod] leg bones missing: he cannot bow for the press (the fist still goes to the dome)");
            // his right shoulder's distance from his centre line (the button stand's dome sits in line with it)
            ezShoulderOut = Mathf.Abs(Vector3.Dot(ez.Rig.ArmR.position - ez.Root.position, ez.Rig.Right));
            if (ezShoulderOut < 0.2f || ezShoulderOut > 0.6f) ezShoulderOut = 0.346f * EzScale;
            CaptureGun(ez.Rig);
            ez.SetActive(false);
        }

        void ResetEvilZod(Vector3 at)
        {
            if (ez == null || !ez.Ok) return;
            ez.Rig.ResetToRest();
            ez.Root.localPosition = at; ezYaw = ezYawGoal = 0f; ezYawVel = 0f; ApplyYaw(ez, ezYaw);
            ezR = ezL = HandMode.Hip; ezRAt = ezLAt = clock; ezHandsInit = false; ezLook = LookMode.Cam; ezLookInit = false;
            ezFingR = ezFingL = ezFingRPrev = ezFingLPrev = "Fist"; ezFingRK = ezFingLK = 1f;
            ezLean = ezLeanGoal = ezTwist = ezTwistGoal = ezChest = ezChestGoal = ezShoulder = ezShoulderGoal = 0f;
            ezCrouch = ezCrouchGoal = ezBack = ezBackGoal = ezBow = ezBowGoal = 0f;
            ezLeanV = ezTwistV = ezChestV = ezShoulderV = ezCrouchV = ezBackV = ezBowV = 0f;
            ezNodT = 9f; ezShakeAt = ezAroundAt = ezPumpAt = ezJabAt = ezPressAt = -10f; ezPressHit = false; ezOffInit = false;
            ezMouth = ezMouthSmooth = ezLastMouth = 0f; ezOnStage = true; ez.SetActive(Live); pumpAt = -10f; pumpSoundOn = false; pressReleaseAt = -1f; releaseClicked = false;
            if (gunPump) gunPump.localPosition = pumpRest;
        }

        void ApplyYaw(Robot r, float yaw) { r.Root.localRotation = Quaternion.Euler(0f, r.YawOffset + yaw, 0f); }

        // ------------------------------------------------------------------------------------------------ beats
        void EvilZodBeat(OzBlueBeat b)
        {
            if (ez == null || !ez.Ok) return;
            switch (b.beat)
            {
                case "stance_hips": SetHands(HandMode.Hip, HandMode.Hip); ezChestGoal = 0f; ezShoulderGoal = 0f; break;
                case "fist_up": SetHand(true, HandMode.FistUp); ezChestGoal = Mathf.Max(ezChestGoal, 6f); Servo(); break;
                case "fist_pump": ezPumpAt = clock; Sfx("OpenZ/Audio/Sfx/sd_body_whoosh_a", -14f, 1.1f, "fist_pump_whoosh", sfxB); break;
                case "fist_cam": SetHand(true, HandMode.FistCam); ezLeanGoal = 10f; Servo(); break;
                case "point_board": SetHand(true, HandMode.PointBoard); ezTwistGoal = 30f; Servo(); break;
                case "point_sk": SetHand(true, HandMode.PointSk); Servo(); break;
                case "jab": ezJabAt = clock; break;
                case "shoo": SetHands(HandMode.Shoo, HandMode.Hip); ezLeanGoal = 6f; ezTwistGoal = 16f; Servo(); Sfx("OpenZ/Audio/Sfx/sd_body_whoosh_b", -16f, 1.0f, "shoo_whoosh", sfxB); break;
                case "arms_wide": SetHands(HandMode.Wide, HandMode.Wide); ezChestGoal = 10f; Servo(); break;
                case "present": SetHands(HandMode.Present, HandMode.Present); ezTwistGoal = 20f; Servo(); break;
                case "shrug": SetHands(HandMode.Shrug, HandMode.Shrug); ezShoulderGoal = 9f; ezChestGoal = -4f; Servo(); break;
                case "fists_up": SetHands(HandMode.FistUpBoth, HandMode.FistUpBoth); ezChestGoal = 8f; Servo(); break;
                case "chest_up": ezChestGoal = 10f * b.a; break;
                case "lean_down": ezLeanGoal = 13f * b.a; break;
                case "shoulders": ezShoulderGoal = 9f * b.a; break;                                         // a shrug of the shoulders alone (no hands)
                case "nod": ezNodT = 0f; ezNodAmp = 6f; break;
                case "head_shake": ezShakeAt = clock; break;
                case "look_around": ezAroundAt = clock; break;
                case "look_sk": ezLook = LavaWalkOn ? LookMode.Cam : LookMode.Sk; break;                    // level 7: BS-01 walks behind him, the head would stop at 70 deg on nothing
                case "look_cam": ezLook = LookMode.Cam; break;
                case "look_board": ezLook = LookMode.Board; break;
                case "look_doors": ezLook = LookMode.Doors; break;
                case "look_button": ezLook = LookMode.Button; break;
                case "look_away": ezLook = LookMode.Away; break;
                case "face_sk": { Vector3 to = SkPos - EzPos; to.y = 0f; if (to.sqrMagnitude > 1e-4f) ezYawGoal = Vector3.SignedAngle(Vector3.forward, to.normalized, Vector3.up); ezTwistGoal = 0f; Servo(); break; }
                case "face_stand": ezYawGoal = PressYaw; ezTwistGoal = 0f; Servo(); break;                 // the nuke scene: toward the button stand, the dome in line with his right shoulder
                case "face_cam": ezYawGoal = b.a; ezTwistGoal = 0f; break;
                case "crouch": ezCrouchGoal = b.a > 0f ? b.a : CrouchDrop; ezBowGoal = b.b > 0f ? b.b : CrouchBow; ezBackGoal = CrouchBack; ezLeanGoal = 0f; Servo(); Sfx("OpenZ/Audio/Sfx/sd_crane_arm_hydraulic", -20f, 1.1f, "ez_crouch", sfxB); break;
                case "stand": ezCrouchGoal = 0f; ezBowGoal = 0f; ezBackGoal = 0f; break;
                case "wind_up": SetHand(true, HandMode.WindUp); ezChestGoal = Mathf.Max(ezChestGoal, 5f); Servo(); break;
                case "press": ezPressAt = clock; ezPressHit = false; ezPressFrom = ezRPos; SetHand(true, HandMode.Press); Sfx("OpenZ/Audio/Sfx/v3-ramp-whoosh", -10f, 1.55f, "fist_swing", sfxB); break;
                case "gun_ready":
                    {
                        GunReady(ez.Rig); SetHands(HandMode.Gun, HandMode.Gun); ezHandsInit = false; ezLook = LookMode.Cam; ezLookInit = false;
                        ezLeanGoal = ezLean = 4f; ezTwistGoal = ezTwist = 10f; ezChestGoal = ezChest = 6f; ezLeanV = ezTwistV = ezChestV = 0f;
                        break;
                    }
                case "pump_gun": PumpStart(); ezLook = LookMode.Gun; break;                              // one small pump (its sound on the slide's two stops)
                default: if (!L18EvilZodBeat(b)) Debug.LogWarning("[EvilZod] unknown EvilZod beat " + b.beat); break;
            }
        }
        void SetHand(bool right, HandMode m) { if (right) { ezR = m; ezRAt = clock; } else { ezL = m; ezLAt = clock; } }
        void SetHands(HandMode r, HandMode l) { SetHand(true, r); SetHand(false, l); }
        void Servo() { SfxRandom(Servos, -20f, 0.9f, 1.1f, "ez_servo", sfxB); }

        // ------------------------------------------------------------------------------------------------ frame
        void TickEvilZod(float dt)
        {
            if (!ez.Ok || !ez.Go.activeInHierarchy) return;
            var rig = ez.Rig; float H = EzH;
            if (dt > 0f)
            {
                ezYaw = Mathf.SmoothDampAngle(ezYaw, ezYawGoal, ref ezYawVel, 0.3f, 720f, dt);
                // the body's moves ease in AND out (critically damped): an exponential follow starts at full speed on the beat's frame (the
                // r1 log: 70-125 m/s^2 at the head on the crouch / stand of the press)
                ezLean = Mathf.SmoothDamp(ezLean, ezLeanGoal, ref ezLeanV, 0.24f, 400f, dt); ezTwist = Mathf.SmoothDamp(ezTwist, ezTwistGoal, ref ezTwistV, 0.26f, 400f, dt);
                ezChest = Mathf.SmoothDamp(ezChest, ezChestGoal, ref ezChestV, 0.26f, 400f, dt); ezShoulder = Mathf.SmoothDamp(ezShoulder, ezShoulderGoal, ref ezShoulderV, 0.22f, 400f, dt);
                ezCrouch = Mathf.SmoothDamp(ezCrouch, ezCrouchGoal, ref ezCrouchV, 0.2f, 20f, dt); ezBack = Mathf.SmoothDamp(ezBack, ezBackGoal, ref ezBackV, 0.2f, 20f, dt);
                ezBow = Mathf.SmoothDamp(ezBow, ezBowGoal, ref ezBowV, 0.2f, 600f, dt);
                ezMouthSmooth += (ezMouth - ezMouthSmooth) * (1f - Mathf.Exp(-dt / (ezMouth > ezMouthSmooth ? 0.03f : 0.06f)));
                ezBreath += dt;
            }
            ApplyYaw(ez, ezYaw);
            if (!TickLavaWalkTake(rig, dt)) rig.ResetToRest();                                           // level 7: he walks; everywhere else the rest pose
            float breath = Mathf.Sin(ezBreath * 1.15f);
            rig.Crouch(ezCrouch, ezBack, ezBow);
            rig.Lean(ezLean + 0.6f * breath + GunLean(), 0f, ezTwist);
            rig.ChestUp(ezChest + 0.8f * breath);
            rig.Shoulders(ezShoulder + 0.5f * breath);
            TickEvilZodHands(dt, H);
            TickEvilZodHead(dt);
            TickJawAndEyes(dt);
            TickGunSeat();
            TickPump();
        }

        void EnsureOffsets(OzPoseRig rig)
        {
            if (ezOffInit) return;
            // the rest pose's socket offsets to begin with; the hips bone's rest place in the root's frame (the trunk's anchor for Body())
            ezOffR = rig.HandPoint(true) - rig.HandR.position; ezOffL = rig.HandPoint(false) - rig.HandL.position;
            if (rig.Hips) { ezHipsRestPos = rig.Root.InverseTransformPoint(rig.Hips.position); ezHipsRestRot = Quaternion.Inverse(rig.Root.rotation) * rig.Hips.rotation; }
            ezOffInit = true;
        }

        void TickEvilZodHands(float dt, float H)
        {
            var rig = ez.Rig;
            Vector3 shR = rig.ArmR.position;
            float reach = Vector3.Distance(rig.ArmR.position, rig.ForeArmR.position) + Vector3.Distance(rig.ForeArmR.position, rig.HandR.position);
            EnsureOffsets(rig);
            Vector3 wantR = HandGoal(true, ezR, H, reach, out Vector3 poleR, out string fingR, out _, out float smoothR);
            Vector3 wantL = HandGoal(false, ezL, H, reach, out Vector3 poleL, out string fingL, out _, out float smoothL);
            bool snap = !ezHandsInit;
            if (snap) { ezRPos = wantR; ezLPos = wantL; ezRVel = ezLVel = Vector3.zero; ezPoleR = poleR; ezPoleL = poleL; ezFingR = ezFingRPrev = fingR; ezFingL = ezFingLPrev = fingL; ezFingRK = ezFingLK = 1f; ezHandsInit = true; }
            if (dt > 0f)
            {
                ezRPos = Vector3.SmoothDamp(ezRPos, wantR, ref ezRVel, Mathf.Max(0.14f, smoothR), 9f, dt);
                ezLPos = Vector3.SmoothDamp(ezLPos, wantL, ref ezLVel, Mathf.Max(0.14f, smoothL), 9f, dt);
                // no instant elbow / finger changes: the poles turn, the finger poses cross-fade (redo 2: no palm turns at all: the hinge IK
                // keeps the wrist straight, the turn of the hand comes from the arm's own swivel)
                float kp = 1f - Mathf.Exp(-dt / 0.16f);
                ezPoleR = Vector3.Slerp(ezPoleR, poleR, kp); ezPoleL = Vector3.Slerp(ezPoleL, poleL, kp);
                if (fingR != ezFingR) { ezFingRPrev = ezFingRK >= 0.5f ? ezFingR : ezFingRPrev; ezFingR = fingR; ezFingRK = 0f; }
                if (fingL != ezFingL) { ezFingLPrev = ezFingLK >= 0.5f ? ezFingL : ezFingLPrev; ezFingL = fingL; ezFingLK = 0f; }
                ezFingRK = Mathf.MoveTowards(ezFingRK, 1f, dt / 0.16f); ezFingLK = Mathf.MoveTowards(ezFingLK, 1f, dt / 0.16f);
            }
            // the press: a direct, accelerating run from the wind-up to the dome, the fist's underside on the cap at PressHit s (the beat
            // leads the word by that much); the wrist aims where the fist (the hand socket) lands on the cap
            if (ezR == HandMode.Press && ezPressAt >= 0f)
            {
                float u = Mathf.Clamp01((clock - ezPressAt) / PressHit), e = u * u * u;
                Vector3 capWrist = ButtonTop + Vector3.up * FistOver - ezOffR;
                ezRPos = Vector3.Lerp(ezPressFrom, capWrist, e); ezRVel = Vector3.zero;
                if (u >= 1f && !ezPressHit) { ezPressHit = true; ButtonPress(); }
                if (clock - ezPressAt > PressHit + 0.5f) SetHand(true, HandMode.Hip);
            }
            L18HandsOverride();                                                                         // level 18: the palm's timed way over the console (OzEvilStage.L18Window.cs)
            Vector3 jab = Vector3.zero;
            if (ezJabAt >= 0f) { float u = clock - ezJabAt; float p = u < 0.11f ? Mathf.SmoothStep(0f, 1f, u / 0.11f) : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.11f) / 0.3f); jab = (ezRPos - shR).normalized * (0.08f * H * p); if (u > 0.5f) ezJabAt = -10f; }   // eased out and back (no one-frame snap)
            Vector3 pump = Vector3.zero;
            if (ezPumpAt >= 0f) { float u = clock - ezPumpAt; float p = u < 0.14f ? Mathf.SmoothStep(0f, 1f, u / 0.14f) : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.14f) / 0.4f); pump = (Vector3.down * 0.13f + rig.Fwd * 0.05f) * (H * p); if (u > 0.6f) ezPumpAt = -10f; }   // the fist pump: down and back up, eased
            Vector3 gunKick = ezR == HandMode.Gun ? GunKickOffset() : Vector3.zero;
            rig.IkArmHinge(true, ezRPos + jab + pump + gunKick, ezPoleR, 1f);
            if (ezR == HandMode.Gun) GunHandRotation(rig);                                              // the gun across the body: the hand turned so the barrel points where it should
            Vector3 wantLeft = ezLPos + (ezL == HandMode.FistUpBoth ? pump : Vector3.zero);
            if (ezL == HandMode.Gun) wantLeft = PumpGrip - ezOffL * 0.6f;                                  // the left hand rides the fore-end (it follows the slide)
            rig.IkArmHinge(false, wantLeft, ezPoleL, 1f);
            if (ezFingRK < 1f) rig.Fingers(true, ezFingRPrev, 1f); rig.Fingers(true, ezFingR, ezFingRK);
            if (ezFingLK < 1f) rig.Fingers(false, ezFingLPrev, 1f); rig.Fingers(false, ezFingL, ezFingLK);
            if (snap)
            {   // a scene start / a cut put the hands in a new mode: the socket offsets of the new pose are settled on this frame (three solves),
                // so the hands do not slide in over the first 0.35 s after the cut (r3_lose_gun: the gun swung 0.3 s after the cut on "Fine")
                for (int i = 0; i < 3; i++)
                {
                    ezOffR = rig.HandPoint(true) - rig.HandR.position; ezOffL = rig.HandPoint(false) - rig.HandL.position;
                    ezRPos = HandGoal(true, ezR, H, reach, out _, out _, out _, out _); ezLPos = HandGoal(false, ezL, H, reach, out _, out _, out _, out _);
                    rig.IkArmHinge(true, ezRPos + gunKick, ezPoleR, 1f);
                    if (ezR == HandMode.Gun) GunHandRotation(rig);
                    rig.IkArmHinge(false, ezL == HandMode.Gun ? PumpGrip - ezOffL * 0.6f : ezLPos, ezPoleL, 1f);
                    rig.Fingers(true, ezFingR, 1f); rig.Fingers(false, ezFingL, 1f);
                }
            }
            // the hands' socket offsets after this frame's pose, followed slowly (the contact targets of the next frames put the fist /
            // palm on the object, not the wrist, without a frame-to-frame feedback loop)
            if (dt > 0f) { float ko = 1f - Mathf.Exp(-dt / 0.35f); ezOffR = Vector3.Lerp(ezOffR, rig.HandPoint(true) - rig.HandR.position, ko); ezOffL = Vector3.Lerp(ezOffL, rig.HandPoint(false) - rig.HandL.position, ko); }
        }

        /// <summary>Seconds from the press beat to the fist on the cap (the beat sits that much before the stressed word).</summary>
        public const float PressHit = 0.36f;

        /// <summary>A point given in his standing frame (Local), carried along with the trunk: the same point while he stands; in the crouch it
        /// moves and turns with the hips bone (the fists stay on the hips, the chest point rises again as he stands up).</summary>
        Vector3 Trunk(Vector3 standing)
        {
            var rig = ez.Rig; if (!rig.Hips || !ezOffInit) return standing;
            Vector3 restPos = rig.Root.TransformPoint(ezHipsRestPos); Quaternion restRot = rig.Root.rotation * ezHipsRestRot;
            return rig.Hips.position + (rig.Hips.rotation * Quaternion.Inverse(restRot)) * (standing - restPos);
        }

        /// <summary>The world target of a hand (its wrist) for a mode, its elbow pole, finger take, palm direction (zero = free) and ease time.
        /// Metres in his frame for this rig at 2.30 m, scaled by his height / 2.30.</summary>
        Vector3 HandGoal(bool right, HandMode m, float H, float reach, out Vector3 pole, out string fingers, out Vector3 palm, out float smooth)
        {
            var rig = ez.Rig; float s = right ? 1f : -1f, k = H / 2.3f;              // s: his right side is +x of Local()
            Vector3 Body(float x, float y, float z) => Trunk(rig.Local(x, y, z));     // a point of his standing frame, carried with the trunk in the crouch
            Vector3 fwd = rig.Fwd, rgt = rig.Right, up = Vector3.up;
            pole = rgt * s * 0.8f + Vector3.down * 0.5f - fwd * 0.3f; fingers = "HandsOpen"; palm = Vector3.zero; smooth = 0.22f;
            Vector3 hip = Body(s * 0.39f * k, 1.22f * k, 0.16f * k) - (right ? ezOffR : ezOffL);                     // fists ON the hips
            if (L18HandGoal(right, m, H, reach, ref pole, ref fingers, ref palm, ref smooth, out Vector3 l18)) return l18;   // level 18: the binoculars, the console, the window
            switch (m)
            {
                case HandMode.Side: palm = -rgt * s; return Body(s * 0.40f * k, 1.33f * k, 0.06f * k);
                case HandMode.Hip: fingers = "Fist"; pole = rgt * s + up * 0.1f - fwd * 0.3f; palm = -rgt * s; return hip;             // the elbow out and a little back
                case HandMode.FistUp: fingers = "Fist"; pole = rgt * s * 0.9f + up * 0.3f; palm = fwd * 0.3f - rgt * s; return Body(s * 0.48f * k, 2.23f * k, 0.23f * k);
                case HandMode.FistUpBoth: fingers = "Fist"; pole = rgt * s * 0.9f + up * 0.2f; palm = fwd * 0.3f - rgt * s; return Body(s * 0.58f * k, 2.10f * k, 0.30f * k);
                case HandMode.FistCam: fingers = "Fist"; pole = rgt * s * 0.7f + Vector3.down * 0.6f; smooth = 0.16f; palm = Vector3.down - rgt * s * 0.3f; return Body(s * 0.20f * k, 1.70f * k, 0.42f * k);
                case HandMode.WindUp: fingers = "Fist"; pole = rgt * s * 0.8f + up * 0.4f; smooth = 0.18f; return Body(s * 0.45f * k, 2.30f * k, 0.25f * k);
                case HandMode.PointBoard:
                    {   // the board on the back wall, screen left: the arm out toward it, the body twisted
                        Vector3 board = transform.TransformPoint(BoardPoint); Vector3 sh = (right ? rig.ArmR : rig.ArmL).position;
                        pole = up * 0.6f + rgt * s * 0.5f; palm = Vector3.down; return sh + (board - sh).normalized * (reach * 0.88f);
                    }
                case HandMode.PointSk:
                    {
                        Vector3 sh = (right ? rig.ArmR : rig.ArmL).position; Vector3 skHead = SkHeadWorld;
                        pole = up * 0.3f + rgt * s * 0.7f; smooth = 0.16f; palm = Vector3.down; return sh + (skHead - sh).normalized * (reach * 0.86f);
                    }
                case HandMode.Shoo:
                    {   // the right hand flicks from the chest out toward the sidekick (his right, +x: the sidekick stands screen left of him) and
                        float u = Mathf.Clamp01((clock - ezRAt) / 0.5f); u = u * u * (3f - 2f * u);
                        Vector3 a = Body(0.26f * k, 1.62f * k, 0.36f * k), b = Body(0.66f * k, 1.45f * k, 0.30f * k);
                        pole = rgt * 0.6f + Vector3.down * 0.7f; palm = rgt + Vector3.down * 0.3f; smooth = 0.14f;
                        if (clock - ezRAt > 1.4f) { fingers = "Fist"; pole = rgt + up * 0.1f - fwd * 0.3f; palm = -rgt; return hip; }
                        return Vector3.Lerp(a, b, u);
                    }
                case HandMode.Wide: pole = rgt * s * 0.6f + Vector3.down * 0.7f; palm = up + fwd * 0.3f; return Body(s * 0.85f * k, 1.75f * k, 0.10f * k);
                case HandMode.Present:
                    {   // toward the alcove (behind him, screen left): both palms up toward the bomb
                        Vector3 bombP = bombCradle ? bombCradle.TransformPoint(new Vector3(0f, 1.3f, 0f)) : transform.TransformPoint(new Vector3(0f, 1.3f, -4.6f));
                        Vector3 sh = (right ? rig.ArmR : rig.ArmL).position; Vector3 to = (bombP - sh).normalized;
                        pole = Vector3.down * 0.8f + rgt * s * 0.4f; palm = up; return sh + to * (reach * 0.72f) + rgt * s * 0.12f * H + up * 0.02f * H;
                    }
                case HandMode.Shrug: pole = rgt * s * 0.4f + Vector3.down * 0.9f; palm = up; return Body(s * 0.55f * k, 1.50f * k, 0.25f * k);
                case HandMode.Press:
                    {   // the run itself is written in TickEvilZodHands; this is where it ends (the fist's underside on the cap)
                        fingers = "Fist"; pole = rgt * s * 0.6f + up * 0.4f; smooth = 0.06f;
                        return ButtonTop + Vector3.up * FistOver - ezOffR;
                    }
                case HandMode.Gun:
                    {   // the shotgun held across the body (port arms): the right fist on the grip at his right waist, the barrel up across
                        // the chest to his left; the left hand on the fore-end (TickEvilZodHands puts it on the pump grip)
                        if (right) { fingers = "GripShotgun"; pole = rgt * 0.7f + Vector3.down * 0.6f - fwd * 0.2f; smooth = 0.14f; return Body(GunGripAt.x * k, GunGripAt.y * k, GunGripAt.z * k) - ezOffR; }
                        fingers = "GripPump"; pole = -rgt * 0.5f + Vector3.down * 0.9f; smooth = 0.12f;
                        return PumpGrip;
                    }
            }
            return Body(s * 0.40f * k, 1.33f * k, 0.06f * k);
        }

        void TickEvilZodHead(float dt)
        {
            var rig = ez.Rig;
            Vector3 target;
            switch (ezLook)
            {
                case LookMode.Sk: target = SkHeadWorld + Vector3.up * 0.45f * EzScale; break;              // over the little one's dome: his face stays up for the lens
                case LookMode.Board: target = transform.TransformPoint(BoardPoint); break;
                case LookMode.Doors: target = transform.TransformPoint(new Vector3(0f, 1.6f, -4.4f)); break;
                case LookMode.Button: target = ButtonTop; break;
                case LookMode.Away: target = rig.Local(0.9f * EzH, 0.78f * EzH, 1.2f); break;
                case LookMode.Gun: target = PumpGrip + Vector3.up * 0.1f; break;
                case LookMode.Out: target = L18LookOut(); break;                                         // level 18: out of the HQ's main window
                default: target = LavaWalkOn ? LavaLookPoint(rig) : CamPos; break;                      // level 7: from the shot's framing, never the last shot's lens (OzEvilStage.L07Walk.cs)
            }
            bool single = false;
            // FaceCamCap deg away from the lens in his own shots (FaceCamCapWide in the wide / two-shots): the look is pulled toward the lens
            // by what is over (he looks at BS-01 / the board / the button with his eyes and part of his head, both red eyes stay in view)
            {
                Vector3 hp = rig.Head.position, toCam = CamPos - hp, toT = target - hp; float ty = toT.y; toCam.y = 0f; toT.y = 0f;
                single = camName == "ez_med" || camName == "ez_face" || camName == "ez_close" || camName == "ez_gun" || camName == "ez_gunclose" || camName == "ez_full" || camName == "ez_wide";
                float cap = single ? FaceCamCap : FaceCamCapWide;
                if (!LavaWalkOn && toCam.sqrMagnitude > 1e-4f && toT.sqrMagnitude > 1e-4f)
                {
                    float ang = Vector3.SignedAngle(toCam, toT, Vector3.up);
                    if (Mathf.Abs(ang) > cap) target = hp + Quaternion.AngleAxis(Mathf.Sign(ang) * cap, Vector3.up) * toCam.normalized * toT.magnitude + Vector3.up * ty;
                }
            }
            // the look point moves (no snap from one target to the next): about 0.2 s, like a head turn
            if (!ezLookInit || clock < 0.15f) { ezLookPt = target; ezLookVel = Vector3.zero; ezLookInit = true; }   // a scene's first frames: the camera takes its place, the look with it (no turn from the rest pose)
            else if (dt > 0f) ezLookPt = Vector3.SmoothDamp(ezLookPt, target, ref ezLookVel, 0.2f, 30f, dt);
            rig.LookAt(ezLookPt, 1f, LavaWalkOn ? walk.headTurnMax : l18On ? 45f : 70f, single ? 8f : 22f, 0.35f);                                   // little look-down in his own shots: the hat brim never hides his eyes
            float nod = ezNodT < 0.42f ? Mathf.Sin(ezNodT / 0.42f * Mathf.PI) * ezNodAmp : 0f;
            float shake = 0f;
            if (ezShakeAt >= 0f) { float u = clock - ezShakeAt; shake = u < 0.9f ? Mathf.Sin(u * Mathf.PI * 2f * 3.0f) * 9f * (1f - u / 0.9f) * Mathf.Clamp01(u / 0.08f) : 0f; if (u > 0.9f) ezShakeAt = -10f; }
            float around = 0f;
            if (ezAroundAt >= 0f)
            {   // left, right, a little left again
                float u = clock - ezAroundAt;
                around = u < 0.45f ? Mathf.SmoothStep(0f, -42f, u / 0.45f) : u < 0.95f ? Mathf.Lerp(-42f, 40f, Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.5f)) : u < 1.5f ? Mathf.Lerp(40f, -20f, Mathf.SmoothStep(0f, 1f, (u - 0.95f) / 0.55f)) : Mathf.Lerp(-20f, 0f, Mathf.SmoothStep(0f, 1f, (u - 1.5f) / 0.4f));
                if (u > 1.9f) ezAroundAt = -10f;
            }
            float lift = -1.5f * ezMouthSmooth;
            rig.HeadAdd(nod + lift + GunNod(), 0f, shake + around);
            if (dt > 0f) ezNodT += dt;
        }

        void TickJawAndEyes(float dt)
        {
            if (ezJaw) { ezJaw.localPosition = ezJawRestPos + ezJawDrop * (Mathf.Min(1.6f, ezMouthSmooth * 1.15f)); }
            if (ez.BodyMat)
            {
                float flick = 1f + 0.08f * (Mathf.PerlinNoise(clock * 2.7f, 0.37f) - 0.5f) * 2f;
                float k = (1.0f + 0.8f * ezMouthSmooth) * flick;
                ez.BodyMat.SetVector("_EmissionColor", (Vector4)(new Color(5.0f, 0.3f, 0.12f) * k));
            }
        }

        /// <summary>The director, every frame: who talks now and how loud (0..1). The other robot's value is 0.</summary>
        public void Talk(string who, float mouth01)
        {
            bool ezTalks = who == OzBlueScenes.EvilZod, skTalks = who == OzBlueScenes.Sidekick;
            float m = Mathf.Clamp01(mouth01);
            ezMouth = ezTalks ? m : 0f;
            // a small nod on a strong onset now and then (it was every 0.45 s at 3.5-6 deg: a nodding jitter)
            if (ezTalks && m - ezLastMouth > 0.4f && clock > ezNextOnset && ezNodT > 0.5f) { ezNodT = 0f; ezNodAmp = Random.Range(2.2f, 3.6f); ezNextOnset = clock + 0.8f; }
            ezLastMouth = ezTalks ? m : 0f;
            SidekickTalk(skTalks ? m : 0f);
        }
        public float EzMouth => ezMouthSmooth;
        /// <summary>The Play checks: the crouch now (m).</summary>
        public float CrouchNow => ezCrouch;
    }
}
