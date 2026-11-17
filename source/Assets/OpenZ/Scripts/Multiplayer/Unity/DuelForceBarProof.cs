using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in native proof of the live display preference and bonus presentation, without changing production.</summary>
    internal static class DuelForceBarProof
    {
        internal static IEnumerator Run(string folder, string role, Action complete)
        {
            var hud = OpenZHud.I;
            bool saved = PlayerPrefs.HasKey(OzForceBarOption.Pref);
            int original = PlayerPrefs.GetInt(OzForceBarOption.Pref, 0);
            try
            {
                OzForceBarOption.MilitaryValue = false;
                yield return new WaitForSecondsRealtime(.4f);
                DuelProofCapture.Save(Path.Combine(folder, role + "-force-count.png"), true);
                var pause = UnityEngine.Object.FindFirstObjectByType<OpenZPauseMenu>();
                pause.OpenOptions(OzSettingTips.TabGame);
                yield return new WaitForSecondsRealtime(.4f);
                if (pause.IsOpen && !pause.ProofOptTabRows.Contains(OzForceBarOption.Label)) throw new Exception("Force bar option missing from GAME options.");
                var rows = (IEnumerable)typeof(OpenZPauseMenu).GetField("toggles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause);
                bool clicked = false;
                foreach (var row in rows)
                    if ((string)row.GetType().GetField("label").GetValue(row) == OzForceBarOption.Label)
                    { ((HudButton)row.GetType().GetField("row").GetValue(row)).OnClick(new HudClick()); clicked = true; break; }
                if (!clicked || !OzForceBarOption.MilitaryValue) throw new Exception("Native force bar option does not switch to military value.");
                yield return new WaitForSecondsRealtime(.2f);
                DuelProofCapture.Save(Path.Combine(folder, role + "-force-options.png"), true);
                pause.Close(); DuelRuntime.I.Menus.CloseOverlay();
                yield return new WaitForSecondsRealtime(.6f);
                var red = (OzText)typeof(OpenZHud).GetField("redCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                var blue = (OzText)typeof(OpenZHud).GetField("blueCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                if (red.text.Contains(":") || blue.text.Contains(":") ||
                    !System.Text.RegularExpressions.Regex.IsMatch(red.text, @"\d") ||
                    !System.Text.RegularExpressions.Regex.IsMatch(blue.text, @"\d"))
                    throw new Exception("Offensive production value is not displayed as a numeric score.");
                DuelProofCapture.Save(Path.Combine(folder, role + "-force-value.png"), true);
                var world = DuelRuntime.I.Game.World;
                var fort = world.Objects.First(o => o.IsFort && o.Owner == Team.Red);
                double end = fort.BFinal;
                hud.SetFortProductionBoost(Team.Red, .2);
                if (fort.BFinal != end) throw new Exception("Visual bonus hook changed production.");
                typeof(OpenZHud).GetMethod("RefreshForceAppearance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                var bonus = (OzText)typeof(OpenZHud).GetField("forceRedBonus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                if (!bonus.rectTransform.gameObject.activeInHierarchy || !bonus.text.Contains("20%")) throw new Exception("Bonus is not signaled within the force bar.");
                DuelProofCapture.Save(Path.Combine(folder, role + "-force-bonus-display.png"), true);
            }
            finally
            {
                if (hud) { hud.SetFortProductionBoost(Team.Red, 0); hud.SetFortProductionBoost(Team.Blue, 0); }
                if (saved) PlayerPrefs.SetInt(OzForceBarOption.Pref, original); else PlayerPrefs.DeleteKey(OzForceBarOption.Pref);
                PlayerPrefs.Save(); OzForceBarOption.Reload();
            }
            if (DuelRuntime.I && DuelRuntime.I.IsHost) yield return DuelMapReadabilityProof.Run(folder);
            complete();
        }
    }
}
