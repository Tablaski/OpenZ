using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k83 (1.2 look fixes on the faction screen): two causes found on the game's own pictures; counted in edit mode by
    /// Editor/OzV12LookFixesCheck.cs, applied by UI/OpenZMainMenu.FactionLook.cs (hooks in UI/OpenZMainMenu.FactionDeck.cs) and the poster
    /// shader's halo mode (Shaders/OzPosterFigure.shader, _Print 3).
    ///  * The DOSSIER rows: a row wider than its box goes on two lines, broken after its label, as the owner's mock-up ("Tendencies:" above
    ///    "World Hostile Takeover"). The card measured its rows while it was still switched off: a TextMeshPro text that has not woken up
    ///    measures at a tenth of its size (its UI flag is set when it wakes), so every row seemed to fit on one line and TextMeshPro then
    ///    broke the long ones at the last word that fitted ("Tendencies: World Hostile" / "Takeover"); the rows' heights were measured
    ///    the same way. The card is now switched on before its words are made, and its texts are marked as UI texts from the start.
    ///  * The VS sign's halo: a soft glow made in sRGB steps (as image tools mix colours: the preview and the owner's mock-up). The game
    ///    renders in linear light, where the UI's blend lifted such a glow two to three times as much over the dark backdrop, its faint
    ///    tails most: a smeared red / blue cloud round the sign. The halo is now drawn with the poster shader's halo mode: its colour mixed
    ///    with the screen's colour behind the VS in sRGB steps, so over that colour it reads exactly as drawn (<see cref="HaloOver"/>).
    /// </summary>
    public static class OzFactionLook
    {
        /// <summary>The poster shader's halo mode (its _Print value).</summary>
        public const float HaloMode = 3f;

        /// <summary>A copy of the poster shader's material set to draw the VS sign's halo (no rim, no effect, no light band).</summary>
        public static void SetHalo(Material m)
        {
            if (!m) return;
            m.SetFloat("_Print", HaloMode);
            m.SetFloat("_RimPx", 0f);
            m.SetFloat("_FxMode", 0f);
            m.SetVector("_Sweep", new Vector4(-1f, 0.1f, 0f, 0f));
        }

        // ---------------------------------------------------------------- the screen's mixes (sRGB colours in and out; a = the halo's alpha)
        /// <summary>As the preview and the owner's mock-up mix: in sRGB steps.</summary>
        public static Color GammaOver(Color c, float a, Color b) => new Color(b.r + (c.r - b.r) * a, b.g + (c.g - b.g) * a, b.b + (c.b - b.b) * a, 1f);

        /// <summary>As the default UI material drew the halo before (k75a3): blended in linear light.</summary>
        public static Color LinearOver(Color c, float a, Color b)
        {
            Color l = c.linear, k = b.linear;
            return new Color(k.r + (l.r - k.r) * a, k.g + (l.g - k.g) * a, k.b + (l.b - k.b) * a, 1f).gamma;
        }

        /// <summary>As the halo mode draws it (the shader's HaloMix and the premultiplied blend) over the backdrop b, when the material is
        /// told the screen's colour behind it is b0: exactly the sRGB mix where b is b0; elsewhere the difference passes through in linear light.</summary>
        public static Color HaloOver(Color c, float a, Color b, Color b0)
        {
            Color m = GammaOver(c, a, b0).linear, k0 = b0.linear, k = b.linear;
            float r = Mathf.Max(0f, m.r - (1f - a) * k0.r) + (1f - a) * k.r;
            float g = Mathf.Max(0f, m.g - (1f - a) * k0.g) + (1f - a) * k.g;
            float bl = Mathf.Max(0f, m.b - (1f - a) * k0.b) + (1f - a) * k.b;
            return new Color(r, g, bl, 1f).gamma;
        }
    }
}
