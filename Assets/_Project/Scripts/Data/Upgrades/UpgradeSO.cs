using Project.Data.Enums;
using UnityEngine;

namespace Project.Data.Upgrades
{
    /// <summary>잠수함, 로봇 팔 업그레이드</summary>
    [CreateAssetMenu(fileName = "Upgrade_", menuName = "Project/Data/Upgrade")]
    public class UpgradeSO : ScriptableObject
    {
        [SerializeField] private string upgradeId; // 업그레이드 고유 ID
        [SerializeField] private string displayName; // 업그레이드 이름
        [SerializeField] private string description; // 업그레이드 설명
        [SerializeField] private UpgradeTargetType targetType; // 업그레이드 대상
        [SerializeField] private int cost; // 구매 비용
        [SerializeField] private float value; // 적용 수치
        [SerializeField] private int maxLevel = 1; // 최대 레벨

        public string UpgradeId => upgradeId;
        public string DisplayName => displayName;
        public string Description => description;
        public UpgradeTargetType TargetType => targetType;
        public int Cost => cost;
        public float Value => value;
        public int MaxLevel => maxLevel;

        /// <summary>업그레이드 데이터 유효성 검사</summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
                return false;

            if (cost < 0)
                return false;

            if (maxLevel <= 0)
                return false;

            return true;
        }
    }
}
