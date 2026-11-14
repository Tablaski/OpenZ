using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of the INCOMING CALL portrait. Menu: OpenZ/Checks/V1.3/Help call portrait hold (Play,
    /// a running level). The calls are staged with AudioDirector.NoteHelpCall (the director's own hook for probes) on the player's own
    /// units; two units are killed through the rules (as CannonExitCheck does); nothing is saved. Nothing must be selected at the start.
    ///  C1  one call, no new one: on screen at once, still on screen at 12 s, gone by 14.6 s; reason "ran out", shown 13.8 .. 14.8 s.
    ///  C2  a second caller 1 s after the first: the first stays at 1.2, 3 and 4.8 s; the second has the screen by 5.4 s; the first's end
    ///      "a new caller", shown at least 5 s.
    ///  C3  click 10 s after the call (later than the old 8 s): selected, the camera flies there (its focus within 4 m in 3 s).
    ///  C4  the caller's whole squad dies 1 s after its call: SIGNAL LOST at once (the name line, a counted cut), still at 2.0 s, gone
    ///      by 2.9 s; a click on it selects nothing.
    ///  C5  only a squad's leader dies: the call goes on with the squad's next member (no SIGNAL LOST, no new cut).
    ///  C6  a selection ends the call at once (reason "a selection").
    /// The real-game count (the calls under fire in CPU games, from the "[OpenZ HUD] incoming call ended" lines) is part B in SOLUTION.md.
    /// </summary>
    public static class OzV13CallPortraitCheck
    {
        const string Tag = "[OpenZ call portrait]";
        public static string Report = "not run";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int ok, bad, waitFrame; static float waitUntil;
        static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();
        static string dir;

        [MenuItem("OpenZ/Checks/V1.3/Help call portrait hold (Play, a running level)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode in a level first"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var hud = OpenZHud.I;
            if (!hud || hud.W == null || hud.W.GameOver || !hud.hudVisible) { Report = Tag + " FAIL: a running match with the HUD shown is needed"; Debug.LogError(Report); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); Report = "running";
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item22_call_portrait"));
            Directory.CreateDirectory(dir);
            stack.Push(All(hud));
            waitFrame = 0; waitUntil = 0f; EditorApplication.update += Step;
        }

        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); Report = Tag + " FAIL: exception " + ex.Message; Debug.LogError(Report); EditorApplication.update -= Step; return; }
            if (!more) { stack.Pop(); if (stack.Count == 0) EditorApplication.update -= Step; return; }
            if (top.Current is IEnumerator child) stack.Push(child);
            else if (top.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (top.Current is int n ? n : 1);
        }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            string l = $"{Tag} {(pass ? "ok  " : "FAIL")} {what}"; lines.AppendLine(l); Debug.Log(l);
        }

        /// <summary>Waits until `t` real seconds after t0.</summary>
        static IEnumerator At(float t0, float t) { while (Time.unscaledTime - t0 < t) yield return 1; }

        /// <summary>Waits until no call shows any more (a clean start for the next case).</summary>
        static IEnumerator Quiet(OpenZHud hud)
        {
            hud.input.Selection.Clear();
            float t0 = Time.unscaledTime;
            while ((hud.CallShown != null || hud.CallLostOnScreen) && Time.unscaledTime - t0 < OpenZHud.CallShowSec + 4f) yield return 5;
            yield return 0.3f;
        }

        static void Kill(ZWorld W, ZObject o, bool squad)
        {
            if (o == null || o.Destroyed) return;
            if (squad) foreach (var m in new List<ZObject>(o.Minions)) if (!m.Destroyed) { m.DamageHealth(m.Health + 1); W.UpdateObjectHealth(m, -1); }
            o.DamageHealth(o.Health + 1); W.UpdateObjectHealth(o, -1);
        }

        static string Name(ZObject o) => o == null ? "none" : o.Type + " #" + o.Id;

        static IEnumerator All(OpenZHud hud)
        {
            var W = hud.W;
            // the player's units for the cases: squad leaders with at least one member (C2 needs two units, C4 / C5 a squad each)
            var leaders = W.Objects.Where(o => o.IsUnit && o.Owner == hud.Player && !o.Destroyed && o.KillAt < 0 && o.Kind == ZKind.Robot && o.Leader == null).ToList();
            var squads = leaders.Where(o => o.Minions.Count > 0).ToList();
            if (leaders.Count < 2 || squads.Count < 2) { Check(false, $"setup: the player needs 2 robot groups, 2 of them squads with members (found {leaders.Count} groups, {squads.Count} squads): start on L02 or L17"); Finish(hud); yield break; }
            ZObject a = leaders[0], b = leaders.First(o => o != a);
            ZObject sqLost = squads.Last(), sqLead = squads.First(o => o != sqLost);
            if (a == sqLost || a == sqLead) a = leaders.FirstOrDefault(o => o != sqLost && o != sqLead) ?? a;
            if (b == sqLost || b == sqLead || b == a) b = leaders.FirstOrDefault(o => o != sqLost && o != sqLead && o != a) ?? b;
            Check(a != b, $"setup: callers {Name(a)} and {Name(b)}, the squad that dies {Name(sqLost)} ({sqLost.Minions.Count} members), the squad that loses its leader {Name(sqLead)}");
            yield return Quiet(hud);

            // ---- C1 the hold after one call
            float t0 = Time.unscaledTime; int ends = hud.CallEnds;
            AudioDirector.NoteHelpCall(a.Id); yield return 3;
            bool atOnce = hud.CallShown == a;
            yield return At(t0, 12f); bool at12 = hud.CallShown == a;
            yield return At(t0, 14.6f); bool gone = hud.CallShown == null;
            Check(atOnce && at12 && gone && hud.CallEnds == ends + 1 && hud.CallEndWhy.Contains("ran out") && hud.CallShownSec > 13.8f && hud.CallShownSec < 14.8f,
                  $"C1 one call ({Name(a)}): on screen at once {atOnce}, at 12 s {at12}, gone by 14.6 s {gone}; ended \"{hud.CallEndWhy}\" after {hud.CallShownSec:0.0} s");
            yield return Quiet(hud);

            // ---- C2 a new caller cannot knock the first one away in its first 5 s
            t0 = Time.unscaledTime;
            AudioDirector.NoteHelpCall(a.Id); yield return 3;
            yield return At(t0, 1f); AudioDirector.NoteHelpCall(b.Id);
            yield return At(t0, 1.2f); bool k1 = hud.CallShown == a;
            yield return At(t0, 3f); bool k3 = hud.CallShown == a;
            yield return At(t0, 4.8f); bool k48 = hud.CallShown == a;
            yield return At(t0, 5.4f); bool sw = hud.CallShown == b;
            Check(k1 && k3 && k48 && sw && hud.CallEndWhy.StartsWith("a new caller") && hud.CallShownSec >= 5f,
                  $"C2 a second caller ({Name(b)}) 1 s after the first: the first kept at 1.2 / 3 / 4.8 s {k1} / {k3} / {k48}; the second by 5.4 s {sw} (shown: {Name(hud.CallShown)}); the first ended \"{hud.CallEndWhy}\" after {hud.CallShownSec:0.0} s");
            yield return Quiet(hud);

            // ---- C3 still clickable late: 10 s after the call
            t0 = Time.unscaledTime; int clicks = hud.CallClicks;
            AudioDirector.NoteHelpCall(a.Id); yield return 3;
            yield return At(t0, 10f);
            bool up = hud.CallShown == a;
            hud.ProofClickCall(); yield return 3f;
            Vector3 u = hud.game.CenterWorld(a), f = hud.rts ? hud.rts.focus : Vector3.zero;
            float d = Vector2.Distance(new Vector2(u.x, u.z), new Vector2(f.x, f.z));
            bool sel = hud.input.Selection.Contains(a);
            Check(up && hud.CallClicks == clicks + 1 && sel && d < 4f, $"C3 the click 10 s after the call: on screen {up}, clicks {hud.CallClicks - clicks}, selected {sel}, the camera's focus {d:0.0} m from the unit after 3 s");
            yield return Quiet(hud);

            // ---- C4 the caller dies: SIGNAL LOST
            t0 = Time.unscaledTime; int losts = hud.CallLosts;
            AudioDirector.NoteHelpCall(sqLost.Id); yield return 3;
            yield return At(t0, 1f);
            bool live = hud.CallShown == sqLost;
            Kill(W, sqLost, true); float tk = Time.unscaledTime; yield return 4;
            bool lostNow = hud.CallLostOnScreen && hud.CallLosts == losts + 1 && hud.CallEndWhy == "the caller was lost";
            hud.ProofClickCall(); yield return 2; bool noSel = hud.input.Selection.Count == 0;
            yield return At(tk, 2.0f); bool lost20 = hud.CallLostOnScreen;
            yield return At(tk, 2.9f); bool lostGone = !hud.CallLostOnScreen && hud.CallShown == null;
            Check(live && lostNow && noSel && lost20 && lostGone, $"C4 the caller's squad dies 1 s after its call: on screen before {live}; SIGNAL LOST at once {lostNow} (cuts {hud.CallLosts - losts}, \"{hud.CallEndWhy}\"); a click selects nothing {noSel}; still at 2.0 s {lost20}; gone by 2.9 s {lostGone}");
            yield return Quiet(hud);

            // ---- C5 only the leader dies: the squad's next member carries the call
            t0 = Time.unscaledTime; int shows = hud.CallShows; losts = hud.CallLosts;
            AudioDirector.NoteHelpCall(sqLead.Id); yield return 3;
            yield return At(t0, 1f);
            Kill(W, sqLead, false); yield return 0.5f;
            var next = sqLead.Successor;
            bool carried = next != null && hud.CallShown == next && hud.CallShows == shows + 1 && hud.CallLosts == losts && !hud.CallLostOnScreen;
            Check(carried, $"C5 only the leader {Name(sqLead)} dies: the call goes on with {Name(hud.CallShown)} (its next member {Name(next)}), cuts {hud.CallShows - shows} (1 = no new cut), SIGNAL LOST {hud.CallLosts - losts}");
            yield return Quiet(hud);

            // ---- C6 a selection ends it at once
            AudioDirector.NoteHelpCall(a.Id); yield return 3;
            bool on6 = hud.CallShown == a;
            hud.input.Selection.Clear(); hud.input.Selection.Add(b); yield return 3;
            Check(on6 && hud.CallShown == null && hud.CallEndWhy == "a selection", $"C6 a selection: on screen before {on6}, gone {hud.CallShown == null}, ended \"{hud.CallEndWhy}\"");
            hud.input.Selection.Clear();
            Finish(hud);
        }

        static void Finish(OpenZHud hud)
        {
            bool pass = bad == 0 && ok > 0;
            Report = $"{Tag} {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} checks (level {hud.game.level}, hold {OpenZHud.CallShowSec:0} s, keep {OpenZHud.CallKeepSec:0} s, SIGNAL LOST {OpenZHud.CallLostSec:0.0} s){(bad == 0 ? "" : "; FAILED:" + fails)}";
            lines.AppendLine(Report);
            if (pass) Debug.Log(Report); else Debug.LogError(Report);
            try { File.AppendAllText(Path.Combine(dir, "checks.txt"), lines.ToString()); } catch (System.Exception e) { Debug.LogWarning(Tag + " could not write checks.txt: " + e.Message); }
        }
    }
}
