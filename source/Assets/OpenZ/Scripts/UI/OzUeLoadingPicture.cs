using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor's loading screen, UI/OpenZMainMenu.UnitEditor.cs): the playground's own loading picture, shown the way a
    /// level's loading screen shows its still (UI/OpenZMainMenu.cs BuildLoading): Resources/OpenZ/Loading/UE_loading_2 (made like the
    /// levels' stills: a PNG 2560 wide, imported as they are) covering the screen without stretching (OzLevelPreview.Cover: cropped to
    /// the screen's shape), the two soft scrims of a level's loading screen (left 50 %, bottom 90 %, the same colour) so the eyebrow, the
    /// name, the bar and the tips read as on a level's, all under the vignette and the words. Added to the main menu's object when its
    /// scene loads (no line of the menu changes); a click on UNIT EDITOR (OpenZMainMenu.UnitEditorStarts) makes it put the picture into the
    /// loading root the menu just built (the root fades in from 0, so the picture is there from its first visible frame). Without the
    /// picture the screen keeps its plain bed (one warning in the log).
    /// </summary>
    public sealed class OzUeLoadingPicture : MonoBehaviour
    {
        /// <summary>1.2 item 64: the second Unit editor picture (UE_loading_2, made as UE_loading_1 was: 2560 x 1440, 8-bit RGB, the same
        /// import); UE_loading_1 stays in the project, no longer shown.</summary>
        public const string Path = "OpenZ/Loading/UE_loading_2";
        /// <summary>The pictures the Unit editor's loading screen picks from, at random each time it shows, never the same one twice in a
        /// row (UE_loading_2 / _3 / _4: 2560 x 1440, 8-bit RGB, the same import; UE_loading_1 stays unused). Shown = the one picked last,
        /// LastPick its index (-1 none yet).</summary>
        public static readonly string[] Paths = { Path, "OpenZ/Loading/UE_loading_3", "OpenZ/Loading/UE_loading_4" };
        public static string Shown = Path;
        public static int LastPick = -1;
        static readonly System.Random pickRng = new System.Random();
        /// <summary>A random index of Paths other than last (any index while last is -1).</summary>
        public static int Pick(int last, System.Random rng)
        {
            int n = Paths.Length;
            if (n < 2) return 0;
            if (last < 0 || last >= n) return rng.Next(n);
            int k = rng.Next(n - 1);
            return k >= last ? k + 1 : k;
        }
        public const string NodeName = "UePicture";
        /// <summary>Checks: pictures put on a loading screen since the game started; the last uv rect and the picture's size.</summary>
        public static int Attached;
        public static Rect LastUv;
        public static Vector2 LastSize;
        /// <summary>Checks: the UNIT EDITOR click (OpenZMainMenu.UnitEditorStarts) the last picture was put on for, and the one whose
        /// loading screen the proof held (-1 none). The picture goes on in the click's own frame, before the proof's next step runs.</summary>
        public static int AttachedFor = -1, HeldFor = -1;
        /// <summary>The Play proof only (View/OzUeProof.Plain.cs, set before its first click on UNIT EDITOR): that loading screen stays up
        /// at least ProofHoldS seconds, so the proof's picture is taken on it (the editor loads the playground within the menu's 1.2 s).</summary>
        public static bool ProofHoldNext;
        public const float ProofHoldS = 5f;
        static bool hooked, warned, lost;
        static Texture2D scrimH, scrimV;
        static OzUeLoadingPicture shownBy;
        int seen; float armedUntil, armedAt; RawImage poster; Texture tex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryInstall(); }
            TryInstall();
        }

        static void TryInstall()
        {
            var menu = FindFirstObjectByType<OpenZMainMenu>();
            if (menu && !menu.GetComponent<OzUeLoadingPicture>()) menu.gameObject.AddComponent<OzUeLoadingPicture>();
        }

        void Awake() { seen = OpenZMainMenu.UnitEditorStarts; }

        void LateUpdate()
        {
            int s = OpenZMainMenu.UnitEditorStarts;
            if (s != seen)
            {
                seen = s; armedAt = Time.unscaledTime; armedUntil = armedAt + 30f; poster = null;
                var menu = GetComponent<OpenZMainMenu>();
                if (ProofHoldNext && OzUeProof.Running && menu)
                {
                    ProofHoldNext = false; HeldFor = s;
                    menu.minLoadingTime = Mathf.Max(menu.minLoadingTime, ProofHoldS);                          // this menu object ends with its scene: the next one has its own 1.2 s
                }
            }
            if (Time.unscaledTime > armedUntil) return;
            if (poster) { poster.uvRect = Cover(); return; }                                                 // the window's shape may change while it loads
            var root = Loading();
            if (root && !root.Find(NodeName)) Attach(root);
            else if (!root && !lost && Time.unscaledTime - armedAt > 2f)
            {
                lost = true;                                                                                  // the menu built its loading screen somewhere else
                Debug.LogWarning("[OpenZ Menu] the Unit editor's loading screen was not found under OpenZ_MainMenu_UI/Root/Loading: no picture on it");
            }
        }

        /// <summary>The menu's newest loading root (its UI: OpenZ_MainMenu_UI / Root / Loading).</summary>
        RectTransform Loading()
        {
            var ui = transform.Find("OpenZ_MainMenu_UI/Root");
            if (!ui) return null;
            for (int i = ui.childCount - 1; i >= 0; i--)
            {
                var c = ui.GetChild(i);
                if (c.name == "Loading" && c.gameObject.activeInHierarchy) return c as RectTransform;
            }
            return null;
        }

        Rect Cover() => tex ? OzLevelPreview.Cover((float)tex.width / Mathf.Max(1, tex.height), (float)Screen.width / Mathf.Max(1, Screen.height)) : new Rect(0, 0, 1, 1);

        /// <summary>Checks (the Play proof): the picture is on the screen now: the poster drawn with its picture at full strength, the
        /// loading screen faded in, the poster over the whole screen. state = the same in words.</summary>
        public static bool OnScreen(out string state)
        {
            var p = shownBy ? shownBy.poster : null;
            var cg = p ? p.GetComponentInParent<CanvasGroup>() : null;
            bool drawn = p && p.isActiveAndEnabled && p.texture && p.color.a > 0.99f;
            float a = cg ? cg.alpha : 0f;
            bool covers = false;
            if (p)
            {
                var c = new Vector3[4]; p.rectTransform.GetWorldCorners(c);                                    // the menu's overlay canvas: world = screen pixels
                covers = c[0].x <= 0.5f && c[0].y <= 0.5f && c[2].x >= Screen.width - 0.5f && c[2].y >= Screen.height - 0.5f;
            }
            state = "the picture drawn: " + drawn + ", the loading screen faded in: " + a.ToString("0.00", CultureInfo.InvariantCulture) + ", over the whole "
                    + Screen.width + " x " + Screen.height + " screen: " + covers;
            return drawn && a > 0.99f && covers;
        }

        void Attach(RectTransform root)
        {
            int pick = Pick(LastPick, pickRng); string path = Paths[pick];
            tex = Resources.Load<Texture2D>(path);
            if (!tex && path != Path) { path = Path; tex = Resources.Load<Texture2D>(path); }                // an extra picture missing: the first one
            LastPick = pick; Shown = path;
            if (!tex)
            {
                if (!warned) { warned = true; Debug.LogWarning("[OpenZ Menu] the Unit editor's loading picture " + Path + " is missing: its loading screen keeps the plain bed"); }
                armedUntil = 0f; return;
            }
            var holder = Hud.Node(NodeName, root); holder.Fill();
            var bed = root.Find("Bed");
            holder.SetSiblingIndex(bed ? bed.GetSiblingIndex() + 1 : 0);                                       // over the bed, under the vignette and the words
            poster = Raw(holder, "Poster"); poster.texture = tex; poster.uvRect = Cover(); poster.color = Color.white;
            var gl = Raw(holder, "ScrimLeft"); gl.texture = Scrim(true); gl.color = OzUi.A(new Color32(10, 13, 11, 255), 0.5f);
            float cw = Screen.height > 0 ? Screen.width * OzLayout.RefH / Screen.height : OzLayout.RefW;          // the menu's canvas width (it matches the height)
            gl.rectTransform.anchorMax = new Vector2(Mathf.Min(1f, 1100f / Mathf.Max(1f, cw)), 1f);
            var gb = Raw(holder, "ScrimBottom"); gb.texture = Scrim(false); gb.color = OzUi.A(new Color32(10, 13, 11, 255), 0.9f);
            gb.rectTransform.anchorMax = new Vector2(1f, 0.54f);
            Attached++; AttachedFor = seen; shownBy = this; LastUv = poster.uvRect; LastSize = new Vector2(tex.width, tex.height);
            Debug.Log("[OpenZ Menu] the Unit editor's loading picture " + Shown + " (" + tex.width + "x" + tex.height + ") on its loading screen, cropped to the screen's shape (uv "
                      + LastUv.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + LastUv.y.ToString("0.###", CultureInfo.InvariantCulture) + " "
                      + LastUv.width.ToString("0.###", CultureInfo.InvariantCulture) + "x" + LastUv.height.ToString("0.###", CultureInfo.InvariantCulture) + ")");
        }

        static RawImage Raw(RectTransform parent, string name)
        {
            var rt = Hud.Node(name, parent); rt.Fill();
            var r = rt.gameObject.AddComponent<RawImage>(); r.raycastTarget = false; r.color = new Color(1, 1, 1, 0); return r;
        }

        /// <summary>The level loading screen's soft scrim: alpha 1 -> 0 along x (left) or y (bottom), smoothstep.</summary>
        static Texture2D Scrim(bool horizontal)
        {
            var cached = horizontal ? scrimH : scrimV;
            if (cached) return cached;
            var t = new Texture2D(horizontal ? 256 : 1, horizontal ? 1 : 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OZ_UeScrim" };
            var px = new Color32[256];
            for (int i = 0; i < 256; i++) { float a = 1f - i / 255f; a = a * a * (3f - 2f * a); px[i] = new Color32(255, 255, 255, (byte)(a * 255)); }
            t.SetPixels32(px); t.Apply(false, true);
            if (horizontal) scrimH = t; else scrimV = t;
            return t;
        }
    }
}
