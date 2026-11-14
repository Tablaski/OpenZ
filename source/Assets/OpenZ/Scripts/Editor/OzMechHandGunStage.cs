#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    ///  17 stage at the RTS camera's look point on open ground: a red mech v1 (north) and v2 (south), both immortal, ordered to attack a blue
    ///     jeep 7 tiles east (immortal, harmless, standing): their rockets go to the jeep; a blue grunt squad S between (inside both guns'
    ///     arcs: shot by the hand guns in every reload), a blue grunt squad T close north-east (outside both arcs: never shot by them), a
    ///     blue light tank and a blue gun beside S (never shot by the hand guns). The counters are reset.
    ///  18 turn the staged mechs on the spot: each is sent 1 tile back west (it turns about 180 deg on the spot, then steps).
    ///  19 print the counters: the view's (bursts, MG clips, rounds, casings, tinkles, the arm's turns), each staged mech's sim counters
    ///     (shots, hits, its targets by kind, shots at S / T), the walk's stance monitor (MechWalk.K86c.cs).
    /// </summary>
    public static class OzMechHandGunStage
    {
        const string M = "OpenZ/Mech v2/";
        const int Big = 100000000;
        static readonly List<ZObject> mechs = new List<ZObject>();
        static ZObject squadS, squadT, jeep;
        static readonly Dictionary<string, int> byKind = new Dictionary<string, int>();
        static int atS, atT, seenShots;
        static bool hooked;

        static bool Ready(out ZGame g)
        {
            g = ZGame.I;
            if (Application.isPlaying && g && g.World != null) return true;
            Debug.LogWarning("[OzMechHandGun] enter Play on a level first (L02 is open ground)");
            return false;
        }

        static ZObject Mech(ZGame g, int model, int tx, int ty)
        {
            var o = g.World.CreateObject("vehicle", OzMech.Type, tx * 16 - 16, ty * 16 - 16, Team.Red);
            OzMech.Models.Pin(o, model); g.SpawnUnitView(o);
            o.MaxHealth = Big; o.SetHealth(o.MaxHealth); if (o.Drivers.Count > 0) o.Drivers[0] = Big;
            return o;
        }

        static ZObject Still(ZGame g, ZObject o)
        {
            foreach (var m in Squad(o)) { m.MaxHealth = Big; m.SetHealth(Big); m.Damage = 0; m.MoveSpeed = 0; m.RealMoveSpeed = 0; if (m.Drivers.Count > 0) m.Drivers[0] = Big; }
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            return o;
        }

        static IEnumerable<ZObject> Squad(ZObject o) { yield return o; foreach (var m in o.Minions) yield return m; }

        [MenuItem(M + "17 Hand gun: stage a fight at the camera (Play)", false, 17)]
        static void Stage()
        {
            if (!Ready(out var g)) return;
            var W = g.World; var cam = Object.FindFirstObjectByType<RtsCamera>();
            var f = cam ? cam.focus : Vector3.zero;
            int tx = Mathf.RoundToInt(f.x * 8f / 16f), ty = Mathf.RoundToInt(-f.z * 8f / 16f);
            MechHandGunView.Ensure(); MechHandGunView.ResetStats(); MechWalk.ResetK86c(); byKind.Clear(); atS = atT = seenShots = 0;
            mechs.Clear();
            mechs.Add(Mech(g, 1, tx, ty - 3)); mechs.Add(Mech(g, 2, tx, ty + 3));
            jeep = Still(g, W.CreateObject("vehicle", "jeep", (tx + 7) * 16 - 16, ty * 16 - 16, Team.Blue));
            squadS = Still(g, W.CreateRobotGroup("grunt", tx * 16 + 64 - 8, ty * 16 + 16 - 8, Team.Blue));
            squadT = Still(g, W.CreateRobotGroup("grunt", tx * 16 + 40 - 8, ty * 16 - 40 - 8, Team.Blue));
            Still(g, W.CreateObject("vehicle", "light", tx * 16 + 96 - 16, ty * 16 + 48 - 16, Team.Blue));
            Still(g, W.CreateObject("cannon", "gun", tx * 16 + 112 - 16, ty * 16 + 80 - 16, Team.Blue));
            foreach (var m in mechs) W.SendWaypoints(Team.Red, m, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = jeep.Id, x = jeep.CenterX, y = jeep.CenterY } });
            if (!hooked) { hooked = true; MechHandGunView.Seen += Watch; }
            Debug.Log($"[OzMechHandGun] staged at tile ({tx},{ty}): red mech v1 #{mechs[0].Id} north, v2 #{mechs[1].Id} south, attacking the blue jeep #{jeep.Id} 7 tiles east; " +
                      $"grunt squad S #{squadS.Id} (inside both arcs), squad T #{squadT.Id} (outside both arcs), a light tank and a gun beside S; rule {(ZWorld.MechHandGunDeviation ? "ON" : "OFF ('Zod exact')")}");
        }

        /// <summary>Counts the staged mechs' hand-gun shots by target kind (MechHandGunView.Seen: every sim shot the view reads).</summary>
        static void Watch(ZEvent e, ZObject t)
        {
            if (!mechs.Exists(m => m.Id == e.objId)) return;
            seenShots++;
            string k = t == null ? "gone" : t.Kind.ToString();
            byKind.TryGetValue(k, out int n); byKind[k] = n + 1;
            if (t != null && squadS != null && (t == squadS || t.Leader == squadS)) atS++;
            if (t != null && squadT != null && (t == squadT || t.Leader == squadT)) atT++;
        }

        [MenuItem(M + "18 Hand gun + feet: turn the staged mechs on the spot (Play)", false, 18)]
        static void Turn()
        {
            if (!Ready(out var g) || mechs.Count == 0) { Debug.LogWarning("[OzMechHandGun] stage first (menu 17)"); return; }
            foreach (var m in mechs) g.World.SendWaypoints(m.Owner, m, new List<ZWaypoint> { ZWaypoint.Move(m.CenterX - 16, m.CenterY, false) });
            Debug.Log("[OzMechHandGun] both mechs sent 1 tile west (behind them): a turn on the spot, then a step; menu 19 for the feet");
        }

        [MenuItem(M + "19 Hand gun + feet: print the counters (Play)", false, 19)]
        static void Print()
        {
            if (!Ready(out var g)) return;
            var sb = new StringBuilder("[OzMechHandGun] ");
            sb.Append(MechHandGunView.Stats).Append('\n');
            foreach (var m in mechs)
                sb.Append($"  mech #{m.Id} ({(OzMech.Models.VersionOf(m) == 2 ? "v2" : "v1")}): hand-gun shots {m.HandGunShots}, hits {m.HandGunHits}, last rocket at {m.HandGunRocketAt:0.00} s, last shot {m.HandGunLastShot:0.00} s (sim time {g.World.Time:0.00})\n");
            sb.Append($"  staged mechs' shots seen {seenShots}: by target kind {string.Join(", ", byKind)}; at squad S (inside the arcs) {atS}, at squad T (outside the arcs) {atT}\n");
            sb.Append($"  feet (MechWalk.K86c, since the stage): quick landings {MechWalk.QuickLands}, stances over {MechWalk.WarnCm:0} cm / {MechWalk.WarnDeg:0} deg {MechWalk.StanceWarnN}, " +
                      $"largest standing-foot move {MechWalk.StanceMoveMaxCm:0.0} cm / turn {MechWalk.StanceTurnMaxDeg:0.0} deg; while turning on the spot {MechWalk.TurnStanceMoveMaxCm:0.0} cm / {MechWalk.TurnStanceTurnMaxDeg:0.0} deg; frames not posed {MechWalk.UnposedFrames}");
            Debug.Log(sb.ToString());
        }
    }
}
#endif
