// OpenZ (GPL-3.0) - version 1.5, kit hit_feedback_sounds_r8 (FAILS A31, a player's report of 15 Nov 2026:
// The sniper bip is gone).
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Every bullet that HITS says so with a short impact sound of what it hit: a robot (imp_hit_robot: a light metal body tick / thud), a vehicle
    /// or a gun (imp_hit_metal: a ping / clank), a building (imp_hit_building: a duller hit). A sniper's round that hits uses the SAME sounds as
    /// rock: the game's imp_bullet_stone as before). The sounds are events of the sound table (Resources/OpenZ/Audio/openz_sound_events.json):
    /// several takes each, a shuffled bag (never the same take twice in a row), a small random pitch and level, 3D with the game's distance and zoom
    /// attenuation. VIEW ONLY: nothing of the sim, the damage, the looks or the firing sounds changes.
    ///
    /// The owner's caps, all here (a sound that does not pass is DROPPED, never queued):
    ///   on screen only      the hit point inside the camera view + ScreenMargin of the view; anything else makes no impact sound at all
    ///   MaxVoices           impact voices sounding at once, everything counted: hits, misses and the game's old ricochets (OpenZHitSparks)
    ///   MaxPerSecond        impact sounds started in any one second, everything counted: hits, misses and the old ricochets (misses: at most
    ///                       MaxMissPerSecond of them, MaxMissVoices at once)
    ///   TargetWindow        at most one hit sound per target in this time
    /// A voice counts as sounding for the longest take of its pool at the lowest pitch (Life) + the audio output latency, so the real number of
    /// voices is never over the cap.
    /// Time base of the caps: the audio clock (AudioSettings.dspTime), the same in play and in a frame-locked recording.
    /// MaxMissPerSecond for 1 s + Edge(), Edge = 2 x the longest frame of the last 1.5-2 s + the audio output latency (MinEdge..MaxEdge). Anyone who
    /// looks at the AudioSources once a frame and stamps a start with that frame's dspTime sees it at most two frames + the audio latency after the
    /// request, so no such observer counts more than the cap in any one second (the cap's own count over exactly 1 s: PeakPerSecond).
    ///
    /// Hooks: OpenZFxPlayer.Shot (jeep, gatling, APC crew, crane driver and every other LegacyMatch round: Round, played when the round arrives),
    /// WeaponsV2Runner.MgImpact / SniperImpact (grunt, psycho, sniper: Landed), MechHandGunView.StepHits (the mech's hand gun: Landed),
    /// OzSoundV3.GroundImpact (every miss: Miss), OzSoundV3.ShotLanded (the old masonry chip on a building is left to imp_hit_building),
    /// OpenZHitSparks.PlayRicochet (the old ricochet shares MaxVoices and MaxPerSecond and is on screen only: RicochetVoice).
    /// On = false: every hook does exactly what it did before the kit.
    /// </summary>
    [DefaultExecutionOrder(113)]
    public sealed class OzHitFeedback : MonoBehaviour
    {
        public static OzHitFeedback I { get; private set; }
        /// <summary>false = the game exactly as before the kit.</summary>
        public static bool On = true;

        public const string EvRobot = "imp_hit_robot", EvMetal = "imp_hit_metal", EvBuilding = "imp_hit_building",
                            EvMiss = "imp_miss_ground", EvMissHard = "imp_bullet_stone";
        public enum Kind : byte { Robot, Metal, Building, Miss, Ricochet }
        public const int Kinds = 5;

        // ---- the caps
        public const int MaxVoices = 5;
        public const int MaxPerSecond = 8;
        public const float TargetWindow = 0.2f;
        public const int MaxMissVoices = 2, MaxMissPerSecond = 3;
        /// <summary>The share of the drawn misses that sound at all (before the kit: 35 %, OzSoundV3.GroundImpact).</summary>
        public const float MissChance = 0.5f;
        /// <summary>On screen = inside the view + this much of it on every side.</summary>
        public const float ScreenMargin = 0.04f;
        /// <summary>How long one voice counts as sounding (s) = the longest take of the pool / the lowest pitch: robot 0.300 / 0.94, metal 0.450 / 0.94,
        /// building 0.400 / 0.94, miss 0.480 / 0.92 (the sand and stone takes), ricochet 0.550 / 0.89 (hs_ricochet_c).</summary>
        static readonly float[] Life = { 0.32f, 0.48f, 0.43f, 0.53f, 0.62f };
        /// <summary>The per-second window's edge margin (s), at least / at most (see Edge).</summary>
        public const float MinEdge = 0.05f, MaxEdge = 0.5f;

        // ---- counts (the Play check reads them; reset by ResetCounts)
        /// <summary>The largest edge margin the per-second caps used (s).</summary>
        public static float EdgeUsed;
        public static int Asked, Played, DropOffScreen, DropVoices, DropPerSecond, DropTarget, DropEngine;
        /// <summary>Sniper rounds among the hits: asked, and played (with the robot / vehicle-gun / building sound of what they hit).</summary>
        public static int SniperAsked, SniperPlayed;
        public static int MissAsked, MissPlayed, MissSkipped, MissOffScreen, MissDropCap, MissDropEngine;
        public static int RicochetAsked, RicochetPlayed, RicochetDropped;
        public static int PeakVoices, PeakPerSecond;
        public static readonly int[] PlayedOf = new int[Kinds];

        public static void ResetCounts()
        {
            Asked = Played = DropOffScreen = DropVoices = DropPerSecond = DropTarget = DropEngine = 0; SniperAsked = SniperPlayed = 0;
            MissAsked = MissPlayed = MissSkipped = MissOffScreen = MissDropCap = MissDropEngine = 0;
            RicochetAsked = RicochetPlayed = RicochetDropped = 0; PeakVoices = PeakPerSecond = 0; EdgeUsed = 0f;
            for (int i = 0; i < Kinds; i++) PlayedOf[i] = 0;
        }

        public static string Status() =>
            $"hit feedback {(On ? "on" : "OFF")}: hits asked {Asked} played {Played} (robot {PlayedOf[0]} vehicle/gun {PlayedOf[1]} building {PlayedOf[2]}; of them sniper rounds asked {SniperAsked} played {SniperPlayed}); "
            + $"dropped: off screen {DropOffScreen}, same target within {TargetWindow:0.00} s {DropTarget}, {MaxVoices} voices full {DropVoices}, {MaxPerSecond} a second full {DropPerSecond}, "
            + $"sound engine {DropEngine}; misses asked {MissAsked} played {MissPlayed} (off screen {MissOffScreen}, not drawn by chance {MissSkipped}, "
            + $"caps {MissDropCap}, sound engine {MissDropEngine}); old ricochets asked {RicochetAsked} played {RicochetPlayed} dropped (off screen or a cap) {RicochetDropped}; "
            + $"most voices counted at once {PeakVoices} (cap {MaxVoices}), most started in one second {PeakPerSecond} (cap {MaxPerSecond}; window 1 s + edge up to {EdgeUsed * 1000f:0} ms)";

        // ---- state (fixed arrays: no garbage per bullet)
        struct Pend { public float at; public Vector3 pos; public Kind kind; public int target; public bool sniper; }
        readonly Pend[] pend = new Pend[192]; int pendCount;
        readonly double[] voiceEnd = new double[16]; readonly Kind[] voiceKind = new Kind[16];
        readonly double[] startAt = new double[16]; readonly Kind[] startKind = new Kind[16]; int startHead;
        readonly int[] tgtId = new int[48]; readonly double[] tgtAt = new double[48]; int tgtHead;
        readonly float[] dtBin = new float[4]; long dtBinNo = -1;                                          // the longest frame of each of the last four half seconds
        double audioLag = 0.1;                                                                             // the audio output latency (s): all DSP buffers
        Camera cam;

        static OzHitFeedback Ensure()
        {
            if (I) return I;
            if (!Application.isPlaying) return null;
            return new GameObject("OzHitFeedback").AddComponent<OzHitFeedback>();
        }

        void Awake()
        {
            I = this;
            for (int i = 0; i < voiceEnd.Length; i++) voiceEnd[i] = -1;
            for (int i = 0; i < startAt.Length; i++) startAt[i] = -99;
            for (int i = 0; i < tgtId.Length; i++) { tgtId[i] = -1; tgtAt[i] = -99; }
            AudioSettings.GetDSPBufferSize(out int len, out int num); int rate = AudioSettings.outputSampleRate;
            if (len > 0 && rate > 0) audioLag = (double)len * Mathf.Max(1, num) / rate;
        }
        void OnDestroy() { if (I == this) I = null; }

        static double Now => AudioSettings.dspTime;

        // ------------------------------------------------------------------------------------------------ the hooks
        /// <summary>OpenZFxPlayer.Shot: a drawn round that will reach `pos` at game time `at`. A hit on a robot (Unit) or on a vehicle, gun or building
        /// (Metal) sounds when it arrives; every other surface is a miss and comes through OzSoundV3.GroundImpact.</summary>
        public static void Round(float at, Vector3 pos, FxWeapon w, FxSurface s, ZObject target)
        {
            if (!On || (s != FxSurface.Unit && s != FxSurface.Metal)) return;
            var i = Ensure(); if (!i) return;
            if (i.pendCount >= i.pend.Length) { Asked++; DropVoices++; return; }                           // 192 rounds in the air at once: more are dropped
            i.pend[i.pendCount++] = new Pend { at = at, pos = pos, kind = KindOf(s, target), target = target != null ? target.Id : -1, sniper = w == FxWeapon.Sniper };
        }

        /// <summary>WeaponsV2Runner (grunt, psycho, sniper) and MechHandGunView: a round that has just landed. `sniper` only counts it for the
        /// Play check: a sniper's round sounds exactly like any bullet that hits the same thing.</summary>
        public static void Landed(Vector3 pos, bool hit, FxSurface s, int targetId, bool sniper)
        {
            if (!On || !hit || (s != FxSurface.Unit && s != FxSurface.Metal)) return;
            var i = Ensure(); if (!i) return;
            ZObject t = null;
            if (targetId >= 0 && ZGame.I && ZGame.I.World != null) t = ZGame.I.World.GetAny(targetId);
            i.Hit(KindOf(s, t), pos, targetId, sniper);
        }

        /// <summary>OzSoundV3.GroundImpact: a drawn round that hit the ground. True = handled here (played or dropped); false = the kit is off.</summary>
        public static bool Miss(Vector3 at, FxSurface s)
        {
            if (!On) return false;
            var i = Ensure(); if (!i) return false;
            MissAsked++;
            if (!i.OnScreen(at)) { MissOffScreen++; return true; }
            if (Random.value >= MissChance) { MissSkipped++; return true; }
            double now = Now, win = 1.0 + i.Edge();
            if (i.Live(now, true) >= MaxMissVoices || i.Live(now, false) >= MaxVoices || i.Starts(now, true, win) >= MaxMissPerSecond || i.Starts(now, false, win) >= MaxPerSecond) { MissDropCap++; return true; }
            var ad = AudioDirector.I;
            if (!ad || !ad.Play(s == FxSurface.Ground ? EvMiss : EvMissHard, at, -1, Random.Range(0.8f, 1f))) { MissDropEngine++; return true; }
            i.Note(Kind.Miss, now, -1); MissPlayed++;
            return true;
        }

        /// <summary>OpenZHitSparks.PlayRicochet: the game's old ricochet / ping asks for a voice. False = off screen (hitsparks culls only at 10 % of
        /// the view) or a cap is full (voices at once, sounds a second): it stays silent.</summary>
        public static bool RicochetVoice(Vector3 at)
        {
            if (!On) return true;
            var i = Ensure(); if (!i) return true;
            RicochetAsked++;
            if (!i.OnScreen(at)) { RicochetDropped++; return false; }
            double now = Now;
            if (i.Live(now, false) >= MaxVoices || i.Starts(now, false, 1.0 + i.Edge()) >= MaxPerSecond) { RicochetDropped++; return false; }
            i.Note(Kind.Ricochet, now, -1); RicochetPlayed++;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the rules
        /// <summary>What was hit, for every bullet alike (a sniper's round too): a building, else a robot (Unit), else a vehicle or a gun (Metal).</summary>
        static Kind KindOf(FxSurface s, ZObject t)
        {
            if (t != null && t.Kind == ZKind.Building) return Kind.Building;
            return s == FxSurface.Unit ? Kind.Robot : Kind.Metal;
        }

        static string EventOf(Kind k) => k == Kind.Robot ? EvRobot : k == Kind.Metal ? EvMetal : EvBuilding;

        void Update()
        {
            TrackFrame();
            if (pendCount == 0) return;
            float now = Time.time;
            for (int i = pendCount - 1; i >= 0; i--)
            {
                if (pend[i].at > now) continue;
                var p = pend[i]; pend[i] = pend[--pendCount];
                if (On) Hit(p.kind, p.pos, p.target, p.sniper);
            }
        }

        bool Hit(Kind k, Vector3 pos, int target, bool sniper)
        {
            Asked++; if (sniper) SniperAsked++;
            if (!OnScreen(pos)) { DropOffScreen++; return false; }
            double now = Now;
            if (target >= 0 && TargetBusy(target, now)) { DropTarget++; return false; }
            if (Live(now, false) >= MaxVoices) { DropVoices++; return false; }
            if (Starts(now, false, 1.0 + Edge()) >= MaxPerSecond) { DropPerSecond++; return false; }
            var ad = AudioDirector.I;
            if (!ad || !ad.Play(EventOf(k), pos, -1)) { DropEngine++; return false; }
            Note(k, now, target); Played++; PlayedOf[(int)k]++; if (sniper) SniperPlayed++;
            return true;
        }

        bool OnScreen(Vector3 p)
        {
            if (!cam) cam = Camera.main;
            if (!cam) return true;
            if (OzFpsView.Own(p)) return true;                                                             // the viewed unit's own sounds are in view (as AudioDirector)
            var vp = cam.WorldToViewportPoint(p);
            return vp.z > 0f && vp.x > -ScreenMargin && vp.x < 1f + ScreenMargin && vp.y > -ScreenMargin && vp.y < 1f + ScreenMargin;
        }

        /// <summary>Voices counted as sounding now (missesOnly: the ground misses among them).</summary>
        int Live(double now, bool missesOnly)
        {
            int n = 0;
            for (int i = 0; i < voiceEnd.Length; i++) if (voiceEnd[i] > now && (!missesOnly || voiceKind[i] == Kind.Miss)) n++;
            return n;
        }

        /// <summary>Impact sounds started in the last `window` seconds: this kit's and the old ricochets (missesOnly: the ground misses among them).</summary>
        int Starts(double now, bool missesOnly, double window)
        {
            int n = 0;
            for (int i = 0; i < startAt.Length; i++) if (now - startAt[i] < window && (!missesOnly || startKind[i] == Kind.Miss)) n++;
            return n;
        }

        /// <summary>Every frame: the longest frame (unscaled) of each half second, the last four kept (1.5-2 s).</summary>
        void TrackFrame()
        {
            long no = (long)(Time.unscaledTimeAsDouble * 2.0);
            if (no != dtBinNo) { for (long b = System.Math.Max(dtBinNo + 1, no - 3); b <= no; b++) dtBin[(int)(b & 3)] = 0f; dtBinNo = no; }
            int k = (int)(no & 3); float dt = Time.unscaledDeltaTime; if (dt > dtBin[k]) dtBin[k] = dt;
        }

        /// <summary>The per-second window's edge margin (s): a start can be seen up to two frames + the audio latency after its request
        /// (one frame when it is asked after the observer looked, the observer's own frame, and the audio clock's buffer steps).</summary>
        double Edge()
        {
            float w = 0f; for (int i = 0; i < dtBin.Length; i++) if (dtBin[i] > w) w = dtBin[i];
            double m = 2.0 * w + audioLag; if (m < MinEdge) m = MinEdge; else if (m > MaxEdge) m = MaxEdge;
            if (m > EdgeUsed) EdgeUsed = (float)m;
            return m;
        }

        bool TargetBusy(int id, double now)
        {
            for (int i = 0; i < tgtId.Length; i++) if (tgtId[i] == id && now - tgtAt[i] < TargetWindow) return true;
            return false;
        }

        void Note(Kind k, double now, int target)
        {
            int slot = 0; double oldest = double.MaxValue;
            for (int i = 0; i < voiceEnd.Length; i++) { if (voiceEnd[i] <= now) { slot = i; break; } if (voiceEnd[i] < oldest) { oldest = voiceEnd[i]; slot = i; } }
            voiceEnd[slot] = now + Life[(int)k] + audioLag; voiceKind[slot] = k;                           // + the latency: the voice starts up to that much later
            startAt[startHead] = now; startKind[startHead] = k; startHead = (startHead + 1) % startAt.Length;
            if (target >= 0) { tgtId[tgtHead] = target; tgtAt[tgtHead] = now; tgtHead = (tgtHead + 1) % tgtId.Length; }
            int live = Live(now, false); if (live > PeakVoices) PeakVoices = live;
            int st = Starts(now, false, 1.0); if (st > PeakPerSecond) PeakPerSecond = st;                   // the cap's own count over exactly one second
        }
    }
}
