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
        Material contactMaterial;
        Mesh contactRing;
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
            if (contactMaterial) Destroy(contactMaterial);
            if (contactRing) Destroy(contactRing);
        }
        internal static bool Covers(ZObject obj) => I && I.Fog && obj != null && obj.Owner != I.seat &&
            (obj.IsUnit || obj.Kind == ZKind.Building || obj.Kind == ZKind.MapItem && obj.Type != "flag") &&
            !I.Visible(obj.CenterX, obj.CenterY);

        internal static RadarLossRule RuleFor(ZWorld world) => world.HasDuelRules || world.Map.skirmish ? world.DuelRadarLoss : OzBattleOptions.SoloRadar;

        internal bool Visible(int x, int y) => !Fog || Sight == null || Sight.ShownAtPx(x, y) >= OzFowMask.Revealed;

        void Update()
        {
            if (!game || game.World == null) return;
            hud = OpenZHud.I;
            Team vision = DuelRuntime.I && DuelRuntime.I.RoomObserver ? DuelRuntime.GhostVisionTeam : game.player;
            if (world != game.World || seat != vision)
            {
                world = game.World; seat = vision;
                Sight = null;
                Contacts.Clear(); knownRadars.Clear(); meshCache.Clear();
                nextSight = -1;
            }
            var rule = RuleFor(world);
            bool enabled = seat != Team.Neutral && rule != RadarLossRule.Off && !game.World.GameOver && !OzUeHooks.Active &&
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
                if (!view || view.Obj == null || view.Obj.Owner == seat || view.Obj.Owner == Team.Neutral || !view.Obj.IsUnit) continue;
                RecordContact(view.Obj, view.gameObject, false);
            }
            foreach (var entry in game.StaticViews)
            {
                if (!entry.Value) continue;
                var obj = world.Get(entry.Key);
                if (obj != null && (obj.Kind == ZKind.Building || obj.Kind == ZKind.MapItem && obj.Type != "flag"))
                    RecordContact(obj, entry.Value, true);
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

        void RecordContact(ZObject obj, GameObject view, bool fixedObject)
        {
            liveViews.Add(obj.Id);
            if (!Visible(obj.CenterX, obj.CenterY)) return;
            if (!fixedObject && obj.Destroyed) { Contacts.Remove(obj.Id); return; }
            if (!Contacts.TryGetValue(obj.Id, out var contact)) Contacts[obj.Id] = contact = new Contact();
            if (fixedObject && contact.Parts.Count > 0 && contact.Destroyed == obj.Destroyed && contact.Team == obj.Owner) return;
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
                if (!item.Fixed) DrawContactMarker(item, cam);
            }
        }

        void DrawContactMarker(Contact item, Camera cam)
        {
            if (!contactMaterial)
            {
                var shader = Shader.Find("OpenZ/Overlay");
                if (!shader) return;
                contactMaterial = new Material(shader) { color = new Color(.9f, .9f, .9f, .8f), hideFlags = HideFlags.DontSave };
                const int sides = 24;
                var vertices = new Vector3[sides * 2];
                var colors = new Color32[vertices.Length];
                var triangles = new List<int>(sides * 6);
                for (int i = 0; i < sides; i++)
                {
                    float angle = Mathf.PI * 2 * i / sides;
                    var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    int a = i * 2, b = (a + 2) % vertices.Length;
                    vertices[a] = direction;
                    vertices[a + 1] = direction * .72f;
                    colors[a] = colors[a + 1] = new Color32(255, 255, 255, 255);
                    if (i % 3 == 2) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(b); triangles.Add(b + 1); triangles.Add(a + 1);
                }
                contactRing = new Mesh { name = "Last known position", vertices = vertices, colors32 = colors, triangles = triangles.ToArray() };
            }
            var position = ZGame.PxToWorld(item.X, item.Y);
            position.y = game.GroundY(position.x, position.z) + .09f;
            float size = item.Kind == ZKind.Robot ? 1.1f : 1.8f;
            Graphics.DrawMesh(contactRing, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * size), contactMaterial, 0,
                cam, 0, null, ShadowCastingMode.Off, false);
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
