using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FloodFill.ThreeD.PowerUps
{
    // One surface-outline mesh; never a material or effect object per cube.
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class FloodPowerUpHighlight3D : MonoBehaviour
    {
        private Mesh mesh;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private readonly List<Vector3> positions=new List<Vector3>(16000);
        private readonly List<Vector2> uv=new List<Vector2>(16000);
        private readonly List<Color> colors=new List<Color>(16000);
        private readonly List<int> triangles=new List<int>(24000);
        private static readonly Vector3Int[] Faces={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        private float pulseSpeed=2;
        public void Initialize(Material material)
        {
            if(mesh==null){mesh=new Mesh{name="Power-Up Surface Outlines"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;}
            surface=GetComponent<MeshRenderer>();surface.sharedMaterial=material;surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
            surface.lightProbeUsage=LightProbeUsage.Off;surface.reflectionProbeUsage=ReflectionProbeUsage.Off;properties=new MaterialPropertyBlock();Clear();
        }
        public void Clear(){if(surface!=null)surface.enabled=false;if(mesh!=null)mesh.Clear();}
        public void Show(VoxelBoardManager3D board,IReadOnlyList<VoxelCell3D> primary,Color primaryColor,IReadOnlyList<VoxelCell3D> secondary,Color secondaryColor,float speed)
        {
            if(mesh==null||surface.sharedMaterial==null)return;
            pulseSpeed=speed;positions.Clear();uv.Clear();colors.Clear();triangles.Clear();
            if(secondary!=null)for(int i=0;i<secondary.Count;i++)AddCube(board,secondary[i],secondaryColor);
            if(primary!=null)for(int i=0;i<primary.Count;i++)AddCube(board,primary[i],primaryColor);
            mesh.Clear();mesh.indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0,true);surface.enabled=positions.Count>0;
        }
        private void AddCube(VoxelBoardManager3D board,VoxelCell3D cube,Color color)
        {
            if(cube==null)return;Vector3 center=transform.InverseTransformPoint(cube.transform.position);float half=board.PowerUpVoxelWidth*.535f;
            foreach(var face in Faces)
            {
                if(board.IsActiveCoordinate(cube.X+face.x,cube.Y+face.y,cube.Z+face.z))continue;
                Vector3 normal=(Vector3)face,along=Mathf.Abs(normal.y)>.9f?Vector3.right:Vector3.up,side=Vector3.Cross(normal,along);
                int start=positions.Count;Vector3 middle=center+normal*half;
                positions.Add(middle-along*half-side*half);positions.Add(middle+along*half-side*half);positions.Add(middle+along*half+side*half);positions.Add(middle-along*half+side*half);
                uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.one);uv.Add(Vector2.up);
                for(int i=0;i<4;i++)colors.Add(color);
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
            }
        }
        private void LateUpdate()
        {
            if(surface==null||!surface.enabled)return;
            properties.SetFloat("_Pulse",.7f+.3f*Mathf.Sin(Time.unscaledTime*pulseSpeed*Mathf.PI));surface.SetPropertyBlock(properties);
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
