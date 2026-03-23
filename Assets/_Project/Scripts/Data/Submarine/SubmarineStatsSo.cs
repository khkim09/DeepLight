using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>잠수함 스탯(런타임과 분리)</summary>
    [CreateAssetMenu(fileName = "SubmarineStats_", menuName = "Project/Data/Submarine Stats")]
    public class SubmarineStatsSO : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 5f; // 기본 이동 속도
        [SerializeField] private float baseTurnSpeed = 90f; // 기본 선회 속도

        [Header("Capacity")]
        [SerializeField] private SubmarineInventoryLayoutSO inventoryLayout; // 인벤토리 레이아웃 데이터
        [SerializeField] private int inventoryWidth = 8; // 인벤토리 가로 칸 수
        [SerializeField] private int inventoryHeight = 6; // 인벤토리 세로 칸 수
        [SerializeField] private float maxCargoWeight = 100f; // 최대 적재 중량

        [Header("Hull")]
        [SerializeField] private float maxHullDurability = 100f; // 최대 선체 내구도

        [Header("Battery")]
        [SerializeField] private float maxBattery = 100f; // 최대 배터리 용량

        [Header("Optional Weight Penalty")]
        [SerializeField] private bool useWeightPenalty = true; // 무게 페널티 사용 여부
        [SerializeField] private float moveSpeedPenaltyPerWeight = 0.01f; // 무게당 이동 속도 감소량
        [SerializeField] private float turnSpeedPenaltyPerWeight = 0.02f; // 무게당 선회 속도 감소량

        public float BaseMoveSpeed => baseMoveSpeed;
        public float BaseTurnSpeed => baseTurnSpeed;
        public SubmarineInventoryLayoutSO InventoryLayout => inventoryLayout;
        public int InventoryWidth => inventoryWidth;
        public int InventoryHeight => inventoryHeight;
        public float MaxCargoWeight => maxCargoWeight;
        public float MaxHullDurability => maxHullDurability;
        public float MaxBattery => maxBattery;
        public bool UseWeightPenalty => useWeightPenalty;
        public float MoveSpeedPenaltyPerWeight => moveSpeedPenaltyPerWeight;
        public float TurnSpeedPenaltyPerWeight => turnSpeedPenaltyPerWeight;

        /// <summary>현재 적재 무게 기준 이동 속도 계산</summary>
        public float GetMoveSpeed(float currentCargoWeight)
        {
            if (!useWeightPenalty)
                return baseMoveSpeed;

            float reducedSpeed = baseMoveSpeed - (currentCargoWeight * moveSpeedPenaltyPerWeight);
            return Mathf.Max(0.1f, reducedSpeed);
        }

        /// <summary>현재 적재 무게 기준 선회 속도 계산</summary>
        public float GetTurnSpeed(float currentCargoWeight)
        {
            if (!useWeightPenalty)
                return baseTurnSpeed;

            float reducedSpeed = baseTurnSpeed - (currentCargoWeight * turnSpeedPenaltyPerWeight);
            return Mathf.Max(1f, reducedSpeed);
        }
    }
}
