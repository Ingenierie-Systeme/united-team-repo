Shader "IngeSys/Transparent Pool Water"
{
    Properties
    {
        _WaterTint ("Water tint", Color) = (0.025,0.22,0.25,1)
        _Absorption ("Absorption RGB per metre", Vector) = (0.08,0.025,0.016,0)
        _Refraction ("Refraction", Range(0,0.02)) = 0.004
        _DetailStrength ("Fine ripples (shading only)", Range(0,2)) = 0.7
        _ReflectionStrength ("Reflections", Range(0,1)) = 0.85
        _ReflectionTex ("Reflection", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_IngeSysWaterBackground" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _IngeSysWaterBackground, _ReflectionTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4x4 _ReflectionVP;
            float4 _WaterTint, _Absorption;
            float _Refraction, _DetailStrength, _ReflectionStrength, _ReflectionAvailable;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f
            {
                float4 pos:SV_POSITION; float4 screen:TEXCOORD0; float3 world:TEXCOORD1;
                float3 normal:TEXCOORD2; float eye:TEXCOORD3; float4 reflection:TEXCOORD4; float4 depthScreen:TEXCOORD6;
                UNITY_FOG_COORDS(5)
            };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.screen=ComputeGrabScreenPos(o.pos);
                o.depthScreen=ComputeScreenPos(o.pos);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);
                o.eye=-UnityObjectToViewPos(v.vertex).z;o.reflection=mul(_ReflectionVP,float4(o.world,1));
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i, fixed facing:VFACE):SV_Target
            {
                float2 q=i.world.xz;float t=_Time.y;
                float a=cos(dot(q,float2(8,11))+t*.55),b=cos(dot(q,float2(-17,9))-t*.7);
                float3 n=normalize(i.normal+float3(.035*a-.025*b,0,.04*a+.02*b)*_DetailStrength);
                if(facing<0)n=-n;
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float2 uv=i.screen.xy/i.screen.w;
                float2 offset=mul((float3x3)UNITY_MATRIX_V,n).xy*_Refraction;
                float2 depthUV=i.depthScreen.xy/i.depthScreen.w;
                float sceneEye=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,depthUV));
                float thickness=max(0,sceneEye-i.eye)*distance(_WorldSpaceCameraPos,i.world)/max(i.eye,.001);
                float2 refractedUV=uv+offset*saturate(thickness);
                float2 shiftedDepthUV=depthUV+float2(offset.x,
                    #if UNITY_UV_STARTS_AT_TOP
                        -offset.y
                    #else
                        offset.y
                    #endif
                )*saturate(thickness);
                if(LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,shiftedDepthUV))<i.eye)
                    refractedUV=uv;
                float3 background=tex2D(_IngeSysWaterBackground,refractedUV).rgb;
                float3 transmission=exp(-_Absorption.rgb*min(thickness,30));
                // Underwater view looks through the interface into air; do not tint the whole room twice.
                if(facing<0)transmission=1;
                float3 through=background*transmission+_WaterTint.rgb*(1-transmission);
                float fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
                float2 reflectionUV=i.reflection.xy/max(i.reflection.w,.001)*.5+.5;
                float3 reflected=tex2D(_ReflectionTex,reflectionUV+offset*2).rgb;
                float reflectionWeight=fresnel*_ReflectionStrength*_ReflectionAvailable*(facing>0?1:0);
                float3 lightDirection=normalize(UnityWorldSpaceLightDir(i.world));
                float highlight=pow(saturate(dot(n,normalize(view+lightDirection))),180)*.18;
                fixed4 c=fixed4(lerp(through,reflected,reflectionWeight)+_LightColor0.rgb*highlight,1);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
}
