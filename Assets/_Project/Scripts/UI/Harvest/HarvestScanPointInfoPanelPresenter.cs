using Project.Core.Events;
using TMPro;
using UnityEngine;

namespace Project.UI.Harvest
{
    /// <summary>hover 중인 scanpoint 상세 패널을 갱신한다.</summary>
    public class HarvestScanPointInfoPanelPresenter : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private CanvasGroup panelCanvasGroup; // hover 정보 전체 CanvasGroup

        [Header("Radar")]
        [SerializeField] private HarvestRadarChartGraphic radarGraphic; // 부채꼴 레이더 그래프

        [Header("Axis Name Texts")]
        [SerializeField] private TMP_Text topAxisNameText; // 위 축 이름
        [SerializeField] private TMP_Text bottomAxisNameText; // 아래 축 이름
        [SerializeField] private TMP_Text leftAxisNameText; // 왼 축 이름
        [SerializeField] private TMP_Text rightAxisNameText; // 오른 축 이름

        [Header("Value Text Roots")]
        [SerializeField] private RectTransform topValueRoot; // 위 값 루트
        [SerializeField] private RectTransform bottomValueRoot; // 아래 값 루트
        [SerializeField] private RectTransform leftValueRoot; // 왼 값 루트
        [SerializeField] private RectTransform rightValueRoot; // 오른 값 루트

        [SerializeField] private TMP_Text topValueText; // 위 값
        [SerializeField] private TMP_Text bottomValueText; // 아래 값
        [SerializeField] private TMP_Text leftValueText; // 왼 값
        [SerializeField] private TMP_Text rightValueText; // 오른 값

        [Header("Axis Labels")]
        [SerializeField] private string topAxisLabel = "Stability"; // 위 축 이름
        [SerializeField] private string bottomAxisLabel = "Sequence"; // 아래 축 이름
        [SerializeField] private string leftAxisLabel = "First\nAnchor"; // 왼 축 이름
        [SerializeField] private string rightAxisLabel = "Risk"; // 오른 축 이름

        [Header("Text Layout")]
        [SerializeField] private float valueInsidePadding = 18f; // inside 배치 기본 여유
        [SerializeField] private float valueOutsidePadding = 18f; // outside 배치 기본 여유
        [SerializeField][Range(0f, 1f)] private float insideThreshold01 = 0.5f; // inside / outside 기준

        private bool isHarvestMode; // 현재 Harvest 모드 여부
        private bool hasCurrentPoint; // 현재 표시 중인 포인트 존재 여부

        private float currentTopValue; // 현재 안정성
        private float currentBottomValue; // 현재 후속 순서 적합도
        private float currentLeftValue; // 현재 첫 앵커 적합도
        private float currentRightValue; // 현재 위험도

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<HarvestHoveredPointChangedEvent>(OnHoveredPointChanged);

            ApplyAxisNames();
            HidePanelImmediate();
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<HarvestHoveredPointChangedEvent>(OnHoveredPointChanged);
        }

        /// <summary>RectTransform 크기 변경 시 텍스트 배치를 다시 맞춘다.</summary>
        private void OnRectTransformDimensionsChange()
        {
            if (!isHarvestMode || !hasCurrentPoint)
                return;

            RefreshRadarTextLayout();
        }

        /// <summary>Harvest 진입 시 기본 숨김 상태로 준비한다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            hasCurrentPoint = false;
            HidePanelImmediate();
        }

        /// <summary>Harvest 종료 시 패널을 숨기고 상태를 초기화한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
            hasCurrentPoint = false;
            HidePanelImmediate();
        }

        /// <summary>hover 포인트 변경 시 패널을 갱신한다.</summary>
        private void OnHoveredPointChanged(HarvestHoveredPointChangedEvent publishedEvent)
        {
            if (!isHarvestMode)
            {
                HidePanelImmediate();
                return;
            }

            if (!publishedEvent.HasPoint)
            {
                hasCurrentPoint = false;
                HidePanelImmediate();
                return;
            }

            hasCurrentPoint = true;
            ShowPanelImmediate();

            // 현재 값 매핑
            currentTopValue = publishedEvent.BaseStability;
            currentBottomValue = publishedEvent.SequenceBias;
            currentLeftValue = publishedEvent.FirstAnchorBias;
            currentRightValue = publishedEvent.RiskWeight;

            // 숫자 텍스트 갱신
            if (topValueText != null)
                topValueText.text = $"{Mathf.RoundToInt(currentTopValue * 100f)}";

            if (bottomValueText != null)
                bottomValueText.text = $"{Mathf.RoundToInt(currentBottomValue * 100f)}";

            if (leftValueText != null)
                leftValueText.text = $"{Mathf.RoundToInt(currentLeftValue * 100f)}";

            if (rightValueText != null)
                rightValueText.text = $"{Mathf.RoundToInt(currentRightValue * 100f)}";

            // 레이더 그래프 갱신
            if (radarGraphic != null)
            {
                radarGraphic.SetValues(
                    currentTopValue,
                    currentBottomValue,
                    currentLeftValue,
                    currentRightValue);
            }

            RefreshRadarTextLayout();
        }

        /// <summary>축 이름 텍스트를 적용한다.</summary>
        private void ApplyAxisNames()
        {
            if (topAxisNameText != null)
                topAxisNameText.text = topAxisLabel;

            if (bottomAxisNameText != null)
                bottomAxisNameText.text = bottomAxisLabel;

            if (leftAxisNameText != null)
                leftAxisNameText.text = leftAxisLabel;

            if (rightAxisNameText != null)
                rightAxisNameText.text = rightAxisLabel;
        }

        /// <summary>차트 주변 값 텍스트의 위치를 갱신한다.</summary>
        private void RefreshRadarTextLayout()
        {
            if (radarGraphic == null)
                return;

            // 핵심:
            // GetValueLocalPosition에는 padding을 0으로 넣고 "기준 위치"만 받는다.
            // 그 다음 텍스트 절반 크기 + inside/outside padding + extra offset을 직접 더해
            // 글자 외곽선 기준 여유를 맞춘다.

            Vector2 topValuePosition = radarGraphic.GetValueLocalPosition(
                HarvestRadarChartGraphic.RadarAxis.Top,
                currentTopValue,
                insideThreshold01,
                0f,
                0f);

            bool topIsInside = currentTopValue >= insideThreshold01;
            float topHalfHeight = topValueText != null ? topValueText.preferredHeight * 0.5f : 0f;
            float topPadding = topIsInside
                ? valueInsidePadding + topHalfHeight
                : valueOutsidePadding + topHalfHeight;

            topValuePosition += topIsInside
                ? Vector2.down * topPadding
                : Vector2.up * topPadding;

            SetAnchoredPosition(topValueRoot, topValuePosition);

            Vector2 bottomValuePosition = radarGraphic.GetValueLocalPosition(
                HarvestRadarChartGraphic.RadarAxis.Bottom,
                currentBottomValue,
                insideThreshold01,
                0f,
                0f);

            bool bottomIsInside = currentBottomValue >= insideThreshold01;
            float bottomHalfHeight = bottomValueText != null ? bottomValueText.preferredHeight * 0.5f : 0f;
            float bottomPadding = bottomIsInside
                ? valueInsidePadding + bottomHalfHeight
                : valueOutsidePadding + bottomHalfHeight;

            bottomValuePosition += bottomIsInside
                ? Vector2.up * bottomPadding
                : Vector2.down * bottomPadding;

            SetAnchoredPosition(bottomValueRoot, bottomValuePosition);

            Vector2 leftValuePosition = radarGraphic.GetValueLocalPosition(
                HarvestRadarChartGraphic.RadarAxis.Left,
                currentLeftValue,
                insideThreshold01,
                0f,
                0f);

            bool leftIsInside = currentLeftValue >= insideThreshold01;
            float leftHalfWidth = leftValueText != null ? leftValueText.preferredWidth * 0.5f : 0f;
            float leftPadding = leftIsInside
                ? valueInsidePadding + leftHalfWidth
                : valueOutsidePadding + leftHalfWidth;

            leftValuePosition += leftIsInside
                ? Vector2.right * leftPadding
                : Vector2.left * leftPadding;

            SetAnchoredPosition(leftValueRoot, leftValuePosition);

            Vector2 rightValuePosition = radarGraphic.GetValueLocalPosition(
                HarvestRadarChartGraphic.RadarAxis.Right,
                currentRightValue,
                insideThreshold01,
                0f,
                0f);

            bool rightIsInside = currentRightValue >= insideThreshold01;
            float rightHalfWidth = rightValueText != null ? rightValueText.preferredWidth * 0.5f : 0f;
            float rightPadding = rightIsInside
                ? valueInsidePadding + rightHalfWidth
                : valueOutsidePadding + rightHalfWidth;

            rightValuePosition += rightIsInside
            ? Vector2.left * rightPadding
            : Vector2.right * rightPadding;

            SetAnchoredPosition(rightValueRoot, rightValuePosition);
        }

        /// <summary>RectTransform anchoredPosition을 설정한다.</summary>
        private void SetAnchoredPosition(RectTransform target, Vector2 localPosition)
        {
            if (target == null)
                return;

            target.anchoredPosition = localPosition;
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
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }

            currentTopValue = 0f;
            currentBottomValue = 0f;
            currentLeftValue = 0f;
            currentRightValue = 0f;

            if (radarGraphic != null)
                radarGraphic.ClearValues();

            if (topValueText != null)
                topValueText.text = "--";

            if (bottomValueText != null)
                bottomValueText.text = "--";

            if (leftValueText != null)
                leftValueText.text = "--";

            if (rightValueText != null)
                rightValueText.text = "--";
        }
    }
}
