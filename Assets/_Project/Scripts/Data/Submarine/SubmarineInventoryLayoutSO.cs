using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>잠수함의 인벤토리 논리적 마스크와 시각적 격자 수치 설정값을 보관한다.</summary>
    [CreateAssetMenu(fileName = "InventoryLayout_Submarine", menuName = "Project/Submarine/Inventory Layout")]
    public class SubmarineInventoryLayoutSO : ScriptableObject
    {
        [Header("UI Visual Settings")]
        public Vector2 CellSize = new Vector2(55f, 55f); // 1칸 크기 (UI 종속성 없이 값만 제공)
        public Vector2 Spacing = new Vector2(5f, 5f); // 타일 간 여백

        [Header("Logical Grid Data")]
        public Vector2Int GridSize = new Vector2Int(6, 8); // 최대 그리드 규격
        public List<Vector2Int> UsableCells = new(); // 실제 사용 가능한 칸 좌표 목록

        /// <summary>기획된 형태로 셀 데이터를 즉시 생성한다.</summary>
        [ContextMenu("Build Submarine Shape")]
        public void BuildSubmarineShape()
        {
            UsableCells.Clear();
            int[] rows = { 2, 4, 4, 4, 6, 6, 4, 2 };
            for (int y = 0; y < rows.Length; y++)
            {
                int startX = (GridSize.x - rows[y]) / 2;
                for (int x = 0; x < rows[y]; x++)
                    UsableCells.Add(new Vector2Int(startX + x, y));
            }
        }

        /// <summary>논리적 배치 검사용 2D 마스크 배열을 생성하여 반환한다.</summary>
        public bool[,] BuildUsableMask()
        {
            bool[,] mask = new bool[GridSize.x, GridSize.y];
            foreach (var cell in UsableCells) mask[cell.x, cell.y] = true;
            return mask;
        }

        public bool IsValid() => UsableCells.Count > 0;
    }
}
