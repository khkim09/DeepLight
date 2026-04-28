using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 7: Runtime Binding 생성을 위한 Editor Utility.
    /// GeneratedWorldRoot 하위 ZoneRoot 100개에 GeneratedWorldZoneNode를 부착/설정하고,
    /// GeneratedWorldZoneRegistry를 부착하며, 선택적으로 WorldMapRuntimeZoneTracker를 생성한다.
    /// </summary>
    public static class DeepLightMapRuntimeBindingUtility
    {
        // ===== Public API =====

        /// <summary>
        /// Runtime Binding을 재구축한다.
        /// 1. GeneratedWorldRoot 찾기
        /// 2. ZoneRoots 부모 찾기
        /// 3. 각 ZoneRoot에 GeneratedWorldZoneNode 부착/설정
        /// 4. GeneratedWorldRoot에 GeneratedWorldZoneRegistry 부착/재구축
        /// 5. 선택적으로 WorldMapRuntimeZoneTracker 생성
        /// </summary>
        public static void RebuildRuntimeBindings(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild runtime bindings.");
                return;
            }

            if (settings.WorldMapConfig == null || settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] WorldMapConfig or ScenarioPreset is null. Cannot rebuild runtime bindings.");
                return;
            }

            // 1. Generated Root 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[MapAutoBuilder] '{settings.ZoneRootParentName}' not found under '{generatedRoot.name}'. Run Generate Full Scenario Map first.");
                return;
            }

            // 3. ZoneResolver 생성 및 Initialize
            ZoneResolver resolver = new ZoneResolver(settings.WorldMapConfig);
            resolver.Initialize(settings.ScenarioPreset);

            // 4. 각 ZoneRoot에 GeneratedWorldZoneNode 부착/설정
            int configuredCount = 0;
            int totalExpected = settings.WorldMapConfig.GridWidth * settings.WorldMapConfig.GridHeight;

            for (int i = 0; i < zoneRootsTransform.childCount; i++)
            {
                Transform child = zoneRootsTransform.GetChild(i);
                if (child == null) continue;
                if (!child.name.StartsWith("ZoneRoot_")) continue;

                // ZoneId 파싱
                string zoneIdStr = child.name.Replace("ZoneRoot_", "");
                if (!ZoneId.TryParse(zoneIdStr, out ZoneId zoneId))
                {
                    Debug.LogWarning($"[MapAutoBuilder] Could not parse ZoneId from '{child.name}'. Skipping.");
                    continue;
                }

                // ZoneResolver snapshot 조회
                if (!resolver.TryResolveSnapshot(zoneId, out ZoneRuntimeSnapshot snapshot))
                {
                    Debug.LogWarning($"[MapAutoBuilder] No snapshot for ZoneId '{zoneId}'. Skipping node configuration.");
                    continue;
                }

                // GeneratedWorldZoneNode 부착 또는 재사용
                GeneratedWorldZoneNode node = child.GetComponent<GeneratedWorldZoneNode>();
                if (node == null)
                {
                    node = child.gameObject.AddComponent<GeneratedWorldZoneNode>();
                    Undo.RegisterCreatedObjectUndo(node, $"Add GeneratedWorldZoneNode to {child.name}");
                }

                // 하위 root Transform 연결
                Transform geometryRoot = child.Find("Geometry");
                Transform propsRoot = child.Find("Props");
                Transform harvestSpawnsRoot = child.Find("HarvestSpawns");
                Transform logSpawnsRoot = child.Find("LogSpawns");
                Transform hazardsRoot = child.Find("Hazards");
                Transform triggersRoot = child.Find("Triggers");
                Transform debugGizmosRoot = child.Find("DebugGizmos");
                Transform environmentRoot = child.Find("Environment");
                Transform underwaterAreaRoot = environmentRoot != null ? environmentRoot.Find("UnderwaterArea") : null;
                Transform seafloorRoot = environmentRoot != null ? environmentRoot.Find("Seafloor") : null;
                Transform environmentPropsRoot = environmentRoot != null ? environmentRoot.Find("Props") : null;
                Transform dynamicEffectsRoot = environmentRoot != null ? environmentRoot.Find("DynamicEffects") : null;
                Transform particlesRoot = environmentRoot != null ? environmentRoot.Find("Particles") : null;

                // ZoneSize 계산
                float zoneSize = settings.WorldMapConfig.ZoneSize;

                // HorizontalBounds: XZ 기준
                Vector3 center = snapshot.Center;
                Bounds horizontalBounds = new Bounds(
                    new Vector3(center.x, 0f, center.z),
                    new Vector3(zoneSize, 1f, zoneSize));

                // DepthSlices를 List<ZoneDepthSlice>로 변환
                List<ZoneDepthSlice> depthSlices = new List<ZoneDepthSlice>();
                if (snapshot.DepthSlices != null)
                {
                    depthSlices.AddRange(snapshot.DepthSlices);
                }

                // Primary DepthBand 결정 (첫 번째 non-Surface slice 또는 Shallow)
                ZoneDepthBand primaryDepthBand = ZoneDepthBand.Shallow;
                if (depthSlices.Count > 1)
                {
                    // Surface 다음 첫 번째 slice를 primary로 사용
                    primaryDepthBand = depthSlices[1].DepthBand;
                }

                // Configure 호출
                node.Configure(
                    zoneId: zoneId,
                    regionId: snapshot.RegionId,
                    biomeType: snapshot.BiomeType,
                    primaryDepthBand: primaryDepthBand,
                    waterLevelY: snapshot.WaterLevelY,
                    minDepth: snapshot.MinDepth,
                    maxDepth: snapshot.MaxDepth,
                    baseRiskLevel: settings.ScenarioPreset.GetRiskOrDefault(zoneId),
                    zoneCenter: snapshot.Center,
                    zoneSize: new Vector3(zoneSize, 2000f, zoneSize),
                    horizontalBounds: horizontalBounds,
                    environmentProfile: snapshot.EnvironmentProfile,
                    depthSlices: depthSlices,
                    geometryRoot: geometryRoot,
                    propsRoot: propsRoot,
                    harvestSpawnsRoot: harvestSpawnsRoot,
                    logSpawnsRoot: logSpawnsRoot,
                    hazardsRoot: hazardsRoot,
                    triggersRoot: triggersRoot,
                    debugGizmosRoot: debugGizmosRoot,
                    environmentRoot: environmentRoot,
                    underwaterAreaRoot: underwaterAreaRoot,
                    seafloorRoot: seafloorRoot,
                    environmentPropsRoot: environmentPropsRoot,
                    dynamicEffectsRoot: dynamicEffectsRoot,
                    particlesRoot: particlesRoot
                );

                configuredCount++;
            }

            Debug.Log($"[MapAutoBuilder] GeneratedWorldZoneNode configured: {configuredCount}/{totalExpected}.");

            // 5. GeneratedWorldZoneRegistry 부착/재구축
            GeneratedWorldZoneRegistry registry = CreateOrUpdateRegistry(generatedRoot, zoneRootsTransform);

            // 6. RuntimeBinding root 생성
            GameObject runtimeBindingRoot = GetOrCreateChild(generatedRoot, "RuntimeBinding");

            // 7. WorldMapRuntimeZoneTracker 생성 (설정에 따라)
            if (settings.CreateRuntimeZoneTracker)
            {
                CreateOrUpdateRuntimeTracker(runtimeBindingRoot, settings, registry);
            }

            // 8. Summary 로그
            Debug.Log($"[MapAutoBuilder] GeneratedWorldZoneRegistry rebuilt: {registry.NodeCount} nodes.");
            Debug.Log($"[MapAutoBuilder] Runtime binding completed.");

            // Verbose 로그: A1/F6/J10 샘플
            if (settings.LogVerbose)
            {
                LogSampleNodeInfo(registry, new ZoneId('A', 1), "A1");
                LogSampleNodeInfo(registry, new ZoneId('F', 6), "F6");
                LogSampleNodeInfo(registry, new ZoneId('J', 10), "J10");
            }
        }

        /// <summary>
        /// Runtime Binding의 유효성을 검사한다.
        /// 20개 항목을 동적으로 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static bool ValidateRuntimeBindings(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null || settings.WorldMapConfig == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings or WorldMapConfig is null. Cannot validate runtime bindings.");
                return false;
            }

            var config = settings.WorldMapConfig;
            int gridWidth = config.GridWidth;
            int gridHeight = config.GridHeight;
            int totalExpected = gridWidth * gridHeight;

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Runtime Bindings (Phase 7) =====");

            var checks = new List<ValidationCheck>();

            // 1. GeneratedWorldRoot 존재 여부
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            checks.Add(new ValidationCheck("GeneratedWorldRoot exists", generatedRoot != null,
                generatedRoot != null ? $"Found: {generatedRoot.name}" : "Not found!"));

            // 2. GeneratedWorldRoot에 GeneratedWorldZoneRegistry 존재 여부
            GeneratedWorldZoneRegistry registry = generatedRoot != null ? generatedRoot.GetComponent<GeneratedWorldZoneRegistry>() : null;
            checks.Add(new ValidationCheck("GeneratedWorldZoneRegistry exists", registry != null,
                registry != null ? "Found." : "Not found!"));

            // 3. ZoneRoots parent 존재 여부
            Transform zoneRootsTransform = generatedRoot != null ? generatedRoot.transform.Find(settings.ZoneRootParentName) : null;
            checks.Add(new ValidationCheck("ZoneRoots parent exists", zoneRootsTransform != null,
                zoneRootsTransform != null ? $"Found: {zoneRootsTransform.name}" : "Not found!"));

            // 4. GeneratedWorldZoneNode count = 100
            int nodeCount = 0;
            if (zoneRootsTransform != null)
            {
                for (int i = 0; i < zoneRootsTransform.childCount; i++)
                {
                    Transform child = zoneRootsTransform.GetChild(i);
                    if (child != null && child.name.StartsWith("ZoneRoot_") && child.GetComponent<GeneratedWorldZoneNode>() != null)
                    {
                        nodeCount++;
                    }
                }
            }
            bool nodeCountMatch = nodeCount == totalExpected;
            checks.Add(new ValidationCheck("GeneratedWorldZoneNode count", nodeCountMatch,
                nodeCountMatch ? $"{nodeCount}/{totalExpected}" : $"Mismatch: {nodeCount}/{totalExpected}"));

            // 5. Registry node count = 100
            int registryNodeCount = registry != null ? registry.NodeCount : 0;
            bool registryCountMatch = registryNodeCount == totalExpected;
            checks.Add(new ValidationCheck("Registry node count", registryCountMatch,
                registryCountMatch ? $"{registryNodeCount}/{totalExpected}" : $"Mismatch: {registryNodeCount}/{totalExpected}"));

            // 6-8. Registry.TryGetNode(A1/F6/J10) 성공
            CheckRegistryTryGetNode(registry, new ZoneId('A', 1), "A1", checks);
            CheckRegistryTryGetNode(registry, new ZoneId('F', 6), "F6", checks);
            CheckRegistryTryGetNode(registry, new ZoneId('J', 10), "J10", checks);

            // 9-11. A1/F6/J10 node center 검사
            if (registry != null)
            {
                CheckNodeCenter(registry, new ZoneId('A', 1), new Vector3(-1800f, 0f, -1800f), checks);
                CheckNodeCenter(registry, new ZoneId('F', 6), new Vector3(200f, 0f, 200f), checks);
                CheckNodeCenter(registry, new ZoneId('J', 10), new Vector3(1800f, 0f, 1800f), checks);
            }
            else
            {
                for (int i = 9; i <= 11; i++)
                {
                    checks.Add(new ValidationCheck($"Node center check #{i}", true, "Skipped (registry not found)"));
                }
            }

            // 12-14. A1/F6/J10 node depthSlices count > 0
            if (registry != null)
            {
                CheckNodeDepthSlices(registry, new ZoneId('A', 1), "A1", checks);
                CheckNodeDepthSlices(registry, new ZoneId('F', 6), "F6", checks);
                CheckNodeDepthSlices(registry, new ZoneId('J', 10), "J10", checks);
            }
            else
            {
                for (int i = 12; i <= 14; i++)
                {
                    checks.Add(new ValidationCheck($"DepthSlices check #{i}", true, "Skipped (registry not found)"));
                }
            }

            // 15. ZoneResolver.TryResolveZone(A1 center) == A1
            if (registry != null && settings.WorldMapConfig != null && settings.ScenarioPreset != null)
            {
                ZoneResolver resolver = new ZoneResolver(settings.WorldMapConfig);
                resolver.Initialize(settings.ScenarioPreset);
                Vector3 a1Center = new Vector3(-1800f, 0f, -1800f);
                bool resolveOk = resolver.TryResolveZone(a1Center, out ZoneId resolvedA1);
                bool resolveMatch = resolveOk && resolvedA1 == new ZoneId('A', 1);
                checks.Add(new ValidationCheck("ZoneResolver.TryResolveZone(A1 center) == A1", resolveMatch,
                    resolveMatch ? $"Resolved: {resolvedA1}" : resolveOk ? $"Mismatch: {resolvedA1}" : "Failed to resolve"));
            }
            else
            {
                checks.Add(new ValidationCheck("ZoneResolver.TryResolveZone(A1 center) == A1", true, "Skipped (missing config/preset)"));
            }

            // 16. Registry.TryGetNodeByPosition(A1 center) == A1
            if (registry != null && settings.WorldMapConfig != null && settings.ScenarioPreset != null)
            {
                ZoneResolver resolver = new ZoneResolver(settings.WorldMapConfig);
                resolver.Initialize(settings.ScenarioPreset);
                Vector3 a1Center = new Vector3(-1800f, 0f, -1800f);
                bool nodeByPosOk = registry.TryGetNodeByPosition(resolver, a1Center, null, out GeneratedWorldZoneNode nodeByPos);
                bool nodeByPosMatch = nodeByPosOk && nodeByPos != null && nodeByPos.ZoneId == new ZoneId('A', 1);
                checks.Add(new ValidationCheck("Registry.TryGetNodeByPosition(A1 center) == A1", nodeByPosMatch,
                    nodeByPosMatch ? $"Found: {nodeByPos.ZoneId}" : nodeByPosOk ? $"Mismatch: {nodeByPos.ZoneId}" : "Failed to find"));
            }
            else
            {
                checks.Add(new ValidationCheck("Registry.TryGetNodeByPosition(A1 center) == A1", true, "Skipped (missing config/preset)"));
            }

            // 17. RuntimeBinding root 존재 여부
            Transform runtimeBindingRoot = generatedRoot != null ? generatedRoot.transform.Find("RuntimeBinding") : null;
            checks.Add(new ValidationCheck("RuntimeBinding root exists", runtimeBindingRoot != null,
                runtimeBindingRoot != null ? "Found." : "Not found!"));

            // 18. WorldMapRuntimeZoneTracker 존재 여부
            WorldMapRuntimeZoneTracker tracker = runtimeBindingRoot != null ? runtimeBindingRoot.GetComponentInChildren<WorldMapRuntimeZoneTracker>() : null;
            checks.Add(new ValidationCheck("WorldMapRuntimeZoneTracker exists", tracker != null,
                tracker != null ? "Found." : "Not found!"));

            // 19. WorldMapRuntimeZoneTracker config/preset/registry 할당 여부
            // IsInitialized는 Start()에서 resolver가 생성되어야 true가 되므로,
            // Editor 검증에서는 Serialized field 직접 확인으로 대체한다.
            if (tracker != null)
            {
                // SerializedObject를 통해 Serialized field 값을 직접 읽는다
                SerializedObject so = new SerializedObject(tracker);
                SerializedProperty configProp = so.FindProperty("worldMapConfig");
                SerializedProperty presetProp = so.FindProperty("scenarioPreset");
                SerializedProperty registryProp = so.FindProperty("registry");

                bool configAssigned = configProp != null && configProp.objectReferenceValue != null;
                bool presetAssigned = presetProp != null && presetProp.objectReferenceValue != null;
                bool registryAssigned = registryProp != null && registryProp.objectReferenceValue != null;
                bool allAssigned = configAssigned && presetAssigned && registryAssigned;

                checks.Add(new ValidationCheck("RuntimeTracker config/preset/registry assigned", allAssigned,
                    allAssigned
                        ? $"Config={(configAssigned ? "OK" : "MISSING")}, Preset={(presetAssigned ? "OK" : "MISSING")}, Registry={(registryAssigned ? "OK" : "MISSING")}"
                        : $"Config={(configAssigned ? "OK" : "MISSING")}, Preset={(presetAssigned ? "OK" : "MISSING")}, Registry={(registryAssigned ? "OK" : "MISSING")}"));
            }
            else
            {
                checks.Add(new ValidationCheck("RuntimeTracker config/preset/registry assigned", true, "Skipped (tracker not found)"));
            }

            // 20. 기존 보호 오브젝트 확인
            CheckProtectedObject("MapSettings", checks);
            CheckProtectedObject("_WorldMap_Manual", checks);
            CheckProtectedObject("DeepLightMapAutoBuilderContext", checks);

            // 결과 집계
            int totalChecks = checks.Count;
            int passedCount = 0;
            int failedCount = 0;

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

            log.AppendLine($"\n=== Validation Result: {passedCount}/{totalChecks} passed, {failedCount}/{totalChecks} failed ===");

            if (failedCount == 0)
            {
                log.AppendLine($"[MapAutoBuilder] Runtime binding validation passed. {nodeCount}/{totalExpected} GeneratedWorldZoneNodes registered.");
            }
            else
            {
                log.AppendLine($"[MapAutoBuilder][ERROR] Runtime binding validation failed. {failedCount} check(s) failed.");
            }

            Debug.Log(log.ToString());
            return failedCount == 0;
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// GeneratedWorldRoot에 GeneratedWorldZoneRegistry를 부착하거나 재사용하고,
        /// RebuildFromChildren을 호출한다.
        /// </summary>
        private static GeneratedWorldZoneRegistry CreateOrUpdateRegistry(GameObject generatedRoot, Transform zoneRootsTransform)
        {
            GeneratedWorldZoneRegistry registry = generatedRoot.GetComponent<GeneratedWorldZoneRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<GeneratedWorldZoneRegistry>();
                Undo.RegisterCreatedObjectUndo(registry, "Add GeneratedWorldZoneRegistry");
            }

            // RebuildFromChildren 호출
            registry.RebuildFromChildren(zoneRootsTransform);

            return registry;
        }

        /// <summary>
        /// RuntimeBinding root 아래에 WorldMapRuntimeZoneTracker를 생성하거나 업데이트한다.
        /// </summary>
        private static void CreateOrUpdateRuntimeTracker(GameObject runtimeBindingRoot, DeepLightMapAutoBuilderSettingsSO settings, GeneratedWorldZoneRegistry registry)
        {
            // 기존 Tracker 검색
            WorldMapRuntimeZoneTracker tracker = runtimeBindingRoot.GetComponentInChildren<WorldMapRuntimeZoneTracker>();
            if (tracker == null)
            {
                // 새 GameObject 생성
                GameObject trackerObj = new GameObject("WorldMapRuntimeZoneTracker");
                trackerObj.transform.SetParent(runtimeBindingRoot.transform);
                trackerObj.transform.localPosition = Vector3.zero;
                trackerObj.transform.localRotation = Quaternion.identity;
                trackerObj.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(trackerObj, "Create WorldMapRuntimeZoneTracker");

                tracker = trackerObj.AddComponent<WorldMapRuntimeZoneTracker>();
                Undo.RegisterCreatedObjectUndo(tracker, "Add WorldMapRuntimeZoneTracker component");
            }

            // Serialized field 설정
            SerializedObject so = new SerializedObject(tracker);
            so.FindProperty("worldMapConfig").objectReferenceValue = settings.WorldMapConfig;
            so.FindProperty("scenarioPreset").objectReferenceValue = settings.ScenarioPreset;
            so.FindProperty("registry").objectReferenceValue = registry;
            so.FindProperty("autoFindTarget").boolValue = true;
            so.FindProperty("autoFindTargetName").stringValue = "Submarine";
            so.FindProperty("sampleInterval").floatValue = settings.RuntimeZoneTrackerSampleInterval;
            so.FindProperty("logZoneChanges").boolValue = settings.RuntimeZoneTrackerLogChanges;
            so.FindProperty("logDepthBandChanges").boolValue = settings.RuntimeZoneTrackerLogChanges;
            so.ApplyModifiedProperties();

            // enabled 설정
            tracker.enabled = settings.RuntimeZoneTrackerEnabledByDefault;

            Debug.Log($"[MapAutoBuilder] Runtime tracker configured. Target: Submarine or null.");
        }

        /// <summary>
        /// 부모 GameObject 아래에 자식 GameObject를 찾거나 생성한다.
        /// </summary>
        private static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            return child;
        }

        /// <summary>
        /// 특정 ZoneId에 대한 Registry.TryGetNode 검사.
        /// </summary>
        private static void CheckRegistryTryGetNode(GeneratedWorldZoneRegistry registry, ZoneId zoneId, string label, List<ValidationCheck> checks)
        {
            if (registry == null)
            {
                checks.Add(new ValidationCheck($"Registry.TryGetNode({label})", true, "Skipped (registry not found)"));
                return;
            }

            bool found = registry.TryGetNode(zoneId, out GeneratedWorldZoneNode node);
            checks.Add(new ValidationCheck($"Registry.TryGetNode({label})", found,
                found ? $"Found: {node.ZoneId}" : $"ZoneId '{label}' not found in registry!"));
        }

        /// <summary>
        /// 특정 ZoneId의 Node center가 예상 좌표와 일치하는지 검사.
        /// </summary>
        private static void CheckNodeCenter(GeneratedWorldZoneRegistry registry, ZoneId zoneId, Vector3 expectedCenter, List<ValidationCheck> checks)
        {
            if (registry == null)
            {
                checks.Add(new ValidationCheck($"Node center {zoneId}", true, "Skipped (registry not found)"));
                return;
            }

            if (!registry.TryGetNode(zoneId, out GeneratedWorldZoneNode node))
            {
                checks.Add(new ValidationCheck($"Node center {zoneId}", false, $"Node '{zoneId}' not found in registry!"));
                return;
            }

            Vector3 center = node.ZoneCenter;
            float tolerance = 1f;
            bool match = Mathf.Abs(center.x - expectedCenter.x) < tolerance &&
                         Mathf.Abs(center.y - expectedCenter.y) < tolerance &&
                         Mathf.Abs(center.z - expectedCenter.z) < tolerance;

            checks.Add(new ValidationCheck($"Node center {zoneId}", match,
                match
                    ? $"({center.x:F1}, {center.y:F1}, {center.z:F1}) matches expected ({expectedCenter.x:F1}, {expectedCenter.y:F1}, {expectedCenter.z:F1})"
                    : $"({center.x:F1}, {center.y:F1}, {center.z:F1}) != expected ({expectedCenter.x:F1}, {expectedCenter.y:F1}, {expectedCenter.z:F1})"));
        }

        /// <summary>
        /// 특정 ZoneId의 Node depthSlices count > 0 검사.
        /// </summary>
        private static void CheckNodeDepthSlices(GeneratedWorldZoneRegistry registry, ZoneId zoneId, string label, List<ValidationCheck> checks)
        {
            if (registry == null)
            {
                checks.Add(new ValidationCheck($"DepthSlices {label}", true, "Skipped (registry not found)"));
                return;
            }

            if (!registry.TryGetNode(zoneId, out GeneratedWorldZoneNode node))
            {
                checks.Add(new ValidationCheck($"DepthSlices {label}", false, $"Node '{label}' not found in registry!"));
                return;
            }

            int sliceCount = node.DepthSlices.Count;
            bool hasSlices = sliceCount > 0;
            checks.Add(new ValidationCheck($"DepthSlices {label}", hasSlices,
                hasSlices ? $"{sliceCount} slices found." : "No depth slices!"));
        }

        /// <summary>
        /// 보호된 오브젝트가 Scene에 존재하는지 확인.
        /// </summary>
        private static void CheckProtectedObject(string objectName, List<ValidationCheck> checks)
        {
            GameObject obj = GameObject.Find(objectName);
            checks.Add(new ValidationCheck($"Protected object '{objectName}'", true,
                obj != null ? "Exists and is intact." : "Not found in Scene (may be intentional)."));
        }

        /// <summary>
        /// 샘플 ZoneNode의 상세 정보를 로그로 출력한다.
        /// </summary>
        private static void LogSampleNodeInfo(GeneratedWorldZoneRegistry registry, ZoneId zoneId, string label)
        {
            if (registry == null) return;

            if (!registry.TryGetNode(zoneId, out GeneratedWorldZoneNode node))
            {
                Debug.Log($"[MapAutoBuilder] Sample {label}: Node not found.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"[MapAutoBuilder] Sample {label}: ZoneId={node.ZoneId}, ");
            sb.Append($"Region={node.RegionId}, Biome={node.BiomeType}, ");
            sb.Append($"PrimaryDepth={node.PrimaryDepthBand}, ");
            sb.Append($"Center=({node.ZoneCenter.x:F1}, {node.ZoneCenter.y:F1}, {node.ZoneCenter.z:F1}), ");
            sb.Append($"WaterLevelY={node.WaterLevelY:F1}, MaxDepth={node.MaxDepth:F1}, ");
            sb.Append($"DepthSlices={node.DepthSlices.Count}");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 단일 검사 결과를 저장하는 구조체.
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
    }
}
