using System;
using System.Collections;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in executable integration proof; inactive in ordinary games.</summary>
    public sealed class DuelProofRunner : MonoBehaviour
    {
        string role, folder;
        double began;
        bool joined, ordered, surrendered, finished, captured, lobbyCaptured, menuShown, menuCaptured, menuClosed, arenaMade, damaged;
        bool forceProofStarted, forceProofDone;
        double connectedAt = -1, lastVoice;
        int exceptions;
        bool lobbyVoice, chatSent, chatOpened, chatClosed, chatExpired;
        double noticeSeenAt = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-oz-duel-proof");
            if (option < 0 || option + 2 >= args.Length) return;
            var go = new GameObject("DuelProof"); DontDestroyOnLoad(go);
            var proof = go.AddComponent<DuelProofRunner>(); proof.role = args[option + 1]; proof.folder = args[option + 2];
            proof.began = Time.realtimeSinceStartupAsDouble;
            Application.logMessageReceived += proof.OnLog;
        }
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Exception) exceptions++; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void Update()
        {
            if (finished || string.IsNullOrEmpty(role)) return;
            try
            {
                var r = DuelRuntime.I; if (!r) return;
                if (!joined)
                {
                    Directory.CreateDirectory(folder);
                    if (role == "host")
                    {
                        if (Environment.GetCommandLineArgs().Contains("-oz-duel-online-proof")) r.ConnectOnline("HOST", true, "");
                        else r.Host("HOST", 27889);
                        var args = Environment.GetCommandLineArgs(); int mapOption = Array.IndexOf(args, "-oz-duel-proof-map");
                        if (mapOption >= 0 && mapOption + 1 < args.Length) r.ChangeRules(DuelRuntime.Defaults(args[mapOption + 1]));
                        if (!r.OnlineMode) File.WriteAllText(Path.Combine(folder, "invitation.txt"), r.Invitation("127.0.0.1", 27889));
                        joined = true;
                    }
                    else if (File.Exists(Path.Combine(folder, "invitation.txt")))
                    {
                        string invitation = File.ReadAllText(Path.Combine(folder, "invitation.txt"));
                        if (Environment.GetCommandLineArgs().Contains("-oz-duel-online-proof")) r.ConnectOnline("GUEST", false, invitation);
                        else r.Join("GUEST", invitation);
                        joined = true;
                    }
                }
                if (role == "host" && r.OnlineMode && !string.IsNullOrEmpty(r.OnlineRoom) && !File.Exists(Path.Combine(folder, "invitation.txt")))
                    File.WriteAllText(Path.Combine(folder, "invitation.txt"), r.Invitation("", 0));
                if (r.Connected && r.View != null && r.View.Phase == (int)DuelPhase.Lobby)
                {
                    if (Environment.GetCommandLineArgs().Contains("-oz-duel-relay-only") && (r.OnlinePeer == null || !r.OnlinePeer.UsesRelay)) throw new Exception("Forced relay proof selected a direct path.");
                    if (connectedAt < 0) connectedAt = Time.realtimeSinceStartupAsDouble;
                    double elapsed = Time.realtimeSinceStartupAsDouble - connectedAt;
                    SendVoiceProof(r);
                    if (r.Voice.FramesReceived > 0) lobbyVoice = true;
                    if (!lobbyCaptured && elapsed > 1) { DuelProofCapture.Save(Path.Combine(folder, role + "-lobby.png"), true); lobbyCaptured = true; }
                    if (elapsed > 4 && (role == "host" ? !r.View.HostReady : !r.View.GuestReady))
                    { if (!lobbyVoice) throw new Exception("Voice was not received in the lobby."); r.Ready(); }
                }
                if (r.Playing && r.Game)
                {
                    var w = r.Game.World;
                    if (!chatSent && w.Time > 2) { r.SendChat("Hello from " + role); chatSent = true; }
                    var notice = r.Menus.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Incoming chat");
                    if (notice && noticeSeenAt < 0) noticeSeenAt = Time.realtimeSinceStartupAsDouble;
                    if (!chatOpened && w.Time > 3.5)
                    {
                        if (!notice || !r.Chat.Any(c => c.Contains("Hello from " + (role == "host" ? "guest" : "host")))) throw new Exception("Incoming text was not shown without opening chat.");
                        DuelProofCapture.Save(Path.Combine(folder, role + "-chat-notice.png"), true);
                        notice.onClick.Invoke(); chatOpened = true;
                    }
                    if (chatOpened && !chatClosed && w.Time > 4.5)
                    {
                        if (!r.Menus.GetComponentsInChildren<InputField>().Any()) throw new Exception("Clicking incoming text did not open chat.");
                        r.Menus.CloseOverlay(); chatClosed = true;
                    }
                    if (!chatExpired && noticeSeenAt >= 0 && Time.realtimeSinceStartupAsDouble > noticeSeenAt + 10.5)
                    { if (notice) throw new Exception("Incoming text did not expire after ten seconds."); chatExpired = true; }
                    if (w.Bots.Count != 0 || r.Game.blueBot || r.Game.redBot) throw new Exception("A CPU controls a human duel seat.");
                    if (role == "host" && !arenaMade && w.Time > 1)
                    {
                        CreateArena(w, r.Game); arenaMade = true;
                    }
                    if (role == "guest" && !ordered && w.Time > 2)
                    {
                        var unit = w.Objects.First(o => o.Owner == Team.Blue && o.IsMobile && !o.IsMinion);
                        bool accepted = w.SendWaypoints(Team.Blue, unit, new[] { ZWaypoint.Move(Math.Min(w.Map.WidthPix - 32, unit.CenterX + 100), unit.CenterY) });
                        if (!accepted) throw new Exception("Guest order was not forwarded.");
                        var fort = w.Objects.First(o => o.Owner == Team.Blue && o.IsProducer);
                        w.AddBuildingQueue(Team.Blue, fort, "grunt"); ordered = true;
                        Debug.Log("[DUEL PROOF] Guest move and factory commands forwarded.");
                    }
                    var combatants = w.Objects.Where(o => o.BuiltCount == 12026).ToArray();
                    if (!w.GameOver && combatants.Any(o => o.Health < o.MaxHealth)) damaged = true;
                    if (combatants.Length > 0 && !menuShown)
                    {
                        var camera = FindFirstObjectByType<RtsCamera>();
                        if (camera) { camera.focus = ZGame.PxToWorld(combatants[0].CenterX, combatants[0].CenterY); camera.SetHeightImmediate(30); }
                    }
                    if (!menuShown && w.Time > 7) { r.Menus.ShowInGameMenu(); menuShown = true; }
                    if (!menuCaptured && w.Time > 8)
                    {
                        var pause = FindFirstObjectByType<OpenZPauseMenu>();
                        if (!pause || !pause.IsOpen || r.Game.paused || Time.timeScale != 1) throw new Exception("Native options menu paused or replaced the duel.");
                        foreach (var field in new[] { "bOptions", "bControls", "bReport" })
                            if (typeof(OpenZPauseMenu).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause) == null) throw new Exception("Missing normal menu option: " + field);
                        for (int tab = 0; tab < OzSettingTips.TabNames.Length; tab++) pause.ProofOptTab(tab);
                        pause.ProofOptTab(0);
                        DuelProofCapture.Save(Path.Combine(folder, role + "-menu.png"), true); menuCaptured = true;
                    }
                    if (!menuClosed && w.Time > 9) { r.Menus.CloseOverlay(); menuClosed = true; }
                    if (w.Time > 4 && w.Time < 12 && Time.realtimeSinceStartupAsDouble - lastVoice > .02)
                    {
                        lastVoice = Time.realtimeSinceStartupAsDouble;
                        // A low-volume synthetic frame tests the active voice transport without opening a microphone.
                        var tone = new float[DuelVoiceCodec.FrameSamples];
                        for (int i = 0; i < tone.Length; i++) tone[i] = .005f * Mathf.Sin(2 * Mathf.PI * 440 * i / DuelVoiceCodec.SampleRate);
                        r.Voice.SendProofFrame(tone);
                    }
                    if (!captured && w.Time > 13)
                    {
                        DuelProofCapture.Save(Path.Combine(folder, role + "-game.png")); captured = true;
                    }
                    if (!forceProofStarted && w.Time > 14)
                    { forceProofStarted = true; StartCoroutine(DuelForceBarProof.Run(folder, role, () => forceProofDone = true)); }
                    if (role == "guest" && !surrendered && forceProofDone && w.Time > 20) { r.Surrender(); surrendered = true; }
                    if (w.GameOver)
                    {
                        bool blueFortGone = w.Objects.Where(o => o.IsFort && o.Owner == Team.Blue).All(o => o.Destroyed);
                        Finish(w.Winner == Team.Red && blueFortGone && exceptions == 0 && damaged && forceProofDone && lobbyVoice && chatOpened && chatClosed && chatExpired && r.Voice.FramesReceived > 0 && (role == "host" || ordered),
                            "winner=" + w.Winner + "; blueFortDestroyed=" + blueFortGone + "; combatDamage=" + damaged + "; lobbyVoice=" + lobbyVoice + "; chatClick=" + chatOpened + "; chatExpiry=" + chatExpired + "; voiceFrames=" + r.Voice.FramesReceived + "; objects=" + w.Objects.Count + "; exceptions=" + exceptions + "; transport=" + (r.OnlineMode ? (r.OnlinePeer.UsesRelay ? "TURN" : "WebRTC direct") : "TCP"));
                    }
                }
                if (Time.realtimeSinceStartupAsDouble - began > 240) Finish(false, "Timeout; " + r.Status + "; exceptions=" + exceptions);
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        void SendVoiceProof(DuelRuntime runtime)
        {
            if (Time.realtimeSinceStartupAsDouble - lastVoice < .02) return;
            lastVoice = Time.realtimeSinceStartupAsDouble;
            var tone = new float[DuelVoiceCodec.FrameSamples];
            for (int i = 0; i < tone.Length; i++) tone[i] = .005f * Mathf.Sin(2 * Mathf.PI * 440 * i / DuelVoiceCodec.SampleRate);
            runtime.Voice.SendProofFrame(tone);
        }
        static void CreateArena(ZWorld world, ZGame game)
        {
            ZObject a = null, b = null;
            for (int y = 4; y < world.Map.height - 5 && b == null; y++)
                for (int x = 4; x < world.Map.width - 12 && b == null; x++)
                {
                    bool clear = true;
                    for (int yy = y; yy < y + 3 && clear; yy++)
                        for (int xx = x; xx < x + 9; xx++)
                            if (!world.Path.TilePassable(xx, yy, false) || world.Map.ClassAt(xx, yy) == ZClass.Water || world.Map.ClassAt(xx, yy) == ZClass.DeepWater) { clear = false; break; }
                    if (!clear || world.Objects.Any(o => o.IsUnit && Math.Abs(o.CenterX - (x + 4) * 16) < 100 && Math.Abs(o.CenterY - (y + 1) * 16) < 64)) continue;
                    a = world.CreateObject("vehicle", "light", x * 16, y * 16, Team.Red);
                    b = world.CreateObject("vehicle", "light", (x + 6) * 16, y * 16, Team.Blue);
                    a.BuiltCount = b.BuiltCount = 12026;
                    game.SpawnUnitView(a); game.SpawnUnitView(b);
                }
            if (a == null || b == null) throw new Exception("No free space for combat proof.");
            world.SendWaypoints(a.Owner, a, new[] { new ZWaypoint { mode = WpMode.Attack, refId = b.Id, x = b.CenterX, y = b.CenterY } });
            world.SendWaypoints(b.Owner, b, new[] { new ZWaypoint { mode = WpMode.Attack, refId = a.Id, x = a.CenterX, y = a.CenterY } });
        }
        void Finish(bool passed, string detail)
        {
            if (finished) return; finished = true;
            File.WriteAllText(Path.Combine(folder, role + "-result.txt"), (passed ? "PASS" : "FAIL") + "\n" + detail);
            Debug.Log("[DUEL PROOF] " + role + ": " + (passed ? "PASS " : "FAIL ") + detail);
            StartCoroutine(Exit(passed));
        }
        IEnumerator Exit(bool passed)
        {
            yield return new WaitForSecondsRealtime(1);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
