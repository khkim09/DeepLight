using System.Collections.Generic;
using Project.Data.Enums;
using Project.Gameplay.Harvest;

namespace Project.Gameplay.GameModes
{
    /// <summary>채집 세션의 대상, 스캔, 순서 슬롯 상태를 보관한다.</summary>
    public class HarvestModeSession
    {
        private IHarvestTarget currentTarget; // 현재 채집 대상
        private HarvestScanMode currentScanMode; // 현재 센서 모드
        private readonly List<string> revealedPointIds = new(); // 공개된 포인트 ID 목록
        private readonly List<string> selectedPointSequence = new(); // 순서 슬롯 목록 (빈 슬롯은 string.Empty)
        private float estimatedRecoveryChance; // 추정 회수 성공률
        private float estimatedBatteryCost; // 추정 배터리 소모량
        private float estimatedDurabilityCost; // 추정 내구도 소모량
        private int scanPulseCount; // 누적 스캔 횟수
        private bool isResolved; // 회수 완료 여부

        public IHarvestTarget CurrentTarget => currentTarget;
        public HarvestScanMode CurrentScanMode => currentScanMode;
        public IReadOnlyList<string> RevealedPointIds => revealedPointIds;
        public IReadOnlyList<string> SelectedPointSequence => selectedPointSequence;
        public float EstimatedRecoveryChance => estimatedRecoveryChance;
        public float EstimatedBatteryCost => estimatedBatteryCost;
        public float EstimatedDurabilityCost => estimatedDurabilityCost;
        public int ScanPulseCount => scanPulseCount;
        public bool IsResolved => isResolved;
        public bool HasTarget => currentTarget != null;

        /// <summary>채집 대상 설정과 세션 초기화를 수행한다.</summary>
        public void SetTarget(IHarvestTarget target)
        {
            currentTarget = target; // 현재 대상 보관
            ResetConsoleState(); // 세션 상태 초기화
        }

        /// <summary>현재 센서 모드를 변경한다.</summary>
        public void SetScanMode(HarvestScanMode scanMode)
        {
            currentScanMode = scanMode; // 센서 모드 저장
        }

        /// <summary>스캔 펄스 사용 횟수를 증가시킨다.</summary>
        public void AddScanPulse()
        {
            scanPulseCount++; // 사용 횟수 증가
        }

        /// <summary>포인트를 공개 상태 목록에 추가한다.</summary>
        public void AddRevealedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            if (revealedPointIds.Contains(pointId))
                return;

            revealedPointIds.Add(pointId);
        }

        /// <summary>현재 포인트가 순서 슬롯에 포함되어 있는지 반환한다.</summary>
        public bool ContainsSelectedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return false;

            for (int i = 0; i < selectedPointSequence.Count; i++)
            {
                if (selectedPointSequence[i] == pointId)
                    return true;
            }

            return false;
        }

        /// <summary>현재 순서 슬롯 개수를 지정한다.</summary>
        public void EnsureSequenceCapacity(int count)
        {
            if (count <= 0)
                return;

            // 필요한 개수만큼 빈 슬롯을 뒤에 추가한다.
            while (selectedPointSequence.Count < count)
                selectedPointSequence.Add(string.Empty);

            // 초과 슬롯은 뒤에서 제거한다.
            while (selectedPointSequence.Count > count)
                selectedPointSequence.RemoveAt(selectedPointSequence.Count - 1);
        }

        /// <summary>가장 앞의 빈 슬롯에 포인트를 배치한다.</summary>
        public bool TryAssignSelectedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return false;

            if (ContainsSelectedPoint(pointId))
                return false;

            // 가장 앞의 빈 슬롯을 찾아 채운다.
            for (int i = 0; i < selectedPointSequence.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(selectedPointSequence[i]))
                    continue;

                selectedPointSequence[i] = pointId;
                return true;
            }

            return false;
        }

        /// <summary>특정 포인트를 순서 슬롯에서 제거한다.</summary>
        public bool RemoveSelectedPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return false;

            for (int i = 0; i < selectedPointSequence.Count; i++)
            {
                if (selectedPointSequence[i] != pointId)
                    continue;

                // 중간 값만 비우고 뒤 순서는 유지한다.
                selectedPointSequence[i] = string.Empty;
                return true;
            }

            return false;
        }

        /// <summary>특정 포인트의 현재 순번(1-base)을 반환한다.</summary>
        public int GetAssignedOrder(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return 0;

            for (int i = 0; i < selectedPointSequence.Count; i++)
            {
                if (selectedPointSequence[i] == pointId)
                    return i + 1;
            }

            return 0;
        }

        /// <summary>현재 실제로 배치된 포인트 개수를 반환한다.</summary>
        public int GetAssignedPointCount()
        {
            int count = 0;

            // 빈 슬롯이 아닌 값만 실제 선택 개수로 계산한다.
            for (int i = 0; i < selectedPointSequence.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(selectedPointSequence[i]))
                    count++;
            }

            return count;
        }

        /// <summary>순서 슬롯을 전부 비운다.</summary>
        public void ClearSequence()
        {
            for (int i = 0; i < selectedPointSequence.Count; i++)
                selectedPointSequence[i] = string.Empty;
        }

        /// <summary>추정 회수 수치를 갱신한다.</summary>
        public void SetEstimatedOutcome(float recoveryChance, float batteryCost, float durabilityCost)
        {
            estimatedRecoveryChance = recoveryChance;
            estimatedBatteryCost = batteryCost;
            estimatedDurabilityCost = durabilityCost;
        }

        /// <summary>회수 결과가 확정되었음을 표시한다.</summary>
        public void MarkResolved()
        {
            isResolved = true;
        }

        /// <summary>채집 세션 전체를 정리한다.</summary>
        public void ClearTarget()
        {
            currentTarget = null;
            ResetConsoleState();
        }

        /// <summary>회수 콘솔 상태를 초기화한다.</summary>
        private void ResetConsoleState()
        {
            currentScanMode = HarvestScanMode.None;
            revealedPointIds.Clear();
            selectedPointSequence.Clear();
            estimatedRecoveryChance = 0f;
            estimatedBatteryCost = 0f;
            estimatedDurabilityCost = 0f;
            scanPulseCount = 0;
            isResolved = false;
        }
    }
}
