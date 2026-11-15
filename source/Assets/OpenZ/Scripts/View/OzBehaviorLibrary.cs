using System;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Named behavior presets and editor drafts are independent of the selected next-match profiles.</summary>
    public static class OzBehaviorLibrary
    {
        public const string TanksLabel = "TANK BEHAVIOR", RobotsLabel = "ROBOT BEHAVIOR";
        const string LibraryKey = "oz_behavior_library_v1";
        [Serializable] sealed class Saved { public string[] profiles = Array.Empty<string>(); }
        static List<ZBehaviorProfile> saved;
        public static string LastError;
        public static List<ZBehaviorProfile> SavedProfiles
        {
            get
            {
                if (saved != null) return saved;
                saved = new List<ZBehaviorProfile>();
                try
                {
                    var data = JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(LibraryKey, "{}"));
                    foreach (var text in data?.profiles ?? Array.Empty<string>())
                    {
                        try { var p = ZBehaviorProfile.Decode(text); if (p.id.Length > 0) saved.Add(p); }
                        catch (Exception e) { Debug.LogWarning("[OpenZ Behavior] Ignored invalid preset: " + e.Message); }
                    }
                }
                catch (Exception e) { Debug.LogWarning("[OpenZ Behavior] Could not read presets: " + e.Message); }
                return saved;
            }
        }
        public static ZBehaviorProfile Native(ZBehaviorFamily f) => new ZBehaviorProfile { family = f, id = "native", name = "OpenZ Native" };
        public static ZBehaviorProfile Zod(ZBehaviorFamily f) => new ZBehaviorProfile { family = f, id = "zod-classic", name = "Zod Classic", master = 100 };
        public static List<ZBehaviorProfile> Choices(ZBehaviorFamily f)
        {
            var list = new List<ZBehaviorProfile> { Native(f), Zod(f) };
            var local = Zod(f); local.id = "zod-local"; local.name = "Zod Local (idle only)";
            foreach (var k in new[] { ZBehaviorKey.Move, ZBehaviorKey.AttackOrder, ZBehaviorKey.Retreat, ZBehaviorKey.ForceMove, ZBehaviorKey.Approach }) local.Set("", k, 0);
            list.Add(local);
            if (f == ZBehaviorFamily.Robots)
            {
                var retreat = Native(f); retreat.id = "retreat-dodge"; retreat.name = "Retreat Dodge";
                retreat.Set("", ZBehaviorKey.Retreat, 1); retreat.Set("", ZBehaviorKey.StandFirm, 0);
                retreat.Set("", ZBehaviorKey.BreakForRetreat, 1); retreat.Set("", ZBehaviorKey.Cooldown, 1);
                retreat.Set("", ZBehaviorKey.ShootFirst, 0); list.Add(retreat);
            }
            foreach (var p in SavedProfiles) if (p.family == f) list.Add(p.Clone());
            return list;
        }
        static string SelectedKey(ZBehaviorFamily f) => "oz_behavior_selected_" + (int)f;
        public static string SelectedId(ZBehaviorFamily f) => PlayerPrefs.GetString(SelectedKey(f), "native");
        public static ZBehaviorProfile Selected(ZBehaviorFamily f) => Choices(f).Find(p => p.id == SelectedId(f)) ?? Native(f);
        public static string SelectedName(ZBehaviorFamily f)
        {
            string id = SelectedId(f);
            if (id == "native") return "OpenZ Native  v";
            if (id == "zod-classic") return "Zod Classic  v";
            if (id == "zod-local") return "Zod Local (idle only)  v";
            if (id == "retreat-dodge" && f == ZBehaviorFamily.Robots) return "Retreat Dodge  v";
            return (SavedProfiles.Find(p => p.id == id && p.family == f)?.name ?? "OpenZ Native") + "  v";
        }
        public static void Reload() { saved = null; }
        public static void Select(ZBehaviorProfile p)
        { PlayerPrefs.SetString(SelectedKey(p.family), p.id); PlayerPrefs.Save(); }
        public static void Save(ZBehaviorProfile working, string name, bool copy)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80) throw new ArgumentException("Use a preset name between 1 and 80 characters.");
            bool exists = SavedProfiles.Exists(p => p.id == working.id && p.family == working.family);
            if (copy || !exists) working.id = Guid.NewGuid().ToString("N");
            working.name = name;
            // Freeze the endpoints while retaining the master/override relationship for subsequent editing.
            var frozen = working.Clone();
            frozen.FreezeReferences();
            SavedProfiles.RemoveAll(p => p.id == working.id); SavedProfiles.Add(frozen);
            Persist();
        }
        public static void Delete(ZBehaviorProfile p)
        {
            SavedProfiles.RemoveAll(x => x.id == p.id); Persist();
            if (SelectedId(p.family) == p.id) Select(Native(p.family));
        }
        static void Persist()
        {
            var data = new Saved { profiles = SavedProfiles.ConvertAll(p => p.Encode()).ToArray() };
            PlayerPrefs.SetString(LibraryKey, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        public static void SaveDraft(ZBehaviorProfile p) => PlayerPrefs.SetString("oz_behavior_draft_" + (int)p.family, p.Encode());
        public static ZBehaviorProfile Draft(ZBehaviorFamily f)
        {
            try { string s = PlayerPrefs.GetString("oz_behavior_draft_" + (int)f, ""); return s.Length == 0 ? Selected(f) : ZBehaviorProfile.Decode(s); }
            catch (Exception) { return Native(f); }
        }
        public static string Folder => Path.Combine(OzUeFiles.Folder, "BehaviorPresets");
        [Serializable] sealed class JsonSetting { public string type, key; public double value; }
        [Serializable] sealed class JsonProfile
        {
            public string format = "openz-combat-dynamics";
            public int version = 1;
            public string family, name;
            public double master;
            public JsonSetting[] overrides, openZReferences, zodReferences;
        }
        static JsonSetting[] JsonSettings(List<ZBehaviorOverride> settings) => settings.ConvertAll(o =>
            new JsonSetting { type = o.type, key = o.key.ToString(), value = o.value }).ToArray();
        public static string EncodeJson(ZBehaviorProfile profile)
        {
            var p = profile.Clone(); p.FreezeReferences();
            return JsonUtility.ToJson(new JsonProfile { family = p.family == ZBehaviorFamily.Tanks ? "tanks" : "robots", name = p.name,
                master = p.master, overrides = JsonSettings(p.overrides), openZReferences = JsonSettings(p.nativeReferences),
                zodReferences = JsonSettings(p.zodReferences) }, true);
        }
        public static ZBehaviorProfile DecodeJson(string text)
        {
            if (text == null || text.Length > 2000000) throw new FormatException("Invalid JSON profile size.");
            var data = JsonUtility.FromJson<JsonProfile>(text);
            if (data == null || data.format != "openz-combat-dynamics" || data.version != 1 ||
                data.family != "tanks" && data.family != "robots" || string.IsNullOrWhiteSpace(data.name))
                throw new FormatException("Expected an OpenZ combat-dynamics JSON profile (version 1, tanks or robots).");
            var p = new ZBehaviorProfile { family = data.family == "tanks" ? ZBehaviorFamily.Tanks : ZBehaviorFamily.Robots,
                name = data.name, master = data.master };
            void Read(JsonSetting[] entries, List<ZBehaviorOverride> references)
            {
                foreach (var entry in entries ?? Array.Empty<JsonSetting>())
                {
                    if (entry == null || entry.type == null || !Enum.TryParse(entry.key, out ZBehaviorKey key) ||
                        key < 0 || key >= ZBehaviorKey.Count || !p.KnownType(entry.type)) throw new FormatException("Invalid JSON profile setting.");
                    double value = ZBehaviorSchema.Def(key).Clamp(entry.value);
                    if (references == null) p.Set(entry.type, key, value);
                    else
                    {
                        if (references.Exists(o => o.type == entry.type && o.key == key)) throw new FormatException("Duplicate JSON reference.");
                        references.Add(new ZBehaviorOverride { type = entry.type, key = key, value = value });
                    }
                }
            }
            Read(data.overrides, null); Read(data.openZReferences, p.nativeReferences); Read(data.zodReferences, p.zodReferences);
            // Reuse the same header, family, finite-number and reference validation as saved/native profiles.
            return p.Clone();
        }
        public static string ExportJson(ZBehaviorProfile p) => WriteExport(p, ".json", EncodeJson(p));
        public static string Export(ZBehaviorProfile p)
            => WriteExport(p, ".ozbehavior", p.Encode());
        static string WriteExport(ZBehaviorProfile p, string extension, string contents)
        {
            Directory.CreateDirectory(Folder);
            string name = p.name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (name.Length == 0 || name == "." || name == "..") name = "Preset";
            string path = Path.Combine(Folder, name + extension);
            string temp = path + ".tmp"; File.WriteAllText(temp, contents);
            if (File.Exists(path)) File.Delete(path); File.Move(temp, path); return path;
        }
        public static ZBehaviorProfile Import(string path)
        {
            if (new FileInfo(path).Length > 2000000) throw new IOException("Preset file is too large.");
            string text = File.ReadAllText(path);
            var p = text.TrimStart().StartsWith("{", StringComparison.Ordinal) ? DecodeJson(text) : ZBehaviorProfile.Decode(text);
            p.id = ""; return p;
        }
        public static void AtWorldStart(ZGame game)
        {
            if (game.World == null || OzUnitEditor.IsPlayground(game.gameObject.scene)) return;
            // Multiplayer guests use the host's replicated profiles, never their own local choices.
            if (OpenZ.Multiplayer.DuelRuntime.Active && !OpenZ.Multiplayer.DuelRuntime.I.IsHost) return;
            game.World.SetBehavior(Selected(ZBehaviorFamily.Tanks)); game.World.SetBehavior(Selected(ZBehaviorFamily.Robots));
        }
    }
}
