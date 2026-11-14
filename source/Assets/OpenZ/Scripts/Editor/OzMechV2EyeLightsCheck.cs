#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The mech v2's eye bloom (View/OzMechV2EyeGlow.cs) and its night eye beams, the editor side:
    ///  - BeamsAsV1 / Note: the parity check's lamp line (Editor/OzMechV2bSetup.cs menu 2) accepts the v2's lamp entry whose headlight is the
    ///    jeep's strength (the real spot's intensity, the haze cone's length and dip, the ground pool's width) instead of v1's: the four values
    ///    are put back to v1's before the eye-socket comparison, and only when they are exactly this kit's numbers;
    ///  - menu 27 (Play): every v2 mech's eye glow and eye beams now, beside every jeep's headlights -> the proof folder's eyes_glow_beams.txt.
    /// </summary>
    public static class OzMechV2EyeLightsCheck
    {
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/mech/proof"));

        /// <summary>The v2's eye beams in vehicle_lights.json "medium@v2" (v1 'medium': 18, 5, 7, 4.07; the jeep: 26, 6, 7, 4.5).</summary>
        public const float Intensity = 26f, BeamLength = 6.5f, PitchBeam = 14f, PoolWidth = 4.5f;
        static readonly string[] Keys = { "intensity", "beamLength", "pitchBeam", "width" };
        static readonly float[] Values = { Intensity, BeamLength, PitchBeam, PoolWidth };

        static Match Find(string entry, string key) => Regex.Match(entry, "\"" + key + "\": (-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)");

        /// <summary>True when the entry's headlight has exactly this kit's four beam values.</summary>
        public static bool HasBeams(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return false;
            for (int i = 0; i < Keys.Length; i++)
            {
                var m = Find(entry, Keys[i]);
                if (!m.Success || m.NextMatch().Success) return false;                                // each key once (the headlight / its pool)
                if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) || Mathf.Abs(f - Values[i]) > 1e-4f) return false;
            }
            return true;
        }

        /// <summary>The v2 entry with the four beam values put back to v1's (text only, nothing else touched); unchanged when it does not
        /// carry this kit's beams (then the comparison fails as before).</summary>
        public static string BeamsAsV1(string v1Entry, string v2Entry)
        {
            if (string.IsNullOrEmpty(v1Entry) || !HasBeams(v2Entry)) return v2Entry;
            string s = v2Entry;
            foreach (var k in Keys)
            {
                var a = Find(v1Entry, k); var b = Find(s, k);
                if (!a.Success || a.NextMatch().Success || !b.Success) return v2Entry;
                var g = b.Groups[1];
                s = s.Substring(0, g.Index) + a.Groups[1].Value + s.Substring(g.Index + g.Length);
            }
            return s;
        }

        /// <summary>The lamp line's extra words when the v2 carries this kit's beams.</summary>
        public static string Note(string v2Entry) => HasBeams(v2Entry)
            ? $"; the eye beams the jeep's strength (spot {Intensity:0}, cone {BeamLength:0.0} m dipped {PitchBeam:0} deg, pool {PoolWidth:0.0} m wide)"
            : "";

        [MenuItem("OpenZ/Mech v2/27 Eye glow and eye beams status (Play: day / night, beside the jeeps)", false, 81)]
        static void Status()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[OzMechV2EyeGlow] enter Play first (a level with mech v2s and a jeep)"); return; }
            var sb = new StringBuilder();
            sb.Append(OzMechV2EyeGlow.I ? OzMechV2EyeGlow.I.Status() : "[OpenZ] OzMechV2EyeGlow: not running in this scene");
            var vl = VehicleLights.I; var game = ZGame.I;
            sb.Append($"\n[OpenZ] eye beams / headlights (VehicleLights): night {NightGrade.NightK:F2}, t {NightGrade.TimeOfDay:F3}{(vl ? $", real lights lit {vl.RealLightsLit}" : ", NO VehicleLights")}");
            if (vl && game)
                foreach (var kv in game.Views)
                {
                    var v = kv.Value;
                    if (!v || v.Obj == null || (v.Obj.Type != OzMech.Type && v.Obj.Type != "jeep")) continue;
                    var p = vl.Probe(v.Obj.Id);
                    if (!p.Ok) continue;
                    bool v2 = OzV2Units.IsV2(v);
                    string who = v.Obj.Type == "jeep" ? "JEEP" : v2 ? "MECH v2" : "mech v1";
                    sb.Append($"\n  {who} #{v.Obj.Id} ({p.Type}) {(p.Crewed ? "crewed " + CaptureHold.Shown(v.Obj) : "EMPTY")}: lamp power {p.Power:F2}, headlights {p.HeadNorm:F2}, " +
                              $"real spot {p.SpotIntensity:F1} (range {p.SpotRange:F0} m), lamps lit {p.Share:F2}{(p.Split ? $" (one lamp can fail: side {(p.Plan.side > 0 ? "+x" : "-x")}, level {p.FaultK:F2}{(p.Dead ? ", OUT" : "")})" : "")}");
                }
            Directory.CreateDirectory(ProofDir);
            File.AppendAllText(Path.Combine(ProofDir, "eyes_glow_beams.txt"), $"--- t {Time.time:F1} s\n{sb}\n");
            Debug.Log(sb.ToString());
        }
    }
}
#endif
