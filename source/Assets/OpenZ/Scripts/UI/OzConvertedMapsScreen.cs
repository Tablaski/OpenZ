using UnityEngine;
namespace OpenZ
{
    public static class OzConvertedMapsScreen
    {
        static Sprite poop;
        public static Sprite PoopSprite()
        {
            if (poop) return poop;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                var c = Color.clear;
                if (Ellipse(x,y,32,18,27,12) || Ellipse(x,y,32,31,21,12) || Ellipse(x,y,32,43,14,11) || Ellipse(x,y,30,51,7,9)) c = new Color(.40f,.20f,.08f);
                if (Ellipse(x,y,23,29,5,6) || Ellipse(x,y,42,29,5,6)) c = Color.white;
                if (Ellipse(x,y,24,29,2,3) || Ellipse(x,y,41,29,2,3)) c = Color.black;
                if (y > 16 && y < 20 && x > 25 && x < 40) c = Color.white;
                texture.SetPixel(x,y,c);
            }
            texture.Apply(); poop = Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f)); return poop;
        }
        static bool Ellipse(int x,int y,float cx,float cy,float rx,float ry) => Mathf.Pow((x-cx)/rx,2)+Mathf.Pow((y-cy)/ry,2)<1;
        public static void Open(OpenZMainMenu owner)
        {
            owner.enabled=false;
            OzShittyMapGallery.Open(true,true,null,d=>owner.DeployConvertedMap(d),()=>{if(owner)owner.enabled=true;},
                d => owner.DeployConvertedMap(d, paletteV3: true));
        }
    }
}
