using UnityEngine;

namespace OpenZ
{
    public static class OzAiMapBadge
    {
        static Sprite robot;

        public static void Stamp(RectTransform preview, float width)
        {
            var badge = Hud.Chip(preview, "AI stamp", new OzRect(width - 87, 5, 78, 27),
                "AI", OzUi.GM950, OzUi.Ink, 13, .12f);
            var icon = Hud.Img(badge.bg.transform.parent, "Robot head", Color.white);
            icon.sprite = Robot();
            icon.rectTransform.Place(new OzRect(49, 4, 22, 20));
            icon.raycastTarget = false;
        }

        static Sprite Robot()
        {
            if (robot) return robot;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "AI robot head" };
            var pixels = new Color32[32 * 32];
            Box(15, 23, 2, 6, new Color32(157, 176, 186, 255));
            Box(13, 28, 6, 3, new Color32(254, 148, 79, 255));
            Box(3, 10, 26, 10, new Color32(119, 143, 153, 255));
            Box(6, 6, 20, 18, new Color32(185, 207, 214, 255));
            Box(9, 10, 14, 10, new Color32(32, 46, 51, 255));
            Box(11, 14, 3, 3, new Color32(143, 238, 228, 255));
            Box(18, 14, 3, 3, new Color32(143, 238, 228, 255));
            Box(13, 11, 6, 1, new Color32(157, 176, 186, 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return robot = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f));

            void Box(int x, int y, int w, int h, Color32 color)
            {
                for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++) pixels[yy * 32 + xx] = color;
            }
        }
    }
}
