#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// Play, the unit editor open, a few minutes, hands off the mouse and the keyboard). The rules have their own count (menu 3a). Here what only the drawn game can show:
    ///  P0 the palette: two tiles right of NUKE, names, pictures, one per drop, always neutral;
    ///  P1 every robot, vehicle, gun and the heroes dropped on both decks stand ON them (root at the deck, never under it, the rules' height the same); on the ramp the slope, a vehicle tilted with it;
    ///  P2 placing: real clicks on the deck, on a wall's face and a rail land on the deck; the far kerb and rail by a real camera ray: on the deck, the platform under the pointer; a gun on the
    ///     ramp goes to the deck; flag, crate and obstacle refused with their words; a drop beside a platform stays on the ground; a drop onto a gun stands beside it; a platform over units is refused;
    ///  P3 shots up and down, each weapon from the high deck at the ground and from the ground at the deck: rounds fired, the drawn end's height at the target, never at the wall's foot;
    ///  P4 the same shots: the barrel's worst error at the shot against its own limit, no wait timed out;
    ///  P5 the shuttle over both platforms with the mech on the deck: the least gap between the ship and anything under it, measured on the drawn renderers;
    ///  P6 destroyed with one of each on it and an older wreck: the deck goes first, every view comes down, nothing above the floor after 6 s, no loose piece resting in the air, three break sounds;
    ///     scorch marks on the deck go, a turret thrown onto it from beside it goes, one in the air toward it lands on the floor; burning ground on the deck goes, a patch beside the wall
    ///     comes down to the floor; a settled body on the deck whose root stands beside the wall goes;
    ///  P7 save and load: same platforms, ramp sides, HP, units on the decks; no key with no platform;
    ///  P8 Delete takes what stands on it quietly, and a wreck, a body, a mark and burning ground on the deck with it; CLEAR FIELD leaves nothing, tiles open;
    ///  P9 the mouse: a plain right-click on a deck sends a tank up the ramp, on the far kerb it is a move on the deck, Ctrl + right-click attacks the platform, units on the deck can be picked, marks on the deck.
    /// Pictures for the eye go to the Folder: the ramp's crest, shots up and down, the collapse.
    /// </summary>
    [DefaultExecutionOrder(-37)]                                                                         // after OzUeField, before CombatView clears the frame's events
    public sealed class OzUePlatformCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ PlatformCheck]";
        public static string Report = "not run";
        public static string Folder;
        public static bool Running => run;
        static OzUePlatformCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();
        readonly List<string> pictures = new List<string>();
        Vector2 lastPos; bool lBtn, rBtn, hidden0;
        OzUnitEditor ed; ZGame g; ZWorld w;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_PlatformCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUePlatformCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { Route(false); if (run == this) run = null; }

        bool routed; InputSettings.EditorInputBehaviorInPlayMode route0; InputSettings.BackgroundBehavior back0;
        void Route(bool on)
        {
            var s = InputSystem.settings;
            if (on && !routed) { routed = true; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior; s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
            else if (!on && routed) { routed = false; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0; }
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        void Move(Vector2 p)
        {
            var m = Mouse.current; if (m == null) return;
            var st = new MouseState { position = p, delta = p - lastPos };
            if (lBtn) st = st.WithButton(MouseButton.Left);
            if (rBtn) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(m, st); lastPos = p;
        }

        IEnumerator Click(Vector2 p, bool right = false)
        {
            Move(p); yield return Frames(2);
            if (right) rBtn = true; else lBtn = true;
            Move(p); yield return Frames(2);
            if (right) rBtn = false; else lBtn = false;
            Move(p); yield return Frames(3);
        }

        static void Keys(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }
        static Vector2 ToScreen(Vector3 world) { var cam = OzUeDraw.Cam; return cam ? (Vector2)cam.WorldToScreenPoint(world) : Vector2.zero; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;

        IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            Directory.CreateDirectory(Folder);
            string b = name + "_" + Screen.width + "x" + Screen.height, path = Path.Combine(Folder, b + ".png");
            int n = 1; while (File.Exists(path)) path = Path.Combine(Folder, b + "_" + (++n) + ".png");      // never over an older picture
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            pictures.Add(path);
        }

        static Vector3 Focus(OzUnitEditor e)
        {
            Vector3 f = e && e.Rts ? e.Rts.focus : new Vector3(160, 0, -100);
            var map = e && e.World != null ? e.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 70f, map.width * 2f - 70f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); }
            f.y = 0f; return f;
        }

        UnitView View(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        Vector3 Deck(ZWorld.PlatformInfo p, float across, float along) => OzUePlatformViews.Point(p, across, along, (float)p.HeightAt(along));
        ZObject Heal(ZObject o) { if (o != null) { o.MaxHealth = o.Health = 900000000; } return o; }

        static float Top(UnitView v)
        {
            float top = float.NegativeInfinity;
            if (v) foreach (var r in v.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) top = Mathf.Max(top, r.bounds.max.y);
            return top;
        }

        // the frame's rounds of the pair under test (P3, P4)
        ZObject shotFrom, shotAt; int rounds, blasts; float worstY; bool lowEnd;
        void Update()
        {
            if (w == null || shotFrom == null) return;
            var ev = w.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i]; bool mine = e.objId == shotFrom.Id || (shotFrom.Minions != null && shotFrom.Minions.Exists(m => m != null && m.Id == e.objId));
                if (!mine) continue;
                if (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired) rounds++;
                if (e.type == ZEventType.MissileExploded && shotAt != null && View(shotAt))
                {
                    var p = ZGame.PxToWorld(e.x, e.y); p.y = g.GroundY(p.x, p.z);
                    var to = OzShuttleAim.AirAim(shotAt, p, 0f);
                    float ty = View(shotAt).transform.position.y, dy = Mathf.Abs(to.y - ty);
                    blasts++; worstY = Mathf.Max(worstY, dy);
                    if (ty > 2f && to.y < ty - 1.5f) lowEnd = true;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the run
        IEnumerator Main()
        {
            Report = "running"; Route(true);
            float tStart = Time.unscaledTime;
            ed = OzUnitEditor.I; g = ed.Game; w = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            hidden0 = OzUnitEditor.PanelsHidden; if (hidden0) OzUnitEditor.SetPanelsHidden(false);
            OzUePlace.Stop(); OzUePlayground.ClearField(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            var f = Focus(ed);
            OzUeControls.CentreOn(f);
            yield return Seconds(1f);
            Info("Game view " + Screen.width + " x " + Screen.height + ", camera turn " + OzUePlayground.PlatformDirNow);

            P0();
            var mid = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, f + new Vector3(-30f, 0f, 0f), 1));
            var high = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, f + new Vector3(30f, 0f, 0f), 1));
            yield return Frames(4);
            if (mid == null || high == null) { Check(false, "P1 both platforms drop on the free field"); yield return Finish(tStart, playing0, speed0); yield break; }
            var mi = w.PlatformOf(mid.Id); var hi = w.PlatformOf(high.Id);
            var deckM = Deck(mi, 5f, 5f); var deckH = Deck(hi, 5f, 5f);
            Check(Mathf.Abs(g.GroundY(deckM.x, deckM.z) - 4.5f) < 0.02f && Mathf.Abs(g.GroundY(deckH.x, deckH.z) - 9.25f) < 0.02f && OzUePlatformViews.Count == 2,
                  "P1 ZGame.GroundY over the decks: " + g.GroundY(deckM.x, deckM.z).ToString("0.00") + " m and " + g.GroundY(deckH.x, deckH.z).ToString("0.00") + " m; the same numbers in the rules");
            OzUeControls.CentreOn(deckH);
            yield return Seconds(0.5f); yield return Shot("platforms_standing");
            OzUeControls.CentreOn(Deck(hi, 5f, 9f)); yield return Seconds(0.3f); yield return Shot("ramp_crest");

            yield return P1(mi); yield return P1(hi);
            yield return P2(mi, hi);
            yield return P34(hi);
            yield return P5(mi, hi, f);
            OzUePlayground.ClearField(); yield return Frames(4);
            yield return P9(f);
            OzUePlayground.ClearField(); yield return Frames(4);
            yield return P7(f);
            OzUePlayground.ClearField(); yield return Frames(4);
            yield return P6(f);
            OzUePlayground.ClearField(); yield return Frames(4);
            yield return P8(f);
            yield return Finish(tStart, playing0, speed0);
        }

        void P0()
        {
            foreach (var id in new[] { OzUePlayground.PlatformMid, OzUePlayground.PlatformHigh })
            {
                bool high = id == OzUePlayground.PlatformHigh;
                Check(OzUePlayground.KindOf(id) == UeKind.Platform && OzUePlayground.ShortName(id) == (high ? "TOWER" : "PLATFORM") && OzUePlayground.FullName(id) == (high ? "HIGH PLATFORM" : "MEDIUM PLATFORM")
                      && OzUePlayground.ClassWord(id) == "Platform" && OzUePlayground.AlwaysNeutral(id) && !OzUePlayground.CanBeNeutral(id) && OzUePlayground.Formation(id, 5, Vector3.zero, 0f).Count == 1
                      && System.Array.IndexOf(OzUePlayground.Extras, id) == (high ? 5 : 4) && OzUePlayground.UnitsOfGroup(id, -1) == 0 && OzUePlayground.Offered(id, out _) && OzUeArt.Picture(id, Team.Neutral) != null,
                      "P0 " + id + ": extra " + (high ? 6 : 5) + ", its names, always neutral, one per drop, no unit of the cap, its picture");
                var plate = GameObject.Find("UE_Dock");
                var tile = plate ? plate.transform.Find("Tile_" + id) as RectTransform : null; var nuke = plate ? plate.transform.Find("Tile_" + OzUePlayground.Nuke) as RectTransform : null;
                Check(tile && nuke && tile.gameObject.activeInHierarchy && tile.anchoredPosition.x > nuke.anchoredPosition.x, "P0 " + id + "'s tile stands in the dock right of NUKE");
            }
        }

        IEnumerator P1(ZWorld.PlatformInfo p)
        {
            var ids = new List<string>(OzUePlayground.Robots); ids.AddRange(OzUePlayground.Vehicles); ids.AddRange(OzUePlayground.Guns); ids.Add(OzUePlayground.Heroes);
            int ok = 0; var bad = new List<string>();
            var at = Deck(p, 5f, 5f);
            foreach (var id in ids)
            {
                var o = Lead(OzUePlayground.Drop(id, Team.Red, at, 1));
                yield return Frames(6);
                if (o == null) { bad.Add(id + " refused"); continue; }
                var members = new List<ZObject> { o }; members.AddRange(o.Minions);
                bool good = true; float worst = 0f;
                foreach (var m in members)
                {
                    var v = View(m); if (!v) { good = false; continue; }
                    var c = v.transform.position; float floor = g.GroundY(c.x, c.z), dy = c.y - (float)p.DeckM;
                    worst = Mathf.Abs(dy) > Mathf.Abs(worst) ? dy : worst;
                    if (dy < -0.15f || dy > 0.7f || Mathf.Abs(floor - (float)p.DeckM) > 0.05f || System.Math.Abs(w.PlatformHeightM(m.CenterX, m.CenterY) - p.DeckM) > 1e-6) good = false;
                }
                if (good) ok++; else bad.Add(id + " (" + worst.ToString("0.00") + " m)");
                OzUePlayground.Remove(o); yield return Frames(1);
            }
            Check(bad.Count == 0, "P1 " + p.DeckM.ToString("0.##") + " m deck: " + ok + " of " + ids.Count + " kinds stand on it, root at the deck, not inside" + (bad.Count > 0 ? "; wrong: " + string.Join(", ", bad) : ""));
            foreach (var id in new[] { "grunt", "light", "heavy" })
            {
                var r = Deck(p, 5f, p.Rows - p.RampRows * 0.5f);
                var o = Lead(OzUePlayground.Drop(id, Team.Red, r, 1));
                yield return Seconds(0.5f);
                var v = View(o);
                float want = v ? (float)w.PlatformHeightM(o.CenterX, o.CenterY) : -9f, dy = v ? v.transform.position.y - want : 9f, tilt = v ? Vector3.Angle(v.transform.up, Vector3.up) : 0f;
                Check(o != null && v && dy > -0.15f && dy < 0.8f && (o.Kind == ZKind.Robot || (tilt > 10f && tilt < 24f)), "P1 " + id + " dropped on the ramp: root " + dy.ToString("0.00") + " m over the slope, tilt " + tilt.ToString("0.0") + " deg");
                if (o != null) OzUePlayground.Remove(o);
                yield return Frames(1);
            }
        }

        IEnumerator P2(ZWorld.PlatformInfo mi, ZWorld.PlatformInfo hi)
        {
            var deck = Deck(hi, 4f, 4f);
            OzUeControls.CentreOn(Deck(hi, 5f, 6f)); yield return Seconds(0.4f);
            // real clicks: armed tile, a press on the field
            OzUePlace.ToggleArm("grunt", Team.Red); yield return Frames(3);
            int n0 = OzUePlayground.OnField("grunt");
            yield return Click(ToScreen(deck)); yield return Frames(4);
            Check(OzUePlayground.OnField("grunt") > n0 && UnitOnDeck("grunt", hi), "P2 a click on the deck: the squad stands on the deck");
            OzUePlace.Stop(); OzUePlayground.ClearUnits(); yield return Frames(4);
            var wall = OzUePlatformViews.Point(hi, 0f, 5f, 4f); var cam = OzUeDraw.Cam;
            if (cam && Vector3.Dot(cam.transform.forward, wall - Deck(hi, 10f, 5f)) < 0f) wall = OzUePlatformViews.Point(hi, 10f, 5f, 4f);
            bool floorOk = OzUePlaceDriver.FloorPoint(ed, ToScreen(wall), out var wp);
            var onWall = Lead(OzUePlayground.Drop("light", Team.Red, wp, 1)); yield return Frames(6);
            byte part = onWall != null ? w.PlatformPartAt(onWall.CenterX >> 4, onWall.CenterY >> 4) : (byte)0;
            Check(floorOk && onWall != null && System.Math.Abs(w.PlatformHeightM(onWall.CenterX, onWall.CenterY) - hi.DeckM) < 1e-6 && part == ZWorld.PlatPartDeck, "P2 a drop on a wall's face lands on the deck, part " + part);
            OzUePlayground.ClearUnits(); yield return Frames(3);
            var rail = OzUePlatformViews.Point(hi, 0.5f, 5f, 9.25f);
            var onRail = Lead(OzUePlayground.Drop("grunt", Team.Red, rail, 1)); yield return Frames(4);
            Check(onRail != null && w.PlatformPartAt(onRail.CenterX >> 4, onRail.CenterY >> 4) == ZWorld.PlatPartDeck, "P2 a drop on a rail lands on the deck");
            OzUePlayground.ClearUnits(); yield return Frames(3);
            // the far side by a real camera ray (check 2): the walk surface stops 1 m in, the ray would land on the floor behind the wall
            foreach (var (edge, name) in new[] { (OzUePlatformViews.Point(hi, 5f, 0.1f, (float)hi.DeckM + OzUePlatformView.KerbM), "kerb"), (OzUePlatformViews.Point(hi, 5f, 0.7f, (float)hi.DeckM), "rail") })
            {
                var sp = ToScreen(edge);
                bool got = OzUePlaceDriver.FloorPoint(ed, sp, out var ep);
                var onEdge = Lead(OzUePlayground.Drop("grunt", Team.Red, ep, 1)); yield return Frames(4);
                var ev = View(onEdge);
                Check(got && onEdge != null && w.PlatformPartAt(onEdge.CenterX >> 4, onEdge.CenterY >> 4) == ZWorld.PlatPartDeck && ev && ev.transform.position.y > 9f, "P2 a drop by the camera's ray on the far " + name + " lands on the deck, y " + (ev ? ev.transform.position.y.ToString("0.00") : "none"));
                Check(OzUePlatformViews.Under(sp) == hi.O, "P2 the far " + name + " under the pointer is the platform");
                OzUePlayground.ClearUnits(); yield return Frames(3);
            }
            foreach (var at in new[] { Deck(hi, 5f, hi.Rows - 1f), Deck(hi, 5f, 10f), Deck(mi, 5f, mi.Rows - 1f) })
            {
                var gun = Lead(OzUePlayground.Drop("gun", Team.Red, at, 1)); yield return Frames(4);
                var pl = at.x == Deck(mi, 5f, mi.Rows - 1f).x ? mi : hi;
                bool on = gun != null && gun.LocX >= pl.TileX * 16 && w.PlatformPartAt(gun.LocX >> 4, gun.LocY >> 4) == ZWorld.PlatPartDeck && w.PlatformPartAt((gun.LocX + 31) >> 4, (gun.LocY + 31) >> 4) == ZWorld.PlatPartDeck;
                Check(on, "P2 a gun dropped on the ramp stands on the deck");
                OzUePlayground.ClearUnits(); yield return Frames(3);
            }
            foreach (var (id, words) in new[] { (OzUePlayground.Flag, "NO ROOM: NOT ON A PLATFORM"), (OzUePlayground.Grenades, "NO ROOM: NOT ON A PLATFORM"), (OzUePlayground.Obstacle, "NO ROOM: NOT ON A PLATFORM") })
            {
                bool can = OzUePlayground.CanDrop(id, Team.Neutral, deck, 1, out string why);
                var d = OzUePlayground.Drop(id, Team.Neutral, deck, 1);
                Check(!can && !d.ok && why == words, "P2 " + id + " on a deck is refused: " + why);
            }
            var beside = OzUePlatformViews.Point(hi, -2f, 5f, 0f);
            var near = Lead(OzUePlayground.Drop("grunt", Team.Blue, beside, 1)); yield return Frames(5);
            var nv = View(near);
            Check(near != null && w.PlatformAtTile(near.CenterX >> 4, near.CenterY >> 4) == 0 && nv && nv.transform.position.y < 0.6f, "P2 a drop beside the wall stays on the ground");
            OzUePlayground.ClearUnits(); yield return Frames(3);
            var gunG = Lead(OzUePlayground.Drop("gun", Team.Red, OzUePlatformViews.Point(hi, -12f, 5f, 0f), 1)); yield return Frames(3);
            var onGun = Lead(OzUePlayground.Drop("grunt", Team.Red, g.CenterWorld(gunG), 1)); yield return Frames(3);
            bool apart = gunG != null && onGun != null && !gunG.IntersectsObject(onGun);
            Check(apart, "P2 a squad dropped onto a gun stands beside it, not inside it");
            OzUePlayground.ClearUnits(); yield return Frames(3);
            var inRoad = Lead(OzUePlayground.Drop("grunt", Team.Red, OzUePlatformViews.Point(hi, 5f, hi.Rows + 12f, 0f), 1)); yield return Frames(2);
            bool can2 = OzUePlayground.CanDrop(OzUePlayground.PlatformHigh, Team.Neutral, g.CenterWorld(inRoad), 1, out string why2);
            Check(!can2 && why2 == "NO ROOM: A PLATFORM CANNOT OVERLAP UNITS", "P2 a platform over units is refused: " + why2);
            OzUePlayground.ClearUnits(); yield return Frames(3);
        }

        bool UnitOnDeck(string type, ZWorld.PlatformInfo p)
        {
            foreach (var o in w.Objects) if (o.Type == type && !o.Destroyed && o.KillAt < 0 && w.PlatformAtTile(o.CenterX >> 4, o.CenterY >> 4) == p.O.Id) return true;
            return false;
        }

        IEnumerator P34(ZWorld.PlatformInfo hi)
        {
            OzUeControls.SetSpeed(2f);
            OzUeControls.CentreOn(Deck(hi, 0f, 5f));
            yield return Seconds(0.3f);
            string[] types = { "grunt", "tough", "sniper", "pyro", "laser", "light", "heavy", "medium", "missile_launcher", "gatling", "gun", "howitzer", "missile_cannon" };
            foreach (var type in types)
                foreach (bool down in new[] { true, false })
                {
                    OzUePlayground.ClearUnits(); yield return Frames(3);
                    bool robot = System.Array.IndexOf(OzUePlayground.Robots, type) >= 0;
                    var row = w.S.Unit(type);
                    float reach = Mathf.Clamp((row != null ? row.attack_radius : 120) * 0.8f / 16f, 5f, 8.5f);          // tiles: inside its range, at most 17 m out
                    var high = Deck(hi, 2.5f, 5f); var low = OzUePlatformViews.Point(hi, 2.5f - reach, 5f, 0f);
                    string foe = "heavy";
                    var a = Lead(OzUePlayground.Drop(type, Team.Red, down ? high : low, 1));
                    var t = Lead(OzUePlayground.Drop(foe, Team.Blue, down ? low : high, 1));
                    yield return Frames(4);
                    if (a == null || t == null) { Check(false, "P3 " + type + " set up"); continue; }
                    Heal(a); Heal(t); t.Damage = 0; t.MoveSpeed = 0; a.MoveSpeed = 0; foreach (var m in a.Minions) { Heal(m); m.MoveSpeed = 0; }
                    w.SendWaypoints(Team.Red, a, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY, playerGiven = true } });
                    shotFrom = a; shotAt = t; rounds = blasts = 0; worstY = 0f; lowEnd = false;
                    int hp0 = t.Health; float t0 = Time.realtimeSinceStartup;
                    bool pic = false;
                    while (Time.realtimeSinceStartup - t0 < (type == "howitzer" || type == "missile_launcher" ? 7f : 4f))
                    {
                        if (!pic && rounds > 0) { pic = true; yield return Shot("shot_" + type + (down ? "_down" : "_up")); }
                        yield return null;
                    }
                    shotFrom = null;
                    var aim = View(a) ? View(a).GetComponent<UnitAim>() : null;
                    float err = aim ? aim.MaxFireError : -1f, tol = UnitAim.SlewFor(type, robot ? ZKind.Robot : a.Kind).tol;
                    int lost = hp0 - t.Health;
                    string way = type + (down ? " from the high deck at the ground" : " from the ground at the high deck");
                    Check((rounds > 0 || lost > 0) && (lost > 0 || blasts > 0) && !lowEnd && (blasts == 0 || worstY <= 1.5f), "P3 " + way + ": " + rounds + " rounds, " + lost + " HP, " + blasts + " blasts, drawn end within " + worstY.ToString("0.00") + " m of the target's height" + (lowEnd ? ", ONE AT THE WALL'S FOOT" : ""));
                    Check(aim == null || err <= tol + 2f, "P4 " + way + ": the barrel's worst error at a shot " + err.ToString("0.0") + " deg, its limit " + tol.ToString("0") + " deg + 2");
                }
            OzUeControls.SetSpeed(1f);
            OzUePlayground.ClearUnits(); yield return Frames(3);
        }

        IEnumerator P5(ZWorld.PlatformInfo mi, ZWorld.PlatformInfo hi, Vector3 f)
        {
            OzUeControls.SetSpeed(2f);
            var mech = Lead(OzUePlayground.Drop("medium", Team.Blue, Deck(hi, 5f, 5f), 1));
            var mech2 = Lead(OzUePlayground.Drop("medium", Team.Blue, Deck(mi, 5f, 5f), 1));
            if (mech != null) { mech.Damage = 0; mech.MoveSpeed = 0; } if (mech2 != null) { mech2.Damage = 0; mech2.MoveSpeed = 0; }
            var from = Deck(mi, 5f, 5f) + new Vector3(-45f, 0f, 0f); from.y = 0f; var to = Deck(hi, 5f, 5f) + new Vector3(45f, 0f, 0f); to.y = 0f;
            var ship = Lead(OzUePlayground.Drop(OzUePlayground.Shuttle, Team.Red, from, 1));
            yield return Seconds(1f);
            if (ship == null) { Check(false, "P5 the shuttle drops beside the platforms"); yield break; }
            ship.Health = ship.MaxHealth;
            var view = OzPlayableShuttle.Of(ship);
            float least = 99f, top = 0f, deckGap = 99f; int over = 0; float t0 = Time.realtimeSinceStartup; bool shotDone = false;
            foreach (var (a, b) in new[] { (from, to), (to, from) })
            {
                var px = ZGame.WorldToPx(b);
                w.SendWaypoints(Team.Red, ship, new[] { ZWaypoint.Move(px.x, px.y, false) });
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 45f)
                {
                    yield return null;
                    if (!view) break;
                    var bo = new Bounds(); bool any = false;
                    foreach (var r in view.GetComponentsInChildren<Renderer>()) { if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue; if (!any) { bo = r.bounds; any = true; } else bo.Encapsulate(r.bounds); }
                    if (!any) continue;
                    top = Mathf.Max(top, view.transform.position.y);
                    foreach (var (o, p) in new[] { (mech, hi), (mech2, mi) })
                    {
                        Vector3 c0 = OzUePlatformViews.Point(p, 0f, 0f, 0f), c1 = OzUePlatformViews.Point(p, 10f, p.Rows, 0f);
                        float x0 = Mathf.Min(c0.x, c1.x) - bo.extents.x, x1 = Mathf.Max(c0.x, c1.x) + bo.extents.x, z0 = Mathf.Min(c0.z, c1.z) - bo.extents.z, z1 = Mathf.Max(c0.z, c1.z) + bo.extents.z;
                        var cs = bo.center;
                        if (cs.x < x0 || cs.x > x1 || cs.z < z0 || cs.z > z1) continue;
                        over++;
                        deckGap = Mathf.Min(deckGap, bo.min.y - (float)p.DeckM);
                        var mv = View(o);
                        if (mv && Mathf.Abs(mv.transform.position.x - cs.x) < bo.extents.x + 3f && Mathf.Abs(mv.transform.position.z - cs.z) < bo.extents.z + 3f) least = Mathf.Min(least, bo.min.y - Top(mv));
                    }
                    if (!shotDone && Mathf.Abs(view.transform.position.x - Deck(hi, 5f, 5f).x) < 8f) { shotDone = true; OzUeControls.CentreOn(Deck(hi, 5f, 5f)); yield return Shot("shuttle_over_high"); }
                    var q = ZGame.WorldToPx(view.transform.position);
                    if (new Vector2(q.x - px.x, q.y - px.y).magnitude < 40f) break;
                }
            }
            Check(over > 0 && deckGap >= 2.5f, "P5 the ship over the decks in " + over + " frames: the least gap to a deck " + deckGap.ToString("0.00") + " m");
            Check(over > 0 && least >= 1.5f, "P5 the least gap to the mech on a deck " + least.ToString("0.00") + " m (want 1.5 m)");
            Check(top <= 9.25f + OzUePlatformViews.TallM + 3.7f + 1.2f, "P5 the ship's highest root " + top.ToString("0.00") + " m: no 9 m climb over the rule height");
            OzUeControls.SetSpeed(1f);
        }

        // a dead tank's tossed turret: thrown onto the deck by hand, the debris point is the rules' own
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly MethodInfo LightF = typeof(OpenZPyroFire).GetMethod("IgniteGround", Priv);
        static readonly FieldInfo GroundF = typeof(OpenZPyroFire).GetField("ground", Priv);
        /// <summary>Burning ground at a point, by the game's own call. False = the fire system has not started (P3's pyro starts it).</summary>
        static bool Light(Vector3 at) { var f = OpenZPyroFire.I; if (!f || GroundF.GetValue(f) == null) return false; LightF.Invoke(f, new object[] { at, 1.3f, Time.time }); return true; }
        /// <summary>The highest flame root of the burning ground now (m), 0 with none.</summary>
        static float FlameTop()
        {
            float top = 0f; var gf = OpenZPyroFire.I ? GroundF.GetValue(OpenZPyroFire.I) as OpenZPyroGroundFire : null;
            if (gf != null) foreach (var p in gf.patches) if (p.active) foreach (var y in p.tY) top = Mathf.Max(top, y);
            return top;
        }
        static readonly FieldInfo TossedF = typeof(UnitView).GetField("tossed", Priv), TossToF = typeof(UnitView).GetField("tossTo", Priv), TossTF = typeof(UnitView).GetField("tossT", Priv), TossDurF = typeof(UnitView).GetField("tossDur", Priv);
        static Transform Tossed(UnitView v) => v ? TossedF.GetValue(v) as Transform : null;
        IEnumerator ThrowOnto(ZObject tank, Vector3 deck, float flight)
        {
            if (tank == null) yield break;
            tank.SetHealth(0); w.UpdateObjectHealth(tank);
            float t1 = Time.realtimeSinceStartup;
            while (!Tossed(View(tank)) && Time.realtimeSinceStartup - t1 < 2f) yield return null;
            var v = View(tank); if (!Tossed(v)) yield break;
            deck.y = g.GroundY(deck.x, deck.z) + 0.35f;
            TossToF.SetValue(v, deck); TossDurF.SetValue(v, (float)TossTF.GetValue(v) + flight);
        }

        IEnumerator P6(Vector3 f)
        {
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            var a = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, f + new Vector3(-30f, 0f, 0f), 1));
            var b = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, f + new Vector3(30f, 0f, 0f), 1));
            yield return Frames(3);
            var ai = w.PlatformOf(a.Id); var bi = w.PlatformOf(b.Id);
            var on = new List<ZObject>();
            void D(string id, Team t, Vector3 p) { var o = Lead(OzUePlayground.Drop(id, t, p, 1)); if (o != null) { on.Add(o); foreach (var m in o.Minions) on.Add(m); if (o.Damage > 0) o.Damage = 0; } }
            D("grunt", Team.Red, Deck(ai, 3f, 3f)); D("heavy", Team.Red, Deck(ai, 6f, 3f)); D("jeep", Team.Neutral, Deck(ai, 3f, 7f)); D("gun", Team.Blue, Deck(ai, 6f, 7f)); D("tough", Team.Blue, Deck(ai, 5f, 11f));
            D("tough", Team.Red, Deck(bi, 3f, 3f)); D("light", Team.Red, Deck(bi, 6f, 3f)); D("medium", Team.Blue, Deck(bi, 3f, 7f)); D("missile_cannon", Team.Blue, Deck(bi, 6f, 7f)); D("apc", Team.Red, Deck(bi, 5f, 13f));
            var old = Lead(OzUePlayground.Drop("crane", Team.Neutral, Deck(bi, 2f, 5f), 1));
            var outside = Lead(OzUePlayground.Drop("grunt", Team.Blue, OzUePlatformViews.Point(bi, -3f, 5f, 0f), 1));
            if (outside != null) outside.Damage = 0;
            var tankRest = Lead(OzUePlayground.Drop("light", Team.Red, OzUePlatformViews.Point(bi, 13f, 3f, 0f), 1)); var tankAir = Lead(OzUePlayground.Drop("light", Team.Red, OzUePlatformViews.Point(bi, 13f, 7f, 0f), 1));
            if (tankRest != null) tankRest.Damage = 0;
            if (tankAir != null) tankAir.Damage = 0;
            var thrown = Lead(OzUePlayground.Drop("grunt", Team.Blue, OzUePlatformViews.Point(bi, 13f, 11f, 0f), 1));
            if (thrown != null) thrown.Damage = 0;
            yield return Seconds(1f);
            if (old != null) { old.SetHealth(0); w.UpdateObjectHealth(old); }
            OzUeControls.CentreOn(Deck(bi, 5f, 5f));
            yield return ThrowOnto(tankRest, Deck(bi, 7f, 3f), 0.3f);
            ScorchDecals.Add(Deck(bi, 8f, 2f), 1.5f);
            yield return Seconds(2f);
            // a body blasted onto the deck from beside the wall: settled, kinematic, its root on the floor
            var vThrown = View(thrown);
            if (thrown != null) { thrown.LastMissileHit = w.Time; thrown.SetHealth(0); w.UpdateObjectHealth(thrown); }
            yield return Frames(3);
            var rag = vThrown ? vThrown.GetComponent<RobotRagdoll>() : null;
            if (vThrown && !rag) rag = RobotRagdoll.Launch(vThrown.gameObject, Vector3.right, 1f);
            var hips = rag ? rag.GetComponentInChildren<Rigidbody>() : null;
            if (rag) rag.Freeze();
            if (hips) hips.transform.position = Deck(bi, 8f, 5f) + Vector3.up * 0.3f;
            bool bodyUp = vThrown && hips && vThrown.gameObject.activeInHierarchy && vThrown.transform.position.y < 1f && hips.transform.position.y > 9f;
            var vRest = View(tankRest); bool restOn = Tossed(vRest) && Tossed(vRest).position.y > 9f;
            yield return ThrowOnto(tankAir, Deck(bi, 7f, 6f), 3f);
            var vAir = View(tankAir); bool airGoes = Tossed(vAir);
            float t0 = Time.unscaledTime; int marks0 = ScorchDecals.OverCleared; int fell0 = OzUePlatformViews.Fell, taken0 = OzUePlatformViews.LooseTaken, toast0 = OzUeField.EventToasts;
            var deckA = Deck(ai, 5f, 5f); var deckB = Deck(bi, 5f, 5f);
            int out0 = OpenZPyroFire.GroundsGone;
            bool lit = Light(Deck(bi, 7f, 4f)) & Light(OzUePlatformViews.Point(bi, -1.5f, 3f, 0f));   // on the deck, and on the floor with its flames up on the deck
            yield return null;
            float flames0 = FlameTop();
            a.SetHealth(0); w.UpdateObjectHealth(a); b.SetHealth(0); w.UpdateObjectHealth(b);
            yield return null; yield return null;
            Check(g.GroundY(deckA.x, deckA.z) < 0.05f && g.GroundY(deckB.x, deckB.z) < 0.05f, "P6 the deck is gone from the walk surface in the same frames: floor " + g.GroundY(deckB.x, deckB.z).ToString("0.00") + " m");
            Check(OzUePlatformViews.Fell == fell0 + 2 && OzUePlatformViews.Count == 0, "P6 both break by the platform's own Death event");
            yield return Seconds(0.5f); yield return Shot("collapse_0_5s");
            Check(lit && flames0 > 9f && OpenZPyroFire.GroundsGone > out0 && FlameTop() < 0.5f, "P6 burning ground: flames at " + flames0.ToString("0.00") + " m before, " + FlameTop().ToString("0.00") + " m after, the patch on the deck out, the one beside the wall on the floor");
            yield return Seconds(1f); yield return Shot("collapse_1_5s");
            yield return Seconds(1.5f); yield return Shot("collapse_3s");
            int dead = 0; foreach (var o in on) if (o.Destroyed) dead++;
            Check(dead == on.Count && outside != null && !outside.Destroyed, "P6 " + dead + " of " + on.Count + " on them dead, the squad beside the wall alive");
            yield return Seconds(3f);
            int hangFrames = 0, shownHigh = 0; float tEnd = Time.realtimeSinceStartup + 3f; var names = new HashSet<string>();
            while (Time.realtimeSinceStartup < tEnd)
            {
                foreach (var o in on)
                {
                    var v = View(o); if (!v || !v.gameObject.activeInHierarchy) continue;
                    foreach (var r in v.GetComponentsInChildren<Renderer>())
                    {   // what is still drawn of it: a gun's root stays up there with its parts gone
                        if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r.bounds.center.y <= 1.4f) continue;
                        hangFrames++; names.Add(o.Type + " " + r.name + " " + r.bounds.center.y.ToString("0.0"));
                        break;
                    }
                }
                yield return null;
            }
            Check(hangFrames == 0, "P6 nothing above the floor after 6 s: " + hangFrames + " frames" + (names.Count > 0 ? " (" + string.Join(", ", names) + ")" : ""));
            Check(ScorchDecals.OverCleared > marks0, "P6 the scorch marks on the deck go with it: " + (ScorchDecals.OverCleared - marks0));
            Check(restOn && !Tossed(vRest), "P6 a turret thrown onto the deck from beside it goes with the deck (it lay there " + restOn + ")");
            Check(airGoes && (!Tossed(vAir) || Tossed(vAir).position.y < 1.4f), "P6 a turret in the air toward the deck lands on the floor: y " + (Tossed(vAir) ? Tossed(vAir).position.y.ToString("0.00") : "gone"));
            Check(bodyUp && !vThrown.gameObject.activeInHierarchy, "P6 a settled body on the deck, its root on the floor beside the wall, goes with the deck (it lay there " + bodyUp + ")");
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                var p = rb.transform.position;
                if (!rb.isKinematic && rb.IsSleeping() && p.y > 1.2f) shownHigh++;
                else if (rb.isKinematic && p.y > 1.2f && rb.gameObject.layer == BuildingBreaker.DebrisLayer) shownHigh++;
            }
            Check(shownHigh == 0, "P6 no loose piece resting in the air: " + shownHigh);
            var log = OzSoundLog.Since(t0);
            int chunks = 0, crash = 0, col = 0;
            foreach (var e in log) { if (e.key == "dst_rock_chunks") chunks++; if (e.key == "dst_rock_crash_large") crash++; if (e.key == "dst_l16_pyramid_collapse") col++; }
            Check(chunks >= 2 && crash >= 2 && col >= 2, "P6 the sound log: dst_rock_chunks " + chunks + ", dst_rock_crash_large " + crash + ", dst_l16_pyramid_collapse " + col + " (want 2 of each)");
            Check(OzUeField.EventToasts >= toast0 + 2, "P6 the toasts of the two platforms");
            Info("P6 older pieces taken with the decks: " + (OzUePlatformViews.LooseTaken - taken0));
        }

        IEnumerator P7(Vector3 f)
        {
            var none = OzUePlayground.FieldToTree() as Dictionary<string, object>;
            Check(none != null && !none.ContainsKey("platforms"), "P7 no platform on the field: no key in the saved block");
            var a = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, f, 1));
            if (a == null) { Check(false, "P7 a platform drops"); yield break; }
            a.Health = 7777; var ai = w.PlatformOf(a.Id); int dir = ai.Dir, tx = ai.TileX, ty = ai.TileY;
            var u = Lead(OzUePlayground.Drop("light", Team.Red, Deck(ai, 5f, 5f), 1));
            yield return Frames(3);
            var tree = OzUePlayground.FieldToTree();
            int n = OzUePlayground.FieldFromTree(tree, out string err);
            yield return Frames(4);
            var ps = w.Platforms;
            bool same = err == null && ps.Count == 1 && ps[0].Dir == dir && ps[0].TileX == tx && ps[0].TileY == ty && ps[0].O.Health == 7777 && OzUePlatformViews.Count == 1;
            Check(same, "P7 loaded: " + n + " object(s), the same platform, side " + (ps.Count > 0 ? ps[0].Dir : -1) + ", HP " + (ps.Count > 0 ? ps[0].O.Health : -1));
            bool onIt = false; foreach (var o in w.Objects) if (o.Type == "light" && !o.Destroyed && ps.Count > 0 && w.PlatformAtTile(o.CenterX >> 4, o.CenterY >> 4) == ps[0].O.Id) { var v = View(o); onIt = v && v.transform.position.y > 9f; }
            Check(onIt, "P7 the tank stands on the deck again");
        }

        IEnumerator P8(Vector3 f)
        {
            float t0 = Time.unscaledTime;
            OzUeControls.CentreOn(f); yield return Seconds(0.3f);
            OzUePlace.ToggleArm(OzUePlayground.PlatformHigh, Team.Neutral); yield return Frames(3);
            Move(ToScreen(f)); yield return Frames(3);
            Check(OzUePlaceDriver.PlatformDirShown == OzUePlayground.PlatformDirNow && !OzUePlaceDriver.PlatformRefusedLook, "P8 the armed tile's preview: the wire outline, its ramp toward the view (side " + OzUePlaceDriver.PlatformDirShown + ")");
            yield return Click(ToScreen(f)); OzUePlace.Stop(); yield return Frames(4);
            var a = w.Platforms.Count > 0 ? w.Platforms[0].O : null;
            int confirms = 0; foreach (var e in OzSoundLog.Since(t0)) if (e.key == "ui_confirm") confirms++;
            Check(a != null && confirms >= 2, "P8 the tile armed and a click on the field: a platform, ui_confirm " + confirms + "x");
            if (a == null) yield break;
            var ai = w.PlatformOf(a.Id); int rx = ai.TileX, ry = ai.TileY;
            var u = Lead(OzUePlayground.Drop("light", Team.Red, Deck(ai, 5f, 5f), 1));
            var r = Lead(OzUePlayground.Drop("grunt", Team.Red, Deck(ai, 3f, 3f), 1));
            yield return Frames(3);
            int gone = OzUePlayground.Remove(a);
            yield return Frames(3);
            bool quiet = u != null && w.Get(u.Id) == null && r != null && w.Get(r.Id) == null && !w.PlatformFallen(u.Id);
            int cancels = 0; foreach (var e in OzSoundLog.Since(t0)) if (e.key == "ui_cancel") cancels++;
            Check(quiet && w.Path.TilePassable(rx, ry, true) && OzUePlatformViews.Count == 0 && cancels >= 1, "P8 Remove: what stands on it goes quietly (no death), the tiles open, the view gone, ui_cancel " + cancels + "x");
            var e2 = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, f, 1));
            var ei = w.PlatformOf(e2.Id);
            var wreck = Lead(OzUePlayground.Drop("heavy", Team.Red, Deck(ei, 6f, 6f), 1)); var body = Lead(OzUePlayground.Drop("grunt", Team.Red, Deck(ei, 3f, 3f), 1));
            yield return Frames(3);
            var wv = View(wreck); var bv = View(body); int marks0 = ScorchDecals.OverCleared;
            wreck.SetHealth(0); w.UpdateObjectHealth(wreck); body.SetHealth(0); w.UpdateObjectHealth(body);
            ScorchDecals.Add(Deck(ei, 8f, 2f), 1.5f);
            yield return Seconds(0.6f);
            bool shown = wv && wv.gameObject.activeInHierarchy && wv.transform.position.y > 9f;
            bool lit = Light(Deck(ei, 7f, 7f));
            yield return null;
            float flames0 = FlameTop();
            OzUePlayground.Remove(e2);
            yield return Frames(3);
            Check(lit && flames0 > 9f && FlameTop() < 0.5f, "P8 Remove with burning ground on the deck: flames at " + flames0.ToString("0.00") + " m before, " + FlameTop().ToString("0.00") + " m after");
            Check(shown && !wv.gameObject.activeInHierarchy && !bv.gameObject.activeInHierarchy && ScorchDecals.OverCleared > marks0, "P8 Remove with a burning wreck, a body and a mark on the deck: all gone with it");
            var b = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, f, 1));
            var c = Lead(OzUePlayground.Drop("grunt", Team.Blue, Deck(w.PlatformOf(b.Id), 3f, 3f), 1));
            yield return Frames(3);
            OzUePlayground.ClearField(); yield return Frames(4);
            Check(OzUePlatformViews.Count == 0 && w.Platforms.Count == 0 && OzUePlayground.ClearLeft == 0, "P8 CLEAR FIELD leaves no platform, no view, nothing drawn (" + OzUePlayground.ClearLeft + ")");
        }

        IEnumerator P9(Vector3 f)
        {
            var a = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, f, 1));
            if (a == null) { Check(false, "P9 a platform drops"); yield break; }
            var ai = w.PlatformOf(a.Id);
            OzUeControls.CentreOn(Deck(ai, 5f, 9f)); yield return Seconds(0.3f);
            var foot = OzUePlatformViews.Point(ai, 5f, ai.Rows + 5f, 0f);
            var tank = Lead(OzUePlayground.Drop("heavy", Team.Red, foot, 1));
            var onDeck = Lead(OzUePlayground.Drop("grunt", Team.Blue, Deck(ai, 3f, 3f), 1));
            if (onDeck != null) onDeck.Damage = 0;
            yield return Frames(4);
            OzUePick.Pick(new[] { tank }, false); yield return Frames(3);
            yield return Click(ToScreen(Deck(ai, 6f, 6f)), true);
            OzUeControls.SetSpeed(2f);
            float t1 = Time.realtimeSinceStartup; float y = 0f;
            while (Time.realtimeSinceStartup - t1 < 30f) { y = View(tank).transform.position.y; if (y > 4.4f && !tank.IsMoving) break; yield return null; }
            OzUeControls.SetSpeed(1f);
            Check(y > 4.4f, "P9 a plain right-click on the deck: the tank drives up the ramp, at " + y.ToString("0.00") + " m");
            yield return Shot("tank_on_deck");
            Check(tank.Waypoints.Count == 0 || tank.Waypoints[0].mode != WpMode.Attack, "P9 a plain right-click is no attack on the platform");
            yield return Click(ToScreen(OzUePlatformViews.Point(ai, 5f, 0.1f, (float)ai.DeckM + OzUePlatformView.KerbM)), true); yield return Frames(3);
            var mark = ZGame.WorldToPx(ed.Input.MarkerPos);
            Check(w.PlatformPartAt(mark.x >> 4, mark.y >> 4) == ZWorld.PlatPartDeck, "P9 a plain right-click on the far kerb is a move on the deck, not on the floor behind the wall");
            OzUePick.ClearInspect(); yield return Frames(3);
            var dv = View(onDeck);
            var deckEmpty = Deck(ai, 7f, 2f);
            Move(ToScreen(deckEmpty)); yield return Frames(4);
            Check(OzUePick.Hover == a && OzUeMarks.PlatformWords == 2, "P9 the pointer on the deck: the platform's marks, " + OzUeMarks.LastPlatformTop);
            yield return Click(ToScreen(dv.transform.position + Vector3.up * 1.2f)); yield return Frames(4);
            Check(ed.Input.Selection.Count > 0 && ed.Input.Selection[0].Owner == Team.Blue, "P9 a robot on the deck is picked by a click (the platform does not swallow it)");
            var mp = OzUeMarks.Pos(g, onDeck);
            Check(mp.y > 4f, "P9 its marks lie on the deck: y " + mp.y.ToString("0.00"));
            OzUePick.ClearInspect(); yield return Frames(3);
            // Ctrl + right-click: from beside it
            var side = OzUePlatformViews.Point(ai, -8f, 6f, 0f);
            var tank2 = Lead(OzUePlayground.Drop("heavy", Team.Red, side, 1)); yield return Frames(4);
            OzUePick.Pick(new[] { tank2 }, false); yield return Frames(3);
            Keys(Key.LeftCtrl); yield return Frames(3);
            yield return Click(ToScreen(deckEmpty), true);
            Keys(); yield return Frames(3);
            Check((tank2.Waypoints.Count > 0 && tank2.Waypoints[0].refId == a.Id) || tank2.AttackObject == a, "P9 Ctrl + right-click on the deck attacks the platform");
            OzUePick.ClearInspect(); yield return Frames(2);
            yield return Click(ToScreen(deckEmpty)); yield return Frames(4);
            Check(OzUePick.Kind == UeInspect.Platform && OzUePick.Inspected == a && OzUeObjectPanelPlatformSetups() > 0, "P9 a left click on the deck shows the platform in the STATS box");
            OzUePick.ClearInspect(); yield return Frames(2);
        }

        static int OzUeObjectPanelPlatformSetups() => OzUeObjectPanel.PlatformSetups;

        IEnumerator Finish(float tStart, bool playing0, float speed0)
        {
            shotFrom = null;
            OzUePlace.Stop(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            if (hidden0) OzUnitEditor.SetPanelsHidden(true);
            yield return Frames(2);
            var sb = new StringBuilder();
            sb.Append(Tag).Append(' ').Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            if (pictures.Count > 0) sb.Append("pictures: ").AppendLine(string.Join(", ", pictures));
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n"));
                    File.WriteAllText(Path.Combine(Folder, "sound_log.txt"), OzSoundLog.Format(OzSoundLog.Since(tStart)).Replace("\r\n", "\n"));
                }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (fail == 0) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
#endif
