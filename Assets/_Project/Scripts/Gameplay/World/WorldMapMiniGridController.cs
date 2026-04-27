using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 미니맵 시드 Controller.
    /// IWorldMapService를 주입받아 현재 존 기준 3x3 이웃 그리드를 계산하고 View를 업데이트한다.
    ///
    /// 이 컴포넌트는 정식 미니맵이 아니다.
    /// 월드맵 데이터를 시각적으로 보여주는 최소한의 씨앗이다.
    ///
    /// Phase 9: 미니맵 시드 (full minimap 아님)
    /// </summary>
    public class WorldMapMiniGridController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldMapMiniGridView gridView;

        [Header("Submarine-Relative Settings")]
        [Tooltip("추적할 Transform (예: submarine root). 설정 시 이 Transform의 forward 방향을 그리드 '위쪽'으로 사용한다.")]
        [SerializeField] private Transform trackedTransform;
        [Tooltip("Submarine-relative 모드 활성화. trackedTransform의 forward 방향을 기준으로 3x3 그리드를 회전시킨다.")]
        [SerializeField] private bool useSubmarineRelative = false;

        [Header("Settings")]
        [SerializeField] private float updateInterval = 1.0f; // 그리드 업데이트 간격
        [SerializeField] private bool updateEveryFrame;

        // 내부 상태
        private IWorldMapService _service;
        private float _lastUpdateTime;
        private bool _hasInitializedService;

        /// <summary>Controller 초기화 (Installer가 호출)</summary>
        /// <param name="service">공유 IWorldMapService 인스턴스</param>
        public void Initialize(IWorldMapService service)
        {
            _service = service;
            _hasInitializedService = service != null;

            if (gridView != null)
            {
                gridView.Initialize();
            }

            if (_hasInitializedService)
            {
                // EventBus 구독: 존 변경 시 즉시 그리드 갱신
                EventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
                EventBus.Subscribe<CurrentZoneStateChangedEvent>(OnZoneStateChanged);
                EventBus.Subscribe<CurrentZoneClearedEvent>(OnZoneCleared);

                UnityEngine.Debug.Log("[WorldMapMiniGridController] Initialized with shared service. Subscribed to zone events.");
            }
        }

        private void OnDestroy()
        {
            if (_hasInitializedService)
            {
                EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChanged);
                EventBus.Unsubscribe<CurrentZoneStateChangedEvent>(OnZoneStateChanged);
                EventBus.Unsubscribe<CurrentZoneClearedEvent>(OnZoneCleared);
            }
        }

        private void Update()
        {
            if (!_hasInitializedService || _service == null)
            {
                gridView?.ClearGrid();
                return;
            }

            if (updateEveryFrame)
            {
                RefreshGrid();
                return;
            }

            if (Time.unscaledTime - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.unscaledTime;
                RefreshGrid();
            }
        }

        private void OnZoneChanged(ZoneChangedEvent evt)
        {
            RefreshGrid();
        }

        private void OnZoneStateChanged(CurrentZoneStateChangedEvent evt)
        {
            RefreshGrid();
        }

        private void OnZoneCleared(CurrentZoneClearedEvent evt)
        {
            gridView?.ClearGrid();
        }

        /// <summary>
        /// 그리드 갱신 (현재 존 기준 3x3 이웃).
        /// useSubmarineRelative가 true이고 trackedTransform이 설정되어 있으면,
        /// trackedTransform.forward 방향을 그리드 '위쪽'으로 간주하고 이웃 좌표를 회전시킨다.
        /// </summary>
        [ContextMenu("Refresh Grid")]
        public void RefreshGrid()
        {
            if (!_hasInitializedService || _service == null || !_service.HasCurrentZone)
            {
                gridView?.ClearGrid();
                return;
            }

            ZoneId centerZoneId = _service.CurrentZoneId;

            // 3x3 이웃 좌표 계산 (row-major: 0=top-left, 4=center, 8=bottom-right)
            ZoneRuntimeState[] neighborStates = new ZoneRuntimeState[9];
            char centerColumn = centerZoneId.Column;
            int centerRow = centerZoneId.Row;

            // Submarine-relative 모드: trackedTransform.forward 방향을 기준으로 이웃 오프셋을 회전
            bool useRelative = useSubmarineRelative && trackedTransform != null;

            for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (int colOffset = -1; colOffset <= 1; colOffset++)
                {
                    int index = (rowOffset + 1) * 3 + (colOffset + 1); // row-major

                    // 중앙(현재 존)은 오프셋 불필요
                    if (rowOffset == 0 && colOffset == 0)
                    {
                        if (_service.TryGetCurrentZoneState(out ZoneRuntimeState currentState))
                        {
                            neighborStates[index] = currentState;
                        }
                        continue;
                    }

                    // Submarine-relative: forward 방향에 따라 col/row 오프셋을 회전
                    int actualColOffset = colOffset;
                    int actualRowOffset = rowOffset;

                    if (useRelative)
                    {
                        // trackedTransform.forward를 월드 Z+ (North) 방향과 비교하여
                        // 그리드의 '위쪽' 방향을 결정한다.
                        Vector3 forward = trackedTransform.forward;
                        // XZ 평면에서의 각도 계산 (라디안)
                        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                        // 45도 단위로 8방향 중 하나로 양자화
                        int octant = Mathf.RoundToInt(angle / 45f);
                        // 0~7 범위로 정규화 (0 = North, 1 = NE, 2 = East, ...)
                        octant = ((octant % 8) + 8) % 8;

                        // octant에 따라 (colOffset, rowOffset)을 회전
                        // octant 0 (North) = 원본 그대로
                        // octant 1 (NE) = col+row, row-col
                        // octant 2 (East) = row, -col
                        // octant 3 (SE) = col-row, -col-row
                        // octant 4 (South) = -col, -row
                        // octant 5 (SW) = -col-row, col-row
                        // octant 6 (West) = -row, col
                        // octant 7 (NW) = row-col, col+row
                        switch (octant)
                        {
                            case 0: // North (Z+)
                                actualColOffset = colOffset;
                                actualRowOffset = rowOffset;
                                break;
                            case 1: // NE
                                actualColOffset = colOffset + rowOffset;
                                actualRowOffset = rowOffset - colOffset;
                                break;
                            case 2: // East (X+)
                                actualColOffset = rowOffset;
                                actualRowOffset = -colOffset;
                                break;
                            case 3: // SE
                                actualColOffset = colOffset - rowOffset;
                                actualRowOffset = -colOffset - rowOffset;
                                break;
                            case 4: // South (Z-)
                                actualColOffset = -colOffset;
                                actualRowOffset = -rowOffset;
                                break;
                            case 5: // SW
                                actualColOffset = -colOffset - rowOffset;
                                actualRowOffset = colOffset - rowOffset;
                                break;
                            case 6: // West (X-)
                                actualColOffset = -rowOffset;
                                actualRowOffset = colOffset;
                                break;
                            case 7: // NW
                                actualColOffset = rowOffset - colOffset;
                                actualRowOffset = colOffset + rowOffset;
                                break;
                        }
                    }

                    char neighborColumn = (char)(centerColumn + actualColOffset);
                    int neighborRow = centerRow + actualRowOffset;

                    ZoneId neighborId = new ZoneId(neighborColumn, neighborRow);

                    // 이웃 존 조회
                    if (_service.TryGetZoneState(neighborId, out ZoneRuntimeState neighborState))
                    {
                        neighborStates[index] = neighborState;
                    }
                    // null = 데이터 없음 (그리드 경계 밖)
                }
            }

            gridView?.UpdateGrid(centerZoneId, neighborStates);
        }
    }
}
