#if UNITY_EDITOR
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k87b, the v2 films (View/HeavyV2Films.cs) after the medium tank's Play checks (editor only):
    ///  * THE TRACK TEST (heavy menu 14, light menu 14, medium menu 15; PlantK81d): the second tank ("v1") never had ten frames on sloped
    ///    ground with the planting on (heavy 5, light 4, medium 7: k81d's way back passes the canal bank's edge only, it turns for home two
    ///    tiles before the slope) and the medium tanks hardly stood or fired (the tanks kept the jeep as their target after the bridge stand
    ///    and drove after it, one ended the take 9 tiles off its last goal). The way back (planting ON) now stands each tank 2.5 s ON the bank
    ///    (<see cref="PlantStandK87b"/>: open tiles of k81c's scan whose ground slopes 4.4 deg by the test's own measure at every heading),
    ///    at the bridge stand a tank takes the jeep as its target when it is within its gun's reach (ZObject.Engage, the sim's own pick), a
    ///    target beyond its reach is dropped at a stand and when it leaves a goal (no chase), and the jeep never steps aside for a shell (it
    ///    stays in reach: the MLRS's rockets had made it dodge 9 tiles away). Off the engine (the kit's headless take + its planting check:
    ///    the sim's own places frame by frame, the real L02 ground, the planting): the counts PASS, 0 frames over 3 cm (SPEC).
    ///  * THE COMBAT TEST (menu 14 of the medium, 13 of the light): the medium's driver was shot by the level's own armed grunts beside the
    ///    test's spot, the empty tank was boarded by a blue grunt and fought nothing ("aim changes 0"). The level's own enemies near the stage
    ///    now have no weapon for the test (not the heavy's take); a counted line says how many targets the tank engaged. The turret test the same.
    ///  * THE RECOIL TEST (k84b's rule per unit): a unit with ONE model (the medium tank) is v2 in both windows (its head kicks 0.20, not the
    ///    light v1's 0.10), and a single-barrel gun's "settled" sample is read 0.7 s after its shot when the next shot comes later (it fell
    ///    6 frames after the medium's second shot); the heavy exactly as before.
    /// </summary>
    public partial class HeavyV2Films
    {
        /// <summary>The bank stands of the track test's way back (L02, absolute tiles: a tank's centre stands at (x*16+8, y*16+8) px): open
        /// tiles of k81c's scan, ground sloped 4.4 deg (the test's 2.6 / 1.5 m measure, the lowest over 8 headings), 2 tiles apart.</summary>
        static readonly Dictionary<string, (int x, int y)> PlantStandK87b = new Dictionary<string, (int x, int y)> { { "v1", (48, 40) }, { "v2", (46, 39) } };
        /// <summary>The track test's way back: the bridge stand (3 s, firing), the bank stand (2.5 s on the slope), home.</summary>
        public static float PlantBankHoldK87b = 2.5f;
        ZObject plantJeepK87b;

        /// <summary>PlantK81d's way back (planting ON) for one tank ("v1" / "v2").</summary>
        List<(int x, int y, float hold)> PlantBackK87b(Dictionary<string, (int x, int y)> at, string tag)
        {
            var s = PlantStandK87b[tag];
            bool foot = W.Path.StepOk(s.x, s.y, s.x, s.y, false, false);
            Note($"k87b: {tag}'s way back stands {PlantBankHoldK87b:0.0} s on the canal bank at tile ({s.x},{s.y}) (ground sloped 4.4 deg; footprint {(foot ? "passes" : "BLOCKED")})");
            if (!foot) SpotFail($"the bank stand of {tag} at tile ({s.x},{s.y}) is blocked");
            return new List<(int x, int y, float hold)> { (at["bridge north"].x, at["bridge north"].y, 3f), (s.x, s.y, PlantBankHoldK87b), (at[tag].x, at[tag].y, 0f) };
        }

        /// <summary>PlantK81d, once its jeep stands: the target the tanks take at a stand. It never steps aside for a shell (staged: it cannot
        /// move, so Zod's DodgeMissile skips it): in k81d's MLRS take the rockets' dodges took it from (47,53) to (40,58), out of reach.</summary>
        void PlantJeepK87b(ZObject jeep)
        {
            plantJeepK87b = jeep;
            MTankStage.HoldTarget(jeep, Note);
        }

        /// <summary>LegStep, when a tank's stand begins: a target out of its gun's reach is dropped (no chase from a stand); at a stand with a
        /// hold the jeep becomes its target when it is within its gun's reach.</summary>
        void LegStandK87b(ZObject o, float hold)
        {
            if (o == null) return;
            DropFarK87b(o, "at this stand");
            var j = plantJeepK87b;
            if (hold <= 0f || j == null || j.Destroyed) return;
            if (o.WithinAttackRadius(j)) { o.Engage(j); Note($"k87b: id {o.Id} takes the jeep as its target at this stand (within its gun's {o.AttackRadius} px)"); }
        }

        /// <summary>LegStep, when a tank leaves a goal for the next: a target out of its gun's reach is dropped (the medium tanks drove after
        /// the jeep and one ended k87's take 9 tiles off its last goal); a target in reach is kept (it fires on the way, as the sim does).</summary>
        void LegLeaveK87b(ZObject o)
        {
            if (o != null) DropFarK87b(o, "as it leaves for its next goal");
        }

        void DropFarK87b(ZObject o, string when)
        {
            var t = o.AttackObject;
            if (t == null || (!t.Destroyed && o.WithinAttackRadius(t))) return;
            Note($"k87b: id {o.Id} drops its target #{t.Id} {when} (beyond its gun's {o.AttackRadius} px: no chase)");
            o.AttackObject = null;
        }

        /// <summary>The combat / turret tests: the level's own enemy units within 24 tiles of the tank get no weapon for the test (the film's
        /// own units untouched; the heavy's takes as before).</summary>
        void CombatStageK87b(ZObject tank, string test = "combat test")
        {
            if (tank == null || S.Type == OzHeavyTank.Type) return;
            var mine = new HashSet<ZObject>();
            foreach (var (o, _) in cast) { if (o == null) continue; mine.Add(o); foreach (var m in o.Minions) mine.Add(m); }
            int n = 0;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Destroyed || mine.Contains(o) || o.Owner == Team.Neutral || o.Owner == tank.Owner) continue;
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Cannon && o.Kind != ZKind.Vehicle) continue;
                if (Mathf.Abs(o.CenterX - tank.CenterX) > 24 * 16 || Mathf.Abs(o.CenterY - tank.CenterY) > 24 * 16) continue;
                o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; n++;
            }
            Note($"staged (k87b, the {test}): the level's own {n} enemy unit(s) within 24 tiles of id {tank.Id} have no weapon for the test (in k87's combat test they shot the medium tank's driver and a blue grunt boarded the empty tank)");
        }

        /// <summary>The combat test, after its turret lines: how many targets the tank really engaged (the sim's attack target) in the judged phase.</summary>
        void CombatEngagedK87b()
        {
            foreach (var y in yawTracks)
            {
                int n = 0, on = 0; var ids = new HashSet<int>();
                for (int i = 0; i < y.phase.Count; i++)
                {
                    if (y.phase[i] != "combat") continue;
                    n++; if (y.tgt[i] >= 0) { on++; ids.Add(y.tgt[i]); }
                }
                if (n == 0) continue;
                var s = Analyse(y, "combat");
                bool ok = ids.Count >= 2 && s.aimChanges >= 3;
                Fact($"turret {y.tag} / combat, the targets (k87b): the {S.Label} had a target in {on} of {n} frames, {ids.Count} different target(s), aim changes {s.aimChanges}: {(ok ? "PASS" : "FAIL")} (at least 2 targets and 3 aim changes: it really fought)");
            }
        }

        /// <summary>The recoil test: a unit with one model (the medium tank: its "v1" and "v2" keys are the same prefab) is judged as v2 in both windows.</summary>
        void RecModelsK87b()
        {
            foreach (var w in recWatch)
            {
                var u = w.o != null ? OzV2Units.Of(w.o.Type) : null;
                if (u == null || u.V1Key != u.V2Key || w.model == 2) continue;
                Note($"k87b: the {S.Label} has one model: its '{w.tag}' window is judged as that model (v2), not as the light tank v1");
                w.model = 2;
            }
        }

        /// <summary>RecVerdict's sample k frames after the shot (12: the head at home, 21: the hull settled). The heavy (two barrels 0.36 s apart)
        /// as before: after its last shot. A single-barrel gun whose next shot comes later than that: after its first shot (before the next).</summary>
        int RecSampleK87b(RecWin c, int n, int last, int k)
        {
            if (S.Type == OzHeavyTank.Type || c.shots.Count < 2) return Mathf.Min(n - 1, last + k);
            int first = c.shots[0], second = c.shots[1];
            return second > first + k ? Mathf.Min(n - 1, first + k) : Mathf.Min(n - 1, last + k);
        }
    }
}
#endif
