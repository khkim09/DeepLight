using System.Text;
using Project.Data.World;
using Project.Gameplay.World;
using TMPro;
using UnityEngine;

namespace Project.Gameplay.World.Debug
{
    /// <summary>월드맵 디버그 HUD의 순수 View 계층 (텍스트 렌더링 전용)</summary>
    public class WorldMapDebugHUDView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI hudText;
        [SerializeField] private GameObject backgroundPanel;

        [Header("Formatting")]
        [SerializeField] private string labelColor = "#88CCFF";
        [SerializeField] private string valueColor = "#FFFFFF";
        [SerializeField] private string warningColor = "#FF8888";
        [SerializeField] private string successColor = "#88FF88";

        private readonly StringBuilder _sb = new(512);

        /// <summary>View 초기화 (런타임 설치자용)</summary>
        public void Initialize(TextMeshProUGUI textComponent, GameObject panel)
        {
            hudText = textComponent;
            backgroundPanel = panel;
        }

        /// <summary>HUD 텍스트 업데이트 (서비스 상태 없음)</summary>
        public void ShowUninitialized()
        {
            if (hudText == null) return;
            hudText.text = "<color=#FF8888>[WorldMap HUD] Not initialized</color>\nAssign a WorldMapRuntimeTest or IWorldMapService provider.";
            if (backgroundPanel != null) backgroundPanel.SetActive(true);
        }

        /// <summary>HUD 텍스트 업데이트 (위치가 월드 경계 밖)</summary>
        public void ShowOutOfBounds(Vector3 trackedPosition)
        {
            if (hudText == null) return;
            _sb.Clear();
            _sb.AppendLine($"<color={warningColor}>[OUT OF BOUNDS]</color>");
            _sb.AppendLine($"  Pos: ({trackedPosition.x:F1}, {trackedPosition.y:F1}, {trackedPosition.z:F1})");
            hudText.text = _sb.ToString();
            if (backgroundPanel != null) backgroundPanel.SetActive(true);
        }

        /// <summary>HUD 텍스트 업데이트 (현재 존 있음 + Ambient 정보 포함)</summary>
        public void ShowCurrentZoneState(
            ZoneId currentZoneId,
            RegionId currentRegionId,
            ZoneRuntimeState state,
            Vector3 trackedPosition,
            bool zoneChanged,
            string testZoneQueryResult,
            string ambientProfileId = null,
            string ambientDisplayName = null,
            string bgmStateKey = null,
            bool dangerWarningActive = false,
            float riskOverlayIntensity = 0f)
        {
            if (hudText == null) return;

            _sb.Clear();

            // 헤더
            _sb.AppendLine($"<color={labelColor}>=== WorldMap Debug HUD ===</color>");

            // 현재 존 정보
            _sb.AppendLine($"<color={labelColor}>Zone:</color> <color={valueColor}>{currentZoneId}</color>");
            _sb.AppendLine($"<color={labelColor}>Region:</color> <color={valueColor}>{currentRegionId}</color>");

            // 접근성
            string accessibilityColor = state.Accessibility switch
            {
                ZoneAccessibility.Accessible => successColor,
                ZoneAccessibility.AccessibleWithRisk => warningColor,
                ZoneAccessibility.Locked => warningColor,
                _ => warningColor
            };
            _sb.AppendLine($"<color={labelColor}>Accessibility:</color> <color={accessibilityColor}>{state.Accessibility}</color>");

            // 잠금 사유
            _sb.AppendLine($"<color={labelColor}>Lock Reason:</color> <color={valueColor}>{state.LockReason}</color>");

            // 발견/해금/위험 상태
            string discoveredColor = state.IsDiscovered ? successColor : warningColor;
            string unlockedColor = state.IsUnlocked ? successColor : warningColor;
            string riskyColor = state.IsRisky ? warningColor : successColor;

            _sb.AppendLine($"<color={labelColor}>Discovered:</color> <color={discoveredColor}>{state.IsDiscovered}</color>");
            _sb.AppendLine($"<color={labelColor}>Unlocked:</color> <color={unlockedColor}>{state.IsUnlocked}</color>");
            _sb.AppendLine($"<color={labelColor}>Risky:</color> <color={riskyColor}>{state.IsRisky}</color>");

            // 위험도
            string riskLevelColor = state.CurrentRiskLevel > 0.5f ? warningColor : valueColor;
            _sb.AppendLine($"<color={labelColor}>Risk Level:</color> <color={riskLevelColor}>{state.CurrentRiskLevel:P1}</color>");

            // 추적 위치
            _sb.AppendLine($"<color={labelColor}>Tracked Pos:</color> <color={valueColor}>({trackedPosition.x:F1}, {trackedPosition.y:F1}, {trackedPosition.z:F1})</color>");

            // 존 변경 여부
            string changedText = zoneChanged ? "<color=#FFFF88>CHANGED</color>" : "<color=#888888>unchanged</color>";
            _sb.AppendLine($"<color={labelColor}>Zone Changed:</color> {changedText}");

            // 실패 조건
            if (state.FailedConditions.Count > 0)
            {
                _sb.AppendLine($"<color={warningColor}>Failed Conditions:</color>");
                foreach (var condition in state.FailedConditions)
                {
                    _sb.AppendLine($"  <color={warningColor}>- {condition.ConditionType}: {condition.ConditionKey} ({condition.RequiredValue})</color>");
                }
            }

            // 테스트 존 조회 결과 (현재 존의 상세 상태)
            if (!string.IsNullOrEmpty(testZoneQueryResult))
            {
                _sb.AppendLine();
                _sb.AppendLine($"<color={labelColor}>--- Current Zone Detail ---</color>");
                _sb.AppendLine(testZoneQueryResult);
            }

            // Ambient Profile 정보 (Phase 6)
            if (!string.IsNullOrEmpty(ambientProfileId))
            {
                _sb.AppendLine();
                _sb.AppendLine($"<color={labelColor}>--- Ambient Profile ---</color>");
                _sb.AppendLine($"<color={labelColor}>Profile:</color> <color={valueColor}>{ambientProfileId}</color>");
                if (!string.IsNullOrEmpty(ambientDisplayName))
                    _sb.AppendLine($"<color={labelColor}>Display:</color> <color={valueColor}>{ambientDisplayName}</color>");
                if (!string.IsNullOrEmpty(bgmStateKey))
                    _sb.AppendLine($"<color={labelColor}>BGM Key:</color> <color={valueColor}>{bgmStateKey}</color>");
                if (dangerWarningActive)
                {
                    _sb.AppendLine($"<color={warningColor}>⚠ DANGER WARNING ACTIVE (Intensity: {riskOverlayIntensity:P0})</color>");
                }
            }

            hudText.text = _sb.ToString();
            if (backgroundPanel != null) backgroundPanel.SetActive(true);
        }

        /// <summary>HUD 숨김</summary>
        public void Hide()
        {
            if (hudText != null) hudText.text = string.Empty;
            if (backgroundPanel != null) backgroundPanel.SetActive(false);
        }

        /// <summary>HUD 표시 여부</summary>
        public bool IsVisible => backgroundPanel == null || backgroundPanel.activeSelf;
    }
}
