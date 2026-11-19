using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// while it drives, more when it sets off or speeds up. The wisps are the shuttle's exhaust (View/OzShuttleExhaust.cs on the same object, its
    /// two mouths = the model's MK_Exhaust_L / MK_Exhaust_R; the game's soft smoke channel leg_smoke, the level's wind, nothing off screen or
    /// paused); this component sets how strong and how dark they are from the drive (VehicleMotion: speed, acceleration, setting off):
    /// standing with a crew a thin pale wisp, driving more and greyer with the speed, setting off or a hard speed-up a short dark burst
    /// (<see cref="burstSeconds"/>) with a diesel chuff (sound event <see cref="ChuffEvent"/>: every visible action has its sound). Empty, neutral
    /// or destroyed: none (the wreck has its own smoke). The menu OpenZ/1.5/Big Tanks/1 puts both components on the Bizon's prefab. View only.
    /// </summary>
    [RequireComponent(typeof(OzShuttleExhaust))]
    public sealed class OzBigTankExhaust : MonoBehaviour
    {
        public const string ChuffEvent = "veh_exhaust_chuff";
        [Tooltip("the wisps' strength standing, at full drive, in a burst (OzShuttleExhaust.strength)")] public float idleStrength = 0.35f, driveStrength = 1.15f, burstStrength = 3.2f;
        [Tooltip("the speed (m/s) at which the drive's strength is full")] public float fullSpeed = 1.2f;
        [Tooltip("a speed-up harder than this (m/s2) makes a burst")] public float burstAccel = 0.55f;
        [Tooltip("how long a burst fades (s)")] public float burstSeconds = 1.1f;
        public Color idleColour = new Color(0.80f, 0.81f, 0.82f, 1f), driveColour = new Color(0.62f, 0.62f, 0.62f, 1f), burstColour = new Color(0.27f, 0.26f, 0.25f, 1f);

        /// <summary>Proof: bursts (setting off / speed-ups) and chuffs played.</summary>
        public static int Bursts, Chuffs;
        public float Burst { get; private set; }

        OzShuttleExhaust ex; VehicleMotion vm; UnitView uv; bool wasMoving; float nextChuff;

        void Awake() { ex = GetComponent<OzShuttleExhaust>(); if (ex) ex.SetMode(2); }

        void Update()
        {
            if (!Application.isPlaying || !ex) return;
            if (!uv) uv = GetComponent<UnitView>();
            if (!vm) vm = GetComponent<VehicleMotion>();
            var o = uv ? uv.Obj : null;
            bool on = o != null && !o.Destroyed && o.Owner != Team.Neutral && o.Drivers.Count > 0 && vm && !uv.Dying;
            if (!on) { ex.SetMode(2); wasMoving = false; Burst = 0f; return; }
            ex.SetMode(0);
            float sp = Mathf.Abs(vm.DriveSpeed), ac = vm.DriveAccel; bool moving = vm.DriveMoving;
            if (moving && !wasMoving) Puff(1f);
            else if (moving && ac > burstAccel && Burst < 0.25f) Puff(0.7f);
            wasMoving = moving;
            Burst = Mathf.Max(0f, Burst - Time.deltaTime / Mathf.Max(0.1f, burstSeconds));
            float k = Mathf.Clamp01(sp / Mathf.Max(0.05f, fullSpeed));
            float s = moving ? Mathf.Lerp(idleStrength, driveStrength, k) : idleStrength;
            ex.strength = Mathf.Max(s, burstStrength * Burst);
            ex.colour = Color.Lerp(moving ? Color.Lerp(idleColour, driveColour, k) : idleColour, burstColour, Burst);
        }

        void Puff(float power)   // not "Start": Unity calls a Start() message and refuses one with parameters
        {
            Burst = Mathf.Max(Burst, power); Bursts++;
            if (Time.time < nextChuff) return;
            nextChuff = Time.time + 0.6f;
            var p = ex.mouths != null && ex.mouths.Length > 0 && ex.mouths[0] ? ex.mouths[0].position : transform.position + Vector3.up * 2.5f;
            AudioDirector.Fire(ChuffEvent, p, uv && uv.Obj != null ? uv.Obj.Id : -1, Mathf.Lerp(0.7f, 1f, power), Random.Range(0.92f, 1.06f));
            Chuffs++;
        }
    }
}
