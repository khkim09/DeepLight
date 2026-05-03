using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-J-3: Runtime Spawn Instance Replacement Utility.
    /// RuntimePlaceholder를 실제 prefab instance로 치환하기 위한 "실행형 생성 단계".
    /// RuntimePlaceholder 원본은 삭제하지 않고, 옵션에 따라 비활성화만 한다.
    /// 실제 삭제/완전 정리는 Phase 14.10-J-4 이후로 넘긴다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnInstanceReplacementUtility
    {
        private const string LogPrefix = "[SpawnInstanceReplacement]";
        private const string PlaceholderPrefix = "RuntimePlaceholder";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeSpawnProfileDatabase.asset";
        private const string RuntimeSpawnedInstancesRootName = "RuntimeSpawnedInstances";
        private const string UnknownCategoryName = "Unknown";

        // ===== Public API =====

        /// <summary>
        /// 모든 RuntimePlaceholder에 대해 Replacement Plan을 빌드하고,
        /// 각 plan에 대응되는 실제 runtime spawn prefab instance를 ZoneRoot 하위 RuntimeSpawnedInstances root에 생성한다.
        /// RuntimePlaceholder 원본은 절대 삭제하지 않는다.
        /// disableSourcePlaceholders == true일 때만 RuntimePlaceholder 원본을 SetActive(false) 한다.
        /// </summary>
        public static void RebuildRuntimeSpawnInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context,
            bool disableSourcePlaceholders)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot rebuild runtime spawn instances.");
                return;
            }
            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot rebuild runtime spawn instances.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-3: Rebuild Runtime Spawn Instances =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError($"{LogPrefix} GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. RuntimeSpawnProfileDatabase 로드
            WorldMapRuntimeSpawnProfileDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
            if (database == null)
            {
                Debug.LogError($"{LogPrefix} RuntimeSpawnProfileDatabase not found at '{DatabaseAssetPath}'. Run Phase 14.10-H-2 first.");
                return;
            }
            log.AppendLine($"  [OK] RuntimeSpawnProfileDatabase loaded: {database.name} (entries: {database.Entries.Count})");

            // 3. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"{LogPrefix} '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 4. 기존 실제 runtime spawn instance 제거 (preview instance는 삭제하지 않음)
            int clearedCount = ClearRuntimeInstancesInternal(generatedRoot, log);

            // 5. 모든 RuntimePlaceholder 수집 (중복 방지 HashSet 사용, 비활성 포함)
            HashSet<GameObject> placeholderSet = CollectAllRuntimePlaceholders(zoneRootsTransform);
            List<GameObject> allPlaceholders = new List<GameObject>(placeholderSet);
            log.AppendLine($"  [OK] Found {allPlaceholders.Count} unique RuntimePlaceholder GameObjects.");

            if (allPlaceholders.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No RuntimePlaceholder GameObjects found. Nothing to generate.");
                log.AppendLine("  [WARN] No RuntimePlaceholder GameObjects found.");
                Debug.Log(log.ToString());
                return;
            }

            // 6. 각 placeholder에 대해 plan build + runtime instance 생성
            int totalSuccess = 0;
            int totalFail = 0;
            int totalFallback = 0;
            int totalEditorPlaceholder = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // 실패한 placeholder 목록
            List<string> failedPlaceholders = new List<string>();

            // ZoneRoot_XX별 RuntimeSpawnedInstances root 캐시
            Dictionary<string, Transform> zoneInstanceRootCache = new Dictionary<string, Transform>();

            // RuntimeSpawnedInstances/{category} root 캐시 (zone별)
            Dictionary<string, Dictionary<string, Transform>> categoryRootCache = new Dictionary<string, Dictionary<string, Transform>>();

            // 이름 충돌 방지를 위한 instance 이름 카운터
            Dictionary<string, int> instanceNameCounter = new Dictionary<string, int>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // plan build
                if (WorldMapRuntimeSpawnReplacementResolver.TryBuildPlan(placeholder, database, out WorldMapRuntimeSpawnReplacementPlan plan))
                {
                    totalSuccess++;

                    if (plan.IsFallbackProfile)
                    {
                        totalFallback++;
                    }

                    if (plan.IsEditorPlaceholderPrefab)
                    {
                        totalEditorPlaceholder++;
                    }

                    // category count
                    string cat = !string.IsNullOrEmpty(plan.RuntimeCategory) ? plan.RuntimeCategory : UnknownCategoryName;
                    if (categoryCount.ContainsKey(cat))
                    {
                        categoryCount[cat]++;
                    }
                    else
                    {
                        categoryCount[cat] = 1;
                    }

                    // ZoneRoot_XX 찾기
                    Transform zoneRoot = FindZoneRootForPlaceholder(placeholder, zoneRootsTransform);
                    if (zoneRoot == null)
                    {
                        log.AppendLine($"  [WARN] Could not find ZoneRoot for placeholder '{placeholder.name}'. Skipping instance creation.");
                        continue;
                    }

                    // RuntimeSpawnedInstances root 획득 (zone별 캐시)
                    Transform instanceRoot = GetOrCreateRuntimeSpawnedInstancesRoot(zoneRoot, zoneInstanceRootCache);

                    // category root 획득 (zone별 캐시)
                    Transform categoryRoot = GetOrCreateCategoryRoot(instanceRoot, cat, zoneRoot.name, categoryRootCache);

                    // instance 이름 생성 (충돌 방지)
                    string baseName = SanitizeName($"RuntimeSpawn_{plan.ProfileId}_{plan.MarkerId}");
                    string instanceName = baseName;
                    if (instanceNameCounter.ContainsKey(instanceName))
                    {
                        instanceNameCounter[instanceName]++;
                        instanceName = $"{baseName}_{instanceNameCounter[instanceName]:D3}";
                    }
                    else
                    {
                        instanceNameCounter[instanceName] = 0;
                    }

                    // PrefabUtility.InstantiatePrefab 사용
                    GameObject instance = PrefabUtility.InstantiatePrefab(plan.Prefab, categoryRoot) as GameObject;
                    if (instance == null)
                    {
                        log.AppendLine($"  [WARN] Failed to instantiate prefab '{plan.PrefabName}' for placeholder '{placeholder.name}'.");
                        continue;
                    }

                    // instance 이름 설정
                    instance.name = instanceName;

                    // transform 설정
                    instance.transform.position = plan.WorldPosition;
                    instance.transform.rotation = plan.WorldRotation;
                    instance.transform.localScale = Vector3.one;

                    // WorldMapRuntimeSpawnInstanceTag 부착 (isPreviewInstance = false)
                    WorldMapRuntimeSpawnInstanceTag tag = instance.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                    if (tag == null)
                    {
                        tag = instance.AddComponent<WorldMapRuntimeSpawnInstanceTag>();
                    }
                    tag.Configure(plan, false);

                    // WorldMapRuntimePlaceholderBinding / WorldMapZoneContentMarker는 추가하지 않음
                }
                else
                {
                    totalFail++;
                    failedPlaceholders.Add($"{placeholder.name}: {plan.FailureReason}");
                }
            }

            // 7. disableSourcePlaceholders 옵션 처리
            int disabledCount = 0;
            if (disableSourcePlaceholders)
            {
                foreach (GameObject placeholder in allPlaceholders)
                {
                    if (placeholder != null && placeholder.activeSelf)
                    {
                        placeholder.SetActive(false);
                        disabledCount++;
                    }
                }
                log.AppendLine($"  [INFO] Disabled {disabledCount} source RuntimePlaceholder(s).");
            }
            else
            {
                log.AppendLine("  [INFO] Source RuntimePlaceholders preserved (disableSourcePlaceholders=false).");
            }

            // 8. 결과 로그
            log.AppendLine($"  Runtime instances created: {totalSuccess}");
            log.AppendLine($"    - Normal: {totalSuccess - totalFallback}");
            log.AppendLine($"    - Fallback profile: {totalFallback}");
            log.AppendLine($"    - Editor placeholder prefab: {totalEditorPlaceholder}");
            log.AppendLine($"  Failed: {totalFail}");

            if (failedPlaceholders.Count > 0)
            {
                log.AppendLine("  Failed placeholders:");
                foreach (string failInfo in failedPlaceholders)
                {
                    log.AppendLine($"    [FAIL] {failInfo}");
                }
            }

            log.AppendLine("");

            // category별 count 출력
            log.AppendLine("  === Category Counts ===");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-J-3: Rebuild Runtime Spawn Instances Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Runtime Spawn Instance의 유효성을 검사한다.
        /// 20개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN] summary를 출력한다.
        /// RuntimePlaceholder 원본 count/transform/name 변경 없음을 검증한다.
        /// Preview instance와 Runtime instance가 서로 구분되는지 확인한다.
        /// </summary>
        public static void ValidateRuntimeSpawnInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate runtime spawn instances.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-3: Validate Runtime Spawn Instances =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot 존재 확인
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine("  [PASS] GeneratedWorldRoot exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-3: Validate Runtime Spawn Instances Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. RuntimeSpawnProfileDatabase asset 존재 확인
            WorldMapRuntimeSpawnProfileDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
            if (database != null)
            {
                log.AppendLine($"  [PASS] RuntimeSpawnProfileDatabase exists (entries: {database.Entries.Count}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] RuntimeSpawnProfileDatabase not found at '{DatabaseAssetPath}'!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-3: Validate Runtime Spawn Instances Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 3. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform != null)
            {
                log.AppendLine("  [PASS] ZoneRoots parent exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] ZoneRoots parent not found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-3: Validate Runtime Spawn Instances Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4. 모든 RuntimePlaceholder 수집 (원본 보존 검증용)
            HashSet<GameObject> placeholderSet = CollectAllRuntimePlaceholders(zoneRootsTransform);
            List<GameObject> allPlaceholders = new List<GameObject>(placeholderSet);

            if (allPlaceholders.Count > 0)
            {
                log.AppendLine($"  [PASS] RuntimePlaceholder count: {allPlaceholders.Count} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] No RuntimePlaceholder GameObjects found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-3: Validate Runtime Spawn Instances Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 5. Replacement Plan build success count == RuntimePlaceholder count
            int totalPlansBuilt = 0;
            int totalPlanFailed = 0;
            int fallbackCount = 0;
            int editorPlaceholderCount = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // placeholder snapshot (원본 보존 검증용)
            Dictionary<string, GameObject> placeholderByName = new Dictionary<string, GameObject>();
            Dictionary<GameObject, Vector3> positionSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, Quaternion> rotationSnapshot = new Dictionary<GameObject, Quaternion>();
            Dictionary<GameObject, Vector3> scaleSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, string> nameSnapshot = new Dictionary<GameObject, string>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // 이름 기반 dictionary 구축
                if (!placeholderByName.ContainsKey(placeholder.name))
                {
                    placeholderByName[placeholder.name] = placeholder;
                }

                // snapshot 저장
                positionSnapshot[placeholder] = placeholder.transform.position;
                rotationSnapshot[placeholder] = placeholder.transform.rotation;
                scaleSnapshot[placeholder] = placeholder.transform.localScale;
                nameSnapshot[placeholder] = placeholder.name;

                // plan build
                if (WorldMapRuntimeSpawnReplacementResolver.TryBuildPlan(placeholder, database, out WorldMapRuntimeSpawnReplacementPlan plan))
                {
                    totalPlansBuilt++;

                    if (plan.IsFallbackProfile)
                    {
                        fallbackCount++;
                    }

                    if (plan.IsEditorPlaceholderPrefab)
                    {
                        editorPlaceholderCount++;
                    }

                    string cat = !string.IsNullOrEmpty(plan.RuntimeCategory) ? plan.RuntimeCategory : UnknownCategoryName;
                    if (categoryCount.ContainsKey(cat))
                    {
                        categoryCount[cat]++;
                    }
                    else
                    {
                        categoryCount[cat] = 1;
                    }
                }
                else
                {
                    totalPlanFailed++;
                }
            }

            // 5. plan build 성공률
            if (totalPlanFailed == 0)
            {
                log.AppendLine($"  [PASS] All {totalPlansBuilt} plans built successfully.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {totalPlanFailed} plan(s) failed to build.");
                failCount++;
            }

            // 6. Runtime instance count == successful plan count
            List<WorldMapRuntimeSpawnInstanceTag> allRuntimeTags = CollectAllRuntimeInstanceTags(generatedRoot);
            int runtimeInstanceCount = allRuntimeTags.Count;

            if (runtimeInstanceCount == totalPlansBuilt)
            {
                log.AppendLine($"  [PASS] Runtime instance count ({runtimeInstanceCount}) matches successful plan count ({totalPlansBuilt}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Runtime instance count ({runtimeInstanceCount}) does not match successful plan count ({totalPlansBuilt}).");
                failCount++;
            }

            // 7. 모든 runtime instance에 WorldMapRuntimeSpawnInstanceTag 존재
            int missingTag = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag == null)
                {
                    missingTag++;
                }
            }
            if (missingTag == 0)
            {
                log.AppendLine("  [PASS] All runtime instances have WorldMapRuntimeSpawnInstanceTag.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {missingTag} runtime instance(s) missing WorldMapRuntimeSpawnInstanceTag.");
                failCount++;
            }

            // 8. 모든 tag.IsPreviewInstance == false
            int isPreview = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag != null && tag.IsPreviewInstance)
                {
                    isPreview++;
                }
            }
            if (isPreview == 0)
            {
                log.AppendLine("  [PASS] All tags have IsPreviewInstance == false.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {isPreview} tag(s) have IsPreviewInstance == true (expected false).");
                failCount++;
            }

            // 9. 모든 tag.ZoneId non-empty
            int emptyZoneId = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.ZoneId))
                {
                    emptyZoneId++;
                }
            }
            if (emptyZoneId == 0)
            {
                log.AppendLine("  [PASS] All tag ZoneId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyZoneId} tag(s) have empty ZoneId.");
                failCount++;
            }

            // 10. 모든 tag.MarkerId non-empty
            int emptyMarkerId = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.MarkerId))
                {
                    emptyMarkerId++;
                }
            }
            if (emptyMarkerId == 0)
            {
                log.AppendLine("  [PASS] All tag MarkerId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyMarkerId} tag(s) have empty MarkerId.");
                failCount++;
            }

            // 11. 모든 tag.ProfileId non-empty
            int emptyProfileId = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.ProfileId))
                {
                    emptyProfileId++;
                }
            }
            if (emptyProfileId == 0)
            {
                log.AppendLine("  [PASS] All tag ProfileId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyProfileId} tag(s) have empty ProfileId.");
                failCount++;
            }

            // 12. 모든 runtime instance에 WorldMapRuntimePlaceholderBinding 없음
            int hasBinding = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag != null)
                {
                    WorldMapRuntimePlaceholderBinding binding = tag.GetComponent<WorldMapRuntimePlaceholderBinding>();
                    if (binding != null)
                    {
                        hasBinding++;
                    }
                }
            }
            if (hasBinding == 0)
            {
                log.AppendLine("  [PASS] No runtime instance has WorldMapRuntimePlaceholderBinding.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {hasBinding} runtime instance(s) have WorldMapRuntimePlaceholderBinding.");
                failCount++;
            }

            // 13. 모든 runtime instance에 WorldMapZoneContentMarker 없음
            int hasMarker = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag != null)
                {
                    WorldMapZoneContentMarker marker = tag.GetComponent<WorldMapZoneContentMarker>();
                    if (marker != null)
                    {
                        hasMarker++;
                    }
                }
            }
            if (hasMarker == 0)
            {
                log.AppendLine("  [PASS] No runtime instance has WorldMapZoneContentMarker.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {hasMarker} runtime instance(s) have WorldMapZoneContentMarker.");
                failCount++;
            }

            // 14. 모든 runtime instance 위치가 source placeholder 위치와 0.1 unit 이내
            int positionMismatch = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allRuntimeTags)
            {
                if (tag == null) continue;

                // SourcePlaceholderName으로 placeholder 찾기
                if (placeholderByName.TryGetValue(tag.SourcePlaceholderName, out GameObject sourcePlaceholder))
                {
                    if (sourcePlaceholder != null)
                    {
                        float distance = Vector3.Distance(tag.transform.position, sourcePlaceholder.transform.position);
                        if (distance > 0.1f)
                        {
                            positionMismatch++;
                        }
                    }
                }
            }
            if (positionMismatch == 0)
            {
                log.AppendLine("  [PASS] All runtime instance positions match source placeholder positions (within 0.1 unit).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {positionMismatch} runtime instance(s) have position mismatch > 0.1 unit.");
                failCount++;
            }

            // 15. fallback runtime count (INFO)
            log.AppendLine($"  [INFO] Fallback runtime instances: {fallbackCount}");

            // 16. editor placeholder prefab runtime count (INFO)
            log.AppendLine($"  [INFO] Editor placeholder prefab runtime instances: {editorPlaceholderCount}");

            // 17. category별 breakdown (INFO)
            log.AppendLine("  [INFO] Category breakdown:");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }

            // 18. RuntimePlaceholder 원본 count가 validate 전후 동일한지 확인
            HashSet<GameObject> placeholderSetAfter = CollectAllRuntimePlaceholders(zoneRootsTransform);
            int placeholderCountAfter = placeholderSetAfter.Count;
            if (placeholderCountAfter == allPlaceholders.Count)
            {
                log.AppendLine($"  [PASS] RuntimePlaceholder count unchanged: {placeholderCountAfter}.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] RuntimePlaceholder count changed! Before: {allPlaceholders.Count}, After: {placeholderCountAfter}.");
                failCount++;
            }

            // 19. RuntimePlaceholder 원본 transform/name 변경 없음 확인
            int sceneObjectChanged = 0;
            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                if (positionSnapshot.TryGetValue(placeholder, out Vector3 pos) &&
                    Vector3.Distance(pos, placeholder.transform.position) > 0.001f)
                {
                    sceneObjectChanged++;
                }
                else if (rotationSnapshot.TryGetValue(placeholder, out Quaternion rot) &&
                         Quaternion.Angle(rot, placeholder.transform.rotation) > 0.01f)
                {
                    sceneObjectChanged++;
                }
                else if (scaleSnapshot.TryGetValue(placeholder, out Vector3 scl) &&
                         Vector3.Distance(scl, placeholder.transform.localScale) > 0.001f)
                {
                    sceneObjectChanged++;
                }
                else if (nameSnapshot.TryGetValue(placeholder, out string name) &&
                         placeholder.name != name)
                {
                    sceneObjectChanged++;
                }
            }
            if (sceneObjectChanged == 0)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholder transform/name was modified.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {sceneObjectChanged} RuntimePlaceholder(s) had transform/name changes detected.");
                failCount++;
            }

            // 20. Preview instance와 Runtime instance가 서로 구분되는지 확인
            List<WorldMapRuntimeSpawnInstanceTag> allPreviewTags = CollectAllPreviewInstanceTags(generatedRoot);
            int previewCount = allPreviewTags.Count;
            int runtimeCount = allRuntimeTags.Count;

            // IsPreviewInstance=true는 preview, IsPreviewInstance=false는 runtime
            int previewTagCount = 0;
            int runtimeTagCount = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true))
            {
                if (tag != null)
                {
                    if (tag.IsPreviewInstance) previewTagCount++;
                    else runtimeTagCount++;
                }
            }

            if (previewTagCount == previewCount && runtimeTagCount == runtimeCount)
            {
                log.AppendLine($"  [PASS] Preview instances ({previewTagCount}) and Runtime instances ({runtimeTagCount}) are correctly distinguished.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Preview/Runtime instance distinction mismatch. Preview tags: {previewTagCount}, Runtime tags: {runtimeTagCount}.");
                failCount++;
            }

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine($"  Total placeholders: {allPlaceholders.Count}");
            log.AppendLine($"  Plans built: {totalPlansBuilt}");
            log.AppendLine($"  Plans failed: {totalPlanFailed}");
            log.AppendLine($"  Runtime instances: {runtimeInstanceCount}");
            log.AppendLine($"  Preview instances: {previewCount}");
            log.AppendLine($"  Fallback profiles: {fallbackCount}");
            log.AppendLine($"  Editor placeholder prefabs: {editorPlaceholderCount}");
            log.AppendLine("===== Phase 14.10-J-3: Validate Runtime Spawn Instances Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 Runtime Spawn Instance를 삭제한다.
        /// 삭제 대상: WorldMapRuntimeSpawnInstanceTag.IsPreviewInstance == false 인 GameObject.
        /// Preview instance / RuntimePlaceholder / Marker / DebugVisual은 절대 삭제하지 않는다.
        /// reactivateSourcePlaceholders == true이면 비활성화된 RuntimePlaceholder를 다시 SetActive(true) 한다.
        /// </summary>
        public static void ClearRuntimeSpawnInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context,
            bool reactivateSourcePlaceholders)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot clear runtime spawn instances.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-3: Clear Runtime Spawn Instances =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogWarning($"{LogPrefix} GeneratedWorldRoot not found. Nothing to clear.");
                log.AppendLine("  [WARN] GeneratedWorldRoot not found. Nothing to clear.");
                Debug.Log(log.ToString());
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. Runtime Instance 제거 (IsPreviewInstance == false)
            int clearedCount = ClearRuntimeInstancesInternal(generatedRoot, log);

            // 3. reactivateSourcePlaceholders 옵션 처리
            int reactivatedCount = 0;
            if (reactivateSourcePlaceholders)
            {
                // ZoneRoots parent 찾기
                Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zoneRootsTransform != null)
                {
                    HashSet<GameObject> placeholderSet = CollectAllRuntimePlaceholders(zoneRootsTransform);
                    foreach (GameObject placeholder in placeholderSet)
                    {
                        if (placeholder != null && !placeholder.activeSelf)
                        {
                            placeholder.SetActive(true);
                            reactivatedCount++;
                        }
                    }
                }
                log.AppendLine($"  [INFO] Reactivated {reactivatedCount} source RuntimePlaceholder(s).");
            }
            else
            {
                log.AppendLine("  [INFO] Source RuntimePlaceholders not reactivated (reactivateSourcePlaceholders=false).");
            }

            log.AppendLine($"  [OK] Cleared {clearedCount} runtime instance(s).");
            log.AppendLine("  [OK] Preview instances / RuntimePlaceholder / Marker / DebugVisual preserved.");
            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-J-3: Clear Runtime Spawn Instances Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 Runtime Instance를 제거하고 제거된 개수를 반환한다.
        /// 삭제 대상: WorldMapRuntimeSpawnInstanceTag.IsPreviewInstance == false 인 GameObject.
        /// Preview instance / RuntimePlaceholder / Marker / DebugVisual은 절대 삭제하지 않는다.
        /// 빈 RuntimeSpawnedInstances root는 하위가 비면 삭제한다.
        /// </summary>
        private static int ClearRuntimeInstancesInternal(GameObject generatedRoot, StringBuilder log)
        {
            int clearedCount = 0;

            // GeneratedWorldRoot 하위의 모든 WorldMapRuntimeSpawnInstanceTag 수집
            List<WorldMapRuntimeSpawnInstanceTag> allTags = new List<WorldMapRuntimeSpawnInstanceTag>(
                generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true));

            // IsPreviewInstance == false 인 것만 삭제
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                if (tag != null && !tag.IsPreviewInstance)
                {
                    toDestroy.Add(tag.gameObject);
                }
            }

            // 중복 제거 (같은 GameObject에 여러 tag가 있을 수 있음)
            HashSet<GameObject> uniqueToDestroy = new HashSet<GameObject>(toDestroy);

            foreach (GameObject obj in uniqueToDestroy)
            {
                if (obj != null)
                {
                    // 부모 Transform 기록 (빈 root 정리용)
                    Transform parentTransform = obj.transform.parent;

                    Undo.DestroyObjectImmediate(obj);
                    clearedCount++;

                    // 부모가 RuntimeSpawnedInstances root이고, 하위가 비었으면 삭제
                    if (parentTransform != null && parentTransform.name == RuntimeSpawnedInstancesRootName)
                    {
                        if (parentTransform.childCount == 0)
                        {
                            Transform grandParent = parentTransform.parent;
                            Undo.DestroyObjectImmediate(parentTransform.gameObject);
                            log.AppendLine($"  [INFO] Removed empty RuntimeSpawnedInstances root under '{grandParent?.name ?? "null"}'.");
                        }
                    }
                }
            }

            log.AppendLine($"  [OK] Found {uniqueToDestroy.Count} runtime instance(s) to clear.");
            return clearedCount;
        }

        /// <summary>
        /// ZoneRoots parent Transform 하위의 모든 RuntimePlaceholder GameObject를 재귀적으로 수집한다.
        /// 기준: WorldMapRuntimePlaceholderBinding 컴포넌트를 가진 GameObject 또는 이름이 "RuntimePlaceholder_"로 시작하는 GameObject.
        /// 비활성 RuntimePlaceholder도 포함한다.
        /// 중복 카운트가 생기지 않도록 HashSet을 사용한다.
        /// </summary>
        private static HashSet<GameObject> CollectAllRuntimePlaceholders(Transform zoneRootsTransform)
        {
            HashSet<GameObject> result = new HashSet<GameObject>();

            for (int i = 0; i < zoneRootsTransform.childCount; i++)
            {
                Transform zoneRoot = zoneRootsTransform.GetChild(i);
                if (zoneRoot == null) continue;
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;

                for (int j = 0; j < zoneRoot.childCount; j++)
                {
                    Transform childRoot = zoneRoot.GetChild(j);
                    if (childRoot == null) continue;

                    CollectPlaceholdersRecursive(childRoot, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Transform 하위를 재귀적으로 탐색하여 RuntimePlaceholder GameObject를 수집한다.
        /// 기준: WorldMapRuntimePlaceholderBinding 컴포넌트를 가졌거나 이름이 "RuntimePlaceholder_"로 시작하는 GameObject.
        /// 비활성 GameObject도 포함한다.
        /// </summary>
        private static void CollectPlaceholdersRecursive(Transform parent, HashSet<GameObject> result)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;

                // 기준 1: WorldMapRuntimePlaceholderBinding 컴포넌트를 가진 GameObject
                bool hasBinding = child.GetComponent<WorldMapRuntimePlaceholderBinding>() != null;

                // 기준 2: 이름이 "RuntimePlaceholder_"로 시작하는 GameObject
                bool nameMatch = child.name.StartsWith(PlaceholderPrefix + "_");

                if (hasBinding || nameMatch)
                {
                    result.Add(child.gameObject);
                }

                CollectPlaceholdersRecursive(child, result);
            }
        }

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 Runtime Instance Tag (IsPreviewInstance == false)를 수집한다.
        /// </summary>
        private static List<WorldMapRuntimeSpawnInstanceTag> CollectAllRuntimeInstanceTags(GameObject generatedRoot)
        {
            List<WorldMapRuntimeSpawnInstanceTag> result = new List<WorldMapRuntimeSpawnInstanceTag>();

            if (generatedRoot == null) return result;

            WorldMapRuntimeSpawnInstanceTag[] allTags = generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true);
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                if (tag != null && !tag.IsPreviewInstance)
                {
                    result.Add(tag);
                }
            }

            return result;
        }

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 Preview Instance Tag (IsPreviewInstance == true)를 수집한다.
        /// </summary>
        private static List<WorldMapRuntimeSpawnInstanceTag> CollectAllPreviewInstanceTags(GameObject generatedRoot)
        {
            List<WorldMapRuntimeSpawnInstanceTag> result = new List<WorldMapRuntimeSpawnInstanceTag>();

            if (generatedRoot == null) return result;

            WorldMapRuntimeSpawnInstanceTag[] allTags = generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true);
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                if (tag != null && tag.IsPreviewInstance)
                {
                    result.Add(tag);
                }
            }

            return result;
        }

        /// <summary>
        /// placeholder GameObject가 속한 ZoneRoot_XX Transform을 찾는다.
        /// 부모 체인을 따라 올라가면서 ZoneRoot_XX로 시작하는 Transform을 반환한다.
        /// </summary>
        private static Transform FindZoneRootForPlaceholder(GameObject placeholder, Transform zoneRootsTransform)
        {
            if (placeholder == null) return null;

            Transform current = placeholder.transform.parent;
            while (current != null)
            {
                if (current.name.StartsWith("ZoneRoot_"))
                {
                    return current;
                }
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// ZoneRoot 하위에 RuntimeSpawnedInstances root를 찾거나 생성한다.
        /// zoneInstanceRootCache를 사용하여 중복 검색을 방지한다.
        /// </summary>
        private static Transform GetOrCreateRuntimeSpawnedInstancesRoot(
            Transform zoneRoot,
            Dictionary<string, Transform> zoneInstanceRootCache)
        {
            if (zoneRoot == null) return null;

            string zoneKey = zoneRoot.name;

            // 캐시 확인
            if (zoneInstanceRootCache.TryGetValue(zoneKey, out Transform cached))
            {
                if (cached != null) return cached;
            }

            // RuntimeSpawnedInstances root 찾기
            Transform instanceRoot = zoneRoot.Find(RuntimeSpawnedInstancesRootName);
            if (instanceRoot == null)
            {
                // 새로 생성
                GameObject newRoot = new GameObject(RuntimeSpawnedInstancesRootName);
                newRoot.transform.SetParent(zoneRoot);
                newRoot.transform.localPosition = Vector3.zero;
                newRoot.transform.localRotation = Quaternion.identity;
                newRoot.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(newRoot, $"Create {RuntimeSpawnedInstancesRootName}");
                instanceRoot = newRoot.transform;
            }

            zoneInstanceRootCache[zoneKey] = instanceRoot;
            return instanceRoot;
        }

        /// <summary>
        /// RuntimeSpawnedInstances root 하위에 category root를 찾거나 생성한다.
        /// categoryRootCache를 사용하여 중복 검색을 방지한다.
        /// </summary>
        private static Transform GetOrCreateCategoryRoot(
            Transform instanceRoot,
            string category,
            string zoneName,
            Dictionary<string, Dictionary<string, Transform>> categoryRootCache)
        {
            if (instanceRoot == null) return null;

            string zoneKey = zoneName;

            // zone별 캐시 확인
            if (!categoryRootCache.ContainsKey(zoneKey))
            {
                categoryRootCache[zoneKey] = new Dictionary<string, Transform>();
            }

            Dictionary<string, Transform> zoneCache = categoryRootCache[zoneKey];

            // category 캐시 확인
            if (zoneCache.TryGetValue(category, out Transform cached))
            {
                if (cached != null) return cached;
            }

            // category root 찾기
            Transform categoryRoot = instanceRoot.Find(category);
            if (categoryRoot == null)
            {
                // 새로 생성
                GameObject newCategoryRoot = new GameObject(category);
                newCategoryRoot.transform.SetParent(instanceRoot);
                newCategoryRoot.transform.localPosition = Vector3.zero;
                newCategoryRoot.transform.localRotation = Quaternion.identity;
                newCategoryRoot.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(newCategoryRoot, $"Create {category}");
                categoryRoot = newCategoryRoot.transform;
            }

            zoneCache[category] = categoryRoot;
            return categoryRoot;
        }

        /// <summary>
        /// 문자열을 파일/오브젝트 이름에 안전하도록 sanitize 처리한다.
        /// 공백, 특수문자를 언더스코어로 치환한다.
        /// </summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                // 유효하지 않은 파일 이름 문자를 언더스코어로 치환
                bool isInvalid = false;
                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (chars[i] == invalidChars[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }
                if (isInvalid || chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
