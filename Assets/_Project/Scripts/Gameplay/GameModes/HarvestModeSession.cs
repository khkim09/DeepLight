using System.Collections.Generic;
using Project.Data.Enums;
using Project.Gameplay.Harvest;

namespace Project.Gameplay.GameModes
{
    /// <summary>현재 채집 세션의 대상, 스캔, 선택 상태를 보관하는 클래스</summary>
    public class HarvestModeSession
    {
        private IHarvestTarget currentTarget; // 현재 채집 대상
        private HarvestScanMode currentScanMode; // 현재 센서 모드
        private readonly List<string> revealedPointIds = new List<string>(); // 밝혀진 포인트 ID
        private readonly List<string> selectedPointSequence = new List<string>(); // 선택된 포인트 순서
        private float estimatedRecoveryChance; // 추정 회수 성공률
        private float estimatedBatteryCost; // 추정 배터리 소모량
        private float estimatedDurabilityCost; // 추정 내구도 소모량
        private int scanPulseCount; // 현재 세션에서 사용한 스캔 횟수
        private bool isCommitted; // 회수 계획 확정 여부
        private bool isResolved; // 회수 완료 여부

        public IHarvestTarget CurrentTarget => currentTarget;
        public HarvestScanMode CurrentScanMode => currentScanMode;
        public IReadOnlyList<string> RevealedPointIds => revealedPointIds;
        public IReadOnlyList<string> SelectedPointSequence => selectedPointSequence;
        public float EstimatedRecoveryChance => estimatedRecoveryChance;
        public float EstimatedBatteryCost => estimatedBatteryCost;
        public float EstimatedDurabilityCost => estimatedDurabilityCost;
        public int ScanPulseCount => scanPulseCount;
        public bool IsCommitted => isCommitted;
        public bool IsResolved => isResolved;
        public bool HasTarget => currentTarget != null;

        /// <summary>채집 대상 설정과 세션 초기화를 수행한다</summary>
        public void SetTarget(IHarvestTarget target)
        {
            currentTarget = target; // 대상 저장
            ResetConsoleState(); // 콘솔 상태 초기화
        }

        /// <summary>현재 센서 모드를 변경한다</summary>
        public void SetScanMode(HarvestScanMode scanMode)
        {
            currentScanMode = scanMode; // 센서 모드 저장
        }

        /// <summary>스캔 펄스 사용 횟수를 증가시킨다</summary>
        public void AddScanPulse()
        {
            scanPulseCount++; // 사용 횟수 증가
        }

        /// <summary>포인트를 공개 상태에 추가한다</summary>
        public void AddRevealedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            if (revealedPointIds.Contains(pointId))
                return;

            revealedPointIds.Add(pointId);
        }

        /// <summary>포인트를 선택 순서에 추가한다</summary>
        public void AddSelectedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            selectedPointSequence.Add(pointId);
        }

        /// <summary>선택 포인트 순서를 비운다</summary>
        public void ClearSelectedPoints()
        {
            selectedPointSequence.Clear();
            isCommitted = false;
        }

        /// <summary>추정 회수 수치를 갱신한다</summary>
        public void SetEstimatedOutcome(float recoveryChance, float batteryCost, float durabilityCost)
        {
            estimatedRecoveryChance = recoveryChance;
            estimatedBatteryCost = batteryCost;
            estimatedDurabilityCost = durabilityCost;
        }

        /// <summary>현재 회수 계획을 확정 상태로 전환한다</summary>
        public void CommitRecoveryPlan()
        {
            isCommitted = true;
        }

        /// <summary>회수 결과가 확정되었음을 표시한다</summary>
        public void MarkResolved()
        {
            isResolved = true;
        }

        /// <summary>채집 세션 전체를 정리한다</summary>
        public void ClearTarget()
        {
            currentTarget = null;
            ResetConsoleState();
        }

        /// <summary>회수 콘솔 상태를 초기화한다</summary>
        private void ResetConsoleState()
        {
            currentScanMode = HarvestScanMode.None;
            revealedPointIds.Clear();
            selectedPointSequence.Clear();
            estimatedRecoveryChance = 0f;
            estimatedBatteryCost = 0f;
            estimatedDurabilityCost = 0f;
            scanPulseCount = 0;
            isCommitted = false;
            isResolved = false;
        }
    }
}
