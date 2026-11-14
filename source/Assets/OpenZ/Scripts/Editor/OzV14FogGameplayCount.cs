// OpenZ (GPL-3.0) - version 1.4 kit fog_gameplay_only: the fog gate's counted rows without Play (View/OzFowGate.cs). Plain C#: the menu
// OpenZ/V1.4/Fog Gameplay Only: Gate Count (headless) runs it in the editor (Editor/OzV14FogGameplayCheck.cs), the kit's off-Unity harness
// runs this same file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 kit fog_gameplay_only, counted without Play:
    /// <list type="bullet">
    /// <item>T rows: every state the scene studies found (gameplay and the moments that keep the fog, every takeover with a hard cut, every one
    /// that blends in), each as the inputs the game gives the gate then, against the expected answer (open, closed by a cut, closed with the
    /// fade) and its reason. T0: every reason of the gate before this kit closes the new one too.</item>
    /// <item>F rows: the fade (smootherstep table, 0.4 s at 60, 30 and 144 fps, a cut, the largest step a frame, a hitch counted as 0.05 s, a
    /// bad time step), Covers and the units held off together whenever the gate is open (from the first frame of play, whatever the
    /// fade).</item>
    /// <item>Q rows: whole scenes frame by frame (the level-start briefing and its hand-back, a level start without one, the blue briefing's
    /// last frame, a mid-match hard cut, a scene begun after the fog's Update, the ending's blend, the pause menu, the idle barb, the demo,
    /// the testing panel, hitches, the red and the blue briefing's window folding on the live latch), with the game's own order of reads
    /// (Update, then LateUpdate and the camera).</item>
    /// <item>S rows (given the sources): the gate's reader reads none of the gameplay states, the off path returns before the gate (rule 5),
    /// the reveal map runs before and outside it, every output reads it, the live partial only runs the counted clock, the old linear fade and
    /// the strength-based hiding are gone, the switch is reset to its constant.</item>
    /// <item>N rows (given the project's folder): the build's scenes without a ZGame (the main menu with its flybys and level cards, the
    /// intro) can never have the fog; the Unit editor's playground is refused by WhyOff.</item>
    /// </list>
    /// The Q rows run <see cref="OzFowGateClock"/> itself, the class the live gate (OzFogOfWar.Gate.cs GateStep / GateRecheck / GateCamera)
    /// runs; S10 checks that the partial does nothing else with it. The Play check (Editor/OzV14FogGameplayCheck.cs) measures the game.
    /// </summary>
    public static class OzV14FogGameplayCount
    {
        public delegate void RowFn(string id, bool ok, string text);

        /// <summary>0 open (the player plays), 1 closed with a hard cut, 2 closed with the fade.</summary>
        public const int Open = 0, Cut = 1, Fade = 2;
        static readonly string[] KindNames = { "open", "closed, hard cut", "closed, 0.4 s fade" };

        public sealed class State
        {
            public string Id, Name; public OzFowGateInputs In; public int Expect; public string Why;
            public State(string id, string name, OzFowGateInputs s, int expect, string why) { Id = id; Name = name; In = s; Expect = expect; Why = why; }
        }

        static OzFowGateInputs I(bool briefing = false, bool blue = false, bool fly = false, bool hold = false, bool epic = false, bool capture = false,
                                 bool noRts = false, bool rtsOff = false, bool camOff = false, bool screen = false, bool simWaits = false,
                                 bool decided = false, bool demo = false, bool corner = false, bool sceneWin = false) =>
            new OzFowGateInputs
            {
                Briefing = briefing, BlueBriefing = blue, Flyover = fly, EndingHold = hold, EpicFlyby = epic, CaptureCine = capture, NoRtsCamera = noRts,
                RtsCameraOff = rtsOff, CameraOff = camOff, ScreenTaken = screen, SimWaits = simWaits, MatchDecided = decided, Demo = demo, CornerWindow = corner,
                SceneWindow = sceneWin
            };

        /// <summary>Every state found (the two scene studies, 5 Nov, and fix rounds 1 and 2), as the game's flags stand then. The rows of
        /// Commander Zod's window over the running match expect what the switch says at its constant (<see cref="OzFowGate.CornerWindowKeepsFogDefault"/>,
        /// true since fix round 2: gameplay); a scene's window is a scene with either value.</summary>
        public static List<State> States()
        {
            bool keep = OzFowGate.CornerWindowKeepsFogDefault;
            int tx = keep ? Open : Fade; string txWhy = keep ? null : "a transmission";
            return new List<State>
            {
                // gameplay: the fog stays (the gate reads none of these; S1 proves it)
                new State("T01", "gameplay: the normal RTS camera the player's, the match running", I(), Open, null),
                new State("T02", "the pause menu over the match (game.paused, Time.timeScale 0, the RTS camera stays on)", I(), Open, null),
                new State("T03", "the in-match OPTIONS (inside the pause menu)", I(), Open, null),
                new State("T04", "game speed 0.25 x to 4 x", I(), Open, null),
                new State("T05", "camera shakes (RtsCamera.Shake, OpenZFxShake, the shuttle's big crash)", I(), Open, null),
                new State("T06", "zoom (the wheel, ZoomTo, the zoom limits)", I(), Open, null),
                new State("T07", "follow, free look, the middle-mouse turn, edge scroll, right-drag, the player's fly-tos (minimap, toasts, Space, groups)", I(), Open, null),
                new State("T08", "the minimap, the testing panel (Alt+P), OzFpsBench holding the camera", I(), Open, null),
                // Commander Zod's window over the running match: the camera, the screen and the controls stay the player's (the switch, fix round 2: gameplay)
                new State("T09", "Commander Zod's idle barb (ZodBriefing.cs:617, 60 s without a key, a click or the wheel) or another line in his in-game window over the running match", I(corner: true), tx, txWhy),
                // takeovers with a hard cut (the RTS camera switched off, the sim waiting)
                new State("T10", "red level-start briefing: the flyover (sim waits, RTS camera off)", I(briefing: true, fly: true, rtsOff: true, simWaits: true), Cut, "the briefing"),
                new State("T11", "red briefing, Handing: the ease to the home pose (RTS camera still off)", I(briefing: true, fly: true, rtsOff: true, simWaits: true), Cut, "the briefing"),
                new State("T12", "red briefing, after fly.End and the sim, before phase Play", I(briefing: true), Cut, "the briefing"),
                new State("T13", "blue level-start briefing: the flyover", I(blue: true, fly: true, rtsOff: true, simWaits: true), Cut, "the blue briefing"),
                new State("T14", "blue briefing, the frame after fly.End before phase Play (the old gate was open there)", I(blue: true), Cut, "the blue briefing"),
                new State("T15", "a mid-match flyover: ZodCinePreview.Play (the HUD stays up)", I(fly: true, rtsOff: true, simWaits: true), Cut, "a flyover"),
                new State("T16", "a mid-match briefing: ZodBriefing.DebugBriefing / OzBlueCameo.DebugBriefing", I(briefing: true, fly: true, rtsOff: true, simWaits: true), Cut, "the briefing"),
                new State("T17", "the L16 lounger / L07 hot tub ending scene (a hard cut 1.25 s after the impact)", I(hold: true, rtsOff: true, decided: true), Cut, "the ending"),
                new State("T18", "capture cameras: -cinematic, -showcase, F9, Orbit / Chase / Flyover / Crane, the flyby and card takes, OzStillCam", I(capture: true, rtsOff: true), Cut, "a capture camera"),
                new State("T19", "the editor's menu-flyby recorder (OzEpicFlyby)", I(epic: true), Cut, "a flyby recording"),
                new State("T20", "the gate battery's free camera (tests: the RTS camera off)", I(rtsOff: true), Cut, "the game holds the camera (the RTS camera is off)"),
                new State("T21", "a scene camera on the whole screen (OzEvilStage.SetLive(on, onScreen: true): the main camera masked to nothing)", I(screen: true), Cut, "a scene camera has the screen"),
                new State("T22", "the player's camera switched off", I(camOff: true), Cut, "the game holds the camera (the player's camera is off)"),
                new State("T23", "a level with no RTS camera", I(noRts: true), Cut, "the game holds the camera (no RTS camera)"),
                new State("T24", "the game waits for a scene (ZGame switched off, nothing else known)", I(simWaits: true), Cut, "the game waits for a scene"),
                // moments that blend in (the RTS camera stays on): the 0.4 s fade
                new State("T25", "the ending: the fort falls, the glide to it (RTS camera on, input locked), the bomber, the death wave, the orbit", I(decided: true), Fade, "the match is decided (ending)"),
                new State("T26", "the end screen, the victory / defeat line, the cheers, the win soda (W.GameOver)", I(decided: true, corner: true), Fade, "the match is decided (ending)"),
                new State("T27", "the blue endings and the blue nuke scene (a blue scene's window after the match is decided)", I(decided: true, sceneWin: true), Fade, "the match is decided (ending)"),
                new State("T28", "game over with no fort down (one side lost every unit)", I(decided: true), Fade, "the match is decided (ending)"),
                new State("T29", "attract / demo: the computer plays the player's side (CaptureKit F8)", I(demo: true), Fade, "the computer plays the player's side (demo)"),
                // fix rounds 1 and 2: the dev paths that run a window over a running match, and the briefings' windows folding
                new State("T32", "dev: the blue ending or nuke scene in its window over a running match (OzBlueCameo.DebugEnding / DebugNuke, OzBlueLab's frames)", I(sceneWin: true), Fade, "a scene's window"),
                new State("T33", "dev: a victory, defeat or any line of Zod's over a running match (ZodBriefing.DebugLine / DebugPunchline / DebugLineAs / DebugNuke)", I(corner: true), tx, txWhy),
                new State("T34", "the red briefing's comms window still folding when phase Play comes (the latch OzFowWindowTail: the window the briefing had)", I(corner: true, sceneWin: true), Fade, "a scene's window"),
                new State("T35", "the blue briefing's comms window still folding after phase Play (show.done at the fold's start, OzBlueCameo.cs:509-514; the window Visible until folded)", I(sceneWin: true), Fade, "a scene's window"),
                new State("T36", "the pause menu opened during Commander Zod's idle barb (the barb waits, its window stays up)", I(corner: true), tx, txWhy),
            };
        }

        /// <summary>The gate before this kit (OzFogOfWar.GateWhyBefore), as inputs: each of its reasons must close the new gate too.</summary>
        static readonly (string why, OzFowGateInputs s)[] OldReasons =
        {
            ("the briefing", I(briefing: true)), ("a flyover", I(fly: true)), ("the ending", I(hold: true)), ("the match is decided (ending)", I(decided: true)),
            ("a flyby recording", I(epic: true)), ("a capture camera", I(capture: true)),
        };

        /// <summary>The words the gate's reader (OzFogOfWar.Gate.cs GateInputsNow) must not contain: the gameplay states that keep the fog.</summary>
        public static readonly string[] GameplayWords = { "timeScale", "paused", "Pause", "Shake", "follow", "ZoomTo", "FreeTurn", "OzFreeCam", "FlyTo",
                                                          "OzCheatSpawner", "OpenZHud", "Minimap", "Speed", "InputLocked", "Modal" };

        static string Kind(int k) => KindNames[k];
        static string F3(double v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------------------------------------ the live gate's clock, framed
        /// <summary>OzFogOfWar.Gate.cs's frame order: <see cref="Update"/> (GateStep), <see cref="Recheck"/> (LateUpdate and the player's
        /// camera), <see cref="Drawn"/> (BeginCamera queues the pass), all on the live <see cref="OzFowGateClock"/>. <see cref="strength"/> is
        /// the fog's own strength field (GateAfter copies the clock's into it; the testing panel and the off path put it to 0).</summary>
        public sealed class Model
        {
            public readonly OzFowGateClock C = new OzFowGateClock();
            public float strength;
            public void Update(OzFowGateInputs s, float dt, int f) { string w = OzFowGate.Decide(s, out bool c); C.Update(w, c, dt, f, true, strength); strength = C.Strength; }
            public void Recheck(OzFowGateInputs s) { string w = OzFowGate.Decide(s, out bool c); C.Recheck(w, c, true); strength = C.Strength; }
            public bool Drawn => C.Draws;
        }

        sealed class Frame { public int f; public float strength; public bool drawn, covers, hold, open; public string why; }

        /// <summary>Runs a timeline: inputs(frame) before Update, late(frame) (or null) after it (LateUpdate / the camera); dt(frame) the frame's time step.</summary>
        static List<Frame> Play(int frames, Func<int, float> dt, Func<int, OzFowGateInputs> inputs, Func<int, OzFowGateInputs?> late = null, Action<Model, int> before = null)
        {
            var m = new Model(); var list = new List<Frame>(frames);
            for (int f = 0; f < frames; f++)
            {
                before?.Invoke(m, f);
                m.Update(inputs(f), dt(f), f);
                var l = late?.Invoke(f); if (l.HasValue) m.Recheck(l.Value);
                list.Add(new Frame { f = f, strength = m.strength, drawn = m.Drawn, covers = m.C.Covers, hold = m.C.HoldOff, open = m.C.Open, why = m.C.Why });
            }
            return list;
        }
        static List<Frame> Play(int frames, float dt, Func<int, OzFowGateInputs> inputs, Func<int, OzFowGateInputs?> late = null, Action<Model, int> before = null) =>
            Play(frames, f => dt, inputs, late, before);

        static float MaxStep(List<Frame> t, int a, int b) { float m = 0f; for (int i = Math.Max(1, a); i <= b && i < t.Count; i++) m = Math.Max(m, Math.Abs(t[i].strength - t[i - 1].strength)); return m; }
        static int FirstAt(List<Frame> t, int from, Func<Frame, bool> ok) { for (int i = from; i < t.Count; i++) if (ok(t[i])) return i; return -1; }
        static bool All(List<Frame> t, int a, int b, Func<Frame, bool> ok) { for (int i = a; i <= b && i < t.Count; i++) if (!ok(t[i])) return false; return true; }
        static bool Monotone(List<Frame> t, int a, int b, bool up) { for (int i = a + 1; i <= b && i < t.Count; i++) if (up ? t[i].strength < t[i - 1].strength - 1e-6f : t[i].strength > t[i - 1].strength + 1e-6f) return false; return true; }
        /// <summary>Covers answers exactly where the units are held off (the gate open and the fog drawable), on every frame a .. b.</summary>
        static bool CoversHold(List<Frame> t, int a, int b) => All(t, a, b, x => x.covers == x.hold);

        // ------------------------------------------------------------------------------------------------ the rows
        /// <summary>Every row. sources: file name (OzFogOfWar.cs, OzFogOfWar.Render.cs, OzFogOfWar.Gate.cs, OzFogOfWar.Minimap.cs, and the other
        /// fog partials) to its text, or null (the S rows are skipped then). assetsDir: the project's Assets folder (the N rows), or null.
        /// Returns the number of failed rows.</summary>
        public static int Run(RowFn row, Action<string> say, IDictionary<string, string> sources, string assetsDir)
        {
            int fails = 0;
            void R(string id, bool ok, string text) { if (!ok) fails++; row(id, ok, text); }
            bool keepWas = OzFowGate.CornerWindowKeepsFog, keep = OzFowGate.CornerWindowKeepsFogDefault;
            try
            {
                OzFowGate.CornerWindowKeepsFog = keep;
                // ---- T: the decision table
                var states = States();
                var table = new StringBuilder();
                foreach (var st in states)
                {
                    string why = OzFowGate.Decide(st.In, out bool cut);
                    int got = why == null ? Open : cut ? Cut : Fade;
                    bool ok = got == st.Expect && (st.Why == null ? why == null : why == st.Why);
                    R(st.Id, ok, $"{st.Name}: {Kind(got)}{(why != null ? " (" + why + ")" : "")}, wanted {Kind(st.Expect)}{(st.Why != null ? " (" + st.Why + ")" : "")}");
                    table.Append(st.Id).Append(';').Append(Kind(got)).Append(';').Append(why ?? "").Append('\n');
                }
                LastTable = table.ToString();
                bool superset = true; var sb = new StringBuilder();
                foreach (var (w, s) in OldReasons) { string n = OzFowGate.Decide(s, out _); if (n == null) superset = false; sb.Append($"'{w}' -> {(n ?? "OPEN")}; "); }
                R("T0", superset, "every reason of the gate before this kit closes the new gate too: " + sb.ToString().TrimEnd(' ', ';'));
                // one flag at a time: each closes (the order of the reasons only picks the text)
                var one = new (string n, OzFowGateInputs s, bool cut)[]
                {
                    ("Briefing", I(briefing: true), true), ("BlueBriefing", I(blue: true), true), ("Flyover", I(fly: true), true), ("EndingHold", I(hold: true), true),
                    ("EpicFlyby", I(epic: true), true), ("CaptureCine", I(capture: true), true), ("NoRtsCamera", I(noRts: true), true), ("RtsCameraOff", I(rtsOff: true), true),
                    ("CameraOff", I(camOff: true), true), ("ScreenTaken", I(screen: true), true), ("SimWaits", I(simWaits: true), true),
                    ("MatchDecided", I(decided: true), false), ("Demo", I(demo: true), false), ("SceneWindow", I(sceneWin: true), false),
                };
                bool each = true; var eb = new StringBuilder();
                foreach (var (n, s, c) in one) { string w = OzFowGate.Decide(s, out bool cut); bool ok = w != null && cut == c; each &= ok; if (!ok) eb.Append(n).Append(' '); }
                string mixed = OzFowGate.Decide(I(decided: true, rtsOff: true), out bool mixedCut);
                R("T30", each && mixedCut && mixed != null, $"each of the {one.Length} inputs alone closes the gate, the 11 camera / screen ones with a cut, MatchDecided, Demo and SceneWindow with the fade{(eb.Length > 0 ? "; WRONG: " + eb : "")}; "
                    + $"the match decided and then the RTS camera off (the lounger set): {(mixedCut ? "a cut" : "the fade")}");
                OzFowGate.CornerWindowKeepsFog = !keep;
                string cw = OzFowGate.Decide(I(corner: true), out bool cwCut), swOther = OzFowGate.Decide(I(sceneWin: true, corner: true), out bool swOtherCut);
                OzFowGate.CornerWindowKeepsFog = keep;
                string cwDef = OzFowGate.Decide(I(corner: true), out bool cwDefCut), swDef = OzFowGate.Decide(I(sceneWin: true, corner: true), out bool swDefCut);
                bool sceneAlways = swOther == "a scene's window" && swDef == "a scene's window" && !swOtherCut && !swDefCut;
                R("T31", sceneAlways && (!keep ? cw == null && cwDef == "a transmission" && !cwDefCut : cw == "a transmission" && !cwCut && cwDef == null),
                    $"the switch OzFowGate.CornerWindowKeepsFog: at its constant ({keep}) Commander Zod's window over the match is {(cwDef == null ? "OPEN (the fog stays: gameplay)" : cwDef + (cwDefCut ? " (cut)" : " (the 0.4 s fade)"))}; "
                    + $"the other value ({!keep}): {(cw == null ? "OPEN (the fog stays)" : cw + (cwCut ? " (cut)" : " (the 0.4 s fade)"))}; a scene's window (the blue scenes, the briefing's window folding) with either value: "
                    + $"'{swDef}' / '{swOther}' (the fade)");

                // ---- F: the fade
                float S(float t) => OzFowGate.Smootherstep(t / OzFowGate.FadeSeconds);
                float s0 = S(0f), s1 = S(0.1f), s2 = S(0.2f), s3 = S(0.3f), s4 = S(0.4f);
                R("F1", s0 == 0f && Math.Abs(s1 - 0.103516f) < 1e-4f && Math.Abs(s2 - 0.5f) < 1e-6f && Math.Abs(s3 - 0.896484f) < 1e-4f && s4 == 1f,
                    $"smootherstep over 0.4 s: 0 / 0.1 / 0.2 / 0.3 / 0.4 s -> {F3(s0)} / {F3(s1)} / {F3(s2)} / {F3(s3)} / {F3(s4)} (0, 0.104, 0.5, 0.896, 1)");
                int N(int fps) { float fd = 0f; int n = 0; while (fd < 1f && n < 10000) { fd = OzFowGate.FadeStep(fd, false, true, false, 1f / fps, true); n++; } return n; }
                int n60 = N(60), n30 = N(30), n144 = N(144), n20 = N(20), n15 = N(15);
                R("F2", n60 == 96 && n30 == 48 && (n144 == 230 || n144 == 231) && n20 == 32, $"the fade in (fog_fade_in_ease: 1.6 s) reaches full fog in {n60} frames at 60 fps (96), {n30} at 30 fps (48), {n144} at 144 fps (230.4), {n20} at 20 fps (32)");
                float slope = 0f; for (int i = 1; i <= 1000; i++) slope = Math.Max(slope, (OzFowGate.Smootherstep(i / 1000f) - OzFowGate.Smootherstep((i - 1) / 1000f)) * 1000f);
                float step60 = slope / 60f / OzFowGate.FadeSeconds;
                R("F3", slope < 1.876f && step60 < 0.08f, $"the steepest point (smootherstep's 1.875) is a step of {F3(step60)} of full fog a frame at 60 fps (under 0.08: no pop), 0 speed at both ends");
                float cutTo = OzFowGate.FadeStep(1f, false, false, true, 1f / 60f, true), cutLate = OzFowGate.FadeStep(1f, false, false, true, 0f, false);
                R("F4", cutTo == 0f && cutLate == 0f, "a hard cut from full fog: 0 on the cut's first frame, also when it is found after Update (a read with no time step)");
                float hitch = OzFowGate.FadeStep(1f, false, false, false, 0.6f, true), nan = OzFowGate.FadeStep(0.5f, false, true, false, float.NaN, true), neg = OzFowGate.FadeStep(0.5f, false, true, false, -1f, true);
                R("F5", Math.Abs(hitch - 0.875f) < 1e-6f && nan == 0.5f && neg == 0.5f,
                    $"a 0.6 s frame moves the fade's clock by {F3(1f - hitch)} (the frame counted as {OzFowGate.MaxStep} s: 1/8 of the way, a ramp, never a jump); a NaN or a negative step does not move it ({F3(nan)}, {F3(neg)})");
                float same = OzFowGate.FadeStep(0.3f, false, true, false, 1f / 60f, false);
                R("F6", same == 0.3f, "a second read in the same frame (LateUpdate, the player's camera) does not move the clock");
                bool c1 = OzFowGate.CoversAt(true, true), c2 = OzFowGate.CoversAt(false, true), c3 = OzFowGate.CoversAt(true, false);
                bool h1 = OzFowGate.HoldsOff(true, true), h3 = OzFowGate.HoldsOff(false, true), h4 = OzFowGate.HoldsOff(true, false);
                var hc = new OzFowGateClock(); hc.Update(null, false, 1f / 60f, 0, true, 1f); hc.Update("a scene", true, 1f / 60f, 1, true, 1f); hc.Update(null, false, 1f / 60f, 2, true, 0f);
                bool h2 = hc.Open && hc.HoldOff && hc.Covers && !hc.Draws && hc.Fade > 0f && hc.Fade < 0.05f;
                R("F7", c1 && !c2 && !c3 && h1 && h2 && !h3 && !h4,
                    "Covers and the units held off answer together: whenever the gate is open and the fog drawable, whatever the fade "
                    + $"(the clock's first open frame after a cut: fade {F3(hc.Fade)}, strength {hc.Strength.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture)} (drawn from over 0.001), the cloud not drawn yet, "
                    + $"Covers {(hc.Covers ? "on" : "OFF")}, the units {(hc.HoldOff ? "held off" : "NOT held off")}), never with the gate closed or the fog not drawable");
                bool same8 = true; foreach (bool o8 in new[] { false, true }) foreach (bool d8 in new[] { false, true }) same8 &= OzFowGate.CoversAt(o8, d8) == OzFowGate.HoldsOff(o8, d8);
                R("F8", same8 && OzFowGate.MaxStep == 0.05f && n15 == 32,
                    $"Covers answers exactly where the units are held off (CoversAt = HoldsOff for all 4 inputs: {(same8 ? "yes" : "NO")}; it reads no strength); a fade step counts at most {OzFowGate.MaxStep} s, so at 15 fps the fade in (1.6 s) takes {n15} frames ({F3(n15 / 15.0)} s of real time, each frame counted as 0.05 s)");
                LastCurve = Curve();

                // ---- Q: whole scenes, 60 fps
                const float dt = 1f / 60f;
                OzFowGateInputs play = I(), fly = I(briefing: true, fly: true, rtsOff: true, simWaits: true), handing = I(briefing: true, fly: true, rtsOff: true),
                                lastBrief = I(briefing: true), decided = I(decided: true), lounge = I(hold: true, rtsOff: true, decided: true), cine = I(fly: true, rtsOff: true, simWaits: true),
                                corner = I(corner: true);

                // Q1 the level-start briefing (0 .. 599), Handing (600 .. 689), after fly.End (690, 691), Play from 692
                var t1 = Play(900, dt, f => f < 600 ? fly : f < 690 ? handing : f < 692 ? lastBrief : play);
                int full1 = FirstAt(t1, 692, x => x.strength >= 1f), cov1 = FirstAt(t1, 692, x => x.covers), hold1 = FirstAt(t1, 692, x => x.hold);
                R("Q1", All(t1, 0, 691, x => !x.drawn && x.strength == 0f && !x.covers && !x.hold) && full1 == 692 + 95 && hold1 == 692 && !t1[692].drawn && All(t1, 692, 899, x => x.hold)
                        && cov1 == 692 && CoversHold(t1, 0, 899) && Monotone(t1, 692, full1, true) && MaxStep(t1, 692, full1) < 0.08f,
                    $"the level-start briefing, its hand-back and the 2 frames after fly.End: no cloud, no Covers, nothing held off on every frame (0 .. 691); play from frame 692: the fade in "
                    + $"{F3(t1[692].strength)} -> full fog at frame {full1} (692 + 95 = 1.6 s, fog_fade_in_ease), rising only, largest step {F3(MaxStep(t1, 692, full1))}; "
                    + $"the covered units held off from frame {hold1} and Covers from frame {cov1}, on every frame (the first frame of play, strength {F3(t1[692].strength)}, the cloud {(t1[692].drawn ? "drawn" : "not drawn yet: GateCamera holds them off")}; "
                    + "no covered enemy under the thin cloud of the fade in, and no bar, badge, marker, hover or click on one)");
                // Q2 a level start with no briefing
                var t2 = Play(30, dt, f => play);
                R("Q2", t2[0].strength == 1f && t2[0].covers && t2[0].hold && t2[0].drawn && All(t2, 0, 29, x => x.strength == 1f && x.hold),
                    "a level start with no briefing (OPTIONS briefing off, a retry without one): covered at once on the first frame (the map starts covered: no fade from an open map), the units held off from it");
                // Q3 the blue briefing's frame after fly.End (old gap): BlueBriefing alone on 690, 691
                OzFowGateInputs bfly = I(blue: true, fly: true, rtsOff: true, simWaits: true), blast = I(blue: true);
                var t3 = Play(800, dt, f => f < 690 ? bfly : f < 692 ? blast : play);
                R("Q3", All(t3, 0, 691, x => !x.drawn && x.strength == 0f && !x.covers && !x.hold) && t3[692].open && t3[692].hold && All(t3, 692, 759, x => x.hold) && t3[787].strength >= 1f,
                    "the blue briefing: its frames after fly.End and before phase Play stay closed (strength 0, the old gate's fade started there), the fade in from phase Play, the units held off from its first frame");
                // Q4 a mid-match hard cut (ZodCinePreview) from full fog at frame 100, back at 400
                var t4 = Play(500, dt, f => f >= 100 && f < 400 ? cine : play);
                int full4 = FirstAt(t4, 400, x => x.strength >= 1f), cov4 = FirstAt(t4, 400, x => x.covers), hold4 = FirstAt(t4, 400, x => x.hold);
                R("Q4", t4[99].strength == 1f && t4[99].drawn && t4[99].hold && t4[100].strength == 0f && !t4[100].drawn && !t4[100].covers && !t4[100].hold
                        && All(t4, 100, 399, x => !x.drawn && !x.covers && !x.hold) && full4 == 495 && hold4 == 400 && All(t4, 400, 499, x => x.hold) && cov4 == 400 && CoversHold(t4, 0, 499),
                    $"a mid-match hard cut from full fog (frame 100): gone on the cut's first frame (strength {F3(t4[100].strength)}, no pass, no Covers, nothing held off), none through the scene; back at 400: "
                    + $"the units held off from frame {hold4} and Covers from frame {cov4} (both the first frame of play, strength {F3(t4[400].strength)}), full fog at frame {full4} (1.6 s)");
                // Q5 a scene begun after the fog's Update (a coroutine, a LateUpdate, the render loop) at frame 100: the late read; it ends after
                //    the fog's Update too, at frame 150 (a flight ending in a LateUpdate): the late read opens the gate with the fade still at 0
                var t5 = Play(260, dt, f => f <= 100 ? play : f <= 150 ? I(rtsOff: true, simWaits: true) : play, f => f == 100 ? I(simWaits: true) : f == 150 ? play : (OzFowGateInputs?)null);
                int full5 = FirstAt(t5, 150, x => x.strength >= 1f);
                R("Q5", t5[99].drawn && !t5[100].drawn && t5[100].strength == 0f && !t5[100].covers && !t5[100].hold && All(t5, 100, 149, x => !x.drawn && !x.hold && !x.covers)
                        && t5[150].open && t5[150].strength == 0f && !t5[150].drawn && t5[150].hold && t5[150].covers && All(t5, 150, 259, x => x.hold && x.covers) && full5 == 246,
                    "a scene begun after the fog's Update (found by the read in LateUpdate or at the player's camera), even one that keeps the RTS camera on: no cloud, no Covers and nothing held off on its first frame; "
                    + $"one that ends after the fog's Update (frame 150): on that frame the gate open with the fade still at 0 (strength {F3(t5[150].strength)}, the cloud not drawn yet: GateCamera holds them off), "
                    + $"the units {(t5[150].hold ? "held off" : "NOT held off")} and Covers {(t5[150].covers ? "on" : "OFF")} at once, full fog at frame {full5} (150 + 96)");
                // Q6 the ending blend at frame 100 (the glide), the lounger set at frame 175 (1.25 s), 60 fps
                var t6 = Play(400, dt, f => f < 100 ? play : f < 175 ? decided : lounge);
                int zero6 = FirstAt(t6, 100, x => x.strength == 0f), off6 = FirstAt(t6, 100, x => !x.covers);
                R("Q6", t6[99].hold && t6[99].covers && !t6[100].hold && t6[100].strength > 0.99f && t6[100].drawn && !t6[100].covers && zero6 == 123 && Monotone(t6, 100, 123, false) && MaxStep(t6, 100, 123) < 0.08f
                        && All(t6, 123, 399, x => !x.drawn && x.strength == 0f) && All(t6, 100, 399, x => !x.hold) && off6 == 100 && CoversHold(t6, 0, 399),
                    $"the ending (the match decided at frame 100, the RTS camera stays on): no unit held off and Covers off from its first frame (frame {off6}: the units show under the full cloud, which thins over them, "
                    + $"and their bars and badges with them), the cloud fades {F3(t6[100].strength)} -> 0 at frame {zero6} (0.4 s), falling only, largest step {F3(MaxStep(t6, 100, 123))}; the lounger / hot tub cut at 175 finds it at 0");
                // Q7 the pause menu (and the in-match OPTIONS, game speed, shakes ...): the gate's inputs do not change (S1), the fog stays
                var t7 = Play(400, dt, f => play);
                R("Q7", All(t7, 0, 399, x => x.strength == 1f && x.drawn && x.covers && x.hold), "the pause menu open 100 .. 300 (the gate reads no pause, no time scale): full fog, Covers, the units held off, the pass every frame");
                // Q8 the idle barb's window 100 .. 400, both values of the switch
                OzFowGate.CornerWindowKeepsFog = false;
                var t8 = Play(500, dt, f => f >= 100 && f < 400 ? corner : play);
                OzFowGate.CornerWindowKeepsFog = true;
                var t8b = Play(500, dt, f => f >= 100 && f < 400 ? corner : play);
                OzFowGate.CornerWindowKeepsFog = keep;
                int z8 = FirstAt(t8, 100, x => x.strength == 0f), f8 = FirstAt(t8, 400, x => x.strength >= 1f), h8 = FirstAt(t8, 400, x => x.hold);
                R("Q8", z8 == 123 && f8 == 495 && h8 == 400 && All(t8, 100, 399, x => !x.hold) && All(t8, 400, 499, x => x.hold) && t8[100].why == "a transmission" && CoversHold(t8, 0, 499) && All(t8b, 0, 499, x => x.strength == 1f && x.hold && x.covers),
                    $"Commander Zod's idle barb over the running match 100 .. 399: the switch true ({(keep ? "the constant: gameplay" : "the other value")}): the fog stays, Covers and the units held off on every frame; "
                    + $"the switch false ({(keep ? "the other value" : "the constant")}): a transmission, the units shown and Covers off from the window's first frame, the cloud out over 0.4 s (0 at frame {z8}), back over 1.6 s after it "
                    + $"(the units held off and Covers on again from frame {h8}, the first frame of play; full at frame {f8})");
                // Q9 the demo (F8) 100 .. 106 then given back: the fade turns round where it is (no jump)
                var t9 = Play(200, dt, f => f >= 100 && f < 106 ? I(demo: true) : play);
                int h9 = FirstAt(t9, 100, x => x.hold);
                R("Q9", t9[105].strength < 0.95f && t9[105].strength > 0.85f && MaxStep(t9, 99, 140) < 0.08f && t9[140].strength == 1f && h9 == 106 && All(t9, 100, 105, x => !x.hold) && All(t9, 106, 199, x => x.hold)
                        && All(t9, 100, 105, x => !x.covers) && CoversHold(t9, 0, 199),
                    $"the demo for 6 frames, then the player again: the fade turns round where it is ({F3(t9[105].strength)} at its lowest), no jump (largest step {F3(MaxStep(t9, 99, 140))}); the units shown and Covers off from "
                    + $"its first frame, both back from the first frame of play (frame {h9}, strength {F3(t9[106].strength)})");
                // Q10 30 fps and a 0.6 s hitch in a fade out
                var t10 = Play(60, 1f / 30f, f => f < 10 ? play : decided);
                var t10b = Play(60, 1f / 60f, f => f < 10 ? play : decided);
                var m10 = new Model(); m10.Update(play, dt, 0); m10.Update(decided, dt, 1); float b10 = m10.strength; m10.Update(decided, 0.6f, 2);
                R("Q10", FirstAt(t10, 10, x => x.strength == 0f) == 21 && FirstAt(t10b, 10, x => x.strength == 0f) == 33 && b10 - m10.strength > 0.02f && b10 - m10.strength < 0.05f,
                    $"the fade out takes 12 frames at 30 fps (0 at frame {FirstAt(t10, 10, x => x.strength == 0f)}, from 10) and 24 at 60 fps (0 at frame {FirstAt(t10b, 10, x => x.strength == 0f)}); "
                    + $"a 0.6 s frame in it moves strength {F3(b10)} -> {F3(m10.strength)} (counted as 0.05 s: one short step, the fade goes on over the next frames)");
                // Q11 the testing panel: FOG OFF puts strength to 0 (the off path), FOG ON again: a fade in from 0, never a jump back to the old clock
                var t11 = Play(140, dt, f => play, null, (m, f) => { if (f == 20) { m.strength = 0f; } });
                R("Q11", t11[20].strength < 0.01f && t11[19].strength == 1f && FirstAt(t11, 20, x => x.strength >= 1f) == 115 && All(t11, 0, 79, x => x.hold),
                    $"the testing panel's FOG OFF then ON (strength put to 0 elsewhere): the fade starts again from 0 ({F3(t11[20].strength)}), full at 1.6 s; the units held off on every frame (the gate open: also on the fade's first frame)");
                // Q12 the first frame is a scene: nothing to fade from
                var t12 = Play(10, dt, f => fly);
                R("Q12", All(t12, 0, 9, x => x.strength == 0f && !x.drawn && !x.covers && !x.hold), "the match's first frame inside the briefing: strength 0 from the start (no cloud before the flyover)");
                // Q13 hitches: the hand-over frame (0.15 s, ZodBriefing's allocations and the HUD coming back) right after a hand-back, and one at the fade's steepest point
                var t13 = Play(200, f => f == 101 ? 0.15f : dt, f => f >= 50 && f < 100 ? cine : play);
                float first13 = t13[101].strength - t13[100].strength, raw13 = OzFowGate.Smootherstep(1f / 24f + 0.15f / OzFowGate.FadeSeconds) - t13[100].strength;
                var t13b = Play(200, f => f == 112 ? 0.15f : dt, f => f >= 50 && f < 100 ? cine : play);
                float mid13 = MaxStep(t13b, 100, 160), rawMid = OzFowGate.Smootherstep(12f / 24f + 0.15f / OzFowGate.FadeSeconds) - OzFowGate.Smootherstep(12f / 24f);
                R("Q13", first13 < 0.05f && mid13 < 0.25f && Monotone(t13, 100, 160, true) && FirstAt(t13, 100, x => x.strength >= 1f) > 0,
                    $"a 0.15 s hand-over frame right after the hand-back: the cloud's first step {F3(first13)} (a frame not counted at most 0.05 s would jump it {F3(raw13)}); "
                    + $"the same frame at the fade's steepest point: {F3(mid13)} (without the cap {F3(rawMid)}); rising only, full fog in the end");
                // Q14 the red briefing (its comms window up from frame 0), phase Play at 692, the window still folding 692 .. 709, then play;
                //     Commander Zod's idle barb in the same window 900 .. 1099: the live latch (OzFowWindowTail) tells the two apart
                var tail14 = new OzFowWindowTail();
                OzFowGateInputs Live14(int f)
                {
                    var s = f < 600 ? fly : f < 690 ? handing : f < 692 ? lastBrief : play;
                    bool win = f < 710 || (f >= 900 && f < 1100);
                    s.CornerWindow = win; s.SceneWindow = tail14.Step(s.Briefing, win);
                    return s;
                }
                var t14 = Play(1300, dt, Live14);
                int full14 = FirstAt(t14, 692, x => x.strength >= 1f), z14 = FirstAt(t14, 900, x => x.strength == 0f);
                bool barb14 = keep ? All(t14, full14 >= 0 ? full14 : 0, 1299, x => x.strength == 1f && x.hold && x.covers) : t14[900].why == "a transmission" && z14 == 923;
                R("Q14", All(t14, 0, 709, x => x.strength == 0f && !x.drawn && !x.covers && !x.hold) && t14[700].why == "a scene's window" && t14[710].open && full14 == 710 + 95
                         && FirstAt(t14, 692, x => x.hold) == 710 && FirstAt(t14, 692, x => x.covers) == 710 && All(t14, 710, 899, x => x.hold) && CoversHold(t14, 0, 1299) && barb14 && !tail14.Held,
                    $"the red briefing's comms window still folding 18 frames after phase Play: '{t14[700].why}' (strength 0, no flash between the briefing's cut and the window's close), the gate open at the window's close "
                    + $"(frame 710) and the units held off and Covers on from that frame, full fog at frame {full14} (710 + 95); the idle barb later in the same window (900 .. 1099): "
                    + $"{(keep ? "the switch true: full fog, Covers and the units held off on every frame (the latch let go when the window closed)" : $"the switch false: a transmission, 0 at frame {z14}")}");
                // Q15 the blue briefing, its window still folding 692 .. 709 (show.done at the fold's start: only the window says so), then play;
                //     a blue scene's window over the running match 900 .. 1099 (dev: DebugNuke): the fade out and back with either switch value
                var t15 = Play(1300, dt, f => f < 690 ? bfly : f < 692 ? blast : f < 710 || (f >= 900 && f < 1100) ? I(sceneWin: true) : play);
                int full15 = FirstAt(t15, 692, x => x.strength >= 1f), z15 = FirstAt(t15, 900, x => x.strength == 0f), back15 = FirstAt(t15, 1100, x => x.strength >= 1f);
                R("Q15", All(t15, 0, 709, x => x.strength == 0f && !x.drawn && !x.covers && !x.hold) && t15[700].why == "a scene's window" && full15 == 805 && FirstAt(t15, 692, x => x.hold) == 710
                         && All(t15, 710, 899, x => x.hold) && z15 == 923 && All(t15, 900, 1099, x => !x.hold) && back15 == 1195 && FirstAt(t15, 1100, x => x.hold) == 1100 && All(t15, 1100, 1299, x => x.hold)
                         && Monotone(t15, 900, 923, false) && CoversHold(t15, 0, 1299),
                    $"the blue briefing's window still folding after phase Play: '{t15[700].why}', strength 0 until it has folded (710), the units held off and Covers on from that frame, full fog at frame {full15}; a blue scene's window over the "
                    + $"running match (900 .. 1099): the units shown and Covers off from its first frame, the cloud 0 at frame {z15} (0.4 s), both back from frame 1100 (the first frame of play), back full at frame {back15}");

                // ---- S: the sources
                if (sources != null)
                {
                    string Src(string n) => sources.TryGetValue(n, out var s) ? s : null;
                    string gate = Src("OzFogOfWar.Gate.cs"), fog = Src("OzFogOfWar.cs"), render = Src("OzFogOfWar.Render.cs"), mini = Src("OzFogOfWar.Minimap.cs");
                    string reader = Body(gate, "public OzFowGateInputs GateInputsNow()");
                    var bad = new List<string>(); if (reader != null) foreach (var w in GameplayWords) if (reader.Contains(w)) bad.Add(w);
                    string blueWin = Body(gate, "static bool GateBlueWindow()");
                    bool reads = reader != null && reader.Contains("ZodBriefing.BriefingActive") && reader.Contains("PlayerInput.MatchDecided") && reader.Contains("ZodBriefing.CommsVisible")
                                 && reader.Contains("gateBriefTail.Step(ZodBriefing.BriefingActive, zodWindow)") && reader.Contains("OzBlueCameo.SceneShowing") && reader.Contains("GateBlueWindow()")
                                 && blueWin != null && blueWin.Contains("b.Comms.Visible");
                    R("S1", reader != null && bad.Count == 0 && reads,
                        reader == null ? "OzFogOfWar.Gate.cs GateInputsNow not found" : $"the gate's reader (GateInputsNow, {reader.Length} chars) reads none of the gameplay states ({string.Join(", ", GameplayWords)}){(bad.Count > 0 ? ": FOUND " + string.Join(", ", bad) : "")}; "
                        + $"it reads the scenes' windows: Zod's briefing window through the latch, the blue scene's window open or folding: {(reads ? "yes" : "NO")}");
                    string upd = Body(fog, "void Update()");
                    int offAt = upd != null ? upd.IndexOf("if (!wanted || mask == null)", StringComparison.Ordinal) : -1, retAt = upd != null && offAt >= 0 ? upd.IndexOf("return;", offAt, StringComparison.Ordinal) : -1;
                    int stepAt = upd != null ? upd.IndexOf("GateStep()", StringComparison.Ordinal) : -1, stamp = upd != null ? upd.IndexOf("StampRound(false)", StringComparison.Ordinal) : -1, ease = upd != null ? upd.IndexOf("mask.Ease(", StringComparison.Ordinal) : -1;
                    R("S2", upd != null && offAt >= 0 && retAt > offAt && stepAt > retAt && Count(upd, "GateStep()") == 1 && !upd.Contains("Mathf.MoveTowards(strength") && !upd.Contains("strength >= HideAt") && !upd.Contains("hidingNow = strength"),
                        "Update: the off path (FOG OF WAR off, any WhyOff reason) returns before the gate is read (rule 5: nothing changes, no fog anywhere); the gate read once; the old linear fade and the strength-based hiding gone");
                    R("S3", upd != null && stamp >= 0 && ease > stamp && stepAt > ease && !Before(upd, "GateStep()", "StampRound(false)"),
                        "the reveal map runs every frame before the gate and whatever it says (the stamp round, the eased reveal, the upload): play comes back with the right explored ground");
                    string begin = Body(render, "void BeginCamera(ScriptableRenderContext ctx, Camera cam)");
                    int guard = begin != null ? begin.IndexOf("!PlayerView(cam) || !GateCamera(cam)", StringComparison.Ordinal) : -1;
                    string camGate = Body(gate, "bool GateCamera(Camera cam)"), holdNow = Body(gate, "void GateHoldOffNow(Camera cam)");
                    int camHold = camGate != null ? camGate.IndexOf("if (!gateClock.Draws && gateClock.HoldOff) GateHoldOffNow(cam);", StringComparison.Ordinal) : -1;
                    R("S4", begin != null && guard >= 0 && begin.Contains("if (gateClock.HoldOff)") && !begin.Contains("strength >= HideAt") && !begin.Contains("strength <= 0.001f || !PlayerView")
                        && begin.IndexOf("EnqueuePass", StringComparison.Ordinal) > guard && begin.IndexOf("HideCovered(cam)", StringComparison.Ordinal) > guard
                        && camHold >= 0 && camHold > camGate.IndexOf("GateRecheck(true)", StringComparison.Ordinal) && camHold < camGate.IndexOf("return gateClock.Draws;", StringComparison.Ordinal)
                        && holdNow != null && holdNow.Contains("HideCovered(cam);"),
                        "BeginCamera: the gate read again at the player's camera before the pass and the hiding; the units held off on the gate's HoldOff (the gate open: from the first frame of play), "
                        + "not on the fade's strength; on a frame the cloud is not drawn yet (the fade in's first frames: BeginCamera returns before its own hiding) GateCamera holds them off itself (GateHoldOffNow -> HideCovered), after the read");
                    string covers = Body(fog, "public static bool Covers(ZObject o)");
                    R("S5", covers != null && covers.Contains("if (!hidingNow ||"), "Covers (the hover, the click, the attack orders, the health bars, the badges, the crate markers) answers only while hidingNow, which the gate sets (the gate open and the fog drawable: the same moment as the hold-off)");
                    string ms = Body(mini, "void MinimapStep()");
                    R("S6", ms != null && ms.Contains("strength > 0.001f") && ms.Contains("strength)"), "the minimap cover is shown while strength is over 0.001 with its alpha = strength: it follows the gate's fade (0 at once on a cut)");
                    string why = Body(fog, "public string GateWhyBefore()");
                    R("S7", fog != null && fog.Contains("public string GateWhy() => GateNow(out _);") && why != null && why.Contains("ZodBriefing.BriefingActive") && why.Contains("CaptureKit.CineActive"),
                        "GateWhy (read by the fog checks) is the new gate; the old reasons are kept as GateWhyBefore for the Play check's superset row");
                    int late = 0; var lateIn = new List<string>();
                    foreach (var kv in sources) { int c = Count(kv.Value, "void LateUpdate()"); if (c > 0) { late += c; lateIn.Add(kv.Key); } }
                    R("S8", late == 1 && lateIn.Count == 1 && lateIn[0] == "OzFogOfWar.Gate.cs", $"one LateUpdate in the fog's partial files ({string.Join(", ", lateIn)}): the gate's own");
                    var byHide = new List<string>(); foreach (var kv in sources) if (kv.Value.Contains(">= HideAt")) byHide.Add(kv.Key);
                    R("S9", fog != null && fog.Contains("FadeInSeconds = OzFowGate.FadeInSeconds, FadeOutSeconds = OzFowGate.FadeSeconds") && byHide.Count == 0,
                        "the fade constants the older checks read (FadeInSeconds, FadeOutSeconds) are the gate's (in 1.6 s, out 0.4 s); no fog partial gates Covers or the hiding on the strength any more ('>= HideAt' "
                        + (byHide.Count == 0 ? "in none of them" : "FOUND in " + string.Join(", ", byHide)) + ")");
                    string gStep = Body(gate, "bool GateStep()"), gRe = Body(gate, "void GateRecheck(bool atCamera)"), gAfter = Body(gate, "void GateAfter(bool cut, bool late)"), gCam = Body(gate, "bool GateCamera(Camera cam)");
                    string gCode = gate != null ? gate.Substring(Math.Max(0, gate.IndexOf("public sealed partial class OzFogOfWar", StringComparison.Ordinal))) : null;
                    R("S10", gStep != null && gStep.Contains("gateClock.Update(") && gStep.Contains("GateAfter(") && gRe != null && gRe.Contains("gateClock.Recheck(") && gRe.Contains("GateAfter(")
                        && gAfter != null && gAfter.Contains("strength = gateClock.Strength;") && gAfter.Contains("hidingNow = gateClock.Covers;") && gCam != null && gCam.Contains("return gateClock.Draws;")
                        && gCode != null && !gCode.Contains("Smootherstep(") && !gCode.Contains("FadeStep(") && !gCode.Contains("HoldsOff(") && !gCode.Contains("CoversAt("),
                        "the live gate (OzFogOfWar.Gate.cs) runs the counted clock and nothing else: GateStep -> OzFowGateClock.Update, GateRecheck -> Recheck, GateAfter copies Strength and Covers, GateCamera returns Draws; no fade or hiding rule of its own");
                    string reset = Body(gate, "static void GateResetStatics()");
                    R("S11", reset != null && reset.Contains("OzFowGate.CornerWindowKeepsFog = OzFowGate.CornerWindowKeepsFogDefault;") && !reset.Contains("CornerWindowKeepsFog = true") && !reset.Contains("CornerWindowKeepsFog = false"),
                        $"every Play and exe start resets the switch to its constant OzFowGate.CornerWindowKeepsFogDefault ({keep}), never to a literal: flipping the constant flips the game");
                }
                else say("  (S rows skipped: no sources given)");

                // ---- N: the scenes with no fog component
                if (assetsDir != null && Directory.Exists(assetsDir))
                {
                    string guid = Guid(Path.Combine(assetsDir, "OpenZ/Scripts/View/ZGame.cs.meta"));
                    var scenes = BuildScenes(Path.Combine(Path.GetDirectoryName(assetsDir.TrimEnd('/', '\\')) ?? "", "ProjectSettings/EditorBuildSettings.asset"));
                    var with = new List<string>(); var without = new List<string>();
                    foreach (var sc in scenes)
                    {
                        string p = Path.Combine(Path.GetDirectoryName(assetsDir.TrimEnd('/', '\\')) ?? "", sc);
                        if (!File.Exists(p)) { without.Add(Path.GetFileNameWithoutExtension(sc) + " (missing)"); continue; }
                        (File.ReadAllText(p).Contains("guid: " + guid) ? with : without).Add(Path.GetFileNameWithoutExtension(sc));
                    }
                    R("N1", guid != null && without.Contains("MainMenu") && without.Contains("Intro") && !with.Contains("MainMenu") && !with.Contains("Intro"),
                        $"the build's scenes without a ZGame (no fog component can ever be added there, OzFogOfWar.TryInstall): {string.Join(", ", without)} (the main menu with its flybys, level cards, posters "
                        + $"and the loading screen; the intro); with one: {string.Join(", ", with)}");
                    string fog = sources != null && sources.TryGetValue("OzFogOfWar.cs", out var fs) ? fs : null;
                    string whyOff = Body(fog, "string WhyOff()");
                    R("N2", with.Contains("UE_Playground") && whyOff != null && whyOff.Contains("OzUnitEditor.IsPlayground") && whyOff.Contains("game.IsMayhem") && !whyOff.Contains("OzFogOfWarOption.On"),
                        "the Unit editor's playground has a ZGame but WhyOff refuses the fog there (and outside MAYHEM, a computer match, 'Zod exact' unless MAYHEM: 1.4 MAYHEM)");
                }
                else say("  (N rows skipped: no project folder given)");
            }
            finally { OzFowGate.CornerWindowKeepsFog = keepWas; }
            return fails;
        }

        /// <summary>The decision table of the last run (id;answer;reason a line) and the fade curve (t;fade;strength a line, 60 fps in and out).</summary>
        public static string LastTable = "", LastCurve = "";

        static string Curve()
        {
            var sb = new StringBuilder("t_s;phase;fade;strength\n");
            float fd = 0f;
            for (int i = 0; i <= 30; i++) { sb.Append(F3(i / 60.0)).Append(";in;").Append(F3(fd)).Append(';').Append(F3(OzFowGate.Smootherstep(fd))).Append('\n'); fd = OzFowGate.FadeStep(fd, false, true, false, 1f / 60f, true); }
            fd = 1f;
            for (int i = 0; i <= 30; i++) { sb.Append(F3(i / 60.0)).Append(";out;").Append(F3(fd)).Append(';').Append(F3(OzFowGate.Smootherstep(fd))).Append('\n'); fd = OzFowGate.FadeStep(fd, false, false, false, 1f / 60f, true); }
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------ source helpers
        /// <summary>The body of the member whose header is <paramref name="head"/> (from its first '{' to the matching '}'), or null.</summary>
        public static string Body(string src, string head)
        {
            if (src == null) return null;
            int a = src.IndexOf(head, StringComparison.Ordinal); if (a < 0) return null;
            int b = src.IndexOf('{', a); if (b < 0) return null;
            int depth = 0;
            for (int i = b; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/') { int e = src.IndexOf('\n', i); if (e < 0) break; i = e; continue; }   // a line comment
                if (c == '"') { i++; while (i < src.Length && src[i] != '"') { if (src[i] == '\\') i++; i++; } continue; }                        // a string
                if (c == '\'') { i++; while (i < src.Length && src[i] != '\'') { if (src[i] == '\\') i++; i++; } continue; }                       // a char
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return src.Substring(b, i - b + 1);
            }
            return null;
        }
        static int Count(string s, string w) { if (s == null) return 0; int n = 0, i = 0; while ((i = s.IndexOf(w, i, StringComparison.Ordinal)) >= 0) { n++; i += w.Length; } return n; }
        static bool Before(string s, string a, string b) { int i = s.IndexOf(a, StringComparison.Ordinal), j = s.IndexOf(b, StringComparison.Ordinal); return i >= 0 && j >= 0 && i < j; }

        static string Guid(string meta)
        {
            if (!File.Exists(meta)) return null;
            foreach (var l in File.ReadAllLines(meta)) if (l.StartsWith("guid: ", StringComparison.Ordinal)) return l.Substring(6).Trim();
            return null;
        }

        static List<string> BuildScenes(string settings)
        {
            var list = new List<string>();
            if (!File.Exists(settings)) return list;
            foreach (var l in File.ReadAllLines(settings)) { var t = l.Trim(); if (t.StartsWith("path: ", StringComparison.Ordinal)) list.Add(t.Substring(6).Trim()); }
            return list;
        }
    }
}
