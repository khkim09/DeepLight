using System;
using UnityEngine;

namespace Project.Data.Items
{
    [Serializable]
    public struct ItemShapeCell
    {
        public Vector2Int Position; // 점유 셀 좌표

        /// <summary>점유 셀 좌표 생성</summary>
        public ItemShapeCell(Vector2Int position)
        {
            Position = position;
        }
    }
}
