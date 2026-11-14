#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The MEDIUM TANK in the v2 films (View/HeavyV2Films.cs + .Subject.cs; menus OpenZ/Medium Tank/5 .. 16): every job of the light tank v2
    /// (tread, battle, board, fight, snipe, turret, recoil, combat, plant) films the medium tank on the same fixed open spots (a medium tank
    /// fits where a heavy does; every slot asked again with the sim's own tests), and two takes of its own:
    ///  "factory": a red vehicle factory builds one medium tank right after the mech in its list and the tank drives off its door
    ///     (staged and logged: the factory at level 2 when it is lower, the mech's tech level; the build started 4.5 s before its end so the
    ///     take is short; the list order, the build time the rules give and the new tank's health and speed are FACTS);
    ///  "lineup": a light tank v2, a medium tank and a heavy tank v2, red, side by side on the proof's fixed open lineup, hold, drive forward
    ///     together, then fire at a jeep (the sizes measured on the drawn models: lengths, widths, heights; the medium between the two).
    /// </summary>
    public partial class HeavyV2Films
    {
        static readonly FilmSubject MTankSubject = new FilmSubject
        {
            Type = OzMediumTank.Type, Label = "medium tank", Prefix = "mt2", ProofDir = "work/staging/v1_2_20261022/v2_units/medium/proof", BuildMenu = "menu OpenZ/Medium Tank/1",
            Yaw = "TankTurretYaw", Approach = "tank_Board_Approach", ShotKey = "tankshot", Tracks = new[] { "Tank_track_left", "Tank_track_right" },
            PrefabV2 = () => OzMediumTank.Models.PrefabV2, Version = () => 2, Pin = (o, v) => { },
        };

        /// <summary>The drawn size of a view (m): its renderers' world box, the wreck holders and effects left out.</summary>
        static Vector3 DrawnSize(UnitView v, out Bounds box)
        {
            box = default; bool any = false;
            if (!v) return Vector3.zero;
            foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                bool frag = false; for (var p = r.transform; p; p = p.parent) if (p.name == "OZ_WreckFragments") { frag = true; break; }
                if (frag || r.name.Contains("Antenna")) continue;
                if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds);
            }
            return any ? box.size : Vector3.zero;
        }

        // ================================================================================================ "factory": built after the mech, drives off
        IEnumerator MTankFactory()
        {
            var team = g.player;
            var f = W.Objects.Where(o => o.Owner == team && o.Kind == ZKind.Building && o.Type == "vehicle_factory" && !o.Destroyed).OrderBy(o => o.Id).FirstOrDefault()
                    ?? W.Objects.Where(o => o.Owner == team && o.IsFort && !o.Destroyed).OrderBy(o => o.Id).FirstOrDefault();
            if (f == null) { SpotFail("no " + team + " vehicle factory or fort on " + g.level); yield break; }
            Fact($"switch ZWorld.MTankDeviation {ZWorld.MTankDeviation} (the game's start sets it; DV145)");
            if (f.Level < 2) { Note($"staged: the {f.Type} at tile ({f.LocX / 16},{f.LocY / 16}) raised from level {f.Level} to 2 (the mech's tech level)"); f.Level = 2; }
            var list = ZBuildLists.For(f.Type, f.BuildLevel).ToList();
            int im = list.IndexOf(ZMTank.Mech), it = list.IndexOf(ZMTank.Type);
            Fact($"the {f.Type}'s list at level {f.BuildLevel}: {string.Join(", ", list)}: the medium tank right after the mech: {(im >= 0 && it == im + 1 ? "PASS" : "FAIL")}");
            hero = null; follow.Clear(); follow.Add(f);
            var before = new HashSet<int>(W.Objects.Select(o => o.Id));
            bool ok = W.StartBuilding(team, f, ZMTank.Type);
            double full = f.BFinal - f.BInit;
            Fact($"StartBuilding({ZMTank.Type}) on the {f.Type}: {(ok ? "PASS" : "FAIL")}; its build time under the rules now {full:0.0} s (the mech's base {W.S.Unit(ZMTank.Mech).build_time} s x this factory's flags and health)");
            if (!ok) yield break;
            yield return Hold(1.0f);
            yield return StartTake();
            yield return TakeWait(1.5f);
            f.BInit = W.Time - (full - 4.5); f.RecalcBuildTime();
            Note("staged: the build started 4.5 s before its end (the take shows the last seconds, the door and the drive off)");
            ZObject made = null; int startF = frameNo;
            while (!done && made == null && frameNo - startF < 20 * Fps)
            {
                foreach (var o in W.Objects) if (!before.Contains(o.Id) && o.Type == ZMTank.Type && o.Owner == team) { made = o; break; }
                yield return null;
            }
            if (made == null) { Fact("no medium tank came out of the factory within 20 s: FAIL"); StopTake(); yield break; }
            hero = made; follow.Clear(); follow.Add(made);
            int x0 = made.CenterX, y0 = made.CenterY;
            Fact($"a medium tank came out (id {made.Id}): {made.MaxHealth} HP, speed {made.MoveSpeed} (the mech's {W.S.Unit(ZMTank.Mech).MaxHealth} HP / {W.S.Unit(ZMTank.Mech).move_speed}), drawn as {(OzMediumTank.Models.IsV2(View(made) ? View(made).transform : null) ? "the medium tank model" : "NO VIEW / WRONG MODEL")}: {(View(made) ? "PASS" : "FAIL")}");
            yield return TakeWait(9f);
            double moved = System.Math.Sqrt((made.CenterX - x0) * (made.CenterX - x0) + (made.CenterY - y0) * (made.CenterY - y0)) / 8.0;
            Fact($"it drove off the door: {moved:0.0} m in 9 s: {(moved > 3 ? "PASS" : "FAIL")}; the tread on the drive {TreadV(View(made))}");
            StopTake(); WriteCrops();
            Fact("pass rule (MP4): the factory's door opens, the medium tank rolls out with its tracks turning on the ground (the tread steps), its turret and hull steady, the door shuts; the build menu shows MEDIUM TANK right after MECH");
        }

        // ================================================================================================ "lineup": light / medium / heavy side by side
        ZObject Pinned(string type, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", type, tx * 16, ty * 16, t);
            if (type == OzLightTank.Type) OzLightTank.Models.Pin(o, 2);
            if (type == OzHeavyTank.Type) OzHeavyTank.Pin(o, 2);
            g.SpawnUnitView(o); cast.Add((o, tag));
            Note($"{tag}: {t} {type}, id {o.Id}, tile ({tx},{ty}){(W.SnipeSafeSpot(o) ? ", ON A BRIDGE / DOOR SPOT" : "")}");
            return o;
        }
        IEnumerator MTankLineup()
        {
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red_v1", "red_v2", "neutral_v1", "jeep" }) { if (!Spot(n, out int sx, out int sy, "proof")) yield break; at[n] = (sx, sy); }
            var lt = Pinned(OzLightTank.Type, at["red_v1"].x, at["red_v1"].y, Team.Red, "red light tank v2");
            var mt = Pinned(OzMediumTank.Type, at["red_v2"].x, at["red_v2"].y, Team.Red, "red medium tank");
            var hv = Pinned(OzHeavyTank.Type, at["neutral_v1"].x, at["neutral_v1"].y, Team.Red, "red heavy tank v2");
            foreach (var (o, tag) in new[] { (lt, "light"), (mt, "medium"), (hv, "heavy") })
            {
                if (!CheckHeavy(o, tag, false)) yield break;
                o.MaxHealth = 100000000; o.SetHealth(o.MaxHealth); o.AttackRadius = 0;
            }
            Note("staged: the three tanks cannot die and hold their fire until the last part");
            hero = mt; follow.Clear(); follow.AddRange(new[] { lt, mt, hv });
            yield return Hold(2.0f);
            var sz = new[] { lt, mt, hv }.Select(o => DrawnSize(View(o), out _)).ToArray();
            Fact($"drawn sizes (m, length x width x height, antennas out): light v2 {sz[0].z:0.00} x {sz[0].x:0.00} x {sz[0].y:0.00}, medium {sz[1].z:0.00} x {sz[1].x:0.00} x {sz[1].y:0.00}, heavy v2 {sz[2].z:0.00} x {sz[2].x:0.00} x {sz[2].y:0.00}: " +
                 $"the medium between the two in length: {(sz[1].z > sz[0].z && sz[1].z < sz[2].z ? "PASS" : "FAIL")}");
            yield return StartTake();
            yield return TakeWait(4f);
            foreach (var o in new[] { lt, mt, hv }) Order(o, MoveTo((o.LocX >> 4), (o.LocY >> 4) - 8, false));
            Note("orders: all three drive 8 tiles north side by side (each at its own speed)");
            yield return TakeWait(7f);
            var jeep = Vehicle("jeep", at["jeep"].x, at["jeep"].y - 6, Team.Blue, "blue jeep (target)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0;
            foreach (var o in new[] { lt, mt, hv }) { o.AttackRadius = o.U.attack_radius; Order(o, Attack(jeep)); }
            Note("orders: all three attack an unkillable blue jeep (each shot: barrel slide, turret kick, hull rock; the tracks stay on the ground)");
            yield return TakeWait(8f);
            StopTake(); WriteCrops();
            Fact("pass rule (MP4): light, medium and heavy side by side, the medium visibly between them in size; the three drive with their tracks rolling on the ground; on each of the medium's shots its barrel slides into the dome, the dome kicks and the hull rocks a little while its tracks stay planted; the light tank's shot sound");
        }
    }
}
#endif
