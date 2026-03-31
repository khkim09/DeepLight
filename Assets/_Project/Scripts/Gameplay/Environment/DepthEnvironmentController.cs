using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Gameplay.Environment
{
    /// <summary>
    /// 플레이어(잠수함)의 수심(Y좌표)을 추적하여 런타임에 글로벌 안개와 포스트 프로세싱 색상을 제어합니다.
    /// </summary>
    public class DepthEnvironmentController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("수심을 추적할 기준 타겟 (잠수함)")]
        public Transform targetSubmarine;

        [Tooltip("색상을 조절할 Global Volume")]
        public Volume globalVolume;

        [Header("Depth Settings")]
        [Tooltip("수면의 Y 좌표 (진입점)")]
        public float surfaceY = 0f;

        [Tooltip("최대 심해의 Y 좌표 (완전한 어둠)")]
        public float maxDepthY = -100f;

        [Header("Colors (Left: Surface, Right: Deep)")]
        [Tooltip("수심에 따른 안개 색상 그라데이션")]
        public Gradient fogGradient;

        [Tooltip("수심에 따른 포스트 프로세싱(Color Filter) 그라데이션")]
        public Gradient postProcessGradient;

        // 조작할 포스트 프로세싱 컴포넌트 캐싱
        private ColorAdjustments _colorAdjustments;

        private void Start()
        {
            // 볼륨 프로파일에서 ColorAdjustments를 찾아 캐싱하여 매 프레임 GC 발생을 방지합니다.
            if (globalVolume != null && globalVolume.profile.TryGet(out _colorAdjustments) == false)
                Debug.LogWarning("[DepthEnvironment] Global Volume에 Color Adjustments 컴포넌트가 없습니다.");
        }

        private void Update()
        {
            if (targetSubmarine == null) return;

            // 1. 현재 Y 좌표를 기준으로 0.0 (수면) ~ 1.0 (심해) 사이의 보간 비율을 계산합니다.
            float depthRatio = Mathf.InverseLerp(surfaceY, maxDepthY, targetSubmarine.position.y);

            // 2. 안개 색상을 깊이 비율에 맞게 업데이트합니다.
            RenderSettings.fogColor = fogGradient.Evaluate(depthRatio);

            // 3. 포스트 프로세싱 컬러 필터 색상을 깊이 비율에 맞게 업데이트합니다.
            if (_colorAdjustments != null)
            {
                _colorAdjustments.colorFilter.value = postProcessGradient.Evaluate(depthRatio);
            }
        }
    }
}
