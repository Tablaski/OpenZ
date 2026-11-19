// OpenZ (GPL-3.0) - version 1.3: metal heated by fire, on every level. View only: reads the sim, writes nothing to it, uses no random number.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A robot, vehicle or gun that stays in fire takes the look of a robot wading in lava: TeamLit's own leg ramp, up from the ground line, a bit weaker, easing up and cooling
    /// slowly (OzFireHeatCurve). Glow only: no smoke, no sound, no light. OpenZPyroFire owns one of these, says what is in fire (OpenZPyroFire.Heat.cs) and steps it after the lava.
    /// <list type="bullet">
    /// <item>The floats are the lava legs' own: _OZLegHeat, _OZLegLine, _OZLegTop. A negative _OZLegTop says the band stands on the unit's own ground line and not on the lava
    /// (Shaders/TeamLit.shader), and lava's own write turns it back by itself. _OZHeat is never used: it reddens the whole body.</item>
    /// <item>On a level with no lava the colour ramp is all zero, so the first write of a level puts it in force (OzLava.PushFireRamp). A level with no fire never pays.</item>
    /// <item>Fixed entries, reused lists, one block that is only ever Get then Set: Get replaces all the block holds, so nothing crosses to the next unit or the other team's paint.
    /// A cold unit keeps its blocks: 0, 0, 0 in them, never a null block, as a slot's block may hold what TeamPaint gave it since.</item>
    /// <item>Every path that copies a block to a new piece finds it cold: a death goes through <see cref="Death"/> before the copies, a piece copied from a living unit
    /// goes through <see cref="ColdCopy"/>. A robot's body then cools in a second on its frozen line, a vehicle or a gun is cold under the blast.</item>
    /// <item>Lava and fire: the stronger stays, never a sum. On a lava level a hot robot is written every frame after OzLavaWade, as that rewrites its skins whenever it wades.</item>
    /// </list>
    /// Buildings, forts, wrecks and the shuttle in the air are not part of this, and nothing in water starts or keeps a glow.
    /// </summary>
    public sealed class OzFireHeat
    {
        /// <summary>Units hot at once, renderers on one unit, the count above which each writes every second frame. Past MaxHot a unit shows no heat (counted).</summary>
        public const int MaxHot = 64, MaxParts = 40, ManyHot = 24;
        /// <summary>_OZLegHeat at full heat: the first numbers to set on real frames. Below 0.5 there is no glow left, only darkened steel.</summary>
        public const float PeakRobot = 0.90f, PeakMachine = 0.85f;
        /// <summary>A robot's band at full heat as a share of its measured height (the height as OzLavaWade.Bind reads it).</summary>
        public const float RobotBand = 0.28f;
        /// <summary>One flame arrival counts as in fire for this long (s, at game speed 1 or more): arrivals come about every 0.22 s and three in ten miss.</summary>
        public const float FlameGap = 0.6f;
        /// <summary>Frames a unit counts as on burning ground after the scan last found it there: the scan sees a unit every fourth frame.</summary>
        public const int GroundHold = 4;

        /// <summary>Band at full heat in metres at 100 % scale. Metres by type, not a share: a gun's height holds its barrel. Glow ends at about 0.85 of it, soot at 1.7.</summary>
        public static float BandOf(string type)
        {
            switch (type)
            {
                case "jeep": case "gatling": return 0.55f;
                case "light": case ZMTank.Type: case ZWorld.ShuttleType: case "missile_cannon": return 0.60f;
                case "heavy": return 0.65f;
                case OzBigTanks.Mammoth: return 0.75f; case OzBigTanks.Bizon: return 0.80f;   // 1.5 DV180 / DV181
                case "medium": return 1.10f;                                                       // the mech: feet to thighs
                case "missile_launcher": case "apc": case "crane": return 0.70f;
                case "howitzer": return 0.45f;
                case "gun": return 0.40f;
                default: return 0.60f;
            }
        }

        /// <summary>Counted by the checks: steps run, units bound, refused past MaxHot, renderers past MaxParts, low renderers off TeamLit, ramp pushes.</summary>
        public static int Steps, Binds, Overflow, RendererOverflow, NotTeamLit, RampPushes;

        static readonly int LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegLineId = Shader.PropertyToID("_OZLegLine"), LegTopId = Shader.PropertyToID("_OZLegTop");
        static MaterialPropertyBlock copyBlock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Steps = Binds = Overflow = RendererOverflow = NotTeamLit = RampPushes = 0; copyBlock = null; }

        sealed class Entry
        {
            public UnitView v; public Transform t; public int id, at, n, groundFrame;
            public float soak, heat, peak, band, flameAt, deadLine, lastShown, lastLine, lastTop;
            public bool robot, bound, wrote, dead;
            public readonly Renderer[] rs = new Renderer[MaxParts];
            public readonly int[] slots = new int[MaxParts], has = new int[MaxParts];              // has: a bit for each slot whose material has the leg floats
            public readonly bool[] skin = new bool[MaxParts];
        }

        readonly Entry[] all = new Entry[MaxHot];                                                  // [0, count) are in use, the rest wait
        readonly Dictionary<int, Entry> byId = new Dictionary<int, Entry>(MaxHot);
        readonly List<Renderer> parts = new List<Renderer>(64);
        readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>(8);
        readonly List<Material> mats = new List<Material>(8);
        MaterialPropertyBlock block;                                                               // Unity refuses a block in a field initializer of a component's constructor path: Boot makes it
        int count;
        bool rampDone;

        /// <summary>Units held now, hot or soaking.</summary>
        public int Count => count;

        public void Boot()
        {
            if (block != null) return;
            block = new MaterialPropertyBlock();
            for (int i = 0; i < all.Length; i++) all[i] = new Entry();
        }

        /// <summary>Can this be heated at all. Only reads the sim.</summary>
        public static bool Wanted(ZObject o) =>
            o != null && (o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon)
            && !o.IsTowerGun && !o.IsFortDefender && !o.ShuttleAirborne && !o.Destroyed && o.KillAt < 0;

        // ------------------------------------------------------------------ what the fire says
        /// <summary>A flame reached this unit now.</summary>
        public void Flame(UnitView v, float now) { var e = Hold(v); if (e != null) e.flameAt = now; }

        /// <summary>The scan found this unit on burning ground this frame.</summary>
        public void OnGround(UnitView v, int frame) { var e = Hold(v); if (e != null) e.groundFrame = frame; }

        Entry Hold(UnitView v)
        {
            if (block == null || !v || v.Dying || !Wanted(v.Obj)) return null;
            int id = v.Obj.Id;
            if (byId.TryGetValue(id, out var e))
            {
                if (e.v == v) return e;
                Cold(e); Free(e);                                                                  // a new view on the same id
            }
            if (count == MaxHot) { Overflow++; return null; }
            e = all[count]; e.at = count; count++;
            byId[id] = e;
            e.v = v; e.t = v.transform; e.id = id; e.n = 0; e.robot = v.Obj.Kind == ZKind.Robot; e.peak = e.robot ? PeakRobot : PeakMachine;
            e.soak = e.heat = e.band = 0f; e.flameAt = e.lastShown = -100f; e.groundFrame = -100; e.bound = e.wrote = e.dead = false;
            return e;
        }

        void Free(Entry e)
        {
            byId.Remove(e.id);
            for (int i = 0; i < e.n; i++) e.rs[i] = null;
            e.n = 0; e.v = null; e.t = null;
            int last = --count;
            if (e.at != last) { var m = all[last]; all[last] = e; all[e.at] = m; m.at = e.at; e.at = last; }
        }

        // ------------------------------------------------------------------ every frame the fire is alive, after the lava and the placement
        public void Step(float dt, float now, int frame, float gameSpeed, bool levelHasLava)
        {
            Steps++;
            float gap = FlameGap / Mathf.Clamp(gameSpeed, 0.25f, 1f);
            var wade = OzLavaWade.I; bool lavaHere = wade;                                         // lava kit booted: only on a level with lava
            bool many = count > ManyHot;
            for (int i = count - 1; i >= 0; i--)
            {
                var e = all[i]; var v = e.v;
                if (!v || !v.isActiveAndEnabled) { Cold(e); Free(e); continue; }                   // removed, popped, boarding: its view may come back and starts cold
                var o = v.Obj;
                bool dead = o == null || o.Destroyed || o.KillAt >= 0 || v.Dying;
                if (dead && !e.dead) { e.dead = true; e.deadLine = e.t.position.y; }
                bool can = !dead && Wanted(o);                                                     // alive and not wanted (a shuttle taking off): out of fire, it cools as usual
                bool wet = can && OpenZPyroFire.InWater(e.t.position);
                bool inFire = can && !wet && (now - e.flameAt <= gap || frame - e.groundFrame <= GroundHold);
                bool sticky = can && !wet && OpenZPyroFire.FullBurning(e.id);
                OzFireHeatCurve.Step(ref e.soak, ref e.heat, dt, inFire, sticky, wet, dead);

                bool contested = lavaHere && e.robot;
                float lava = contested ? OzFireHeatCurve.Smooth(wade.LegHeatOf(v)) : 0f;
                if (e.heat <= 0f)
                {
                    if (inFire || e.soak > 0f) continue;
                    if (e.wrote)
                    {
                        if (lava > 0f) Write(e, 0f, 0f, 0f, true);                                 // no cold over lava: the skins are the lava's, its own last write ends them
                        else Cold(e);
                    }
                    Free(e);
                    continue;
                }
                if (!e.bound) Bind(e);
                if (!rampDone) { rampDone = true; if (!levelHasLava) { OzLava.PushFireRamp(); RampPushes++; } }
                float shown = OzFireHeatCurve.Shown(e.heat, e.peak), line = e.dead ? e.deadLine : e.t.position.y;
                float top = OzFireHeatCurve.Band(e.heat, e.band * OzVehicleScaleTest.Of(v));
                if (!OzFireHeatCurve.FireWins(shown, lava))
                {   // lava wins: its floats stay on the skins it writes; the parts it does not write go on following the fire
                    e.lastShown = -1f;
                    Write(e, shown, line, -top, true);
                    continue;
                }
                if (!contested)
                {
                    bool atPeak = e.heat >= 1f && e.lastShown != shown;                            // the way to full heat always ends on the exact value
                    if (!atPeak && Mathf.Abs(shown - e.lastShown) < 0.004f && Mathf.Abs(line - e.lastLine) < 0.01f && Mathf.Abs(top - e.lastTop) < 0.01f) continue;
                    if (!atPeak && many && ((frame + i) & 1) != 0) continue;
                }
                Write(e, shown, line, -top, false);
                e.lastShown = shown; e.lastLine = line; e.lastTop = top;
            }
        }

        /// <summary>From UnitView.Die and Squash, before anything copies a block. A robot's body goes on and cools from here, the step writes it again this same frame, so it does not blink;
        /// a vehicle or a gun is cold now and done with, its wreck is not part of this.</summary>
        public void Death(UnitView v)
        {
            if (count == 0 || !v || v.Obj == null || !byId.TryGetValue(v.Obj.Id, out var e) || e.v != v) return;
            Cold(e);
            if (!e.robot) { Free(e); return; }
            if (!e.dead) { e.dead = true; e.deadLine = e.t.position.y; }
            e.lastShown = -1f;
        }

        /// <summary>Level reset, the fire switched off: all cold, all free.</summary>
        public void Clear() { while (count > 0) { var e = all[count - 1]; Cold(e); Free(e); } }

        /// <summary>A new piece that was given a copy of a hot block: it is cold now. Only fire's own mark (a negative band) is touched: what lava or a melt wrote stays theirs.</summary>
        public static void ColdCopy(Renderer to, int slots)
        {
            if (!to) return;
            copyBlock ??= new MaterialPropertyBlock();
            to.GetPropertyBlock(copyBlock);
            if (IsFire(copyBlock)) { Zero(copyBlock); to.SetPropertyBlock(copyBlock); }
            for (int s = 0; s < slots; s++)
            {
                to.GetPropertyBlock(copyBlock, s);
                if (IsFire(copyBlock)) { Zero(copyBlock); to.SetPropertyBlock(copyBlock, s); }
            }
        }

        static bool IsFire(MaterialPropertyBlock b) => !b.isEmpty && b.HasFloat(LegTopId) && b.GetFloat(LegTopId) < 0f;
        static void Zero(MaterialPropertyBlock b) { b.SetFloat(LegHeatId, 0f); b.SetFloat(LegLineId, 0f); b.SetFloat(LegTopId, 0f); }

        // ------------------------------------------------------------------ the renderers
        /// <summary>At the first write, never at first contact, so a lick costs nothing. What it allocates (names, the first look at a paint) is per bind, not per frame or shot.</summary>
        void Bind(Entry e)
        {
            e.bound = true; Binds++;
            var v = e.v; float y0 = e.t.position.y;
            e.band = e.robot ? RobotBand * Height(v, y0) : BandOf(v.Obj.Type);
            float reach = 1.7f * e.band + 0.25f;
            var paint = v.GetComponent<TeamPaint>();
            v.GetComponentsInChildren<Renderer>(false, parts);
            for (int i = 0; i < parts.Count; i++)
            {
                var r = parts[i];
                if (!Draws(r)) continue;
                var host = r.GetComponentInParent<TeamPaint>(true);                                // a figure riding on it keeps its own paint and stays cold: TeamPaint.Apply's rule
                var owner = host ? host : paint;
                if (owner && owner.IsRobotBody && host != paint && !(paint && paint.IsRobotBody)) continue;
                if (r.bounds.min.y - y0 > reach) continue;                                         // turrets, racks, hand guns: never reach the band
                r.GetSharedMaterials(mats);
                int has = 0;
                for (int s = 0; s < mats.Count && s < 32; s++) if (mats[s] && mats[s].HasProperty(LegHeatId)) has |= 1 << s;
                if (has == 0) { NotTeamLit++; continue; }
                if (e.n == MaxParts) { RendererOverflow++; continue; }
                e.rs[e.n] = r; e.slots[e.n] = Mathf.Min(mats.Count, 32); e.has[e.n] = has; e.skin[e.n] = r is SkinnedMeshRenderer; e.n++;
            }
            parts.Clear(); mats.Clear();
        }

        static bool Draws(Renderer r)
        {
            if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer) return false;
            var m = r.sharedMaterial; if (m && m.renderQueue >= 2450) return false;                // light cones, glows, decals
            var n = r.name; return n.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>A robot's height as OzLavaWade.Bind measures it.</summary>
        float Height(UnitView v, float y0)
        {
            float top = float.NegativeInfinity;
            v.GetComponentsInChildren<SkinnedMeshRenderer>(false, skins);
            for (int i = 0; i < skins.Count; i++) if (skins[i].enabled) top = Mathf.Max(top, skins[i].bounds.max.y);
            skins.Clear();
            if (float.IsInfinity(top))
            {
                v.GetComponentsInChildren<Renderer>(false, parts);
                for (int i = 0; i < parts.Count; i++) if (Draws(parts[i])) top = Mathf.Max(top, parts[i].bounds.max.y);
                parts.Clear();
            }
            float h = top - y0;
            return float.IsInfinity(h) || float.IsNaN(h) ? OzLavaWade.FallbackHeight : Mathf.Clamp(h, 1.2f, 4.5f);
        }

        /// <summary>A renderer block, if there is one, goes on serving every slot with no block of its own; a slot's own block is the one drawn, so each that has one is written too.
        /// With no renderer block each slot takes a block of its own, as the legs in lava do. Only slots whose material has the floats.</summary>
        void Write(Entry e, float h, float line, float top, bool plainOnly)
        {
            for (int i = 0; i < e.n; i++)
            {
                var r = e.rs[i]; if (!r || (plainOnly && e.skin[i])) continue;
                r.GetPropertyBlock(block);
                bool wide = !block.isEmpty;
                if (wide) { Put(h, line, top); r.SetPropertyBlock(block); }
                int has = e.has[i], n = e.slots[i];
                for (int s = 0; s < n; s++)
                {
                    if ((has & (1 << s)) == 0) continue;
                    r.GetPropertyBlock(block, s);
                    if (wide && block.isEmpty) continue;
                    Put(h, line, top);
                    r.SetPropertyBlock(block, s);
                }
            }
            e.wrote = true;
        }

        void Put(float h, float line, float top) { block.SetFloat(LegHeatId, h); block.SetFloat(LegLineId, line); block.SetFloat(LegTopId, top); }

        void Cold(Entry e) { if (e.wrote) { Write(e, 0f, 0f, 0f, false); e.wrote = false; } }

        // ------------------------------------------------------------------ checks
        /// <summary>Check: this unit's heat, soak and listed renderers. False = not held.</summary>
        public bool Probe(UnitView v, out float heat, out float soak, out int listed)
        {
            heat = soak = 0f; listed = 0;
            if (!v || v.Obj == null || !byId.TryGetValue(v.Obj.Id, out var e) || e.v != v) return false;
            heat = e.heat; soak = e.soak; listed = e.n; return true;
        }

        /// <summary>Check: true when this renderer is listed by a held unit.</summary>
        public bool Lists(Renderer r)
        {
            for (int i = 0; i < count; i++) { var e = all[i]; for (int k = 0; k < e.n; k++) if (ReferenceEquals(e.rs[k], r)) return true; }
            return false;
        }

        /// <summary>Check: the renderers listed for this unit.</summary>
        public void Parts(UnitView v, List<Renderer> into)
        {
            into.Clear();
            if (!v || v.Obj == null || !byId.TryGetValue(v.Obj.Id, out var e) || e.v != v) return;
            for (int k = 0; k < e.n; k++) into.Add(e.rs[k]);
        }
    }
}
