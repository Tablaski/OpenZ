using System;
using System.Collections.Generic;
using OpenZ.Multiplayer;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OpenZ
{
    [DefaultExecutionOrder(-200)]
    internal sealed class OzRadarLoss : MonoBehaviour
    {
        internal static OzRadarLoss I { get; private set; }
        internal bool Fog { get; private set; }
        internal bool Zoom { get; private set; }
        internal bool Jammed { get; private set; }
        internal OzFowMask Sight { get; private set; }
        internal int SightRevision { get; private set; }
        internal readonly Dictionary<int, Contact> Contacts = new();
        internal sealed class Contact
        {
            public int X, Y, Left, Top, Width, Height;
            public string Type;
            public Team Team;
            public ZKind Kind;
            public bool Destroyed;
            public bool Fixed;
            public readonly List<Part> Parts = new();
        }
        internal readonly struct Part
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 Transform;
            public readonly Material[] Materials;
            public Part(Mesh mesh, Matrix4x4 transform, Material[] materials)
            { Mesh = mesh; Transform = transform; Materials = materials; }
        }

        ZGame game;
        OpenZHud hud;
        ZWorld world;
        Team seat;
        double nextSight = -1;
        readonly Dictionary<int, bool> knownRadars = new();
        readonly List<int> forgotten = new();
        readonly Dictionary<int, MeshFilter[]> meshCache = new();
        readonly HashSet<int> liveViews = new();
        readonly List<int> destroyedRadars = new();
        float staticEnds, staticLength;
        RawImage staticImage;
        Texture2D staticPixels;
        AudioClip staticAudio;
        AudioSource staticSource;
        readonly Color32[] pixels = new Color32[96 * 54];
        readonly System.Random noise = new();

        internal static void Install(ZGame owner)
        {
            if (!owner || owner.TryGetComponent<OzRadarLoss>(out _)) return;
            owner.gameObject.AddComponent<OzRadarLoss>().game = owner;
        }
        void OnEnable() { I = this; }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (staticImage) Destroy(staticImage.gameObject);
            if (staticPixels) Destroy(staticPixels);
            if (staticAudio) Destroy(staticAudio);
        }
        internal static bool Covers(ZObject obj) => I && I.Fog && obj != null && obj.Owner != I.seat &&
            (obj.IsUnit || obj.Kind == ZKind.Building || obj.Kind == ZKind.MapItem && obj.Type != "flag") &&
            !I.Visible(obj.CenterX, obj.CenterY);

        internal bool Visible(int x, int y) => !Fog || Sight == null || Sight.ShownAtPx(x, y) >= OzFowMask.Revealed;

        void Update()
        {
            if (!game || game.World == null) return;
            hud = OpenZHud.I;
            if (world != game.World || seat != game.player)
            {
                world = game.World; seat = game.player;
                Sight = null;
                Contacts.Clear(); knownRadars.Clear(); meshCache.Clear();
                nextSight = -1;
            }
            var rule = world.DuelRadarLoss;
            bool enabled = rule != RadarLossRule.Off && !game.World.GameOver && !OzUeHooks.Active &&
                !CaptureKit.DemoRun && !ZodBriefing.BriefingActive && (!DuelRuntime.Active || DuelRuntime.I.Playing);
            if (!enabled)
            {
                Fog = Zoom = Jammed = false;
                if (rule == RadarLossRule.Off) { knownRadars.Clear(); Contacts.Clear(); }
                UpdateStatic();
                return;
            }

            int active = 0;
            destroyedRadars.Clear();
            foreach (var obj in world.Objects)
            {
                if (obj.Type != "radar") continue;
                bool ownedAndRunning = obj.Owner == seat && !obj.Destroyed;
                if (ownedAndRunning) active++;
                if (obj.Destroyed && knownRadars.TryGetValue(obj.Id, out bool stillOurs) && stillOurs) destroyedRadars.Add(obj.Id);
                knownRadars[obj.Id] = ownedAndRunning;
            }
            bool wasJammed = Jammed, wasFog = Fog;
            Jammed = active == 0;
            Fog = Jammed && (rule == RadarLossRule.Fog || rule == RadarLossRule.FogAndZoom);
            if (Fog && Sight == null) Sight = new OzFowMask(world.Map.WidthPix, world.Map.HeightPix);
            Zoom = Jammed && (rule == RadarLossRule.Zoom || rule == RadarLossRule.FogAndZoom);
            if (Jammed && !wasJammed && hud) hud.ClearRadarCommunications();
            if (destroyedRadars.Count > 0)
            {
                bool last = active == 0;
                foreach (int id in destroyedRadars)
                {
                    var radar = world.GetAny(id);
                    if (radar != null && hud) hud.RadarDestroyed(radar.CenterX, radar.CenterY, last);
                }
                StartStatic(last);
            }
            if (wasFog != Fog || world.Time >= nextSight)
            {
                nextSight = world.Time + .15;
                if (Fog) UpdateSight();
                if (rule == RadarLossRule.Fog || rule == RadarLossRule.FogAndZoom) RecordContacts();
                else Contacts.Clear();
            }
            UpdateStatic();
        }

        void UpdateSight()
        {
            Sight.Clear();
            foreach (var zone in world.Zones)
            {
                if (zone.Owner != seat) continue;
                int left = Sight.CellX(zone.X), right = Sight.CellX(zone.X + zone.W - 1);
                int top = Sight.CellY(zone.Y), bottom = Sight.CellY(zone.Y + zone.H - 1);
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++) Sight.Target[y * Sight.W + x] = 255;
            }
            foreach (var unit in world.Objects)
                if (unit.Owner == seat && unit.IsUnit && !unit.Destroyed)
                    Sight.Stamp(unit.CenterX, unit.CenterY, OzFogOfWar.SightPx(unit, world.S.agro_distance));
            Sight.ShowAll();
            SightRevision++;
        }

        void RecordContacts()
        {
            liveViews.Clear();
            foreach (var view in game.LiveViewValues)
            {
                if (!view || view.Obj == null) continue;
                var obj = view.Obj;
                bool fixedObject = obj.Kind == ZKind.Building || obj.Kind == ZKind.MapItem && obj.Type != "flag";
                if (!fixedObject && (obj.Owner == seat || obj.Owner == Team.Neutral || !obj.IsUnit)) continue;
                liveViews.Add(obj.Id);
                if (!Visible(obj.CenterX, obj.CenterY)) continue;
                if (obj.Destroyed && !fixedObject) { Contacts.Remove(obj.Id); continue; }
                if (!Contacts.TryGetValue(obj.Id, out var contact)) Contacts[obj.Id] = contact = new Contact();
                if (fixedObject && contact.Parts.Count > 0 && contact.Destroyed == obj.Destroyed && contact.Team == obj.Owner) continue;
                contact.X = obj.CenterX; contact.Y = obj.CenterY;
                contact.Left = obj.LocX; contact.Top = obj.LocY; contact.Width = obj.WidthPix; contact.Height = obj.HeightPix;
                contact.Type = obj.Type;
                contact.Team = obj.Owner; contact.Kind = obj.Kind; contact.Destroyed = obj.Destroyed; contact.Fixed = fixedObject;
                contact.Parts.Clear();
                if (!meshCache.TryGetValue(obj.Id, out var filters) || fixedObject) meshCache[obj.Id] = filters = view.GetComponentsInChildren<MeshFilter>(true);
                foreach (var filter in filters)
                    if (filter && filter.sharedMesh && filter.gameObject.activeInHierarchy && filter.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled)
                        contact.Parts.Add(new Part(filter.sharedMesh, filter.transform.localToWorldMatrix, renderer.sharedMaterials));
            }
            forgotten.Clear();
            foreach (var pair in Contacts)
            {
                bool present = liveViews.Contains(pair.Key);
                if (!present && Visible(pair.Value.X, pair.Value.Y)) forgotten.Add(pair.Key);
                else if (!pair.Value.Fixed && Visible(pair.Value.X, pair.Value.Y))
                {
                    var obj = world.Get(pair.Key);
                    if (obj == null || !Visible(obj.CenterX, obj.CenterY)) forgotten.Add(pair.Key);
                }
            }
            foreach (int id in forgotten) { Contacts.Remove(id); meshCache.Remove(id); }
        }

        internal void DrawMemory(Camera cam)
        {
            foreach (var item in Contacts.Values)
            {
                if (Visible(item.X, item.Y)) continue;
                foreach (var part in item.Parts)
                {
                    if (!part.Mesh) continue;
                    int count = Math.Min(part.Mesh.subMeshCount, part.Materials.Length);
                    for (int i = 0; i < count; i++)
                        if (part.Materials[i]) Graphics.DrawMesh(part.Mesh, part.Transform, part.Materials[i], 0, cam,
                            i, null, ShadowCastingMode.Off, false);
                }
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (Fog && cam && cam.TryGetComponent<RtsCamera>(out var rts) && rts.isActiveAndEnabled) DrawMemory(cam);
        }

        void StartStatic(bool last)
        {
            if (!hud || !hud.Root) return;
            if (!staticImage)
            {
                staticImage = Hud.Node("Radar static", hud.Root).gameObject.AddComponent<RawImage>();
                staticImage.rectTransform.Fill();
                staticImage.raycastTarget = false;
                staticPixels = new Texture2D(96, 54, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                staticImage.texture = staticPixels;
            }
            if (!staticAudio)
            {
                staticAudio = AudioClip.Create("Radar static", 24000, 1, 24000, false);
                var samples = new float[24000];
                for (int i = 0; i < samples.Length; i++) samples[i] = ((float)noise.NextDouble() * 2 - 1) * Mathf.Min(1, (samples.Length - i) / 2500f);
                staticAudio.SetData(samples, 0);
                staticSource = game.gameObject.AddComponent<AudioSource>();
                staticSource.clip = staticAudio;
                staticSource.playOnAwake = false;
            }
            staticLength = last ? .85f : .28f;
            staticEnds = Time.unscaledTime + staticLength;
            staticSource.volume = PlayerPrefs.GetFloat("oz_vol_effects", 1) * (last ? .085f : .06f);
            staticSource.pitch = last ? 1.18f : 3.57f;
            staticSource.Play();
        }

        void UpdateStatic()
        {
            if (!staticImage) return;
            float left = staticEnds - Time.unscaledTime;
            staticImage.enabled = left > 0;
            if (left <= 0) return;
            float opacity = Mathf.Clamp01(left / staticLength) * (OzMotion.Reduced ? .2f : .46f);
            staticImage.color = new Color(1, 1, 1, opacity);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte luma = (byte)noise.Next(256);
                pixels[i] = new Color32(luma, luma, luma, 255);
            }
            staticPixels.SetPixels32(pixels);
            staticPixels.Apply(false, false);
        }
    }
}
