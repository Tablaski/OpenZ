#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 11 Play proof (EXPERIMENTAL PLAY AS BLUE; menu OpenZ/V1.2/Play As Blue: 11 Play Proof, Editor/OzPlayBlueMenu.cs). Started on
    /// the MainMenu scene in Play it walks the whole flow itself, level by level (2, 16, 17), and survives the scene loads:
    ///  M  the battlefield screen: the box OFF -> the card says "BREAK INTO THE BLUE FORT"; a click on the line turns it ON (the card: "BREAK
    ///     INTO THE RED FORT", "EXPERIMENTAL"; levels 16 / 17 on EASY or MEDIUM: the SHUTTLE block offered, not locked: DV144); the key B off and
    ///     on again; the line inside the card, apart from the tag; its tooltip over the bar, right of the commander's strip; a picture; DEPLOY.
    ///  S  the level's start: blue is the human (ZGame.player, the CPU list = red only), the camera over the blue fort at the normal height,
    ///     no Commander Zod cameo (no briefing, no flyover), the colours the sim's own (the option not used), the words turned
    ///     ("BREAK INTO THE RED FORT"), the HUD's player blue and its mission banner, blue units drawn blue and selectable, a red unit never
    ///     stays selected, a blue unit takes the player's order; no ship by the red fort, the box's ship by the blue fort (DV144);
    ///     pictures (the start view, the own units selected).
    ///  R  the red CPU plays: its orders, a red unit on an attack or a fort break-in (the game at 4x until then, at most 75 s); a picture of it.
    ///  W  the red fort falls (the rules' own fort death): the end screen says the blue forces hold the level (VICTORY), no Zod; a picture.
    ///  L  REPLAY (the level again, still blue): the blue fort falls: "YOUR FORT HAS FALLEN · RED HOLDS ..." (DEFEAT); a picture; MAIN MENU
    ///     (the mode ends there, the box stays on).
    /// At the end the box is put back as it was. Started in a level that was deployed with the box ON it runs S, R, W and L there only.
    /// Counted PASS / FAIL lines and the pictures in <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzPlayBlueProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzPlayBlueProof I { get; private set; }
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", "play_as_blue_k11");
            return System.IO.Path.Combine(Application.persistentDataPath, "play_as_blue_k11");
        }
        public static void Run()
        {
            if (I) { Debug.LogWarning("[OzPlayBlueProof] already running"); return; }
            Report = "running"; var go = new GameObject("OzPlayBlueProof"); DontDestroyOnLoad(go); go.AddComponent<OzPlayBlueProof>();
        }

        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir; bool boxWas, started, putBack;
        static readonly string[] Order = { "L02", "L16", "L17" };

        void Awake() { I = this; }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (started && !putBack) { OzPlayBlue.Box = boxWas; Debug.Log($"[OzPlayBlueProof] stopped before its end: the box put back {(boxWas ? "ON" : "OFF")}"); }   // Play stopped mid-run: never leave the box on for the next proofs
        }
        void Start() { dir = Folder(); System.IO.Directory.CreateDirectory(dir); boxWas = OzPlayBlue.Box; started = true; StartCoroutine(All()); }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzPlayBlueProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            string p = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p); Log("   picture " + name + ".png");
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        IEnumerator All()
        {
            var menu = FindFirstObjectByType<OpenZMainMenu>();
            if (menu)
                foreach (var lv in Order)
                {
                    menu = FindFirstObjectByType<OpenZMainMenu>();
                    if (!menu) { yield return Until(() => FindFirstObjectByType<OpenZMainMenu>(), 30f); menu = FindFirstObjectByType<OpenZMainMenu>(); }
                    if (!menu) { Check("back on the main menu before " + lv, false); break; }
                    yield return Menu(menu, lv);
                    yield return Until(() => ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 90f);
                    if (!ZGame.I) { Check(lv + ": the level loaded after DEPLOY", false); break; }
                    yield return Level(lv, true);
                }
            else if (ZGame.I && ZGame.I.World != null) yield return Level(ZGame.I.level, false);
            else Check("started on the MainMenu scene or in a level deployed with the box ON", false);
            OzPlayBlue.Box = boxWas; putBack = true;
            Log($"the box put back: {(boxWas ? "ON" : "OFF")}");
            Report = $"[OzPlayBlueProof] {(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks; pictures in {dir}";
            Log(Report);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ M: the battlefield screen
        IEnumerator Menu(OpenZMainMenu m, string lv)
        {
            yield return Real(1f);
            m.ProofSelect(); yield return Real(0.8f);
            int card = System.Array.FindIndex(OzLevelDef.All, d => d.Id == lv); m.ProofFocus(card); yield return Real(0.6f);
            if (OzPlayBlue.Box) { m.ProofTogglePlayBlue(false); yield return Real(0.3f); }
            string off = m.ProofPlayBlueText;
            Check($"{lv} menu, the box OFF: the card names the blue fort ({Plain(off)})", Plain(off).Contains("BLUE FORT") || (TeamColors.Swap && Plain(off).Contains("RED FORT")));
            int t0 = OpenZMainMenu.PlayBlueToggles;
            m.ProofTogglePlayBlue(false); yield return Real(0.4f);
            string on = m.ProofPlayBlueText; bool hard = OpenZDifficulty.Level == GameDifficulty.Hard;
            bool shuttleOk = !OzShuttleStart.BoxLevel(lv) || hard || !on.Contains(OzPlayBlue.ShuttleLockedRow);   // 1.2 item 111 DV144: the SHUTTLE block is offered to blue too
            Check($"{lv} menu, a click turns it ON: saved, the card \"{Plain(on)}\"", OzPlayBlue.Box && OpenZMainMenu.PlayBlueToggles == t0 + 1 && Plain(on).Contains("RED FORT") && Plain(on).Contains(OzPlayBlue.Experimental) && shuttleOk);
            m.ProofTogglePlayBlue(true); yield return Real(0.3f); bool offByKey = !OzPlayBlue.Box;
            m.ProofTogglePlayBlue(true); yield return Real(0.3f);
            Check($"{lv} menu, the key B turns it off and on again", offByKey && OzPlayBlue.Box);
            var line = m.ProofPlayBlueLine; var hot = m.ProofPlayBlueHot; var cardRt = m.ProofCardRt;
            bool inside = line && hot && cardRt && Inside(hot, cardRt) && m.ProofPlayBlueGap >= 6f;
            Check($"{lv} menu, the line sits on the card's tag line, inside the card, {m.ProofPlayBlueGap:0.0} u from OBJECTIVE", inside);
            m.ProofPlayBlueTipHover(true); yield return Real(0.45f);
            var tip = m.ProofPlayBlueTip; bool tipOk = tip && tip.gameObject.activeInHierarchy && Left(tip) >= m.ProofStripRight + 5f && Bottom(tip) <= OzDeployBar.TipBottom + 0.5f;
            yield return Shot($"menu_{lv}_box_on_tooltip");
            m.ProofPlayBlueTipHover(false); yield return Real(0.4f);
            Check($"{lv} menu, the tooltip opens over the bar, right of the commander's strip", tipOk);
            yield return Shot($"menu_{lv}_box_on");
            m.ProofDeploy();
            Log($"{lv}: DEPLOY with the box ON");
        }

        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");
        static Rect CanvasRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            float k = OzLayout.RefH / Mathf.Max(1f, Screen.height);
            return Rect.MinMaxRect(c[0].x * k, (Screen.height - c[1].y) * k, c[2].x * k, (Screen.height - c[0].y) * k);
        }
        static bool Inside(RectTransform a, RectTransform b) { var x = CanvasRect(a); var y = CanvasRect(b); return x.xMin >= y.xMin - 0.5f && x.xMax <= y.xMax + 0.5f && x.yMin >= y.yMin - 0.5f && x.yMax <= y.yMax + 0.5f; }
        static float Left(RectTransform a) => CanvasRect(a).xMin;
        static float Bottom(RectTransform a) => CanvasRect(a).yMax;

        // ------------------------------------------------------------------------------------------------ S, R, W, L: the level
        IEnumerator Level(string lv, bool flow)
        {
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 4f, 30f);
            yield return Real(1.5f);
            var g = ZGame.I; var W = g.World; var rts = FindFirstObjectByType<RtsCamera>(); var input = FindFirstObjectByType<PlayerInput>();
            Log($"{lv}: {OzPlayBlueLevel.Status()}");
            Check($"{lv}: the human commands BLUE, the CPU drives RED only (blueBot {g.blueBot}, redBot {g.redBot})", OzPlayBlue.Active && g.player == Team.Blue && W.Bots.Count == 1 && W.Bots[0].Team == Team.Red && !g.blueBot && g.redBot);
            Check($"{lv}: no Commander Zod cameo (briefing, flyover, transmission window)", !ZodBriefing.I && !ZodBriefing.BriefingActive);
            var lvl = OzPlayBlueLevel.I; Vector3 bf = Vector3.zero; bool hasFort = false;
            foreach (var o in W.Objects) if (o.IsFort && o.Owner == Team.Blue) { bf += g.CenterWorld(o, false); hasFort = true; }
            int nf = W.Objects.Count(o => o.IsFort && o.Owner == Team.Blue); if (nf > 0) bf /= nf;
            float camToFort = rts ? Vector2.Distance(new Vector2(rts.focus.x, rts.focus.z), new Vector2(bf.x, bf.z)) : 999f;
            Check($"{lv}: the camera starts over the blue fort ({camToFort:0.0} m from its middle, height {(rts ? rts.height : 0f):0} m)", hasFort && lvl && lvl.Placed && camToFort < 30f && rts && Mathf.Abs(rts.height - 70f) < 1f);
            Check($"{lv}: the sim's own colours (the colour option not used) and the words turned for blue", !TeamColors.Swap && TeamColors.WordsFlip && TeamColors.Words("BREAK INTO THE BLUE FORT") == "BREAK INTO THE RED FORT");
            var hud = OpenZHud.I; string banner = Texts("Foot", "MissionBanner").FirstOrDefault() ?? "", goal = Texts("Line", "MissionBanner").FirstOrDefault() ?? "";
            Check($"{lv}: the HUD's player is blue; the mission banner \"{goal}\" / \"{banner}\"", hud && hud.Player == Team.Blue && banner.Contains("YOU COMMAND " + TeamColors.Name(Team.Blue)) && goal.Contains("DESTROY THE RED FORT"));
            int blueViews = 0, bluePainted = 0;
            foreach (var kv in g.Views)
            {
                var v = kv.Value; if (!v || v.Obj == null || v.Obj.Owner != Team.Blue || !v.Obj.IsMobile) continue;
                var tp = v.GetComponentInChildren<TeamPaint>(true); if (!tp) continue;                              // robots and vehicles with a team paint
                blueViews++; if (tp.Team == Team.Blue) bluePainted++;
            }
            bool blueIsBlue = TeamColors.Of(Team.Blue) == TeamColors.Blue && TeamColors.Of(Team.Red) == TeamColors.Red;
            Check($"{lv}: blue units drawn blue ({bluePainted} of {blueViews} blue robots / vehicles with a team paint; the palette not swapped: {blueIsBlue})", blueViews > 0 && bluePainted == blueViews && blueIsBlue);
            var own = W.OwnedSelectable(Team.Blue).Where(o => o.IsMobile).ToList(); var red = W.Objects.FirstOrDefault(o => o.Owner == Team.Red && o.IsMobile && !o.Destroyed);
            input.Selection.Clear(); input.Selection.AddRange(own); if (red != null) input.Selection.Add(red);
            yield return null; yield return null;
            Check($"{lv}: the own units stay selected ({input.Selection.Count(o => o.Owner == Team.Blue)} blue), the red unit does not", own.Count > 0 && input.Selection.All(o => o.Owner == Team.Blue) && input.Selection.Count > 0);
            var mover = own.FirstOrDefault(o => !o.IsShuttle);
            bool took = mover != null && W.SendWaypoints(g.player, mover, new[] { ZWaypoint.Move(mover.CenterX + 48, mover.CenterY - 48, true) });
            Check($"{lv}: a blue unit takes the player's order ({(mover != null ? mover.Type : "none")})", took);
            Check($"{lv}: no ship by the red fort; the player's ship {(W.PlayableShuttle != null ? "#" + W.PlayableShuttle.Id + " " + W.PlayableShuttle.Owner : "none")} is the one by the blue fort (DV144)", W.L17FortShuttle == null && (W.PlayableShuttle == null || (W.PlayableShuttle == W.BlueStartShuttle && W.PlayableShuttle.Owner == Team.Blue)));
            yield return Shot($"level_{lv}_start_h70");
            if (rts && mover != null) { rts.focus = new Vector3(g.CenterWorld(mover, false).x, 0f, g.CenterWorld(mover, false).z); rts.SetHeightImmediate(70f); }
            yield return Real(0.5f); yield return Shot($"level_{lv}_own_selected_h70");
            input.Selection.Clear();
            // R: the red CPU plays
            var bot = W.Bots[0]; float keep = g.gameSpeed; g.gameSpeed = 4f; ZObject attacker = null;
            yield return Until(() =>
            {
                foreach (var o in W.Objects) if (o.Owner == Team.Red && o.IsMobile && !o.Destroyed && o.Waypoints.Count > 0 && (o.Waypoints[0].mode == WpMode.Attack || o.Waypoints[0].mode == WpMode.EnterFort)) { attacker = o; return true; }
                return false;
            }, 75f);
            g.gameSpeed = keep;
            Check($"{lv}: the red CPU plays: {bot.Orders} orders by {W.Time:0} s, a red unit on an attack ({(attacker != null ? attacker.Type + " " + attacker.Waypoints[0].mode : "none")})", bot.Orders > 0 && attacker != null);
            if (rts && attacker != null) { var p = g.CenterWorld(attacker, false); rts.focus = new Vector3(p.x, 0f, p.z); rts.SetHeightImmediate(70f); yield return Real(0.6f); yield return Shot($"level_{lv}_red_attacks_h70"); }
            // W: the red fort falls
            OpenZVictory.DebugFortFalls(true);
            yield return Until(() => hud && hud.EndScreen && hud.EndScreen.IsShown, 60f);
            yield return Real(2.5f);
            string sub = Texts("Subtitle").FirstOrDefault(t => t.Contains("HOLD")) ?? "";
            Check($"{lv}: the red fort fell: {W.Winner} won, the end screen \"{sub}\" (VICTORY for blue), no Zod", W.GameOver && W.Winner == Team.Blue && OzSides.Outcome(W.Winner, g.player) == "victory" && sub.Contains(TeamColors.Name(Team.Blue) + " FORCES HOLD") && !ZodBriefing.I);
            yield return Shot($"level_{lv}_victory");
            // L: REPLAY, the blue fort falls
            OzScenes.Restart();
            yield return Until(() => ZGame.I && ZGame.I != g && ZGame.I.World != null, 60f);
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 4f, 30f);
            yield return Real(1f);
            g = ZGame.I; W = g ? g.World : null; hud = OpenZHud.I;
            Check($"{lv}: REPLAY keeps the side (human {(g ? g.player.ToString() : "none")})", g && g.player == Team.Blue && OzPlayBlue.Active);
            if (g && W != null)
            {
                OpenZVictory.DebugFortFalls(false);
                yield return Until(() => hud && hud.EndScreen && hud.EndScreen.IsShown, 60f);
                yield return Real(2.5f);
                sub = Texts("Subtitle").FirstOrDefault(t => t.Contains("HOLDS")) ?? "";
                Check($"{lv}: the blue fort fell: {W.Winner} won, the end screen \"{sub}\" (DEFEAT for blue), no Zod", W.GameOver && W.Winner == Team.Red && OzSides.Outcome(W.Winner, g.player) == "defeat" && sub.Contains(TeamColors.Name(Team.Red) + " HOLDS") && !ZodBriefing.I);
                yield return Shot($"level_{lv}_defeat");
            }
            if (flow)
            {
                OzScenes.MainMenu();
                yield return Until(() => FindFirstObjectByType<OpenZMainMenu>(), 60f);
                yield return Real(0.5f);
                Check($"{lv}: back on the main menu the mode has ended, the box stays on", !OzPlayBlue.Active && OzPlayBlue.Box);
            }
        }

        /// <summary>The texts of the scene's labels called <paramref name="name"/> (or inside an object of that name; TextMeshPro and the
        /// legacy text; hidden ones too), inside an object called <paramref name="parent"/> when one is given.</summary>
        static IEnumerable<string> Texts(string name, string parent = null)
        {
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && Under(t.transform, name, true) && (parent == null || Under(t.transform, parent, false))) yield return t.text ?? "";
            foreach (var t in FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && Under(t.transform, name, true) && (parent == null || Under(t.transform, parent, false))) yield return t.text ?? "";
        }
        /// <summary>The transform (self: also itself) or one of its parents up to three levels is called n.</summary>
        static bool Under(Transform t, string n, bool self)
        {
            var p = self ? t : t.parent;
            for (int i = 0; i < 4 && p; i++, p = p.parent) if (p.name == n) return true;
            return false;
        }
    }
}
#endif
