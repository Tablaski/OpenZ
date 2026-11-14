#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.4 radar_enemy_bars, DV172 Play proof (editor only), menu OpenZ/V1.4/Radar Enemy Bars: Play Proof (Editor/OzV14RadarBarsCount.cs). Start a
    /// level in Play first (L02: two neutral radars) and let the briefing end; about 1.5 minutes. The real game at the normal RTS camera (70 m), a
    /// proof mouse whose events go through the Input System like a real one (the View/OzBlueRepairProof.cs devices; no window focus needed). The
    /// CPU is told to give no orders (ZBot.Ignored), the day clock is held at morning; zones change owner through the game's own rule
    /// (ZWorld.AwardZone, as a robot at the flag) and a radar is ruined through the game's own health rule (SetHealth 0 + UpdateObjectHealth).
    /// What is drawn is read from the overlay's own images (OpenZ_UnitOverlay/Root/Mark/Bar: Frame, Lost, Fill shown = active and enabled, at any
    /// alpha: the kit switches them off at or under OzRadarBars.Hidden, so an image left on at a tiny alpha counts as drawn, and a drawn Fill's
    /// alpha is checked against the factor apart, within 0.01; the hovered mark = the one whose name chip is on), not only from the numbers the
    /// kit writes. Round 2: "shown" no longer needs alpha over 0.01 (the first frames of a fade in and the last of a fade out draw the bar
    /// between factor 0.004 and 0.01, which failed G1 / L1 on a correct build).
    ///  P0 the rule acts (human side, DV33's switch on, not 'Zod exact', not the Unit editor) and the player owns no radar (radars he held are
    ///     given to the enemy first, put back at the end); the enemy under the pointer carries grenades (one given if it had none);
    ///  H1 the pointer on the enemy for 1 s: on every frame no bar image of any mark is drawn and the factor is 0 (nothing flickers), its frame
    ///     and name chip as before, its grenade icon drawn (picture 1);
    ///  H2 the pointer on an own unit: its bar as before (alpha 1, its images drawn; picture 2);
    ///  G1 a radar zone given to the player, the pointer still on the enemy: nothing changes for the 0.6 s hold (game time), then the bar fades in,
    ///     monotonic, done in 0.35 s (a frame's slack), the drawn images' alpha = the shared factor, no step bigger than the curve allows; M1: the
    ///     mark's brackets, its name chip and its grenade icon do not move between factor 0, the middle of the fade and 1 (picture 3);
    ///  L1 that radar ruined: held, then the bar fades out the same way, nothing moves (picture 4);
    ///  K1 a contested radar flag: its zone changes owner every 0.2 s of game time for 3 s (as two units of both sides on it): the factor stays 0
    ///     and no bar image is drawn on any frame, no change is followed;
    ///  Q1 the cost: the radar truth's time a call on this world (no radar: the whole object list) and the overlay's own time a frame
    ///     (OzPerfNumbers) with the rule on and with DV33's switch off; Q1g the garbage a frame in those two states, by the engine's own
    ///     "GC Allocated In Frame" (the counter View/OzPerfNumbers.cs reads; the editor's Mono has no thread counter), quiet frames only (the proof
    ///     itself does nothing but move the pointer), the median: the rule on makes no more than DV33 off (64 bytes of slack for the game's noise);
    ///  Q2 the rule's per-frame questions (DV33's switch, the Unit editor, the human side, the radar truth, the hold and the fade on a separate
    ///     state, BarAlpha / Hides / BarOff, the label) 5000 times in one frame: no more garbage than a plain frame (under 1.6 bytes a call, so no
    ///     allocation at all); both rows only when the counter sees a known 64 KB array a frame, else INFO "not measurable";
    ///  Z1 another radar taken; 0.1 s into its fade the pause menu opens for 1 s and closes: the fade runs on unscaled time through the pause
    ///     (the factor just before the menu closes is 1), every frame's step within the curve's bound, the bar drawn again once the HUD is back;
    ///  A1 HEALTH BARS ON EVERY DAMAGED UNIT on (put back after), the enemy squad and an own robot damaged, the pointer on empty ground: no enemy
    ///     mark at all without a radar (left out, not even a grenade icon; picture 5); with the radar the enemy bars drawn, more bar images than
    ///     without (picture 6);
    ///  C  the real-time stretch for the OBS take (no pictures here: they would stall the frames): the pointer on the enemy, the radar lost, then
    ///     taken again; the log gives its clock times to cut the take (README: the OBS lines), C1 checks it faded out and in.
    /// Writes oz_v14_radar_bars.txt (PASS / FAIL rows + the sampled curves) and pictures 1-6 in
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OzRadarBarsProof : MonoBehaviour
    {
        public static string Report = "idle";
        const string KitName = "radar_enemy_bars";
        public const float NormalHeight = 70f;
        public static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "oz_v14_radar_bars.txt"));

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                    foreach (var k in new[] { Path.Combine(stage, "kits", KitName), Path.Combine(stage, KitName) })
                        if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "oz_v14_radar_bars"));
        }

        public static void Run()
        {
            if (FindFirstObjectByType<OzRadarBarsProof>()) { Debug.LogWarning("[OzRadarBarsProof] already running"); return; }
            Report = "running"; new GameObject("OzRadarBarsProof").AddComponent<OzRadarBarsProof>();
        }

        ZGame g; ZWorld W; RtsCamera rts; OpenZHud hud; OpenZUnitOverlay ov; Team me, foe;
        readonly StringBuilder sb = new StringBuilder(); int passes, fails, shots; string dir;
        readonly List<KeyValuePair<ZObject, Team>> zones = new List<KeyValuePair<ZObject, Team>>();
        static Mouse proofMouse, realMouse;
        bool alwaysWas, alwaysSet, ruleWas, ruleSet; ZObject nadeOf;

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzRadarBarsProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png")); shots++;
            Log($"   picture {name}.png (full screen {Screen.width}x{Screen.height}, the RTS camera at {(rts ? rts.height : 0f):0} m)");
            yield return Frames(3);
        }
        void Cam(Vector3 at) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(NormalHeight); }
        Vector2 ScreenOf(ZObject o) { var c = rts ? rts.GetComponent<Camera>() : Camera.main; var p = c.WorldToScreenPoint(g.CenterWorld(o) + Vector3.up * 0.9f); return new Vector2(p.x, p.y); }   // the middle of the figure (the overlay's hover box)
        ZObject FlagOf(ZObject b) { foreach (var f in W.Flags) foreach (var x in f.LinkedObjects) if (x == b) return f; return null; }
        void Give(ZObject flag, Team t) { if (flag == null) return; if (!zones.Any(z => z.Key == flag)) zones.Add(new KeyValuePair<ZObject, Team>(flag, flag.Owner)); W.AwardZone(flag, t); }
        void Ruin(ZObject b) { if (b != null && !b.Destroyed) { b.SetHealth(0); W.UpdateObjectHealth(b); } }
        bool OnEnemy(ZObject e) { var h = ov ? ov.HoverMember : null; return h != null && (h == e || h.Leader == e); }
        static float Bound(float dt) => 1.875f * Mathf.Min(Mathf.Max(dt, 0f), OzRadarBarsRule.MaxStep) / Mathf.Max(0.001f, OzRadarBars.FadeSeconds) + 0.002f;
        static string Clock() => System.DateTime.Now.ToString("HH:mm:ss.fff");

        // ---- what the overlay really draws (its own images, not the kit's numbers)
        struct Seen { public int bars; public float fillA; public bool hov, nade; public Vector2 pos, size, chip; public Vector3 nadeAt; }
        static readonly string[] BarParts = { "Frame", "Lost", "Fill" };
        RectTransform ovRoot; Canvas ovCanvas;
        static Image Img(Transform p, string n) { var t = p ? p.Find(n) : null; return t ? t.GetComponent<Image>() : null; }
        static bool Shown(Image im) => im && im.gameObject.activeInHierarchy && im.enabled;                // the kit's contract: off at or under Hidden (any alpha left on counts as drawn)
        /// <summary>bars = marks with a bar image shown; fillA = the highest alpha of a shown Fill (-1 = none); the hovered mark (its name chip on:
        /// nothing selected, so only the hovered mark has one): its place and size (the brackets), its chip's place, its grenade icon.</summary>
        Seen Look()
        {
            var s = new Seen { fillA = -1f };
            if (!ovRoot && hud) { ovRoot = hud.transform.Find("OpenZ_UnitOverlay/Root") as RectTransform; ovCanvas = ovRoot ? ovRoot.GetComponentInParent<Canvas>() : null; }
            if (!ovRoot || !ovCanvas || !ovCanvas.enabled) return s;
            for (int i = 0; i < ovRoot.childCount; i++)
            {
                var mk = ovRoot.GetChild(i) as RectTransform;
                if (!mk || mk.name != "Mark" || !mk.gameObject.activeInHierarchy) continue;
                var bar = mk.Find("Bar"); bool any = false;
                foreach (var nm in BarParts) { var im = Img(bar, nm); if (Shown(im)) { any = true; if (nm == "Fill") s.fillA = Mathf.Max(s.fillA, im.color.a); } }
                if (any) s.bars++;
                var chip = mk.Find("Label") as RectTransform;
                if (!s.hov && chip && chip.gameObject.activeInHierarchy)
                {
                    s.hov = true; s.pos = mk.anchoredPosition; s.size = mk.sizeDelta; s.chip = chip.anchoredPosition;
                    var nd = Img(bar, "Nade"); s.nade = Shown(nd); if (s.nade) s.nadeAt = nd.rectTransform.position;
                }
            }
            return s;
        }

        // ---- the proof mouse (View/OzBlueRepairProof.cs): the game gets its events whatever window has the focus
        static bool routed; static InputSettings.EditorInputBehaviorInPlayMode route0; static InputSettings.BackgroundBehavior back0;
        static void RouteOn()
        {
            if (routed) return; routed = true;
            var s = InputSystem.settings; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior;
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        static void RouteOff()
        {
            if (!routed) return; routed = false;
            var s = InputSystem.settings; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0;
        }
        void DevicesOn()
        {
            RouteOn();
            realMouse = Mouse.current; proofMouse = InputSystem.AddDevice<Mouse>("OzProofMouse");
            if (realMouse != null && realMouse != proofMouse) InputSystem.DisableDevice(realMouse);
            proofMouse.MakeCurrent();
            MouseAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f));
        }
        static void DevicesOff()
        {
            if (realMouse != null) { InputSystem.EnableDevice(realMouse); realMouse.MakeCurrent(); realMouse = null; }
            if (proofMouse != null && proofMouse.added) InputSystem.RemoveDevice(proofMouse);
            proofMouse = null;
            RouteOff();
        }
        static void MouseAt(Vector2 p) { if (proofMouse != null) InputSystem.QueueStateEvent(proofMouse, new MouseState { position = p }); }

        void Start() { StartCoroutine(All()); }

        /// <summary>One sampled stretch: every frame for `seconds` (real time) with the pointer kept on `e`; t from `t0`.</summary>
        sealed class Curve
        {
            public readonly List<float> t = new List<float>(), k = new List<float>(), drawn = new List<float>(), dt = new List<float>();
            public readonly List<bool> on = new List<bool>(), owns = new List<bool>(); public readonly List<double> wt = new List<double>(); public readonly List<Seen> seen = new List<Seen>();
        }
        IEnumerator Sample(Curve c, ZObject e, float t0, float seconds, System.Action each = null)
        {
            while (Time.unscaledTime - t0 < seconds)
            {
                each?.Invoke();
                if (e != null && !e.Destroyed) MouseAt(ScreenOf(e));
                yield return null; yield return new WaitForEndOfFrame();                                   // after this frame's overlay: its factor, its time step and its images
                c.t.Add(Time.unscaledTime - t0); c.k.Add(OzRadarBars.Factor); c.dt.Add(Time.unscaledDeltaTime); c.owns.Add(OzRadarBars.Owns); c.wt.Add(W.Time);
                bool on = OnEnemy(e) && OpenZUnitOverlay.HoverRadarId >= 0; c.on.Add(on); c.drawn.Add(on ? OpenZUnitOverlay.HoverRadarAlpha : -1f);
                c.seen.Add(Look());
            }
        }
        string Rows(Curve c)
        {
            var s = new StringBuilder();
            for (int i = 0; i < c.t.Count; i++)
                s.Append($"{c.t[i]:0.000} s (game {c.wt[i]:0.00}): factor {c.k[i]:0.000}, radar {(c.owns[i] ? 1 : 0)}, hover bar {(c.on[i] ? c.drawn[i].ToString("0.000") : "-")}, bars drawn {c.seen[i].bars}, fill alpha {c.seen[i].fillA:0.000}; ");
            return s.ToString();
        }
        /// <summary>The hovered mark's brackets, chip and grenade icon over a stretch: the largest move from the first sample (canvas units / pixels).</summary>
        static float Moves(Curve c, out int n, out int nades)
        {
            float worst = 0f; n = 0; nades = 0; Seen first = default; bool have = false; Vector3 nade0 = default; bool haveNade = false;
            foreach (var s in c.seen)
            {
                if (!s.hov) continue;
                n++; if (s.nade) nades++;
                if (!have) { first = s; have = true; }
                worst = Mathf.Max(worst, (s.pos - first.pos).magnitude, (s.size - first.size).magnitude, (s.chip - first.chip).magnitude);
                if (s.nade) { if (!haveNade) { nade0 = s.nadeAt; haveNade = true; } else worst = Mathf.Max(worst, (s.nadeAt - nade0).magnitude); }
            }
            return worst;
        }
        /// <summary>A change followed after the hold, then the fade: before the frame the rule followed it nothing moves (factor and images as
        /// they were), the game time of the hold, then monotonic, done within the fade + a frame, every step inside the curve's bound, the hover bar's
        /// alpha and the drawn Fill image's alpha = the factor, no bar image at factor 0.</summary>
        void Fade(string id, string what, Curve c, bool up, double changeAt)
        {
            int ci = c.owns.FindIndex(x => x == up);
            float startK = up ? 0f : 1f;
            bool found = ci > 0;
            bool flat = found && Enumerable.Range(0, ci).All(i => c.k[i] == startK && (up ? c.seen[i].bars == 0 : !c.on[i] || c.seen[i].bars > 0));   // held: no bar anywhere / the hovered bar still drawn
            double hold = found ? c.wt[ci] - changeAt : double.NaN;
            bool mono = true, steps = true, same = true, img = true; float done = float.NaN, worst = 0f;
            for (int i = found ? ci : c.k.Count; i < c.k.Count; i++)
            {
                float prev = i == ci ? startK : c.k[i - 1], d = c.k[i] - prev;
                if (up ? d < -1e-5f : d > 1e-5f) mono = false;
                if (Mathf.Abs(d) > Bound(c.dt[i])) steps = false;
                worst = Mathf.Max(worst, Mathf.Abs(d));
                if (float.IsNaN(done) && (up ? c.k[i] >= 1f : c.k[i] <= 0f)) done = c.t[i] - c.t[ci - 1];
                if (c.on[i] && Mathf.Abs(c.drawn[i] - (c.k[i] > OzRadarBars.Hidden ? c.k[i] : 0f)) > 0.01f) same = false;   // the hover bar: alpha 1 x the factor, 0 when held back
                var s = c.seen[i];
                if (c.k[i] <= OzRadarBars.Hidden) { if (s.bars != 0) img = false; }
                else if (c.on[i] && (s.fillA < 0f || Mathf.Abs(s.fillA - c.k[i]) > 0.01f)) img = false;
            }
            float frame = c.dt.Count > 0 ? c.dt.Max() : 0.02f, fade = OzRadarBars.FadeSeconds;
            Check($"{id} {what}: nothing moved for {hold:0.00} s of game time (the {OzRadarBarsRule.HoldSec:0.0} s hold; factor and bar images as before on all {ci} frames: {flat}), then {(up ? "in" : "out")} in {done:0.000} s "
                  + $"(the fade {fade:0.00} s, the longest frame {frame:0.000} s), monotonic {mono}, every step within the curve's bound {steps} (largest {worst:0.000}), the hover bar drawn with the factor {same}, "
                  + $"the drawn bar images' alpha = the factor and none at 0 {img} ({c.on.Count(x => x)} of {c.on.Count} frames on the enemy)",
                  found && flat && hold >= OzRadarBarsRule.HoldSec - 0.02 && hold <= OzRadarBarsRule.HoldSec + 0.25 && mono && steps && same && img
                  && !float.IsNaN(done) && done <= fade + frame + 0.02f && done >= fade - frame - 0.02f && c.on.Count(x => x) >= c.on.Count / 2);
        }
        /// <summary>wantNade: the enemy carries grenades, so its grenade icon must be drawn on every frame, the bar shown or not.</summary>
        void Still(string id, Curve c, bool wantNade)
        {
            float mv = Moves(c, out int n, out int nades);
            Check($"{id} nothing else moves: the hovered mark's brackets, name chip{(wantNade ? " and grenade icon" : "")} on {n} frames from factor {c.k.FirstOrDefault():0.0} to {c.k.LastOrDefault():0.0}: largest move {mv:0.00} (grenade icon drawn on {nades} of them)",
                  n >= c.seen.Count / 2 && mv <= 0.5f && (!wantNade || nades == n));
        }

        // ---- the garbage (round 2), by the engine's own "GC Allocated In Frame" (the counter View/OzPerfLine.cs reads for OzPerfNumbers)
        const int AskCalls = 5000;
        ProfilerRecorder gcRec; bool gcRecOn;
        /// <summary>frames frames with the pointer on e (each starting with the job `each`, if any), each frame's garbage into `into`; the block's
        /// first two frames are left out (the counter may still show the frame before the block).</summary>
        IEnumerator GcBlock(List<long> into, ZObject e, int frames, System.Action each = null)
        {
            for (int i = 0; i < frames; i++)
            {
                if (e != null && !e.Destroyed) MouseAt(ScreenOf(e));
                each?.Invoke();
                yield return null;
                if (i >= 2 && gcRec.Valid) into.Add(gcRec.LastValue);
            }
        }
        static long Median(List<long> v) { if (v.Count == 0) return -1; var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
        /// <summary>The middle half's width (the interquartile range): the frames' own noise.</summary>
        static long Spread(List<long> v) { if (v.Count < 4) return 0; var s = v.OrderBy(x => x).ToList(); return s[s.Count * 3 / 4] - s[s.Count / 4]; }
        /// <summary>The rule's per-frame questions n times: the switches (OzRadarBars.WhyOffNow), the radar truth, the hold and the fade (on r, a
        /// state apart, so the game's own fade is not moved), what the overlay and the unit panel ask (BarAlpha, Hides, BarOff, the label). The
        /// sum only keeps the work from being dropped.</summary>
        float Questions(OzRadarBarsRule r, ZObject e, ZObject mine, int n)
        {
            float s = 0f; double t = W.Time;
            for (int i = 0; i < n; i++)
            {
                string why = OzRadarBars.WhyOffNow(W, me, false, false);
                r.Advance(why, why == null && OzStrikeWarning.OwnsRadar(W, me), me, 1f / 60f, OzRadarBars.FadeSeconds, false, t + i * 0.01);
                s += OzRadarBars.BarAlpha(e) + OzRadarBars.BarAlpha(mine) + r.Alpha(e.Owner) + (OzRadarBars.Hides(e) ? 1f : 0f) + (OzRadarBars.BarOff(e) ? 1f : 0f)
                   + OzRadarBarsRule.PanelLabel(r.BarOff(e.Owner)).Length;
            }
            return s;
        }

        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; rts = FindFirstObjectByType<RtsCamera>(); hud = OpenZHud.I; ov = hud ? hud.Overlay : null;
            dir = Folder(); Directory.CreateDirectory(dir);
            if (W == null || !hud || !ov || !rts) { Log("no level in Play (start L02, the kit applied)"); fails++; Finish(); yield break; }
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            Log($"1.4 radar_enemy_bars (DV172) Play proof, {g.level}, player {me}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s, fade {OzRadarBars.FadeSeconds:0.00} s after a {OzRadarBarsRule.HoldSec:0.0} s hold, at {Clock()}");
            yield return WaitFor(() => !ZodBriefing.BriefingActive, 120f); yield return Real(0.5f);
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);
            foreach (var b in W.Bots) b.Ignored = true;
            alwaysWas = OpenZUnitOverlay.AlwaysBars; alwaysSet = true; OpenZUnitOverlay.AlwaysBars = false;      // only the hovered bars until A1 (the overlay reads it every 0.5 s; put back at the end)
            if (hud.input) hud.input.Selection.Clear();                                                       // nothing selected: the unit panel stays empty (and its tick never runs ahead of the overlay's)
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            var radars = W.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "radar" && !o.Destroyed).OrderBy(o => o.Id).ToList();
            foreach (var r in radars) if (r.Owner == me) Give(FlagOf(r), foe);
            yield return Real((float)OzRadarBarsRule.HoldSec + 0.6f);                                          // a radar he held: lost after the hold, faded out
            var free = radars.Where(r => !r.Destroyed && FlagOf(r) != null).ToList();

            // the enemy under the pointer: a robot squad leader (grenades: the icon stays without the bar), else a vehicle; not under the fog
            var enemy = W.Objects.Where(o => o.Owner == foe && !o.Destroyed && o.KillAt < 0 && !o.IsMinion && (o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle) && !o.IsFortDefender && !o.IsShuttle
                                             && BoardingView.FigureShown(o) && !OzFogOfWar.Covers(o))
                                 .OrderBy(o => o.Kind == ZKind.Robot ? 0 : 1).ThenBy(o => o.Id).FirstOrDefault();
            var own = W.Objects.FirstOrDefault(o => o.Owner == me && !o.Destroyed && o.KillAt < 0 && !o.IsMinion && o.Kind == ZKind.Robot && BoardingView.FigureShown(o));
            if (enemy != null && enemy.Kind == ZKind.Robot && enemy.Grenades <= 0 && enemy.CanPickupGrenades()) { nadeOf = enemy; enemy.Grenades = 1; }   // the grenade icon must stay without the bar (taken back at the end)
            Check($"P0 the rule acts ({(OzRadarBars.RuleOn ? "on" : "off: " + OzRadarBars.Why)}), the player owns no radar ({!OzRadarBars.Owns}), the factor {OzRadarBars.Factor:0.000}; radars with a zone {free.Count} (2 needed); "
                  + $"an enemy unit ({enemy != null}) and an own robot ({own != null}); the enemy carries grenades ({(enemy != null ? enemy.Grenades : 0)}{(nadeOf != null ? ", one given" : "")})",
                  OzRadarBars.RuleOn && !OzRadarBars.Owns && OzRadarBars.Factor == 0f && free.Count >= 2 && enemy != null && own != null);
            if (!OzRadarBars.RuleOn || free.Count < 2 || enemy == null || own == null) { Finish(); yield break; }
            ZObject rA = free[0], rB = free[1];
            Log($"the enemy: {enemy.Type}#{enemy.Id} ({enemy.Owner}, {enemy.Minions.Count + 1} robots, grenades {enemy.Grenades}, veteran {enemy.VeteranActive}); own: {own.Type}#{own.Id}; radars #{rA.Id} and #{rB.Id}");
            DevicesOn();

            // ---- H1 / H2: no bar on the enemy without a radar, the own bar as before
            Cam(g.CenterWorld(enemy)); yield return Real(0.6f);
            int hidden0 = OzRadarBars.EnemyHidden;
            var h1 = new Curve(); yield return Sample(h1, enemy, Time.unscaledTime, 1.0f);
            int onF = h1.on.Count(x => x), barF = h1.on.Where((x, i) => x && h1.drawn[i] > 0f).Count(), imgF = h1.seen.Count(s => s.bars > 0), hovF = h1.seen.Count(s => s.hov), nadeF = h1.seen.Count(s => s.hov && s.nade), shownF = 0;
            Check($"H1 the pointer on the enemy for 1 s without a radar: under the pointer {onF} of {h1.on.Count} frames, the factor 0 on all ({h1.k.All(x => x == 0f)}), its bar asked with alpha 0 on every one ({barF} drawn), "
                  + $"bar images drawn on {imgF} frames (any mark), its frame and name chip on {hovF}, its grenade icon on {nadeF}; enemy marks held back {OzRadarBars.EnemyHidden - hidden0}",
                  onF >= h1.on.Count * 0.8f && barF == 0 && imgF == 0 && h1.k.All(x => x == 0f) && hovF >= h1.seen.Count * 0.8f && (!(enemy.Kind == ZKind.Robot && enemy.Grenades > 0) || nadeF == hovF) && OzRadarBars.EnemyHidden - hidden0 > 0);
            for (int i = 0; i < 20; i++) { MouseAt(ScreenOf(enemy)); yield return null; if (OpenZUnitOverlay.HoverShownId >= 0) shownF++; }
            Check($"H1b no hover health bar either (the proofs' HoverShownId) on {20 - shownF} of 20 frames", shownF == 0);
            MouseAt(ScreenOf(enemy)); yield return Frames(2);
            yield return Shot("1_enemy_no_radar_no_bar");
            Cam(g.CenterWorld(own)); yield return Real(0.6f);
            int ownOn = 0, ownBar = 0, ownImg = 0;
            for (int i = 0; i < 30; i++)
            {
                MouseAt(ScreenOf(own)); yield return null; yield return new WaitForEndOfFrame();
                var hm = ov.HoverMember; var s = Look();
                if (hm != null && (hm == own || hm.Leader == own) && OpenZUnitOverlay.HoverRadarId >= 0) { ownOn++; if (Mathf.Abs(OpenZUnitOverlay.HoverRadarAlpha - 1f) < 0.004f) ownBar++; if (s.bars > 0 && Mathf.Abs(s.fillA - 1f) < 0.01f) ownImg++; }
            }
            Check($"H2 the pointer on an own robot: under it {ownOn} of 30 frames, its bar at full alpha on {ownBar}, its bar images drawn at alpha 1 on {ownImg}", ownOn >= 20 && ownBar == ownOn && ownImg == ownOn);
            yield return Shot("2_own_bar_as_before");

            // ---- G1 / L1: gained, then ruined (no pictures while it is sampled)
            Cam(g.CenterWorld(enemy)); yield return Real(0.6f);
            for (int i = 0; i < 5; i++) { MouseAt(ScreenOf(enemy)); yield return null; }
            int gained0 = OzRadarBars.Gained, lost0 = OzRadarBars.Lost;
            float t0 = Time.unscaledTime; double w0 = W.Time; Give(FlagOf(rA), me);
            var g1 = new Curve(); yield return Sample(g1, enemy, t0, 1.5f);
            Fade("G1", $"radar #{rA.Id}'s zone given to the player (gained {OzRadarBars.Gained - gained0})", g1, true, w0);
            Still("M1 (G1)", g1, enemy.Kind == ZKind.Robot && enemy.Grenades > 0);
            yield return Shot("3_radar_gained_enemy_bar_in");
            for (int i = 0; i < 5; i++) { MouseAt(ScreenOf(enemy)); yield return null; }
            t0 = Time.unscaledTime; w0 = W.Time; Ruin(rA);
            var l1 = new Curve(); yield return Sample(l1, enemy, t0, 1.5f);
            Fade("L1", $"radar #{rA.Id} ruined (lost {OzRadarBars.Lost - lost0}, a ruin is no radar)", l1, false, w0);
            Still("M2 (L1)", l1, enemy.Kind == ZKind.Robot && enemy.Grenades > 0);
            yield return Shot("4_radar_ruined_enemy_bar_out");

            // ---- K1: a contested radar flag (Sim/ZWorld.Rules.cs CheckFlagCaptures: the first unit of the other team takes it, every 0.2 s)
            int flips0 = OzRadarBars.Flips, gained1 = OzRadarBars.Gained, lost1 = OzRadarBars.Lost, log1 = OzRadarBars.LogLines; double nextFlip = W.Time; bool mine = false; int owners = 0;
            var k1 = new Curve();
            yield return Sample(k1, enemy, Time.unscaledTime, 3.0f, () => { if (W.Time >= nextFlip) { nextFlip = W.Time + 0.2; mine = !mine; owners++; Give(FlagOf(rB), mine ? me : foe); } });
            if (mine) { Give(FlagOf(rB), foe); owners++; }
            Check($"K1 a contested radar flag: radar #{rB.Id}'s zone changed owner {owners} times in 3 s (every 0.2 s of game time; the truth changed {OzRadarBars.Flips - flips0} times): the factor 0 on all {k1.k.Count} frames ({k1.k.All(x => x == 0f)}), "
                  + $"bar images drawn on {k1.seen.Count(s => s.bars > 0)} frames, changes followed {OzRadarBars.Gained - gained1 + OzRadarBars.Lost - lost1} (no pulse), log lines written {OzRadarBars.LogLines - log1} (no log spam)",
                  owners >= 10 && OzRadarBars.Flips - flips0 >= 10 && k1.k.All(x => x == 0f) && k1.seen.All(s => s.bars == 0) && OzRadarBars.Gained == gained1 && OzRadarBars.Lost == lost1 && OzRadarBars.LogLines == log1);
            yield return Real((float)OzRadarBarsRule.HoldSec + 0.2f);

            // ---- Q1: the cost (no radar: the radar truth walks the whole object list, every level's start)
            var swq = System.Diagnostics.Stopwatch.StartNew(); int yes = 0;
            for (int i = 0; i < 2000; i++) if (OzStrikeWarning.OwnsRadar(W, me)) yes++;
            double us = swq.Elapsed.TotalMilliseconds * 1000.0 / 2000.0;
            ruleWas = OpenZMinimap.RadarRule; ruleSet = true;
            double onMs = double.NaN, offMs = double.NaN;
            gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); gcRecOn = true;    // round 2: the engine's own counter (View/OzPerfLine.cs)
            var gcOn = new List<long>(16); var gcOff = new List<long>(16);                                        // sized once: no list growth inside a counted frame
            for (int pass = 0; pass < 2; pass++)
            {
                OpenZMinimap.RadarRule = pass == 1 ? false : ruleWas;                                          // pass 1: DV33's switch off (every enemy bar shown)
                yield return Real(0.5f);
                OzPerfNumbers.Reset(); int f0 = Time.frameCount;
                var q = new Curve(); yield return Sample(q, enemy, Time.unscaledTime, 2.0f);
                var line = new StringBuilder(); OzPerfNumbers.AppendMinute(line, Mathf.Max(1, Time.frameCount - f0));
                var mm = Regex.Match(line.ToString(), @"overlay ([0-9.]+) ms a frame");
                double ms = mm.Success ? double.Parse(mm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
                if (pass == 0) onMs = ms; else offMs = ms;
                yield return GcBlock(pass == 0 ? gcOn : gcOff, enemy, 14);                                    // Q1g: quiet frames, the pointer on the enemy
            }
            OpenZMinimap.RadarRule = ruleWas; ruleSet = false; OzPerfNumbers.Reset();
            yield return Real(0.5f);
            Check($"Q1 the cost: the radar truth {us:0.00} microseconds a call on this world ({W.Objects.Count} objects, no radar: the whole list; bound 50), asked once a frame by the rule; the overlay's own time "
                  + $"{onMs:0.000} ms a frame with the rule on (the enemy bar held back), {offMs:0.000} ms with DV33's switch off (every bar drawn): the rule costs at most 0.05 ms a frame",
                  yes == 0 && us < 50.0 && !double.IsNaN(onMs) && !double.IsNaN(offMs) && onMs <= offMs + 0.05);

            // ---- Q1g / Q2 (round 2): the garbage, by the engine's own "GC Allocated In Frame" (the headless G1 / W7 read the thread counter, which
            //      Unity's Boehm Mono leaves at 0). Known first: a 64 KB array a frame must show, else both rows are INFO, not PASS.
            var gcPlain = new List<long>(16); var gcKnown = new List<long>(16); var gcAsk = new List<long>(16);
            byte[] gcKeep = null; var askRule = new OzRadarBarsRule(); float askSum = 0f;
            yield return GcBlock(gcPlain, enemy, 12);
            yield return GcBlock(gcKnown, enemy, 12, () => gcKeep = new byte[64 * 1024]);
            yield return GcBlock(gcAsk, enemy, 12, () => askSum += Questions(askRule, enemy, own, AskCalls));
            gcRec.Dispose(); gcRecOn = false; System.GC.KeepAlive(gcKeep);
            long mOn = Median(gcOn), mOff = Median(gcOff), mPlain = Median(gcPlain), mKnown = Median(gcKnown), mAsk = Median(gcAsk), slack = System.Math.Max(64L, Spread(gcOff));
            string gcNums = $"median bytes a frame: rule on {mOn}, DV33 off {mOff} (its spread {Spread(gcOff)}), plain {mPlain}, with a 64 KB array made {mKnown}, the questions x{AskCalls} {mAsk} "
                          + $"({gcOn.Count}/{gcOff.Count}/{gcPlain.Count}/{gcKnown.Count}/{gcAsk.Count} frames; checksum {askSum:0})";
            if (gcKnown.Count < 4 || gcPlain.Count < 4 || mKnown - mPlain < 60 * 1024) Log("INFO Q1g / Q2 not measurable here: the engine's GC Allocated In Frame did not see a 64 KB array a frame (" + gcNums + ")");
            else
            {
                Check($"Q1g the garbage with the rule on (the enemy bar held back) is no more than with DV33's switch off (every bar drawn), quiet frames, the pointer on the enemy: "
                      + $"{mOn - mOff} bytes a frame more (bound {slack}: 64 bytes or the off frames' own spread, the game's and the editor's noise); {gcNums}",
                      gcOn.Count >= 4 && gcOff.Count >= 4 && mOn <= mOff + slack);
                Check($"Q2 the rule's per-frame questions {AskCalls} times in one frame make no garbage: {mAsk - mPlain} bytes a frame more than a plain frame (bound {AskCalls * 16 / 10}: under 1.6 bytes a call, "
                      + $"and an allocation is 16 bytes or more); {gcNums}", gcAsk.Count >= 4 && mAsk - mPlain < AskCalls * 16 / 10);
            }

            // ---- Z1: the pause menu in the middle of a fade (after the hold: the fade itself runs on unscaled time)
            for (int i = 0; i < 5; i++) { MouseAt(ScreenOf(enemy)); yield return null; }
            Give(FlagOf(rB), me);
            yield return WaitFor(() => { MouseAt(ScreenOf(enemy)); return OzRadarBars.Owns; }, 3f);          // the hold (0.6 s of game time); the fade starts on this frame
            float tc = Time.unscaledTime; var z2 = new Curve(); yield return Sample(z2, enemy, tc, 0.1f);
            float kAtPause = OzRadarBars.Factor;
            if (hud.Pause) hud.Pause.Open();
            bool opened = hud.Pause && hud.Pause.IsOpen;
            yield return Sample(z2, enemy, tc, 1.1f);
            float kBeforeClose = OzRadarBars.Factor;
            if (hud.Pause) hud.Pause.Close();
            yield return Sample(z2, enemy, tc, 2.4f);
            bool zSteps = true; float zWorst = 0f;
            for (int i = 1; i < z2.k.Count; i++) { float d = Mathf.Abs(z2.k[i] - z2.k[i - 1]); zWorst = Mathf.Max(zWorst, d); if (d > Bound(z2.dt[i])) zSteps = false; }
            int zOn = z2.on.Skip(z2.on.Count / 2).Count(x => x), zImg = z2.seen.Skip(z2.seen.Count / 2).Count(s => s.bars > 0);
            Check($"Z1 radar #{rB.Id} taken, followed after the hold; the pause menu opened 0.1 s into the fade at factor {kAtPause:0.000} (open {opened}) for 1 s: just before it closed the factor was {kBeforeClose:0.000} "
                  + $"(the fade ran on unscaled time through the pause), every frame's step within the curve's bound {zSteps} (largest {zWorst:0.000}), the factor at the end {OzRadarBars.Factor:0.000}, "
                  + $"the enemy bar drawn again after the HUD came back on {zOn} frames ({zImg} with its images)",
                  opened && zSteps && kAtPause > 0f && kAtPause < 1f && kBeforeClose >= 0.999f && kBeforeClose > kAtPause && OzRadarBars.Factor == 1f && zOn > 0 && zImg > 0);
            Log("   G1 samples: " + Rows(g1)); Log("   L1 samples: " + Rows(l1)); Log("   K1 samples: " + Rows(k1)); Log("   Z1 samples (from the frame the rule followed the radar): " + Rows(z2));

            // ---- A1: HEALTH BARS ON EVERY DAMAGED UNIT
            OpenZUnitOverlay.AlwaysBars = true;                                                                 // (its value before the proof: alwaysWas, put back at the end)
            enemy.SetHealth(Mathf.Max(1, enemy.MaxHealth * 6 / 10)); foreach (var m in enemy.Minions) if (!m.Destroyed) m.SetHealth(Mathf.Max(1, m.MaxHealth * 6 / 10));
            own.SetHealth(Mathf.Max(1, own.MaxHealth * 6 / 10));
            Give(FlagOf(rB), foe);
            Cam(g.CenterWorld(enemy)); yield return Real((float)OzRadarBarsRule.HoldSec + 0.9f);                // the hold, the fade, the option read every 0.5 s
            var empty = ScreenOf(enemy) + new Vector2(0f, Screen.height * 0.22f);
            MouseAt(empty); yield return Frames(4);
            int sk0 = OzRadarBars.SkippedAlways, sh0 = OzRadarBars.EnemyShown, n0 = 0;
            for (int i = 0; i < 30; i++) { MouseAt(empty); yield return null; yield return new WaitForEndOfFrame(); n0 = Mathf.Max(n0, Look().bars); }
            int skipped = OzRadarBars.SkippedAlways - sk0, enemyDrawn = OzRadarBars.EnemyShown - sh0;
            yield return Shot("5_every_damaged_unit_no_radar");
            Give(FlagOf(rB), me); yield return Real((float)OzRadarBarsRule.HoldSec + 0.6f);
            int sh1 = OzRadarBars.EnemyShown, n1 = 0;
            for (int i = 0; i < 30; i++) { MouseAt(empty); yield return null; yield return new WaitForEndOfFrame(); n1 = Mathf.Max(n1, Look().bars); }
            int enemyDrawnRadar = OzRadarBars.EnemyShown - sh1;
            Check($"A1 HEALTH BARS ON EVERY DAMAGED UNIT, the enemy squad damaged, the pointer off it: without a radar enemy marks left out {skipped} times in 30 frames, enemy bars drawn {enemyDrawn}, bar images drawn on {n0} marks; "
                  + $"with the radar enemy bars drawn {enemyDrawnRadar}, bar images on {n1} marks",
                  skipped > 0 && enemyDrawn == 0 && enemyDrawnRadar > 0 && n1 > n0);
            yield return Shot("6_every_damaged_unit_with_radar");
            OpenZUnitOverlay.AlwaysBars = false;

            // ---- C: the real-time stretch for the OBS take (the radar lost, then taken again, the pointer on the enemy; no pictures, no marks)
            Cam(g.CenterWorld(enemy)); yield return Real(1.0f);
            var c0 = new Curve(); float tC = Time.unscaledTime; string at0 = Clock();
            yield return Sample(c0, enemy, tC, 2.0f);
            string at1 = Clock(); float tLost = Time.unscaledTime - tC; Give(FlagOf(rB), foe);
            yield return Sample(c0, enemy, tC, 2.0f + 2.5f);
            string at2 = Clock(); float tBack = Time.unscaledTime - tC; Give(FlagOf(rB), me);
            yield return Sample(c0, enemy, tC, 2.0f + 2.5f + 2.5f);
            string at3 = Clock();
            bool outOk = c0.t.Select((t, i) => (t, i)).Any(x => x.t > tLost && x.t < tBack && c0.k[x.i] == 0f && c0.seen[x.i].bars == 0);
            bool inOk = c0.k.Count > 0 && c0.k[c0.k.Count - 1] == 1f && c0.seen[c0.seen.Count - 1].bars > 0;
            bool cSteps = true; for (int i = 1; i < c0.k.Count; i++) if (Mathf.Abs(c0.k[i] - c0.k[i - 1]) > Bound(c0.dt[i])) cSteps = false;
            float fps = c0.t.Count / Mathf.Max(0.01f, c0.t.Count > 0 ? c0.t[c0.t.Count - 1] : 0.01f);
            Check($"C1 the OBS stretch, real time ({fps:0} frames a second, no picture taken): from {at0}; the radar lost at {at1} (+{tLost:0.00} s): the enemy bar faded out ({outOk}); taken again at {at2} (+{tBack:0.00} s): faded in ({inOk}); "
                  + $"every step within the curve's bound {cSteps}; the stretch ends at {at3}",
                  outOk && inOk && cSteps);
            Log("   C samples: " + Rows(c0));
            Finish();
        }

        void Finish()
        {
            if (alwaysSet) { OpenZUnitOverlay.AlwaysBars = alwaysWas; alwaysSet = false; }
            if (ruleSet) { OpenZMinimap.RadarRule = ruleWas; ruleSet = false; }
            if (nadeOf != null) { nadeOf.Grenades = 0; nadeOf = null; }
            if (gcRecOn) { gcRec.Dispose(); gcRecOn = false; }
            if (W != null)
                for (int i = zones.Count - 1; i >= 0; i--)
                    if (zones[i].Key != null && zones[i].Key.Owner != zones[i].Value) W.AwardZone(zones[i].Key, zones[i].Value);
            DevicesOff();
            Log($"result: {passes} PASS, {fails} FAIL, {shots} pictures in {dir}; ended at {Clock()}");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.WriteAllText(LogPath, "[OpenZ V1.4 radar bars, Play proof] " + Report + "\n" + sb); }
            catch (System.Exception ex) { Debug.LogWarning("[OzRadarBarsProof] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            DevicesOff();
            if (alwaysSet) { OpenZUnitOverlay.AlwaysBars = alwaysWas; alwaysSet = false; }
            if (ruleSet) { OpenZMinimap.RadarRule = ruleWas; ruleSet = false; }
            if (nadeOf != null) { nadeOf.Grenades = 0; nadeOf = null; }
            if (gcRecOn) { gcRec.Dispose(); gcRecOn = false; }
        }
    }
}
#endif
