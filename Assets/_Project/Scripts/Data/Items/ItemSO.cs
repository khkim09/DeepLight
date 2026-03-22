using System.Collections.Generic;
using Project.Data.Enums;
using UnityEngine;

namespace Project.Data.Items
{
    /// <summary>정적 정의</summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "Project/Data/Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemId; // 아이템 고유 ID
        [SerializeField] private string displayName; // 아이템 표시 이름
        [SerializeField] private string description; // 아이템 설명

        [Header("Visual")]
        [SerializeField] private Sprite icon; // 인벤토리 아이콘
        [SerializeField] private GameObject worldPrefab; // 월드 프리팹

        [Header("Category")]
        [SerializeField] private ItemCategoryType category; // 아이템 분류
        [SerializeField] private ItemRarityType rarity; // 희귀도

        [Header("Value")]
        [SerializeField] private int sellPrice; // 판매 가격
        [SerializeField] private float weight; // 아이템 무게
        [SerializeField] private float baseCatchDifficulty; // 기본 채집 난이도

        [Header("Inventory Shape")]
        [SerializeField] private Vector2Int shapeBounds = Vector2Int.one; // 아이템 외곽 크기
        [SerializeField] private List<ItemShapeCell> occupiedCells = new(); // 점유 셀 목록
        [SerializeField] private bool canRotate; // 회전 가능 여부

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public ItemCategoryType Category => category;
        public ItemRarityType Rarity => rarity;
        public int SellPrice => sellPrice;
        public float Weight => weight;
        public float BaseCatchDifficulty => baseCatchDifficulty;
        public Vector2Int ShapeBounds => shapeBounds;
        public IReadOnlyList<ItemShapeCell> OccupiedCells => occupiedCells;
        public bool CanRotate => canRotate;

        /// <summary>아이템 총 점유 칸 수 반환</summary>
        public int GetOccupiedCellCount() => occupiedCells.Count;

        /// <summary>좌표가 점유 셀인지 확인</summary>
        public bool IsOccupiedCell(Vector2Int localPosition)
        {
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                if (occupiedCells[i].Position == localPosition)
                    return true;
            }

            return false;
        }

        /// <summary>런타임용 점유 그리드 생성</summary>
        public bool[,] BuildShapeGrid()
        {
            bool[,] grid = new bool[shapeBounds.x, shapeBounds.y];

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int position = occupiedCells[i].Position;

                if (position.x < 0 || position.x >= shapeBounds.x)
                    continue;

                if (position.y < 0 || position.y >= shapeBounds.y)
                    continue;

                grid[position.x, position.y] = true;
            }

            return grid;
        }

        /// <summary>아이템 데이터 유효성 검사</summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            if (shapeBounds.x <= 0 || shapeBounds.y <= 0)
                return false;

            if (occupiedCells.Count <= 0)
                return false;

            return true;
        }
    }
}
