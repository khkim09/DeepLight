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
    /// Phase 14.10-G-1: Runtime Placeholder Metadata Binding Utility.
    /// RuntimePlaceholder GameObject에 WorldMapRuntimePlaceholderBinding 컴포넌트를 부착/갱신하고
    /// source marker metadata를 기반으로 binding 필드를 채운다.
    /// 기존 placeholder visual/transform/material은 절대 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimePlaceholderBindingUtility
    {
        // ===== Constants =====
        private const string PlaceholderPrefix = "RuntimePlaceholder";
        private const string BindingComponentName = "WorldMapRuntimePlaceholderBinding";

        /// <summary>
        /// Prototype 17개 zone: Hub(4), Harbor(8), Wreck(5)
        /// </summary>
        private static readonly HashSet<string> PrototypeZoneIds = new HashSet<string>
        {
            // Hub (4)
            "E5", "F5", "E6", "F6",
            // Harbor (8)
            "D5", "D6", "E4", "F4", "G5", "G6", "E7", "F7",
            // Wreck (5)
            "B5", "C5", "B6", "C6", "C7"
        };

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 RuntimePlaceholder_* GameObject를 찾아
        /// 대응되는 source WorldMapZoneContentMarker를 기준으로
        /// WorldMapRuntimePlaceholderBinding 컴포넌트를 추가/갱신한다.
        /// 기존 placeholder visual/transform/material은 절대 변경하지 않는다.
        /// </summary>
        public static void RebuildRuntimePlaceholderBindings(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[PlaceholderBinding] Settings is null! Cannot rebuild placeholder bindings.");
                return;
            }
            if (context == null)
            {
                Debug.LogError("[PlaceholderBinding] Context is null! Cannot rebuild placeholder bindings.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-G-1: Rebuild Runtime Placeholder Bindings =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[PlaceholderBinding] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. 모든 RuntimePlaceholder_* GameObject 탐색
            // placeholder는 ZoneRoot_XX 하위의 child root (Props, HarvestSpawns, Hazards, LogSpawns, Triggers, DebugGizmos)에 위치
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[PlaceholderBinding] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 3. 모든 RuntimePlaceholder GameObject 수집
            List<GameObject> allPlaceholders = CollectAllRuntimePlaceholders(zoneRootsTransform);
            log.AppendLine($"  [OK] Found {allPlaceholders.Count} RuntimePlaceholder GameObjects.");

            if (allPlaceholders.Count == 0)
            {
                Debug.LogWarning("[PlaceholderBinding] No RuntimePlaceholder GameObjects found. Nothing to bind.");
                log.AppendLine("  [WARN] No RuntimePlaceholder GameObjects found.");
                Debug.Log(log.ToString());
                return;
            }

            // 4. 모든 WorldMapZoneContentMarker를 dictionary로 캐싱 (markerId 기준)
            WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
            Dictionary<string, WorldMapZoneContentMarker> markerById = new Dictionary<string, WorldMapZoneContentMarker>();
            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;
                string mid = marker.MarkerId;
                if (!string.IsNullOrEmpty(mid) && !markerById.ContainsKey(mid))
                {
                    markerById[mid] = marker;
                }
            }
            log.AppendLine($"  [OK] Cached {markerById.Count} markers by MarkerId (total components: {allMarkers.Length}).");

            // 5. 각 placeholder에 binding 컴포넌트 추가/갱신
            int totalProcessed = 0;
            int totalCreated = 0;
            int totalReused = 0;
            int totalSkipped = 0;
            int totalFallback = 0;

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // 5a. placeholder 이름에서 markerId 후보 추출
                // 이름 형식: RuntimePlaceholder_{MarkerType}_{MarkerId}
                string placeholderName = placeholder.name;
                string markerIdCandidate = ExtractMarkerIdFromPlaceholderName(placeholderName);

                if (string.IsNullOrEmpty(markerIdCandidate))
                {
                    // 이름에서 markerId를 추출할 수 없으면 skip
                    LogIfVerbose(settings, $"[SKIP] Cannot extract markerId from placeholder name '{placeholderName}'. Skipped.");
                    totalSkipped++;
                    continue;
                }

                // 5b. markerId 기준으로 source marker 찾기
                WorldMapZoneContentMarker sourceMarker = null;
                bool isFallback = false;

                if (markerById.TryGetValue(markerIdCandidate, out WorldMapZoneContentMarker foundMarker))
                {
                    sourceMarker = foundMarker;
                }
                else
                {
                    // 5c. markerId로 못 찾으면 zone root / parent root / position 기반 fallback 시도
                    sourceMarker = FindMarkerByFallback(placeholder, markerById, allMarkers);
                    isFallback = (sourceMarker != null);
                }

                if (sourceMarker == null)
                {
                    // 5d. 그래도 못 찾으면 WARN 후 skip
                    Debug.LogWarning($"[PlaceholderBinding] Cannot find source marker for placeholder '{placeholderName}' (candidate markerId='{markerIdCandidate}'). Skipped.");
                    totalSkipped++;
                    continue;
                }

                // 5e. binding 컴포넌트 추가 또는 재사용
                WorldMapRuntimePlaceholderBinding binding = placeholder.GetComponent<WorldMapRuntimePlaceholderBinding>();
                if (binding == null)
                {
                    binding = placeholder.AddComponent<WorldMapRuntimePlaceholderBinding>();
                    Undo.RegisterCreatedObjectUndo(binding, $"Add {BindingComponentName} to {placeholderName}");
                    totalCreated++;
                }
                else
                {
                    totalReused++;
                }

                // 5f. marker metadata 기반으로 binding 필드 채우기
                string zoneId = sourceMarker.ZoneId;
                string markerId = sourceMarker.MarkerId;
                WorldMapZoneContentMarkerType markerType = sourceMarker.MarkerType;
                string[] tags = sourceMarker.Tags;
                string ruleSummary = sourceMarker.SourceRuleSummary;
                string planSummary = sourceMarker.SourcePlanSummary;

                // RuntimeCategory / RuntimeKey 생성
                string runtimeCategory = ResolveRuntimeCategory(markerType);
                string runtimeKey = ResolveRuntimeKey(markerType, markerId, tags);

                // IsPrototypeZone 판정
                bool isPrototypeZone = IsPrototypeZone(zoneId);

                // IsRuntimeReady: 이번 단계에서는 기본 false
                // 단, RuntimeCategory/RuntimeKey가 정상 생성되면 binding 자체는 valid
                bool isRuntimeReady = false;

                // Configure 호출
                binding.Configure(
                    zoneId: zoneId,
                    markerId: markerId,
                    markerType: markerType,
                    semanticTags: tags,
                    sourceRuleSummary: ruleSummary,
                    sourcePlanSummary: planSummary,
                    runtimeCategory: runtimeCategory,
                    runtimeKey: runtimeKey,
                    isRuntimeReady: isRuntimeReady,
                    isPrototypeZone: isPrototypeZone,
                    isFallbackBinding: isFallback
                );

                // 변경사항 저장
                EditorUtility.SetDirty(binding);

                totalProcessed++;
                if (isFallback) totalFallback++;
            }

            // 6. 결과 로그
            log.AppendLine($"  Processed: {totalProcessed}");
            log.AppendLine($"  Created: {totalCreated}");
            log.AppendLine($"  Reused: {totalReused}");
            log.AppendLine($"  Skipped: {totalSkipped}");
            log.AppendLine($"  Fallback: {totalFallback}");
            log.AppendLine("===== Phase 14.10-G-1: Rebuild Runtime Placeholder Bindings Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 RuntimePlaceholder에 대해
        /// WorldMapRuntimePlaceholderBinding 컴포넌트의 유효성을 검사한다.
        /// 12개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN] summary를 출력한다.
        /// </summary>
        public static void ValidateRuntimePlaceholderBindings(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[PlaceholderBinding] Settings is null! Cannot validate placeholder bindings.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-G-1: Validate Runtime Placeholder Bindings =====");

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
                log.AppendLine("===== Phase 14.10-G-1: Validate Runtime Placeholder Bindings Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                log.AppendLine("  [FAIL] ZoneRoots parent not found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-G-1: Validate Runtime Placeholder Bindings Complete =====");
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
                log.AppendLine("===== Phase 14.10-G-1: Validate Runtime Placeholder Bindings Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4-12. 상세 검사 카운터
            int placeholdersWithBinding = 0;
            int placeholdersWithMultipleBindings = 0;
            int placeholdersMissingBinding = 0;
            int emptyZoneId = 0;
            int emptyMarkerId = 0;
            int unknownMarkerType = 0;
            int emptyRuntimeCategory = 0;
            int emptyRuntimeKey = 0;
            int nullSemanticTags = 0;
            int prototypeZoneMismatch = 0;
            int nonPrototypeZoneMismatch = 0;
            int placeholdersWithMarkerComponent = 0;

            // prototype zone 판정 검사용
            int prototypePlaceholderCount = 0;
            int nonPrototypePlaceholderCount = 0;

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // 4. binding component가 정확히 1개 있는지 확인
                WorldMapRuntimePlaceholderBinding[] bindings = placeholder.GetComponents<WorldMapRuntimePlaceholderBinding>();
                if (bindings.Length == 1)
                {
                    placeholdersWithBinding++;
                }
                else if (bindings.Length == 0)
                {
                    placeholdersMissingBinding++;
                    continue; // binding이 없으면 상세 검사 불가
                }
                else
                {
                    placeholdersWithMultipleBindings++;
                    continue; // 중복 binding은 상세 검사 불가
                }

                WorldMapRuntimePlaceholderBinding binding = bindings[0];

                // 5. binding.ZoneId non-empty 확인
                if (string.IsNullOrEmpty(binding.ZoneId))
                {
                    emptyZoneId++;
                }

                // 6. binding.MarkerId non-empty 확인
                if (string.IsNullOrEmpty(binding.MarkerId))
                {
                    emptyMarkerId++;
                }

                // 7. binding.MarkerType이 Unknown이 아닌지 확인 (진짜 Unknown은 WARN만 허용)
                if (binding.MarkerType == WorldMapZoneContentMarkerType.Unknown)
                {
                    unknownMarkerType++;
                }

                // 8. RuntimeCategory non-empty 확인
                if (string.IsNullOrEmpty(binding.RuntimeCategory))
                {
                    emptyRuntimeCategory++;
                }

                // 9. RuntimeKey non-empty 확인
                if (string.IsNullOrEmpty(binding.RuntimeKey))
                {
                    emptyRuntimeKey++;
                }

                // 10. SemanticTags null 방지 확인
                if (binding.SemanticTags == null)
                {
                    nullSemanticTags++;
                }

                // 11. Prototype zone 판정 검사
                bool isPrototype = IsPrototypeZone(binding.ZoneId);
                if (isPrototype)
                {
                    prototypePlaceholderCount++;
                    if (!binding.IsPrototypeZone)
                    {
                        prototypeZoneMismatch++;
                    }
                }
                else
                {
                    nonPrototypePlaceholderCount++;
                    if (binding.IsPrototypeZone)
                    {
                        nonPrototypeZoneMismatch++;
                    }
                }

                // 12. WorldMapZoneContentMarker 컴포넌트가 RuntimePlaceholder에 붙어있지 않은지 확인
                WorldMapZoneContentMarker markerComp = placeholder.GetComponent<WorldMapZoneContentMarker>();
                if (markerComp != null)
                {
                    placeholdersWithMarkerComponent++;
                }
            }

            // 4. 모든 RuntimePlaceholder에 binding component가 정확히 1개 있는지 확인
            if (placeholdersMissingBinding == 0 && placeholdersWithMultipleBindings == 0)
            {
                log.AppendLine($"  [PASS] All {allPlaceholders.Count} RuntimePlaceholders have exactly 1 {BindingComponentName}.");
                passCount++;
            }
            else
            {
                if (placeholdersMissingBinding > 0)
                    log.AppendLine($"  [FAIL] {placeholdersMissingBinding} RuntimePlaceholder(s) missing {BindingComponentName}.");
                if (placeholdersWithMultipleBindings > 0)
                    log.AppendLine($"  [FAIL] {placeholdersWithMultipleBindings} RuntimePlaceholder(s) have multiple {BindingComponentName}.");
                failCount++;
            }

            // 5. binding.ZoneId non-empty
            if (emptyZoneId == 0)
            {
                log.AppendLine("  [PASS] All binding ZoneId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyZoneId} binding(s) have empty ZoneId.");
                failCount++;
            }

            // 6. binding.MarkerId non-empty
            if (emptyMarkerId == 0)
            {
                log.AppendLine("  [PASS] All binding MarkerId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyMarkerId} binding(s) have empty MarkerId.");
                failCount++;
            }

            // 7. binding.MarkerType이 Unknown이 아닌지 확인
            if (unknownMarkerType == 0)
            {
                log.AppendLine("  [PASS] All binding MarkerType values are not Unknown.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {unknownMarkerType} binding(s) have Unknown MarkerType (may be intentional).");
                warnCount++;
            }

            // 8. RuntimeCategory non-empty
            if (emptyRuntimeCategory == 0)
            {
                log.AppendLine("  [PASS] All binding RuntimeCategory values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyRuntimeCategory} binding(s) have empty RuntimeCategory.");
                failCount++;
            }

            // 9. RuntimeKey non-empty
            if (emptyRuntimeKey == 0)
            {
                log.AppendLine("  [PASS] All binding RuntimeKey values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyRuntimeKey} binding(s) have empty RuntimeKey.");
                failCount++;
            }

            // 10. SemanticTags null 방지
            if (nullSemanticTags == 0)
            {
                log.AppendLine("  [PASS] All binding SemanticTags arrays are non-null.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {nullSemanticTags} binding(s) have null SemanticTags array.");
                failCount++;
            }

            // 11. Prototype zone 판정 검사
            if (prototypeZoneMismatch == 0 && nonPrototypeZoneMismatch == 0)
            {
                log.AppendLine($"  [PASS] All {prototypePlaceholderCount} prototype zone placeholders have IsPrototypeZone=true, " +
                              $"all {nonPrototypePlaceholderCount} non-prototype zone placeholders have IsPrototypeZone=false.");
                passCount++;
            }
            else
            {
                if (prototypeZoneMismatch > 0)
                    log.AppendLine($"  [FAIL] {prototypeZoneMismatch} prototype zone placeholder(s) have IsPrototypeZone=false.");
                if (nonPrototypeZoneMismatch > 0)
                    log.AppendLine($"  [FAIL] {nonPrototypeZoneMismatch} non-prototype zone placeholder(s) have IsPrototypeZone=true.");
                failCount++;
            }

            // 12. WorldMapZoneContentMarker 컴포넌트가 RuntimePlaceholder에 붙어있지 않은지 확인
            if (placeholdersWithMarkerComponent == 0)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholder has WorldMapZoneContentMarker component.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersWithMarkerComponent} RuntimePlaceholder(s) have WorldMapZoneContentMarker (should not).");
                failCount++;
            }

            // 13. placeholder count와 binding count가 일치하는지 확인
            if (placeholdersWithBinding == allPlaceholders.Count)
            {
                log.AppendLine($"  [PASS] Placeholder count ({allPlaceholders.Count}) matches binding count ({placeholdersWithBinding}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] Placeholder count ({allPlaceholders.Count}) != binding count ({placeholdersWithBinding}). " +
                              $"Missing: {allPlaceholders.Count - placeholdersWithBinding}");
                warnCount++;
            }

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-G-1: Validate Runtime Placeholder Bindings Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// ZoneRoots parent Transform 하위의 모든 RuntimePlaceholder_* GameObject를 재귀적으로 수집한다.
        /// </summary>
        private static List<GameObject> CollectAllRuntimePlaceholders(Transform zoneRootsTransform)
        {
            List<GameObject> result = new List<GameObject>();

            // ZoneRoots parent의 모든 자식(ZoneRoot_XX) 순회
            for (int i = 0; i < zoneRootsTransform.childCount; i++)
            {
                Transform zoneRoot = zoneRootsTransform.GetChild(i);
                if (zoneRoot == null) continue;
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;

                // ZoneRoot 하위의 모든 child root 순회 (Props, HarvestSpawns, Hazards, LogSpawns, Triggers, DebugGizmos)
                for (int j = 0; j < zoneRoot.childCount; j++)
                {
                    Transform childRoot = zoneRoot.GetChild(j);
                    if (childRoot == null) continue;

                    // child root 하위의 모든 자식 중 RuntimePlaceholder_ prefix를 가진 것 수집
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

                // RuntimePlaceholder_ prefix 확인
                if (child.name.StartsWith(PlaceholderPrefix + "_"))
                {
                    result.Add(child.gameObject);
                }

                // 재귀 탐색 (중첩 구조 대비)
                CollectPlaceholdersRecursive(child, result);
            }
        }

        /// <summary>
        /// RuntimePlaceholder 이름에서 markerId를 추출한다.
        /// 이름 형식: RuntimePlaceholder_{MarkerType}_{MarkerId}
        /// "RuntimePlaceholder_" prefix를 제거한 후, 첫 번째 '_' 이후의 모든 문자열을 markerId로 간주한다.
        /// </summary>
        private static string ExtractMarkerIdFromPlaceholderName(string placeholderName)
        {
            if (string.IsNullOrEmpty(placeholderName)) return null;

            string prefix = PlaceholderPrefix + "_";
            if (!placeholderName.StartsWith(prefix)) return null;

            // prefix 제거
            string afterPrefix = placeholderName.Substring(prefix.Length);

            // 첫 번째 '_' 이후가 markerId (MarkerType 부분 제거)
            int firstUnderscore = afterPrefix.IndexOf('_');
            if (firstUnderscore < 0)
            {
                // MarkerType만 있고 markerId가 없는 경우
                return null;
            }

            // 첫 번째 '_' 이후의 모든 문자열이 markerId
            string markerId = afterPrefix.Substring(firstUnderscore + 1);

            return string.IsNullOrEmpty(markerId) ? null : markerId;
        }

        /// <summary>
        /// markerId 기반 lookup에 실패했을 때 fallback 전략으로 source marker를 찾는다.
        /// 1. placeholder의 parent chain에서 ZoneRoot_XX를 찾아 zoneId 추출
        /// 2. zoneId 기준으로 해당 zone의 marker 중에서 위치가 가장 가까운 marker를 찾음
        /// 3. 찾으면 해당 marker 반환 (IsFallbackBinding=true 처리됨)
        /// </summary>
        private static WorldMapZoneContentMarker FindMarkerByFallback(
            GameObject placeholder,
            Dictionary<string, WorldMapZoneContentMarker> markerById,
            WorldMapZoneContentMarker[] allMarkers)
        {
            if (placeholder == null || allMarkers == null || allMarkers.Length == 0) return null;

            // 1. placeholder의 parent chain에서 ZoneRoot_XX 찾기
            Transform current = placeholder.transform.parent;
            string zoneId = null;
            while (current != null)
            {
                if (current.name.StartsWith("ZoneRoot_"))
                {
                    zoneId = current.name.Replace("ZoneRoot_", "");
                    break;
                }
                current = current.parent;
            }

            if (string.IsNullOrEmpty(zoneId)) return null;

            // 2. 같은 zoneId를 가진 marker 중에서 위치가 가장 가까운 marker 찾기
            Vector3 placeholderPos = placeholder.transform.position;
            WorldMapZoneContentMarker closestMarker = null;
            float closestDistance = float.MaxValue;

            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;
                if (!string.Equals(marker.ZoneId, zoneId, System.StringComparison.OrdinalIgnoreCase)) continue;

                float distance = Vector3.Distance(placeholderPos, marker.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestMarker = marker;
                }
            }

            // 3. 너무 먼 marker는 fallback으로 간주하지 않음 (임계값: 50유닛)
            if (closestMarker != null && closestDistance < 50f)
            {
                Debug.Log($"[PlaceholderBinding] Fallback: matched placeholder '{placeholder.name}' to marker '{closestMarker.MarkerId}' " +
                          $"in zone '{zoneId}' (distance={closestDistance:F2}).");
                return closestMarker;
            }

            return null;
        }

        /// <summary>
        /// marker type에 따라 RuntimeCategory 문자열을 결정한다.
        /// </summary>
        private static string ResolveRuntimeCategory(WorldMapZoneContentMarkerType markerType)
        {
            switch (markerType)
            {
                case WorldMapZoneContentMarkerType.Resource:
                    return "HarvestResource";
                case WorldMapZoneContentMarkerType.Hazard:
                    return "Hazard";
                case WorldMapZoneContentMarkerType.Landmark:
                    return "Landmark";
                case WorldMapZoneContentMarkerType.Narrative:
                    return "NarrativeLog";
                case WorldMapZoneContentMarkerType.Route:
                    return "RouteTrigger";
                case WorldMapZoneContentMarkerType.Debug:
                case WorldMapZoneContentMarkerType.Unknown:
                default:
                    return "Debug";
            }
        }

        /// <summary>
        /// marker type과 metadata를 기반으로 RuntimeKey 문자열을 결정한다.
        /// Resource/Hazard/Landmark: 첫 번째 semantic tag 기반
        /// Narrative: markerId 또는 첫 번째 semantic tag 기반
        /// Route: markerId 기반
        /// Debug/Unknown: markerId 기반
        /// </summary>
        private static string ResolveRuntimeKey(
            WorldMapZoneContentMarkerType markerType,
            string markerId,
            string[] tags)
        {
            string firstTag = (tags != null && tags.Length > 0 && !string.IsNullOrEmpty(tags[0]))
                ? tags[0]
                : null;

            switch (markerType)
            {
                case WorldMapZoneContentMarkerType.Resource:
                    return firstTag ?? "UnknownResource";

                case WorldMapZoneContentMarkerType.Hazard:
                    return firstTag ?? "UnknownHazard";

                case WorldMapZoneContentMarkerType.Landmark:
                    return firstTag ?? "UnknownLandmark";

                case WorldMapZoneContentMarkerType.Narrative:
                    // markerId 또는 첫 번째 semantic tag 기반
                    return firstTag ?? (markerId ?? "UnknownNarrative");

                case WorldMapZoneContentMarkerType.Route:
                    return markerId ?? "UnknownRoute";

                case WorldMapZoneContentMarkerType.Debug:
                case WorldMapZoneContentMarkerType.Unknown:
                default:
                    return markerId ?? "Unknown";
            }
        }

        /// <summary>
        /// zoneId가 prototype 17개 zone에 속하는지 판정한다.
        /// Prototype zone: Hub(E5,F5,E6,F6), Harbor(D5,D6,E4,F4,G5,G6,E7,F7), Wreck(B5,C5,B6,C6,C7)
        /// </summary>
        private static bool IsPrototypeZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return false;
            return PrototypeZoneIds.Contains(zoneId);
        }

        // ===== Logging =====

        /// <summary>
        /// settings.LogVerbose가 true일 때만 로그를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[PlaceholderBinding] {message}");
            }
        }
    }
}
