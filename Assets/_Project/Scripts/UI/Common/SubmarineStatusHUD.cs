using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.UI
{
    /// <summary>배터리와 내구도 상태를 공통 HUD에 퍼센트 단위로 표시하는 클래스이다.</summary>
    public class SubmarineStatusHUD : MonoBehaviour
    {
        [Header("Battery")]
        [SerializeField] private Slider batterySlider; // 배터리 슬라이더
        [SerializeField] private TMP_Text batteryText; // 배터리 퍼센트 텍스트

        [Header("Durability")]
        [SerializeField] private Slider durabilitySlider; // 내구도 슬라이더
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

            if (batterySlider != null)
                batterySlider.value = normalized;

            if (batteryText != null)
                batteryText.text = $"{normalized * 100f:0}%";
        }

        /// <summary>내구도 UI를 갱신한다.</summary>
        private void OnHullDurabilityChanged(HullDurabilityChangedEvent publishedEvent)
        {
            float maxDurability = Mathf.Max(1f, publishedEvent.MaxDurability);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentDurability / maxDurability);

            if (durabilitySlider != null)
                durabilitySlider.value = normalized;

            if (durabilityText != null)
                durabilityText.text = $"{normalized * 100f:0}%";
        }
    }
}
