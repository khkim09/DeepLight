using System.Collections.Generic;
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
    /// Phase 11: Safe Runtime Rendering Finalization Utility.
    /// Phase 8~10에서 만든 Visual Runtime / Visual Adapter 구조를 실제 런타임에서
    /// 안전하게 사용할 수 있도록 최종 설정한다.
    ///
    /// 핵심 원칙:
    /// - Scene View / Game View가 회색/검정/과노출/저채도로 변하면 실패.
    /// - Validation 과정에서 VolumeProfile이나 Material 값을 테스트용 극단값으로 변경 금지.
    /// - 모든 작업은 GeneratedWorldRoot 하위에만 적용.
    /// - MapSettings / _WorldMap_Manual / 기존 Hierarchy는 절대 수정 금지.
    /// </summary>
    public static class DeepLightMapSafeRuntimeRenderingUtility
    {
        private static List<string> _passMessages = new List<string>();
        private static List<string> _failMessages = new List<string>();
        private static StringBuilder _logBuilder = new StringBuilder();

        // ======================================================================
        //  Phase 11: Enable Safe Runtime Rendering
        // ======================================================================

        /// <summary>
        /// Safe Runtime Rendering을 활성화한다.
        /// VisualController에 AdapterRoot 연결, enabled/blendSpeed 설정,
        /// RuntimeVisualVolume의 VolumeProfile을 neutral 값으로 복구한다.
        /// </summary>
        public static void EnableSafeRuntimeRendering(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[Phase11] Settings is null! Cannot enable safe runtime rendering.");
                return;
            }

            Debug.Log("[Phase11] ===== Enable Safe Runtime Rendering =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[Phase11] GeneratedWorldRoot not found. Cannot enable safe runtime rendering.");
                return;
            }

            // 2. RuntimeBinding root 찾기
            Transform runtimeBindingTransform = generatedRoot.transform.Find("RuntimeBinding");
            if (runtimeBindingTransform == null)
            {
                Debug.LogWarning("[Phase11] RuntimeBinding not found under GeneratedWorldRoot. Skipping.");
                return;
            }
            GameObject runtimeBindingRoot = runtimeBindingTransform.gameObject;

            // 3. WorldMapVisualController 찾기 (타입 기반)
            WorldMapVisualController visualController = runtimeBindingRoot.GetComponentInChildren<WorldMapVisualController>();
            if (visualController == null)
            {
                Debug.LogWarning("[Phase11] WorldMapVisualController not found under RuntimeBinding. Skipping.");
                return;
            }

            // 4. VisualAdapters root 찾기
            Transform visualAdaptersTransform = runtimeBindingRoot.transform.Find("VisualAdapters");
            WorldMapVisualAdapterRoot adapterRoot = null;
            if (visualAdaptersTransform != null)
            {
                adapterRoot = visualAdaptersTransform.GetComponent<WorldMapVisualAdapterRoot>();
            }

            // 5. VisualController에 AdapterRoot 연결 (없으면 연결)
            if (adapterRoot != null && visualController.AdapterRoot == null)
            {
                // SerializedObject를 통해 private field 설정
                SerializedObject so = new SerializedObject(visualController);
                SerializedProperty adapterRootProp = so.FindProperty("adapterRoot");
                if (adapterRootProp != null)
                {
                    adapterRootProp.objectReferenceValue = adapterRoot;
                    so.ApplyModifiedProperties();
                    Debug.Log("[Phase11] Connected AdapterRoot to VisualController.");
                }
                else
                {
                    Debug.LogWarning("[Phase11] Could not find 'adapterRoot' SerializedProperty on VisualController.");
                }
            }

            // 6. VisualController.enabled 설정
            bool enabledByDefault = settings.SafeRuntimeRenderingEnabledByDefault;
            if (visualController.enabled != enabledByDefault)
            {
                visualController.enabled = enabledByDefault;
                Debug.Log($"[Phase11] VisualController.enabled = {enabledByDefault}");
            }

            // 7. applyToAdapters 설정 (반드시 false)
            bool applyToAdapters = settings.VisualControllerApplyToAdaptersByDefault;
            {
                SerializedObject so = new SerializedObject(visualController);
                SerializedProperty applyProp = so.FindProperty("applyToAdapters");
                if (applyProp != null)
                {
                    applyProp.boolValue = applyToAdapters;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[Phase11] VisualController.applyToAdapters = {applyToAdapters} (safe default)");
                }
                else
                {
                    Debug.LogWarning("[Phase11] Could not find 'applyToAdapters' SerializedProperty on VisualController.");
                }
            }

            // 8. blendSpeed 설정
            float blendSpeed = settings.SafeRuntimeBlendSpeed;
            {
                SerializedObject so = new SerializedObject(visualController);
                SerializedProperty blendProp = so.FindProperty("blendSpeed");
                if (blendProp != null)
                {
                    blendProp.floatValue = blendSpeed;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[Phase11] VisualController.blendSpeed = {blendSpeed}");
                }
                else
                {
                    Debug.LogWarning("[Phase11] Could not find 'blendSpeed' SerializedProperty on VisualController.");
                }
            }

            // 9. RuntimeVisualVolume Neutral Profile 복구
            if (settings.RestoreNeutralRuntimeVisualProfileOnGenerate)
            {
                RestoreNeutralRuntimeVisualProfile(runtimeBindingRoot);
            }

            Debug.Log("[Phase11] ===== Enable Safe Runtime Rendering Complete =====");
        }

        /// <summary>
        /// RuntimeVisualVolume의 VolumeProfile을 neutral 값으로 복구한다.
        /// 원본 VolumeProfile asset을 직접 수정하지 않고, runtime instance를 생성하여 할당한다.
        /// </summary>
        private static void RestoreNeutralRuntimeVisualProfile(GameObject runtimeBindingRoot)
        {
            // RuntimeVisualVolume 찾기
            Transform runtimeVisualVolumeTransform = runtimeBindingRoot.transform.Find("RuntimeVisualVolume");
            if (runtimeVisualVolumeTransform == null)
            {
                Debug.LogWarning("[Phase11] RuntimeVisualVolume not found under RuntimeBinding. Skipping profile restore.");
                return;
            }

            Volume volume = runtimeVisualVolumeTransform.GetComponent<Volume>();
            if (volume == null)
            {
                Debug.LogWarning("[Phase11] RuntimeVisualVolume has no Volume component. Skipping profile restore.");
                return;
            }

            // Neutral VolumeProfile runtime instance 생성
            VolumeProfile neutralProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            neutralProfile.name = "RuntimeVisualVolume_NeutralProfile";

            // ColorAdjustments 추가 (neutral 값)
            var colorAdjustments = neutralProfile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(overrides: true);
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.Override(0f);
                colorAdjustments.postExposure.overrideState = true;

                colorAdjustments.contrast.Override(0f);
                colorAdjustments.contrast.overrideState = true;

                colorAdjustments.saturation.Override(0f);
                colorAdjustments.saturation.overrideState = true;

                colorAdjustments.colorFilter.Override(Color.white);
                colorAdjustments.colorFilter.overrideState = true;

                colorAdjustments.hueShift.Override(0f);
                colorAdjustments.hueShift.overrideState = true;

                Debug.Log("[Phase11] ColorAdjustments set to neutral values.");
            }

            // Vignette 추가 (neutral 값)
            var vignette = neutralProfile.Add<UnityEngine.Rendering.Universal.Vignette>(overrides: true);
            if (vignette != null)
            {
                vignette.intensity.Override(0f);
                vignette.intensity.overrideState = true;

                vignette.smoothness.Override(0.2f);
                vignette.smoothness.overrideState = true;

                vignette.color.Override(Color.black);
                vignette.color.overrideState = true;

                vignette.center.Override(new Vector2(0.5f, 0.5f));
                vignette.center.overrideState = true;

                Debug.Log("[Phase11] Vignette set to neutral values.");
            }

            // Volume에 neutral profile 할당 (원본 asset 수정 금지)
            volume.profile = neutralProfile;

            // isGlobal 유지
            if (!volume.isGlobal)
            {
                volume.isGlobal = true;
            }

            Debug.Log($"[Phase11] RuntimeVisualVolume profile restored to neutral runtime instance: {neutralProfile.name}");
        }

        // ======================================================================
        //  Phase 11: Validate Runtime Rendering (Non-destructive)
        // ======================================================================

        /// <summary>
        /// Runtime Rendering 설정의 유효성을 검증한다.
        /// Non-destructive 방식으로 동작하며, 실제 렌더링 상태를 변경하지 않는다.
        /// </summary>
        public static void ValidateRuntimeRendering(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[Phase11] Settings is null! Cannot validate.");
                return;
            }

            // 결과 리스트 초기화
            _passMessages.Clear();
            _failMessages.Clear();
            _logBuilder.Clear();

            _logBuilder.AppendLine("===== Map Auto Builder: Validate Runtime Rendering (Phase 11) =====");

            // ===== 1. GeneratedWorldRoot exists =====
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            bool rootExists = generatedRoot != null;
            LogResult("GeneratedWorldRoot exists", rootExists);

            if (!rootExists)
            {
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
            LogResult("WorldMapVisualController exists under GeneratedWorldRoot", visualControllerExists);

            // ===== 4. WorldMapVisualController is under GeneratedWorldRoot =====
            bool visualControllerUnderGenerated = false;
            if (visualControllerExists)
            {
                visualControllerUnderGenerated = IsUnderTransform(visualController.transform, generatedRoot.transform);
            }
            LogResult("WorldMapVisualController is under GeneratedWorldRoot", visualControllerUnderGenerated);

            // ===== 5. WorldMapVisualController is NOT under MapSettings =====
            bool visualControllerNotUnderMapSettings = true;
            if (visualControllerExists)
            {
                GameObject mapSettings = GameObject.Find("MapSettings");
                if (mapSettings != null)
                {
                    visualControllerNotUnderMapSettings = !IsUnderTransform(visualController.transform, mapSettings.transform);
                }
            }
            LogResult("WorldMapVisualController is NOT under MapSettings", visualControllerNotUnderMapSettings);

            // ===== 6. VisualProfileSet assigned =====
            bool profileSetAssigned = visualControllerExists && visualController.ProfileSet != null;
            LogResult("VisualProfileSet assigned", profileSetAssigned);

            // ===== 7. ZoneTracker assigned =====
            bool zoneTrackerAssigned = visualControllerExists && visualController.ZoneTracker != null;
            LogResult("ZoneTracker assigned", zoneTrackerAssigned);

            // ===== 8. AdapterRoot assigned =====
            bool adapterRootAssigned = visualControllerExists && visualController.AdapterRoot != null;
            LogResult("AdapterRoot assigned", adapterRootAssigned);

            // ===== 9. AdapterRoot exists under GeneratedWorldRoot =====
            bool adapterRootUnderGenerated = false;
            if (visualControllerExists && visualController.AdapterRoot != null)
            {
                adapterRootUnderGenerated = IsUnderTransform(visualController.AdapterRoot.transform, generatedRoot.transform);
            }
            LogResult("AdapterRoot exists under GeneratedWorldRoot", adapterRootUnderGenerated);

            // ===== 10. AdapterRoot is NOT under MapSettings =====
            bool adapterRootNotUnderMapSettings = true;
            if (visualControllerExists && visualController.AdapterRoot != null)
            {
                GameObject mapSettings = GameObject.Find("MapSettings");
                if (mapSettings != null)
                {
                    adapterRootNotUnderMapSettings = !IsUnderTransform(visualController.AdapterRoot.transform, mapSettings.transform);
                }
            }
            LogResult("AdapterRoot is NOT under MapSettings", adapterRootNotUnderMapSettings);

            // ===== 11. VisualController.ApplyToAdapters matches settings =====
            bool applyToAdaptersMatches = visualControllerExists &&
                visualController.ApplyToAdapters == settings.VisualControllerApplyToAdaptersByDefault;
            bool actualApplyToAdapters = visualControllerExists ? visualController.ApplyToAdapters : false;
            LogResult($"VisualController.ApplyToAdapters matches settings (expected: {settings.VisualControllerApplyToAdaptersByDefault}, actual: {actualApplyToAdapters})",
                applyToAdaptersMatches);

            // ===== 12. VisualController.BlendSpeed matches settings =====
            bool blendSpeedMatches = visualControllerExists &&
                Mathf.Approximately(visualController.BlendSpeed, settings.SafeRuntimeBlendSpeed);
            float actualBlendSpeed = visualControllerExists ? visualController.BlendSpeed : 0f;
            LogResult($"VisualController.BlendSpeed matches settings (expected: {settings.SafeRuntimeBlendSpeed}, actual: {actualBlendSpeed})",
                blendSpeedMatches);

            // ===== 13. RuntimeVisualVolume exists =====
            Transform runtimeVisualVolumeTransform = runtimeBindingRoot.transform.Find("RuntimeVisualVolume");
            bool runtimeVisualVolumeExists = runtimeVisualVolumeTransform != null;
            LogResult("RuntimeVisualVolume exists", runtimeVisualVolumeExists);

            // ===== 14. RuntimeVisualVolume is under GeneratedWorldRoot =====
            bool runtimeVisualVolumeUnderGenerated = false;
            if (runtimeVisualVolumeExists)
            {
                runtimeVisualVolumeUnderGenerated = IsUnderTransform(runtimeVisualVolumeTransform, generatedRoot.transform);
            }
            LogResult("RuntimeVisualVolume is under GeneratedWorldRoot", runtimeVisualVolumeUnderGenerated);

            // ===== 15. RuntimeVisualVolume is NOT under MapSettings =====
            bool runtimeVisualVolumeNotUnderMapSettings = true;
            if (runtimeVisualVolumeExists)
            {
                GameObject mapSettings = GameObject.Find("MapSettings");
                if (mapSettings != null)
                {
                    runtimeVisualVolumeNotUnderMapSettings = !IsUnderTransform(runtimeVisualVolumeTransform, mapSettings.transform);
                }
            }
            LogResult("RuntimeVisualVolume is NOT under MapSettings", runtimeVisualVolumeNotUnderMapSettings);

            // ===== 16. RuntimeVisualVolume has Volume component =====
            Volume volume = null;
            if (runtimeVisualVolumeExists)
            {
                volume = runtimeVisualVolumeTransform.GetComponent<Volume>();
            }
            bool hasVolumeComponent = volume != null;
            LogResult("RuntimeVisualVolume has Volume component", hasVolumeComponent);

            // ===== 17. RuntimeVisualVolume.profile exists =====
            bool profileExists = hasVolumeComponent && volume.profile != null;
            LogResult("RuntimeVisualVolume.profile exists", profileExists);

            // ===== 18. RuntimeVisualVolume.profile is runtime instance or safely assigned =====
            bool profileIsRuntimeInstance = false;
            if (profileExists)
            {
                // Runtime instance는 AssetDatabase에 없는 profile
                string assetPath = AssetDatabase.GetAssetPath(volume.profile);
                profileIsRuntimeInstance = string.IsNullOrEmpty(assetPath);
            }
            LogResult("RuntimeVisualVolume.profile is runtime instance (not asset)", profileIsRuntimeInstance);

            // ===== 19. ColorAdjustments override exists or can be neutral =====
            bool colorAdjustmentsExists = false;
            if (profileExists)
            {
                colorAdjustmentsExists = volume.profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out _);
            }
            LogResult("ColorAdjustments override exists or can be neutral", colorAdjustmentsExists);

            // ===== 20. Vignette override exists or can be neutral =====
            bool vignetteExists = false;
            if (profileExists)
            {
                vignetteExists = volume.profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out _);
            }
            LogResult("Vignette override exists or can be neutral", vignetteExists);

            // ===== 21. WaterMaterialAdapter targetRenderer is under GeneratedWorldRoot =====
            bool waterTargetUnderGenerated = false;
            Transform visualAdaptersTransform = runtimeBindingRoot.transform.Find("VisualAdapters");
            if (visualAdaptersTransform != null)
            {
                WorldMapWaterMaterialAdapter waterAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapWaterMaterialAdapter>();
                if (waterAdapter != null && waterAdapter.TargetRenderer != null)
                {
                    waterTargetUnderGenerated = IsUnderTransform(waterAdapter.TargetRenderer.transform, generatedRoot.transform);
                }
            }
            LogResult("WaterMaterialAdapter targetRenderer is under GeneratedWorldRoot", waterTargetUnderGenerated);

            // ===== 22. WaterMaterialAdapter targetRenderer is NOT under MapSettings =====
            bool waterTargetNotUnderMapSettings = true;
            if (visualAdaptersTransform != null)
            {
                WorldMapWaterMaterialAdapter waterAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapWaterMaterialAdapter>();
                if (waterAdapter != null && waterAdapter.TargetRenderer != null)
                {
                    GameObject mapSettings = GameObject.Find("MapSettings");
                    if (mapSettings != null)
                    {
                        waterTargetNotUnderMapSettings = !IsUnderTransform(waterAdapter.TargetRenderer.transform, mapSettings.transform);
                    }
                }
            }
            LogResult("WaterMaterialAdapter targetRenderer is NOT under MapSettings", waterTargetNotUnderMapSettings);

            // ===== 23. RuntimeVolumeAdapter targetVolume is under GeneratedWorldRoot =====
            bool volumeTargetUnderGenerated = false;
            if (visualAdaptersTransform != null)
            {
                WorldMapRuntimeVolumeAdapter volumeAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapRuntimeVolumeAdapter>();
                if (volumeAdapter != null && volumeAdapter.TargetVolume != null)
                {
                    volumeTargetUnderGenerated = IsUnderTransform(volumeAdapter.TargetVolume.transform, generatedRoot.transform);
                }
            }
            LogResult("RuntimeVolumeAdapter targetVolume is under GeneratedWorldRoot", volumeTargetUnderGenerated);

            // ===== 24. RuntimeVolumeAdapter targetVolume is NOT under MapSettings =====
            bool volumeTargetNotUnderMapSettings = true;
            if (visualAdaptersTransform != null)
            {
                WorldMapRuntimeVolumeAdapter volumeAdapter = visualAdaptersTransform.GetComponentInChildren<WorldMapRuntimeVolumeAdapter>();
                if (volumeAdapter != null && volumeAdapter.TargetVolume != null)
                {
                    GameObject mapSettings = GameObject.Find("MapSettings");
                    if (mapSettings != null)
                    {
                        volumeTargetNotUnderMapSettings = !IsUnderTransform(volumeAdapter.TargetVolume.transform, mapSettings.transform);
                    }
                }
            }
            LogResult("RuntimeVolumeAdapter targetVolume is NOT under MapSettings", volumeTargetNotUnderMapSettings);

            // ===== 25. MapSettings exists and is preserved =====
            GameObject mapSettingsObj = GameObject.Find("MapSettings");
            bool mapSettingsPreserved = mapSettingsObj != null;
            LogResult("MapSettings exists and is preserved", mapSettingsPreserved);

            // ===== 26. _WorldMap_Manual exists and is preserved =====
            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            bool worldMapManualPreserved = worldMapManual != null;
            LogResult("_WorldMap_Manual exists and is preserved", worldMapManualPreserved);

            // ===== 27. DeepLightMapAutoBuilderContext exists and is preserved =====
            GameObject builderContext = GameObject.Find("DeepLightMapAutoBuilderContext");
            bool builderContextPreserved = builderContext != null;
            LogResult("DeepLightMapAutoBuilderContext exists and is preserved", builderContextPreserved);

            // ===== Finalize =====
            FinalizeLog();
        }

        // ======================================================================
        //  Internal Helpers
        // ======================================================================

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
            _logBuilder.AppendLine("===== Validate Runtime Rendering Complete =====");

            // 전체 로그 출력
            Debug.LogWarning(_logBuilder.ToString());

            // Fail 항목이 있으면 별도 경고
            if (failCount > 0)
            {
                StringBuilder failSummary = new StringBuilder();
                failSummary.AppendLine($"[Phase11] {failCount} validation(s) FAILED:");
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
