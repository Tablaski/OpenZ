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
        internal bool ShowsMemory => Fog || OzFogOfWar.Hiding;
        bool CampaignMemory => OzFogOfWar.Wanted;
        double nextContacts = -1;
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
            public bool StartingNeutral;
            public ZObject Known;
            public bool MapFeature => Fixed || StartingNeutral && Team == Team.Neutral;
            public readonly List<Part> Parts = new();
        }
        internal readonly struct Part
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 Transform;
            public readonly Material[] Materials;
            public readonly MaterialPropertyBlock[] Properties;
            public readonly bool OwnsMesh;
            public readonly ShadowCastingMode Shadows;
            public readonly int Layer;
            public Part(Mesh mesh, Matrix4x4 transform, Renderer renderer, bool ownsMesh)
            {
                Mesh = mesh;
                Transform = transform;
                Materials = renderer.sharedMaterials;
                Shadows = renderer.shadowCastingMode;
                Layer = renderer.gameObject.layer;
                Properties = new MaterialPropertyBlock[Materials.Length];
                for (int i = 0; i < Properties.Length; i++)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    if (block.isEmpty) renderer.GetPropertyBlock(block);
                    if (!block.isEmpty) Properties[i] = block;
                }
                OwnsMesh = ownsMesh;
            }
        }

        ZGame game;
        OpenZHud hud;
        RulesView rules;
        ZWorld world;
        Team seat;
        bool mapHasRadar;
        double nextSight = -1;
        readonly Dictionary<int, bool> knownRadars = new();
        readonly List<int> forgotten = new();
        readonly Dictionary<int, (GameObject View, Renderer[] Parts)> meshCache = new();
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
            ClearContacts();
            if (staticImage) Destroy(staticImage.gameObject);
            if (staticPixels) Destroy(staticPixels);
            if (staticAudio) Destroy(staticAudio);
            if (contactMaterial) Destroy(contactMaterial);
            if (contactRing) Destroy(contactRing);
        }
        internal static bool Covers(ZObject obj) => I && I.Fog && obj != null && obj.Owner != I.seat && obj.World?.AreAllied(obj.Owner, I.seat) != true &&
            (obj.IsUnit || obj.Kind == ZKind.Building || obj.Kind == ZKind.MapItem && obj.Type != "flag") &&
            !I.ContactVisible(obj.CenterX, obj.CenterY);

        internal static RadarLossRule RuleFor(ZWorld world) => world.HasDuelRules || world.Map.skirmish ? world.DuelRadarLoss : OzBattleOptions.SoloRadar;

        internal bool Visible(int x, int y) => !Fog || Sight == null || Sight.ShownAtPx(x, y) >= OzFowMask.Revealed;

        void Update()
        {
            if (!game || game.World == null) return;
            hud = OpenZHud.I;
            Team vision = DuelRuntime.I && DuelRuntime.I.MatchGhost ? DuelRuntime.GhostVisionTeam : game.player;
            if (world != game.World || seat != vision)
            {
                if (world != game.World)
                {
                    mapHasRadar = ZBattleSight.MapHasRadar(game.World.Map);
                }
                bool newWorld = world != game.World;
                world = game.World; seat = vision;
                rules = game.GetComponent<RulesView>();
                Sight = null;
                ClearContacts(); knownRadars.Clear();
                nextContacts = -1;
                nextSight = -1;
                if (newWorld && seat != Team.Neutral && (game.IsMayhem || mapHasRadar && ZBattleSight.RadarFog(RuleFor(world))))
                    RecordContacts(true);
            }
            var rule = RuleFor(world);
            bool enabled = mapHasRadar && seat != Team.Neutral && rule != RadarLossRule.Off && !game.World.GameOver && !OzUeHooks.Active &&
                !CaptureKit.DemoRun && !ZodBriefing.BriefingActive && (!DuelRuntime.Active || DuelRuntime.I.Playing);
            if (!enabled)
            {
                Fog = Zoom = Jammed = false;
                if (rule == RadarLossRule.Off) knownRadars.Clear();
                UpdateStatic();
                return;
            }

            int active = 0;
            destroyedRadars.Clear();
            foreach (var obj in world.Objects)
            {
                if (obj.Type != "radar") continue;
                bool ownedAndRunning = obj.ProvidesRadarTo(seat);
                if (ownedAndRunning) active++;
                if (obj.Destroyed && knownRadars.TryGetValue(obj.Id, out bool stillOurs) && stillOurs) destroyedRadars.Add(obj.Id);
                knownRadars[obj.Id] = ownedAndRunning;
            }
            bool wasJammed = Jammed, wasFog = Fog;
            Jammed = active == 0;
            Fog = Jammed && ZBattleSight.RadarFog(rule);
            if (Fog && Sight == null) Sight = new OzFowMask(world.Map.WidthPix, world.Map.HeightPix);
            bool freeGhost = DuelRuntime.I && DuelRuntime.I.MatchGhost && !DuelRuntime.I.GhostFollowing;
            Zoom = !freeGhost && Jammed && ZBattleSight.RadarZoom(rule);
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
                nextSight = world.Time + .05;
                if (Fog) UpdateSight();
                if (wasFog != Fog) nextContacts = -1;
            }
            UpdateStatic();
        }

        void UpdateSight()
        {
            ZBattleSight.StampRadarLoss(world, seat, Sight);
            Sight.ShowAll();
            SightRevision++;
        }

        internal bool ContactVisible(int x, int y) =>
            Fog ? Visible(x, y) : OzFogOfWar.ContactVisible(x, y);

        internal bool ShowContact(int id, Contact contact)
        {
            if (!ShowsMemory || ContactVisible(contact.X, contact.Y)) return false;
            var current = world.Get(id);
            return current == null || !world.AreAllied(current.Owner, seat) &&
                (current.Destroyed || current.KillAt >= 0 || !ContactVisible(current.CenterX, current.CenterY));
        }

        internal bool TryKnownObject(int id, out ZObject known)
        {
            known = null;
            if (!Contacts.TryGetValue(id, out var contact) || !contact.MapFeature || !ShowContact(id, contact)) return false;
            known = contact.Known;
            return true;
        }

        internal static ZObject Presented(ZObject obj) =>
            I && obj != null && I.TryKnownObject(obj.Id, out var known) ? known : obj;

        internal static Team ZoneOwner(ZZone zone) => zone.Flag != null ? Presented(zone.Flag).Owner : zone.Owner;

        internal bool TryAttackContact(ZWaypoint waypoint, out int x, out int y)
        {
            x = y = 0;
            if (waypoint.mode != WpMode.Attack || !Contacts.TryGetValue(waypoint.refId, out var contact) ||
                !ShowContact(waypoint.refId, contact)) return false;
            x = contact.X;
            y = contact.Y;
            return true;
        }

        void ReleaseParts(Contact contact)
        {
            foreach (var part in contact.Parts)
                if (part.OwnsMesh && part.Mesh) Destroy(part.Mesh);
            contact.Parts.Clear();
        }

        void ForgetContact(int id)
        {
            if (Contacts.TryGetValue(id, out var contact)) ReleaseParts(contact);
            Contacts.Remove(id);
            meshCache.Remove(id);
        }

        void ClearContacts()
        {
            foreach (var contact in Contacts.Values) ReleaseParts(contact);
            Contacts.Clear();
            meshCache.Clear();
        }

        void RecordContacts(bool mapStart = false)
        {
            liveViews.Clear();
            foreach (var view in game.LiveViewValues)
            {
                if (!view || view.Obj == null || !view.Obj.IsUnit) continue;
                if (mapStart && !ZBattleSight.KnownAtStart(view.Obj)) continue;
                RecordContact(view.Obj, view.gameObject, mapStart);
            }
            foreach (var obj in world.Objects)
            {
                if (obj.Kind != ZKind.Building && !obj.IsDestroyableImpass &&
                    obj.Type != "flag" && obj.Type != "grenades") continue;
                if (mapStart && !ZBattleSight.KnownAtStart(obj)) continue;
                game.StaticViews.TryGetValue(obj.Id, out var view);
                if (obj.IsBridge && rules) view = rules.ActiveBridgeModel(obj.Id);
                RecordContact(obj, obj.Type == "flag" ? null : view, mapStart);
            }
            if (mapStart) return;
            forgotten.Clear();
            foreach (var pair in Contacts)
            {
                bool present = liveViews.Contains(pair.Key);
                if (pair.Value.Kind == ZKind.Building || pair.Value.Type == "flag" || !ContactVisible(pair.Value.X, pair.Value.Y)) continue;
                var obj = world.Get(pair.Key);
                if (!present || obj == null || !ContactVisible(obj.CenterX, obj.CenterY)) forgotten.Add(pair.Key);
            }
            foreach (int id in forgotten) ForgetContact(id);
        }

        void RecordContact(ZObject obj, GameObject view, bool mapStart)
        {
            liveViews.Add(obj.Id);
            if (!mapStart && !world.AreAllied(obj.Owner, seat) && !ContactVisible(obj.CenterX, obj.CenterY)) return;
            bool fixedObject = !obj.IsUnit;
            if (obj.Kind != ZKind.Building && (obj.Destroyed || obj.KillAt >= 0))
            {
                ForgetContact(obj.Id);
                return;
            }
            if (!Contacts.TryGetValue(obj.Id, out var contact)) Contacts[obj.Id] = contact = new Contact();
            if (fixedObject && contact.Known != null && contact.Team == obj.Owner && contact.Known.Health == obj.Health &&
                (obj.Type == "flag" || meshCache.TryGetValue(obj.Id, out var previous) &&
                    previous.View == view && contact.Parts.Count > 0)) return;
            contact.X = obj.CenterX; contact.Y = obj.CenterY;
            contact.Left = obj.LocX; contact.Top = obj.LocY; contact.Width = obj.WidthPix; contact.Height = obj.HeightPix;
            contact.Type = obj.Type;
            contact.Team = obj.Owner;
            contact.Kind = obj.Kind; contact.Destroyed = obj.Destroyed; contact.Fixed = fixedObject;
            contact.StartingNeutral = (mapStart || contact.StartingNeutral) && obj.IsUnit && obj.Owner == Team.Neutral;
            contact.Known = contact.MapFeature ? new ZObject
            {
                Id = obj.Id, World = world, Kind = obj.Kind, Type = obj.Type, Owner = obj.Owner, U = obj.U,
                LocX = obj.LocX, LocY = obj.LocY, CenterX = obj.CenterX, CenterY = obj.CenterY,
                WidthPix = obj.WidthPix, HeightPix = obj.HeightPix, ZoneIndex = obj.ZoneIndex,
                Health = obj.Health, MaxHealth = obj.MaxHealth, IsDestroyableImpass = obj.IsDestroyableImpass,
                AttackedByExplosives = obj.AttackedByExplosives
            } : null;
            ReleaseParts(contact);
            if (!view) return;
            if (!meshCache.TryGetValue(obj.Id, out var cached) || fixedObject || cached.View != view)
                meshCache[obj.Id] = cached = (view, view.GetComponentsInChildren<Renderer>(true));
            foreach (var renderer in cached.Parts)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                    contact.Parts.Add(new Part(filter.sharedMesh, filter.transform.localToWorldMatrix, renderer, false));
                else if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    var mesh = new Mesh { hideFlags = HideFlags.DontSave };
                    skin.BakeMesh(mesh);
                    contact.Parts.Add(new Part(mesh, skin.transform.localToWorldMatrix, skin, true));
                }
            }
        }

        internal void DrawMemory(Camera cam)
        {
            foreach (var pair in Contacts)
            {
                var item = pair.Value;
                if (!ShowContact(pair.Key, item)) continue;
                foreach (var part in item.Parts)
                {
                    if (!part.Mesh) continue;
                    int count = Math.Min(part.Mesh.subMeshCount, part.Materials.Length);
                    for (int i = 0; i < count; i++)
                        if (part.Materials[i]) Graphics.DrawMesh(part.Mesh, part.Transform, part.Materials[i], part.Layer, cam,
                            i, part.Properties[i], item.MapFeature ? part.Shadows : ShadowCastingMode.Off, item.MapFeature);
                }
                if (!item.MapFeature) DrawContactMarker(item, cam);
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
            if (world == null) return;
            if ((CampaignMemory || mapHasRadar && ZBattleSight.RadarFog(RuleFor(world))) &&
                world.Time >= nextContacts)
            {
                nextContacts = world.Time + .1;
                RecordContacts();
            }
            if (ShowsMemory && cam && cam.TryGetComponent<RtsCamera>(out var rts) && rts.isActiveAndEnabled) DrawMemory(cam);
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
