using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Experimental;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>Batch authoring only: small tracked scenes reference shared prefabs and regenerate terrain from grids.</summary>
    public static class OzAiMapBake
    {
        const string Package = "Packages/org.openz.aimaps/";
        public static void Skirmish()
        {
            Directory.CreateDirectory("../aimaps/Scenes");
            Directory.CreateDirectory("../aimaps/Resources/OpenZ/AiMaps/Previews");
            var reports = new List<string>();
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var entry in OzAiMapCatalog.MultiplayerMaps)
            {
                Bake(entry, reports);
                Debug.Log("[AI Maps] " + reports[reports.Count - 1]);
                int index = scenes.FindIndex(scene => scene.path == entry.ScenePath);
                var enabled = new EditorBuildSettingsScene(entry.ScenePath, true);
                if (index < 0) scenes.Add(enabled);
                else scenes[index] = enabled;
            }
            AssetDatabase.Refresh();
            foreach (var entry in OzAiMapCatalog.MultiplayerMaps)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.ScenePath) ||
                    !Resources.Load<Texture2D>("OpenZ/AiMaps/Previews/" + entry.Id))
                    throw new InvalidOperationException("Missing converted scene or preview for " + entry.Id);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[AI Maps] Verified " + reports.Count + " multiplayer scenes and previews.");
        }

        public static void All()
        {
            Directory.CreateDirectory("../aimaps/Scenes"); Directory.CreateDirectory("../aimaps/Materials"); Directory.CreateDirectory("../aimaps/Resources/OpenZ/AiMaps/Previews");
            AssetDatabase.Refresh();
            var reports=new List<string>();
            int index=0;
            foreach(var entry in OzAiMapCatalog.All)
            {
                Bake(entry, reports);
                Debug.Log("[AI Maps] "+(++index)+"/"+OzAiMapCatalog.All.Length+" "+entry.Id+" "+entry.File);
            }
            var scenes=EditorBuildSettings.scenes.Where(s=>!s.path.EndsWith("/L19_Mayhem_Converted.unity",StringComparison.Ordinal)).ToList();
            foreach(var entry in OzAiMapCatalog.All) if(!scenes.Any(s=>s.path==entry.ScenePath)) scenes.Add(new EditorBuildSettingsScene(entry.ScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Verify();
            File.WriteAllLines("../aimaps/BAKE-REPORT.txt",reports);
            Debug.Log("PASS all converted maps baked and verified. No game player built or launched.");
        }
        static void Bake(OzAiMap entry,List<string> reports)
        {
            var text=Resources.Load<TextAsset>("OpenZ/Maps/"+entry.Id+"_grid");
            if(!text) throw new Exception("Missing package map "+entry.Id);
            var map=ZMapData.FromJson(text.text);
            var scene=EditorSceneManager.OpenScene("Assets/OpenZ/Scenes/L17_CarPark.unity",OpenSceneMode.Single);
            foreach(var name in new[]{"Environment","ZodObjects","SKIRT","DetailsV2_L17","OZ_TowerMounts_Baked"}) {var go=GameObject.Find(name); if(go) UnityEngine.Object.DestroyImmediate(go);}
            var old=GameObject.Find("COL_Terrain");
            if(old) { var oldRoot=old.transform.root.gameObject; if(oldRoot.name=="ZGame" || oldRoot.name=="Main Camera") throw new Exception("Unexpected inherited terrain root"); UnityEngine.Object.DestroyImmediate(oldRoot); }
            var materials=Materials(entry.World);
            var environment=new GameObject("Environment"); var terrain=OzDraftTerrain.Build(map.width,map.height,map.classes,entry.World,materials,environment.transform);
            var game=UnityEngine.Object.FindFirstObjectByType<ZGame>(); game.level=entry.Id;
            var camera=UnityEngine.Object.FindFirstObjectByType<RtsCamera>(); camera.mapSizeMeters=new Vector2(map.width*2,map.height*2);
            var red=map.objects.First(o=>o.owner=="red"&&o.kind.StartsWith("building:fort")); camera.focus=new Vector3(red.x*2+10,0,-red.y*2-12);
            var type=typeof(OpenZSetup); const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
            var objectType=type.GetNestedType("ZObj",BindingFlags.NonPublic); var forts=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(objectType));
            foreach(var o in map.objects.Where(o=>o.kind.StartsWith("building:fort"))) forts.Add(JsonUtility.FromJson(JsonUtility.ToJson(o),objectType));
            type.GetField("Forts",flags).SetValue(null,forts);
            string resourceLevel=entry.World=="city" ? "L17" : entry.World=="jungle" ? "L16" : entry.World=="volcanic" || entry.World=="arctic" ? "L07" : "L02";
            type.GetField("SpawnLv",flags).SetValue(null,resourceLevel);
            var objects=new GameObject("ZodObjects"); var spawn=type.GetMethod("Spawn",flags); var random=new System.Random(19); int spawned=0;
            Physics.SyncTransforms();
            foreach(var o in map.objects)
            {
                int count=(int)spawn.Invoke(null,new[]{JsonUtility.FromJson(JsonUtility.ToJson(o),objectType),(object)(entry.World=="city"),objects.transform,random,entry.World=="arctic"});
                if(count==0) throw new Exception("No view for "+entry.File+" "+o.kind+" at "+o.x+","+o.y);
                spawned+=count;
            }
            int rocks=0;
            foreach(Transform item in objects.transform)
            {
                var renderers=item.GetComponentsInChildren<Renderer>();
                if(item.name.StartsWith("rock_"))
                {
                    var before=Bounds(renderers); var scale=item.localScale; scale.y*=4.1f/before.size.y; item.localScale=scale;
                    item.position+=Vector3.up*(before.min.y-Bounds(renderers).min.y);
                    var after=Bounds(renderers);
                    if(Mathf.Abs(after.size.y-4.1f)>.01f || Mathf.Abs(before.size.x-after.size.x)>.01f || Mathf.Abs(before.size.z-after.size.z)>.01f) throw new Exception("Rock geometry mismatch");
                    foreach(var renderer in renderers) renderer.sharedMaterial=materials[3]; rocks++;
                }
                if(entry.World=="arctic" && (item.name.StartsWith("map_object") || item.name.StartsWith("hut_"))) foreach(var renderer in renderers) renderer.sharedMaterial=materials[3];
            }
            TowerGunSeat.SeatAll(objects.transform);
            var atmosphere=UnityEngine.Object.FindFirstObjectByType<Atmosphere>();
            if(atmosphere) { atmosphere.level=entry.Id; atmosphere.preset=entry.World=="city" ? Atmosphere.Preset.City : entry.World=="jungle" ? Atmosphere.Preset.Jungle : entry.World=="volcanic" ? Atmosphere.Preset.Volcanic : Atmosphere.Preset.Desert; atmosphere.particles=entry.World!="arctic"; atmosphere.lampRoot=objects.transform; atmosphere.groundMaterial=materials[0]; }
            var day=UnityEngine.Object.FindFirstObjectByType<DayNight>(); if(day) day.lampRoot=objects.transform;
            // Previews use the actual map and original starting units, not another map's art.
            var world=ZWorld.Create(Resources.Load<TextAsset>("OpenZ/default_settings").text,text.text,19);
            var preview=OzLevelInfo.Bake(world,entry.Id,true);
            File.WriteAllBytes("../aimaps/Resources/OpenZ/AiMaps/Previews/"+entry.Id+".png",preview.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(preview);
            var meshes=new HashSet<Mesh>(); foreach(var filter in terrain.GetComponentsInChildren<MeshFilter>()) meshes.Add(filter.sharedMesh); foreach(var collider in terrain.GetComponentsInChildren<MeshCollider>()) meshes.Add(collider.sharedMesh);
            UnityEngine.Object.DestroyImmediate(terrain); foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            var runtime=environment.AddComponent<OzLegacyAiTerrain>(); runtime.mapId=entry.Id; runtime.world=entry.World; runtime.materials=materials;
            // Inherited atmospheric/detail helpers must not read the original L17 map's data.
            foreach(var root in scene.GetRootGameObjects()) foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(!behaviour) continue;
                var field=behaviour.GetType().GetField("level",BindingFlags.Public|BindingFlags.Instance);
                if(field!=null && field.FieldType==typeof(string) && !field.IsInitOnly) field.SetValue(behaviour,entry.Id);
            }
            if (!EditorSceneManager.SaveScene(scene,entry.ScenePath)) throw new IOException("Could not save " + entry.ScenePath);
            reports.Add(entry.Id+" | "+entry.File+" | "+entry.World+" | "+map.width+"x"+map.height+" | "+map.objects.Count+" source objects | "+spawned+" views | "+rocks+" raised rocks | PASS");
        }
        static Bounds Bounds(Renderer[] renderers) { if(renderers.Length==0) throw new Exception("Missing obstacle mesh"); var bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds; }
        static Material[] Materials(string world)
        {
            var materials=new Material[5];
            for(int category=0;category<5;category++)
            {
                string path=Package+"Materials/"+world+"_"+category+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material)
                {
                    string reference=category==3 ? OzDraftTerrain.MaterialPaths(world=="arctic" ? "desert" : world)[3] : "CITY_Concrete";
                    var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/"+reference+".mat");
                    material=new Material(original) {name=world+" surface "+category};
                    OzDraftTerrain.Configure(material,category,world);
                    if(category!=3) { material.SetTexture("_BaseMap",Grain(category==1)); material.SetFloat("_Triplanar",0); }
                    material.SetColor("_BaseColor",Tint(world,category));
                    if(category==3 && world=="arctic") { material.SetFloat("_TopBlend",1); material.SetColor("_TopColor",new Color(.92f,.97f,1)); }
                    AssetDatabase.CreateAsset(material,path);
                }
                materials[category]=material;
            }
            return materials;
        }
        static Color Tint(string world,int category)
        {
            if(world=="arctic") return category==0 ? new Color(.94f,.97f,1) : category==1 ? new Color(.68f,.82f,.9f) : category==2 ? new Color(.2f,.52f,.76f) : category==4 ? new Color(.08f,.26f,.5f) : new Color(.68f,.82f,.92f);
            if(category==3) return Color.white;
            if(category==1) return world=="city" ? new Color(.32f,.34f,.36f) : new Color(.57f,.54f,.48f);
            if(category==2 || category==4) return world=="volcanic" ? new Color(.92f,.24f,.035f) : world=="city" || world=="jungle" ? new Color(.30f,.39f,.21f) : new Color(.22f,.62f,.68f);
            return world=="city" ? new Color(.60f,.58f,.54f) : world=="jungle" ? new Color(.43f,.53f,.25f) : world=="volcanic" ? new Color(.32f,.29f,.28f) : new Color(.76f,.59f,.38f);
        }
        static Texture2D Grain(bool road)
        {
            string path=Package+"Materials/"+(road ? "RoadGrain" : "GroundGrain")+".asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path); if(texture) return texture;
            const int size=128; texture=new Texture2D(size,size,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
            var random=new System.Random(road ? 1 : 2); var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) { float shade=.84f+.16f*(float)random.NextDouble(); if(!road && (x==0||y==0))shade*=.85f; pixels[y*size+x]=new Color(shade,shade,shade); }
            texture.SetPixels(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture,path); return texture;
        }
        public static void Verify()
        {
            int units=0;
            var settings=Resources.Load<TextAsset>("OpenZ/default_settings").text;
            foreach(var entry in OzAiMapCatalog.Playable)
            {
                var mapAsset=Resources.Load<TextAsset>("OpenZ/Maps/"+entry.Id+"_grid"); if(!mapAsset) throw new Exception("Missing grid "+entry.Id);
                var map=ZMapData.FromJson(mapAsset.text); var world=ZWorld.Create(settings,mapAsset.text,19);
                if(map.file!=entry.File || map.planet!=entry.World) throw new Exception("Source identity mismatch "+entry.Id);
                foreach(var source in map.objects.Where(o=>o.ot==3||o.ot==4||o.ot==5))
                {
                    var owner=source.owner=="red" ? Team.Red : source.owner=="blue" ? Team.Blue : Team.Neutral;
                    if(!world.Objects.Any(o=>o.Owner==owner&&o.Type==source.Type&&o.LocX==source.x*16&&o.LocY==source.y*16)) throw new Exception("Unit identity mismatch "+entry.Id+" "+source.kind); units++;
                }
                if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.ScenePath)) throw new Exception("Missing scene "+entry.Scene);
                var preview = Resources.Load<UnityEngine.Video.VideoClip>("OpenZ/AiMaps/Videos/" + entry.Id + "_preview");
                if (!preview || preview.frameCount == 0 || !Resources.Load<Texture2D>("OpenZ/AiMaps/Posters/" + entry.Id + "_poster"))
                    throw new InvalidOperationException("Missing AI map preview or fallback still " + entry.Id);
                if (map.HasLava)
                {
                    var minimap = OzLevelInfo.Bake(world, entry.Id, true);
                    var png = "../aimaps/Resources/OpenZ/AiMaps/Previews/" + entry.Id + ".png";
                    var pixels = minimap.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(minimap);
                    if (!File.Exists(png) || !File.ReadAllBytes(png).SequenceEqual(pixels)) File.WriteAllBytes(png, pixels);
                }
                if(entry.Duel) { var rules=new OpenZ.Multiplayer.DuelRules{Map=entry.Id}; rules.Validate(); world.ConfigureDuel(rules,false); world.DuelApplyStartingArmies(); }
            }
            File.WriteAllText("../aimaps/SOURCE-CHECKS.txt","PASS "+OzAiMapCatalog.Playable.Count()+" AI maps; "+units+" source starting-unit records; original types, ownership and positions; duel rule validation, starting-army application, scenes, preview videos and fallback stills.\n");
        }
    }
}
