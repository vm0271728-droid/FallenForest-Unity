Shader "FallenForest/TreeFoliageURP"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _OpacityMap("Opacity Map", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.38
        _WindAmplitude("Wind Amplitude", Range(0,0.5)) = 0.055
        _WindFrequency("Wind Frequency", Range(0.05,4)) = 0.62
        _WindScale("Wind World Scale", Range(0.001,0.2)) = 0.035
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_OpacityMap); SAMPLER(sampler_OpacityMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float _Cutoff;
        float _WindAmplitude;
        float _WindFrequency;
        float _WindScale;
        CBUFFER_END

        float _FF_WindStrength;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 tangentWS : TEXCOORD2;
            float3 bitangentWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
        };

        float3 Deform(float3 positionOS)
        {
            float3 ws = TransformObjectToWorld(positionOS);
            float objectHeight = max(abs(positionOS.y), 0.01);
            float heightMask = saturate(objectHeight * 0.10);
            heightMask *= heightMask;
            float phase = dot(ws.xz, float2(0.77, 1.19)) * _WindScale + _Time.y * _WindFrequency;
            float gust = sin(phase) * 0.66 + sin(phase * 1.83 + 1.7) * 0.24 + sin(phase * 3.11) * 0.10;
            ws.xz += float2(0.84, 0.54) * gust * _WindAmplitude * max(_FF_WindStrength, 0.25) * heightMask;
            return ws;
        }

        Varyings Vert(Attributes IN)
        {
            Varyings OUT;
            float3 ws = Deform(IN.positionOS.xyz);
            VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
            OUT.positionCS = TransformWorldToHClip(ws);
            OUT.positionWS = ws;
            OUT.normalWS = n.normalWS;
            OUT.tangentWS = n.tangentWS;
            OUT.bitangentWS = n.bitangentWS;
            OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
            return OUT;
        }

        half AlphaAt(float2 uv)
        {
            half baseAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            half opacity = SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, uv).r;
            return min(baseAlpha, opacity) * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half alpha = AlphaAt(IN.uv);
                clip(alpha - _Cutoff);

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                half3x3 tangentToWorld = half3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), normalize(IN.normalWS));
                half3 normalWS = normalize(mul(normalTS, tangentToWorld));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half back = saturate(dot(-normalWS, mainLight.direction)) * 0.22h;
                half3 ambient = SampleSH(normalWS);
                half3 direct = mainLight.color * (0.12h + ndl * 0.88h + back) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 color = baseSample.rgb * (ambient + direct);
                color = MixFog(color, ComputeFogFactor(IN.positionCS.z));
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(Attributes IN)
            {
                ShadowVaryings OUT;
                float3 ws = Deform(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(ws);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                clip(AlphaAt(IN.uv) - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
