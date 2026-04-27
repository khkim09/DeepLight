using Project.Data.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 미니맵 시드의 View 계층.
    /// 3x3 로컬 이웃 그리드로 현재 존과 인접 존의 상태를 표시한다.
    ///
    /// 이 컴포넌트는 정식 미니맵이 아니다.
    /// 월드맵 데이터를 시각적으로 보여주는 최소한의 씨앗이다.
    ///
    /// Phase 9: 미니맵 시드 (full minimap 아님)
    /// </summary>
    public class WorldMapMiniGridView : MonoBehaviour
    {
        [Header("Grid Cell References")]
        [SerializeField] private Image[] cellImages; // 9개 셀 (row-major: 0=top-left, 8=bottom-right)
        [SerializeField] private TextMeshProUGUI[] cellLabels; // 9개 셀 라벨 (옵션)

        [Header("Grid Settings")]
        [SerializeField] private Color currentCellColor = new Color(0.2f, 0.6f, 1f, 0.8f);
        [SerializeField] private Color safeCellColor = new Color(0.3f, 0.8f, 0.4f, 0.5f);
        [SerializeField] private Color dangerCellColor = new Color(1f, 0.4f, 0.2f, 0.5f);
        [SerializeField] private Color lockedCellColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        [SerializeField] private Color undiscoveredCellColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        [SerializeField] private Color emptyCellColor = new Color(0.1f, 0.1f, 0.1f, 0.2f);

        /// <summary>View 초기화 (셀 참조 검증)</summary>
        public void Initialize()
        {
            if (cellImages == null || cellImages.Length < 9)
            {
                UnityEngine.Debug.LogWarning("[WorldMapMiniGridView] Not enough cell images assigned. Need 9 cells.");
            }
        }

        /// <summary>
        /// 3x3 그리드 업데이트.
        /// centerZoneId가 현재 존이며, 그리드 중앙(인덱스 4)에 표시된다.
        /// </summary>
        /// <param name="centerZoneId">현재 존 ID (그리드 중앙)</param>
        /// <param name="neighborStates">이웃 존 상태 배열 (9개, null 허용)</param>
        public void UpdateGrid(ZoneId centerZoneId, ZoneRuntimeState[] neighborStates)
        {
            if (cellImages == null)
                return;

            for (int i = 0; i < 9 && i < cellImages.Length; i++)
            {
                Image cell = cellImages[i];
                if (cell == null)
                    continue;

                ZoneRuntimeState state = (neighborStates != null && i < neighborStates.Length) ? neighborStates[i] : null;

                if (i == 4)
                {
                    // 중앙 = 현재 존
                    cell.color = currentCellColor;
                }
                else if (state == null)
                {
                    // 데이터 없음 = 빈 셀
                    cell.color = emptyCellColor;
                }
                else if (!state.IsDiscovered)
                {
                    // 미발견
                    cell.color = undiscoveredCellColor;
                }
                else if (state.IsSafe)
                {
                    cell.color = safeCellColor;
                }
                else if (state.IsRisky)
                {
                    cell.color = dangerCellColor;
                }
                else
                {
                    // Locked
                    cell.color = lockedCellColor;
                }

                // 라벨 업데이트
                if (cellLabels != null && i < cellLabels.Length && cellLabels[i] != null)
                {
                    if (state != null)
                    {
                        cellLabels[i].text = state.ZoneId.ToString();
                    }
                    else
                    {
                        cellLabels[i].text = i == 4 ? centerZoneId.ToString() : "---";
                    }
                }
            }
        }

        /// <summary>그리드 초기화 (모든 셀 비움)</summary>
        public void ClearGrid()
        {
            if (cellImages == null)
                return;

            for (int i = 0; i < cellImages.Length; i++)
            {
                if (cellImages[i] != null)
                    cellImages[i].color = emptyCellColor;

                if (cellLabels != null && i < cellLabels.Length && cellLabels[i] != null)
                    cellLabels[i].text = "---";
            }
        }
    }
}
