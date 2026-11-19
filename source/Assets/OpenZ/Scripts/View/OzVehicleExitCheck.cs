#if UNITY_EDITOR && !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.4 rule DV171 EXIT DRIVER (Sim/ZObject.VehicleExit.cs): the Play check, driven by the real mouse and keyboard (synthetic Input System
    /// events, as View/OrderCheck.cs clicks EXIT CREW), on a level playing in the normal mode (the rule's switch on). Menus: OpenZ/V1.4/Vehicle
    /// Exit Jump: Play Check (real mouse, a level playing), ...: Play Check 14c (level 2) (fix round 3), ...: Owner Frames (the frame sheets only;
    /// fix round 3: its own report, owner_frames_report.txt), ...: Play Check, last report
    /// (Editor/OzV14VehicleExitStopCount.cs; the older menu OpenZ/V1.4/Vehicle Exit: Play Check runs the same). Pictures and the report: Folder
    /// (the vehicle_exit_jump kit's proof/play).
    /// the climb-out rows (5, 5c, 5d, 12-18 of the vehicle_exit kit: hatch climb, deck, hop, ladder, flank, the climb's side, the climb's marks
    /// and deaths) are gone with the climb itself; the tab and rule rows 1-4 and 6-11 are kept as they were; the jump and the stop rows are new.
    /// Every row needs its samples: a row with nothing measured FAILS.
    /// vehicle_exit_jump2 (the first jump kit's look failed this check on all four types; its rules rows passed and stay): the jump is now his
    /// own clips only (no joint turned by code), a push from his crouch on the rim, one arc, one somersault about his hips, the knee bend and his
    /// landing clip; the drawn hull and turret stop in the emptied frame (View/UnitView.QuietStop.cs). Changed rows: the phases have a "push"
    /// (between the crouch and the flight: the take-off's speed is the hips' in the last PUSH frame against the first flight frame, within
    /// VyMatchFrac 25 %); the turret's turn on the hull is its YAW about the hull's up (a recoil rock snapping to rest in the emptied frame is no
    /// turn); the plan's "late for a plan 0.00 / 0.02 / 0.04 s after the click" (the old solver's tau probe) is gone. Check faults fixed (README):
    /// 3b keeps the light tank on screen (a click there moved the camera; it is flown back, noted); the level's own enemies are quieted for the
    /// check (their Damage 0, restored at the end: they shot the scenes' tanks before the click, S1p / K1, and the drivers, K1 / P / E); K1's
    /// driver cannot be killed by the hull's own blast (the row is the jump at once, not the blast); P2 takes every enemy unit again each frame
    /// until the game is over (3 s at most) and names what is left; E and P name the phases seen and the driver's death.
    /// Review round 2 (vehicle_exit_jump2): J1's feet on the rim are read in every crouch AND push frame (the drawn sole point against the drawn
    /// take-off point, SoleOnRimM; in the push his soles had slid 0.16-0.39 m and no row saw it); the take-off's speed out is matched too (the
    /// hips' horizontal speed, last push frame against the first flight frame); the stop (S1, S2, and J1's stop) is read from the frame after the
    /// emptied frame: the drawn hull and turret move or turn in at most BrakeFrames frames after it, then glide at most StopGlideM and nod or turn
    /// at most StopStillDeg (the old rows let a 0.3 s glide pass and never read the hull's nod).
    ///  1  a free stretch of ground near the camera (batch C: ClearedBlock when none is free): a red light tank, medium tank, heavy tank, mech and a
    ///     gun, each with a grunt inside, and a second light tank;
    ///  2, 3, 3b, 3c, 4  the tab: shown on a select, no driver out by clicks, double-clicks and right-clicks on and round the hull, an attack order,
    ///     the two tabs' sizes at two screens and three UI scales (unchanged);
    /// Fix round 1: every frame is measured from before the click (also while a sheet cell is captured); the turret's and the drawn hull's
    /// baselines are the last crewed frame's. Fix round 2 (review): the rules' shots of the frame the vehicle is first empty are split at its
    /// own Ejected event or at the sniper's own hit (the Hitscan with its snipe flag in the emptying tick); the figure is measured at the END of
    /// every frame (what is drawn) on its own, never from BoardingView's record of itself; no row passes on 0 samples.
    ///  J1 each of the four (light tank, medium tank, heavy tank, mech), a click on the tab, every frame until the walk on ends, from real frames:
    ///     the figure's size against his own model constant over the whole exit (within SizeTol); its marks (ring, frame, bar: K431's state 2) on
    ///     it in every drawn frame of the jump; no frame where a body point (his bones and the middles of their segments) comes closer than a
    ///     limb's half thickness (BoardingView.HatchJumpLimbM) to a solid of the vehicle (every drawn part, its mesh as columns, nothing cut at
    ///     the rim, nothing left out by name; the lid as drawn) or of a building or flag near it, only the hatch's opening left out; one full turn
    ///     from the push to the touch (330-390 deg); at the touch (the first frame drawn landing) the lower ankle at its planted height (the walk
    ///     on's) within FeetAtTouchM and the body within TiltAtTouchDeg of upright; the click-to-touch time (the rules' click, the check's own
    ///     touch frame) 1.0-1.5 s and within 0.1 s of the rules' out moment (its tag "[touch type s hold s]" is H5 of
    ///     Editor/OzV14VehicleExitStopCount.cs); the take-off continuous (the hips' vertical speed in the last crouch-spring frame against the
    ///     first flight frame); the plan clear, not late, not late for a plan 0.00 / 0.02 / 0.04 s after the click; rob_land in the touch frame
    ///     (+-1, the sound log's time), the take-off sound, the lid's open (when it was shut) and shut sounds, ground steps after the touch,
    ///     CombatView's exit and vo_vehicle_exit; the robot held still while the rules hold him; the vehicle's stop (StopOk); a 12-frame sheet at
    ///     the normal RTS camera anchored to the jump's moments (shown, mid-spring, take-off, 15 / 35 / 55 / 75 / 90 % of the flight, touch,
    ///     mid-bend, stand, walk; exit_jump_<type>_frames.png);
    ///     fix round 3 (review): the size from the drawn hips-to-head length along the bones against his own model's (every frame, within
    ///     SizeTol); in every crouch frame his lower ankle at the drawn rim's height + his ankle over the sole, his ankles' middle on the drawn
    ///     take-off point (the hull's frame) and his root still along the hull's up (CrouchAnkleM, CrouchFeetM, RootRiseTolM; review round 2: his
    ///     drawn sole point within SoleOnRimM of it in every crouch AND push frame, the take-off's speed out matched too); the feet at the
    ///     touch against the ground too; the take-off sound in the sound log within +-1 frame of the first flight frame; the stand-up: the
    ///     hips' upward speed in every landing frame at most LandRiseMaxMps, StandUpMinSec or more from their lowest to standing;
    ///  J1j each of the four: the joint limits in every drawn frame of the jump (hip flexion and abduction against his pelvis, the knee's hinge,
    ///     no hyperextension, no side bend, the spine and the neck): 0 violations, FAIL when no frame was read; vehicle_exit_jump3 (E2b: 1-3
    ///     frames a jump read "left knee bent the wrong way 11-16 deg" or "hinge 48-76 deg off": the BS-01 rig's left knee is drawn 16.5 deg
    ///     bent even at rest, its kink 44.5 deg off the side axis, so a nearly straight knee's geometric hinge is that kink, not a knee's
    ///     hinge): the knee is measured against HIS rig's own idle pose (the shank's turn in the thigh bone's own frame from idle's), its
    ///     hinge judged only past KneeHingeMinDeg of bend, "the wrong way" past KneeWrongMinDeg;
    ///  vehicle_exit_jump3, J1 also: the leap (his drawn hips at least ApexMinM, the mech ApexMinMechM, over the take-off; the landing
    ///     LandMinM-LandMaxM from the take-off point), the lid kicked open (at most LidKickMaxSec), he shows crouched within ShowAfterLidMaxSec
    ///     of the lid's start and pushes within PushAfterShowMaxSec of the show; the take-off's speed at ANY frame rate: the drawn hips against
    ///     the plan (BoardingView.HatchPlanNow) at the last push frame and the first flight frame (PlanPosTolM), the drawn speed over each of
    ///     those steps against the plan's over the same steps (PlanSpeedFrac), the plan's own speed at the push's end against the flight's
    ///     start; the cut count by the planner's own rule (BoardingView.HatchMarginFor); the camera kept on the scene (Home, OnScreen);
    ///  6, 7, 8a, 8b, 9, 10, 11  the lock, several tanks, a gun and a tank together, a tank driving, a tank boxed in, a tank under the tab (unchanged);
    ///  14a, 14b, 14c the free-spot rule and his side with the jump (fix round 2, review: ported from the vehicle_exit kit): a light tank beside a
    ///     flag, a heavy tank against a building, level 2's tank at (13, 5) turned to 8 headings (14c on level 2 only): the landing on free ground
    ///     on his side of the hull, no frame of the figure in the vehicle, a building or a flag;
    ///  S1 each of the four driving (an attack-move) and firing at enemy grunts (their fire off): the tab clicked right after a shot while it drives
    ///     (the mech: while its hand gun's burst runs): from the frame it is seen empty no shot by the rules, none sent to the view, none drawn,
    ///     no weapon sound heard (OzVehicleQuiet: DrawnAfter, MutedOf, HeardAfter: the weapons' voices and the tanks' gun launches, tankshot and
    ///     heavyshot), the mech's hand-gun rounds after 0 (rounds before > 0, the burst cut), its move, its hull's and its turret's turn (on the
    ///     hull and in the world) after the stop, review round 2: still from the frame after the emptied frame (StillStep: no glide, no nod, no
    ///     turn) (stop_<type>_frames.png); S1p / S1y: a light tank whose driver is a psycho / a pyro (DV87), the
    ///     tab clicked while his own weapon fires from the hatch: his machine-gun rounds, jet cues and flame loops after the emptied frame 0, his
    ///     muzzle refused;
    ///  S1j / S1jj (fix round 3, review: no jump off a moving vehicle was measured): J1's whole verdict and J1j on every S1 row's jump;
    ///  S2 each of the four: it fires, then DRIVES to and fro (a plain move) while a squad of REAL enemy snipers shoots at its driver (the game at
    ///     2x until the snipe; measured from before the snipe): the snipe must find it driving (up to three scenes), from the sniper's hit the same
    ///     counts, the vehicle's centre unchanged from the hit's own point (the mech: hand-gun rounds before > 0, none after) (sniped_<type>_frames.png);
    ///  K1 the light tank and the mech destroyed while the lid opens (+0.2 s) and while he crouch-springs on the rim (+0.35 s): the same jump at
    ///     once, landed with rob_land, the robot alive, the wreck's lid shut (0 .. 0.01) (hull_killed_<type>_<phase>_frames.png);
    ///  K2 the light tank's and the mech's driver killed (the rules' own death) while he crouch-springs on the rim and in the air, and the light
    ///     tank's in the air by a real enemy sniper's headshot (DV116's own death): his death drawn where the figure is, a death sound, no figure
    ///     left, the body 2.5 s later at rest on what is under it (the ground, or the hull's deck or turret top: at most 0.6 m above it) or gone,
    ///     not inside the hull or the turret (killed_<type>_<phase>_frames.png);
    ///  P  the game paused while he is in the air (1 s): the figure stands still; resumed: he lands; no error in the Console;
    ///  E  a pyro beside the emptied mech ordered in (the player's ENTER order) at the click: its figure waits off the hull until the driver has
    ///     landed (a held boarding, released after the touch), the two figures never both on the hull, the pyro climbs in;
    ///  P2 (last: it ends the level) the game won while he is in the air (every enemy unit gone): he lands, nothing frozen, no error;
    ///  F  (also the menu Owner Frames alone: OzVehicleExitCheck.Only = "frames") the owner frame sheets: J1's four sheets, S1's mech and S2's
    ///     sheet, at the normal RTS camera, no check markers.
    /// Everything it made is removed at the end; the Game view size, the UI scale and the game speed are put back.
    /// </summary>
    public sealed class OzVehicleExitCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.4 vehicle exit play]";
        public static string Report = "not run", Folder;
        public static bool Running;
        public static readonly Vector2Int[] Screens = { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) };
        /// <summary>vehicle_exit_jump, the limits of the jump and stop rows: the click-to-touch time (s of game time), the touch against the rules'
        /// hold (s), the feet's height (m: the lower ankle at the touch against its height when planted in the walk on and, fix round 3, against
        /// his rig's ankle over the ground) and the body's tilt (deg) at the touch, the somersault drawn (deg), the size drawn against his own
        /// model (fix round 3: the drawn hips-to-head length along the bones), the landing sound's frames from the touch (and the take-off sound's
        /// from the first flight frame), the vehicle's brake (s of game time), its move after it (m), its turret's and hull's turn after the stop
        /// (deg). Fix round 2: a body point cuts a solid when it is closer to it than BoardingView.HatchJumpLimbM (a limb's half thickness); the
        /// take-off's vertical speed: the last crouch-spring frame against the first flight frame (within VyMatchFrac of it, at least
        /// VyMatchFloorMps); the joint limits (deg, in his pelvis's frame): hip flexion HipFlexMin..HipFlexMax, hip abduction HipAbdMax, knee
        /// 0..KneeMax (no hyperextension, its hinge within KneeSideMax of the pelvis's side axis), spine bend SpineMax, neck bend NeckMax.</summary>
        public static float TouchMinSec = 1.0f, TouchMaxSec = 1.5f, TouchHoldSec = 0.1f, FeetAtTouchM = 0.05f, TiltAtTouchDeg = 10f, FlipMinDeg = 330f, FlipMaxDeg = 390f,
                            SizeTol = 0.02f, BrakeSec = 0.3f, StopMoveM = 0.05f, StopTurnDeg = 0.5f, VyMatchFrac = 0.25f, VyMatchFloorMps = 0.25f,   // vehicle_exit_jump2: 25 %
                            HipFlexMin = -30f, HipFlexMax = 125f, HipAbdMax = 50f, KneeMax = 150f, KneeSideMax = 45f, SpineMax = 70f, NeckMax = 60f;
        /// <summary>Fix round 3 (review): in every crouch frame his lower ankle within CrouchAnkleM of the drawn rim's height + his ankle over the
        /// sole, his ankles' middle within CrouchFeetM of the drawn take-off point, his root within RootRiseTolM along the hull's up (the lean
        /// about his feet moves it 1-3 cm; the old rise out of the hatch was 0.45 m); after the touch his hips rise at most LandRiseMaxMps in
        /// every landing frame and stand up in StandUpMinSec or more.</summary>
        public static float CrouchAnkleM = 0.05f, CrouchFeetM = 0.05f, RootRiseTolM = 0.03f, LandRiseMaxMps = 2.5f, StandUpMinSec = 0.25f;
        public static int LandSoundFrames = 1;
        /// <summary>Review round 2 (vehicle_exit_jump2): his drawn sole point (his ankles' middle brought down along his body's up to his lower
        /// ankle, less his rig's ankle over the sole) within SoleOnRimM of the drawn take-off point in every crouch and push frame; the drawn
        /// stop: the hull or its turret still moving (a frame step over StopStepM m or StopStepDeg deg) in at most BrakeFrames frames after the
        /// emptied frame (the view reads it in that frame or the next), then, from the frame after the emptied frame (StopSampleFrames frames at
        /// least), the hull's glide at most StopGlideM m and its nod (its up), its turn and its turret's turn and nod in the world at most
        /// StopStillDeg deg (BrakeSec / StopMoveM: the old rows' limits, read no more).</summary>
        public static float SoleOnRimM = 0.03f, StopStepM = 0.0005f, StopStepDeg = 0.02f, StopGlideM = 0.005f, StopStillDeg = 0.2f;
        public static int BrakeFrames = 1, StopSampleFrames = 20;
        /// <summary>vehicle_exit_jump3: the leap (his drawn hips' highest point in the flight over the plan's hips at the take-off, m; the mech's
        /// 3.65 m rim and its fixed touch: ApexMinMechM), the landing's distance from the take-off point (m, flat), the lid's kick (s of game time
        /// from its first move to open), the show after the lid's start and the push after the show (s); the knee row against his rig's idle: the
        /// hinge judged past KneeHingeMinDeg of bend, the wrong way past KneeWrongMinDeg; the take-off: the drawn hips within PlanPosTolM of the
        /// plan's at the last push frame and the first flight frame, the drawn speed over each of those steps within PlanSpeedFrac (at least
        /// PlanSpeedFloorMps) of the plan's over the same steps, the plan's own speed at the push's end and the flight's start within
        /// PlanKinkMps; staging: a unit to click at least EdgeKeepPx from the screen's edges, the camera's focus within HomeTolM of the scene's.</summary>
        public static float ApexMinM = 0.5f, ApexMinMechM = 0.35f, LandMinM = 1.9f, LandMaxM = 4.1f, LidKickMaxSec = 0.20f, ShowAfterLidMaxSec = 0.18f, PushAfterShowMaxSec = 0.12f,
                            KneeHingeMinDeg = 20f, KneeWrongMinDeg = 12f, PlanPosTolM = 0.03f, PlanSpeedFrac = 0.15f, PlanSpeedFloorMps = 0.3f, PlanKinkMps = 0.15f, EdgeKeepPx = 160f, HomeTolM = 3f;
        /// <summary>Fix round 4 (review: at the editor's ~30 fps every commander-up exit read as a 0.03 s "slip" and its leap went unjudged): the
        /// leap row is left out only for a REAL slip (the plan moved the take-off more than SlipJudgeSec: another figure on the rim, a hitch);
        /// the second robot of a two-robot squad shows as the first takes off (0.12 s later, the same rules' touch), so his flight is that much
        /// shorter and his leap lower: ApexMinSecondM, the mech ApexMinSecondMechM (the arc model: _work/sim_jump3.py, the "second robot" case).</summary>
        public static float SlipJudgeSec = 0.10f, ApexMinSecondM = 0.30f, ApexMinSecondMechM = 0.17f;
        /// <summary>Fix round 3 (review: Owner Frames overwrote the full check's report that H5 reads): the report's file in Folder: the whole check
        /// play_check_report.txt (Stop Count's H5 reads it), Owner Frames owner_frames_report.txt, any other part run play_check_only_&lt;groups&gt;_report.txt.</summary>
        public static string ReportFile => string.IsNullOrEmpty(Only) ? "play_check_report.txt" : Only.Trim() == "frames" ? "owner_frames_report.txt"
                                           : "play_check_only_" + new string(Only.Where(char.IsLetterOrDigit).ToArray()) + "_report.txt";
        /// <summary>A comma list of the step groups to run (2, 3, 4, J, 6, 7, 8, 10, 11, 14, 14c, S, K, P, E, P2; "frames" = the owner frame sheets
        /// only), set before the menu; null or empty = the whole check (the default; P2 ends the level, so it runs last). Fix round 3: the menus
        /// set it (PlayStart: null, "frames", "14c") and every run clears it at its end.</summary>
        public static string Only;
        static bool Do(string k) => string.IsNullOrEmpty(Only) || Only.Split(',').Any(s => s.Trim() == k);
        /// <summary>D3: the robot that left vehicle v through EXIT DRIVER (the rules' own record, ZObject.DriverOutFromId), not any own robot
        /// near the hull (L17's own robots walk past the car park).</summary>
        static bool LeftBy(ZObject o, ZObject v) => o.DriverOutFromId == v.Id || (o.DriverOutFromId < 0 && o.DriverOutAt >= 0);   // or let out by an EXIT whose hold the hull's death cut (DriverOutHullGone clears the id)
        /// <summary>D3 diagnostics: the tab's state for the selected vehicle v, noted when the step's click on it cannot let him out.</summary>
        string TabState(ZObject v)
        {
            bool can = v.CanExitDriver(out string why);
            return $"tab shown {DriverShown}, live {hud.DriverTabLive}, selected {input.Selection.Count} (v in it {input.Selection.Contains(v)}), {Name(v)} #{v.Id} {v.Owner}, drivers {v.Drivers.Count}, "
                   + $"CanExitDriver {can} ({why ?? "-"}), first waypoint {(v.Waypoints.Count > 0 ? v.Waypoints[0].mode.ToString() : "none")}, moving {v.IsMoving}, on screen at {Screen(v)}";
        }
        /// <summary>D3: the click on EXIT DRIVER of a step; a Note with the tab's state when it is not shown or not live.</summary>
        IEnumerator ClickTab(ZObject v, string step)
        {
            if (!DriverShown || !hud.DriverTabLive) Note($"{step}: the tab cannot let him out at the click: {TabState(v)}");
            if (DriverShown) yield return Click(Mid(hud.DriverExitButtonRect));
        }

        ZGame g; ZWorld W; OpenZHud hud; PlayerInput input; OpenZCursors cursors; Team me, foe;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);   // synthetic events reach the game only while the Game view has focus
            new GameObject("OZ_VehicleExitCheck").AddComponent<OzVehicleExitCheck>();
        }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            float speed = 1f, ui = OzText.UiScale; int view = OzFinalGateView.SelectedIndex();
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null; hud = OpenZHud.I; input = FindFirstObjectByType<PlayerInput>();
                cursors = FindFirstObjectByType<OpenZCursors>();
                if (!g || W == null || !hud || !input) { Report = Tag + " FAIL: no level playing (ZGame, OpenZHud, PlayerInput)"; Debug.LogWarning(Report); yield break; }
                if (!ZWorld.VehicleExitDeviation) { Report = Tag + " FAIL: EXIT DRIVER is off in this session ('Zod exact'): start a level in the normal mode"; Debug.LogWarning(Report); yield break; }
                me = hud.Player; foe = me == Team.Red ? Team.Blue : Team.Red; speed = g.gameSpeed; startMaxId = W.Objects.Count > 0 ? W.Objects.Max(o => o.Id) : 0;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                StartCoroutine(Peace());                                                                          // vehicle_exit_jump2: the level's own enemies never shoot the scenes
                yield return Main();
            }
            finally
            {
                if (g) g.gameSpeed = speed;
                if (!Mathf.Approximately(OzText.UiScale, ui) && hud) { OzText.UiScale = ui; hud.ApplyUiScale(); }
                OzFinalGateView.Select(view);
                Cleanup();
                Report = $"{Tag} {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, ReportFile), Report); } catch { } }   // vehicle_exit_jump fix round 3: the whole check, Owner Frames and a part run each their own file
                Debug.Log(Report);
                Running = false; Only = null; Destroy(gameObject);                                       // vehicle_exit_jump fix round 3: the next menu runs what it asks for (PlayStart sets Only)
            }
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);

        // ------------------------------------------------------------------ the real mouse and keyboard
        static void MouseAt(Vector2 p, bool left = false, bool right = false)
        {
            var st = new MouseState { position = p };
            if (left) st = st.WithButton(MouseButton.Left);
            if (right) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        IEnumerator Click(Vector2 p, bool right = false)
        {
            MouseAt(p); yield return Frames(2);
            MouseAt(p, !right, right); yield return Frames(2);
            MouseAt(p); yield return Frames(3);
            Park(p);
        }
        /// <summary>vehicle_exit_jump3 (check fault, E2b runs 2 and 3: rows 4, 6a, 6b, 7b, 8b, 11a and J1 heavy / mech failed with their units off
        /// screen, the mech at x 2044 of 1920): the RTS camera scrolls while the mouse rests at the screen's edge (RtsCamera edgePx); a click near an
        /// edge (or past it after a Game view size change) leaves the mouse there: it is moved to the screen's middle.</summary>
        static void Park(Vector2 p)
        {
            float m = 24f;
            if (p.x < m || p.y < m || p.x > UnityEngine.Screen.width - m || p.y > UnityEngine.Screen.height - m) MouseAt(new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f));
        }
        /// <summary>vehicle_exit_jump3: the scene's camera focus (set when the scene is built) and the flights the check made to keep it there.</summary>
        Vector3 homeFocus; bool homeSet; int camFlights;
        /// <summary>vehicle_exit_jump3: the camera back over the scene when its focus left it by more than HomeTolM (noted).</summary>
        IEnumerator Home(string step)
        {
            var rts = FindFirstObjectByType<RtsCamera>(); if (!rts || !homeSet) yield break;
            MouseAt(new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f));
            var d = rts.focus - homeFocus; d.y = 0f; if (d.magnitude <= HomeTolM) yield break;
            Note($"{step}: the camera's focus was {d.magnitude:0.0} m off the scene: flown back (check staging)"); camFlights++;
            rts.FlyTo(homeFocus); float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < 3f && rts.Flying) yield return null; yield return Real(0.3f);
        }
        /// <summary>vehicle_exit_jump3: before a click on a unit: it at least EdgeKeepPx inside the screen, else the camera flown to it (noted).</summary>
        IEnumerator OnScreen(ZObject o, string step)
        {
            if (o == null) yield break;
            var rts = FindFirstObjectByType<RtsCamera>(); if (!rts) yield break;
            var s = Camera.main.WorldToScreenPoint(g.CenterWorld(o));
            if (s.z > 0f && s.x >= EdgeKeepPx && s.x <= UnityEngine.Screen.width - EdgeKeepPx && s.y >= EdgeKeepPx && s.y <= UnityEngine.Screen.height - EdgeKeepPx) yield break;
            Note($"{step}: {Name(o)} #{o.Id} was off the middle of the screen at ({s.x:0}, {s.y:0}): the camera flown to it (check staging)"); camFlights++;
            MouseAt(new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f));
            rts.FlyTo(g.CenterWorld(o)); float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < 3f && rts.Flying) yield return null; yield return Real(0.3f);
        }
        /// <summary>Two left clicks one frame apart (well inside PlayerInput's 0.35 s double-click window).</summary>
        IEnumerator DoubleClick(Vector2 p)
        {
            MouseAt(p); yield return Frames(2);
            MouseAt(p, true); yield return Frames(1);
            MouseAt(p); yield return Frames(1);
            MouseAt(p, true); yield return Frames(1);
            MouseAt(p); yield return Frames(3);
            Park(p);
        }
        IEnumerator Drag(Vector2 a, Vector2 b)
        {
            MouseAt(a); yield return Frames(2);
            MouseAt(a, true); yield return Frames(2);
            for (int i = 1; i <= 6; i++) { MouseAt(Vector2.Lerp(a, b, i / 6f), true); yield return Frames(1); }
            MouseAt(b); yield return Frames(3);
            Park(b);
        }
        IEnumerator PressKey(Key k)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); yield return Frames(2);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return Frames(3);
        }
        Vector2 Screen(ZObject o) { var p = Camera.main.WorldToScreenPoint(g.CenterWorld(o)); return new Vector2(p.x, p.y); }
        Vector2 Screen(Vector3 w) { var p = Camera.main.WorldToScreenPoint(w); return new Vector2(p.x, p.y); }
        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, c[0]), b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        static Vector2 Mid(RectTransform rt) => ScreenRect(rt).center;
        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

        // ------------------------------------------------------------------ units
        ZObject Unit(string cat, string type, Team t, int tx, int ty, int amount = -1) => UnitPx(cat, type, t, tx * 16, ty * 16, amount);
        /// <summary>A unit with its top-left at (x, y) px (robots 16 px, vehicles and guns 32 px square).</summary>
        ZObject UnitPx(string cat, string type, Team t, int x, int y, int amount = -1)
        {
            var o = cat == "robot" ? W.CreateRobotGroup(type, x, y, t, amount) : W.CreateObject(cat, type, x, y, t);
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            made.Add(o); made.AddRange(o.Minions);
            return o;
        }
        void Kill(ZObject o) { if (o == null || o.Destroyed || o.KillAt >= 0) return; o.SetHealth(0); W.UpdateObjectHealth(o); }
        int startMaxId = int.MaxValue;
        /// <summary>vehicle_exit_jump2 (check fault, the first jump kit's run on L17: the level's own CPU units destroyed S1p's and K1's tanks before
        /// the click and shot the drivers of K1, P and E): every enemy object this check did not make has its Damage set to 0 while the check runs
        /// (every 0.5 s, for new ones too), put back at the end; the check's own enemies (S2's snipers) keep theirs.</summary>
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();
        IEnumerator Peace()
        {
            while (Running && W != null)
            {
                foreach (var o in W.Objects) if (o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.Damage > 0 && !made.Contains(o) && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        void Cleanup()
        {
            if (W == null) return;
            foreach (var kv in quieted) if (kv.Key != null && !kv.Key.Destroyed) kv.Key.Damage = kv.Value;           // vehicle_exit_jump2: the level's own enemies as they were
            if (quieted.Count > 0) Note($"the level's own enemies quieted for the check: {quieted.Count} (their Damage put back)");
            quieted.Clear();
            if (input) input.Selection.Clear();
            var ids = new HashSet<int>(made.Select(o => o.Id)); ids.UnionWith(created);
            foreach (var o in new List<ZObject>(W.Objects))                                                   // + every robot let out meanwhile beside one of ours
                if (o.IsRobot && o.Id > startMaxId && made.Any(v => v.Kind != ZKind.Robot && Mathf.Abs(v.CenterX - o.CenterX) < 96 && Mathf.Abs(v.CenterY - o.CenterY) < 96)) ids.Add(o.Id);
            foreach (var o in new List<ZObject>(W.Objects)) if (ids.Contains(o.Id)) Kill(o);
        }
        readonly HashSet<int> created = new HashSet<int>();
        ZObject OutOf(ZObject v)
        {
            foreach (var o in W.Objects) if (o.IsRobot && o.Owner == me && !o.Destroyed && o.KillAt < 0 && !made.Contains(o) && !created.Contains(o.Id) && LeftBy(o, v) && Mathf.Abs(o.CenterX - v.CenterX) < 64 && Mathf.Abs(o.CenterY - v.CenterY) < 64) { created.Add(o.Id); return o; }   // D3: LeftBy (batch C's 14b / 16b / 16c took a passing L17 robot)
            return null;
        }

        bool TileFree(int x, int y) => W.Path.TilePassable(x, y, true) && W.Path.TilePassable(x, y, false) && !W.LavaAtPx(x * 16 + 8, y * 16 + 8);
        bool AreaFree(int x, int y, int w, int h)
        {
            if (x < 3 || y < 3) return false;
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) if (!TileFree(x + i, y + j)) return false;
            return !W.Objects.Any(o => !o.Destroyed && o.KillAt < 0 && o.Kind != ZKind.MapItem && o.LocX < (x + w) * 16 && o.LocX + o.WidthPix > x * 16 && o.LocY < (y + h) * 16 && o.LocY + o.HeightPix > y * 16)
                   && !W.Objects.Any(o => !o.Destroyed && o.Kind == ZKind.Building && o.LocX < (x + w + 2) * 16 && o.LocX + o.WidthPix > (x - 2) * 16 && o.LocY < (y + h + 2) * 16 && o.LocY + o.HeightPix > (y - 2) * 16);
        }
        /// <summary>Unity batch C (6 Nov 2026), check-only: no shipped level has a free 26 x 14-tile block (L02 / L07 / L16: never passable that wide;
        /// L17: only its car park rows, with parked neutral vehicles on them). Then the whole map is searched for a block whose every tile is passable
        /// for robots and vehicles, with no building within 2 tiles and only NEUTRAL non-building objects on it (parked vehicles, guns); those are
        /// taken out (ZWorld.RemoveNow, as the counted checks do), the block is used. The rules and the view are not touched.</summary>
        bool ClearedBlock(int w, int h, out int x0, out int y0)
        {
            for (int y = 3; y + h < W.Path.H; y++)
                for (int x = 3; x + w < W.Path.W; x++)
                {
                    bool ok = true;
                    for (int j = 0; j < h && ok; j++) for (int i = 0; i < w && ok; i++) if (!TileFree(x + i, y + j)) ok = false;
                    if (!ok) continue;
                    var inside = new List<ZObject>();
                    foreach (var o in W.Objects)
                    {
                        if (o.Destroyed || o.KillAt >= 0) continue;
                        if (o.Kind == ZKind.Building && o.LocX < (x + w + 2) * 16 && o.LocX + o.WidthPix > (x - 2) * 16 && o.LocY < (y + h + 2) * 16 && o.LocY + o.HeightPix > (y - 2) * 16) { ok = false; break; }
                        if (o.Kind != ZKind.MapItem && o.LocX < (x + w) * 16 && o.LocX + o.WidthPix > x * 16 && o.LocY < (y + h) * 16 && o.LocY + o.HeightPix > y * 16)
                        { if (o.Owner != Team.Neutral) { ok = false; break; } inside.Add(o); }
                    }
                    if (!ok) continue;
                    int gone = 0; foreach (var o in inside) gone += W.RemoveNow(o);
                    Note($"batch C: no free 26 x 14 block near the camera; tiles ({x}, {y}) .. ({x + w - 1}, {y + h - 1}) used after taking out {gone} parked neutral object(s)");
                    x0 = x; y0 = y; return true;
                }
            x0 = 0; y0 = 0; return false;
        }
        /// <summary>A w x h tile block near tile (fx, fy), every tile passable for robots and vehicles, no unit on it, no building within 2 tiles.</summary>
        bool BlockNear(int fx, int fy, int w, int h, int reach, out int x0, out int y0)
        {
            for (int r = 0; r <= reach; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = fx + dx - w / 2, y = fy + dy - h / 2;
                        if (AreaFree(x, y, w, h)) { x0 = x; y0 = y; return true; }
                    }
            x0 = fx; y0 = fy; return false;
        }
        bool Block(int w, int h, out int x0, out int y0)
        {
            g.GroundPoint(Camera.main.ScreenPointToRay(new Vector3(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f, 0)), out var c);
            var f = ZGame.WorldToPx(c);
            return BlockNear(f.x >> 4, f.y >> 4, w, h, 30, out x0, out y0);
        }

        IEnumerator Picture(string file, Rect around)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                var r = Clip(around, tex);
                var crop = new Texture2D(Mathf.Max(1, (int)r.width), Mathf.Max(1, (int)r.height), TextureFormat.RGB24, false);
                crop.SetPixels(tex.GetPixels((int)r.xMin, (int)r.yMin, crop.width, crop.height)); crop.Apply();
                File.WriteAllBytes(Path.Combine(Folder, file), crop.EncodeToPNG()); Destroy(crop);
                Note("picture " + file);
            }
            catch (System.Exception ex) { Note("picture " + file + " failed: " + ex.Message); }
            finally { if (tex) Destroy(tex); }
        }
        static Rect Clip(Rect a, Texture2D tex) => Rect.MinMaxRect(Mathf.Clamp(a.xMin, 0, tex.width - 2), Mathf.Clamp(a.yMin, 0, tex.height - 2), Mathf.Clamp(a.xMax, 1, tex.width), Mathf.Clamp(a.yMax, 1, tex.height));

        /// <summary>A frame sheet: cell i of a cols x rows sheet filled with the screen crop `around` now (end of this frame).</summary>
        IEnumerator SheetCell(Texture2D sheet, int i, int cols, int rows, Rect around)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                int cw = sheet.width / cols, ch = sheet.height / rows;
                var r = Clip(around, tex);
                int w = Mathf.Min(cw, (int)r.width), h = Mathf.Min(ch, (int)r.height);
                var px = tex.GetPixels((int)r.xMin, (int)r.yMin, w, h);
                sheet.SetPixels((i % cols) * cw, (rows - 1 - i / cols) * ch, w, h, px);
            }
            catch (System.Exception ex) { Note("frame " + i + " failed: " + ex.Message); }
            finally { if (tex) Destroy(tex); }
        }
        void SaveSheet(Texture2D sheet, string file)
        {
            if (!sheet) return;
            try { sheet.Apply(); if (!string.IsNullOrEmpty(Folder)) { File.WriteAllBytes(Path.Combine(Folder, file), sheet.EncodeToPNG()); Note("frame sheet " + file); } }
            catch (System.Exception ex) { Note("frame sheet " + file + " failed: " + ex.Message); }
            Destroy(sheet);
        }

        bool ExitCrewShown => hud.ExitButtonRect && hud.ExitButtonRect.gameObject.activeInHierarchy;
        bool DriverShown => hud.DriverExitButtonRect && hud.DriverExitButtonRect.gameObject.activeInHierarchy;

        /// <summary>Select one unit with a real click on it; wait past the tab's 0.4 s.</summary>
        IEnumerator Select(ZObject o)
        {
            yield return OnScreen(o, "select");                                                              // vehicle_exit_jump3: the unit on screen before its click
            input.Selection.Clear(); yield return Frames(2);
            yield return Click(Screen(o));
            yield return Real(OpenZHud.DriverTabShowAfterSec + 0.15f);
        }

        /// <summary>A sound log entry that really played (not dropped, skipped, held or waiting).</summary>
        static bool Played(OzSoundLog.Entry e)
        {
            var o = e.outcome ?? "";
            return !o.StartsWith("DROP") && !o.Contains("dropped") && !o.Contains("skipped") && !o.StartsWith("held") && !o.StartsWith("waits") && !o.StartsWith("cheer");
        }

        static string Name(ZObject v) => v.Type == "light" ? "light tank" : v.Type == ZMTank.Type ? "medium tank" : v.Type == "heavy" ? "heavy tank" : v.Type == ZMTank.Mech ? "mech" : v.Type;
        static float Wrap180(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }

        IEnumerator Main()
        {
            if (!string.IsNullOrEmpty(Only)) Note("D3: only the step groups " + Only + " (OzVehicleExitCheck.Only)");
            if (Only != null && Only.Trim() == "14c") { yield return JumpTurnedBesideBuilding(); yield break; }   // level 2's own row, no free block needed (fix round 2: with the jump)
            bool framesOnly = Only != null && Only.Trim() == "frames";                                       // the menu Owner Frames: the sheets only
            if (!Block(26, 14, out int x0, out int y0) && !ClearedBlock(26, 14, out x0, out y0)) { Log("1 free ground near the camera", false, "no 26 x 14-tile stretch of open ground found: move the camera to open ground"); yield break; }   // batch C (6 Nov): ClearedBlock, see there
            var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) rts.FlyTo(ZGame.PxToWorld((x0 + 13) * 16, (y0 + 7) * 16));
            yield return Real(2.5f);
            homeFocus = ZGame.PxToWorld((x0 + 13) * 16, (y0 + 7) * 16); homeFocus.y = 0f; homeSet = true;          // vehicle_exit_jump3: the scene's focus (Home)
            // the layout (tiles from the block's corner): row 1 the four and the resting tank; the gun (2, 6) with steps 7b/8 east of it;
            // 5d (13, 6), 6a (17, 6), 6b (22, 7), 7a (2, 10) and (6, 10), 10 (11, 11) with its ring of hulls
            var light = Unit("vehicle", "light", me, x0 + 1, y0 + 1);
            var mtank = Unit("vehicle", ZMTank.Type, me, x0 + 6, y0 + 1);
            var heavy = Unit("vehicle", "heavy", me, x0 + 11, y0 + 1);
            var mech = Unit("vehicle", ZMTank.Mech, me, x0 + 16, y0 + 1);
            var gun = Unit("cannon", "gatling", me, x0 + 2, y0 + 6);
            var rest = Unit("vehicle", "light", me, x0 + 22, y0 + 1);                                       // left idle: its crew rests in the open hatch (DV75) for 5c
            yield return Real(1.5f);
            Log("1 a red light tank, medium tank, heavy tank, mech, gatling and a second light tank, each with a grunt inside", new[] { light, mtank, heavy, mech, gun, rest }.All(o => o.Owner == me && o.Drivers.Count == 1),
                $"at tiles ({x0}, {y0}) .. ({x0 + 25}, {y0 + 13})");

            // 2 select by a click on the hull; the real cursor over it
            if (Do("2"))
            {
            yield return Home("2");
            yield return Select(light);
            MouseAt(Screen(light)); yield return Frames(4);
            string cur = cursors ? cursors.Current : "(no OpenZCursors)";
            Log("2 a left click on the light tank's hull selects it: EXIT DRIVER shows, EXIT CREW not; the cursor over the hull is not the EXIT cursor",
                input.Selection.Count == 1 && input.Selection[0] == light && DriverShown && !ExitCrewShown && hud.DriverTabLive && cursors && cur != "exit",
                $"selected {input.Selection.Count} ({(input.Selection.Count > 0 ? input.Selection[0].Type : "-")}), EXIT DRIVER {DriverShown} (live {hud.DriverTabLive}), EXIT CREW {ExitCrewShown}, the cursor over the hull \"{cur}\"");

            }

            // 3 right-clicks on the hull and the ground round it
            if (Do("3"))
            {                                                                                                   // D3: steps 3, 3b, 3c
            yield return Home("3");
            int exits0 = W.DriverExits;
            yield return Click(Screen(light), true);
            var around = new List<Vector3>();
            for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f, d = 1f + 2f * (k % 3) / 2f; around.Add(new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d)); }
            foreach (var off in around) { yield return Click(Screen(g.CenterWorld(light) + off), true); yield return Real(0.15f); }
            yield return Real(0.8f);
            Log("3 right-clicks on the light tank's hull and on 8 ground points 1 to 3 m round it", W.DriverExits == exits0 && light.Owner == me && light.Drivers.Count == 1,
                $"drivers let out {W.DriverExits - exits0}, the tank {light.Owner} with {light.Drivers.Count} driver(s)");
            W.SendWaypoints(me, light, new List<ZWaypoint>());                                              // the empty order: it stops (the sim's own STOP)
            yield return Real(1f);

            // 3b left clicks and double-clicks on the hull and the ground while the tab shows
            {
                int e0 = W.DriverExits, tries = 0, shownAt = 0;
                var points = new List<Vector3> { Vector3.zero }; points.AddRange(around);
                int flown = 0; var rts3 = FindFirstObjectByType<RtsCamera>();
                foreach (var off in points)
                    foreach (bool dbl in new[] { false, true })
                    {
                        var sp = Screen(light);
                        if (rts3 && (sp.x < 200f || sp.x > UnityEngine.Screen.width - 200f || sp.y < 200f || sp.y > UnityEngine.Screen.height - 200f))
                        {   // vehicle_exit_jump2 (check fault: in the first jump kit's run a click of this step moved the camera and the tank left the screen at try 10): fly back to it
                            Note($"3b try {tries + 1}: the light tank was off the middle of the screen at {sp} (the camera moved by the last click at {off}): the camera flown back to it");
                            flown++; rts3.FlyTo(g.CenterWorld(light)); yield return Real(1.5f);
                        }
                        yield return Select(light);
                        if (DriverShown) shownAt++;
                        else Note($"3b try {tries + 1} ({(dbl ? "double-click" : "click")} at {off}): no tab after the select: {TabState(light)}");   // D3 diagnostics
                        var p = Screen(g.CenterWorld(light) + off);
                        if (dbl) yield return DoubleClick(p); else yield return Click(p);
                        yield return Real(0.45f); tries++;                                                         // D3: past PlayerInput's 0.35 s double-click window, so the next select's click is no double-click with this one
                    }
                yield return Real(0.5f);
                Log("3b while EXIT DRIVER shows: a left click and a double-click on the hull and on each of the 8 ground points", W.DriverExits == e0 && light.Owner == me && light.Drivers.Count == 1 && shownAt == tries,
                    $"{tries} clicks, the tab showing before {shownAt} of them; drivers let out {W.DriverExits - e0}; the tank {light.Owner} with {light.Drivers.Count} driver(s); the camera flown back to it {flown} time(s)");
            }

            // 3c an attack order with a right-click on an enemy
            {
                var enemy = Unit("robot", "grunt", foe, (light.CenterX >> 4) + 3, (light.CenterY >> 4) + 3, 1);
                yield return Real(0.4f);
                int e0 = W.DriverExits;
                yield return Select(light);
                yield return Click(Screen(enemy), true);
                bool attack = light.AttackObject == enemy || light.Waypoints.Any(w => w.mode == WpMode.Attack && w.refId == enemy.Id) || enemy.Destroyed || enemy.KillAt >= 0;   // asked right after the release (a shot may already have killed it)
                yield return Real(0.5f);
                Log("3c a right-click on an enemy grunt with the light tank selected: it attacks, nobody gets out", attack && W.DriverExits == e0 && light.Owner == me && light.Drivers.Count == 1,
                    $"the tank's target {(light.AttackObject != null ? light.AttackObject.Type + " #" + light.AttackObject.Id : "none")}, attack order {attack}; drivers let out {W.DriverExits - e0}");
                Kill(enemy);
                W.SendWaypoints(me, light, new List<ZWaypoint>());
                yield return Real(1f);
            }

            }

            // 4 the two tabs at both sizes and three UI scales
            if (Do("4"))
            {
                yield return Home("4");
                float s0 = OzText.UiScale; int ok4 = 0, n4 = 0;
                foreach (var scr in Screens)
                    foreach (float sc in new[] { OzText.ScaleMin, s0, OzText.ScaleMax }.Distinct())
                    {
                        OzFinalGateView.SetFixed(scr.x, scr.y, "OpenZ check " + scr.x + "x" + scr.y, out string info);
                        OzText.UiScale = sc; hud.ApplyUiScale();
                        yield return Real(0.8f);
                        yield return Select(gun);
                        bool crewOn = ExitCrewShown; Rect rc = crewOn ? ScreenRect(hud.ExitButtonRect) : default;
                        string tagS = $"{UnityEngine.Screen.width}x{UnityEngine.Screen.height}_ui{Mathf.RoundToInt(sc * 100)}";
                        if (crewOn) yield return Picture($"tab_exit_crew_{tagS}.png", Rect.MinMaxRect(rc.xMin - 160, rc.yMin - 60, rc.xMax + 40, rc.yMax + 40));
                        yield return Select(heavy);
                        bool drvOn = DriverShown; Rect rd = drvOn ? ScreenRect(hud.DriverExitButtonRect) : default;
                        if (drvOn) yield return Picture($"tab_exit_driver_{tagS}.png", Rect.MinMaxRect(rc.xMin - 160, rc.yMin - 60, rc.xMax + 40, rc.yMax + 40));
                        float kw = crewOn && drvOn ? rd.width / rc.width : 0f, kh = crewOn && drvOn ? rd.height / rc.height : 0f;
                        bool inside = drvOn && rd.xMin >= 0 && rd.yMin >= 0 && rd.xMax <= UnityEngine.Screen.width && rd.yMax <= UnityEngine.Screen.height;
                        var panel = hud.UnitPanelRect; float leftU = 0f;
                        if (panel && drvOn) { var c = new Vector3[4]; hud.DriverExitButtonRect.GetWorldCorners(c); leftU = panel.InverseTransformPoint(c[0]).x - panel.rect.xMin; }
                        bool ok = crewOn && drvOn && Mathf.Abs(kw - 0.8f) <= 0.02f && Mathf.Abs(kh - 0.8f) <= 0.02f && Mathf.Abs(rd.xMax - rc.xMax) <= 1f && Mathf.Abs(rd.yMin - rc.yMin) <= 1f && inside && leftU > 268f
                                  && UnityEngine.Screen.width == scr.x && UnityEngine.Screen.height == scr.y;
                        n4++; if (ok) ok4++;
                        Log($"4 the tabs at {UnityEngine.Screen.width} x {UnityEngine.Screen.height}, UI scale {sc * 100:0} %", ok,
                            $"EXIT CREW {rc.width:0.0} x {rc.height:0.0} px at x {rc.xMin:0}..{rc.xMax:0}, y {rc.yMin:0}..{rc.yMax:0}; EXIT DRIVER {rd.width:0.0} x {rd.height:0.0} px at x {rd.xMin:0}..{rd.xMax:0}, y {rd.yMin:0}..{rd.yMax:0}; "
                            + $"ratio {kw:0.000} x {kh:0.000}; right edges {rc.xMax - rd.xMax:+0.0;-0.0} px, bottom edges {rc.yMin - rd.yMin:+0.0;-0.0} px apart; inside the screen {inside}; its left edge {leftU:0} u from the panel's left (the under-attack tab ends at 268) ({info})");
                    }
                OzText.UiScale = s0; hud.ApplyUiScale();
                OzFinalGateView.SetFixed(1920, 1080, "OpenZ check 1920x1080", out _);
                yield return Real(0.8f);
                yield return Home("4 (after the screen sizes)");                                                // vehicle_exit_jump3: the size changes left the mouse at an edge
            }

            // J1 vehicle_exit_jump: a click on the tab for each of the four: the jump off, measured frame by frame (the old step 5's climb is gone)
            if (Do("J") || framesOnly)
                foreach (var v in new[] { light, mtank, heavy, mech })
                {
                    yield return Home("J1 " + Name(v));
                    yield return Select(v);
                    bool shown = DriverShown;
                    var j = new Jump();
                    yield return WatchExit(v, j, $"exit_jump_{v.Type}_frames.png");
                    JumpVerdict("J1 " + Name(v) + ": a click on EXIT DRIVER: he jumps off with one somersault and lands on his feet", j, shown);
                }
            if (framesOnly) { yield return StopRows(true); yield break; }                                    // the owner frames: J1's four sheets, S1's mech, S2's sniped light tank

            // 6 a pyro sent in gets in at once (inside the lock); robots beside an emptied tank: nobody climbs in for 30 s
            if (Do("6"))
            {
                yield return Home("6");
                var m6 = Unit("vehicle", ZMTank.Mech, me, x0 + 17, y0 + 6);
                yield return Real(1f);
                yield return Select(m6);
                yield return ClickTab(m6, "6a");   // D3: a Note when the tab cannot let him out
                yield return Real(1.5f);
                var pyro = Unit("robot", "pyro", me, (m6.CenterX >> 4) - 3, (m6.CenterY >> 4) + 3, 1);
                yield return Real(0.5f);
                yield return Select(pyro);
                bool sel = input.Selection.Contains(pyro), locked = m6.DriverOutLocked;
                yield return OnScreen(m6, "6a");
                float t0 = Time.unscaledTime;
                yield return Click(Screen(m6), true);
                while (Time.unscaledTime - t0 < 10f && m6.Owner != me) yield return null;
                Log("6a a pyro selected with a click and sent with a right-click on the emptied mech (its lock on) gets in at once", sel && locked && m6.Owner == me && m6.DriverType == "pyro" && m6.DriverOutLockUntilTick == -1,
                    $"selected {sel}, lock on at the order {locked}; the mech {m6.Owner}, driver {m6.DriverType}, in {Time.unscaledTime - t0:0.0} s after the click; lock {m6.DriverOutLockUntilTick}");

                var l6 = Unit("vehicle", "light", me, x0 + 22, y0 + 7);
                yield return Real(1f);
                yield return Select(l6);
                yield return ClickTab(l6, "6b");   // D3: a Note when the tab cannot let him out
                yield return Real(0.5f);
                var sq1 = Unit("robot", "grunt", me, (l6.CenterX >> 4) - 2, (l6.CenterY >> 4) + 2, 3);
                var sq2 = Unit("robot", "grunt", me, (l6.CenterX >> 4) + 2, (l6.CenterY >> 4) - 2, 3);
                input.Selection.Clear();
                long lockEnd = l6.DriverOutLockUntilTick, start = W.TickIndex;
                g.gameSpeed = 4f;
                while (W.TickIndex < lockEnd - 30 && l6.Owner == Team.Neutral) yield return null;
                bool held = l6.Owner == Team.Neutral; long heldTo = W.TickIndex;
                while (W.TickIndex < lockEnd + 600 && l6.Owner == Team.Neutral) yield return null;           // and after it someone may (the normal rule)
                g.gameSpeed = 1f;
                Log("6b two red squads and the driver beside an emptied light tank: nobody climbs in by himself for 30 s", held && lockEnd > 0 && lockEnd - start >= ZObject.DriverOutLockTicks - 200,
                    $"empty from tick {start} to {heldTo} (the lock ends at {lockEnd}); after the lock: {(l6.Owner == Team.Neutral ? "still empty 6 s later" : $"taken by {l6.DriverType} at tick {W.TickIndex}")}");
            }

            // 7 two fresh tanks in a box and one click; one tank and the E key
            if (Do("7"))
            {
                yield return Home("7");
                var a7 = Unit("vehicle", ZMTank.Type, me, x0 + 2, y0 + 10);
                var b7 = Unit("vehicle", "heavy", me, x0 + 6, y0 + 10);
                yield return Real(1.2f);
                yield return OnScreen(a7, "7a"); yield return OnScreen(b7, "7a");
                input.Selection.Clear(); yield return Frames(2);
                yield return Drag(Screen(a7) + new Vector2(-50, 50), Screen(b7) + new Vector2(50, -50));
                yield return Real(OpenZHud.DriverTabShowAfterSec + 0.2f);
                int n = input.Selection.Count(o => o == a7 || o == b7);
                if (DriverShown) yield return Click(Mid(hud.DriverExitButtonRect));
                yield return Real(1f);
                Log("7a a box round a medium and a heavy tank, one click on EXIT DRIVER: both drivers out", n == 2 && a7.Owner == Team.Neutral && b7.Owner == Team.Neutral,
                    $"boxed {n} of 2; medium tank {a7.Owner}, heavy tank {b7.Owner}");
                var t2 = Unit("vehicle", "light", me, (gun.CenterX >> 4) + 6, gun.CenterY >> 4);
                yield return Real(0.8f);
                yield return Select(t2);
                yield return PressKey(OzBindings.KeyOf(OzAction.ExitCrew));
                yield return Real(1f);
                Log($"7b one light tank selected and the EXIT CREW key ({OzBindings.Cap(OzAction.ExitCrew)}): its driver out", t2.Owner == Team.Neutral, $"the tank {t2.Owner}");
            }

            // 8 a gun and a tank together
            if (Do("8"))
            {
                yield return Home("8");
                var t3 = Unit("vehicle", "light", me, (gun.CenterX >> 4) + 3, gun.CenterY >> 4);
                yield return Real(0.8f);
                yield return OnScreen(gun, "8a"); yield return OnScreen(t3, "8a");
                input.Selection.Clear(); yield return Frames(2);
                yield return Drag(Screen(gun) + new Vector2(-50, 50), Screen(t3) + new Vector2(50, -50));
                yield return Real(OpenZHud.DriverTabShowAfterSec + 0.2f);
                bool both = input.Selection.Contains(gun) && input.Selection.Contains(t3), crew = ExitCrewShown, drv = DriverShown;
                int held0 = OpenZHud.DriverTabHeldBack, e8 = W.DriverExits;
                if (crew) yield return DoubleClick(Mid(hud.ExitButtonRect));                                   // fix round 2: the second click lands where EXIT DRIVER would appear
                yield return Real(1f);
                Log("8a a gun and a light tank in one box: EXIT CREW only; a real double-click on EXIT CREW lets the gun's crew out, the tank keeps its driver",
                    both && crew && !drv && gun.Owner == Team.Neutral && t3.Owner == me && t3.Drivers.Count == 1 && W.DriverExits == e8,
                    $"both selected {both}; EXIT CREW {crew}, EXIT DRIVER {drv}; gun {gun.Owner}, tank {t3.Owner} with {t3.Drivers.Count} driver(s); drivers let out {W.DriverExits - e8}; the tab held back {OpenZHud.DriverTabHeldBack - held0} frames");
                {
                    var gun2 = Unit("cannon", "gatling", me, x0 + 17, y0 + 11);
                    var t3b = Unit("vehicle", "light", me, x0 + 20, y0 + 11);
                    yield return Real(1.2f);
                    yield return OnScreen(gun2, "8b"); yield return OnScreen(t3b, "8b");
                    input.Selection.Clear(); yield return Frames(2);
                    yield return Drag(Screen(gun2) + new Vector2(-50, 50), Screen(t3b) + new Vector2(50, -50));
                    yield return Real(OpenZHud.DriverTabShowAfterSec + 0.2f);
                    bool both2 = input.Selection.Contains(gun2) && input.Selection.Contains(t3b);
                    int keyHeld0 = OpenZHud.DriverKeyHeldBack, e8b = W.DriverExits;
                    yield return PressKey(OzBindings.KeyOf(OzAction.ExitCrew));
                    yield return PressKey(OzBindings.KeyOf(OzAction.ExitCrew));                                 // the second press about 0.1 s after the first
                    yield return Real(0.25f);
                    bool kept = gun2.Owner == Team.Neutral && t3b.Owner == me && t3b.Drivers.Count == 1 && W.DriverExits == e8b;
                    yield return Real(0.6f);
                    yield return PressKey(OzBindings.KeyOf(OzAction.ExitCrew));                                 // a deliberate press later
                    yield return Real(0.8f);
                    Log("8b a second gun and light tank in one box: E pressed twice quickly lets the gun's crew out, the tank keeps its driver; E again 0.6 s later lets its driver out",
                        both2 && kept && t3b.Owner == Team.Neutral,
                        $"both selected {both2}; after the two presses: gun {gun2.Owner}, the tank kept its driver {kept} (key presses held back {OpenZHud.DriverKeyHeldBack - keyHeld0}); after the third: the tank {t3b.Owner}");
                }

                // 9 a tank driving
                yield return Select(t3);
                var far = g.CenterWorld(t3) + new Vector3(-14f, 0, -4f);
                yield return Click(Screen(far), true);
                yield return Real(1.2f);
                bool driving = t3.IsMoving;
                yield return ClickTab(t3, "9");   // D3: a Note when the tab cannot let him out
                yield return Frames(2);
                double px = t3.PosX, py = t3.PosY;
                yield return Real(1f);
                double moved = System.Math.Sqrt((t3.PosX - px) * (t3.PosX - px) + (t3.PosY - py) * (t3.PosY - py));
                Log("9 a light tank driving: EXIT DRIVER stops it and lets the driver out", driving && t3.Owner == Team.Neutral && !t3.IsMoving && moved < 0.5,
                    $"driving at the click {driving}; then {t3.Owner}, moving {t3.IsMoving}, moved {moved:0.00} px in the next second");
            }

            // 10 a tank boxed in
            if (Do("10"))
            {
                yield return Home("10");
                var t4 = Unit("vehicle", "light", me, x0 + 11, y0 + 11);
                yield return Real(0.3f);
                int r1 = ZWorld.DriverExitRing1("light");
                for (int k = 0; k < 8; k++) UnitPx("vehicle", "heavy", Team.Neutral, t4.CenterX + ZWorld.DriverExitDx[k] * r1 - 16, t4.CenterY + ZWorld.DriverExitDy[k] * r1 - 16);   // on the first ring's spots: every spot of both rings is taken
                yield return Real(0.8f);
                input.Selection.Clear(); input.Selection.Add(t4);                                              // set, not clicked: a click there may hit one of the hulls round it
                yield return Real(0.5f);
                bool shown = DriverShown, live = hud.DriverTabLive; string tip = hud.DriverTabTip ?? "";
                float t0 = Time.unscaledTime;
                if (shown) yield return Click(Mid(hud.DriverExitButtonRect));
                yield return Real(0.4f);
                bool blip = OzSoundLog.Since(t0).Any(e => e.key != null && e.key.Contains("disabled"));             // ui_disabled (HudButton's click on a greyed button)
                if (shown && !string.IsNullOrEmpty(Folder)) { var rr = ScreenRect(hud.DriverExitButtonRect); yield return Picture("tab_exit_driver_greyed.png", Rect.MinMaxRect(rr.xMin - 160, rr.yMin - 60, rr.xMax + 40, rr.yMax + 40)); }
                Log("10 a light tank boxed in by 8 empty heavy tanks: the tab greyed with the reason, a click gives the disabled blip", shown && !live && tip.Contains("No free ground") && blip && t4.Owner == me,
                    $"shown {shown}, live {live}, tooltip \"{tip.Replace("|", " / ")}\", disabled blip {blip}, the tank {t4.Owner}");
            }

            // 11 a light tank where the tab appears: a double-click and a quick second click on it
            if (Do("11")) { yield return Home("11"); yield return UnderTheTab(); }


            // vehicle_exit_jump: the stop (S1, S2), the hull or the driver killed during the exit (K1, K2), the pause (P), a robot ordered in (E)
            if (camFlights > 0) Note($"the check flew the camera back to the scene {camFlights} time(s) so far (vehicle_exit_jump3's staging guard)");
            if (Do("S")) yield return StopRows(false);
            if (Do("K")) yield return KilledRows();
            if (Do("P") || Do("E")) yield return PauseAndEnter();
            // fix round 2 (review): the free-spot rule and his side with the jump (the vehicle_exit kit's 14 and 14c, ported); last, the game over mid-jump (it ends the level)
            if (Do("14")) yield return JumpBesideFlagAndBuilding();
            if (Do("14c")) yield return JumpTurnedBesideBuilding();
            if (Do("P2")) yield return GameEndMidJump();
        }

        // ------------------------------------------------------------------ helpers (fix round 4, kept)
        /// <summary>A unit taken away without a death or a wreck (the rules drop it the next tick; its view hides).</summary>
        void Gone(ZObject o) { if (o == null) return; o.KillAt = 0; foreach (var m in o.Minions) if (m != null) m.KillAt = 0; }
        static bool OzExitAlive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        /// <summary>Fix round 5 (review: vehicles have no colliders, a body killed on the deck could lie inside the hull): where the dead robot's body
        /// rests now: its hips (rag doll, collapse, melt) inside the hull's box under its deck = sunk (BoardingView.HatchUnderDeck); its model gone
        /// (a pop: its parts fly, nothing is left to lie there). Final fix (review r3): inside the turret's own box more than 0.3 m under its top is
        /// sunk too (BoardingView.HatchInTurret: a body fallen through the turret's mesh onto the deck).</summary>
        string BodyRest(ZObject r, ZObject v, out bool sunk)
        {
            sunk = false;
            if (r == null || !g.Views.TryGetValue(r.Id, out var lv) || !lv || !lv.gameObject.activeInHierarchy) return "gone (popped or faded)";
            var hips = lv.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Hips"));
            var p = hips ? hips.position : lv.transform.position;
            bool deck = BoardingView.HatchUnderDeck(v.Id, p), turret = BoardingView.HatchInTurret(v.Id, p);
            sunk = deck || turret;
            var px = ZGame.WorldToPx(p);
            return $"hips at px ({px.x}, {px.y}), {p.y - g.GroundY(p.x, p.z):0.00} m above the ground, {(deck ? "INSIDE THE HULL UNDER ITS DECK" : turret ? "INSIDE THE TURRET" : "not inside the hull or the turret")}";
        }
        /// <summary>The camera's focus tile (the steps build near it).</summary>
        void Focus(out int fx, out int fy)
        {
            var rts = FindFirstObjectByType<RtsCamera>();
            var c = ZGame.WorldToPx(rts ? rts.focus : Vector3.zero); fx = c.x >> 4; fy = c.y >> 4;
        }
        /// <summary>A crewed red vehicle of `type` on a free w x h block near tile (fx, fy), the camera over it (into[0] null: none).</summary>
        IEnumerator FreshTank(string type, int fx, int fy, ZObject[] into, int w = 6, int h = 6)
        {
            into[0] = null;
            if (!BlockNear(fx, fy, w, h, 40, out int bx, out int by)) yield break;
            var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) { rts.FlyTo(ZGame.PxToWorld((bx + w / 2) * 16, (by + h / 2) * 16)); yield return Real(1.6f); }
            into[0] = Unit("vehicle", type, me, bx + w / 2 - 1, by + h / 2 - 1);
            yield return Real(1.2f);
        }
        static Rect ScreenBox(Camera cam, Bounds b)
        {
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var s = cam.WorldToScreenPoint(c); if (s.z < 0) continue; mn = Vector2.Min(mn, s); mx = Vector2.Max(mx, s);
            }
            return Rect.MinMaxRect(mn.x, mn.y, mx.x, mx.y);
        }
        /// <summary>OpenZUnitOverlay's robot frame as its UnitRect builds it (0.55 x Tall round, 1.9 x Tall high, from the marked point), screen px.</summary>
        static Rect FrameOf(Camera cam, Vector3 p, float tall) => ScreenBox(cam, new Bounds(p + Vector3.up * 0.95f * tall, new Vector3(1.1f * tall, 1.9f * tall, 1.1f * tall)));
        static string Kind(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;
        /// <summary>The computer's attack order (ZBot.Command), through the rules' own command path.</summary>
        void CpuAttack(ZObject u, ZObject t) => W.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, attackTo = true, playerGiven = true, x = t.LocX + 8, y = t.LocY + 8 } });
        // ------------------------------------------------------------------ vehicle_exit_jump: one exit watched frame by frame
        /// <summary>What one exit did, frame by frame (J1, J1j, 14, S1, S2, K1, K2, E, P, P2). The rules and the vehicle are read in MeasureLoop
        /// (after Update, before LateUpdate: the turret's, the hull's and the drawn hull's baselines in the frame the rules first show it empty, as
        /// the last crewed frame drew them); fix round 2 (review): the figure is read in FigureLoop at the END of every frame (what is drawn: its
        /// bones, its solids' cuts, its joints, its hips' speed, its feet and tilt at the touch), never from BoardingView's record of itself.</summary>
        sealed class Jump
        {
            public ZObject v, robot; public float clickT, emptiedT = -1f; public int emptiedFrame = -1, frames, jumpFrames, cutFrames, noBoneFrames, markFrames, markMissing; public float worstCut; public string worstWhat = "";
            public float sizeMin = float.MaxValue, sizeMax, flip; public bool upInit; public Vector3 lastUp, axis;
            public int touchFrame = -1; public double touchSim = -1; public float touchFeet = float.NaN, touchTilt = -1f, touchT = -1f, touchDt = 1f / 60f, touchAnkle = float.NaN; public bool phaseBack; public int phaseRank = -1;
            public int simShotsBefore, simShotsAfter, simHgBefore, simDsBefore; public double simX = double.NaN, simY, simMoved; public bool splitSeen; public long lastTick = -1;
            public bool viewInit; public Vector3 viewPos; public float lastMoveT = -1f, movedAfterBrake;
            public Transform turret; public Quaternion turret0; public float turretTurn, turretOnHull0 = float.NaN; public bool turretSet;   // vehicle_exit_jump2: the turret's yaw on the hull
            public float hullYaw0 = float.NaN, turretYaw0 = float.NaN, hullYawBrake = float.NaN, turretYawBrake = float.NaN, hullTurnAfter, turretWorldTurnAfter, lastHullYaw = float.NaN;   // fix round 2: the hull's and the turret's world yaw
            public bool heldSeen; public double heldX, heldY, heldMoved;
            public float endT = -1f; public bool sawFigure, done; public Vector3 lastFigure; public float stopAt = -1f; public double stopSim = -1; public int stopFrame = -1;
            public int heardAt0 = -1, jetsAt0 = -1, mgRoundsAt0 = -1, flameLoopsAfter;
            public bool snipe; public int snipeX = int.MinValue, snipeY, snipeCentreMoved = -1;                                       // fix round 2: the sniper's own hit and the vehicle's centre at it (px)
            public readonly List<BoardingView.HatchSolid> solids = new List<BoardingView.HatchSolid>(); public bool solidsRead; public int boxSolids, colSolids, nearSolids;
            public readonly Dictionary<string, int> cutBy = new Dictionary<string, int>(); public int nearCutFrames; public string boxNames = "";
            public int jointFrames, jointViol; public string worstJoint = ""; public float worstJointBy;
            public Vector3 lastHips; public float lastHipsT = -1f, lastHipsGT = -1f; public string lastPhase = ""; public float vyLastCrouch = float.NaN, vyFirstAir = float.NaN;
            public float walkAnkleMin = float.MaxValue; public int walkFrames;
            public double clickSimRules = double.NaN; public float lidAtClick = -1f;
            // fix round 3 (review): the crouch on the drawn rim (frames; the lower ankle's height over the rim against his standing ankle, the
            // ankles' middle against the drawn take-off point, the root's height along the hull's up: least and most); the landing (frames, the
            // hips' fastest rise, their lowest point and its sim time, the stand-up's length); the first flight frame's time; the drawn length
            // hips -> head along the bones against his own model's (least and most ratio; the model's length)
            public int crouchFrames, crouchFeetFrames, landFrames; public float crouchAnkleDev, crouchFeetOff, rootUpMin = float.MaxValue, rootUpMax = float.MinValue;
            // review round 2 (vehicle_exit_jump2): the push's frames and the worst drawn sole point off the drawn take-off point there (crouchFeetOff:
            // the same in the crouch); the hips' speed out in the last push frame and the first flight frame; the drawn stop (StillStep): the last
            // frame the hull or turret moved, the frames it moved, the baseline frame after the emptied frame and from it the glide, nod and turns,
            // the frames read; a hit that rocked it (OzVehicleRock) freezes the reading
            public int pushFrames, pushFeetFrames; public float pushFeetOff, vhLastPush = float.NaN, vhFirstAir = float.NaN;
            public int lastMoveFrame = -1, moveFrames, baseFrame = -1, stillFrames, kicks0 = -1, kickFrame = -1; public bool lastUpSet, baseSet, baseTurret;
            public Vector3 lastHullUp, lastTurretUp, basePos, baseUp, baseTurretUp; public float lastTurretYaw, baseYaw, baseTurretYaw, glide, nod, hullYawAfter, turretYawAfter, turretNod;
            public float landRiseMax = float.NegativeInfinity, landLowY = float.MaxValue, standUpSec = -1f, firstAirT = -1f, firstAirDt = 1f / 60f; public double landLowSim = -1;
            public float lenMin = float.MaxValue, lenMax, liveLen = float.NaN; public int lenFrames;
            // vehicle_exit_jump3: the leap (the drawn hips' highest point in the flight, the drawn ankles' middle at the touch), the lid (game time
            // of its first move, its open, the show, the push's first frame; the lid at the show), the take-off against the plan (the exit's own
            // time and the drawn and planned hips of the last read; the last push frame's and the first flight frame's position error and speed,
            // drawn and planned; the plan's own speed at the push's end and the flight's start)
            public float airHipsMax = float.MinValue; public Vector3 touchAnkMid; public bool touchAnkSet;
            public float lidStartGT = -1f, lidOpenGT = -1f, showGT = -1f, pushGT = -1f, lidAtShow = -1f; public string firstPhase = "";
            public float prevTau = -1f; public Vector3 prevDrawnH, prevPlanH; public string prevPlanPhase = "";
            public float errLastPush = -1f, errFirstAir = -1f, kink = -1f; public Vector3 vPushD, vPushP, vAirD, vAirP; public bool vPushSet, vAirSet;
        }

        /// <summary>A shot of any weapon by v in the rules' events (the gun's roll and missile, the mech's hand gun, a hatch driver's weapon, a bolt).</summary>
        static bool ShotBy(ZEvent e, ZObject v) => v != null && e.objId == v.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired || e.type == ZEventType.MechHandGun
                                                                                    || e.type == ZEventType.DriverShot || e.type == ZEventType.ElectroBolt || e.type == ZEventType.ElectroCharge);
        static int Rank(string phase) => phase == "inside" ? 0 : phase == "crouch" ? 1 : phase == "push" ? 2 : phase == "air" ? 3 : phase == "land" ? 4 : phase == "walk" ? 5 : -1;   // vehicle_exit_jump2: + push
        static float YawOf(Transform t) { var f = Vector3.ProjectOnPlane(t.forward, Vector3.up); return f.sqrMagnitude > 1e-8f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : 0f; }
        /// <summary>vehicle_exit_jump2: the turret's yaw on the hull (deg): its forward against the hull's, both laid in the hull's plane (a recoil
        /// rock of the turret about its own side axis is no turn).</summary>
        static float YawOnHull(Transform turret, Transform hull)
        {
            var up = hull.up; var a = Vector3.ProjectOnPlane(hull.forward, up); var b = Vector3.ProjectOnPlane(turret.forward, up);
            return a.sqrMagnitude > 1e-8f && b.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(a, b, up) : 0f;
        }

        /// <summary>The vehicle's turret (or the mech's waist): the first node under its view named ...Yaw (not a pitch node); null: none.</summary>
        Transform TurretOf(ZObject v)
        {
            if (!g.Views.TryGetValue(v.Id, out var uv) || !uv) return null;
            foreach (var t in uv.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith("Yaw") && !t.name.Contains("Pitch")) return t;
            return null;
        }

        /// <summary>Measure every frame until the watch is over (its own coroutine: no frame is skipped while a sheet cell is captured); the figure
        /// at the end of every frame (FigureLoop).</summary>
        IEnumerator MeasureLoop(Jump j)
        {
            StartCoroutine(FigureLoop(j));
            while (!j.done) { Measure(j); yield return null; }
        }
        IEnumerator FigureLoop(Jump j)
        {
            var eof = new WaitForEndOfFrame();
            while (!j.done) { yield return eof; if (!j.done) Figure(j); }
        }

        /// <summary>One frame of the watch (after Update): the rules' shots, the vehicle's moves, hull and turret.</summary>
        void Measure(Jump j)
        {
            var v = j.v; if (v == null) return;
            bool empty = v.Owner == Team.Neutral || v.Drivers.Count == 0;
            if (W.TickIndex != j.lastTick)
            {   // this frame's ticks' events (once per frame); in the frame the rules first show it empty they are split at the vehicle's own Ejected
                // event or (fix round 2, review) at the sniper's own hit: the first Hitscan with its snipe flag on v in the tick of the Sniped event
                // (an event's tick: end - value): a shot of v after it in that frame counts as after
                j.lastTick = W.TickIndex;
                int split = -1;
                if (empty && j.stopSim < 0)
                {
                    double snipeTick = double.NaN;
                    foreach (var e in W.Events) if (e.type == ZEventType.Sniped && e.objId == v.Id) { snipeTick = e.end - e.value; break; }
                    for (int i = 0; i < W.Events.Count; i++)
                    {
                        var e = W.Events[i];
                        if (e.objId == v.Id && e.type == ZEventType.Ejected) { split = i; break; }
                        if (!double.IsNaN(snipeTick) && e.type == ZEventType.Hitscan && e.flag2 && e.otherId == v.Id && System.Math.Abs(e.end - snipeTick) < 1e-6) { split = i; j.snipe = true; j.snipeX = e.x; j.snipeY = e.y; break; }
                    }
                }
                for (int i = 0; i < W.Events.Count; i++)
                {
                    var e = W.Events[i]; if (!ShotBy(e, v)) continue;
                    bool after = j.stopSim >= 0 || (split >= 0 && i > split);
                    if (after) j.simShotsAfter++;
                    else { j.simShotsBefore++; if (e.type == ZEventType.MechHandGun) j.simHgBefore++; if (e.type == ZEventType.DriverShot) j.simDsBefore++; }
                }
                if (split >= 0) j.splitSeen = true;
            }
            if (!j.turretSet && j.turret) { j.turret0 = j.turret.localRotation; }
            if (empty && j.stopSim < 0)
            {   // the rules show it empty: this coroutine runs after Update, before LateUpdate: the turret and the drawn hull are still as the last crewed frame drew them
                j.stopSim = W.Time; j.stopAt = Time.unscaledTime; j.stopFrame = Time.frameCount; j.emptiedT = j.stopAt;
                if (j.turret) { j.turret0 = j.turret.localRotation; j.turretSet = true; j.turretYaw0 = YawOf(j.turret); if (g.Views.TryGetValue(v.Id, out var tv0) && tv0) j.turretOnHull0 = YawOnHull(j.turret, tv0.transform); }
                if (g.Views.TryGetValue(v.Id, out var uv0) && uv0) { j.viewPos = uv0.transform.position; j.viewInit = true; j.hullYaw0 = j.lastHullYaw = YawOf(uv0.transform); }
                j.simX = v.PosX; j.simY = v.PosY;
                if (j.snipe) j.snipeCentreMoved = Mathf.Max(Mathf.Abs(v.CenterX - j.snipeX), Mathf.Abs(v.CenterY - j.snipeY));   // its centre (px) after the snipe's frame against the hit's own point
                j.heardAt0 = OzVehicleQuiet.HeardOf(v.Id); j.jetsAt0 = BoardingView.DriverJets; j.mgRoundsAt0 = WeaponsV2Runner.I ? WeaponsV2Runner.I.MgRounds : -1;
            }
            if (j.robot == null)
            {
                j.robot = OutOf(v);
                if (j.robot != null) { j.heldX = j.robot.PosX; j.heldY = j.robot.PosY; if (j.robot.ExitOutAt >= 0) j.clickSimRules = j.robot.ExitOutAt - ZObject.DriverOutHoldTicks(v.Type) * ZConst.TICK; }
            }
            int ef = OzVehicleQuiet.EmptiedFrameOf(v.Id);
            if (ef >= 0 && j.emptiedFrame < 0) j.emptiedFrame = ef;
            if (j.stopSim >= 0 && !v.Destroyed && v.KillAt < 0)
            {
                j.simMoved = System.Math.Max(j.simMoved, System.Math.Sqrt((v.PosX - j.simX) * (v.PosX - j.simX) + (v.PosY - j.simY) * (v.PosY - j.simY)));
                if (g.Views.TryGetValue(v.Id, out var uv) && uv && !uv.Dying)
                {
                    var p = uv.transform.position; float yaw = YawOf(uv.transform);
                    if (!j.viewInit) { j.viewInit = true; j.viewPos = p; }
                    if (float.IsNaN(j.lastHullYaw)) j.lastHullYaw = yaw;
                    float step = (p - j.viewPos).magnitude, turn = Mathf.Abs(Mathf.DeltaAngle(yaw, j.lastHullYaw));
                    if (step > 0.002f || turn > 0.05f) j.lastMoveT = Time.unscaledTime;                                 // a move or a turn of the drawn hull: its brake still runs
                    StillStep(j, uv.transform, p, yaw, step, turn);                                                     // review round 2: the stop from the frame after the emptied frame
                    bool braked = Time.unscaledTime - j.emptiedT > BrakeSec / Mathf.Max(0.05f, g.gameSpeed);            // the brake in game time
                    if (braked)
                    {
                        j.movedAfterBrake += step;
                        if (float.IsNaN(j.hullYawBrake)) j.hullYawBrake = yaw;
                        j.hullTurnAfter = Mathf.Max(j.hullTurnAfter, Mathf.Abs(Mathf.DeltaAngle(yaw, j.hullYawBrake)));
                        if (j.turret) { float ty = YawOf(j.turret); if (float.IsNaN(j.turretYawBrake)) j.turretYawBrake = ty; j.turretWorldTurnAfter = Mathf.Max(j.turretWorldTurnAfter, Mathf.Abs(Mathf.DeltaAngle(ty, j.turretYawBrake))); }
                    }
                    j.viewPos = p; j.lastHullYaw = yaw;
                }
                if (j.turret && g.Views.TryGetValue(v.Id, out var tv) && tv && !float.IsNaN(j.turretOnHull0)) j.turretTurn = Mathf.Max(j.turretTurn, Mathf.Abs(Mathf.DeltaAngle(YawOnHull(j.turret, tv.transform), j.turretOnHull0)));   // vehicle_exit_jump2: its yaw on the hull
            }
            {   // vehicle_exit_jump3: the lid's kick, on the game clock (Time.time x the game's speed)
                float h = BoardingView.HatchOpenOf(v.Id), gt = Time.time * Mathf.Max(0.05f, g.gameSpeed);
                if (j.lidAtClick >= 0f && j.lidAtClick < 0.05f && j.lidStartGT < 0f && h > 0.02f) j.lidStartGT = gt;
                if (j.lidStartGT >= 0f && j.lidOpenGT < 0f && h >= 0.98f) j.lidOpenGT = gt;
            }
            var r = j.robot; if (r == null) return;
            if (r.ExitWalking(W.Time)) { j.heldSeen = true; j.heldMoved = System.Math.Max(j.heldMoved, System.Math.Sqrt((r.PosX - j.heldX) * (r.PosX - j.heldX) + (r.PosY - j.heldY) * (r.PosY - j.heldY))); }
        }
        /// <summary>Review round 2 (vehicle_exit_jump2: the stop rows let a 0.3 s glide of the drawn hull pass and never read its nod): one frame of
        /// the drawn stop after the rules show it empty (Measure, after Update): a frame in which the hull or its turret still moves or turns (a
        /// step over StopStepM, a turn or nod over StopStepDeg) is the brake's last so far; from the frame after the emptied frame (the view reads
        /// it in that frame or the next) the hull's glide from there, its nod (its up against then) and turn, and its turret's turn and nod in the
        /// world; frozen at the first hit that rocks it (OzVehicleRock.Kicks: the row then reads until that frame and names it).</summary>
        void StillStep(Jump j, Transform hull, Vector3 p, float yaw, float step, float turn)
        {
            int ef0 = j.emptiedFrame >= 0 ? j.emptiedFrame : j.stopFrame, f = Time.frameCount;
            if (j.kicks0 < 0) j.kicks0 = OzVehicleRock.Kicks;
            if (j.kickFrame < 0 && OzVehicleRock.Kicks != j.kicks0) j.kickFrame = f;
            if (j.kickFrame >= 0) return;
            var hu = hull.up; var tu = j.turret ? j.turret.up : Vector3.up; float ty = j.turret ? YawOf(j.turret) : 0f;
            if (j.lastUpSet)
            {
                float nodStep = Vector3.Angle(hu, j.lastHullUp), tStep = j.turret ? Mathf.Max(Vector3.Angle(tu, j.lastTurretUp), Mathf.Abs(Mathf.DeltaAngle(ty, j.lastTurretYaw))) : 0f;
                if (step > StopStepM || turn > StopStepDeg || nodStep > StopStepDeg || tStep > StopStepDeg) { j.lastMoveFrame = f; j.moveFrames++; }
            }
            j.lastHullUp = hu; j.lastTurretUp = tu; j.lastTurretYaw = ty; j.lastUpSet = true;
            if (f <= ef0) return;
            if (!j.baseSet) { j.baseSet = true; j.baseFrame = f; j.basePos = p; j.baseUp = hu; j.baseYaw = yaw; j.baseTurret = j.turret; j.baseTurretUp = tu; j.baseTurretYaw = ty; return; }
            j.stillFrames++;
            j.glide = Mathf.Max(j.glide, (p - j.basePos).magnitude); j.nod = Mathf.Max(j.nod, Vector3.Angle(hu, j.baseUp)); j.hullYawAfter = Mathf.Max(j.hullYawAfter, Mathf.Abs(Mathf.DeltaAngle(yaw, j.baseYaw)));
            if (j.baseTurret && j.turret) { j.turretNod = Mathf.Max(j.turretNod, Vector3.Angle(tu, j.baseTurretUp)); j.turretYawAfter = Mathf.Max(j.turretYawAfter, Mathf.Abs(Mathf.DeltaAngle(ty, j.baseTurretYaw))); }
        }
        readonly List<Vector3> bones = new List<Vector3>();
        readonly List<string> boneNames = new List<string>();
        readonly List<bool> inHole = new List<bool>();

        /// <summary>A bone of a figure: its Animator's human bone, else by its Mixamo name.</summary>
        static Transform BoneOf(GameObject go, HumanBodyBones hb, string name)
        {
            if (!go) return null;
            var a = go.GetComponentInChildren<Animator>();
            if (a && a.isHuman && a.avatar) { var t = a.GetBoneTransform(hb); if (t) return t; }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { string n = t.name; int c = n.LastIndexOf(':'); if (c >= 0) n = n.Substring(c + 1); if (string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase)) return t; }
            return null;
        }
        /// <summary>Fix round 3 (review: the size was the root's localScale, which BoardingView writes itself): the drawn length from the hips to the
        /// head along the bones (each bone's distance from its parent, summed: the same in every pose, changed only by a size); NaN: no chain.</summary>
        static float ChainLen(GameObject go)
        {
            var head = BoneOf(go, HumanBodyBones.Head, "Head"); var hips = BoneOf(go, HumanBodyBones.Hips, "Hips");
            if (!head || !hips) return float.NaN;
            float len = 0f; var t = head;
            for (int i = 0; i < 32 && t && t != hips; i++) { if (!t.parent) return float.NaN; len += (t.position - t.parent.position).magnitude; t = t.parent; }
            return t == hips && len > 1e-3f ? len : float.NaN;
        }
        /// <summary>His lower ankle's height above the ground under it (m; NaN: no foot bones).</summary>
        float LowAnkleAbove(GameObject go)
        {
            var l = BoneOf(go, HumanBodyBones.LeftFoot, "LeftFoot"); var r = BoneOf(go, HumanBodyBones.RightFoot, "RightFoot");
            if (!l || !r) return float.NaN;
            var f = l.position.y < r.position.y ? l : r;
            return f.position.y - (g.GroundY(f.position.x, f.position.z) - 0.08f);                                // BoardingView's RobotY
        }

        /// <summary>Fix round 2 (review: no joint-limit row): the joint angles of the drawn figure now against the limits (his pelvis's frame: up =
        /// hips -> spine, side = left -> right thigh, front = side x up): each hip's flexion and abduction, each knee's bend (0 .. KneeMax, no
        /// hyperextension: its hinge must turn the shank back, its hinge axis within KneeSideMax of the side axis), the spine's bend (hips ->
        /// spine against spine -> neck) and the neck's (spine -> neck against neck -> head). Returns the violations, the worst named.</summary>
        int Joints(GameObject go, BoardingView.HatchJumpRec rec, out string worstOut, out float byOut)
        {
            worstOut = ""; byOut = 0f; string worst = ""; float by = 0f; int n = 0;
            Transform hips = BoneOf(go, HumanBodyBones.Hips, "Hips"), spine = BoneOf(go, HumanBodyBones.Spine, "Spine"), neck = BoneOf(go, HumanBodyBones.Neck, "Neck"), head = BoneOf(go, HumanBodyBones.Head, "Head");
            Transform[] up = { BoneOf(go, HumanBodyBones.LeftUpperLeg, "LeftUpLeg"), BoneOf(go, HumanBodyBones.RightUpperLeg, "RightUpLeg") };
            Transform[] lo = { BoneOf(go, HumanBodyBones.LeftLowerLeg, "LeftLeg"), BoneOf(go, HumanBodyBones.RightLowerLeg, "RightLeg") };
            Transform[] ft = { BoneOf(go, HumanBodyBones.LeftFoot, "LeftFoot"), BoneOf(go, HumanBodyBones.RightFoot, "RightFoot") };
            if (!hips || !spine || !up[0] || !up[1] || !lo[0] || !lo[1] || !ft[0] || !ft[1]) { worstOut = "the leg / pelvis bones not found"; return -1; }
            var U = (spine.position - hips.position).normalized; var S = Vector3.ProjectOnPlane(up[1].position - up[0].position, U).normalized; var F = Vector3.Cross(S, U).normalized;
            void Bad(string what, float over) { n++; if (over > by) { by = over; worst = what; } }
            for (int k = 0; k < 2; k++)
            {
                string side = k == 0 ? "left" : "right";
                var th = lo[k].position - up[k].position; var sh = ft[k].position - lo[k].position; if (th.sqrMagnitude < 1e-6f || sh.sqrMagnitude < 1e-6f) continue;
                float flex = Mathf.Atan2(Vector3.Dot(th, F), Vector3.Dot(th, -U)) * Mathf.Rad2Deg;
                float abd = Mathf.Asin(Mathf.Clamp(Mathf.Abs(Vector3.Dot(th.normalized, S)), 0f, 1f)) * Mathf.Rad2Deg;
                if (flex > HipFlexMax) Bad($"{side} hip flexed {flex:0} deg (at most {HipFlexMax:0})", flex - HipFlexMax);
                if (flex < HipFlexMin) Bad($"{side} hip extended {flex:0} deg (at least {HipFlexMin:0})", HipFlexMin - flex);
                if (abd > HipAbdMax) Bad($"{side} hip spread {abd:0} deg (at most {HipAbdMax:0})", abd - HipAbdMax);
                float knee = Vector3.Angle(th, sh);
                if (knee > KneeMax) Bad($"{side} knee bent {knee:0} deg (at most {KneeMax:0})", knee - KneeMax);
                if (rec != null && rec.kneeRefOk && rec.kneeSide[k].sqrMagnitude > 0.5f)
                {   // vehicle_exit_jump3: against HIS rig's own idle pose: the shank's turn in the thigh bone's own frame from idle's (the BS-01 rig's left
                    // knee is drawn 16.5 deg bent at rest, its kink 44.5 deg off the side axis: a nearly straight knee's geometric hinge is no hinge)
                    var dNow = Quaternion.Inverse(up[k].rotation) * sh.normalized; var d0 = rec.kneeShank[k]; var sideL = rec.kneeSide[k].normalized;
                    float bend = Vector3.Angle(d0, dNow); var ax = Vector3.Cross(d0, dNow);
                    if (bend > KneeWrongMinDeg && ax.sqrMagnitude > 1e-8f)
                    {
                        float along = Vector3.Dot(ax.normalized, sideL);                                           // a knee bending the right way turns the shank back: its turn along the thigh's side axis
                        if (along < 0f) Bad($"{side} knee bent the wrong way ({bend:0} deg past his idle's)", bend);
                        else if (bend > KneeHingeMinDeg) { float off = Mathf.Acos(Mathf.Clamp(along, -1f, 1f)) * Mathf.Rad2Deg; if (off > KneeSideMax) Bad($"{side} knee's hinge {off:0} deg off his thigh's side axis (at most {KneeSideMax:0}; bent {bend:0} deg past his idle's)", off - KneeSideMax); }
                    }
                }
                else
                {   // no reference read on his rig: the geometric hinge, judged past KneeHingeMinDeg only
                    var h = Vector3.Cross(th.normalized, sh.normalized);
                    if (knee > KneeHingeMinDeg && h.sqrMagnitude > 1e-6f)
                    {
                        float along = Vector3.Dot(h.normalized, S);
                        if (along < 0f) Bad($"{side} knee bent the wrong way ({knee:0} deg)", knee);
                        else { float off = Mathf.Acos(Mathf.Clamp(along, -1f, 1f)) * Mathf.Rad2Deg; if (off > KneeSideMax) Bad($"{side} knee's hinge {off:0} deg off his side axis (at most {KneeSideMax:0})", off - KneeSideMax); }
                    }
                }
            }
            if (neck)
            {
                float sp = Vector3.Angle(U, neck.position - spine.position); if (sp > SpineMax) Bad($"spine bent {sp:0} deg (at most {SpineMax:0})", sp - SpineMax);
                if (head) { float nk = Vector3.Angle(neck.position - spine.position, head.position - neck.position); if (nk > NeckMax) Bad($"neck bent {nk:0} deg (at most {NeckMax:0})", nk - NeckMax); }
            }
            worstOut = worst; byOut = by;
            return n;
        }

        /// <summary>Fix round 2: the figure at the end of a frame (what is drawn): size, phases, marks, its body points against the solids of the
        /// vehicle and the buildings and flags near (BoardingView.HatchVehicleSolids: every drawn part, its mesh as columns, nothing cut, the lid as
        /// it swings; a point closer than a limb's half thickness cuts; the hatch's opening left out), the joints, the somersault from the push to
        /// the touch, the hips' vertical speed per phase, the touch (its frame, time, the lower ankle and the tilt) and the planted ankle's height
        /// in the walk on.</summary>
        void Figure(Jump j)
        {
            var v = j.v; var r = j.robot; if (v == null || r == null) return;
            int st = BoardingView.HatchFigureNow(r.Id, out var mark, out var drawn, out float size, out string phase);
            int wst = BoardingView.ExitWalkFigure(r.Id, out var pos, out var rot);
            if (wst == 0) { if (j.sawFigure && j.endT < 0f) j.endT = Time.unscaledTime; return; }
            if (st != 2 || wst != 2) return;
            j.sawFigure = true; j.frames++; j.lastFigure = pos;
            j.sizeMin = Mathf.Min(j.sizeMin, size); j.sizeMax = Mathf.Max(j.sizeMax, size);
            int rank = Rank(phase);
            if (rank >= 0) { if (rank < j.phaseRank) j.phaseBack = true; j.phaseRank = Mathf.Max(j.phaseRank, rank); }
            var go = BoardingView.HatchFigureGo(r.Id);
            // fix round 3 (review: the size held by construction): the drawn hips -> head length against his own model's, every frame
            if (float.IsNaN(j.liveLen) && g.Views.TryGetValue(r.Id, out var lv) && lv) j.liveLen = ChainLen(lv.gameObject);
            float len = go ? ChainLen(go) : float.NaN;
            if (!float.IsNaN(len) && !float.IsNaN(j.liveLen)) { float k = len / j.liveLen; j.lenMin = Mathf.Min(j.lenMin, k); j.lenMax = Mathf.Max(j.lenMax, k); j.lenFrames++; }
            if (phase == "walk")
            {
                float a = LowAnkleAbove(go); if (!float.IsNaN(a)) { j.walkAnkleMin = Mathf.Min(j.walkAnkleMin, a); j.walkFrames++; }
                if (j.lastPhase == "land" && j.standUpSec < 0f && j.landLowSim >= 0) j.standUpSec = (float)(W.Time - j.landLowSim);   // fix round 3: from the hips' lowest to standing
                j.lastPhase = phase; return;
            }
            if (phase != "crouch" && phase != "push" && phase != "air" && phase != "land") { j.lastPhase = phase; return; }   // vehicle_exit_jump2: + push
            j.jumpFrames++;
            {   // vehicle_exit_jump3: the show (crouched from its first frame), the push's first frame, the lid then
                float gt = Time.time * Mathf.Max(0.05f, g.gameSpeed);
                if (j.showGT < 0f) { j.showGT = gt; j.firstPhase = phase; j.lidAtShow = BoardingView.HatchOpenOf(v.Id); }
                if (phase == "push" && j.pushGT < 0f) j.pushGT = gt;
            }
            if (phase == "air" && j.firstAirT < 0f) { j.firstAirT = Time.unscaledTime; j.firstAirDt = Mathf.Max(1f / 240f, Time.unscaledDeltaTime); }   // fix round 3: the take-off's sound against this frame
            if ((phase == "crouch" || phase == "push") && BoardingView.HatchRimFrameOf(v.Id, out var rimW, out var upW))
            {   // fix round 3 (review: nothing measured his feet on the rim, nor a rise out of the hatch): his feet on the DRAWN rim, the root still;
                // review round 2 (vehicle_exit_jump2: no row measured his feet in the PUSH, where they slid 0.16-0.39 m along the rim): in every
                // crouch and push frame his drawn sole point (his ankles' middle brought down along his body's up to his lower ankle, less his rig's
                // ankle over the sole: the point he stands on, however he leans) against the drawn take-off point
                var la = BoneOf(go, HumanBodyBones.LeftFoot, "LeftFoot"); var ra = BoneOf(go, HumanBodyBones.RightFoot, "RightFoot");
                BoardingView.HatchJumps.TryGetValue(r.Id, out var rc0);
                if (la && ra && rc0 != null)
                {
                    bool inPush = phase == "push"; if (inPush) j.pushFrames++; else j.crouchFrames++;
                    var bu = rot * Vector3.up; var ankMid = 0.5f * (la.position + ra.position);
                    float lowU = Mathf.Min(Vector3.Dot(la.position, bu), Vector3.Dot(ra.position, bu));
                    var soleP = ankMid - bu * (Vector3.Dot(ankMid, bu) - lowU) - bu * rc0.ankle0;
                    if (!inPush)
                    {
                        float low = Mathf.Min(Vector3.Dot(la.position - rimW, upW), Vector3.Dot(ra.position - rimW, upW));
                        j.crouchAnkleDev = Mathf.Max(j.crouchAnkleDev, Mathf.Abs(low - rc0.ankle0));
                        float ru = Vector3.Dot(pos - rimW, upW); j.rootUpMin = Mathf.Min(j.rootUpMin, ru); j.rootUpMax = Mathf.Max(j.rootUpMax, ru);
                    }
                    if (BoardingView.HatchTakeoffNow(r.Id, out var want))
                    {
                        float soleOff = (soleP - want).magnitude;
                        if (inPush) { j.pushFeetFrames++; j.pushFeetOff = Mathf.Max(j.pushFeetOff, soleOff); } else { j.crouchFeetFrames++; j.crouchFeetOff = Mathf.Max(j.crouchFeetOff, soleOff); }
                    }
                }
            }
            if (BoardingView.ExitFigureState(r, out _) == 2) j.markFrames++; else j.markMissing++;
            // the cuts: every body point against every solid now
            if (!j.solidsRead)
            {
                j.solidsRead = BoardingView.HatchVehicleSolids(v.Id, j.solids);
                foreach (var s in j.solids) { if (s.world) j.nearSolids++; if (s.boxModel) { j.boxSolids++; if (j.boxNames.Length < 200) j.boxNames += (j.boxNames.Length > 0 ? ", " : "") + s.name; } else j.colSolids++; }
            }
            foreach (var s in j.solids) if (!s.world && s.t) s.Use(s.Basis());
            BoardingView.HatchFigureBones(r.Id, bones, boneNames);                                              // vehicle_exit_jump3: each point's bone (the one margin rule)
            if (bones.Count == 0 || j.solids.Count == 0) j.noBoneFrames++;                                         // a frame with no bone or no solid read is no clear frame
            inHole.Clear(); foreach (var p in bones) inHole.Add(BoardingView.HatchInHoleOf(v.Id, p));
            float worst = 0f; string what = "";
            foreach (var s in j.solids)
            {
                float reach = s.rad + 3f;
                for (int i = 0; i < bones.Count; i++)
                {
                    var p = bones[i]; if (inHole[i] || (p - s.centre).sqrMagnitude > reach * reach) continue;
                    float limb = BoardingView.HatchMarginFor(i < boneNames.Count ? boneNames[i] : "");                // vehicle_exit_jump3: the planner's own rule (a hand: its fist more)
                    float cut = limb - s.Gap(p, limb + 0.05f);                                                       // > 0: the limb's skin reaches into it
                    if (cut > worst) { worst = cut; what = (s.world ? "a building / flag's " : s.lid ? "the lid's " : "the vehicle's ") + s.name + (s.boxModel ? " (a box: its mesh could not be read)" : ""); }
                }
            }
            if (worst > 0f)
            {
                j.cutFrames++; j.cutBy.TryGetValue(what, out int nc); j.cutBy[what] = nc + 1; if (what.StartsWith("a building")) j.nearCutFrames++;
                if (worst > j.worstCut) { j.worstCut = worst; j.worstWhat = $"{what} in the {phase} ({Time.unscaledTime - j.clickT:0.00} s after the click)"; }
            }
            // the joints
            BoardingView.HatchJumps.TryGetValue(r.Id, out var recJ);
            string jw = ""; float jb = 0f; int jv = go ? Joints(go, recJ, out jw, out jb) : -1;
            if (jv >= 0) { j.jointFrames++; j.jointViol += jv; if (jv > 0 && jb > j.worstJointBy) { j.worstJointBy = jb; j.worstJoint = $"{jw} in the {phase} ({Time.unscaledTime - j.clickT:0.00} s)"; } }
            // the somersault (the push's lean and the flight; the landing's lean is not the turn), from the drawn root's up
            var up = rot * Vector3.up;
            if (BoardingView.HatchJumps.TryGetValue(r.Id, out var rec) && j.axis == Vector3.zero)
            {
                var d = rec.landing - rec.takeoff; d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) j.axis = Vector3.Cross(Vector3.up, d.normalized).normalized;
            }
            if (j.axis != Vector3.zero && phase != "land")
            {
                var a = Vector3.ProjectOnPlane(up, j.axis);
                if (j.upInit && a.sqrMagnitude > 1e-6f && j.lastUp.sqrMagnitude > 1e-6f) j.flip += Mathf.Abs(Vector3.SignedAngle(j.lastUp, a, j.axis));
                if (a.sqrMagnitude > 1e-6f) { j.lastUp = a; j.upInit = true; }
            }
            // the hips' vertical speed (game time): the last push frame and the first flight frame (vehicle_exit_jump2: the push is its own phase)
            var hips = BoneOf(go, HumanBodyBones.Hips, "Hips");
            if (hips && phase == "air") j.airHipsMax = Mathf.Max(j.airHipsMax, hips.position.y);
            if (hips && (phase == "push" || phase == "air") && BoardingView.HatchPlanNow(r.Id, out float tauP, out var planH, out var vPE, out var vAS))
            {   // vehicle_exit_jump3 (E2b: the old speed row was uneven at the editor's ~30 fps: a 0.1 s push accelerates within one frame): the drawn
                // hips against the plan at the exit's own time, and the drawn speed over each step against the plan's over the SAME step (any fps)
                var hd = hips.position;
                if (j.prevTau >= 0f && tauP > j.prevTau + 1e-5f)
                {
                    float dtp = tauP - j.prevTau; var vD = (hd - j.prevDrawnH) / dtp; var vP = (planH - j.prevPlanH) / dtp;
                    if (phase == "push" && j.prevPlanPhase == "push") { j.vPushD = vD; j.vPushP = vP; j.vPushSet = true; }
                    else if (phase == "air" && j.prevPlanPhase == "push" && !j.vAirSet) { j.vAirD = vD; j.vAirP = vP; j.vAirSet = true; }
                }
                float err = (hd - planH).magnitude;
                if (phase == "push") j.errLastPush = err; else if (j.errFirstAir < 0f) j.errFirstAir = err;
                j.kink = (vAS - vPE).magnitude;
                if (tauP > j.prevTau + 1e-5f) { j.prevTau = tauP; j.prevDrawnH = hd; j.prevPlanH = planH; j.prevPlanPhase = phase; }
            }
            // E2b check fix: this runs at WaitForEndOfFrame, which the editor's Game view does not resume once in every frame (a frame skipped, then
            // two in one): the game time since the last READ, not this frame's dt (a per-frame log showed the drawn take-off smooth while this row read 0.39 / 7.58 m/s)
            float gdt = g.paused ? 0f : (j.lastHipsGT >= 0f ? Time.time - j.lastHipsGT : Time.deltaTime) * Mathf.Max(0.05f, g.gameSpeed);
            if (g.paused) j.lastHipsGT = Time.time;
            if (hips)
            {
                if (j.lastHipsT >= 0f && gdt > 1e-4f)
                {
                    float vy = (hips.position.y - j.lastHips.y) / gdt;
                    var dvh = hips.position - j.lastHips; dvh.y = 0f; float vhNow = dvh.magnitude / gdt;                    // review round 2: the speed out too
                    if (phase == "push" && j.lastPhase == "push") { j.vyLastCrouch = vy; j.vhLastPush = vhNow; }
                    else if (phase == "air" && j.lastPhase == "push") { j.vyFirstAir = vy; j.vhFirstAir = vhNow; }
                    else if (phase == "land" && j.lastPhase == "land") j.landRiseMax = Mathf.Max(j.landRiseMax, vy);   // fix round 3 (review: the stand-up rebounded at 7-8 m/s)
                }
                if (gdt > 1e-4f || j.lastHipsT < 0f) { j.lastHips = hips.position; j.lastHipsT = Time.unscaledTime; j.lastHipsGT = Time.time; }
                if (phase == "land") { j.landFrames++; if (hips.position.y < j.landLowY) { j.landLowY = hips.position.y; j.landLowSim = W.Time; } }
            }
            if (phase == "land" && j.touchFrame < 0)
            {   // the check's own touch: the first frame drawn landing (BoardingView lands him in its LateUpdate: this is that frame's end)
                j.touchFrame = Time.frameCount; j.touchSim = W.Time; j.touchT = Time.unscaledTime; j.touchDt = Mathf.Max(1f / 240f, Time.unscaledDeltaTime);
                j.touchAnkle = LowAnkleAbove(go); j.touchTilt = Vector3.Angle(up, Vector3.up);
                var la2 = BoneOf(go, HumanBodyBones.LeftFoot, "LeftFoot"); var ra2 = BoneOf(go, HumanBodyBones.RightFoot, "RightFoot");
                if (la2 && ra2) { j.touchAnkMid = 0.5f * (la2.position + ra2.position); j.touchAnkSet = true; }   // vehicle_exit_jump3: where he landed
            }
            j.lastPhase = phase;
        }

        /// <summary>Clicks the tab for the selected vehicle (or, act != null, runs it instead: the EXIT made otherwise) and watches the exit until the
        /// walk on ends (at most `max` s of real time); a 12-frame sheet (4 x 3) when sheetName is given: fix round 2 (review: a fixed grid missed
        /// the spring), its cells at the jump's own moments (the record's: shown, mid-spring, take-off, 15 / 35 / 55 / 75 / 90 % of the flight,
        /// touch, mid-bend, stand, the walk on) at the normal RTS camera; no jump record: every 0.183 s from the stop. measuring = the caller
        /// already runs MeasureLoop for j (S2: from before the snipe).</summary>
        IEnumerator WatchExit(ZObject v, Jump j, string sheetName, float max = 8f, IEnumerator act = null, System.Action<Jump> during = null, bool measuring = false)
        {
            if (!measuring)
            {
                BoardingView.HatchExitStatsReset(); BoardingView.ExitWalkStatsReset();
                j.v = v; j.turret = TurretOf(v); j.clickT = Time.unscaledTime; j.lidAtClick = BoardingView.HatchOpenOf(v.Id);
                StartCoroutine(MeasureLoop(j));                                                                       // from before the click (fix round 1)
            }
            if (act != null) yield return act; else yield return ClickTab(v, "watch");
            var sheet = string.IsNullOrEmpty(Folder) || sheetName == null ? null : new Texture2D(4 * 320, 3 * 240, TextureFormat.RGB24, false);
            int cell = 0; double[] at = null; double gridAt = -1;
            try
            {
                while (Time.unscaledTime - j.clickT < max)
                {
                    during?.Invoke(j);
                    if (sheet && cell < 12 && j.stopSim >= 0)
                    {
                        if (at == null && j.robot != null && BoardingView.HatchJumps.TryGetValue(j.robot.Id, out var rec) && rec.flight > 0f)
                        {
                            double c0 = rec.clickSim, to = rec.takeoffAt, fl = rec.flight, tc = to + fl, ls = Mathf.Max(0.1f, rec.landSec);
                            at = new[] { c0 + rec.appearAt + 0.01, c0 + to - 0.5 * rec.pushSec, c0 + to + 0.01, c0 + to + 0.15 * fl, c0 + to + 0.35 * fl, c0 + to + 0.55 * fl,
                                         c0 + to + 0.75 * fl, c0 + to + 0.90 * fl, c0 + tc + 0.01, c0 + tc + 0.5 * ls, c0 + tc + ls + 0.05, c0 + tc + ls + 0.6 };
                        }
                        if (at == null && gridAt < 0 && Time.unscaledTime - j.emptiedT > 0.5f) gridAt = W.Time;                // no jump record (no figure): a plain grid
                        double due = at != null ? at[cell] : gridAt >= 0 ? gridAt + cell * (2.2 / 12.0) : double.MaxValue;
                        if (W.Time >= due)
                        {
                            var c = Screen(v); var rc = j.robot != null ? Screen(j.robot) : c; var mid = 0.5f * (c + rc);
                            yield return SheetCell(sheet, cell++, 4, 3, new Rect(mid.x - 160, mid.y - 120, 320, 240));
                            continue;
                        }
                    }
                    if (j.endT > 0f && Time.unscaledTime - j.endT > 0.8f && (sheet == null || cell >= 12)) break;
                    yield return null;
                }
            }
            finally { j.done = true; }
            SaveSheet(sheet, sheetName);
        }

        /// <summary>The verdict of the vehicle's stop (every row that has one): needs its samples (the view saw it empty, the rules' stop tick, its
        /// turret found); no shot after the stop by the rules (split at the vehicle's own Ejected event or the sniper's own hit), none drawn
        /// (OzVehicleQuiet.DrawnAfter), none sent to the view after its emptied frame (MutedOf), no weapon sound heard for it after its emptied
        /// frame (OzVehicleQuiet.HeardAfter: CombatAudio's own weapon voices and, fix round 2, the tanks' gun launches tankshot and heavyshot);
        /// the mech's hand gun (gun: 1 = S1, rounds before > 0, none after, a running burst cut; 2 = S2, rounds before > 0, none after; 0 = none
        /// after, -1 never seen quiet FAILS); its move after the brake, its turret's turn on the hull, and, fix round 2 (review), its drawn hull's
        /// and its turret's WORLD turn after the brake (StopTurnDeg) and the brake itself (moves and turns end within BrakeSec); review round 2
        /// (vehicle_exit_jump2: that passed a 0.3 s glide and never read a nod): the drawn hull and turret still moving in at most BrakeFrames
        /// frames after the emptied frame, then (StopSampleFrames frames at least) a glide of at most StopGlideM, a nod and turns of at most
        /// StopStillDeg (StillStep) replace BrakeSec, StopMoveM and the world turns after BrakeSec; after a snipe its
        /// centre unchanged from the sniper's hit's own point (px); the text.</summary>
        bool StopOk(Jump j, int gun, out string text)
        {
            int id = j.v.Id;
            int drawnAfter = OzVehicleQuiet.DrawnAfter(id), muted = OzVehicleQuiet.MutedOf(id), heardAfter = OzVehicleQuiet.HeardAfter(id), heardBefore = OzVehicleQuiet.HeardBefore(id);
            int mech = MechHandGunView.RoundsAfterQuiet(id), mech0 = MechHandGunView.RoundsAtQuiet(id); bool cut = MechHandGunView.CutAtQuiet(id);
            int ef0 = j.emptiedFrame >= 0 ? j.emptiedFrame : j.stopFrame;
            int brakeFrames = j.lastMoveFrame < 0 || ef0 < 0 ? 0 : Mathf.Max(0, j.lastMoveFrame - ef0);                     // review round 2: frames it still moved after the emptied frame
            bool stillSeen = j.baseSet && j.stillFrames >= StopSampleFrames && (!j.turret || j.baseTurret);
            bool isMech = j.v.Type == ZMTank.Mech;
            bool mechOk = !isMech || (gun == 1 ? mech == 0 && mech0 > 0 && cut : gun == 2 ? mech == 0 && mech0 > 0 && j.simHgBefore > 0 : mech == 0);
            bool snipeOk = !j.snipe || j.snipeCentreMoved == 0;
            bool ok = j.emptiedFrame >= 0 && j.stopSim >= 0 && j.turret && j.turretSet && !float.IsNaN(j.turretOnHull0) && j.simShotsAfter == 0 && drawnAfter == 0 && muted == 0 && heardAfter == 0 && mechOk
                      && j.simMoved <= 0.01 && brakeFrames <= BrakeFrames && stillSeen && j.glide <= StopGlideM && j.nod <= StopStillDeg && j.hullYawAfter <= StopStillDeg
                      && j.turretYawAfter <= StopStillDeg && j.turretNod <= StopStillDeg && j.turretTurn <= StopTurnDeg && snipeOk && j.v.AttackObject == null && j.v.Waypoints.Count == 0;
            text = $"the vehicle seen empty in frame {j.emptiedFrame} (the rules: frame {j.stopFrame}, {(j.emptiedT > 0f ? (j.emptiedT - j.clickT).ToString("0.00") : "-")} s after the click; the frame's events split at "
                   + $"{(j.snipe ? "the sniper's own hit" : "its own Ejected event")} {j.splitSeen}); its shots before {j.simShotsBefore} (hand gun {j.simHgBefore}, hatch driver {j.simDsBefore}), after: the rules' {j.simShotsAfter}, "
                   + $"sent to the view after its emptied frame {muted}, drawn {drawnAfter}; its weapon sounds heard {heardBefore} before, {heardAfter} after (the weapons' voices and tankshot / heavyshot)"
                   + $"{(isMech ? $"; the hand gun's rounds before {mech0}, after {mech} (a burst cut {cut})" : "")}; moved after the stop: the rules {j.simMoved:0.000} px"
                   + $"{(j.snipe ? $" (its centre {j.snipeCentreMoved} px off the sniper's hit's own point)" : "")}, drawn: the hull or its turret still moved in {j.moveFrames} frame(s), the last {brakeFrames} frame(s) after its emptied frame {ef0} (at most {BrakeFrames}: the view reads it in that frame or the next); "
                   + $"from frame {j.baseFrame} on ({j.stillFrames} frames read, at least {StopSampleFrames}{(j.kickFrame >= 0 ? $"; read until a hit rocked it in frame {j.kickFrame}" : "")}; {(stillSeen ? "measured" : "NOT MEASURED")}): its hull glided {j.glide:0.0000} m (at most {StopGlideM:0.000}), "
                   + $"nodded {j.nod:0.00} deg (its up) and turned {j.hullYawAfter:0.00} deg, its turret turned {j.turretYawAfter:0.00} deg and nodded {j.turretNod:0.00} deg in the world (each at most {StopStillDeg:0.0}); "
                   + $"its {(j.turret ? j.turret.name : "turret: NONE FOUND")} turned {j.turretTurn:0.00} deg (its yaw) on the hull from the last crewed frame (at most {StopTurnDeg:0.0}); "
                   + $"target {(j.v.AttackObject != null ? "#" + j.v.AttackObject.Id : "none")}, orders {j.v.Waypoints.Count}; held shots of the emptied frame drawn {OzVehicleQuiet.HeldDrawnOf(id)} (the gun on them), "
                   + $"dropped {OzVehicleQuiet.HeldDroppedOf(id)} (their impacts not drawn {OzVehicleQuiet.ImpactsDroppedOf(id)}); hatch driver shots not drawn {OzVehicleQuiet.DriverDroppedOf(id)}, MG clips stopped {OzVehicleQuiet.HatchClipsStopped}";
            return ok;
        }

        /// <summary>J1's verdict for one exit, fix round 2 (review: parts of it held by construction): from the check's own frames: the touch (the
        /// first frame drawn landing; its time against the rules' click, robot.ExitOutAt - the hold), the lower ankle there against its planted
        /// height in the walk on, the drawn body's up there, rob_land's time in the sound log against that frame's, the lid's open (when it was
        /// shut at the click) and shut sounds apart, ground steps after the touch, the take-off's vertical speed match; the plan's own facts from
        /// BoardingView's record (clear, not late, the tau probe). The machine tag "[touch &lt;type&gt; &lt;s&gt; hold &lt;s&gt;]" (the check's own
        /// touch) is read by the Editor's Stop Count (H5). J1j: the joints.</summary>
        void JumpVerdict(string what, Jump j, bool shown, string jointsWhat = null)
        {
            var v = j.v; var r = j.robot;
            BoardingView.HatchJumpRec rec = null; if (r != null) BoardingView.HatchJumps.TryGetValue(r.Id, out rec);
            double hold = ZObject.DriverOutHoldTicks(v.Type) * ZConst.TICK;
            double click = !double.IsNaN(j.clickSimRules) ? j.clickSimRules : rec != null ? rec.clickSim : double.NaN;
            double touch = j.touchFrame > 0 && !double.IsNaN(click) ? j.touchSim - click : -1, vsHold = touch >= 0 ? touch - hold : 99;
            var log = OzSoundLog.Since(j.clickT);
            int P(string key) => log.Count(e => e.key == key && Played(e));
            bool Asked(string key) => log.Any(e => e.key == key);
            // fix round 3 (review: the localScale was written by BoardingView itself): the drawn hips -> head length against his own model's, every frame
            bool size = j.frames > 0 && j.lenFrames >= j.frames && j.lenMax - j.lenMin <= SizeTol && Mathf.Abs(j.lenMin - 1f) <= SizeTol && Mathf.Abs(j.lenMax - 1f) <= SizeTol;
            float feet = !float.IsNaN(j.touchAnkle) && j.walkFrames > 0 ? j.touchAnkle - j.walkAnkleMin : float.NaN;
            float feetGround = !float.IsNaN(j.touchAnkle) && rec != null ? j.touchAnkle - rec.ankle0 : float.NaN;     // fix round 3 (review: only against the later walk): against the ground (his rig's ankle over the sole)
            bool touchOk = j.touchFrame > 0 && !float.IsNaN(feet) && feet >= -0.05f && feet <= FeetAtTouchM && !float.IsNaN(feetGround) && Mathf.Abs(feetGround) <= FeetAtTouchM
                           && j.touchTilt >= 0f && j.touchTilt <= TiltAtTouchDeg;
            // fix round 3 (review: no row had his feet on the rim or the root still before the take-off; the stand-up rebounded)
            bool crouchOk = j.crouchFrames + j.pushFrames > 0 && j.crouchFeetFrames == j.crouchFrames && j.crouchAnkleDev <= CrouchAnkleM && j.crouchFeetOff <= SoleOnRimM && j.rootUpMax - j.rootUpMin <= RootRiseTolM;
            bool pushFeetOk = j.pushFrames > 0 && j.pushFeetFrames == j.pushFrames && j.pushFeetOff <= SoleOnRimM;          // review round 2: his soles planted in every push frame
            bool standOk = j.landFrames > 1 && !float.IsNegativeInfinity(j.landRiseMax) && j.landRiseMax <= LandRiseMaxMps && j.standUpSec >= StandUpMinSec;
            bool timeOk = touch >= TouchMinSec - 1e-3 && touch <= TouchMaxSec + 1e-3 && System.Math.Abs(vsHold) <= TouchHoldSec;
            bool flipOk = j.flip >= FlipMinDeg && j.flip <= FlipMaxDeg;
            bool cutOk = j.jumpFrames > 0 && j.cutFrames == 0 && j.noBoneFrames == 0 && j.solids.Count > 0;
            bool marks = j.jumpFrames > 0 && j.markMissing == 0;
            float window = LandSoundFrames * j.touchDt + 1e-4f;
            var lands = log.Where(e => e.key == "rob_land" && Played(e)).ToList();
            bool land = j.touchFrame > 0 && lands.Any(e => Mathf.Abs(e.t - j.touchT) <= window);
            int opens = P("veh_lid_open"), closes = P("veh_lid_close"), hatches = P("hatch");
            bool lidWasShut = j.lidAtClick >= 0f && j.lidAtClick < 0.05f;
            bool lid = (!lidWasShut || opens >= 1) && closes >= 1;
            int steps = j.touchFrame > 0 ? log.Count(e => e.t > j.touchT && e.key != null && e.key.StartsWith("rob_step_") && e.key != "rob_step_metal" && Played(e)) : 0;
            // fix round 3 (review: the take-off sound came from BoardingView's own record): rob_step_metal or dst_ragdoll_whoosh PLAYED in the sound log within +-1 frame of the first flight frame
            float tWindow = LandSoundFrames * j.firstAirDt + 1e-4f;
            var takeoffs = log.Where(e => (e.key == "rob_step_metal" || e.key == "dst_ragdoll_whoosh") && Played(e)).ToList();
            bool takeoffSnd = j.firstAirT > 0f && takeoffs.Any(e => Mathf.Abs(e.t - j.firstAirT) <= tWindow);
            bool sounds = land && takeoffSnd && P("exit") >= 1 && Asked("vo_vehicle_exit") && lid && steps > 0;
            // vehicle_exit_jump3: the take-off at any frame rate (the drawn hips against the plan; the speeds over the same steps); the old rows
            // (the last push frame's speed against the first flight frame's, each over its own step) read unevenly at ~30 fps and are gone
            bool SpeedOk(Vector3 d, Vector3 p) => (d - p).magnitude <= Mathf.Max(PlanSpeedFloorMps, PlanSpeedFrac * p.magnitude);
            bool vyOk = j.errLastPush >= 0f && j.errLastPush <= PlanPosTolM && j.errFirstAir >= 0f && j.errFirstAir <= PlanPosTolM && j.kink >= 0f && rec != null && j.kink <= Mathf.Max(PlanKinkMps, 0.03f * rec.v0.magnitude);
            bool vhOk = j.vPushSet && j.vAirSet && SpeedOk(j.vPushD, j.vPushP) && SpeedOk(j.vAirD, j.vAirP);
            // vehicle_exit_jump3: the leap (from the drawn hips and feet), the lid kicked, the show crouched, no wait
            bool isMech = v.Type == ZMTank.Mech, secondJ = rec != null && rec.index >= 1; float apexMin = secondJ ? (isMech ? ApexMinSecondMechM : ApexMinSecondM) : isMech ? ApexMinMechM : ApexMinM;
            float apexDrawn = rec != null && j.airHipsMax > float.MinValue ? j.airHipsMax - rec.hEnd.y : float.NaN;
            float landDrawn = rec != null && j.touchAnkSet ? Vector3.ProjectOnPlane(j.touchAnkMid - rec.takeoff, Vector3.up).magnitude : float.NaN;
            bool bailedJ = rec != null && rec.bailed, cmdJ = rec != null && rec.commanderWasUp, slipJ = rec != null && rec.slipSec > SlipJudgeSec;
            bool leapOk = bailedJ || slipJ || (!float.IsNaN(apexDrawn) && apexDrawn >= apexMin && !float.IsNaN(landDrawn) && landDrawn >= LandMinM && landDrawn <= LandMaxM);
            bool lidKicked = j.lidAtClick >= 0.05f || cmdJ || (j.lidStartGT >= 0f && j.lidOpenGT >= 0f && j.lidOpenGT - j.lidStartGT <= LidKickMaxSec);
            bool showOk = (j.firstPhase == "crouch" || j.firstPhase == "push") && j.showGT >= 0f && j.pushGT >= 0f && j.pushGT - j.showGT <= PushAfterShowMaxSec + (slipJ ? 1f : 0f)
                          && (j.lidAtClick >= 0.05f || cmdJ || (j.lidStartGT >= 0f && j.showGT - j.lidStartGT <= ShowAfterLidMaxSec));
            bool rules = shown && v.Owner == Team.Neutral && v.Drivers.Count == 0 && r != null && j.heldSeen && j.heldMoved < 0.01 && rec != null && !rec.late && rec.clearFound && rec.posesRead && !j.phaseBack;   // vehicle_exit_jump2: his poses read on his rig
            bool stop = StopOk(j, 0, out string stopText);
            string cuts = j.cutBy.Count == 0 ? "none" : string.Join(", ", j.cutBy.Select(kv => $"{kv.Key} {kv.Value} frame(s)"));
            Log(what, rules && size && touchOk && timeOk && flipOk && cutOk && marks && sounds && vyOk && vhOk && stop && crouchOk && pushFeetOk && standOk && leapOk && lidKicked && showOk,
                $"[touch {v.Type} {(touch >= 0 ? touch.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) : "-1")} hold {hold.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}] "
                + $"tab shown {shown}; the robot {(r != null ? $"{r.Type} at ({r.CenterX - v.CenterX:+0;-0}, {r.CenterY - v.CenterY:+0;-0}) px, held {j.heldSeen} (moved {j.heldMoved:0.00} px)" : "MISSING")}; "
                + $"figure drawn {j.frames} frames ({j.jumpFrames} in the jump), its drawn hips-to-head length {j.lenMin:0.000} .. {j.lenMax:0.000} of his model's {j.liveLen:0.00} m in {j.lenFrames} frames (within {SizeTol:0.00}; the root's scale {j.sizeMin:0.000} .. {j.sizeMax:0.000}); "
                + $"the crouch on the drawn rim in {j.crouchFrames} frames: the lower ankle {j.crouchAnkleDev:0.000} m off the rim + his ankle {(rec != null ? rec.ankle0.ToString("0.00") : "-")} m (at most {CrouchAnkleM:0.00}), "
                + $"his sole point {j.crouchFeetOff:0.000} m off the drawn take-off point in {j.crouchFeetFrames} frames (at most {SoleOnRimM:0.00}), the root along the hull's up {(j.crouchFrames > 0 ? (j.rootUpMax - j.rootUpMin).ToString("0.000") : "-")} m (at most {RootRiseTolM:0.00}); "
                + $"the push on the drawn rim in {j.pushFrames} frames: his sole point at most {j.pushFeetOff:0.000} m off the drawn take-off point in {j.pushFeetFrames} frames (at most {SoleOnRimM:0.00}: his feet planted until the take-off); "
                + $"the stand-up: the hips at most {(float.IsNegativeInfinity(j.landRiseMax) ? "NOT MEASURED" : j.landRiseMax.ToString("0.00"))} m/s up in {j.landFrames} landing frames (at most {LandRiseMaxMps:0.0}), from their lowest to standing {j.standUpSec:0.00} s (at least {StandUpMinSec:0.00}); "
                + $"marked in {j.markFrames} of {j.jumpFrames} jump frames; "
                + $"solids {j.solids.Count} ({j.colSolids} as mesh columns, {j.boxSolids} as boxes{(j.boxSolids > 0 ? ": " + j.boxNames : "")}; {j.nearSolids} of buildings / flags near): cut in {j.cutFrames} frames "
                + $"({cuts}){(j.cutFrames > 0 ? $", the worst {j.worstCut:0.00} m: {j.worstWhat}" : "")}, frames with no bone or solid read {j.noBoneFrames}; the turn from the push to the touch {j.flip:0} deg ({FlipMinDeg:0}-{FlipMaxDeg:0}); "
                + $"the touch (its own frame {j.touchFrame}) {(touch >= 0 ? touch.ToString("0.00") : "NEVER")} s after the rules' click ({TouchMinSec:0.0}-{TouchMaxSec:0.0}), {vsHold:+0.00;-0.00} s from the hold {hold:0.00} s (within {TouchHoldSec:0.0}); "
                + $"the lower ankle there {(float.IsNaN(feet) ? "NOT MEASURED" : feet.ToString("+0.000;-0.000"))} m from its planted height in the walk on ({j.walkFrames} walk frames) and {(float.IsNaN(feetGround) ? "NOT MEASURED" : feetGround.ToString("+0.000;-0.000"))} m from his rig's ankle over the ground, the body {j.touchTilt:0.0} deg off upright; "
                + $"the leap: his drawn hips {(float.IsNaN(apexDrawn) ? "NOT MEASURED" : apexDrawn.ToString("0.00"))} m over the take-off (at least {apexMin:0.00}{(secondJ ? ", the second robot of the squad: a shorter flight" : "")}{(bailedJ ? ", a jump at once: not judged" : slipJ ? $", the show slipped {rec.slipSec:0.00} s (over {SlipJudgeSec:0.00}): not judged" : "")}), the plan {rec?.apexM:0.00} m, {rec?.vy0Mps:0.00} m/s up and {rec?.vhMps:0.00} m/s out; "
                + $"he landed {(float.IsNaN(landDrawn) ? "NOT MEASURED" : landDrawn.ToString("0.00"))} m from the take-off point ({LandMinM:0.0}-{LandMaxM:0.0}); "
                + $"the lid {(j.lidAtClick >= 0.05f || cmdJ ? "open at the click" : j.lidStartGT >= 0f && j.lidOpenGT >= 0f ? $"kicked open in {j.lidOpenGT - j.lidStartGT:0.00} s (at most {LidKickMaxSec:0.00})" : "NOT SEEN OPENING")}, "
                + $"he showed {(j.showGT >= 0f && j.lidStartGT >= 0f ? $"{j.showGT - j.lidStartGT:0.00} s after its first move (at most {ShowAfterLidMaxSec:0.00})" : "-")} with the lid {j.lidAtShow:0.00} open, first drawn \"{j.firstPhase}\" (crouch, or the push at a slow frame), "
                + $"pushed {(j.pushGT >= 0f && j.showGT >= 0f ? (j.pushGT - j.showGT).ToString("0.00") : "-")} s after the show (at most {PushAfterShowMaxSec:0.00}); "
                + $"the take-off against the plan (any frame rate): the drawn hips {j.errLastPush:0.000} m off the plan's in the last push frame and {j.errFirstAir:0.000} m in the first flight frame (at most {PlanPosTolM:0.00}), "
                + $"their speed over those steps {(j.vPushSet ? $"{j.vPushD.magnitude:0.00} (plan {j.vPushP.magnitude:0.00})" : "-")} / {(j.vAirSet ? $"{j.vAirD.magnitude:0.00} (plan {j.vAirP.magnitude:0.00}; up {j.vAirD.y:0.00}, out {new Vector2(j.vAirD.x, j.vAirD.z).magnitude:0.00})" : "-")} m/s (each within {PlanSpeedFrac * 100f:0} %, at least {PlanSpeedFloorMps:0.0}), "
                + $"the plan's own speed at the push's end against the flight's start {j.kink:0.000} m/s apart; "
                + $"phases in order {!j.phaseBack}; the plan: a clear arc {rec?.clearFound} (by {rec?.clearM:0.00} m), late {rec?.late}, his poses read on his rig {rec?.posesRead} ({rec?.clips}); "
                + $"sounds: take-off {takeoffs.Count} played (in the first flight frame +-{LandSoundFrames}: {takeoffSnd}; its own record {rec?.takeoffSound}), rob_land {lands.Count} (in the touch frame +-{LandSoundFrames}: {land}), exit {P("exit")}, vo_vehicle_exit asked {Asked("vo_vehicle_exit")}, "
                + $"lid open {opens} (needed: {lidWasShut}) / shut {closes} / hatch {hatches}, ground steps after the touch {steps}; stop: {stopText}");
            Log((jointsWhat ?? what.Replace("J1 ", "J1j ")) + ": every pose inside human joint limits", j.jointFrames > 0 && j.jointViol == 0,
                $"joint frames read {j.jointFrames} (of {j.jumpFrames} jump frames), violations {j.jointViol}{(j.jointViol > 0 ? ", the worst: " + j.worstJoint : "")} (hip {HipFlexMin:0}..{HipFlexMax:0}, spread {HipAbdMax:0}, knee 0..{KneeMax:0} "
                + $"and its hinge within {KneeSideMax:0} deg, spine {SpineMax:0}, neck {NeckMax:0})");
            if (rec != null)
            {
                Note("plan: " + rec.plan + $" (late {rec.late}, its own record: flip {rec.flipDeg:0} deg, frames {rec.frames}, size {rec.scaleMin:0.000}..{rec.scaleMax:0.000}, arcs tried {rec.tries})");
                Note($"the swap in the hatch: the figure shown in frame {rec.shownFrame}; his crouch clip {rec.crouchClip} ({rec.crouchRangeM:0.00} m deep; this crouch {rec.riseM:0.00} m), the lid's kick {rec.kickSec:0.00} s from {rec.kickAt:0.00} s, the flight's tuck {rec.tuckM:0.00} m, the landing's squat {rec.landDepthM:0.00} m; the knee row's idle reference read {rec.kneeRefOk}; {(rec.commanderWasUp ? $"the commander, up at the click, swapped out in frame {rec.commanderGoneFrame} ({rec.commanderSwapped}): {(rec.shownFrame >= 0 && rec.commanderGoneFrame >= 0 ? (rec.shownFrame - rec.commanderGoneFrame).ToString() : "?")} frame(s) between" : "no commander was up at the click")}; "
                     + $"the show slipped {rec.slipSec:0.00} s; crouched {rec.crouchSec:0.00} s (leaning back {rec.leanDeg:0.0} deg over his soles; {(rec.waitedSec > 1e-3f ? $"{rec.waitedSec:0.00} s longer for his legs" : "no longer for his legs")}), the push {rec.pushSec:0.00} s (swung upright about his soles, {rec.swingDegps:0} deg/s at the take-off; his legs straightened {rec.riseM:0.000} of {rec.riseMaxM:0.000} m, {rec.crouchClip} to {rec.legsAtTakeoff:0.00}); the take-off {rec.vhMps:0.00} m/s out, {rec.vy0Mps:0.00} m/s up; "
                     + $"the knee bend {rec.landDownSec:0.00} s, the stand-up {rec.landRiseSec:0.00} s; his ankle over the sole {rec.ankle0:0.000} m; the plan's closest point: {rec.worst}; "
                     + $"the plan at the click {rec.planMs:0.0} ms (the arcs {rec.arcsMs:0.0} ms: {rec.tries} arcs, {rec.gapTests} gap tests, {rec.gapSkipped} skipped by the bound; out of the budget {rec.budgetHit}; his rig from the cache {rec.bodyCached}), "
                     + $"he showed {rec.showLateSec:0.000} s after his planned moment (a slip only past one frame's step), the squad's robot {rec.index + 1}");
            }
            foreach (var e in log.Where(e => e.key == "exit" || e.key == "rob_land" || e.key == "hatch" || e.key == "latch" || e.key.StartsWith("veh_lid") || e.key.StartsWith("vo_") || e.key == "dst_ragdoll_whoosh" || e.key.StartsWith("rob_step_")))
                Note($"{e.t - j.clickT,6:0.00} s  {e.src,-4} {e.key,-18} {e.clip ?? "-",-28} {e.outcome}");
        }

        // ------------------------------------------------------------------ 14a, 14b, 14c: the free-spot rule and his side, with the jump
        static readonly System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        /// <summary>The drawn hull turned to `deg` as UnitView.FaceSide turns a placed vehicle (yaw, heading and prevYaw, the root's turn,
        /// HullSteer.Adopt, the slip follower reset, the turret along the new hull; the vehicle_exit kit's 14c, kept). View only; false = a field is missing.</summary>
        static bool TurnHull(UnitView vv, float deg)
        {
            var t = typeof(UnitView);
            var fYaw = t.GetField("yaw", NP); var fHead = t.GetField("heading", NP); var fPrev = t.GetField("prevYaw", NP); var fModel = t.GetField("modelYaw", NP);
            if (fYaw == null || fHead == null || fPrev == null || fModel == null) return false;
            fYaw.SetValue(vv, deg); fHead.SetValue(vv, deg); fPrev.SetValue(vv, deg);
            vv.transform.rotation = Quaternion.Euler(0f, deg + (float)fModel.GetValue(vv), 0f);
            var steer = t.GetField("hullSteer", NP)?.GetValue(vv); steer?.GetType().GetMethod("Adopt")?.Invoke(steer, new object[] { deg });
            vv.Slip?.Reset(vv.transform.position.x, vv.transform.position.z, deg);
            t.GetField("turretInit", NP)?.SetValue(vv, false);
            return true;
        }
        static bool Overlap(ZObject b, ZObject r) => r.LocX < b.LocX + b.WidthPix && r.LocX + r.WidthPix > b.LocX && r.LocY < b.LocY + b.HeightPix && r.LocY + r.HeightPix > b.LocY;
        /// <summary>A point on the ground (world) on a robot-passable tile and in no building (not a bridge) (the vehicle_exit kit's, kept).</summary>
        bool GroundFree(Vector3 p)
        {
            var px = ZGame.WorldToPx(p);
            if (px.x < 0 || px.y < 0 || !W.Path.TilePassable(px.x >> 4, px.y >> 4, true)) return false;
            foreach (var b in W.Objects) if (b.Kind == ZKind.Building && !b.IsBridge && !b.Destroyed && px.x >= b.LocX && px.x < b.LocX + b.WidthPix && px.y >= b.LocY && px.y < b.LocY + b.HeightPix) return false;
            return true;
        }
        /// <summary>Tiles free for robots and vehicles, off lava, no live object of any kind on them.</summary>
        bool ClearTiles(int x, int y, int w, int h)
        {
            if (x < 1 || y < 1) return false;
            for (int jj = 0; jj < h; jj++) for (int i = 0; i < w; i++) if (!TileFree(x + i, y + jj)) return false;
            return !W.Objects.Any(o => !o.Destroyed && o.KillAt < 0 && !(o.IsShuttle && o.ShuttleAirborne) && o.LocX < (x + w) * 16 && o.LocX + o.WidthPix > x * 16 && o.LocY < (y + h) * 16 && o.LocY + o.HeightPix > y * 16);
        }
        /// <summary>A live unit within `d` px (both axes) of the point: the rows keep them away, so the robot that comes out is the driver.</summary>
        bool UnitsNear(int x, int y, int d) => W.Objects.Any(u => u.IsUnit && !u.Destroyed && u.KillAt < 0 && Mathf.Abs(u.CenterX - x) < d && Mathf.Abs(u.CenterY - y) < d);
        /// <summary>A tank position (top-left tile) with its 2 x 2 hull box touching the box of `by` (a flag, or a building that is no bridge) on one
        /// side, nearest the camera's focus first; open ground on the far side of the hull; no other unit within 7 tiles (the vehicle_exit kit's
        /// step 14, kept).</summary>
        bool Beside(bool flag, out int tx, out int ty, out ZObject by)
        {
            g.GroundPoint(Camera.main.ScreenPointToRay(new Vector3(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f, 0)), out var c);
            var f = ZGame.WorldToPx(c);
            var list = W.Objects.Where(o => !o.Destroyed && o.KillAt < 0 && (flag ? o.Kind == ZKind.MapItem && o.Type == "flag" : o.Kind == ZKind.Building && !o.IsBridge))
                                .OrderBy(o => (long)(o.CenterX - f.x) * (o.CenterX - f.x) + (long)(o.CenterY - f.y) * (o.CenterY - f.y)).ThenBy(o => o.Id).ToList();
            foreach (var o in list)
            {
                int l = o.LocX >> 4, tp = o.LocY >> 4, rr = (o.LocX + o.WidthPix + 15) >> 4, b = (o.LocY + o.HeightPix + 15) >> 4;
                for (int side = 0; side < 4; side++)
                    for (int a = side < 2 ? tp - 1 : l - 1; a <= (side < 2 ? b - 1 : rr - 1); a++)
                    {
                        int x = side == 0 ? l - 2 : side == 1 ? rr : a, y = side == 2 ? tp - 2 : side == 3 ? b : a;
                        int ox = side == 0 ? x - 2 : side == 1 ? x + 1 : x - 1, oy = side == 2 ? y - 2 : side == 3 ? y + 1 : y - 1;
                        int ow = side < 2 ? 3 : 4, oh = side < 2 ? 4 : 3;
                        if (ClearTiles(x, y, 2, 2) && ClearTiles(ox, oy, ow, oh) && !UnitsNear(x * 16 + 16, y * 16 + 16, 112)) { tx = x; ty = y; by = o; return true; }
                    }
            }
            tx = ty = -1; by = null; return false;
        }
        /// <summary>The landing of that exit (its record) on free ground (GroundFree: a robot-passable tile, in no building) and on his side of the
        /// hull: the side his robot's spot is on (each axis the spot leaves the hull's middle by more than 8 px, the landing the same way).</summary>
        bool LandingOk(Jump j, int hx, int hy, out string text)
        {
            text = "no jump record"; if (j.robot == null || !BoardingView.HatchJumps.TryGetValue(j.robot.Id, out var rec)) return false;
            var lp = ZGame.WorldToPx(rec.landing); bool free = GroundFree(rec.landing);
            int sx = j.robot.CenterX - hx, sy = j.robot.CenterY - hy, lx = lp.x - hx, ly = lp.y - hy;
            bool side = (System.Math.Abs(sx) <= 8 || System.Math.Sign(lx) == System.Math.Sign(sx)) && (System.Math.Abs(sy) <= 8 || System.Math.Sign(ly) == System.Math.Sign(sy));
            text = $"the landing at ({lx:+0;-0}, {ly:+0;-0}) px from the hull's middle, his spot at ({sx:+0;-0}, {sy:+0;-0}): free ground {free}, on his side {side}";
            return free && side;
        }

        /// <summary>14a / 14b (the vehicle_exit kit's step 14, ported to the jump; review): a light tank parked beside a flag, a heavy tank against a
        /// building, each let out with a click on the tab: the rules' spot and his side free of buildings (as before), the landing free and on his
        /// side, no frame of the figure in the vehicle, the flag or a building (J1's cut count with the solids near), the robot not on the flag
        /// or in a building.</summary>
        IEnumerator JumpBesideFlagAndBuilding()
        {
            foreach (bool flag in new[] { true, false })
            {
                string what = flag ? "14a a light tank parked with its hull beside a flag: the jump" : "14b a heavy tank parked with a flank against a building: the jump";
                if (!Beside(flag, out int tx, out int ty, out var by)) { Log(what, false, "no free tank position beside a " + (flag ? "flag" : "building") + " with open ground beyond it on this level"); continue; }
                var rts = FindFirstObjectByType<RtsCamera>();
                if (rts) { rts.FlyTo(ZGame.PxToWorld(tx * 16 + 16, ty * 16 + 16)); yield return Real(2f); }
                var v = Unit("vehicle", flag ? "light" : "heavy", me, tx, ty);
                yield return Real(1.2f);
                input.Selection.Clear(); input.Selection.Add(v);                                               // set, not clicked: a click there may hit the flag or the building
                yield return Real(OpenZHud.DriverTabShowAfterSec + 0.3f);
                bool shown = DriverShown, can = v.CanExitDriver(out string why, out int sx, out int sy);
                int hx = v.CenterX, hy = v.CenterY;
                var j = new Jump();
                yield return WatchExit(v, j, flag ? "exit_jump_beside_flag_frames.png" : "exit_jump_beside_building_frames.png");
                var r = j.robot;
                bool onIt = r != null && Overlap(by, r), inBuilding = r != null && W.Objects.Any(b => b.Kind == ZKind.Building && !b.IsBridge && !b.Destroyed && Overlap(b, r));
                bool landOk = LandingOk(j, hx, hy, out string lt);
                Log(what, shown && can && r != null && v.Owner == Team.Neutral && !onIt && !inBuilding && landOk && j.jumpFrames > 0 && j.cutFrames == 0 && j.noBoneFrames == 0 && j.nearSolids > 0,
                    $"the {(flag ? "flag" : by.Type)} #{by.Id}; the tab shown {shown}, CanExitDriver {can} ({why ?? "-"}); the vehicle {v.Owner}; the driver {(r != null ? $"at ({r.CenterX - hx:+0;-0}, {r.CenterY - hy:+0;-0}) px" : "MISSING")} "
                    + $"(asked before the click ({sx - hx:+0;-0}, {sy - hy:+0;-0})), on the {(flag ? "flag" : "building")} {onIt}, in a building {inBuilding}; {lt}; the figure in {j.jumpFrames} jump frames against {j.solids.Count} solids "
                    + $"({j.nearSolids} of buildings / flags near): cut in {j.cutFrames} frames ({j.nearCutFrames} by a building / flag){(j.cutFrames > 0 ? ": " + j.worstWhat : "")}, no bone read {j.noBoneFrames}");
                Note("plan: " + BoardingView.HatchExitLastPlan);
                Gone(r); Gone(v); yield return Real(1f);
            }
        }

        /// <summary>14c (the vehicle_exit kit's, ported to the jump; review): on level 2 a light tank at tile (13, 5) (a building SE of it, its driver's
        /// spot SW) turned to 0, 45 .. 315 deg (the drawn hull turned): the landing free and on his side (the first free way when none is: 225 deg,
        /// front-first into his SW corner), no frame of the figure in the vehicle or the building; a frame sheet each.</summary>
        IEnumerator JumpTurnedBesideBuilding()
        {
            const string what = "14c level 2: a light tank at tile (13, 5) (a building SE of it, its driver's spot SW) turned to 0, 45 .. 315 deg: the jump";
            if (g.level != "L02") { Note("14c not run here (" + g.level + "): a level 2 row (OzVehicleExitCheck.Only = \"14c\" on level 2)"); yield break; }
            if (!ClearTiles(13, 5, 2, 2)) { Log(what, false, "the tiles (13, 5) .. (14, 6) are not free now (something stands on them)"); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) { rts.FlyTo(ZGame.PxToWorld(14 * 16, 6 * 16)); yield return Real(2f); }
            int ok = 0, n = 0, offSide = 0; var lines = new List<string>();
            foreach (float deg in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                var v = Unit("vehicle", "light", me, 13, 5);
                yield return Real(1.2f);
                bool turnedSet = g.Views.TryGetValue(v.Id, out var vv) && vv && TurnHull(vv, deg);
                yield return Real(0.8f);
                float drawnYaw = g.Views.TryGetValue(v.Id, out var vw) && vw ? vw.DrawnYaw : float.NaN;
                bool turned = turnedSet && !float.IsNaN(drawnYaw) && Mathf.Abs(Mathf.DeltaAngle(drawnYaw, deg)) <= 2f;
                input.Selection.Clear(); input.Selection.Add(v);
                yield return Real(OpenZHud.DriverTabShowAfterSec + 0.3f);
                bool can = v.CanExitDriver(out string why, out int sx, out int sy);
                int hx = v.CenterX, hy = v.CenterY;
                var j = new Jump();
                yield return WatchExit(v, j, $"exit_jump_l02_13_5_turned_{deg:000}_frames.png");
                bool landOk = LandingOk(j, hx, hy, out string lt);
                bool freeOnly = !landOk && deg == 225f && j.robot != null && BoardingView.HatchJumps.TryGetValue(j.robot.Id, out var rc) && GroundFree(rc.landing);   // front-first into his SW corner: the first free way
                if (!landOk) offSide++;
                bool good = turned && can && j.robot != null && v.Owner == Team.Neutral && (landOk || freeOnly) && j.jumpFrames > 0 && j.cutFrames == 0 && j.noBoneFrames == 0;
                n++; if (good) ok++;
                lines.Add($"{deg:0} deg (drawn {drawnYaw:0}{(turned ? "" : ": NOT TURNED")}): {(good ? "ok" : "WRONG")}, spot ({sx - hx:+0;-0}, {sy - hy:+0;-0}){(can ? "" : " (" + why + ")")}, {lt}{(freeOnly ? " (225 deg: the first free way)" : "")}, "
                          + $"cut in {j.cutFrames} of {j.jumpFrames} jump frames{(j.cutFrames > 0 ? " (" + j.worstWhat + ")" : "")}");
                Note($"14c {deg:0} deg plan: " + BoardingView.HatchExitLastPlan);
                Gone(j.robot); Gone(v);
                yield return Real(1f);
            }
            Log(what, n == 8 && ok == 8, string.Join("; ", lines) + $"; landings off his side {offSide}");
        }

        // ------------------------------------------------------------------ S1, S2: the vehicle stops and goes quiet
        /// <summary>A fresh crewed red vehicle of `type` on free ground near the camera (FreshTank), its driver of type `driver` (a psycho or a pyro
        /// for the hatch driver's own weapon, DV87; set as if he had climbed in), with enemy grunts 6-8 tiles east of it whose fire is off (CPU,
        /// Damage 0) and that never die: the vehicle's own fire is what is counted.</summary>
        IEnumerator FiringScene(string type, ZObject[] into, List<ZObject> foes, string driver = "grunt")
        {
            Focus(out int fx, out int fy);
            yield return FreshTank(type, fx, fy, into, 14, 8);
            var v = into[0]; if (v == null) yield break;
            if (driver != "grunt") v.DriverType = driver;
            for (int k = 0; k < 2; k++) { var f = Unit("robot", "grunt", foe, (v.CenterX >> 4) + 4 + 2 * k, (v.CenterY >> 4) - 1 + 2 * k, 1); f.Damage = 0; f.MoveSpeed = 0; f.MaxHealth = f.Health = 1000000000; foes.Add(f); }
            yield return Real(0.8f);
        }

        IEnumerator StopRows(bool frames)
        {
            // S1: EXIT right after a shot while it drives and fires (the mech: while its hand gun's burst runs); S1p / S1y: a light tank with a psycho / a pyro driver
            var s1 = new List<(string type, string driver)> { ("light", "grunt"), (ZMTank.Type, "grunt"), ("heavy", "grunt"), (ZMTank.Mech, "grunt"), ("light", "psycho"), ("light", "pyro") };
            foreach (var (type, driver) in s1)
            {
                if (frames && type != ZMTank.Mech) continue;                                                     // the owner frames: the mech's
                string row = driver == "grunt" ? $"S1 {Kind(type)}" : $"S1{(driver == "psycho" ? "p" : "y")} {Kind(type)} with a {driver} driver";
                var into = new ZObject[1]; var foes = new List<ZObject>();
                yield return FiringScene(type, into, foes, driver);
                var v = into[0];
                if (v == null) { Log($"{row}: EXIT while it drives and fires", false, "no free ground for the scene"); continue; }
                yield return Select(v);
                yield return Click(Screen(new Vector3(g.CenterWorld(v).x + 14f, 0f, g.CenterWorld(v).z - 1f)), true);   // a right-click past the enemies: an attack-move with this selection (the player's order)
                float t0 = Time.unscaledTime; int shots = 0, hg = 0, ds = 0; bool fired = false; float lastDs = -1f;
                while (Time.unscaledTime - t0 < 15f && !fired)
                {
                    foreach (var e in W.Events) if (ShotBy(e, v)) { shots++; if (e.type == ZEventType.MechHandGun) hg++; if (e.type == ZEventType.DriverShot) { ds++; lastDs = Time.unscaledTime; } }
                    bool moving = v.IsMoving && v.Waypoints.Count > 0;
                    if (type == ZMTank.Mech) fired = moving && hg > 0 && MechHandGunView.BurstNow(v.Id);                    // the click while its hand gun's burst runs
                    else if (driver != "grunt") fired = moving && ds > 0 && lastDs > 0f && Time.unscaledTime - lastDs < 0.15f && BoardingView.DriverMuzzleNow(v.Id, out _);   // while his own weapon fires from the hatch
                    else fired = moving && shots > 0;
                    if (!fired && v.Owner == me && !v.IsMoving && v.Waypoints.Count == 0) W.SendWaypoints(me, v, new[] { ZWaypoint.Move(v.CenterX + (v.CenterX < foes[0].CenterX ? 220 : -220), v.CenterY, true) });   // past them again
                    else if (!fired && driver != "grunt" && ds > 0 && v.Owner == me && !v.IsMoving) W.SendWaypoints(me, v, new[] { ZWaypoint.Move(v.CenterX + (v.CenterX < foes[0].CenterX ? 220 : -220), v.CenterY, false) });   // vehicle_exit_jump2 (check fault: the attack-move halted to fight and S1p never drove while the psycho fired): a plain move past them, his hatch weapon fires on the way
                    if (!fired) yield return null;
                }
                bool drivingAtClick = v.IsMoving && v.Waypoints.Count > 0, burstAtClick = type == ZMTank.Mech && MechHandGunView.BurstNow(v.Id);
                var j = new Jump();
                if (!input.Selection.Contains(v)) { input.Selection.Clear(); input.Selection.Add(v); yield return Real(OpenZHud.DriverTabShowAfterSec + 0.1f); }
                int jets0 = BoardingView.DriverJets; bool shownS1 = DriverShown;
                yield return WatchExit(v, j, frames || type == ZMTank.Mech ? $"stop_{type}_frames.png" : driver != "grunt" ? $"stop_{type}_{driver}_frames.png" : null, 6f);
                bool ok = StopOk(j, type == ZMTank.Mech ? 1 : 0, out string text);
                // the hatch driver's own weapon: his jet cues and machine-gun rounds after the emptied frame, the flame loop started after it, his muzzle refused
                int jetsAfter = j.jetsAt0 >= 0 ? BoardingView.DriverJets - j.jetsAt0 : -1, mgAfter = j.mgRoundsAt0 >= 0 && WeaponsV2Runner.I ? WeaponsV2Runner.I.MgRounds - j.mgRoundsAt0 : -1;
                int loopsAfter = OzSoundLog.Since(j.emptiedT > 0f ? j.emptiedT + 0.02f : j.clickT).Count(e => e.key == "wpn_pyro_flame_loop" && e.outcome != null && e.outcome.StartsWith("PLAY"));
                bool muzzleNow = BoardingView.DriverMuzzleNow(v.Id, out _);
                bool own = driver == "grunt" || (ds > 0 && !muzzleNow && (driver == "psycho" ? mgAfter == 0 : jetsAfter == 0 && loopsAfter == 0 && jets0 > 0 && OzVehicleQuiet.FlameLoopsCutOf(v.Id) > 0));
                Log($"{row}: EXIT DRIVER right after its shot while it drives (an attack-move) and fires at enemy grunts{(type == ZMTank.Mech ? ", its hand gun's burst running" : driver != "grunt" ? $", the {driver} firing from the hatch" : "")}",
                    fired && drivingAtClick && ok && own && OzVehicleQuiet.HeardBefore(v.Id) > 0 && v.Owner == Team.Neutral,                     // its weapon sounds were heard before (the count is alive; tanks: their gun launches too)
                    $"fired {shots} before (hand gun {hg}, the hatch driver {ds}), driving at the click {drivingAtClick}{(type == ZMTank.Mech ? $", a hand-gun burst running at the click {burstAtClick}" : "")}; "
                    + $"{(driver != "grunt" ? $"his weapon after the emptied frame: machine-gun rounds {mgAfter}, jet cues {jetsAfter} (before {jets0}), flame loops started {loopsAfter}, flame loops cut in the emptied frame {OzVehicleQuiet.FlameLoopsCutOf(v.Id)}, his muzzle now {(muzzleNow ? "STILL THERE" : "refused")}; " : "")}"
                    + $"{(type == ZMTank.Mech ? $"a killing hand-gun round pending at the stop: {(MechHandGunView.KillAtQuiet(v.Id) == 1 ? "drawn (the arm on its target)" : MechHandGunView.KillAtQuiet(v.Id) == 0 ? "dropped (the arm off it)" : "none")}; " : "")}{text}");
                // fix round 3 (review: a jump off a vehicle still braking was never measured): the whole jump verdict on every S1 row
                string s1j = $"S1j {Kind(type)}{(driver != "grunt" ? $" with a {driver} driver" : "")}: the jump off it right after the EXIT while it drove (his feet on the drawn rim of the braking hull)";
                JumpVerdict(s1j, j, shownS1, $"S1jj {Kind(type)}{(driver != "grunt" ? $" with a {driver} driver" : "")}: the jump off it after the EXIT while it drove");
                foreach (var f in foes) Kill(f);
            }
            // S2: the driver sniped by real enemy snipers while it DRIVES (the snipe must find it driving; up to three scenes)
            foreach (var type in new[] { "light", ZMTank.Type, "heavy", ZMTank.Mech })
            {
                if (frames && type != "light") continue;                                                         // the owner frames: a sniped tank
                bool rowDone = false; string why = "";
                for (int attempt = 0; attempt < 3 && !rowDone; attempt++)
                {
                    var into = new ZObject[1]; var foes = new List<ZObject>();
                    yield return FiringScene(type, into, foes);
                    var v = into[0];
                    if (v == null) { why = "no free ground for the scene"; break; }
                    var snipers = Unit("robot", "sniper", foe, (v.CenterX >> 4) + 5, (v.CenterY >> 4) - 5, 3);
                    W.Objects.Remove(v); W.Objects.Add(v);                                                             // fix round 2 (review): the vehicle runs after the snipers in every tick (the server loop's order): in the snipe's tick its whole step comes after the hit
                    yield return Real(0.3f);
                    input.Selection.Clear(); input.Selection.Add(v);
                    W.SendWaypoints(me, v, new[] { ZWaypoint.Move(v.CenterX + 100, v.CenterY, true) });            // first it fires (an attack-move past the grunts)
                    BoardingView.HatchExitStatsReset(); BoardingView.ExitWalkStatsReset();
                    var j = new Jump { v = v, turret = TurretOf(v), clickT = Time.unscaledTime, lidAtClick = BoardingView.HatchOpenOf(v.Id) };
                    StartCoroutine(MeasureLoop(j));                                                                    // from before the snipe: its frame is measured too
                    float sp0 = g.gameSpeed; g.gameSpeed = 2f;
                    bool sniped = false, movingPrev = false, movingAtSnipe = false, driving = false; float t0 = Time.unscaledTime;
                    while (Time.unscaledTime - t0 < 90f && !sniped && OzExitAlive(v))
                    {
                        if (Time.frameCount % 30 == 0) foreach (var m in SquadOf(snipers)) if (OzExitAlive(m) && m.AttackObject != v && v.Owner == me) CpuAttack(m, v);
                        foreach (var e in W.Events) if (e.type == ZEventType.Sniped && e.objId == v.Id) { sniped = true; movingAtSnipe = movingPrev; }   // its state as the last frame left it (before this frame's ticks)
                        if (!sniped && !driving && j.simShotsBefore > 0 && (type != ZMTank.Mech || j.simHgBefore > 0)) { driving = true; W.SendWaypoints(me, v, new[] { ZWaypoint.Move(v.CenterX - 220, v.CenterY, false) }); }   // then it drives to and fro (a plain move: no halt to fire); the mech after its hand gun fired
                        if (!sniped && driving && v.Owner == me && !v.IsMoving && v.Waypoints.Count == 0) W.SendWaypoints(me, v, new[] { ZWaypoint.Move(v.CenterX < snipers.CenterX ? v.CenterX + 220 : v.CenterX - 220, v.CenterY, false) });
                        movingPrev = v.IsMoving && v.Waypoints.Count > 0;
                        if (!sniped) yield return null;
                    }
                    g.gameSpeed = sp0;
                    if (!sniped) { why = $"no snipe in 90 s (the vehicle {(OzExitAlive(v) ? "alive" : "destroyed")}; its shots {j.simShotsBefore}): nothing measured"; j.done = true; Kill(snipers); foreach (var f in foes) Kill(f); continue; }
                    if (!movingAtSnipe && attempt < 2) { why = "the snipe found it standing"; j.done = true; Kill(snipers); foreach (var f in foes) Kill(f); Kill(v); yield return Real(0.5f); continue; }
                    yield return WatchExit(v, j, frames || type == "light" ? $"sniped_{type}_frames.png" : null, 4f, NoAct(), null, true);
                    bool ok = StopOk(j, type == ZMTank.Mech ? 2 : 0, out string text);
                    Log($"S2 {Kind(type)}: its driver sniped by a squad of three real enemy snipers while it drives (it fired before; the game at 2x until the snipe)", j.simShotsBefore > 0 && movingAtSnipe && j.snipe && ok && v.Owner == Team.Neutral,
                        $"attempt {attempt + 1}; its shots before the snipe {j.simShotsBefore}, driving at the snipe {movingAtSnipe} (read in the frame before the snipe's ticks); the sniper's own hit found {j.snipe}; {text}");
                    rowDone = true;
                    Kill(snipers); foreach (var f in foes) Kill(f);
                }
                if (!rowDone) Log($"S2 {Kind(type)}: its driver sniped by real enemy snipers while it drives", false, why);
            }
        }
        static IEnumerator NoAct() { yield break; }
        static IEnumerable<ZObject> SquadOf(ZObject o) { yield return o; foreach (var m in o.Minions) if (m != null) yield return m; }

        // ------------------------------------------------------------------ K1, K2: the hull or the driver killed during the exit
        /// <summary>Fix round 2 (review: K2 never required the body to come down): where his body is 2.5 s later: gone (popped, faded), or its hips'
        /// height above what is under them (the ground, or the hull's deck or turret top: BoardingView.HatchSurfaceY); inside the hull or turret.</summary>
        string BodyAtRest(ZObject r, ZObject v, out bool rests, out bool sunk)
        {
            rests = false; sunk = false;
            if (r == null || !g.Views.TryGetValue(r.Id, out var lv) || !lv || !lv.gameObject.activeInHierarchy) { rests = true; return "gone (popped or faded)"; }
            var hips = lv.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Hips"));
            var p = hips ? hips.position : lv.transform.position;
            bool deck = BoardingView.HatchUnderDeck(v.Id, p), turret = BoardingView.HatchInTurret(v.Id, p);
            sunk = deck || turret;
            float under = BoardingView.HatchSurfaceY(v.Id, p), above = p.y - under;
            rests = !sunk && above <= 0.6f;
            return $"hips {above:0.00} m above what is under them ({p.y - g.GroundY(p.x, p.z):0.00} m above the ground), {(deck ? "INSIDE THE HULL UNDER ITS DECK" : turret ? "INSIDE THE TURRET" : "not inside the hull or the turret")}";
        }

        IEnumerator KilledRows()
        {
            bool exitLeapThrown0 = ZWorld.DriverThrownDeviation; ZWorld.DriverThrownDeviation = false;   // 1.5 kit exit_leap_death: K1 is 1.4's jump at once = DV179 off (the throw's own rows: View/OzExitLeapDeathCheck.cs)
            foreach (var type in new[] { "light", ZMTank.Mech })
                foreach (var (at, phase) in new[] { (0.2f, "lid"), (0.35f, "crouch") })
                {
                    Focus(out int fx, out int fy);
                    var into = new ZObject[1]; yield return FreshTank(type, fx, fy, into);
                    var v = into[0]; if (v == null) { Log($"K1 {Kind(type)} {phase}", false, "no free ground"); continue; }
                    yield return Select(v);
                    var j = new Jump(); bool killed = false;
                    yield return WatchExit(v, j, $"hull_killed_{type}_{phase}_frames.png", 7f, null, jj =>
                    {
                        if (jj.robot != null && jj.robot.MaxHealth < 1000000000) { jj.robot.MaxHealth = jj.robot.Health = 1000000000; }   // vehicle_exit_jump2 (check fault: the mech's driver died in its blast): the row is the jump at once, not the blast
                        if (!killed && jj.stopSim >= 0 && W.Time - jj.stopSim >= at) { killed = true; Kill(v); }
                    });
                    BoardingView.HatchJumpRec rec = null; if (j.robot != null) BoardingView.HatchJumps.TryGetValue(j.robot.Id, out rec);
                    var log = OzSoundLog.Since(j.clickT);
                    bool land = log.Any(e => e.key == "rob_land" && Played(e));
                    float lid = BoardingView.HatchOpenOf(v.Id);
                    bool alive = OzExitAlive(j.robot);
                    bool tilt = j.touchFrame > 0 && j.touchTilt <= TiltAtTouchDeg;
                    Log($"K1 {Kind(type)} destroyed {at:0.00} s after the EXIT ({(phase == "lid" ? "while the lid opens" : "while he crouch-springs on the rim")}): the same jump at once",
                        killed && rec != null && rec.bailed && tilt && land && alive && ((lid >= 0f && lid <= 0.01f) || (lid < 0f && BoardingView.HatchLidsShut > 0)) && !j.phaseBack,
                        $"killed {killed}; the jump at once {rec?.bailed}, touched {(j.touchFrame > 0 ? $"{j.touchT - j.clickT:0.00} s after the click" : "NEVER")} (tilt {j.touchTilt:0.0} deg), "
                        + $"turned {j.flip:0} deg; rob_land {land}; the driver alive {alive}; the wreck's lid {lid:0.00} (0 .. 0.01; -1: the wreck's rig gone, then its lid must have been shut by the jump at once: lids shut {BoardingView.HatchLidsShut}); plan: {rec?.plan}");
                }
            ZWorld.DriverThrownDeviation = exitLeapThrown0;                                             // exit_leap_death: DV179 back as the game has it for K2 (the driver's own death)
            foreach (var (type, phase, head) in new[] { ("light", "crouch", false), ("light", "air", false), ("light", "air", true), (ZMTank.Mech, "crouch", false), (ZMTank.Mech, "air", false) })
            {
                Focus(out int fx, out int fy);
                var into = new ZObject[1]; yield return FreshTank(type, fx, fy, into);
                var v = into[0]; if (v == null) { Log($"K2 {Kind(type)} {phase}", false, "no free ground"); continue; }
                ZObject sniper = head ? Unit("robot", "sniper", foe, (v.CenterX >> 4) + 6, (v.CenterY >> 4) - 6, 1) : null;
                if (sniper != null) { sniper.Damage = 0; sniper.MoveSpeed = 0; }
                yield return Select(v);
                var j = new Jump(); bool killed = false; float diedAt = -1f; Vector3 figAt = default;
                yield return WatchExit(v, j, $"killed_{type}_{phase}{(head ? "_headshot" : "")}_frames.png", 6f, null, jj =>
                {
                    if (killed || jj.robot == null) return;
                    BoardingView.HatchFigureNow(jj.robot.Id, out _, out _, out _, out string ph);
                    if (ph != phase) return;
                    if (phase == "air" && BoardingView.HatchJumps.TryGetValue(jj.robot.Id, out var rr) && jj.stopSim >= 0 && W.Time - jj.stopSim < rr.takeoffAt + 0.35f * rr.flight) return;   // a third into the flight
                    killed = true; diedAt = Time.unscaledTime; figAt = jj.lastFigure;
                    if (sniper != null && OzExitAlive(sniper)) { jj.robot.HeadshotBy = sniper.Id; jj.robot.HeadshotAt = W.Time; jj.robot.SetHealth(0); W.UpdateObjectHealth(jj.robot, sniper.Id); }   // DV116's own headshot death, by that sniper
                    else { jj.robot.SetHealth(0); W.UpdateObjectHealth(jj.robot); }                                  // the rules' own death (as a fatal hit)
                });
                yield return Real(2.5f);
                var log = OzSoundLog.Since(j.clickT);
                bool deathSound = diedAt > 0f && log.Any(e => e.t >= diedAt - 0.1f && e.key != null && (e.key.Contains("death") || e.key.Contains("die") || e.key.StartsWith("rob_pop") || e.key.Contains("head")) && Played(e));
                var at = BoardingView.HatchExitLastDieAt; var dd = at - figAt; dd.y = 0f;
                bool onFigure = BoardingView.HatchExitDiesShown >= 1 && BoardingView.HatchExitLastDieFrom.StartsWith("figure") && dd.magnitude < 0.6f;
                string rest = BodyAtRest(j.robot, v, out bool rests, out bool sunk);
                bool left = BoardingView.HatchFigureNow(j.robot != null ? j.robot.Id : -1, out _, out _, out _, out _) == 0;
                Log($"K2 {Kind(type)}: the driver killed {(phase == "crouch" ? "while he crouch-springs on the rim" : "in the air")}{(head ? " by a real enemy sniper's headshot (DV116)" : "")}: he dies where his figure is and comes to rest",
                    killed && onFigure && deathSound && rests && !sunk && left,
                    $"killed {killed}; his model shown for the death from \"{BoardingView.HatchExitLastDieFrom}\", {dd.magnitude:0.00} m from the figure's last place; a death sound {deathSound}; "
                    + $"no figure left {left}; the body 2.5 s later: {rest} (at rest: at most 0.6 m above it, or gone)");
                if (sniper != null) Kill(sniper);
            }
        }

        // ------------------------------------------------------------------ P: pause in the air; E: a robot ordered in during the jump; P2: the game over in the air
        IEnumerator PauseAndEnter()
        {
            if (Do("P"))
            {
                Focus(out int fx, out int fy);
                var into = new ZObject[1]; yield return FreshTank(ZMTank.Mech, fx, fy, into);
                var v = into[0];
                if (v == null) Log("P the game paused while he is in the air", false, "no free ground");
                else
                {
                    yield return Select(v);
                    int errors = 0; void OnLog(string c, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception) errors++; }
                    Application.logMessageReceived += OnLog;
                    var j = new Jump(); bool paused = false; float pauseAt = -1f, drift = 0f; Vector3 p0 = default; var seenP = new List<string>();
                    try
                    {
                        yield return WatchExit(v, j, null, 9f, null, jj =>
                        {
                            if (jj.robot == null) return;
                            BoardingView.HatchFigureNow(jj.robot.Id, out _, out _, out _, out string ph);
                            if (seenP.Count == 0 || seenP[seenP.Count - 1] != ph) seenP.Add(ph == "" ? "none" : ph);                                    // vehicle_exit_jump2: the phases seen
                            if (pauseAt < 0f && ph == "air") { pauseAt = Time.unscaledTime; paused = true; g.paused = true; BoardingView.ExitWalkFigure(jj.robot.Id, out p0, out _); }
                            else if (pauseAt > 0f && g.paused)
                            {
                                BoardingView.ExitWalkFigure(jj.robot.Id, out var p, out _); drift = Mathf.Max(drift, (p - p0).magnitude);
                                if (Time.unscaledTime - pauseAt >= 1f) g.paused = false;
                            }
                        });
                    }
                    finally { Application.logMessageReceived -= OnLog; g.paused = false; }
                    Log("P the game paused while the mech's driver is in the air (1 s), then resumed: he stands still, then lands; no error", paused && drift < 0.01f && j.touchFrame > 0 && errors == 0,
                        $"paused in the air {paused}, the figure moved {drift:0.000} m while paused; landed after the resume {j.touchFrame > 0} (touch {(j.touchFrame > 0 ? (j.touchT - j.clickT).ToString("0.00") : "-")} s real time after the click); errors {errors}; "
                        + $"the phases seen {string.Join(" > ", seenP)}; the driver alive {OzExitAlive(j.robot)}");
                }
            }
            if (Do("E"))
            {   // fix round 2 (review: the pyro came after the landing, so no wait was ever tested): a pyro right beside the hull, ordered in at the click
                Focus(out int fx, out int fy);
                var into = new ZObject[1]; yield return FreshTank(ZMTank.Mech, fx, fy, into, 10, 8);
                var v = into[0];
                if (v == null) { Log("E a pyro ordered in during the jump", false, "no free ground"); yield break; }
                var pyro = Unit("robot", "pyro", me, (v.CenterX >> 4) + 2, (v.CenterY >> 4) - 1, 1);         // one tile off the hull's east side
                yield return Real(0.5f);
                yield return Select(v);
                var j = new Jump(); bool ordered = false, both = false, inHeld = false; float minGap = float.MaxValue, releasedAt = -1f; int rel0 = 0; var seenE = new List<string>();
                yield return WatchExit(v, j, "pyro_in_during_jump_frames.png", 9f, null, jj =>
                {
                    if (!ordered && jj.stopSim >= 0) { ordered = true; rel0 = BoardingView.HatchEntersReleased; W.SendWaypoints(me, pyro, new[] { new ZWaypoint { mode = WpMode.Enter, refId = v.Id, x = v.CenterX, y = v.CenterY } }); }   // the player's ENTER order, at the click
                    if (jj.robot != null) { BoardingView.HatchFigureNow(jj.robot.Id, out _, out _, out _, out string phE); if (seenE.Count == 0 || seenE[seenE.Count - 1] != phE) seenE.Add(phE == "" ? "none" : phE); }   // vehicle_exit_jump2: the phases seen
                    BoardingView.HatchFiguresOf(v.Id, out bool outShown, out var outPos, out bool outOn, out bool inShown, out var inPos, out bool inOn, out bool held);
                    if (outOn && inOn) both = true;
                    if (held) inHeld = true;
                    if (releasedAt < 0f && ordered && BoardingView.HatchEntersReleased > rel0) releasedAt = Time.unscaledTime;
                    if (outShown && inShown) minGap = Mathf.Min(minGap, (outPos - inPos).magnitude);
                });
                float t1 = Time.unscaledTime; while (Time.unscaledTime - t1 < 8f && !(v.Owner == me && v.DriverType == "pyro")) yield return null;
                bool afterTouch = releasedAt > 0f && j.touchFrame > 0 && releasedAt >= j.touchT - 0.1f;
                Log("E a pyro beside the emptied mech ordered in (the player's ENTER order) at the click: it waits off the hull until the driver has landed, then climbs in",
                    ordered && inHeld && BoardingView.HatchEntersHeld > 0 && afterTouch && !both && v.Owner == me && v.DriverType == "pyro" && (minGap == float.MaxValue || minGap >= 0.5f),
                    $"ordered {ordered}; held outside {inHeld} (boardings held {BoardingView.HatchEntersHeld}, released {BoardingView.HatchEntersReleased}, the last wait {BoardingView.HatchEnterLastHold:0.00} s); released "
                    + $"{(releasedAt > 0f ? (releasedAt - j.clickT).ToString("0.00") : "NEVER")} s after the click, the touch {(j.touchFrame > 0 ? (j.touchT - j.clickT).ToString("0.00") : "NEVER")} s (released after the touch - 0.1 s {afterTouch}); "
                    + $"both figures on the hull in a frame {both}; the least gap between them {(minGap == float.MaxValue ? "-" : minGap.ToString("0.00"))} m; the mech {v.Owner}, driver {v.DriverType}; "
                    + $"the driver {(j.robot == null ? "NOT FOUND" : OzExitAlive(j.robot) ? "alive" : "DEAD")}, the phases seen {string.Join(" > ", seenE)}");
            }
        }

        /// <summary>P2 (fix round 2, review: no row for the game over mid-jump; LAST: it ends the level): a mech's driver let out, every enemy unit
        /// taken at the click (the game is won within the rules' next second, while he is in the hatch or in the air): he lands, his figure never
        /// stands still in the air for 0.5 s, no error in the Console.</summary>
        IEnumerator GameEndMidJump()
        {
            Focus(out int fx, out int fy);
            var into = new ZObject[1]; yield return FreshTank(ZMTank.Mech, fx, fy, into);
            var v = into[0]; if (v == null) { Log("P2 the game over while he is in the air", false, "no free ground"); yield break; }
            yield return Select(v);
            int errors = 0; void OnLog(string c, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception) errors++; }
            Application.logMessageReceived += OnLog;
            var j = new Jump(); bool won = false; double wonAt = -1; string phaseAtEnd = ""; float stillSince = -1f, stillMax = 0f; Vector3 last = default; bool lastSet = false;
            try
            {
                yield return WatchExit(v, j, "game_over_mid_jump_frames.png", 6f, null, jj =>
                {
                    if (jj.stopSim >= 0 && !W.GameOver && (!won || W.Time - wonAt < 3.0))
                    {   // vehicle_exit_jump2 (check fault: in the first jump kit's run L17 was not over 6 s after one sweep): every enemy unit taken, again each frame (new ones from a factory, crews of a wreck) until the game is over
                        if (!won) { won = true; wonAt = W.Time; }
                        foreach (var o in W.Objects.ToList()) if (o.IsUnit && o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.KillAt < 0) Kill(o);
                    }
                    if (jj.robot == null) return;
                    BoardingView.HatchFigureNow(jj.robot.Id, out _, out _, out _, out string ph);
                    if (W.GameOver && phaseAtEnd == "") phaseAtEnd = ph == "" ? "none" : ph;
                    if (W.GameOver && (ph == "crouch" || ph == "push" || ph == "air") && BoardingView.ExitWalkFigure(jj.robot.Id, out var p, out _) == 2)
                    {
                        if (lastSet && (p - last).magnitude < 0.001f) { if (stillSince < 0f) stillSince = Time.unscaledTime; stillMax = Mathf.Max(stillMax, Time.unscaledTime - stillSince); } else stillSince = -1f;
                        last = p; lastSet = true;
                    }
                });
            }
            finally { Application.logMessageReceived -= OnLog; }
            bool mid = phaseAtEnd == "inside" || phaseAtEnd == "crouch" || phaseAtEnd == "push" || phaseAtEnd == "air";
            Log("P2 the game won (every enemy unit gone at the click) while the mech's driver gets out: he lands, nothing frozen in the air, no error (the level ends here)",
                won && W.GameOver && mid && j.touchFrame > 0 && stillMax < 0.5f && errors == 0,
                $"enemy units taken {won}; the game over {W.GameOver}, his phase then \"{phaseAtEnd}\" (must be inside / crouch / air: mid-exit {mid}); landed {j.touchFrame > 0} "
                + $"({(j.touchFrame > 0 ? (j.touchT - j.clickT).ToString("0.00") : "-")} s real time after the click); the longest stand-still in the air after the game over {stillMax:0.00} s; errors {errors}"
                + (W.GameOver ? "" : "; still in the game: " + string.Join(", ", W.Objects.Where(o => o.IsUnit && o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed).Take(8).Select(o => $"{o.Type} #{o.Id} {o.Owner}{(o.IsFortDefender ? " (a fort defender)" : "")}"))));
        }

        /// <summary>Step 11: a light tank whose hull is under the place where the tab appears, and a second light tank on screen. Nothing
        /// selected: a real double-click there selects both light tanks and lets nobody out; then a click and a second click 0.33 s later.</summary>
        IEnumerator UnderTheTab()
        {
            input.Selection.Clear(); yield return Real(0.3f);
            var tab = hud.DriverExitButtonRect;
            if (!tab) { Log("11 a light tank under the tab's place", false, "no EXIT DRIVER tab"); yield break; }
            var mid = Mid(tab);
            if (!g.GroundPoint(Camera.main.ScreenPointToRay(new Vector3(mid.x, mid.y, 0)), out var gp)) { Log("11 a light tank under the tab's place", false, "no ground under the tab"); yield break; }
            var px = ZGame.WorldToPx(gp);
            if (!AreaFree((px.x >> 4) - 7, (px.y >> 4) - 2, 9, 4))                                         // the tank under the tab and a second one 5 tiles west of it
            {   // fly the view so that free ground lies under the tab
                if (!BlockNear(px.x >> 4, px.y >> 4, 9, 4, 30, out int bx, out int by)) { Log("11 a light tank under the tab's place", false, "no free ground found near the place under the tab"); yield break; }
                var rts = FindFirstObjectByType<RtsCamera>();
                var want = ZGame.PxToWorld((bx + 7) * 16, (by + 2) * 16);
                if (rts) rts.FlyTo(rts.focus + (want - gp));
                yield return Real(2.5f);
                mid = Mid(tab);
                if (!g.GroundPoint(Camera.main.ScreenPointToRay(new Vector3(mid.x, mid.y, 0)), out gp)) { Log("11 a light tank under the tab's place", false, "no ground under the tab after the flight"); yield break; }
                px = ZGame.WorldToPx(gp);
            }
            var under = UnitPx("vehicle", "light", me, px.x - 16, px.y - 16);
            var other = UnitPx("vehicle", "light", me, px.x - 16 - 80, px.y - 16);
            yield return Real(1.2f);
            var sp = Screen(under); float off = (sp - mid).magnitude;
            int e0 = W.DriverExits; int held0 = OpenZHud.DriverTabHeldBack;
            yield return DoubleClick(mid);
            yield return Real(0.1f);
            var sel = input.Selection.ToList();
            bool all = sel.Contains(under) && sel.Contains(other) && sel.All(o => o.Type == "light" && o.Owner == me);
            bool tabThen = DriverShown;
            yield return Real(0.6f);
            bool exitsA = W.DriverExits == e0 && under.Owner == me && under.Drivers.Count == 1 && other.Owner == me;
            yield return Picture("tab_over_tank_after_double_click.png", new Rect(mid.x - 220, mid.y - 120, 440, 240));
            Log("11a a light tank under the tab's place (its centre " + off.ToString("0") + " px from the tab's middle), nothing selected: a real double-click on it",
                exitsA && all && !tabThen && off < 40f,
                $"selected {sel.Count} ({string.Join(", ", sel.Select(o => o.Type + " #" + o.Id))}), both light tanks {all}; the tab shown right after {tabThen} (held back {OpenZHud.DriverTabHeldBack - held0} frames); drivers let out {W.DriverExits - e0}; the tanks {under.Owner} / {other.Owner}");
            input.Selection.Clear(); yield return Real(0.5f);
            e0 = W.DriverExits;
            yield return Click(mid);
            yield return Real(0.33f - 7f / 60f);
            yield return Click(mid);
            yield return Real(0.6f);
            Log("11b the same place: a click, and a second click 0.33 s later", W.DriverExits == e0 && under.Owner == me && under.Drivers.Count == 1,
                $"drivers let out {W.DriverExits - e0}; the tank {under.Owner} with {under.Drivers.Count} driver(s); selected now {input.Selection.Count}");
        }
    }
}
#endif
