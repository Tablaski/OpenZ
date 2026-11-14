// OpenZ (GPL-3.0) - version 1.4: every level's own shader warm list, made from the shared list and what the level uses, with no hand work.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The level warm lists that View/OzLevelWarm.cs warms on a level's loading screen: Resources/OpenZ/Shaders/Levels/&lt;id&gt;_warm.
    /// For each level of the menu (OzLevelDef.All, level 7 too before it is registered) the list is the shared list
    /// (Resources/OpenZ/Shaders/OzWarmVariants, Editor/OzShaderWarmup.cs) without the shaders that only OTHER scenes use: a shader the level's
    /// scene uses, one that any Resources asset uses (the game loads those on any level: units, effects, lava), an always-included one and the
    /// unit shaders all stay. So a list never misses what its level can draw, and a level drops what only the other maps, the menu or the
    /// Unit editor draw. Made by the menu OpenZ/Shaders/Level warm lists - make now, and by itself before every player build
    /// (<see cref="OzLevelWarmListsBuild"/>): a new map registered in OzLevelDef.All gets its list at the next build. A list is written only
    /// when its variants changed. Report: Logs/oz_level_warm.txt.
    /// </summary>
    public static class OzLevelWarmLists
    {
        public const string Dir = "Assets/OpenZ/Resources/OpenZ/Shaders/Levels";
        public const string ReportPath = "Logs/oz_level_warm.txt";
        public static string LastLine { get; private set; } = "";
        public static string PathOf(string id) => Dir + "/" + id + OzLevelWarm.Suffix + ".shadervariants";

        /// <summary>The menu's levels: id and scene name, in menu order.</summary>
        public static List<KeyValuePair<string, string>> Levels()
        {
            var l = new List<KeyValuePair<string, string>>();
            foreach (var d in OzLevelDef.All) l.Add(new KeyValuePair<string, string>(d.Id, d.Scene));
            if (!OzLevelL07.Registered) l.Add(new KeyValuePair<string, string>(OzLevelL07.Id, OzLevelL07.Scene));
            return l;
        }

        /// <summary>The scene's asset path: from the build settings, else found by name (null = none).</summary>
        public static string ScenePath(string scene)
        {
            foreach (var s in EditorBuildSettings.scenes) if (Path.GetFileNameWithoutExtension(s.path) == scene) return s.path;
            foreach (var g in AssetDatabase.FindAssets("t:Scene " + scene))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == scene) return p;
            }
            return null;
        }

        static readonly Regex ShaderRef = new Regex(@"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})");

        /// <summary>The shaders of these assets and of everything they depend on: .shader and .shadergraph files, and the shader each
        /// material names (read from the material's text, so no texture is loaded).</summary>
        public static HashSet<Shader> ShadersOf(IEnumerable<string> roots)
        {
            var set = new HashSet<Shader>();
            var r = roots.Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (r.Length == 0) return set;
            foreach (var p in AssetDatabase.GetDependencies(r, true))
            {
                if (p.EndsWith(".shadervariants", StringComparison.OrdinalIgnoreCase)) continue;
                if (p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                {
                    var s = AssetDatabase.LoadAssetAtPath<Shader>(p); if (s) set.Add(s);
                }
                else if (p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    string t;
                    try { t = File.ReadAllText(Path.GetFullPath(p)); } catch (Exception) { continue; }
                    var m = ShaderRef.Match(t);
                    if (!m.Success) continue;
                    var sp = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                    if (string.IsNullOrEmpty(sp)) continue;
                    var s = AssetDatabase.LoadAssetAtPath<Shader>(sp); if (s) set.Add(s);
                }
            }
            return set;
        }

        /// <summary>What any level may draw: every Resources asset's shaders, the always-included shaders and the unit shaders.</summary>
        public static HashSet<Shader> SharedShaders()
        {
            var res = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)
                                                                && !p.EndsWith(".shadervariants", StringComparison.OrdinalIgnoreCase));
            var set = ShadersOf(res);
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs != null && gs.Length > 0 && gs[0])
            {
                var arr = new SerializedObject(gs[0]).FindProperty("m_AlwaysIncludedShaders");
                if (arr != null && arr.isArray)
                    for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue is Shader s && s) set.Add(s);
            }
            foreach (var n in OzShaderWarmup.UnitShaders) { var s = Shader.Find(n); if (s) set.Add(s); }
            return set;
        }

        /// <summary>The entries of a collection: shader, pass type, keywords.</summary>
        public static List<(Shader s, PassType t, string[] kw)> Entries(ShaderVariantCollection c)
        {
            var o = new List<(Shader, PassType, string[])>();
            if (!c) return o;
            var arr = new SerializedObject(c).FindProperty("m_Shaders");
            if (arr == null || !arr.isArray) return o;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var s = e.FindPropertyRelative("first")?.objectReferenceValue as Shader;
                var vs = e.FindPropertyRelative("second.variants");
                if (!s || vs == null || !vs.isArray) continue;
                for (int j = 0; j < vs.arraySize; j++)
                {
                    var v = vs.GetArrayElementAtIndex(j);
                    var kw = (v.FindPropertyRelative("keywords")?.stringValue ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    o.Add((s, (PassType)(v.FindPropertyRelative("passType")?.intValue ?? 0), kw));
                }
            }
            return o;
        }

        static string Key((Shader s, PassType t, string[] kw) e) => e.s.name + "|" + (int)e.t + "|" + string.Join(" ", e.kw.OrderBy(k => k, StringComparer.Ordinal));

        /// <summary>One level's plan: its scene's shaders, the shaders only other scenes use (left out), and the entries of its list.</summary>
        public sealed class Plan
        {
            public string Id, Scene, ScenePath;
            public HashSet<Shader> SceneShaders = new HashSet<Shader>(), OnlyOthers = new HashSet<Shader>();
            public List<(Shader s, PassType t, string[] kw)> Entries = new List<(Shader, PassType, string[])>();
        }

        /// <summary>The plans of every level (no file written).</summary>
        public static List<Plan> Plans(out string why)
        {
            why = null;
            var shared = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(OzShaderWarmup.ListPath);
            if (!shared) { why = "no shared list " + OzShaderWarmup.ListPath; return new List<Plan>(); }
            var all = Entries(shared);
            var common = SharedShaders();
            var sceneShaders = new Dictionary<string, HashSet<Shader>>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled && !sceneShaders.ContainsKey(s.path)) sceneShaders[s.path] = ShadersOf(new[] { s.path });
            var plans = new List<Plan>();
            foreach (var kv in Levels())
            {
                var p = new Plan { Id = kv.Key, Scene = kv.Value, ScenePath = ScenePath(kv.Value) };
                if (p.ScenePath != null)
                {
                    if (!sceneShaders.TryGetValue(p.ScenePath, out var mine)) sceneShaders[p.ScenePath] = mine = ShadersOf(new[] { p.ScenePath });
                    p.SceneShaders = mine;
                }
                foreach (var o in sceneShaders) if (o.Key != p.ScenePath) foreach (var s in o.Value) if (!p.SceneShaders.Contains(s) && !common.Contains(s)) p.OnlyOthers.Add(s);
                foreach (var e in all) if (!p.OnlyOthers.Contains(e.s)) p.Entries.Add(e);
                plans.Add(p);
            }
            return plans;
        }

        /// <summary>Makes every level's list (writes only a list whose variants changed); one line per level in the report and the log.</summary>
        public static string BuildAll(string why)
        {
            var plans = Plans(out var bad);
            if (bad != null) { LastLine = "level warm lists (" + why + "): " + bad + ", nothing made"; Debug.LogWarning("[OpenZ LevelWarm] " + LastLine); return LastLine; }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(Dir).Replace('\\', '/'), Path.GetFileName(Dir));
            var sb = new StringBuilder(); var line = new StringBuilder();
            sb.AppendLine("OpenZ level warm lists (" + why + ", " + DateTime.Now.ToString("d MMM yyyy HH:mm") + ")");
            foreach (var p in plans)
            {
                bool wrote = Write(PathOf(p.Id), p.Entries, out int n);
                if (line.Length > 0) line.Append(", ");
                line.Append(p.Id).Append(' ').Append(n).Append(wrote ? " (written)" : " (unchanged)");
                sb.AppendLine($"{p.Id} ({p.Scene}, {p.ScenePath ?? "SCENE NOT FOUND"}): {n} variants in {p.Entries.Select(e => e.s).Distinct().Count()} shaders; the scene uses {p.SceneShaders.Count} shaders; "
                              + $"left out (only other scenes use them): {(p.OnlyOthers.Count == 0 ? "none" : string.Join(", ", p.OnlyOthers.Select(s => s.name).OrderBy(x => x, StringComparer.Ordinal)))}");
            }
            LastLine = "level warm lists (" + why + "): " + line;
            sb.Insert(0, LastLine + "\n");
            try { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, sb.ToString()); } catch (Exception) { }
            Debug.Log("[OpenZ LevelWarm] " + LastLine);
            return LastLine;
        }

        static bool Write(string path, List<(Shader s, PassType t, string[] kw)> entries, out int count)
        {
            var have = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            var want = new HashSet<string>(entries.Select(Key));
            if (have && new HashSet<string>(Entries(have).Select(Key)).SetEquals(want)) { count = have.variantCount; return false; }
            var svc = have ? have : new ShaderVariantCollection();
            if (have) svc.Clear();
            foreach (var e in entries)
            {
                try { svc.Add(new ShaderVariantCollection.ShaderVariant(e.s, e.t, e.kw)); }
                catch (ArgumentException) { }
            }
            if (!have) AssetDatabase.CreateAsset(svc, path);
            else EditorUtility.SetDirty(svc);
            AssetDatabase.SaveAssetIfDirty(svc);
            count = svc.variantCount;
            return true;
        }

        [MenuItem("OpenZ/Shaders/Level warm lists - make now")]
        static void MenuMake() => BuildAll("menu");
    }

    /// <summary>Before every player build: every level's list made again from the shared list (a new map gets its list here).</summary>
    public sealed class OzLevelWarmListsBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { OzLevelWarmLists.BuildAll("player build"); }
            catch (Exception e) { Debug.LogWarning("[OpenZ LevelWarm] level warm lists not made before the build: " + e.Message); }
        }
    }
}
