#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.5, level 18 briefings: a look at the corner window's own picture at its full size (editor only, never ships).
// While a briefing plays, the frames of the window's render texture whose briefing clock lies in one of the asked ranges are written as PNG
// (blue: the evil HQ's feed, clock = the scene's own seconds; red: the commander stage's feed, clock = the line's seconds).
// Start it in Play before the take: OpenZ.OzL18FeedDump.Begin("folder", "from,to,every; from,to,every; ..."). It stops with the scene.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzL18FeedDump : MonoBehaviour
    {
        public static int Saved;
        /// <summary>True: the transmission window's own canvas is not drawn while this helper lives (a take of the full-screen flight alone).</summary>
        public static bool HideWindow;
        public static string HideOn() { if (!Application.isPlaying) return "not in Play"; if (!I) { var go = new GameObject("OzL18FeedDump"); DontDestroyOnLoad(go); I = go.AddComponent<OzL18FeedDump>(); I.dir = ""; } HideWindow = true; return "the window's canvas is hidden for this Play session"; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { HideWindow = false; I = null; Saved = 0; }
        static OzL18FeedDump I;
        Canvas hidden; string dir; readonly List<Vector3> ranges = new List<Vector3>(); Texture2D tex; int n;

        public static string Begin(string folder, string spans)
        {
            if (!Application.isPlaying) return "not in Play";
            if (I) Destroy(I.gameObject);
            var go = new GameObject("OzL18FeedDump"); DontDestroyOnLoad(go);
            I = go.AddComponent<OzL18FeedDump>(); I.dir = folder; Directory.CreateDirectory(folder); Saved = 0;
            foreach (var part in spans.Split(';'))
            {
                var a = part.Split(','); if (a.Length < 2) continue;
                I.ranges.Add(new Vector3(float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture),
                    a.Length > 2 ? float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture) : 1f));
            }
            return "feed dump armed: " + I.ranges.Count + " ranges into " + folder;
        }

        void LateUpdate()
        {
            if (HideWindow)
            {   // found once (a search every half second until then), then only held off: the take's frames stay as fast as without it
                if (!hidden && Time.frameCount % 15 == 0)
                    foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c && c.sortingOrder == 103 && c.renderMode == RenderMode.ScreenSpaceOverlay) hidden = c;
                if (hidden && hidden.enabled) hidden.enabled = false;
            }
            if (string.IsNullOrEmpty(dir)) return;
            RenderTexture feed = null; float t = -99f; string side = "";
            if (OzBlueCameo.I && OzBlueCameo.SceneShowing && OzBlueCameo.I.Stage) { feed = OzBlueCameo.I.Stage.Feed; t = OzBlueCameo.LastSceneT; side = "blue"; }
            else if (ZodBriefing.I && ZodBriefing.BriefingActive) { var st = FindFirstObjectByType<ZodStage>(); if (st) feed = st.Feed; t = ZodBriefing.DebugLineT; side = "red"; }
            if (!feed || t < -90f) return;
            n++;
            bool take = false;
            foreach (var r in ranges) if (t >= r.x && t <= r.y && n % Mathf.Max(1, Mathf.RoundToInt(r.z)) == 0) take = true;
            if (!take) return;
            if (!tex || tex.width != feed.width || tex.height != feed.height) tex = new Texture2D(feed.width, feed.height, TextureFormat.RGB24, false, false);
            var prev = RenderTexture.active; RenderTexture.active = feed;
            tex.ReadPixels(new Rect(0, 0, feed.width, feed.height), 0, 0, false); RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, side + "_" + Mathf.RoundToInt((t + 100f) * 1000f).ToString("D6") + ".png"), tex.EncodeToPNG());
            Saved++;
        }
    }
}
#endif
