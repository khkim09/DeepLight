using Project.Data.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 플레이어가 실제로 볼 수 있는 최소 현재 존 HUD View.
    /// 디버그 HUD와 독립적이며, 전용 Canvas 또는 기존 HUD 앵커에 부착 가능.
    ///
    /// 표시 정보:
    /// - 현재 존 이름 (ZoneId + DisplayName)
    /// - 현재 리전 이름
    /// - 접근성/위험 상태 (SAFE / DANGER / LOCKED)
    /// - 발견 상태 (Discovered / Undiscovered)
    /// - 위험도 색상 틴트
    ///
    /// Phase 9: 사용자용 최소 HUD (full map 아님)
    /// </summary>
    public class WorldMapCurrentZoneHUDView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI zoneNameText;       // 존 이름 표시
        [SerializeField] private TextMeshProUGUI regionNameText;     // 리전 이름 표시
        [SerializeField] private TextMeshProUGUI riskStatusText;     // 위험 상태 텍스트
        [SerializeField] private TextMeshProUGUI discoveryStatusText; // 발견 상태 텍스트
        [SerializeField] private Image riskTintImage;                // 위험도 색상 틴트 (옵션)

        [Header("Display Settings")]
        [SerializeField] private Color safeColor = Color.green;
        [SerializeField] private Color dangerColor = new Color(1f, 0.4f, 0.2f);
        [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color discoveredColor = Color.white;
        [SerializeField] private Color undiscoveredColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color outOfBoundsColor = new Color(0.3f, 0.3f, 0.5f);

        /// <summary>View 초기화 (UI 참조가 없으면 경고)</summary>
        public void Initialize()
        {
            if (zoneNameText == null || regionNameText == null || riskStatusText == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapCurrentZoneHUDView] Some UI references are not assigned. Display will be limited.");
            }
        }

        /// <summary>현재 존 정보 표시</summary>
        public void ShowCurrentZone(ZoneId zoneId, RegionId regionId, ZoneRuntimeState state, Vector3 worldPosition)
        {
            if (zoneNameText != null)
            {
                string displayName = state?.ZoneData != null ? state.ZoneData.DisplayName : "Unknown";
                zoneNameText.text = $"{zoneId} - {displayName}";
            }

            if (regionNameText != null)
            {
                regionNameText.text = $"Region: {regionId}";
            }

            if (riskStatusText != null)
            {
                // ZoneRuntimeState.Accessibility 기반 상태 매핑
                // IsSafe: ZoneAccessibility.Accessible
                // IsRisky: ZoneAccessibility.AccessibleWithRisk
                // IsUnlocked: !Locked (Safe + Risky)
                if (state == null)
                {
                    riskStatusText.text = "OUT OF BOUNDS";
                    riskStatusText.color = outOfBoundsColor;
                }
                else if (state.IsSafe)
                {
                    riskStatusText.text = "SAFE";
                    riskStatusText.color = safeColor;
                }
                else if (state.IsRisky)
                {
                    riskStatusText.text = $"DANGER ({state.CurrentRiskLevel:P0})";
                    riskStatusText.color = dangerColor;
                }
                else
                {
                    // Locked
                    riskStatusText.text = "LOCKED";
                    riskStatusText.color = lockedColor;
                }
            }

            if (discoveryStatusText != null)
            {
                bool discovered = state?.IsDiscovered ?? false;
                discoveryStatusText.text = discovered ? "Discovered" : "Undiscovered";
                discoveryStatusText.color = discovered ? discoveredColor : undiscoveredColor;
            }

            if (riskTintImage != null)
            {
                if (state == null)
                {
                    riskTintImage.color = outOfBoundsColor;
                }
                else if (state.IsSafe)
                {
                    riskTintImage.color = safeColor;
                }
                else if (state.IsRisky)
                {
                    riskTintImage.color = Color.Lerp(safeColor, dangerColor, state.CurrentRiskLevel);
                }
                else
                {
                    riskTintImage.color = lockedColor;
                }
            }
        }

        /// <summary>HUD 숨김 (존 정보 없음)</summary>
        public void ShowOutOfBounds(Vector3 worldPosition)
        {
            if (zoneNameText != null)
                zoneNameText.text = "--- Out of Bounds ---";

            if (regionNameText != null)
                regionNameText.text = "Unknown Region";

            if (riskStatusText != null)
            {
                riskStatusText.text = "OUT OF BOUNDS";
                riskStatusText.color = outOfBoundsColor;
            }

            if (discoveryStatusText != null)
            {
                discoveryStatusText.text = "---";
                discoveryStatusText.color = undiscoveredColor;
            }

            if (riskTintImage != null)
                riskTintImage.color = outOfBoundsColor;
        }

        /// <summary>HUD 초기화 전 상태</summary>
        public void ShowUninitialized()
        {
            if (zoneNameText != null)
                zoneNameText.text = "World Map: Initializing...";

            if (regionNameText != null)
                regionNameText.text = "";

            if (riskStatusText != null)
            {
                riskStatusText.text = "WAITING";
                riskStatusText.color = lockedColor;
            }

            if (discoveryStatusText != null)
            {
                discoveryStatusText.text = "---";
                discoveryStatusText.color = undiscoveredColor;
            }

            if (riskTintImage != null)
                riskTintImage.color = lockedColor;
        }
    }
}
