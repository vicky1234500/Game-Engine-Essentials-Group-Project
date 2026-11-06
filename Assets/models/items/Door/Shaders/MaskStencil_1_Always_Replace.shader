Shader "JRD/MaskStencil_1_Always_Replace"
{
    SubShader
    {
        Tags
        {
            "Queue"="Geometry" "RenderType"="Opaque"
        }
        ZWrite Off // НЕ БЛОКИРУЕМ Z-БУФЕР
        ZTest Always // ВСЕГДА ПРОХОДИМ Z-ТЕСТ
        ColorMask 0

        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}