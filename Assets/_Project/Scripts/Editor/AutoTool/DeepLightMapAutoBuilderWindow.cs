using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// DeepLight Map Auto Builder의 Editor Window.
    /// SettingsSO(Project Asset) + SceneContext(Scene MonoBehaviour)를 함께 관리한다.
    /// 모든 Hierarchy 조작은 DeepLightMapAutoBuilder 정적 클래스에 위임한다.
    /// </summary>
    public class DeepLightMapAutoBuilderWindow : EditorWindow
    {
        private DeepLightMapAutoBuilderSettingsSO _settings;
        private DeepLightMapAutoBuilderSceneContext _context;
        private Vector2 _scrollPosition;

        // ===== GUI 스타일 =====
        private GUIStyle _titleStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _helpBoxStyle;

        /// <summary>
        /// 메뉴에서 Window 열기
        /// </summary>
        [MenuItem("Tools/DeepLight/World Map/Open Map Auto Builder")]
        private static void OpenWindow()
        {
            var window = GetWindow<DeepLightMapAutoBuilderWindow>("DeepLight Map Auto Builder");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }

        private void OnEnable()
        {
            // GUI 스타일 초기화
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.6f, 0.9f) }
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            _helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true
            };
        }

        private void OnGUI()
        {
            // try/finally로 GUILayout Begin/End 쌍이 예외 발생 시에도 깨지지 않도록 보호
            try
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                DrawHeader();
                DrawSettingsSection();
                DrawContextSection();
                DrawActionButtons();
                DrawHelpSection();
            }
            finally
            {
                // EndScrollView가 반드시 호출되도록 보장
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// Window 상단 타이틀 영역
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("DeepLight Map Auto Builder", _titleStyle);
            EditorGUILayout.LabelField("Phase 3: Base Root Structure Generation", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// SettingsSO ObjectField 및 상태 표시 영역
        /// </summary>
        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("Settings (Project Asset)", _headerStyle);
            EditorGUILayout.Space(3);

            // SettingsSO ObjectField
            _settings = (DeepLightMapAutoBuilderSettingsSO)EditorGUILayout.ObjectField(
                "Settings SO",
                _settings,
                typeof(DeepLightMapAutoBuilderSettingsSO),
                false);

            EditorGUILayout.Space(3);

            // Create/Auto Fill Settings Asset 버튼 (항상 활성)
            if (GUILayout.Button("Create/Auto Fill Settings Asset", GUILayout.Height(22)))
            {
                ExecuteCreateOrAutoFillSettings();
            }

            EditorGUILayout.Space(3);

            // Settings 상태 표시
            if (_settings == null)
            {
                EditorGUILayout.HelpBox(
                    "SettingsSO가 연결되지 않았습니다.\n" +
                    "\"Create/Auto Fill Settings Asset\" 버튼을 먼저 눌러주세요.\n" +
                    "또는 우클릭 > Create > DeepLight > World > Map Auto Builder Settings 로 생성 후 연결하세요.",
                    MessageType.Warning);
            }
            else
            {
                DrawSettingsStatus();

                // 누락 필드 안내
                if (_settings.WorldMapConfig == null || _settings.ScenarioPreset == null)
                {
                    EditorGUILayout.HelpBox(
                        "WorldMapConfig 또는 ScenarioPreset이 누락되었습니다.\n" +
                        "\"Create/Auto Fill Settings Asset\" 버튼을 다시 눌러 자동 연결하세요.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// SettingsSO의 현재 상태를 표시한다.
        /// </summary>
        private void DrawSettingsStatus()
        {
            EditorGUILayout.LabelField("Settings Status", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;

            // WorldMapConfig
            if (_settings.WorldMapConfig != null)
            {
                EditorGUILayout.LabelField($"WorldMapConfig: {_settings.WorldMapConfig.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("WorldMapConfig: [NULL]", EditorStyles.miniLabel);
            }

            // ScenarioPreset
            if (_settings.ScenarioPreset != null)
            {
                int ruleCount = _settings.ScenarioPreset.ZoneRules != null
                    ? _settings.ScenarioPreset.ZoneRules.Count
                    : 0;
                EditorGUILayout.LabelField($"ScenarioPreset: {_settings.ScenarioPreset.name} ({ruleCount} rules)", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("ScenarioPreset: [NULL]", EditorStyles.miniLabel);
            }

            // Root Name
            EditorGUILayout.LabelField($"Root Name: {_settings.GeneratedRootName}", EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// SceneContext ObjectField 및 상태 표시 영역
        /// </summary>
        private void DrawContextSection()
        {
            EditorGUILayout.LabelField("Scene Context (Scene Object)", _headerStyle);
            EditorGUILayout.Space(3);

            // SceneContext ObjectField
            _context = (DeepLightMapAutoBuilderSceneContext)EditorGUILayout.ObjectField(
                "Scene Context",
                _context,
                typeof(DeepLightMapAutoBuilderSceneContext),
                true);

            EditorGUILayout.Space(3);

            // Scene Context 버튼들
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create/Find Scene Context", GUILayout.Height(22)))
            {
                ExecuteCreateOrFindSceneContext();
            }
            if (GUILayout.Button("Auto Fill Scene Context", GUILayout.Height(22)))
            {
                ExecuteAutoFillSceneContext();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Scene Context 상태 표시
            if (_context == null)
            {
                EditorGUILayout.HelpBox(
                    "SceneContext가 연결되지 않았습니다.\n" +
                    "\"Create/Find Scene Context\" 버튼을 눌러 생성하세요.\n" +
                    "그 후 \"Auto Fill Scene Context\"로 템플릿을 자동 연결하세요.",
                    MessageType.Warning);
            }
            else
            {
                DrawContextStatus();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// SceneContext의 현재 상태를 표시한다.
        /// 삭제된 GameObject 참조(fake null)를 안전하게 처리한다.
        /// </summary>
        private void DrawContextStatus()
        {
            EditorGUILayout.LabelField("Scene Context Status", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;

            // MapSettingsTemplate (fake null 체크 포함)
            GameObject mapSettings = _context.MapSettingsTemplateRoot;
            if (mapSettings != null)
            {
                EditorGUILayout.LabelField($"MapSettingsTemplate: {mapSettings.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("MapSettingsTemplate: [NULL]", EditorStyles.miniLabel);
            }

            // GlobalWaterTemplate (fake null 체크 포함)
            GameObject globalWater = _context.GlobalWaterTemplate;
            if (globalWater != null)
            {
                EditorGUILayout.LabelField($"GlobalWaterTemplate: {globalWater.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("GlobalWaterTemplate: [NULL]", EditorStyles.miniLabel);
            }

            // UnderwaterAreaTemplate (fake null 체크 포함)
            GameObject underwater = _context.UnderwaterAreaTemplate;
            if (underwater != null)
            {
                EditorGUILayout.LabelField($"UnderwaterAreaTemplate: {underwater.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("UnderwaterAreaTemplate: [NULL] (Phase 4+)", EditorStyles.miniLabel);
            }

            // BubbleParticleTemplate (fake null 체크 포함)
            GameObject bubble = _context.BubbleParticleTemplate;
            if (bubble != null)
            {
                EditorGUILayout.LabelField($"BubbleParticleTemplate: {bubble.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("BubbleParticleTemplate: [NULL] (Phase 4+)", EditorStyles.miniLabel);
            }

            // DynamicEffectTemplate (fake null 체크 포함)
            GameObject dynamicEffect = _context.DynamicEffectTemplate;
            if (dynamicEffect != null)
            {
                EditorGUILayout.LabelField($"DynamicEffectTemplate: {dynamicEffect.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("DynamicEffectTemplate: [NULL] (Phase 4+)", EditorStyles.miniLabel);
            }

            // GeneratedRootOverride (fake null 체크 포함)
            GameObject rootOverride = _context.GeneratedRootOverride;
            if (rootOverride != null)
            {
                EditorGUILayout.LabelField($"Root Override: {rootOverride.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Root Override: [NULL] (name-based)", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 실행 버튼 영역
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("Actions", _headerStyle);
            EditorGUILayout.Space(3);

            // Create/Auto Fill All
            if (GUILayout.Button("Create/Auto Fill All (Settings + Context)", GUILayout.Height(25)))
            {
                ExecuteCreateOrAutoFillAll();
            }

            EditorGUILayout.Space(3);

            // Validate Settings + Context
            GUI.enabled = _settings != null;
            if (GUILayout.Button("Validate Settings + Context", GUILayout.Height(30)))
            {
                ExecuteValidate();
            }

            EditorGUILayout.Space(2);

            // Dry Run Preview
            if (GUILayout.Button("Dry Run Preview", GUILayout.Height(30)))
            {
                ExecuteDryRun();
            }

            EditorGUILayout.Space(2);

            // Generate Full Scenario Map
            GUI.color = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Generate Full Scenario Map", GUILayout.Height(35)))
            {
                ExecuteGenerate();
            }
            GUI.color = Color.white;

            EditorGUILayout.Space(2);

            // Ping Assigned Objects
            GUI.enabled = _settings != null || _context != null;
            if (GUILayout.Button("Ping Assigned Objects", GUILayout.Height(25)))
            {
                ExecutePingAssignedObjects();
            }

            EditorGUILayout.Space(5);

            // Clear Generated Map
            GUI.color = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Clear Generated Map", GUILayout.Height(30)))
            {
                ExecuteClear();
            }
            GUI.color = Color.white;

            GUI.enabled = true;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// 도움말 영역
        /// </summary>
        private void DrawHelpSection()
        {
            EditorGUILayout.LabelField("Manual Setup Guide", _headerStyle);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginVertical(_helpBoxStyle);

            // 중요: 수동 할당 권장
            EditorGUILayout.LabelField(
                "⚠️ SceneContext 수동 할당이 가장 안전한 경로입니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "1. SceneContext의 GameObject 필드를 Inspector에서 직접 할당하세요.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "2. \"Auto Fill Scene Context\"는 초기 편의 기능이며, 이미 할당된 필드는 덮어쓰지 않습니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "3. SettingsSO의 \"Project Prefab Templates\"는 SceneContext 미할당 시 fallback으로 사용됩니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "4. 템플릿 resolve 우선순위: SceneContext 직접 할당 > SettingsSO Prefab Asset > skip",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "5. 기존 Hierarchy(MapSettings, UIRoot 등)는 절대 수정하지 않음",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        // ===== 실행 메서드 =====

        /// <summary>
        /// SettingsSO만 생성/보정
        /// </summary>
        private void ExecuteCreateOrAutoFillSettings()
        {
            DeepLightMapAutoBuilderSettingsSO settings = DeepLightMapAutoBuilderSettingsCreator.CreateOrAutoFillSettingsAsset();
            if (settings != null)
            {
                _settings = settings;
                Debug.Log($"[MapAutoBuilder] SettingsSO assigned to Window: {settings.name}");
            }
        }

        /// <summary>
        /// SceneContext 생성/탐색
        /// </summary>
        private void ExecuteCreateOrFindSceneContext()
        {
            DeepLightMapAutoBuilderSceneContext context = DeepLightMapAutoBuilderSettingsCreator.CreateOrFindSceneContext();
            if (context != null)
            {
                _context = context;
                Selection.activeGameObject = context.gameObject;
                EditorGUIUtility.PingObject(context.gameObject);
                Debug.Log($"[MapAutoBuilder] SceneContext assigned to Window: {context.name}");
            }
        }

        /// <summary>
        /// SceneContext 자동 연결
        /// </summary>
        private void ExecuteAutoFillSceneContext()
        {
            if (_context == null)
            {
                Debug.LogError("[MapAutoBuilder] SceneContext is null! Create/Find Scene Context first.");
                return;
            }
            DeepLightMapAutoBuilderSettingsCreator.AutoFillSceneContextFromCurrentScene(_context);
        }

        /// <summary>
        /// SettingsSO + SceneContext 한 번에 생성/보정
        /// </summary>
        private void ExecuteCreateOrAutoFillAll()
        {
            DeepLightMapAutoBuilderSettingsSO settings;
            DeepLightMapAutoBuilderSceneContext context;
            DeepLightMapAutoBuilderSettingsCreator.CreateOrAutoFillAll(out settings, out context);
            if (settings != null)
            {
                _settings = settings;
            }
            if (context != null)
            {
                _context = context;
            }
            Debug.Log("[MapAutoBuilder] Create/Auto Fill All complete.");
        }

        /// <summary>
        /// Validate Settings + Context 실행
        /// </summary>
        private void ExecuteValidate()
        {
            if (_settings == null)
            {
                Debug.LogError("[MapAutoBuilder] SettingsSO is null! Assign a SettingsSO first.");
                return;
            }

            DeepLightMapAutoBuilder.ValidateSettings(_settings, _context);
        }

        /// <summary>
        /// Dry Run Preview 실행
        /// </summary>
        private void ExecuteDryRun()
        {
            if (_settings == null)
            {
                Debug.LogError("[MapAutoBuilder] SettingsSO is null! Assign a SettingsSO first.");
                return;
            }

            DeepLightMapAutoBuilder.DryRunPreview(_settings, _context);
        }

        /// <summary>
        /// Generate Full Scenario Map 실행
        /// </summary>
        private void ExecuteGenerate()
        {
            if (_settings == null)
            {
                Debug.LogError("[MapAutoBuilder] SettingsSO is null! Assign a SettingsSO first.");
                return;
            }

            DeepLightMapAutoBuilder.GenerateFullScenarioMap(_settings, _context);
        }

        /// <summary>
        /// Ping Assigned Objects 실행: SettingsSO + SceneContext의 모든 참조를 순서대로 ping/log
        /// </summary>
        private void ExecutePingAssignedObjects()
        {
            Debug.Log("===== Map Auto Builder: Ping Assigned Objects =====");

            // 1. WorldMapConfig (Project asset)
            if (_settings != null && _settings.WorldMapConfig != null)
            {
                Selection.activeObject = _settings.WorldMapConfig;
                EditorGUIUtility.PingObject(_settings.WorldMapConfig);
                Debug.Log($"[PING] WorldMapConfig: {_settings.WorldMapConfig.name}");
            }
            else
            {
                Debug.Log("[PING] WorldMapConfig: NULL");
            }

            // 2. ScenarioPreset (Project asset)
            if (_settings != null && _settings.ScenarioPreset != null)
            {
                Selection.activeObject = _settings.ScenarioPreset;
                EditorGUIUtility.PingObject(_settings.ScenarioPreset);
                Debug.Log($"[PING] ScenarioPreset: {_settings.ScenarioPreset.name}");
            }
            else
            {
                Debug.Log("[PING] ScenarioPreset: NULL");
            }

            // 3. SceneContext GameObject
            if (_context != null)
            {
                Selection.activeGameObject = _context.gameObject;
                EditorGUIUtility.PingObject(_context.gameObject);
                Debug.Log($"[PING] SceneContext: {_context.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] SceneContext: NULL");
            }

            // 4. MapSettingsTemplateRoot (Scene object)
            if (_context != null && _context.MapSettingsTemplateRoot != null)
            {
                Selection.activeGameObject = _context.MapSettingsTemplateRoot;
                EditorGUIUtility.PingObject(_context.MapSettingsTemplateRoot);
                Debug.Log($"[PING] MapSettingsTemplateRoot: {_context.MapSettingsTemplateRoot.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] MapSettingsTemplateRoot: NULL");
            }

            // 5. GlobalWaterTemplate (Scene object)
            if (_context != null && _context.GlobalWaterTemplate != null)
            {
                Selection.activeGameObject = _context.GlobalWaterTemplate;
                EditorGUIUtility.PingObject(_context.GlobalWaterTemplate);
                Debug.Log($"[PING] GlobalWaterTemplate: {_context.GlobalWaterTemplate.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] GlobalWaterTemplate: NULL");
            }

            // 6. UnderwaterAreaTemplate (Scene object)
            if (_context != null && _context.UnderwaterAreaTemplate != null)
            {
                Selection.activeGameObject = _context.UnderwaterAreaTemplate;
                EditorGUIUtility.PingObject(_context.UnderwaterAreaTemplate);
                Debug.Log($"[PING] UnderwaterAreaTemplate: {_context.UnderwaterAreaTemplate.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] UnderwaterAreaTemplate: NULL");
            }

            // 7. BubbleParticleTemplate (Scene object)
            if (_context != null && _context.BubbleParticleTemplate != null)
            {
                Selection.activeGameObject = _context.BubbleParticleTemplate;
                EditorGUIUtility.PingObject(_context.BubbleParticleTemplate);
                Debug.Log($"[PING] BubbleParticleTemplate: {_context.BubbleParticleTemplate.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] BubbleParticleTemplate: NULL");
            }

            // 8. DynamicEffectTemplate (Scene object)
            if (_context != null && _context.DynamicEffectTemplate != null)
            {
                Selection.activeGameObject = _context.DynamicEffectTemplate;
                EditorGUIUtility.PingObject(_context.DynamicEffectTemplate);
                Debug.Log($"[PING] DynamicEffectTemplate: {_context.DynamicEffectTemplate.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] DynamicEffectTemplate: NULL");
            }

            // 9. GeneratedRootOverride (Scene object)
            if (_context != null && _context.GeneratedRootOverride != null)
            {
                Selection.activeGameObject = _context.GeneratedRootOverride;
                EditorGUIUtility.PingObject(_context.GeneratedRootOverride);
                Debug.Log($"[PING] GeneratedRootOverride: {_context.GeneratedRootOverride.name} (Scene)");
            }
            else
            {
                Debug.Log("[PING] GeneratedRootOverride: NULL (will use name-based lookup)");
            }

            Debug.Log("===== Ping Complete =====");
        }

        /// <summary>
        /// Clear Generated Map 실행 (확인 대화상자 포함)
        /// </summary>
        private void ExecuteClear()
        {
            if (_settings == null)
            {
                Debug.LogError("[MapAutoBuilder] SettingsSO is null! Assign a SettingsSO first.");
                return;
            }

            // 확인 대화상자
            string rootName = _settings.GeneratedRootName;
            if (_context != null && _context.GeneratedRootOverride != null)
            {
                rootName = _context.GeneratedRootOverride.name;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Generated Map",
                $"정말로 '{rootName}'을(를) 삭제하시겠습니까?\n\n" +
                "이 작업은 Undo로 되돌릴 수 있습니다.\n" +
                "보호된 오브젝트(MapSettings, UIRoot 등)는 절대 삭제되지 않습니다.",
                "삭제",
                "취소");

            if (confirmed)
            {
                DeepLightMapAutoBuilder.ClearGeneratedMap(_settings, _context);
                // Clear 후 Window 상태 갱신 (삭제된 참조가 UI에 표시되지 않도록)
                Repaint();
            }
            else
            {
                Debug.Log("[MapAutoBuilder] Clear cancelled by user.");
            }
        }
    }
}
