using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBigTanksFx
    {
        /// <summary>One big tank's dust, exhaust and lamps (OzBigTanksFx.cs says what and why).</summary>
        public sealed partial class Tank                                       // 1.5 big_tanks_dust_redo: partial (OzBigTanksFx.DustRedo.cs)
        {
            public UnitView view; public ZObject obj; public string type;
            OzBigTanksFx fx;
            Transform root, chassis, turret;                                   // the model's nodes the holders follow
            Renderer hullR, turretR;                                           // the parts are shown while these are
            Transform hRoot, hHull, hTurret;                                   // the holders (children of OZ_BigTanksFx, never of the tank)
            readonly List<Renderer> rootStatic = new List<Renderer>(), hullStatic = new List<Renderer>(), turretStatic = new List<Renderer>();
            VehicleMotion vm; TeamPaint tp; OzBigTankExhaust exh;
            // neon
            readonly List<Renderer> neonGlow = new List<Renderer>(); float[] neonShown;
            /// <summary>
            /// neon runs' channel and tube meshes, drawn only while the lamps are on (night); neonVis eases 0 (day: no strip drawn at all) .. 1.</summary>
            readonly List<Renderer> neonStatic = new List<Renderer>(); float neonVis;
            // markers
            readonly List<Renderer> markGlow = new List<Renderer>(), markHalo = new List<Renderer>(); float[] markShown, markHaloShown; readonly List<bool> markOnHull = new List<bool>();
            // searchlight
            Transform sYaw, sPitch, sBeamT, sPoolT; Renderer sLens, sHalo, sBeam, sPool; Light sLight; NightFixture sFix;
            float sLevel, sYawOff, sPitchNow = 62f, sLensShown = -1f, sHaloShown = -1f, sBeamShown = -1f, sPoolShown = -1f, sPoolDist = 6f;
            // beacon (Bizon)
            Transform bSpin; Renderer bBulb, bHalo; Light bLight; NightFixture bFix; float bLevel, bAngle, bBulbShown = -1f, bHaloShown = -1f;
            // exhaust
            Transform[] mouths = new Transform[0]; float smokeAcc;
            // dust
            Vector3 lastPos; bool hasLast, wasMoving; float dustDist, lastYaw, yawRate, startRing;
            // state
            float power; bool deadDone; public bool broken;
            readonly float seed;

            /// <summary>Play check: the lamps' power (0..1), the searchlight's and the beacon's level, neon runs and pads built, puffs.</summary>
            public float Power => power;
            public float SearchLevel => sLevel;
            public float BeaconLevel => bLevel;
            public int NeonRuns, PadCount, Smoke, Dust;
            public Transform Holder(string anchor) => anchor == "turret" ? hTurret : anchor == "hull" ? hHull : hRoot;
            public Light SearchLight => sLight; public Light BeaconLight => bLight;
            public IReadOnlyList<Renderer> NeonGlow => neonGlow;

            Tank(float s) { seed = s; }

            static Transform Find(Transform t, string n)
            {
                if (t.name == n) return t;
                foreach (Transform c in t) { var f = Find(c, n); if (f) return f; }
                return null;
            }

            public static Tank Make(OzBigTanksFx fx, UnitView v)
            {
                var o = v.Obj; bool mam = o.Type == OzBigTanks.Mammoth;
                var root = Find(v.transform, "TankRoot"); if (!root) { var su = Find(v.transform, "TankSuspension"); if (su) root = su.parent; }   // the prefab renames the GLB's TankRoot "Model" (Editor/OzBigTanksSetup.cs)
                var ch = Find(v.transform, "TankChassis"); var tur = Find(v.transform, mam ? "MammothTurretYaw" : "BizonTurretYaw");
                var hull = Find(v.transform, mam ? "Mammoth_hull" : "Bizon_hull"); var tm = Find(v.transform, mam ? "Mammoth_turret" : "Bizon_turret");
                if (!root || !ch || !tur || !hull || !tm) { Last = $"{o.Type} #{o.Id}: model nodes missing (TankRoot / TankChassis / turret / hull)"; return null; }
                var t = new Tank((o.Id * 0.6180339f) % 1f) { fx = fx, view = v, obj = o, type = o.Type, root = root, chassis = ch, turret = tur, hullR = hull.GetComponent<Renderer>(), turretR = tm.GetComponent<Renderer>() };
                t.vm = v.GetComponent<VehicleMotion>(); t.tp = v.GetComponent<TeamPaint>();
                t.hRoot = Holder(fx, o, "Root"); t.hHull = Holder(fx, o, "Hull"); t.hTurret = Holder(fx, o, "Turret");
                t.Follow();
                t.Build(mam);
                Last = $"{o.Type} #{o.Id}: {t.NeonRuns} neon runs, {t.PadCount} pads, {t.mouths.Length} exhaust mouths";
                return t;
            }

            static Transform Holder(OzBigTanksFx fx, ZObject o, string n)
            {
                var h = new GameObject($"OZ_BigTankFx_{o.Type}_{o.Id}_{n}").transform; h.SetParent(fx.transform, false); return h;
            }

            public void Dispose()
            {
                if (sFix != null) NightBudgetShare.Fixtures.Remove(sFix);
                if (bFix != null) NightBudgetShare.Fixtures.Remove(bFix);
                if (hRoot) Object.Destroy(hRoot.gameObject); if (hHull) Object.Destroy(hHull.gameObject); if (hTurret) Object.Destroy(hTurret.gameObject);
                if (sPoolT) Object.Destroy(sPoolT.gameObject);
                DustFxRelease();                                                   // 1.5 big_tanks_dust_redo: removed (REMOVE / CLEAR FIELD / gone): fade out, then destroyed
            }

            void Follow()
            {
                Put(hRoot, root); Put(hHull, chassis); Put(hTurret, turret);
            }

            static void Put(Transform h, Transform a)
            {
                if (!h || !a) return;
                h.SetPositionAndRotation(a.position, a.rotation);
                var s = a.lossyScale; if ((h.localScale - s).sqrMagnitude > 1e-8f) h.localScale = s;
            }

            // ------------------------------------------------------------------ build
            GameObject Part(string name, string anchor, Mesh mesh, Material mat, bool shadows, Vector3 pos, Quaternion rot, Vector3 scale, bool statik)
            {
                var go = NightMeshes.Part(name, Holder(anchor), mesh, mat, shadows, pos, rot, scale);
                go.layer = view.gameObject.layer;
                if (statik) (anchor == "turret" ? turretStatic : anchor == "hull" ? hullStatic : rootStatic).Add(go.GetComponent<Renderer>());
                PartsBuilt++;
                return go;
            }

            /// <summary>The measured pad (a short gunmetal block from its foot to its top); returns the top's position in the anchor frame.</summary>
            Vector3 PadAt(string name, Pad p, float widthK = 2f)
            {
                var c = p.top - Vector3.up * (p.height * 0.5f);
                Part(name + "_Pad", p.anchor, NightMeshes.Cube(), NightMeshes.Housing(), true, c, Quaternion.identity, new Vector3(p.radius * widthK, p.height, p.radius * widthK), true);
                PadCount++; PadsBuilt++;
                return p.top;
            }

            void Build(bool mam)
            {
                BuildHeadLamps(mam, (n, m, mt, sh, p, r, s) => Part(n, "hull", m, mt, sh, p, r, s, true));
                // NEON: a dark channel on the panel, the frosted tube in it, the glow over the tube
                var runs = mam ? MammothNeon : BizonNeon;
                foreach (var n in runs)
                {
                    float len = n.z1 - n.z0; if (len < 0.2f) continue;
                    var mid = new Vector3(n.x, n.y, (n.z0 + n.z1) * 0.5f);
                    var face = mid - Vector3.right * (n.side * 0.006f);                                        // the panel's face
                    var ch = Part("Neon_Channel", "root", NightMeshes.Cube(), NightMeshes.Housing(), false, face + Vector3.right * (n.side * 0.006f), Quaternion.identity, new Vector3(0.012f, NeonH + 0.016f, len + 0.02f), false).GetComponent<Renderer>();
                    var tb = Part("Neon_Tube", "root", NightMeshes.Cube(), NightMeshes.DarkGlass(), false, mid + Vector3.right * (n.side * 0.006f), Quaternion.identity, new Vector3(NeonD, NeonH, len), false).GetComponent<Renderer>();
                    ch.enabled = tb.enabled = false; neonStatic.Add(ch); neonStatic.Add(tb);                    // N19: drawn only at night (Neon)
                    var g = Part("Neon_Glow", "root", NightMeshes.Cube(), NightMeshes.Bulb(), false, mid + Vector3.right * (n.side * 0.0075f), Quaternion.identity, new Vector3(NeonD + 0.004f, NeonH + 0.004f, len + 0.004f), false);
                    var r = g.GetComponent<Renderer>(); r.enabled = false; neonGlow.Add(r);
                    NeonRuns++;
                }
                neonShown = new float[neonGlow.Count]; for (int i = 0; i < neonShown.Length; i++) neonShown[i] = -1f;
                // TEAM MARKERS: a small dark glass dome on a pad, its LED glow and a halo
                var marks = mam ? new[] { MammothMarkerL, MammothMarkerR } : new[] { BizonMarkerL, BizonMarkerR };
                for (int i = 0; i < marks.Length; i++)
                {
                    var top = PadAt("Marker" + i, marks[i], 2.2f);
                    Part("Marker_Base", marks[i].anchor, NightMeshes.BeaconBase(), NightMeshes.Housing(), true, top, Quaternion.identity, Vector3.one * 0.32f, true);
                    var dome = top + Vector3.up * (0.16f * 0.32f + 0.03f);
                    Part("Marker_Glass", marks[i].anchor, NightMeshes.Sphere(), LedGlass, false, dome, Quaternion.identity, Vector3.one * 0.038f, true);
                    var gl = Part("Marker_Glow", marks[i].anchor, NightMeshes.Sphere(), NightMeshes.Bulb(), false, dome, Quaternion.identity, Vector3.one * 0.042f, false).GetComponent<Renderer>();
                    var ha = Part("Marker_Halo", marks[i].anchor, NightMeshes.Quad(), NightMeshes.Halo(), false, dome, Quaternion.identity, Vector3.one * 0.28f, false).GetComponent<Renderer>();
                    gl.enabled = ha.enabled = false; markGlow.Add(gl); markHalo.Add(ha); markOnHull.Add(marks[i].anchor != "turret");
                }
                markShown = new float[markGlow.Count]; markHaloShown = new float[markGlow.Count];
                for (int i = 0; i < markShown.Length; i++) markShown[i] = markHaloShown[i] = -1f;
                // SEARCHLIGHT on the turret roof
                BuildSearch(PadAt("Search", mam ? MammothSearch : BizonSearch, 1.9f), mam ? MammothSearch.anchor : BizonSearch.anchor);
                // BIZON: the amber work beacon
                if (!mam) BuildBeacon(PadAt("Beacon", BizonBeacon, 2f), BizonBeacon.anchor);
                // EXHAUST: the Bizon's own pipes (MK_Exhaust_L / _R); the Mammoth's two rear deck stubs (built here)
                exh = view.GetComponent<OzBigTankExhaust>();
                if (mam)
                {
                    var ms = new List<Transform>();
                    foreach (var p in new[] { MammothExhaustL, MammothExhaustR })
                    {
                        var top = PadAt("Exhaust", p, 2f);
                        Part("Exhaust_Stub", p.anchor, Pipe(), NightMeshes.Housing(), true, top, Quaternion.identity, new Vector3(0.17f, 0.30f, 0.17f), true);
                        Part("Exhaust_Bore", p.anchor, Pipe(), Soot, false, top + Vector3.up * 0.002f, Quaternion.identity, new Vector3(0.12f, 0.30f, 0.12f), true);
                        var m = new GameObject("Exhaust_Mouth").transform; m.SetParent(hHull, false); m.localPosition = top + Vector3.up * 0.32f; ms.Add(m);
                    }
                    mouths = ms.ToArray();
                    if (!exh) exh = view.gameObject.AddComponent<OzBigTankExhaust>();                       // its bursts and the diesel chuff (it brings its OzShuttleExhaust)
                }
                else
                {
                    var l = Find(view.transform, "MK_Exhaust_L"); var r = Find(view.transform, "MK_Exhaust_R");
                    mouths = l && r ? new[] { l, r } : new Transform[0];
                }
                var se = view.GetComponent<OzShuttleExhaust>();
                if (se) { if (se.mouths == null || se.mouths.Length == 0) se.mouths = mouths; se.enabled = false; }      // the faint wisps: this kit draws the smoke
            }

            void BuildSearch(Vector3 top, string anchor)
            {
                float s = SearchScale; var H = NightMeshes.Housing();
                sYaw = new GameObject("Search_Yaw").transform; sYaw.SetParent(Holder(anchor), false);
                sYaw.localPosition = top + Vector3.up * (0.33f * s); sYaw.localRotation = Quaternion.Euler(0f, 180f, 0f); sYaw.localScale = Vector3.one * s;
                sYaw.gameObject.layer = view.gameObject.layer;
                var yoke = NightMeshes.Part("Search_Yoke", sYaw, NightMeshes.SearchYoke(), H, true, Vector3.zero, Quaternion.identity, Vector3.one);
                sPitch = new GameObject("Search_Pitch").transform; sPitch.SetParent(sYaw, false); sPitch.localPosition = new Vector3(0f, 0.02f, 0f);
                var drum = NightMeshes.Part("Search_Drum", sPitch, NightMeshes.SearchDrum(), H, true, Vector3.zero, Quaternion.identity, Vector3.one);
                var glass = NightMeshes.Part("Search_Glass", sPitch, NightMeshes.Disc(), NightMeshes.DarkGlass(), false, new Vector3(0f, 0f, 0.335f), Quaternion.identity, new Vector3(0.31f, 0.31f, 1f));
                var l = (anchor == "turret" ? turretStatic : hullStatic);
                l.Add(yoke.GetComponent<Renderer>()); l.Add(drum.GetComponent<Renderer>()); l.Add(glass.GetComponent<Renderer>());
                sLens = NightMeshes.Part("Search_Lens", sPitch, NightMeshes.Disc(), NightMeshes.Lens(), false, new Vector3(0f, 0f, 0.345f), Quaternion.identity, new Vector3(0.3f, 0.3f, 1f)).GetComponent<Renderer>();
                sHalo = NightMeshes.Part("Search_Halo", sPitch, NightMeshes.Quad(), NightMeshes.Halo(), false, new Vector3(0f, 0f, 0.5f), Quaternion.identity, Vector3.one * 1.3f).GetComponent<Renderer>();
                var b = NightMeshes.Part("Search_Beam", sPitch, NightMeshes.Cone(), NightMeshes.Beam(), false, new Vector3(0f, 0f, 0.36f), Quaternion.identity, Vector3.one);
                sBeam = b.GetComponent<Renderer>(); sBeamT = b.transform;
                foreach (var t in sYaw.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = view.gameObject.layer;
                sLens.enabled = sHalo.enabled = sBeam.enabled = false;
                var lg = new GameObject("Search_Light"); lg.transform.SetParent(sPitch, false); lg.transform.localPosition = new Vector3(0f, 0f, 0.38f);
                sLight = lg.AddComponent<Light>();
                sLight.type = LightType.Spot; sLight.range = SearchRange; sLight.color = new Color(0.86f, 0.92f, 1f); sLight.intensity = 0f; sLight.enabled = false;
                sLight.shadows = LightShadows.None; sLight.renderMode = LightRenderMode.ForcePixel; sLight.spotAngle = SearchAngle; sLight.innerSpotAngle = SearchAngle * 0.35f;
                sFix = new NightFixture
                {
                    Kind = NightKind.Searchlight, Id = $"bigtank_search_{obj.Id}", Light = sLight, Root = sYaw, Col = sLight.color, ColLinear = sLight.color.linear,
                    Intensity = SearchIntensity, Range = SearchRange, Angle = SearchAngle, Priority = SearchPriority, ShadowCandidate = false, BeamK = 1f, PoolK = 1f,
                };
                NightBudgetShare.Fixtures.Add(sFix);
                var pg = NightMeshes.Part($"OZ_BigTankFx_{obj.Id}_SearchPool", fx.transform, NightMeshes.Cube(), NightMeshes.Pool(), false, Vector3.zero, Quaternion.identity, Vector3.one);
                pg.layer = view.gameObject.layer; sPoolT = pg.transform; sPool = pg.GetComponent<Renderer>(); sPool.enabled = false;
                sPitch.localRotation = Quaternion.Euler(sPitchNow, 0f, 0f);
                PartsBuilt += 7;
            }

            void BuildBeacon(Vector3 top, string anchor)
            {
                const float s = 0.55f;
                Part("Beacon_Base", anchor, NightMeshes.BeaconBase(), NightMeshes.Housing(), true, top, Quaternion.identity, Vector3.one * s, true);
                var c = top + Vector3.up * (0.16f * s + 0.07f);
                Part("Beacon_Dome", anchor, NightMeshes.Sphere(), AmberDome, false, c, Quaternion.identity, new Vector3(0.075f, 0.085f, 0.075f), true);
                bSpin = new GameObject("Beacon_Spin").transform; bSpin.SetParent(Holder(anchor), false); bSpin.localPosition = c;
                bBulb = NightMeshes.Part("Beacon_Reflector", bSpin, NightMeshes.Quad(), NightMeshes.Bulb(), false, new Vector3(0f, 0f, 0.012f), Quaternion.identity, new Vector3(0.05f, 0.06f, 1f)).GetComponent<Renderer>();
                bHalo = NightMeshes.Part("Beacon_Halo", bSpin, NightMeshes.Quad(), NightMeshes.Halo(), false, new Vector3(0f, 0f, 0.09f), Quaternion.identity, Vector3.one * 0.55f).GetComponent<Renderer>();
                foreach (var t in bSpin.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = view.gameObject.layer;
                bBulb.enabled = bHalo.enabled = false;
                var lg = new GameObject("Beacon_Light"); lg.transform.SetParent(bSpin, false); lg.transform.localPosition = new Vector3(0f, 0.6f, 0.05f); lg.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
                bLight = lg.AddComponent<Light>();
                bLight.type = LightType.Spot; bLight.range = 9f; bLight.spotAngle = 42f; bLight.innerSpotAngle = 14f; bLight.color = new Color(1f, 0.55f, 0.1f); bLight.intensity = 0f; bLight.enabled = false;
                bLight.shadows = LightShadows.None; bLight.renderMode = LightRenderMode.ForcePixel;
                bFix = new NightFixture
                {
                    Kind = NightKind.WorkBeacon, Id = $"bigtank_beacon_{obj.Id}", Light = bLight, Root = bSpin, Col = bLight.color, ColLinear = bLight.color.linear,
                    Intensity = 16f, Range = 9f, Angle = 42f, Priority = 1.6f, ShadowCandidate = false, BeamK = 0f, PoolK = 0f,
                };
                NightBudgetShare.Fixtures.Add(bFix);
                PartsBuilt += 4;
            }

            // ------------------------------------------------------------------ every frame
            public void Broken()
            {
                broken = true;
                power = 0f;
            }

            static bool Visible(Renderer r) => r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff;

            static void ShowAll(List<Renderer> l, bool on) { for (int i = 0; i < l.Count; i++) { var r = l[i]; if (r && r.enabled != on) r.enabled = on; } }

            public void Step(float dt)
            {
                if (!view || obj == null) return;
                Follow();
                bool hullShown = !broken && Visible(hullR) && view.isActiveAndEnabled, turShown = Visible(turretR) && view.isActiveAndEnabled;
                ShowAll(rootStatic, hullShown); ShowAll(hullStatic, hullShown); ShowAll(turretStatic, turShown);
                bool dead = obj.Destroyed || view.Dying || broken;
                bool crewed = obj.Owner != Team.Neutral && obj.Drivers.Count > 0;
                float was = power;
                if (dead)
                {
                    if (!deadDone) { deadDone = true; if (power > 0.05f) { LampDeaths++; if (AudioDirector.Has(LampOffEvent)) AudioDirector.Fire(LampOffEvent, hTurret.position + Vector3.up * 0.5f, obj.Id, 0.45f); } }
                    power = 0f;
                }
                else power = Mathf.MoveTowards(power, crewed && hullShown ? 1f : 0f, dt / (crewed ? 0.25f : 0.4f));
                if (was <= 0.001f && power > 0.001f && !dead)
                {
                    NeonSwitchOns++;
                    if (AudioDirector.Has(NeonOnEvent)) AudioDirector.Fire(NeonOnEvent, hRoot.position + Vector3.up, obj.Id, 0.3f, 1.15f);
                }
                bool night = Night(out float nightK);
                Neon(dt, night, nightK, hullShown);
                Markers(nightK, hullShown, turShown);
                Search(dt, night, nightK, turShown && !dead);
                if (bSpin) Beacon(dt, nightK, turShown && !dead);
                if (OzBigTanksDust.On) DustFxStep(dt, dead, crewed, hullShown);                                  // 1.5 big_tanks_dust_redo: the tank's own lit flipbook dust + exhaust (OzBigTanksDust)
                else if (!dead && crewed && hullShown) { Exhaust(dt); DustStep(dt); } else { hasLast = false; wasMoving = false; }
            }

            /// <summary>The drawn team's glow colour, eased with the vehicle colour fill (TeamPaint.Fill), linear.</summary>
            Color TeamGlow()
            {
                if (!tp) return Color.black;
                Vector4 to = TeamColors.GlowLinear(tp.Team);
                if (tp.Filling) { Vector4 from = TeamColors.GlowLinear(tp.FillFrom); to = Vector4.Lerp(from, to, tp.FillShare); }
                return new Color(to.x, to.y, to.z, 1f);
            }

            /// <summary>N19: by day no strip at all (channel, tube and glow hidden); at night (the lamps' clock) as before, eased in / out over
            /// <see cref="NeonFadeSec"/> (the glow with it; the strip meshes drawn while any of it shows). NeonDayHidden off = shown by day as before.</summary>
            void Neon(float dt, bool night, float nightK, bool shown)
            {
                if (!NeonDayHidden) neonVis = 1f;
                else if (dt <= 0f) neonVis = night ? 1f : 0f;                                                  // the clock stopped (pause, a switch): at once
                else neonVis = Mathf.MoveTowards(neonVis, night ? 1f : 0f, dt / Mathf.Max(0.01f, NeonFadeSec));
                bool vis = shown && neonVis > 0.001f;
                ShowAll(neonStatic, vis);
                var c = TeamGlow() * (Mathf.Lerp(NeonDay, NeonNight, nightK) * power * neonVis * (vis ? 1f : 0f));
                for (int i = 0; i < neonGlow.Count; i++) fx.Glow(neonGlow[i], c, ref neonShown[i]);
            }

            void Markers(float nightK, bool hullShown, bool turShown)
            {
                var cam = Camera.main; float tt = Time.time + seed * 1.7f;
                float blink = (tt % 1.1f) < 0.16f ? 1f : 0.12f;                                                     // a short flash, a faint glow between
                var col = TeamGlow();
                var nl = NightLights.I; float haloGain = nl ? nl.haloGain : 0.35f; bool halos = !nl || nl.halos;
                for (int i = 0; i < markGlow.Count; i++)
                {
                    bool sh = markOnHull[i] ? hullShown : turShown;
                    float k = power * (sh ? 1f : 0f) * blink * (i == 1 ? 1f : 1f);
                    fx.Glow(markGlow[i], col * (Mathf.Lerp(3f, 9f, nightK) * k), ref markShown[i]);
                    fx.Glow(markHalo[i], col * (halos ? haloGain * 2.2f * nightK * k : 0f), ref markHaloShown[i]);
                    if (cam && markHalo[i].enabled) markHalo[i].transform.rotation = Quaternion.LookRotation(cam.transform.position - markHalo[i].transform.position);
                }
            }

            void Search(float dt, bool night, float nightK, bool alive)
            {
                if (!sYaw) return;
                bool on = alive && night && power > 0.5f;
                float prev = sLevel;
                sLevel = on ? Mathf.MoveTowards(sLevel, 1f, dt / 0.35f) : Mathf.MoveTowards(sLevel, 0f, alive ? dt / 0.6f : 1f);
                if (prev <= 0f && sLevel > 0f)
                {
                    SearchSwitchOns++;
                    if (AudioDirector.Has(SearchOnEvent)) AudioDirector.Fire(SearchOnEvent, sYaw.position, obj.Id, 0.35f, 1.1f);
                }
                // the sweep round the turret's front, the drum's nod
                float tYaw = 180f, tPitch = 62f;
                if (sLevel > 0.01f)
                {
                    float ph = (float)(OzShuttleLights.GameClock() * 2.0 * Mathf.PI / Mathf.Max(4f, SearchPeriod)) + seed * 6.283f;
                    tYaw = 180f + SearchSweep * Mathf.Sin(ph); tPitch = SearchPitch + 2f * Mathf.Sin(ph * 2.3f);
                }
                float a = 1f - Mathf.Exp(-2.4f * dt);
                sYawOff = Mathf.LerpAngle(sYawOff, tYaw, a); sPitchNow = Mathf.Lerp(sPitchNow, tPitch, a);
                sYaw.localRotation = Quaternion.Euler(0f, sYawOff, 0f); sPitch.localRotation = Quaternion.Euler(sPitchNow, 0f, 0f);
                var nl = NightLights.I;
                float dusk = Mathf.Lerp(0.25f, 1f, nightK); var c = sLight.color.linear;
                bool halos = !nl || nl.halos, beams = !nl || nl.beams, pools = !nl || nl.pools;
                float haloGain = nl ? nl.haloGain : 0.35f, beamGain = nl ? nl.beamGain : 1f, poolGain = nl ? nl.poolGain : 0.22f, lightGain = nl ? nl.lightGain * nl.searchLightGain : 3f;
                float outK = sLevel * (OzMotion.Reduced ? 0.96f : 0.975f + 0.025f * Mathf.PerlinNoise(seed * 3.3f, Time.time * 8f));
                fx.Glow(sLens, c * (7f * outK), ref sLensShown);
                fx.Glow(sHalo, c * (halos ? haloGain * dusk * outK * 2.6f : 0f), ref sHaloShown);
                fx.Glow(sBeam, c * (beams ? beamGain * outK * Mathf.Pow(nightK, 0.8f) * 0.42f : 0f), ref sBeamShown);
                var cam = Camera.main; if (cam && sHalo.enabled) sHalo.transform.rotation = Quaternion.LookRotation(cam.transform.position - sHalo.transform.position);
                sFix.LightOut = outK;
                float share = NightBudgetShare.HookFrame >= Time.frameCount - 2 ? sFix.RealK : 0f;
                float k = outK * share; bool lit = k > 0.002f;
                if (sLight.enabled != lit) sLight.enabled = lit;
                if (lit) sLight.intensity = SearchIntensity * k * lightGain;
                // the beam's length and the ground pool where it lands
                var lens = sPitch.position; var dir = sPitch.forward; var g = ZGame.I;
                float gy = g ? g.TerrainY(lens.x, lens.z) : 0f, h = Mathf.Max(0.5f, lens.y - gy);
                float down = Mathf.Max(3f, -dir.y); float dist = dir.y < -0.05f ? Mathf.Min(SearchRange, h / -dir.y) : SearchRange;
                float ta = Mathf.Tan(SearchAngle * 0.5f * Mathf.Deg2Rad) * 1.2f, inv = 1f / Mathf.Max(0.01f, sPitch.lossyScale.x);
                sBeamT.localScale = new Vector3(dist * ta * inv, dist * ta * inv, (dist + 0.8f) * inv);
                var hz = new Vector3(dir.x, 0f, dir.z); if (hz.sqrMagnitude < 1e-4f) hz = -hTurret.forward; hz.Normalize();
                float flat = dist * Mathf.Sqrt(Mathf.Max(0f, 1f - dir.y * dir.y)), near = Mathf.Max(0.8f, flat * 0.55f), far = flat + 1.2f, mid = (near + far) * 0.5f;
                var centre = lens + hz * mid; float py = g ? g.TerrainY(centre.x, centre.z) : 0f;
                sPoolT.SetPositionAndRotation(new Vector3(centre.x, py + 0.35f, centre.z), Quaternion.LookRotation(hz, Vector3.up));
                float w = Mathf.Clamp(2f * flat * ta, 1.2f, SearchRange);
                sPoolT.localScale = new Vector3(w, 3.8f, Mathf.Max(w, far - near)); sPoolDist = Mathf.Max(1f, mid);
                float e = SearchIntensity / Mathf.Max(1f, sPoolDist * sPoolDist);
                fx.Glow(sPool, c * (pools ? poolGain * outK * dusk * (1f - share) * Mathf.Min(10f, e * 4f) : 0f), ref sPoolShown);
            }

            void Beacon(float dt, float nightK, bool alive)
            {
                bool on = alive && power > 0.5f;
                float prev = bLevel;
                bLevel = on ? Mathf.MoveTowards(bLevel, 1f, dt / 0.3f) : Mathf.MoveTowards(bLevel, 0f, alive ? dt / 0.5f : 1f);
                if (prev <= 0f && bLevel > 0f)
                {
                    BeaconSwitchOns++;
                    if (AudioDirector.Has(SearchOnEvent)) AudioDirector.Fire(SearchOnEvent, bSpin.position, obj.Id, 0.25f, 1.45f);   // a small relay click
                }
                bAngle = (bAngle + dt * 360f / 0.9f * Mathf.Max(0.2f, bLevel)) % 360f;
                bSpin.localRotation = Quaternion.Euler(0f, bAngle, 0f);
                var amber = new Color(1f, 0.55f, 0.1f).linear;
                var cam = Camera.main; float face = 0f;
                if (cam) { var to = cam.transform.position - bSpin.position; to.y = 0f; var fw = bSpin.forward; fw.y = 0f; if (to.sqrMagnitude > 1e-4f && fw.sqrMagnitude > 1e-4f) face = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(to.normalized, fw.normalized)), 6f); }
                var nl = NightLights.I; float haloGain = nl ? nl.haloGain : 0.35f; bool halos = !nl || nl.halos;
                fx.Glow(bBulb, amber * (bLevel * Mathf.Lerp(2.5f, 6f, nightK)), ref bBulbShown);
                fx.Glow(bHalo, amber * (halos ? bLevel * face * Mathf.Lerp(0.5f, 2.4f, nightK) * haloGain * 2f : 0f), ref bHaloShown);
                if (cam && bHalo.enabled) bHalo.transform.rotation = Quaternion.LookRotation(cam.transform.position - bHalo.transform.position);
                bFix.LightOut = bLevel * nightK;
                float share = NightBudgetShare.HookFrame >= Time.frameCount - 2 ? bFix.RealK : 0f;
                float k = bLevel * nightK * share; bool lit = k > 0.002f;
                if (bLight.enabled != lit) bLight.enabled = lit;
                if (lit) bLight.intensity = 16f * k * (nl ? nl.lightGain : 1f);
            }

            // ------------------------------------------------------------------ exhaust
            void Exhaust(float dt)
            {
                var fxp = OpenZFxPlayer.I;
                if (!fxp || !fxp.isActiveAndEnabled || mouths.Length == 0 || !vm || dt <= 0f) return;
                if (!DetailKit.OnScreen(Camera.main, view.transform.position + Vector3.up * 2f, 0.15f)) return;
                float sp = Mathf.Abs(vm.DriveSpeed), k = Mathf.Clamp01(sp / 1.2f); bool moving = vm.DriveMoving;
                float burst = exh ? exh.Burst : 0f;
                float rate = (moving ? Mathf.Lerp(SmokeRateIdle, SmokeRateDrive, k) : SmokeRateIdle) + burst * 14f;
                smokeAcc += rate * dt;
                if (burst > 0.6f && smokeAcc >= 1f) SmokeBursts++;
                var wind = OzShuttleExhaust.Wind();
                while (smokeAcc >= 1f)
                {
                    smokeAcc -= 1f;
                    foreach (var m in mouths)
                    {
                        if (!m) continue;
                        var p = m.position + new Vector3(Random.Range(-0.04f, 0.04f), 0f, Random.Range(-0.04f, 0.04f));
                        var col = Color.Lerp(moving ? Color.Lerp(SmokeIdle, SmokeDrive, k) : SmokeIdle, SmokeBurst, Mathf.Clamp01(burst * 1.2f));
                        float size = SmokeSize * Random.Range(0.85f, 1.2f) * (1f + 0.8f * burst + 0.3f * k);
                        float alpha = Mathf.Min(0.75f, SmokeAlpha * (1f + 0.6f * burst) * (moving ? 1f : 0.75f));
                        var back = view.transform.forward;                                                           // models face -z: +forward is the rear
                        var v = Vector3.up * Random.Range(1.0f, 1.5f) * (1f + 0.5f * burst) + back * (0.25f + 0.5f * k) + wind * 0.3f
                                + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f));
                        fxp.EmitChannel("leg_smoke", p, v, SmokeLife * Random.Range(0.85f, 1.15f), size, col, alpha);
                        SmokePuffs++; Smoke++;
                    }
                }
            }

            // ------------------------------------------------------------------ dust
            void DustStep(float dt)
            {
                var dust = DustTrails.I; var tm = TrackMarks.I;
                if (!dust || !dust.isActiveAndEnabled || tm == null || tm.Surface == null || dt <= 0f) { hasLast = false; return; }
                var t = view.transform; var pos = t.position;
                var fw = -t.forward; fw.y = 0f; if (fw.sqrMagnitude < 1e-4f) return; fw.Normalize();
                float yaw = Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg;
                if (!hasLast) { hasLast = true; lastPos = pos; lastYaw = yaw; return; }
                var mv = pos - lastPos; mv.y = 0f; float md = mv.magnitude; lastPos = pos;
                yawRate = Mathf.Lerp(yawRate, Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt, 1f - Mathf.Exp(-8f * dt)); lastYaw = yaw;
                if (md > 4f) return;                                                                                  // a teleport
                bool moving = vm ? vm.DriveMoving : md > 0.002f;
                float s01 = Mathf.Clamp01((vm ? Mathf.Abs(vm.DriveSpeed) : md / dt) / 1.25f);
                float scale = DustScale(type); var lat = new Vector3(fw.z, 0f, -fw.x);
                float hg = type == OzBigTanks.Bizon ? 1.83f : 1.8f, half = type == OzBigTanks.Bizon ? 3.0f : 2.55f;
                bool turning = yawRate > 18f;
                if (moving && !wasMoving) { startRing = 1f; StartRing(dust, tm, pos, fw, lat, hg, half, scale); }
                wasMoving = moving;
                if (!moving && !turning) return;
                dustDist += md + (turning ? yawRate * Mathf.Deg2Rad * hg * dt : 0f);                                 // a turn on the spot scrubs the tracks too
                float every = Mathf.Lerp(DustEverySlow, DustEveryFast, s01) * (turning ? 0.7f : 1f);
                if (dustDist < every) return;
                dustDist = 0f; DustVolleys++;
                var dir = md > 1e-4f ? mv / md : fw;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    var rear = pos + lat * (sgn * hg) - dir * half;                                                    // behind the track's rear
                    Puff(dust, tm, rear, -dir * (0.4f + 1.1f * s01) + lat * (sgn * Random.Range(0.15f, 0.5f)), scale * (0.75f + 0.55f * s01), s01, 1f);
                    Puff(dust, tm, rear - dir * 0.6f, -dir * (0.25f + 0.6f * s01) + lat * (sgn * Random.Range(0.3f, 0.7f)), scale * (0.9f + 0.6f * s01), s01, 0.8f);   // the rolling cloud behind
                    var side = pos + lat * (sgn * (hg + 0.45f)) + dir * Random.Range(-half * 0.6f, half * 0.4f);
                    Puff(dust, tm, side, lat * (sgn * Random.Range(0.4f, 0.9f)) - dir * 0.2f, scale * (0.6f + 0.4f * s01), s01, turning ? 1f : 0.6f);   // beside the track
                    if (turning) Puff(dust, tm, pos + lat * (sgn * hg) + dir * half, lat * (sgn * 0.6f), scale * 0.8f, Mathf.Max(0.5f, s01), 0.9f);   // the front of the track scrubs in a turn
                }
                if (startRing > 0f) startRing = Mathf.Max(0f, startRing - 0.34f);
            }

            void StartRing(DustTrails dust, TrackMarks tm, Vector3 pos, Vector3 fw, Vector3 lat, float hg, float half, float scale)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    for (int i = 0; i < 5; i++)
                    {
                        float along = Mathf.Lerp(-half, half, i / 4f);
                        var at = pos + lat * (sgn * (hg + 0.3f)) + fw * along;
                        Puff(dust, tm, at, lat * (sgn * Random.Range(0.5f, 1.1f)) + Vector3.up * 0.1f, scale * Random.Range(0.9f, 1.25f), 0.6f, 1.2f);
                    }
            }

            void Puff(DustTrails dust, TrackMarks tm, Vector3 at, Vector3 vel, float sizeK, float s01, float alphaK)
            {
                tm.Surface.Sample(at.x, at.z, out float y, out bool deck);
                float ds = deck ? tm.deckDust : tm.Surface.DustStrength(at.x, at.z);
                if (ds < 0.02f) return;
                at.y = y;
                float size = DustSize * sizeK * Random.Range(0.85f, 1.15f);
                float life = DustLife * (0.9f + 0.5f * s01) * Random.Range(0.85f, 1.2f);
                float alpha = DustAlpha * ds * (0.4f + 0.6f * Mathf.Max(s01, startRing)) * alphaK;
                dust.Emit(at + Vector3.up * (0.15f + 0.12f * size), vel + Vector3.up * Random.Range(0.25f, 0.55f), size, life, alpha, tm.Surface.DustColor(at.x, at.z));
                DustPuffs++; Dust++;
            }
        }
    }
}
