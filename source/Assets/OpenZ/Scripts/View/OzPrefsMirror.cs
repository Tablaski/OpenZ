// OpenZ (GPL-3.0) - 1.5 kit issue8_linux_settings (GitHub issue 8): the Linux player's settings get a safety copy.
// Why: on a player's Fedora PC (1.4 Linux build) the engine looked for its settings file in ~/.config/unity3d/unknown/unknown/ on a
// start with -screen-* arguments (not in ~/.config/unity3d/OpenZ/OpenZ/), and on another start "all settings were reset to high".
// The engine picks that folder before any game code runs, so the game cannot choose it: it can only notice, say so and repair.
// What this file does, in the built Linux player only (Windows keeps its settings in the registry, the Mac in a plist: untouched):
//  1. Finds the file the engine really writes: a fresh number is saved (oz_store_alive_v1) and looked up in the candidate files
//     ($XDG_CONFIG_HOME or ~/.config, then /unity3d/<company>/<product>/prefs and /unity3d/unknown/unknown/prefs).
//  2. Keeps a safety copy of that file: openz_settings_copy.xml in the game's folder (beside OpenZ.x86_64) or, when that folder
//     cannot be written, in ~/.config/openz/. Refreshed when the engine's file changed (looked at every 20 s, when the window
//     loses focus and at the quit). A copy is never replaced by an empty or unreadable file.
//  3. At the start, before any setting is read: a safety copy or another settings file of this game that is NEWER than the engine's
//     file gives its oz_* values back (the last session wins); an older one only fills in values the engine's file does not have,
//     and only on a start whose file never saw this code. The engine's own keys (Screenmanager ..., unity. ...) are never touched.
//  4. One Player.log line says where everything is: "[OpenZ Settings] store (Linux): ...".
// Then, on every platform, View/OzSlowChipStart.cs runs (the first start on a slow graphics chip), after the values are back.
// Nothing here changes a rule, a picture or a sound. NOT proven on a real Linux machine (made on Windows): every step is guarded
// and leaves the settings as the engine loaded them when anything is not as expected. Check: Editor/OzV15Issue8LinuxCheck.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OpenZ
{
    public static class OzPrefsMirror
    {
        public const string AliveKey = "oz_store_alive_v1", CopyName = "openz_settings_copy.xml";
        /// <summary>Real seconds between two looks at the engine's file (one file time read; a copy only when it changed).</summary>
        public const float LookEvery = 20f;
        const string Tag = "[OpenZ Settings]";

        /// <summary>One line of the engine's Linux settings file: &lt;pref name="oz_x" type="int"&gt;1&lt;/pref&gt; (strings are base64).</summary>
        public struct Row { public string name, type, text; }

        /// <summary>The saved settings as this file needs them (the engine's PlayerPrefs; the check uses a table).</summary>
        public interface IStore
        {
            bool Has(string key);
            int GetInt(string key, int fallback);
            float GetFloat(string key, float fallback);
            string GetString(string key, string fallback);
            void SetInt(string key, int v);
            void SetFloat(string key, float v);
            void SetString(string key, string v);
        }

        sealed class Engine : IStore
        {
            public bool Has(string key) => PlayerPrefs.HasKey(key);
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
            public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
            public void SetInt(string key, int v) => PlayerPrefs.SetInt(key, v);
            public void SetFloat(string key, float v) => PlayerPrefs.SetFloat(key, v);
            public void SetString(string key, string v) => PlayerPrefs.SetString(key, v);
        }

        static readonly Regex RowRx = new Regex("<pref\\s+name=\"([^\"]*)\"\\s+type=\"(int|float|string)\"\\s*>([^<]*)</pref>", RegexOptions.CultureInvariant);

        static string Unescape(string s) => s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");

        /// <summary>Every value line of a settings file's text; none when the text is empty or not such a file.</summary>
        public static List<Row> Parse(string xml)
        {
            var list = new List<Row>();
            if (string.IsNullOrEmpty(xml)) return list;
            foreach (Match m in RowRx.Matches(xml)) list.Add(new Row { name = Unescape(m.Groups[1].Value), type = m.Groups[2].Value, text = m.Groups[3].Value.Trim() });
            return list;
        }

        /// <summary>Gives the game's own values (oz_*) of a file back to the store. overwrite: a value the store has is replaced when
        /// it differs (a newer file); without it only missing values are filled in. Returns how many values were written.</summary>
        public static int PutBack(List<Row> rows, IStore to, bool overwrite)
        {
            int n = 0;
            foreach (var r in rows)
            {
                if (r.name == null || !r.name.StartsWith("oz_", StringComparison.Ordinal) || r.name == AliveKey) continue;
                bool has = to.Has(r.name);
                if (has && !overwrite) continue;
                if (r.type == "int")
                {
                    if (!int.TryParse(r.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) continue;
                    if (has && to.GetInt(r.name, v == 0 ? 1 : 0) == v) continue;
                    to.SetInt(r.name, v); n++;
                }
                else if (r.type == "float")
                {
                    if (!float.TryParse(r.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                    if (has && Mathf.Approximately(to.GetFloat(r.name, v + 1f), v)) continue;
                    to.SetFloat(r.name, v); n++;
                }
                else
                {
                    string v;
                    try { v = Encoding.UTF8.GetString(Convert.FromBase64String(r.text)); }
                    catch (FormatException) { continue; }
                    if (has && to.GetString(r.name, v + "?") == v) continue;
                    to.SetString(r.name, v); n++;
                }
            }
            return n;
        }

        /// <summary>The user's configuration folder as the engine works it out: $XDG_CONFIG_HOME when it is an absolute path, else ~/.config.</summary>
        public static string ConfigRoot(string xdg, string home)
        {
            if (!string.IsNullOrEmpty(xdg) && xdg[0] == '/') return xdg.TrimEnd('/');
            return string.IsNullOrEmpty(home) ? "" : home.TrimEnd('/') + "/.config";
        }

        /// <summary>Where the engine's settings file of this game can be: its data folder, then company/product and unknown/unknown
        /// (seen in issue 8) under the configuration folder, with and without XDG_CONFIG_HOME.</summary>
        public static List<string> EngineFiles(string xdg, string home, string company, string product, string persistent)
        {
            var l = new List<string>();
            void Add(string p) { if (!string.IsNullOrEmpty(p) && !l.Contains(p)) l.Add(p); }
            if (!string.IsNullOrEmpty(persistent)) Add(persistent.TrimEnd('/') + "/prefs");
            foreach (var root in new[] { ConfigRoot(xdg, home), ConfigRoot("", home) })
            {
                if (root.Length == 0) continue;
                Add(root + "/unity3d/" + company + "/" + product + "/prefs");
                Add(root + "/unity3d/unknown/unknown/prefs");
            }
            return l;
        }

        /// <summary>Where the safety copy goes, in order: the game's folder, then openz/ under the configuration folder.</summary>
        public static List<string> CopyFiles(string xdg, string home, string gameFolder)
        {
            var l = new List<string>();
            void Add(string p) { if (!l.Contains(p)) l.Add(p); }
            if (!string.IsNullOrEmpty(gameFolder)) Add(gameFolder.TrimEnd('/') + "/" + CopyName);
            foreach (var root in new[] { ConfigRoot(xdg, home), ConfigRoot("", home) }) if (root.Length > 0) Add(root + "/openz/" + CopyName);
            return l;
        }

        // ------------------------------------------------------------------ the built Linux player
        static string engineFile = "", copyFile = "", homeDir = "";
        static List<string> copies = new List<string>();
        static DateTime copiedAt = DateTime.MinValue;
        static bool hooked;

        /// <summary>The file the engine writes ("" = not found, or not the Linux player) and the safety copy in use.</summary>
        public static string EngineFile => engineFile;
        public static string CopyFile => copyFile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Boot()
        {
            if (Application.isEditor) return;
            if (Application.platform == RuntimePlatform.LinuxPlayer)
            {
                try { Linux(); }
                catch (Exception e) { Debug.Log($"{Tag} store (Linux): left as the engine has it ({e.GetType().Name}: {e.Message})"); }
            }
            try { OzSlowChipStart.Run(); }
            catch (Exception e) { Debug.Log($"{Tag} slow graphics chip start: nothing chosen ({e.GetType().Name}: {e.Message})"); }
        }

        static DateTime Written(string f) { try { return File.Exists(f) ? File.GetLastWriteTimeUtc(f) : DateTime.MinValue; } catch (Exception) { return DateTime.MinValue; } }
        static string ReadText(string f) { try { return File.Exists(f) ? File.ReadAllText(f) : ""; } catch (Exception) { return ""; } }
        static string Short(string p) => homeDir.Length > 1 && p.StartsWith(homeDir, StringComparison.Ordinal) ? "~" + p.Substring(homeDir.Length) : p;

        static void Linux()
        {
            homeDir = (Environment.GetEnvironmentVariable("HOME") ?? "").TrimEnd('/');
            string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? "";
            string game = "";
            try { game = Path.GetDirectoryName(Application.dataPath) ?? ""; } catch (Exception) { }
            var engines = EngineFiles(xdg, homeDir, Application.companyName, Application.productName, Application.persistentDataPath);
            copies = CopyFiles(xdg, homeDir, game);
            var before = new Dictionary<string, DateTime>();
            foreach (var f in engines) before[f] = Written(f);
            bool known = PlayerPrefs.HasKey(AliveKey);

            // 1. the file the engine really writes: a fresh number, saved, then looked up
            int stamp = 1 + (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond % 2000000000L);
            PlayerPrefs.SetInt(AliveKey, stamp); PlayerPrefs.Save();
            string want = stamp.ToString(CultureInfo.InvariantCulture);
            engineFile = "";
            foreach (var f in engines)
            {
                bool hit = false;
                foreach (var r in Parse(ReadText(f))) if (r.name == AliveKey && r.type == "int" && r.text == want) { hit = true; break; }
                if (hit) { engineFile = f; break; }
            }

            // 2. the game's other settings files (safety copies, the engine's other folder), oldest first: the newest has the last word
            var others = new List<string>(copies);
            foreach (var f in engines) if (f != engineFile) others.Add(f);
            others.RemoveAll(f => Written(f) == DateTime.MinValue);
            others.Sort((a, b) => Written(a).CompareTo(Written(b)));
            var store = new Engine(); int back = 0; var said = new StringBuilder();
            foreach (var f in others)
            {
                var rows = Parse(ReadText(f));
                if (rows.Count == 0) continue;
                bool newer = engineFile.Length > 0 && Written(f) > before[engineFile].AddSeconds(2);       // with no engine file found nothing counts as newer
                if (!newer && known) continue;
                int n = PutBack(rows, store, newer);
                if (n == 0) continue;
                back += n;
                said.Append($"{(said.Length > 0 ? "; " : "")}{n} from {Short(f)} ({(newer ? "newer than the engine's file: the last session wins" : "older: only what was missing")})");
            }
            if (back > 0) PlayerPrefs.Save();

            string where = engineFile.Length > 0 ? Short(engineFile) : "a file that was not found after a save (looked at: " + string.Join(", ", engines.ConvertAll(Short)) + ")";
            Debug.Log($"{Tag} store (Linux): the engine keeps the settings in {where}{(known ? "" : " (the first start with this file)")}; "
                      + $"XDG_CONFIG_HOME {(xdg.Length == 0 ? "not set" : "[" + Short(xdg) + "]")}; values put back: {(back > 0 ? said.ToString() : "none needed")}");
            Sync("start");
            if (!hooked)
            {
                hooked = true;
                Application.quitting += () => { PlayerPrefs.Save(); Sync("quit"); };
                Application.focusChanged += focus => { if (!focus) Sync("focus"); };
            }
        }

        /// <summary>Copies the engine's settings file to the safety copy when it changed since the last copy.</summary>
        public static void Sync(string why)
        {
            if (engineFile.Length == 0) return;
            try
            {
                DateTime t = Written(engineFile);
                if (t == DateTime.MinValue || t == copiedAt) return;
                string text = ReadText(engineFile);
                if (Parse(text).Count == 0) return;                                                      // never a good copy replaced by an empty or half-written file
                copiedAt = t;
                foreach (var c in copies)
                {
                    if (!Write(c, text)) continue;
                    if (c != copyFile) { copyFile = c; Debug.Log($"{Tag} store (Linux): safety copy of the settings in {Short(c)} ({why})"); }
                    return;
                }
                if (copyFile != "none") { copyFile = "none"; Debug.Log($"{Tag} store (Linux): no safety copy could be written (tried: {string.Join(", ", copies.ConvertAll(Short))})"); }
            }
            catch (Exception) { }
        }

        static bool Write(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                File.Copy(tmp, path, true);
                File.Delete(tmp);
                return true;
            }
            catch (Exception) { return false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Host()
        {
            if (engineFile.Length == 0) return;
            var go = new GameObject("OzPrefsMirror") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<OzPrefsMirrorHost>();
        }
    }

    /// <summary>The 20 s look at the engine's settings file (the Linux player only; made by OzPrefsMirror).</summary>
    public sealed class OzPrefsMirrorHost : MonoBehaviour
    {
        float next;
        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + OzPrefsMirror.LookEvery;
            OzPrefsMirror.Sync("timer");
        }
    }
}
