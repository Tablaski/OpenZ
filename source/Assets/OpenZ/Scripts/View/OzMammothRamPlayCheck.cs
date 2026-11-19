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
    /// 1.5 kit mammoth_ram: the Play check of DV182 (editor only; menu OpenZ/V1.5/Mammoth Ram: Play Check). In a running level or the Unit editor
    /// it finds free open ground near the camera and stages four rows one after another, each with a player Mammoth that drives in from 6 tiles back
    /// (so its hull has a heading) on a plain MOVE order, and enemy vehicles standing ahead:
    ///   P1 one enemy heavy tank straight ahead;  P2 two enemy heavies side by side;  P3 two enemy heavies one behind the other;
    ///   P4 an enemy Bizon (a big tank as the target).
    /// It reads the rules (the start, the hit, the 1.3 s hold, the one Death per target, the order going on) and the view (the flip started,
    /// its landing before the death, the wreck broken into pieces, the shake, the ram line, the sounds in OzSoundLog: veh_mammoth_ram_rev,
    /// veh_mammoth_ram, dst_turret_crash, vo_ram_x_mammoth) and takes 6 frames a row from the normal RTS camera (impact + 0, 0.35, 0.7, 1.1,
    /// 1.6, 2.4 s) into the kit's proof/play with a report. The staged units are removed after each row. Pictures are for the eye; the OBS
    /// owner clip (true 60 fps, full screen, sound) is a separate step (README).
    /// </summary>
    public class OzMammothRamPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 Mammoth Ram Play]";
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no Play check run yet";
        static readonly float[] ShotsAt = { 0f, 0.35f, 0.7f, 1.1f, 1.6f, 2.4f };

        ZGame G; ZWorld W; RtsCamera rts;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail;

        public static void Begin()
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play on a level (or the Unit editor) first"); return; }
            ZGame.I.gameObject.AddComponent<OzMammothRamPlayCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>();
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            md.AppendLine($"# Mammoth ram Play check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), level {G.level}").AppendLine();
            if (!ZWorld.MammothRamDeviation || !ZWorld.MammothDeviation) { md.AppendLine("DV182 or DV180 is off ('Zod exact'?): nothing to check"); Finish(); yield break; }
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            foreach (var row in new[] { "P1 one heavy ahead", "P2 two heavies side by side", "P3 two heavies one behind the other", "P4 a Bizon ahead" })
            {
                if (!FreeArea(out int ox, out int oy)) { Row(row, false, "no free 26 x 10 tiles of open dry ground near the camera"); continue; }
                var made = new List<ZObject>();
                // the Mammoth 6 tiles back, the targets 2 tiles of gap ahead of where it will be
                int mx = ox + 3 * 16, my = oy + 5 * 16;
                var m = W.CreateObject("vehicle", ZBigTanks.Mammoth, mx - 16, my - 16, me); made.Add(m); W.Emit(ZEventType.Created, m);
                int ax = mx + 6 * 16 + ZPathing.Radius(m) + 20 + 26;
                var targets = new List<ZObject>();
                if (row.StartsWith("P1")) targets.Add(Make("heavy", ax, my, foe));
                else if (row.StartsWith("P2")) { targets.Add(Make("heavy", ax, my - 21, foe)); targets.Add(Make("heavy", ax, my + 21, foe)); }
                else if (row.StartsWith("P3")) { targets.Add(Make("heavy", ax, my, foe)); targets.Add(Make("heavy", ax + 42, my, foe)); }
                else targets.Add(Make(ZBigTanks.Bizon, ax + 6, my, foe));
                foreach (var t in targets) { t.Damage = 0; made.Add(t); }
                m.Damage = 0;
                yield return null;
                if (rts) { rts.FlyTo(ZGame.PxToWorld(mx + 8 * 16, my)); }
                yield return new WaitForSeconds(1.5f);
                float t0 = Time.unscaledTime; int starts0 = OzMammothRamView.Starts, imp0 = OzMammothRamView.Impacts, flips0 = OzMammothRamView.Flips,
                    land0 = OzMammothRamView.Landings, br0 = OzMammothRamView.WreckBreaks, sh0 = OzMammothRamView.Shakes, ln0 = OzMammothRamView.Lines;
                W.SendWaypoints(me, m, new[] { ZWaypoint.Move(mx + 20 * 16, my) });
                var order = m.Waypoints.Count > 0 ? m.Waypoints[0] : default;
                // wait for the hit (or 30 s: the Mammoth drives 8 px/s, the ram starts 2 tiles out; 12 s was too short in Play, Unity pass 9 Nov)
                float until = Time.time + 30f; ZObject hitT = null;
                while (Time.time < until && hitT == null) { hitT = targets.FirstOrDefault(t => t.RamDoomed); yield return null; }
                if (hitT == null) { Row(row, false, $"no hit in 30 s (starts {OzMammothRamView.Starts - starts0}, the Mammoth at {m.CenterX},{m.CenterY}, ramming {m.Ramming})"); Tidy(made); yield return new WaitForSeconds(0.5f); continue; }
                float hitAt = Time.time; var tv = G.Views.TryGetValue(hitT.Id, out var v) ? v : null;
                var flip = tv ? tv.GetComponent<OzRamFlip>() : null;
                bool heldWhileFlip = true, resumed = false; float landedAt = -1f, deathAt = -1f;
                int k = 0;
                while (Time.time - hitAt < 3.2f)
                {
                    float a = Time.time - hitAt;
                    if (k < ShotsAt.Length && a >= ShotsAt[k]) { Shot(row, k, a); k++; }
                    if (flip && flip.LandedAt >= 0f && landedAt < 0f) landedAt = a;
                    if (deathAt < 0f && hitT.Destroyed) deathAt = a;
                    if (deathAt < 0f && OzV11KitAlive(hitT) && !OzMammothRamView.Holds(hitT) && a < 1.2f) heldWhileFlip = false;
                    if (!m.Ramming && m.Waypoints.Count > 0 && m.Waypoints[0].Equals(order)) resumed = true;
                    yield return null;
                }
                var others = targets.Where(t => t != hitT).ToList();
                bool othersUntouched = others.All(t => !t.RamDoomed || t.RamHitTick - hitT.RamHitTick >= ZObject.RamCoolTicks);
                var sounds = OzSoundLog.Since(t0);
                bool sRev = sounds.Any(e => e.key == "veh_mammoth_ram_rev"), sHit = sounds.Any(e => e.key == "veh_mammoth_ram"), sLand = sounds.Any(e => e.key == "dst_turret_crash" || (e.src == "CA" && e.key == "debris")),   // Unity pass 9 Nov: AudioDirector forwards dst_turret_crash to CombatAudio, logged as CA debris (as OzBigCrashProof reads it)
                     sLine = sounds.Any(e => e.key == "vo_ram_x_mammoth");
                bool ok = OzMammothRamView.Starts > starts0 && OzMammothRamView.Impacts > imp0 && OzMammothRamView.Flips > flips0 && landedAt >= 0f && deathAt >= 0f && landedAt <= deathAt + 0.05f
                          && OzMammothRamView.WreckBreaks > br0 && othersUntouched && heldWhileFlip && resumed && sRev && sHit && sLand;
                Row(row, ok, $"hit #{hitT.Id} ({hitT.Type}); flip landed {landedAt:0.00} s, destroyed {deathAt:0.00} s after the hit (rules {ZObject.RamFlipTicks * ZConst.TICK / Mathf.Max(0.05f, G.gameSpeed):0.00} s); "
                    + $"held by the flip {heldWhileFlip}; wreck broken {OzMammothRamView.WreckBreaks > br0}; the other(s) untouched by that ram {othersUntouched}; the order going on {resumed}; "
                    + $"shake {OzMammothRamView.Shakes > sh0} (the gate may hold it), ram line asked {OzMammothRamView.Lines > ln0} / played {sLine} (Alan's channel may drop it while another line speaks); "
                    + $"sounds rev {sRev}, crunch {sHit}, landing {sLand}");
                Tidy(made);
                yield return new WaitForSeconds(1.0f);
            }
            Finish();
        }

        static bool OzV11KitAlive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        ZObject Make(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, t);
            W.Emit(ZEventType.Created, o);
            return o;
        }

        bool FreeArea(out int ox, out int oy)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 40; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - 13, ty = cy + dy - 5;
                        if (Free(tx, ty, 26, 10)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
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

        void Shot(string row, int k, float at)
        {
            if (string.IsNullOrEmpty(Folder)) return;
            string name = row.Substring(0, 2) + "_" + k + "_" + at.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name));
        }

        void Tidy(List<ZObject> made)
        {
            foreach (var o in made)
                if (o != null && !o.Destroyed && o.KillAt < 0) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); }
        }

        void Row(string row, bool ok, string what)
        {
            md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}");
            if (ok) pass++; else fail++;
        }

        void Finish()
        {
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames: {Folder}");
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "play_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }
    }
}
#endif
