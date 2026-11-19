// Do not edit by hand: re-measure (place_fx.py, pick_spots.py) and re-write.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBigTanksFx
    {
        /// <summary>One lamp pad: the anchor ('turret' = the turret's yaw node, 'hull' = TankChassis), the pad's top centre in the anchor's
        /// frame, its height (foot 2 cm under the lowest roof point of its footprint, top 3 cm over the highest) and footprint radius.</summary>
        public struct Pad { public string anchor; public Vector3 top; public float height, radius; public Pad(string a, Vector3 t, float h, float r) { anchor = a; top = t; height = h; radius = r; } }
        /// <summary>One neon run on a side skirt's lower rim: the side (+1 left = +x, -1 right), its height, the tube's centre x (6 mm proud of
        /// the flat panel it lies on), its ends along z (TankRoot frame).</summary>
        public struct Neon { public float side, y, x, z0, z1; public Neon(float s, float yy, float xx, float a, float b) { side = s; y = yy; x = xx; z0 = a; z1 = b; } }
        public static readonly Neon[] MammothNeon =
        {
            new Neon(1f, 0.66f, 2.3173f, -2.295f, -1.605f),
            new Neon(1f, 0.66f, 2.3172f, -1.445f, -0.705f),
            new Neon(1f, 0.66f, 2.3163f, -0.445f, 0.27f),
            new Neon(1f, 0.66f, 2.3179f, 0.93f, 1.545f),
            new Neon(-1f, 0.66f, -2.3155f, -2.295f, -1.905f),
            new Neon(-1f, 0.66f, -2.3206f, -1.67f, -0.78f),
            new Neon(-1f, 0.66f, -2.3197f, -0.345f, 0.345f),
            new Neon(-1f, 0.66f, -2.3177f, 1.005f, 1.67f),
        };
        public static readonly Pad MammothExhaustL = new Pad("hull", new Vector3(1.05f, 1.9104f, 2.25f), 0.1338f, 0.11f);
        public static readonly Pad MammothExhaustR = new Pad("hull", new Vector3(-1.05f, 1.8972f, 2.25f), 0.1234f, 0.11f);
        public static readonly Pad MammothMarkerL = new Pad("turret", new Vector3(0.95f, 0.8123f, 0.9819f), 0.0662f, 0.06f);
        public static readonly Pad MammothMarkerR = new Pad("turret", new Vector3(-0.95f, 0.8081f, 0.9819f), 0.0736f, 0.06f);
        public static readonly Pad MammothSearch = new Pad("turret", new Vector3(-0.75f, 0.8603f, -0.8181f), 0.1776f, 0.19f);
        public static readonly Neon[] BizonNeon =
        {
            new Neon(1f, 0.78f, 2.3688f, -2.045f, -0.805f),
            new Neon(1f, 0.78f, 2.3746f, 0.355f, 0.97f),
            new Neon(1f, 0.78f, 2.3742f, 1.855f, 2.22f),
            new Neon(-1f, 0.78f, -2.3019f, -2.87f, -2.405f),
            new Neon(-1f, 0.78f, -2.376f, -1.02f, -0.205f),
            new Neon(-1f, 0.78f, -2.3756f, 0.18f, 2.27f),
            new Neon(-1f, 0.78f, -2.3013f, 2.405f, 2.77f),
        };
        public static readonly Pad BizonBeacon = new Pad("turret", new Vector3(0.8f, 0.6206f, 0.4004f), 0.0772f, 0.13f);
        public static readonly Pad BizonMarkerL = new Pad("hull", new Vector3(1.2f, 2.4203f, 2.8f), 0.0514f, 0.06f);
        public static readonly Pad BizonMarkerR = new Pad("hull", new Vector3(-1.2f, 2.4178f, 2.8f), 0.0525f, 0.06f);
        public static readonly Pad BizonSearch = new Pad("turret", new Vector3(-0.75f, 0.6342f, 0.4004f), 0.0882f, 0.19f);
    }
}
