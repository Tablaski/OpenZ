using System;
using System.Collections;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    internal sealed class DuelMatchControlsProof : MonoBehaviour
    {
        string folder, role, firstException;
        bool host, online, done;
        DuelRuntime Runtime => DuelRuntime.I;
        string Other => host ? "guest" : "host";
        string FileFor(string owner, string name, string ext) => Path.Combine(folder, owner + "-" + name + ext);
        internal void Begin(string roleName, string directory, bool useOnline)
        {
            folder = directory; role = roleName; host = role == "host"; online = useOnline;
            Directory.CreateDirectory(folder);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Drive());
        }
        void OnDestroy() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string _, LogType type)
        {
            if (type == LogType.Exception) firstException ??= message;
        }
        void Finish(string result)
        {
            if (done) return;
            done = true;
            File.WriteAllText(FileFor(role, "result", ".txt"), result);
            Application.Quit(result.StartsWith("PASS") ? 0 : 2);
        }
        IEnumerator Drive()
        {
            var steps = Run().GetEnumerator();
            while (!done)
            {
                object item;
                try
                {
                    if (firstException != null) throw new Exception(firstException);
                    if (!steps.MoveNext()) break;
                    item = steps.Current;
                }
                catch (Exception error) { Finish("FAIL: " + error); yield break; }
                yield return item;
            }
            if (!done) Finish("PASS: fort placement, map decline and acceptance, chat, frozen rules, reconnection and consensual lobby return.");
        }
        IEnumerable Run()
        {
            var r = Runtime;
            var invitation = Path.Combine(folder, "invitation.txt");
            if (host)
            {
                var menu = FindFirstObjectByType<OpenZMainMenu>();
                menu.ProofOpenOptions(OzSettingTips.TabNames.Length);
                yield return new WaitForSecondsRealtime(.6f);
                menu.ProofOptTab(OzSettingTips.TabNames.Length);
                yield return null;
                Shot("build-keys");
                menu.ProofOpen("");
                if (online) r.ConnectOnline("HOST", true, ""); else r.Host("HOST", 27889);
                var rules = DuelRuntime.Defaults("L02");
                rules.Common.FortTurrets = new[] { "gatling", "gun", "howitzer", "missile_cannon" };
                rules.TankDamageEffects = true;
                rules.RadarLoss = RadarLossRule.FogAndZoom;
                rules.TankDynamics = ZBehaviorProfile.Classic(ZBehaviorFamily.Tanks).Encode();
                rules.RobotDynamics = ZBehaviorProfile.Classic(ZBehaviorFamily.Robots).Encode();
                r.ChangeRules(rules);
                foreach (var step in WaitFor(() => !online || !string.IsNullOrEmpty(r.OnlineRoom), "invitation", 45)) yield return step;
                File.WriteAllText(invitation, r.Invitation("127.0.0.1", 27889));
            }
            else
            {
                foreach (var step in WaitFor(() => File.Exists(invitation), "host invitation", 45)) yield return step;
                string link = File.ReadAllText(invitation);
                if (online) r.ConnectOnline("GUEST", false, link); else r.Join("GUEST", link);
            }
            foreach (var step in WaitFor(() => r.Connected && r.View?.GuestName != null, "connected lobby", 35)) yield return step;
            yield return null;
            Shot("lobby-dynamics");
            if (host)
            {
                Click("FORT PLACEMENT · RED / BLUE"); yield return null;
                Check(r.Menus.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("FORT MOUNT ")) == 8, "Eight turret slots missing.");
                Shot("fort-slots");
                Click("CLOSE"); yield return null;
                Click("BATTLE OPTIONS"); yield return null;
                Shot("battle-options");
                Click("CLOSE");
            }
            r.Ready();
            foreach (var step in WaitFor(() => r.Playing && r.Game && r.Game.World.Time > 1, "opening gameplay", 65)) yield return step;
            if (host)
            {
                var world = r.Game.World;
                foreach (var fort in world.Objects.Where(o => o.IsFort))
                {
                    fort.GetCreationMovePoint(out _, out int gateY);
                    int nearY = gateY < fort.LocY ? fort.LocY : fort.LocY + 48;
                    var front = world.Objects.Where(o => o.Kind == ZKind.Cannon && o.Owner == fort.Owner && o.LocY == nearY &&
                        (o.LocX == fort.LocX + 16 || o.LocX == fort.LocX + 112)).Select(o => o.Type).ToArray();
                    Check(front.Length == 2 && front.Contains("missile_cannon") && front.Contains("howitzer"), "Strongest guns missing from the entrance.");
                }
            }
            r.Menus.ShowInGameMenu();
            yield return null;
            Shot("pause-menu");
            r.Menus.CloseOverlay();
            if (host)
            {
                r.Menus.ShowDiagnosticsProof();
                yield return null;
                Shot("network-turn-allowance");
                r.Menus.CloseOverlay();
            }
            foreach (var step in Sync("pause-menu")) yield return step;
            if (!host) r.RequestMapChange();
            foreach (var step in WaitFor(() => r.CurrentMatchRequest == DuelRuntime.MatchRequestKind.Map, "guest map request")) yield return step;
            yield return new WaitForSecondsRealtime(.25f);
            double tick = r.Game.World.Time;
            yield return new WaitForSecondsRealtime(.4f);
            Check(r.Game.World.Time == tick, "Simulation did not pause.");
            r.SendChat(host ? "Let's play the jungle next." : "Can we try another map?");
            if (!host) r.SelectMatchMap("L16");
            foreach (var step in WaitFor(() => r.MatchMap == "L16" && r.Chat.Count >= 2, "shared map and chat")) yield return step;
            yield return null;
            Shot("map-choice");
            foreach (var step in Sync("map-choice")) yield return step;
            if (!host) Click("SUGGEST MAP");
            foreach (var step in WaitFor(() => r.MatchMapSuggested, "locked map suggestion")) yield return step;
            yield return null;
            Shot("map-suggested");
            foreach (var step in Sync("map-suggested")) yield return step;
            if (host) Click("DECLINE");
            foreach (var step in WaitFor(() => !r.MatchSuspended && r.Game.World.Time > tick, "resume after decline")) yield return step;
            yield return null;
            Shot("map-declined");
            foreach (var step in Sync("map-declined")) yield return step;
            if (host) { r.RequestMapChange(); r.SelectMatchMap("L16"); r.SuggestMatchMap(); }
            foreach (var step in WaitFor(() => r.MatchMapSuggested, "host map suggestion")) yield return step;
            yield return null;
            if (!host) Click("ACCEPT");
            foreach (var step in WaitFor(() => r.Playing && r.Game && r.View.Map == "L16" && r.Game.World.Time > 1, "replacement map", 70)) yield return step;
            Check(r.Rules.TankDamageEffects && r.Rules.RadarLoss == RadarLossRule.FogAndZoom &&
                r.Rules.FortTurretsFor(0).Length == 4, "Frozen rules changed during map replacement.");
            if (host)
            {
                var world = r.Game.World;
                var fort = world.Objects.First(o => o.IsFort && o.Owner == Team.Red);
                var camera = Camera.main.GetComponent<RtsCamera>();
                var radar = world.Objects.FirstOrDefault(o => o.Type == "radar" && !o.Destroyed);
                Check(radar != null, "No radar for the loss demonstration.");
                radar.Owner = Team.Red;
                var tank = world.AddHeadStart(Team.Red, "light").Single();
                r.Game.SpawnUnitView(tank);
                tank.SetHealth(Math.Max(1, tank.MaxHealth / 8));
                int tx = tank.CenterX / ZConst.TILE, ty = tank.CenterY / ZConst.TILE;
                var directions = new[] { (x: 1, y: 0), (x: -1, y: 0), (x: 0, y: 1), (x: 0, y: -1) };
                int Reach(int x, int y)
                {
                    int tiles = 1;
                    while (tiles < 13 && world.Path.TilePassable(tx + x * tiles, ty + y * tiles, false)) tiles++;
                    return tiles - 1;
                }
                var direction = directions.OrderByDescending(d => Reach(d.x, d.y)).First();
                int distance = Reach(direction.x, direction.y) * ZConst.TILE;
                Check(distance >= 96 && world.SendWaypoints(Team.Red, tank,
                    new[] { ZWaypoint.Move(tank.CenterX + direction.x * distance, tank.CenterY + direction.y * distance) }), "Cannot order the damaged tank to move.");
                camera.focus = r.Game.CenterWorld(tank);
                camera.SetHeightImmediate(35);
                foreach (var step in WaitFor(() => r.Game.GetComponent<OzCripplingTrails>()?.Emitted >= 14, "fourteen oil marks from the moving tank", 15)) yield return step;
                var end = r.Game.CenterWorld(tank);
                camera.focus = end - new Vector3(direction.x * 1.4f, 0, -direction.y * 1.4f);
                camera.SetHeightImmediate(35);
                r.Game.GetComponent<OpenZHud>()?.Toast("RED HEALTH · SMOKE AND OIL", OzUi.Amber, 2f);
                yield return null;
                WorldShot("damaged-tanks");
                camera.focus = Vector3.Lerp(r.Game.CenterWorld(fort), ZGame.PxToWorld(world.Map.WidthPix / 2, world.Map.HeightPix / 2), .6f);
                camera.SetHeightImmediate(70);
                WorldShot("radar-ground-before");
            }
            yield return new WaitForSecondsRealtime(1.2f);
            Shot("replacement-map");
            foreach (var step in Sync("replacement-map")) yield return step;
            if (host)
            {
                var world = r.Game.World;
                foreach (var radar in world.Objects.Where(o => o.Type == "radar" && o.Owner == Team.Red && !o.Destroyed).ToArray())
                { radar.SetHealth(0); world.UpdateObjectHealth(radar); }
                yield return null;
                WorldShot("radar-static");
                yield return new WaitForSecondsRealtime(1f);
                WorldShot("radar-fog-zoom");
                WorldShot("radar-ground-after");
                yield return null;
                r.ProofInterruptMatch();
            }
            foreach (var step in WaitFor(() => r.ConnectionInterrupted, "connection interruption", 13)) yield return step;
            yield return null;
            Shot("reconnect-countdown");
            foreach (var step in WaitFor(() => r.Playing && r.Connected && !r.MatchSuspended, "reconnection and full state sync", 34)) yield return step;
            yield return null;
            Shot("reconnected");
            foreach (var step in Sync("reconnected")) yield return step;
            if (!host) r.RequestLobbyReturn();
            foreach (var step in WaitFor(() => r.CurrentMatchRequest == DuelRuntime.MatchRequestKind.Lobby, "return request")) yield return step;
            yield return null;
            Shot("return-to-lobby");
            foreach (var step in Sync("return-to-lobby")) yield return step;
            if (host) Click("ACCEPT");
            foreach (var step in WaitFor(() => r.View?.Phase == (int)DuelPhase.Lobby && SceneManager.GetActiveScene().name == "MainMenu", "lobby return", 30)) yield return step;
            yield return new WaitForSecondsRealtime(.4f);
            Shot("returned");
        }
        IEnumerable Sync(string shot) => WaitFor(() => File.Exists(FileFor(Other, shot, ".png")), shot + " on both sides", 30);
        IEnumerable WaitFor(Func<bool> ready, string message, double seconds = 12)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready())
            {
                Check(Time.realtimeSinceStartupAsDouble < deadline, "Timed out waiting for " + message + ".");
                yield return null;
            }
        }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Click(string name)
        {
            var button = Runtime.Menus.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name);
            Check(button && button.interactable, "Missing or disabled button: " + name);
            button.onClick.Invoke();
        }
        void Shot(string name) => DuelProofCapture.Save(FileFor(role, name, ".png"), !Runtime.Playing || name != "replacement-map");
        void WorldShot(string name) => DuelProofCapture.Save(FileFor(role, name, ".png"));
    }
}
