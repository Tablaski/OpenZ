using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV161 EvilZod, the view's front door (the rules: Sim/ZEvilZod.cs). His prefab Resources/OpenZ/HeroSquad/Z_bs01_evilzod is made by the editor menu OpenZ/V1.3/EvilZod/1 Build
    /// (Editor/OzHeroSquadSetup.EvilZod.cs) and loads from Resources, because ZGame.unitPrefabs is a list inside each scene. While the prefab is missing he is drawn as the game's
    /// plain robot, with a line in the log. Everything here runs only for him: no level, no parity row, no other unit passes through it.
    /// </summary>
    public static class OzEvilZodView
    {
        public const string PrefabKey = "Z_bs01_evilzod", PrefabPath = OzHeroSquadView.PrefabDir + PrefabKey;
        /// <summary>How many grunts tall he is drawn: the pick box, the frame, the ring and the bar follow this one number (OzHeroSquadView.Tall).</summary>
        public const float Tall = 1.8f;
        /// <summary>His model's scale at that height (menu 1 measured 1.5427 at 1.5 x): only the eye lines' spacing reads it.</summary>
        public const float ModelScale = 1.85f;
        /// <summary>His view spacing against other units (m); the sim's body radius is 9 px.</summary>
        public const float ViewRadius = 1.38f;

        static GameObject prefab; static bool looked, warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { prefab = null; looked = false; warned = false; }

        /// <summary>Editor: forget the prefab after the build menu made it.</summary>
        public static void Forget() { prefab = null; looked = false; }

        static GameObject Prefab
        {
            get
            {
                if (!prefab && !looked) { looked = true; prefab = Resources.Load<GameObject>(PrefabPath); }
                if (!prefab && !warned) { warned = true; Debug.LogWarning("[OpenZ EvilZod] Resources/" + PrefabPath + " is missing (menu OpenZ/V1.3/EvilZod/1 Build): he is drawn as a plain robot"); }
                return prefab;
            }
        }

        static bool Is(ZObject o) => o != null && o.Type == ZEvilZod.Type;

        /// <summary>ZGame.SpawnUnitView: his view prefab.</summary>
        public static GameObject ForUnit(ZObject o, GameObject found) => Is(o) && Prefab ? Prefab : found;

        /// <summary>ZGame.Prefab: the Unit editor's drop preview and the lost-crew figure ask by key.</summary>
        public static GameObject PrefabFor(string key, GameObject found) => key == PrefabKey && Prefab ? Prefab : found;

        /// <summary>ZGame.SpawnUnitView, before UnitView.Bind paints him: his own materials for the drawn team colour.</summary>
        public static void Dress(GameObject go, ZObject o)
        {
            if (!go || !Is(o)) return;
            var look = go.GetComponent<OzHeroLook>(); if (look) look.Apply(o.Owner);
        }

        /// <summary>ZGame.SpawnUnitView, after UnitView.Bind: his legs, his upper body, his effects. On the plain robot model (no prefab yet) nothing is added.</summary>
        public static void OnSpawn(UnitView v)
        {
            if (!v || !Is(v.Obj)) return;
            var body = v.GetComponentInChildren<Animator>();
            if (!body || !body.isHuman || !v.GetComponent<OzHeroLook>()) return;
            OzHeroLegs.Attach(v, OzEvilZodBody.Member);
            var hold = v.GetComponent<WeaponHold>(); if (hold) { hold.Suspended = true; hold.enabled = false; }   // his hands are OzEvilZodBody's
            var b = OzEvilZodBody.Attach(v);
            if (b && b.Ready) v.gameObject.AddComponent<OzEvilZodFx>().Bind(v, b);
            v.EvilZodSpacing();
        }

        /// <summary>BoardingView.SpawnFigure: a seated, waiting or thrown figure of him (no unit behind it) wears his paint and holds his gun.</summary>
        public static void Figure(GameObject go, string type, Team team)
        {
            if (!go || type != ZEvilZod.Type) return;
            var look = go.GetComponent<OzHeroLook>(); if (look) look.Apply(team);
            var an = go.GetComponentInChildren<Animator>();
            if (an && an.isHuman && look) an.gameObject.AddComponent<OzEvilZodBody>().Start2();
        }

        /// <summary>The drawn point of a burst: the rules' own, every frame (UnitView.Sync).</summary>
        public static bool InBurst(ZObject o) => o != null && o.EvilPhase == ZEvilPhase.Burst;
    }
}
