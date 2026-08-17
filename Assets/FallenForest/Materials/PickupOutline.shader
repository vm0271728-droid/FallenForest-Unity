Shader "FallenForest/PickupOutline"
{
    Properties { _Color("Color", Color) = (0.82,0.86,0.90,0.18) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Front
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            float4 _Color;
            Varyings vert(Attributes i){Varyings o;float3 p=i.positionOS.xyz+i.normalOS*0.025;o.positionCS=TransformObjectToHClip(p);return o;}
            half4 frag(Varyings i):SV_Target{return _Color;}
            ENDHLSL
        }
    }
}
