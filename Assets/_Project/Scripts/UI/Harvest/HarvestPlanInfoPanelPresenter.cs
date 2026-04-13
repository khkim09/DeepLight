using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>전체 회수 계획 고정 패널을 갱신한다.</summary>
    public class HarvestPlanInfoPanelPresenter : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private CanvasGroup panelCanvasGroup; // 전체 계획 패널 CanvasGroup

        [Header("Texts")]
        [SerializeField] private TMP_Text revealCountText; // 공개 개수 / 총 개수
        [SerializeField] private TMP_Text selectedCountText; // 선택 개수 / 권장 개수
        [SerializeField] private TMP_Text firstAnchorScoreText; // 현재 1번 포인트 앵커 점수
        [SerializeField] private TMP_Text sequenceScoreText; // 현재 순서 균형 점수
        [SerializeField] private TMP_Text successChanceValueText; // 실제 갱신되는 확률 텍스트

        [Header("Chance Ring")]
        [SerializeField] private Image successChanceFillImage; // 원형 fill ring
        [SerializeField] private Color lowChanceColor = new(1f, 0.2f, 0.2f, 1f); // 빨강
        [SerializeField] private Color midChanceColor = new(1f, 0.78f, 0.15f, 1f); // 노랑
        [SerializeField] private Color highChanceColor = new(0.45f, 1f, 0.45f, 1f); // 초록

        [Header("Thresholds")]
        [SerializeField][Range(0f, 1f)] private float lowChanceThreshold01 = 0.20f; // 빨강 상한
        [SerializeField][Range(0f, 1f)] private float midChanceThreshold01 = 0.65f; // 노랑 상한

        [Header("Defaults")]
        [SerializeField] private int defaultRecommendedCount = 3; // fallback 권장 개수

        private bool isHarvestMode; // 현재 Harvest 모드 여부
        private string activeTargetKey = string.Empty; // 현재 표시 중인 타깃 키

        private int currentTotalPointCount; // 현재 타깃 총 포인트 수
        private int currentRevealedPointCount; // 현재 공개 포인트 수
        private int currentSelectedPointCount; // 현재 선택 포인트 수
        private int currentRecommendedPointCount; // 현재 권장 포인트 수

        /// <summary>초기 상태를 적용한다.</summary>
        private void Awake()
        {
            HidePanelImmediate();
            ResetRuntimeState();
            ApplyAllTextsImmediate();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);

            EventBus.Subscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Subscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);

            EventBus.Subscribe<HarvestConsoleTargetPreparedEvent>(OnTargetPrepared);
            EventBus.Subscribe<HarvestPointRevealedEvent>(OnPointRevealed);
            EventBus.Subscribe<HarvestSelectionSequenceChangedEvent>(OnSelectionSequenceChanged);
            EventBus.Subscribe<HarvestRecoveryPreviewUpdatedEvent>(OnRecoveryPreviewUpdated);
            EventBus.Subscribe<HarvestRecoveryPlanMetricsUpdatedEvent>(OnPlanMetricsUpdated);
            EventBus.Subscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);

            EventBus.Unsubscribe<HarvestSessionStartedEvent>(OnHarvestSessionStarted);
            EventBus.Unsubscribe<HarvestSessionEndedEvent>(OnHarvestSessionEnded);

            EventBus.Unsubscribe<HarvestConsoleTargetPreparedEvent>(OnTargetPrepared);
            EventBus.Unsubscribe<HarvestPointRevealedEvent>(OnPointRevealed);
            EventBus.Unsubscribe<HarvestSelectionSequenceChangedEvent>(OnSelectionSequenceChanged);
            EventBus.Unsubscribe<HarvestRecoveryPreviewUpdatedEvent>(OnRecoveryPreviewUpdated);
            EventBus.Unsubscribe<HarvestRecoveryPlanMetricsUpdatedEvent>(OnPlanMetricsUpdated);
            EventBus.Unsubscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved);

            activeTargetKey = string.Empty;
        }

        /// <summary>채집 세션 시작 시 현재 활성 타깃 키를 저장한다.</summary>
        private void OnHarvestSessionStarted(HarvestSessionStartedEvent publishedEvent)
        {
            activeTargetKey = publishedEvent.TargetKey;
        }

        /// <summary>채집 세션 종료 시 현재 활성 타깃 키를 비운다.</summary>
        private void OnHarvestSessionEnded(HarvestSessionEndedEvent publishedEvent)
        {
            if (activeTargetKey == publishedEvent.TargetKey)
                activeTargetKey = string.Empty;
        }

        /// <summary>Harvest 진입 시 패널을 초기화하고 표시한다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            ResetRuntimeState();
            ShowPanelImmediate();
            ApplyAllTextsImmediate();
        }

        /// <summary>Harvest 카메라 전환 완료 시 패널을 다시 초기화한다.</summary>
        private void OnHarvestCameraTransitionCompleted(HarvestCameraTransitionCompletedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            ResetRuntimeState();
            ShowPanelImmediate();
            ApplyAllTextsImmediate();
        }

        /// <summary>Harvest 종료 시 패널을 숨기고 초기화한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
            activeTargetKey = string.Empty;

            ResetRuntimeState();
            HidePanelImmediate();
            ApplyAllTextsImmediate();
        }

        /// <summary>채집 성공 시 패널을 알파 0으로 숨긴다.</summary>
        private void OnHarvestRecoveryResolved(HarvestRecoveryResolvedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            if (!publishedEvent.IsSuccess)
                return;

            // 오브젝트는 활성 상태로 유지하고, 표시만 끈다.
            HidePanelImmediate();
        }

        /// <summary>현재 타깃 준비 완료 시 기본 0 상태를 현재 타깃 총 개수 기준으로 맞춘다.</summary>
        private void OnTargetPrepared(HarvestConsoleTargetPreparedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            currentTotalPointCount = Mathf.Max(0, publishedEvent.TotalPointCount);
            currentRevealedPointCount = 0;
            currentSelectedPointCount = 0;
            currentRecommendedPointCount = Mathf.Min(
                defaultRecommendedCount,
                Mathf.Max(1, currentTotalPointCount));

            ShowPanelImmediate();
            ApplyCountTextsImmediate();
            ApplyAnchorSequenceTextsImmediate(0f, 0f);
            ApplyChanceImmediate(0f);
        }

        /// <summary>회수 포인트 공개 이벤트를 반영한다.</summary>
        private void OnPointRevealed(HarvestPointRevealedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            currentRevealedPointCount = Mathf.Clamp(
                currentRevealedPointCount + 1,
                0,
                currentTotalPointCount);

            ApplyRevealTextImmediate();
        }

        /// <summary>선택 순서 개수 변경 이벤트를 반영한다.</summary>
        private void OnSelectionSequenceChanged(HarvestSelectionSequenceChangedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            currentSelectedPointCount = Mathf.Max(0, publishedEvent.SelectedCount);
            currentTotalPointCount = Mathf.Max(currentTotalPointCount, publishedEvent.TotalCount);

            if (currentRecommendedPointCount <= 0)
            {
                currentRecommendedPointCount = Mathf.Min(
                    defaultRecommendedCount,
                    Mathf.Max(1, currentTotalPointCount));
            }

            ApplySelectedTextImmediate();
        }

        /// <summary>추정 성공률 이벤트를 받아 chance text와 ring을 갱신한다.</summary>
        private void OnRecoveryPreviewUpdated(HarvestRecoveryPreviewUpdatedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            ApplyChanceImmediate(publishedEvent.RecoveryChance);
        }

        /// <summary>회수 계획 상태 이벤트를 받아 앵커/순서 점수와 count 보정을 반영한다.</summary>
        private void OnPlanMetricsUpdated(HarvestRecoveryPlanMetricsUpdatedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            if (string.IsNullOrWhiteSpace(activeTargetKey))
                activeTargetKey = publishedEvent.TargetKey;

            if (publishedEvent.TargetKey != activeTargetKey)
                return;

            currentTotalPointCount = Mathf.Max(0, publishedEvent.TotalPointCount);
            currentRevealedPointCount = Mathf.Clamp(publishedEvent.RevealedPointCount, 0, currentTotalPointCount);
            currentSelectedPointCount = Mathf.Clamp(publishedEvent.SelectedPointCount, 0, currentTotalPointCount);
            currentRecommendedPointCount = publishedEvent.RecommendedPointCount > 0
                ? publishedEvent.RecommendedPointCount
                : Mathf.Min(defaultRecommendedCount, Mathf.Max(1, currentTotalPointCount));

            ShowPanelImmediate();
            ApplyCountTextsImmediate();
            ApplyAnchorSequenceTextsImmediate(
                publishedEvent.FirstAnchorScore01,
                publishedEvent.SequenceScore01);
            ApplyChanceImmediate(publishedEvent.FinalChance01);
        }

        /// <summary>런타임 표시 상태를 초기화한다.</summary>
        private void ResetRuntimeState()
        {
            currentTotalPointCount = 0;
            currentRevealedPointCount = 0;
            currentSelectedPointCount = 0;
            currentRecommendedPointCount = defaultRecommendedCount;
        }

        /// <summary>모든 텍스트와 링 상태를 즉시 반영한다.</summary>
        private void ApplyAllTextsImmediate()
        {
            ApplyCountTextsImmediate();
            ApplyAnchorSequenceTextsImmediate(0f, 0f);
            ApplyChanceImmediate(0f);
        }

        /// <summary>개수 관련 텍스트를 즉시 반영한다.</summary>
        private void ApplyCountTextsImmediate()
        {
            ApplyRevealTextImmediate();
            ApplySelectedTextImmediate();
        }

        /// <summary>공개 개수 텍스트를 즉시 반영한다.</summary>
        private void ApplyRevealTextImmediate()
        {
            if (revealCountText != null)
                revealCountText.text = $"{currentRevealedPointCount} / {currentTotalPointCount}";
        }

        /// <summary>선택 개수 텍스트를 즉시 반영한다.</summary>
        private void ApplySelectedTextImmediate()
        {
            if (selectedCountText != null)
                selectedCountText.text = $"{currentSelectedPointCount} / {currentRecommendedPointCount}";
        }

        /// <summary>앵커/시퀀스 점수 텍스트를 즉시 반영한다.</summary>
        private void ApplyAnchorSequenceTextsImmediate(float firstAnchorScore01, float sequenceScore01)
        {
            if (firstAnchorScoreText != null)
                firstAnchorScoreText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(firstAnchorScore01) * 100f)}%";

            if (sequenceScoreText != null)
                sequenceScoreText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(sequenceScore01) * 100f)}%";
        }

        /// <summary>확률 텍스트와 링 상태를 즉시 반영한다.</summary>
        private void ApplyChanceImmediate(float chance01)
        {
            float clampedChance01 = Mathf.Clamp01(chance01);

            if (successChanceValueText != null)
                successChanceValueText.text = $"{Mathf.RoundToInt(clampedChance01 * 100f)}";

            if (successChanceFillImage != null)
            {
                successChanceFillImage.fillAmount = clampedChance01;
                successChanceFillImage.color = EvaluateChanceColor(clampedChance01);
            }
        }

        /// <summary>확률 구간에 맞는 ring 색상을 반환한다.</summary>
        private Color EvaluateChanceColor(float chance01)
        {
            if (chance01 < lowChanceThreshold01)
                return lowChanceColor;

            if (chance01 < midChanceThreshold01)
                return midChanceColor;

            return highChanceColor;
        }

        /// <summary>CanvasGroup을 즉시 보이게 한다.</summary>
        private void ShowPanelImmediate()
        {
            if (panelCanvasGroup == null)
                return;

            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        /// <summary>CanvasGroup을 즉시 숨긴다.</summary>
        private void HidePanelImmediate()
        {
            if (panelCanvasGroup == null)
                return;

            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
    }
}
