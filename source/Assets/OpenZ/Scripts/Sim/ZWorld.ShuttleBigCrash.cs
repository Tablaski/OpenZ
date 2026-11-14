using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// The
    /// deviation row DV154 on top of DV118 (the playable shuttle) and its rough landing (Sim/ZWorld.Shuttle.cs).
    /// <list type="bullet">
    /// <item>
    /// crash landings stay exactly as in 1.2 (one landing in <see cref="ZWorld.ShuttleRoughEvery"/>, by the landing's number, never on a
    /// repair station), and one crash landing in <see cref="ZWorld.ShuttleBigCrashEvery"/> (3) of those is the BIG crash landing (about one
    /// landing in 12). No random draw: in every block of 3 crash landings of a ship (by the ship's own crash count: its crashes 1-3, 4-6,
    /// ...) exactly one is the big crash, which one by the ship's id and the block's number (<see cref="ShuttleBagPick"/>), so it is exactly
    /// one crash in 3 over a match, never more than 2 big crashes in a row and never more than 4 of today's crashes in a row between them
    /// (the plain hash bunches: up to 11-15 in a row, checks/big_crash_share.py of the 1.3 kit).</item>
    /// <item>READING switch: <see cref="ZWorld.ShuttleBigCrashOfAllLandings"/> off (the default, the owner's answer above). On (the other
    /// reading, kept as a code switch only): one landing in 3 of ALL landings is the big crash (the same bag over the ship's landings) and
    /// every crash landing is the big one (today's one-in-4 test is not used then).</item>
    /// <item>THE RULES' TIMING of a big crash is the rough landing's (crash, tilted rest, self-righting; EXIT, the ramp and a take-off wait for
    /// it to stand level): <see cref="ShuttleRoughLanding"/> is true for it too, so every rule and view that knows the rough landing goes on
    /// as before; <see cref="ShuttleBigCrash"/> says it is the big one (the view adds its sparks, its harder sound, its stronger shake and
    /// the banter line).</item>
    /// <item>THE DAMAGE, at the touchdown (the slam): <see cref="ZWorld.ShuttleBigCrashLossPct"/> % of the FULL health (MaxHealth), but the
    /// health never goes under the safe floor, <see cref="ZWorld.ShuttleBigCrashFloorPct"/> % of the full health; a ship already at or under
    /// the floor loses nothing. Whole hit points, rounded half up: the shuttle's 4054 = 1014 lost, the floor 405. So it is never destroyed by
    /// its own landing. Only the hull: the squad aboard and the pilot are not hurt. Nobody is credited with it (no attacker, no kill).</item>
    /// <item>Every shuttle the same (level 16's, level 17's, the testing panel's, the computer's under DV138; a Custom level's own hit points
    /// scale it). Deterministic: whole numbers, no random draw, no clock.</item>
    /// </list>
    /// Switch <see cref="ZWorld.ShuttleBigCrashDeviation"/> (code switch; ZGame: with DV118, so off in 'Zod exact'; headless default off,
    /// so every older counted check keeps its numbers): off = the 1.2 rough landing exactly (one in 4, no hit points lost).
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV154: this landing is the BIG crash landing (from the start of its descent until the next take-off; always with
        /// ShuttleRoughLanding). Never on a repair station.</summary>
        public bool ShuttleBigCrash { get; internal set; }
        /// <summary>DV154 counts: big crash landings so far; the last big crash's health before it and the hit points it took (0: at or under
        /// the floor, or no big crash yet).</summary>
        public int ShuttleBigCrashes { get; internal set; }
        public int ShuttleBigCrashHpBefore { get; internal set; } = -1;
        public int ShuttleBigCrashLost { get; internal set; }

        /// <summary>One of every ZWorld.ShuttleBigCrashEvery in a row (k = 1, 2, ...): in each block of that many, the one at the place the
        /// hash of (seed, the block's number) names.</summary>
        static bool ShuttleBagPick(int seed, int k)
        {
            int e = ZWorld.ShuttleBigCrashEvery;
            if (k < 1 || e < 1) return false;
            return ShuttleHash(seed, (k - 1) / e) % e == (k - 1) % e;
        }

        /// <summary>ShuttleDescentBegins: is landing n a crash landing (rough), and the big one? The switch off = the 1.2 test exactly.</summary>
        bool ShuttleLandingPick(int n, out bool big)
        {
            big = false;
            bool today = ZWorld.ShuttleRoughProofNext || ShuttleHash(Id, n) % ZWorld.ShuttleRoughEvery == 0;   // the 1.2 rough landing
            if (!ZWorld.ShuttleBigCrashDeviation) return today;
            bool proofBig = ZWorld.ShuttleBigCrashProofNext;
            if (ZWorld.ShuttleBigCrashOfAllLandings)
            {   // reading (a), the code switch: one landing in 3 is the big crash, and every crash landing is the big one
                big = proofBig || ZWorld.ShuttleRoughProofNext || ShuttleBagPick(Id + 5, n);
                return big;
            }
            // reading (b), the default: today's one in 4 crash landings unchanged; one crash in 3 of those (the ship's own crash count) is the big one
            bool rough = proofBig || today;
            big = rough && (proofBig || ShuttleBagPick(Id + 13, ShuttleRoughLandings + 1));
            return rough;
        }

        /// <summary>ShuttleTouchdown of a big crash (the slam): a quarter of the full health, never under the safe floor; nothing at or under it.</summary>
        void ShuttleBigCrashHit()
        {
            ZWorld.ShuttleBigCrashProofNext = false;
            ShuttleBigCrashes++;
            ShuttleBigCrashHpBefore = Health; ShuttleBigCrashLost = 0;
            if (Destroyed || KillAt >= 0 || MaxHealth <= 0) return;
            int after = ZWorld.ShuttleBigCrashHealthAfter(Health, MaxHealth);
            if (after >= Health) return;
            ShuttleBigCrashLost = Health - after;
            SetHealth(after);                                                                   // never 0: the floor is at least 1 hit point
            ZWorld.ShuttleBigCrashHpTaken += ShuttleBigCrashLost;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV154 switch (code switch; ZGame: with DV118, off in 'Zod exact'; headless default off). Off = the 1.2 rough landing.</summary>
        public static bool ShuttleBigCrashDeviation = false;
        /// <summary>
        /// stay, one in 3 of those is the big one (about one landing in 12). On (code switch only): one landing in 3 of ALL landings is the
        /// big crash (every crash landing is the big one).</summary>
        public static bool ShuttleBigCrashOfAllLandings = false;
        /// <summary>DV154: one crash landing (the default; or one landing of all, the other reading) in this many is the big crash.</summary>
        public const int ShuttleBigCrashEvery = 3;
        /// <summary>DV154: the big crash takes this share of the full health (%), never going under the safe floor (% of the full health).</summary>
        public const int ShuttleBigCrashLossPct = 25, ShuttleBigCrashFloorPct = 10;
        /// <summary>Proof only: the next landing (not on a repair station) is a big crash (the switch on); cleared when it is used.</summary>
        public static bool ShuttleBigCrashProofNext;
        /// <summary>Check counter: hit points taken by big crashes (every shuttle).</summary>
        public static int ShuttleBigCrashHpTaken;

        /// <summary>The hit points a big crash takes from a ship of maxHp (25 %, rounded half up: 4054 -> 1014).</summary>
        public static int ShuttleBigCrashLoss(int maxHp) => maxHp <= 0 ? 0 : (int)(((long)maxHp * ShuttleBigCrashLossPct + 50) / 100);
        /// <summary>The safe floor of a ship of maxHp (10 %, rounded half up, at least 1: 4054 -> 405).</summary>
        public static int ShuttleBigCrashFloor(int maxHp) => maxHp <= 0 ? 0 : Math.Max(1, (int)(((long)maxHp * ShuttleBigCrashFloorPct + 50) / 100));
        /// <summary>The health after a big crash: at or under the floor nothing is lost; else 25 % of the full health, never under the floor.</summary>
        public static int ShuttleBigCrashHealthAfter(int hp, int maxHp)
        {
            int floor = ShuttleBigCrashFloor(maxHp);
            if (hp <= floor) return hp;
            return Math.Max(floor, hp - ShuttleBigCrashLoss(maxHp));
        }

        /// <summary>The parity report and the older counted checks: no DV154 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable ShuttleBigCrashOff() => new ShuttleBigCrashOffHold();
        sealed class ShuttleBigCrashOffHold : IDisposable
        {
            readonly bool was = ShuttleBigCrashDeviation;
            public ShuttleBigCrashOffHold() { ShuttleBigCrashDeviation = false; }
            public void Dispose() { ShuttleBigCrashDeviation = was; }
        }
    }
}
