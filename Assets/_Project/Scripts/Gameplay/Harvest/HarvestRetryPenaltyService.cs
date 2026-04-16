using System.Collections.Generic;
using Project.Data.Items;
using Project.Gameplay.Services;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>타깃별 재시도 패널티 종료 시각을 관리한다.</summary>
    public class HarvestRetryPenaltyService
    {
        private readonly GameTimeService gameTimeService; // 인게임 시간 서비스
        private readonly Dictionary<string, RetryPenaltyEntry> penaltyByTargetKey = new(); // 타깃별 패널티 종료 데이터

        /// <summary>재시도 패널티 서비스를 생성한다.</summary>
        public HarvestRetryPenaltyService(GameTimeService newGameTimeService)
        {
            gameTimeService = newGameTimeService;
        }

        /// <summary>현재 타깃이 재시도 제한 중인지 반환한다.</summary>
        public bool IsRetryBlocked(IHarvestTarget harvestTarget)
        {
            return TryGetRemainingPenaltyHours(harvestTarget, out _);
        }

        /// <summary>현재 타깃의 남은 재시도 제한 시간을 반환한다.</summary>
        public bool TryGetRemainingPenaltyHours(IHarvestTarget harvestTarget, out float remainingHours)
        {
            remainingHours = 0f;

            string targetKey = BuildTargetKey(harvestTarget);
            if (string.IsNullOrWhiteSpace(targetKey))
                return false;

            // 현재 타깃 기준으로 만료 데이터 먼저 정리
            ClearExpiredPenalty(targetKey);

            if (!penaltyByTargetKey.TryGetValue(targetKey, out RetryPenaltyEntry entry))
                return false;

            float nowAbsoluteHours = GetCurrentAbsoluteHours();
            remainingHours = Mathf.Max(0f, entry.EndAbsoluteHours - nowAbsoluteHours);

            // 사실상 만료된 값이면 즉시 제거
            if (remainingHours <= 0.0001f)
            {
                penaltyByTargetKey.Remove(targetKey);
                remainingHours = 0f;
                return false;
            }

            return true;
        }

        /// <summary>실패한 타깃에 재시도 패널티를 시작한다.</summary>
        public void ApplyRetryPenalty(IHarvestTarget harvestTarget, ItemSO itemData)
        {
            if (harvestTarget == null || itemData == null)
                return;

            float penaltyHours = Mathf.Max(0f, itemData.GetRetryPenaltyHours());

            // 패널티가 0시간이면 기존 막힘도 제거
            if (penaltyHours <= 0f)
            {
                ClearPenalty(harvestTarget);
                return;
            }

            string targetKey = BuildTargetKey(harvestTarget);
            if (string.IsNullOrWhiteSpace(targetKey))
                return;

            float nowAbsoluteHours = GetCurrentAbsoluteHours();

            // 동일 타깃 재실패 시 더 긴 종료 시각만 유지
            float nextEndAbsoluteHours = nowAbsoluteHours + penaltyHours;
            if (penaltyByTargetKey.TryGetValue(targetKey, out RetryPenaltyEntry oldEntry))
                nextEndAbsoluteHours = Mathf.Max(oldEntry.EndAbsoluteHours, nextEndAbsoluteHours);

            penaltyByTargetKey[targetKey] = new RetryPenaltyEntry(
                targetKey,
                itemData.ItemId,
                nowAbsoluteHours,
                nextEndAbsoluteHours);
        }

        /// <summary>특정 타깃의 재시도 패널티를 제거한다.</summary>
        public void ClearPenalty(IHarvestTarget harvestTarget)
        {
            string targetKey = BuildTargetKey(harvestTarget);
            if (string.IsNullOrWhiteSpace(targetKey))
                return;

            penaltyByTargetKey.Remove(targetKey);
        }

        /// <summary>전체 패널티 중 만료된 항목을 정리한다.</summary>
        public void ClearAllExpiredPenalties()
        {
            if (penaltyByTargetKey.Count == 0)
                return;

            float nowAbsoluteHours = GetCurrentAbsoluteHours();
            List<string> expiredKeys = null;

            foreach (KeyValuePair<string, RetryPenaltyEntry> pair in penaltyByTargetKey)
            {
                if (pair.Value.EndAbsoluteHours > nowAbsoluteHours)
                    continue;

                expiredKeys ??= new List<string>();
                expiredKeys.Add(pair.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                penaltyByTargetKey.Remove(expiredKeys[i]);
        }

        /// <summary>현재 타깃의 남은 시간을 사용자 표시 문자열로 변환한다.</summary>
        public string GetRemainingPenaltyDisplayText(IHarvestTarget harvestTarget)
        {
            if (!TryGetRemainingPenaltyHours(harvestTarget, out float remainingHours))
                return string.Empty;

            // 1분 미만이어도 사용자에게는 최소 1분으로 보이게 보정
            int totalMinutes = Mathf.Max(1, Mathf.CeilToInt(remainingHours * 60f));
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0)
                return $"{hours}h {minutes}m";

            if (hours > 0)
                return $"{hours}h";

            return $"{minutes}m";
        }

        /// <summary>타깃에서 재시도 패널티용 고유 키를 생성한다.</summary>
        private string BuildTargetKey(IHarvestTarget harvestTarget)
        {
            if (harvestTarget == null)
                return string.Empty;

            if (harvestTarget is HarvestTargetBehaviour targetBehaviour)
                return targetBehaviour.GetRuntimeTargetKey();

            if (harvestTarget is Component component)
                return component.GetInstanceID().ToString();

            return harvestTarget.GetHashCode().ToString();
        }

        /// <summary>현재 절대 인게임 시간을 반환한다.</summary>
        private float GetCurrentAbsoluteHours()
        {
            return gameTimeService != null ? gameTimeService.CurrentAbsoluteHours : 0f;
        }

        /// <summary>특정 타깃의 만료된 패널티를 제거한다.</summary>
        private void ClearExpiredPenalty(string targetKey)
        {
            if (!penaltyByTargetKey.TryGetValue(targetKey, out RetryPenaltyEntry entry))
                return;

            float nowAbsoluteHours = GetCurrentAbsoluteHours();
            if (entry.EndAbsoluteHours <= nowAbsoluteHours)
                penaltyByTargetKey.Remove(targetKey);
        }

        /// <summary>타깃별 재시도 패널티 런타임 데이터를 보관한다.</summary>
        private readonly struct RetryPenaltyEntry
        {
            public readonly string TargetKey; // 대상 고유 키
            public readonly string ItemId; // 실패 당시 아이템 ID
            public readonly float StartAbsoluteHours; // 시작 절대 시간
            public readonly float EndAbsoluteHours; // 종료 절대 시간

            public RetryPenaltyEntry(
                string targetKey,
                string itemId,
                float startAbsoluteHours,
                float endAbsoluteHours)
            {
                TargetKey = targetKey;
                ItemId = itemId;
                StartAbsoluteHours = startAbsoluteHours;
                EndAbsoluteHours = endAbsoluteHours;
            }
        }
    }
}
