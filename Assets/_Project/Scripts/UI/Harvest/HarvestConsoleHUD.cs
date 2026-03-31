using TMPro;
using Project.Core.Events;
using Project.Data.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>Harvest 콘솔 HUD 전체 텍스트와 확률 바, 툴팁을 갱신하는 UI 클래스이다.</summary>
    public class HarvestConsoleHUD : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameInputBindingsSO inputBindings;

        [Header("Canvas")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private Canvas rootCanvas;

        [Header("Top")]
        [SerializeField] private TMP_Text targetNameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Image probabilityFillImage;
        [SerializeField] private TMP_Text probabilityText;
        [SerializeField] private Color lowChanceColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color midChanceColor = new(1f, 0.78f, 0.2f, 1f);
        [SerializeField] private Color highChanceColor = new(0.3f, 0.95f, 0.35f, 1f);

        [Header("Bottom Left")]
        [SerializeField] private TMP_Text sensorModeText;
        [SerializeField] private TMP_Text batteryText;
        [SerializeField] private TMP_Text durabilityText;

        [Header("Guides")]
        [SerializeField] private TMP_Text mouseGuideText;
        [SerializeField] private TMP_Text keyboardGuideText;
        [SerializeField] private TMP_Text resetGuideText;
        [SerializeField] private TMP_Text exitGuideText;
        [SerializeField] private TMP_Text inventoryGuideText;

        [Header("Tooltip")]
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TMP_Text tooltipTitleText;
        [SerializeField] private TMP_Text tooltipBodyText;
        [SerializeField] private Vector2 tooltipOffset = new(28f, -20f);

        private int currentScanMode;

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<HarvestConsoleTargetPreparedEvent>(OnTargetPrepared);
            EventBus.Subscribe<HarvestSelectionSequenceChangedEvent>(OnSelectionSequenceChanged);
            EventBus.Subscribe<HarvestScanModeChangedEvent>(OnScanModeChanged);
            EventBus.Subscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Subscribe<HullDurabilityChangedEvent>(OnDurabilityChanged);
            EventBus.Subscribe<HarvestRecoveryPreviewUpdatedEvent>(OnPreviewUpdated);
            EventBus.Subscribe<HarvestHoveredPointChangedEvent>(OnHoveredPointChanged);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<HarvestConsoleTargetPreparedEvent>(OnTargetPrepared);
            EventBus.Unsubscribe<HarvestSelectionSequenceChangedEvent>(OnSelectionSequenceChanged);
            EventBus.Unsubscribe<HarvestScanModeChangedEvent>(OnScanModeChanged);
            EventBus.Unsubscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Unsubscribe<HullDurabilityChangedEvent>(OnDurabilityChanged);
            EventBus.Unsubscribe<HarvestRecoveryPreviewUpdatedEvent>(OnPreviewUpdated);
            EventBus.Unsubscribe<HarvestHoveredPointChangedEvent>(OnHoveredPointChanged);
        }

        /// <summary>초기 상태를 적용한다.</summary>
        private void Start()
        {
            ApplyStaticGuideTexts();
            SetVisible(false);
            SetTooltipVisible(false);
        }

        /// <summary>Harvest HUD를 표시한다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            SetVisible(true);
        }

        /// <summary>Harvest HUD를 숨긴다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            SetVisible(false);
            SetTooltipVisible(false);
        }

        /// <summary>타깃 이름과 총 포인트 개수를 갱신한다.</summary>
        private void OnTargetPrepared(HarvestConsoleTargetPreparedEvent publishedEvent)
        {
            if (targetNameText != null)
                targetNameText.text = publishedEvent.DisplayName;

            if (countText != null)
                countText.text = $"0 / {publishedEvent.TotalPointCount}";
        }

        /// <summary>선택 개수 표시를 갱신한다.</summary>
        private void OnSelectionSequenceChanged(HarvestSelectionSequenceChangedEvent publishedEvent)
        {
            if (countText != null)
                countText.text = $"{publishedEvent.SelectedCount} / {publishedEvent.TotalCount}";
        }

        /// <summary>센서 모드 텍스트를 갱신한다.</summary>
        private void OnScanModeChanged(HarvestScanModeChangedEvent publishedEvent)
        {
            currentScanMode = publishedEvent.ScanMode;

            if (sensorModeText == null)
                return;

            sensorModeText.text = publishedEvent.ScanMode == 0
                ? "센서 : 없음"
                : publishedEvent.ScanMode == 1
                    ? "센서 : 소나"
                    : "센서 : 라이다";
        }

        /// <summary>배터리 텍스트를 갱신한다.</summary>
        private void OnBatteryChanged(BatteryChangedEvent publishedEvent)
        {
            if (batteryText != null)
                batteryText.text = $"배터리 : {publishedEvent.CurrentBattery:0}/{publishedEvent.MaxBattery:0}";
        }

        /// <summary>내구도 텍스트를 갱신한다.</summary>
        private void OnDurabilityChanged(HullDurabilityChangedEvent publishedEvent)
        {
            if (durabilityText != null)
                durabilityText.text = $"내구도 : {publishedEvent.CurrentDurability:0}/{publishedEvent.MaxDurability:0}";
        }

        /// <summary>성공 확률 바와 텍스트를 갱신한다.</summary>
        private void OnPreviewUpdated(HarvestRecoveryPreviewUpdatedEvent publishedEvent)
        {
            float chance01 = Mathf.Clamp01(publishedEvent.RecoveryChance);

            if (probabilityFillImage != null)
            {
                probabilityFillImage.fillAmount = chance01;
                probabilityFillImage.color = chance01 < 0.25f
                    ? lowChanceColor
                    : chance01 < 0.65f
                        ? midChanceColor
                        : highChanceColor;
            }

            if (probabilityText != null)
                probabilityText.text = $"성공 확률 {chance01 * 100f:0}%";
        }

        /// <summary>hover 포인트 툴팁을 갱신한다.</summary>
        private void OnHoveredPointChanged(HarvestHoveredPointChangedEvent publishedEvent)
        {
            if (!publishedEvent.HasPoint)
            {
                SetTooltipVisible(false);
                return;
            }

            if (tooltipTitleText != null)
                tooltipTitleText.text = publishedEvent.DisplayLabel;

            if (tooltipBodyText != null)
            {
                string sensorText = currentScanMode == 1
                    ? $"소나 반응 : {publishedEvent.SonarSignature * 100f:0}%"
                    : currentScanMode == 2
                        ? $"라이다 반응 : {publishedEvent.LidarSignature * 100f:0}%"
                        : $"소나 : {publishedEvent.SonarSignature * 100f:0}%\n라이다 : {publishedEvent.LidarSignature * 100f:0}%";

                string orderText = publishedEvent.AssignedOrder > 0
                    ? $"\n현재 순번 : {publishedEvent.AssignedOrder}"
                    : "\n현재 순번 : 미지정";

                tooltipBodyText.text = sensorText + orderText;
            }

            UpdateTooltipPosition(publishedEvent.ScreenPosition);
            SetTooltipVisible(true);
        }

        /// <summary>고정 키 가이드 문구를 적용한다.</summary>
        private void ApplyStaticGuideTexts()
        {
            if (mouseGuideText != null)
            {
                mouseGuideText.text =
                    "LMB + Drag : 타깃 회전\n" +
                    "LMB : 다음 순서 선택\n" +
                    "RMB : 해당 순서 제거";
            }

            if (keyboardGuideText != null)
            {
                string scanKey = inputBindings != null ? inputBindings.HarvestScanKey.ToString() : "F";
                string switchKey = inputBindings != null ? inputBindings.HarvestSwitchSensorKey.ToString() : "R";
                string commitKey = inputBindings != null ? inputBindings.HarvestCommitKey.ToString() : "Space";

                keyboardGuideText.text =
                    $"{scanKey} : 스캔\n" +
                    $"{switchKey} : 센서 전환\n" +
                    $"{commitKey} : 회수";
            }

            if (resetGuideText != null)
            {
                string resetKey = inputBindings != null ? inputBindings.HarvestResetSequenceKey.ToString() : "Delete";
                resetGuideText.text = $"{resetKey} : 순서 리셋";
            }

            if (exitGuideText != null)
            {
                string exitKey = inputBindings != null ? inputBindings.HarvestExitKey.ToString() : "X";
                exitGuideText.text = $"{exitKey} : 떠나기";
            }

            if (inventoryGuideText != null)
            {
                string inventoryKey = inputBindings != null ? inputBindings.HarvestInventoryKey.ToString() : "Tab";
                inventoryGuideText.text = $"{inventoryKey} : 인벤토리";
            }
        }

        /// <summary>HUD 전체 표시 여부를 적용한다.</summary>
        private void SetVisible(bool visible)
        {
            if (rootCanvasGroup == null)
                return;

            rootCanvasGroup.alpha = visible ? 1f : 0f;
            rootCanvasGroup.blocksRaycasts = visible;
            rootCanvasGroup.interactable = visible;
        }

        /// <summary>툴팁 표시 여부를 적용한다.</summary>
        private void SetTooltipVisible(bool visible)
        {
            if (tooltipRoot != null)
                tooltipRoot.gameObject.SetActive(visible);
        }

        /// <summary>스크린 좌표 기준으로 툴팁 위치를 갱신한다.</summary>
        private void UpdateTooltipPosition(Vector2 screenPosition)
        {
            if (tooltipRoot == null || rootCanvas == null)
                return;

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition + tooltipOffset,
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                out Vector2 localPoint);

            tooltipRoot.anchoredPosition = localPoint;
        }
    }
}
