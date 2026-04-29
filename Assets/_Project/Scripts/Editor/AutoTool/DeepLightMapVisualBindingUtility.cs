using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.VisualAdapters;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 8 + Phase 9: Visual Runtime Binding 생성을 위한 Editor Utility.
    /// GeneratedWorldRoot/RuntimeBinding 아래 WorldMapVisualController를 생성/설정하고,
    /// WorldMapRuntimeZoneTracker와 WorldMapVisualProfileSetSO를 연결한다.
    /// Phase 9: Visual Adapter Root + Adapter GameObject들을 생성/설정한다.
    /// </summary>
    public static class DeepLightMapVisualBindingUtility
    {
        private const string RuntimeBindingRootName = "RuntimeBinding";
        private const string VisualControllerName = "WorldMapVisualController";
        private const string VisualAdaptersRootName = "VisualAdapters";
        private const string DebugAdapterName = "DebugAdapter";
        private const string WaterMaterialAdapterName = "WaterMaterialAdapter";
        private const string RuntimeVolumeAdapterName = "RuntimeVolumeAdapter";
        private const string RuntimeVisualVolumeName = "RuntimeVisualVolume";

        // ===== Public API =====

        /// <summary>
        /// Visual Runtime Binding을 재구축한다.
        /// 1. GeneratedWorldRoot 찾기
        /// 2. RuntimeBinding root 찾기 또는 생성
        /// 3. WorldMapRuntimeZoneTracker 찾기
        /// 4. Visual Profile Set 생성/할당
        /// 5. WorldMapVisualController 생성/설정
        /// </summary>
        public static void RebuildVisualRuntimeBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild visual runtime binding.");
                return;
            }

            // 1. Generated Root 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. RuntimeBinding root 찾기 또는 생성
            GameObject runtimeBindingRoot = GetOrCreateChild(generatedRoot, RuntimeBindingRootName);

            // 3. WorldMapRuntimeZoneTracker 찾기
            WorldMapRuntimeZoneTracker tracker = runtimeBindingRoot.GetComponentInChildren<WorldMapRuntimeZoneTracker>();
            if (tracker == null)
            {
                // GeneratedWorldRoot 전체에서 검색
                tracker = generatedRoot.GetComponentInChildren<WorldMapRuntimeZoneTracker>();
            }

            if (tracker == null)
            {
                Debug.LogWarning("[MapAutoBuilder] WorldMapRuntimeZoneTracker not found. VisualController will be created without tracker reference.");
            }

            // 4. Visual Profile Set 생성/할당
            WorldMapVisualProfileSetSO profileSet = settings.VisualProfileSet;
            if (profileSet == null)
            {
                // 자동 생성 시도
                profileSet = DeepLightMapVisualProfileAssetCreator.CreateOrUpdateDefaultVisualProfileSet();
                if (profileSet != null)
                {
                    // SettingsSO에 할당
                    SerializedObject settingsSo = new SerializedObject(settings);
                    settingsSo.FindProperty("visualProfileSet").objectReferenceValue = profileSet;
                    settingsSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    Debug.Log("[MapAutoBuilder] SettingsSO visualProfileSet auto-assigned.");
                }
            }

            // 5. WorldMapVisualController 생성/설정
            CreateOrUpdateVisualController(runtimeBindingRoot, settings, tracker, profileSet);

            Debug.Log("[MapAutoBuilder] WorldMapVisualController configured.");
        }

        /// <summary>
        /// Phase 9: Visual Adapter Binding을 재구축한다.
        /// 1. RuntimeBinding root 찾기
        /// 2. RuntimeVisualVolume GameObject 생성 (Volume component 포함)
        /// 3. VisualAdapters root 찾기 또는 생성
        /// 4. DebugAdapter 생성/설정
        /// 5. WaterMaterialAdapter 생성/설정 (GlobalWater Renderer 찾기)
        /// 6. RuntimeVolumeAdapter 생성/설정 (RuntimeVisualVolume 찾기)
        /// 7. WorldMapVisualAdapterRoot 생성/설정
        /// 8. WorldMapVisualController에 adapterRoot 연결
        /// </summary>
        public static void RebuildVisualAdapterBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild visual adapter binding.");
                return;
            }

            // 1. Generated Root 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. RuntimeBinding root 찾기
            GameObject runtimeBindingRoot = GetOrCreateChild(generatedRoot, RuntimeBindingRootName);

            // 3. RuntimeVisualVolume GameObject 생성 (Volume component 포함)
            CreateOrUpdateRuntimeVisualVolume(runtimeBindingRoot, settings);

            // 4. VisualAdapters root 찾기 또는 생성
            GameObject visualAdaptersRoot = GetOrCreateChild(runtimeBindingRoot, VisualAdaptersRootName);

            // 5. DebugAdapter 생성/설정
            CreateOrUpdateDebugAdapter(visualAdaptersRoot);

            // 6. WaterMaterialAdapter 생성/설정 (GlobalWater Renderer 찾기)
            CreateOrUpdateWaterMaterialAdapter(visualAdaptersRoot, generatedRoot);

            // 7. RuntimeVolumeAdapter 생성/설정 (RuntimeVisualVolume 찾기)
            CreateOrUpdateRuntimeVolumeAdapter(visualAdaptersRoot, runtimeBindingRoot);

            // 8. WorldMapVisualAdapterRoot 생성/설정
            WorldMapVisualAdapterRoot adapterRoot = CreateOrUpdateAdapterRoot(visualAdaptersRoot);

            // 9. WorldMapVisualController에 adapterRoot 연결
            LinkAdapterRootToVisualController(runtimeBindingRoot, adapterRoot);

            Debug.Log("[MapAutoBuilder] Visual adapter binding completed.");
        }

        /// <summary>
        /// Phase 9: Visual Adapter Binding의 유효성을 검사한다.
        /// </summary>
        public static bool ValidateVisualAdapterBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate visual adapter binding.");
                return false;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Visual Adapter Binding (Phase 9) =====");

            var checks = new System.Collections.Generic.List<ValidationCheck>();

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            checks.Add(new ValidationCheck("GeneratedWorldRoot exists", generatedRoot != null,
                generatedRoot != null ? "Found" : "Not found"));

            // 2. RuntimeBinding root exists
            Transform runtimeBindingRoot = generatedRoot != null ? generatedRoot.transform.Find(RuntimeBindingRootName) : null;
            checks.Add(new ValidationCheck("RuntimeBinding root exists", runtimeBindingRoot != null,
                runtimeBindingRoot != null ? "Found" : "Not found"));

            // 3. VisualAdapters root exists
            Transform visualAdaptersRoot = runtimeBindingRoot != null ? runtimeBindingRoot.Find(VisualAdaptersRootName) : null;
            checks.Add(new ValidationCheck("VisualAdapters root exists", visualAdaptersRoot != null,
                visualAdaptersRoot != null ? "Found" : "Not found"));

            // 4. WorldMapVisualAdapterRoot exists
            WorldMapVisualAdapterRoot adapterRoot = visualAdaptersRoot != null
                ? visualAdaptersRoot.GetComponent<WorldMapVisualAdapterRoot>()
                : null;
            checks.Add(new ValidationCheck("WorldMapVisualAdapterRoot exists", adapterRoot != null,
                adapterRoot != null ? "Found" : "Not found"));

            // 5. DebugAdapter exists
            Transform debugAdapterTransform = visualAdaptersRoot != null ? visualAdaptersRoot.Find(DebugAdapterName) : null;
            WorldMapVisualDebugAdapter debugAdapter = debugAdapterTransform != null
                ? debugAdapterTransform.GetComponent<WorldMapVisualDebugAdapter>()
                : null;
            checks.Add(new ValidationCheck("DebugAdapter exists", debugAdapter != null,
                debugAdapter != null ? "Found" : "Not found"));

            // 6. WaterMaterialAdapter exists
            Transform waterAdapterTransform = visualAdaptersRoot != null ? visualAdaptersRoot.Find(WaterMaterialAdapterName) : null;
            WorldMapWaterMaterialAdapter waterAdapter = waterAdapterTransform != null
                ? waterAdapterTransform.GetComponent<WorldMapWaterMaterialAdapter>()
                : null;
            checks.Add(new ValidationCheck("WaterMaterialAdapter exists", waterAdapter != null,
                waterAdapter != null ? "Found" : "Not found"));

            // 7. RuntimeVolumeAdapter exists
            Transform volumeAdapterTransform = visualAdaptersRoot != null ? visualAdaptersRoot.Find(RuntimeVolumeAdapterName) : null;
            WorldMapRuntimeVolumeAdapter volumeAdapter = volumeAdapterTransform != null
                ? volumeAdapterTransform.GetComponent<WorldMapRuntimeVolumeAdapter>()
                : null;
            checks.Add(new ValidationCheck("RuntimeVolumeAdapter exists", volumeAdapter != null,
                volumeAdapter != null ? "Found" : "Not found"));

            // 8. WorldMapVisualController exists and has adapterRoot assigned
            WorldMapVisualController visualController = runtimeBindingRoot != null
                ? runtimeBindingRoot.GetComponentInChildren<WorldMapVisualController>()
                : null;
            if (visualController != null)
            {
                bool adapterRootAssigned = visualController.AdapterRoot != null;
                checks.Add(new ValidationCheck("VisualController adapterRoot assigned", adapterRootAssigned,
                    adapterRootAssigned ? "OK" : "MISSING"));
            }
            else
            {
                checks.Add(new ValidationCheck("VisualController adapterRoot assigned", true, "Skipped (controller not found)"));
            }

            // 9. AdapterRoot has adapterBehaviours populated
            if (adapterRoot != null)
            {
                int behaviourCount = adapterRoot.AdapterBehaviourCount;
                checks.Add(new ValidationCheck("AdapterRoot behaviours count > 0", behaviourCount > 0,
                    behaviourCount > 0 ? $"{behaviourCount} behaviours" : "Empty"));
            }
            else
            {
                checks.Add(new ValidationCheck("AdapterRoot behaviours count > 0", true, "Skipped (adapterRoot not found)"));
            }

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
                log.AppendLine($"[MapAutoBuilder] Visual adapter binding validation passed. {totalChecks}/{totalChecks} checks.");
            }
            else
            {
                log.AppendLine($"[MapAutoBuilder][ERROR] Visual adapter binding validation failed. {failedCount} check(s) failed.");
            }

            Debug.Log(log.ToString());
            return failedCount == 0;
        }

        // ===== Phase 9 Internal Helpers =====

        /// <summary>
        /// RuntimeVisualVolume GameObject를 찾거나 생성하고 Volume component를 부착한다.
        /// RuntimeBinding root 아래에 위치하며, RuntimeVolumeAdapter가 참조하는 대상이다.
        /// </summary>
        private static void CreateOrUpdateRuntimeVisualVolume(GameObject runtimeBindingRoot, DeepLightMapAutoBuilderSettingsSO settings)
        {
            Transform existing = runtimeBindingRoot.transform.Find(RuntimeVisualVolumeName);
            GameObject volumeObj;

            if (existing != null)
            {
                volumeObj = existing.gameObject;
            }
            else
            {
                volumeObj = new GameObject(RuntimeVisualVolumeName);
                volumeObj.transform.SetParent(runtimeBindingRoot.transform);
                volumeObj.transform.localPosition = Vector3.zero;
                volumeObj.transform.localRotation = Quaternion.identity;
                volumeObj.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(volumeObj, $"Create {RuntimeVisualVolumeName}");
            }

            // Volume component 부착
            Volume volume = volumeObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObj.AddComponent<Volume>();
                Undo.RegisterCreatedObjectUndo(volume, "Add Volume component");
            }

            // Volume 설정
            SerializedObject so = new SerializedObject(volume);
            SerializedProperty isGlobalProp = so.FindProperty("m_IsGlobal");
            if (isGlobalProp != null)
            {
                isGlobalProp.boolValue = settings.RuntimeVolumeIsGlobal;
            }
            else
            {
                Debug.LogWarning("[MapAutoBuilder] Volume.m_IsGlobal property not found. Skipping isGlobal setting.");
            }

            SerializedProperty priorityProp = so.FindProperty("m_Priority");
            if (priorityProp != null)
            {
                priorityProp.floatValue = settings.RuntimeVolumePriority;
            }
            else
            {
                // Fallback: "priority" (일부 Unity 버전)
                priorityProp = so.FindProperty("priority");
                if (priorityProp != null)
                {
                    priorityProp.floatValue = settings.RuntimeVolumePriority;
                }
                else
                {
                    Debug.LogWarning("[MapAutoBuilder] Volume priority property not found. Skipping priority setting.");
                }
            }
            so.ApplyModifiedProperties();

            // VolumeProfile은 null로 두고, RuntimeVolumeAdapter가 런타임에 동적으로 할당한다.

            Debug.Log($"[MapAutoBuilder] RuntimeVisualVolume ready: {volumeObj.name} (isGlobal={settings.RuntimeVolumeIsGlobal}, priority={settings.RuntimeVolumePriority})");
        }

        /// <summary>
        /// DebugAdapter GameObject를 찾거나 생성하고 WorldMapVisualDebugAdapter를 부착한다.
        /// </summary>
        private static void CreateOrUpdateDebugAdapter(GameObject visualAdaptersRoot)
        {
            Transform existing = visualAdaptersRoot.transform.Find(DebugAdapterName);
            GameObject adapterObj;

            if (existing != null)
            {
                adapterObj = existing.gameObject;
            }
            else
            {
                adapterObj = new GameObject(DebugAdapterName);
                adapterObj.transform.SetParent(visualAdaptersRoot.transform);
                adapterObj.transform.localPosition = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(adapterObj, $"Create {DebugAdapterName}");
            }

            // WorldMapVisualDebugAdapter 부착
            WorldMapVisualDebugAdapter debugAdapter = adapterObj.GetComponent<WorldMapVisualDebugAdapter>();
            if (debugAdapter == null)
            {
                debugAdapter = adapterObj.AddComponent<WorldMapVisualDebugAdapter>();
                Undo.RegisterCreatedObjectUndo(debugAdapter, "Add WorldMapVisualDebugAdapter component");
            }

            Debug.Log($"[MapAutoBuilder] DebugAdapter ready: {adapterObj.name}");
        }

        /// <summary>
        /// WaterMaterialAdapter GameObject를 찾거나 생성하고 WorldMapWaterMaterialAdapter를 부착한다.
        /// GlobalWater Renderer를 자동으로 찾아 targetRenderer에 할당한다.
        /// </summary>
        private static void CreateOrUpdateWaterMaterialAdapter(GameObject visualAdaptersRoot, GameObject generatedRoot)
        {
            Transform existing = visualAdaptersRoot.transform.Find(WaterMaterialAdapterName);
            GameObject adapterObj;

            if (existing != null)
            {
                adapterObj = existing.gameObject;
            }
            else
            {
                adapterObj = new GameObject(WaterMaterialAdapterName);
                adapterObj.transform.SetParent(visualAdaptersRoot.transform);
                adapterObj.transform.localPosition = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(adapterObj, $"Create {WaterMaterialAdapterName}");
            }

            // WorldMapWaterMaterialAdapter 부착
            WorldMapWaterMaterialAdapter waterAdapter = adapterObj.GetComponent<WorldMapWaterMaterialAdapter>();
            if (waterAdapter == null)
            {
                waterAdapter = adapterObj.AddComponent<WorldMapWaterMaterialAdapter>();
                Undo.RegisterCreatedObjectUndo(waterAdapter, "Add WorldMapWaterMaterialAdapter component");
            }

            // GlobalWater Renderer 자동 찾기
            if (waterAdapter.TargetRenderer == null)
            {
                // GeneratedWorldRoot/GlobalWater 아래에서 Renderer 찾기
                Transform globalWaterRoot = generatedRoot.transform.Find("GlobalWater");
                if (globalWaterRoot != null)
                {
                    Renderer waterRenderer = globalWaterRoot.GetComponentInChildren<Renderer>();
                    if (waterRenderer != null)
                    {
                        SerializedObject so = new SerializedObject(waterAdapter);
                        so.FindProperty("targetRenderer").objectReferenceValue = waterRenderer;
                        so.ApplyModifiedProperties();
                        Debug.Log($"[MapAutoBuilder] WaterMaterialAdapter targetRenderer auto-assigned: {waterRenderer.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[MapAutoBuilder] No Renderer found under GlobalWater. WaterMaterialAdapter targetRenderer will be null.");
                    }
                }
                else
                {
                    Debug.LogWarning("[MapAutoBuilder] GlobalWater root not found. WaterMaterialAdapter targetRenderer will be null.");
                }
            }

            Debug.Log($"[MapAutoBuilder] WaterMaterialAdapter ready: {adapterObj.name}");
        }

        /// <summary>
        /// RuntimeVolumeAdapter GameObject를 찾거나 생성하고 WorldMapRuntimeVolumeAdapter를 부착한다.
        /// RuntimeBinding root 아래 Runtime Volume을 자동으로 찾아 targetVolume에 할당한다.
        /// </summary>
        private static void CreateOrUpdateRuntimeVolumeAdapter(GameObject visualAdaptersRoot, GameObject runtimeBindingRoot)
        {
            Transform existing = visualAdaptersRoot.transform.Find(RuntimeVolumeAdapterName);
            GameObject adapterObj;

            if (existing != null)
            {
                adapterObj = existing.gameObject;
            }
            else
            {
                adapterObj = new GameObject(RuntimeVolumeAdapterName);
                adapterObj.transform.SetParent(visualAdaptersRoot.transform);
                adapterObj.transform.localPosition = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(adapterObj, $"Create {RuntimeVolumeAdapterName}");
            }

            // WorldMapRuntimeVolumeAdapter 부착
            WorldMapRuntimeVolumeAdapter volumeAdapter = adapterObj.GetComponent<WorldMapRuntimeVolumeAdapter>();
            if (volumeAdapter == null)
            {
                volumeAdapter = adapterObj.AddComponent<WorldMapRuntimeVolumeAdapter>();
                Undo.RegisterCreatedObjectUndo(volumeAdapter, "Add WorldMapRuntimeVolumeAdapter component");
            }

            // Runtime Volume 자동 찾기
            if (volumeAdapter.TargetVolume == null)
            {
                // RuntimeBinding root 아래 Volume 컴포넌트 찾기
                Volume volume = runtimeBindingRoot.GetComponentInChildren<Volume>();
                if (volume != null)
                {
                    SerializedObject so = new SerializedObject(volumeAdapter);
                    so.FindProperty("targetVolume").objectReferenceValue = volume;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[MapAutoBuilder] RuntimeVolumeAdapter targetVolume auto-assigned: {volume.name}");
                }
                else
                {
                    Debug.LogWarning("[MapAutoBuilder] No Volume found under RuntimeBinding. RuntimeVolumeAdapter targetVolume will be null.");
                }
            }

            Debug.Log($"[MapAutoBuilder] RuntimeVolumeAdapter ready: {adapterObj.name}");
        }

        /// <summary>
        /// WorldMapVisualAdapterRoot를 찾거나 생성하고 adapterBehaviours를 설정한다.
        /// </summary>
        private static WorldMapVisualAdapterRoot CreateOrUpdateAdapterRoot(GameObject visualAdaptersRoot)
        {
            // WorldMapVisualAdapterRoot는 VisualAdapters root 자체에 붙인다
            WorldMapVisualAdapterRoot adapterRoot = visualAdaptersRoot.GetComponent<WorldMapVisualAdapterRoot>();
            if (adapterRoot == null)
            {
                adapterRoot = visualAdaptersRoot.AddComponent<WorldMapVisualAdapterRoot>();
                Undo.RegisterCreatedObjectUndo(adapterRoot, "Add WorldMapVisualAdapterRoot component");
            }

            // adapterBehaviours 설정 (SerializedProperty로 접근)
            SerializedObject so = new SerializedObject(adapterRoot);
            SerializedProperty behavioursProp = so.FindProperty("adapterBehaviours");
            if (behavioursProp != null)
            {
                behavioursProp.ClearArray();

                // DebugAdapter 추가
                Transform debugTransform = visualAdaptersRoot.transform.Find(DebugAdapterName);
                if (debugTransform != null)
                {
                    WorldMapVisualDebugAdapter debugAdapter = debugTransform.GetComponent<WorldMapVisualDebugAdapter>();
                    if (debugAdapter != null)
                    {
                        behavioursProp.InsertArrayElementAtIndex(0);
                        behavioursProp.GetArrayElementAtIndex(0).objectReferenceValue = debugAdapter;
                    }
                }

                // WaterMaterialAdapter 추가
                Transform waterTransform = visualAdaptersRoot.transform.Find(WaterMaterialAdapterName);
                if (waterTransform != null)
                {
                    WorldMapWaterMaterialAdapter waterAdapter = waterTransform.GetComponent<WorldMapWaterMaterialAdapter>();
                    if (waterAdapter != null)
                    {
                        int index = behavioursProp.arraySize;
                        behavioursProp.InsertArrayElementAtIndex(index);
                        behavioursProp.GetArrayElementAtIndex(index).objectReferenceValue = waterAdapter;
                    }
                }

                // RuntimeVolumeAdapter 추가
                Transform volumeTransform = visualAdaptersRoot.transform.Find(RuntimeVolumeAdapterName);
                if (volumeTransform != null)
                {
                    WorldMapRuntimeVolumeAdapter volumeAdapter = volumeTransform.GetComponent<WorldMapRuntimeVolumeAdapter>();
                    if (volumeAdapter != null)
                    {
                        int index = behavioursProp.arraySize;
                        behavioursProp.InsertArrayElementAtIndex(index);
                        behavioursProp.GetArrayElementAtIndex(index).objectReferenceValue = volumeAdapter;
                    }
                }

                so.ApplyModifiedProperties();
                Debug.Log($"[MapAutoBuilder] AdapterRoot behaviours populated: {behavioursProp.arraySize} adapters.");
            }

            return adapterRoot;
        }

        /// <summary>
        /// WorldMapVisualController에 adapterRoot를 연결한다.
        /// </summary>
        private static void LinkAdapterRootToVisualController(GameObject runtimeBindingRoot, WorldMapVisualAdapterRoot adapterRoot)
        {
            WorldMapVisualController controller = runtimeBindingRoot.GetComponentInChildren<WorldMapVisualController>();
            if (controller == null)
            {
                Debug.LogWarning("[MapAutoBuilder] WorldMapVisualController not found. Cannot link adapterRoot.");
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty adapterRootProp = so.FindProperty("adapterRoot");
            if (adapterRootProp != null)
            {
                adapterRootProp.objectReferenceValue = adapterRoot;
                so.ApplyModifiedProperties();
                Debug.Log("[MapAutoBuilder] WorldMapVisualController adapterRoot linked.");
            }
        }

        /// <summary>
        /// Visual Runtime Binding의 유효성을 검사한다.
        /// 19개 항목을 동적으로 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static bool ValidateVisualRuntimeBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate visual runtime binding.");
                return false;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Visual Profiles (Phase 8) =====");

            var checks = new System.Collections.Generic.List<ValidationCheck>();

            // 1. settings exists
            checks.Add(new ValidationCheck("Settings exists", settings != null, settings != null ? "OK" : "NULL"));

            // 2. settings.VisualProfileSet exists
            bool profileSetExists = settings.VisualProfileSet != null;
            checks.Add(new ValidationCheck("Settings.VisualProfileSet exists", profileSetExists,
                profileSetExists ? $"Found: {settings.VisualProfileSet.name}" : "NULL"));

            // 3-7. defaultRules count > 0 and each band evaluatable
            if (profileSetExists)
            {
                var profileSet = settings.VisualProfileSet;
                int defaultRuleCount = profileSet.DefaultRules != null ? profileSet.DefaultRules.Count : 0;
                checks.Add(new ValidationCheck("DefaultRules count > 0", defaultRuleCount > 0,
                    defaultRuleCount > 0 ? $"{defaultRuleCount} rules" : "Empty"));

                // 각 DepthBand 평가 가능 여부
                CheckBandEvaluatable(profileSet, ZoneDepthBand.Surface, checks);
                CheckBandEvaluatable(profileSet, ZoneDepthBand.Shallow, checks);
                CheckBandEvaluatable(profileSet, ZoneDepthBand.Mid, checks);
                CheckBandEvaluatable(profileSet, ZoneDepthBand.Deep, checks);
                CheckBandEvaluatable(profileSet, ZoneDepthBand.Forbidden, checks);

                // Hub biome override 평가 가능
                CheckBiomeOverrideEvaluatable(profileSet, ZoneBiomeType.Hub, checks);
                // AbyssExperiment biome override 평가 가능
                CheckBiomeOverrideEvaluatable(profileSet, ZoneBiomeType.AbyssExperiment, checks);
            }
            else
            {
                // Skip checks 4-10
                for (int i = 4; i <= 10; i++)
                {
                    checks.Add(new ValidationCheck($"Profile check #{i}", true, "Skipped (profileSet not found)"));
                }
            }

            // 11. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            checks.Add(new ValidationCheck("GeneratedWorldRoot exists", generatedRoot != null,
                generatedRoot != null ? $"Found: {generatedRoot.name}" : "Not found"));

            // 12. RuntimeBinding root exists
            Transform runtimeBindingRoot = generatedRoot != null ? generatedRoot.transform.Find(RuntimeBindingRootName) : null;
            checks.Add(new ValidationCheck("RuntimeBinding root exists", runtimeBindingRoot != null,
                runtimeBindingRoot != null ? "Found" : "Not found"));

            // 13. WorldMapRuntimeZoneTracker exists
            WorldMapRuntimeZoneTracker tracker = runtimeBindingRoot != null
                ? runtimeBindingRoot.GetComponentInChildren<WorldMapRuntimeZoneTracker>()
                : (generatedRoot != null ? generatedRoot.GetComponentInChildren<WorldMapRuntimeZoneTracker>() : null);
            checks.Add(new ValidationCheck("WorldMapRuntimeZoneTracker exists", tracker != null,
                tracker != null ? "Found" : "Not found"));

            // 14. WorldMapVisualController exists
            WorldMapVisualController visualController = runtimeBindingRoot != null
                ? runtimeBindingRoot.GetComponentInChildren<WorldMapVisualController>()
                : null;
            checks.Add(new ValidationCheck("WorldMapVisualController exists", visualController != null,
                visualController != null ? "Found" : "Not found"));

            // 15. WorldMapVisualController profileSet assigned
            if (visualController != null)
            {
                bool profileAssigned = visualController.ProfileSet != null;
                checks.Add(new ValidationCheck("VisualController profileSet assigned", profileAssigned,
                    profileAssigned ? $"OK: {visualController.ProfileSet.name}" : "MISSING"));
            }
            else
            {
                checks.Add(new ValidationCheck("VisualController profileSet assigned", true, "Skipped (controller not found)"));
            }

            // 16. WorldMapVisualController tracker assigned
            if (visualController != null)
            {
                bool trackerAssigned = visualController.ZoneTracker != null;
                checks.Add(new ValidationCheck("VisualController tracker assigned", trackerAssigned,
                    trackerAssigned ? "OK" : "MISSING"));
            }
            else
            {
                checks.Add(new ValidationCheck("VisualController tracker assigned", true, "Skipped (controller not found)"));
            }

            // 17-19. Sample visual profile evaluation
            if (profileSetExists && settings.WorldMapConfig != null && settings.ScenarioPreset != null)
            {
                ZoneResolver resolver = new ZoneResolver(settings.WorldMapConfig);
                resolver.Initialize(settings.ScenarioPreset);
                WorldMapVisualProfileResolver visualResolver = new WorldMapVisualProfileResolver();
                visualResolver.Initialize(settings.VisualProfileSet);

                // F6 y=-100
                SampleAndEvaluate(resolver, visualResolver, new Vector3(200f, -100f, 200f), "F6 y=-100", checks);
                // J10 y=-700
                SampleAndEvaluate(resolver, visualResolver, new Vector3(1800f, -700f, 1800f), "J10 y=-700", checks);
                // J10 y=-1500
                SampleAndEvaluate(resolver, visualResolver, new Vector3(1800f, -1500f, 1800f), "J10 y=-1500", checks);
            }
            else
            {
                for (int i = 17; i <= 19; i++)
                {
                    checks.Add(new ValidationCheck($"Sample check #{i}", true, "Skipped (missing config/preset/profileSet)"));
                }
            }

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
                log.AppendLine($"[MapAutoBuilder] Visual profile validation passed. {totalChecks}/{totalChecks} checks.");
            }
            else
            {
                log.AppendLine($"[MapAutoBuilder][ERROR] Visual profile validation failed. {failedCount} check(s) failed.");
            }

            Debug.Log(log.ToString());
            return failedCount == 0;
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// WorldMapVisualController를 생성하거나 업데이트한다.
        /// </summary>
        private static void CreateOrUpdateVisualController(GameObject runtimeBindingRoot, DeepLightMapAutoBuilderSettingsSO settings,
            WorldMapRuntimeZoneTracker tracker, WorldMapVisualProfileSetSO profileSet)
        {
            // 기존 VisualController 검색
            WorldMapVisualController controller = runtimeBindingRoot.GetComponentInChildren<WorldMapVisualController>();
            GameObject controllerObj;

            if (controller != null)
            {
                controllerObj = controller.gameObject;
            }
            else
            {
                // 기존 GameObject 검색
                Transform existingTransform = runtimeBindingRoot.transform.Find(VisualControllerName);
                if (existingTransform != null)
                {
                    controllerObj = existingTransform.gameObject;
                    controller = controllerObj.GetComponent<WorldMapVisualController>();
                    if (controller == null)
                    {
                        controller = controllerObj.AddComponent<WorldMapVisualController>();
                        Undo.RegisterCreatedObjectUndo(controller, "Add WorldMapVisualController component");
                    }
                }
                else
                {
                    // 새 GameObject 생성
                    controllerObj = new GameObject(VisualControllerName);
                    controllerObj.transform.SetParent(runtimeBindingRoot.transform);
                    controllerObj.transform.localPosition = Vector3.zero;
                    controllerObj.transform.localRotation = Quaternion.identity;
                    controllerObj.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(controllerObj, $"Create {VisualControllerName}");

                    controller = controllerObj.AddComponent<WorldMapVisualController>();
                    Undo.RegisterCreatedObjectUndo(controller, "Add WorldMapVisualController component");
                }
            }

            // Configure 호출
            controller.Configure(
                tracker: tracker,
                profileSet: profileSet,
                blendSpeed: settings.VisualBlendSpeed,
                logChanges: settings.LogVisualProfileChanges,
                applyToAdapters: false
            );

            // enabled 설정
            controller.enabled = settings.VisualControllerEnabledByDefault;

            Debug.Log($"[MapAutoBuilder] WorldMapVisualController configured. Tracker: {(tracker != null ? "assigned" : "null")}, ProfileSet: {(profileSet != null ? profileSet.name : "null")}");
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
        /// 특정 DepthBand가 defaultRules에서 평가 가능한지 검사한다.
        /// </summary>
        private static void CheckBandEvaluatable(WorldMapVisualProfileSetSO profileSet, ZoneDepthBand band, System.Collections.Generic.List<ValidationCheck> checks)
        {
            if (profileSet.DefaultRules == null)
            {
                checks.Add(new ValidationCheck($"{band} rule evaluatable", false, "DefaultRules is null"));
                return;
            }

            bool found = false;
            for (int i = 0; i < profileSet.DefaultRules.Count; i++)
            {
                if (profileSet.DefaultRules[i] != null && profileSet.DefaultRules[i].DepthBand == band)
                {
                    found = true;
                    break;
                }
            }

            checks.Add(new ValidationCheck($"{band} rule evaluatable", found,
                found ? $"Found in defaultRules" : $"Not found in defaultRules"));
        }

        /// <summary>
        /// 특정 BiomeType이 biomeOverrides에서 평가 가능한지 검사한다.
        /// </summary>
        private static void CheckBiomeOverrideEvaluatable(WorldMapVisualProfileSetSO profileSet, ZoneBiomeType biomeType, System.Collections.Generic.List<ValidationCheck> checks)
        {
            if (profileSet.BiomeOverrides == null)
            {
                checks.Add(new ValidationCheck($"{biomeType} biome override evaluatable", false, "BiomeOverrides is null"));
                return;
            }

            bool found = false;
            for (int i = 0; i < profileSet.BiomeOverrides.Count; i++)
            {
                if (profileSet.BiomeOverrides[i] != null && profileSet.BiomeOverrides[i].BiomeType == biomeType)
                {
                    found = true;
                    break;
                }
            }

            checks.Add(new ValidationCheck($"{biomeType} biome override evaluatable", found,
                found ? $"Found in biomeOverrides" : $"Not found in biomeOverrides"));
        }

        /// <summary>
        /// 특정 위치에서 ZoneResolver 샘플링 + VisualProfile 평가를 수행하고 결과를 로그로 출력한다.
        /// </summary>
        private static void SampleAndEvaluate(ZoneResolver resolver, WorldMapVisualProfileResolver visualResolver,
            Vector3 position, string label, System.Collections.Generic.List<ValidationCheck> checks)
        {
            if (resolver.TrySampleDepth(position, null, out ZoneDepthSample sample))
            {
                WorldMapVisualProfile profile = visualResolver.ResolveOrFallback(sample);
                checks.Add(new ValidationCheck($"Sample {label}", true,
                    $"DepthBand={sample.DepthBand}, FogDensity={profile.FogDensity:F3}, Visibility={profile.VisibilityDistance:F1}"));
            }
            else
            {
                checks.Add(new ValidationCheck($"Sample {label}", false, "Failed to sample"));
            }
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
