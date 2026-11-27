using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>The fork version is independent of upstream OpenZ's version and build counter.</summary>
    public static class OzForkVersion
    {
        public const string Number = "1.5.103";
        [Serializable] public sealed class BuildIdentity { public string version, commit, builtUtc; public bool dirty; }
        static BuildIdentity identity;
        public static string Label
        {
            get
            {
                if (identity == null)
                {
                    try { identity = JsonUtility.FromJson<BuildIdentity>(File.ReadAllText(Path.Combine(Application.dataPath, "../FORK_BUILD.json"))) ?? new BuildIdentity(); }
                    catch { identity = new BuildIdentity(); }
                }
                if (string.IsNullOrEmpty(identity.builtUtc)) return "OpenZ-VK " + Number + (Application.isEditor ? " · SOURCE" : " · UNSTAMPED BUILD");
                string commit = identity.commit ?? "unknown";
                if (commit.Length > 10) commit = commit.Substring(0, 10);
                return "OpenZ-VK " + identity.version + " · " + commit + (identity.dirty ? "+modified" : "") + "\nBUILD " + identity.builtUtc;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("OpenZ-VK build identity", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1200;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            var panel = Hud.Node("Version", go.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
            panel.sizeDelta = new Vector2(740, 20);
            panel.anchoredPosition = new Vector2(-16, 2);
            var label = Hud.Label(panel, "Identity", Label.Replace("\nBUILD ", " · "), 12, OzForkOptions.Brand, .02f)
                .FillParent(OzH.R);
            label.graphic.raycastTarget = false;
        }
    }
}
