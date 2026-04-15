Shader "Project/Unlit/SubmarineBeamVisual"
{
    Properties
    {
        [HDR] _BeamColor("Beam Color", Color) = (0.75, 0.90, 1.00, 1.0)

        // x = BeamOpacity
        // y = LengthFadePower
        // z = ViewRimPower
        // w = DepthFadeDistance
        _BeamParams0("Beam Params 0", Vector) = (0.35, 1.8, 1.2, 1.2)

        // x = BeamLengthWS
        // yzw = reserved
        _BeamParams1("Beam Params 1", Vector) = (6.0, 0.0, 0.0, 0.0)

        [HideInInspector] _BeamOriginWS("Beam Origin WS", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BeamForwardWS("Beam Forward WS", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            // 투명 빔 비주얼 기본 렌더 상태
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
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

            // 빌드 안정성을 위해 float4 단위 정렬 유지
            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float4 _BeamParams0;
                float4 _BeamParams1;
                float4 _BeamOriginWS;
                float4 _BeamForwardWS;
            CBUFFER_END

            /// <summary>
            /// 정점 데이터를 클립 공간/월드 공간으로 변환한다.
            /// </summary>
            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 정점 위치 변환
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                // 노멀을 월드 기준으로 변환
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);

                // 화면 UV 계산용 클립 좌표 저장
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            /// <summary>
            /// 화면 깊이 텍스처에서 eye depth를 읽는다.
            /// </summary>
            float GetSceneEyeDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            /// <summary>
            /// 빔의 길이 페이드, 시점 림, 깊이 페이드를 합성한다.
            /// </summary>
            half4 Frag(Varyings input) : SV_Target
            {
                // 파라미터 unpack
                float beamOpacity       = _BeamParams0.x;
                float lengthFadePower   = max(_BeamParams0.y, 0.001);
                float viewRimPower      = max(_BeamParams0.z, 0.001);
                float depthFadeDistance = max(_BeamParams0.w, 0.001);
                float beamLength        = max(_BeamParams1.x, 0.001);

                // 월드 기준 방향/노멀/시선
                float3 beamForward = normalize(_BeamForwardWS.xyz);
                float3 normalWS    = normalize(input.normalWS);
                float3 viewDirWS   = normalize(GetWorldSpaceViewDir(input.positionWS));

                // 1) 길이 페이드
                // 빔 시작점에서 현재 픽셀까지의 축 방향 거리
                float axialDistance = dot(input.positionWS - _BeamOriginWS.xyz, beamForward);

                // 0 ~ 1 구간으로 정규화
                float axial01 = saturate(axialDistance / beamLength);

                // 시작점은 강하고, 끝으로 갈수록 줄어듦
                float lengthFade = pow(saturate(1.0 - axial01), lengthFadePower);

                // 2) 시점 림
                // 표면이 시선에 수직에 가까울수록 더 살아나는 림
                float ndotv = abs(dot(normalWS, viewDirWS));
                float rim = pow(saturate(1.0 - ndotv), viewRimPower);

                // 3) 깊이 페이드
                // 다른 지오메트리와 만나는 경계가 부드럽게 사라지도록 처리
                float2 screenUv = input.screenPos.xy / input.screenPos.w;
                float sceneEyeDepth = GetSceneEyeDepth(screenUv);
                float fragmentEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthFade = saturate((sceneEyeDepth - fragmentEyeDepth) / depthFadeDistance);

                // 최종 알파 합성
                float alpha = beamOpacity * lengthFade;

                // 림 가중치 적용
                alpha *= lerp(0.75, 1.25, rim);

                // 깊이 페이드 적용
                alpha *= depthFade;

                alpha = saturate(alpha);

                return half4(_BeamColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}