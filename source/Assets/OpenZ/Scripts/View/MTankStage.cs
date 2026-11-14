#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k87b: the medium tank's Play proof staging (View/MTankProof.cs calls it) on k84b's light-proof staging (View/LightV2Stage.cs: the same
    /// code, the words of the one firing tank of this proof, "the red medium tank"):
    ///  * FIRE (tracks_planted, recoil, head kick, hull rock: standing and firing): k87's red medium tank fought the level's own grunts and drove
    ///    (its tracks' pitch moved 0.8 deg, their bottom 5.6 cm): the jeep now stands at k84b's open tile in reach (LightV2Spots proof/jeep
    ///    near: the medium tank stands where the light tank's red_v2 stands), the level's own enemies near it have no weapon, it takes the jeep;
    ///  * HATCH: a veteran crew stays buttoned up (DV12): its kills and rank are logged and a veteran's rank reset for the check (k81d);
    ///  * DEATH: its seeded way to break up (DV18) is told first and the proof waits until it broke (a cook-off after 5-7 s of burning).
    /// </summary>
    public static class MTankStage
    {
        static string Words(string s) => s.Replace("red_v1", "the red medium tank").Replace("red_v2", "the red medium tank").Replace("the red pair", "the red medium tank");

        /// <summary>The fire phase: k84b's staging with the red medium tank as its one firing tank, the jeep held in reach.</summary>
        public static void FireStage(ZWorld W, ZObject rm, ZObject jeep, IEnumerable<ZObject> proofUnits, System.Action<string> log)
        {
            log("k87b: k84b's fire staging (View/LightV2Stage.cs) for the red medium tank, this proof's one firing tank");
            string last = null;
            LightV2Stage.FireStage(W, rm, rm, jeep, proofUnits, s => { s = Words(s); if (s != last) log(s); last = s; });
            HoldTarget(jeep, log);
        }

        /// <summary>A staged target (the unarmed, unkillable jeep) never steps aside for a shell: it cannot move, so Zod's DodgeMissile skips
        /// it (a unit that cannot move never dodges) and it stays where the staging put it, in reach (each MLRS rocket aimed at it made it
        /// dodge: k81d's MLRS track take ended with it 9 tiles off, out of reach, and the firing tanks drive after a target out of reach).</summary>
        public static void HoldTarget(ZObject jeep, System.Action<string> log)
        {
            if (jeep == null || jeep.Destroyed) return;
            jeep.MoveSpeed = 0; jeep.RealMoveSpeed = 0;
            for (int i = jeep.Waypoints.Count - 1; i >= 0; i--) if (jeep.Waypoints[i].mode == WpMode.Dodge) jeep.Waypoints.RemoveAt(i);
            log?.Invoke($"staged (k87b): the jeep #{jeep.Id} never steps aside for a shell (it cannot move: Zod's DodgeMissile skips it), it stays at tile ({jeep.CenterX >> 4},{jeep.CenterY >> 4}) in reach");
        }

        /// <summary>The hatch check: k84b's (k81d's) rank staging for the red medium tank.</summary>
        public static void HatchStage(ZObject rm, System.Action<string> log) => LightV2Stage.HatchStage(rm, null, s => log(Words(s)));

        /// <summary>The red medium tank's seeded way to break up (DV18), told before the kill.</summary>
        public static string DeathWay(ZObject rm) =>
            LightV2Stage.DeathWays(rm, "the red medium tank", null, "-").Replace("; - -", "").Replace("the break-ups to come", "the break-up to come").Replace("until both broke", "until it broke");
    }

    /// <summary>
    /// k87b, picture b (the medium tank firing, close): k87's own capture missed (its grunts stood 8 tiles out, past the gun's 7.5 tiles).
    /// In Play on L02 (menu OpenZ/Medium Tank/17): a red medium tank (cannot be destroyed) at the recoil test's open tile, an unkillable,
    /// unarmed blue jeep at k84b's open tile in reach (LightV2Spots recoil/jeep near: 58-80 px, the gun reaches 120), the level's own
    /// enemies near it without a weapon (MTankStage.FireStage); the camera close (24 m) on the tank; from its first shot 24 frames 1/15 s
    /// apart (the shot's own frame first) into v2_units/medium/proof/pictures_k87b/b_close_fire_*.png, the distance logged.
    /// </summary>
    public class MTankCloseFire : MonoBehaviour
    {
        static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/medium/proof/pictures_k87b"));
        readonly List<string> log = new List<string>();

        public static void Begin()
        {
            var go = new GameObject("MTankCloseFire"); go.AddComponent<MTankCloseFire>();
        }

        void Note(string s) { log.Add(s); Debug.Log("[OzMediumTank] close fire: " + s); }

        IEnumerator Start()
        {
            var g = ZGame.I; var W = g ? g.World : null;
            if (W == null) { Debug.LogWarning("[OzMediumTank] close fire: no level in Play"); Destroy(gameObject); yield break; }
            if (!HeavyV2Spots.Get(g.level, "recoil", "v2", out int tx, out int ty) || !LightV2Spots.Get(g.level, "recoil", "jeep near", out int jx, out int jy))
            { Debug.LogWarning($"[OzMediumTank] close fire: no fixed tiles for {g.level} (L02 only)"); Destroy(gameObject); yield break; }
            var m = W.CreateObject("vehicle", OzMediumTank.Type, tx * 16, ty * 16, Team.Red); g.SpawnUnitView(m);
            m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m);
            var j = W.CreateObject("vehicle", "jeep", jx * 16, jy * 16, Team.Blue); g.SpawnUnitView(j);
            j.MaxHealth = 100000000; j.SetHealth(j.MaxHealth); W.UpdateObjectHealth(j); j.Damage = 0; j.AttackRadius = 0;
            Note($"red medium tank #{m.Id} at tile ({tx},{ty}), blue jeep #{j.Id} at tile ({jx},{jy}) (cannot be destroyed, no weapon)");
            MTankStage.FireStage(W, m, j, new[] { m, j }, Note);
            var cam = FindFirstObjectByType<RtsCamera>();
            Directory.CreateDirectory(Dir);
            float t0 = Time.unscaledTime; UnitView v = null; float last = -1f; int got = 0;
            while (Time.unscaledTime - t0 < 25f && got < 24)
            {
                if (!v && g.Views.TryGetValue(m.Id, out var vv)) { v = vv; last = v.LastFireAt; }
                if (cam && v) { var p = ZGame.PxToWorld(m.CenterX, m.CenterY); cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(24f); }
                if (v && got == 0)
                {
                    if (v.LastFireAt > last + 1e-4f) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "b_close_fire_00.png")); got = 1; Note($"first shot {Time.unscaledTime - t0:0.0} s in; the jeep {Mathf.Sqrt((m.CenterX - j.CenterX) * (float)(m.CenterX - j.CenterX) + (m.CenterY - j.CenterY) * (float)(m.CenterY - j.CenterY)):0} px off (the gun reaches {m.AttackRadius} px), the tank {(m.IsMoving ? "MOVING" : "standing")}"); }
                }
                else if (got > 0 && Time.frameCount % 2 == 0) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"b_close_fire_{got:00}.png")); got++; }
                yield return null;
            }
            Note(got > 0 ? $"the shot's frame + {got - 1} frames after it in {Dir}" : "NO shot within 25 s (the jeep out of reach or no line of sight?)");
            File.WriteAllLines(Path.Combine(Dir, "b_close_fire_log.txt"), log);
            Destroy(gameObject);
        }
    }
}
#endif
