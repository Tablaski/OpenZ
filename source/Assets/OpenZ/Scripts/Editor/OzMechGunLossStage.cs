#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// camera beside a repair station: L16 tiles (16,110) / (92,29), L17 (93,21) / (7,20)):
    ///  23 stage at the RTS camera's look point: a red mech v1 (north) and v2 (south) at 15 % health (their drivers cannot be sniped), a
    ///     blue light tank 7 tiles east (it cannot die or move; its shell 700 HP: a heavy hit by the rule that a mech at 15 % survives) that
    ///     shells the v1 mech until its gun is lost, then the v2 mech; the mechs fire their rockets at it and, while they still have the gun,
    ///     the hand gun at a lone blue grunt beside each (harmless, cannot die). Until its gun is lost a mech is put back to 15 % after each
    ///     hit (a draw is a coin: it may take a few shells). Then the tank stops. The counters are reset.
    ///  24 send both staged mechs to the repair station nearest to them (made red for the stage if it is not), as the R key does.
    ///  25 print the counters with PASS / FAIL lines: each mech lost its gun, no hand-gun shot after, a wreck piece fell for each and settled,
    ///     the clanks, repaired to full health with the gun still gone; the view's counters (View/MechGunLossView.cs).
    /// </summary>
    public static class OzMechGunLossStage
    {
        const string M = "OpenZ/Mech v2/";
        const int Big = 100000000, ShellHp = 700;
        const double Pin = 0.15;
        sealed class Staged { public ZObject mech; public int model, shotsAtLoss = -1; public bool onPad, fullAfter; public double lostAt = -1; }
        static readonly List<Staged> staged = new List<Staged>();
        static readonly List<ZObject> grunts = new List<ZObject>();
        static ZObject tank;
        static bool hooked;
        static float real0;

        static bool Ready(out ZGame g)
        {
            g = ZGame.I;
            if (Application.isPlaying && g && g.World != null) return true;
            Debug.LogWarning("[OzMechGunLoss] enter Play on a level first (level 16 or 17 beside a repair station for the repair step)");
            return false;
        }

        static ZObject Mech(ZGame g, int model, int tx, int ty)
        {
            var o = g.World.CreateObject("vehicle", OzMech.Type, tx * 16 - 16, ty * 16 - 16, Team.Red);
            OzMech.Models.Pin(o, model); g.SpawnUnitView(o);
            if (o.Drivers.Count > 0) o.Drivers[0] = Big;
            o.SetHealth(Mathf.Max(1, (int)(o.MaxHealth * Pin)));
            return o;
        }

        [MenuItem(M + "23 Gun loss: stage at the camera (Play)", false, 23)]
        static void Stage()
        {
            if (!Ready(out var g)) return;
            var W = g.World; var cam = Object.FindFirstObjectByType<RtsCamera>();
            var f = cam ? cam.focus : Vector3.zero;
            int tx = Mathf.RoundToInt(f.x * 8f / 16f), ty = Mathf.RoundToInt(-f.z * 8f / 16f);
            MechGunLossView.Ensure(); MechGunLossView.ResetStats(); real0 = Time.unscaledTime;
            staged.Clear(); grunts.Clear();
            staged.Add(new Staged { mech = Mech(g, 1, tx, ty - 3), model = 1 });
            staged.Add(new Staged { mech = Mech(g, 2, tx, ty + 3), model = 2 });
            tank = W.CreateObject("vehicle", "light", (tx + 7) * 16 - 16, ty * 16 - 16, Team.Blue);
            tank.MaxHealth = Big; tank.SetHealth(Big); tank.MoveSpeed = 0; tank.RealMoveSpeed = 0; tank.Damage = ShellHp; if (tank.Drivers.Count > 0) tank.Drivers[0] = Big;
            g.SpawnUnitView(tank);
            foreach (var s in staged)
            {
                var m = s.mech;
                m.HandGunGunPoint(tank.CenterX, tank.CenterY, out double gx, out double gy);
                double ax = tank.CenterX - gx, ay = tank.CenterY - gy, al = System.Math.Sqrt(ax * ax + ay * ay), c = System.Math.Cos(0.35), sn = System.Math.Sin(0.35);
                int x = (int)(gx + (ax * c - ay * sn) / al * 60), y = (int)(gy + (ax * sn + ay * c) / al * 60);
                var o = W.CreateRobotGroup("grunt", x - 8, y - 8, Team.Blue, 1);
                o.MaxHealth = Big; o.SetHealth(Big); o.Damage = 0; o.MoveSpeed = 0; o.RealMoveSpeed = 0;
                g.SpawnUnitView(o); grunts.Add(o);
                W.SendWaypoints(Team.Red, m, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = tank.Id, x = tank.CenterX, y = tank.CenterY } });
            }
            Aim(W, staged[0].mech);
            if (!hooked) { hooked = true; EditorApplication.update += Watch; }
            Debug.Log($"[OzMechGunLoss] staged at tile ({tx},{ty}): red mech v1 #{staged[0].mech.Id} north, v2 #{staged[1].mech.Id} south at {Pin * 100:0} % health; the blue light tank #{tank.Id} " +
                      $"7 tiles east shells v1 first ({ShellHp} HP: heavy by the rule, heavy from {W.GunLossHeavyHp} HP), then v2; rule DV149 {(ZWorld.MechGunLossDeviation ? "ON" : "OFF ('Zod exact')")}, DV146 {(ZWorld.MechHandGunDeviation ? "ON" : "OFF")}; " +
                      "menu 24 sends them to repair, menu 25 prints the counters");
        }

        static void Aim(ZWorld W, ZObject m) =>
            W.SendWaypoints(Team.Blue, tank, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = m.Id, x = m.CenterX, y = m.CenterY } });

        /// <summary>Until its gun is lost a staged mech is put back to 15 % after a hit; the tank moves on to the next mech, then stops.</summary>
        static void Watch()
        {
            if (!Application.isPlaying) { staged.Clear(); grunts.Clear(); tank = null; return; }
            if (staged.Count == 0) return;
            var g = ZGame.I; if (!g || g.World == null) return;
            foreach (var s in staged)
            {
                var m = s.mech; if (m == null || !OzV11Kit.Alive(m)) continue;
                if (!m.HandGunLost) { if (m.Health < (int)(m.MaxHealth * Pin)) m.SetHealth((int)(m.MaxHealth * Pin)); continue; }
                if (s.lostAt < 0) { s.lostAt = m.HandGunLostAt; s.shotsAtLoss = m.HandGunShots; }
                if (m.OnAnyRepairPad) s.onPad = true;
                if (s.onPad && m.Health >= m.MaxHealth) s.fullAfter = true;
            }
            if (tank == null || !OzV11Kit.Alive(tank) || tank.Damage == 0) return;
            var next = staged.FirstOrDefault(s => s.mech != null && OzV11Kit.Alive(s.mech) && !s.mech.HandGunLost);
            if (next == null) { tank.Waypoints.Clear(); tank.Damage = 0; Debug.Log("[OzMechGunLoss] both staged mechs lost their guns: the tank stops; menu 24 sends them to repair"); return; }
            if (tank.Waypoints.Count == 0 || tank.Waypoints[0].refId != next.mech.Id) Aim(g.World, next.mech);
        }

        [MenuItem(M + "24 Gun loss: send the staged mechs to repair (Play)", false, 24)]
        static void Repair()
        {
            if (!Ready(out var g)) return;
            var W = g.World;
            if (staged.Count == 0) { Debug.LogWarning("[OzMechGunLoss] stage first (menu 23)"); return; }
            if (tank != null) { tank.Waypoints.Clear(); tank.Damage = 0; }
            var m0 = staged[0].mech;
            var st = W.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "repair" && !o.Destroyed)
                              .OrderBy(o => (long)(o.CenterX - m0.CenterX) * (o.CenterX - m0.CenterX) + (long)(o.CenterY - m0.CenterY) * (o.CenterY - m0.CenterY)).FirstOrDefault();
            if (st == null) { Debug.LogWarning("[OzMechGunLoss] this level has no repair station (level 16 or 17 have two)"); return; }
            if (st.Owner != Team.Red)
            {
                var flag = W.Flags.FirstOrDefault(fl => fl.LinkedObjects.Contains(st)) ?? (st.ZoneIndex >= 0 ? W.Flags.FirstOrDefault(fl => fl.ZoneIndex == st.ZoneIndex) : null);
                if (flag != null) W.AwardZone(flag, Team.Red); else st.Owner = Team.Red;
            }
            int sent = 0;
            foreach (var s in staged)
            {
                var m = s.mech; if (m == null || !OzV11Kit.Alive(m)) continue;
                var wp = ZWaypoint.Move(st.CenterX, st.CenterY, true); wp.refId = st.Id; wp.mode = WpMode.UnitRepair; wp.attackTo = false; wp.x = st.CenterX - 8; wp.y = st.CenterY;
                if (W.SendWaypoints(Team.Red, m, new[] { wp })) sent++;
            }
            Debug.Log($"[OzMechGunLoss] {sent} staged mech(s) sent to the repair station #{st.Id} at tile ({st.CenterX / 16},{st.CenterY / 16}) (red for the stage); one at a time on its pad; menu 25 after both are out");
        }

        [MenuItem(M + "25 Gun loss: print the counters (Play)", false, 25)]
        static void Print()
        {
            if (!Ready(out var g)) return;
            var sb = new StringBuilder("[OzMechGunLoss] ").Append(MechGunLossView.Stats).Append('\n');
            bool allLost = staged.Count == 2, noShot = true, repaired = staged.Count == 2;
            foreach (var s in staged)
            {
                var m = s.mech; if (m == null) continue;
                int after = s.shotsAtLoss >= 0 ? m.HandGunShots - s.shotsAtLoss : -1;
                sb.Append($"  mech #{m.Id} v{s.model}: health {m.Health * 100L / System.Math.Max(1, m.MaxHealth)} %, gun lost {m.HandGunLost} at {m.HandGunLostAt:0.00} s after {m.GunLossRolls} draw(s), " +
                          $"hand-gun shots before {(s.shotsAtLoss >= 0 ? s.shotsAtLoss : m.HandGunShots)}, after {after}; on a repair pad {s.onPad}, full health after the loss {s.fullAfter}\n");
                allLost &= m.HandGunLost; noShot &= after == 0; repaired &= s.fullAfter && m.HandGunLost;
            }
            int falls = 0, fell = 0, settled = 0;
            foreach (var f in MechGunLossView.Falls)
            {
                falls++;
                float drop = f.startY - (f.lowY == float.MaxValue ? f.startY : f.lowY);
                if (drop > 1f) fell++;
                if (f.settledAt >= 0f) settled++;
                sb.Append($"  fall: mech #{f.mech} v{f.model}, {f.parts} parts, from {f.startY:0.00} m down {drop:0.00} m, thrown at {f.v0.magnitude:0.0} m/s, settled {(f.settledAt >= 0f ? (f.settledAt - f.t0).ToString("0.0") + " s after" : "not yet")}\n");
            }
            var clanks = OzSoundLog.Since(real0).Count(e => e.key == "dst_debris_metal" && e.outcome != null && e.outcome.StartsWith("PLAY"));
            sb.Append($"  dst_debris_metal played since the stage: {clanks} (the snap at the break + the landing clanks)\n");
            sb.Append(OzV11Kit.Verdict(allLost, "both staged mechs (v1, v2) lost the gun")).Append('\n');
            sb.Append(OzV11Kit.Verdict(allLost && noShot, "no hand-gun shot after the loss")).Append('\n');
            sb.Append(OzV11Kit.Verdict(falls == 2 && MechGunLossView.PiecesMade == 2 && MechGunLossView.NoNode == 0, $"a wreck piece fell for each: {falls} falls, {MechGunLossView.PiecesMade} pieces, {MechGunLossView.NoNode} models without the forearm node")).Append('\n');
            sb.Append(OzV11Kit.Verdict(fell == falls && settled == falls && falls > 0, $"each fell more than 1 m and settled: {fell} / {settled} of {falls}")).Append('\n');
            sb.Append(OzV11Kit.Verdict(clanks >= 2 * falls && falls > 0, $"the clanks: {clanks} for {falls} falls (at least the snap and one landing each)")).Append('\n');
            sb.Append(OzV11Kit.Verdict(repaired, "both repaired on a pad to full health, the gun still gone (menu 24 first)"));
            Debug.Log(sb.ToString());
        }
    }
}
#endif
