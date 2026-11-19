using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 rule DV171 EXIT DRIVER (Sim/ZObject.VehicleExit.cs), the look: the driver of a light, medium or heavy tank or the mech JUMPS off it.
    /// View only, nothing here touches the rules. The sim lets him out in one tick, puts the robot on the first free spot beside the hull and
    /// holds him as getting out for ZObject.DriverOutHoldTicks of his vehicle: the moment this view's figure touches the ground. The vehicle is
    /// emptied and stopped in that same tick (DV79); its gun goes quiet in the view at once (View/OzVehicleQuiet.cs) and, vehicle_exit_jump2,
    /// its drawn hull and turret stop in that frame too (View/UnitView.QuietStop.cs).
    /// The first jump kit's look failed in Unity: a knee bent sideways by code, cuts, a speed kink): the
    /// simple way, the robot's OWN clips only (no knee, hip or spine turned by code), at 1x:
    /// <list type="bullet">
    /// <item>
    /// His own gun is out of sight from the show to the landing's
    /// squat; at the squat's bottom (<see cref="HatchLandDownSec"/> after the touch) it is in his right hand again (its own palm socket: in his
    /// hand from the first frame, never in the air) and he brings it up: his pose blends from the landing's squat into his armed idle over
    /// <see cref="HatchGunUpSec"/> while his legs stand up, with a short gun-ready sound; from then on nothing hides it again (Place's old
    /// "drawn after 86 % of the walk out" rule is not his any more), and at the hand-over his own model's gun is checked
    /// (<see cref="HatchGunHandOver"/>).</item>
    /// <item>vehicle_exit_jump3: the lid is KICKED open in <see cref="HatchKickSec"/> from the frame the view learns of the exit (its own open
    /// sounds; StepRig's rate for this rig, HatchLidOpenTime); he shows at full size at <see cref="HatchKickShowFrac"/> of the kick (or, with a
    /// commander drawn in the hatch, in the click's frame, swapped for him), already crouched: his own crouch clip (HatchCrouchClips: the
    /// deepest real human crouch on his rig) as deep as the push needs, the root placed so his soles stand on the drawn rim;</item>
    /// <item>he holds the crouch <see cref="HatchCrouchHolds"/> s (the lid's last swing; his whole figure leaning back a little over his planted
    /// soles), then the push (<see cref="HatchPushSec"/> 0.09 s, his soles PLANTED on the drawn rim): his whole figure swings forward about his
    /// soles to upright while his crouch clip straightens his legs to standing (only the root is turned; the legs are the clip's own), both from
    /// rest at a constant rate of change, solved so his hips leave at exactly the arc's take-off velocity, mostly UP (no kink); a jump that needs
    /// a deeper crouch than his clip has or more than <see cref="HatchJumpMaxUpMps"/> up waits crouched longer (the same touch);</item>
    /// <item>ONE ballistic arc of his hips under real gravity (<see cref="HatchJumpGravity"/>) with ONE forward somersault in the air above and
    /// beside the hull: the whole figure turned 360 deg about his hips, starting at the push's swing rate and at rest again at
    /// <see cref="HatchFlipEndFrac"/> of the flight (upright); the legs tuck in (the same clip, <see cref="HatchAirTuckM"/>) and straighten for the
    /// touch; his feet touch 2-3.5 m out beside the vehicle exactly at the rules' hold;</item>
    /// <item>his knees take the impact (the same clip down <see cref="HatchLandDepthM"/> in <see cref="HatchLandDownSec"/>), he stands up on it
    /// (<see cref="HatchLandRiseSec"/>) and walks to his robot's spot.</item>
    /// </list>
    /// The landing: on free ground on his side of the hull, toward his robot's spot; the plan tests his body (head, chest, hips, hands, knees,
    /// ankles, toes, soles, measured on his own rig in the three poses) along the crouch, the push and the arc against every drawn part of the
    /// vehicle (<see cref="HatchSolid"/>: each part's mesh as 0.1 m columns in its own space; the lid at its open pose) and the buildings and flags
    /// within 12 m; blocked: a shorter crouch (a higher jump, the same touch), another take-off point on the rim, another direction, a landing
    /// farther out; none clear: the best one.
    /// vehicle_exit_jump3 (Unity E2b: the V2 look read as a TUMBLE off the hull's edge, his hips only 0.13-0.27 m over the take-off, 1.6-2.3 m/s
    /// up and 3-5 m/s out, and he waited in the hatch 0.25-0.36 s): a real LEAP. The lid is KICKED open (<see cref="HatchKickSec"/>, its own
    /// open sounds), he shows already crouched as it opens (<see cref="HatchKickShowFrac"/>), the push is short and strong
    /// (<see cref="HatchPushSec"/> 0.09 s) from a deep crouch of his OWN clip (<see cref="HatchCrouchClips"/>: the deepest real human crouch
    /// found on his rig at the plan, both feet planted, read from his own controller: grab_stand_L on the BS-01 rigs, a squat down to 129 deg
    /// of knee and back up; jump_takeoff where a rig has nothing deeper), the take-off mostly UP (about 3.5 m/s up, 2 m/s out: his hips rise
    /// 0.6 m on the tanks, 0.4 m on the mech), the somersault in the air above and beside the hull, the landing 2-3.5 m out, absorbed by
    /// the same clip's squat. No knee, hip or spine is turned by code. The planner's clearance is the Play check's cut rule
    /// (<see cref="HatchMarginFor"/>) on the same body points (every bone of his skin and every bone segment's middle).
    /// The hull destroyed (or driven again) while he is inside or crouched: the same jump at once (<see cref="HatchBailJump"/>); killed in the
    /// hatch or the air: his own model takes the figure's place and dies there (the arc's velocity carried on); paused: the figure stands still;
    /// the game over with its clock stopped: the jump ends on the real clock. Selection, shots, blasts and flames follow the figure.
    /// </summary>
    public partial class BoardingView
    {
        /// <summary>DV171 look: always on (the jump is the only way out of the light, medium and heavy tank and the mech; AudioDirector leaves its
        /// own +0.45 s landing sound to this view).</summary>
        public static bool HatchExitOn => true;
        /// <summary>The timeline (s of game time): he shows this long before the lid is fully open; the crouch held before the push (the plan
        /// tries them in this order: a shorter crouch = a longer, higher flight with the same touch); the push; the knee bend after the touch.</summary>
        /// <summary>vehicle_exit_jump3: HatchJumpLidLeadSec is read no more (the lid is kicked open: HatchKickSec, HatchKickShowFrac); the push
        /// (s: short and strong, a strong human jump's push is 0.1-0.2 s from a deep crouch); the knee bend of the landing (s: down into his own
        /// clip's squat by HatchLandDepthM, fast at first).</summary>
        public static float HatchJumpLidLeadSec = 0.10f, HatchPushSec = 0.09f, HatchLandDownSec = 0.12f;
        /// <summary>The stand-up from the landing's squat (s): his own crouch clip straightening again (smoothstep: at most about 1.4 m/s up).</summary>
        public static float HatchLandRiseSec = 0.35f;
        /// <summary>vehicle_exit_gun: the gun back in his hands after the leap: the blend from the landing's squat into his armed idle (s; it
        /// starts at the squat's bottom, HatchLandDownSec after the touch, so it is over before the stand-up ends), the switch of that blend
        /// (off: the stand-up on his crouch clip with the gun already in his hand, then the walk's own cross-fade), the gun-ready sound's gain.</summary>
        public static float HatchGunUpSec = 0.30f, HatchGunSoundGain = 0.55f;
        public static bool HatchGunBlendOn = true;
        /// <summary>vehicle_exit_gun proof counters: guns brought up after a landing, of them with no gun found on the figure, gun-ready sounds
        /// played; hand-overs checked, of them with his model's gun object switched off (switched on there), with no gun found on his model, and
        /// with a gun renderer of his model off after the hand-over (counted only: ShowLive puts back exactly what was drawn at the click).</summary>
        public static int HatchGunsUp, HatchGunsMissing, HatchGunSounds, HatchGunHandOvers, HatchGunHandObjOn, HatchGunHandNoGun, HatchGunHandRendOff;
        /// <summary>vehicle_exit_gun review 2: landings whose whole stand-up one long frame stepped past (4x game speed, a hitch): the gun was
        /// brought up in that frame, before the walk on (never left to Place's old rule).</summary>
        public static int HatchGunsLate;
        /// <summary>vehicle_exit_jump3: the crouch held after he shows (s), tried in this order by the planner (the first: the lid fully open as the
        /// push starts; a later push = a shorter, lower flight with the same touch).</summary>
        public static readonly float[] HatchCrouchHolds = { 0.03f, 0.02f, 0.07f };
        /// <summary>vehicle_exit_jump3: the lid is KICKED open at an exit in this many s of game time (view only; its own open sounds play as
        /// before: StepRig's rate for this rig, HatchLidOpenTime); he shows crouched in the hatch when the lid is this far through its kick (it
        /// stands at hatchOpenDeg x SmoothStep(that) then, 85 deg of 95: clear of his body).</summary>
        public static float HatchKickSec = 0.12f, HatchKickShowFrac = 0.80f;
        /// <summary>vehicle_exit_jump3: his crouch is the deepest real human crouch among these clips of his own controller (both ankles level
        /// within HatchCrouchFeetLevelM, his hips over his feet within HatchCrouchBalanceM, rising without a dip to standing within
        /// HatchCrouchStandTolM of his idle; the deepest range wins, at least HatchCrouchMinRangeM): grab_stand_L / _R (the BS-01 rigs: a squat
        /// down to 129 deg of knee and up, Blender read of BS01_RobotClips_v1.fbx), landing, jump_takeoff (every rig: 22 / 27 deg). The flight's
        /// tuck (m below standing) and the landing's squat (m) are the same clip's poses.</summary>
        public static readonly string[] HatchCrouchClips = { "grab_stand_L", "grab_stand_R", "landing", "jump_takeoff" };
        public static float HatchCrouchFeetLevelM = 0.05f, HatchCrouchBalanceM = 0.30f, HatchCrouchStandTolM = 0.12f, HatchCrouchMinRangeM = 0.05f, HatchAirTuckM = 0.25f, HatchLandDepthM = 0.30f;
        /// <summary>Gravity (m/s2 of the game's metres: 8 Zod px = 1 m = 1 Unity unit); the take-off's upward speed at most (a longer time to the
        /// touch: he waits crouched longer); the jump at once off a destroyed hull starts at least this fast upward, its flight at most HatchJumpBailMaxSec.</summary>
        public static float HatchJumpGravity = 9.81f, HatchJumpMaxUpMps = 4.2f, HatchJumpMinUpMps = 1.0f, HatchJumpBailMaxSec = 1.0f;
        /// <summary>Review round 2 (his soles slid along the rim in the push): the push swings his whole figure about his planted soles from a lean
        /// back of at most this much (deg) in the crouch to upright at the take-off; a jump that needs more is not taken while another is clear.</summary>
        public static float HatchLeanMaxDeg = 20f;
        /// <summary>The somersault: upright again at this part of the flight; the legs bend in over the first HatchTuckFrac and straighten over the last.</summary>
        public static float HatchFlipEndFrac = 0.85f, HatchTuckFrac = 0.15f;
        /// <summary>The landing: at least this far (m) outside the hull's and turret's footprint, at most this far past his robot's spot, in these
        /// steps (m); the directions tried (deg off the line to his robot); the take-off points tried (m from the rim's middle: toward the
        /// landing, to its side).</summary>
        public static float HatchJumpOutM = 0.6f, HatchJumpPastSpotM = 1.5f, HatchJumpStepM = 0.25f;
        /// <summary>vehicle_exit_jump3: the landing from the take-off point (m, flat): from at least HatchJumpLandMinM (or the footprint +
        /// HatchJumpOutM) out to HatchJumpLandMaxM (the leap's 1.5-2.9 m/s out over its 1.1-1.3 s flight).</summary>
        public static float HatchJumpLandMinM = 2.0f, HatchJumpLandMaxM = 3.5f;
        public static readonly float[] HatchJumpTurns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
        /// <summary>vehicle_exit_jump3: the take-off points (m, flat, from the hatch's middle): x away from the lid's hinge, y across that (E2b: the
        /// V2 points, taken toward the landing, put his body into the open lid and the turret in the push: -0.02..-0.07 m); his soles stand where
        /// the open lid is farthest, so he springs away from it.</summary>
        public static readonly Vector2[] HatchTakeoffPts = { new Vector2(0.15f, 0f), new Vector2(0.25f, 0f), new Vector2(0.25f, 0.12f), new Vector2(0.25f, -0.12f), new Vector2(0f, 0f), new Vector2(0.10f, 0.15f), new Vector2(0.10f, -0.15f) };
        /// <summary>The clearance (vehicle_exit_jump3: ONE rule for the plan and the Play check's cut count, HatchMarginFor): every body point
        /// (every bone of his skin and every bone segment's middle, as the cut count reads them) keeps HatchJumpLimbM (a limb's half thickness)
        /// from every part, a hand HatchHandExtraM more (the robot's fist past its wrist bone: the BS-01 rigs have no finger bones), in the crouch,
        /// the push and the air; the plan takes the first arc clear by HatchJumpClearM more, else the first clear one; HatchRimMarginM / HatchSkinM
        /// are read no more; points in the hatch's opening (HatchHoleM round its middle, from HatchHoleDownM below the rim to HatchHoleUpM above
        /// it) are in the hole; mesh columns of HatchSolidCellM; at most HatchJumpMaxTries arcs or HatchJumpMaxMs of real time are spent on one
        /// plan (then the first clear one so far, else the least cut: fix round 4, HatchJumpMaxMsNoClear).</summary>
        public static float HatchJumpLimbM = 0.10f, HatchJumpClearM = 0.03f, HatchRimMarginM = 0.12f, HatchSkinM = 0.02f, HatchHandExtraM = 0.06f;
        public static float HatchHoleM = 0.42f, HatchHoleUpM = 0.30f, HatchHoleDownM = 0.05f, HatchSolidCellM = 0.10f;
        public static int HatchJumpMaxTries = 900; public static float HatchJumpMaxMs = 80f, HatchJumpMaxMsNoClear = 120f;
        /// <summary>Fix round 4 (review: a plan cut short by its time could take a cut): HatchJumpMaxMs ends the search for an arc clear by
        /// HatchJumpClearM more once a clear arc is in hand (that one is taken: no cut); with none in hand yet the search goes on up to
        /// HatchJumpMaxMsNoClear (the arc model needs at most 56 arcs: about 19 ms on the editor's Mono, tools/budget_harness.py).</summary>
        /// <summary>Fix round 4 (review: the planner's budget with 47 points a pose): HatchSolid.Gap is exact up to this far (m) over a point's
        /// margin and HatchClear reports at most this (every decision of the plan lies below it: 0 and HatchJumpClearM), which lets HatchClear
        /// skip a point's test while a lower bound of its distance stays over it: the same plan with a fraction of the tests.</summary>
        static float HatchGapExact => Mathf.Max(0.05f, HatchJumpClearM + 0.02f);
        /// <summary>Proof (since HatchExitStatsReset): HatchClear's gap tests made and skipped by its bound; the click frames' plans (ms: his rig,
        /// the solids and the arcs) at most, and how often a plan ran out of HatchJumpMaxTries / HatchJumpMaxMs.</summary>
        public static int HatchClearTests, HatchClearSkipped, HatchPlanBudgetHits; public static float HatchPlanMsMax;
        /// <summary>Fix round 4 (review: at the editor's ~30 fps a show one frame after its planned moment re-ran the plan and the check then
        /// skipped the leap row): the show is a slip (planned again) only when it comes later than its planned moment by more than this frame's
        /// step + HatchSlipFrameExtraSec, at least HatchSlipMinSec and at most HatchSlipMaxTolSec (a real slip: another figure on the rim, a
        /// commander still drawn, a hitch).</summary>
        public static float HatchSlipMinSec = 0.03f, HatchSlipFrameExtraSec = 0.01f, HatchSlipMaxTolSec = 0.06f;
        /// <summary>vehicle_exit_jump3: the one clearance rule (the plan and the Play check's cut count): a body point named `bone` (a bone of his
        /// skin, or the middle of the segment to its parent: that bone's name) must keep this far (m) from every solid. A hand: HatchHandExtraM more.</summary>
        public static float HatchMarginFor(string bone) => HatchJumpLimbM + (HatchIsHand(bone) ? HatchHandExtraM : 0f);
        /// <summary>A hand or finger bone (by its Mixamo / humanoid name: "Hand", "Thumb", "Index", "Middle", "Ring", "Pinky").</summary>
        public static bool HatchIsHand(string bone)
        {
            if (string.IsNullOrEmpty(bone)) return false;
            foreach (var k in HandWords) if (bone.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        static readonly string[] HandWords = { "Hand", "Thumb", "Index", "Middle", "Ring", "Pinky" };
        /// <summary>The lid is kept open this long after the take-off (s).</summary>
        public static float HatchJumpLidKeepSec = 0.25f;
        /// <summary>The walk on to his robot (m/s), its ground steps, the take-off sounds' gains.</summary>
        public static float HatchWalkMps = 1.75f, HatchGroundStrideM = 0.7f, HatchGroundStepGain = 0.6f, HatchTakeoffGain = 0.8f, HatchWhooshGain = 0.7f;
        /// <summary>A second robot of the same squad (Brad's) shows as the first takes off and jumps turned this much to the side.</summary>
        public static float HatchJumpStaggerDeg = 40f;
        /// <summary>A robot ordered in while the driver is still on the hull waits this far (m) outside the hull's box, at least HatchEnterClearM
        /// from the old driver's landing and his robot, until he has landed + HatchEnterGapSec (at most HatchEnterMaxHoldSec), walking at HatchEnterWaitMps.</summary>
        public static float HatchEnterSpotOutM = 0.9f, HatchEnterClearM = 1.8f, HatchEnterGapSec = 0.3f, HatchEnterMaxHoldSec = 6f, HatchEnterWaitMps = 1.4f;
        const float HatchLidMinR = 0.42f;

        enum JumpPhase { Inside, Crouch, Push, Air, Land, Walk }

        /// <summary>Proof: one record per driver let out (the Play check reads it): the plan (s of game time from the click: shows, takes off,
        /// touches; the flight, the apex of his hips above the take-off, the landing's distance, the turn off the line to his robot, the least
        /// clearance and what it was against) and what was drawn (the touch, the frames of the take-off and landing sounds, the turn drawn, the
        /// size drawn, the tilt at the touch).</summary>
        public sealed class HatchJumpRec
        {
            public int robotId, vehicleId, index; public string type = "", plan = "", clips = "", worst = "";
            public double clickSim, dueSim = -1; public float holdSec, appearAt, takeoffAt, touchAt, flight, apexM, landM, turnDeg, clearM; public bool late, bailed, dead, clearFound, posesRead;
            public float touchTau = -1f; public double touchSim = -1; public int touchFrame = -1, takeoffFrame = -1, takeoffSoundFrame = -1, landSoundFrame = -1, standFrame = -1;
            public bool takeoffSound, landSound; public float flipDeg, scaleMin = float.MaxValue, scaleMax, tiltAtTouchDeg = -1f, feetAboveAtTouch = -1f; public int frames, groundSteps, tries;
            public Vector3 takeoff, landing;
            /// <summary>Fix round 4: the plan at the click (ms of real time: his rig, the solids, the arcs and the walk on), the arcs' own time (ms),
            /// HatchClear's gap tests made and skipped by its bound, whether the plan ran out of HatchJumpMaxTries / HatchJumpMaxMs (then the
            /// clearest arc so far), his rig's measure taken from the cache; how late he showed against the plan (s; a slip only past one
            /// frame's step: HatchSlipMinSec .. HatchSlipMaxTolSec).</summary>
            public float planMs, arcsMs, showLateSec = -1f; public int gapTests, gapSkipped; public bool budgetHit, bodyCached;
            /// <summary>The frame he showed, the frame a commander who was up was swapped out (-1: none), the show's slip (s), the commander swapped.</summary>
            public int shownFrame = -1, commanderGoneFrame = -1; public float slipSec; public bool commanderWasUp, commanderSwapped;
            /// <summary>The crouch held and the push (s), the take-off velocity (out, up; m/s), the legs' straightening at the take-off (jump_takeoff's
            /// normalized time), the knee bend and the stand-up (s), his ankle over the sole standing (m, measured on his rig at the plan).</summary>
            public float crouchSec, pushSec, vhMps, vy0Mps, legsAtTakeoff, landDownSec, landRiseSec, landSec, ankle0;
            /// <summary>Review round 2: the crouch's lean back over his planted soles (deg), the push's swing rate at the take-off (deg/s, the
            /// somersault's start), his legs' straightening in the push and the most his clip has (m), the flight shortened for his legs (s; 0: not).</summary>
            public float leanDeg, swingDegps, riseM, riseMaxM, waitedSec;
            /// <summary>The arc's velocity handed to his death in the air (m/s; zero: none).</summary>
            public Vector3 deathCarry;
            /// <summary>vehicle_exit_jump3: his crouch clip (the deepest real crouch on his rig) and its range (m: his hips over his soles from its
            /// deepest frame to standing), the crouch he takes (riseM: m below standing), the lid's kick (s) and the moment it started (s after the
            /// click), the planned hips at the take-off (world) and the take-off velocity (world m/s), the flight's tuck and the landing's squat (m).</summary>
            public string crouchClip = ""; public float crouchRangeM, kickSec, kickAt = -1f, tuckM, landDepthM; public Vector3 hEnd, v0;
            /// <summary>vehicle_exit_jump3, the knee row (J1j) against HIS rig's own idle pose (E2b: the BS-01 rig's left knee is drawn 16.5 deg bent
            /// even at rest, its kink 44.5 deg off the side axis, so the geometric hinge of a nearly straight knee is no knee's hinge): for the left
            /// [0] and right [1] knee, the shank's direction (knee -> ankle) in the thigh bone's own frame in his idle's first frame, and the pelvis's
            /// side axis (left -> right thigh) in that frame; kneeRefOk: read on his rig.</summary>
            public readonly Vector3[] kneeShank = new Vector3[2], kneeSide = new Vector3[2]; public bool kneeRefOk;
            /// <summary>vehicle_exit_gun: the frame his gun came back to his hand and its time after the touch (s), the gun object's name on the
            /// figure ("" none found), the gun-ready sound; at the hand-over: the frame, his model's gun found, its object switched on there, its
            /// renderers drawn / off after it.</summary>
            public int gunUpFrame = -1, handFrame = -1, handRendOn, handRendOff; public float gunUpAfterTouch = -1f; public string gunName = ""; public bool gunSound, handGunFound, handObjOn;
            /// <summary>vehicle_exit_gun review 2: one long frame stepped past his whole stand-up; the gun came back in that frame (HatchGunsLate).</summary>
            public bool gunLate;
        }
        /// <summary>Proof: the jump records by robot id (cleared past 64).</summary>
        public static readonly Dictionary<int, HatchJumpRec> HatchJumps = new Dictionary<int, HatchJumpRec>();
        /// <summary>Proof counters (since <see cref="HatchExitStatsReset"/>).</summary>
        public static int HatchExitsPlanned, HatchJumpsTaken, HatchExitLandings, HatchTakeoffSounds, HatchJumpsLate, HatchExitBails, HatchEntersHeld, HatchEntersReleased,
                          HatchExitDeaths, HatchCommandersTaken, HatchLidsShut, HatchShotsOnFigure, HatchBlastsOnFigure, HatchFlamesOnFigure, HatchExitGroundSteps;
        public static int HatchCommandersSwapped, HatchDeathCarries, HatchDeathCarriedBodies; public static Vector3 HatchExitLastDeathCarry;
        /// <summary>Proof: the last plan's line; the last held boarding's wait (s).</summary>
        public static string HatchExitLastPlan = "";
        public static float HatchEnterLastHold;

        public static void HatchExitStatsReset()
        {
            HatchExitsPlanned = HatchJumpsTaken = HatchExitLandings = HatchTakeoffSounds = HatchJumpsLate = HatchExitBails = HatchEntersHeld = HatchEntersReleased = 0;
            HatchExitDeaths = HatchCommandersTaken = HatchLidsShut = HatchShotsOnFigure = HatchBlastsOnFigure = HatchFlamesOnFigure = HatchExitGroundSteps = 0;
            HatchExitLastPlan = ""; HatchEnterLastHold = 0f;
            HatchGunsUp = HatchGunsMissing = HatchGunSounds = HatchGunHandOvers = HatchGunHandObjOn = HatchGunHandNoGun = HatchGunHandRendOff = HatchGunsLate = 0;   // vehicle_exit_gun
            HatchClearTests = HatchClearSkipped = HatchPlanBudgetHits = HatchBodyCacheHits = HatchBodyCacheReads = 0; HatchPlanMsMax = 0f;
            HatchExitLastDeathAt = Vector3.zero; HatchExitLastDeathOnHull = false;
            HatchExitDiesShown = HatchExitDiesOnHull = HatchExitMeltsAvoided = HatchDeckColliders = 0; HatchExitLastDieRenderers = -1; HatchExitLastDieAt = Vector3.zero; HatchExitLastDieFrom = "";
            HatchExitLastBailDrawnDead = false; HatchExitLastBailAfterRulesSec = -1f; HatchExitLastDeckTop = -1f;
            HatchTurretDecks = 0; HatchExitLastTurretDeckTop = -1f; HatchExitLastTurretDeckOver = false;
            HatchCommandersSwapped = HatchDeathCarries = HatchDeathCarriedBodies = 0; HatchExitLastDeathCarry = Vector3.zero;
        }

        /// <summary>One pose of his own clips, measured on his rig at the plan (the root's frame): his hips, the middle of his soles (the ankles'
        /// middle at the lower ankle's height less his standing ankle height), the ankles' middle over that, and his body points relative to
        /// his hips.</summary>
        sealed class HatchPose { public Vector3 hipsL, soleL; public float ankleMidUp; public readonly List<Vector3> pts = new List<Vector3>(); }
        /// <summary>His rig: the bones read (hips, feet, the body points with their kind: 0 a body point, 1 a hand), the state names of his clips,
        /// his standing ankle height (ankle0) and his poses; vehicle_exit_jump3: `push` is his CROUCH clip (the deepest real human crouch on his
        /// rig, HatchCrouchClips), read from its deepest normalized time ntDeep up to standing at ntTop in HatchPathN samples: ext[k] his hips over
        /// his soles there (rising), hos[k] his hips over his sole point, path[k] his body points; the push straightens his legs from the plan's
        /// crouch to standing (ext's top), the flight tucks them HatchAirTuckM and the landing squats HatchLandDepthM on the same path; the body
        /// points are every bone of his skin and every bone segment's middle (the Play check's cut-count points), kind 1 for a hand
        /// (HatchMarginFor); names: each point's bone (for the plan's line); landLen: the stand-up's time.</summary>
        sealed class HatchBody
        {
            public Transform hips, footL, footR; public readonly List<Transform> bones = new List<Transform>(); public readonly List<int> kind = new List<int>();
            public readonly List<Transform> ptA = new List<Transform>(), ptB = new List<Transform>(); public readonly List<string> names = new List<string>();
            public bool ok, cached; public string push, air, land; public float ankle0, landLen = 0.47f, reach = 1.5f, ntDeep = 0.5f, ntTop = 1f, range;
            public HatchPose crouch = new HatchPose(), straight = new HatchPose(), airP = new HatchPose();
            public readonly float[] ext = new float[HatchPathN];
            /// <summary>Review round 2: his hips over his sole point (his frame) at the same times of his crouch clip (the push's pose, planted).</summary>
            public readonly Vector3[] hos = new Vector3[HatchPathN];
            public readonly HatchPose[] path = new HatchPose[HatchPathN];
            public string why = "";
            /// <summary>vehicle_exit_jump3: the knee row's reference in his idle (HatchKneeRef).</summary>
            public readonly Vector3[] kneeShank = new Vector3[2], kneeSide = new Vector3[2]; public bool kneeOk;
            public HatchBody() { for (int k = 0; k < HatchPathN; k++) path[k] = new HatchPose(); }
        }
        /// <summary>vehicle_exit_jump3: the samples of his crouch clip's rise read at the plan; the samples of each candidate clip searched.</summary>
        const int HatchPathN = 9, HatchScanN = 17;

        /// <summary>One jump (review round 2: his soles planted on P through the crouch and the push): the take-off point P (his sole point), his
        /// hips in the crouch (h0: leaning back `lean` deg about P), at the take-off (hEnd: upright, his legs straightened `rise` m) and at the
        /// touch (hT), the take-off velocity v0 (= his hips' velocity at the push's end: the swing's rate omega rad/s about the flip axis and the
        /// legs' straightening, both from rest), the flat direction, the flight T and the push tp (s); need: the straightening the arc asked for
        /// (more than his clip has: the plan waits crouched longer); b: his body.</summary>
        sealed class HatchArc { public Vector3 P, h0, hEnd, hT, v0, dir; public float T, tp, lean, rise, need, omega; public HatchBody b; }

        sealed class HatchExit
        {
            public int vehicleId, robotId, index; public JumpPhase phase; public bool bailed, dead, shown, lidShut, cmdUp, pRimLSet;
            public double clickSim, due = -1;                                                                  // the click and the rules' out moment (sim s; -1: none)
            public float tau, appearAt, crouchSec, takeoffAt, touchAt, shownAt = -1f, slip, turn0, legsEnd = 1f;   // tau: game s since the click
            public Vector3 p0, land, dir, spot, markPos, pRimL; public HatchArc arc; public HatchBody body;
            public float lastFlip, deadFor, hullDeadAt = -1f, stepDist; public Vector3 stepPos; public bool stepInit;
            public HatchJumpRec rec;
            /// <summary>vehicle_exit_jump3: the lid's kick: when it started (tau, s after the click; -1: no kick, the lid was open) and how far the lid
            /// stood then (0 shut .. 1 open).</summary>
            public float kickAt = -1f, kickHatch0;
            /// <summary>Fix round 4: this frame's step of the exit's clock (s of game time; the slip's tolerance).</summary>
            public float lastStep;
            /// <summary>vehicle_exit_gun: his gun is in his hand again (from the squat's bottom on; nothing hides it after that) and the landing's
            /// time it came back (s after the touch).</summary>
            public bool gunUp; public float gunUpTl = -1f;
        }
        readonly Dictionary<Ghost, HatchExit> hatchExits = new Dictionary<Ghost, HatchExit>();

        sealed class EnterHold { public readonly List<Vector3> way = new List<Vector3>(); public int at; public float t, stepDist; public Vector3 stepPos; public bool stepInit; }
        readonly Dictionary<Ghost, EnterHold> enterHolds = new Dictionary<Ghost, EnterHold>();

        bool HatchExitFor(Ghost g, Rig r) =>
            g != null && r != null && !g.enter && !g.demo && g.seatK < 0 && r.mode == Mode.Roof && r.lid && r.obj != null && r.obj.IsHatchVehicle;

        // ------------------------------------------------------------------ the plan (StartExit, after BuildExit)
        /// <summary>StartExit, after BuildExit: the timeline on the click's clock (the lid, a commander in the hatch swapped for him in this frame),
        /// his poses measured on his rig, the jump planned; BuildExit's keys replaced by the walk on from the landing.</summary>
        void HatchExitPlan(Ghost g, Rig r)
        {
            if (!HatchExitFor(g, r)) return;
            var planClock = System.Diagnostics.Stopwatch.StartNew();                                           // fix round 4: the click frame's cost of the plan, recorded
            if (hatchExits.Count > 0 || enterHolds.Count > 0)
            {
                var gone = new List<Ghost>();
                foreach (var kv in hatchExits) if (kv.Key == null || kv.Key.done) gone.Add(kv.Key);
                foreach (var k in gone) hatchExits.Remove(k);
                gone.Clear();
                foreach (var kv in enterHolds) if (kv.Key == null || kv.Key.done) gone.Add(kv.Key);
                foreach (var k in gone) enterHolds.Remove(k);
            }
            var o = g.live ? g.live.Obj : null; var w = game ? game.World : null;
            int index = Mathf.Max(0, Mathf.RoundToInt(g.delay / Mathf.Max(0.01f, crewStagger)));              // StartExit's squad order (Brad's squad: two robots)
            var st = new HatchExit { vehicleId = r.obj.Id, robotId = g.robotId, index = index, phase = JumpPhase.Inside };
            float hold = ZObject.DriverOutHoldTicks(r.obj.Type) * (float)ZConst.TICK;
            st.due = o != null && ZWorld.ExitWalkHoldOn && o.ExitOutAt >= 0 && w != null ? o.ExitOutAt : -1;
            double now = w != null ? w.Time : 0;
            st.clickSim = st.due >= 0 ? st.due - hold : now;
            st.tau = Mathf.Max(0f, (float)(now - st.clickSim));
            st.cmdUp = r.crew && r.crew.activeSelf && r.crewPop > 0.001f;                                       // a commander drawn in the hatch (up, rising or sinking)
            // vehicle_exit_jump3: the lid is KICKED open from this frame (HatchKickSec; StepRig opens it at that rate, HatchLidOpenTime, with its own
            // open sounds); the commander's sink on the click's clock as before (the plan may come a few ticks after the EXIT's)
            bool lidClock = showLidCommander && r.lid && r.boarding == 0;
            float hatch0 = r.hatch, crew0 = r.crew ? r.crewPop : 0f;
            if (lidClock && index == 0 && st.tau > 0f && r.crew && r.crewPop > 0f && !st.cmdUp) r.crewPop = Mathf.Max(0.002f, r.crewPop - st.tau / Mathf.Max(0.01f, commanderSinkTime));
            if (lidClock && hatch0 < 0.999f) { st.kickAt = st.tau; st.kickHatch0 = hatch0; }
            bool swapped = false;
            if (st.cmdUp && index == 0)
            {   // the commander drawn in the hatch IS him: swapped in ONE frame, at the click, for the full-size driver already crouched on the rim
                if (r.crew) { r.crew.SetActive(false); Destroy(r.crew); }
                r.crew = null; r.crewPop = 0f; HatchCommandersSwapped++; swapped = true;
            }
            st.appearAt = HatchAppearAt(r, st.tau, st.cmdUp, hatch0, lidClock) + index * (HatchCrouchHolds[0] + HatchPushSec);   // the second robot shows as the first takes off
            st.touchAt = hold;
            st.body = HatchMeasure(g);
            var toW = g.live ? g.live.transform.position : Wp(r, new Vector3(C(r).x + r.box.extents.x + 1.5f, 0, C(r).z));
            st.spot = toW; st.turn0 = index * HatchJumpStaggerDeg;
            st.rec = new HatchJumpRec
            {
                robotId = g.robotId, vehicleId = r.obj.Id, index = index, type = r.obj.Type, clickSim = st.clickSim, dueSim = st.due, holdSec = hold, appearAt = st.appearAt,
                commanderWasUp = st.cmdUp, commanderSwapped = swapped, commanderGoneFrame = swapped ? Time.frameCount : -1, ankle0 = st.body.ankle0, posesRead = st.body.ok,
                clips = $"{st.body.push ?? "-"} (his crouch, {st.body.ntDeep:0.00} .. {st.body.ntTop:0.00}: {st.body.range:0.00} m) / {st.body.air ?? "-"} / {st.body.land ?? "-"}",
                crouchClip = st.body.push ?? "", crouchRangeM = st.body.range, kickSec = st.kickAt >= 0f ? HatchKickSec * (1f - st.kickHatch0) : 0f, kickAt = st.kickAt
            };
            HatchKneeRef(st.body, st.rec);
            string line = HatchPlanJump(r, st, toW, false);
            HatchWalkKeys(g, st);
            st.rec.planMs = (float)planClock.Elapsed.TotalMilliseconds; st.rec.bodyCached = st.body.cached; HatchPlanMsMax = Mathf.Max(HatchPlanMsMax, st.rec.planMs);
            line += $"; the whole plan at the click {st.rec.planMs:0.0} ms (his rig {(st.body.cached ? "from the cache of his figure's make" : "read on his rig now")})";
            g.delay = 0f; g.speedMul = 1f;
            g.scaleA = g.scaleB = 1f; g.scaleT0 = 0f; g.scaleSpan = 1f;                                         // his own size the whole way (no growth)
            g.fitA = g.fitB = 1f; g.fitT0 = 0f; g.fitT1 = -1f; g.fitBackT0 = -1f; g.stowA = g.stowB = 0f;
            exitDue[g] = -1;                                                                                   // K441's even pace-up is off for this figure (the touch is the rules' moment)
            if (g.go) { g.go.SetActive(false); g.go.transform.localScale = Vector3.one * g.baseScale; }
            r.restLid = false;                                                                                 // the exit's lid sounds at full gain
            st.rec.plan = line;
            if (HatchJumps.Count > 64) HatchJumps.Clear();
            HatchJumps[g.robotId] = st.rec;
            hatchExits[g] = st;
            HatchExitsPlanned++;
            HatchExitLastPlan = $"{r.obj.Type} #{r.obj.Id}: shows {st.appearAt:0.00} s, takes off {st.takeoffAt:0.00} s, touches {st.touchAt:0.00} s after the click "
                                + $"(the rules' hold {hold:0.00} s{(st.due >= 0 ? "" : ", DV132 off")}); {line}";
        }

        /// <summary>When he shows (s after the click) for a plan `tau` s after it: with a commander drawn in the hatch at the plan (the swap); else
        /// (vehicle_exit_jump3) as the kicked lid is HatchKickShowFrac through its kick from this frame (crouched already: no standing, no wait),
        /// never before the plan.</summary>
        float HatchAppearAt(Rig r, float tau, bool cmdUp, float hatch0, bool lidClock)
        {
            if (cmdUp) return Mathf.Max(0f, tau);
            if (lidClock) return tau + HatchKickSec * Mathf.Max(0f, HatchKickShowFrac - hatch0);
            float lidAt = tau + (1f - hatch0) / Mathf.Max(0.1f, hatchOpenRate) * SimRate;
            return Mathf.Max(tau, lidAt - (1f - HatchKickShowFrac) * HatchKickSec);
        }

        /// <summary>vehicle_exit_jump3: StepRig's lid time for this rig (s of game time for the whole swing): HatchKickSec while a hatch exit of it
        /// kicks the lid open (its driver inside, crouched or pushing), else Zod's lidOpenTime. Its open sounds (veh_lid_open, hatch) play as
        /// StepRig plays them.</summary>
        float HatchLidOpenTime(Rig r)
        {
            if (hatchExits.Count == 0 || r == null) return lidOpenTime;
            foreach (var kv in hatchExits)
            {
                var x = kv.Key; var s = kv.Value;
                if (x != null && !x.done && x.rig == r && s.kickAt >= 0f && !s.bailed && s.phase <= JumpPhase.Push) return HatchKickSec;
            }
            return lidOpenTime;
        }

        // ------------------------------------------------------------------ his rig: his own clips' poses, measured once at the plan
        /// <summary>A bone of the figure: the Animator's human bone, else by name (any "prefix:" and a "mixamorig" prefix dropped, case ignored).</summary>
        static Transform HatchBone(GameObject go, Animator a, HumanBodyBones hb, string name)
        {
            if (a && a.isHuman && a.avatar) { var t = a.GetBoneTransform(hb); if (t) return t; }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (string.Equals(HatchBoneName(t.name), name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
        static string HatchBoneName(string n)
        {
            int c = n.LastIndexOf(':'); if (c >= 0) n = n.Substring(c + 1);
            if (n.StartsWith("mixamorig", StringComparison.OrdinalIgnoreCase)) n = n.Substring(9);
            return n;
        }

        /// <summary>His rig at the plan: the bones (hips, feet; head, head top, chest, hands, knees, toes, and a sole under each ankle), his clips'
        /// state names, and his poses: idle's first frame (his standing ankle height: ankle0), jump_takeoff at its deepest bend (0.5: the crouch)
        /// and straight (1.0), jump_air's middle (the flight), his hips over his soles along jump_takeoff 0.5 .. 1.0 (ext), the landing clip's
        /// length. The figure is shown for this moment only; every bone and the root are put back after. No rig: a column of points.</summary>
        HatchBody HatchMeasure(Ghost g)
        {
            var b = new HatchBody(); var go = g.go; var t = go ? go.transform : null; var a = g.anim;
            float hip = hipHeight * g.baseScale;
            if (g.hips && t) { float m = Vector3.Dot(g.hips.position - t.position, t.up); if (m > 0.3f * hip && m < 2f * hip) hip = m; }
            string jt = Clip(a, "jump_takeoff"); b.push = jt; b.air = Clip(a, "jump_air"); b.land = Clip(a, "landing");
            Transform thL = null, thR = null, knL = null, knR = null, spine = null; SkinnedMeshRenderer sk = null;
            if (t)
            {
                b.hips = HatchBone(go, a, HumanBodyBones.Hips, "Hips") ?? g.hips;
                b.footL = HatchBone(go, a, HumanBodyBones.LeftFoot, "LeftFoot"); b.footR = HatchBone(go, a, HumanBodyBones.RightFoot, "RightFoot");
                thL = HatchBone(go, a, HumanBodyBones.LeftUpperLeg, "LeftUpLeg"); thR = HatchBone(go, a, HumanBodyBones.RightUpperLeg, "RightUpLeg");
                knL = HatchBone(go, a, HumanBodyBones.LeftLowerLeg, "LeftLeg"); knR = HatchBone(go, a, HumanBodyBones.RightLowerLeg, "RightLeg");
                spine = HatchBone(go, a, HumanBodyBones.Spine, "Spine");
                // vehicle_exit_jump3 (one rule with the Play check's cut count): his body points are every bone of his skin and every bone segment's
                // middle, as BoardingView.HatchFigureBones gives them to the check (its first enabled skinned mesh with bones)
                sk = null;
                foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.enabled && s.bones != null && s.bones.Length > 0) { sk = s; break; }
                if (sk != null)
                {
                    var set = new HashSet<Transform>(); foreach (var x in sk.bones) if (x) set.Add(x);
                    foreach (var x in sk.bones)
                    {
                        if (!x) continue;
                        int kd = HatchIsHand(x.name) ? 1 : 0; string n = HatchBoneName(x.name);
                        b.ptA.Add(x); b.ptB.Add(null); b.names.Add(n); b.kind.Add(kd); if (!b.bones.Contains(x)) b.bones.Add(x);
                        if (x.parent && set.Contains(x.parent)) { b.ptA.Add(x); b.ptB.Add(x.parent); b.names.Add(n + " (its segment's middle)"); b.kind.Add(kd); }
                    }
                }
            }
            b.ok = t && a && b.hips && b.footL && b.footR && jt != null && jt != "walking" && b.ptA.Count >= 6;
            float standH = 0f;
            string cacheKey = b.ok ? HatchBodyKey(g, a, sk, b) : null;                                       // fix round 4: a figure of the same make: its measure from the cache
            if (cacheKey != null && HatchBodyCache.TryGetValue(cacheKey, out var known)) { HatchBodyCopy(known, b); HatchBodyCacheHits++; }
            else if (b.ok)
            {
                var all = go.GetComponentsInChildren<Transform>(true); var lp = new Vector3[all.Length]; var lr = new Quaternion[all.Length];
                for (int i = 0; i < all.Length; i++) { lp[i] = all[i].localPosition; lr[i] = all[i].localRotation; }
                bool was = go.activeSelf, rm = a.applyRootMotion; float speed = a.speed; var cull = a.cullingMode;
                try
                {
                    if (!was) go.SetActive(true);
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;                                         // E2b fix: the figure is not drawn yet (CullUpdateTransforms would leave every bone as it was: all poses read the same, his legs' rise 0)
                    a.applyRootMotion = false; t.localScale = Vector3.one * g.baseScale;                   // measured at his own size (the plan draws him at it)
                    var inv = Quaternion.Inverse(t.rotation);
                    Vector3 L(Vector3 p) => inv * (p - t.position);
                    HatchSample(a, Clip(a, "idle"), 0f);
                    b.ankle0 = Mathf.Min(L(b.footL.position).y, L(b.footR.position).y);
                    standH = L(b.hips.position).y;
                    // vehicle_exit_jump3, the knee row's reference (J1j): his own idle pose on his rig (the BS-01 rig's left knee is drawn 16.5 deg
                    // bent at rest, its kink 44.5 deg off the side axis): each shank's direction and the pelvis's side axis in the thigh bone's own frame
                    if (thL && thR && knL && knR && spine)
                    {
                        var U = (spine.position - b.hips.position).normalized; var S = Vector3.ProjectOnPlane(thR.position - thL.position, U).normalized;
                        for (int k = 0; k < 2; k++)
                        {
                            var th = k == 0 ? thL : thR; var kn = k == 0 ? knL : knR; var ft = k == 0 ? b.footL : b.footR;
                            b.kneeShank[k] = Quaternion.Inverse(th.rotation) * (ft.position - kn.position).normalized; b.kneeSide[k] = Quaternion.Inverse(th.rotation) * S;
                        }
                        b.kneeOk = S.sqrMagnitude > 0.5f;
                    }
                    void Read(HatchPose p)
                    {
                        Vector3 fl = L(b.footL.position), fr = L(b.footR.position), mid = 0.5f * (fl + fr); float low = Mathf.Min(fl.y, fr.y);
                        p.hipsL = L(b.hips.position); p.soleL = new Vector3(mid.x, low - b.ankle0, mid.z); p.ankleMidUp = mid.y - p.soleL.y;
                        p.pts.Clear();
                        for (int i = 0; i < b.ptA.Count; i++) p.pts.Add(L(b.ptB[i] ? 0.5f * (b.ptA[i].position + b.ptB[i].position) : b.ptA[i].position) - p.hipsL);
                    }
                    // vehicle_exit_jump3: his crouch = the deepest REAL human crouch among his own clips (HatchCrouchClips): from the clip's deepest
                    // frame it rises to standing with both feet planted (ankles level), his hips over his feet, no dip; the deepest such rise wins
                    var hs = new float[HatchScanN]; var level = new bool[HatchScanN]; var bal = new bool[HatchScanN];
                    string best = null; float bestRange = 0f, bestDeep = 0.5f; var why = new System.Text.StringBuilder();
                    foreach (var cand in HatchCrouchClips)
                    {
                        if (!a.HasState(0, Animator.StringToHash(cand)) && !a.HasState(0, Animator.StringToHash("Base Layer." + cand))) { why.Append(cand).Append(" not in his controller; "); continue; }
                        int kd = 0;
                        for (int k = 0; k < HatchScanN; k++)
                        {
                            HatchSample(a, cand, k / (float)(HatchScanN - 1));
                            Vector3 fl = L(b.footL.position), fr = L(b.footR.position), mid = 0.5f * (fl + fr); float low = Mathf.Min(fl.y, fr.y);
                            var hp = L(b.hips.position); hs[k] = hp.y - (low - b.ankle0);
                            level[k] = Mathf.Abs(fl.y - fr.y) <= HatchCrouchFeetLevelM; var off = hp - mid; off.y = 0f; bal[k] = off.magnitude <= HatchCrouchBalanceM;
                            if (hs[k] < hs[kd]) kd = k;
                        }
                        float range = hs[HatchScanN - 1] - hs[kd]; string bad = null;
                        for (int k = kd; k < HatchScanN && bad == null; k++)
                        {
                            if (!level[k]) bad = "a foot off the ground";
                            else if (!bal[k]) bad = "his hips off his feet";
                            else if (k > kd && hs[k] < hs[k - 1] - 0.01f) bad = "it dips on the way up";
                        }
                        if (bad == null && Mathf.Abs(hs[HatchScanN - 1] - standH) > HatchCrouchStandTolM) bad = "it does not end standing";
                        if (bad == null && range < HatchCrouchMinRangeM) bad = "too shallow";
                        why.Append(cand).Append(' ').Append(range.ToString("0.00")).Append(" m").Append(bad != null ? " (" + bad + ")" : "").Append("; ");
                        if (bad == null && range > bestRange + 1e-4f) { best = cand; bestRange = range; bestDeep = kd / (float)(HatchScanN - 1); }
                    }
                    b.push = best ?? jt; b.ntDeep = best != null ? bestDeep : 0.5f; b.ntTop = 1f; b.why = why.ToString();
                    for (int k = 0; k < HatchPathN; k++)
                    {
                        HatchSample(a, b.push, Mathf.Lerp(b.ntDeep, b.ntTop, k / (float)(HatchPathN - 1))); Read(b.path[k]);
                        b.ext[k] = b.path[k].hipsL.y - b.path[k].soleL.y; b.hos[k] = b.path[k].hipsL - b.path[k].soleL;   // his hips over his sole point
                    }
                    for (int k = 1; k < HatchPathN; k++) b.ext[k] = Mathf.Max(b.ext[k], b.ext[k - 1]);                 // rising (interpolation's crumbs)
                    b.range = b.ext[HatchPathN - 1] - b.ext[0];
                    HatchSample(a, jt, 1f); Read(b.straight);
                    HatchSample(a, b.air ?? jt, 0.5f); Read(b.airP);
                    b.crouch = b.path[0];
                    b.landLen = HatchLandRiseSec;                                                               // the stand-up: his crouch clip straightening (HatchLandFrame)
                }
                catch (Exception) { b.ok = false; }
                finally
                {
                    for (int i = 0; i < all.Length; i++) if (all[i]) { all[i].localPosition = lp[i]; all[i].localRotation = lr[i]; }
                    a.applyRootMotion = rm; a.speed = speed; a.cullingMode = cull;
                    if (!was) go.SetActive(false);
                    g.clipNow = null;
                }
                if (b.ok) { if (HatchBodyCache.Count > 32) HatchBodyCache.Clear(); HatchBodyCache[cacheKey] = b; HatchBodyCacheReads++; }
            }
            if (!b.ok)
            {   // no rig read: a column of points, his hips at one hip height, his feet at the root
                b.ankle0 = 0.06f * hip; b.range = 0f; b.kneeOk = false; b.push = jt; b.ntDeep = 0.5f; b.ntTop = 1f;
                b.names.Clear(); b.kind.Clear();
                for (float y = -hip; y <= 0.9f * hip + 1e-3f; y += 0.15f) { b.names.Add("a point of his column"); b.kind.Add(0); }
                var poses = new List<HatchPose>(b.path) { b.straight, b.airP };
                foreach (var p in poses)
                {
                    p.hipsL = Vector3.up * hip; p.soleL = Vector3.zero; p.ankleMidUp = b.ankle0; p.pts.Clear();
                    for (float y = -hip; y <= 0.9f * hip + 1e-3f; y += 0.15f) p.pts.Add(Vector3.up * y);
                }
                for (int k = 0; k < HatchPathN; k++) { b.ext[k] = hip; b.hos[k] = Vector3.up * hip; }
                b.crouch = b.path[0];
            }
            b.reach = 0f;
            foreach (var p in b.path) foreach (var q in p.pts) b.reach = Mathf.Max(b.reach, q.magnitude);
            foreach (var q in b.straight.pts) b.reach = Mathf.Max(b.reach, q.magnitude);
            foreach (var q in b.airP.pts) b.reach = Mathf.Max(b.reach, q.magnitude);
            return b;
        }

        /// <summary>Fix round 4 (review: his rig was read again at every click, about 80 Animator samples in the click's frame): his rig's measure
        /// by his figure's make; a figure of the same make (the same controller, avatar, skin, points, size and crouch knobs) takes it from here,
        /// only his own bones looked up (they are his figure's). Proof: HatchBodyCacheHits / HatchBodyCacheReads.</summary>
        static readonly Dictionary<string, HatchBody> HatchBodyCache = new Dictionary<string, HatchBody>();
        public static int HatchBodyCacheHits, HatchBodyCacheReads;
        static string HatchBodyKey(Ghost g, Animator a, SkinnedMeshRenderer sk, HatchBody b)
        {
            var ctl = a ? a.runtimeAnimatorController : null;
            return $"{(ctl ? ctl.GetInstanceID() : 0)}|{(a && a.avatar ? a.avatar.GetInstanceID() : 0)}|{(sk && sk.sharedMesh ? sk.sharedMesh.GetInstanceID() : 0)}|{b.ptA.Count}|{b.bones.Count}|"
                   + $"{g.baseScale:0.00000}|{HatchCrouchFeetLevelM}|{HatchCrouchBalanceM}|{HatchCrouchStandTolM}|{HatchCrouchMinRangeM}|{string.Join(",", HatchCrouchClips)}";
        }
        /// <summary>A measure of the same make into his body (his bones, points' names and kinds stay his figure's own; the poses are shared, read only).</summary>
        static void HatchBodyCopy(HatchBody from, HatchBody to)
        {
            to.push = from.push; to.air = from.air; to.land = from.land; to.ankle0 = from.ankle0; to.landLen = from.landLen; to.ntDeep = from.ntDeep; to.ntTop = from.ntTop;
            to.range = from.range; to.why = from.why; to.kneeOk = from.kneeOk; to.cached = true;
            for (int k = 0; k < HatchPathN; k++) { to.path[k] = from.path[k]; to.ext[k] = from.ext[k]; to.hos[k] = from.hos[k]; }
            for (int k = 0; k < 2; k++) { to.kneeShank[k] = from.kneeShank[k]; to.kneeSide[k] = from.kneeSide[k]; }
            to.crouch = to.path[0]; to.straight = from.straight; to.airP = from.airP;
        }

        /// <summary>vehicle_exit_jump3: the knee row's reference of his rig (his own idle pose, HatchMeasure) handed to his record (the Play check's J1j).</summary>
        static void HatchKneeRef(HatchBody b, HatchJumpRec rec)
        {
            if (b == null || rec == null) return;
            rec.kneeRefOk = b.kneeOk;
            for (int k = 0; k < 2; k++) { rec.kneeShank[k] = b.kneeShank[k]; rec.kneeSide[k] = b.kneeSide[k]; }
        }

        /// <summary>His clip `state` at normalized time `nt`, held there (the Animator evaluated now: this frame shows it).</summary>
        static void HatchSample(Animator a, string state, float nt)
        {
            if (!a || !a.isActiveAndEnabled || string.IsNullOrEmpty(state)) return;
            a.speed = 0f; a.Play(state, 0, Mathf.Clamp01(nt));
            if (a.layerCount > 1) a.SetLayerWeight(1, 0f);                                                  // no upper-body aim layer
            a.Update(0f);
        }

        /// <summary>The kind of a body point of a pose (0 a body point, 1 a hand: HatchMarginFor).</summary>
        static int HatchKind(HatchBody b, int i) => i < b.kind.Count ? b.kind[i] : 0;
        /// <summary>vehicle_exit_jump3: his crouch clip's rise: the segment of its samples (k, k + 1) and the part f between them at which his legs
        /// have straightened `e` m from the clip's deepest frame (ext, rising).</summary>
        static void HatchSeg(HatchBody b, float e, out int k, out float f)
        {
            for (k = 1; k < HatchPathN; k++)
            {
                float e0 = b.ext[k - 1] - b.ext[0], e1 = b.ext[k] - b.ext[0];
                if (e <= e1 + 1e-6f) { f = e1 > e0 + 1e-5f ? Mathf.Clamp01((e - e0) / (e1 - e0)) : 1f; k--; return; }
            }
            k = HatchPathN - 2; f = 1f;
        }
        /// <summary>His crouch clip's normalized time (ntDeep .. ntTop) at which his legs have straightened `e` m from its deepest frame.</summary>
        static float HatchLegsFor(HatchBody b, float e)
        {
            if (e <= 0f) return b.ntDeep;
            HatchSeg(b, e, out int k, out float f);
            return Mathf.Lerp(b.ntDeep, b.ntTop, (k + f) / (HatchPathN - 1));
        }

        // ------------------------------------------------------------------ the push and the arc (vehicle_exit_jump3: from a deep crouch, his soles planted)
        /// <summary>How far his crouch clip straightens his legs (m): his hips over his soles from its deepest frame to standing.</summary>
        static float HatchRiseMax(HatchBody b) => Mathf.Max(0f, b.ext[HatchPathN - 1] - b.ext[0]);
        /// <summary>His hips over his sole point (his own frame, upright) with his legs straightened `e` m from his crouch clip's deepest frame.</summary>
        static Vector3 HatchHoS(HatchBody b, float e)
        {
            HatchSeg(b, Mathf.Clamp(e, 0f, HatchRiseMax(b)), out int k, out float f);
            return Vector3.Lerp(b.hos[k], b.hos[k + 1], f);
        }
        /// <summary>vehicle_exit_jump3: the flight's legs at part u of it (m of straightening): standing at the take-off, tucked HatchAirTuckM in
        /// over the first HatchTuckFrac, straight again over the last (feet first); the same crouch clip.</summary>
        static float HatchAirE(HatchBody b, float u)
        {
            float top = HatchRiseMax(b), tuck = top - Mathf.Min(HatchAirTuckM, top);
            if (u < HatchTuckFrac) return Mathf.Lerp(top, tuck, u / HatchTuckFrac);
            if (u > 1f - HatchTuckFrac) return Mathf.Lerp(tuck, top, (u - (1f - HatchTuckFrac)) / HatchTuckFrac);
            return tuck;
        }
        /// <summary>The jump from his sole point P (planted on the rim) to his hips at the touch (hT) in T s of flight after a tp s push.
        /// vehicle_exit_jump3: the push ends upright with his legs STRAIGHT (standing: hEnd = P + his hips over his soles at the clip's top, fixed),
        /// so v0 = (hT - hEnd + g T^2 / 2 up) / T; the push is his whole figure swung forward about P (the flip axis) from a lean back of `lean` deg
        /// and his legs straightened from `rise` m below standing (his own crouch clip), both as x^2 of the push's time (rest at its start, a
        /// constant rate of change: no kink), solved so that his hips' velocity at the take-off, omega x (hips - P) + d(hips)/d(e) * 2 rise / tp, is
        /// v0 (out and up). need: the crouch asked for (deeper than his clip goes: HatchFitFlight).</summary>
        static HatchArc HatchArcOf(HatchBody b, Vector3 P, Vector3 hT, float T, float tp, Vector3 dir)
        {
            var a = new HatchArc { P = P, hT = hT, T = Mathf.Max(0.05f, T), tp = Mathf.Max(0.02f, tp), dir = dir, b = b };
            var face = HatchFace(dir); var A = FlipAxis(dir); float eMax = HatchRiseMax(b), g = HatchJumpGravity;
            var H = face * HatchHoS(b, eMax); var hEnd = P + H;
            var v0 = (hT - hEnd + Vector3.up * (0.5f * g * a.T * a.T)) / a.T;
            var Jt = Vector3.Cross(A, H);                                                                       // d(hips)/d(swing), per radian
            float e1 = eMax, e0 = Mathf.Max(0f, eMax - 0.02f);
            var Je = e1 > e0 + 1e-5f ? face * (HatchHoS(b, e1) - HatchHoS(b, e0)) / (e1 - e0) : Vector3.up;     // d(hips)/d(straightening) at standing
            float a11 = Vector3.Dot(Jt, dir), a12 = Vector3.Dot(Je, dir), a21 = Jt.y, a22 = Je.y, b1 = Vector3.Dot(v0, dir), b2 = v0.y, det = a11 * a22 - a12 * a21;
            float w, ed; if (Mathf.Abs(det) < 1e-6f) { w = 0f; ed = b2; } else { w = (b1 * a22 - a12 * b2) / det; ed = (a11 * b2 - a21 * b1) / det; }
            a.need = ed * a.tp * 0.5f; a.rise = Mathf.Clamp(a.need, 0f, eMax);
            a.omega = w; a.lean = w * a.tp * 0.5f * Mathf.Rad2Deg; a.hEnd = hEnd; a.v0 = v0;
            a.h0 = P + Quaternion.AngleAxis(-a.lean, A) * (face * HatchHoS(b, eMax - a.rise));
            return a;
        }
        /// <summary>The push t s after its start: his hips, the figure's lean (deg about the flip axis; negative: back) and his legs' straightening
        /// (m from his crouch clip's deepest frame: from rise below standing up to standing).</summary>
        static Vector3 HatchPushAt(HatchArc a, float t, out float lean, out float e)
        {
            float x = Mathf.Clamp01(t / a.tp); lean = -a.lean * (1f - x * x); e = HatchRiseMax(a.b) - a.rise * (1f - x * x);
            return a.P + Quaternion.AngleAxis(lean, FlipAxis(a.dir)) * (HatchFace(a.dir) * HatchHoS(a.b, e));
        }
        /// <summary>His hips t s after the push's start (the planted push, then the flight from hEnd).</summary>
        static Vector3 HatchHipsAt(HatchArc a, float t)
        {
            if (t <= a.tp) return HatchPushAt(a, t, out _, out _);
            float s = t - a.tp; return a.hEnd + a.v0 * s + Vector3.down * (0.5f * HatchJumpGravity * s * s);
        }
        /// <summary>His hips' velocity t s after the push's start.</summary>
        static Vector3 HatchVelAt(HatchArc a, float t)
        {
            if (t > a.tp) return a.v0 + Vector3.down * (HatchJumpGravity * (t - a.tp));
            float t1 = Mathf.Clamp(t, 0.004f, a.tp); return (HatchHipsAt(a, t1) - HatchHipsAt(a, t1 - 0.004f)) / 0.004f;
        }
        /// <summary>The flight whose take-off goes up at `up` m/s from his hips standing on P to hT (the larger root).</summary>
        static float HatchFlightFor(HatchBody b, Vector3 P, Vector3 hT, Vector3 dir, float up)
        {
            float dy = hT.y - (P + HatchFace(dir) * HatchHoS(b, HatchRiseMax(b))).y, g = HatchJumpGravity;
            return Mathf.Max(0.05f, (up + Mathf.Sqrt(Mathf.Max(0f, up * up - 2f * g * dy))) / g);
        }
        /// <summary>The flight for the touch: T, shortened (he waits crouched longer, the touch the same) while the jump asks for a deeper crouch
        /// than his clip has or goes up faster than upMax; the arc returned.</summary>
        static float HatchFitFlight(HatchBody b, Vector3 P, Vector3 hT, float T, float tp, Vector3 dir, float upMax, out HatchArc arc)
        {
            float eMax = HatchRiseMax(b);
            arc = HatchArcOf(b, P, hT, T, tp, dir);
            if (arc.need <= eMax + 1e-4f && arc.v0.y <= upMax + 1e-4f) return T;
            float lo = 0.05f, hi = T;
            for (int i = 0; i < 16; i++)
            {
                float m = 0.5f * (lo + hi); var x = HatchArcOf(b, P, hT, m, tp, dir);
                if (x.need <= eMax && x.v0.y <= upMax) lo = m; else hi = m;
            }
            arc = HatchArcOf(b, P, hT, lo, tp, dir);
            return lo;
        }
        /// <summary>The somersault (deg) s s into the flight: a cubic from 0 deg at the push's swing rate (omega) to 360 deg at rest at
        /// HatchFlipEndFrac of the flight (upright from then on).</summary>
        static float HatchFlipAt(HatchArc a, float s)
        {
            float S = Mathf.Max(0.05f, HatchFlipEndFrac * a.T), x = Mathf.Clamp01(s / S), wd = a.omega * Mathf.Rad2Deg;
            return x >= 1f ? 360f : 360f * (3f * x * x - 2f * x * x * x) + wd * S * (x - 2f * x * x + x * x * x);
        }
        /// <summary>The somersault's axis: horizontal, across the jump (a forward turn: his head over toward the landing).</summary>
        static Vector3 FlipAxis(Vector3 dir) { var a = Vector3.Cross(Vector3.up, dir); return a.sqrMagnitude < 1e-6f ? Vector3.right : a.normalized; }
        /// <summary>His facing for a jump along dir (robot front = -z).</summary>
        static Quaternion HatchFace(Vector3 dir) => Quaternion.LookRotation(-dir, Vector3.up);

        // ------------------------------------------------------------------ the plan of the jump
        /// <summary>The jump's plan: from the rim to free ground on his side of the hull toward his robot's spot. Tried in order: the crouch held
        /// (HatchCrouchHolds: shorter = a longer, higher flight, the same touch), the direction (the line to his spot turned by HatchJumpTurns),
        /// the take-off point on the rim (HatchTakeoffPts), the landing from just outside the footprint (+HatchJumpOutM) out to HatchJumpPastSpotM
        /// past his spot; the first whose crouch, push and arc clear every solid (HatchClear) is taken, else the one whose first cut is the shallowest (fix round 4: each arc's test stops at its first cut, at its first point under HatchJumpClearM once a clear arc is in hand). The take-off goes up at
        /// most HatchJumpMaxUpMps and at most what his legs give in the planted push (HatchFitFlight: a shorter flight, he waits crouched); bail = the jump at once off a destroyed hull (the shortest crouch, an upward
        /// start of HatchJumpMinUpMps, the touch when it lands). Sets st's arc, take-off, touch, landing and the take-off point in the hull's frame;
        /// returns the plan's line.</summary>
        string HatchPlanJump(Rig r, HatchExit st, Vector3 toW, bool bail)
        {
            var rim = HatchRimW(r); var b = st.body;
            var d0 = toW - rim; d0.y = 0f;
            if (d0.sqrMagnitude < 1e-4f) { d0 = r.root.right; d0.y = 0f; }
            d0.Normalize();
            var w = game ? game.World : null; bool path = w != null && w.Path != null;
            if (path && r.obj != null) HatchSideBlocks(w, r.obj); else sideBlocks.Clear();
            var parts = SolveParts(r, out string model);
            float spotM = Vector3.ProjectOnPlane(toW - rim, Vector3.up).magnitude, tp = HatchPushSec, top = HatchRiseMax(b);
            float shortest = HatchCrouchHolds[0]; foreach (var h in HatchCrouchHolds) shortest = Mathf.Min(shortest, h);
            var holds = bail ? new[] { shortest } : HatchCrouchHolds;
            // vehicle_exit_jump3: his take-off points from the hatch's middle AWAY from the lid's hinge (the open lid farthest from his body)
            var away = r.hinge ? Vector3.ProjectOnPlane(rim - r.hinge.position, Vector3.up) : Vector3.zero;
            if (away.sqrMagnitude < 1e-4f) away = d0; away.Normalize(); var across = Vector3.Cross(Vector3.up, away).normalized;
            HatchArc best = null, first = null; float bestClear = float.MinValue, bestHold = holds[0], bestTurn = 0f, bestTake = 0f, bestWait = 0f; Vector3 bestP0 = rim, bestLand = rim; string bestWorst = "";
            float fClear = 0f, fHold = 0f, fTurn = 0f, fTake = 0f, fWait = 0f; Vector3 fP0 = rim, fLand = rim; string fWorst = ""; bool fLate = false;
            bool found = false, late = false; int tries = 0; var clock = System.Diagnostics.Stopwatch.StartNew(); int tests0 = HatchClearTests, skipped0 = HatchClearSkipped;
            bool Out() => tries >= HatchJumpMaxTries || clock.Elapsed.TotalMilliseconds > (first != null ? HatchJumpMaxMs : HatchJumpMaxMsNoClear);   // fix round 4: longer while no clear arc is in hand
            foreach (float hold in holds)
            {
                if (found || Out()) break;
                foreach (float tu in HatchJumpTurns)
                {
                    if (found || Out()) break;
                    var dir = Quaternion.AngleAxis(st.turn0 + tu, Vector3.up) * d0; var face = HatchFace(dir);
                    foreach (var tpt in HatchTakeoffPts)
                    {
                        if (found || Out()) break;
                        var p0 = rim + away * tpt.x + across * tpt.y;                                               // his sole point: planted here through the crouch and the push
                        float dOut = Mathf.Max(HatchJumpLandMinM, HatchFootprintExit(r, p0, dir) + HatchJumpOutM), dMax = Mathf.Max(dOut, HatchJumpLandMaxM);
                        for (float d = dOut; d <= dMax + 1e-3f && !Out(); d += HatchJumpStepM)
                        {
                            var land = Ground(p0 + dir * d);
                            if (path && (!HatchFootFree(w, land) || !HatchWalkFree(w, r, land, toW))) continue;
                            if (!HatchOnSide(r, land, toW)) continue;
                            var hT = land + face * HatchHoS(b, top);                                                // his hips at the touch: his crouch clip standing (the pose drawn there)
                            float takeoff = st.appearAt + hold + tp, T, T0; bool lt = false;
                            if (bail) T0 = Mathf.Min(HatchJumpBailMaxSec, HatchFlightFor(b, p0, hT, dir, HatchJumpMinUpMps));
                            else
                            {
                                T0 = st.touchAt - takeoff;
                                float least = HatchFlightFor(b, p0, hT, dir, 0f);
                                if (T0 < least - 1e-4f) { T0 = least; lt = true; }                                       // he showed too late for the rules' moment (a slip): the touch later
                            }
                            T = HatchFitFlight(b, p0, hT, T0, tp, dir, HatchJumpMaxUpMps, out var a); tries++;         // a deeper crouch than his clip has, or faster up than HatchJumpMaxUpMps: a shorter flight, he waits crouched longer
                            if (!bail && !lt) takeoff = st.touchAt - T;
                            float c = HatchClear(r, parts, b, a, first != null ? HatchJumpClearM : 0f, out string worst);   // fix round 4: an arc stops at its first cut (only a clear one can be taken); with a clear arc in hand, at its first point under HatchJumpClearM
                            if (best == null || c > bestClear) { best = a; bestClear = c; bestHold = hold; bestTurn = st.turn0 + tu; bestTake = takeoff; bestP0 = p0; bestLand = land; bestWorst = worst; late = lt; bestWait = T0 - T; }
                            if (c >= 0f && first == null) { first = a; fClear = c; fHold = hold; fTurn = st.turn0 + tu; fTake = takeoff; fP0 = p0; fLand = land; fWorst = worst; fLate = lt; fWait = T0 - T; }
                            if (c >= HatchJumpClearM) { found = true; break; }                                       // clear by the cut rule and HatchJumpClearM more: taken
                        }
                    }
                }
            }
            bool budgetHit = !found && Out(); if (budgetHit) HatchPlanBudgetHits++;                          // fix round 4: the search cut short by HatchJumpMaxTries / HatchJumpMaxMs (recorded)
            float arcsMs = (float)clock.Elapsed.TotalMilliseconds; int tests = HatchClearTests - tests0, skipped = HatchClearSkipped - skipped0;
            if (!found && first != null)
            {   // vehicle_exit_jump3: nothing clear by the extra HatchJumpClearM: the first arc clear by the cut rule itself (the Play check's)
                found = true; best = first; bestClear = fClear; bestHold = fHold; bestTurn = fTurn; bestTake = fTake; bestP0 = fP0; bestLand = fLand; bestWorst = fWorst; late = fLate; bestWait = fWait;
            }
            if (best == null)
            {   // no free landing at all (it cannot happen beside a spot the rules found free; safety): just outside the footprint toward his robot
                var face = HatchFace(d0); bestP0 = rim + away * 0.15f; bestLand = Ground(bestP0 + d0 * Mathf.Max(HatchJumpLandMinM, HatchFootprintExit(r, bestP0, d0) + HatchJumpOutM)); bestTurn = st.turn0;
                var hT = bestLand + face * HatchHoS(b, top);
                bestTake = st.appearAt + bestHold + tp;
                float T0 = bail ? HatchFlightFor(b, bestP0, hT, d0, HatchJumpMinUpMps) : Mathf.Max(HatchFlightFor(b, bestP0, hT, d0, 0f), st.touchAt - bestTake);
                float T = HatchFitFlight(b, bestP0, hT, T0, tp, d0, HatchJumpMaxUpMps, out best); if (!bail) bestTake = Mathf.Max(bestTake, st.touchAt - T); bestWait = T0 - T;
                bestClear = HatchClear(r, parts, b, best, float.MinValue, out bestWorst);
                found = bestClear >= 0f;
            }
            st.arc = best; st.dir = best.dir; st.p0 = bestP0; st.land = bestLand; st.crouchSec = bestHold;
            st.takeoffAt = bestTake; if (bail || late) st.touchAt = bestTake + best.T;
            if (late && !bail) HatchJumpsLate++;
            st.legsEnd = b.ntTop;
            st.pRimL = r.root ? r.root.InverseTransformPoint(bestP0) : bestP0; st.pRimLSet = r.root && !bail;   // his feet on the DRAWN rim until the push (not after a jump at once: the wreck may tumble)
            var rec = st.rec;
            float vh = new Vector3(best.v0.x, 0f, best.v0.z).magnitude, apex = best.v0.y > 0f ? best.v0.y * best.v0.y / (2f * HatchJumpGravity) : 0f;
            float landM = Vector3.ProjectOnPlane(bestLand - bestP0, Vector3.up).magnitude, drop = bestP0.y - bestLand.y;
            float tuck = Mathf.Min(HatchAirTuckM, top), squat = Mathf.Min(HatchLandDepthM, top);
            if (rec != null)
            {
                rec.appearAt = st.appearAt; rec.takeoffAt = st.takeoffAt; rec.touchAt = st.touchAt; rec.flight = best.T; rec.apexM = apex; rec.landM = landM; rec.turnDeg = bestTurn;
                rec.clearM = bestClear; rec.clearFound = found; rec.late |= late; rec.takeoff = bestP0; rec.landing = bestLand; rec.worst = bestWorst; rec.tries = tries;
                rec.crouchSec = Mathf.Max(0f, st.takeoffAt - tp - st.appearAt); rec.pushSec = tp; rec.vhMps = vh; rec.vy0Mps = best.v0.y; rec.legsAtTakeoff = st.legsEnd;
                rec.leanDeg = best.lean; rec.swingDegps = best.omega * Mathf.Rad2Deg; rec.riseM = best.rise; rec.riseMaxM = top; rec.waitedSec = Mathf.Max(0f, bestWait);
                rec.landDownSec = HatchLandDownSec; rec.landRiseSec = b.landLen; rec.landSec = HatchLandDownSec + b.landLen;
                rec.hEnd = best.hEnd; rec.v0 = best.v0; rec.tuckM = tuck; rec.landDepthM = squat;
                rec.arcsMs = arcsMs; rec.gapTests = tests; rec.gapSkipped = skipped; rec.budgetHit = budgetHit;
            }
            return $"jump {drop:0.00} m down, {landM:0.00} m out ({bestTurn:+0;-0} deg off the line to his robot, {spotM:0.00} m from the rim), from the hatch's middle {Vector3.Dot(bestP0 - rim, away):+0.00;-0.00} m away from the lid's hinge / "
                   + $"{Vector3.Dot(bestP0 - rim, across):+0.00;-0.00} m across; the lid kicked open in {(st.kickAt >= 0f ? HatchKickSec * (1f - st.kickHatch0) : 0f):0.00} s, he shows crouched at {st.appearAt:0.00} s; "
                   + $"crouched {Mathf.Max(0f, st.takeoffAt - tp - st.appearAt):0.00} s, the push {tp:0.00} s, the flight {best.T:0.00} s; "
                   + $"his soles planted on the rim through the crouch and the push: his crouch {best.rise:0.00} m below standing ({b.push} from {HatchLegsFor(b, top - best.rise):0.00} up to {b.ntTop:0.00}; his clip goes {top:0.00} m deep), leaning back {best.lean:0.0} deg, swung upright about his soles in the push (at {best.omega * Mathf.Rad2Deg:0} deg/s at the take-off)"
                   + $"{(bestWait > 1e-3f ? $", the flight {bestWait:0.00} s shorter (a deeper crouch than his clip has or over {HatchJumpMaxUpMps:0.0} m/s up)" : "")}; "
                   + $"take-off {vh:0.00} m/s out and {best.v0.y:0.00} m/s up (his hips' velocity at the push's end: the same), his hips {apex:0.00} m over the take-off at the apex; "
                   + $"one somersault (360 deg from the push's swing rate) upright at {HatchFlipEndFrac:0%} of the flight, his legs tucked {tuck:0.00} m; the touch {(bail ? "when he lands (the jump at once)" : late ? "LATE: after the rules' moment" : "at the rules' moment")}; "
                   + $"the landing {HatchJumpGravity * best.T - best.v0.y:0.0} m/s down: his knees take it {squat:0.00} m deep in {HatchLandDownSec:0.00} s ({b.push}), he stands up in {b.landLen:0.00} s; "
                   + $"his body {(b.ok ? $"read on his rig ({b.bones.Count} bones, {b.names.Count} points with their segments' middles; his crouch clip {b.push}: {b.why}the clips {b.push} / {b.air} / {b.land}, his ankle {b.ankle0:0.00} m over the sole)" : "NOT READ (a column of points)")}; "
                   + $"clear by {(bestClear >= HatchGapExact - 1e-4f ? "at least " : "")}{bestClear:+0.00;-0.00} m over the cut rule ({HatchJumpLimbM:0.00} m from every part, a hand {HatchJumpLimbM + HatchHandExtraM:0.00}){(found ? (bestClear >= HatchJumpClearM ? "" : $" (under the {HatchJumpClearM:0.00} m extra: the first clear one)") : " (NO CLEAR ARC FOUND: the one whose first cut is the shallowest)")} (the closest: {bestWorst}); {tries} arcs tried in {arcsMs:0.0} ms ({tests} gap tests, {skipped} skipped by the bound){(budgetHit ? $" OUT OF THE BUDGET ({HatchJumpMaxTries} arcs / {HatchJumpMaxMs:0} ms, {HatchJumpMaxMsNoClear:0} ms with no clear arc): the best so far" : "")}; the solids: {model}";
        }

        /// <summary>The landing lies on his side of the hull: on each axis where his robot's spot leaves the hull's middle by more than 8 px, the
        /// landing leaves it the same way (the rules' side; the Play check's LandingOk).</summary>
        bool HatchOnSide(Rig r, Vector3 land, Vector3 spotW)
        {
            if (r == null || r.obj == null) return true;
            var lp = ZGame.WorldToPx(land); var sp = ZGame.WorldToPx(spotW);
            int sx = sp.x - r.obj.CenterX, sy = sp.y - r.obj.CenterY, lx = lp.x - r.obj.CenterX, ly = lp.y - r.obj.CenterY;
            return (Math.Abs(sx) <= 8 || Math.Sign(lx) == Math.Sign(sx)) && (Math.Abs(sy) <= 8 || Math.Sign(ly) == Math.Sign(sy));
        }

        /// <summary>The least clearance (m; negative: a cut) of his body along one jump, by the ONE rule of the plan and the Play check's cut count
        /// (vehicle_exit_jump3, HatchMarginFor): every body point (every bone of his skin and every bone segment's middle, read on his rig) of the
        /// pose drawn then (his crouch clip at the crouch's depth, along the push, the flight's tuck) turned as drawn (in the crouch and the push
        /// the lean about his planted soles, in the air the somersault about his hips), against every solid (HatchSolid.Gap) less its margin
        /// (HatchJumpLimbM, a hand HatchHandExtraM more): the crouch, the push (six samples) and the flight (every 1/60 s); the lid as it still
        /// swings for the crouch and the push; points in the hatch's opening are skipped. Stops once below `need`. A crouch leaning back more
        /// than HatchLeanMaxDeg counts as not clear. `worst`: the closest point and part. Fix round 4 (review: 47 points a pose made one plan
        /// too slow for its budget): at most HatchGapExact is reported (Gap is exact only up to there; every decision of the plan lies below
        /// it), and a point's test against a part is skipped while a lower bound of its distance stays over its margin + the least found (the
        /// least distance at its last test, HatchSolid.Gap's `low`, less the way the point has moved since: a point cannot come nearer than it
        /// has moved; Gap itself is never below `low`). The result is the same as testing every point (min(that, HatchGapExact)), with a
        /// fraction of the tests (HatchClearTests / HatchClearSkipped; tools/budget_harness.py checks it on every modelled arc).</summary>
        float HatchClear(Rig r, List<HatchSolid> parts, HatchBody b, HatchArc a, float need, out string worst)
            => HatchClearCore(HatchRimW(r), r.root ? r.root.up : Vector3.up, parts, b, a, need, out worst);

        /// <summary>HatchClear's buffers (reused: no garbage in the plan): each point's position at the last sample and the way it has moved since
        /// the first; per part and point, the least distance at its last test (HatchSolid.Gap's `low`) and the point's way then.</summary>
        static float[] hcLast = new float[0], hcWayAt = new float[0], hcWay = new float[0]; static Vector3[] hcPos = new Vector3[0];

        static float HatchClearCore(Vector3 rim, Vector3 hullUp, List<HatchSolid> parts, HatchBody b, HatchArc a, float need, out string worst)
        {
            worst = "";
            var face = HatchFace(a.dir); var A = FlipAxis(a.dir);
            float least = HatchGapExact, top = HatchRiseMax(b);
            if (Mathf.Abs(a.lean) > HatchLeanMaxDeg) { worst = $"the crouch's lean back {a.lean:0.0} deg (at most {HatchLeanMaxDeg:0})"; return -0.001f - 0.01f * (Mathf.Abs(a.lean) - HatchLeanMaxDeg); }
            int np = 0; foreach (var p in b.path) np = Mathf.Max(np, p.pts.Count);
            int nParts = parts.Count, cells = nParts * np;
            if (hcLast.Length < cells) { hcLast = new float[cells]; hcWayAt = new float[cells]; }
            if (hcWay.Length < np) { hcWay = new float[np]; hcPos = new Vector3[np]; }
            for (int x = 0; x < cells; x++) hcLast[x] = float.NegativeInfinity;                              // not tested yet
            for (int k = 0; k < np; k++) hcWay[k] = 0f;
            int wk = -1, wp = -1; string wphase = ""; bool firstSample = true;
            int N = Mathf.Max(8, Mathf.CeilToInt(a.T * 60f));
            for (int i = -7; i <= N; i++)
            {
                Vector3 hips; Quaternion rot = face; float e; bool rimPart = i < 0; string phase;
                if (i == -7) { hips = a.h0; rot = Quaternion.AngleAxis(-a.lean, A) * face; e = top - a.rise; phase = "crouch"; }           // leaning back over his planted soles
                else if (i < 0) { float t = a.tp * (i + 7) / 6f; hips = HatchPushAt(a, t, out float ln, out e); rot = Quaternion.AngleAxis(ln, A) * face; phase = "push"; }
                else
                {
                    float s = a.T * i / N; hips = HatchHipsAt(a, a.tp + s);
                    rot = Quaternion.AngleAxis(HatchFlipAt(a, s), A) * face; e = HatchAirE(b, s / a.T); phase = "air";
                }
                HatchSeg(b, e, out int pk, out float pf); var P0 = b.path[pk].pts; var P1 = b.path[pk + 1].pts; int n = Mathf.Min(P0.Count, P1.Count);
                for (int k = 0; k < n; k++)
                {   // every point where it is drawn now, and the way it has come since the last sample (the bound's)
                    var q = hips + rot * Vector3.LerpUnclamped(P0[k], P1[k], pf);
                    if (!firstSample) hcWay[k] += (q - hcPos[k]).magnitude;
                    hcPos[k] = q;
                }
                firstSample = false;
                for (int pi = 0; pi < nParts; pi++)
                {
                    var pt = parts[pi];
                    if (pt.pushOnly && !rimPart) continue;                                                       // the lid as it still swings: the crouch and the push only
                    float reachP = pt.rad + b.reach + HatchJumpLimbM + HatchHandExtraM + 0.3f; if ((pt.centre - hips).sqrMagnitude > reachP * reachP) continue;
                    int row = pi * np;
                    for (int k = 0; k < n; k++)
                    {
                        float m = HatchJumpLimbM + (HatchKind(b, k) == 1 ? HatchHandExtraM : 0f);
                        float bound = hcLast[row + k] - (hcWay[k] - hcWayAt[row + k]);                          // it cannot be nearer than this now
                        if (bound > 0f && bound - m >= least + 1e-4f) { HatchClearSkipped++; continue; }
                        var q = hcPos[k];
                        if (InHole(rim, hullUp, q)) continue;
                        float raw = pt.Gap(q, m + HatchGapExact, out float low); HatchClearTests++;
                        hcLast[row + k] = low; hcWayAt[row + k] = hcWay[k];                                     // its least distance (Gap's `low`) and the way then
                        float gap = raw - m;
                        if (gap < least)
                        {
                            least = gap; wk = k; wp = pi; wphase = phase;
                            if (least < need) { worst = HatchWorstText(b, parts, wk, wp, least, wphase); return least; }
                        }
                    }
                }
            }
            worst = wk >= 0 ? HatchWorstText(b, parts, wk, wp, least, wphase) : $"nothing nearer than {HatchGapExact:0.00} m over the margin";
            return least;
        }

        /// <summary>The plan's line on the closest point of a jump (written once, not at every nearer point found).</summary>
        static string HatchWorstText(HatchBody b, List<HatchSolid> parts, int k, int pi, float gap, string phase)
        {
            var pt = parts[pi];
            return $"{(k < b.names.Count ? b.names[k] : "a body point")} {gap:+0.00;-0.00} m off {(pt.world ? "a building / flag's " : pt.lid ? "the lid's " : "")}{pt.name} in the {phase}";
        }

        /// <summary>The solids the jump is tested against: every drawn part of the vehicle (HatchVehicleRenderers) with its transform's matrix
        /// now, the lid's parts at their OPEN pose and, for the crouch and the push only, as the lid stands when he shows (its last swing); the
        /// buildings and flags near (HatchNearRenderers). `model`: the counts.</summary>
        List<HatchSolid> SolveParts(Rig r, out string model)
        {
            var list = new List<HatchSolid>(); var rs = new List<Renderer>();
            int cols = 0, boxes = 0, near = 0;
            Matrix4x4 lidOpen = Matrix4x4.identity, lidLate = Matrix4x4.identity; bool lidM = false;
            if (r.hinge)
            {
                var pm = r.hinge.parent ? r.hinge.parent.localToWorldMatrix : Matrix4x4.identity;
                float atShow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(HatchKickShowFrac));                             // vehicle_exit_jump3: the kicked lid as he shows
                lidOpen = pm * Matrix4x4.TRS(r.hinge.localPosition, r.hingeRest * Quaternion.AngleAxis(r.hingeSign * hatchOpenDeg, Vector3.right), r.hinge.localScale) * r.hinge.worldToLocalMatrix;
                lidLate = pm * Matrix4x4.TRS(r.hinge.localPosition, r.hingeRest * Quaternion.AngleAxis(r.hingeSign * hatchOpenDeg * atShow, Vector3.right), r.hinge.localScale) * r.hinge.worldToLocalMatrix;
                lidM = true;
            }
            HatchVehicleRenderers(r, rs);
            foreach (var rr in rs)
            {
                var s = HatchSolid.Of(rr); if (s == null) continue;
                s.lid = r.hinge && rr.transform.IsChildOf(r.hinge);
                s.Use(s.lid && lidM ? lidOpen * s.Basis() : s.Basis());
                if (s.boxModel) boxes++; else cols++;
                list.Add(s);
                if (s.lid && lidM) { var s2 = HatchSolid.Of(rr); if (s2 != null) { s2.lid = true; s2.pushOnly = true; s2.Use(lidLate * s2.Basis()); list.Add(s2); } }
            }
            rs.Clear(); HatchNearRenderers(r, rs);
            foreach (var rr in rs) { var s = HatchSolid.Of(rr); if (s == null) continue; s.world = true; s.Use(s.Basis()); list.Add(s); near++; if (s.boxModel) boxes++; else cols++; }
            model = $"{list.Count - near} vehicle solids (the lid twice: open and as he shows) and {near} parts of buildings / flags near, {cols} read as mesh columns, {boxes} as boxes";
            return list;
        }

        /// <summary>How far (m) along the flat `dir` from p0 the hull's box (and the turret's) ends.</summary>
        float HatchFootprintExit(Rig r, Vector3 p0, Vector3 dir)
        {
            float best = RayExit(r.root, HullOnlyBox(r), p0, dir);
            if (r.turret && TurretOnlyBox(r, out var tb)) best = Mathf.Max(best, RayExit(r.turret, tb, p0, dir));
            return best;
        }

        /// <summary>The distance (world m) from p along the flat dir until it leaves the box's x / z extent (the box in t's space); 0 when outside.</summary>
        static float RayExit(Transform t, Bounds b, Vector3 p, Vector3 dir)
        {
            var lp = t.InverseTransformPoint(p); var ld = t.InverseTransformDirection(dir);
            float k = ld.magnitude; if (k < 1e-5f) return 0f;
            float exit = float.MaxValue;
            for (int ax = 0; ax < 3; ax += 2)
            {
                float o = ax == 0 ? lp.x : lp.z, v = ax == 0 ? ld.x : ld.z, lo = ax == 0 ? b.min.x : b.min.z, hi = ax == 0 ? b.max.x : b.max.z;
                if (Mathf.Abs(v) < 1e-6f) { if (o < lo || o > hi) return 0f; continue; }
                exit = Mathf.Min(exit, ((v > 0f ? hi : lo) - o) / v);
            }
            if (exit == float.MaxValue || exit < 0f) return 0f;
            return exit / k;
        }

        /// <summary>Every drawn renderer of the vehicle: no particles, trails or lines, not the commander figure, not a flat thing at the ground
        /// (under 0.3 m above the hull's root), not a shadows-only one.</summary>
        void HatchVehicleRenderers(Rig r, List<Renderer> into)
        {
            if (r == null || !r.root) return;
            float floor = r.root.position.y + 0.3f;
            foreach (var rr in r.root.GetComponentsInChildren<Renderer>(true))
            {
                if (rr is ParticleSystemRenderer || rr is TrailRenderer || rr is LineRenderer || (r.crew && rr.transform.IsChildOf(r.crew.transform))) continue;
                if (!rr.enabled || !rr.gameObject.activeInHierarchy || rr.bounds.max.y < floor || rr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                into.Add(rr);
            }
        }
        /// <summary>The drawn renderers of the buildings (no bridge) and flags whose box comes within 12 m of the hull's middle.</summary>
        void HatchNearRenderers(Rig r, List<Renderer> into)
        {
            var w = game ? game.World : null; if (w == null || r == null || r.obj == null) return;
            const int reach = 96;                                                                                 // 12 m in px
            foreach (var b in w.Objects)
            {
                bool flag = b.Kind == ZKind.MapItem && b.Type == "flag", bld = b.Kind == ZKind.Building && !b.IsBridge;
                if ((!flag && !bld) || b.Destroyed) continue;
                if (b.LocX > r.obj.CenterX + reach || b.LocX + b.WidthPix < r.obj.CenterX - reach || b.LocY > r.obj.CenterY + reach || b.LocY + b.HeightPix < r.obj.CenterY - reach) continue;
                GameObject go = null;
                if (game.StaticViews.TryGetValue(b.Id, out var sv) && sv) go = sv; else if (game.Views.TryGetValue(b.Id, out var uv) && uv) go = uv.gameObject;
                if (!go) continue;
                foreach (var rr in go.GetComponentsInChildren<Renderer>())
                    if (!(rr is ParticleSystemRenderer || rr is TrailRenderer || rr is LineRenderer) && rr.enabled && rr.gameObject.activeInHierarchy && rr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) into.Add(rr);
            }
        }

        /// <summary>The point lies in the hatch's opening (within HatchHoleM of the hatch's middle across the hull, from HatchHoleDownM below the
        /// rim up to HatchHoleUpM above it): nothing solid there (his feet on the rim).</summary>
        static bool InHole(Vector3 rim, Vector3 up, Vector3 p)
        {
            float h = Vector3.Dot(p - rim, up);
            return Vector3.ProjectOnPlane(p - rim, up).sqrMagnitude < HatchHoleM * HatchHoleM && h >= -HatchHoleDownM && h < HatchHoleUpM;
        }

        /// <summary>The distance (world m) from the point to a box (lp in the box's space, its axes scaled by sc); negative inside (the shallowest way out).</summary>
        static float BoxGap(Vector3 lp, Bounds b, Vector3 sc)
        {
            float dx = Mathf.Max(b.min.x - lp.x, lp.x - b.max.x, 0f) * sc.x, dy = Mathf.Max(b.min.y - lp.y, lp.y - b.max.y, 0f) * sc.y, dz = Mathf.Max(b.min.z - lp.z, lp.z - b.max.z, 0f) * sc.z;
            if (dx > 0f || dy > 0f || dz > 0f) return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            return -Mathf.Min(Mathf.Min(lp.x - b.min.x, b.max.x - lp.x) * sc.x, Mathf.Min(Mathf.Min(lp.y - b.min.y, b.max.y - lp.y) * sc.y, Mathf.Min(lp.z - b.min.z, b.max.z - lp.z) * sc.z));
        }

        /// <summary>
        /// One solid of the vehicle or of a building / flag near it, for the jump's plan and the Play check's cut count. Kept from the first jump
        /// kit (measured, vehicle_exit_jump2: a plain renderer box is no model here: the heavy's hatch rim sits level with its turret's box top
        /// and the mech's rim 0.2 m under its body's box top, so a box would put him inside it on his own rim). A readable mesh is read as
        /// COLUMNS: its own x / z cut into cells of HatchSolidCellM, each holding the lowest and the highest point of its triangles over it; built
        /// once per mesh. Anything else (a skinned mesh, one that cannot be read): its box (boxModel). Tested with a matrix (Use).
        /// </summary>
        public sealed class HatchSolid
        {
            public Renderer rr; public Transform t; public string name = ""; public bool lid, boxModel, world, pushOnly;
            /// <summary>Its local box (the mesh's), the matrix in use and its inverse, the axes' world scale, its middle (world) and radius (m).</summary>
            public Bounds b; public Matrix4x4 l2w = Matrix4x4.identity, w2l = Matrix4x4.identity; public Vector3 sc = Vector3.one, centre; public float rad;
            Cols cols;
            sealed class Cols { public float x0, z0, cell; public int nx, nz; public float[] lo, hi; }
            static readonly Dictionary<Mesh, Cols> cache = new Dictionary<Mesh, Cols>();
            /// <summary>Proof: solids built as columns / as boxes, meshes read.</summary>
            public static int Built, BoxesBuilt, MeshesRead;

            /// <summary>The solid of a renderer (null: no mesh).</summary>
            public static HatchSolid Of(Renderer rr)
            {
                if (!rr) return null;
                var s = new HatchSolid { rr = rr, name = rr.name };
                if (rr is SkinnedMeshRenderer sk) { s.b = sk.localBounds; s.t = sk.rootBone ? sk.rootBone : sk.transform; s.boxModel = true; }
                else
                {
                    var mf = rr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return null;
                    var m = mf.sharedMesh; s.b = m.bounds; s.t = rr.transform;
                    s.cols = ColsOf(m, rr.transform); s.boxModel = s.cols == null;
                }
                if (s.b.size.sqrMagnitude < 1e-8f) return null;
                if (s.boxModel) BoxesBuilt++; else Built++;
                s.Use(s.Basis());
                return s;
            }

            /// <summary>Its transform's matrix now (identity: none).</summary>
            public Matrix4x4 Basis() => t ? t.localToWorldMatrix : Matrix4x4.identity;
            /// <summary>Tests it with this matrix from now on.</summary>
            public void Use(Matrix4x4 m)
            {
                l2w = m; w2l = m.inverse;
                sc = new Vector3(new Vector3(m.m00, m.m10, m.m20).magnitude, new Vector3(m.m01, m.m11, m.m21).magnitude, new Vector3(m.m02, m.m12, m.m22).magnitude);
                centre = m.MultiplyPoint3x4(b.center); rad = Vector3.Scale(b.extents, sc).magnitude;
            }
            /// <summary>A world point in its space.</summary>
            public Vector3 ToLocal(Vector3 p) => w2l.MultiplyPoint3x4(p);

            static Cols ColsOf(Mesh m, Transform t)
            {
                if (cache.TryGetValue(m, out var c)) return c;
                if (cache.Count > 96) cache.Clear();
                c = null;
                try
                {
                    if (m.isReadable && m.vertexCount > 0)
                    {
                        var v = m.vertices; var tri = m.triangles;
                        if (v.Length > 0 && tri.Length >= 3 && tri.Length <= 180000)
                        {
                            var ls = t ? t.lossyScale : Vector3.one; float s = Mathf.Max(1e-4f, 0.5f * (Mathf.Abs(ls.x) + Mathf.Abs(ls.z)));
                            var bb = m.bounds; float cell = HatchSolidCellM / s;
                            int nx = Mathf.Max(1, Mathf.CeilToInt(bb.size.x / cell) + 1), nz = Mathf.Max(1, Mathf.CeilToInt(bb.size.z / cell) + 1);
                            while (nx * nz > 40000) { cell *= 1.5f; nx = Mathf.Max(1, Mathf.CeilToInt(bb.size.x / cell) + 1); nz = Mathf.Max(1, Mathf.CeilToInt(bb.size.z / cell) + 1); }
                            c = new Cols { x0 = bb.min.x, z0 = bb.min.z, cell = cell, nx = nx, nz = nz, lo = new float[nx * nz], hi = new float[nx * nz] };
                            for (int i = 0; i < c.lo.Length; i++) { c.lo[i] = float.MaxValue; c.hi[i] = float.MinValue; }
                            float step = 0.5f * cell;
                            for (int k = 0; k + 2 < tri.Length; k += 3)
                            {
                                Vector3 A = v[tri[k]], B = v[tri[k + 1]], C = v[tri[k + 2]];
                                float e = Mathf.Max((B - A).magnitude, Mathf.Max((C - B).magnitude, (A - C).magnitude));
                                int n = Mathf.Clamp(Mathf.CeilToInt(e / step), 1, 40);
                                for (int i = 0; i <= n; i++)
                                    for (int j = 0; i + j <= n; j++)
                                    {
                                        float u = i / (float)n, w = j / (float)n;
                                        var p = A + (B - A) * u + (C - A) * w;
                                        int ix = Mathf.Clamp((int)((p.x - c.x0) / cell), 0, nx - 1), iz = Mathf.Clamp((int)((p.z - c.z0) / cell), 0, nz - 1), at = iz * nx + ix;
                                        if (p.y < c.lo[at]) c.lo[at] = p.y;
                                        if (p.y > c.hi[at]) c.hi[at] = p.y;
                                    }
                            }
                            MeshesRead++;
                        }
                    }
                }
                catch (Exception) { c = null; }
                cache[m] = c;
                return c;
            }

            /// <summary>The distance (world m) from the world point to this solid, exact up to `R` (farther: at least R); negative inside a column or
            /// the box (its depth along the column).</summary>
            public float Gap(Vector3 p, float R) => Gap(p, R, out _);
            /// <summary>Gap, and `low`: a distance this solid's columns are at least that far (m) from the point, which a point moved d m can
            /// lower by at most d (fix round 4, HatchClear's bound). The columns' last cells reach up to one cell past the mesh's box on its
            /// +x / +z sides, so beyond R (where Gap answers with the box) `low` is the box's distance less that overhang; within R it is the
            /// columns' own distance (at most R).</summary>
            public float Gap(Vector3 p, float R, out float low)
            {
                var lp = w2l.MultiplyPoint3x4(p);
                float box = BoxGap(lp, b, sc);
                if (cols == null) { low = box; return box; }
                if (box > R) { low = box - cols.cell * Mathf.Sqrt(sc.x * sc.x + sc.z * sc.z); return box; }
                var c = cols;
                float rx = R / Mathf.Max(1e-5f, sc.x), rz = R / Mathf.Max(1e-5f, sc.z);
                int i0 = Mathf.Max(0, (int)Mathf.Floor((lp.x - rx - c.x0) / c.cell)), i1 = Mathf.Min(c.nx - 1, (int)Mathf.Floor((lp.x + rx - c.x0) / c.cell));
                int j0 = Mathf.Max(0, (int)Mathf.Floor((lp.z - rz - c.z0) / c.cell)), j1 = Mathf.Min(c.nz - 1, (int)Mathf.Floor((lp.z + rz - c.z0) / c.cell));
                float best = float.MaxValue;
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int at = j * c.nx + i; float lo = c.lo[at], hi = c.hi[at]; if (lo > hi) continue;
                        float xa = c.x0 + i * c.cell, za = c.z0 + j * c.cell;
                        float dx = Mathf.Max(xa - lp.x, lp.x - (xa + c.cell), 0f) * sc.x, dz = Mathf.Max(za - lp.z, lp.z - (za + c.cell), 0f) * sc.z, dy = Mathf.Max(lo - lp.y, lp.y - hi, 0f) * sc.y;
                        float d = dx > 0f || dy > 0f || dz > 0f ? Mathf.Sqrt(dx * dx + dy * dy + dz * dz) : -Mathf.Min(lp.y - lo, hi - lp.y) * sc.y;
                        if (d < best) best = d;
                    }
                float gap = best == float.MaxValue ? Mathf.Max(R, box) : best;
                low = Mathf.Min(gap, R);
                return gap;
            }
        }

        // ------------------------------------------------------------------ per frame
        /// <summary>StepGhost, every frame (dt = this frame's game time). An exit figure: inside while the lid opens, the crouch on the rim, the
        /// push, the arc, the touch and the stand-up, all placed here (true: StepGhost stops here this frame); then the walk on, played by
        /// StepGhost from the keys (false). A boarding figure held while a driver is still on the same hull: HatchEnterHold.</summary>
        bool HatchExitStep(Ghost g, Rig r, float dt)
        {
            if (g == null) return false;
            if (g.enter) return HatchEnterHold(g, r, dt);
            if (hatchExits.Count == 0 || !hatchExits.TryGetValue(g, out var st)) return false;
            float step = dt;
            if (step <= 0f && game && game.World != null && game.World.GameOver && st.phase < JumpPhase.Walk) step = Time.unscaledDeltaTime;   // the game over with its clock stopped: the jump ends on the real clock
            if (HatchThrowStep(g, r, st, step)) return true;                                               // 1.5 DV179 (kit exit_leap_death): the hull destroyed before his take-off: thrown by its blast, as the rules hold him (View/BoardingView.HatchThrow.cs); not thrown: 1.4's steps below
            if (st.dead)
            {   // his robot died while this figure is drawn (HatchExitDeath put his own model where the figure is): the figure goes on until that
                // model's death plays (UnitView.Die -> HatchExitDies shows the model in its place and StepGhost ends this walk); safety: 3 s
                st.deadFor += step;
                if ((g.live && OpenZEnding.Holds(g.robotId)) || st.deadFor > 3f) { EndGhost(g, true); return true; }
                if (st.phase == JumpPhase.Walk) { HatchDeadFollow(g); return true; }
            }
            if (st.phase == JumpPhase.Walk) { if (!st.dead) OzExitLegIK.Mark(g.anim, g.go ? g.go.transform : null); HatchWalkSteps(g, st); return false; }   // 1.5 N2: his legs on the clips' foot goals on the walk on (View/OzExitLegIK.cs)
            if (!st.dead) HatchBailCheck(g, r, st);
            st.tau += step;
            st.lastStep = step;
            bool held = HatchJumpFrame(g, r, st);
            if (st.dead) HatchDeadFollow(g);
            return held;
        }

        bool HatchJumpFrame(Ghost g, Rig r, HatchExit st)
        {
            bool hullGone = r == null || r.obj == null || !r.view || r.view.Dying || r.obj.Destroyed || r.obj.KillAt >= 0;
            if (hullGone && !st.lidShut && st.phase >= JumpPhase.Air) HatchLidShut(r, st);                       // the wreck keeps no open lid
            var b = st.body; var face = HatchFace(st.dir);
            switch (st.phase)
            {
                case JumpPhase.Inside:
                {
                    if (g.go && g.go.activeSelf) g.go.SetActive(false);
                    if (r != null && !hullGone) r.hatchWant = true;
                    bool cmd = r != null && r.crew && r.crew.activeSelf && r.crewPop > 0.001f;
                    bool cmdGoes = cmd && r.crewPop - Time.deltaTime * SimRate / Mathf.Max(0.01f, commanderSinkTime) <= 0.001f;   // StepRig hides him later in THIS frame
                    if (st.cmdUp && (!cmd || cmdGoes) && st.rec != null && st.rec.commanderGoneFrame < 0) st.rec.commanderGoneFrame = Time.frameCount;
                    bool ready = st.bailed || (st.cmdUp ? !cmd || cmdGoes : st.tau >= st.appearAt);
                    if (!ready && st.tau < st.appearAt + 0.5f) return true;                                         // the lid opening
                    if (cmd && !cmdGoes && !st.bailed && st.tau < st.appearAt + 0.5f) return true;                  // never two of him in the hatch
                    if (r != null && HatchOtherOnHull(r, g) && st.tau < st.appearAt + 2f) return true;             // never two figures on the rim
                    st.phase = JumpPhase.Crouch; st.shownAt = st.tau;
                    if (st.rec != null) st.rec.shownFrame = Time.frameCount;
                    float slipTol = Mathf.Clamp(st.lastStep + HatchSlipFrameExtraSec, HatchSlipMinSec, Mathf.Max(HatchSlipMinSec, HatchSlipMaxTolSec));   // fix round 4: shown in the first frame at or past its moment is no slip (at ~30 fps that frame comes up to 0.033 s late)
                    if (st.rec != null) st.rec.showLateSec = st.tau - st.appearAt;
                    if (st.tau > st.appearAt + slipTol && r != null && !st.bailed) HatchSlip(g, r, st);            // the show slipped: planned again for the rules' touch
                    if (g.go) { g.go.SetActive(true); g.clipNow = null; }
                    if (g.anim) { g.anim.applyRootMotion = false; g.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
                    goto case JumpPhase.Crouch;
                }
                case JumpPhase.Crouch:
                {
                    if (r != null && !hullGone) r.hatchWant = true;
                    var p = HatchTakeoffW(r, st, hullGone);                                                         // his soles on the DRAWN rim
                    HatchPlace(g, st, b.push, HatchLegsFor(b, HatchRiseMax(b) - st.arc.rise), face, null, p, -st.arc.lean);   // vehicle_exit_jump3: his own crouch clip as deep as the push needs, leaning back over his planted soles
                    st.markPos = p;
                    if (st.tau < st.takeoffAt - st.arc.tp) return true;
                    // the push starts: solved again from his soles where the drawn rim holds them now (the hull may have stopped a few cm off the plan), the same touch
                    st.arc = HatchArcOf(b, p, st.arc.hT, st.arc.T, st.arc.tp, st.dir);
                    st.legsEnd = b.ntTop;
                    if (st.rec != null) { st.rec.vy0Mps = st.arc.v0.y; st.rec.vhMps = new Vector3(st.arc.v0.x, 0f, st.arc.v0.z).magnitude; st.rec.legsAtTakeoff = st.legsEnd; st.rec.leanDeg = st.arc.lean; st.rec.swingDegps = st.arc.omega * Mathf.Rad2Deg; st.rec.riseM = st.arc.rise; st.rec.hEnd = st.arc.hEnd; st.rec.v0 = st.arc.v0; st.rec.apexM = st.arc.v0.y > 0f ? st.arc.v0.y * st.arc.v0.y / (2f * HatchJumpGravity) : 0f; }
                    st.phase = JumpPhase.Push;
                    goto case JumpPhase.Push;
                }
                case JumpPhase.Push:
                {
                    if (r != null && !hullGone) r.hatchWant = true;
                    float t = st.tau - (st.takeoffAt - st.arc.tp);
                    if (t >= st.arc.tp)
                    {
                        st.phase = JumpPhase.Air; HatchJumpsTaken++;
                        var at = HatchHipsAt(st.arc, st.arc.tp);
                        bool a1 = Voice("rob_step_metal", st.p0, st.robotId, HatchTakeoffGain), a2 = Voice("dst_ragdoll_whoosh", at, st.robotId, HatchWhooshGain);
                        if (a1 || a2) HatchTakeoffSounds++;
                        if (st.rec != null) { st.rec.takeoffFrame = Time.frameCount; st.rec.takeoffSound = a1 || a2; if (a1 || a2) st.rec.takeoffSoundFrame = Time.frameCount; st.rec.takeoff = st.p0; }
                        st.lastFlip = 0f;
                        goto case JumpPhase.Air;
                    }
                    // review round 2 (his soles slid along the rim): his soles PLANTED on the drawn rim; his whole figure swung about them toward upright
                    // and jump_takeoff straightening his legs, so his hips reach the arc's take-off velocity at the push's end
                    var sole = HatchTakeoffW(r, st, hullGone);
                    HatchPushAt(st.arc, t, out float lean, out float ext);
                    HatchPlace(g, st, b.push, HatchLegsFor(b, ext), face, null, sole, lean);                       // vehicle_exit_jump3: his crouch clip straightening to standing
                    st.markPos = sole;
                    return true;
                }
                case JumpPhase.Air:
                {
                    float s = st.tau - st.takeoffAt;
                    if (r != null && s < HatchJumpLidKeepSec && !hullGone) r.hatchWant = true;
                    if (s >= st.arc.T)
                    {   // the touch: feet on the ground, upright
                        st.phase = JumpPhase.Land;
                        HatchExitLandings++;
                        bool snd = Voice("rob_land", st.land, st.robotId >= 0 ? st.robotId : st.vehicleId, 1f);
                        HatchLandFrame(g, st, 0f);
                        if (st.rec != null)
                        {
                            st.rec.touchTau = st.tau; st.rec.touchFrame = Time.frameCount; st.rec.touchSim = game && game.World != null ? game.World.Time : -1;
                            st.rec.landSound = snd; if (snd) st.rec.landSoundFrame = Time.frameCount;
                            if (g.go) { st.rec.tiltAtTouchDeg = Vector3.Angle(g.go.transform.up, Vector3.up); st.rec.feetAboveAtTouch = g.go.transform.position.y - Ground(g.go.transform.position).y; }
                        }
                        return true;
                    }
                    var a = st.arc; float u = s / a.T, ang = HatchFlipAt(a, s);
                    if (st.rec != null) st.rec.flipDeg += Mathf.Abs(ang - st.lastFlip);
                    st.lastFlip = ang;
                    var rot = Quaternion.AngleAxis(ang, FlipAxis(a.dir)) * face;                                    // a forward somersault about his hips: his head over toward the landing
                    // vehicle_exit_jump3: his legs tuck in (his own crouch clip, HatchAirTuckM) over the first HatchTuckFrac and straighten over the last (feet first)
                    var hips = HatchHipsAt(a, a.tp + s);
                    HatchPlace(g, st, b.push, HatchLegsFor(b, HatchAirE(b, u)), rot, hips, null);
                    st.markPos = hips - Vector3.up * (b.straight.hipsL.y - b.straight.soleL.y);
                    return true;
                }
                case JumpPhase.Land:
                {
                    float tl = st.tau - st.takeoffAt - st.arc.T;
                    if (tl < HatchLandDownSec + b.landLen) { HatchLandFrame(g, st, tl); return true; }
                    if (!st.gunUp) { HatchGunUp(g, st, Mathf.Max(tl, HatchLandDownSec)); HatchGunsLate++; if (st.rec != null) st.rec.gunLate = true; }   // vehicle_exit_gun review 2: one long frame (4x game speed, a hitch) stepped past his whole stand-up: his gun comes back in this frame, never left to the walk's old 86 % rule
                    st.phase = JumpPhase.Walk;                                                                      // he stands: the walk on (StepGhost from the keys)
                    if (st.rec != null) st.rec.standFrame = Time.frameCount;
                    if (g.anim) g.anim.speed = 1f;
                    g.time = 0f; g.clipNow = null; st.markPos = st.land;
                    return false;
                }
            }
            return false;
        }

        /// <summary>tl s after the touch: his knees take the impact (vehicle_exit_jump3: his own crouch clip down HatchLandDepthM in HatchLandDownSec,
        /// fast at first), then he stands up on the same clip (smoothstep, HatchLandRiseSec); his feet planted on the landing.</summary>
        void HatchLandFrame(Ghost g, HatchExit st, float tl)
        {
            var b = st.body; var face = HatchFace(st.dir); float top = HatchRiseMax(b), depth = Mathf.Min(HatchLandDepthM, top), e;
            if (tl < HatchLandDownSec) { float x = Mathf.Clamp01(tl / HatchLandDownSec); e = top - depth * (1f - (1f - x) * (1f - x)); }
            else { float x = Mathf.Clamp01((tl - HatchLandDownSec) / Mathf.Max(0.05f, b.landLen)); e = top - depth * (1f - x * x * (3f - 2f * x)); }
            if (!st.gunUp && tl >= HatchLandDownSec) HatchGunUp(g, st, tl);                                    // vehicle_exit_gun: the squat's bottom: his gun in his hand again
            float w = st.gunUp && HatchGunBlendOn ? HatchGunBlend(tl - st.gunUpTl) : 0f;
            HatchPlace(g, st, b.push, HatchLegsFor(b, e), face, null, st.land, 0f, w > 0f ? "idle" : null, w);  // vehicle_exit_gun: the squat blending into his armed idle (his soles planted)
            st.markPos = st.land;
        }

        /// <summary>vehicle_exit_gun: the share of his armed idle in his pose `s` s after his gun came back (smoothstep over HatchGunUpSec).</summary>
        static float HatchGunBlend(float s) { float x = Mathf.Clamp01(s / Mathf.Max(0.02f, HatchGunUpSec)); return x * x * (3f - 2f * x); }

        /// <summary>vehicle_exit_gun: the squat's bottom after the landing: his OWN gun (the figure's weapon object, under his right palm socket: in
        /// his hand from this frame) is shown again and stays shown (HatchPlace and Place hide it no more), with a short gun-ready sound of the
        /// game's own (the Tough's reload click for his bazooka, the bolt's rack for every other gun).</summary>
        void HatchGunUp(Ghost g, HatchExit st, float tl)
        {
            st.gunUp = true; st.gunUpTl = tl; HatchGunsUp++;
            bool found = g.weapon;
            if (found && !g.weapon.gameObject.activeSelf) g.weapon.gameObject.SetActive(true);
            if (!found) HatchGunsMissing++;
            string type = g.live && g.live.Obj != null ? g.live.Obj.Type : "";
            var at = g.weapon ? g.weapon.position : g.go ? g.go.transform.position : st.land;
            bool snd = found && Voice(type == "tough" ? "wpn_tough_reload" : "wpn_sniper_bolt", at, st.robotId >= 0 ? st.robotId : st.vehicleId, HatchGunSoundGain);
            if (snd) HatchGunSounds++;
            if (st.rec == null) return;
            st.rec.gunUpFrame = Time.frameCount; st.rec.gunUpAfterTouch = tl; st.rec.gunName = found ? g.weapon.name : ""; st.rec.gunSound = snd;
        }

        /// <summary>vehicle_exit_gun: Place (the walk on): his gun stays in his hand once it came back after the leap (true), whatever share of
        /// the walk is left (Place's old rule drew it only over the walk's last 14 %).</summary>
        bool HatchGunHeld(Ghost g) => g != null && hatchExits.Count > 0 && hatchExits.TryGetValue(g, out var st) && st.gunUp;

        /// <summary>vehicle_exit_gun: EndGhost, right after ShowLive gave his own model back its renderers: his model's gun (FindWeapon) is
        /// checked; an object of it switched off is switched on (counted); its renderers drawn / off are recorded for the Play check (ShowLive
        /// puts back exactly what was drawn at the click; nothing else is switched on here).</summary>
        void HatchGunHandOver(Ghost g, bool abort)
        {
            if (g == null || g.enter || g.demo || abort || !g.live || g.live.Dying || hatchExits.Count == 0 || !hatchExits.TryGetValue(g, out var st)) return;
            HatchGunHandOvers++;
            var w = FindWeapon(g.live.gameObject);
            if (st.rec != null) { st.rec.handFrame = Time.frameCount; st.rec.handGunFound = w; }
            if (!w) { HatchGunHandNoGun++; return; }
            bool objOn = false;
            for (var t = w; t && t != g.live.transform; t = t.parent) if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); objOn = true; }
            if (objOn) HatchGunHandObjOn++;
            int on = 0, off = 0;
            foreach (var r in w.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.enabled) on++; else off++;
            }
            if (on == 0) HatchGunHandRendOff++;
            if (st.rec != null) { st.rec.handObjOn = objOn; st.rec.handRendOn = on; st.rec.handRendOff = off; }
        }

        /// <summary>vehicle_exit_gun proof (View/OzVehicleExitGunCheck.cs): this robot's hatch-exit figure now (null: none, or done) and its phase
        /// ("inside", "crouch", "push", "air", "land", "walk", "dead").</summary>
        public static GameObject HatchFigureGo(int robotId, out string phase)
        {
            phase = ""; var bv = I; if (!bv || bv.hatchExits.Count == 0) return null;
            foreach (var kv in bv.hatchExits)
            {
                var x = kv.Key; var s = kv.Value;
                if (x == null || x.done || s.robotId != robotId || !x.go) continue;
                phase = s.dead ? "dead" : s.phase.ToString().ToLowerInvariant(); return x.go;
            }
            return null;
        }
        /// <summary>vehicle_exit_gun proof: the gun of a figure or of a robot's model (BoardingView's own FindWeapon).</summary>
        public static Transform GunOf(GameObject go) => go ? FindWeapon(go) : null;

        /// <summary>vehicle_exit_gun: his pose `w` (0..1) of the way from the state sampled now into `to` (his armed idle): the Animator's own
        /// cross-fade evaluated at that point of it (speed 0, as HatchSample holds his clips); 1 = that state alone.</summary>
        static void HatchBlendTo(Animator a, string to, float w)
        {
            if (!a || !a.isActiveAndEnabled || w <= 0f) return;
            var s = Clip(a, to); if (s == null) return;
            if (w >= 0.999f) { a.Play(s, 0, 0f); a.Update(0f); return; }
            a.CrossFadeInFixedTime(s, Mathf.Max(0.02f, HatchGunUpSec), 0, 0f, Mathf.Clamp01(w));
            a.Update(0f);
        }

        /// <summary>His feet's take-off point now: on the drawn rim (the plan's point in the hull's frame, carried with the hull), or fixed in the
        /// world after a jump at once or once the hull is gone.</summary>
        Vector3 HatchTakeoffW(Rig r, HatchExit st, bool hullGone) => st.pRimLSet && !st.bailed && !hullGone && r != null && r.root ? r.root.TransformPoint(st.pRimL) : st.p0;

        /// <summary>The figure drawn: turned `rot` (world), its own size, his clip `state` at `nt` (held: the Animator evaluated now), then placed:
        /// his hips on `hips`, or (`sole`) planted: the middle of his ankles over that point and his lower ankle his standing ankle height over
        /// it, then (`lean`, deg) the whole figure turned about that point on the flip axis (review round 2: the crouch and the push, his soles
        /// planted). Nothing but the root is moved (no joint is turned by code). The team band, his rifle on his back; proof: frames, size.</summary>
        void HatchPlace(Ghost g, HatchExit st, string state, float nt, Quaternion rot, Vector3? hips, Vector3? sole, float lean = 0f, string blendTo = null, float blendW = 0f)
        {
            if (!g.go) return;
            if (!g.go.activeSelf) { g.go.SetActive(true); g.clipNow = null; }
            var t = g.go.transform; var b = st.body;
            t.rotation = rot; t.localScale = Vector3.one * g.baseScale;
            g.rot = HatchFace(st.dir);
            if (st.phase >= JumpPhase.Land && !st.dead) OzExitLegIK.Mark(g.anim, t);                          // 1.5 N2: from the touch on his legs on the clips' foot goals, knees to his front (View/OzExitLegIK.cs)
            HatchSample(g.anim, state, nt); g.clipNow = "jump";
            if (blendTo != null && blendW > 0f) HatchBlendTo(g.anim, blendTo, blendW);                      // vehicle_exit_gun: into his armed idle as his gun comes up (before he is placed: his soles stay planted)
            if (hips.HasValue) t.position += hips.Value - (b.ok ? b.hips.position : t.position + rot * b.crouch.hipsL);
            else if (sole.HasValue)
            {
                if (b.ok)
                {
                    Vector3 fl = b.footL.position, fr = b.footR.position, mid = 0.5f * (fl + fr); float low = Mathf.Min(fl.y, fr.y);
                    t.position += new Vector3(sole.Value.x - mid.x, sole.Value.y + b.ankle0 - low, sole.Value.z - mid.z);
                }
                else t.position = sole.Value;
                if (lean != 0f) t.RotateAround(sole.Value, FlipAxis(st.dir), lean);                            // review round 2: the whole figure leaned about his planted sole point
            }
            if (Vector3.Angle(t.up, Vector3.up) <= 45f) Band(g.skin, t, g.hips, t.localScale.x);
            else HatchBandOff(g.skin);                                                                         // the team band is painted by WORLD height: off while he is turned over
            if (!st.gunUp && g.weapon && g.weapon.gameObject.activeSelf) g.weapon.gameObject.SetActive(false);   // his gun out of sight while he jumps; vehicle_exit_gun: back in his hand from the landing's squat (HatchGunUp), never hidden again
            if (st.rec != null) { st.rec.frames++; float sc = t.localScale.x / Mathf.Max(1e-4f, g.baseScale); st.rec.scaleMin = Mathf.Min(st.rec.scaleMin, sc); st.rec.scaleMax = Mathf.Max(st.rec.scaleMax, sc); }
        }

        /// <summary>The figure showed later than planned (another figure on the hull, a commander still drawn): the jump planned again from now for
        /// the rules' touch (LATE only when even a plain step off does not fit).</summary>
        void HatchSlip(Ghost g, Rig r, HatchExit st)
        {
            float was = st.takeoffAt;
            st.appearAt = st.tau;
            string line = HatchPlanJump(r, st, g.live ? g.live.transform.position : st.spot, false);
            st.slip = Mathf.Max(0f, st.takeoffAt - was);
            HatchWalkKeys(g, st);
            string note = $"; the show slipped to {st.tau:0.00} s (another figure on the hull or the commander still drawn): planned again for the rules' touch: {line}";
            HatchExitLastPlan += note;
            if (st.rec == null) return;
            st.rec.slipSec = st.slip; st.rec.plan += note;
        }

        /// <summary>The team band (UnitView's _BandWorld) off while the figure is turned over.</summary>
        void HatchBandOff(SkinnedMeshRenderer skin)
        {
            if (!skin) return;
            bandBlock ??= new MaterialPropertyBlock();
            skin.GetPropertyBlock(bandBlock, 0);
            bandBlock.SetFloat(AccentLoId, -10000f); bandBlock.SetFloat(AccentHiId, -9999f);
            skin.SetPropertyBlock(bandBlock, 0);
        }

        /// <summary>The walk on: from the landing to his robot (K441 follows it and hands over on it), at a robot's walking pace.</summary>
        void HatchWalkKeys(Ghost g, HatchExit st)
        {
            var keys = g.keys; keys.Clear(); g.detour = 0f; g.backAt = -1f;
            var land = st.land; Func<Vector3> landF = () => land; var live = g.live;
            keys.Add(K(landF, 0, "idle", Face.Keep));
            WalkTo(g, landF, () => live ? live.transform.position : land, minLast: 0.15f);
            for (int i = 1; i < keys.Count; i++) keys[i].dur = Mathf.Max(i == keys.Count - 1 ? 0.15f : 0.02f, Dist(keys[i - 1].at, keys[i].at) / Mathf.Max(0.5f, HatchWalkMps));
            g.total = Sum(keys); g.time = 0f;
        }

        /// <summary>The walk on: a ground step of the level's surface every HatchGroundStrideM.</summary>
        void HatchWalkSteps(Ghost g, HatchExit st)
        {
            if (!g.go || !g.go.activeSelf) { st.stepInit = false; return; }
            var p = g.go.transform.position;
            if (st.stepInit) { var d = p - st.stepPos; d.y = 0f; st.stepDist += d.magnitude; }
            st.stepPos = p; st.stepInit = true; st.markPos = p;
            if (st.stepDist >= HatchGroundStrideM) { st.stepDist = 0f; GroundStep(p, st.robotId); if (st.rec != null) st.rec.groundSteps++; }
        }

        /// <summary>The hull destroyed (or dying as drawn), or driven / firing again with a new driver, while the lid opens or he crouches: the same
        /// jump at once (<see cref="HatchBailJump"/>). Once he pushes off nothing changes (the rules let him go in the next tick).</summary>
        void HatchBailCheck(Ghost g, Rig r, HatchExit st)
        {
            if (st.bailed || st.phase >= JumpPhase.Push || r == null) return;
            bool rulesGone = r.obj == null || r.obj.Destroyed || r.obj.KillAt >= 0;
            if (rulesGone && st.hullDeadAt < 0f) st.hullDeadAt = Time.time;
            // a hull killed by a round is drawn exploding when the round arrives (VehicleDeathSync.Hold): he jumps with the drawn explosion, or at the
            // rules' death once VehicleDeathSync's longest wait is over (+0.5 s)
            bool gone = r.obj == null || !r.view || r.view.Dying || (rulesGone && (!HatchBailOnDrawnDeath || Time.time - st.hullDeadAt > VehicleDeathSync.MaxDelay + 0.5f));
            bool driven = !rulesGone && r.obj.Owner != Team.Neutral && r.obj.Drivers.Count > 0 && (r.obj.IsMoving || r.acted);
            if (gone || driven) HatchBailJump(g, r, st, gone);
        }

        /// <summary>The same jump, now: (still inside: the commander still sinking IS him and goes) he shows (or stays) crouched, the shortest crouch,
        /// the push and an arc planned again from here (an upward start of HatchJumpMinUpMps; the touch when he lands). Fixed in the world (the wreck
        /// may tumble).</summary>
        void HatchBailJump(Ghost g, Rig r, HatchExit st, bool gone)
        {
            st.bailed = true; HatchExitBails++;
            HatchExitLastBailDrawnDead = gone && (r == null || !r.view || r.view.Dying); HatchExitLastBailAfterRulesSec = gone && st.hullDeadAt >= 0f ? Time.time - st.hullDeadAt : -1f;
            if (gone) st.due = -1;
            if (st.phase == JumpPhase.Inside && r.crew && r.lid && r.obj != null && r.obj.Drivers.Count == 0) { Destroy(r.crew); r.crew = null; r.crewPop = 0f; HatchCommandersTaken++; }
            st.appearAt = st.tau;
            string line = HatchPlanJump(r, st, g.live ? g.live.transform.position : st.spot, true);
            HatchWalkKeys(g, st);
            string why = gone ? "the hull destroyed" : "the hull driven or firing again";
            if (st.rec != null) { st.rec.bailed = true; st.rec.plan += $"; jumped at once ({why}): {line}"; }
            HatchExitLastPlan += $"; jumped at once ({why}): {line}";
        }
        public static bool HatchExitLastBailDrawnDead; public static float HatchExitLastBailAfterRulesSec = -1f;

        bool Voice(string id, Vector3 at, int emitter, float gain) => sounds && AudioDirector.I && AudioDirector.I.Play(id, at, emitter, gain);

        /// <summary>A ground step of the level's surface (as AudioDirector's own robot steps choose it).</summary>
        void GroundStep(Vector3 p, int emitter)
        {
            string ev = "rob_step_sand", alt = null;
            var w = game ? game.World : null;
            if (w != null && w.Map != null)
            {
                var px = ZGame.WorldToPx(p); var c = w.Map.ClassAt(px.x >> 4, px.y >> 4);
                string jg = AudioDirector.JungleGround(p);
                if (c == ZClass.Water || c == ZClass.DeepWater) ev = "rob_step_water";
                else if (OzCity.Is(game.level) || c == ZClass.Road) ev = "rob_step_concrete";
                else if (jg != null) { ev = jg == "mud" ? "rob_step_mud" : jg == "road" ? "rob_step_concrete" : "rob_step_grass"; alt = jg == "mud" ? "rob_step_grass" : "rob_step_sand"; }
            }
            if (!Voice(ev, p, emitter, HatchGroundStepGain) && alt != null) Voice(alt, p, emitter, HatchGroundStepGain);
            HatchExitGroundSteps++;
        }

        // ------------------------------------------------------------------ free ground for the landing
        /// <summary>The landing foot (a body HatchSideBodyM wide) and the walk on stand on robot-passable tiles and in no building (a pure read of
        /// the rules' map, View only).</summary>
        public static float HatchSideBodyM = 0.75f, HatchSideStepM = 0.25f;
        readonly List<ZObject> sideBlocks = new List<ZObject>();

        void HatchSideBlocks(ZWorld w, ZObject v)
        {
            sideBlocks.Clear();
            foreach (var b in w.Objects)
                if (b.Kind == ZKind.Building && !b.IsBridge && !b.Destroyed && Math.Abs(b.CenterX - v.CenterX) < 160 + b.WidthPix && Math.Abs(b.CenterY - v.CenterY) < 160 + b.HeightPix) sideBlocks.Add(b);
        }

        bool HatchFootFree(ZWorld w, Vector3 p)
        {
            float h = 0.5f * HatchSideBodyM;
            return HatchPointFree(w, p) && HatchPointFree(w, p + new Vector3(h, 0, 0)) && HatchPointFree(w, p - new Vector3(h, 0, 0)) && HatchPointFree(w, p + new Vector3(0, 0, h)) && HatchPointFree(w, p - new Vector3(0, 0, h));
        }

        bool HatchPointFree(ZWorld w, Vector3 p)
        {
            var px = ZGame.WorldToPx(p);
            if (px.x < 0 || px.y < 0 || !w.Path.TilePassable(px.x >> 4, px.y >> 4, true)) return false;
            foreach (var b in sideBlocks) if (px.x >= b.LocX && px.x < b.LocX + b.WidthPix && px.y >= b.LocY && px.y < b.LocY + b.HeightPix) return false;
            return true;
        }

        /// <summary>The walk on (WalkTo: round the hull's corners when the straight way crosses it) from `from` to `to`, every HatchSideStepM.</summary>
        bool HatchWalkFree(ZWorld w, Rig r, Vector3 from, Vector3 to)
        {
            var pts = new List<Vector3> { from };
            foreach (var corner in Around(r, from, to)) pts.Add(Ground(Wp(r, corner)));
            pts.Add(to);
            for (int i = 1; i < pts.Count; i++)
            {
                var d = pts[i] - pts[i - 1]; d.y = 0f; int n = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / HatchSideStepM));
                for (int k = 1; k <= n; k++) if (!HatchPointFree(w, pts[i - 1] + d * (k / (float)n))) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ a robot ordered in while the driver is still on the hull
        /// <summary>The unfinished hatch exit on this rig whose figure is still on the hull (inside, crouching, pushing or in the air) or landed
        /// less than HatchEnterGapSec ago, or null.</summary>
        Ghost HatchExitOnHull(Rig r, out HatchExit xs)
        {
            xs = null;
            foreach (var kv in hatchExits)
            {
                var x = kv.Key; var s = kv.Value;
                if (x == null || x.done || x.rig != r || !x.go || s.dead) continue;
                if (s.phase < JumpPhase.Land || s.tau < s.touchAt + HatchEnterGapSec) { xs = s; return x; }
            }
            return null;
        }

        /// <summary>StepGhost (through HatchExitStep) for a boarding figure: while a driver is still on the same hull it walks to a spot outside the
        /// hull and waits there facing the hull (true: StepGhost stops here); when he has landed (+HatchEnterGapSec), the hull drives off, or after
        /// HatchEnterMaxHoldSec, its way in is keyed again from where it waited (BuildEnter) and StepGhost goes on. View only.</summary>
        bool HatchEnterHold(Ghost g, Rig r, float dt)
        {
            if (!HatchExitOn || g.demo || r == null || !g.go) return false;
            enterHolds.TryGetValue(g, out var h);
            if (h == null && (hatchExits.Count == 0 || r.mode != Mode.Roof || !r.lid || r.obj == null || !r.obj.IsHatchVehicle || g.time > 0f)) return false;
            var x = HatchExitOnHull(r, out var xs);
            if (x == null || (h != null && h.t >= HatchEnterMaxHoldSec) || (r.obj != null && r.obj.IsMoving))
            {
                if (h == null) return false;
                enterHolds.Remove(g);
                BuildEnter(g, g.go.transform.position);
                HatchEnterLastHold = h.t; HatchEntersReleased++;
                if (r.obj != null) CaptureHold.Keep(r.obj.Id, CaptureHold.MaxWait);
                return false;
            }
            if (r.obj != null) CaptureHold.Keep(r.obj.Id, CaptureHold.MaxWait);
            if (h == null)
            {
                h = new EnterHold(); enterHolds[g] = h; HatchEntersHeld++;
                var start = g.go.transform.position;
                var spotL = HatchEnterSpot(r, start, x, xs);
                foreach (var corner in Around(r, start, Wp(r, spotL))) h.way.Add(corner);
                h.way.Add(spotL);
            }
            h.t += dt;
            var tr = g.go.transform; var p = tr.position; Vector3 dir = Vector3.zero; bool walking = false; float step = HatchEnterWaitMps * dt;
            while (h.at < h.way.Count && step > 0f)
            {
                var goal = Ground(Wp(r, h.way[h.at])); var d = goal - p; d.y = 0f; float dist = d.magnitude;
                if (dist <= step) { p = goal; step -= dist; h.at++; if (dist > 1e-4f) { dir = d; walking = true; } continue; }
                p = Ground(p + d / dist * step); dir = d; walking = true; step = 0f;
            }
            if (!walking) { dir = r.root.TransformPoint(C(r)) - p; dir.y = 0f; }
            if (dir.sqrMagnitude > 1e-4f) g.rot = Quaternion.Slerp(g.rot, Quaternion.LookRotation(-dir.normalized, Vector3.up), 1f - Mathf.Exp(-dt * 10f));
            tr.SetPositionAndRotation(p, g.rot);
            string now = g.clipNow; Play(g.anim, ref now, walking ? "walking" : "idle", (walking ? HatchEnterWaitMps / WalkClipSpeed : 1f) * SimRate); g.clipNow = now;
            Band(g.skin, tr, g.hips, tr.localScale.x);
            if (h.stepInit) { var d = p - h.stepPos; d.y = 0f; h.stepDist += d.magnitude; }
            h.stepPos = p; h.stepInit = true;
            if (walking && h.stepDist >= HatchGroundStrideM) { h.stepDist = 0f; GroundStep(p, g.robotId); }
            return true;
        }

        /// <summary>Where a boarding figure waits (root space, y 0): of the eight spots HatchEnterSpotOutM outside the hull's box, the nearest to it
        /// that lies HatchEnterClearM or more from the old driver's landing, his figure and his robot; none such: the one farthest from them.</summary>
        Vector3 HatchEnterSpot(Rig r, Vector3 startW, Ghost x, HatchExit xs)
        {
            var c = C(r); var e = r.box.extents; float ox = e.x + HatchEnterSpotOutM, oz = e.z + HatchEnterSpotOutM;
            var avoid = new List<Vector3>();
            if (xs != null) avoid.Add(xs.land);
            if (x.go) avoid.Add(x.go.transform.position);
            if (x.live) avoid.Add(x.live.transform.position);
            Vector3 best = default, far = default; float bestD = float.MaxValue, farClear = -1f;
            for (int i = 0; i < 8; i++)
            {
                float a = i == 0 || i == 4 || i == 5 ? ox : i == 1 || i == 6 || i == 7 ? -ox : 0f;
                float b = i == 2 || i == 4 || i == 6 ? oz : i == 3 || i == 5 || i == 7 ? -oz : 0f;
                var l = new Vector3(c.x + a, 0f, c.z + b); var w = Wp(r, l);
                float clear = float.MaxValue;
                foreach (var q in avoid) { var d = q - w; d.y = 0f; clear = Mathf.Min(clear, d.magnitude); }
                var s = w - startW; s.y = 0f; float near = s.magnitude;
                if (clear >= HatchEnterClearM && near < bestD) { bestD = near; best = l; }
                if (clear > farClear) { farClear = clear; far = l; }
            }
            return bestD < float.MaxValue ? best : far;
        }

        // ------------------------------------------------------------------ the hatch, the hull and the deaths
        /// <summary>The hatch rim (world): the hatch top raised to the round hatch's collar top, as PlaceCommander's.</summary>
        Vector3 HatchRimW(Rig r)
        {
            var top = Wp(r, HatchTopL(r));
            if (r.round) { var up = r.root.up; top += up * Vector3.Dot(r.round.RimWorld - top, up); }
            return top;
        }

        /// <summary>Another figure on this hull: an unfinished boarding of it, or another driver of the same squad crouching or pushing on the rim.</summary>
        bool HatchOtherOnHull(Rig r, Ghost g)
        {
            foreach (var x in ghosts)
            {
                if (x == g || x.done || x.demo || x.rig != r || !x.go) continue;
                if (x.enter) return true;
                if (hatchExits.TryGetValue(x, out var s) && !s.dead && (s.phase == JumpPhase.Crouch || s.phase == JumpPhase.Push)) return true;
            }
            return false;
        }

        /// <summary>The dying hull's lid seats at once once he is out of the hatch: the wreck shows no open lid.</summary>
        void HatchLidShut(Rig r, HatchExit st)
        {
            st.lidShut = true;
            if (r == null || r.hatch <= 0f) return;
            r.hatch = 0f; r.hatchWant = false; r.lidAjar = false; r.lidSeated = true;
            if (r.hinge) r.hinge.localRotation = r.hingeRest;
            HatchLidsShut++;
        }

        /// <summary>BoardingView.Update at a robot's Death event: a driver killed while his figure is still in the hatch, in the air, landing or
        /// walking on: his own model, hidden meanwhile, is put where the figure is drawn (or at the hatch rim while it still waits inside);
        /// CombatView then plays his death there, never a pop on his ground spot beside the hull.</summary>
        void HatchExitDeath(int robotId)
        {
            if (!HatchExitOn || hatchExits.Count == 0 || robotId < 0) return;
            foreach (var kv in hatchExits)
            {
                var g = kv.Key; var st = kv.Value;
                if (g == null || g.done || st.dead || st.robotId != robotId || !g.live) continue;
                st.dead = true; HatchExitDeaths++; if (st.rec != null) st.rec.dead = true;
                var lt = g.live.transform; var r = g.rig;
                for (int i = 1; i < g.keys.Count; i++) { var q = g.keys[i].at(); g.keys[i].at = () => q; }
                if (g.go && g.go.activeInHierarchy) { lt.SetPositionAndRotation(g.go.transform.position, g.go.transform.rotation); lt.localScale = g.go.transform.localScale; }
                else if (r != null && r.root) { lt.SetPositionAndRotation(HatchRimW(r), HatchFace(st.dir)); lt.localScale = Vector3.one * g.baseScale; }
                HatchExitLastDeathAt = lt.position; HatchExitLastDeathOnHull = r != null && r.root && OnHull(r, lt.position);
                return;
            }
        }
        /// <summary>Proof: where the last driver's death was put, and whether that was on the hull.</summary>
        public static Vector3 HatchExitLastDeathAt; public static bool HatchExitLastDeathOnHull;

        /// <summary>On = a body killed over the hull gets a deck to land on (HatchDeckOn), the hull's death as DRAWN starts his jump (HatchBailOnDrawnDeath).</summary>
        public static bool HatchDeckOn = true, HatchBailOnDrawnDeath = true;
        /// <summary>How long a hull keeps the deck under a body that died on it (s, game time).</summary>
        public static float HatchDeckLifeSec = 14f;
        /// <summary>Proof counters (since HatchExitStatsReset): deaths drawn with his own model shown in the figure's place first, of them on the hull,
        /// flame deaths drawn as a fall, decks made.</summary>
        public static int HatchExitDiesShown, HatchExitDiesOnHull, HatchExitMeltsAvoided, HatchDeckColliders;
        /// <summary>Proof: the renderers of his own model enabled when its death started, where it started, how (figure / commander / rim).</summary>
        public static int HatchExitLastDieRenderers = -1; public static Vector3 HatchExitLastDieAt; public static string HatchExitLastDieFrom = "";

        /// <summary>While his death is still to be drawn, his hidden model stands where the figure is drawn.</summary>
        void HatchDeadFollow(Ghost g)
        {
            if (!g.live || g.live.Dying || !g.go || !g.go.activeInHierarchy) return;
            var lt = g.live.transform; var ft = g.go.transform;
            lt.SetPositionAndRotation(ft.position, ft.rotation); lt.localScale = ft.localScale;
        }

        /// <summary>UnitView.Die (its first lines): a robot whose figure is still in the hatch, in the air, landing or walking on dies: his model
        /// takes the figure's place first, his renderers on, the figure off. A flame kill in the hatch or in the air is drawn as a fall (`fire`
        /// cleared). True = the death is over the hull (a deck for the body; UnitView draws a pop as a throw). Every other robot: false.</summary>
        public static bool HatchExitDies(UnitView v, ref bool fire)
        {
            var bv = I;
            if (!HatchExitOn || !bv || !v || bv.hatchExits.Count == 0) return false;
            foreach (var kv in bv.hatchExits)
            {
                var g = kv.Key; var st = kv.Value;
                if (g == null || g.done || st.shown || g.live != v) continue;
                return bv.HatchBodyTakesOver(g, st, ref fire);
            }
            return false;
        }

        bool HatchBodyTakesOver(Ghost g, HatchExit st, ref bool fire)
        {
            st.shown = true; HatchExitDiesShown++;
            var lt = g.live.transform; var r = g.rig; string from = "figure";
            if (g.go && g.go.activeInHierarchy) { lt.SetPositionAndRotation(g.go.transform.position, g.go.transform.rotation); lt.localScale = g.go.transform.localScale; }
            else if (r != null && r.root)
            {
                if (r.crew && r.crew.activeSelf && r.crewPop > 0.001f && r.lid && r.obj != null && r.obj.Drivers.Count == 0)
                {   // still inside, the commander still sinking: that figure IS him
                    var ct = r.crew.transform; lt.SetPositionAndRotation(ct.position, ct.rotation); lt.localScale = ct.lossyScale;
                    Destroy(r.crew); r.crew = null; r.crewPop = 0f; HatchCommandersTaken++; from = "the sinking commander";
                }
                else { lt.SetPositionAndRotation(HatchRimW(r), HatchFace(st.dir)); lt.localScale = Vector3.one * g.baseScale; from = "the hatch rim"; }
            }
            bool onHull = false, inHatch = false, air = false;
            if (r != null && r.root)
            {
                float below = HatchBelowRim(r, lt.position);
                if (below > 0f) { lt.position += r.root.up * below; inHatch = true; }
                onHull = OnHull(r, lt.position);
                air = !onHull && lt.position.y - Ground(lt.position).y > 0.3f;
            }
            if (fire && (inHatch || air || st.phase <= JumpPhase.Push)) { fire = false; HatchExitMeltsAvoided++; }
            // killed in the push or the air: the arc's velocity at this moment goes on with his death (the pop's pieces, the rag doll's bodies)
            if ((st.phase == JumpPhase.Air || st.phase == JumpPhase.Push) && st.arc != null && g.go && g.go.activeInHierarchy)
            {
                var carry = HatchVelAt(st.arc, Mathf.Clamp(st.tau - (st.takeoffAt - st.arc.tp), 0f, st.arc.tp + st.arc.T));
                popCarryView = g.live; popCarryVel = carry; popCarryFrame = Time.frameCount; HatchDeathCarries++; HatchExitLastDeathCarry = carry;
                if (st.rec != null) st.rec.deathCarry = carry;
                StartCoroutine(HatchCarryBody(g.live, carry));
            }
            HatchThrowCarry(g, st);                                                                         // 1.5 DV179 (kit exit_leap_death): killed in the throw's flight: its velocity goes on with his death (View/BoardingView.HatchThrow.cs)
            ShowLive(g);
            int n = 0; foreach (var rr in g.live.GetComponentsInChildren<Renderer>()) if (rr.enabled && !(rr is ParticleSystemRenderer)) n++;
            HatchExitLastDieRenderers = n; HatchExitLastDieAt = lt.position; HatchExitLastDieFrom = from + (inHatch ? ", raised to the rim" : "") + (air ? ", in the air" : onHull ? ", on the hull" : "");
            HatchExitLastDeathAt = lt.position; HatchExitLastDeathOnHull = onHull;
            if (g.go) g.go.SetActive(false);
            if (onHull) { HatchExitDiesOnHull++; HatchDeathDeck(r, g.live); }
            return onHull;
        }

        static UnitView popCarryView; static Vector3 popCarryVel; static int popCarryFrame = -1;
        /// <summary>RobotPopDeath.DoPlay asks for the robot it pops: that robot's flight velocity when it dies in the air off a hatch jump in this
        /// frame (HatchPopCarry, added to every piece's start velocity), else zero.</summary>
        public static void HatchPopCarryFor(UnitView view) => HatchPopCarry = view && view == popCarryView && popCarryFrame == Time.frameCount ? popCarryVel : Vector3.zero;
        /// <summary>The start velocity RobotPopDeath.Launch adds to each piece of the pop it is drawing (HatchPopCarryFor).</summary>
        public static Vector3 HatchPopCarry;

        /// <summary>At the end of the frame of his death in the air, the rag doll's moving bodies carry on at the arc's velocity.</summary>
        System.Collections.IEnumerator HatchCarryBody(UnitView body, Vector3 vel)
        {
            yield return new WaitForEndOfFrame();
            if (!body) yield break;
            int n = 0;
            foreach (var rb in body.GetComponentsInChildren<Rigidbody>()) if (rb && !rb.isKinematic) { rb.linearVelocity += vel; n++; }
            HatchDeathCarriedBodies += n;
        }

        /// <summary>Vehicles have no colliders: a box over the hull's own footprint from its bottom up to under his feet (at most its deck), on the
        /// layer the fort defender's roof uses, placed as the hull stands, until the body is gone, HatchDeckLifeSec, or the hull moves or dies.</summary>
        void HatchDeathDeck(Rig r, UnitView body)
        {
            if (!HatchDeckOn || r == null || !r.root || !r.view || r.view.Dying || !body) return;
            var go = new GameObject("OZ_HatchDeathDeck") { layer = 2 };
            go.transform.SetPositionAndRotation(r.root.position, r.root.rotation); go.transform.localScale = r.root.lossyScale;
            var bc = go.AddComponent<BoxCollider>();
            var hb = HullOnlyBox(r);
            float bot = hb.min.y, top = Mathf.Clamp(r.root.InverseTransformPoint(body.transform.position).y - 0.05f, bot + 0.3f, DeckY(r));
            bc.center = new Vector3(hb.center.x, 0.5f * (top + bot), hb.center.z);
            bc.size = new Vector3(hb.size.x * 0.96f, Mathf.Max(0.1f, top - bot), hb.size.z * 0.96f);
            HatchDeckColliders++; HatchExitLastDeckTop = top - bot;
            StartCoroutine(HatchDeckLife(go, r, body));
            HatchTurretDeck(r, body);
        }
        /// <summary>Proof: the last deck box's height above the hull's bottom (m, root space).</summary>
        public static float HatchExitLastDeckTop = -1f;

        /// <summary>A second box, the turret's own (TurretOnlyBox), placed as it stands: its top 5 cm under his feet when they are over the turret,
        /// never above the turret's top. No turret (the mech): nothing.</summary>
        void HatchTurretDeck(Rig r, UnitView body)
        {
            if (!HatchDeckOn || r == null || !r.turret || !r.root || !body || !TurretOnlyBox(r, out var tb)) return;
            var tt = r.turret;
            var go = new GameObject("OZ_HatchTurretDeck") { layer = 2 };
            go.transform.SetPositionAndRotation(tt.position, tt.rotation); go.transform.localScale = tt.lossyScale;
            var bc = go.AddComponent<BoxCollider>();
            var lp = tt.InverseTransformPoint(body.transform.position);
            bool over = Mathf.Abs(lp.x - tb.center.x) < tb.extents.x + 0.1f && Mathf.Abs(lp.z - tb.center.z) < tb.extents.z + 0.1f;
            float bot = tb.min.y, top = over ? Mathf.Clamp(lp.y - 0.05f, bot + 0.1f, tb.max.y) : tb.max.y;
            bc.center = new Vector3(tb.center.x, 0.5f * (top + bot), tb.center.z);
            bc.size = new Vector3(tb.size.x * 0.96f, Mathf.Max(0.1f, top - bot), tb.size.z * 0.96f);
            HatchTurretDecks++; HatchExitLastTurretDeckTop = top - bot; HatchExitLastTurretDeckOver = over;
            StartCoroutine(HatchDeckLife(go, r, body));
        }
        /// <summary>Proof: turret boxes made since HatchExitStatsReset; the last one's height and whether his feet were over the turret.</summary>
        public static int HatchTurretDecks; public static float HatchExitLastTurretDeckTop = -1f; public static bool HatchExitLastTurretDeckOver;

        /// <summary>The turret's own box in the turret's space: its renderers without the gun, the hatch and its lid and hinge, the antennas, the
        /// ladder and the commander figure. False = no turret, or nothing left.</summary>
        static bool TurretOnlyBox(Rig r, out Bounds b)
        {
            b = new Bounds(); if (!r.turret) return false;
            var w2l = r.turret.worldToLocalMatrix; bool any = false;
            foreach (var rr in r.turret.GetComponentsInChildren<Renderer>(true))
            {
                if (rr is ParticleSystemRenderer || rr is TrailRenderer || rr is LineRenderer || (r.crew && rr.transform.IsChildOf(r.crew.transform)) || TurretPartLeftOut(r.turret, rr.transform)) continue;
                Bounds lb; Matrix4x4 m;
                if (rr is SkinnedMeshRenderer sk) { lb = sk.localBounds; m = w2l * (sk.rootBone ? sk.rootBone : sk.transform).localToWorldMatrix; }
                else { var mf = rr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; lb = mf.sharedMesh.bounds; m = w2l * rr.transform.localToWorldMatrix; }
                for (int i = 0; i < 8; i++)
                {
                    var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return any && b.size.x > 0.3f && b.size.z > 0.3f && b.size.y > 0.1f;
        }
        static readonly string[] TurretLeftOut = { "barrel", "recoil", "muzzle", "mantlet", "pitch", "gun", "hatch", "lid", "hinge", "antenna", "ladder", "rung", "rail" };
        static bool TurretPartLeftOut(Transform turret, Transform t)
        {
            for (; t && t != turret; t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                foreach (var k in TurretLeftOut) if (n.Contains(k)) return true;
            }
            return false;
        }

        /// <summary>The hull's box in root space without its turret (and the gun on it); no turret, or nothing left: r.box.</summary>
        static Bounds HullOnlyBox(Rig r)
        {
            if (!r.turret) return r.box;
            var w2l = r.root.worldToLocalMatrix; bool any = false; var b = new Bounds();
            foreach (var rr in r.root.GetComponentsInChildren<Renderer>(true))
            {
                if (rr is ParticleSystemRenderer || rr is TrailRenderer || rr is LineRenderer || rr.transform.IsChildOf(r.turret)) continue;
                Bounds lb; Matrix4x4 m;
                if (rr is SkinnedMeshRenderer sk) { lb = sk.localBounds; m = w2l * (sk.rootBone ? sk.rootBone : sk.transform).localToWorldMatrix; }
                else { var mf = rr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; lb = mf.sharedMesh.bounds; m = w2l * rr.transform.localToWorldMatrix; }
                for (int i = 0; i < 8; i++)
                {
                    var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return any && b.size.x > 0.5f && b.size.z > 0.5f ? b : r.box;
        }

        System.Collections.IEnumerator HatchDeckLife(GameObject go, Rig r, UnitView body)
        {
            float t0 = Time.time; var p0 = r.root ? r.root.position : Vector3.zero;
            while (go && Time.time - t0 < HatchDeckLifeSec && r.view && !r.view.Dying && r.root && (r.root.position - p0).sqrMagnitude < 0.01f && body && body.gameObject.activeInHierarchy) yield return null;
            if (go) Destroy(go);
        }

        /// <summary>The point an enemy aims at on a robot whose figure is in the hatch, in the air or landing: his hips (his chest once he walks),
        /// or the hatch rim + 0.3 m while he still waits inside. False = no such figure (UnitAim as before).</summary>
        public static bool HatchExitAimAt(ZObject t, out Vector3 p)
        {
            p = default; var bv = I;
            if (!HatchExitOn || !bv || t == null || t.Kind != ZKind.Robot || bv.hatchExits.Count == 0 || !bv.HatchFigureFeet(t.Id, out var feet, out var fg, out var xs)) return false;
            float com = xs.body != null ? xs.body.straight.hipsL.y - xs.body.straight.soleL.y : 1f;
            p = fg.go && fg.go.activeInHierarchy ? feet + Vector3.up * (xs.phase == JumpPhase.Walk ? 0.95f * fg.go.transform.localScale.x / Mathf.Max(1e-4f, fg.baseScale) : com) : feet + Vector3.up * 0.3f;
            return true;
        }

        /// <summary>OpenZUnitOverlay calls this first in its LateUpdate: this frame's figures are placed before it draws (once a frame: lateDoneFrame).</summary>
        public static void LateFirst() { var bv = I; if (bv && bv.isActiveAndEnabled && bv.lateDoneFrame != Time.frameCount) bv.LateUpdate(); }
        int lateDoneFrame = -1;

        /// <summary>CrewDies (before it flings the hatch commander): the hull dies in the frame its driver is let out, or while his figure still
        /// waits inside: that commander figure IS the driver who gets out; it is taken away silently.</summary>
        void HatchCommanderLeaving(Rig r)
        {
            if (!HatchExitOn || r == null || !r.crew || r.obj == null || !r.lid || !r.obj.IsHatchVehicle || r.obj.Drivers.Count > 0) return;
            bool leaving = false;
            foreach (var x in exitsNow) if (x.vehicle == r.obj) { leaving = true; break; }
            if (!leaving) foreach (var kv in hatchExits) if (kv.Key != null && !kv.Key.done && kv.Key.rig == r && !kv.Value.dead) { leaving = true; break; }
            if (!leaving) return;
            Destroy(r.crew); r.crew = null; r.crewPop = 0f; HatchCommandersTaken++;
        }

        /// <summary>How far (m, along the hull's up) these feet stand below the hatch rim while they are in the hatch; 0 elsewhere.</summary>
        float HatchBelowRim(Rig r, Vector3 feet)
        {
            var rim = HatchRimW(r); var up = r.root.up;
            var flat = Vector3.ProjectOnPlane(feet - rim, up);
            if (flat.magnitude > HatchLidMinR) return 0f;
            return Mathf.Max(0f, Vector3.Dot(rim - feet, up));
        }

        /// <summary>K431's FigureState: a hatch figure is marked under his hips in the air, on the rim while he crouches and pushes, at his feet once
        /// landed. True = p set (state 2).</summary>
        bool HatchFigureMark(Ghost g, ref Vector3 p)
        {
            if (hatchExits.Count == 0 || g == null || !g.go || !hatchExits.TryGetValue(g, out var st)) return false;
            p = st.phase == JumpPhase.Walk ? g.go.transform.position : st.markPos;
            return true;
        }

        /// <summary>OzHeroSquadView.Tall: the figure is his own size the whole way, so his boxes are his own (the hook is kept).</summary>
        public static float HatchFigureTall(ZObject o, float own) => own;

        /// <summary>The figure of that robot: its marked point (world), or the hatch rim while it still waits inside; false = none.</summary>
        bool HatchFigureFeet(int robotId, out Vector3 feet, out Ghost fg, out HatchExit xs)
        {
            feet = default; fg = null; xs = null;
            if (hatchExits.Count == 0 || robotId < 0) return false;
            foreach (var kv in hatchExits)
            {
                var x = kv.Key;
                if (x == null || x.done || kv.Value.robotId != robotId || !x.live) continue;
                fg = x; xs = kv.Value;
                if (x.go && x.go.activeInHierarchy) { feet = xs.phase == JumpPhase.Walk ? x.go.transform.position : xs.markPos; return true; }
                if (x.rig == null || !x.rig.root) return false;
                feet = HatchRimW(x.rig); return true;
            }
            return false;
        }

        /// <summary>CombatView.DrawShot: a shot at a robot whose figure is in the hatch or in the air ends on that figure. True = `to` changed.</summary>
        public static bool HatchExitShotEnd(ZObject t, Vector3 from, bool hit, ref Vector3 to)
        {
            var bv = I;
            if (!HatchExitOn || !bv || t == null || t.Kind != ZKind.Robot || bv.hatchExits.Count == 0 || !bv.HatchFigureFeet(t.Id, out var feet, out var fg, out var xs)) return false;
            Vector3 chest;
            if (fg.go && fg.go.activeInHierarchy && fg.skin && fg.skin.enabled && fg.skin.bounds.size.y > 0.2f) { var b = fg.skin.bounds; chest = xs.phase == JumpPhase.Walk ? new Vector3(b.center.x, b.max.y - 0.3f * b.size.y, b.center.z) : b.center; }
            else chest = feet + Vector3.up * 0.3f;
            if (hit) to = chest + UnityEngine.Random.insideUnitSphere * 0.12f;
            else
            {
                var d = chest - from; d.y = 0f; var side = d.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, d.normalized) : Vector3.right;
                to = HitSurface.MissGround(from, chest + side * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(0.35f, 0.9f) + Vector3.up * UnityEngine.Random.Range(-0.2f, 0.35f));
            }
            HatchShotsOnFigure++;
            return true;
        }

        /// <summary>CombatView.MissileFired / MissileExploded: one aimed at a robot whose figure is in the hatch or in the air lands and bursts at
        /// that figure. True = moved.</summary>
        public static bool HatchExitBlastAt(ZObject t, ref Vector3 at)
        {
            var bv = I;
            if (!HatchExitOn || !bv || t == null || t.Kind != ZKind.Robot || bv.hatchExits.Count == 0 || !bv.HatchFigureFeet(t.Id, out var feet, out var fg, out var xs)) return false;
            at += feet - (xs.dead ? xs.spot : fg.live.transform.position);
            HatchBlastsOnFigure++;
            return true;
        }

        /// <summary>OpenZPyroFire.Arrive: a flame that hits a robot whose figure is in the hatch or in the air sets the FIGURE alight. False = none.</summary>
        public static bool HatchFigureBody(int robotId, out GameObject go)
        {
            go = null; var bv = I;
            if (!HatchExitOn || !bv || bv.hatchExits.Count == 0 || !bv.HatchFigureFeet(robotId, out _, out var fg, out _) || !fg.go || !fg.go.activeInHierarchy) return false;
            go = fg.go; HatchFlamesOnFigure++;
            return true;
        }

        /// <summary>Proof (Play check): the drawn figure of a robot let out of a hatch vehicle now: 0 none, 1 waiting inside, 2 drawn; its marked point,
        /// the renderers' world bounds, its drawn size against his own model's, the phase ("inside", "crouch", "push", "air", "land", "walk", "dead").</summary>
        public static int HatchFigureNow(int robotId, out Vector3 feet, out Bounds drawn, out float size, out string phase)
        {
            feet = default; drawn = default; size = 0f; phase = "";
            var bv = I; if (!bv || bv.hatchExits.Count == 0) return 0;
            foreach (var kv in bv.hatchExits)
            {
                var x = kv.Key; var st = kv.Value;
                if (x == null || x.done || st.robotId != robotId || !x.live) continue;
                phase = st.dead ? "dead" : st.phase.ToString().ToLowerInvariant();
                if (!x.go || !x.go.activeInHierarchy) return 1;
                feet = st.phase == JumpPhase.Walk ? x.go.transform.position : st.markPos; size = x.go.transform.localScale.x / Mathf.Max(1e-4f, x.live.transform.localScale.x);
                bool any = false;
                foreach (var rr in x.go.GetComponentsInChildren<Renderer>()) { if (!rr.enabled || rr is ParticleSystemRenderer) continue; if (!any) { drawn = rr.bounds; any = true; } else drawn.Encapsulate(rr.bounds); }
                return 2;
            }
            return 0;
        }

        /// <summary>Proof (Play check): the drawn figure's body points now (world): its skeleton's bones and the middle of every bone segment.</summary>
        public static void HatchFigureBones(int robotId, List<Vector3> into)
        {
            into.Clear(); var go = HatchFigureGo(robotId); if (!go) return;
            foreach (var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.enabled || sk.bones == null) continue;
                var set = new HashSet<Transform>(); foreach (var b in sk.bones) if (b) set.Add(b);
                foreach (var b in sk.bones) { if (!b) continue; into.Add(b.position); if (b.parent && set.Contains(b.parent)) into.Add(0.5f * (b.position + b.parent.position)); }
                return;
            }
        }
        /// <summary>vehicle_exit_jump3, Proof (Play check, the take-off's speed row at any frame rate): for that robot's exit in its push or flight,
        /// the exit's own time now (tau: game s since the click, the clock the figure is drawn on), the plan's hips at it (HatchPushAt / the arc,
        /// what the drawn hips must follow), and the plan's own hips' velocity 2 ms before and after the take-off (the push's end against the
        /// arc's start). False: none (not in the push or the air, or no plan).</summary>
        public static bool HatchPlanNow(int robotId, out float tau, out Vector3 hips, out Vector3 vPushEnd, out Vector3 vAirStart)
        {
            tau = 0f; hips = vPushEnd = vAirStart = default; var bv = I; if (!bv) return false;
            foreach (var kv in bv.hatchExits)
            {
                var x = kv.Key; var s = kv.Value;
                if (x == null || x.done || s.robotId != robotId || s.arc == null || (s.phase != JumpPhase.Push && s.phase != JumpPhase.Air)) continue;
                tau = s.tau; float t = s.tau - (s.takeoffAt - s.arc.tp);
                hips = HatchHipsAt(s.arc, Mathf.Clamp(t, 0f, s.arc.tp + s.arc.T));
                vPushEnd = (HatchHipsAt(s.arc, s.arc.tp) - HatchHipsAt(s.arc, s.arc.tp - 0.002f)) / 0.002f;
                vAirStart = (HatchHipsAt(s.arc, s.arc.tp + 0.002f) - HatchHipsAt(s.arc, s.arc.tp)) / 0.002f;
                return true;
            }
            return false;
        }
        /// <summary>vehicle_exit_jump3, Proof (Play check): the drawn figure's body points now as HatchFigureBones, each with its bone's name (the
        /// cut count's margin: HatchMarginFor).</summary>
        public static void HatchFigureBones(int robotId, List<Vector3> into, List<string> names)
        {
            into.Clear(); names?.Clear(); var go = HatchFigureGo(robotId); if (!go) return;
            foreach (var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.enabled || sk.bones == null) continue;
                var set = new HashSet<Transform>(); foreach (var b in sk.bones) if (b) set.Add(b);
                foreach (var b in sk.bones)
                {
                    if (!b) continue; into.Add(b.position); names?.Add(b.name);
                    if (b.parent && set.Contains(b.parent)) { into.Add(0.5f * (b.position + b.parent.position)); names?.Add(b.name); }
                }
                return;
            }
        }
        /// <summary>Proof (Play check): the drawn figure of a robot let out of a hatch vehicle (null: none drawn now).</summary>
        public static GameObject HatchFigureGo(int robotId)
        {
            var bv = I; if (!bv) return null;
            foreach (var kv in bv.hatchExits)
            {
                var x = kv.Key;
                if (x == null || x.done || kv.Value.robotId != robotId || !x.go || !x.go.activeInHierarchy) continue;
                return x.go;
            }
            return null;
        }
        /// <summary>Proof (Play check): the solids of every drawn part of that vehicle and of the buildings and flags near it, each with its
        /// transform's matrix now (the Play check calls Use(Basis()) every frame: the lid as it swings); false = no rig.</summary>
        public static bool HatchVehicleSolids(int vehicleId, List<HatchSolid> into)
        {
            into.Clear(); var r = HatchRigOf(vehicleId); if (r == null || !r.root || !I) return false;
            var rs = new List<Renderer>(); I.HatchVehicleRenderers(r, rs);
            foreach (var rr in rs) { var s = HatchSolid.Of(rr); if (s == null) continue; s.lid = r.hinge && rr.transform.IsChildOf(r.hinge); into.Add(s); }
            rs.Clear(); I.HatchNearRenderers(r, rs);
            foreach (var rr in rs) { var s = HatchSolid.Of(rr); if (s == null) continue; s.world = true; into.Add(s); }
            return true;
        }
        /// <summary>Proof (Play check): the point lies in that vehicle's hatch opening (InHole).</summary>
        public static bool HatchInHoleOf(int vehicleId, Vector3 p)
        {
            var r = HatchRigOf(vehicleId); if (r == null || !r.root || !I) return false;
            return InHole(I.HatchRimW(r), r.root.up, p);
        }
        /// <summary>Proof (Play check K2): the height of what the point stands over: the ground, or the hull's deck or the turret's top when it is
        /// over their footprint; -inf: no rig.</summary>
        public static float HatchSurfaceY(int vehicleId, Vector3 p)
        {
            var bv = I; if (!bv) return float.NegativeInfinity;
            float y = bv.Ground(p).y; var r = HatchRigOf(vehicleId); if (r == null || !r.root || !r.view || r.view.Dying) return y;
            var hb = HullOnlyBox(r); var lp = r.root.InverseTransformPoint(p);
            if (lp.x > hb.min.x && lp.x < hb.max.x && lp.z > hb.min.z && lp.z < hb.max.z) y = Mathf.Max(y, r.root.TransformPoint(new Vector3(lp.x, hb.max.y, lp.z)).y);
            if (r.turret && TurretOnlyBox(r, out var tb)) { var lt = r.turret.InverseTransformPoint(p); if (lt.x > tb.min.x && lt.x < tb.max.x && lt.z > tb.min.z && lt.z < tb.max.z) y = Mathf.Max(y, r.turret.TransformPoint(new Vector3(lt.x, tb.max.y, lt.z)).y); }
            return y;
        }

        // ------------------------------------------------------------------ for the Play check (View/OzVehicleExitCheck.cs)
        static Rig HatchRigOf(int vehicleId)
        {
            var bv = I; if (!bv) return null;
            foreach (var kv in bv.rigs) if (kv.Value.obj != null && kv.Value.obj.Id == vehicleId) return kv.Value;
            return null;
        }
        /// <summary>Play check: the hatch rim of that vehicle (world); false = no rig.</summary>
        public static bool HatchRimOf(int vehicleId, out Vector3 rim) { var r = HatchRigOf(vehicleId); rim = r != null && r.root && I ? I.HatchRimW(r) : default; return r != null && r.root; }
        /// <summary>Play check: the drawn hatch rim of that vehicle and its hull's up now (world); false = no rig.</summary>
        public static bool HatchRimFrameOf(int vehicleId, out Vector3 rim, out Vector3 up)
        {
            var r = HatchRigOf(vehicleId); bool ok = r != null && r.root && I;
            rim = ok ? I.HatchRimW(r) : default; up = ok ? r.root.up : Vector3.up; return ok;
        }
        /// <summary>Play check: where his sole point belongs now while he crouches or pushes on the rim (review round 2: the push too): his take-off
        /// point on the DRAWN rim (the plan's point in the hull's frame, carried with the hull); false = none (not on the rim, a jump at once).</summary>
        public static bool HatchTakeoffNow(int robotId, out Vector3 p)
        {
            p = default; var bv = I; if (!bv) return false;
            foreach (var kv in bv.hatchExits)
            {
                var x = kv.Key; var s = kv.Value;
                if (x == null || x.done || s.robotId != robotId || (s.phase != JumpPhase.Crouch && s.phase != JumpPhase.Push) || !s.pRimLSet || s.bailed || x.rig == null || !x.rig.root) continue;
                p = x.rig.root.TransformPoint(s.pRimL); return true;
            }
            return false;
        }
        /// <summary>Play check: the point (world) lies inside the hull's box under its deck; false = no rig.</summary>
        public static bool HatchUnderDeck(int vehicleId, Vector3 p)
        {
            var r = HatchRigOf(vehicleId); if (r == null || !r.root || !I) return false;
            var lp = r.root.InverseTransformPoint(p); var c = r.box.center; var e = r.box.extents;
            return Mathf.Abs(lp.x - c.x) < e.x - 0.1f && Mathf.Abs(lp.z - c.z) < e.z - 0.1f && lp.y < I.DeckY(r) - 0.15f;
        }
        /// <summary>Play check: the point (world) lies inside the turret's own box, more than 0.3 m under its top; false = no rig or no turret.</summary>
        public static bool HatchInTurret(int vehicleId, Vector3 p)
        {
            var r = HatchRigOf(vehicleId); if (r == null || !r.turret || !TurretOnlyBox(r, out var tb)) return false;
            var lp = r.turret.InverseTransformPoint(p);
            return Mathf.Abs(lp.x - tb.center.x) < tb.extents.x - 0.1f && Mathf.Abs(lp.z - tb.center.z) < tb.extents.z - 0.1f && lp.y < tb.max.y - 0.3f && lp.y > tb.min.y - 0.5f;
        }
        /// <summary>How far the vehicle's hatch lid is open now (0 shut .. 1 open; -1 no rig).</summary>
        public static float HatchOpenOf(int vehicleId) { var r = HatchRigOf(vehicleId); return r != null ? r.hatch : -1f; }
        /// <summary>The commander stands up in the vehicle's open hatch now.</summary>
        public static bool CommanderUpOf(int vehicleId) { var r = HatchRigOf(vehicleId); return r != null && r.crew && r.crew.activeSelf && r.crewPop > 0.9f; }
        /// <summary>Any part of the commander's figure shows in the hatch now (rising, up or sinking).</summary>
        public static bool CommanderShownOf(int vehicleId) { var r = HatchRigOf(vehicleId); return r != null && r.mode == Mode.Roof && r.crew && r.crew.activeSelf && r.crewPop > 0.001f; }

        /// <summary>Proof: the drawn figures on this vehicle now: the driver getting out (outShown, where, on the hull) and a robot boarding it
        /// (inShown, where, on the hull, held while the driver is on the hull). On the hull = over the hull's box and 0.25 m or more above the ground.</summary>
        public static void HatchFiguresOf(int vehicleId, out bool outShown, out Vector3 outPos, out bool outOn, out bool inShown, out Vector3 inPos, out bool inOn, out bool inHeld)
        {
            outShown = inShown = outOn = inOn = inHeld = false; outPos = inPos = Vector3.zero;
            var bv = I; var r = HatchRigOf(vehicleId); if (!bv || r == null) return;
            foreach (var x in bv.ghosts)
            {
                if (x.done || x.demo || x.rig != r || !x.go || !x.go.activeInHierarchy) continue;
                var p = x.go.transform.position; bool on = bv.OnHull(r, p);
                if (x.enter) { inShown = true; inPos = p; inOn = on; inHeld = bv.enterHolds.ContainsKey(x); }
                else { outShown = true; outPos = p; outOn = on; }
            }
        }

        bool OnHull(Rig r, Vector3 p)
        {
            var l = r.root.InverseTransformPoint(p) - C(r); var e = r.box.extents;
            return Mathf.Abs(l.x) < e.x && Mathf.Abs(l.z) < e.z && p.y - Ground(p).y > 0.25f;
        }
    }
}
