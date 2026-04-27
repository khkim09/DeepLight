using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Debug;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// WorldMapSetupTool의 씬 런타임 오브젝트 관련 기능을 분리한 보조 유틸리티 (Phase 2).
    /// _WorldMap_Manual 하이라키 기준으로 RuntimeRoot, ZoneRoots, Trigger를 생성/보정한다.
    /// HUD/Canvas/MiniGrid/사용자 UI는 생성하지 않는다.
    /// 기존 UIRoot / 기존 게임 HUD는 절대 건드리지 않는다.
    /// </summary>
    public static class WorldMapSceneHelper
    {
        // ===== 폴더 경로 상수 (WorldMapAssetHelper와 공유) =====
        private const string CONFIG_FOLDER = "Assets/_Project/ScriptableObjects/World/Config";
        private const string ZONES_FOLDER = "Assets/_Project/ScriptableObjects/World/Zones";
        private const string UNLOCK_CONDITIONS_FOLDER = "Assets/_Project/ScriptableObjects/World/UnlockConditions";
        private const string AMBIENT_FOLDER = "Assets/_Project/ScriptableObjects/World/AmbientProfiles";

        // ===== 결과 요약 =====
        private static readonly StringBuilder _summaryLog = new();
        private static int _createdCount;
        private static int _foundCount;
        private static int _fixedCount;
        private static int _warningCount;

        // ======================================================================
        //  Public: Scene Runtime Only 생성/보정
        // ======================================================================

        /// <summary>
        /// _WorldMap_Manual 하이라키를 기준으로 RuntimeRoot + ZoneRoots + Trigger를 생성/보정한다.
        /// HUD/Canvas/MiniGrid는 생성하지 않는다.
        /// </summary>
        public static void CreateUpdateSceneRuntimeOnly()
        {
            ResetSummary();
            LogSummary("===== WorldMap Scene Runtime Only 생성/보정 시작 =====");

            // 1. _WorldMap_Manual 루트
            GameObject root = FindOrCreateRoot("_WorldMap_Manual");

            // 2. WorldMap_RuntimeRoot 하위 오브젝트
            GameObject runtimeRoot = FindOrCreateChild(root, "WorldMap_RuntimeRoot");
            FindOrCreateChild(runtimeRoot, "WorldMapTrackedTransform");
            FindOrCreateChild(runtimeRoot, "WorldMapRuntimeInstaller");
            FindOrCreateChild(runtimeRoot, "WorldMapRuntimeTestRoot");
            FindOrCreateChild(runtimeRoot, "WorldMapDebugHUD");
            FindOrCreateChild(runtimeRoot, "WorldMapAmbientReactionRoot");

            // WorldMapBootstrapBridge는 optional — 있으면 재사용, 없으면 생성하지 않음
            Transform bootstrapBridgeTransform = root.transform.Find("WorldMapBootstrapBridge");
            if (bootstrapBridgeTransform != null)
            {
                LogFound("WorldMapBootstrapBridge (이미 존재, 재사용)");
            }
            else
            {
                LogSummary("[Scene] WorldMapBootstrapBridge는 optional — 필요 시 수동 생성 (DeepLightTestBedBootstrapper 연결용)");
            }

            // 3. ZoneRoots
            GameObject zoneRoots = FindOrCreateChild(root, "ZoneRoots");
            CreateSingleZoneRoot(zoneRoots, "ZoneRoot_Hub_E5", new Vector3(-200f, 0f, -200f));
            CreateSingleZoneRoot(zoneRoots, "ZoneRoot_Research_I6", new Vector3(1400f, 0f, 200f));
            CreateSingleZoneRoot(zoneRoots, "ZoneRoot_Sealed_F10", new Vector3(200f, 0f, 1800f));

            // 4. WorldMapZoneContentController (ZoneRoots 아래 또는 _WorldMap_Manual 직계)
            CreateOrFindZoneContentController(root);

            LogSummary("[Scene] 계층 구조 생성 완료.");
        }

        /// <summary>
        /// 모든 씬 런타임 오브젝트 + 컴포넌트를 설정한다.
        /// SetupAllComponents + SetupInstallerComponent + SetupZoneRootComponents를 한 번에 실행한다.
        /// </summary>
        public static void SetupAllSceneComponents()
        {
            ResetSummary();
            LogSummary("===== WorldMap Scene Runtime 컴포넌트 설정 시작 =====");

            SetupTrackedTransformPosition();
            SetupRuntimeTestComponent();
            SetupDebugHUDComponent();
            SetupAmbientReactionComponent();
            SetupZoneRootComponents();
            SetupInstallerComponent();
            SetupZoneContentControllerComponent();

            LogSummary("[Scene] 모든 컴포넌트 설정 완료.");
            PrintFinalSummary();
        }

        /// <summary>
        /// Installer의 런타임 참조만 재연결한다 (Rewire Installer Runtime References).
        /// </summary>
        public static void RewireInstallerRuntimeReferences()
        {
            ResetSummary();
            LogSummary("===== Installer Runtime 참조 재연결 시작 =====");
            SetupInstallerComponent();
            PrintFinalSummary();
        }

        /// <summary>
        /// 씬 런타임 오브젝트 + 컴포넌트 존재 여부를 검증한다.
        /// </summary>
        public static void ValidateSceneRuntime()
        {
            ResetSummary();
            LogSummary("===== WorldMap Scene Runtime 검증 시작 =====");

            int totalChecks = 0;
            int passedChecks = 0;
            int failedChecks = 0;

            // 1. 루트 오브젝트
            totalChecks++;
            if (ValidateSceneObject("_WorldMap_Manual")) passedChecks++; else failedChecks++;

            totalChecks++;
            if (ValidateSceneObject("_WorldMap_Manual/WorldMap_RuntimeRoot")) passedChecks++; else failedChecks++;

            totalChecks++;
            if (ValidateSceneObject("_WorldMap_Manual/ZoneRoots")) passedChecks++; else failedChecks++;

            // 2. RuntimeRoot 하위
            string[] runtimeChildren = { "WorldMapTrackedTransform", "WorldMapRuntimeInstaller", "WorldMapRuntimeTestRoot", "WorldMapDebugHUD", "WorldMapAmbientReactionRoot" };
            foreach (string child in runtimeChildren)
            {
                totalChecks++;
                if (ValidateSceneObject($"_WorldMap_Manual/WorldMap_RuntimeRoot/{child}")) passedChecks++; else failedChecks++;
            }

            // 3. 컴포넌트
            totalChecks++;
            if (ValidateComponent<WorldMapRuntimeTest>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapRuntimeTestRoot")) passedChecks++; else failedChecks++;

            totalChecks++;
            if (ValidateComponent<WorldMapDebugHUDRuntime>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapDebugHUD")) passedChecks++; else failedChecks++;

            totalChecks++;
            if (ValidateComponent<WorldMapAmbientReactionController>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapAmbientReactionRoot")) passedChecks++; else failedChecks++;

            totalChecks++;
            if (ValidateComponent<WorldMapRuntimeInstaller>("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapRuntimeInstaller")) passedChecks++; else failedChecks++;

            // 4. ZoneRoots
            string[] zoneRootNames = { "ZoneRoot_Hub_E5", "ZoneRoot_Research_I6", "ZoneRoot_Sealed_F10" };
            foreach (string zr in zoneRootNames)
            {
                totalChecks++;
                if (ValidateSceneObject($"_WorldMap_Manual/ZoneRoots/{zr}")) passedChecks++; else failedChecks++;

                totalChecks++;
                if (ValidateComponent<ZoneRoot>($"_WorldMap_Manual/ZoneRoots/{zr}")) passedChecks++; else failedChecks++;

                // Trigger
                totalChecks++;
                if (ValidateSceneObject($"_WorldMap_Manual/ZoneRoots/{zr}/Triggers/{zr}_Trigger")) passedChecks++; else failedChecks++;

                totalChecks++;
                if (ValidateComponent<ZoneTriggerRelay>($"_WorldMap_Manual/ZoneRoots/{zr}/Triggers/{zr}_Trigger")) passedChecks++; else failedChecks++;
            }

            // 5. ZoneContentController
            totalChecks++;
            if (ValidateSceneObject("_WorldMap_Manual/WorldMapZoneContentController")) passedChecks++; else failedChecks++;

            totalChecks++;
            if (ValidateComponent<WorldMapZoneContentController>("_WorldMap_Manual/WorldMapZoneContentController")) passedChecks++; else failedChecks++;

            _summaryLog.AppendLine("");
            _summaryLog.AppendLine($"===== Scene Runtime 검증 결과: {totalChecks}개 검사, {passedChecks}개 통과, {failedChecks}개 실패 =====");
            PrintFinalSummary();
        }

        // ======================================================================
        //  내부: 오브젝트 생성/찾기
        // ======================================================================

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject root = GameObject.Find(name);
            if (root == null)
            {
                root = new GameObject(name);
                LogCreated($"루트: {name}");
            }
            else
            {
                LogFound($"루트 (이미 존재): {name}");
            }
            return root;
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
        //  내부: ZoneRoot 생성
        // ======================================================================

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
        //  내부: ZoneContentController 생성/찾기
        // ======================================================================

        private static void CreateOrFindZoneContentController(GameObject root)
        {
            Transform existing = root.transform.Find("WorldMapZoneContentController");
            if (existing == null)
            {
                GameObject controllerObj = new GameObject("WorldMapZoneContentController");
                controllerObj.transform.SetParent(root.transform, false);
                LogCreated("WorldMapZoneContentController");
            }
            else
            {
                LogFound("WorldMapZoneContentController (이미 존재)");
            }
        }

        // ======================================================================
        //  내부: 컴포넌트 설정
        // ======================================================================

        /// <summary>WorldMapTrackedTransform 위치 설정</summary>
        private static void SetupTrackedTransformPosition()
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

        /// <summary>WorldMapRuntimeTest 컴포넌트 설정</summary>
        private static void SetupRuntimeTestComponent()
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

            // TrackedTransform
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

            // NOTE: validateZoneRoots / logZoneRootMismatches 필드는 WorldMapRuntimeTest에 없음
            // zoneRoots 배열은 수동 할당 필요
            LogWarning("WorldMapRuntimeTest.zoneRoots: 3개 ZoneRoot를 인스펙터에서 배열에 드래그 필요");

            so.ApplyModifiedProperties();
            LogFixed("WorldMapRuntimeTest 값 설정 완료.");
        }

        /// <summary>WorldMapDebugHUDRuntime 컴포넌트 설정</summary>
        private static void SetupDebugHUDComponent()
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

        /// <summary>WorldMapAmbientReactionController 컴포넌트 설정</summary>
        private static void SetupAmbientReactionComponent()
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
            LogFixed("WorldMapAmbientReactionController outOfBoundsProfile 설정 완료.");
        }

        /// <summary>ZoneRoot 컴포넌트 설정 (ZoneDataSO 연결)</summary>
        private static void SetupZoneRootComponents()
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

        /// <summary>WorldMapRuntimeInstaller 컴포넌트 설정 (모든 참조 연결)</summary>
        private static void SetupInstallerComponent()
        {
            GameObject runtimeRoot = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot");
            if (runtimeRoot == null)
            {
                LogWarning("WorldMap_RuntimeRoot를 찾을 수 없습니다. 먼저 Create/Update Scene Runtime Only를 실행하세요.");
                return;
            }

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

            // Mode = Debug
            var modeProp = so.FindProperty("mode");
            if (modeProp != null)
                modeProp.enumValueIndex = 0; // InstallerMode.Debug

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

            // trackedTransform — Submarine 우선 탐색, 없으면 Player 태그, 없으면 WorldMapTrackedTransform
            Transform trackedTransform = FindTrackedTransform();
            if (trackedTransform != null)
            {
                so.FindProperty("trackedTransform").objectReferenceValue = trackedTransform;
                LogFixed($"Installer.trackedTransform = {trackedTransform.name}");
            }
            else
            {
                // Fallback: WorldMapTrackedTransform
                Transform fallbackTracked = runtimeRoot.transform.Find("WorldMapTrackedTransform");
                if (fallbackTracked != null)
                {
                    so.FindProperty("trackedTransform").objectReferenceValue = fallbackTracked;
                    LogFixed("Installer.trackedTransform = WorldMapTrackedTransform (fallback)");
                }
            }

            // runtimeTest
            Transform testRootTransform = runtimeRoot.transform.Find("WorldMapRuntimeTestRoot");
            if (testRootTransform != null)
            {
                var testComp = testRootTransform.GetComponent<WorldMapRuntimeTest>();
                if (testComp != null) so.FindProperty("runtimeTest").objectReferenceValue = testComp;
            }

            // ambientController
            Transform ambientTransform = runtimeRoot.transform.Find("WorldMapAmbientReactionRoot");
            if (ambientTransform != null)
            {
                var ambientCtrl = ambientTransform.GetComponent<WorldMapAmbientReactionController>();
                if (ambientCtrl != null) so.FindProperty("ambientController").objectReferenceValue = ambientCtrl;
            }

            // debugHUDComponent
            Transform debugHUDTransform = runtimeRoot.transform.Find("WorldMapDebugHUD");
            if (debugHUDTransform != null)
            {
                var debugHUD = debugHUDTransform.GetComponent<WorldMapDebugHUDRuntime>();
                if (debugHUD != null) so.FindProperty("debugHUDComponent").objectReferenceValue = debugHUD;
            }

            // targetCamera — Camera_Exploration 우선, 없으면 Camera.main
            Camera targetCam = FindTargetCamera();
            if (targetCam != null)
            {
                so.FindProperty("targetCamera").objectReferenceValue = targetCam;
                LogFixed($"Installer.targetCamera = {targetCam.name}");
            }
            else
            {
                LogWarning("targetCamera: Camera_Exploration 또는 Camera.main을 찾을 수 없습니다. 수동 할당 필요.");
            }

            // currentZoneHUD / miniGridController — 이번 단계에서 비워둠
            LogSummary("[Installer] currentZoneHUD / miniGridController는 이번 단계에서 비워둠 (Phase 3에서 연결)");

            // outOfBoundsProfile
            var ambientOob = AssetDatabase.LoadAssetAtPath<ZoneAmbientProfileSO>($"{AMBIENT_FOLDER}/Ambient_OutOfBounds_Default.asset");
            if (ambientOob != null)
                so.FindProperty("outOfBoundsProfile").objectReferenceValue = ambientOob;

            // Mock Progress values
            so.FindProperty("useMockProgress").boolValue = true;
            so.FindProperty("testUpgradeId").stringValue = "upgrade_battery_mk2";
            so.FindProperty("testLogId").stringValue = "log_intro_01";
            so.FindProperty("testNarrativeFlag").stringValue = "mara_research_analysis_done";
            so.FindProperty("mockHullTier").intValue = 2;
            so.FindProperty("mockDepthLevel").intValue = 3;
            so.FindProperty("mockSensorAccuracy").floatValue = 0.8f;
            so.FindProperty("mockLogCount").intValue = 5;

            // zoneContentController
            GameObject contentController = GameObject.Find("_WorldMap_Manual/WorldMapZoneContentController");
            if (contentController != null)
            {
                var cc = contentController.GetComponent<WorldMapZoneContentController>();
                if (cc != null) so.FindProperty("zoneContentController").objectReferenceValue = cc;
            }

            so.ApplyModifiedProperties();
            LogFixed("WorldMapRuntimeInstaller 모든 참조 연결 완료.");
        }

        /// <summary>WorldMapZoneContentController 컴포넌트 설정</summary>
        private static void SetupZoneContentControllerComponent()
        {
            GameObject controllerObj = GameObject.Find("_WorldMap_Manual/WorldMapZoneContentController");
            if (controllerObj == null) return;

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

            // worldMapConfig는 Initialize()에서 런타임 주입되므로 인스펙터 설정 불필요
            LogSummary("[ZoneContentController] worldMapConfig는 Initialize()에서 런타임 주입됨");
        }

        // ======================================================================
        //  내부: Transform/Camera 탐색 유틸리티
        // ======================================================================

        /// <summary>
        /// Submarine 오브젝트를 우선 탐색, 없으면 Player 태그, 없으면 null 반환.
        /// </summary>
        private static Transform FindTrackedTransform()
        {
            // 1. "Submarine" 이름으로 탐색
            GameObject submarine = GameObject.Find("Submarine");
            if (submarine != null)
            {
                LogFound("TrackedTransform: Submarine (이름 검색)");
                return submarine.transform;
            }

            // 2. Player 태그로 탐색
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                LogFound("TrackedTransform: Player (태그 검색)");
                return player.transform;
            }

            LogWarning("TrackedTransform: Submarine 또는 Player 태그를 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// Camera_Exploration를 우선 탐색, 없으면 Camera.main 반환.
        /// </summary>
        private static Camera FindTargetCamera()
        {
            // 1. "Camera_Exploration" 이름으로 탐색
            GameObject camObj = GameObject.Find("Camera_Exploration");
            if (camObj != null)
            {
                Camera cam = camObj.GetComponent<Camera>();
                if (cam != null)
                {
                    LogFound("TargetCamera: Camera_Exploration (이름 검색)");
                    return cam;
                }
            }

            // 2. Camera.main
            if (Camera.main != null)
            {
                LogFound("TargetCamera: Camera.main");
                return Camera.main;
            }

            return null;
        }

        // ======================================================================
        //  내부: 검증 유틸리티
        // ======================================================================

        private static bool ValidateSceneObject(string path)
        {
            GameObject obj = GameObject.Find(path);
            if (obj != null)
            {
                LogFound($"씬 오브젝트 존재: {path}");
                return true;
            }
            else
            {
                LogWarning($"씬 오브젝트 없음: {path}");
                return false;
            }
        }

        private static bool ValidateComponent<T>(string gameObjectPath) where T : Component
        {
            GameObject obj = GameObject.Find(gameObjectPath);
            if (obj == null)
            {
                LogWarning($"씬 오브젝트 없음 (컴포넌트 검사 불가): {gameObjectPath}");
                return false;
            }

            var comp = obj.GetComponent<T>();
            if (comp != null)
            {
                LogFound($"컴포넌트 존재: {typeof(T).Name} on {gameObjectPath}");
                return true;
            }
            else
            {
                LogWarning($"컴포넌트 없음: {typeof(T).Name} on {gameObjectPath}");
                return false;
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
            Debug.Log($"[WorldMapSceneHelper] 생성: {message}");
        }

        private static void LogFound(string message)
        {
            _foundCount++;
            _summaryLog.AppendLine($"  [발견] {message}");
            Debug.Log($"[WorldMapSceneHelper] 발견: {message}");
        }

        private static void LogFixed(string message)
        {
            _fixedCount++;
            _summaryLog.AppendLine($"  [보정] {message}");
            Debug.Log($"[WorldMapSceneHelper] 보정: {message}");
        }

        private static void LogWarning(string message)
        {
            _warningCount++;
            _summaryLog.AppendLine($"  [경고] {message}");
            Debug.LogWarning($"[WorldMapSceneHelper] 경고: {message}");
        }

        private static void PrintFinalSummary()
        {
            _summaryLog.AppendLine("");
            _summaryLog.AppendLine("===== WorldMap Scene Helper 결과 요약 =====");
            _summaryLog.AppendLine($"  생성: {_createdCount}개");
            _summaryLog.AppendLine($"  발견/재사용: {_foundCount}개");
            _summaryLog.AppendLine($"  보정/재연결: {_fixedCount}개");
            _summaryLog.AppendLine($"  경고: {_warningCount}개");
            _summaryLog.AppendLine("");

            if (_warningCount > 0)
            {
                _summaryLog.AppendLine("⚠ 수동 확인 필요 항목:");
                _summaryLog.AppendLine("  - WorldMapRuntimeTest.zoneRoots: 3개 ZoneRoot를 인스펙터에서 배열에 드래그");
                _summaryLog.AppendLine("  - WorldMapBootstrapBridge: optional, 필요 시 수동 생성");
                _summaryLog.AppendLine("  - currentZoneHUD / miniGridController: Phase 3에서 연결 예정");
            }

            Debug.Log(_summaryLog.ToString());
        }
    }
}
