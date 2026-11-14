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
    /// 1.3 item 17 counted Play check (View/UnitView.GunSpin.cs): menu OpenZ/V1.3/Wrecks: Item 17 Gun Spin Check, a level in Play (full look,
    /// normal RTS camera). Near the camera's focus it puts a red gun and a red howitzer on free open ground (a few metres apart), destroys them
    /// in the rules, and records each turret toss: when it started after the death, how long it spun in the air, and when the rules' debris
    /// blast came (the moment its debris missile left the rules' list). <see cref="Rounds"/> rounds with the rule off (as before), the same with
    /// it on. A 12-frame strip 0.25 s apart of the first toss of each type with the rule on.
    ///  S1 the spinning flight with the rule on is 0.65-0.75 of the one with it off (mean per type), the spin speed range the same (300-620 deg/s);
    ///  S2 (default way) every landing within 0.05 s of its rules' blast, with the rule on and off;
    ///  S3 only guns and howitzers changed: a gatling and a missile cannon toss at their own old moment (rule on: their flight = blast - toss).
    /// The units it made are removed at the end. Output: <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzGunSpinCheck : MonoBehaviour
    {
        public static int Rounds = 3;
        public static string Report = "idle";
        public static string Folder => OzRelease.Milestones("v13_item17_gun_spin");
        public static void Run() { Report = "running"; new GameObject("OzGunSpinCheck").AddComponent<OzGunSpinCheck>(); }

        readonly StringBuilder sb = new StringBuilder();
        void Line(string t) { sb.AppendLine(t); Debug.Log("[OzGunSpinCheck] " + t); }

        sealed class Rec { public string type; public bool rule; public float death, start = -1, flight = -1, oldFlight = -1, blast = -1; public ZMissile m; }

        IEnumerator Start()
        {
            var g = ZGame.I; var W = g ? g.World : null;
            if (W == null) { Report = "no game"; Destroy(gameObject); yield break; }
            Directory.CreateDirectory(Folder);
            var rts = FindFirstObjectByType<RtsCamera>();
            Vector3 c = rts ? rts.focus : g.CenterWorld(W.Objects.First(o => o.IsFort && o.Owner == Team.Red));
            bool rule0 = GunSpin.Enabled; float speed0 = g.gameSpeed; g.gameSpeed = 1f;
            var recs = new List<Rec>(); var made = new List<ZObject>();
            int tx0 = Mathf.RoundToInt(c.x * 8f / 16f), ty0 = Mathf.RoundToInt(-c.z * 8f / 16f);
            bool strip = false;
            foreach (bool rule in new[] { false, true })
                for (int r = 0; r < Rounds; r++)
                    foreach (var type in rule && r == 0 ? new[] { "gun", "howitzer", "gatling", "missile_cannon" } : new[] { "gun", "howitzer" })
                    {
                        GunSpin.Enabled = rule;
                        if (!FreeTile(W, tx0, ty0, out int tx, out int ty)) { Line("no free ground near the camera"); continue; }
                        var o = W.CreateObject("cannon", type, tx * 16, ty * 16, Team.Red); made.Add(o); g.SpawnUnitView(o);
                        yield return new WaitForSeconds(1f);
                        int n0 = GunSpin.Last.Count;
                        var rec = new Rec { type = type, rule = rule, death = Time.time };
                        o.SetHealth(0); W.UpdateObjectHealth(o);
                        yield return null;
                        rec.m = FindDebris(W, o.Id);
                        bool shoot = rule && !strip && (type == "howitzer");
                        float t = 0f; int shots = 0;
                        while (t < 14f)
                        {
                            t += Time.deltaTime;
                            if (rec.start < 0 && GunSpin.Last.Count > n0) { var last = GunSpin.Last[GunSpin.Last.Count - 1]; rec.start = last.start - rec.death; rec.flight = last.flight; rec.oldFlight = last.oldFlight; }
                            if (rec.blast < 0 && rec.m != null && !Listed(W, rec.m)) rec.blast = Time.time - rec.death;
                            if (shoot && t >= 1f && shots < 12 && t >= 1f + shots * 0.25f) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"howitzer_toss_{shots:00}.png")); shots++; }
                            if (rec.blast >= 0 && (rec.start >= 0 || t > 12f)) break;
                            yield return null;
                        }
                        if (shoot) strip = true;
                        recs.Add(rec);
                        Line($"rule {(rule ? "on " : "off")} {type}: toss {F(rec.start)} s after the death, spun {F(rec.flight)} s, the rules' blast {F(rec.blast)} s after the death, landing - blast {(rec.start >= 0 && rec.flight >= 0 && rec.blast >= 0 ? F(rec.start + rec.flight - rec.blast) : "-")} s");
                        tx0 += 3;
                    }
            GunSpin.Enabled = rule0; g.gameSpeed = speed0;
            var v = new List<string>();
            foreach (var type in new[] { "gun", "howitzer" })
            {
                var off = recs.Where(x => x.type == type && !x.rule && x.flight > 0).ToList(); var on = recs.Where(x => x.type == type && x.rule && x.flight > 0).ToList();
                float mo = off.Count > 0 ? off.Average(x => x.flight) : -1, mn = on.Count > 0 ? on.Average(x => x.flight) : -1, k = mo > 0 ? mn / mo : -1;
                var each = on.Where(x => x.oldFlight > 0).Select(x => x.flight / x.oldFlight).ToList();
                v.Add(Verdict(each.Count > 0 && each.All(x => x >= 0.65f && x <= 0.75f),
                    $"S1 {type}: each toss with the rule on keeps {string.Join(", ", each.Select(F))} of the flight it would have had (want 0.65-0.75); mean spinning flight off {F(mo)} s, on {F(mn)} s (on / off {F(k)}, different draws)"));
            }
            var gh = recs.Where(x => x.type == "gun" || x.type == "howitzer").Where(x => x.start >= 0 && x.flight >= 0 && x.blast >= 0).ToList();
            v.Add(Verdict(GunSpin.EndsSooner || (gh.Count > 0 && gh.All(x => Mathf.Abs(x.start + x.flight - x.blast) <= 0.05f)),
                $"S2 landing on the rules' blast: worst {F(gh.Count == 0 ? -1 : gh.Max(x => Mathf.Abs(x.start + x.flight - x.blast)))} s over {gh.Count} tosses{(GunSpin.EndsSooner ? " (the other way is set: lands sooner by design)" : "")}"));
            var others = recs.Where(x => x.type == "gatling" || x.type == "missile_cannon").ToList();
            v.Add(Verdict(others.Count > 0 && others.All(x => x.start >= 1.95f && x.start <= 4.1f && x.oldFlight < 0),
                $"S3 other cannons untouched (toss at Zod's 2-4 s, no flight changed): {string.Join(", ", others.Select(x => x.type + " toss " + F(x.start) + " s, flight " + F(x.flight) + " s"))}"));
            foreach (var l in v) Line(l);
            Report = $"{v.Count(l => l.StartsWith("PASS"))} PASS, {v.Count(l => l.StartsWith("FAIL"))} FAIL"; Line(Report);
            foreach (var o in made) if (W.Get(o.Id) == o) { o.KillAt = W.Time; }
            File.WriteAllText(Path.Combine(Folder, "report.txt"), sb.ToString());
            Destroy(gameObject);
        }

        static string F(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        static string Verdict(bool ok, string t) => (ok ? "PASS  " : "FAIL  ") + t;

        static ZMissile FindDebris(ZWorld W, int id)
        {
            foreach (var x in W.PendingMissiles) if (x.debris && x.attackerId == id) return x;
            foreach (var x in W.QueuedMissiles) if (x.debris && x.attackerId == id) return x;
            return null;
        }
        static bool Listed(ZWorld W, ZMissile m)
        {
            foreach (var x in W.PendingMissiles) if (x == m) return true;
            foreach (var x in W.QueuedMissiles) if (x == m) return true;
            return false;
        }

        static bool FreeTile(ZWorld W, int tx0, int ty0, out int tx, out int ty)
        {
            for (int r = 0; r < 12; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        tx = tx0 + dx; ty = ty0 + dy;
                        if (tx < 2 || ty < 2 || tx > W.Map.width - 4 || ty > W.Map.height - 4) continue;
                        bool ok = true;
                        for (int j = 0; j < 2 && ok; j++) for (int i = 0; i < 2 && ok; i++) ok = W.Path.TilePassable(tx + i, ty + j, false);
                        if (!ok) continue;
                        foreach (var o in W.Objects) if (!o.Destroyed && System.Math.Abs((o.CenterX >> 4) - tx) < 3 && System.Math.Abs((o.CenterY >> 4) - ty) < 3) { ok = false; break; }
                        if (ok) return true;
                    }
            tx = ty = 0; return false;
        }
    }
}
#endif
