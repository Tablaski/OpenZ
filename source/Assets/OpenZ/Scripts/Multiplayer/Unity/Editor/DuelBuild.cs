using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using OpenZ.Sim;

namespace OpenZ.Multiplayer.Editor
{
    public static class DuelBuild
    {
        [MenuItem("OpenZ/Multiplayer/Build Windows POC")]
        public static void Windows()
        {
            ValidateScriptBindings();
            ValidateWire();
            var scenes = Array.ConvertAll(new[] { "MainMenu", "L02_Desert", "L07_Volcanic", "L16_Jungle", "L17_CarPark", OzUnitEditor.SceneName }, s => "Assets/OpenZ/Scenes/" + s + ".unity").Concat(OzAiMapCatalog.Playable.Select(m=>m.ScenePath)).ToArray();
            foreach (string scene in scenes)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene))
                    throw new InvalidOperationException("Required player scene is missing: " + scene);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var args = Environment.GetCommandLineArgs();
            int outputOption = Array.IndexOf(args, "-oz-duel-output");
            string outputFolder = outputOption >= 0 && outputOption + 1 < args.Length ? Path.GetFullPath(args[outputOption + 1]) : Path.Combine(root, "builds", "OpenZ-Release");
            string output = Path.Combine(outputFolder, "openz-vk.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            OpenZ.EditorTools.OpenZBuild.IncludeRuntimeShaders();
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneWindows64, locationPathName = output,
                options = BuildOptions.CompressWithLz4
            });
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "BUILD_RESULT.txt"), report.summary.result + "\nErrors: " + report.summary.totalErrors + "\n" + output);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Duel player build failed: " + report.summary.result);
            string notices = Path.Combine(Application.dataPath, "../Packages/org.openz.datachannel/Third Party Notices.txt");
            File.Copy(notices, Path.Combine(Path.GetDirectoryName(output), "WebRTC Third Party Notices.txt"), true);
            string editorFolder = Path.Combine(outputFolder, "Unit Editor");
            Directory.CreateDirectory(editorFolder);
            File.WriteAllText(Path.Combine(editorFolder, "Open Unit Editor.bat"),
                "@echo off\r\nstart \"\" /D \"%~dp0..\" \"%~dp0..\\openz-vk.exe\" -oz-unit-editor %*\r\n");
            File.WriteAllText(Path.Combine(editorFolder, "README.txt"),
                "Double-click Open Unit Editor.bat to open the combat playground directly.\r\n" +
                "Keep this folder inside the complete OpenZ-VK player folder; it shares the game runtime.\r\n" +
                "The editor is also available as UNIT EDITOR in the game's main menu.\r\n");
            Debug.Log("[OpenZ Duel] Built " + output);
        }
        public static void ValidateScriptBindings()
        {
            string[] paths = { "View/ZGame.cs", "UI/OpenZMainMenu.cs", "View/UnitView.cs", "View/RtsCamera.cs" };
            Type[] expected = { typeof(ZGame), typeof(OpenZMainMenu), typeof(UnitView), typeof(RtsCamera) };
            bool needsImport = false;
            for (int i = 0; i < paths.Length; i++)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/OpenZ/Scripts/" + paths[i]);
                if (!script || script.GetClass() != expected[i]) needsImport = true;
            }
            // Moving a local project can leave cached MonoScript bindings stale.
            // Reimport script metadata without changing upstream source or scene files.
            if (needsImport)
                AssetDatabase.ImportAsset("Assets/OpenZ/Scripts", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < paths.Length; i++)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/OpenZ/Scripts/" + paths[i]);
                if (!script || script.GetClass() != expected[i])
                    throw new InvalidOperationException("Unity script binding is invalid: " + paths[i] + ". Reimport the Scripts folder before building.");
            }
            Debug.Log("[OpenZ Duel] Scene script bindings validated.");
        }
        public static void ValidateWire()
        {
            var command = new DuelOrder { Sequence = 1, Kind = DuelOrderKind.Waypoints,
                Points = new[] { OpenZ.Sim.ZWaypoint.Move(123, 456) } };
            var restored = JsonUtility.FromJson<DuelOrder>(JsonUtility.ToJson(command));
            if (restored.Points == null || restored.Points.Length != 1 || restored.Points[0].x != 123 || restored.Points[0].y != 456)
                throw new Exception("Unity command serialization loses waypoints.");
            var events = new DuelEventData { Events = new[] { new OpenZ.Sim.ZEvent { objId = 42, type = OpenZ.Sim.ZEventType.GameOver, value = 2 } } };
            var restoredEvents = JsonUtility.FromJson<DuelEventData>(JsonUtility.ToJson(events));
            if (restoredEvents.Events == null || restoredEvents.Events.Length != 1 || restoredEvents.Events[0].objId != 42 || restoredEvents.Events[0].value != 2)
                throw new Exception("Unity event serialization loses combat data.");
            var lobby = new DuelLobby(); lobby.Join(0, "HOST"); lobby.Join(1, "GUEST");
            var rules = DuelRuntime.Defaults("L02"); rules.EnableHandicap(1); rules.Handicap.StartingUnits.Add("heavy");
            lobby.ChangeRules(0, rules);
            var read = JsonUtility.FromJson<DuelLobbyData>(JsonUtility.ToJson(DuelLobbyData.From(lobby))).ReadRules();
            if (!read.HandicapEnabled || read.HandicapSlot != 1 || read.Handicap.StartingUnits.Count != rules.Handicap.StartingUnits.Count)
                throw new Exception("Unity lobby serialization loses the optional handicap.");
            rules.SetUnitPreset(DuelUnitPreset.Z1996); rules.SetUnitAllowed("heavy", false);
            var tanks = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, name = "Wire tank profile", master = 100 };
            tanks.Set("light", ZBehaviorKey.RedShake, 2.5);
            rules.TankDynamics = tanks.Encode();
            rules.RobotDynamics = new ZBehaviorProfile { family = ZBehaviorFamily.Robots, name = "Wire robot profile", master = 50 }.Encode();
            lobby.ChangeRules(0, rules);
            read = JsonUtility.FromJson<DuelLobbyData>(JsonUtility.ToJson(DuelLobbyData.From(lobby))).ReadRules();
            if (read.UnitPreset != DuelUnitPreset.Custom || read.Allows("heavy") || read.Allows("electrobot") || !read.Allows("mtank") || read.Allows("medium") ||
                read.TankDynamics != rules.TankDynamics || read.RobotDynamics != rules.RobotDynamics)
                throw new Exception("Unity lobby serialization loses unit restrictions or server dynamics.");
            Debug.Log("[OpenZ Duel] Unity wire validation PASS: waypoints, combat events, common rules and optional handicap.");
        }
    }
}
