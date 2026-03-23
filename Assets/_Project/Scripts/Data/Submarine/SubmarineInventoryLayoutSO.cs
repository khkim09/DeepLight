using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>배 모양 인벤토리 레이아웃을 정의하는 데이터 클래스</summary>
    [CreateAssetMenu(fileName = "SubmarineInventoryLayout_", menuName = "Project/Data/Submarine Inventory Layout")]
    public class SubmarineInventoryLayoutSO : ScriptableObject
    {
        [SerializeField] private Vector2Int gridSize = new Vector2Int(8, 8); // 전체 그리드 크기
        [SerializeField] private List<SubmarineInventoryLayoutCell> usableCells = new(); // 실제 사용 가능한 셀 목록

        public Vector2Int GridSize => gridSize;
        public IReadOnlyList<SubmarineInventoryLayoutCell> UsableCells => usableCells;

        /// <summary>사용 가능 셀 마스크를 생성한다</summary>
        public bool[,] BuildUsableMask()
        {
            // 마스크 생성
            bool[,] mask = new bool[gridSize.x, gridSize.y];

            // 셀 반영
            for (int i = 0; i < usableCells.Count; i++)
            {
                Vector2Int position = usableCells[i].Position;
                if (position.x < 0 || position.x >= gridSize.x)
                    continue;

                if (position.y < 0 || position.y >= gridSize.y)
                    continue;

                mask[position.x, position.y] = true;
            }

            return mask;
        }

        /// <summary>현재 레이아웃 데이터가 유효한지 검사한다</summary>
        public bool IsValid()
        {
            if (gridSize.x <= 0 || gridSize.y <= 0)
                return false;

            if (usableCells == null || usableCells.Count <= 0)
                return false;

            return true;
        }
    }
}
