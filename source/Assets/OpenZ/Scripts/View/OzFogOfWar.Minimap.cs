// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the minimap covered the same way.
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR on the minimap (UI/OpenZMinimap.cs is not changed): one picture over the map image, right above its unit and
    /// building markers and under the camera frame, the pings and the glass. It is the reveal map itself (a texel a metre, bilinear, so its
    /// edges are as soft as the fog's): the planet's cover colour (MiniOf), fully covering where nothing is explored (no marker shows through),
    /// open where the player's side has been. Repainted at most 5 times a second, only when the reveal map, the style or the planet changed.
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        RawImage miniImg; Texture2D miniTex; Color32[] miniBuf; float miniNext; bool miniDirty = true; int miniLook = -1;
        /// <summary>Checks: the minimap cover picture (null before the HUD's minimap exists or with the fog off).</summary>
        public static RawImage MinimapCover => I ? I.miniImg : null;
        public static int MinimapPaints;

        void MinimapStep()
        {
            bool want = (wanted || radarFog) && mask != null;
            if (!miniImg) { if (!want || !MinimapBuild()) return; }
            bool show = want && strength > 0.001f;
            if (miniImg.enabled != show) miniImg.enabled = show;
            if (!show) return;
            var c = miniImg.color; if (!Mathf.Approximately(c.a, strength)) miniImg.color = new Color(1f, 1f, 1f, strength);
            if ((miniDirty || miniLook != look * 8 + PlanetIndex) && Time.unscaledTime >= miniNext) { MinimapPaint(); miniNext = Time.unscaledTime + 0.2f; }
        }

        bool MinimapBuild()
        {
            var hud = OpenZHud.I; var mm = hud ? hud.Minimap : null;
            if (!mm || !mm.MapRect) return false;
            var rt = Hud.Node("FogOfWar", mm.MapRect); rt.Fill();
            var markers = mm.MapRect.Find("Markers");
            rt.SetSiblingIndex(markers ? markers.GetSiblingIndex() + 1 : mm.MapRect.childCount - 1);        // over the markers, under the camera frame and the pings
            miniImg = rt.gameObject.AddComponent<RawImage>(); miniImg.raycastTarget = false;
            miniImg.uvRect = new Rect(0f, 1f, 1f, -1f);                                                      // the mask's first row is the map's north edge, the top of the minimap
            if (miniTex) Destroy(miniTex);                                                                   // a rebuilt HUD: the old picture went with it
            miniTex = new Texture2D(mask.W, mask.H, TextureFormat.RGBA32, false) { name = "OZ_FogOfWarMinimap", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            miniBuf = new Color32[mask.W * mask.H];
            miniImg.texture = miniTex; miniImg.enabled = false;
            miniDirty = true;
            return true;
        }

        void MinimapPaint()
        {
            if (!miniTex || mask == null) return;
            var k = radarFog ? Color.gray : MiniOf[PlanetIndex]; byte r = (byte)(k.r * 255f), g = (byte)(k.g * 255f), b = (byte)(k.b * 255f);
            var s = mask.Shown;
            for (int i = 0; i < s.Length; i++) miniBuf[i] = new Color32(r, g, b, (byte)((255 - s[i]) * (radarFog ? .42f : 1)));
            miniTex.SetPixels32(miniBuf); miniTex.Apply(false, false);
            miniDirty = false; miniLook = look * 8 + PlanetIndex; MinimapPaints++;
        }

        void MinimapDestroy()
        {
            if (miniImg) Destroy(miniImg.gameObject);
            if (miniTex) Destroy(miniTex);
        }
    }
}
