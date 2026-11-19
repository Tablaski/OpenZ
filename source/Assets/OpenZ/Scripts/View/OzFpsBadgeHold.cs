// OpenZ (GPL-3.0) - version 1.5 kit fps_shuttle_badges: the first-person view (View/OzFpsView.cs) really holds the viewed robot's own rank
// chevrons (and every other part of it the GPU Resident Drawer draws) off its pictures. View only: no rule reads any of it.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 kit fps_shuttle_badges, part 2: nothing of the viewed unit floats round the eye.
    /// <para>Why the badges floated: the view holds its unit's model off ONLY inside its camera's pictures (fps_view_fix (c): forceRenderingOff set
    /// at RenderPipelineManager.beginCameraRendering, given back at endCameraRendering). That reaches the renderers the engine culls itself on the
    /// CPU. Since version 1.3 (item 25 f) the GPU Resident Drawer is on (the URP assets' gpuResidentDrawerMode InstancedDrawing, BatchRendererGroup
    /// variants kept, OpenZ/TeamLit with its DOTS instancing variant): it draws every mesh renderer that carries NO property block from its own
    /// instance data, which takes a renderer's changes once a frame before the pictures, so a switch made and undone inside one picture never reaches
    /// it (OzGrd's note: the drawer never takes a renderer with a property block; units keep their team paint block, so their bodies, weapons and
    /// kit are held as meant). VeteranMarks' golden chevrons (OZ_VetChevron, two on the shoulder pads of every veteran robot, on its upper-arm
    /// bones) are made after the team paint and get no block: the view's hold missed exactly them, and with the body held off they hung in the air
    /// </para>
    /// <para>The fix: while the view looks out of a robot or the shuttle, each frame before the pictures (OzFpsView.PlaceCamera, LateUpdate 31900,
    /// after VeteranMarks attached any new chevron in its LateUpdate 40) every mesh renderer of that unit without a property block gets one small
    /// block with a value no shader reads (<see cref="KeepProperty"/>, the same one OzGrd.KeepCpuCulling writes): the picture is the same, the
    /// renderer is culled by the engine again, and the view's own per-picture hold takes it like the body. The same for the robots the view holds
    /// off for being by the eye (a squad mate within 1.5 m). Nothing else changes: the chevrons stay where VeteranMarks put them, on their bones,
    /// on, drawn in every other camera and between the pictures. Once the view is over (OzFpsView.On false: with kit fps_feel only after its glide
    /// out) every block this made is taken off again (SetPropertyBlock(null)) if it still holds only this value: the renderer is exactly as it was.
    /// A block another writer filled meanwhile (the team paint, the dead tint, a camera fade) is left as that writer left it (the unread value
    /// in it changes nothing).</para>
    /// </summary>
    public static class OzFpsBadgeHold
    {
        /// <summary>Off = the 1.4 behaviour (the root cause's before / after; the Play check's reproduction row).</summary>
        public static bool On = true;
        /// <summary>The value the block carries (no shader reads it; OzGrd.KeepCpuCulling writes the same).</summary>
        public const string KeepProperty = "_OzKeepCpuCulling";
        static readonly int KeepId = Shader.PropertyToID(KeepProperty);
        // what other writers put into a block (TeamPaint, OzDeadTint, TeamPaint.Heat, a camera fade): such a block is theirs too, never taken off
        static readonly int TeamColorId = Shader.PropertyToID("_TeamColor"), VividId = Shader.PropertyToID("_OZTeamVivid"), DeadId = Shader.PropertyToID("_OZDead"),
                            HeatId = Shader.PropertyToID("_OZHeat"), EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor"),
                            FadeId = Shader.PropertyToID(OzShuttleCamFade.FadeProperty);

        /// <summary>Check counters: blocks made, taken off again, left because another writer filled them; renderers holding one of ours now.</summary>
        public static int Marks, Releases, KeptForOthers;
        public static int MarkedNow => marked.Count;
        /// <summary>The renderer carries this kit's block now (the checks).</summary>
        public static bool IsMarked(Renderer r) => r && markedSet.Contains(r);

        static MaterialPropertyBlock keep, read;
        static readonly List<MeshRenderer> buf = new List<MeshRenderer>();
        static readonly List<Material> mats = new List<Material>();
        static readonly List<Renderer> marked = new List<Renderer>();
        static readonly HashSet<Renderer> markedSet = new HashSet<Renderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = true; Marks = Releases = KeptForOthers = 0; marked.Clear(); markedSet.Clear(); keep = null; read = null; }

        /// <summary>The units this looks after: a robot (the owner's badges, every robot type) and the shuttle (kit fps_shuttle_badges part 1).</summary>
        public static bool Wants(ZObject u) => u != null && (u.Kind == ZKind.Robot || OzFpsShuttleView.Flies(u));

        /// <summary>OzFpsView.PlaceCamera, right after the view renewed its lists (every frame while it is on, and at its entry): the viewed unit's
        /// mesh renderers without a block, and those of the robots it holds off by the eye (figs entries that are unit views, unitFigs).</summary>
        public static void Mark(ZObject unit, UnitView view, Dictionary<Transform, List<Renderer>> figs, HashSet<Transform> unitFigs)
        {
            if (!On || !view) return;
            if (Wants(unit))
            {
                buf.Clear(); view.GetComponentsInChildren(true, buf);
                for (int i = 0; i < buf.Count; i++) MarkOne(buf[i]);
            }
            if (figs == null || unitFigs == null) return;
            foreach (var kv in figs)
            {
                var t = kv.Key; if (!t || !unitFigs.Contains(t)) continue;
                var uv = t.GetComponent<UnitView>(); if (!uv || uv.Obj == null || uv.Obj.Kind != ZKind.Robot) continue;   // another robot by the eye (a squad mate)
                var l = kv.Value; for (int i = 0; i < l.Count; i++) MarkOne(l[i]);
            }
        }

        /// <summary>1.5 close_hide_all_parts_r6 (OzFpsFeel.Collect): a block-less mesh part of a unit or crew figure the first-person eye fades (a
        /// lamp lens, a weapon, a box another kit added) gets this kit's block too, so that fade's per-picture hold reaches it; taken off with the
        /// others once the view is over (ReleaseWhenOut). True: a block was set now.</summary>
        public static bool Keep(Renderer r)
        {
            if (!On || !(r is MeshRenderer) || !r || r.HasPropertyBlock()) return false;
            MarkOne(r); return true;
        }

        static void MarkOne(Renderer r)
        {
            if (!(r is MeshRenderer) || !r || r.HasPropertyBlock()) return;                                        // a renderer with a block is never the drawer's: held as it was
            if (keep == null) { keep = new MaterialPropertyBlock(); keep.SetFloat(KeepId, 1f); }
            r.SetPropertyBlock(keep);                                                                              // review: also again when another writer cleared its block during the view (SetPropertyBlock(null)): it would be the drawer's again
            if (markedSet.Add(r)) { marked.Add(r); Marks++; }
        }

        /// <summary>OzFpsView.Update while the view is off: once nothing of the view holds the camera any more (OzFpsView.On false; with kit
        /// fps_feel that is after its glide out), every block this made is taken off.</summary>
        public static void ReleaseWhenOut() { if (marked.Count == 0 || OzFpsView.On) return; Release(); }

        /// <summary>Every block this made is taken off again when it still holds only this value (OzFpsView.OnDestroy calls it too).</summary>
        public static void Release()
        {
            if (marked.Count == 0) return;
            read ??= new MaterialPropertyBlock();
            for (int i = 0; i < marked.Count; i++)
            {
                var r = marked[i]; if (!r) continue;
                read.Clear(); r.GetPropertyBlock(read);
                if (read.isEmpty || !read.HasFloat(KeepId)) continue;                                                  // another writer set a block of its own: not this kit's any more
                if (Others(read) || MaterialBlocks(r)) { KeptForOthers++; continue; }
                r.SetPropertyBlock(null); Releases++;
            }
            marked.Clear(); markedSet.Clear();
        }

        static bool Others(MaterialPropertyBlock b) =>
            b.HasColor(TeamColorId) || b.HasVector(VividId) || b.HasFloat(DeadId) || b.HasFloat(HeatId) || b.HasColor(EmissionId) || b.HasColor(BaseColorId)
            || (b.HasFloat(FadeId) && b.GetFloat(FadeId) != 0f);

        /// <summary>A block of one of its materials was set meanwhile (a per-material writer): the renderer is left with its blocks.</summary>
        static bool MaterialBlocks(Renderer r)
        {
            r.GetSharedMaterials(mats);
            for (int m = 0; m < mats.Count; m++) { read.Clear(); r.GetPropertyBlock(read, m); if (!read.isEmpty) return true; }
            return false;
        }

        public static string Status() => $"badge hold {(On ? "on" : "off")}: blocks made {Marks}, taken off {Releases}, left to other writers {KeptForOthers}, held now {MarkedNow}";
    }
}
