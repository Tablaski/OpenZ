using System.Collections.Generic;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// (-45: PointerOverUi of this frame) and before PlayerInput (0), which keeps the game's picking and orders (both teams through
    /// View/PlayerInput.BothSides.cs):
    ///  * the field input is held off while placing by click (OzUePick.HoldInput);
    ///  * the hover (any unit, flag, obstacle or grenade crate under the pointer; a squad member = its leader; nothing over a panel);
    ///  * a right click with NOTHING picked: on a unit it picks it (its inspector opens; that release gives no order), on a flag, an
    ///  * a right click with units picked = the game's order; the hint toasts of the prototype where the game gives no order: a plain
    ///    right-click on the obstacle ("CTRL + RIGHT-CLICK ATTACKS THE OBSTACLE", the units walk to it; armed units without explosives,
    ///    plain or with Ctrl: "ONLY EXPLOSIVES HURT THE OBSTACLE: ...", the game's rule for map items), on empty hardware with no robot
    ///    picked ("ONLY ROBOTS CLIMB IN. CTRL + RIGHT-CLICK ATTACKS IT"), a forced attack no picked unit can make ("THE PICKED UNITS HAVE
    ///    NO WEAPON");
    ///  * a left click on a flag, an obstacle or empty hardware (it picks no unit) shows that object; on empty ground: back to the list;
    ///  * a box that picked more than one unit: "N UNITS SELECTED. DELETE REMOVES THEM.";
    ///  * this frame's sim events (before CombatView clears them): "BLUE TOOK THE FLAG( FROM RED)", "BLUE GRUNT CLIMBED INTO THE LIGHT
    ///    TANK", "OBSTACLE DESTROYED", "EMPTY LIGHT TANK DESTROYED", the flags' take counts, FieldChanged;
    ///  * LateUpdate (after PlayerInput): the inspector follows the selection, then OzUePick.Changed / OzUePlayground.FieldChanged once.
    /// Esc closers (OzUeKeys, after every module built): EscUnpick (70) unpicks, EscObject (60) leaves the flag / obstacle / empty hardware.
    /// </summary>
    [DefaultExecutionOrder(-38)]
    public sealed class OzUeField : MonoBehaviour
    {
        /// <summary>Proof counters: units picked by a right click, objects shown by a click, hint toasts, event toasts.</summary>
        public static int RightPicks, ObjectClicks, Hints, EventToasts;

        OzUnitEditor ed;
        ZObject liftCandidate;
        bool lifting;
        bool suppressRightRelease;
        readonly List<ZObject> lifted = new List<ZObject>();
        readonly List<Vector2Int> liftOffsets = new List<Vector2Int>();

        bool TestingPointer(Mouse m, Keyboard kb, PlayerInput input, Vector2 mp, bool overUi)
        {
            var panel = OzBehaviorPanel.I;
            if (panel && panel.RespawnPointTeam != Team.Neutral)
            {
                input.enabled = false; heldByUs = true;
                if (m.rightButton.wasPressedThisFrame)
                { suppressRightRelease = true; panel.FinishRespawnPoint(); return true; }
                if (m.leftButton.wasPressedThisFrame && !overUi && OzUePlaceDriver.FloorPoint(ed, mp, out var p))
                {
                    var px = ZGame.WorldToPx(p);
                    if (W.UnitTesting.SetRespawnPoint(panel.RespawnPointTeam, px.x, px.y))
                    { panel.FinishRespawnPoint(); OzUeToast.Show("RESPAWN POINT SET"); }
                    else OzUeToast.Show("CHOOSE CLEAR GROUND INSIDE THE FIELD");
                }
                return true;
            }
            if (m.leftButton.wasPressedThisFrame && !overUi && !OzUePick.InputHeld && OzUePick.Hover != null && OzUePick.Hover.IsUnit)
            {
                liftCandidate = OzUePick.Hover; leftDownAt = mp;
                OzUePick.HoldInput(this, true);
            }
            if (liftCandidate == null) return false;
            if (liftCandidate.Destroyed || W.Get(liftCandidate.Id) != liftCandidate) { ReleaseLift(); return true; }
            input.enabled = false; heldByUs = true;
            if (!lifting && (mp - leftDownAt).sqrMagnitude > PlayerInput.BoxSqr)
            {
                lifting = true; lifted.Add(liftCandidate); lifted.AddRange(liftCandidate.Minions);
                foreach (var o in lifted)
                {
                    liftOffsets.Add(new Vector2Int(o.CenterX - liftCandidate.CenterX, o.CenterY - liftCandidate.CenterY));
                    W.UnitTesting.Hold(o, true);
                }
            }
            if (lifting && !overUi && OzUePlaceDriver.FloorPoint(ed, mp, out var ground))
            {
                var px = ZGame.WorldToPx(ground);
                bool valid = true;
                for (int i = 0; i < lifted.Count; i++)
                    if (!W.UnitTesting.CanPlace(lifted[i], px.x + liftOffsets[i].x, px.y + liftOffsets[i].y)) valid = false;
                if (valid) for (int i = 0; i < lifted.Count; i++)
                    lifted[i].SetCords(px.x + liftOffsets[i].x - lifted[i].WidthPix / 2, px.y + liftOffsets[i].y - lifted[i].HeightPix / 2);
            }
            if (!m.leftButton.isPressed || !Application.isFocused || OzUeHooks.AnyModal)
            {
                if (!lifting && !overUi) OzUePick.Pick(new[] { liftCandidate }, kb != null && kb.shiftKey.isPressed);
                ReleaseLift();
            }
            return true;
        }
        void ReleaseLift()
        {
            foreach (var o in lifted) W?.UnitTesting?.Hold(o, false);
            lifted.Clear(); liftOffsets.Clear(); liftCandidate = null; lifting = false;
            OzUePick.HoldInput(this, false);
        }
        bool heldByUs;
        Vector2 rightDownAt, leftDownAt; bool rightOnField, leftOnField, leftWasDown;
        int leftReleaseFrame = -1; bool leftReleaseDrag; ZObject leftReleaseHover;

        void Awake()
        {
            ed = GetComponent<OzUnitEditor>();
            OzUnitEditor.UiReady += RegisterEsc;
        }

        void OnDestroy()
        {
            ReleaseLift();
            OzUnitEditor.UiReady -= RegisterEsc;
            if (heldByUs && ed && ed.Input) ed.Input.enabled = true;
        }

        void RegisterEsc()
        {
            OzUeKeys.OnEsc(115, () =>
            {
                if (OzBehaviorPanel.I && OzBehaviorPanel.I.RespawnPointTeam != Team.Neutral)
                { OzBehaviorPanel.I.FinishRespawnPoint(); return true; }
                if (liftCandidate == null) return false;
                ReleaseLift(); return true;
            });
            OzUeKeys.OnEsc(OzUeKeys.EscUnpick, () =>
            {
                var i = ed ? ed.Input : null;
                if (!i || i.Selection.Count == 0) return false;
                OzUePick.Unpick(); AudioDirector.UI("ui_cancel"); return true;
            });
            OzUeKeys.OnEsc(OzUeKeys.EscObject, () =>
            {
                var k = OzUePick.Kind;
                if (k != UeInspect.Flag && k != UeInspect.Obstacle && k != UeInspect.EmptyHardware && k != UeInspect.Crate && k != UeInspect.Platform) return false;
                OzUePick.Set(UeInspect.None, null, null, Team.Neutral); AudioDirector.UI("ui_cancel"); return true;
            });
        }

        ZWorld W => ed ? ed.World : null;

        void Update()
        {
            var input = ed ? ed.Input : null; var w = W;
            if (!input || w == null) return;
            // click-to-place owns the mouse: PlayerInput stays off (re-enabled in LateUpdate, after the HUD's own gating)
            if (OzUePick.InputHeld) { input.enabled = false; heldByUs = true; }
            var m = Mouse.current; var kb = Keyboard.current; var hud = OpenZHud.I;
            if (m == null) { ReleaseLift(); return; }
            if (suppressRightRelease && m.rightButton.wasReleasedThisFrame)
            { suppressRightRelease = false; rightOnField = false; OzUeHooks.RightPickFrame = Time.frameCount; }
            if (OzFpsView.On) { ReleaseLift(); OzUePick.Hover = null; leftOnField = rightOnField = false; ScanEvents(w); return; }   // 1.5 fps_in_ue: the view's clicks are its own (fire); the sim events still go to the toasts
            Vector2 mp = m.position.ReadValue();
            bool overUi = (hud && (hud.PointerOverUi || hud.Modal)) || OzUeHooks.AnyModal || mp.x < 0f || mp.y < 0f || mp.x > Screen.width || mp.y > Screen.height;
            OzUePick.Hover = overUi ? null : HoverAt(input, w, mp);
            if (TestingPointer(m, kb, input, mp, overUi)) return;
            bool placing = OzUePick.InputHeld;
            ScanEvents(w);

            // ---- left button: remember a press that went down on the field (the release is decided in LateUpdate)
            if (m.leftButton.wasPressedThisFrame) { leftOnField = !overUi && !placing; leftDownAt = mp; }
            if (m.leftButton.wasReleasedThisFrame && leftOnField && !placing)
            {
                leftReleaseFrame = Time.frameCount;
                leftReleaseDrag = (mp - leftDownAt).sqrMagnitude > PlayerInput.BoxSqr;
                leftReleaseHover = OzUePick.Hover;
            }

            // ---- right button: a click (no drag) picks with nothing picked; with units picked the game orders, the hints are ours
            if (m.rightButton.wasPressedThisFrame) { rightOnField = !overUi && !placing; rightDownAt = mp; }
            if (m.rightButton.wasReleasedThisFrame && rightOnField)
            {
                rightOnField = false;
                if (!overUi && !placing && OzRightDrag.Click(m, OzRightDrag.SiteOrders)) RightClick(input, w, kb);
            }
        }

        void RightClick(PlayerInput input, ZWorld w, Keyboard kb)
        {
            var h = OzUePick.Hover;
            if (input.Selection.Count == 0)
            {
                if (h == null) return;
                if (h.IsUnit && h.Owner != Team.Neutral)
                {
                    OzUePick.Pick(new[] { h }, false);
                    OzUeHooks.RightPickFrame = Time.frameCount;                                    // this release gives no order (PlayerInput `over`)
                    RightPicks++;
                    Debug.Log($"{OzUnitEditor.Tag} right click picked {OzUeUi.TeamWord(h.Owner)} {OzUePlayground.IdOf(h)} #{h.Id} (nothing was picked)");
                }
                else { OzUePick.InspectObject(h); ObjectClicks++; if (OzUePick.Inspected == h) AudioDirector.UI(ObjectClickSound); }
                return;
            }
            if (h == null) return;
            bool ctrl = kb != null && kb.ctrlKey.isPressed;
            bool anyArmed = false, anyRobot = false, anyBlast = false;
            foreach (var u in input.Selection)
            {
                if (u == null || u.Destroyed) continue;
                if (u.CanAttack()) { anyArmed = true; if (u.HasExplosives()) anyBlast = true; }
                if (u.Kind == ZKind.Robot) anyRobot = true;
            }
            if (OzUePlayground.IsObstacle(h))
            {   // the game's rule for map items (CanAttackObject): only explosives hurt the obstacle, so armed units without them are told so
                if (anyArmed && !anyBlast) Hint(ExplosivesOnly);
                else if (!ctrl) Hint(OzUePlayground.IsBox(h) ? "CTRL + RIGHT-CLICK ATTACKS THE BOX" : "CTRL + RIGHT-CLICK ATTACKS THE OBSTACLE");   // 1.5 ue_boxes
                else if (!anyArmed) Hint("THE PICKED UNITS HAVE NO WEAPON");
            }
            else if (OzUePlayground.IsPlatform(h))
            {   // 1.3: a plain click is a move onto it, no hint; Ctrl attacks it
                if (ctrl && anyArmed && !anyBlast) Hint(PlatformExplosivesOnly);
                else if (ctrl && !anyArmed) Hint("THE PICKED UNITS HAVE NO WEAPON");
            }
            else if (OzUePlayground.IsEmptyHardware(h))
            {
                if (!ctrl && !anyRobot) Hint("ONLY ROBOTS CLIMB IN. CTRL + RIGHT-CLICK ATTACKS IT");
                else if (ctrl && !anyArmed) Hint("THE PICKED UNITS HAVE NO WEAPON");
            }
            else if (h.IsUnit && ctrl && !anyArmed) Hint("THE PICKED UNITS HAVE NO WEAPON");
        }

        static void Hint(string text) { Hints++; OzUeToast.Show(text); }
        /// <summary>A flag, an obstacle, empty hardware or a grenade crate shown in the STATS box by a click on the field: the click sound, as a
        /// dock tile's (every visible action has a sound).</summary>
        public const string ObjectClickSound = "ui_click";
        /// <summary>The hint when the picked units are armed but none has explosives (rifles, machine guns, flames, lasers, the lightning gun).</summary>
        public const string ExplosivesOnly = "ONLY EXPLOSIVES HURT THE OBSTACLE: TOUGHS, TANKS, MECHS, MLRS, CANNONS";
        public const string PlatformExplosivesOnly = "ONLY EXPLOSIVES HURT A PLATFORM: TOUGHS, TANKS, MECHS, MLRS, CANNONS";

        // ------------------------------------------------------------------------------------------------ hover
        /// <summary>1.3 item 8 (the unit cap, View/OzUeCapBench.cs): the hover asked every unit of the field for its pick box every frame
        /// (PlayerInput.ScreenRect: up to 8 corner projections, the mesh box of a vehicle). Now a unit whose drawn point is far from the
        /// pointer on the screen is skipped first: farther than the projection of a HoverCullM sphere round it (x 1.5) and than HoverCullMinPx
        /// (x the screen's scale), which no pick box of the game reaches (the shuttle, the biggest unit, is about 13 m; the drawn model sits
        /// up to ~2 m off its sim spot; MinSize / Grow add at most 25 px). The unit picked is the same as before (the last one whose box
        /// holds the pointer: a skipped unit's box never held it); points behind the camera are never skipped. HoverCull = false: the old
        /// full pass (the bench's "same hover" check compares both on the same frames). View only.</summary>
        public static bool HoverCull = true;
        public const float HoverCullM = 20f, HoverCullMinPx = 80f;
        static readonly ProfilerMarker HoverMarker = new ProfilerMarker("OzUe.Hover");
        /// <summary>Checks: units skipped by the cull and asked for their box, the last frame.</summary>
        public static int HoverSkipped, HoverAsked;

        /// <summary>The bench's "same hover" check: the hover at a screen point with the cull as given (the field's own pass).</summary>
        internal static ZObject HoverProbe(Vector2 mp, bool cull)
        {
            var ed0 = OzUnitEditor.I; var input = ed0 ? ed0.Input : null; var w = ed0 ? ed0.World : null;
            if (!input || w == null) return null;
            bool was = HoverCull; HoverCull = cull;
            try { return HoverAt(input, w, mp); } finally { HoverCull = was; }
        }

        static bool FarFromPointer(Camera cam, ZGame g, ZObject o, Vector2 mp, float focal, float minPx)
        {
            Vector3 p;
            if (g != null && g.Views.TryGetValue(o.Id, out var v) && v && v.gameObject.activeInHierarchy) p = v.transform.position;
            else { p = ZGame.PxToWorld(o.CenterX, o.CenterY); if (g != null) p.y = g.GroundY(p.x, p.z); }
            var s = cam.WorldToScreenPoint(p);
            if (s.z <= 0.5f) return false;                                                                          // near or behind the camera: always asked
            float r = Mathf.Max(HoverCullM * focal / s.z * 1.5f, minPx);
            float dx = s.x - mp.x, dy = s.y - mp.y;
            return dx * dx + dy * dy > r * r;
        }

        /// <summary>The last unit (a member = its leader) under the pointer, else the last flag / obstacle (units beat items, as the game).</summary>
        static ZObject HoverAt(PlayerInput input, ZWorld w, Vector2 mp)
        {
            using var mark = HoverMarker.Auto();
            ZObject hu = null, h = null;
            var cam = input.cam ? input.cam.GetComponent<Camera>() : Camera.main;
            if (!cam) return null;
            var g = OzUnitEditor.I ? OzUnitEditor.I.Game : null;
            bool cull = HoverCull && !cam.orthographic;
            float focal = cull ? cam.pixelHeight * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) : 0f, minPx = HoverCullMinPx * Mathf.Max(0.75f, Screen.height / 1080f);
            int skipped = 0, asked = 0;
            var objs = w.Objects;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o.Destroyed || o.KillAt >= 0) continue;
                if (o.IsUnit)
                {
                    if (o.IsFortDefender) continue;
                    if (cull && FarFromPointer(cam, g, o, mp, focal, minPx)) { skipped++; continue; }                // 1.3 item 8: no pick box far from the pointer
                    asked++;
                    if (!input.ScreenRect(o).Contains(mp)) continue;
                    var l = o.Leader;
                    hu = l != null && !l.Destroyed && l.KillAt < 0 && l.Owner == o.Owner ? l : o;
                }
                else if (OzUePlayground.IsFlag(o) || OzUePlayground.IsObstacle(o) || OzUePlayground.IsCrate(o))
                {
                    float hm = OzUePlayground.IsFlag(o) ? FlagHeightM : OzUePlayground.IsCrate(o) ? CrateHeightM : OzUePlayground.ObstacleHeightM;
                    if (BoxRect(cam, o, hm).Contains(mp)) h = o;
                }
                else if (OzUePlayground.IsPlatform(o)) { if (OzUePlatformViews.Under(mp) == o && !OzUePlayground.IsFlag(h)) h = o; }   // 1.3: by the mouse's ground ray on its footprint: units on the deck stay pickable; ue_flag_anywhere: a flag on it too
            }
            h = OzUeBoxes.Pick(cam, mp, h);                                                                 // 1.5 ue_boxes: the box the pointer is on, on its level (not its floor footprint)
            HoverSkipped = skipped; HoverAsked = asked;
            return hu ?? h;
        }

        /// <summary>Flag pick box height (the pole, View/FlagView.cs poleHeight 3.4 m, and its cloth).</summary>
        const float FlagHeightM = 3.6f;
        /// <summary>Grenade crate pick box height: the game's own for a map item (PlayerInput.ScreenRect), so the crate under the pointer
        /// is the one a right-click sends the picked robots to.</summary>
        const float CrateHeightM = 1.2f;

        /// <summary>The screen rect of an object's sim footprint extruded to a height (the flag's pole, the obstacle's 3 m box).</summary>
        static Rect BoxRect(Camera cam, ZObject o, float height)
        {
            var a = ZGame.PxToWorld(o.LocX, o.LocY); var b = ZGame.PxToWorld(o.LocX + o.WidthPix, o.LocY + o.HeightPix);
            var mid = ZGame.PxToWorld(o.CenterX, o.CenterY); float y0 = OzUePlatformViews.UnderM(mid.x, mid.z);   // ue_flag_anywhere: a flag on a ramp platform (0 with none)
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue); bool any = false;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? a.x : b.x, y0 + ((i & 4) == 0 ? 0f : height), (i & 2) == 0 ? a.z : b.z);
                var s = cam.WorldToScreenPoint(p);
                if (s.z < 0f) continue;
                any = true; mn = Vector2.Min(mn, s); mx = Vector2.Max(mx, s);
            }
            return any ? Rect.MinMaxRect(mn.x, mn.y, mx.x, mx.y) : new Rect(-9999f, -9999f, 0f, 0f);
        }

        // ------------------------------------------------------------------------------------------------ sim events (this frame's)
        void ScanEvents(ZWorld w)
        {
            var ev = w.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                switch (e.type)
                {
                    case ZEventType.FlagCaptured:
                    {
                        var team = (Team)(int)e.value; var old = (Team)e.radius;
                        OzUePlayground.NoteFlagTaken(e.objId);
                        Toast(OzUeUi.TeamWord(team) + " TOOK THE FLAG" + (old != Team.Neutral && old != team ? " FROM " + OzUeUi.TeamWord(old) : ""));
                        OzUePlayground.MarkChanged();
                        break;
                    }
                    case ZEventType.Entered:
                    {
                        var r = w.GetAny(e.objId); var v = w.GetAny(e.otherId);
                        if (!e.flag && r != null && v != null && v.IsUnit)
                            Toast(OzUeUi.TeamWord(r.Owner) + " " + RobotName(r) + " CLIMBED INTO THE " + (v.IsShuttle ? "SHUTTLE" : UnitInfo.Name(v.Type)));
                        OzUePlayground.MarkChanged();
                        break;
                    }
                    case ZEventType.Death:
                    {
                        var o = w.GetAny(e.objId);
                        if (OzUePlayground.IsObstacle(o)) Toast(OzUePlayground.IsBox(o) ? "BOX DESTROYED" : "OBSTACLE DESTROYED");   // 1.5 ue_boxes
                        else if (OzUePlayground.IsPlatform(o)) Toast("PLATFORM DESTROYED");
                        else if (o != null && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner == Team.Neutral)
                            Toast("EMPTY " + (o.IsShuttle ? "SHUTTLE" : UnitInfo.Name(o.Type)) + " DESTROYED");
                        OzUePlayground.MarkChanged();
                        break;
                    }
                    case ZEventType.OwnerChanged: case ZEventType.Created: case ZEventType.Ejected: case ZEventType.Sniped:
                        OzUePlayground.MarkChanged();
                        break;
                }
            }
        }

        static string RobotName(ZObject r) => r.IsHero ? OzHeroSquadView.NameOr(r, UnitInfo.Name(r.Type)) : UnitInfo.Name(r.Type);
        static void Toast(string text) { EventToasts++; OzUeToast.Show(text); }

        // ------------------------------------------------------------------------------------------------ after PlayerInput
        void LateUpdate()
        {
            var input = ed ? ed.Input : null; var w = W;
            if (heldByUs && !OzUePick.InputHeld) { heldByUs = false; if (input) input.enabled = true; }
            if (!input || w == null) return;
            var m = Mouse.current;
            bool leftHeld = m != null && m.leftButton.isPressed && leftOnField;
            OzUePick.Reconcile(leftHeld);
            if (leftReleaseFrame == Time.frameCount)
            {
                leftReleaseFrame = -1; leftOnField = false;
                if (leftReleaseDrag)
                {
                    int n = 0; foreach (var u in input.Selection) n += UnitsOf(u);
                    if (n > 1) OzUeToast.Show(n + " UNITS SELECTED. DELETE REMOVES THEM.");
                }
                else if (input.Selection.Count == 0)
                {
                    var h = leftReleaseHover;
                    if (h != null && (OzUePlayground.IsFlag(h) || OzUePlayground.IsObstacle(h) || OzUePlayground.IsEmptyHardware(h) || OzUePlayground.IsCrate(h) || OzUePlayground.IsPlatform(h)))
                    { OzUePick.InspectObject(h); ObjectClicks++; AudioDirector.UI(ObjectClickSound); }
                    else if (h == null && OzUePick.Kind != UeInspect.None) OzUePick.Set(UeInspect.None, null, null, Team.Neutral);   // a click on empty ground: back to the list
                }
                leftReleaseHover = null;
            }
            else if (m != null && !m.leftButton.isPressed && leftWasDown) leftOnField = false;
            leftWasDown = m != null && m.leftButton.isPressed;
            OzUePick.RaiseIfChanged();
            OzUePlayground.RaiseFieldChangedIfDirty();
        }

        static int UnitsOf(ZObject u)
        {
            if (u == null || u.Destroyed || !u.IsUnit) return 0;
            int n = 1; foreach (var x in u.Minions) if (x != null && !x.Destroyed && x.KillAt < 0) n++;
            return n;
        }
    }
}
