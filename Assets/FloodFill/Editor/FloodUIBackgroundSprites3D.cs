using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    /// <summary>Creates ordinary sliced sprites in the Editor; no custom runtime renderer is required.</summary>
    public static class FloodUIBackgroundSprites3D
    {
        public const string Folder="Assets/FloodFill/Art/UI/PanelBackgrounds";
        public static bool IsStyled(Image image)=>image!=null&&image.sprite!=null&&AssetDatabase.GetAssetPath(image.sprite).StartsWith(Folder+"/",StringComparison.Ordinal);
        public static Image Apply(RectTransform rect,Color fill,Color border,float radius,float borderWidth,float verticalInset=0,bool raycastTarget=true)
        {
            var image=rect.GetComponent<Image>();if(image==null)image=rect.gameObject.AddComponent<Image>();
            image.sprite=Get(fill,border,radius,borderWidth,verticalInset);image.type=Image.Type.Sliced;image.fillCenter=true;image.color=Color.white;image.raycastTarget=raycastTarget;image.pixelsPerUnitMultiplier=1;
            return image;
        }
        public static Sprite Get(Color fill,Color border,float radius,float borderWidth,float verticalInset,bool rebuild=false)
        {
            string key=ColorUtility.ToHtmlStringRGBA(fill)+ColorUtility.ToHtmlStringRGBA(border)+radius.ToString("R",CultureInfo.InvariantCulture)+":"+borderWidth.ToString("R",CultureInfo.InvariantCulture)+":"+verticalInset.ToString("R",CultureInfo.InvariantCulture);
            string digest;using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","").Substring(0,12);
            string path=Folder+"/Rounded_"+digest+".png";
            var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null&&!rebuild)return existing;
            Directory.CreateDirectory(Folder);
            // Match UI vertex colors: quantize as the former mesh did, then composite in the project's color space.
            fill=(Color)(Color32)fill;border=(Color)(Color32)border;
            bool linear=PlayerSettings.colorSpace==ColorSpace.Linear;
            Color fillSample=linear?fill.linear:fill,borderSample=linear?border.linear:border;
            const int size=128,samples=4;var pixels=new Color32[size*size];
            bool Inside(float x,float y,float inset,float padding,float r)
            {
                float halfX=size*.5f-inset,halfY=size*.5f-padding-inset;r=Mathf.Min(r,Mathf.Min(halfX,halfY));
                float qx=Mathf.Abs(x-size*.5f)-(halfX-r),qy=Mathf.Abs(y-size*.5f)-(halfY-r);
                return new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)<=r;
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector4 sum=Vector4.zero;
                for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
                {
                    float px=x+(sx+.5f)/samples,py=y+(sy+.5f)/samples;
                    if(!Inside(px,py,0,verticalInset,radius))continue;
                    float a=borderSample.a;Vector3 premultiplied=new Vector3(borderSample.r,borderSample.g,borderSample.b)*a;
                    if(Inside(px,py,borderWidth,verticalInset,Mathf.Max(0,radius-borderWidth)))
                    {premultiplied=new Vector3(fillSample.r,fillSample.g,fillSample.b)*fillSample.a+premultiplied*(1-fillSample.a);a=fillSample.a+a*(1-fillSample.a);}
                    sum+=new Vector4(premultiplied.x,premultiplied.y,premultiplied.z,a);
                }
                sum/=samples*samples;
                Color sample=sum.w>0?new Color(sum.x/sum.w,sum.y/sum.w,sum.z/sum.w,sum.w):Color.clear;
                pixels[y*size+x]=linear?(Color32)sample.gamma:(Color32)sample;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);texture.SetPixels32(pixels);texture.Apply();
            try{File.WriteAllBytes(path,texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
            float side=Mathf.Ceil(radius)+2,vertical=side+verticalInset;importer.spriteBorder=new Vector4(side,vertical,side,vertical);var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.mipmapEnabled=false;importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=128;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
