using Project.Core.Events;
using TMPro;
using UnityEngine;

namespace Project.Gameplay.DebugView
{
    /// <summary>선체와 로봇 팔 내구도 텍스트 디버깅</summary>
    public class DurabilityDebugView : MonoBehaviour
    {
        [SerializeField] private TMP_Text hullText; // 선체 내구도 텍스트
        [SerializeField] private TMP_Text clawText; // 팔 내구도 텍스트

        /// <summary>이벤트 구독 등록</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HullDurabilityChangedEvent>(OnHullDurabilityChanged);
            EventBus.Subscribe<ClawDurabilityChangedEvent>(OnClawDurabilityChanged);
        }

        /// <summary>이벤트 구독 해제</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HullDurabilityChangedEvent>(OnHullDurabilityChanged);
            EventBus.Unsubscribe<ClawDurabilityChangedEvent>(OnClawDurabilityChanged);
        }

        /// <summary>선체 내구도 텍스트 갱신</summary>
        private void OnHullDurabilityChanged(HullDurabilityChangedEvent publishedEvent)
        {
            if (hullText == null) return;

            hullText.text = $"Hull : {publishedEvent.CurrentDurability:0.0} / {publishedEvent.MaxDurability:0.0}";
        }

        /// <summary>팔 내구도 텍스트 갱신</summary>
        private void OnClawDurabilityChanged(ClawDurabilityChangedEvent publishedEvent)
        {
            if (clawText == null) return;

            clawText.text = $"Claw : {publishedEvent.CurrentDurability:0.0} / {publishedEvent.MaxDurability:0.0}";
        }
    }
}
