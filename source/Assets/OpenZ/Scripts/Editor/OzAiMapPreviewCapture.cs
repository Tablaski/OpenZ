using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenZ.Experimental;
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
    public static partial class OzAiMapPreviewCapture
    {
        const string Session = "OpenZ-VK AI map preview capture";
        const int Fps = 24;
        static readonly string[] ComparisonMaps = { "SM02", "SM08", "SM11", "SM16", "SM17" };
        static OzAiMap[] entries;
        static string output, ffmpeg, clip;
        static int index, frame, loadedAt, lastFrame, width, height;
        static bool comparison, stills, force, rim;
        static Camera camera;
        static RenderTexture target;
        static Texture2D image;
        static Process encoder;
        static ZGame game;
        static DayNight lighting;
        static Shot[] shots;
        static double worldTime;

        static OzAiMapPreviewCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Session, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Configure();
                    index = SessionState.GetInt(Session + " index", 0);
                    loadedAt = Time.frameCount;
                    lastFrame = -1;
                    SceneManager.sceneLoaded += FreezeScene;
                    FreezeScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
                    EditorApplication.update += Capture;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.EraseBool(Session);
                    EditorApplication.Exit(0);
                }
            };
        }

        static void FreezeScene(Scene scene, LoadSceneMode mode)
        {
            var loaded = ZGame.I;
            if (!loaded || loaded.gameObject.scene != scene) return;
            loaded.paused = true;
        }

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
            comparison = args.Contains("-oz-preview-comparisons");
            rim = comparison && args.Contains("-oz-preview-rim");
            stills = args.Contains("-oz-preview-skirmish-posters");
            force = args.Contains("-oz-preview-force");
            width = comparison ? 3840 : 960;
            height = width * 9 / 16;
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToHashSet();
            var filter = Arg("-oz-preview-map");
            entries = OzAiMapCatalog.SkirmishMaps
                .Where(entry => enabledScenes.Contains(entry.ScenePath))
                .Where(entry => !comparison || ComparisonMaps.Contains(entry.Id))
                .Where(entry => !stills || entry.Starts > 2)
                .Where(entry => filter == null || string.Equals(entry.Id, filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length == 0) throw new InvalidOperationException("No matching AI maps.");
        }

        static string Video(int i) => Path.Combine(output, entries[i].Id + "_preview.mp4");
        static string Poster(int i) => Path.Combine(output, entries[i].Id + "_poster.jpg");
        static string Comparison(int i) => Path.Combine(output, entries[i].Id + ".png");
        static int Next(int i)
        {
            if (force) return i;
            while (i < entries.Length && (comparison ? File.Exists(Comparison(i))
                : File.Exists(Poster(i)) && (stills || File.Exists(Video(i))))) i++;
            return i;
        }

        static void Capture()
        {
            try
            {
                FreezeScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
                if (Time.frameCount == lastFrame || Time.frameCount - loadedAt < 12) return;
                lastFrame = Time.frameCount;
                if (!camera) Open();
                if (game.World.Time != worldTime) throw new InvalidOperationException("The simulation advanced during " + game.level);
                int active = 0, offset = frame;
                while (offset >= shots[active].Frames) offset -= shots[active++].Frames;
                var shot = shots[active];
                Pose(shot, comparison || stills ? .5f : offset / (shot.Frames - 1f));
                if (lighting) lighting.Apply(.08f);
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
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                }
                finally { RenderTexture.active = previous; }

                if (comparison)
                {
                    File.WriteAllBytes(Comparison(index) + ".partial", image.EncodeToPNG());
                    Publish(Comparison(index) + ".partial", Comparison(index));
                }
                else if (stills)
                {
                    File.WriteAllBytes(Poster(index) + ".partial", image.EncodeToJPG(88));
                    Publish(Poster(index) + ".partial", Poster(index));
                }
                else
                {
                    byte[] pixels = image.GetRawTextureData();
                    encoder.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
                    if (active == 0 && offset == shot.Frames / 2)
                        File.WriteAllBytes(Poster(index) + ".partial", image.EncodeToJPG(88));
                    frame++;
                    if (active != shots.Length - 1 || offset != shot.Frames - 1) return;
                    Close();
                }
                ReleaseCapture();

                index = Next(index + 1);
                if (index == entries.Length)
                {
                    Debug.Log($"[AI map previews] PASS {entries.Length} {(comparison || stills ? "stills" : "clips and posters")}");
                    EditorApplication.update -= Capture;
                    SceneManager.sceneLoaded -= FreezeScene;
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

        static void Open()
        {
            var entry = entries[index];
            game = ZGame.I;
            if (!game || game.World == null || game.level != entry.Id)
                throw new InvalidOperationException("Wrong scene for " + entry.Id);
            game.paused = true;
            worldTime = game.World.Time;
            var terrain = UnityEngine.Object.FindFirstObjectByType<OzConversionTerrainV2>();
            if (!terrain || !terrain.IsPaletteV3 || terrain.IsPaletteV4 != (entry.Variant != "V3") ||
                terrain.IsPaletteV5 != (entry.Variant == "V5"))
                throw new InvalidOperationException("Wrong terrain profile for " + entry.Id);
            lighting = UnityEngine.Object.FindFirstObjectByType<DayNight>();
            shots = rim ? new[] { RimShot(entry, game.World.Map) } : Plan(entry, game);
            camera = Camera.main;
            camera.GetComponent<RtsCamera>().enabled = false;
            camera.enabled = false;
            camera.aspect = width / (float)height;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            target = new RenderTexture(width, height, 24);
            target.Create();
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            if (!comparison && !stills)
            {
                clip = Video(index) + ".partial.mp4";
                encoder = Process.Start(new ProcessStartInfo(ffmpeg,
                    $"-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size {width}x{height} -framerate {Fps} -i pipe:0 " +
                    $"-vf vflip -an -c:v libx264 -threads 2 -preset veryfast -crf 25 -pix_fmt yuv420p -bf 0 -movflags +faststart \"{clip}\"")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
                });
            }
            Debug.Log($"[AI map previews] {index + 1}/{entries.Length} {entry.Id}: {string.Join(", ", shots.Select(shot => shot.Subject))}");
        }

        static void Close()
        {
            encoder.StandardInput.Close();
            if (!encoder.WaitForExit(30000) || encoder.ExitCode != 0) throw new IOException("AI map preview encoding failed.");
            encoder.Dispose();
            encoder = null;
            Publish(clip, Video(index));
            Publish(Poster(index) + ".partial", Poster(index));
        }

        static void ReleaseCapture()
        {
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
