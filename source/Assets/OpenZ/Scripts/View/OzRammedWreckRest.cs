// OpenZ (GPL-3.0)
// nothing here is read by the rules.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A vehicle wreck that a Mammoth rammed, or that a big tank's wreck shove (View/OzBigTankWreckShove.cs) moved, lies ON the ground under it and
    /// then stays still; the lights of every dead vehicle go out with it and none is left in the air.
    /// <list type="bullet">
    /// <item>REST (N30): every drawn frame the lowest points of the hull still drawn on the wreck (the real mesh points; lamps, glows, shadows and
    /// labels left out) are measured against the solid ground right under each of them (the walk layer seen from just above the point: terrain,
    /// bridge decks, the Unit editor floor and its platforms' decks; the level ground). Higher than <see cref="TolM"/>: it falls at 1 g until it
    /// lies (a thump when it lands hard, on screen only); in the ground: it is lifted out. Water, lava, the end-of-life sink and a body that
    /// PhysX still flies are left to their own rules. A wreck with no hull left on it (broken into pieces: the pieces are physics bodies on
    /// the ground) has nothing to rest.</item>
    /// <item>STILL (N30): once it lies, only the big tanks' shove moves it again (and the water / lava / sink rules): a pose change from anything
    /// else is put back on the same frame (this runs last of the wreck's drawing, order 1100); a shove's new place is taken and the wreck is
    /// laid on the ground there again. So it never spins or jitters on its own.</item>
    /// <item>LIGHTS (N31): on the frame its death is drawn every real light of the vehicle (and of its thrown turret) goes off, and its
    /// transparent lamp glows (lamp / beacon / neon / glow / halo / cone / beam / pool / lens / bulb parts); the solid lamp housings stay hull
    /// parts and fly with their pieces. For <see cref="WatchLightsS"/> s after it, four times a second, any lamp part, light or big-tank lamp
    /// holder (OzBigTanksFx: code-built, not children of the tank) that is still shown but has no solid part of the wreck within
    /// <see cref="HoverM"/> (the hull went to pieces and left it where it was) breaks off: switched off with a small spark.</item>
    /// </list>
    /// Switches (off = exactly as before): <see cref="RestOn"/>, <see cref="SteadyShove"/> (read by OzBigTankWreckShove), <see cref="LightsOff"/>.
    /// </summary>
    [DefaultExecutionOrder(1100)]   // after UnitView (60) placed the wreck, the shove (75) and the ram flip (OzRamLever, 1000): the last word on its pose
    public sealed class OzRammedWreckRest : MonoBehaviour
    {
        /// <summary>N30: the rammed / shoved wreck lies on the ground and stays still.</summary>
        public static bool RestOn = true;
        /// <summary>N30: the wreck shove pushes only while the big tank really drives into the wreck, turns it one way, and never adds its offset again.</summary>
        public static bool SteadyShove = true;
        /// <summary>N31: a dead vehicle's lights go off at its death; no lamp part is left hovering.</summary>
        public static bool LightsOff = true;
        /// <summary>Lying = its lowest hull point within this of the ground (m); a lamp part farther than HoverM from every solid part hovers (m);
        /// the lamps are watched this long after the death (s); the ground is looked for from this far above a point (m).</summary>
        public static float TolM = 0.05f, HoverM = 0.6f, WatchLightsS = 20f, AboveM = 1.5f;

        /// <summary>Proof counters.</summary>
        public static int Watched, Rests, Falls, Lifts, Thumps, HeldStill, ShovesTaken, LightsOut, GlowsOut, HoverOut, HoldersOut, Sparks;
        public static float MaxFallM, MaxLiftM, MaxHeldDeg, MaxHeldM;
        public static string Last = "-";

        static readonly Dictionary<int, OzRammedWreckRest> live = new Dictionary<int, OzRammedWreckRest>();
        static readonly RaycastHit[] hits = new RaycastHit[8];
        static readonly List<Renderer> rbuf = new List<Renderer>();
        static readonly List<Light> lbuf = new List<Light>();

        UnitView view; Transform root; int id; bool rest; float born;
        readonly List<Vector3> pts = new List<Vector3>(); int sig = int.MinValue; float sigAt = -1f;
        bool falling, held; float vy, fallFrom, recheckAt, lightsAt; int shovedFrame = -1, sparksHere;
        Vector3 heldPos; Quaternion heldRot;
        bool ground; float carryVy, spin, slideV, tipVy; Vector3 spinAxis; int retip; readonly OzWreckGround.HullMeasure gm = new OzWreckGround.HullMeasure();
        readonly List<Bounds> solid = new List<Bounds>();
        float[] lowY = new float[24]; Vector3[] lowP = new Vector3[24];

        /// <summary>The wreck rests (rammed or shoved) or only its lamps are watched.</summary>
        public bool Resting => rest;
        /// <summary>Checks: the gap of its lowest hull point to the ground under it now (m; NaN = no hull left on it).</summary>
        public float GapNow() { Points(true); return pts.Count == 0 ? float.NaN : Gap(); }

        // ------------------------------------------------------------------ entry points
        /// <summary>UnitView.BeginWreck (its last line): a vehicle's death is drawn.</summary>
        public static void AtDeath(UnitView v)
        {
            if (!v || v.Obj == null || v.Obj.Kind != ZKind.Vehicle) return;
            if (LightsOff) { LampsOff(v.transform, v); LampsOff(v.RamRestTossed, v); }
            bool rammed = OzMammothRamView.Rammed(v.Obj);
            bool every = OzWreckGround.HullFor(v); if (every) OzWreckGround.HullsWatched++;
            if ((RestOn && rammed) || LightsOff || every) { var r = Of(v, RestOn && rammed); if (r && every) r.ground = true; }
        }

        /// <summary>OzBigTankWreckShove: it moved this wreck this frame (its new place is taken; it is laid on the ground there).</summary>
        public static void Shoved(UnitView v)
        {
            if (!RestOn || !v) return;
            var r = Of(v, true); if (r) r.shovedFrame = Time.frameCount;
        }

        /// <summary>1.5 kit wreck_rest_ground_r6: WreckContact froze this flipped hull (its body stops wherever it is): from here it rests only on real support,
        /// falling on from the speed its body had when it is in the air (View/OzWreckGround.cs).</summary>
        public static void Ground(UnitView v, float fallSpeed)
        {
            if (!OzWreckGround.HullFor(v)) return;
            var r = Of(v, false); if (!r) return;
            r.ground = true; r.held = false; r.recheckAt = 0f; r.carryVy = Mathf.Max(0f, fallSpeed);
        }

        /// <summary>The component of this dead view (made when missing); rest = it also rests and stays still.</summary>
        public static OzRammedWreckRest Of(UnitView v, bool rest)
        {
            if (!v || v.Obj == null) return null;
            int key = v.GetInstanceID();
            if (!live.TryGetValue(key, out var r) || !r)
            {
                r = v.gameObject.AddComponent<OzRammedWreckRest>();
                r.view = v; r.root = v.transform; r.id = v.Obj.Id; r.born = Time.time; r.lightsAt = Time.time + 0.05f;
                live[key] = r; Watched++;
            }
            if (rest && !r.rest) { r.rest = true; r.held = false; r.recheckAt = 0f; }
            return r;
        }

        public static OzRammedWreckRest Find(UnitView v) => v && live.TryGetValue(v.GetInstanceID(), out var r) && r ? r : null;

        void OnDestroy() { if (view && live.TryGetValue(view.GetInstanceID(), out var r) && r == this) live.Remove(view.GetInstanceID()); }

        void Done() { enabled = false; Destroy(this); }

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!view || !view.RamRestWreck) { Done(); return; }                                   // gone, hidden or sinking away at its end
            float now = Time.time, dt = Time.deltaTime;
            if (LightsOff && now >= lightsAt && now - born <= WatchLightsS) { lightsAt = now + 0.25f; Hover(); }
            if (!(rest && RestOn) && !(ground && OzWreckGround.HullsOn())) { if (now - born > WatchLightsS) Done(); return; }   // 1.5 kit wreck_rest_ground_r6: also every vehicle wreck (support only)
            if (dt <= 0f) return;
            if (view.RamRestPhysics || OzWaterLanding.InWater(root) || OzLavaMelt.Busy(root) || OzRamLever.Holds(view.Obj) || (!(rest && RestOn) && OzWreckGround.HullWaits(view, root))) { held = false; falling = false; return; }   // 1.5 kit wreck_rest_ground_r6: a support-only hull also waits for a ram turn-over, a crush ride, open water, the rock rule
            bool shoved = shovedFrame == Time.frameCount;
            if (shoved) { ShovesTaken++; held = false; }
            if (held)
            {   // STILL: what anything but the shove moved goes back
                float dm = (root.position - heldPos).magnitude, da = Quaternion.Angle(root.rotation, heldRot);
                if ((dm > 1e-3f || da > 0.05f) && rest && RestOn)   // 1.5 kit wreck_rest_ground_r6: only a rammed or shoved wreck is held still (a support-only hull is only measured again)
                {
                    HeldStill++; MaxHeldM = Mathf.Max(MaxHeldM, dm); MaxHeldDeg = Mathf.Max(MaxHeldDeg, da);
                    root.SetPositionAndRotation(heldPos, heldRot);
                }
                if (now < recheckAt) return;                                                        // the ground under it again twice a second (a platform or deck gone)
            }
            recheckAt = now + (OzWreckGround.On ? OzWreckGround.RecheckS(id) : 0.5f);   // 1.5 kit wreck_rest_ground_r6: about half a second, spread by its id (not every hull on one frame)
            Points(false);
            if (pts.Count == 0) return;                                                             // no hull left on it
            float gap = Gap();
            if (float.IsNaN(gap)) return;
            if (gap > TolM)
            {   // in the air: it falls at 1 g onto what is under it
                if (!falling) { falling = true; vy = carryVy; carryVy = 0f; fallFrom = root.position.y; Falls++; Last = $"{view.Obj.Type} #{id}: {gap:0.00} m over the ground, falls"; if (!(rest && RestOn)) OzWreckGround.HullFell(view, gap); }   // 1.5 kit wreck_rest_ground_r6: a frozen flipped body falls on at the speed it had
                held = false;
                vy += 9.81f * dt;
                float step = Mathf.Min(gap, vy * dt);
                root.position += Vector3.down * step;
                if (spin > 0f) spin = step >= gap - 1e-4f ? 0f : gm.Spin(root, spinAxis, spin, gap - step, vy, dt);   // 1.5 kit wreck_rest_ground_r6 (critic round): a hull that went over an edge turns on as it falls, never close to its support
                if (step >= gap - 1e-4f) Land();
                return;
            }
            if (gap < -TolM && rest && RestOn)   // 1.5 kit wreck_rest_ground_r6: a support-only hull in the ground is left as it was (no lift)
            {   // in the ground: out of it (quickly, no pop)
                held = false;
                float up = Mathf.Min(-gap, Mathf.Max(0.05f, 4f * dt));
                root.position += Vector3.up * up; Lifts++; MaxLiftM = Mathf.Max(MaxLiftM, -gap);
                return;
            }
            if (falling) { Land(); spin = 0f; }   // 1.5 kit wreck_rest_ground_r6: its turn after an edge ends with the landing
            if (held && TipOn && OzWreckGround.On && retip < 3 && !gm.Steady(root, pts)) { held = false; tipT = 0f; tipSpeed = 0f; retip++; OzWreckGround.Retips++; }   // 1.5 kit wreck_rest_ground_r6 (critic round): at every look, its middle no longer over what it stands on (a side's support went): it tips over
            if (!held)
            {
                if (TipOn && Tip(dt)) return;                                                       // 1.5 N30 (Unity pass 12 Nov): standing on a corner or an edge: it tips over first
                if (tipDeg > 10f)
                {   // it came down on its side: dust and a metal thump, on screen only
                    var p = root.position + Vector3.up * 0.3f;
                    if (WreckFx.OnScreen(p, 0.1f)) { Thumps++; FxManager.I?.Dust(p, 0.6f); AudioDirector.Fire("dst_debris_metal", p, id, 0.7f, 0.75f); }
                    Last = $"{view.Obj.Type} #{id}: tipped over {tipDeg:0} deg, lies on its side";
                }
                tipT = 0f; tipDeg = 0f; tipSpeed = 0f;
                held = true; heldPos = root.position; heldRot = root.rotation; Rests++;
                carryVy = 0f; spin = 0f; slideV = 0f; tipVy = 0f; if (OzWreckGround.On && gm.Steady(root, pts)) retip = 0;   // 1.5 kit wreck_rest_ground_r6: it lies (stable: a later look may tip it again)
            }
        }

        // ------------------------------------------------------------------ 1.5 N30 (Unity pass 12 Nov): a hull that stands on a corner tips over
        /// <summary>
        /// ground, its bulk in the air beside it) tips over under its own weight, about the edge it stands on, until its middle is over what it lies on.
        /// Off = held at the landing pose as before.</summary>
        public static bool TipOn = true;
        public static int Tips; public static float MaxTipDeg;
        static readonly List<Vector3> tipPts = new List<Vector3>(), tipCon = new List<Vector3>();
        float tipSpeed, tipT, tipDeg; Vector3 tipDir; bool tipCounted;

        /// <summary>One step of the tip-over; false = it lies stable (or its 4 s are over): it is held there.</summary>
        bool Tip(float dt)
        {
            if (pts.Count < 3 || tipT > OzWreckGround.TipMax(4f)) return false;   // 1.5 kit wreck_rest_ground_r6: up to 6 s (a tall hull on its end needs the time)
            Vector3 com;
            if (OzWreckGround.On) { if (!gm.Contacts(root, pts, tipCon, out com)) return false; } else {   // 1.5 kit wreck_rest_ground_r6 (critic round): what it stands on from its support measure (no ray per point; the old lines below, off)
            var m = root.localToWorldMatrix; com = Vector3.zero; float minY = float.MaxValue;
            tipPts.Clear();
            for (int i = 0; i < pts.Count; i++) { var w = m.MultiplyPoint3x4(pts[i]); tipPts.Add(w); com += w; if (w.y < minY) minY = w.y; }
            com /= pts.Count;
            tipCon.Clear();
            for (int i = 0; i < tipPts.Count && tipCon.Count < 96; i++)
            {   // what it stands on: its points within 6 cm of the ground under them
                var w = tipPts[i]; if (w.y > minY + 0.8f) continue;
                float g = GroundUnder(w); if (g > float.MinValue && w.y - g <= 0.06f) tipCon.Add(w);
            }
            if (tipCon.Count == 0) return false;
            }   // 1.5 kit wreck_rest_ground_r6: (the end of the old measure)
            // the side on which nothing holds it: the direction in which its support ends farthest short of its middle
            float worst = float.MaxValue; Vector3 dir = Vector3.right, pivot = tipCon[0];
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); float s = float.MinValue; Vector3 at = tipCon[0];
                for (int i = 0; i < tipCon.Count; i++) { float v = (tipCon[i].x - com.x) * d.x + (tipCon[i].z - com.z) * d.z; if (v > s) { s = v; at = tipCon[i]; } }
                if (s < worst) { worst = s; dir = d; pivot = at; }
            }
            if (worst >= -0.03f) { tipSpeed = 0f; return false; }                                   // its middle is over what it lies on: stable
            if (tipT > 0f && Vector3.Dot(dir, tipDir) < 0.5f) tipSpeed *= 0.3f;                     // it came down on another edge: most of the turn is lost
            tipDir = dir;
            float R = Mathf.Max(0.3f, (com - pivot).magnitude);
            tipSpeed = Mathf.Min(tipSpeed + 9.81f * -worst / (R * R) * dt, 3.5f) * Mathf.Clamp01(1f - 1.5f * dt);   // rad/s: its weight about the edge, a little lost to the ground
            float deg = Mathf.Max(tipSpeed, OzWreckGround.TipMinRad(0.25f, tipT)) * Mathf.Rad2Deg * dt;   // 1.5 kit wreck_rest_ground_r6: at least 0.8 rad/s after half a second (a quick fall over, never a creep that stops half way)
            root.RotateAround(pivot, Vector3.Cross(Vector3.up, dir), deg);
            if (OzWreckGround.On) gm.Slide(root, com, pivot, dir, ref slideV, dt);   // 1.5 kit wreck_rest_ground_r6 (critic round): over a drop (a deck or platform edge, a rock) it slides off the edge as it turns
            tipT += dt; tipDeg += deg;
            float gap = Gap(); if (!float.IsNaN(gap)) { if (OzWreckGround.On && gap > TolM) { falling = true; vy = 0f; fallFrom = root.position.y; Falls++; OzWreckGround.EdgeFalls++; OzWreckGround.HullFell(view, gap); spin = Mathf.Max(tipSpeed, OzWreckGround.TipMinRad(0.25f, tipT)); spinAxis = Vector3.Cross(Vector3.up, dir); tipVy = 0f; } else if (OzWreckGround.On && gap > 0f) { tipVy += 9.81f * dt; root.position += Vector3.down * Mathf.Min(gap, tipVy * dt + 0.004f); } else { tipVy = 0f; root.position += Vector3.down * gap; } }   /* 1.5 kit wreck_rest_ground_r6 (critic round): over a lower support the 1 g fall takes it from here (it turns on), never a jump down; down at most a 1 g step */       // it stays on the ground while it turns
            if (!tipCounted) { tipCounted = true; Tips++; }
            MaxTipDeg = Mathf.Max(MaxTipDeg, tipDeg);
            return true;
        }

        void Land()
        {
            float drop = fallFrom - root.position.y; MaxFallM = Mathf.Max(MaxFallM, drop);
            falling = false;
            if (vy > 2f)
            {   // a hard landing: dust and a metal thump, on screen only
                var p = root.position + Vector3.up * 0.3f;
                if (WreckFx.OnScreen(p, 0.1f))
                {
                    Thumps++;
                    FxManager.I?.Dust(p, Mathf.Clamp(vy / 6f, 0.4f, 1.1f));
                    AudioDirector.Fire("dst_debris_metal", p, id, Mathf.Clamp(vy / 6f, 0.5f, 1f), 0.8f);
                }
            }
            vy = 0f;
            if (TipOn) { held = false; tipT = 0f; tipSpeed = 0f; }                                  // 1.5 N30 (Unity pass 12 Nov): held only once it lies stable (Tip)
            else { held = true; heldPos = root.position; heldRot = root.rotation; Rests++; }
            Last = $"{view.Obj.Type} #{id}: lies on the ground after a {drop:0.00} m fall";
        }

        // ------------------------------------------------------------------ the hull's points and the ground
        static bool Word(string n)
        {
            n = n.ToLowerInvariant();
            return n.Contains("lamp") || n.Contains("beacon") || n.Contains("neon") || n.Contains("glow") || n.Contains("halo") || n.Contains("flare")
                || n.Contains("lens") || n.Contains("bulb") || n.Contains("beam") || n.Contains("cone") || n.Contains("searchlight") || n.Contains("headlight")
                || n.Contains("spotlight") || n.Contains("taillight") || n.Contains("chest_light") || n.Contains("tail_light") || n.Contains("head_light")
                || n.Contains("lightcone") || n.Contains("light_cone") || n.StartsWith("vl_") || n.Contains("pool");   // never "light" alone: the light tank's own nodes (Z_light, light_tank_v2)
        }

        static bool Quad(string n)
        {
            n = n.ToLowerInvariant();
            return n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("decal") || (OzWreckGround.On && OzWreckGround.FxName(n));   // 1.5 kit wreck_rest_ground_r6: fire, smoke, scorch, dust meshes are no hull
        }

        static bool Lamp(Renderer r) => (r is MeshRenderer || r is SkinnedMeshRenderer) && Word(r.name);
        static bool Glass(Renderer r) => r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450;
        static bool Solid(Renderer r) => r && r.enabled && r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer) && !Glass(r) && !Word(r.name) && !Quad(r.name);

        /// <summary>The hull's points in the root's frame (real mesh points, at most 160 a mesh), rebuilt when the drawn parts change.</summary>
        void Points(bool force)
        {
            float now = Time.time;
            if (!force && now < sigAt && pts.Count > 0) return;
            sigAt = now + 0.25f;
            rbuf.Clear(); root.GetComponentsInChildren(false, rbuf);
            int s = 17;
            foreach (var r in rbuf)
            {
                if (!Solid(r)) continue;
                Mesh m = null; if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh; else if (r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                unchecked { s = s * 31 + r.GetInstanceID(); s = s * 31 + (m ? m.GetInstanceID() : 0); s = s * 31 + r.transform.localPosition.GetHashCode() + r.transform.localRotation.GetHashCode(); }
            }
            if (s == sig && pts.Count > 0) return;
            sig = s; pts.Clear();
            var inv = root.worldToLocalMatrix;
            if (OzWreckGround.On) { OzWreckGround.HullPoints(rbuf, Solid, inv, pts); return; }
            foreach (var r in rbuf)
            {
                if (!Solid(r)) continue;
                Mesh m = null; if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh; else if (r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                var to = inv * r.localToWorldMatrix;
                if (m && m.isReadable && m.vertexCount > 0 && !(r is SkinnedMeshRenderer))
                {
                    var v = m.vertices; int step = Mathf.Max(1, v.Length / 1200);   // Unity pass 12 Nov: was 160 a mesh: the lowest of 160 points lay up to 9 cm over the mesh's real lowest point (the crane hull ended 9 cm in the ground)
                    for (int i = 0; i < v.Length; i += step) pts.Add(to.MultiplyPoint3x4(v[i]));
                }
                else
                {   // no readable points (or skinned): the corners of its drawn box
                    var b = r.bounds;
                    for (int c = 0; c < 8; c++) pts.Add(inv.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                }
                if (pts.Count > 2400) break;
            }
        }

        /// <summary>The lowest hull points (the 24 lowest) against the ground under each: the smallest gap (m).</summary>
        float Gap()
        {
            if (OzWreckGround.On) return gm.Gap(root, pts);   // 1.5 kit wreck_rest_ground_r6 (critic round): the smallest clearance over the support under EVERY point (a slope, a deck edge, a rock under one side), one measure per pose
            int n = 0; var m = root.localToWorldMatrix;
            for (int i = 0; i < pts.Count; i++)
            {
                var w = m.MultiplyPoint3x4(pts[i]);
                if (n < lowY.Length) { lowY[n] = w.y; lowP[n] = w; n++; continue; }
                int hi = 0; for (int k = 1; k < n; k++) if (lowY[k] > lowY[hi]) hi = k;
                if (w.y < lowY[hi]) { lowY[hi] = w.y; lowP[hi] = w; }
            }
            float gap = float.MaxValue;
            for (int k = 0; k < n; k++) { float g = GroundUnder(lowP[k]); if (g > float.MinValue) gap = Mathf.Min(gap, lowP[k].y - g); }
            return gap == float.MaxValue ? float.NaN : gap;
        }

        /// <summary>The highest solid ground under a point, looked for from AboveM over it (the walk layer: terrain, decks, the Unit editor floor and
        /// its platforms; the level's own ground); float.MinValue when there is none.</summary>
        float GroundUnder(Vector3 p)
        {
            float best = float.MinValue;
            if (ZGame.WalkLayer >= 0)
            {
                int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + AboveM, p.z), Vector3.down, hits, AboveM + 80f, 1 << ZGame.WalkLayer, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider; if (c && c.transform.IsChildOf(root)) continue;
                    if (hits[i].point.y > best) best = hits[i].point.y;
                }
            }
            var g = ZGame.I;
            if (g) { float gy = g.GroundY(p.x, p.z); if (gy <= p.y + AboveM && gy > best) best = gy; }
            return best;
        }

        // ------------------------------------------------------------------ N31: the lights
        /// <summary>At the death: the vehicle's real lights and transparent lamp glows under `t` go off (a spark at the first burning one).</summary>
        static void LampsOff(Transform t, UnitView v)
        {
            if (!t) return;
            bool sparked = false;
            lbuf.Clear(); t.GetComponentsInChildren(false, lbuf);
            foreach (var l in lbuf)
            {
                if (!l || !l.enabled) continue;
                if (!sparked && l.intensity > 0.05f && WreckFx.OnScreen(l.transform.position, 0.1f)) { sparked = true; Sparks++; FxManager.I?.Pop(l.transform.position, 0.4f); }
                l.enabled = false; LightsOut++;
            }
            rbuf.Clear(); t.GetComponentsInChildren(false, rbuf);
            foreach (var r in rbuf)
            {
                if (!r || !r.enabled || !Lamp(r) || !Glass(r)) continue;
                r.enabled = false; GlowsOut++;
            }
        }

        /// <summary>Four times a second after the death: a lamp part, light or big-tank lamp holder still shown with no solid part of the wreck
        /// (its hull, its thrown turret) within HoverM is left in the air: it breaks off (off, a small spark).</summary>
        void Hover()
        {
            solid.Clear();
            var tossed = view.RamRestTossed;
            Solids(root); Solids(tossed);
            Cull(root); Cull(tossed);
            var fx = OzBigTanksFx.I; var t = fx && OzBigTanks.Is(view.Obj.Type) ? fx.Of(id) : null;
            if (t != null)
                foreach (var a in new[] { "root", "hull", "turret" })
                {
                    var h = t.Holder(a); if (!h || !h.gameObject.activeSelf) continue;
                    rbuf.Clear(); h.GetComponentsInChildren(false, rbuf);
                    bool any = false; var b = new Bounds();
                    foreach (var r in rbuf) if (r && r.enabled) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                    if (!any || Near(b)) continue;
                    Spark(b.center); h.gameObject.SetActive(false); HoldersOut++;                     // its pads, housings, neon, markers, searchlight, beacon: off with the hull
                    Last = $"{view.Obj.Type} #{id}: big-tank lamp holder '{a}' left in the air: off";
                }
        }

        void Solids(Transform t)
        {
            if (!t) return;
            rbuf.Clear(); t.GetComponentsInChildren(false, rbuf);
            foreach (var r in rbuf) if (Solid(r)) solid.Add(r.bounds);
        }

        void Cull(Transform t)
        {
            if (!t) return;
            rbuf.Clear(); t.GetComponentsInChildren(false, rbuf);
            foreach (var r in rbuf)
            {
                if (!r || !r.enabled || !Lamp(r) || Near(r.bounds)) continue;
                Spark(r.bounds.center); r.enabled = false; HoverOut++;
                Last = $"{view.Obj.Type} #{id}: lamp part {r.name} left in the air: off";
            }
            lbuf.Clear(); t.GetComponentsInChildren(false, lbuf);
            foreach (var l in lbuf)
            {
                if (!l || !l.enabled) continue;
                if (Near(new Bounds(l.transform.position, Vector3.one * 0.1f))) continue;
                l.enabled = false; HoverOut++;
            }
        }

        bool Near(Bounds b)
        {
            foreach (var s in solid)
            {
                var e = s; e.Expand(HoverM * 2f);
                if (e.Intersects(b)) return true;
            }
            return false;
        }

        void Spark(Vector3 at)
        {
            if (sparksHere >= 3 || !WreckFx.OnScreen(at, 0.1f)) return;
            sparksHere++; Sparks++;
            FxManager.I?.Pop(at, 0.35f);
        }

        public static string Status() =>
            $"rammed wreck rest: rest {(RestOn ? "on" : "off")}, steady shove {(SteadyShove ? "on" : "off")}, lights {(LightsOff ? "on" : "off")}; watched {Watched}, " +
            $"rests {Rests}, falls {Falls} (max {MaxFallM:0.00} m), lifts {Lifts} (max {MaxLiftM:0.00} m), thumps {Thumps}, held still {HeldStill} (max {MaxHeldM:0.000} m / {MaxHeldDeg:0.0} deg), " +
            $"shoves taken {ShovesTaken}; lights out {LightsOut}, glows out {GlowsOut}, hovering parts off {HoverOut}, big-tank holders off {HoldersOut}, sparks {Sparks}; tip-overs {(TipOn ? "on" : "off")} {Tips} (max {MaxTipDeg:0} deg); last: {Last}";
    }
}
