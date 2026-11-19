#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// (editor only; numbers, no pictures).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// In Play in the Unit editor, on flat free ground near the camera, 20 staged cases: 8 Mammoth rams (a player Mammoth on a plain MOVE order
    /// through an enemy jeep, light tank, mech, medium tank, heavy tank, MLRS, crane, APC), 6 Bizon crushes (light tank, medium tank, heavy tank,
    /// jeep, APC, MLRS; the Bizon drives on over the wreck, so the wreck is shoved too), 4 big-tank deaths (Bizon, Mammoth, twice each: the
    /// wreck pieces and the Bizon's fuel cans) and 2 burning wrecks a Bizon then drives through (heavy tank, medium tank). From the start of a
    /// case until <see cref="Seconds"/> s after the death every fire made through OzFireSource's hooks (the wreck's burn puffs and flame
    /// tongues, the sticky fire and its licks, the small damage fire, the crush fires, the burning pieces, the can bursts, the death burst) is
    /// measured at the END of its frame, after every script has posed the hulls, against what is drawn of its source right then
    /// (OzFireSource.Measure: its own reading of the renderers, not the hooks' cached one). A fire is "far" when its foot is more than
    /// OzFireSource.NearM (0.3 m) from every solid part drawn under its source, or when its source shows nothing at all.
    /// Sets: "before" = the tie off (the fires exactly as the test exe test_1_5_20261111b makes them), "after" = the tie on.
    /// PASS per "after" row: the case happened (the hit and the death) and 0 far fires. The "before" rows are the fault's measure, not counted.
    /// </summary>
    public class OzFireSourceCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Fire Has A Source]";
        public static string Folder = "", Sets = "before,after";
        public static float Seconds = 12f;
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";

        enum Mode { Ram, Crush, BigDeath, Shove }
        /// <summary>The 20 cases: what happens, the target (type, model version 0 = as drawn), its label.</summary>
        static readonly (Mode mode, string type, int ver, string label)[] Cases =
        {
            (Mode.Ram, "jeep", 0, "ram: jeep"), (Mode.Ram, "light", 1, "ram: light v1"), (Mode.Ram, "medium", 1, "ram: mech v1"), (Mode.Ram, ZMTank.Type, 0, "ram: medium tank"),
            (Mode.Ram, "heavy", 1, "ram: heavy v1"), (Mode.Ram, "missile_launcher", 1, "ram: MLRS v1"), (Mode.Ram, "crane", 0, "ram: crane"), (Mode.Ram, "apc", 0, "ram: APC"),
            (Mode.Crush, "light", 1, "crush: light v1"), (Mode.Crush, ZMTank.Type, 0, "crush: medium tank"), (Mode.Crush, "heavy", 1, "crush: heavy v1"),
            (Mode.Crush, "jeep", 0, "crush: jeep"), (Mode.Crush, "apc", 0, "crush: APC"), (Mode.Crush, "missile_launcher", 1, "crush: MLRS v1"),
            (Mode.BigDeath, ZBigTanks.Bizon, 0, "death: Bizon"), (Mode.BigDeath, ZBigTanks.Mammoth, 0, "death: Mammoth"),
            (Mode.BigDeath, ZBigTanks.Bizon, 0, "death: Bizon (2)"), (Mode.BigDeath, ZBigTanks.Mammoth, 0, "death: Mammoth (2)"),
            (Mode.Shove, "heavy", 1, "burning wreck shoved: heavy v1"), (Mode.Shove, ZMTank.Type, 0, "burning wreck shoved: medium tank"),
        };

        ZGame G; ZWorld W; RtsCamera rts;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail, farBefore, farAfter, firesBefore, firesAfter, casesDone;
        readonly int[] setSeen = new int[OzFireSource.KindNames.Length], setFar = new int[OzFireSource.KindNames.Length];

        public static void Begin(string folder, string sets = null)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play in the Unit editor first"); return; }
            Folder = folder; if (!string.IsNullOrEmpty(sets)) Sets = sets;
            ZGame.I.gameObject.AddComponent<OzFireSourceCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; OzFireSource.Record = false; Time.captureFramerate = 0; }

        sealed class Row
        {
            public bool hit, died; public string why = "";
            public int seen, far, fires, shoves; public float worst; public string kinds = "", rows = "";
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>();
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            bool on0 = OzFireSource.On;
            md.AppendLine($"# Fire has a source: check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), level {G.level}, {(OzUnitEditor.Active ? "the Unit editor" : "a level")}, 60 fps frame-locked").AppendLine();
            md.AppendLine($"rules: Mammoth ram {ZWorld.MammothRamDeviation}, Bizon crush {ZWorld.BizonCrushDeviation}, big tank wreck shove {OzBigTankWreckShove.On}; pyro tongues ready {OpenZPyroFire.TonguesReady}, fx player {(OpenZFxPlayer.I && OpenZFxPlayer.I.isActiveAndEnabled)}").AppendLine();
            md.AppendLine($"Columns: fires = fires noted (one per burn beat, per flame tongue and frame, per sticky lick, per piece beat, per burst) from the start of the case to {Seconds:0} s after the death; "
                + $"far = of them farther than {OzFireSource.NearM:0.0} m from every solid part drawn under their source at the end of their frame (or their source shows nothing); "
                + "far fires = the distinct fires (kind + source) among them; worst = the farthest (m; 99 = the source shows nothing); by kind = noted / far.").AppendLine();
            if (OzUnitEditor.Active) OzUnitEditor.SetPanelsHidden(true);
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            foreach (var set in Sets.Split(','))
            {
                OzFireSource.On = set != "before";
                md.AppendLine().AppendLine($"## {set}: the tie {(OzFireSource.On ? "ON (the kit)" : "OFF (as the test exe)")}").AppendLine();
                md.AppendLine("| case | happened | fires | far | far fires | worst | wreck shove frames | by kind (noted / far) | the far ones | result |");
                md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                System.Array.Clear(setSeen, 0, setSeen.Length); System.Array.Clear(setFar, 0, setFar.Length);
                foreach (var c in Cases)
                {
                    Row r = null;
                    var e = One(c, me, foe, x => r = x);
                    while (e.MoveNext()) yield return e.Current;
                    if (r != null) Line(set, c.label, r);
                }
                var by = new StringBuilder();
                for (int i = 0; i < setSeen.Length; i++) if (setSeen[i] > 0) by.Append($"{OzFireSource.KindNames[i]} {setSeen[i]} / {setFar[i]}; ");
                md.AppendLine().AppendLine($"{set}, by kind over the 20 cases (noted / far): {by}");
            }
            Time.captureFramerate = 0;
            if (OzUnitEditor.Active) OzUnitEditor.SetPanelsHidden(false);
            OzFireSource.On = on0; OzFireSource.Record = false;
            md.AppendLine().AppendLine($"TOTAL after {casesDone} staged rams / crushes / big-tank deaths / shoves per set: before (tie off) {farBefore} far of {firesBefore} fires; after (tie on) {farAfter} far of {firesAfter} fires.");
            md.AppendLine($"{pass} PASS, {fail} FAIL (the 'before' rows are the test exe's behaviour, not counted). {OzFireSource.Status()}");
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "fire_source_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }

        IEnumerator One((Mode mode, string type, int ver, string label) c, Team me, Team foe, System.Action<Row> done)
        {
            var row = new Row(); done(row);
            if (OzUnitEditor.Active) { OzUePlayground.ClearField(); yield return null; }
            if (!Area(out int ox, out int oy, 30, 12)) { row.why = "no free ground near the camera"; yield break; }
            int mx = ox + 4 * 16, my = oy + 6 * 16;
            ZObject m = null, t;
            if (c.mode == Mode.BigDeath) t = Make(c.type, mx + 10 * 16, my, foe, c.ver);
            else
            {
                m = Make(c.mode == Mode.Ram ? ZBigTanks.Mammoth : ZBigTanks.Bizon, mx, my, me, 0);
                t = Make(c.type, mx + ZPathing.Radius(m) + 3 * 16 + 20, my, foe, c.ver);
                m.Damage = 0;
            }
            t.Damage = 0;
            yield return null;
            var mid = ZGame.PxToWorld(t.CenterX, t.CenterY);
            if (rts) rts.FlyTo(mid);
            Time.captureFramerate = 60;
            float until = Time.time + 1.5f; while (Time.time < until) yield return null;
            OzFireSource.ResetStats(); OzFireSource.Record = true;
            int shove0 = OzBigTankWreckShove.Shoves;
            var tv = G.Views.TryGetValue(t.Id, out var v0) ? v0 : null;
            if (c.mode == Mode.Ram || c.mode == Mode.Crush)
            {
                W.SendWaypoints(me, m, new[] { ZWaypoint.Move(mx + 22 * 16, my) });
                until = Time.time + 30f;
                while (Time.time < until && !t.RamDoomed) yield return null;
                if (!t.RamDoomed) { row.why = $"no {(c.mode == Mode.Ram ? "ram" : "crush")} in 30 s"; Take(row, shove0); Tidy(m, tv, mid); yield break; }
                row.hit = true;
                until = Time.time + 25f;
                while (Time.time < until && !(t.Destroyed || t.KillAt >= 0 || (tv && tv.Dying))) yield return null;
            }
            else
            {
                row.hit = true;
                t.SetHealth(0); W.UpdateObjectHealth(t, -1);
                if (c.mode == Mode.Shove)
                {   // the wreck burns for a moment, then the Bizon drives through it
                    until = Time.time + 1.2f; while (Time.time < until) yield return null;
                    W.SendWaypoints(me, m, new[] { ZWaypoint.Move(mx + 22 * 16, my) });
                }
                until = Time.time + 3f;
                while (Time.time < until && !(t.Destroyed || t.KillAt >= 0 || (tv && tv.Dying))) yield return null;
            }
            row.died = t.Destroyed || t.KillAt >= 0 || (tv && tv.Dying);
            if (!row.died) row.why = "the target did not die";
            until = Time.time + Seconds;
            while (Time.time < until) yield return null;
            yield return null;                                                                          // the last frame's fires are judged in its LateUpdate
            Take(row, shove0);
            Tidy(m, tv, mid);
            yield return null;
        }

        void Take(Row row, int shove0)
        {
            OzFireSource.Record = false;
            var kinds = new StringBuilder();
            for (int i = 0; i < OzFireSource.Seen.Length; i++)
            {
                row.seen += OzFireSource.Seen[i]; row.far += OzFireSource.Far[i]; row.worst = Mathf.Max(row.worst, OzFireSource.Worst[i]);
                setSeen[i] += OzFireSource.Seen[i]; setFar[i] += OzFireSource.Far[i];
                if (OzFireSource.Seen[i] > 0) kinds.Append($"{OzFireSource.KindNames[i]} {OzFireSource.Seen[i]} / {OzFireSource.Far[i]}; ");
            }
            row.fires = OzFireSource.FarFires.Count; row.kinds = kinds.ToString();
            row.rows = string.Join("; ", OzFireSource.FarRows.GetRange(0, Mathf.Min(4, OzFireSource.FarRows.Count)));
            row.shoves = OzBigTankWreckShove.Shoves - shove0;
        }

        void Line(string set, string label, Row r)
        {
            bool happened = r.hit && r.died;
            bool ok = happened && r.far == 0 && r.seen > 0;
            string what = happened ? "yes" : r.why;
            md.AppendLine($"| {label} | {what} | {r.seen} | {r.far} | {r.fires} | {r.worst:0.00} | {r.shoves} | {r.kinds} | {r.rows} | {(set == "before" ? (r.far > 0 ? "fault shown" : "no fault here") : ok ? "PASS" : "FAIL")} |");
            if (set == "before") { farBefore += r.far; firesBefore += r.seen; }
            else { farAfter += r.far; firesAfter += r.seen; if (ok) pass++; else fail++; }
            casesDone = Cases.Length;
        }

        ZObject Make(string type, int cx, int cy, Team team, int ver)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, team);
            if (ver > 0)
            {
                if (type == "heavy") OzHeavyTank.Pin(o, ver);
                else foreach (var u in OzV2Units.All) if (u.Type == type) u.Pin(o, ver);
            }
            W.Emit(ZEventType.Created, o);
            if (OzUnitEditor.Active) OzUePlayground.MarkChanged();
            return o;
        }

        void Tidy(ZObject m, UnitView tv, Vector3 mid)
        {
            Time.captureFramerate = 0;
            if (m != null && !m.Destroyed && m.KillAt < 0) { if (OzUnitEditor.Active) OzUePlayground.Remove(m); else { m.SetHealth(0); W.UpdateObjectHealth(m, -1); } }
            if (tv) { foreach (var kv in new List<KeyValuePair<int, UnitView>>(G.Views)) if (kv.Value == tv) G.Views.Remove(kv.Key); Destroy(tv.gameObject); }
            if (OzUnitEditor.Active) { OzUePlayground.SweepDebrisIn(q => (q - mid).sqrMagnitude < 60f * 60f); OzUePlayground.ClearField(); }
        }

        bool Area(out int ox, out int oy, int w, int h)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - w / 2, ty = cy + dy - h / 2;
                        if (Free(tx, ty, w, h)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
            if (tx < 1 || ty < 1 || tx + w > W.Map.width - 1 || ty + h > W.Map.height - 1) return false;
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                    if (W.PlatformAtTile(i, j) != 0) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building || o.Type == ZWorld.PlatformType)) return false;
            return true;
        }
    }
}
#endif
