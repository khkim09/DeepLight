using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// DeepLight WorldMap Zone별 환경(UnderwaterArea/Seafloor) 자동 생성을 위한 Editor Utility.
    /// Phase 5: ZoneRoot_A1~J10 각각에 UnderwaterArea 복제본과 Seafloor placeholder를 생성한다.
    /// Phase 5.5: UnderwaterArea BoxCollider top padding 보정, Water Level Source SerializedObject 보정.
    /// 모든 Hierarchy 조작은 GeneratedWorldRoot/ZoneRoots 하위로 제한된다.
    /// </summary>
    public static class DeepLightMapEnvironmentGenerationUtility
    {
        // ===== Zone 환경 데이터 구조 =====

        /// <summary>
        /// Zone별 환경 데이터를 저장하는 구조체.
        /// ScenarioPreset Rule 또는 EnvironmentProfile에서 추출한다.
        /// </summary>
        public struct ZoneEnvironmentData
        {
            public float WaterLevelY; // 수면 Y 좌표 (기본 0)
            public float MinDepth; // 최소 깊이 (위쪽 경계, 예: -50)
            public float MaxDepth; // 최대 깊이 (아래쪽 경계, 예: -450)
            public ZoneDepthBand DepthBand;
            public ZoneBiomeType BiomeType;
            public string RuleName; // Rule 이름 (Debug용)
            public Color DebugColor; // 디버그 색상
            public Material SeafloorMaterial; // 해저 머티리얼 (null 가능)
            public GameObject UnderwaterAreaTemplatePrefab; // Zone별 UnderwaterArea 템플릿 (null 가능)
        }

        // ===== Public API =====

        /// <summary>
        /// 모든 ZoneRoot에 대해 UnderwaterArea 복제본과 Seafloor placeholder를 생성한다.
        /// settings의 createZoneUnderwaterAreas, createSeafloorPlaceholders 플래그를 따른다.
        /// </summary>
        public static void RebuildAllZoneEnvironments(DeepLightMapAutoBuilderSettingsSO settings, GameObject zoneRootsParent)
        {
            if (settings == null || settings.WorldMapConfig == null || settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings, WorldMapConfig, or ScenarioPreset is null. Cannot rebuild environments.");
                return;
            }

            if (zoneRootsParent == null)
            {
                Debug.LogError("[MapAutoBuilder] ZoneRoots parent is null. Cannot rebuild environments.");
                return;
            }

            var config = settings.WorldMapConfig;
            var preset = settings.ScenarioPreset;
            int gridWidth = config.GridWidth;
            int gridHeight = config.GridHeight;
            float zoneSize = config.ZoneSize;

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Rebuild Zone Environments (Phase 5 + 5.5) =====");
            log.AppendLine($"Grid: {gridWidth}x{gridHeight}, ZoneSize: {zoneSize}");

            int underwaterCount = 0;
            int seafloorCount = 0;
            int totalExpected = gridWidth * gridHeight;

            // 10x10 그리드 순회
            for (int colIndex = 0; colIndex < gridWidth; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < gridHeight; rowIndex++)
                {
                    char columnChar = (char)('A' + colIndex);
                    int rowNumber = rowIndex + 1;
                    ZoneId zoneId = new ZoneId(columnChar, rowNumber);

                    // ZoneRoot 찾기
                    string zoneRootName = $"ZoneRoot_{zoneId}";
                    Transform zoneTransform = zoneRootsParent.transform.Find(zoneRootName);
                    if (zoneTransform == null)
                    {
                        log.AppendLine($"  [SKIP] {zoneRootName} not found. Skipping environment generation.");
                        continue;
                    }

                    GameObject zoneRoot = zoneTransform.gameObject;

                    // Zone 환경 데이터 resolve
                    ZoneEnvironmentData envData = ResolveZoneEnvironmentData(preset, zoneId);

                    // UnderwaterArea 생성
                    if (settings.CreateZoneUnderwaterAreas)
                    {
                        CreateOrUpdateZoneUnderwaterArea(zoneRoot, zoneId, zoneSize, envData, settings);
                        underwaterCount++;
                    }

                    // Seafloor placeholder 생성
                    if (settings.CreateSeafloorPlaceholders)
                    {
                        CreateOrUpdateSeafloorPlaceholder(zoneRoot, zoneId, zoneSize, envData, settings);
                        seafloorCount++;
                    }
                }
            }

            // Phase 5.5: UnderwaterArea volume correction 적용
            if (settings.CreateZoneUnderwaterAreas)
            {
                ApplyAllUnderwaterVolumeCorrections(settings, zoneRootsParent);
            }

            // Summary 로그
            log.AppendLine($"\nUnderwaterArea generated/updated: {underwaterCount}/{totalExpected}.");
            log.AppendLine($"Seafloor placeholders generated/updated: {seafloorCount}/{totalExpected}.");
            log.AppendLine("===== Rebuild Zone Environments Complete =====");

            // Verbose 샘플 로그
            if (settings.LogVerbose)
            {
                LogSampleEnvironmentData(settings, zoneRootsParent);
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// ZoneRoot/Environment/UnderwaterArea 아래에 UnderwaterArea 복제본을 생성한다.
        /// 템플릿 우선순위: SceneContext > SettingsSO Prefab > 빈 GameObject + BoxCollider
        /// Phase 5.5: volume 계산에 top padding을 적용한다.
        /// </summary>
        public static void CreateOrUpdateZoneUnderwaterArea(
            GameObject zoneRoot, ZoneId zoneId, float zoneSize,
            ZoneEnvironmentData envData, DeepLightMapAutoBuilderSettingsSO settings)
        {
            // Environment/UnderwaterArea 부모 찾기
            Transform envTransform = zoneRoot.transform.Find("Environment");
            if (envTransform == null)
            {
                Debug.LogWarning($"[MapAutoBuilder] Environment root not found under {zoneRoot.name}. Skipping underwater area.");
                return;
            }

            Transform underwaterRoot = envTransform.Find("UnderwaterArea");
            if (underwaterRoot == null)
            {
                Debug.LogWarning($"[MapAutoBuilder] UnderwaterArea root not found under {zoneRoot.name}/Environment. Skipping.");
                return;
            }

            string underwaterName = $"UnderwaterArea_{zoneId}";

            // Phase 5.5: 수심 볼륨 계산 (top padding 적용)
            float topY = envData.WaterLevelY + settings.UnderwaterVolumeTopPadding; // 수면 Y + 패딩
            float bottomY = envData.MaxDepth; // 최대 깊이 (예: -450)
            float volumeHeight = Mathf.Max(1f, topY - bottomY); // 최소 1m 보장
            float volumeCenterY = (topY + bottomY) * 0.5f;

            // 기존 UnderwaterArea 검색
            Transform existing = underwaterRoot.Find(underwaterName);
            GameObject underwaterObj;

            if (existing != null)
            {
                // 기존 오브젝트 재사용, 위치 보정
                underwaterObj = existing.gameObject;
                underwaterObj.transform.localPosition = new Vector3(0f, volumeCenterY, 0f);
                underwaterObj.transform.localRotation = Quaternion.identity;
                underwaterObj.transform.localScale = Vector3.one;
            }
            else
            {
                // 템플릿 resolve
                GameObject template = ResolveUnderwaterTemplate(settings);

                // 템플릿 복제 시도
                underwaterObj = null;
                if (template != null)
                {
                    underwaterObj = CloneTemplate(template, underwaterRoot, underwaterName);
                    if (underwaterObj != null)
                    {
                        underwaterObj.transform.localPosition = new Vector3(0f, volumeCenterY, 0f);
                        underwaterObj.transform.localRotation = Quaternion.identity;
                        underwaterObj.transform.localScale = Vector3.one;
                    }
                }

                // 템플릿이 없거나 복제 실패 시 빈 GameObject 생성
                if (underwaterObj == null)
                {
                    underwaterObj = new GameObject(underwaterName);
                    underwaterObj.transform.SetParent(underwaterRoot);
                    underwaterObj.transform.localPosition = new Vector3(0f, volumeCenterY, 0f);
                    underwaterObj.transform.localRotation = Quaternion.identity;
                    underwaterObj.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(underwaterObj, $"Create {underwaterName}");
                }
            }

            // BoxCollider 보정 (Phase 5.5: root BoxCollider 우선)
            AdjustUnderwaterColliderPhase55(underwaterObj, zoneSize, volumeHeight);
        }

        /// <summary>
        /// ZoneRoot/Environment/Seafloor 아래에 Seafloor placeholder를 생성한다.
        /// PrimitiveType.Cube를 사용하며, maxDepth 위치에 배치한다.
        /// </summary>
        public static void CreateOrUpdateSeafloorPlaceholder(
            GameObject zoneRoot, ZoneId zoneId, float zoneSize,
            ZoneEnvironmentData envData, DeepLightMapAutoBuilderSettingsSO settings)
        {
            // Environment/Seafloor 부모 찾기
            Transform envTransform = zoneRoot.transform.Find("Environment");
            if (envTransform == null)
            {
                Debug.LogWarning($"[MapAutoBuilder] Environment root not found under {zoneRoot.name}. Skipping seafloor.");
                return;
            }

            Transform seafloorRoot = envTransform.Find("Seafloor");
            if (seafloorRoot == null)
            {
                Debug.LogWarning($"[MapAutoBuilder] Seafloor root not found under {zoneRoot.name}/Environment. Skipping.");
                return;
            }

            string seafloorName = $"Seafloor_{zoneId}";
            float seafloorY = envData.MaxDepth; // 해저는 maxDepth 위치

            // 기존 Seafloor 검색
            Transform existing = seafloorRoot.Find(seafloorName);
            GameObject seafloorObj;

            if (existing != null)
            {
                // 기존 오브젝트 재사용, 위치/스케일 보정
                seafloorObj = existing.gameObject;
                seafloorObj.transform.localPosition = new Vector3(0f, seafloorY, 0f);
                seafloorObj.transform.localRotation = Quaternion.identity;
                seafloorObj.transform.localScale = new Vector3(zoneSize, 2f, zoneSize);
            }
            else
            {
                // 새 Cube placeholder 생성
                seafloorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seafloorObj.name = seafloorName;
                seafloorObj.transform.SetParent(seafloorRoot);
                seafloorObj.transform.localPosition = new Vector3(0f, seafloorY, 0f);
                seafloorObj.transform.localRotation = Quaternion.identity;
                seafloorObj.transform.localScale = new Vector3(zoneSize, 2f, zoneSize);
                Undo.RegisterCreatedObjectUndo(seafloorObj, $"Create {seafloorName}");
            }

            // SeafloorMaterial 적용 (있으면)
            if (envData.SeafloorMaterial != null)
            {
                MeshRenderer renderer = seafloorObj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = envData.SeafloorMaterial;
                }
            }

            // 이름에 placeholder임을 명시
            if (!seafloorObj.name.Contains("_PLACEHOLDER"))
            {
                seafloorObj.name = $"{seafloorName}_PLACEHOLDER";
            }
        }

        // ===== Phase 5.5: Underwater Volume Correction =====

        /// <summary>
        /// 모든 UnderwaterArea에 대해 Phase 5.5 보정을 적용한다.
        /// BoxCollider root 우선 처리, SerializedObject로 trigger volume/water level source 보정.
        /// </summary>
        public static void ApplyAllUnderwaterVolumeCorrections(DeepLightMapAutoBuilderSettingsSO settings, GameObject zoneRootsParent)
        {
            if (settings == null || settings.WorldMapConfig == null || zoneRootsParent == null)
                return;

            var config = settings.WorldMapConfig;
            int gridWidth = config.GridWidth;
            int gridHeight = config.GridHeight;
            float zoneSize = config.ZoneSize;

            int totalExpected = gridWidth * gridHeight;
            int paddingApplied = 0;
            int triggerVolumeLinked = 0;
            int fixedWaterLevelApplied = 0;
            int waterLevelSourceChanged = 0;
            int waterLevelSourceUnchanged = 0;
            int serializedPropertyLogged = 0;

            // Generated Ocean resolve
            GameObject generatedOcean = FindGeneratedOcean(settings);

            var warningSummary = new StringBuilder();
            int warningCount = 0;

            for (int colIndex = 0; colIndex < gridWidth; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < gridHeight; rowIndex++)
                {
                    char columnChar = (char)('A' + colIndex);
                    int rowNumber = rowIndex + 1;
                    ZoneId zoneId = new ZoneId(columnChar, rowNumber);

                    string zoneRootName = $"ZoneRoot_{zoneId}";
                    Transform zoneTransform = zoneRootsParent.transform.Find(zoneRootName);
                    if (zoneTransform == null) continue;

                    GameObject zoneRoot = zoneTransform.gameObject;

                    // UnderwaterArea 찾기
                    Transform envTransform = zoneRoot.transform.Find("Environment");
                    if (envTransform == null) continue;
                    Transform underwaterRoot = envTransform.Find("UnderwaterArea");
                    if (underwaterRoot == null) continue;

                    string underwaterName = $"UnderwaterArea_{zoneId}";
                    Transform underwaterTransform = underwaterRoot.Find(underwaterName);
                    if (underwaterTransform == null) continue;

                    GameObject underwaterObj = underwaterTransform.gameObject;

                    // 1. BoxCollider root 우선 처리
                    BoxCollider rootCollider = underwaterObj.GetComponent<BoxCollider>();
                    if (rootCollider == null)
                    {
                        // root에 없으면 새로 추가
                        rootCollider = underwaterObj.AddComponent<BoxCollider>();
                    }

                    // Zone 환경 데이터로 volume 재계산
                    ZoneEnvironmentData envData = ResolveZoneEnvironmentData(settings.ScenarioPreset, zoneId);
                    float topY = envData.WaterLevelY + settings.UnderwaterVolumeTopPadding;
                    float bottomY = envData.MaxDepth;
                    float volumeHeight = Mathf.Max(1f, topY - bottomY);

                    rootCollider.isTrigger = true;
                    rootCollider.center = Vector3.zero;
                    rootCollider.size = new Vector3(zoneSize, volumeHeight, zoneSize);
                    paddingApplied++;

                    // 2. UnderwaterArea 컴포넌트 찾기 (타입 이름에 "Underwater" + "Area" 포함)
                    Component underwaterComponent = FindUnderwaterAreaComponent(underwaterObj);
                    if (underwaterComponent == null)
                    {
                        // 컴포넌트가 없으면 더 이상 보정할 것이 없음
                        continue;
                    }

                    // 3. SerializedProperty 로그 (옵션)
                    if (settings.LogUnderwaterAreaSerializedProperties)
                    {
                        LogSerializedPropertiesIfEnabled(underwaterComponent, true);
                        serializedPropertyLogged++;
                    }

                    // 4. Trigger volume 연결 시도
                    bool triggerLinked = TrySetTriggerVolume(underwaterComponent, rootCollider);
                    if (triggerLinked)
                    {
                        triggerVolumeLinked++;
                    }

                    // 5. Water level source 보정
                    if (settings.ForceUnderwaterAreaFixedWaterLevel)
                    {
                        bool sourceChanged = TryFixWaterLevelSource(underwaterComponent, settings.FixedUnderwaterWaterLevelY, generatedOcean);
                        if (sourceChanged)
                        {
                            waterLevelSourceChanged++;
                        }
                        else
                        {
                            waterLevelSourceUnchanged++;
                        }

                        // Fixed water level float 값 설정 시도
                        bool waterLevelSet = TrySetFloatByPropertyNames(underwaterComponent,
                            new[] { "waterLevel", "m_waterLevel", "waterLevelHeight", "m_waterLevelHeight",
                                    "height", "m_height", "fixedWaterLevel", "m_fixedWaterLevel",
                                    "customWaterLevel", "m_customWaterLevel" },
                            settings.FixedUnderwaterWaterLevelY);
                        if (waterLevelSet)
                        {
                            fixedWaterLevelApplied++;
                        }
                    }
                }
            }

            // Summary 로그
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Underwater Volume Correction (Phase 5.5) =====");
            log.AppendLine($"UnderwaterArea volume padding applied: {paddingApplied}/{totalExpected}.");
            log.AppendLine($"UnderwaterArea trigger volume linked by SerializedObject: {triggerVolumeLinked}/{totalExpected}.");
            log.AppendLine($"UnderwaterArea fixed water level applied: {fixedWaterLevelApplied}/{totalExpected}.");
            log.AppendLine($"UnderwaterArea water level source changed to Fixed/Manual: {waterLevelSourceChanged}/{totalExpected}.");
            log.AppendLine($"UnderwaterArea water level source unchanged: {waterLevelSourceUnchanged}/{totalExpected}.");

            if (warningCount > 0)
            {
                log.AppendLine($"\nWarnings ({warningCount}):");
                log.Append(warningSummary);
            }

            log.AppendLine("===== Underwater Volume Correction Complete =====");
            Debug.Log(log.ToString());

            // Verbose 샘플 로그
            if (settings.LogVerbose)
            {
                LogSampleVolumeCorrection(settings, zoneRootsParent);
            }
        }

        // ===== Phase 5.5: SerializedObject Helpers =====

        /// <summary>
        /// GameObject에서 타입 이름에 "Underwater"와 "Area"가 포함된 컴포넌트를 찾는다.
        /// root 우선, 없으면 children에서 검색.
        /// </summary>
        public static Component FindUnderwaterAreaComponent(GameObject root)
        {
            // root에서 먼저 검색
            Component[] rootComponents = root.GetComponents<Component>();
            foreach (Component comp in rootComponents)
            {
                if (comp != null)
                {
                    string typeName = comp.GetType().Name;
                    if (typeName.Contains("Underwater") && typeName.Contains("Area"))
                    {
                        return comp;
                    }
                }
            }

            // children에서 검색
            Component[] allComponents = root.GetComponentsInChildren<Component>();
            foreach (Component comp in allComponents)
            {
                if (comp != null && comp.gameObject != root)
                {
                    string typeName = comp.GetType().Name;
                    if (typeName.Contains("Underwater") && typeName.Contains("Area"))
                    {
                        return comp;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// SerializedObject로 trigger volume property를 찾아 BoxCollider를 연결한다.
        /// </summary>
        public static bool TrySetTriggerVolume(Component component, BoxCollider collider)
        {
            if (component == null || collider == null) return false;

            try
            {
                SerializedObject so = new SerializedObject(component);

                // Trigger volume 후보 property 이름 목록
                string[] candidateNames = new[] { "triggerVolume", "m_triggerVolume", "TriggerVolume",
                                                  "boxCollider", "m_boxCollider", "volume", "m_volume" };

                foreach (string name in candidateNames)
                {
                    SerializedProperty prop = so.FindProperty(name);
                    if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        prop.objectReferenceValue = collider;
                        so.ApplyModifiedProperties();
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// SerializedObject로 water level source enum을 찾아 Fixed/Manual/Custom으로 변경한다.
        /// enumDisplayNames를 확인하여 적절한 index를 찾는다.
        /// </summary>
        public static bool TryFixWaterLevelSource(Component component, float fixedWaterLevelY, GameObject generatedOcean)
        {
            if (component == null) return false;

            try
            {
                SerializedObject so = new SerializedObject(component);

                // Water level source 후보 property 이름 목록
                string[] sourceNames = new[] { "waterLevelSource", "m_waterLevelSource", "WaterLevelSource",
                                               "heightSource", "m_heightSource" };

                foreach (string name in sourceNames)
                {
                    SerializedProperty prop = so.FindProperty(name);
                    if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
                    {
                        // enumDisplayNames 확인
                        string[] displayNames = prop.enumDisplayNames;
                        for (int i = 0; i < displayNames.Length; i++)
                        {
                            string displayName = displayNames[i].ToLower();
                            // "Fixed", "Manual", "Custom", "Constant", "World" 중 매칭
                            if (displayName.Contains("fixed") || displayName.Contains("manual") ||
                                displayName.Contains("custom") || displayName.Contains("constant"))
                            {
                                prop.enumValueIndex = i;
                                so.ApplyModifiedProperties();
                                return true;
                            }
                        }

                        // Ocean -> Fixed 전환 시도: enumNames에서 "Fixed" 또는 "Manual" 찾기
                        string[] enumNames = prop.enumNames;
                        for (int i = 0; i < enumNames.Length; i++)
                        {
                            string enumName = enumNames[i].ToLower();
                            if (enumName.Contains("fixed") || enumName.Contains("manual") ||
                                enumName.Contains("custom") || enumName.Contains("constant"))
                            {
                                prop.enumValueIndex = i;
                                so.ApplyModifiedProperties();
                                return true;
                            }
                        }

                        // 매칭되는 enum 값이 없으면 변경하지 않음
                        return false;
                    }
                }

                // Ocean object reference 연결 시도 (generatedOcean이 있을 때만)
                if (generatedOcean != null)
                {
                    string[] oceanNames = new[] { "ocean", "m_ocean", "waterObject", "m_waterObject",
                                                  "water", "m_water" };
                    foreach (string name in oceanNames)
                    {
                        SerializedProperty prop = so.FindProperty(name);
                        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            // 타입 호환성 확인: try/catch로 안전하게 할당
                            try
                            {
                                prop.objectReferenceValue = generatedOcean;
                                so.ApplyModifiedProperties();
                                return true;
                            }
                            catch
                            {
                                // 타입 불일치 시 skip
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// SerializedObject로 float property를 찾아 값을 설정한다.
        /// </summary>
        public static bool TrySetFloatByPropertyNames(Component component, string[] names, float value)
        {
            if (component == null) return false;

            try
            {
                SerializedObject so = new SerializedObject(component);

                foreach (string name in names)
                {
                    SerializedProperty prop = so.FindProperty(name);
                    if (prop != null && prop.propertyType == SerializedPropertyType.Float)
                    {
                        prop.floatValue = value;
                        so.ApplyModifiedProperties();
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// SerializedObject의 모든 SerializedProperty를 로그로 출력한다.
        /// logUnderwaterAreaSerializedProperties가 true일 때만 동작한다.
        /// </summary>
        public static void LogSerializedPropertiesIfEnabled(Component component, bool enabled)
        {
            if (!enabled || component == null) return;

            try
            {
                SerializedObject so = new SerializedObject(component);
                var log = new StringBuilder();
                log.AppendLine($"SerializedProperties for {component.GetType().Name} on {component.gameObject.name}:");

                SerializedProperty iterator = so.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    log.AppendLine($"  path={iterator.propertyPath}, displayName={iterator.displayName}, type={iterator.propertyType}");
                    enterChildren = false;
                }

                Debug.Log(log.ToString());
            }
            catch
            {
                // 로그 출력 중 예외는 무시
            }
        }

        /// <summary>
        /// GeneratedWorldRoot/GlobalWater 아래에서 Ocean/Water GameObject를 찾는다.
        /// </summary>
        public static GameObject FindGeneratedOcean(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings == null) return null;

            GameObject generatedRoot = GameObject.Find(settings.GeneratedRootName);
            if (generatedRoot == null) return null;

            Transform globalWaterTransform = generatedRoot.transform.Find(settings.GlobalWaterRootName);
            if (globalWaterTransform == null) return null;

            // 1. StylizedWater3_OceanGrid_Generated 우선
            Transform exactMatch = globalWaterTransform.Find("StylizedWater3_OceanGrid_Generated");
            if (exactMatch != null) return exactMatch.gameObject;

            // 2. 이름에 "Ocean" 포함
            for (int i = 0; i < globalWaterTransform.childCount; i++)
            {
                Transform child = globalWaterTransform.GetChild(i);
                if (child.name.Contains("Ocean"))
                    return child.gameObject;
            }

            // 3. 이름에 "Water" 포함
            for (int i = 0; i < globalWaterTransform.childCount; i++)
            {
                Transform child = globalWaterTransform.GetChild(i);
                if (child.name.Contains("Water"))
                    return child.gameObject;
            }

            return null;
        }

        // ===== 템플릿 Resolve =====

        /// <summary>
        /// UnderwaterArea 템플릿을 resolve한다.
        /// 우선순위: SceneContext.UnderwaterAreaTemplate > SettingsSO.UnderwaterAreaPrefab > null
        /// </summary>
        public static GameObject ResolveUnderwaterTemplate(DeepLightMapAutoBuilderSettingsSO settings)
        {
            // SceneContext에서 템플릿 찾기
            // DeepLightMapAutoBuilderSceneContext는 Scene에 존재하는 MonoBehaviour
            DeepLightMapAutoBuilderSceneContext context = Object.FindFirstObjectByType<DeepLightMapAutoBuilderSceneContext>();
            if (context != null && context.UnderwaterAreaTemplate != null)
            {
                return context.UnderwaterAreaTemplate;
            }

            // SettingsSO Prefab fallback
            if (settings != null && settings.UnderwaterAreaPrefab != null)
            {
                return settings.UnderwaterAreaPrefab;
            }

            return null;
        }

        /// <summary>
        /// Zone 환경 데이터를 ScenarioPreset Rule 또는 default profile에서 resolve한다.
        /// </summary>
        public static ZoneEnvironmentData ResolveZoneEnvironmentData(WorldMapScenarioPresetSO preset, ZoneId zoneId)
        {
            ZoneEnvironmentData data = new ZoneEnvironmentData();

            // 기본값 설정
            data.WaterLevelY = 0f;
            data.MinDepth = -100f;
            data.MaxDepth = -600f;
            data.DepthBand = ZoneDepthBand.Mid;
            data.BiomeType = ZoneBiomeType.OpenWater;
            data.RuleName = "Default";
            data.DebugColor = Color.gray;
            data.SeafloorMaterial = null;
            data.UnderwaterAreaTemplatePrefab = null;

            if (preset == null)
                return data;

            // 1. ScenarioPreset Rule 조회
            if (preset.TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                data.RuleName = !string.IsNullOrEmpty(rule.RuleName) ? rule.RuleName : "UnnamedRule";
                data.DepthBand = rule.DepthBand;
                data.BiomeType = rule.BiomeType;
                data.MinDepth = rule.MinDepth;
                data.MaxDepth = rule.MaxDepth;
                data.DebugColor = rule.DebugColor;

                // 2. Rule에 EnvironmentProfile이 있으면 profile 값 사용
                if (rule.EnvironmentProfile != null)
                {
                    var profile = rule.EnvironmentProfile;
                    data.WaterLevelY = profile.WaterLevelY;
                    data.MinDepth = profile.MinDepth;
                    data.MaxDepth = profile.MaxDepth;
                    data.DepthBand = profile.DepthBand;
                    data.BiomeType = profile.BiomeType;
                    data.DebugColor = profile.DebugColor;
                    data.SeafloorMaterial = profile.SeafloorMaterial;
                    data.UnderwaterAreaTemplatePrefab = profile.UnderwaterAreaTemplatePrefab;
                }

                return data;
            }

            // 3. Rule이 없으면 DefaultOpenWaterProfile 사용
            ZoneEnvironmentProfileSO defaultProfile = preset.DefaultOpenWaterProfile;
            if (defaultProfile != null)
            {
                data.WaterLevelY = defaultProfile.WaterLevelY;
                data.MinDepth = defaultProfile.MinDepth;
                data.MaxDepth = defaultProfile.MaxDepth;
                data.DepthBand = defaultProfile.DepthBand;
                data.BiomeType = defaultProfile.BiomeType;
                data.DebugColor = defaultProfile.DebugColor;
                data.SeafloorMaterial = defaultProfile.SeafloorMaterial;
                data.UnderwaterAreaTemplatePrefab = defaultProfile.UnderwaterAreaTemplatePrefab;
                data.RuleName = "DefaultOpenWater";
            }

            return data;
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// UnderwaterArea GameObject의 BoxCollider를 보정한다.
        /// Phase 5.5: root BoxCollider를 우선 사용하고, 없으면 새로 추가한다.
        /// child collider가 있으면 root collider를 추가하고 경고를 남긴다.
        /// </summary>
        private static void AdjustUnderwaterColliderPhase55(GameObject underwaterObj, float zoneSize, float volumeHeight)
        {
            // root BoxCollider 우선
            BoxCollider rootCollider = underwaterObj.GetComponent<BoxCollider>();

            if (rootCollider == null)
            {
                // child에 BoxCollider가 있는지 확인
                BoxCollider childCollider = underwaterObj.GetComponentInChildren<BoxCollider>();
                if (childCollider != null)
                {
                    // child collider가 있으면 root에 새로 추가 (child collider는 그대로 둠)
                    rootCollider = underwaterObj.AddComponent<BoxCollider>();
                    Debug.Log($"[MapAutoBuilder] Added root BoxCollider to '{underwaterObj.name}' (child collider on '{childCollider.gameObject.name}' preserved).");
                }
                else
                {
                    // 없으면 root에 새로 추가
                    rootCollider = underwaterObj.AddComponent<BoxCollider>();
                }
            }

            // BoxCollider 설정
            rootCollider.isTrigger = true;
            rootCollider.size = new Vector3(zoneSize, volumeHeight, zoneSize);
            rootCollider.center = Vector3.zero;
        }

        /// <summary>
        /// 템플릿 GameObject를 복제한다.
        /// PrefabUtility.InstantiatePrefab을 우선 시도하고, 실패 시 Object.Instantiate fallback.
        /// </summary>
        private static GameObject CloneTemplate(GameObject template, Transform parent, string cloneName)
        {
            if (template == null) return null;

            GameObject clone = null;

            // PrefabUtility.InstantiatePrefab 시도
            if (PrefabUtility.GetPrefabAssetType(template) != PrefabAssetType.NotAPrefab)
            {
                clone = PrefabUtility.InstantiatePrefab(template) as GameObject;
            }

            // 실패하거나 Prefab이 아니면 Object.Instantiate fallback
            if (clone == null)
            {
                clone = Object.Instantiate(template);
            }

            if (clone == null)
            {
                Debug.LogError("[MapAutoBuilder] Failed to clone template!");
                return null;
            }

            // 복제본 설정
            clone.name = cloneName;
            clone.transform.SetParent(parent);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(clone, $"Clone {cloneName}");

            return clone;
        }

        /// <summary>
        /// Verbose 모드에서 샘플 Zone 3개의 환경 데이터를 로그로 출력한다.
        /// </summary>
        private static void LogSampleEnvironmentData(DeepLightMapAutoBuilderSettingsSO settings, GameObject zoneRootsParent)
        {
            if (settings.ScenarioPreset == null) return;

            // A1, F6, J10 샘플
            string[] sampleZoneIds = { "A1", "F6", "J10" };
            foreach (string zoneIdStr in sampleZoneIds)
            {
                if (ZoneId.TryParse(zoneIdStr, out ZoneId zoneId))
                {
                    ZoneEnvironmentData envData = ResolveZoneEnvironmentData(settings.ScenarioPreset, zoneId);
                    float topY = envData.WaterLevelY + settings.UnderwaterVolumeTopPadding;
                    float bottomY = envData.MaxDepth;
                    float volumeHeight = Mathf.Max(1f, topY - bottomY);
                    float centerY = (topY + bottomY) * 0.5f;

                    Debug.Log($"[MapAutoBuilder] Sample {zoneIdStr}: maxDepth={envData.MaxDepth}, topPadding={settings.UnderwaterVolumeTopPadding}, " +
                              $"topY={topY}, bottomY={bottomY}, volumeHeight={volumeHeight}, centerY={centerY}.");
                }
            }
        }

        /// <summary>
        /// Verbose 모드에서 샘플 Zone 3개의 volume correction 결과를 로그로 출력한다.
        /// </summary>
        private static void LogSampleVolumeCorrection(DeepLightMapAutoBuilderSettingsSO settings, GameObject zoneRootsParent)
        {
            if (settings.ScenarioPreset == null || zoneRootsParent == null) return;

            string[] sampleZoneIds = { "A1", "F6", "J10" };
            foreach (string zoneIdStr in sampleZoneIds)
            {
                if (!ZoneId.TryParse(zoneIdStr, out ZoneId zoneId)) continue;

                string zoneRootName = $"ZoneRoot_{zoneId}";
                Transform zoneTransform = zoneRootsParent.transform.Find(zoneRootName);
                if (zoneTransform == null) continue;

                Transform envTransform = zoneTransform.Find("Environment");
                if (envTransform == null) continue;
                Transform underwaterRoot = envTransform.Find("UnderwaterArea");
                if (underwaterRoot == null) continue;

                string underwaterName = $"UnderwaterArea_{zoneId}";
                Transform underwaterTransform = underwaterRoot.Find(underwaterName);
                if (underwaterTransform == null) continue;

                BoxCollider collider = underwaterTransform.GetComponent<BoxCollider>();
                if (collider == null) continue;

                // 월드 좌표 기준 volume range 계산
                Vector3 worldCenter = underwaterTransform.TransformPoint(collider.center);
                Vector3 worldSize = Vector3.Scale(collider.size, underwaterTransform.lossyScale);
                float worldTopY = worldCenter.y + worldSize.y * 0.5f;
                float worldBottomY = worldCenter.y - worldSize.y * 0.5f;

                Debug.Log($"[MapAutoBuilder] VolumeCorrection {zoneIdStr}: localPos=({underwaterTransform.localPosition.x:F1}, {underwaterTransform.localPosition.y:F1}, {underwaterTransform.localPosition.z:F1}), " +
                          $"collider.size=({collider.size.x:F1}, {collider.size.y:F1}, {collider.size.z:F1}), " +
                          $"worldRange Y: {worldBottomY:F1} ~ {worldTopY:F1}.");
            }
        }

        // ===== 검증 =====

        /// <summary>
        /// Seafloor placeholder를 찾는다. 다음 이름 형식을 모두 허용한다:
        /// 1. Seafloor_{zoneId}
        /// 2. Seafloor_{zoneId}_PLACEHOLDER
        /// 3. 이름이 "Seafloor_{zoneId}"로 시작하는 child
        /// direct child만 검색한다.
        /// </summary>
        private static Transform FindSeafloorPlaceholder(Transform seafloorRoot, string zoneIdStr)
        {
            if (seafloorRoot == null) return null;

            // 1. 정확한 이름 매치: Seafloor_A1
            string exactName = $"Seafloor_{zoneIdStr}";
            Transform exact = seafloorRoot.Find(exactName);
            if (exact != null) return exact;

            // 2. _PLACEHOLDER 접미사: Seafloor_A1_PLACEHOLDER
            string placeholderName = $"Seafloor_{zoneIdStr}_PLACEHOLDER";
            Transform placeholder = seafloorRoot.Find(placeholderName);
            if (placeholder != null) return placeholder;

            // 3. 이름이 "Seafloor_{zoneId}"로 시작하는 direct child 검색
            string prefix = $"Seafloor_{zoneIdStr}";
            for (int i = 0; i < seafloorRoot.childCount; i++)
            {
                Transform child = seafloorRoot.GetChild(i);
                if (child.name.StartsWith(prefix))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// Zone 환경 설정의 유효성을 검사한다.
        /// UnderwaterArea 존재 여부, BoxCollider 설정, volume range, Seafloor placeholder 등을 검사한다.
        /// </summary>
        public static void ValidateEnvironmentSetup(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings == null || settings.WorldMapConfig == null || settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings, WorldMapConfig, or ScenarioPreset is null. Cannot validate environment setup.");
                return;
            }

            var config = settings.WorldMapConfig;
            int gridWidth = config.GridWidth;
            int gridHeight = config.GridHeight;
            float zoneSize = config.ZoneSize;
            float tolerance = 0.1f;

            // Generated Root 찾기
            GameObject generatedRoot = GameObject.Find(settings.GeneratedRootName);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Cannot validate environment setup.");
                return;
            }

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError("[MapAutoBuilder] ZoneRoots not found. Cannot validate environment setup.");
                return;
            }

            var checks = new List<ValidationCheck>();
            int totalExpected = gridWidth * gridHeight;
            int underwaterFound = 0;
            int seafloorFound = 0;
            int colliderFound = 0;
            int colliderTriggerOk = 0;
            int colliderSizeOk = 0;

            // 누락된 Seafloor 이름을 기록할 리스트
            var missingSeafloorNames = new List<string>();

            // 10x10 그리드 순회
            for (int colIndex = 0; colIndex < gridWidth; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < gridHeight; rowIndex++)
                {
                    char columnChar = (char)('A' + colIndex);
                    int rowNumber = rowIndex + 1;
                    ZoneId zoneId = new ZoneId(columnChar, rowNumber);
                    string zoneIdStr = zoneId.ToString();

                    string zoneRootName = $"ZoneRoot_{zoneId}";
                    Transform zoneTransform = zoneRootsTransform.Find(zoneRootName);
                    if (zoneTransform == null) continue;

                    Transform envTransform = zoneTransform.Find("Environment");
                    if (envTransform == null) continue;

                    // UnderwaterArea 검사
                    Transform underwaterRoot = envTransform.Find("UnderwaterArea");
                    if (underwaterRoot != null)
                    {
                        string underwaterName = $"UnderwaterArea_{zoneId}";
                        Transform underwaterTransform = underwaterRoot.Find(underwaterName);
                        if (underwaterTransform != null)
                        {
                            underwaterFound++;

                            // BoxCollider 검사
                            BoxCollider collider = underwaterTransform.GetComponent<BoxCollider>();
                            if (collider != null)
                            {
                                colliderFound++;
                                if (collider.isTrigger) colliderTriggerOk++;
                                if (Mathf.Abs(collider.size.x - zoneSize) < tolerance &&
                                    Mathf.Abs(collider.size.z - zoneSize) < tolerance)
                                {
                                    colliderSizeOk++;
                                }
                            }
                        }
                    }

                    // Seafloor 검사 (FindSeafloorPlaceholder 사용)
                    Transform seafloorRoot = envTransform.Find("Seafloor");
                    if (seafloorRoot != null)
                    {
                        Transform seafloorTransform = FindSeafloorPlaceholder(seafloorRoot, zoneIdStr);
                        if (seafloorTransform != null)
                        {
                            seafloorFound++;
                        }
                        else
                        {
                            missingSeafloorNames.Add(zoneIdStr);
                        }
                    }
                    else
                    {
                        missingSeafloorNames.Add(zoneIdStr);
                    }
                }
            }

            // 기본 검사 추가
            checks.Add(new ValidationCheck("GeneratedWorldRoot exists", generatedRoot != null, ""));
            checks.Add(new ValidationCheck("ZoneRoots exists", zoneRootsTransform != null, ""));

            // Seafloor 누락 메시지 생성
            string seafloorFailMessage = "";
            if (seafloorFound < totalExpected)
            {
                seafloorFailMessage = $"Missing {totalExpected - seafloorFound} Seafloor(s).";
                if (missingSeafloorNames.Count > 0 && missingSeafloorNames.Count <= 10)
                {
                    seafloorFailMessage += $" Missing: {string.Join(", ", missingSeafloorNames)}";
                }
            }

            checks.Add(new ValidationCheck($"UnderwaterArea count: {underwaterFound}/{totalExpected}", underwaterFound == totalExpected,
                underwaterFound < totalExpected ? $"Missing {totalExpected - underwaterFound} UnderwaterArea(s)." : ""));
            checks.Add(new ValidationCheck($"Seafloor count: {seafloorFound}/{totalExpected}", seafloorFound == totalExpected,
                seafloorFailMessage));
            checks.Add(new ValidationCheck($"BoxCollider count: {colliderFound}/{totalExpected}", colliderFound == totalExpected,
                colliderFound < totalExpected ? $"Missing {totalExpected - colliderFound} BoxCollider(s)." : ""));
            checks.Add(new ValidationCheck($"BoxCollider.isTrigger: {colliderTriggerOk}/{totalExpected}", colliderTriggerOk == totalExpected,
                colliderTriggerOk < totalExpected ? $"{totalExpected - colliderTriggerOk} collider(s) not trigger." : ""));
            checks.Add(new ValidationCheck($"BoxCollider size (X,Z): {colliderSizeOk}/{totalExpected}", colliderSizeOk == totalExpected,
                colliderSizeOk < totalExpected ? $"{totalExpected - colliderSizeOk} collider(s) wrong size." : ""));

            // Phase 5.5: Volume range 검사 (A1, F6, J10 샘플)
            string[] sampleZoneIds = { "A1", "F6", "J10" };
            foreach (string zoneIdStr in sampleZoneIds)
            {
                if (!ZoneId.TryParse(zoneIdStr, out ZoneId zoneId)) continue;

                string zoneRootName = $"ZoneRoot_{zoneId}";
                Transform zoneTransform = zoneRootsTransform.Find(zoneRootName);
                if (zoneTransform == null) continue;

                Transform envTransform = zoneTransform.Find("Environment");
                if (envTransform == null) continue;
                Transform underwaterRoot = envTransform.Find("UnderwaterArea");
                if (underwaterRoot == null) continue;

                string underwaterName = $"UnderwaterArea_{zoneId}";
                Transform underwaterTransform = underwaterRoot.Find(underwaterName);
                if (underwaterTransform == null) continue;

                BoxCollider collider = underwaterTransform.GetComponent<BoxCollider>();
                if (collider == null) continue;

                // 월드 좌표 기준 volume range
                Vector3 worldCenter = underwaterTransform.TransformPoint(collider.center);
                Vector3 worldSize = Vector3.Scale(collider.size, underwaterTransform.lossyScale);
                float worldTopY = worldCenter.y + worldSize.y * 0.5f;
                float worldBottomY = worldCenter.y - worldSize.y * 0.5f;

                // Zone 환경 데이터로 expected range 계산
                ZoneEnvironmentData envData = ResolveZoneEnvironmentData(settings.ScenarioPreset, zoneId);
                float expectedTopY = envData.WaterLevelY + settings.UnderwaterVolumeTopPadding;
                float expectedBottomY = envData.MaxDepth;

                bool topOk = worldTopY >= expectedTopY - tolerance;
                bool bottomOk = worldBottomY <= expectedBottomY + tolerance;

                checks.Add(new ValidationCheck(
                    $"UnderwaterArea_{zoneIdStr} volume top Y: {worldTopY:F1} >= {expectedTopY:F1}",
                    topOk,
                    topOk ? "" : $"Volume top {worldTopY:F1} is below expected {expectedTopY:F1}."));

                checks.Add(new ValidationCheck(
                    $"UnderwaterArea_{zoneIdStr} volume bottom Y: {worldBottomY:F1} <= {expectedBottomY:F1}",
                    bottomOk,
                    bottomOk ? "" : $"Volume bottom {worldBottomY:F1} is above expected {expectedBottomY:F1}."));

                Debug.Log($"[MapAutoBuilder] UnderwaterArea_{zoneIdStr} volume range Y: {worldBottomY:F1} ~ {worldTopY:F1}, expected top >= {expectedTopY:F1}.");
            }

            // Phase 5.5: 샘플 Seafloor 위치 검사 (A1, F6, J10)
            foreach (string zoneIdStr in sampleZoneIds)
            {
                if (!ZoneId.TryParse(zoneIdStr, out ZoneId zoneId)) continue;

                string zoneRootName = $"ZoneRoot_{zoneId}";
                Transform zoneTransform = zoneRootsTransform.Find(zoneRootName);
                if (zoneTransform == null) continue;

                Transform envTransform = zoneTransform.Find("Environment");
                if (envTransform == null) continue;
                Transform seafloorRoot = envTransform.Find("Seafloor");
                if (seafloorRoot == null) continue;

                Transform seafloorTransform = FindSeafloorPlaceholder(seafloorRoot, zoneIdStr);
                if (seafloorTransform == null)
                {
                    checks.Add(new ValidationCheck($"Seafloor_{zoneIdStr} exists", false, $"Seafloor_{zoneIdStr} not found."));
                    continue;
                }

                checks.Add(new ValidationCheck($"Seafloor_{zoneIdStr} exists", true, ""));

                // Seafloor localPosition.y 검사 (ZoneRoot 기준, ZoneRoot y=0이므로 world y와 동일)
                ZoneEnvironmentData envData = ResolveZoneEnvironmentData(settings.ScenarioPreset, zoneId);
                float expectedSeafloorY = envData.MaxDepth;
                float actualSeafloorY = seafloorTransform.localPosition.y;
                bool seafloorYOk = Mathf.Abs(actualSeafloorY - expectedSeafloorY) < 10f; // 10m tolerance

                checks.Add(new ValidationCheck(
                    $"Seafloor_{zoneIdStr} localPosition.y: {actualSeafloorY:F1} ~ {expectedSeafloorY:F1}",
                    seafloorYOk,
                    seafloorYOk ? "" : $"Seafloor Y {actualSeafloorY:F1} is far from expected {expectedSeafloorY:F1}."));
            }

            // 결과 출력
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Environment Setup =====");

            int passedCount = 0;
            int failedCount = 0;
            foreach (var check in checks)
            {
                string status = check.Passed ? "[OK]" : "[FAIL]";
                log.AppendLine($"  {status} {check.Label}");
                if (!string.IsNullOrEmpty(check.Message))
                {
                    log.AppendLine($"       {check.Message}");
                }
                if (check.Passed) passedCount++;
                else failedCount++;
            }

            log.AppendLine($"\nResult: {passedCount}/{checks.Count} passed, {failedCount} failed.");
            if (failedCount == 0)
            {
                log.AppendLine("Environment setup validation PASSED.");
                log.AppendLine($"[MapAutoBuilder] Environment validation passed. {underwaterFound}/{totalExpected} UnderwaterAreas, {seafloorFound}/{totalExpected} Seafloor placeholders.");
            }
            else
            {
                log.AppendLine("Environment setup validation FAILED. Check above for details.");
            }

            // Inspector 경고 안내 (실패로 취급하지 않음)
            log.AppendLine("");
            log.AppendLine("[INFO] The following warnings are Stylized Water configuration warnings and are not treated as Phase 5.5 generation failures:");
            log.AppendLine("[INFO]   - Opaque texture not rendering at full resolution (URP quality setting)");
            log.AppendLine("[INFO]   - Camera not inside any underwater area (SceneCamera not in volume)");
            log.AppendLine("[INFO]   - Environment Audio Mixer missing (audio setup not yet configured)");
            log.AppendLine("[INFO] These will be handled in later rendering/audio setup phases.");

            log.AppendLine("===== Validate Environment Setup Complete =====");

            Debug.Log(log.ToString());
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


