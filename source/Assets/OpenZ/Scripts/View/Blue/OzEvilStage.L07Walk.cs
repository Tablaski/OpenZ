// OpenZ (GPL-3.0) - Level 7, blue side: the briefing "L07_start" is not played in the evil HQ. For this one scene the stage is carried out onto the level's
// real lava and walks: EvilZod in front, straight through it, boots under the surface; BS-01 far behind him, about a third of his speed, a little LOWER WITH EVERY STEP;
// it ends on "Then walk faster." In the same corner window as every blue briefing (the stage camera renders into the feed), the fly-over behind it.
//  - The room is hidden (every renderer and room light of the set), the two robots keep their rigs, faces, beats and stage lights; the stage camera sees
//    the level as well (the lava sheet, the banks, the volcanoes), far clip 500.
//  - Stage space: the stage's origin stands on the lava (local y 0 = the lava surface), local +z = the way they walk, toward the lens.
//  - Their legs go under the sheet by the depth test, deep enough from the first frame that no boot breaks the surface, hops included. These two are not TeamLit, so the hot metal is a second skinned
//    draw of each body, OpenZ/L07LegHot (the lava's own ramp at LV3's full heat, a smooth rising edge under the fists, the plates kept readable); a dark collar, a hot line, ripples and smoke at each leg (OzL07CutFx.Wade). The
//    lava's light on them is a soft warm light well clear of the legs, or it burns them flat. They walk west along the lake, clear of cones 02, 05 and vent 08 (tools/09_sight_check.py).
//  - The stage is unloaded after the briefing (OzBlueCameo.UnloadStage): the nuke scene and the endings build a fresh one in the HQ, far from here.
//  - EvilZod talks to the lens here: BS-01 is behind him, so "look_sk" turns into "look_cam" while LavaWalkOn (the beat switch in OzEvilStage.EvilZod.cs). While the lens films BS-01 it stands beside EvilZod, so in those shots he looks where he walks.
//    level with BS-01's words); BS-01's salute beside his brow (his fist went through his face); EvilZod's ez_med crash zoom looks 2.5 deg lower (too much sky).
// Numbers: Resources/OpenZ/L07Cut/l07_walk_scene.json. Play check: OzEvilStage.WalkStatus.
// WRITTEN BY READING ONLY: compiled off-line with the project's code, never run in the engine. Remove this line once it runs.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    [Serializable]
    public class OzWalkFile
    {
        public int version; public string scene; public float placeX, placeZ, yaw;
        public float ezX, ezZ, skX, skZ, ezSink, skSinkFrom, skSinkStep, skSinkMost, ezRate, skRate, farClip, volumeSize, dofStart, dofEnd;
        public float ezHeatSeconds, ezHeatA, ezHeatB, skHeatA, skHeatB, ezTopA, ezTopB, skTopA, skTopB, ezLegSize, skLegSize, ezLight, skLightA, skLightB, lightUp;
        public string[] stepClips;
        /// <summary>Round 3: his shots' tilt (deg, plus = the lens over the aim looks down), how far they reach under his hips and over his hat (m), the head's most turn from the chest (deg), the walk's sound (db under the voice's gain).</summary>
        public float ezMedTilt, ezCloseTilt, twoTilt, ezMedBelow, ezMedOver, ezCloseOver, headTurnMax, bedDbA, bedDbB, bedPitch, meltDb, popDb, popEveryA, popEveryB;
        public string bedClip; public string[] meltClips, popClips;
        /// <summary>Round 3 second fix: a melting gloop every meltEvery-th step of BS-01 (they run about 3 s); the music's duck under this scene, dB, under the other briefings' -8, so the lava is the bed here; the soft glows at the legs.</summary>
        public int meltEvery; public float musicDuckDb, ezGlow, skGlowA, skGlowB;
        /// <summary>
        /// fly-over stood level with BS-01's words) ducked this many dB while the walk plays, on CombatAudio's world / vehicle buses (the deepest duck wins,
        /// 0 = none); EvilZod's landed crash zoom on ez_med tilts down this many deg (LavaCrashTilt, 0 = none).</summary>
        public float worldDuckDb, ezCrashTilt;
        /// <summary>The gain each line of this scene's voice file carries over the approved take (tools/18_voice_mix.py): mouthT = line start (s), mouthDb = what takes it back. The jaw follows the take as it was approved, not the mix.</summary>
        public float[] mouthT, mouthDb;
    }

    public sealed partial class OzEvilStage
    {
        public const string LavaWalkScene = "L07_start", LavaWalkData = "OpenZ/L07Cut/l07_walk_scene";
        /// <summary>True while this stage is the lava walk (this scene only).</summary>
        public bool LavaWalkOn { get; private set; }
        /// <summary>Play check: the walk's state in one line.</summary>
        public string WalkStatus => LavaWalkOn && walk != null ? $"lava walk: EvilZod z {(ez != null && ez.Ok ? ez.Root.localPosition.z : 0f):F2}, BS-01 z {(sk != null && sk.Ok ? sk.Root.localPosition.z : 0f):F2}, sunk {skSink:F2} m after {skSteps} steps, head most {walkHeadMost:F1} deg from the chest, bed {(walkBed && walkBed.isPlaying ? "on" : "off")}, {walkMelts} melts, {walkPops} pops, game camera can draw {WalkMainCanDraw()}" : "lava walk: off";
        /// <summary>Play check: the stage's renderers the game camera could draw now. 0 is the only good answer.</summary>
        public int WalkMainCanDraw()
        {
            var main = Camera.main; if (!main || main == cam) return 0;
            int n = 0; foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && (main.cullingMask & (1 << r.gameObject.layer)) != 0) n++;
            return n;
        }

        OzWalkFile walk; float skSink, skSinkGoal, lavaWorldY; int skSteps, ezStepParity = -1, skStepSeen = -1; Light ezLava, skLava; MeshRenderer ezGlow, skGlow, skRing; OzL07CutFx.Wade ezWade, skWade; Material ezHot, skHot;
        AudioSource walkBed, walkMelt, walkPop, walkCrash; float walkPopAt, walkDuckAt, walkHeadMost, walkCrashSeen = -1f; int walkMelts, walkPops;

        /// <summary>BeginScene: the lava walk starts, or nothing at all for every other scene.</summary>
        void BeginLavaWalk(OzBlueScene s)
        {
            if (s == null || s.id != LavaWalkScene) return;
            var ta = Resources.Load<TextAsset>(LavaWalkData);
            walk = ta ? JsonUtility.FromJson<OzWalkFile>(ta.text) : null;
            if (walk == null) { Debug.LogWarning("[EvilZod] Resources/" + LavaWalkData + " missing: the scene plays in the HQ"); return; }
            LavaWalkOn = true; MouthWalk = walk;
            float lavaY = OzLava.I ? OzLava.I.lavaY : -0.3f;
            var mark = GameObject.Find("MRK_lava_level"); if (mark) lavaY = mark.transform.position.y;
            lavaWorldY = lavaY;
            transform.SetPositionAndRotation(new Vector3(walk.placeX, lavaY, walk.placeZ), Quaternion.Euler(0f, walk.yaw, 0f));
            HideRoom();
            if (cam)
            {
                var main = Camera.main; cam.farClipPlane = walk.farClip; cam.useOcclusionCulling = false;
                if (main) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; cam.cullingMask = (main.cullingMask & ~(1 << 5)) | (1 << Layer); }
            }
            var box = vol ? vol.GetComponent<BoxCollider>() : null; if (box) box.size = Vector3.one * walk.volumeSize;
            if (dof != null) { dof.gaussianStart.Override(walk.dofStart); dof.gaussianEnd.Override(walk.dofEnd); }
            if (ez != null && ez.Ok && !ez.Rig.HasWalk) ez.Rig.LoadWalk(Folder, "EvilZod");
            if (ez != null && ez.Ok) { ez.Rig.WalkRate = walk.ezRate; ez.Rig.StartWalkCut(); }
            skSink = skSinkGoal = walk.skSinkFrom; skSteps = 0; ezStepParity = -1; skStepSeen = -1;
            if (sk != null && sk.Ok && !sk.Rig.HasHinges) { sk.Rig.MeasureHinges(Folder, "Sidekick", "Walk"); Debug.Log("[EvilZod] lava walk: BS-01 hinges: " + sk.Rig.HingeReport); }   // gap 26: his arms on the elbow hinge for this walk (TickSidekick)
            skWalkAt = clock; skWalkFor = 999f; skWalkDir = Vector3.forward; skWalkHasTarget = false; skWalkRate = walk.skRate; skYaw = skYawGoal = 0f;
            uint mask = LightMask;
            ezLava = AddLight("EzLava", LightType.Point, Vector3.zero, Vector3.zero, new Color(1f, 0.40f, 0.12f), walk.ezLight, 4.5f);
            skLava = AddLight("SkLava", LightType.Point, Vector3.zero, Vector3.zero, new Color(1f, 0.40f, 0.12f), walk.skLightA, 3.6f);
            ezGlow = OzL07CutFx.Flat(transform, "EzGlow", Layer, 6f, OzL07CutFx.LavaGlow, mask); skGlow = OzL07CutFx.Flat(transform, "SkGlow", Layer, 6f, OzL07CutFx.LavaGlow, mask);
            skRing = OzL07CutFx.Flat(transform, "SkRing", Layer, 5f, OzL07CutFx.LavaRing, mask);
            ezWade = new OzL07CutFx.Wade(transform, "EzWade", Layer, mask, walk.ezLegSize); skWade = new OzL07CutFx.Wade(transform, "SkWade", Layer, mask, walk.skLegSize);
            if (ez != null && ez.Ok) ezWade.Bind(ez.Rig.FootL, ez.Rig.FootR);
            if (sk != null && sk.Ok) skWade.Bind(sk.Rig.FootL, sk.Rig.FootR);
            ezHot = HotLegs(ez != null && ez.Ok ? ez.Root : null, lavaY); skHot = HotLegs(sk != null && sk.Ok ? sk.Root : null, lavaY);
            PlaceLavaRobots(0f);
            LavaBedOn();
            Debug.Log($"[EvilZod] lava walk: stage on the lava at ({walk.placeX:F1}, {lavaY:F2}, {walk.placeZ:F1}) heading {walk.yaw:F0}, room hidden");
        }

        /// <summary>These two are not TeamLit: their legs read hot through a second skinned draw of the same mesh, from the lava line up (OpenZ/L07LegHot, the body's own picture keeps the plates). Gone with the stage.</summary>
        Material HotLegs(Transform root, float lavaY)
        {
            var src = Resources.Load<Material>("OpenZ/L07Cut/M_L07_LegHot"); if (!root || !src) { if (root) Debug.LogWarning("[EvilZod] lava walk: M_L07_LegHot missing: the legs are not hot"); return null; }
            var m = new Material(src) { hideFlags = HideFlags.DontSave }; m.SetFloat("_Line", lavaY); madeMats.Add(m);
            foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!s.sharedMesh || !s.name.EndsWith("_Body")) continue;
                var body = s.sharedMaterial; if (body && body.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", body.GetTexture("_BaseMap"));
                var go = new GameObject(s.name + "_Hot") { layer = Layer }; go.transform.SetParent(s.transform.parent, false);
                go.transform.localPosition = s.transform.localPosition; go.transform.localRotation = s.transform.localRotation; go.transform.localScale = s.transform.localScale;
                var c = go.AddComponent<SkinnedMeshRenderer>(); c.sharedMesh = s.sharedMesh; c.bones = s.bones; c.rootBone = s.rootBone; c.updateWhenOffscreen = true;
                c.shadowCastingMode = ShadowCastingMode.Off; c.receiveShadows = false; c.lightProbeUsage = LightProbeUsage.Off; c.renderingLayerMask = s.renderingLayerMask;
                var mats = new Material[s.sharedMesh.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = m; c.sharedMaterials = mats;
            }
            return m;
        }

        /// <summary>The framing "two" on the lava. In the HQ they stand side by side; here they walk one behind the other down the lens's line, so Pair's midpoint would leave EvilZod's head out of the top of the picture
        /// and its span would run down to feet that are under the lava. This one fits the picture at EvilZod's own distance, from the lava line to over his hat; BS-01 behind him is smaller and stays inside. Proof: tools/13_previews_blue.py, art/two_shot.json.</summary>
        Framing LavaTwo(Vector3 near, Vector3 far, float top)
        {
            const float fov = 32f, yaw = -6f; float elev = walk.twoTilt;
            var f = Pair(near, far, -0.06f, top + 0.18f, 1.25f, fov, elev, yaw);
            float ahead = Mathf.Max(0f, Vector3.Dot(near - f.look, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward));
            f.dist += ahead; f.look.y += ahead * Mathf.Tan(-elev * Mathf.Deg2Rad);                         // the middle of the picture at his depth, not at the aim point's: the lens stands low and looks up
            return f;
        }

        /// <summary>Resolve's framing on the lava walk. In the HQ the lens stands low and looks up at him; out here that is a picture of sky: the true horizon sat at row 274 of 294 in ez_med. A small tilt down puts it about a third
        /// from the top: the frame is his body, the lava and his hot legs, with little sky. The fit to his bones stays (OzEvilStage.Camera.Single). Numbers: l07_walk_scene.json. Proof: tools/17_rock_gap_and_tilt.py.</summary>
        Framing LavaFrame(string n, Framing f)
        {
            if (walk == null) return f;
            if (n != "ez_med" && n != "ez_close") return f;
            EzPoints(out var top, out var face, out var chest, out var hips, out _);
            return n == "ez_med" ? Single(chest, hips.y - walk.ezMedBelow, top.y + walk.ezMedOver, 1.45f, 30f, walk.ezMedTilt, 16f, 1)
                                 : Single(face, chest.y - 0.12f, top.y + walk.ezCloseOver, 0.95f, 28f, walk.ezCloseTilt, 22f, 1);
        }

        /// <summary>The crash zoom's aim on EvilZod here: all the way to the middle of his head (hat top to chin), so HeadMargin stands above the hat as well as under the chin. The HQ's aim stops 85 % of the way to the eyes: under this tilted lens that left the hat about 24 px from the top at the spring's overshoot.</summary>
        Vector3 LavaCrashAim(Vector3 from, float zoomK)
        {
            EzHead(out var top, out var chin);
            return Vector3.Lerp(from, (top + chin) * 0.5f, Mathf.Clamp01(zoomK * 1.6f));
        }

        /// <summary>
        /// His landed crash zoom on the ez_med shot (3.69 and 13.34 s) tilts down walk.ezCrashTilt deg about the lens. The lens stays where it is,
        /// the zoom keeps its size and timing; the tilt rides the zoom's share as LavaCrashAim. Measured on the 2 Nov record: the hat's top 29-33 % from the
        /// window's top edge, with 2.5 deg about 14-17 %, where his ez_close crash (18.04 s) already lands (14-18 %); that one and every other shot keep
        /// their lens. Proof: open_fixes_2nov/l07_blue_brief.</summary>
        Vector3 LavaCrashTilt(Vector3 look, Vector3 pos, float zoomK)
        {
            if (walk == null || walk.ezCrashTilt == 0f || camName != "ez_med") return look;
            Vector3 d = look - pos, side = Vector3.Cross(transform.up, d);
            if (side.sqrMagnitude < 1e-8f) return look;
            return pos + Quaternion.AngleAxis(walk.ezCrashTilt * Mathf.Clamp01(zoomK * 1.6f), side.normalized) * d;
        }

        /// <summary>OzBlueCameo.LiveMouth: the dB that take this second of the voice back to the approved take (0 off the walk).</summary>
        public static float LavaMouthDb(float voiceSeconds)
        {
            var w = MouthWalk; if (w == null || w.mouthT == null || w.mouthDb == null) return 0f;
            float db = 0f; for (int i = 0; i < w.mouthT.Length && i < w.mouthDb.Length; i++) if (w.mouthT[i] <= voiceSeconds) db = w.mouthDb[i];
            return db;
        }
        static OzWalkFile MouthWalk;

        /// <summary>Where EvilZod looks on the walk. The real frames showed his head starting 60 to 70 deg to his left on every cut to him: on the beat's frame CamPos is still the last shot's lens, and that lens stands beside him
        /// (it films BS-01 behind him). So here the look comes from the framing itself, never from the camera's last place; while the lens is on BS-01 he looks where he walks; and the turn from the chest bends softly into headTurnMax, so it eases and never hits a stop.</summary>
        Vector3 LavaLookPoint(OzPoseRig rig)
        {
            Vector3 hp = rig.Head.position, fwd = rig.Fwd, to = fwd;
            if (!camName.StartsWith("sk_"))
            {
                var f = Resolve(camName); float el = f.elev * Mathf.Deg2Rad, yw = f.yaw * Mathf.Deg2Rad;
                to = transform.TransformPoint(f.look + new Vector3(Mathf.Sin(yw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yw) * Mathf.Cos(el)) * f.dist) - hp;
            }
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up); float most = Mathf.Max(5f, walk.headTurnMax);
            float a = flat.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(fwd, flat.normalized, Vector3.up) : 0f, soft = most * (float)System.Math.Tanh(a / most);
            return hp + Quaternion.AngleAxis(soft, Vector3.up) * fwd * Mathf.Max(1.5f, flat.magnitude) + Vector3.up * to.y;
        }

        /// <summary>The walk's sound: the bubbling bed starts with the walk and ends with the stage. Every moment goes into the sound log.</summary>
        void LavaBedOn()
        {
            walkMelts = walkPops = 0; walkHeadMost = 0f; walkPopAt = walk.popEveryA; walkDuckAt = float.NegativeInfinity;
            walkMelt = NewSource(40); walkPop = NewSource(41); walkCrash = NewSource(32); walkCrashSeen = -1f;
            var c = string.IsNullOrEmpty(walk.bedClip) ? null : ClipOf(walk.bedClip);
            if (!c) { OzSoundLog.Add("EvilZod", "walk_lava_bubbling", walk.bedClip, "missing clip"); return; }
            walkBed = NewSource(44); walkBed.loop = true; walkBed.clip = c; walkBed.pitch = walk.bedPitch; walkBed.volume = BedVolume(0f); walkBed.time = UnityEngine.Random.Range(0f, c.length * 0.8f); walkBed.Play();
            OzSoundLog.Add("EvilZod", "walk_lava_bubbling", c.name, "loop on", walkBed.volume);
        }
        const string CrashWhoosh = "OpenZ/Audio/Sfx/v3-ramp-whoosh";
        float BedVolume(float k) => Mathf.Clamp01(VoiceGain * Mathf.Pow(10f, Mathf.Lerp(walk.bedDbA, walk.bedDbB, k) / 20f));
        /// <summary>OnDestroy: the stage goes when the window has folded (after SPACE as well).</summary>
        void LavaBedOff() { MouthWalk = null; if (walkBed && walkBed.isPlaying) { walkBed.Stop(); OzSoundLog.Add("EvilZod", "walk_lava_bubbling", walkBed.clip ? walkBed.clip.name : "", "loop off"); } }

        /// <summary>A gloop or a pop on its own source: a source's pitch would bend the sound still playing on it.</summary>
        void WalkOneShot(AudioSource src, string[] res, float db, float pitchA, float pitchB, string key)
        {
            if (res == null || res.Length == 0 || !src) return;
            Sfx(res[UnityEngine.Random.Range(0, res.Length)], db, UnityEngine.Random.Range(pitchA, pitchB), key, src);
        }

        /// <summary>Every renderer of the room off, its own lights off; the robots and the walk's own pieces stay.</summary>
        void HideRoom()
        {
            var keep = new HashSet<Transform>();
            if (ez != null && ez.Ok) foreach (var t in ez.Root.GetComponentsInChildren<Transform>(true)) keep.Add(t);   // HideRoom runs before the walk's own pieces exist
            if (sk != null && sk.Ok) foreach (var t in sk.Root.GetComponentsInChildren<Transform>(true)) keep.Add(t);
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (!keep.Contains(r.transform)) r.enabled = false;
            if (consoleGlow) consoleGlow.enabled = false;
            foreach (var l in banners) if (l) l.enabled = false;
            foreach (var l in alcove) if (l) l.enabled = false;
            if (beacon) beacon.enabled = false;
        }

        /// <summary>TickEvilZod, in place of the rest pose: his walk take this frame (false = not the lava walk).</summary>
        bool TickLavaWalkTake(OzPoseRig rig, float dt)
        {
            if (!LavaWalkOn) return false;
            bool fromTake = rig.TickWalk(dt);
            if (fromTake && dt > 0f)
            {
                ez.Root.localPosition += Vector3.forward * (rig.WalkSpeed * rig.WalkRate * dt);
                int parity = rig.WalkPhase < 0.5f ? 0 : 1;
                if (parity != ezStepParity) { ezStepParity = parity; SfxRandom(walk.stepClips, -11f, 0.72f, 0.82f, "ez_step_lava", StepSource); }
            }
            return fromTake;
        }

        /// <summary>LateUpdate after the robots: both at their depth, the lava's light and glow at their legs; BS-01 goes lower on every footfall.</summary>
        void TickLavaWalk(float dt)
        {
            if (!LavaWalkOn) return;
            if (sk != null && sk.Ok && skWalkAt >= 0f && sk.Rig.Walking)
            {
                int parity = sk.Rig.WalkPhase < 0.5f ? 0 : 1;
                if (parity != skStepSeen)
                {
                    if (skStepSeen >= 0)
                    {
                        skSteps++; skSinkGoal = Mathf.Min(walk.skSinkMost, skSinkGoal + walk.skSinkStep);
                        if (skSteps % Mathf.Max(1, walk.meltEvery) == 1) { WalkOneShot(walkMelt, walk.meltClips, walk.meltDb, 0.85f, 1.05f, "walk_lava_melt"); walkMelts++; }   // his boot pulled out of the melt: the gloops run about 3 s, this many steps apart they stand one by one
                    }
                    skStepSeen = parity;
                }
            }
            if (dt > 0f) skSink += (skSinkGoal - skSink) * (1f - Mathf.Exp(-dt / 0.14f));
            PlaceLavaRobots(dt);
            if (walkBed) walkBed.volume = BedVolume(Mathf.Clamp01((skSink - walk.skSinkFrom) / Mathf.Max(0.05f, walk.skSinkMost - walk.skSinkFrom)));
            if (clock >= walkDuckAt && AudioDirector.I) { walkDuckAt = clock + 0.3f; AudioDirector.I.AddDuck(walk.musicDuckDb, 0f, 0f, 0f, 0.55f, 0.2f, 1f); if (walk.worldDuckDb < 0f && CombatAudio.I) CombatAudio.I.DuckFor(0.55f, 0f, walk.worldDuckDb); }   // the lower duck wins: under this walk the lava is the bed, not the music; row 61: the level's world sounds under it too
            if (dt > 0f && clock >= walkPopAt) { walkPopAt = clock + UnityEngine.Random.Range(walk.popEveryA, walk.popEveryB); WalkOneShot(walkPop, walk.popClips, walk.popDb, 0.8f, 1.15f, "walk_lava_pop"); walkPops++; }
            if (camPunchUntil > clock && camPunchUntil != walkCrashSeen) { walkCrashSeen = camPunchUntil; Sfx(CrashWhoosh, -9f, 1.55f, "walk_crash_zoom", walkCrash); }   // the crash zoom's own sound, the red briefing's whoosh
            if (ez != null && ez.Ok && ez.Rig.HeadFront && ez.Rig.Head)
            {   // Play check: the head's real turn from the chest this frame
                Vector3 hf = Vector3.ProjectOnPlane(ez.Rig.HeadFront.position - ez.Rig.Head.position, Vector3.up);
                if (hf.sqrMagnitude > 1e-6f) walkHeadMost = Mathf.Max(walkHeadMost, Mathf.Abs(Vector3.SignedAngle(ez.Rig.Fwd, hf, Vector3.up)));
            }
        }

        void PlaceLavaRobots(float dt)
        {
            float flick = 0.9f + 0.1f * Mathf.PerlinNoise(clock * 5f, 0.3f);
            if (ez != null && ez.Ok)
            {
                var p = ez.Root.localPosition; p.y = -walk.ezSink; if (dt <= 0f && clock < 0.01f) { p.x = walk.ezX; p.z = walk.ezZ; }
                ez.Root.localPosition = p;
                Vector3 foot = new Vector3(p.x, 0.03f, p.z);
                float hk = Mathf.Clamp01(clock / Mathf.Max(0.1f, walk.ezHeatSeconds)); hk = hk * hk * (3f - 2f * hk);
                if (ezLava) { ezLava.transform.localPosition = foot + Vector3.up * walk.lightUp; ezLava.intensity = walk.ezLight * flick; }
                if (ezHot) { ezHot.SetFloat("_Top", Mathf.Lerp(walk.ezTopA, walk.ezTopB, hk)); ezHot.SetFloat("_Heat", Mathf.Lerp(walk.ezHeatA, walk.ezHeatB, hk)); }
                if (ezGlow) { ezGlow.transform.localPosition = foot; ezGlow.transform.localScale = Vector3.one * 3.2f; OzL07CutFx.Tint(ezGlow, OzL07CutFx.LavaGlow, walk.ezGlow); }
                ezWade?.Tick(dt, lavaWorldY, ez.Root.position.y, Mathf.Lerp(walk.ezHeatA, walk.ezHeatB, hk));
            }
            if (sk != null && sk.Ok)
            {
                var p = sk.Root.localPosition; if (dt <= 0f && clock < 0.01f) { p.x = walk.skX; p.z = walk.skZ; }
                p.y = -skSink + skDrawOff.y; sk.Root.localPosition = p;                                    // the hop's drawn offset stays on top (TickSidekick takes it off next frame)
                Vector3 foot = new Vector3(p.x, 0.03f, p.z); float k = Mathf.Clamp01((skSink - walk.skSinkFrom) / Mathf.Max(0.05f, walk.skSinkMost - walk.skSinkFrom));
                if (skLava) { skLava.transform.localPosition = foot + Vector3.up * walk.lightUp; skLava.intensity = Mathf.Lerp(walk.skLightA, walk.skLightB, k) * flick; }
                if (skHot) { skHot.SetFloat("_Top", Mathf.Lerp(walk.skTopA, walk.skTopB, k)); skHot.SetFloat("_Heat", Mathf.Lerp(walk.skHeatA, walk.skHeatB, k)); }
                if (skGlow) { skGlow.transform.localPosition = foot; skGlow.transform.localScale = Vector3.one * 2.6f; OzL07CutFx.Tint(skGlow, OzL07CutFx.LavaGlow, Mathf.Lerp(walk.skGlowA, walk.skGlowB, k)); }
                if (skRing) { skRing.transform.localPosition = foot; skRing.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.25f, k); OzL07CutFx.Tint(skRing, OzL07CutFx.LavaRing, 0.08f + 0.08f * k); }
                skWade?.Tick(dt, lavaWorldY, sk.Root.position.y, Mathf.Lerp(walk.skHeatA, walk.skHeatB, k));
            }
        }
    }
}
