using UnityEngine;

namespace Project.UI.Harvest
{
    /// <summary>레이더 trail과 needle을 함께 회전시킨다.</summary>
    public class HarvestRadarSweepRotator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform trailRect; // trail 이미지
        [SerializeField] private RectTransform needleRect; // 바늘 이미지
        [SerializeField] private CanvasGroup panelCanvasGroup; // 레이더 패널 표시 상태 기준

        [Header("Animation")]
        [SerializeField] private bool animateInPlayModeOnly = true; // 플레이 중에만 회전 여부
        [SerializeField] private float sweepSpeedDegreesPerSecond = 80f; // 회전 속도
        [SerializeField] private float initialAngleDeg; // 초기 각도
        [SerializeField] private float visibleAlphaThreshold = 0.01f; // 보이는 상태 판단 기준

        private float sweepAngleDeg; // 현재 회전 각도

        /// <summary>초기 상태를 설정한다.</summary>
        private void Awake()
        {
            sweepAngleDeg = initialAngleDeg;
            ApplyRotationImmediate();
        }

#if UNITY_EDITOR
        /// <summary>에디터 값 변경 시 현재 회전을 즉시 반영한다.</summary>
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                sweepAngleDeg = initialAngleDeg;
                ApplyRotationImmediate();
            }
        }
#endif

        /// <summary>매 프레임 trail과 needle의 회전을 갱신한다.</summary>
        private void Update()
        {
            if (animateInPlayModeOnly && !Application.isPlaying)
                return;

            // 패널이 실질적으로 안 보일 때는 회전을 멈춘다.
            if (!ShouldAnimate())
                return;

            float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0.016f;
            sweepAngleDeg = Mathf.Repeat(sweepAngleDeg - sweepSpeedDegreesPerSecond * deltaTime, 360f);

            ApplyRotationImmediate();
        }

        /// <summary>현재 각도를 trail과 needle에 즉시 적용한다.</summary>
        private void ApplyRotationImmediate()
        {
            Vector3 rotation = new Vector3(0f, 0f, sweepAngleDeg);

            if (trailRect != null)
                trailRect.localEulerAngles = rotation;

            if (needleRect != null)
                needleRect.localEulerAngles = rotation;
        }

        /// <summary>현재 패널 표시 상태 기준으로 회전 가능 여부를 반환한다.</summary>
        private bool ShouldAnimate()
        {
            if (panelCanvasGroup == null)
                return true;

            if (!panelCanvasGroup.gameObject.activeInHierarchy)
                return false;

            return panelCanvasGroup.alpha > visibleAlphaThreshold;
        }
    }
}
