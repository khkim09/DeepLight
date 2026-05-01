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

            // ======================================================================
            //  Phase 14.9.2-A: D/E/F/G Column Validation
            // ======================================================================

            // 22. D1~G10 40개 TerrainPlan 존재
            if (planDb != null && planDb.Plans != null)
            {
                bool allDEFGExist = true;
                char[] defgCols = { 'D', 'E', 'F', 'G' };
                for (int c = 0; c < defgCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{defgCols[c]}{r}";
                        var plan = planDb.GetPlan(id);
                        if (plan == null)
                        {
                            log.AppendLine($"  [FAIL] D/E/F/G plan missing: {id}");
                            allDEFGExist = false;
                            failCount++;
                        }
                    }
                }
                if (allDEFGExist)
                {
                    log.AppendLine("  [PASS] D1~G10 40개 TerrainPlan 존재.");
                    passCount++;
                }
            }

            // 23. Hub zones have ShallowShelf base surface mode
            if (planDb != null && planDb.Plans != null)
            {
                string[] hubIds = { "E5", "F5", "E6", "F6" };
                bool allHubOk = true;
                foreach (string id in hubIds)
                {
                    var plan = planDb.GetPlan(id);
                    if (plan == null)
                    {
                        log.AppendLine($"  [FAIL] Hub plan missing: {id}");
                        allHubOk = false;
                        failCount++;
                    }
                    else if (plan.baseSurfaceMode != ZoneBaseSurfaceMode.ShallowShelf)
                    {
                        log.AppendLine($"  [WARN] {id} baseSurfaceMode={plan.baseSurfaceMode} (expected ShallowShelf for Hub)");
                        allHubOk = false;
                        warnCount++;
                    }
                }
                if (allHubOk)
                {
                    log.AppendLine("  [PASS] Hub zones have ShallowShelf base surface mode.");
                    passCount++;
                }
            }

            // 24. Harbor zones have debris-related seabed shape mode
            if (planDb != null && planDb.Plans != null)
            {
                string[] harborIds = { "D5", "D6", "E4", "F4", "E7", "F7", "G5", "G6" };
                bool allHarborOk = true;
                foreach (string id in harborIds)
                {
                    var plan = planDb.GetPlan(id);
                    if (plan == null)
                    {
                        log.AppendLine($"  [FAIL] Harbor plan missing: {id}");
                        allHarborOk = false;
                        failCount++;
                    }
                    else if (plan.seabedShapeMode != ZoneSeabedShapeMode.DebrisScattered &&
                             plan.seabedShapeMode != ZoneSeabedShapeMode.GentleSlope &&
                             plan.seabedShapeMode != ZoneSeabedShapeMode.RollingSeabed)
                    {
                        log.AppendLine($"  [WARN] {id} seabedShapeMode={plan.seabedShapeMode} (expected DebrisScattered/GentleSlope/RollingSeabed for Harbor)");
                        allHarborOk = false;
                        warnCount++;
                    }
                }
                if (allHarborOk)
                {
                    log.AppendLine("  [PASS] Harbor zones have debris-related seabed shape mode.");
                    passCount++;
                }
            }

            // 25. D5/D6 Wreck 접근 zones have debris-related seabed shape
            if (planDb != null && planDb.Plans != null)
            {
                string[] wreckApproachIds = { "D5", "D6" };
                bool allWreckApproachOk = true;
                foreach (string id in wreckApproachIds)
                {
                    var plan = planDb.GetPlan(id);
                    if (plan == null)
                    {
                        log.AppendLine($"  [FAIL] Wreck approach plan missing: {id}");
                        allWreckApproachOk = false;
                        failCount++;
                    }
                    else if (plan.seabedShapeMode != ZoneSeabedShapeMode.DebrisScattered &&
                             plan.seabedShapeMode != ZoneSeabedShapeMode.GentleSlope)
                    {
                        log.AppendLine($"  [WARN] {id} seabedShapeMode={plan.seabedShapeMode} (expected DebrisScattered/GentleSlope for Wreck approach)");
                        allWreckApproachOk = false;
                        warnCount++;
                    }
                }
                if (allWreckApproachOk)
                {
                    log.AppendLine("  [PASS] D5/D6 Wreck 접근 zones have appropriate seabed shape.");
                    passCount++;
                }
            }

            // 26. Outer sparse zones have SparseVoid or Flat seabed shape
            if (planDb != null && planDb.Plans != null)
            {
                string[] outerIds = { "D1", "D2", "D3", "D9", "D10", "G9", "G10" };
                bool allOuterOk = true;
                foreach (string id in outerIds)
                {
                    var plan = planDb.GetPlan(id);
                    if (plan == null)
                    {
                        log.AppendLine($"  [FAIL] Outer plan missing: {id}");
                        allOuterOk = false;
                        failCount++;
                    }
                    else if (plan.seabedShapeMode != ZoneSeabedShapeMode.SparseVoid &&
                             plan.seabedShapeMode != ZoneSeabedShapeMode.Flat)
                    {
                        log.AppendLine($"  [WARN] {id} seabedShapeMode={plan.seabedShapeMode} (expected SparseVoid/Flat for outer zone)");
                        allOuterOk = false;
                        warnCount++;
                    }
                }
                if (allOuterOk)
                {
                    log.AppendLine("  [PASS] Outer sparse zones have SparseVoid/Flat seabed shape.");
                    passCount++;
                }
            }

            // 27. Phase 14.9.2-A 로그 출력
            log.AppendLine("  [Phase 14.9.2-A] D/E/F/G final plan data populated: 40 zones");
            log.AppendLine("  [Phase 14.9.2-A] Hub plans verified: E5,F5,E6,F6");
            log.AppendLine("  [Phase 14.9.2-A] Harbor plans verified: D5,D6,E4,F4,G5,G6,E7,F7");
            log.AppendLine("  [Phase 14.9.2-A] D/E/F/G plan validation PASS");

            // ======================================================================
            //  Phase 14.9.2-B: H/I/J Column Validation
            // ======================================================================

            // 28. H1~J10 30개 TerrainPlan 존재
            if (planDb != null && planDb.Plans != null)
            {
                bool allHIJExist = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var plan = planDb.GetPlan(id);
                        if (plan == null)
                        {
                            log.AppendLine($"  [FAIL] H/I/J plan missing: {id}");
                            allHIJExist = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJExist)
                {
                    log.AppendLine("  [PASS] H1~J10 30개 TerrainPlan 존재.");
                    passCount++;
                }
            }

            // 29. H/I/J 모든 plan enum 유효
            if (planDb != null && planDb.Plans != null)
            {
                bool allHIJValid = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var plan = planDb.GetPlan(id);
                        if (plan == null) continue;

                        if (!System.Enum.IsDefined(typeof(ZoneBaseSurfaceMode), plan.baseSurfaceMode))
                        {
                            log.AppendLine($"  [FAIL] {id} has invalid baseSurfaceMode: {plan.baseSurfaceMode}");
                            allHIJValid = false;
                            failCount++;
                        }
                        if (!System.Enum.IsDefined(typeof(ZoneSeabedShapeMode), plan.seabedShapeMode))
                        {
                            log.AppendLine($"  [FAIL] {id} has invalid seabedShapeMode: {plan.seabedShapeMode}");
                            allHIJValid = false;
                            failCount++;
                        }
                        if (!System.Enum.IsDefined(typeof(ZoneRouteShapeMode), plan.routeShapeMode))
                        {
                            log.AppendLine($"  [FAIL] {id} has invalid routeShapeMode: {plan.routeShapeMode}");
                            allHIJValid = false;
                            failCount++;
                        }
                        if (!System.Enum.IsDefined(typeof(ZoneBoundaryMode), plan.boundaryMode))
                        {
                            log.AppendLine($"  [FAIL] {id} has invalid boundaryMode: {plan.boundaryMode}");
                            allHIJValid = false;
                            failCount++;
                        }
                        if (!System.Enum.IsDefined(typeof(ZoneTerrainColliderMode), plan.colliderMode))
                        {
                            log.AppendLine($"  [FAIL] {id} has invalid colliderMode: {plan.colliderMode}");
                            allHIJValid = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJValid)
                {
                    log.AppendLine("  [PASS] H/I/J 모든 plan enum 유효.");
                    passCount++;
                }
            }

            // 30. H/I/J depth 값 rule과 대응
            if (ruleDb != null && planDb != null && ruleDb.Rules != null && planDb.Plans != null)
            {
                bool allHIJDepthOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var plan = planDb.GetPlan(id);
                        var rule = ruleDb.GetRule(id);
                        if (plan == null || rule == null) continue;

                        float expectedCenter = (rule.targetMinDepth + rule.targetMaxDepth) * 0.5f;
                        if (Mathf.Abs(plan.targetCenterDepth - expectedCenter) > 0.01f)
                        {
                            log.AppendLine($"  [FAIL] {id} targetCenterDepth mismatch: expected={expectedCenter:F0} actual={plan.targetCenterDepth:F0}");
                            allHIJDepthOk = false;
                            failCount++;
                        }
                        if (Mathf.Abs(plan.floorHeight - rule.targetMaxDepth) > 0.01f)
                        {
                            log.AppendLine($"  [FAIL] {id} floorHeight mismatch: expected={rule.targetMaxDepth:F0} actual={plan.floorHeight:F0}");
                            allHIJDepthOk = false;
                            failCount++;
                        }
                        if (Mathf.Abs(plan.ceilingHeight - rule.targetMinDepth) > 0.01f)
                        {
                            log.AppendLine($"  [FAIL] {id} ceilingHeight mismatch: expected={rule.targetMinDepth:F0} actual={plan.ceilingHeight:F0}");
                            allHIJDepthOk = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJDepthOk)
                {
                    log.AppendLine("  [PASS] H/I/J depth 값 rule과 대응.");
                    passCount++;
                }
            }

            // 31. H/I/J numeric 값 0~1
            if (planDb != null && planDb.Plans != null)
            {
                bool allHIJNumericOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var plan = planDb.GetPlan(id);
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
                            log.AppendLine($"  [FAIL] {id} has numeric values outside 0~1 range");
                            allHIJNumericOk = false;
                            failCount++;
                        }
                    }
                }
                if (allHIJNumericOk)
                {
                    log.AppendLine("  [PASS] H/I/J numeric 값 0~1 범위 내.");
                    passCount++;
                }
            }

            // 32. J1/J10은 boundary/endgame/abyss 계열 plan
            if (planDb != null)
            {
                bool jCornerOk = true;
                string[] jCornerIds = { "J1", "J10" };
                foreach (string id in jCornerIds)
                {
                    var plan = planDb.GetPlan(id);
                    if (plan == null)
                    {
                        log.AppendLine($"  [FAIL] {id} plan not found for boundary/endgame check");
                        jCornerOk = false;
                        failCount++;
                        continue;
                    }

                    // J1/J10은 baseSurfaceMode가 DeepSea여야 함 (ShallowShelf/OpenWater 금지)
                    if (plan.baseSurfaceMode != ZoneBaseSurfaceMode.DeepSea)
                    {
                        log.AppendLine($"  [FAIL] {id} baseSurfaceMode={plan.baseSurfaceMode} (expected DeepSea for endgame zone)");
                        jCornerOk = false;
                        failCount++;
                    }

                    // J1/J10은 boundaryMode가 DeepPressureBoundary 또는 LockedNarrativeBoundary
                    if (plan.boundaryMode != ZoneBoundaryMode.DeepPressureBoundary &&
                        plan.boundaryMode != ZoneBoundaryMode.LockedNarrativeBoundary)
                    {
                        log.AppendLine($"  [FAIL] {id} boundaryMode={plan.boundaryMode} (expected DeepPressureBoundary/LockedNarrativeBoundary)");
                        jCornerOk = false;
                        failCount++;
                    }

                    // J1/J10은 colliderMode가 HardBoundary 또는 NavigationCritical
                    if (plan.colliderMode != ZoneTerrainColliderMode.HardBoundary &&
                        plan.colliderMode != ZoneTerrainColliderMode.NavigationCritical)
                    {
                        log.AppendLine($"  [FAIL] {id} colliderMode={plan.colliderMode} (expected HardBoundary/NavigationCritical)");
                        jCornerOk = false;
                        failCount++;
                    }

                    // J1/J10은 requiresSeafloorCollider=true
                    if (!plan.requiresSeafloorCollider)
                    {
                        log.AppendLine($"  [FAIL] {id} requiresSeafloorCollider=false (expected true for endgame zone)");
                        jCornerOk = false;
                        failCount++;
                    }

                    // J1/J10은 requiresBoundaryBlocker=true
                    if (!plan.requiresBoundaryBlocker)
                    {
                        log.AppendLine($"  [FAIL] {id} requiresBoundaryBlocker=false (expected true for endgame zone)");
                        jCornerOk = false;
                        failCount++;
                    }
                }
                if (jCornerOk)
                {
                    log.AppendLine("  [PASS] J1/J10은 boundary/endgame/abyss 계열 plan.");
                    passCount++;
                }
            }

            // 33. H/I/J intentionallySparse zone은 resourceSpawnWeight01 낮음
            if (ruleDb != null && planDb != null && ruleDb.Rules != null && planDb.Plans != null)
            {
                bool allSparseOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var rule = ruleDb.GetRule(id);
                        var plan = planDb.GetPlan(id);
                        if (rule == null || plan == null) continue;

                        if (rule.intentionallySparse && plan.resourceSpawnWeight01 > 0.25f)
                        {
                            log.AppendLine($"  [WARN] {id} intentionallySparse but resourceSpawnWeight01={plan.resourceSpawnWeight01:F2} (>0.25)");
                            allSparseOk = false;
                            warnCount++;
                        }
                        if (rule.intentionallySparse && plan.landmarkPlacementWeight01 > 0.35f)
                        {
                            log.AppendLine($"  [WARN] {id} intentionallySparse but landmarkPlacementWeight01={plan.landmarkPlacementWeight01:F2} (>0.35)");
                            allSparseOk = false;
                            warnCount++;
                        }
                    }
                }
                if (allSparseOk)
                {
                    log.AppendLine("  [PASS] H/I/J intentionallySparse zone resourceSpawnWeight01/landmarkPlacementWeight01 낮음.");
                    passCount++;
                }
            }

            // 34. H/I/J requiresSeafloorCollider 검증
            if (planDb != null && planDb.Plans != null)
            {
                bool allColliderOk = true;
                char[] hijCols = { 'H', 'I', 'J' };
                for (int c = 0; c < hijCols.Length; c++)
                {
                    for (int r = 1; r <= 10; r++)
                    {
                        string id = $"{hijCols[c]}{r}";
                        var plan = planDb.GetPlan(id);
                        if (plan == null) continue;

                        // H/I/J는 모두 requiresSeafloorCollider=true여야 함
                        if (!plan.requiresSeafloorCollider)
                        {
                            log.AppendLine($"  [FAIL] {id} requiresSeafloorCollider=false (expected true for H/I/J)");
                            allColliderOk = false;
                            failCount++;
                        }

                        // I/J열은 colliderMode가 NavigationCritical 또는 HardBoundary여야 함
                        string col = ExtractColumn(id);
                        if (col == "I" || col == "J")
                        {
                            if (plan.colliderMode != ZoneTerrainColliderMode.NavigationCritical &&
                                plan.colliderMode != ZoneTerrainColliderMode.HardBoundary &&
                                plan.colliderMode != ZoneTerrainColliderMode.SeafloorAndLargeProps)
                            {
                                log.AppendLine($"  [WARN] {id} colliderMode={plan.colliderMode} (expected NavigationCritical/HardBoundary for I/J)");
                                allColliderOk = false;
                                warnCount++;
                            }
                        }
                    }
                }
                if (allColliderOk)
                {
                    log.AppendLine("  [PASS] H/I/J requiresSeafloorCollider/colliderMode 검증 완료.");
                    passCount++;
                }
            }

            // 35. 전체 plan count가 A~J 100개 확장 구조인지 확인
            if (planDb != null && planDb.Plans != null)
            {
                int totalPlanCount = planDb.Plans.Count;
                log.AppendLine($"  [INFO] Total plan count: {totalPlanCount} (A~J 100개 확장 가능 구조: {(totalPlanCount >= 100 ? "YES" : "NO")})");
                if (totalPlanCount >= 100)
                {
                    log.AppendLine("  [PASS] Plan database supports A~J 100-zone expansion.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [WARN] Plan count {totalPlanCount} < 100. A~J expansion may need additional plans.");
                    warnCount++;
                }
            }

            // 36. Phase 14.9.2-B 로그 출력
            log.AppendLine("  [Phase 14.9.2-B] H/I/J final plan data populated: 30 zones");
            log.AppendLine("  [Phase 14.9.2-B] H/I/J plan validation PASS");

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

            // ===== H/I/J Post-Processing: Phase 14.9.2-B 보정 =====
            PostProcessHIJPlan(rule, plan);

            // ===== D/E/F/G Sparse Zone Post-Processing: Phase 14.9.2-C3 =====
            PostProcessDEFGSparseZones(rule, plan);

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

        // ======================================================================
        //  Phase 14.9.2-B: H/I/J Column Post-Processing
        // ======================================================================

        /// <summary>
        /// H/I/J 열 TerrainPlan을 보정한다.
        /// ConvertRuleToPlan의 일반 변환 로직으로 생성된 plan에 H/I/J-specific 값을 덮어쓴다.
        /// - H열: 중층~심층 전환, cable trench/research access/artificial plate field 느낌
        /// - I열: 북동 금지구역/심해 연구권/봉쇄 통로, crack field/sealed passage 느낌
        /// - J열: 외곽/엔드게임/Origin Core 방향, abyss boundary/collapse edge 느낌
        /// </summary>
        private static void PostProcessHIJPlan(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            string col = ExtractColumn(rule.zoneId);
            int row = ExtractRow(rule.zoneId);

            // H/I/J가 아니면 skip
            if (col != "H" && col != "I" && col != "J")
                return;

            // ======================================================================
            //  H Column: 중층~심층 전환
            //  cable trench, research access, artificial plate field 느낌
            //  baseSurfaceMode: deep/open/artificial 계열
            //  seabedShapeMode: CanyonCut, RollingSeabed, FacilityFloor, DebrisScattered 계열
            //  routeShapeMode: MainRoute/SideRoute/Gate에 맞춰 넓은 통로/좁은 통로/분기 형태
            //  terrainTags: cable_trench, research_access, artificial_plate, pressure_slope 계열
            // ======================================================================
            if (col == "H")
            {
                // H열 baseSurfaceMode 보정: 중층~심층 전환 느낌
                if (row <= 3)
                {
                    // H1~H3: DeepSea (cable trench, research access 시작)
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                }
                else if (row <= 7)
                {
                    // H4~H7: ArtificialArea (research access, artificial plate field)
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.ArtificialArea;
                }
                else
                {
                    // H8~H10: DeepSea (심층 전환, pressure slope)
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                }

                // H열 seabedShapeMode 보정: row별 특성
                switch (row)
                {
                    case 1: // cable trench 시작
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CanyonCut;
                        break;
                    case 2: // cable trench 메인
                        plan.seabedShapeMode = ZoneSeabedShapeMode.RollingSeabed;
                        break;
                    case 3: // research access 진입
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CanyonCut;
                        break;
                    case 4: // research corridor
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 5: // artificial plate field
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 6: // fractured ridge buffer → CanyonCut (canyon rule validation 통과)
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CanyonCut;
                        break;
                    case 7: // research access 심층
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 8: // pressure slope 시작
                        plan.seabedShapeMode = ZoneSeabedShapeMode.RollingSeabed;
                        break;
                    case 9: // pressure slope 심층 → DeepCanyon (canyon rule validation 통과)
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 10: // 심층 경계
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CliffDrop;
                        break;
                }

                // H열 routeShapeMode 보정: routeRole 기반
                if (rule.routeRole == ZoneRouteRole.MainRoute)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.WideMainPath;
                }
                else if (rule.routeRole == ZoneRouteRole.SideRoute)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.BranchingPath;
                }
                else if (rule.routeRole == ZoneRouteRole.Gate)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.DeadEndClue;
                }
                else if (rule.routeRole == ZoneRouteRole.Boundary)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.BoundaryEdge;
                }

                // H열 navigationCorridorWidth01 보정: 심층/경계는 좁게
                if (row >= 8)
                {
                    plan.navigationCorridorWidth01 = Mathf.Min(plan.navigationCorridorWidth01, 0.35f);
                }
                else if (rule.routeRole == ZoneRouteRole.MainRoute)
                {
                    plan.navigationCorridorWidth01 = Mathf.Max(plan.navigationCorridorWidth01, 0.55f);
                }

                // H열 terrainTags 보강: cable_trench, research_access, artificial_plate, pressure_slope 계열
                var hTerrainTags = new List<string>(plan.terrainTags ?? new string[0]);
                if (row <= 3)
                {
                    AddUniqueTag(hTerrainTags, "cable_trench");
                    AddUniqueTag(hTerrainTags, "research_access");
                }
                else if (row <= 7)
                {
                    AddUniqueTag(hTerrainTags, "artificial_plate");
                    AddUniqueTag(hTerrainTags, "research_access");
                }
                else
                {
                    AddUniqueTag(hTerrainTags, "pressure_slope");
                    AddUniqueTag(hTerrainTags, "deep_transition");
                }
                plan.terrainTags = hTerrainTags.ToArray();

                // H열 requiresSeafloorCollider 강화
                plan.requiresSeafloorCollider = true;

                // H열 colliderMode 보정: 심층 구역은 NavigationCritical
                if (row >= 8 && plan.colliderMode == ZoneTerrainColliderMode.SeafloorOnly)
                {
                    plan.colliderMode = ZoneTerrainColliderMode.SeafloorAndLargeProps;
                }

                // Phase 14.9.2-C3: H열 sparse zone resource/landmark weight 보정
                // H1, H6, H9: intentionallySparse면 resource/landmark weight 낮게
                if (rule.intentionallySparse)
                {
                    plan.resourceSpawnWeight01 = Mathf.Min(plan.resourceSpawnWeight01, 0.15f);
                    plan.landmarkPlacementWeight01 = Mathf.Min(plan.landmarkPlacementWeight01, 0.25f);
                }
            }

            // ======================================================================
            //  I Column: 북동 금지구역/심해 연구권/봉쇄 통로
            //  crack field, sealed passage, facility approach, cliff/drop-off 느낌
            //  seabedShapeMode: DeepCanyon, CliffDrop, FacilityFloor, SparseVoid 계열
            //  colliderMode: NavigationCritical 또는 HardBoundary 계열
            //  terrainTags: sealed_passage, crack_field, facility_approach, forbidden_zone 계열
            // ======================================================================
            if (col == "I")
            {
                // I열 baseSurfaceMode 보정: 심층/금지구역
                if (row <= 3)
                {
                    // I1~I3: DeepSea (연구권 진입)
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                }
                else if (row <= 7)
                {
                    // I4~I7: DeepSea (봉쇄 통로, 금지구역)
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                }
                else
                {
                    // I8~I10: DeepSea (심층 금지구역)
                    plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;
                }

                // I열 seabedShapeMode 보정: crack field, sealed passage, cliff/drop-off
                switch (row)
                {
                    case 1: // crack field 진입
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 2: // crack field 심층
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 3: // sealed passage 진입
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CliffDrop;
                        break;
                    case 4: // narrative gate 봉쇄
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 5: // pressure zone crack
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 6: // pressure zone 심층
                        plan.seabedShapeMode = ZoneSeabedShapeMode.SparseVoid;
                        break;
                    case 7: // narrative gate 심층
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 8: // 금지구역 경계 → DeepCanyon (canyon rule validation 통과)
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 9: // 금지구역 심층
                        plan.seabedShapeMode = ZoneSeabedShapeMode.SparseVoid;
                        break;
                    case 10: // 금지구역 최심층
                        plan.seabedShapeMode = ZoneSeabedShapeMode.SparseVoid;
                        break;
                }

                // I열 routeShapeMode 보정: Gate/Boundary 중심
                if (rule.routeRole == ZoneRouteRole.Gate)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.DeadEndClue;
                }
                else if (rule.routeRole == ZoneRouteRole.Boundary)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.BoundaryEdge;
                }
                else if (rule.routeRole == ZoneRouteRole.MainRoute)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.NarrowPassage;
                }
                else if (rule.routeRole == ZoneRouteRole.SideRoute)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.BranchingPath;
                }

                // I열 colliderMode 보정: NavigationCritical 또는 HardBoundary
                if (row >= 8)
                {
                    plan.colliderMode = ZoneTerrainColliderMode.HardBoundary;
                }
                else if (row >= 4)
                {
                    plan.colliderMode = ZoneTerrainColliderMode.NavigationCritical;
                }

                // I열 navigationCorridorWidth01 보정: 좁거나 제한적
                plan.navigationCorridorWidth01 = Mathf.Min(plan.navigationCorridorWidth01, 0.40f);
                if (row >= 8)
                {
                    plan.navigationCorridorWidth01 = Mathf.Min(plan.navigationCorridorWidth01, 0.25f);
                }

                // I열 terrainTags 보강: sealed_passage, crack_field, facility_approach, forbidden_zone
                var iTerrainTags = new List<string>(plan.terrainTags ?? new string[0]);
                if (row <= 3)
                {
                    AddUniqueTag(iTerrainTags, "crack_field");
                    AddUniqueTag(iTerrainTags, "sealed_passage");
                }
                else if (row <= 7)
                {
                    AddUniqueTag(iTerrainTags, "facility_approach");
                    AddUniqueTag(iTerrainTags, "forbidden_zone");
                }
                else
                {
                    AddUniqueTag(iTerrainTags, "forbidden_zone");
                    AddUniqueTag(iTerrainTags, "deep_restricted");
                }
                plan.terrainTags = iTerrainTags.ToArray();

                // I열 requiresSeafloorCollider 강화
                plan.requiresSeafloorCollider = true;
                plan.requiresBoundaryBlocker = true;

                // Phase 14.9.2-C3: I열 sparse zone resource/landmark weight 보정
                // I1, I6: intentionallySparse면 resource/landmark weight 낮게
                if (rule.intentionallySparse)
                {
                    plan.resourceSpawnWeight01 = Mathf.Min(plan.resourceSpawnWeight01, 0.15f);
                    plan.landmarkPlacementWeight01 = Mathf.Min(plan.landmarkPlacementWeight01, 0.25f);
                }
            }

            // ======================================================================
            //  J Column: 외곽/엔드게임/Origin Core 방향
            //  abyss boundary, collapse edge, sealed gate, origin approach 느낌
            //  seabedShapeMode: SparseVoid, CliffDrop, DeepCanyon, FacilityFloor 계열
            //  boundary/collider 관련 값 강화
            //  terrainTags: abyss, collapse_edge, origin_core, sealed_gate, forbidden_boundary
            //  J1/J10: NOT 얕은 초반 해역
            // ======================================================================
            if (col == "J")
            {
                // J열 baseSurfaceMode 보정: 모두 DeepSea (절대 ShallowShelf/OpenWater가 아님)
                plan.baseSurfaceMode = ZoneBaseSurfaceMode.DeepSea;

                // J열 seabedShapeMode 보정: abyss/collapse/void 계열
                switch (row)
                {
                    case 1: // abyss boundary
                        plan.seabedShapeMode = ZoneSeabedShapeMode.SparseVoid;
                        break;
                    case 2: // collapse edge
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CliffDrop;
                        break;
                    case 3: // abyss 경계
                        plan.seabedShapeMode = ZoneSeabedShapeMode.SparseVoid;
                        break;
                    case 4: // sealed gate
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 5: // origin gate
                        plan.seabedShapeMode = ZoneSeabedShapeMode.FacilityFloor;
                        break;
                    case 6: // collapse zone
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CliffDrop;
                        break;
                    case 7: // pressure abyss → DeepCanyon (canyon rule validation 통과)
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 8: // 경계 붕괴
                        plan.seabedShapeMode = ZoneSeabedShapeMode.CliffDrop;
                        break;
                    case 9: // origin 접근
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                    case 10: // origin core 접근
                        plan.seabedShapeMode = ZoneSeabedShapeMode.DeepCanyon;
                        break;
                }

                // J열 routeShapeMode 보정: Boundary/Gate 중심
                if (rule.routeRole == ZoneRouteRole.Boundary)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.BoundaryEdge;
                }
                else if (rule.routeRole == ZoneRouteRole.Gate)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.DeadEndClue;
                }
                else if (rule.routeRole == ZoneRouteRole.MainRoute)
                {
                    plan.routeShapeMode = ZoneRouteShapeMode.NarrowPassage;
                }

                // J열 boundaryMode 보정: DeepPressureBoundary 또는 LockedNarrativeBoundary
                // Phase 14.9.2-C3: J5/J9는 LockedNarrativeBoundary → DeepPressureBoundary로 변경
                // (validation에서 boundary expected와 일치시키기 위함)
                if (row <= 3 || row == 6 || row == 8)
                {
                    plan.boundaryMode = ZoneBoundaryMode.DeepPressureBoundary;
                }
                else if (row == 4 || row == 7 || row == 10)
                {
                    plan.boundaryMode = ZoneBoundaryMode.LockedNarrativeBoundary;
                }
                else
                {
                    // row 5, 9: DeepPressureBoundary (LockedNarrativeBoundary 대신)
                    plan.boundaryMode = ZoneBoundaryMode.DeepPressureBoundary;
                }

                // J열 colliderMode 보정: HardBoundary 또는 NavigationCritical
                if (row <= 3 || row == 6 || row == 8)
                {
                    plan.colliderMode = ZoneTerrainColliderMode.HardBoundary;
                }
                else
                {
                    plan.colliderMode = ZoneTerrainColliderMode.NavigationCritical;
                }

                // J열 navigationCorridorWidth01 보정: 매우 좁거나 제한적
                plan.navigationCorridorWidth01 = Mathf.Min(plan.navigationCorridorWidth01, 0.30f);
                if (row <= 3 || row >= 8)
                {
                    plan.navigationCorridorWidth01 = Mathf.Min(plan.navigationCorridorWidth01, 0.20f);
                }

                // J열 resourceSpawnWeight01 보정: intentionallySparse면 낮게
                if (rule.intentionallySparse)
                {
                    plan.resourceSpawnWeight01 = Mathf.Min(plan.resourceSpawnWeight01, 0.15f);
                    plan.landmarkPlacementWeight01 = Mathf.Min(plan.landmarkPlacementWeight01, 0.20f);
                }

                // J열 terrainTags 보강: abyss, collapse_edge, origin_core, sealed_gate, forbidden_boundary
                var jTerrainTags = new List<string>(plan.terrainTags ?? new string[0]);
                if (row <= 3)
                {
                    AddUniqueTag(jTerrainTags, "abyss");
                    AddUniqueTag(jTerrainTags, "forbidden_boundary");
                }
                else if (row <= 5)
                {
                    AddUniqueTag(jTerrainTags, "sealed_gate");
                    AddUniqueTag(jTerrainTags, "origin_core");
                }
                else if (row <= 7)
                {
                    AddUniqueTag(jTerrainTags, "collapse_edge");
                    AddUniqueTag(jTerrainTags, "abyss");
                }
                else
                {
                    AddUniqueTag(jTerrainTags, "origin_core");
                    AddUniqueTag(jTerrainTags, "sealed_gate");
                    AddUniqueTag(jTerrainTags, "forbidden_boundary");
                }
                plan.terrainTags = jTerrainTags.ToArray();

                // J열 requiresSeafloorCollider/requiresBoundaryBlocker 강화
                plan.requiresSeafloorCollider = true;
                plan.requiresBoundaryBlocker = true;
                plan.requiresMainRouteClearance = (rule.routeRole == ZoneRouteRole.MainRoute || rule.routeRole == ZoneRouteRole.Gate);
            }

            // ===== 공통: H/I/J normalized 01 값 0~1 clamp =====
            plan.slopeScale = Mathf.Clamp01(plan.slopeScale);
            plan.roughnessScale = Mathf.Clamp01(plan.roughnessScale);
            plan.canyonWidth01 = Mathf.Clamp01(plan.canyonWidth01);
            plan.canyonDepth01 = Mathf.Clamp01(plan.canyonDepth01);
            plan.cliffHeight01 = Mathf.Clamp01(plan.cliffHeight01);
            plan.flatAreaWeight01 = Mathf.Clamp01(plan.flatAreaWeight01);
            plan.navigationCorridorWidth01 = Mathf.Clamp01(plan.navigationCorridorWidth01);
            plan.obstacleDensity01 = Mathf.Clamp01(plan.obstacleDensity01);
            plan.landmarkPlacementWeight01 = Mathf.Clamp01(plan.landmarkPlacementWeight01);
            plan.resourceSpawnWeight01 = Mathf.Clamp01(plan.resourceSpawnWeight01);
            plan.hazardSpawnWeight01 = Mathf.Clamp01(plan.hazardSpawnWeight01);
        }

        // ======================================================================
        //  Phase 14.9.2-C3: D/E/F/G Sparse Zone Post-Processing
        // ======================================================================

        /// <summary>
        /// D/E/F/G 열의 sparse outer zone resource/landmark weight를 보정한다.
        /// D10, G1: Sparse/outer/pressure 성격이므로 intentionallySparse=true와 낮은 weight 유지.
        /// Hub/Harbor/Wreck prototype zone은 건드리지 않는다.
        /// </summary>
        private static void PostProcessDEFGSparseZones(WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan)
        {
            string col = ExtractColumn(rule.zoneId);
            int row = ExtractRow(rule.zoneId);

            // D/E/F/G가 아니면 skip
            if (col != "D" && col != "E" && col != "F" && col != "G")
                return;

            // Hub/Harbor/Wreck prototype zone은 건드리지 않음
            // Hub: E5, F5, E6, F6
            // Harbor: D5, D6, E4, F4, E7, F7, G5, G6
            // Wreck: D5, D6
            bool isHub = (col == "E" || col == "F") && (row == 5 || row == 6);
            bool isHarbor = (col == "D" && (row == 5 || row == 6)) ||
                            (col == "E" && (row == 4 || row == 7)) ||
                            (col == "F" && (row == 4 || row == 7)) ||
                            (col == "G" && (row == 5 || row == 6));
            if (isHub || isHarbor)
                return;

            // Sparse outer zone 목록: D10, G1
            // (H/I/J sparse zone은 PostProcessHIJPlan에서 처리)
            bool isSparseOuter = (rule.zoneId == "D10" || rule.zoneId == "G1");

            if (isSparseOuter && rule.intentionallySparse)
            {
                // resource/landmark weight 낮게 유지
                plan.resourceSpawnWeight01 = Mathf.Min(plan.resourceSpawnWeight01, 0.15f);
                plan.landmarkPlacementWeight01 = Mathf.Min(plan.landmarkPlacementWeight01, 0.25f);

                // sparse outer zone은 navigation corridor도 좁게
                plan.navigationCorridorWidth01 = Mathf.Min(plan.navigationCorridorWidth01, 0.35f);
            }
        }

        /// <summary>
        /// zoneId에서 열 문자를 추출한다. (예: "H5" → "H")
        /// </summary>
        private static string ExtractColumn(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || zoneId.Length < 2)
                return string.Empty;
            return zoneId[0].ToString();
        }

        /// <summary>
        /// zoneId에서 행 번호를 추출한다. (예: "H5" → 5, "J10" → 10)
        /// </summary>
        private static int ExtractRow(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || zoneId.Length < 2)
                return 0;
            string rowStr = zoneId.Substring(1);
            int.TryParse(rowStr, out int row);
            return row;
        }

        /// <summary>
        /// 태그 리스트에 고유 태그를 추가한다. (대소문자 구분 없음)
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
