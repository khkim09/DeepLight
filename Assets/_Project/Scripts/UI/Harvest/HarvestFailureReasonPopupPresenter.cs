using Project.Core.Events;
using Project.Gameplay.Harvest;
using TMPro;
using UnityEngine;

namespace Project.UI.Harvest
{
    /// <summary>채집 실패 이유와 다음 시도 가이드를 팝업으로 표시한다.</summary>
    public class HarvestFailureReasonPopupPresenter : MonoBehaviour
    {
        [Header("Popup Root")]
        [SerializeField] private GameObject popupRoot; // 실제 켜고 끌 팝업 루트
        [SerializeField] private CanvasGroup popupGroup; // 팝업 CanvasGroup

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI titleText; // 제목 텍스트
        [SerializeField] private TextMeshProUGUI summaryText; // 실패 요약 텍스트
        [SerializeField] private TextMeshProUGUI reasonListText; // 실패 원인 목록 텍스트
        [SerializeField] private TextMeshProUGUI guideText; // 다음 시도 가이드 텍스트
        [SerializeField] private TextMeshProUGUI keyGuideText; // 키 가이드 텍스트

        /// <summary>시작 시 팝업을 숨긴다.</summary>
        private void Awake()
        {
            HideImmediate();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestRecoveryDiagnosticsEvent>(OnDiagnosticsReceived);
            EventBus.Subscribe<HarvestRecoveryResolvedEvent>(OnResolved);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestRecoveryDiagnosticsEvent>(OnDiagnosticsReceived);
            EventBus.Unsubscribe<HarvestRecoveryResolvedEvent>(OnResolved);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>외부 허브나 종료 처리에서 즉시 숨길 때 사용한다.</summary>
        public void HideImmediate()
        {
            if (popupGroup != null)
            {
                popupGroup.alpha = 0f;
                popupGroup.interactable = false;
                popupGroup.blocksRaycasts = false;
            }

            if (popupRoot != null)
                popupRoot.SetActive(false);
        }

        /// <summary>진단 이벤트를 받아 실패 팝업을 갱신한다.</summary>
        private void OnDiagnosticsReceived(HarvestRecoveryDiagnosticsEvent publishedEvent)
        {
            HarvestRecoveryDiagnosticReport report = publishedEvent.Report;
            if (report.IsSuccess)
            {
                HideImmediate();
                return;
            }

            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(report.SummaryTitle) ? "Recovery Failed" : report.SummaryTitle;

            if (summaryText != null)
                summaryText.text = report.SummaryBody;

            if (reasonListText != null)
                reasonListText.text = BuildReasonList(report);

            if (guideText != null)
                guideText.text = report.GuidanceMessage;

            if (keyGuideText != null)
                keyGuideText.text = "[Tab] Inventory   [X] Leave";

            ShowImmediate();
        }

        /// <summary>최종 회수 결과에서 성공이면 실패 팝업을 닫는다.</summary>
        private void OnResolved(HarvestRecoveryResolvedEvent publishedEvent)
        {
            if (publishedEvent.IsSuccess)
                HideImmediate();
        }

        /// <summary>Harvest 종료 시 팝업을 닫는다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            HideImmediate();
        }

        /// <summary>팝업을 즉시 표시한다.</summary>
        private void ShowImmediate()
        {
            if (popupRoot != null && !popupRoot.activeSelf)
                popupRoot.SetActive(true);

            if (popupGroup != null)
            {
                popupGroup.alpha = 1f;
                popupGroup.interactable = true;
                popupGroup.blocksRaycasts = true;
            }
        }

        /// <summary>리포트의 상위 실패 원인을 목록 문구로 변환한다.</summary>
        private string BuildReasonList(HarvestRecoveryDiagnosticReport report)
        {
            string first = ToReasonText(report.PrimaryReason);
            string second = ToReasonText(report.SecondaryReason);
            string third = ToReasonText(report.TertiaryReason);

            string result = string.Empty;

            if (!string.IsNullOrWhiteSpace(first))
                result += $"• {first}";

            if (!string.IsNullOrWhiteSpace(second))
                result += string.IsNullOrWhiteSpace(result) ? $"• {second}" : $"\n• {second}";

            if (!string.IsNullOrWhiteSpace(third))
                result += string.IsNullOrWhiteSpace(result) ? $"• {third}" : $"\n• {third}";

            return result;
        }

        /// <summary>실패 원인 enum을 사용자용 문구로 변환한다.</summary>
        private string ToReasonText(HarvestFailureReasonType reasonType)
        {
            return reasonType switch
            {
                HarvestFailureReasonType.InsufficientScans => "스캔 정보가 부족해 안전한 포인트 구분이 어려웠습니다.",
                HarvestFailureReasonType.InsufficientAnchorCount => "고정 포인트 수가 부족해 인양 축이 안정적으로 잡히지 않았습니다.",
                HarvestFailureReasonType.WeakFirstAnchor => "첫 고정점이 하중 중심을 제대로 잡지 못했습니다.",
                HarvestFailureReasonType.PoorSequenceBalance => "후속 고정 순서가 균형축을 따라가지 못해 장력이 편중되었습니다.",
                HarvestFailureReasonType.LowStructuralStability => "선택한 포인트 자체의 구조 안정성이 낮았습니다.",
                HarvestFailureReasonType.HighRiskPoints => "위험도가 높은 포인트를 포함해 구조 손상 위험이 커졌습니다.",
                HarvestFailureReasonType.BatteryExhausted => "회수 도중 배터리가 바닥나 시스템이 강제 종료되었습니다.",
                _ => string.Empty
            };
        }
    }
}
