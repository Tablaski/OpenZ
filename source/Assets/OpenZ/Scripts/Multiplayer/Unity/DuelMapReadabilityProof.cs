using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    internal static class DuelMapReadabilityProof
    {
        internal static IEnumerator Run(string folder)
        {
            var pause = UnityEngine.Object.FindFirstObjectByType<OpenZPauseMenu>();
            var flags = UnityEngine.Object.FindFirstObjectByType<FlagView>();
            var borders = UnityEngine.Object.FindFirstObjectByType<ZoneMarkers>();
            if (!pause || !flags || !borders) throw new Exception("Missing live map-readability components.");
            var original = new Dictionary<string, float?>();
            foreach (string key in new[] { OzMapReadabilityOptions.FlagPref, OzMapReadabilityOptions.RadarSweepPref })
                original[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : (float?)null;
            const string borderKey = OzMapReadabilityOptions.BorderPref;
            int? originalBorders = PlayerPrefs.HasKey(borderKey) ? PlayerPrefs.GetInt(borderKey) : (int?)null;
            const string radarKey = OzMapReadabilityOptions.RadarPref;
            int? originalRadars = PlayerPrefs.HasKey(radarKey) ? PlayerPrefs.GetInt(radarKey) : (int?)null;
            try
            {
                pause.OpenOptions(OzSettingTips.TabGame);
                if (!pause.ProofOptTabRows.Contains(OzMapReadabilityOptions.FlagLabel) || !pause.ProofOptTabRows.Contains(OzMapReadabilityOptions.BorderLabel) ||
                    !pause.ProofOptTabRows.Contains(OzMapReadabilityOptions.RadarSweepLabel))
                    throw new Exception("Live map-readability settings are missing from in-match GAME options.");
                OzMapReadabilityOptions.FlagSize = 1f;
                foreach (float size in new[] { 3f, 2.5f, 2f, 1.5f, 1f })
                {
                    Click(pause, OzMapReadabilityOptions.FlagLabel, previous: true);
                    yield return new WaitForSecondsRealtime(.2f);
                    if (OzMapReadabilityOptions.FlagSize != size || !flags.FlagsUseSize(size)) throw new Exception("FLAG SIZE did not update existing flags to " + size + "x.");
                }
                OzMapReadabilityOptions.RadarSweepSeconds = 5f;
                foreach (float interval in new[] { 3f, 1.5f, 0f, 5f })
                {
                    Click(pause, OzMapReadabilityOptions.RadarSweepLabel, previous: true);
                    if (OzMapReadabilityOptions.RadarSweepSeconds != interval) throw new Exception("The radar sweep interval did not update.");
                }
                OzMapReadabilityOptions.Borders = false;
                Click(pause, OzMapReadabilityOptions.BorderLabel);
                yield return new WaitForSecondsRealtime(.2f);
                if (!OzMapReadabilityOptions.Borders || !borders.BordersUsePreference() || !borders.ColoursMatchOwners())
                    throw new Exception("Dotted borders did not enable with current owner/neutral colours.");
                DuelProofCapture.Save(Path.Combine(folder, "map-readability-options.png"), true);
                pause.Close(); DuelRuntime.I.Menus.CloseOverlay();
                OzMapReadabilityOptions.FlagSize = 3f;
                yield return new WaitForSecondsRealtime(.2f);
                DuelProofCapture.Save(Path.Combine(folder, "flags-3x-dotted-borders.png"));
                OzMapReadabilityOptions.Borders = false;
                yield return new WaitForSecondsRealtime(.2f);
                if (!borders.BordersUsePreference()) throw new Exception("Dotted borders did not switch off live.");
            }
            finally
            {
                foreach (var v in original) { if (v.Value.HasValue) PlayerPrefs.SetFloat(v.Key, v.Value.Value); else PlayerPrefs.DeleteKey(v.Key); }
                if (originalBorders.HasValue) PlayerPrefs.SetInt(borderKey, originalBorders.Value); else PlayerPrefs.DeleteKey(borderKey);
                if (originalRadars.HasValue) PlayerPrefs.SetInt(radarKey, originalRadars.Value); else PlayerPrefs.DeleteKey(radarKey);
                PlayerPrefs.Save(); OzMapReadabilityOptions.Reload();
            }
        }
        static void Click(OpenZPauseMenu pause, string label, bool previous = false)
        {
            var rows = (IEnumerable)typeof(OpenZPauseMenu).GetField("toggles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause);
            foreach (var row in rows) if ((string)row.GetType().GetField("label").GetValue(row) == label)
            {
                var button = (HudButton)row.GetType().GetField("row").GetValue(row);
                if (previous) button.OnRightClick(new HudClick { right = true });
                else button.OnClick(new HudClick());
                return;
            }
            throw new Exception("Missing native option: " + label);
        }
    }
}
