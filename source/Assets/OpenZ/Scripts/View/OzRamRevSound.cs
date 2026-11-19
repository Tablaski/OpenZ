using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <para>The first frame a Mammoth or a Bizon is in the rules' ram state (ZObject.Ramming: the ram's waypoint put in front, the boost
    /// building up from its start speed, Sim/ZObject.MammothRam.cs; the Bizon's crush is the same state), its engine revs up: one of three takes
    /// (<see cref="Takes"/>: Resources/OpenZ/Audio/Sfx/v15_ram_revup_1..3, made by the kit's tools/make_revs.py from the game's own heavy diesel
    /// and tank engine sounds: a variable-speed read whose pitch and firing rate climb over 1.25-1.45 s like the ram's speed ramp, a darker
    /// start opening up, a +5 dB crescendo, a cosine fade out), picked at random with no take twice in a row (the sound table's noRepeat bag),
    /// at the tank's hull. Only on screen: the rows are inViewOnly (AudioDirector.Play's view test, Zod's PlayWavRestricted margins), so a ram
    /// off screen is silent. Mixed under the impact: -14.5 LUFS takes at volDb -3 on the vehicles bus (the impact: -13.3 LUFS at 0 dB on the
    /// destruction bus).</para>
    /// <para>The hook is the state, not the rules' START event (it works however the rework signals the start): UnitView.Sync watches
    /// ZObject.Ramming per tank (one line in View/UnitView.cs, with R21's). While <see cref="On"/>, the older flat rev the START event fires
    /// (veh_mammoth_ram_rev / veh_bizon_crush_rev, played on and off screen) is silent (AudioDirector's Silent set), so one rev is heard.</para>
    /// Switch <see cref="On"/> (off = the older START rev as before, no new rev).
    /// </summary>
    public static class OzRamRevSound
    {
        /// <summary>The switch.</summary>
        public static bool On = true;
        /// <summary>The sound table rows: the Mammoth's ram, the Bizon's crush (the same three takes, the Bizon's a little deeper).</summary>
        public const string RamRow = "veh_ram_revup", CrushRow = "veh_crush_revup";
        /// <summary>The older rows the rules' START event fires (silent while On).</summary>
        public static readonly string[] Legacy = { "veh_mammoth_ram_rev", "veh_bizon_crush_rev" };
        /// <summary>The takes (Resources paths).</summary>
        public static readonly string[] Takes = { "OpenZ/Audio/Sfx/v15_ram_revup_1", "OpenZ/Audio/Sfx/v15_ram_revup_2", "OpenZ/Audio/Sfx/v15_ram_revup_3" };
        /// <summary>Statistics (the Play check): ram / crush starts seen, rev-ups asked for (AudioDirector plays them on screen only).</summary>
        public static int Starts, Asked;
        public static void ResetStats() { Starts = Asked = 0; }

        /// <summary>A ram / crush just started (the first frame its state is seen): the rev-up at the hull.</summary>
        internal static void Started(ZObject tank, Vector3 at)
        {
            Starts++;
            if (!On || tank == null) return;
            Asked++;
            AudioDirector.Fire(tank.Type == ZBigTanks.Bizon ? CrushRow : RamRow, at, tank.Id);
        }
    }

    public partial class UnitView
    {
        bool rrWas;

        /// <summary>UnitView.Sync (with R21's line in View/UnitView.cs): a Mammoth's / Bizon's ram state, edge by edge.</summary>
        void RamRevWatch()
        {
            if (Obj == null || Obj.Kind != ZKind.Vehicle || !ZBigTanks.Is(Obj.Type)) return;
            AudioDirector.RamRevLegacyQuiet(OzRamRevSound.On);
            bool r = Obj.Ramming;
            if (r && !rrWas) OzRamRevSound.Started(Obj, transform.position);
            rrWas = r;
        }
    }

    public partial class AudioDirector
    {
        static bool ramRevQuiet;

        /// <summary>R18: the older START revs silent while OzRamRevSound.On (put back when it is switched off). Changes the set only when the
        /// switch changed (called every frame by every big tank, before any ram can start: their clips were loaded at the boot).</summary>
        public static void RamRevLegacyQuiet(bool quiet)
        {
            if (quiet == ramRevQuiet) return;
            ramRevQuiet = quiet;
            foreach (var id in OzRamRevSound.Legacy) { if (quiet) Silent.Add(id); else Silent.Remove(id); }
        }
    }
}
