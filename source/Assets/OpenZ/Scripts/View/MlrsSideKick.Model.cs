using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (the MLRS v2, owner item 135; lesson 6: the launcher kicks on every shot): the
    /// launcher kick of View/MlrsSideKick.cs for the model drawn.
    ///  * Sizes: v1 keeps its own (back 0.22 m, up 5 deg, roll 6 deg). The v2 box is hinged at its rear-top edge 7 cm in front of the red
    ///    team block and is 2.7 m wide, so v2 kicks without the slide back and with a 2.5 deg roll (OzMlrs.KickBackV2 / KickRollV2; the tip-up
    ///    about the hinge as v1): measured clear of the red block, the hinge brackets, the antennas and the hull (v2_units/mlrs/V1_PARITY.md 3).
    ///  * The aim sees the launcher at rest: on v2 the muzzles (and the rockets) sit under MLRS_rack, the node this kick moves, so UnitAim (62)
    ///    would read last frame's kick on its muzzles and its solve would turn the kick back out (the box would barely move). When the kicked
    ///    node carries muzzles, it goes back to its rest in Update (after every Update that fires a rocket from the drawn tube: MlrsSalvo 55,
    ///    CombatView 50), UnitAim solves the aim on the launcher at rest in LateUpdate (62), and this kick goes on top again (66) before the
    ///    frame is drawn. v1's muzzles are not under MLRS_rack: v1 is drawn exactly as before.
    /// </summary>
    public partial class MlrsSideKick
    {
        float backM = BackM, rollDeg = RollDeg; bool sized, restBeforeAim;

        /// <summary>Fire, once the kicked node is found: the model's sizes, and whether the kicked node carries muzzles.</summary>
        void ModelSizes()
        {
            if (sized || !rack) return;
            sized = true;
            if (OzMlrs.BoxModel(transform)) { backM = OzMlrs.KickBackV2; rollDeg = OzMlrs.KickRollV2; }   // the first v2 model's box only; the v2 on v1's model kicks as v1
            foreach (var t in rack.GetComponentsInChildren<Transform>(true))
                if (t != rack && t.name.Contains("Muzzle")) { restBeforeAim = true; break; }
        }

        /// <summary>The launcher back at its rest before UnitAim's solve (only where the kicked node carries muzzles).</summary>
        void Update()
        {
            if (!restBeforeAim || !wrote || !rack) return;
            rack.localPosition = restPos; rack.localRotation = restRot;
        }

        /// <summary>Proof: the kick's sizes this launcher uses (m, deg) and whether it puts the launcher at rest before the aim.</summary>
        public float KickBackNow => backM;
        public float KickRollNow => rollDeg;
        public bool RestBeforeAim => restBeforeAim;
        /// <summary>Proof: the kick drawn now (the back / up spring and the roll spring, both clamped as LateUpdate draws them).</summary>
        public float KickNow => Mathf.Clamp(kx, -0.3f, 1.2f);
        public float RollNow => Mathf.Clamp(rx, -1.2f, 1.2f);
    }
}
