using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// DV161 EvilZod's model on the map (View/OzEvilZodView.cs). Menu OpenZ/V1.3/EvilZod (editor, outside Play), in this order:
    ///  1 Build: copies the briefing's model to Models/HeroSquad/Hero_EvilZod.fbx (the briefing's own file keeps its Generic import) and imports the copy as Humanoid; makes his map
    ///    materials (the unit look, the grunts' team paint on his plates from the mask in Hero_EvilZod_Unit_BaseColor_Team.png, so blue reads blue and red red at the game camera; hat, cables and steel
    ///    keep his own dark, his eyes burn by day too), the cloak's mesh and material, the ghost material of the dash, and Resources/OpenZ/HeroSquad/Z_bs01_evilzod: a copy of the grunt's root with his
    ///    model at 1.8 x a grunt, the shotgun in the game's weapon shape.
    ///  2 Render portraits: portrait_evilzod_blue / _red in Resources/OpenZ/UI/PortraitsBS01, real frames of the game model in the port hold, a copy for the owner.
    ///  3 Check: PASS / FAIL lines.
    /// </summary>
    public static partial class OzHeroSquadSetup
    {
        const string EzSrc = "Assets/OpenZ/Resources/OpenZ/Blue/Characters", EzModel = "EvilZod", EzPrefab = Res + "/" + OzEvilZodView.PrefabKey + ".prefab", EzGhost = Res + "/M_EvilZod_Ghost.mat",
                     EzPortraitDir = "Assets/OpenZ/Resources/OpenZ/UI/PortraitsBS01", EzBriefingMd5 = "0df17a92454ac235376d061deee13961", EzM = "mixamorig";
        /// <summary></summary>
        public const float EzTall = OzEvilZodView.Tall;
        static readonly (string bone, string human)[] EzMap =
        {
            (EzM + "Hips", "Hips"), (EzM + "Spine", "Spine"), (EzM + "Spine1", "Chest"), (EzM + "Spine2", "UpperChest"), (EzM + "Neck", "Neck"), (EzM + "Head", "Head"),
            (EzM + "LeftShoulder", "LeftShoulder"), (EzM + "LeftArm", "LeftUpperArm"), (EzM + "LeftForeArm", "LeftLowerArm"), (EzM + "LeftHand", "LeftHand"),
            (EzM + "RightShoulder", "RightShoulder"), (EzM + "RightArm", "RightUpperArm"), (EzM + "RightForeArm", "RightLowerArm"), (EzM + "RightHand", "RightHand"),
            (EzM + "LeftUpLeg", "LeftUpperLeg"), (EzM + "LeftLeg", "LeftLowerLeg"), (EzM + "LeftFoot", "LeftFoot"), (EzM + "LeftToeBase", "LeftToes"),
            (EzM + "RightUpLeg", "RightUpperLeg"), (EzM + "RightLeg", "RightLowerLeg"), (EzM + "RightFoot", "RightFoot"), (EzM + "RightToeBase", "RightToes"),
        };

        [MenuItem("OpenZ/V1.3/EvilZod/1 Build (model, materials, prefab)", false, 10)]
        static void MenuEzBuild() => EzBuild();
        [MenuItem("OpenZ/V1.3/EvilZod/2 Render portraits", false, 11)]
        static void MenuEzPortraits() => EzRenderPortraits();
        [MenuItem("OpenZ/V1.3/EvilZod/3 Check", false, 12)]
        static void MenuEzCheck() => EzCheck();

        public static string EzBuild()
        {
            if (Application.isPlaying) return Say("leave Play mode first");
            var log = new List<string>();
            Folder(Res); Folder(Dir);
            var ctl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Ctl);
            if (!ctl) return Say(Ctl + " missing");
            float gruntH = GruntHeight(log);
            if (gruntH <= 0) return Say("Z_bs01_grunt: no height measured (" + string.Join("; ", log) + ")");
            float height = gruntH * EzTall;
            log.Add($"heights: grunt {gruntH:F3} m -> EvilZod {height:F3} m");
            string fbx = Fbx(EzModel);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(fbx))
            {
                if (!AssetDatabase.CopyAsset($"{EzSrc}/{EzModel}.fbx", fbx)) return Say($"could not copy {EzSrc}/{EzModel}.fbx");
                log.Add(fbx + " copied from the briefing's model (the original keeps its import)");
            }
            if (!(AssetImporter.GetAtPath(fbx) is ModelImporter mi)) return Say("no model importer on " + fbx);
            mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false; mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
            mi.humanDescription = EzDescribe(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            mi.SaveAndReimport();
            var av = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            log.Add($"avatar {(av ? av.name : "-")} valid={(av && av.isValid)} human={(av && av.isHuman)}");
            if (!av || !av.isValid || !av.isHuman) return Say(string.Join("\n", log));
            var sets = EzMaterials(log);
            if (sets.body == null) return Say(string.Join("\n", log));
            EzGhostMaterial(log);
            log.Add(EzBuildPrefab(av, ctl, height, gruntH, sets));
            AssetDatabase.SaveAssets();
            OzEvilZodView.Forget(); OzEvilZodBody.Forget(); OzEvilZodCape.Forget(); OzHeroLegs.ForgetData();
            return Say(string.Join("\n", log));
        }

        /// <summary>His humanoid description: the bone map above, the avatar skeleton at the bind pose with the legs set straight the way the heroes' are; his arms are level in the bind pose already.</summary>
        static HumanDescription EzDescribe(GameObject src)
        {
            var inst = Object.Instantiate(src);
            try
            {
                inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;
                OpenZRobotsV3.ToBindPose(inst);
                var all = inst.GetComponentsInChildren<Transform>(true);
                Transform F(string n) => all.FirstOrDefault(x => x.name == n);
                var hips = F(EzM + "Hips"); var head = F(EzM + "Head"); var front = F("HeadFront");
                var fwd = front && head ? Vector3.ProjectOnPlane(front.position - head.position, Vector3.up) : Vector3.back;
                fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.back;
                var lateral = Vector3.Cross(Vector3.up, fwd).normalized;
                var kh = new KhLegs();
                foreach (var side in new[] { "Left", "Right" })
                {
                    var up = F(EzM + side + "UpLeg"); var kn = F(EzM + side + "Leg"); var ft = F(EzM + side + "Foot");
                    if (up && kn && ft) StraightLeg(side, up, kn, ft, fwd, lateral, kh);
                }
                KhGround(hips, kh); LegLog[EzModel] = kh;
                var human = new List<HumanBone>();
                foreach (var (bone, h) in EzMap)
                    if (F(bone)) { var hb = new HumanBone { humanName = h, boneName = bone }; hb.limit.useDefaultValues = true; human.Add(hb); }
                var skel = all.Select(x => new SkeletonBone { name = x == inst.transform ? src.name : x.name, position = x.localPosition, rotation = x.localRotation, scale = x.localScale }).ToArray();
                return new HumanDescription
                {
                    human = human.ToArray(), skeleton = skel,
                    upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                    armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
                };
            }
            finally { Object.DestroyImmediate(inst); }
        }

        sealed class EzSet { public Material body, bodyRed, gun, rail, cape; }

        /// <summary></summary>
        public static float EzPaintRef = 0.065f, EzPaintShadeMin = 0.7f, EzPaintShadeMax = 1.2f, EzCapeRef = 0.38f, EzCapeShadeMin = 0.3f, EzCapeShadeMax = 0.8f;

        /// <summary>The grunts' unit material (TeamLit): the unit look, the night rim and the stun glow stay. His maps, his eyes burning by day too; team mode 3 on his body, 0 on the gun.</summary>
        static EzSet EzMaterials(List<string> log)
        {
            var grunt = AssetDatabase.LoadAssetAtPath<Material>(GruntBodyMat);
            if (!grunt) { log.Add(GruntBodyMat + " missing"); return new EzSet(); }
            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{EzSrc}/{n}.png");
            var team = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Hero_EvilZod_Unit_BaseColor_Team.png"); var capeMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Hero_EvilZod_Cape_BaseColor.png");
            if (!team || !capeMap) { log.Add("Hero_EvilZod_Unit_BaseColor_Team.png or Hero_EvilZod_Cape_BaseColor.png missing"); return new EzSet(); }
            Material Mat(string name, Texture2D b, Texture2D n, Texture2D ms, Texture2D em)
            {
                string p = $"{Dir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(grunt) { name = name }; AssetDatabase.CreateAsset(m, p); } else { m.shader = grunt.shader; m.CopyPropertiesFromMaterial(grunt); }
                m.SetFloat("_TeamMode", 0f); m.SetFloat("_Degreen", 0f); m.SetFloat("_CreamToTeam", 0f);
                m.SetTexture("_BaseMap", b); m.SetColor("_BaseColor", Color.white);
                if (n) { m.SetFloat("_UseNormalMap", 1f); m.SetTexture("_BumpMap", n); }
                if (ms) { m.SetFloat("_UseMaskMap", 1f); m.SetTexture("_MaskMap", ms); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); }
                if (em) { m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionMapColor", OzEvilZodFx.EyeBase); m.SetFloat("_EmissionNight", 0f); }
                EditorUtility.SetDirty(m);
                return m;
            }
            var set = new EzSet
            {
                body = Mat("M_Hero_EvilZod_Unit", team, T("EvilZod_Normal"), T("EvilZod_MetallicSmoothness"), T("EvilZod_Emission")),
                bodyRed = Mat("M_Hero_EvilZod_Unit_Red", team, T("EvilZod_Normal"), T("EvilZod_MetallicSmoothness"), T("EvilZod_Emission")),
                cape = Mat("M_Hero_EvilZod_Cape", capeMap, null, null, null),
                gun = Mat("M_Hero_EvilZod_Gun", T("Shotgun_BaseColor"), T("Shotgun_Normal"), T("Shotgun_MetallicSmoothness"), null),
                rail = Mat("M_Hero_EvilZod_Rail", T("Shotgun_BaseColor"), T("Shotgun_Normal"), T("Shotgun_MetallicSmoothness"), null),
            };
            set.rail.SetColor("_BaseColor", new Color(0.32f, 0.32f, 0.34f, 1f)); EditorUtility.SetDirty(set.rail);
            foreach (var m in new[] { set.body, set.bodyRed, set.gun }) { m.SetFloat("_NightClass", 1f); m.SetFloat("_SoftFill", 1.2f); m.SetFloat("_AmbientBoost", 1.9f); m.SetColor("_BaseColor", new Color(1f, 1f, 1.04f, 1f)); EditorUtility.SetDirty(m); }   // full metal has no diffuse: the fill lets him read beside a grunt, day and night
            foreach (var m in new[] { set.body, set.bodyRed }) { m.SetFloat("_TeamMode", 3f); m.SetFloat("_TeamRef", EzPaintRef); m.SetFloat("_ShadeMin", EzPaintShadeMin); m.SetFloat("_ShadeMax", EzPaintShadeMax); EditorUtility.SetDirty(m); }
            set.cape.SetFloat("_TeamMode", 3f); set.cape.SetFloat("_TeamRef", EzCapeRef); set.cape.SetFloat("_ShadeMin", EzCapeShadeMin); set.cape.SetFloat("_ShadeMax", EzCapeShadeMax);
            set.cape.SetFloat("_UseNormalMap", 0f); set.cape.SetFloat("_UseMaskMap", 0f); set.cape.SetFloat("_Metallic", 0f); set.cape.SetFloat("_Smoothness", 0.22f); set.cape.SetFloat("_NightClass", 1f); EditorUtility.SetDirty(set.cape);
            log.Add("materials: the grunts' team paint on his plates, team mode 3, the same for both teams; gun team mode 0; the cloak");
            return set;
        }

        /// <summary>His plates turned deep red, each texel keeping its value: Models/HeroSquad/Hero_EvilZod_Unit_BaseColor_Red.png. Brass, black and steel keep their colour.</summary>
        static Texture2D EzRedPaint(Texture2D src, List<string> log)
        {
            string p = AssetDatabase.GetAssetPath(src), o = $"{Dir}/Hero_EvilZod_Unit_BaseColor_Red.png";
            if (string.IsNullOrEmpty(p) || !File.Exists(p)) { log.Add("no base colour to turn red"); return null; }
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!t.LoadImage(File.ReadAllBytes(p), false)) { Object.DestroyImmediate(t); return null; }
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i]; Color.RGBToHSV(c, out float h, out float s, out float v);
                float w = HueBand(h * 360f, 190f, 255f, 12f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.22f, s));
                if (w <= 0f) continue;
                var r = Color.HSVToRGB(358f / 360f, Mathf.Clamp01(s * 1.15f), v * 0.92f); r.a = c.a;
                px[i] = Color.Lerp(c, r, w);
            }
            t.SetPixels32(px); t.Apply(false);
            File.WriteAllBytes(o, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(o, ImportAssetOptions.ForceUpdate);
            CopyTextureSettings(p, o);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(o);
        }

        static void EzGhostMaterial(List<string> log)
        {
            var sh = Shader.Find("OpenZ/EvilZodGhost");
            if (!sh) { log.Add("shader OpenZ/EvilZodGhost missing"); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(EzGhost);
            if (!m) { m = new Material(sh) { name = "M_EvilZod_Ghost" }; AssetDatabase.CreateAsset(m, EzGhost); } else m.shader = sh;
            EditorUtility.SetDirty(m);
        }

        static string EzBuildPrefab(Avatar av, AnimatorController ctl, float height, float gruntH, EzSet mats)
        {
            string path = EzPrefab;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) && !AssetDatabase.DeleteAsset(path)) return "could not replace " + path;
            if (!AssetDatabase.CopyAsset(GruntPrefab, path)) return "could not create " + path;
            var root = PrefabUtility.LoadPrefabContents(path); var log = new List<string>();
            try
            {
                root.name = OzEvilZodView.PrefabKey;
                var kit = root.GetComponent<BS01TypeKit>();
                if (kit) { kit.Remove(); Object.DestroyImmediate(kit); }
                foreach (var c in root.GetComponentsInChildren<BS01KitCable>(true).ToList()) Object.DestroyImmediate(c.gameObject);
                var old = root.transform.Find("Model");
                if (!old) return "the grunt copy has no Model";
                int layer = old.gameObject.layer;
                var oldAnim = old.GetComponent<Animator>();
                var cull = oldAnim ? oldAnim.cullingMode : AnimatorCullingMode.CullUpdateTransforms;
                RuntimeAnimatorController gruntCtl = oldAnim && oldAnim.runtimeAnimatorController ? oldAnim.runtimeAnimatorController : ctl;
                var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(EzModel));
                var hm = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                PrefabUtility.UnpackPrefabInstance(hm, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                hm.name = "Model"; hm.transform.SetParent(root.transform, false);
                hm.transform.localPosition = Vector3.zero; hm.transform.localRotation = Quaternion.identity; hm.transform.localScale = Vector3.one;
                OpenZRobotsV3.ToBindPose(hm);
                var hAll = hm.GetComponentsInChildren<Transform>(true);
                Transform F(string n) => hAll.FirstOrDefault(x => x && x.name == n);
                var head = F(EzM + "Head"); var hf = F("HeadFront");
                if (head && hf)
                {
                    var f = Vector3.ProjectOnPlane(hf.position - head.position, Vector3.up);
                    if (f.sqrMagnitude > 1e-6f) hm.transform.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(f, Vector3.back, Vector3.up), Vector3.up);   // about the vertical: a face already along +z is a half turn, which from-to rotation may lay over his feet
                }
                float h0 = KhBaseHeight(hm, EzModel, av, gruntCtl, height, gruntH, log);
                if (h0 <= 0.01f) return "his model has no skinned body";
                float scale = height / h0;
                hm.transform.localScale = Vector3.one * scale;
                log.Add($"height {height:F3} m (model {h0:F3} m x {scale:F4})");
                foreach (var tr in hm.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
                var an = hm.GetComponent<Animator>(); if (!an) an = hm.AddComponent<Animator>();
                an.avatar = av; an.runtimeAnimatorController = KhController(gruntCtl, log); an.applyRootMotion = false; an.cullingMode = cull;

                // the gun in the game's weapon shape: pivot on the grip, barrel -z, top +y, so everything that asks for a robot's weapon, grip or muzzle finds it
                var sock = F("RightHandSocket"); var gun = F("Shotgun"); var pump = F("Shotgun_Pump"); var pBack = F("Shotgun_PumpBack"); var pGrip = F("Shotgun_PumpGrip"); var muz = F("Shotgun_Muzzle");
                if (!sock || !gun || !pump || !pBack || !pGrip || !muz) return "the shotgun or its sockets are missing in " + Fbx(EzModel);
                var bore = (pump.position - pBack.position).normalized;                               // the slide runs along the bore, front stop ahead of the rear one
                var top = Vector3.ProjectOnPlane(muz.position - gun.position, bore).normalized;
                var sockW = new GameObject("RightHandWeapon") { layer = layer }; sockW.transform.SetParent(sock, false);
                var weapon = new GameObject("BS01_Weapon_EvilZod") { layer = layer }; weapon.transform.SetParent(sockW.transform, false);
                weapon.transform.SetPositionAndRotation(gun.position, Quaternion.LookRotation(-bore, top));
                gun.SetParent(weapon.transform, true);
                void Node(string n, Transform parent, Vector3 at) { var g = new GameObject(n) { layer = layer }; g.transform.SetParent(parent, false); g.transform.position = at; }
                Node("Grip", weapon.transform, gun.position); Node("Muzzle", weapon.transform, muz.position); Node("Foregrip", pump, pGrip.position);
                var lh = F("LeftHandSocket"); if (lh) Node("LeftHandGrip", lh.parent, lh.position);

                // the cloak: a plain mesh under the model, on the bind pose; OzEvilZodCape moves it in the game
                OpenZRobotsV3.ToBindPose(hm);
                var capeGo = new GameObject(OzEvilZodCape.ObjectName) { layer = layer }; capeGo.transform.SetParent(hm.transform, false);
                OzEvilZodCape.Forget();
                var capeMesh = OzEvilZodCape.BakeMesh(capeGo.transform, hm.transform);
                if (!capeMesh) return "the cloak: " + OzEvilZodCape.DataPath + " or his bones are missing";
                string capePath = $"{Dir}/Hero_EvilZod_Cape.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(capePath)) AssetDatabase.DeleteAsset(capePath);
                AssetDatabase.CreateAsset(capeMesh, capePath);
                capeGo.AddComponent<MeshFilter>().sharedMesh = capeMesh; capeGo.AddComponent<MeshRenderer>().sharedMaterial = mats.cape; capeGo.AddComponent<OzEvilZodCape>();

                var renderers = new List<Renderer>(); var red = new List<Material>(); var blue = new List<Material>();
                foreach (var r in hm.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
                    var ms = r.sharedMaterials; bool isGun = r.name.StartsWith("Shotgun"), isCape = r.name == OzEvilZodCape.ObjectName;
                    var mesh = r is SkinnedMeshRenderer sm ? sm.sharedMesh : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                    int small = -1, smallest = int.MaxValue;
                    if (isGun && mesh && ms.Length > 1) for (int i = 0; i < ms.Length && i < mesh.subMeshCount; i++) { int n = mesh.GetSubMesh(i).indexCount; if (n < smallest) { smallest = n; small = i; } }
                    for (int i = 0; i < ms.Length; i++)
                    {
                        var b = isCape ? mats.cape : isGun ? (i == small ? mats.rail : mats.gun) : mats.body; var rd = isGun || isCape ? b : mats.bodyRed;
                        ms[i] = b; blue.Add(b); red.Add(rd);
                    }
                    r.sharedMaterials = ms; renderers.Add(r);
                }
                Object.DestroyImmediate(old.gameObject);
                var look = root.GetComponent<OzHeroLook>(); if (!look) look = root.AddComponent<OzHeroLook>();
                look.renderers = renderers.ToArray(); look.red = red.ToArray(); look.blue = blue.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{path}: {string.Join("; ", log)}";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------------------------------------ 2 portraits
        /// <summary>The folder the owner looks at them in before they ship.</summary>
        public static string EzOwnerDir => Path.Combine(OpenZPaths.Records, "milestones", "v1_3_20261030", "for_owner", "hero_icons");
        public static float EzChinUp = 4f, EzFaceTurn = 20f, EzEyeGain = 1.5f;

        public static string EzRenderPortraits()
        {
            if (Application.isPlaying) return Say("leave Play mode first");
            var log = new List<string>(); string own = EzOwnerDir; Directory.CreateDirectory(own);
            foreach (var team in new[] { Team.Blue, Team.Red })
            {
                string c = team == Team.Red ? "red" : "blue", name = $"portrait_evilzod_{c}.png";
                var png = EzRender(c == "red", out string why);
                if (png == null) { log.Add(name + ": " + why); continue; }
                string p = $"{EzPortraitDir}/{name}";
                File.WriteAllBytes(p, png);
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                CopyTextureSettings(PortraitRef, p);
                File.WriteAllBytes(Path.Combine(own, name), png);
                log.Add($"{p}: written ({png.Length / 1024} KB), a copy in {own}");
            }
            return Say(string.Join("\n", log));
        }

        static byte[] EzRender(bool redPaint, out string why)
        {
            why = null;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(EzPrefab);
            if (!pf) { why = EzPrefab + " missing (menu 1 first)"; return null; }
            var scene = EditorSceneManager.NewPreviewScene(); var made = new List<GameObject>(); var temp = new List<Object>();
            RenderTexture rt = null; Texture2D tex = null;
            try
            {
                var go = Object.Instantiate(pf); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); made.Add(go);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f - EzFaceTurn * 0.5f, 0f));
                var an = go.GetComponentInChildren<Animator>(); if (!an) { why = "no Animator"; return null; }
                // the picture wears the briefing's own lit materials: the metal reads
                var cine = AssetDatabase.LoadAssetAtPath<Material>($"{EzSrc}/M_EvilZod_Body.mat"); var cg = AssetDatabase.LoadAssetAtPath<Material>($"{EzSrc}/M_Shotgun.mat"); var cr = AssetDatabase.LoadAssetAtPath<Material>($"{EzSrc}/M_Shotgun_Rail.mat");
                if (!cine || !cg) { why = "the briefing's materials are missing"; return null; }
                var body = new Material(cine) { name = cine.name + "_portrait" }; temp.Add(body);
                if (body.HasProperty("_EmissionColor")) body.SetColor("_EmissionColor", OzEvilZodFx.EyeBase * EzEyeGain);
                if (redPaint) { var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Hero_EvilZod_Unit_BaseColor_Red.png"); if (t) { if (body.HasProperty("_BaseMap")) body.SetTexture("_BaseMap", t); if (body.HasProperty("_MainTex")) body.SetTexture("_MainTex", t); } }
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) ms[i] = r.name.StartsWith("Shotgun") ? (ms[i] && ms[i].name.Contains("Rail") && cr ? cr : cg) : body;
                    r.sharedMaterials = ms;
                }
                var b = an.gameObject.AddComponent<OzEvilZodBody>();
                b.PortraitPose(EzChinUp, EzFaceTurn * 0.5f);
                if (!b.Ready) { why = "the body data did not load"; return null; }
                var camGo = new GameObject("PortraitCam"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene); made.Add(camGo);
                var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.fieldOfView = 24f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.10f, 0.24f, 1f);
                void Lamp(string n, Color c, float intensity, Vector3 euler)
                {
                    var l = new GameObject(n); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(l, scene); made.Add(l);
                    var li = l.AddComponent<Light>(); li.type = LightType.Directional; li.color = c; li.intensity = intensity; l.transform.rotation = Quaternion.Euler(euler);
                }
                Lamp("Key", new Color(1f, 0.92f, 0.82f), 1.5f, new Vector3(28f, 150f, 0f)); Lamp("Rim", new Color(0.55f, 0.72f, 1f), 1.4f, new Vector3(18f, -30f, 0f)); Lamp("Fill", new Color(0.75f, 0.80f, 0.95f), 0.35f, new Vector3(5f, 200f, 0f));
                var all = go.GetComponentsInChildren<Transform>(true);
                Transform F(string n) => all.FirstOrDefault(x => x.name == n);
                var hat = F("HatTop"); var head = F(EzM + "Head"); var hips = F(EzM + "Hips");
                float top = (hat ? hat.position.y : head.position.y + 0.6f) + 0.10f, low = hips.position.y - (top - hips.position.y) * 0.143f, hgt = top - low;   // hat, shoulders and the gun, the framing of the kit's first portraits
                var at = new Vector3(head.position.x, top - hgt * 0.47f, head.position.z);
                float dist = hgt / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                cam.transform.position = at + Quaternion.Euler(4f, -12f, 0f) * new Vector3(0f, 0f, dist);
                cam.transform.LookAt(at);
                rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                cam.targetTexture = rt; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); tex.Apply();
                RenderTexture.active = prev; cam.targetTexture = null;
                Backdrop(tex, cam.backgroundColor);
                return tex.EncodeToPNG();
            }
            finally
            {
                foreach (var g in made) if (g) Object.DestroyImmediate(g);
                foreach (var o in temp) if (o) Object.DestroyImmediate(o);
                if (rt) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex) Object.DestroyImmediate(tex);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ------------------------------------------------------------------------------------------------ 3 check
        public static string EzCheck()
        {
            var sb = new StringBuilder();
            var av = AssetDatabase.LoadAllAssetsAtPath(Fbx(EzModel)).OfType<Avatar>().FirstOrDefault();
            sb.AppendLine(V(av && av.isValid && av.isHuman, $"{Fbx(EzModel)}: humanoid avatar valid ({(av ? av.humanDescription.human.Length : 0)} bones mapped)"));
            var orig = AssetImporter.GetAtPath($"{EzSrc}/{EzModel}.fbx") as ModelImporter;
            sb.AppendLine(V(orig && orig.animationType == ModelImporterAnimationType.Generic, "the briefing's EvilZod.fbx is still Generic"));
            using (var md = MD5.Create()) sb.AppendLine(V(System.BitConverter.ToString(md.ComputeHash(File.ReadAllBytes($"{EzSrc}/{EzModel}.fbx"))).Replace("-", "").ToLowerInvariant() == EzBriefingMd5, "the briefing's EvilZod.fbx is unchanged (MD5)"));
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(EzPrefab);
            sb.AppendLine(V(pf, EzPrefab));
            float gH = KhCheckHeight(null);
            if (pf)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                try
                {
                    var an = inst.GetComponentInChildren<Animator>();
                    sb.AppendLine(V(an && an.isHuman && an.runtimeAnimatorController && an.runtimeAnimatorController.animationClips.Length > 10, "a humanoid Animator with the grunt's controller"));
                    var model = inst.transform.Find("Model"); if (model) OpenZRobotsV3.ToBindPose(model.gameObject);
                    float h = KhCheckHeight(inst, model ? model.gameObject : inst) * (model ? model.lossyScale.y : 1f);   // the skinned bake leaves his model scale out
                    sb.AppendLine(V(gH > 0f && h / gH >= EzTall - 0.05f && h / gH <= EzTall + 0.05f, $"height {h:F2} m, {h / Mathf.Max(0.01f, gH):0.00} x a grunt ({gH:F2} m), wanted {EzTall - 0.05f:0.00} to {EzTall + 0.05f:0.00}"));
                    var capeF = inst.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == OzEvilZodCape.ObjectName);
                    sb.AppendLine(V(capeF && capeF.sharedMesh && capeF.GetComponent<OzEvilZodCape>() && OzEvilZodCape.Data != null && capeF.sharedMesh.vertexCount == OzEvilZodCape.Data.rows * OzEvilZodCape.Data.cols * 2, "the cloak: mesh, component, data"));
                    var body = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
                    sb.AppendLine(V(body && body.sharedMaterial && body.sharedMaterial.GetFloat("_TeamMode") == 3f, "team paint on his body: team mode 3"));
                    var names = inst.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                    foreach (var n in new[] { "RightHandWeapon", "BS01_Weapon_EvilZod", "Grip", "Muzzle", "Foregrip", "Shotgun_Pump", "Shotgun_PumpBack", "HeadFront", "HatTop", "LeftHandGrip" }) sb.AppendLine(V(names.Contains(n), "socket " + n));
                    var look = inst.GetComponent<OzHeroLook>();
                    sb.AppendLine(V(look && look.red != null && look.blue != null && look.red.Length == look.blue.Length && look.blue.All(m => m) && look.red.All(m => m), "red and blue material sets"));
                    var w = inst.GetComponentsInChildren<Transform>(true).First(t => t.name == "BS01_Weapon_EvilZod");
                    var mz = inst.GetComponentsInChildren<Transform>(true).First(t => t.name == "Muzzle");
                    float along = Vector3.Dot((mz.position - w.position).normalized, -w.forward);
                    sb.AppendLine(V(along > 0.97f, $"barrel along the weapon's -z ({along:0.000})"));
                }
                finally { Object.DestroyImmediate(inst); }
            }
            sb.AppendLine(V(AssetDatabase.LoadAssetAtPath<Material>(EzGhost), "the ghost material keeps its shader in the player: " + EzGhost));
            var legs = OzHeroLegs.Load(OzEvilZodBody.Member);
            sb.AppendLine(V(legs != null && legs.Error == null && legs.Programs.Length == 17, $"leg data: {(legs != null ? legs.Programs.Length : 0)} programs{(legs != null && legs.Error != null ? ", " + legs.Error : "")}"));
            sb.AppendLine(V(Resources.Load<TextAsset>(OzEvilZodBody.HandsPath), "upper body data"));
            foreach (var c in new[] { "red", "blue" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{EzPortraitDir}/portrait_evilzod_{c}.png");
                sb.AppendLine(V(t && t.width == 1024 && t.height == 1024, $"portrait_evilzod_{c}.png 1024 x 1024"));
            }
            sb.AppendLine(V(UnitArt.Portrait(ZEvilZodType, Team.Blue), "UnitArt finds his picture"));
            return Say(sb.ToString());
        }
        const string ZEvilZodType = OpenZ.Sim.ZEvilZod.Type;
    }
}
