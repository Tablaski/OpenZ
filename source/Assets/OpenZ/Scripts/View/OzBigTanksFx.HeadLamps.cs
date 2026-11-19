// Before: the Mammoth's and the Bizon's head lamps were the heavy tank's lamp data (lens 0.12 m, hot core 0.18 m, halo 0.85 m) on a bare glacis
// socket whose normal points 61 deg up, so from the RTS camera (pitch 55) every front showed two full round discs that always faced the camera
// (halo per side 2.27 m2 at every heading: 1.8 x the heavy tank's average, 2.8 x the light tank's).
// Now: per side a small armoured twin-lamp housing on the glacis (measured on the game GLBs: kits/big_tanks_lamp_size/kit_tools/measure_housings.py,
// proof/housings.txt) with two small hard lamps in its front face: glass r 0.040 m, glow lens 0.040 m, hot core 0.060 m, halo 0.36 m (42 % of 0.85),
// facing forward 7 deg up like the family's lamps (so they fade when the tank looks away), slightly warm white. The haze beams, the real spot and
// the pool start at the OUTER lamp of each pair only (headlight.sockets now decides which head lamps carry a beam; every other vehicle lists all
// of its head lamps there, so nothing changes for them). Tail lamps, neon, markers, searchlight, beacon: unchanged.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBigTanksFx
    {
        /// <summary>One side's twin-lamp housing in the TankChassis frame (= the prefab root frame at rest, +y up, front = -z): centre x, front
        /// face z, the glacis top under the front face (measured on the hull mesh) and the glacis rise per metre backwards.</summary>
        public struct HeadHousing { public float x, zFront, yPlate, rise; public HeadHousing(float xx, float zf, float yp, float r) { x = xx; zFront = zf; yPlate = yp; rise = r; } }

        // measure_housings.py: Mammoth plate x 0.65..1.10 flat (barrels >= 0.88 m over it, a raised block from z -2.07 behind); Bizon plate
        // x 0.40..0.95 (a bevel from x 1.00), its front edge at z -3.225.
        public static readonly HeadHousing[] MammothHead = { new HeadHousing(0.85f, -2.267f, 1.538f, 0.555f), new HeadHousing(-0.85f, -2.267f, 1.538f, 0.555f) };
        public static readonly HeadHousing[] BizonHead = { new HeadHousing(0.82f, -2.987f, 1.894f, 0.55f), new HeadHousing(-0.82f, -2.987f, 1.894f, 0.55f) };

        /// <summary>Housing width, depth, height over the plate at its front face, sunk into the plate.</summary>
        public const float HeadW = 0.24f, HeadD = 0.13f, HeadH = 0.125f, HeadSink = 0.03f;
        /// <summary>Each lamp's glass radius, its centre x from the housing centre, its centre height over the plate at the front face.</summary>
        public const float HeadLampR = 0.040f, HeadLampGap = 0.056f, HeadLampY = 0.062f;
        /// <summary>The VehicleLights glow of each lamp: lens disc radius (its hot core is 1.5 x), halo radius (was 0.12 / 0.85, one lamp a side).</summary>
        public const float HeadGlowLens = 0.040f, HeadGlowHalo = 0.36f;
        /// <summary>The lamps' facing for the glow: forward (-z), 7 deg up (the heavy tank's are 13 deg up, the light tank's level).</summary>
        public static readonly Vector3 HeadNrm = new Vector3(0f, 0.122f, -0.9925f).normalized;
        /// <summary>Slightly warm white (the family's head lamps: 1, 0.84, 0.6).</summary>
        static readonly float[] HeadRgb = { 1f, 0.88f, 0.70f };
        public const string HeadNode = "TankChassis";
        public static int LampSpecsTuned;

        /// <summary>A lamp's glass centre (k 0 = the outer lamp, 1 = the inner one), TankChassis frame.</summary>
        public static Vector3 HeadLampAt(HeadHousing h, int k)
        {
            float sx = Mathf.Sign(h.x);
            return new Vector3(h.x + sx * (k == 0 ? HeadLampGap : -HeadLampGap), h.yPlate + HeadLampY, h.zFront - 0.004f);
        }

        /// <summary>VehicleLights.Awake, for every vehicle_lights.json entry: the big tanks' head sockets become two small lamps a side in the
        /// housings (node TankChassis, which the hull mesh and the housings follow); headlight.sockets = the outer lamps (the beams).</summary>
        public static void LampSpec(VLVehicle v)
        {
            if (v == null || v.sockets == null) return;
            var hs = v.type == OzBigTanks.Mammoth ? MammothHead : v.type == OzBigTanks.Bizon ? BizonHead : null;
            if (hs == null) return;
            VLSocket pattern = null; var keep = new List<VLSocket>();
            foreach (var s in v.sockets) { if (s == null) continue; if (s.kind == "head") { if (pattern == null) pattern = s; } else keep.Add(s); }
            if (pattern == null) return;
            var heads = new List<VLSocket>();
            foreach (var h in hs)
                for (int k = 0; k < 2; k++)
                {
                    var p = HeadLampAt(h, k) + HeadNrm * 0.006f;                                         // 6 mm in front of the glass
                    var s = JsonUtility.FromJson<VLSocket>(JsonUtility.ToJson(pattern));
                    s.id = "head_" + (h.x > 0f ? "L" : "R") + (k == 0 ? "" : "2"); s.kind = "head"; s.node = HeadNode; s.nodeScale = 1f; s.surface = "OzBigTankHeadHousing";
                    s.pos = new[] { p.x, p.y, p.z }; s.root = new[] { p.x, p.y, p.z };
                    s.nrm = new[] { HeadNrm.x, HeadNrm.y, HeadNrm.z }; s.rootNrm = new[] { HeadNrm.x, HeadNrm.y, HeadNrm.z };
                    s.size = HeadGlowLens; s.halo = HeadGlowHalo; s.rgb = (float[])HeadRgb.Clone(); s.colour = "head"; s.directional = true;
                    heads.Add(s);
                }
            heads.AddRange(keep);
            v.sockets = heads.ToArray();
            if (v.headlight != null) { v.headlight.node = HeadNode; v.headlight.sockets = new[] { "head_L", "head_R" }; }
            LampSpecsTuned++;
        }

        /// <summary>VehicleLights.Build: does this head / eye lamp carry a haze beam, the real spot and the pool? The ones headlight.sockets lists
        /// (every vehicle lists all of its head / eye lamps there except the big tanks, whose inner lamps glow only); no list = every lamp.</summary>
        public static bool BeamLamp(VLVehicle v, VLSocket s)
        {
            var ids = v != null && v.headlight != null ? v.headlight.sockets : null;
            return ids == null || ids.Length == 0 || s == null || Array.IndexOf(ids, s.id) >= 0;
        }

        public static int HeadHousingsBuilt;

        /// <summary>Tank.Build: the two twin-lamp housings on the glacis, each a gunmetal block sunk 3 cm into the plate, a visor over the lamps, a
        /// divider, per lamp a bezel and a dark glass (VehicleLights draws the glow on the glass). <paramref name="hullPart"/> = the Tank's own
        /// Part(name, "hull", ...) with statik on: shown / hidden with the hull (the break-up), never children of the tank.</summary>
        public static void BuildHeadLamps(bool mam, Func<string, Mesh, Material, bool, Vector3, Quaternion, Vector3, GameObject> hullPart)
        {
            var H = NightMeshes.Housing(); var G = NightMeshes.DarkGlass();
            var face = Quaternion.LookRotation(Vector3.back, Vector3.up);                                  // the Disc's +z -> the front (-z)
            foreach (var h in mam ? MammothHead : BizonHead)
            {
                float yB = h.yPlate - HeadSink, yT = h.yPlate + HeadH;
                hullPart("HeadLamp_Housing", NightMeshes.Cube(), H, true, new Vector3(h.x, (yB + yT) * 0.5f, h.zFront + HeadD * 0.5f), Quaternion.identity, new Vector3(HeadW, yT - yB, HeadD));
                hullPart("HeadLamp_Visor", NightMeshes.Cube(), H, true, new Vector3(h.x, yT + 0.009f, h.zFront - 0.01f), Quaternion.identity, new Vector3(HeadW + 0.02f, 0.018f, 0.07f));
                hullPart("HeadLamp_Divider", NightMeshes.Cube(), H, false, new Vector3(h.x, h.yPlate + HeadLampY, h.zFront - 0.008f), Quaternion.identity, new Vector3(0.014f, 0.095f, 0.016f));
                for (int k = 0; k < 2; k++)
                {
                    var p = HeadLampAt(h, k);
                    hullPart("HeadLamp_Bezel", NightMeshes.Disc(), H, false, p + new Vector3(0f, 0f, 0.002f), face, Vector3.one * (HeadLampR + 0.008f));
                    hullPart("HeadLamp_Glass", NightMeshes.Disc(), G, false, p, face, Vector3.one * HeadLampR);
                }
                HeadHousingsBuilt++;
            }
        }
    }
}
