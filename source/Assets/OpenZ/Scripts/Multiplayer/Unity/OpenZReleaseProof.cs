using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;

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
            var result = failure == null ? "PASS: production identity, both changelog tabs, native AI map videos, Bridge Game, tactical pause, independent oil and shake." : "FAIL: " + failure;
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
            bool oil = OzBattleOptions.SoloDamage, shake = OzBattleOptions.SoloShake, reduced = OzMotion.Reduced;
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
                var bridge = bridges[0];
                var rulesView = game.GetComponent<RulesView>();
                var intact = OzBridgeKit.Prefab(game, bridge, "intact");
                Require(intact && intact.name.Contains("city_long"), "The native long bridge prefab is missing.");
                bridge.SetHealth(bridge.MaxHealth);
                world.UpdateObjectHealth(bridge);
                foreach (var step in WaitFor(() => rulesView.BridgeModels(bridge)[0].activeInHierarchy, "restored long bridge")) yield return step;
                Require(world.Path.TilePassable(bridge.CenterX / 16, bridge.CenterY / 16, false), "The restored bridge rejected vehicle pathing.");
                Debug.Log("[Release proof] Official Bridge Game deployed with both big tanks. Long bridge visual and pathing recovered.");

                OzTacticalPause.On = true;
                OzTacticalPause.TestPress();
                foreach (var step in WaitFor(() => OzTacticalPause.Frozen, "tactical freeze")) yield return step;
                double time = world.Time;
                yield return new WaitForSecondsRealtime(.4f);
                Require(world.Time == time, "The simulation moved during tactical pause.");
                OzTacticalPause.TestPress();
                foreach (var step in WaitFor(() => !OzTacticalPause.Busy && world.Time > time, "tactical resume")) yield return step;
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
                OzBattleOptions.SoloDamage = oil;
                OzBattleOptions.SoloShake = shake;
                OzMotion.Reduced = reduced;
                PlayerPrefs.Save();
            }
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
            OzBattleOptions.SoloShake = true;
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
            Require(Quaternion.Angle(Pose(true, origin + Vector3.right * 2), Quaternion.identity) > .05f &&
                Marks() == initial + 1, "Shake cannot operate independently of oil.");
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
