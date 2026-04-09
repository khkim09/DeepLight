Shader "Project/Unlit/SubmarineBeamVisual"
{
    Properties
    {
        _BeamColor("Beam Color", Color) = (0.75, 0.9, 1.0, 1.0)
        _BeamOpacity("Beam Opacity", Range(0, 1)) = 0.35
        _LengthFadePower("Length Fade Power", Range(0.1, 8)) = 1.8
        _ViewRimPower("View Rim Power", Range(0.1, 8)) = 1.2
        _DepthFadeDistance("Depth Fade Distance", Float) = 1.2

        [HideInInspector] _BeamOriginWS("Beam Origin WS", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BeamForwardWS("Beam Forward WS", Vector) = (0, 0, 1, 0)
        [HideInInspector] _BeamLengthWS("Beam Length WS", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float _BeamOpacity;
                float _LengthFadePower;
                float _ViewRimPower;
                float _DepthFadeDistance;
                float3 _BeamOriginWS;
                float3 _BeamForwardWS;
                float _BeamLengthWS;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            float GetSceneEyeDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 beamForward = normalize(_BeamForwardWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 normalWS = normalize(input.normalWS);

                float beamLength = max(_BeamLengthWS, 0.001f);
                float axial01 = saturate(dot(input.positionWS - _BeamOriginWS, beamForward) / beamLength);

                // 꼭짓점에서 가장 진하고 끝으로 갈수록 사라짐
                float lengthFade = pow(saturate(1.0f - axial01), _LengthFadePower);

                // 몸통 기본 채움
                float bodyAlpha = 0.9f;

                // 가장자리 강조는 약하게만
                float rim = pow(saturate(1.0f - abs(dot(normalWS, viewDirWS))), _ViewRimPower);
                float rimBoost = lerp(1.0f, 1.25f, rim);

                float2 screenUv = input.screenPos.xy / input.screenPos.w;
                float sceneEyeDepth = GetSceneEyeDepth(screenUv);
                float fragmentEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthFade = saturate((sceneEyeDepth - fragmentEyeDepth) / max(_DepthFadeDistance, 0.001f));

                float finalAlpha = _BeamOpacity * lengthFade * bodyAlpha * rimBoost * depthFade;
                finalAlpha = saturate(finalAlpha);

                return half4(_BeamColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}