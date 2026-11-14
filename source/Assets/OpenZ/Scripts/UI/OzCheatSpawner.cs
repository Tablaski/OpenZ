using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// Dev only: on in the editor (PlayerPrefs "oz_dev" = 0 turns it off for takes), off in builds unless "-ozdev" is on the
    /// production window's PLACE button or their own key (default C; bound to P it works too, and PlayerInput never reads it with Ctrl or Alt held, so Alt+P is only this overlay's). Spawns use the normal sim API (CreateRobotGroup with
    /// Zod's squad size, CreateObject), so everything spawned behaves exactly like produced units. Hidden = inactive panels:
    /// no blocker, no input, no per-frame work beyond the Alt+P check.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class OzCheatSpawner : MonoBehaviour
    {
        public static bool DevEnabled
        {
            get
            {
                // The testing overlay is available in every build, the release exe included (it is not listed in the controls).
                if (Application.isEditor) return PlayerPrefs.GetInt("oz_dev", 1) != 0;
                if (ScriptedTake) return false;
                if (PlayerPrefs.GetInt("oz_dev", 1) != 0) return true;
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-ozdev") return true;
                return false;
            }
        }
        /// <summary>
        /// builds only) never installs the overlay, so nothing typed or clicked on the game window can spawn units into the take.
        /// </summary>
        static bool ScriptedTake
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs())
                    if (a.Equals("-ozend", System.StringComparison.OrdinalIgnoreCase) || a.Equals("-ozkill", System.StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }
        /// <summary>1.3 item 28: Alt+P (never a plain P, never Ctrl+P) is ours while the dev flag is on. PlayerInput no longer asks: a plain P goes to its own binding.</summary>
        public static bool OwnsP => I && I.isActiveAndEnabled;
        public static OzCheatSpawner I { get; private set; }
        public static bool Visible => I && I.shown;

        static readonly string[] Robots = { "grunt", "psycho", "tough", "sniper", "pyro", "laser", "electrobot" };
        static readonly string[] Vehicles = { "jeep", OzLightTank.TileV1, OzLightTank.TileV2, OzMech.TileV1, OzMech.TileV2, ZMTank.Type, OzHeavyTank.TileV1, OzHeavyTank.TileV2, "apc", OzMlrs.TileV1, OzMlrs.TileV2, "crane" };   // 1.2 item 110: HEAVY v1 and HEAVY v2 side by side; 1.2 v2 units: LIGHT v1 and LIGHT v2; DV145: MEDIUM TANK after the mech; 1.2 v2 units: MLRS v1 and MLRS v2; MECH v1 and MECH v2
        static readonly string[] EvilHero = { ZEvilZod.Type };                                       // DV161: a robot of the blue team, the blue column's HERO section (Sim/ZEvilZod.cs)
        static readonly string[] Guns = { "gatling", OzGun.TileV1, OzGun.TileV2, "howitzer", "missile_cannon" };   // 1.2 v2 units: GUN v1 and GUN v2 where GUN was (ZBuildLists.Cannons' order; the sim's list untouched)
        const float Cell = 60f, Gap = 14f, Pad = 12f, Head = 22f, LabelH = 13f; const int Cols = 4;

        struct Entry { public RectTransform rt; public string type; public Team team; public Texture tex; }
        readonly List<Entry> entries = new List<Entry>();
        readonly List<RectTransform> panels = new List<RectTransform>();
        OpenZHud hud; ZGame game; bool built, shown;
        Entry? drag; RawImage ghost; RectTransform ghostRt; Image hoverFrame;

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (!hooked) { hooked = true; UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => TryInstall(); }
            TryInstall();
        }

        static void TryInstall()
        {
            if (!DevEnabled || OzUeHooks.NoCheatPanel) return;                                    // 1.2 item 6: not in the Unit editor's playground (P is its dock key; the dock drops units there)
            var g = FindFirstObjectByType<ZGame>();
            if (g && !g.GetComponent<OzCheatSpawner>()) g.gameObject.AddComponent<OzCheatSpawner>();
        }

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            if (!hud) hud = OpenZHud.I;
            if (!game) game = ZGame.I;
            if (!hud || !hud.Root || !game || game.World == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame && kb.altKey.isPressed && !hud.Modal) Show(!shown);   // 1.3 item 28: Alt+P (left or right Alt; AltGr = Ctrl + right Alt counts too) shows / hides it, a plain P or Ctrl+P never
            if (!shown) return;
            if (!hud.hudVisible || hud.Modal) { Show(false); return; }
            Interact();
        }

        public void Show(bool on)
        {
            if (on && !built) Build();
            shown = on;
            foreach (var p in panels) p.gameObject.SetActive(on);
            EndDrag();                                                                                   // item 226: P mid-drag, either way, ends the drag and hides the icon
            AudioDirector.UI(on ? "ui_confirm" : "ui_cancel");
        }

        // ------------------------------------------------------------------ layout
        void Build()
        {
            built = true;
            panels.Add(Column("CheatBlue", Team.Blue, OzPlayBlue.Active ? OzPlayBlue.CheatColumn(Team.Blue) : "BLUE · CPU", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -64f)));   // 1.2 item 11: as blue "BLUE · PLAYER"
            panels.Add(Column("CheatRed", Team.Red, OzPlayBlue.Active ? OzPlayBlue.CheatColumn(Team.Red) : "RED · PLAYER", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -64f)));     // 1.2 item 11: as blue "RED · CPU"
            panels.Add(NeutralRow());
            ghostRt = Hud.Node("CheatGhost", hud.Root);
            ghostRt.Anchor(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Cell, Cell));
            ghost = ghostRt.gameObject.AddComponent<RawImage>(); ghost.raycastTarget = false; ghost.color = new Color(1f, 1f, 1f, 0.85f);
            ghostRt.gameObject.SetActive(false);
            ghostRt.gameObject.SetActive(false);                                                       // item 226: the ghost is NOT one of the panels: Show() must never switch it on (it showed the last dragged icon again, stuck where it was dropped, on every P; a blank one on the first P)
        }

        RectTransform Column(string name, Team team, string title, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            float w = Pad * 2f + Cols * Cell + (Cols - 1) * Gap;
            int extra = OzCheatShuttle.Offered(team) ? 1 : 0;                                              // DV118: the TRANSPORT section (UI/OzCheatShuttle.cs)
            int hero = team == Team.Blue && ZWorld.EvilZodDeviation ? 1 : 0;                               // DV161
            int rows = Rows(Robots.Length) + Rows(Vehicles.Length) + Rows(Guns.Length) + extra * Rows(OzCheatShuttle.Types.Length) + hero * Rows(EvilHero.Length);   // 1.2 v2 units: the GUNS tiles (GUN v1 / v2)
            float h = Pad + Head + (3 + extra + hero) * (Head - 4f) + rows * (Cell + LabelH + Gap) + Pad * 0.5f;
            var p = Hud.Node(name, hud.Root); p.Anchor(anchor, pivot, pos, new Vector2(w, h));
            Hud.Plate(p, "md", Head + 6f, OzUi.Mark(team));
            Hud.Label(p, "Title", title, 12, OzUi.Ink, 0.16f).At(Pad, 8);
            float y = Pad + Head + 4f;
            y = Section(p, "ROBOT SQUADS", Robots, team, y);
            y = Section(p, "VEHICLES", Vehicles, team, y);
            y = Section(p, "GUNS", Guns, team, y);                                                           // 1.2 v2 units: GUN v1 and GUN v2
            if (hero > 0) y = Section(p, "HERO", EvilHero, team, y);                                         // DV161: EvilZod (Sim/ZEvilZod.cs)
            if (OzCheatShuttle.Offered(team)) Section(p, "TRANSPORT", OzCheatShuttle.Types, team, y);       // DV118: the playable shuttle, dragged in as a full unit
            hud.AddBlocker(p);
            return p;
        }

        RectTransform NeutralRow()
        {
            int ng = Guns.Length + (OzCheatShuttle.Offered(Team.Neutral) ? OzCheatShuttle.Types.Length : 0), n = Mathf.Max(Vehicles.Length, ng);   // K256f: + the empty shuttle; 1.2 v2 units: the guns and the shuttle on a second line (18 tiles in one row would reach over the team columns at 1920 x 1080)
            float w = Pad * 2f + n * Cell + (n - 1) * Gap, h = Pad + Head + 2f * (Cell + LabelH) + Gap + Pad * 0.5f, y2 = Pad + Head + Cell + LabelH + Gap;
            var p = Hud.Node("CheatNeutral", hud.Root); p.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(w, h));
            Hud.Plate(p, "md", Head + 6f, OzUi.Neutral);
            Hud.Label(p, "Title", "EMPTY · NEUTRAL (any robot can capture)", 12, OzUi.Ink, 0.16f).At(Pad, 8);
            int i = 0;
            foreach (var t in Vehicles) Tile(p, t, Team.Neutral, Pad + i++ * (Cell + Gap), Pad + Head);
            i = 0;                                                                                           // 1.2 v2 units: the second line
            foreach (var t in Guns) Tile(p, t, Team.Neutral, Pad + i++ * (Cell + Gap), y2);
            if (OzCheatShuttle.Offered(Team.Neutral)) foreach (var t in OzCheatShuttle.Types) Tile(p, t, Team.Neutral, Pad + i++ * (Cell + Gap), y2);   // K256f: the empty shuttle (UI/OzCheatShuttle.cs)
            hud.AddBlocker(p);
            return p;
        }

        static int Rows(int n) => (n + Cols - 1) / Cols;

        float Section(RectTransform p, string title, string[] types, Team team, float y)
        {
            Hud.Label(p, "H_" + title, title, 10, OzUi.A(OzUi.Ink, 0.7f), 0.18f).At(Pad, y);
            y += Head - 4f;
            for (int i = 0; i < types.Length; i++)
                Tile(p, types[i], team, Pad + (i % Cols) * (Cell + Gap), y + (i / Cols) * (Cell + LabelH + Gap));
            return y + Rows(types.Length) * (Cell + LabelH + Gap);
        }

        void Tile(RectTransform p, string type, Team team, float x, float y)
        {
            var cell = Hud.Node("Cell_" + type, p).TL(x, y, Cell, Cell + LabelH);
            var bg = Hud.Img(cell, "Bg", OzUi.A(OzUi.GM950, 0.75f)); bg.rectTransform.TL(0, 0, Cell, Cell);
            Hud.Border(bg.transform, OzUi.A(team == Team.Neutral ? OzUi.Neutral : OzUi.Mark(team), 0.55f));
            var tex = (team == Team.Neutral ? (Texture)OzNeutralArt.ForTile(type) : null) ?? OzHeavyTank.TileArt(type, team) ?? OzV2Units.TileArt(type, team) ?? (team == Team.Neutral ? (OzCheatShuttle.NeutralArt(type) ?? UnitArt.Icon(type, Team.Red)) : ((Texture)UnitArt.VehiclePortrait(type, team) ?? UnitArt.Icon(type, team)));   // crewed = team portrait; K256f: the empty shuttle's own picture; 1.2 item 110: each heavy tank model its own; 1.2 item 167c: an empty tile's own neutral picture (View/OzNeutralArt.cs) first
            var raw = Hud.Node("Art", cell).TL(3, 3, Cell - 6, Cell - 6).gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false; raw.texture = tex;
            raw.color = team == Team.Neutral && !OzNeutralArt.Is(tex) ? new Color(0.62f, 0.62f, 0.62f, 1f) : Color.white;      // grey = empty; 1.2 item 167: a neutral picture of its own is drawn as it is
            if (!tex) Hud.Label(cell, "Code", UnitInfo.Code(type), 12, OzUi.Ink).At(8, 22);
            string label = OzHeavyTank.TileLabel(type) ?? OzV2Units.TileLabel(type) ?? (type == "electrobot" ? "ELECTRO" : UnitInfo.Short(type));   // fits the 60 px cell; 1.2 item 110: HEAVY v1 / HEAVY v2
            if (team != Team.Neutral && ZBuildLists.IsRobot(type)) label += " ×" + (game.World.S.Unit(type)?.group_amount ?? 1);
            Hud.Label(cell, "Name", label, 9, OzUi.A(OzUi.Ink, 0.85f), 0.08f).At(1, Cell + 1);
            entries.Add(new Entry { rt = cell, type = type, team = team, tex = tex });
        }

        // ------------------------------------------------------------------ drag + drop
        void Interact()
        {
            var m = Mouse.current; if (m == null) return;
            Vector2 mp = m.position.ReadValue();
            if (drag == null)
            {
                if (!m.leftButton.wasPressedThisFrame) { V3Hover(mp); return; }                               // sound_v3
                foreach (var e in entries)
                    if (OpenZHud.Contains(e.rt, mp))
                    {
                        drag = e; Drags++; ghost.texture = e.tex; ghost.color = e.team == Team.Neutral && !OzNeutralArt.Is(e.tex) ? new Color(0.62f, 0.62f, 0.62f, 0.85f) : new Color(1f, 1f, 1f, 0.85f);   // 1.2 item 167: a neutral picture as it is
                        ghostRt.gameObject.SetActive(true); ghostRt.SetAsLastSibling(); AudioDirector.UI("ui_drag_pick");   // sound_v3
                        break;
                    }
                if (drag == null) return;
            }
            ghostRt.anchoredPosition = hud.ScreenToCanvas(mp);
            if (!m.leftButton.isPressed && !m.leftButton.wasReleasedThisFrame) { EndDrag(); AudioDirector.UI("ui_drag_cancel"); return; }   // item 226: the release was never seen (the window lost focus mid-drag): the icon goes, nothing spawns
            if (m.rightButton.wasPressedThisFrame || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) { EndDrag(); AudioDirector.UI("ui_cancel"); return; }
            if (!m.leftButton.wasReleasedThisFrame) return;
            var d = drag.Value; EndDrag();
            if (hud.IsOverUi(mp)) { AudioDirector.UI("ui_drag_cancel"); return; }   // sound_v3                                                           // dropped back on a panel: nothing
            var cam = Camera.main;
            if (!cam || !game.GroundPoint(cam.ScreenPointToRay(mp), out var gp)) { AudioDirector.UI("ui_drag_cancel"); return; }   // sound_v3
            Spawn(d.type, d.team, ZGame.WorldToPx(gp));
        }

        int v3Hover = -1;
        void V3Hover(Vector2 mp)
        {   // sound_v3: the tiles tick like HUD buttons
            int hi = -1; for (int k = 0; k < entries.Count; k++) if (OpenZHud.Contains(entries[k].rt, mp)) { hi = k; break; }
            if (hi != v3Hover) { v3Hover = hi; if (hi >= 0) AudioDirector.UI("ui_hover"); }
        }

        void EndDrag() { drag = null; if (ghostRt) ghostRt.gameObject.SetActive(false); }
        /// <summary>item 226: the window lost focus or the overlay went away mid-drag: the icon goes. Public for the counted check.</summary>
        public void CancelDrag() => EndDrag();
        void OnApplicationFocus(bool has) { if (!has) EndDrag(); }
        void OnDisable() => EndDrag();
        /// <summary>Checks: the dragged icon is on the screen right now.</summary>
        public static bool GhostVisible => I && I.ghostRt && I.ghostRt.gameObject.activeInHierarchy;
        /// <summary>Checks: drags started since the game began.</summary>
        public static int Drags;

        void Spawn(string type, Team team, Vector2Int px)
        {
            OzHighScores.Unranked = "TESTING OVERLAY USED · NOT IN THE TABLE";                               // high scores: this match does not enter the table
            int heavyModel = OzHeavyTank.FromTile(ref type);                                                 // 1.2 item 110: a heavy tank tile drops type "heavy" drawn as its model
            string v2Tile = type; OzV2Units.FromTile(ref type);                                              // 1.2 v2 units: a light tank tile drops type "light" drawn as its model
            if (type == ZWorld.ShuttleType) { OzCheatShuttle.Spawn(game, hud, team, px); return; }          // DV118: a full shuttle, its pad and its view (UI/OzCheatShuttle.cs)
            var W = game.World;
            bool robot = ZBuildLists.IsRobot(type);
            int foot = robot ? 1 : 2;
            if (!FreeTile(W, px.x >> 4, px.y >> 4, foot, robot, out int tx, out int ty)) { hud.Toast("NO ROOM HERE", HudStyle.Danger); AudioDirector.UI("ui_error"); return; }
            ZObject o;
            if (robot)
            {
                o = W.CreateRobotGroup(type, tx * 16, ty * 16, team);
                game.SpawnUnitView(o); foreach (var mm in o.Minions) game.SpawnUnitView(mm);
            }
            else
            {
                o = W.CreateObject(ZBuildLists.IsCannon(type) ? "cannon" : "vehicle", type, tx * 16, ty * 16, team);
                OzHeavyTank.Pin(o, heavyModel);                                                              // 1.2 item 110: v1 or v2 whatever the switch says
                OzV2Units.PinTile(o, v2Tile);                                                                // 1.2 v2 units: v1 or v2 whatever the light tank's switch says
                var sv = game.SpawnUnitView(o); if (sv) sv.FaceSide();                                      // 1.2 item 169: a vehicle or gun dropped here faces the screen's right, its barrel across the picture (View/UnitView.SideFacing.cs)
            }
            Debug.Log($"[OzCheat] spawned {team} {type} id {o?.Id} at tile {tx},{ty}");
            { var wp = ZGame.PxToWorld(tx * 16 + 8, ty * 16 + 8); wp.y = game.GroundY(wp.x, wp.z); OzSoundV3.CheatDrop(type, robot, ZBuildLists.IsCannon(type), wp); }   // sound_v3
            AudioDirector.UI("ui_confirm");
        }

        // ------------------------------------------------------------------ self-test (real input events, one step per frame)
        public static string TestReport = "idle";
        public static void SelfTest(Vector2 dropScreen) { if (I) { TestReport = "running"; I.StartCoroutine(I.TestRoutine(dropScreen)); } }

        System.Collections.IEnumerator TestRoutine(Vector2 drop)
        {
            var log = new System.Text.StringBuilder();
            System.Collections.IEnumerator F(int n) { for (int i = 0; i < n; i++) yield return null; }
            void MouseAt(Vector2 p, bool left) { var st = new UnityEngine.InputSystem.LowLevel.MouseState { position = p }; if (left) st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left); InputSystem.QueueStateEvent(Mouse.current, st); }
            int before = game.World.Objects.Count;
            InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.LeftAlt, Key.P)); yield return F(2);
            InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState()); yield return F(2);
            log.Append($"Alt+P shows: {shown}; placing={(FindFirstObjectByType<PlayerInput>()?.Placing != null)}. ");
            int ok = 0;
            foreach (var pick in new[] { ("tough", Team.Red), (OzHeavyTank.TileV2, Team.Blue), ("gatling", Team.Neutral) })   // 1.2 item 110: the heavy tile is HEAVY v2
            {
                var e = entries.Find(x => x.type == pick.Item1 && x.team == pick.Item2);
                Vector2 c = RectTransformUtility.WorldToScreenPoint(null, e.rt.TransformPoint(e.rt.rect.center));
                int n0 = game.World.Objects.Count;
                MouseAt(c, false); yield return F(2); MouseAt(c, true); yield return F(2);
                for (int k = 1; k <= 6; k++) { MouseAt(Vector2.Lerp(c, drop, k / 6f), true); yield return F(1); }
                MouseAt(drop, false); yield return F(3);
                int made = game.World.Objects.Count - n0;
                log.Append($"{pick.Item2} {pick.Item1}: +{made} objects. "); if (made > 0) ok++;
                drop += new Vector2(90f, 0f);
            }
            InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.LeftAlt, Key.P)); yield return F(2);
            InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState()); yield return F(2);
            log.Append($"Alt+P again hides: {!shown}; over UI at the column when hidden: {hud.IsOverUi(new Vector2(40f, Screen.height * 0.5f))}.");
            TestReport = (ok == 3 && !shown ? "PASS " : "FAIL ") + log;
            Debug.Log("[OzCheat] self-test " + TestReport);
        }

        /// <summary>The dropped tile, else the nearest tile (spiral, radius 6) whose foot x foot box is passable for its kind.</summary>
        internal static bool FreeTile(ZWorld W, int cx, int cy, int foot, bool robot, out int tx, out int ty)
        {
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy; bool ok = true;
                        for (int j = 0; j < foot && ok; j++) for (int i = 0; i < foot && ok; i++) ok = W.Path.TilePassable(x + i, y + j, robot);
                        if (ok) { tx = x; ty = y; return true; }
                    }
            tx = cx; ty = cy; return false;
        }
    }
}
