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
    public sealed class OzAiMapGallery : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        bool solo, editable;
        string selected, officialSelection, group, world = "all";
        int sort;
        int starts = 2;
        static readonly int[] MultiplayerCapacities = { 2, 3, 4, 8 };
        float scrollPosition=1;
        readonly Dictionary<string,Texture> previews=new Dictionary<string,Texture>();
        readonly List<Texture> generatedPreviews=new List<Texture>();
        RectTransform root;
        const float ViewportHeight = 570;
        const float RowHeight = 244;
        readonly Dictionary<string, OzLevelPreview> flybys = new Dictionary<string, OzLevelPreview>();
        readonly List<(string id, RawImage image, float top)> videoCards = new List<(string, RawImage, float)>();
        readonly HashSet<string> visibleFlybys = new HashSet<string>();
        readonly List<string> hiddenFlybys = new List<string>();
        RectTransform cardGrid;
        string selectedFlyby;
        RawImage flybyImage, flybyBackdrop;
        Action<OzLevelDef> apply;
        Action closed;
        OzAiMap[] entries;
        static Sprite warning;
        readonly List<GameObject> content = new List<GameObject>();
        public static void Open(bool solo, bool editable, string selected, Action<OzLevelDef> apply, Action closed = null, OzAiMap[] entries = null)
        {
            if (IsOpen) return;
            IsOpen = true;
            var go = new GameObject("AI map gallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var gallery = go.AddComponent<OzAiMapGallery>();
            var entry = OzAiMapCatalog.Find(selected);
            gallery.solo=solo; gallery.editable=editable; gallery.apply=apply; gallery.closed=closed;
            gallery.entries = entries;
            bool selectable = entries != null ? entries.Any(map => map.Id == selected) : entry != null && (solo ? entry.Solo : entry.Duel);
            gallery.selected = gallery.officialSelection = selectable ? selected : null;
            gallery.group = selectable && entry != null ? entry.Group : "Z ORIGINAL";
            if (entries?.Length > 0) gallery.starts = selectable ? entries.First(map => map.Id == selected).Starts : entries.Min(map => map.Starts);
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
        public static OzLevelDef Definition(OzAiMap map) => new OzLevelDef(map.Id,map.Scene,map.Experimental ? "EXP" : map.ForkOriginal ? "VK" : map.Number > 0 ? map.Number.ToString("00") : "MP",map.Name.ToUpperInvariant(),map.World.ToUpperInvariant()+" · "+(map.Experimental ? OzAiMap.ExperimentalGroup : "AI MAP"),"", "Capture the flags. Break into the enemy fort.","Capture the flags. Break into the enemy fort.",new[]{map.Group,map.World.ToUpperInvariant(),map.Variant});
        public static OzLevelDef Definition(string id) => OzAiMapCatalog.Find(id) is OzAiMap map ? Definition(map) : OzLevelDef.All.FirstOrDefault(d=>d.Id==id);
        void Build()
        {
            videoCards.Clear();
            selectedFlyby = null;
            flybyImage = flybyBackdrop = null;
            foreach(var go in content) { go.SetActive(false); Destroy(go); } content.Clear();
            var shade=Hud.Img(root,"Modal background",new Color(0,0,0,.94f)); shade.rectTransform.Fill(); shade.raycastTarget=true; content.Add(shade.gameObject);
            var panel=Hud.Node("Gallery",root); panel.TL(80,70,1760,940); content.Add(panel.gameObject);
            Hud.Img(panel,"Panel",OzUi.GM900).rectTransform.Fill();
            var wordmark=Hud.Kit(panel,"ForkWordmark","Wordmarks/wm_openz_vk_menu",new OzRect(24,8,160,48),null,Image.Type.Simple);
            wordmark.preserveAspect=true; wordmark.enabled=wordmark.sprite!=null;
            if(!wordmark.sprite) Label(panel,"OpenZ-VK",24,20,300,26,OzForkOptions.Brand);
            InlineTitle(panel,"Gallery title",solo ? "AI MAPS · SOLO" : entries?.FirstOrDefault()?.IsOfficial == true ? "OPENZ MAPS" : "AI MAPS · MULTIPLAYER",24,65,1400,44,30);
            Button(panel,"CLOSE",1510,20,220,Close);
            var groups = solo
                ? new[] { "Z ORIGINAL", "Z EXPANSION", "OPENZ-VK", OzAiMap.ExperimentalGroup }
                : new[] { "Z ORIGINAL", "Z EXPANSION", "MULTIPLAYER", "OPENZ", OzAiMap.ExperimentalGroup };
            float tabStep = Mathf.Min(420, 1726f / groups.Length);
            if (entries == null && (solo || starts == 2))
                for(int i=0;i<groups.Length;i++) { string value=groups[i]; Button(panel,value,24+i*tabStep,118,tabStep-20,()=>{group=value;scrollPosition=1;Build();},group==value); }
            else
                Label(panel, $"{starts} STARTING POSITIONS", 24, 132, 1100, 17, OzUi.Muted);
            var worlds=new[]{"all","desert","volcanic","arctic","jungle","city"};
            for(int i=0;i<worlds.Length;i++) { string value=worlds[i]; Button(panel,value.ToUpperInvariant(),24+i*222,180,210,()=>{world=value;scrollPosition=1;Build();},world==value); }
            Button(panel,"SORT: "+new[]{"NUMBER","NAME","WORLD"}[sort],1380,180,350,()=>{sort=(sort+1)%3;scrollPosition=1;Build();});
            if (solo) Label(panel,"Solo campaigns and OpenZ-VK maps.",24,240,1700,16,OzUi.Muted);
            var capacities = entries?.Select(map => map.Starts).Distinct().OrderBy(size => size).ToArray() ?? MultiplayerCapacities;
            if (!solo) for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                Button(panel, $"{capacity} PLAYERS", 24 + i * 205, 234, 193, () =>
                {
                    if (starts == capacity) return;
                    starts = capacity;
                    selected = capacity == 2 ? officialSelection : null;
                    scrollPosition = 1;
                    Build();
                }, starts == capacity, height: 34);
            }
            var maps = entries != null ? OzAiMapCatalog.Sort(entries.Where(map => map.Starts == starts && (world == "all" || map.World == world)), sort)
                .Select(map => map.IsOfficial ? Definition(map.Id) : Definition(map)).ToArray()
                : group == "OPENZ" && starts == 2 ? OzLevelDef.All.Where(d => world == "all" || World(d.Id) == world).ToArray()
                    : (solo ? OzAiMapCatalog.Select(true, group, world, sort) : OzAiMapCatalog.SelectMultiplayer(starts, group, world, sort)).Select(Definition).ToArray();
            Label(panel,maps.Length+" MAPS",24,278,800,15,OzUi.Muted);
            var viewport=Hud.Node("Map viewport",panel); viewport.TL(24,310,1120,ViewportHeight); viewport.gameObject.AddComponent<RectMask2D>();
            var background=viewport.gameObject.AddComponent<Image>(); background.color=OzUi.GM950;
            var grid=Hud.Node("Cards",viewport); grid.TL(0,0,1120,Mathf.Max(ViewportHeight,Mathf.Ceil(maps.Length/4f)*RowHeight));
            cardGrid = grid;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=grid; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=40; scroll.verticalNormalizedPosition=scrollPosition; scroll.onValueChanged.AddListener(p=>scrollPosition=p.y);
            for(int i=0;i<maps.Length;i++)
            {
                var map=maps[i]; var entry=OzAiMapCatalog.Find(map.Id); bool active=selected==map.Id;
                float top = i / 4 * RowHeight;
                var card=Button(grid,"",i%4*280,top,266,()=>{selected=map.Id;Build();},active,true,230);
                card.name = "MAP " + map.Id;
                var mini=Hud.Node("Minimap",card).gameObject.AddComponent<RawImage>(); mini.texture=Preview(map.Id); float ratio=mini.texture ? (float)mini.texture.width/mini.texture.height : 1; float width=Mathf.Min(250,155*ratio),height=width/ratio; mini.rectTransform.TL(8+(250-width)/2,8+(155-height)/2,width,height); mini.raycastTarget=false;
                if(mini.texture) mini.uvRect=new Rect(0,0,1,1);
                if (entry != null && entry.Starts == 2)
                {
                    var video = Hud.Node("Preview video", card).gameObject.AddComponent<RawImage>();
                    video.rectTransform.TL(8, 8, 250, 155);
                    video.raycastTarget = false;
                    video.enabled = false;
                    videoCards.Add((map.Id, video, top + 8));
                }
                InlineTitle(card,"Map title",entry?.Title ?? map.Id+" · "+map.Name,10,166,246,32,17);
                Label(card, World(map.Id).ToUpperInvariant()+" · "+(entry?.Starts > 2 ? entry.Starts + " PLAYERS" : entry?.Variant ?? "OPENZ"),10,204,246,12,OzUi.Muted);
                if(entry!=null && !solo && entry.Starts == 2 && !entry.Balanced) Icon(card,WarningSprite(),10,10,28);
            }
            if(maps.Length==0) Label(grid,"No maps in this filter.",24,40,1000,22,OzUi.Muted);
            var chosen=maps.FirstOrDefault(map => map.Id == selected);
            if(chosen!=null)
            {
                var entry=OzAiMapCatalog.Find(selected);
                bool previewOnly = entries == null && entry?.Starts > 2;
                if (!previewOnly) selectedFlyby = chosen.Id;
                if (entry != null && !previewOnly)
                {
                    flybyBackdrop = Hud.Node("Selected map background", panel).gameObject.AddComponent<RawImage>();
                    flybyBackdrop.rectTransform.TL(1160, 278, 588, 582);
                    flybyBackdrop.color = new Color(1, 1, 1, .2f);
                    flybyBackdrop.raycastTarget = false;
                    flybyBackdrop.enabled = false;
                }
                InlineTitle(panel,"Selected map title",entry?.Title ?? chosen.Id+" · "+chosen.Name,1180,292,550,42,25);
                var mini=Hud.Node("Selected minimap",panel).gameObject.AddComponent<RawImage>(); mini.texture=Preview(selected); mini.raycastTarget=false;
                float ratio = mini.texture ? (float)mini.texture.width / mini.texture.height : 1;
                float w = Mathf.Min(540, 287 * ratio), h = w / ratio;
                mini.rectTransform.TL(1180 + (540 - w) / 2, 344 + (287 - h) / 2, w, h);
                if (previewOnly)
                {
                    OzAiMapBadge.Stamp(mini.rectTransform, w);
                    Label(panel, $"{entry.Starts} PLAYERS · {entry.World.ToUpperInvariant()}", 1180, 670, 550, 18, OzUi.Amber);
                    Label(panel, "Tactical preview. This lobby supports two players.", 1180, 723, 550, 18, OzUi.Ink);
                }
                else
                {
                    var window = Hud.Node("Map video", panel);
                    window.TL(1314, 642, 272, 153);
                    flybyImage = window.gameObject.AddComponent<RawImage>();
                    flybyImage.raycastTarget = false;
                    flybyImage.texture = Texture2D.blackTexture;
                    if (entry != null) OzAiMapBadge.Stamp(window, 272);
                    Label(panel, (entry?.Group ?? "OPENZ") + " · " + World(selected).ToUpperInvariant(), 1180, 805, 550, 14, OzUi.Amber);
                }
                Button(panel, solo ? "DEPLOY" : "APPLY MAP", 1180, 875, 550, () => Deploy(chosen, apply), true, editable && !previewOnly);
            }
            else Label(panel,"Select a map to inspect its layout.",1180,355,550,22,OzUi.Muted);
            Label(panel, entries != null ? editable ? "Select a map, then apply it to the lobby." : "READ ONLY · browse the maps"
                : !solo && starts > 2 ? "Source layout preview · more than two starting positions"
                : !solo && !editable ? "READ ONLY · browse the converted maps"
                : "Source layout preview · lobby army rules apply in 1 vs 1",24,902,1120,14,OzUi.Muted);
        }
        void Deploy(OzLevelDef map, Action<OzLevelDef> deploy)
        {
            Close();
            deploy(map);
        }
        static string World(string id) => OzAiMapCatalog.FindSkirmishMap(id)?.World ?? (id == "L19" ? "city" : "desert");
        static void InlineTitle(Transform parent,string name,string text,float x,float y,float width,float height,float fontSize)
        {
            var row=Hud.Node(name,parent);row.TL(x,y,width,height);
            var label=Hud.Label(row,"Title",text,fontSize,OzUi.Ink);label.FillParent(OzH.L,0,0,0,0);
            if(label.graphic is TMPro.TMP_Text fitted){fitted.enableAutoSizing=true;fitted.fontSizeMin=fontSize-2;fitted.fontSizeMax=fontSize;fitted.overflowMode=TMPro.TextOverflowModes.Ellipsis;fitted.enableWordWrapping=false;}
        }
        Texture Preview(string id)
        {
            if(previews.TryGetValue(id,out var cached)) return cached;
            var texture=Resources.Load<Texture2D>("OpenZ/AiMaps/Previews/"+id);
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
        void Update()
        {
            visibleFlybys.Clear();
            if (selectedFlyby != null)
            {
                var selectedPreview = ShowFlyby(selectedFlyby);
                var frame = selectedPreview.Sharp;
                SetVideo(flybyImage, frame ? frame : Texture2D.blackTexture);
                if (flybyBackdrop) SetVideo(flybyBackdrop, frame);
            }
            float scrollTop = cardGrid.anchoredPosition.y;
            foreach (var card in videoCards)
            {
                bool visible = card.top < scrollTop + ViewportHeight && card.top + card.image.rectTransform.rect.height > scrollTop;
                Texture frame = null;
                if (visible)
                {
                    var preview = ShowFlyby(card.id);
                    if (preview.StallGuard() && preview.ShowingVideo) frame = preview.Frame;
                }
                SetVideo(card.image, frame);
            }
            hiddenFlybys.Clear();
            foreach (var pair in flybys)
                if (!visibleFlybys.Contains(pair.Key))
                {
                    pair.Value.Release();
                    hiddenFlybys.Add(pair.Key);
                }
            foreach (var id in hiddenFlybys) flybys.Remove(id);
            OzLevelPreview.PumpPrepares();
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        OzLevelPreview ShowFlyby(string id)
        {
            visibleFlybys.Add(id);
            if (flybys.TryGetValue(id, out var preview)) return preview;
            preview = OzLevelPreview.Create(transform, id);
            preview.PlayFrom(0, true);
            flybys.Add(id, preview);
            return preview;
        }

        static void SetVideo(RawImage image, Texture frame)
        {
            image.enabled = frame;
            image.texture = frame;
            if (frame) image.uvRect = OzLevelPreview.Cover((float)frame.width / frame.height,
                image.rectTransform.rect.width / image.rectTransform.rect.height);
        }
        void Close() { IsOpen=false; var callback=closed; closed=null; gameObject.SetActive(false); Destroy(gameObject); callback?.Invoke(); }
        void OnDestroy() { foreach (var preview in flybys.Values) preview.Release(); IsOpen=false; foreach(var texture in generatedPreviews) if(texture) Destroy(texture); closed?.Invoke(); }
    }
}
