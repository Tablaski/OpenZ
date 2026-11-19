// OpenZ (GPL-3.0) - version 1.5 kit small_arms_balance_r8: the Play check of DV212 (editor only, never in a build). The stage finder and the
// hit log are plain sim code, so the kit's off-Unity harness runs them on the real maps (OPENZ_HARNESS).
#if (UNITY_EDITOR && !OZ_RELEASE) || OPENZ_HARNESS
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
#endif
#if UNITY_EDITOR && !OZ_RELEASE
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
#endif

#if (UNITY_EDITOR && !OZ_RELEASE) || OPENZ_HARNESS

namespace OpenZ
{
    /// <summary>1.5 kit small_arms_balance_r8: a small block of open dry ground for the Play check's fights (W x H tiles: passable, ground
    /// or road, no door lane, no unit or building within 32 px), searched outward from a tile over the whole map.</summary>
    public static class OzSmallArmsStage
    {
        public const int BlockW = 12, BlockH = 5;

        public static bool Find(ZWorld W, int cx, int cy, out int ox, out int oy)
        {
            ox = oy = 0;
            int max = System.Math.Max(W.Path.W, W.Path.H);
            for (int r = 0; r <= max; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r) continue;                 // the ring of radius r only
                        int tx = cx + dx - BlockW / 2, ty = cy + dy - BlockH / 2;
                        if (Free(W, tx, ty, BlockW, BlockH)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        public static bool Free(ZWorld W, int tx, int ty, int w, int h)
        {
            if (tx < 2 || ty < 2 || tx + w >= W.Path.W - 2 || ty + h >= W.Path.H - 2) return false;
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building)) return false;
            return true;
        }

        /// <summary>Per tick: the HP each hit removed (the target's health drop / its hits of that tick), by "shooter>target" type.</summary>
        public sealed class HitLog
        {
            public readonly Dictionary<string, SortedSet<int>> values = new Dictionary<string, SortedSet<int>>();
            public int odd;
            readonly Dictionary<int, int> hp = new Dictionary<int, int>();
            public void Before(IEnumerable<ZObject> units) { hp.Clear(); foreach (var u in units) if (u != null) hp[u.Id] = u.Health; }
            /// <summary>After a tick: its events are w.Events[from..] (the view clears the list once a frame, after one or more ticks).</summary>
            public void After(ZWorld w, int from = 0)
            {
                var n = new Dictionary<int, int>(); var by = new Dictionary<int, string>();
                for (int i = from; i < w.Events.Count; i++)
                {
                    var e = w.Events[i];
                    if (e.type == ZEventType.Hitscan && e.flag && hp.ContainsKey(e.otherId))
                    {
                        n[e.otherId] = n.TryGetValue(e.otherId, out int c) ? c + 1 : 1;
                        var a = w.Get(e.objId); var t = w.Get(e.otherId);
                        string key = (a != null ? a.Type : "?") + ">" + (t != null ? t.Type : "?");
                        by[e.otherId] = by.TryGetValue(e.otherId, out var k0) && k0 != key ? "mixed" : key;
                    }
                }
                foreach (var kv in hp)
                {
                    var t = w.Get(kv.Key); int now = t == null || t.Destroyed ? 0 : t.Health, drop = kv.Value - System.Math.Max(0, now);
                    n.TryGetValue(kv.Key, out int hits);
                    if (hits == 0) { if (drop > 0) odd++; continue; }
                    if (drop <= 0 || now <= 0 || drop % hits != 0 || by[kv.Key] == "mixed") { if (now > 0) odd++; continue; }   // the killing hit is cut at 0 HP: not counted
                    if (!values.TryGetValue(by[kv.Key], out var s)) values[by[kv.Key]] = s = new SortedSet<int>();
                    s.Add(drop / hits);
                }
            }
            public string Text()
            {
                var l = new List<string>();
                foreach (var kv in values) l.Add(kv.Key + " " + string.Join("/", kv.Value));
                l.Sort(System.StringComparer.Ordinal);
                return (l.Count == 0 ? "none" : string.Join(", ", l)) + (odd > 0 ? $" (ticks not read: {odd})" : "");
            }
        }
    }
}
#endif

#if UNITY_EDITOR && !OZ_RELEASE
namespace OpenZ
{
    /// <summary>
    /// 1.5 kit small_arms_balance_r8: the Play check of DV212 "small arms hit robots on foot harder" (Sim/ZObject.SmallArms.cs); menus
    /// OpenZ/Checks/V1.5/Small arms ... (Editor/OpenZParity.V15SmallArms.cs). In a running level (L02, the normal mode) it finds a small
    /// block of open dry ground (12 x 5 tiles, OzSmallArmsStage, searched over the whole map from the camera) and stages two fights,
    /// each twice: first with DV212 OFF for the fight (today), then ON (as the game set it):
    ///   GRUNT DUEL      one player's grunt against one enemy grunt, 96 px apart, both standing, both firing;
    ///   PSYCHO v GRUNT  a player's psycho squad (3) against an enemy grunt squad (3), 96 px apart, standing, both firing.
    /// Counted from the sim ticks (ZWorld.AfterTickHook): shots and hits per side, the HP each hit removed per shooter>target (the target's
    /// health drop / its hits of the tick), hits to the first kill, seconds from the first shot to the first kill and to the wipe, the
    /// winner. One frame per ON fight at the normal RTS camera 1.5 s after the first shot. The level's other units are quieted (Damage 0,
    /// put back) and everything the check made is removed after each fight. Report and frames: the kit's proof/play.
    /// PASS per fight: it ended (one side wiped inside 45 s), the HP per hit is exactly today's with DV212 off and the DV212 table with it
    /// on (grunt>grunt 155, grunt>psycho 100, psycho>grunt 40), and (ON) a frame.
    /// </summary>
    public class OzSmallArmsPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 small arms Play]";
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no Play check run yet";
        public const float MaxFightSimSeconds = 45f, PictureAfterShotSec = 1.5f;

        ZGame G; ZWorld W; RtsCamera rts; Team me, foe;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail;
        readonly List<ZObject> made = new List<ZObject>();
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();

        // the fight's count (sim ticks)
        List<ZObject> sideA = new List<ZObject>(), sideB = new List<ZObject>();
        OzSmallArmsStage.HitLog log; bool counting; double firstShot, firstKill, wipe; int aShots, aHits, bShots, bHits, hitsToFirstKill, seen; string winner;

        public static void Begin()
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play on a level first"); return; }
            ZGame.I.gameObject.AddComponent<OzSmallArmsPlayCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; if (ZWorld.AfterTickHook == (System.Action<ZWorld>)Tick) ZWorld.AfterTickHook = null; }

        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        void Tick(ZWorld w)
        {
            if (!counting) return;
            var ids = new HashSet<int>(sideA.Select(u => u.Id)); var idsB = new HashSet<int>(sideB.Select(u => u.Id));
            int from = w.Events.Count < seen ? 0 : seen;                                                  // this tick's events only (CombatView clears the list once a frame)
            for (int i = from; i < w.Events.Count; i++)
            {
                var e = w.Events[i];
                if (e.type != ZEventType.Hitscan) continue;
                bool fromA = ids.Contains(e.objId) && idsB.Contains(e.otherId), fromB = idsB.Contains(e.objId) && ids.Contains(e.otherId);
                if (!fromA && !fromB) continue;
                if (firstShot < 0) firstShot = w.Time;
                if (fromA) { aShots++; if (e.flag) aHits++; } else { bShots++; if (e.flag) bHits++; }
            }
            log.After(w, from); seen = w.Events.Count;
            bool aUp = sideA.Any(Alive), bUp = sideB.Any(Alive);
            if (firstKill < 0 && (sideA.Any(u => !Alive(u)) || sideB.Any(u => !Alive(u)))) { firstKill = w.Time; hitsToFirstKill = aHits + bHits; }
            if (wipe < 0 && (!aUp || !bUp)) { wipe = w.Time; winner = aUp ? "the player's side" : bUp ? "the enemy side" : "both down"; }
            log.Before(sideA.Concat(sideB));                                                              // the next tick's health
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>();
            me = G.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            bool was = ZWorld.SmallArmsDeviation; float speed = G.gameSpeed; var hook0 = ZWorld.AfterTickHook;
            md.AppendLine($"# Small arms against robots (DV212), Play check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), level {G.level}").AppendLine();
            try
            {
                if (!was) Row("switch", false, "DV212 is OFF as the game set it (the game turns it on at every start; 'Zod exact'?): the ON fights below turn it on for the fight");
                md.AppendLine($"switches as the game set them: DV212 {(was ? "on" : "off")}, part 2 (the Z idea) {(ZWorld.SmallArmsZ95Deviation ? "ON (it must be off)" : "off")}, DV78 rifle pair {(ZWorld.RiflePairDeviation ? "on" : "off")}; the player {me}").AppendLine();
                if (ZWorld.SmallArmsZ95Deviation) Row("part 2", false, "SmallArmsZ95Deviation is ON: it must stay off until the owner says yes");
                G.gameSpeed = 1f;
                ZWorld.AfterTickHook = Tick;
                foreach (var (name, a, an, b, bn) in new[] { ("GRUNT DUEL", "grunt", 1, "grunt", 1), ("PSYCHO v GRUNT", "psycho", 3, "grunt", 3) })
                    foreach (bool on in new[] { false, true })
                    {
                        ZWorld.SmallArmsDeviation = on;
                        yield return Fight(name, a, an, b, bn, on);
                        ZWorld.SmallArmsDeviation = was;
                    }
            }
            finally
            {
                counting = false;
                ZWorld.AfterTickHook = hook0; ZWorld.SmallArmsDeviation = was; if (G) G.gameSpeed = speed;
                Unquiet(); Tidy();
                md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames: {Folder}");
                Report = md.ToString();
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, "play_report_small_arms.md"), Report); } catch { } }
                Debug.Log(Tag + "\n" + Report);
                Destroy(this);
            }
        }

        List<ZObject> Squad(string type, int n, int cx, int cy, Team team)
        {
            var s = W.CreateRobotGroup(type, cx - 8, cy - 8, team, n);
            var l = new List<ZObject> { s }; l.AddRange(s.Minions);
            for (int i = 0; i < l.Count; i++) { l[i].SetCords(cx - 8, cy - 8 + (i - (l.Count - 1) / 2) * 16); l[i].MoveSpeed = 0; l[i].RealMoveSpeed = 0; }
            foreach (var u in l) { made.Add(u); G.SpawnUnitView(u); }
            return l;
        }

        IEnumerator Fight(string name, string a, int an, string b, int bn, bool on)
        {
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            string row = name + (on ? " ON" : " OFF");
            if (!OzSmallArmsStage.Find(W, cx, cy, out int ox, out int oy)) { Row(row, false, $"no free {OzSmallArmsStage.BlockW} x {OzSmallArmsStage.BlockH} tiles of open dry ground on the whole map"); yield break; }
            int y = oy + OzSmallArmsStage.BlockH * 8, xa = ox + 3 * 16, xb = xa + 96;
            sideA = Squad(a, an, xa, y, me); sideB = Squad(b, bn, xb, y, foe);
            Quiet();
            yield return null;
            if (rts) rts.FlyTo(ZGame.PxToWorld(xa + 48, y));
            yield return new WaitForSeconds(1.0f);
            log = new OzSmallArmsStage.HitLog(); log.Before(sideA.Concat(sideB));
            firstShot = firstKill = wipe = -1; aShots = aHits = bShots = bHits = hitsToFirstKill = 0; winner = "none"; seen = W.Events.Count;
            double start = W.Time; string picture = "";
            counting = true;
            while (W.Time - start < MaxFightSimSeconds && wipe < 0)
            {
                if (on && picture.Length == 0 && firstShot >= 0 && W.Time - firstShot >= PictureAfterShotSec)
                {
                    picture = $"{name.Replace(' ', '_')}_dv212_on_t{W.Time - firstShot:0.0}s.png";
                    yield return new WaitForEndOfFrame();
                    if (!string.IsNullOrEmpty(Folder)) ScreenCapture.CaptureScreenshot(Path.Combine(Folder, picture));
                    continue;
                }
                yield return null;
            }
            counting = false;
            var want = new Dictionary<string, int>();
            foreach (var (s, t) in new[] { (a, b), (b, a) })
                want[s + ">" + t] = on ? System.Math.Max(Today(s, t), ZWorld.SmallArmsRobotHit(s, t)) : Today(s, t);
            bool valuesOk = log.values.Count > 0 && log.values.All(kv => kv.Value.Count == 1 && want.TryGetValue(kv.Key, out int w0) && kv.Value.Min == w0);
            string what = $"DV212 {(on ? "on" : "off")}: HP per hit {log.Text()} (want {string.Join(", ", want.Select(kv => kv.Key + " " + kv.Value))}); "
                        + $"shots / hits: the player's {aShots} / {aHits}, the enemy's {bShots} / {bHits}; hits (both sides) to the first kill {hitsToFirstKill}; "
                        + $"first kill {(firstKill >= 0 && firstShot >= 0 ? (firstKill - firstShot).ToString("0.0") + " s" : "none")}, wipe {(wipe >= 0 && firstShot >= 0 ? (wipe - firstShot).ToString("0.0") + " s" : "none")} after the first shot; winner {winner}"
                        + (on ? $"; frame {(picture.Length > 0 ? picture : "NONE")}" : "");
            Row(row, wipe >= 0 && valuesOk && (!on || picture.Length > 0), what);
            Unquiet(); Tidy();
            yield return new WaitForSeconds(1.0f);
        }

        /// <summary>Today's HP of a body hit (DV212 off): the DV78 pair when it is on and higher than the shooter's damage, else the damage.</summary>
        int Today(string shooter, string target)
        {
            int dmg = W.S.Unit(shooter) != null ? W.S.Unit(shooter).Damage : 0;
            return ZWorld.RiflePairDeviation && ZWorld.RiflePair.TryGetValue((shooter, target), out int p) && p > dmg ? p : dmg;
        }

        void Quiet()
        {
            foreach (var o in W.Objects)
                if (o.IsUnit && !made.Contains(o) && !o.Destroyed && o.Damage > 0 && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
        }

        void Unquiet()
        {
            foreach (var kv in quieted) if (kv.Key != null && !kv.Key.Destroyed) kv.Key.Damage = kv.Value;
            quieted.Clear();
        }

        void Tidy()
        {
            foreach (var o in made)
                if (o != null && !o.Destroyed && o.KillAt < 0) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); }
            made.Clear();
        }

        void Row(string row, bool ok, string what)
        {
            md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}");
            if (ok) pass++; else fail++;
        }
    }
}
#endif
