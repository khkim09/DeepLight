using System.Collections.Generic;
using Project.Core.Events;
using Project.Gameplay.Inventory;
using Project.Gameplay.Runtime;
using UnityEngine;

namespace Project.Gameplay.DebugView
{
    /// <summary>우측 인벤토리 그리드 디버그 표시를 담당하는 클래스</summary>
    public class InventoryGridDebugView : MonoBehaviour
    {
        [SerializeField] private RectTransform gridRoot; // 그리드 부모
        [SerializeField] private InventoryCellDebugView cellPrefab; // 셀 프리팹
        [SerializeField] private Vector2 padding = new Vector2(24f, 24f); // 패널 내부 여백
        [SerializeField] private Vector2 spacing = new Vector2(4f, 4f); // 셀 간격
        [SerializeField] private float maxCellSize = 56f; // 셀 최대 크기

        private SubmarineRuntimeState submarineRuntimeState; // 잠수함 상태
        private readonly List<InventoryCellDebugView> spawnedCells = new(); // 생성된 셀 목록

        /// <summary>인벤토리 표시 대상을 초기화한다</summary>
        public void Initialize(SubmarineRuntimeState newSubmarineRuntimeState)
        {
            // 상태 저장
            submarineRuntimeState = newSubmarineRuntimeState;

            // 그리드 생성
            BuildGrid();

            // 표시 갱신
            RefreshGrid();
        }

        /// <summary>이벤트 구독을 등록한다</summary>
        private void OnEnable()
        {
            // 인벤토리 변경 구독
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
        private void OnDisable()
        {
            // 인벤토리 변경 해제
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        /// <summary>영역 크기 변경 시 그리드를 다시 맞춘다</summary>
        private void OnRectTransformDimensionsChange()
        {
            // 초기화 안 됐으면 중단
            if (submarineRuntimeState == null || gridRoot == null)
                return;

            // 다시 배치
            RepositionCells();
        }

        /// <summary>그리드 셀을 생성한다</summary>
        private void BuildGrid()
        {
            // 상태 없으면 중단
            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return;

            if (gridRoot == null || cellPrefab == null)
                return;

            // 기존 셀 제거
            ClearCells();

            InventoryGridData grid = submarineRuntimeState.InventoryGrid; // 인벤토리 그리드 참조
            int totalCount = grid.Width * grid.Height; // 전체 셀 수 계산

            // 셀 생성
            for (int i = 0; i < totalCount; i++)
            {
                InventoryCellDebugView cell = Instantiate(cellPrefab, gridRoot); // 셀 생성
                spawnedCells.Add(cell);
            }

            // 위치 재배치
            RepositionCells();
        }

        /// <summary>패널 영역에 맞게 셀 위치를 재배치한다</summary>
        private void RepositionCells()
        {
            // 상태 검사
            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return;

            if (gridRoot == null)
                return;

            InventoryGridData grid = submarineRuntimeState.InventoryGrid; // 인벤토리 그리드 참조

            // 사용 가능한 영역 계산
            float availableWidth = gridRoot.rect.width - padding.x * 2f;
            float availableHeight = gridRoot.rect.height - padding.y * 2f;
            if (availableWidth <= 0f || availableHeight <= 0f)
                return;

            // 셀 크기 계산
            float cellWidth = (availableWidth - (grid.Width - 1) * spacing.x) / grid.Width;
            float cellHeight = (availableHeight - (grid.Height - 1) * spacing.y) / grid.Height;
            float cellSize = Mathf.Min(cellWidth, cellHeight, maxCellSize);

            // 실제 전체 크기 계산
            float totalWidth = grid.Width * cellSize + (grid.Width - 1) * spacing.x;
            float totalHeight = grid.Height * cellSize + (grid.Height - 1) * spacing.y;

            // 중앙 정렬 시작점 계산
            float startX = (gridRoot.rect.width - totalWidth) * 0.5f;
            float startY = -(gridRoot.rect.height - totalHeight) * 0.5f;

            int cellIndex = 0; // 셀 인덱스

            // 전체 셀 재배치
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    InventoryCellDebugView cell = spawnedCells[cellIndex];
                    RectTransform rect = cell.GetComponent<RectTransform>();

                    // 앵커 고정
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);

                    // 크기 설정
                    rect.sizeDelta = new Vector2(cellSize, cellSize);

                    // 위치 설정
                    float posX = startX + x * (cellSize + spacing.x);
                    float posY = startY - y * (cellSize + spacing.y);
                    rect.anchoredPosition = new Vector2(posX, posY);

                    cellIndex++;
                }
            }
        }

        /// <summary>그리드 표시를 갱신한다</summary>
        private void RefreshGrid()
        {
            // 상태 검사
            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return;

            InventoryGridData grid = submarineRuntimeState.InventoryGrid;
            int cellIndex = 0;

            // 전체 셀 갱신
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    InventoryCellDebugView cell = spawnedCells[cellIndex];
                    Vector2Int position = new Vector2Int(x, y);

                    // 사용 불가 칸이면 숨김
                    if (!grid.IsUsableCell(position))
                    {
                        cell.SetDisabled();
                        cellIndex++;
                        continue;
                    }

                    // 아이템 조회
                    InventoryItemInstance item = grid.GetItemAt(position);

                    // 빈 칸 표시
                    if (item == null)
                        cell.SetEmpty(x, y);
                    else
                        cell.SetOccupied(item.ItemData.DisplayName);

                    cellIndex++;
                }
            }
        }

        /// <summary>생성된 셀을 제거한다</summary>
        private void ClearCells()
        {
            // 셀 제거
            for (int i = 0; i < spawnedCells.Count; i++)
            {
                if (spawnedCells[i] != null)
                    Destroy(spawnedCells[i].gameObject);
            }

            spawnedCells.Clear();
        }

        /// <summary>인벤토리 변경 시 그리드를 갱신한다</summary>
        private void OnInventoryChanged(InventoryChangedEvent publishedEvent)
        {
            // 표시 갱신
            RefreshGrid();
        }
    }
}
