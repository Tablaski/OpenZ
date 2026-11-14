using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 items 143 / 146 (the heavy v2's tread did not move in the game): VehicleMotion has always written the tread scroll (_BaseMap_ST)
    /// into the track renderer's WHOLE-renderer property block, but TeamPaint gives every renderer of a unit, the tracks too, its own property
    /// block per material slot, and a slot's own block is the one its draw uses. So the belt texture never moved on screen, in the built game
    /// and in the editor alike (the earlier proofs read the whole-renderer block back, never the picture). Now the belts whose material is
    /// made to scroll (<see cref="ScrollMaterials"/>: the heavy v2's belt material) carry the scroll in each slot's own block too, keeping
    /// TeamPaint's values there (as VehicleLights, OzShuttleSelfShadow and OzDeadTint write theirs). Every other vehicle is drawn as before:
    /// their track faces sit in shared texture sheets that must not slide. Nothing here is editor-only: the same code runs in the built game.
    /// Switch: <see cref="SlotScroll"/> (false = as before: no motion drawn). A check that runs in a built development player (and in the
    /// editor's Play): View/OzTreadProbe.cs.
    /// </summary>
    public partial class VehicleMotion
    {
        /// <summary>true: the belts of <see cref="ScrollMaterials"/> roll on screen (the scroll in each material slot's own block); false: as before.</summary>
        public static bool SlotScroll = true;
        /// <summary>The belt materials whose texture is made to scroll (their own belt pictures, not a shared sheet). A new tracked model
        /// with its own belt material adds its name here.</summary>
        public static readonly HashSet<string> ScrollMaterials = new HashSet<string> { "heavy_tank_v2_track", OzLightTank.BeltMaterial, OzMediumTank.BeltMaterial, OzMlrs.BeltMaterial };   // 1.2 v2 units: the light tank v2's belts too; DV145: the medium tank's belts roll in the built game too; 1.2 v2 units: the MLRS v2's belts too
        static readonly int SlotStId = Shader.PropertyToID("_BaseMap_ST");
        static MaterialPropertyBlock slotMpb;
        readonly Dictionary<Renderer, int> slotCounts = new Dictionary<Renderer, int>();   // material slots to write (0 = not a scrolling belt)

        /// <summary>Step's track loop: the scroll it just put in the whole-renderer block, also into every slot's own block (scrolling belts only).</summary>
        void ScrollSlots(Renderer r, Vector4 st)
        {
            if (!SlotScroll || !r) return;
            if (!slotCounts.TryGetValue(r, out int n)) { n = ScrollsBelt(r) ? r.sharedMaterials.Length : 0; slotCounts[r] = n; }
            if (n > 0) WriteSlots(r, st, n);
        }

        /// <summary>True when one of the renderer's materials is a scrolling belt material (an instance's " (Instance)" name counts too).</summary>
        public static bool ScrollsBelt(Renderer r)
        {
            if (!r) return false;
            foreach (var m in r.sharedMaterials)
            {
                if (!m) continue;
                string n = m.name; int k = n.IndexOf(" (Instance)", System.StringComparison.Ordinal);
                if (ScrollMaterials.Contains(k >= 0 ? n.Substring(0, k) : n)) return true;
            }
            return false;
        }

        /// <summary>Writes the scroll into the first n material slots' own blocks of r, keeping whatever else is in them.</summary>
        public static void WriteSlots(Renderer r, Vector4 st, int n)
        {
            if (!r) return;
            slotMpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < n; i++)
            {
                r.GetPropertyBlock(slotMpb, i);
                slotMpb.SetVector(SlotStId, st);
                r.SetPropertyBlock(slotMpb, i);
            }
        }

        /// <summary>The V scroll the draw of material slot 0 of r uses (its own block when it has one, else the whole-renderer block); NaN = none.</summary>
        public static float DrawnScroll(Renderer r)
        {
            if (!r) return float.NaN;
            slotMpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(slotMpb, 0);
            if (!slotMpb.isEmpty) return slotMpb.HasVector(SlotStId) ? slotMpb.GetVector(SlotStId).w : float.NaN;
            r.GetPropertyBlock(slotMpb);
            return slotMpb.HasVector(SlotStId) ? slotMpb.GetVector(SlotStId).w : float.NaN;
        }

        /// <summary>The track renderers this component scrolls (the probe and the films).</summary>
        public void TrackRenderers(List<Renderer> into) { into.Clear(); foreach (var (r, _) in tracks) if (r) into.Add(r); }
    }
}
