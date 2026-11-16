using System.Collections.Generic;
using UnityEngine;
namespace OpenZ.Experimental
{
    /// <summary>Shared flat-map geometry for the independent authoring tool and converted game scenes. GPL-3.0.</summary>
    public static class OzDraftTerrain
    {
        public static string[] MaterialPaths(string world)
        {
            string ground=world=="city"?"CITY_Concrete":world=="desert"?"M_Skirt_Sand":world=="jungle"?"M_Skirt_Jungle":"M_Skirt_Ash";
            string wall=world=="city"?"M_Cliff_CityRock":world=="desert"?"M_Cliff_Sandstone":world=="jungle"?"M_Cliff_JungleRock":"M_Cliff_VolcanicRock";
            string water=world=="city"?"M_Water_Harbour":world=="jungle"?"M_Water_Swamp":"M_Water";
            return new[]{ground,ground,water,wall,water};
        }
        public static void Configure(Material material,int category,string world)
        {
            if(material.HasProperty("_TeamMode"))material.SetFloat("_TeamMode",0);
            if(material.HasProperty("_UseVertexColor"))material.SetFloat("_UseVertexColor",0);
            if(category==1&&material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",new Color(.25f,.26f,.25f));
        }
        public static GameObject Build(int width,int height,int[] classes,string world,Material[] materials,Transform parent)
        {
            var root=new GameObject("Converted terrain");root.transform.SetParent(parent,false);
            var collisionVertices=new List<Vector3>();var collisionTriangles=new List<int>();
            for(int category=0;category<5;category++)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    if(classes[y*width+x]!=category)continue;
                    float Height(float a,float b)=>category==3?(world=="city"?3.5f:4f)+Mathf.PerlinNoise(a*.035f,b*.035f)*1.4f:0;
                    var a=new Vector3(x*2,Height(x*2,y*2),-y*2);var b=new Vector3(x*2+2,Height(x*2+2,y*2),-y*2);
                    var c=new Vector3(x*2,Height(x*2,y*2+2),-y*2-2);var d=new Vector3(x*2+2,Height(x*2+2,y*2+2),-y*2-2);
                    Quad(vertices,triangles,uv,a,b,c,d);
                    var ca=a;var cb=b;var cc=c;var cd=d;if(category==2||category==4){ca.y=cb.y=cc.y=cd.y=-.4f;}
                    Quad(collisionVertices,collisionTriangles,null,ca,cb,cc,cd);
                    if(category!=3)continue;
                    bool Open(int xx,int yy)=>xx<0||yy<0||xx>=width||yy>=height||classes[yy*width+xx]!=3;
                    if(Open(x,y-1))Quad(vertices,triangles,uv,new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z),a,b);
                    if(Open(x,y+1))Quad(vertices,triangles,uv,c,d,new Vector3(c.x,0,c.z),new Vector3(d.x,0,d.z));
                    if(Open(x-1,y))Quad(vertices,triangles,uv,new Vector3(c.x,0,c.z),new Vector3(a.x,0,a.z),c,a);
                    if(Open(x+1,y))Quad(vertices,triangles,uv,new Vector3(b.x,0,b.z),new Vector3(d.x,0,d.z),b,d);
                }
                if(vertices.Count==0)continue;
                var mesh=Mesh(vertices,triangles,uv,"Draft surface "+category);
                var surface=new GameObject("Surface "+category,typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(root.transform,false);
                surface.GetComponent<MeshFilter>().sharedMesh=mesh;surface.GetComponent<MeshRenderer>().sharedMaterial=materials[category];
            }
            var collider=new GameObject("COL_Terrain",typeof(MeshCollider));collider.transform.SetParent(root.transform,false);collider.GetComponent<MeshCollider>().sharedMesh=Mesh(collisionVertices,collisionTriangles,null,"Converted collision");
            root.AddComponent<OzDraftTerrainRenderState>();
            return root;
        }
        static void Quad(List<Vector3> vertices,List<int> triangles,List<Vector2> uv,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3});
            if(uv!=null)foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x,p.z)*.2f);
        }
        static Mesh Mesh(List<Vector3> vertices,List<int> triangles,List<Vector2> uv,string name)
        {
            var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(uv!=null)mesh.SetUVs(0,uv);var colors=new Color[vertices.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
