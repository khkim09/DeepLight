using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>잠수함 인벤토리 레이아웃과 셀 마스크 생성 기능을 보관한다.</summary>
    [CreateAssetMenu(fileName = "InventoryLayout_Submarine", menuName = "Project/Submarine/Inventory Layout")]
    public class SubmarineInventoryLayoutSO : ScriptableObject
    {
        [Header("UI Visual Settings")]
        public Vector2 CellSize = new Vector2(55f, 55f); // 1칸 크기
        public Vector2 Spacing = new Vector2(5f, 5f); // 타일 간 여백

        [Header("Logical Grid Data")]
        public Vector2Int GridSize = new Vector2Int(6, 8); // 전체 그리드 규격
        public List<Vector2Int> UsableCells = new(); // 실제 사용 가능한 셀 좌표 목록

        /// <summary>현재 GridSize 기준으로 기본 잠수함 형태를 생성한다.</summary>
        [ContextMenu("Build Submarine Shape")]
        public void BuildSubmarineShape()
        {
            // 현재 기본형은 6x8 전용
            GridSize = new Vector2Int(6, 8);
            BuildShape(new[] { 2, 4, 4, 4, 6, 6, 4, 2 });
        }

        /// <summary>Lv1 업그레이드용 세로 확장 형태를 생성한다.</summary>
        [ContextMenu("Build LV1 Shape")]
        public void BuildLv1Shape()
        {
            // 위/아래만 늘린 구조
            GridSize = new Vector2Int(6, 10);
            BuildShape(new[] { 2, 4, 4, 6, 6, 6, 6, 4, 4, 2 });
        }

        /// <summary>Lv2 업그레이드용 가로/세로 확장 형태를 생성한다.</summary>
        [ContextMenu("Build LV2 Shape")]
        public void BuildLv2Shape()
        {
            // 중간부 폭을 확장한 구조
            GridSize = new Vector2Int(8, 12);
            BuildShape(new[] { 2, 4, 4, 6, 6, 8, 8, 8, 8, 6, 4, 2 });
        }

        /// <summary>행별 너비 데이터로 좌우 대칭 usable cell을 생성한다.</summary>
        public void BuildShape(int[] rows)
        {
            UsableCells.Clear();

            if (rows == null || rows.Length == 0)
                return;

            for (int y = 0; y < rows.Length; y++)
            {
                int rowWidth = rows[y];

                // 비정상 행 데이터 방어
                if (rowWidth <= 0 || rowWidth > GridSize.x)
                    continue;

                // 좌우 중앙 정렬 시작점 계산
                int startX = (GridSize.x - rowWidth) / 2;

                for (int x = 0; x < rowWidth; x++)
                    UsableCells.Add(new Vector2Int(startX + x, y));
            }
        }

        /// <summary>논리 배치 검사용 2D 마스크 배열을 생성하여 반환한다.</summary>
        public bool[,] BuildUsableMask()
        {
            bool[,] mask = new bool[GridSize.x, GridSize.y];

            foreach (Vector2Int cell in UsableCells)
            {
                // 수동 편집 중 잘못 들어간 좌표 방어
                if (cell.x < 0 || cell.x >= GridSize.x || cell.y < 0 || cell.y >= GridSize.y)
                    continue;

                mask[cell.x, cell.y] = true;
            }

            return mask;
        }

        /// <summary>현재 레이아웃 데이터가 유효한지 검사한다.</summary>
        public bool IsValid()
        {
            if (GridSize.x <= 0 || GridSize.y <= 0)
                return false;

            if (UsableCells == null || UsableCells.Count == 0)
                return false;

            for (int i = 0; i < UsableCells.Count; i++)
            {
                Vector2Int cell = UsableCells[i];
                if (cell.x < 0 || cell.x >= GridSize.x || cell.y < 0 || cell.y >= GridSize.y)
                    return false;
            }

            return true;
        }
    }
}
