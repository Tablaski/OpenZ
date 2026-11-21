using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 39 (the player's PDF ideas, PDF_IDEAS_CHECK.md sections 3.3 and 5 item 3): the hover pre-cull. <see cref="HoverAt"/> runs every
    /// frame and asked every hoverable object of the map (rocks, huts, cacti, props, wrecks, flags, crates, enemy units) for its pick box
    /// (<see cref="ScreenRect"/>): for a map item one ground ray (ZGame.GroundY) and 8 screen projections, for a unit 8 transforms and 8
    /// projections, wherever the pointer was. Now an object is first tested against a box in the world that holds every point its pick box
    /// is made of, projected once with interval arithmetic (no ray, no Unity projection call); when the pointer is outside that box's
    /// screen reach plus every pixel the pick box can grow by (Grow / MinSize), the object is skipped, else it is asked exactly as before.
    ///
    /// Why the hovered object stays the same (the proof is in SOLUTION.md section 3):
    ///  * HoverAt keeps the LAST object in list order whose pick box holds the pointer (units beat the rest). A skipped object's pick box
    ///    cannot hold the pointer, so leaving it out changes neither h nor hu.
    ///  * The test box is built from the same anchor ScreenRect uses at the same moment (the unit's drawn model, the prop's foot, the
    ///    footprint), so no drawn offset (spacing, knock-backs, bridge decks, a tossed robot) has to be guessed. The one unknown, the
    ///    ground height under a map item (the ray), is replaced by the whole height range of the walk ground (every walk-layer collider,
    ///    re-read once a second, plus <see cref="GroundPadM"/>).
    ///  * An object is skipped only when asking it has no effect on anything else: never a building or the shuttle (their frame is the
    ///    per-frame silhouette that the brackets, the production window and the shuttle overlay read again later in the frame), never a
    ///    vehicle or gun whose model box is not measured yet (the measure happens on the same frame as today), never a robot getting out
    ///    of a carrier (its proof counters and its figure), never a unit without a drawn model (tower guns, fort defenders: today's
    ///    footprint code), never anything near or behind the camera.
    /// <see cref="HoverPreCull"/> = false: the old full pass. View only: no rule, picture, cursor or click changes.
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>On (default): objects whose pick box cannot reach the pointer are not asked for it. Off: the full pass as in 1.2.</summary>
        public static bool HoverPreCull = true;
        /// <summary>Checks: the objects of the last pass skipped by the pre-cull and asked for their pick box (both 0 with the switch off).</summary>
        public static int HoverSkipped, HoverAsked;
        /// <summary>Metres added below and above the walk ground's height range: crater dents still easing in (a bowl is at most about 1.5 m
        /// deep), a bridge deck's sag (at most 0.2 m, OzBridgeWobble), colliders made since the last read (bowls, decks). Seconds between two
        /// reads of the range. Pixels the pick box may grow beyond its points, at 1080 lines (ScreenRect: Grow 3 + MinSize 22 / 2 = 14 at
        /// most), plus a pixel margin for the float rounding of the two projections (fixed, and a share of the projected numbers' size).
        /// Metres in front of the camera a test box must keep.</summary>
        public const float GroundPadM = 10f, GroundRescanS = 1f, GrowPx = 24f, RoundPx = 4f, RoundRel = 1e-4f, NearM = 0.5f;
        /// <summary>The tallest pick box of a map item on the footprint path (ScreenRect: 2.2 m huts / cacti, 1.2 m the rest), with room.</summary>
        public const float ItemTopM = 2.5f;

        // the frame's camera, read once a pass: rows of worldToClip (x, y, w) and of the camera depth, the pixel rect, the margin
        Vector4 farX, farY, farW, farD, pointerLeft, pointerRight, pointerBottom, pointerTop;
        float farPx, farPy, farPw, farPh, farMargin;
        // the walk ground's height range (GroundY can answer nothing outside it)
        bool groundOk; float groundLo, groundHi, groundNext = -1f; ZWorld groundWorld; Collider terrainCol;
        static readonly Collider[] walkCols = new Collider[4096];
        readonly List<(int Start, int End, Vector2 Min, Vector2 Max)> rockRuns = new();
        ZWorld rockWorld;
        int rockCount, rockListRevision, rockPositionRevision;

        void EnsureRockRuns(ZWorld world)
        {
            var objects = world.Objects;
            if (rockWorld == world && rockCount == objects.Count && rockListRevision == world.ObjectListRevision &&
                rockPositionRevision == world.RockPositionRevision) return;
            rockWorld = world;
            rockCount = objects.Count;
            rockListRevision = world.ObjectListRevision;
            rockPositionRevision = world.RockPositionRevision;
            rockRuns.Clear();
            for (int start = 0; start < objects.Count;)
            {
                int end = start;
                Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
                for (; end < objects.Count && end < start + 16; end++)
                {
                    var rock = objects[end];
                    if (rock.Kind != ZKind.MapItem || rock.Type != "rock" || rock.BlockTiles != null ||
                        rock.IsMoving || rock.XOver != 0 || rock.YOver != 0) break;
                    var a = ZGame.PxToWorld(rock.LocX, rock.LocY);
                    var b = ZGame.PxToWorld(rock.LocX + rock.WidthPix, rock.LocY + rock.HeightPix);
                    min = Vector2.Min(min, new Vector2(a.x, b.z));
                    max = Vector2.Max(max, new Vector2(b.x, a.z));
                }
                if (end - start >= 4) rockRuns.Add((start, end, min, max));
                start = Mathf.Max(start + 1, end);
            }
        }

        /// <summary>Once a pass: the camera rows and the margin. False (no camera): nothing is skipped.</summary>
        bool FarBegin(Vector2 pointer)
        {
            HoverSkipped = 0; HoverAsked = 0;
            var c = Cam; if (!c) return false;
            Matrix4x4 v = c.worldToCameraMatrix, m = c.projectionMatrix * v;
            farX = m.GetRow(0); farY = m.GetRow(1); farW = m.GetRow(3); farD = -v.GetRow(2);                 // depth = -z in camera space = WorldToScreenPoint's z
            var pr = c.pixelRect; farPx = pr.x; farPy = pr.y; farPw = pr.width; farPh = pr.height;
            farMargin = GrowPx * UiK + RoundPx;
            // Planes through the edges of a conservative pixel window around the pointer.
            // A box separated by any plane need not be projected corner by corner.
            float mx = farMargin + RoundRel * (Mathf.Abs(pointer.x) + farMargin + 1f) * 2f;
            float my = farMargin + RoundRel * (Mathf.Abs(pointer.y) + farMargin + 1f) * 2f;
            pointerLeft = farW * (2f * (pointer.x - mx - farPx) / farPw - 1f) - farX;
            pointerRight = farX - farW * (2f * (pointer.x + mx - farPx) / farPw - 1f);
            pointerBottom = farW * (2f * (pointer.y - my - farPy) / farPh - 1f) - farY;
            pointerTop = farY - farW * (2f * (pointer.y + my - farPy) / farPh - 1f);
            if (groundWorld != W || Time.unscaledTime >= groundNext) GroundRange();
            return true;
        }

        /// <summary>The height range of everything ZGame.GroundY can hit: every collider of the walk layer (the terrain and its chunks,
        /// crater bowls, bridge decks), the terrain collider itself (GroundY's second ray) and 0 (its answer when nothing is hit), with
        /// <see cref="GroundPadM"/> below and above. No range (no walk layer of its own, or too many colliders to read): map items on the
        /// footprint path are never skipped.</summary>
        void GroundRange()
        {
            if (groundWorld != W) terrainCol = null;
            groundWorld = W; groundNext = Time.unscaledTime + GroundRescanS; groundOk = false;
            int layer = ZGame.WalkLayer;
            if (layer <= 0 || W == null || W.Map == null) return;                                         // the Default layer (no free layer found) holds anything: no range
            float lo = 0f, hi = 0f, mw = W.Map.WidthPix / 8f, mh = W.Map.HeightPix / 8f;
            int n = Physics.OverlapBoxNonAlloc(new Vector3(mw * 0.5f, 0f, -mh * 0.5f), new Vector3(mw * 0.5f + 1000f, 5000f, mh * 0.5f + 1000f), walkCols, Quaternion.identity, 1 << layer, QueryTriggerInteraction.Ignore);
            if (n >= walkCols.Length) { for (int i = 0; i < walkCols.Length; i++) walkCols[i] = null; return; }
            for (int i = 0; i < n; i++)
            {
                var col = walkCols[i]; walkCols[i] = null;
                if (!col) continue;
                var b = col.bounds; lo = Mathf.Min(lo, b.min.y); hi = Mathf.Max(hi, b.max.y);
            }
            if (!terrainCol) { var t = GameObject.Find("COL_Terrain"); terrainCol = t ? t.GetComponent<Collider>() : null; }
            if (terrainCol) { var b = terrainCol.bounds; lo = Mathf.Min(lo, b.min.y); hi = Mathf.Max(hi, b.max.y); }
            groundLo = lo - GroundPadM; groundHi = hi + GroundPadM; groundOk = true;
        }

        /// <summary>True = ScreenRect(o) cannot hold mp and asking it would change nothing else: skip it. Mirrors ScreenRect's branches in
        /// their order; every branch it does not know is asked as before.</summary>
        bool HoverFar(ZObject o, Vector2 mp)
        {
            bool skip = FarFromPointer(o, mp);
            if (skip) HoverSkipped++; else HoverAsked++;
            return skip;
        }

        bool FarFromPointer(ZObject o, Vector2 mp)
        {
            if (o.Kind == ZKind.Building)
            {
                // Once measured, an entirely front-facing model has a valid MeshRect contained in these bounds.
                // HUD readers can still request the exact silhouette on demand.
                if (!meshBox.TryGetValue(o.Id, out var building) || !building.t) return false;
                var matrix = building.t.localToWorldMatrix;
                var e = building.local.extents;
                return OffScreenBox(matrix.MultiplyPoint3x4(building.local.center), (Vector3)matrix.GetColumn(0) * e.x,
                    (Vector3)matrix.GetColumn(1) * e.y, (Vector3)matrix.GetColumn(2) * e.z, mp);
            }
            if (o.IsShuttle) return false;
            if (o.Kind == ZKind.Robot && ExitPickOn && BoardingView.ExitFigureState(o, out _) != 0) return false;   // getting out of a carrier (ExitPick): asked as before
            if (o.IsUnit && game.Views.TryGetValue(o.Id, out var uv) && uv && uv.gameObject.activeInHierarchy)
            {
                if (o.Kind != ZKind.Robot)
                {   // a vehicle or gun: its model box (MeshRect), the same corners ScreenRect projects
                    if (!meshBox.TryGetValue(o.Id, out var mb) || !mb.t) return false;                        // not measured yet: measured on this frame, as before
                    Matrix4x4 l2w = mb.t.localToWorldMatrix; Vector3 e = mb.local.extents;
                    return OffScreenBox(l2w.MultiplyPoint3x4(mb.local.center), (Vector3)l2w.GetColumn(0) * e.x, (Vector3)l2w.GetColumn(1) * e.y, (Vector3)l2w.GetColumn(2) * e.z, mp);
                }
                float tall = OzHeroSquadView.Tall(o), rad = 0.6f * tall, half = 0.925f * tall;               // ScreenRect's robot box: 0.6 m round, 1.85 m tall (a hero's size)
                var p = uv.transform.position;
                return OffScreenBox(new Vector3(p.x, p.y + half, p.z), new Vector3(rad, 0f, 0f), new Vector3(0f, half, 0f), new Vector3(0f, 0f, rad), mp);
            }
            if (o.IsUnit || o.IsFortDefender || o.Kind == ZKind.Cannon) return false;                    // no drawn model: the footprint code with its tower / roof heights, asked as before
            if (o.Type == ZWorld.DetailPropType && DetailPropHub.OfSim(o.Id) is DetailProp dp && dp && !dp.Broken)
            {   // DV67: where the prop is drawn, its own size
                float r = Mathf.Max(0.5f, dp.hitRadius), half = Mathf.Max(1f, dp.hitHeight) * 0.5f; var f = dp.Foot;
                return OffScreenBox(new Vector3(f.x, f.y + half, f.z), new Vector3(r, 0f, 0f), new Vector3(0f, half, 0f), new Vector3(0f, 0f, r), mp);
            }
            if (!groundOk) return false;
            // the footprint path: the footprint from the lowest ground GroundY can give to the highest plus the tallest item box
            var a = ZGame.PxToWorld(o.PosX, o.PosY); var b = ZGame.PxToWorld(o.PosX + o.WidthPix, o.PosY + o.HeightPix);
            float top = groundHi + ItemTopM, ey = (top - groundLo) * 0.5f;
            var center = new Vector3((a.x + b.x) * 0.5f, groundLo + ey, (a.z + b.z) * 0.5f);
            float ex = Mathf.Abs(b.x - a.x) * 0.5f, ez = Mathf.Abs(b.z - a.z) * 0.5f;
            return FootprintOutside(center, ex, ey, ez);
        }

        bool FootprintOutside(Vector3 center, float ex, float ey, float ez)
        {
            // ScreenRect ignores negative-depth corners on the footprint path. A wholly hidden box is empty.
            float depth = Lin(farD, center), extent = AxisSpan(farD, ex, ey, ez);
            if (depth + extent < -NearM) return true;
            if (!(depth - extent > NearM) || !(Lin(farW, center) - AxisSpan(farW, ex, ey, ez) > NearM)) return false;
            return Outside(pointerLeft) || Outside(pointerRight) || Outside(pointerBottom) || Outside(pointerTop);

            bool Outside(Vector4 plane) => Lin(plane, center) - AxisSpan(plane, ex, ey, ez) > 0.001f;
        }

        static float AxisSpan(Vector4 row, float ex, float ey, float ez) =>
            Mathf.Abs(row.x) * ex + Mathf.Abs(row.y) * ey + Mathf.Abs(row.z) * ez;

        /// <summary>The box c + s0 u0 + s1 u1 + s2 u2 (|s| at most 1): true when it lies wholly in front of the camera and the pointer is outside
        /// its screen reach (interval arithmetic on the projection, so every point of the box projects inside the reach) widened by the
        /// margin. Any NaN or a box near or behind the camera: false (asked).</summary>
        bool OffScreenBox(Vector3 c, Vector3 u0, Vector3 u1, Vector3 u2, Vector2 mp)
        {
            float w = Lin(farW, c), aw = Span(farW, u0, u1, u2), d = Lin(farD, c), ad = Span(farD, u0, u1, u2);
            float wl = w - aw, wh = w + aw;
            if (!(wl > NearM) || !(d - ad > NearM)) return false;
            float x = Lin(farX, c), ax = Span(farX, u0, u1, u2), y = Lin(farY, c), ay = Span(farY, u0, u1, u2);
            float xl = x - ax, xh = x + ax, yl = y - ay, yh = y + ay;
            float nxl = xl >= 0f ? xl / wh : xl / wl, nxh = xh >= 0f ? xh / wl : xh / wh;                 // the ndc range over the box (w > 0 on all of it)
            float nyl = yl >= 0f ? yl / wh : yl / wl, nyh = yh >= 0f ? yh / wl : yh / wh;
            float sxl = farPx + (nxl + 1f) * 0.5f * farPw, sxh = farPx + (nxh + 1f) * 0.5f * farPw;
            float syl = farPy + (nyl + 1f) * 0.5f * farPh, syh = farPy + (nyh + 1f) * 0.5f * farPh;
            float mx = farMargin + RoundRel * Mathf.Max(Mathf.Abs(sxl), Mathf.Abs(sxh)), my = farMargin + RoundRel * Mathf.Max(Mathf.Abs(syl), Mathf.Abs(syh));   // float rounding grows with the size of the numbers (a box just in front of the camera projects far off the screen)
            return mp.x < sxl - mx || mp.x > sxh + mx || mp.y < syl - my || mp.y > syh + my;
        }

        static float Lin(Vector4 r, Vector3 p) => r.x * p.x + r.y * p.y + r.z * p.z + r.w;
        static float Span(Vector4 r, Vector3 u0, Vector3 u1, Vector3 u2) =>
            Mathf.Abs(r.x * u0.x + r.y * u0.y + r.z * u0.z) + Mathf.Abs(r.x * u1.x + r.y * u1.y + r.z * u1.z) + Mathf.Abs(r.x * u2.x + r.y * u2.y + r.z * u2.z);

        /// <summary>The check (View/OzHoverPreCullCheck.cs): the hover at a screen point with the pre-cull as given, the frame's building
        /// silhouettes worked out again first (the check moves the camera inside a frame).</summary>
        internal ZObject HoverProbe(Vector2 mp, bool preCull)
        {
            ptsRect.Clear();
            bool was = HoverPreCull; HoverPreCull = preCull;
            try { return HoverAt(mp); } finally { HoverPreCull = was; }
        }

        /// <summary>The check: the walk ground's height range now (false: none, map items are never skipped).</summary>
        internal bool ProofGroundRange(out float lo, out float hi) { lo = groundLo; hi = groundHi; return groundOk; }
    }
}
