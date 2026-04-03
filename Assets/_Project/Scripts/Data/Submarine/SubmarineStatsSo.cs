using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>잠수함 스탯(런타임과 분리)</summary>
    [CreateAssetMenu(
        fileName = "SubmarineStats_",
        menuName = "Project/Submarine/Submarine Stats")]
    public class SubmarineStatsSO : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 5f; // 기본 이동 속도
        [SerializeField] private float baseTurnSpeed = 90f; // 기본 선회 속도

        [Header("Capacity")]
        [SerializeField] private SubmarineInventoryLayoutSO inventoryLayout; // 인벤토리 레이아웃 데이터
        [SerializeField] private int inventoryWidth = 6; // 인벤토리 가로 칸 수
        [SerializeField] private int inventoryHeight = 8; // 인벤토리 세로 칸 수

        [Header("Hull")]
        [SerializeField] private float maxHullDurability = 100f; // 최대 선체 내구도

        [Header("Battery")]
        [SerializeField] private float maxBattery = 100f; // 최대 배터리 용량

        public float BaseMoveSpeed => baseMoveSpeed;
        public float BaseTurnSpeed => baseTurnSpeed;
        public SubmarineInventoryLayoutSO InventoryLayout => inventoryLayout;
        public int InventoryWidth => inventoryWidth;
        public int InventoryHeight => inventoryHeight;
        public float MaxHullDurability => maxHullDurability;
        public float MaxBattery => maxBattery;

        public float GetMoveSpeed() => baseMoveSpeed;
        public float GetTurnSpeed() => baseTurnSpeed;
    }
}
