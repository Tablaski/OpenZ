#if !OPENZ_HARNESS
using System.Collections;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of the testing panel's key (UI/OzCheatSpawner.cs): ALT+P only, left or
    /// right Alt. Real key events (queued Input System states, as OzCheatSpawner.TestRoutine does).
    /// Menu OpenZ/Checks/V1.3/Item 28 ALT+P testing panel:
    ///  * Edit mode (no Play): the source reads (E1 .. E7).
    ///  * Play in a level (the Game view in front, no menu open): L1 .. L20 (the panel, the cannon key on P and on C).
    ///  * Play in the Unit editor's playground: U1 .. U5 (its own P = the dock, unchanged; no panel there).
    /// The cannon part puts a gatling into one of the player's buildings when none holds a stored gun, and takes it out again at the
    /// end; the cannon key's saved value is put back exactly. Nothing else is saved. One line "[OpenZ V1.3 item28] ... PASS n/n" at the
    /// </summary>
    public static class OzV13AltPCheck
    {
        const string Tag = "[OpenZ V1.3 item28]";
        public static string Report = "not run";
        static string Dir => OzRelease.Milestones("v1_3_20261030/item28_alt_p");

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzAltPCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Begin() { ok = 0; bad = 0; notes.Length = 0; }
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
        static void End(string part)
        {
            Report = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, part.Replace(' ', '_') + ".txt"), Report + "\n"); }
            catch (System.Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
        static IEnumerator Tap(params Key[] k) { Keys(k); yield return Frames(2); Keys(); yield return Frames(3); }

        [MenuItem("OpenZ/Checks/V1.3/Item 28 ALT+P testing panel (Edit: source; Play: a level or the Unit editor)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { SourceCheck(); return; }
            if (Keyboard.current == null) { Debug.LogError(Tag + " no keyboard device"); return; }
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            if (OzUnitEditor.I) { Report = "running"; R().StartCoroutine(UnitEditorRoutine()); return; }
            var hud = OpenZHud.I;
            if (!hud || !hud.Root || !ZGame.I || ZGame.I.World == null) { Debug.LogError(Tag + " start a level (or the Unit editor) first"); return; }
            if (!OzCheatSpawner.DevEnabled || !OzCheatSpawner.I) { Debug.LogError(Tag + " FAIL: the testing panel is not installed (oz_dev 0?)"); return; }
            if (hud.Modal || !hud.hudVisible) { Debug.LogError(Tag + " close the menus and show the HUD first"); return; }
            Report = "running"; R().StartCoroutine(LevelRoutine());
        }

        // ------------------------------------------------------------------ edit mode: the source
        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

        static void SourceCheck()
        {
            Begin();
            string spawner = Src("UI/OzCheatSpawner.cs"), input = Src("View/PlayerInput.cs"), ueKeys = Src("View/OzUeKeys.cs");
            string hudSrc = Src("UI/OpenZHud.cs"), arrange = Src("UI/OpenZHud.Arrange.cs"), cam = Src("View/RtsCamera.cs");
            Check(spawner.Contains("kb.pKey.wasPressedThisFrame && kb.altKey.isPressed && !hud.Modal) Show(!shown);")
                  && !spawner.Contains("kb.pKey.wasPressedThisFrame && !hud.Modal") && !spawner.Contains("kb.pKey.wasPressedThisFrame && kb.ctrlKey.isPressed"),
                  "E1 the panel toggles on P only with Alt held (left or right: Keyboard.altKey), never on a plain P or Ctrl+P");
            int at = input.IndexOf("OzBindings.Pressed(OzAction.PlaceCannon)", System.StringComparison.Ordinal);
            string line = at >= 0 ? input.Substring(at, Mathf.Min(160, input.Length - at)) : "";
            Check(at >= 0 && line.Contains("!kb.ctrlKey.isPressed && !kb.altKey.isPressed") && !input.Contains("OzCheatSpawner.OwnsP"),
                  "E2 the cannon key gives way to the panel no more, and is never read with Ctrl or Alt held (Ctrl+C = ALL CANNONS, Alt+P = the panel)");
            Check(spawner.Contains("|| OzUeHooks.NoCheatPanel) return;") && ueKeys.Contains("if (Letter(kb, 'P')) Act(\"P\", () => OzUeControls.ToggleDock());")
                  && ueKeys.Contains("if (kb.ctrlKey.isPressed || kb.altKey.isPressed"), "E3 the Unit editor: no panel there, its own P toggles the dock, its keys ignore Ctrl and Alt (unchanged)");
            Check(OzUeBottomLayout.RailKey == "P", "E4 the Unit editor's dock rail still shows the key P (OzUeBottomLayout.RailKey)");
            // E5: no script still opens the panel with a plain P or with Ctrl+P (this file is skipped: it presses them on purpose)
            string[] old = { "KeyboardState(Key." + "P)", "KeyTap(Key." + "P)", "Tap(Key." + "P)", "Key1(Key." + "P)",
                             "KeyboardState(Key.LeftCtrl, Key." + "P)", "Tap(Key." + "P, Key.LeftCtrl)", "KeyCtrl(Key." + "P)", "Ctrl" + "P()" };
            var left = new StringBuilder(); int files = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(f) == "OzV13AltPCheck.cs") continue;
                files++; string s = File.ReadAllText(f);
                foreach (var o in old) if (s.Contains(o)) left.Append(' ').Append(Path.GetFileName(f)).Append(": ").Append(o).Append(';');
            }
            Check(left.Length == 0, $"E5 no script presses a plain P or Ctrl+P for the panel ({files} scripts read){(left.Length > 0 ? ": still" + left : "")}");
            // E6: every reader of a plain bound key skips it with Alt held, so Alt+P fires none of them even when the player binds one to P
            string[] plain =
            {
                "if (!over && !ctrl && !alt && Selection.Count > 0)",                                                   // PATROL / STOP / HOLD
                "if (ctrl && !alt)",                                                                                   // the Ctrl actions
                "else if (!alt)",                                                                                      // NEXT ROBOT / VEHICLE / GUN, SEND TO REPAIR
            };
            bool all = true; var miss = new StringBuilder();
            foreach (var p in plain) if (!input.Contains(p)) { all = false; miss.Append(" PlayerInput: ").Append(p).Append(';'); }
            foreach (var a in new[] { "FactoryList", "ExitCrew", "MinimalUi" })
                if (!hudSrc.Contains("OzBindings.Pressed(OzAction." + a + ") && !kb.ctrlKey.isPressed && !kb.altKey.isPressed")) { all = false; miss.Append(" OpenZHud: ").Append(a).Append(';'); }
            if (!arrange.Contains("OzBindings.Pressed(OzAction.ArrangeHud) && !kb.ctrlKey.isPressed && !kb.altKey.isPressed")) { all = false; miss.Append(" ArrangeHud;"); }
            if (!cam.Contains("bool modifier = kb != null && (kb.ctrlKey.isPressed || kb.altKey.isPressed);")) { all = false; miss.Append(" RtsCamera scroll keys;"); }
            Check(all, "E6 every plain key reader skips Alt (orders, NEXT keys, repair, cannon, factory list, exit crew, minimal UI, arrange, scroll keys)"
                       + (all ? "; read without any modifier test by design: HIDE THE HUD, LAST MESSAGE, CONTROLS HELP" : ": missing" + miss));
            // E7: no default key is P, and the panel is the only reader of the P key in the game code
            bool noDefaultP = true; foreach (var d in OzBindings.Defs) if (d.key == Key.P) noDefaultP = false;
            int pReaders = 0; var who = new StringBuilder();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                if (f.Replace('\\', '/').Contains("/Editor/")) continue;
                if (File.ReadAllText(f).Contains("kb.pKey")) { pReaders++; who.Append(' ').Append(Path.GetFileName(f)); }
            }
            Check(noDefaultP && pReaders == 1 && who.ToString().Trim() == "OzCheatSpawner.cs", $"E7 no action has P as its default ({noDefaultP}); game scripts reading the P key: {pReaders} ({who.ToString().Trim()})");
            End("edit source");
        }

        // ------------------------------------------------------------------ Play, a level
        static string Others(OpenZHud hud, PlayerInput pi, ZGame game)
        {
            var help = Object.FindFirstObjectByType<OzHelpOverlay>();
            return $"hud {hud.hudVisible}, arrange {hud.Arranging}, help {(help && help.IsOpen)}, placing {(pi && pi.Placing != null)}, speed {game.gameSpeed:0.##}, paused {game.paused}";
        }

        static IEnumerator LevelRoutine()
        {
            Begin();
            var cheat = OzCheatSpawner.I; var game = ZGame.I; var W = game.World; var hud = OpenZHud.I;
            var pi = Object.FindFirstObjectByType<PlayerInput>();
            const string pref = "oz_key_PlaceCannon";
            bool hadPref = PlayerPrefs.HasKey(pref); int savedPref = PlayerPrefs.GetInt(pref, 0);
            ZObject lent = null;
            try
            {
                if (OzCheatSpawner.Visible) cheat.Show(false);
                if (pi) pi.CancelPlace();
                OzBindings.Set(OzAction.PlaceCannon, Key.C, out _);                                         // the default C for L1-L12, so a saved P cannon key starts no placement there (put back in finally)
                Keys(); yield return Frames(3);

                // the panel's key
                yield return Tap(Key.P);
                Check(!OzCheatSpawner.Visible, "L1 a plain P does not open the panel");
                yield return Tap(Key.LeftCtrl, Key.P);
                Check(!OzCheatSpawner.Visible, "L2 left Ctrl + P does not open it");
                yield return Tap(Key.RightCtrl, Key.P);
                Check(!OzCheatSpawner.Visible, "L3 right Ctrl + P does not open it");
                string before = Others(hud, pi, game);
                yield return Tap(Key.LeftAlt, Key.P);
                string after = Others(hud, pi, game);
                Check(OzCheatSpawner.Visible && after == before, $"L4 left Alt + P opens it ({OzCheatSpawner.Visible}) and changes nothing else ({after})");
                yield return Tap(Key.P);
                Check(OzCheatSpawner.Visible, "L5 a plain P with the panel open leaves it open");
                yield return Tap(Key.LeftCtrl, Key.P);
                Check(OzCheatSpawner.Visible, "L6 Ctrl + P with the panel open leaves it open");
                yield return Tap(Key.LeftAlt, Key.P);
                Check(!OzCheatSpawner.Visible, "L7 left Alt + P closes it");
                yield return Tap(Key.RightAlt, Key.P);
                Check(OzCheatSpawner.Visible, "L8 right Alt + P opens it");
                yield return Tap(Key.RightAlt, Key.P);
                Check(!OzCheatSpawner.Visible, "L9 right Alt + P closes it");
                Keys(Key.LeftAlt); yield return Frames(4); Keys(Key.LeftAlt, Key.P); yield return Frames(2); Keys(Key.LeftAlt); yield return Frames(2); Keys(); yield return Frames(3);
                Check(OzCheatSpawner.Visible, "L10 Alt held first, P a few frames later: opens it");
                yield return Tap(Key.LeftAlt, Key.P);
                Check(!OzCheatSpawner.Visible, "L11 and Alt + P closes it again");
                yield return Tap(Key.LeftCtrl, Key.RightAlt, Key.P);
                bool altGrOpen = OzCheatSpawner.Visible;
                yield return Tap(Key.LeftCtrl, Key.RightAlt, Key.P);
                Check(altGrOpen && !OzCheatSpawner.Visible, $"L12 AltGr + P (Windows sends left Ctrl + right Alt) opens ({altGrOpen}) and closes it like right Alt");

                // the cannon key
                ZObject b = null;
                if (pi)
                {
                    foreach (var o in W.Objects) if (o.Kind == ZKind.Building && o.Owner == game.player && !o.Destroyed && o.StoredCannons.Count > 0) { b = o; break; }
                    if (b == null)
                        foreach (var o in W.Objects) if (o.Kind == ZKind.Building && o.Owner == game.player && !o.Destroyed) { b = o; o.StoredCannons.Add("gatling"); lent = o; break; }
                }
                if (!pi || b == null) Check(false, "L13-L19 " + (!pi ? "no PlayerInput in this scene" : "the player owns no building (a stored gun is needed)"));
                else
                {
                    notes.Append($"\n  (stored gun in {b.Type} #{b.Id}{(lent != null ? ", a gatling lent for the check" : "")})");

                    OzBindings.Set(OzAction.PlaceCannon, Key.P, out _);
                    pi.CancelPlace(); yield return Frames(2);
                    yield return Tap(Key.P);
                    Check(pi.Placing != null && !OzCheatSpawner.Visible, $"L13 the cannon key bound to P: a plain P starts the placement (placing {pi.Placing != null}) and the panel stays shut ({!OzCheatSpawner.Visible})");
                    pi.CancelPlace(); yield return Frames(2);
                    yield return Tap(Key.LeftAlt, Key.P);
                    Check(OzCheatSpawner.Visible && pi.Placing == null, $"L14 the cannon key on P: Alt + P opens the panel ({OzCheatSpawner.Visible}) and starts no placement ({pi.Placing == null})");
                    yield return Tap(Key.LeftAlt, Key.P);
                    Check(!OzCheatSpawner.Visible && pi.Placing == null, "L15 Alt + P closes it, still no placement");
                    yield return Tap(Key.LeftCtrl, Key.P);
                    Check(pi.Placing == null && !OzCheatSpawner.Visible, "L16 the cannon key on P: Ctrl + P starts no placement and opens no panel");

                    OzBindings.Set(OzAction.PlaceCannon, Key.C, out _);
                    pi.CancelPlace(); yield return Frames(2);
                    yield return Tap(Key.C);
                    Check(pi.Placing != null && !OzCheatSpawner.Visible, "L17 the default C: a plain C starts the placement, as in 1.2");
                    pi.CancelPlace(); yield return Frames(2);
                    yield return Tap(Key.LeftCtrl, Key.C);
                    Check(pi.Placing == null && !OzCheatSpawner.Visible, "L18 Ctrl + C (ALL CANNONS) starts no placement and opens no panel, as in 1.2");
                    pi.CancelPlace(); yield return Frames(2);
                    yield return Tap(Key.LeftAlt, Key.C);
                    Check(pi.Placing == null && !OzCheatSpawner.Visible, "L19 Alt + C starts no placement (the cannon key skips Alt now, like every other plain key)");
                }
            }
            finally
            {
                Keys();
                if (pi) pi.CancelPlace();
                if (lent != null) { int i = lent.StoredCannons.LastIndexOf("gatling"); if (i >= 0) lent.StoredCannons.RemoveAt(i); }
                if (hadPref) PlayerPrefs.SetInt(pref, savedPref); else PlayerPrefs.DeleteKey(pref);
                PlayerPrefs.Save(); OzBindings.Reload();
                if (OzCheatSpawner.Visible && OzCheatSpawner.I) OzCheatSpawner.I.Show(false);
            }
            Check(PlayerPrefs.HasKey(pref) == hadPref && (!hadPref || PlayerPrefs.GetInt(pref, 0) == savedPref),
                  $"L20 the cannon key's saved value is back as it was (now {OzBindings.Cap(OzAction.PlaceCannon)})");
            End("level play");
        }

        // ------------------------------------------------------------------ Play, the Unit editor
        static IEnumerator UnitEditorRoutine()
        {
            Begin();
            Keys(); yield return Frames(3);
            Check(!OzCheatSpawner.I && OzUeHooks.NoCheatPanel, "U1 no testing panel in the Unit editor (unchanged)");
            bool d0 = OzUeControls.DockOpen; int h0 = OzUeKeys.Handled;
            yield return Tap(Key.P);
            Check(OzUeControls.DockOpen != d0 && OzUeKeys.LastKey == "P" && OzUeKeys.Handled == h0 + 1, $"U2 a plain P toggles the dock (open {d0} -> {OzUeControls.DockOpen}, last key {OzUeKeys.LastKey})");
            yield return Tap(Key.P);
            Check(OzUeControls.DockOpen == d0, "U3 a plain P again puts the dock back");
            int h1 = OzUeKeys.Handled;
            yield return Tap(Key.LeftCtrl, Key.P);
            Check(OzUeControls.DockOpen == d0 && OzUeKeys.Handled == h1 && !OzCheatSpawner.I, "U4 Ctrl + P does nothing in the Unit editor: no dock change, no editor key, no panel (unchanged)");
            yield return Tap(Key.LeftAlt, Key.P);
            Check(OzUeControls.DockOpen == d0 && OzUeKeys.Handled == h1 && !OzCheatSpawner.I, "U5 Alt + P does nothing in the Unit editor: no dock change, no editor key, no panel");
            End("unit editor play");
        }
    }
}
#endif
