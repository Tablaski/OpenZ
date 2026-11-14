#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 9 Play watch (View/WreckGroundRest.cs): no vehicle or wreck below the ground. Menu OpenZ/V1.3/Wrecks: Item 9 Sunk Watch, started
    /// in a level (best a computer-vs-computer match at 4x, Options BOT MATCH) or in the Unit editor with fights running. Every 0.5 s (real) it
    /// looks at every drawn vehicle and cannon view (alive or a wreck, not one sinking away at the end of its life, not on a fort tower, not
    /// in water) and every vehicle wreck piece at rest:
    ///  sunk = more than half of its height and more than 0.4 m under the ground at its middle, or its top under the ground.
    /// It writes every sunk sighting (type, alive or wreck, flipped or not, depth, place, time) with a frame of the first one of each object
    /// (normal RTS camera, flown to it), and the totals. Pass: 0 sunk sightings over the watch. Stop it with the same menu (or after
    /// <see cref="Minutes"/> real minutes). Output: <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzSunkWatch : MonoBehaviour
    {
        public static float Minutes = 20f;
        public static string Report = "idle";
        public static string Folder => OzRelease.Milestones("v13_item09_sunk");
        static OzSunkWatch run;
        public static void Toggle()
        {
            if (run) { run.Finish(); return; }
            Report = "running"; run = new GameObject("OzSunkWatch").AddComponent<OzSunkWatch>();
        }

        readonly StringBuilder sb = new StringBuilder();
        readonly HashSet<int> seen = new HashSet<int>();
        int looks, views, pieces, sunk, sunkLive, sunkWreck, sunkPiece; float t0, next; bool done;

        void Start() { t0 = Time.realtimeSinceStartup; Directory.CreateDirectory(Folder); Line($"watch started in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, rule {(WreckGroundRest.Enabled ? "on" : "off")}"); }
        void Line(string t) { sb.AppendLine(t); Debug.Log("[OzSunkWatch] " + t); }

        internal static bool Bounds(Transform t, out Bounds b)
        {
            b = new Bounds(t.position, Vector3.zero); bool any = false;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                var n = r.name.ToLowerInvariant(); if (n.Contains("shadow") || n.Contains("label") || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        internal static bool Sunk(Bounds b, float gy, out float deep)
        {
            deep = gy - b.min.y;
            return b.max.y < gy || (deep > 0.4f && deep > b.size.y * 0.5f);
        }

        /// <summary>
        /// in the ground PieceGraceSec (real) after it was first seen there. The hulls and guns count at once, as before.</summary>
        public static float PieceGraceSec = 1.5f;
        readonly Dictionary<int, float> pieceSeen = new Dictionary<int, float>();
        bool PieceStays(WreckPieces p)
        {
            int id = p.GetInstanceID(); float now = Time.realtimeSinceStartup;
            if (!pieceSeen.TryGetValue(id, out float first)) { pieceSeen[id] = now; return false; }
            return now - first >= PieceGraceSec;
        }

        void Update()
        {
            if (done) return;
            var g = ZGame.I; if (!g || g.World == null) return;
            if (Time.realtimeSinceStartup - t0 > Minutes * 60f) { Finish(); return; }
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 0.5f; looks++;
            foreach (var v in FindObjectsByType<UnitView>(FindObjectsSortMode.None))                    // review: every drawn view, also a wreck's view the game's list no longer holds (test only, 2 a second)
            {
                if (!v || !v.gameObject.activeInHierarchy || v.Obj == null || v.OnTower || v.WreckSinking) continue;
                if (v.Obj.Kind != ZKind.Vehicle && v.Obj.Kind != ZKind.Cannon) continue;
                if (OzWaterLanding.InWater(v.transform) || !Bounds(v.transform, out var b)) continue;
                views++;
                if (!Sunk(b, g.GroundY(b.center.x, b.center.z), out float deep)) continue;
                bool alive = !v.Dying; sunk++; if (alive) sunkLive++; else sunkWreck++;
                Line($"SUNK {(alive ? "live" : "wreck")} {v.Obj.Type} #{v.Obj.Id}{(v.Dying ? (v.FlippedWreck ? " (flipped)" : "") : "")} {deep:0.00} m in at ({b.center.x:0.0}, {b.center.z:0.0}), game {g.World.Time:0.0} s");
                if (seen.Add(v.Obj.Id)) Frame(g, b.center, $"sunk_{v.Obj.Type}_{v.Obj.Id}");
            }
            foreach (var p in FindObjectsByType<WreckPieces>(FindObjectsSortMode.None))
            {
                if (!p || p.Fading || !p.TryGetComponent<Rigidbody>(out var rb) || !(rb.isKinematic || rb.IsSleeping())) continue;
                if (OzWaterLanding.InWater(p.transform) || !Bounds(p.transform, out var b)) continue;
                pieces++;
                if (!Sunk(b, g.GroundY(b.center.x, b.center.z), out float deep)) continue;
                if (!PieceStays(p)) continue;
                sunk++; sunkPiece++;
                Line($"SUNK wreck piece {p.name} {deep:0.00} m in at ({b.center.x:0.0}, {b.center.z:0.0}), game {g.World.Time:0.0} s");
                if (seen.Add(p.GetInstanceID())) Frame(g, b.center, "sunk_piece_" + p.GetInstanceID());
            }
        }

        void Frame(ZGame g, Vector3 at, string name)
        {
            var p = Path.Combine(Folder, name + ".png");
            ScreenCapture.CaptureScreenshot(p);                                                           // the frame as the player sees it now (the camera is left where it is)
            Line("frame " + p);
        }

        void Finish()
        {
            if (done) return; done = true;
            Report = $"{(sunk == 0 ? "PASS" : "FAIL")} sunk sightings {sunk} (live {sunkLive}, wrecks {sunkWreck}, wreck pieces {sunkPiece}) in {looks} looks over {(Time.realtimeSinceStartup - t0) / 60f:0.0} real min " +
                     $"({views} vehicle / gun looks, {pieces} resting piece looks); lifted so far: hulls {WreckGroundRest.HullsLifted} (deepest {WreckGroundRest.DeepestHullM:0.00} m), pieces {WreckGroundRest.PiecesLifted}";
            Line(Report);
            File.WriteAllText(Path.Combine(Folder, "sunk_watch_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), sb.ToString());
            if (run == this) run = null;
            Destroy(gameObject);
        }
    }
}
#endif
