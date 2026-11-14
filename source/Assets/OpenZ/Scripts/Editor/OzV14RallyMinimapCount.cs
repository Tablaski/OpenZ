using System.IO;
using System.Text;

namespace OpenZ
{
    /// <summary>
    /// 1.4 rally_minimap, the counted check in edit mode (no Play, no level): menu OpenZ/V1.4/Rally Minimap: Count (edit mode).
    /// <list type="number">
    /// <item>Every case of the minimap's clicks (the rally tool armed or not, still armed at the release or not, a build menu open or not, a modal
    /// screen or not, 0 / 1 / 3 units selected, the pointer over the map picture or not): what a left press, a left release and a right click
    /// give with the kit (UI/OzRallyMinimap.cs, the answers the minimap asks) against 1.4 as it was (the old lines of UI/OpenZMinimap.cs, written
    /// out below, with what reached them: the minimap takes no click while a build menu shows, OpenZHud.LowerPanels.cs). Wanted: with the tool not
    /// armed every answer the same; the only changed answers are the armed tool's right click with nothing selected (nothing -> the rally point).</item>
    /// <item>The armed cases against the map's own rally tool (UI/OpenZRallyPoints.cs): a left click sets the point and flies nowhere; a right
    /// click with nothing selected sets it; with units selected it is their order (PlayerInput), the tool stays armed.</item>
    /// <item>The cursor over the minimap (UI/OpenZCursors.cs) and the ghost mark: not armed as before; armed the map's armed cursor (move).</item>
    /// <item>The source: the minimap and the cursor ask these answers (each call found as often as wanted in the project's own files).</item>
    /// </list>
    /// </summary>
    public static class OzV14RallyMinimapCount
    {
        const string Tag = "[OpenZ V1.4 rally minimap count]";
        static int pass, fail, changed, armedRows, sameRows;
        static readonly StringBuilder sb = new StringBuilder();

        static void Check(bool ok, string what) { if (ok) pass++; else { fail++; sb.Append("FAIL  ").AppendLine(what); } }

        // ------------------------------------------------------------------ 1.4 as it was (UI/OpenZMinimap.cs before the kit), with what reached it
        /// <summary>The old left press: `if (hud.Rally && hud.Rally.Armed) rallyPress = true; else { dragFly = true; FlyTo(mp); }`.</summary>
        static OzRallyMinimap.Press OldLeftPress(bool armed) => armed ? OzRallyMinimap.Press.Rally : OzRallyMinimap.Press.Fly;
        /// <summary>The old release: `if (!modal && ScreenToPx(..)) hud.Rally.AddPoint(..)`; AddPoint sets nothing without OpenZRallyPoints.Building
        /// (armed: the tool's factory; else the open menu's, and with a menu open the minimap takes no click).</summary>
        static bool OldLeftRelease(bool rallyPress, bool armedNow, bool modal, bool menuOpen) => rallyPress && !modal && !menuOpen && armedNow;
        /// <summary>The old right click: selection first, else `hud.Production.Building != null` (the open menu; the minimap is hidden then).</summary>
        static OzRallyMinimap.RightAct OldRight(int selected, bool menuOpen) => selected > 0 ? OzRallyMinimap.RightAct.Order : menuOpen ? OzRallyMinimap.RightAct.Rally : OzRallyMinimap.RightAct.None;
        static bool OldMoveCursor(bool overMap, int selected) => overMap && selected > 0;

        // ------------------------------------------------------------------ the map's rally tool (UI/OpenZRallyPoints.cs, PlayerInput), armed
        /// <summary>Armed, a left click on the ground: the point (PressCapture Rally, released while armed).</summary>
        static bool MapArmedLeftSets(bool armedNow) => armedNow;
        /// <summary>Armed, a right click on the ground: nothing selected = the point (Building = the armed factory); units selected = their order.</summary>
        static OzRallyMinimap.RightAct MapArmedRight(int selected) => selected > 0 ? OzRallyMinimap.RightAct.Order : OzRallyMinimap.RightAct.Rally;

        public static string Run(string projectRoot)
        {
            pass = fail = changed = armedRows = sameRows = 0; sb.Length = 0;
            int rows = 0;
            int[] sels = { 0, 1, 3 };
            foreach (bool armed in new[] { false, true })
            foreach (bool armedNow in new[] { false, true })
            foreach (bool menuOpen in new[] { false, true })
            foreach (bool modal in new[] { false, true })
            foreach (int sel in sels)
            foreach (bool over in new[] { false, true })
            {
                if (!armed && armedNow) continue;                                                   // the tool cannot become armed during a press
                rows++;
                string at = $"armed {armed} (at the release {armedNow}), menu {menuOpen}, modal {modal}, selected {sel}, over the map {over}";
                bool reach = over && !modal && !menuOpen;                                          // the minimap takes the pointer (its own tests before the rule)

                // left press, then its release
                var pressNew = reach ? OzRallyMinimap.LeftPress(armed) : (OzRallyMinimap.Press?)null;
                var pressOld = reach ? OldLeftPress(armed) : (OzRallyMinimap.Press?)null;
                bool rallyPress = pressNew == OzRallyMinimap.Press.Rally;
                bool relNew = reach && OzRallyMinimap.LeftRelease(rallyPress, armedNow, modal);
                bool relOld = reach && OldLeftRelease(pressOld == OzRallyMinimap.Press.Rally, armedNow, modal, menuOpen);
                // right click
                bool hasFactory = armed || menuOpen;                                                // OpenZRallyPoints.Building: the armed tool's factory, else the open menu's
                var rightNew = reach ? OzRallyMinimap.RightClick(sel, hasFactory) : OzRallyMinimap.RightAct.None;
                var rightOld = reach ? OldRight(sel, menuOpen) : OzRallyMinimap.RightAct.None;
                // cursor and ghost (the cursor's own test: over the map picture and no modal screen)
                bool overMap = over && !modal && !menuOpen;
                bool curNew = OzRallyMinimap.MoveCursor(overMap, sel, armed), curOld = OldMoveCursor(overMap, sel);
                bool ghost = OzRallyMinimap.Ghost(armed, overMap);

                bool same = pressNew == pressOld && relNew == relOld && rightNew == rightOld && curNew == curOld;
                if (!armed)
                {
                    Check(same && !ghost, "tool not armed, 1.4's answers: " + at + $" (press {pressNew}/{pressOld}, release {relNew}/{relOld}, right {rightNew}/{rightOld}, cursor {curNew}/{curOld}, ghost {ghost})");
                    sameRows++;
                    continue;
                }
                armedRows++;
                if (!same) changed++;
                // the only change wanted: the armed right click with nothing selected (and the armed cursor over the map)
                bool wantRightChange = reach && sel == 0;
                Check((rightNew != rightOld) == wantRightChange, "armed, the right click changes only with nothing selected: " + at + $" ({rightOld} -> {rightNew})");
                Check(pressNew == pressOld && relNew == relOld, "armed, the left click as it was: " + at);
                Check(curNew == overMap && ghost == overMap, "armed, the map's cursor (move) and the ghost over the map: " + at);
                if (reach)
                {
                    Check(pressNew == OzRallyMinimap.Press.Rally, "armed, a left press flies nowhere (the map's tool): " + at);
                    Check(relNew == MapArmedLeftSets(armedNow), "armed, the left release sets the point as on the map: " + at);
                    Check(rightNew == MapArmedRight(sel), "armed, the right click as on the map: " + at + " -> " + rightNew);
                }
                else Check(rightNew == OzRallyMinimap.RightAct.None && !relNew, "armed, the minimap does not take the pointer, nothing: " + at);
            }

            // the source: the minimap and the cursor ask these answers
            string S(string rel) { var p = Path.Combine(projectRoot, "Assets/OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
            int N(string text, string s) { int n = 0, i = 0; while ((i = text.IndexOf(s, i, System.StringComparison.Ordinal)) >= 0) { n++; i += s.Length; } return n; }
            string mm = S("UI/OpenZMinimap.cs"), cu = S("UI/OpenZCursors.cs");
            var calls = new (string file, string text, string call, int want)[]
            {
                ("UI/OpenZMinimap.cs", mm, "OzRallyMinimap.LeftPress(", 1), ("UI/OpenZMinimap.cs", mm, "OzRallyMinimap.LeftRelease(", 1),
                ("UI/OpenZMinimap.cs", mm, "OzRallyMinimap.RightClick(", 1), ("UI/OpenZMinimap.cs", mm, "OzRallyMinimap.Factory(", 1),
                ("UI/OpenZMinimap.cs", mm, "OzRallyMinimap.Ghost(", 1), ("UI/OpenZCursors.cs", cu, "OzRallyMinimap.MoveCursor(", 1),
                ("UI/OpenZMinimap.cs", mm, "hud.Production && hud.Production.Building != null", 0),
                // review (6 Nov): the calls with their real arguments and what they lead to, so a wrong argument (the open menu's factory, a constant)
                // or a missing AddPoint cannot pass on the call's name alone
                ("UI/OpenZMinimap.cs", mm, "if (OzRallyMinimap.LeftPress(hud.Rally && hud.Rally.Armed) == OzRallyMinimap.Press.Rally) rallyPress = true;", 1),
                ("UI/OpenZMinimap.cs", mm, "if (OzRallyMinimap.LeftRelease(true, hud.Rally && hud.Rally.Armed, modal) && ScreenToPx(mp, out var rp)) { hud.Rally.AddPoint(rp.x, rp.y, shift); PingOrder(rp); }", 1),
                ("UI/OpenZMinimap.cs", mm, "var act = OzRallyMinimap.RightClick(hud.input.Selection.Count, hud.Rally && hud.Rally.Building != null);", 1),
                ("UI/OpenZMinimap.cs", mm, "if (act == OzRallyMinimap.RightAct.Order) { Order(px, kb, mp); PingOrder(px); }", 1),
                ("UI/OpenZMinimap.cs", mm, "else if (act == OzRallyMinimap.RightAct.Rally) { hud.Rally.AddPoint(px.x, px.y, shift); PingOrder(px); }", 1),
                ("UI/OpenZMinimap.cs", mm, "var open = OzRallyMinimap.Factory(hud);", 1),
                ("UI/OpenZMinimap.cs", mm, "bool ghost = OzRallyMinimap.Ghost(hud.Rally && hud.Rally.Armed, PointerOverMap) && open != null && ScreenToPx(hud.Pointer, out gp);", 1),
                ("UI/OpenZCursors.cs", cu, "if (hud.PointerOverUi) return OzRallyMinimap.MoveCursor(hud.Minimap && hud.Minimap.PointerOverMap, sel.Count, hud.Rally && hud.Rally.Armed) ? \"move\" : \"arrow\";", 1),
            };
            var src = new StringBuilder();
            foreach (var c in calls)
            {
                int n = N(c.text, c.call);
                Check(n == c.want, $"source: {c.call} in {c.file} {n} time(s), want {c.want}");
                src.Append($"  {c.file}: {(c.call.Length > 90 ? c.call.Substring(0, 90) + " ..." : c.call)} {n} (want {c.want})\n");
            }

            bool ok = fail == 0 && rows == 72 && changed == 2;
            var head = $"{Tag} {(ok ? "PASS" : "FAIL")}: {rows} cases ({sameRows} with the tool not armed: every answer as 1.4; {armedRows} armed: {changed} changed, "
                       + $"want 2 = nothing selected, over the map, still armed or not at the release: the right click sets the point and the cursor is the map's armed one); {pass} checks passed, {fail} failed\n";
            string report = head + "source:\n" + src + (sb.Length > 0 ? sb.ToString() : "");
            return report;
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Rally Minimap: Count (edit mode)")]
        static void Menu()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string report = Run(root);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/rally_minimap/proof"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "count_report.txt"), report);
                report += "written: " + Path.Combine(dir, "count_report.txt").Replace('\\', '/') + "\n";
            }
            catch (System.Exception e) { report += "report file not written: " + e.Message + "\n"; }
            if (report.Contains(" PASS:")) UnityEngine.Debug.Log(report); else UnityEngine.Debug.LogWarning(report);
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Rally Minimap: Play Check (a level playing, real clicks)")]
        static void PlayMenu()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.4 rally minimap play] start a level in Play first (normal mode, the player's fort standing), the Game view in front"); return; }
            OzRallyMinimapCheck.Folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/rally_minimap/proof/play"));
            OzRallyMinimapCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Rally Minimap: Play Check, last report")]
        static void PlayLast() => UnityEngine.Debug.Log(OzRallyMinimapCheck.Running ? "[OpenZ V1.4 rally minimap play] running" : OzRallyMinimapCheck.Report);
#endif
    }
}
