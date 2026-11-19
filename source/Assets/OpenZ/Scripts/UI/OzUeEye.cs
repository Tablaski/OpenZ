using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// the OzUnitEditor's own object when its scene loads, as OzReadyGuns / OzCheatSpawner install themselves).
    /// <list type="bullet">
    /// <item>BUG "unit names spam in unit editor ... it should be only on mouse over and never stuck" (refs_1_5/ue_names_stuck_owner_20261108.webp:
    /// every unit carries its name, no brackets, so none of them is picked): the only thing that draws a name over every unit is the
    /// BACKGROUND popover's SHOW option "Name labels on every unit" (View/OzUeMarks.cs: all = OzUeBackdrop.NameLabels). It was remembered in
    /// PlayerPrefs oz_ue_labels for good, so one stray tick (or a counted check stopped half way: View/OzUeStallCheck.cs and
    /// Editor/OzV13LightFloorsCheck.cs switch it on and only put it back when they finish) left every name on at every later visit. Now every
    /// visit of the Unit editor starts with it OFF: names on mouse-over only (the game's own hover chip) and over the units the player picked
    /// (E209, with HP); the box still works for the visit when ticked on purpose. Nothing else draws names: no key (Alt included) is read
    /// for them.</item>
    /// <item>NEW "add in top right corner small eye button that same as f10 hides on /off toggle all interface in unit editor": a 28 x 28 u
    /// kit button in the top bar's right corner (UI/OzUePlan.cs Eye; MAIN MENU and the rest of the right group stand EyeSize + EyeGap
    /// further left), an eye drawn in code (open = the interface shows, closed with lashes = it is hidden). A click does what the bound HIDE
    /// THE HUD key (F10) does: OpenZHud.SetVisible(!hudVisible). It lives on its own overlay canvas (HUD order + 2: under the help overlay,
    /// the pause menu and the end screen), so it stays when the HUD canvas is switched off: dimmed (45 %, 90 % under the pointer), the
    /// closed eye. While the interface shows, the HUD polls it as any HUD button (hover plate, the HUD click sound, its tooltip
    /// "HIDE THE INTERFACE" with the bound key); while it is hidden the HUD takes no pointer, so this component reads the click itself
    /// (press and release on the eye, the same HUD click sound) and holds the field's input while the pointer is on the eye
    /// (OzUePick.HoldInput: no unpick, box or order through the eye; a click-to-place left armed is stopped by the press, so no unit drops
    /// under the eye). Hidden while a dialog, the pause menu or a confirm owns the screen.
    /// </item>
    /// </list>
    /// Proof counters: Clicks, Toggles, LabelResets, Builds. Log lines start "[OpenZ UnitEditor] eye button" and
    /// "[OpenZ UnitEditor] name labels".
    /// </summary>
    [DefaultExecutionOrder(-39)]                                                                         // after the HUD (-45) and the editor root (-40), before OzUeField (-38) reads the field hold
    public sealed class OzUeEye : MonoBehaviour
    {
        /// <summary>The icon's size in the 28 u button, the hidden eye's alpha (and under the pointer), the fade-in after the editor's UI is built.</summary>
        public const float IconSize = 18f, HiddenAlpha = 0.45f, HiddenHoverAlpha = 0.9f, FadeIn = 0.3f;
        /// <summary>The eye's canvas sorts this far over the HUD's (the help overlay is +3, the pause menu +5, the end screen +10).</summary>
        public const int SortAbove = 2;
        public const string TipTitle = "HIDE THE INTERFACE", TipLine = "Hides every panel of the Unit editor. The eye stays: click it again to bring them back";

        public static OzUeEye I { get; private set; }
        /// <summary>Proof: clicks on the eye (both paths), interface toggles by the eye, visits that started with the name labels switched
        /// back off, eye canvases built.</summary>
        public static int Clicks, Toggles, LabelResets, Builds;
        /// <summary>Proof: "shown" / "hidden" after the last click on the eye (null before the first).</summary>
        public static string LastToggle;

        Canvas canvas; RectTransform root; HudButton btn; RawImage icon; CanvasGroup group;
        Texture2D openTex, closedTex;
        bool built, held, leftOnEye, rightOnEye, shownOpen = true;
        float builtAt, nextTip; string tipKey;

        /// <summary>The eye's button (null until the editor's UI is built): proofs click its rect.</summary>
        public HudButton Button => btn;

        // ------------------------------------------------------------------------------------------------ install: the playground only
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryInstall(); }
            TryInstall();
        }

        static void TryInstall()
        {
            var ed = OzUnitEditor.I;                                                                     // set in its Awake (only with a world: else the editor stays off)
            if (!ed || !OzUnitEditor.IsPlayground(ed.gameObject.scene) || ed.GetComponent<OzUeEye>()) return;
            ed.gameObject.AddComponent<OzUeEye>();
        }

        void Awake()
        {
            I = this;
            Clicks = Toggles = 0; LastToggle = null;
            // the bug: "Name labels on every unit" holds for one visit only; every visit starts with names on mouse-over (and over the picked units)
            if (OzUeBackdrop.NameLabels)
            {
                OzUeBackdrop.SetNameLabels(false);                                                     // also writes oz_ue_labels 0 (its own log line)
                LabelResets++;
                Debug.Log("[OpenZ UnitEditor] name labels: the option \"Name labels on every unit\" was left on: off for this visit (names on mouse-over and over the picked units only)");
            }
        }

        void OnDestroy()
        {
            Hold(false);
            if (I == this) I = null;
            if (openTex) Destroy(openTex);
            if (closedTex) Destroy(closedTex);
        }

        void OnDisable() { Hold(false); leftOnEye = rightOnEye = false; }

        // ------------------------------------------------------------------------------------------------ build (once the HUD canvas is up)
        void Build(OpenZHud hud)
        {
            built = true; builtAt = Time.unscaledTime; Builds++;
            var go = new GameObject("OzUe_EyeCanvas", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(transform, false);                                                   // goes with the playground scene
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = hud.sortingOrder + SortAbove; canvas.pixelPerfect = true;
            canvas.scaleFactor = OzUnitEditor.PxPerUnit;                                               // one canvas unit = the HUD's (no CanvasScaler: set every frame below)
            root = Hud.Node("UE_Eye", go.transform);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one; root.sizeDelta = new Vector2(OzUePlan.EyeSize, OzUePlan.BtnH);
            group = root.gameObject.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false; group.alpha = 0f;
            btn = HudButton.Make(root, "Button", null, new Vector2(OzUePlan.EyeSize, OzUePlan.BtnH), HudButtonKind.Small, 12f, 0.12f, HudButtonStyle.Normal);
            btn.Rt.Fill();
            btn.OnClick = _ => Click(false);                                                           // the HUD's path (it plays its click sound after OnClick)
            openTex = Shape(true); closedTex = Shape(false);
            var ir = Hud.Node("Icon", btn.Content);
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f); ir.sizeDelta = new Vector2(IconSize, IconSize); ir.anchoredPosition = Vector2.zero;
            icon = ir.gameObject.AddComponent<RawImage>(); icon.raycastTarget = false; icon.texture = openTex; icon.color = OzUi.Ink2;
            hud.AddBlocker(root);                                                                      // over the eye the pointer is on the HUD (no field hover, no field click)
            Tip();
            Debug.Log("[OpenZ UnitEditor] eye button built: top bar right corner, " + OzUePlan.EyeSize + " x " + OzUePlan.BtnH + " u, canvas order " + canvas.sortingOrder
                      + ", toggles the interface as " + (tipKey.Length > 0 ? tipKey : "the HIDE THE HUD key (unbound)"));
        }

        // ------------------------------------------------------------------------------------------------ every frame
        void Update()
        {
            var hud = OpenZHud.I;
            if (!built)
            {
                if (!OzUnitEditor.UiBuilt || !hud || !hud.Root) return;
                Build(hud);
            }
            bool show = hud && hud.isActiveAndEnabled && hud.Canvas && !hud.Modal && !OzConfirm.Blocks && !OzFpsView.On;   // 1.5 fps_in_ue: hidden in the first-person view (the rest of the interface goes with the HUD canvas)
            root.SetActiveFast(show);
            if (!show) { Hold(false); leftOnEye = rightOnEye = false; return; }

            // place: the top bar's right corner on the HUD's canvas (same units, same compact rule)
            float k = OzUnitEditor.PxPerUnit;
            if (Mathf.Abs(canvas.scaleFactor - k) > 0.0001f) canvas.scaleFactor = k;
            var cs = OzUnitEditor.CanvasSize;
            var r = OzUePlan.Eye(cs.x, OzUeShellRoot.Compact(cs));
            var pos = new Vector2(-(cs.x - r.R), -r.y);
            if ((root.anchoredPosition - pos).sqrMagnitude > 0.0001f) root.anchoredPosition = pos;

            // the pointer: the HUD owns it while the interface shows; hidden, the eye reads its own click and holds the field
            bool hidden = !hud.hudVisible;
            var m = Mouse.current;
            bool over = m != null && OpenZHud.Contains(btn.Rt, m.position.ReadValue());
            bool lr = false, rr = false, anyDown = false;
            if (m != null)
            {
                if (m.leftButton.wasPressedThisFrame)
                {
                    leftOnEye = over && hidden;
                    if (leftOnEye && OzUePlace.Armed) OzUePlace.Disarm(false);                          // click-to-place armed before F10: no unit dropped under the eye (OzUePlaceDriver runs at -36, after this)
                }
                if (m.rightButton.wasPressedThisFrame) rightOnEye = over && hidden;
                lr = m.leftButton.wasReleasedThisFrame; rr = m.rightButton.wasReleasedThisFrame;
                anyDown = m.leftButton.isPressed || m.rightButton.isPressed;
            }
            Hold(hidden && (leftOnEye || rightOnEye || (over && !anyDown)));                            // a drag that started on the field and crosses the eye keeps going
            if (hidden)
            {
                bool pressed = leftOnEye && m != null && m.leftButton.isPressed;
                if (btn.Hovered != over || btn.Pressed != pressed) { btn.Hovered = over; btn.Pressed = pressed; btn.Refresh(); }
                if (lr && leftOnEye && over) Click(true);
            }
            if (lr) leftOnEye = false;
            if (rr) rightOnEye = false;

            // the look: open eye while the interface shows, closed and dimmed while it is hidden; INK under the pointer, else INK_2
            bool open = hud.hudVisible;
            if (open != shownOpen) { shownOpen = open; icon.texture = open ? openTex : closedTex; }
            Color c = btn.Hovered ? (Color)OzUi.Ink : (Color)OzUi.Ink2;
            if (icon.color != c) icon.color = c;
            float a = (open ? 1f : btn.Hovered ? HiddenHoverAlpha : HiddenAlpha) * Mathf.Clamp01((Time.unscaledTime - builtAt) / FadeIn);
            if (Mathf.Abs(group.alpha - a) > 0.001f) group.alpha = a;
            if (Time.unscaledTime >= nextTip) Tip();
        }

        /// <summary>The eye's click: exactly what the bound HIDE THE HUD key does (OpenZHud.cs Update: SetVisible(!hudVisible)).</summary>
        void Click(bool ownPath)
        {
            var hud = OpenZHud.I; if (!hud) return;
            bool showNow = !hud.hudVisible;
            hud.SetVisible(showNow);
            if (showNow && btn != null) { btn.Hovered = false; btn.Pressed = false; btn.Refresh(); }  // the HUD's own hover takes the button over again
            Clicks++; Toggles++; LastToggle = showNow ? "shown" : "hidden";
            if (ownPath) OpenZHud.UiClickSound();                                                       // the HUD's path plays the same click itself, after OnClick
            Debug.Log("[OpenZ UnitEditor] eye button: interface " + LastToggle + (ownPath ? " (click while hidden)" : ""));
        }

        /// <summary>The tooltip with the bound key's name (looked at once a second: the key can be changed in CONTROLS).</summary>
        void Tip()
        {
            nextTip = Time.unscaledTime + 1f;
            var key = OzBindings.KeyOf(OzAction.HideHud);
            string keyName = key == Key.None ? "" : OzKeyNames.Name(key);
            if (keyName == tipKey) return;
            tipKey = keyName;
            if (btn != null) btn.Tooltip = OzUeW.Tip(TipTitle, keyName.Length > 0 ? keyName : null, TipLine);
        }

        void Hold(bool on)
        {
            if (on == held) return;
            held = on;
            OzUePick.HoldInput(this, on);                                                              // OzUeField turns PlayerInput off while it is held
        }

        // ------------------------------------------------------------------------------------------------ the icon (drawn in code, as OzUeArt's)
        /// <summary>A white 64 px eye, anti-aliased (4 x 4 samples a texel), as OzUeArt.Shape draws the editor's small shapes. open: the
        /// almond outline (two arcs) and the iris with a small light; closed: the lid as a curve down with three lashes under it.
        /// tools/eye_icon_preview.py draws the same shapes (proof/eye_icon_*.png).</summary>
        static Texture2D Shape(bool open)
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { name = open ? "ue_eye_open" : "ue_eye_closed", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var lines = open ? OpenLines() : ClosedLines();
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int a = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (Inside(open, lines, x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) a++;
                    px[(n - 1 - y) * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a / 16f * 255f));
                }
            t.SetPixels32(px); t.Apply(true, true);
            return t;
        }

        /// <summary>Half the stroke (texels; 64 texels = 18 u: a 1.6 u line), the iris, its light.</summary>
        const float Half = 2.9f, IrisR = 9f, LightR = 2.8f, LightX = 35.5f, LightY = 28.5f;

        static bool Inside(bool open, List<Vector2> lines, float x, float y)
        {
            for (int i = 0; i + 1 < lines.Count; i += 2)
                if (SegDist(x, y, lines[i], lines[i + 1]) < Half) return true;
            if (!open) return false;
            float dx = x - 32f, dy = y - 32f, lx = x - LightX, ly = y - LightY;
            return dx * dx + dy * dy < IrisR * IrisR && lx * lx + ly * ly > LightR * LightR;
        }

        /// <summary>The open eye's outline as segment pairs: the upper lid through (6, 32) (32, 17) (58, 32), the lower through (32, 47).</summary>
        static List<Vector2> OpenLines()
        {
            var l = new List<Vector2>(96);
            Arc(l, new Vector2(6f, 32f), new Vector2(32f, 17f), new Vector2(58f, 32f), 24);
            Arc(l, new Vector2(6f, 32f), new Vector2(32f, 47f), new Vector2(58f, 32f), 24);
            return l;
        }

        /// <summary>The closed eye: the lid curving down through (7, 24) (32, 35) (57, 24) and three lashes 8 texels long, square to the
        /// lid, from x 17, 32 and 47.</summary>
        static List<Vector2> ClosedLines()
        {
            var l = new List<Vector2>(64);
            Vector2 a = new Vector2(7f, 24f), mid = new Vector2(32f, 35f), b = new Vector2(57f, 24f);
            Arc(l, a, mid, b, 24);
            Circle(a, mid, b, out var c, out float rad);
            foreach (float x in new[] { 17f, 32f, 47f })
            {
                float dx = x - c.x, dy = Mathf.Sqrt(Mathf.Max(0f, rad * rad - dx * dx));
                var p = new Vector2(x, c.y + dy);                                                       // on the lid (the circle's centre is above it)
                var nrm = (p - c) / rad;
                l.Add(p); l.Add(p + nrm * 8f);
            }
            return l;
        }

        /// <summary>The circle through a, mid, b (a and b level, mid half way between them in x).</summary>
        static void Circle(Vector2 a, Vector2 mid, Vector2 b, out Vector2 c, out float rad)
        {
            float hw = (b.x - a.x) * 0.5f, sag = mid.y - a.y;
            rad = (hw * hw + sag * sag) / (2f * Mathf.Abs(sag));
            c = new Vector2(mid.x, mid.y - Mathf.Sign(sag) * rad);
        }

        /// <summary>The arc a -> mid -> b as `segs` segments (pairs of points).</summary>
        static void Arc(List<Vector2> l, Vector2 a, Vector2 mid, Vector2 b, int segs)
        {
            Circle(a, mid, b, out var c, out float rad);
            float t0 = Mathf.Atan2(a.y - c.y, a.x - c.x), t1 = Mathf.Atan2(b.y - c.y, b.x - c.x);
            Vector2 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float t = Mathf.Lerp(t0, t1, i / (float)segs);
                var p = new Vector2(c.x + Mathf.Cos(t) * rad, c.y + Mathf.Sin(t) * rad);
                l.Add(prev); l.Add(p); prev = p;
            }
        }

        static float SegDist(float x, float y, Vector2 a, Vector2 b)
        {
            float dx = b.x - a.x, dy = b.y - a.y, len = dx * dx + dy * dy;
            float t = len > 0f ? Mathf.Clamp01(((x - a.x) * dx + (y - a.y) * dy) / len) : 0f;
            float ex = x - (a.x + dx * t), ey = y - (a.y + dy * t);
            return Mathf.Sqrt(ex * ex + ey * ey);
        }
    }
}
