using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.DebugView
{
    /// <summary>채집 관련 이벤트 디버깅</summary>
    public class HarvestEventDebugLogger : MonoBehaviour
    {
        /// <summary>이벤트 구독을 등록</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Subscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Subscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
            EventBus.Subscribe<HarvestAttemptStartedEvent>(OnHarvestAttemptStarted);
            EventBus.Subscribe<HarvestAttemptResolvedEvent>(OnHarvestAttemptResolved);
            EventBus.Subscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Subscribe<InventoryItemAddedEvent>(OnInventoryItemAdded);
        }

        /// <summary>이벤트 구독을 해제</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Unsubscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Unsubscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);
            EventBus.Unsubscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
            EventBus.Unsubscribe<HarvestAttemptStartedEvent>(OnHarvestAttemptStarted);
            EventBus.Unsubscribe<HarvestAttemptResolvedEvent>(OnHarvestAttemptResolved);
            EventBus.Unsubscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Unsubscribe<InventoryItemAddedEvent>(OnInventoryItemAdded);
        }

        /// <summary>모드 변경 로그를 출력</summary>
        private void OnGameModeChanged(GameModeChangedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Mode] {publishedEvent.PreviousMode} -> {publishedEvent.CurrentMode}");
        }

        /// <summary>세션 시작 로그를 출력</summary>
        private void OnHarvestSessionStarted(HarvestSessionStartedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Harvest Session Started] Target={publishedEvent.TargetId}, Item={publishedEvent.ItemId}");
        }

        /// <summary>세션 종료 로그를 출력</summary>
        private void OnHarvestSessionEnded(HarvestSessionEndedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Harvest Session Ended] Target={publishedEvent.TargetId}");
        }

        /// <summary>방전 종료 로그를 출력</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Harvest Session Forced End] Target={publishedEvent.TargetId}");
        }

        /// <summary>시도 시작 로그를 출력</summary>
        private void OnHarvestAttemptStarted(HarvestAttemptStartedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Harvest Attempt Started] Item={publishedEvent.ItemId}, Preview={(publishedEvent.PreviewChance * 100f):0.0}%");
        }

        /// <summary>시도 결과 로그를 출력</summary>
        private void OnHarvestAttemptResolved(HarvestAttemptResolvedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Harvest Attempt Resolved] Item={publishedEvent.ItemId}, Success={publishedEvent.IsSuccess}, Added={publishedEvent.AddedToInventory}, Chance={(publishedEvent.FinalChance * 100f):0.0}%");
        }

        /// <summary>배터리 로그를 출력</summary>
        private void OnBatteryChanged(BatteryChangedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Battery] {publishedEvent.CurrentBattery:0.0}/{publishedEvent.MaxBattery:0.0}");
        }

        /// <summary>인벤토리 추가 로그를 출력</summary>
        private void OnInventoryItemAdded(InventoryItemAddedEvent publishedEvent)
        {
            UnityEngine.Debug.Log($"[Inventory Added] Item={publishedEvent.ItemId}, Amount={publishedEvent.Amount}");
        }
    }
}
