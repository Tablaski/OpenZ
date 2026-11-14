#if UNITY_EDITOR
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k84b: fixed open tiles for the light tank's films, proof and pictures (editor only), chosen off the editor on k81c's whole-map scan
    /// no protected spot near, no object in a one-tile ring) and the light gun's reach (the sim's 120 px attack radius less a tile).
    /// Absolute tiles (x east, y south), the top-left tile of a vehicle.
    ///   recoil/jeep near: 58-80 px from both recoil tanks (47,37) / (53,37) (the heavy's jeep (50,30): 122-132 px);
    ///   proof/jeep near:  72-82 px from the Play proof's red pair (lineup centre - (6,4) and - (1,4)), the same offset (-3,-8) on L02 and L17;
    ///   card/blue:        an open 3 x 3 box east of the blue fort on L02 (12 tiles from its middle): the drop of a blue light tank for the
    ///                     blue unit card picture (k84's drop beside the blue factory found no room).
    /// </summary>
    public static class LightV2Spots
    {
        static readonly Dictionary<string, (int x, int y)> T = new Dictionary<string, (int x, int y)>
        {
            { "L02/recoil/jeep near", (51, 34) },
            { "L02/proof/jeep near", (30, 11) },
            { "L17/proof/jeep near", (86, 53) },
            { "L02/card/blue", (30, 14) },
        };

        /// <summary>A fixed tile; false = none for this level / job / name.</summary>
        public static bool Get(string level, string job, string what, out int x, out int y)
        {
            x = y = 0;
            if (!T.TryGetValue(level + "/" + job + "/" + what, out var t)) return false;
            x = t.x; y = t.y; return true;
        }
    }

    /// <summary>
    /// k84b: the light tank Play proof's staging (View/LightV2Proof.cs calls it), every step logged.
    ///  * FIRE (tracks_planted, recoil, head kick, hull rock): k84's red pair did not stand: their jeep stood 104 /
    ///    162 px off (the light gun reaches 120 px; red_v1 drove to it) and the level's own blue grunts two tiles off red_v1 fought them
    ///    (the pair fired at them, chased them and were shot at; red_v1 became a veteran, DV12). Now the jeep stands in reach of both
    ///    (LightV2Spots proof/jeep near), the level's own enemy units near the pair have no weapon for the fire phase, and both red tanks
    ///    take the jeep as their target (ZObject.Engage, the sim's own pick): they stand and fire.
    ///  * HATCH: a veteran crew stays buttoned up (DV12, BoardingView.ShowHatchDriver): each red tank's kills and rank are logged before
    ///    the hatch check and a veteran crew's rank is reset for it (as k81d's heavy proof).
    ///  * DEATH: every tank wreck breaks up (DV18, seeded per unit): one in three at once, the others when they have burnt 5-7 s (cook-off).
    ///    k84's proof waited 9 s: a tank cooking off after 6-7 s (+ the sim's kill, + up to 2 s waiting for its cut) broke after the count.
    ///    The proof now tells each tank's way first and waits until both broke (at most <see cref="DeathWaitS"/> s).
    /// </summary>
    public static class LightV2Stage
    {
        /// <summary>The longest wait for both break-ups after the kill (s): 7 s of burning + up to 2 s for the cut + the sim's kill, with room.</summary>
        public static float DeathWaitS = 16f;

        /// <summary>The fire phase: the level's own enemy units within 24 tiles of the red pair get no weapon; both red tanks take the jeep.</summary>
        public static void FireStage(ZWorld W, ZObject r1, ZObject r2, ZObject jeep, IEnumerable<ZObject> proofUnits, System.Action<string> log)
        {
            if (W == null || r1 == null || r2 == null || jeep == null) return;
            var mine = new HashSet<ZObject>(proofUnits);
            int cx = (r1.CenterX + r2.CenterX) / 2, cy = (r1.CenterY + r2.CenterY) / 2, n = 0;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Destroyed || mine.Contains(o) || o.Owner == Team.Neutral || o.Owner == r1.Owner) continue;
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Cannon && o.Kind != ZKind.Vehicle) continue;
                if (Mathf.Abs(o.CenterX - cx) > 24 * 16 || Mathf.Abs(o.CenterY - cy) > 24 * 16) continue;
                o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; n++;
            }
            log($"staged (k84b): the level's own {n} enemy unit(s) within 24 tiles of the red pair have no weapon for the fire phase (in k84's run the pair fought and chased the grunts two tiles off red_v1: no standing)");
            foreach (var (o, tag) in new[] { (r1, "red_v1"), (r2, "red_v2") })
            {
                o.Engage(jeep);
                log($"staged (k84b): {tag} takes the jeep as its target (ZObject.Engage, as the sim's own pick); the jeep is {Dist(o, jeep):0} px off centre to centre, " +
                    $"{(o.WithinAttackRadius(jeep) ? "within" : "NOT within")} its gun's reach ({o.AttackRadius} px) where it stands");
            }
        }

        /// <summary>The hatch check: each red tank's kills and rank, and a veteran crew's rank reset (a veteran crew stays buttoned up, DV12).</summary>
        public static void HatchStage(ZObject r1, ZObject r2, System.Action<string> log)
        {
            foreach (var (o, tag) in new[] { (r1, "red_v1"), (r2, "red_v2") })
            {
                if (o == null) continue;
                log($"{tag} before the hatch check: kills {o.Kills}, veteran {o.Veteran} (perks on: {o.VeteranActive})");
                if (o.Veteran) { o.Veteran = false; o.Kills = 0; o.PromotedAt = -1; log($"staged: {tag}'s crew rank reset for the hatch check (a veteran crew stays buttoned up, DV12)"); }
            }
        }

        /// <summary>Each tank's seeded way to break up (UnitView.BeginWreck: DV18), told before the kill.</summary>
        public static string DeathWays(ZObject a, string ta, ZObject b, string tb) =>
            $"the break-ups to come (DV18, seeded per unit as UnitView.BeginWreck rolls them): {Way(a, ta)}; {Way(b, tb)}; the proof waits until both broke (at most {DeathWaitS:0} s)";

        static string Way(ZObject o, string tag)
        {
            if (o == null) return tag + " -";
            uint h = RobotDeathRule.Hash(o.Id, 0, 11u);
            if (WreckFx.BreakAtDeath && WreckFx.Chance(h >> 4, 3u)) return $"{tag} #{o.Id} explodes into parts at once";
            if (WreckFx.CookOffs && WreckFx.Force != 2) return $"{tag} #{o.Id} burns {5u + h % 3u} s, then cooks off into parts";
            return $"{tag} #{o.Id} stays a whole wreck (the death test aid WreckFx.Force = {WreckFx.Force})";
        }

        static float Dist(ZObject a, ZObject b) => Mathf.Sqrt((a.CenterX - b.CenterX) * (float)(a.CenterX - b.CenterX) + (a.CenterY - b.CenterY) * (float)(a.CenterY - b.CenterY));
    }
}
#endif
