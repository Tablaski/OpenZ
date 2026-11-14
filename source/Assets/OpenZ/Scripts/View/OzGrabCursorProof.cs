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
    /// 1.2 item 17 Play proof (UI/OpenZCursors.cs: a grenade crate a selected robot can pick up shows Z's hand-grab cursor "pickup"; a flag
    /// keeps the flag-capture cursor "grab"), any level in Play (menu OpenZ/V1.2/Cursors: 17 Grenade Hand Cursor Proof, Editor/OzGrabCursorMenu.cs).
    /// Set-up: the CPU teams removed, morning (day clock 0.10, held), the normal RTS camera at 70 m over a grenade crate (the level's own one
    /// nearest our fort, else a new one beside it). The game's pointer is moved and clicked by queued input events inside the game only
    /// (no desktop input). Counted PASS / FAIL lines:
    ///  F  the new pictures load: the 4 HD frames (Kit/Cursors/HD/cur_pickup_256_*), the 12 hardware frames of 32 / 48 / 64 px (readable),
    ///     the HD hardware cursor baked at this screen's size, the order mark (Kit/Cursors/mark_pickedup);
    ///  A  a grunt squad selected, the pointer on the crate: the cursor "pickup", and the lock blip (ui_target_lock) plays as it did for "grab";
    ///  B  the same squad, the pointer on a flag that is not ours: the cursor "grab" (the flag-capture cursor, unchanged);
    ///  C  a Tough squad (it never picks up grenades) on the crate: not "pickup"; D  a grunt squad already holding grenades: not "pickup";
    ///  E  a right-click on the crate with the grunt squad: a pick-up order to that crate, the order mark "pickedup" (drawn by OpenZUnitOverlay);
    ///  G  a picture of the game's whole cursor row at its real size (OzSoftCursor.Gallery), the hand beside the flag.
    /// The pictures are taken with the game's own software cursor drawn (PlayerPrefs oz_cursor_hw 0 for the run, put back at the end and when
    /// Play stops), since a screen capture never holds the system's pointer. Output: <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzGrabCursorProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", "grab_cursor_proof_k17");
            return System.IO.Path.Combine(Application.persistentDataPath, "grab_cursor_proof_k17");
        }
        public static void Run()
        {
            Report = "running";
#if UNITY_EDITOR
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);
#endif
            new GameObject("OzGrabCursorProof").AddComponent<OzGrabCursorProof>();
        }

        const string HwKey = "oz_cursor_hw";
        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; OpenZHud hud; OpenZCursors cursors; Team me = Team.Red;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        bool swapped, hadHw; int oldHw = 1; Vector2 mouse;

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>();
            hud = OpenZHud.I; cursors = FindFirstObjectByType<OpenZCursors>();
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzGrabCursorProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        void Shot(string name) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); Log($"   picture {name}.png (the game's software cursor drawn: {OzSoftCursor.Showing}, {OzSoftCursor.SizePx} px)"); }
        IEnumerator Wait(float s0) { float t = 0f; while (t < s0) { t += Time.unscaledDeltaTime; yield return null; } }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        void Cam(int px, int py, float h) { if (!rts) return; rts.FreeLookOff(); var f = ZGame.PxToWorld(px, py); rts.focus = new Vector3(f.x, 0f, f.z); rts.SetHeightImmediate(h); }
        void Select(ZObject o) { input.Selection.Clear(); if (o != null) input.Selection.Add(o); }
        string Cur => cursors ? cursors.Current : "?";
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        static long D2(ZObject o, int x, int y) => (long)(o.CenterX - x) * (o.CenterX - x) + (long)(o.CenterY - y) * (o.CenterY - y);
        int LockPlays() { var ad = AudioDirector.I; return ad != null && ad.Plays.TryGetValue("ui_target_lock", out var n) ? n : 0; }
        static bool LockCursor(string c) => c == "attack" || c == "enter" || c == "grab" || c == "repair" || c == OpenZCursors.GrenadeCursor;

        /// <summary>The game's own pointer (a queued input event inside the game, as the testing panel's self-test does).</summary>
        void Pointer(Vector2 p, bool right = false)
        {
            if (Mouse.current == null) return;
            mouse = p;
            var st = new MouseState { position = p }; if (right) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }

        /// <summary>The pointer onto o (its pick box's middle, then small steps round it) until the game hovers o; the cursor then.</summary>
        IEnumerator HoverOn(ZObject o, System.Action<bool, string> got)
        {
            bool ok = false;
            if (o != null)
            {
                var c = input.ScreenRect(o).center;
                Vector2[] steps = { Vector2.zero, new Vector2(0, 4), new Vector2(0, -4), new Vector2(4, 0), new Vector2(-4, 0), new Vector2(0, 8), new Vector2(0, -8) };
                foreach (var s0 in steps)
                {
                    Pointer(c + s0); yield return Frames(3); Pointer(c + s0); yield return Frames(2);
                    if (input.Hover == o && !hud.PointerOverUi) { ok = true; break; }
                }
            }
            yield return Frames(2);
            got(ok, Cur);
        }

        /// <summary>The pointer onto bare ground near p (nothing hovered, not over the HUD), so the next hover is a real cursor change.</summary>
        IEnumerator Ground(Vector2 p)
        {
            Vector2[] steps = { new Vector2(0, 150), new Vector2(150, 0), new Vector2(-150, 0), new Vector2(0, -150), new Vector2(110, 110), new Vector2(-110, 110) };
            foreach (var s0 in steps)
            {
                Pointer(p + s0); yield return Frames(4);
                if (input.Hover == null && !hud.PointerOverUi) break;
            }
            yield return Wait(0.45f);                                                                         // past the lock blip's 0.25 s throttle
        }

        ZObject Squad(string type, int px, int py)
        {
            if (!OzCheatSpawner.FreeTile(W, px >> 4, py >> 4, 1, true, out int tx, out int ty)) { tx = px >> 4; ty = py >> 4; }
            var o = W.CreateRobotGroup(type, tx * 16, ty * 16, me); if (o == null) return null;
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            return o;
        }

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            if (W == null || input == null || cursors == null || hud == null) { Log("no game (a level in Play)"); fails++; Finish(); yield break; }
            me = g.player;
            Log($"1.2 item 17 grenade hand cursor proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s, the grenade cursor '{OpenZCursors.GrenadeCursor}'");
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            hadHw = PlayerPrefs.HasKey(HwKey); oldHw = PlayerPrefs.GetInt(HwKey, 1); PlayerPrefs.SetInt(HwKey, 0); swapped = true;
            Log($"set-up: CPU removed {bots}, day 0.10 held, the software cursor for the pictures ({HwKey} was {(hadHw ? oldHw.ToString() : "not set")})");

            // ---- F: the pictures the game loads
            int hd = 0, hw = 0, readable = 0, baked = 0;
            for (int f = 0; f < 4; f++)
            {
                var t = OzKit.GetTexture(OzSoftCursor.HdPrefix + OpenZCursors.GrenadeCursor + "_" + OzSoftCursor.HdSize + "_" + f); if (t != null && t.width == OzSoftCursor.HdSize) hd++;
                foreach (int s in new[] { 32, 48, 64 })
                {
                    var h = OzKit.GetTexture("Cursors/cur_" + OpenZCursors.GrenadeCursor + "_" + s + "_" + f);
                    if (h != null && h.width == s) { hw++; if (h.isReadable) readable++; }
                }
                if (OzSoftCursor.HardwareTexture(OpenZCursors.GrenadeCursor, f, OzKit.HardwareCursorSize) != null) baked++;
            }
            var mark = OzKit.GetSprite("Cursors/mark_pickedup");
            Check($"F1 the 4 HD frames load at {OzSoftCursor.HdSize} px: {hd} of 4", hd == 4);
            Check($"F2 the 12 hardware frames load (32 / 48 / 64 px): {hw} of 12, readable {readable}", hw == 12 && readable == 12);
            Check($"F3 the HD hardware cursor bakes at this screen's size ({OzKit.HardwareCursorSize} px): {baked} of 4 frames", baked == 4);
            Check($"F4 the order mark mark_pickedup loads: {(mark ? mark.rect.width + " px" : "missing")}", mark != null);

            // ---- staging: a NEW grenade crate near our fort, at least 300 px from every robot (an idle robot within 220 px,
            // auto_grab_vehicle_distance, walks over and takes a crate by itself); the grunts that can pick it up stand 260 px off and
            // are held (PlayerHold: no self-started grab); the Tough squad and the grunts holding grenades never take one, they stand close
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me && !o.Destroyed);
            int fx = fort != null ? fort.CenterX : W.Map.width * 8, fy = fort != null ? fort.CenterY : W.Map.height * 8;
            int ctx = -1, cty = -1;
            for (int r = 200; r <= 900 && ctx < 0; r += 48)
                for (int a = 0; a < 16 && ctx < 0; a++)
                {
                    double an = a * System.Math.PI / 8;
                    int cx = fx + (int)(System.Math.Cos(an) * r), cy = fy + (int)(System.Math.Sin(an) * r);
                    if (cx < 64 || cy < 64 || cx > W.Map.width * 16 - 64 || cy > W.Map.height * 16 - 64) continue;
                    if (!OzCheatSpawner.FreeTile(W, cx >> 4, cy >> 4, 1, true, out int tx, out int ty) || System.Math.Abs(tx - (cx >> 4)) > 1 || System.Math.Abs(ty - (cy >> 4)) > 1) continue;
                    int px = tx * 16 + 8, py = ty * 16 + 8;
                    if (W.Objects.Any(o => o.Kind == ZKind.Robot && !o.Destroyed && D2(o, px, py) < 300L * 300L)) continue;
                    ctx = tx; cty = ty;
                }
            if (ctx < 0) { Log("no free spot 300 px clear of every robot within 900 px of our fort"); fails++; Finish(); yield break; }
            var crate = W.CreateObject("item", "grenades", ctx * 16, cty * 16, Team.Neutral);
            try { g.SpawnUnitView(crate); } catch (System.Exception e) { Log("crate view: " + e.Message); }
            Log($"a new grenade crate #{crate.Id} at tile ({ctx},{cty}), {crate.Grenades} grenades");
            int bx = crate.CenterX, by = crate.CenterY;
            var A = Squad("grunt", bx + 260, by); if (A != null) A.PlayerHold = true;
            var T = Squad("tough", bx - 88, by + 40);
            var Dg = Squad("grunt", bx + 40, by + 104); if (Dg != null) Dg.Grenades = 8;
            var flag = W.Objects.Where(o => o.Type == "flag" && !o.Destroyed && o.Owner != me).OrderBy(o => D2(o, bx, by)).FirstOrDefault();
            Log($"staging: grunts A #{A?.Id}, Tough squad #{T?.Id}, grunts holding grenades #{Dg?.Id} ({Dg?.Grenades}), the flag #{flag?.Id} ({flag?.Owner})");
            Cam(bx, by, 70f);
            input.Selection.Clear();
            Pointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.75f));
            yield return Wait(1.5f);
            Check($"S0 the software cursor draws for the pictures (the Game view in front): {OzSoftCursor.Showing}", OzSoftCursor.Showing);

            // ---- A: grunts selected, the pointer on the crate
            Select(A);
            var crateAt = input.ScreenRect(crate).center;
            yield return Ground(crateAt);
            string before = Cur; int lock0 = LockPlays();
            bool hovA = false; string curA = "?";
            yield return HoverOn(crate, (h, c) => { hovA = h; curA = c; });
            yield return Wait(0.3f);
            int lockA = LockPlays() - lock0;
            Check($"A1 grunts selected, the pointer on the grenade crate (hovered {hovA}, {crate.Grenades} grenades in it): the cursor '{curA}' (the hand '{OpenZCursors.GrenadeCursor}')", hovA && curA == OpenZCursors.GrenadeCursor);
            Check($"A2 the lock blip as for 'grab' before: the cursor '{before}' -> '{curA}', ui_target_lock played {lockA}", LockCursor(before) || lockA >= 1);
            Shot("k17_a_grunts_on_crate_hand_cursor_h70");

            // ---- B: the same squad, the pointer on a flag that is not ours
            if (flag != null)
            {
                Cam(flag.CenterX, flag.CenterY, 70f); yield return Wait(0.6f);
                bool hovB = false; string curB = "?";
                yield return HoverOn(flag, (h, c) => { hovB = h; curB = c; });
                Check($"B1 grunts selected, the pointer on the flag #{flag.Id} (hovered {hovB}): the cursor '{curB}' (the flag-capture cursor 'grab', unchanged)", hovB && curB == "grab");
                Shot("k17_b_grunts_on_flag_flag_cursor_h70");
                Cam(bx, by, 70f); yield return Wait(0.6f);
            }
            else Log("B skipped: no flag that is not ours on this level");

            // ---- C: a Tough squad on the crate; D: grunts already holding grenades on the crate
            Select(T);
            bool hovC = false; string curC = "?";
            yield return HoverOn(crate, (h, c) => { hovC = h; curC = c; });
            Check($"C1 a Tough squad (never picks up grenades) on the crate (hovered {hovC}): the cursor '{curC}', not the hand, not the flag", hovC && curC != OpenZCursors.GrenadeCursor && curC != "grab");
            Select(Dg);
            bool hovD = false; string curD = "?";
            yield return HoverOn(crate, (h, c) => { hovD = h; curD = c; });
            Check($"D1 grunts already holding {Dg?.Grenades} grenades on the crate (hovered {hovD}): the cursor '{curD}', not the hand", hovD && curD != OpenZCursors.GrenadeCursor && curD != "grab");

            // ---- E: a right-click on the crate with the grunt squad
            Select(A);
            bool hovE = false; string curE = "?";
            yield return HoverOn(crate, (h, c) => { hovE = h; curE = c; });
            float mark0 = input.MarkerTime;
            Pointer(mouse, true); yield return Frames(3); Pointer(mouse, false); yield return Frames(4);
            string orders = A != null ? string.Join(", ", A.Waypoints.Select(w => w.mode + " #" + w.refId)) : "no squad";
            bool pick = A != null && A.Waypoints.Any(w => w.mode == WpMode.PickupGrenades && w.refId == crate.Id);
            Check($"E1 the right-click on the crate (the cursor '{curE}') gave the grunts a pick-up order to it: [{orders}]", hovE && pick);
            Check($"E2 the order mark: '{cursors.OrderMark}' (the grey hand 'pickedup'), the order moment {(Mathf.Approximately(mark0, input.MarkerTime) ? "unchanged" : "new")}", cursors.OrderMark == "pickedup" && !Mathf.Approximately(mark0, input.MarkerTime));
            var ov = FindFirstObjectByType<OpenZUnitOverlay>();
            const System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            string glyph = ov ? typeof(OpenZUnitOverlay).GetField("orderGlyph", NP)?.GetValue(ov) as string : null;
            var markImg = ov ? typeof(OpenZUnitOverlay).GetField("order", NP)?.GetValue(ov) as UnityEngine.UI.Image : null;
            string drawn = markImg && markImg.sprite ? markImg.sprite.name : "none";
            Check($"E3 the overlay draws the hand mark at the crate: glyph '{glyph}', sprite '{drawn}', shown {(markImg ? markImg.gameObject.activeInHierarchy.ToString() : "?")}", glyph == "pickedup" && drawn.StartsWith("mark_pickedup"));
            Shot("k17_e_order_mark_hand_h70");                                                               // the mark pops for OpenZUnitOverlay.OrderMarkLife (0.36 s): taken at once

            // ---- G: the game's cursor row at its real size
            var galleryAt = OzSoftCursor.GalleryAt;
            OzSoftCursor.GalleryAt = new Vector2(Mathf.Round(Screen.width * 0.22f), Mathf.Round(Screen.height * 0.55f));
            OzSoftCursor.Gallery = true; yield return Wait(0.4f);
            Log($"G the cursor row, left to right: {string.Join(", ", OzSoftCursor.Names)}");
            Check($"G1 the cursor row holds the hand ('{OpenZCursors.GrenadeCursor}') beside the flag ('grab')", System.Array.IndexOf(OzSoftCursor.Names, OpenZCursors.GrenadeCursor) == System.Array.IndexOf(OzSoftCursor.Names, "grab") + 1);
            Shot("k17_g_cursor_row_real_size_h70");
            OzSoftCursor.Gallery = false; OzSoftCursor.GalleryAt = galleryAt;

            // ---- not counted: the order carried out
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 25f && A != null && Alive(A) && A.Grenades <= 0) yield return null;
            Log($"INFO the grunts picked up the grenades: {(A != null && A.Grenades > 0 ? "yes, " + A.Grenades + " after " + (Time.unscaledTime - t0).ToString("0.0") + " s" : "not within 25 s")}");
            Finish();
        }

        void Restore()
        {
            if (!swapped) return;
            swapped = false;
            if (hadHw) PlayerPrefs.SetInt(HwKey, oldHw); else PlayerPrefs.DeleteKey(HwKey);
            OzSoftCursor.Gallery = false;
        }

        void Finish()
        {
            Restore();
            Report = $"{passes} PASS {fails} FAIL";
            Log("RESULT " + Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "grab_cursor_proof_log.txt"), sb.ToString()); } catch (System.Exception e) { Debug.LogWarning("[OzGrabCursorProof] log: " + e.Message); }
            Destroy(gameObject);
        }

        void OnDestroy() { Restore(); if (Report == "running") Report = "stopped"; }
    }
}
