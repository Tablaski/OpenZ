#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV164 counted Play check (Sim/ZObject.HeavyRock.cs): menu OpenZ/V1.3/Heavy tank clears rocks: Play check, on level 7 and on level 16, from the main menu, normal RTS camera, the
    /// player's side. It finds rocks in a wall with a free tile spot touching them, far from every enemy, and puts its own heavy tank there.
    ///  A1-A7 the tank stands idle, turns, the gun is on the rock's foot before the shell, the rock goes, it never moved, it stands idle again, nothing it does not clip was hurt: frames
    ///  before, gun on the rock, after;
    ///  B an order while the gun turns: no shell, the rock is whole;
    ///  C a spot with no rock in reach: nothing.
    /// The tanks it made are removed at the end. Output: <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzHeavyRockCheck : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder => OzRelease.Milestones("v13_heavy_rock_clear");
        public static void Run() { Report = "running"; new GameObject("OzHeavyRockCheck").AddComponent<OzHeavyRockCheck>(); }

        readonly StringBuilder sb = new StringBuilder();
        int good, bad;
        void Line(string t) { sb.AppendLine(t); Debug.Log("[OpenZ HeavyRockCheck] " + t); }
        void V(bool ok, string t) { if (ok) good++; else bad++; Line((ok ? "PASS  " : "FAIL  ") + t); }

        static int FootY(ZObject r) => r.LocY + ZWorld.RockFootPx;
        static bool Alive(ZWorld w, ZObject r) => w.Get(r.Id) == r && !r.Destroyed;
        static bool Clip(ZObject r, int cx, int cy, int reach) { long dx = r.CenterX - cx, dy = FootY(r) - cy; return dx * dx + dy * dy <= (long)reach * reach; }

        /// <summary>A rock with another rock beside it and a tile spot that touches it, far from every enemy and from every unit of ours; the spot's middle.</summary>
        static bool Spot(ZWorld w, Team team, HashSet<int> used, List<ZObject> rocks, out ZObject rock, out int cx, out int cy)
        {
            foreach (var r in rocks)
            {
                if (used.Contains(r.Id) || !Alive(w, r) || !rocks.Any(n => n != r && Alive(w, n) && System.Math.Abs(n.CenterX - r.CenterX) + System.Math.Abs(n.CenterY - r.CenterY) == 16)) continue;
                int bx = r.LocX / 16, by = r.LocY / 16 + 2;
                foreach (var (ox, oy) in new[] { (1, 0), (-2, 0), (0, 1), (0, -2) })
                {
                    if (w.Path.WithinImpassable((bx + ox) * 16 + 1, (by + oy) * 16 + 1, 30, 30, out _, out _, false)) continue;
                    int x = (bx + ox) * 16 + 16, y = (by + oy) * 16 + 16; bool far = true;
                    foreach (var o in w.Objects)
                    {
                        if (!o.IsUnit || o.Destroyed) continue;
                        long dx = o.CenterX - x, dy = o.CenterY - y, d2 = dx * dx + dy * dy;
                        if (d2 < (o.Owner != team && o.Owner != Team.Neutral ? 260L * 260L : 64L * 64L)) { far = false; break; }
                    }
                    foreach (var f in w.Flags)
                    {   // an idle tank takes a flag near it by itself, and that wins: not by one
                        long dx = f.CenterX - x, dy = f.CenterY - y;
                        if (f.Owner != team && dx * dx + dy * dy < (long)(w.S.auto_grab_flag_distance + 32) * (w.S.auto_grab_flag_distance + 32)) far = false;
                    }
                    if (!far) continue;
                    rock = r; cx = x; cy = y; used.Add(r.Id);
                    return true;
                }
            }
            rock = null; cx = cy = 0;
            return false;
        }

        IEnumerator Shot(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png")); yield return null; yield return null; }

        ZObject Tank(ZGame g, ZWorld w, Team team, int cx, int cy) { var o = w.CreateObject("vehicle", "heavy", cx - 16, cy - 16, team); g.SpawnUnitView(o); return o; }

        void Look(RtsCamera rts, int cx, int cy)
        {
            if (!rts) return;
            var p = ZGame.PxToWorld(cx, cy); rts.focus = new Vector3(p.x, 0f, p.z); rts.SetHeightImmediate(rts.minHeight + 10f);
        }

        IEnumerator Start()
        {
            var g = ZGame.I; var w = g ? g.World : null;
            if (w == null) { Report = "no game"; Destroy(gameObject); yield break; }
            Directory.CreateDirectory(Folder);
            string lv = w.Map.file != null && w.Map.file.Length >= 6 ? "L" + w.Map.file.Substring(w.Map.file.Length - 6, 2) : "level";
            var rocks = w.Objects.Where(o => o.Type == "rock").ToList();
            var team = g.player; var rts = FindFirstObjectByType<RtsCamera>();
            float speed0 = g.gameSpeed; g.gameSpeed = 1f;
            var made = new List<ZObject>(); var used = new HashSet<int>();
            Line($"{lv}: {rocks.Count} rocks, the rule {(ZWorld.HeavyRockDeviation ? "on" : "OFF")}, drawn size {ZWorld.HeavyRockDrawnPct} %, reach {ZWorld.HeavyRockReachPx(ZWorld.HeavyRockDrawnPct)} px");
            V(ZWorld.HeavyRockDeviation, "the rule is on (it is off in 'Zod exact')");
            while (w.CalmStart(team)) yield return null;

            // ---- A: it clears the rock it clips
            if (Spot(w, team, used, rocks, out var rock, out int cx, out int cy))
            {
                int reach = ZWorld.HeavyRockReachPx(ZWorld.HeavyRockDrawnPct) + ZWorld.HeavyRockRingBackPx(ZWorld.HeavyRockDrawnPct);
                var keep = rocks.Where(r => Alive(w, r) && !Clip(r, cx, cy, reach)).Select(r => (r, r.Health)).ToList();
                var o = Tank(g, w, team, cx, cy); made.Add(o); int x0 = o.LocX, y0 = o.LocY; Look(rts, cx, cy);
                yield return new WaitForSeconds(1f);
                yield return Shot(lv + "_a_before");
                float t = 0f; while (t < 6f && o.AttackObject == null) { t += Time.deltaTime; yield return null; }
                V(o.AttackObject != null && o.AttackObject.Type == "rock", $"A1 idle 1 s + {t:0.0} s: on a rock (#{(o.AttackObject != null ? o.AttackObject.Id : -1)})");
                var on = o.AttackObject ?? rock;
                yield return new WaitForSeconds((float)ZWorld.HeavyRockTurnSeconds - 0.15f);
                var aim = g.Views.TryGetValue(o.Id, out var view) && view ? view.GetComponent<UnitAim>() : null;
                V(aim && aim.AimError <= 8f, $"A2 the gun on the rock's foot before the shell: {(aim ? aim.AimError : -1f):0.0} deg off");
                yield return Shot(lv + "_a_gun_on_rock");
                t = 0f; while (t < 8f && Alive(w, on)) { t += Time.deltaTime; yield return null; }
                V(!Alive(w, on), $"A3 the rock is gone {t:0.0} s later");
                yield return new WaitForSeconds(2.5f);
                yield return Shot(lv + "_a_after");
                V(o.LocX == x0 && o.LocY == y0, "A4 it never moved");
                t = 0f; while (t < 12f && (o.AttackObject != null || w.HeavyRockTargets == 0)) { t += Time.deltaTime; yield return null; }
                yield return new WaitForSeconds(0.5f);
                int bad0 = keep.Count(k => !Alive(w, k.r) || k.r.Health != k.Health);
                V(bad0 == 0, $"A5 of {keep.Count} rocks out of its reach {bad0} were hurt");
                int left = rocks.Count(r => Alive(w, r) && Clip(r, cx, cy, reach) && o.CanAttackObject(r) && o.WithinAttackRadius(r));
                yield return new WaitForSeconds(4f * Mathf.Min(left, 4));
                V(o.Waypoints.Count == 0, $"A6 no order of its own ({left} more rocks in reach when it stopped: each goes on its own idle moment)");
                yield return Shot(lv + "_a_end");
            }
            else V(false, "A no rock with a free spot beside it, far from every enemy");

            // ---- B: an order wins at once
            if (Spot(w, team, used, rocks, out rock, out cx, out cy))
            {
                var o = Tank(g, w, team, cx, cy); made.Add(o); Look(rts, cx, cy);
                float t = 0f; while (t < 7f && o.AttackObject == null) { t += Time.deltaTime; yield return null; }
                var on = o.AttackObject; int hp = on != null ? on.Health : -1;
                yield return new WaitForSeconds(0.4f);
                w.SendWaypoints(team, o, new[] { ZWaypoint.Move(cx + (cx > w.Map.WidthPix / 2 ? -64 : 64), cy + (cy > w.Map.HeightPix / 2 ? -64 : 64), false) });
                yield return new WaitForSeconds(5f);
                V(on != null && on.Type == "rock" && Alive(w, on) && on.Health == hp, $"B an order while the gun turns: no shell, the rock whole ({(on != null ? on.Health : -1)} of {hp})");
                V(o.AttackObject == null || o.AttackObject.Type != "rock", "B2 it does not go back to the rock while the order runs");
            }
            else V(false, "B no second rock with a free spot beside it");

            // ---- C: no rock in reach, nothing
            {
                int reach = ZWorld.HeavyRockReachPx(ZWorld.HeavyRockDrawnPct) + ZWorld.HeavyRockRingBackPx(ZWorld.HeavyRockDrawnPct) + 8, sx = -1, sy = -1;
                for (int ty = 2; ty < w.Map.height - 4 && sx < 0; ty += 2)
                    for (int tx = 2; tx < w.Map.width - 4 && sx < 0; tx += 2)
                    {
                        if (w.Path.WithinImpassable(tx * 16 + 1, ty * 16 + 1, 30, 30, out _, out _, false)) continue;
                        int x = tx * 16 + 16, y = ty * 16 + 16, near = 0;
                        foreach (var r in rocks) if (Alive(w, r)) { long dx = r.CenterX - x, dy = FootY(r) - y; long d2 = dx * dx + dy * dy; if (d2 <= (long)reach * reach) near = 2; else if (d2 <= 72L * 72L && near == 0) near = 1; }
                        if (near != 1 || w.Objects.Any(o => o.IsUnit && !o.Destroyed && Mathf.Abs(o.CenterX - x) + Mathf.Abs(o.CenterY - y) < (o.Owner != team && o.Owner != Team.Neutral ? 300 : 72))) continue;
                        sx = x; sy = y;
                    }
                if (sx >= 0)
                {
                    var o = Tank(g, w, team, sx, sy); made.Add(o); Look(rts, sx, sy);
                    yield return new WaitForSeconds(6f);
                    V(o.AttackObject == null, "C a rock a little past the reach: nothing in 6 s");
                }
                else V(false, "C no open spot found");
            }

            g.gameSpeed = speed0;
            foreach (var o in made) if (w.Get(o.Id) == o) o.KillAt = w.Time;
            Report = $"{(bad == 0 ? "PASS" : "FAIL")}: {good} of {good + bad} checks"; Line(Report);
            File.WriteAllText(Path.Combine(Folder, lv + "_report.txt"), sb.ToString());
            Destroy(gameObject);
        }
    }
}
#endif
