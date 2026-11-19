using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// OpenZ/UeHolo, the same hit flash, low-health flicker and shatter) as a 4 m cube drawn on its stack level (Sim/ZWorld.UeBoxes.cs:
    /// level x 4 m). Only a box on the floor shows the projector's light pool. When its level drops in the rules (a box under it was destroyed
    /// or removed) it FALLS: gravity (9.81 m/s2, game time: it stops while the game is paused and follows the game's speed), a small tumble
    /// when it is off-centre (tipping toward the side that has nothing under it, at most <see cref="OzUeBoxes.TumbleMaxDeg"/>), and on landing a
    /// damped rock back to level, the hologram's hit flash, a heavy thud, a crackle of holo sparks along the contact and a puff (the floor's own
    /// dust on Desert dunes / Snow field, holo light on the dark studio floors; a holo flash where it lands on another box).
    /// </summary>
    public sealed partial class OzUeObstacleView
    {
        bool isBox, falling, poolOff;
        int drawnLevel;
        float baseY, vy, fallFrom, fallDist, rockT = -1f, rockAmp;
        Vector3 tipDir;
        float tiltDeg;

        /// <summary>The box's drawn floor height now (m), -1 when this view is not a box.</summary>
        internal float BoxBaseY => isBox ? baseY : -1f;
        internal bool IsBox => isBox;
        internal Vector3 BoxCentre => centre;
        internal bool BoxFalling => falling;

        /// <summary>Bind's hook: a box gets its own height and stands on its level.</summary>
        void BoxBind(ZObject o, ref float h, ref Vector3 c)
        {
            isBox = OzUePlayground.IsBox(o);
            if (!isBox) return;
            h = OzUePlayground.BoxHeightM;
            drawnLevel = Mathf.Max(0, OzUePlayground.BoxLevel(o));
            baseY = drawnLevel * OzUePlayground.BoxHeightM; c.y = baseY;
            falling = false; vy = 0f; rockT = -1f; tiltDeg = 0f;
            OzUeBoxes.Register(this);
        }

        void OnDestroy() { if (isBox) OzUeBoxes.Unregister(this); }

        /// <summary>BreakUp's hook: a box up on a stack has no floor under it for the projector's pool (the shatter's pool is hidden).</summary>
        void BoxBrokeUp()
        {
            if (!isBox) return;
            OzUeBoxes.Unregister(this);
            if (baseY > 0.05f && Obj != null)
            {
                var s = GameObject.Find("OZ_UeHoloShatter_" + Obj.Id);
                var pool = s ? s.transform.Find("Pool") : null;
                if (pool) pool.gameObject.SetActive(false);
            }
        }

        /// <summary>LateUpdate's hook (alive only): the fall, the tumble, the landing, the pool.</summary>
        void BoxFrame()
        {
            if (!isBox || Obj == null || parts == null) return;
            int lv = OzUePlayground.BoxLevel(Obj); if (lv < 0) return;
            float H = OzUePlayground.BoxHeightM, target = lv * H, dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (!falling && target < baseY - 0.001f) StartFall(lv, target);
            else if (!falling && target > baseY + 0.001f) { baseY = target; drawnLevel = lv; }   // never in the rules (a level only drops); kept honest
            if (falling)
            {
                vy += OzUeBoxes.Gravity * dt; baseY -= vy * dt;
                float q = fallDist > 0.01f ? Mathf.Clamp01((fallFrom - baseY) / fallDist) : 1f;
                tiltDeg = OzUeBoxes.TumbleMaxDeg * TipK() * Mathf.SmoothStep(0f, 1f, q);
                if (baseY <= target)
                {
                    baseY = target; falling = false; drawnLevel = lv;
                    rockAmp = OzUeBoxes.TumbleMaxDeg * TipK() * 0.6f; rockT = 0f;
                    hitAt = Time.unscaledTime;                                                       // the hologram's own hit flash on the impact
                    OzUeBoxes.Land(this, Obj, centre, vy, lv);
                }
            }
            else if (rockT >= 0f)
            {   // the settle: a damped rock back to level (two swings, 0.45 s)
                rockT += dt;
                float k = rockT / OzUeBoxes.RockSec;
                tiltDeg = k >= 1f ? 0f : rockAmp * Mathf.Exp(-4.2f * k) * Mathf.Cos(k * Mathf.PI * 2.6f);
                if (k >= 1f) { rockT = -1f; tiltDeg = 0f; }
            }
            centre.y = baseY;
            float lift = OzUePlayground.BoxSideM * 0.5f * Mathf.Sin(Mathf.Abs(tiltDeg) * Mathf.Deg2Rad);   // it tips on its low edge: that edge stays on the floor plane
            transform.SetPositionAndRotation(new Vector3(centre.x, baseY + lift, centre.z), TiltOf(tipDir, tiltDeg));
            bool pool = lv == 0 && !falling && baseY < 0.05f;
            if (parts[0] && parts[0].enabled != (pool && parts[1] && parts[1].enabled)) parts[0].enabled = pool && parts[1] && parts[1].enabled;
        }

        void StartFall(int lv, float target)
        {
            falling = true; vy = 0f; fallFrom = baseY; fallDist = Mathf.Max(0.01f, baseY - target);
            tipDir = OzUeBoxes.OffCentre(Obj, lv);
            OzUeBoxes.Falls++;
            Debug.Log($"{OzUnitEditor.Tag} box #{Obj.Id} falls from level {drawnLevel} to {lv} ({fallDist:0.0} m){(tipDir.sqrMagnitude > 0.01f ? ", off-centre: it tips" : "")}");
        }

        /// <summary>How far off-centre (0 = centred, 1 = half the box hangs over nothing).</summary>
        float TipK() => Mathf.Clamp01(tipDir.magnitude / (OzUePlayground.BoxSideM * 0.5f));

        /// <summary>A tilt that lowers the side dir points to (angle in degrees; the floor middle stays put).</summary>
        static Quaternion TiltOf(Vector3 dir, float deg)
        {
            if (dir.sqrMagnitude < 1e-4f || Mathf.Abs(deg) < 1e-3f) return Quaternion.identity;
            var axis = Vector3.Cross(Vector3.up, dir.normalized);                                // AngleAxis(+, up x dir) turns +dir downward
            return Quaternion.AngleAxis(deg, axis);
        }
    }

    /// <summary>1.5 kit ue_boxes: the BOX views' list and what the rest of the view asks of them: the pointer's pick, the aim point of a shot at a
    /// box up a stack, the drop point on a box's top, the off-centre test of a fall, the landing's sound and puff. Every call answers at its first
    /// line while no box view exists (every level).</summary>
    public static class OzUeBoxes
    {
        /// <summary>Gravity (m/s2), the most a falling box tips (deg), the settle's length (s).</summary>
        public static float Gravity = 9.81f, TumbleMaxDeg = 9f, RockSec = 0.45f;
        /// <summary>The BOX tile's picture (a render of the box, kit ue_boxes; the obstacle's picture stands in when it is missing).</summary>
        public const string TilePath = "OpenZ/UnitEditor/ue_box_tile";
        /// <summary>Checks: boxes drawn, falls started, landings, landing sounds played, picks by the pointer, aim points lifted, drop points taken from a top.</summary>
        public static int Placed, Falls, Landings, Thuds, Picks, Aims, TopDrops;
        static readonly List<OzUeObstacleView> views = new List<OzUeObstacleView>();
        static float soundWindowAt; static int soundsInWindow;

        public static int Count => views.Count;
        internal static void Register(OzUeObstacleView v) { if (v && !views.Contains(v)) { views.Add(v); Placed++; } }
        internal static void Unregister(OzUeObstacleView v) { views.Remove(v); }
        internal static void Reset() { views.Clear(); }

        /// <summary>The panel's three lines (UI/OzUeObjectPanel.cs).</summary>
        public static readonly string[] PanelWords =
        {
            "A neutral holo box: it blocks every shot and every path, like the obstacle.",
            "Drop a box on a box and it goes on top: build towers, walls and castles, up to " + ZWorld.BoxLevels + " high.",
            "Only explosives hurt it (Ctrl + right-click). When a box breaks, the boxes above it fall.",
        };

        /// <summary>The panel's size row: "4 x 4 m, 4 m tall, on the floor" / "..., level 3 of 6".</summary>
        public static string SizeWords(ZObject o)
        {
            int lv = OzUePlayground.BoxLevel(o);
            string side = OzUePlayground.BoxSideM.ToString("0"), tall = OzUePlayground.BoxHeightM.ToString("0");
            return side + " x " + side + " m, " + tall + " m tall, " + (lv <= 0 ? "on the floor" : "level " + (lv + 1) + " of " + ZWorld.BoxLevels);
        }

        static OzUeObstacleView ViewOf(ZObject o)
        {
            if (o == null) return null;
            for (int i = 0; i < views.Count; i++) if (views[i] && views[i].Obj == o) return views[i];
            return null;
        }

        /// <summary>The pointer's ray against every box's drawn cube; the nearest box hit (null = none) and the hit point.</summary>
        internal static OzUeObstacleView RayHit(Ray ray, out Vector3 hit, out Vector3 normal)
        {
            hit = default; normal = default;
            OzUeObstacleView best = null; float bestT = float.MaxValue;
            float half = OzUePlayground.BoxSideM * 0.5f, H = OzUePlayground.BoxHeightM;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (!v || v.Dying || v.Obj == null) continue;
                var c = v.BoxCentre; float y0 = v.BoxBaseY;
                var b = new Bounds(new Vector3(c.x, y0 + H * 0.5f, c.z), new Vector3(half * 2f, H, half * 2f));
                if (b.IntersectRay(ray, out float t) && t < bestT) { bestT = t; best = v; }
            }
            if (best)
            {
                hit = ray.GetPoint(bestT);
                var c = best.BoxCentre; float top = best.BoxBaseY + H;
                normal = Mathf.Abs(hit.y - top) < 0.02f ? Vector3.up : Mathf.Abs(hit.x - c.x) > Mathf.Abs(hit.z - c.z) ? new Vector3(Mathf.Sign(hit.x - c.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(hit.z - c.z));
            }
            return best;
        }

        /// <summary>The hover picks (View/OzUeField.cs HoverAt, View/PlayerInput.cs HoverAt): the box the pointer really is on (the drawn cube on its
        /// level, the nearest one), in place of a box picked by its floor footprint; h unchanged when no box view exists or the pointer is on
        /// something else.</summary>
        public static ZObject Pick(Camera cam, Vector2 mp, ZObject h)
        {
            if (views.Count == 0 || !cam || !OzUnitEditor.Active) return h;
            if (h != null && !OzUePlayground.IsBox(h)) return h;
            var v = RayHit(cam.ScreenPointToRay(mp), out _, out _);
            if (v) { Picks++; return v.Obj; }
            return null;
        }

        /// <summary>View/OzUePlatformView.cs Aim (OzShuttleAim.AirAim's chain): a shot at a box hits its middle on its level (the drawn height
        /// while the view stands, the rules' level after it broke). False = not a box (the chain goes on).</summary>
        public static bool Aim(ZObject t, ref Vector3 ground)
        {
            if (views.Count == 0 || t == null || t.Type != ZWorld.WreckType || !OzUnitEditor.Active) return false;
            int lv = OzUePlayground.BoxLevel(t); if (lv < 0) return false;
            var v = ViewOf(t);
            float y0 = v && !v.Dying ? v.BoxBaseY : lv * OzUePlayground.BoxHeightM;
            ground.y = y0 + OzUePlayground.BoxHeightM * 0.5f; Aims++;
            return true;
        }

        /// <summary>View/OzUePlace.cs FloorPoint: while the BOX is being placed, a pointer on a box gives that box's top: the top face's point (near
        /// its middle = straight on top, near an edge = half over, brick style), a side face the box's middle (on top of that stack).</summary>
        public static void TopRay(Ray ray, ref Vector3 p)
        {
            if (views.Count == 0 || OzUePlace.Id != OzUePlayground.Box) return;
            var v = RayHit(ray, out var hit, out var n);
            if (!v) return;
            var c = v.BoxCentre;
            if (n == Vector3.up) { p.x = hit.x; p.z = hit.z; }
            else { p.x = c.x; p.z = c.z; }
            TopDrops++;
        }

        /// <summary>A falling box's tip direction (m, from the middle of what it lands on toward its own middle): zero when it lands on the floor or
        /// square on boxes, else toward the side that hangs over nothing.</summary>
        internal static Vector3 OffCentre(ZObject o, int level)
        {
            var w = OzUnitEditor.I ? OzUnitEditor.I.World : null;
            if (w == null || o == null || level <= 0) return Vector3.zero;
            int tx = o.LocX >> 4, ty = o.LocY >> 4, n = ZWorld.BoxTiles; double sx = 0, sy = 0; int cells = 0;
            foreach (var b in w.Boxes)
            {
                if (b == o || w.BoxLevelOf(b) != level - 1) continue;
                int bx = b.LocX >> 4, by = b.LocY >> 4;
                for (int j = ty; j < ty + n; j++) for (int i = tx; i < tx + n; i++)
                    if (i >= bx && i < bx + n && j >= by && j < by + n) { sx += i + 0.5; sy += j + 0.5; cells++; }
            }
            if (cells == 0 || cells == n * n) return Vector3.zero;
            var mine = ZGame.PxToWorld((tx + n * 0.5) * 16, (ty + n * 0.5) * 16);
            var sup = ZGame.PxToWorld(sx / cells * 16, sy / cells * 16);
            var d = mine - sup; d.y = 0f;
            return d;
        }

        /// <summary>A box landed: the heavy thud (the fort gun's concrete landing, higher and lighter for a hollow box; one a column: the event
        /// keeps one voice), a crackle of holo sparks along its foot, and the puff: the floor's dust on Desert dunes / Snow field, holo light on
        /// the dark floors and on a box under it.</summary>
        internal static void Land(OzUeObstacleView v, ZObject o, Vector3 c, float speed, int level)
        {
            Landings++;
            float half = OzUePlayground.BoxSideM * 0.5f, y = level * OzUePlayground.BoxHeightM;
            var foot = new Vector3(c.x, y, c.z);
            float k = Mathf.Clamp01(speed / 9f);
            int id = o != null ? o.Id : -1;
            if (Time.unscaledTime - soundWindowAt > 0.12f) { soundWindowAt = Time.unscaledTime; soundsInWindow = 0; }
            if (soundsInWindow < 2)
            {
                soundsInWindow++;
                if (AudioDirector.Has("dst_fortgun_land_heavy")) { AudioDirector.Fire("dst_fortgun_land_heavy", foot, id, 0.55f + 0.45f * k, Random.Range(1.12f, 1.3f)); Thuds++; }
                var au = CombatAudio.I;
                if (au) au.PlayPitched("ue_box_land_crackle", foot + Vector3.up * 0.3f, 0.22f + 0.18f * k, 0f, id, Random.Range(0.9f, 1.15f), "sd_lamp_spark_a", "sd_lamp_spark_b", "sd_lamp_spark_c");
            }
            var fx = OpenZFxPlayer.I; if (!fx) return;
            var holo = OzUeObstacleViews.HoloColor; var hot = new Color(holo.r * 2.4f, holo.g * 2.4f, holo.b * 2.4f, 1f);
            int sparks = 10 + Mathf.RoundToInt(10f * k);
            for (int i = 0; i < sparks; i++)
            {   // along the four bottom edges, skidding outward
                float s = Random.Range(-half, half); int e = i & 3;
                var at = foot + (e == 0 ? new Vector3(half, 0.05f, s) : e == 1 ? new Vector3(-half, 0.05f, s) : e == 2 ? new Vector3(s, 0.05f, half) : new Vector3(s, 0.05f, -half));
                var outv = new Vector3(at.x - foot.x, 0f, at.z - foot.z).normalized;
                fx.EmitChannel(i % 3 == 0 ? "leg_spark" : "wv2_spark", at, outv * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(0.4f, 1.6f), Random.Range(0.2f, 0.45f), Random.Range(0.03f, 0.06f), i % 4 == 0 ? Color.white : hot, 1f);
            }
            if (level == 0 && OzUeDust.KindNow != 0)
            {   // the floor's own dust: a low ring rolling out from under the four sides
                var dust = OzUeDust.Colour;
                int puffs = 10 + Mathf.RoundToInt(8f * k);
                for (int i = 0; i < puffs; i++)
                {
                    float ang = i * Mathf.PI * 2f / puffs + Random.Range(-0.15f, 0.15f);
                    var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    var at = foot + d * half * Random.Range(0.95f, 1.15f) + Vector3.up * 0.15f;
                    fx.EmitChannel("m12_dust", at, d * Random.Range(1.2f, 2.6f) * (0.6f + 0.6f * k) + Vector3.up * Random.Range(0.2f, 0.6f), Random.Range(1.1f, 1.7f), Random.Range(0.9f, 1.5f), dust, 0.32f + 0.18f * k);
                }
            }
            else
            {   // holo light: soft glows spreading along the contact, a flash of light
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI * 0.25f + Random.Range(-0.2f, 0.2f);
                    var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    fx.EmitChannel("wv2_glow", foot + d * half * 0.9f + Vector3.up * 0.1f, d * Random.Range(0.6f, 1.4f) + Vector3.up * 0.2f, Random.Range(0.3f, 0.55f), Random.Range(0.5f, 0.9f), holo, 0.7f);
                }
            }
            fx.AddLight(foot + Vector3.up * 0.6f, holo, 2.5f + 2.5f * k, 9f, 0.3f, 2.2f, 0.15f, false);
        }
    }
}
