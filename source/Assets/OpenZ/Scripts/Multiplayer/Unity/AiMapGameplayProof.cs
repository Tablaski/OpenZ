// OpenZ-VK (GPL-3.0).
using System;
using System.Collections;
using System.IO;
using System.Linq;
using OpenZ.Experimental;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.Multiplayer
{
    /// <summary>Command-line integration checks against the release executable's actual converted scenes.</summary>
    public sealed class AiMapGameplayProof : MonoBehaviour
    {
        static readonly string[] Missions = { "SM04", "SM08", "SM13", "SM15", "SM18F", "SMChillyWillyV2" };
        string output;
        string recordingDirectory;
        int bridges, rocks, crates, crocodiles;
        string failure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartFromArguments()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-oz-ai-map-proof");
            if (i < 0 || i + 1 >= args.Length || FindFirstObjectByType<AiMapGameplayProof>()) return;
            var go = new GameObject("AI map gameplay proof");
            DontDestroyOnLoad(go);
            var proof = go.AddComponent<AiMapGameplayProof>();
            proof.output = args[i + 1];
            if (i + 2 < args.Length && !args[i + 2].StartsWith("-", StringComparison.Ordinal))
                proof.recordingDirectory = args[i + 2];
        }

        IEnumerator Start()
        {
            foreach (var id in Missions)
            {
                var entry = OzAiMapCatalog.Find(id);
                var load = SceneManager.LoadSceneAsync(entry.Scene);
                yield return load;
                yield return new WaitForSecondsRealtime(.3f);
                if (!RunSafe(() => CheckScene(id))) break;
                var game = ZGame.I;
                var bridge = game.World.Objects.FirstOrDefault(o => o.IsBridge && !o.Destroyed);
                if (bridge == null) continue;
                var original = game.StaticViews[bridge.Id];
                SetHealth(game, bridge, Math.Max(1, bridge.MaxHealth / 4));
                yield return new WaitForSecondsRealtime(.75f);
                if (!RunSafe(() => CheckStage(original, "_damaged", true))) break;
                SetHealth(game, bridge, 0);
                yield return new WaitForSecondsRealtime(.75f);
                if (!RunSafe(() =>
                {
                    CheckStage(original, "_destroyed", false);
                    int x = bridge.CenterX / 16, y = bridge.CenterY / 16;
                    Require(!game.World.Path.TilePassable(x, y, false), "A destroyed crossing still admits a tank");
                })) break;
                SetHealth(game, bridge, bridge.MaxHealth);
                yield return new WaitForSecondsRealtime(.75f);
                if (!RunSafe(() => Require(original.activeInHierarchy && original.transform.Find("WalkDeck"),
                    "Repaired bridge did not recover its walking surface"))) break;
                bridges++;
            }
            if (failure == null) RunSafe(() => Require(bridges >= 3 && rocks > 0 && crates > 0 && crocodiles > 0,
                "The selected missions did not exercise every regression category."));
            if (failure == null && recordingDirectory != null)
                yield return CaptureSolo();
            string result = failure == null
                ? $"PASS: {Missions.Length} compiled scenes, {bridges} damaged/destroyed/repaired bridges, {rocks} rock views, {crates} grenade crates, {crocodiles} crocodiles; water, lava and placement checks."
                : failure;
            File.WriteAllText(output, result);
            Debug.Log(result);
            Application.Quit(failure == null ? 0 : 1);
        }

        IEnumerator CaptureSolo()
        {
            yield return SceneManager.LoadSceneAsync(OzAiMapCatalog.Find("SM04").Scene);
            yield return new WaitForSecondsRealtime(.5f);
            var game = ZGame.I;
            var world = game.World;
            var map = world.Map;
            var ordered = new System.Collections.Generic.List<(ZObject unit, Vector2 start)>();
            foreach (var unit in world.Objects.Where(obj => obj.Owner == Team.Red && obj.IsMobile && !obj.IsMinion))
            {
                if (ordered.Count >= 5) break;
                for (int step = 0; step < 8; step++)
                {
                    float a = step * Mathf.PI / 4f;
                    int dx = Mathf.RoundToInt(Mathf.Cos(a)), dy = Mathf.RoundToInt(Mathf.Sin(a));
                    int tx = unit.CenterX / 16, ty = unit.CenterY / 16;
                    if (!Enumerable.Range(1, 7).All(k => world.Path.TilePassable(tx + dx * k, ty + dy * k, unit.IsRobot)
                        && !map.LavaAt(tx + dx * k, ty + dy * k))) continue;
                    if (!world.SendWaypoints(unit.Owner, unit, new[]
                        { ZWaypoint.Move((tx + dx * 7) * 16 + 8, (ty + dy * 7) * 16 + 8) })) continue;
                    ordered.Add((unit, new Vector2(unit.CenterX, unit.CenterY)));
                    break;
                }
            }
            if (!RunSafe(() => Require(ordered.Count > 0, "No unit accepted a solo movement order"))) yield break;
            Directory.CreateDirectory(recordingDirectory);
            var camera = Camera.main;
            camera.GetComponent<RtsCamera>().enabled = false;
            game.paused = false;
            var lead = ordered[0].unit;
            Time.captureFramerate = 24;
            for (int frame = 0; frame < 240; frame++)
            {
                float t = frame / 239f;
                var center = game.CenterWorld(lead);
                float yaw = Mathf.Lerp(16, 30, t) * Mathf.Deg2Rad;
                camera.transform.position = center + new Vector3(Mathf.Sin(yaw) * 22, 27, -Mathf.Cos(yaw) * 22);
                camera.transform.LookAt(center);
                yield return new WaitForEndOfFrame();
                DuelProofCapture.Save(Path.Combine(recordingDirectory, $"frame-{frame:D3}.jpg"));
            }
            Time.captureFramerate = 0;
            RunSafe(() => Require(ordered.Any(o => Vector2.Distance(o.start,
                new Vector2(o.unit.CenterX, o.unit.CenterY)) >= 16), "The solo units did not move during capture"));
        }

        bool RunSafe(Action action)
        {
            try { action(); return true; }
            catch (Exception error) { failure = "FAIL: " + error; return false; }
        }

        void CheckScene(string id)
        {
            var game = ZGame.I;
            Require(game && game.level == id && game.World != null, "Expected compiled scene " + id);
            game.paused = true;
            var world = game.World;
            var map = world.Map;
            var terrain = FindFirstObjectByType<OzConversionTerrainV2>();
            Require(terrain && terrain.IsPaletteV3, id + ": missing V3 terrain");
            foreach (var obj in world.Objects)
            {
                if (obj.Source == null) continue;
                if (obj.IsBridge && !obj.Destroyed) CheckWalkDeck(game, game.StaticViews[obj.Id]);
                if (obj.Type == "grenades")
                {
                    Require(game.StaticViews[obj.Id].GetComponent<GrenadeCrateV2>(), id + ": missing grenade box art");
                    crates++;
                }
                if (obj.Type == "rock")
                {
                    var art = game.StaticViews[obj.Id].GetComponent<RockPillar>();
                    Require(art && art.pillar && art.damaged, id + ": missing rock destruction stages");
                    var renderer = art.pillar.GetComponentInChildren<Renderer>();
                    Require(renderer && renderer.bounds.size.y > .8f, id + ": visually flat rock");
                    rocks++;
                }
                if (obj.Type == "flag" || obj.Source.Category == "building")
                    Require(obj.LocX == obj.Source.x * 16 && obj.LocY == obj.Source.y * 16, id + ": misplaced " + obj.Type);
            }
            var croc = FindFirstObjectByType<CrocodileView>();
            if (world.Crocs.Count > 0)
            {
                Require(croc && croc.isActiveAndEnabled, id + ": crocodiles are simulated without a view");
                crocodiles += world.Crocs.Count;
            }
            if (map.HasLava)
            {
                int at = Array.FindIndex(map.lava, x => x > 0);
                int x = at % map.width, y = at / map.width;
                bool previous = ZWorld.LavaDeviation;
                try
                {
                    ZWorld.LavaDeviation = false;
                    Require(map.TileWalkSpeed(x * 16 + 8, y * 16 + 8) == 0, id + ": lava option off");
                    ZWorld.LavaDeviation = true;
                    Require(map.TileWalkSpeed(x * 16 + 8, y * 16 + 8) == ZWorld.LavaWalkSpeed, id + ": original lava walk missing");
                    Require(!world.Path.TilePassable(x, y, false), id + ": vehicle admitted onto lava");
                }
                finally { ZWorld.LavaDeviation = previous; }
            }
        }

        static void CheckWalkDeck(ZGame game, GameObject bridge)
        {
            var deck = bridge.transform.Find("WalkDeck");
            Require(deck && deck.TryGetComponent<MeshCollider>(out _), bridge.name + ": missing bridge collider");
            var mesh = deck.GetComponent<MeshCollider>().sharedMesh;
            var vertices = mesh.vertices.Select(v => deck.TransformPoint(v)).ToArray();
            Require(vertices.Length == 8, bridge.name + ": deck or ramp geometry is missing");
            Require(vertices[2].y > -.30f && vertices[4].y > -.30f,
                bridge.name + ": the deck is below the water surface");
            var previous = (vertices[0] + vertices[1]) * .5f;
            float last = game.GroundY(previous.x, previous.z);
            for (int i = 2; i < vertices.Length; i += 2)
            {
                var next = (vertices[i] + vertices[i + 1]) * .5f;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / .25f));
                for (int step = 1; step <= steps; step++)
                {
                    var at = Vector3.Lerp(previous, next, step / (float)steps);
                    float current = game.GroundY(at.x, at.z);
                    Require(Mathf.Abs(current - last) <= .95f, bridge.name + ": abrupt height change across the deck");
                    last = current;
                }
                previous = next;
            }
        }

        static void CheckStage(GameObject original, string suffix, bool walking)
        {
            var stage = original.transform.parent.Find(original.name + suffix);
            Require(stage && stage.gameObject.activeInHierarchy, original.name + ": missing visible " + suffix + " model");
            Require((stage.Find("WalkDeck") != null) == walking, original.name + ": incorrect walk deck after " + suffix);
        }

        static void SetHealth(ZGame game, ZObject obj, int health)
        {
            obj.SetHealth(health);
            game.World.UpdateObjectHealth(obj);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
