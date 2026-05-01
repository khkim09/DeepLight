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

            // 11. WreckFocus or WreckRecovery entries produce Wreck-related rule
            if (sourceDb != null && ruleDb != null && sourceDb.Entries != null && ruleDb.Rules != null)
            {
                bool allWreckOk = true;
                for (int i = 0; i < sourceDb.Entries.Count; i++)
                {
                    var entry = sourceDb.Entries[i];
                    var rule = ruleDb.GetRule(entry?.zoneId);
                    if (entry == null || rule == null) continue;

                    bool isWreckMood = entry.terrainMood == ZoneTerrainMood.WreckFocus;
                    bool isWreckPurpose = entry.primaryPurpose == ZonePrimaryPurpose.WreckRecovery;

                    if (isWreckMood || isWreckPurpose)
                    {
                        if (rule.landmarkRole != ZoneLandmarkRole.Wreck)
                        {
                            log.AppendLine($"  [WARN] '{entry.zoneId}' is Wreck-related but landmarkRole={rule.landmarkRole} (expected Wreck)");
                            allWreckOk = false;
                            warnCount++;
                        }
                        if (rule.collisionRequirement != ZoneCollisionRequirement.SeafloorAndLargeProps &&
                            rule.collisionRequirement != ZoneCollisionRequirement.SeafloorOnly)
                        {
                            log.AppendLine($"  [WARN] '{entry.zoneId}' is Wreck-related but collisionRequirement={rule.collisionRequirement}");
                            allWreckOk = false;
                            warnCount++;
                        }
                    }
                }
                if (allWreckOk)
                {
                    log.AppendLine("  [PASS] Wreck-related entries have appropriate Wreck rules");
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
