using Project.Data.Items;
using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>채집 포인트 정적 설정을 담당</summary>
    [CreateAssetMenu(
        fileName = "HarvestTarget_",
        menuName = "Project/Harvest/Harvest Target")]
    public class HarvestTargetSO : ScriptableObject
    {
        [SerializeField] private string targetId; // 채집 포인트 고유 ID
        [SerializeField] private ItemSO itemData; // 획득 아이템 데이터
        [SerializeField] private float additionalDifficulty; // 추가 난이도 보정값
        [SerializeField] private float collisionDamage = 10f; // 충돌 시 팔 피해량
        [SerializeField] private bool consumeOnSuccess = true; // 성공 시 포인트 소멸 여부

        public string TargetId => targetId;
        public ItemSO ItemData => itemData;
        public float AdditionalDifficulty => additionalDifficulty;
        public float CollisionDamage => collisionDamage;
        public bool ConsumeOnSuccess => consumeOnSuccess;

        /// <summary>채집 포인트 데이터 유효성 검사</summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(targetId))
                return false;

            if (itemData == null)
                return false;

            if (!itemData.IsValid())
                return false;

            return true;
        }
    }
}
