using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>회수 성공률, 배터리 비용, 내구도 비용 계산 수치를 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestRecoveryTuningSO",
        menuName = "Project/Harvest/Harvest Recovery Tuning")]
    public class HarvestRecoveryTuningSO : ScriptableObject
    {
        [Header("Base Chance")]
        [SerializeField] private float baseRecoveryChance = 0.55f;          // 회수 기본 성공률
        [SerializeField] private float difficultyPenaltyMultiplier = 0.55f; // 난이도 패널티 배율
        [SerializeField] private float batteryBonusAtEmpty = -0.1f;         // 배터리 0% 보너스
        [SerializeField] private float batteryBonusAtFull = 0.1f;           // 배터리 100% 보너스

        [Header("Scan Cost")]
        [SerializeField] private float sonarPulseBatteryCost = 3f; // 소나 스캔 1회 배터리 비용
        [SerializeField] private float lidarPulseBatteryCost = 5f; // 라이다 스캔 1회 배터리 비용

        [Header("Preview Bonus")]
        [SerializeField] private float revealBonusPerPoint = 0.05f;      // 공개 포인트 1개당 보너스
        [SerializeField] private float maxRevealBonus = 0.2f;            // 공개 보너스 최대치
        [SerializeField] private float firstAnchorBiasMultiplier = 0.12f; // 첫 포인트 적합도 배율
        [SerializeField] private float sequenceBiasMultiplier = 0.08f;    // 후속 포인트 적합도 배율
        [SerializeField] private float baseStabilityMultiplier = 0.08f;   // 기본 안정성 배율
        [SerializeField] private float riskPenaltyMultiplier = 0.1f;      // 위험도 패널티 배율

        [Header("Estimated Cost")]
        [SerializeField] private float selectedPointBatteryCost = 1f;          // 선택 포인트 1개당 추가 배터리 비용
        [SerializeField] private float selectedPointDurabilityCost = 0.5f;     // 선택 포인트 1개당 추가 내구도 비용
        [SerializeField] private float riskPenaltyToDurabilityMultiplier = 3f; // 위험도를 내구도 비용으로 환산하는 배율

        /// <summary>회수 기본 성공률을 반환한다.</summary>
        public float BaseRecoveryChance => Mathf.Clamp01(baseRecoveryChance);

        /// <summary>난이도 패널티 배율을 반환한다.</summary>
        public float DifficultyPenaltyMultiplier => Mathf.Max(0f, difficultyPenaltyMultiplier);

        /// <summary>배터리 0% 기준 보너스를 반환한다.</summary>
        public float BatteryBonusAtEmpty => batteryBonusAtEmpty;

        /// <summary>배터리 100% 기준 보너스를 반환한다.</summary>
        public float BatteryBonusAtFull => batteryBonusAtFull;

        /// <summary>소나 스캔 1회 비용을 반환한다.</summary>
        public float SonarPulseBatteryCost => Mathf.Max(0f, sonarPulseBatteryCost);

        /// <summary>라이다 스캔 1회 비용을 반환한다.</summary>
        public float LidarPulseBatteryCost => Mathf.Max(0f, lidarPulseBatteryCost);

        /// <summary>공개 포인트 1개당 보너스를 반환한다.</summary>
        public float RevealBonusPerPoint => Mathf.Max(0f, revealBonusPerPoint);

        /// <summary>공개 보너스 최대치를 반환한다.</summary>
        public float MaxRevealBonus => Mathf.Max(0f, maxRevealBonus);

        /// <summary>첫 포인트 적합도 배율을 반환한다.</summary>
        public float FirstAnchorBiasMultiplier => Mathf.Max(0f, firstAnchorBiasMultiplier);

        /// <summary>후속 포인트 적합도 배율을 반환한다.</summary>
        public float SequenceBiasMultiplier => Mathf.Max(0f, sequenceBiasMultiplier);

        /// <summary>기본 안정성 배율을 반환한다.</summary>
        public float BaseStabilityMultiplier => Mathf.Max(0f, baseStabilityMultiplier);

        /// <summary>위험도 패널티 배율을 반환한다.</summary>
        public float RiskPenaltyMultiplier => Mathf.Max(0f, riskPenaltyMultiplier);

        /// <summary>선택 포인트 1개당 배터리 비용을 반환한다.</summary>
        public float SelectedPointBatteryCost => Mathf.Max(0f, selectedPointBatteryCost);

        /// <summary>선택 포인트 1개당 내구도 비용을 반환한다.</summary>
        public float SelectedPointDurabilityCost => Mathf.Max(0f, selectedPointDurabilityCost);

        /// <summary>위험도-내구도 환산 배율을 반환한다.</summary>
        public float RiskPenaltyToDurabilityMultiplier => Mathf.Max(0f, riskPenaltyToDurabilityMultiplier);
    }
}
