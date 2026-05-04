using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Gameplay.World.Content;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-M-3: Runtime Final Content Requirement Database의 각 entry에 대해,
    /// finalPrefab/finalProfile이 비어 있는 경우 category/finalContentKind별 자동 생성 placeholder final content asset을 생성하고 연결한다.
    /// 기존 사용자 연결값(finalPrefab/finalProfile이 이미 non-null)은 절대 덮어쓰지 않는다.
    /// Scene object는 절대 생성/삭제/이동/name 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentPlaceholderBindingUtility
    {
        private const string LogPrefix = "[FinalContentPlaceholderBinding]";
        private const string PrefabRootFolder = "Assets/_Project/Generated/RuntimeFinalContentPlaceholderPrefabs";
        private const string ProfileRootFolder = "Assets/_Project/Generated/RuntimeFinalContentPlaceholderProfiles";
        private const string MaterialRootFolder = "Assets/_Project/Generated/RuntimeFinalContentPlaceholderMaterials";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeFinalContentRequirementDatabase.asset";

        // ===== FinalContentKind -> Primitive Type Mapping =====
        private static readonly Dictionary<WorldMapRuntimeFinalContentKind, PrimitiveType> KindPrimitiveMap =
            new Dictionary<WorldMapRuntimeFinalContentKind, PrimitiveType>
            {
                { WorldMapRuntimeFinalContentKind.HarvestResource, PrimitiveType.Sphere },
                { WorldMapRuntimeFinalContentKind.HazardVolume, PrimitiveType.Cube },
                { WorldMapRuntimeFinalContentKind.LandmarkProp, PrimitiveType.Cylinder },
                { WorldMapRuntimeFinalContentKind.NarrativeLog, PrimitiveType.Capsule },
                { WorldMapRuntimeFinalContentKind.RouteTrigger, PrimitiveType.Cube },
                { WorldMapRuntimeFinalContentKind.DebugMarker, PrimitiveType.Cube },
                { WorldMapRuntimeFinalContentKind.Unknown, PrimitiveType.Cube },
            };

        // ===== FinalContentKind -> Local Scale Mapping =====
        private static readonly Dictionary<WorldMapRuntimeFinalContentKind, Vector3> KindScaleMap =
            new Dictionary<WorldMapRuntimeFinalContentKind, Vector3>
            {
                { WorldMapRuntimeFinalContentKind.HarvestResource, new Vector3(2f, 2f, 2f) },
                { WorldMapRuntimeFinalContentKind.HazardVolume, new Vector3(4f, 2f, 4f) },
                { WorldMapRuntimeFinalContentKind.LandmarkProp, new Vector3(5f, 5f, 5f) },
                { WorldMapRuntimeFinalContentKind.NarrativeLog, new Vector3(2f, 4f, 2f) },
                { WorldMapRuntimeFinalContentKind.RouteTrigger, new Vector3(5f, 1f, 5f) },
                { WorldMapRuntimeFinalContentKind.DebugMarker, new Vector3(2f, 2f, 2f) },
                { WorldMapRuntimeFinalContentKind.Unknown, new Vector3(2f, 2f, 2f) },
            };

        // ===== FinalContentKind -> Color Mapping =====
        private static readonly Dictionary<WorldMapRuntimeFinalContentKind, Color> KindColorMap =
            new Dictionary<WorldMapRuntimeFinalContentKind, Color>
            {
                { WorldMapRuntimeFinalContentKind.HarvestResource, Color.green },
                { WorldMapRuntimeFinalContentKind.HazardVolume, Color.red },
                { WorldMapRuntimeFinalContentKind.LandmarkProp, new Color(1f, 0.5f, 0f) }, // orange
                { WorldMapRuntimeFinalContentKind.NarrativeLog, Color.cyan },
                { WorldMapRuntimeFinalContentKind.RouteTrigger, Color.white },
                { WorldMapRuntimeFinalContentKind.DebugMarker, Color.magenta },
                { WorldMapRuntimeFinalContentKind.Unknown, Color.gray },
            };

        // ===== Public API =====

        /// <summary>
        /// M-2 Requirement Database의 모든 entry를 순회하며,
        /// finalPrefab/finalProfile이 null인 entry에 대해 generated placeholder prefab/profile을 생성/갱신하고 연결한다.
        /// 기존 사용자 연결값은 절대 덮어쓰지 않는다.
        /// Scene object는 절대 생성/삭제/이동/name 변경하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings (null 허용, 로그 레벨 제어용)</param>
        /// <param name="context">AutoBuilder scene context (null 허용, 현재는 사용하지 않음)</param>
        public static void CreateOrUpdateRuntimeFinalContentPlaceholderAssets(
            object settings,
            object context)
        {
            // StringBuilder로 로그를 누적하여 마지막에 한 번만 출력
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-3: Create/Update Runtime Final Content Placeholder Assets =====");

            // 1. Requirement Database 로드
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentRequirementDatabaseSO>(DatabaseAssetPath);
            if (database == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Requirement Database not found at: {DatabaseAssetPath}");
                Debug.LogError(logBuilder.ToString());
                return;
            }

            var entries = database.Entries;
            if (entries == null || entries.Count == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Requirement Database has no entries.");
                Debug.LogError(logBuilder.ToString());
                return;
            }

            // 2. Generated 폴더 확인/생성
            EnsureFolderExists(PrefabRootFolder);
            EnsureFolderExists(ProfileRootFolder);
            EnsureFolderExists(MaterialRootFolder);

            // 3. 통계 변수
            int totalEntries = entries.Count;
            int createdPrefabCount = 0;
            int updatedPrefabCount = 0;
            int linkedPrefabCount = 0;
            int createdProfileCount = 0;
            int updatedProfileCount = 0;
            int linkedProfileCount = 0;
            int preservedUserPrefabCount = 0;
            int preservedUserProfileCount = 0;
            int skippedNoChangePrefab = 0;
            int skippedNoChangeProfile = 0;

            // 4. 모든 entry 순회
            foreach (WorldMapRuntimeFinalContentRequirementEntry entry in entries)
            {
                if (entry == null) continue;

                string sanitizedId = SanitizeRequirementId(entry.RequirementId);
                if (string.IsNullOrEmpty(sanitizedId))
                {
                    logBuilder.AppendLine($"{LogPrefix} [SKIP] Entry with empty/null requirementId after sanitization. Skipping.");
                    continue;
                }

                // ===== FinalPrefab 처리 =====
                if (entry.FinalPrefab == null)
                {
                    // finalPrefab이 null이면 generated placeholder prefab 생성/갱신
                    string prefabPath = $"{PrefabRootFolder}/FinalContentPlaceholder_{sanitizedId}.prefab";
                    GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (existingPrefab == null)
                    {
                        // 새로 생성
                        GameObject prefabInstance = CreatePlaceholderPrefabInstance(entry, sanitizedId);
                        if (prefabInstance != null)
                        {
                            // Prefab 저장
                            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                            GameObject.DestroyImmediate(prefabInstance);

                            if (prefabAsset != null)
                            {
                                entry.FinalPrefab = prefabAsset;
                                createdPrefabCount++;
                                linkedPrefabCount++;
                                logBuilder.AppendLine($"{LogPrefix} [CREATE] Created and linked placeholder prefab: {prefabPath}");
                            }
                            else
                            {
                                logBuilder.AppendLine($"{LogPrefix} [FAIL] Failed to save prefab asset: {prefabPath}");
                            }
                        }
                    }
                    else
                    {
                        // 기존 generated prefab이 있으면 갱신
                        bool updated = UpdatePlaceholderPrefab(existingPrefab, entry, sanitizedId);
                        if (updated)
                        {
                            updatedPrefabCount++;
                            logBuilder.AppendLine($"{LogPrefix} [UPDATE] Updated existing placeholder prefab: {prefabPath}");
                        }
                        else
                        {
                            skippedNoChangePrefab++;
                        }

                        // 연결 (이미 연결되어 있을 수 있지만, null이므로 연결)
                        entry.FinalPrefab = existingPrefab;
                        linkedPrefabCount++;
                    }
                }
                else
                {
                    // finalPrefab이 이미 non-null이면 preserve
                    preservedUserPrefabCount++;
                    logBuilder.AppendLine($"{LogPrefix} [PRESERVE] User-assigned finalPrefab preserved: {entry.FinalPrefab.name} (entry: {entry.RequirementId})");
                }

                // ===== FinalProfile 처리 =====
                if (entry.FinalProfile == null)
                {
                    // finalProfile이 null이면 generated placeholder profile 생성/갱신
                    string profilePath = $"{ProfileRootFolder}/FinalContentPlaceholderProfile_{sanitizedId}.asset";
                    WorldMapRuntimeFinalContentPlaceholderProfileSO existingProfile =
                        AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentPlaceholderProfileSO>(profilePath);

                    if (existingProfile == null)
                    {
                        // 새로 생성
                        WorldMapRuntimeFinalContentPlaceholderProfileSO profile = ScriptableObject.CreateInstance<WorldMapRuntimeFinalContentPlaceholderProfileSO>();
                        profile.Configure(entry);
                        AssetDatabase.CreateAsset(profile, profilePath);
                        entry.FinalProfile = profile;
                        createdProfileCount++;
                        linkedProfileCount++;
                        logBuilder.AppendLine($"{LogPrefix} [CREATE] Created and linked placeholder profile: {profilePath}");
                    }
                    else
                    {
                        // 기존 generated profile이 있으면 갱신
                        existingProfile.Configure(entry);
                        EditorUtility.SetDirty(existingProfile);
                        updatedProfileCount++;
                        linkedProfileCount++;
                        logBuilder.AppendLine($"{LogPrefix} [UPDATE] Updated existing placeholder profile: {profilePath}");

                        // 연결
                        entry.FinalProfile = existingProfile;
                    }
                }
                else
                {
                    // finalProfile이 이미 non-null이면 preserve
                    preservedUserProfileCount++;
                    logBuilder.AppendLine($"{LogPrefix} [PRESERVE] User-assigned finalProfile preserved: {entry.FinalProfile.name} (entry: {entry.RequirementId})");
                }
            }

            // 5. DB 변경 사항 저장
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. 결과 로그 출력 (StringBuilder로 한 번에)
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-3: Create/Update Complete =====");
            logBuilder.AppendLine($"{LogPrefix} Total entries: {totalEntries}");
            logBuilder.AppendLine($"{LogPrefix} Created prefabs: {createdPrefabCount}");
            logBuilder.AppendLine($"{LogPrefix} Updated prefabs: {updatedPrefabCount}");
            logBuilder.AppendLine($"{LogPrefix} Linked prefabs: {linkedPrefabCount}");
            logBuilder.AppendLine($"{LogPrefix} Created profiles: {createdProfileCount}");
            logBuilder.AppendLine($"{LogPrefix} Updated profiles: {updatedProfileCount}");
            logBuilder.AppendLine($"{LogPrefix} Linked profiles: {linkedProfileCount}");
            logBuilder.AppendLine($"{LogPrefix} Preserved user prefabs: {preservedUserPrefabCount}");
            logBuilder.AppendLine($"{LogPrefix} Preserved user profiles: {preservedUserProfileCount}");
            logBuilder.AppendLine($"{LogPrefix} Skipped prefab update (no change): {skippedNoChangePrefab}");
            logBuilder.AppendLine($"{LogPrefix} Skipped profile update (no change): {skippedNoChangeProfile}");
            Debug.Log(logBuilder.ToString());
        }

        /// <summary>
        /// M-3 단계에서 생성된 Runtime Final Content Placeholder Asset의 유효성을 검사한다.
        /// 최소 20개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Scene object는 절대 생성/삭제/이동/name 변경하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings (null 허용, 로그 레벨 제어용)</param>
        /// <param name="context">AutoBuilder scene context (null 허용, 현재는 사용하지 않음)</param>
        public static void ValidateRuntimeFinalContentPlaceholderAssets(
            object settings,
            object context)
        {
            Debug.Log($"{LogPrefix} ===== Phase 14.10-M-3: Validate Runtime Final Content Placeholder Assets =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 검사 결과 기록용
            var results = new List<string>();

            // 통계 변수
            int totalEntries = 0;
            int missingFinalPrefabCount = 0;
            int missingFinalProfileCount = 0;
            int preservedUserPrefabCount = 0;
            int preservedUserProfileCount = 0;
            int generatedPrefabCount = 0;
            int generatedProfileCount = 0;
            int fallbackEntryCount = 0;
            int exactEntryCount = 0;

            // finalContentKind별 breakdown
            var kindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var kindConfiguredBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();

            // 1. Requirement Database asset exists
            totalChecks++;
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentRequirementDatabaseSO>(DatabaseAssetPath);
            if (database != null)
            {
                results.Add($"[PASS] Check 1: Requirement Database asset exists at {DatabaseAssetPath}");
                passCount++;
            }
            else
            {
                results.Add($"[FAIL] Check 1: Requirement Database asset NOT found at {DatabaseAssetPath}");
                failCount++;
            }

            if (database == null)
            {
                // Database가 없으면 더 이상 검사 불가
                results.Add("[FAIL] Validation aborted: Requirement Database is null.");
                failCount++;
                LogValidationSummary(results, passCount, failCount, warnCount, infoCount, totalChecks,
                    totalEntries, missingFinalPrefabCount, missingFinalProfileCount,
                    preservedUserPrefabCount, preservedUserProfileCount,
                    generatedPrefabCount, generatedProfileCount,
                    fallbackEntryCount, exactEntryCount, kindBreakdown, kindConfiguredBreakdown);
                return;
            }

            var entries = database.Entries;

            // 2. Entries count > 0
            totalChecks++;
            if (entries != null && entries.Count > 0)
            {
                results.Add($"[PASS] Check 2: Entries count = {entries.Count} (> 0)");
                passCount++;
                totalEntries = entries.Count;
            }
            else
            {
                results.Add($"[FAIL] Check 2: Entries count = {(entries == null ? 0 : entries.Count)} (expected > 0)");
                failCount++;
            }

            if (entries == null || entries.Count == 0)
            {
                results.Add("[FAIL] Validation aborted: No entries to validate.");
                failCount++;
                LogValidationSummary(results, passCount, failCount, warnCount, infoCount, totalChecks,
                    totalEntries, missingFinalPrefabCount, missingFinalProfileCount,
                    preservedUserPrefabCount, preservedUserProfileCount,
                    generatedPrefabCount, generatedProfileCount,
                    fallbackEntryCount, exactEntryCount, kindBreakdown, kindConfiguredBreakdown);
                return;
            }

            // 3. 모든 entry requirementId non-empty
            totalChecks++;
            bool allIdsNonEmpty = true;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.RequirementId))
                {
                    allIdsNonEmpty = false;
                    break;
                }
            }
            if (allIdsNonEmpty)
            {
                results.Add($"[PASS] Check 3: All {entries.Count} entries have non-empty requirementId");
                passCount++;
            }
            else
            {
                results.Add("[FAIL] Check 3: Some entries have empty/null requirementId");
                failCount++;
            }

            // 4. 모든 entry finalContentKind != None
            totalChecks++;
            bool allKindsValid = true;
            foreach (var entry in entries)
            {
                if (entry == null || entry.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                {
                    allKindsValid = false;
                    break;
                }
            }
            if (allKindsValid)
            {
                results.Add($"[PASS] Check 4: All entries have valid FinalContentKind (not None)");
                passCount++;
            }
            else
            {
                results.Add("[FAIL] Check 4: Some entries have FinalContentKind == None");
                failCount++;
            }

            // 5-20: 각 entry별 상세 검사
            int entryIndex = 0;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                entryIndex++;

                string sanitizedId = SanitizeRequirementId(entry.RequirementId);
                string prefabPath = $"{PrefabRootFolder}/FinalContentPlaceholder_{sanitizedId}.prefab";
                string profilePath = $"{ProfileRootFolder}/FinalContentPlaceholderProfile_{sanitizedId}.asset";

                // finalContentKind breakdown
                if (!kindBreakdown.ContainsKey(entry.FinalContentKind))
                    kindBreakdown[entry.FinalContentKind] = 0;
                kindBreakdown[entry.FinalContentKind]++;

                // fallback/exact count
                if (entry.IsFallbackRequirement)
                    fallbackEntryCount++;
                else
                    exactEntryCount++;

                // 5. 모든 entry finalPrefab non-null
                if (entryIndex == 1) // 한 번만 체크
                {
                    totalChecks++;
                }
                if (entry.FinalPrefab == null)
                {
                    if (entryIndex == 1) // 첫 번째 missing에서만 FAIL 카운트
                    {
                        results.Add($"[FAIL] Check 5: Some entries have null FinalPrefab (entry: {entry.RequirementId})");
                        failCount++;
                    }
                    missingFinalPrefabCount++;
                }
                else
                {
                    if (entryIndex == 1)
                    {
                        // 모든 entry가 non-null이면 PASS
                        bool allPrefabNonNull = true;
                        foreach (var e in entries)
                        {
                            if (e != null && e.FinalPrefab == null) { allPrefabNonNull = false; break; }
                        }
                        if (allPrefabNonNull)
                        {
                            results.Add($"[PASS] Check 5: All entries have non-null FinalPrefab");
                            passCount++;
                        }
                    }
                }

                // 6. 모든 entry finalProfile non-null
                if (entryIndex == 1)
                {
                    totalChecks++;
                }
                if (entry.FinalProfile == null)
                {
                    if (entryIndex == 1)
                    {
                        results.Add($"[FAIL] Check 6: Some entries have null FinalProfile (entry: {entry.RequirementId})");
                        failCount++;
                    }
                    missingFinalProfileCount++;
                }
                else
                {
                    if (entryIndex == 1)
                    {
                        bool allProfileNonNull = true;
                        foreach (var e in entries)
                        {
                            if (e != null && e.FinalProfile == null) { allProfileNonNull = false; break; }
                        }
                        if (allProfileNonNull)
                        {
                            results.Add($"[PASS] Check 6: All entries have non-null FinalProfile");
                            passCount++;
                        }
                    }
                }

                // FinalPrefab 검사
                if (entry.FinalPrefab != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(entry.FinalPrefab);
                    bool isGenerated = assetPath != null && assetPath.StartsWith("Assets/_Project/Generated/");

                    if (isGenerated)
                    {
                        generatedPrefabCount++;

                        // 7. generated placeholder prefab에 WorldMapRuntimeFinalContentPlaceholderTag 존재
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        WorldMapRuntimeFinalContentPlaceholderTag tag = entry.FinalPrefab.GetComponent<WorldMapRuntimeFinalContentPlaceholderTag>();
                        if (tag != null)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 7: Generated prefabs have WorldMapRuntimeFinalContentPlaceholderTag");
                                passCount++;
                            }

                            // 8. tag.IsGeneratedPlaceholder == true
                            if (entryIndex == 1)
                            {
                                totalChecks++;
                            }
                            if (tag.IsGeneratedPlaceholder)
                            {
                                if (entryIndex == 1)
                                {
                                    results.Add($"[PASS] Check 8: Generated prefab tag.IsGeneratedPlaceholder == true");
                                    passCount++;
                                }
                            }
                            else
                            {
                                if (entryIndex == 1)
                                {
                                    results.Add($"[FAIL] Check 8: Generated prefab tag.IsGeneratedPlaceholder == false (entry: {entry.RequirementId})");
                                    failCount++;
                                }
                            }

                            // 9. tag.RequirementId == entry.RequirementId
                            if (entryIndex == 1)
                            {
                                totalChecks++;
                            }
                            if (string.Equals(tag.RequirementId, entry.RequirementId, StringComparison.Ordinal))
                            {
                                if (entryIndex == 1)
                                {
                                    results.Add($"[PASS] Check 9: Generated prefab tag.RequirementId matches entry.RequirementId");
                                    passCount++;
                                }
                            }
                            else
                            {
                                if (entryIndex == 1)
                                {
                                    results.Add($"[FAIL] Check 9: Generated prefab tag.RequirementId mismatch (tag={tag.RequirementId}, entry={entry.RequirementId})");
                                    failCount++;
                                }
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 7: Generated prefab missing WorldMapRuntimeFinalContentPlaceholderTag (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }

                        // 10. generated placeholder prefab에 Collider 없음
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        Collider collider = entry.FinalPrefab.GetComponent<Collider>();
                        if (collider == null)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 10: Generated prefabs have no Collider");
                                passCount++;
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 10: Generated prefab has Collider (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }

                        // 11. generated placeholder prefab에 WorldMapRuntimePlaceholderBinding 없음
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        var placeholderBinding = entry.FinalPrefab.GetComponent<WorldMapRuntimePlaceholderBinding>();
                        if (placeholderBinding == null)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 11: Generated prefabs have no WorldMapRuntimePlaceholderBinding");
                                passCount++;
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 11: Generated prefab has WorldMapRuntimePlaceholderBinding (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }

                        // 12. generated placeholder prefab에 WorldMapZoneContentMarker 없음
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        var contentMarker = entry.FinalPrefab.GetComponent<WorldMapZoneContentMarker>();
                        if (contentMarker == null)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 12: Generated prefabs have no WorldMapZoneContentMarker");
                                passCount++;
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 12: Generated prefab has WorldMapZoneContentMarker (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }

                        // 13. generated placeholder prefab에 WorldMapRuntimeSpawnInstanceTag 없음
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        var spawnInstanceTag = entry.FinalPrefab.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                        if (spawnInstanceTag == null)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 13: Generated prefabs have no WorldMapRuntimeSpawnInstanceTag");
                                passCount++;
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 13: Generated prefab has WorldMapRuntimeSpawnInstanceTag (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }

                        // 14. generated placeholder prefab에 WorldMapRuntimeSpawnGameplayAdapter 없음
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        var spawnAdapter = entry.FinalPrefab.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                        if (spawnAdapter == null)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 14: Generated prefabs have no WorldMapRuntimeSpawnGameplayAdapter");
                                passCount++;
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 14: Generated prefab has WorldMapRuntimeSpawnGameplayAdapter (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }
                    }
                    else
                    {
                        // Generated 폴더 밖의 asset = 사용자 real prefab
                        preservedUserPrefabCount++;
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                            results.Add($"[INFO] Check 7: User real prefab detected (outside Generated folder): {assetPath} (entry: {entry.RequirementId})");
                            infoCount++;
                        }
                    }
                }

                // FinalProfile 검사
                if (entry.FinalProfile != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(entry.FinalProfile);
                    bool isGenerated = assetPath != null && assetPath.StartsWith("Assets/_Project/Generated/");

                    if (isGenerated)
                    {
                        generatedProfileCount++;

                        // 15. generated profile이 WorldMapRuntimeFinalContentPlaceholderProfileSO 타입 확인
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                        }
                        if (entry.FinalProfile is WorldMapRuntimeFinalContentPlaceholderProfileSO)
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[PASS] Check 15: Generated profiles are WorldMapRuntimeFinalContentPlaceholderProfileSO type");
                                passCount++;
                            }

                            // 16. profile.RequirementId == entry.RequirementId
                            if (entryIndex == 1)
                            {
                                totalChecks++;
                            }
                            var profile = entry.FinalProfile as WorldMapRuntimeFinalContentPlaceholderProfileSO;
                            if (profile != null && string.Equals(profile.RequirementId, entry.RequirementId, StringComparison.Ordinal))
                            {
                                if (entryIndex == 1)
                                {
                                    results.Add($"[PASS] Check 16: Generated profile RequirementId matches entry.RequirementId");
                                    passCount++;
                                }
                            }
                            else
                            {
                                if (entryIndex == 1)
                                {
                                    results.Add($"[FAIL] Check 16: Generated profile RequirementId mismatch (entry: {entry.RequirementId})");
                                    failCount++;
                                }
                            }
                        }
                        else
                        {
                            if (entryIndex == 1)
                            {
                                results.Add($"[FAIL] Check 15: Generated profile is not WorldMapRuntimeFinalContentPlaceholderProfileSO type (entry: {entry.RequirementId})");
                                failCount++;
                            }
                        }
                    }
                    else
                    {
                        // Generated 폴더 밖의 asset = 사용자 real profile
                        preservedUserProfileCount++;
                        if (entryIndex == 1)
                        {
                            totalChecks++;
                            results.Add($"[INFO] Check 15: User real profile detected (outside Generated folder): {assetPath} (entry: {entry.RequirementId})");
                            infoCount++;
                        }
                    }
                }

                // kind breakdown for configured entries
                if (entry.FinalPrefab != null && entry.FinalProfile != null)
                {
                    if (!kindConfiguredBreakdown.ContainsKey(entry.FinalContentKind))
                        kindConfiguredBreakdown[entry.FinalContentKind] = 0;
                    kindConfiguredBreakdown[entry.FinalContentKind]++;
                }
            }

            // 17. finalContentKind별 total/configured/missing breakdown 출력
            totalChecks++;
            StringBuilder kindBreakdownStr = new StringBuilder();
            kindBreakdownStr.AppendLine($"[INFO] Check 17: FinalContentKind breakdown:");
            foreach (WorldMapRuntimeFinalContentKind kind in Enum.GetValues(typeof(WorldMapRuntimeFinalContentKind)))
            {
                if (kind == WorldMapRuntimeFinalContentKind.None) continue;
                int total = kindBreakdown.ContainsKey(kind) ? kindBreakdown[kind] : 0;
                int configured = kindConfiguredBreakdown.ContainsKey(kind) ? kindConfiguredBreakdown[kind] : 0;
                int missing = total - configured;
                kindBreakdownStr.AppendLine($"  {kind}: total={total}, configured={configured}, missing={missing}");
            }
            results.Add(kindBreakdownStr.ToString().TrimEnd());
            infoCount++;

            // 18. fallback/exact entry breakdown 출력
            totalChecks++;
            results.Add($"[INFO] Check 18: Entry breakdown - Exact: {exactEntryCount}, Fallback: {fallbackEntryCount}");
            infoCount++;

            // 19. user-assigned final asset preserve count 출력
            totalChecks++;
            results.Add($"[INFO] Check 19: User-assigned final assets preserved - Prefabs: {preservedUserPrefabCount}, Profiles: {preservedUserProfileCount}");
            infoCount++;

            // 20. generated final asset count 출력
            totalChecks++;
            results.Add($"[INFO] Check 20: Generated final assets - Prefabs: {generatedPrefabCount}, Profiles: {generatedProfileCount}");
            infoCount++;

            // 최종 요약
            LogValidationSummary(results, passCount, failCount, warnCount, infoCount, totalChecks,
                totalEntries, missingFinalPrefabCount, missingFinalProfileCount,
                preservedUserPrefabCount, preservedUserProfileCount,
                generatedPrefabCount, generatedProfileCount,
                fallbackEntryCount, exactEntryCount, kindBreakdown, kindConfiguredBreakdown);
        }

        // ===== Private Helpers =====

        /// <summary>
        /// requirementId에서 파일명으로 사용할 수 없는 문자를 제거한다.
        /// </summary>
        private static string SanitizeRequirementId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            // 파일명으로 사용할 수 없는 문자를 _로 치환
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(id);
            for (int i = 0; i < sb.Length; i++)
            {
                if (Array.IndexOf(invalidChars, sb[i]) >= 0)
                {
                    sb[i] = '_';
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 지정된 경로에 폴더가 없으면 생성한다.
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                // 상위 폴더부터 순차적으로 생성
                string parent = System.IO.Path.GetDirectoryName(folderPath).Replace("\\", "/");
                string folderName = System.IO.Path.GetFileName(folderPath);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolderExists(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log($"{LogPrefix} Created folder: {folderPath}");
            }
        }

        /// <summary>
        /// requirement entry 정보를 기반으로 placeholder prefab 인스턴스를 생성한다.
        /// Collider는 제거한다. Renderer material은 URP Unlit 우선 사용.
        /// </summary>
        private static GameObject CreatePlaceholderPrefabInstance(WorldMapRuntimeFinalContentRequirementEntry entry, string sanitizedId)
        {
            if (entry == null) return null;

            // 1. Primitive GameObject 생성
            PrimitiveType primType = GetPrimitiveType(entry.FinalContentKind);
            GameObject go = GameObject.CreatePrimitive(primType);
            go.name = $"FinalContentPlaceholder_{sanitizedId}";

            // 2. Scale 설정
            Vector3 scale = GetScale(entry.FinalContentKind);
            go.transform.localScale = scale;

            // 3. Collider 제거
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                GameObject.DestroyImmediate(collider);
            }

            // 4. Material 설정 (URP Unlit 우선)
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = GetColor(entry.FinalContentKind);
                Material mat = GetOrCreateMaterial(entry.FinalContentKind, color);
                if (mat != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }

            // 5. WorldMapRuntimeFinalContentPlaceholderTag 부착
            WorldMapRuntimeFinalContentPlaceholderTag tag = go.AddComponent<WorldMapRuntimeFinalContentPlaceholderTag>();
            tag.Configure(entry);

            return go;
        }

        /// <summary>
        /// 기존 generated placeholder prefab을 entry 정보로 갱신한다.
        /// 변경이 필요하면 true를 반환한다.
        /// </summary>
        private static bool UpdatePlaceholderPrefab(GameObject prefab, WorldMapRuntimeFinalContentRequirementEntry entry, string sanitizedId)
        {
            if (prefab == null || entry == null) return false;

            bool changed = false;

            // Tag 갱신
            WorldMapRuntimeFinalContentPlaceholderTag tag = prefab.GetComponent<WorldMapRuntimeFinalContentPlaceholderTag>();
            if (tag == null)
            {
                tag = prefab.AddComponent<WorldMapRuntimeFinalContentPlaceholderTag>();
                changed = true;
            }

            // Tag의 기존 값과 비교하여 변경이 필요한 경우만 갱신
            if (!string.Equals(tag.RequirementId, entry.RequirementId, StringComparison.Ordinal) ||
                tag.FinalContentKind != entry.FinalContentKind ||
                tag.AdapterKind != entry.AdapterKind ||
                !string.Equals(tag.RuntimeCategory, entry.RuntimeCategory ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(tag.RuntimeKey, entry.RuntimeKey ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(tag.ProfileId, entry.ProfileId ?? string.Empty, StringComparison.Ordinal) ||
                tag.IsFallbackRequirement != entry.IsFallbackRequirement ||
                !string.Equals(tag.DisplayName, entry.DisplayName ?? string.Empty, StringComparison.Ordinal))
            {
                tag.Configure(entry);
                changed = true;
            }

            // Scale 갱신
            Vector3 expectedScale = GetScale(entry.FinalContentKind);
            if (prefab.transform.localScale != expectedScale)
            {
                prefab.transform.localScale = expectedScale;
                changed = true;
            }

            // Material 갱신
            Renderer renderer = prefab.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color expectedColor = GetColor(entry.FinalContentKind);
                Material expectedMat = GetOrCreateMaterial(entry.FinalContentKind, expectedColor);
                if (expectedMat != null && renderer.sharedMaterial != expectedMat)
                {
                    renderer.sharedMaterial = expectedMat;
                    changed = true;
                }
            }

            // Collider 제거 확인
            Collider collider = prefab.GetComponent<Collider>();
            if (collider != null)
            {
                GameObject.DestroyImmediate(collider, true);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
            }

            return changed;
        }

        /// <summary>
        /// FinalContentKind에 대응하는 PrimitiveType을 반환한다.
        /// </summary>
        private static PrimitiveType GetPrimitiveType(WorldMapRuntimeFinalContentKind kind)
        {
            if (KindPrimitiveMap.TryGetValue(kind, out PrimitiveType primType))
            {
                return primType;
            }
            return PrimitiveType.Cube;
        }

        /// <summary>
        /// FinalContentKind에 대응하는 Local Scale을 반환한다.
        /// </summary>
        private static Vector3 GetScale(WorldMapRuntimeFinalContentKind kind)
        {
            if (KindScaleMap.TryGetValue(kind, out Vector3 scale))
            {
                return scale;
            }
            return Vector3.one;
        }

        /// <summary>
        /// FinalContentKind에 대응하는 Color를 반환한다.
        /// </summary>
        private static Color GetColor(WorldMapRuntimeFinalContentKind kind)
        {
            if (KindColorMap.TryGetValue(kind, out Color color))
            {
                return color;
            }
            return Color.gray;
        }

        /// <summary>
        /// FinalContentKind에 대응하는 Material을 찾거나 생성한다.
        /// URP Unlit shader를 우선 사용한다.
        /// </summary>
        private static Material GetOrCreateMaterial(WorldMapRuntimeFinalContentKind kind, Color color)
        {
            string materialName = $"RuntimeFinalContentPlaceholder_{kind}_Mat";
            string materialPath = $"{MaterialRootFolder}/{materialName}.mat";

            // 기존 Material 확인
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existingMat != null)
            {
                return existingMat;
            }

            // 새 Material 생성
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            Material newMat;
            if (unlitShader != null)
            {
                newMat = new Material(unlitShader);
            }
            else
            {
                // URP Unlit이 없으면 기본 Unlit/Color 사용
                Shader fallbackShader = Shader.Find("Unlit/Color");
                if (fallbackShader != null)
                {
                    newMat = new Material(fallbackShader);
                }
                else
                {
                    newMat = new Material(Shader.Find("Standard"));
                }
            }

            newMat.name = materialName;
            newMat.color = color;

            // Material 저장
            AssetDatabase.CreateAsset(newMat, materialPath);
            Debug.Log($"{LogPrefix} Created material: {materialPath}");

            return newMat;
        }

        /// <summary>
        /// Validation 결과를 Console에 요약 출력한다.
        /// StringBuilder로 하나의 로그로 누적하여 출력한다.
        /// </summary>
        private static void LogValidationSummary(
            List<string> results,
            int passCount, int failCount, int warnCount, int infoCount, int totalChecks,
            int totalEntries,
            int missingFinalPrefabCount, int missingFinalProfileCount,
            int preservedUserPrefabCount, int preservedUserProfileCount,
            int generatedPrefabCount, int generatedProfileCount,
            int fallbackEntryCount, int exactEntryCount,
            Dictionary<WorldMapRuntimeFinalContentKind, int> kindBreakdown,
            Dictionary<WorldMapRuntimeFinalContentKind, int> kindConfiguredBreakdown)
        {
            // StringBuilder로 모든 로그를 누적하여 마지막에 한 번만 출력
            StringBuilder summaryBuilder = new StringBuilder();

            // 모든 검사 결과 출력
            foreach (string result in results)
            {
                summaryBuilder.AppendLine($"{LogPrefix} {result}");
            }

            // 최종 요약
            summaryBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-3: Validation Summary =====");
            summaryBuilder.AppendLine($"{LogPrefix} Total checks: {totalChecks} | [PASS] {passCount} | [FAIL] {failCount} | [WARN] {warnCount} | [INFO] {infoCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Total entries: {totalEntries}");
            summaryBuilder.AppendLine($"{LogPrefix} Missing FinalPrefab: {missingFinalPrefabCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Missing FinalProfile: {missingFinalProfileCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Preserved user prefabs: {preservedUserPrefabCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Preserved user profiles: {preservedUserProfileCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Generated prefabs: {generatedPrefabCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Generated profiles: {generatedProfileCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Exact entries: {exactEntryCount}");
            summaryBuilder.AppendLine($"{LogPrefix} Fallback entries: {fallbackEntryCount}");

            if (failCount > 0)
            {
                summaryBuilder.AppendLine($"{LogPrefix} [FAIL] Validation completed with {failCount} failure(s). Review logs above.");
                Debug.LogError(summaryBuilder.ToString());
            }
            else
            {
                summaryBuilder.AppendLine($"{LogPrefix} [PASS] All checks passed.");
                Debug.Log(summaryBuilder.ToString());
            }
        }
    }
}


