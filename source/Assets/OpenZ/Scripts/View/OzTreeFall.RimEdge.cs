// OpenZ (GPL-3.0) - Level 16, round 5e (View/OzL16Outskirts.RimEdge.cs): an edge plant on the map's cliff tops and rims drawn by a
// stand-in model (its child) falls as that model: its fall's height and crown (the lean's leaves, the fall's hinge and landing, the blast's
// and the big pieces' reach) are the stand-in's, its stump is cut from the stand-in's mesh (OzTreeFall.CutStump: EdgeModelOf). The rules'
// side of the tree (its tile, its hit points, where a shot or a click finds it) is not touched; nor is the box its far run is folded by
// (the run is the plant's own far shape). The fort's blast in an ending throws the plants round 5e stands outside the map as it threw
// the old ring trees there (AddBlastFlora, BlastOutside). Drawn only: no rule reads any of it.
using UnityEngine;

namespace OpenZ
{
    public partial class OzTreeFall
    {
        /// <summary>The fall's size of a plant from the renderer that draws it now (model: its stand-in; null: the plant's own shape):
        /// height from its foot to the shape's top, crown radius the shape's half width (its mesh's own box, not the wind's drawing box).</summary>
        internal static void EdgeResize(Transform plant, Renderer model)
        {
            if (!I || !plant) return;
            var r = model ? model : plant.GetComponent<Renderer>(); var mf = r ? r.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh) return;
            var b = mf.sharedMesh.bounds; var w = r.transform.localToWorldMatrix;
            var box = new Bounds(w.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int k = 0; k < 8; k++) box.Encapsulate(w.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f))));
            foreach (var t in I.trees)
            {
                if (t.main != plant || t.fallen || !t.holder) continue;
                var foot = t.holder.position;
                t.height = Mathf.Max(1f, box.max.y - foot.y); t.radius = Mathf.Max(0.5f, Mathf.Max(box.extents.x, box.extents.z));
            }
            for (int i = 0; i < I.floraTrees.Count; i++)
            {
                var f = I.floraTrees[i]; if (f.plant != plant) continue;
                f.height = Mathf.Max(1f, box.max.y - f.foot.y); f.radius = Mathf.Max(0.5f, Mathf.Max(box.extents.x, box.extents.z));
                I.floraTrees[i] = f;
            }
        }

        /// <summary>Round 5e: the fort's blast throws the plants outside the map too, as it threw the rim ring's own trees before round 5e
        /// put them away (OzTreeFall.Blast.cs: the level's other trees, from BlastFloraMinH up). OzL16Outskirts gives each of its new plants
        /// here when the land is shown (RimFixApply); its size is its mesh's own box (the wind widens Renderer.bounds). True = taken.</summary>
        internal static bool AddBlastFlora(Transform plant)
        {
            if (!I || !plant || !plant.GetComponent<MeshRenderer>()) return false;
            var mf = plant.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return false;
            var b = mf.sharedMesh.bounds; var w = plant.localToWorldMatrix;
            var box = new Bounds(w.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int k = 0; k < 8; k++) box.Encapsulate(w.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f))));
            var foot = plant.position;
            if (foot.y - box.min.y > 1f) foot.y = box.min.y;                                        // as CollectBlastFlora reads a plant's foot
            float h = box.max.y - foot.y; if (h < BlastFloraMinH) return false;
            I.floraTrees.Add(new FloraTree { plant = plant, foot = foot, height = h, radius = Mathf.Max(0.5f, Mathf.Max(box.extents.x, box.extents.z)) });
            return true;
        }

        /// <summary>OzTreeFall.Blast.cs: a holder the plants outside the map stand in (OzL16Outskirts.RimFix.cs NewPlant) counts as their
        /// cell, as a tree cell does for the level's own trees (a plant the blast has taken stands in its own holder: passed over).</summary>
        static bool BlastOutside(Transform holder) => holder && (holder.name == "OUTER_UNDER" || holder.name == "OUTER_UNDER_LOW" || holder.name == "OUTER_GROVE");
    }
}
