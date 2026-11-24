using System;
using System.Collections;
using System.IO;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Opt-in recording of live duels in the compiled Unit Editor.</summary>
    public sealed class ZClassicDuelCapture : MonoBehaviour
    {
        const int Fps = 30, Repeats = 3, TimeoutSeconds = 20;
        static readonly string[] Tanks = { "light", "medium", "heavy" };
        static readonly (int first, int second)[] Pairs = { (0, 0), (0, 1), (0, 2), (1, 1), (1, 2), (2, 2) };
        string folder, encoder;
        OzUnitEditor editor;
        ZObject left, right;
        int trial = -1, startFrame, resultFrame;
        double startedAt;
        bool done;
        string title, result;
        GUIStyle heading, stats;
        Vector3 centre;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-oz-zclassic-duels");
            if (option < 0 || option + 2 >= args.Length) return;
            var recorder = new GameObject(nameof(ZClassicDuelCapture)).AddComponent<ZClassicDuelCapture>();
            DontDestroyOnLoad(recorder.gameObject);
            recorder.folder = args[option + 1];
            recorder.encoder = args[option + 2];
            Application.runInBackground = true;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (!OzUnitEditor.UiBuilt && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!OzUnitEditor.UiBuilt || !File.Exists(encoder)) { Finish("FAIL: Unit Editor or FFmpeg unavailable"); yield break; }
            editor = OzUnitEditor.I;
            var world = editor.World;
            world.SetBehavior(ZBehaviorProfile.Classic(ZBehaviorFamily.Tanks));
            editor.Game.paused = true;
            editor.Game.gameSpeed = 1f;
            centre = ZGame.PxToWorld(world.Map.WidthPix / 2, world.Map.HeightPix / 2);
            editor.Rts.focus = centre;
            editor.Rts.pitch = 53f;
            editor.Rts.yaw = 0f;
            editor.Rts.SetHeightImmediate(29f);
            for (int frame = 0; frame < 8; frame++) yield return null;

            File.WriteAllText(Path.Combine(folder, "duels.tsv"), "duel\ttrial\tleft\tright\tseed\tvideo_seconds\tsimulation_seconds\tleft_hp\tright_hp\tresult\n");
            OzFowClipRecorder.FfmpegPath = encoder;
            var recording = OzFowClipRecorder.Begin(Path.Combine(folder, "OpenZ-VK-Z-Classic-Duels.mp4"),
                Pairs.Length * Repeats * (TimeoutSeconds + 4) * Fps, Fps, 19, Frame, () => done);
            if (!recording) { Finish("FAIL: Could not start capture"); yield break; }
            while (recording && recording.Running) yield return null;
            if (!done || !recording.Result.StartsWith("OK", StringComparison.Ordinal)) Finish("FAIL: " + recording.Result);
            else Finish("PASS: 18 duels. " + recording.Result);
        }

        void Frame(int frame)
        {
            if (trial < 0 || (result != null && frame - resultFrame >= 45 && frame - startFrame >= 135))
            {
                if (++trial == Pairs.Length * Repeats) { done = true; return; }
                Stage(frame);
            }
            int elapsed = frame - startFrame;
            if (elapsed == Fps) { Attack(left, right); Attack(right, left); }
            if (elapsed >= Fps && result == null && (left.Destroyed || right.Destroyed || elapsed >= TimeoutSeconds * Fps))
            {
                result = left.Destroyed ? (right.Destroyed ? "Draw" : "Red wins") : right.Destroyed ? "Blue wins" : "Time limit";
                resultFrame = frame;
                File.AppendAllText(Path.Combine(folder, "duels.tsv"), FormattableString.Invariant(
                    $"{trial + 1}\t{trial % Repeats + 1}\t{left.Type}\t{right.Type}\t{Seed}\t{startFrame / (double)Fps:F2}\t{editor.World.Time - startedAt:F2}\t{left.Health}\t{right.Health}\t{result}\n"));
            }
        }

        int Seed => 7183 + trial * 983;

        void Stage(int frame)
        {
            OzUePlayground.ClearField();
            var world = editor.World;
            world.Rng = new ZRng(Seed);
            (int a, int b) = Pairs[trial / Repeats];
            int repetition = trial % Repeats;
            string blue = Tanks[repetition == 1 ? b : a], red = Tanks[repetition == 1 ? a : b];
            float depth = repetition == 1 ? -2.5f : 2.5f;
            Vector3 blueStart = centre + new Vector3(-11f, 0, depth);
            Vector3 redStart = centre + new Vector3(11f, 0, -depth);
            if (repetition == 1)
            {
                right = Drop(red, Team.Red, redStart);
                left = Drop(blue, Team.Blue, blueStart);
            }
            else
            {
                left = Drop(blue, Team.Blue, blueStart);
                right = Drop(red, Team.Red, redStart);
            }
            Move(left, blueStart + new Vector3(6f, 0, -depth));
            Move(right, redStart + new Vector3(-6f, 0, depth));
            title = $"{Name(Tanks[a])} vs {Name(Tanks[b])}  |  Round {repetition + 1}/{Repeats}  |  {trial + 1}/18";
            result = null;
            startFrame = frame;
            startedAt = world.Time;
            editor.Game.paused = false;
        }

        static ZObject Drop(string type, Team team, Vector3 at)
        {
            var placed = OzUePlayground.Drop(type, team, at, 1);
            return placed.ok && placed.made.Count == 1 ? placed.made[0] : throw new InvalidOperationException($"Failed to place {type}: {placed.why}");
        }

        static void Move(ZObject unit, Vector3 target)
        {
            var px = ZGame.WorldToPx(target);
            unit.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, x = px.x, y = px.y, playerGiven = true });
        }

        static void Attack(ZObject unit, ZObject target)
        {
            if (unit.Destroyed || target.Destroyed) return;
            unit.Waypoints.Clear();
            unit.Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = target.Id, x = target.CenterX, y = target.CenterY, playerGiven = true });
        }

        static string Name(string type) => char.ToUpperInvariant(type[0]) + type.Substring(1);

        void OnGUI()
        {
            if (title == null) return;
            heading ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 24 };
            stats ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            var box = new Rect((Screen.width - 510f) / 2f, 15f, 510f, 91f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x, box.y + 3f, box.width, 34f), title, heading);
            GUI.Label(new Rect(box.x, box.y + 34f, box.width, 24f), "OpenZ-VK  |  Z Classic  |  1× speed", stats);
            string state = $"Blue {Name(left.Type)} {left.Health:N0} HP   •   Red {Name(right.Type)} {right.Health:N0} HP";
            GUI.Label(new Rect(box.x, box.y + 59f, box.width, 26f), result == null ? state : result + "  |  " + state, stats);
        }

        void Finish(string message)
        {
            File.WriteAllText(Path.Combine(folder, "capture-result.txt"), message + "\n");
            Debug.Log(message);
            Application.Quit(message.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1);
        }
    }
}
