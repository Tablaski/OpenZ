using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace OpenZ
{
    /// <summary>Experimental converted-map browser. Normal operations retain their existing selection and loading flow.</summary>
    public sealed class OzConvertedMapsScreen : MonoBehaviour
    {
        public static readonly OzLevelDef Mayhem = new OzLevelDef("L19", "L19_Mayhem_Converted", "19", "MAYHEM", "CITY · EXPERIMENTAL CONVERSION", "Wordmarks/mask_mayhem", "Capture the flags. Break into the enemy fort.", "Capture the flags. Break into the enemy fort.", new[] { "CITY", "ORIGINAL ZOD MAP", "EXPERIMENTAL" });
        OpenZMainMenu menu;
        readonly List<Tuple<HudButton, Action>> controls = new List<Tuple<HudButton, Action>>();
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
            var go = new GameObject("Converted map browser", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var view = go.AddComponent<OzConvertedMapsScreen>(); view.menu = owner; owner.enabled = false;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            var scale = go.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1920,1080); scale.matchWidthOrHeight = 1;
            view.Build(go.GetComponent<RectTransform>());
        }
        void Build(RectTransform root)
        {
            Hud.Img(root,"Background",OzUi.GM950).rectTransform.Fill();
            Hud.Label(root,"Brand","OpenZ-VK",28,new Color(.85f,.35f,.07f)).At(80,45);
            Hud.Label(root,"Title","SHITTY MAPS",44,OzUi.Ink).At(80,110);
            Hud.Label(root,"Subtitle","EXPERIMENTAL ZOD CONVERSIONS",15,OzUi.Muted).At(80,172);
            float screenWidth=Screen.width*1080f/Mathf.Max(1,Screen.height);
            Add(root,"Back","BACK",new OzRect(Mathf.Max(650,screenWidth-300),60,220,56),Close);
            var panel=Hud.Node("MayhemCard",root).Place(new OzRect(110,245,530,640));
            Hud.Img(panel,"Card",OzUi.GM900).rectTransform.Fill();
            var mini=Hud.Node("Minimap",panel).Place(new OzRect(20,20,490,440)).gameObject.AddComponent<RawImage>();
            mini.texture=OzLevelInfo.Load("L19").Mini; mini.raycastTarget=false;
            Hud.Label(panel,"Name","Level 19 - Mayhem",24,OzUi.Ink).At(24,485);
            Hud.Label(panel,"Planet","City",18,OzUi.Amber).At(24,528);
            Hud.Label(panel,"Source","Original Zod Engine · converted layout",13,OzUi.Muted).At(24,562);
            Add(panel,"Deploy","DEPLOY",new OzRect(24,590,482,40),()=>{menu.enabled=true;menu.DeployConvertedMap(Mayhem);Destroy(gameObject);});
            Hud.Label(root,"Disclaimer","Experimental terrain conversion. Original starting teams and object coordinates are retained.",15,OzUi.Muted).At(110,930);
        }
        void Add(RectTransform parent,string name,string label,OzRect rect,Action action)
        {
            var button=HudButton.Make(parent,name,label,new Vector2(rect.w,rect.h),HudButtonKind.Large,16);button.Rt.Place(rect);
            controls.Add(Tuple.Create(button,action));
        }
        void Update()
        {
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close();return;}
            if(Mouse.current==null)return;
            foreach(var entry in controls)
            {
                bool hover=RectTransformUtility.RectangleContainsScreenPoint(entry.Item1.Rt,Mouse.current.position.ReadValue());
                if(entry.Item1.Hovered!=hover){entry.Item1.Hovered=hover;entry.Item1.Refresh();}
                if(hover&&Mouse.current.leftButton.wasPressedThisFrame){entry.Item2();return;}
            }
        }
        void Close(){if(menu)menu.enabled=true;Destroy(gameObject);}
    }
}
