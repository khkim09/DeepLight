using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// M-4 단계의 Runtime Final Content Resolver.
    /// 각 RuntimeSpawnedInstance(adapter)가 어떤 final content prefab/profile로 교체될 수 있는지
    /// "최종 해석 계획(WorldMapRuntimeFinalContentResolutionPlan)"을 생성한다.
    /// 이 단계에서는 scene instance를 실제로 교체하지 않으며, read-only plan build만 수행한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public static class WorldMapRuntimeFinalContentResolver
    {
        /// <summary>
        /// 주어진 adapter에 대해 최종 content 해석 계획을 생성한다.
        /// adapter의 contract를 가져와 requirement database에서 최적의 entry를 찾고,
        /// finalPrefab/finalProfile binding 정보를 plan에 기록한다.
        /// scene object는 절대 수정하지 않는다.
        /// </summary>
        /// <param name="adapter">해석 대상 runtime spawned gameplay adapter</param>
        /// <param name="database">final content requirement database</param>
        /// <param name="plan">생성된 resolution plan (실패 시에도 가능한 정보는 채워서 반환)</param>
        /// <returns>plan 생성 성공 시 true</returns>
        public static bool TryBuildPlan(
            WorldMapRuntimeSpawnGameplayAdapter adapter,
            WorldMapRuntimeFinalContentRequirementDatabaseSO database,
            out WorldMapRuntimeFinalContentResolutionPlan plan)
        {
            plan = new WorldMapRuntimeFinalContentResolutionPlan();

            // 1. adapter null check
            if (adapter == null)
            {
                plan.Configure(
                    sourceInstanceName: string.Empty,
                    sourceMarkerId: string.Empty,
                    zoneId: string.Empty,
                    runtimeCategory: string.Empty,
                    runtimeKey: string.Empty,
                    profileId: string.Empty,
                    adapterKind: WorldMapRuntimeGameplayAdapterKind.None,
                    finalContentKind: WorldMapRuntimeFinalContentKind.None,
                    requirementId: string.Empty,
                    requirementDisplayName: string.Empty,
                    requirementDescription: string.Empty,
                    finalPrefab: null,
                    finalProfile: null,
                    worldPosition: Vector3.zero,
                    worldRotation: Quaternion.identity,
                    worldScale: Vector3.one,
                    isFallbackProfile: false,
                    isFallbackRequirement: false,
                    isEditorPlaceholderPrefab: false,
                    isGeneratedFinalPlaceholder: false,
                    isUserAssignedFinalAsset: false,
                    isValidPlan: false,
                    failureReason: "Adapter is null");
                return false;
            }

            // 2. database null check
            if (database == null)
            {
                plan.Configure(
                    sourceInstanceName: adapter.gameObject.name,
                    sourceMarkerId: adapter.MarkerId,
                    zoneId: adapter.ZoneId,
                    runtimeCategory: adapter.RuntimeCategory,
                    runtimeKey: adapter.RuntimeKey,
                    profileId: adapter.ProfileId,
                    adapterKind: adapter.AdapterKind,
                    finalContentKind: WorldMapRuntimeFinalContentKind.None,
                    requirementId: string.Empty,
                    requirementDisplayName: string.Empty,
                    requirementDescription: string.Empty,
                    finalPrefab: null,
                    finalProfile: null,
                    worldPosition: adapter.transform.position,
                    worldRotation: adapter.transform.rotation,
                    worldScale: adapter.transform.lossyScale,
                    isFallbackProfile: adapter.IsFallbackProfile,
                    isFallbackRequirement: false,
                    isEditorPlaceholderPrefab: adapter.IsEditorPlaceholderPrefab,
                    isGeneratedFinalPlaceholder: false,
                    isUserAssignedFinalAsset: false,
                    isValidPlan: false,
                    failureReason: "Requirement database is null");
                return false;
            }

            // 3. adapter의 WorldMapRuntimeFinalContentContract 가져오기
            WorldMapRuntimeFinalContentContract contract = adapter.GetComponent<WorldMapRuntimeFinalContentContract>();
            if (contract == null)
            {
                plan.Configure(
                    sourceInstanceName: adapter.gameObject.name,
                    sourceMarkerId: adapter.MarkerId,
                    zoneId: adapter.ZoneId,
                    runtimeCategory: adapter.RuntimeCategory,
                    runtimeKey: adapter.RuntimeKey,
                    profileId: adapter.ProfileId,
                    adapterKind: adapter.AdapterKind,
                    finalContentKind: WorldMapRuntimeFinalContentKind.None,
                    requirementId: string.Empty,
                    requirementDisplayName: string.Empty,
                    requirementDescription: string.Empty,
                    finalPrefab: null,
                    finalProfile: null,
                    worldPosition: adapter.transform.position,
                    worldRotation: adapter.transform.rotation,
                    worldScale: adapter.transform.lossyScale,
                    isFallbackProfile: adapter.IsFallbackProfile,
                    isFallbackRequirement: false,
                    isEditorPlaceholderPrefab: adapter.IsEditorPlaceholderPrefab,
                    isGeneratedFinalPlaceholder: false,
                    isUserAssignedFinalAsset: false,
                    isValidPlan: false,
                    failureReason: "Adapter has no WorldMapRuntimeFinalContentContract component");
                return false;
            }

            // 4. database에서 최적의 requirement entry 찾기
            if (!database.TryFindBestRequirement(contract, out WorldMapRuntimeFinalContentRequirementEntry entry))
            {
                plan.Configure(
                    sourceInstanceName: adapter.gameObject.name,
                    sourceMarkerId: contract.SourceMarkerId,
                    zoneId: contract.ZoneId,
                    runtimeCategory: contract.RuntimeCategory,
                    runtimeKey: contract.RuntimeKey,
                    profileId: contract.ProfileId,
                    adapterKind: contract.AdapterKind,
                    finalContentKind: contract.FinalContentKind,
                    requirementId: string.Empty,
                    requirementDisplayName: string.Empty,
                    requirementDescription: string.Empty,
                    finalPrefab: null,
                    finalProfile: null,
                    worldPosition: adapter.transform.position,
                    worldRotation: adapter.transform.rotation,
                    worldScale: adapter.transform.lossyScale,
                    isFallbackProfile: contract.IsFallbackProfile,
                    isFallbackRequirement: false,
                    isEditorPlaceholderPrefab: contract.IsEditorPlaceholderPrefab,
                    isGeneratedFinalPlaceholder: false,
                    isUserAssignedFinalAsset: false,
                    isValidPlan: false,
                    failureReason: $"No matching requirement entry found for contract (Kind={contract.FinalContentKind}, Category={contract.RuntimeCategory}, Key={contract.RuntimeKey}, ProfileId={contract.ProfileId})");
                return false;
            }

            // 5. entry의 FinalPrefab / FinalProfile null check
            if (entry.FinalPrefab == null || entry.FinalProfile == null)
            {
                string reason = entry.FinalPrefab == null
                    ? "Matched entry has null FinalPrefab"
                    : "Matched entry has null FinalProfile";

                plan.Configure(
                    sourceInstanceName: adapter.gameObject.name,
                    sourceMarkerId: contract.SourceMarkerId,
                    zoneId: contract.ZoneId,
                    runtimeCategory: contract.RuntimeCategory,
                    runtimeKey: contract.RuntimeKey,
                    profileId: contract.ProfileId,
                    adapterKind: contract.AdapterKind,
                    finalContentKind: contract.FinalContentKind,
                    requirementId: entry.RequirementId,
                    requirementDisplayName: entry.DisplayName,
                    requirementDescription: entry.Description,
                    finalPrefab: entry.FinalPrefab,
                    finalProfile: entry.FinalProfile,
                    worldPosition: adapter.transform.position,
                    worldRotation: adapter.transform.rotation,
                    worldScale: adapter.transform.lossyScale,
                    isFallbackProfile: contract.IsFallbackProfile,
                    isFallbackRequirement: entry.IsFallbackRequirement,
                    isEditorPlaceholderPrefab: contract.IsEditorPlaceholderPrefab,
                    isGeneratedFinalPlaceholder: false,
                    isUserAssignedFinalAsset: false,
                    isValidPlan: false,
                    failureReason: reason);
                return false;
            }

            // 6. final prefab이 generated placeholder인지 확인
            bool isGeneratedFinalPlaceholder = false;
            WorldMapRuntimeFinalContentPlaceholderTag placeholderTag = entry.FinalPrefab.GetComponent<WorldMapRuntimeFinalContentPlaceholderTag>();
            if (placeholderTag != null && placeholderTag.IsGeneratedPlaceholder)
            {
                isGeneratedFinalPlaceholder = true;
            }

            // 7. generated placeholder가 아니고 finalPrefab이 non-null이면 user-assigned final asset으로 판정
            bool isUserAssignedFinalAsset = !isGeneratedFinalPlaceholder && entry.FinalPrefab != null;

            // 8. adapter transform 기준으로 world position/rotation/scale 기록 (read-only, 수정하지 않음)
            Vector3 worldPos = adapter.transform.position;
            Quaternion worldRot = adapter.transform.rotation;
            Vector3 worldScale = adapter.transform.lossyScale;

            // 9. 성공 plan 생성
            plan.Configure(
                sourceInstanceName: adapter.gameObject.name,
                sourceMarkerId: contract.SourceMarkerId,
                zoneId: contract.ZoneId,
                runtimeCategory: contract.RuntimeCategory,
                runtimeKey: contract.RuntimeKey,
                profileId: contract.ProfileId,
                adapterKind: contract.AdapterKind,
                finalContentKind: contract.FinalContentKind,
                requirementId: entry.RequirementId,
                requirementDisplayName: entry.DisplayName,
                requirementDescription: entry.Description,
                finalPrefab: entry.FinalPrefab,
                finalProfile: entry.FinalProfile,
                worldPosition: worldPos,
                worldRotation: worldRot,
                worldScale: worldScale,
                isFallbackProfile: contract.IsFallbackProfile,
                isFallbackRequirement: entry.IsFallbackRequirement,
                isEditorPlaceholderPrefab: contract.IsEditorPlaceholderPrefab,
                isGeneratedFinalPlaceholder: isGeneratedFinalPlaceholder,
                isUserAssignedFinalAsset: isUserAssignedFinalAsset,
                isValidPlan: true,
                failureReason: string.Empty);

            return true;
        }
    }
}
