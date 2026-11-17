using System;
using OpenZ.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>The same unit catalogue and controls for solo selection and lobby rules.</summary>
    public static class OzAllowedUnitsPanel
    {
        public static void Build(RectTransform panel,DuelRules rules,bool editable,Action changed,float scrollPosition,Action<float> scrolled,bool solo)
        {
            int index=0;
            foreach(DuelUnitPreset preset in Enum.GetValues(typeof(DuelUnitPreset)))
            {
                var selected=preset;
                Button(panel,DuelUnitRoster.Name(preset),24+index++*340,92,320,46,()=>{rules.SetUnitPreset(selected);changed();},rules.UnitPreset==preset,editable,solo);
            }
            Label(panel,"Starting armies, neutral map units and production. A unit toggle selects Custom. Original tech requirements still apply.",24,146,1410,16,OzUi.Ink);
            Label(panel,rules.AllowedUnits.Count+"/"+DuelUnitRoster.All.Length+" allowed · Z 1996 excludes the additional medium tank, Electrobot, heroes and shuttle.",24,173,1410,14,OzUi.Muted);
            var viewport=Hud.Node("Allowed units viewport",panel);viewport.TL(24,205,1432,560);viewport.gameObject.AddComponent<RectMask2D>();
            var background=viewport.gameObject.AddComponent<Image>();background.color=OzUi.GM950;
            var content=Hud.Node("Allowed units",viewport);content.TL(0,0,1432,4*146);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
            scroll.verticalNormalizedPosition=scrollPosition;scroll.onValueChanged.AddListener(p=>scrolled(p.y));
            for(index=0;index<DuelUnitRoster.All.Length;index++)
            {
                string type=DuelUnitRoster.All[index];bool allowed=rules.Allows(type);
                var tile=Button(content,DuelMenus.UnitName(type),index%6*238,index/6*146,224,136,()=>{rules.SetUnitAllowed(type,!allowed);changed();},false,editable,solo);
                tile.name="ALLOWED UNIT "+type;DuelMenus.Portrait(tile,type,77,28,70);
                Label(tile,allowed?"ALLOWED":"EXCLUDED",10,8,204,13,allowed?(Color)OzUi.Go:OzUi.Muted);
            }
        }
        static void Label(Transform parent,string text,float x,float y,float width,float size,Color color)=>Hud.Label(parent,text,text,size,color).At(x,y,OzH.L,false,width);
        static RectTransform Button(Transform parent,string text,float x,float y,float width,float height,Action action,bool selected,bool editable,bool solo)
        {
            var kit=HudButton.Make(parent,text,text,new Vector2(width,height),HudButtonKind.Large,17,.08f,selected?HudButtonStyle.Primary:HudButtonStyle.Normal);
            kit.Rt.TL(x,y,width,height);kit.Interactable=editable;kit.LabelColor=solo?OzForkOptions.Accent:OzUi.Ink;kit.Refresh();kit.Bg.raycastTarget=true;
            if(kit.Label?.graphic is TMPro.TMP_Text fitted){fitted.enableAutoSizing=true;fitted.fontSizeMin=10;fitted.fontSizeMax=17;}
            if(height>70&&kit.Label!=null)kit.Label.rectTransform.TL(3,height-28,width-6,26);
            if(solo)OzForkOptions.Highlight(kit,OzSoloAllowedUnits.Label);
            var button=kit.Rt.gameObject.AddComponent<Button>();button.targetGraphic=kit.Bg;button.interactable=editable;button.onClick.AddListener(()=>action());
            return kit.Rt;
        }
    }
}
