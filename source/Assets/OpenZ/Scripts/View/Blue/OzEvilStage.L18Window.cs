// OpenZ (GPL-3.0)
// The scene L18_start of blue_scenes.json plays on the evil HQ as every blue
// scene does; this part adds what only it has, built and shown for that scene alone (every other scene: the room as it was):
//  - THE MAIN WINDOW: an armoured steel wall across the lens side of the room with one wide opening (MODELLED: Resources/OpenZ/L18Cut/
//    L18_WindowWall: plates, a deep splayed embrasure, the armoured frame with its rivets, the shutter box with its slats, the sill; in
//    the room's own steel). The scene's first picture (`win_two`) looks in through it from outside and HOLDS through "Three bridges!":
//    the frame round both robots, EvilZod at the sill, BS-01 behind his shoulder. The wall casts no shadow (the robots' approved lights
//    reach them as before); a warm dawn light comes in with the lens.
//  - THE BINOCULARS (MODELLED: L18_Binoculars, porro glasses: stepped barrels, rubber eyecups and armour rings, coated front glass, a
//    focus wheel, strap lugs): at his eyes in both hands, then in the right hand alone (the free fist goes up on "victory"). On BS-01's
//    last word a short WIND-UP (the glasses 2 cm up and in, the shoulders rise), then he tears them down in an ARC (out in front of the
//    chest, then down and out to the right hip) with a small overshoot and a hard stop. From the wind-up's first frame the glasses are
//    RIGID IN HIS GRIP (hand pose x the grip's own fixed offset: they cannot leave the fist); at his eyes before that they follow his head.
//  - THE CONSOLE with TWO BIG RED MUSHROOM BUTTONS at his left hip (MODELLED: L18_Console: bevelled housing, yellow collars, dark
//    bases, warning stripes, a cable conduit, two lit picture plaques standing behind the buttons: a cup, a bridge: ButtonPlates). The
//    buttons stand ButtonHalf x 2 apart: more than a palm, so the wrong one reads. Beat `pat_console` (a = seconds to the contact) runs
//    the whole gag on the clock, counted back from the press: the hand comes down, pats the panel twice (sharp contacts, round lifts),
//    hovers over the cup button with a small dip, slides to the bridge button and comes down flat on it: the dome sinks and STAYS down
//    under his palm (the palm does not lift again until the wind-up), its lamp flashes, the clunk, a kick.
//  - Never still: through the blasts he keeps the glasses up and leans slowly toward the glass (beat `lean_glass`).
//  - THE BLAST LIGHT through the window (set beat `flash`), the alarm's two chirps (faded out, no click) and its red lamp (`alarm_chirp`).
//  - Framings: win_two (through the window), l18_two / l18_calm (both, with the sill and one jamb of the window in the picture), console.
// New EvilZod beats: binoc_up (a 2 = both hands), look_out, binoc_sweep (hold = seconds), fist_up_l, pat_console (a = seconds to the press),
// lean_glass (a = degrees, hold = seconds), binoc_windup, binoc_off, point_window (the open hand thrown out at the window: his model has
// no pointing finger), hand_l_hip, binoc_sink.
// Human limits: every hand target is inside the arm's reach from its own shoulder, the elbows bend about their measured hinges
// (OzPoseRig.IkArmHinge: no wrist turn is made), the head follows the stage's own look rule (45 degrees at most on this scene). COUNTED
// IN PLAY, every frame of the scene (L18Status): each elbow's bend (never straight, never over 150), the head's turn from the chest
// (45 at most), each wrist's turn since the scene began (none wanted): L18JointOver must be 0.
// The blasts' SOUND is the full-screen stage's (OzL18BriefingStage): the window adds none of its own, so nothing sounds twice.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>The scene's modelled props (Resources paths): the check menu counts them; without one the scene logs an error (no stand-in is built).</summary>
        public const string L18WallRes = "OpenZ/L18Cut/L18_WindowWall", L18ConsoleRes = "OpenZ/L18Cut/L18_Console", L18BinocRes = "OpenZ/L18Cut/L18_Binoculars";
        public static readonly string[] L18Props = { L18WallRes, L18ConsoleRes, L18BinocRes };
        /// <summary>Level 18's marks (stage frame: the robots face +Z, the lens side; +X is screen left): EvilZod at the sill, BS-01 behind his right shoulder.</summary>
        public static Vector3 L18EzMark = new Vector3(0.30f, 0f, 0.10f), L18SkMark = new Vector3(1.50f, 0f, -0.75f);
        /// <summary>The window wall: its INNER face (z; the wall is 0.35 m thick, outward) and the model's opening: left / right edge (x), sill and
        /// lintel (y). The opening's numbers are the model's own (L18_WindowWall): they frame the pictures, they do not reshape the wall.</summary>
        public static float WinZ = 1.75f, WinLift = 0.35f; public const float WinLeft = 1.85f, WinRight = -1.45f, WinLo = 0.60f, WinHi = 2.80f;
        /// <summary>The establishing picture through the window: the point it looks at and how far out the lens stands (m).</summary>
        public static Vector3 WinTwoLook = new Vector3(0.66f, 1.72f, 0f); public static float WinTwoDist = 7.6f, WinFov = 24f;
        /// <summary>The two-shot through the window (l18_two; l18_calm stands half a metre further out): its look point and the lens's distance.</summary>
        public static Vector3 L18TwoLook = new Vector3(0.50f, 1.58f, 0f); public static float L18TwoDist = 6.0f;
        /// <summary>The console's top from his LEFT shoulder (m at his 2.30 m rig: to his left, down, ahead), half the distance of its two domes'
        /// centres, the dome's radius, its travel, the dome's top over the panel (the model's own numbers: L18_Console).</summary>
        public static Vector3 ConsoleFromShoulder = new Vector3(-0.50f, -0.72f, 0.22f); public static float ButtonHalf = 0.225f, ButtonR = 0.11f, ButtonTravel = 0.024f;
        public const float DomeRise = 0.085f, ConsoleModelTop = 1.0f;
        /// <summary>The two lit picture plaques behind the buttons (a cup, a bridge). The owner may drop them: false.</summary>
        public static bool ButtonPlates = true;
        /// <summary>The binoculars: ahead of his eyes and under them (m at 2.30), half the distance of his two grips, their size.</summary>
        public static float BinocAhead = 0.17f, BinocDrop = 0.015f, BinocGripHalf = 0.105f, BinocSize = 1.25f;
        /// <summary>The press: seconds before the contact the hover over the cup button begins, the slide begins, the fall begins.</summary>
        public static float PatHoverAt = 0.62f, PatSlideAt = 0.42f, PatFallAt = 0.16f, HoverHigh = 0.15f; public static int Pats = 1;
        /// <summary>The tear-down: its seconds, the overshoot past the hip (m at 2.30), the wind-up's lift (m), the arc's middle (his standing
        /// frame at 2.30: right, up, ahead) and its end at the right hip.</summary>
        public static float TearSeconds = 0.30f, TearOver = 0.03f, WindLift = 0.06f, WindSeconds = 0.20f;
        public static Vector3 TearMid = new Vector3(0.34f, 1.60f, 0.64f), TearEnd = new Vector3(0.42f, 1.30f, 0.34f);
        /// <summary>The lights of the scene: the dawn through the window, the blast flash at strength 1, the alarm lamp.</summary>
        public static float DawnI = 20f, BlastI = 120f, AlarmLampI = 6f, FrameI = 26f, RimI = 34f;
        /// <summary>The insert's lens: distance (m), field of view, height angle, heading.</summary>
        public static float InsertDist = 1.20f, InsertFov = 46f, InsertElev = 6f, InsertYaw = -12f;   // nearly level: the palm shows UNDER the two signs, the domes under the palm
        /// <summary>The insert's aim moved to the picture's left (m) and its distance factor: the commander and BS-01 whole at the left edge, the stand and both plates still in.</summary>
        public static float InsertLeft = 0.32f, InsertBack = 1.20f;

        bool l18On; Transform l18Root, binoc, consoleRoot, consoleTopT, consolePed, domeB; Material domeAMat, domeBMat; Light l18Dawn, l18Blast, l18Lamp; AudioSource l18Alarm;
        readonly List<GameObject> l18Plates = new List<GameObject>();
        Vector3 consoleTop; float l18PrevClock = -99f, chirpVol;
        float sweepAt = -10f, sweepDur = 1f, sweepAmp = 1f, patAt = -10f, pressAt = -10f, flashAt = -10f, flashK, chirpAt = -10f, tearAt = -10f, windAt = -10f, leanAt = -10f, leanDur = 1f, leanDeg; Vector3 patFrom, tearFrom;
        bool l18Pressed, binocGrip, tearStopped; Vector3 gripPos, domeRest; Quaternion gripRot = Quaternion.identity; float l18DomePress, l18LampK, binocPress;
        // the joint gate of the scene (Play): frames looked at, frames over a limit, the worst numbers
        Quaternion wristRestR = Quaternion.identity, wristRestL = Quaternion.identity; bool jointInit;
        /// <summary>Play checks: the scene is on, the contact's clock second (-1 = not yet), the palm's distance from the bridge button's top at that second (m).</summary>
        public bool L18On => l18On;
        public static float L18PressClock = -1f, L18PressMiss = -1f;
        public static int L18Flashes, L18Chirps, L18Pats, L18JointFrames, L18JointOver;
        public static float L18ElbowMin = 999f, L18ElbowMax, L18HeadMax, L18WristMax, L18GripSlip; public static string L18JointWorst = "";

        // ------------------------------------------------------------------------------------------------ scene begin
        void BeginL18Window(OzBlueScene s)
        {
            l18On = s != null && s.id == OzCity.BlueStartScene && ez != null && ez.Ok;
            if (!l18On) { if (l18Root) l18Root.gameObject.SetActive(false); if (binoc) binoc.gameObject.SetActive(false); L18DofOff(); return; }
            ez.Root.localPosition = L18EzMark; ApplyYaw(ez, 0f);
            if (sk != null && sk.Ok) sk.Root.localPosition = L18SkMark;
            float k = EzH / 2.3f;
            Vector3 sh = L(ez.Rig.ArmL.position);                                                        // his left shoulder at rest (the rig was reset just now)
            consoleTop = new Vector3(sh.x + ConsoleFromShoulder.x * k, sh.y + ConsoleFromShoulder.y * k, sh.z + ConsoleFromShoulder.z * k);
            EnsureL18Set();
            l18Root.gameObject.SetActive(true); if (binoc) binoc.gameObject.SetActive(true);
            if (consoleRoot) consoleRoot.localPosition = new Vector3(consoleTop.x, 0f, consoleTop.z);
            if (consoleTopT) consoleTopT.localPosition = new Vector3(0f, consoleTop.y - ConsoleModelTop, 0f);   // the housing and its buttons at his hip's height
            if (consolePed) consolePed.localScale = new Vector3(1f, Mathf.Max(0.3f, consoleTop.y / ConsoleModelTop), 1f);   // the pedestal reaches up to it
            foreach (var p in l18Plates) if (p) p.SetActive(ButtonPlates);
            lungeAt = lungeEnd = headNoAt = glowFrom = -10f; l18CamWas = null; L18DofOn();
            sweepAt = patAt = pressAt = flashAt = chirpAt = tearAt = windAt = leanAt = -10f; l18Pressed = false; l18DomePress = 0f; l18LampK = 0f; flashK = 0f; l18PrevClock = -99f;
            binocGrip = tearStopped = false; binocPress = 0f; jointInit = false;
            L18PressClock = L18PressMiss = -1f; L18Flashes = L18Chirps = L18Pats = L18JointFrames = L18JointOver = 0; L18ElbowMin = 999f; L18ElbowMax = L18HeadMax = L18WristMax = L18GripSlip = 0f; L18JointWorst = "";
            if (domeB) domeB.localPosition = domeRest;
            if (l18Blast) l18Blast.intensity = 0f; if (l18Lamp) l18Lamp.intensity = 0f;
        }

        /// <summary>The top of a button's dome (stage frame): the cup button is the one nearer to him, the bridge button the outer one.</summary>
        Vector3 DomeTop(bool cup) => new Vector3(consoleTop.x + (cup ? -NukeX : NukeX), consoleTop.y + DomeRise - (cup ? 0f : l18DomePress), consoleTop.z);

        // ------------------------------------------------------------------------------------------------ the set (built once, at the scene's first use)
        /// <summary>Every mesh node of a modelled prop under `parent`, each dressed by its own name (one material a piece, as the level 7 tub's
        /// pieces). Null = the model is not in the project: an error is logged, nothing is built in its place.</summary>
        Transform L18Model(string res, string name, Transform parent, System.Func<string, Material> mat, bool cast)
        {
            var pf = Resources.Load<GameObject>(res);
            if (!pf) { Debug.LogError("[EvilZod] level 18: the modelled prop Resources/" + res + " is missing: the scene plays without it (no stand-in is built)"); return null; }
            var go = Instantiate(pf, parent, false); go.name = name;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = pf.transform.localRotation; go.transform.localScale = pf.transform.localScale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.layer = Layer; var m = mat(r.gameObject.name); if (m) r.sharedMaterial = m;
                r.renderingLayerMask = LightMask; r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.Off;
            }
            go.layer = Layer;
            return go.transform;
        }

        static Transform L18Piece(Transform root, string name)
        {
            if (!root) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        void EnsureL18Set()
        {
            if (l18Root) return;
            l18Root = new GameObject("L18Window") { layer = Layer }.transform; l18Root.SetParent(transform, false);
            EnsureSteelMaps();
            var steel = wallPbrMat ? wallPbrMat : SteelPbr(mapsWall); if (!steel) steel = Lit(new Color32(40, 44, 50, 255), 0.35f, 0.55f);
            var body = consoleMat ? consoleMat : SteelPbr(mapsConsole); if (!body) body = Lit(new Color32(48, 52, 58, 255), 0.4f, 0.6f);
            var rib = Lit(new Color32(62, 67, 74, 255), 0.45f, 0.7f); var dark = Lit(new Color32(16, 18, 21, 255), 0.4f, 0.4f);
            var hazard = Lit(new Color32(214, 170, 38, 255), 0.35f, 0.1f); var rubber = Lit(new Color32(12, 12, 13, 255), 0.18f, 0f);
            var chrome = Lit(new Color32(150, 155, 162, 255), 0.6f, 0.9f);
            // ---- the window wall (modelled about the opening's sill line; its inner face on WinZ)
            var wall = L18Model(L18WallRes, "L18WindowWall", l18Root, n => n.EndsWith("_Wall") ? steel : n.EndsWith("_Frame") || n.EndsWith("_Slats") ? rib : n.EndsWith("_Hazard") ? hazard : dark, false);
            if (wall) wall.localPosition = new Vector3(0f, WinLift, WinZ);
            // ---- the lights
            l18Dawn = AddLight("L18Dawn", LightType.Spot, new Vector3(2.6f, 3.0f, 6.4f), new Vector3(0f, 1.6f, 0f), new Color(1f, 0.62f, 0.38f), DawnI, 14f, 46f, false, l18Root);
            l18Blast = AddLight("L18Blast", LightType.Point, new Vector3(-0.6f, 2.0f, 4.4f), new Vector3(-0.6f, 2.0f, 4.4f), new Color(1f, 0.5f, 0.18f), 0f, 12f, 50f, false, l18Root);
            // the dawn on the window's own steel (sill and jamb read as a window from outside) and a cool rim that lifts both robots off the dark room
            AddLight("L18Frame", LightType.Spot, new Vector3(3.4f, 2.4f, 5.6f), new Vector3(1.3f, 1.2f, 2.0f), new Color(1f, 0.7f, 0.46f), FrameI, 9f, 70f, false, l18Root);
            AddLight("L18RimEz", LightType.Spot, new Vector3(-1.5f, 3.4f, -2.0f), new Vector3(0.3f, 1.9f, 0.1f), new Color(0.55f, 0.72f, 1f), RimI, 9f, 46f, false, l18Root);
            AddLight("L18RimSk", LightType.Spot, new Vector3(2.6f, 2.6f, -2.2f), new Vector3(1.3f, 0.9f, -0.7f), new Color(0.55f, 0.72f, 1f), RimI * 0.6f, 7f, 40f, false, l18Root);
            // ---- the console: the pedestal reaches up to his hip, the housing with its two mushroom buttons sits on it
            consoleRoot = new GameObject("L18Console") { layer = Layer }.transform; consoleRoot.SetParent(l18Root, false);
            domeAMat = Glow(new Color32(176, 18, 14, 255), new Color(1f, 0.08f, 0.04f), 0.5f); if (domeAMat) domeAMat.SetFloat("_Smoothness", 0.78f);
            domeBMat = Glow(new Color32(176, 18, 14, 255), new Color(1f, 0.08f, 0.04f), 0.5f); if (domeBMat) domeBMat.SetFloat("_Smoothness", 0.78f);
            var white = Glow(new Color32(225, 228, 232, 255), new Color(0.9f, 0.92f, 1f), 1.1f); var plateBack = Lit(new Color32(22, 24, 28, 255), 0.3f, 0.2f);
            var con = L18Model(L18ConsoleRes, "L18ConsoleModel", consoleRoot, n =>
                n.EndsWith("_Housing") || n.EndsWith("_Pedestal") ? body : n.EndsWith("_DomeCup") ? (NukeNear ? domeBMat : domeAMat) : n.EndsWith("_DomeBridge") ? (NukeNear ? domeAMat : domeBMat) : n.EndsWith("_Collars") || n.EndsWith("_StripeY") ? hazard
                : n.EndsWith("_Bases") ? chrome : n.EndsWith("_PlateIcons") ? white : n.EndsWith("_PlateBack") ? plateBack : dark, true);
            if (con)
            {
                // the pieces that ride at the panel's height go under one node; the pedestal, its foot and the conduit stay on the floor
                consoleTopT = new GameObject("L18ConsoleTop") { layer = Layer }.transform; consoleTopT.SetParent(consoleRoot, false);
                consolePed = new GameObject("L18ConsolePedestal") { layer = Layer }.transform; consolePed.SetParent(consoleRoot, false);
                var kids = new List<Transform>(); foreach (Transform t in con) kids.Add(t);
                foreach (var t in kids)
                {
                    bool low = t.name.EndsWith("_Pedestal") || t.name.EndsWith("_Conduit");
                    if (t.name.EndsWith("_Foot")) continue;
                    t.SetParent(low ? consolePed : consoleTopT, true);
                    if (t.name.EndsWith("_PlateIcons")) { t.gameObject.SetActive(false); continue; }               // the old drawn icons: the two picture plates stand in their place (L18Plates)
                    if (t.name.EndsWith("_PlateBack")) { t.gameObject.SetActive(false); continue; }
                }
                domeB = L18Piece(consoleTopT, NukeNear ? "L18Con_DomeCup" : "L18Con_DomeBridge"); if (domeB) domeRest = domeB.localPosition;   // the dome that goes down: the wrong button
                L18Plates(plateBack);
            }
            l18Lamp = AddLight("L18ButtonLamp", LightType.Point, new Vector3(NukeX, ConsoleModelTop + 0.26f, 0.06f), new Vector3(NukeX, ConsoleModelTop + 0.26f, 0.06f), new Color(1f, 0.1f, 0.05f), 0f, 2.4f, 50f, false, consoleTopT ? consoleTopT : consoleRoot);
            l18Alarm = NewSource(46);
            // ---- the binoculars (the model's +Z looks where he looks)
            var glass = L18LensGlass(); L18LensKeep(glass);                                              // round 4: the blasts light the lenses (L18R4.cs)
            var gun = Lit(new Color32(30, 33, 38, 255), 0.5f, 0.75f);
            binoc = L18Model(L18BinocRes, "L18Binoculars", transform, n => n.EndsWith("_Glass") ? glass : n.EndsWith("_Rubber") ? rubber : n.EndsWith("_Wheel") ? chrome : gun, true);
            foreach (var t in l18Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        // ------------------------------------------------------------------------------------------------ framings
        static bool L18Known(string n) => n == "l18_coffee" || n == "win_two" || n == "l18_two" || n == "l18_calm" || n == "console" || n == "ez_binoc" || n == "sk_scream" || n == "sk_past" || n == "ez_low" || n == "low_two";

        /// <summary>The scene's own framings (and its first picture: `two` is the window shot here). False = the room's own framing of that name.</summary>
        bool L18Frame(string n, out Framing f)
        {
            f = default;
            if (!l18On) return false;
            switch (n)
            {
                case "two":
                case "win_two":
                    // through the window from outside: the steel frame round the picture, both robots inside it (the sill hides their shins)
                    f = new Framing { look = WinTwoLook, dist = WinTwoDist, fov = WinFov, elev = -1f, yaw = -2f, wide = true, subject = 0 }; return true;
                case "l18_two":
                    // both, through the window from outside but nearer than the first picture: the sill along the bottom and the window's left jamb
                    // (BS-01's side) in the picture's edge, so the place stays readable in every wide (critic fail 6); room over a raised fist.
                    // By maths at the wall's plane: lens L18TwoDist out, the picture there 1.8 m high from under the sill, the jamb 0.1-0.2 m in.
                    f = new Framing { look = L18TwoLook, dist = L18TwoDist, fov = WinFov, elev = -1f, yaw = -3f, wide = true, subject = 0 }; return true;
                case "l18_calm":
                    // the calm closing picture: a little wider, nearly level
                    f = new Framing { look = L18TwoLook + new Vector3(-0.04f, 0f, 0f), dist = L18TwoDist + 0.7f, fov = WinFov, elev = 0f, yaw = -2f, wide = true, subject = 0 }; return true;
                case "ez_binoc":
                    {   // THE KEY PICTURE: three quarters on him from BS-01's side of the lens, hat to chest: the two barrels, both hands on them, the
                        // coated glass with the river in it. It reads as a robot looking THROUGH binoculars, never as two eyes.
                        EzPoints(out _, out var bFace, out _, out _, out _);
                        f = new Framing { look = bFace + new Vector3(0f, -0.07f, 0f), dist = BinocDist, fov = BinocFov, elev = BinocElev, yaw = L18BinocYaw(), wide = false, subject = 1 }; return true;
                    }
                case "sk_scream":
                    // BS-01's scream and the press in ONE picture: a wide lens low inside the room, in front of both: the commander in the middle with
                    // the glasses at his eyes, his palm coming down on the wrong button on one side, the little one throwing himself at it from the other
                    f = new Framing { look = ScreamLook, dist = ScreamDist, fov = ScreamFov, elev = ScreamElev, yaw = ScreamYaw, wide = true, subject = 0 }; return true;
                case "low_two":
                    {   // the closing line's calm picture: low beside BS-01 (near, waist up), the commander towering behind him
                        SkPoints(out _, out var cEyes, out _, out _); EzPoints(out _, out _, out var cChest, out _, out _);
                        f = new Framing { look = Vector3.Lerp(cEyes, cChest, LowTwoShare) + new Vector3(0f, -0.05f, 0f), dist = LowTwoDist, fov = LowTwoFov, elev = LowTwoElev, yaw = LowTwoYaw, wide = true, subject = 0 }; return true;
                    }
                case "sk_past":
                    {   // BS-01 from his own eye level past the commander's leg (near, out of focus): the little one looks up at him
                        SkPoints(out _, out var pEyes, out _, out _);
                        f = new Framing { look = pEyes + new Vector3(0.24f, 0.04f, 0f), dist = PastDist, fov = 27f, elev = PastElev, yaw = PastYaw, wide = false, subject = 2 }; return true;
                    }
                case "ez_low":
                    {   // the low angle up at him over the window's sill and the console's edge (near, out of focus): the commander fills the picture
                        EzPoints(out _, out _, out var lChest, out _, out _);
                        f = new Framing { look = lChest + new Vector3(0f, 0.25f, 0f), dist = LowDist, fov = 30f, elev = LowElev, yaw = LowYaw, wide = false, subject = 1 }; return true;
                    }
                case "l18_coffee":
                    // "Now, where's my coffee": the room's own medium on him, a little wider and moved toward the stand: both whole signs beside him
                    f = ResolveHq("ez_med"); f.look += new Vector3(CoffeeShift, 0f, 0.08f); f.dist *= CoffeeWide; return true;
                case "console":
                    // the insert: over the panel from the lens side and his far side, looking down at the two domes and the plaques behind them; his
                    // arm comes in from the top. The lens stands well inside the wall's inner face (critic fail 14).
                    f = new Framing { look = consoleTop + new Vector3(-PlateOut * 0.5f, 0.30f, 0.10f) + new Vector3(Mathf.Cos(InsertYaw * Mathf.Deg2Rad), 0f, -Mathf.Sin(InsertYaw * Mathf.Deg2Rad)) * InsertLeft, dist = InsertDist * InsertBack, fov = InsertFov, elev = InsertElev, yaw = InsertYaw, wide = true, subject = 0 }; return true;   // the two domes AND both whole plates over them
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ beats
        bool L18EvilZodBeat(OzBlueBeat b)
        {
            if (!l18On) return false;
            switch (b.beat)
            {
                case "binoc_up": SetHand(true, HandMode.Binoc); if (b.a >= 2f) SetHand(false, HandMode.Binoc); return true;
                case "look_out": ezLook = LookMode.Out; return true;
                case "binoc_sweep": sweepAt = clock; sweepDur = Mathf.Max(0.6f, b.hold); sweepAmp = b.a > 0f ? b.a : 1f; Servo(); return true;
                case "fist_up_l": SetHand(false, HandMode.FistUp); ezChestGoal = Mathf.Max(ezChestGoal, 6f); Servo(); return true;
                case "pat_console":
                    SetHand(false, HandMode.PatConsole); patAt = clock; pressAt = clock + Mathf.Max(1.2f, b.a); patFrom = ezLPos + ezOffL; l18Pressed = false; ezChestGoal = 0f; Servo();
                    return true;
                case "lean_glass":
                    // never still: a slow lean toward the glass, the binoculars pressed closer (the palm stays on the pressed button)
                    leanAt = clock; leanDur = Mathf.Max(0.5f, b.hold); leanDeg = b.a > 0f ? b.a : 6f; Servo();
                    return true;
                case "binoc_windup":
                    // the anticipation: the glasses a little up and in, the shoulders rise; from this frame the glasses ride the right hand; the
                    // left palm leaves the button for the hip
                    windAt = clock; leanAt = -10f; ezLeanGoal = 0f; ezShoulderGoal = 7f; binocGrip = false; Servo();
                    return true;
                case "binoc_off":
                    tearAt = clock; tearFrom = ezRPos; tearStopped = false; windAt = -10f;
                    SetHand(true, HandMode.BinocLow); ezShoulderGoal = 0f; ezChestGoal = 8f; Servo();
                    Sfx("OpenZ/Audio/Sfx/sd_body_whoosh_a", -12f, 1.15f, "binoc_tear_whoosh", sfxB);
                    return true;
                case "point_window": SetHand(false, HandMode.PointOut); ezTwistGoal = -8f; Servo(); Sfx("OpenZ/Audio/Sfx/sd_body_whoosh_b", -16f, 1.0f, "point_window_whoosh", sfxB); return true;
                case "hand_l_hip": SetHand(false, HandMode.Hip); ezTwistGoal = 0f; Servo(); return true;
                case "binoc_sink": SetHand(true, HandMode.BinocSide); ezChestGoal = 0f; Servo(); return true;
                case "binoc_back": SetHand(true, HandMode.Binoc); ezChestGoal = 0f; ezLeanGoal = 0f; Servo(); return true;                 // the glasses back to his eyes in the right hand (they ride the hand: he looks out again)
                case "fist_l": SetHand(false, HandMode.FistCam); ezLeanGoal = 7f; Servo(); Sfx("OpenZ/Audio/Sfx/sd_body_whoosh_b", -18f, 1.05f, "fist_l_whoosh", sfxB); return true;   // the left fist up in front of his chest (the room's own fist pose, mirrored)
            }
            return false;
        }

        void L18SetBeat(OzBlueBeat b)
        {
            if (!l18On) return;
            switch (b.beat)
            {
                case "flash": flashAt = clock; flashK = b.a > 0f ? b.a : 1f; L18Flashes++; if (glowFrom < 0f) glowFrom = clock; break;         // the light only: the blast's sound is the full-screen stage's
                case "alarm_chirp":
                    chirpAt = clock; L18Chirps++;
                    var c = ClipOf("OpenZ/Audio/Sfx/sd_alarm_klaxon");
                    if (c && l18Alarm) { l18Alarm.Stop(); l18Alarm.clip = c; l18Alarm.pitch = 1.6f; chirpVol = Mathf.Clamp01(VoiceGain * Mathf.Pow(10f, -9f / 20f)); l18Alarm.volume = chirpVol; l18Alarm.Play(); OzSoundLog.Add("EvilZod", "alarm_chirp", c.name, "played", l18Alarm.volume); }
                    else OzSoundLog.Add("EvilZod", "alarm_chirp", "sd_alarm_klaxon", "missing clip");
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------ hands
        /// <summary>Where he looks with the binoculars: out of the window, far; the sweep carries the point across (about 23 degrees each way).</summary>
        Vector3 L18LookOut()
        {
            float x = 0f;
            if (sweepAt >= 0f) { float u = Mathf.Clamp01((clock - sweepAt) / sweepDur); x = Mathf.Sin(u * Mathf.PI * 2f) * 6f * sweepAmp * (1f - 0.35f * u); if (u >= 1f) sweepAt = -10f; }
            return transform.TransformPoint(new Vector3(L18EzMark.x + x, 0.86f * EzH, 14f));
        }

        /// <summary>The wind-up's share now (0..1): the glasses lift WindLift and come in by as much over WindSeconds.</summary>
        float WindK => windAt >= 0f ? Mathf.SmoothStep(0f, 1f, (clock - windAt) / Mathf.Max(0.04f, WindSeconds)) : 0f;

        /// <summary>The binoculars at his eyes as his head WILL stand this frame (the hands are placed before the head is turned): the point
        /// between the barrels and the way they look, from the look point and the head's own turn limits.</summary>
        void BinocAtEyes(out Vector3 mid, out Vector3 dir)
        {
            var rig = ez.Rig; float k = EzH / 2.3f;
            Vector3 hp = rig.Head.position, to = (ezLookInit ? ezLookPt : L18LookOut()) - hp;
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up); if (flat.sqrMagnitude < 1e-4f) flat = rig.Fwd;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(rig.Fwd, flat, Vector3.up), -45f, 45f);
            dir = Quaternion.AngleAxis(yaw, Vector3.up) * rig.Fwd;
            Vector3 hat = rig.HatTop ? rig.HatTop.position : hp + Vector3.up * 0.36f * k;
            float w = WindK;
            mid = Vector3.Lerp(hp, hat, 0.4f) + dir * ((0.08f + BinocAhead - binocPress - WindLift * w) * k) + Vector3.down * ((BinocDrop - WindLift * w) * k);
        }

        /// <summary>HandGoal's level-18 modes (the wrist's world target, elbow pole, finger take). False = not one of them.</summary>
        bool L18HandGoal(bool right, HandMode m, float H, float reach, ref Vector3 pole, ref string fingers, ref Vector3 palm, ref float smooth, out Vector3 goal)
        {
            goal = default;
            if (!l18On) return false;
            var rig = ez.Rig; float s = right ? 1f : -1f, k = H / 2.3f; Vector3 fwd = rig.Fwd, rgt = rig.Right, up = Vector3.up;
            Vector3 off = right ? ezOffR : ezOffL;
            switch (m)
            {
                case HandMode.Binoc:
                    {   // a grip on the barrel of his own side, the elbow out and down (a man holding field glasses)
                        BinocAtEyes(out var mid, out var dir);
                        Vector3 side = Vector3.Cross(up, dir).normalized;
                        fingers = "GripPump"; pole = rgt * s * 0.9f + Vector3.down * 0.6f + fwd * 0.1f; smooth = 0.14f;
                        goal = mid + side * (s * BinocGripHalf * k) + Vector3.down * (0.02f * k) - dir * (0.02f * k) - off; return true;
                    }
                case HandMode.BinocLow:
                    // at the front of his right hip, the elbow out (the tear's own way is written each frame by L18HandsOverride; this is where it rests)
                    fingers = "GripPump"; pole = rgt * s + up * 0.1f - fwd * 0.3f; smooth = 0.14f;
                    goal = Trunk(rig.Local(s * TearEnd.x * k, TearEnd.y * k, TearEnd.z * k)) - off; return true;
                case HandMode.BinocSide:
                    fingers = "GripPump"; pole = rgt * s * 0.8f + Vector3.down * 0.5f - fwd * 0.3f; smooth = 0.42f;
                    goal = Trunk(rig.Local(s * 0.45f * k, 1.10f * k, 0.12f * k)) - off; return true;
                case HandMode.PatConsole:
                    // the timed way itself is written each frame by L18HandsOverride; this is where it rests
                    fingers = "HandsOpen"; pole = rgt * s * 0.8f + up * 0.25f - fwd * 0.2f; palm = Vector3.down; smooth = 0.14f;
                    goal = PatPoint(clock) - off; return true;
                case HandMode.PointOut:
                    {   // the open hand thrown out at the window, a little to his own side of it, the arm not quite straight
                        Vector3 sh = (right ? rig.ArmR : rig.ArmL).position;
                        Vector3 to = (fwd * 0.72f + rgt * (s * 0.70f) - up * 0.10f).normalized;
                        fingers = "HandsOpen"; pole = up * 0.5f + rgt * s * 0.6f; palm = Vector3.down; smooth = 0.16f;
                        goal = sh + to * (reach * 0.90f); return true;
                    }
            }
            return false;
        }

        /// <summary>The palm's way over the console on the stage clock, counted back from the contact (world point of the palm).</summary>
        Vector3 PatPoint(float now)
        {
            Vector3 W(Vector3 local) => transform.TransformPoint(local);
            Vector3 edge = W(consoleTop + new Vector3(ButtonHalf + ButtonR + 0.10f, 0.07f, -0.17f));     // over the panel's near corner (his side)
            Vector3 edge2 = W(consoleTop + new Vector3(ButtonHalf - ButtonR * 0.4f, 0.07f, -0.17f));     // along its near edge, before the cup button
            Vector3 overCup = W(DomeTop(true) + new Vector3(0.03f, HoverHigh, -0.06f)), overBridge = W(DomeTop(false) + new Vector3(0f, HoverHigh * 0.8f + l18DomePress, 0f));
            Vector3 onBridge = W(DomeTop(false) + new Vector3(0f, 0.012f, 0f));
            float toPress = pressAt - now;
            if (toPress <= 0f) return onBridge;                                                         // flat on the pressed dome, and it stays there (no hover over the button: critic fail 12)
            if (toPress <= PatFallAt) { float u = 1f - toPress / PatFallAt; return Vector3.Lerp(overBridge, onBridge, u * u); }          // the fall: faster and faster
            if (toPress <= PatSlideAt) { float u = 1f - (toPress - PatFallAt) / (PatSlideAt - PatFallAt); u = u * u * (3f - 2f * u); return Vector3.Lerp(overCup, overBridge, u); }
            if (toPress <= PatHoverAt)
            {   // over the cup button, one small dip toward it: he nearly presses the right one
                float u = 1f - (toPress - PatSlideAt) / (PatHoverAt - PatSlideAt);
                return overCup + Vector3.down * (0.03f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI));
            }
            // before that: down from where the hand was to the panel's corner, two pats on the panel, then across to the cup button
            float since = now - patAt, free = Mathf.Max(0.3f, (pressAt - PatHoverAt) - patAt);
            float reachT = Mathf.Min(0.45f, free * 0.4f);
            if (since < reachT) { float u = since / reachT; u = u * u * (3f - 2f * u); return Vector3.Lerp(patFrom, edge, u); }
            float v = Mathf.Clamp01((since - reachT) / Mathf.Max(0.05f, free - reachT));
            if (v >= PatLeg) { float u = (v - PatLeg) / (1f - PatLeg); u = u * u * (3f - 2f * u); return Vector3.Lerp(edge2, overCup, u); }
            // along the panel's near edge (clear of both domes), the palm down onto the panel twice: a sharp contact at a quarter and at three
            // quarters of the leg, a round top between them (critic fail 13: the bounce was the other way round)
            float q = v / PatLeg;
            Vector3 p = Vector3.Lerp(edge, edge2, q * q * (3f - 2f * q));
            p.y = Mathf.Lerp(p.y, W(consoleTop).y + 0.012f, 1f - Mathf.Abs(Mathf.Cos(q * Mathf.PI * Mathf.Max(1, Pats))));
            return p;
        }
        /// <summary>The share of the free time (reach done, hover not begun) the two pats take; they land at a quarter and three quarters of it.</summary>
        const float PatLeg = 0.75f;

        /// <summary>The right wrist's way in the tear-down (world): an arc from where the hand was at the glasses, out in front of the chest,
        /// down and out to the right hip in TearSeconds (off the mark at once, arriving fast), a small overshoot along its own way and back.</summary>
        Vector3 TearPoint(float now)
        {
            var rig = ez.Rig; float k = EzH / 2.3f;
            Vector3 mid = Trunk(rig.Local(TearMid.x * k, TearMid.y * k, TearMid.z * k)) - ezOffR, end = Trunk(rig.Local(TearEnd.x * k, TearEnd.y * k, TearEnd.z * k)) - ezOffR;
            float u = Mathf.Clamp01((now - tearAt) / Mathf.Max(0.08f, TearSeconds)), e = Mathf.Pow(u, 1.7f);          // off the eyes slowly for two frames, then faster and faster into the hard stop
            Vector3 p = (1f - e) * (1f - e) * tearFrom + 2f * (1f - e) * e * mid + e * e * end;
            float late = now - tearAt - TearSeconds;
            if (late > 0f) p += (end - mid).normalized * (TearOver * k * Mathf.Exp(-late / 0.05f) * Mathf.Cos(late * 24f));   // the overshoot, gone in a fifth of a second
            return p;
        }

        /// <summary>TickEvilZodHands, before the arms are solved: the left palm rides its timed way exactly (no ease behind the clock), and so
        /// does the right hand in the tear-down.</summary>
        void L18HandsOverride()
        {
            if (!l18On) return;
            if (ezL == HandMode.PatConsole && patAt >= 0f) { ezLPos = PatPoint(clock) - ezOffL; ezLVel = Vector3.zero; }
            if (ezR == HandMode.BinocLow && tearAt >= 0f && clock - tearAt < TearSeconds + 0.30f) { ezRPos = TearPoint(clock); ezRVel = Vector3.zero; }
        }

        // ------------------------------------------------------------------------------------------------ frame
        void TickL18Window(float dt)
        {
            if (!l18On || ez == null || !ez.Ok) return;
            var rig = ez.Rig; float k = EzH / 2.3f;
            // ---- the slow lean toward the glass (never still through the blasts)
            if (leanAt >= 0f) { float u = Mathf.SmoothStep(0f, 1f, (clock - leanAt) / leanDur); ezLeanGoal = leanDeg * u; binocPress = 0.015f * u; }
            else if (dt > 0f) binocPress = Mathf.MoveTowards(binocPress, 0f, dt * 0.1f);
            // ---- the binoculars: at his eyes (the head's real pose of this frame) until the wind-up; from its first frame RIGID in the right hand
            Vector3 hf = rig.HeadFront ? rig.HeadFront.position - rig.Head.position : rig.Fwd; hf.y *= 0.5f;
            if (hf.sqrMagnitude < 1e-5f) hf = rig.Fwd; hf.Normalize();
            Vector3 hat = rig.HatTop ? rig.HatTop.position : rig.Head.position + Vector3.up * 0.36f * k;
            Vector3 eyes = Vector3.Lerp(rig.Head.position, hat, 0.4f) + hf * (0.08f * k);
            Vector3 upPos = eyes + hf * ((BinocAhead - binocPress) * k) + Vector3.down * (BinocDrop * k); Quaternion upRot = Quaternion.LookRotation(hf, Vector3.up);
            bool inHand = windAt >= 0f || tearAt >= 0f || ezR == HandMode.BinocLow || ezR == HandMode.BinocSide;
            if (binoc)
            {
                if (!inHand) { binocGrip = false; binoc.SetPositionAndRotation(upPos, upRot); }
                else
                {
                    if (!binocGrip)
                    {   // the grip's own offset, taken once from where the glasses are in the fist at this frame: hand pose x this = their pose from now on
                        binoc.SetPositionAndRotation(upPos, upRot);
                        gripPos = rig.HandR.InverseTransformPoint(upPos); gripRot = Quaternion.Inverse(rig.HandR.rotation) * upRot; binocGrip = true;
                    }
                    binoc.SetPositionAndRotation(rig.HandR.TransformPoint(gripPos), rig.HandR.rotation * gripRot);
                    L18GripSlip = Mathf.Max(L18GripSlip, Vector3.Distance(rig.HandR.InverseTransformPoint(binoc.position), gripPos));   // 0 by construction: counted all the same
                }
                binoc.localScale = Vector3.one * (rig.Scale * BinocSize);
            }
            // the hard stop of the tear: the glasses against his hip plate
            if (tearAt >= 0f && !tearStopped && clock >= tearAt + TearSeconds) { tearStopped = true; Sfx("OpenZ/Audio/Sfx/sd_tank_breech_clank_b", -15f, 1.35f, "binoc_hip_stop", sfxB); Kick(0.25f); }
            // ---- the console: the pats' taps, the contact, the dome, the lamp
            if (patAt >= 0f && dt > 0f)
            {
                float free = Mathf.Max(0.3f, (pressAt - PatHoverAt) - patAt), reachT = Mathf.Min(0.45f, free * 0.4f);
                for (int i = 0; i < Mathf.Max(1, Pats); i++)
                {   // the palm meets the panel in the middle of each pat of the pats' leg
                    float at = patAt + reachT + (free - reachT) * PatLeg * ((i + 0.5f) / Mathf.Max(1, Pats));
                    if (l18PrevClock > -90f && l18PrevClock < at && clock >= at) { L18Pats++; Sfx("OpenZ/Audio/Sfx/sd_seat_thud_b", -22f, 1.7f, "console_pat", sfxB); }
                }
                if (!l18Pressed && clock >= pressAt)
                {
                    l18Pressed = true; l18LampK = 1f; L18PressClock = clock;
                    L18PressMiss = Vector3.Distance(rig.HandPoint(false), transform.TransformPoint(DomeTop(false)));
                    Sfx("OpenZ/Zod/Sfx/zod_button_slam", -6f, 1.12f, "wrong_button_clunk");
                    Sfx("OpenZ/Audio/Sfx/sd_tank_breech_clank_a", -11f, 1.0f, "wrong_button_latch", sfxB);
                    Kick(0.5f);
                    Debug.Log($"[EvilZod] level 18: the palm on the wrong button at clock {clock:F2} (the beat asked {pressAt:F2}), {L18PressMiss * 100f:F1} cm from the dome's top");
                }
            }
            if (dt > 0f)
            {
                l18DomePress = Mathf.MoveTowards(l18DomePress, l18Pressed ? ButtonTravel : 0f, dt / 0.05f * ButtonTravel);   // it latches: down for the rest of the scene
                l18LampK = Mathf.MoveTowards(l18LampK, 0f, dt / 0.32f);                               // a flash on the contact; after it the lamp flashes with the alarm's chirps
            }
            if (domeB) domeB.localPosition = domeRest + Vector3.down * l18DomePress;
            // the alarm: a chirp of 0.2 s, faded out over 20 ms (a hard stop of the klaxon clicks)
            float since = chirpAt >= 0f ? clock - chirpAt : 99f, chirp = since < 0.22f ? 1f : 0f;
            if (l18Alarm && l18Alarm.isPlaying && chirpAt >= 0f)
            {
                if (since >= 0.22f) l18Alarm.Stop();
                else l18Alarm.volume = chirpVol * Mathf.Clamp01((0.22f - since) / 0.02f);
            }
            if (l18Lamp) l18Lamp.intensity = AlarmLampI * Mathf.Max(l18LampK * (0.6f + 0.4f * Mathf.Sin(clock * 30f)), chirp);
            if (domeBMat) domeBMat.SetVector("_EmissionColor", (Vector4)(new Color(1f, 0.08f, 0.04f) * (0.5f + 3.5f * Mathf.Max(l18LampK, chirp))));
            // ---- the blast light through the window: a hard flash that dies in about a second, a little flicker in it
            if (l18Blast)
            {
                float u = flashAt >= 0f ? clock - flashAt : 99f;
                float flash = u < 2.2f ? BlastI * flashK * Mathf.Clamp01(u / 0.07f) * Mathf.Exp(-u / 0.38f) * (0.8f + 0.2f * Mathf.PerlinNoise(clock * 23f, 4.1f)) : 0f;
                float glow = glowFrom >= 0f ? FireGlowI * Mathf.Clamp01((clock - glowFrom) / 0.8f) * (0.72f + 0.28f * Mathf.PerlinNoise(clock * 5.3f, 9.7f)) : 0f;   // the bridges burn outside
                l18Blast.intensity = Mathf.Max(flash, glow);
            }
            L18LensFlash();
            if (l18Dawn && dt > 0f) l18Dawn.intensity = DawnI * (0.96f + 0.04f * Mathf.PerlinNoise(clock * 0.7f, 2.3f));
            L18Dressing();
            if (dt > 0f) L18JointGate(rig);
            l18PrevClock = clock;
        }

        /// <summary>The joint gate of this scene, every frame of Play: each elbow's bend (180 = folded flat, 0 = straight: wanted 6..150), the
        /// head's turn from the chest about the up axis (45 at most, 1 degree of ease), each wrist's turn against its forearm since the
        /// scene's first frame (the hinge IK makes none: 8 at most). Counted: L18JointOver must stay 0.</summary>
        void L18JointGate(OzPoseRig rig)
        {
            if (!rig.Spine2 || !rig.Head) return;
            if (!jointInit) { wristRestR = Quaternion.Inverse(rig.ForeArmR.rotation) * rig.HandR.rotation; wristRestL = Quaternion.Inverse(rig.ForeArmL.rotation) * rig.HandL.rotation; jointInit = true; }
            L18JointFrames++; string bad = null;
            for (int s = 0; s < 2; s++)
            {
                Transform a = s == 0 ? rig.ArmR : rig.ArmL, f = s == 0 ? rig.ForeArmR : rig.ForeArmL, h = s == 0 ? rig.HandR : rig.HandL;
                float bend = 180f - Vector3.Angle(a.position - f.position, h.position - f.position);
                L18ElbowMin = Mathf.Min(L18ElbowMin, bend); L18ElbowMax = Mathf.Max(L18ElbowMax, bend);
                if (bend < 6f || bend > 150f) bad = (s == 0 ? "right" : "left") + " elbow " + bend.ToString("F0");
                float turn = Quaternion.Angle(s == 0 ? wristRestR : wristRestL, Quaternion.Inverse(f.rotation) * h.rotation);
                L18WristMax = Mathf.Max(L18WristMax, turn);
                if (turn > 8f) bad = (s == 0 ? "right" : "left") + " wrist " + turn.ToString("F0");
            }
            Vector3 hf = rig.HeadFront ? rig.HeadFront.position - rig.Head.position : rig.Head.forward; hf.y = 0f;
            Vector3 cf = rig.Fwd; cf.y = 0f;                                                              // the chest faces where the body faces (the twist is under 10 degrees on this scene)
            if (hf.sqrMagnitude > 1e-5f && cf.sqrMagnitude > 1e-5f)
            {
                float yaw = Mathf.Abs(Vector3.SignedAngle(Quaternion.AngleAxis(ezTwist, Vector3.up) * cf, hf, Vector3.up));
                L18HeadMax = Mathf.Max(L18HeadMax, yaw);
                if (yaw > 46f) bad = "head " + yaw.ToString("F0") + " from the chest";
            }
            if (bad != null) { L18JointOver++; L18JointWorst = bad + " at clock " + clock.ToString("F2"); }
        }

        /// <summary>One-line state of the level-18 scene for the Play check.</summary>
        public string L18Status() => !l18On ? "L18 window: off" :
            $"L18 window: binoculars {(binocGrip ? "rigid in his right hand (slip " + (L18GripSlip * 1000f).ToString("F1") + " mm)" : "at his eyes")}, hands R {ezR} / L {ezL}, press {(l18Pressed ? "at clock " + L18PressClock.ToString("F2") + " (" + (L18PressMiss * 100f).ToString("F1") + " cm off)" : pressAt >= 0f ? "due at " + pressAt.ToString("F2") : "not asked")}, "
            + $"pats {L18Pats}, chirps {L18Chirps}, flashes {L18Flashes}, camera {camName}, props wall {(L18Piece(l18Root, "L18WindowWall") ? "ok" : "MISSING")} / console {(consoleTopT ? "ok" : "MISSING")} / binoculars {(binoc ? "ok" : "MISSING")}, "
            + $"joint gate {L18JointFrames} frames, {L18JointOver} over a limit{(L18JointOver > 0 ? " (" + L18JointWorst + ")" : "")}: elbows {L18ElbowMin:F0}-{L18ElbowMax:F0} deg bent, head {L18HeadMax:F0} deg from the chest at most, wrists {L18WristMax:F1} deg turned at most";

        public const string L18LensRes = "OpenZ/L18Cut/L18_BinocLens", L18LensGlowRes = "OpenZ/L18Cut/L18_BinocLens_E";
        public const string L18PlateCoffeeRes = "OpenZ/L18Cut/L18_PlateCoffee", L18PlateNukeRes = "OpenZ/L18Cut/L18_PlateNuke";
        /// <summary>The wrong button is the one NEARER to him (his palm falls on it without a stretch); the coffee button is the outer one.</summary>
        public static bool NukeNear = true;
        static float NukeX => NukeNear ? ButtonHalf : -ButtonHalf;
        /// <summary>The picture plates behind the buttons: size (m), lean back (deg), their own glow; the lenses' glow and smoothness (never a mirror: a mirror washes the reflection out to white).</summary>
        public static float PlateSize = 0.32f, PlateLean = 14f, PlateGlow = 0.62f, LensGlow = 0.75f, LensSmooth = 0.42f;
        /// <summary>The plates stand FREE on two thin posts each at the panel's FRONT edge, each over its own button: their lower edge this far
        /// over the panel (m; over the domes, the collars and a hovering palm from every lens of the scene), this far behind the domes' line
        /// (below 0 = in front of it: no hand, no arm and nothing of the stand can be between a lens and a symbol), this far out from the button.</summary>
        public static float PlateLift = 0.35f, PlateBehind = -0.20f, PlateOut = 0f;
        /// <summary>The scene's lens for a real depth of field (the room's own is a far blur only): focal length (mm), stop. What stands near the lens goes soft.</summary>
        public static float L18Focal = 85f, L18Aperture = 2.4f; public static bool L18Bokeh = true;
        /// <summary>The key picture on the binoculars: distance (m), field of view, height angle, heading off his own axis (deg, 25 to 35 wanted).</summary>
        public static float BinocDist = 3.0f, BinocFov = 20f, BinocElev = -5f, BinocYaw = 31f;
        /// <summary>BS-01's scream past the button, BS-01 past the commander's leg, the low angle on the commander: distance, height angle, heading.</summary>
        public static float ScreamDist = 1.62f, ScreamFov = 74f, ScreamElev = -4f, ScreamYaw = 4f, PastDist = 2.8f, PastElev = 3f, PastYaw = -37f, LowDist = 2.7f, LowElev = -14f, LowYaw = -20f;
        public static Vector3 ScreamLook = new Vector3(0.21f, 1.27f, 0.05f); public static float LowTwoDist = 2.3f, LowTwoElev = -12f, LowTwoYaw = 38f, LowTwoFov = 60f, LowTwoShare = 0.35f;
        /// <summary>The fires on the bridges light the room through the window from the first blast on (a warm, uneasy glow).</summary>
        public static float FireGlowI = 16f; float glowFrom = -10f;
        /// <summary>BS-01's lunge at the hand: where he ends from his mark (m, stage frame), the seconds it takes, his lean, the turn toward the console.</summary>
        public static Vector3 LungeTo = new Vector3(-0.14f, 0f, 0.80f); public static float LungeSeconds = 0.26f, LungeLean = 15f, LungeYaw = -48f, LungeBack = 0.55f;
        /// <summary>
        /// throws both mittens UP at the hand on the button: their reach from his chest (m at his own size) and their lift.</summary>
        public static float LungeReach = 0.30f, LungeLift = 0.24f;
        /// <summary>The coffee line's medium (l18_coffee): the picture's shift toward the stand (m, stage x) and how much wider than the room's ez_med.</summary>
        public static float CoffeeShift = -0.24f, CoffeeWide = 1.14f;
        float lungeAt = -10f, lungeEnd = -10f, headNoAt = -10f; string l18CamWas; Transform l18Dress; Material cableMat;
        readonly List<Mesh> l18DressMeshes = new List<Mesh>();

        Material L18LensGlass()
        {   // dark coated glass with the river and the three bridges in it (a real frame of this level, mirrored and bulged): the model's own glass
            // UVs take both lenses from one picture
            var lens = Resources.Load<Texture2D>(L18LensRes); var glow = Resources.Load<Texture2D>(L18LensGlowRes);
            var m = Glow(new Color32(255, 255, 255, 255), Color.white, lens ? LensGlow : 0.3f); if (!m) return null;
            if (!lens) { Debug.LogError("[EvilZod] level 18: the binoculars' lens picture Resources/" + L18LensRes + " is missing: plain dark glass"); m.SetColor("_BaseColor", new Color32(14, 30, 58, 255)); m.SetFloat("_Smoothness", 0.5f); return m; }
            var tile = new Vector2(5.2419f, 10.4839f); var off = new Vector2(0.5f, 0.5f);
            m.SetTexture("_BaseMap", lens); m.SetTextureScale("_BaseMap", tile); m.SetTextureOffset("_BaseMap", off);
            m.SetTexture("_EmissionMap", glow ? glow : lens); m.SetTextureScale("_EmissionMap", tile); m.SetTextureOffset("_EmissionMap", off);
            m.SetFloat("_Smoothness", LensSmooth); m.SetFloat("_Metallic", 0f);
            return m;
        }

        static Mesh l18Quad;
        /// <summary>A unit quad in the XY plane that faces +Z (the lens side), UV 0..1.</summary>
        static Mesh L18QuadMesh()
        {
            if (l18Quad) return l18Quad;
            l18Quad = new Mesh { name = "L18Plate" };
            l18Quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            l18Quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            l18Quad.triangles = new[] { 0, 1, 2, 1, 3, 2 }; l18Quad.RecalculateNormals(); l18Quad.RecalculateBounds();
            return l18Quad;
        }

        /// <summary>The two picture plates behind the buttons: a white COFFEE CUP with steam behind the coffee button, the black-on-yellow radiation
        /// sign with hazard stripes behind the wrong one. Big enough to read at the window's real size; the two red domes stay alike.</summary>
        void L18Plates(Material back)
        {
            if (!consoleTopT) return;
            if (SignsOnBoard) { L18BoardSigns(back); return; }
            Material steel = null;
            for (int i = 0; i < 2; i++)
            {
                bool nuke = i == 0; string res = nuke ? L18PlateNukeRes : L18PlateCoffeeRes;
                var tex = Resources.Load<Texture2D>(res); var glow = Resources.Load<Texture2D>(res + "_E");
                if (!tex) { Debug.LogError("[EvilZod] level 18: the button plate Resources/" + res + " is missing"); continue; }
                var m = Glow(new Color32(255, 255, 255, 255), Color.white, PlateGlow); if (!m) continue;
                m.SetTexture("_BaseMap", tex); m.SetTexture("_EmissionMap", glow ? glow : tex); m.SetFloat("_Smoothness", 0.3f); m.SetFloat("_Metallic", 0.1f);
                var go = new GameObject(nuke ? "L18PlateNuke" : "L18PlateCoffee") { layer = Layer }; go.transform.SetParent(consoleTopT, false);
                if (!steel) steel = Lit(new Color32(128, 134, 142, 255), 0.45f, 0.5f);
                float px = (nuke ? NukeX : -NukeX) - PlateOut, leanC = Mathf.Cos(PlateLean * Mathf.Deg2Rad), leanS = Mathf.Sin(PlateLean * Mathf.Deg2Rad);
                go.transform.localPosition = new Vector3(px, ConsoleModelTop + PlateLift + PlateSize * 0.5f * leanC, -PlateBehind - PlateSize * 0.5f * leanS);
                // two thin steel posts carry it from the panel (it stands free: nothing of the stand is in front of the picture)
                for (int q = -1; q <= 1; q += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "L18PlatePost"; post.layer = Layer; var pc = post.GetComponent<Collider>(); if (pc) Destroy(pc);
                    post.transform.SetParent(consoleTopT, false); float ph = PlateLift + 0.012f;
                    post.transform.localPosition = new Vector3(px + q * (PlateSize * 0.5f - 0.012f), ConsoleModelTop + ph * 0.5f - 0.01f, -PlateBehind - 0.02f); post.transform.localScale = new Vector3(0.022f, ph, 0.022f);
                    var pr = post.GetComponent<MeshRenderer>(); pr.sharedMaterial = steel; pr.renderingLayerMask = LightMask; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.lightProbeUsage = LightProbeUsage.Off;
                    l18Plates.Add(post);
                }
                go.transform.localRotation = Quaternion.Euler(-PlateLean, 0f, 0f); go.transform.localScale = new Vector3(PlateSize, PlateSize, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = L18QuadMesh(); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
                r.renderingLayerMask = LightMask; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off;
                l18Plates.Add(go);
            }
        }

        // ------------------------------------------------------------------------------------------------ the scene's lens
        void L18DofOn()
        {
            if (dof == null || !L18Bokeh) return;
            dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh); dof.focalLength.Override(L18Focal); dof.aperture.Override(L18Aperture); dof.focusDistance.Override(5f);
            dof.bladeCount.Override(7); dof.bladeCurvature.Override(0.9f);
        }
        void L18DofOff() { if (dof != null && dof.mode.value == UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh) dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian); }
        /// <summary>PlaceCamera, each frame of this scene: the focus rides the subject (the point the lens looks at).</summary>
        void L18Dof()
        {
            if (dof == null) return;
            if (!L18Bokeh) { dof.gaussianStart.value = camDistS + 1.2f; dof.gaussianEnd.value = camDistS + 7.2f; return; }
            dof.focusDistance.value = Mathf.Max(0.4f, Vector3.Distance(cam.transform.position, transform.TransformPoint(camLookS)));
            dof.aperture.value = camName == "console" || camName == "sk_scream" || camName == "low_two" ? 11f : L18Aperture;   // the insert and the wide pictures: everything sharp
        }

        // ------------------------------------------------------------------------------------------------ foreground dressing
        static Mesh L18Tube(Vector3[] p, float r, int sides = 7)
        {
            int n = p.Length; var v = new Vector3[n * sides]; var tr = new int[(n - 1) * sides * 6];
            for (int i = 0; i < n; i++)
            {
                Vector3 d = (p[Mathf.Min(n - 1, i + 1)] - p[Mathf.Max(0, i - 1)]).normalized, a = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized, b = Vector3.Cross(d, a);
                for (int s = 0; s < sides; s++) { float an = s * Mathf.PI * 2f / sides; v[i * sides + s] = p[i] + (a * Mathf.Cos(an) + b * Mathf.Sin(an)) * r; }
            }
            int k = 0;
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = i * sides + s, a1 = i * sides + (s + 1) % sides, b0 = a0 + sides, b1 = a1 + sides;
                    tr[k++] = a0; tr[k++] = b0; tr[k++] = a1; tr[k++] = a1; tr[k++] = b0; tr[k++] = b1;
                }
            var m = new Mesh { name = "L18Cable" }; m.vertices = v; m.triangles = tr; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A cable hanging between two points (the lens's own frame at the cut: x right, y up, z ahead, metres), sagging `sag` m.</summary>
        void L18Cable(Vector3 camPos, Quaternion camRot, Vector3 a, Vector3 b, float sag, float r)
        {
            const int n = 14; var p = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) { float u = i / (float)n; p[i] = transform.InverseTransformPoint(camPos + camRot * (Vector3.Lerp(a, b, u) + Vector3.down * (sag * 4f * u * (1f - u)))); }
            var mesh = L18Tube(p, r); l18DressMeshes.Add(mesh);
            var go = new GameObject("L18Cable") { layer = Layer }; go.transform.SetParent(l18Dress, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = cableMat;
            mr.renderingLayerMask = LightMask; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off;
        }

        /// <summary>
        /// Cables of the HQ that sag across a corner of the picture, a hand's width from the
        /// lens, soft in the scene's depth of field. The pictures that have their own near thing (the commander's arm, his leg, the sill and
        /// the console's edge) and the plain ones get none. Built from the picture's own lens at the cut; they stand still in the room.</summary>
        void L18Dressing()
        {
            if (camName == l18CamWas) return;
            l18CamWas = camName;
            if (l18Dress) Destroy(l18Dress.gameObject);
            foreach (var m in l18DressMeshes) if (m) Destroy(m);
            l18DressMeshes.Clear();
            l18Dress = new GameObject("L18Dress") { layer = Layer }.transform; l18Dress.SetParent(l18Root, false);
            if (!cableMat) cableMat = Lit(new Color32(18, 19, 22, 255), 0.35f, 0.2f);
            var f = Resolve(camName);
            float el = f.elev * Mathf.Deg2Rad, yw = f.yaw * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(yw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yw) * Mathf.Cos(el));
            Vector3 posL = f.look + dir * f.dist; if (posL.y < 0.28f) posL.y = 0.28f;
            Vector3 pos = transform.TransformPoint(posL); Quaternion rot = Quaternion.LookRotation(transform.TransformPoint(f.look) - pos, transform.up);
            float h = Mathf.Tan(f.fov * 0.5f * Mathf.Deg2Rad), w = h * FeedAspect;                          // the picture's half height / width at 1 m
            switch (camName)
            {
                case "two":
                case "win_two":
                case "l18_two":
                case "l18_calm":
                    // across the upper left corner and along the bottom right
                    L18Cable(pos, rot, new Vector3(-w * 1.5f, h * 0.30f, 1.15f), new Vector3(-w * 0.10f, h * 1.6f, 1.3f), 0.05f, 0.030f);
                    L18Cable(pos, rot, new Vector3(-w * 1.6f, h * 0.05f, 1.35f), new Vector3(-w * 0.45f, h * 1.6f, 1.5f), 0.07f, 0.014f);
                    L18Cable(pos, rot, new Vector3(w * 0.25f, -h * 1.5f, 1.2f), new Vector3(w * 1.6f, -h * 0.55f, 1.3f), 0.04f, 0.026f);
                    break;
                case "ez_binoc":
                    // down the left edge (the window's side of the lens) and a loop over the upper right corner
                    L18Cable(pos, rot, new Vector3(-w * 0.80f, h * 1.6f, 0.95f), new Vector3(-w * 1.02f, -h * 1.6f, 1.0f), 0.01f, 0.028f);
                    L18Cable(pos, rot, new Vector3(w * 0.35f, h * 1.6f, 1.25f), new Vector3(w * 1.6f, h * 0.45f, 1.3f), 0.06f, 0.016f);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------ BS-01: the lunge, one head shake
        /// <summary>The sidekick's level-18 beats: lunge (he throws himself at the hand on the button: hold = seconds until he lets go), head_no
        /// (ONE readable shake of the head), look_console. False = not one of them.</summary>
        bool L18SidekickBeat(OzBlueBeat b)
        {
            if (!l18On) return false;
            switch (b.beat)
            {
                case "lunge":
                    lungeAt = clock; lungeEnd = clock + (b.hold > 0f ? b.hold : 1.5f); skYawGoal = LungeYaw; skLeanGoal = LungeLean; skLeanUntil = lungeEnd; skMood = SkMood.Scared;
                    SetSkHand(SkHand.Present, b.hold > 0f ? b.hold : 1.5f); SkServo(); Sfx("OpenZ/Audio/Sfx/sd_body_whoosh_a", -15f, 1.3f, "sk_lunge_whoosh", sfxB);
                    return true;
                case "lunge_back": lungeEnd = Mathf.Min(lungeEnd, clock); skYawGoal = 0f; return true;
                case "head_no": headNoAt = clock; return true;
            }
            return false;
        }

        /// <summary>The lunge's share now (0 = on his mark, 1 = at its end): out in LungeSeconds, back over LungeBack after it is let go.</summary>
        float LungeK => lungeAt < 0f ? 0f : clock < lungeEnd ? Mathf.SmoothStep(0f, 1f, (clock - lungeAt) / Mathf.Max(0.05f, LungeSeconds)) : 1f - Mathf.SmoothStep(0f, 1f, (clock - lungeEnd) / Mathf.Max(0.05f, LungeBack));
        /// <summary>TickSidekick: his drawn place off his mark while he lunges (stage frame, taken off again each frame like the hop).</summary>
        Vector3 L18SkLunge() => l18On ? LungeTo * LungeK : Vector3.zero;
        /// <summary>TickSidekickHead: the head's extra turn: ONE shake (left, right, back to the middle) in 0.7 s, eased in and out.</summary>
        float L18SkHeadNo()
        {
            if (!l18On || headNoAt < 0f) return 0f;
            float u = (clock - headNoAt) / 0.7f; if (u >= 1f) { headNoAt = -10f; return 0f; }
            return 24f * Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Sin(u * Mathf.PI);
        }
        /// <summary>TickSidekickHead: where he looks while he lunges: at the hand on the button.</summary>
        Vector3 L18SkLookAt(Vector3 other) => l18On && lungeAt >= 0f && LungeK > 0.05f ? transform.TransformPoint(DomeTop(false)) : other;
        /// <summary>TickSidekickHands: both mittens thrown out at the button while he lunges (inside his reach: the room's own arm solve clamps it).</summary>
        void L18SkHands(OzPoseRig rig, float k, ref Vector3 wantR, ref Vector3 wantL, ref Vector3 poleR, ref Vector3 poleL, ref float smooth)
        {
            if (!l18On || lungeAt < 0f || LungeK <= 0.05f) return;
            Vector3 chest = rig.Local(0f, 0.62f * k, 0f), to = (transform.TransformPoint(DomeTop(false)) - chest).normalized, side = Vector3.Cross(Vector3.up, to).normalized;
            wantR = chest + to * (LungeReach * k) + side * (0.13f * k) + Vector3.up * (LungeLift * k); wantL = chest + to * (LungeReach * k) - side * (0.13f * k) + Vector3.up * (LungeLift * k);
            poleR = side + Vector3.down * 0.6f; poleL = -side + Vector3.down * 0.6f; smooth = 0.1f;
        }
    }
}
