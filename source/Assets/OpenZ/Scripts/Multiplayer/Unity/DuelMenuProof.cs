using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in native menu button and rendering checks. No effect in a normal game.</summary>
    public sealed class DuelMenuProof : MonoBehaviour
    {
        string folder;
        int step, preparation;
        double next;
        string pendingMap;
        int mapPickerStep;
        bool hadPlayerName;
        string savedPlayerName;
        string savedMicrophone;
        bool hadMicrophone;
        bool hadEdgeScroll;
        int savedEdgeScroll;
        string savedSoloUnits;bool hadSoloUnits;
        void Awake()
        {
            hadPlayerName = PlayerPrefs.HasKey("oz_duel_name");
            savedPlayerName = PlayerPrefs.GetString("oz_duel_name", "");
            hadMicrophone = PlayerPrefs.HasKey("oz_duel_mic_device"); savedMicrophone = PlayerPrefs.GetString("oz_duel_mic_device", "");
            hadEdgeScroll = PlayerPrefs.HasKey("oz_edge_scroll"); savedEdgeScroll = PlayerPrefs.GetInt("oz_edge_scroll", 1);
            hadSoloUnits=PlayerPrefs.HasKey(OzSoloAllowedUnits.Preference);savedSoloUnits=PlayerPrefs.GetString(OzSoloAllowedUnits.Preference,"");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-oz-duel-menu-proof");
            if (option < 0 || option + 1 >= args.Length) return;
            var proof = new GameObject("DuelMenuProof").AddComponent<DuelMenuProof>();
            proof.folder = args[option + 1]; proof.next = Time.realtimeSinceStartupAsDouble + 3;
        }
        void Click(string name)
        {
            var button = DuelRuntime.I.Menus.GetComponentsInChildren<Button>().Single(b => b.gameObject.activeInHierarchy && b.gameObject.name == name && b.interactable);
            button.onClick.Invoke();
        }
        void Capture(string name)
        {
            // Hidden test windows do not drive their offscreen portrait cameras.
            var generals = OzFactionPosters.I;
            if (generals && generals.Ready)
                foreach (var poster in generals.P)
                    if (poster.cam && poster.rt)
                    {
                        RenderPipeline.SubmitRenderRequest(poster.cam, new UniversalRenderPipeline.SingleCameraRequest { destination = poster.rt });
                        var previous = RenderTexture.active;
                        var pixels = new Texture2D(poster.rt.width, poster.rt.height, TextureFormat.RGBA32, false);
                        try
                        {
                            RenderTexture.active = poster.rt;
                            pixels.ReadPixels(new Rect(0, 0, poster.rt.width, poster.rt.height), 0, 0); pixels.Apply();
                            if (!pixels.GetPixels32().Any(p => p.a > 32 && (p.r > 16 || p.g > 16 || p.b > 16)))
                                throw new Exception("General portrait is empty: " + poster.id);
                        }
                        finally { RenderTexture.active = previous; Destroy(pixels); }
                    }
            DuelProofCapture.Save(Path.Combine(folder, name + ".png"), true);
        }
        void ClickMainItem(string label)
        {
            var menu = FindFirstObjectByType<OpenZMainMenu>();
            var items = (IEnumerable)typeof(OpenZMainMenu).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            foreach (var item in items)
                if ((string)item.GetType().GetField("label").GetValue(item) == label)
                { ((Action)item.GetType().GetField("act").GetValue(item))(); return; }
            throw new Exception("Missing native main menu item: " + label);
        }
        void CheckPicker(bool fort)
        {
            var choices = DuelRuntime.I.Menus.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("SELECT ") &&
                (b.transform.parent.name == "TURRETS" || b.transform.parent.name == "ROBOTS" || b.transform.parent.name == "VEHICLES")).ToArray();
            string[] turrets = { "gun", "gatling", "howitzer", "missile_cannon" };
            string[] robots = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot" };
            string[] vehicles = { "jeep", "light", "medium", "mtank", "heavy", "missile_launcher", "crane", "apc" };
            var expected = fort ? turrets : robots.Concat(vehicles).ToArray();
            if (!choices.Select(b => b.name.Substring(7)).OrderBy(t => t).SequenceEqual(expected.OrderBy(t => t)) || choices.Any(b => !b.interactable))
                throw new Exception("Picker exposes incorrect unit types.");
            foreach (var choice in choices)
            {
                string type = choice.name.Substring(7);
                string category = fort ? "TURRETS" : robots.Contains(type) ? "ROBOTS" : "VEHICLES";
                if (choice.transform.parent.name != category) throw new Exception("Incorrect picker category: " + type);
                if (!choice.GetComponentsInChildren<RawImage>().Any(i => i.name == "Portrait" && i.texture)) throw new Exception("Missing picker portrait: " + type);
            }
            if (!fort)
            {
                var laser = choices.Single(b => b.name == "SELECT laser").GetComponentsInChildren<RawImage>().Single(i => i.name == "Portrait");
                if (laser.texture != Resources.Load<Texture2D>("OpenZ/UI/PortraitsBS01/portrait_laser_red"))
                    throw new Exception("Laser picker uses the obsolete portrait.");
            }
        }
        void Update()
        {
            if (step < 0 || Time.realtimeSinceStartupAsDouble < next) return;
            next = Time.realtimeSinceStartupAsDouble + 1;
            try
            {
                Directory.CreateDirectory(folder); var r = DuelRuntime.I;
                var menu = FindFirstObjectByType<OpenZMainMenu>();
                if (preparation < 21)
                {
                    next = Time.realtimeSinceStartupAsDouble + .6;
                    switch (preparation++)
                    {
                        case 0:
                            var expected=new OzResourcePackage.Identity{resourcePackage="test-package",commit="a",builtUtc="now"};
                            var package=new OzResourcePackage.Identity{version="test-package"};
                            var embedded=new OzResourcePackage.Identity{resourcePackage="test-package",commit="a",builtUtc="now"};
                            if(OzResourcePackage.Validate(expected,package,embedded)!=null)throw new Exception("Matching resource identity rejected.");
                            embedded.commit="b";
                            if(OzResourcePackage.Validate(expected,package,embedded)==null||OzResourcePackage.Validate(expected,null,embedded)==null||OzResourcePackage.Validate(null,package,embedded)==null)throw new Exception("Mismatched resource identity accepted.");
                            menu.ProofOpenOptions(OzSettingTips.TabGame);
                            if (!menu.ProofOptTabRows.Contains(OzForceBarOption.Label) || !menu.ProofOptTabRows.Contains(OzMapReadabilityOptions.FlagLabel) || !menu.ProofOptTabRows.Contains(OzMapReadabilityOptions.BorderLabel)) throw new Exception("Fork options missing.");
                            break;
                        case 1: DuelOptionProof.SetEdgeScroll(menu, true); break;
                        case 2:
                            DuelOptionProof.Check(menu); Capture("main-menu-options-on"); ScreenCapture.CaptureScreenshot(Path.Combine(folder,"main-menu-options-on-screen.png")); break;
                        case 3: DuelOptionProof.SetEdgeScroll(menu, false); break;
                        case 4:
                            DuelOptionProof.Check(menu); Capture("main-menu-options-off"); ScreenCapture.CaptureScreenshot(Path.Combine(folder,"main-menu-options-off-screen.png")); break;
                        case 5:
                            RestoreEdgeScroll(); menu.ProofOpen(""); break;
                        case 6:
                            Capture("main-menu"); menu.ProofSelect(); break;
                        case 7:
                            Capture("solo-selection");menu.ProofOpenSoloAllowed();break;
                        case 8:
                            ClickSoloUnits("Z 1996");break;
                        case 9:
                            if(OzSoloAllowedUnits.Read().UnitPreset!=DuelUnitPreset.Z1996||OzSoloAllowedUnits.Read().Allows("electrobot") ||
                                OzSoloAllowedUnits.Read().Allows("medium") || !new[] { "light", "mtank", "heavy" }.All(OzSoloAllowedUnits.Read().Allows))
                                throw new Exception("Solo Z 1996 roster did not include the three original tanks.");
                            Capture("solo-allowed-z1996");ClickSoloUnits("ALLOWED UNIT heavy");break;
                        case 10:
                            if(OzSoloAllowedUnits.Read().UnitPreset!=DuelUnitPreset.Custom||OzSoloAllowedUnits.Read().Allows("heavy"))throw new Exception("Solo custom selection not saved.");
                            Capture("solo-allowed-custom");ClickSoloUnits("OpenZ");break;
                        case 11:
                            if(OzSoloAllowedUnits.Read().UnitPreset!=DuelUnitPreset.OpenZ||OzSoloAllowedUnits.Read().AllowedUnits.Count!=22)throw new Exception("Solo OpenZ preset not restored.");
                            Capture("solo-allowed-openz");OzSoloAllowedUnits.Dismiss();RestoreSoloUnits();break;
                        case 12:
                            menu.ProofSwitchMapCatalog();
                            next = Time.realtimeSinceStartupAsDouble + 2.5;
                            break;
                        case 13:
                            if (!menu.ProofAiPreviewPlaying || menu.ProofCardCount != OzAiMapCatalog.Select(true).Count(m => !m.Experimental))
                                throw new Exception("The solo AI Maps carousel is missing its looping video or stamp.");
                            Capture("ai-maps-solo-full");
                            break;
                        case 14:
                            menu.ProofSwitchMapCatalog();
                            if (menu.ProofCardCount != OzLevelDef.All.Length)
                                throw new Exception("The official carousel did not return.");
                            break;
                        case 15:
                            menu.ProofOpenSoloDynamics(ZBehaviorFamily.Tanks); break;
                        case 16:
                            Capture("solo-tank-dynamics"); CheckSoloDynamics(ZBehaviorFamily.Tanks); break;
                        case 17:
                            menu.ProofOpenSoloDynamics(ZBehaviorFamily.Robots); break;
                        case 18:
                            Capture("solo-robot-dynamics"); CheckSoloDynamics(ZBehaviorFamily.Robots); menu.ProofBackFromSelect(); break;
                        case 19:
                            next = Time.realtimeSinceStartupAsDouble + 1.5; break;
                        case 20:
                            var logo = menu.ProofFactionRoot.Find("ForkWordmark")?.GetComponent<Image>();
                            if (!menu.ProofFactionShown || !logo || !logo.sprite) throw new Exception("OpenZ-VK faction logo is missing.");
                            Capture("solo-faction"); menu.ProofMain(); break;
                    }
                    return;
                }
                if (pendingMap != null)
                {
                    if (mapPickerStep++ == 0) Click("MAP " + pendingMap);
                    else
                    {
                        var flyby = r.Menus.GetComponentsInChildren<RawImage>().Single(i => i.name == "OpenZ flyby");
                        if (!flyby.texture) throw new Exception("Missing OpenZ preview for " + pendingMap);
                        Click("APPLY MAP");
                        if (r.Rules.Map != pendingMap) throw new Exception("Picker did not apply " + pendingMap);
                        pendingMap = null;
                    }
                    return;
                }
                switch (step++)
                {
                    case 0:
                        var mainMenu = FindFirstObjectByType<OpenZMainMenu>();
                        if (!FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name == "Item_PLAY SOLO")) throw new Exception("Missing PLAY SOLO entry.");
                        if (r.Menus.GetComponentsInChildren<Button>().Any(b => b.name == "1VS1")) throw new Exception("Old floating duel button is still present.");
                        Capture("main-menu"); ClickMainItem("PLAY 1 VS 1"); break;
                    case 1:
                        Capture("online-connection");
                        if (r.Menus.GetComponentsInChildren<Button>().Any(b => b.name == "ONLINE SETTINGS")) Click("ONLINE SETTINGS");
                        else step = 3;
                        break;
                    case 2:
                        Capture("online-settings"); Click("ADVANCED TCP/IP"); break;
                    case 3:
                        DuelRuntime.I.Menus.GetComponentsInChildren<InputField>().Single(i => i.characterLimit == 32).text = "HOST";
                        Capture("connection"); Click("CREATE MATCH"); break;
                    case 4:
                        if (r.Rules.HandicapEnabled) throw new Exception("Handicap enabled by default.");
                        if (!r.Menus.GetComponentsInChildren<RawImage>().Any(i => i.name == "Portrait" && i.texture)) throw new Exception("Missing native unit portraits.");
                        if (!r.Menus.GetComponentsInChildren<RawImage>().Any(i => i.name == "TacticalMap" && i.texture)) throw new Exception("Missing complete tactical map.");
                        if (!r.Rules.Common.FortTurrets.SequenceEqual(new[] { "howitzer", "missile_cannon", "", "" })) throw new Exception("Incorrect default fort cannons.");
                        if (r.Rules.PlanningSeconds != 0 || !r.Rules.Common.StartingUnits.SequenceEqual(new[] { "", "", "", "" }))
                            throw new Exception("Incorrect opening order or starting army defaults.");
                        var fortButton = r.Menus.GetComponentsInChildren<Button>().Single(b => b.name == "FORT PLACEMENT · RED / BLUE");
                        var fortLabel = fortButton.transform.Find("Content/Label") as RectTransform;
                        var fortRect = fortButton.GetComponent<RectTransform>();
                        if (!fortLabel || Mathf.Abs(fortLabel.TransformPoint(fortLabel.rect.center).y - fortRect.TransformPoint(fortRect.rect.center).y) > 2)
                            throw new Exception("Fort placement label is not vertically centered.");
                        Capture("shared-rules"); Click("FORT PLACEMENT · RED / BLUE"); break;
                    case 5:
                        if (r.Menus.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("FORT MOUNT ")) != 8)
                            throw new Exception("Fort popup must expose four mounts for each team.");
                        Capture("fort-unit-picker"); Click("FORT CANNON howitzer"); Click("CLOSE"); break;
                    case 6:
                        Click("ARMY SLOT 1:3"); break;
                    case 7: CheckPicker(false); Capture("outside-unit-picker"); Click("SELECT heavy"); break;
                    case 8:
                        if (r.Rules.Common.StartingUnits[3] != "heavy") throw new Exception("Shared outside slot was not set.");
                        Click("HANDICAP PLAYER 0"); break;
                    case 9:
                        if (!r.Rules.HandicapEnabled || r.Rules.HandicapSlot != 0 || r.Rules.Handicap.StartingUnits.Count != 0) throw new Exception("Red handicap did not start with empty slots.");
                        Capture("handicap-active"); Click("ARMY SLOT 2:0"); break;
                    case 10: CheckPicker(false); Capture("handicap-unit-picker"); Click("SELECT jeep"); break;
                    case 11:
                        if (r.Rules.Handicap.StartingUnits[0] != "jeep" || r.Rules.EffectiveFor(0).StartingUnits.Count != r.Rules.EffectiveFor(1).StartingUnits.Count + 1)
                            throw new Exception("Handicap units are not additive.");
                        Capture("handicap-unit-added"); Click("HANDICAP PLAYER 1"); break;
                    case 12:
                        if (r.Rules.HandicapSlot != 1 || r.Rules.Handicap.StartingUnits[0] != "jeep") throw new Exception("Switching the beneficiary lost the extras.");
                        Click("ARMY SLOT 1:3"); break;
                    case 13: Click("EMPTY SLOT"); break;
                    case 14:
                        if (r.Rules.Common.StartingUnits[3] != "" || r.Rules.EffectiveFor(1).StartingUnits.Count != r.Rules.EffectiveFor(0).StartingUnits.Count + 1)
                            throw new Exception("Shared slot edit did not propagate to the handicap army.");
                        SelectMap("L16"); break;
                    case 15: Capture("jungle-map"); SelectMap("L17"); break;
                    case 16: Capture("car-park-map"); SelectMap("L07"); break;
                    case 17: Capture("volcanic-map"); SelectMap("L02"); break;
                    case 18:
                        if (r.Rules.Common.StartingUnits[3] != "" || r.Rules.Handicap.StartingUnits[0] != "jeep") throw new Exception("Map selection reset the custom armies.");
                        Click("DISABLE"); break;
                    case 19:
                        if (r.Rules.HandicapEnabled) throw new Exception("Cannot disable handicap.");
                        if (r.Rules.TerritoryBonus != .5 || r.Rules.Common.BuildDuration != 1 || r.Rules.Handicap.BuildDuration != 1 ||
                            r.Rules.BuildSeconds(0, 100, .5, 1) != 75 || r.Rules.BuildSeconds(1, 100, .5, 1) != 75)
                            throw new Exception("Army configuration changed construction times.");
                        Capture("shared-rules-restored");
                        Click("SETUP"); break;
                    case 20:
                        if (!r.Voice.Monitoring || r.Voice.MicrophoneEnabled) throw new Exception("Microphone preview must meter locally while muted.");
                        var gain = r.Menus.GetComponentsInChildren<Slider>().Single();
                        if (gain.minValue != 0 || gain.maxValue != 4 || gain.value != r.Voice.Gain) throw new Exception("Incorrect microphone gain slider.");
                        Capture("microphone-settings");
                        Click("MICROPHONE: " + r.Voice.Device + "  ▼"); break;
                    case 21:
                        var choices = r.Menus.GetComponentsInChildren<ScrollRect>().Single().content.GetComponentsInChildren<Button>();
                        if (choices.Length != Microphone.devices.Length + 1 || !choices.Any(b => b.name == "System default") || Microphone.devices.Any(d => !choices.Any(b => b.name == d)))
                            throw new Exception("Microphone dropdown does not contain every input device.");
                        Capture("microphone-devices"); Click("System default"); break;
                    case 22: Click("BACK"); break;
                    case 23:
                        if (r.Voice.Monitoring || !r.Menus.GetComponentsInChildren<Button>().Any(b => b.name == "Toggle microphone")) throw new Exception("Lobby microphone controls were not restored.");
                        Click("AI MAPS"); break;
                    case 24:
                        CheckGallery(); Capture("ai-maps-duel"); ClickGallery("ARCTIC"); break;
                    case 25:
                        CheckGallery(); ClickGallery("MAP SM09");
                        next = Time.realtimeSinceStartupAsDouble + 2.5; break;
                    case 26:
                        CheckGallery();
                        var gallery = FindFirstObjectByType<OzAiMapGallery>();
                        var mini = gallery.GetComponentsInChildren<RawImage>().Single(i => i.name == "Selected minimap");
                        var video = gallery.GetComponentsInChildren<RawImage>().Single(i => i.name == "Map video");
                        var decoder = gallery.GetComponentInChildren<UnityEngine.Video.VideoPlayer>();
                        if (!decoder || !decoder.isLooping || !decoder.isPlaying || !(video.texture is RenderTexture)
                            || mini.rectTransform.rect.height <= video.rectTransform.rect.height)
                            throw new Exception("The duel AI Maps gallery must show a looping preview and a larger minimap.");
                        Capture("ai-maps-1v1-full");
                        break;
                    case 27:
                        ClickGallery("SORT: NUMBER"); ClickGallery("CITY"); CheckGallery(); Capture("ai-maps-city"); ClickGallery("CLOSE"); break;
                    case 28:
                        if (OzAiMapGallery.IsOpen) throw new Exception("Gallery did not close.");
                        OzAiMapGallery.Open(true, true, OzAiMapCatalog.All.First(m => m.SoloException).Id, _ => { }); break;
                    case 29:
                        CheckGallery(); Capture("ai-maps-solo"); ClickGallery("Z EXPANSION"); break;
                    case 30:
                        CheckGallery(); Capture("ai-maps-expansion"); ClickGallery("CLOSE"); break;
                    case 31:
                        Click("Allowed units · "+DuelUnitRoster.Name(r.Rules.UnitPreset));break;
                    case 32:
                        if(r.Menus.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("ALLOWED UNIT "))!=22)throw new Exception("Lobby unit catalogue changed.");
                        Capture("lobby-allowed-openz");Click("Z 1996");break;
                    case 33:
                        if(r.Rules.UnitPreset!=DuelUnitPreset.Z1996||r.Rules.Allows("electrobot"))throw new Exception("Shared lobby preset does not apply.");
                        Capture("lobby-allowed-z1996");Click("OpenZ");break;
                    case 34:Click("CLOSE");break;
                    case 35:
                        File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: native main menu, four maps, independent fort popup, army pickers, additive handicap, unchanged construction times, microphone controls and dropdown; solo/duel converted-map galleries, Arctic/City filters, sorting, expansion tab and closing.");
                        step = -1; r.Close(); StartCoroutine(Exit(0)); break;
                }
            }
            catch (Exception error)
            { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: " + error); step = -1; StartCoroutine(Exit(1)); }
        }
        void ClickGallery(string name)
        {
            var gallery = FindFirstObjectByType<OzAiMapGallery>();
            gallery.GetComponentsInChildren<Button>().Single(b => b.name == name && b.interactable).onClick.Invoke();
        }
        void CheckSoloDynamics(ZBehaviorFamily family)
        {
            string key = OzBehaviorLibrary.SelectedKey(family);
            string previous = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            try
            {
                var picker = FindFirstObjectByType<OzBehaviorOptions>();
                var choices = picker.GetComponentsInChildren<Button>();
                choices.Single(button => button.name == "PROFILE native");
                choices.Single(button => button.name == "PROFILE zod-classic").onClick.Invoke();
                if (OzBehaviorLibrary.SelectedId(family) != "zod-classic")
                    throw new Exception("Z Classic did not apply to " + family);
            }
            finally
            {
                if (previous == null) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetString(key, previous);
                PlayerPrefs.Save();
            }
        }

        void ClickSoloUnits(string name)
        {
            var panel=FindFirstObjectByType<OzSoloAllowedUnits>();
            if(!panel||panel.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("ALLOWED UNIT "))!=22)throw new Exception("Solo unit catalogue is incomplete.");
            var viewport=panel.GetComponentInChildren<RectMask2D>().GetComponent<RectTransform>();
            foreach(var tile in panel.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("ALLOWED UNIT "))){var rect=tile.GetComponent<RectTransform>();var bottom=viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.min));if(bottom.y<viewport.rect.yMin-1)throw new Exception("Solo unit row is clipped.");}
            panel.GetComponentsInChildren<Button>().Single(b=>b.name==name&&b.interactable).onClick.Invoke();
        }
        void SelectMap(string id)
        {
            pendingMap = id;
            mapPickerStep = 0;
            Click("OPENZ MAPS");
        }
        void CheckGallery()
        {
            var gallery = FindFirstObjectByType<OzAiMapGallery>();
            if (!gallery || !OzAiMapGallery.IsOpen || gallery.GetComponent<Canvas>().sortingOrder <= 1000)
                throw new Exception("Map gallery is not above the lobby.");
            var images = gallery.GetComponentsInChildren<RawImage>();
            if (!images.Any(i => i.name == "Minimap") || images.Any(i => !i.texture))
                throw new Exception("Gallery contains missing minimaps.");
            var heading=gallery.transform.Find("Gallery/Gallery title");
            if (!heading || !heading.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("AI MAPS"))
                throw new Exception("Gallery is missing the AI Maps name.");
        }
        void RestoreSoloUnits(){if(hadSoloUnits)PlayerPrefs.SetString(OzSoloAllowedUnits.Preference,savedSoloUnits);else PlayerPrefs.DeleteKey(OzSoloAllowedUnits.Preference);PlayerPrefs.Save();}
        void RestoreEdgeScroll(){if(hadEdgeScroll)PlayerPrefs.SetInt("oz_edge_scroll",savedEdgeScroll);else PlayerPrefs.DeleteKey("oz_edge_scroll");PlayerPrefs.Save();}
        IEnumerator Exit(int code)
        {
            yield return null;
            if (hadPlayerName) PlayerPrefs.SetString("oz_duel_name", savedPlayerName);
            else PlayerPrefs.DeleteKey("oz_duel_name");
            if (hadMicrophone) PlayerPrefs.SetString("oz_duel_mic_device", savedMicrophone); else PlayerPrefs.DeleteKey("oz_duel_mic_device");
            RestoreEdgeScroll();
            RestoreSoloUnits();
            PlayerPrefs.Save();
            Application.Quit(code);
        }
    }
    internal static class DuelOptionProof
    {
        static IEnumerable Rows(object menu) => (IEnumerable)menu.GetType().GetField("toggles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
        internal static void SetEdgeScroll(object menu,bool desired)
        {
            foreach(var item in Rows(menu))if((string)item.GetType().GetField("label").GetValue(item)=="EDGE SCROLL")
            {
                var get=(Func<bool>)item.GetType().GetField("get").GetValue(item);
                if(get()!=desired)
                {
                    var row=(HudButton)item.GetType().GetField("row").GetValue(item);
                    if(menu is OpenZMainMenu main)main.ProofClickAt(RectTransformUtility.WorldToScreenPoint(null,row.Rt.TransformPoint(row.Rt.rect.center)));
                    else row.OnClick(new HudClick());
                    if(get()!=desired)throw new Exception("Native edge-scroll click did not change its setting.");
                }
                return;
            }
            throw new Exception("Edge-scroll toggle missing.");
        }
        internal static void Check(object menu)
        {
            int visible=0;
            foreach(var item in Rows(menu))
            {
                var knob=item.GetType().GetField("knob").GetValue(item) as Image;var row=(HudButton)item.GetType().GetField("row").GetValue(item);
                if(!knob||!row.Rt.gameObject.activeInHierarchy)continue;visible++;
                var point=row.Rt.InverseTransformPoint(knob.rectTransform.TransformPoint(knob.rectTransform.rect.center));
                if(point.x < row.Rt.rect.xMin+row.Rt.rect.width*.75f || point.x>row.Rt.rect.xMax)throw new Exception("Toggle thumb is outside the right-hand control: "+item.GetType().GetField("label").GetValue(item));
            }
            if(visible==0)throw new Exception("No visible boolean options were checked.");
        }
    }
}
