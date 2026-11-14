using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>One file of the LOAD list (DESIGN_SPEC E200): what the file says about itself, read without loading it.</summary>
    public struct UeFileInfo
    {
        public string path, file, name;
        public DateTime saved;
        public int values, units, onField;
        public bool ok;
        public string error;
    }

    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the setup file on this computer (DESIGN_SPEC section 10, ARCHITECTURE D10).
    /// Folder: Documents/OpenZ/UnitEditor (UnitEditor_editor inside the Unity editor, so a check never touches a player's files),
    /// Application.persistentDataPath/UnitEditor when Documents cannot be written (logged), or FolderOverride (the Play proof).
    /// The file: the spec-10 keys (format, version, game, saved, then the Custom set's name, based_on, changed, fields, units from
    /// OzUeSets.CustomToTree) and the "field" block (OzUePlayground.FieldToTree: units, flags and obstacles on the field). Written
    /// to &lt;file&gt;.tmp in UTF-8 and moved over the old one (as UI/OzHighScores.cs does). Nothing is ever sent anywhere: the only
    /// outside call is OPEN FOLDER, which hands the folder to the system's file browser.
    /// </summary>
    public static class OzUeFiles
    {
        public const string FileName = "openz_custom_stats.json";
        public const string Format = "openz-unit-stats";
        public const int Version = 1;
        const long MaxBytes = 4L * 1024 * 1024;

        /// <summary>The proof points this at its own folder; null in play.</summary>
        public static string FolderOverride;
        /// <summary>Checks: files written / loaded and refused loads since the game started; the last file and the last error.</summary>
        public static int Saves, Loads, Refused;
        public static string LastPath, LastError;
        static string fallback;

        static string Sub => Application.isEditor ? "UnitEditor_editor" : "UnitEditor";
        static string FallbackFolder => Path.Combine(Application.persistentDataPath, Sub);

        /// <summary>The folder files are saved to and listed from (it may not exist yet: SAVE and OPEN FOLDER make it).</summary>
        public static string Folder
        {
            get
            {
                if (!string.IsNullOrEmpty(FolderOverride)) return FolderOverride;
                if (fallback != null) return fallback;
                string docs = null;
                try { docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); } catch (Exception) { }
                return string.IsNullOrEmpty(docs) ? FallbackFolder : Path.Combine(docs, "OpenZ", Sub);
            }
        }

        /// <summary>The full path of the setup file.</summary>
        public static string FilePath => Path.Combine(Folder, FileName);

        /// <summary>A path as the dialogs show it (and COPY PATH copies it): the user's own folder written as %USERPROFILE% on
        /// Windows (the system's file windows and dialogs understand it) and ~ elsewhere, so no account name is shown on screen.</summary>
        public static string Shown(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string home = null;
            try { home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } catch (Exception) { }
            bool win = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
            if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                path = (win ? "%USERPROFILE%" : "~") + path.Substring(home.Length);
            return win ? path.Replace('/', '\\') : path;
        }

        // ------------------------------------------------------------------ save
        /// <summary>Writes the setup file (the Custom values and the field). False with the toast text when it could not.</summary>
        public static bool Save(out string path, out string error)
        {
            path = null;
            var tree = BuildTree(out error);
            if (tree == null) { Fail(error); return false; }
            string text = OzUeJson.Write(tree, true);
            if (TryWrite(Folder, text, out path, out error)) { Saved(path, text.Length); return true; }
            if (string.IsNullOrEmpty(FolderOverride) && fallback == null)
            {
                Debug.LogWarning("[OpenZ UnitEditor] the Documents folder cannot be written (" + LastError + "): files go to " + FallbackFolder + " for this session");
                fallback = FallbackFolder;
                if (TryWrite(fallback, text, out path, out error)) { Saved(path, text.Length); return true; }
            }
            Fail(error);
            return false;
        }

        static void Saved(string path, int chars)
        {
            Saves++; LastPath = path; LastError = null;
            Debug.Log("[OpenZ UnitEditor] saved " + path + " (" + chars + " characters)");
            OzUeSets.SaveProfile();                                                                           // the Custom stats also become the saved profile the levels can play (View/OzUeProfile.cs)
        }

        static void Fail(string error) { LastError = error; Debug.LogWarning("[OpenZ UnitEditor] save refused: " + error); }

        /// <summary>The whole file as a tree: P4's keys first, the Custom set's keys, then the field block.</summary>
        static Dictionary<string, object> BuildTree(out string error)
        {
            error = null;
            var root = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["format"] = Format, ["version"] = (double)Version, ["game"] = "OpenZ",
                ["saved"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            };
            object custom;
            try { custom = OzUeSets.CustomToTree(); }
            catch (Exception e) { Debug.LogException(e); error = "THE CUSTOM VALUES COULD NOT BE READ."; return null; }
            if (!(custom is Dictionary<string, object> c)) { error = "THE CUSTOM VALUES COULD NOT BE READ."; return null; }
            foreach (var kv in c) if (!root.ContainsKey(kv.Key)) root[kv.Key] = kv.Value;
            try { var field = OzUePlayground.FieldToTree(); if (field != null) root["field"] = field; }
            catch (Exception e) { Debug.LogException(e); }                                                // the values are saved even when the field cannot be
            return root;
        }

        static bool TryWrite(string dir, string text, out string path, out string error)
        {
            path = Path.Combine(dir, FileName); error = null;
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);                                                   // the UI/OzHighScores.cs way: the new file replaces the old one
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                error = "COULD NOT WRITE THE FILE.";
                return false;
            }
        }

        // ------------------------------------------------------------------ list
        /// <summary>The .json files of the folder, newest first, each read for its name, counts and whether it is a stats file.</summary>
        public static List<UeFileInfo> List()
        {
            var list = new List<UeFileInfo>();
            string dir = Folder;
            string[] files;
            try { if (!Directory.Exists(dir)) return list; files = Directory.GetFiles(dir, "*.json"); }
            catch (Exception e) { Debug.LogWarning("[OpenZ UnitEditor] the folder cannot be listed: " + e.Message); return list; }
            var stamp = new Dictionary<string, DateTime>();
            foreach (var f in files)
            {
                list.Add(Info(f));
                try { stamp[f] = File.GetLastWriteTimeUtc(f); } catch (Exception) { stamp[f] = DateTime.MinValue; }
            }
            list.Sort((a, b) => stamp[b.path].CompareTo(stamp[a.path]));
            return list;
        }

        /// <summary>What one file says about itself (nothing is loaded).</summary>
        public static UeFileInfo Info(string path)
        {
            var fi = new UeFileInfo { path = path, file = Path.GetFileName(path), name = "", ok = false };
            try { fi.saved = File.GetLastWriteTime(path); } catch (Exception) { }
            if (!Read(path, out object tree, out fi.error)) return fi;
            fi.name = OzUeJson.Str(tree, "name", "");
            if (DateTime.TryParse(OzUeJson.Str(tree, "saved", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)) fi.saved = when.ToLocalTime();
            if (OzUeJson.Get(tree, "units") is Dictionary<string, object> units)
                foreach (var u in units.Values)
                {
                    if (!(u is Dictionary<string, object> keys)) continue;
                    fi.units++;
                    foreach (var v in keys.Values) if (v is double d && !double.IsNaN(d) && !double.IsInfinity(d)) fi.values++;
                }
            var field = OzUeJson.Get(tree, "field");
            foreach (var part in new[] { "items", "flags", "obstacles", "crates" }) if (OzUeJson.Get(field, part) is List<object> l) fi.onField += l.Count;
            fi.ok = true;
            return fi;
        }

        /// <summary>Reads and checks one file: a JSON object whose format is the stats file's. error = the toast text.</summary>
        static bool Read(string path, out object tree, out string error)
        {
            tree = null; error = null;
            string text;
            try
            {
                var f = new FileInfo(path);
                if (!f.Exists || f.Length > MaxBytes) { error = "COULD NOT READ THE FILE."; return false; }
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception) { error = "COULD NOT READ THE FILE."; return false; }
            if (!OzUeJson.TryParse(text, out tree, out _)) { tree = null; error = "THIS FILE IS NOT A STATS FILE (NOT JSON)."; return false; }
            if (!(tree is Dictionary<string, object>) || OzUeJson.Str(tree, "format", null) != Format) { tree = null; error = "THIS FILE IS NOT AN OPENZ STATS FILE."; return false; }
            return true;
        }

        // ------------------------------------------------------------------ load
        /// <summary>LOAD: the file's Custom values (the playground then uses Custom); withField = LOAD WITH UNITS: the field is cleared
        /// and built again from the file's field block. message = the toast text either way.</summary>
        public static bool Load(string path, bool withField, out string message)
        {
            if (!Read(path, out object tree, out message)) { Refused++; LastError = message; Debug.LogWarning("[OpenZ UnitEditor] load refused (" + path + "): " + message); return false; }
            bool newer = OzUeJson.Num(tree, "version", Version) > Version;
            bool ok; string m;
            try { ok = OzUeSets.CustomFromTree(tree, out m); }
            catch (Exception e) { Debug.LogException(e); ok = false; m = null; }
            if (!ok)
            {
                message = string.IsNullOrEmpty(m) ? "THIS FILE IS NOT AN OPENZ STATS FILE." : m;
                Refused++; LastError = message; Debug.LogWarning("[OpenZ UnitEditor] load refused (" + path + "): " + message);
                return false;
            }
            if (OzUeSets.InUse != UeSet.Custom) OzUeSets.Use(UeSet.Custom);                                  // spec 10: a loaded file switches the playground to Custom
            message = string.IsNullOrEmpty(m) ? LoadedLine(tree) : m;
            if (newer && message.IndexOf("NEWER FILE", StringComparison.OrdinalIgnoreCase) < 0) message += " (NEWER FILE, KNOWN VALUES ONLY)";
            int placed = -1;
            if (withField)
            {
                var field = OzUeJson.Get(tree, "field");
                if (field == null) message += ". THIS FILE HAS NO FIELD";
                else
                {
                    OzUePlayground.ClearField();
                    int capped = OzUeCap.Refusals;
                    placed = OzUePlayground.FieldFromTree(field, out string ferr);
                    message += ". FIELD: " + placed + " PLACED";
                    if (!string.IsNullOrEmpty(ferr)) message += " (" + ferr.TrimEnd('.') + ")";
                    if (OzUeCap.Refusals > capped) message += " (" + (OzUeCap.Refusals - capped) + " NOT PLACED: " + OzUeCap.LastFull + ")";   // the unit cap (View/OzUeCap.cs)
                }
            }
            Loads++; LastPath = path; LastError = null;
            Debug.Log("[OpenZ UnitEditor] loaded " + path + (withField ? " with the field (" + placed + " placed)" : "") + ": " + message);
            return true;
        }

        /// <summary>The toast when the stats set gives none: LOADED "&lt;NAME&gt;": &lt;n&gt; VALUES, &lt;m&gt; UNITS (spec 10).</summary>
        static string LoadedLine(object tree)
        {
            int values = 0, units = 0;
            if (OzUeJson.Get(tree, "units") is Dictionary<string, object> us)
                foreach (var u in us.Values) if (u is Dictionary<string, object> keys) { units++; foreach (var v in keys.Values) if (v is double) values++; }
            return "LOADED \"" + OzUeJson.Str(tree, "name", "CUSTOM") + "\": " + values + " VALUES, " + units + " UNITS";
        }

        // ------------------------------------------------------------------ the folder in the system's file browser
        /// <summary>OPEN FOLDER: makes the folder and opens it in the system's file browser (a local file:/// address, nothing sent).</summary>
        public static void OpenFolder()
        {
            string dir = Folder;
            try { Directory.CreateDirectory(dir); }
            catch (Exception e) { Debug.LogWarning("[OpenZ UnitEditor] the folder cannot be made: " + e.Message); }
            string local = dir.Replace('\\', '/').TrimStart('/').Replace(" ", "%20").Replace("#", "%23");
            Application.OpenURL("file:///" + local);
            Debug.Log("[OpenZ UnitEditor] OPEN FOLDER " + dir);
        }
    }
}
