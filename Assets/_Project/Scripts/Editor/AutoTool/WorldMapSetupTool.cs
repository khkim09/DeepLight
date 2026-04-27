using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Debug;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// 월드맵 시스템 에디터 자동화 도구 (Phase 1~10 통합, 최신 런타임 시그니처 기준).
    /// Quick Setup All 한 번으로 수동 기준선에 가까운 월드맵 테스트 환경을 idempotent하게 재구성한다.
    ///
    /// 설계 원칙:
    /// - 모든 생성/연결은 idempotent: 중복 생성보다 "찾아서 보정/재연결" 우선
    /// - 기존 UIRoot / 기존 게임 HUD는 절대 건드리지 않음
    /// - 월드맵용 UI는 모두 독립 Canvas / 독립 오브젝트에 생성
    /// - Console에 상세 결과 로그 출력
    /// - 자동화 불가 항목은 명확히 경고 로그로 남김
    /// </summary>
    public class WorldMapSetupTool : EditorWindow
    {
        // ===== 폴더 경로 상수 =====
        private const string CONFIG_FOLDER = "Assets/_Project/ScriptableObjects/World/Config";
        private const string ZONES_FOLDER = "Assets/_Project/ScriptableObjects/World/Zones";
        private const string UNLOCK_CONDITIONS_FOLDER = "Assets/_Project/ScriptableObjects/World/UnlockConditions";
        private const string DEBUG_FOLDER = "Assets/_Project/ScriptableObjects/World/Debug";
        private const string AMBIENT_FOLDER = "Assets/_Project/ScriptableObjects/World/AmbientProfiles";

        // ===== 결과 요약 =====
        private static readonly StringBuilder _summaryLog = new();
        private static int _createdCount;
        private static int _foundCount;
        private static int _fixedCount;
        private static int _warningCount;

        private Vector2 _scrollPosition;
        private bool _showFoldersSection = true;
        private bool _showConfigSection = true;
        private bool _showUnlockConditionsSection = true;
        private bool _showZonesSection = true;
        private bool _showSceneSection = true;
        private bool _showRuntimeTestSection = true;
        private bool _showDebugHUDSection = true;
        private bool _showAmbientSection = true;
        private bool _showInstallerSection = true;
        private bool _showZoneRootsSection = true;
        private bool _showHUDSection = true;
        private bool _showMiniGridSection = true;
        private bool _showZoneContentSection = true;

        // ===== 메뉴 항목 =====

        [MenuItem("Tools/World Map/Setup Tool")]
        public static void ShowWindow()
        {
            GetWindow<WorldMapSetupTool>("World Map Setup Tool");
        }

        [MenuItem("Tools/World Map/Quick Setup All")]
        public static void QuickSetupAll()
        {
            ResetSummary();
            LogSummary("===== Quick Setup All 시작 (A→B→C) =====");

            // A단계: Assets Only
            WorldMapAssetHelper.CreateUpdateAllAssets();

            // B단계: Scene Runtime Only
            WorldMapSceneHelper.CreateUpdateSceneRuntimeOnly();
            WorldMapSceneHelper.SetupAllSceneComponents();

            // C단계: HUD + MiniGrid
            WorldMapHUDHelper.CreateUpdateHUDOnly();
            WorldMapHUDHelper.CreateUpdateMiniGridOnly();

            // Installer 재연결 (모든 참조 최종 보정)
            SetupInstallerComponent();

            PrintFinalSummary();
            SelectMainRoot();
        }

        [MenuItem("Tools/World Map/Validate Full Manual Setup")]
        public static void ValidateFullManualSetup()
        {
            ResetSummary();
            LogSummary("===== Validate Full Manual Setup 시작 =====");
            ValidateAll();
            PrintFinalSummary();
        }

        [MenuItem("Tools/World Map/Rewire Installer References")]
        public static void RewireInstallerReferences()
        {
            ResetSummary();
            LogSummary("===== Installer 참조 재연결 =====");
            SetupInstallerComponent();
            PrintFinalSummary();
        }

        [MenuItem("Tools/World Map/Create/Update Assets Only")]
        public static void CreateUpdateAssetsOnly()
        {
            WorldMapAssetHelper.CreateUpdateAllAssets();
        }

        [MenuItem("Tools/World Map/Validate WorldMap Assets")]
        public static void ValidateWorldMapAssets()
        {
            WorldMapAssetHelper.ValidateAllAssets();
        }

        [MenuItem("Tools/World Map/Ping WorldMap Assets")]
        public static void PingWorldMapAssets()
        {
            WorldMapAssetHelper.PingAllAssets();
        }


        [MenuItem("Tools/World Map/Create/Update Scene Objects Only")]
        public static void CreateUpdateSceneObjectsOnly()
        {
            ResetSummary();
            LogSummary("===== 씬 오브젝트만 생성/갱신 =====");
            CreateFullSceneHierarchy();
            SetupAllComponents();
            PrintFinalSummary();
        }

        [MenuItem("Tools/World Map/Create/Update Scene Runtime Only (Phase 2)")]
        public static void CreateUpdateSceneRuntimeOnly()
        {
            WorldMapSceneHelper.CreateUpdateSceneRuntimeOnly();
        }

        [MenuItem("Tools/World Map/Setup All Scene Components (Phase 2)")]
        public static void SetupAllSceneComponents()
        {
            WorldMapSceneHelper.SetupAllSceneComponents();
        }

        [MenuItem("Tools/World Map/Validate Scene Runtime (Phase 2)")]
        public static void ValidateSceneRuntime()
        {
            WorldMapSceneHelper.ValidateSceneRuntime();
        }

        [MenuItem("Tools/World Map/Rewire Installer Runtime References (Phase 2)")]
        public static void RewireInstallerRuntimeReferences()
        {
            WorldMapSceneHelper.RewireInstallerRuntimeReferences();
        }

        [MenuItem("Tools/World Map/Create/Update HUD Only")]
        public static void CreateUpdateHUDOnly()
        {
            ResetSummary();
            LogSummary("===== HUD만 생성/갱신 =====");
            CreateCurrentZoneHUDObjects();
            PrintFinalSummary();
        }

        [MenuItem("Tools/World Map/Create/Update MiniGrid Only")]
        public static void CreateUpdateMiniGridOnly()
        {
            ResetSummary();
            LogSummary("===== MiniGrid만 생성/갱신 =====");
            CreateMiniGridObjects();
            PrintFinalSummary();
        }

        [MenuItem("Tools/World Map/Create/Update ZoneRoots Only")]
        public static void CreateUpdateZoneRootsOnly()
        {
            ResetSummary();
            LogSummary("===== ZoneRoots만 생성/갱신 =====");
            CreateZoneRootObjects();
            SetupZoneRootComponents();
            PrintFinalSummary();
        }

        [MenuItem("Tools/World Map/Create/Update Zone Content Samples Only")]
        public static void CreateUpdateZoneContentSamplesOnly()
        {
            ResetSummary();
            LogSummary("===== Zone Content 샘플만 생성/갱신 =====");
            CreateZoneContentControllerObject();
            CreateSampleBoundContent();
            PrintFinalSummary();
        }

        // ===== Phase 3: HUD + MiniGrid 전용 메뉴 =====

        [MenuItem("Tools/World Map/Create/Update HUD Only (Phase 3)")]
        public static void CreateUpdateHUDOnlyPhase3()
        {
            WorldMapHUDHelper.CreateUpdateHUDOnly();
        }

        [MenuItem("Tools/World Map/Create HUD UI Elements Only (Phase 3)")]
        public static void CreateHUDUIElementsOnly()
        {
            WorldMapHUDHelper.CreateHUDUIElementsOnly();
        }

        [MenuItem("Tools/World Map/Create/Update MiniGrid Only (Phase 3)")]
        public static void CreateUpdateMiniGridOnlyPhase3()
        {
            WorldMapHUDHelper.CreateUpdateMiniGridOnly();
        }

        [MenuItem("Tools/World Map/Validate HUD & MiniGrid (Phase 3)")]
        public static void ValidateHUDAndMiniGrid()
        {
            WorldMapHUDHelper.ValidateHUDAndMiniGrid();
        }

        [MenuItem("Tools/World Map/Setup MiniGrid Submarine-Relative (Phase 3)")]
        public static void SetupMiniGridSubmarineRelative()
        {
            WorldMapHUDHelper.SetupMiniGridSubmarineRelative();
        }

        // ===== OnGUI =====

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("World Map System Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Quick Setup All (Recommended)", GUILayout.Height(40)))
            {
                QuickSetupAll();
            }

            EditorGUILayout.HelpBox("이 도구는 월드맵 시스템에 필요한 모든 폴더, ScriptableObject, 씬 오브젝트를 idempotent하게 생성/재연결합니다.", MessageType.Info);
            EditorGUILayout.Space(20);

            DrawFoldersSection();
            DrawConfigSection();
            DrawUnlockConditionsSection();
            DrawZonesSection();
            DrawSceneSection();
            DrawRuntimeTestSection();
            DrawDebugHUDSection();
            DrawAmbientSection();
            DrawInstallerSection();
            DrawZoneRootsSection();
            DrawHUDSection();
            DrawMiniGridSection();
            DrawZoneContentSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawFoldersSection()
        {
            _showFoldersSection = EditorGUILayout.Foldout(_showFoldersSection, "1. 폴더 생성");
            if (_showFoldersSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("모든 폴더 생성")) CreateAllFolders();
                EditorGUILayout.LabelField("생성할 폴더:");
                EditorGUILayout.LabelField($"  \u2022 {CONFIG_FOLDER}");
                EditorGUILayout.LabelField($"  \u2022 {ZONES_FOLDER}");
                EditorGUILayout.LabelField($"  \u2022 {UNLOCK_CONDITIONS_FOLDER}");
                EditorGUILayout.LabelField($"  \u2022 {DEBUG_FOLDER}");
                EditorGUILayout.LabelField($"  \u2022 {AMBIENT_FOLDER}");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawConfigSection()
        {
            _showConfigSection = EditorGUILayout.Foldout(_showConfigSection, "2. WorldMapConfig 생성");
            if (_showConfigSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("WorldMapConfig_Main 생성")) CreateAllConfigAssets();
                EditorGUILayout.HelpBox("10x10 그리드, 400 유닛 존 크기, 월드 경계 -2000~2000", MessageType.Info);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawUnlockConditionsSection()
        {
            _showUnlockConditionsSection = EditorGUILayout.Foldout(_showUnlockConditionsSection, "3. 해금 조건 세트 생성");
            if (_showUnlockConditionsSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("모든 해금 조건 세트 생성")) CreateAllUnlockConditionSets();
                EditorGUILayout.LabelField("\u2022 UCS_Free - 빈 조건");
                EditorGUILayout.LabelField("\u2022 UCS_ResearchZone_Test - 연구 구역 조건");
                EditorGUILayout.LabelField("\u2022 UCS_SealedNorth_Test - 봉인 북부 조건");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawZonesSection()
        {
            _showZonesSection = EditorGUILayout.Foldout(_showZonesSection, "4. 존 데이터 & Ambient Profile 에셋 생성");
            if (_showZonesSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("모든 존 데이터 에셋 생성")) CreateAllZoneDataAssets();
                if (GUILayout.Button("모든 Ambient Profile 생성")) CreateAllAmbientProfiles();
                if (GUILayout.Button("AmbientProfile \u2192 ZoneData 연결")) AssignAmbientProfilesToZones();
                EditorGUILayout.LabelField("\u2022 ZoneData_DefaultFallback, Hub_E5, Research_I6, Sealed_F10");
                EditorGUILayout.LabelField("\u2022 Ambient_Hub_Safe, Research_Tense, Sealed_Danger, OutOfBounds_Default");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawSceneSection()
        {
            _showSceneSection = EditorGUILayout.Foldout(_showSceneSection, "5. 씬 계층 구조 생성");
            if (_showSceneSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("전체 씬 계층 구조 생성")) CreateFullSceneHierarchy();
                EditorGUILayout.LabelField("_WorldMap_Manual 루트부터 모든 하위 오브젝트 생성");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawRuntimeTestSection()
        {
            _showRuntimeTestSection = EditorGUILayout.Foldout(_showRuntimeTestSection, "6. RuntimeTest 설정");
            if (_showRuntimeTestSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("RuntimeTest 컴포넌트 설정")) SetupRuntimeTestComponent();
                EditorGUILayout.LabelField("WorldMapRuntimeTestRoot에 WorldMapRuntimeTest 부착 및 설정");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawDebugHUDSection()
        {
            _showDebugHUDSection = EditorGUILayout.Foldout(_showDebugHUDSection, "7. Debug HUD 설정");
            if (_showDebugHUDSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Debug HUD 컴포넌트 설정")) SetupDebugHUDComponent();
                EditorGUILayout.LabelField("WorldMapDebugHUD에 WorldMapDebugHUDRuntime 부착 및 설정");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawAmbientSection()
        {
            _showAmbientSection = EditorGUILayout.Foldout(_showAmbientSection, "8. Ambient Reaction 설정");
            if (_showAmbientSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Ambient Reaction 컴포넌트 설정")) SetupAmbientReactionComponent();
                EditorGUILayout.LabelField("WorldMapAmbientReactionRoot에 WorldMapAmbientReactionController 부착");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawInstallerSection()
        {
            _showInstallerSection = EditorGUILayout.Foldout(_showInstallerSection, "9. Runtime Installer 설정");
            if (_showInstallerSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Installer 컴포넌트 설정")) SetupInstallerComponent();
                EditorGUILayout.LabelField("WorldMapRuntimeInstaller에 모든 참조 연결");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawZoneRootsSection()
        {
            _showZoneRootsSection = EditorGUILayout.Foldout(_showZoneRootsSection, "10. ZoneRoots 생성");
            if (_showZoneRootsSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("ZoneRoots 생성")) CreateZoneRootObjects();
                if (GUILayout.Button("ZoneRoot 컴포넌트 설정")) SetupZoneRootComponents();
                EditorGUILayout.LabelField("3개 ZoneRoot + 7개 child root + Trigger 생성");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawHUDSection()
        {
            _showHUDSection = EditorGUILayout.Foldout(_showHUDSection, "11. Current Zone HUD 생성");
            if (_showHUDSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("HUD 생성")) CreateCurrentZoneHUDObjects();
                EditorGUILayout.LabelField("독립 Canvas + WorldMapCurrentZoneHUDController + View + 5개 UI 요소");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawMiniGridSection()
        {
            _showMiniGridSection = EditorGUILayout.Foldout(_showMiniGridSection, "12. MiniGrid 생성");
            if (_showMiniGridSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("MiniGrid 생성")) CreateMiniGridObjects();
                EditorGUILayout.LabelField("독립 Canvas + WorldMapMiniGridController + View + 9개 셀");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }
        }

        private void DrawZoneContentSection()
        {
            _showZoneContentSection = EditorGUILayout.Foldout(_showZoneContentSection, "13. Zone Content Controller & 샘플 바인딩");
            if (_showZoneContentSection)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("ZoneContentController 생성")) CreateZoneContentControllerObject();
                if (GUILayout.Button("샘플 바인딩 생성")) CreateSampleBoundContent();
                EditorGUILayout.LabelField("4개 샘플 WorldMapZoneContentBinding 생성");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
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
            Debug.Log($"[WorldMapSetupTool] 생성: {message}");
        }

        private static void LogFound(string message)
        {
            _foundCount++;
            _summaryLog.AppendLine($"  [발견] {message}");
            Debug.Log($"[WorldMapSetupTool] 발견: {message}");
        }

        private static void LogFixed(string message)
        {
            _fixedCount++;
            _summaryLog.AppendLine($"  [보정] {message}");
            Debug.Log($"[WorldMapSetupTool] 보정: {message}");
        }

        private static void LogWarning(string message)
        {
            _warningCount++;
            _summaryLog.AppendLine($"  [경고] {message}");
            Debug.LogWarning($"[WorldMapSetupTool] 경고: {message}");
        }

        private static void PrintFinalSummary()
        {
            _summaryLog.AppendLine("");
            _summaryLog.AppendLine("===== Quick Setup All 결과 요약 =====");
            _summaryLog.AppendLine($"  생성: {_createdCount}개");
            _summaryLog.AppendLine($"  발견/재사용: {_foundCount}개");
            _summaryLog.AppendLine($"  보정/재연결: {_fixedCount}개");
            _summaryLog.AppendLine($"  경고: {_warningCount}개");
            _summaryLog.AppendLine("");

            if (_warningCount > 0)
            {
                _summaryLog.AppendLine("⚠ 완전 자동화 불가 / 수동 확인 필요 항목:");
                _summaryLog.AppendLine("  - TargetCamera: Camera.main을 사용하므로 별도 할당 불필요");
                _summaryLog.AppendLine("  - WorldMapBootstrapBridge: 선택적 오브젝트, 필요 시 수동 생성");
                _summaryLog.AppendLine("  - ZoneContentController의 worldMapConfig: Initialize()에서 런타임 주입");
                _summaryLog.AppendLine("  - MiniGrid 셀 Image/Text: View의 cellImages/cellLabels 배열은 수동 할당 필요");
                _summaryLog.AppendLine("    → WorldMapMiniGridView를 선택하고 9개 Image + 9개 TMP를 배열에 드래그");
                _summaryLog.AppendLine("  - WorldMapRuntimeTest.zoneRoots: ZoneRoot 배열은 수동 할당 필요");
                _summaryLog.AppendLine("    → WorldMapRuntimeTest를 선택하고 3개 ZoneRoot를 배열에 드래그");
            }

            Debug.Log(_summaryLog.ToString());
        }

        private static void SelectMainRoot()
        {
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root != null)
            {
                Selection.activeGameObject = root;
                EditorGUIUtility.PingObject(root);
            }
            else
            {
                var config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>($"{CONFIG_FOLDER}/WorldMapConfig_Main.asset");
                if (config != null)
                {
                    Selection.activeObject = config;
                    EditorGUIUtility.PingObject(config);
                }
            }
        }

        // ======================================================================
        //  1. 폴더 생성
        // ======================================================================

        public static void CreateAllFolders()
        {
            CreateFolder(CONFIG_FOLDER);
            CreateFolder(ZONES_FOLDER);
            CreateFolder(UNLOCK_CONDITIONS_FOLDER);
            CreateFolder(DEBUG_FOLDER);
            CreateFolder(AMBIENT_FOLDER);
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
        //  2. WorldMapConfigSO 생성
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
        //  3. UnlockConditionSetSO 생성
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
        //  4. ZoneAmbientProfileSO 생성
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
        //  5. ZoneDataSO 생성
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
        //  6. AmbientProfile -> ZoneData 연결

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
        //  7. 씬 계층 구조 생성
        // ======================================================================

        public static void CreateFullSceneHierarchy()
        {
            // _WorldMap_Manual root
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                root = new GameObject("_WorldMap_Manual");
                LogCreated("_WorldMap_Manual 루트");
            }
            else
            {
                LogFound("_WorldMap_Manual 루트 (이미 존재)");
            }

            // WorldMap_RuntimeRoot
            GameObject runtimeRoot = FindOrCreateChild(root, "WorldMap_RuntimeRoot");
            FindOrCreateChild(runtimeRoot, "WorldMapTrackedTransform");
            FindOrCreateChild(runtimeRoot, "WorldMapRuntimeInstaller");
            FindOrCreateChild(runtimeRoot, "WorldMapRuntimeTestRoot");
            FindOrCreateChild(runtimeRoot, "WorldMapDebugHUD");
            FindOrCreateChild(runtimeRoot, "WorldMapAmbientReactionRoot");
            // WorldMapBootstrapBridge is optional - skip

            // ZoneRoots
            GameObject zoneRoots = FindOrCreateChild(root, "ZoneRoots");

            // HUD Canvas
            CreateCurrentZoneHUDObjects();

            // MiniGrid Canvas
            CreateMiniGridObjects();

            LogSummary("[씬 계층] 전체 계층 구조 생성 완료.");
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                LogFound($"오브젝트 (이미 존재): {parent.name}/{name}");
                return existing.gameObject;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            LogCreated($"오브젝트: {parent.name}/{name}");
            return child;
        }

        // ======================================================================
        //  8. 모든 컴포넌트 설정 (SetupAllComponents)
        // ======================================================================

        public static void SetupAllComponents()
        {
            SetupTrackedTransform();
            SetupRuntimeTestComponent();
            SetupDebugHUDComponent();
            SetupAmbientReactionComponent();
            SetupZoneRootComponents();
            LogSummary("[컴포넌트] 모든 컴포넌트 설정 완료.");
        }

        // ======================================================================
        //  8a. WorldMapTrackedTransform 위치 설정
        // ======================================================================

        private static void SetupTrackedTransform()
        {
            GameObject runtimeRoot = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot");
            if (runtimeRoot == null) return;

            Transform tracked = runtimeRoot.transform.Find("WorldMapTrackedTransform");
            if (tracked != null)
            {
                tracked.localPosition = new Vector3(-200f, 0f, -200f);
                LogFixed("WorldMapTrackedTransform 위치 = (-200, 0, -200)");
            }
        }

        // ======================================================================
        //  8b. WorldMapRuntimeTest 설정
        // ======================================================================

        public static void SetupRuntimeTestComponent()
        {
            GameObject runtimeRoot = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot");
            if (runtimeRoot == null) return;

            Transform testRootTransform = runtimeRoot.transform.Find("WorldMapRuntimeTestRoot");
            if (testRootTransform == null) return;
            GameObject testRoot = testRootTransform.gameObject;

            var testComp = testRoot.GetComponent<WorldMapRuntimeTest>();
            if (testComp == null)
            {
                testComp = testRoot.AddComponent<WorldMapRuntimeTest>();
                LogCreated("WorldMapRuntimeTest 컴포넌트");
            }
            else
            {
                LogFound("WorldMapRuntimeTest 컴포넌트 (이미 존재)");
            }

            SerializedObject so = new SerializedObject(testComp);
            var config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>($"{CONFIG_FOLDER}/WorldMapConfig_Main.asset");
            if (config != null) so.FindProperty("worldMapConfig").objectReferenceValue = config;

            // ZoneDataAssets array
            var zoneDataArray = so.FindProperty("zoneDataAssets");
            if (zoneDataArray != null)
            {
                string[] zoneNames = { "ZoneData_DefaultFallback", "ZoneData_Hub_E5", "ZoneData_Research_I6", "ZoneData_Sealed_F10" };
                zoneDataArray.ClearArray();
                for (int i = 0; i < zoneNames.Length; i++)
                {
                    var zd = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/{zoneNames[i]}.asset");
                    if (zd != null)
                    {
                        zoneDataArray.arraySize++;
                        zoneDataArray.GetArrayElementAtIndex(i).objectReferenceValue = zd;
                    }
                }
            }

            // TrackedTransform (Transform reference for WorldMapRuntimeInstaller)
            Transform trackedTransform = runtimeRoot.transform.Find("WorldMapTrackedTransform");
            if (trackedTransform != null)
            {
                so.FindProperty("trackedTransform").objectReferenceValue = trackedTransform;
            }


            // Mock values
            so.FindProperty("useMockProgress").boolValue = true;
            so.FindProperty("testUpgradeId").stringValue = "upgrade_battery_mk2";
            so.FindProperty("testLogId").stringValue = "log_intro_01";
            so.FindProperty("testNarrativeFlag").stringValue = "mara_research_analysis_done";
            so.FindProperty("mockHullTier").intValue = 2;
            so.FindProperty("mockDepthLevel").intValue = 3;
            so.FindProperty("mockSensorAccuracy").floatValue = 0.8f;
            so.FindProperty("mockLogCount").intValue = 5;
            so.FindProperty("updateEveryFrame").boolValue = false;
            so.FindProperty("logEventsToConsole").boolValue = true;

            // NOTE: validateZoneRoots and logZoneRootMismatches do not exist on WorldMapRuntimeTest
            // The actual script has zoneRoots array instead
            LogWarning("WorldMapRuntimeTest: zoneRoots 배열은 수동 할당 필요 (3개 ZoneRoot 드래그)");

            so.ApplyModifiedProperties();
            LogFixed("WorldMapRuntimeTest 값 설정 완료.");
        }

        // ======================================================================
        //  8c. WorldMapDebugHUDRuntime 설정
        // ======================================================================

        public static void SetupDebugHUDComponent()
        {
            GameObject runtimeRoot = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot");
            if (runtimeRoot == null) return;

            Transform debugHUDTransform = runtimeRoot.transform.Find("WorldMapDebugHUD");
            if (debugHUDTransform == null) return;
            GameObject debugHUD = debugHUDTransform.gameObject;

            var debugComp = debugHUD.GetComponent<WorldMapDebugHUDRuntime>();
            if (debugComp == null)
            {
                debugComp = debugHUD.AddComponent<WorldMapDebugHUDRuntime>();
                LogCreated("WorldMapDebugHUDRuntime 컴포넌트");
            }
            else
            {
                LogFound("WorldMapDebugHUDRuntime 컴포넌트 (이미 존재)");
            }

            SerializedObject so = new SerializedObject(debugComp);

            // RuntimeTestSource
            Transform testRootTransform = runtimeRoot.transform.Find("WorldMapRuntimeTestRoot");
            if (testRootTransform != null)
            {
                var testComp = testRootTransform.GetComponent<WorldMapRuntimeTest>();
                if (testComp != null)
                    so.FindProperty("runtimeTestSource").objectReferenceValue = testComp;
            }

            so.FindProperty("updateEveryFrame").boolValue = false;
            so.FindProperty("updateInterval").floatValue = 0.5f;
            // toggleKey is KeyCode enum - F9 = 292
            so.FindProperty("toggleKey").enumValueIndex = 292; // KeyCode.F9
            so.FindProperty("testZoneId").stringValue = "I6";

            so.ApplyModifiedProperties();
            LogFixed("WorldMapDebugHUDRuntime 값 설정 완료.");
        }

        // ======================================================================
        //  8d. WorldMapAmbientReactionController 설정
        // ======================================================================

        public static void SetupAmbientReactionComponent()
        {
            GameObject runtimeRoot = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot");
            if (runtimeRoot == null) return;

            Transform ambientTransform = runtimeRoot.transform.Find("WorldMapAmbientReactionRoot");
            if (ambientTransform == null) return;
            GameObject ambientRoot = ambientTransform.gameObject;

            var ambientComp = ambientRoot.GetComponent<WorldMapAmbientReactionController>();
            if (ambientComp == null)
            {
                ambientComp = ambientRoot.AddComponent<WorldMapAmbientReactionController>();
                LogCreated("WorldMapAmbientReactionController 컴포넌트");
            }
            else
            {
                LogFound("WorldMapAmbientReactionController 컴포넌트 (이미 존재)");
            }

            SerializedObject so = new SerializedObject(ambientComp);
            var ambientOob = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_OutOfBounds_Default.asset");
            if (ambientOob != null)
                so.FindProperty("outOfBoundsProfile").objectReferenceValue = ambientOob;

            so.ApplyModifiedProperties();
            LogFixed("WorldMapAmbientReactionController 값 설정 완료.");
        }

        // ======================================================================
        //  9. ZoneRoots 생성
        // ======================================================================

        public static void CreateZoneRootObjects()
        {
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                LogWarning("_WorldMap_Manual 루트가 없습니다. 먼저 씬 계층을 생성하세요.");
                return;
            }

            Transform zoneRootsTransform = root.transform.Find("ZoneRoots");
            GameObject zoneRoots;
            if (zoneRootsTransform == null)
            {
                zoneRoots = new GameObject("ZoneRoots");
                zoneRoots.transform.SetParent(root.transform, false);
                LogCreated("ZoneRoots 폴더");
            }
            else
            {
                zoneRoots = zoneRootsTransform.gameObject;
                LogFound("ZoneRoots 폴더 (이미 존재)");
            }

            CreateSingleZoneRoot(zoneRoots, "ZoneRoot_Hub_E5", new Vector3(-200f, 0f, -200f));
            CreateSingleZoneRoot(zoneRoots, "ZoneRoot_Research_I6", new Vector3(1400f, 0f, 200f));
            CreateSingleZoneRoot(zoneRoots, "ZoneRoot_Sealed_F10", new Vector3(200f, 0f, 1800f));

            LogSummary("[ZoneRoots] 3개 ZoneRoot 생성 완료.");
        }

        private static void CreateSingleZoneRoot(GameObject parent, string name, Vector3 position)
        {
            Transform existing = parent.transform.Find(name);
            GameObject zoneRoot;
            if (existing == null)
            {
                zoneRoot = new GameObject(name);
                zoneRoot.transform.SetParent(parent.transform, false);
                LogCreated($"ZoneRoot: {name}");
            }
            else
            {
                zoneRoot = existing.gameObject;
                LogFound($"ZoneRoot (이미 존재): {name}");
            }

            zoneRoot.transform.localPosition = position;

            // 7 child roots
            string[] childNames = { "Geometry", "Props", "HarvestSpawns", "LogSpawns", "Hazards", "Triggers", "DebugGizmos" };
            foreach (string childName in childNames)
            {
                Transform childTransform = zoneRoot.transform.Find(childName);
                if (childTransform == null)
                {
                    GameObject child = new GameObject(childName);
                    child.transform.SetParent(zoneRoot.transform, false);
                    LogCreated($"  Child: {name}/{childName}");
                }
                else
                {
                    LogFound($"  Child (이미 존재): {name}/{childName}");
                }
            }

            // Trigger under Triggers
            Transform triggersTransform = zoneRoot.transform.Find("Triggers");
            if (triggersTransform != null)
            {
                string triggerName = $"{name}_Trigger";
                Transform triggerTransform = triggersTransform.Find(triggerName);
                GameObject trigger;
                if (triggerTransform == null)
                {
                    trigger = new GameObject(triggerName);
                    trigger.transform.SetParent(triggersTransform, false);
                    LogCreated($"  Trigger: {name}/Triggers/{triggerName}");
                }
                else
                {
                    trigger = triggerTransform.gameObject;
                    LogFound($"  Trigger (이미 존재): {name}/Triggers/{triggerName}");
                }

                // BoxCollider
                var collider = trigger.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = trigger.AddComponent<BoxCollider>();
                    LogCreated($"  BoxCollider: {triggerName}");
                }
                collider.isTrigger = true;
                collider.center = Vector3.zero;
                collider.size = new Vector3(400f, 50f, 400f);

                // ZoneTriggerRelay
                var relay = trigger.GetComponent<ZoneTriggerRelay>();
                if (relay == null)
                {
                    relay = trigger.AddComponent<ZoneTriggerRelay>();
                    LogCreated($"  ZoneTriggerRelay: {triggerName}");
                }
                SerializedObject relaySo = new SerializedObject(relay);
                relaySo.FindProperty("zoneRoot").objectReferenceValue = zoneRoot;
                relaySo.ApplyModifiedProperties();
            }
        }

        // ======================================================================
        //  10. ZoneRoot 컴포넌트 설정
        // ======================================================================

        public static void SetupZoneRootComponents()
        {
            GameObject zoneRoots = GameObject.Find("_WorldMap_Manual/ZoneRoots");
            if (zoneRoots == null) return;

            SetupSingleZoneRootComponent(zoneRoots.transform, "ZoneRoot_Hub_E5", "ZoneData_Hub_E5");
            SetupSingleZoneRootComponent(zoneRoots.transform, "ZoneRoot_Research_I6", "ZoneData_Research_I6");
            SetupSingleZoneRootComponent(zoneRoots.transform, "ZoneRoot_Sealed_F10", "ZoneData_Sealed_F10");

            LogSummary("[ZoneRoot 컴포넌트] 3개 ZoneRoot 설정 완료.");
        }

        private static void SetupSingleZoneRootComponent(Transform parent, string zoneRootName, string zoneDataAssetName)
        {
            Transform zoneRootTransform = parent.Find(zoneRootName);
            if (zoneRootTransform == null) return;
            GameObject zoneRoot = zoneRootTransform.gameObject;

            var zoneRootComp = zoneRoot.GetComponent<ZoneRoot>();
            if (zoneRootComp == null)
            {
                zoneRootComp = zoneRoot.AddComponent<ZoneRoot>();
                LogCreated($"ZoneRoot 컴포넌트: {zoneRootName}");
            }
            else
            {
                LogFound($"ZoneRoot 컴포넌트 (이미 존재): {zoneRootName}");
            }

            SerializedObject so = new SerializedObject(zoneRootComp);
            var zoneData = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/{zoneDataAssetName}.asset");
            if (zoneData != null)
                so.FindProperty("zoneData").objectReferenceValue = zoneData;
            so.ApplyModifiedProperties();
        }

        // ======================================================================
        //  11. Current Zone HUD 생성
        // ======================================================================

        public static void CreateCurrentZoneHUDObjects()
        {
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                LogWarning("_WorldMap_Manual 루트가 없습니다. 먼저 씬 계층을 생성하세요.");
                return;
            }

            // WorldMapHUDCanvas
            Transform canvasTransform = root.transform.Find("WorldMapHUDCanvas");
            GameObject canvas;
            if (canvasTransform == null)
            {
                canvas = new GameObject("WorldMapHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.transform.SetParent(root.transform, false);
                LogCreated("WorldMapHUDCanvas");
            }
            else
            {
                canvas = canvasTransform.gameObject;
                LogFound("WorldMapHUDCanvas (이미 존재)");
            }

            // Canvas settings
            Canvas c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 500;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // WorldMapCurrentZoneHUD
            Transform hudTransform = canvas.transform.Find("WorldMapCurrentZoneHUD");
            GameObject hud;
            if (hudTransform == null)
            {
                hud = new GameObject("WorldMapCurrentZoneHUD", typeof(RectTransform));
                hud.transform.SetParent(canvas.transform, false);
                LogCreated("WorldMapCurrentZoneHUD");
            }
            else
            {
                hud = hudTransform.gameObject;
                LogFound("WorldMapCurrentZoneHUD (이미 존재)");
            }

            // RectTransform
            RectTransform hudRt = hud.GetComponent<RectTransform>();
            hudRt.anchorMin = new Vector2(0.5f, 1f);
            hudRt.anchorMax = new Vector2(0.5f, 1f);
            hudRt.pivot = new Vector2(0.5f, 1f);
            hudRt.anchoredPosition = new Vector2(0f, -20f);
            hudRt.sizeDelta = new Vector2(360f, 120f);

            // Controller
            var controller = hud.GetComponent<WorldMapCurrentZoneHUDController>();
            if (controller == null)
            {
                controller = hud.AddComponent<WorldMapCurrentZoneHUDController>();
                LogCreated("WorldMapCurrentZoneHUDController");
            }
            else
            {
                LogFound("WorldMapCurrentZoneHUDController (이미 존재)");
            }

            // View child
            Transform viewTransform = hud.transform.Find("View");
            GameObject view;
            if (viewTransform == null)
            {
                view = new GameObject("View", typeof(RectTransform));
                view.transform.SetParent(hud.transform, false);
                LogCreated("WorldMapCurrentZoneHUD/View");
            }
            else
            {
                view = viewTransform.gameObject;
                LogFound("WorldMapCurrentZoneHUD/View (이미 존재)");
            }

            RectTransform viewRt = view.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;

            // View component
            var viewComp = view.GetComponent<WorldMapCurrentZoneHUDView>();
            if (viewComp == null)
            {
                viewComp = view.AddComponent<WorldMapCurrentZoneHUDView>();
                LogCreated("WorldMapCurrentZoneHUDView");
            }
            else
            {
                LogFound("WorldMapCurrentZoneHUDView (이미 존재)");
            }

            // Create UI elements under View
            // RiskTintImage
            GameObject riskTint = FindOrCreateUIChild(view, "RiskTintImage", typeof(Image));
            Image riskTintImg = riskTint.GetComponent<Image>();
            riskTintImg.color = new Color(0f, 0f, 0f, 0.35f);
            StretchRect(riskTint.GetComponent<RectTransform>());

            // ZoneNameText
            GameObject zoneName = FindOrCreateUIChild(view, "ZoneNameText", typeof(TextMeshProUGUI));
            TextMeshProUGUI zoneNameTmp = zoneName.GetComponent<TextMeshProUGUI>();
            zoneNameTmp.fontSize = 24;
            zoneNameTmp.alignment = TextAlignmentOptions.Center;
            zoneNameTmp.text = "Zone Name";
            RectTransform zoneNameRt = zoneName.GetComponent<RectTransform>();
            zoneNameRt.anchorMin = new Vector2(0.5f, 1f);
            zoneNameRt.anchorMax = new Vector2(0.5f, 1f);
            zoneNameRt.pivot = new Vector2(0.5f, 1f);
            zoneNameRt.anchoredPosition = new Vector2(0f, -10f);
            zoneNameRt.sizeDelta = new Vector2(340f, 28f);

            // RegionNameText
            GameObject regionName = FindOrCreateUIChild(view, "RegionNameText", typeof(TextMeshProUGUI));
            TextMeshProUGUI regionNameTmp = regionName.GetComponent<TextMeshProUGUI>();
            regionNameTmp.fontSize = 18;
            regionNameTmp.alignment = TextAlignmentOptions.Center;
            regionNameTmp.text = "Region";
            RectTransform regionNameRt = regionName.GetComponent<RectTransform>();
            regionNameRt.anchorMin = new Vector2(0.5f, 1f);
            regionNameRt.anchorMax = new Vector2(0.5f, 1f);
            regionNameRt.pivot = new Vector2(0.5f, 1f);
            regionNameRt.anchoredPosition = new Vector2(0f, -40f);
            regionNameRt.sizeDelta = new Vector2(340f, 24f);

            // RiskStatusText
            GameObject riskStatus = FindOrCreateUIChild(view, "RiskStatusText", typeof(TextMeshProUGUI));
            TextMeshProUGUI riskStatusTmp = riskStatus.GetComponent<TextMeshProUGUI>();
            riskStatusTmp.fontSize = 18;
            riskStatusTmp.alignment = TextAlignmentOptions.Center;
            riskStatusTmp.text = "Risk: Safe";
            RectTransform riskStatusRt = riskStatus.GetComponent<RectTransform>();
            riskStatusRt.anchorMin = new Vector2(0.5f, 1f);
            riskStatusRt.anchorMax = new Vector2(0.5f, 1f);
            riskStatusRt.pivot = new Vector2(0.5f, 1f);
            riskStatusRt.anchoredPosition = new Vector2(0f, -68f);
            riskStatusRt.sizeDelta = new Vector2(340f, 24f);

            // DiscoveryStatusText
            GameObject discoveryStatus = FindOrCreateUIChild(view, "DiscoveryStatusText", typeof(TextMeshProUGUI));
            TextMeshProUGUI discoveryStatusTmp = discoveryStatus.GetComponent<TextMeshProUGUI>();
            discoveryStatusTmp.fontSize = 16;
            discoveryStatusTmp.alignment = TextAlignmentOptions.Center;
            discoveryStatusTmp.text = "Status: Undiscovered";
            RectTransform discoveryStatusRt = discoveryStatus.GetComponent<RectTransform>();
            discoveryStatusRt.anchorMin = new Vector2(0.5f, 1f);
            discoveryStatusRt.anchorMax = new Vector2(0.5f, 1f);
            discoveryStatusRt.pivot = new Vector2(0.5f, 1f);
            discoveryStatusRt.anchoredPosition = new Vector2(0f, -94f);
            discoveryStatusRt.sizeDelta = new Vector2(340f, 22f);

            // Wire View references
            SerializedObject viewSo = new SerializedObject(viewComp);
            viewSo.FindProperty("riskTintImage").objectReferenceValue = riskTintImg;
            viewSo.FindProperty("zoneNameText").objectReferenceValue = zoneNameTmp;
            viewSo.FindProperty("regionNameText").objectReferenceValue = regionNameTmp;
            viewSo.FindProperty("riskStatusText").objectReferenceValue = riskStatusTmp;
            viewSo.FindProperty("discoveryStatusText").objectReferenceValue = discoveryStatusTmp;
            viewSo.ApplyModifiedProperties();

            // Wire View into Controller
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("hudView").objectReferenceValue = viewComp;
            controllerSo.ApplyModifiedProperties();

            LogFixed("WorldMapCurrentZoneHUD UI 요소 및 참조 연결 완료.");
        }

        private static GameObject FindOrCreateUIChild(GameObject parent, string name, System.Type componentType)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                LogFound($"UI 요소 (이미 존재): {parent.name}/{name}");
                return existing.gameObject;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            child.AddComponent(componentType);
            LogCreated($"UI 요소: {parent.name}/{name}");
            return child;
        }

        private static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ======================================================================
        //  12. MiniGrid 생성
        // ======================================================================

        public static void CreateMiniGridObjects()
        {
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                LogWarning("_WorldMap_Manual 루트가 없습니다. 먼저 씬 계층을 생성하세요.");
                return;
            }

            // WorldMapMiniGridCanvas
            Transform canvasTransform = root.transform.Find("WorldMapMiniGridCanvas");
            GameObject canvas;
            if (canvasTransform == null)
            {
                canvas = new GameObject("WorldMapMiniGridCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.transform.SetParent(root.transform, false);
                LogCreated("WorldMapMiniGridCanvas");
            }
            else
            {
                canvas = canvasTransform.gameObject;
                LogFound("WorldMapMiniGridCanvas (이미 존재)");
            }

            Canvas c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 450;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // WorldMapMiniGrid
            Transform gridTransform = canvas.transform.Find("WorldMapMiniGrid");
            GameObject grid;
            if (gridTransform == null)
            {
                grid = new GameObject("WorldMapMiniGrid", typeof(RectTransform));
                grid.transform.SetParent(canvas.transform, false);
                LogCreated("WorldMapMiniGrid");
            }
            else
            {
                grid = gridTransform.gameObject;
                LogFound("WorldMapMiniGrid (이미 존재)");
            }

            RectTransform gridRt = grid.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0f, 1f);
            gridRt.anchorMax = new Vector2(0f, 1f);
            gridRt.pivot = new Vector2(0f, 1f);
            gridRt.anchoredPosition = new Vector2(20f, -20f);
            gridRt.sizeDelta = new Vector2(190f, 110f);

            // Controller
            var controller = grid.GetComponent<WorldMapMiniGridController>();
            if (controller == null)
            {
                controller = grid.AddComponent<WorldMapMiniGridController>();
                LogCreated("WorldMapMiniGridController");
            }
            else
            {
                LogFound("WorldMapMiniGridController (이미 존재)");
            }

            // View child
            Transform viewTransform = grid.transform.Find("View");
            GameObject view;
            if (viewTransform == null)
            {
                view = new GameObject("View", typeof(RectTransform));
                view.transform.SetParent(grid.transform, false);
                LogCreated("WorldMapMiniGrid/View");
            }
            else
            {
                view = viewTransform.gameObject;
                LogFound("WorldMapMiniGrid/View (이미 존재)");
            }

            RectTransform viewRt = view.GetComponent<RectTransform>();
            StretchRect(viewRt);

            // View component
            var viewComp = view.GetComponent<WorldMapMiniGridView>();
            if (viewComp == null)
            {
                viewComp = view.AddComponent<WorldMapMiniGridView>();
                LogCreated("WorldMapMiniGridView");
            }
            else
            {
                LogFound("WorldMapMiniGridView (이미 존재)");
            }

            // Create 9 cells
            // Cell positions: 3x3 grid
            Vector2[] cellPositions = new Vector2[]
            {
                new Vector2(-60f, 30f), new Vector2(0f, 30f), new Vector2(60f, 30f),
                new Vector2(-60f, 0f),  new Vector2(0f, 0f),  new Vector2(60f, 0f),
                new Vector2(-60f, -30f), new Vector2(0f, -30f), new Vector2(60f, -30f)
            };

            // We'll create cells but note that cellImages/cellLabels arrays need manual assignment
            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;
                int col = i % 3;
                string cellName = $"Cell_{row}_{col}";

                Transform cellTransform = view.transform.Find(cellName);
                GameObject cell;
                if (cellTransform == null)
                {
                    cell = new GameObject(cellName, typeof(RectTransform), typeof(Image));
                    cell.transform.SetParent(view.transform, false);
                    LogCreated($"  Cell: {cellName}");
                }
                else
                {
                    cell = cellTransform.gameObject;
                    LogFound($"  Cell (이미 존재): {cellName}");
                }

                RectTransform cellRt = cell.GetComponent<RectTransform>();
                cellRt.anchorMin = new Vector2(0.5f, 0.5f);
                cellRt.anchorMax = new Vector2(0.5f, 0.5f);
                cellRt.pivot = new Vector2(0.5f, 0.5f);
                cellRt.anchoredPosition = cellPositions[i];
                cellRt.sizeDelta = new Vector2(50f, 30f);

                // Cell Image
                Image cellImg = cell.GetComponent<Image>();
                cellImg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);

                // Cell Label
                GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(cell.transform, false);
                RectTransform labelRt = label.GetComponent<RectTransform>();
                StretchRect(labelRt);
                TextMeshProUGUI labelTmp = label.GetComponent<TextMeshProUGUI>();
                labelTmp.fontSize = 12;
                labelTmp.alignment = TextAlignmentOptions.Center;
                labelTmp.text = $"{row},{col}";
            }

            // Wire View into Controller
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("gridView").objectReferenceValue = viewComp;
            controllerSo.ApplyModifiedProperties();

            LogWarning("WorldMapMiniGridView: cellImages/cellLabels 배열은 수동 할당 필요 (9개 Image + 9개 TMP 드래그)");
            LogFixed("WorldMapMiniGrid UI 요소 및 참조 연결 완료.");
        }

        // ======================================================================
        //  13. ZoneContentController & 샘플 바인딩
        // ======================================================================

        public static void CreateZoneContentControllerObject()
        {
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                LogWarning("_WorldMap_Manual 루트가 없습니다. 먼저 씬 계층을 생성하세요.");
                return;
            }

            Transform existing = root.transform.Find("WorldMapZoneContentController");
            GameObject controllerObj;
            if (existing == null)
            {
                controllerObj = new GameObject("WorldMapZoneContentController");
                controllerObj.transform.SetParent(root.transform, false);
                LogCreated("WorldMapZoneContentController");
            }
            else
            {
                controllerObj = existing.gameObject;
                LogFound("WorldMapZoneContentController (이미 존재)");
            }

            var controller = controllerObj.GetComponent<WorldMapZoneContentController>();
            if (controller == null)
            {
                controller = controllerObj.AddComponent<WorldMapZoneContentController>();
                LogCreated("WorldMapZoneContentController 컴포넌트");
            }
            else
            {
                LogFound("WorldMapZoneContentController 컴포넌트 (이미 존재)");
            }

            LogWarning("WorldMapZoneContentController.worldMapConfig는 Initialize()에서 런타임 주입됨");
        }

        public static void CreateSampleBoundContent()
        {
            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null) return;

            Transform controllerTransform = root.transform.Find("WorldMapZoneContentController");
            if (controllerTransform == null) return;
            GameObject controllerObj = controllerTransform.gameObject;

            var controller = controllerObj.GetComponent<WorldMapZoneContentController>();
            if (controller == null) return;

            // Create 4 sample bindings
            string[] zoneNames = { "ZoneData_DefaultFallback", "ZoneData_Hub_E5", "ZoneData_Research_I6", "ZoneData_Sealed_F10" };
            string[] bindingNames = { "Binding_DefaultFallback", "Binding_Hub_E5", "Binding_Research_I6", "Binding_Sealed_F10" };

            for (int i = 0; i < zoneNames.Length; i++)
            {
                Transform bindingTransform = controllerObj.transform.Find(bindingNames[i]);
                GameObject bindingObj;
                if (bindingTransform == null)
                {
                    bindingObj = new GameObject(bindingNames[i]);
                    bindingObj.transform.SetParent(controllerObj.transform, false);
                    LogCreated($"ZoneContentBinding: {bindingNames[i]}");
                }
                else
                {
                    bindingObj = bindingTransform.gameObject;
                    LogFound($"ZoneContentBinding (이미 존재): {bindingNames[i]}");
                }

                var binding = bindingObj.GetComponent<WorldMapZoneContentBinding>();
                if (binding == null)
                {
                    binding = bindingObj.AddComponent<WorldMapZoneContentBinding>();
                    LogCreated($"WorldMapZoneContentBinding 컴포넌트: {bindingNames[i]}");
                }

                SerializedObject so = new SerializedObject(binding);
                var zoneData = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/{zoneNames[i]}.asset");
                if (zoneData != null)
                    so.FindProperty("zoneData").objectReferenceValue = zoneData;
                so.ApplyModifiedProperties();
            }

            LogSummary("[ZoneContent] 4개 샘플 바인딩 생성 완료.");
        }

        // ======================================================================
        //  14. RuntimeInstaller 설정
        // ======================================================================

        public static void SetupInstallerComponent()
        {
            GameObject runtimeRoot = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot");
            if (runtimeRoot == null) return;

            Transform installerTransform = runtimeRoot.transform.Find("WorldMapRuntimeInstaller");
            if (installerTransform == null) return;
            GameObject installerObj = installerTransform.gameObject;

            var installer = installerObj.GetComponent<WorldMapRuntimeInstaller>();
            if (installer == null)
            {
                installer = installerObj.AddComponent<WorldMapRuntimeInstaller>();
                LogCreated("WorldMapRuntimeInstaller 컴포넌트");
            }
            else
            {
                LogFound("WorldMapRuntimeInstaller 컴포넌트 (이미 존재)");
            }

            SerializedObject so = new SerializedObject(installer);

            // worldMapConfig
            var config = AssetDatabase.LoadAssetAtPath<WorldMapConfigSO>($"{CONFIG_FOLDER}/WorldMapConfig_Main.asset");
            if (config != null) so.FindProperty("worldMapConfig").objectReferenceValue = config;

            // zoneDataAssets array
            var zoneDataArray = so.FindProperty("zoneDataAssets");
            if (zoneDataArray != null)
            {
                string[] zoneNames = { "ZoneData_DefaultFallback", "ZoneData_Hub_E5", "ZoneData_Research_I6", "ZoneData_Sealed_F10" };
                zoneDataArray.ClearArray();
                for (int i = 0; i < zoneNames.Length; i++)
                {
                    var zd = AssetDatabase.LoadAssetAtPath<ZoneDataSO>($"{ZONES_FOLDER}/{zoneNames[i]}.asset");
                    if (zd != null)
                    {
                        zoneDataArray.arraySize++;
                        zoneDataArray.GetArrayElementAtIndex(i).objectReferenceValue = zd;
                    }
                }
            }

            // zoneRoots array
            var zoneRootsArray = so.FindProperty("zoneRoots");
            if (zoneRootsArray != null)
            {
                zoneRootsArray.ClearArray();
                string[] zoneRootNames = { "ZoneRoot_Hub_E5", "ZoneRoot_Research_I6", "ZoneRoot_Sealed_F10" };
                GameObject zoneRootsParent = GameObject.Find("_WorldMap_Manual/ZoneRoots");
                if (zoneRootsParent != null)
                {
                    for (int i = 0; i < zoneRootNames.Length; i++)
                    {
                        Transform zr = zoneRootsParent.transform.Find(zoneRootNames[i]);
                        if (zr != null)
                        {
                            zoneRootsArray.arraySize++;
                            zoneRootsArray.GetArrayElementAtIndex(i).objectReferenceValue = zr.gameObject;
                        }
                    }
                }
            }

            // zoneContentController
            GameObject contentController = GameObject.Find("_WorldMap_Manual/WorldMapZoneContentController");
            if (contentController != null)
            {
                var cc = contentController.GetComponent<WorldMapZoneContentController>();
                if (cc != null) so.FindProperty("zoneContentController").objectReferenceValue = cc;
            }

            // currentZoneHUD (최신 시그니처 기준: Installer.currentZoneHUD)
            GameObject hudCanvas = GameObject.Find("_WorldMap_Manual/WorldMapHUDCanvas");
            if (hudCanvas != null)
            {
                Transform hudTransform = hudCanvas.transform.Find("WorldMapCurrentZoneHUD");
                if (hudTransform != null)
                {
                    var hudCtrl = hudTransform.GetComponent<WorldMapCurrentZoneHUDController>();
                    if (hudCtrl != null) so.FindProperty("currentZoneHUD").objectReferenceValue = hudCtrl;
                }
            }

            // miniGridController
            GameObject gridCanvas = GameObject.Find("_WorldMap_Manual/WorldMapMiniGridCanvas");
            if (gridCanvas != null)
            {
                Transform gridTransform = gridCanvas.transform.Find("WorldMapMiniGrid");
                if (gridTransform != null)
                {
                    var gridCtrl = gridTransform.GetComponent<WorldMapMiniGridController>();
                    if (gridCtrl != null) so.FindProperty("miniGridController").objectReferenceValue = gridCtrl;
                }
            }

            // debugHUD
            Transform debugHUDTransform = runtimeRoot.transform.Find("WorldMapDebugHUD");
            if (debugHUDTransform != null)
            {
                var debugHUD = debugHUDTransform.GetComponent<WorldMapDebugHUDRuntime>();
                if (debugHUD != null) so.FindProperty("debugHUD").objectReferenceValue = debugHUD;
            }

            // ambientReaction
            Transform ambientTransform = runtimeRoot.transform.Find("WorldMapAmbientReactionRoot");
            if (ambientTransform != null)
            {
                var ambientCtrl = ambientTransform.GetComponent<WorldMapAmbientReactionController>();
                if (ambientCtrl != null) so.FindProperty("ambientReactionController").objectReferenceValue = ambientCtrl;
            }

            // trackedTransform (Transform reference for WorldMapRuntimeInstaller)
            Transform trackedTransform = runtimeRoot.transform.Find("WorldMapTrackedTransform");
            if (trackedTransform != null)
            {
                so.FindProperty("trackedTransform").objectReferenceValue = trackedTransform;
            }


            // runtimeTest
            Transform testRootTransform = runtimeRoot.transform.Find("WorldMapRuntimeTestRoot");
            if (testRootTransform != null)
            {
                var testComp = testRootTransform.GetComponent<WorldMapRuntimeTest>();
                if (testComp != null) so.FindProperty("runtimeTest").objectReferenceValue = testComp;
            }

            so.ApplyModifiedProperties();
            LogFixed("WorldMapRuntimeInstaller 모든 참조 연결 완료.");
        }

        // ======================================================================
        //  15. Validate All
        // ======================================================================

        public static void ValidateAll()
        {
            LogSummary("===== 전체 검증 시작 =====");

            // 1. 폴더 존재 확인
            string[] folders = { CONFIG_FOLDER, ZONES_FOLDER, UNLOCK_CONDITIONS_FOLDER, DEBUG_FOLDER, AMBIENT_FOLDER };
            foreach (string folder in folders)
            {
                if (System.IO.Directory.Exists(folder))
                    LogFound($"폴더 존재: {folder}");
                else
                    LogWarning($"폴더 없음: {folder}");
            }

            // 2. ScriptableObject 에셋 확인
            ValidateAsset<WorldMapConfigSO>($"{CONFIG_FOLDER}/WorldMapConfig_Main.asset", "WorldMapConfig_Main");

            string[] ucsNames = { "UCS_Free", "UCS_ResearchZone_Test", "UCS_SealedNorth_Test" };
            foreach (string name in ucsNames)
                ValidateAsset<UnlockConditionSetSO>($"{UNLOCK_CONDITIONS_FOLDER}/{name}.asset", name);

            string[] ambientNames = { "Ambient_Hub_Safe", "Ambient_Research_Tense", "Ambient_Sealed_Danger", "Ambient_OutOfBounds_Default" };
            foreach (string name in ambientNames)
                ValidateAsset<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/{name}.asset", name);

            string[] zoneNames = { "ZoneData_DefaultFallback", "ZoneData_Hub_E5", "ZoneData_Research_I6", "ZoneData_Sealed_F10" };
            foreach (string name in zoneNames)
                ValidateAsset<ZoneDataSO>($"{ZONES_FOLDER}/{name}.asset", name);

            // 3. 씬 오브젝트 확인
            ValidateSceneObject("_WorldMap_Manual");
            ValidateSceneObject("_WorldMap_Manual/WorldMap_RuntimeRoot");
            ValidateSceneObject("_WorldMap_Manual/ZoneRoots");
            ValidateSceneObject("_WorldMap_Manual/WorldMapHUDCanvas");
            ValidateSceneObject("_WorldMap_Manual/WorldMapMiniGridCanvas");

            // 4. 컴포넌트 확인
            ValidateComponent<WorldMapRuntimeTest>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapRuntimeTestRoot");
            ValidateComponent<WorldMapDebugHUDRuntime>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapDebugHUD");
            ValidateComponent<WorldMapAmbientReactionController>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapAmbientReactionRoot");
            ValidateComponent<WorldMapRuntimeInstaller>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapRuntimeInstaller");
            ValidateComponent<WorldMapCurrentZoneHUDController>("_WorldMap_Manual/WorldMapHUDCanvas/WorldMapCurrentZoneHUD");
            ValidateComponent<WorldMapMiniGridController>("_WorldMap_Manual/WorldMapMiniGridCanvas/WorldMapMiniGrid");
            ValidateComponent<WorldMapZoneContentController>("_WorldMap_Manual/WorldMapZoneContentController");

            // 5. ZoneRoot 확인
            ValidateSceneObject("_WorldMap_Manual/ZoneRoots/ZoneRoot_Hub_E5");
            ValidateSceneObject("_WorldMap_Manual/ZoneRoots/ZoneRoot_Research_I6");
            ValidateSceneObject("_WorldMap_Manual/ZoneRoots/ZoneRoot_Sealed_F10");
            ValidateComponent<ZoneRoot>("_WorldMap_Manual/ZoneRoots/ZoneRoot_Hub_E5");
            ValidateComponent<ZoneRoot>("_WorldMap_Manual/ZoneRoots/ZoneRoot_Research_I6");
            ValidateComponent<ZoneRoot>("_WorldMap_Manual/ZoneRoots/ZoneRoot_Sealed_F10");

            PrintFinalSummary();
        }

        private static void ValidateAsset<T>(string path, string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                LogFound($"에셋 존재: {name} ({path})");
            else
                LogWarning($"에셋 없음: {name} ({path})");
        }

        private static void ValidateSceneObject(string path)
        {
            GameObject obj = GameObject.Find(path);
            if (obj != null)
                LogFound($"씬 오브젝트 존재: {path}");
            else
                LogWarning($"씬 오브젝트 없음: {path}");
        }

        private static void ValidateComponent<T>(string gameObjectPath) where T : Component
        {
            GameObject obj = GameObject.Find(gameObjectPath);
            if (obj == null)
            {
                LogWarning($"씬 오브젝트 없음 (컴포넌트 검사 불가): {gameObjectPath}");
                return;
            }

            var comp = obj.GetComponent<T>();
            if (comp != null)
                LogFound($"컴포넌트 존재: {typeof(T).Name} on {gameObjectPath}");
            else
                LogWarning($"컴포넌트 없음: {typeof(T).Name} on {gameObjectPath}");
        }
    }
}

