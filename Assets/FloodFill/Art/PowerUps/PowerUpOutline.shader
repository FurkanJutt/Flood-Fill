Shader "FloodFill/Power Up Outline"
{
    Properties { _Pulse("Pulse",Range(0,1))=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Pulse;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            V Vert(A a){V v;v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.uv=a.uv;v.color=a.color;return v;}
            half4 Frag(V v):SV_Target
            {
                float2 edge=min(v.uv,1-v.uv);
                float border=1-smoothstep(.025,.055,min(edge.x,edge.y));
                return half4(v.color.rgb,v.color.a*border*_Pulse);
            }
            ENDHLSL
        }
    }
}
