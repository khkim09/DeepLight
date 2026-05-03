using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimePlaceholder GameObject와 RuntimeSpawnProfileDatabase를 입력받아
    /// WorldMapRuntimeSpawnReplacementPlan을 생성하는 순수 C# static resolver 클래스.
    /// scene object를 생성/삭제/수정하지 않으며, 단순 계산만 수행한다.
    /// Phase 14.10-J-1: Replacement Plan Resolver (읽기 전용 검증/계획 단계).
    /// </summary>
    public static class WorldMapRuntimeSpawnReplacementResolver
    {
        /// <summary>
        /// RuntimePlaceholder GameObject와 database를 읽어서 WorldMapRuntimeSpawnReplacementPlan을 생성한다.
        /// placeholder null, database null, binding 없음, candidate resolve 실패, database match 실패,
        /// entry.Prefab null인 경우 false를 반환하고 FailureReason에 사유를 기록한다.
        /// scene object를 절대 수정하지 않는다.
        /// </summary>
        /// <param name="placeholder">RuntimePlaceholder GameObject (WorldMapRuntimePlaceholderBinding 컴포넌트 필요)</param>
        /// <param name="database">RuntimeSpawnProfileDatabase</param>
        /// <param name="plan">생성된 Replacement Plan (실패 시에도 new로 생성되며 FailureReason이 설정됨)</param>
        /// <returns>plan이 성공적으로 생성되면 true</returns>
        public static bool TryBuildPlan(
            GameObject placeholder,
            WorldMapRuntimeSpawnProfileDatabaseSO database,
            out WorldMapRuntimeSpawnReplacementPlan plan)
        {
            // plan은 항상 new로 생성
            plan = new WorldMapRuntimeSpawnReplacementPlan();

            // 1. placeholder null 검사
            if (placeholder == null)
            {
                plan.FailureReason = "Placeholder GameObject is null.";
                plan.IsValidPlan = false;
                return false;
            }

            // 2. database null 검사
            if (database == null)
            {
                plan.FailureReason = "RuntimeSpawnProfileDatabase is null.";
                plan.IsValidPlan = false;
                return false;
            }

            // 3. placeholder에서 WorldMapRuntimePlaceholderBinding 컴포넌트 획득
            WorldMapRuntimePlaceholderBinding binding = placeholder.GetComponent<WorldMapRuntimePlaceholderBinding>();
            if (binding == null)
            {
                plan.FailureReason = $"Placeholder '{placeholder.name}' has no WorldMapRuntimePlaceholderBinding component.";
                plan.IsValidPlan = false;
                return false;
            }

            // 4. WorldMapRuntimeSpawnProfileResolver.TryResolve 호출
            if (!WorldMapRuntimeSpawnProfileResolver.TryResolve(binding, out WorldMapRuntimeSpawnProfileCandidate candidate))
            {
                plan.FailureReason = $"Failed to resolve candidate for placeholder '{placeholder.name}'.";
                plan.IsValidPlan = false;
                return false;
            }

            // 5. database.TryFindBestMatch 호출
            if (!database.TryFindBestMatch(candidate, out WorldMapRuntimeSpawnProfileEntry entry))
            {
                plan.FailureReason = $"No matching profile entry found for candidate (Category={candidate.RuntimeCategory}, Key={candidate.RuntimeKey}).";
                plan.IsValidPlan = false;
                return false;
            }

            // 6. entry.Prefab null 검사
            if (entry.Prefab == null)
            {
                plan.FailureReason = $"Matched entry '{entry.ProfileId}' has null Prefab.";
                plan.IsValidPlan = false;
                return false;
            }

            // 7. plan 필드 채우기 (candidate 기준)
            plan.ZoneId = candidate.ZoneId;
            plan.MarkerId = candidate.MarkerId;
            plan.MarkerType = candidate.MarkerType;
            plan.RuntimeCategory = candidate.RuntimeCategory;
            plan.RuntimeKey = candidate.RuntimeKey;

            // 8. plan 필드 채우기 (entry 기준)
            plan.ProfileId = entry.ProfileId;
            plan.Prefab = entry.Prefab;
            plan.PrefabName = entry.Prefab.name;

            // 9. plan 필드 채우기 (placeholder 기준)
            plan.PlaceholderName = placeholder.name;
            plan.WorldPosition = placeholder.transform.position;
            plan.WorldRotation = placeholder.transform.rotation;
            plan.WorldScale = placeholder.transform.lossyScale;

            // 10. plan 플래그 설정
            plan.IsFallbackProfile = entry.IsFallbackProfile;

            // IsEditorPlaceholderPrefab: Prefab에 WorldMapRuntimeSpawnPlaceholderTag가 있고 IsEditorPlaceholder == true인지 확인
            WorldMapRuntimeSpawnPlaceholderTag tag = entry.Prefab.GetComponent<WorldMapRuntimeSpawnPlaceholderTag>();
            plan.IsEditorPlaceholderPrefab = (tag != null && tag.IsEditorPlaceholder);

            plan.IsValidPlan = true;
            plan.FailureReason = null;

            return true;
        }
    }
}
