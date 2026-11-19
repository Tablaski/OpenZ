// OpenZ (GPL-3.0)
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit robot_pieces_ground_r10 (A35). The robot pop death (RobotPopDeath.DoPlay) throws copies of the robot's own rigid parts: the BS-01
    /// type kit (grunt steel pot + pack, tough helmet + pauldron, sniper hat + cape, pyro tanks, psycho drum, hip plates), the gun, the heroes' and
    /// the laser robot's rigid parts. Each copy took the mesh of the part as it is and was flown and put down by the part's PIVOT. The BS-01 kit
    /// vertices where they are on the body; BS01_Type_Grunt.fbx: the helmet's vertices 2.16-2.40 m and the pack's 1.48-1.95 m above their own
    /// pivot, x the model's scale): so the pivot is at the robot's FEET while the drawn helmet / pack is 2-3.5 m above it. The pivot fell, bounced
    /// and rested on the ground (pos.y = GroundY + rad) and spun about that far point, and the drawn helmet and pack stayed up in the air. That is
    /// the owner's frame. The same family as EvilZod's cape (U9h, OzEvilZodBurst.Cape) and the gun seats (A29: pieces measured by what they do not
    /// draw).
    /// <para>Now every piece of a robot's pop (its own rigid parts, its baked body parts, the made-up chunks) is a root at the MIDDLE of what it
    /// draws with the mesh on a child put back by the mesh's own box centre (<see cref="Fit"/>): it flies, spins and lands about its middle; it
    /// meets the ground with the real half height of its box as it is turned now (<see cref="FitHalfUp"/>, no corner in the ground, no gap);
    /// when it comes to rest it tips onto the most level box face it can stand on (FitFace) in <see cref="OzRobotPieceRest.SettleS"/> s and lies on it (the face
    /// touches what the mesh draws, so the lowest drawn point is on the ground, <see cref="OzRobotPieceRest.LiftM"/> over it); it follows the
    /// ground if a crater is dug under it and falls again if the deck under it goes. Its life, glow / dead-colour fade, water and lava rules and the
    /// pop out at its end are unchanged (PopOut scales it about its drawn bottom, as before). Chips (HC1, RobotPopDeath.Chips) keep their own rest
    /// rule. The parts knocked off a LIVING vehicle (HC1 KnockOff) are not robots: unchanged.</para>
    /// Switch <see cref="OzRobotPieceRest.On"/> (false = drawn and moved exactly as before: the mesh child stays at the root, nothing fitted; the pool
    /// pieces always carry the child, so the switch can be flipped in Play).
    /// </summary>
    public partial class RobotPopDeath
    {
        /// <summary>A pool piece with its mesh on a child (the child keeps the name, so every check that looks for an OZ_PopPiece renderer still finds it).</summary>
        static Piece NewFitPiece(GameObject go)
        {
            var mg = new GameObject("OZ_PopPiece") { layer = go.layer };
            mg.transform.SetParent(go.transform, false);
            return new Piece { go = go, t = go.transform, mt = mg.transform, mf = mg.AddComponent<MeshFilter>(), mr = mg.AddComponent<MeshRenderer>() };
        }

        /// <summary>Take(): a piece starts unfitted, its mesh at the root (chips, knock-offs: exactly as before).</summary>
        static void Unfit(Piece p)
        {
            p.fit = false;
            if (p.mt) p.mt.localPosition = Vector3.zero;
        }

        /// <summary>The piece's root goes to the middle of what its mesh draws (the mesh's own box centre), the mesh child back by as much: nothing
        /// moves on screen. Call after its pose and scale are set, before Launch.</summary>
        void Fit(Piece p)
        {
            if (!OzRobotPieceRest.On || !p.mt || !p.mf || !p.mf.sharedMesh) return;
            var b = p.mf.sharedMesh.bounds;
            if (b.extents.sqrMagnitude < 1e-8f) return;
            var c = b.center;
            if (Vector3.Scale(c, p.t.localScale).magnitude > OzRobotPieceRest.FarPivotM) OzRobotPieceRest.FarPivots++;
            p.t.position = p.t.TransformPoint(c);
            p.mt.localPosition = -c;
            p.be = b.extents; p.fit = true;
            OzRobotPieceRest.Fitted++;
        }

        /// <summary>Half the height of the piece's box along world up, as it is turned now (its root is the box's middle).</summary>
        static float FitHalfUp(Piece p)
        {
            var q = p.t.rotation; var s = p.t.localScale;
            return Mathf.Abs((q * Vector3.right).y) * p.be.x * Mathf.Abs(s.x) + Mathf.Abs((q * Vector3.up).y) * p.be.y * Mathf.Abs(s.y)
                 + Mathf.Abs((q * Vector3.forward).y) * p.be.z * Mathf.Abs(s.z);
        }

        /// <summary>The piece came to rest at pos (gy = the ground there): it will lie on the most level box face it can stand on (<see cref="FitFace"/>),
        /// that face on the ground's slope (four samples, as a chip's), the lowest drawn point LiftM over it.</summary>
        void FitRest(Piece p, Vector3 pos, float gy, ZGame game)
        {
            const float d = 0.25f;
            var n = new Vector3(game.GroundY(pos.x - d, pos.z) - game.GroundY(pos.x + d, pos.z), 2f * d, game.GroundY(pos.x, pos.z - d) - game.GroundY(pos.x, pos.z + d)).normalized;
            if (n.y < 0.85f) n = Vector3.up;                                                            // a deck edge / kerb step under it: lie level
            var q = p.t.rotation; var s = p.t.localScale;
            Vector3 ax = q * Vector3.right, ay = q * Vector3.up, az = q * Vector3.forward;
            float dx = Vector3.Dot(ax, n), dy = Vector3.Dot(ay, n), dz = Vector3.Dot(az, n);
            float ex = p.be.x * Mathf.Abs(s.x), ey = p.be.y * Mathf.Abs(s.y), ez = p.be.z * Mathf.Abs(s.z);
            int f = FitFace(Mathf.Abs(dx), Mathf.Abs(dy), Mathf.Abs(dz), ex, ey, ez);
            Vector3 a; float h;
            if (f == 0) { a = dx < 0f ? -ax : ax; h = ex; }
            else if (f == 1) { a = dy < 0f ? -ay : ay; h = ey; }
            else { a = dz < 0f ? -az : az; h = ez; }
            p.fromP = pos; p.fromQ = q;
            p.restQ = Quaternion.FromToRotation(a, n) * q;                                              // the smallest turn: that face down on the ground
            p.restN = n; p.restGy = gy;
            p.restP = new Vector3(pos.x, gy + h / Mathf.Max(0.5f, n.y) + OzRobotPieceRest.LiftM, pos.z);
            FitRestSurface(p);
            p.settle = 0f; p.chk = R(0.2f, 0.4f);
            OzRobotPieceRest.Rested++;
        }

        /// <summary>Independent review (16 Nov): the box face a landed piece lies on = the most level face (|d| = its axis against the ground's
        /// normal) whose middle stays over that face's edge at the tilt it landed with (h sin^2 &lt;= (|d_i| e_i + |d_j| e_j) cos): a long piece (a gun,
        /// an arm, a tank) that lands near end-on tips over onto its side instead of being stood upright on its end; none stands (an exact edge):
        /// the most level face. 0 = x, 1 = y, 2 = z. e = the box's half sizes (scaled).</summary>
        static int FitFace(float dx, float dy, float dz, float ex, float ey, float ez)
        {
            int best = -1; float bd = -1f;
            if (ex * (1f - dx * dx) <= (dy * ey + dz * ez) * dx + 1e-5f && dx > bd) { best = 0; bd = dx; }
            if (ey * (1f - dy * dy) <= (dx * ex + dz * ez) * dy + 1e-5f && dy > bd) { best = 1; bd = dy; }
            if (ez * (1f - dz * dz) <= (dx * ex + dy * ey) * dz + 1e-5f && dz > bd) { best = 2; bd = dz; }
            if (best >= 0) return best;
            return dx >= dy && dx >= dz ? 0 : dy >= dz ? 1 : 2;
        }

        /// <summary>A resting fitted piece: it tips onto its face, follows the ground (a crater dug under it), falls again when the deck under
        /// it goes.</summary>
        void FitRestUpdate(Piece p, float dt, ZGame game)
        {
            if ((p.chk -= dt) <= 0f)
            {
                p.chk = OzRobotPieceRest.RecheckS;
                float gy = game.GroundY(p.restP.x, p.restP.z), dy = gy - p.restGy;
                if (dy < -OzRobotPieceRest.DropM)
                {   // the support went: it falls again (and lands, rests and lies on its face again)
                    p.rest = false; p.settle = 1f; p.v = Vector3.zero; p.w = Random.onUnitSphere * 60f;
                    OzRobotPieceRest.Refell++;
                    return;
                }
                if (Mathf.Abs(dy) > 0.01f) { p.restGy = gy; p.restP.y += dy; p.fromP.y += dy; if (p.settle >= 1f) p.t.position = p.restP; }
            }
            if (p.settle < 1f)
            {
                p.settle = Mathf.Min(1f, p.settle + dt / Mathf.Max(0.02f, OzRobotPieceRest.SettleS));
                float e = p.settle * p.settle * (3f - 2f * p.settle);
                p.t.SetPositionAndRotation(Vector3.Lerp(p.fromP, p.restP, e), Quaternion.Slerp(p.fromQ, p.restQ, e));
                if (p.settle >= 1f) OzRobotPieceRest.Lying++;
            }
        }

        /// <summary>Play check (OzRobotPiecesCheck): one robot pop piece as the check sees it.</summary>
        public struct PieceInfo { public Transform t; public bool chip, fit, rest, wet, popping; public float age, life; }

        /// <summary>Play check: the pop pieces in use now (not the chips unless asked).</summary>
        public static int ListPieces(List<PieceInfo> into, bool chips = false)
        {
            into.Clear(); var me = inst; if (!me) return 0;
            foreach (var p in me.pieces)
            {
                if (!p.active || !p.go || !p.go.activeInHierarchy || (p.chip && !chips)) continue;
                into.Add(new PieceInfo { t = p.t, chip = p.chip, fit = p.fit, rest = p.rest, wet = p.wet, popping = p.popping, age = p.age, life = p.life });
            }
            return into.Count;
        }
    }

    /// <summary>1.5 kit robot_pieces_ground_r10 (A35): the switch, the numbers and the counters of the robot pieces' rest
    /// (View/RobotPopDeath.PieceRest.cs), and EvilZod's burst pieces brought down onto the ground when they rest above it
    /// (<see cref="Settle"/>, OzEvilZodBurst).</summary>
    public static class OzRobotPieceRest
    {
        /// <summary>false = exactly as before (pieces flown and put down by their pivot; EvilZod's pieces stop where they stop).</summary>
        public static bool On = true;
        /// <summary>The lowest drawn point this much over the ground (m); the tip onto its face (s); how often a lying piece looks at the ground
        /// under it (s); a drop of the ground under it that makes it fall again (m); a pivot this far from the middle of the mesh is counted (m).</summary>
        public static float LiftM = 0.01f, SettleS = 0.25f, RecheckS = 0.4f, DropM = 0.15f, FarPivotM = 0.25f;
        /// <summary>EvilZod's pieces: gravity of the drop (m/s2, his burst's own), the most points kept per piece to find its lowest one.</summary>
        public static float Gravity = 13f; public static int MaxPoints = 400;
        /// <summary>Counters (Play checks): pieces fitted, of them with the pivot far from the drawn middle (the BS-01 kit, ...), come to rest,
        /// lying on a face, fallen again; EvilZod's pieces brought down.</summary>
        public static int Fitted, FarPivots, Rested, Lying, Refell, EvilDrops;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Statics() { Fitted = FarPivots = Rested = Lying = Refell = EvilDrops = 0; On = true; }

        public static string Status() =>
            $"robot pieces (A35): {(On ? "ON" : "OFF")}, fitted {Fitted} (pivot far from the drawn middle {FarPivots}), came to rest {Rested}, lying on a face {Lying}, fell again {Refell}; EvilZod pieces brought down {EvilDrops}";

        /// <summary>OzEvilZodBurst: up to MaxPoints of a piece's own points (its mesh vertices, piece space), every one when fewer; the box
        /// extremes always in.</summary>
        public static Vector3[] Sample(List<Vector3> pv)
        {
            if (pv == null || pv.Count == 0) return null;
            int st = Mathf.Max(1, Mathf.CeilToInt(pv.Count / (float)MaxPoints));
            var o = new List<Vector3>(Mathf.Min(pv.Count, MaxPoints) + 6);
            int lx = 0, hx = 0, ly = 0, hy = 0, lz = 0, hz = 0;
            for (int i = 0; i < pv.Count; i++)
            {
                var v = pv[i];
                if (v.x < pv[lx].x) lx = i; if (v.x > pv[hx].x) hx = i; if (v.y < pv[ly].y) ly = i; if (v.y > pv[hy].y) hy = i; if (v.z < pv[lz].z) lz = i; if (v.z > pv[hz].z) hz = i;
                if (i % st == 0) o.Add(v);
            }
            o.Add(pv[lx]); o.Add(pv[hx]); o.Add(pv[ly]); o.Add(pv[hy]); o.Add(pv[lz]); o.Add(pv[hz]);
            return o.ToArray();
        }

        /// <summary>OzEvilZodBurst, a resting piece: its lowest drawn point (pts in its space) more than LiftM over the ground under it: it drops
        /// (1 g from vy, never below that point's ground). A piece lying on or in the ground is left as it is.</summary>
        public static void Settle(Transform t, Vector3[] pts, ZGame g, float dt, ref float vy)
        {
            if (!On || !t || pts == null || pts.Length == 0 || !g) return;
            var m = t.localToWorldMatrix; var low = m.MultiplyPoint3x4(pts[0]);
            for (int i = 1; i < pts.Length; i++) { var w = m.MultiplyPoint3x4(pts[i]); if (w.y < low.y) low = w; }
            float gap = low.y - g.GroundY(low.x, low.z);
            if (gap <= LiftM + 0.005f) { vy = 0f; return; }
            if (vy >= 0f) EvilDrops++;
            vy = Mathf.Min(0f, vy) - Gravity * dt;
            float step = Mathf.Max(vy * dt, -(gap - LiftM));
            t.position += Vector3.up * step;
            if (step > vy * dt) vy = 0f;                                                                     // it reached the ground
        }
    }
}
