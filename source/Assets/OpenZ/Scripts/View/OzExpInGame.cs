// no Sim file, no rule, no damage, no DV row; the random picks are this file's own System.Random (OzExpPick), the clock is read from the
// rules (ZWorld.Time) and never written. The seven test explosions of the Unit editor (View/OzUeBlastFx*.cs) in the game:
//  - 45: exp_v2_6 (SHOCK) ON TOP of every building destruction (not bridges), at the building's centre on the ground, scaled to its footprint
//    (the wave reaching about half its long side + 2.5 m; a fort the full size), its flash x BuildingFlash (the building has its own flash);
//    and at the megacannon's BASE on the fort roof when it is blown off (roof height, the wave flat on the roof plane), x MegaScale.
//  - 47.1: at most once per 60 s of game time, the next vehicle / turret death that is ON SCREEN and visible to the player plays a random
//    one of exp_v2_1..4 INSTEAD of the standard blast and its boom (scaled to the unit; the wreck, its fire, the debris tail as today).
//  - 47.2: at most once per 60 s (its own turn), a thrown turret / tank head that is on screen and visible bursts near the top of its flight
//    with exp_v2_5 (FLAK, scaled to its height) and is gone (FLAK's own fragments fall); its landing blast and clang are left out, a small
//    fall of pieces is drawn where the rules' debris lands (the rules' damage there is unchanged).
//  - 46: while one of these explosions lives in a level, the fog pass's own column / cap rule (the one the level 16 ending and the level 18
//    briefings use) is set round it for the game camera: inside its outline the haze runs only to the smoke's front, so the far haze or a far
//    rock behind it no longer cuts its shape out of the smoke. Off in briefings, flyovers and after the game over (those own the rule).
// Never in the Unit editor (its tiles stay as they are). Off-screen events never use up a turn. A log line per explosion ('[OpenZ FX] ...').
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed class OzExpInGame : MonoBehaviour
    {
        public const string Tag = "[OpenZ FX]";
        /// <summary>The switches (all on): off = that use is not made, everything as before it.</summary>
        public static bool BuildingOn = true, MegaOn = true, SwapOn = true, HeadOn = true, FogShape = true;
        /// <summary></summary>
        public const double SwapEvery = 60.0, HeadEvery = 60.0;
        public static float BuildingFlash = 0.6f, MegaFlash = 0.6f, MegaScale = 1f, BuildingMin = 0.4f, SwapMin = 0.5f, SwapMax = 0.9f, SwapPerM = 1f / 6f, HeadMinH = 4f;   // round 3 R3.40 (kit megacannon_big_r3): MegaScale 0.45 -> 1 (the fort's own building scale)
        /// <summary>
        /// With the GEYSER (MegaScale 1: about 14 m over the roof of 7.2 m) exp_v2_1 COLUMN at the same base on the roof plane at
        /// MegaColumnScale (0.9: about 15-18 m over it, the fire column), MegaColumnDelay s of game time later so the fire builds, its flash x
        /// MegaColumnFlash. MegaColumnScale 0 = the GEYSER alone (as R3.4).</summary>
        public static float MegaColumnScale = 0.9f, MegaColumnDelay = 0.15f, MegaColumnFlash = 0.85f;
        /// <summary>
        /// While the player plays (<see cref="SceneNow"/> null) FLAK's (exp_v2_5, the thrown head's
        /// air burst) cloud, heart and wisps live FlakSmokeLife times as long (0.4: the dark cloud gone at about 2.5 s instead of 6.2 s, the last wisp
        /// at about 3.3 s instead of 7.4 s, x sqrt of its scale); the burst itself, its fire, flash, fragments and ground dust as made. Every scene
        /// (the briefings, flyovers, the endings, the films, a scene camera) and the Unit editor keep the long smoke. QuickSmoke off = the long
        /// smoke everywhere, as before this kit. FlakSmokeLife is read when a level starts (the short channels are made for it). Counts: FLAKs
        /// played in a level with the short smoke / with the long one.</summary>
        public static bool QuickSmoke = true;
        public static float FlakSmokeLife = 0.4f;
        public static int QuickSmokes, LongSmokes;
        /// <summary>Proof only (the check menu, the film takes): the next on-screen death / head takes its turn at once.</summary>
        public static bool ForceSwap, ForceHead;
        /// <summary>Counts for the checks.</summary>
        public static int Buildings, Megas, Swaps, Heads, HeadLandings, HeadSounds, Refused, Warms, FogFrames;
        public static readonly int[] SwapsByType = new int[4];
        public static string LastLine = "";
        public static readonly OzExpTurn SwapTurn = new OzExpTurn(SwapEvery), HeadTurn = new OzExpTurn(HeadEvery);

        static OzExpInGame run;
        static ZWorld world;
        static OzExpPick pick;
        static readonly Dictionary<int, float> headGone = new Dictionary<int, float>();

        struct Beat { public OzUeBlastFx fx; public int stage; public Vector3 at; public float due, flatY; }
        readonly List<Beat> beats = new List<Beat>(32);
        float clock, warmIn = -1f;

        // ------------------------------------------------------------------------------------------------ life
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Ensure(); }

        static OzExpInGame Ensure()
        {
            if (!run)
            {
                var go = new GameObject("OZ_ExpInGame");
                DontDestroyOnLoad(go);
                run = go.AddComponent<OzExpInGame>();
            }
            return run;
        }

        /// <summary>In a level (not the Unit editor, not the menu).</summary>
        public static bool InLevel()
        {
            var g = ZGame.I;
            return g && g.World != null && !OzUnitEditor.Active && !OzUnitEditor.IsPlayground(SceneManager.GetActiveScene());
        }

        static void Sync(ZWorld w)
        {
            if (w == world) return;
            world = w; SwapTurn.Reset(); HeadTurn.Reset(); headGone.Clear();
            pick = new OzExpPick(unchecked(System.Environment.TickCount * 31 + 4747));
            if (run) run.warmIn = 0.5f;
        }

        void Update()
        {
            var g = ZGame.I;
            clock += g && g.paused ? 0f : Time.deltaTime;                                                 // a paused field holds the beats, as the explosion's own clock
            for (int i = 0; i < beats.Count; i++)
            {
                var b = beats[i];
                if (!b.fx) { beats.RemoveAt(i--); continue; }
                if (clock < b.due) continue;
                beats.RemoveAt(i--); Fire(b);
            }
            if (g && g.World != null) Sync(g.World);
            if (warmIn >= 0f && InLevel())
            {   // the channels, flipbooks and shader made once per level, at its start (never in the frame of the first explosion)
                warmIn -= Time.unscaledDeltaTime;
                if (warmIn < 0f)
                {
                    var cam = Camera.main; var z = OpenZFxPlayer.I;
                    if (cam && z)
                    {
                        float t0 = Time.realtimeSinceStartup;
                        OzUeBlastFx.Warm(cam.transform.position + cam.transform.forward * 12f);
                        if (QuickSmoke && FlakSmokeLife < 1f) OzUeBlastFx.WarmQuick(cam.transform.position + cam.transform.forward * 12f, FlakSmokeLife);   // 1.5 A38 (kit air_burst_smoke_fade_r11)
                        Warms++;
                        Debug.Log($"{Tag} the seven explosions' channels made for this level ({(Time.realtimeSinceStartup - t0) * 1000f:0} ms)");
                    }
                    else warmIn = 0.5f;
                }
            }
            if (headGone.Count > 0 && Time.frameCount % 120 == 0)
            {   // a head's landing is waited for at most 90 s
                float now = Time.unscaledTime; List<int> old = null;
                foreach (var kv in headGone) if (now - kv.Value > 90f) (old ??= new List<int>()).Add(kv.Key);
                if (old != null) foreach (var id in old) headGone.Remove(id);
            }
        }

        void LateUpdate() { FogPick(); }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += FogBegin; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= FogBegin; }

        // ------------------------------------------------------------------------------------------------ playing one
        void Fire(Beat b)
        {
            var z = OpenZFxPlayer.I;
            if (float.IsNaN(b.flatY) || !z) { b.fx.Beat(b.stage, b.at); return; }
            z.UeFlatY = b.flatY;
            try { b.fx.Beat(b.stage, b.at); }
            finally { z.UeFlatY = float.NaN; }
        }

        /// <summary>Plays explosion `type` (OzUeBlasts.Column .. Airburst) with its origin at O, k times its size, its flash x flash, the rules'
        /// beats drawn at their own times (x sqrt k) and places, no damage. flatY: the ground of its beats (NaN = the real ground).</summary>
        /// <param name="delay">round 3 R3.40: its first beat this many s of game time later (0 = now). It is made now, so it is never refused later.</param>
        public static bool Play(int type, Vector3 O, float k, float flash, float flatY = float.NaN, float delay = 0f)
        {
            var r = Ensure();
            pick ??= new OzExpPick(4747);
            int turn = pick.Raw(12), seed = pick.Raw(int.MaxValue);
            var st = OzUeBlasts.StagesOf(type, turn);
            float smoke = SmokeLife(type);                                                                 // 1.5 A38 (kit air_burst_smoke_fade_r11): FLAK's short smoke while the player plays
            var fx = OzUeBlastFx.Begin(type, O, seed, st, k, flash, smoke);
            if (!fx) { Refused++; return false; }
            if (type == OzUeBlasts.Flak)
            {
                bool quick = fx.SmokeLife < 1f;
                if (quick) QuickSmokes++; else LongSmokes++;
                string why = quick ? "short, life x" + fx.SmokeLife.ToString("0.00") + " (the player plays)" : !QuickSmoke ? "long (QuickSmoke off)"
                           : smoke < 1f ? "long (the short channels could not be made)" : "long (" + (SceneNow() ?? "FlakSmokeLife " + FlakSmokeLife.ToString("0.00")) + ")";
                Debug.Log($"{Tag} exp_v2_5 smoke: {why}");
            }
            float sk = Mathf.Sqrt(k);
            for (int i = 0; i < st.Length; i++)
            {
                var b = new Beat { fx = fx, stage = i, at = O + new Vector3(st[i].dx / 8f, 0f, -st[i].dy / 8f), flatY = flatY,
                                   due = r.clock + Mathf.Max(0f, delay) + (float)((st[i].tick - 1) * ZConst.TICK) * sk };
                if (b.due <= r.clock) r.Fire(b); else r.beats.Add(b);
            }
            return true;
        }

        /// <summary>1.5 A38 (kit air_burst_smoke_fade_r11): the life of explosion `type`'s smoke (x, 1 = as made): FlakSmokeLife for FLAK while the
        /// player plays and QuickSmoke is on; 1 for every other explosion and moment.</summary>
        public static float SmokeLife(int type) => QuickSmoke && type == OzUeBlasts.Flak && FlakSmokeLife > 0f && FlakSmokeLife < 1f && PlayerPlays() ? FlakSmokeLife : 1f;

        /// <summary>1.5 A38: why the player does not play now (null = he plays: a level, the normal RTS camera his, the rules running). The fog of
        /// war's one gate (View/OzFowGate.cs Decide on View/OzFogOfWar.Gate.cs GateInputsNow), read whether the fog is on or off in OPTIONS: a
        /// briefing (red or blue), a flyover, the ending (the glide, the strike, the nuke, the outro scenes, the end screen), a scene's window, a
        /// flyby recording, a capture camera, a scene camera (the RTS camera off or gone, the screen taken), the rules waiting for a scene (the
        /// briefings, the chase film), the computer playing the player's side (demo); "no level" in the menu and the Unit editor.</summary>
        public static string SceneNow()
        {
            var f = OzFogOfWar.I;
            if (!f || !InLevel()) return "no level";
            return OzFowGate.Decide(f.GateInputsNow(), out _);
        }
        public static bool PlayerPlays() => SceneNow() == null;

        static void Say(string s) { LastLine = Tag + " " + s; Debug.Log(LastLine); }
        static double Now() { var g = ZGame.I; return g && g.World != null ? g.World.Time : 0.0; }
        static float GroundAt(Vector3 p) { var z = OpenZFxPlayer.I; var g = ZGame.I; return z ? z.GroundAt(p) : g ? g.GroundY(p.x, p.z) : p.y; }

        /// <summary>The point is inside the game camera's picture (in front of it, 2 % in from every edge).</summary>
        public static bool OnScreen(Vector3 p)
        {
            var cam = Camera.main; if (!cam) return false;
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
        }

        static bool Seen(ZObject o) => o == null || !OzFogOfWar.Covers(o);

        // ------------------------------------------------------------------------------------------------ 45: buildings, the megacannon
        /// <summary>LegacyMatch.BuildingDeath: exp_v2_6 on top of every building's own destruction (not a bridge).</summary>
        public static void BuildingDied(ZObject o, Vector3 p)
        {
            if (!BuildingOn || o == null || o.IsBridge || !InLevel()) return;
            var c = ZGame.PxToWorld(o.LocX + o.WidthPix * 0.5, o.LocY + o.HeightPix * 0.5); c.y = GroundAt(c);
            float half = Mathf.Max(o.WidthPix, o.HeightPix) / 16f;                                        // px -> m, half the long side
            float k = o.IsFort ? 1f : Mathf.Clamp((half + 2.5f) / OzUeBlastFx.WaveM, BuildingMin, 1f);
            if (!Play(OzUeBlasts.Shock, c, k, BuildingFlash)) return;
            Buildings++;
            Say($"building: exp_v2_6 at t={Now():0.0} s on {o.Type} #{o.Id} ({c.x:0.0}, {c.y:0.0}, {c.z:0.0}), scale {k:0.00}, flash x{BuildingFlash:0.00}");
        }

        /// <summary>Checks: megacannons that also played exp_v2_1 COLUMN (round 3 R3.40).</summary>
        public static int MegaColumns;

        /// <summary>FortGunView.StartToss: the megacannon is blown off its fort: exp_v2_4 (GEYSER) at its base on the roof at MegaScale and (round 3
        /// R3.40, kit megacannon_big_r3) exp_v2_1 (COLUMN) at the same base at MegaColumnScale, MegaColumnDelay s later; the beats of both on the roof
        /// plane, each with its own sounds (round 3 R3.4: replaces exp_v2_6 + the old big blast there). The COLUMN is made in this call (never
        /// refused later), its beats wait on this file's clock. True = the GEYSER plays (the caller then leaves its old blasts out).</summary>
        public static bool MegacannonOff(ZObject fort, Vector3 baseAt)
        {
            if (!MegaOn || !InLevel()) return false;
            if (!Play(OzUeBlasts.Geyser, baseAt, MegaScale, MegaFlash, baseAt.y)) return false;
            Megas++;
            bool col = MegaColumnScale > 0f && Play(OzUeBlasts.Column, baseAt, MegaColumnScale, MegaColumnFlash, baseAt.y, MegaColumnDelay);
            if (col) MegaColumns++;
            Say($"megacannon: exp_v2_4 at t={Now():0.0} s at its base on fort #{(fort != null ? fort.Id : -1)} ({baseAt.x:0.0}, {baseAt.y:0.0}, {baseAt.z:0.0}), roof {baseAt.y - GroundAt(baseAt):0.0} m over the ground, scale {MegaScale:0.00}"
                + (col ? $"; exp_v2_1 +{MegaColumnDelay:0.00} s, scale {MegaColumnScale:0.00}, flash x{MegaColumnFlash:0.00}" : MegaColumnScale > 0f ? "; exp_v2_1 refused" : ""));
            return true;
        }

        /// <summary>Round 3 R3.27: the smallest vehicles and turrets never get a new explosion (jeep, light tank, gatling, light gun).</summary>
        public static bool TooSmallForSwap(ZObject o) => o != null && TooSmallType(o.Type);
        public static bool TooSmallType(string t) => t == "jeep" || t == "light" || t == "gatling" || t == "gun";
        /// <summary>Round 3 R3.28: the heavy tank, the Bizon and the Mammoth get one of exp_v2_1..4 at EVERY death, outside the once-a-minute turn.</summary>
        public static bool AlwaysSwap(ZObject o) => o != null && AlwaysType(o.Type);
        public static bool AlwaysType(string t) => t == "heavy" || t == ZBigTanks.Mammoth || t == ZBigTanks.Bizon;
        public static int BigAlways, SmallKept;

        // ------------------------------------------------------------------------------------------------ 47.1: the swapped death blast
        /// <summary>LegacyMatch.UnitDeath: true = this vehicle / turret death plays a new explosion INSTEAD of the standard blast and boom.</summary>
        public static bool SwapDeath(ZObject o, Vector3 p)
        {
            if (!SwapOn || o == null || o.IsShuttle || (o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) || !InLevel()) return false;
            Sync(ZGame.I.World);
            double now = Now();
            if (TooSmallForSwap(o)) { SmallKept++; return false; }                                         // R3.27: its standard blast, no turn used
            bool always = AlwaysSwap(o);                                                                   // R3.28: every death, on or off screen, no turn used
            if (!always && !SwapTurn.Offer(now, OnScreen(p), Seen(o), ForceSwap)) return false;
            float k = Mathf.Clamp(Mathf.Max(o.WidthPix, o.HeightPix) / 8f * SwapPerM, SwapMin, SwapMax);
            int type = pick.Next(4);                                                                       // exp_v2_1..4: COLUMN, FUEL, CHAIN, GEYSER
            var O = new Vector3(p.x, GroundAt(p), p.z);
            if (!Play(type, O, k, 1f)) return false;                                                       // refused: the standard blast plays, the turn waits
            if (always) BigAlways++; else { SwapTurn.Take(now); ForceSwap = false; }
            Swaps++; SwapsByType[type]++;
            Say($"swap: exp_v2_{type + 1} at t={now:0.0} s for {o.Type} #{o.Id} ({O.x:0.0}, {O.z:0.0}), scale {k:0.00}{(always ? " (big tank: every death)" : "")}");
            return true;
        }

        // ------------------------------------------------------------------------------------------------ 47.2: the head that bursts in the air
        /// <summary>UnitView.UpdateToss, once per thrown head just after the top of its arc: true = it bursts now with exp_v2_5 and is gone
        /// (this hides it; the caller ends its flight).</summary>
        public static bool HeadApex(ZObject o, Transform head, Vector3 p)
        {
            if (!HeadOn || o == null || !head || !InLevel()) return false;
            Sync(ZGame.I.World);
            float h = p.y - GroundAt(p);
            if (h < HeadMinH && !ForceHead) return false;                                                  // a low throw: it drops as before, the turn waits
            double now = Now();
            if (!HeadTurn.Offer(now, OnScreen(p), Seen(o), ForceHead)) return false;
            float k = Mathf.Clamp(h / 9f, 0.45f, 1f);                                                       // FLAK bursts 9 m over its origin: at the head
            var O = p - Vector3.up * (9f * k);
            if (!Play(OzUeBlasts.Flak, O, k, 1f)) return false;
            head.gameObject.SetActive(false);
            headGone[o.Id] = Time.unscaledTime;
            HeadTurn.Take(now); ForceHead = false; Heads++;
            Say($"head burst: exp_v2_5 at t={now:0.0} s, the head of {o.Type} #{o.Id} {h:0.0} m up ({p.x:0.0}, {p.z:0.0}), scale {k:0.00}");
            return true;
        }

        /// <summary>LegacyMatch.MissileBlast, the rules' turret debris lands: true = that head burst in the air; a few small pieces fall here
        /// instead of the turret's landing blast (the rules' damage is unchanged).</summary>
        public static bool HeadLanding(int unitId, Vector3 at)
        {
            if (headGone.Count == 0 || !headGone.ContainsKey(unitId)) return false;
            var z = OpenZFxPlayer.I;
            if (z)
            {
                z.Dust(at, 0.35f);
                for (int i = 0; i < 4; i++) z.EmitChannel("m12_stone", at + Vector3.up * 0.4f, new Vector3((i & 1) * 2 - 1, 2.2f + i * 0.4f, (i & 2) - 1), 1.2f, 0.18f, Color.white, 1f);
            }
            var ad = AudioDirector.I; if (ad) ad.Play("dst_debris_metal", at, -1, 0.55f, 1.15f);
            HeadLandings++;
            return true;
        }

        /// <summary>AudioDirector.OnExploded: the turret crash sound of a head that burst in the air is left out.</summary>
        public static bool HeadGoneEvent(ZEvent e)
        {
            if (headGone.Count == 0 || !e.flag2 || !headGone.ContainsKey(e.objId)) return false;
            HeadSounds++;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ 46: the fog pass round the explosion
        static readonly int IdColumn = Shader.PropertyToID("_AtmoColumn"), IdColumnTop = Shader.PropertyToID("_AtmoColumnTop"), IdCap = Shader.PropertyToID("_AtmoColumnCap");
        // per type (scale 1, m): stem radius, stem height, cap height over the origin, cap radius (0 = none)
        static readonly float[,] Shape =
        {
            { 6f, 20f, 20f, 7f },       // COLUMN
            { 4.5f, 10f, 12.5f, 7f },   // FUEL
            { 9f, 7f, 0f, 0f },         // CHAIN
            { 6f, 14f, 0f, 0f },        // GEYSER
            { 0f, 0f, 9f, 6f },         // FLAK
            { 6f, 12f, 0f, 0f },        // SHOCK (the head out of its middle)
            { 0f, 0f, 9f, 5f },         // BURST (white air burst)
        };
        bool fogWant, fogSet; Camera fogCam; Vector4 colV, capV; float colTop;
        /// <summary>Checks: the type and origin of the explosion the fog rule is set round now (-1 = none).</summary>
        public static int FogType = -1; public static Vector3 FogAt;

        /// <summary>Round 3 R3.40: the top of an explosion's fog shape (m over its origin, at its scale).</summary>
        static float ShapeTop(OzUeBlastFx fx) => Mathf.Max(Shape[fx.Type, 1], Shape[fx.Type, 2] + Shape[fx.Type, 3]) * fx.Scale;

        void FogPick()
        {
            fogWant = false; FogType = -1;
            if (!FogShape || OzUeBlastFx.All.Count == 0 || !InLevel()) return;
            var g = ZGame.I;
            if (g.World.GameOver || ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive || (ZodFlyover.Live != null && ZodFlyover.Live.Active)) return;
            var rts = FindFirstObjectByType<RtsCamera>(); fogCam = rts ? rts.GetComponent<Camera>() : Camera.main;
            if (!fogCam) return;
            OzUeBlastFx best = null; float bestD = float.MaxValue; var cp = fogCam.transform.position;
            foreach (var fx in OzUeBlastFx.All)
            {
                if (!fx || fx.Type < 0 || fx.Type >= Shape.GetLength(0)) continue;
                float d = (fx.Origin - cp).sqrMagnitude;
                bool same = best && Mathf.Abs(d - bestD) <= 1f;                                               // round 3 R3.40: two at one place (the megacannon's GEYSER + COLUMN): the taller shape
                if (same ? ShapeTop(fx) > ShapeTop(best) : d < bestD) { bestD = d; best = fx; }
            }
            if (!best) return;
            int ty = best.Type; float k = best.Scale; var O = best.Origin;
            colV = Shape[ty, 0] > 0f ? new Vector4(O.x, O.y, O.z, Shape[ty, 0] * k) : Vector4.zero;
            colTop = Shape[ty, 1] * k;
            capV = Shape[ty, 3] > 0f ? new Vector4(O.x, O.y + Shape[ty, 2] * k, O.z, Shape[ty, 3] * k) : Vector4.zero;
            fogWant = true; FogType = ty; FogAt = O; FogFrames++;
        }

        void FogBegin(ScriptableRenderContext ctx, Camera cam)
        {
            if (!fogWant && !fogSet) return;
            bool mine = fogWant && cam == fogCam;
            Shader.SetGlobalVector(IdColumn, mine ? colV : Vector4.zero);
            Shader.SetGlobalFloat(IdColumnTop, mine ? colTop : 0f);
            Shader.SetGlobalVector(IdCap, mine ? capV : Vector4.zero);
            fogSet = fogWant;
        }

        /// <summary>One line of every count, for the checks.</summary>
        public static string Report() =>
            $"{Tag} buildings {Buildings}, megacannons {Megas} (with exp_v2_1 {MegaColumns}), swaps {Swaps} (column {SwapsByType[0]}, fuel {SwapsByType[1]}, chain {SwapsByType[2]}, geyser {SwapsByType[3]}; " +
            $"offered not due {SwapTurn.NotDue}, off screen {SwapTurn.OffScreen}, under fog {SwapTurn.Fogged}), heads {Heads} (not due {HeadTurn.NotDue}, off screen {HeadTurn.OffScreen}, " +
            $"under fog {HeadTurn.Fogged}; landings drawn as pieces {HeadLandings}, crash sounds left out {HeadSounds}), refused {Refused}, warmed {Warms}, fog rule frames {FogFrames}";
    }
}
