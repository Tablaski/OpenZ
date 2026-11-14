#if UNITY_EDITOR
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k92, the Play proofs and films after the v2 units' checks (editor only):
    ///  * <see cref="HoldLevelUnits"/>: a boarding check gives an empty unit to a red robot; the level's own units near it boarded first (the gun
    ///    proof's reman / board lines: "owner Blue"; the mech's board film: a blue robot climbed in). Before the order, the level's own units of
    ///    another side near the empty unit stand still, without a weapon, for the rest of the check (the proof's own units untouched).
    ///  * <see cref="GearStill"/>: the track tests judged "standing" by the sim's IsMoving; after the sim stops the DRAWN tank still settles
    ///    for about a second (its place and heading ease in), so its planted belts follow the ground under them (the recorded frames: the ground
    ///    under the root moved 0.5 deg while "standing"; while it really stood, firing, the v1 belts did not move: 0.00-0.02 deg). Standing now
    ///    also needs the drawn tank's ground place (UnitView.ProofGearPlace) still.
    ///  * <see cref="MediumSilhouette"/>: the medium tank's Play size line uses k87b's silhouette rule (the parity's: its outline from the RTS
    ///    camera midway between the light tank v2 and the heavy tank v2), not the drawn length (the light v2's long gun makes it the longest).
    /// </summary>
    public static class ProofStageK92
    {
        /// <summary>The level's own robots and vehicles of a side other than `keep` (and not neutral) within `tiles` of any of `around` stand
        /// still and have no weapon (their orders dropped). Returns how many.</summary>
        public static int HoldLevelUnits(ZWorld W, IEnumerable<ZObject> around, Team keep, System.Action<string> log, string what, int tiles = 24)
        {
            if (W == null) return 0;
            var at = new List<ZObject>(); foreach (var a in around) if (a != null) at.Add(a);
            int n = 0;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Destroyed || o.Owner == Team.Neutral || o.Owner == keep) continue;
                if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle) continue;
                bool near = false;
                foreach (var a in at) if (Mathf.Abs(o.CenterX - a.CenterX) <= tiles * 16 && Mathf.Abs(o.CenterY - a.CenterY) <= tiles * 16) { near = true; break; }
                if (!near) continue;
                o.MoveSpeed = 0; o.RealMoveSpeed = 0; o.Waypoints.Clear(); o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; n++;
            }
            log?.Invoke($"staged (k92, {what}): the level's own {n} unit(s) of another side within {tiles} tiles stand still without a weapon (they boarded the empty unit before the red robot in the last run)");
            return n;
        }

        static readonly Dictionary<UnitView, (Vector3 p, float yaw, float movedAt)> still = new Dictionary<UnitView, (Vector3, float, float)>();
        /// <summary>Once per sampled frame: the drawn unit's planted-gear place has not moved (5 mm) nor turned (0.1 deg) for `settleS` s.</summary>
        public static bool GearStill(UnitView v, float settleS)
        {
            if (!v || !v.ProofGearPlace(out var p, out float y)) return false;
            float now = Time.time;
            if (!still.TryGetValue(v, out var s)) { still[v] = (p, y, now); return false; }
            bool moved = (p - s.p).sqrMagnitude > 0.005f * 0.005f || Mathf.Abs(Mathf.DeltaAngle(y, s.yaw)) > 0.1f;
            float movedAt = moved ? now : s.movedAt;
            still[v] = (p, y, movedAt);
            return !moved && now - movedAt >= settleS;
        }

        /// <summary>The medium tank's size by k87b's silhouette rule (the editor's OzMediumTankK87b.SizeCheck on the three prefabs).</summary>
        public static bool MediumSilhouette(out string what)
        {
            what = "size (silhouette rule): ";
            var t = System.Type.GetType("OpenZ.EditorTools.OzMediumTankK87b, Assembly-CSharp-Editor");
            var m = t != null ? t.GetMethod("SizeCheck", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) : null;
            var mt = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OpenZ/Resources/OpenZ/MTank/Z_mtank.prefab");
            if (m == null || !mt) { what += $"not run (the editor's size check {(m != null ? "found" : "MISSING")}, the medium prefab {(mt ? "found" : "MISSING")})"; return false; }
            var args = new object[] { mt, null };
            bool ok = (bool)m.Invoke(null, args);
            what = (string)args[1];
            return ok;
        }
    }
}
#endif
