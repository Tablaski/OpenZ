#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 32 Play proof (rule DV141, Sim/ZObject.CraneSnipe.cs; menu OpenZ/V1.2/Combat: 32 Crane Driver Snipe Proof (Play),
    /// Editor/OzCraneSnipeMenu.cs). In a running level it removes the computer players (nothing else fires), picks the open spot of the map
    /// farthest from every unit and building, puts an enemy crane there and a sniper squad of the player 5 tiles west of it, and gives the
    /// squad the player's attack order on the crane. Counted: the crane's driver comes up and fires (DV31); he is shot out (the crane turns
    /// neutral, no driver); the crane stands empty: no orders, stopped, enterable, the snipers let go of it. Three pictures at the normal
    /// RTS camera (70 m), full screen: the driver up and firing, the moment he is shot out, the empty crane. Lines and pictures in
    /// <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzCraneSnipeProof : MonoBehaviour
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
                    var kit = System.IO.Path.Combine(stage, "k32_crane_driver_snipe");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k32_crane_driver_snipe");
        }

        public static void Run()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning("[OzCraneSnipeProof] start a level in Play first"); return; }
            if (FindFirstObjectByType<OzCraneSnipeProof>()) { Debug.LogWarning("[OzCraneSnipeProof] already running"); return; }
            Report = "running";
            new GameObject("OzCraneSnipeProof").AddComponent<OzCraneSnipeProof>();
        }

        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        ZGame g; ZWorld W; RtsCamera rts;

        void Start() { dir = Folder(); System.IO.Directory.CreateDirectory(dir); g = ZGame.I; W = g.World; rts = FindFirstObjectByType<RtsCamera>(); StartCoroutine(All()); }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzCraneSnipeProof] " + t); }
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
        /// with room for a crane (2 x 2) there and a squad 5 tiles west of it.</summary>
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
            if (best >= 0) Log($"spot: crane tile ({cx},{cy}), squad tile ({sx},{sy}); nearest other unit or building {Mathf.Sqrt(best) / 16f:0} tiles away");
            return best >= 0;
        }

        IEnumerator All()
        {
            if (W == null) { Check("a level in Play", false); Finish(); yield break; }
            var me = g.player; var them = me == Team.Red ? Team.Blue : Team.Red;
            Log($"1.2 item 32 crane driver snipe proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s, the player {me}");
            Check($"P0 the rule's switches are on (ZWorld.CraneSnipeDeviation {ZWorld.CraneSnipeDeviation}, the crane's gun DV31 {ZWorld.CraneGunDeviation}, driver sniping DC2 {ZWorld.SnipeDeviation})",
                  ZWorld.CraneSnipeDeviation && ZWorld.CraneGunDeviation && ZWorld.SnipeDeviation);
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            Log($"set-up: the computer players removed ({bots}), daylight held");
            if (!Spot(out int ctx, out int cty, out int stx, out int sty)) { Check("an open spot for the crane and the squad", false); Finish(); yield break; }
            var crane = W.CreateObject("vehicle", "crane", ctx * 16, cty * 16, them);
            var sn = W.CreateRobotGroup("sniper", stx * 16, sty * 16, me);
            if (crane == null || sn == null) { Check("the crane and the sniper squad were placed", false); Finish(); yield break; }
            g.SpawnUnitView(crane); g.SpawnUnitView(sn); foreach (var m in sn.Minions) g.SpawnUnitView(m);
            var squad = new[] { sn }.Concat(sn.Minions).ToList();
            int pool0 = crane.Drivers.Count > 0 ? crane.Drivers[0] : 0;
            Log($"placed: {them} crane #{crane.Id} with {crane.Drivers.Count} driver ({crane.DriverType}, {pool0} HP), the player's sniper squad #{sn.Id} x{squad.Count}, {Mathf.Sqrt((crane.CenterX - sn.CenterX) * (crane.CenterX - sn.CenterX) + (crane.CenterY - sn.CenterY) * (crane.CenterY - sn.CenterY)):0} px apart");
            Look(crane, sn);
            yield return Real(1.0f);
            bool ordered = W.SendWaypoints(me, sn, new[] { new ZWaypoint { mode = WpMode.Attack, refId = crane.Id, x = crane.CenterX, y = crane.CenterY } });
            Check("P1 the player's attack order on the crane", ordered);

            // the driver comes up (his first shot follows ZWorld.CraneGunPopSeconds later; snipers often shoot him out a moment after it)
            float t0 = Time.time; double lastShot = crane.CraneGunLastShot; int shots = 0; float upAt = -1f; bool pictured = false;
            while (Time.time - t0 < 20f && crane.Owner == them && crane.Drivers.Count > 0)
            {
                if (crane.CraneGunLastShot > lastShot) { lastShot = crane.CraneGunLastShot; shots++; }
                if (upAt < 0f && crane.CraneGunUp) upAt = Time.time - t0;
                if (!pictured && upAt >= 0f && crane.CraneGunUp && (shots > 0 || Time.time - t0 - upAt >= (float)ZWorld.CraneGunPopSeconds))
                { pictured = true; Look(crane, sn); yield return Shot("k32_1_driver_up_firing_h70"); continue; }
                yield return null;
            }
            Check($"P2 the crane's driver comes up through his hatch to fire at the snipers ({(upAt >= 0f ? $"{upAt:0.00} s after the order" : "never")}; he fired {shots} shot(s) before the end of the fight; picture taken {pictured})", upAt >= 0f);
            int lowest = crane.Drivers.Count > 0 ? crane.Drivers[0] : 0;
            bool sniped = crane.Owner == Team.Neutral && crane.Drivers.Count == 0 && !crane.Destroyed;
            Check($"P3 the snipers shoot the driver out ({(Time.time - t0):0.0} s after the order; the driver's health went {pool0} -> {(sniped ? 0 : lowest)}; the crane {crane.Owner}, drivers {crane.Drivers.Count}, hull {crane.Health}/{crane.MaxHealth})", sniped);
            if (sniped) { yield return Real(0.35f); Look(crane, sn); yield return Shot("k32_2_driver_shot_out_h70"); }

            // the empty crane (looked at 1.5 s after the snipe: an idle robot of either side that stands near an empty vehicle may then
            // walk over and take it by itself, as with every sniped vehicle; that is only written down)
            yield return Real(1.5f);
            bool empty = crane.Owner == Team.Neutral && crane.Drivers.Count == 0 && crane.Waypoints.Count == 0 && !crane.IsMoving && !crane.CraneGunUp && crane.CanBeEntered() && !crane.CanBeSniped();
            bool letGo = squad.All(m => m.Destroyed || m.AttackObject != crane);
            Check($"P4 the crane stands empty: neutral, no driver, no orders, stopped, its driver not drawn up, enterable ({empty}); every sniper let go of it ({letGo})", empty && letGo);
            Look(crane, sn); yield return Shot("k32_3_empty_crane_h70");
            Log($"the snipers: {string.Join(", ", squad.Select(m => m.Destroyed ? "dead" : m.Health + " HP"))}");
            float t2 = Time.time;
            while (Time.time - t2 < 8f && crane.Owner == Team.Neutral) yield return null;
            Log(crane.Owner == Team.Neutral ? "after 8 s more the crane still stands empty" : $"{Time.time - t2 + 1.5f:0.0} s after the snipe a robot of {crane.Owner} took the empty crane by itself (the game's own grab of an empty vehicle; not a check)");
            Finish();
        }

        void Finish()
        {
            Report = $"[OzCraneSnipeProof] {(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks; pictures in {dir}";
            Log(Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.txt"), sb.ToString()); } catch (System.Exception ex) { Debug.LogWarning("[OzCraneSnipeProof] " + ex.Message); }
            Destroy(gameObject);
        }
    }
}
#endif
