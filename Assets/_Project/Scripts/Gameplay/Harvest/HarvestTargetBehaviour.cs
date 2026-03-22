using Project.Data.Harvest;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>맵 위 채집 대상</summary>
    public class HarvestTargetBehaviour : MonoBehaviour, IHarvestTarget
    {
        [SerializeField] private HarvestTargetSO targetData; // 채집 포인트 데이터
        [SerializeField] private bool isAvailable = true; // 현재 채집 가능 여부

        public HarvestTargetSO TargetData => targetData;
        public bool IsAvailable => isAvailable;

        /// <summary>채집 성공 후 대상 상태를 갱신</summary>
        public void Consume()
        {
            // 이미 사용 불가면 중단
            if (!isAvailable) return;

            // 사용 불가 처리
            isAvailable = false;

            // 성공 시 소멸 옵션이면 오브젝트 비활성화
            if (targetData != null && targetData.ConsumeOnSuccess)
                gameObject.SetActive(false);
        }

        /// <summary>장애물 충돌 등의 반응을 처리</summary>
        public void OnClawCollision()
        {
            // 현재는 기본 반응 없음
            // 추후 피격 이펙트, 애니메이션, SFX 연결 가능
        }
    }
}
