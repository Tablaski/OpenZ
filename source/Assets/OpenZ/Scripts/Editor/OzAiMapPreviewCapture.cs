using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenZ.Experimental;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ.EditorTools
{
    [InitializeOnLoad]
    public static class OzAiMapPreviewCapture
    {
        const string Session = "OpenZ-VK AI map preview capture";
        const int Width = 640, Height = 360, Fps = 24, Frames = 96;
        enum Shot { Overview, Traverse, Landmark, Fort }
        static readonly Shot[][] Edits =
        {
            new[] { Shot.Overview, Shot.Traverse, Shot.Landmark },
            new[] { Shot.Fort, Shot.Landmark, Shot.Overview },
            new[] { Shot.Landmark, Shot.Overview, Shot.Traverse },
            new[] { Shot.Traverse, Shot.Fort, Shot.Overview },
            new[] { Shot.Overview, Shot.Fort, Shot.Landmark },
            new[] { Shot.Landmark, Shot.Traverse, Shot.Overview }
        };
        static OzAiMap[] entries;
        static string output, ffmpeg, clip;
        static int index, frame, loadedAt, lastFrame, firstCut, secondCut;
        static Camera camera;
        static RenderTexture target;
        static Texture2D image;
        static Process encoder;
        static ZGame game;
        static Vector3 a, b, detail, center, extent, fortExtent;
        static float angle, side, motion;
        static bool stills;

        static OzAiMapPreviewCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Session, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Configure();
                    Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                    index = SessionState.GetInt(Session + " index", 0);
                    loadedAt = Time.frameCount;
                    lastFrame = -1;
                    EditorApplication.update += Capture;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.EraseBool(Session);
                    EditorApplication.Exit(0);
                }
            };
        }

        // Supply -oz-preview-output PATH -oz-preview-ffmpeg PATH and optionally -oz-preview-map ID.
        public static void Run()
        {
            Configure();
            Directory.CreateDirectory(output);
            index = Next(0);
            if (index == entries.Length) { EditorApplication.Exit(0); return; }
            OzShaderWarmup.PlayStartCapSeconds = 2;
            EditorSceneManager.OpenScene(entries[index].ScenePath);
            SessionState.SetInt(Session + " index", index);
            SessionState.SetBool(Session, true);
            EditorApplication.EnterPlaymode();
        }

        static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string key)
            {
                int i = Array.IndexOf(args, key);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
            output = Path.GetFullPath(Arg("-oz-preview-output") ?? "../.local/ai-map-previews");
            ffmpeg = Arg("-oz-preview-ffmpeg") ?? "ffmpeg";
            stills = args.Contains("-oz-preview-skirmish-posters");
            string map = Arg("-oz-preview-map");
            entries = (stills ? OzAiMapCatalog.MultiplayerMaps : OzAiMapCatalog.Playable)
                .Where(e => map == null || string.Equals(e.Id, map, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length == 0) throw new InvalidOperationException("No matching AI maps.");
        }

        static string Video(int i) => Path.Combine(output, entries[i].Id + "_preview.mp4");
        static string Poster(int i) => Path.Combine(output, entries[i].Id + "_poster.jpg");
        static int Next(int i)
        {
            while (i < entries.Length && File.Exists(Poster(i)) && (stills || File.Exists(Video(i)))) i++;
            return i;
        }

        static void Capture()
        {
            try
            {
                if (Time.frameCount == lastFrame || Time.frameCount - loadedAt < 12) return;
                lastFrame = Time.frameCount;
                if (!camera) Open();
                int shot = frame < firstCut ? 0 : frame < secondCut ? 1 : 2;
                int start = shot == 0 ? 0 : shot == 1 ? firstCut : secondCut;
                int end = shot == 0 ? firstCut : shot == 1 ? secondCut : Frames;
                float t = (frame - start) / (end - start - 1f);
                Shot kind = Edit()[shot];
                Pose(stills ? Shot.Landmark : kind, stills ? .45f : t);
                RenderSettings.fog = false;
                Atmosphere.DebugFog = 2;
                Shader.SetGlobalFloat("_AtmoEnabled", 0);
                Shader.SetGlobalFloat("_HorizonStart", 3000f);
                Shader.SetGlobalFloat("_HorizonEnd", 5000f);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                }
                finally { RenderTexture.active = previous; }
                if (stills)
                {
                    File.WriteAllBytes(Poster(index), image.EncodeToJPG(82));
                    UnityEngine.Object.Destroy(target);
                    UnityEngine.Object.Destroy(image);
                    camera = null;
                }
                else
                {
                    byte[] pixels = image.GetRawTextureData();
                    encoder.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
                    if (kind == Shot.Overview && frame == (start + end) / 2)
                        File.WriteAllBytes(Poster(index) + ".partial", image.EncodeToJPG(85));
                    if (++frame < Frames) return;
                    Close();
                }
                index = Next(index + 1);
                if (index == entries.Length)
                {
                    Debug.Log($"[AI map previews] PASS {entries.Length} {(stills ? "posters" : "clips and posters")}");
                    EditorApplication.update -= Capture;
                    EditorApplication.ExitPlaymode();
                    return;
                }
                frame = 0;
                loadedAt = Time.frameCount;
                EditorSceneManager.LoadSceneInPlayMode(entries[index].ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                if (encoder != null && !encoder.HasExited) encoder.Kill();
                EditorApplication.Exit(1);
            }
        }

        static uint Seed(string id)
        {
            uint value = 2166136261u;
            foreach (char ch in id) value = unchecked((value ^ ch) * 16777619u);
            return value;
        }

        static Shot[] Edit() => Edits[Seed(entries[index].Id) % (uint)Edits.Length];

        static Vector3 Location(ZObject obj) => game.CenterWorld(obj);
        static Vector3 Ground(Vector3 p, float offset)
        {
            p.y = game.GroundY(p.x, p.z) + offset;
            return p;
        }

        static void Open()
        {
            game = ZGame.I;
            if (!game || game.World == null || game.level != entries[index].Id)
                throw new InvalidOperationException("Wrong scene for " + entries[index].Id);
            game.paused = true;
            foreach (var bot in game.World.Bots) bot.Ignored = true;
            var map = game.World.Map;
            extent = new Vector3(map.width, 0, map.height);
            center = new Vector3(extent.x, 0, -extent.z);
            uint seed = Seed(entries[index].Id);
            angle = seed % 360;
            side = (seed & 1) == 0 ? 1 : -1;
            motion = ((seed >> 16) & 255) / 255f;
            firstCut = 29 + (int)(seed % 9);
            secondCut = firstCut + 29 + (int)((seed >> 8) % 8);
            var forts = game.World.Objects.Where(o => o.IsFort && o.Owner != Team.Neutral)
                .GroupBy(o => o.Owner).Select(g => g.OrderByDescending(o => o.Type == "fort_front").First()).ToArray();
            if (forts.Length < 2) throw new InvalidOperationException("Expected two forts on " + game.level);
            var nearFort = forts[side > 0 ? 0 : 1];
            a = Location(nearFort);
            b = Location(forts[side > 0 ? 1 : 0]);
            fortExtent = new Vector3(nearFort.WidthPix / 16f, 4, nearFort.HeightPix / 16f);
            if (game.StaticViews.TryGetValue(nearFort.Id, out var model))
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var bounds = renderer.bounds;
                    var offset = bounds.center - a;
                    fortExtent = Vector3.Max(fortExtent, bounds.extents + new Vector3(Mathf.Abs(offset.x), 0, Mathf.Abs(offset.z)));
                }
            var landmarks = game.World.Objects.Where(o => o.IsBridge || o.Kind == ZKind.Building)
                .Where(o => (Location(o) - a).sqrMagnitude > 42 && (Location(o) - b).sqrMagnitude > 42)
                .OrderBy(o => (Location(o) - center).sqrMagnitude * (o.IsBridge ? .34f : 1f)).Take(5).ToArray();
            detail = landmarks.Length == 0 ? Vector3.Lerp(a, b, .48f) : Location(landmarks[seed % (uint)landmarks.Length]);
            if (map.HasLava)
            {
                var lava = Enumerable.Range(0, map.lava.Length).Where(i => map.lava[i] > 0)
                    .Select(i => new Vector3(2 * (i % map.width) + 1, 0, -2 * (i / map.width) - 1))
                    .OrderBy(p => (p - detail).sqrMagnitude).Take(12).ToArray();
                detail = lava[(seed >> 11) % (uint)lava.Length];
            }
            camera = Camera.main;
            camera.GetComponent<RtsCamera>().enabled = false;
            camera.enabled = false;
            camera.aspect = Width / (float)Height;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            target = new RenderTexture(Width, Height, 24);
            target.Create();
            image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            if (stills)
            {
                Debug.Log($"[AI map previews] {index + 1}/{entries.Length} {game.level}: poster");
                return;
            }
            clip = Video(index) + ".partial.mp4";
            encoder = Process.Start(new ProcessStartInfo(ffmpeg,
                $"-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size {Width}x{Height} -framerate {Fps} -i pipe:0 " +
                $"-vf vflip -an -c:v libx264 -threads 2 -preset veryfast -crf 24 -pix_fmt yuv420p -bf 0 -movflags +faststart \"{clip}\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
            });
            Debug.Log($"[AI map previews] {index + 1}/{entries.Length} {game.level}: {string.Join(", ", Edit())}");
        }

        static void Pose(Shot shot, float progress)
        {
            float t = Mathf.SmoothStep(0, 1, progress);
            var axis = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            var lateral = Vector3.Cross(Vector3.up, axis) * side;
            float span = Vector3.Distance(a, b);
            switch (shot)
            {
                case Shot.Overview:
                    float pitch = Mathf.Lerp(65, 79, Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)));
                    var rotation = Quaternion.Euler(pitch, angle + side * (t - .5f) * 20, 0);
                    camera.fieldOfView = 45;
                    float height = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                    float distance = 0;
                    var inverse = Quaternion.Inverse(rotation);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var cornerPosition = new Vector3((corner & 1) == 0 ? -extent.x : extent.x,
                            (corner & 2) == 0 ? -3 : 7, (corner & 4) == 0 ? -extent.z : extent.z);
                        var local = inverse * cornerPosition;
                        distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(local.x) / camera.aspect, Mathf.Abs(local.y)) / height - local.z);
                    }
                    camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance * Mathf.Lerp(1.03f, 1.17f, t), rotation);
                    break;
                case Shot.Traverse:
                    var track = Vector3.Lerp(a, b, Mathf.Lerp(.05f, .90f, t));
                    float rise = Mathf.Lerp(.20f, .35f, motion) + .065f * Mathf.Sin(t * Mathf.PI);
                    Look(track + lateral * Mathf.Clamp(span * Mathf.Lerp(.12f, .27f, motion), 12, 45),
                        Mathf.Clamp(span * rise, 15, 56), Vector3.Lerp(track, detail, .32f), 53);
                    break;
                case Shot.Landmark:
                    var offset = Quaternion.Euler(0, angle + side * Mathf.Lerp(14, 76, t), 0) * Vector3.back;
                    float scale = Mathf.Min(extent.x, extent.z);
                    Look(detail + offset * Mathf.Lerp(scale * .82f, scale * (.56f + .14f * motion), t),
                        Mathf.Lerp(scale * .61f, scale * (.38f + .10f * motion), t),
                        Vector3.Lerp(detail, center, .23f + .08f * t), Mathf.Lerp(51, 48, t));
                    break;
                case Shot.Fort:
                    float front = Mathf.Abs(axis.x) * fortExtent.x + Mathf.Abs(axis.z) * fortExtent.z;
                    float flank = Mathf.Abs(lateral.x) * fortExtent.x + Mathf.Abs(lateral.z) * fortExtent.z;
                    if (!Inside(a + lateral * (flank + 4f) + axis * front)) lateral = -lateral;
                    var lowPass = a + axis * (front + Mathf.Lerp(1.7f, 6f, t)) +
                        lateral * Mathf.Lerp(flank + 3f, flank + 1f, t);
                    var sweep = Vector3.Lerp(a, b, Mathf.Lerp(.53f, 1f, t)) + lateral * Mathf.Lerp(span * .13f, 0, t);
                    Look(lowPass, Mathf.Lerp(4.2f + 1.4f * motion, 7.5f, t), sweep, Mathf.Lerp(66, 58, t));
                    break;
            }
        }

        static bool Inside(Vector3 p) => p.x > 2 && p.x < 2 * extent.x - 2 && p.z < -2 && p.z > 2 - 2 * extent.z;

        static void Look(Vector3 position, float height, Vector3 at, float field)
        {
            position = Ground(position, height);
            camera.fieldOfView = field;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Ground(at, 1.3f) - position));
        }

        static void Close()
        {
            encoder.StandardInput.Close();
            if (!encoder.WaitForExit(30000) || encoder.ExitCode != 0) throw new IOException("AI map preview encoding failed.");
            encoder.Dispose();
            encoder = null;
            Publish(clip, Video(index));
            Publish(Poster(index) + ".partial", Poster(index));
            UnityEngine.Object.Destroy(image);
            target.Release();
            UnityEngine.Object.Destroy(target);
            camera = null;
        }

        static void Publish(string staging, string destination)
        {
            if (File.Exists(destination)) File.Replace(staging, destination, null);
            else File.Move(staging, destination);
        }
    }
}
