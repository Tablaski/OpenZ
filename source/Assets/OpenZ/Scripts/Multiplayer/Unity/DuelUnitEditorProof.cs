using System;
using System.IO;
using System.Reflection;
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
                    var menu = FindFirstObjectByType<OpenZMainMenu>();
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
                    SceneManager.LoadScene("MainMenu");
                    step = 3;
                    readyAt = Time.realtimeSinceStartupAsDouble + 3;
                }
                else if (FindFirstObjectByType<OpenZMainMenu>() && !OzUnitEditor.Active)
                {
                    if (firstError != null) throw new Exception("Player logged an error: " + firstError);
                    Finish("PASS: menu opens the shipped playground, editor UI/world/input/camera initialize, and return to menu succeeds.", 0);
                }
            }
            catch (Exception error) { Finish("FAIL: " + error, 1); }
        }

        void Finish(string result, int exitCode)
        {
            step = -1;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "result.txt"), result);
            Debug.Log("[OpenZ Unit Editor proof] " + result);
            Application.Quit(exitCode);
        }
    }
}
