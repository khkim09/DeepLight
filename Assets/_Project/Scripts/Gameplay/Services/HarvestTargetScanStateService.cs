using System.Collections.Generic;
using Project.Gameplay.Harvest;

namespace Project.Gameplay.Services
{
    /// <summary>타깃별 공개된 스캔 포인트 상태를 유지한다.</summary>
    public class HarvestTargetScanStateService
    {
        private readonly Dictionary<string, HashSet<string>> revealedPointIdsByTargetKey = new(); // 타깃 키별 공개 포인트 ID 집합

        /// <summary>포인트 공개 상태를 저장한다.</summary>
        public void MarkPointRevealed(IHarvestTarget harvestTarget, string pointId)
        {
            string targetKey = ResolveTargetKey(harvestTarget);
            if (string.IsNullOrWhiteSpace(targetKey) || string.IsNullOrWhiteSpace(pointId))
                return;

            if (!revealedPointIdsByTargetKey.TryGetValue(targetKey, out HashSet<string> revealedSet))
            {
                revealedSet = new HashSet<string>();
                revealedPointIdsByTargetKey[targetKey] = revealedSet;
            }

            revealedSet.Add(pointId);
        }

        /// <summary>타깃 키 기준으로 포인트 공개 상태를 저장한다.</summary>
        public void MarkPointRevealed(string targetKey, string pointId)
        {
            if (string.IsNullOrWhiteSpace(targetKey) || string.IsNullOrWhiteSpace(pointId))
                return;

            if (!revealedPointIdsByTargetKey.TryGetValue(targetKey, out HashSet<string> revealedSet))
            {
                revealedSet = new HashSet<string>();
                revealedPointIdsByTargetKey[targetKey] = revealedSet;
            }

            revealedSet.Add(pointId);
        }

        /// <summary>현재 타깃의 공개 포인트 ID 목록을 반환한다.</summary>
        public IReadOnlyCollection<string> GetRevealedPointIds(IHarvestTarget harvestTarget)
        {
            string targetKey = ResolveTargetKey(harvestTarget);
            return GetRevealedPointIds(targetKey);
        }

        /// <summary>타깃 키의 공개 포인트 ID 목록을 반환한다.</summary>
        public IReadOnlyCollection<string> GetRevealedPointIds(string targetKey)
        {
            if (string.IsNullOrWhiteSpace(targetKey))
                return System.Array.Empty<string>();

            if (!revealedPointIdsByTargetKey.TryGetValue(targetKey, out HashSet<string> revealedSet))
                return System.Array.Empty<string>();

            return revealedSet;
        }

        /// <summary>특정 포인트가 이미 공개되었는지 반환한다.</summary>
        public bool IsPointRevealed(IHarvestTarget harvestTarget, string pointId)
        {
            string targetKey = ResolveTargetKey(harvestTarget);
            if (string.IsNullOrWhiteSpace(targetKey) || string.IsNullOrWhiteSpace(pointId))
                return false;

            return revealedPointIdsByTargetKey.TryGetValue(targetKey, out HashSet<string> revealedSet)
                    && revealedSet.Contains(pointId);
        }

        /// <summary>타깃의 저장된 공개 포인트 상태를 제거한다.</summary>
        public void ClearTargetState(IHarvestTarget harvestTarget)
        {
            string targetKey = ResolveTargetKey(harvestTarget);
            ClearTargetState(targetKey);
        }

        /// <summary>타깃 키의 저장된 공개 포인트 상태를 제거한다.</summary>
        public void ClearTargetState(string targetKey)
        {
            if (string.IsNullOrWhiteSpace(targetKey))
                return;

            revealedPointIdsByTargetKey.Remove(targetKey);
        }

        /// <summary>타깃 키를 해석한다.</summary>
        private string ResolveTargetKey(IHarvestTarget harvestTarget)
        {
            if (harvestTarget is HarvestTargetBehaviour targetBehaviour)
                return targetBehaviour.GetRuntimeTargetKey();

            return string.Empty;
        }
    }
}
