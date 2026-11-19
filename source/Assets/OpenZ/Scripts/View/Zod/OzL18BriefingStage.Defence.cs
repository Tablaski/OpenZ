// OpenZ (GPL-3.0)
// While the blue briefing's parades come over the three long bridges, the red robots of the level's start set are not drawn; in their place
// the stage shows red squads in firing lines at the three south bridge heads: a kneeling row in front, a standing row behind it, every
// rifle up and aimed north along its bridge, two runners taking their places in the first seconds. Nobody fires (nothing may hit the
// parade). After the blasts each one takes his eye off the sights in his own time: the kneeling ones stand up, the rifles come down.
// STAGING ONLY: the rules, the start set and the units themselves are untouched. At the hand-back (or a skip) the stage's robots are
// destroyed and the level's own red robots are drawn again where the rules hold them. A pure function of the stage clock.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        /// <summary>The red defence of the blue briefing (false = the level's own red robots stand as the rules placed them).</summary>
        public static bool RedDefence = true;
        /// <summary>The lines: metres south of a deck's south end to the kneeling row and to the standing row, the distance of two men in a
        /// row, the seconds after the first blast before the first one lowers his rifle and the spread of the others, the runners' pace.</summary>
        public static float DefFront = 13.0f, DefBack = 15.5f, DefStep = 2.3f, DefReact = 0.55f, DefReactSpread = 0.9f, DefRunSpeed = 5.4f;
        /// <summary>Play checks: the stage's defenders alive now, the level's red robot renderers held back.</summary>
        public static int Defenders, RedHeldBack;

        sealed class Guard
        {
            public Transform root; public Animator anim; public bool kneel, runner; public Vector3 from, to; public Quaternion face, runFace;
            public float runAt, runFor, react, aim; public string state = "";
        }
        readonly List<Guard> guards = new List<Guard>();
        readonly List<Renderer> redHeld = new List<Renderer>();

        void BuildDefence()
        {
            if (!RedDefence || !blue || spans.Count == 0 || !game) return;
            var pf = game.Prefab("Z_bs01_grunt"); if (!pf) { Warn("no Z_bs01_grunt prefab: the red defence is not staged"); return; }
            uint seed = 4177u; float start = -(BlueOpen + BluePressClock);
            for (int i = 0; i < spans.Count; i++)
            {
                var sp = spans[i]; float zS = sp.c.z - sp.half; bool mid = i == spans.Count / 2;
                int front = mid ? 4 : 3, back = mid ? 3 : 2;
                for (int k = 0; k < front + back; k++)
                {
                    bool kneel = k < front; int n = kneel ? front : back, j = kneel ? k : k - front;
                    float x = sp.c.x + (j - (n - 1) * 0.5f) * DefStep + (Rnd(ref seed) - 0.5f) * 0.5f;
                    float z = zS - (kneel ? DefFront : DefBack) + (Rnd(ref seed) - 0.5f) * 0.6f;
                    var to = new Vector3(x, game.GroundY(x, z), z);
                    Vector3 aimAt = new Vector3(sp.c.x + (Rnd(ref seed) - 0.5f) * 3f, to.y, sp.c.z); Vector3 d = aimAt - to; d.y = 0f;
                    var g = MakeGuard(pf, "L18Defence_" + i + "_" + k, to, Quaternion.LookRotation(d.normalized, Vector3.up), kneel);
                    g.react = DefReact + Rnd(ref seed) * DefReactSpread;
                    if (mid && !kneel && j != 1)
                    {   // the two outer men of the middle line run in along the east-west road in the first seconds
                        g.runner = true; float side = j == 0 ? -1f : 1f; float fx = to.x + side * 12.5f, fz = to.z - 1.2f;
                        g.from = new Vector3(fx, game.GroundY(fx, fz), fz); Vector3 rd = to - g.from; rd.y = 0f; g.runFace = Quaternion.LookRotation(rd.normalized, Vector3.up);
                        g.runAt = start + 0.2f + (j == 0 ? 0f : 0.7f); g.runFor = rd.magnitude / Mathf.Max(1f, DefRunSpeed);
                    }
                }
            }
            // the level's own red robots are not drawn while the stage's lines stand (the units themselves are untouched)
            foreach (var kv in game.Views)
            {
                var v = kv.Value; if (!v || v.Obj == null || v.Obj.Owner != Team.Red || v.Obj.Kind != ZKind.Robot) continue;
                foreach (var r in v.GetComponentsInChildren<Renderer>(true)) if (r && r.enabled) { r.enabled = false; redHeld.Add(r); }
            }
            Defenders = guards.Count; RedHeldBack = redHeld.Count;
            Debug.Log($"[OpenZ L18 briefing] the red defence: {guards.Count} robots in lines at {spans.Count} bridge heads, {redHeld.Count} renderers of the level's own red robots held back until the hand-back");
        }

        Guard MakeGuard(GameObject pf, string name, Vector3 at, Quaternion face, bool kneel)
        {
            var holder = new GameObject(name).transform; holder.SetParent(root, false); holder.SetPositionAndRotation(at, face);
            var go = Instantiate(pf, holder, false); go.name = "Model";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) Destroy(a);
            var paint = go.GetComponent<TeamPaint>(); if (!paint) paint = go.AddComponent<TeamPaint>();
            paint.Team = Team.Red;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; }   // a living unit: crisp
            var g = new Guard { root = holder, to = at, from = at, face = face, runFace = face, kneel = kneel };
            g.anim = go.GetComponentInChildren<Animator>(true);
            if (g.anim) { g.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; g.anim.applyRootMotion = false; }
            WeaponHold.Attach(go);                                                                       // the rifle in his hands, as every robot of the game
            guards.Add(g);
            return g;
        }

        /// <summary>Each frame of the blue stage: where a defender stands, what his body does (a formula of the stage clock).</summary>
        void TickDefence(float t)
        {
            if (guards.Count == 0) return;
            for (int i = 0; i < redHeld.Count; i++) { var r = redHeld[i]; if (r && r.enabled) r.enabled = false; }
            float blast0 = float.MaxValue; foreach (var s in spans) blast0 = Mathf.Min(blast0, s.blastAt);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            foreach (var g in guards)
            {
                if (!g.root) continue;
                string want; float aim; Vector3 pos = g.to; Quaternion rot = g.face;
                if (g.runner && t < g.runAt + g.runFor + 0.25f)
                {
                    float u = Mathf.Clamp01((t - g.runAt) / Mathf.Max(0.1f, g.runFor));
                    pos = Vector3.Lerp(g.from, g.to, u); pos.y = game.GroundY(pos.x, pos.z);
                    rot = u < 1f ? g.runFace : Quaternion.Slerp(g.runFace, g.face, Mathf.SmoothStep(0f, 1f, (t - g.runAt - g.runFor) / 0.25f));
                    want = u > 0f && u < 1f ? "run_forward" : "idle"; aim = 0f;
                }
                else if (t < blast0 + g.react) { want = g.kneel ? "sniper_kneel_aim" : "idle"; aim = g.kneel ? 0f : 1f; }
                else if (g.kneel && t < blast0 + g.react + 0.85f) { want = "sniper_kneel_up"; aim = 0f; }
                else { want = "idle"; aim = 0f; }
                g.root.SetPositionAndRotation(pos, rot);
                if (!g.anim) continue;
                if (g.state != want)
                {
                    bool first = g.state == "";
                    g.state = want; g.anim.CrossFadeInFixedTime(want, first ? 0f : want == "sniper_kneel_up" ? 0.1f : 0.18f, 0, first ? (g.react * 7.3f) % 1f : 0f);
                    if (first && g.anim.layerCount > 1) g.anim.CrossFadeInFixedTime("aim_hold", 0f, 1, 0f);
                    if (first) g.aim = aim;
                }
                g.aim = Mathf.MoveTowards(g.aim, aim, dt / 0.45f);
                if (g.anim.layerCount > 1) g.anim.SetLayerWeight(1, g.aim);
                g.anim.speed = 1f;
            }
        }

        /// <summary>The hand-back, a skip, a rewind: the stage's defenders go, the level's own red robots are drawn again.</summary>
        void DefenceOff()
        {
            foreach (var g in guards) if (g.root) Destroy(g.root.gameObject);
            guards.Clear(); Defenders = 0;
            foreach (var r in redHeld) if (r) r.enabled = true;
            redHeld.Clear(); RedHeldBack = 0;
        }
    }
}
