using System;
using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>인벤토리 레이아웃의 사용 가능 셀을 정의하는 구조체</summary>
    [Serializable]
    public struct SubmarineInventoryLayoutCell
    {
        public Vector2Int Position; // 사용 가능 셀 좌표

        /// <summary>사용 가능 셀 좌표를 생성한다</summary>
        public SubmarineInventoryLayoutCell(Vector2Int position)
        {
            Position = position;
        }
    }
}
