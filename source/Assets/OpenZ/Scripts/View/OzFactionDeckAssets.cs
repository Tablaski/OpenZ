using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// k75a3 (1.2 owner items 101 / 102): the pictures, fonts and motion maps of the faction screen's VS sign, live prints and DOSSIER
    /// cards (UI/OzFactionDeck.cs, UI/OpenZMainMenu.FactionDeck.cs). Made by the main menu when the faction screen opens and closed when
    /// it is left, as the posters' rig (View/OzFactionPosters.cs): nothing of it exists in a level and nothing of it stays loaded.
    ///  * Loads asynchronously from Resources/OpenZ/Faction: the VS sign picked by OzFactionDeck.VsSign and its halo, the two cards, the
    ///    icon atlas, the two posters' motion maps and the cards' three fonts (the heading's condensed capitals, the typewriter's regular
    ///    and bold; open font licence, see the fonts' OFL files).
    ///  * Makes the cards' text fonts (dynamic SDF font assets, the bold one as the regular one's bold weight, so &lt;b&gt; draws the real
    ///    bold) when the menu's text runs on TextMeshPro; else the menu draws the cards with the fonts themselves (legacy text).
    ///  * <see cref="Close"/>: the font assets it made are destroyed (their atlases and materials too), then every asset it loaded is
    ///    unloaded (Resources.UnloadAsset).
    /// </summary>
    public sealed class OzFactionDeckAssets : MonoBehaviour
    {
        public static OzFactionDeckAssets I { get; private set; }
        /// <summary>Checks: opened / closed since the game started; the assets the last close unloaded; the font assets it destroyed.</summary>
        public static int Opened { get; private set; }
        public static int Closed { get; private set; }
        public static int LastUnloadCount { get; private set; }
        public static int LastFontsDestroyed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { I = null; Opened = Closed = LastUnloadCount = LastFontsDestroyed = 0; }

        public int VsSign { get; private set; }
        public Texture2D Vs { get; private set; }
        public Texture2D VsHalo { get; private set; }
        public Texture2D Icons { get; private set; }
        public readonly Texture2D[] Card = new Texture2D[2];
        public readonly Texture2D[] Motion = new Texture2D[2];
        public Font Heading { get; private set; }
        public Font Body { get; private set; }
        public Font Bold { get; private set; }
        public TMP_FontAsset HeadingTmp { get; private set; }
        public TMP_FontAsset BodyTmp { get; private set; }
        public TMP_FontAsset BoldTmp { get; private set; }
        public bool Ready { get; private set; }
        public bool Loading { get; private set; }
        public float LoadSeconds { get; private set; } = -1f;
        /// <summary>The names of what it loads (the Play proof counts what is left of them after a close).</summary>
        public static readonly List<string> Names = new List<string>();

        readonly List<Object> loaded = new List<Object>();
        readonly List<Object> made = new List<Object>();
        bool closing; float openedAt;

        public static OzFactionDeckAssets Open(Transform parent, int vsSign)
        {
            if (I && !I.closing && I.VsSign == vsSign) return I;
            var go = new GameObject("OzFactionDeckAssets");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<OzFactionDeckAssets>();
            d.VsSign = vsSign == OzFactionDeck.VsGlow ? OzFactionDeck.VsGlow : OzFactionDeck.VsGrunge;
            return d;
        }

        void Awake() { I = this; Opened++; openedAt = Time.realtimeSinceStartup; }
        void Start() { StartCoroutine(Load()); }

        T Keep<T>(T o) where T : Object { if (o) { loaded.Add(o); if (!Names.Contains(o.name)) Names.Add(o.name); } return o; }

        IEnumerator Load()
        {
            Loading = true;
            var vs = Resources.LoadAsync<Texture2D>(OzFactionDeck.VsTex[VsSign]);
            var halo = Resources.LoadAsync<Texture2D>(OzFactionDeck.VsHaloTex[VsSign]);
            var icons = Resources.LoadAsync<Texture2D>(OzFactionDeck.IconTex);
            var cards = new ResourceRequest[2]; var maps = new ResourceRequest[2];
            for (int i = 0; i < 2; i++) { cards[i] = Resources.LoadAsync<Texture2D>(OzFactionDeck.CardTex[i]); maps[i] = Resources.LoadAsync<Texture2D>(OzFactionDeck.MotionTex[i]); }
            var fh = Resources.LoadAsync<Font>(OzFactionDeck.FontHeading);
            var fb = Resources.LoadAsync<Font>(OzFactionDeck.FontBody);
            var fx = Resources.LoadAsync<Font>(OzFactionDeck.FontBold);
            yield return vs; yield return halo; yield return icons;
            Vs = Keep(vs.asset as Texture2D); VsHalo = Keep(halo.asset as Texture2D); Icons = Keep(icons.asset as Texture2D);
            for (int i = 0; i < 2; i++)
            {
                yield return cards[i]; yield return maps[i];
                Card[i] = Keep(cards[i].asset as Texture2D); Motion[i] = Keep(maps[i].asset as Texture2D);
            }
            yield return fh; yield return fb; yield return fx;
            Heading = Keep(fh.asset as Font); Body = Keep(fb.asset as Font); Bold = Keep(fx.asset as Font);
            if (!closing && OzText.UsesTmp && Heading && Body)
            {
                try
                {
                    HeadingTmp = MakeFont(Heading, "OzFaction_Heading (runtime)");
                    BodyTmp = MakeFont(Body, "OzFaction_Typewriter (runtime)");
                    if (Bold) BoldTmp = MakeFont(Bold, "OzFaction_TypewriterBold (runtime)");
                    if (BodyTmp && BoldTmp && BodyTmp.fontWeightTable != null && BodyTmp.fontWeightTable.Length > 7)
                        BodyTmp.fontWeightTable[7].regularTypeface = BoldTmp;                                     // <b> draws with the real bold
                }
                catch (System.Exception e) { Debug.LogWarning("[OpenZ Menu] faction dossier fonts: " + e.Message + " (the cards use the plain text)"); HeadingTmp = BodyTmp = BoldTmp = null; }
            }
            foreach (var n in new[] { (Vs, OzFactionDeck.VsTex[VsSign]), (VsHalo, OzFactionDeck.VsHaloTex[VsSign]), (Icons, OzFactionDeck.IconTex), (Card[0], OzFactionDeck.CardTex[0]),
                                      (Card[1], OzFactionDeck.CardTex[1]), (Motion[0], OzFactionDeck.MotionTex[0]), (Motion[1], OzFactionDeck.MotionTex[1]) })
                if (!n.Item1) Debug.LogWarning("[OpenZ Menu] faction screen: Resources/" + n.Item2 + " missing");
            if (!Heading || !Body || !Bold) Debug.LogWarning("[OpenZ Menu] faction screen: a dossier font is missing (Resources/" + OzFactionDeck.Folder + "Fonts)");
            LoadSeconds = Time.realtimeSinceStartup - openedAt;
            Loading = false;
            Ready = !closing;
            if (Ready) Debug.Log($"[OpenZ Menu] faction VS / dossiers loaded in {LoadSeconds * 1000f:0} ms: the {(VsSign == OzFactionDeck.VsGlow ? "glowing" : "painted")} VS, {loaded.Count} assets, {(BodyTmp ? "TextMeshPro" : "plain text")} cards");
        }

        TMP_FontAsset MakeFont(Font f, string name)
        {
            var fa = TMP_FontAsset.CreateFontAsset(f, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            if (!fa) return null;
            fa.name = name; fa.hideFlags = HideFlags.DontSave;
            made.Add(fa);
            return fa;
        }

        /// <summary>Leaves: (after any load in flight) the font assets it made are destroyed, then every loaded asset is unloaded.</summary>
        public void Close()
        {
            if (closing) return;
            closing = true; Ready = false; Closed++;
            StartCoroutine(CloseRoutine());
        }

        IEnumerator CloseRoutine()
        {
            while (Loading) yield return null;
            int fonts = 0;
            foreach (var o in made)
            {
                if (!(o is TMP_FontAsset fa) || !fa) continue;
                if (fa.material) Destroy(fa.material);
                if (fa.atlasTextures != null) foreach (var t in fa.atlasTextures) if (t) Destroy(t);
                Destroy(fa); fonts++;
            }
            made.Clear();
            HeadingTmp = BodyTmp = BoldTmp = null;
            Vs = VsHalo = Icons = null; Card[0] = Card[1] = Motion[0] = Motion[1] = null; Heading = Body = Bold = null;
            yield return null; yield return null;                                                             // the menu's pictures and texts let go first
            int n = 0;
            foreach (var o in loaded) if (o) { Resources.UnloadAsset(o); n++; }
            loaded.Clear();
            LastUnloadCount = n; LastFontsDestroyed = fonts;
            Debug.Log($"[OpenZ Menu] faction VS / dossiers closed: {fonts} font assets destroyed, {n} assets unloaded");
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            foreach (var o in made) if (o) Destroy(o);
            if (I == this) I = null;
        }
    }
}
