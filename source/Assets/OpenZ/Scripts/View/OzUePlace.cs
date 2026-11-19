using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): placing units, flags and obstacles (DESIGN_SPEC E183, E185, E188, E205,
    /// section 9.3), as the testing panel P does it, both ways of the prototype:
    ///  - DRAG: a left press that travelled more than 6 u on a dock tile or a picture (P2 / P3 call BeginDrag): the picture follows
    ///    the mouse (56 u with a team edge and a soft amber glow; over the field 32 u at 55 % beside the mouse) while the true-size
    ///    drop preview lies on the floor; released over the field = the drop, over a panel = nothing (ui_drag_cancel), the right
    ///    button or Esc = cancelled (ui_cancel), the window losing focus = cancelled.
    ///  - CLICK TO PLACE: a click on a tile arms it (ToggleArm: ui_confirm, its inspector opens, the arm bar shows, PlayerInput is held
    ///    so no box or pick starts); a left press on the field drops; Shift keeps it armed; a right click (no drag), Esc, DONE, the same
    ///    tile again = stop (ui_cancel). The team switch moves an armed unit to the new side (a robot under NEUTRAL stops).
    /// Robots, heroes and the shuttle are never neutral: refused with the toast "ROBOTS ARE NEVER NEUTRAL: PICK RED OR BLUE". The
    /// drop itself (rooms, the Sim calls, the drop sounds, its toasts) is OzUePlayground.Drop (P1); x1 / x3 / x5 / x10 / x20 = OzUeControls.Count,
    /// a flag or an obstacle drops one at a time.
    /// </summary>
    public static class OzUePlace
    {
        public static bool Busy => Dragging || Armed;
        public static bool Dragging { get; private set; }
        public static bool Armed { get; private set; }
        public static string Id { get; private set; }
        public static Team Team { get; private set; } = Team.Neutral;
        public static event Action Changed;
        /// <summary>Checks: drags begun, drops made (at least one group), arms, cancels since the editor opened.</summary>
        public static int Drags, Drops, Arms, Cancels;
        internal static Vector2 DragFrom;
        static readonly object holder = new object();

        /// <summary>A left press that travelled more than 6 u on a tile / picture: the drag starts (the press began over a panel).</summary>
        public static void BeginDrag(string id, Team team, Vector2 screen)
        {
            if (!OzUnitEditor.Active || string.IsNullOrEmpty(id)) return;
            if (!Allowed(id, ref team)) return;
            if (Armed) Disarm(false);
            Dragging = true; Id = id; Team = team; DragFrom = screen; Drags++;
            AudioDirector.UI("ui_drag_pick");
            Debug.Log("[OpenZ UnitEditor] drag " + Team + " " + id);
            Raise();
        }

        /// <summary>A left click on a tile: arm it, again = disarm.</summary>
        public static void ToggleArm(string id, Team team)
        {
            if (!OzUnitEditor.Active || string.IsNullOrEmpty(id)) return;
            if (OzUePlayground.AlwaysNeutral(id)) team = Team.Neutral;
            if (Armed && Id == id && Team == team) { Disarm(true); return; }
            if (!Allowed(id, ref team)) return;
            if (Dragging) EndDrag();
            Armed = true; Id = id; Team = team; Arms++;
            OzUePick.HoldInput(holder, true);
            if (!OzUePlayground.AlwaysNeutral(id)) OzUePick.InspectType(id, team);                      // spec 4.10: arming a tile shows its inspector
            AudioDirector.UI("ui_confirm");
            Debug.Log("[OpenZ UnitEditor] armed " + Team + " " + id);
            Raise();
        }

        /// <summary>Stops a drag or the click-to-place, silently (callers play their own answer).</summary>
        public static void Stop()
        {
            bool was = Busy;
            if (Dragging) EndDrag();
            if (Armed) { Armed = false; OzUePick.HoldInput(holder, false); }
            if (was) { Cancels++; Raise(); }
        }

        internal static void Disarm(bool sound)
        {
            if (!Armed) return;
            Armed = false; OzUePick.HoldInput(holder, false);
            if (sound) AudioDirector.UI("ui_cancel");
            Cancels++;
            Debug.Log("[OpenZ UnitEditor] placing stopped");
            Raise();
        }

        internal static void CancelDrag(string sound)
        {
            if (!Dragging) return;
            EndDrag(); Cancels++;
            if (!string.IsNullOrEmpty(sound)) AudioDirector.UI(sound);
            Raise();
        }

        static void EndDrag() { Dragging = false; }

        /// <summary>Esc closer (OzUeKeys.EscPlacing).</summary>
        internal static bool EscClose()
        {
            if (Dragging) { CancelDrag("ui_cancel"); return true; }
            if (Armed) { Disarm(true); return true; }
            return false;
        }

        /// <summary>The team switch changed while armed: the armed unit joins the new side (a robot under NEUTRAL stops).</summary>
        internal static void TeamChanged(Team t)
        {
            if (!Armed || OzUePlayground.AlwaysNeutral(Id) || t == Team) return;
            if (!OzUePlayground.ForTeam(Id, t)) { Disarm(true); return; }                                 // rows 79 / 80: EvilZod under RED, ALAN & BRAD under BLUE: their tile is gone, so is the arm
            if (Id == ZEvilZod.Type) return;                                                               // DV161: he stays Blue (under NEUTRAL too)
            if (t == Team.Neutral && !OzUePlayground.CanBeNeutral(Id)) { Disarm(true); return; }
            Team = t; Raise();
        }

        internal static void Raise() { try { Changed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }

        static bool Allowed(string id, ref Team team)
        {
            if (id == ZEvilZod.Type)                                                          // DV161: he is always Blue, and the human is told
            {
                if (OzUeControls.Team != Team.Blue) OzUeToast.Show("EVILZOD ALWAYS JOINS " + OzUeUi.TeamWord(Team.Blue));
                team = Team.Blue;
            }
            if (id == OzUePlayground.Heroes && team == Team.Blue)
            {
                OzUeToast.Show("ALAN & BRAD ALWAYS JOIN " + OzUeUi.TeamWord(Team.Red));
                team = Team.Red;
            }
            if (OzUePlayground.AlwaysNeutral(id)) team = Team.Neutral;
            else if (team == Team.Neutral && !OzUePlayground.CanBeNeutral(id))
            {
                OzUeToast.Show("ROBOTS ARE NEVER NEUTRAL: PICK RED OR BLUE", true);
                AudioDirector.UI("ui_error");
                OzUePick.InspectType(id, OzUeKeys.ShownBlue);                                           // E047: the refused tile still opens its inspector
                return false;
            }
            if (!OzUePlayground.Offered(id, out string why))
            {
                OzUeToast.Show(string.IsNullOrEmpty(why) ? "NOT OFFERED NOW" : why, true);
                AudioDirector.UI("ui_error");
                return false;
            }
            return true;
        }

        /// <summary>How many groups one drop makes now: 1 for a flag / obstacle, else x1 / x3 / x5 / x10 / x20.</summary>
        internal static int CountFor(string id) => OzUePlayground.AlwaysNeutral(id) ? 1 : Mathf.Clamp(OzUeControls.Count, 1, 20);

        internal static void DropAt(string id, Team team, Vector3 p)
        {
            UeDrop d;
            try { d = OzUePlayground.Drop(id, team, p, CountFor(id)); }
            catch (Exception e) { Debug.LogException(e); OzSfx.Ui("ui_error"); return; }
            if (d.ok) Drops++;
            if (!string.IsNullOrEmpty(d.why)) OzUeToast.Show(d.why, !d.ok);
            if (d.ok && Armed && OzUePlayground.KindOf(id) == UeKind.Nuke && !OzUeBlasts.Is(id) /* 1.5 ue_explosions_6 */) Disarm(false);                     // 1.3 item 2: one nuke per call, Shift does not keep it armed (its 30 s run now)
        }

        /// <summary>The editor closed: nothing armed, no listener of this session left.</summary>
        internal static void Reset()
        {
            Dragging = false; Armed = false; Id = null; Team = Team.Neutral; Changed = null;
            Drags = Drops = Arms = Cancels = 0;
        }
    }

    /// <summary>The mouse side of placing and the drop preview (order -36: before PlayerInput, after the HUD's pointer test).</summary>
    [DefaultExecutionOrder(-36)]
    internal sealed partial class OzUePlaceDriver : MonoBehaviour
    {
        RectTransform ghostRt; RawImage ghost, glowRaw; Image glyph; Image[] edge; Texture2D glowTex;
        bool rightDown; Vector2 rightFrom; float rightTravel;
        string ghostFor; Team ghostTeam;
        static readonly Color NoRoom = new Color32(0xFF, 0x7A, 0x5E, 255), RingNeutral = new Color32(0xCF, 0xCD, 0xBF, 255), HoloPale = new Color32(0xE6, 0xEF, 0xE9, 255),
                              NeutralText = new Color32(0xE4, 0xE2, 0xD4, 255);

        void OnEnable() { OzUeControls.Changed += ControlsChanged; }
        void OnDisable() { OzUeControls.Changed -= ControlsChanged; OzUePlace.Stop(); modelsUsed.Clear(); HideUnusedModels(); }
        void OnApplicationFocus(bool has) { if (!has && OzUePlace.Dragging) OzUePlace.CancelDrag(null); }

        void ControlsChanged() { if (OzUePlace.Armed) OzUePlace.TeamChanged(OzUeControls.Team); }

        void OnDestroy() { if (glowTex) Destroy(glowTex); DestroyModels(); }

        void Update()
        {
            if (!OzUnitEditor.UiBuilt) { ShowGhost(false, false, Vector2.zero); return; }
            var m = Mouse.current; if (m == null) return;
            var ed = OzUnitEditor.I; var hud = ed.Hud; if (!hud) return;
            Vector2 mp = m.position.ReadValue();
            bool overUi = hud.IsOverUi(mp);
            if (OzUePlace.Dragging)
            {
                if (!m.leftButton.isPressed && !m.leftButton.wasReleasedThisFrame) { OzUePlace.CancelDrag("ui_drag_cancel"); ShowGhost(false, false, mp); return; }   // the release was never seen (focus lost)
                if (m.rightButton.wasPressedThisFrame) { OzUePlace.CancelDrag("ui_cancel"); ShowGhost(false, false, mp); return; }
                if (m.leftButton.wasReleasedThisFrame)
                {
                    string id = OzUePlace.Id; Team t = OzUePlace.Team;
                    Vector3 p = default;
                    bool floor = !overUi && FloorPoint(ed, mp, out p);
                    OzUePlace.CancelDrag(floor ? null : "ui_drag_cancel");
                    if (floor) OzUePlace.DropAt(id, t, p);
                    ShowGhost(false, false, mp);
                    return;
                }
                ShowGhost(true, !overUi, mp);
                return;
            }
            ShowGhost(false, false, mp);
            if (!OzUePlace.Armed || hud.Modal || OzConfirm.Blocks) { rightDown = false; return; }
            if (m.leftButton.wasPressedThisFrame && !overUi && FloorPoint(ed, mp, out var at))
            {
                var kb = Keyboard.current; bool keep = kb != null && kb.shiftKey.isPressed;               // E188: Shift keeps placing
                OzUePlace.DropAt(OzUePlace.Id, OzUePlace.Team, at);
                if (!keep) OzUePlace.Disarm(false);
            }
            if (m.rightButton.wasPressedThisFrame) { rightDown = !overUi; rightFrom = mp; rightTravel = 0f; }
            if (rightDown && m.rightButton.isPressed) rightTravel = Mathf.Max(rightTravel, (mp - rightFrom).magnitude / Mathf.Max(0.01f, OzUnitEditor.PxPerUnit));
            if (rightDown && m.rightButton.wasReleasedThisFrame)
            {
                rightDown = false;
                if (!overUi) OzUePlace.Disarm(true);                                                          // a right click stops (run 11: always; the right button never moves the view, View/OzUeCamera.cs)
            }
        }

        void LateUpdate()
        {
            modelsUsed.Clear();
            try { if (OzUnitEditor.UiBuilt && OzUePlace.Busy) PreviewNow(); }
            finally { HideUnusedModels(); }
        }

        void PreviewNow()
        {
            var m = Mouse.current; if (m == null) return;
            var ed = OzUnitEditor.I; var hud = ed.Hud; if (!hud || hud.Modal) return;
            Vector2 mp = m.position.ReadValue();
            if (hud.IsOverUi(mp) || !FloorPoint(ed, mp, out var c)) return;
            Preview(OzUePlace.Id, OzUePlace.Team, c, OzUePlace.CountFor(OzUePlace.Id), OzUePlace.Armed);
        }

        /// <summary>The floor point under the mouse, kept inside the playground's map.</summary>
        internal static bool FloorPoint(OzUnitEditor ed, Vector2 screen, out Vector3 p)
        {
            p = default;
            var cam = OzUeDraw.Cam; if (!cam || !ed || !ed.Game) return false;
            var ray = cam.ScreenPointToRay(screen);
            if (!ed.Game.GroundPoint(ray, out p)) return false;
            DeckRay(ed.Game, ray, ref p);
            OzUeBoxes.TopRay(ray, ref p);                                                                  // 1.5 ue_boxes: placing a BOX: the pointer on a box gives its top (View/OzUeObstacleView.Box.cs)                                                                  // 1.5 ue_flag_ghost_height: the pointer's point on the platform under it (View/OzUePlace.Surface.cs)
            var map = ed.World != null ? ed.World.Map : null;
            if (map != null) { float w = map.width * 2f, h = map.height * 2f; p.x = Mathf.Clamp(p.x, 1f, w - 1f); p.z = Mathf.Clamp(p.z, -(h - 1f), -1f); }
            p.y = 0f;
            return true;
        }

        // ------------------------------------------------------------------ E205 drop preview: the whole drop at true size on the floor
        // drawPreview: a unit drop = every robot / vehicle of it at 40 % on its spot with a dashed team ring (3 / 3, 1.5 u, 95 %),
        // a squad's robots on a small ring 2 m apart round the squad's point; a neutral drop = the object(s) at 60 % with their ground ring
        // (4 / 3, 1.2 u, 90 % x 60 %); the flag's ring 1.2 m; the obstacle's box in the hologram colour; no room = #FF7A5E.
        readonly List<Vector3> members = new List<Vector3>(32);

        void Preview(string id, Team team, Vector3 centre, int count, bool armed)
        {
            var kind = OzUePlayground.KindOf(id);
            float yaw = OzUnitEditor.I.Rts ? OzUnitEditor.I.Rts.yaw : 0f;
            List<Vector3> spots;
            try { spots = OzUePlayground.Formation(id, count, centre, yaw); } catch (Exception) { spots = null; }
            if (spots == null || spots.Count == 0) spots = new List<Vector3> { centre };
            if (OzUePlatformViews.Count > 0)
            {   // 1.3: with a ramp platform on the field the rings and the words lie on the deck, not under it (0 with none, as before)
                var gm = OzUnitEditor.I.Game; centre.y = OnSurface(gm, centre, OzUePlatformViews.MarkY(gm, centre));   // 1.5 ue_flag_ghost_height: the preview on the surface the drop stands on (View/OzUePlace.Surface.cs)
                for (int i = 0; i < spots.Count; i++) { var s = spots[i]; s.y = OnSurface(gm, s, OzUePlatformViews.MarkY(gm, s)); spots[i] = s; }
                LastCentre = centre;
            }
            bool ok; string why;
            try { ok = OzUePlayground.CanDrop(id, team, centre, count, out why); } catch (Exception) { ok = true; why = null; }
            float foot = Mathf.Max(1f, OzUePlayground.FootprintM(id));
            bool neutral = team == Team.Neutral;
            Color ring = ok ? (neutral ? RingNeutral : OzUeUi.Ring(team)) : NoRoom;
            Color text = !ok ? NoRoom : neutral ? NeutralText : OzUeUi.TeamText(team);
            if (kind == UeKind.Platform) { PlatformPreview(id, centre, ok, why, armed); return; }             // 1.3: its wire outline at true size (View/OzUePlace.Platforms.cs)
            if (kind == UeKind.Nuke) { if (OzUeBlasts.Is(id)) BlastPreview(id, centre, ok, why, armed); /* 1.5 ue_explosions_6 */ else NukePreview(centre, ok, why, armed); return; }                          // 1.3 item 2: the strike's ring (View/OzUePlace.Nuke.cs)
            if (kind == UeKind.Obstacle && id == OzUePlayground.Box) BoxPreview(spots, ok);                // 1.5 ue_boxes: the cube on the level it will stand on (View/OzUePlace.Box.cs)
            else if (kind == UeKind.Obstacle)
            {
                Color holo = ok ? Color.Lerp(OzUeBackdrop.HiColor, HoloPale, 0.42f) : NoRoom;
                float h = foot * 0.5f;
                foreach (var s in spots)
                {
                    OzUeDraw.RectOutline(s, h, h, OzUi.A(holo, 0.9f), 0f, 0f, 1f);
                    OzUeDraw.RectOutline(s + Vector3.up * OzUePlayground.ObstacleHeightM, h, h, OzUi.A(holo, 0.6f), 0f, 0f, 1f);
                    for (int k = 0; k < 4; k++) OzUeDraw.Up(s + new Vector3(k % 3 == 0 ? -h : h, 0f, k < 2 ? -h : h), OzUePlayground.ObstacleHeightM, OzUi.A(holo, 0.6f), 1f);
                }
            }
            else if (kind == UeKind.Flag || neutral)
            {
                float r = kind == UeKind.Flag ? 1.2f : kind == UeKind.Grenades ? 0.9f : OzUeMarks.SizeM(id) * 0.5f;
                foreach (var s in spots)
                {
                    OzUeDraw.Ring(s, r, OzUi.A(ring, 0.9f * 0.6f), 4f, 3f, kind == UeKind.Flag ? 1f : 1.2f);
                    if (kind == UeKind.Flag) FlagGhost(s, ring);                                              // 1.5 ue_flag_ghost_height: the flag standing at its height
                    if (kind != UeKind.Flag && kind != UeKind.Grenades) PlaceModel(id, Team.Neutral, s, FaceYaw(Team.Neutral), OzUeGhost.NeutralAlpha);   // the empty vehicle / gun: its grey ghost at 50 %
                }
            }
            else
            {
                float r = OzUeMarks.SizeM(id) * 0.5f;
                members.Clear();
                int per = kind == UeKind.Robot || kind == UeKind.Hero ? Mathf.Max(1, OzUePlayground.SquadSize(id)) : 1;
                foreach (var s in spots) Members(s, per, yaw, members);
                float face = FaceYaw(team);
                foreach (var s in members)
                {
                    OzUeDraw.Ring(s, r, OzUi.A(ring, 0.95f), 3f, 3f, 1.5f);
                    PlaceModel(id, team, s, face, OzUeGhost.UnitAlpha);                                            // the unit: its team's ghost at 45 %
                }
            }
            float rangeM = 0f, blastM = 0f;
            if (!neutral && kind != UeKind.Flag && kind != UeKind.Obstacle)                                 // an empty vehicle or gun has nobody to fire: no range ring
            {
                double rg = OzUeSets.InPlay(id, UeStat.Range), bl = OzUeSets.InPlay(id, UeStat.Blast);
                rangeM = double.IsNaN(rg) ? 0f : (float)rg; blastM = double.IsNaN(bl) ? 0f : (float)bl;
                if (OzUePlatformViews.Count > 0) { var hg = ZGame.WorldToPx(centre); if (OzUnitEditor.I.Game.World.HighGroundAt(hg.x, hg.y)) rangeM *= ZWorld.HighGroundRangePct / 100f; }   // DV162: dropped on a deck it shoots farther (Sim/ZObject.HighGround.cs); nothing with no platform
            }
            if (rangeM > 0.1f)
            {
                OzUeDraw.Ring(centre, rangeM, OzUi.A(ring, 0.7f), 6f, 5f, 1f);
                if (blastM > 0.6f) OzUeDraw.Ring(centre, blastM, OzUi.A(OzUi.Ink, 0.3f), 0f, 0f, 1f);       // Q8: INK 30 %, never amber on the floor
                OzUeDraw.Label(OzUeMarks.FarTop(centre, rangeM), OzUeMarks.Metres(rangeM), OzUi.A(text, 0.85f), 12f, 3f);   // the prototype's ring label: alpha 0.7 + 0.15
            }
            string line;
            if (!ok) line = string.IsNullOrEmpty(why) ? "NO ROOM" : why;
            else if (kind == UeKind.Flag) line = "NEUTRAL FLAG";
            else if (kind == UeKind.Obstacle) line = id == OzUePlayground.Box ? BoxLine() : "HOLO OBSTACLE";
            else if (kind == UeKind.Grenades) line = "GRENADE CRATE";
            else if (neutral) line = "EMPTY " + OzUePlayground.FullName(id) + (spots.Count > 1 ? "  x" + spots.Count : "");
            else
            {
                int per = OzUePlayground.SquadSize(id);
                line = OzUePlayground.FullName(id) + (spots.Count > 1 ? "  x" + spots.Count : "") + (per > 1 ? "  (" + spots.Count * per + " robots)" : "");
            }
            // the drop's words on dark plates, clear of every see-through model and its ring (all the squads of x3 / x5), of the
            // squad's range ring and its metres label and of the blast ring (drawn above with the range ring): under the models, under
            // the whole preview, over the models or over the whole, the first that stays in the free field (View/OzUeWords.cs)
            var shape = kind == UeKind.Obstacle || kind == UeKind.Flag || neutral ? spots : members;
            float ringM = kind == UeKind.Obstacle ? foot * 0.71f : kind == UeKind.Flag ? 1.2f : kind == UeKind.Grenades ? 0.9f : OzUeMarks.SizeM(id) * 0.5f;
            float tallM = kind == UeKind.Obstacle ? (id == OzUePlayground.Box ? BoxTallM : OzUePlayground.ObstacleHeightM) : kind == UeKind.Flag || kind == UeKind.Shuttle ? 3.6f
                        : kind == UeKind.Robot || kind == UeKind.Hero ? 1.9f : 2.8f;
            Words(centre, shape, ringM, tallM, rangeM, rangeM > 0.1f && blastM > 0.6f ? blastM : 0f, armed, line, out float lineUp, out float hintUp);
            OzUeDraw.Label(centre, line, text, 12f, lineUp, true);
            if (armed) OzUeDraw.Label(centre, ArmedHint, OzUi.A(OzUi.Ink2, 0.92f), 12f, hintUp, true);
        }

        public const string ArmedHint = "Click to place  -  Shift keeps placing  -  Esc cancels";
        /// <summary>Checks (the Play proof's drop preview picture): the last frame's words and preview in canvas u from the canvas bottom:
        /// the words' lowest and highest plate edge, the preview's lowest and highest point, whether the words went over the preview.</summary>
        internal static float WordsLow, WordsTop, PreviewLow, PreviewHigh; internal static bool WordsOver;

        /// <summary>Where the drop's words go, as OzUeDraw.Label's up (canvas u from the centre's screen point to the label's box bottom).
        /// View/OzUeWords.cs Place decides it over the preview's lowest and highest point (the near, far and side points of every member
        /// ring and the models' tops), the squad's range ring (rangeM round the centre: its line on the screen) and that ring's metres
        /// label, and the blast ring (blastM round the centre, 0 = none: part of the preview, View/OzUePlace.Blast.cs): the line's plate
        /// 8 u under the models, under the whole preview, 8 u over the models or over the whole, the hint's plate 2 u under the line's;
        /// the first that stays in the free field (over the play bar and the dock, under the top bar and the arm bar) and touches neither
        /// the range ring, its label nor the blast ring.</summary>
        void Words(Vector3 centre, List<Vector3> shape, float ringM, float tallM, float rangeM, float blastM, bool hint, string line, out float lineUp, out float hintUp)
        {
            const float Inset = 2f;                                                                        // the plate reaches 2 u past the label's box
            float ph = OzUeDraw.PlateH(12f);
            lineUp = -(OzUeWords.Gap + 22f + ph); hintUp = lineUp - ph - OzUeWords.Between;
            WordsChoice = 0; WordsClearRing = true; WordsRangeM = rangeM; WordsBlastM = blastM; WordsCentre = centre;
            var cam = OzUeDraw.Cam; var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : null;
            if (!cam || !hud || shape == null || shape.Count == 0) return;
            Vector3 cs = cam.WorldToScreenPoint(centre); if (cs.z <= cam.nearClipPlane) return;
            Vector2 cc = hud.ScreenToCanvas(cs);
            float cy = Mathf.Round(cc.y);
            Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
            Vector3 side = new Vector3(f.z, 0f, -f.x), up = Vector3.up * tallM;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in shape)
            {
                Reach(cam, hud, p - f * ringM, ref lo, ref hi); Reach(cam, hud, p + f * ringM, ref lo, ref hi);
                Reach(cam, hud, p - side * ringM, ref lo, ref hi); Reach(cam, hud, p + side * ringM, ref lo, ref hi);
                Reach(cam, hud, p + up, ref lo, ref hi); Reach(cam, hud, p + up + f * ringM, ref lo, ref hi);
            }
            if (lo > hi) return;
            int rn = RangePolyline(cam, hud, centre, rangeM);
            int bn = BlastPolyline(cam, hud, centre, blastM);                                              // the blast ring, part of the preview (View/OzUePlace.Blast.cs)
            float lx0 = 0f, ly0 = 0f, lx1 = -1f, ly1 = -1f;                                                // the range ring's metres label (OzUeDraw.Label 3 u over its far top)
            if (rn > 0)
            {
                Vector3 ls = cam.WorldToScreenPoint(OzUeMarks.FarTop(centre, rangeM));
                if (ls.z > cam.nearClipPlane)
                {
                    Vector2 lc = hud.ScreenToCanvas(ls); float lw = WordsWidth(OzUeMarks.Metres(rangeM)) * 0.5f + 2f;
                    lx0 = lc.x - lw; lx1 = lc.x + lw; ly0 = lc.y + 3f; ly1 = lc.y + 3f + 18f;
                }
            }
            float band = OzUeInsets.Get(UeSide.Bottom);
            float floorY = (band > 0.5f ? band + OzUeBottomLayout.PlayAbove + OzUeBottomLayout.PlayH : 0f) + 4f;   // the free field over the play bar and the dock
            float top = OzUeInsets.Get(UeSide.Top); if (top < 0.5f) top = 40f;
            float topY = OzUnitEditor.CanvasSize.y - top - (hint ? 66f + OzUeArmBar.H + 4f : 8f);           // under the top bar, and under the arm bar while a tile is armed
            float halfW = Mathf.Max(WordsWidth(line), hint ? WordsWidth(ArmedHint) : 0f) * 0.5f + 9f;        // the wider plate: its text + 8 u each side (OzUeDraw), + 1
            var plan = OzUeWords.Place(lo, hi, ringX, ringY, rn, blastX, blastY, bn, lx0, ly0, lx1, ly1, cc.x, halfW, ph, hint, floorY, topY);
            float lineBottom = plan.lineBottom, hintBottom = lineBottom - OzUeWords.Between - ph;
            lineUp = lineBottom + Inset - cy; hintUp = hintBottom + Inset - cy;
            WordsTop = plan.top; WordsLow = plan.low; PreviewLow = lo; PreviewHigh = hi; WordsOver = plan.over;
            WordsChoice = plan.choice; WordsClearRing = plan.clear; WordsX = cc.x; WordsHalfW = halfW;
        }

        static void Reach(Camera cam, OpenZHud hud, Vector3 w, ref float lo, ref float hi)
        {
            var s = cam.WorldToScreenPoint(w); if (s.z <= cam.nearClipPlane) return;
            float y = hud.ScreenToCanvas(s).y; if (y < lo) lo = y; if (y > hi) hi = y;
        }

        /// <summary>The prototype's squad (formation): n robots on a ring round the squad's point, 2 m apart, the first one
        /// nearest the viewer; one robot / vehicle / gun stands on the point itself. yawDeg = the camera's turn.</summary>
        static void Members(Vector3 group, int n, float yawDeg, List<Vector3> into)
        {
            if (n <= 1) { into.Add(group); return; }
            float rr = 1f / Mathf.Sin(Mathf.PI / n);                                                         // 2 m sides: 2 / (2 sin(pi / n))
            var rot = Quaternion.Euler(0f, yawDeg, 0f); Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Mathf.PI * 0.5f;
                var p = group + right * (Mathf.Cos(a) * rr) - fwd * (Mathf.Sin(a) * rr); p.y = group.y;
                into.Add(p);
            }
        }

        /// <summary>The way a ghost looks: toward the camera, turned a third to the side (red to the left of the screen, blue and
        /// the empty ones to the right), as the prototype's preview pictures face.</summary>
        static float FaceYaw(Team team)
        {
            var cam = OzUeDraw.Cam; if (!cam) return 180f;
            Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) { f = cam.transform.up; f.y = 0f; }
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            Vector3 right = new Vector3(f.z, 0f, -f.x);
            float side = TeamColors.Shown(team) == Team.Red ? -0.6f : 0.6f;
            Vector3 d = -f + right * side;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ E205: the models of the drop (a unit 45 %, an empty vehicle / gun 50 %)
        // The game's own placing ghost (PlayerInput's cannon ghost: the unit's prefab, its colliders gone) with every part drawn as ONE even
        // see-through ghost in its team's colour (View/OzUeGhost.cs: the team's ring colour, red, blue or the neutral grey, shaded by its
        // own form; body, kit, head and weapon alike; the same on every graphics preset and screen size). No shadow, no light, no per-object
        // motion, no sound, nothing to click. Robots, vehicles and guns; the heroes and the shuttle (built by their own view code) show their
        // rings. Pooled per unit and team, hidden when unused.
        sealed class Model { public GameObject go; public Renderer[] rs; public int[] mats; public float modelYaw; public HashSet<Renderer> seen, ghosts; public int checkAt; }
        readonly Dictionary<string, List<Model>> models = new Dictionary<string, List<Model>>();
        readonly Dictionary<string, int> modelsUsed = new Dictionary<string, int>();
        readonly HashSet<string> noModel = new HashSet<string>();
        GameObject modelRoot, modelHold;
        /// <summary>Checks: ghost models placed in the last frame.</summary>
        internal static int ModelsShown;

        void PlaceModel(string id, Team team, Vector3 at, float faceYaw, float alpha)
        {
            string key = id + "|" + (int)team;
            if (noModel.Contains(key)) return;
            if (!models.TryGetValue(key, out var list)) models[key] = list = new List<Model>();
            modelsUsed.TryGetValue(key, out int n);
            Model m = n < list.Count ? list[n] : null;
            if (m == null || !m.go)
            {
                try { m = MakeModel(id, team); } catch (Exception e) { Debug.LogWarning("[OpenZ UnitEditor] no drop preview model for " + id + ": " + e.Message); m = null; }
                if (m == null) { noModel.Add(key); return; }
                if (n < list.Count) list[n] = m; else list.Add(m);
            }
            modelsUsed[key] = n + 1;
            var g = OzUnitEditor.I ? OzUnitEditor.I.Game : null;
            at.y = g ? OnSurface(g, at, g.GroundY(at.x, at.z)) : 0f;                                       // 1.5 ue_flag_ghost_height: on a platform its rules' height
            if (!m.go.activeSelf) m.go.SetActive(true);
            m.go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, faceYaw + m.modelYaw, 0f));
            Ghost(m, team, alpha);                                                                             // its team's see-through ghost (View/OzUePlace.Ghost.cs)
        }

        Model MakeModel(string id, Team team)
        {
            var g = OzUnitEditor.I ? OzUnitEditor.I.Game : null; if (!g) return null;
            var kind = OzUePlayground.KindOf(id);
            GameObject pf = null;
            if (kind == UeKind.Robot)                                                                          // ZGame.SpawnUnitView's choice of model
            {
                pf = id == "laser" && !g.Prefab("Z_bs01_laser") ? g.Prefab("Z_robot") : g.Prefab("Z_bs01_" + id);
                if (!pf) pf = g.Prefab(id == "tough" ? "Z_tough" : "Z_robot");
            }
            else if (kind == UeKind.Vehicle || kind == UeKind.Gun) pf = g.Prefab("Z_" + id);
            if (!pf) return null;
            if (!modelHold) { modelHold = new GameObject("OzUe_DropModelsHold"); modelHold.SetActive(false); }   // made here inactive: nothing of it runs before it is a ghost
            if (!modelRoot) modelRoot = new GameObject("OzUe_DropModels");
            var go = Instantiate(pf, modelHold.transform);
            go.name = "OzUe_DropModel_" + id;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);                    // never in the way of a click or a ground ray
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var em = ps.emission; em.enabled = false; }
            var keep = new List<Renderer>(); var mats = new List<int>();
            var all = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in all)
            {
                bool takes = OzUeGhost.Takes(r);                                                               // before its shadow is switched off: a shadow-only proxy is left out
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (takes) { keep.Add(r); mats.Add(r.sharedMaterials.Length); }
                else { r.enabled = false; OzUeGhost.LeftOut++; }                                               // glows, lenses, beams, particles: no ghost of them
            }
            if (keep.Count == 0) { Destroy(go); return null; }
            var tp = go.GetComponent<TeamPaint>(); if (tp) tp.Team = team;
            if (kind == UeKind.Robot) WeaponHold.Attach(go);                                                    // the weapon in the hands, as UnitView.Bind does it
            foreach (var cape in go.GetComponentsInChildren<OzEvilZodCape>(true)) cape.Held = true;             // 1.3 final: EvilZod's cloak holds still while he is dragged (View/OzEvilZodCape.cs)
            float modelYaw = 180f;                                                                             // UnitAim.ModelYaw: every model faces -z, unsplit imported cannons +x
            if (kind == UeKind.Gun && (id == "gatling" || id == "howitzer" || id == "missile_cannon"))
            {
                bool split = false;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith("TurretYaw", StringComparison.Ordinal)) { split = true; break; }
                if (!split) modelYaw = 270f;
            }
            go.SetActive(false);                                                                               // switched on (Awake, its paint) where it is first placed
            go.transform.SetParent(modelRoot.transform, false);
            return new Model { go = go, rs = keep.ToArray(), mats = mats.ToArray(), modelYaw = modelYaw, seen = new HashSet<Renderer>(all), ghosts = new HashSet<Renderer>(keep) };
        }

        void HideUnusedModels()
        {
            int shown = 0;
            foreach (var kv in models)
            {
                modelsUsed.TryGetValue(kv.Key, out int n); shown += n;
                for (int i = n; i < kv.Value.Count; i++) { var m = kv.Value[i]; if (m != null && m.go && m.go.activeSelf) m.go.SetActive(false); }
            }
            ModelsShown = shown;
        }

        void DestroyModels()
        {
            if (modelRoot) Destroy(modelRoot);
            if (modelHold) Destroy(modelHold);
            modelRoot = modelHold = null; models.Clear(); modelsUsed.Clear(); noModel.Clear(); ModelsShown = 0;
        }

        /// <summary>Canvas units from the centre's screen point down to the near edge of a footprint of half size `half` metres.</summary>
        static float PxDown(Vector3 centre, float half)
        {
            var cam = OzUeDraw.Cam; if (!cam) return 20f;
            float mpp = OzUeDraw.MetresPerPixel(cam, centre);
            return half / Mathf.Max(0.0005f, mpp) / Mathf.Max(0.01f, OzUnitEditor.PxPerUnit) * Mathf.Sin(Mathf.Clamp(cam.transform.eulerAngles.x, 10f, 90f) * Mathf.Deg2Rad);
        }

        // ------------------------------------------------------------------ E185 drag ghost
        void ShowGhost(bool on, bool overField, Vector2 mp)
        {
            if (!on) { if (ghostRt && ghostRt.gameObject.activeSelf) ghostRt.gameObject.SetActive(false); return; }
            if (!ghostRt) BuildGhost();
            if (!ghostRt) return;
            if (!ghostRt.gameObject.activeSelf) ghostRt.gameObject.SetActive(true);
            if (ghostFor != OzUePlace.Id || ghostTeam != OzUePlace.Team) Paint();
            float size = overField ? 32f : 56f;
            var hud = OzUnitEditor.I.Hud;
            Vector2 p = hud.ScreenToCanvas(mp);
            if (overField) p += new Vector2(14f + size * 0.5f, -14f - size * 0.5f);                    // beside the mouse, right / down, the floor preview shows the drop
            ghostRt.sizeDelta = new Vector2(size, size);
            ghostRt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            float a = overField ? 0.55f : 1f;
            ghost.color = new Color(ghost.color.r, ghost.color.g, ghost.color.b, a);
            glowRaw.enabled = !overField;
            foreach (var e in edge) { var c = e.color; c.a = (overField ? 0.55f : 1f); e.color = c; }
            if (glyph) glyph.color = OzUi.A(OzUi.Ink, a);
        }

        void BuildGhost()
        {
            var layer = OzUnitEditor.Layer(UeLayer.Drag); if (!layer) return;
            ghostRt = Hud.Node("OzUe_DragGhost", layer);
            ghostRt.anchorMin = ghostRt.anchorMax = Vector2.zero; ghostRt.pivot = new Vector2(0.5f, 0.5f); ghostRt.sizeDelta = new Vector2(56, 56);
            glowTex = OzUeShell.SoftSquare(64, 18f / 92f);
            var gr = Hud.Node("Glow", ghostRt); gr.Fill(-18, -18, -18, -18);                               // E185: an AMBER 50 % glow 18 u round the picture
            glowRaw = gr.gameObject.AddComponent<RawImage>(); glowRaw.texture = glowTex; glowRaw.color = OzUi.A(OzUi.Amber, 0.5f); glowRaw.raycastTarget = false;
            var well = Hud.Img(ghostRt, "Well", OzUi.A(OzUi.GM950, 0.75f)); well.rectTransform.Fill();
            var pr = Hud.Node("Picture", ghostRt); pr.Fill(2, 2, 2, 2);
            ghost = pr.gameObject.AddComponent<RawImage>(); ghost.raycastTarget = false;
            glyph = Hud.Icon(ghostRt, "Glyph", "flag", 0, 0, 24, OzUi.Ink); glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.pivot = new Vector2(0.5f, 0.5f); glyph.rectTransform.anchoredPosition = Vector2.zero; glyph.enabled = false;
            edge = Hud.Border(ghostRt, OzUi.Ink, 2f);
            ghostRt.gameObject.SetActive(false);
        }

        void Paint()
        {
            ghostFor = OzUePlace.Id; ghostTeam = OzUePlace.Team;
            OzUeArt.Apply(ghost, ghostFor, ghostTeam);
            bool pic = ghost.texture != null;
            ghost.enabled = pic;
            if (glyph)
            {
                glyph.enabled = !pic;
                if (!pic) Hud.SetIcon(glyph, OzUePlayground.KindOf(ghostFor) == UeKind.Obstacle ? "cube" : OzUePlayground.KindOf(ghostFor) == UeKind.Flag ? "flag" : "robot", 24);
            }
            Color e = ghostTeam == Team.Neutral ? OzUi.A(OzUi.Khaki, 0.7f) : OzUeUi.Ring(ghostTeam);
            foreach (var b in edge) b.color = e;
        }
    }
}
