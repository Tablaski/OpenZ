using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ.Sim
{
    public static class ZBehaviorJson
    {
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
    }
}
