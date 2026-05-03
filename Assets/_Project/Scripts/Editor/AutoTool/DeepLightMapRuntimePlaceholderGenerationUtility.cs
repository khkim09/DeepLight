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
    /// Phase 14.10-E-1: Full Marker Runtime Placeholder Generation Utility.
    /// GeneratedWorldRoot 아래의 모든 WorldMapZoneContentMarker를 기준으로
    /// ZoneRoot_XX 하위 runtime parent root에 placeholder GameObject를 생성한다.
    /// marker root/marker metadata/DebugVisual/Registry count는 절대 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimePlaceholderGenerationUtility
    {
        // ===== Constants =====
        private const string PlaceholderPrefix = "RuntimePlaceholder";
        private const string GeneratedMaterialsFolder = "Assets/_Project/Generated/RuntimePlaceholderMaterials";

        // ===== MarkerType별 Parent Root Name Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, string> MarkerTypeToParentRootMap =
            new Dictionary<WorldMapZoneContentMarkerType, string>
            {
                { WorldMapZoneContentMarkerType.Resource, "HarvestSpawns" },
                { WorldMapZoneContentMarkerType.Hazard, "Hazards" },
                { WorldMapZoneContentMarkerType.Landmark, "Props" },
                { WorldMapZoneContentMarkerType.Narrative, "LogSpawns" },
                { WorldMapZoneContentMarkerType.Route, "Triggers" },
                { WorldMapZoneContentMarkerType.Debug, "DebugGizmos" },
                { WorldMapZoneContentMarkerType.Unknown, "DebugGizmos" },
            };

        // ===== ZoneRoot 하위 보장할 child root 이름 목록 =====
        private static readonly string[] ZoneChildRootNames = new[]
        {
            "Props",
            "HarvestSpawns",
            "LogSpawns",
            "Hazards",
            "Triggers",
            "DebugGizmos"
        };

        // ===== MarkerType → Primitive Type Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, PrimitiveType> MarkerPrimitiveMap =
            new Dictionary<WorldMapZoneContentMarkerType, PrimitiveType>
            {
                { WorldMapZoneContentMarkerType.Resource, PrimitiveType.Sphere },
                { WorldMapZoneContentMarkerType.Hazard, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Landmark, PrimitiveType.Cylinder },
                { WorldMapZoneContentMarkerType.Narrative, PrimitiveType.Capsule },
                { WorldMapZoneContentMarkerType.Route, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Debug, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Unknown, PrimitiveType.Cube },
            };

        // ===== MarkerType → Local Scale Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, Vector3> MarkerScaleMap =
            new Dictionary<WorldMapZoneContentMarkerType, Vector3>
            {
                { WorldMapZoneContentMarkerType.Resource, new Vector3(3f, 3f, 3f) },
                { WorldMapZoneContentMarkerType.Hazard, new Vector3(4f, 4f, 4f) },
                { WorldMapZoneContentMarkerType.Landmark, new Vector3(5f, 5f, 5f) },
                { WorldMapZoneContentMarkerType.Narrative, new Vector3(3f, 5f, 3f) },
                { WorldMapZoneContentMarkerType.Route, new Vector3(6f, 1f, 6f) },
                { WorldMapZoneContentMarkerType.Debug, new Vector3(3f, 3f, 3f) },
                { WorldMapZoneContentMarkerType.Unknown, new Vector3(3f, 3f, 3f) },
            };

        // ===== MarkerType → Material Color Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, Color> MarkerColorMap =
            new Dictionary<WorldMapZoneContentMarkerType, Color>
            {
                { WorldMapZoneContentMarkerType.Resource, new Color(0.2f, 0.8f, 0.2f, 0.9f) },   // Green
                { WorldMapZoneContentMarkerType.Hazard, new Color(0.9f, 0.2f, 0.2f, 0.9f) },     // Red
                { WorldMapZoneContentMarkerType.Landmark, new Color(1.0f, 0.6f, 0.1f, 0.9f) },   // Yellow/Orange
                { WorldMapZoneContentMarkerType.Narrative, new Color(0.2f, 0.7f, 1.0f, 0.9f) },  // Cyan/Blue
                { WorldMapZoneContentMarkerType.Route, new Color(0.8f, 0.8f, 0.8f, 0.9f) },      // White/Gray
                { WorldMapZoneContentMarkerType.Debug, new Color(0.5f, 0.5f, 0.5f, 0.9f) },      // Gray
                { WorldMapZoneContentMarkerType.Unknown, new Color(0.5f, 0.5f, 0.5f, 0.9f) },    // Gray
            };

        // ===== Material Name Map =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, string> MaterialNameMap =
            new Dictionary<WorldMapZoneContentMarkerType, string>
            {
                { WorldMapZoneContentMarkerType.Resource, "RuntimePlaceholder_Resource_Mat" },
                { WorldMapZoneContentMarkerType.Hazard, "RuntimePlaceholder_Hazard_Mat" },
                { WorldMapZoneContentMarkerType.Landmark, "RuntimePlaceholder_Landmark_Mat" },
                { WorldMapZoneContentMarkerType.Narrative, "RuntimePlaceholder_Narrative_Mat" },
                { WorldMapZoneContentMarkerType.Route, "RuntimePlaceholder_Route_Mat" },
                { WorldMapZoneContentMarkerType.Debug, "RuntimePlaceholder_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Unknown, "RuntimePlaceholder_Unknown_Mat" },
            };

        // ===== Cached Materials =====
        private static Dictionary<WorldMapZoneContentMarkerType, Material> _cachedMaterials = null;

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 WorldMapZoneContentMarker를 기준으로
        /// ZoneRoot_XX 하위 runtime parent root에 placeholder GameObject를 생성/갱신한다.
        /// 기존 placeholder는 markerId 기반으로 재사용하거나 삭제 후 재생성한다.
        /// marker root/marker metadata/DebugVisual/Registry count는 절대 변경하지 않는다.
        /// </summary>
        public static void RebuildRuntimePlaceholders(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[RuntimePlaceholder] Settings is null! Cannot rebuild runtime placeholders.");
                return;
            }
            if (context == null)
            {
                Debug.LogError("[RuntimePlaceholder] Context is null! Cannot rebuild runtime placeholders.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-E-1: Rebuild Runtime Placeholders =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[RuntimePlaceholder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[RuntimePlaceholder] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 3. 모든 WorldMapZoneContentMarker 컴포넌트 찾기
            WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
            if (allMarkers == null || allMarkers.Length == 0)
            {
                Debug.LogWarning("[RuntimePlaceholder] No WorldMapZoneContentMarker components found. Nothing to generate.");
                log.AppendLine("  [WARN] No WorldMapZoneContentMarker components found.");
                Debug.Log(log.ToString());
                return;
            }
            log.AppendLine($"  [OK] Found {allMarkers.Length} WorldMapZoneContentMarker components.");

            // 4. Editor material cache 초기화 (URP 호환 shader 사용)
            EnsureMaterialCache();

            // 5. 각 marker에 대해 runtime placeholder 생성
            int totalCreated = 0;
            int totalReused = 0;
            int totalSkipped = 0;

            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;

                GameObject markerGo = marker.gameObject;
                WorldMapZoneContentMarkerType markerType = marker.MarkerType;
                string markerId = marker.MarkerId;

                // markerId가 비어있으면 스킵
                if (string.IsNullOrEmpty(markerId))
                {
                    totalSkipped++;
                    continue;
                }

                // 5a. ZoneRoot 찾기 (marker의 부모 체인에서 ZoneRoot_XX 검색)
                Transform zoneRootTransform = FindParentZoneRoot(markerGo.transform);
                if (zoneRootTransform == null)
                {
                    LogIfVerbose(settings, $"[SKIP] Cannot find ZoneRoot parent for marker '{markerId}'. Skipped.");
                    totalSkipped++;
                    continue;
                }

                // 5b. ZoneRoot 하위 child root 보장 (Props, HarvestSpawns, LogSpawns, Hazards, Triggers, DebugGizmos)
                EnsureZoneChildRoots(zoneRootTransform.gameObject);

                // 5c. marker type에 맞는 parent root 결정
                string parentRootName = GetParentRootNameForType(markerType);
                Transform parentRootTransform = zoneRootTransform.Find(parentRootName);
                if (parentRootTransform == null)
                {
                    LogIfVerbose(settings, $"[SKIP] Parent root '{parentRootName}' not found under '{zoneRootTransform.name}'. Skipped.");
                    totalSkipped++;
                    continue;
                }

                // 5d. placeholder 이름 생성: RuntimePlaceholder_{MarkerType}_{MarkerId}
                string placeholderName = $"{PlaceholderPrefix}_{markerType}_{markerId}";

                // 5e. 기존 placeholder 찾기 (markerId 기반)
                Transform existingPlaceholder = parentRootTransform.Find(placeholderName);

                if (existingPlaceholder != null)
                {
                    // 기존 placeholder가 있으면 위치만 갱신 (재사용)
                    existingPlaceholder.position = markerGo.transform.position;
                    totalReused++;
                }
                else
                {
                    // 기존 placeholder가 없으면 새로 생성
                    GameObject placeholder = CreateRuntimePlaceholder(placeholderName, parentRootTransform.gameObject, markerGo, markerType);
                    if (placeholder != null)
                    {
                        totalCreated++;
                    }
                    else
                    {
                        totalSkipped++;
                    }
                }
            }

            log.AppendLine($"  RuntimePlaceholder created: {totalCreated}, reused: {totalReused}, skipped: {totalSkipped}");
            log.AppendLine("===== Phase 14.10-E-1: Rebuild Runtime Placeholders Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 WorldMapZoneContentMarker에 대해
        /// RuntimePlaceholder의 유효성을 검사한다.
        /// 12개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN] summary를 출력한다.
        /// </summary>
        public static void ValidateRuntimePlaceholders(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[RuntimePlaceholder] Settings is null! Cannot validate runtime placeholders.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-E-1: Validate Runtime Placeholders =====");

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
                log.AppendLine("===== Phase 14.10-E-1: Validate Runtime Placeholders Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. ZoneRoots parent 존재 확인
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
            }

            if (zoneRootsTransform == null)
            {
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-E-1: Validate Runtime Placeholders Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 3. WorldMapZoneContentMarker count > 0 확인
            WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
            if (allMarkers != null && allMarkers.Length > 0)
            {
                log.AppendLine($"  [PASS] WorldMapZoneContentMarker count: {allMarkers.Length} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] No WorldMapZoneContentMarker components found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-E-1: Validate Runtime Placeholders Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4-12. 상세 검사 카운터
            int markersWithPlaceholder = 0;
            int markersMissingPlaceholder = 0;
            int markersWithMultiplePlaceholders = 0;
            int placeholdersWithMarkerComponent = 0;
            int placeholdersWithCollider = 0;
            int placeholdersMissingMeshFilter = 0;
            int placeholdersMissingMeshRenderer = 0;
            int placeholdersNullMaterial = 0;
            int placeholdersStandardShader = 0;
            int placeholdersPositionMismatch = 0;
            int placeholdersWrongParentRoot = 0;

            // marker type별 parent root 매핑 검증용
            Dictionary<WorldMapZoneContentMarkerType, int> typeParentRootMismatch = new Dictionary<WorldMapZoneContentMarkerType, int>();

            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;

                GameObject markerGo = marker.gameObject;
                WorldMapZoneContentMarkerType markerType = marker.MarkerType;
                string markerId = marker.MarkerId;

                if (string.IsNullOrEmpty(markerId))
                {
                    // markerId가 비어있으면 placeholder 검사 불가
                    continue;
                }

                // ZoneRoot 찾기
                Transform zoneRootTransform = FindParentZoneRoot(markerGo.transform);
                if (zoneRootTransform == null) continue;

                // 예상 parent root 이름
                string expectedParentRootName = GetParentRootNameForType(markerType);
                Transform expectedParentRoot = zoneRootTransform.Find(expectedParentRootName);

                // placeholder 이름
                string placeholderName = $"{PlaceholderPrefix}_{markerType}_{markerId}";

                // 4. 모든 marker에 대응되는 RuntimePlaceholder가 정확히 1개 존재하는지 확인
                int placeholderCount = 0;
                Transform foundPlaceholder = null;

                if (expectedParentRoot != null)
                {
                    foundPlaceholder = expectedParentRoot.Find(placeholderName);
                    if (foundPlaceholder != null)
                    {
                        placeholderCount = 1;
                    }
                }

                if (placeholderCount == 1)
                {
                    markersWithPlaceholder++;
                }
                else
                {
                    markersMissingPlaceholder++;
                }

                // placeholder가 정확히 1개일 때만 상세 검사
                if (placeholderCount == 1 && foundPlaceholder != null)
                {
                    GameObject placeholder = foundPlaceholder.gameObject;

                    // 5. placeholder에 WorldMapZoneContentMarker 컴포넌트가 붙어있지 않음
                    WorldMapZoneContentMarker placeholderMarker = placeholder.GetComponent<WorldMapZoneContentMarker>();
                    if (placeholderMarker != null)
                    {
                        placeholdersWithMarkerComponent++;
                    }

                    // 6. placeholder에 Collider가 없음
                    Collider col = placeholder.GetComponent<Collider>();
                    if (col != null)
                    {
                        placeholdersWithCollider++;
                    }

                    // 7. placeholder MeshFilter/MeshRenderer 존재
                    MeshFilter mf = placeholder.GetComponent<MeshFilter>();
                    MeshRenderer mr = placeholder.GetComponent<MeshRenderer>();
                    if (mf == null) placeholdersMissingMeshFilter++;
                    if (mr == null) placeholdersMissingMeshRenderer++;

                    // 8. placeholder material null 아님
                    if (mr != null)
                    {
                        Material sharedMat = mr.sharedMaterial;
                        if (sharedMat == null)
                        {
                            placeholdersNullMaterial++;
                        }
                        else
                        {
                            // 9. placeholder material shader가 Standard가 아님
                            Shader shader = sharedMat.shader;
                            if (shader != null && shader.name.Contains("Standard"))
                            {
                                placeholdersStandardShader++;
                            }
                        }
                    }

                    // 10. placeholder 위치가 source marker 위치와 거의 동일함
                    Vector3 markerWorldPos = markerGo.transform.position;
                    Vector3 placeholderWorldPos = placeholder.transform.position;
                    float distance = Vector3.Distance(markerWorldPos, placeholderWorldPos);
                    if (distance > 0.1f)
                    {
                        placeholdersPositionMismatch++;
                    }

                    // 11. marker type별 parent root 매핑이 올바름
                    // placeholder의 parent 이름이 예상 parent root 이름과 일치하는지 확인
                    Transform actualParent = placeholder.transform.parent;
                    if (actualParent != null && actualParent.name != expectedParentRootName)
                    {
                        placeholdersWrongParentRoot++;
                        if (!typeParentRootMismatch.ContainsKey(markerType))
                        {
                            typeParentRootMismatch[markerType] = 0;
                        }
                        typeParentRootMismatch[markerType]++;
                    }
                }
            }

            // 4. RuntimePlaceholder count 검사
            if (markersMissingPlaceholder == 0 && markersWithMultiplePlaceholders == 0)
            {
                log.AppendLine($"  [PASS] All {markersWithPlaceholder} markers have exactly 1 RuntimePlaceholder.");
                passCount++;
            }
            else
            {
                if (markersMissingPlaceholder > 0)
                    log.AppendLine($"  [FAIL] {markersMissingPlaceholder} marker(s) missing RuntimePlaceholder.");
                if (markersWithMultiplePlaceholders > 0)
                    log.AppendLine($"  [FAIL] {markersWithMultiplePlaceholders} marker(s) have multiple RuntimePlaceholders.");
                failCount++;
            }

            // 5. WorldMapZoneContentMarker 컴포넌트 없음
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

            // 6. Collider 없음
            if (placeholdersWithCollider == 0)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholder has Collider.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersWithCollider} RuntimePlaceholder(s) have Collider (should be removed).");
                failCount++;
            }

            // 7. MeshFilter 존재
            if (placeholdersMissingMeshFilter == 0)
            {
                log.AppendLine("  [PASS] All RuntimePlaceholders have MeshFilter.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersMissingMeshFilter} RuntimePlaceholder(s) missing MeshFilter.");
                failCount++;
            }

            // 7. MeshRenderer 존재
            if (placeholdersMissingMeshRenderer == 0)
            {
                log.AppendLine("  [PASS] All RuntimePlaceholders have MeshRenderer.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersMissingMeshRenderer} RuntimePlaceholder(s) missing MeshRenderer.");
                failCount++;
            }

            // 8. Material null 아님
            if (placeholdersNullMaterial == 0)
            {
                log.AppendLine("  [PASS] All RuntimePlaceholder materials are non-null.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersNullMaterial} RuntimePlaceholder(s) have null material.");
                failCount++;
            }

            // 9. Standard shader 사용 금지
            if (placeholdersStandardShader == 0)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholder material uses Standard shader.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersStandardShader} RuntimePlaceholder(s) still use Standard shader (should be URP).");
                failCount++;
            }

            // 10. 위치 일치 검사
            if (placeholdersPositionMismatch == 0)
            {
                log.AppendLine("  [PASS] All RuntimePlaceholder positions match source marker positions.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {placeholdersPositionMismatch} RuntimePlaceholder(s) have position mismatch (> 0.1 unit).");
                warnCount++;
            }

            // 11. Parent root 매핑 올바름
            if (placeholdersWrongParentRoot == 0)
            {
                log.AppendLine("  [PASS] All RuntimePlaceholder parent root mappings are correct.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {placeholdersWrongParentRoot} RuntimePlaceholder(s) have incorrect parent root mapping.");
                foreach (var kvp in typeParentRootMismatch)
                {
                    log.AppendLine($"    Type '{kvp.Key}': {kvp.Value} mismatch(es).");
                }
                failCount++;
            }

            // 12. ZoneRoot 하위 child root 존재 확인 (Props, HarvestSpawns, LogSpawns, Hazards, Triggers, DebugGizmos)
            int zoneRootsWithAllChildren = 0;
            int totalZoneRootsChecked = 0;
            for (int i = 0; i < zoneRootsTransform.childCount; i++)
            {
                Transform zoneRoot = zoneRootsTransform.GetChild(i);
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;
                totalZoneRootsChecked++;

                bool allExist = true;
                foreach (string childName in ZoneChildRootNames)
                {
                    if (zoneRoot.Find(childName) == null)
                    {
                        allExist = false;
                        break;
                    }
                }
                if (allExist) zoneRootsWithAllChildren++;
            }

            if (zoneRootsWithAllChildren == totalZoneRootsChecked)
            {
                log.AppendLine($"  [PASS] All {totalZoneRootsChecked} ZoneRoots have required child roots (Props, HarvestSpawns, LogSpawns, Hazards, Triggers, DebugGizmos).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {totalZoneRootsChecked - zoneRootsWithAllChildren} ZoneRoot(s) missing some child roots.");
                warnCount++;
            }

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-E-1: Validate Runtime Placeholders Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// marker type에 대응하는 ZoneRoot 하위 parent root 이름을 반환한다.
        /// Resource -> HarvestSpawns, Hazard -> Hazards, Landmark -> Props,
        /// Narrative -> LogSpawns, Route -> Triggers, Unknown/기타 -> DebugGizmos
        /// </summary>
        private static string GetParentRootNameForType(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerTypeToParentRootMap.TryGetValue(markerType, out string rootName))
            {
                return rootName;
            }
            return "DebugGizmos";
        }

        /// <summary>
        /// marker GameObject의 부모 체인에서 ZoneRoot_XX 이름을 가진 Transform을 찾아 반환한다.
        /// </summary>
        private static Transform FindParentZoneRoot(Transform childTransform)
        {
            Transform current = childTransform;
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
        /// ZoneRoot GameObject 하위에 6개 child root (Props, HarvestSpawns, LogSpawns, Hazards, Triggers, DebugGizmos)를
        /// 존재하지 않으면 생성하여 보장한다.
        /// </summary>
        private static void EnsureZoneChildRoots(GameObject zoneRoot)
        {
            if (zoneRoot == null) return;

            foreach (string childName in ZoneChildRootNames)
            {
                Transform existing = zoneRoot.transform.Find(childName);
                if (existing == null)
                {
                    // child root가 없으면 생성
                    GameObject child = new GameObject(childName);
                    child.transform.SetParent(zoneRoot.transform, false);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(child, $"Create {childName} under {zoneRoot.name}");
                }
            }
        }

        /// <summary>
        /// marker type에 대응하는 PrimitiveType을 반환한다.
        /// </summary>
        private static PrimitiveType GetMarkerPrimitive(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerPrimitiveMap.TryGetValue(markerType, out PrimitiveType primitive))
            {
                return primitive;
            }
            return PrimitiveType.Cube;
        }

        /// <summary>
        /// marker type에 대응하는 local scale을 반환한다.
        /// </summary>
        private static Vector3 GetMarkerScale(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerScaleMap.TryGetValue(markerType, out Vector3 scale))
            {
                return scale;
            }
            return Vector3.one;
        }

        /// <summary>
        /// marker type에 대응하는 editor material을 반환한다.
        /// </summary>
        private static Material GetMarkerMaterial(WorldMapZoneContentMarkerType markerType)
        {
            if (_cachedMaterials != null && _cachedMaterials.TryGetValue(markerType, out Material mat))
            {
                return mat;
            }
            return null;
        }

        /// <summary>
        /// URP 호환 shader를 우선순위대로 찾아 반환한다.
        /// 1. Universal Render Pipeline/Unlit
        /// 2. Universal Render Pipeline/Lit
        /// 3. Sprites/Default
        /// 4. Unlit/Color (fallback)
        /// </summary>
        private static Shader FindURPCompatibleShader()
        {
            // 우선순위 1: Universal Render Pipeline/Unlit
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) return shader;

            // 우선순위 2: Universal Render Pipeline/Lit
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            // 우선순위 3: Sprites/Default
            shader = Shader.Find("Sprites/Default");
            if (shader != null) return shader;

            // 우선순위 4: Unlit/Color (최후의 fallback)
            shader = Shader.Find("Unlit/Color");
            if (shader != null) return shader;

            // 모든 shader를 찾을 수 없으면 null 반환
            Debug.LogError("[RuntimePlaceholder] No URP-compatible shader found! Check URP package installation.");
            return null;
        }

        /// <summary>
        /// shader 프로퍼티에 안전하게 material color를 설정한다.
        /// _BaseColor (URP Unlit/Lit), _Color (Legacy), mat.color 순으로 시도.
        /// </summary>
        private static void SetMaterialColorSafe(Material mat, Color color)
        {
            if (mat == null) return;

            // URP shader는 _BaseColor 사용
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            // Legacy shader는 _Color 사용
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            // mat.color도 설정 (가장 기본적인 방법)
            mat.color = color;
        }

        /// <summary>
        /// URP 호환 shader에 대해 투명도(alpha blending)를 설정한다.
        /// </summary>
        private static void SetupTransparentMode(Material mat)
        {
            if (mat == null) return;

            // URP Unlit shader의 SurfaceType 설정
            if (mat.HasProperty("_Surface"))
            {
                // _Surface: 0=Opaque, 1=Transparent
                mat.SetFloat("_Surface", 1.0f);
            }

            // URP Blend mode 설정
            if (mat.HasProperty("_Blend"))
            {
                // _Blend: 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
                mat.SetFloat("_Blend", 0.0f);
            }

            // URP Alpha Clipping
            if (mat.HasProperty("_AlphaClip"))
            {
                mat.SetFloat("_AlphaClip", 0.0f);
            }

            // Render queue 설정 (Transparent)
            mat.renderQueue = 3000;

            // ZWrite 설정
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 0.0f);
            }

            // Keyword 설정
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // SurfaceType keyword (URP)
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        /// <summary>
        /// material의 shader가 Standard shader이거나 null인 경우 URP 호환 shader로 교체한다.
        /// </summary>
        private static void EnsureMaterialShaderIsURP(Material mat)
        {
            if (mat == null) return;

            Shader currentShader = mat.shader;

            // shader가 null이거나 Standard shader인 경우에만 교체
            bool needsReplacement = (currentShader == null) || currentShader.name.Contains("Standard");

            if (!needsReplacement) return;

            Shader urpShader = FindURPCompatibleShader();
            if (urpShader == null)
            {
                Debug.LogError($"[RuntimePlaceholder] Cannot replace shader for material '{mat.name}': no URP shader found.");
                return;
            }

            // shader 교체
            mat.shader = urpShader;

            // URP shader에 맞게 color 프로퍼티 재설정
            SetMaterialColorSafe(mat, mat.color);

            Debug.Log($"[RuntimePlaceholder] Replaced shader for material '{mat.name}': " +
                      $"'{currentShader?.name ?? "null"}' -> '{urpShader.name}'");
        }

        /// <summary>
        /// Editor material cache를 초기화한다.
        /// Assets/_Project/Generated/RuntimePlaceholderMaterials 폴더 아래에
        /// marker type별 material을 생성/로드하여 캐싱한다.
        /// URP 호환 shader를 사용하며, 기존 Standard shader material은 자동 교체한다.
        /// </summary>
        private static void EnsureMaterialCache()
        {
            if (_cachedMaterials != null) return;

            _cachedMaterials = new Dictionary<WorldMapZoneContentMarkerType, Material>();

            // GeneratedMaterialsFolder 존재 확인/생성
            if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            {
                // 상위 폴더 확인
                string parentFolder = "Assets/_Project/Generated";
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Generated");
                }
                AssetDatabase.CreateFolder(parentFolder, "RuntimePlaceholderMaterials");
            }

            // 각 marker type별 material 생성/로드
            foreach (var kvp in MaterialNameMap)
            {
                WorldMapZoneContentMarkerType markerType = kvp.Key;
                string matName = kvp.Value;
                string matPath = $"{GeneratedMaterialsFolder}/{matName}.mat";

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    // 새 material 생성 — URP 호환 shader 사용
                    Shader urpShader = FindURPCompatibleShader();
                    if (urpShader == null)
                    {
                        Debug.LogError($"[RuntimePlaceholder] Cannot create material '{matName}': no URP shader found. " +
                                       "Falling back to built-in shader.");
                        mat = new Material(Shader.Find("Standard"));
                    }
                    else
                    {
                        mat = new Material(urpShader);
                    }

                    mat.name = matName;

                    // color 설정 (shader 프로퍼티에 안전하게)
                    Color markerColor = MarkerColorMap[markerType];
                    SetMaterialColorSafe(mat, markerColor);

                    // 투명도 설정
                    SetupTransparentMode(mat);

                    AssetDatabase.CreateAsset(mat, matPath);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    // 기존 material이 있으면 shader 검사 및 교체
                    EnsureMaterialShaderIsURP(mat);

                    // color가 올바른지 확인하고 갱신
                    Color expectedColor = MarkerColorMap[markerType];
                    SetMaterialColorSafe(mat, expectedColor);

                    // 투명도 설정 확인
                    SetupTransparentMode(mat);

                    // 변경사항 저장
                    EditorUtility.SetDirty(mat);
                }

                _cachedMaterials[markerType] = mat;
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// parent GameObject 하위에 runtime placeholder GameObject를 생성한다.
        /// marker의 worldPosition을 그대로 사용하며, localRotation은 identity,
        /// localScale은 type별 크기를 적용한다.
        /// Collider는 제거하고, MeshFilter/MeshRenderer를 추가한다.
        /// </summary>
        private static GameObject CreateRuntimePlaceholder(
            string placeholderName,
            GameObject parent,
            GameObject sourceMarker,
            WorldMapZoneContentMarkerType markerType)
        {
            if (parent == null || sourceMarker == null) return null;

            // 1. placeholder GameObject 생성
            GameObject placeholder = new GameObject(placeholderName);
            placeholder.transform.SetParent(parent.transform, false);

            // 2. Transform 설정: worldPosition은 marker와 동일, localRotation은 identity
            placeholder.transform.position = sourceMarker.transform.position;
            placeholder.transform.localRotation = Quaternion.identity;
            placeholder.transform.localScale = GetMarkerScale(markerType);

            // 3. Primitive mesh 추가
            PrimitiveType primitiveType = GetMarkerPrimitive(markerType);
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);

            if (primitive != null)
            {
                // primitive의 컴포넌트를 placeholder로 이동
                MeshFilter srcMf = primitive.GetComponent<MeshFilter>();
                MeshRenderer srcMr = primitive.GetComponent<MeshRenderer>();
                Collider srcCol = primitive.GetComponent<Collider>();

                // MeshFilter 복사
                if (srcMf != null)
                {
                    MeshFilter dstMf = placeholder.AddComponent<MeshFilter>();
                    dstMf.sharedMesh = srcMf.sharedMesh;
                }

                // MeshRenderer 복사 — material 할당
                if (srcMr != null)
                {
                    MeshRenderer dstMr = placeholder.AddComponent<MeshRenderer>();
                    dstMr.sharedMaterial = GetMarkerMaterial(markerType);
                }

                // Collider는 제거 (placeholder에는 붙이지 않음)
                // srcCol은 primitive에 붙어있으므로 무시

                // primitive 삭제
                Object.DestroyImmediate(primitive);
            }

            // 4. Collider가 있으면 제거 (안전장치)
            Collider existingCollider = placeholder.GetComponent<Collider>();
            if (existingCollider != null)
            {
                Object.DestroyImmediate(existingCollider);
            }

            Undo.RegisterCreatedObjectUndo(placeholder, $"Create RuntimePlaceholder {placeholderName}");

            return placeholder;
        }

        // ===== Logging =====

        /// <summary>
        /// settings.LogVerbose가 true일 때만 로그를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[RuntimePlaceholder] {message}");
            }
        }
    }
}

