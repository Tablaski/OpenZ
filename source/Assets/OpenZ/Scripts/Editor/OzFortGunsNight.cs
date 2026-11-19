// Editor only, the measure: Play a level scene (level 18), then the menu with a tag (Logs/fort_guns_night_tag.txt: "before" or "after").
// The level's opening is skipped, the clock is held, the sim paused and the HUD hidden; every turret kind (gun, gatling, howitzer,
// missile cannon) is stood on a free tower pad of both forts by the rules' own CreateObject + Created event (as a placed cannon), the
// normal game camera (height 70) looks at each fort by day (clock 0.08) and by night (0.60), and the luminance of each gun kind's pixels
// and of the fort's own pixels is measured (masks = what changes on screen when that gun's / the fort's renderers are switched off).
// Ratio = gun / fort. PASS for a kind: its night ratio within 10 % of its day ratio. Frames + numbers into
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzFortGunsNight
    {
        static readonly string[] Kinds = { "gun", "gatling", "howitzer", "missile_cannon" };
        static readonly (int lx, int ly)[] Spots = { (16, 0), (112, 0), (16, 48), (112, 48) };
        static IEnumerator<double> run; static double waitUntil; static string outDir, tag; static System.Text.StringBuilder log;

        [MenuItem("OpenZ/V1.5/Fort guns at night: measure (Play, a level)")]
        static void Begin()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning("[OzFortGunsNight] enter Play in a level first"); return; }
            tag = File.Exists("Logs/fort_guns_night_tag.txt") ? File.ReadAllText("Logs/fort_guns_night_tag.txt").Trim() : "run";
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/owner_frames/fort_guns_night/_measure")).Replace('\\', '/');
            Directory.CreateDirectory(outDir); log = new System.Text.StringBuilder();
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); Done(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION " + e); more = false; }
            if (!more) { Done(); return; }
            waitUntil = EditorApplication.timeSinceStartup + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null;
            try { File.WriteAllText(Path.Combine(outDir, tag + "_numbers.txt"), log.ToString()); } catch (Exception) { }
            Debug.Log("[OzFortGunsNight] DONE\n" + log);
        }

        static void Log(string s) { log.AppendLine(s); Debug.Log("[OzFortGunsNight] " + s); }
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        static Texture2D Grab() { var t = ScreenCapture.CaptureScreenshotAsTexture(); return t; }
        static float[] Lum(Texture2D t)
        {
            var px = t.GetPixels32(); var l = new float[px.Length];
            for (int i = 0; i < px.Length; i++) l[i] = (0.2126f * px[i].r + 0.7152f * px[i].g + 0.0722f * px[i].b) / 255f;
            return l;
        }
        /// <summary>The screen rectangle (px, bottom-up like GetPixels32) of a view's renderers, 8 px wider each side.</summary>
        static RectInt ScreenRect(GameObject g)
        {
            var cam = Camera.main; if (!cam || !g) return new RectInt(0, 0, Screen.width, Screen.height);
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var r in g.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                var b = r.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var c = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                    var sp = cam.WorldToScreenPoint(c); if (sp.z <= 0) continue;
                    x0 = Mathf.Min(x0, sp.x); y0 = Mathf.Min(y0, sp.y); x1 = Mathf.Max(x1, sp.x); y1 = Mathf.Max(y1, sp.y);
                }
            }
            int ix0 = Mathf.Clamp((int)x0 - 8, 0, Screen.width), iy0 = Mathf.Clamp((int)y0 - 8, 0, Screen.height), ix1 = Mathf.Clamp((int)x1 + 8, 0, Screen.width), iy1 = Mathf.Clamp((int)y1 + 8, 0, Screen.height);
            return new RectInt(ix0, iy0, Mathf.Max(0, ix1 - ix0), Mathf.Max(0, iy1 - iy0));
        }
        static bool In(RectInt r, int i, int w) { int x = i % w, y = i / w; return x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax; }
        static void SetOn(GameObject g, bool on) { if (g) foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = !on; }

        static IEnumerator<double> Run()
        {
            var g = ZGame.I; var W = g.World;
            for (int i = 0; i < 40; i++)
            {
                bool a = false; try { a = ZodBriefing.ProofSkipBriefing() || OzBlueCameo.ProofSkipBriefing(); } catch (Exception) { }
                if (!ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && !Flying && i > 6) break;
                yield return 0.5;
            }
            yield return 2.0;
            var forts = W.Objects.Where(o => o.IsFort && !o.Destroyed).ToList();
            var placed = new List<(ZObject c, ZObject fort)>();
            foreach (var f in forts)
            {
                for (int k = 0; k < Spots.Length; k++)
                {
                    int x = f.LocX + Spots[k].lx, y = f.LocY + Spots[k].ly;
                    var have = W.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && !o.Destroyed && o.LocX == x && o.LocY == y);
                    if (have != null) { placed.Add((have, f)); Log($"fort {f.Owner}: spot {k} already holds {have.Type}#{have.Id}"); continue; }
                    var c = W.CreateObject("cannon", Kinds[k], x, y, f.Owner);
                    W.Emit(ZEventType.Created, c, f);
                    placed.Add((c, f)); Log($"fort {f.Owner}: spot {k} {Kinds[k]}#{c.Id} placed");
                }
            }
            yield return 4.0;                                                                              // the views mount on their pads
            g.paused = true;
            var hud = OpenZHud.I; bool hudWas = hud && hud.hudVisible; if (hud) hud.hudVisible = false;
            var dn = UnityEngine.Object.FindFirstObjectByType<DayNight>(); bool runWas = dn && dn.running;
            var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
            var rows = new List<string>();
            foreach (var light in new[] { ("day", 0.08f), ("night", 0.60f) })
            {
                if (dn) { dn.running = false; dn.timeOfDay = light.Item2; }
                foreach (var f in forts)
                {
                    var fv = g.StaticViews.TryGetValue(f.Id, out var fg) ? fg : null;
                    if (rc) { rc.focus = ZGame.PxToWorld(f.CenterX, f.CenterY); rc.ZoomTo(70f); }
                    yield return 2.5;
                    var full = Grab(); var lf = Lum(full); int sw = full.width; var fr = ScreenRect(fv);
                    File.WriteAllBytes(Path.Combine(outDir, $"{tag}_{light.Item1}_{f.Owner.ToString().ToLower()}_fort.jpg"), full.EncodeToJPG(92));
                    var mine = placed.Where(p => p.fort == f).ToList();
                    // the fort's pixels: what changes when the fort's renderers (not its guns) are off
                    foreach (var p in mine) if (g.Views.TryGetValue(p.c.Id, out var v0) && v0) SetOn(v0.gameObject, false);
                    yield return 0.4; var noGuns = Lum(Grab());
                    SetOn(fv, false); yield return 0.4; var noFort = Lum(Grab()); SetOn(fv, true);
                    foreach (var p in mine) if (g.Views.TryGetValue(p.c.Id, out var v1) && v1) SetOn(v1.gameObject, true);
                    double fs = 0; int fn = 0;
                    for (int i = 0; i < lf.Length; i++) if (In(fr, i, sw) && Mathf.Abs(noGuns[i] - noFort[i]) > 0.04f && Mathf.Abs(lf[i] - noGuns[i]) < 0.02f) { fs += lf[i]; fn++; }
                    double fortL = fn > 0 ? fs / fn : 0;
                    foreach (var p in mine)
                    {
                        if (!g.Views.TryGetValue(p.c.Id, out var v) || !v) { Log($"{light.Item1} {f.Owner} {p.c.Type}: no view"); continue; }
                        var gr = ScreenRect(v.gameObject);
                        SetOn(v.gameObject, false); yield return 0.4; var off = Lum(Grab()); SetOn(v.gameObject, true);
                        double gs = 0; int gn = 0;
                        for (int i = 0; i < lf.Length; i++) if (In(gr, i, sw) && Mathf.Abs(lf[i] - off[i]) > 0.04f) { gs += lf[i]; gn++; }
                        double gunL = gn > 0 ? gs / gn : 0;
                        string row = $"{light.Item1},{f.Owner},{p.c.Type},{(v.OnTower ? "tower" : "NOT ON TOWER")},{gunL:F4},{gn},{fortL:F4},{fn},{(fortL > 0 ? gunL / fortL : 0):F3}";
                        rows.Add(row); Log(row);
                    }
                    UnityEngine.Object.Destroy(full);
                }
            }
            Log("light,fort,kind,mount,gun_lum,gun_px,fort_lum,fort_px,ratio");
            foreach (var kind in Kinds)
                foreach (var f in forts)
                {
                    var d = rows.FirstOrDefault(r => r.StartsWith("day," + f.Owner + "," + kind + ","));
                    var n = rows.FirstOrDefault(r => r.StartsWith("night," + f.Owner + "," + kind + ","));
                    if (d == null || n == null) { Log($"RESULT {kind} {f.Owner}: no pair"); continue; }
                    float rd = float.Parse(d.Split(',').Last(), System.Globalization.CultureInfo.InvariantCulture), rn = float.Parse(n.Split(',').Last(), System.Globalization.CultureInfo.InvariantCulture);
                    float dev = rd > 0 ? Mathf.Abs(rn / rd - 1f) : 9f;
                    Log($"RESULT {(dev <= 0.10f ? "PASS" : "FAIL")} {kind} on the {f.Owner} fort: day ratio {rd:F3}, night ratio {rn:F3}, night / day {rn / Mathf.Max(rd, 1e-4f):F3} (within 10 % wanted)");
                }
            if (dn) { dn.running = runWas; } if (hud) hud.hudVisible = hudWas; g.paused = false;
        }
    }
}
