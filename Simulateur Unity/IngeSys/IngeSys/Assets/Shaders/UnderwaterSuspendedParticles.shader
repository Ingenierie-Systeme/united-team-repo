Shader "IngeSys/Underwater Suspended Particles"
{
    Properties { _Color ("Tint", Color) = (0.65,0.85,0.8,0.32) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color;
            float4 _LampPositionRange, _LampDirectionCos;
            float4 _LampColor;
            float _CaveBlend;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.color=v.color*_Color;o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 delta=i.world-_LampPositionRange.xyz;
                float distance=max(length(delta),0.001);
                float cone=smoothstep(_LampDirectionCos.w,min(1.0,_LampDirectionCos.w+0.08),dot(delta/distance,_LampDirectionCos.xyz));
                float attenuation=saturate(1-distance/max(_LampPositionRange.w,0.001));
                float3 lighting=0.006+_LampColor.rgb*cone*attenuation*attenuation;
                float radius=length(i.uv*2-1);
                fixed4 c=fixed4(i.color.rgb*lighting,i.color.a*(1-smoothstep(0.2,1,radius))*_CaveBlend);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
}
