using Project.Core.Events;
using TMPro;
using UnityEngine;

namespace Project.UI.Debug
{
    /// <summary>채집 세션 상태를 텍스트 디버깅</summary>
    public class HarvestSessionPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text modeText; // 현재 모드 텍스트
        [SerializeField] private TMP_Text sessionText; // 세션 상태 텍스트
        [SerializeField] private TMP_Text previewText; // 예상 확률 텍스트
        [SerializeField] private TMP_Text resultText; // 결과 텍스트

        /// <summary>이벤트 구독을 등록</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Subscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Subscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
            EventBus.Subscribe<HarvestAttemptStartedEvent>(OnHarvestAttemptStarted);
            EventBus.Subscribe<HarvestAttemptResolvedEvent>(OnHarvestAttemptResolved);
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
        }

        /// <summary>초기 텍스트를 설정</summary>
        private void Start()
        {
            if (modeText != null)
                modeText.text = "Mode : Exploration3D";

            if (sessionText != null)
                sessionText.text = "Session : None";

            if (previewText != null)
                previewText.text = "Preview : -";

            if (resultText != null)
                resultText.text = "Result : -";
        }

        /// <summary>모드 변경 텍스트를 갱신</summary>
        private void OnGameModeChanged(GameModeChangedEvent publishedEvent)
        {
            if (modeText != null)
                modeText.text = $"Mode : {publishedEvent.CurrentMode}";
        }

        /// <summary>세션 시작 텍스트를 갱신</summary>
        private void OnHarvestSessionStarted(HarvestSessionStartedEvent publishedEvent)
        {
            if (sessionText != null)
                sessionText.text = $"Session : Target={publishedEvent.TargetId}, Item={publishedEvent.ItemId}";
        }

        /// <summary>세션 종료 텍스트를 갱신</summary>
        private void OnHarvestSessionEnded(HarvestSessionEndedEvent publishedEvent)
        {
            if (sessionText != null)
                sessionText.text = $"Session : Ended ({publishedEvent.TargetId})";
        }

        /// <summary>방전 종료 텍스트를 갱신</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            if (sessionText != null)
                sessionText.text = $"Session : Forced End by Battery ({publishedEvent.TargetId})";
        }

        /// <summary>채집 시도 시작 텍스트를 갱신</summary>
        private void OnHarvestAttemptStarted(HarvestAttemptStartedEvent publishedEvent)
        {
            if (previewText != null)
                previewText.text = $"Preview : {publishedEvent.ItemId} / {(publishedEvent.PreviewChance * 100f):0.0}%";
        }

        /// <summary>채집 시도 결과 텍스트를 갱신</summary>
        private void OnHarvestAttemptResolved(HarvestAttemptResolvedEvent publishedEvent)
        {
            if (resultText != null)
                resultText.text = $"Result : {publishedEvent.ItemId} / Success={publishedEvent.IsSuccess} / Added={publishedEvent.AddedToInventory} / Chance={(publishedEvent.FinalChance * 100f):0.0}%";
        }
    }
}
