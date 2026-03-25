using System.Collections.Generic;
using Project.Data.Enums;
using Project.Gameplay.Harvest;

namespace Project.Gameplay.GameModes
{
    /// <summary>현재 채집 세션의 대상, 스캔, 선택 상태를 보관하는 클래스</summary>
    public class HarvestModeSession
    {
        private IHarvestTarget currentTarget; // 현재 채집 대상

        // 신규 회수 콘솔 상태
        private HarvestScanMode currentScanMode; // 현재 센서 모드
        private readonly List<string> revealedPointIds = new List<string>(); // 밝혀진 포인트 ID 목록
        private readonly List<string> selectedPointSequence = new List<string>(); // 선택한 포인트 순서
        private float estimatedRecoveryChance; // 추정 회수 성공률
        private float estimatedBatteryCost; // 추정 배터리 소모량
        private float estimatedDurabilityCost; // 추정 내구도 소모량
        private bool isCommitted; // 회수 시퀀스 확정 여부
        private bool isResolved; // 회수 결과 확정 여부

        // 레거시 호환 필드
        private IHarvestTarget latchedTarget; // 레거시 포획 대상
        private bool isAttemptInProgress; // 레거시 시도 진행 여부
        private float cachedPreviewChance; // 레거시 사전 확률
        private float latchQuality01; // 레거시 조작 정확도

        public IHarvestTarget CurrentTarget => currentTarget;
        public HarvestScanMode CurrentScanMode => currentScanMode;
        public IReadOnlyList<string> RevealedPointIds => revealedPointIds;
        public IReadOnlyList<string> SelectedPointSequence => selectedPointSequence;
        public float EstimatedRecoveryChance => estimatedRecoveryChance;
        public float EstimatedBatteryCost => estimatedBatteryCost;
        public float EstimatedDurabilityCost => estimatedDurabilityCost;
        public bool IsCommitted => isCommitted;
        public bool IsResolved => isResolved;

        // 레거시 호환 프로퍼티
        public IHarvestTarget LatchedTarget => latchedTarget;
        public bool HasTarget => currentTarget != null;
        public bool HasLatchedTarget => latchedTarget != null;
        public bool IsAttemptInProgress => isAttemptInProgress;
        public float CachedPreviewChance => cachedPreviewChance;
        public float LatchQuality01 => latchQuality01;

        /// <summary>채집 대상 설정과 세션 초기화를 수행한다</summary>
        public void SetTarget(IHarvestTarget target)
        {
            currentTarget = target; // 대상 저장
            ResetConsoleState(); // 회수 콘솔 상태 초기화
            ResetLegacyAttemptState(); // 레거시 상태 초기화
        }

        /// <summary>현재 센서 모드를 변경한다</summary>
        public void SetScanMode(HarvestScanMode scanMode)
        {
            currentScanMode = scanMode; // 센서 모드 저장
        }

        /// <summary>밝혀진 포인트 ID를 추가한다</summary>
        public void AddRevealedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            if (revealedPointIds.Contains(pointId))
                return;

            revealedPointIds.Add(pointId);
        }

        /// <summary>선택한 포인트 순서를 추가한다</summary>
        public void AddSelectedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            selectedPointSequence.Add(pointId);
        }

        /// <summary>선택한 포인트 순서를 모두 비운다</summary>
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

        /// <summary>현재 회수 시퀀스를 확정 상태로 전환한다</summary>
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
            ResetLegacyAttemptState();
        }

        /// <summary>레거시 채집 시도를 시작한다</summary>
        public void BeginAttempt(float previewChance)
        {
            isAttemptInProgress = true;
            cachedPreviewChance = previewChance;
            latchedTarget = null;
            latchQuality01 = 0f;
        }

        /// <summary>레거시 포획 대상과 정확도를 기록한다</summary>
        public void SetLatchedTarget(IHarvestTarget target, float quality01)
        {
            latchedTarget = target;
            latchQuality01 = quality01;
        }

        /// <summary>레거시 포획 대상을 제거한다</summary>
        public void ClearLatchedTarget()
        {
            latchedTarget = null;
            latchQuality01 = 0f;
        }

        /// <summary>레거시 채집 시도를 종료한다</summary>
        public void CompleteAttempt()
        {
            isAttemptInProgress = false;
            cachedPreviewChance = 0f;
            latchedTarget = null;
            latchQuality01 = 0f;
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
            isCommitted = false;
            isResolved = false;
        }

        /// <summary>레거시 로봇팔 상태를 초기화한다</summary>
        private void ResetLegacyAttemptState()
        {
            latchedTarget = null;
            isAttemptInProgress = false;
            cachedPreviewChance = 0f;
            latchQuality01 = 0f;
        }
    }
}
