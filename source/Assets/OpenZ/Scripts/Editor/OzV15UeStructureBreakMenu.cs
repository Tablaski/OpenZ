using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.5 ue_structure_destruction: the counted Play check of the Unit editor's tower / platform collapse and the obstacle's shatter
    /// (View/OzUeStructureBreakCheck.cs; its report and dense frames go to the kit's proof/play folder unless OzUeStructureBreakCheck.Folder is set first).</summary>
    static class OzV15UeStructureBreakMenu
    {
        [MenuItem("OpenZ/V1.5/Unit Editor: Structure Destruction Check (Play, the unit editor open)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ StructureBreakCheck] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(OzUeStructureBreakCheck.Folder))
                OzUeStructureBreakCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/ue_structure_destruction/proof/play"));
            OzUeStructureBreakCheck.Begin();
        }

        [MenuItem("OpenZ/V1.5/Unit Editor: Structure Destruction Check, last report")]
        static void Last() => Debug.Log(OzUeStructureBreakCheck.Running ? "[OpenZ StructureBreakCheck] running" : OzUeStructureBreakCheck.Report);

        [MenuItem("OpenZ/V1.5/Unit Editor: Structure Destruction, numbers")]
        static void Numbers() => Debug.Log($"[OpenZ StructureBreak] on {OzUeStructureBreak.On}; collapses {OzUeStructureBreak.Collapses} (old path {OzUeStructureBreak.OldPath}), pieces {OzUeStructureBreak.PiecesMade} "
            + $"(slabs {OzUeStructureBreak.SlabsMade}, panels {OzUeStructureBreak.WallPanelsMade}, kerb {OzUeStructureBreak.KerbsMade}, chunks {OzUeStructureBreak.ChunksMade}, crumbs {OzUeStructureBreak.CrumbsMade}), "
            + $"alive {OzUeStructureBreak.LivePieces}, landings {OzUeStructureBreak.Landings}, knocks {OzUeStructureBreak.Knocks}, kicks {OzUeStructureBreak.Kicks}, fog puffs {OzUeStructureBreak.FogPuffs}, "
            + $"sunk {OzUeStructureBreak.Sunk}, swept {OzUeStructureBreak.Swept}, cleared {OzUeStructureBreak.Cleared}; slowest build {OzUeStructureBreak.MaxBuildMs:0.0} ms; "
            + $"holo shatters {OzUeStructureBreak.HoloShatters} ({OzUeStructureBreak.HoloPanels} panels, {OzUeStructureBreak.HoloTinks} tinks); flag falls {FlagView.DeckFalls} (landed {FlagView.DeckLandings})");
    }
}
