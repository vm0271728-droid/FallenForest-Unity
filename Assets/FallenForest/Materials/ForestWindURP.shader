Shader "FallenForest/ForestWindURP"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.42
        _WindAmplitude("Wind Amplitude", Range(0,0.8)) = 0.22
        _WindFrequency("Wind Frequency", Range(0.05,5)) = 0.9
        _BendStrength("Player Bend", Range(0,1)) = 0.45
        _HeightMask("Height Mask", Range(0.1,8)) = 2.0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST; float4 _BaseColor; float _Cutoff; float _WindAmplitude; float _WindFrequency; float _BendStrength; float _HeightMask;
        CBUFFER_END
        float _FF_WindStrength; float4 _FF_PlayerWS; float _FF_GrassBendRadius;
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
        float3 Deform(float3 posOS)
        {
            float3 ws = TransformObjectToWorld(posOS);
            float height = saturate(max(posOS.y,0.0) / max(_HeightMask,0.01));
            height *= height;
            float phase = dot(ws.xz,float2(0.071,0.113)) + _Time.y * _WindFrequency;
            float gust = sin(phase) * 0.68 + sin(phase*1.73+1.1)*0.22 + sin(phase*3.17)*0.10;
            ws.xz += normalize(float2(0.83,0.55)) * gust * _WindAmplitude * _FF_WindStrength * height;
            float2 away = ws.xz - _FF_PlayerWS.xz; float d = length(away); float r=max(_FF_GrassBendRadius,0.01);
            float influence = saturate(1.0-d/r); influence*=influence;
            if(d>0.001) ws.xz += away/d * influence * _BendStrength * height;
            return ws;
        }
        Varyings Vert(Attributes IN)
        {
            Varyings OUT; float3 ws=Deform(IN.positionOS.xyz); OUT.positionWS=ws; OUT.positionCS=TransformWorldToHClip(ws); OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS); OUT.uv=TRANSFORM_TEX(IN.uv,_BaseMap); return OUT;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings IN):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,IN.uv)*_BaseColor; clip(tex.a-_Cutoff);
                Light light=GetMainLight(TransformWorldToShadowCoord(IN.positionWS)); half ndl=saturate(dot(normalize(IN.normalWS),light.direction)); half3 ambient=SampleSH(normalize(IN.normalWS)); half3 lit=tex.rgb*(ambient + light.color*(0.18h+ndl*0.82h)*light.shadowAttenuation*light.distanceAttenuation); return half4(lit,tex.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            struct ShadowVaryings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            ShadowVaryings ShadowVert(Attributes IN){ShadowVaryings OUT;float3 ws=Deform(IN.positionOS.xyz);OUT.positionCS=TransformWorldToHClip(ws);OUT.uv=TRANSFORM_TEX(IN.uv,_BaseMap);return OUT;}
            half4 ShadowFrag(ShadowVaryings IN):SV_Target{half a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,IN.uv).a*_BaseColor.a;clip(a-_Cutoff);return 0;}
            ENDHLSL
        }
    }
}
