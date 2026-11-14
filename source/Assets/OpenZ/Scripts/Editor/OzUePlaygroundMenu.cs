using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor, View/OzUnitEditor.cs): the playground scene and its counted check, edit mode only.
    ///  1 Build Playground Scene: Assets/OpenZ/Scenes/UE_Playground.unity made as Editor/OpenZSetup.cs BuildLevel makes a level, without the
    ///    level's environment, skirt, sky, fog, day-night clock, atmosphere and map objects: the Sun (directional, soft shadows) in the
    ///    game's own midday day look (DayNight.Apply at the playground's time, the city's cool warmth: the map's planet is "city"), the
    ///    PostFX volume, the Main Camera (FOV 32, near 0.5, far 1500, HDR, URP post + SMAA, AudioListener, RtsCamera over the 320 x 200 m
    ///    field, start height 70 m, solid clear colour: the floor style sets it in Play), the ZGame object (level UE, the player red, no
    ///    CPU bots, the same 33 unit prefabs a level has, PlayerInput, SelectionOverlay, OzUnitEditor), the ZodObjects root and the flat
    ///    ground collider COL_Terrain (400 x 300 m, top at 0, no renderer; the floor is drawn in Play). Saved, added (enabled) to the build
    ///    settings after the levels; the hologram material's shader is set again if its reference is missing. Refused while playing or
    ///    while an open scene has unsaved changes. Running it again rebuilds the same scene.
    ///  2 Counted Check: the scene's objects and settings, the build settings line, the kit's hook lines in the game's scripts (each once),
    ///    the game's Discord link (the kit's line in Resources/OpenZ/Text/openz_links.json), then every module's SelfCheck
    ///    (OzUnitEditor.SelfCheck). "N PASS, M FAIL" in the console.
    /// </summary>
    public static class OzUePlaygroundMenu
    {
        public const string ScenePath = "Assets/OpenZ/Scenes/" + OzUnitEditor.SceneName + ".unity";
        public const string GridPath = "Assets/OpenZ/Resources/OpenZ/Maps/UE_grid.json";
        const string Root = "Assets/OpenZ";
        /// <summary>The playground's time of day for its one fixed light (OzUnitEditor.DayTime: just after DayNight's morning warmth fades, near noon).</summary>
        public const float MiddayT = OzUnitEditor.DayTime;
        /// <summary>DayNight warmth of the city (L17: cool grey light), the playground's planet.</summary>
        public const float CityWarmth = 0.35f;
        /// <summary>The invite code the kit writes under the key "discord" of Resources/OpenZ/Text/openz_links.json (1.2 item 30: the
        /// non-expiring invite to the OpenZ channel of the ZZone Discord; the main menu's OPENZ DISCORD and the share dialog's OPEN DISCORD).</summary>
        public const string DiscordInvite = "5r532Qs4HM";
        /// <summary>The unit prefabs a level's ZGame carries (Editor/OpenZSetup.cs BuildLevel, the same names).</summary>
        static readonly string[] PrefabNames =
        {
            "robot", "tough", "jeep", "light", "medium", "heavy", "apc", "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon", "grenades",
            "bridge_desert_intact", "bridge_desert_destroyed", "bridge_city_intact", "bridge_city_destroyed",
            "bridge_desert_damaged", "bridge_city_damaged", "bridge_desert_chunks", "bridge_city_chunks",
            "bridge_jungle_intact", "bridge_jungle_damaged", "bridge_jungle_destroyed", "bridge_jungle_chunks",
            "bs01_grunt", "bs01_psycho", "bs01_tough", "bs01_sniper", "bs01_pyro", "bs01_laser", "bs01_electrobot",
        };

        [MenuItem("OpenZ/V1.2/Unit Editor/1 Build Playground Scene")]
        public static void BuildMenu() => Build();

        [MenuItem("OpenZ/V1.2/Unit Editor/2 Counted Check")]
        public static void CheckMenu() => Check();

        static bool AnyDirtyScene()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
        }

        public static string Build()
        {
            const string T = "[OpenZ UnitEditor] build playground scene: ";
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(T + "refused while playing"); return "refused: playing"; }
            if (AnyDirtyScene()) { Debug.LogError(T + "refused: an open scene has unsaved changes (save or close it first)"); return "refused: unsaved scene"; }
            if (!AssetDatabase.LoadAssetAtPath<TextAsset>(GridPath)) { Debug.LogError(T + "the playground map " + GridPath + " is missing (apply the kit first)"); return "refused: no grid"; }
            var log = new StringBuilder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // the sun: the game's own day look at midday (DayNight's keys and its city warmth), fixed: no clock in the playground
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            sunGo.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = sun; RenderSettings.skybox = null;
            var look = new GameObject("DayLook"); var dn = look.AddComponent<DayNight>();
            dn.sun = sun; dn.warmth = CityWarmth; dn.Apply(MiddayT);
            UnityEngine.Object.DestroyImmediate(look);
            RenderSettings.fog = false;
            log.AppendLine($"sun: rotation {sun.transform.eulerAngles}, colour {sun.color}, intensity {sun.intensity:0.00}, shadow strength {sun.shadowStrength:0.00}; ambient trilight sky {RenderSettings.ambientSkyColor}, equator {RenderSettings.ambientEquatorColor}, ground {RenderSettings.ambientGroundColor}; fog off, no sky");

            var vol = new GameObject("PostFX").AddComponent<Volume>(); vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Root}/Settings/OpenZ_PostFX.asset");
            log.AppendLine("post: " + (vol.sharedProfile ? vol.sharedProfile.name : "PROFILE MISSING"));

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 32; cam.nearClipPlane = 0.5f; cam.farClipPlane = 1500; cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x0B, 0x0D, 0x0C, 255);   // GM950 until the floor style sets its own
            var cd = camGo.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>();
            var rts = camGo.AddComponent<RtsCamera>();
            var map = ZMapData.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>(GridPath).text);
            rts.mapSizeMeters = new Vector2(map.width * 2, map.height * 2);
            rts.focus = new Vector3(map.width, 0f, -map.height);                           // the field's centre
            rts.height = 70f;

            var zgo = new GameObject("ZGame");
            var zg = zgo.AddComponent<ZGame>(); zg.level = OzUnitEditor.LevelId; zg.player = Team.Red; zg.blueBot = false; zg.redBot = false;
            var prefabs = PrefabNames.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Z_{n}.prefab")).ToList();
            zg.unitPrefabs = prefabs.Where(g => g).ToArray();
            var zin = zgo.AddComponent<PlayerInput>(); zin.game = zg; zin.cam = rts;
            var zov = zgo.AddComponent<SelectionOverlay>(); zov.input = zin; zov.overlayMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/UI/M_Overlay.mat");
            zgo.AddComponent<OzUnitEditor>();
            log.AppendLine($"ZGame: level {zg.level}, player {zg.player}, bots red {zg.redBot} blue {zg.blueBot}, unit prefabs {zg.unitPrefabs.Length} of {PrefabNames.Length}, overlay material {(zov.overlayMaterial ? "ok" : "MISSING")}");

            new GameObject("ZodObjects");
            var col = new GameObject("COL_Terrain");
            var bc = col.AddComponent<BoxCollider>(); bc.size = new Vector3(400f, 1f, 300f); bc.center = Vector3.zero;
            col.transform.position = new Vector3(map.width, -0.5f, -map.height);
            log.AppendLine($"ground collider COL_Terrain: {bc.size} centred at {col.transform.position} (top at 0), no renderer");

            Directory.CreateDirectory($"{Root}/Scenes");
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            int at = list.FindIndex(s => s.path == ScenePath);
            if (at < 0) list.Add(new EditorBuildSettingsScene(ScenePath, true)); else list[at] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = list.ToArray();
            log.AppendLine($"scene saved {(saved ? "OK" : "FAILED")}: {ScenePath}; build settings: {list.Count} scenes, the playground at {list.FindIndex(s => s.path == ScenePath)} (enabled)");
            log.AppendLine(EnsureHoloMaterial());
            var text = T + "\n" + log;
            Debug.Log(text);
            return text;
        }

        /// <summary>The hologram's material in Resources (it keeps the shader in builds): made from the shader when it is not there yet, its
        /// shader set again when the reference does not name it. The kit ships the shader only, so the reference is always the shader's
        /// real id.</summary>
        public static string EnsureHoloMaterial()
        {
            string dir = $"{Root}/Resources/OpenZ/UnitEditor", p = $"{Root}/Resources/{OzUeObstacleViews.MaterialPath}.mat";
            var sh = Shader.Find(OzUeObstacleViews.ShaderName);
            if (!sh) return "hologram shader MISSING: " + OzUeObstacleViews.ShaderName + " (apply the kit, let the editor import it)";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m)
            {
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder($"{Root}/Resources/OpenZ", "UnitEditor");
                m = new Material(sh) { name = "M_UeHolo" };
                AssetDatabase.CreateAsset(m, p); AssetDatabase.SaveAssets();
                return "hologram material made: " + p + " (shader " + sh.name + ")";
            }
            if (m.shader == sh) return "hologram material: shader " + sh.name + " (ok)";
            m.shader = sh; EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
            return "hologram material: shader set again to " + sh.name;
        }

        // ------------------------------------------------------------------------------------------------ 2 Counted Check
        /// <summary>Every hook line of the kit in the game's scripts (P1's 32, then P2's 2 camera lines and P4's main menu line): (file
        /// under Scripts/, a piece of code only the hook has), each once.</summary>
        static readonly (string file, string marker)[] Hooks =
        {
            ("View/PlayerInput.cs", "!s.Destroyed && Mine(s.Owner)"), ("View/PlayerInput.cs", "|| !Mine(o.Owner) ||"), ("View/PlayerInput.cs", "|| OzUeHooks.OrdersHeld"),
            ("View/PlayerInput.cs", "w.SendStop(SideOf(u), u)"), ("View/PlayerInput.cs", "w.SendPatrol(SideOf(u), u, pt.x, pt.y)"), ("View/PlayerInput.cs", "|| BothSides) continue;"),
            ("View/PlayerInput.cs", "if (!Pickable(o)) continue;"), ("View/PlayerInput.cs", "W.Objects.Where(o => Pickable(o) && ScreenRect(o)"),
            ("View/PlayerInput.cs", "W.OwnedSelectable(SideOf(pick))"), ("View/PlayerInput.cs", "o.Owner == game.player && !BothSides))"),
            ("View/PlayerInput.cs", "(BothSides || o.Owner != game.player)) ||"), ("View/PlayerInput.cs", "BothSides ? h.Owner != u.Owner : h.Owner != game.player"),
            ("View/PlayerInput.cs", "|| OzUeHooks.PlainObstacleClick(h, ctrl)"), ("View/PlayerInput.cs", "W.Eject(SideOf(u), u)"),
            ("View/PlayerInput.cs", "W.SendWaypoints(SideOf(kv.Key), kv.Key, kv.Value)"),
            ("Sim/ZWorld.Rules.cs", "if (NoGameEnd) return;"), ("Sim/ZWorld.Rules.cs", "|| !FlagCaptureAllowed(f)) continue;"),
            ("UI/OzCheatSpawner.cs", "|| OzUeHooks.NoCheatPanel) return;"), ("UI/OpenZCursors.cs", "var me = OzUeHooks.CursorTeam(hud);"),
            ("UI/OpenZUnitOverlay.cs", "o.Owner == hud.Player && !OzUeHooks.TeamBrackets ?"), ("View/CombatView.cs", "(OzUeHooks.ObstacleDied(o, p) || HarbourDetails.SimWreckDied("),
            ("UI/OpenZHud.cs", "(OzSettingTips.MinimalUi || OzUeHooks.HideMatchHud) && !Arranging"), ("UI/OpenZHud.cs", "|| ArrangeHeld || OzUeHooks.HudModal;"),
            ("UI/OpenZHud.cs", "(o.Owner == Player || OzUeHooks.BothTeams(o.Owner))"),
            ("View/Zod/ZodBriefing.cs", "if (OzUeHooks.NoCameo(s)) return;"), ("View/AudioDirector.cs", "if (OzUeHooks.MuteAnnouncer) return false;"),
            ("View/AudioDirector.cs", "if (OzUeHooks.MenuMusic) { if (musicDef == null || musicDef.id != \"mus_menu\") StartMenuMusic(); return; }"),
            ("View/OzBindings.cs", "if (kb == null || OzUeHooks.SilenceAction(a)) return null;"), ("View/OzTrackpadScroll.cs", "mouse != null && !OzUeHooks.WheelBlocked() ?"),
            ("UI/OpenZPauseMenu.cs", "&& !OzConfirm.Blocks && !OzUeHooks.EscTaken;"), ("View/TrackMarks.cs", "== 0 || OzUeHooks.NoGroundDressing) return null;"),
            ("View/CraterField.cs", "if (!Enabled || OzUeHooks.NoGroundDressing) return;"),
            ("View/RtsCamera.cs", "OzFreeCam.On || OzUeHooks.Active"), ("View/RtsCamera.cs", "!flying && !OzUeHooks.Typing"),
            ("UI/OpenZMainMenu.cs", "AddItem(\"UNIT EDITOR\", \"PLAYGROUND \u00B7 ALL UNITS \u00B7 STATS\", () => GoUnitEditor());"),
        };

        static int Count(string text, string piece) { int n = 0, i = 0; while ((i = text.IndexOf(piece, i, StringComparison.Ordinal)) >= 0) { n++; i += piece.Length; } return n; }

        public static string Check()
        {
            var log = new StringBuilder();
            int fails = 0;
            int L(bool ok, string what) => OzUnitEditor.Line(log, ok, what);
            if (EditorApplication.isPlaying) fails += L(false, "run the counted check in edit mode (not playing)");
            bool open = EditorSceneManager.GetActiveScene().path == ScenePath;
            if (!open && File.Exists(ScenePath))
            {
                if (AnyDirtyScene()) fails += L(false, "an open scene has unsaved changes: the playground scene was not opened (save or close it, then check again)");
                else { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); open = true; }
            }
            fails += L(File.Exists(ScenePath), "scene " + ScenePath + " exists");
            var bs = EditorBuildSettings.scenes.ToList(); int idx = bs.FindIndex(s => s.path == ScenePath);
            int lastLevel = bs.FindLastIndex(s => s.path.Contains("/L02_") || s.path.Contains("/L16_") || s.path.Contains("/L17_"));
            fails += L(idx >= 0 && bs[idx].enabled && idx > lastLevel, $"build settings: the playground enabled at {idx}, after the last level ({lastLevel})");
            if (open)
            {
                var sun = GameObject.Find("Sun") ? GameObject.Find("Sun").GetComponent<Light>() : null;
                fails += L(sun && sun.type == LightType.Directional && sun.shadows == LightShadows.Soft && RenderSettings.sun == sun && !RenderSettings.fog,
                           $"Sun: directional, soft shadows, the scene's sun, fog off{(sun ? $" (intensity {sun.intensity:0.00})" : " (MISSING)")}");
                var vol = UnityEngine.Object.FindFirstObjectByType<Volume>();
                fails += L(vol && vol.isGlobal && vol.sharedProfile, "PostFX: a global volume with the game's profile");
                var camGo = GameObject.FindWithTag("MainCamera"); var cam = camGo ? camGo.GetComponent<Camera>() : null; var rts = camGo ? camGo.GetComponent<RtsCamera>() : null;
                fails += L(cam && Mathf.Approximately(cam.fieldOfView, 32f) && Mathf.Approximately(cam.nearClipPlane, 0.5f) && Mathf.Approximately(cam.farClipPlane, 1500f) && cam.allowHDR
                           && camGo.GetComponent<AudioListener>() && camGo.GetComponent<UniversalAdditionalCameraData>(), "Main Camera: FOV 32, near 0.5, far 1500, HDR, URP data, AudioListener");
                fails += L(rts && rts.mapSizeMeters == new Vector2(320f, 200f) && Vector3.Distance(rts.focus, new Vector3(160f, 0f, -100f)) < 0.01f && Mathf.Approximately(rts.height, 70f),
                           "RtsCamera: 320 x 200 m field, focus on its centre (160, 0, -100), start height 70 m");
                var zg = UnityEngine.Object.FindFirstObjectByType<ZGame>();
                fails += L(zg && zg.level == OzUnitEditor.LevelId && zg.player == Team.Red && !zg.blueBot && !zg.redBot, $"ZGame: level UE, the player red, no CPU bots{(zg ? "" : " (MISSING)")}");
                fails += L(zg && zg.unitPrefabs != null && zg.unitPrefabs.Length == PrefabNames.Length && zg.unitPrefabs.All(g => g), $"ZGame: {PrefabNames.Length} unit prefabs, none missing ({(zg && zg.unitPrefabs != null ? zg.unitPrefabs.Length : 0)})");
                var zin = zg ? zg.GetComponent<PlayerInput>() : null; var zov = zg ? zg.GetComponent<SelectionOverlay>() : null;
                fails += L(zin && zin.game == zg && zin.cam == rts && zov && zov.input == zin && zov.overlayMaterial && zg.GetComponent<OzUnitEditor>(),
                           "ZGame object: PlayerInput (game, camera), SelectionOverlay (input, material), OzUnitEditor");
                fails += L(GameObject.Find("ZodObjects"), "ZodObjects root");
                var col = GameObject.Find("COL_Terrain"); var bc = col ? col.GetComponent<BoxCollider>() : null;
                fails += L(bc && bc.size == new Vector3(400f, 1f, 300f) && Mathf.Abs(col.transform.position.y + bc.center.y + bc.size.y * 0.5f) < 0.001f && !col.GetComponent<Renderer>(),
                           "COL_Terrain: a 400 x 1 x 300 m box collider, its top at 0, no renderer");
                fails += L(!UnityEngine.Object.FindFirstObjectByType<DayNight>() && !GameObject.Find("Environment"), "no environment, no day-night clock in the playground");
            }
            string scripts = Application.dataPath + "/OpenZ/Scripts/";
            var texts = new Dictionary<string, string>();
            foreach (var (file, marker) in Hooks)
            {
                if (!texts.TryGetValue(file, out var t)) texts[file] = t = File.Exists(scripts + file) ? File.ReadAllText(scripts + file) : "";
                fails += L(Count(t, marker) == 1, $"hook in {file}: {marker}");
            }
            OzLinks.Reload(); string discord = OzLinks.Get(OzLinks.Discord);                                    // the links file read again
            fails += L(discord.EndsWith("/" + DiscordInvite, StringComparison.Ordinal),
                       $"the game's Discord link (key discord of the links file): {OzLinks.Site(OzLinks.Discord)} invite {DiscordInvite}, the non-expiring one (OPENZ DISCORD, OPEN DISCORD)");
            fails += L(OzUeStatics.Names.Count == 0, "no static is held outside Play (the playground holds ZWorld.CalmStartDeviation = false while it runs)");
            fails += OzUnitEditor.SelfCheck(log);
            int pass = log.ToString().Split('\n').Count(l => l.StartsWith("PASS "));
            int fail = log.ToString().Split('\n').Count(l => l.StartsWith("FAIL "));
            var text = $"[OpenZ UnitEditor] COUNTED CHECK: {pass} PASS, {fail} FAIL\n{log}";
            if (fail == 0) Debug.Log(text); else Debug.LogWarning(text);
            return text;
        }
    }
}
