#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using C = OpenZ.OzUeMushroomCurve;

namespace OpenZ
{
    /// <summary>
    /// 1.4 (editor only): the Play stage of the Unit editor's NUKE mushroom (View/OzUeMushroom.cs), menu OpenZ/V1.4/Unit Editor/Nuke
    /// Mushroom: Frames + Check (Play, the unit editor open) (Editor/OzUeMushroomMenu.cs). Through the editor's own calls (no mouse
    /// events); the field cleared before and after, the play state, the speed, the switch and the size put back. About 50 s.
    /// The field: a red and a blue squad 6 m from the drop point (inside the kill ring), two empty light tanks 55 m out (past the band:
    /// they stand in every frame for scale). The camera: the editor's start view (Fit on an empty field) centred on the drop point.
    /// Drop A, the new cloud (size pinned to 1.05: 40 m high, cap 30 m): frames at 0 / 1 / 2 / 4 / 7 / 10 s after the impact into
    /// Folder/frames_new (t00s.jpg ..). Drop B, the same field and point with the switch off (the levels' blast and mushroom, as in
    /// 1.3): the same frames into Folder/frames_old. Then a direct NukeFx.Trigger as the levels call it. Rows (report.txt):
    ///  M1 the shader in Resources, supported; the switch on; the bloom on (director, Bloom active with intensity, camera HDR + post);
    ///  M2 drop A accepted; at the impact one cloud at the drop point, 40 m high at size 1.05;
    ///  M3 the levels' mushroom of that blast left out: its cap, stem, fireball and glow shell hidden (4 of 4) at 1 and 4 s, no puff
    ///     cloud, the puff switch back as it was;
    ///  M4 the levels' blast kept: the screen flash, the flash light, the heart, the shockwave at the point, the long blast, the rumble
    ///     and the side booms in the sound log;
    ///  M5 the cloud on the source's timeline at 1, 2 and 4 s (height, width, flattening, the stem): within 1 cm / 0.5 %;
    ///  M6 the bloom picks it up: at 1, 2 and 4 s the camera rendered HDR with the post off twice (the cloud drawn and hidden): of the
    ///     pixels the cloud adds light to, the share over the bloom's linear threshold: at least 35 / 30 / 15 % (model: 70 / 59 / 36 %);
    ///  M7 the end: at 7 s nothing of the cloud drawn and the fog hole cleared; at 10 s the cloud and the levels' blast gone;
    ///  M8 drop B (switch off): no cloud of ours, the levels' cap and stem drawn, its puff cloud made when the game has the smoke channel;
    ///  M9 the levels' own call (NukeFx.Trigger): no cloud of ours, the levels' cap and stem drawn, the puff switch untouched.
    /// </summary>
    public sealed class OzUeMushroomCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ NukeMushroom]";
        public static string Report = "not run";
        public static string Folder;
        public static bool Running => run;
        static OzUeMushroomCheck run;
        static readonly float[] FrameAt = { 0f, 1f, 2f, 4f, 7f, 10f };
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_MushroomCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeMushroomCheck>();
        }

        bool changed, on0; float size0;
        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { PutBack(); if (run == this) run = null; }
        /// <summary>The switch and the size as they were (also when the run is cut short).</summary>
        void PutBack() { if (!changed) return; changed = false; OzUeMushroom.On = on0; OzUeMushroom.FixedSize = size0; }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        static Vector3 Focus(OzUnitEditor ed)
        {
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 60f, map.width * 2f - 60f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); }
            f.y = 0f; return f;
        }

        static int Played(List<OzSoundLog.Entry> es, string name)
        {
            int n = 0;
            foreach (var e in es)
                if (e.outcome != null && !e.outcome.StartsWith("SKIP", System.StringComparison.Ordinal) && !e.outcome.StartsWith("DROP", System.StringComparison.Ordinal)
                    && ((e.clip != null && e.clip.Contains(name)) || e.key == name)) n++;
            return n;
        }

        static Renderer Part(NukeFx n, string name) { var t = n ? n.transform.Find(name) : null; return t ? t.GetComponent<Renderer>() : null; }

        static int HiddenParts(NukeFx n)
        {
            int k = 0;
            foreach (var name in OzUeMushroom.LevelsMushroomParts) { var r = Part(n, name); if (r && !r.enabled) k++; }
            return k;
        }

        static float ScreenFlash()
        {
            var f = typeof(NukeFx).GetField("flash", BindingFlags.NonPublic | BindingFlags.Static);
            return f != null && f.GetValue(null) is UnityEngine.UI.Image im && im ? im.color.a : -1f;
        }

        void SetField(Vector3 f)
        {
            OzUePlace.Stop(); OzUePick.ClearInspect();
            OzUePlayground.ClearField();
            OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(6f, 0f, 0f), 1);
            OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(-6f, 0f, 0f), 1);
            OzUePlayground.Drop("light", Team.Neutral, f + new Vector3(55f, 0f, 0f), 1);
            OzUePlayground.Drop("light", Team.Neutral, f + new Vector3(-55f, 0f, 0f), 1);
        }

        static void SaveFrame(Texture2D tex, string dir, float at)
        {
            if (string.IsNullOrEmpty(dir) || !tex) return;
            try { Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, "t" + Mathf.RoundToInt(at).ToString("00") + "s.jpg"), tex.EncodeToJPG(90)); }
            catch (System.Exception e) { Debug.LogWarning(Tag + " frame not written: " + e.Message); }
        }

        static Texture2D CaptureFloat(Camera c)
        {
            int w = Mathf.Max(64, c.pixelWidth), h = Mathf.Max(64, c.pixelHeight);
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBHalf, 24) { msaaSamples = 1, sRGB = false };
            var rt = RenderTexture.GetTemporary(desc);
            var data = c.GetUniversalAdditionalCameraData();
            bool oldPost = data && data.renderPostProcessing; var oldT = c.targetTexture;
            try { if (data) data.renderPostProcessing = false; c.targetTexture = rt; c.Render(); }
            finally { c.targetTexture = oldT; if (data) data.renderPostProcessing = oldPost; }
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>M6: the camera HDR (post off) with the cloud drawn and hidden; of the pixels the cloud adds light to (max channel over
        /// 0.02), the share whose added light alone is over the bloom's linear threshold. Returns the share (0..1); `drawn` the pixel count.</summary>
        static float BloomShare(Camera cam, OzUeMushroom m, float threshold, out int drawn)
        {
            drawn = 0;
            if (!cam || !m || !m.CapRenderer) return 0f;
            var with = CaptureFloat(cam);
            bool capOn = m.CapRenderer.enabled, stemOn = m.StemRenderer && m.StemRenderer.enabled;
            m.CapRenderer.enabled = false; if (m.StemRenderer) m.StemRenderer.enabled = false;
            Texture2D without;
            try { without = CaptureFloat(cam); }
            finally { m.CapRenderer.enabled = capOn; if (m.StemRenderer) m.StemRenderer.enabled = stemOn; }
            var a = with.GetPixels(); var b = without.GetPixels();
            int over = 0;
            for (int i = 0; i < a.Length; i++)
            {
                float d = Mathf.Max(a[i].r - b[i].r, Mathf.Max(a[i].g - b[i].g, a[i].b - b[i].b));
                if (d > 0.02f) { drawn++; if (d > threshold) over++; }
            }
            Destroy(with); Destroy(without);
            return drawn > 0 ? (float)over / drawn : 0f;
        }

        IEnumerator Main()
        {
            Report = "running";
            var ed = OzUnitEditor.I;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            on0 = OzUeMushroom.On; size0 = OzUeMushroom.FixedSize; changed = true;
            var cam = ed.Rts ? ed.Rts.GetComponent<Camera>() : Camera.main;
            OzUeMushroom.On = true; OzUeMushroom.FixedSize = 1.05f;                                       // the switch on, the mean size (40 m high, cap 30 m)
            OzUeNuke.Reset();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            OzUePlayground.ClearField();
            OzUeControls.CompassHome(); OzUeControls.Fit();
            var f = Focus(ed); f.y = ed.Game != null ? ed.Game.GroundY(f.x, f.z) : 0f;
            OzUeControls.CentreOn(f);
            yield return Seconds(1.5f);

            // ---- M1 the shader, the switch, the bloom
            var sh = Resources.Load<Shader>(OzUeMushroom.ShaderRes);
            Check(sh && sh.isSupported && sh.name == OzUeMushroom.ShaderName && OzUeMushroom.On,
                  "M1 the shader " + OzUeMushroom.ShaderName + " is in Resources/" + OzUeMushroom.ShaderRes + " (the build carries it) and supported here; the switch on");
            var stack = VolumeManager.instance.stack; var bloom = stack != null ? stack.GetComponent<Bloom>() : null;
            var camData = cam ? cam.GetUniversalAdditionalCameraData() : null;
            float th = OpenZBloomDirector.I ? OpenZBloomDirector.I.LinearThreshold : 1.63f;
            Check(OpenZBloomDirector.I && bloom && bloom.IsActive() && bloom.intensity.value > 0f && cam && cam.allowHDR && camData && camData.renderPostProcessing,
                  "M1 the bloom is on in the Unit editor: director " + (OpenZBloomDirector.I ? "yes" : "NO") + ", Bloom " + (bloom && bloom.IsActive() ? "active, intensity " + bloom.intensity.value.ToString("0.00") + ", threshold " + bloom.threshold.value.ToString("0.00") + " (linear " + th.ToString("0.00") + ")" : "NOT ACTIVE")
                  + ", camera HDR " + (cam && cam.allowHDR) + ", post-processing " + (camData && camData.renderPostProcessing));
            Info("M1 night " + NightGrade.NightK.ToString("0.00") + " (the cloud's gain x " + Mathf.Lerp(1f, NukeFx.NightGainMul, NightGrade.NightK).ToString("0.00") + ")");

            // ---- drop A: the new cloud
            yield return Drop(ed, cam, f, true, Folder != null ? Path.Combine(Folder, "frames_new") : null, th);

            // ---- drop B: the switch off (the levels' look, as in 1.3)
            OzUeNuke.Reset();
            OzUeMushroom.On = false;
            yield return Drop(ed, cam, f, false, Folder != null ? Path.Combine(Folder, "frames_old") : null, th);
            OzUeMushroom.On = true;

            // ---- M9 the levels' own call
            OzUePlayground.ClearField();
            int blasts0 = OzUeMushroom.Blasts; bool puffSw = OpenZNukeMushroom.Enabled;
            var n9 = NukeFx.Trigger(f);
            yield return Seconds(1f);
            var cap9 = Part(n9, "FX_NukeCap"); var stem9 = Part(n9, "FX_NukeStem");
            Check(OzUeMushroom.Blasts == blasts0 && OzUeMushroom.Current == null && cap9 && cap9.enabled && stem9 && stem9.enabled && OpenZNukeMushroom.Enabled == puffSw,
                  "M9 the levels' own call (NukeFx.Trigger, as the forts and the endings call it): no cloud of ours (" + (OzUeMushroom.Blasts - blasts0) + "), the levels' cap and stem drawn ("
                  + (cap9 && cap9.enabled) + ", " + (stem9 && stem9.enabled) + "), the puff switch untouched (" + OpenZNukeMushroom.Enabled + ")");
            yield return Seconds(9f);

            // ---- put back
            PutBack();
            OzUePlayground.ClearField();
            OzUeNuke.Reset();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            var head = new StringBuilder();
            head.AppendLine("OzUeMushroomCheck (1.4 ue_nuke_mushroom), " + System.DateTime.Now.ToString("d MMM yyyy HH:mm") + ": " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + "/" + (pass + fail));
            head.AppendLine("frames: frames_new (this cloud) and frames_old (the switch off: the levels' look as in 1.3), t00s t01s t02s t04s t07s t10s after the impact");
            Report = head.ToString() + lines;
            Debug.Log(Tag + " " + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail) + "\n" + lines);
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n")); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
            }
            Destroy(gameObject);
        }

        /// <summary>One drop at f with the switch as given: the field, the call, the impact, the frames and the rows of that drop.</summary>
        IEnumerator Drop(OzUnitEditor ed, Camera cam, Vector3 f, bool mine, string frames, float th)
        {
            string L = mine ? "A" : "B";
            SetField(f);
            OzUeControls.CentreOn(f);
            yield return Seconds(1f);
            int impacts0 = OzUeNuke.Impacts, blasts0 = OzUeMushroom.Blasts; float wave0 = OpenZFortShockwave.LastFireTime;
            bool puffSw = OpenZNukeMushroom.Enabled; var puff0 = OpenZNukeMushroom.Current;
            float tCall = Time.unscaledTime;
            var d = OzUePlayground.Drop(OzUePlayground.Nuke, Team.Neutral, f, 1);
            if (mine) Check(d.ok && d.why == OzUeNuke.InboundWords, "M2 drop A: the call accepted (" + (d.why ?? "no words") + ")");
            else Info("drop B: the call " + (d.ok ? "accepted" : "REFUSED: " + d.why));
            float w0 = Time.realtimeSinceStartup;
            while (OzUeNuke.Impacts == impacts0 && Time.realtimeSinceStartup - w0 < 12f) yield return null;
            if (OzUeNuke.Impacts == impacts0) { Check(false, L + " the bomb never hit (12 s)"); yield break; }
            float tImp = Time.time; var at = OzUeNuke.LastImpactPoint;
            var m = OzUeMushroom.Current; var n = NukeFx.Current;
            if (mine)
            {
                Check(m && OzUeMushroom.Blasts == blasts0 + 1 && Mathf.Abs(m.transform.position.x - at.x) < 0.5f && Mathf.Abs(m.transform.position.z - at.z) < 0.5f
                      && Mathf.Abs(m.Height - C.HeightFor(1.05f, OzUeMushroom.CloudRingM)) < 0.01f && m.Height < 20f,
                      "M2 at the impact one cloud at the drop point: " + (m ? "size x" + m.Size.ToString("0.00") + ", " + m.Height.ToString("0.0") + " m high (want about 18.9 at size 1.05, under 20; 1.4: 18 % bigger), its foot "
                      + (m.transform.position - at).magnitude.ToString("0.00") + " m from the point (the source's 1 unit lift)" : "NONE"));
                Check(OzUeMushroom.LastHidden == 4 && OzUeMushroom.LastPuffsSkipped && OpenZNukeMushroom.Current == puff0 && OpenZNukeMushroom.Enabled == puffSw,
                      "M3 the levels' mushroom of this blast left out: hidden " + OzUeMushroom.LastHidden + " of 4 (cap, stem, fireball, glow shell), puff cloud "
                      + (OzUeMushroom.LastPuffsSkipped ? "not made" : "MADE") + ", the puff switch back as it was (" + OpenZNukeMushroom.Enabled + ")");
            }
            else
            {
                var capB = Part(n, "FX_NukeCap"); var stemB = Part(n, "FX_NukeStem");
                bool canPuff = OpenZFxPlayer.I && puffSw;
                Check(OzUeMushroom.Blasts == blasts0 && OzUeMushroom.Current == null && capB && capB.enabled && HiddenParts(n) == 0
                      && (!canPuff || (OpenZNukeMushroom.Current && OpenZNukeMushroom.Current != puff0)),
                      "M8 drop B, the switch off: no cloud of ours (" + (OzUeMushroom.Blasts - blasts0) + "), the levels' cap and stem drawn (" + (capB && capB.enabled) + ", stem from 0.15 s), hidden parts "
                      + HiddenParts(n) + ", the puff cloud " + (OpenZNukeMushroom.Current && OpenZNukeMushroom.Current != puff0 ? "made" : canPuff ? "NOT MADE" : "not made (no smoke channel or the switch off here)"));
            }

            // the frames, the timeline rows, the bloom rows; flash, heart and sounds watched meanwhile
            float flashPeak = 0f, lightPeak = 0f; bool heart = false; int hid1 = -1, hid4 = -1;
            var flashLight = n ? n.transform.Find("NukeFlashLight") : null; var flashL = flashLight ? flashLight.GetComponent<Light>() : null;
            var core = Part(n, "NukeCore");
            for (int k = 0; k < FrameAt.Length; k++)
            {
                float target = FrameAt[k], r0 = Time.realtimeSinceStartup;
                while (Time.time - tImp < target && Time.realtimeSinceStartup - r0 < target + 20f)                // game time (a paused field would hold it: 20 s more at most)
                {
                    flashPeak = Mathf.Max(flashPeak, ScreenFlash());
                    if (flashL && flashL.enabled) lightPeak = Mathf.Max(lightPeak, flashL.intensity);
                    heart |= core && core.enabled && core.gameObject.activeInHierarchy;
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                float tNow = Time.time - tImp;
                flashPeak = Mathf.Max(flashPeak, ScreenFlash());
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                SaveFrame(tex, frames, target);
                if (tex) Destroy(tex);
                if (!mine) continue;
                m = OzUeMushroom.Current;
                if (target == 1f || target == 2f || target == 4f)
                {
                    if (m && m.Cap)
                    {
                        float a = m.Age, H = m.Height;
                        float eRise = Mathf.Abs(m.Cap.localPosition.y - C.Rise(a, H)), eR = Mathf.Abs(m.Cap.localScale.x - C.CapRadius(a, H)) / Mathf.Max(0.01f, C.CapRadius(a, H)),
                              eFlat = Mathf.Abs(m.Cap.localScale.y - C.CapRadius(a, H) * C.Flatten(a)) / Mathf.Max(0.01f, C.CapRadius(a, H)),
                              eStem = Mathf.Abs(m.Stem.localScale.y - C.StemHeight(a, H)) + Mathf.Abs(m.Stem.localScale.x - C.StemRadius(a, H)) + Mathf.Abs(m.Stem.localPosition.y - C.Rise(a, H) * 0.5f);
                        Check(eRise < 0.01f && eR < 0.005f && eFlat < 0.005f && eStem < 0.02f && m.Stem.gameObject.activeSelf == C.StemShown(a),   // 1.4 ue_nuke_stem_gone: at 4 s the stem is gone (C.StemFadeEnd 3.13 s)
                              "M5 " + target.ToString("0") + " s (age " + a.ToString("0.00") + "): the cap " + m.Cap.localPosition.y.ToString("0.00") + " m up, " + m.Cap.localScale.x.ToString("0.00") + " m wide (radius), "
                              + (m.Cap.localScale.y / Mathf.Max(0.01f, m.Cap.localScale.x)).ToString("0.00") + " flat; the stem " + m.Stem.localScale.y.ToString("0.00") + " m tall, " + m.Stem.localScale.x.ToString("0.00")
                              + " m wide: the source's timeline (errors " + eRise.ToString("0.000") + " m, " + (eR * 100f).ToString("0.00") + " %, " + (eFlat * 100f).ToString("0.00") + " %, stem " + eStem.ToString("0.000") + "); "
                              + "the stem drawn " + m.Stem.gameObject.activeSelf + " (want " + C.StemShown(a) + ": its fade " + C.StemFadeFrom.ToString("0.00") + " - " + C.StemFadeEnd.ToString("0.00") + " s)");
                        int drawn; float share = BloomShare(cam, m, th, out drawn);
                        float bar = target < 1.5f ? 0.15f : target < 3f ? 0.20f : 0.15f;
                        Check(drawn > 2000 && share >= bar, "M6 " + target.ToString("0") + " s: the cloud adds light to " + drawn + " px; " + (share * 100f).ToString("0.0") + " % of them over the bloom's linear threshold "
                              + th.ToString("0.00") + " (bar " + (bar * 100f).ToString("0") + " %, gain " + m.GainNow.ToString("0.00") + ")");
                    }
                    else Check(false, "M5/M6 " + target.ToString("0") + " s: no cloud");
                    if (target == 1f) hid1 = HiddenParts(n); else if (target == 4f) hid4 = HiddenParts(n);
                }
                if (target == 7f)
                {
                    var hole = Shader.GetGlobalVector("_AtmoHole");
                    Check(m == null || (m.Ended && !m.CapRenderer.enabled && !m.StemRenderer.enabled), "M7 at " + tNow.ToString("0.0") + " s nothing of the cloud drawn (" + (m ? (m.Ended ? "ended at its " + C.Life.ToString("0.0") + " s" : "STILL DRAWN") : "gone") + ")");
                    Check(hole.w <= 0f, "M7 at 7 s the fog hole cleared (w " + hole.w.ToString("0.0") + ")");
                }
                if (target == 10f)
                    Check(OzUeMushroom.Current == null && !GameObject.Find("UeNukeMushroom") && NukeFx.Current == null, "M7 at 10 s the cloud and the levels' blast are gone (cloud " + (OzUeMushroom.Current ? "STILL THERE" : "gone") + ", NukeFx " + (NukeFx.Current ? "STILL THERE" : "gone") + ")");
            }
            if (mine)
            {
                Check(hid1 == 4 && hid4 == 4, "M3 the levels' cap, stem, fireball and glow shell stay hidden while NukeFx runs on: " + hid1 + " of 4 at 1 s, " + hid4 + " of 4 at 4 s");
                var es = OzSoundLog.Since(tCall);
                int big = Played(es, "legacy-nuke-long"), rumble = Played(es, "collapse"), booms = Played(es, "fortboom");
                Check(flashPeak >= 0.3f && lightPeak > 100f && heart, "M4 the levels' flash kept: the screen flash up to " + flashPeak.ToString("0.00") + " (0.38 at the blast), the flash light up to " + lightPeak.ToString("0") + ", the white-hot heart " + (heart ? "drawn" : "NOT DRAWN"));
                Check(OpenZFortShockwave.LastFireTime > wave0 && (OpenZFortShockwave.LastCentre - at).sqrMagnitude < 4f, "M4 the levels' shockwave (ring, dust wall, shove) fired at the drop point");
                Check(big >= 1 && rumble >= 1 && booms >= 1, "M4 the levels' sounds: the long blast " + big + ", the rumble " + rumble + ", the side booms " + booms + " (want each at least 1)");
            }
            else Info("drop B: the screen flash up to " + flashPeak.ToString("0.00") + ", the flash light up to " + lightPeak.ToString("0") + ", the heart " + (heart ? "drawn" : "not drawn"));
            yield return Seconds(1f);
        }
    }
}
#endif
