using Project.Core.Events;
using TMPro;
using UnityEngine;

namespace Project.Gameplay.DebugView
{
    /// <summary>채집 세션 상태를 텍스트로 디버깅하는 클래스</summary>
    public class HarvestSessionDebugPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text modeText; // 현재 모드 텍스트
        [SerializeField] private TMP_Text sessionText; // 세션 상태 텍스트
        [SerializeField] private TMP_Text previewText; // 추정 회수 수치 텍스트
        [SerializeField] private TMP_Text resultText; // 최종 결과 텍스트

        private string currentSensorText = "Sensor=None";
        private string revealedText = "Revealed=-";
        private string selectedText = "Order=-";
        private float lastChance;
        private float lastBatteryCost;
        private float lastDurabilityCost;

        /// <summary>이벤트 구독을 등록한다</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Subscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Subscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
            EventBus.Subscribe<HarvestScanModeChangedEvent>(OnHarvestScanModeChanged);
            EventBus.Subscribe<HarvestPointRevealedEvent>(OnHarvestPointRevealed);
            EventBus.Subscribe<HarvestPointSelectedEvent>(OnHarvestPointSelected);
            EventBus.Subscribe<HarvestRecoveryPreviewUpdatedEvent>(OnHarvestRecoveryPreviewUpdated);
            EventBus.Subscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Unsubscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Unsubscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);
            EventBus.Unsubscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
            EventBus.Unsubscribe<HarvestScanModeChangedEvent>(OnHarvestScanModeChanged);
            EventBus.Unsubscribe<HarvestPointRevealedEvent>(OnHarvestPointRevealed);
            EventBus.Unsubscribe<HarvestPointSelectedEvent>(OnHarvestPointSelected);
            EventBus.Unsubscribe<HarvestRecoveryPreviewUpdatedEvent>(OnHarvestRecoveryPreviewUpdated);
            EventBus.Unsubscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved);
        }

        /// <summary>초기 텍스트를 설정한다</summary>
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

        /// <summary>모드 텍스트를 갱신한다</summary>
        private void OnGameModeChanged(GameModeChangedEvent publishedEvent)
        {
            if (modeText != null)
                modeText.text = $"Mode : {publishedEvent.CurrentMode}";
        }

        /// <summary>세션 시작 텍스트를 갱신한다</summary>
        private void OnHarvestSessionStarted(HarvestSessionStartedEvent publishedEvent)
        {
            currentSensorText = "Sensor=Sonar"; // 기본 시작 센서
            revealedText = "Revealed=-";
            selectedText = "Order=-";
            lastChance = 0f;
            lastBatteryCost = 0f;
            lastDurabilityCost = 0f;

            if (sessionText != null)
                sessionText.text = $"Session : Target={publishedEvent.TargetId}, Item={publishedEvent.ItemId}";

            RefreshPreviewText(); // 즉시 HUD 반영
        }

        /// <summary>세션 종료 텍스트를 갱신한다</summary>
        private void OnHarvestSessionEnded(HarvestSessionEndedEvent publishedEvent)
        {
            if (sessionText != null)
                sessionText.text = $"Session : Ended ({publishedEvent.TargetId})";
        }

        /// <summary>방전 종료 텍스트를 갱신한다</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            if (sessionText != null)
                sessionText.text = $"Session : Forced End by Battery ({publishedEvent.TargetId})";
        }

        /// <summary>센서 변경 텍스트를 갱신한다</summary>
        private void OnHarvestScanModeChanged(HarvestScanModeChangedEvent publishedEvent)
        {
            currentSensorText = publishedEvent.ScanMode == 1 ? "Sensor=Sonar" : "Sensor=Lidar";
            RefreshPreviewText(); // 이벤트 직후 즉시 텍스트 갱신
        }

        /// <summary>공개 포인트 텍스트를 갱신한다</summary>
        private void OnHarvestPointRevealed(HarvestPointRevealedEvent publishedEvent)
        {
            if (revealedText == "Revealed=-")
                revealedText = $"Revealed={publishedEvent.PointId}";
            else
                revealedText += $", {publishedEvent.PointId}";

            RefreshPreviewText(); // 즉시 갱신
        }

        /// <summary>선택 순서 텍스트를 갱신한다</summary>
        private void OnHarvestPointSelected(HarvestPointSelectedEvent publishedEvent)
        {
            if (selectedText == "Order=-")
                selectedText = $"Order={publishedEvent.PointId}";
            else
                selectedText += $" -> {publishedEvent.PointId}";

            RefreshPreviewText(); // 즉시 갱신
        }

        /// <summary>추정 회수 수치를 갱신한다</summary>
        private void OnHarvestRecoveryPreviewUpdated(HarvestRecoveryPreviewUpdatedEvent publishedEvent)
        {
            lastChance = publishedEvent.RecoveryChance; // 최신 추정 성공률 저장
            lastBatteryCost = publishedEvent.BatteryCost; // 최신 추정 배터리 비용 저장
            lastDurabilityCost = publishedEvent.DurabilityCost; // 최신 추정 내구도 비용 저장

            RefreshPreviewText(); // 즉시 갱신
        }

        /// <summary>최종 결과 텍스트를 갱신한다</summary>
        private void OnHarvestRecoveryResolved(HarvestRecoveryResolvedEvent publishedEvent)
        {
            if (resultText != null)
                resultText.text = $"Result : Item={publishedEvent.ItemId}, Success={publishedEvent.IsSuccess}, Added={publishedEvent.AddedToInventory}, Chance={(publishedEvent.FinalChance * 100f):0.0}%";
        }

        /// <summary>현재 상태를 기준으로 preview 텍스트를 다시 그린다</summary>
        private void RefreshPreviewText()
        {
            if (previewText == null)
                return;

            previewText.text =
                $"{currentSensorText}\n" +
                $"{revealedText}\n" +
                $"{selectedText}\n" +
                $"Chance={(lastChance * 100f):0.0}% / Battery={lastBatteryCost:0.0} / Durability={lastDurabilityCost:0.0}";
        }
    }
}
