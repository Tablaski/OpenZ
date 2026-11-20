using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        bool radarVideo;
        int videoFrame;
        DuelRuntime Runtime => DuelRuntime.I;
        string Other => host ? "guest" : "host";
        string FileFor(string owner, string name, string ext) => Path.Combine(folder, owner + "-" + name + ext);
        internal void Begin(string roleName, string directory, bool useOnline)
        {
            folder = directory; role = roleName; host = role == "host"; online = useOnline;
            radarVideo = Environment.GetCommandLineArgs().Contains("-oz-duel-radar-video");
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
            if (!done) Finish("PASS: manual pause from both seats, confirmation and ownership, frozen time and inputs, menus, chat and voice, fort placement, map changes, reconnection while paused and lobby return.");
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
                rules.Common.StartingUnits[0] = "light";
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
            foreach (var step in CheckManualPause(true)) yield return step;
            foreach (var step in CheckManualPause(false)) yield return step;
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
            int borderX = 0, borderY = 0;
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
                r.Game.GetComponent<OpenZHud>()?.Toast("CRITICAL HEALTH · OIL TRAILS", OzUi.Amber, 2f);
                yield return null;
                WorldShot("damaged-tanks");
                var owned = world.Zones.Single(zone => zone.Owner == Team.Red && zone.Contains(fort.CenterX, fort.CenterY));
                var unowned = world.Zones.Single(zone => zone.Owner == Team.Neutral && zone.Y + zone.H == owned.Y &&
                    zone.X < fort.CenterX && fort.CenterX < zone.X + zone.W);
                borderX = (Math.Max(owned.X, unowned.X) + Math.Min(owned.X + owned.W, unowned.X + unowned.W)) / 2;
                borderY = owned.Y;
                if (radarVideo) { world.SendStop(Team.Red, tank); tank.Owner = Team.Neutral; }
            }
            yield return new WaitForSecondsRealtime(1.2f);
            Shot("replacement-map");
            foreach (var step in Sync("replacement-map")) yield return step;
            if (host)
            {
                var world = r.Game.World;
                var camera = Camera.main.GetComponent<RtsCamera>();
                camera.focus = ZGame.PxToWorld(borderX, borderY);
                camera.pitch = 89;
                camera.SetHeightImmediate(70);
                WorldShot("radar-frontier-before");
                ZObject[] enemy = null;
                ZObject scout = null;
                if (radarVideo)
                {
                    camera.focus = ZGame.PxToWorld(borderX, borderY - 3 * ZConst.TILE);
                    camera.SetHeightImmediate(70);
                    scout = DemoUnit(r.Game, "vehicle", "jeep", 51, 105, Team.Red);
                    enemy = Enumerable.Range(50, 3).Select(x => DemoUnit(r.Game, "robot", "grunt", x, 93, Team.Blue)).ToArray();
                    RadarCaption("RADAR ACTIVE · ENEMY CONTACTS");
                    foreach (var frame in CaptureRadarFrames(2.1)) yield return frame;
                }
                foreach (var radar in world.Objects.Where(o => o.Type == "radar" && o.Owner == Team.Red && !o.Destroyed).ToArray())
                { radar.SetHealth(0); world.UpdateObjectHealth(radar); }
                yield return null;
                WorldShot("radar-static");
                if (radarVideo) { foreach (var frame in CaptureRadarFrames(1.3)) yield return frame; }
                else yield return new WaitForSecondsRealtime(1f);
                WorldShot("radar-fog-zoom");
                var loss = OzRadarLoss.I;
                Check(loss && loss.Fog && loss.Visible(borderX, borderY + 112) && !loss.Visible(borderX, borderY - 176),
                    "Owned ground and neutral ground were not on opposite sides of the fog frontier.");
                camera.focus = ZGame.PxToWorld(borderX, borderY);
                camera.SetHeightImmediate(70);
                WorldShot("radar-frontier-after");
                if (radarVideo)
                {
                    camera.focus = ZGame.PxToWorld(borderX, borderY - 3 * ZConst.TILE);
                    camera.SetHeightImmediate(70);
                    foreach (var frame in CaptureRadarReveal(world, scout, enemy, loss, camera)) yield return frame;
                }
                yield return null;
                r.RequestPauseMatch();
            }
            foreach (var step in WaitFor(() => r.MatchPaused, "pause before interruption", radarVideo ? 85 : 13)) yield return step;
            Shot("paused-before-interruption");
            foreach (var step in Sync("paused-before-interruption")) yield return step;
            if (host) r.ProofInterruptMatch();
            foreach (var step in WaitFor(() => r.ConnectionInterrupted, "connection interruption", radarVideo ? 85 : 13)) yield return step;
            yield return null;
            Shot("reconnect-countdown");
            foreach (var step in WaitFor(() => r.Playing && r.Connected && !r.ConnectionInterrupted, "reconnection and full state sync", 34)) yield return step;
            Check(r.MatchPaused && r.OwnsMatchPause == host, "Reconnection changed the manual pause or its owner.");
            yield return null;
            Shot("reconnected");
            foreach (var step in Sync("reconnected")) yield return step;
            if (host) r.ResumePausedMatch();
            foreach (var step in WaitFor(() => !r.MatchSuspended, "resume after reconnection")) yield return step;
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

        IEnumerable CheckManualPause(bool hostPauses)
        {
            var r = Runtime;
            string label = hostPauses ? "host-manual-pause" : "guest-manual-pause";
            bool owner = host == hostPauses;
            if (owner)
            {
                r.Menus.ShowInGameMenu();
                yield return null;
                var menu = FindFirstObjectByType<OpenZPauseMenu>();
                var pauseButton = typeof(OpenZPauseMenu).GetField("bPauseMatch", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(menu) as HudButton;
                Check(menu && menu.IsOpen && pauseButton != null && pauseButton.Interactable, "The match menu did not offer pause.");
                pauseButton.OnClick(default);
                Check(!OzConfirm.IsOpen && !menu.IsOpen, "Clicking Pause Match did not directly request the pause.");
                if (host) Check(r.MatchPaused, "The host did not pause immediately.");
            }
            foreach (var step in WaitFor(() => r.MatchPaused, label)) yield return step;
            yield return null;
            Check(Time.timeScale == 0 && r.OwnsMatchPause == owner && r.MatchPausePlayer == (hostPauses ? "HOST" : "GUEST"), "The pause was not synchronized to the correct player.");
            var world = r.Game.World;
            var camera = FindFirstObjectByType<RtsCamera>();
            var focus = camera.focus;
            var position = camera.transform.position;
            double time = world.Time;
            var unit = world.Objects.First(o => o.IsUnit && o.IsMobile && o.Owner == r.LocalTeam);
            var fort = world.Objects.First(o => o.IsFort && o.Owner == r.LocalTeam);
            camera.DragPixels(new Vector2(120, -50));
            camera.ZoomBy(.5f);
            camera.SetHeightImmediate(20);
            Check(RtsCamera.InputLocked && !world.SendWaypoints(r.LocalTeam, unit, new[] { ZWaypoint.Move(unit.CenterX + 64, unit.CenterY) }) &&
                !world.AddBuildingQueue(r.LocalTeam, fort, "grunt"), "A gameplay input was accepted during pause.");
            if (!owner) r.ResumePausedMatch();
            yield return new WaitForSecondsRealtime(.25f);
            Check(r.MatchPaused && world.Time == time && camera.focus == focus && camera.transform.position == position, "Camera, time or pause ownership changed while paused.");
            Check(r.Menus.GetComponentsInChildren<Button>().Any(b => b.name == "RESUME MATCH") == owner, "Resume button has the wrong owner.");
            Shot(label);

            Click("MATCH MENU");
            yield return null;
            var nativeMenu = FindFirstObjectByType<OpenZPauseMenu>();
            Check(nativeMenu && nativeMenu.IsOpen, "Match menu is inaccessible while paused.");
            nativeMenu.OpenOptions(OpenZPauseMenu.MicrophoneTab);
            yield return null;
            Check(nativeMenu.IsOpen, "Microphone menu is inaccessible while paused.");
            r.Menus.CloseOverlay();
            yield return null;
            Click("CHAT");
            yield return null;
            Check(r.Menus.GetComponentsInChildren<InputField>().Any(), "Chat is inaccessible while paused.");
            r.SendChat(label + " " + role);
            int received = r.Voice.FramesReceived;
            var tone = new float[DuelVoiceCodec.FrameSamples];
            for (int i = 0; i < tone.Length; i++) tone[i] = .005f * Mathf.Sin(2 * Mathf.PI * 440 * i / DuelVoiceCodec.SampleRate);
            foreach (var step in WaitFor(() =>
            {
                r.Voice.SendProofFrame(tone);
                return r.Voice.FramesReceived > received && r.Chat.Any(line => line.Contains(label + " " + Other));
            }, "chat and voice while paused")) yield return step;
            Check(r.MatchPaused && world.Time == time, "Communication advanced match time.");
            r.Menus.CloseOverlay();
            yield return null;
            Shot(label + "-communication");
            foreach (var step in Sync(label + "-communication")) yield return step;
            if (owner) Click("RESUME MATCH");
            foreach (var step in WaitFor(() => !r.MatchPaused && world.Time > time, "authoritative resume")) yield return step;
            Shot(label + "-resumed");
            foreach (var step in Sync(label + "-resumed")) yield return step;
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

        static ZObject DemoUnit(ZGame game, string kind, string type, int x, int y, Team team)
        {
            var unit = game.World.CreateObject(kind, type, x * ZConst.TILE, y * ZConst.TILE, team);
            unit.Damage = 0;
            game.SpawnUnitView(unit);
            return unit;
        }
        static void RadarCaption(string text) => OpenZHud.I.Toast(text, OzUi.Amber, 4f);
        IEnumerable CaptureRadarReveal(ZWorld world, ZObject scout, ZObject[] enemies, OzRadarLoss loss, RtsCamera camera)
        {
            Check(enemies.All(o => OzRadarLoss.Covers(o) && loss.Contacts.ContainsKey(o.Id)), "Enemy robots have no last known contacts.");
            var remembered = enemies.Select(o => loss.Contacts[o.Id].Y).ToArray();
            RadarCaption("ENEMY ROBOTS · LAST KNOWN POSITIONS");
            foreach (var enemy in enemies)
                Check(world.SendWaypoints(Team.Blue, enemy, new[] { ZWaypoint.Move(enemy.CenterX, 88 * ZConst.TILE) }), "Robot movement refused.");
            foreach (var frame in CaptureRadarFrames(4.5)) yield return frame;
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                Check(OzRadarLoss.Covers(enemy) && Math.Abs(enemy.CenterY - remembered[i]) >= ZConst.TILE &&
                    loss.Contacts.TryGetValue(enemy.Id, out var contact) && contact.Y == remembered[i], "A hidden robot exposed its live position.");
            }
            RadarCaption("ALLY ADVANCING · VISION BEYOND THE BORDER");
            Check(world.SendWaypoints(Team.Red, scout, new[] { ZWaypoint.Move(scout.CenterX, 89 * ZConst.TILE) }), "Scout movement refused.");
            var startingFocus = camera.focus;
            var cameraTravel = ZGame.PxToWorld(0, -3 * ZConst.TILE);
            void FollowScout()
            {
                float advance = Mathf.InverseLerp(104 * ZConst.TILE, 99 * ZConst.TILE, scout.CenterY);
                camera.focus = startingFocus + cameraTravel * Mathf.SmoothStep(0, 1, advance);
            }
            bool EnemiesRevealed() => enemies.All(o => !o.Destroyed && !OzRadarLoss.Covers(o));
            foreach (var frame in CaptureRadarFrames(11, EnemiesRevealed, FollowScout)) yield return frame;
            Check(EnemiesRevealed(), "The scout did not reveal the enemy robots.");
            RadarCaption("ENEMY ROBOTS REVEALED");
            foreach (var frame in CaptureRadarFrames(3.2)) yield return frame;
            WorldShot("radar-robots-revealed");
            File.WriteAllText(Path.Combine(folder, "radar-video-proof.txt"), "PASS: hidden robots moved without updating their last known positions; the allied scout revealed all three.");
        }
        IEnumerable CaptureRadarFrames(double duration, Func<bool> stop = null, Action beforeFrame = null)
        {
            var destination = Path.Combine(folder, "radar-video-frames");
            Directory.CreateDirectory(destination);
            double end = Time.realtimeSinceStartupAsDouble + duration;
            do
            {
                beforeFrame?.Invoke();
                DuelProofCapture.Save(Path.Combine(destination, $"frame-{videoFrame++:D4}.jpg"));
                yield return new WaitForSecondsRealtime(.065f);
            } while (Time.realtimeSinceStartupAsDouble < end && (stop == null || !stop()));
        }
    }
}
