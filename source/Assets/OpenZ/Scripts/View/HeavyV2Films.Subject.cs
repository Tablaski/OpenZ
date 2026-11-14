#if UNITY_EDITOR
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The v2 films (View/HeavyV2Films.cs) for more than the heavy tank (k84): every job (tread, battle, board, board_close, fight, snipe,
    /// turret, recoil, combat, plant) films the unit the menu asked for, on the same fixed open spots of L02 (View/HeavyV2Spots.cs: a light
    /// tank fits where a heavy does; every slot is asked again with the sim's own tests for the unit filmed), with that unit's node names
    /// (its belts, its turn joint, its boarding approach), its shot sound, its own take names and its own proof folder. No subject asked =
    /// the heavy tank, exactly as before. The light tank's menus: OpenZ/Light Tank v2/4 .. 14 (Editor/OzLightV2Setup.cs).
    /// </summary>
    public partial class HeavyV2Films
    {
        /// <summary>The unit the next take films ("light"; none = the heavy tank). Set by the menus beside AutoPref, read once per take.</summary>
        public const string SubjectPref = "oz_v2films_subject";

        /// <summary>One filmed unit: its sim type, its name in the log, the take names' prefix, its proof folder (under the OpenZ folder), its
        /// build menu, its belt renderers (left, right), its turn joint, its boarding approach anchor, its shot sound key, its v2 prefab, its pin.</summary>
        public sealed class FilmSubject
        {
            public string Type, Label, Prefix, ProofDir, BuildMenu, Yaw, Approach, ShotKey;
            public string[] Tracks;
            public System.Func<GameObject> PrefabV2;
            public System.Func<int> Version;
            public System.Action<ZObject, int> Pin;
        }

        static readonly FilmSubject HeavySubject = new FilmSubject
        {
            Type = OzHeavyTank.Type, Label = "heavy tank", Prefix = "hv2", ProofDir = "work/staging/v1_2_20261022/heavy_v2/proof", BuildMenu = "menu OpenZ/Heavy Tank v2/1",
            Yaw = "HeavyTankTurretYaw", Approach = "heavy_Board_Approach", ShotKey = "heavyshot", Tracks = new[] { "HeavyTankTrack_L", "HeavyTankTrack_R" },
            PrefabV2 = () => OzHeavyTank.PrefabV2, Version = () => OzHeavyTank.Version, Pin = OzHeavyTank.Pin,
        };
        static readonly FilmSubject LightSubject = new FilmSubject
        {
            Type = OzLightTank.Type, Label = "light tank", Prefix = "lt2", ProofDir = "work/staging/v1_2_20261022/v2_units/light/proof", BuildMenu = "menu OpenZ/Light Tank v2/1",
            Yaw = "TankTurretYaw", Approach = "tank_Board_Approach", ShotKey = "tankshot", Tracks = new[] { "Tank_track_left", "Tank_track_right" },
            PrefabV2 = () => OzLightTank.Models.PrefabV2, Version = () => OzLightTank.Version, Pin = (o, v) => OzLightTank.Models.Pin(o, v),
        };

        static FilmSubject subject;
        /// <summary>The unit this take films (read from SubjectPref at the take's start, then kept for the take).</summary>
        static FilmSubject S => subject ??= PickSubject();
        static FilmSubject PickSubject()
        {
            string s = PlayerPrefs.GetString(SubjectPref, "");
            if (!string.IsNullOrEmpty(s)) { PlayerPrefs.DeleteKey(SubjectPref); PlayerPrefs.Save(); }
            return s == OzLightTank.Type ? LightSubject : s == OzMediumTank.Type ? MTankSubject : s == OzMlrs.Type ? MlrsSubject : s == OzMech.Type ? MechSubject : s == OzGun.Type ? GunSubject : HeavySubject;   // DV145: + the medium tank (View/HeavyV2Films.MTank.cs); 1.2 v2 units: + the MLRS (View/HeavyV2Films.Mlrs.cs); + the mech (View/HeavyV2Films.Mech.cs); + the gun (View/HeavyV2Films.Gun.cs)
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSubject() => subject = null;
    }
}
#endif
