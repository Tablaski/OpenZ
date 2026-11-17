using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        int step;
        double next;
        bool hadPlayerName;
        string savedPlayerName;
        string savedMicrophone;
        bool hadMicrophone;
        void Awake()
        {
            hadPlayerName = PlayerPrefs.HasKey("oz_duel_name");
            savedPlayerName = PlayerPrefs.GetString("oz_duel_name", "");
            hadMicrophone = PlayerPrefs.HasKey("oz_duel_mic_device"); savedMicrophone = PlayerPrefs.GetString("oz_duel_mic_device", "");
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
            var choices = DuelRuntime.I.Menus.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("SELECT ")).ToArray();
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
                switch (step++)
                {
                    case 0:
                        var mainMenu = FindFirstObjectByType<OpenZMainMenu>();
                        mainMenu.ProofOpenOptions(OzSettingTips.TabGame);
                        if (!mainMenu.ProofOptTabRows.Contains(OzForceBarOption.Label)) throw new Exception("Force bar option missing from main menu GAME options.");
                        if (!mainMenu.ProofOptTabRows.Contains(OzMapReadabilityOptions.FlagLabel) || !mainMenu.ProofOptTabRows.Contains(OzMapReadabilityOptions.BorderLabel))
                            throw new Exception("Live flag-size or territory-border option is absent from main menu GAME options.");
                        mainMenu.ProofOpen("");
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
                        Click("GENERAL ZOD · RED"); break;
                    case 9:
                        if (!r.Rules.HandicapEnabled || r.Rules.HandicapSlot != 0 || r.Rules.Handicap.StartingUnits.Count != 0) throw new Exception("Red handicap did not start with empty slots.");
                        Capture("handicap-active"); Click("ARMY SLOT 2:0"); break;
                    case 10: CheckPicker(false); Capture("handicap-unit-picker"); Click("SELECT jeep"); break;
                    case 11:
                        if (r.Rules.Handicap.StartingUnits[0] != "jeep" || r.Rules.EffectiveFor(0).StartingUnits.Count != r.Rules.EffectiveFor(1).StartingUnits.Count + 1)
                            throw new Exception("Handicap units are not additive.");
                        Capture("handicap-unit-added"); Click("EVIL ZOD · BLUE"); break;
                    case 12:
                        if (r.Rules.HandicapSlot != 1 || r.Rules.Handicap.StartingUnits[0] != "jeep") throw new Exception("Switching the beneficiary lost the extras.");
                        Click("ARMY SLOT 1:3"); break;
                    case 13: Click("EMPTY SLOT"); break;
                    case 14:
                        if (r.Rules.Common.StartingUnits[3] != "" || r.Rules.EffectiveFor(1).StartingUnits.Count != r.Rules.EffectiveFor(0).StartingUnits.Count + 1)
                            throw new Exception("Shared slot edit did not propagate to the handicap army.");
                        Click("JUNGLE"); break;
                    case 15: Capture("jungle-map"); Click("CAR PARK"); break;
                    case 16: Capture("car-park-map"); Click("VOLCANO"); break;
                    case 17: Capture("volcanic-map"); Click("DESERT"); break;
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
                        File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: native main menu, four maps, army pickers, additive handicap, unchanged construction times, lobby microphone controls, gain slider, local meter and complete microphone dropdown.");
                        step = -1; r.Close(); StartCoroutine(Exit(0)); break;
                }
            }
            catch (Exception error)
            { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: " + error); step = -1; StartCoroutine(Exit(1)); }
        }
        IEnumerator Exit(int code)
        {
            yield return null;
            if (hadPlayerName) PlayerPrefs.SetString("oz_duel_name", savedPlayerName);
            else PlayerPrefs.DeleteKey("oz_duel_name");
            if (hadMicrophone) PlayerPrefs.SetString("oz_duel_mic_device", savedMicrophone); else PlayerPrefs.DeleteKey("oz_duel_mic_device");
            PlayerPrefs.Save();
            Application.Quit(code);
        }
    }
}
