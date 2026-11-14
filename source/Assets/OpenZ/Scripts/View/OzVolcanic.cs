// OpenZ (GPL-3.0) - version 1.3, item 12 part B6 + B7: everything level 7 "Pyro Technics" picks per level, in one place, so no level
// switch of the game falls through to the desert on it. Plain values; view only: no rule reads this file.
using UnityEngine;

namespace OpenZ
{
    /// <summary>The volcanic level's id, labels and colours. Colours come from the Z map picture (lava_biome_L07/L07_z1996.png, medians per
    /// tile class: ground 95 83 71, road 95 75 71, rock 111 99 95) and the owner's lava picture (molten 192 47 17, core 253 160 35),
    /// lifted a little for the small minimap.</summary>
    public static class OzVolcanic
    {
        public const string Level = "L07", Planet = "volcanic", OperationLabel = "L07 · VOLCANO", EndName = "THE VOLCANO";
        public const string BlueStartScene = "L07_start", PortraitBgName = "volcanic";

        public static bool Is(string level) => level == Level;
        /// <summary>The level running now is level 7.</summary>
        public static bool Here => ZGame.I && ZGame.I.level == Level;

        // ---- dust: dark grey-brown ash in the place of the desert's tan
        public static readonly Color DustAsh = new Color(0.40f, 0.36f, 0.33f);
        /// <summary>The dust of every blast, shell and landing: ash on level 7, the given colour on the other levels.</summary>
        public static Color Dust(Color other) => Here ? DustAsh : other;
        /// <summary>The tint OpenZFxPlayer.EmitGolden takes for the dust layer (null = the layer's own colour).</summary>
        public static Color? DustTint => Here ? DustAsh : (Color?)null;
        public static readonly Color32 FallbackDust = new Color32(102, 92, 84, 255);

        // ---- stone chunks: grey in the place of the desert's brown (the hut's own grey)
        public static readonly Color StoneGrey = new Color(0.34f, 0.31f, 0.29f);
        /// <summary>The colour of flying stone: grey on level 7, the given colour on the other levels.</summary>
        public static Color Stone(Color other) => Here ? StoneGrey : other;

        // ---- the small maps (OpenZMinimap.Palette, OzLevelPreview.Bake)
        public static readonly Color32 MiniGround = new Color32(98, 84, 72, 255), MiniRoad = new Color32(126, 114, 102, 255),
            MiniCliff = new Color32(118, 106, 100, 255), MiniCliffDark = new Color32(88, 78, 74, 255),
            MiniLavaCrust = new Color32(216, 84, 22, 255), MiniLavaHot = new Color32(252, 156, 40, 255);
        /// <summary>A lava tile on the small maps (kind 2 = Z's animated hot pools).</summary>
        public static Color32 MiniLava(int kind) => kind >= 2 ? MiniLavaHot : MiniLavaCrust;

        /// <summary>The portrait backdrop's name for a level: the desert's on level 2 and 16 as today, the city's on level 17; level 7 its
        /// own when the kit has it, else the city's grey, never the desert's sand.</summary>
        public static string PortraitBg(string level)
        {
            if (level == "L17") return "city";
            if (level != Level) return "desert";
            return OzKit.GetSprite("Portrait/portrait_bg_" + PortraitBgName) != null ? PortraitBgName : "city";
        }

        /// <summary>The blue side's briefing scene of a level: level 7's own lava walk once its data and voice are in, else the shared one.</summary>
        public static string BlueBriefingScene(string level) =>
            Is(level) && OzBlueScenes.Shared.Ready(BlueStartScene, out _) ? BlueStartScene : OzBlueScenes.Briefing;
    }
}
