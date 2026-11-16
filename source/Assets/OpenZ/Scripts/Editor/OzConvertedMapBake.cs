using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenZ.Experimental;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace OpenZ.EditorTools
{
    /// <summary>Bakes a converted City operation using the ordinary game prefabs and its existing object spawner.</summary>
    public static class OzConvertedMapBake
    {
        [MenuItem("OpenZ/Converted Maps/Bake Level 19 - Mayhem")]
        public static void Mayhem()
        {
            const string mapPath="Assets/OpenZ/Resources/OpenZ/Maps/L19_grid.json";
            const string scenePath="Assets/OpenZ/Scenes/L19_Mayhem_Converted.unity";
            if(!File.Exists(mapPath))throw new FileNotFoundException("Convert the original p02_bb_orig19_fixed.map first",mapPath);
            string json=File.ReadAllText(mapPath);var map=ZMapData.FromJson(json);
            if(map.planet!="city")throw new Exception("Mayhem must use the City resource set");
            var scene=EditorSceneManager.OpenScene("Assets/OpenZ/Scenes/L17_CarPark.unity",OpenSceneMode.Single);
            foreach(var name in new[]{"Environment","ZodObjects","SKIRT","DetailsV2_L17","OZ_TowerMounts_Baked"}){var go=GameObject.Find(name);if(go)UnityEngine.Object.DestroyImmediate(go);}
            var oldTerrain=GameObject.Find("COL_Terrain");if(oldTerrain){var oldRoot=oldTerrain.transform.root.gameObject;if(oldRoot.name=="ZGame"||oldRoot.name=="Main Camera")throw new Exception("Unexpected original terrain hierarchy");UnityEngine.Object.DestroyImmediate(oldRoot);}
            var game=UnityEngine.Object.FindFirstObjectByType<ZGame>();if(!game)throw new Exception("Missing game bootstrap");game.level="L19";
            var camera=UnityEngine.Object.FindFirstObjectByType<RtsCamera>();camera.mapSizeMeters=new Vector2(map.width*2,map.height*2);var red=map.objects.First(o=>o.owner=="red"&&o.kind.StartsWith("building:fort"));camera.focus=new Vector3(red.x*2+10,0,-red.y*2-12);
            var materials=new List<Material>();int index=0;Directory.CreateDirectory("Assets/OpenZ/Materials/Converted");
            foreach(var name in OzDraftTerrain.MaterialPaths("city")){var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/"+name+".mat");if(!original)throw new Exception("Missing City material "+name);var material=new Material(original);OzDraftTerrain.Configure(material,index,"city");AssetDatabase.CreateAsset(material,"Assets/OpenZ/Materials/Converted/L19_"+index+".mat");materials.Add(material);index++;}
            var environment=new GameObject("Environment");OzDraftTerrain.Build(map.width,map.height,map.classes,"city",materials.ToArray(),environment.transform);
            Directory.CreateDirectory("Assets/OpenZ/Models/Generated/Converted");var meshes=new HashSet<Mesh>();
            foreach(var filter in environment.GetComponentsInChildren<MeshFilter>())meshes.Add(filter.sharedMesh);
            foreach(var collider in environment.GetComponentsInChildren<MeshCollider>())meshes.Add(collider.sharedMesh);
            int meshIndex=0;foreach(var mesh in meshes)if(mesh&&!AssetDatabase.Contains(mesh))AssetDatabase.CreateAsset(mesh,"Assets/OpenZ/Models/Generated/Converted/L19_"+(meshIndex++)+".asset");
            var type=typeof(OpenZSetup);const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
            var objectType=type.GetNestedType("ZObj",BindingFlags.NonPublic);var listType=typeof(List<>).MakeGenericType(objectType);var forts=(IList)Activator.CreateInstance(listType);
            foreach(var o in map.objects.Where(o=>o.kind.StartsWith("building:fort")))forts.Add(JsonUtility.FromJson(JsonUtility.ToJson(o),objectType));
            type.GetField("Forts",flags).SetValue(null,forts);type.GetField("SpawnLv",flags).SetValue(null,"L19");
            var objects=new GameObject("ZodObjects");var spawn=type.GetMethod("Spawn",flags);var random=new System.Random(19);int spawned=0;
            Physics.SyncTransforms();
            foreach(var o in map.objects)spawned+=(int)spawn.Invoke(null,new object[]{JsonUtility.FromJson(JsonUtility.ToJson(o),objectType),true,objects.transform,random});
            TowerGunSeat.SeatAll(objects.transform);
            var atmosphere=UnityEngine.Object.FindFirstObjectByType<Atmosphere>();if(atmosphere){atmosphere.level="L19";atmosphere.preset=Atmosphere.Preset.City;atmosphere.lampRoot=objects.transform;atmosphere.groundMaterial=materials[0];}
            var day=UnityEngine.Object.FindFirstObjectByType<DayNight>();if(day)day.lampRoot=objects.transform;
            EditorSceneManager.SaveScene(scene,scenePath);var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==scenePath))scenes.Add(new EditorBuildSettingsScene(scenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();
            string report="Level 19 - Mayhem\nWorld: City\nOriginal file: "+map.file+"\nObjects: "+map.objects.Count+"\nSpawned views: "+spawned+"\nRed starting units: "+map.objects.Count(o=>o.owner=="red"&&(o.Category=="robot"||o.Category=="vehicle"||o.Category=="cannon"))+"\nBlue starting units: "+map.objects.Count(o=>o.owner=="blue"&&(o.Category=="robot"||o.Category=="vehicle"||o.Category=="cannon"));
            File.WriteAllText(Path.GetFullPath("../.local/mayhem-bake-report.txt"),report);Debug.Log(report);
        }
    }
}
