using System.Collections.Generic;
using Project.Data.Enums;
using UnityEngine;

namespace Project.Data.Items
{
    /// <summary>아이템의 정적 데이터를 정의하는 ScriptableObject이다.</summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "Project/Item/Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemId; // 고유 ID
        [SerializeField] private string displayName; // 표시 이름
        [SerializeField] private string description; // 설명

        [Header("Visual")]
        [SerializeField] private Sprite icon; // 아이콘
        [SerializeField] private Sprite typeIcon; // 유형 아이콘

        [Header("Category")]
        [SerializeField] private ItemCategoryType category; // 아이템 분류
        [SerializeField] private ItemRarityType rarity; // 희귀도

        [Header("Relic Settings")]
        [Tooltip("유물일 경우 분석에 소요되는 인게임 시간(시간 단위)")]
        [SerializeField] private float analysisTimeHours = 24f; // 유물 분석 소요 시간

        [TextArea]
        [SerializeField] private string analysisTeaserMessage; // 유물 분석 떡밥 메시지

        [Header("Retry Penalty")]
        [Tooltip("true면 아래 custom 값을 그대로 사용하고, false면 카테고리/희귀도 기본 규칙을 사용한다.")]
        [SerializeField] private bool useCustomRetryPenaltyHours; // 커스텀 패널티 사용 여부

        [Tooltip("재시도 제한 시간(인게임 시간 기준, 시간 단위)")]
        [Min(0f)]
        [SerializeField] private float customRetryPenaltyHours; // 커스텀 재시도 패널티 시간

        [Header("Value")]
        [SerializeField] private int sellPrice; // 판매 가격
        [SerializeField] private float baseCatchDifficulty; // 기본 채집 난이도

        [Header("Inventory Shape")]
        [SerializeField] private Vector2Int shapeBounds = Vector2Int.one; // 아이템 외곽 크기
        [SerializeField] private List<Vector2Int> occupiedCells = new(); // 점유 셀 로컬 좌표 목록
        [SerializeField] private bool canRotate; // 회전 가능 여부

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Sprite TypeIcon => typeIcon;
        public ItemCategoryType Category => category;
        public ItemRarityType Rarity => rarity;
        public float AnalysisTimeHours => analysisTimeHours;
        public string AnalysisTeaserMessage => analysisTeaserMessage;
        public bool UseCustomRetryPenaltyHours => useCustomRetryPenaltyHours;
        public float CustomRetryPenaltyHours => customRetryPenaltyHours;
        public int SellPrice => sellPrice;
        public float BaseCatchDifficulty => baseCatchDifficulty;
        public Vector2Int ShapeBounds => shapeBounds;
        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;
        public bool CanRotate => canRotate;

        /// <summary>아이템 총 점유 칸 수를 반환한다.</summary>
        public int GetOccupiedCellCount() => occupiedCells.Count;

        /// <summary>재시도 패널티 시간을 반환한다.</summary>
        public float GetRetryPenaltyHours()
        {
            // 개별 튜닝이 필요하면 SO에서 직접 덮어쓴다.
            if (useCustomRetryPenaltyHours)
                return Mathf.Max(0f, customRetryPenaltyHours);

            // 자원은 우선 0시간
            if (category == ItemCategoryType.Resource)
                return 0f;

            // 유물 기본 1시간 + 희귀도 보정
            if (category == ItemCategoryType.Relic)
                return 1f + GetRarityBonusRetryPenaltyHours(rarity);

            return 0f;
        }

        /// <summary>희귀도별 추가 재시도 패널티 시간을 반환한다.</summary>
        private float GetRarityBonusRetryPenaltyHours(ItemRarityType itemRarity)
        {
            return itemRarity switch
            {
                ItemRarityType.Common => 0f,
                ItemRarityType.Uncommon => 0.25f,
                ItemRarityType.Rare => 0.5f,
                ItemRarityType.Epic => 1f,
                ItemRarityType.Legendary => 2f,
                _ => 0f
            };
        }

        /// <summary>아이템 데이터 유효성을 검사한다.</summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (shapeBounds.x <= 0 || shapeBounds.y <= 0) return false;
            if (occupiedCells.Count <= 0) return false;

            return true;
        }
    }
}
