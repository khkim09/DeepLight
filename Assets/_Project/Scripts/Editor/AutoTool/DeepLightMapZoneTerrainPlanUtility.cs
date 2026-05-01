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
    /// WorldMapZoneTerrainPlan 생성을 위한 Editor 전용 유틸리티.
    /// Phase 14.3: WorldMapZoneDesignRuleDatabaseSO의 정형화된 rule을
    /// 실제 지형 생성/배치에 사용할 수 있는 구체적인 Terrain Plan(WorldMapZoneTerrainPlan)으로 변환한다.
    ///
    /// [설계 원칙]
    /// - 기존 asset이 있으면 재사용하고 plans를 갱신한다.
    /// - 덮어쓰기 전에 null 체크를 수행한다.
    /// - 기존 asset을 삭제하지 않고 SerializedObject로 안전하게 갱신한다.
    /// - Scene 오브젝트를 전혀 생성하지 않는다.
    /// - 실제 terrain mesh/prop 배치/맵 오브젝트 생성은 하지 않는다.
    /// </summary>
    public static class DeepLightMapZoneTerrainPlanUtility
    {
        private const string AssetPath = "Assets/_Project/ScriptableObjects/World/Design/WorldMapZoneTerrainPlanDatabase.asset";
        private const string AssetFolder = "Assets/_Project/ScriptableObjects/World/Design";

        // ======================================================================
        //  Public API
        // ======================================================================

        /// <summary>
        /// Zone Terrain Plan Database를 재구축한다.
        /// WorldMapZoneDesignRuleDatabaseSO를 읽고 각 rule을 WorldMapZoneTerrainPlan로 변환한다.
        /// </summary>
        public static void RebuildZoneTerrainPlans(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ZoneTerrainPlan] Settings is null! Cannot rebuild zone terrain plans.");
                return;
            }

            // 1. Source Rule Database 확인
            WorldMapZoneDesignRuleDatabaseSO ruleDb = settings.ZoneDesignRuleDatabase;
            if (ruleDb == null)
            {
                Debug.LogError("[ZoneTerrainPlan] ZoneDesignRuleDatabase is null in Settings! Run Phase 14.2 first.");
                return;
            }

            if (ruleDb.Rules == null || ruleDb.Rules.Count == 0)
            {
                Debug.LogError("[ZoneTerrainPlan] ZoneDesignRuleDatabase has no rules! Run Phase 14.2 first.");
                return;
            }

            Debug.Log($"[ZoneTerrainPlan] Source rule database has {ruleDb.Rules.Count} rules. Converting to terrain plans...");

            // 2. 각 Rule을 TerrainPlan으로 변환
            var plans = new List<WorldMapZoneTerrainPlan>();
            for (int i = 0; i < ruleDb.Rules.Count; i++)
            {
                WorldMapZoneDesignRule rule = ruleDb.Rules[i];
                if (rule == null)
                {
                    Debug.LogWarning($"[ZoneTerrainPlan] Rule at index {i} is null. Skipping.");
                    continue;
                }

                WorldMapZoneTerrainPlan plan = ConvertRuleToPlan(rule, settings);
                plans.Add(plan);

                if (settings.LogZoneTerrainPlanVerbose)
                {
                    Debug.Log($"[ZoneTerrainPlan] Converted {rule.zoneId}: " +
                              $"surface={plan.baseSurfaceMode} seabed={plan.seabedShapeMode} " +
                              $"route={plan.routeShapeMode} boundary={plan.boundaryMode} " +
                              $"collider={plan.colliderMode} depth={plan.targetCenterDepth}");
                }
            }

            // 3. Terrain Plan Database asset 찾기 또는 생성
            WorldMapZoneTerrainPlanDatabaseSO planDb = FindOrCreatePlanDatabaseAsset();
            if (planDb == null)
            {
                Debug.LogError("[ZoneTerrainPlan] Failed to find or create ZoneTerrainPlanDatabase asset.");
                return;
            }

            // 4. SerializedObject로 plans 갱신
            SerializedObject serializedDb = new SerializedObject(planDb);
            SerializedProperty plansProp = serializedDb.FindProperty("plans");

            plansProp.ClearArray();
            for (int i = 0; i < plans.Count; i++)
            {
                plansProp.InsertArrayElementAtIndex(i);
                SerializedProperty element = plansProp.GetArrayElementAtIndex(i);

                SetStringProperty(element, "zoneId", plans[i].zoneId);

                SetEnumProperty(element, "baseSurfaceMode", plans[i].baseSurfaceMode);
                SetEnumProperty(element, "seabedShapeMode", plans[i].seabedShapeMode);
                SetEnumProperty(element, "routeShapeMode", plans[i].routeShapeMode);
                SetEnumProperty(element, "boundaryMode", plans[i].boundaryMode);
                SetEnumProperty(element, "colliderMode", plans[i].colliderMode);

                SetFloatProperty(element, "targetCenterDepth", plans[i].targetCenterDepth);
                SetFloatProperty(element, "depthRange", plans[i].depthRange);
                SetFloatProperty(element, "floorHeight", plans[i].floorHeight);
                SetFloatProperty(element, "ceilingHeight", plans[i].ceilingHeight);

                SetFloatProperty(element, "slopeScale", plans[i].slopeScale);
                SetFloatProperty(element, "roughnessScale", plans[i].roughnessScale);
                SetFloatProperty(element, "canyonWidth01", plans[i].canyonWidth01);
                SetFloatProperty(element, "canyonDepth01", plans[i].canyonDepth01);
                SetFloatProperty(element, "cliffHeight01", plans[i].cliffHeight01);
                SetFloatProperty(element, "flatAreaWeight01", plans[i].flatAreaWeight01);

                SetFloatProperty(element, "navigationCorridorWidth01", plans[i].navigationCorridorWidth01);
                SetFloatProperty(element, "obstacleDensity01", plans[i].obstacleDensity01);
                SetFloatProperty(element, "landmarkPlacementWeight01", plans[i].landmarkPlacementWeight01);
                SetFloatProperty(element, "resourceSpawnWeight01", plans[i].resourceSpawnWeight01);
                SetFloatProperty(element, "hazardSpawnWeight01", plans[i].hazardSpawnWeight01);

                SetIntProperty(element, "recommendedLargePropCount", plans[i].recommendedLargePropCount);
                SetIntProperty(element, "recommendedSmallPropCount", plans[i].recommendedSmallPropCount);

                SetBoolProperty(element, "requiresMainRouteClearance", plans[i].requiresMainRouteClearance);
                SetBoolProperty(element, "requiresBoundaryBlocker", plans[i].requiresBoundaryBlocker);
                SetBoolProperty(element, "requiresSeafloorCollider", plans[i].requiresSeafloorCollider);
                SetBoolProperty(element, "allowsDecorativeOnlySurface", plans[i].allowsDecorativeOnlySurface);

                SetStringArrayProperty(element, "terrainTags", plans[i].terrainTags);
                SetStringArrayProperty(element, "propTags", plans[i].propTags);
                SetStringArrayProperty(element, "hazardTags", plans[i].hazardTags);

                SetStringProperty(element, "debugSummary", plans[i].debugSummary);
            }

            serializedDb.ApplyModifiedProperties();
            EditorUtility.SetDirty(planDb);
            AssetDatabase.SaveAssets();

            // 5. SettingsSO에 참조 연결
            if (settings.ZoneTerrainPlanDatabase != planDb)
            {
                SerializedObject serializedSettings = new SerializedObject(settings);
                SerializedProperty dbProp = serializedSettings.FindProperty("zoneTerrainPlanDatabase");
                if (dbProp != null)
                {
                    dbProp.objectReferenceValue = planDb;
                    serializedSettings.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    Debug.Log("[ZoneTerrainPlan] ZoneTerrainPlanDatabase reference linked to SettingsSO.");
                }
            }

            Debug.Log($"[ZoneTerrainPlan] Phase 14.3: Zone Terrain Plan rebuild complete. {plans.Count} plans generated.");
        }

        /// <summary>
        /// Zone Terrain Plan Database의 유효성을 검사한다.
        /// 21개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneTerrainPlans(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ZoneTerrainPlan] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Zone Terrain Plan Validation =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. ZoneDesignRuleDatabase exists
            WorldMapZoneDesignRuleDatabaseSO ruleDb = settings.ZoneDesignRuleDatabase;
            if (ruleDb == null)
            {
                log.AppendLine("  [FAIL] ZoneDesignRuleDatabase is null! Run Phase 14.2 first.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] ZoneDesignRuleDatabase exists: {ruleDb.name} ({ruleDb.Rules?.Count ?? 0} rules)");
                passCount++;
            }

            // 2. ZoneTerrainPlanDatabase exists
            WorldMapZoneTerrainPlanDatabaseSO planDb = settings.ZoneTerrainPlanDatabase;
            if (planDb == null)
            {
                log.AppendLine("  [FAIL] ZoneTerrainPlanDatabase is null! Run RebuildZoneTerrainPlans first.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] ZoneTerrainPlanDatabase exists: {planDb.name} ({planDb.Plans?.Count ?? 0} plans)");
                passCount++;
            }

            // 3. plan count == rule count
            if (ruleDb != null && planDb != null)
            {
                int ruleCount = ruleDb.Rules?.Count ?? 0;
                int planCount = planDb.Plans?.Count ?? 0;
                if (planCount == ruleCount)
                {
                    log.AppendLine($"  [PASS] Plan count ({planCount}) == Rule count ({ruleCount})");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Plan count ({planCount}) != Rule count ({ruleCount})");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] Cannot compare plan count vs rule count (one or both databases null)");
                warnCount++;
            }

            // 4. all zone ids unique
            if (planDb != null)
            {
                bool unique = planDb.ValidateUniqueZoneIds();
                if (unique)
                {
                    log.AppendLine("  [PASS] All zone IDs are unique in TerrainPlanDatabase");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] Duplicate zone IDs found in TerrainPlanDatabase");
                    failCount++;
                }
            }

            // 5. A1~J10 plan exists (Phase 14.9 full) or A1~C10 (Phase 14.1 legacy)
            if (planDb != null)
            {
                bool allPresent = true;
                int planCount = planDb.Plans?.Count ?? 0;
                char[] columns = (planCount >= 100)
                    ? new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J' }
                    : new char[] { 'A', 'B', 'C' };

                for (int c = 0; c < columns.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string zoneId = $"{columns[c]}{r}";
                        if (planDb.GetPlan(zoneId) == null)
                        {
                            log.AppendLine($"  [FAIL] Required plan missing: '{zoneId}'");
                            allPresent = false;
                            failCount++;
                        }
                    }
                }
                if (allPresent)
                {
                    log.AppendLine($"  [PASS] All {new string(columns)} plans exist");
                    passCount++;
                }
            }

            // 6. every plan has valid baseSurfaceMode
            if (planDb != null && planDb.Plans != null)
            {
                bool allValid = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    if (planDb.Plans[i] == null)
                    {
                        log.AppendLine($"  [FAIL] Plan at index {i} is null");
                        allValid = false;
                        failCount++;
                        continue;
                    }
                    var mode = planDb.Plans[i].baseSurfaceMode;
                    if (!System.Enum.IsDefined(typeof(ZoneBaseSurfaceMode), mode))
                    {
                        log.AppendLine($"  [FAIL] Plan '{planDb.Plans[i].zoneId}' has invalid baseSurfaceMode: {mode}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All plans have valid baseSurfaceMode");
                    passCount++;
                }
            }

            // 7. every plan has valid seabedShapeMode
            if (planDb != null && planDb.Plans != null)
            {
                bool allValid = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    if (planDb.Plans[i] == null) continue;
                    var mode = planDb.Plans[i].seabedShapeMode;
                    if (!System.Enum.IsDefined(typeof(ZoneSeabedShapeMode), mode))
                    {
                        log.AppendLine($"  [FAIL] Plan '{planDb.Plans[i].zoneId}' has invalid seabedShapeMode: {mode}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All plans have valid seabedShapeMode");
                    passCount++;
                }
            }

            // 8. every plan has valid routeShapeMode
            if (planDb != null && planDb.Plans != null)
            {
                bool allValid = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    if (planDb.Plans[i] == null) continue;
                    var mode = planDb.Plans[i].routeShapeMode;
                    if (!System.Enum.IsDefined(typeof(ZoneRouteShapeMode), mode))
                    {
                        log.AppendLine($"  [FAIL] Plan '{planDb.Plans[i].zoneId}' has invalid routeShapeMode: {mode}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All plans have valid routeShapeMode");
                    passCount++;
                }
            }

            // 9. every plan has valid boundaryMode
            if (planDb != null && planDb.Plans != null)
            {
                bool allValid = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    if (planDb.Plans[i] == null) continue;
                    var mode = planDb.Plans[i].boundaryMode;
                    if (!System.Enum.IsDefined(typeof(ZoneBoundaryMode), mode))
                    {
                        log.AppendLine($"  [FAIL] Plan '{planDb.Plans[i].zoneId}' has invalid boundaryMode: {mode}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All plans have valid boundaryMode");
                    passCount++;
                }
            }

            // 10. every plan has valid colliderMode
            if (planDb != null && planDb.Plans != null)
            {
                bool allValid = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    if (planDb.Plans[i] == null) continue;
                    var mode = planDb.Plans[i].colliderMode;
                    if (!System.Enum.IsDefined(typeof(ZoneTerrainColliderMode), mode))
                    {
                        log.AppendLine($"  [FAIL] Plan '{planDb.Plans[i].zoneId}' has invalid colliderMode: {mode}");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All plans have valid colliderMode");
                    passCount++;
                }
            }

            // 11. all normalized values are in 0~1
            if (planDb != null && planDb.Plans != null)
            {
                bool allInRange = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    var plan = planDb.Plans[i];
                    if (plan == null) continue;

                    if (plan.slopeScale < 0f || plan.slopeScale > 1f ||
                        plan.roughnessScale < 0f || plan.roughnessScale > 1f ||
                        plan.canyonWidth01 < 0f || plan.canyonWidth01 > 1f ||
                        plan.canyonDepth01 < 0f || plan.canyonDepth01 > 1f ||
                        plan.cliffHeight01 < 0f || plan.cliffHeight01 > 1f ||
                        plan.flatAreaWeight01 < 0f || plan.flatAreaWeight01 > 1f ||
                        plan.navigationCorridorWidth01 < 0f || plan.navigationCorridorWidth01 > 1f ||
                        plan.obstacleDensity01 < 0f || plan.obstacleDensity01 > 1f ||
                        plan.landmarkPlacementWeight01 < 0f || plan.landmarkPlacementWeight01 > 1f ||
                        plan.resourceSpawnWeight01 < 0f || plan.resourceSpawnWeight01 > 1f ||
                        plan.hazardSpawnWeight01 < 0f || plan.hazardSpawnWeight01 > 1f)
                    {
                        log.AppendLine($"  [FAIL] Plan '{plan.zoneId}' has normalized values outside 0~1 range");
                        allInRange = false;
                        failCount++;
                    }
                }
                if (allInRange)
                {
                    log.AppendLine("  [PASS] All normalized values are in 0~1 range");
                    passCount++;
                }
            }

            // 12. depthRange is positive
            if (planDb != null && planDb.Plans != null)
            {
                bool allPositive = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    var plan = planDb.Plans[i];
                    if (plan == null) continue;

                    if (plan.depthRange <= 0f)
                    {
                        log.AppendLine($"  [FAIL] Plan '{plan.zoneId}' has non-positive depthRange: {plan.depthRange}");
                        allPositive = false;
                        failCount++;
                    }
                }
                if (allPositive)
                {
                    log.AppendLine("  [PASS] All depthRange values are positive");
                    passCount++;
                }
            }

            // 13. floorHeight is deeper than or equal to ceilingHeight
            if (planDb != null && planDb.Plans != null)
            {
                bool allValid = true;
                for (int i = 0; i < planDb.Plans.Count; i++)
                {
                    var plan = planDb.Plans[i];
                    if (plan == null) continue;

                    // floorHeight는 더 깊은 값(더 음수), ceilingHeight는 더 얕은 값(덜 음수)
                    if (plan.floorHeight > plan.ceilingHeight)
                    {
                        log.AppendLine($"  [FAIL] Plan '{plan.zoneId}' floorHeight ({plan.floorHeight}) > ceilingHeight ({plan.ceilingHeight})");
                        allValid = false;
                        failCount++;
                    }
                }
                if (allValid)
                {
                    log.AppendLine("  [PASS] All floorHeight <= ceilingHeight");
                    passCount++;
                }
            }

            // 14. WreckField rules produce WreckDepression plan
            if (ruleDb != null && planDb != null)
            {
                bool allWreckOk = true;
                for (int i = 0; i < (ruleDb.Rules?.Count ?? 0); i++)
                {
                    var rule = ruleDb.Rules?[i];
                    if (rule == null) continue;

                    if (rule.terrainArchetype == ZoneTerrainArchetype.WreckField)
                    {
                        var plan = planDb.GetPlan(rule.zoneId);
                        if (plan == null)
                        {
                            log.AppendLine($"  [FAIL] WreckField rule '{rule.zoneId}' has no corresponding plan");
                            allWreckOk = false;
                            failCount++;
                        }
                        else if (plan.seabedShapeMode != ZoneSeabedShapeMode.WreckDepression)
                        {
                            log.AppendLine($"  [FAIL] WreckField rule '{rule.zoneId}' produced seabedShapeMode={plan.seabedShapeMode} (expected WreckDepression)");
                            allWreckOk = false;
                            failCount++;
                        }
                    }
                }
                if (allWreckOk)
                {
                    log.AppendLine("  [PASS] WreckField rules produce WreckDepression plan");
                    passCount++;
                }
            }

            // 15. CanyonEntry/CanyonDeep rules produce canyon-related plan
            if (ruleDb != null && planDb != null)
            {
                bool allCanyonOk = true;
                for (int i = 0; i < (ruleDb.Rules?.Count ?? 0); i++)
                {
                    var rule = ruleDb.Rules?[i];
                    if (rule == null) continue;

                    if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry ||
                        rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep)
                    {
                        var plan = planDb.GetPlan(rule.zoneId);
                        if (plan == null)
                        {
                            log.AppendLine($"  [FAIL] Canyon rule '{rule.zoneId}' has no corresponding plan");
                            allCanyonOk = false;
                            failCount++;
                        }
                        else if (plan.seabedShapeMode != ZoneSeabedShapeMode.CanyonCut &&
                                 plan.seabedShapeMode != ZoneSeabedShapeMode.DeepCanyon)
                        {
                            log.AppendLine($"  [FAIL] Canyon rule '{rule.zoneId}' produced seabedShapeMode={plan.seabedShapeMode} (expected CanyonCut/DeepCanyon)");
                            allCanyonOk = false;
                            failCount++;
                        }
                    }
                }
                if (allCanyonOk)
                {
                    log.AppendLine("  [PASS] CanyonEntry/CanyonDeep rules produce canyon-related plan");
                    passCount++;
                }
            }

            // 16. CliffWall rules produce boundary/collider-heavy plan
            if (ruleDb != null && planDb != null)
            {
                bool allCliffOk = true;
                for (int i = 0; i < (ruleDb.Rules?.Count ?? 0); i++)
                {
                    var rule = ruleDb.Rules?[i];
                    if (rule == null) continue;

                    if (rule.terrainArchetype == ZoneTerrainArchetype.CliffWall)
                    {
                        var plan = planDb.GetPlan(rule.zoneId);
                        if (plan == null)
                        {
                            log.AppendLine($"  [FAIL] CliffWall rule '{rule.zoneId}' has no corresponding plan");
                            allCliffOk = false;
                            failCount++;
                        }
                        else
                        {
                            bool hasBoundary = plan.boundaryMode == ZoneBoundaryMode.TerrainWall ||
                                               plan.boundaryMode == ZoneBoundaryMode.DeepPressureBoundary;
                            bool hasCollider = plan.colliderMode == ZoneTerrainColliderMode.HardBoundary ||
                                               plan.colliderMode == ZoneTerrainColliderMode.NavigationCritical;
                            if (!hasBoundary)
                            {
                                log.AppendLine($"  [WARN] CliffWall rule '{rule.zoneId}' boundaryMode={plan.boundaryMode} (expected TerrainWall/DeepPressureBoundary)");
                                allCliffOk = false;
                                warnCount++;
                            }
                            if (!hasCollider)
                            {
                                log.AppendLine($"  [WARN] CliffWall rule '{rule.zoneId}' colliderMode={plan.colliderMode} (expected HardBoundary/NavigationCritical)");
                                allCliffOk = false;
                                warnCount++;
                            }
                        }
                    }
                }
                if (allCliffOk)
                {
                    log.AppendLine("  [PASS] CliffWall rules produce boundary/collider-heavy plan");
                    passCount++;
                }
            }

            // 17. intentionally sparse zones have low resourceSpawnWeight01
            if (ruleDb != null && planDb != null)
            {
                bool allSparseOk = true;
                for (int i = 0; i < (ruleDb.Rules?.Count ?? 0); i++)
                {
                    var rule = ruleDb.Rules?[i];
                    if (rule == null) continue;

                    if (rule.intentionallySparse)
                    {
                        var plan = planDb.GetPlan(rule.zoneId);
                        if (plan != null && plan.resourceSpawnWeight01 > 0.35f)
                        {
                            log.AppendLine($"  [WARN] Plan '{rule.zoneId}' is intentionallySparse but resourceSpawnWeight01={plan.resourceSpawnWeight01:F2} (>0.35)");
                            allSparseOk = false;
                            warnCount++;
                        }
                    }
                }
                if (allSparseOk)
                {
                    log.AppendLine("  [PASS] All intentionallySparse zones have low resourceSpawnWeight01");
                    passCount++;
                }
            }

            // 18. collision-relevant rules produce requiresSeafloorCollider=true
            if (ruleDb != null && planDb != null)
            {
                bool allCollisionOk = true;
                for (int i = 0; i < (ruleDb.Rules?.Count ?? 0); i++)
                {
                    var rule = ruleDb.Rules?[i];
                    if (rule == null) continue;

                    if (rule.collisionRequirement != ZoneCollisionRequirement.None)
                    {
                        var plan = planDb.GetPlan(rule.zoneId);
                        if (plan != null && !plan.requiresSeafloorCollider)
                        {
                            log.AppendLine($"  [WARN] Plan '{rule.zoneId}' has collisionRequirement={rule.collisionRequirement} but requiresSeafloorCollider=false");
                            allCollisionOk = false;
                            warnCount++;
                        }
                    }
                }
                if (allCollisionOk)
                {
                    log.AppendLine("  [PASS] Collision-relevant rules produce requiresSeafloorCollider=true");
                    passCount++;
                }
            }

            // 19. MapSettings preserved
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

            // 20. _WorldMap_Manual preserved
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

            // 21. DeepLightMapAutoBuilderContext preserved
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
        //  Rule → Plan Conversion
        // ======================================================================

        /// <summary>
        /// WorldMapZoneDesignRule 하나를 WorldMapZoneTerrainPlan로 변환한다.
        /// 변환 규칙은 작업 정의서의 Rule → Plan 변환 규칙을 따른다.
        /// </summary>
        private static WorldMapZoneTerrainPlan ConvertRuleToPlan(WorldMapZoneDesignRule rule, DeepLightMapAutoBuilderSettingsSO settings)
        {
            var plan = new WorldMapZoneTerrainPlan();

            // ===== Identity =====
            plan.zoneId = rule.zoneId;

            // ===== Surface & Shape Modes =====
            ResolveSurfaceAndShapeModes(rule, plan);

            // ===== Route Shape Mode =====
            ResolveRouteShapeMode(rule, plan);

            // ===== Boundary Mode =====
            ResolveBoundaryMode(rule, plan);

            // ===== Collider Mode =====
            ResolveColliderMode(rule, plan);

            // ===== Depth =====
            ResolveDepth(rule, plan);

            // ===== Terrain Profile =====
            ResolveTerrainProfile(rule, plan);

            // ===== Navigation & Content =====
            ResolveNavigationAndContent(rule, plan);

            // ===== Prop Counts =====
            ResolvePropCounts(rule, plan);

            // ===== Flags =====
            plan.requiresMainRouteClearance = (rule.routeRole == ZoneRouteRole.MainRoute || rule.routeRole == ZoneRouteRole.Gate);
            plan.requiresBoundaryBlocker = (plan.boundaryMode == ZoneBoundaryMode.TerrainWall ||
                                            plan.boundaryMode == ZoneBoundaryMode.DeepPressureBoundary ||
                                            plan.boundaryMode == ZoneBoundaryMode.LockedNarrativeBoundary);
            plan.requiresSeafloorCollider = (plan.colliderMode != ZoneTerrainColliderMode.None);
            plan.allowsDecorativeOnlySurface = (rule.collisionRequirement == ZoneCollisionRequirement.None);

            // ===== Tags =====
            ResolveTags(rule, plan);

            // ===== Debug Summary =====
            plan.debugSummary = BuildDebugSummary(rule, plan);

            return plan;
        }

        /// <summary>
        /// terrainArchetype을 기반으로 baseSurfaceMode와 seabedShapeMode를 결정한다.
        /// </summary>
        private static void ResolveSurfaceAndShapeModes(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            switch (rule.terrainArchetype)
            {
                case ZoneTerrainArchetype.OpenFlat:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.OpenWater;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.Flat;
                    break;

                case ZoneTerrainArchetype.ShallowShelf:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.ShallowShelf;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.GentleSlope;
                    break;

                case ZoneTerrainArchetype.GentleSlope:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.RollingSeabed;
                    break;

                case ZoneTerrainArchetype.DebrisField:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.DebrisScattered;
                    break;

                case ZoneTerrainArchetype.WreckField:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.WreckDepression;
                    break;

                case ZoneTerrainArchetype.CanyonEntry:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.CanyonCut;
                    break;

                case ZoneTerrainArchetype.CanyonDeep:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                    break;

                case ZoneTerrainArchetype.CliffWall:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.CliffDrop;
                    break;

                case ZoneTerrainArchetype.ArtificialPassage:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.ArtificialArea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                    break;

                case ZoneTerrainArchetype.FacilityApproach:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.ArtificialArea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                    break;

                case ZoneTerrainArchetype.SparsePressure:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.SparseVoid;
                    break;

                default:
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.OpenWater;
                    plan.seabedShapeMode = ZoneSeabedShapeMode.Flat;
                    break;
            }
        }

        /// <summary>
        /// routeRole과 terrainArchetype을 기반으로 routeShapeMode를 결정한다.
        /// </summary>
        private static void ResolveRouteShapeMode(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            // CanyonEntry → NarrowPassage 또는 BranchingPath
            if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry)
            {
                plan.routeShapeMode = (rule.canyonIntensity01 > 0.6f)
                    ? ZoneRouteShapeMode.NarrowPassage
                    : ZoneRouteShapeMode.BranchingPath;
                return;
            }

            // CanyonDeep → NarrowPassage
            if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep)
            {
                plan.routeShapeMode = ZoneRouteShapeMode.NarrowPassage;
                return;
            }

            // ArtificialPassage → WideMainPath 또는 NarrowPassage
            if (rule.terrainArchetype == ZoneTerrainArchetype.ArtificialPassage)
            {
                plan.routeShapeMode = (rule.openWaterFlatness01 > 0.7f)
                    ? ZoneRouteShapeMode.WideMainPath
                    : ZoneRouteShapeMode.NarrowPassage;
                return;
            }

            // routeRole 기반 매핑
            switch (rule.routeRole)
            {
                case ZoneRouteRole.FreeExploration:
                    plan.routeShapeMode = ZoneRouteShapeMode.FreeRoam;
                    break;
                case ZoneRouteRole.MainRoute:
                    plan.routeShapeMode = ZoneRouteShapeMode.WideMainPath;
                    break;
                case ZoneRouteRole.SideRoute:
                    plan.routeShapeMode = ZoneRouteShapeMode.BranchingPath;
                    break;
                case ZoneRouteRole.Gate:
                    plan.routeShapeMode = ZoneRouteShapeMode.DeadEndClue;
                    break;
                case ZoneRouteRole.Boundary:
                    plan.routeShapeMode = ZoneRouteShapeMode.BoundaryEdge;
                    break;
                case ZoneRouteRole.HubApproach:
                    plan.routeShapeMode = ZoneRouteShapeMode.HubApproach;
                    break;
                default:
                    plan.routeShapeMode = ZoneRouteShapeMode.FreeRoam;
                    break;
            }
        }

        /// <summary>
        /// terrainArchetype과 routeRole을 기반으로 boundaryMode를 결정한다.
        /// </summary>
        private static void ResolveBoundaryMode(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            // CliffWall → TerrainWall 또는 DeepPressureBoundary
            if (rule.terrainArchetype == ZoneTerrainArchetype.CliffWall)
            {
                plan.boundaryMode = (rule.cliffIntensity01 > 0.6f)
                    ? ZoneBoundaryMode.DeepPressureBoundary
                    : ZoneBoundaryMode.TerrainWall;
                return;
            }

            // SparsePressure → SoftVisualBoundary 또는 DeepPressureBoundary
            if (rule.terrainArchetype == ZoneTerrainArchetype.SparsePressure)
            {
                plan.boundaryMode = (rule.hazardDensity01 > 0.6f)
                    ? ZoneBoundaryMode.DeepPressureBoundary
                    : ZoneBoundaryMode.SoftVisualBoundary;
                return;
            }

            // Boundary route role → TerrainWall
            if (rule.routeRole == ZoneRouteRole.Boundary)
            {
                plan.boundaryMode = ZoneBoundaryMode.TerrainWall;
                return;
            }

            // NarrativeGate → LockedNarrativeBoundary
            if (rule.landmarkRole == ZoneLandmarkRole.NarrativeGate)
            {
                plan.boundaryMode = ZoneBoundaryMode.LockedNarrativeBoundary;
                return;
            }

            // Default: None
            plan.boundaryMode = ZoneBoundaryMode.None;
        }

        /// <summary>
        /// collisionRequirement를 기반으로 colliderMode를 결정한다.
        /// </summary>
        private static void ResolveColliderMode(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            switch (rule.collisionRequirement)
            {
                case ZoneCollisionRequirement.None:
                    plan.colliderMode = ZoneTerrainColliderMode.None;
                    break;
                case ZoneCollisionRequirement.SeafloorOnly:
                    plan.colliderMode = ZoneTerrainColliderMode.SeafloorOnly;
                    break;
                case ZoneCollisionRequirement.SeafloorAndLargeProps:
                    plan.colliderMode = ZoneTerrainColliderMode.SeafloorAndLargeProps;
                    break;
                case ZoneCollisionRequirement.BlockingBoundary:
                    plan.colliderMode = ZoneTerrainColliderMode.HardBoundary;
                    break;
                case ZoneCollisionRequirement.NavigationCritical:
                    plan.colliderMode = ZoneTerrainColliderMode.NavigationCritical;
                    break;
                default:
                    plan.colliderMode = ZoneTerrainColliderMode.None;
                    break;
            }
        }

        /// <summary>
        /// targetMinDepth와 targetMaxDepth를 기반으로 depth 관련 값을 계산한다.
        /// </summary>
        private static void ResolveDepth(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            float minDepth = rule.targetMinDepth;
            float maxDepth = rule.targetMaxDepth;

            // targetCenterDepth = (min + max) * 0.5
            plan.targetCenterDepth = (minDepth + maxDepth) * 0.5f;

            // depthRange = Abs(max - min)
            plan.depthRange = Mathf.Abs(maxDepth - minDepth);

            // floorHeight는 targetMaxDepth에 가깝게 (더 깊은 쪽)
            plan.floorHeight = maxDepth;

            // ceilingHeight는 targetMinDepth에 가깝게 (더 얕은 쪽)
            plan.ceilingHeight = minDepth;
        }

        /// <summary>
        /// rule의 지형 프로파일 값을 plan의 terrain profile 값으로 변환한다.
        /// </summary>
        private static void ResolveTerrainProfile(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            // slopeScale = slopeIntensity01
            plan.slopeScale = rule.slopeIntensity01;

            // roughnessScale = terrainRoughness01
            plan.roughnessScale = rule.terrainRoughness01;

            // canyonWidth01: canyonIntensity01 기반 (낮으면 좁은 협곡, 높으면 넓은 협곡)
            plan.canyonWidth01 = Mathf.Clamp01(rule.canyonIntensity01 * 0.8f + 0.1f);

            // canyonDepth01: canyonIntensity01 기반
            plan.canyonDepth01 = rule.canyonIntensity01;

            // cliffHeight01: cliffIntensity01 기반
            plan.cliffHeight01 = rule.cliffIntensity01;

            // flatAreaWeight01: openWaterFlatness01 기반
            plan.flatAreaWeight01 = rule.openWaterFlatness01;
        }

        /// <summary>
        /// rule의 routeRole, content density, terrainArchetype을 기반으로
        /// navigation 및 content 관련 값을 결정한다.
        /// </summary>
        private static void ResolveNavigationAndContent(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            // ===== Navigation Corridor Width =====
            // FreeRoam / OpenFlat → 넓은 통로
            if (rule.routeRole == ZoneRouteRole.FreeExploration &&
                rule.terrainArchetype == ZoneTerrainArchetype.OpenFlat)
            {
                plan.navigationCorridorWidth01 = 0.85f;
            }
            // MainRoute → 중간~넓음
            else if (rule.routeRole == ZoneRouteRole.MainRoute)
            {
                plan.navigationCorridorWidth01 = 0.70f;
            }
            // CanyonDeep, NarrowPassage → 좁음
            else if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep)
            {
                plan.navigationCorridorWidth01 = 0.25f;
            }
            // CanyonEntry → 중간
            else if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry)
            {
                plan.navigationCorridorWidth01 = 0.45f;
            }
            // Boundary → 좁음
            else if (rule.routeRole == ZoneRouteRole.Boundary)
            {
                plan.navigationCorridorWidth01 = 0.30f;
            }
            // Default: 중간
            else
            {
                plan.navigationCorridorWidth01 = 0.55f;
            }

            // ===== Obstacle Density =====
            // DebrisField → 중간
            if (rule.terrainArchetype == ZoneTerrainArchetype.DebrisField)
            {
                plan.obstacleDensity01 = 0.50f;
            }
            // WreckField → 중간~높음
            else if (rule.terrainArchetype == ZoneTerrainArchetype.WreckField)
            {
                plan.obstacleDensity01 = 0.60f;
            }
            // CanyonDeep → 높음
            else if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep)
            {
                plan.obstacleDensity01 = 0.70f;
            }
            // CliffWall → 높음
            else if (rule.terrainArchetype == ZoneTerrainArchetype.CliffWall)
            {
                plan.obstacleDensity01 = 0.65f;
            }
            // OpenFlat → 낮음
            else if (rule.terrainArchetype == ZoneTerrainArchetype.OpenFlat)
            {
                plan.obstacleDensity01 = 0.10f;
            }
            // SparsePressure → 낮음
            else if (rule.terrainArchetype == ZoneTerrainArchetype.SparsePressure)
            {
                plan.obstacleDensity01 = 0.15f;
            }
            // Default: terrainRoughness01 기반
            else
            {
                plan.obstacleDensity01 = Mathf.Clamp01(rule.terrainRoughness01 * 0.7f);
            }

            // ===== Landmark Placement Weight =====
            plan.landmarkPlacementWeight01 = rule.landmarkWeight01;

            // ===== Resource Spawn Weight =====
            plan.resourceSpawnWeight01 = rule.resourceDensity01;

            // ===== Hazard Spawn Weight =====
            plan.hazardSpawnWeight01 = rule.hazardDensity01;
        }

        /// <summary>
        /// terrainArchetype과 content density를 기반으로 권장 소품 개수를 결정한다.
        /// </summary>
        private static void ResolvePropCounts(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            // WreckField → 대형 소품 증가
            if (rule.terrainArchetype == ZoneTerrainArchetype.WreckField)
            {
                plan.recommendedLargePropCount = 3;
                plan.recommendedSmallPropCount = 8;
                return;
            }

            // DebrisField → 소형 소품 증가
            if (rule.terrainArchetype == ZoneTerrainArchetype.DebrisField)
            {
                plan.recommendedLargePropCount = 1;
                plan.recommendedSmallPropCount = 10;
                return;
            }

            // CanyonEntry → 중간
            if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonEntry)
            {
                plan.recommendedLargePropCount = 2;
                plan.recommendedSmallPropCount = 5;
                return;
            }

            // CanyonDeep → 적음
            if (rule.terrainArchetype == ZoneTerrainArchetype.CanyonDeep)
            {
                plan.recommendedLargePropCount = 1;
                plan.recommendedSmallPropCount = 3;
                return;
            }

            // CliffWall → 적음
            if (rule.terrainArchetype == ZoneTerrainArchetype.CliffWall)
            {
                plan.recommendedLargePropCount = 1;
                plan.recommendedSmallPropCount = 2;
                return;
            }

            // ArtificialPassage / FacilityApproach → 중간
            if (rule.terrainArchetype == ZoneTerrainArchetype.ArtificialPassage ||
                rule.terrainArchetype == ZoneTerrainArchetype.FacilityApproach)
            {
                plan.recommendedLargePropCount = 2;
                plan.recommendedSmallPropCount = 6;
                return;
            }

            // SparsePressure → 매우 적음
            if (rule.terrainArchetype == ZoneTerrainArchetype.SparsePressure)
            {
                plan.recommendedLargePropCount = 0;
                plan.recommendedSmallPropCount = 1;
                return;
            }

            // Default: density 기반
            plan.recommendedLargePropCount = Mathf.RoundToInt(rule.resourceDensity01 * 3f);
            plan.recommendedSmallPropCount = Mathf.RoundToInt(rule.resourceDensity01 * 6f);
        }

        /// <summary>
        /// rule의 태그를 plan의 terrainTags, propTags, hazardTags로 변환한다.
        /// </summary>
        private static void ResolveTags(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            var terrainTags = new List<string>();
            var propTags = new List<string>();
            var hazardTags = new List<string>();

            // ===== Terrain Archetype 기반 태그 =====
            switch (rule.terrainArchetype)
            {
                case ZoneTerrainArchetype.OpenFlat:
                    terrainTags.Add("open");
                    terrainTags.Add("flat");
                    break;
                case ZoneTerrainArchetype.ShallowShelf:
                    terrainTags.Add("shelf");
                    terrainTags.Add("shallow");
                    break;
                case ZoneTerrainArchetype.GentleSlope:
                    terrainTags.Add("slope");
                    terrainTags.Add("gentle");
                    break;
                case ZoneTerrainArchetype.DebrisField:
                    terrainTags.Add("debris");
                    terrainTags.Add("scattered");
                    break;
                case ZoneTerrainArchetype.WreckField:
                    terrainTags.Add("wreck");
                    terrainTags.Add("depression");
                    propTags.Add("wreck_part");
                    break;
                case ZoneTerrainArchetype.CanyonEntry:
                    terrainTags.Add("canyon");
                    terrainTags.Add("entry");
                    break;
                case ZoneTerrainArchetype.CanyonDeep:
                    terrainTags.Add("canyon");
                    terrainTags.Add("deep");
                    break;
                case ZoneTerrainArchetype.CliffWall:
                    terrainTags.Add("cliff");
                    terrainTags.Add("wall");
                    break;
                case ZoneTerrainArchetype.ArtificialPassage:
                    terrainTags.Add("artificial");
                    terrainTags.Add("passage");
                    propTags.Add("structure");
                    break;
                case ZoneTerrainArchetype.FacilityApproach:
                    terrainTags.Add("facility");
                    terrainTags.Add("approach");
                    propTags.Add("research");
                    propTags.Add("structure");
                    break;
                case ZoneTerrainArchetype.SparsePressure:
                    terrainTags.Add("sparse");
                    terrainTags.Add("pressure");
                    break;
            }

            // ===== Landmark Tags → propTags/terrainTags =====
            if (rule.landmarkTags != null)
            {
                foreach (string tag in rule.landmarkTags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    string lower = tag.ToLowerInvariant();

                    // canyon/wreck/research/communication/hub/pressure/current 태그 유지
                    if (lower == "canyon" || lower == "wreck" || lower == "research" ||
                        lower == "communication" || lower == "hub" || lower == "pressure" ||
                        lower == "current" || lower == "drone" || lower == "log" ||
                        lower == "beacon" || lower == "antenna" || lower == "sensor")
                    {
                        terrainTags.Add(lower);
                        propTags.Add(lower);
                    }
                    else
                    {
                        propTags.Add(lower);
                    }
                }
            }

            // ===== Resource Tags → propTags =====
            if (rule.resourceTags != null)
            {
                foreach (string tag in rule.resourceTags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    string lower = tag.ToLowerInvariant();
                    propTags.Add(lower);
                }
            }

            // ===== Hazard Tags → hazardTags =====
            if (rule.hazardTags != null)
            {
                foreach (string tag in rule.hazardTags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    string lower = tag.ToLowerInvariant();
                    hazardTags.Add(lower);
                }
            }

            plan.terrainTags = terrainTags.ToArray();
            plan.propTags = propTags.ToArray();
            plan.hazardTags = hazardTags.ToArray();
        }

        /// <summary>
        /// rule과 plan 데이터를 기반으로 사람이 읽기 쉬운 디버그 요약 문자열을 생성한다.
        /// </summary>
        private static string BuildDebugSummary(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            var sb = new StringBuilder();
            sb.Append($"[{rule.zoneId}] ");
            sb.Append($"Surface={plan.baseSurfaceMode} ");
            sb.Append($"Seabed={plan.seabedShapeMode} ");
            sb.Append($"Route={plan.routeShapeMode} ");
            sb.Append($"Boundary={plan.boundaryMode} ");
            sb.Append($"Collider={plan.colliderMode} | ");
            sb.Append($"Depth={plan.targetCenterDepth:F0}±{plan.depthRange:F0} ");
            sb.Append($"Floor={plan.floorHeight:F0} Ceiling={plan.ceilingHeight:F0} | ");
            sb.Append($"Slope={plan.slopeScale:F2} Rough={plan.roughnessScale:F2} ");
            sb.Append($"CanyonW={plan.canyonWidth01:F2} CanyonD={plan.canyonDepth01:F2} ");
            sb.Append($"Cliff={plan.cliffHeight01:F2} Flat={plan.flatAreaWeight01:F2} | ");
            sb.Append($"Nav={plan.navigationCorridorWidth01:F2} Obs={plan.obstacleDensity01:F2} ");
            sb.Append($"LM={plan.landmarkPlacementWeight01:F2} ");
            sb.Append($"Res={plan.resourceSpawnWeight01:F2} Haz={plan.hazardSpawnWeight01:F2} | ");
            sb.Append($"LargeProp={plan.recommendedLargePropCount} SmallProp={plan.recommendedSmallPropCount} | ");
            sb.Append($"ClearRoute={plan.requiresMainRouteClearance} ");
            sb.Append($"Blocker={plan.requiresBoundaryBlocker} ");
            sb.Append($"Collider={plan.requiresSeafloorCollider} ");
            sb.Append($"DecoOnly={plan.allowsDecorativeOnlySurface}");
            return sb.ToString();
        }

        // ======================================================================
        //  Asset Management
        // ======================================================================

        /// <summary>
        /// 기존 WorldMapZoneTerrainPlanDatabaseSO asset을 찾거나 없으면 새로 생성한다.
        /// </summary>
        private static WorldMapZoneTerrainPlanDatabaseSO FindOrCreatePlanDatabaseAsset()
        {
            // 기존 asset 로드
            WorldMapZoneTerrainPlanDatabaseSO existing = AssetDatabase.LoadAssetAtPath<WorldMapZoneTerrainPlanDatabaseSO>(AssetPath);
            if (existing != null)
            {
                Debug.Log($"[ZoneTerrainPlan] Found existing database asset at '{AssetPath}'.");
                return existing;
            }

            // 폴더 확인 및 생성
            System.IO.Directory.CreateDirectory(AssetFolder);

            // 새 asset 생성
            WorldMapZoneTerrainPlanDatabaseSO newDb = ScriptableObject.CreateInstance<WorldMapZoneTerrainPlanDatabaseSO>();
            AssetDatabase.CreateAsset(newDb, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZoneTerrainPlan] Created new database asset at '{AssetPath}'.");
            return newDb;
        }

        // ======================================================================
        //  SerializedProperty Helpers
        // ======================================================================

        private static void SetStringProperty(SerializedProperty parent, string propertyName, string value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            if (prop != null) prop.stringValue = value ?? string.Empty;
        }

        private static void SetEnumProperty(SerializedProperty parent, string propertyName, System.Enum value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            if (prop != null) prop.enumValueIndex = System.Convert.ToInt32(value);
        }

        private static void SetFloatProperty(SerializedProperty parent, string propertyName, float value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetIntProperty(SerializedProperty parent, string propertyName, int value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            if (prop != null) prop.intValue = value;
        }

        private static void SetBoolProperty(SerializedProperty parent, string propertyName, bool value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetStringArrayProperty(SerializedProperty parent, string propertyName, string[] values)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            if (prop == null) return;

            prop.ClearArray();
            if (values == null) return;

            for (int i = 0; i < values.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                SerializedProperty element = prop.GetArrayElementAtIndex(i);
                element.stringValue = values[i] ?? string.Empty;
            }
        }

        /// <summary>
        /// 태그 배열에 특정 태그가 포함되어 있는지 확인한다. (대소문자 무시)
        /// </summary>
        private static bool ContainsTag(string[] tags, string target)
        {
            if (tags == null || tags.Length == 0)
                return false;

            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] != null &&
                    string.Equals(tags[i], target, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
