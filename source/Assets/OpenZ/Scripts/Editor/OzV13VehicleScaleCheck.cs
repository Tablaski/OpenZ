#if !OPENZ_HARNESS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of View/OzVehicleScaleTest.cs.
    /// Menu OpenZ/Checks/V1.3/Item 36 vehicle scale test:
    ///  * Edit mode (no Play): E1 .. E10 (the steps, who gets the tool, the key rule, the exact 100 %, the source, every vehicle prefab
    ///    scaled about its root, a headless L02 bot match at every step with the same rules checksum, the label's rule and place, and no
    ///    player text naming the tool).
    ///  * Play in a level or the Unit editor (the Game view in front): P1 .. P9 with real key events (the editor keys Ctrl+Alt+I / O).
    ///    It drops one of each vehicle model for red and blue near the view centre (the testing panel's drop path) and holds the game
    ///    (ZGame.paused) while it measures, so nothing fights; the pause is put back at the end, the dropped vehicles stay.
    /// Nothing is saved anywhere else.
    /// </summary>
    public static class OzV13VehicleScaleCheck
    {
        const string Tag = "[OpenZ V1.3 item36]";
        public static string Report = "not run";
        static string Dir => OzRelease.Milestones("v1_3_20261030/item36_vehicle_scale");

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzVehicleScaleCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Begin() { ok = 0; bad = 0; notes.Length = 0; }
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
        static void Info(string what) => notes.Append("\n  [info] ").Append(what);
        static void End(string part)
        {
            Report = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, part.Replace(' ', '_') + ".txt"), Report + "\n"); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
        /// <summary>A key press. With the editor in the background the keys do not reach the game: the same step goes in by the tool's own Press.</summary>
        static IEnumerator Tap(params Key[] k)
        {
            if (!Application.isFocused)
            {
                bool alt = Array.IndexOf(k, Key.LeftAlt) >= 0, i = Array.IndexOf(k, Key.I) >= 0;
                if (alt) OzVehicleScaleTest.Press(i ? -1 : 1);
                yield return Frames(5); yield break;
            }
            Keys(k); yield return Frames(2); Keys(); yield return Frames(3);
        }
        static IEnumerator AfterNote() { yield return new WaitForSecondsRealtime(OzVehicleScaleTest.NoteSeconds + 0.2f); yield return Frames(2); }

        [MenuItem("OpenZ/Checks/V1.3/Item 36 vehicle scale test (Edit: steps, source, rules; Play: every vehicle and driver)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { EditCheck(); return; }
            if (Keyboard.current == null) { Debug.LogError(Tag + " no keyboard device"); return; }
            if (!ZGame.I || ZGame.I.World == null) { Debug.LogError(Tag + " start a level (or the Unit editor) first"); return; }
            var gv = Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null && Application.isFocused) EditorWindow.FocusWindowIfItsOpen(gv);   // never takes the window focus from another program
            Report = "running"; R().StartCoroutine(PlayRoutine());
        }

        // ------------------------------------------------------------------ edit mode
        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        static int Count(string s, string what) { int n = 0, i = 0; while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }
        static bool Bits(Vector3 a, Vector3 b) => BitConverter.SingleToInt32Bits(a.x) == BitConverter.SingleToInt32Bits(b.x)
            && BitConverter.SingleToInt32Bits(a.y) == BitConverter.SingleToInt32Bits(b.y) && BitConverter.SingleToInt32Bits(a.z) == BitConverter.SingleToInt32Bits(b.z);

        static readonly string[] Prefabs =
        {
            "Assets/OpenZ/Prefabs/Z_jeep.prefab", "Assets/OpenZ/Prefabs/Z_light.prefab", "Assets/OpenZ/Resources/OpenZ/LightV2/Z_light_v2.prefab",
            "Assets/OpenZ/Prefabs/Z_medium.prefab", "Assets/OpenZ/Resources/OpenZ/MechV2/Z_medium_v2.prefab", "Assets/OpenZ/Resources/OpenZ/MTank/Z_mtank.prefab",
            "Assets/OpenZ/Prefabs/Z_heavy.prefab", "Assets/OpenZ/Resources/OpenZ/HeavyV2/Z_heavy_v2.prefab",
            "Assets/OpenZ/Prefabs/Z_missile_launcher.prefab", "Assets/OpenZ/Resources/OpenZ/MlrsV2/Z_missile_launcher_v2.prefab",
            "Assets/OpenZ/Prefabs/Z_apc.prefab", "Assets/OpenZ/Prefabs/Z_crane.prefab",
        };

        static void EditCheck()
        {
            Begin();
            var S = OzVehicleScaleTest.Steps;
            // E1 the steps
            int st = 0; var down = new List<int>(); var up = new List<int>();
            for (int i = 0; i < 9; i++) { st = OzVehicleScaleTest.StepAfter(st, -1); down.Add(Mathf.RoundToInt(S[st] * 100f)); }
            for (int i = 0; i < 9; i++) { st = OzVehicleScaleTest.StepAfter(st, 1); up.Add(Mathf.RoundToInt(S[st] * 100f)); }
            Check(S.Length == 9 && S[0] == 1f && Mathf.RoundToInt(S[8] * 100f) == 60 && Mathf.RoundToInt(S[2] * 100f) == OzVehicleSizeOption.DefaultPercent, "E1a the steps are 100 to 60 % in fives, the default 90 %");
            Check(string.Join(",", down) == "95,90,85,80,75,70,65,60,60" && string.Join(",", up) == "65,70,75,80,85,90,95,100,100",
                  $"E1b nine steps down: {string.Join(" ", down)}; nine back up: {string.Join(" ", up)} (held at 60 % and at 100 %)");
            // E2 who gets the tool
            string tool0 = Src("View/OzVehicleScaleTest.cs");
            Check(tool0.Length > 0 && Count(tool0, "DevArg") == 0 && Count(tool0, "DevCmd") == 0 && Count(tool0, "IsRelease") == 0 && Count(tool0, "OzRelease") == 0
                  && Count(tool0, "OZ_RELEASE") == 0 && Count(tool0, "ozdev") == 0 && Count(tool0, "#if") == 0 && Count(tool0, "if (I) return;") == 1,
                  "E2 every game gets it, every exe too, with no start flag: the tool has no dev flag, no release switch and no #if");
            // E3 the key rule
            bool h;
            bool exe = OzVehicleScaleTest.KeyStep(true, false, true, false, false, out h) == -1 && OzVehicleScaleTest.KeyStep(true, false, false, true, false, out h) == 1
                    && OzVehicleScaleTest.KeyStep(true, true, true, false, false, out h) == -1 && OzVehicleScaleTest.KeyStep(true, true, false, true, false, out h) == 1
                    && OzVehicleScaleTest.KeyStep(false, false, true, false, false, out h) == 0 && OzVehicleScaleTest.KeyStep(false, true, false, true, false, out h) == 0
                    && OzVehicleScaleTest.KeyStep(true, false, true, true, false, out h) == 0;
            bool hintPlain = OzVehicleScaleTest.KeyStep(true, false, true, false, true, out bool h1) == 0 && h1;
            bool ed = hintPlain && OzVehicleScaleTest.KeyStep(true, true, true, false, true, out h) == -1 && !h && OzVehicleScaleTest.KeyStep(true, true, false, true, true, out h) == 1
                    && OzVehicleScaleTest.KeyStep(false, true, true, false, true, out h) == 0 && !h;
            Check(exe, "E3a exe: Ctrl+I / Ctrl+Alt+I one step smaller, Ctrl+O / Ctrl+Alt+O one step bigger; I, O, Alt+O alone and I+O together do nothing");
            Check(ed, "E3b editor: Ctrl+Alt+I / Ctrl+Alt+O step; a plain Ctrl+I / O only shows the hint (Unity owns them: Invert Selection, Open Scene)");
            // E4 100 % exactly
            var v = new Vector3(1.0000001f, 0.62f, 1.3f);
            Check(Bits(OzVehicleScaleTest.Want(v, 1f), v) && OzVehicleScaleTest.Want(v, 0.85f) == v * 0.85f, "E4 at 100 % a root gets back its own size bit for bit");
            // E11 (2 Nov) the jeep's share
            Check(OzVehicleScaleTest.JeepShare == 0.92f && OzVehicleScaleTest.Share("jeep") == 0.92f && OzVehicleScaleTest.Share("light") == 1f && OzVehicleScaleTest.Share("heavy") == 1f
                  && OzVehicleScaleTest.Share("apc") == 1f && OzVehicleScaleTest.Share(ZWorld.ShuttleType) == 1f && OzVehicleScaleTest.Share(null) == 1f,
                  "E11 the jeep is drawn at 92 % of the vehicle size (owner 2 Nov), every other vehicle at the size itself");
            // E5 the source
            string tool = Src("View/OzVehicleScaleTest.cs"), k386 = Src("View/BoardingView.K386.cs"), vm = Src("View/VehicleMotion.cs"), uv = Src("View/UnitView.cs");
            Check(tool.Length > 0 && Count(tool, "PlayerPrefs") == 0 && Count(tool, "EditorPrefs") == 0 && Count(tool, "File.") == 0 && Count(tool, ".position =") == 0
                  && Count(tool, "RuntimeInitializeLoadType.AfterSceneLoad") == 1 && Count(tool, "Debug.Log") == 1,
                  "E5a the size is saved only through the option, the tool writes no position, and its one log line is a key press (none at the start of a game)");
            int simRefs = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts/Sim"), "*.cs", SearchOption.AllDirectories))
                if (File.ReadAllText(f).Contains("OzVehicleScaleTest")) simRefs++;
            Check(simRefs == 0, $"E5b no rules file (Scripts/Sim) reads the tool ({simRefs})");
            var readers = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
                if (f.Replace('\\', '/').IndexOf("/Editor/", StringComparison.Ordinal) < 0 && File.ReadAllText(f).Contains("OzVehicleScaleTest")) readers.Add(Path.GetFileName(f));
            readers.Sort(StringComparer.Ordinal);
            Check(string.Join(",", readers) == "BoardingView.K386.cs,OzVehicleScaleTest.cs,OzVehicleSizeOption.cs,UnitView.cs,VehicleMotion.cs",
                  $"E5g only the tool, its option and its three view hooks name it in the game's code: {string.Join(", ", readers)}");
            Check(Count(k386, "float k = OzVehicleScaleTest.Of(r.view), s = r.view.ExitScaleNow;") == 1 && Count(k386, "Mathf.Clamp(k != 1f ? s * k : s, 0.05f, 1f)") == 1,
                  "E5c the crews follow the drawn hull (BoardingView.HullGrow x the test; the old value when it is 1)");
            Check(Count(vm, "float drawn = OzVehicleScaleTest.Of(this); travel += drawn != 1f ? speed * dt / drawn : speed * dt;") == 1,
                  "E5d wheels and belts roll with the drawn size (VehicleMotion; the old sum when it is 1)");
            Check(Count(uv, "float k = OzVehicleScaleTest.Of(this), h = ") == 1 && Count(uv, "1.0f * OzVehicleScaleTest.Of(this)") == 1,
                  "E5e the fallback muzzle and the aim point stay on the drawn hull (UnitView.MuzzlePos / AimPos)");
            bool keysFree = true;
            foreach (var d in OzBindings.Defs) if (d.key == Key.I || d.key == Key.O) keysFree = false;
            Check(keysFree, "E5f no action has I or O as its default key");
            // E6 every vehicle prefab scaled about its root, in a preview scene (the project is not touched)
            var scene = EditorSceneManager.NewPreviewScene(); int found = 0, good = 0; float worstGap = 0f; var missing = new List<string>();
            try
            {
                foreach (var path in Prefabs)
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!pf) { missing.Add(Path.GetFileNameWithoutExtension(path)); continue; }
                    found++;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene); var t = go.transform;
                    t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var b0 = t.localScale; float lossy0 = Mathf.Abs(t.lossyScale.x), low0 = Lowest(go); bool pass = true;
                    for (int i = 1; i < S.Length; i++)
                    {
                        t.localScale = OzVehicleScaleTest.Want(b0, S[i]);
                        float ratio = Mathf.Abs(t.lossyScale.x) / Mathf.Max(1e-6f, lossy0), low = Lowest(go);
                        float gapErr = Mathf.Abs(low - low0 * S[i]); worstGap = Mathf.Max(worstGap, Mathf.Abs(low - low0));
                        if (Mathf.Abs(ratio - S[i]) > 1e-5f || gapErr > 1e-3f || t.position != Vector3.zero) pass = false;
                    }
                    t.localScale = OzVehicleScaleTest.Want(b0, 1f);
                    if (!Bits(t.localScale, b0)) pass = false;
                    if (pass) good++;
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Check(found == Prefabs.Length && good == found,
                  $"E6 every vehicle model ({good}/{found} of {Prefabs.Length}{(missing.Count > 0 ? "; missing " + string.Join(", ", missing) : "")}) scales about its root: size x k, root in place, lowest point x k, 100 % bit for bit; the lowest point moves at most {worstGap * 100f:0.0} cm at 60 %");
            // E7 the rules: a headless L02 bot match at every step
            var runs = new List<string>(); string first = null; bool same = true;
            int keep = OzVehicleScaleTest.Step;
            try
            {
                foreach (int s in new[] { 0, 2, 4, 6, 8, 0 })
                {
                    OzVehicleScaleTest.SetStepForCheck(s);
                    var marks = string.Join(" ", Match());
                    runs.Add($"{Mathf.RoundToInt(S[s] * 100f)} %: {marks}");
                    if (first == null) first = marks; else if (marks != first) same = false;
                }
            }
            finally { OzVehicleScaleTest.SetStepForCheck(keep); }
            Check(same, "E7 headless L02 bot match (seed 1, 30 s, 100 Hz), the rules checksum at 10 / 20 / 30 s is the same at every step:\n      " + string.Join("\n      ", runs));
            // E8 the label's rule
            Check(OzVehicleScaleTest.LabelSize(false) == 0 && OzVehicleScaleTest.LabelSize(true) == 2, "E8a 2 s after a key the note, then nothing at any size");
            Check(OzVehicleScaleTest.LabelText(85) == "VEHICLE SCALE 85 %" && OzVehicleScaleTest.HintText == "VEHICLE SCALE: CTRL+ALT+I SMALLER, CTRL+ALT+O BIGGER"
                  && Count(tool, "IN THE EDITOR") == 0 && Count(tool, "(test") == 0, $"E8b the texts: \"{OzVehicleScaleTest.LabelText(85)}\" and the hint \"{OzVehicleScaleTest.HintText}\"");
            // E10 no player text
            var named = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string n = f.Replace('\\', '/');
                if (n.EndsWith("/View/OzVehicleScaleTest.cs", StringComparison.Ordinal) || (n.IndexOf("/Editor/", StringComparison.Ordinal) >= 0 && !n.EndsWith("/OpenZReleaseBuild.cs", StringComparison.Ordinal))) continue;
                string t = File.ReadAllText(f);
                foreach (var word in new[] { "VEHICLE SCALE", "CTRL+I", "CTRL+O", "CTRL+ALT+I", "CTRL+ALT+O" }) if (t.Contains(word)) { named.Add(Path.GetFileName(f) + ": " + word); break; }
            }
            Check(named.Count == 0, $"E10 no settings, help, key list, tooltip, changes screen or readme text names the tool or its keys ({string.Join(", ", named)})");
            End("edit");
        }

        static float Lowest(GameObject go)
        {
            float low = float.MaxValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                low = Mathf.Min(low, r.bounds.min.y);
            }
            return low == float.MaxValue ? 0f : low;
        }

        const int MatchTicks = 3000;
        static string[] Match()
        {
            var w = ZWorld.Create(Resources.Load<TextAsset>("OpenZ/default_settings").text, Resources.Load<TextAsset>("OpenZ/Maps/L02_grid").text, 1, ZGame.ParkedStartOn("L02"));
            w.Bots.Add(new ZBot(w, Team.Red, 1)); w.Bots.Add(new ZBot(w, Team.Blue, 102));
            var marks = new List<string>();
            for (int t = 1; t <= MatchTicks; t++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (t % 1000 == 0) marks.Add(Rules(w).ToString("x16")); }
            return marks.ToArray();
        }

        /// <summary>The rules state: every object's id, owner, kind, type, place (with the sub-pixel carry), health, crew, orders; the time.</summary>
        public static ulong Rules(ZWorld w)
        {
            ulong h = 1469598103934665603UL;
            void Mix(long x) { h ^= (ulong)x; h *= 1099511628211UL; h ^= h >> 31; }
            Mix(BitConverter.DoubleToInt64Bits(w.Time)); Mix(w.Objects.Count);
            foreach (var o in w.Objects)
            {
                Mix(o.Id); Mix((long)o.Owner); Mix((long)o.Kind); Mix(Fnv(o.Type)); Mix(o.LocX); Mix(o.LocY);
                Mix(BitConverter.DoubleToInt64Bits(o.XOver)); Mix(BitConverter.DoubleToInt64Bits(o.YOver));
                Mix(o.Health); Mix(o.MaxHealth); Mix(o.Drivers.Count); Mix(o.Waypoints.Count);
                foreach (var p in o.Waypoints) { Mix((long)p.mode); Mix(p.refId); Mix(p.x); Mix(p.y); }
            }
            return h;
        }

        static long Fnv(string s) { ulong h = 14695981039346656037UL; if (s != null) foreach (char ch in s) { h ^= ch; h *= 1099511628211UL; } return (long)h; }

        // ------------------------------------------------------------------ Play
        static readonly string[] Tiles = { "jeep", "light@v1", "light@v2", "medium@v1", "medium@v2", "mtank", "heavy@v1", "heavy@v2",
                                           "missile_launcher@v1", "missile_launcher@v2", "apc", "crane" };

        sealed class Snap
        {
            public UnitView v; public Vector3 scale; public float lossy, gap, muzzle, aimUp, rect; public string rules;
            public bool crew; public float crewSize, crewWant; public Vector3 hips;
        }

        static float Gap(ZGame g, UnitView v) => Lowest(v.gameObject) - g.GroundY(v.transform.position.x, v.transform.position.z);
        static string RulesOf(ZObject o) => $"{o.WidthPix},{o.HeightPix},{o.MoveSpeed},{o.AttackRadius},{o.DamageRadius},{o.MaxHealth},{o.Health},{o.LocX},{o.LocY}";
        static float RectH(ZObject o) { var hud = OpenZHud.I; return hud && hud.input && hud.input.MeshRect(o, out var r) ? r.height : -1f; }

        static Snap Take(ZGame g, UnitView v)
        {
            var t = v.transform; float ls = Mathf.Max(1e-6f, Mathf.Abs(t.lossyScale.x));
            var s = new Snap { v = v, scale = t.localScale, lossy = ls, gap = Gap(g, v), muzzle = (v.MuzzlePos() - t.position).magnitude / ls,
                               aimUp = v.AimPos().y - t.position.y, rect = RectH(v.Obj), rules = RulesOf(v.Obj) };
            if (BoardingView.K386Sample(v, out var c) && c.shown) { s.crew = true; s.crewSize = c.size; s.crewWant = c.wantSize; s.hips = c.hipsInHull; }
            return s;
        }

        static IEnumerator PlayRoutine()
        {
            Begin();
            var g = ZGame.I; bool wasPaused = g.paused; int sizeAtStart = OzVehicleScaleTest.Percent;
            if (!OzVehicleScaleTest.I) { Check(false, "P1 the tool is running in the editor (OzVehicleScaleTest.I)"); End("play"); yield break; }
            Check(true, "P1 the tool is running in the editor (OzVehicleScaleTest.I)");
            while (OzVehicleScaleTest.Step != 0) yield return Tap(Key.LeftCtrl, Key.LeftAlt, Key.O);   // start from 100 %
            yield return AfterNote();
            int w0 = OzVehicleScaleTest.Writes - OzVehicleScaleTest.ShareWrites; yield return Frames(30);
            Check(OzVehicleScaleTest.Writes - OzVehicleScaleTest.ShareWrites == w0 && !OzVehicleScaleTest.AnyStepApplied && OzVehicleScaleTest.ShownSize == 0 && !OzVehicleScaleTest.DrawerOn,
                  "P2 at 100 % the tool writes nothing but a jeep's 92 % (no other vehicle transform touched in 30 frames), shows nothing and its drawer is off");
            // one of each vehicle model for red and blue near the view centre, the game held (put back in the finally block)
            g.paused = true;
            try
            {
                var cam = Camera.main; Vector3 c0 = Vector3.zero;
                if (cam) { var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); if (Mathf.Abs(ray.direction.y) > 1e-3f) c0 = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); }
                int cx = Mathf.RoundToInt(c0.x * 8f) >> 4, cy = Mathf.RoundToInt(-c0.z * 8f) >> 4, dropped = 0;
                for (int i = 0; i < Tiles.Length; i++)
                {
                    if (OzVehicleScaleTest.CheckSpawn(g, Tiles[i], Team.Red, cx - 8 + (i % 6) * 3, cy - 6 + (i / 6) * 3) != null) dropped++;
                    if (OzVehicleScaleTest.CheckSpawn(g, Tiles[i], Team.Blue, cx - 8 + (i % 6) * 3, cy + 34 + (i / 6) * 3) != null) dropped++;
                }
                Info($"dropped {dropped} of {Tiles.Length * 2} vehicles (one of each model for red and blue) near tile {cx},{cy}; the game is held while it measures");
                yield return Frames(40);
                var list = new List<Snap>(); var types = new SortedDictionary<string, int>(); UnitView shuttle = null; Vector3 shuttleScale = default;
                foreach (var kv in g.Views)
                {
                    var v = kv.Value; if (!v || v.Obj == null || !v.gameObject.activeInHierarchy) continue;
                    if (v.Obj.IsShuttle) { shuttle = v; shuttleScale = v.transform.localScale; continue; }
                    if (!OzVehicleScaleTest.Includes(v) || v.Dying || v.ExitScaleNow != 1f) continue;   // one still growing out of a building has no size to compare yet
                    list.Add(Take(g, v)); string key = v.Obj.Type + " " + (v.Obj.Owner == Team.Red ? "red" : v.Obj.Owner == Team.Blue ? "blue" : "neutral");
                    types[key] = types.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                int crews = 0; foreach (var s in list) if (s.crew) crews++;
                Check(list.Count >= 12, $"P3 vehicles on the field: {list.Count} ({string.Join(", ", Pairs(types))}); drivers drawn: {crews}");
                { var jp = g.Prefab("Z_jeep"); float j0 = jp ? Mathf.Abs(jp.transform.lossyScale.x) : 1f; int jn = 0, jok = 0;
                  foreach (var s in list) if (s.v && s.v.Obj.Type == "jeep") { jn++; if (Mathf.Abs(s.lossy / j0 - OzVehicleScaleTest.JeepShare) <= 1e-4f && Mathf.Abs(OzVehicleScaleTest.Of(s.v) - OzVehicleScaleTest.JeepShare) <= 1e-6f) jok++; }
                  Check(jn > 0 && jok == jn, $"P3b at 100 % the jeeps are drawn at 92 % of their model ({jok}/{jn}), their readers get 0.92"); }
                string world0 = OzV13VehicleScaleCheck.Rules(g.World).ToString("x16");
                for (int i = 1; i < OzVehicleScaleTest.Steps.Length; i++)
                {
                    int clicks = OzVehicleScaleTest.Clicks;
                    yield return Tap(Key.LeftCtrl, Key.LeftAlt, Key.I);
                    float k = OzVehicleScaleTest.Steps[i]; int pct = Mathf.RoundToInt(k * 100f);
                    Check(OzVehicleScaleTest.Step == i && OzVehicleScaleTest.LastNote == $"VEHICLE SCALE {pct} %" && OzVehicleScaleTest.Clicks == clicks + 1 && OzVehicleScaleTest.ShownSize == 2 && OzVehicleScaleTest.DrawerOn,
                          $"P4.{i}a Ctrl+Alt+I -> step {OzVehicleScaleTest.Step}, big note \"{OzVehicleScaleTest.LastNote}\", click sound");
                    Measure(g, list, k, $"P4.{i}");
                    Check(OzV13VehicleScaleCheck.Rules(g.World).ToString("x16") == world0, $"P4.{i}g the rules state checksum is the one at 100 % ({world0})");
                }
                yield return Tap(Key.LeftCtrl, Key.LeftAlt, Key.I);
                Check(OzVehicleScaleTest.Step == 8 && OzVehicleScaleTest.LastNote == "VEHICLE SCALE 60 %", "P5 one more Ctrl+Alt+I stays at 60 %");
                yield return AfterNote();
                Check(OzVehicleScaleTest.ShownSize == 0 && OzVehicleScaleTest.ShownText == "" && !OzVehicleScaleTest.DrawerOn, "P5b 2 s later nothing stays on screen at 60 %, the drawer is off");
                yield return Tap(Key.LeftCtrl, Key.I);
                if (Application.isFocused) Check(OzVehicleScaleTest.Step == 8 && OzVehicleScaleTest.LastNote == OzVehicleScaleTest.HintText, "P6 a plain Ctrl+I in the editor changes nothing and shows the hint");
                for (int i = 0; i < 8; i++) yield return Tap(Key.LeftCtrl, Key.LeftAlt, Key.O);
                yield return Frames(3);
                int exact = 0, crewExact = 0, crewN = 0;
                foreach (var s in list)
                {
                    if (!s.v) continue;
                    if (Bits(s.v.transform.localScale, s.scale)) exact++;
                    if (s.crew && BoardingView.K386Sample(s.v, out var c) && c.shown) { crewN++; if (c.size == s.crewSize) crewExact++; }
                }
                Check(OzVehicleScaleTest.Step == 0 && exact == list.Count && crewExact == crewN && !OzVehicleScaleTest.AnyStepApplied,
                      $"P7 eight Ctrl+Alt+O back to 100 %: {exact}/{list.Count} vehicles have their old size bit for bit, {crewExact}/{crewN} drivers too");
                Check(OzVehicleScaleTest.ShownSize == 2 && OzVehicleScaleTest.ShownText == "VEHICLE SCALE 100 %", "P7b back at 100 % the note shows VEHICLE SCALE 100 %");
                int w1 = OzVehicleScaleTest.Writes - OzVehicleScaleTest.ShareWrites; yield return Frames(30);
                yield return Tap(Key.LeftCtrl, Key.LeftAlt, Key.O);
                Check(OzVehicleScaleTest.Writes - OzVehicleScaleTest.ShareWrites == w1 && OzVehicleScaleTest.Step == 0 && OzVehicleScaleTest.LastNote == "VEHICLE SCALE 100 %",
                      "P8 at 100 % again nothing is written but a jeep's 92 % (30 frames), one more Ctrl+Alt+O stays at 100 %");
                yield return AfterNote();
                Check(OzVehicleScaleTest.ShownSize == 0 && OzVehicleScaleTest.ShownText == "" && !OzVehicleScaleTest.DrawerOn, "P8b 2 s later nothing is on screen and the drawer is off");
                if (shuttle) Check(Bits(shuttle.transform.localScale, shuttleScale), "P9 the shuttle kept its size at every step");
            }
            finally { g.paused = wasPaused; OzVehicleScaleTest.SetStepForCheck(OzVehicleScaleTest.StepOf(sizeAtStart)); OzVehicleSizeOption.Save(sizeAtStart); }
            End("play");
        }

        static IEnumerable<string> Pairs(SortedDictionary<string, int> d) { foreach (var kv in d) yield return kv.Key + " x" + kv.Value; }

        static void Measure(ZGame g, List<Snap> list, float k, string id)
        {
            int n = 0, size = 0, ground = 0, muzzle = 0, aim = 0, rect = 0, rectN = 0, rules = 0, crewN = 0, crewOk = 0; float worstGap = 0f;
            foreach (var s in list)
            {
                if (!s.v || s.v.Dying) continue;
                n++; var t = s.v.transform; float ls = Mathf.Abs(t.lossyScale.x);
                if (Mathf.Abs(ls / s.lossy - k) <= 1e-4f) size++; else if (k < 0.91f && k > 0.89f) Info($"{id} off: {s.v.name} {ls:0.000} / {s.lossy:0.000} local {t.localScale.x:0.000} was {s.scale.x:0.000}");
                float gap = Gap(g, s.v), want = s.gap * k, gapOk = 0.03f + 0.25f * (1f - k); worstGap = Mathf.Max(worstGap, Mathf.Abs(gap - want));   // the wheels and feet stand on the ground at every size: the allowance grows with the step
                if (Mathf.Abs(gap - want) <= gapOk) ground++;
                if (Mathf.Abs((s.v.MuzzlePos() - t.position).magnitude / Mathf.Max(1e-6f, ls) - s.muzzle) <= 0.03f * Mathf.Max(1f, s.muzzle)) muzzle++; else if (k < 0.91f && k > 0.89f) Info($"{id} muzzle: {s.v.name} {(s.v.MuzzlePos() - t.position).magnitude / Mathf.Max(1e-6f, ls):0.00} / {s.muzzle:0.00}");
                if (Mathf.Abs((s.v.AimPos().y - t.position.y) - s.aimUp * k) <= 1e-4f) aim++;
                float rh = RectH(s.v.Obj); if (s.rect > 0f && rh > 0f) { rectN++; if (Mathf.Abs(rh / s.rect - k) <= 0.1f) rect++; }
                if (RulesOf(s.v.Obj) == s.rules) rules++;
                if (s.crew && BoardingView.K386Sample(s.v, out var c) && c.shown)
                {
                    crewN++;
                    bool sized = Mathf.Abs(c.wantSize / Mathf.Max(1e-6f, s.crewWant) - k) <= 1e-3f && Mathf.Abs(c.size / Mathf.Max(1e-6f, c.wantSize) - 1f) <= 0.02f;
                    bool seated = (c.hipsInHull - s.hips).magnitude <= 0.05f;
                    if (sized && seated) crewOk++;
                }
            }
            Check(size == n && n > 0, $"{id}b {size}/{n} vehicles drawn at {k * 100f:0} % of their own size");
            Check(ground == n, $"{id}c {ground}/{n} stay on the ground (lowest point = its 100 % gap x k; worst {worstGap * 100f:0.0} cm)");
            Check(muzzle == n && aim == n, $"{id}d shot start and aim point follow the drawn hull: muzzles {muzzle}/{n}, aim points {aim}/{n}");
            Check(crewOk == crewN, $"{id}e {crewOk}/{crewN} drawn drivers at {k * 100f:0} % of their size and in their seat (hips in the hull's own space within 5 cm)");
            Check(rect == rectN, $"{id}f pick box / brackets / health bar place follow the drawn mesh: {rect}/{rectN} within 10 % of k");
            Check(rules == n, $"{id}h the rules of every vehicle unchanged (footprint, speed, ranges, health, place): {rules}/{n}");
        }
    }
}
#endif
