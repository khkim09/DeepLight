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

        private void Awake()
        {
            // 최초 1회만 초기 상태 적용
            HidePanelImmediate();
            ResetView();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<HarvestRecoveryPlanMetricsUpdatedEvent>(OnPlanMetricsUpdated);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<HarvestRecoveryPlanMetricsUpdatedEvent>(OnPlanMetricsUpdated);
        }

        /// <summary>Harvest 진입 시 패널을 표시한다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            ShowPanelImmediate();
            ResetView();
        }

        /// <summary>Harvest 종료 시 패널을 숨기고 초기화한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
            HidePanelImmediate();
            ResetView();
        }

        /// <summary>회수 계획 상태 이벤트를 받아 고정 패널을 갱신한다.</summary>
        private void OnPlanMetricsUpdated(HarvestRecoveryPlanMetricsUpdatedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            ShowPanelImmediate();

            if (revealCountText != null)
                revealCountText.text = $"{publishedEvent.RevealedPointCount} / {publishedEvent.TotalPointCount}";

            int recommendedCount = publishedEvent.RecommendedPointCount > 0
                ? publishedEvent.RecommendedPointCount
                : defaultRecommendedCount;

            if (selectedCountText != null)
                selectedCountText.text = $"{publishedEvent.SelectedPointCount} / {recommendedCount}";

            if (firstAnchorScoreText != null)
                firstAnchorScoreText.text = $"{Mathf.RoundToInt(publishedEvent.FirstAnchorScore01 * 100f)}%";

            if (sequenceScoreText != null)
                sequenceScoreText.text = $"{Mathf.RoundToInt(publishedEvent.SequenceScore01 * 100f)}%";

            if (successChanceValueText != null)
                successChanceValueText.text = $"{Mathf.RoundToInt(publishedEvent.FinalChance01 * 100f)}";

            if (successChanceFillImage != null)
            {
                float chance01 = Mathf.Clamp01(publishedEvent.FinalChance01);
                successChanceFillImage.fillAmount = chance01;
                successChanceFillImage.color = EvaluateChanceColor(chance01);
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

        /// <summary>표시값을 초기 상태로 되돌린다.</summary>
        private void ResetView()
        {
            if (revealCountText != null)
                revealCountText.text = "0 / 0";

            if (selectedCountText != null)
                selectedCountText.text = $"0 / {defaultRecommendedCount}";

            if (firstAnchorScoreText != null)
                firstAnchorScoreText.text = "0%";

            if (sequenceScoreText != null)
                sequenceScoreText.text = "0%";

            if (successChanceValueText != null)
                successChanceValueText.text = "0";

            if (successChanceFillImage != null)
            {
                successChanceFillImage.fillAmount = 0f;
                successChanceFillImage.color = lowChanceColor;
            }
        }
    }
}
