// OpenZ (GPL-3.0) - version 1.5 kit bridge_death_exp_r3: the film helpers of the bridge death explosions (View/OzBridgeDeathExp.cs). Play only.
//  - OpenZ/V1.5/Bridge Death/List bridges (Play): every bridge of the loaded level, its kind, length and the explosion it gets.
//  - OpenZ/V1.5/Bridge Death/Blow bridge nearest the view (Play): the bridge nearest the middle of the game camera's picture dies by the
//    rules' own death (as the restore kit's film helper does); a bridge that is down already gets its automatic repair 3 s from now (run
//    the menu again once it stands).
//  - OpenZ/V1.5/Bridge Death/Report (Play): the counts of this kit and of the in-game explosions.
// Kit long_bridge_blast_vary_r6 (a long bridge's charges each get their own turn, View/OzBridgeBlastVary.cs):
//  - OpenZ/V1.5/Bridge Death/Vary check (counted, no Play needed): 16000 deaths drawn in numbers only, one PASS / FAIL line.
//  - OpenZ/V1.5/Bridge Death/Vary on - off (this session only, for before / after frames): off = every charge the same, as before the kit.
#if UNITY_EDITOR
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV15BridgeDeathExpMenu
    {
        const string Tag = "[OpenZ][bridge_death_exp_r3]";

        static ZGame Game()
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " Play only"); return null; }
            var g = ZGame.I; if (!g || g.World == null) { Debug.LogWarning(Tag + " no level running"); return null; }
            return g;
        }

        [MenuItem("OpenZ/V1.5/Bridge Death/List bridges (Play)")]
        static void List()
        {
            var g = Game(); if (!g) return;
            var sb = new StringBuilder(Tag + " level " + g.level + " bridges:");
            foreach (var o in g.World.Objects)
            {
                if (o == null || !o.IsBridge) continue;
                int t = OzBridgeDeathExp.Tiles(o);
                sb.Append($"\n  #{o.Id} {o.Type} {OzBridgeKit.Biome(g.level)} tile ({o.LocX / 16},{o.LocY / 16}) {t} tiles = {t * OzBridgeDeathExp.TileM:0} m -> {(OzBridgeDeathExp.IsLong(o) ? "LONG: the briefing's charges" : "SHORT: exp_v2_3 CHAIN")}{(o.Destroyed ? " (down now)" : "")}");
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("OpenZ/V1.5/Bridge Death/Blow bridge nearest the view (Play)")]
        static void Blow()
        {
            var g = Game(); if (!g) return;
            var cam = Camera.main; if (!cam) { Debug.LogWarning(Tag + " no game camera"); return; }
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 look = cam.transform.position;
            if (Mathf.Abs(ray.direction.y) > 1e-4f) look = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
            ZObject best = null; float bd = float.MaxValue;
            foreach (var o in g.World.Objects)
            {
                if (o == null || !o.IsBridge) continue;
                var c = g.CenterWorld(o); float d = (c.x - look.x) * (c.x - look.x) + (c.z - look.z) * (c.z - look.z);
                if (d < bd) { bd = d; best = o; }
            }
            if (best == null) { Debug.LogWarning(Tag + " no bridge in this level"); return; }
            if (best.Destroyed)
            {
                best.DoAutoRepair = true; best.NextAutoRepairTime = g.World.Time + 3.0;
                Debug.Log($"{Tag} bridge #{best.Id} is down: its automatic repair in 3 s; run the menu again once it stands");
                return;
            }
            best.DamageHealth(best.Health + 1); g.World.UpdateObjectHealth(best, -1);
            Debug.Log($"{Tag} bridge #{best.Id} {best.Type} ({OzBridgeDeathExp.Tiles(best)} tiles, {(OzBridgeDeathExp.IsLong(best) ? "long" : "short")}) destroyed by the rules' death");
        }

        [MenuItem("OpenZ/V1.5/Bridge Death/Vary check (counted, no Play needed)")]
        static void VaryCheck() => Debug.Log(OzBridgeBlastVary.Check());

        [MenuItem("OpenZ/V1.5/Bridge Death/Vary on - off (this session only, for before - after frames)")]
        static void VaryToggle()
        {
            OzBridgeBlastVary.On = !OzBridgeBlastVary.On;
            Debug.Log($"{Tag} long_bridge_blast_vary_r6: each charge its own turn {(OzBridgeBlastVary.On ? "ON (the game's state)" : "OFF (every charge the same, as before the kit; this session only)")}");
        }

        [MenuItem("OpenZ/V1.5/Bridge Death/Report (Play)")]
        static void Report()
        {
            Debug.Log(OzBridgeDeathExp.Report() + "\n" + OzExpInGame.Report() + "\n" + OzBridgeDeathExp.LastLine);
        }
    }
}
#endif
