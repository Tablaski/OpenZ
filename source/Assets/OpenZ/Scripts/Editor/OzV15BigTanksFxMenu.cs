using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit big_tanks_fx: the headless counted checks (the ram duel: Editor/OzV15RamDuelCount.cs; the view's numbers: <see cref="ViewRows"/>)
    /// and the Play check (View/OzBigTanksFxPlayCheck.cs, the Unit editor in Play). Reports go to the kit's proof folder.</summary>
    public static class OzV15BigTanksFxMenu
    {
        static string Proof(string f) => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/big_tanks_fx/proof/" + f));

        [UnityEditor.MenuItem("OpenZ/V1.5/Big Tanks FX: Ram Duel Count (headless, DV185)")]
        static void Count()
        {
            if (UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] Ram Duel count: leave Play mode first (the check sets the simulation's static switches)."); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15RamDuelCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            r += ViewRows();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Proof("count_unity.txt")));
            System.IO.File.WriteAllText(Proof("count_unity.txt"), r);
            UnityEngine.Debug.Log("[OpenZ V1.5] Ram Duel + Big Tanks FX count (" + Proof("count_unity.txt") + ")\n" + r);
        }

        /// <summary>The view's own numbers, counted (no scene): the turret turn rates (SlewFor), the ram clear's constants, the lamp data.</summary>
        public static string ViewRows()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# the big tanks' view numbers, counted (kit big_tanks_fx)");
            var h = UnitAim.SlewFor("heavy", ZKind.Vehicle); var m = UnitAim.SlewFor(OzBigTanks.Mammoth, ZKind.Vehicle);
            var md = UnitAim.SlewFor(ZMTank.Type, ZKind.Vehicle); var b = UnitAim.SlewFor(OzBigTanks.Bizon, ZKind.Vehicle);
            bool mOk = System.Math.Abs(m.yawSpeed - h.yawSpeed * 0.8f) < 1e-3f && System.Math.Abs(m.yawSmooth - h.yawSmooth / 0.8f) < 1e-4f;
            sb.AppendLine(OzV11Kit.Verdict(mOk && h.yawSpeed == 150f && h.yawSmooth == 0.14f, $"the Mammoth's turret = the heavy's x 0.8: {m.yawSpeed:0.###} deg/s (heavy {h.yawSpeed:0.###}), smoothing {m.yawSmooth:0.####} s (heavy {h.yawSmooth:0.####} / 0.8); the heavy unchanged (150 / 0.14)"));
            bool bOk = b.yawSpeed == md.yawSpeed && b.yawSmooth == md.yawSmooth && b.yawSpeed == UnitAim.MediumYawSpeed && b.yawSmooth == UnitAim.MediumYawSmooth;
            sb.AppendLine(OzV11Kit.Verdict(bOk, $"the Bizon's turret = the medium tank's exactly: {b.yawSpeed:0.###} deg/s / {b.yawSmooth:0.####} s (medium tank {md.yawSpeed:0.###} / {md.yawSmooth:0.####})"));
            sb.AppendLine(OzV11Kit.Verdict(UnitAim.RamClearHurry > 1f && UnitAim.RamClearTurnDeg >= 60f && UnitAim.RamClearSeconds > 0.3f && UnitAim.RamClearOn,
                $"the ram clear: on {UnitAim.RamClearOn}, turn {UnitAim.RamClearTurnDeg} deg, hurried x{UnitAim.RamClearHurry}, the heave waits until {UnitAim.RamClearSeconds} s after the start (at most {OzRamBarrels.MaxWaitShare:0.0} of the flip)"));
            int mn = OzBigTanksFx.MammothNeon.Length, bn = OzBigTanksFx.BizonNeon.Length;
            bool neonOk = mn >= 4 && bn >= 4;
            foreach (var n in OzBigTanksFx.MammothNeon) neonOk &= n.z1 - n.z0 >= 0.3f && System.Math.Abs(n.x) > 2.2f;
            foreach (var n in OzBigTanksFx.BizonNeon) neonOk &= n.z1 - n.z0 >= 0.3f && System.Math.Abs(n.x) > 2.2f;
            sb.AppendLine(OzV11Kit.Verdict(neonOk, $"neon runs (measured on the GLBs, kit_tools/place_fx.py): Mammoth {mn}, Bizon {bn}, every run 0.3 m or longer, on the skirts (|x| > 2.2 m)"));
            var pads = new[] { OzBigTanksFx.MammothSearch, OzBigTanksFx.MammothMarkerL, OzBigTanksFx.MammothMarkerR, OzBigTanksFx.MammothExhaustL, OzBigTanksFx.MammothExhaustR,
                               OzBigTanksFx.BizonSearch, OzBigTanksFx.BizonBeacon, OzBigTanksFx.BizonMarkerL, OzBigTanksFx.BizonMarkerR };
            bool padOk = true; foreach (var p in pads) padOk &= p.height > 0.04f && p.height < 0.2f && p.radius > 0.04f && (p.anchor == "turret" || p.anchor == "hull");
            sb.AppendLine(OzV11Kit.Verdict(padOk, $"{pads.Length} measured pads (kit_tools/pick_spots.py), each 4-20 cm high on the turret or the hull"));
            return sb.ToString();
        }

        [UnityEditor.MenuItem("OpenZ/V1.5/Big Tanks FX: Play Check (Unit editor in Play)")]
        static void Play()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] Big Tanks FX Play check: enter Play and open the Unit editor first."); return; }
            OzBigTanksFxPlayCheck.Begin(Proof("play"));
        }
    }
}
