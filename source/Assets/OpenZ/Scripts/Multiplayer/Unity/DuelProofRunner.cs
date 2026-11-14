using System;
using System.Collections;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in executable integration proof; inactive in ordinary games.</summary>
    public sealed class DuelProofRunner : MonoBehaviour
    {
        string role, folder;
        double began;
        bool joined, ordered, surrendered, finished, captured, lobbyCaptured, menuShown, menuCaptured, menuClosed, arenaMade, damaged;
        double connectedAt = -1, lastVoice;
        int exceptions;
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
                        r.Host("HOTE", 27889);
                        var args = Environment.GetCommandLineArgs(); int mapOption = Array.IndexOf(args, "-oz-duel-proof-map");
                        if (mapOption >= 0 && mapOption + 1 < args.Length) r.ChangeRules(DuelRuntime.Defaults(args[mapOption + 1]));
                        File.WriteAllText(Path.Combine(folder, "invitation.txt"), r.Invitation("127.0.0.1", 27889));
                        joined = true;
                    }
                    else if (File.Exists(Path.Combine(folder, "invitation.txt")))
                    { r.Join("INVITE", File.ReadAllText(Path.Combine(folder, "invitation.txt"))); joined = true; }
                }
                if (r.Connected && r.View != null && r.View.Phase == (int)DuelPhase.Lobby)
                {
                    if (connectedAt < 0) connectedAt = Time.realtimeSinceStartupAsDouble;
                    double elapsed = Time.realtimeSinceStartupAsDouble - connectedAt;
                    if (!lobbyCaptured && elapsed > 1) { DuelProofCapture.Save(Path.Combine(folder, role + "-lobby.png"), true); lobbyCaptured = true; }
                    if (elapsed > 2 && (role == "host" ? !r.View.HostReady : !r.View.GuestReady)) r.Ready();
                }
                if (r.Playing && r.Game)
                {
                    var w = r.Game.World;
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
                    if (!menuCaptured && w.Time > 8) { DuelProofCapture.Save(Path.Combine(folder, role + "-menu.png"), true); menuCaptured = true; }
                    if (!menuClosed && w.Time > 9) { r.Menus.CloseOverlay(); menuClosed = true; }
                    if (w.Time > 4 && w.Time < 12 && Time.realtimeSinceStartupAsDouble - lastVoice > .02)
                    {
                        lastVoice = Time.realtimeSinceStartupAsDouble;
                        // A low-volume synthetic frame tests UDP and playback without opening a microphone.
                        var tone = new float[DuelVoiceCodec.FrameSamples];
                        for (int i = 0; i < tone.Length; i++) tone[i] = .005f * Mathf.Sin(2 * Mathf.PI * 440 * i / DuelVoiceCodec.SampleRate);
                        r.Voice.SendProofFrame(tone);
                    }
                    if (!captured && w.Time > 13)
                    {
                        DuelProofCapture.Save(Path.Combine(folder, role + "-game.png")); captured = true;
                    }
                    if (role == "guest" && !surrendered && w.Time > 20) { r.Surrender(); surrendered = true; }
                    if (w.GameOver)
                    {
                        bool blueFortGone = w.Objects.Where(o => o.IsFort && o.Owner == Team.Blue).All(o => o.Destroyed);
                        Finish(w.Winner == Team.Red && blueFortGone && exceptions == 0 && damaged && r.Voice.FramesReceived > 0 && (role == "host" || ordered),
                            "winner=" + w.Winner + "; blueFortDestroyed=" + blueFortGone + "; combatDamage=" + damaged + "; voiceFrames=" + r.Voice.FramesReceived + "; objects=" + w.Objects.Count + "; exceptions=" + exceptions);
                    }
                }
                if (Time.realtimeSinceStartupAsDouble - began > 240) Finish(false, "Timeout; " + r.Status + "; exceptions=" + exceptions);
            }
            catch (Exception error) { Finish(false, error.ToString()); }
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
