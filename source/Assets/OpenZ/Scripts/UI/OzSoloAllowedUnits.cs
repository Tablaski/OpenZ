using System;
using System.Linq;
using OpenZ.Multiplayer;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OpenZ
{
    public sealed class OzSoloAllowedUnits : MonoBehaviour
    {
        public const string Label="ALLOWED UNITS",Preference="oz_solo_allowed_units_v1";
        [Serializable] sealed class Selection { public int schema,preset;public string[] allowed;public bool excludeMammoth,excludeBizon; }
        static OzSoloAllowedUnits instance;static int closedFrame=-1;
        public static bool CapturesInput=>instance||closedFrame==Time.frameCount;
        DuelRules rules;Action changed;RectTransform sheet;float scroll=1;
        public static DuelRules Read()
        {
            var rules=new DuelRules();string json=PlayerPrefs.GetString(Preference,"");if(json.Length==0)return rules;
            try{
                var saved=JsonUtility.FromJson<Selection>(json);
                if(saved==null||!Enum.IsDefined(typeof(DuelUnitPreset),saved.preset)||saved.allowed==null||saved.allowed.Length>DuelUnitRoster.All.Length||saved.allowed.Distinct().Count()!=saved.allowed.Length||saved.allowed.Any(t=>!DuelUnitRoster.Known(t)))return rules;
                rules.SetUnitPreset((DuelUnitPreset)saved.preset);
                if(rules.UnitPreset==DuelUnitPreset.Custom)
                {
                    foreach(string type in DuelUnitRoster.All)rules.SetUnitAllowed(type,Array.IndexOf(saved.allowed,type)>=0);
                    // Older custom subsets implicitly excluded both map-only tanks.
                    bool legacyExclusion = saved.schema == 0 && saved.allowed.Length != DuelUnitRoster.All.Length;
                    rules.SetUnitAllowed(ZBigTanks.Mammoth,!legacyExclusion&&!saved.excludeMammoth);
                    rules.SetUnitAllowed(ZBigTanks.Bizon,!legacyExclusion&&!saved.excludeBizon);
                }
                rules.Validate();return rules;
            }catch(Exception){return new DuelRules();}
        }
        public static void Apply(ZGame game)
        {
            if(game.World==null||DuelRuntime.Active||OzUnitEditor.IsPlayground(game.gameObject.scene))return;
            var rules=Read();
            game.World.ConfigureSoloUnits(rules.AllowedUnits,!rules.Allows(ZBigTanks.Mammoth),!rules.Allows(ZBigTanks.Bizon));
        }
        public static void Show(Action changed=null)
        {
            if(instance||DuelRuntime.Active)return;
            var go=new GameObject("Solo allowed units",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)){layer=5};
            instance=go.AddComponent<OzSoloAllowedUnits>();instance.rules=Read();instance.changed=changed;
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1150;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
            if(!EventSystem.current){var input=new GameObject("Solo unit input",typeof(EventSystem),typeof(InputSystemUIInputModule));input.transform.SetParent(go.transform,false);input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            instance.Build();
        }
        void Save()
        {
            Store(rules);Build();
        }
        internal static void Store(DuelRules rules)
        {
            rules.Validate();
            PlayerPrefs.SetString(Preference,JsonUtility.ToJson(new Selection
            {
                schema=1,preset=(int)rules.UnitPreset,allowed=rules.AllowedUnits.ToArray(),
                excludeMammoth=rules.ExcludeMammoth,excludeBizon=rules.ExcludeBizon
            }));
            PlayerPrefs.Save();
        }
        void Build()
        {
            if(sheet){sheet.gameObject.SetActive(false);Destroy(sheet.gameObject);}
            sheet=Hud.Node("Sheet",transform);sheet.Fill();var shade=Hud.Img(sheet,"Shade",new Color(0,0,0,.94f));shade.rectTransform.Fill();shade.raycastTarget=true;
            var panel=Hud.Node("Solo unit selection",sheet);panel.TL(220,155,1480,790);var bg=Hud.Img(panel,"Background",OzUi.GM950);bg.rectTransform.Fill();bg.raycastTarget=true;Hud.Plate(panel,"lg",0,OzForkOptions.Accent);
            Hud.Label(panel,"Title","ALLOWED UNITS · SOLO",25,OzForkOptions.Accent).At(24,20,OzH.L,false,1150);
            Hud.Label(panel,"Help","NEXT SOLO MISSION · applies to you and the computer. Multiplayer settings stay separate.",18,OzUi.Muted).At(24,58,OzH.L,false,1390);
            var close=HudButton.Make(panel,"CLOSE","CLOSE",new Vector2(190,46),HudButtonKind.Large,17);
            close.Rt.TL(1260,12,190,46);close.Bg.raycastTarget=true;var button=close.Rt.gameObject.AddComponent<Button>();button.targetGraphic=close.Bg;button.onClick.AddListener(Dismiss);
            OzAllowedUnitsPanel.Build(panel,rules,true,Save,scroll,p=>scroll=p,true);
        }
        public static void Dismiss()
        {
            if(!instance)return;var old=instance;instance=null;closedFrame=Time.frameCount;old.gameObject.SetActive(false);Destroy(old.gameObject);old.changed?.Invoke();
        }
        void Update(){if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)Dismiss();}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
