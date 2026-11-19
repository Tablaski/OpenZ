// OpenZ (GPL-3.0) - version 1.5 kit bridge_restore_r3: the restore's counted check (no Play) and its film helper (Play).
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 bridge_restore_r3. Menu OpenZ/Checks/V1.5/Bridge restore (no Play): the shader and its Resources material, the two new sounds
    /// and the game's own clank / thud takes it names, and every bridge kind's intact model (desert, jungle, city, the long city bridge of
    /// level 18) cut into its pieces: odd slice count, two halves each, every piece with its solid mesh and materials, the pieces' area =
    /// the model's area (the cut loses nothing), the cut's time. Menu OpenZ/V1.5/Bridge Restore/Film nearest bridge (Play): the bridge
    /// nearest the camera destroyed by the rules' own death, its automatic repair waited out (game speed 8, then 1), every frame of the
    /// restore drawn from a WIDE RTS view (whole bridge) and a closer view into Logs/bridge_restore_r3/ (kit on = AFTER, then the same with
    /// the kit off = BEFORE).
    /// </summary>
    public static class OzV15BridgeRestoreCheck
    {
        const string Tag = "[OpenZ][bridge_restore_r3]";
        public const string Out = "Logs/bridge_restore_r3/";
        static readonly string[] Kinds = { "desert", "jungle", "city", "city_long" };

        [MenuItem("OpenZ/Checks/V1.5/Bridge restore (no Play)")]
        static void Menu() { var r = RunCheck(); Debug.Log(Tag + "\n" + r); }

        static int Line(StringBuilder sb, bool ok, string what) { sb.AppendLine((ok ? "PASS " : "FAIL ") + what); return ok ? 0 : 1; }

        public static string RunCheck()
        {
            var sb = new StringBuilder(); int f = 0;
            var mat = Resources.Load<Material>(OzBridgeRestore.MaterialPath);
            f += Line(sb, mat && mat.shader && mat.shader.name == OzBridgeRestore.ShaderName, $"material Resources/{OzBridgeRestore.MaterialPath} with shader {OzBridgeRestore.ShaderName} ({(mat && mat.shader ? mat.shader.name : "missing")})");
            var sh = Shader.Find(OzBridgeRestore.ShaderName);
            f += Line(sb, sh && sh.isSupported, $"shader {OzBridgeRestore.ShaderName} {(sh ? (sh.isSupported ? "supported" : "NOT supported") : "missing")}");
            if (sh) f += Line(sb, !ShaderUtil.ShaderHasError(sh), "shader compiles without errors");
            foreach (var c in new[] { "br_restore_ring_a", "br_restore_energy_a", "sd_tank_breech_clank_a", "sd_tank_breech_clank_b", "final-metal-land-a", "final-metal-land-b" })
            {
                var clip = Resources.Load<AudioClip>("OpenZ/Audio/Sfx/" + c);
                f += Line(sb, clip, $"sound {c} {(clip ? $"{clip.length:0.00} s" : "missing")}");
            }
            int heavy = 0; foreach (var n in OzSoundV3.BankMetalHeavy) if (Resources.Load<AudioClip>("OpenZ/Audio/Sfx/" + n) || Resources.Load<AudioClip>("OpenZ/Audio/Sfx/SoundV3/" + n)) heavy++;
            f += Line(sb, heavy >= 3, $"the heavy metal bank for the last lock: {heavy} of {OzSoundV3.BankMetalHeavy.Length} takes found");
            foreach (var k in Kinds)
            {
                string path = $"Assets/OpenZ/Prefabs/Z_bridge_{k}_intact.prefab";
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!pf) { f += Line(sb, false, path + " missing"); continue; }
                var go = Object.Instantiate(pf); go.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var c = OzBridgeRestore.Cut(go, OzBridgeRestore.SliceM);
                    if (c == null) { f += Line(sb, false, k + ": nothing to cut"); continue; }
                    bool odd = c.slices % 2 == 1;
                    int full = 0, mats = 0; foreach (var p in c.pieces) { if (p.solid && p.fx && p.solid.vertexCount > 0) full++; foreach (var m in p.materials) if (m) mats++; }
                    float lost = c.areaIn > 0f ? Mathf.Abs(c.areaOut - c.areaIn) / c.areaIn : 1f;
                    int ranks = (c.slices + 1) / 2, lastRank = 0; foreach (var p in c.pieces) if (p.rank == ranks - 1) lastRank++;
                    f += Line(sb, odd && c.pieces.Count >= c.slices * 2 - 2 && full == c.pieces.Count,
                        $"{k}: {c.half * 2f:0.0} x {c.width:0.0} m, {c.tris} triangles cut into {c.slices} slices x 2 = {c.pieces.Count} pieces (every one with its solid mesh: {full}), {ranks} ranks from the ends, {lastRank} lock last in the middle");
                    f += Line(sb, lost < 0.005f, $"{k}: the pieces' area {c.areaOut:0.00} m2 = the model's {c.areaIn:0.00} m2 ({lost * 100f:0.000} % apart)");
                    f += Line(sb, mats > 0, $"{k}: the pieces keep the bridge's own materials ({mats} material slots)");
                    f += Line(sb, c.ms < 1500f, $"{k}: the whole cut {c.ms:0.0} ms of work (in the game spread over the frames after the bridge falls, cached for the next time; wall clock of a loaded editor)");
                    var rs = new OzBridgeRestore.CutResult(); var job = OzBridgeRestore.CutJob(go, OzBridgeRestore.SliceM, rs, OzBridgeRestore.CutBudgetMs);
                    while (job.MoveNext()) { }
                    bool spread = rs.steps >= 10 && rs.stepMaxMs <= Mathf.Max(8f, 0.2f * rs.ms);           // never the whole cut in one frame (wall clock: a busy PC stretches single shares)
                    f += Line(sb, rs.done && rs.pieces.Count == c.pieces.Count && Mathf.Abs(rs.areaOut - c.areaOut) < 0.01f && spread,
                        $"{k}: the sliced cut ({OzBridgeRestore.CutBudgetMs:0.0} ms a frame) = the same {rs.pieces.Count} pieces and area, over {rs.steps} frames, {rs.stepMaxMs:0.0} ms in one frame at most of {rs.ms:0.0} ms");
                    foreach (var p in rs.pieces) { Object.DestroyImmediate(p.solid); Object.DestroyImmediate(p.fx); }
                    foreach (var p in c.pieces) { Object.DestroyImmediate(p.solid); Object.DestroyImmediate(p.fx); }
                }
                finally { Object.DestroyImmediate(go); }
            }
            sb.AppendLine(f == 0 ? " CHECK PASS (bridge_restore_r3, no Play)" : $" CHECK FAIL: {f} line(s)");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ film (Play)
        public static bool Busy;

        [MenuItem("OpenZ/V1.5/Bridge Restore/Film nearest bridge (Play)")]
        static void FilmMenu()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning(Tag + " enter Play on a level first"); return; }
            if (Busy) return;
            var cam = Camera.main; var at = cam ? cam.transform.position : Vector3.zero;
            ZObject best = null; float bd = float.MaxValue;
            foreach (var o in ZGame.I.World.Objects) if (o.IsBridge) { float d = (ZGame.I.CenterWorld(o) - at).sqrMagnitude; if (d < bd) { bd = d; best = o; } }
            if (best == null) { Debug.LogWarning(Tag + " no bridge on this level"); return; }
            ZGame.I.StartCoroutine(Film(ZGame.I, best, Out + System.DateTime.Now.ToString("HHmmss") + "/", null, true));
        }

        static void Kill(ZWorld W, ZObject o)
        {
            if (o == null || o.Destroyed) return;
            o.DamageHealth(o.Health + 1); W.UpdateObjectHealth(o, -1);
        }

        /// <summary>The bridge's length in tiles (16 px) along its longer side.</summary>
        public static int Tiles(ZObject o) => Mathf.Max(o.WidthPix, o.HeightPix) / 16;

        /// <summary>Destroys the bridge, waits for its automatic repair and draws every frame of the restore (kit on), then the same with the
        /// kit off (before). A bridge that starts destroyed is repaired first the way a crane's job ends (the same Revived): that restore is
        /// filmed too ("crane").</summary>
        public static IEnumerator Film(ZGame g, ZObject br, string dir, StringBuilder log, bool both)
        {
            Busy = true;
            var W = g.World; var cam = Camera.main; var rts = Object.FindFirstObjectByType<RtsCamera>(); var dn = Object.FindFirstObjectByType<DayNight>();
            bool bb = g.blueBot, rb = g.redBot; g.blueBot = false; g.redBot = false;
            if (rts) rts.enabled = false;
            var hud = Object.FindFirstObjectByType<OpenZHud>(); bool hudWas = hud && hud.hudVisible; if (hud) hud.SetVisible(false);   // clean frames: no badges, no HUD
            if (dn) { dn.running = false; dn.timeOfDay = 0.1f; dn.Apply(0.1f); }
            var c = g.CenterWorld(br);
            bool alongX = br.WidthPix >= br.HeightPix;
            var axisB = alongX ? Vector3.forward : Vector3.right;
            float len = Mathf.Max(br.WidthPix, br.HeightPix) / 16f * 2f;
            var views = new List<(string name, Vector3 pos, Quaternion rot)>();
            foreach (var (name, dist, pitch) in new[] { ("wide", Mathf.Max(80f, len * 3.4f), 56f), ("close", Mathf.Max(30f, len * 1.15f), 48f) })
            {
                var back = -axisB * Mathf.Cos(pitch * Mathf.Deg2Rad) * dist; var up = Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad) * dist;
                var pos = c + back + up; views.Add((name, pos, Quaternion.LookRotation(c - pos, Vector3.up)));
            }
            log?.AppendLine($"bridge #{br.Id} {br.Type} {OzBridgeKit.Biome(g.level)}, {Tiles(br)} tiles ({len:0} m), at ({c.x:0.0}, {c.y:0.0}, {c.z:0.0}), destroyed at start {br.Destroyed}; {OzBridgeRestore.Status()}");
            if (br.Destroyed)
            {
                br.DoAutoRepair = true; br.NextAutoRepairTime = 0;                                    // a crane's job ends exactly so (ZObject.Buildings.cs): repaired on the next tick
                yield return Record(g, br, dir + "crane_after/", views, log, false, 0f);
            }
            var modes = both ? new[] { true, false } : new[] { true };
            foreach (bool on in modes)
            {
                OzBridgeRestore.On = on;
                Kill(W, br);
                yield return null;
                if (br.Destroyed) { br.DoAutoRepair = true; br.NextAutoRepairTime = W.Time + 7.0; }       // the film's shortcut: the automatic repair 7 s after the fall (the restore runs on the rules' clock as in a game)
                double killT = W.Time;
                log?.AppendLine($"{(on ? "AFTER (kit on)" : "BEFORE (kit off)")}: destroyed at game time {killT:F1}, automatic repair at {br.NextAutoRepairTime:F1}");
                float real0 = Time.realtimeSinceStartup;
                g.gameSpeed = 4f;
                while (br.Destroyed && br.NextAutoRepairTime - W.Time > 4.0 && Time.realtimeSinceStartup - real0 < 600f) { Hold(cam, views[0]); yield return null; }
                g.gameSpeed = 1f;
                if (!br.Destroyed) { log?.AppendLine("FAIL the bridge was repaired before the film started"); continue; }
                yield return Record(g, br, dir + (on ? "after/" : "before/"), views, log, true, (float)(br.NextAutoRepairTime - W.Time));
            }
            OzBridgeRestore.On = true;
            g.blueBot = bb; g.redBot = rb; if (rts) rts.enabled = true; if (dn) dn.running = true; if (hud) hud.SetVisible(hudWas);
            log?.AppendLine("end: " + OzBridgeRestore.Status());
            Debug.Log($"{Tag} film written: {dir}");
            Busy = false;
        }

        static Vector3 pivot; static float push;                                                          // the wide view's slight push-in (6 % over the take)
        static void Hold(Camera cam, (string name, Vector3 pos, Quaternion rot) v)
        {
            if (!cam) return;
            var pos = v.name == "wide" && push > 0f ? pivot + (v.pos - pivot) * (1f - push) : v.pos;
            cam.transform.SetPositionAndRotation(pos, v.rot);
        }

        /// <summary>Every frame at 60 fps of game time from 3.8 s before the rules' repair (pre: the earlier 3.5 s chunk rise fits too) or from now
        /// (crane) to 1.3 s after it.</summary>
        static IEnumerator Record(ZGame g, ZObject br, string dir, List<(string name, Vector3 pos, Quaternion rot)> views, StringBuilder log, bool pre, float left)
        {
            var cam = Camera.main; var W = g.World;
            Directory.CreateDirectory(dir);
            int cf0 = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                pivot = g.CenterWorld(br); push = 0f;
                if (pre) while (br.Destroyed && br.NextAutoRepairTime - W.Time > 3.8) { Hold(cam, views[0]); yield return null; }
                double repairedAt = pre ? -1 : W.Time; int frame = 0;
                var sb = new StringBuilder();
                while (frame < 60 * 6)
                {
                    push = 0.06f * Mathf.Clamp01(frame / 330f);
                    Hold(cam, views[0]);
                    yield return null;
                    if (repairedAt < 0 && !br.Destroyed) repairedAt = W.Time;
                    foreach (var v in views)
                    {
                        Hold(cam, v);
                        var t = v.name == "wide" ? Shot(cam, 1920, 1080) : Shot(cam, 1280, 720);
                        if (t) { File.WriteAllBytes($"{dir}{v.name}_{frame:0000}.jpg", t.EncodeToJPG(90)); Object.Destroy(t); }
                    }
                    Hold(cam, views[0]);
                    sb.AppendLine($"{frame} game {W.Time:F3} destroyed {br.Destroyed} {OzBridgeRestore.Status()}");
                    frame++;
                    if (repairedAt >= 0 && W.Time - repairedAt > (pre ? 1.3 : OzBridgeRestore.Span + OzBridgeRestore.FadeAfter + 0.7)) break;   // after a crane: the whole animation runs after the repair
                }
                File.WriteAllText(dir + "frames.txt", sb.ToString());
                log?.AppendLine($"  {dir}: {frame} frames, repaired at game time {repairedAt:F2}; {OzBridgeRestore.Status()}");
            }
            finally { Time.captureFramerate = cf0; push = 0f; }
        }

        /// <summary>The camera drawn once into a new texture (sRGB bytes) through a render request (the full URP look).</summary>
        public static Texture2D Shot(Camera cam, int w, int h)
        {
            if (!cam) return null;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            try
            {
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(cam, req)) return null;
                RenderPipeline.SubmitRenderRequest(cam, req);
                var was = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = was;
                return tex;
            }
            finally { RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
#endif
