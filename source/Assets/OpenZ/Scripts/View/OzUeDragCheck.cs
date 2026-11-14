using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 70: the Play check of the dock's drag start (UI/OzUeDock.cs, UI/OzUePressAt.cs). With the unit editor open (menu
    /// OpenZ/V1.2/Unit Editor: 70 Dock Drag Gestures Check): 40 press-and-pull gestures as real mouse events, 10 in each tab, on random
    /// tiles (the EXTRAS too) at random points (the middle, 1 u inside each edge, a corner, the "i" corner, the squad badge, 1.5 u above
    /// the tile's edge), pulled 40 u up three ways (a smooth pull in 6 steps, one quick jump, the press and the pull in one frame as in a
    /// long frame): each must start the drag of its own tile. Each is let go back over the dock (no drop). The team, the tab and the
    /// dock are put back. Hands off the mouse while it runs.
    /// </summary>
    public sealed class OzUeDragCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ DockDrag]";
        public const int Gestures = 40;
        public static string Report = "not run";
        public static bool Running => run;
        static OzUeDragCheck run;
        Vector2 last;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_DragCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeDragCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { Route(false); if (run == this) run = null; }

        void Send(Vector2 p, bool down)
        {
            var m = Mouse.current; if (m == null) return;
            var st = new MouseState { position = p, delta = p - last };
            if (down) st = st.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(m, st); last = p;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        struct T { public string id; public RectTransform root, info, squad; }

        static List<T> Tiles()
        {
            var list = new List<T>();
            var plate = GameObject.Find("UE_Dock"); if (!plate) return list;
            foreach (Transform c in plate.transform)
            {
                if (!c.gameObject.activeInHierarchy || !c.name.StartsWith("Tile_")) continue;
                string id = c.name.Substring(5);
                if (!OzUePlayground.Offered(id, out _)) continue;
                if (OzUePlayground.IsPlatformId(id)) continue;                                                 // 1.3: a platform takes 20 x 46 m of the drag spot: its own check (View/OzUePlatformCheck.cs)
                if (id == OzUePlayground.Nuke) continue;                                                       // 1.3 item 2: a NUKE drag calls a strike and starts its 30 s: its own check (View/OzUeNukeCheck.cs)
                list.Add(new T { id = id, root = (RectTransform)c, info = c.Find("Art/Info") as RectTransform, squad = c.Find("Art/Squad") as RectTransform });
            }
            return list;
        }

        static Vector2 ToScreen(RectTransform rt, Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(local));
        static Vector2 Middle(RectTransform rt) { var r = rt.rect; return ToScreen(rt, r.center); }

        // the editor gives mouse events to the game only while a Game view has focus: for the check's events the input goes to the game
        // whatever window has focus (no window focus needed), put back at the end
#if UNITY_EDITOR
        bool routed; InputSettings.EditorInputBehaviorInPlayMode route0; InputSettings.BackgroundBehavior back0;
#endif
        void Route(bool on)
        {
#if UNITY_EDITOR
            var s = InputSystem.settings;
            if (on && !routed) { routed = true; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior; s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
            else if (!on && routed) { routed = false; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0; }
#endif
        }

        IEnumerator Main()
        {
            Report = "running";
            Route(true);
            var team0 = OzUeControls.Team; string tab0 = OzUeControls.Tab; bool open0 = OzUeControls.DockOpen;
            OzUePlace.Stop();
            OzUeControls.SetTeam(Team.Red);
            if (!OzUeControls.DockOpen) OzUeControls.SetDockOpen(true);
            yield return new WaitForSecondsRealtime(0.6f);
            var rng = new System.Random(70);
            string[] where = { "the middle", "1 u inside the left edge", "1 u inside the right edge", "1 u inside the top edge", "1 u inside the bottom edge (the name)", "a corner", "the i corner", "the squad badge", "1.5 u above the tile" };
            string[] how = { "a smooth pull", "one quick jump", "press and pull in one frame" };
            int ok = 0, bad = 0, rescues0 = OzUeDock.Rescues, seen0 = OzUePressAt.Seen;
            var byHow = new int[3]; var byWhere = new int[where.Length];
            var fails = new StringBuilder();
            for (int g = 0; g < Gestures; g++)
            {
                string tab = OzUeControls.Tabs[g % 4];
                if (OzUeControls.Tab != tab) { OzUeControls.SetTab(tab); yield return Frames(4); }
                var tiles = Tiles();
                if (tiles.Count == 0) { bad++; fails.Append($"\n  FAIL gesture {g + 1}: no tile shown in the tab {tab}"); continue; }
                var t = tiles[rng.Next(tiles.Count)];
                int wi = rng.Next(where.Length), hi = g % 3;
                if (wi == 6 && !t.info) wi = 0;
                if (wi == 7 && !t.squad) wi = 0;
                var r = t.root.rect; float y = Mathf.Lerp(r.yMin + 8f, r.yMax - 8f, (float)rng.NextDouble()), x = Mathf.Lerp(r.xMin + 4f, r.xMax - 4f, (float)rng.NextDouble());
                Vector2 a = wi switch
                {
                    1 => ToScreen(t.root, new Vector2(r.xMin + 1f, y)),
                    2 => ToScreen(t.root, new Vector2(r.xMax - 1f, y)),
                    3 => ToScreen(t.root, new Vector2(x, r.yMax - 1f)),
                    4 => ToScreen(t.root, new Vector2(x, r.yMin + 1f)),
                    5 => ToScreen(t.root, new Vector2(r.xMax - 1f, r.yMax - 1f)),
                    6 => Middle(t.info),
                    7 => Middle(t.squad),
                    8 => ToScreen(t.root, new Vector2(r.center.x, r.yMax + 1.5f)),
                    _ => ToScreen(t.root, r.center),
                };
                Vector2 b = a + Vector2.up * 40f * OzUnitEditor.PxPerUnit;
                Send(a, false); yield return Frames(3);
                if (hi == 0) { Send(a, true); yield return Frames(2); for (int i = 1; i <= 6; i++) { Send(Vector2.Lerp(a, b, i / 6f), true); yield return null; } }
                else if (hi == 1) { Send(a, true); yield return Frames(2); Send(b, true); }
                else { Send(a, true); Send(b, true); }                                                   // both events in one input update
                float t0 = Time.realtimeSinceStartup;
                while (!(OzUePlace.Dragging && OzUePlace.Id == t.id) && Time.realtimeSinceStartup - t0 < 0.5f) yield return null;
                bool started = OzUePlace.Dragging && OzUePlace.Id == t.id;
                string got = OzUePlace.Dragging ? OzUePlace.Id : "nothing";
                Send(a, true); yield return Frames(2); Send(a, false); yield return Frames(3);           // let go over the dock: no drop
                if (OzUePlace.Busy) { OzUePlace.Stop(); yield return Frames(2); }
                if (started) { ok++; byHow[hi]++; byWhere[wi]++; }
                else { bad++; fails.Append($"\n  FAIL gesture {g + 1}: {t.id} ({tab}), pressed on {where[wi]}, {how[hi]}: the drag of {got} started"); }
            }
            OzUeControls.SetTab(tab0); OzUeControls.SetTeam(team0);
            if (!open0) OzUeControls.SetDockOpen(false);
            var sb = new StringBuilder();
            sb.Append($"{Tag} COUNTED CHECK 70 {(bad == 0 ? "PASS" : "FAIL")}: {ok} of {Gestures} press-and-pull gestures on random tiles started the drag of their own tile (");
            for (int i = 0; i < how.Length; i++) sb.Append((i > 0 ? ", " : "") + how[i] + " " + byHow[i]);
            sb.Append("; pressed on: ");
            for (int i = 0; i < where.Length; i++) sb.Append((i > 0 ? ", " : "") + where[i] + " " + byWhere[i]);
            sb.Append($"); presses read from the input events {OzUePressAt.Seen - seen0}, presses the dock took beside a tile or after a long frame {OzUeDock.Rescues - rescues0}");
            sb.Append(fails);
            Report = sb.ToString();
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
