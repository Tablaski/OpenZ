using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in Windows release regression checks using the shipped scenes and resources.</summary>
    public sealed class OpenZReleaseProof : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-oz-duel-release-proof");
            if (option < 0 || option + 1 >= args.Length || FindFirstObjectByType<OpenZReleaseProof>()) return;
            var go = new GameObject("OpenZ-VK release proof");
            DontDestroyOnLoad(go);
            go.AddComponent<OpenZReleaseProof>().folder = args[option + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            var checks = CheckRelease();
            Exception failure = null;
            try
            {
                while (true)
                {
                    object step;
                    try { if (!checks.MoveNext()) break; step = checks.Current; }
                    catch (Exception error) { failure = error; break; }
                    yield return step;
                }
            }
            finally { (checks as IDisposable)?.Dispose(); }
            var result = failure == null ? "PASS: production identity, both changelog tabs, native AI map videos, Bridge Game crane repair and tank boarding, tactical-pause key, independent oil and shake." : "FAIL: " + failure;
            File.WriteAllText(Path.Combine(folder, "result.txt"), result);
            Debug.Log(result);
            OzQuit.ExitCode = failure == null ? 0 : 1;
            OzQuit.Request("OpenZ-VK release proof complete");
        }

        IEnumerator CheckRelease()
        {
            Require(OzRelease.IsRelease && !Debug.isDebugBuild && Application.version == OzForkVersion.Number && OzResourcePackage.Compatible,
                "Production flags, application version or resource identity are wrong.");
            OpenZMainMenu menu = null;
            foreach (var step in WaitFor(() => (menu = FindFirstObjectByType<OpenZMainMenu>()) && menu.ProofPanel("changes"), "main menu")) yield return step;
            menu.ProofOpen("changes");
            var selectTab = typeof(OpenZMainMenu).GetMethod("SelectChangesTab", Private);
            for (int tab = 0; tab < 2; tab++)
            {
                selectTab.Invoke(menu, new object[] { tab });
                yield return null;
                Require(menu.ProofChangesMax > 0 && menu.ProofChangesInfo.Contains("overlaps 0") && menu.ProofChangesInfo.Contains("overflow 0"),
                    "Changes & Updates tab " + tab + ": " + menu.ProofChangesInfo);
                Debug.Log("[Release proof] Changes & Updates tab " + tab + ": " + menu.ProofChangesInfo);
            }
            menu.ProofOpen("");
            foreach (string id in new[] { "SM01", "SM05", "SM09", "SM13", "SM19" })
            {
                var preview = OzLevelPreview.Create(transform, id);
                try
                {
                    Require(preview.Poster, id + " is missing its fallback still.");
                    preview.PlayFrom(0, true);
                    foreach (var step in WaitFor(() =>
                    {
                        OzLevelPreview.PumpPrepares();
                        if (preview.Failed) throw new InvalidOperationException(id + ": " + preview.LastError);
                        return preview.ShowingVideo && preview.Player.frame > 2;
                    }, id + " video", 15)) yield return step;
                    Debug.Log("[Release proof] Native video decoded: " + id);
                }
                finally { preview.Release(); }
            }

            string level = PlayerPrefs.GetString(OpenZMainMenu.PrefLevel, "");
            bool hasLevel = PlayerPrefs.HasKey(OpenZMainMenu.PrefLevel);
            string roster = PlayerPrefs.GetString(OzSoloAllowedUnits.Preference, "");
            bool hasRoster = PlayerPrefs.HasKey(OzSoloAllowedUnits.Preference);
            const string exactKey = "oz_zod_exact";
            int? exact = PlayerPrefs.HasKey(exactKey) ? PlayerPrefs.GetInt(exactKey) : (int?)null;
            bool tactical = OzTacticalPause.On;
            const string oilKey = OzBattleOptions.TankDamageKey, shakeKey = OzBattleOptions.TankShakeKey;
            int? oil = PlayerPrefs.HasKey(oilKey) ? PlayerPrefs.GetInt(oilKey) : (int?)null;
            int? shake = PlayerPrefs.HasKey(shakeKey) ? PlayerPrefs.GetInt(shakeKey) : (int?)null;
            bool reduced = OzMotion.Reduced;
            try
            {
                PlayerPrefs.SetInt(exactKey, 0);
                OzSoloAllowedUnits.Store(new DuelRules());
                menu.ProofSelect();
                menu.ProofFocus(menu.ProofLevelIndex(OzCity.BridgeGame));
                yield return null;
                menu.ProofDeploy();
                foreach (var step in WaitFor(() => ZGame.I && ZGame.I.level == OzCity.BridgeGame && ZGame.I.World.Time > .5 &&
                    OpenZHud.I && !RtsCamera.InputLocked, "Bridge Game deployment", 100)) yield return step;
                var game = ZGame.I;
                var world = game.World;
                foreach (var bot in world.Bots) bot.Ignored = true;
                Require(world.L18Map && world.Objects.Any(o => o.Type == ZBigTanks.Mammoth && o.Owner == Team.Neutral) &&
                    world.Objects.Any(o => o.Type == ZBigTanks.Bizon && o.Owner == Team.Neutral), "The native Bridge Game starting vehicles are missing.");
                var bridges = world.Objects.Where(o => o.IsBridge && o.Destroyed && OzBridgeKit.Tiles(o) >= OzBridgeKit.LongFromTiles).ToArray();
                Require(bridges.Length >= 3, "Bridge Game is missing its three breached long bridges.");
                var repair = (from candidate in bridges
                              from unit in world.Objects
                              where unit.Type == "crane" && (unit.Owner == game.player || unit.Owner == Team.Neutral) &&
                                    unit.CraneLandEndPick(candidate, out _, out _)
                              orderby (candidate.CenterX - unit.CenterX) * (candidate.CenterX - unit.CenterX) +
                                      (candidate.CenterY - unit.CenterY) * (candidate.CenterY - unit.CenterY)
                              select (bridge: candidate, crane: unit)).FirstOrDefault();
                var (bridge, crane) = repair;
                Require(bridge != null && crane != null, "No starting crane could reach a breached bridge.");
                if (crane.Owner == Team.Neutral)
                    foreach (var step in BoardVehicle(game, crane)) yield return step;
                var rulesView = game.GetComponent<RulesView>();
                var intact = OzBridgeKit.Prefab(game, bridge, "intact");
                Require(intact && intact.name.Contains("city_long"), "The native long bridge prefab is missing.");
                bridge.DoAutoRepair = true;
                bridge.NextAutoRepairTime = world.Time + 1000;
                var craneOrder = OpenZHud.I.input.OrderFor(crane, bridge, bridge.CenterX, bridge.CenterY);
                Require(craneOrder.mode == WpMode.CraneRepair && world.SendWaypoints(game.player, crane, new[] { craneOrder }), "The crane could not take the bridge repair order.");
                game.gameSpeed = 2;
                foreach (var step in WaitFor(() => crane.CraneWorking || !bridge.Destroyed, "crane starting its bridge repair", 55)) yield return step;
                Require(crane.CraneWorking && crane.CraneLandEndDry(bridge, crane.CenterX, crane.CenterY), "The crane did not repair from the bank.");
                foreach (var step in WaitFor(() => !bridge.Destroyed, "crane completing its bridge repair", 55)) yield return step;
                game.gameSpeed = 1;
                foreach (var step in WaitFor(() => rulesView.BridgeModels(bridge)[0].activeInHierarchy, "restored long bridge")) yield return step;
                Require(world.Path.TilePassable(bridge.CenterX / 16, bridge.CenterY / 16, false), "The restored bridge rejected vehicle pathing.");
                Debug.Log("[Release proof] Crane repaired the official long bridge from the bank. Visual and pathing recovered.");

                foreach (string type in ZBigTanks.Types)
                {
                    var tank = world.Objects.Single(unit => unit.Type == type && unit.Owner == Team.Neutral);
                    foreach (var step in BoardVehicle(game, tank)) yield return step;
                    Require(world.SendWaypoints(game.player, tank, new[] { ZWaypoint.Move(tank.CenterX + 32, tank.CenterY) }), "The captured tank rejected a movement order: " + type);
                }

                var binding = OzBindings.DefOf(OzAction.TacticalPause);
                int? savedKey = PlayerPrefs.HasKey(binding.pref) ? PlayerPrefs.GetInt(binding.pref) : (int?)null;
                var background = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                PlayerPrefs.DeleteKey(binding.pref);
                OzBindings.Reload();
                OzTacticalPause.On = true;
                var keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    var key = OzBindings.KeyOf(OzAction.TacticalPause);
                    Require(key == Key.Space, "The default tactical-pause key is not Space.");
                    keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    foreach (var step in WaitFor(() => OzTacticalPause.Frozen, "tactical freeze from the configured key")) yield return step;
                    double time = world.Time;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    var fort = world.Objects.First(o => o.IsFort && o.Owner == game.player);
                    var differentUnit = world.ProductionUnits(fort).First(type => type != fort.CurUnit);
                    Require(world.StartBuilding(game.player, fort, differentUnit) && world.SendStop(game.player, crane), "Tactical pause refused an order or production change.");
                    yield return new WaitForSecondsRealtime(.4f);
                    Require(world.Time == time, "The simulation moved during tactical pause.");
                    keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    foreach (var step in WaitFor(() => !OzTacticalPause.Busy && world.Time > time, "tactical resume from the configured key")) yield return step;
                }
                finally
                {
                    InputSystem.RemoveDevice(keyboard);
                    InputSystem.settings.backgroundBehavior = background;
                    if (savedKey.HasValue) PlayerPrefs.SetInt(binding.pref, savedKey.Value);
                    OzBindings.Reload();
                }
                Debug.Log("[Release proof] Tactical pause froze and resumed the real Bridge Game simulation.");
                CheckDamage(game);
            }
            finally
            {
                if (hasLevel) PlayerPrefs.SetString(OpenZMainMenu.PrefLevel, level);
                else PlayerPrefs.DeleteKey(OpenZMainMenu.PrefLevel);
                if (hasRoster) PlayerPrefs.SetString(OzSoloAllowedUnits.Preference, roster);
                else PlayerPrefs.DeleteKey(OzSoloAllowedUnits.Preference);
                if (exact.HasValue) PlayerPrefs.SetInt(exactKey, exact.Value);
                else PlayerPrefs.DeleteKey(exactKey);
                OzTacticalPause.On = tactical;
                if (oil.HasValue) PlayerPrefs.SetInt(oilKey, oil.Value); else PlayerPrefs.DeleteKey(oilKey);
                if (shake.HasValue) PlayerPrefs.SetInt(shakeKey, shake.Value); else PlayerPrefs.DeleteKey(shakeKey);
                OzMotion.Reduced = reduced;
                PlayerPrefs.Save();
            }
        }

        static IEnumerable BoardVehicle(ZGame game, ZObject vehicle)
        {
            var world = game.World;
            int tx = vehicle.CenterX / 16, ty = vehicle.CenterY / 16;
            var spot = new[] { (tx - 3, ty), (tx + 3, ty), (tx, ty - 3), (tx, ty + 3) }
                .First(p => world.Path.TilePassable(p.Item1, p.Item2, false));
            var robot = world.CreateRobotGroup("psycho", spot.Item1 * 16, spot.Item2 * 16, game.player, 1);
            Require(robot != null && game.SpawnUnitView(robot), "The boarding proof could not place a robot.");
            var order = OpenZHud.I.input.OrderFor(robot, vehicle, vehicle.CenterX, vehicle.CenterY);
            Require(order.mode == WpMode.Enter && world.SendWaypoints(game.player, robot, new[] { order }), "Could not order a robot to board " + vehicle.Type);
            foreach (var step in WaitFor(() => vehicle.Owner == game.player && vehicle.Drivers.Count > 0, "boarding " + vehicle.Type, 25)) yield return step;
        }

        static void CheckDamage(ZGame game)
        {
            var tank = game.World.CreateObject("vehicle", "light", 256, 256, game.player);
            var view = game.SpawnUnitView(tank);
            Require(view, "The damage test could not spawn a light tank.");
            tank.MaxHealth = 1000;
            var visual = typeof(UnitView).GetMethod("CripplingVisual", Private);
            Quaternion Pose(bool moving, Vector3 p) => (Quaternion)visual.Invoke(view, new object[] { moving, p, .016f });
            var origin = game.CenterWorld(tank);
            int Marks() => game.GetComponent<OzCripplingTrails>()?.Emitted ?? 0;
            var initial = Marks();
            OzBattleOptions.SoloDamage = true;
            OzMotion.Reduced = false;
            tank.Health = 310;
            Pose(true, origin);
            Pose(true, origin + Vector3.right);
            Require(Marks() == initial && Pose(false, origin) == Quaternion.identity, "Damage effects started above 30%.");
            tank.Health = 300;
            Require(Quaternion.Angle(Pose(false, origin), Quaternion.identity) > .05f, "A stationary tank did not shake at 30%.");
            Require(Marks() == initial, "Stationary oil emission occurred.");
            Pose(true, origin);
            Pose(true, origin + Vector3.right);
            Require(Marks() == initial + 1, "A moving tank did not leak at 30%.");
            OzMotion.Reduced = true;
            Require(Pose(false, origin) == Quaternion.identity, "Reduced motion did not suppress the shake.");
            OzMotion.Reduced = false;
            OzBattleOptions.SoloDamage = false;
            tank.Health = 100;
            Require(Pose(true, origin + Vector3.right * 2) == Quaternion.identity &&
                Marks() == initial + 1, "Turning damage effects off did not stop both oil and shake.");
            OzBattleOptions.SoloDamage = true;
            tank.Health = 1000;
            Require(Pose(false, origin) == Quaternion.identity, "The shake continued after repair.");
            Debug.Log("[Release proof] Oil and shake checked at 31%, 30%, 10% and repaired health.");
        }

        static IEnumerable WaitFor(Func<bool> check, string item, float seconds = 25)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!check())
            {
                if (Time.realtimeSinceStartup > end) throw new TimeoutException(item);
                yield return null;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
