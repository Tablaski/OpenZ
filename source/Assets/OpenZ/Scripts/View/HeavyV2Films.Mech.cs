#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The mech in the v2 films (View/HeavyV2Films.cs + .Subject.cs; menus OpenZ/Mech v2/5 .. 15). The shared jobs (battle, board,
    /// board_close, fight, snipe, turret, recoil, combat) film the mech on the same fixed open spots of L02 (a mech fits where a heavy does;
    /// every slot asked again with the sim's own tests), with its node names (its waist as the turn joint, its boarding approach behind the
    /// backpack's rungs) and its rocket sound, into v2_units/mech/proof/ (take names mc2_*). The track jobs (tread, plant) are the tanks'; the
    /// mech's two of its own:
    ///  "walk" (item 146 for the mech, its feet are its tracks): a red v2 and a red v1 (unhurt) walk the track test's route (k81d: over the
    ///     canal bank's real slope, down to the river bridge, stand there 3 s firing at a blue jeep (staged: unkillable, no weapon), over the
    ///     bridge's ramps and deck to the far side, then turn in place and walk back), at the game's play zoom; every frame, per mech and foot
    ///     (View/MechV2Proof.cs MechFeetWatch): planted or swinging, the sole's lowest point against the ground under it, a standing foot's
    ///     drift and turn, the body's tilt; PASS: no standing sole more than 3 cm in or over the ground, no standing foot moving, no swinging
    ///     foot under the ground, the body rocking above them; CSV mc2_walk_feet_L02.csv;
    ///  "walk_close": the same walk with the camera at 32 m on v2 (the walk close at RTS distance).
    /// </summary>
    public partial class HeavyV2Films
    {
        static readonly FilmSubject MechSubject = new FilmSubject
        {
            Type = OzMech.Type, Label = "mech", Prefix = "mc2", ProofDir = "work/staging/v1_2_20261022/v2_units/mech/proof", BuildMenu = "menu OpenZ/Mech v2/1",
            Yaw = "MechWaistYaw", Approach = "mech_Board_Approach", ShotKey = "mech_rocket", Tracks = new string[0],
            PrefabV2 = () => OzMech.Models.PrefabV2, Version = () => OzMech.Version, Pin = (o, v) => OzMech.Models.Pin(o, v),
        };

        readonly List<MechFeetWatch> mechFeet = new List<MechFeetWatch>();
        string mechPhase = "-"; float mechLast = -1f;

        /// <summary>Every frame of a take (OnFrame, after every LateUpdate): the watched mechs' feet.</summary>
        void SampleMech(Texture2D tex)
        {
            if (mechFeet.Count == 0) return;
            float dt = mechLast < 0f ? 1f / Fps : Mathf.Max(1e-4f, TakeT - mechLast); mechLast = TakeT;
            foreach (var w in mechFeet) w.Sample(g, frameNo, TakeT, mechPhase, dt);
        }

        /// <summary>"walk" / "walk_close": the mech's feet on the ground while it walks, turns in place, stands and fires (item 146 for the mech).</summary>
        IEnumerator MechWalkTake(bool close)
        {
            if (S.Type != OzMech.Type) { Finish("the 'walk' takes are the mech's (menu OpenZ/Mech v2/14, 15)"); yield break; }
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "v2", "v1", "bridge north", "v2 south", "v1 south", "jeep" }) { if (!Spot(n, out int sx, out int sy, "plant")) yield break; at[n] = (sx, sy); }
            foreach (var kv in PlantBank) { at[kv.Key] = kv.Value; Note($"bank stretch {kv.Key}: tile ({kv.Value.x},{kv.Value.y}) (k81d: the canal bank's real slope)"); }
            var v2 = Heavy(at["v2"].x, at["v2"].y, Team.Red, 2, "red v2 (feet test)"); Unhurt(v2);
            var v1 = Heavy(at["v1"].x, at["v1"].y, Team.Red, 1, "red v1 (feet test)"); Unhurt(v1);
            if (!CheckHeavy(v2, "red v2 (feet test)", false) || !CheckHeavy(v1, "red v1 (feet test)", false)) yield break;
            var jeep = Vehicle("jeep", at["jeep"].x, at["jeep"].y, Team.Blue, "blue jeep (the target)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); W.UpdateObjectHealth(jeep); jeep.Damage = 0; jeep.AttackRadius = 0; jeep.MoveSpeed = 0;   // MoveSpeed 0: Zod's DodgeMissile never moves it out of reach
            Note("staged: the jeep cannot be destroyed and has no weapon (the target the mechs fire at while they stand at the bridge)");
            hero = v2; follow.Add(v2); if (!close) follow.Add(v1);
            if (close) { h0 = CloseHeight; Note($"the camera at {h0:0} m on v2 (the walk close at RTS distance)"); }
            yield return Hold(2.5f);
            mechFeet.Add(new MechFeetWatch("v2", View(v2), 2)); mechFeet.Add(new MechFeetWatch("v1", View(v1), 1));
            foreach (var w in mechFeet) Fact($"{w.Tag}: MechWalk legs and both soles found: {(w.Ok ? "PASS" : "FAIL")} (MECH FOOT IK {(MechWalk.On ? "ON" : "OFF")})");
            yield return StartTake();
            yield return TakeWait(1.0f);
            // the way out: v2 first, v1 4 s behind; then both turn in place and walk the way back
            for (int phase = 0; phase < 2; phase++)
            {
                mechPhase = phase == 0 ? "the way out" : "the way back (turns in place first)";
                Note(mechPhase);
                var a = new Leg { o = phase == 0 ? v2 : v1 }; var b = new Leg { o = phase == 0 ? v1 : v2 };
                if (phase == 0)
                {
                    a.goals = new List<(int, int, float)> { (at["bank v2"].x, at["bank v2"].y, 0f), (at["bridge north"].x, at["bridge north"].y, 3f), (at["v2 south"].x, at["v2 south"].y, 0f) };
                    b.goals = new List<(int, int, float)> { (at["bank v1"].x, at["bank v1"].y, 0f), (at["bridge north"].x, at["bridge north"].y, 3f), (at["v1 south"].x, at["v1 south"].y, 0f) };
                }
                else
                {
                    a.goals = new List<(int, int, float)> { (at["bridge north"].x, at["bridge north"].y, 3f), (at["bank v1"].x, at["bank v1"].y, 0f), (at["v1"].x, at["v1"].y, 0f) };
                    b.goals = new List<(int, int, float)> { (at["bridge north"].x, at["bridge north"].y, 3f), (at["bank v2"].x, at["bank v2"].y, 0f), (at["v2"].x, at["v2"].y, 0f) };
                }
                int t0 = frameNo;
                while (!done && frameNo - t0 < 130 * Fps && !(a.Done && b.Done))
                {
                    LegStep(a);
                    if (frameNo - t0 > 4 * Fps) LegStep(b);
                    yield return null;
                }
                Note($"{mechPhase}: done after {(frameNo - t0) / (float)Fps:0.0} s{(a.Done && b.Done ? "" : " (CUT at 130 s)")}; v2 at tile ({v2.CenterX >> 4},{v2.CenterY >> 4}), v1 at ({v1.CenterX >> 4},{v1.CenterY >> 4})");
                mechPhase = "-";
                yield return TakeWait(1.0f);
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            var sb = new StringBuilder(MechFeetWatch.CsvHead + "\n");
            foreach (var w in mechFeet) foreach (var r in w.Rows) sb.Append(r).Append('\n');
            string csv = Path.Combine(Dir, take + "_feet.csv"); File.WriteAllText(csv, sb.ToString());
            Fact($"the feet against the ground every frame: {csv}");
            foreach (var w in mechFeet)
            {
                bool pass = w.Pass(true) && w.Plants >= 12 && w.TiltMax > 0.3f;
                Fact($"feet {w.Summary()}: {(pass ? "PASS" : "FAIL")} (standing feet never more than 3 cm in or over the ground and never moving, swinging feet never under it, also while it turns in place; steps taken; the body rocking above them)");
            }
            Fact($"steps in the take (sound log): mech_step {Count("mech_step")}, mech_servo {Count("mech_servo")}, mech_stomp {Count("mech_stomp")}; shots: mech_rocket {Count("mech_rocket")}");
            Fact("pass rule (the lines above + the MP4): each foot lands flat on the ground and stays exactly where it landed while it carries the weight, over the canal bank's slope, the bridge's ramps and deck and the flat, while the mech turns in place and while it stands and fires; only the body (and its upper body on the waist) sways and rocks a little above the legs; every landing has its step sound and a footprint");
        }
    }
}
#endif
