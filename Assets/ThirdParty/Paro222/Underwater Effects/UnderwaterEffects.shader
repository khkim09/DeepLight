Shader "Paro222/UnderwaterEffects"
{
    Properties
    {
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _color ("Water Color", Color) = (0.1, 0.45, 0.6, 1)
        _dis ("Distance", Float) = 10
        _alpha ("Alpha", Range(0,1)) = 0.15
        _refraction ("Refraction", Float) = 0.1
        _normalUV ("Normal UV", Vector) = (1,1,0.2,0.1)
        _EffectEnabled ("Effect Enabled", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UnderwaterFullscreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            float4 _color;
            float _dis;
            float _alpha;
            float _refraction;
            float4 _normalUV;
            float _EffectEnabled;

            float3 SampleNormalMap(float2 uv)
            {
                float4 n = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
                return UnpackNormal(n);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // 원본 화면
                half4 src = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                if (_EffectEnabled < 0.5)
                    return src;

                // 아주 약한 노말 스크롤
                float2 normalUv = uv * _normalUV.xy + (_normalUV.zw * _Time.y);
                float3 normal = SampleNormalMap(normalUv);

                // 굴절은 미세하게만
                float2 distortedUv = uv + (normal.xy * (_refraction * 0.0025));

                half4 refracted = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, distortedUv);

                // 깊이 샘플
                float rawDepth = SampleSceneDepth(distortedUv);
                float linearDepth01 = Linear01Depth(rawDepth, _ZBufferParams);

                // 먼 곳일수록 안개가 쌓이게
                float fogFactor = smoothstep(0.0, _dis * 0.01, linearDepth01);
                fogFactor = saturate(fogFactor) * _alpha;

                // 가까운 곳은 원본 유지, 먼 곳만 살짝 물색으로 이동
                half3 underwaterColor = lerp(refracted.rgb, _color.rgb, fogFactor);

                return half4(underwaterColor, src.a);
            }
            ENDHLSL
        }
    }
}