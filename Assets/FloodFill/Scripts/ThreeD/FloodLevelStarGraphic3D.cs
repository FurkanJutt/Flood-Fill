using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD
{
    /// <summary>Editable white difficulty icon, independent of the earned-star sprite colors.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FloodLevelStarGraphic3D : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;Vector2 center=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            mesh.AddVert(center,color,Vector2.zero);
            for(int i=0;i<10;i++)
            {
                float angle=(90-i*36)*Mathf.Deg2Rad;float r=i%2==0?radius:radius*.46f;
                mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r,color,Vector2.zero);
            }
            for(int i=0;i<10;i++)mesh.AddTriangle(0,i+1,(i+1)%10+1);
        }
    }
}
