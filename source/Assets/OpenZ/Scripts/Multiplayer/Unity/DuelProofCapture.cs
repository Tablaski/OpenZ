using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.Multiplayer
{
    // Explicit rendering lets the opt-in proof capture a hidden window too.
    internal static class DuelProofCapture
    {
        internal static void Save(string path, bool interfaceOnly = false)
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var planes = canvases.Select(c => c.planeDistance).ToArray();
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); rt.Create();
            var go = new GameObject("DuelProofCamera"); var camera = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main && !interfaceOnly)
            {
                camera.CopyFrom(main);
                camera.cullingMask |= 1 << 5;
                camera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            }
            else { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 5; }
            camera.enabled = false; camera.targetTexture = rt; camera.aspect = 16f / 9f;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var previous = RenderTexture.active;
            float atmosphere = Shader.GetGlobalFloat("_AtmoEnabled");
            Texture2D image = null;
            var uiLayers = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).Distinct()
                .Select(t => new { Transform = t, Layer = t.gameObject.layer }).ToArray();
            try
            {
                // Overlay canvases normally render after atmosphere. The explicit UI-only
                // camera must skip this world pass to preserve the actual overlay colours.
                if (interfaceOnly) Shader.SetGlobalFloat("_AtmoEnabled", 0);
                // Newly created modal canvases may start on Default. Capture all UI
                // without making the UI-only camera draw the world's Default layer.
                if (interfaceOnly) foreach (var item in uiLayers) item.Transform.gameObject.layer = 5;
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1; }
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32();
                if (pixels.All(p => p.r < 2 && p.g < 2 && p.b < 2)) throw new InvalidOperationException("Proof rendering produced a black image.");
                File.WriteAllBytes(path, path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? image.EncodeToJPG(88) : image.EncodeToPNG());
            }
            finally
            {
                if (interfaceOnly) Shader.SetGlobalFloat("_AtmoEnabled", atmosphere);
                if (interfaceOnly) foreach (var item in uiLayers) if (item.Transform) item.Transform.gameObject.layer = item.Layer;
                RenderTexture.active = previous;
                for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = null; canvases[i].planeDistance = planes[i]; }
                camera.targetTexture = null;
                UnityEngine.Object.Destroy(go); if (image) UnityEngine.Object.Destroy(image); rt.Release(); UnityEngine.Object.Destroy(rt);
            }
        }
    }
}
