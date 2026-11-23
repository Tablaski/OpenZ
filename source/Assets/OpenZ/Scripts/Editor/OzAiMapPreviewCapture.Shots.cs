using System;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static partial class OzAiMapPreviewCapture
    {
        enum Motion { Sweep, Crane, Orbit, Follow }

        readonly struct Shot
        {
            public readonly string Subject;
            public readonly Vector3 Focus, Travel;
            public readonly float Heading, Turn, Pitch, Rise, Distance, Dolly, Field;
            public readonly int Frames;

            public Shot(string subject, Vector3 focus, Vector3 travel, float heading, float turn,
                float pitch, float rise, float distance, float dolly, float field, int frames)
            {
                Subject = subject;
                Focus = focus;
                Travel = travel;
                Heading = heading;
                Turn = turn;
                Pitch = pitch;
                Rise = rise;
                Distance = distance;
                Dolly = dolly;
                Field = field;
                Frames = frames;
            }

            public Shot AsStill(string subject) => new Shot(subject, Focus, Vector3.zero, Heading, 0, Pitch, 0, Distance, 0, Field, 1);
        }

        static Shot[] Plan(OzAiMap entry, ZGame game)
        {
            var map = game.World.Map;
            int seed = 0;
            foreach (char ch in entry.Id) seed = unchecked(seed * 16777619 ^ ch);
            var random = new System.Random(seed);
            float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();
            var center = new Vector3(map.width, 1, -map.height);
            Vector3 At(ZObject obj) => game.CenterWorld(obj, false) + Vector3.up;
            var forts = game.World.Objects.Where(obj => obj.IsFort && obj.Owner != Team.Neutral)
                .GroupBy(obj => obj.Owner).Select(group => group.First()).OrderBy(obj => obj.Id).ToArray();
            if (forts.Length < 2) throw new InvalidOperationException("Expected at least two forts on " + entry.Id);
            var fort = forts[random.Next(forts.Length)];
            var buildings = game.World.Objects.Where(obj => !obj.Destroyed &&
                (obj.Type == "radar" || obj.Type == "robot_factory" || obj.Type == "vehicle_factory")).ToArray();
            var building = buildings.Length == 0 ? fort : buildings[random.Next(buildings.Length)];
            var units = game.World.Objects.Where(obj => obj.IsUnit && obj.Source != null && !obj.Destroyed).ToArray();
            var rocks = game.World.Objects.Where(obj => obj.Type == "rock" && !obj.Destroyed).ToArray();
            var bridges = game.World.Objects.Where(obj => obj.IsBridge).ToArray();

            Vector3 Group(ZObject[] objects, float radius, Vector3 fallback, bool preferVariety)
            {
                if (objects.Length == 0) return fallback;
                int stride = Mathf.Max(1, objects.Length / 140);
                var groups = objects.Where((_, i) => i % stride == 0).Select(anchor =>
                {
                    var group = objects.Where(obj => (At(obj) - At(anchor)).sqrMagnitude <= radius * radius).ToArray();
                    int types = preferVariety ? group.Select(obj => obj.Type).Distinct().Count() : 0;
                    return (Position: group.Aggregate(Vector3.zero, (sum, obj) => sum + At(obj)) / group.Length,
                        Weight: group.Length + types * 4 + Range(0, 3));
                }).OrderByDescending(group => group.Weight).Take(4).ToArray();
                return groups[random.Next(groups.Length)].Position;
            }

            var fortPoint = At(fort);
            var unitPoint = Group(units, 24, fortPoint, true);
            var rockPoint = Group(rocks, 29, center, false);
            bool showBridge = bridges.Length != 0 && (rocks.Length < 12 || random.Next(3) != 0);
            var bridge = showBridge ? bridges[random.Next(bridges.Length)] : null;
            var terrainPoint = showBridge ? At(bridge) : rockPoint;
            string terrainSubject = showBridge ? (bridge.Destroyed ? "broken crossing" : "bridge and banks")
                : rocks.Length != 0 ? "rock formations" : "terrain corridor";

            float reach = Mathf.Min(map.width, map.height);
            Shot Build(string subject, Vector3 focus, Motion motion, float distance, int minFrames, float? direction = null)
            {
                float heading = direction ?? Range(-180, 180);
                float pitch = Range(36, 65);
                var axis = Quaternion.Euler(0, heading, 0) * Vector3.forward;
                var lateral = Vector3.Cross(Vector3.up, axis);
                float sign = random.Next(2) == 0 ? -1 : 1;
                return new Shot(subject, focus,
                    (motion == Motion.Follow ? axis : lateral) * sign * Range(10, motion == Motion.Orbit ? 15 : 38),
                    heading, sign * Range(motion == Motion.Orbit ? 35 : 5, motion == Motion.Orbit ? 65 : 24),
                    pitch, sign * Range(motion == Motion.Crane ? 15 : 2, motion == Motion.Crane ? 25 : 11),
                    Mathf.Clamp(distance, 43, 106), sign * Range(4, 19), Range(38, 48),
                    minFrames + random.Next(18));
            }

            var fortShot = Build("fort and approaches", fortPoint, Motion.Crane, reach * Range(.57f, .78f), 31);
            var unitShot = Build("stationary unit group", unitPoint, Motion.Sweep, reach * Range(.49f, .66f), 28);
            var buildingShot = Build(building.Type, At(building), Motion.Orbit, reach * Range(.61f, .84f), 27);
            var terrainShot = Build(terrainSubject, terrainPoint, Motion.Follow, reach * Range(.72f, .93f), 29);
            if (entry.Id == "SM16")
            {
                // The two ravines, the high road, and the road approaches fit in a single moving frame.
                var road = new Vector3(map.width, 1, -map.height * .91f);
                return new[] { Build("raised straight road and both ravines", road, Motion.Follow, 91, 49, 5), unitShot, terrainShot, fortShot };
            }

            var shots = rocks.Length >= 20 && showBridge && random.Next(3) == 0
                ? new[] { terrainShot, fortShot, unitShot, buildingShot, Build("rock field", rockPoint, Motion.Crane, reach * .87f, 24) }
                : new[] { terrainShot, fortShot, unitShot, buildingShot };
            for (int i = shots.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (shots[i], shots[j]) = (shots[j], shots[i]);
            }
            return shots;
        }

        static void Pose(Shot shot, float progress)
        {
            float t = Mathf.SmoothStep(0, 1, progress) - .5f;
            var map = game.World.Map;
            var focus = shot.Focus + shot.Travel * t;
            focus.x = Mathf.Clamp(focus.x, 6, 2 * map.width - 6);
            focus.z = Mathf.Clamp(focus.z, 6 - 2 * map.height, -6);
            // Use the same world-space datum in both versions so a terrain-height change stays visible.
            var rotation = Quaternion.Euler(shot.Pitch + shot.Rise * t, shot.Heading + shot.Turn * t, 0);
            float distance = Mathf.Max(43, shot.Distance + shot.Dolly * t);
            camera.fieldOfView = shot.Field;
            camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            while (FullMapVisible(map) && camera.fieldOfView > 30) camera.fieldOfView -= 2;
            if (FullMapVisible(map)) throw new InvalidOperationException("A shot frames the complete map on " + game.level);
        }

        static Shot RimShot(OzAiMap entry, ZMapData map)
        {
            float heading = entry.World == "desert" ? -22 : entry.World == "city" ? 20 : 8;
            float x = map.width * (entry.World == "arctic" ? .52f : .82f);
            return new Shot("borders", new Vector3(x, 3, -13), Vector3.zero,
                heading, 0, 66, 0, 68, 0, 47, 24);
        }

        static Shot TerrainReviewShot(OzAiMap entry, Shot planned) => entry.Id switch
        {
            "SM08" => new Shot("terrain", new Vector3(116, 1, -107), Vector3.zero, 29, 0, 57, 0, 68, 0, 44, 1),
            "SM11" => new Shot("terrain", new Vector3(139, 1, -100), Vector3.zero, 20, 0, 61, 0, 55, 0, 44, 1),
            "SM17" => new Shot("terrain", new Vector3(169, 1, -161), Vector3.zero, 154, 0, 58, 0, 52, 0, 43, 1),
            _ => planned.AsStill("terrain")
        };

        static bool TryBridgeShot(OzAiMap entry, ZGame battle, out Shot shot)
        {
            var map = battle.World.Map;
            var center = new Vector3(map.width, 0, -map.height);
            var bridge = battle.World.Objects.Where(obj => obj.IsBridge).OrderBy(obj => obj.Destroyed)
                .ThenBy(obj => (battle.CenterWorld(obj, false) - center).sqrMagnitude).FirstOrDefault();
            if (bridge == null)
            {
                shot = default;
                return false;
            }
            bool vertical = bridge.Source?.kind == "building:bridge_vert";
            float heading = vertical ? 37 : 128;
            shot = new Shot("bridges", battle.CenterWorld(bridge, false) + Vector3.up, Vector3.zero,
                heading, 0, 45, 0, entry.World == "jungle" ? 49 : 43, 0, 34, 1);
            return true;
        }

        static bool FullMapVisible(ZMapData map)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                var point = camera.WorldToViewportPoint(new Vector3((corner & 1) == 0 ? 0 : map.width * 2, 0,
                    (corner & 2) == 0 ? 0 : -map.height * 2));
                if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return false;
            }
            return true;
        }
    }
}
