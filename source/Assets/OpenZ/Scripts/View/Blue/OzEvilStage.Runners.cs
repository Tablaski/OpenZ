// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the LOSE scene's fleeing army (OzEvilStage).
// Beat "clones_run" (set): three BS-01 lookalikes (the same model, a touch of size spread) run across the
// back of the HQ toward the left wing (+X, screen left) in the scene's first seconds: the walk take at a scurry, the body leaning
// forward, both mittens up by the head flailing, the head turned back over the shoulder, the eyes wide with small pupils (scared), quick
// light steps. They pass behind EvilZod and BS-01 (z -1.7 .. -2.05: clear of both and of the console) and are gone off the left wing
// before the cut to EvilZod's "SEND IN EVERYONE!" (2.67 s): then only the two of them are left. Built when the LOSE scene begins (the
// owner's weight rule: load where shown) and destroyed when they have run off.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        sealed class Runner { public Robot R; public float startAt, x, z, speed, phase, size, squeakAt; public bool on, done, squeaked; public int parity = -1; public AudioSource src; public Light lamp; }
        /// <summary>The Play checks / the DOF: a clone is running now.</summary>
        public bool RunnersRunning { get { foreach (var u in runners) if (u.on && !u.done) return true; return false; } }
        readonly List<Runner> runners = new List<Runner>();
        /// <summary>The plan, per runner: start (s after the beat), the start x, z (stage frame), speed (m/s), size (x the sidekick's).</summary>
        /// than a body's width), the same speed (nobody passes anybody) and starts 0.35 s apart from the same x (0.84 m apart along the run), so
        /// their silhouettes never merge from any of the scene's cameras.
        static readonly float[][] RunPlan =
        {
            new[] { 0.00f, -2.2f, -1.15f, 3.0f, 1.05f },
            new[] { 0.32f, -2.2f, -1.65f, 3.0f, 1.00f },
            new[] { 0.64f, -2.2f, -2.15f, 3.0f, 1.10f },
        };
        /// <summary>Where a runner is gone (stage x: past the left wing, out of every framing of the scene).</summary>
        public static float RunEndX = 5.2f;
        /// <summary>The Play checks: how many lookalikes ran in this stage's life.</summary>
        public int RunnersRan { get; private set; }

        /// <summary>Beat "clones_run": the lookalikes are built and start their run (each on its own delay).</summary>
        void StartRunners()
        {
            DisposeRunners();
            var pupilMat = Resources.Load<Material>(Folder + "M_Sidekick_Pupil");
            for (int i = 0; i < RunPlan.Length; i++)
            {
                var plan = RunPlan[i];
                var r = LoadRobot("Sidekick", "Sidekick_Body", "M_Sidekick_Body", SkScale * plan[4]);
                if (!r.Ok) { if (r.Go) Destroy(r.Go); continue; }
                r.Go.name = "SidekickRunner" + i;
                r.Rig.LoadWalk(Folder, "Sidekick");
                foreach (var rd in r.Go.GetComponentsInChildren<Renderer>(true))
                {   // the eyes as BS-01's (the same glow material instance: it glows with his)
                    if (rd.name.EndsWith("_Glow") && skEyeMat) rd.sharedMaterial = skEyeMat;
                    else if (rd.name.EndsWith("_Pupil") && pupilMat) rd.sharedMaterial = pupilMat;
                    else if (rd.name.Contains("_Lid") && r.BodyMat) rd.sharedMaterial = r.BodyMat;
                    if (rd.name.StartsWith("Eye_")) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach (var en in new[] { "Eye_L", "Eye_R" })
                {   // scared: the eye discs wide open, the pupils small (the Scared preset of BS-01's own eye rig)
                    var e = r.Rig.Find(en); if (e) e.localScale *= 1.2f;
                    var pu = r.Rig.Find(en + "_Pupil"); if (pu) pu.localScale *= 0.55f;
                }
                r.SetActive(false);
                var src = r.Go.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0f; src.priority = 60; src.dopplerLevel = 0f; src.bypassReverbZones = true;
                // its own light (review: the clones read dark navy and unlit): a blue-white key in front of it that runs with it
                var lgo = new GameObject("RunnerKey") { layer = Layer }; lgo.transform.SetParent(r.Root, false); lgo.transform.localPosition = Vector3.zero;
                var lamp = AddLight("RunnerKey", LightType.Point, Vector3.zero, Vector3.zero, new Color(0.75f, 0.85f, 1f), 3.2f, 1.8f, 50f, false, lgo.transform);
                lamp.transform.position = r.Root.position + Vector3.up * 0.9f * SkScale + transform.forward * 0.7f;
                runners.Add(new Runner { R = r, startAt = clock + plan[0], x = plan[1], z = plan[2], speed = plan[3], size = plan[4], phase = i * 1.7f, src = src, lamp = lamp, squeakAt = 0.35f + 0.3f * i });
            }
            Debug.Log($"[EvilZod] LOSE: {runners.Count} BS-01 lookalikes run off at the back (the army flees)");
        }

        void TickRunners(float dt)
        {
            if (runners.Count == 0) return;
            bool any = false;
            foreach (var u in runners)
            {
                if (u.done) continue;
                any = true;
                var rig = u.R.Rig;
                if (!u.on)
                {
                    if (clock < u.startAt) continue;
                    u.on = true; RunnersRan++;
                    u.R.Root.localPosition = new Vector3(u.x, 0f, u.z); ApplyYaw(u.R, 90f);                 // facing +X: toward the left wing
                    u.R.SetActive(Live);
                    rig.WalkRate = Mathf.Clamp(u.speed / Mathf.Max(0.2f, rig.WalkSpeed), 1f, 6f);                // the legs cycle with the speed (no sliding feet)
                    rig.StartWalkCut();                                                                      // in step at once (no blend from the T-pose)
                    RunnerSfx(u, SkServos[UnityEngine.Random.Range(0, SkServos.Length)], -13f, UnityEngine.Random.Range(1.55f, 1.8f), "runner_panic_squeak");
                }
                if (!u.R.Go.activeSelf) continue;                                                            // the stage is dark (paused window)
                if (dt > 0f)
                {
                    var p = u.R.Root.localPosition; p.x += u.speed * dt; u.R.Root.localPosition = p;
                    if (p.x > RunEndX) { u.done = true; rig.StopWalk(0.05f); u.R.SetActive(false); continue; }
                }
                ApplyYaw(u.R, 90f);
                bool fromTake = rig.TickWalk(dt);
                if (!fromTake) rig.ResetToRest();
                rig.Lean(14f, 0f, 0f);
                // both mittens up by the head, flailing; the head turned back over the shoulder at EvilZod
                float k = rig.Scale, t = clock - u.startAt;
                float wa = Mathf.Sin(t * 17f + u.phase) * 0.06f, wb = Mathf.Sin(t * 17f + u.phase + 2.1f) * 0.06f;
                rig.IkArm(true, rig.Local(0.32f * k, (1.04f + wa) * k, 0.08f * k), rig.Right + Vector3.down * 0.3f, 1f);
                rig.IkArm(false, rig.Local(-0.32f * k, (1.04f + wb) * k, 0.08f * k), -rig.Right + Vector3.down * 0.3f, 1f);
                rig.LookAt(rig.Local(-0.9f, 0.9f * k, -1.4f), 1f, 70f, 30f, 0.4f);
                rig.FinishWalkBlend();
                int parity = rig.WalkPhase < 0.5f ? 0 : 1;
                if (parity != u.parity) { u.parity = parity; RunnerSfx(u, LightSteps[UnityEngine.Random.Range(0, LightSteps.Length)], -13f, UnityEngine.Random.Range(1.5f, 1.7f), "runner_step"); }
                if (!u.squeaked && clock - u.startAt > u.squeakAt) { u.squeaked = true; RunnerSfx(u, SkServos[UnityEngine.Random.Range(0, SkServos.Length)], -15f, UnityEngine.Random.Range(1.7f, 1.95f), "runner_panic_squeak"); }
                if (u.lamp) u.lamp.transform.position = u.R.Root.position + Vector3.up * 0.9f * SkScale + transform.forward * 0.7f;
            }
            if (!any) DisposeRunners();
        }

        /// <summary>A clone's sound on its own source, panned to where it is in the picture (every visible action has its sound).</summary>
        void RunnerSfx(Runner u, string res, float db, float pitch, string key)
        {
            var c = ClipOf(res); if (!c || !u.src) { OzSoundLog.Add("EvilZod", key, res, "missing clip"); return; }
            float pan = 0f;
            if (cam) { var vp = cam.WorldToViewportPoint(u.R.Root.position); pan = vp.z > 0f ? Mathf.Clamp((vp.x - 0.5f) * 1.6f, -0.9f, 0.9f) : 0f; }
            u.src.panStereo = pan; u.src.pitch = pitch;
            float g = Mathf.Clamp01(VoiceGain * Mathf.Pow(10f, db / 20f)); u.src.PlayOneShot(c, g);
            OzSoundLog.Add("EvilZod", key, c.name, "played", g);
        }

        void SetRunnersActive(bool on) { foreach (var u in runners) u.R.SetActive(on && u.on && !u.done); }

        void DisposeRunners()
        {
            foreach (var u in runners) { u.R.Dispose(); if (u.R.Go) Destroy(u.R.Go); }
            runners.Clear();
        }
    }
}
