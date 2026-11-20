using System.Collections.Generic;
using UnityEngine;
namespace FloodFill.ThreeD
{
    /// <summary>Numbered Items silhouettes. Existing procedural and hollow-grid generators are unchanged.</summary>
    public static class FloodItemShape3D
    {
        private static readonly Vector3Int[] Directions = {Vector3Int.left,Vector3Int.right,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        public static bool[,,] Generate(int width,int height,int depth,int seed,out VoxelShapeBounds bounds)
        {
            var solid=new bool[width,height,depth];int kind=(int)((uint)seed%4);
            for(int x=0;x<width;x++)for(int y=0;y<height;y++)for(int z=0;z<depth;z++)
            {
                float px=(x-(width-1)*.5f)/(width*.47f), py=(y-(height-1)*.5f)/(height*.47f), pz=(z-(depth-1)*.5f)/(depth*.47f);
                bool inside;
                if(kind==0) // Apple: broad shoulders, tapered base and central dimple.
                {
                    float radius=.83f+.12f*py;
                    float dimple=py>.35f ? .18f*Mathf.Exp(-(px*px+pz*pz)*12) : 0;
                    inside=(px*px+pz*pz)/(radius*radius)+Mathf.Pow((py+dimple)/.93f,2)<=1;
                }
                else if(kind==1) // Pear: bulb below a narrow neck.
                {
                    float radius=Mathf.Lerp(.98f,.38f,Mathf.Clamp01((py+.35f)/1.15f));
                    inside=(px*px+pz*pz)/(radius*radius)+py*py<=1;
                }
                else if(kind==2) inside=px*px+py*py+pz*pz<=1; // Orange.
                else // Banana: a curved tube, with narrowing ends.
                {
                    float center=.6f*py*py-.33f;
                    float radius=.27f+.14f*(1-py*py);
                    inside=Mathf.Abs(py)<.96f && (px-center)*(px-center)+pz*pz<=radius*radius;
                }
                solid[x,y,z]=inside;
            }
            // Use the visible voxel skin so the player can reach regions by orbiting the object.
            var shell=new bool[width,height,depth];
            for(int x=0;x<width;x++)for(int y=0;y<height;y++)for(int z=0;z<depth;z++)
            {
                if(!solid[x,y,z])continue;
                foreach(var d in Directions)
                {
                    int nx=x+d.x,ny=y+d.y,nz=z+d.z;
                    if(nx<0||ny<0||nz<0||nx>=width||ny>=height||nz>=depth||!solid[nx,ny,nz]){shell[x,y,z]=true;break;}
                }
            }
            // Low resolutions can separate a tip. Keep one connected, playable object.
            var visited=new bool[width,height,depth];var largest=new List<Vector3Int>();
            for(int x=0;x<width;x++)for(int y=0;y<height;y++)for(int z=0;z<depth;z++)
            {
                if(!shell[x,y,z]||visited[x,y,z])continue;
                var component=new List<Vector3Int>();var queue=new Queue<Vector3Int>();queue.Enqueue(new Vector3Int(x,y,z));visited[x,y,z]=true;
                while(queue.Count>0)
                {
                    var p=queue.Dequeue();component.Add(p);
                    foreach(var d in Directions){var n=p+d;if(n.x<0||n.y<0||n.z<0||n.x>=width||n.y>=height||n.z>=depth||visited[n.x,n.y,n.z]||!shell[n.x,n.y,n.z])continue;visited[n.x,n.y,n.z]=true;queue.Enqueue(n);}
                }
                if(component.Count>largest.Count)largest=component;
            }
            var mask=new bool[width,height,depth];var min=new Vector3Int(width,height,depth);var max=Vector3Int.zero;
            foreach(var p in largest){mask[p.x,p.y,p.z]=true;min=Vector3Int.Min(min,p);max=Vector3Int.Max(max,p);}
            bounds=new VoxelShapeBounds(min.x,max.x,min.y,max.y,min.z,max.z);return mask;
        }
    }
}
