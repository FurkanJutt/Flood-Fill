Shader "FloodFill/Liquid Splash"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.kind=v.kind;o.color=v.color;return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float distance;
                if(i.kind.x<.5)
                {
                    // Scalloped lip of the curled 3D fan; narrow rounded fingers break up the silhouette.
                    float lip=.84+.10*cos(p.y*8);
                    distance=max(abs(p.y)-(.88-.12*i.uv.x),i.uv.x-lip);
                }
                else if(i.kind.x<1.5) distance=length(p)-.75;
                else
                {
                    float taper=sin(i.uv.x*3.14159265);
                    distance=max(abs(p.y)-max(.06,taper*.85),abs(p.x)-.95);
                }
                float edge=max(fwidth(distance),.015);
                half alpha=(1-smoothstep(-edge,edge,distance))*i.color.a;
                // Same-hue glossy highlight: no white/cyan color substitution or bloom pass.
                half highlight=pow(saturate(1-length((p-float2(-.2,.2))*2.8)),3)*.24;
                highlight+=i.kind.x<.5?pow(saturate(i.uv.x),4)*.28:0;
                return half4(i.color.rgb*(.86+.22*saturate(-distance*4)+highlight),alpha);
            }
            ENDHLSL
        }
    }
}
