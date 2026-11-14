// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the light on the two robots (OzEvilStage).
// 400 x 300 comms window EvilZod (dark blue metal) read as a dark shape and the window's 5 % static showed on the black. Now each robot
// has its own film lighting that goes where he stands (every scene puts them on other marks): a key from the front left and above (soft
// shadows), a warm red rim and a cool blue rim from behind on the two sides (his outline off the dark walls), and for EvilZod a small
// cold light from below the lens that reaches under the hat brim (his face and red eyes, the villain's light); BS-01 a key and a rim of
// his own at his height. The room's fill, the banner uplights, the console glow, the alcove spots and the alarm beacon stay.
// Looked at in the engine on the raw feed and in the window (proof/redo/grabs/l2_lights_*).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        Light rimB, ezUp, skKey, skRim;
        readonly List<Transform> beaconHeads = new List<Transform>(); readonly List<Light> beaconLights = new List<Light>(); Material beaconDomeMat;
        /// <summary>
        /// turn speed (deg/s).</summary>
        public static float WallBeaconI = 70f, WallBeaconSpin = 250f;
        Light alarmWash, ezHatRim;
        /// <summary>The robots' lights (intensity): EvilZod's key, warm rim, cool rim, face light; BS-01's key and rim.</summary>
        public static float EzKeyI = 45f, EzRimI = 45f, EzRimBI = 35f, EzUpI = 7f, SkKeyI = 18f, SkRimI = 18f, FillI = 9f, WashI = 4f;

        void BuildCharacterLights()
        {
            key = AddLight("EzKey", LightType.Spot, new Vector3(1.3f, 3.5f, 3.3f), new Vector3(-0.3f, 1.7f, 0f), new Color(1f, 0.95f, 0.88f), EzKeyI, 12f, 62f, true);
            key.innerSpotAngle = 30f;
            rim = AddLight("EzRimWarm", LightType.Spot, new Vector3(-1.6f, 3.6f, -2.3f), new Vector3(-0.4f, 1.9f, 0.2f), new Color(1f, 0.45f, 0.3f), EzRimI, 9f, 55f);
            rimB = AddLight("EzRimCool", LightType.Spot, new Vector3(1.6f, 3.6f, -2.2f), new Vector3(0.2f, 1.6f, 0.2f), new Color(0.45f, 0.6f, 1f), EzRimBI, 9f, 60f);
            ezHatRim = AddLight("EzHatRim", LightType.Spot, new Vector3(0.3f, 3.5f, -1.6f), new Vector3(-0.5f, 2.3f, 0f), new Color(0.8f, 0.88f, 1f), 26f, 7f, 30f);
            ezUp = AddLight("EzFace", LightType.Spot, new Vector3(-0.3f, 0.35f, 1.5f), new Vector3(-0.5f, 2.2f, 0f), new Color(0.75f, 0.85f, 1f), EzUpI, 4f, 40f);
            skKey = AddLight("SkKey", LightType.Spot, new Vector3(1.8f, 2.6f, 2.7f), new Vector3(0.9f, 0.8f, 0.3f), new Color(1f, 0.95f, 0.88f), SkKeyI, 7f, 50f);
            skRim = AddLight("SkRim", LightType.Spot, new Vector3(1.5f, 2.4f, -1.5f), new Vector3(0.9f, 0.9f, 0.3f), new Color(0.5f, 0.65f, 1f), SkRimI, 6f, 55f);
            fill = AddLight("Fill", LightType.Point, new Vector3(-2.6f, 1.6f, 2.8f), new Vector3(-2.6f, 1.6f, 2.8f), new Color(0.55f, 0.68f, 1f), FillI, 8f);
            BuildWallBeacons();
            // the back wall's wash at both sides (the banners and the steel read in the 400 x 300 window instead of black: proof/redo/grabs/l3_cmp.png)
            foreach (float x in new[] { 2.6f, -2.6f })
                AddLight("WallWash" + x, LightType.Point, new Vector3(x, 3.2f, -1.9f), new Vector3(x, 3.2f, -1.9f), new Color(0.55f, 0.6f, 1f), WashI, 5.5f);
        }

        /// <summary>Three red rotating alarm beacons on the back wall (left of the doors' wall, over the doors, right of it): a dark base, a red
        /// dome that glows, a spot inside that turns and sweeps the walls, the floor and the robots with red. Dark until the alarm is on.</summary>
        void BuildWallBeacons()
        {
            var dark = Lit(new Color32(22, 24, 28, 255), 0.4f, 0.5f);
            beaconDomeMat = Glow(new Color32(120, 6, 4, 255), new Color(1f, 0.03f, 0.01f), 0.15f);
            // three on the back wall (EvilZod's shots) and two on the console (BS-01's shots look past him at it: proof/redo/f2_lose_feeds.jpg)
            foreach (var at in new[] { new Vector3(-3.25f, 3.55f, -3.17f), new Vector3(0f, 3.30f, -3.17f), new Vector3(3.25f, 3.55f, -3.17f), new Vector3(1.62f, 1.13f, -2.75f), new Vector3(3.08f, 1.13f, -2.75f) })
            {
                var root = new GameObject("WallBeacon") { layer = Layer }.transform; root.SetParent(transform, false); root.localPosition = at;
                Box("BeaconBase", new Vector3(0f, -0.07f, -0.02f), new Vector3(0.24f, 0.06f, 0.14f), dark, root, false);
                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name = "BeaconDome"; Destroy(dome.GetComponent<Collider>());
                dome.transform.SetParent(root, false); dome.transform.localPosition = new Vector3(0f, 0.02f, 0f); dome.transform.localScale = new Vector3(0.17f, 0.15f, 0.17f);
                Dress(dome, beaconDomeMat, false);
                var head = new GameObject("BeaconHead") { layer = Layer }.transform; head.SetParent(root, false); head.localPosition = new Vector3(0f, 0.02f, 0.03f);
                var l = AddLight("BeaconSpot", LightType.Spot, Vector3.zero, Vector3.zero, new Color(1f, 0.06f, 0.03f), 0f, 10f, 34f, false, head);   // a narrower, brighter beam: a visible sweep
                l.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
                beaconHeads.Add(head); beaconLights.Add(l); beaconRoots.Add(root);
            }
            alarmWash = AddLight("AlarmWash", LightType.Point, new Vector3(0f, 3.6f, -0.6f), new Vector3(0f, 3.6f, -0.6f), new Color(1f, 0.08f, 0.04f), 0f, 10f);   // the whole room pulses red
            SetWallBeacons(false);
        }
        readonly List<Transform> beaconRoots = new List<Transform>();
        /// <summary>The wall beacons are part of the LOSE scene only (the approved briefing / nuke / WIN pictures stay as they were).</summary>
        void SetWallBeacons(bool shown) { foreach (var r in beaconRoots) if (r && r.gameObject.activeSelf != shown) r.gameObject.SetActive(shown); }

        void TickWallBeacons()
        {
            if (beaconHeads.Count == 0) return;
            float flash = alarmOn ? (Mathf.Sin(clock * 9f) > 0f ? 1f : 0.15f) : 0f;                          // on / off: an alarm, not a lamp
            for (int i = 0; i < beaconHeads.Count; i++)
            {
                if (alarmOn) beaconHeads[i].localRotation = Quaternion.Euler(0f, clock * WallBeaconSpin + i * 120f, 0f);
                beaconLights[i].intensity = alarmOn ? WallBeaconI * (0.45f + 0.55f * flash) : 0f;
            }
            if (beaconDomeMat) beaconDomeMat.SetVector("_EmissionColor", (Vector4)(new Color(1f, 0.03f, 0.01f) * (alarmOn ? 0.4f + 3.0f * flash : 0.1f)));   // deep red, flashing
            float pulse = alarmOn ? 0.5f + 0.5f * Mathf.Sin(clock * 5.2f) : 0f;
            if (alarmWash) alarmWash.intensity = alarmOn ? 2.5f + 4f * pulse : 0f;
            // the robots' rims pulse red (review)
            if (rimB) rimB.color = alarmOn ? Color.Lerp(new Color(0.45f, 0.6f, 1f), new Color(1f, 0.12f, 0.08f), pulse) : new Color(0.45f, 0.6f, 1f);
            if (skRim) skRim.color = alarmOn ? Color.Lerp(new Color(0.5f, 0.65f, 1f), new Color(1f, 0.12f, 0.08f), pulse) : new Color(0.5f, 0.65f, 1f);
        }

        void Aim(Light l, Vector3 posLocal, Vector3 atLocal)
        {
            if (!l) return;
            l.transform.position = transform.TransformPoint(posLocal);
            Vector3 d = transform.TransformPoint(atLocal) - l.transform.position;
            if (d.sqrMagnitude > 1e-6f) l.transform.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>Every frame: the robots' lights go where the robots stand (stage axes, so the look is the same on every mark).</summary>
        void PlaceCharacterLights()
        {
            float H = EzHeight, h = SkHeight;
            if (ez != null && ez.Ok && ezOnStage)
            {
                Vector3 e = EzPos;
                Aim(key, e + new Vector3(1.85f, 3.5f, 3.3f), e + new Vector3(0.2f, 0.74f * H, 0f));
                Aim(rim, e + new Vector3(-1.1f, 3.6f, -2.3f), e + new Vector3(0f, 0.82f * H, 0.2f));
                Aim(rimB, e + new Vector3(2.1f, 3.6f, -2.2f), e + new Vector3(0.3f, 0.70f * H, 0.2f));
                Aim(ezUp, e + new Vector3(0.25f, 0.35f, 1.5f), e + new Vector3(0f, 0.95f * H, 0f));
                Aim(ezHatRim, e + new Vector3(0.4f, 3.6f, -1.7f), e + new Vector3(0f, 0.97f * H, 0.05f));
            }
            if (sk != null && sk.Ok)
            {
                Vector3 s = SkPos;
                Aim(skKey, s + new Vector3(0.9f, 2.6f, 2.4f), s + new Vector3(0f, 0.6f * h, 0f));
                Aim(skRim, s + new Vector3(0.6f, 2.4f, -1.8f), s + new Vector3(0f, 0.7f * h, 0f));
                if (skKey) skKey.enabled = skOnStage; if (skRim) skRim.enabled = skOnStage;
            }
            TickWallBeacons();
        }
    }
}
