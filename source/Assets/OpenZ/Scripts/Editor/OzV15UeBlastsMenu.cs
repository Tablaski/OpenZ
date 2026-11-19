using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_explosions_6: the Unit editor's six test explosions (View/OzUeBlasts.cs), their checks and the proof helpers.
    /// Count (edit mode or Play): the six ids, names, tiles, the two volume-lit smoke flipbooks and the blast shader, the twenty channels, the beats' numbers, the
    /// sounds' events, the SPECIAL tab's labels; one PASS / FAIL line each, the report also to Logs/ue_blasts_count.txt.
    /// Play (the Unit editor open): Drop next = the next of the six on the middle of the field (one at a time: for the dense frames
    /// and the clip of each); Drop all six = the six in a row 30 m apart; Report = every count.
    /// </summary>
    public static class OzV15UeBlastsMenu
    {
        const string Tag = "[OpenZ UeBlasts]";
        static int next;

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Count")]
        static void CountMenu() => Count();

        /// <summary>Batch entry: -executeMethod OpenZ.OzV15UeBlastsMenu.CountBatch (exit 0 = every line passed).</summary>
        public static void CountBatch() { int f = Count(); EditorApplication.Exit(f == 0 ? 0 : 1); }

        static int Count()
        {
            var log = new StringBuilder(); int fails = 0;
            void Line(bool ok, string s) { if (!ok) fails++; log.AppendLine((ok ? "PASS " : "FAIL ") + s); }

            var ids = OzUeBlasts.Ids;
            Line(ids.Length == OzUeBlasts.Count && new HashSet<string>(ids).Count == 7, $"seven different ids: {string.Join(", ", ids)}");
            var shorts = new List<string>(); var fulls = new HashSet<string>(); bool kinds = true, neutral = true, fit = true;
            foreach (var id in ids)
            {
                kinds &= OzUePlayground.KindOf(id) == UeKind.Nuke; neutral &= OzUePlayground.AlwaysNeutral(id);
                string s = OzUePlayground.ShortName(id); shorts.Add(s); fulls.Add(OzUePlayground.FullName(id));
                fit &= s.Length >= 3 && s.Length <= 6;
            }
            Line(kinds && neutral, "every id is of the NUKE's palette kind (always neutral, nothing placed, no stats card)");
            Line(fit && new HashSet<string>(shorts).Count == 7 && fulls.Count == 7, $"seven different names, the tile labels 3 to 6 letters (a 48 u cell): {string.Join(", ", shorts)}");
            Line(OzUePlayground.KindOf(OzUePlayground.Nuke) == UeKind.Nuke && OzUePlayground.ShortName(OzUePlayground.Nuke) == "NUKE" && OzUePlayground.FullName(OzUePlayground.Nuke) == "NUKE STRIKE", "the NUKE keeps its kind and names");

            int tiles = 0; string badTile = "";
            foreach (var p in OzUeBlasts.TilePaths) { var t = Resources.Load<Texture2D>(p); if (t && t.width == 256 && t.height == 256) tiles++; else badTile += " " + p; }
            Line(tiles == 7, $"seven tile pictures 256 x 256 in Resources: {tiles}{badTile}");
            var bA = Resources.Load<Texture2D>(OzUeBlastFx.BillowPath + "A"); var bB = Resources.Load<Texture2D>(OzUeBlastFx.BillowPath + "B");
            var sA = Resources.Load<Texture2D>(OzUeBlastFx.SprayPath + "A"); var sB = Resources.Load<Texture2D>(OzUeBlastFx.SprayPath + "B");
            Line(bA && bB && sA && sB && bA.width == bA.height && bA.width == bB.width && sA.width == sA.height && sA.width == sB.width && bA.width >= 2048 && sA.width >= 2048,
                $"the two volume-lit smoke flipbooks load in pairs, square, 2048 px or more: billow {(bA ? bA.width : 0)} / {(bB ? bB.width : 0)}, spray {(sA ? sA.width : 0)} / {(sB ? sB.width : 0)}");
            var bsh = Resources.Load<Shader>(OzUeBlastFx.ShaderPath);
            Line(bsh && bsh.isSupported && Resources.Load<Texture2D>(OzUeBlastFx.DetailPath), $"the blast shader loads and is supported, the detail texture is there: {(bsh ? bsh.name : "none")}");

            var defs = OzUeBlastFx.Defs(); var names = new HashSet<string>(); bool pre = true; int cap = 0;
            foreach (var d in defs) { names.Add(d.name); pre &= d.name.StartsWith("ueb_"); cap += d.capacity; }
            bool clash = false;
            foreach (var d in OpenZFxCatalog.Channels) clash |= names.Contains(d.name);
            Line(defs.Length == 20 && names.Count == 20 && pre && !clash, $"twenty channels of their own (names ueb_*, none of the catalog's), {cap} particles of room in all");

            bool beats = true; var reach = new StringBuilder();
            for (int t = 0; t < OzUeBlasts.Count; t++)
            {
                var a = OzUeBlasts.StagesOf(t, 0); var b = OzUeBlasts.StagesOf(t, 7);
                beats &= a.Length >= 1 && a.Length == b.Length;
                foreach (var s in a) beats &= s.radiusPx > 0 && s.damage > 0 && s.damage <= ZConst.MAX_UNIT_HEALTH && s.tick >= 1;
                reach.Append(OzUePlayground.ShortName(ids[t])).Append(' ').Append(a.Length).Append(" beat(s) ").Append(OzUeBlasts.ReachM(t).ToString("0.#")).Append(" m; ");
            }
            Line(beats, "every explosion has its beats (radius, damage 1.." + ZConst.MAX_UNIT_HEALTH + ", tick 1 or later): " + reach);
            Line(OzUeBlasts.StagesOf(OzUeBlasts.Chain, 0).Length == 11 && OzUeBlasts.StagesOf(OzUeBlasts.Fuel, 0).Length == 5 && OzUeBlasts.StagesOf(OzUeBlasts.Flak, 0).Length == 9 && OzUeBlasts.StagesOf(OzUeBlasts.Shock, 0).Length == 3 && OzUeBlasts.StagesOf(OzUeBlasts.Airburst, 0).Length == 9,
                "CHAIN 11 pops, FUEL 1 + 4 burn beats, FLAK 1 + 8 shard hits, SHOCK 3 wave steps, BURST 1 + 8 shard hits");

            var ta = Resources.Load<TextAsset>("OpenZ/Audio/openz_sound_events"); string json = ta ? ta.text : "";
            var missing = new List<string>();
            foreach (var ev in new[] { "exp_small", "exp_medium", "exp_large", "exp_debris_rain", "exp_vehicle_death", "dst_debris_settle", "dst_debris_concrete", "dst_fire_ignite", "imp_shrapnel_tink", "imp_bullet_stone", "dst_debris_metal", "electro_deploy", "electro_death", "electro_spark" })
                if (!json.Contains("\"" + ev + "\"")) missing.Add(ev);
            Line(json.Length > 0 && missing.Count == 0, "every sound event the explosions play is in the game's table (14)" + (missing.Count > 0 ? ": missing " + string.Join(", ", missing) : ""));

            var labels = OzUeDock.TileLabels();
            Line(labels.Length == 4 && labels[3].Length == OzUePlayground.Special.Length + OzUeBlasts.Count, $"the dock's SPECIAL tab: {labels[3].Length} tiles ({string.Join(", ", labels[3])})");
            int widest = Mathf.Max(labels[0].Length, Mathf.Max(labels[1].Length, labels[2].Length));
            Line(labels[3].Length <= widest, $"SPECIAL has no more tiles than the widest tab ({widest}): the dock keeps its width (the layout checks of the dock prove the pixels)");

            string text = $"{Tag} count: {(fails == 0 ? "ALL PASS" : fails + " FAIL")}\n{log}";
            if (fails == 0) Debug.Log(text); else Debug.LogError(text);
            try { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ue_blasts_count.txt", text.Replace("\r\n", "\n")); } catch { }
            return fails;
        }

        static bool Field(out OzUnitEditor ed, out Vector3 mid)
        {
            ed = OzUnitEditor.I; mid = default;
            if (!Application.isPlaying || !ed || ed.World == null || ed.World.Map == null) { Debug.LogWarning($"{Tag} enter Play and open the Unit editor first"); return false; }
            mid = ZGame.PxToWorld(ed.World.Map.width * 8, ed.World.Map.height * 8);
            return true;
        }

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Drop next (Play, the unit editor open)")]
        static void DropNext()
        {
            if (!Field(out _, out var mid)) return;
            int t = next % OzUeBlasts.Count; next++;
            Debug.Log($"{Tag} drop {OzUeBlasts.Ids[t]} on the middle of the field: {OzUeBlasts.TestDrop(t, mid.x, mid.z)}");
        }

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Drop all seven (Play, the unit editor open)")]
        static void DropAll()
        {
            if (!Field(out _, out var mid)) return;
            for (int t = 0; t < OzUeBlasts.Count; t++) OzUeBlasts.TestDrop(t, mid.x + (t - 3f) * 30f, mid.z);
            Debug.Log($"{Tag} the seven dropped in a row 30 m apart. {OzUeBlasts.Report()}");
        }

        [MenuItem("OpenZ/V1.5/Unit Editor/Explosions: Report")]
        static void Report() => Debug.Log($"{Tag} {OzUeBlasts.Report()}");
    }
}
