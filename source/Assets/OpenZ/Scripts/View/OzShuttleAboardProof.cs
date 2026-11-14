using System.Collections;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 2 Play proof: who sits in a shuttle and how many, on its card and on its name chip over it (UI/OzShuttleHud.cs,
    /// UI/OpenZHud.ShuttleAboard.cs, UI/OpenZUnitOverlay.ShuttleAboard.cs), on level 16 in Play (menu OpenZ/V1.2/Shuttle: Who Is Aboard
    /// Proof, Editor/OzShuttleAboardMenu.cs). Counted PASS / FAIL lines and full-screen pictures from the normal RTS camera (70 m; also the
    /// closest 25 m and the widest 175 m); each picture's card and unit rects (px, top-left origin) go to shots.tsv for the crops.
    /// Set-up: the CPU teams removed, morning (day clock 0.10, held). The game's pointer is moved by queued input events inside the game
    /// only: no desktop input. Round 3: who is aboard
    /// shows once on the card (its type chip; no strip on the portrait, no faces in the stat slot, which reads PILOT), and over the ship on
    /// its own name chip above the health bar (nothing under the bar); the chip's gap to the bar is the one a grunt squad's and a light
    /// tank's name chip have (measured, within 1 px); its words fit. Stages:
    ///  S the siblings: a grunt squad and a light tank, each selected with the pointer on it (day; night in C): their bar-to-chip gaps;
    ///  A an empty neutral ship: no chip with the pointer away, the plain name chip with the pointer on it;
    ///  D ALAN &amp; BRAD ordered into it: selected with the pointer on it, selected only (the chip stays: someone aboard), not selected;
    ///  B an own ship with its pilot only: selected with the pointer on it (the plain chip; the card's chip EMPTY, PILOT STAYS IN), selected only;
    ///  C a grunt squad of 3 ordered into it: selected with the pointer on it at 70, 25 and 175 m, two ships selected, not selected, at night,
    ///    flying at 70, 25 and 175 m, flying not selected.
    /// <see cref="Folder"/>
    /// </summary>
    public sealed class OzShuttleAboardProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "staging")))
                    return System.IO.Path.Combine(d.FullName, "work", "staging", "v1_2_20261022", "shuttle_occupants", "shots");
            return System.IO.Path.Combine(Application.persistentDataPath, "shuttle_occupants");
        }
        public static void Run() { Report = "running"; new GameObject("OzShuttleAboardProof").AddComponent<OzShuttleAboardProof>(); }

        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; DayNight dn; OpenZHud hud; OpenZUnitOverlay overlay; Team me = Team.Red;
        readonly StringBuilder sb = new StringBuilder(), tsv = new StringBuilder(); int fails, passes, frames; string dir;
        float gapGrunt = -1f, gapTank = -1f;

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>();
            hud = OpenZHud.I; overlay = FindFirstObjectByType<OpenZUnitOverlay>();
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            tsv.AppendLine("name\tcard_x\tcard_y\tcard_w\tcard_h\tship_x\tship_y\tship_w\tship_h\tline\taboard\tstrip\tpill");
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzShuttleAboardProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        IEnumerator WaitFor(System.Func<bool> c, float max) { float t = 0f; while (t < max && !c()) { t += Time.deltaTime; yield return null; } }
        IEnumerator Wait(float s0) { float t = 0f; while (t < s0) { t += Time.deltaTime; yield return null; } }
        void Cam(Vector3 focus, float h) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(focus.x, 0f, focus.z); rts.SetHeightImmediate(h); }
        /// <summary>The camera at height h on the drawn ship's middle, in the air too (its focus where the view through it meets the ground).</summary>
        void CamOn(OzPlayableShuttle sv, float h)
        {
            if (!rts || !sv) return;
            var p = sv.transform.TransformPoint(new Vector3(0f, 2.8f, 0f));
            Cam(p, h);
            var f = rts.transform.forward; float up = p.y - g.GroundY(p.x, p.z);
            if (f.y < -0.05f && up > 0.5f) { var gp = p + f * (up / -f.y); Cam(gp, h); }
        }
        ZObject Squad(string type, int tx, int ty, int amount = -1)
        {
            OzCheatSpawner.FreeTile(W, tx, ty, 1, true, out tx, out ty);
            var o = W.CreateRobotGroup(type, tx * 16, ty * 16, me, amount); if (o == null) return null;
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            return o;
        }
        ZObject Unit(string type, int tx, int ty)
        {
            OzCheatSpawner.FreeTile(W, tx, ty, 2, false, out tx, out ty);
            var o = W.CreateObject(ZBuildLists.IsCannon(type) ? "cannon" : "vehicle", type, tx * 16, ty * 16, me);
            if (o != null) g.SpawnUnitView(o);
            return o;
        }
        bool Move(ZObject o, int x, int y) => W.SendWaypoints(me, o, new[] { new ZWaypoint { mode = WpMode.Move, x = x, y = y, playerGiven = true } });
        bool Enter(ZObject sq, ZObject ship0) => sq != null && W.SendWaypoints(me, sq, new[] { new ZWaypoint { mode = WpMode.Enter, refId = ship0.Id, x = ship0.CenterX, y = ship0.CenterY, playerGiven = true } });
        void Select(ZObject o) { input.Selection.Clear(); if (o != null) input.Selection.Add(o); }
        Rect ScreenRectOf(ZObject o) => o.IsShuttle && input.MeshRect(o, out var r) ? r : input.ScreenRect(o);
        void PointAt(ZObject o) { if (Mouse.current != null && o != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = ScreenRectOf(o).center }); }
        void PointAway() { if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(Screen.width * 0.3f, Screen.height * 0.52f) }); }
        IEnumerator Board(ZObject sq, ZObject s, float max)
        {
            int p0 = s.Passengers.Count, d0 = s.Drivers.Count;
            if (!Enter(sq, s)) { Log("the ENTER order was refused"); yield break; }
            yield return WaitFor(() => s.Passengers.Count > p0 || s.Drivers.Count > d0, max);
            yield return WaitFor(() => s.ShuttleRampPhase == ZShuttleRamp.Shut && W.Time >= s.ShuttleRampHoldUntil && s.ShuttleCrewSettled, 25f);
            yield return Wait(0.8f);
        }
        /// <summary>The camera on the ship at height h, the pointer kept on it (it moves in the air) or away, for t s.</summary>
        IEnumerator Follow(OzPlayableShuttle sv, ZObject s, float h, float t, bool point)
        {
            yield return WaitFor(() => { CamOn(sv, h); if (point) PointAt(s); else PointAway(); return false; }, t);
            CamOn(sv, h); if (point) PointAt(s);
        }
        IEnumerator FollowUnit(ZObject u, float h, float t, bool point)
        {
            yield return WaitFor(() => { Cam(ZGame.PxToWorld(u.CenterX, u.CenterY), h); if (point) PointAt(u); else PointAway(); return false; }, t);
            if (point) PointAt(u);
        }

        int[] Spot(int cx, int cy, int minR, int maxR, ZObject self)
        {
            for (int r = minR; r <= maxR; r += 16)
                for (int a = 0; a < 32; a++)
                {
                    double t = a * System.Math.PI / 16;
                    var q = W.ShuttleClamp(cx + (int)(System.Math.Cos(t) * r), cy + (int)(System.Math.Sin(t) * r));
                    if (W.ShuttlePadFree(q.x, q.y, self, out _)) return new[] { q.x, q.y };
                }
            return null;
        }

        static readonly Vector3[] corners = new Vector3[4];
        static Rect Px(RectTransform rt)
        {
            if (!rt) return default;
            rt.GetWorldCorners(corners);                                                                // a screen-space overlay canvas: corners in screen px
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        Transform OverlayRoot => hud.transform.Find("OpenZ_UnitOverlay/Root");

        /// <summary>The unit's own name chip shown this frame (the overlay mark round the unit with its chip on) and that mark's health bar
        /// (bottom-left origin px).</summary>
        bool ChipOf(ZObject u, out Rect bar, out Rect chip, out RectTransform chipRt)
        {
            bar = chip = default; chipRt = null;
            var root = OverlayRoot; if (!root || u == null) return false;
            var ur = ScreenRectOf(u); float best = 0.5f * Mathf.Max(ur.width, ur.height) + 30f; best *= best;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i); if (c.name != "Mark" || !c.gameObject.activeInHierarchy) continue;
                var lt = c.Find("Label"); var bt = c.Find("Bar");
                if (!lt || !lt.gameObject.activeSelf) continue;
                float d = (Px((RectTransform)c).center - ur.center).sqrMagnitude;
                if (d >= best) continue;
                best = d; chipRt = (RectTransform)lt; chip = Px(chipRt); bar = bt && bt.gameObject.activeSelf ? Px((RectTransform)bt) : default;
            }
            return chipRt != null && bar.width > 0f;
        }

        /// <summary>The chip's words and faces as drawn lie inside it (1 px kept clear at its right end).</summary>
        bool ChipFits(RectTransform chipRt, out string info)
        {
            info = "no chip"; if (!chipRt) return false;
            var cr = Px(chipRt); bool ok = true; var s = new StringBuilder();
            foreach (var tmp in chipRt.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (!tmp.gameObject.activeInHierarchy || string.IsNullOrEmpty(tmp.text)) continue;
                tmp.ForceMeshUpdate();
                var lt = tmp.rectTransform; var b = tmp.textBounds;
                float lo = lt.TransformPoint(b.min).x, hi = lt.TransformPoint(b.max).x;
                lt.GetWorldCorners(corners); float pref = corners[0].x + tmp.GetPreferredValues(tmp.text, 99999f, 0f).x * lt.lossyScale.x;
                bool fit = lo >= cr.xMin - 0.5f && hi <= cr.xMax - 1f && pref <= cr.xMax - 1f && tmp.overflowMode != TMPro.TextOverflowModes.Ellipsis;
                ok &= fit; s.Append($"'{tmp.text}' {lo - cr.xMin:F0}..{hi - cr.xMin:F0} (full {pref - cr.xMin:F0}) ");
            }
            foreach (var im in chipRt.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            {
                if (!im.isActiveAndEnabled || im is TMPro.TextMeshProUGUI || im.transform == chipRt) continue;
                var r = Px(im.rectTransform); ok &= r.xMin >= cr.xMin - 0.5f && r.xMax <= cr.xMax + 0.5f && r.yMin >= cr.yMin - 0.5f && r.yMax <= cr.yMax + 0.5f;
            }
            info = s + $"in a chip {cr.width:F0} x {cr.height:F0} px";
            return ok;
        }

        /// <summary>The overlay draws nothing of round 2's tag any more, and the unit panel no strip or slot faces.</summary>
        bool NoOldPieces()
        {
            var root = OverlayRoot; if (root) for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c.name == "ShuttleAboard" && c.gameObject.activeSelf) return false; }
            var panel = hud.UnitPanelRect; if (!panel) return true;
            foreach (var t in panel.GetComponentsInChildren<Transform>(true)) if (t.gameObject.activeInHierarchy && (t.name == "ShuttleAboard" || t.name.StartsWith("AboardFace"))) return false;
            return true;
        }

        OzText Field(string name) { var f = typeof(OpenZHud).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); return f != null ? f.GetValue(hud) as OzText : null; }

        /// <summary>The unit panel's sub line as drawn (its font size after the fit, and whether it ends in "...").</summary>
        bool subCut;
        string SubAsDrawn()
        {
            var t = Field("uSub"); if (t == null) return "?";
            var tmp = t.graphic as TMPro.TextMeshProUGUI;
            subCut = tmp && tmp.overflowMode == TMPro.TextOverflowModes.Ellipsis;
            return tmp ? $"'{t.text}' size {tmp.fontSize:F1} of {t.Size:F0} (variant {OpenZHud.ShuttleLineShown}){(subCut ? " ELLIPSIS" : "")}" : $"'{t.text}'";
        }
        static string Plain(string s) => s == null ? "" : s.Replace("<mspace=0.62em>", "").Replace("</mspace>", "");

        /// <summary>The picture, and its card and unit rects for the crops (top-left origin), and what each place says; with a name chip
        /// shown, its checks (above the bar with the siblings' gap, its words fit).</summary>
        IEnumerator Shot(string name, ZObject s, bool chipWanted = false, string sibling = null)
        {
            yield return null;
            yield return new WaitForEndOfFrame();                                                       // after the overlay's LateUpdate: its rects and the unit's as drawn this frame
            var card = Px(hud.UnitPanelRect);
            Rect unit = s != null ? ScreenRectOf(s) : default;
            Rect bar = default, chip = default; RectTransform chipRt = null;
            bool chipOn = s != null && ChipOf(s, out bar, out chip, out chipRt);
            if (chipOn) unit = Rect.MinMaxRect(Mathf.Min(unit.xMin, chip.xMin, bar.xMin), Mathf.Min(unit.yMin, bar.yMin), Mathf.Max(unit.xMax, chip.xMax, bar.xMax), Mathf.Max(unit.yMax, chip.yMax));
            string tail = s != null && OpenZUnitOverlay.AboardTexts.TryGetValue(s.Id, out var tt) ? tt + " " + (OpenZUnitOverlay.AboardFaces.TryGetValue(s.Id, out var tf) ? tf : "") : "-";
            bool sel = s != null && input.Selection.Contains(s) && hud.HudUnit == s;
            string line = sel ? SubAsDrawn() : "(not on the card)";
            string chipCard = sel && s.IsShuttle ? $"chip {OpenZHud.AboardChipFacesShown} faces '{OpenZHud.AboardChipShows}' '{Plain(OpenZHud.AboardChipText)}', {OpenZHud.PilotText} ({OpenZHud.PilotShows})" : "-";
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); frames++;
            int H = Screen.height;
            tsv.AppendLine($"{name}\t{card.x:F0}\t{H - card.yMax:F0}\t{card.width:F0}\t{card.height:F0}\t{unit.x:F0}\t{H - unit.yMax:F0}\t{unit.width:F0}\t{unit.height:F0}\t{line}\t{chipCard}\t-\t{tail}");
            Log($"frame {name}.png (camera {(rts ? rts.height : 0f):F0} m): line {line}; card {chipCard}; name chip {(chipOn ? "on" : "off")}, tail '{tail}'");
            if (sel) Check($"{name}: the card's line fits the panel (no \"...\")", !subCut);
            if (s != null && s.IsShuttle) Check($"{name}: nothing of the old tag over the ship, no strip on the portrait, no faces in the stat slot", NoOldPieces());
            if (chipOn)
            {
                float gap = chip.yMin - bar.yMax;
                if (sibling == "grunt") gapGrunt = gap; else if (sibling == "tank") gapTank = gap;
                Check($"{name}: the name chip above the health bar, nothing between them (chip bottom {chip.yMin:F0}, bar top {bar.yMax:F0}: a gap of {gap:F1} px)", gap > 0f);
                if (s.IsShuttle && gapGrunt > 0f && gapTank > 0f)
                    Check($"{name}: the gap bar to chip is the grunt squad's ({gapGrunt:F1}) and the light tank's ({gapTank:F1}) within 1 px: {gap:F1}", Mathf.Abs(gap - gapGrunt) <= 1f && Mathf.Abs(gap - gapTank) <= 1f);
                bool fits = ChipFits(chipRt, out var fi);
                Check($"{name}: the name chip's words and faces fit ({fi})", fits);
            }
            if (chipWanted) Check($"{name}: the name chip shows (the pointer on it, or selected with someone aboard)", chipOn);
            yield return null; yield return null;                                                       // the capture is written at the frame's end
        }

        /// <summary>The card's type chip shows who is aboard (faces and count), once; the stat slot reads PILOT.</summary>
        void CardChip(string name, ZObject s, string faces, int n)
        {
            var nl = Field("uNadeLabel"); var wv = Field("uWeapon");
            Check($"{name}: the WEAPON slot says NONE (the shuttle carries none), no glyph ('{(wv != null ? wv.text : "?")}')", wv != null && wv.text == "NONE");
            Check($"{name}: the card's type chip shows who is aboard once ('{OpenZHud.AboardChipShows}' '{Plain(OpenZHud.AboardChipText)}', want '{faces}' {(n > 0 ? "×" + n : "EMPTY")}); the stat slot reads '{(nl != null ? nl.text : "?")}' {OpenZHud.PilotText}",
                hud.HudUnit == s && OpenZHud.AboardChipShows == faces && (n > 0 ? Plain(OpenZHud.AboardChipText) == "×" + n : OpenZHud.AboardChipText == OzShuttleHud.EmptyWord) && nl != null && nl.text.StartsWith("PILOT"));
        }

        bool NoChip(ZObject s) => !ChipOf(s, out _, out _, out _);

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            if (W == null || hud == null || input == null || overlay == null) { Log("no game (level 16 in Play)"); fails++; Finish(); yield break; }
            if (!ZWorld.ShuttlePlayableDeviation) { Log("the shuttle rule is off ('Zod exact')"); fails++; Finish(); yield break; }
            me = g.player;
            Log($"1.2 item 2 who-is-aboard proof (round 3), {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:F1} s, UI scale {OzText.UiScale:F2}");
            int bots = W.Bots.Count; W.Bots.Clear();
            dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            Log($"set-up: CPU removed {bots}, day 0.10 held");
            yield return Wait(0.5f);
            var brad = W.Objects.FirstOrDefault(o => o.IsHero && o.HeroMemberName == "brad" && !o.Destroyed && o.KillAt < 0 && o.Owner == me && !o.IsMinion);
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me);
            var home = brad ?? fort;
            // ---- S: the siblings (a grunt squad, a light tank) beside the fort, apart from the ships
            var sp = fort != null ? Spot(fort.CenterX, fort.CenterY, 420, 900, null) : null;
            ZObject sibG = sp != null ? Squad("grunt", sp[0] >> 4, sp[1] >> 4) : null, sibT = sp != null ? Unit("light", (sp[0] >> 4) + 6, sp[1] >> 4) : null;
            yield return Wait(0.6f);
            if (sibG != null) { Select(sibG); yield return FollowUnit(sibG, 70f, 0.8f, true); yield return Shot("s1_grunt_squad_day", sibG, true, "grunt"); }
            if (sibT != null) { Select(sibT); yield return FollowUnit(sibT, 70f, 0.8f, true); yield return Shot("s2_light_tank_day", sibT, true, "tank"); Log($"the light tank: driver {sibT.Drivers.Count} ({sibT.DriverType})"); }
            Check($"S the siblings' bar-to-chip gaps measured (grunt squad {gapGrunt:F1} px, light tank {gapTank:F1} px)", gapGrunt > 0f && gapTank > 0f);
            Select(null);
            // ---- A: an empty neutral ship
            var q = home != null ? Spot(home.CenterX, home.CenterY, 240, 700, null) : null;
            var e = q != null ? OzCheatShuttle.Spawn(g, hud, Team.Neutral, new Vector2Int(q[0], q[1])) : null;
            var ev = e != null ? OzPlayableShuttle.Of(e) : null;
            if (e == null || !ev) { Check("A the panel drops an empty shuttle", false); Finish(); yield break; }
            yield return Follow(ev, e, 70f, 1.2f, false);
            yield return Shot("a1_empty_neutral_pointer_away", e);
            Check("A empty neutral ship, the pointer away: no name chip", NoChip(e));
            yield return Follow(ev, e, 70f, 0.5f, true);
            yield return Shot("a2_empty_neutral_pointer_on", e, true);
            Check("A the pointer on the empty neutral ship: its plain name chip, nothing about who is aboard", !OpenZUnitOverlay.AboardTexts.ContainsKey(e.Id) && OzShuttleHud.Line(e) == OzShuttleHud.EmptyLine);
            // ---- D: ALAN & BRAD into it
            if (brad != null)
            {
                PointAway();
                Cam(Vector3.Lerp(ev.transform.position, ZGame.PxToWorld(brad.CenterX, brad.CenterY), 0.4f), 70f);
                yield return Board(brad, e, 40f);
                Select(e); yield return Follow(ev, e, 70f, 0.9f, true);
                yield return Shot("d1_heroes_selected_pointer_on", e, true);
                CardChip("d1", e, "hero:brad+hero:alan", 2);
                Check($"D ALAN & BRAD aboard: the name chip's tail ×2 with both faces; the slot PILOT · BRAD CAN EXIT; the line ALAN & BRAD ABOARD ({OzShuttleHud.Lines(e)[0]})",
                    OpenZUnitOverlay.AboardTexts.TryGetValue(e.Id, out var td) && td == "×2" && OpenZUnitOverlay.AboardFaces[e.Id] == "hero:brad+hero:alan" && OpenZHud.PilotText == "PILOT · BRAD CAN EXIT" && OzShuttleHud.Lines(e)[0].StartsWith("ALAN & BRAD ABOARD"));
                yield return Follow(ev, e, 70f, 0.7f, false);
                yield return Shot("d2_heroes_selected_pointer_away", e, true);
                Select(null); yield return Follow(ev, e, 70f, 0.7f, false);
                yield return Shot("d3_heroes_not_selected_pointer_away", e);
                Check("D not selected, the pointer away: no name chip (the name chip's own rule)", NoChip(e));
            }
            else Log("D skipped: no ALAN & BRAD on this level (hero squads off)");
            PointAway();
            // ---- B: an own ship with its pilot only
            var s = W.PlayableShuttle;
            if (s == null || s.Destroyed || s.Owner != me || s.Drivers.Count == 0 || s.Passengers.Count > 0)
            {
                var q2 = Spot(e.CenterX, e.CenterY, 360, 900, null);
                s = q2 != null ? OzCheatShuttle.Spawn(g, hud, me, new Vector2Int(q2[0], q2[1])) : null;
                Log($"B the level's own ship not usable: the panel's crewed drop #{(s != null ? s.Id : -1)}");
            }
            var sv = s != null ? OzPlayableShuttle.Of(s) : null;
            if (s == null || !sv) { Check("B an own shuttle with its pilot", false); Finish(); yield break; }
            Select(s); yield return Follow(sv, s, 70f, 1.0f, true);
            yield return Shot("b1_pilot_only_selected_pointer_on", s, true);
            CardChip("b1", s, "", 0);
            Check($"B own ship, pilot only: the plain name chip (no tail), the slot PILOT STAYS IN with his face ({OpenZHud.PilotShows}), the line ROOM FOR ONE SQUAD",
                !OpenZUnitOverlay.AboardTexts.ContainsKey(s.Id) && OzShuttleHud.Who(s).Count == 0 && OpenZHud.PilotText == "PILOT STAYS IN" && OpenZHud.PilotShows == s.DriverType && OzShuttleHud.Lines(s)[0].StartsWith("ROOM FOR ONE SQUAD"));
            yield return Follow(sv, s, 70f, 0.6f, false);
            yield return Shot("b2_pilot_only_selected_pointer_away", s);
            Check("B selected, nobody aboard, the pointer away: no name chip (as any selected unit)", NoChip(s));
            // ---- C: a grunt squad of 3 into the pilot-only ship (its passengers)
            double h = s.ShuttleHeadingDeg * System.Math.PI / 180.0;
            var sq = Squad("grunt", (int)(s.CenterX - System.Math.Cos(h) * 112) >> 4, (int)(s.CenterY - System.Math.Sin(h) * 112) >> 4); yield return Wait(0.5f);
            if (sq == null) { Check("C a grunt squad", false); Finish(); yield break; }
            yield return Board(sq, s, 30f);
            Select(s); yield return Follow(sv, s, 70f, 0.8f, true);
            yield return Shot("c1_grunts_selected_pointer_on_70m", s, true);
            CardChip("c1", s, "grunt", 3);
            Check($"C 3 grunts aboard a pilot's ship: 3 counted (not the pilot), the tail ×3 with a grunt face, the line 3 GRUNTS ABOARD ({OzShuttleHud.Lines(s)[0]})",
                OzShuttleHud.Who(s).Count == 3 && OpenZUnitOverlay.AboardTexts.TryGetValue(s.Id, out var tc) && tc == "×3" && OpenZUnitOverlay.AboardFaces[s.Id] == "grunt" && OzShuttleHud.Lines(s)[0].StartsWith("3 GRUNTS ABOARD"));
            yield return Follow(sv, s, 25f, 0.6f, true);
            yield return Shot("c1b_grunts_selected_pointer_on_25m", s, true);
            yield return Follow(sv, s, 175f, 0.6f, true);
            yield return Shot("c1c_grunts_selected_pointer_on_175m", s, true);
            if (e != null && e.Owner == me)
            {   // a multi-selection still reads in the line
                input.Selection.Clear(); input.Selection.Add(s); input.Selection.Add(e);
                yield return Follow(sv, s, 70f, 0.6f, false);
                var hu = hud.HudUnit; string ln = SubAsDrawn();
                yield return Shot("c1d_two_ships_selected", hu ?? s);
                Check($"C two ships selected: the card's line still says +1 MORE SELECTED ({ln})", ln.Contains("+1 MORE SELECTED"));
            }
            Select(null); yield return Follow(sv, s, 70f, 0.6f, false);
            yield return Shot("c2_grunts_not_selected_pointer_away", s);
            Check("C not selected, the pointer away: no name chip", NoChip(s));
            if (dn) { dn.timeOfDay = 0.62f; dn.Apply(0.62f); }
            yield return Follow(sv, s, 70f, 1.5f, true);
            yield return Shot("c3_grunts_night_pointer_on", s, true);
            Select(s); yield return Follow(sv, s, 70f, 0.6f, false);
            yield return Shot("c4_grunts_night_selected", s, true);
            if (sibG != null) { Select(sibG); yield return FollowUnit(sibG, 70f, 0.6f, true); yield return Shot("s3_grunt_squad_night", sibG, true); }
            if (sibT != null) { Select(sibT); yield return FollowUnit(sibT, 70f, 0.6f, true); yield return Shot("s4_light_tank_night", sibT, true); }
            if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); }
            yield return Wait(0.8f);
            var q3 = Spot(s.CenterX, s.CenterY, 1200, 2000, s) ?? Spot(s.CenterX, s.CenterY, 700, 1200, s);
            if (q3 == null) { Check("C a place to fly to", false); Finish(); yield break; }
            Select(s); Move(s, q3[0], q3[1]);
            yield return WaitFor(() => { CamOn(sv, 70f); PointAt(s); return s.ShuttleState == ZShuttleState.Cruise; }, 25f);
            yield return Follow(sv, s, 70f, 2.5f, true);
            yield return Shot("c5_grunts_flying_selected_pointer_on_70m", s, true);
            Check("C flying, selected: the tail ×3 on the name chip over the ship in the air; the line says FLYING", s.ShuttleAirborne && OpenZUnitOverlay.AboardTexts.TryGetValue(s.Id, out var tfl) && tfl == "×3" && OzShuttleHud.Lines(s)[0].EndsWith("FLYING"));
            yield return Follow(sv, s, 25f, 0.5f, true);
            yield return Shot("c5b_grunts_flying_selected_pointer_on_25m", s);
            yield return Follow(sv, s, 175f, 0.5f, true);
            yield return Shot("c5c_grunts_flying_selected_pointer_on_175m", s, true);
            Select(null); yield return Follow(sv, s, 70f, 0.6f, false);
            yield return Shot("c6_grunts_flying_not_selected_pointer_away", s);
            Check("C flying, not selected, the pointer away: no name chip", s.ShuttleAirborne && NoChip(s));
            yield return WaitFor(() => { Cam(sv.transform.position, 70f); return s.ShuttleState == ZShuttleState.Landed; }, 40f);
            Finish();
        }

        void Finish()
        {
            Report = $"{(fails == 0 ? "PASS" : "FAIL")} {passes} passed, {fails} failed, {frames} frames";
            sb.Insert(0, Report + "\n");
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "aboard_proof_log.txt"), sb.ToString());
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "shots.tsv"), tsv.ToString());
            }
            catch (System.Exception ex) { Debug.LogWarning("[OzShuttleAboardProof] " + ex.Message); }
            Debug.Log("[OzShuttleAboardProof] " + Report + " -> " + dir);
            Destroy(gameObject);
        }
    }
}
