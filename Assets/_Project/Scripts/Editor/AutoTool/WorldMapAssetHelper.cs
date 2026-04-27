using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// WorldMapSetupTool의 에셋/폴더 관련 기능을 분리한 보조 유틸리티.
    /// 폴더 생성, ScriptableObject 생성/보정, 검증, Ping 기능을 제공한다.
    /// Scene 오브젝트/HUD/MiniGrid/ZoneRoot 생성은 포함하지 않는다.
    /// </summary>
    public static class WorldMapAssetHelper
    {
        // ===== 폴더 경로 상수 =====
        public const string CONFIG_FOLDER = "Assets/_Project/ScriptableObjects/World/Config";
        public const string ZONES_FOLDER = "Assets/_Project/ScriptableObjects/World/Zones";
        public const string UNLOCK_CONDITIONS_FOLDER = "Assets/_Project/ScriptableObjects/World/UnlockConditions";
        public const string DEBUG_FOLDER = "Assets/_Project/ScriptableObjects/World/Debug";
        public const string AMBIENT_FOLDER = "Assets/_Project/ScriptableObjects/World/AmbientProfiles";

        private static readonly string[] AllFolders = { CONFIG_FOLDER, ZONES_FOLDER, UNLOCK_CONDITIONS_FOLDER, DEBUG_FOLDER, AMBIENT_FOLDER };

        // ===== 결과 요약 (WorldMapSetupTool과 공유) =====
        private static readonly StringBuilder _summaryLog = new();
        private static int _createdCount;
        private static int _foundCount;
        private static int _fixedCount;
        private static int _warningCount;

        // ===== Public: 전체 에셋 생성/보정 =====

        /// <summary>모든 폴더 + ScriptableObject 에셋을 생성/보정한다. Scene/UI/HUD는 건드리지 않는다.</summary>
        public static void CreateUpdateAllAssets()
        {
            ResetSummary();
            LogSummary("===== WorldMap Assets Only 생성/보정 시작 =====");

            CreateAllFolders();
            CreateAllConfigAssets();
            CreateAllUnlockConditionSets();
            CreateAllAmbientProfiles();
            CreateAllZoneDataAssets();
            AssignAmbientProfilesToZones();

            PrintFinalSummary();
        }

        /// <summary>모든 WorldMap 에셋의 존재 여부와 값 정합성을 검증한다.</summary>
        public static void ValidateAllAssets()
        {
            ResetSummary();
            LogSummary("===== WorldMap Assets 검증 시작 =====");

            int totalChecks = 0;
            int passedChecks = 0;
            int failedChecks = 0;

            // 1. 폴더 존재 확인
            foreach (string folder in AllFolders)
            {
                totalChecks++;
                if (Directory.Exists(folder))
                {
                    passedChecks++;
                    LogFound($"폴더 존재: {folder}");
                }
                else
                {
                    failedChecks++;
                    LogWarning($"폴더 없음: {folder}");
                }
            }

            // 2. WorldMapConfig_Main 검증
            totalChecks++;
            var config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>($"{CONFIG_FOLDER}/WorldMapConfig_Main.asset");
            if (config != null)
            {
                passedChecks++;
                LogFound($"WorldMapConfig_Main 존재: {CONFIG_FOLDER}/WorldMapConfig_Main.asset");

                // 값 검증
                SerializedObject so = new SerializedObject(config);
                totalChecks += 7;
                if (so.FindProperty("gridWidth").intValue == 10) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.gridWidth != 10"); }
                if (so.FindProperty("gridHeight").intValue == 10) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.gridHeight != 10"); }
                if (Mathf.Abs(so.FindProperty("zoneSize").floatValue - 400f) < 0.01f) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.zoneSize != 400"); }
                if (Mathf.Abs(so.FindProperty("worldMinX").floatValue - (-2000f)) < 0.01f) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.worldMinX != -2000"); }
                if (Mathf.Abs(so.FindProperty("worldMaxX").floatValue - 2000f) < 0.01f) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.worldMaxX != 2000"); }
                if (Mathf.Abs(so.FindProperty("worldMinZ").floatValue - (-2000f)) < 0.01f) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.worldMinZ != -2000"); }
                if (Mathf.Abs(so.FindProperty("worldMaxZ").floatValue - 2000f) < 0.01f) passedChecks++; else { failedChecks++; LogWarning("WorldMapConfig_Main.worldMaxZ != 2000"); }

                // defaultZoneData 참조 검증
                totalChecks++;
                var defaultZone = so.FindProperty("defaultZoneData").objectReferenceValue as ZoneDataSO;
                if (defaultZone != null)
                {
                    passedChecks++;
                    LogFound($"WorldMapConfig_Main.defaultZoneData = {defaultZone.name}");
                }
                else
                {
                    failedChecks++;
                    LogWarning("WorldMapConfig_Main.defaultZoneData == null (ZoneData_DefaultFallback 필요)");
                }
            }
            else
            {
                failedChecks++;
                LogWarning("WorldMapConfig_Main 에셋 없음");
            }

            // 3. UnlockConditionSet 검증
            string[] ucsNames = { "UCS_Free", "UCS_ResearchZone_Test", "UCS_SealedNorth_Test" };
            foreach (string name in ucsNames)
            {
                totalChecks++;
                var ucs = AssetDatabase.LoadAssetAtPath<UnlockConditionSetSO>($"{UNLOCK_CONDITIONS_FOLDER}/{name}.asset");
                if (ucs != null)
                {
                    passedChecks++;
                    LogFound($"UnlockConditionSet 존재: {name}");
                }
                else
                {
                    failedChecks++;
                    LogWarning($"UnlockConditionSet 없음: {name}");
                }
            }

            // 4. AmbientProfile 검증
            string[] ambientNames = { "Ambient_Hub_Safe", "Ambient_Research_Tense", "Ambient_Sealed_Danger", "Ambient_OutOfBounds_Default" };
            foreach (string name in ambientNames)
            {
                totalChecks++;
                var ambient = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/{name}.asset");
                if (ambient != null)
                {
                    passedChecks++;
                    LogFound($"AmbientProfile 존재: {name}");
                }
                else
                {
                    failedChecks++;
                    LogWarning($"AmbientProfile 없음: {name}");
                }
            }

            // 5. ZoneData 검증
            string[] zoneNames = { "ZoneData_DefaultFallback", "ZoneData_Hub_E5", "ZoneData_Research_I6", "ZoneData_Sealed_F10" };
            foreach (string name in zoneNames)
            {
                totalChecks++;
                var zone = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/{name}.asset");
                if (zone != null)
                {
                    passedChecks++;
                    LogFound($"ZoneData 존재: {name}");

                    // AmbientProfile 참조 검증
                    totalChecks++;
                    SerializedObject zoneSo = new SerializedObject(zone);
                    var ambientRef = zoneSo.FindProperty("ambientProfile").objectReferenceValue as ZoneAmbientProfileSO;
                    if (ambientRef != null)
                    {
                        passedChecks++;
                        LogFound($"  {name}.ambientProfile = {ambientRef.name}");
                    }
                    else
                    {
                        failedChecks++;
                        LogWarning($"  {name}.ambientProfile == null");
                    }

                    // UnlockConditionSet 참조 검증
                    totalChecks++;
                    var ucsRef = zoneSo.FindProperty("unlockConditionSet").objectReferenceValue as UnlockConditionSetSO;
                    if (ucsRef != null)
                    {
                        passedChecks++;
                        LogFound($"  {name}.unlockConditionSet = {ucsRef.name}");
                    }
                    else
                    {
                        failedChecks++;
                        LogWarning($"  {name}.unlockConditionSet == null");
                    }
                }
                else
                {
                    failedChecks++;
                    LogWarning($"ZoneData 없음: {name}");
                }
            }

            _summaryLog.AppendLine("");
            _summaryLog.AppendLine($"===== 검증 결과: {totalChecks}개 검사, {passedChecks}개 통과, {failedChecks}개 실패 =====");
            PrintFinalSummary();
        }

        /// <summary>모든 WorldMap 에셋을 Ping하여 에디터에서 찾기 쉽게 한다.</summary>
        public static void PingAllAssets()
        {
            ResetSummary();
            LogSummary("===== WorldMap Assets Ping =====");

            var config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>($"{CONFIG_FOLDER}/WorldMapConfig_Main.asset");
            if (config != null)
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
                LogFound($"Ping: WorldMapConfig_Main");
            }

            string[] ucsNames = { "UCS_Free", "UCS_ResearchZone_Test", "UCS_SealedNorth_Test" };
            foreach (string name in ucsNames)
            {
                var ucs = AssetDatabase.LoadAssetAtPath<UnlockConditionSetSO>($"{UNLOCK_CONDITIONS_FOLDER}/{name}.asset");
                if (ucs != null)
                {
                    EditorGUIUtility.PingObject(ucs);
                    LogFound($"Ping: {name}");
                }
            }

            string[] ambientNames = { "Ambient_Hub_Safe", "Ambient_Research_Tense", "Ambient_Sealed_Danger", "Ambient_OutOfBounds_Default" };
            foreach (string name in ambientNames)
            {
                var ambient = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/{name}.asset");
                if (ambient != null)
                {
                    EditorGUIUtility.PingObject(ambient);
                    LogFound($"Ping: {name}");
                }
            }

            string[] zoneNames = { "ZoneData_DefaultFallback", "ZoneData_Hub_E5", "ZoneData_Research_I6", "ZoneData_Sealed_F10" };
            foreach (string name in zoneNames)
            {
                var zone = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/{name}.asset");
                if (zone != null)
                {
                    EditorGUIUtility.PingObject(zone);
                    LogFound($"Ping: {name}");
                }
            }

            PrintFinalSummary();
        }

        // ======================================================================
        //  폴더 생성
        // ======================================================================

        public static void CreateAllFolders()
        {
            foreach (string folder in AllFolders)
            {
                CreateFolder(folder);
            }
            LogSummary("[폴더] 5개 폴더 생성 완료.");
        }

        private static void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
                LogCreated($"폴더: {path}");
            }
            else
            {
                LogFound($"폴더 (이미 존재): {path}");
            }
        }

        // ======================================================================
        //  WorldMapConfigSO 생성/보정
        // ======================================================================

        public static void CreateAllConfigAssets()
        {
            CreateFolder(CONFIG_FOLDER);
            string configPath = $"{CONFIG_FOLDER}/WorldMapConfig_Main.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>(configPath);
            if (existing != null)
            {
                LogFound($"WorldMapConfig_Main (이미 존재): {configPath}");
                SerializedObject serialized = new SerializedObject(existing);
                bool changed = false;
                if (serialized.FindProperty("gridWidth").intValue != 10) { serialized.FindProperty("gridWidth").intValue = 10; changed = true; }
                if (serialized.FindProperty("gridHeight").intValue != 10) { serialized.FindProperty("gridHeight").intValue = 10; changed = true; }
                if (Mathf.Abs(serialized.FindProperty("zoneSize").floatValue - 400f) > 0.01f) { serialized.FindProperty("zoneSize").floatValue = 400f; changed = true; }
                if (Mathf.Abs(serialized.FindProperty("worldMinX").floatValue - (-2000f)) > 0.01f) { serialized.FindProperty("worldMinX").floatValue = -2000f; changed = true; }
                if (Mathf.Abs(serialized.FindProperty("worldMaxX").floatValue - 2000f) > 0.01f) { serialized.FindProperty("worldMaxX").floatValue = 2000f; changed = true; }
                if (Mathf.Abs(serialized.FindProperty("worldMinZ").floatValue - (-2000f)) > 0.01f) { serialized.FindProperty("worldMinZ").floatValue = -2000f; changed = true; }
                if (Mathf.Abs(serialized.FindProperty("worldMaxZ").floatValue - 2000f) > 0.01f) { serialized.FindProperty("worldMaxZ").floatValue = 2000f; changed = true; }
                var defaultZone = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/ZoneData_DefaultFallback.asset");
                if (defaultZone != null)
                {
                    var currentDefault = serialized.FindProperty("defaultZoneData").objectReferenceValue as ZoneDataSO;
                    if (currentDefault != defaultZone)
                    {
                        serialized.FindProperty("defaultZoneData").objectReferenceValue = defaultZone;
                        changed = true;
                    }
                }
                if (changed)
                {
                    serialized.ApplyModifiedProperties();
                    LogFixed($"WorldMapConfig_Main 값 보정 완료.");
                }
                return;
            }

            var config = ScriptableObject.CreateInstance<WorldMapConfigSO>();
            SerializedObject so = new SerializedObject(config);
            so.FindProperty("gridWidth").intValue = 10;
            so.FindProperty("gridHeight").intValue = 10;
            so.FindProperty("zoneSize").floatValue = 400f;
            so.FindProperty("worldMinX").floatValue = -2000f;
            so.FindProperty("worldMaxX").floatValue = 2000f;
            so.FindProperty("worldMinZ").floatValue = -2000f;
            so.FindProperty("worldMaxZ").floatValue = 2000f;
            so.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            LogCreated($"WorldMapConfig_Main: {configPath}");
        }

        // ======================================================================
        //  UnlockConditionSetSO 생성/보정
        // ======================================================================

        public static void CreateAllUnlockConditionSets()
        {
            CreateFolder(UNLOCK_CONDITIONS_FOLDER);

            CreateOrPatchUnlockConditionSet("UCS_Free", (so) =>
            {
                so.FindProperty("traversalConditions").ClearArray();
                so.FindProperty("knowledgeConditions").ClearArray();
                so.FindProperty("narrativeConditions").ClearArray();
                so.FindProperty("riskConditions").ClearArray();
            });

            CreateOrPatchUnlockConditionSet("UCS_ResearchZone_Test", (so) =>
            {
                so.FindProperty("traversalConditions").ClearArray();
                so.FindProperty("knowledgeConditions").ClearArray();
                so.FindProperty("narrativeConditions").ClearArray();
                so.FindProperty("riskConditions").ClearArray();
                AddConditionEntry(so, "traversalConditions", UnlockConditionType.HasUpgrade, "upgrade_battery_mk2", 0, "Battery Mk2 required");
                AddConditionEntry(so, "knowledgeConditions", UnlockConditionType.HasLogCount, "empty", 2, "Need 2 logs");
                AddConditionEntry(so, "narrativeConditions", UnlockConditionType.HasNarrativeFlag, "mara_research_analysis_done", 0, "Mara analysis required");
            });

            CreateOrPatchUnlockConditionSet("UCS_SealedNorth_Test", (so) =>
            {
                so.FindProperty("traversalConditions").ClearArray();
                so.FindProperty("knowledgeConditions").ClearArray();
                so.FindProperty("narrativeConditions").ClearArray();
                so.FindProperty("riskConditions").ClearArray();
                AddConditionEntry(so, "traversalConditions", UnlockConditionType.HasUpgrade, "upgrade_pressure_hull_lv3", 0, "Pressure Hull Lv3 required");
                AddConditionEntry(so, "knowledgeConditions", UnlockConditionType.HasLog, "log_base_02", 0, "Base log required");
                AddConditionEntry(so, "knowledgeConditions", UnlockConditionType.HasLog, "log_research_03", 0, "Research log required");
                AddConditionEntry(so, "riskConditions", UnlockConditionType.HasUpgrade, "upgrade_resonance_filter", 0, "Resonance Filter required");
            });

            LogSummary("[UnlockCondition] 3개 세트 생성 완료.");
        }

        private static void CreateOrPatchUnlockConditionSet(string assetName, System.Action<SerializedObject> patchAction)
        {
            string path = $"{UNLOCK_CONDITIONS_FOLDER}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnlockConditionSetSO>(path);
            if (existing != null)
            {
                LogFound($"UnlockConditionSet (이미 존재): {assetName}");
                SerializedObject so = new SerializedObject(existing);
                patchAction(so);
                so.ApplyModifiedProperties();
                LogFixed($"UnlockConditionSet 값 보정: {assetName}");
                return;
            }

            var instance = ScriptableObject.CreateInstance<UnlockConditionSetSO>();
            SerializedObject soNew = new SerializedObject(instance);
            patchAction(soNew);
            soNew.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            LogCreated($"UnlockConditionSet: {assetName}");
        }

        private static void AddConditionEntry(SerializedObject so, string fieldName, UnlockConditionType type, string key, int value, string hint)
        {
            SerializedProperty list = so.FindProperty(fieldName);
            int idx = list.arraySize;
            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(idx);
            entry.FindPropertyRelative("conditionType").enumValueIndex = (int)type;
            entry.FindPropertyRelative("conditionKey").stringValue = key;
            entry.FindPropertyRelative("requiredValue").intValue = value;
            entry.FindPropertyRelative("displayHint").stringValue = hint;
        }

        // ======================================================================
        //  ZoneAmbientProfileSO 생성/보정
        // ======================================================================

        public static void CreateAllAmbientProfiles()
        {
            CreateFolder(AMBIENT_FOLDER);

            CreateOrPatchAmbientProfile("Ambient_Hub_Safe", (so) =>
            {
                so.FindProperty("profileId").stringValue = "ambient_hub_safe";
                so.FindProperty("displayName").stringValue = "Hub Safe";
                so.FindProperty("backgroundColor").colorValue = new Color(0.08f, 0.22f, 0.28f, 1f);
                so.FindProperty("fogColor").colorValue = new Color(0.12f, 0.35f, 0.42f, 1f);
                so.FindProperty("fogDensity").floatValue = 0.002f;
                so.FindProperty("ambientLightColor").colorValue = new Color(0.45f, 0.55f, 0.60f, 1f);
                so.FindProperty("bgmStateKey").stringValue = "bgm_hub_safe";
                so.FindProperty("riskOverlayColor").colorValue = new Color(0f, 0f, 0f, 0f);
                so.FindProperty("riskOverlayIntensity").floatValue = 0f;
                so.FindProperty("showDangerWarning").boolValue = false;
                so.FindProperty("debugDescription").stringValue = "Safe hub waters";
            });

            CreateOrPatchAmbientProfile("Ambient_Research_Tense", (so) =>
            {
                so.FindProperty("profileId").stringValue = "ambient_research_tense";
                so.FindProperty("displayName").stringValue = "Research Tense";
                so.FindProperty("backgroundColor").colorValue = new Color(0.05f, 0.18f, 0.22f, 1f);
                so.FindProperty("fogColor").colorValue = new Color(0.18f, 0.42f, 0.45f, 1f);
                so.FindProperty("fogDensity").floatValue = 0.006f;
                so.FindProperty("ambientLightColor").colorValue = new Color(0.35f, 0.45f, 0.50f, 1f);
                so.FindProperty("bgmStateKey").stringValue = "bgm_research_tense";
                so.FindProperty("riskOverlayColor").colorValue = new Color(1f, 0.55f, 0.2f, 0.2f);
                so.FindProperty("riskOverlayIntensity").floatValue = 0.35f;
                so.FindProperty("showDangerWarning").boolValue = true;
                so.FindProperty("debugDescription").stringValue = "Research zone medium tension";
            });

            CreateOrPatchAmbientProfile("Ambient_Sealed_Danger", (so) =>
            {
                so.FindProperty("profileId").stringValue = "ambient_sealed_danger";
                so.FindProperty("displayName").stringValue = "Sealed Danger";
                so.FindProperty("backgroundColor").colorValue = new Color(0.03f, 0.08f, 0.12f, 1f);
                so.FindProperty("fogColor").colorValue = new Color(0.15f, 0.08f, 0.12f, 1f);
                so.FindProperty("fogDensity").floatValue = 0.012f;
                so.FindProperty("ambientLightColor").colorValue = new Color(0.22f, 0.22f, 0.28f, 1f);
                so.FindProperty("bgmStateKey").stringValue = "bgm_sealed_danger";
                so.FindProperty("riskOverlayColor").colorValue = new Color(1f, 0.15f, 0.15f, 0.35f);
                so.FindProperty("riskOverlayIntensity").floatValue = 0.7f;
                so.FindProperty("showDangerWarning").boolValue = true;
                so.FindProperty("debugDescription").stringValue = "High risk sealed perimeter";
            });

            CreateOrPatchAmbientProfile("Ambient_OutOfBounds_Default", (so) =>
            {
                so.FindProperty("profileId").stringValue = "ambient_out_of_bounds";
                so.FindProperty("displayName").stringValue = "Out Of Bounds";
                so.FindProperty("backgroundColor").colorValue = new Color(0.05f, 0.05f, 0.08f, 1f);
                so.FindProperty("fogColor").colorValue = new Color(0.08f, 0.08f, 0.10f, 1f);
                so.FindProperty("fogDensity").floatValue = 0.001f;
                so.FindProperty("ambientLightColor").colorValue = new Color(0.20f, 0.20f, 0.25f, 1f);
                so.FindProperty("bgmStateKey").stringValue = "bgm_none";
                so.FindProperty("riskOverlayColor").colorValue = new Color(1f, 0f, 0f, 0f);
                so.FindProperty("riskOverlayIntensity").floatValue = 0f;
                so.FindProperty("showDangerWarning").boolValue = false;
                so.FindProperty("debugDescription").stringValue = "Neutral fallback for out-of-bounds positions";
            });

            LogSummary("[AmbientProfile] 4개 프로필 생성 완료.");
        }

        private static void CreateOrPatchAmbientProfile(string assetName, System.Action<SerializedObject> patchAction)
        {
            string path = $"{AMBIENT_FOLDER}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>(path);
            if (existing != null)
            {
                LogFound($"AmbientProfile (이미 존재): {assetName}");
                SerializedObject so = new SerializedObject(existing);
                patchAction(so);
                so.ApplyModifiedProperties();
                LogFixed($"AmbientProfile 값 보정: {assetName}");
                return;
            }

            var instance = ScriptableObject.CreateInstance<ZoneAmbientProfileSO>();
            SerializedObject soNew = new SerializedObject(instance);
            patchAction(soNew);
            soNew.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            LogCreated($"AmbientProfile: {assetName}");
        }

        // ======================================================================
        //  ZoneDataSO 생성/보정
        // ======================================================================

        public static void CreateAllZoneDataAssets()
        {
            CreateFolder(ZONES_FOLDER);

            var ucsFree = AssetDatabase.LoadAssetAtPath<UnlockConditionSetSO>($"{UNLOCK_CONDITIONS_FOLDER}/UCS_Free.asset");
            var ucsResearch = AssetDatabase.LoadAssetAtPath<UnlockConditionSetSO>($"{UNLOCK_CONDITIONS_FOLDER}/UCS_ResearchZone_Test.asset");
            var ucsSealed = AssetDatabase.LoadAssetAtPath<UnlockConditionSetSO>($"{UNLOCK_CONDITIONS_FOLDER}/UCS_SealedNorth_Test.asset");
            var ambientHub = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_Hub_Safe.asset");
            var ambientResearch = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_Research_Tense.asset");
            var ambientSealed = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_Sealed_Danger.asset");
            var ambientOob = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_OutOfBounds_Default.asset");

            CreateOrPatchZoneData("ZoneData_DefaultFallback", (so) =>
            {
                SetZoneId(so, "E5");
                SetRegionId(so, "Hub");

                so.FindProperty("displayName").stringValue = "Default Zone";
                so.FindProperty("unlockConditionSet").objectReferenceValue = ucsFree;
                so.FindProperty("baseRiskLevel").floatValue = 0f;
                so.FindProperty("isStartingZone").boolValue = false;
                so.FindProperty("isAlwaysUnlocked").boolValue = true;
                so.FindProperty("isStoryCritical").boolValue = false;
                so.FindProperty("zoneColor").colorValue = Color.white;
                so.FindProperty("ambientProfile").objectReferenceValue = ambientOob;
            });

            CreateOrPatchZoneData("ZoneData_Hub_E5", (so) =>
            {
                SetZoneId(so, "E5");
                SetRegionId(so, "Hub");

                so.FindProperty("displayName").stringValue = "Village Dock";
                so.FindProperty("unlockConditionSet").objectReferenceValue = ucsFree;
                so.FindProperty("baseRiskLevel").floatValue = 0f;
                so.FindProperty("isStartingZone").boolValue = true;
                so.FindProperty("isAlwaysUnlocked").boolValue = false;
                so.FindProperty("isStoryCritical").boolValue = true;
                so.FindProperty("zoneColor").colorValue = new Color(0.6f, 0.9f, 0.8f, 1f); // light cyan/green
                so.FindProperty("ambientProfile").objectReferenceValue = ambientHub;
            });

            CreateOrPatchZoneData("ZoneData_Research_I6", (so) =>
            {
                SetZoneId(so, "I6");
                SetRegionId(so, "East");

                so.FindProperty("displayName").stringValue = "Research Waters";
                so.FindProperty("unlockConditionSet").objectReferenceValue = ucsResearch;
                so.FindProperty("baseRiskLevel").floatValue = 0.35f;
                so.FindProperty("isStartingZone").boolValue = false;
                so.FindProperty("isAlwaysUnlocked").boolValue = false;
                so.FindProperty("isStoryCritical").boolValue = true;
                so.FindProperty("zoneColor").colorValue = new Color(1f, 0.7f, 0.1f, 1f); // yellow/orange
                so.FindProperty("ambientProfile").objectReferenceValue = ambientResearch;
            });

            CreateOrPatchZoneData("ZoneData_Sealed_F10", (so) =>
            {
                SetZoneId(so, "F10");
                SetRegionId(so, "North");

                so.FindProperty("displayName").stringValue = "Sealed Perimeter";
                so.FindProperty("unlockConditionSet").objectReferenceValue = ucsSealed;
                so.FindProperty("baseRiskLevel").floatValue = 0.7f;
                so.FindProperty("isStartingZone").boolValue = false;
                so.FindProperty("isAlwaysUnlocked").boolValue = false;
                so.FindProperty("isStoryCritical").boolValue = true;
                so.FindProperty("zoneColor").colorValue = Color.red;
                so.FindProperty("ambientProfile").objectReferenceValue = ambientSealed;
            });

            LogSummary("[ZoneData] 4개 존 데이터 생성 완료.");
        }

        private static void CreateOrPatchZoneData(string assetName, System.Action<SerializedObject> patchAction)
        {
            string path = $"{ZONES_FOLDER}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ZoneDataSO>(path);
            if (existing != null)
            {
                LogFound($"ZoneData (이미 존재): {assetName}");
                SerializedObject so = new SerializedObject(existing);
                patchAction(so);
                so.ApplyModifiedProperties();
                LogFixed($"ZoneData 값 보정: {assetName}");
                return;
            }

            var instance = ScriptableObject.CreateInstance<ZoneDataSO>();
            SerializedObject soNew = new SerializedObject(instance);
            patchAction(soNew);
            soNew.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            LogCreated($"ZoneData: {assetName}");
        }

        /// <summary>
        /// ZoneId struct를 SerializedProperty로 설정한다.
        /// ZoneId는 Column(char) + Row(int) 필드를 가진 struct이므로,
        /// 각각을 개별적으로 설정한다.
        /// </summary>
        private static void SetZoneId(SerializedObject so, string zoneId)
        {
            var zoneIdProp = so.FindProperty("zoneId");
            if (zoneIdProp == null) return;

            // ZoneId struct의 Column과 Row 필드에 접근
            var columnProp = zoneIdProp.FindPropertyRelative("Column");
            var rowProp = zoneIdProp.FindPropertyRelative("Row");

            if (columnProp != null && rowProp != null)
            {
                // ZoneId struct: Column(char), Row(int)
                if (!string.IsNullOrEmpty(zoneId) && zoneId.Length >= 2)
                {
                    columnProp.intValue = zoneId[0]; // char -> int (Unity serializes char as int)
                    if (int.TryParse(zoneId.Substring(1), out int row))
                    {
                        rowProp.intValue = row;
                    }
                }
            }
            else
            {
                // Fallback: try direct string (legacy)
                zoneIdProp.stringValue = zoneId;
            }
        }

        /// <summary>
        /// RegionId struct를 SerializedProperty로 설정한다.
        /// RegionId는 Id(string) 필드를 가진 struct이므로,
        /// nested Id 필드에 접근하여 설정한다.
        /// </summary>
        private static void SetRegionId(SerializedObject so, string regionId)
        {
            var regionIdProp = so.FindProperty("regionId");
            if (regionIdProp == null) return;

            // RegionId struct의 Id 필드에 접근
            var idProp = regionIdProp.FindPropertyRelative("Id");
            if (idProp != null)
            {
                idProp.stringValue = regionId;
            }
            else
            {
                // Fallback: try direct string
                regionIdProp.stringValue = regionId;
            }
        }

        // ======================================================================
        //  AmbientProfile -> ZoneData 연결

        // ======================================================================

        public static void AssignAmbientProfilesToZones()
        {
            var ambientHub = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_Hub_Safe.asset");
            var ambientResearch = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_Research_Tense.asset");
            var ambientSealed = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_Sealed_Danger.asset");
            var ambientOob = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_OutOfBounds_Default.asset");

            AssignAmbientToZone("ZoneData_DefaultFallback", ambientOob);
            AssignAmbientToZone("ZoneData_Hub_E5", ambientHub);
            AssignAmbientToZone("ZoneData_Research_I6", ambientResearch);
            AssignAmbientToZone("ZoneData_Sealed_F10", ambientSealed);

            LogSummary("[Ambient 연결] 4개 존에 AmbientProfile 연결 완료.");
        }

        private static void AssignAmbientToZone(string zoneAssetName, ZoneAmbientProfileSO profile)
        {
            string path = $"{ZONES_FOLDER}/{zoneAssetName}.asset";
            var zoneData = AssetDatabase.LoadAssetAtPath<ZoneDataSO>(path);
            if (zoneData == null) return;

            SerializedObject so = new SerializedObject(zoneData);
            var current = so.FindProperty("ambientProfile").objectReferenceValue as ZoneAmbientProfileSO;
            if (current != profile)
            {
                so.FindProperty("ambientProfile").objectReferenceValue = profile;
                so.ApplyModifiedProperties();
                LogFixed($"ZoneData {zoneAssetName} AmbientProfile 연결 보정.");
            }
        }

        // ======================================================================
        //  결과 요약 유틸리티
        // ======================================================================

        private static void ResetSummary()
        {
            _summaryLog.Clear();
            _createdCount = 0;
            _foundCount = 0;
            _fixedCount = 0;
            _warningCount = 0;
        }

        private static void LogSummary(string message)
        {
            _summaryLog.AppendLine(message);
            Debug.Log(message);
        }

        private static void LogCreated(string message)
        {
            _createdCount++;
            _summaryLog.AppendLine($"  [생성] {message}");
            Debug.Log($"[WorldMapAssetHelper] 생성: {message}");
        }

        private static void LogFound(string message)
        {
            _foundCount++;
            _summaryLog.AppendLine($"  [발견] {message}");
            Debug.Log($"[WorldMapAssetHelper] 발견: {message}");
        }

        private static void LogFixed(string message)
        {
            _fixedCount++;
            _summaryLog.AppendLine($"  [보정] {message}");
            Debug.Log($"[WorldMapAssetHelper] 보정: {message}");
        }

        private static void LogWarning(string message)
        {
            _warningCount++;
            _summaryLog.AppendLine($"  [경고] {message}");
            Debug.LogWarning($"[WorldMapAssetHelper] 경고: {message}");
        }

        private static void PrintFinalSummary()
        {
            _summaryLog.AppendLine("");
            _summaryLog.AppendLine("===== WorldMap Asset Helper 결과 요약 =====");
            _summaryLog.AppendLine($"  생성: {_createdCount}개");
            _summaryLog.AppendLine($"  발견/재사용: {_foundCount}개");
            _summaryLog.AppendLine($"  보정/재연결: {_fixedCount}개");
            _summaryLog.AppendLine($"  경고: {_warningCount}개");
            _summaryLog.AppendLine("");

            Debug.Log(_summaryLog.ToString());
        }
    }
}
