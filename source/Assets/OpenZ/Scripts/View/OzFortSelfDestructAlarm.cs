using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// A red rotating ALARM BEACON on the hallway wall right
    /// above every fort's SELF DESTRUCT panel (View/OzFortSelfDestructPanels.cs, made about 1.8 x bigger by this kit) and a soft alarm hum heard
    /// only close by. Decoration only (no collider, no gameplay); view only (the fort's health is read, nothing is written).
    /// <list type="bullet">
    /// <item>The beacon: a gunmetal wall plate and a short arm out of the wall (<see cref="Box"/> lines below: the clearance proof reads them), the
    /// beacon foot on the arm, a red glass dome, inside it a turning reflector (the night lights' bulb glow) that flashes when it faces the eye,
    /// a small halo, and one small red spot light turning with it that sweeps the hallway walls (a real light only while the camera is within
    /// <see cref="LightNearM"/> m: from the RTS camera it is never drawn).</item>
    /// <item>Always on while the fort stands: one turn every <see cref="SpinCalm"/> s; below <see cref="UrgentShare"/> of the fort's health it spins
    /// faster (<see cref="SpinUrgent"/> s a turn) and the whole glow pulses (<see cref="PulseHz"/> a second), and the hum rises (pitch x
    /// <see cref="HumUrgentPitch"/>, louder).</item>
    /// <item>The hum: the sound event <see cref="HumEvent"/> (a soft electronic two-tone alarm hum, 3D, heard within 10 m: never at the RTS
    /// camera), one loop per fort, started while the camera is within <see cref="HumNearM"/> m and the beacon is shown, stopped otherwise.</item>
    /// <item>Gone with the fort: it hangs on the fort's FactoryView rig beside the panel (FactoryView.SetShown hides it with the rig), and it is shown
    /// only while the panel is (the panel follows the hall: the collapse, the game-over fall); hidden = dark, silent.</item>
    /// </list>
    /// Boots itself in every level scene (ZGame present). Switch: <see cref="Enabled"/>.
    /// </summary>
    [DefaultExecutionOrder(32110)]   // after OzFortSelfDestructPanels (32100) set its panels' shown state this frame
    public sealed class OzFortSelfDestructAlarm : MonoBehaviour
    {
        public static bool Enabled = true;
        public const string BeaconName = "OZ_SelfDestructBeacon", HumEvent = "amb_selfdestruct_alarm_hum";
        /// <summary>One turn of the reflector (s): calm, and below the urgent health share.</summary>
        public static float SpinCalm = 1.1f, SpinUrgent = 0.42f, UrgentShare = 0.25f, PulseHz = 2.6f;
        /// <summary>The real light only this near the camera (m); the hum loop only this near (its event is heard within 10 m).</summary>
        public static float LightNearM = 22f, HumNearM = 16f, HumUrgentPitch = 1.22f, HumUrgentVol = 1.45f;
        /// <summary>The dome's centre in panel space (x along the wall, y up from the panel's centre, -z out of the wall) and its radii.</summary>
        public const float DomeBaseY = 0.494f, DomeZ = -0.140f, DomeR = 0.052f, DomeH = 0.058f;
        /// <summary>Proof counters: beacons built, frames urgent, hum starts / stops, light frames.</summary>
        public static int Built, UrgentFrames, HumStarts, HumStops, LightFrames;
        public static OzFortSelfDestructAlarm I { get; private set; }

        sealed class Beacon
        {
            public FactoryView fv; public Transform root, spin, panel; public MeshRenderer panelR; public readonly List<Renderer> solid = new List<Renderer>();
            public Renderer bulb, halo; public Light light; public float angle, bulbShown = -1f, haloShown = -1f; public bool humOn, shown = true; public string key;
            public float Urgency;
        }
        readonly List<Beacon> beacons = new List<Beacon>();
        readonly HashSet<FactoryView> done = new HashSet<FactoryView>();
        MaterialPropertyBlock mpb; float nextScan;
        static Material redDome, plate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Built = UrgentFrames = HumStarts = HumStops = LightFrames = 0; I = null; }

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => TryAttach(); }
            TryAttach();
        }

        static void TryAttach()
        {
            if (!Enabled || !OzFortSelfDestructPanels.Enabled || !ZGame.I || FindFirstObjectByType<OzFortSelfDestructAlarm>()) return;
            new GameObject("OZ_FortWallAlarms").AddComponent<OzFortSelfDestructAlarm>();
        }

        void Awake() { I = this; mpb = new MaterialPropertyBlock(); }
        void OnDestroy() { if (I == this) I = null; foreach (var b in beacons) if (b.humOn && AudioDirector.I) AudioDirector.I.StopLoop(b.key, 0.2f); }

        /// <summary>The beacon of a fort rig (the check), or null.</summary>
        public static Transform BeaconOf(FactoryView fv) => fv ? fv.transform.Find(BeaconName) : null;
        /// <summary>The check: the beacon's state for a fort rig (its spin period now, urgent, the hum on, the light on, shown).</summary>
        public bool StateOf(FactoryView fv, out float period, out bool urgent, out bool hum, out bool lit, out bool shown)
        {
            period = 0f; urgent = hum = lit = shown = false;
            foreach (var b in beacons)
                if (b.fv == fv) { urgent = b.Urgency > 0.5f; period = urgent ? SpinUrgent : SpinCalm; hum = b.humOn; lit = b.light && b.light.enabled; shown = b.shown; return true; }
            return false;
        }

        static Material Lit(string name, Color c, float smooth, float metal, Color emission)
        {
            var sh = Shader.Find("OpenZ/TeamLit"); if (!sh) sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { name = name, hideFlags = HideFlags.DontSave, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_TeamMode")) m.SetFloat("_TeamMode", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            return m;
        }
        static Material RedDome => redDome ? redDome : redDome = Lit("OZ_SelfDestructBeacon_Dome", new Color(0.78f, 0.05f, 0.03f), 0.9f, 0f, new Color(0.25f, 0.01f, 0.005f));
        static Material Plate => plate ? plate : plate = NightMeshes.Housing();

        // ------------------------------------------------------------------ build (twice a second: the panels are made by OzFortSelfDestructPanels)
        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            var fvs = FactoryViews.I; var g = ZGame.I;
            if (!fvs || !g || g.World == null) return;
            for (int i = 0; i < g.World.Objects.Count; i++)
            {
                var o = g.World.Objects[i]; if (o == null || !o.IsFort) continue;
                var fv = fvs.For(o.Id); if (!fv || done.Contains(fv)) continue;
                var panel = OzFortSelfDestructPanels.PanelOf(fv); if (!panel) continue;
                done.Add(fv); beacons.Add(Build(fv, panel));
            }
            for (int i = beacons.Count - 1; i >= 0; i--)
                if (!beacons[i].root || !beacons[i].fv) { if (beacons[i].humOn && AudioDirector.I) AudioDirector.I.StopLoop(beacons[i].key, 0.2f); done.Remove(beacons[i].fv); beacons.RemoveAt(i); }
        }

        Beacon Build(FactoryView fv, Transform panel)
        {
            var b = new Beacon { fv = fv, panel = panel, panelR = panel.GetComponent<MeshRenderer>(), key = "sdalarm_" + (fv.B != null ? fv.B.Id : fv.GetInstanceID()) };
            var root = new GameObject(BeaconName).transform; root.SetParent(fv.transform, false); b.root = root;
            root.gameObject.layer = panel.gameObject.layer;
            // the wall plate, the arm, the beacon foot (panel space: x along the wall, y up, -z out of the wall; z = 0 is the wall)
            Box(b, -0.045f, 0.430f, -0.016f, 0.045f, 0.505f, -0.004f);       // the wall plate, 4 mm off the wall
            Box(b, -0.016f, 0.445f, -0.120f, 0.016f, 0.468f, -0.016f);       // the arm out of the wall
            Box(b, -0.045f, 0.468f, -0.185f, 0.045f, 0.484f, -0.095f);       // the beacon's foot plate on the arm
            Box(b, -0.032f, 0.484f, -0.172f, 0.032f, 0.494f, -0.108f);       // the collar under the dome
            var dome = NightMeshes.Part("Dome", root, NightMeshes.Sphere(), RedDome, false, new Vector3(0f, DomeBaseY, DomeZ), Quaternion.identity, new Vector3(DomeR, DomeH, DomeR));   // its lower half inside the collar: a dome DomeH high
            b.solid.Add(dome.GetComponent<Renderer>());
            b.spin = new GameObject("Spin").transform; b.spin.SetParent(root, false); b.spin.localPosition = new Vector3(0f, DomeBaseY + DomeH * 0.35f, DomeZ);
            b.bulb = NightMeshes.Part("Reflector", b.spin, NightMeshes.Quad(), NightMeshes.Bulb(), false, new Vector3(0f, 0f, 0.012f), Quaternion.identity, new Vector3(0.036f, 0.026f, 1f)).GetComponent<Renderer>();
            b.halo = NightMeshes.Part("Halo", b.spin, NightMeshes.Quad(), NightMeshes.Halo(), false, new Vector3(0f, 0f, 0.07f), Quaternion.identity, Vector3.one * 0.32f).GetComponent<Renderer>();
            b.bulb.enabled = b.halo.enabled = false;
            var lg = new GameObject("Light"); lg.transform.SetParent(b.spin, false); lg.transform.localPosition = new Vector3(0f, 0f, 0.03f); lg.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            b.light = lg.AddComponent<Light>();
            b.light.type = LightType.Spot; b.light.color = new Color(1f, 0.08f, 0.04f); b.light.range = 5f; b.light.spotAngle = 55f; b.light.innerSpotAngle = 20f;
            b.light.intensity = 0f; b.light.shadows = LightShadows.None; b.light.renderMode = LightRenderMode.ForcePixel; b.light.enabled = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = panel.gameObject.layer;
            Place(b);
            Built++;
#if !OZ_RELEASE
            Debug.Log($"[OpenZ] fort wall beacon: {(fv.B != null ? fv.B.Type + " #" + fv.B.Id : fv.name)} above the panel");
#endif
            return b;
        }

        /// <summary>A gunmetal box of the beacon's mount in panel space (the clearance proof reads these lines).</summary>
        void Box(Beacon b, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var go = NightMeshes.Part("Mount", b.root, NightMeshes.Cube(), Plate, false, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), Quaternion.identity,
                                      new Vector3(x1 - x0, y1 - y0, z1 - z0));
            b.solid.Add(go.GetComponent<Renderer>());
        }

        /// <summary>The beacon's root takes the panel's own pose and scale (panel space = beacon space).</summary>
        static void Place(Beacon b)
        {
            if (!b.panel) return;
            b.root.SetPositionAndRotation(b.panel.position, b.panel.rotation);
            if ((b.root.localScale - b.panel.localScale).sqrMagnitude > 1e-10f) b.root.localScale = b.panel.localScale;
        }

        void Glow(Renderer r, Color c, ref float shown)
        {
            if (!r) return;
            float key = c.r + c.g * 1.7f + c.b * 0.6f;
            if (shown >= 0f && (key > 0.0015f) == r.enabled && Mathf.Abs(key - shown) <= Mathf.Max(0.003f, shown * 0.01f)) return;   // an on/off change always goes through (a faint glow could stay lit after the panel went)
            shown = key; bool on = key > 0.0015f;
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            mpb.Clear(); mpb.SetColor(NightMeshes.ColorId, c); r.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------------ every frame: shown with the panel, the spin, the glow, the light, the hum
        void LateUpdate()
        {
            var g = ZGame.I; var cam = Camera.main; var ad = AudioDirector.I;
            float dt = g && g.paused ? 0f : Time.deltaTime;
            foreach (var b in beacons)
            {
                if (!b.root || !b.panel) continue;
                bool shown = b.panelR && b.panelR.enabled && b.panel.gameObject.activeInHierarchy && b.fv.B != null && !b.fv.B.Destroyed;
                if (shown) Place(b);
                if (shown != b.shown) { b.shown = shown; foreach (var r in b.solid) if (r) r.enabled = shown; }
                var o = b.fv.B;
                bool wild = OzFpsFortButton.IsWild(o);                                                   // 1.5 fps_fort_button_win: the panel was pressed: the beacon goes wild
                float hp = o != null && o.MaxHealth > 0 ? (float)o.Health / o.MaxHealth : 1f;
                b.Urgency = Mathf.MoveTowards(b.Urgency, shown && (hp < UrgentShare || wild) ? 1f : 0f, dt / (wild ? 0.1f : 0.6f));
                if (b.Urgency > 0.5f) UrgentFrames++;
                float period = Mathf.Lerp(SpinCalm, SpinUrgent, b.Urgency) * (wild ? OzFpsFortButton.WildSpin : 1f);
                b.angle = (b.angle + dt * 360f / period) % 360f;
                if (b.spin) b.spin.localRotation = Quaternion.Euler(0f, b.angle, 0f);
                float pulse = Mathf.Lerp(1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * PulseHz * (wild ? OzFpsFortButton.WildPulse : 1f) * Mathf.PI)), b.Urgency);
                float face = 0f;
                if (cam && b.spin)
                {
                    var to = cam.transform.position - b.spin.position; var fw = b.spin.forward;
                    if (to.sqrMagnitude > 1e-4f) face = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(to.normalized, fw)), 5f);
                }
                var red = new Color(1f, 0.08f, 0.04f).linear;
                float k = shown ? pulse : 0f;
                Glow(b.bulb, red * (4.5f * k * (1f + 0.4f * b.Urgency)), ref b.bulbShown);
                float dist = cam ? Vector3.Distance(cam.transform.position, b.root.position) : 999f;
                Glow(b.halo, red * (1.6f * k * face * (dist < LightNearM ? 1f : 0f)), ref b.haloShown);   // the flare is for the hallway view only (it reached past the door frame at the RTS camera's 40 deg)
                if (cam && b.halo && b.halo.enabled) b.halo.transform.rotation = Quaternion.LookRotation(cam.transform.position - b.halo.transform.position);
                bool lit = shown && dist < LightNearM;
                if (b.light && b.light.enabled != lit) b.light.enabled = lit;
                if (lit) { b.light.intensity = 2.2f * pulse * (1f + 0.5f * b.Urgency) * (wild ? OzFpsFortButton.WildLight : 1f); LightFrames++; }
                bool hum = shown && dist < HumNearM && ad;
                if (hum)
                {
                    if (!b.humOn) { if (ad.StartLoop(b.key, HumEvent, b.spin.position, b.spin, 0.8f, 1f) > 0) { b.humOn = true; HumStarts++; } }
                    else ad.SetLoop(b.key, Mathf.Lerp(0.8f, 0.8f * HumUrgentVol, b.Urgency) * Mathf.Lerp(1f, pulse, 0.5f), Mathf.Lerp(1f, HumUrgentPitch, b.Urgency));
                }
                else if (b.humOn) { if (ad) ad.StopLoop(b.key, 0.3f); b.humOn = false; HumStops++; }
            }
        }
    }
}
