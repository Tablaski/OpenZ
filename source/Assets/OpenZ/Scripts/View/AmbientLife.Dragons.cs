using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Level 7 "Pyro Technics": three lava dragons in the place of Z's volcanic birds (the original's pterodactyls, bird_volcanic_*,
    /// abird.cpp). They ARE the eagle of the desert (the same soar, thermal circles, swoops, call timer, hit, fall, respawn from the map
    /// edge), with their own model, size, call and hit sounds, embers instead of feathers, and routes over the four lava pits and the
    /// border volcanoes. A swoop skims low over the lava field below its circle. View only, like every critter (no sim object).
    /// Model contract (asset to make): Resources/OpenZ/Critters/LavaDragon (FBX), the critter clip layout of build_critters.py (24 fps:
    /// flap 1-15, tuck 2, limp 6, stretch 1, glide 18, perch 20), bones "head", "wing_1_L", "wing_1_R", the shared critter palette atlas,
    /// 4.5 m wingspan at scale 1, nose along +Z. Without the model nothing spawns (NewBird returns null) and the level simply has no dragons.
    /// </summary>
    public partial class AmbientLife
    {
        [Header("Level 7")]
        public bool dragons = true;
        [Tooltip("size multiplier on top of the FBX size (lava dragon 4.5 m wingspan at 1)")] public float dragonScale = 1f;

        public const string SndDragonScream = "amb_l07_dragon_scream";
        public const string SndDragonCall = "amb_l07_dragon_call";        // NEW: distant screech-roar, a soft fire puff at the end
        public const string SndDragonHit = "critter_dragon_hit";          // NEW: hit: a squawk-roar and a spray of embers

        /// <summary>Level 7 dragon routes: thermal circles (x, z, radius) over the lava pits (tile boxes from the level 7 import:
        /// north 37-56 x 36-44, the ring 26-42 x 44-62, east 49-55 x 49-59, south 36-55 x 65-73) and the border volcanoes, visited in turn.</summary>
        static readonly Vector3[][] L07DragonRoutes =
        {
            new[] { new Vector3(94f, -81f, 16f), new Vector3(69f, -107f, 15f), new Vector3(36f, -28f, 18f) },
            new[] { new Vector3(92f, -139f, 16f), new Vector3(105f, -109f, 13f), new Vector3(134f, -192f, 18f) },
            new[] { new Vector3(69f, -107f, 15f), new Vector3(92f, -139f, 16f), new Vector3(28f, -178f, 18f) },
        };
        static readonly float[] L07DragonAlt = { 30f, 27f, 33f };

        void SpawnDragons()
        {
            for (int i = 0; i < L07DragonRoutes.Length; i++)
            {
                var b = NewBird(true, i, "OpenZ/Critters/LavaDragon", paletteMat, dragonScale);
                if (b == null) return;
                b.dragon = true; DragonRigBones(b);                                                               // DV163: the neck and the mouth (AmbientLife.DragonAttack.cs)
                b.route = L07DragonRoutes[i]; b.alt = L07DragonAlt[i]; b.clear = 11f; b.transitAlt = b.alt;
                b.span = 4.5f * dragonScale; b.hitR = 1.8f * dragonScale; b.flapRate = 0.9f;                       // bigger and slower wings than the eagle
                b.cruise = 7.8f + R(b.rng, -0.6f, 0.6f); b.speed = b.cruise; b.dir = i % 2 == 0 ? 1f : -1f;
                var w = b.route[0]; b.centre = new Vector2(w.x, w.y); b.radius = w.z; b.hold = R(b.rng, 25f, 60f);
                float a = R(b.rng, 0f, Mathf.PI * 2f);
                b.pos = new Vector3(b.centre.x + Mathf.Cos(a) * b.radius, b.alt, b.centre.y + Mathf.Sin(a) * b.radius);
                b.yaw = Mathf.Atan2(-Mathf.Sin(a) * b.dir, Mathf.Cos(a) * b.dir) * Mathf.Rad2Deg;
                b.st = BS.Soar; b.burstT = R(b.rng, 2f, 9f); b.callT = 3f + R(b.rng, 0f, 5f);
                b.swoopT = R(b.rng, swoopEvery.x, swoopEvery.y) * 0.6f + i * 11f;
                birds.Add(b);
            }
        }

        /// <summary>Embers in the place of feathers (a hit, the trail of a falling dragon, its landing): small fire puffs.</summary>
        void DragonEmbers(Vector3 at, int n)
        {
            var fx = FxManager.I;
            if (!fx) return;
            if (n <= 1 && Time.time - lastEmberTrail < 0.36f) return;                                // the falling trail: one puff in three of the feather trail's
            if (n <= 1) lastEmberTrail = Time.time;
            fx.Burn(at, Mathf.Clamp(0.08f * n, 0.08f, 0.6f));
        }
        float lastEmberTrail = -10f;
    }
}
