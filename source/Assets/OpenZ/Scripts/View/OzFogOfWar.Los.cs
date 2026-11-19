// OpenZ (GPL-3.0) - version 1.5 kit los_fog: MAYHEM's line-of-sight fog. Explored ground nothing of the player's side sees now is drawn a little
// darker and the enemy units there are hidden; buildings, guns, flags, crates, wrecks and the ground stay. View only.
using System.Collections.Generic;
using System.Diagnostics;
using OpenZ.Sim;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// <list type="bullet">
    /// <item>The "seen now" map (<see cref="OzFowSeen"/>, the reveal map's grid): made again about every <see cref="LosRoundSeconds"/> (the
    /// round's stamps spread evenly over its frames, no spike) from the sight circles of the player's side as they are NOW (its units, buildings
    /// and held flags with the reveal's own <see cref="SightPx"/>, and every standing radar of ours with its radar circle at the radius it has
    /// now, so a radar gained opens it growing smoothly, View/OzFowRadar.cs); a cell no longer seen keeps its value for 0.3 s
    /// (<see cref="OzFowSeen.HoldRounds"/>: a unit turning or shuffling leaves no breathing edge), then every cell eases both ways: up over
    /// <see cref="LosUpSeconds"/>, down over <see cref="LosDownSeconds"/> (the line-of-sight fog comes back over about a second after a unit died
    /// or left; no flicker at a moving unit's edge: a cell only moves toward its goal). Uploaded as a second R8 texture (_OzFowSeen) on the frames
    /// it changed.</item>
    /// <item>The look (Resources/OpenZ/FX/OzFogOfWar.shader, pass Sheets, the same full-screen pass as the cloud: no new pass): the scene under
    /// a pixel whose ground is not seen now keeps <see cref="LosKeepDay"/> of its light by day (<see cref="LosKeepNight"/> at night, so the
    /// night stays readable) plus a faint grey veil (<see cref="LosVeil"/>, a touch of desaturation), eased by a smootherstep of the seen value
    /// (soft edges, a soft fade in time). Explored ground only shows it; the cloud still covers ground never explored.</item>
    /// <item>What hides there: units of the other side that can move (robots, vehicles, the shuttle) whose centre stands on explored ground not
    /// seen now, with everything that reads <see cref="Covers"/> (the health bars, hover frames, squad frames, badges, clicks and attack orders,
    /// minimap dots, the croc alert) and their models, shadows and real headlights on the player's camera (held off while it draws, given back
    /// right after it). With a threshold and a gap (hidden under <see cref="LosHideBelow"/>, shown again from <see cref="LosShowAt"/>), so a unit
    /// at the border never blinks. What stays: the ground, decoration, every building, ground guns, flags with their colours, crates, wrecks
    /// and empty (neutral) vehicles; units in ground never explored keep the cloud's own rule (the early band of mayhem_headstart). The player's
    /// own units are never hidden.</item>
    /// <item>Only when the fog runs (MAYHEM, a human side, not the Unit editor, never in briefings, flyovers, endings: the fog's own gate,
    /// <see cref="Hiding"/>, <see cref="HoldingOff"/>, the pass only on the player's camera). Fog off: nothing of it runs. The rules and the
    /// computer never read it.</item>
    /// </list>
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        /// <summary>A newly seen cell clears over this long (s); a cell no longer seen darkens over <see cref="LosDownSeconds"/> after its 0.3 s
        /// hold (<see cref="OzFowSeen.HoldRounds"/>): about 1.1 s from a death to the fog fully back.</summary>
        public const float LosUpSeconds = 0.4f, LosDownSeconds = 0.8f;
        /// <summary>The seen map is made again about this often (s): 20 times a second at any frame rate, each round's stamps spread over its frames.</summary>
        public const float LosRoundSeconds = 0.05f;
        /// <summary>A unit hidden by the line-of-sight fog shows again when the seen value under its centre reaches this (of 255) ...</summary>
        public const byte LosShowAt = 128;
        /// <summary>... and a shown one hides when it falls under this: the gap keeps a unit at the border from blinking.</summary>
        public const byte LosHideBelow = 77;
        /// <summary>The scene's light kept in the line-of-sight fog (linear light, before the tone curve): by day, at night.</summary>
        public static float LosKeepDay = 0.50f, LosKeepNight = 0.62f;
        /// <summary>The grey veil laid over it (the share of the light taken that comes back as a neutral grey: a touch of desaturation).</summary>
        public static float LosVeil = 0.35f;
        /// <summary>Checks only (the A / B timing): the line-of-sight fog on (true, the game) or off.</summary>
        public static bool LosOn = true;

        OzFowSeen seen; Texture2D seenTex; float losNextPrune; int losCursor;
        HashSet<int> losHidden = new HashSet<int>(), losSpare = new HashSet<int>();
        static HashSet<int> losNow;                                                       // Covers' fast state (null: nothing hidden by it)
        readonly List<Light> losLights = new List<Light>(16);
        sealed class LCache { public readonly List<Light> list = new List<Light>(4); public float at = -1f; }
        readonly Dictionary<int, LCache> lcache = new Dictionary<int, LCache>(64);
        int losViewFrame = -1;

        static readonly int IdSeen = Shader.PropertyToID("_OzFowSeen"), IdLos = Shader.PropertyToID("_OzFowLos");

        /// <summary>The seen-now map of this match (null with the fog off).</summary>
        public static OzFowSeen Seen => I ? I.seen : null;
        /// <summary>Its texture (R8, a texel a metre).</summary>
        public static Texture2D SeenTexture => I ? I.seenTex : null;
        /// <summary>Checks: enemy units hidden by the line-of-sight fog now (on explored ground).</summary>
        public static int LosHiddenCount => I && losNow != null ? I.losHidden.Count : 0;
        /// <summary>Checks: units held off by it on the player's camera's last frame (in its view), and real lamps held off with them.</summary>
        public static int LosViews => I && I.losViewFrame >= Time.frameCount - 1 ? losViewsN : 0;
        public static int LosLightsHeld => I && I.losViewFrame >= Time.frameCount - 1 ? losLightsN : 0;
        static int losViewsN, losLightsN;
        /// <summary>Checks: its processor time a frame (ms): the last frame, the worst and the sum since <see cref="LosCostReset"/>, frames,
        /// rounds (seen map made again), uploads.</summary>
        public static double LosMsLast, LosMsMax, LosMsSum; public static int LosFrames, LosRounds, LosUploads;
        public static void LosCostReset() { LosMsLast = LosMsMax = LosMsSum = 0.0; LosFrames = LosRounds = LosUploads = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LosStatics() { LosOn = true; losNow = null; losViewsN = losLightsN = 0; LosCostReset(); }

        /// <summary>StartFog, right after the reveal map's start: the seen map from where the player's side stands now, shown at once.</summary>
        void LosStart()
        {
            if (mask == null || seen != null) return;
            seen = new OzFowSeen(mask.W, mask.H);
            LosRoundAll(); seen.ShowAll(); LosLiftStart();                                     // 1.5 r14_r16: a radar of ours at the start: lifted at once (View/OzFogOfWar.LosRadarLift.cs)
            seenTex = new Texture2D(seen.W, seen.H, TextureFormat.R8, false, true) { name = "OZ_FogOfWarSeen", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0, hideFlags = HideFlags.DontSave };
            LosUpload();
            losHidden.Clear();
            LosDecide();
            Debug.Log($"[OpenZ FogOfWar] line-of-sight fog (1.5 los_fog): seen now {seen.SeenShare() * 100f:0.0} % of the map, eased up {LosUpSeconds:0.0} s / down {LosDownSeconds:0.0} s, "
                      + $"light kept {LosKeepDay:0.00} by day / {LosKeepNight:0.00} at night, enemy units hidden on explored unseen ground: {losHidden.Count}");
        }

        /// <summary>Update, every frame with the fog on (after the reveal and the radar circle): this frame's share of the round's stamps (a round
        /// about every 0.05 s), the easing, the upload when it changed; the enemy units it hides decided.</summary>
        void LosStep()
        {
            if (!LosOn || seen == null) { losNow = null; return; }
            long t0 = Stopwatch.GetTimestamp();
            float dt = FowDt;                                                               // the fog's clock (the capture step while a take records)
            LosRoundPart(dt);
            LosLiftStep(dt);                                                                // 1.5 r14_r16: lifted over the whole map while a radar of ours stands, eased out / back over 1 s (View/OzFogOfWar.LosRadarLift.cs)
            if (seen.Ease(dt, LosUpSeconds, LosDownSeconds)) LosUpload();
            LosDecide();
            if (Time.unscaledTime >= losNextPrune) { losNextPrune = Time.unscaledTime + 30f; if (game && lcache.Count > 2 * game.LiveViewValues.Count + 64) lcache.Clear(); }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            LosMsLast = ms; LosMsSum += ms; if (ms > LosMsMax) LosMsMax = ms; LosFrames++;
        }

        /// <summary>This frame's share of the round: the objects of the list from the cursor on, as many as this frame's part of a round
        /// (dt / LosRoundSeconds of the list, at least one); the round ends (the seen map's target made again) when the list is done. An object
        /// the list moved past while it changed is simply missed for a round: the hold covers it.</summary>
        void LosRoundPart(float dt)
        {
            var list = W.Objects; int n = list.Count;
            if (losCursor == 0) seen.Begin();
            int per = Mathf.Max(1, Mathf.CeilToInt(n * Mathf.Min(1f, Mathf.Max(dt, 0.001f) / LosRoundSeconds)));
            int end = Mathf.Min(n, losCursor + per), agro = W.S != null ? W.S.agro_distance : 40;
            for (int i = losCursor; i < end; i++) LosStampOne(list[i], agro);
            losCursor = end;
            if (losCursor >= n) { seen.End(); losCursor = 0; LosRounds++; }
        }

        /// <summary>A whole round at once (the match start).</summary>
        void LosRoundAll()
        {
            seen.Begin();
            int agro = W.S != null ? W.S.agro_distance : 40; var list = W.Objects;
            for (int i = 0; i < list.Count; i++) LosStampOne(list[i], agro);
            seen.End(); losCursor = 0;
        }

        /// <summary>One object's sight now, when it is of the player's side: its sight circle, and a standing radar's circle at its radius now.</summary>
        void LosStampOne(ZObject o, int agro)
        {
            if (o == null || o.Owner != player || o.Destroyed || o.KillAt >= 0) return;
            int r = SightPx(o, agro);
            if (r > 0) seen.Stamp(o.CenterX, o.CenterY, r);
            if (o.Kind == ZKind.Building && o.Type == "radar")
            {
                float rr = radar.RadiusOf(o.Id);                                            // the radar circle at its radius now (growing smoothly when just gained)
                if (rr >= OzFowRadar.CirclePx) seen.Stamp(o.CenterX, o.CenterY, OzFowRadar.CirclePx);   // open: the cached circle
                else if (rr > r) seen.Stamp(o.CenterX, o.CenterY, rr);                      // growing: worked out from its radius now
            }
        }

        void LosUpload()
        {
            if (!seenTex || seen == null) return;
            seenTex.LoadRawTextureData(seen.Shown);
            seenTex.Apply(false, false);
            LosUploads++;
        }

        /// <summary>Which enemy units the line-of-sight fog hides now: robots and vehicles of the other side (not the player's, not neutral: an
        /// empty vehicle stays), not destroyed (a wreck stays), standing on explored ground (ground not explored keeps the cloud's own rule) whose
        /// seen value is under <see cref="LosHideBelow"/>, or under <see cref="LosShowAt"/> for one hidden already. Two sets swapped: no allocation.</summary>
        void LosDecide()
        {
            losSpare.Clear();
            var list = W.Objects;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o.Owner == player || o.Owner == Team.Neutral || o.Destroyed) continue;
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle) continue;              // ground guns are structures: they stay
                if (mask.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed) continue;     // not explored yet: the cloud's own rule (Covers, the early band)
                byte v = LosLifted(seen.ShownAtPx(o.CenterX, o.CenterY));                   // 1.5 r14_r16: with the radar lift (max of the two)
                if (v < (losHidden.Contains(o.Id) ? LosShowAt : LosHideBelow)) losSpare.Add(o.Id);
            }
            var t = losHidden; losHidden = losSpare; losSpare = t;
            losNow = losHidden;
        }

        /// <summary>The line-of-sight fog hides this unit now (Covers and the hold-off read it; Covers checks the gate first).</summary>
        static bool LosCovers(ZObject o) => losNow != null && o != null && losNow.Contains(o.Id);

        /// <summary>Covers' answer for a unit the line-of-sight fog hides (counted as every covered answer).</summary>
        static bool LosCountCovered() { CoversTrue++; return true; }

        /// <summary>SetMaterial, every frame: the seen texture and the look's numbers (by day / at night) onto the fog's material.</summary>
        void LosMaterial()
        {
            if (!mat) return;
            bool on = LosOn && seenTex;
            mat.SetTexture(IdSeen, on ? (Texture)seenTex : Texture2D.whiteTexture);
            float keep = Mathf.Clamp01(Mathf.Lerp(LosKeepDay, LosKeepNight, Mathf.Clamp01(NightGrade.NightK)));
            mat.SetVector(IdLos, LosLiftLook(new Vector4(keep, Mathf.Max(0f, LosVeil) * 0.25f, on ? 1f : 0f, on && LosScanLines ? Mathf.Clamp01(LosScanDepth) : 0f)));   /* 1.5 r14_r16: eased away while a radar of ours stands (View/OzFogOfWar.LosRadarLift.cs) */   // final pass 14h: w = the scan lines' depth (View/OzFogOfWar.LosLamps.cs)   // y: the veil x a ground albedo of about 0.25 (times the sky's light in the shader)
        }

        /// <summary>HideCovered, for a unit held off by the line-of-sight fog in the camera's view: counted, and its real lamps (a few crewed vehicles
        /// near the camera have spot lights) held off with it, so no headlight pool shows its way at night. Given back in RestoreHidden.</summary>
        void LosHeldView(UnitView v)
        {
            int f = Time.frameCount;
            if (losViewFrame != f) { losViewFrame = f; losViewsN = 0; losLightsN = 0; }
            losViewsN++;
            int id = v.GetInstanceID();
            if (!lcache.TryGetValue(id, out var c)) { c = new LCache(); lcache[id] = c; }
            float now = Time.unscaledTime;
            if (c.at < 0f || now - c.at > 1f) { v.GetComponentsInChildren(true, c.list); c.at = now; }
            for (int k = 0; k < c.list.Count; k++)
            {
                var l = c.list[k];
                if (l && l.enabled) { l.enabled = false; losLights.Add(l); losLightsN++; }
            }
        }

        void LosRestoreLights()
        {
            for (int i = 0; i < losLights.Count; i++) { var l = losLights[i]; if (l) l.enabled = true; }
            losLights.Clear();
        }

        void LosRelease()
        {
            LosRestoreLights();
            if (losNow == losHidden || losNow == losSpare) losNow = null;                 // (OnDestroy has let go of I already)
            if (seenTex) Destroy(seenTex);
            seenTex = null; seen = null;
        }

        /// <summary>The cost log's part (CostLine): the line-of-sight fog's processor time and state.</summary>
        static string LosCostText()
        {
            var f = I; if (!f) return "";
            if (!LosOn || f.seen == null) return "; line-of-sight fog off";
            return $"; line-of-sight fog {(LosFrames > 0 ? LosMsSum / LosFrames : 0.0):0.000} ms a frame (worst {LosMsMax:0.000}), seen now {f.seen.SeenShare() * 100f:0.0} %, enemy units hidden {f.losHidden.Count}, uploads {LosUploads}" + LosLiftText();   // 1.5 r14_r16
        }
    }
}
