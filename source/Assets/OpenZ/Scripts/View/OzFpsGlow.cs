// OpenZ (GPL-3.0)
// The hidden first-person view's entry and leave hooks for the
// glows (View/OzFpsView.cs calls Entered at the entry and Left at the end of every leave):
//  - Stop NaN: the owner's white blob has a BLACK pixel at its centre: the mark of one non-finite (NaN / infinite) HDR pixel that the night
//    bloom (threshold 0.6 linear, intensity 2.0, scatter 0.80: FxV2/OpenZBloomDirector) spreads over a third of the screen. L02's game camera
//    draws with Stop NaN off (L02_Desert.unity m_StopNaN: 0) and with SMAA (m_Antialiasing: 2, no history), and URP's bloom is made again every
//    frame: a blob that STAYS after the leave needs a source that keeps drawing a broken pixel in the RTS view. So from the first entry of a
//    level the game camera keeps its Stop NaN pass on until the camera goes away with its level (URP: a non-finite pixel becomes black before
//    the post effects; a picture without one is the same to the bit; one cheap full-screen pass). Levels where the view is never used: no change;
//  - the source named: the first RTS frame after the leave and 1 s later every enabled light and renderer and the sky's / fog's globals are
//    scanned for non-finite values (NonFinite, one log line naming them); the scan changes nothing;
//  - the huge star: every veteran star image (VeteranMarks.Star(): the promotion pop, the bar star, the portrait star) drawn bigger than
//    StarMaxU canvas units (the biggest coded star is 34 u x 1.35 = 46 u) is not drawn while it is, from the leave for StarWatchSeconds (longer
//    while one is found); its path, canvas, size, scale and place are logged once (StarLast), so the next play names the object;
//  - Left: VehicleLights picks its real lights and cones for the RTS focus in the same frame (FpsLeaveNow).
// Fixes (Play check only) false = the kit's view fixes off (Stop NaN, the star guard, the lamp sizing, the re-pick), as P before the kit, for
// the check's owner repro pass. The RTS camera before the first entry, the flybys and every other camera: nothing changes.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OpenZ
{
    public static class OzFpsGlow
    {
        /// <summary>Play check only: false = the kit's view fixes off (Stop NaN, the star guard, VehicleLights' view sizing and leave re-pick).</summary>
        public static bool Fixes = true;
        /// <summary>A veteran star image drawn bigger than this (canvas units; coded at most 34 u x 1.35 = 46 u) is not drawn while it is.</summary>
        public static float StarMaxU = 64f;
        /// <summary>Seconds after the leave (and after the last oversized star found) the star guard watches.</summary>
        public static float StarWatchSeconds = 5f;
        /// <summary>Check: entries and leaves seen, the camera's Stop NaN before the first entry, true while this holds it on; oversized star
        /// frames found and the last one named; the non-finite values the scans after the leave found, and the scans run.</summary>
        public static int Entries, Leaves, StarHits, Scans; public static bool NanWas, NanHeld;
        public static string StarLast = "-";
        public static readonly List<string> NonFinite = new List<string>();

        static UniversalAdditionalCameraData data; static bool hooked;
        static float watchUntil = -1f, scanAt = -1f, findAt = -1f; static int scanFrame = -1;
        static readonly List<Image> stars = new List<Image>();
        static readonly HashSet<int> named = new HashSet<int>();
        static readonly string[] Globals = { "_FlybySunGlow", "_FlybySunShape", "_FlybySunDir", "_FlybySunDisc", "_OzFowUnder" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Unhook(); data = null; NanHeld = NanWas = false; Fixes = true; Entries = Leaves = StarHits = Scans = 0; StarLast = "-";
            NonFinite.Clear(); named.Clear(); watchUntil = scanAt = findAt = -1f; scanFrame = -1;
        }

        /// <summary>OzFpsView.Begin: Stop NaN on for the view's camera, kept on for the rest of the level (its own value kept for GiveBack).</summary>
        public static void Entered(Camera c)
        {
            Entries++;
            if (!Fixes || !c || !c.TryGetComponent(out UniversalAdditionalCameraData d)) return;
            if (NanHeld && data == d) { d.stopNaN = true; return; }
            if (NanHeld && data) data.stopNaN = NanWas;                                                  // another camera than the held one (a destroyed one is skipped)
            data = d; NanWas = d.stopNaN; d.stopNaN = true; NanHeld = true;
        }

        /// <summary>OzFpsView.Leave (the RTS camera's pose, lens, HUD and focus already back): the lights re-picked for the RTS focus now; the
        /// non-finite scans and the star guard start; Stop NaN stays on.</summary>
        public static void Left(Camera c)
        {
            Leaves++;
            if (Fixes) { var vl = VehicleLights.I; if (vl && vl.isActiveAndEnabled) vl.FpsLeaveNow(); }
            float now = Time.unscaledTime;
            watchUntil = now + StarWatchSeconds; findAt = -1f; scanFrame = Time.frameCount + 1; scanAt = now + 1f;
            if (!hooked) { Canvas.willRenderCanvases += Tick; hooked = true; }
        }

        static void Unhook() { if (hooked) { Canvas.willRenderCanvases -= Tick; hooked = false; } stars.Clear(); }

        /// <summary>Every frame after the leave, after every LateUpdate placed the HUD and before the canvases are drawn.</summary>
        static void Tick()
        {
            float now = Time.unscaledTime;
            if (scanFrame >= 0 && Time.frameCount >= scanFrame) { scanFrame = -1; Scan("the first RTS frame after the leave"); }
            else if (scanAt >= 0f && now >= scanAt) { scanAt = -1f; Scan("1 s after the leave"); }
            if (Fixes && now <= watchUntil) GuardStars(now);
            if (now > watchUntil && scanFrame < 0 && scanAt < 0f) Unhook();
        }

        // ------------------------------------------------------------------ the star guard
        static void GuardStars(float now)
        {
            if (now >= findAt)
            {   // the star images alive (pops come and go): found again 4 times a second, checked every frame
                findAt = now + 0.25f; stars.Clear(); var s = VeteranMarks.Star();
                foreach (var im in Object.FindObjectsByType<Image>(FindObjectsSortMode.None)) if (im && im.sprite == s) stars.Add(im);
            }
            for (int i = 0; i < stars.Count; i++)
            {
                var im = stars[i]; if (!im || !im.enabled || !im.gameObject.activeInHierarchy) continue;
                float u = SizeU(im);
                if (u <= StarMaxU) continue;                                                             // a NaN size fails this test too
                im.enabled = false; StarHits++;                                                          // not drawn this frame (its owner may turn it on again: checked again next frame)
                watchUntil = Mathf.Max(watchUntil, now + StarWatchSeconds);
                if (named.Add(im.GetInstanceID()))
                {
                    var rt = im.rectTransform; var root = im.canvas ? im.canvas.rootCanvas : null;
                    StarLast = $"{PathOf(im.transform)} on canvas {(root ? root.name + " (order " + root.sortingOrder + ")" : "-")}: {u:0} u (size {rt.sizeDelta:F0}, scale {rt.localScale:F2}, lossy {rt.lossyScale:F2}, at {rt.anchoredPosition:F0}, parent scale {(rt.parent ? rt.parent.localScale.ToString("F2") : "-")})";
                    Debug.Log("[OpenZ] fps_glow: a veteran star drawn at " + StarLast + "; the biggest coded star is 46 u: not drawn while it is");
                }
            }
        }

        /// <summary>A star image's drawn size in its root canvas's units (the larger side; NaN when broken).</summary>
        static float SizeU(Image im)
        {
            var rt = im.rectTransform; var r = rt.rect; var ls = rt.lossyScale;
            var root = im.canvas ? im.canvas.rootCanvas : null; float sf = root && root.scaleFactor > 0f ? root.scaleFactor : 1f;
            return Mathf.Max(Mathf.Abs(r.width * ls.x), Mathf.Abs(r.height * ls.y)) / sf;
        }

        static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the non-finite scan (names a broken source; changes nothing)
        static bool Fin(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool Fin(Vector4 v) => Fin(v.x) && Fin(v.y) && Fin(v.z) && Fin(v.w);
        static bool Fin(Matrix4x4 m) { for (int i = 0; i < 16; i++) if (!Fin(m[i])) return false; return true; }

        static void Scan(string when)
        {
            Scans++; int n0 = NonFinite.Count;
            void Add(string s) { if (NonFinite.Count < 40) NonFinite.Add(when + ": " + s); }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                var c = l.color;
                if (!Fin(l.intensity) || !Fin(l.range) || !Fin(new Vector4(c.r, c.g, c.b, c.a)) || !Fin(l.transform.localToWorldMatrix))
                    Add($"light {PathOf(l.transform)} (intensity {l.intensity}, range {l.range}, colour {c}, at {l.transform.position})");
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                if (!Fin(r.localToWorldMatrix)) Add($"renderer {PathOf(r.transform)} (its place / turn / scale)");
            }
            foreach (var g in Globals) if (!Fin(Shader.GetGlobalVector(g))) Add($"global {g} {Shader.GetGlobalVector(g)}");
            if (!Fin(Shader.GetGlobalFloat("_FlybySkyW"))) Add("global _FlybySkyW " + Shader.GetGlobalFloat("_FlybySkyW"));
            if (NonFinite.Count > n0) Debug.Log($"[OpenZ] fps_glow: non-finite values {when}: {string.Join(" | ", NonFinite.GetRange(n0, NonFinite.Count - n0))}");
        }

        /// <summary>Stop NaN back to the camera's own value (the Play check's owner repro pass; a level change drops the held camera itself).</summary>
        public static void GiveBack()
        {
            if (NanHeld && data) data.stopNaN = NanWas;
            NanHeld = false; data = null;
        }
    }
}
