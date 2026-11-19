// OpenZ (GPL-3.0)
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 vehicle_colour_fill (view only): a vehicle's or a gun's paint FILLS from its old look to its new one over
    /// <see cref="OzVehicleColourFill.Seconds"/> (ease in-out, Mathf.SmoothStep, like the flag cloth's look fading in, FlagView.Looks.cs), in
    /// both directions: empty grey to the team colour (captured, boarded) and the team colour to the empty grey (driver out, driver sniped),
    /// also team to team. The team itself (<see cref="Team"/>) changes at once, as before: everything that reads it (the checks, the gunner's
    /// paint sync, the shuttle look) sees the new owner in the same frame; only what is drawn moves over.
    /// <para>The whole look moves, not only the colour: TeamLit's owned / neutral switches (_OZTeamVivid.w 1 neutral, 2 owned: the vivid
    /// accents, the livery tint, the flat empty grey, the team panels, the team rim, the night team glow, the heavy branch and the k98b empty
    /// v2 look) read w as a share since this kit (1 + the share owned; exactly as before at 0, 1 and 2), so w goes 1 -> 2 (or 2 -> 1) with the
    /// fill while _OZTeamVivid.rgb / _OZTeamGlow stay the owned side's colour, _TeamColor goes from the old paint to the new, and the empty v2
    /// look (_OZEmptyV1) stays on for the whole fill whenever one side is neutral (TeamLit weights it by the neutral share).</para>
    /// <para>Cost: one bind of the paint's renderers at the start (the same walk an Apply does), then each frame of the fill three values per
    /// material slot through the renderers' own blocks (no allocation); the last frame is a full Apply (the exact new look). At most 0.4 s a
    /// change.</para>
    /// </summary>
    public partial class TeamPaint
    {
        bool fillOn; Team fillFrom; float fillAt; UnitView fillView;
        int fillSeen = -1;                                                                    // the last frame UnitView synced this paint's owner (-1 = never)
        readonly List<Renderer> fillR = new List<Renderer>();
        readonly List<int> fillSlots = new List<int>();
        readonly List<bool> fillRobot = new List<bool>();
        MaterialPropertyBlock fillBlock;
        static readonly List<Renderer> fillAll = new List<Renderer>();
        static readonly List<Material> fillMats = new List<Material>();

        /// <summary>The fill runs now (its share below 1, the view alive, not a wreck).</summary>
        public bool Filling => FillLive();
        /// <summary>The team the fill started from (meaningful while <see cref="Filling"/>).</summary>
        public Team FillFrom => fillFrom;
        /// <summary>The fill's eased share now (0 = the old look, 1 = the new; 1 when no fill runs).</summary>
        public float FillShare => FillLive() ? FillE() : 1f;

        float FillRaw() { float s = OzVehicleColourFill.Seconds; return s > 0f ? (Time.time - fillAt) / s : 1f; }
        float FillE() => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(FillRaw()));

        bool FillLive()
        {
            if (!fillOn) return false;
            if (FillRaw() >= 1f || charred >= OzV2EmptyLook.WreckFrom || (fillView && fillView.Dying) || !OzVehicleColourFill.On) { fillOn = false; return false; }
            return true;
        }

        /// <summary>OzV2EmptyLook (TeamPaint.EmptyV1): the empty v2 look is drawn for a neutral paint, and for the whole of a fill one side of
        /// which is neutral (TeamLit weights it by the neutral share).</summary>
        bool EmptyShown => team == Team.Neutral || (FillLive() && fillFrom == Team.Neutral);

        static Color PaintOf(Team t)
        {
            Color c = TeamColors.Paint(t).linear;
            if (t == Team.Neutral) { c *= 3.0f; c.a = 1; }                                       // = Apply's neutral lift
            return c;
        }

        /// <summary>Apply: _TeamColor (c = this team's, the neutral lift included).</summary>
        Color FillPaint(Color c)
        {
            if (!FillLive()) return EasedPaint(c);                                                // K389 (the shuttle's capture) as before
            var r = Color.Lerp(PaintOf(fillFrom), c, FillE()); r.a = c.a;
            return r;
        }

        /// <summary>Apply: _OZTeamVivid. Owned to owned: the colour moves, w 2. Neutral to owned: the new team's colour, w 1 -> 2. Owned to
        /// neutral: the old team's colour, w 2 -> 1.</summary>
        Vector4 FillVivid(Vector4 v, Material m)
        {
            if (!FillLive()) return EasedVivid(v, m);
            float e = FillE();
            bool fo = fillFrom != Team.Neutral, to = team != Team.Neutral;
            if (fo && to) { var a = TeamColors.VividLinear(fillFrom); return new Vector4(Mathf.Lerp(a.x, v.x, e), Mathf.Lerp(a.y, v.y, e), Mathf.Lerp(a.z, v.z, e), v.w); }
            if (to) return new Vector4(v.x, v.y, v.z, 1f + e);
            if (fo) { var a = TeamColors.VividLinear(fillFrom); return new Vector4(a.x, a.y, a.z, 2f - e); }
            return v;
        }

        /// <summary>Apply: _OZTeamGlow (TeamLit weights it by the owned share).</summary>
        Vector4 FillGlow(Vector4 g)
        {
            if (!FillLive()) return EasedGlow(g);
            bool fo = fillFrom != Team.Neutral, to = team != Team.Neutral;
            if (fo && to) return Vector4.Lerp(TeamColors.GlowLinear(fillFrom), g, FillE());
            if (fo) return TeamColors.GlowLinear(fillFrom);
            return g;
        }

        /// <summary>OzVehicleColourFill.Begin (UnitView.SetOwner): the team changes to `to` now and the look fills over. False = not here
        /// (switched off, not a living vehicle or gun, not drawn last frame, a wreck, the shuttle's capture: K389 eases that one): the caller
        /// sets the team at once, as before.</summary>
        internal bool BeginFill(Team to, UnitView v)
        {
            if (!OzVehicleColourFill.On || OzVehicleColourFill.Seconds <= 0f || to == team || !v || !isActiveAndEnabled) return false;
            var o = v.Obj;
            if (o == null || (o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) || o.Destroyed || v.Dying) return false;
            if (o.IsShuttle && to != Team.Neutral) return false;                                 // K389 (View/OzShuttleCaptureFx.cs) eases the shuttle's capture
            if (fillSeen < 0 || Time.frameCount - fillSeen > 2) { OzVehicleColourFill.Snaps++; return false; }   // not drawn with its old look just now (a first paint, a view back from hiding)
            if (charred >= OzV2EmptyLook.WreckFrom) return false;
            Team from = team; float raw0 = 0f;
            if (FillLive())
            {
                if (to == fillFrom) { raw0 = 1f - Mathf.Clamp01(FillRaw()); OzVehicleColourFill.Reversals++; }   // back the way it came: from where it is now (smoothstep is symmetric)
                else { if (FillE() < 0.5f) from = fillFrom; OzVehicleColourFill.Restarts++; }                  // a third team inside the blend (very rare): from the nearer look
            }
            fillFrom = from; fillAt = Time.time - raw0 * OzVehicleColourFill.Seconds; fillOn = true; fillView = v; fillSeen = Time.frameCount;
            team = to;
            BindFill();
            Apply();                                                                               // the first frame (the old look, or where the blend stood)
            OzVehicleColourFill.Started(o, from, to);
            return true;
        }

        /// <summary>UnitView.SetOwner, every sync with no team change: the next frame of a running fill (three values a slot), its end (a
        /// full Apply: the exact new look).</summary>
        internal void StepFill()
        {
            fillSeen = Time.frameCount;
            if (!fillOn) return;
            if (!FillLive()) { Apply(); OzVehicleColourFill.Ends++; return; }
            WriteFill();
        }

        /// <summary>The renderers Apply paints (its own rule: a robot figure riding on this object keeps its own paint), once per fill.</summary>
        void BindFill()
        {
            fillR.Clear(); fillSlots.Clear(); fillRobot.Clear();
            fillAll.Clear(); GetComponentsInChildren(true, fillAll);
            bool self = IsRobotBody;
            for (int i = 0; i < fillAll.Count; i++)
            {
                var r = fillAll[i]; if (!r) continue;
                var host = r.GetComponentInParent<TeamPaint>(true);
                bool robot = (host ? host : this).IsRobotBody;
                if (robot && host != this && !self) continue;
                r.GetSharedMaterials(fillMats);
                fillR.Add(r); fillSlots.Add(fillMats.Count); fillRobot.Add(robot);
            }
            fillAll.Clear(); fillMats.Clear();
            OzVehicleColourFill.Binds++;
        }

        /// <summary>One frame of the fill: _TeamColor, _OZTeamVivid and _OZTeamGlow as Apply sets them now, into each slot's own block (every
        /// other value in the block stays as its writer left it).</summary>
        void WriteFill()
        {
            fillBlock ??= new MaterialPropertyBlock();
            Color c = TeamColors.Paint(team).linear;
            if (team == Team.Neutral) { c *= 3.0f; c.a = 1; }
            c = FillPaint(c);
            float k = 1f - 0.78f * charred;
            var viv = FillVivid(TeamColors.VividLinear(team), null);
            var vk = new Vector4(viv.x * k, viv.y * k, viv.z * k, viv.w);
            var glow = FillGlow(TeamColors.GlowLinear(team)) * (k * glowLevel);
            var cv = c * k;
            var cr = new Color(c.r * k * RobotTeamGain, c.g * k * RobotTeamGain, c.b * k * RobotTeamGain, c.a * k);
            for (int i = 0; i < fillR.Count; i++)
            {
                var r = fillR[i]; if (!r) continue;
                var tc = fillRobot[i] ? cr : cv;
                for (int m = 0; m < fillSlots[i]; m++)
                {
                    r.GetPropertyBlock(fillBlock, m);
                    fillBlock.SetColor(TeamColorId, tc);
                    fillBlock.SetVector(VividId, vk);
                    fillBlock.SetVector(GlowId, glow);
                    r.SetPropertyBlock(fillBlock, m);
                }
            }
            OzVehicleColourFill.Steps++;
        }
    }
}
