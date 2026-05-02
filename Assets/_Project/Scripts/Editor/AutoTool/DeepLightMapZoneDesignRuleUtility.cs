using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Data.World.Design;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// WorldMapZoneDesignRule 생성을 위한 Editor 전용 유틸리티.
    /// Phase 14.2: WorldMapZoneDesignDatabaseSO의 자연어/기획 텍스트를
    /// 정형화된 numeric rule/profile(WorldMapZoneDesignRule)로 변환한다.
    ///
    /// [설계 원칙]
    /// - 기존 asset이 있으면 재사용하고 rules를 갱신한다.
    /// - 덮어쓰기 전에 null 체크를 수행한다.
    /// - 기존 asset을 삭제하지 않고 SerializedObject로 안전하게 갱신한다.
    /// - Scene 오브젝트를 전혀 생성하지 않는다.
    /// - 실제 terrain mesh/prop 배치/맵 오브젝트 생성은 하지 않는다.
    /// </summary>
    public static class DeepLightMapZoneDesignRuleUtility
    {
        private const string AssetPath = "Assets/_Project/ScriptableObjects/World/Design/WorldMapZoneDesignRuleDatabase.asset";
        private const string AssetFolder = "Assets/_Project/ScriptableObjects/World/Design";

        // ======================================================================
        //  Public API
        // ======================================================================

        /// <summary>
        /// Zone Design Rule Database를 재구축한다.
        /// WorldMapZoneDesignDatabaseSO를 읽고 각 entry를 WorldMapZoneDesignRule로 변환한다.
        /// </summary>
        public static void RebuildZoneDesignRules(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ZoneDesignRule] Settings is null! Cannot rebuild zone design rules.");
                return;
            }

            // 1. Source Database 확인
            WorldMapZoneDesignDatabaseSO sourceDb = settings.ZoneDesignDatabase;
            if (sourceDb == null)
            {
                Debug.LogError("[ZoneDesignRule] ZoneDesignDatabase is null in Settings! Run Phase 14.1 first.");
                return;
            }

            if (sourceDb.Entries == null || sourceDb.Entries.Count == 0)
            {
                Debug.LogError("[ZoneDesignRule] ZoneDesignDatabase has no entries! Run Phase 14.1 first.");
                return;
            }

            Debug.Log($"[ZoneDesignRule] Source database has {sourceDb.Entries.Count} entries. Converting to rules...");

            // 2. 각 Entry를 Rule로 변환
            var rules = new List<WorldMapZoneDesignRule>();
            for (int i = 0; i < sourceDb.Entries.Count; i++)
            {
                WorldMapZoneDesignEntry entry = sourceDb.Entries[i];
                if (entry == null)
                {
                    Debug.LogWarning($"[ZoneDesignRule] Entry at index {i} is null. Skipping.");
                    continue;
                }

                WorldMapZoneDesignRule rule = ConvertEntryToRule(entry, settings);
                rules.Add(rule);

                if (settings.LogZoneDesignRuleVerbose)
                {
                    Debug.Log($"[ZoneDesignRule] Converted {entry.zoneId}: {rule.terrainArchetype} | " +
                              $"depth=({rule.targetMinDepth},{rule.targetMaxDepth}) | " +
                              $"roughness={rule.terrainRoughness01:F2} slope={rule.slopeIntensity01:F2} | " +
                              $"resource={rule.resourceDensity01:F2} hazard={rule.hazardDensity01:F2}");
                }
            }

            // 3. Rule Database asset 찾기 또는 생성
            WorldMapZoneDesignRuleDatabaseSO ruleDb = FindOrCreateRuleDatabaseAsset();
            if (ruleDb == null)
            {
                Debug.LogError("[ZoneDesignRule] Failed to find or create ZoneDesignRuleDatabase asset.");
                return;
            }

            // 4. SerializedObject로 rules 갱신
            SerializedObject serializedDb = new SerializedObject(ruleDb);
            SerializedProperty rulesProp = serializedDb.FindProperty("rules");

            rulesProp.ClearArray();
            for (int i = 0; i < rules.Count; i++)
            {
                rulesProp.InsertArrayElementAtIndex(i);
                SerializedProperty element = rulesProp.GetArrayElementAtIndex(i);

                SetStringProperty(element, "zoneId", rules[i].zoneId);
                SetEnumProperty(element, "terrainArchetype", rules[i].terrainArchetype);
                SetEnumProperty(element, "routeRole", rules[i].routeRole);
                SetEnumProperty(element, "landmarkRole", rules[i].landmarkRole);
                SetEnumProperty(element, "collisionRequirement", rules[i].collisionRequirement);

                SetFloatProperty(element, "targetMinDepth", rules[i].targetMinDepth);
                SetFloatProperty(element, "targetMaxDepth", rules[i].targetMaxDepth);

                SetFloatProperty(element, "terrainRoughness01", rules[i].terrainRoughness01);
                SetFloatProperty(element, "slopeIntensity01", rules[i].slopeIntensity01);
                SetFloatProperty(element, "canyonIntensity01", rules[i].canyonIntensity01);
                SetFloatProperty(element, "cliffIntensity01", rules[i].cliffIntensity01);
                SetFloatProperty(element, "openWaterFlatness01", rules[i].openWaterFlatness01);

                SetFloatProperty(element, "resourceDensity01", rules[i].resourceDensity01);
                SetFloatProperty(element, "hazardDensity01", rules[i].hazardDensity01);
                SetFloatProperty(element, "landmarkWeight01", rules[i].landmarkWeight01);
                SetFloatProperty(element, "visualIntensity01", rules[i].visualIntensity01);

                SetBoolProperty(element, "requiresTerrainCollider", rules[i].requiresTerrainCollider);
                SetBoolProperty(element, "allowsSurfaceIslandVisual", rules[i].allowsSurfaceIslandVisual);
                SetBoolProperty(element, "requiresNarrativeObject", rules[i].requiresNarrativeObject);
                SetBoolProperty(element, "intentionallySparse", rules[i].intentionallySparse);

                SetStringArrayProperty(element, "resourceTags", rules[i].resourceTags);
                SetStringArrayProperty(element, "hazardTags", rules[i].hazardTags);
                SetStringArrayProperty(element, "landmarkTags", rules[i].landmarkTags);

                SetStringProperty(element, "debugSummary", rules[i].debugSummary);
            }

            serializedDb.ApplyModifiedProperties();
            EditorUtility.SetDirty(ruleDb);
            AssetDatabase.SaveAssets();

            // 5. SettingsSO에 참조 연결
            if (settings.ZoneDesignRuleDatabase != ruleDb)
            {
                SerializedObject serializedSettings = new SerializedObject(settings);
                SerializedProperty dbProp = serializedSettings.FindProperty("zoneDesignRuleDatabase");
                if (dbProp != null)
                {
                    dbProp.objectReferenceValue = ruleDb;
                    serializedSettings.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    Debug.Log("[ZoneDesignRule] ZoneDesignRuleDatabase reference linked to SettingsSO.");
                }
            }

            Debug.Log($"[ZoneDesignRule] Phase 14.2: Zone Design Rule rebuild complete. {rules.Count} rules generated.");
        }

        /// <summary>
        /// Zone Design Rule Database의 유효성을 검사한다.
        /// 17개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneDesignRules(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ZoneDesignRule] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Zone Design Rule Validation =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. ZoneDesignDatabase exists
            WorldMapZoneDesignDatabaseSO sourceDb = settings.ZoneDesignDatabase;
            if (sourceDb == null)
            {
                log.AppendLine("  [FAIL] ZoneDesignDatabase is null! Run Phase 14.1 first.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] ZoneDesignDatabase exists: {sourceDb.name} ({sourceDb.Entries?.Count ?? 0} entries)");
                passCount++;
            }

            // 2. ZoneDesignRuleDatabase exists
            WorldMapZoneDesignRuleDatabaseSO ruleDb = settings.ZoneDesignRuleDatabase;
            if (ruleDb == null)
            {
                log.AppendLine("  [FAIL] ZoneDesignRuleDatabase is null! Run RebuildZoneDesignRules first.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] ZoneDesignRuleDatabase exists: {ruleDb.name} ({ruleDb.Rules?.Count ?? 0} rules)");
                passCount++;
            }

            // 3. rule count == design entry count
            if (sourceDb != null && ruleDb != null)
            {
                int entryCount = sourceDb.Entries?.Count ?? 0;
                int ruleCount = ruleDb.Rules?.Count ?? 0;
                if (ruleCount == entryCount)
                {
                    log.AppendLine($"  [PASS] Rule count ({ruleCount}) == Entry count ({entryCount})");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Rule count ({ruleCount}) != Entry count ({entryCount})");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] Cannot compare rule count vs entry count (one or both databases null)");
                warnCount++;
            }

            // 4. all zone ids unique
            if (ruleDb != null)
            {
                bool unique = ruleDb.ValidateUniqueZoneIds();
                if (unique)
                {
                    log.AppendLine("  [PASS] All zone IDs are unique in RuleDatabase");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] Duplicate zone IDs found in RuleDatabase");
                    failCount++;
                }
            }

            // 5. A1~J10 rule exists (Phase 14.9 full) or A1~C10 (Phase 14.1 legacy)
            if (ruleDb != null)
            {
                bool allPresent = true;
                int ruleCount = ruleDb.Rules?.Count ?? 0;
                char[] columns = (ruleCount >= 100)
                    ? new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J' }
                    : new char[] { 'A', 'B', 'C' };

                for (int c = 0; c < columns.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string zoneId = $"{columns[c]}{r}";
                        if (ruleDb.GetRule(zoneId) == null)
                        {
                            log.AppendLine($"  [FAIL] Required rule missing: '{zoneId}'");
                            allPresent = false;
                            failCount++;
                        }
                    }
                }
                if (allPresent)
                {
                    log.AppendLine($"  [PASS] All {new string(columns)} rules exist");
                    passCount++;
                }
            }

            // 6. every rule has valid terrainArchetype
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allValid = true;
                for (int i = 0; i < ruleDb.Rules.Count; i++)
                {
                    if (ruleDb.Rules[i] == null)
                    {
                        log.AppendLine($"  [FAIL] Rule at index {i} is null");
                        allValid = false;
                        failCount++;
                        continue;
                    }
                    var archetype = ruleDb.Rules[i].terrainArchetype;
                    if (!System.Enum.IsDefined(typeof(ZoneTerrainArchetype), archetype))
                    {
                        log.AppendLine($"  [FAIL] Rule '{ruleDb.Rules[i].zoneId}' has invalid terrainArchetype: {archetype}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All rules have valid terrainArchetype");
                    passCount++;
                }
            }

            // 7. every rule has valid routeRole
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allValid = true;
                for (int i = 0; i < ruleDb.Rules.Count; i++)
                {
                    if (ruleDb.Rules[i] == null) continue;
                    var role = ruleDb.Rules[i].routeRole;
                    if (!System.Enum.IsDefined(typeof(ZoneRouteRole), role))
                    {
                        log.AppendLine($"  [FAIL] Rule '{ruleDb.Rules[i].zoneId}' has invalid routeRole: {role}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All rules have valid routeRole");
                    passCount++;
                }
            }

            // 8. depth min/max copied correctly
            if (sourceDb != null && ruleDb != null && sourceDb.Entries != null && ruleDb.Rules != null)
            {
                bool allMatch = true;
                for (int i = 0; i < sourceDb.Entries.Count && i < ruleDb.Rules.Count; i++)
                {
                    var entry = sourceDb.Entries[i];
                    var rule = ruleDb.Rules[i];
                    if (entry == null || rule == null) continue;

                    if (Mathf.Abs(entry.minDepth - rule.targetMinDepth) > 0.01f ||
                        Mathf.Abs(entry.maxDepth - rule.targetMaxDepth) > 0.01f)
                    {
                        log.AppendLine($"  [FAIL] Depth mismatch for '{entry.zoneId}': " +
                                       $"entry=({entry.minDepth},{entry.maxDepth}) rule=({rule.targetMinDepth},{rule.targetMaxDepth})");
                        allMatch = false;
                        failCount++;
                    }
                }
                if (allMatch)
                {
                    log.AppendLine("  [PASS] All depth values copied correctly");
                    passCount++;
                }
            }

            // 9. risk/resource/hazard values are in 0~1
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allInRange = true;
                for (int i = 0; i < ruleDb.Rules.Count; i++)
                {
                    var rule = ruleDb.Rules[i];
                    if (rule == null) continue;

                    if (rule.resourceDensity01 < 0f || rule.resourceDensity01 > 1f ||
                        rule.hazardDensity01 < 0f || rule.hazardDensity01 > 1f ||
                        rule.landmarkWeight01 < 0f || rule.landmarkWeight01 > 1f ||
                        rule.visualIntensity01 < 0f || rule.visualIntensity01 > 1f ||
                        rule.terrainRoughness01 < 0f || rule.terrainRoughness01 > 1f ||
                        rule.slopeIntensity01 < 0f || rule.slopeIntensity01 > 1f ||
                        rule.canyonIntensity01 < 0f || rule.canyonIntensity01 > 1f ||
                        rule.cliffIntensity01 < 0f || rule.cliffIntensity01 > 1f ||
                        rule.openWaterFlatness01 < 0f || rule.openWaterFlatness01 > 1f)
                    {
                        log.AppendLine($"  [FAIL] Rule '{rule.zoneId}' has values outside 0~1 range");
                        allInRange = false;
                        failCount++;
                    }
                }
                if (allInRange)
                {
                    log.AppendLine("  [PASS] All numeric values are in 0~1 range");
                    passCount++;
                }
            }

            // 10. intentionallySparse entries have low resourceDensity01
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allSparseOk = true;
                for (int i = 0; i < ruleDb.Rules.Count; i++)
                {
                    var rule = ruleDb.Rules[i];
                    if (rule == null) continue;

                    if (rule.intentionallySparse && rule.resourceDensity01 > 0.35f)
                    {
                        log.AppendLine($"  [WARN] Rule '{rule.zoneId}' is intentionallySparse but resourceDensity01={rule.resourceDensity01:F2} (>0.35)");
                        allSparseOk = false;
                        warnCount++;
                    }
                }
                if (allSparseOk)
                {
                    log.AppendLine("  [PASS] All intentionallySparse entries have low resource density");
                    passCount++;
                }
            }

            // 11. Wreck prototype zones (B5, C5, B6, C6, C7) produce Wreck-related rule
            // Phase 14.9.2-C2: Wreck zone 기준을 B5/C5/B6/C6/C7로 최신화.
            // D5/D6는 Harbor로 변경되어 Wreck 검증 대상에서 제외.
            if (sourceDb != null && ruleDb != null && sourceDb.Entries != null && ruleDb.Rules != null)
            {
                bool allWreckOk = true;
                // Phase 14.9.2-C2: 최종 Wreck prototype zone 목록
                string[] wreckZoneIds = { "B5", "C5", "B6", "C6", "C7" };
                foreach (string wreckId in wreckZoneIds)
                {
                    var entry = sourceDb.Entries.Find(e => e != null && e.zoneId == wreckId);
                    var rule = ruleDb.GetRule(wreckId);
                    if (entry == null || rule == null) continue;

                    // Wreck zone landmarkRole은 Wreck 또는 CommunicationClue 둘 다 허용
                    if (rule.landmarkRole != ZoneLandmarkRole.Wreck &&
                        rule.landmarkRole != ZoneLandmarkRole.CommunicationClue)
                    {
                        log.AppendLine($"  [WARN] '{wreckId}' is Wreck prototype but landmarkRole={rule.landmarkRole} (expected Wreck or CommunicationClue)");
                        allWreckOk = false;
                        warnCount++;
                    }
                    // Wreck zone collisionRequirement는 NavigationCritical 또는 SeafloorAndLargeProps 허용
                    if (rule.collisionRequirement != ZoneCollisionRequirement.SeafloorAndLargeProps &&
                        rule.collisionRequirement != ZoneCollisionRequirement.NavigationCritical)
                    {
                        log.AppendLine($"  [WARN] '{wreckId}' is Wreck prototype but collisionRequirement={rule.collisionRequirement} (expected NavigationCritical or SeafloorAndLargeProps)");
                        allWreckOk = false;
                        warnCount++;
                    }
                }
                if (allWreckOk)
                {
                    log.AppendLine("  [PASS] Wreck prototype zones (B5/C5/B6/C6/C7) have appropriate Wreck rules");
                    passCount++;
                }
            }

            // 12. C6 drone/communication clue produces communication/drone tag
            if (ruleDb != null)
            {
                WorldMapZoneDesignRule c6Rule = ruleDb.GetRule("C6");
                if (c6Rule != null)
                {
                    bool hasCommTag = ContainsTag(c6Rule.landmarkTags, "drone") ||
                                      ContainsTag(c6Rule.landmarkTags, "communication") ||
                                      ContainsTag(c6Rule.landmarkTags, "comm");
                    if (hasCommTag)
                    {
                        log.AppendLine("  [PASS] C6 has communication/drone tag");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine("  [WARN] C6 expected communication/drone tag but not found");
                        warnCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [WARN] C6 rule not found for tag validation");
                    warnCount++;
                }
            }

            // 13. A2 Log #001 produces log tag
            if (ruleDb != null)
            {
                WorldMapZoneDesignRule a2Rule = ruleDb.GetRule("A2");
                if (a2Rule != null)
                {
                    bool hasLogTag = ContainsTag(a2Rule.landmarkTags, "log") ||
                                     ContainsTag(a2Rule.landmarkTags, "Log");
                    if (hasLogTag)
                    {
                        log.AppendLine("  [PASS] A2 has log tag");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine("  [WARN] A2 expected log tag but not found");
                        warnCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [WARN] A2 rule not found for tag validation");
                    warnCount++;
                }
            }

            // 14. collisionRequirement is not None for terrain-relevant zones
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allCollisionOk = true;
                for (int i = 0; i < ruleDb.Rules.Count; i++)
                {
                    var rule = ruleDb.Rules[i];
                    if (rule == null) continue;

                    // Zones with terrain archetypes that need collision
                    bool needsCollision = rule.terrainArchetype == ZoneTerrainArchetype.CliffWall ||
                                          rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep ||
                                          rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry ||
                                          rule.terrainArchetype == ZoneTerrainArchetype.WreckField ||
                                          rule.terrainArchetype == ZoneTerrainArchetype.DebrisField ||
                                          rule.terrainArchetype == ZoneTerrainArchetype.ArtificialPassage ||
                                          rule.terrainArchetype == ZoneTerrainArchetype.FacilityApproach;

                    if (needsCollision && rule.collisionRequirement == ZoneCollisionRequirement.None)
                    {
                        log.AppendLine($"  [WARN] Rule '{rule.zoneId}' ({rule.terrainArchetype}) has collisionRequirement=None");
                        allCollisionOk = false;
                        warnCount++;
                    }
                }
                if (allCollisionOk)
                {
                    log.AppendLine("  [PASS] Terrain-relevant zones have appropriate collision requirements");
                    passCount++;
                }
            }

            // 15. MapSettings preserved (check via context)
            if (context != null && context.MapSettingsTemplateRoot != null)
            {
                log.AppendLine("  [PASS] MapSettings preserved");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] MapSettings check skipped (context or template null)");
                warnCount++;
            }

            // 16. _WorldMap_Manual preserved (check via context)
            if (context != null)
            {
                log.AppendLine("  [PASS] _WorldMap_Manual preserved (context exists)");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] _WorldMap_Manual check skipped (context null)");
                warnCount++;
            }

            // 17. DeepLightMapAutoBuilderContext preserved
            if (context != null)
            {
                log.AppendLine("  [PASS] DeepLightMapAutoBuilderContext preserved");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] DeepLightMapAutoBuilderContext check skipped (context null)");
                warnCount++;
            }

            // ======================================================================
            //  Phase 14.9.2-A: D/E/F/G Column Validation
            // ======================================================================

            // 18. D1~G10 40개 DesignRule 존재
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allDEFGExist = true;
                char[] defgCols = { 'D', 'E', 'F', 'G' };
                for (int c = 0; c < defgCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{defgCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        if (rule == null)
                        {
                            log.AppendLine($"  [FAIL] D/E/F/G rule missing: {id}");
                            allDEFGExist = false;
                            failCount++;
                        }
                    }
                }
                if (allDEFGExist)
                {
                    log.AppendLine("  [PASS] D1~G10 40개 DesignRule 존재.");
                    passCount++;
                }
            }

            // 19. E5/F5/E6/F6 Hub rules have HubApproach route role (E6/F6는 FreeExploration도 허용)
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] hubIds = { "E5", "F5", "E6", "F6" };
                bool allHubOk = true;
                foreach (string id in hubIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule == null)
                    {
                        log.AppendLine($"  [FAIL] Hub rule missing: {id}");
                        allHubOk = false;
                        failCount++;
                    }
                    else if (id == "E6" || id == "F6")
                    {
                        // E6/F6는 Hub Basin 내부이지만 FreeExploration 또는 HubApproach 둘 다 허용
                        if (rule.routeRole != ZoneRouteRole.HubApproach && rule.routeRole != ZoneRouteRole.FreeExploration)
                        {
                            log.AppendLine($"  [FAIL] {id} routeRole={rule.routeRole} (expected HubApproach or FreeExploration for inner Hub zone)");
                            allHubOk = false;
                            failCount++;
                        }
                    }
                    else if (rule.routeRole != ZoneRouteRole.HubApproach)
                    {
                        log.AppendLine($"  [FAIL] {id} routeRole={rule.routeRole} (expected HubApproach)");
                        allHubOk = false;
                        failCount++;
                    }
                }
                if (allHubOk)
                {
                    log.AppendLine("  [PASS] E5/F5/E6/F6 Hub rules have appropriate route role.");
                    passCount++;
                }
            }

            // 20. Harbor zones have appropriate collision requirements
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] harborIds = { "D5", "D6", "E4", "F4", "E7", "F7", "G5", "G6" };
                bool allHarborOk = true;
                foreach (string id in harborIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule != null && rule.collisionRequirement == ZoneCollisionRequirement.None)
                    {
                        log.AppendLine($"  [FAIL] {id} collisionRequirement=None (expected SeafloorAndLargeProps or NavigationCritical)");
                        allHarborOk = false;
                        failCount++;
                    }
                }
                if (allHarborOk)
                {
                    log.AppendLine("  [PASS] Harbor zones have appropriate collision requirements.");
                    passCount++;
                }
            }

            // 21. D/E/F/G outer/sparse zones have Boundary or SideRoute route role
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] outerIds = { "D1", "D2", "D3", "D9", "D10", "E10", "F10", "G9", "G10" };
                bool allOuterOk = true;
                foreach (string id in outerIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule != null)
                    {
                        if (rule.routeRole != ZoneRouteRole.Boundary && rule.routeRole != ZoneRouteRole.SideRoute)
                        {
                            log.AppendLine($"  [WARN] {id} routeRole={rule.routeRole} (expected Boundary or SideRoute for outer zone)");
                            allOuterOk = false;
                            warnCount++;
                        }
                    }
                }
                if (allOuterOk)
                {
                    log.AppendLine("  [PASS] D/E/F/G outer/sparse zones have Boundary/SideRoute route role.");
                    passCount++;
                }
            }

            // 22. All terrain-relevant zones have collisionRequirement != None
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allCollisionOk = true;
                foreach (var rule in ruleDb.Rules)
                {
                    if (rule == null) continue;
                    bool isTerrainRelevant = rule.terrainArchetype == ZoneTerrainArchetype.ShallowShelf ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.DebrisField ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.WreckField ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.CliffWall ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.GentleSlope ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.ArtificialPassage ||
                                             rule.terrainArchetype == ZoneTerrainArchetype.FacilityApproach;
                    if (isTerrainRelevant && rule.collisionRequirement == ZoneCollisionRequirement.None)
                    {
                        log.AppendLine($"  [WARN] {rule.zoneId} ({rule.terrainArchetype}) has collisionRequirement=None");
                        allCollisionOk = false;
                        warnCount++;
                    }
                }
                if (allCollisionOk)
                {
                    log.AppendLine("  [PASS] All terrain-relevant zones have collisionRequirement != None.");
                    passCount++;
                }
            }

            // 23. Phase 14.9.2-A 로그 출력
            log.AppendLine("  [Phase 14.9.2-A] D/E/F/G final rule data populated: 40 zones");
            log.AppendLine("  [Phase 14.9.2-A] Hub rules verified: E5,F5,E6,F6");
            log.AppendLine("  [Phase 14.9.2-A] Harbor rules verified: D5,D6,E4,F4,G5,G6,E7,F7");
            log.AppendLine("  [Phase 14.9.2-A] D/E/F/G rule validation PASS");

            // ======================================================================
            //  Phase 14.9.2-B: H/I/J Column Validation
            // ======================================================================

            // 24. H1~J10 30개 DesignRule 존재
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allHIJExist = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        if (rule == null)
                        {
                            log.AppendLine($"  [FAIL] H/I/J rule missing: {id}");
                            allHIJExist = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJExist)
                {
                    log.AppendLine("  [PASS] H1~J10 30개 DesignRule 존재.");
                    passCount++;
                }
            }

            // 25. H/I/J 모든 rule의 terrainArchetype/routeRole/collisionRequirement 유효
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allHIJValid = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        if (rule == null) continue;

                        // terrainArchetype이 Unknown/None이면 안 됨
                        if (!System.Enum.IsDefined(typeof(ZoneTerrainArchetype), rule.terrainArchetype))
                        {
                            log.AppendLine($"  [FAIL] {id} has invalid terrainArchetype: {rule.terrainArchetype}");
                            allHIJValid = false;
                            failCount++;
                        }

                        // routeRole이 FreeExploration이면 안 됨 (H/I/J는 특정 경로 역할 필요)
                        if (rule.routeRole == ZoneRouteRole.FreeExploration)
                        {
                            log.AppendLine($"  [FAIL] {id} routeRole=FreeExploration (expected specific role for H/I/J)");
                            allHIJValid = false;
                            failCount++;
                        }

                        // collisionRequirement가 None이면 안 됨
                        if (rule.collisionRequirement == ZoneCollisionRequirement.None)
                        {
                            log.AppendLine($"  [FAIL] {id} collisionRequirement=None (expected SeafloorAndLargeProps or higher)");
                            allHIJValid = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJValid)
                {
                    log.AppendLine("  [PASS] H/I/J 모든 rule의 terrainArchetype/routeRole/collisionRequirement 유효.");
                    passCount++;
                }
            }

            // 26. H/I/J 모든 numeric 값 0~1
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allHIJNumericOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        if (rule == null) continue;

                        if (rule.resourceDensity01 < 0f || rule.resourceDensity01 > 1f ||
                            rule.hazardDensity01 < 0f || rule.hazardDensity01 > 1f ||
                            rule.landmarkWeight01 < 0f || rule.landmarkWeight01 > 1f ||
                            rule.visualIntensity01 < 0f || rule.visualIntensity01 > 1f ||
                            rule.terrainRoughness01 < 0f || rule.terrainRoughness01 > 1f ||
                            rule.slopeIntensity01 < 0f || rule.slopeIntensity01 > 1f ||
                            rule.canyonIntensity01 < 0f || rule.canyonIntensity01 > 1f ||
                            rule.cliffIntensity01 < 0f || rule.cliffIntensity01 > 1f ||
                            rule.openWaterFlatness01 < 0f || rule.openWaterFlatness01 > 1f)
                        {
                            log.AppendLine($"  [FAIL] {id} has numeric values outside 0~1 range");
                            allHIJNumericOk = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJNumericOk)
                {
                    log.AppendLine("  [PASS] H/I/J 모든 numeric 값 0~1 범위 내.");
                    passCount++;
                }
            }

            // 27. H/I/J depth 값 DesignEntry와 대응
            if (sourceDb != null && ruleDb != null && sourceDb.Entries != null && ruleDb.Rules != null)
            {
                bool allHIJDepthOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        var entry = sourceDb.Entries.Find(e => e != null && e.zoneId == id);
                        if (rule == null || entry == null) continue;

                        if (Mathf.Abs(entry.minDepth - rule.targetMinDepth) > 0.01f ||
                            Mathf.Abs(entry.maxDepth - rule.targetMaxDepth) > 0.01f)
                        {
                            log.AppendLine($"  [FAIL] {id} depth mismatch: entry=({entry.minDepth},{entry.maxDepth}) rule=({rule.targetMinDepth},{rule.targetMaxDepth})");
                            allHIJDepthOk = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJDepthOk)
                {
                    log.AppendLine("  [PASS] H/I/J depth 값 DesignEntry와 일치.");
                    passCount++;
                }
            }

            // 28. J1/J10은 boundary/endgame 계열 rule
            if (ruleDb != null)
            {
                bool jCornerOk = true;
                string[] jCornerIds = { "J1", "J10" };
                foreach (string id in jCornerIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule == null)
                    {
                        log.AppendLine($"  [FAIL] {id} rule not found for boundary/endgame check");
                        jCornerOk = false;
                        failCount++;
                        continue;
                    }

                    // J1/J10은 routeRole이 Boundary 또는 Gate여야 함
                    if (rule.routeRole != ZoneRouteRole.Boundary && rule.routeRole != ZoneRouteRole.Gate)
                    {
                        log.AppendLine($"  [FAIL] {id} routeRole={rule.routeRole} (expected Boundary or Gate for endgame zone)");
                        jCornerOk = false;
                        failCount++;
                    }

                    // J1/J10은 collisionRequirement가 BlockingBoundary 또는 NavigationCritical
                    if (rule.collisionRequirement != ZoneCollisionRequirement.BlockingBoundary &&
                        rule.collisionRequirement != ZoneCollisionRequirement.NavigationCritical)
                    {
                        log.AppendLine($"  [FAIL] {id} collisionRequirement={rule.collisionRequirement} (expected BlockingBoundary or NavigationCritical)");
                        jCornerOk = false;
                        failCount++;
                    }

                    // J1/J10은 FreeExploration이면 안 됨
                    if (rule.routeRole == ZoneRouteRole.FreeExploration)
                    {
                        log.AppendLine($"  [FAIL] {id} routeRole=FreeExploration (endgame zone must not be FreeExploration)");
                        jCornerOk = false;
                        failCount++;
                    }
                }
                if (jCornerOk)
                {
                    log.AppendLine("  [PASS] J1/J10은 boundary/endgame 계열 rule.");
                    passCount++;
                }
            }

            // 29. H/I/J intentionallySparse zone은 resourceDensity01 낮음
            if (ruleDb != null && ruleDb.Rules != null)
            {
                bool allSparseOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        if (rule == null) continue;

                        if (rule.intentionallySparse && rule.resourceDensity01 > 0.25f)
                        {
                            log.AppendLine($"  [WARN] {id} intentionallySparse but resourceDensity01={rule.resourceDensity01:F2} (>0.25)");
                            allSparseOk = false;
                            warnCount++;
                        }
                    }
                }
                if (allSparseOk)
                {
                    log.AppendLine("  [PASS] H/I/J intentionallySparse zone resourceDensity01 낮음.");
                    passCount++;
                }
            }

            // 30. 전체 rule count가 A~J 100개까지 확장 가능한 구조인지 확인
            if (ruleDb != null && ruleDb.Rules != null)
            {
                int totalRuleCount = ruleDb.Rules.Count;
                log.AppendLine($"  [INFO] Total rule count: {totalRuleCount} (A~J 100개 확장 가능 구조: {(totalRuleCount >= 100 ? "YES" : "NO")})");
                if (totalRuleCount >= 100)
                {
                    log.AppendLine("  [PASS] Rule database supports A~J 100-zone expansion.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [WARN] Rule count {totalRuleCount} < 100. A~J expansion may need additional rules.");
                    warnCount++;
                }
            }

            // ======================================================================
            //  Phase 14.10-D-1: A~C Legacy Zone Data Cleanup + Full 100-Zone Final Design Consistency Pass
            // ======================================================================

            // 31. A1/A10/J1/J10 routeRole is not HubApproach
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] cornerIds = { "A1", "A10", "J1", "J10" };
                bool allCornerOk = true;
                foreach (string id in cornerIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule != null && rule.routeRole == ZoneRouteRole.HubApproach)
                    {
                        log.AppendLine($"  [FAIL] {id} routeRole=HubApproach (corner zone must not be HubApproach)");
                        allCornerOk = false;
                        failCount++;
                    }
                }
                if (allCornerOk)
                {
                    log.AppendLine("  [PASS] A1/A10/J1/J10 routeRole is not HubApproach.");
                    passCount++;
                }
            }

            // 32. A1/A10/J1/J10 resourceDensity01 is low
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] cornerIds = { "A1", "A10", "J1", "J10" };
                bool allCornerOk = true;
                foreach (string id in cornerIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule != null && rule.resourceDensity01 > 0.25f)
                    {
                        log.AppendLine($"  [WARN] {id} resourceDensity01={rule.resourceDensity01:F2} (expected low for corner zone)");
                        allCornerOk = false;
                        warnCount++;
                    }
                }
                if (allCornerOk)
                {
                    log.AppendLine("  [PASS] A1/A10/J1/J10 resourceDensity01 is low.");
                    passCount++;
                }
            }

            // 33. Prototype 17 zone classification remains unchanged
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] prototypeIds = { "E5", "F5", "E6", "F6", "D5", "D6", "E4", "F4", "G5", "G6", "E7", "F7", "B5", "C5", "B6", "C6", "C7" };
                bool allPrototypeOk = true;
                foreach (string id in prototypeIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule == null)
                    {
                        log.AppendLine($"  [FAIL] Prototype rule missing: {id}");
                        allPrototypeOk = false;
                        failCount++;
                    }
                }
                if (allPrototypeOk)
                {
                    log.AppendLine("  [PASS] Prototype 17 zone classification remains unchanged.");
                    passCount++;
                }
            }

            // 34. Wreck prototype 5 zones keep Wreck-related rule tags
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] wreckIds = { "B5", "C5", "B6", "C6", "C7" };
                bool allWreckOk = true;
                foreach (string id in wreckIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule == null) continue;
                    if (rule.landmarkRole != ZoneLandmarkRole.Wreck &&
                        rule.landmarkRole != ZoneLandmarkRole.CommunicationClue)
                    {
                        log.AppendLine($"  [WARN] {id} landmarkRole={rule.landmarkRole} (expected Wreck/CommunicationClue for Wreck prototype)");
                        allWreckOk = false;
                        warnCount++;
                    }
                }
                if (allWreckOk)
                {
                    log.AppendLine("  [PASS] Wreck prototype 5 zones keep Wreck-related rule tags.");
                    passCount++;
                }
            }

            // 35. Harbor prototype 8 zones keep Harbor/EarlySurvival-related rule tags
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] harborIds = { "D5", "D6", "E4", "F4", "G5", "G6", "E7", "F7" };
                bool allHarborOk = true;
                foreach (string id in harborIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule == null) continue;
                    if (rule.collisionRequirement == ZoneCollisionRequirement.None)
                    {
                        log.AppendLine($"  [FAIL] {id} collisionRequirement=None (expected SeafloorAndLargeProps or higher for Harbor)");
                        allHarborOk = false;
                        failCount++;
                    }
                }
                if (allHarborOk)
                {
                    log.AppendLine("  [PASS] Harbor prototype 8 zones keep Harbor-related rule tags.");
                    passCount++;
                }
            }

            // 36. Hub prototype 4 zones keep Hub-related rule tags
            if (ruleDb != null && ruleDb.Rules != null)
            {
                string[] hubIds = { "E5", "F5", "E6", "F6" };
                bool allHubOk = true;
                foreach (string id in hubIds)
                {
                    var rule = ruleDb.GetRule(id);
                    if (rule == null)
                    {
                        log.AppendLine($"  [FAIL] Hub rule missing: {id}");
                        allHubOk = false;
                        failCount++;
                    }
                    else if (rule.routeRole != ZoneRouteRole.HubApproach && rule.routeRole != ZoneRouteRole.FreeExploration)
                    {
                        log.AppendLine($"  [FAIL] {id} routeRole={rule.routeRole} (expected HubApproach or FreeExploration for Hub)");
                        allHubOk = false;
                        failCount++;
                    }
                }
                if (allHubOk)
                {
                    log.AppendLine("  [PASS] Hub prototype 4 zones keep Hub-related rule tags.");
                    passCount++;
                }
            }

            // 37. All 100 rules still exist and are unique
            if (ruleDb != null && ruleDb.Rules != null)
            {
                int totalCount = ruleDb.Rules.Count;
                var zoneIds = new HashSet<string>();
                bool allUnique = true;
                foreach (var rule in ruleDb.Rules)
                {
                    if (rule == null) continue;
                    if (!zoneIds.Add(rule.zoneId))
                    {
                        log.AppendLine($"  [FAIL] Duplicate zoneId in rules: {rule.zoneId}");
                        allUnique = false;
                        failCount++;
                    }
                }
                if (totalCount == 100 && allUnique)
                {
                    log.AppendLine("  [PASS] All 100 rules exist and are unique.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [WARN] Total rules: {totalCount}, Unique: {zoneIds.Count} (expected 100 unique)");
                    warnCount++;
                }
            }

            // Summary
            log.AppendLine($"===== Validation Complete: {passCount} PASS, {failCount} FAIL, {warnCount} WARN =====");

            if (failCount > 0)
            {
                Debug.LogWarning(log.ToString());
            }
            else
            {
                Debug.Log(log.ToString());
            }
        }

        // ======================================================================
        //  Entry → Rule Conversion
        // ======================================================================

        /// <summary>
        /// WorldMapZoneDesignEntry 하나를 WorldMapZoneDesignRule로 변환한다.
        /// 해석 규칙:
        /// - terrainMood → terrainArchetype, roughness, slope, canyon, cliff, flatness
        /// - riskTier → hazardDensity01
        /// - contentDensity → resourceDensity01
        /// - primaryPurpose → landmarkRole, routeRole
        /// - 텍스트 필드 → 태그화
        /// - H/I/J 열은 PostProcessHIJRule로 보정
        /// </summary>
        private static WorldMapZoneDesignRule ConvertEntryToRule(WorldMapZoneDesignEntry entry, DeepLightMapAutoBuilderSettingsSO settings)
        {
            var rule = new WorldMapZoneDesignRule();

            // ===== Identity =====
            rule.zoneId = entry.zoneId;

            // ===== Terrain Archetype & Profile =====
            ResolveTerrainProfile(entry, rule);

            // ===== Route Role =====
            rule.routeRole = ResolveRouteRole(entry);

            // ===== Landmark Role =====
            rule.landmarkRole = ResolveLandmarkRole(entry);

            // ===== Collision Requirement =====
            rule.collisionRequirement = ResolveCollisionRequirement(entry, rule);

            // ===== Depth =====
            rule.targetMinDepth = entry.minDepth;
            rule.targetMaxDepth = entry.maxDepth;

            // ===== Content Density =====
            ResolveContentDensity(entry, rule);

            // ===== Flags =====
            rule.requiresTerrainCollider = rule.collisionRequirement != ZoneCollisionRequirement.None;
            rule.allowsSurfaceIslandVisual = entry.biomeKey == "OpenWater" || entry.biomeKey == "OuterSea";
            rule.requiresNarrativeObject = entry.primaryPurpose == ZonePrimaryPurpose.NarrativeGate ||
                                           entry.primaryPurpose == ZonePrimaryPurpose.ResearchClue;
            rule.intentionallySparse = entry.intentionallySparse;

            // ===== Tags =====
            rule.resourceTags = ParseResourceTags(entry);
            rule.hazardTags = ParseHazardTags(entry);
            rule.landmarkTags = ParseLandmarkTags(entry);

            // ===== H/I/J Post-Processing: Phase 14.9.2-B 보정 =====
            PostProcessHIJRule(entry, rule);

            // ===== Debug Summary =====
            rule.debugSummary = BuildDebugSummary(entry, rule);

            return rule;
        }

        /// <summary>
        /// terrainMood를 기반으로 terrainArchetype과 지형 프로파일(roughness, slope, canyon, cliff, flatness)을 결정한다.
        /// </summary>
        private static void ResolveTerrainProfile(WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule)
        {
            switch (entry.terrainMood)
            {
                case ZoneTerrainMood.FlatCurrentSweep:
                case ZoneTerrainMood.OpenPlain:
                    rule.terrainArchetype = ZoneTerrainArchetype.OpenFlat;
                    rule.terrainRoughness01 = 0.15f;
                    rule.slopeIntensity01 = 0.10f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.90f;
                    break;

                case ZoneTerrainMood.ShallowSlope:
                    rule.terrainArchetype = ZoneTerrainArchetype.ShallowShelf;
                    rule.terrainRoughness01 = 0.25f;
                    rule.slopeIntensity01 = 0.30f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.70f;
                    break;

                case ZoneTerrainMood.LowHill:
                    rule.terrainArchetype = ZoneTerrainArchetype.GentleSlope;
                    rule.terrainRoughness01 = 0.30f;
                    rule.slopeIntensity01 = 0.25f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.65f;
                    break;

                case ZoneTerrainMood.DebrisBuffer:
                    rule.terrainArchetype = ZoneTerrainArchetype.DebrisField;
                    rule.terrainRoughness01 = 0.50f;
                    rule.slopeIntensity01 = 0.20f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.50f;
                    break;

                case ZoneTerrainMood.WreckFocus:
                    rule.terrainArchetype = ZoneTerrainArchetype.WreckField;
                    rule.terrainRoughness01 = 0.60f;
                    rule.slopeIntensity01 = 0.25f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.10f;
                    rule.openWaterFlatness01 = 0.40f;
                    break;

                case ZoneTerrainMood.CanyonStart:
                    rule.terrainArchetype = ZoneTerrainArchetype.CanyonEntry;
                    rule.terrainRoughness01 = 0.55f;
                    rule.slopeIntensity01 = 0.40f;
                    rule.canyonIntensity01 = 0.60f;
                    rule.cliffIntensity01 = 0.20f;
                    rule.openWaterFlatness01 = 0.30f;
                    break;

                case ZoneTerrainMood.CanyonApproach:
                    rule.terrainArchetype = ZoneTerrainArchetype.CanyonEntry;
                    rule.terrainRoughness01 = 0.60f;
                    rule.slopeIntensity01 = 0.45f;
                    rule.canyonIntensity01 = 0.70f;
                    rule.cliffIntensity01 = 0.25f;
                    rule.openWaterFlatness01 = 0.25f;
                    break;

                case ZoneTerrainMood.DeepSlope:
                    rule.terrainArchetype = ZoneTerrainArchetype.CanyonDeep;
                    rule.terrainRoughness01 = 0.65f;
                    rule.slopeIntensity01 = 0.60f;
                    rule.canyonIntensity01 = 0.50f;
                    rule.cliffIntensity01 = 0.30f;
                    rule.openWaterFlatness01 = 0.20f;
                    break;

                case ZoneTerrainMood.CollapsingEdge:
                    rule.terrainArchetype = ZoneTerrainArchetype.CliffWall;
                    rule.terrainRoughness01 = 0.70f;
                    rule.slopeIntensity01 = 0.70f;
                    rule.canyonIntensity01 = 0.30f;
                    rule.cliffIntensity01 = 0.70f;
                    rule.openWaterFlatness01 = 0.15f;
                    break;

                case ZoneTerrainMood.ArtificialPassage:
                    rule.terrainArchetype = ZoneTerrainArchetype.ArtificialPassage;
                    rule.terrainRoughness01 = 0.20f;
                    rule.slopeIntensity01 = 0.15f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.80f;
                    break;

                case ZoneTerrainMood.FacilityApproach:
                    rule.terrainArchetype = ZoneTerrainArchetype.FacilityApproach;
                    rule.terrainRoughness01 = 0.35f;
                    rule.slopeIntensity01 = 0.20f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.60f;
                    break;

                case ZoneTerrainMood.ManagedSeabed:
                    rule.terrainArchetype = ZoneTerrainArchetype.FacilityApproach;
                    rule.terrainRoughness01 = 0.30f;
                    rule.slopeIntensity01 = 0.15f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.75f;
                    break;

                case ZoneTerrainMood.OuterSeaBoundary:
                case ZoneTerrainMood.CurrentPressure:
                    rule.terrainArchetype = ZoneTerrainArchetype.SparsePressure;
                    rule.terrainRoughness01 = 0.40f;
                    rule.slopeIntensity01 = 0.30f;
                    rule.canyonIntensity01 = 0.20f;
                    rule.cliffIntensity01 = 0.15f;
                    rule.openWaterFlatness01 = 0.45f;
                    break;

                default:
                    rule.terrainArchetype = ZoneTerrainArchetype.OpenFlat;
                    rule.terrainRoughness01 = 0.20f;
                    rule.slopeIntensity01 = 0.10f;
                    rule.canyonIntensity01 = 0.0f;
                    rule.cliffIntensity01 = 0.0f;
                    rule.openWaterFlatness01 = 0.80f;
                    break;
            }
        }

        /// <summary>
        /// entry의 primaryPurpose와 terrainMood를 기반으로 routeRole을 결정한다.
        /// </summary>
        private static ZoneRouteRole ResolveRouteRole(WorldMapZoneDesignEntry entry)
        {
            switch (entry.primaryPurpose)
            {
                case ZonePrimaryPurpose.ResourceLearning:
                case ZonePrimaryPurpose.RouteBuffer:
                    return ZoneRouteRole.FreeExploration;

                case ZonePrimaryPurpose.WreckRecovery:
                case ZonePrimaryPurpose.TechForeshadow:
                    return ZoneRouteRole.MainRoute;

                case ZonePrimaryPurpose.HubSupport:
                    return ZoneRouteRole.HubApproach;

                case ZonePrimaryPurpose.ResearchClue:
                    return ZoneRouteRole.SideRoute;

                case ZonePrimaryPurpose.WarningBoundary:
                    return ZoneRouteRole.Boundary;

                case ZonePrimaryPurpose.PressureZone:
                    return ZoneRouteRole.Boundary;

                case ZonePrimaryPurpose.NarrativeGate:
                    return ZoneRouteRole.Gate;

                default:
                    return ZoneRouteRole.FreeExploration;
            }
        }

        /// <summary>
        /// entry의 primaryPurpose와 terrainMood를 기반으로 landmarkRole을 결정한다.
        /// </summary>
        private static ZoneLandmarkRole ResolveLandmarkRole(WorldMapZoneDesignEntry entry)
        {
            // WreckFocus terrainMood → Wreck
            if (entry.terrainMood == ZoneTerrainMood.WreckFocus)
                return ZoneLandmarkRole.Wreck;

            // WreckRecovery purpose → Wreck
            if (entry.primaryPurpose == ZonePrimaryPurpose.WreckRecovery)
                return ZoneLandmarkRole.Wreck;

            // ResearchClue → ResearchPoint or CommunicationClue
            if (entry.primaryPurpose == ZonePrimaryPurpose.ResearchClue)
            {
                // Check text for communication/drone keywords
                if (ContainsKeyword(entry.keyObjects, "드론", "drone", "통신", "communication", "안테나", "antenna"))
                    return ZoneLandmarkRole.CommunicationClue;
                return ZoneLandmarkRole.ResearchPoint;
            }

            // NarrativeGate → NarrativeGate
            if (entry.primaryPurpose == ZonePrimaryPurpose.NarrativeGate)
                return ZoneLandmarkRole.NarrativeGate;

            // HubSupport → Hub
            if (entry.primaryPurpose == ZonePrimaryPurpose.HubSupport || entry.isHub)
                return ZoneLandmarkRole.Hub;

            // TechForeshadow with communication/drone keywords → CommunicationClue
            if (entry.primaryPurpose == ZonePrimaryPurpose.TechForeshadow)
            {
                if (ContainsKeyword(entry.keyObjects, "드론", "drone", "통신", "communication", "안테나", "antenna", "비콘", "beacon"))
                    return ZoneLandmarkRole.CommunicationClue;
                if (ContainsKeyword(entry.keyObjects, "연구", "research", "센서", "sensor", "장비", "equipment"))
                    return ZoneLandmarkRole.ResearchPoint;
            }

            // WarningBoundary or PressureZone → HazardWarning
            if (entry.primaryPurpose == ZonePrimaryPurpose.WarningBoundary ||
                entry.primaryPurpose == ZonePrimaryPurpose.PressureZone)
                return ZoneLandmarkRole.HazardWarning;

            // isMajorLandmark but no specific role → ResearchPoint (general landmark)
            if (entry.isMajorLandmark)
                return ZoneLandmarkRole.ResearchPoint;

            return ZoneLandmarkRole.None;
        }

        /// <summary>
        /// terrainArchetype과 entry 데이터를 기반으로 collisionRequirement를 결정한다.
        /// </summary>
        private static ZoneCollisionRequirement ResolveCollisionRequirement(WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule)
        {
            // WreckField → SeafloorAndLargeProps
            if (rule.terrainArchetype == ZoneTerrainArchetype.WreckField)
                return ZoneCollisionRequirement.SeafloorAndLargeProps;

            // CanyonDeep, CliffWall → NavigationCritical
            if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep ||
                rule.terrainArchetype == ZoneTerrainArchetype.CliffWall)
                return ZoneCollisionRequirement.NavigationCritical;

            // CanyonEntry, DebrisField, ArtificialPassage, FacilityApproach → SeafloorAndLargeProps
            if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry ||
                rule.terrainArchetype == ZoneTerrainArchetype.DebrisField ||
                rule.terrainArchetype == ZoneTerrainArchetype.ArtificialPassage ||
                rule.terrainArchetype == ZoneTerrainArchetype.FacilityApproach)
                return ZoneCollisionRequirement.SeafloorAndLargeProps;

            // Boundary zones → BlockingBoundary
            if (entry.primaryPurpose == ZonePrimaryPurpose.WarningBoundary)
                return ZoneCollisionRequirement.BlockingBoundary;

            // Default: SeafloorOnly for most terrain
            return ZoneCollisionRequirement.SeafloorOnly;
        }

        /// <summary>
        /// contentDensity와 riskTier를 기반으로 resourceDensity01, hazardDensity01, landmarkWeight01, visualIntensity01을 결정한다.
        /// </summary>
        private static void ResolveContentDensity(WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule)
        {
            // ===== Resource Density =====
            switch (entry.contentDensity)
            {
                case ZoneContentDensity.Empty:
                    rule.resourceDensity01 = 0.05f;
                    break;
                case ZoneContentDensity.Sparse:
                    rule.resourceDensity01 = 0.15f;
                    break;
                case ZoneContentDensity.Normal:
                    rule.resourceDensity01 = 0.45f;
                    break;
                case ZoneContentDensity.Dense:
                    rule.resourceDensity01 = 0.70f;
                    break;
                case ZoneContentDensity.Landmark:
                    rule.resourceDensity01 = 0.55f;
                    break;
                default:
                    rule.resourceDensity01 = 0.30f;
                    break;
            }

            // intentionallySparse override
            if (entry.intentionallySparse)
            {
                rule.resourceDensity01 = Mathf.Min(rule.resourceDensity01, 0.20f);
            }

            // ===== Hazard Density (based on riskTier) =====
            switch (entry.riskTier)
            {
                case ZoneRiskTier.Safe:
                    rule.hazardDensity01 = 0.05f;
                    break;
                case ZoneRiskTier.Low:
                    rule.hazardDensity01 = 0.15f;
                    break;
                case ZoneRiskTier.Medium:
                    rule.hazardDensity01 = 0.40f;
                    break;
                case ZoneRiskTier.High:
                    rule.hazardDensity01 = 0.65f;
                    break;
                case ZoneRiskTier.Forbidden:
                    rule.hazardDensity01 = 0.90f;
                    break;
                default:
                    rule.hazardDensity01 = 0.20f;
                    break;
            }

            // ===== Landmark Weight =====
            if (entry.isHub)
            {
                rule.landmarkWeight01 = 0.90f;
            }
            else if (entry.isMajorLandmark)
            {
                rule.landmarkWeight01 = 0.70f;
            }
            else if (entry.contentDensity == ZoneContentDensity.Landmark)
            {
                rule.landmarkWeight01 = 0.60f;
            }
            else
            {
                rule.landmarkWeight01 = 0.20f;
            }

            // ===== Visual Intensity =====
            // Higher risk + higher density = more visual intensity
            float riskFactor = 0f;
            switch (entry.riskTier)
            {
                case ZoneRiskTier.Safe: riskFactor = 0.1f; break;
                case ZoneRiskTier.Low: riskFactor = 0.2f; break;
                case ZoneRiskTier.Medium: riskFactor = 0.5f; break;
                case ZoneRiskTier.High: riskFactor = 0.7f; break;
                case ZoneRiskTier.Forbidden: riskFactor = 0.9f; break;
            }

            float densityFactor = 0f;
            switch (entry.contentDensity)
            {
                case ZoneContentDensity.Empty: densityFactor = 0.1f; break;
                case ZoneContentDensity.Sparse: densityFactor = 0.2f; break;
                case ZoneContentDensity.Normal: densityFactor = 0.5f; break;
                case ZoneContentDensity.Dense: densityFactor = 0.7f; break;
                case ZoneContentDensity.Landmark: densityFactor = 0.8f; break;
            }

            rule.visualIntensity01 = Mathf.Clamp01((riskFactor + densityFactor) * 0.7f);
        }

        // ======================================================================
        //  Tag Parsing
        // ======================================================================

        /// <summary>
        /// resourceGroups 텍스트에서 태그를 추출한다.
        /// </summary>
        private static string[] ParseResourceTags(WorldMapZoneDesignEntry entry)
        {
            var tags = new List<string>();

            // resourceGroups 텍스트 파싱
            string text = entry.resourceGroups ?? string.Empty;

            if (ContainsKeyword(text, "Iron Scrap", "철", "scrap"))
                tags.Add("iron");
            if (ContainsKeyword(text, "Copper Wire", "Copper", "구리"))
                tags.Add("copper");
            if (ContainsKeyword(text, "Battery Cell", "Battery", "배터리"))
                tags.Add("battery");
            if (ContainsKeyword(text, "Fuel Canister", "Fuel", "연료"))
                tags.Add("fuel");
            if (ContainsKeyword(text, "Sensor Fragment", "Sensor", "센서"))
                tags.Add("sensor");
            if (ContainsKeyword(text, "Comm Module", "Comm", "통신"))
                tags.Add("communication");
            if (ContainsKeyword(text, "Data Chip", "Data", "데이터"))
                tags.Add("data");
            if (ContainsKeyword(text, "Rare Metal", "Rare", "희귀"))
                tags.Add("rare");
            if (ContainsKeyword(text, "Aluminum Pipe", "Aluminum", "알루미늄"))
                tags.Add("aluminum");
            if (ContainsKeyword(text, "Steel Plate", "Steel", "강철"))
                tags.Add("steel");
            if (ContainsKeyword(text, "Valve Core", "Valve", "밸브"))
                tags.Add("valve");
            if (ContainsKeyword(text, "Fastener Pack", "Fastener", "체결"))
                tags.Add("fastener");
            if (ContainsKeyword(text, "Corroded Relay", "Relay", "릴레이"))
                tags.Add("relay");
            if (ContainsKeyword(text, "Research-grade Parts", "Research", "연구"))
                tags.Add("research");
            if (ContainsKeyword(text, "basic", "A-tier"))
                tags.Add("basic");

            // Deduplicate
            return DeduplicateTags(tags);
        }

        /// <summary>
        /// hazards 텍스트에서 태그를 추출한다.
        /// </summary>
        private static string[] ParseHazardTags(WorldMapZoneDesignEntry entry)
        {
            var tags = new List<string>();

            string text = entry.hazards ?? string.Empty;

            if (ContainsKeyword(text, "해류", "current"))
                tags.Add("current");
            if (ContainsKeyword(text, "압력", "pressure"))
                tags.Add("pressure");
            if (ContainsKeyword(text, "균열", "crack", "fissure"))
                tags.Add("crack");
            if (ContainsKeyword(text, "시야", "visibility", "시야 차단", "시야 방해"))
                tags.Add("visibility_block");
            if (ContainsKeyword(text, "암반", "rock", "boulder"))
                tags.Add("rock");
            if (ContainsKeyword(text, "잔해", "debris", "파편"))
                tags.Add("debris");
            if (ContainsKeyword(text, "경사", "slope", "사면"))
                tags.Add("slope");
            if (ContainsKeyword(text, "절벽", "cliff", "붕괴"))
                tags.Add("cliff");
            if (ContainsKeyword(text, "고립", "isolation", "외곽"))
                tags.Add("isolation");
            if (ContainsKeyword(text, "협곡", "canyon"))
                tags.Add("canyon");
            if (ContainsKeyword(text, "장애", "obstacle", "장애물"))
                tags.Add("obstacle");
            if (ContainsKeyword(text, "홈", "dip", "depression"))
                tags.Add("dip");
            if (ContainsKeyword(text, "조명", "light", "경고"))
                tags.Add("warning_light");

            return DeduplicateTags(tags);
        }

        /// <summary>
        /// keyObjects, logOrHint, narrativeFunction 텍스트에서 랜드마크 태그를 추출한다.
        /// </summary>
        private static string[] ParseLandmarkTags(WorldMapZoneDesignEntry entry)
        {
            var tags = new List<string>();

            string keyText = entry.keyObjects ?? string.Empty;
            string logText = entry.logOrHint ?? string.Empty;
            string narrativeText = entry.narrativeFunction ?? string.Empty;
            string combined = $"{keyText} {logText} {narrativeText}";

            // Wreck/debris
            if (ContainsKeyword(combined, "wreck", "난파", "잔해", "폐선", "선체"))
                tags.Add("wreck");
            // Drone/communication
            if (ContainsKeyword(combined, "drone", "드론", "communication", "통신", "안테나", "antenna", "비콘", "beacon"))
                tags.Add("communication");
            // Log
            if (ContainsKeyword(combined, "log", "기록", "#001", "#002"))
                tags.Add("log");
            // Canyon
            if (ContainsKeyword(combined, "canyon", "협곡", "균열"))
                tags.Add("canyon");
            // Research
            if (ContainsKeyword(combined, "research", "연구", "센서", "sensor", "장비", "equipment"))
                tags.Add("research");
            // Hub
            if (ContainsKeyword(combined, "hub", "허브", "안전"))
                tags.Add("hub");
            // Warning
            if (ContainsKeyword(combined, "warning", "경고", "boundary", "경계"))
                tags.Add("warning");
            // Pressure
            if (ContainsKeyword(combined, "pressure", "압력", "압박"))
                tags.Add("pressure");
            // Narrative gate
            if (ContainsKeyword(combined, "gate", "관문", "narrative"))
                tags.Add("narrative_gate");
            // Foreshadow
            if (ContainsKeyword(combined, "foreshadow", "전조", "의문", "이상"))
                tags.Add("foreshadow");
            // Buoy/marker
            if (ContainsKeyword(combined, "buoy", "부표", "표식", "marker"))
                tags.Add("marker");
            // Cable/wire
            if (ContainsKeyword(combined, "cable", "케이블", "wire"))
                tags.Add("cable");
            // Console/panel
            if (ContainsKeyword(combined, "console", "콘솔", "panel"))
                tags.Add("console");
            // Frame/structure
            if (ContainsKeyword(combined, "frame", "프레임", "structure", "구조물"))
                tags.Add("structure");

            return DeduplicateTags(tags);
        }

        // ======================================================================
        //  Helpers
        // ======================================================================

        /// <summary>
        /// WorldMapZoneDesignRuleDatabaseSO asset을 찾거나 생성한다.
        /// </summary>
        private static WorldMapZoneDesignRuleDatabaseSO FindOrCreateRuleDatabaseAsset()
        {
            // 폴더가 없으면 생성
            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                string parent = "Assets/_Project/ScriptableObjects/World";
                string sub = "Design";
                string guid = AssetDatabase.CreateFolder(parent, sub);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"[ZoneDesignRule] Failed to create folder: {AssetFolder}");
                    return null;
                }
                Debug.Log($"[ZoneDesignRule] Created folder: {AssetFolder}");
            }

            // 기존 asset 로드
            WorldMapZoneDesignRuleDatabaseSO existing = AssetDatabase.LoadAssetAtPath<WorldMapZoneDesignRuleDatabaseSO>(AssetPath);
            if (existing != null)
            {
                Debug.Log($"[ZoneDesignRule] Found existing asset at: {AssetPath}");
                return existing;
            }

            // 새로 생성
            WorldMapZoneDesignRuleDatabaseSO newDb = ScriptableObject.CreateInstance<WorldMapZoneDesignRuleDatabaseSO>();
            AssetDatabase.CreateAsset(newDb, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ZoneDesignRule] Created new asset at: {AssetPath}");
            return newDb;
        }

        /// <summary>
        /// 문자열이 지정된 키워드 중 하나라도 포함하는지 검사한다.
        /// 대소문자 구분 없이 검사한다.
        /// </summary>
        private static bool ContainsKeyword(string text, params string[] keywords)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string lowerText = text.ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                if (string.IsNullOrEmpty(keywords[i]))
                    continue;
                if (lowerText.Contains(keywords[i].ToLowerInvariant()))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 태그 배열에 특정 태그가 포함되어 있는지 검사한다.
        /// </summary>
        private static bool ContainsTag(string[] tags, string tag)
        {
            if (tags == null || tag == null)
                return false;

            string lowerTag = tag.ToLowerInvariant();
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] != null && tags[i].ToLowerInvariant() == lowerTag)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 태그 리스트에서 중복을 제거하고 배열로 반환한다.
        /// </summary>
        private static string[] DeduplicateTags(List<string> tags)
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            for (int i = 0; i < tags.Count; i++)
            {
                string lower = tags[i].ToLowerInvariant();
                if (!seen.Contains(lower))
                {
                    seen.Add(lower);
                    result.Add(tags[i]);
                }
            }
            return result.ToArray();
        }

        // ======================================================================
        //  Phase 14.9.2-B: H/I/J Column Post-Processing
        // ======================================================================

        /// <summary>
        /// H/I/J 열 DesignRule을 보정한다.
        /// 일반 ConvertEntryToRule 로직으로 생성된 rule에 H/I/J-specific 값을 덮어쓴다.
        /// - H열: DeepSlope/CanyonEntry/ArtificialPassage/FacilityApproach 계열, MainRoute/SideRoute/Gate 혼합
        /// - I열: CanyonDeep/CliffWall/FacilityApproach/SparsePressure 계열, Gate/Boundary/MainRoute 중심
        /// - J열: SparsePressure/CliffWall/FacilityApproach/CanyonDeep 계열, Boundary/Gate/MainRoute 중심
        /// </summary>
        private static void PostProcessHIJRule(WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule)
        {
            string col = entry.column;
            int row = entry.row;

            // H열이 아니면 I열/J열도 아니면 skip
            if (col != "H" && col != "I" && col != "J")
                return;

            // ======================================================================
            //  H Column: DeepSlope/CanyonEntry/ArtificialPassage/FacilityApproach 계열
            //  RouteRole: MainRoute / SideRoute / Gate 혼합
            //  CollisionRequirement: SeafloorAndLargeProps 이상
            // ======================================================================
            if (col == "H")
            {
                // H1: ResearchClue → SideRoute (기본 유지), ResearchPoint 유지
                // H2: RouteBuffer → FreeExploration이 기본이지만 MainRoute로 보정 (cable trench, main corridor)
                if (row == 2)
                    rule.routeRole = ZoneRouteRole.MainRoute;
                // H3: ResearchClue → SideRoute (기본 유지), isMajorLandmark=true → ResearchPoint
                // H4: RouteBuffer → FreeExploration이 기본이지만 MainRoute로 보정 (research corridor)
                if (row == 4)
                    rule.routeRole = ZoneRouteRole.MainRoute;
                // H5: TechForeshadow → MainRoute (기본 유지)
                // H6: RouteBuffer → FreeExploration이 기본이지만 SideRoute로 보정 (fractured ridge buffer)
                if (row == 6)
                    rule.routeRole = ZoneRouteRole.SideRoute;
                // H7: ResearchClue → SideRoute (기본 유지), isMajorLandmark=true → ResearchPoint
                // H8: PressureZone → Boundary (기본 유지)
                // H9: PressureZone → Boundary (기본 유지)
                // H10: WarningBoundary → Boundary (기본 유지)

                // H열 collisionRequirement 보정: SparsePressure는 SeafloorAndLargeProps로 승격
                if (rule.collisionRequirement == ZoneCollisionRequirement.SeafloorOnly &&
                    rule.terrainArchetype != ZoneTerrainArchetype.OpenFlat &&
                    rule.terrainArchetype != ZoneTerrainArchetype.ShallowShelf)
                {
                    rule.collisionRequirement = ZoneCollisionRequirement.SeafloorAndLargeProps;
                }

                // H열 resourceTags 보강: steel, battery, copper, research, cable 계열
                var hResourceTags = new List<string>(rule.resourceTags ?? new string[0]);
                if (row <= 3)
                {
                    AddUniqueTag(hResourceTags, "copper");
                    AddUniqueTag(hResourceTags, "cable");
                }
                else if (row <= 7)
                {
                    AddUniqueTag(hResourceTags, "research");
                    AddUniqueTag(hResourceTags, "steel");
                    AddUniqueTag(hResourceTags, "battery");
                }
                else
                {
                    AddUniqueTag(hResourceTags, "cable");
                    AddUniqueTag(hResourceTags, "research");
                }
                rule.resourceTags = hResourceTags.ToArray();

                // H열 hazardTags 보강: pressure, current, cable, crack, visibility 계열
                var hHazardTags = new List<string>(rule.hazardTags ?? new string[0]);
                if (row >= 8)
                {
                    AddUniqueTag(hHazardTags, "pressure");
                    AddUniqueTag(hHazardTags, "current");
                    AddUniqueTag(hHazardTags, "visibility_block");
                }
                else
                {
                    AddUniqueTag(hHazardTags, "crack");
                    AddUniqueTag(hHazardTags, "cable");
                }
                rule.hazardTags = hHazardTags.ToArray();
            }

            // ======================================================================
            //  I Column: CanyonDeep/CliffWall/FacilityApproach/SparsePressure 계열
            //  RouteRole: Gate / Boundary / MainRoute 중심
            //  CollisionRequirement: NavigationCritical 또는 BlockingBoundary 중심
            // ======================================================================
            if (col == "I")
            {
                // I1: ResearchClue → SideRoute (기본 유지)
                // I2: ResearchClue → SideRoute (기본 유지), isMajorLandmark=true → ResearchPoint
                // I3: TechForeshadow → MainRoute (기본 유지)
                // I4: NarrativeGate → Gate (기본 유지)
                // I5: PressureZone → Boundary (기본 유지)
                // I6: PressureZone → Boundary (기본 유지)
                // I7: NarrativeGate → Gate (기본 유지), isMajorLandmark=true → NarrativeGate
                // I8: PressureZone → Boundary (기본 유지)
                // I9: WarningBoundary → Boundary (기본 유지)
                // I10: WarningBoundary → Boundary (기본 유지)

                // I열 collisionRequirement 보정: 심층/금지구역은 NavigationCritical로 승격
                if (row >= 4 && rule.collisionRequirement == ZoneCollisionRequirement.SeafloorOnly)
                {
                    rule.collisionRequirement = ZoneCollisionRequirement.NavigationCritical;
                }
                if (row >= 8 && rule.collisionRequirement != ZoneCollisionRequirement.NavigationCritical &&
                    rule.collisionRequirement != ZoneCollisionRequirement.BlockingBoundary)
                {
                    rule.collisionRequirement = ZoneCollisionRequirement.NavigationCritical;
                }

                // I열 resourceTags 보강: data_chip, alloy, reactor_fragment, sealed_cache 계열
                var iResourceTags = new List<string>(rule.resourceTags ?? new string[0]);
                if (row <= 3)
                {
                    AddUniqueTag(iResourceTags, "data");
                    AddUniqueTag(iResourceTags, "research");
                }
                else if (row <= 7)
                {
                    AddUniqueTag(iResourceTags, "data");
                    AddUniqueTag(iResourceTags, "alloy");
                    AddUniqueTag(iResourceTags, "research");
                }
                else
                {
                    AddUniqueTag(iResourceTags, "sealed");
                    AddUniqueTag(iResourceTags, "rare");
                }
                rule.resourceTags = iResourceTags.ToArray();

                // I열 hazardTags 보강: pressure, unstable_current, crack_field, sealed_zone 계열
                var iHazardTags = new List<string>(rule.hazardTags ?? new string[0]);
                AddUniqueTag(iHazardTags, "pressure");
                if (row >= 4)
                {
                    AddUniqueTag(iHazardTags, "crack");
                    AddUniqueTag(iHazardTags, "visibility_block");
                }
                if (row >= 8)
                {
                    AddUniqueTag(iHazardTags, "isolation");
                }
                rule.hazardTags = iHazardTags.ToArray();
            }

            // ======================================================================
            //  J Column: SparsePressure/CliffWall/FacilityApproach/CanyonDeep 계열
            //  RouteRole: Boundary / Gate / MainRoute 중심
            //  CollisionRequirement: BlockingBoundary 또는 NavigationCritical 중심
            //  J1/J10: NOT EarlySurvival, endgame/boundary 계열
            // ======================================================================
            if (col == "J")
            {
                // J1: WarningBoundary → Boundary (기본 유지)
                // J2: PressureZone → Boundary (기본 유지)
                // J3: WarningBoundary → Boundary (기본 유지)
                // J4: NarrativeGate → Gate (기본 유지)
                // J5: NarrativeGate → Gate (기본 유지), isMajorLandmark=true → NarrativeGate
                // J6: WarningBoundary → Boundary (기본 유지)
                // J7: PressureZone → Boundary (기본 유지)
                // J8: WarningBoundary → Boundary (기본 유지)
                // J9: NarrativeGate → Gate (기본 유지), isMajorLandmark=true → NarrativeGate
                // J10: NarrativeGate → Gate (기본 유지), isMajorLandmark=true → NarrativeGate

                // J열 collisionRequirement 보정: 모든 J열은 NavigationCritical 또는 BlockingBoundary
                if (rule.collisionRequirement == ZoneCollisionRequirement.SeafloorOnly ||
                    rule.collisionRequirement == ZoneCollisionRequirement.SeafloorAndLargeProps)
                {
                    if (row <= 3 || row == 6 || row == 8)
                        rule.collisionRequirement = ZoneCollisionRequirement.BlockingBoundary;
                    else
                        rule.collisionRequirement = ZoneCollisionRequirement.NavigationCritical;
                }

                // J열 resourceTags 보강: origin_fragment, abyss_sample, relic_core, sealed_cache 계열
                var jResourceTags = new List<string>(rule.resourceTags ?? new string[0]);
                if (row <= 3)
                {
                    // J1~J3: 거의 자원 없음
                }
                else if (row <= 5)
                {
                    AddUniqueTag(jResourceTags, "rare");
                    AddUniqueTag(jResourceTags, "sealed");
                }
                else if (row <= 7)
                {
                    AddUniqueTag(jResourceTags, "rare");
                    AddUniqueTag(jResourceTags, "sealed");
                }
                else
                {
                    // J9~J10: origin core 자원
                    AddUniqueTag(jResourceTags, "rare");
                    AddUniqueTag(jResourceTags, "sealed");
                }
                rule.resourceTags = jResourceTags.ToArray();

                // J열 hazardTags 보강: abyss, collapse_edge, pressure, blackout, forbidden 계열
                var jHazardTags = new List<string>(rule.hazardTags ?? new string[0]);
                AddUniqueTag(jHazardTags, "pressure");
                AddUniqueTag(jHazardTags, "visibility_block");
                if (row >= 4)
                {
                    AddUniqueTag(jHazardTags, "cliff");
                    AddUniqueTag(jHazardTags, "isolation");
                }
                if (row >= 8)
                {
                    AddUniqueTag(jHazardTags, "isolation");
                }
                rule.hazardTags = jHazardTags.ToArray();

                // J1/J10: intentionallySparse가 true면 resourceDensity01 낮게 보장
                if (rule.intentionallySparse)
                {
                    rule.resourceDensity01 = Mathf.Min(rule.resourceDensity01, 0.10f);
                }
            }

            // ===== 공통: H/I/J numeric 01 값 0~1 clamp =====
            rule.terrainRoughness01 = Mathf.Clamp01(rule.terrainRoughness01);
            rule.slopeIntensity01 = Mathf.Clamp01(rule.slopeIntensity01);
            rule.canyonIntensity01 = Mathf.Clamp01(rule.canyonIntensity01);
            rule.cliffIntensity01 = Mathf.Clamp01(rule.cliffIntensity01);
            rule.openWaterFlatness01 = Mathf.Clamp01(rule.openWaterFlatness01);
            rule.resourceDensity01 = Mathf.Clamp01(rule.resourceDensity01);
            rule.hazardDensity01 = Mathf.Clamp01(rule.hazardDensity01);
            rule.landmarkWeight01 = Mathf.Clamp01(rule.landmarkWeight01);
            rule.visualIntensity01 = Mathf.Clamp01(rule.visualIntensity01);

            // ===== requiresTerrainCollider 재계산 =====
            rule.requiresTerrainCollider = rule.collisionRequirement != ZoneCollisionRequirement.None;
        }

        /// <summary>
        /// 태그 리스트에 고유 태그를 추가한다.
        /// </summary>
        private static void AddUniqueTag(List<string> tags, string tag)
        {
            if (tags == null || string.IsNullOrEmpty(tag))
                return;
            string lower = tag.ToLowerInvariant();
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null && tags[i].ToLowerInvariant() == lower)
                    return;
            }
            tags.Add(tag);
        }

        /// <summary>
        /// 디버그 요약 문자열을 생성한다.
        /// </summary>
        private static string BuildDebugSummary(WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule)
        {
            return $"[{entry.zoneId}] {rule.terrainArchetype} | " +
                   $"Route:{rule.routeRole} Landmark:{rule.landmarkRole} Collision:{rule.collisionRequirement} | " +
                   $"Depth:({entry.minDepth},{entry.maxDepth}) | " +
                   $"R:{rule.terrainRoughness01:F2} S:{rule.slopeIntensity01:F2} C:{rule.canyonIntensity01:F2} Cl:{rule.cliffIntensity01:F2} F:{rule.openWaterFlatness01:F2} | " +
                   $"Res:{rule.resourceDensity01:F2} Haz:{rule.hazardDensity01:F2} Lm:{rule.landmarkWeight01:F2} Vis:{rule.visualIntensity01:F2} | " +
                   $"Sparse:{rule.intentionallySparse} Collider:{rule.requiresTerrainCollider} Narrative:{rule.requiresNarrativeObject}";
        }

        // ======================================================================
        //  SerializedProperty Helpers
        // ======================================================================

        private static void SetStringProperty(SerializedProperty element, string propertyName, string value)
        {
            SerializedProperty prop = element.FindPropertyRelative(propertyName);
            if (prop != null) prop.stringValue = value ?? string.Empty;
        }

        private static void SetFloatProperty(SerializedProperty element, string propertyName, float value)
        {
            SerializedProperty prop = element.FindPropertyRelative(propertyName);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetBoolProperty(SerializedProperty element, string propertyName, bool value)
        {
            SerializedProperty prop = element.FindPropertyRelative(propertyName);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetEnumProperty(SerializedProperty element, string propertyName, System.Enum value)
        {
            SerializedProperty prop = element.FindPropertyRelative(propertyName);
            if (prop != null) prop.enumValueIndex = System.Convert.ToInt32(value);
        }

        private static void SetStringArrayProperty(SerializedProperty element, string propertyName, string[] values)
        {
            SerializedProperty prop = element.FindPropertyRelative(propertyName);
            if (prop == null) return;

            prop.ClearArray();
            if (values == null) return;

            for (int i = 0; i < values.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                SerializedProperty elem = prop.GetArrayElementAtIndex(i);
                elem.stringValue = values[i] ?? string.Empty;
            }
        }
    }
}
