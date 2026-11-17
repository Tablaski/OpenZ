using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in standalone regression check for the menu's Unit Editor entry.</summary>
    public sealed class DuelUnitEditorProof : MonoBehaviour
    {
        string folder;
        int step;
        double deadline, readyAt;
        string firstError;
        readonly Dictionary<string, string> preferences = new Dictionary<string, string>();
        string previousFolder;

        void OnEnable() => Application.logMessageReceived += RecordError;
        void OnDisable() => Application.logMessageReceived -= RecordError;
        void RecordError(string message, string trace, LogType type)
        {
            if (firstError == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) firstError = message;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-oz-duel-unit-editor-proof");
            if (option < 0 || option + 1 >= args.Length) return;
            var proof = new GameObject("DuelUnitEditorProof").AddComponent<DuelUnitEditorProof>();
            DontDestroyOnLoad(proof.gameObject);
            proof.folder = args[option + 1];
            proof.deadline = Time.realtimeSinceStartupAsDouble + 60;
            proof.readyAt = Time.realtimeSinceStartupAsDouble + 3;
        }

        void Update()
        {
            if (step < 0 || Time.realtimeSinceStartupAsDouble < readyAt) return;
            try
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Unit Editor proof timed out at step " + step);
                if (step == 0)
                {
                    if (preferences.Count == 0)
                    {
                        foreach (string key in new[] { "oz_behavior_library_v1", "oz_behavior_selected_0", "oz_behavior_selected_1", "oz_behavior_draft_0", "oz_behavior_draft_1" })
                            preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                        previousFolder = OzUeFiles.FolderOverride; OzUeFiles.FolderOverride = folder;
                    }
                    var menu = FindFirstObjectByType<OpenZMainMenu>();
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-oz-unit-editor") >= 0)
                    {
                        if (SceneManager.GetActiveScene().name != OzUnitEditor.SceneName)
                            throw new Exception("Direct launcher did not open the Unit Editor.");
                        step = 1;
                        return;
                    }
                    if (!menu || !menu.ProofOnMainList) return;
                    if (!menu.ProofUnitEditorItem(out _, out _, out _, out _)) throw new Exception("UNIT EDITOR menu item is missing.");
                    if (!Application.CanStreamedLevelBeLoaded(OzUnitEditor.SceneName)) throw new Exception("Playground scene is absent from the player.");
                    int starts = OpenZMainMenu.UnitEditorStarts;
                    typeof(OpenZMainMenu).GetMethod("GoUnitEditor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
                    if (OpenZMainMenu.UnitEditorStarts != starts + 1) throw new Exception("Menu refused to open the Unit Editor.");
                    step = 1;
                }
                else if (step == 1)
                {
                    if (!OzUnitEditor.UiBuilt) return;
                    if (SceneManager.GetActiveScene().name != OzUnitEditor.SceneName) throw new Exception("Wrong playground scene.");
                    var editor = OzUnitEditor.I;
                    if (OzUnitEditor.BuildErrors != 0 || editor.World == null || !editor.Input || !editor.Rts)
                        throw new Exception("Unit Editor did not initialize fully.");
                    if (editor.World.Bots.Count != 0 || !editor.World.NoGameEnd) throw new Exception("Playground rules were not applied.");
                    step = 2;
                    readyAt = Time.realtimeSinceStartupAsDouble + 2;
                }
                else if (step == 2)
                {
                    Directory.CreateDirectory(folder);
                    DuelProofCapture.Save(Path.Combine(folder, "unit-editor.png"));
                    var panel = OzBehaviorPanel.I;
                    if (!panel) throw new Exception("Behavior laboratory was not installed.");
                    if (!panel.EntryIsClickable) throw new Exception("BEHAVIOR button is visible but the HUD does not capture clicks on it.");
                    if (!panel.TestingEntryIsClickable) throw new Exception("UNIT TESTING button does not capture clicks.");
                    panel.OpenFromButton();
                    if (!panel.IsOpen) throw new Exception("BEHAVIOR button did not open its panel.");
                    panel.SetMaster(ZBehaviorFamily.Tanks, 100);
                    panel.SetMaster(ZBehaviorFamily.Robots, 100);
                    panel.Edit(ZBehaviorFamily.Robots, ZBehaviorKey.Engaged, 1);
                    var draft = panel.Draft(ZBehaviorFamily.Tanks);
                    OzBehaviorLibrary.Save(draft, "Native proof tank preset", true);
                    OzBehaviorLibrary.Select(draft);
                    string exported = OzBehaviorLibrary.Export(draft);
                    var imported = OzBehaviorLibrary.Import(exported);
                    if (imported.master != 100 || imported.family != ZBehaviorFamily.Tanks) throw new Exception("Behavior import/export did not retain the profile.");
                    if (OzUnitEditor.I.World.BehaviorProfile(ZBehaviorFamily.Robots).Get("grunt", ZBehaviorKey.Engaged) != 1)
                        throw new Exception("Live robot override did not reach the editor world.");
                    foreach (var d in ZBehaviorSchema.All)
                        if (string.IsNullOrWhiteSpace(d.Tip) || !panel.Explain(d.Key).Contains(d.Tip) || !panel.Explain(d.Key).Contains("Defaults: OpenZ "))
                            throw new Exception("Missing behavior help/reference: " + d.Key);
                    panel.ShowPage(ZBehaviorFamily.Tanks, 0);
                    step = 3; readyAt = Time.realtimeSinceStartupAsDouble + .8;
                }
                else if (step >= 3 && step <= 14)
                {
                    int shot = step - 3;
                    DuelProofCapture.Save(Path.Combine(folder, "behavior-" + (shot < 6 ? "tanks-" : "robots-") + (shot % 6) + ".png"));
                    if (OzBehaviorPanel.I.PanelRect.x < OzUnitEditor.CanvasSize.x / 2 || OzBehaviorPanel.I.PanelRect.width < 640 || OzBehaviorPanel.I.PanelRect.width > 760)
                        throw new Exception("Dynamics is not hosted in the widened right-hand panel.");
                    if (step < 14) OzBehaviorPanel.I.ShowPage(shot < 5 ? ZBehaviorFamily.Tanks : ZBehaviorFamily.Robots, (shot + 1) % 6);
                    else
                    {
                        OzBehaviorPanel.I.ShowTesting();
                        var w = OzUnitEditor.I.World; var testing = w.UnitTesting;
                        testing.SetHpPercent(Team.Red, 37); testing.SetHpPercent(Team.Blue, 64);
                        testing.Configure(Team.Red, true, true); testing.Configure(Team.Blue, true, false);
                        testing.ShowDamage = testing.ShowCounts = true;
                        var a = w.CreateObject("vehicle", "light", 240, 240, Team.Red);
                        var b = w.CreateObject("vehicle", "heavy", 600, 240, Team.Blue);
                        a.DamageHealth(a.Health, b.Id);
                        if (a.Destroyed || a.Health != testing.ReferenceHp(a) || testing.For(a.Id).Deaths != 1 || testing.For(b.Id).Kills != 1)
                            throw new Exception("Circular HP/score integration failed in the native playground.");
                        b.DamageHealth(1, a.Id);
                        if (b.Health != testing.ReferenceHp(b)) throw new Exception("Pinned blue HP changed after damage.");
                    }
                    step++; readyAt = Time.realtimeSinceStartupAsDouble + .5;
                }
                else if (step == 15)
                {
                    if (!OzBehaviorPanel.I.TestingOpen) throw new Exception("Testing sidebar did not open.");
                    DuelProofCapture.Save(Path.Combine(folder, "unit-testing.png"));
                    SceneManager.LoadScene("MainMenu");
                    step = 16;
                    readyAt = Time.realtimeSinceStartupAsDouble + 3;
                }
                else if (step == 16 && FindFirstObjectByType<OpenZMainMenu>() && !OzUnitEditor.Active)
                {
                    if (firstError != null) throw new Exception("Player logged an error: " + firstError);
                    OzBehaviorOptions.Show(ZBehaviorFamily.Tanks);
                    step = 17; readyAt = Time.realtimeSinceStartupAsDouble + 1;
                }
                else if (step == 17)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, "behavior-game-dropdown.png"));
                    step = 18; readyAt = Time.realtimeSinceStartupAsDouble + 1;
                }
                else if (step == 18)
                {
                    Finish("PASS: Unit Editor; clickable behavior/testing entries; twelve Dynamics pages in the shared right-hand tabbed panel; help/references; live independent family profiles; save/import/export; circular reference HP and virtual scores; pinned HP; testing sidebar; game dropdown; clean return to menu.", 0);
                }
            }
            catch (Exception error) { Finish("FAIL: " + error, 1); }
        }

        void Finish(string result, int exitCode)
        {
            step = -1;
            foreach (var p in preferences) { if (p.Value == null) PlayerPrefs.DeleteKey(p.Key); else PlayerPrefs.SetString(p.Key, p.Value); }
            PlayerPrefs.Save(); OzBehaviorLibrary.Reload(); OzUeFiles.FolderOverride = previousFolder;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "result.txt"), result);
            Debug.Log("[OpenZ Unit Editor proof] " + result);
            Application.Quit(exitCode);
        }
    }
}
