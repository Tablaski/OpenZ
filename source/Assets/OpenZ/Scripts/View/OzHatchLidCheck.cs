#if UNITY_EDITOR && !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.5 kit hatch_lid: the Play check (editor only, never in a build).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// playing in the normal mode, run from the menus OpenZ/V1.5/Hatch Lid (Editor/OzV15HatchLidMenu.cs). It stages each hatch vehicle (the
    /// light tank, the medium tank, the heavy tank, the mech) twice on free ground near the camera, crewed, red, and replays the sequences that
    /// can touch a hatch lid, counting every frame (after every LateUpdate, the guard's included) in which a shown hull has its lid missing:
    /// a lid part destroyed, switched off, its renderer off, held off from drawing outside a picture, scaled to nothing, at no position, or
    /// its hinge turned past the open pose or off its own axis. Each row needs its samples: a row with nothing measured FAILS.
    /// <list type="bullet">
    /// <item>0 the staging: eight hulls, each crewed, each with its lid read under its *_HatchHinge.</item>
    /// <item>1 heavy hits on the SHUT lid (row A): the knock-off itself aimed at the lid's middle (RobotPopDeath.KnockOff), then
    /// <see cref="Bursts"/> heavy bursts on the lid through the game's own HC1 path (HeavyChips.OnBlast, as CombatView calls it for a shell),
    /// then <see cref="WatchAfterSec"/> s watched.</item>
    /// <item>2 EXIT DRIVER and back in (row A): each driver let out (ZWorld.Eject, the HUD's EXIT DRIVER), then ordered back in (the player's
    /// ENTER order): the lid kicked open, shut, the hull neutral, the boarding's open and shut, the hull red again.</item>
    /// <item>3 damage stages (row A): smoke (40 %), burning (15 %), repaired (100 %).</item>
    /// <item>4 OzFpsView in and out on each hull (row A): the view's per-picture holds must leave nothing held after the frame.</item>
    /// <item>5 heavy hits on the OPEN lid (row B): once the idle crew rests in the open hatch (DV75, 25 - 33 s), the same hits.</item>
    /// <item>6 the guard: NORMAL run: nothing to put back (the fix alone holds); GUARD ONLY run: it put back what the 1.4 knock-off took.</item>
    /// <item>7 (review fix) one row A hull hidden whole (every renderer off, as the level 16 ending scene does to the units near its set): the
    /// guard leaves its lid hidden with it for 30 frames (no lone lid drawn), then every renderer is switched back on.</item>
    /// </list>
    /// Runs (<see cref="Mode"/>): 0 NORMAL (the fix and the guard: every row PASS), 1 BEFORE (the 1.4 knock-off, no guard: rows 1 and 5 must
    /// FAIL on the light tank, the medium tank and the heavy tank, whose lids are modelled, and PASS on the mech, whose built OZ_ lid the 1.4
    /// filter already kept: the check sees the owner's fault), 2 GUARD ONLY (the 1.4 knock-off, the guard on: rows 1 and 5 PASS, row 6 counts
    /// the lids it put back). The switches are put back at the end; everything the check made is removed.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class OzHatchLidCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.5 hatch lid play]";
        public static string Report = "not run", Folder;
        public static bool Running;
        /// <summary>0 NORMAL, 1 BEFORE (the 1.4 knock-off, no guard), 2 GUARD ONLY (the 1.4 knock-off, the guard on). Set by the menu.</summary>
        public static int Mode;
        /// <summary>Heavy bursts on each lid; seconds watched after them; the longest wait for the crews to rest in the open hatch; the wait
        /// after EXIT DRIVER before the ENTER order; the longest wait for the driver to be back in; seconds in OzFpsView per hull.</summary>
        public static int Bursts = 12;
        public static float WatchAfterSec = 1.5f, RestWaitMaxSec = 90f,   /* Unity pass 1.5: 45 s was too short for the row B light tank on L16 (its calm restarts while row A is hit) */ ExitWaitSec = 4f, BoardWaitMaxSec = 15f, ViewHoldSec = 1.5f;
        static readonly string[] Types = { "light", ZMTank.Type, "heavy", ZMTank.Mech };

        ZGame g; ZWorld W; PlayerInput input; Team me;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();
        int startMaxId = int.MaxValue;
        ZObject shooter;

        sealed class Tank
        {
            public string type; public int row; public ZObject o; public UnitView v; public Transform hinge; public Quaternion rest;
            public readonly List<Renderer> lid = new List<Renderer>();
            public bool watch; public int frames, missing; public string first = "";
        }
        readonly List<Tank> tanks = new List<Tank>();

        static string ModeName => Mode == 1 ? "BEFORE (the 1.4 knock-off, no guard)" : Mode == 2 ? "GUARD ONLY (the 1.4 knock-off, the guard on)" : "NORMAL (the fix and the guard)";
        public static string ReportFile => Mode == 1 ? "play_check_before_report.txt" : Mode == 2 ? "play_check_guard_only_report.txt" : "play_check_report.txt";
        static string Kind(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_HatchLidCheck").AddComponent<OzHatchLidCheck>();
        }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            float speed = 1f; bool keep0 = OzHatchLid.KeepLidOn, guard0 = OzHatchLid.GuardOn;
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>();
                if (!g || W == null || !input || !BoardingView.I) { Report = Tag + " FAIL: no level playing (ZGame, PlayerInput, BoardingView)"; Debug.LogWarning(Report); yield break; }
                me = OpenZHud.I ? OpenZHud.I.Player : g.player;
                speed = g.gameSpeed; g.gameSpeed = 1f;
                startMaxId = W.Objects.Count > 0 ? W.Objects.Max(o => o.Id) : 0;
                OzHatchLid.KeepLidOn = Mode == 0; OzHatchLid.GuardOn = Mode != 1;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                Note($"run: {ModeName}; OzHatchLid.KeepLidOn {OzHatchLid.KeepLidOn}, OzHatchLid.GuardOn {OzHatchLid.GuardOn}; player {me}");
                StartCoroutine(Peace());
                yield return Main();
            }
            finally
            {
                if (g) g.gameSpeed = speed;
                OzHatchLid.KeepLidOn = keep0; OzHatchLid.GuardOn = guard0;
                Cleanup();
                Report = $"{Tag} {ModeName}: {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, ReportFile), Report); } catch { } }
                Debug.Log(Report);
                Running = false; Mode = 0; Destroy(gameObject);                                            // the next menu sets its own run
            }
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);
        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

        // ------------------------------------------------------------------ the count: after every LateUpdate (order 32000), each frame
        void LateUpdate()
        {
            if (!Running) return;
            for (int i = 0; i < tanks.Count; i++)
            {
                var t = tanks[i];
                if (!t.watch || t.o == null || t.o.Destroyed || t.o.KillAt >= 0 || !t.v || t.v.Dying || !t.v.gameObject.activeInHierarchy) continue;
                t.frames++;
                string why = LidMissing(t);
                if (why == null) continue;
                t.missing++;
                if (t.first.Length == 0) t.first = $"frame {Time.frameCount}: {why}";
            }
        }

        /// <summary>Why this shown hull's lid is missing now (null: it is drawn, on its hinge, between shut and open).</summary>
        static string LidMissing(Tank t)
        {
            if (!t.hinge) return "its hinge is gone";
            if (t.lid.Count == 0) return "no lid was found at the staging";
            for (int i = 0; i < t.lid.Count; i++)
            {
                var r = t.lid[i];
                if (!r) return "a lid part was destroyed";
                if (!r.gameObject.activeInHierarchy) return r.name + " is switched off";
                if (!r.enabled) return r.name + "'s renderer is off";
                if (r.forceRenderingOff) return r.name + " is held off from drawing after the pictures";
                var s = r.transform.lossyScale;
                if (!(Mathf.Abs(s.x) > 1e-4f && Mathf.Abs(s.y) > 1e-4f && Mathf.Abs(s.z) > 1e-4f)) return r.name + " is scaled to nothing";
                var p = r.transform.position;
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)) return r.name + " has no position";
            }
            float open = BoardingView.I ? BoardingView.I.hatchOpenDeg : 95f;
            var rel = Quaternion.Inverse(t.rest) * t.hinge.localRotation;
            rel.ToAngleAxis(out float ang, out Vector3 axis);
            if (float.IsNaN(ang)) return "its hinge has no turn";
            if (ang > 180f) { ang = 360f - ang; axis = -axis; }
            if (ang > open + 3f) return $"its lid turned {ang:0} deg off its seat (open is {open:0})";
            if (ang > 3f && Mathf.Abs(axis.x) < 0.95f) return $"its lid turned {ang:0} deg about another axis than its hinge's";
            return null;
        }

        static string Counts(Tank t) => $"{t.missing} of {t.frames} frames with the lid missing{(t.first.Length > 0 ? " (first: " + t.first + ")" : "")}";
        static void Restart(Tank t) { t.watch = true; t.frames = 0; t.missing = 0; t.first = ""; }

        // ------------------------------------------------------------------ the staging
        ZObject Unit(string cat, string type, Team team, int tx, int ty)
        {
            var o = W.CreateObject(cat, type, tx * 16, ty * 16, team);
            if (o == null) return null;
            g.SpawnUnitView(o);
            made.Add(o);
            return o;
        }

        void Kill(ZObject o) { if (o == null || o.Destroyed || o.KillAt >= 0) return; o.SetHealth(0); W.UpdateObjectHealth(o); }

        bool TileFree(int x, int y) => W.Path.TilePassable(x, y, true) && W.Path.TilePassable(x, y, false) && !W.LavaAtPx(x * 16 + 8, y * 16 + 8);
        bool AreaFree(int x, int y, int w, int h)
        {
            if (x < 3 || y < 3 || x + w >= W.Path.W - 3 || y + h >= W.Path.H - 3) return false;
            // Unity pass 1.5: no level has a whole free 26 x 11 block (roads, rocks, trees, parked vehicles, buildings everywhere), so only the
            // nine stands the staging uses must be open: a 4 x 4-tile patch round each hull (x0 + 6k, rows y0 and y0 + 6) and the shooter's.
            for (int k = 0; k < 4; k++) if (!SpotFree(x + 6 * k, y) || !SpotFree(x + 6 * k, y + 6)) return false;
            return SpotFree(x + 23, y + 3);
        }
        bool SpotFree(int x, int y)
        {
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++) if (!TileFree(x + i, y + j)) return false;
            return !W.Objects.Any(o => !o.Destroyed && o.KillAt < 0 && o.Kind != ZKind.MapItem && o.LocX < (x + 4) * 16 && o.LocX + o.WidthPix > x * 16 && o.LocY < (y + 4) * 16 && o.LocY + o.HeightPix > y * 16);
        }
        /// <summary>A w x h tile block near the camera's middle, every tile passable for robots and vehicles, no unit on it, no building within 2 tiles.</summary>
        bool Block(int w, int h, out int x0, out int y0)
        {
            var cam = Camera.main; var c = Vector3.zero;
            if (cam) g.GroundPoint(cam.ScreenPointToRay(new Vector3(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f, 0f)), out c);
            var f = ZGame.WorldToPx(c); int fx = f.x >> 4, fy = f.y >> 4;
            for (int r = 0; r <= 40; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = fx + dx - w / 2, y = fy + dy - h / 2;
                        if (AreaFree(x, y, w, h)) { x0 = x; y0 = y; return true; }
                    }
            x0 = fx; y0 = fy; return false;
        }

        void Capture(Tank t)
        {
            t.lid.Clear(); t.hinge = null; t.v = null;
            if (t.o == null || !g.Views.TryGetValue(t.o.Id, out var v) || !v) return;
            t.v = v;
            foreach (var x in v.GetComponentsInChildren<Transform>(true)) if (x.name.EndsWith("_HatchHinge")) { t.hinge = x; break; }
            if (!t.hinge) return;
            t.rest = t.hinge.localRotation;                                                                     // a fresh crew: the lid shut (checked: open share 0)
            foreach (var r in t.hinge.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (r.enabled && r.gameObject.activeInHierarchy && mf && mf.sharedMesh) t.lid.Add(r);
            }
        }

        static Vector3 LidCentre(Tank t)
        {
            var c = Vector3.zero; int n = 0;
            foreach (var r in t.lid) if (r) { c += r.bounds.center; n++; }
            return n > 0 ? c / n : (t.hinge ? t.hinge.position : Vector3.zero);
        }

        /// <summary>The level's own enemies never shoot the scene: their Damage 0 while the check runs (put back at the end).</summary>
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();
        IEnumerator Peace()
        {
            while (Running && W != null)
            {
                foreach (var o in W.Objects) if (o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.Damage > 0 && !made.Contains(o) && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        void Cleanup()
        {
            if (W == null) return;
            foreach (var t in tanks) t.watch = false;
            foreach (var kv in quieted) if (kv.Key != null && !kv.Key.Destroyed) kv.Key.Damage = kv.Value;
            if (quieted.Count > 0) Note($"the level's own enemies quieted for the check: {quieted.Count} (their Damage put back)");
            quieted.Clear();
            if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check ends", false, true);
            if (input) input.Selection.Clear();
            var ids = new HashSet<int>(made.Where(o => o != null).Select(o => o.Id));
            foreach (var o in new List<ZObject>(W.Objects))                                                      // + every driver let out meanwhile beside one of ours
                if (o.IsRobot && o.Id > startMaxId && made.Any(v => v != null && v.Kind != ZKind.Robot && Mathf.Abs(v.CenterX - o.CenterX) < 96 && Mathf.Abs(v.CenterY - o.CenterY) < 96)) ids.Add(o.Id);
            foreach (var o in new List<ZObject>(W.Objects)) if (ids.Contains(o.Id)) Kill(o);
        }

        IEnumerator Picture(string file)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(Folder, file), tex.EncodeToPNG());
                Note("picture " + file);
            }
            catch (System.Exception ex) { Note("picture " + file + " failed: " + ex.Message); }
            finally { if (tex) Destroy(tex); }
        }

        string PicName(string s) => (Mode == 1 ? "before_" : Mode == 2 ? "guard_only_" : "") + s;

        // ------------------------------------------------------------------ the rows
        IEnumerator Main()
        {
            if (!Block(26, 11, out int x0, out int y0)) { Log("0 free ground near the camera", false, "no free 26 x 11-tile stretch of open ground near the camera: move the camera to open ground and run again"); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) rts.FlyTo(ZGame.PxToWorld((x0 + 13) * 16, (y0 + 5) * 16));
            yield return Real(2f);
            for (int k = 0; k < Types.Length; k++)
            {
                tanks.Add(new Tank { type = Types[k], row = 0, o = Unit("vehicle", Types[k], me, x0 + 1 + 6 * k, y0 + 1) });
                tanks.Add(new Tank { type = Types[k], row = 1, o = Unit("vehicle", Types[k], me, x0 + 1 + 6 * k, y0 + 7) });
            }
            shooter = Unit("vehicle", "light", me, x0 + 24, y0 + 4);                                            // the heavy shooter the bursts are put down to (HeavyChips reads its type and where it stands)
            yield return Real(1.5f);
            foreach (var t in tanks) Capture(t);
            bool shut = tanks.All(t => !BoardingView.HatchLidOpenShare(t.o != null ? t.o.Id : -1, out float sh) || sh <= 0.01f);
            Log("0 staged: a red light tank, medium tank, heavy tank and mech, twice, each crewed, each with its hatch lid read under its hinge, the lids shut",
                tanks.All(t => t.o != null && t.v && t.lid.Count > 0 && t.o.Drivers.Count > 0) && shooter != null && shut,
                $"tiles ({x0}, {y0}) .. ({x0 + 25}, {y0 + 10}); " + string.Join("; ", tanks.Select(t => $"{Kind(t.type)} #{(t.o != null ? t.o.Id : -1)} row {(t.row == 0 ? "A" : "B")}: "
                    + (t.lid.Count > 0 ? string.Join(" + ", t.lid.Select(r => r.name)) + " under " + t.hinge.name : "NO LID FOUND"))));
            foreach (var t in tanks) Restart(t);
            float restFrom = Time.time;

            var rowA = tanks.Where(t => t.row == 0 && t.o != null && t.v).ToList();
            var rowB = tanks.Where(t => t.row == 1 && t.o != null && t.v).ToList();

            // 1 heavy hits on the shut lid
            foreach (var t in rowA)
            {
                Restart(t);
                int knocks0 = RobotPopDeath.KnockOffs, over0 = OzHatchLid.PassedOver, fixes0 = BoardingView.LidGuardRepairs;
                yield return Storm(t);
                yield return Real(WatchAfterSec);
                Log($"1 {Kind(t.type)} #{t.o.Id}: heavy hits on the SHUT lid (the knock-off aimed at it + {Bursts} HC1 bursts): the lid stays, shut",
                    t.frames > 0 && t.missing == 0,
                    $"{Counts(t)}; parts knocked off this hull {RobotPopDeath.KnockOffs - knocks0}, hatch parts passed over {OzHatchLid.PassedOver - over0}, guard repairs {BoardingView.LidGuardRepairs - fixes0}");
            }
            yield return Picture(PicName("hatch_lid_row_A_after_hits.png"));

            // 2 EXIT DRIVER and back in
            yield return ExitAndBack(rowA);

            // 3 damage stages
            foreach (var t in rowA) Restart(t);
            foreach (var t in rowA) if (!t.o.Destroyed) t.o.SetHealth(Mathf.Max(1, t.o.MaxHealth * 40 / 100));
            yield return Real(1.5f);
            foreach (var t in rowA) if (!t.o.Destroyed) t.o.SetHealth(Mathf.Max(1, t.o.MaxHealth * 15 / 100));
            yield return Real(2.5f);
            foreach (var t in rowA) if (!t.o.Destroyed) t.o.SetHealth(t.o.MaxHealth);
            yield return Real(1.5f);
            foreach (var t in rowA)
                Log($"3 {Kind(t.type)} #{t.o.Id}: damage stages (smoke 40 %, burning 15 %, repaired): the lid stays", t.frames > 0 && t.missing == 0 && !t.o.Destroyed, Counts(t));

            // 4 OzFpsView in and out
            foreach (var t in rowA)
            {
                Restart(t);
                if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check", false, true);
                input.Selection.Clear(); if (!t.o.Destroyed && t.o.Drivers.Count > 0) input.Selection.Add(t.o);
                yield return null;
                bool inside = input.Selection.Count == 1 && OzFpsView.TryEnter(input);
                string refused = inside ? "" : OzFpsView.LastRefusal;
                yield return Real(ViewHoldSec);
                if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check");
                yield return Real(1f);
                Log($"4 {Kind(t.type)} #{t.o.Id}: OzFpsView in and out: nothing of the lid held after the pictures, before or after the leave", inside && t.frames > 0 && t.missing == 0,
                    $"{(inside ? "in for " + ViewHoldSec.ToString("0.0") + " s" : "NOT IN (" + refused + ")")}; {Counts(t)}");
            }
            input.Selection.Clear();

            // 5 heavy hits on the open lid (the idle crew rests in the open hatch: DV75)
            while (Time.time - restFrom < RestWaitMaxSec && rowB.Any(t => !Open(t))) yield return null;
            var opened = rowB.Where(Open).ToList();
            Note($"5: {opened.Count} of {rowB.Count} row B crews rest in the open hatch after {Time.time - restFrom:0.0} s");
            if (opened.Count > 0) yield return Picture(PicName("hatch_lid_row_B_open_before_hits.png"));
            foreach (var t in rowB)
            {
                Restart(t);
                bool open = Open(t);
                if (!open) { Log($"5 {Kind(t.type)} #{t.o.Id}: heavy hits on the OPEN lid", false, $"its crew never rested in the open hatch in {RestWaitMaxSec:0} s (an enemy near? an order?): no sample"); continue; }
                int knocks0 = RobotPopDeath.KnockOffs, over0 = OzHatchLid.PassedOver, fixes0 = BoardingView.LidGuardRepairs;
                yield return Storm(t);
                yield return Real(WatchAfterSec);
                BoardingView.HatchLidOpenShare(t.o.Id, out float share);
                Log($"5 {Kind(t.type)} #{t.o.Id}: heavy hits on the OPEN lid (the knock-off aimed at it + {Bursts} HC1 bursts): the lid stays, open",
                    t.frames > 0 && t.missing == 0,
                    $"{Counts(t)}; open share after {share:0.00}; parts knocked off this hull {RobotPopDeath.KnockOffs - knocks0}, hatch parts passed over {OzHatchLid.PassedOver - over0}, guard repairs {BoardingView.LidGuardRepairs - fixes0}");
            }
            if (opened.Count > 0) yield return Picture(PicName("hatch_lid_row_B_open_after_hits.png"));

            // 7 (review fix) a hull hidden WHOLE by its own code, as the level 16 ending scene does (ZodBriefing.EndingScene.cs: every renderer
            // of the units near its set switched off): the guard must leave its lid hidden with it (no lone lid floating in the shot)
            var hid = rowA.FirstOrDefault(t => t.o != null && !t.o.Destroyed && t.v && t.lid.Count > 0);
            if (hid != null)
            {
                hid.watch = false;
                var off = new List<Renderer>();
                foreach (var r in hid.v.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; off.Add(r); }
                int lone = 0, seen = 0, hidden0 = BoardingView.LidGuardHullHidden, fixes0 = BoardingView.LidGuardRepairs;
                for (int f = 0; f < 30; f++)
                {
                    yield return new WaitForEndOfFrame();                                                        // after every LateUpdate (the guard's at 97) of this frame
                    seen++; foreach (var r in hid.lid) if (r && r.enabled && r.gameObject.activeInHierarchy) { lone++; break; }
                }
                foreach (var r in off) if (r) r.enabled = true;
                yield return null;
                int left = BoardingView.LidGuardHullHidden - hidden0;
                Log($"7 {Kind(hid.type)} #{hid.o.Id}: the whole hull hidden by its own code (as the level 16 ending scene does): its lid stays hidden with it, nothing put back",
                    seen > 0 && lone == 0 && BoardingView.LidGuardRepairs == fixes0 && (Mode == 1 || left > 0),
                    $"{lone} of {seen} frames with a lone lid drawn; the guard left the hidden hull alone {left} times; repairs meanwhile {BoardingView.LidGuardRepairs - fixes0}; {off.Count} renderers switched off and back on");
                Restart(hid);
            }

            // 6 the guard
            int repairs = BoardingView.LidGuardRepairs;
            string guardText = $"guard repairs this Play {repairs} (switched on {BoardingView.LidGuardShown}, renderer on {BoardingView.LidGuardEnabled}, scale {BoardingView.LidGuardScale}, pose {BoardingView.LidGuardPose}, "
                               + $"not a number {BoardingView.LidGuardNaN}; parts destroyed {BoardingView.LidGuardGone}; hidden hulls left alone {BoardingView.LidGuardHullHidden}); last: {(BoardingView.LidGuardLast.Length > 0 ? BoardingView.LidGuardLast : "-")}; knock-offs this Play {RobotPopDeath.KnockOffs}, hatch parts passed over {OzHatchLid.PassedOver}";
            if (Mode == 0) Log("6 the guard had nothing to put back (the fix alone keeps every lid; the knock-off still takes other parts)", repairs == 0, guardText);
            else if (Mode == 2) Log("6 the guard put back the lids the 1.4 knock-off took", repairs > 0, guardText);
            else Note("6 (BEFORE run, no guard): " + guardText);
        }

        bool Open(Tank t) => t.o != null && BoardingView.HatchLidOpenShare(t.o.Id, out float s) && s >= 0.95f;

        /// <summary>The knock-off aimed at the lid's middle (its own rule picks the nearest small part), then the heavy bursts on the lid through
        /// HeavyChips.OnBlast (a light tank's shell from the shooter's side), two frames apart.</summary>
        IEnumerator Storm(Tank t)
        {
            if (t.o == null || !t.v || t.lid.Count == 0 || shooter == null) yield break;
            var c = LidCentre(t);
            var from = g.Views.TryGetValue(shooter.Id, out var sv) && sv ? sv.transform.position : c + Vector3.right * 8f;
            var dir = from - c; dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.right;
            RobotPopDeath.KnockOff(t.v.transform, t.o.Id, c, from, (uint)(7919 + 31 * t.o.Id));
            yield return null;
            for (int i = 0; i < Bursts; i++)
            {
                if (t.o.Destroyed || !t.v) yield break;
                c = LidCentre(t);
                var at = c + dir * 0.25f + Vector3.up * 0.05f;                                                 // on the lid's side facing the shooter
                var e = new ZEvent { type = ZEventType.MissileExploded, objId = shooter.Id, otherId = t.o.Id, x = t.o.CenterX, y = t.o.CenterY, radius = 8 };
                HeavyChips.OnBlast(g, e, shooter, t.o, at);
                yield return null; yield return null;
            }
        }

        /// <summary>Row 2: each driver of row A let out (the HUD's EXIT DRIVER: ZWorld.Eject), then ordered back in (the player's ENTER order).</summary>
        IEnumerator ExitAndBack(List<Tank> row)
        {
            if (!ZWorld.VehicleExitDeviation)
            {
                foreach (var t in row) Log($"2 {Kind(t.type)} #{t.o.Id}: EXIT DRIVER and back in", false, "EXIT DRIVER is off in this session ('Zod exact'): start a level in the normal mode");
                yield break;
            }
            foreach (var t in row) Restart(t);
            int exits0 = W.DriverExits;
            var asked = new Dictionary<Tank, bool>();
            foreach (var t in row) { asked[t] = !t.o.Destroyed && W.Eject(me, t.o); yield return null; }
            var seen = new Dictionary<Tank, ZObject>();                                                      // 1.5 pass 4: the driver as he gets out (a mech's driver loses DriverOutFromId
            float tw = Time.realtimeSinceStartup;                                                            // once his climb-down hold ends, so the search after the wait found none)
            while (Time.realtimeSinceStartup - tw < ExitWaitSec)
            {
                foreach (var t in row)
                    if (!seen.ContainsKey(t))
                        foreach (var o in W.Objects)
                            if (o.IsRobot && o.Owner == me && !o.Destroyed && o.KillAt < 0 && o.DriverOutFromId == t.o.Id) { seen[t] = o; break; }
                yield return null;
            }
            var robots = new Dictionary<Tank, ZObject>();
            foreach (var t in row)
            {
                ZObject rob = seen.TryGetValue(t, out var sr) && sr != null && !sr.Destroyed && sr.KillAt < 0 ? sr : null;
                if (rob == null)
                foreach (var o in W.Objects)
                    if (o.IsRobot && o.Owner == me && !o.Destroyed && o.KillAt < 0 && (o.DriverOutFromId == t.o.Id || (o.Id > startMaxId && o.DriverOutAt >= 0 && Mathf.Abs(o.CenterX - t.o.CenterX) < 96 && Mathf.Abs(o.CenterY - t.o.CenterY) < 96 && !robots.ContainsValue(o))))
                    { rob = o; break; }
                robots[t] = rob;
                if (rob != null) { made.Add(rob); W.SendWaypoints(me, rob, new[] { new ZWaypoint { mode = WpMode.Enter, refId = t.o.Id, x = t.o.CenterX, y = t.o.CenterY } }); }
            }
            float t0 = Time.time;
            while (Time.time - t0 < BoardWaitMaxSec && row.Any(t => !t.o.Destroyed && t.o.Drivers.Count == 0 && robots[t] != null)) yield return null;
            yield return Real(2.5f);                                                                           // the boarding's lid shut again
            Note($"2: drivers let out {W.DriverExits - exits0}");
            foreach (var t in row)
            {
                bool back = t.o.Drivers.Count > 0 && t.o.Owner == me;
                Log($"2 {Kind(t.type)} #{t.o.Id}: EXIT DRIVER (the lid kicked open, shut, the hull neutral), the driver ordered back in (open, shut, red again): the lid never missing",
                    asked[t] && robots[t] != null && back && t.frames > 0 && t.missing == 0,
                    $"let out {asked[t]}, his robot {(robots[t] != null ? "#" + robots[t].Id : "NOT FOUND")}, back in {back}; {Counts(t)}");
            }
        }
    }
}
#endif
