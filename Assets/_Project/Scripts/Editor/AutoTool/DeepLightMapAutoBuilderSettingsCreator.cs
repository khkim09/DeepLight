using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// DL_MapAutoBuilderSettingsSO 에셋 생성/보정 + SceneContext 생성/보정을 담당하는 Editor Utility.
    /// SettingsSO는 Project Asset 참조(worldMapConfig, scenarioPreset)만 보관한다.
    /// Scene GameObject 참조는 DeepLightMapAutoBuilderSceneContext에 저장한다.
    /// </summary>
    public static class DeepLightMapAutoBuilderSettingsCreator
    {
        private const string SETTINGS_FOLDER = "Assets/_Project/ScriptableObjects/World/Config";
        private const string SETTINGS_PATH = SETTINGS_FOLDER + "/DL_MapAutoBuilderSettings.asset";
        private const string SCENE_CONTEXT_NAME = "DeepLightMapAutoBuilderContext";

        // ======================================================================
        //  SettingsSO Asset
        // ======================================================================

        /// <summary>
        /// 메뉴: DL_MapAutoBuilderSettingsSO 에셋을 생성하거나 찾아서 자동 연결한다.
        /// </summary>
        [MenuItem("Tools/DeepLight/World Map/Create Or Auto Fill Map Builder Settings")]
        private static void CreateOrAutoFillSettingsAssetMenu()
        {
            DeepLightMapAutoBuilderSettingsSO settings = CreateOrAutoFillSettingsAsset();
            if (settings != null)
            {
                // 생성/보정된 SettingsSO를 Selection으로 설정
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        /// <summary>
        /// DL_MapAutoBuilderSettingsSO 에셋을 생성하거나 찾아서 자동 연결한다.
        /// Scene GameObject는 절대 SettingsSO에 저장하지 않는다.
        /// 반환값: 생성/보정된 SettingsSO (실패 시 null)
        /// </summary>
        public static DeepLightMapAutoBuilderSettingsSO CreateOrAutoFillSettingsAsset()
        {
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Create Or Auto Fill Settings Asset =====");

            // 1. 폴더 보장
            if (!AssetDatabase.IsValidFolder(SETTINGS_FOLDER))
            {
                EnsureFolderPath(SETTINGS_FOLDER);
                log.AppendLine($"[OK] Created folder: {SETTINGS_FOLDER}");
            }

            // 2. 기존 에셋 찾기 또는 생성
            DeepLightMapAutoBuilderSettingsSO settings = AssetDatabase.LoadAssetAtPath<DeepLightMapAutoBuilderSettingsSO>(SETTINGS_PATH);

            if (settings != null)
            {
                log.AppendLine($"[OK] Found existing SettingsSO: {SETTINGS_PATH}");
            }
            else
            {
                // 새로 생성
                settings = ScriptableObject.CreateInstance<DeepLightMapAutoBuilderSettingsSO>();
                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                log.AppendLine($"[OK] Created new SettingsSO: {SETTINGS_PATH}");
            }

            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Failed to create or load SettingsSO!");
                return null;
            }

            // 3. SerializedObject로 필드 자동 연결 (Project Asset만)
            SerializedObject serialized = new SerializedObject(settings);
            bool anyFieldSet = false;

            // 3a. worldMapConfig
            anyFieldSet |= AutoFillWorldMapConfig(settings, serialized, log);

            // 3b. scenarioPreset
            anyFieldSet |= AutoFillScenarioPreset(settings, serialized, log);

            // 4. 변경 사항 저장
            if (anyFieldSet)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                log.AppendLine("[OK] SettingsSO updated and saved.");
            }
            else
            {
                log.AppendLine("[INFO] No fields were changed. SettingsSO is already up-to-date.");
            }

            log.AppendLine("===== Auto Fill Complete =====");
            Debug.Log(log.ToString());

            return settings;
        }

        // ======================================================================
        //  Scene Context
        // ======================================================================

        /// <summary>
        /// 현재 Scene에서 DeepLightMapAutoBuilderContext를 찾거나 생성한다.
        /// 기존 Hierarchy는 절대 수정하지 않는다.
        /// </summary>
        public static DeepLightMapAutoBuilderSceneContext CreateOrFindSceneContext()
        {
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Create/Find Scene Context =====");

            // 1. Scene에서 이름으로 검색
            GameObject existing = GameObject.Find(SCENE_CONTEXT_NAME);
            DeepLightMapAutoBuilderSceneContext context = null;

            if (existing != null)
            {
                log.AppendLine($"[OK] Found existing SceneContext GameObject: {SCENE_CONTEXT_NAME}");

                // 컴포넌트 확인
                context = existing.GetComponent<DeepLightMapAutoBuilderSceneContext>();
                if (context == null)
                {
                    // 컴포넌트가 없으면 추가
                    context = existing.AddComponent<DeepLightMapAutoBuilderSceneContext>();
                    log.AppendLine($"[OK] Added DeepLightMapAutoBuilderSceneContext component to existing GameObject.");
                }
                else
                {
                    log.AppendLine($"[OK] DeepLightMapAutoBuilderSceneContext component already exists.");
                }
            }
            else
            {
                // 새 GameObject 생성
                GameObject newObj = new GameObject(SCENE_CONTEXT_NAME);
                context = newObj.AddComponent<DeepLightMapAutoBuilderSceneContext>();
                Undo.RegisterCreatedObjectUndo(newObj, $"Create {SCENE_CONTEXT_NAME}");
                log.AppendLine($"[OK] Created new SceneContext GameObject: {SCENE_CONTEXT_NAME}");
            }

            log.AppendLine("===== Scene Context Complete =====");
            Debug.Log(log.ToString());

            return context;
        }

        /// <summary>
        /// SceneContext의 Scene Object 참조를 현재 Scene에서 자동으로 찾아 연결한다.
        /// MapSettings와 그 하위 오브젝트는 절대 수정하지 않는다.
        /// </summary>
        public static void AutoFillSceneContextFromCurrentScene(DeepLightMapAutoBuilderSceneContext context)
        {
            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] SceneContext is null! Cannot auto-fill.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Auto Fill Scene Context =====");

            bool anyFieldSet = false;

            // 1. MapSettings root
            if (context.MapSettingsTemplateRoot == null)
            {
                GameObject mapSettings = GameObject.Find("MapSettings");
                if (mapSettings != null)
                {
                    context.SetMapSettingsTemplateRoot(mapSettings);
                    log.AppendLine($"[OK] Auto-filled MapSettingsTemplateRoot: {mapSettings.name}");
                    anyFieldSet = true;
                }
                else
                {
                    log.AppendLine("[WARNING] MapSettings not found in Scene!");
                }
            }
            else
            {
                log.AppendLine($"[OK] MapSettingsTemplateRoot already set: {context.MapSettingsTemplateRoot.name}");
            }

            // 2. MapSettings 하위 템플릿 (MapSettings가 있을 때만)
            if (context.MapSettingsTemplateRoot != null)
            {
                // GlobalWater
                if (context.GlobalWaterTemplate == null)
                {
                    GameObject found = FindChildInMapSettings(context.MapSettingsTemplateRoot, new[] {
                        "StylizedWater3_OceanGrid",
                        "OceanGrid",
                        "Ocean Grid",
                        "Ocean",
                        "WaterGrid",
                        "Water"
                    });
                    if (found != null)
                    {
                        context.SetGlobalWaterTemplate(found);
                        log.AppendLine($"[OK] Auto-filled GlobalWaterTemplate: {found.name}");
                        anyFieldSet = true;
                    }
                    else
                    {
                        log.AppendLine("[WARNING] GlobalWaterTemplate not found under MapSettings.");
                    }
                }
                else
                {
                    log.AppendLine($"[OK] GlobalWaterTemplate already set: {context.GlobalWaterTemplate.name}");
                }

                // UnderwaterArea
                if (context.UnderwaterAreaTemplate == null)
                {
                    GameObject found = FindChildInMapSettings(context.MapSettingsTemplateRoot, new[] {
                        "Ocean Volume",
                        "Random Underwater Sphere",
                        "Underwater",
                        "Volume"
                    });
                    if (found != null)
                    {
                        context.SetUnderwaterAreaTemplate(found);
                        log.AppendLine($"[OK] Auto-filled UnderwaterAreaTemplate: {found.name}");
                        anyFieldSet = true;
                    }
                    else
                    {
                        log.AppendLine("[INFO] UnderwaterAreaTemplate not found. Will be used in Phase 4+.");
                    }
                }
                else
                {
                    log.AppendLine($"[OK] UnderwaterAreaTemplate already set: {context.UnderwaterAreaTemplate.name}");
                }

                // BubbleParticle
                if (context.BubbleParticleTemplate == null)
                {
                    GameObject found = FindChildInMapSettings(context.MapSettingsTemplateRoot, new[] {
                        "Bubbles Particles",
                        "Bubble",
                        "Bubbles",
                        "Particle"
                    });
                    if (found != null)
                    {
                        context.SetBubbleParticleTemplate(found);
                        log.AppendLine($"[OK] Auto-filled BubbleParticleTemplate: {found.name}");
                        anyFieldSet = true;
                    }
                    else
                    {
                        log.AppendLine("[INFO] BubbleParticleTemplate not found. Will be used in Phase 4+.");
                    }
                }
                else
                {
                    log.AppendLine($"[OK] BubbleParticleTemplate already set: {context.BubbleParticleTemplate.name}");
                }

                // DynamicEffect
                if (context.DynamicEffectTemplate == null)
                {
                    GameObject found = FindChildInMapSettings(context.MapSettingsTemplateRoot, new[] {
                        "Water Whirpool",
                        "ImpactRipple",
                        "Ripples",
                        "Ripple",
                        "Whirlpool",
                        "Dynamic"
                    });
                    if (found != null)
                    {
                        context.SetDynamicEffectTemplate(found);
                        log.AppendLine($"[OK] Auto-filled DynamicEffectTemplate: {found.name}");
                        anyFieldSet = true;
                    }
                    else
                    {
                        log.AppendLine("[INFO] DynamicEffectTemplate not found. Will be used in Phase 4+.");
                    }
                }
                else
                {
                    log.AppendLine($"[OK] DynamicEffectTemplate already set: {context.DynamicEffectTemplate.name}");
                }
            }
            else
            {
                log.AppendLine("[INFO] MapSettingsTemplateRoot is null. Skipping child template auto-fill.");
            }

            if (anyFieldSet)
            {
                // SceneContext는 MonoBehaviour이므로 EditorUtility.SetDirty로 저장
                EditorUtility.SetDirty(context);
                log.AppendLine("[OK] SceneContext updated.");
            }
            else
            {
                log.AppendLine("[INFO] No fields were changed. SceneContext is already up-to-date.");
            }

            log.AppendLine("===== Auto Fill Scene Context Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// SettingsSO + SceneContext를 한 번에 생성/보정한다.
        /// </summary>
        public static void CreateOrAutoFillAll(out DeepLightMapAutoBuilderSettingsSO settings, out DeepLightMapAutoBuilderSceneContext context)
        {
            settings = CreateOrAutoFillSettingsAsset();
            context = CreateOrFindSceneContext();
            if (context != null)
            {
                AutoFillSceneContextFromCurrentScene(context);
            }
        }

        // ======================================================================
        //  Auto-Fill Methods (SettingsSO - Project Asset only)
        // ======================================================================

        /// <summary>WorldMapConfigSO 자동 연결</summary>
        private static bool AutoFillWorldMapConfig(DeepLightMapAutoBuilderSettingsSO settings, SerializedObject serialized, StringBuilder log)
        {
            if (settings.WorldMapConfig != null)
            {
                log.AppendLine($"[OK] WorldMapConfig already set: {settings.WorldMapConfig.name}");
                return false;
            }

            // 우선순위 1: 정확한 이름 검색
            string[] guids = AssetDatabase.FindAssets("WorldMapConfig_Main t:WorldMapConfigSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                WorldMapConfigSO config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>(path);
                if (config != null)
                {
                    settings.SetWorldMapConfig(config);
                    log.AppendLine($"[OK] Auto-filled WorldMapConfig: {config.name} (from {path})");
                    return true;
                }
            }

            // 우선순위 2: 이름에 "WorldMapConfig" 포함
            guids = AssetDatabase.FindAssets("WorldMapConfig t:WorldMapConfigSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                WorldMapConfigSO config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>(path);
                if (config != null)
                {
                    settings.SetWorldMapConfig(config);
                    log.AppendLine($"[OK] Auto-filled WorldMapConfig: {config.name} (fallback, from {path})");
                    return true;
                }
            }

            log.AppendLine("[WARNING] WorldMapConfig not found! Create WorldMapConfig_Main first.");
            return false;
        }

        /// <summary>WorldMapScenarioPresetSO 자동 연결</summary>
        private static bool AutoFillScenarioPreset(DeepLightMapAutoBuilderSettingsSO settings, SerializedObject serialized, StringBuilder log)
        {
            if (settings.ScenarioPreset != null)
            {
                log.AppendLine($"[OK] ScenarioPreset already set: {settings.ScenarioPreset.name}");
                return false;
            }

            // 우선순위 1: 정확한 이름 검색
            string[] guids = AssetDatabase.FindAssets("WMSP_DeepLight_Default t:WorldMapScenarioPresetSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                WorldMapScenarioPresetSO preset = AssetDatabase.LoadAssetAtPath<WorldMapScenarioPresetSO>(path);
                if (preset != null)
                {
                    settings.SetScenarioPreset(preset);
                    log.AppendLine($"[OK] Auto-filled ScenarioPreset: {preset.name} (from {path})");
                    return true;
                }
            }

            // 우선순위 2: 이름에 "WorldMapScenario" 또는 "Scenario" 포함
            guids = AssetDatabase.FindAssets("Scenario t:WorldMapScenarioPresetSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                WorldMapScenarioPresetSO preset = AssetDatabase.LoadAssetAtPath<WorldMapScenarioPresetSO>(path);
                if (preset != null)
                {
                    settings.SetScenarioPreset(preset);
                    log.AppendLine($"[OK] Auto-filled ScenarioPreset: {preset.name} (fallback, from {path})");
                    return true;
                }
            }

            log.AppendLine("[WARNING] ScenarioPreset not found! Create WMSP_DeepLight_Default first.");
            return false;
        }

        // ======================================================================
        //  Utility Methods
        // ======================================================================

        /// <summary>
        /// MapSettings 하위 Transform을 재귀적으로 탐색하여 이름 우선순위에 따라 GameObject를 찾는다.
        /// Scene 오브젝트를 절대 수정하지 않으며, Transform.Find만 사용한다.
        /// </summary>
        private static GameObject FindChildInMapSettings(GameObject mapSettings, string[] namePriority)
        {
            if (mapSettings == null) return null;

            Transform mapTransform = mapSettings.transform;

            // 우선순위 순서대로 정확한 이름 검색
            foreach (string name in namePriority)
            {
                // 전체 하위 트리에서 재귀 검색
                Transform result = FindChildRecursive(mapTransform, name);
                if (result != null)
                    return result.gameObject;
            }

            return null;
        }

        /// <summary>Transform 하위 트리에서 이름이 일치하는 첫 번째 자식을 재귀적으로 찾는다.</summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;

            // 정확한 이름 일치 검사
            if (parent.name == name)
                return parent;

            // 부분 일치 검사 (name이 parent.name에 포함되는지)
            if (parent.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return parent;

            // 자식 재귀 검색
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildRecursive(parent.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>지정된 경로에 폴더가 없으면 생성한다.</summary>
        private static void EnsureFolderPath(string folderPath)
        {
            // "Assets/..." 형식의 경로를 "/"로 분할
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string parentPath = currentPath;
                currentPath = parentPath + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    AssetDatabase.CreateFolder(parentPath, parts[i]);
                }
            }
        }
    }
}
