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
    /// Phase 14.10-H-1: Runtime Spawn Profile Resolver Utility.
    /// GeneratedWorldRoot 아래 RuntimePlaceholder들을 순회하며
    /// WorldMapRuntimePlaceholderBinding을 읽어 WorldMapRuntimeSpawnProfileResolver 결과를 검증한다.
    /// 이번 단계는 로그/검증 중심이며, scene object를 생성/삭제/수정하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnProfileResolverUtility
    {
        // ===== Constants =====
        private const string PlaceholderPrefix = "RuntimePlaceholder";
        private const string LogPrefix = "[SpawnProfileResolver]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 RuntimePlaceholder를 순회하며
        /// WorldMapRuntimeSpawnProfileResolver.TryResolve를 호출하고 결과를 집계한다.
        /// placeholder 또는 binding 값은 변경하지 않는다.
        /// </summary>
        public static void RebuildRuntimeSpawnProfileCandidates(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot rebuild spawn profile candidates.");
                return;
            }
            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot rebuild spawn profile candidates.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-H-1: Rebuild Runtime Spawn Profile Candidates =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError($"{LogPrefix} GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"{LogPrefix} '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 3. 모든 RuntimePlaceholder 수집
            List<GameObject> allPlaceholders = CollectAllRuntimePlaceholders(zoneRootsTransform);
            log.AppendLine($"  [OK] Found {allPlaceholders.Count} RuntimePlaceholder GameObjects.");

            if (allPlaceholders.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No RuntimePlaceholder GameObjects found. Nothing to resolve.");
                log.AppendLine("  [WARN] No RuntimePlaceholder GameObjects found.");
                Debug.Log(log.ToString());
                return;
            }

            // 4. 각 placeholder에 대해 resolver 실행
            int totalResolved = 0;
            int totalFallback = 0;
            int totalSkipped = 0;
            int totalNoBinding = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // 샘플 candidate key 저장 (verbose 전용)
            List<string> sampleKeys = new List<string>();
            const int maxSamples = 5;

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // binding 컴포넌트 확인
                WorldMapRuntimePlaceholderBinding binding = placeholder.GetComponent<WorldMapRuntimePlaceholderBinding>();
                if (binding == null)
                {
                    totalNoBinding++;
                    continue;
                }

                // resolver 호출
                if (WorldMapRuntimeSpawnProfileResolver.TryResolve(binding, out WorldMapRuntimeSpawnProfileCandidate candidate))
                {
                    totalResolved++;

                    if (candidate.IsFallbackCandidate)
                    {
                        totalFallback++;
                    }

                    // category count
                    string cat = !string.IsNullOrEmpty(candidate.RuntimeCategory) ? candidate.RuntimeCategory : "Unknown";
                    if (categoryCount.ContainsKey(cat))
                    {
                        categoryCount[cat]++;
                    }
                    else
                    {
                        categoryCount[cat] = 1;
                    }

                    // verbose 샘플 수집
                    if (settings.LogVerbose && sampleKeys.Count < maxSamples)
                    {
                        string key = WorldMapRuntimeSpawnProfileResolver.BuildCandidateKey(binding);
                        sampleKeys.Add($"{placeholder.name} -> {key} ({candidate.ResolverNote})");
                    }
                }
                else
                {
                    totalSkipped++;
                }
            }

            // 5. 결과 로그
            log.AppendLine($"  Resolved: {totalResolved}");
            log.AppendLine($"    - Normal: {totalResolved - totalFallback}");
            log.AppendLine($"    - Fallback: {totalFallback}");
            log.AppendLine($"  Skipped (resolver returned false): {totalSkipped}");
            log.AppendLine($"  No Binding component: {totalNoBinding}");
            log.AppendLine("");

            // category별 count 출력
            log.AppendLine("  === Category Counts ===");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            // verbose 샘플 출력
            if (settings.LogVerbose && sampleKeys.Count > 0)
            {
                log.AppendLine("");
                log.AppendLine("  === Sample Candidate Keys (first {maxSamples}) ===");
                foreach (string sample in sampleKeys)
                {
                    log.AppendLine($"    {sample}");
                }
            }

            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-H-1: Rebuild Runtime Spawn Profile Candidates Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 RuntimePlaceholder에 대해
        /// WorldMapRuntimeSpawnProfileResolver의 검증을 수행한다.
        /// 12개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN] summary를 출력한다.
        /// scene object transform/metadata는 변경하지 않는다.
        /// </summary>
        public static void ValidateRuntimeSpawnProfileCandidates(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate spawn profile candidates.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-H-1: Validate Runtime Spawn Profile Candidates =====");

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
                log.AppendLine("===== Phase 14.10-H-1: Validate Runtime Spawn Profile Candidates Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. ZoneRoots parent 찾기
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
                log.AppendLine("===== Phase 14.10-H-1: Validate Runtime Spawn Profile Candidates Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 3. 모든 RuntimePlaceholder 수집
            List<GameObject> allPlaceholders = CollectAllRuntimePlaceholders(zoneRootsTransform);
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
                log.AppendLine("===== Phase 14.10-H-1: Validate Runtime Spawn Profile Candidates Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4-12. 상세 검사 카운터
            int totalWithBinding = 0;
            int totalNoBinding = 0;
            int totalResolved = 0;
            int totalFallback = 0;
            int totalSkipped = 0;
            int emptyZoneId = 0;
            int emptyMarkerId = 0;
            int emptyRuntimeCategory = 0;
            int emptyRuntimeKey = 0;
            int unknownMarkerType = 0;
            int prototypeCount = 0;
            int nonPrototypeCount = 0;
            int prototypeMismatch = 0;
            int nonPrototypeMismatch = 0;
            int sceneObjectChanged = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // 검증 시작 전 placeholder transform/meta snapshot (변경 감지용)
            Dictionary<GameObject, Vector3> positionSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, Quaternion> rotationSnapshot = new Dictionary<GameObject, Quaternion>();
            Dictionary<GameObject, Vector3> scaleSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, string> nameSnapshot = new Dictionary<GameObject, string>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // snapshot 저장
                positionSnapshot[placeholder] = placeholder.transform.position;
                rotationSnapshot[placeholder] = placeholder.transform.rotation;
                scaleSnapshot[placeholder] = placeholder.transform.localScale;
                nameSnapshot[placeholder] = placeholder.name;

                // binding 컴포넌트 확인
                WorldMapRuntimePlaceholderBinding binding = placeholder.GetComponent<WorldMapRuntimePlaceholderBinding>();
                if (binding == null)
                {
                    totalNoBinding++;
                    continue;
                }

                totalWithBinding++;

                // 4. 모든 binding에 대해 resolver가 candidate를 반환하거나 명확한 fallback note를 남김
                if (WorldMapRuntimeSpawnProfileResolver.TryResolve(binding, out WorldMapRuntimeSpawnProfileCandidate candidate))
                {
                    totalResolved++;
                    if (candidate.IsFallbackCandidate)
                    {
                        totalFallback++;
                    }

                    // 5. Candidate ZoneId non-empty
                    if (string.IsNullOrEmpty(candidate.ZoneId))
                    {
                        emptyZoneId++;
                    }

                    // 6. Candidate MarkerId non-empty
                    if (string.IsNullOrEmpty(candidate.MarkerId))
                    {
                        emptyMarkerId++;
                    }

                    // 7. Candidate RuntimeCategory non-empty
                    if (string.IsNullOrEmpty(candidate.RuntimeCategory))
                    {
                        emptyRuntimeCategory++;
                    }

                    // 8. Candidate RuntimeKey 또는 fallback key non-empty
                    if (string.IsNullOrEmpty(candidate.RuntimeKey))
                    {
                        emptyRuntimeKey++;
                    }

                    // 9. Candidate MarkerType이 Unknown이 아닌 비율
                    if (candidate.MarkerType == WorldMapZoneContentMarkerType.Unknown)
                    {
                        unknownMarkerType++;
                    }

                    // 10. Prototype / NonPrototype count가 기존 binding 판정과 일치
                    if (candidate.IsPrototypeZone)
                    {
                        prototypeCount++;
                        if (!binding.IsPrototypeZone)
                        {
                            prototypeMismatch++;
                        }
                    }
                    else
                    {
                        nonPrototypeCount++;
                        if (binding.IsPrototypeZone)
                        {
                            nonPrototypeMismatch++;
                        }
                    }

                    // category count
                    string cat = !string.IsNullOrEmpty(candidate.RuntimeCategory) ? candidate.RuntimeCategory : "Unknown";
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
                    totalSkipped++;
                }
            }

            // 11. Resolver가 scene object transform/metadata를 변경하지 않았는지 확인
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

            // ===== 검증 결과 출력 =====

            // 4. 모든 binding에 대해 resolver가 candidate를 반환하거나 명확한 fallback note를 남김
            if (totalSkipped == 0)
            {
                log.AppendLine($"  [PASS] All {totalWithBinding} bindings resolved successfully (fallback: {totalFallback}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {totalSkipped} binding(s) failed to resolve.");
                failCount++;
            }

            // 5. Candidate ZoneId non-empty
            if (emptyZoneId == 0)
            {
                log.AppendLine("  [PASS] All candidate ZoneId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyZoneId} candidate(s) have empty ZoneId.");
                failCount++;
            }

            // 6. Candidate MarkerId non-empty
            if (emptyMarkerId == 0)
            {
                log.AppendLine("  [PASS] All candidate MarkerId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyMarkerId} candidate(s) have empty MarkerId.");
                failCount++;
            }

            // 7. Candidate RuntimeCategory non-empty
            if (emptyRuntimeCategory == 0)
            {
                log.AppendLine("  [PASS] All candidate RuntimeCategory values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyRuntimeCategory} candidate(s) have empty RuntimeCategory.");
                failCount++;
            }

            // 8. Candidate RuntimeKey 또는 fallback key non-empty
            if (emptyRuntimeKey == 0)
            {
                log.AppendLine("  [PASS] All candidate RuntimeKey values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyRuntimeKey} candidate(s) have empty RuntimeKey.");
                failCount++;
            }

            // 9. Candidate MarkerType이 Unknown이 아닌 비율
            int nonUnknownCount = totalWithBinding - unknownMarkerType;
            float nonUnknownRatio = totalWithBinding > 0 ? (float)nonUnknownCount / totalWithBinding * 100f : 0f;
            log.AppendLine($"  [INFO] MarkerType non-Unknown ratio: {nonUnknownCount}/{totalWithBinding} ({nonUnknownRatio:F1}%)");
            if (unknownMarkerType == 0)
            {
                log.AppendLine("  [PASS] No candidate has Unknown MarkerType.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {unknownMarkerType} candidate(s) have Unknown MarkerType (may be intentional).");
                warnCount++;
            }

            // 10. Prototype / NonPrototype count가 기존 binding 판정과 일치
            if (prototypeMismatch == 0 && nonPrototypeMismatch == 0)
            {
                log.AppendLine($"  [PASS] Prototype/NonPrototype counts match binding: Proto={prototypeCount}, NonProto={nonPrototypeCount}.");
                passCount++;
            }
            else
            {
                if (prototypeMismatch > 0)
                    log.AppendLine($"  [FAIL] {prototypeMismatch} candidate(s) have IsPrototypeZone mismatch with binding.");
                if (nonPrototypeMismatch > 0)
                    log.AppendLine($"  [FAIL] {nonPrototypeMismatch} candidate(s) have IsPrototypeZone mismatch with binding.");
                failCount++;
            }

            // 11. Resolver가 scene object transform/metadata를 변경하지 않았는지 확인
            if (sceneObjectChanged == 0)
            {
                log.AppendLine("  [PASS] No scene object transform/metadata was modified by resolver.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {sceneObjectChanged} placeholder(s) had transform/metadata changes detected.");
                failCount++;
            }

            // 12. category별 candidate count 출력
            log.AppendLine("  [INFO] Category counts:");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }
            // category count 자체는 PASS/FAIL이 아니므로 passCount 증가 없음

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine($"  Total placeholders: {allPlaceholders.Count}");
            log.AppendLine($"  With binding: {totalWithBinding}");
            log.AppendLine($"  No binding: {totalNoBinding}");
            log.AppendLine($"  Resolved: {totalResolved} (Normal: {totalResolved - totalFallback}, Fallback: {totalFallback})");
            log.AppendLine($"  Skipped: {totalSkipped}");
            log.AppendLine("===== Phase 14.10-H-1: Validate Runtime Spawn Profile Candidates Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// ZoneRoots parent Transform 하위의 모든 RuntimePlaceholder_* GameObject를 재귀적으로 수집한다.
        /// DeepLightMapRuntimePlaceholderBindingUtility의 동일 메서드와 동일한 로직.
        /// </summary>
        private static List<GameObject> CollectAllRuntimePlaceholders(Transform zoneRootsTransform)
        {
            List<GameObject> result = new List<GameObject>();

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
        /// Transform 하위를 재귀적으로 탐색하여 RuntimePlaceholder_ prefix를 가진 GameObject를 수집한다.
        /// </summary>
        private static void CollectPlaceholdersRecursive(Transform parent, List<GameObject> result)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;

                if (child.name.StartsWith(PlaceholderPrefix + "_"))
                {
                    result.Add(child.gameObject);
                }

                CollectPlaceholdersRecursive(child, result);
            }
        }
    }
}
