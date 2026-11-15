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
            var original = new Dictionary<string, int?>();
            foreach (string key in new[] { OzMapReadabilityOptions.FlagPref, OzMapReadabilityOptions.BorderPref })
                original[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            try
            {
                pause.OpenOptions(OzSettingTips.TabGame);
                if (!pause.ProofOptTabRows.Contains(OzMapReadabilityOptions.FlagLabel) || !pause.ProofOptTabRows.Contains(OzMapReadabilityOptions.BorderLabel))
                    throw new Exception("Live map-readability settings are missing from in-match GAME options.");
                OzMapReadabilityOptions.FlagSize = 1;
                yield return new WaitForSecondsRealtime(.2f);
                foreach (int size in new[] { 4, 2, 1 })
                {
                    Click(pause, OzMapReadabilityOptions.FlagLabel, true);
                    yield return new WaitForSecondsRealtime(.2f);
                    if (OzMapReadabilityOptions.FlagSize != size || !flags.FlagsUseSize(size)) throw new Exception("FLAG SIZE did not update existing flags to x" + size);
                }
                OzMapReadabilityOptions.Borders = false;
                Click(pause, OzMapReadabilityOptions.BorderLabel);
                yield return new WaitForSecondsRealtime(.2f);
                if (!OzMapReadabilityOptions.Borders || !borders.BordersUsePreference() || !borders.ColoursMatchOwners())
                    throw new Exception("Dotted borders did not enable with current owner/neutral colours.");
                DuelProofCapture.Save(Path.Combine(folder, "map-readability-options.png"), true);
                pause.Close(); DuelRuntime.I.Menus.CloseOverlay();
                OzMapReadabilityOptions.FlagSize = 4;
                yield return new WaitForSecondsRealtime(.2f);
                DuelProofCapture.Save(Path.Combine(folder, "flags-x4-dotted-borders.png"));
                OzMapReadabilityOptions.Borders = false;
                yield return new WaitForSecondsRealtime(.2f);
                if (!borders.BordersUsePreference()) throw new Exception("Dotted borders did not switch off live.");
            }
            finally
            {
                foreach (var v in original) { if (v.Value.HasValue) PlayerPrefs.SetInt(v.Key, v.Value.Value); else PlayerPrefs.DeleteKey(v.Key); }
                PlayerPrefs.Save(); OzMapReadabilityOptions.Reload();
            }
        }
        static void Click(OpenZPauseMenu pause, string label, bool backwards = false)
        {
            var rows = (IEnumerable)typeof(OpenZPauseMenu).GetField("toggles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause);
            foreach (var row in rows) if ((string)row.GetType().GetField("label").GetValue(row) == label)
            { var button = (HudButton)row.GetType().GetField("row").GetValue(row); if (backwards) button.OnRightClick(new HudClick()); else button.OnClick(new HudClick()); return; }
            throw new Exception("Missing native option: " + label);
        }
    }
}
