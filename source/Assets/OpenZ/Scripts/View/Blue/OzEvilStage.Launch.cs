// OpenZ (GPL-3.0) - version 1.2, the blue WIN NUKE scene: the World-Domination-inator LAUNCHES (OzEvilStage).
// On the frame EvilZod's fist hits the blue button (ButtonPress, the same frame OpenZNukeStrike.LaunchNow starts the strike
// that brings the bomb onto the red fort) the bomb in the alcove ignites: a short rumble on its cradle with the flame lighting up, then it
// lifts off, its nose swinging up, and climbs out of the alcove over the doors' lintel on a roaring flame and a trail of smoke; an orange
// glow on the HQ walls, the camera shakes. Sounds: the ignition crack and the rocket roar (the game's own clips). Everything on the
// stage's layer and light layer (the game's own FX never draw on this stage). Reset by BeginScene (the bomb back on its cradle).
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>Play checks: the HQ bomb launched (one per press).</summary>
        public static int BombLaunches { get; private set; }
        /// <summary>The launch: the rumble before the lift (s), the lift's acceleration (m/s^2), the nose's swing up (deg) and its time (s).</summary>
        public static float LaunchRumble = 0.3f, LaunchAccel = 9f, LaunchPitch = 70f, LaunchPitchSeconds = 0.7f;

        Vector3 bombHomePos, bombCenterLocal; Quaternion bombHomeRot; float bombHalfLen = 1.5f, launchAt = -10f, nextLaunchKick; bool bombHomeSaved;
        Transform flameOuter, flameInner; Light flameLight; ParticleSystem launchSmoke;

        /// <summary>BuildProps, after the bomb is placed: its home pose and its centre / half length (the tail is where the flame goes).</summary>
        void SaveBombHome()
        {
            if (!bomb) return;
            bombHomePos = bomb.localPosition; bombHomeRot = bomb.localRotation; bombHomeSaved = true;
            var rs = bomb.GetComponentsInChildren<Renderer>(true); var b = new Bounds(); bool any = false;
            foreach (var r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (any) { bombCenterLocal = bomb.InverseTransformPoint(b.center); bombHalfLen = Mathf.Max(0.5f, Vector3.Dot(b.extents, new Vector3(Mathf.Abs(bomb.forward.x), Mathf.Abs(bomb.forward.y), Mathf.Abs(bomb.forward.z)))); }
            var flameM = Glow(new Color32(255, 120, 30, 255), new Color(1f, 0.45f, 0.08f), 6f);
            var coreM = Glow(new Color32(255, 230, 170, 255), new Color(1f, 0.9f, 0.6f), 9f);
            flameOuter = MakeFlame("LaunchFlame", flameM); flameInner = MakeFlame("LaunchFlameCore", coreM);
            flameLight = AddLight("LaunchFlameLight", LightType.Point, Vector3.zero, Vector3.zero, new Color(1f, 0.55f, 0.2f), 0f, 8f);
            launchSmoke = MakeLaunchSmoke();
            ResetBombLaunch();
        }

        Transform MakeFlame(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            Dress(go, m, false);
            go.SetActive(false);
            return go.transform;
        }

        ParticleSystem MakeLaunchSmoke()
        {
            var go = new GameObject("LaunchSmoke") { layer = Layer }; go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 3f; main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.78f, 0.74f, 0.70f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 300; main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 70f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.12f;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3.2f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.75f, 0.45f), 0f), new GradientColorKey(new Color(0.7f, 0.7f, 0.72f), 0.25f), new GradientColorKey(new Color(0.55f, 0.55f, 0.58f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.08f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard; pr.sortMode = ParticleSystemSortMode.Distance;
            pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.renderingLayerMask = LightMask;
            pr.sharedMaterial = LaunchSmokeMaterial();
            return ps;
        }

        static Material launchSmokeMat;
        static Material LaunchSmokeMaterial()
        {
            if (launchSmokeMat) return launchSmokeMat;
            var t = Resources.Load<Material>(ZodStage.MatPath + "ZodStage_Smoke");
            if (t) launchSmokeMat = new Material(t) { hideFlags = HideFlags.DontSave };
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (!sh) return null;
                launchSmokeMat = new Material(sh) { hideFlags = HideFlags.DontSave, name = "EvilStage_LaunchSmoke" };
                launchSmokeMat.SetFloat("_Surface", 1); launchSmokeMat.SetFloat("_Blend", 0);
                launchSmokeMat.SetOverrideTag("RenderType", "Transparent"); launchSmokeMat.renderQueue = 3000;
                launchSmokeMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); launchSmokeMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                launchSmokeMat.SetFloat("_ZWrite", 0); launchSmokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            var tex = Resources.Load<Texture2D>(ZodStage.SmokeTexPath);
            if (!tex)
            {   // a soft round puff
                tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float dx = (x + 0.5f) / 32f - 1, dy = (y + 0.5f) / 32f - 1, a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * 64 + x] = new Color32(255, 255, 255, (byte)(255 * a * a * (3 - 2 * a)));
                    }
                tex.SetPixels32(px); tex.Apply();
            }
            launchSmokeMat.SetTexture("_BaseMap", tex);
            madeMats.Add(launchSmokeMat);
            return launchSmokeMat;
        }

        /// <summary>BeginScene: the bomb back on its cradle, no flame.</summary>
        void ResetBombLaunch()
        {
            launchAt = -10f;
            if (bomb && bombHomeSaved) { bomb.localPosition = bombHomePos; bomb.localRotation = bombHomeRot; bomb.gameObject.SetActive(true); }
            if (flameOuter) flameOuter.gameObject.SetActive(false); if (flameInner) flameInner.gameObject.SetActive(false);
            if (flameLight) flameLight.intensity = 0f;
            if (launchSmoke) launchSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>ButtonPress: the ignition, on the same frame the strike launches.</summary>
        void BombLaunch()
        {
            if (!bomb || !bombHomeSaved || launchAt >= 0f) return;
            launchAt = clock; nextLaunchKick = clock + 0.25f; BombLaunches++;
            Sfx("OpenZ/Audio/Sfx/mlrs-launch-v1-20260909", -4f, 0.8f, "bomb_ignition", sfxB);
            Sfx("OpenZ/Audio/Sfx/rocket-blast-sep11", -2f, 0.75f, "bomb_launch_roar");
            if (launchSmoke) launchSmoke.Play();
            Kick(0.5f);
            Debug.Log($"[EvilZod] the World-Domination-inator launches (clock {clock:F2}, frame {Time.frameCount})");
        }

        void TickBombLaunch(float dt)
        {
            if (launchAt < 0f || !bomb) return;
            float u = clock - launchAt;
            Vector3 up = transform.up;
            Transform cr = bomb.parent ? bomb.parent : transform;                                         // the cradle: the bomb's home is in its frame
            Vector3 nose0 = (cr.rotation * bombHomeRot) * Vector3.forward;
            float pitchK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - LaunchRumble) / LaunchPitchSeconds));
            Vector3 nose = Vector3.Slerp(nose0, up, pitchK * LaunchPitch / 90f).normalized;
            Quaternion turn = Quaternion.FromToRotation(nose0, nose);
            float lift = u > LaunchRumble ? 0.5f * LaunchAccel * (u - LaunchRumble) * (u - LaunchRumble) : 0f;
            Vector3 shake = u < LaunchRumble + 0.4f ? new Vector3(Mathf.Sin(u * 71f), Mathf.Sin(u * 89f + 1f), Mathf.Sin(u * 63f + 2f)) * 0.012f : Vector3.zero;
            // the bomb turns about its centre and rises
            Quaternion homeW = cr.rotation * bombHomeRot;
            Vector3 homePosW = cr.TransformPoint(bombHomePos);
            Vector3 centreHomeW = homePosW + homeW * Vector3.Scale(bombCenterLocal, bomb.localScale);
            Vector3 centreNow = centreHomeW + up * lift + nose * lift * 0.15f + shake;
            bomb.rotation = turn * homeW;
            bomb.position = centreNow - bomb.rotation * Vector3.Scale(bombCenterLocal, bomb.localScale);
            bool gone = lift > 9f;
            if (gone && bomb.gameObject.activeSelf) bomb.gameObject.SetActive(false);
            // the flame and the smoke at the tail
            Vector3 tail = centreNow - nose * bombHalfLen;
            float grow = Mathf.Clamp01(u / 0.25f), flick = 0.85f + 0.15f * Mathf.PerlinNoise(clock * 23f, 0.4f);
            float len = (0.8f + 1.6f * Mathf.Clamp01((u - LaunchRumble) / 0.5f)) * grow * flick;
            if (flameOuter)
            {
                flameOuter.gameObject.SetActive(!gone);
                flameOuter.rotation = Quaternion.LookRotation(-nose); flameOuter.localScale = new Vector3(0.55f, 0.55f, len) * grow;
                flameOuter.position = tail - nose * len * 0.45f;
            }
            if (flameInner)
            {
                flameInner.gameObject.SetActive(!gone);
                flameInner.rotation = Quaternion.LookRotation(-nose); flameInner.localScale = new Vector3(0.3f, 0.3f, len * 0.6f) * grow;
                flameInner.position = tail - nose * len * 0.25f;
            }
            if (flameLight)
            {
                flameLight.transform.position = tail - nose * 0.6f;
                flameLight.intensity = gone ? Mathf.MoveTowards(flameLight.intensity, 0f, dt * 60f) : 45f * grow * flick;
            }
            if (launchSmoke)
            {
                launchSmoke.transform.position = tail; launchSmoke.transform.rotation = Quaternion.LookRotation(-nose);
                if (gone && launchSmoke.isEmitting) launchSmoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!gone && clock >= nextLaunchKick && dt > 0f) { nextLaunchKick = clock + 0.22f; Kick(Mathf.Lerp(0.45f, 0.2f, Mathf.Clamp01(u / 1.5f))); }
        }
    }
}
