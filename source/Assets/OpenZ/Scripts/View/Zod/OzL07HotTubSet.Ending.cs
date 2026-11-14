// OpenZ (GPL-3.0) - Level 7: the hot tub as the picture of the red side's win (IOzEndingSet of ZodBriefing.EndingScene.cs). The enemy fort's mushroom stands behind them.
// The ending routine of level 16 runs the scene: clock on the voice, bars, subtitles, jaws, camera, ears, sky, ducking, the end screen. This file only stands the set up and plays its clips.
//  - Bound in Awake, because the set is built when a line asks for it and a skipped briefing asks for nothing: Ready builds it then, behind the hard cut.
//  - Begin: the briefing's state is undone (point shape, bed, lights), the set turns so the fort stands straight behind the two robots, every object goes to layer 0 so the game camera draws it, and only from this cut on.
//    Alan takes the lounger kit's body with the grip shape and his can in the left hand, Brad's can moves to his right hand: both inner hands, or the cans cannot meet. All of it from Resources/OpenZ/L07Cut/l07_tub_end.json.
//  - Beats: the cue sheet's names (Resources/OpenZ/Zod/ending_L07_cues.json). The clink is in the clips: the two cans tilt toward each other, their top rims meet, the contact is held 0.1 s.
//  - The bed fades when the end screen comes, on both the played and the skipped path.
//  - From the cut on the shot shows the far blast only: the fort wave's ground band and its air refraction are made for the high game camera. From this low lens the band is a white stripe over the ground and the refraction doubles the cans, so its renderers go off from the cut. The rocks' see-through holes for the units behind them go off too: those units are out of the picture. The blast's heat shimmer goes off too: it reads the picture from before the painted sky is drawn, a grey disc round the cloud from this low lens. So does the fireball's hole in the fog, every frame: made for ground under the high camera, here it would open the sky behind the cloud (the cloud's own column and ball, ZodBriefing.EndingHold.cs, keep its haze right). The painted sky is full on the cut's first frame.
//  - The clips go by the scene's clock, the voice's own samples, not by frame time: a long frame cannot put one robot behind the other or both behind the line. All clips load before the cut.
//  - Row 50 (the owner on the first record): the water shows a little of what lies under it near the robots, their hips and thighs, so they read as sitting
//    in water (OpenZL07TubSeeThru.shader, a second draw of the water: the water itself goes to the first transparent slot, so the opaque picture and the depth
//    texture hold what is under it, and it throws no shadow on that). The steam is soft: faded where it comes near anything (soft particles on the depth
//    texture, so the water's surface no longer cuts its puffs in half), lit by the scene's own lights at night, a little thinner, a little higher. Alan's can is
//    back in the seat level 16 gives this grip, his clips solved again for it: the hand the right way round. All of it from l07_tub_end.json, this ending only.
// Play check: OzL07HotTubSet.EndStatus().
// WRITTEN BY READING ONLY: compiled off-line with the project's code, never run in the engine. Remove this line once it runs.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>fortLeftDeg: as ZodBriefing.EndingFortLeftDeg. cam*: as the routine's statics. upPower: the lava's glow on them in this scene only. steamRate: this lens stands lower than the briefing's, so the steam is thinner here, or it reaches Alan's chin line. clinkD: the two cans' centres at contact (m), for the check. seeMost, seeDepth: the water's see-through at its surface (0..1) and the depth under it where it is gone (m); 0 = the water as in the briefing. seeNear: only within this of either robot's hips, across the ground (m), so the tub's walls and bench keep the water's colour. steamAlpha: the puffs' alpha, steamSoft: their soft fade near anything (m of eye depth; 0 = as in the briefing), steamLift: the steam's source over the water (m).</summary>
    [Serializable] public class OzTubEndStage { public float fortLeftDeg, camX, camY, camZ, camLeftDeg, camUpDeg, camFov, push, mushroomScale, mushroomLife, upPower, steamRate, glowPower, glowRange, bedDb, bedFade, clinkD, seeMost, seeDepth, seeNear, steamAlpha, steamSoft, steamLift; }
    [Serializable] public class OzTubEndFile { public int version; public string level, alanBody; public OzTubEndStage stage; public OzLoungerCan alanCan, bradCan; public OzLoungerClip[] clips; }

    public sealed partial class OzL07HotTubSet : IOzEndingSet
    {
        public const string EndPath = "OpenZ/L07Cut/l07_tub_end";
        /// <summary>Play check: the two cans' centres on the frame the clink's beat came (m), the least over the scene, frames with both heads in the picture, frames counted.</summary>
        public static float EndClinkD = -1f, EndClinkLeast = -1f; public static int EndHeadsIn, EndFrames;

        OzTubEndFile end; bool endOn, endClinkWait, bedOut, endShimmerSet; Light endGlow; float endGlowAt = -100f, endShiverAt = -100f, bedFrom, endClockWas, endShimmerWas; Vector3 endFort;
        readonly List<AnimationClip> endAnims = new List<AnimationClip>();
        static readonly int FogHole = Shader.PropertyToID("_AtmoHole");

        void BindEnding() { ZodBriefing.EndingSet = this; EndClinkD = EndClinkLeast = -1f; EndHeadsIn = EndFrames = 0; }
        void UnbindEnding() { if (ReferenceEquals(ZodBriefing.EndingSet, this)) ZodBriefing.EndingSet = null; if (endShimmerSet) { endShimmerSet = false; NukeFx.ShimmerStrength = endShimmerWas; } }

        bool LoadEnd()
        {
            if (end != null) return true;
            var ta = Resources.Load<TextAsset>(EndPath);
            if (!ta) { Warn("data file Resources/" + EndPath + " missing: no ending scene"); return false; }
            var f = JsonUtility.FromJson<OzTubEndFile>(ta.text);
            if (f == null || f.stage == null || f.clips == null || f.clips.Length == 0 || f.alanCan == null || f.bradCan == null) { Warn("data file " + EndPath + " not readable"); return false; }
            var all = new List<OzLoungerClip>(data.clips); all.AddRange(f.clips); data.clips = all.ToArray();
            foreach (var c in f.clips) { var ac = Resources.Load<AnimationClip>(c.res); if (ac) endAnims.Add(ac); }   // behind the hard cut, not on a beat in the middle of the line
            end = f;
            return true;
        }

        bool EndBody(Robot r, string res)
        {   // the body with the grip shapes, same skeleton, bound by bone name (as Build does for Brad)
            var bm = Resources.Load<GameObject>(res); var src = bm ? bm.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (!r.body || !src || !src.sharedMesh) { Warn(r.who + ": the body " + res + " is missing"); return false; }
            var bones = new Transform[src.bones.Length];
            for (int i = 0; i < bones.Length; i++) { bones[i] = src.bones[i] ? FindDeep(r.root, src.bones[i].name) : null; if (!bones[i]) { Warn(r.who + ": bone " + (src.bones[i] ? src.bones[i].name : "?") + " missing"); return false; } }
            r.body.sharedMesh = src.sharedMesh; r.body.bones = bones;
            return true;
        }

        void EndCan(Robot r, OzLoungerCan cd, float scale)
        {   // the seat the grip was fitted on, one standard can for both robots
            int k = cd.hand == "R" ? 1 : 0;
            r.can[k] = FindDeep(r.root, cd.node); r.seat[k] = cd;
            if (!r.can[k]) { Warn(r.who + ": can node " + cd.node + " missing"); return; }
            r.can[k].localPosition = new Vector3(cd.px, cd.py, cd.pz); r.can[k].localRotation = new Quaternion(cd.qx, cd.qy, cd.qz, cd.qw); r.can[k].localScale = Vector3.one * scale;
            r.can[k].gameObject.SetActive(true);
            foreach (var x in r.can[k].GetComponentsInChildren<Renderer>(true)) x.enabled = true;
            r.grip[k] = -1;
            var m = r.body ? r.body.sharedMesh : null;
            if (m) for (int i = 0; i < m.blendShapeCount; i++) { string n = m.GetBlendShapeName(i); if (n == cd.grip || n.EndsWith("." + cd.grip)) r.grip[k] = i; }
            if (r.grip[k] >= 0) r.body.SetBlendShapeWeight(r.grip[k], 100f); else Warn(r.who + ": no shape " + cd.grip + " on the body");
        }

        public Transform Begin(Vector3 fort, bool won)
        {
            if (!EnsureBuilt() || !LoadEnd()) return null;
            if (briefOn) BriefEnd();
            var s = end.stage; endFort = fort;
            ZodBriefing.EndingFortLeftDeg = s.fortLeftDeg; ZodBriefing.EndingCamPos = new Vector3(s.camX, s.camY, s.camZ); ZodBriefing.EndingCamLeftDeg = s.camLeftDeg; ZodBriefing.EndingCamUpDeg = s.camUpDeg; ZodBriefing.EndingCamFov = s.camFov;
            ZodBriefing.EndingPushWin = s.push; ZodBriefing.EndingMushroomLife = s.mushroomLife; ZodBriefing.EndingMushroomScale = s.mushroomScale;
            root.rotation = ZodBriefing.EndingSetRotation(root.position, fort);
            foreach (var w in FindObjectsByType<OpenZFortShockwave>(FindObjectsSortMode.None)) foreach (var mr in w.GetComponentsInChildren<MeshRenderer>(true)) mr.enabled = false;   // the wave still shoves and ends as ever, it is only not drawn in this shot
            var holes = FindFirstObjectByType<RockSeeThru>(); if (holes) holes.enabled = false;               // its OnDisable clears the holes
            if (!endShimmerSet) { endShimmerSet = true; endShimmerWas = NukeFx.ShimmerStrength; NukeFx.ShimmerStrength = 0f; }   // back when the level is left
            FlybySky.Ensure().CutFull();                                                                      // the routine switches it on next: without this it fades in from the plain sky over 1 s
            Show(true); SetWalker(false); SetArm(false);
            if (armLight) armLight.enabled = false; if (rockLight) rockLight.enabled = false;
            var up = root.Find("LavaUplight"); var upLight = up ? up.GetComponent<Light>() : null; if (upLight && s.upPower > 0f) upLight.intensity = s.upPower;
            TickShapes(-99f);                                                                                 // a skip inside "See?" leaves the point on, and the can goes into that hand
            var a = Find("alan"); var b = Find("brad");
            var st = FindFirstObjectByType<ZodStage>(); float alanMul = st ? st.alanScale : 1.19f;
            if (EndBody(a, end.alanBody)) EndCan(a, end.alanCan, 1f / Mathf.Max(0.01f, alanMul));
            if (b.can[0]) b.can[0].gameObject.SetActive(false);
            if (b.grip[0] >= 0) b.body.SetBlendShapeWeight(b.grip[0], 0f);
            b.seat[0] = null; EndCan(b, end.bradCan, 1f);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;          // as the level 16 set stands: the game camera draws it, the stage's scan leaves it, no camera mask changes
            foreach (var r in robots) { r.cues.Clear(); r.jaw = 0f; Play(r, "end_sit", 0f); r.graph.Evaluate(0f); }   // now: this runs after Update, and the cut's first picture must already hold the new pose
            EndWater(s); EndSteam(s);                                                                         // row 50: before the steam plays, so its first puffs are already the soft ones
            if (steam) { if (s.steamRate > 0f) { var em = steam.emission; em.rateOverTime = s.steamRate; } steam.Play(); }
            voiceGain = AudioDirector.I ? AudioDirector.I.VoiceBusLinear("Voice/Commander") : 1f;
            var bedClip = Clip(BedRes);
            if (bedClip && bed)
            {   // a skip in the crash shot leaves the bed muted
                bed.Stop(); bed.clip = bedClip; bed.mute = false; bed.pitch = 1.35f; bed.volume = bedFrom = Mathf.Clamp01(voiceGain * Mathf.Pow(10f, s.bedDb / 20f)); bed.Play();
                OzSoundLog.Add("L07Tub", "end_tub_bubbling", bedClip.name, "loop on", bed.volume);
            }
            else OzSoundLog.Add("L07Tub", "end_tub_bubbling", BedRes, "missing clip");
            bedOut = false; endGlowAt = endShiverAt = -100f; EndClinkD = EndClinkLeast = -1f; EndHeadsIn = EndFrames = 0; ZodBriefing.EndingClock = endClockWas = 0f;
            endOn = true;
            Debug.Log($"[OpenZ L07 tub] ending: the set turned to the fort at ({fort.x:F0}, {fort.z:F0}), heading {root.eulerAngles.y:F1}, camera ({s.camX:F2}, {s.camY:F2}, {s.camZ:F2}) left {s.camLeftDeg:F1} up {s.camUpDeg:F1} lens {s.camFov:F0}, far cloud x {s.mushroomScale:F2}");
            return root;
        }

        // ------------------------------------------------------------------------------------------------ row 50: the water and the steam of this ending
        /// <summary>The material that keeps OpenZL07TubSeeThru in the build, as M_L07_LegHot does for its shader.</summary>
        public const string SeeThruRes = "OpenZ/L07Cut/M_L07_TubSeeThru";
        /// <summary>The first slot after the opaque picture is taken: in this ending the water itself is drawn there, its see-through just after it.</summary>
        const int WaterQueue = 2501;
        /// <summary>Play check: the water's see-through draw is on.</summary>
        public static bool EndSeeThruOn;

        void EndWater(OzTubEndStage s)
        {   // what lies just under the surface shows a little (near the robots: their hips and thighs), deeper the water keeps its own colour; its material, picture and foam stay
            EndSeeThruOn = false;
            if (s.seeMost <= 0f || !water) return;
            var wr = water.GetComponentInChildren<MeshRenderer>(true); var mf = wr ? wr.GetComponent<MeshFilter>() : null; var top = wr ? wr.sharedMaterial : null; var src = Resources.Load<Material>(SeeThruRes);
            if (!top || !src || !mf || !mf.sharedMesh || mf.sharedMesh.subMeshCount != 1) { Warn("the water's see-through (" + SeeThruRes + ") cannot go on: the water stays as in the briefing"); return; }
            top.renderQueue = WaterQueue;                                                                     // drawn just after the opaque picture is taken: that picture and the depth texture hold what is under it
            wr.shadowCastingMode = ShadowCastingMode.Off;                                                     // water throws no shadow on what lies in it
            var m = new Material(src) { name = "L07Tub_WaterSeeThru", hideFlags = HideFlags.DontSave }; mats.Add(m);
            if (top.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", top.GetTexture("_BaseMap"));
            m.SetFloat("_WaterY", wr.bounds.center.y); m.SetFloat("_SeeMost", Mathf.Clamp01(s.seeMost)); m.SetFloat("_SeeDepth", Mathf.Max(0.05f, s.seeDepth)); m.renderQueue = WaterQueue + 1;
            var ra = Find("alan"); var rb = Find("brad"); var ha = ra != null ? FindDeep(ra.root, "Hips") : null; var hb = rb != null ? FindDeep(rb.root, "Hips") : null;
            if (s.seeNear > 0f && ha && hb) { m.SetVector("_NearA", ha.position); m.SetVector("_NearB", hb.position); m.SetFloat("_NearR", s.seeNear); }   // near the two of them only: the walls and the bench keep the water's colour
            wr.sharedMaterials = new[] { top, m };                                                            // one mesh of one part: the second material draws the same water again, over it
            EndSeeThruOn = true;
            Debug.Log($"[OpenZ L07 tub] ending water: see-through {s.seeMost:F2} at the surface, gone {s.seeDepth:F2} m under it, surface y {wr.bounds.center.y:F2}");
        }

        void EndSteam(OzTubEndStage s)
        {   // no hard line where the steam meets the water or a robot, no pale band at night: the steam stays, softer
            var pr = steam ? steam.GetComponent<ParticleSystemRenderer>() : null; var m = pr ? pr.sharedMaterial : null;
            if (!m) return;
            if (s.steamSoft > 0f) { m.SetFloat("_SoftFade", s.steamSoft); m.SetFloat("_ZTest", (float)CompareFunction.Always); }   // faded near anything in the depth texture (robots, rim, bubbles; not the water now): the surface cuts no puff in half
            m.SetFloat("_Lit", Mathf.Clamp01(DetailKit.Night));                                               // lit by the scene's own lights at night (the lava's glow, the blast); by day as before
            var mn = steam.main; var c = mn.startColor.color; if (s.steamAlpha > 0f) c.a = s.steamAlpha; mn.startColor = c;
            if (s.steamLift > 0f) steam.transform.localPosition = new Vector3(0f, WaterH + s.steamLift, 0f);
        }

        public void Beat(string who, string beat)
        {
            if (!built) return;
            switch (beat)
            {
                case "blast_glow_on": EndGlowOn(); ZodBriefing.EndingShake(1f, 0.5f); return;
                case "boom_shiver": endShiverAt = Time.unscaledTime; ZodBriefing.EndingShake(0.7f, 0.85f); return;
                case "clink": endClinkWait = true; return;
                case "cut_two_shot": return;
                case "end_screen": Debug.Log("[OpenZ L07 tub] " + EndStatus()); return;
            }
            foreach (var w in who == "both" ? new[] { "alan", "brad" } : new[] { who })
            {
                var r = Find(w); if (r != null && Play(r, beat, 0.15f) && ZodBriefing.EndingBeatLate > 0f) r.slot[r.cur].SetTime(ZodBriefing.EndingBeatLate);   // the beat came on the first frame past its time: the clip that much in, so both clips land on the sheet's clink
            }
        }

        public void Jaw(string who, float open01) { var r = Find(who); if (r != null) r.jaw = open01; }

        public Vector3 Point(string at)
        {
            if (at == "alan" || at == "brad") { var r = Find(at); return r != null && r.head ? r.head.position : root.position; }
            var a = Find("alan"); var b = Find("brad");
            if (at == "between" && a != null && b != null && a.can[0] && b.can[1]) return (a.can[0].position + b.can[1].position) * 0.5f;
            return root.position;
        }

        void EndGlowOn()
        {   // the fireball's warm light from the fort's side, as level 16's
            if (!endGlow)
            {
                var go = new GameObject("L07TubBlastGlow"); go.transform.SetParent(root, false);
                endGlow = go.AddComponent<Light>(); endGlow.type = LightType.Point; endGlow.color = new Color(1f, 0.55f, 0.2f); endGlow.shadows = LightShadows.None; endGlow.range = end.stage.glowRange;
            }
            Vector3 to = endFort - root.position; to.y = 0f; to = to.sqrMagnitude > 1f ? to.normalized : root.forward;
            endGlow.transform.position = root.position + to * 6f + Vector3.up * 3f;
            endGlowAt = Time.unscaledTime; endGlow.enabled = true;
        }

        void TickEnding()
        {
            float dt = AudioListener.pause ? 0f : Time.unscaledDeltaTime;
            float clock = ZodBriefing.EndingClock, step = OpenZVictory.HoldEndScreen ? Mathf.Max(0f, clock - endClockWas) : dt;   // the clips on the voice's clock; once the end screen is up that clock has stopped and real time goes on
            endClockWas = clock;
            TickLife(Mathf.Min(dt, 0.1f));
            foreach (var r in robots)
            {
                if (r.cur >= 0 && r.fadeDur > 0f && r.fadeT < r.fadeDur)
                {
                    r.fadeT = Mathf.Min(r.fadeDur, r.fadeT + dt); float k = r.fadeT / r.fadeDur; k = k * k * (3f - 2f * k);
                    r.mixer.SetInputWeight(r.cur, k); if (r.slot[1 - r.cur].IsValid()) r.mixer.SetInputWeight(1 - r.cur, 1f - k);
                }
                if (r.clip != null && !r.clip.loop && !string.IsNullOrEmpty(r.clip.hold) && r.cur >= 0 && r.slot[r.cur].IsValid() && r.slot[r.cur].GetTime() >= r.clip.seconds)
                    Play(r, r.clip.hold, r.clip.holdFade > 0f ? r.clip.holdFade : 0.3f);
                r.graph.Evaluate(step);
            }
            float now = Time.unscaledTime;
            if (endGlow && endGlow.enabled)
            {   // the fireball's own life, double for 0.4 s on the far boom; under the calm-screen option no flicker, no jump
                float age = now - endGlowAt, k = Mathf.Clamp01(1f - (age - 2f) / 6.5f); bool calm = OzMotion.Reduced;
                float flick = calm ? 1f : 0.9f + 0.1f * Mathf.PerlinNoise(now * 1.7f, 3.1f), jump = !calm && now - endShiverAt < 0.4f ? 2f : 1f;
                endGlow.intensity = end.stage.glowPower * k * flick * jump;
                if (k <= 0f) endGlow.enabled = false;
            }
            if (bed && bed.isPlaying && !OpenZVictory.HoldEndScreen)
            {   // the end screen is up, by the scene's end or by a skip: the bed goes under it
                bedOut = true;
                bed.volume = Mathf.MoveTowards(bed.volume, 0f, bedFrom * dt / Mathf.Max(0.05f, end.stage.bedFade));
                if (bed.volume <= 0f) { bed.Stop(); OzSoundLog.Add("L07Tub", "end_tub_bubbling", "", "loop off"); }
            }
        }

        void LateEnding()
        {
            Shader.SetGlobalVector(FogHole, Vector4.zero);                                                    // written by the blast every Update
            foreach (var r in robots)
            {
                for (int k = 0; k < 2; k++)
                    if (r.can[k] && r.seat[k] != null) r.can[k].localPosition = new Vector3(r.seat[k].px, r.seat[k].py, r.seat[k].pz);
                if (r.mouth) r.mouth.localRotation = r.mouthRest * Quaternion.Euler(r.jaw * r.jawDeg * r.jawSign, 0f, 0f);
            }
            var a = Find("alan"); var b = Find("brad");
            if (a != null && b != null && a.can[0] && b.can[1])
            {
                float d = Vector3.Distance(a.can[0].position, b.can[1].position);
                if (EndClinkLeast < 0f || d < EndClinkLeast) EndClinkLeast = d;
                if (endClinkWait) { endClinkWait = false; EndClinkD = d; }
            }
            var c = Camera.main;
            if (c && a != null && b != null && a.head && b.head)
            {   // the level 16 hold counts read that set only, so this scene counts its own heads
                Vector3 p = c.WorldToViewportPoint(a.head.position), q = c.WorldToViewportPoint(b.head.position);
                EndFrames++; if (p.z > 0f && q.z > 0f && p.x > 0.02f && p.x < 0.98f && q.x > 0.02f && q.x < 0.98f && p.y > 0.11f && p.y < 0.94f && q.y > 0.11f && q.y < 0.94f) EndHeadsIn++;
            }
        }

        /// <summary>One line for the Play check. A good clink: the first number within 1 cm of the data's.</summary>
        public static string EndStatus()
        {
            if (!I || !I.endOn) return "hot tub ending: not on";
            float want = I.end != null ? I.end.stage.clinkD : 0f;
            return $"hot tub ending: can centres {EndClinkD:F3} m on the clink's frame, data {want:F3}, least {EndClinkLeast:F3}, both heads in the picture {EndHeadsIn} of {EndFrames} frames, game camera can draw {MainCanDraw()} renderers, bed fading {I.bedOut}, playing {(I.bed && I.bed.isPlaying)}" + (LastWarning.Length > 0 ? " | last warning: " + LastWarning : "");
        }
    }
}
