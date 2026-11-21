using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.Goals
{
    /// <summary>Rounded native UI geometry: no texture padding or bitmap asset dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FloodGoalPanelGraphic3D : MaskableGraphic
    {
        [Min(0)] public float radius=24, borderWidth=2, verticalInset;
        public Color borderColor=new Color(.12f,.65f,.95f,.85f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();r.yMin+=verticalInset;r.yMax-=verticalInset;
            Fill(mesh,r,radius,borderColor);
            var inner=new Rect(r.x+borderWidth,r.y+borderWidth,Mathf.Max(0,r.width-2*borderWidth),Mathf.Max(0,r.height-2*borderWidth));
            Fill(mesh,inner,Mathf.Max(0,radius-borderWidth),color);
        }
        private static void Fill(VertexHelper mesh,Rect r,float radius,Color color)
        {
            if(r.width<=0||r.height<=0)return;
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            int start=mesh.currentVertCount;mesh.AddVert(r.center,color,Vector2.zero);
            const int steps=6;
            for(int corner=0;corner<4;corner++)
            {
                Vector2 center=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
                for(int s=0;s<=steps;s++)
                {
                    float angle=(corner*90+s*90f/steps)*Mathf.Deg2Rad;
                    mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
                }
            }
            int perimeter=4*(steps+1);
            for(int v=0;v<perimeter;v++)mesh.AddTriangle(start,start+1+v,start+1+(v+1)%perimeter);
        }
    }
}
