using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>잠수함 실제 조명과 단일 빔 비주얼 튜닝 값을 보관한다.</summary>
    [CreateAssetMenu(
        fileName = "SubmarineLightTuning_",
        menuName = "Project/Submarine/Submarine Light Tuning")]
    public class SubmarineLightTuningSO : ScriptableObject
    {
        [Header("Top Exploration World Light")]
        [SerializeField] private float explorationTopWorldBaseIntensity = 120f; // 탐사 상단 실제 조명 강도
        [SerializeField] private float explorationTopOuterAngleWide = 56f; // 넓은 조리개 상단 외곽각
        [SerializeField] private float explorationTopOuterAngleNarrow = 24f; // 좁은 조리개 상단 외곽각
        [SerializeField] private float explorationTopRangeWide = 28f; // 넓은 조리개 상단 사거리
        [SerializeField] private float explorationTopRangeNarrow = 42f; // 좁은 조리개 상단 사거리

        [Header("Bottom Harvest World Lights")]
        [SerializeField] private float harvestBottomWorldBaseIntensity = 55f; // 하단 실제 조명 강도
        [SerializeField] private float harvestBottomFixedIntensity = 75f; // 하단 고정 실제 조명 강도 (조리개 무시)
        [SerializeField] private float harvestBottomOuterAngleWide = 34f; // 넓은 조리개 하단 외곽각
        [SerializeField] private float harvestBottomOuterAngleNarrow = 18f; // 좁은 조리개 하단 외곽각
        [SerializeField] private float harvestBottomRangeWide = 12f; // 넓은 조리개 하단 사거리
        [SerializeField] private float harvestBottomRangeNarrow = 20f; // 좁은 조리개 하단 사거리

        [Header("Intensity By Aperture")]
        [SerializeField] private float narrowApertureIntensityMultiplier = 1.20f; // 조리개를 조일수록 광량 상승
        [SerializeField] private float wideApertureIntensityMultiplier = 0.72f; // 조리개를 풀수록 광량 감소

        [Header("Spot Shape")]
        [SerializeField, Range(0.05f, 0.95f)] private float innerOuterAngleRatio = 0.45f; // Inner / Outer 비율

        [Header("Aperture Wheel")]
        [SerializeField, Range(0f, 1f)] private float defaultAperture01 = 0.45f; // 시작 조리개 값
        [SerializeField, Range(0f, 1f)] private float minAperture01 = 0.00f; // 가장 넓은 조리개
        [SerializeField, Range(0f, 1f)] private float maxAperture01 = 1.00f; // 가장 좁은 조리개
        [SerializeField] private float apertureWheelStep = 0.08f; // 휠 1칸당 조리개 변화량

        [Header("Focus Assist / Close Target Anti-Blowout")]
        [SerializeField] private LayerMask focusAssistMask = ~0; // 근접 감쇠 대상 레이어
        [SerializeField] private float focusAssistStartDistance = 14f; // 감쇠 시작 거리
        [SerializeField] private float focusAssistEndDistance = 4f; // 최소 광량 도달 거리
        [SerializeField, Range(0.01f, 1f)] private float focusAssistMinIntensityMultiplier = 0.05f; // 근접 시 실제 광량 최소 배율

        [Header("Top Beam Visual Scale")]
        [SerializeField] private Vector3 topBeamScaleWide = new(400f, 400f, 250f); // 넓은 조리개 상단 빔 스케일
        [SerializeField] private Vector3 topBeamScaleNarrow = new(200f, 200f, 500f); // 좁은 조리개 상단 빔 스케일

        [Header("Bottom Beam Visual Scale")]
        [SerializeField] private Vector3 bottomBeamScaleWide = new(150f, 150f, 150f); // 넓은 조리개 하단 빔 스케일
        [SerializeField] private Vector3 bottomBeamScaleNarrow = new(100f, 100f, 200f); // 좁은 조리개 하단 빔 스케일

        [Header("Top Beam Visual Opacity")]
        [SerializeField, Range(0f, 1f)] private float topBeamOpacityWide = 0.32f; // 넓은 조리개 상단 빔 투명도
        [SerializeField, Range(0f, 1f)] private float topBeamOpacityNarrow = 0.42f; // 좁은 조리개 상단 빔 투명도

        [Header("Bottom Beam Visual Opacity")]
        [SerializeField, Range(0f, 1f)] private float bottomBeamOpacityWide = 0.24f; // 넓은 조리개 하단 빔 투명도
        [SerializeField, Range(0f, 1f)] private float bottomBeamOpacityNarrow = 0.34f; // 좁은 조리개 하단 빔 투명도

        [Header("Beam Shader")]
        [SerializeField, Range(0.1f, 8f)] private float beamLengthFadePower = 1.8f; // 빔 길이 방향 알파 감소 곡선
        [SerializeField, Range(0.1f, 8f)] private float beamViewRimPower = 1.2f; // 시점 기반 가장자리 보강
        [SerializeField] private float beamDepthFadeDistance = 1.2f; // 지오메트리와 만날 때의 soft fade 거리

        public LayerMask FocusAssistMask => focusAssistMask;
        public float FocusAssistStartDistance => focusAssistStartDistance;
        public float FocusAssistEndDistance => focusAssistEndDistance;
        public float FocusAssistMinIntensityMultiplier => focusAssistMinIntensityMultiplier;
        public float HarvestBottomFixedIntensity => harvestBottomFixedIntensity;

        public float DefaultAperture01 => defaultAperture01;
        public float MinAperture01 => minAperture01;
        public float MaxAperture01 => maxAperture01;
        public float ApertureWheelStep => apertureWheelStep;

        public float BeamLengthFadePower => beamLengthFadePower;
        public float BeamViewRimPower => beamViewRimPower;
        public float BeamDepthFadeDistance => beamDepthFadeDistance;

        /// <summary>상단 월드 광원의 outer angle을 계산한다.</summary>
        public float GetTopOuterAngle(float aperture01)
        {
            return Mathf.Lerp(explorationTopOuterAngleWide, explorationTopOuterAngleNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>상단 월드 광원의 inner angle을 계산한다.</summary>
        public float GetTopInnerAngle(float aperture01)
        {
            return GetTopOuterAngle(aperture01) * innerOuterAngleRatio;
        }

        /// <summary>상단 월드 광원의 range를 계산한다.</summary>
        public float GetTopRange(float aperture01)
        {
            return Mathf.Lerp(explorationTopRangeWide, explorationTopRangeNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>상단 월드 광원의 intensity를 계산한다.</summary>
        public float GetTopWorldIntensity(float aperture01)
        {
            float t = Mathf.Clamp01(aperture01);
            float multiplier = Mathf.Lerp(wideApertureIntensityMultiplier, narrowApertureIntensityMultiplier, t);
            return explorationTopWorldBaseIntensity * multiplier;
        }

        /// <summary>하단 월드 광원의 outer angle을 계산한다.</summary>
        public float GetBottomOuterAngle(float aperture01)
        {
            return Mathf.Lerp(harvestBottomOuterAngleWide, harvestBottomOuterAngleNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>하단 월드 광원의 inner angle을 계산한다.</summary>
        public float GetBottomInnerAngle(float aperture01)
        {
            return GetBottomOuterAngle(aperture01) * innerOuterAngleRatio;
        }

        /// <summary>하단 월드 광원의 range를 계산한다.</summary>
        public float GetBottomRange(float aperture01)
        {
            return Mathf.Lerp(harvestBottomRangeWide, harvestBottomRangeNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>하단 월드 광원의 intensity를 계산한다.</summary>
        public float GetBottomWorldIntensity(float aperture01)
        {
            float t = Mathf.Clamp01(aperture01);
            float multiplier = Mathf.Lerp(wideApertureIntensityMultiplier, narrowApertureIntensityMultiplier, t);
            return harvestBottomWorldBaseIntensity * multiplier;
        }

        /// <summary>상단 빔 스케일을 계산한다.</summary>
        public Vector3 GetTopBeamScale(float aperture01)
        {
            return Vector3.Lerp(topBeamScaleWide, topBeamScaleNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>하단 빔 스케일을 계산한다.</summary>
        public Vector3 GetBottomBeamScale(float aperture01)
        {
            return Vector3.Lerp(bottomBeamScaleWide, bottomBeamScaleNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>상단 빔 투명도를 계산한다.</summary>
        public float GetTopBeamOpacity(float aperture01)
        {
            return Mathf.Lerp(topBeamOpacityWide, topBeamOpacityNarrow, Mathf.Clamp01(aperture01));
        }

        /// <summary>하단 빔 투명도를 계산한다.</summary>
        public float GetBottomBeamOpacity(float aperture01)
        {
            return Mathf.Lerp(bottomBeamOpacityWide, bottomBeamOpacityNarrow, Mathf.Clamp01(aperture01));
        }
    }
}
