using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 2 (View/OzUePlayground.cs DropCore, View/OzUeNuke.cs): the NUKE extra's drop. Dragged or click-placed like the flag and
    /// the crate, it places nothing: it calls the levels' ending bomber onto the floor point (OzUeNuke.Call). Refused while the last nuke's
    /// 30 s run (the toast "NUKE READY IN 12 S", ui_error). Accepted: ui_confirm and the toast "NUKE INBOUND"; the strike plays its own
    /// sounds (the launch alarm, the engine, the fly-by, the release, the whistle, the blast).
    /// </summary>
    public static partial class OzUePlayground
    {
        static UeDrop NukeDrop(ZGame g, ZWorld w, Vector3 centre, bool sound, UeDrop d)
        {
            if (!OzUeNuke.Call(g, w, centre, out string why))
            {
                d.why = why; Refused++;
                if (sound) AudioDirector.UI("ui_error");
                Debug.Log($"{OzUnitEditor.Tag} NUKE refused: {why}");
                return d;
            }
            d.ok = true; d.groups = 0; d.why = OzUeNuke.InboundWords;
            if (sound) AudioDirector.UI("ui_confirm");
            Debug.Log($"{OzUnitEditor.Tag} NUKE called on ({centre.x:0.0}, {centre.z:0.0}), {OzUeNuke.KillRadiusM:0} m, next one in {OzUeNuke.CooldownSec:0} s");
            return d;
        }
    }
}
