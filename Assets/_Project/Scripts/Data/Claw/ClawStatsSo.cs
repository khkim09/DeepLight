using UnityEngine;

namespace Project.Data.Claw
{
    /// <summary>로봇 팔 스탯</summary>
    [CreateAssetMenu(fileName = "ClawStats_", menuName = "Project/Data/Claw Stats")]
    public class ClawStatsSO : ScriptableObject
    {
        [Header("Weight")]
        [SerializeField] private float maxLiftWeight = 50f; // 최대 인양 가능 중량
        [SerializeField] private float overweightPenaltyMultiplier = 0.2f; // 초과 중량 시 확률 배수

        [Header("Battery")]
        [SerializeField] private float batteryCostPerDrop = 10f; // 1회 투하 배터리 소모량
        [SerializeField] private float minBatteryEfficiency = 0.25f; // 배터리 최저 효율

        [Header("Durability")]
        [SerializeField] private float maxDurability = 100f; // 최대 팔 내구도
        [SerializeField] private float collisionDamage = 10f; // 충돌 시 내구도 감소량
        [SerializeField] private float minDurabilityEfficiency = 0.25f; // 내구도 최저 효율

        [Header("Control")]
        [SerializeField] private float horizontalMoveSpeed = 5f; // 좌우 이동 속도
        [SerializeField] private float dropSpeed = 8f; // 하강 속도
        [SerializeField] private float returnSpeed = 10f; // 복귀 속도

        public float MaxLiftWeight => maxLiftWeight;
        public float OverweightPenaltyMultiplier => overweightPenaltyMultiplier;
        public float BatteryCostPerDrop => batteryCostPerDrop;
        public float MinBatteryEfficiency => minBatteryEfficiency;
        public float MaxDurability => maxDurability;
        public float CollisionDamage => collisionDamage;
        public float MinDurabilityEfficiency => minDurabilityEfficiency;
        public float HorizontalMoveSpeed => horizontalMoveSpeed;
        public float DropSpeed => dropSpeed;
        public float ReturnSpeed => returnSpeed;

        /// <summary>배터리 상태 효율 보정값 계산</summary>
        public float EvaluateBatteryEfficiency(float currentBattery, float maxBattery)
        {
            if (maxBattery <= 0f)
                return minBatteryEfficiency;

            float normalized = Mathf.Clamp01(currentBattery / maxBattery);
            return Mathf.Lerp(minBatteryEfficiency, 1f, normalized);
        }

        /// <summary>내구도 상태 효율 보정값 계산</summary>
        public float EvaluateDurabilityEfficiency(float currentDurability)
        {
            if (maxDurability <= 0f)
                return minDurabilityEfficiency;

            float normalized = Mathf.Clamp01(currentDurability / maxDurability);
            return Mathf.Lerp(minDurabilityEfficiency, 1f, normalized);
        }

        /// <summary>중량 기준 성공 확률 보정값 계산</summary>
        public float EvaluateWeightEfficiency(float itemWeight)
        {
            if (itemWeight <= maxLiftWeight)
                return 1f;

            return overweightPenaltyMultiplier;
        }

        /// <summary>최종 채집 성공 확률 계산</summary>
        public float EvaluateCatchChance(float itemWeight, float itemDifficulty, float currentBattery, float maxBattery, float currentDurability)
        {
            float batteryEfficiency = EvaluateBatteryEfficiency(currentBattery, maxBattery);
            float durabilityEfficiency = EvaluateDurabilityEfficiency(currentDurability);
            float weightEfficiency = EvaluateWeightEfficiency(itemWeight);

            float difficultyFactor = 1f - Mathf.Clamp01(itemDifficulty);
            float chance = batteryEfficiency * durabilityEfficiency * weightEfficiency * difficultyFactor;

            return Mathf.Clamp01(chance);
        }
    }
}
