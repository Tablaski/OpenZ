using System;
using System.Collections;
using System.IO;
using System.Linq;
using OpenZ.Experimental;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    internal sealed class SkirmishMenuProof : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-oz-duel-skirmish-proof");
            if (option < 0 || option + 1 >= args.Length) return;
            var proof = new GameObject(nameof(SkirmishMenuProof)).AddComponent<SkirmishMenuProof>();
            DontDestroyOnLoad(proof);
            int roomOption = Array.IndexOf(args, "-oz-duel-proof-three-humans");
            string mode = roomOption >= 0 && roomOption + 1 < args.Length ? args[roomOption + 1] :
                args.Contains("-oz-duel-proof-online-bots") ? "online" : args.Contains("-oz-duel-proof-solo-tech") ? "solo" : "local";
            proof.StartCoroutine(proof.Run(args[option + 1], mode));
        }

        static void Click(string text)
        {
            var button = DuelRuntime.I.Menus.GetComponentsInChildren<Button>()
                .SingleOrDefault(control => control.isActiveAndEnabled && control.interactable && control.name == text);
            if (!button) throw new InvalidOperationException("Missing enabled button: " + text);
            button.onClick.Invoke();
        }

        IEnumerator Run(string folder, string mode)
        {
            Directory.CreateDirectory(folder);
            var checks = mode switch
            {
                "online" => ExerciseOnline(folder),
                "solo" => ExerciseSoloTech(folder),
                "host" or "guest1" or "guest2" => ExerciseSharedRoom(folder, mode),
                _ => Exercise(folder)
            };
            string report = Path.Combine(folder, mode is "host" or "guest1" or "guest2" ? mode + "-result.txt" : "result.txt");
            while (true)
            {
                object wait;
                try
                {
                    if (!checks.MoveNext()) break;
                    wait = checks.Current;
                }
                catch (Exception error)
                {
                    File.WriteAllText(report, "FAIL: " + error);
                    Application.Quit(1);
                    yield break;
                }
                yield return wait;
            }
            File.WriteAllText(report, "PASS: " + mode + " Skirmish validation.");
            Application.Quit();
        }

        IEnumerator Exercise(string folder)
        {
            yield return new WaitForSecondsRealtime(3);
            DuelRuntime.I.Menus.ShowConnect();
            yield return null;
            yield return null;
            Click("LOCAL SKIRMISH WITH BOTS");
            yield return null;
            yield return null;
            Click("2");
            yield return null;
            yield return null;
            DuelProofCapture.Save(Path.Combine(folder, "01-map-two-starts.png"), true);
            Click("BALANCING OPTIONS");
            yield return null;
            yield return null;
            var balance = DuelRuntime.I.Menus.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Territory cap");
            balance.value = 25;
            balance.GetComponent<DuelLobbySlider>().Commit(25);
            yield return null;
            yield return null;
            DuelProofCapture.Save(Path.Combine(folder, "02-balancing.png"), true);
            Click("STARTING ARMIES");
            yield return null;
            yield return null;
            Click("PLAYER ADVANTAGE");
            yield return null;
            yield return null;
            Click("BATTLE RULES");
            yield return null;
            yield return null;
            DuelProofCapture.Save(Path.Combine(folder, "03-battle-rules.png"), true);
            Click("LAUNCH MATCH");
            yield return new WaitForSecondsRealtime(10);
            var game = ZGame.I;
            if (!game || !game.World.Map.skirmish || game.World.Bots.Count != 1 || !game.World.HasSkirmishProduction || game.World.Time <= 0)
                throw new InvalidOperationException("The two-start game did not apply the selected balancing rules and start the bot.");

            SceneManager.LoadScene("MainMenu");
            yield return new WaitForSecondsRealtime(6);
            DuelRuntime.I.Menus.ShowConnect();
            yield return null;
            yield return null;
            Click("LOCAL SKIRMISH WITH BOTS");
            yield return null;
            yield return null;
            Click("MAP");
            yield return null;
            yield return null;
            Click("6 active players");
            yield return null;
            yield return null;
            if (DuelRuntime.I.Menus.GetComponentsInChildren<Button>().Count(button => button.name == "CLOSED") != 2)
                throw new InvalidOperationException("Six active participants require two closed positions on the eight-start map.");
            DuelProofCapture.Save(Path.Combine(folder, "04-six-participants.png"), true);
            Click("4");
            yield return null;
            yield return null;
            Click("LAUNCH MATCH");
            yield return new WaitForSecondsRealtime(14);
            game = ZGame.I;
            var terrain = FindFirstObjectByType<OzConversionTerrainV2>();
            if (!game || !game.World.Map.skirmish || game.World.Bots.Count != 3 || !terrain || !terrain.IsPaletteV3 || game.World.Time <= 0)
                throw new InvalidOperationException("The four-start game did not start with three bots and the V3 terrain.");
            DuelProofCapture.Save(Path.Combine(folder, "05-four-start-game.png"));
        }

        IEnumerator ExerciseOnline(string folder)
        {
            yield return new WaitForSecondsRealtime(3);
            var runtime = DuelRuntime.I;
            runtime.ConnectOnline("Skirmish Proof", true, "", skirmish: true);
            double deadline = Time.realtimeSinceStartupAsDouble + 45;
            while ((!runtime.Connected || string.IsNullOrEmpty(runtime.OnlineRoom)) && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (!runtime.Connected || string.IsNullOrEmpty(runtime.OnlineRoom))
                throw new InvalidOperationException("Online room creation failed: " + runtime.Status);
            yield return null;
            yield return null;
            Click("READY");
            yield return null;
            yield return null;
            if (!runtime.RoomCanLaunch || runtime.RoomTransport.Remotes.Length != 0)
                throw new InvalidOperationException("The host cannot launch without a connected guest.");
            DuelProofCapture.Save(Path.Combine(folder, "01-online-ready.png"), true);
            Click("LAUNCH MATCH");
            deadline = Time.realtimeSinceStartupAsDouble + 60;
            while ((!runtime.Playing || !runtime.Game || runtime.Game.World.Time < 1) && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (!runtime.Playing || !runtime.Game || runtime.Game.World.Bots.Count != 2 || runtime.Game.World.Time < 1)
                throw new InvalidOperationException("The online host did not enter the match with two bots: " + runtime.Status);
            runtime.Close();
        }

        IEnumerator ExerciseSharedRoom(string folder, string role)
        {
            yield return new WaitForSecondsRealtime(3);
            bool host = role == "host";
            var runtime = DuelRuntime.I;
            string linkFile = Path.Combine(folder, "join.txt");
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            if (host)
            {
                runtime.ConnectOnline("Skirmish Host", true, "", skirmish: true);
                while ((!runtime.Connected || string.IsNullOrEmpty(runtime.OnlineRoom)) && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                if (!runtime.Connected || string.IsNullOrEmpty(runtime.OnlineRoom))
                    throw new InvalidOperationException("Online room creation failed: " + runtime.Status);
                File.WriteAllText(linkFile, runtime.Invitation("", 0));
            }
            else
            {
                while (!File.Exists(linkFile) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!File.Exists(linkFile)) throw new IOException("The host did not publish the test invitation.");
                runtime.ConnectOnline(role, false, File.ReadAllText(linkFile));
            }

            while ((host ? runtime.HostedRoom?.Members.Length : runtime.JoinedRoom?.Humans.Length) != 3 &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if ((host ? runtime.HostedRoom?.Members.Length : runtime.JoinedRoom?.Humans.Length) != 3)
                throw new InvalidOperationException("The lobby did not seat all three humans: " + runtime.Status);
            runtime.SetRoomReady(true);
            if (host)
            {
                while (!runtime.RoomCanLaunch && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!runtime.RoomCanLaunch) throw new InvalidOperationException("Three ready humans did not unlock launch.");
                DuelProofCapture.Save(Path.Combine(folder, "01-three-humans.png"), true);
                runtime.LaunchRoom();
            }
            deadline = Time.realtimeSinceStartupAsDouble + 80;
            while ((!runtime.Playing || !runtime.Game || runtime.Game.World.Time < 3) && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (!runtime.Playing || !runtime.Game || runtime.LocalTeam == Team.Neutral || runtime.Game.World.Bots.Count != 0 ||
                runtime.Game.World.Time < 3 || !runtime.Game.World.Objects.Any(unit => unit.Owner == runtime.LocalTeam && unit.IsFort))
                throw new InvalidOperationException("The three-human battlefield did not start on the assigned seat: " + runtime.Status);
            File.WriteAllText(Path.Combine(folder, role + "-loaded.txt"), runtime.LocalTeam.ToString());
            var roles = new[] { "host", "guest1", "guest2" };
            while (!roles.All(seat => File.Exists(Path.Combine(folder, seat + "-loaded.txt"))) &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!roles.All(seat => File.Exists(Path.Combine(folder, seat + "-loaded.txt"))))
                throw new InvalidOperationException("A participant did not reach the shared battlefield.");
            if (host && roles.Select(seat => File.ReadAllText(Path.Combine(folder, seat + "-loaded.txt"))).Distinct().Count() != 3)
                throw new InvalidOperationException("A starting position was shared by multiple humans.");
        }

        IEnumerator ExerciseSoloTech(string folder)
        {
            yield return new WaitForSecondsRealtime(3);
            var map = OzAiMapCatalog.All.First(entry => entry.Solo && DuelMenus.ReadMapTech(entry.Id).Vehicles is > 0 and < 6);
            var original = DuelMenus.ReadMapTech(map.Id);
            string old = PlayerPrefs.GetString(OzSoloMapTech.Preference, "");
            bool existed = PlayerPrefs.HasKey(OzSoloMapTech.Preference);
            try
            {
                OzSoloMapTech.Show(map.Id);
                yield return null;
                var tech = FindFirstObjectByType<OzSoloMapTech>();
                var reset = tech.GetComponentsInChildren<Button>().Single(button => button.name == "RESTORE ORIGINAL TECH");
                reset.onClick.Invoke();
                yield return null;
                var vehicleIncrease = tech.GetComponentsInChildren<Button>().First(button => button.name == "+" && button.interactable);
                vehicleIncrease.onClick.Invoke();
                yield return null;
                var chosen = OzSoloMapTech.Read(map.Id);
                if (!chosen.Native || chosen.Vehicle != original.Vehicles + 1)
                    throw new InvalidOperationException("The chosen mission did not save its technology override.");
                DuelProofCapture.Save(Path.Combine(folder, "01-solo-tech.png"), true);
                SceneManager.LoadScene(map.ScenePath);
                yield return new WaitForSecondsRealtime(12);
                var game = ZGame.I;
                if (!game || game.World.Map.skirmish || !game.World.UseNativeMapTech || game.World.MapFactoryTech("vehicle_factory") != chosen.Vehicle)
                    throw new InvalidOperationException("The mission did not apply the saved factory level.");
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(OzSoloMapTech.Preference, old);
                else PlayerPrefs.DeleteKey(OzSoloMapTech.Preference);
                PlayerPrefs.Save();
            }
        }
    }
}
