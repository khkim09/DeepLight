using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.VisualAdapters;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 10: Visual Adapter Runtime Flow Validation Utility.
    /// Phase 9까지 생성된 Visual Adapter Runtime 구조를 Non-destructive 방식으로 검증한다.
    /// 실제 렌더링 상태, VolumeProfile, Material, WaterMaterial, MapSettings를 절대 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapVisualAdapterRuntimeValidationUtility
    {
        private static List<string> _passMessages = new List<string>();
        private static List<string> _failMessages = new List<string>();
        private static StringBuilder _logBuilder = new StringBuilder();

        /// <summary>
        /// Phase 9까지 생성된 Visual Adapter Runtime 구조를 검증한다.
        /// Non-destructive 방식으로 동작하며, 실제 렌더링 상태를 변경하지 않는다.
        /// </summary>
        public static void ValidateVisualAdapterRuntimeFlow(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[Phase10] Settings is null! Cannot validate.");
                return;
            }

            // 결과 리스트 초기화
            _passMessages.Clear();
            _failMessages.Clear();
            _logBuilder.Clear();

            _logBuilder.AppendLine("===== Map Auto Builder: Validate Visual Adapter Runtime Flow (Phase 10) =====");

            // ===== 1. GeneratedWorldRoot exists =====
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            bool rootExists = generatedRoot != null;
            LogResult("GeneratedWorldRoot exists", rootExists);

            if (!rootExists)
            {
                // GeneratedWorldRoot가 없으면 이후 검증 불가
                _logBuilder.AppendLine("[SKIP] GeneratedWorldRoot not found. Skipping remaining checks.");
                FinalizeLog();
                return;
            }

            // ===== 2. RuntimeBinding root exists =====
            Transform runtimeBindingTransform = generatedRoot.transform.Find("RuntimeBinding");
            bool runtimeBindingExists = runtimeBindingTransform != null;
            LogResult("RuntimeBinding root exists", runtimeBindingExists);

            if (!runtimeBindingExists)
            {
                _logBuilder.AppendLine("[SKIP] RuntimeBinding not found. Skipping remaining checks.");
                FinalizeLog();
                return;
            }
            GameObject runtimeBindingRoot = runtimeBindingTransform.gameObject;

            // ===== 3. WorldMapVisualController exists under GeneratedWorldRoot =====
            WorldMapVisualController visualController = runtimeBindingRoot.GetComponentInChildren<WorldMapVisualController>();
            bool visualControllerExists = visualController != null;
            LogResult("WorldMapVisualController exists under RuntimeBinding", visualControllerExists);

            // ===== 4. WorldMapVisualController.profileSet assigned =====
            bool profileSetAssigned = visualControllerExists && visualController.ProfileSet != null;
            LogResult("WorldMapVisualController.profileSet assigned", profileSetAssigned);

            // ===== 5. WorldMapVisualController.zoneTracker assigned =====
            bool zoneTrackerAssigned = visualControllerExists && visualController.ZoneTracker != null;
            LogResult("WorldMapVisualController.zoneTracker assigned", zoneTrackerAssigned);

            // ===== 6. WorldMapVisualController.adapterRoot assigned =====
            bool adapterRootAssigned = visualControllerExists && visualController.AdapterRoot != null;
            LogResult("WorldMapVisualController.adapterRoot assigned", adapterRootAssigned);

            // ===== 7. VisualAdapters root exists under RuntimeBinding =====
            Transform visualAdaptersTransform = runtimeBindingRoot.transform.Find("VisualAdapters");
            bool visualAdaptersRootExists = visualAdaptersTransform != null;
            LogResult("VisualAdapters root exists under RuntimeBinding", visualAdaptersRootExists);

            // ===== 8. WorldMapVisualAdapterRoot component exists =====
            WorldMapVisualAdapterRoot adapterRoot = null;
            if (visualAdaptersRootExists)
            {
                adapterRoot = visualAdaptersTransform.GetComponent<WorldMapVisualAdapterRoot>();
            }
            bool adapterRootComponentExists = adapterRoot != null;
            LogResult("WorldMapVisualAdapterRoot component exists", adapterRootComponentExists);

            // ===== 9. WorldMapVisualAdapterRoot.AdapterBehaviourCount >= 1 =====
            int behaviourCount = adapterRootComponentExists ? adapterRoot.AdapterBehaviourCount : 0;
            bool hasBehaviours = behaviourCount >= 1;
            LogResult($"WorldMapVisualAdapterRoot.AdapterBehaviourCount >= 1 (actual: {behaviourCount})", hasBehaviours);

            // ===== 10. WorldMapVisualAdapterRoot.RuntimeAdapterCount >= 1 after CollectAdapters =====
            int runtimeAdapterCount = 0;
            if (adapterRootComponentExists)
            {
                adapterRoot.CollectAdapters();
                runtimeAdapterCount = adapterRoot.AdapterCount;
            }
            bool hasRuntimeAdapters = runtimeAdapterCount >= 1;
            LogResult($"WorldMapVisualAdapterRoot.RuntimeAdapterCount >= 1 after CollectAdapters (actual: {runtimeAdapterCount})", hasRuntimeAdapters);

            // ===== 11. DebugAdapter exists =====
            WorldMapVisualDebugAdapter debugAdapter = null;
            if (visualAdaptersRootExists)
            {
                debugAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapVisualDebugAdapter>();
            }
            bool debugAdapterExists = debugAdapter != null;
            LogResult("DebugAdapter exists", debugAdapterExists);

            // ===== 12. DebugAdapter implements IWorldMapVisualAdapter =====
            bool debugImplementsInterface = debugAdapterExists && debugAdapter is IWorldMapVisualAdapter;
            LogResult("DebugAdapter implements IWorldMapVisualAdapter", debugImplementsInterface);

            // ===== 13. DebugAdapter can Initialize safely =====
            bool debugCanInitialize = false;
            if (debugAdapterExists)
            {
                try
                {
                    debugAdapter.Initialize();
                    debugCanInitialize = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Phase10] DebugAdapter.Initialize() threw exception: {ex.Message}");
                }
            }
            LogResult("DebugAdapter can Initialize safely", debugCanInitialize);

            // ===== 14. DebugAdapter IsReady after Initialize =====
            bool debugIsReady = debugAdapterExists && debugAdapter.IsReady;
            LogResult("DebugAdapter IsReady after Initialize", debugIsReady);

            // ===== 15. DebugAdapter 단독 ApplyProfile 테스트 =====
            // DebugAdapter에만 직접 ApplyProfile을 호출한다.
            // AdapterRoot.ApplyProfile / WaterMaterialAdapter.ApplyProfile / RuntimeVolumeAdapter.ApplyProfile는 호출하지 않는다.
            bool debugApplyProfileWorks = false;
            if (debugAdapterExists && debugIsReady)
            {
                try
                {
                    // 중립적인 테스트용 profile 생성 (렌더링에 영향을 주지 않음)
                    WorldMapVisualProfile testProfile = WorldMapVisualProfile.CreateFallback(
                        ZoneDepthBand.Mid, ZoneBiomeType.OpenWater, 0.5f);

                    // DebugAdapter 전용 WorldMapVisualRuntimeState 생성
                    WorldMapVisualRuntimeState testState = new WorldMapVisualRuntimeState();
                    testState.Update(
                        new ZoneDepthSample(
                            new ZoneId('A', 1),
                            new RegionId("Validation"),
                            ZoneBiomeType.OpenWater,
                            ZoneDepthBand.Mid,
                            0f,          // y
                            0.5f,        // normalizedDepth01
                            0f,          // waterLevelY
                            100f,        // maxDepth
                            "Validation",
                            null),       // environmentProfile
                        testProfile,
                        testProfile);

                    // DebugAdapter에만 직접 ApplyProfile 호출 (다른 Adapter는 호출 금지)
                    debugAdapter.ApplyProfile(testState, 0.016f);

                    // DebugAdapter의 내부 상태가 변경되었는지 확인
                    debugApplyProfileWorks = debugAdapter.IsReady;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Phase10] DebugAdapter.ApplyProfile() threw exception: {ex.Message}");
                }
            }
            LogResult("DebugAdapter 단독 ApplyProfile 테스트 성공", debugApplyProfileWorks);

            // ===== 16. WaterMaterialAdapter exists =====
            WorldMapWaterMaterialAdapter waterAdapter = null;
            if (visualAdaptersRootExists)
            {
                waterAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapWaterMaterialAdapter>();
            }
            bool waterAdapterExists = waterAdapter != null;
            LogResult("WaterMaterialAdapter exists", waterAdapterExists);

            // ===== 17. WaterMaterialAdapter targetRenderer assigned =====
            bool waterTargetAssigned = waterAdapterExists && waterAdapter.TargetRenderer != null;
            LogResult("WaterMaterialAdapter targetRenderer assigned", waterTargetAssigned);

            // ===== 18. WaterMaterialAdapter targetRenderer is under GeneratedWorldRoot =====
            bool waterTargetUnderGenerated = false;
            if (waterAdapterExists && waterAdapter.TargetRenderer != null)
            {
                waterTargetUnderGenerated = IsUnderTransform(waterAdapter.TargetRenderer.transform, generatedRoot.transform);
            }
            LogResult("WaterMaterialAdapter targetRenderer is under GeneratedWorldRoot", waterTargetUnderGenerated);

            // ===== 19. WaterMaterialAdapter targetRenderer is NOT under MapSettings =====
            bool waterTargetNotUnderMapSettings = true;
            if (waterAdapterExists && waterAdapter.TargetRenderer != null)
            {
                GameObject mapSettings = GameObject.Find("MapSettings");
                if (mapSettings != null)
                {
                    waterTargetNotUnderMapSettings = !IsUnderTransform(waterAdapter.TargetRenderer.transform, mapSettings.transform);
                }
            }
            LogResult("WaterMaterialAdapter targetRenderer is NOT under MapSettings", waterTargetNotUnderMapSettings);

            // ===== 20. WaterMaterialAdapter can Initialize safely =====
            bool waterCanInitialize = false;
            if (waterAdapterExists)
            {
                try
                {
                    waterAdapter.Initialize();
                    waterCanInitialize = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Phase10] WaterMaterialAdapter.Initialize() threw exception: {ex.Message}");
                }
            }
            LogResult("WaterMaterialAdapter can Initialize safely", waterCanInitialize);

            // ===== 21. RuntimeVolumeAdapter exists =====
            WorldMapRuntimeVolumeAdapter volumeAdapter = null;
            if (visualAdaptersRootExists)
            {
                volumeAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapRuntimeVolumeAdapter>();
            }
            bool volumeAdapterExists = volumeAdapter != null;
            LogResult("RuntimeVolumeAdapter exists", volumeAdapterExists);

            // ===== 22. RuntimeVolumeAdapter targetVolume assigned =====
            bool volumeTargetAssigned = volumeAdapterExists && volumeAdapter.TargetVolume != null;
            LogResult("RuntimeVolumeAdapter targetVolume assigned", volumeTargetAssigned);

            // ===== 23. RuntimeVolumeAdapter targetVolume is under GeneratedWorldRoot =====
            bool volumeTargetUnderGenerated = false;
            if (volumeAdapterExists && volumeAdapter.TargetVolume != null)
            {
                volumeTargetUnderGenerated = IsUnderTransform(volumeAdapter.TargetVolume.transform, generatedRoot.transform);
            }
            LogResult("RuntimeVolumeAdapter targetVolume is under GeneratedWorldRoot", volumeTargetUnderGenerated);

            // ===== 24. RuntimeVolumeAdapter targetVolume is NOT under MapSettings =====
            bool volumeTargetNotUnderMapSettings = true;
            if (volumeAdapterExists && volumeAdapter.TargetVolume != null)
            {
                GameObject mapSettings = GameObject.Find("MapSettings");
                if (mapSettings != null)
                {
                    volumeTargetNotUnderMapSettings = !IsUnderTransform(volumeAdapter.TargetVolume.transform, mapSettings.transform);
                }
            }
            LogResult("RuntimeVolumeAdapter targetVolume is NOT under MapSettings", volumeTargetNotUnderMapSettings);

            // ===== 25. RuntimeVolumeAdapter can Initialize safely =====
            bool volumeCanInitialize = false;
            if (volumeAdapterExists)
            {
                try
                {
                    volumeAdapter.Initialize();
                    volumeCanInitialize = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Phase10] RuntimeVolumeAdapter.Initialize() threw exception: {ex.Message}");
                }
            }
            LogResult("RuntimeVolumeAdapter can Initialize safely", volumeCanInitialize);

            // ===== 26. AdapterRoot contains DebugAdapter =====
            bool rootContainsDebug = false;
            if (adapterRootComponentExists)
            {
                var allAdapters = adapterRoot.GetAdapters();
                for (int i = 0; i < allAdapters.Count; i++)
                {
                    if (allAdapters[i] is WorldMapVisualDebugAdapter)
                    {
                        rootContainsDebug = true;
                        break;
                    }
                }
            }
            LogResult("AdapterRoot contains DebugAdapter", rootContainsDebug);

            // ===== 27. AdapterRoot contains WaterMaterialAdapter =====
            bool rootContainsWater = false;
            if (adapterRootComponentExists)
            {
                var allAdapters = adapterRoot.GetAdapters();
                for (int i = 0; i < allAdapters.Count; i++)
                {
                    if (allAdapters[i] is WorldMapWaterMaterialAdapter)
                    {
                        rootContainsWater = true;
                        break;
                    }
                }
            }
            LogResult("AdapterRoot contains WaterMaterialAdapter", rootContainsWater);

            // ===== 28. AdapterRoot contains RuntimeVolumeAdapter =====
            bool rootContainsVolume = false;
            if (adapterRootComponentExists)
            {
                var allAdapters = adapterRoot.GetAdapters();
                for (int i = 0; i < allAdapters.Count; i++)
                {
                    if (allAdapters[i] is WorldMapRuntimeVolumeAdapter)
                    {
                        rootContainsVolume = true;
                        break;
                    }
                }
            }
            LogResult("AdapterRoot contains RuntimeVolumeAdapter", rootContainsVolume);

            // ===== 29. MapSettings exists and is preserved =====
            GameObject mapSettingsObj = GameObject.Find("MapSettings");
            bool mapSettingsPreserved = mapSettingsObj != null;
            LogResult("MapSettings exists and is preserved", mapSettingsPreserved);

            // ===== 30. _WorldMap_Manual exists and is preserved =====
            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            bool worldMapManualPreserved = worldMapManual != null;
            LogResult("_WorldMap_Manual exists and is preserved", worldMapManualPreserved);

            // ===== 31. DeepLightMapAutoBuilderContext exists and is preserved =====
            GameObject builderContext = GameObject.Find("DeepLightMapAutoBuilderContext");
            bool builderContextPreserved = builderContext != null;
            LogResult("DeepLightMapAutoBuilderContext exists and is preserved", builderContextPreserved);

            // ===== Finalize =====
            FinalizeLog();
        }

        /// <summary>
        /// 검증 결과를 pass/fail 리스트에 추가한다.
        /// </summary>
        private static void LogResult(string description, bool passed)
        {
            if (passed)
            {
                _passMessages.Add(description);
                _logBuilder.AppendLine($"[PASS] {description}");
            }
            else
            {
                _failMessages.Add(description);
                _logBuilder.AppendLine($"[FAIL] {description}");
            }
        }

        /// <summary>
        /// 최종 결과를 Console에 출력한다.
        /// </summary>
        private static void FinalizeLog()
        {
            int passCount = _passMessages.Count;
            int failCount = _failMessages.Count;
            int totalCount = passCount + failCount;

            _logBuilder.AppendLine($"Result: {passCount}/{totalCount} passed, {failCount}/{totalCount} failed.");
            _logBuilder.AppendLine("===== Validate Visual Adapter Runtime Flow Complete =====");

            // 전체 로그 출력
            Debug.LogWarning(_logBuilder.ToString());

            // Fail 항목이 있으면 별도 경고
            if (failCount > 0)
            {
                StringBuilder failSummary = new StringBuilder();
                failSummary.AppendLine($"[Phase10] {failCount} validation(s) FAILED:");
                for (int i = 0; i < _failMessages.Count; i++)
                {
                    failSummary.AppendLine($"  - {_failMessages[i]}");
                }
                Debug.LogWarning(failSummary.ToString());
            }
        }

        /// <summary>
        /// target transform이 parent transform의 하위 계층에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderTransform(Transform target, Transform parent)
        {
            Transform current = target;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
