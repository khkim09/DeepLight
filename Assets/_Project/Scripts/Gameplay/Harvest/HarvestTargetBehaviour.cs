using System.Collections.Generic;
using System.Linq;
using Project.Data.Harvest;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>맵 위 채집 대상 오브젝트와 회수 포인트를 담당하는 컴포넌트 클래스</summary>
    public class HarvestTargetBehaviour : MonoBehaviour, IHarvestTarget
    {
        [SerializeField] private HarvestTargetSO targetData; // 채집 대상 데이터
        [SerializeField] private bool isAvailable = true; // 현재 채집 가능 여부
        [SerializeField] private HarvestScanPoint[] scanPoints; // 대상 하위 회수 포인트 목록

        public HarvestTargetSO TargetData => targetData;
        public bool IsAvailable => isAvailable;

        /// <summary>하위 회수 포인트를 자동 캐싱한다</summary>
        private void Awake()
        {
            if (scanPoints == null || scanPoints.Length == 0)
                scanPoints = GetComponentsInChildren<HarvestScanPoint>(true);

            ResetScanPoints();
        }

        /// <summary>비활성화 시 회수 포인트 상태를 초기화한다</summary>
        private void OnDisable()
        {
            ResetScanPoints();
        }

        /// <summary>채집 성공 후 대상 상태를 갱신한다</summary>
        public void Consume()
        {
            if (!isAvailable)
                return;

            isAvailable = false; // 사용 불가 처리
            ResetScanPoints(); // 포인트 상태 초기화

            if (targetData != null && targetData.ConsumeOnSuccess)
                gameObject.SetActive(false); // 성공 시 소멸 옵션
        }

        /// <summary>레거시 충돌 반응을 처리한다</summary>
        public void OnClawCollision()
        {
            // 로봇팔 구조 폐기 후 현재는 기본 반응 없음
        }

        /// <summary>회수 포인트를 표시 순서 기준으로 반환한다</summary>
        public IReadOnlyList<HarvestScanPoint> GetScanPoints()
        {
            return scanPoints
                .Where(point => point != null)
                .OrderBy(point => point.DisplayOrder)
                .ToArray();
        }

        /// <summary>모든 회수 포인트 런타임 상태를 초기화한다</summary>
        public void ResetScanPoints()
        {
            if (scanPoints == null)
                return;

            foreach (HarvestScanPoint scanPoint in scanPoints)
            {
                if (scanPoint == null)
                    continue;

                scanPoint.ResetRuntimeState();
            }
        }
    }
}
