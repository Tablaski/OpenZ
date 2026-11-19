// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": the round 4 rows of the menu OpenZ/Checks/V1.5/L18 briefings data (no Play)
// (kit level18_briefings/kit_r4). Data only: what Play must show is in the kit README's Unity steps.
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    public static class OzL18BriefingCheckR4
    {
        public static void Rows(System.Action<string, bool> C)
        {
            // 29: the owner's march (installed as Resources/OpenZ/Audio/Sfx/brief_l18_army_march_a / _b)
            foreach (var n in new[] { OzL18BriefingStage.OwnerMarchA, OzL18BriefingStage.OwnerMarchB })
            {
                var clip = Resources.Load<AudioClip>(OzL18BriefingStage.SfxRes + n);
                C("round 4: the owner's march Resources/" + OzL18BriefingStage.SfxRes + n + " (7.4 s, played once)" + (clip ? " " + clip.length.ToString("F2") + " s" : " MISSING"), clip && Mathf.Abs(clip.length - 7.4f) < 0.1f);
            }
            C("round 4: the owner's march is on and B starts " + OzL18BriefingStage.OwnerMarchBAfter.ToString("F1") + " s after A (1.0 wanted)", OzL18BriefingStage.OwnerMarch && Mathf.Abs(OzL18BriefingStage.OwnerMarchBAfter - 1f) < 0.01f);
            // 31: the signs on the back board
            C("round 4: the two signs stand on the back board behind the buttons, no posts (lift " + OzEvilStage.SignLift.ToString("F2") + " m, lean " + OzEvilStage.SignLean.ToString("F0") + " deg, board z " + OzEvilStage.BoardZ.ToString("F3") + ")",
              OzEvilStage.SignsOnBoard && OzEvilStage.BoardZ < -0.15f && OzEvilStage.SignLean >= 15f && OzEvilStage.SignLean <= 20f && OzEvilStage.SignLift > 0.12f);
            // 33 / 38: the clips the tail and the firefight play (also in WarmClips: the row above counts them)
            foreach (var n in new[] { "explosion-v1-20260909", "explosion-v3-20260909", "v3-tank-impact-b", "v3-rifle-single-a", "v3-rifle-single-b", "v3-rifle-single-c", "v3-tank-shot-b", "sd_nuke_rumble_tail", "sd_debris_rain_b", "sd_structure_groan_b" })
                C("round 4: the clip Resources/" + OzL18BriefingStage.SfxRes + n, Resources.Load<AudioClip>(OzL18BriefingStage.SfxRes + n));
            // 42: the red watch post with the sandbag wall (the scene file names it)
            var ta = Resources.Load<TextAsset>(OzL18WatchSet.DataPath);
            string pres = null;
            if (ta) { var d = JsonUtility.FromJson<OzWatchFile>(ta.text); if (d != null) pres = d.parapetRes; }
            C("round 4: the red watch post with the sandbag wall Resources/" + (pres ?? "?") + " (L18_WatchPost_r4 wanted)", pres != null && pres.EndsWith("L18_WatchPost_r4") && Resources.Load<GameObject>(pres));
            // 7C: the parade's pace (the heads on their decks in the first frame)
            C("round 4: the parade's pace " + OzL18BriefingStage.ConvoySpeed.ToString("F2") + " m/s (1.8 wanted: every head on its deck in the first frame that shows the bridges)", Mathf.Abs(OzL18BriefingStage.ConvoySpeed - 1.8f) < 0.01f);
            // 40: the flare's data type is in the render pipeline
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            C("round 4: the render pipeline draws data-driven lens flares (the sun flare of owner addition 40)" + (rp ? " (" + rp.name + ")" : " (no URP asset)"), rp && rp.supportDataDrivenLensFlare);
        }
    }
}
