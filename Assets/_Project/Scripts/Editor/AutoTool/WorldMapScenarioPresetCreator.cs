using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// 기획서 기준 기본 ScenarioPreset과 EnvironmentProfile 에셋들을 생성/보정하는 Editor Utility.
    /// Scene Hierarchy는 절대 수정하지 않으며, ScriptableObject 에셋만 생성/보정한다.
    ///
    /// 생성하는 에셋:
    /// - WMSP_DeepLight_Default (WorldMapScenarioPresetSO) - 7개 Zone Rule 포함
    /// - ZEP_Hub ~ ZEP_OpenWater (ZoneEnvironmentProfileSO) - 8개 환경 프로필
    /// </summary>
    public static class WorldMapScenarioPresetCreator
    {
        // ===== 폴더 경로 =====
        private const string SCENARIO_FOLDER = "Assets/_Project/ScriptableObjects/World/ScenarioPresets";
        private const string ENVIRONMENT_FOLDER = "Assets/_Project/ScriptableObjects/World/EnvironmentProfiles";

        // ===== 결과 요약 =====
        private static readonly StringBuilder _summaryLog = new();
        private static int _createdCount;
        private static int _foundCount;
        private static int _fixedCount;
        private static int _warningCount;

        // ===== 메뉴 항목 =====

        /// <summary>
        /// 기획서 기준 기본 ScenarioPreset + EnvironmentProfile 에셋을 모두 생성/보정한다.
        /// Tools/DeepLight/World Map/Create Default Scenario Preset Assets
        /// </summary>
        [MenuItem("Tools/DeepLight/World Map/Create Default Scenario Preset Assets")]
        public static void CreateDefaultScenarioPresetAssets()
        {
            ResetSummary();
            LogSummary("===== Default Scenario Preset Assets 생성/보정 시작 =====");

            // 1. 폴더 생성
            EnsureFolder(SCENARIO_FOLDER);
            EnsureFolder(ENVIRONMENT_FOLDER);

            // 2. EnvironmentProfile 8개 생성 (ZoneEnvironmentProfileSO)
            var envHub = CreateOrPatchEnvironmentProfile("ZEP_Hub", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Shallow;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.Hub;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = 0f;
                so.FindProperty("maxDepth").floatValue = -120f;
                so.FindProperty("basePropDensity").floatValue = 0.25f;
                so.FindProperty("rockDensity").floatValue = 0.1f;
                so.FindProperty("vegetationDensity").floatValue = 0.35f;
                so.FindProperty("wreckDensity").floatValue = 0.05f;
                so.FindProperty("ruinDensity").floatValue = 0.02f;
                so.FindProperty("hazardDensity").floatValue = 0f;
                so.FindProperty("debugColor").colorValue = new Color(0.3f, 0.9f, 0.5f, 1f); // 연녹색
            });

            var envShallowWreck = CreateOrPatchEnvironmentProfile("ZEP_ShallowWreck", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Mid;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.ShallowWreck;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -50f;
                so.FindProperty("maxDepth").floatValue = -450f;
                so.FindProperty("basePropDensity").floatValue = 0.4f;
                so.FindProperty("rockDensity").floatValue = 0.25f;
                so.FindProperty("vegetationDensity").floatValue = 0.1f;
                so.FindProperty("wreckDensity").floatValue = 0.35f;
                so.FindProperty("ruinDensity").floatValue = 0.1f;
                so.FindProperty("hazardDensity").floatValue = 0.2f;
                so.FindProperty("debugColor").colorValue = new Color(0.7f, 0.4f, 0.15f, 1f); // 갈색/주황
            });

            var envResearchField = CreateOrPatchEnvironmentProfile("ZEP_ResearchField", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Deep;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.ResearchField;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -150f;
                so.FindProperty("maxDepth").floatValue = -700f;
                so.FindProperty("basePropDensity").floatValue = 0.45f;
                so.FindProperty("rockDensity").floatValue = 0.2f;
                so.FindProperty("vegetationDensity").floatValue = 0.05f;
                so.FindProperty("wreckDensity").floatValue = 0.15f;
                so.FindProperty("ruinDensity").floatValue = 0.3f;
                so.FindProperty("hazardDensity").floatValue = 0.3f;
                so.FindProperty("debugColor").colorValue = new Color(0.2f, 0.7f, 0.7f, 1f); // 청록
            });

            var envSealedNorth = CreateOrPatchEnvironmentProfile("ZEP_SealedNorth", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Forbidden;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.SealedNorth;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -500f;
                so.FindProperty("maxDepth").floatValue = -1400f;
                so.FindProperty("basePropDensity").floatValue = 0.5f;
                so.FindProperty("rockDensity").floatValue = 0.35f;
                so.FindProperty("vegetationDensity").floatValue = 0.02f;
                so.FindProperty("wreckDensity").floatValue = 0.1f;
                so.FindProperty("ruinDensity").floatValue = 0.25f;
                so.FindProperty("hazardDensity").floatValue = 0.5f;
                so.FindProperty("debugColor").colorValue = new Color(0.6f, 0.2f, 0.6f, 1f); // 보라/적색
            });

            var envOuterSea = CreateOrPatchEnvironmentProfile("ZEP_OuterSea", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Deep;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.OuterSea;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -100f;
                so.FindProperty("maxDepth").floatValue = -900f;
                so.FindProperty("basePropDensity").floatValue = 0.35f;
                so.FindProperty("rockDensity").floatValue = 0.3f;
                so.FindProperty("vegetationDensity").floatValue = 0.08f;
                so.FindProperty("wreckDensity").floatValue = 0.2f;
                so.FindProperty("ruinDensity").floatValue = 0.05f;
                so.FindProperty("hazardDensity").floatValue = 0.35f;
                so.FindProperty("debugColor").colorValue = new Color(0.1f, 0.2f, 0.6f, 1f); // 짙은 파랑
            });

            var envCanyon = CreateOrPatchEnvironmentProfile("ZEP_Canyon", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Deep;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.Canyon;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -300f;
                so.FindProperty("maxDepth").floatValue = -1000f;
                so.FindProperty("basePropDensity").floatValue = 0.3f;
                so.FindProperty("rockDensity").floatValue = 0.5f;
                so.FindProperty("vegetationDensity").floatValue = 0.03f;
                so.FindProperty("wreckDensity").floatValue = 0.08f;
                so.FindProperty("ruinDensity").floatValue = 0.15f;
                so.FindProperty("hazardDensity").floatValue = 0.4f;
                so.FindProperty("debugColor").colorValue = new Color(0.4f, 0.4f, 0.5f, 1f); // 회색/남색
            });

            var envAbyssExperiment = CreateOrPatchEnvironmentProfile("ZEP_AbyssExperiment", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Forbidden;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.AbyssExperiment;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -900f;
                so.FindProperty("maxDepth").floatValue = -1600f;
                so.FindProperty("basePropDensity").floatValue = 0.6f;
                so.FindProperty("rockDensity").floatValue = 0.3f;
                so.FindProperty("vegetationDensity").floatValue = 0f;
                so.FindProperty("wreckDensity").floatValue = 0.05f;
                so.FindProperty("ruinDensity").floatValue = 0.4f;
                so.FindProperty("hazardDensity").floatValue = 0.7f;
                so.FindProperty("debugColor").colorValue = new Color(0.6f, 0.05f, 0.05f, 1f); // 진한 빨강
            });

            var envOpenWater = CreateOrPatchEnvironmentProfile("ZEP_OpenWater", (so) =>
            {
                so.FindProperty("depthBand").enumValueIndex = (int)ZoneDepthBand.Mid;
                so.FindProperty("biomeType").enumValueIndex = (int)ZoneBiomeType.OpenWater;
                so.FindProperty("waterLevelY").floatValue = 0f;
                so.FindProperty("minDepth").floatValue = -100f;
                so.FindProperty("maxDepth").floatValue = -600f;
                so.FindProperty("basePropDensity").floatValue = 0.2f;
                so.FindProperty("rockDensity").floatValue = 0.15f;
                so.FindProperty("vegetationDensity").floatValue = 0.1f;
                so.FindProperty("wreckDensity").floatValue = 0.1f;
                so.FindProperty("ruinDensity").floatValue = 0.05f;
                so.FindProperty("hazardDensity").floatValue = 0.15f;
                so.FindProperty("debugColor").colorValue = new Color(0.3f, 0.3f, 0.4f, 1f); // 회색
            });

            // 3. ScenarioPreset 생성 (WMSP_DeepLight_Default)
            CreateOrPatchScenarioPreset("WMSP_DeepLight_Default", envOpenWater, (so) =>
            {
                // zoneRules 배열 설정
                var rulesProp = so.FindProperty("zoneRules");
                rulesProp.ClearArray();

                // Rule 1: 중앙 허브권 (E5, F5, E6, F6)
                AddZoneRule(rulesProp, "Hub Zone", new string[] { "E5", "F5", "E6", "F6" }, "Hub",
                    ZoneDepthBand.Shallow, ZoneBiomeType.Hub, 0f, -120f, 0.05f, envHub,
                    new Color(0.3f, 0.9f, 0.5f, 1f));

                // Rule 2: 서쪽 폐선권 (B5, B6, C5, C6, C7)
                AddZoneRule(rulesProp, "Shallow Wreck Zone", new string[] { "B5", "B6", "C5", "C6", "C7" }, "WestWreck",
                    ZoneDepthBand.Mid, ZoneBiomeType.ShallowWreck, -50f, -450f, 0.25f, envShallowWreck,
                    new Color(0.7f, 0.4f, 0.15f, 1f));

                // Rule 3: 동쪽 연구권 (H5, H6, I5, I6, I7)
                AddZoneRule(rulesProp, "Research Field Zone", new string[] { "H5", "H6", "I5", "I6", "I7" }, "EastResearch",
                    ZoneDepthBand.Deep, ZoneBiomeType.ResearchField, -150f, -700f, 0.45f, envResearchField,
                    new Color(0.2f, 0.7f, 0.7f, 1f));

                // Rule 4: 북쪽 봉인권 (E9, F9, E10, F10, G9, G10)
                AddZoneRule(rulesProp, "Sealed North Zone", new string[] { "E9", "F9", "E10", "F10", "G9", "G10" }, "SealedNorth",
                    ZoneDepthBand.Forbidden, ZoneBiomeType.SealedNorth, -500f, -1400f, 0.75f, envSealedNorth,
                    new Color(0.6f, 0.2f, 0.6f, 1f));

                // Rule 5: 남쪽 외해권 (E1, F1, E2, F2, G1)
                AddZoneRule(rulesProp, "Outer Sea Zone", new string[] { "E1", "F1", "E2", "F2", "G1" }, "OuterSea",
                    ZoneDepthBand.Deep, ZoneBiomeType.OuterSea, -100f, -900f, 0.55f, envOuterSea,
                    new Color(0.1f, 0.2f, 0.6f, 1f));

                // Rule 6: 북서 협곡권 (A8, A9, B8, B9)
                AddZoneRule(rulesProp, "Canyon Zone", new string[] { "A8", "A9", "B8", "B9" }, "Canyon",
                    ZoneDepthBand.Deep, ZoneBiomeType.Canyon, -300f, -1000f, 0.65f, envCanyon,
                    new Color(0.4f, 0.4f, 0.5f, 1f));

                // Rule 7: 북동 실험 심부권 (I9, J9, I10, J10)
                AddZoneRule(rulesProp, "Abyss Experiment Zone", new string[] { "I9", "J9", "I10", "J10" }, "AbyssExperiment",
                    ZoneDepthBand.Forbidden, ZoneBiomeType.AbyssExperiment, -900f, -1600f, 0.95f, envAbyssExperiment,
                    new Color(0.6f, 0.05f, 0.05f, 1f));

                // 기본값 설정
                so.FindProperty("defaultRiskLevel").floatValue = 0.35f;
                so.FindProperty("defaultDebugColor").colorValue = new Color(0.3f, 0.3f, 0.4f, 1f);

                // defaultRegionId 설정
                var regionIdProp = so.FindProperty("defaultRegionId");
                var idProp = regionIdProp.FindPropertyRelative("Id");
                if (idProp != null)
                {
                    idProp.stringValue = "OpenWater";
                }

                // defaultOpenWaterProfile 설정
                so.FindProperty("defaultOpenWaterProfile").objectReferenceValue = envOpenWater;
            });

            LogSummary("===== Default Scenario Preset Assets 생성/보정 완료 =====");
            PrintFinalSummary();
        }

        // ======================================================================
        //  폴더 생성
        // ======================================================================

        private static void EnsureFolder(string path)
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
        //  ZoneEnvironmentProfileSO 생성/보정
        // ======================================================================

        /// <summary>
        /// ZoneEnvironmentProfileSO 에셋을 생성하거나 기존 에셋을 보정한다.
        /// </summary>
        /// <param name="assetName">에셋 이름 (확장자 제외)</param>
        /// <param name="patchAction">값 설정 액션</param>
        /// <returns>생성/보정된 ZoneEnvironmentProfileSO 인스턴스</returns>
        private static ZoneEnvironmentProfileSO CreateOrPatchEnvironmentProfile(string assetName, System.Action<SerializedObject> patchAction)
        {
            string path = $"{ENVIRONMENT_FOLDER}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ZoneEnvironmentProfileSO>(path);
            if (existing != null)
            {
                LogFound($"EnvironmentProfile (이미 존재, 재사용): {assetName}");
                SerializedObject so = new SerializedObject(existing);
                patchAction(so);
                so.ApplyModifiedProperties();
                LogFixed($"EnvironmentProfile 값 보정: {assetName}");
                return existing;
            }

            var instance = ScriptableObject.CreateInstance<ZoneEnvironmentProfileSO>();
            SerializedObject soNew = new SerializedObject(instance);
            patchAction(soNew);
            soNew.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            LogCreated($"EnvironmentProfile: {assetName}");
            return instance;
        }

        // ======================================================================
        //  WorldMapScenarioPresetSO 생성/보정
        // ======================================================================

        /// <summary>
        /// WorldMapScenarioPresetSO 에셋을 생성하거나 기존 에셋을 보정한다.
        /// 기존 에셋이 있으면 zoneRules를 비우지 않고 재설정한다.
        /// </summary>
        /// <param name="assetName">에셋 이름 (확장자 제외)</param>
        /// <param name="defaultOpenWater">기본 개방 수역 프로필</param>
        /// <param name="patchAction">값 설정 액션 (zoneRules 포함)</param>
        private static void CreateOrPatchScenarioPreset(string assetName, ZoneEnvironmentProfileSO defaultOpenWater, System.Action<SerializedObject> patchAction)
        {
            string path = $"{SCENARIO_FOLDER}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WorldMapScenarioPresetSO>(path);
            if (existing != null)
            {
                LogFound($"ScenarioPreset (이미 존재, 재사용): {assetName}");
                SerializedObject so = new SerializedObject(existing);
                patchAction(so);
                so.ApplyModifiedProperties();
                LogFixed($"ScenarioPreset 값 보정: {assetName}");
                return;
            }

            var instance = ScriptableObject.CreateInstance<WorldMapScenarioPresetSO>();
            SerializedObject soNew = new SerializedObject(instance);
            patchAction(soNew);
            soNew.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            LogCreated($"ScenarioPreset: {assetName}");
        }

        // ======================================================================
        //  ZoneRule 추가 헬퍼
        // ======================================================================

        /// <summary>
        /// WorldMapScenarioZoneRule을 SerializedProperty 배열에 추가한다.
        /// WorldMapScenarioZoneRule은 [Serializable] 클래스이므로,
        /// SerializedProperty를 통해 각 필드에 접근하여 값을 설정한다.
        /// </summary>
        private static void AddZoneRule(
            SerializedProperty rulesProp,
            string ruleName,
            string[] zoneIdStrings,
            string regionIdString,
            ZoneDepthBand depthBand,
            ZoneBiomeType biomeType,
            float minDepth,
            float maxDepth,
            float baseRiskLevel,
            ZoneEnvironmentProfileSO envProfile,
            Color debugColor)
        {
            int idx = rulesProp.arraySize;
            rulesProp.arraySize++;
            SerializedProperty ruleProp = rulesProp.GetArrayElementAtIndex(idx);

            // ruleName
            ruleProp.FindPropertyRelative("ruleName").stringValue = ruleName;

            // zoneIds 배열 (ZoneId struct)
            SerializedProperty zoneIdsProp = ruleProp.FindPropertyRelative("zoneIds");
            zoneIdsProp.ClearArray();
            for (int i = 0; i < zoneIdStrings.Length; i++)
            {
                zoneIdsProp.arraySize++;
                SerializedProperty zoneIdProp = zoneIdsProp.GetArrayElementAtIndex(i);
                // ZoneId struct: Column(char), Row(int)
                if (ZoneId.TryParse(zoneIdStrings[i], out ZoneId parsedId))
                {
                    zoneIdProp.FindPropertyRelative("Column").intValue = parsedId.Column;
                    zoneIdProp.FindPropertyRelative("Row").intValue = parsedId.Row;
                }
            }

            // zoneIdStrings fallback 동기화
            SerializedProperty zoneIdStringsProp = ruleProp.FindPropertyRelative("zoneIdStrings");
            zoneIdStringsProp.ClearArray();
            for (int i = 0; i < zoneIdStrings.Length; i++)
            {
                zoneIdStringsProp.arraySize++;
                zoneIdStringsProp.GetArrayElementAtIndex(i).stringValue = zoneIdStrings[i];
            }

            // regionId (RegionId struct)
            SerializedProperty regionIdProp = ruleProp.FindPropertyRelative("regionId");
            regionIdProp.FindPropertyRelative("Id").stringValue = regionIdString;

            // regionIdString fallback
            ruleProp.FindPropertyRelative("regionIdString").stringValue = regionIdString;

            // depthBand (enum)
            ruleProp.FindPropertyRelative("depthBand").enumValueIndex = (int)depthBand;

            // biomeType (enum)
            ruleProp.FindPropertyRelative("biomeType").enumValueIndex = (int)biomeType;

            // minDepth, maxDepth
            ruleProp.FindPropertyRelative("minDepth").floatValue = minDepth;
            ruleProp.FindPropertyRelative("maxDepth").floatValue = maxDepth;

            // baseRiskLevel
            ruleProp.FindPropertyRelative("baseRiskLevel").floatValue = baseRiskLevel;

            // environmentProfile (object reference)
            ruleProp.FindPropertyRelative("environmentProfile").objectReferenceValue = envProfile;

            // debugColor
            ruleProp.FindPropertyRelative("debugColor").colorValue = debugColor;

            LogCreated($"ZoneRule 추가: {ruleName} ({string.Join(", ", zoneIdStrings)})");
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
            Debug.Log($"[WorldMapScenarioPresetCreator] 생성: {message}");
        }

        private static void LogFound(string message)
        {
            _foundCount++;
            _summaryLog.AppendLine($"  [발견] {message}");
            Debug.Log($"[WorldMapScenarioPresetCreator] 발견: {message}");
        }

        private static void LogFixed(string message)
        {
            _fixedCount++;
            _summaryLog.AppendLine($"  [보정] {message}");
            Debug.Log($"[WorldMapScenarioPresetCreator] 보정: {message}");
        }

        private static void LogWarning(string message)
        {
            _warningCount++;
            _summaryLog.AppendLine($"  [경고] {message}");
            Debug.LogWarning($"[WorldMapScenarioPresetCreator] 경고: {message}");
        }

        private static void PrintFinalSummary()
        {
            _summaryLog.AppendLine("");
            _summaryLog.AppendLine("===== WorldMap Scenario Preset Creator 결과 요약 =====");
            _summaryLog.AppendLine($"  생성: {_createdCount}개");
            _summaryLog.AppendLine($"  발견/재사용: {_foundCount}개");
            _summaryLog.AppendLine($"  보정/재연결: {_fixedCount}개");
            _summaryLog.AppendLine($"  경고: {_warningCount}개");
            _summaryLog.AppendLine("");

            Debug.Log(_summaryLog.ToString());
        }
    }
}
