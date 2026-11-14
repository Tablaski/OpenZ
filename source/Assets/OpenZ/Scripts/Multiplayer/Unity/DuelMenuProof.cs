using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in native menu button and rendering checks. No effect in a normal game.</summary>
    public sealed class DuelMenuProof : MonoBehaviour
    {
        string folder;
        int step;
        double next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-oz-duel-menu-proof");
            if (option < 0 || option + 1 >= args.Length) return;
            var proof = new GameObject("DuelMenuProof").AddComponent<DuelMenuProof>();
            proof.folder = args[option + 1]; proof.next = Time.realtimeSinceStartupAsDouble + 3;
        }
        void Click(string name)
        {
            var button = DuelRuntime.I.Menus.GetComponentsInChildren<Button>().Single(b => b.gameObject.name == name && b.interactable);
            button.onClick.Invoke();
        }
        void Capture(string name) => DuelProofCapture.Save(Path.Combine(folder, name + ".png"), true);
        void Update()
        {
            if (step < 0 || Time.realtimeSinceStartupAsDouble < next) return;
            next = Time.realtimeSinceStartupAsDouble + 1;
            try
            {
                Directory.CreateDirectory(folder); var r = DuelRuntime.I;
                switch (step++)
                {
                    case 0: Click("1 CONTRE 1"); break;
                    case 1: Capture("connexion"); Click("CRÉER LA PARTIE"); break;
                    case 2:
                        if (r.Rules.HandicapEnabled) throw new Exception("Handicap enabled by default.");
                        if (!r.Menus.GetComponentsInChildren<RawImage>().Any(i => i.name == "Portrait" && i.texture)) throw new Exception("Missing native unit portraits.");
                        Capture("regles-communes"); Click("ACTIVER"); break;
                    case 3:
                        if (!r.Rules.HandicapEnabled || r.Rules.HandicapSlot != 1) throw new Exception("Handicap target incorrect.");
                        Capture("handicap-actif"); Click("ARMÉE"); break;
                    case 4:
                        Capture("armee-handicap");
                        // Changing the exception must preserve the common army.
                        int count = r.Rules.Common.StartingUnits.Count;
                        Click("HEAVY");
                        if (r.Rules.Common.StartingUnits.Count != count || r.Rules.Handicap.StartingUnits.Last() != "heavy")
                            throw new Exception("Exception edited the common army.");
                        break;
                    case 5: Capture("handicap-unite-ajoutee"); Click("DÉSACTIVER"); break;
                    case 6:
                        if (r.Rules.HandicapEnabled) throw new Exception("Cannot disable handicap.");
                        Capture("retour-regles-communes");
                        File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: native buttons, common rules, optional targeted handicap, portraits and rendered menus.");
                        step = -1; r.Close(); StartCoroutine(Exit(0)); break;
                }
            }
            catch (Exception error)
            { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: " + error); step = -1; StartCoroutine(Exit(1)); }
        }
        IEnumerator Exit(int code) { yield return null; Application.Quit(code); }
    }
}
