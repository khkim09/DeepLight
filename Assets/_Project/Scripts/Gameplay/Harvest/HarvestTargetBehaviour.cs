using Project.Data.Harvest;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>맵 위 채집 대상 오브젝트를 담당하는 컴포넌트 클래스</summary>
    public class HarvestTargetBehaviour : MonoBehaviour, IHarvestTarget
    {
        [SerializeField] private HarvestTargetSO targetData; // 채집 포인트 데이터
        [SerializeField] private bool isAvailable = true; // 현재 채집 가능 여부
        [SerializeField] private LayerMask defaultLayerMask; // 기본 타겟 레이어 마스크
        [SerializeField] private LayerMask harvestingLayerMask; // 채집 중 레이어 마스크

        private int defaultLayer = -1; // 기본 레이어 인덱스
        private int harvestingLayer = -1; // 채집 중 레이어 인덱스

        public HarvestTargetSO TargetData => targetData;
        public bool IsAvailable => isAvailable;

        /// <summary>레이어 인덱스를 캐싱한다</summary>
        private void Awake()
        {
            // 기본 레이어 캐싱
            defaultLayer = GetSingleLayerIndex(defaultLayerMask);

            // 채집 중 레이어 캐싱
            harvestingLayer = GetSingleLayerIndex(harvestingLayerMask);

            // 기본 레이어 적용
            RestoreDefaultLayer();
        }

        /// <summary>비활성화 시 기본 레이어로 복귀한다</summary>
        private void OnDisable()
        {
            // 풀링/비활성화 대비 기본 레이어 복귀
            RestoreDefaultLayer();
        }

        /// <summary>채집 성공 후 대상 상태를 갱신한다</summary>
        public void Consume()
        {
            // 이미 사용 불가면 중단
            if (!isAvailable)
                return;

            // 사용 불가 처리
            isAvailable = false;

            // 기본 레이어 복귀
            RestoreDefaultLayer();

            // 성공 시 소멸 옵션이면 비활성화
            if (targetData != null && targetData.ConsumeOnSuccess)
                gameObject.SetActive(false);
        }

        /// <summary>장애물 충돌 등의 반응을 처리한다</summary>
        public void OnClawCollision()
        {
            // 현재는 기본 반응 없음
        }

        /// <summary>채집 중 레이어를 적용한다</summary>
        public void ApplyHarvestingLayer()
        {
            // 유효 레이어 없으면 중단
            if (harvestingLayer < 0)
                return;

            // 하위 전체에 레이어 적용
            ApplyLayerRecursively(transform, harvestingLayer);
        }

        /// <summary>기본 타겟 레이어를 복구한다</summary>
        public void RestoreDefaultLayer()
        {
            // 유효 레이어 없으면 중단
            if (defaultLayer < 0)
                return;

            // 하위 전체에 레이어 적용
            ApplyLayerRecursively(transform, defaultLayer);
        }

        /// <summary>풀 복귀 시 초기 상태를 재설정한다</summary>
        public void ResetForReuse()
        {
            // 채집 가능 상태 복구
            isAvailable = true;

            // 기본 레이어 복구
            RestoreDefaultLayer();
        }

        /// <summary>단일 레이어 마스크에서 실제 레이어 인덱스를 추출한다</summary>
        private int GetSingleLayerIndex(LayerMask layerMask)
        {
            // 비어 있으면 실패
            if (layerMask.value == 0)
                return -1;

            int value = layerMask.value; // 원본 값 캐싱
            int index = 0; // 레이어 인덱스 계산용

            // 첫 번째 활성 비트 탐색
            while (value > 1)
            {
                value >>= 1;
                index++;
            }

            return index;
        }

        /// <summary>하위 포함 전체 레이어를 재귀 적용한다</summary>
        private void ApplyLayerRecursively(Transform root, int layer)
        {
            // 현재 오브젝트 레이어 적용
            root.gameObject.layer = layer;

            // 자식 순회
            for (int i = 0; i < root.childCount; i++)
                ApplyLayerRecursively(root.GetChild(i), layer);
        }
    }
}
