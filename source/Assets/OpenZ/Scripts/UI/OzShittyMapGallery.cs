using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>Shared modal map gallery; inspection is available to guests, applying a lobby choice is host-only.</summary>
    public sealed class OzShittyMapGallery : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        bool solo, editable;
        string selected, group, world = "all";
        int sort;
        float scrollPosition=1;
        readonly Dictionary<string,Texture> previews=new Dictionary<string,Texture>();
        readonly List<Texture> generatedPreviews=new List<Texture>();
        RectTransform root;
        Action<OzLevelDef> apply;
        Action closed;
        static Sprite warning;
        readonly List<GameObject> content = new List<GameObject>();
        public static void Open(bool solo, bool editable, string selected, Action<OzLevelDef> apply, Action closed = null)
        {
            if (IsOpen) return;
            IsOpen = true;
            var go = new GameObject("Shitty map gallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var gallery = go.AddComponent<OzShittyMapGallery>();
            gallery.solo=solo; gallery.editable=editable; gallery.selected=selected; gallery.apply=apply; gallery.closed=closed;
            gallery.group=solo ? "Z ORIGINAL" : OzShittyMapCatalog.Find(selected)?.Group ?? "OPENZ";
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; // Above DuelCanvas (1000), including its full-screen input blocker.
            canvas.sortingOrder=1100;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=0;
            if (!EventSystem.current)
            {
                var events = new GameObject("Gallery input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(go.transform); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            gallery.root=go.GetComponent<RectTransform>(); gallery.Build();
        }
        public static OzLevelDef Definition(OzShittyMap map) => new OzLevelDef(map.Id,map.Scene,map.Number > 0 ? map.Number.ToString("00") : "MP",map.Name.ToUpperInvariant(),map.World.ToUpperInvariant()+" · SHITTY MAP","", "Capture the flags. Break into the enemy fort.","Capture the flags. Break into the enemy fort.",new[]{map.Group,map.World.ToUpperInvariant(),map.Variant});
        public static OzLevelDef Definition(string id) => OzShittyMapCatalog.Find(id) is OzShittyMap map ? Definition(map) : OzLevelDef.All.FirstOrDefault(d=>d.Id==id);
        void Build()
        {
            foreach(var go in content) { go.SetActive(false); Destroy(go); } content.Clear();
            var shade=Hud.Img(root,"Modal background",new Color(0,0,0,.94f)); shade.rectTransform.Fill(); shade.raycastTarget=true; content.Add(shade.gameObject);
            var panel=Hud.Node("Gallery",root); panel.TL(80,70,1760,940); content.Add(panel.gameObject);
            Hud.Img(panel,"Panel",OzUi.GM900).rectTransform.Fill();
            Label(panel,"OpenZ-VK",24,20,300,26,OzForkOptions.Brand);
            Label(panel,solo ? "SHITTY MAPS · SOLO" : "MAP GALLERY · 1 VS 1",24,65,1400,30,OzUi.Ink);
            Icon(panel,OzConvertedMapsScreen.PoopSprite(),330,16,38);
            Button(panel,"CLOSE",1510,20,220,Close);
            var groups=solo ? new[]{"Z ORIGINAL","Z EXPANSION"} : new[]{"OPENZ","Z ORIGINAL","Z EXPANSION","MULTIPLAYER"};
            for(int i=0;i<groups.Length;i++) { string value=groups[i]; Button(panel,value,24+i*420,118,400,()=>{group=value;scrollPosition=1;Build();},group==value); }
            var worlds=new[]{"all","desert","volcanic","arctic","jungle","city"};
            for(int i=0;i<worlds.Length;i++) { string value=worlds[i]; Button(panel,value.ToUpperInvariant(),24+i*222,180,210,()=>{world=value;scrollPosition=1;Build();},world==value); }
            Button(panel,"SORT: "+new[]{"NUMBER","NAME","WORLD"}[sort],1380,180,350,()=>{sort=(sort+1)%3;scrollPosition=1;Build();});
            Label(panel,solo ? "Original campaigns · levels 21 and 27 explicitly use FIXED files because their originals are absent." : editable ? "HOST SELECTION · applying a map clears both READY confirmations." : "READ ONLY · inspect every map; only the host can apply a selection.",24,240,1700,16,OzUi.Muted);
            var maps=new List<OzLevelDef>();
            if(group=="OPENZ")
            {
                maps.AddRange(OzLevelDef.All.Where(d=>world=="all" || World(d.Id)==world));
                if(sort==1) maps=maps.OrderBy(d=>d.Name).ToList();
                if(sort==2) maps=maps.OrderBy(d=>World(d.Id)).ThenBy(d=>d.Number).ToList();
            }
            else maps.AddRange(OzShittyMapCatalog.Select(solo,group,world,sort).Select(Definition));
            Label(panel,maps.Count+" MAPS",24,278,800,15,OzUi.Muted);
            var viewport=Hud.Node("Map viewport",panel); viewport.TL(24,310,1120,570); viewport.gameObject.AddComponent<RectMask2D>();
            var background=viewport.gameObject.AddComponent<Image>(); background.color=OzUi.GM950;
            var grid=Hud.Node("Cards",viewport); grid.TL(0,0,1120,Mathf.Max(570,Mathf.Ceil(maps.Count/4f)*244));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=grid; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=40; scroll.verticalNormalizedPosition=scrollPosition; scroll.onValueChanged.AddListener(p=>scrollPosition=p.y);
            for(int i=0;i<maps.Count;i++)
            {
                var map=maps[i]; var entry=OzShittyMapCatalog.Find(map.Id); bool active=selected==map.Id;
                var card=Button(grid,"",i%4*280,i/4*244,266,()=>{selected=map.Id;Build();},active,true,230);
                var mini=Hud.Node("Minimap",card).gameObject.AddComponent<RawImage>(); mini.texture=Preview(map.Id); float ratio=mini.texture ? (float)mini.texture.width/mini.texture.height : 1; float width=Mathf.Min(250,155*ratio),height=width/ratio; mini.rectTransform.TL(8+(250-width)/2,8+(155-height)/2,width,height); mini.raycastTarget=false;
                if(mini.texture) mini.uvRect=new Rect(0,0,1,1);
                Label(card,entry?.Title ?? map.Id+" · "+map.Name,10,174,246,17,OzUi.Ink);
                Label(card,World(map.Id).ToUpperInvariant()+" · "+(entry?.Variant ?? "OPENZ"),10,204,246,12,OzUi.Muted);
                if(entry!=null) Icon(card,OzConvertedMapsScreen.PoopSprite(),226,10,30);
                if(entry!=null && !solo && !entry.Balanced) Icon(card,WarningSprite(),10,10,28);
                if(entry?.SoloException==true && solo) Icon(card,WarningSprite(),10,10,28);
            }
            if(maps.Count==0) Label(grid,"No maps in this filter.",24,40,1000,22,OzUi.Muted);
            var chosen=Definition(selected);
            if(chosen!=null)
            {
                var entry=OzShittyMapCatalog.Find(selected);
                Label(panel,entry?.Title ?? chosen.Id+" · "+chosen.Name,1180,300,550,25,OzUi.Ink);
                var mini=Hud.Node("Selected minimap",panel).gameObject.AddComponent<RawImage>(); mini.texture=Preview(selected); mini.raycastTarget=false;
                float ratio=mini.texture ? (float)mini.texture.width/mini.texture.height : 1; float w=Mathf.Min(540,380*ratio),h=w/ratio; mini.rectTransform.TL(1180+(540-w)/2,355+(380-h)/2,w,h);
                Label(panel,(entry?.Group ?? "OPENZ")+" · "+World(selected).ToUpperInvariant(),1180,755,550,17,OzUi.Amber);
                Label(panel,entry==null ? "Original OpenZ scene" : entry.File,1180,791,550,14,OzUi.Muted);
                string note=entry?.SoloException==true && solo ? "FIXED SOLO EXCEPTION · original file unavailable" : entry!=null && !solo && !entry.Balanced ? "WARNING · no FIXED / FDL balancing variant available" : entry?.Balanced==true ? "FIXED · user-balanced map variant" : "Original source layout and starting armies";
                Label(panel,note,1180,824,550,14,OzUi.Amber);
                Button(panel,solo ? "DEPLOY" : "APPLY MAP",1180,875,550,()=>{var callback=apply;Close();callback(chosen);},true,editable);
            }
            else Label(panel,"Select a map to inspect its layout.",1180,355,550,22,OzUi.Muted);
            Label(panel,"Source layout preview · lobby army rules apply in 1v1",24,902,1120,14,OzUi.Muted);
        }
        static string World(string id) => OzShittyMapCatalog.Find(id)?.World ?? (id=="L07" ? "volcanic" : id=="L16" ? "jungle" : id=="L17" || id=="L19" ? "city" : "desert");
        Texture Preview(string id)
        {
            if(previews.TryGetValue(id,out var cached)) return cached;
            var texture=Resources.Load<Texture2D>("OpenZ/ShittyMaps/Previews/"+id);
            if(!texture) { texture=OzLevelInfo.Load(id).Mini; if(texture) generatedPreviews.Add(texture); }
            previews[id]=texture; return texture;
        }
        static void Label(Transform parent,string text,float x,float y,float width,float size,Color color) => Hud.Label(parent,text,text,size,color).At(x,y,OzH.L,false,width);
        static RectTransform Button(Transform parent,string text,float x,float y,float width,Action action,bool selected=false,bool enabled=true,float height=46)
        {
            var b=HudButton.Make(parent,text,text,new Vector2(width,height),HudButtonKind.Large,17,.04f,selected ? HudButtonStyle.Primary : HudButtonStyle.Normal);
            b.Rt.TL(x,y,width,height); b.LabelColor=OzUi.Ink; b.Interactable=enabled; b.Refresh(); b.Bg.raycastTarget=true;
            var button=b.Rt.gameObject.AddComponent<Button>(); button.targetGraphic=b.Bg; button.interactable=enabled; button.onClick.AddListener(()=>action()); return b.Rt;
        }
        static void Icon(Transform parent,Sprite sprite,float x,float y,float size) { var image=Hud.Img(parent,"Map pictogram",Color.white); image.sprite=sprite; image.rectTransform.TL(x,y,size,size); image.raycastTarget=false; }
        static Sprite WarningSprite()
        {
            if(warning) return warning;
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) { bool triangle=y>=2 && y<=29 && Mathf.Abs(x-15.5f)<(30-y)*.55f; bool mark=(x>=14 && x<=17 && ((y>=11 && y<=22)||(y>=6 && y<=8))); texture.SetPixel(x,y,triangle ? mark ? Color.black : new Color(1,.7f,.15f) : Color.clear); }
            texture.Apply(); warning=Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f)); return warning;
        }
        void Update() { if(Keyboard.current?.escapeKey.wasPressedThisFrame==true) Close(); }
        void Close() { IsOpen=false; var callback=closed; closed=null; gameObject.SetActive(false); Destroy(gameObject); callback?.Invoke(); }
        void OnDestroy() { IsOpen=false; foreach(var texture in generatedPreviews) if(texture) Destroy(texture); closed?.Invoke(); }
    }
}
