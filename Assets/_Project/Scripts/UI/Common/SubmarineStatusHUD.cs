using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.UI
{
    /// <summary>배터리와 내구도 상태를 공통 HUD에 원형 게이지 및 퍼센트 단위로 표시하는 클래스이다.</summary>
    public class SubmarineStatusHUD : MonoBehaviour
    {
        [Header("Battery")]
        [SerializeField] private Image batteryFillImage; // 배터리 채워지는 링 이미지
        [SerializeField] private RectTransform batteryHandlePivot; // 배터리 핸들 회전축
        [SerializeField] private TMP_Text batteryText; // 배터리 퍼센트 텍스트

        [Header("Durability")]
        [SerializeField] private Image durabilityFillImage; // 내구도 채워지는 링 이미지
        [SerializeField] private RectTransform durabilityHandlePivot; // 내구도 핸들 회전축
        [SerializeField] private TMP_Text durabilityText; // 내구도 퍼센트 텍스트

        /// <summary>상태 변경 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Subscribe<HullDurabilityChangedEvent>(OnHullDurabilityChanged);
        }

        /// <summary>상태 변경 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Unsubscribe<HullDurabilityChangedEvent>(OnHullDurabilityChanged);
        }

        /// <summary>배터리 UI를 갱신한다.</summary>
        private void OnBatteryChanged(BatteryChangedEvent publishedEvent)
        {
            float maxBattery = Mathf.Max(1f, publishedEvent.MaxBattery);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentBattery / maxBattery);

            UpdateCircularGauge(batteryFillImage, batteryHandlePivot, batteryText, normalized);
        }

        /// <summary>내구도 UI를 갱신한다.</summary>
        private void OnHullDurabilityChanged(HullDurabilityChangedEvent publishedEvent)
        {
            float maxDurability = Mathf.Max(1f, publishedEvent.MaxDurability);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentDurability / maxDurability);

            UpdateCircularGauge(durabilityFillImage, durabilityHandlePivot, durabilityText, normalized);
        }

        /// <summary>원형 UI 게이지, 핸들 회전, 텍스트를 일괄적으로 업데이트하는 헬퍼 메서드</summary>
        private void UpdateCircularGauge(Image fillImage, RectTransform handlePivot, TMP_Text percentText, float normalizedValue)
        {
            // 1. 게이지 채우기
            if (fillImage != null)
            {
                fillImage.fillAmount = normalizedValue;
            }

            // 2. 핸들 위치(회전) 조정
            if (handlePivot != null)
            {
                // Top(12시) 기준, 시계방향 진행이므로 Z축을 음수 방향으로 회전
                float rotationAngle = normalizedValue * 360f;
                handlePivot.localRotation = Quaternion.Euler(0f, 0f, rotationAngle);
            }

            // 3. 퍼센트 텍스트 업데이트
            if (percentText != null)
            {
                percentText.text = $"{normalizedValue * 100f:0}";
            }
        }
    }
}
