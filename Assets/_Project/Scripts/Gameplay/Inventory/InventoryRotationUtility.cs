using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>인벤토리 아이템의 90도 단위 회전 계산을 담당하는 유틸리티이다.</summary>
    public static class InventoryRotationUtility
    {
        /// <summary>회전 쿼터 턴 값을 0~3 범위로 정규화한다.</summary>
        public static int NormalizeQuarterTurns(int rotationQuarterTurns)
        {
            int normalized = rotationQuarterTurns % 4;
            if (normalized < 0)
                normalized += 4;

            return normalized;
        }

        /// <summary>회전 상태를 반영한 bounding box 크기를 반환한다.</summary>
        public static Vector2Int GetRotatedBounds(Vector2Int originalBounds, int rotationQuarterTurns)
        {
            int normalized = NormalizeQuarterTurns(rotationQuarterTurns);

            // 0도/180도는 원래 bounds 유지, 90도/270도는 가로세로를 뒤집는다.
            if (normalized == 0 || normalized == 2)
                return originalBounds;

            return new Vector2Int(originalBounds.y, originalBounds.x);
        }

        /// <summary>원본 로컬 셀 좌표를 지정 회전값에 맞게 변환한다.</summary>
        public static Vector2Int RotateLocalCell(Vector2Int localCell, Vector2Int originalBounds, int rotationQuarterTurns)
        {
            int normalized = NormalizeQuarterTurns(rotationQuarterTurns);

            return normalized switch
            {
                // 0도
                0 => localCell,

                // 90도 시계 방향
                1 => new Vector2Int(originalBounds.y - 1 - localCell.y, localCell.x),

                // 180도
                2 => new Vector2Int(originalBounds.x - 1 - localCell.x, originalBounds.y - 1 - localCell.y),

                // 270도 시계 방향
                3 => new Vector2Int(localCell.y, originalBounds.x - 1 - localCell.x),

                _ => localCell
            };
        }

        /// <summary>UI 회전용 Z 각도를 반환한다.</summary>
        public static float GetRotationZ(int rotationQuarterTurns)
        {
            int normalized = NormalizeQuarterTurns(rotationQuarterTurns);
            return normalized switch
            {
                0 => 0f,
                1 => -90f,
                2 => -180f,
                3 => -270f,
                _ => 0f
            };
        }
    }
}
