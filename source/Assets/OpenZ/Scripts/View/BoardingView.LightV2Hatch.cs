using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 light tank v2: the hatch driver never goes through the turret top. The light tank v2's hatch is the model's own: a round opening
    /// in a flat bolted plate, no raised collar (the light v1's built collar stands 0.11 over its plate and hides what is under it). His clips
    /// hold the weapon at his hips (idle: 2 to 44 cm under the rim, his hands 10 to 20 cm outside the opening) and at his waist (shooting:
    /// <list type="bullet">
    /// <item>while his weapon is not up (resting, rising, sinking, a hit while resting) his arms are IN: both arms turned down into the
    /// opening (<see cref="LightV2HatchArms"/>, after WeaponHold), his weapon is not drawn and he does not kick back;</item>
    /// <item>to shoot: the shooting clip starts under the arms-in pose, he rises (2 m/s) until the lowest point of his weapon outside the
    /// opening (its mesh's box on a 3 x 3 x 3 lattice, as WeaponHold put it on the clip's own arms) is over the plate's top (the rim +
    /// <see cref="LightV2PlateOverRim"/>) + <see cref="LightV2WeaponMargin"/>, at most <see cref="LightV2LiftMax"/> (a hit's jolt
    /// included), then brings his arms out (<see cref="LightV2ArmsOutTime"/>); his weapon is drawn once they are out and it is clear;</item>
    /// <item>back: the arms go in first (<see cref="LightV2ArmsInTime"/>, the shooting clip held meanwhile), then the idle clip and the way
    /// down (0.5 m/s); when the lid closes on him while his arms are out, his sink waits until they are in.</item>
    /// </list>
    /// Shots while his weapon is not drawn leave in front of his chest (the Electrobot's rule). Every other vehicle and robot is
    /// unchanged. View only, no sim state (his shots stay the sim's).
    /// </summary>
    public partial class BoardingView
    {
        /// <summary>The top of the hatch plate around the opening (its seat ring and bolts) over the rim, in the model's own units.</summary>
        public const float LightV2PlateOverRim = 0.026f;
        /// <summary>The opening's radius (the dark well) in the model's own units.</summary>
        public const float LightV2OpeningR = 0.367f;
        /// <summary>The clearance kept between the weapon and the plate, and the most he is lifted for it (m at full size; measured need
        /// 0.17 to 0.19 m shooting, up to 0.25 m in a hit's jolt).</summary>
        public const float LightV2WeaponMargin = 0.02f, LightV2LiftMax = 0.30f;
        /// <summary>Seconds: arms in, arms out, and the shooting clip's settle before his arms come out (its cross-fade is 0.15 s).</summary>
        public const float LightV2ArmsInTime = 0.12f, LightV2ArmsOutTime = 0.15f, LightV2ClipSettle = 0.2f;
        /// <summary>Counters (the checks): frames the weapon was not drawn; frames he was lifted, the largest lift (m); frames with his
        /// arms (partly) in; frames his sink waited for his arms.</summary>
        public static int LightV2GunHiddenFrames, LightV2LiftFrames, LightV2ArmsInFrames, LightV2SinkHeldFrames; public static float LightV2LiftMaxSeen;

        class LvHatch { public Renderer[] gun; public Transform crew; public LightV2HatchArms arms; public bool hidden; public float lift, w = 1f, shootT; }
        readonly Dictionary<Rig, LvHatch> lvHatch = new Dictionary<Rig, LvHatch>();

        // the light tank only: the medium tank (OzMediumTank) has the same frame node name and its own hatch
        static bool IsLightV2Hatch(Rig r) => r != null && r.obj != null && r.obj.Type == OzLightTank.Type && r.round && r.round.Frame && r.round.Frame.name == OzLightTank.FrameNode;

        LvHatch LvOf(Rig r) => IsLightV2Hatch(r) && r.crew && lvHatch.TryGetValue(r, out var h) && h.crew == r.crew.transform ? h : null;

        /// <summary>Hook (PlaceCommander, right after the figure's position is set): the light tank v2 driver's arms, lift and weapon.</summary>
        void LightV2HatchClear(Rig r, Transform t, Vector3 up, Vector3 shownAt, bool engaged)
        {
            if (!IsLightV2Hatch(r) || !r.crew || !r.crewWeapon || !r.crewWeapon.gameObject.activeSelf) return;
            if (!lvHatch.TryGetValue(r, out var h) || h.crew != r.crew.transform)
            {
                var arms = r.crew.GetComponent<LightV2HatchArms>(); if (!arms) arms = r.crew.AddComponent<LightV2HatchArms>();
                arms.Init(r.crewAnim, r.crewWeapon);
                h = new LvHatch { crew = r.crew.transform, gun = r.crewWeapon.GetComponentsInChildren<Renderer>(true), arms = arms };
                lvHatch[r] = h;
                if (lvHatch.Count > rigs.Count + 32) LvPurge();
            }
            float dt = Time.deltaTime;
            var fr = r.round.Frame;
            float unit = Mathf.Abs(fr.lossyScale.y);                                                       // the model's scale (x the hull's grow share)
            float full = 0.9302325f * OzLightTank.V2Size;
            Vector3 rim = r.round.RimWorld;
            float plate = Vector3.Dot(rim, up) + LightV2PlateOverRim * unit;
            float margin = LightV2WeaponMargin * unit / full, liftMax = LightV2LiftMax * unit / full * HatchGruntLift(r);   // row 54: x the grunt's hatch size (BoardingView.HatchGrunt.cs)
            // his sink waits while his arms are out (the lid closed on a shooting driver): held up, the arms go in, then he sinks
            bool hold = r.crewPop < 0.999f && h.w < 1f;
            if (hold) { r.crewPop = 1f; t.position = shownAt; LightV2SinkHeldFrames++; }
            bool outWant = r.crewPop >= 0.999f && engaged && !hold;
            // the lift his weapon needs over the plate from here (the arms component's measure of the last frame, on the clip's own arms)
            var a = h.arms;
            float need = !a.measured ? 1f : float.IsNaN(a.lowRel) ? -1f : plate + margin - (Vector3.Dot(t.position, up) + a.lowRel);
            h.shootT = outWant ? h.shootT + dt : 0f;
            float want = outWant && need <= liftMax ? Mathf.Max(need, 0f) : (h.w < 1f ? h.lift : 0f);
            h.lift = Mathf.MoveTowards(h.lift, want, dt * (want > h.lift ? 2f : 0.5f));
            bool ready = outWant && h.shootT >= LightV2ClipSettle && need <= liftMax && h.lift >= need - 0.002f;
            h.w = Mathf.MoveTowards(h.w, ready ? 0f : 1f, dt / (ready ? LightV2ArmsOutTime : LightV2ArmsInTime));
            if (h.w > 0f)
            {   // arms in: no kick back (he holds no gun up), and his shoulders centred over the opening (x w; the idle clip leans 13 cm forward)
                LightV2ArmsInFrames++;
                if (r.kick > 0f) t.position -= (r.crewAim * Vector3.forward) * (0.07f * t.localScale.x * r.kick * r.kick);
                if (a.shoulderOk) t.position += Vector3.ProjectOnPlane(rim - (t.position + a.shoulderRel), up) * h.w;
            }
            if (h.lift > 0f) { t.position += up * h.lift; LightV2LiftFrames++; LightV2LiftMaxSeen = Mathf.Max(LightV2LiftMaxSeen, h.lift); }
            bool hide = h.w > 0f || need - h.lift > 0.0005f;
            if (hide) { LightV2GunHiddenFrames++; if (r.shotAt > 0f) r.shotAt = Mathf.Max(r.shotAt, Time.time + dt); }   // a picture shot waits for his weapon
            if (hide != h.hidden) { h.hidden = hide; foreach (var g in h.gun) if (g) g.enabled = !hide; }
            a.Drive(h.w, rim, up, LightV2OpeningR * unit, h.hidden, h.gun);
        }

        /// <summary>PlaceCommander's clip for the light tank v2 driver: from shooting back to idle only once his arms are in.</summary>
        string LightV2Clip(Rig r, string want)
        {
            var h = LvOf(r);
            return h != null && h.w < 1f && r.crewClip == "shooting" ? "shooting" : want;
        }

        /// <summary>DriverMuzzle: the light tank v2 driver's weapon is not drawn now (his shots leave in front of his chest).</summary>
        bool LightV2GunHidden(Rig r) { var h = LvOf(r); return h != null && h.hidden; }

        void LvPurge()
        {
            var live = new HashSet<Rig>(rigs.Values); var gone = new List<Rig>();
            foreach (var k in lvHatch.Keys) if (!live.Contains(k)) gone.Add(k);
            foreach (var k in gone) lvHatch.Remove(k);
        }

        /// <summary>The light tank v2 drivers now (the editor check, View/LightV2HatchCheck.cs): the figure, its weapon, the turret mesh node, the hatch frame.</summary>
        public struct LightV2Crew { public UnitView view; public GameObject crew; public Transform weapon, turret, frame; public float pop, flinch, lift, arms; public bool gunHidden; }

        public static void LightV2Crews(List<LightV2Crew> into)
        {
            into.Clear();
            if (!I) return;
            foreach (var kv in I.rigs)
            {
                var r = kv.Value;
                if (!IsLightV2Hatch(r) || !r.crew) continue;
                Transform tur = null;
                foreach (var x in r.root.GetComponentsInChildren<Transform>(true)) if (x.name == "Tank_turret") { tur = x; break; }
                var h = I.LvOf(r);
                into.Add(new LightV2Crew { view = kv.Key, crew = r.crew, weapon = r.crewWeapon, turret = tur, frame = r.round.Frame, pop = r.crewPop, flinch = r.flinch,
                                           lift = h != null ? h.lift : 0f, arms = h != null ? h.w : 0f, gunHidden = h != null && h.hidden });
            }
        }
    }
}
