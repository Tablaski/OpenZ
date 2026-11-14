#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// editor's playground):
    ///  20 stage at the RTS camera's look point: a red mech v1 (north) and v2 (south), both immortal, ordered to attack a blue jeep 7 tiles
    ///     east (immortal, harmless); beside each mech two lone blue grunts inside its gun's arc, at +25 deg (outward) and -10 deg (inward)
    ///     of its line to the jeep, 60 px from the gun, standing and harmless, each falling after 3 hits and back on its spot 1.5 s after it
    ///     fell: the hand guns switch between the two robots again and again (the arm turns, then fires). The counters are reset.
    ///  21 print the counters: the aim and flash counters (View/MechHandGunAim.cs), the view's (View/MechHandGunView.cs) and each staged
    ///     mech's sim counters (shots, new robots, the ticks a robot waited for the arm, the last turn).
    /// </summary>
    public static class OzMechHandGunAimStage
    {
        const string M = "OpenZ/Mech v2/";
        const int Big = 100000000;
        static readonly List<ZObject> mechs = new List<ZObject>();
        sealed class Spot { public int x, y; public ZObject o; public double fellAt = -1; }
        static readonly List<Spot> spots = new List<Spot>();
        static bool hooked;
        static int respawns;

        static bool Ready(out ZGame g)
        {
            g = ZGame.I;
            if (Application.isPlaying && g && g.World != null) return true;
            Debug.LogWarning("[OzMechHandGunAim] enter Play on a level first (L02 is open ground) or in the Unit editor's playground");
            return false;
        }

        static ZObject Mech(ZGame g, int model, int tx, int ty)
        {
            var o = g.World.CreateObject("vehicle", OzMech.Type, tx * 16 - 16, ty * 16 - 16, Team.Red);
            OzMech.Models.Pin(o, model); g.SpawnUnitView(o);
            o.MaxHealth = Big; o.SetHealth(o.MaxHealth); if (o.Drivers.Count > 0) o.Drivers[0] = Big;
            return o;
        }

        static ZObject Robot(ZGame g, int x, int y)
        {
            var o = g.World.CreateRobotGroup("grunt", x - 8, y - 8, Team.Blue, 1);
            var u = g.World.S.Unit(ZWorld.HandGunType);
            int hp = u != null ? u.Damage * 3 : 78;
            o.MaxHealth = hp; o.SetHealth(hp); o.Damage = 0; o.MoveSpeed = 0; o.RealMoveSpeed = 0;
            g.SpawnUnitView(o);
            return o;
        }

        /// <summary>A point `px` from the mech's gun at `deg` from its line to (lx, ly) (+ = outward), the sim's own gun place.</summary>
        static (int x, int y) At(ZObject m, int lx, int ly, double deg, double px)
        {
            m.HandGunGunPoint(lx, ly, out double gx, out double gy);
            double ax = lx - gx, ay = ly - gy, al = System.Math.Sqrt(ax * ax + ay * ay);
            double c = System.Math.Cos(deg * System.Math.PI / 180), s = System.Math.Sin(deg * System.Math.PI / 180);
            return ((int)(gx + (ax * c - ay * s) / al * px), (int)(gy + (ax * s + ay * c) / al * px));
        }

        [MenuItem(M + "20 Hand gun: stage the target switch at the camera (Play)", false, 20)]
        static void Stage()
        {
            if (!Ready(out var g)) return;
            var W = g.World; var cam = Object.FindFirstObjectByType<RtsCamera>();
            var f = cam ? cam.focus : Vector3.zero;
            int tx = Mathf.RoundToInt(f.x * 8f / 16f), ty = Mathf.RoundToInt(-f.z * 8f / 16f);
            MechHandGunView.Ensure(); MechHandGunView.ResetStats(); MechHandGunAim.ResetStats(); respawns = 0;
            mechs.Clear(); spots.Clear();
            mechs.Add(Mech(g, 1, tx, ty - 3)); mechs.Add(Mech(g, 2, tx, ty + 3));
            var jeep = W.CreateObject("vehicle", "jeep", (tx + 7) * 16 - 16, ty * 16 - 16, Team.Blue);
            jeep.MaxHealth = Big; jeep.SetHealth(Big); jeep.Damage = 0; jeep.MoveSpeed = 0; jeep.RealMoveSpeed = 0; if (jeep.Drivers.Count > 0) jeep.Drivers[0] = Big;
            g.SpawnUnitView(jeep);
            foreach (var m in mechs)
                foreach (double deg in new[] { 25.0, -10.0 })
                {
                    var p = At(m, jeep.CenterX, jeep.CenterY, deg, 60);
                    spots.Add(new Spot { x = p.x, y = p.y, o = Robot(g, p.x, p.y) });
                }
            foreach (var m in mechs) W.SendWaypoints(Team.Red, m, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = jeep.Id, x = jeep.CenterX, y = jeep.CenterY } });
            if (!hooked) { hooked = true; EditorApplication.update += Respawn; }
            Debug.Log($"[OzMechHandGunAim] staged at tile ({tx},{ty}): red mech v1 #{mechs[0].Id} north, v2 #{mechs[1].Id} south, attacking the blue jeep #{jeep.Id} 7 tiles east; " +
                      $"two lone blue grunts beside each gun at +25 / -10 deg (3 hits each, back 1.5 s after they fall); rule {(ZWorld.MechHandGunDeviation ? "ON" : "OFF ('Zod exact')")}; menu 21 for the counters");
        }

        /// <summary>A fallen robot of the stage stands up again on its spot 1.5 s (sim time) after it fell.</summary>
        static void Respawn()
        {
            if (!Application.isPlaying) { spots.Clear(); mechs.Clear(); return; }                            // Play ended: the stage is gone
            if (spots.Count == 0) return;
            var g = ZGame.I; if (!g || g.World == null) return;
            foreach (var s in spots)
            {
                bool down = s.o == null || s.o.Destroyed || s.o.KillAt >= 0;
                if (!down) { s.fellAt = -1; continue; }
                if (s.fellAt < 0) { s.fellAt = g.World.Time; continue; }
                if (g.World.Time - s.fellAt < 1.5) continue;
                s.o = Robot(g, s.x, s.y); s.fellAt = -1; respawns++;
            }
        }

        [MenuItem(M + "21 Hand gun: print the aim and flash counters (Play)", false, 21)]
        static void Print()
        {
            if (!Ready(out var g)) return;
            var sb = new StringBuilder("[OzMechHandGunAim] ");
            sb.Append(MechHandGunAim.Stats).Append('\n').Append(MechHandGunView.Stats).Append('\n');
            sb.Append($"  view bursts {MechHandGunView.Bursts} = first rounds {MechHandGunAim.FirstRounds} = first rounds with a flash {MechHandGunAim.FlashBursts}? " +
                      $"{(MechHandGunView.Bursts > 0 && MechHandGunAim.FlashBursts == MechHandGunAim.FirstRounds && MechHandGunAim.FirstRounds >= MechHandGunView.Bursts - 2 ? "yes" : "NO")}; " +
                      $"first rounds more than 3 deg off the barrel {MechHandGunAim.FirstOver3} (want 0); robots stood up again {respawns}\n");
            foreach (var m in mechs)
                sb.Append($"  mech #{m.Id} ({(OzMech.Models.VersionOf(m) == 2 ? "v2" : "v1")}): hand-gun shots {m.HandGunShots}, hits {m.HandGunHits}, new robots {m.HandGunTargetSwitches}, " +
                          $"ticks a robot waited for the arm {m.HandGunAimWaitTicks}, the last turn {m.HandGunLastTurnDeg:0.0} deg (sim time {g.World.Time:0.00})\n");
            Debug.Log(sb.ToString());
        }
    }
}
#endif
