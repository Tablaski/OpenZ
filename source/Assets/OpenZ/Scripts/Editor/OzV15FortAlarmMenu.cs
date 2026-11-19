using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit self_destruct_v2: the Play check of the fort wall alarm beacon (View/OzFortSelfDestructAlarmCheck.cs) and the headless count of the
    /// bigger panel's and the beacon's numbers (no scene). Reports go to the kit's proof folder.</summary>
    public static class OzV15FortAlarmMenu
    {
        static string Proof => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/self_destruct_v2/proof"));

        [UnityEditor.MenuItem("OpenZ/V1.5/Fort: Wall Alarm Check (Play, a level playing)")]
        static void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] fort wall alarm check: start a level in Play first."); return; }
            OzFortSelfDestructAlarmCheck.Folder = Proof;
            OzFortSelfDestructAlarmCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.5/Fort: Wall Alarm Count (headless)")]
        static void Count()
        {
            var sb = new System.Text.StringBuilder("# the bigger fort wall panel and its alarm beacon, counted (kit self_destruct_v2)\n");
            float area = 0.44f * 0.83f, old = 0.40f * 0.52f;
            sb.AppendLine(OzV11Kit.Verdict(System.Math.Abs(area / old - 1.8f) < 0.1f, $"the panel 0.44 x 0.83 m = {area / old:0.00} x the old 0.40 x 0.52 m (want about 1.8)"));
            sb.AppendLine(OzV11Kit.Verdict(OzFortSelfDestructPanels.CentreUp > 1.2f && OzFortSelfDestructPanels.CentreUp + OzFortSelfDestructAlarm.DomeBaseY + OzFortSelfDestructAlarm.DomeH < 2.098f && OzFortSelfDestructPanels.CentreUp - 0.415f >= 1.10f,
                $"the panel's centre {OzFortSelfDestructPanels.CentreUp:0.000} m over the hall floor; the beacon's top {OzFortSelfDestructPanels.CentreUp + OzFortSelfDestructAlarm.DomeBaseY + OzFortSelfDestructAlarm.DomeH:0.000} m, under the strip light (2.098 m)"));
            sb.AppendLine(OzV11Kit.Verdict(OzFortSelfDestructAlarm.SpinUrgent < OzFortSelfDestructAlarm.SpinCalm && OzFortSelfDestructAlarm.UrgentShare == 0.25f && OzFortSelfDestructAlarm.HumNearM < 30f,
                $"the beacon turns every {OzFortSelfDestructAlarm.SpinCalm} s, {OzFortSelfDestructAlarm.SpinUrgent} s under {OzFortSelfDestructAlarm.UrgentShare * 100f:0} % health; the hum within {OzFortSelfDestructAlarm.HumNearM} m"));
            bool ev = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "OpenZ/Resources/OpenZ/Audio/openz_sound_events.json")).Contains("\"id\": \"" + OzFortSelfDestructAlarm.HumEvent + "\"");
            sb.AppendLine(OzV11Kit.Verdict(ev && Resources.Load<AudioClip>("OpenZ/Audio/Sfx/v15_selfdestruct_hum"), $"the hum event {OzFortSelfDestructAlarm.HumEvent} and its clip v15_selfdestruct_hum are there"));
            System.IO.Directory.CreateDirectory(Proof);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Proof, "count_unity.txt"), sb.ToString());
            Debug.Log("[OpenZ V1.5] fort wall alarm count\n" + sb);
        }
    }
}
