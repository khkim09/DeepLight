using Project.Core.Events;
using TMPro;
using UnityEngine;

namespace Project.Gameplay.DebugView
{
    /// <summary>배터리 상태 텍스트 디버깅</summary>
    public class BatteryDebugView : MonoBehaviour
    {
        [SerializeField] private TMP_Text batteryText; // 배터리 텍스트

        /// <summary>이벤트 구독 등록</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<BatteryChangedEvent>(OnBatteryChanged);
        }

        /// <summary>이벤트 구독 해제</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<BatteryChangedEvent>(OnBatteryChanged);
        }

        /// <summary>배터리 상태를 텍스트에 반영</summary>
        private void OnBatteryChanged(BatteryChangedEvent publishedEvent)
        {
            // 텍스트 없으면 중단
            if (batteryText == null) return;

            batteryText.text = $"Battery : {publishedEvent.CurrentBattery:0.0} / {publishedEvent.MaxBattery:0.0}";
        }
    }
}
