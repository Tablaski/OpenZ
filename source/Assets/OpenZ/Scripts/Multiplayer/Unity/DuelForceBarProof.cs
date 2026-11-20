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
            bool saved = PlayerPrefs.HasKey(OzForceBarOption.DuelPref);
            int original = PlayerPrefs.GetInt(OzForceBarOption.DuelPref, 0);
            bool hadEdge = PlayerPrefs.HasKey("oz_edge_scroll"); int edge = PlayerPrefs.GetInt("oz_edge_scroll", 1);
            try
            {
                while (OzForceBarOption.ForMatch(true) != OzForceBarOption.Mode.Units) OzForceBarOption.StepMatch(true, 1);
                yield return new WaitForSecondsRealtime(.4f);
                DuelProofCapture.Save(Path.Combine(folder, role + "-force-count.png"), true);
                var pause = UnityEngine.Object.FindFirstObjectByType<OpenZPauseMenu>();
                pause.OpenOptions(OzSettingTips.TabGame);
                yield return new WaitForSecondsRealtime(.4f);
                DuelOptionProof.SetEdgeScroll(pause,true);
                yield return new WaitForSecondsRealtime(.3f);
                DuelOptionProof.Check(pause);
                DuelProofCapture.Save(Path.Combine(folder,role+"-pause-options-on.png"),true);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,role+"-pause-options-on-screen.png"));
                yield return new WaitForSecondsRealtime(.2f);
                DuelOptionProof.SetEdgeScroll(pause,false);
                yield return new WaitForSecondsRealtime(.3f);
                DuelOptionProof.Check(pause);
                DuelProofCapture.Save(Path.Combine(folder,role+"-pause-options-off.png"),true);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,role+"-pause-options-off-screen.png"));
                yield return new WaitForSecondsRealtime(.2f);
                if (pause.IsOpen && !pause.ProofOptTabRows.Contains(OzForceBarOption.Label)) throw new Exception("Force bar option missing from GAME options.");
                var rows = (IEnumerable)typeof(OpenZPauseMenu).GetField("toggles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause);
                bool clicked = false;
                foreach (var row in rows)
                    if ((string)row.GetType().GetField("label").GetValue(row) == OzForceBarOption.Label)
                    { ((HudButton)row.GetType().GetField("row").GetValue(row)).OnClick(new HudClick()); clicked = true; break; }
                if (!clicked || OzForceBarOption.ForMatch(true) != OzForceBarOption.Mode.AttackingPower) throw new Exception("Native force bar option does not switch to attacking power.");
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
                OzForceBarOption.StepMatch(true, 1);
                yield return new WaitForSecondsRealtime(.4f);
                var unitsSuffix = (OzText)typeof(OpenZHud).GetField("redOf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                var power = (OzText)typeof(OpenZHud).GetField("powerRedText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                if (unitsSuffix.text != "/ 70" || !power.rectTransform.gameObject.activeInHierarchy ||
                    (!power.text.Contains("%") && !power.text.Contains("∞")))
                    throw new Exception("Both mode must put the percentage on attacking power and the unit limit on units.");
                DuelProofCapture.Save(Path.Combine(folder, role + "-force-both.png"), true);
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
                if (saved) PlayerPrefs.SetInt(OzForceBarOption.DuelPref, original); else PlayerPrefs.DeleteKey(OzForceBarOption.DuelPref);
                if(hadEdge)PlayerPrefs.SetInt("oz_edge_scroll",edge);else PlayerPrefs.DeleteKey("oz_edge_scroll");
                PlayerPrefs.Save(); OzForceBarOption.Reload();
            }
            if (DuelRuntime.I && DuelRuntime.I.IsHost) yield return DuelMapReadabilityProof.Run(folder);
            complete();
        }
    }
}
