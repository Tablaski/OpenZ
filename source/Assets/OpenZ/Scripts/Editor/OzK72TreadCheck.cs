using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.2 kit K72 check (menu OpenZ/V1.2/K72 ...), item 72: a destroyed MLRS's tracks fall apart (OzTreadBreak.cs), like the
    /// heavy tank's. The check makes an enemy MLRS (or, for comparison, a heavy tank) on a free spot near the view's middle, kills it 1 s
    /// later, waits for its break (at once, or after its burn-out), then until the pieces rest (at most 14 s), counts on how many bodies
    /// each track's lengths lie (a track whose lengths all ride on one body = the closed ring), and writes its report (console +
    /// nothing is saved; the forced outcome is put back.
    /// </summary>
    public static class OzK72TreadCheck
    {
        const string M = "OpenZ/V1.2/K72 ";
        static string Out { get { var p = Path.Combine(OpenZPaths.Records, "milestones", "k72_mlrs_tracks_check"); Directory.CreateDirectory(p); return p; } }
        static string name = "-", type = "-", lead = "";
        static float until, shotAt = -1f, brokeAt = -1f, killAt;
        static int step, force0, treadBreaks0, tankBreaks0, binned0;
        static bool atDeath0;
        static Vector3 at; static float camYaw;
        static ZObject unit;
        static readonly List<MeshRenderer> tracks = new List<MeshRenderer>();

        static bool Ready(out ZGame g)
        {
            g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null) { Debug.LogWarning("[OpenZ K72] enter Play first (a level running)"); return false; }
            return true;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static Team Enemy(ZGame g) { var me = g.World.Objects.FirstOrDefault(o => o != null && o.IsFort && o.Owner != Team.Neutral); return me != null && me.Owner == Team.Red ? Team.Blue : Team.Red; }

        static bool Spot(ZGame g, Vector3 c, out int px, out int py)
        {
            px = py = 0; var W = g.World;
            for (float d = 0f; d <= 30f; d += 2f)
                for (int s = 0; s < 16; s++)
                {
                    var p = c + Quaternion.Euler(0f, s * 22.5f, 0f) * Vector3.forward * d;
                    var q = ZGame.WorldToPx(p); int tx = q.x >> 4, ty = q.y >> 4; bool ok = true;
                    for (int j = -2; j <= 2 && ok; j++) for (int i = -2; i <= 2 && ok; i++) ok = W.Path.TilePassable(tx + i, ty + j, false);
                    if (!ok) continue;
                    if (W.Objects.Any(o => o != null && !o.Destroyed && Flat(ZGame.PxToWorld(o.CenterX, o.CenterY), p) < 7f)) continue;
                    px = tx * 16; py = ty * 16; return true;
                }
            return false;
        }

        [MenuItem(M + "MLRS: Kill One, Its Tracks Fall Apart At Once + Report + Pictures (Play)")]
        static void MlrsNow() => Run("mlrs_now", "missile_launcher", false);
        [MenuItem(M + "MLRS: Kill One, It Burns Out And Cooks Off + Report + Pictures (Play)")]
        static void MlrsCook() => Run("mlrs_cookoff", "missile_launcher", true);
        [MenuItem(M + "Heavy Tank (for comparison): Kill One, Its Tracks Break + Report + Pictures (Play)")]
        static void HeavyNow() => Run("heavy_now", "heavy", false);
        [MenuItem(M + "State")]
        static void State() => Debug.Log("[OpenZ K72] " + OzTreadBreak.State + "\n" + OzTankWreck.State);

        static void Run(string check, string t, bool cook)
        {
            if (!Ready(out var g)) return;
            var cam = Camera.main;
            if (cam) camYaw = cam.transform.eulerAngles.y;
            var c = cam && g.GroundPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var gp) ? gp : Vector3.zero;
            if (!Spot(g, c, out int px, out int py)) { Debug.LogWarning("[OpenZ K72] no free spot near the view's middle"); return; }
            var o = g.World.CreateObject("vehicle", t, px, py, Enemy(g));
            if (o == null) { Debug.LogWarning("[OpenZ K72] could not make a " + t); return; }
            var v = g.SpawnUnitView(o); unit = o;
            tracks.Clear();
            if (v) foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true))
                    if (r && r.enabled && r.transform.parent && r.transform.parent.name != "OZ_WreckFragments" && (r.name.ToLowerInvariant().Contains("tread") || r.name.ToLowerInvariant().Contains("track"))) tracks.Add(r);
            at = ZGame.PxToWorld(o.CenterX, o.CenterY); at.y = g.GroundY(at.x, at.z);
            name = check; type = t;
            lead = $"an enemy {t} #{o.Id} killed near the view's middle; {(cook ? "it burns out and cooks off (5-7 s)" : "its wreck breaks apart at once")}; its track renderers: {string.Join(", ", tracks.Select(r => r.name))}";
            treadBreaks0 = OzTreadBreak.Breaks; tankBreaks0 = OzTankWreck.Breaks; binned0 = OzTreadBreak.Binned;
            force0 = WreckFx.Force; atDeath0 = WreckFx.BreakAtDeath; WreckFx.Force = 1; WreckFx.BreakAtDeath = !cook;
            step = -1; brokeAt = -1f; shotAt = -1f; killAt = Time.realtimeSinceStartup + 1f; until = Time.realtimeSinceStartup + 40f;
            Debug.Log("[OpenZ K72] " + check + ": " + lead);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void PutBack() { WreckFx.Force = force0; WreckFx.BreakAtDeath = atDeath0; }

        static bool Mlrs => type == "missile_launcher";

        /// <summary>The latest break's track lengths with their side (L / R).</summary>
        static List<(Transform t, string side)> Lengths()
        {
            var list = new List<(Transform, string)>();
            if (Mlrs) { for (int i = 0; i < OzTreadBreak.LastTread.Count; i++) if (OzTreadBreak.LastTread[i]) list.Add((OzTreadBreak.LastTread[i], OzTreadBreak.LastSide[i])); }
            else foreach (var p in OzTankWreck.LastPieces) if (p && p.name.StartsWith("OZ_TankPiece_belt")) list.Add((p, p.name.Contains("belt_l") ? "L" : p.name.Contains("belt_r") ? "R" : "?"));
            return list;
        }

        static Rigidbody BodyOf(Transform t) { for (var x = t; x; x = x.parent) if (x.TryGetComponent<Rigidbody>(out var b)) return b; return null; }

        static List<Rigidbody> Bodies() => Lengths().Select(l => BodyOf(l.t)).Where(b => b).Distinct().ToList();

        static void Shot(string tag, float dist, float pitch, float dyaw)
        {
            try
            {
                var ps = Lengths().Select(l => l.t.position).ToList(); var mid = at;
                if (ps.Count > 0) { mid = Vector3.zero; foreach (var p in ps) mid += p; mid /= ps.Count; mid.y = at.y; }
                var path = OpenZShot.Shot(mid, dist, pitch, camYaw + dyaw, Path.Combine(Out, "k72_" + name + "_" + tag + ".png"), 1920, 1080); Debug.Log("[OpenZ K72] picture " + path);
            }
            catch (System.Exception e) { Debug.LogWarning("[OpenZ K72] picture " + tag + " failed: " + e.Message); }
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; PutBack(); return; }
            float now = Time.realtimeSinceStartup;
            if (step == -1 && now >= killAt)
            {
                var g = ZGame.I; if (g && unit != null && !unit.Destroyed) { unit.DamageHealth(unit.Health); g.World.UpdateObjectHealth(unit); }
                step = 0;
            }
            if (step == 0 && (Mlrs ? OzTreadBreak.Breaks > treadBreaks0 : OzTankWreck.Breaks > tankBreaks0)) { step = 1; brokeAt = now; shotAt = now + 1.5f; PutBack(); }
            if (step == 1 && now >= shotAt) { Shot("70m_1_5s", 70f, 55f, 0f); step = 2; }
            if (step == 2)
            {
                var bodies = Bodies();
                bool rested = bodies.Count == 0 || bodies.All(b => b.isKinematic || b.IsSleeping());
                if ((rested && now - brokeAt > 4f) || now - brokeAt > 14f) step = 3;
            }
            if (step == 3 || now > until)
            {
                EditorApplication.update -= Tick; PutBack();
                Shot("35m_rest", 35f, 45f, 0f); Shot("35m_side_rest", 35f, 45f, 150f);
                Report(step == 3);
            }
        }

        static void Report(bool broke)
        {
            var sb = new StringBuilder("[OpenZ K72] " + name + ": " + lead + "\n");
            var lengths = Lengths();
            sb.AppendLine(OzV11Kit.Verdict(broke, broke ? "the wreck broke apart" : "the wreck did NOT break within 40 s"));
            int rings = 0, sides = 0;
            foreach (var side in new[] { "L", "R" })
            {
                var mine = lengths.Where(l => l.side == side).ToList();
                var bodies = mine.Select(l => BodyOf(l.t)).Where(b => b).Distinct().ToList();
                bool ring = mine.Count > 0 && bodies.Count <= 1;
                if (mine.Count > 0) sides++;
                if (ring) rings++;
                sb.AppendLine(OzV11Kit.Verdict(mine.Count > 1 && bodies.Count > 1,
                    $"the {(side == "L" ? "left" : "right")} track: {mine.Count} lengths on {bodies.Count} bodies (wanted more than 1 of each: it falls apart, no closed ring)"));
            }
            sb.AppendLine(OzV11Kit.Verdict(sides == 2 && rings == 0, $"tracks broken {sides - rings} of 2, closed rings left {rings} (wanted 0)"));
            int whole = tracks.Count(r => r && r.enabled);
            sb.AppendLine(OzV11Kit.Verdict(tracks.Count > 0 && whole == 0, $"the unit's whole track renderers still shown after the break: {whole} of {tracks.Count} (wanted 0: only the lengths are drawn)"));
            if (Mlrs) sb.AppendLine(OzV11Kit.Verdict(OzTreadBreak.NotReadable == 0, $"tracks binned at this break: {OzTreadBreak.Binned - binned0} (the right track has no baked fragments: 1 for the first MLRS of a session, 0 after: the same lengths are reused), not readable {OzTreadBreak.NotReadable}"));
            var all = Bodies();
            sb.AppendLine(OzV11Kit.Verdict(all.Count > 0 && all.All(b => b.GetComponent<WreckPieces>()), $"every body with a track length is a wreck piece (the same throw, clanks, settle, sink and fade): {all.Count(b => b.GetComponent<WreckPieces>())} of {all.Count}"));
            foreach (var b in all.OrderByDescending(b => b.mass))
            {
                var parts = string.Join(", ", b.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name != "OZ_ChunkCore").Select(r => r.name));
                sb.AppendLine($"      body {b.name} {b.mass:0} kg, {(b.isKinematic ? "rests" : b.IsSleeping() ? "asleep" : "MOVING")}: {parts}");
            }
            sb.AppendLine("      " + OzTreadBreak.State);
            sb.AppendLine("      " + WheeledBreakup.Last);
            sb.AppendLine($"      LOOK at the pictures {Out}/k72_{name}_*.png: the tracks lie in several lengths with the hull pieces, no closed track loop anywhere");
            var text = sb.ToString();
            Debug.Log(text);
            try { File.WriteAllText(Path.Combine(Out, "k72_" + name + "_report.txt"), text); } catch (System.Exception) { }
        }
    }
}
