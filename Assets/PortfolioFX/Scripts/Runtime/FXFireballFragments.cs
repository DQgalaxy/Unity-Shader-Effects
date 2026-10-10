using System.Collections.Generic;
using UnityEngine;

namespace PortfolioFX
{
    // Build presentation fragments once. Exterior faces retain the source mesh UVs;
    // exposed boundaries receive interior caps instead of leaving paper-thin shards.
    public static class FXFireballFragments
    {
        public sealed class Piece
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 home;
            public float floorOffset;
        }
        struct Edge { public int a,b; public Edge(int x,int y){a=x;b=y;} }
        public static Piece[] Build(Mesh source,float scale,Quaternion rotation,Vector3 offset,Transform parent,Material exterior,Material interior,List<Mesh> owned)
        {
            if(!source.isReadable)throw new System.InvalidOperationException("Rune mesh must have Read/Write enabled.");
            Vector3[] v=source.vertices,n=source.normals;Vector2[] uv=source.uv;int[] triangles=source.triangles;
            var bounds=source.bounds;
            var seeds=new Vector2[12];var groups=new List<int>[12];
            for(int i=0;i<12;i++){seeds[i]=new Vector2((i%3+.42f+.12f*Mathf.Sin(i*4.1f))/3,(i/3+.45f+.1f*Mathf.Cos(i*3.7f))/4);groups[i]=new List<int>();}
            for(int t=0;t<triangles.Length;t+=3)
            {
                Vector3 c=(v[triangles[t]]+v[triangles[t+1]]+v[triangles[t+2]])/3;
                Vector2 p=new Vector2(Mathf.InverseLerp(bounds.min.x,bounds.max.x,c.x),Mathf.InverseLerp(bounds.min.y,bounds.max.y,c.y));
                int closest=0;float distance=float.MaxValue;
                for(int j=0;j<seeds.Length;j++){float d=(p-seeds[j]).sqrMagnitude;if(d<distance){distance=d;closest=j;}}
                groups[closest].Add(triangles[t]);groups[closest].Add(triangles[t+1]);groups[closest].Add(triangles[t+2]);
            }
            var pieces=new List<Piece>();
            foreach(var group in groups)
            {
                if(group.Count==0)continue;
                var verts=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var outer=new List<int>();var caps=new List<int>();
                var edges=new Dictionary<string,Edge>();var counts=new Dictionary<string,int>();
                Vector3 center=Vector3.zero;foreach(int index in group)center+=v[index];center/=group.Count;
                // A slab's interior anchor is at its mid-depth, not at its visible front face.
                center.z=bounds.center.z;
                for(int t=0;t<group.Count;t+=3)
                {
                    int a=group[t],b=group[t+1],c=group[t+2];
                    foreach(int index in new[]{a,b,c}){outer.Add(verts.Count);verts.Add(rotation*((v[index]-center)*scale));normals.Add(rotation*(n.Length==v.Length?n[index]:Vector3.forward));uvs.Add(uv.Length==v.Length?uv[index]:Vector2.zero);}
                    foreach(var e in new[]{new Edge(a,b),new Edge(b,c),new Edge(c,a)}){string key=Key(v[e.a],v[e.b]);if(counts.ContainsKey(key))counts[key]++;else{counts[key]=1;edges[key]=e;}}
                }
                foreach(var entry in edges)
                {
                    if(counts[entry.Key]!=1)continue;var e=entry.Value;
                    Vector3 a=rotation*((v[e.b]-center)*scale),b=rotation*((v[e.a]-center)*scale);
                    if(Vector3.Cross(b-a,-a).sqrMagnitude<1e-12f)continue;
                    Vector3 normal=Vector3.Cross(b-a,-a).normalized;
                    foreach(var p in new[]{a,b,Vector3.zero}){caps.Add(verts.Count);verts.Add(p);normals.Add(normal);uvs.Add(new Vector2(p.x,p.y)*2);}
                }
                var mesh=new Mesh{name="RuneFragment_"+pieces.Count,hideFlags=HideFlags.DontSave};mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.subMeshCount=2;mesh.SetTriangles(outer,0);mesh.SetTriangles(caps,1);mesh.RecalculateTangents();mesh.RecalculateBounds();owned.Add(mesh);
                Vector3 home=offset+rotation*(center*scale);
                var go=new GameObject(mesh.name){hideFlags=HideFlags.DontSave};go.transform.SetParent(parent,false);go.transform.localPosition=home;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{exterior,interior};renderer.enabled=false;
                int pieceIndex=pieces.Count;
                Quaternion resting=Quaternion.Euler(75+pieceIndex*13,41+pieceIndex*31,63+pieceIndex*19);
                float lowest=float.MaxValue;foreach(var p in verts)lowest=Mathf.Min(lowest,(resting*p).y);
                pieces.Add(new Piece{transform=go.transform,renderer=renderer,home=home,floorOffset=-lowest});
            }
            return pieces.ToArray();
        }
        static string Point(Vector3 p){return Mathf.RoundToInt(p.x*10000)+","+Mathf.RoundToInt(p.y*10000)+","+Mathf.RoundToInt(p.z*10000);}
        static string Key(Vector3 a,Vector3 b){string x=Point(a),y=Point(b);return string.CompareOrdinal(x,y)<0?x+"/"+y:y+"/"+x;}
    }
}
