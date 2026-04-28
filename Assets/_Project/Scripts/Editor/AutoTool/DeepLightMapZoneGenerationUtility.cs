using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// DeepLight WorldMap ZoneRoot 자동 생성을 위한 Editor Utility.
    /// Phase 4: 10x10 ZoneRoot_A1~J10, ZoneTrigger, Environment 루트 생성.
    /// 모든 Hierarchy 조작은 GeneratedWorldRoot/ZoneRoots 하위로 제한된다.
    /// </summary>
    public static class DeepLightMapZoneGenerationUtility
    {
        // ===== Public API =====

        // ===== Summary counters for component skip logging =====
        private static int _zoneRootSkipCount;
        private static int _triggerRelaySkipCount;

        /// <summary>
        /// ZoneRoot_A1~J10을 생성하거나 업데이트한다.
        /// 기존 ZoneRoot가 있으면 재사용하고 위치/이름을 보정한다.
        /// </summary>
        public static void RebuildAllZoneRoots(DeepLightMapAutoBuilderSettingsSO settings, GameObject zoneRootsParent)
        {
            if (settings == null || settings.WorldMapConfig == null || settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings, WorldMapConfig, or ScenarioPreset is null. Cannot rebuild zone roots.");
                return;
            }

            if (zoneRootsParent == null)
            {
                Debug.LogError("[MapAutoBuilder] ZoneRoots parent is null. Cannot rebuild zone roots.");
                return;
            }

            // Summary counters 초기화
            _zoneRootSkipCount = 0;
            _triggerRelaySkipCount = 0;

            var config = settings.WorldMapConfig;
            var preset = settings.ScenarioPreset;
            int gridWidth = config.GridWidth;   // 10
            int gridHeight = config.GridHeight; // 10
            float zoneSize = config.ZoneSize;   // 400

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Rebuild Zone Roots (Phase 4) =====");
            log.AppendLine($"Grid: {gridWidth}x{gridHeight}, ZoneSize: {zoneSize}");

            int totalExpected = gridWidth * gridHeight; // 100
            int updated = 0;

            // 10x10 그리드 순회
            for (int colIndex = 0; colIndex < gridWidth; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < gridHeight; rowIndex++)
                {
                    // colIndex: A=0, B=1, ..., J=9
                    // rowIndex: 1=0, 2=1, ..., 10=9
                    char columnChar = (char)('A' + colIndex);
                    int rowNumber = rowIndex + 1;
                    ZoneId zoneId = new ZoneId(columnChar, rowNumber);

                    // ZoneRoot 생성 또는 업데이트
                    GameObject zoneRoot = CreateOrUpdateZoneRoot(
                        settings, zoneRootsParent, zoneId, colIndex, rowIndex, zoneSize);

                    if (zoneRoot != null)
                    {
                        // ZoneRoot 하위 표준 루트 생성
                        CreateZoneStandardChildren(zoneRoot, settings);

                        // ZoneTrigger 생성
                        if (settings.CreateZoneTriggers)
                        {
                            CreateZoneTrigger(zoneRoot, zoneId, zoneSize);
                        }

                        // Environment 하위 루트 생성
                        if (settings.CreateEnvironmentRoots)
                        {
                            CreateZoneEnvironmentRoots(zoneRoot);
                        }

                        // DebugGizmos 생성
                        if (settings.CreateDebugVisuals)
                        {
                            CreateZoneDebugGizmos(zoneRoot, zoneId, settings);
                        }

                        // ZoneRoot 컴포넌트 부착 시도 (Phase 4에서는 ZoneDataSO 없이 안전하게 스킵)
                        TryAttachZoneRootComponent(zoneRoot);

                        updated++;
                    }
                    else
                    {
                        log.AppendLine($"  [ERROR] Failed to create ZoneRoot_{zoneId}");
                    }
                }
            }

            // Summary 로그 출력 (개별 warning 대신)
            if (_zoneRootSkipCount > 0)
            {
                Debug.Log($"[MapAutoBuilder] ZoneRoot component skipped for {_zoneRootSkipCount} ZoneRoots because required ZoneDataSO binding is not implemented in Phase 4.");
            }
            if (_triggerRelaySkipCount > 0)
            {
                Debug.Log($"[MapAutoBuilder] ZoneTriggerRelay skipped for {_triggerRelaySkipCount} ZoneTriggers because runtime binding is not implemented in Phase 4.");
            }

            log.AppendLine($"\nResult: {updated}/{totalExpected} ZoneRoots processed.");
            log.AppendLine("===== Rebuild Zone Roots Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// ZoneRoot GameObject를 생성하거나 기존 것을 찾아서 업데이트한다.
        /// </summary>
        public static GameObject CreateOrUpdateZoneRoot(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject zoneRootsParent,
            ZoneId zoneId,
            int colIndex,
            int rowIndex,
            float zoneSize)
        {
            string zoneRootName = $"ZoneRoot_{zoneId}";

            // 부모 Transform에서 기존 ZoneRoot 검색
            Transform existing = zoneRootsParent.transform.Find(zoneRootName);
            if (existing != null)
            {
                // 기존 ZoneRoot 재사용, 위치 보정
                Vector3 expectedCenter = GetZoneCenter(settings.WorldMapConfig, colIndex, rowIndex);
                existing.localPosition = expectedCenter;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return existing.gameObject;
            }

            // 새 ZoneRoot 생성
            GameObject zoneRoot = new GameObject(zoneRootName);
            zoneRoot.transform.SetParent(zoneRootsParent.transform);

            // 위치 설정
            Vector3 center = GetZoneCenter(settings.WorldMapConfig, colIndex, rowIndex);
            zoneRoot.transform.localPosition = center;
            zoneRoot.transform.localRotation = Quaternion.identity;
            zoneRoot.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(zoneRoot, $"Create {zoneRootName}");

            LogIfVerbose(settings, $"  Created {zoneRootName} at ({center.x:F1}, 0, {center.z:F1})");

            return zoneRoot;
        }

        /// <summary>
        /// ZoneRoot 하위에 표준 자식 루트들을 생성한다.
        /// Geometry, Props, HarvestSpawns, LogSpawns, Hazards, Triggers, DebugGizmos
        /// </summary>
        public static void CreateZoneStandardChildren(GameObject zoneRoot, DeepLightMapAutoBuilderSettingsSO settings)
        {
            // ZoneRoot.cs의 CreateChildRoots와 동일한 구조
            string[] rootNames = { "Geometry", "Props", "HarvestSpawns", "LogSpawns", "Hazards", "Triggers", "DebugGizmos" };

            for (int i = 0; i < rootNames.Length; i++)
            {
                string childName = rootNames[i];
                Transform existing = zoneRoot.transform.Find(childName);
                if (existing == null)
                {
                    GameObject child = new GameObject(childName);
                    child.transform.SetParent(zoneRoot.transform);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(child, $"Create {zoneRoot.name}/{childName}");
                }
            }
        }

        /// <summary>
        /// ZoneRoot/Triggers 아래에 ZoneTrigger를 생성한다.
        /// BoxCollider를 추가하고 isTrigger, size, center를 설정한다.
        /// </summary>
        public static void CreateZoneTrigger(GameObject zoneRoot, ZoneId zoneId, float zoneSize)
        {
            // Triggers 자식 찾기
            Transform triggersTransform = zoneRoot.transform.Find("Triggers");
            if (triggersTransform == null)
            {
                Debug.LogWarning($"[MapAutoBuilder] Triggers root not found under {zoneRoot.name}. Skipping trigger creation.");
                return;
            }

            string triggerName = $"ZoneTrigger_{zoneId}";

            // 기존 트리거 검색
            Transform existingTrigger = triggersTransform.Find(triggerName);
            GameObject triggerObj;
            BoxCollider boxCollider;

            if (existingTrigger != null)
            {
                triggerObj = existingTrigger.gameObject;
                // 기존 BoxCollider 재사용 또는 추가
                boxCollider = triggerObj.GetComponent<BoxCollider>();
                if (boxCollider == null)
                {
                    boxCollider = triggerObj.AddComponent<BoxCollider>();
                }
            }
            else
            {
                // 새 트리거 생성
                triggerObj = new GameObject(triggerName);
                triggerObj.transform.SetParent(triggersTransform);
                triggerObj.transform.localPosition = Vector3.zero;
                triggerObj.transform.localRotation = Quaternion.identity;
                triggerObj.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(triggerObj, $"Create {triggerName}");

                boxCollider = triggerObj.AddComponent<BoxCollider>();
            }

            // BoxCollider 설정
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(zoneSize, 1600f, zoneSize);
            boxCollider.center = new Vector3(0f, -800f, 0f);

            // ZoneTriggerRelay 컴포넌트 부착 시도 (Phase 4에서는 필수 참조 없이 스킵)
            TryAttachZoneTriggerRelay(triggerObj, zoneId);
        }

        /// <summary>
        /// ZoneRoot/Environment 하위에 표준 루트들을 생성한다.
        /// UnderwaterArea, Seafloor, Props, DynamicEffects, Particles
        /// </summary>
        public static void CreateZoneEnvironmentRoots(GameObject zoneRoot)
        {
            // Environment 부모 찾기 또는 생성
            Transform envTransform = zoneRoot.transform.Find("Environment");
            GameObject envRoot;
            if (envTransform != null)
            {
                envRoot = envTransform.gameObject;
            }
            else
            {
                envRoot = new GameObject("Environment");
                envRoot.transform.SetParent(zoneRoot.transform);
                envRoot.transform.localPosition = Vector3.zero;
                envRoot.transform.localRotation = Quaternion.identity;
                envRoot.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(envRoot, $"Create {zoneRoot.name}/Environment");
            }

            // Environment 하위 표준 루트
            string[] envChildNames = { "UnderwaterArea", "Seafloor", "Props", "DynamicEffects", "Particles" };

            foreach (string childName in envChildNames)
            {
                Transform existing = envRoot.transform.Find(childName);
                if (existing == null)
                {
                    GameObject child = new GameObject(childName);
                    child.transform.SetParent(envRoot.transform);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(child, $"Create {zoneRoot.name}/Environment/{childName}");
                }
            }
        }

        /// <summary>
        /// ZoneRoot/DebugGizmos 아래에 Debug Label GameObject를 생성한다.
        /// ScenarioPreset Rule 정보를 이름에 반영한다.
        /// </summary>
        public static void CreateZoneDebugGizmos(GameObject zoneRoot, ZoneId zoneId, DeepLightMapAutoBuilderSettingsSO settings)
        {
            // DebugGizmos 부모 찾기
            Transform debugTransform = zoneRoot.transform.Find("DebugGizmos");
            if (debugTransform == null)
            {
                return; // DebugGizmos가 없으면 스킵 (CreateZoneStandardChildren에서 생성됨)
            }

            string debugLabelName = $"DebugLabel_{zoneId}";

            // 기존 DebugLabel 검색
            Transform existingLabel = debugTransform.Find(debugLabelName);
            if (existingLabel != null)
            {
                return; // 이미 존재하면 스킵
            }

            // ScenarioPreset Rule 조회
            string ruleInfo = GetScenarioRuleDebugInfo(settings.ScenarioPreset, zoneId);

            // DebugLabel GameObject 생성 (이름에 Rule 정보 포함)
            GameObject debugLabel = new GameObject(debugLabelName);
            debugLabel.transform.SetParent(debugTransform);
            debugLabel.transform.localPosition = new Vector3(0f, 20f, 0f); // ZoneRoot 기준 약간 위
            debugLabel.transform.localRotation = Quaternion.identity;
            debugLabel.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(debugLabel, $"Create {debugLabelName}");

            // Rule 정보를 GameObject.name에 추가 (Inspector에서 확인 가능)
            if (!string.IsNullOrEmpty(ruleInfo))
            {
                debugLabel.name = $"{debugLabelName}_{ruleInfo}";
            }
        }

        /// <summary>
        /// ZoneRoot에 ZoneRoot 컴포넌트를 안전하게 부착한다.
        /// Phase 4에서는 ZoneDataSO가 없으므로 부착하지 않고 summary counter만 증가시킨다.
        /// </summary>
        private static void TryAttachZoneRootComponent(GameObject zoneRoot)
        {
            // ZoneRoot 컴포넌트는 ZoneDataSO 필수 참조가 필요하므로
            // Phase 4에서는 부착하지 않음. Summary counter만 증가.
            _zoneRootSkipCount++;
        }

        /// <summary>
        /// ZoneTrigger에 ZoneTriggerRelay 컴포넌트를 안전하게 부착한다.
        /// Phase 4에서는 ZoneRoot/ZoneDataSO 참조가 없으므로 부착하지 않고 summary counter만 증가시킨다.
        /// </summary>
        private static void TryAttachZoneTriggerRelay(GameObject triggerObj, ZoneId zoneId)
        {
            // ZoneTriggerRelay는 ZoneRoot 또는 ZoneDataSO 필수 참조가 필요하므로
            // Phase 4에서는 부착하지 않음. Summary counter만 증가.
            _triggerRelaySkipCount++;
        }

        // ===== 좌표 계산 =====

        /// <summary>
        /// colIndex(0=A, 1=B, ..., 9=J)와 rowIndex(0=1, 1=2, ..., 9=10)로부터
        /// ZoneRoot의 월드 중심 좌표를 계산한다.
        /// ZoneCoordinate.GetZoneCenterWorldPosition()과 동일한 로직.
        /// </summary>
        public static Vector3 GetZoneCenter(WorldMapConfigSO config, int colIndex, int rowIndex)
        {
            if (config == null)
            {
                Debug.LogError("[MapAutoBuilder] WorldMapConfig is null! Cannot calculate zone center.");
                return Vector3.zero;
            }

            float centerX = config.WorldMinX + (colIndex * config.ZoneSize) + (config.ZoneSize * 0.5f);
            float centerZ = config.WorldMinZ + (rowIndex * config.ZoneSize) + (config.ZoneSize * 0.5f);

            return new Vector3(centerX, 0f, centerZ);
        }

        /// <summary>
        /// colIndex와 rowNumber(1-based)로부터 ZoneId 문자열을 생성한다.
        /// 예: (0, 1) => "A1", (9, 10) => "J10"
        /// </summary>
        public static string GetZoneIdString(int colIndex, int rowNumber)
        {
            char columnChar = (char)('A' + colIndex);
            return $"{columnChar}{rowNumber}";
        }

        /// <summary>
        /// colIndex와 rowNumber(1-based)로부터 ZoneId를 생성한다.
        /// </summary>
        public static ZoneId GetZoneId(int colIndex, int rowNumber)
        {
            char columnChar = (char)('A' + colIndex);
            return new ZoneId(columnChar, rowNumber);
        }

        // ===== Scenario Rule 조회 =====

        /// <summary>
        /// ScenarioPreset에서 ZoneId에 해당하는 Rule을 찾는다.
        /// Rule이 없으면 null을 반환한다.
        /// </summary>
        public static bool GetScenarioRuleOrDefault(WorldMapScenarioPresetSO preset, ZoneId zoneId, out WorldMapScenarioZoneRule rule)
        {
            if (preset == null)
            {
                rule = null;
                return false;
            }

            return preset.TryGetRuleForZone(zoneId, out rule);
        }

        /// <summary>
        /// ScenarioPreset Rule 정보를 Debug용 문자열로 반환한다.
        /// 형식: "RuleName_RegionId_DepthBand_Biome_Risk"
        /// </summary>
        private static string GetScenarioRuleDebugInfo(WorldMapScenarioPresetSO preset, ZoneId zoneId)
        {
            if (preset == null)
                return "NoPreset";

            if (preset.TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                // Rule 정보를 간결한 문자열로 구성
                string ruleName = !string.IsNullOrEmpty(rule.RuleName) ? rule.RuleName.Replace(" ", "") : "NoName";
                string regionStr = !string.IsNullOrEmpty(rule.RegionId.ToString()) ? rule.RegionId.ToString() : "NoRegion";
                string depthStr = rule.DepthBand.ToString();
                string biomeStr = rule.BiomeType.ToString();
                float risk = rule.BaseRiskLevel;

                return $"{ruleName}_{regionStr}_{depthStr}_{biomeStr}_Risk{risk:F2}";
            }

            // Rule이 없으면 기본값 사용
            string defaultRegion = preset.DefaultRegionId.ToString();
            string defaultDepth = "Shallow";
            string defaultBiome = "OpenWater";
            float defaultRisk = preset.DefaultRiskLevel;

            return $"Default_{defaultRegion}_{defaultDepth}_{defaultBiome}_Risk{defaultRisk:F2}";
        }

        // ===== 검증 =====

        /// <summary>
        /// 단일 검사 결과를 저장하는 구조체.
        /// label: 검사 항목 설명, Passed: 성공 여부, Message: 상세 메시지
        /// </summary>
        private struct ValidationCheck
        {
            public string Label;
            public bool Passed;
            public string Message;

            public ValidationCheck(string label, bool passed, string message)
            {
                Label = label;
                Passed = passed;
                Message = message;
            }
        }

        /// <summary>
        /// ZoneRoot 설정의 유효성을 검사한다.
        /// 모든 검사 결과를 리스트에 기록한 후 동적으로 카운트하여 출력한다.
        /// </summary>
        public static bool ValidateZoneSetup(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings == null || settings.WorldMapConfig == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings or WorldMapConfig is null. Cannot validate zone setup.");
                return false;
            }

            var config = settings.WorldMapConfig;
            int gridWidth = config.GridWidth;
            int gridHeight = config.GridHeight;
            float zoneSize = config.ZoneSize;
            int totalExpected = gridWidth * gridHeight;

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Zone Setup =====");

            // 동적 검사 결과 리스트
            var checks = new List<ValidationCheck>();

            // 1. GeneratedWorldRoot 존재 여부
            GameObject generatedRoot = GameObject.Find(settings.GeneratedRootName);
            if (generatedRoot != null)
            {
                checks.Add(new ValidationCheck("GeneratedWorldRoot exists", true, $"Found: {generatedRoot.name}"));
            }
            else
            {
                checks.Add(new ValidationCheck("GeneratedWorldRoot exists", false, $"'{settings.GeneratedRootName}' not found!"));
            }

            // 2. GeneratedWorldRoot/ZoneRoots 존재 여부
            GameObject zoneRootsParent = null;
            if (generatedRoot != null)
            {
                Transform zrTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zrTransform != null)
                {
                    zoneRootsParent = zrTransform.gameObject;
                    checks.Add(new ValidationCheck("ZoneRoots parent exists", true, $"Found: {zoneRootsParent.name}"));
                }
                else
                {
                    checks.Add(new ValidationCheck("ZoneRoots parent exists", false, $"'{settings.ZoneRootParentName}' not found under GeneratedWorldRoot!"));
                }
            }
            else
            {
                checks.Add(new ValidationCheck("ZoneRoots parent exists", true, "Skipped (GeneratedWorldRoot not found)"));
            }

            // 3. ZoneRoot 개수 검사
            int zoneRootCount = 0;
            if (zoneRootsParent != null)
            {
                for (int i = 0; i < zoneRootsParent.transform.childCount; i++)
                {
                    Transform child = zoneRootsParent.transform.GetChild(i);
                    if (child.name.StartsWith("ZoneRoot_"))
                    {
                        zoneRootCount++;
                    }
                }

                bool countMatch = zoneRootCount == totalExpected;
                checks.Add(new ValidationCheck("ZoneRoot count", countMatch,
                    countMatch ? $"{zoneRootCount}/{totalExpected}" : $"Mismatch: {zoneRootCount}/{totalExpected}"));
            }
            else
            {
                checks.Add(new ValidationCheck("ZoneRoot count", true, "Skipped (ZoneRoots parent not found)"));
            }

            // 4-9. 특정 ZoneRoot 검사
            if (zoneRootsParent != null)
            {
                // 4. ZoneRoot_A1 존재
                CheckZoneRootExistsV2(zoneRootsParent, "ZoneRoot_A1", checks);

                // 5. ZoneRoot_F6 존재
                CheckZoneRootExistsV2(zoneRootsParent, "ZoneRoot_F6", checks);

                // 6. ZoneRoot_J10 존재
                CheckZoneRootExistsV2(zoneRootsParent, "ZoneRoot_J10", checks);

                // 7. A1 좌표 검사
                CheckZoneRootPositionV2(zoneRootsParent, "ZoneRoot_A1", new Vector3(-1800f, 0f, -1800f), checks);

                // 8. F6 좌표 검사
                CheckZoneRootPositionV2(zoneRootsParent, "ZoneRoot_F6", new Vector3(200f, 0f, 200f), checks);

                // 9. J10 좌표 검사
                CheckZoneRootPositionV2(zoneRootsParent, "ZoneRoot_J10", new Vector3(1800f, 0f, 1800f), checks);

                // 10. 각 ZoneRoot 하위 표준 루트 존재 여부 (샘플: A1, F6, J10)
                string[] sampleZones = { "ZoneRoot_A1", "ZoneRoot_F6", "ZoneRoot_J10" };
                int sampleRootCheckPassed = 0;
                int sampleRootCheckTotal = sampleZones.Length * 7;
                foreach (string zoneName in sampleZones)
                {
                    Transform zoneTransform = zoneRootsParent.transform.Find(zoneName);
                    if (zoneTransform == null) continue;

                    string[] standardRoots = { "Geometry", "Props", "HarvestSpawns", "LogSpawns", "Hazards", "Triggers", "DebugGizmos" };
                    foreach (string rootName in standardRoots)
                    {
                        Transform child = zoneTransform.Find(rootName);
                        if (child != null)
                        {
                            sampleRootCheckPassed++;
                        }
                        else
                        {
                            log.AppendLine($"  [WARN] {zoneName}/{rootName} not found.");
                        }
                    }
                }
                bool allRootsPresent = sampleRootCheckPassed == sampleRootCheckTotal;
                checks.Add(new ValidationCheck("Standard child roots (sampled)", allRootsPresent,
                    $"{sampleRootCheckPassed}/{sampleRootCheckTotal}"));

                // 11. ZoneTrigger 존재 여부 (샘플)
                string[] triggerSampleZones = { "ZoneRoot_A1", "ZoneRoot_F6", "ZoneRoot_J10" };
                int triggerCheckPassed = 0;
                int triggerCheckTotal = triggerSampleZones.Length;
                bool triggerFailed = false;
                foreach (string zoneName in triggerSampleZones)
                {
                    Transform zoneTransform = zoneRootsParent.transform.Find(zoneName);
                    if (zoneTransform == null) continue;

                    Transform triggersRoot = zoneTransform.Find("Triggers");
                    if (triggersRoot == null)
                    {
                        log.AppendLine($"  [WARN] {zoneName}/Triggers not found.");
                        continue;
                    }

                    string zoneIdStr = zoneName.Replace("ZoneRoot_", "");
                    Transform trigger = triggersRoot.Find($"ZoneTrigger_{zoneIdStr}");
                    if (trigger != null)
                    {
                        triggerCheckPassed++;
                    }
                    else
                    {
                        log.AppendLine($"  [FAIL] 11. ZoneTrigger_{zoneIdStr} not found under {zoneName}/Triggers!");
                        triggerFailed = true;
                    }
                }
                bool allTriggersPresent = triggerCheckPassed == triggerCheckTotal && !triggerFailed;
                checks.Add(new ValidationCheck("ZoneTriggers exist (sampled)", allTriggersPresent,
                    allTriggersPresent ? $"{triggerCheckPassed}/{triggerCheckTotal}" : $"Partial: {triggerCheckPassed}/{triggerCheckTotal}"));

                // 12-14. BoxCollider 검사 (샘플)
                CheckZoneTriggerColliderV2(zoneRootsParent, "ZoneRoot_A1", "ZoneTrigger_A1", zoneSize, checks);
                CheckZoneTriggerColliderV2(zoneRootsParent, "ZoneRoot_F6", "ZoneTrigger_F6", zoneSize, checks);
                CheckZoneTriggerColliderV2(zoneRootsParent, "ZoneRoot_J10", "ZoneTrigger_J10", zoneSize, checks);
            }
            else
            {
                // Skip checks 4-14
                for (int i = 4; i <= 14; i++)
                {
                    checks.Add(new ValidationCheck($"Check #{i}", true, "Skipped (ZoneRoots parent not found)"));
                }
            }

            // 15. 보호된 오브젝트 확인
            CheckProtectedObjectV2("MapSettings", checks);
            CheckProtectedObjectV2("_WorldMap_Manual", checks);
            CheckProtectedObjectV2("DeepLightMapAutoBuilderContext", checks);

            // 16. GeneratedWorldRoot 밖의 ZoneRoot_* 확인
            int externalZoneRoots = CountExternalZoneRoots();
            checks.Add(new ValidationCheck("External ZoneRoot_* check", true,
                externalZoneRoots == 0
                    ? "No external ZoneRoot_* found outside GeneratedWorldRoot."
                    : $"Found {externalZoneRoots} external ZoneRoot_* (protected, not modified)"));

            // 결과 집계
            int totalChecks = checks.Count;
            int passedCount = 0;
            int failedCount = 0;

            // 로그 출력
            for (int i = 0; i < checks.Count; i++)
            {
                var check = checks[i];
                string status = check.Passed ? "[OK]" : "[FAIL]";
                log.AppendLine($"  {status} {i + 1}. {check.Label}: {check.Message}");
                if (check.Passed)
                    passedCount++;
                else
                    failedCount++;
            }

            // 결과 요약
            log.AppendLine($"\n=== Validation Result: {passedCount}/{totalChecks} passed, {failedCount}/{totalChecks} failed ===");

            if (failedCount == 0)
            {
                log.AppendLine($"[MapAutoBuilder] Zone setup validation passed. {zoneRootCount}/{totalExpected} ZoneRoots, sampled ZoneTriggers OK.");
            }
            else
            {
                log.AppendLine($"[MapAutoBuilder][ERROR] Zone setup validation failed. {failedCount} check(s) failed.");
            }

            Debug.Log(log.ToString());
            return failedCount == 0;
        }

        /// <summary>
        /// 특정 ZoneRoot이 존재하는지 검사하여 checks 리스트에 추가한다.
        /// </summary>
        private static void CheckZoneRootExistsV2(GameObject parent, string zoneRootName, List<ValidationCheck> checks)
        {
            Transform t = parent.transform.Find(zoneRootName);
            checks.Add(new ValidationCheck($"{zoneRootName} exists", t != null,
                t != null ? "Found." : "Not found!"));
        }

        /// <summary>
        /// 특정 ZoneRoot의 위치가 예상 좌표와 일치하는지 검사하여 checks 리스트에 추가한다.
        /// </summary>
        private static void CheckZoneRootPositionV2(GameObject parent, string zoneRootName, Vector3 expectedPosition, List<ValidationCheck> checks)
        {
            Transform t = parent.transform.Find(zoneRootName);
            if (t == null)
            {
                checks.Add(new ValidationCheck($"{zoneRootName} position", false, "ZoneRoot not found, cannot check position."));
                return;
            }

            Vector3 pos = t.localPosition;
            float tolerance = 1f;
            bool match = Mathf.Abs(pos.x - expectedPosition.x) < tolerance &&
                         Mathf.Abs(pos.y - expectedPosition.y) < tolerance &&
                         Mathf.Abs(pos.z - expectedPosition.z) < tolerance;

            checks.Add(new ValidationCheck($"{zoneRootName} position", match,
                match
                    ? $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) matches expected ({expectedPosition.x:F1}, {expectedPosition.y:F1}, {expectedPosition.z:F1})"
                    : $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) != expected ({expectedPosition.x:F1}, {expectedPosition.y:F1}, {expectedPosition.z:F1})"));
        }

        /// <summary>
        /// ZoneTrigger의 BoxCollider 설정을 검사하여 checks 리스트에 추가한다.
        /// </summary>
        private static void CheckZoneTriggerColliderV2(GameObject zoneRootsParent, string zoneRootName, string triggerName, float zoneSize, List<ValidationCheck> checks)
        {
            Transform zoneTransform = zoneRootsParent.transform.Find(zoneRootName);
            if (zoneTransform == null)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", true, $"Skipped ({zoneRootName} not found)"));
                return;
            }

            Transform triggersRoot = zoneTransform.Find("Triggers");
            if (triggersRoot == null)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", true, $"Skipped (Triggers not found)"));
                return;
            }

            Transform triggerTransform = triggersRoot.Find(triggerName);
            if (triggerTransform == null)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", false, "Trigger not found!"));
                return;
            }

            BoxCollider collider = triggerTransform.GetComponent<BoxCollider>();
            if (collider == null)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", false, "No BoxCollider component!"));
                return;
            }

            if (!collider.isTrigger)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", false, "isTrigger is false!"));
                return;
            }

            Vector3 expectedSize = new Vector3(zoneSize, 1600f, zoneSize);
            float tolerance = 0.1f;
            if (Vector3.Distance(collider.size, expectedSize) > tolerance)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", false,
                    $"size ({collider.size}) != expected ({expectedSize})"));
                return;
            }

            Vector3 expectedCenter = new Vector3(0f, -800f, 0f);
            if (Vector3.Distance(collider.center, expectedCenter) > tolerance)
            {
                checks.Add(new ValidationCheck($"{triggerName} BoxCollider", false,
                    $"center ({collider.center}) != expected ({expectedCenter})"));
                return;
            }

            checks.Add(new ValidationCheck($"{triggerName} BoxCollider", true,
                $"isTrigger=true, size={expectedSize}, center={expectedCenter}"));
        }

        /// <summary>
        /// 보호된 오브젝트가 Scene에 존재하는지 확인하여 checks 리스트에 추가한다.
        /// </summary>
        private static void CheckProtectedObjectV2(string objectName, List<ValidationCheck> checks)
        {
            GameObject obj = GameObject.Find(objectName);
            checks.Add(new ValidationCheck($"Protected object '{objectName}'", true,
                obj != null ? "Exists and is intact." : "Not found in Scene (may be intentional)."));
        }

        /// <summary>
        /// 특정 ZoneRoot이 존재하는지 검사한다.
        /// </summary>
        private static void CheckZoneRootExists(GameObject parent, string zoneRootName, StringBuilder log, ref int passed, ref int failed)
        {
            Transform t = parent.transform.Find(zoneRootName);
            if (t != null)
            {
                log.AppendLine($"  [OK] {zoneRootName} exists.");
                passed++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {zoneRootName} not found!");
                failed++;
            }
        }

        /// <summary>
        /// 특정 ZoneRoot의 위치가 예상 좌표와 일치하는지 검사한다.
        /// </summary>
        private static void CheckZoneRootPosition(GameObject parent, string zoneRootName, Vector3 expectedPosition, StringBuilder log, ref int passed, ref int failed)
        {
            Transform t = parent.transform.Find(zoneRootName);
            if (t == null)
            {
                log.AppendLine($"  [SKIP] Position check for {zoneRootName} skipped (not found).");
                return;
            }

            Vector3 pos = t.localPosition;
            float tolerance = 1f; // 1m tolerance
            if (Mathf.Abs(pos.x - expectedPosition.x) < tolerance &&
                Mathf.Abs(pos.y - expectedPosition.y) < tolerance &&
                Mathf.Abs(pos.z - expectedPosition.z) < tolerance)
            {
                log.AppendLine($"  [OK] {zoneRootName} position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) matches expected ({expectedPosition.x:F1}, {expectedPosition.y:F1}, {expectedPosition.z:F1})");
                passed++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {zoneRootName} position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) != expected ({expectedPosition.x:F1}, {expectedPosition.y:F1}, {expectedPosition.z:F1})");
                failed++;
            }
        }

        /// <summary>
        /// ZoneTrigger의 BoxCollider 설정을 검사한다.
        /// </summary>
        private static void CheckZoneTriggerCollider(GameObject zoneRootsParent, string zoneRootName, string triggerName, float zoneSize, StringBuilder log, ref int passed, ref int failed)
        {
            Transform zoneTransform = zoneRootsParent.transform.Find(zoneRootName);
            if (zoneTransform == null)
            {
                log.AppendLine($"  [SKIP] Collider check for {triggerName} skipped ({zoneRootName} not found).");
                return;
            }

            Transform triggersRoot = zoneTransform.Find("Triggers");
            if (triggersRoot == null)
            {
                log.AppendLine($"  [SKIP] Collider check for {triggerName} skipped (Triggers not found).");
                return;
            }

            Transform triggerTransform = triggersRoot.Find(triggerName);
            if (triggerTransform == null)
            {
                log.AppendLine($"  [FAIL] {triggerName} not found!");
                failed++;
                return;
            }

            BoxCollider collider = triggerTransform.GetComponent<BoxCollider>();
            if (collider == null)
            {
                log.AppendLine($"  [FAIL] {triggerName} has no BoxCollider!");
                failed++;
                return;
            }

            // isTrigger 검사
            if (!collider.isTrigger)
            {
                log.AppendLine($"  [FAIL] {triggerName} BoxCollider.isTrigger is false!");
                failed++;
                return;
            }

            // size 검사
            Vector3 expectedSize = new Vector3(zoneSize, 1600f, zoneSize);
            float tolerance = 0.1f;
            if (Vector3.Distance(collider.size, expectedSize) > tolerance)
            {
                log.AppendLine($"  [FAIL] {triggerName} BoxCollider.size ({collider.size}) != expected ({expectedSize})");
                failed++;
                return;
            }

            // center 검사
            Vector3 expectedCenter = new Vector3(0f, -800f, 0f);
            if (Vector3.Distance(collider.center, expectedCenter) > tolerance)
            {
                log.AppendLine($"  [FAIL] {triggerName} BoxCollider.center ({collider.center}) != expected ({expectedCenter})");
                failed++;
                return;
            }

            log.AppendLine($"  [OK] {triggerName} BoxCollider: isTrigger=true, size={expectedSize}, center={expectedCenter}");
            passed++;
        }

        /// <summary>
        /// 보호된 오브젝트가 Scene에 존재하는지 확인한다.
        /// </summary>
        private static void CheckProtectedObject(string objectName, StringBuilder log, ref int passed, ref int failed)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null)
            {
                log.AppendLine($"  [OK] Protected object '{objectName}' exists and is intact.");
                passed++;
            }
            else
            {
                log.AppendLine($"  [WARN] Protected object '{objectName}' not found in Scene. This may be intentional.");
                passed++; // Not a failure - some scenes may not have all objects
            }
        }

        /// <summary>
        /// GeneratedWorldRoot 밖에 있는 ZoneRoot_* 오브젝트 개수를 센다.
        /// </summary>
        private static int CountExternalZoneRoots()
        {
            int count = 0;
            // Scene root의 모든 GameObject 검색
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.StartsWith("ZoneRoot_"))
                {
                    // 부모 체인에 GeneratedWorldRoot가 있는지 확인
                    Transform parent = obj.transform.parent;
                    bool isUnderGenerated = false;
                    while (parent != null)
                    {
                        if (parent.name == "GeneratedWorldRoot")
                        {
                            isUnderGenerated = true;
                            break;
                        }
                        parent = parent.parent;
                    }

                    if (!isUnderGenerated)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        // ===== 로깅 =====

        /// <summary>
        /// logVerbose가 true일 때만 로그를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[MapAutoBuilder] {message}");
            }
        }
    }
}
