// OpenZ (GPL-3.0) - version 1.3, level 7: the break of the two crystals (map objects 15 and 16). View only: the rules, hit points, blocked tile and
// the map's debris missile stay as they are; nothing here reads or writes the sim beyond the object it is handed.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A crystal of level 7 stands on the scene object map_object15_x_y or map_object16_x_y (OzL07Scene.Crystals keeps the BODY_, RUIN_ and LOOSE_ children of
    /// DV2_L07_Kit.fbx under the view, so the view scale reaches all of them). When the rules destroy it (CombatView calls SimDied) the body goes, the
    /// low stump stays on the map, the shards fly and rest, yellow-green chips burst from a pool of their own, the map object's small blast and ash dust
    /// stay, and the glass break plays over the rock break. No other level has this component, so SimDied answers false there and nothing changes.
    /// Play check: All, Broken, Breaks, LastBreak.
    /// </summary>
    public sealed class OzL07Crystal : MonoBehaviour
    {
        public static readonly List<OzL07Crystal> All = new List<OzL07Crystal>();
        public static int Breaks; public static string LastBreak = "";
        public const string BreakEvent = "dst_glass";
        static readonly string[] ShardLand = { "sd_lamp_glass_a", "sd_lamp_glass_b" };
        public GameObject[] body; public GameObject ruin; public Transform[] loose;
        public Color[] chips = { new Color(0.80f, 0.80f, 0.33f), new Color(0.61f, 0.61f, 0.24f), new Color(0.89f, 0.84f, 0.35f) };   // the crystal's own paint (dv2_atlas_L07.png)
        public bool Broken { get; private set; }
        DetailKit.Bits bits;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>OzL07Scene.Crystals: takes the kit's children of the model and puts the break stage away (the shards are slices of the body: they stay off until the break).</summary>
        public void Bind(Transform model)
        {
            var b = new List<GameObject>(); var l = new List<Transform>(); ruin = null;
            foreach (Transform c in model)
            {
                if (c.name.StartsWith("BODY_")) b.Add(c.gameObject);
                else if (c.name.StartsWith("RUIN_")) ruin = c.gameObject;
                else if (c.name.StartsWith("LOOSE_")) l.Add(c);
            }
            body = b.ToArray(); loose = l.ToArray();
            if (ruin) ruin.SetActive(false);
            foreach (var t in loose) t.gameObject.SetActive(false);
        }

        /// <summary>CombatView: the rules destroyed a map object. True = this was a crystal and it broke itself (false: not ours, the caller goes on).</summary>
        public static bool SimDied(ZGame game, ZObject o, Vector3 from, FxManager fx, CombatAudio au)
        {
            if (game == null || o == null || !o.Type.StartsWith("map_object") || !game.StaticViews.TryGetValue(o.Id, out var go) || !go) return false;
            var c = go.GetComponent<OzL07Crystal>();
            if (!c) return false;
            c.Break(o, from, fx, au);
            return true;
        }

        Vector3 Centre()
        {
            var r = body != null && body.Length > 0 && body[0] ? body[0].GetComponent<Renderer>() : null;
            return r ? r.bounds.center : transform.position + Vector3.up;
        }

        void Break(ZObject o, Vector3 from, FxManager fx, CombatAudio au)
        {
            if (Broken) return;
            Broken = true; Breaks++;
            var foot = transform.position; var c = Centre();
            var away = foot - from; away.y = 0f; if (away.sqrMagnitude < 1e-4f) away = Vector3.forward; away.Normalize();
            DetailPropHub.Ensure();
            var hub = DetailPropHub.I;
            foreach (var t in loose)
            {
                if (!t) continue;
                t.gameObject.SetActive(true);
                var v = away * Random.Range(2.5f, 5f) + Vector3.up * Random.Range(3.5f, 6.5f) + Random.insideUnitSphere * 1.2f;
                var w = Random.onUnitSphere * Random.Range(240f, 720f);
                if (hub) hub.Chunks.Throw(t, v, w, Random.Range(9f, 14f), ShardLand); else t.gameObject.SetActive(false);
            }
            foreach (var b in body) if (b) b.SetActive(false);
            if (ruin) ruin.SetActive(true);
            if (bits == null) { bits = new DetailKit.Bits(chips); DetailPropHub.Track(bits); }
            var rng = new System.Random(GetInstanceID() * 31 + Time.frameCount);
            for (int i = 0; i < chips.Length; i++) bits.Burst(c, 14, (byte)i, 3.4f, 0.13f, rng);
            if (fx)
            {
                LegacyMatch.PropDeath(o, foot + Vector3.up * 0.6f, fx);
                fx.DemolitionDust(foot, 1.6f, 1.6f, 0.45f);
            }
            au?.Play("rock", c, 0.8f, 0.05f, -1, "v3-rock-break-a", "v3-rock-break-b");
            if (AudioDirector.Has(BreakEvent)) AudioDirector.Fire(BreakEvent, c, o.Id, 1f, 1.1f);
            else OzSoundLog.Add("L07", BreakEvent, null, "DROP no event", 0f, c);
            OzSoundV3.Debris(c, "stone", 2f, 10);                                                        // the chips land
            LastBreak = name + " broke at " + Time.time.ToString("0.0") + " s";
            Debug.Log("[OpenZ] L07 crystal " + LastBreak);
        }
    }
}
