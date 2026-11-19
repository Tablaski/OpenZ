// OpenZ (GPL-3.0)
#if UNITY_EDITOR
using System.Collections;
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
    /// 1.5 factory_restore_crane_r3.
    /// <list type="bullet">
    /// <item>OpenZ/V1.5/Building Restore/Bake readable meshes (no Play): the four building prefabs' GLB meshes are not Read/Write in a
    /// player; a readable copy of each goes to Resources/OpenZ/RestoreMeshes/ (what OzBuildingRestore cuts). Copies already there are kept.</item>
    /// <item>OpenZ/Checks/V1.5/Building restore (no Play): every building prefab cut into its pieces (pieces, layers, area kept, time).</item>
    /// <item>OpenZ/V1.5/Crane Restore/1 Count (Play, a level): every building of the level cut as it stands; then EVERY bridge destroyed and
    /// repaired by a crane from BOTH ends, the crane's drawn ground height measured every frame against the destroyed model's own deck top
    /// under it (rays at its centre): never over the gap, within 5 cm standing (8 cm driving: the suspension's bounce); Logs/crane_restore_r3/.</item>
    /// <item>2 Film: factory repair / 3 Film: long bridge (Play, a level): the owner clips' events in normal play from a WIDE slow-orbit film
    /// camera with nothing on screen but the game (HUD, canvases, rings, zone wash hidden; music muted; day light): record with OBS while it
    /// runs (the log says when it starts and ends).</item>
    /// </list>
    /// </summary>
    public static class OzV15CraneRestoreCheck
    {
        const string Tag = "[OpenZ][factory_restore_crane_r3]";
        public const string Out = "Logs/crane_restore_r3/";
        static readonly string[] Types = { "robot_factory", "vehicle_factory", "radar", "repair" };
        static bool busy;

        // ------------------------------------------------------------------ bake (no Play)
        [MenuItem("OpenZ/V1.5/Building Restore/Bake readable meshes (no Play)")]
        static void Bake()
        {
            const string dir = "Assets/OpenZ/Resources/OpenZ/RestoreMeshes";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/OpenZ/Resources/OpenZ")) { Debug.LogWarning(Tag + " Assets/OpenZ/Resources/OpenZ is missing"); return; }
                AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "RestoreMeshes");
            }
            var sb = new StringBuilder(); int made = 0, kept = 0, readable = 0;
            foreach (var type in Types)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/OpenZ/Prefabs/Z_{type}.prefab");
                if (!pf) { sb.AppendLine($"{type}: prefab missing"); continue; }
                foreach (var mf in pf.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh; if (!m) continue;
                    if (m.isReadable) { readable++; continue; }
                    string path = $"{dir}/{OzBuildingRestore.MeshKey(type, m)}.asset";
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) { kept++; continue; }
                    var c = new Mesh { name = OzBuildingRestore.MeshKey(type, m), indexFormat = m.indexFormat };
                    c.SetVertices(m.vertices);
                    if (m.normals.Length == m.vertexCount) c.SetNormals(m.normals);
                    if (m.tangents.Length == m.vertexCount) c.SetTangents(m.tangents);
                    if (m.uv.Length == m.vertexCount) c.SetUVs(0, m.uv);
                    if (m.uv2.Length == m.vertexCount) c.SetUVs(1, m.uv2);
                    if (m.colors.Length == m.vertexCount) c.SetColors(m.colors);
                    c.subMeshCount = m.subMeshCount;
                    for (int s = 0; s < m.subMeshCount; s++) c.SetIndices(m.GetIndices(s), m.GetTopology(s), s, false);
                    c.RecalculateBounds();
                    AssetDatabase.CreateAsset(c, path); made++;
                    sb.AppendLine($"{type}: {m.name} ({m.vertexCount} vertices, {m.subMeshCount} submeshes) -> {path}");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} readable copies: made {made}, kept {kept}, already readable {readable}\n{sb}");
        }

        // ------------------------------------------------------------------ the cut of every building prefab (no Play)
        [MenuItem("OpenZ/Checks/V1.5/Building restore (no Play)")]
        static void CheckMenu() { Debug.Log(Tag + "\n" + RunCheck()); }

        static int Line(StringBuilder sb, bool ok, string what) { sb.AppendLine((ok ? "PASS " : "FAIL ") + what); return ok ? 0 : 1; }

        public static string RunCheck()
        {
            var sb = new StringBuilder(); int f = 0;
            f += Line(sb, OzBuildingRestore.Active, $"the bridge kit's restore shader and material are here (OzBridgeRestore: {OzBridgeRestore.MaterialPath})");
            foreach (var type in Types)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/OpenZ/Prefabs/Z_{type}.prefab");
                if (!pf) { f += Line(sb, false, $"Z_{type} prefab missing"); continue; }
                var go = Object.Instantiate(pf); go.hideFlags = HideFlags.HideAndDontSave;
                try { f += CutLine(sb, go, type, type + " (prefab)"); }
                finally { Object.DestroyImmediate(go); }
            }
            sb.AppendLine(f == 0 ? " CHECK PASS (factory_restore_crane_r3, building restore, no Play)" : $" CHECK FAIL: {f} line(s)");
            return sb.ToString();
        }

        static int CutLine(StringBuilder sb, GameObject go, string type, string what)
        {
            bool ok = OzBuildingRestore.CheckCut(go, type, out int pieces, out int slices, out int layers, out float aIn, out float aOut, out float ms, out int steps, out float stepMax, out int parts, out int hideOnly);
            float lost = aIn > 0f ? Mathf.Abs(aOut - aIn) / aIn : 1f;
            return Line(sb, ok && hideOnly == 0 && lost < 0.005f && slices % 2 == 1,
                $"{what}: {parts} part(s) cut into {pieces} pieces ({slices} slices x 2 x {layers} layer(s)), parts without a readable mesh {hideOnly} (menu Bake readable meshes), "
                + $"area {aIn:0.0} -> {aOut:0.0} m2 ({lost * 100f:0.000} % apart), {ms:0.0} ms of work over {steps} frame(s) at {OzBuildingRestore.CutBudgetMs:0.0} ms (largest share {stepMax:0.0} ms)");
        }

        // ------------------------------------------------------------------ helpers (Play)
        static void Kill(ZWorld W, ZObject o)
        {
            if (o == null || o.Destroyed) return;
            o.DamageHealth(o.Health + 1); W.UpdateObjectHealth(o, -1);
        }

        static bool FreeTile(ZWorld W, int cx, int cy, int foot, out int tx, out int ty)
        {
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy; bool ok = true;
                        for (int j = 0; j < foot && ok; j++) for (int i = 0; i < foot && ok; i++) ok = W.Path.TilePassable(x + i, y + j, false);
                        if (ok) { tx = x; ty = y; return true; }
                    }
            tx = cx; ty = cy; return false;
        }

        static ZObject Spawn(ZGame g, string type, int tx, int ty, Team team)
        {
            var W = g.World;
            if (!FreeTile(W, tx, ty, 2, out tx, out ty)) return null;
            var o = W.CreateObject("vehicle", type, tx * 16, ty * 16, team);
            if (o != null) g.SpawnUnitView(o);
            return o;
        }

        static bool Order(ZGame g, ZObject u, WpMode mode, ZObject t, int x = 0, int y = 0) =>
            u != null && g.World.SendWaypoints(g.player, u, new[] { new ZWaypoint { mode = mode, refId = t != null ? t.Id : -1, x = t != null ? t.CenterX : x, y = t != null ? t.CenterY : y, playerGiven = true } });

        /// <summary>The bridge's two crane entrances (Zod's, px) and the outward direction of each (tiles).</summary>
        static (int x, int y, int dx, int dy)[] Ends(ZObject br)
        {
            br.GetCraneEntrance(out int ex, out int ey, out int ex2, out int ey2);
            return br.Type == "bridge_vert" ? new[] { (ex, ey, 0, -1), (ex2, ey2, 0, 1) } : new[] { (ex, ey, -1, 0), (ex2, ey2, 1, 0) };
        }

        static UnitView View(ZGame g, ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;

        /// <summary>The destroyed model's own deck top under a point (rays at the visible geometry; temporary colliders on the model).</summary>
        sealed class Truth
        {
            readonly List<MeshCollider> cols = new List<MeshCollider>();
            public Truth(GameObject wreck)
            {
                if (!wreck) return;
                foreach (var mf in wreck.GetComponentsInChildren<MeshFilter>())
                {
                    var r = mf.GetComponent<MeshRenderer>(); if (!r || !mf.sharedMesh) continue;
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; cols.Add(mc);
                }
            }
            public bool Top(Vector3 at, float from, out float y)
            {
                y = float.NegativeInfinity; var ray = new Ray(new Vector3(at.x, from, at.z), Vector3.down);
                foreach (var c in cols) if (c && c.Raycast(ray, out var h, 60f)) y = Mathf.Max(y, h.point.y);
                return !float.IsNegativeInfinity(y);
            }
            public void Done() { foreach (var c in cols) if (c) Object.Destroy(c); cols.Clear(); }
        }

        sealed class Meter
        {
            public int frames, onDeck, gap, held; public float maxStand, maxDrive; public string worst = "";
            /// <summary>One frame of the crane: over the destroyed bridge's deck footprint its drawn ground (transform y + 0.01, UnitView's
            /// vehicle yOffset) against the deck top under its centre.</summary>
            public void Sample(ZGame g, ZObject br, UnitView v, OzCraneDeck.Deck d, Truth tr, bool standing)
            {
                if (!v || d == null || !d.ok) return;
                frames++;
                var p = v.transform.position; float a = d.Along(p), b = d.Across(p);
                if (a < d.a0 || a > d.a1 || Mathf.Abs(b - d.bMid) > d.halfW) return;                 // not over the bridge's deck footprint (bank, ramp)
                bool deck = tr.Top(p, d.deckY + 6f, out float top) && top >= d.deckY - 0.35f;
                if (!deck) { gap++; worst = $"over the gap at along {a:0.0} m (deck {d.a0:0.0}..{d.a1:0.0}, standing {d.stumpA:0.0} + {d.stumpB:0.0} m), drawn y {p.y:0.00}"; return; }
                onDeck++;
                float dy = Mathf.Abs(p.y + 0.01f - top);
                if (standing) maxStand = Mathf.Max(maxStand, dy); else maxDrive = Mathf.Max(maxDrive, dy);
            }
        }

        // ------------------------------------------------------------------ 1 count (Play)
        [MenuItem("OpenZ/V1.5/Crane Restore/1 Count: building cuts + a crane on every bridge from both ends (Play, a level)")]
        static void CountMenu()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning(Tag + " enter Play on a level first"); return; }
            if (busy) return;
            ZGame.I.StartCoroutine(Count(ZGame.I));
        }

        static IEnumerator Count(ZGame g)
        {
            busy = true;
            var W = g.World; var sb = new StringBuilder(); int f = 0;
            bool bb = g.blueBot, rb = g.redBot; g.blueBot = false; g.redBot = false; float sp0 = g.gameSpeed;
            sb.AppendLine($"factory_restore_crane_r3 count, level {g.level}, {System.DateTime.Now:dd MMM yyyy HH:mm}");
            // the level's buildings as they stand
            foreach (var o in W.Objects.ToList())
                if (OzBuildingRestore.Eligible(o) && !o.Destroyed && g.StaticViews.TryGetValue(o.Id, out var go) && go)
                    f += CutLine(sb, go, o.Type, $"{o.Type} #{o.Id} {o.Owner} at ({o.LocX >> 4}, {o.LocY >> 4})");
            var rv = g.GetComponent<RulesView>(); if (!rv) rv = Object.FindFirstObjectByType<RulesView>();
            foreach (var br in W.Objects.Where(o => o.IsBridge).ToList())
            {
                int tiles = Mathf.Max(br.WidthPix, br.HeightPix) / 16; bool lng = tiles >= OzBridgeKit.LongFromTiles;
                var ends = Ends(br);
                for (int e = 0; e < 2; e++)
                {
                    if (!br.Destroyed) Kill(W, br);
                    for (int i = 0; i < 3; i++) yield return null;
                    if (!br.Destroyed) { f += Line(sb, false, $"bridge #{br.Id}: could not be destroyed"); break; }
                    br.DoAutoRepair = true; br.NextAutoRepairTime = W.Time + 900.0;                          // the count's crane repairs it (no timer in between)
                    var d = OzCraneDeck.Get(g, br);
                    var models = rv ? rv.BridgeModels(br) : null; var tr = new Truth(models != null && models.Length > 2 ? models[2] : null);
                    var (ex, ey, ox, oy) = ends[e];
                    var crane = Spawn(g, "crane", (ex >> 4) + ox * 4, (ey >> 4) + oy * 4, g.player);
                    yield return new WaitForSeconds(0.3f);
                    bool sent = Order(g, crane, WpMode.CraneRepair, br);
                    var m = new Meter(); double t0 = W.Time; int held0 = OzCraneDeck.HeldFrames;
                    g.gameSpeed = 2f;
                    while (sent && br.Destroyed && W.Time - t0 < 150.0 && crane != null && !crane.Destroyed)
                    {
                        int h = OzCraneDeck.HeldFrames;
                        yield return null;
                        m.Sample(g, br, View(g, crane), d, tr, OzCraneDeck.HeldFrames > h);
                    }
                    g.gameSpeed = 1f;
                    bool fixedOk = !br.Destroyed;
                    tr.Done();
                    bool standOk = d != null && d.ok && (d.stumpA + d.stumpB < 0.1f || m.onDeck > 0);
                    f += Line(sb, sent && fixedOk && m.gap == 0 && standOk && m.maxStand <= 0.05f && m.maxDrive <= 0.08f,
                        $"bridge #{br.Id} {br.Type} {OzBridgeKit.Biome(g.level)} {tiles} tiles{(lng ? " LONG" : "")}, crane from end {(e == 0 ? "A" : "B")}: order {(sent ? "taken" : "REFUSED")}, repaired {fixedOk} in {W.Time - t0:0.0} s, "
                        + $"deck {(d != null && d.ok ? $"{d.deckY:0.00} m, standing {d.stumpA:0.0} + {d.stumpB:0.0} m" : "not measured " + d?.why)}; frames on the deck {m.onDeck} (held at the edge {OzCraneDeck.HeldFrames - held0}), "
                        + $"over the gap {m.gap}, drawn ground vs deck top: standing {m.maxStand * 100f:0.0} cm, driving {m.maxDrive * 100f:0.0} cm at most{(m.worst.Length > 0 ? "; " + m.worst : "")}");
                    yield return new WaitForSeconds(OzBridgeRestore.Span + OzBridgeRestore.FadeAfter + 0.6f);
                }
            }
            g.gameSpeed = sp0; g.blueBot = bb; g.redBot = rb;
            sb.AppendLine(OzCraneDeck.Status()); sb.AppendLine(OzBuildingRestore.Status()); sb.AppendLine(OzBridgeRestore.Status());
            sb.AppendLine(f == 0 ? " COUNT PASS (factory_restore_crane_r3)" : $" COUNT FAIL: {f} line(s)");
            Directory.CreateDirectory(Out);
            string path = $"{Out}count_{g.level}_{System.DateTime.Now:HHmmss}.txt";
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"{Tag} count written: {path}\n{sb}");
            busy = false;
        }

        // ------------------------------------------------------------------ films (Play)
        sealed class Clean
        {
            readonly List<Behaviour> off = new List<Behaviour>(); readonly List<Canvas> canv = new List<Canvas>(); readonly List<AudioSource> music = new List<AudioSource>();
            readonly List<GameObject> washes = new List<GameObject>();
            bool bb, rb, run0, hud; float tod0; RtsCamera rts; DayNight dn; ZGame g; PlayerInput pin;
            public Clean(ZGame game)
            {
                g = game; bb = g.blueBot; rb = g.redBot; g.blueBot = false; g.redBot = false;
                rts = Object.FindFirstObjectByType<RtsCamera>(); if (rts) rts.enabled = false;
                dn = Object.FindFirstObjectByType<DayNight>(); if (dn) { run0 = dn.running; tod0 = dn.timeOfDay; dn.running = false; dn.timeOfDay = 0.1f; dn.Apply(0.1f); }
                hud = OpenZHud.I && OpenZHud.I.hudVisible;
                foreach (var b in Object.FindObjectsByType<SelectionOverlay>(FindObjectsSortMode.None)) if (b.enabled) { b.enabled = false; off.Add(b); }
                pin = Object.FindFirstObjectByType<PlayerInput>(); if (pin) { pin.Selection.Clear(); if (pin.enabled) { pin.enabled = false; off.Add(pin); } }
                foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (s && s.gameObject.name.StartsWith("Music") && !s.mute) { s.mute = true; music.Add(s); }
                Tick();
            }
            /// <summary>Every frame: the HUD's own hide, the screen canvases, the zone wash and flag rings (they come back by themselves).</summary>
            public void Tick()
            {
                if (OpenZHud.I && OpenZHud.I.hudVisible) OpenZHud.I.SetVisible(false);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c && c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; canv.Add(c); }
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) if (r && (r.gameObject.name == "ZoneWash" || r.gameObject.name == "FlagRing")) { r.gameObject.SetActive(false); washes.Add(r.gameObject); }
            }
            public void Undo()
            {
                g.blueBot = bb; g.redBot = rb; if (rts) rts.enabled = true;
                if (dn) { dn.running = run0; dn.timeOfDay = tod0; dn.Apply(tod0); }
                if (hud && OpenZHud.I) OpenZHud.I.SetVisible(true);
                foreach (var b in off) if (b) b.enabled = true;
                foreach (var c in canv) if (c) c.enabled = true;
                foreach (var s in music) if (s) s.mute = false;
                foreach (var w in washes) if (w) w.SetActive(true);
            }
        }

        /// <summary>The slow WIDE orbit with a slight push-in (whole event in frame, at least RTS distance).</summary>
        sealed class Orbit
        {
            readonly Camera cam; readonly Vector3 target; readonly float dist, pitch; float yaw; readonly float t0;
            public Orbit(Vector3 at, float distance, float pitchDeg, float yawDeg) { cam = Camera.main; target = at; dist = distance; pitch = pitchDeg; yaw = yawDeg; t0 = Time.time; Step(); }
            public void Step()
            {
                if (!cam) return;
                float t = Time.time - t0;
                float y = yaw + 0.6f * t, d = dist * (1f - 0.06f * Mathf.Clamp01(t / 70f));
                var dir = Quaternion.Euler(pitch, y, 0f) * Vector3.back;
                cam.transform.position = target + dir * d;
                cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
            }
        }

        static IEnumerator Wait(float seconds, Clean c, Orbit o, System.Func<bool> until = null)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seconds && (until == null || !until())) { c.Tick(); o.Step(); yield return null; }
        }

        static ZObject NearestTo(ZGame g, System.Func<ZObject, bool> ok)
        {
            var rts = Object.FindFirstObjectByType<RtsCamera>(); var at = rts ? rts.focus : Vector3.zero;
            ZObject best = null; float bd = float.MaxValue;
            foreach (var o in g.World.Objects) if (ok(o)) { var c = g.CenterWorld(o); float d = (c.x - at.x) * (c.x - at.x) + (c.z - at.z) * (c.z - at.z); if (d < bd) { bd = d; best = o; } }
            return best;
        }

        [MenuItem("OpenZ/V1.5/Crane Restore/2 Film: factory repair (Play, a level; record with OBS)")]
        static void FactoryMenu()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning(Tag + " enter Play on a level first"); return; }
            if (busy) return;
            ZGame.I.StartCoroutine(FactoryFilm(ZGame.I));
        }

        static IEnumerator FactoryFilm(ZGame g)
        {
            busy = true; var W = g.World; var log = new StringBuilder();
            var fac = NearestTo(g, o => (o.Type == "vehicle_factory" || o.Type == "robot_factory") && !o.Destroyed && o.Owner == g.player)
                      ?? NearestTo(g, o => (o.Type == "vehicle_factory" || o.Type == "robot_factory") && !o.Destroyed);
            if (fac == null) { Debug.LogWarning(Tag + " no standing factory on this level"); busy = false; yield break; }
            var c = new Clean(g); var orb = new Orbit(g.CenterWorld(fac) + Vector3.up * 2f, 62f, 50f, 200f);
            log.AppendLine($"factory film, level {g.level}: {fac.Type} #{fac.Id} {fac.Owner} at ({fac.LocX >> 4}, {fac.LocY >> 4})");
            Kill(W, fac);
            Debug.Log($"{Tag} FACTORY FILM: the factory is down; the crane rolls in after 9 s (start OBS now)");
            yield return Wait(9f, c, orb);
            fac.GetCraneEntrance(out int ex, out int ey, out _, out _);
            var crane = Spawn(g, "crane", (ex >> 4), (ey >> 4) + 9, g.player);
            yield return Wait(0.3f, c, orb);
            bool sent = Order(g, crane, WpMode.CraneRepair, fac);
            log.AppendLine($"crane #{crane?.Id} from ({(ex >> 4)}, {(ey >> 4) + 9}), order {(sent ? "taken" : "REFUSED")} at game time {W.Time:F1}");
            int runs0 = OzBuildingRestore.Runs;
            yield return Wait(150f, c, orb, () => !fac.Destroyed);
            log.AppendLine($"repaired {!fac.Destroyed} at game time {W.Time:F1}; restore runs {OzBuildingRestore.Runs - runs0}");
            yield return Wait(0.6f, c, orb);
            if (!fac.Destroyed && fac.Owner != Team.Neutral) log.AppendLine($"production started: {fac.SetBuildingDefaultProduction()}");   // the factory working again
            yield return Wait(8f, c, orb);
            c.Undo();
            log.AppendLine(OzBuildingRestore.Status());
            Write("factory_film", g, log);
            Debug.Log($"{Tag} FACTORY FILM DONE (stop OBS)\n{log}");
            busy = false;
        }

        [MenuItem("OpenZ/V1.5/Crane Restore/3 Film: long bridge destroyed + crane repair (Play, a level; record with OBS)")]
        static void BridgeMenu()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning(Tag + " enter Play on a level first"); return; }
            if (busy) return;
            ZGame.I.StartCoroutine(BridgeFilm(ZGame.I));
        }

        static IEnumerator BridgeFilm(ZGame g)
        {
            busy = true; var W = g.World; var log = new StringBuilder();
            var br = NearestTo(g, o => o.IsBridge && !o.Destroyed && Mathf.Max(o.WidthPix, o.HeightPix) / 16 >= OzBridgeKit.LongFromTiles)
                     ?? NearestTo(g, o => o.IsBridge && !o.Destroyed);
            if (br == null) { Debug.LogWarning(Tag + " no standing bridge on this level"); busy = false; yield break; }
            int tiles = Mathf.Max(br.WidthPix, br.HeightPix) / 16; float len = tiles * 2f;
            bool vert = br.Type == "bridge_vert";
            var c = new Clean(g); var orb = new Orbit(g.CenterWorld(br) + Vector3.up * 1f, Mathf.Max(85f, len * 3.4f), 52f, vert ? 250f : 160f);   // looking across the bridge
            log.AppendLine($"bridge film, level {g.level}: {br.Type} #{br.Id}, {tiles} tiles ({len:0} m)");
            var ends = Ends(br); var (ex, ey, ox, oy) = ends[0]; var (fx, fy, fox, foy) = ends[1];
            var tanks = new List<ZObject>();
            for (int i = 0; i < 3; i++)
            {
                int side = i - 1;
                var t = Spawn(g, OzHeavyTank.Type, (ex >> 4) + ox * 7 + (vert ? side * 3 : 0), (ey >> 4) + oy * 7 + (vert ? 0 : side * 3), g.player);
                if (t != null) tanks.Add(t);
            }
            Debug.Log($"{Tag} BRIDGE FILM: {tanks.Count} heavy tanks open fire in 3 s (start OBS now)");
            yield return Wait(3f, c, orb);
            int took = 0; foreach (var t in tanks) if (Order(g, t, WpMode.Attack, br)) took++;
            log.AppendLine($"{tanks.Count} heavy tanks, attack orders taken {took}, at game time {W.Time:F1}");
            yield return Wait(120f, c, orb, () => br.Destroyed);
            if (!br.Destroyed) { log.AppendLine("the tanks did not bring it down in 120 s: destroyed by the rules' own death now"); Kill(W, br); }
            log.AppendLine($"destroyed at game time {W.Time:F1} (automatic repair due at {br.NextAutoRepairTime:F1})");
            for (int i = 0; i < tanks.Count; i++) Order(g, tanks[i], WpMode.Move, null, ((ex >> 4) + ox * 10 + (vert ? (i - 1) * 3 : 0)) * 16, ((ey >> 4) + oy * 10 + (vert ? 0 : (i - 1) * 3)) * 16);   // the tanks make room
            yield return Wait(4f, c, orb);
            var crane = Spawn(g, "crane", (ex >> 4) + ox * 12, (ey >> 4) + oy * 12, g.player);
            yield return Wait(0.3f, c, orb);
            bool sent = Order(g, crane, WpMode.CraneRepair, br);
            log.AppendLine($"crane #{crane?.Id}, order {(sent ? "taken" : "REFUSED")} at game time {W.Time:F1}");
            var rv = g.GetComponent<RulesView>(); if (!rv) rv = Object.FindFirstObjectByType<RulesView>();
            var d = OzCraneDeck.Get(g, br); var models = rv ? rv.BridgeModels(br) : null; var tr = new Truth(models != null && models.Length > 2 ? models[2] : null);
            var m = new Meter(); int held0 = OzCraneDeck.HeldFrames; float t0 = Time.time;
            while (sent && br.Destroyed && Time.time - t0 < 150f && crane != null && !crane.Destroyed)
            {
                int h = OzCraneDeck.HeldFrames;
                c.Tick(); orb.Step(); yield return null;
                m.Sample(g, br, View(g, crane), d, tr, OzCraneDeck.HeldFrames > h);
            }
            tr.Done();
            log.AppendLine($"repaired {!br.Destroyed} at game time {W.Time:F1}; crane frames on the deck {m.onDeck} (held at the edge {OzCraneDeck.HeldFrames - held0}), over the gap {m.gap}, "
                           + $"drawn ground vs deck top standing {m.maxStand * 100f:0.0} cm, driving {m.maxDrive * 100f:0.0} cm{(m.worst.Length > 0 ? "; " + m.worst : "")}");
            yield return Wait(OzBridgeRestore.Span + OzBridgeRestore.FadeAfter + 1f, c, orb);
            if (tanks.Count > 0) { bool go = Order(g, tanks[0], WpMode.Move, null, ((fx >> 4) + fox * 4) * 16, ((fy >> 4) + foy * 4) * 16); log.AppendLine($"a heavy tank crosses: {go}"); }
            yield return Wait(14f, c, orb);
            c.Undo();
            log.AppendLine(OzCraneDeck.Status()); log.AppendLine(OzBridgeRestore.Status());
            Write("long_bridge_film", g, log);
            Debug.Log($"{Tag} BRIDGE FILM DONE (stop OBS)\n{log}");
            busy = false;
        }

        static void Write(string name, ZGame g, StringBuilder sb)
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText($"{Out}{name}_{g.level}_{System.DateTime.Now:HHmmss}.txt", sb.ToString());
        }
    }
}
#endif
