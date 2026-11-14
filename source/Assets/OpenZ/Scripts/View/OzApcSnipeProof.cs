#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 69 Play proof (rule DV143, Sim/ZObject.ApcSnipe.cs; menu OpenZ/V1.2/Combat: 69 APC Driver Snipe Proof (Play),
    /// Editor/OzApcSnipeMenu.cs). In a running level it removes the computer players (nothing else fires), picks the open spot of the map
    /// farthest from every unit and building, puts an enemy APC there with its driver alone and a sniper squad of the player 5 tiles west of
    /// it, and gives the squad the player's attack order on the APC. Counted: the APC's driver comes up through the roof hatch and fires
    /// (DV87 part C); he is shot out (the APC turns neutral, no driver); the APC stands empty: no orders, stopped, nobody up, enterable, the
    /// snipers let go of it; the player's snipers are sent in and take it (their leader drives, the others ride, DV15). Four pictures at the
    /// normal RTS camera (70 m), full screen: the driver up and firing, the moment he is shot out, the empty APC, the APC taken. Lines and
    /// pictures in <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzApcSnipeProof : MonoBehaviour
    {
        public static string Report = "idle";

        /// <summary></summary>
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var kit = System.IO.Path.Combine(stage, "k69_apc_driver_unkillable");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k69_apc_driver_unkillable");
        }

        public static void Run()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning("[OzApcSnipeProof] start a level in Play first"); return; }
            if (FindFirstObjectByType<OzApcSnipeProof>()) { Debug.LogWarning("[OzApcSnipeProof] already running"); return; }
            Report = "running";
            new GameObject("OzApcSnipeProof").AddComponent<OzApcSnipeProof>();
        }

        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        ZGame g; ZWorld W; RtsCamera rts;

        void Start() { dir = Folder(); System.IO.Directory.CreateDirectory(dir); g = ZGame.I; W = g.World; rts = FindFirstObjectByType<RtsCamera>(); StartCoroutine(All()); }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzApcSnipeProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        IEnumerator Shot(string name)
        {
            string p = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p); Log("   picture " + name + ".png");
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }
        void Look(ZObject a, ZObject b)
        {
            if (!rts || a == null || b == null) return;
            var p = (g.CenterWorld(a, false) + g.CenterWorld(b, false)) * 0.5f;
            rts.focus = new Vector3(p.x, 0f, p.z); rts.SetHeightImmediate(70f);
        }

        /// <summary>The 16 px tile, on a 4-tile grid, farthest from every unit and building of any team (flags and map items do not count),
        /// with room for an APC (2 x 2) there and a squad 5 tiles west of it.</summary>
        bool Spot(out int cx, out int cy, out int sx, out int sy)
        {
            cx = cy = sx = sy = 0; long best = -1;
            var things = W.Objects.Where(o => !o.Destroyed && (o.IsUnit || o.Kind == ZKind.Building)).ToList();
            for (int ty = 8; ty < W.Map.height - 8; ty += 4)
                for (int tx = 12; tx < W.Map.width - 8; tx += 4)
                {
                    if (!OzCheatSpawner.FreeTile(W, tx, ty, 2, false, out int vx, out int vy) || Mathf.Abs(vx - tx) > 1 || Mathf.Abs(vy - ty) > 1) continue;
                    if (!OzCheatSpawner.FreeTile(W, vx - 5, vy, 1, true, out int rx, out int ry) || Mathf.Abs(rx - (vx - 5)) > 1 || Mathf.Abs(ry - vy) > 1) continue;
                    long near = long.MaxValue; int px = vx * 16 + 16, py = vy * 16 + 16;
                    foreach (var o in things) { long dx = o.CenterX - px, dy = o.CenterY - py, d = dx * dx + dy * dy; if (d < near) near = d; }
                    if (near > best) { best = near; cx = vx; cy = vy; sx = rx; sy = ry; }
                }
            if (best >= 0) Log($"spot: APC tile ({cx},{cy}), squad tile ({sx},{sy}); nearest other unit or building {Mathf.Sqrt(best) / 16f:0} tiles away");
            return best >= 0;
        }

        static long SquadHp(System.Collections.Generic.List<ZObject> squad) => squad.Sum(m => m.Destroyed ? 0L : m.Health);

        IEnumerator All()
        {
            if (W == null) { Check("a level in Play", false); Finish(); yield break; }
            var me = g.player; var them = me == Team.Red ? Team.Blue : Team.Red;
            Log($"1.2 item 69 APC driver snipe proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s, the player {me}");
            Check($"P0 the rule's switches are on (ZWorld.ApcSnipeDeviation {ZWorld.ApcSnipeDeviation}, the hatch drivers DV87 {ZWorld.DriverFireDeviation}, driver sniping DC2 {ZWorld.SnipeDeviation}, the APC crew DV15 {ZWorld.ApcCrewDeviation})",
                  ZWorld.ApcSnipeDeviation && ZWorld.DriverFireDeviation && ZWorld.SnipeDeviation && ZWorld.ApcCrewDeviation);
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            Log($"set-up: the computer players removed ({bots}), daylight held");
            if (!Spot(out int ctx, out int cty, out int stx, out int sty)) { Check("an open spot for the APC and the squad", false); Finish(); yield break; }
            var apc = W.CreateObject("vehicle", "apc", ctx * 16, cty * 16, them);
            var sn = W.CreateRobotGroup("sniper", stx * 16, sty * 16, me);
            if (apc == null || sn == null) { Check("the APC and the sniper squad were placed", false); Finish(); yield break; }
            g.SpawnUnitView(apc); g.SpawnUnitView(sn); foreach (var m in sn.Minions) g.SpawnUnitView(m);
            var squad = new[] { sn }.Concat(sn.Minions).ToList();
            int pool0 = apc.Drivers.Count > 0 ? apc.Drivers[0] : 0;
            Log($"placed: {them} APC #{apc.Id} with {apc.Drivers.Count} driver ({apc.DriverType}, {pool0} HP) and {apc.Passengers.Count} passengers, the player's sniper squad #{sn.Id} x{squad.Count}, {Mathf.Sqrt((apc.CenterX - sn.CenterX) * (apc.CenterX - sn.CenterX) + (apc.CenterY - sn.CenterY) * (apc.CenterY - sn.CenterY)):0} px apart");
            Look(apc, sn);
            yield return Real(1.0f);
            bool ordered = W.SendWaypoints(me, sn, new[] { new ZWaypoint { mode = WpMode.Attack, refId = apc.Id, x = apc.CenterX, y = apc.CenterY } });
            Check("P1 the player's attack order on the APC", ordered);

            // the driver comes up (the APC's first shot follows ZWorld.CraneGunPopSeconds later; snipers often shoot him out a moment after it)
            float t0 = Time.time; float upAt = -1f; double upSim = -1; bool pictured = false; long hp0 = SquadHp(squad), lostUp = 0;
            while (Time.time - t0 < 20f && apc.Owner == them && apc.Drivers.Count > 0)
            {
                long hp = SquadHp(squad);
                if (apc.ApcDriverUp) { if (upAt < 0f) { upAt = Time.time - t0; upSim = W.Time; } lostUp += System.Math.Max(0L, hp0 - hp); }
                hp0 = hp;
                if (!pictured && upAt >= 0f && apc.ApcDriverUp && W.Time - upSim >= ZWorld.CraneGunPopSeconds)
                { pictured = true; Look(apc, sn); yield return Shot("k69_1_driver_up_firing_h70"); continue; }
                yield return null;
            }
            Check($"P2 the APC's lone driver comes up through the roof hatch to fire at the snipers ({(upAt >= 0f ? $"{upAt:0.00} s after the order" : "never")}; the snipers lost {lostUp} HP to his rifle while he was up; picture taken {pictured})", upAt >= 0f);
            int lowest = apc.Drivers.Count > 0 ? apc.Drivers[0] : 0;
            bool sniped = apc.Owner == Team.Neutral && apc.Drivers.Count == 0 && !apc.Destroyed;
            // the empty APC, looked at in the frame of the snipe (its server pass has run: orders gone, attackers let go) and again 0.5 s
            // after the second picture (an idle robot of either side that stands near an empty vehicle may later walk over and take it by
            // itself, as with every sniped vehicle)
            bool emptyNow = sniped && apc.Passengers.Count == 0 && apc.Waypoints.Count == 0 && !apc.ApcDriverUp && apc.CanBeEntered() && !apc.CanBeSniped();
            bool letGo = squad.All(m => m.Destroyed || m.AttackObject != apc);
            Check($"P3 the snipers shoot the driver out ({(Time.time - t0):0.0} s after the order; the driver's health went {pool0} -> {(sniped ? 0 : lowest)}; the APC {apc.Owner}, drivers {apc.Drivers.Count}, hull {apc.Health}/{apc.MaxHealth})", sniped);
            if (sniped) { yield return Real(0.35f); Look(apc, sn); yield return Shot("k69_2_driver_shot_out_h70"); }
            yield return Real(0.5f);
            bool still = apc.Owner == Team.Neutral && apc.Drivers.Count == 0 && apc.Waypoints.Count == 0 && !apc.IsMoving && !apc.ApcDriverUp;
            Check($"P4 the APC stands empty: in the frame of the snipe neutral, no driver, no passengers, no orders, nobody drawn up, enterable, not snipable ({emptyNow}), every sniper let go of it ({letGo}); a moment later still empty and stopped ({still})", emptyNow && letGo && still);
            Look(apc, sn); yield return Shot("k69_3_empty_apc_h70");
            Log($"the snipers: {string.Join(", ", squad.Select(m => m.Destroyed ? "dead" : m.Health + " HP"))}");

            // the player's snipers are sent in: the APC is theirs (the leader drives, the others ride)
            var leader = squad.FirstOrDefault(m => !m.Destroyed && m.KillAt < 0 && !m.IsMinion) ?? squad.FirstOrDefault(m => !m.Destroyed && m.KillAt < 0);
            bool sent = leader != null && apc.Owner == Team.Neutral && W.SendWaypoints(me, leader, new[] { new ZWaypoint { mode = WpMode.Enter, refId = apc.Id, x = apc.CenterX, y = apc.CenterY } });
            float t2 = Time.time;
            while (Time.time - t2 < 15f && apc.Owner == Team.Neutral) yield return null;
            yield return Real(1.0f);
            bool taken = apc.Owner == me && apc.Drivers.Count == 1;
            Check($"P5 the player's snipers sent in ({sent}) take the APC: {apc.Owner}, {apc.Drivers.Count} driver ({apc.DriverType}), {apc.Passengers.Count} passengers ({Time.time - t2:0.0} s)", taken);
            Look(apc, apc); yield return Shot("k69_4_apc_taken_h70");
            Finish();
        }

        void Finish()
        {
            Report = $"[OzApcSnipeProof] {(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks; pictures in {dir}";
            Log(Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.txt"), sb.ToString()); } catch (System.Exception ex) { Debug.LogWarning("[OzApcSnipeProof] " + ex.Message); }
            Destroy(gameObject);
        }
    }
}
#endif
