using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUePlace.cs): the drop preview's see-through models. Every part of a preview model (a robot's
    /// body, its bolt-on kit and helmet, its weapon; a vehicle's or a gun's hull, turret, wheels and barrels) is drawn with ONE material of
    /// its team: OpenZ/UeGhost (Resources/OpenZ/UnitEditor/OzUeGhost.shader) in the team's ring colour as the player sees it (red #FF7A5E,
    /// blue #7FA4FF, neutral #CFCDBF; the YOUR TEAM COLOUR option swaps red and blue) at UnitAlpha (an empty vehicle or gun NeutralAlpha),
    /// shaded only by its own form (a fixed soft key light and a lighter rim), its nearest surface once per pixel (a depth pass, then the
    /// colour once per pixel through a stencil bit), drawn after the opaque world and before the transparent marks. No shadow, no light
    /// probe, no ambient occlusion, no per-object motion (the MOTION BLUR option never smears it): the same look on every graphics preset
    /// and at every screen size.
    /// What it replaces: the game's TeamLit screen-door cut (a 4 x 4 pattern, 6 of 16 pixels drawn). Under it a robot's skinned body
    /// still wrote its own per-object motion (every living unit is kept out of it by UnitLookMotionGate.KeepUnitCrisp, the preview models
    /// were not), so with the motion blur on (the default) the body was drawn as a dark smear of the floor behind it with a few red dots,
    /// while the rigid helmet kit and the weapon (no motion of their own) kept their grey and orange paint: floating guns.
    /// </summary>
    public static class OzUeGhost
    {
        public const string ShaderRes = "OpenZ/UnitEditor/OzUeGhost", ShaderName = "OpenZ/UeGhost";
        /// <summary>Opacity of a unit's ghost and of an empty (neutral) vehicle's or gun's.</summary>
        public const float UnitAlpha = 0.45f, NeutralAlpha = 0.5f;
        /// <summary>After every opaque object (Geometry 2000, AlphaTest 2450, the factory lights 2460) and before the transparent ones
        /// (from 2501): drawn front to back, so where two parts overlap the nearer one is the one seen.</summary>
        public const int Queue = 2490;
        public const string DepthPass = "GhostDepth", ColourPass = "GhostColour";
        static readonly int ColorId = Shader.PropertyToID("_GhostColor"), AlphaId = Shader.PropertyToID("_GhostAlpha");
        static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();
        static readonly List<Material> buf = new List<Material>(8);
        static Shader shader; static bool warned;
        /// <summary>Checks: renderers given the ghost material, parts left out (not drawn: glows, beams, particles, shadow proxies).</summary>
        public static int Swaps, LeftOut;

        /// <summary>The ghost shader (Resources, so the player build carries it), or null when it is missing or not supported here.</summary>
        public static Shader GhostShader
        {
            get
            {
                if (shader) return shader;
                shader = Resources.Load<Shader>(ShaderRes);
                if (!shader) shader = Shader.Find(ShaderName);
                if ((!shader || !shader.isSupported) && !warned)
                {
                    warned = true;
                    Debug.LogWarning("[OpenZ UnitEditor] the drop preview's ghost shader " + (shader ? "is not supported here" : "is missing (Resources/" + ShaderRes + ")") + ": the preview keeps the screen-door cut");
                }
                if (shader && !shader.isSupported) return null;
                return shader;
            }
        }

        public static bool Ready => GhostShader != null;

        /// <summary>The colour of a team's ghost as drawn (the team's ring colour, TeamColors.Shown).</summary>
        public static Color Colour(Team team) => OzUeUi.Ring(team);

        /// <summary>The one ghost material of a team at an opacity (made once; null without the shader).</summary>
        public static Material MaterialFor(Team team, float alpha)
        {
            int key = (int)TeamColors.Shown(team) * 1000 + Mathf.RoundToInt(Mathf.Clamp01(alpha) * 100f);
            if (mats.TryGetValue(key, out var m) && m) return m;
            var sh = GhostShader; if (!sh) return null;
            m = new Material(sh) { name = "OzUe_Ghost_" + TeamColors.Key(team) + "_" + Mathf.RoundToInt(alpha * 100f), hideFlags = HideFlags.DontSave, renderQueue = Queue };
            m.SetColor(ColorId, Colour(team));
            m.SetFloat(AlphaId, Mathf.Clamp01(alpha));
            mats[key] = m;
            return m;
        }

        /// <summary>A part of a preview model that becomes ghost: a mesh or skinned mesh drawn with the game's TeamLit (the model's own
        /// paint), not a shadow-only proxy. Its other renderers (glows, lamp lenses, beams, particles, trails) are left out. A mesh part
        /// may have no mesh yet: a kit hose or belt (BS01KitCable: the electrobot's, the laser's, the pyro's) builds its tube when the
        /// model is first switched on, after this choice, so a mesh part is taken by its paint, not by its mesh.</summary>
        public static bool Takes(Renderer r)
        {
            if (!r || r.forceRenderingOff) return false;
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return false;
            if (r is SkinnedMeshRenderer s) { if (!s.sharedMesh) return false; }
            else if (r is MeshRenderer) { if (!r.TryGetComponent<MeshFilter>(out _)) return false; }
            else return false;
            return OzShuttleCamFade.TakesFade(r);
        }

        /// <summary>Gives the part its team's ghost (every material slot), no shadow, no probes, no per-object motion. Idempotent: a part
        /// that has it already is only checked (a later paint step that put a material back is undone).</summary>
        public static bool Apply(Renderer r, Team team, float alpha)
        {
            if (!r) return false;
            var m = MaterialFor(team, alpha); if (!m) return false;
            r.GetSharedMaterials(buf);
            bool same = buf.Count > 0;
            for (int i = 0; i < buf.Count; i++) if (buf[i] != m) { same = false; break; }
            int n = Mathf.Max(1, buf.Count);
            buf.Clear();
            if (!same)
            {
                var a = new Material[n];
                for (int i = 0; i < n; i++) a[i] = m;
                r.sharedMaterials = a;
                Swaps++;
            }
            if (r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.Off;
            if (r.receiveShadows) r.receiveShadows = false;
            if (r.lightProbeUsage != LightProbeUsage.Off) r.lightProbeUsage = LightProbeUsage.Off;
            if (r.reflectionProbeUsage != ReflectionProbeUsage.Off) r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (r.motionVectorGenerationMode != MotionVectorGenerationMode.Camera) r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;   // as a living unit (UnitLookMotionGate.KeepUnitCrisp)
            if (r is SkinnedMeshRenderer s && s.skinnedMotionVectors) s.skinnedMotionVectors = false;
            return true;
        }

        /// <summary>True when every material slot of the part is the ghost of this team and opacity.</summary>
        public static bool Is(Renderer r, Team team, float alpha)
        {
            if (!r) return false;
            var m = MaterialFor(team, alpha); if (!m) return false;
            r.GetSharedMaterials(buf);
            bool all = buf.Count > 0;
            for (int i = 0; i < buf.Count; i++) if (buf[i] != m) { all = false; break; }
            buf.Clear();
            return all;
        }

        // ------------------------------------------------------------------------------------------------ counted check
        /// <summary>Menu 2: the shader is in the build and supported, its two passes and its queue, the three team colours as drawn
        /// (red and blue hues, neutral grey) and the two opacities (40 .. 50 %).</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int f = 0;
            var sh = GhostShader;
            f += OzUnitEditor.Line(log, sh != null, "P4 drop preview ghost: the shader " + ShaderName + " (Resources/" + ShaderRes + ") is in the build and supported here");
            if (sh != null)
            {
                var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    bool passes = m.passCount == 2 && m.GetPassName(0) == DepthPass && m.GetPassName(1) == ColourPass;
                    f += OzUnitEditor.Line(log, passes && sh.renderQueue == Queue, "P4 drop preview ghost: two passes (" + (m.passCount > 0 ? m.GetPassName(0) : "-") + ", " + (m.passCount > 1 ? m.GetPassName(1) : "-")
                                           + ": the nearest surface, then the colour once per pixel), queue " + sh.renderQueue + " (after the opaque world, before the transparent marks)");
                }
                finally { if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m); }
            }
            Color.RGBToHSV(Colour(Team.Red), out float hr, out float sr, out _);
            Color.RGBToHSV(Colour(Team.Blue), out float hb, out float sb, out _);
            Color.RGBToHSV(Colour(Team.Neutral), out _, out float sn, out float vn);
            bool red = TeamColors.Shown(Team.Red) == Team.Red ? (hr < 0.08f || hr > 0.95f) : (hr > 0.55f && hr < 0.7f);
            bool blue = TeamColors.Shown(Team.Blue) == Team.Blue ? (hb > 0.55f && hb < 0.7f) : (hb < 0.08f || hb > 0.95f);
            f += OzUnitEditor.Line(log, red && blue && sr > 0.4f && sb > 0.4f && sn < 0.15f && vn > 0.6f,
                                   "P4 drop preview ghost: the team colours as drawn: red side hue " + Mathf.RoundToInt(hr * 360f) + " deg, blue side hue " + Mathf.RoundToInt(hb * 360f)
                                   + " deg, neutral a light grey (saturation " + Mathf.RoundToInt(sn * 100f) + " %)");
            f += OzUnitEditor.Line(log, UnitAlpha >= 0.4f && UnitAlpha <= 0.5f && NeutralAlpha >= 0.4f && NeutralAlpha <= 0.5f,
                                   "P4 drop preview ghost: opacity " + Mathf.RoundToInt(UnitAlpha * 100f) + " % for a unit, " + Mathf.RoundToInt(NeutralAlpha * 100f) + " % for an empty vehicle or gun (40 .. 50 %)");
            return f;
        }
    }
}
