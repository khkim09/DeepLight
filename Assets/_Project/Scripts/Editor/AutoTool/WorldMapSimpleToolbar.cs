using UnityEditor;
using UnityEngine;

namespace Project.Editor.AutoTool
{
    /// <summary>월드맵 시스템 간단한 툴바 버튼 (에디터 윈도우 방식)</summary>
    public class WorldMapSimpleToolbar : EditorWindow
    {
        private static bool _showToolbar = true;

        [MenuItem("Tools/World Map/Show Toolbar")]
        public static void ShowToolbar()
        {
            _showToolbar = true;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/World Map/Hide Toolbar")]
        public static void HideToolbar()
        {
            _showToolbar = false;
            SceneView.RepaintAll();
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_showToolbar) return;

            Handles.BeginGUI();

            // Calculate position (top center of scene view)
            float width = 200;
            float height = 40;
            float x = (sceneView.position.width - width) / 2;
            float y = 10;

            GUILayout.BeginArea(new Rect(x, y, width, height));

            GUILayout.BeginHorizontal("box");

            // Quick Setup Button
            if (GUILayout.Button("World Map\nQuick Setup", GUILayout.Height(35)))
            {
                WorldMapSetupTool.QuickSetupAll();
            }

            // Open Setup Tool Button
            if (GUILayout.Button("Setup\nTool", GUILayout.Height(35)))
            {
                WorldMapSetupTool.ShowWindow();
            }

            // Hide Button
            if (GUILayout.Button("Hide", GUILayout.Height(35)))
            {
                _showToolbar = false;
                sceneView.Repaint();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Handles.EndGUI();
        }
    }

    /// <summary>월드맵 시스템 컨텍스트 메뉴</summary>
    public static class WorldMapContextMenu
    {
        [MenuItem("GameObject/World Map/Create Debug Setup", false, 10)]
        public static void CreateDebugSetup()
        {
            WorldMapSetupTool.QuickSetupAll();
        }

        [MenuItem("GameObject/World Map/Open Setup Tool", false, 11)]
        public static void OpenSetupTool()
        {
            WorldMapSetupTool.ShowWindow();
        }

        [MenuItem("GameObject/World Map/Test Current Zone", false, 20)]
        public static void TestCurrentZone()
        {
            var testRoot = GameObject.Find("WorldMapSystemTestRoot");
            if (testRoot != null)
            {
                var testComponent = testRoot.GetComponent<Project.Gameplay.World.WorldMapSystemTest>();
                if (testComponent != null)
                {
                    testComponent.PrintCurrentZoneInfo();
                }
                else
                {
                    Debug.LogWarning("[WorldMapContextMenu] WorldMapSystemTest component not found on WorldMapSystemTestRoot");
                }
            }
            else
            {
                Debug.LogWarning("[WorldMapContextMenu] WorldMapSystemTestRoot not found in scene");
            }
        }

        [MenuItem("GameObject/World Map/Run All Tests", false, 21)]
        public static void RunAllTests()
        {
            var testRoot = GameObject.Find("WorldMapSystemTestRoot");
            if (testRoot != null)
            {
                var testComponent = testRoot.GetComponent<Project.Gameplay.World.WorldMapSystemTest>();
                if (testComponent != null)
                {
                    testComponent.RunTestsInEditor();
                }
                else
                {
                    Debug.LogWarning("[WorldMapContextMenu] WorldMapSystemTest component not found on WorldMapSystemTestRoot");
                }
            }
            else
            {
                Debug.LogWarning("[WorldMapContextMenu] WorldMapSystemTestRoot not found in scene");
            }
        }

        // ===== Phase 10: Zone Content Activation =====

        [MenuItem("GameObject/World Map/Phase 10/Create Zone Content Controller", false, 30)]
        public static void CreateZoneContentController()
        {
            WorldMapSetupTool.CreateZoneContentControllerObject();
        }

        [MenuItem("GameObject/World Map/Phase 10/Create Sample Bound Content", false, 31)]
        public static void CreateSampleBoundContent()
        {
            WorldMapSetupTool.CreateSampleBoundContent();
        }

        [MenuItem("GameObject/World Map/Phase 10/Connect Controller to Installer", false, 32)]
        public static void ConnectControllerToInstaller()
        {
            WorldMapSetupTool.RewireInstallerReferences();
        }

        [MenuItem("GameObject/World Map/Phase 10/Refresh Content Activation", false, 33)]
        public static void RefreshContentActivation()
        {
            var controllerObj = GameObject.Find("WorldMapZoneContentController");
            if (controllerObj != null)
            {
                var controller = controllerObj.GetComponent<Project.Gameplay.World.WorldMapZoneContentController>();
                if (controller != null)
                {
                    controller.RefreshBindings();
                    Debug.Log("[WorldMapContextMenu] Content activation refreshed.");
                }
                else
                {
                    Debug.LogWarning("[WorldMapContextMenu] WorldMapZoneContentController component not found.");
                }
            }
            else
            {
                Debug.LogWarning("[WorldMapContextMenu] WorldMapZoneContentController object not found in scene.");
            }
        }

        [MenuItem("GameObject/World Map/Phase 10/Validate Bindings", false, 34)]
        public static void ValidateBindings()
        {
            var controllerObj = GameObject.Find("WorldMapZoneContentController");
            if (controllerObj != null)
            {
                var controller = controllerObj.GetComponent<Project.Gameplay.World.WorldMapZoneContentController>();
                if (controller != null)
                {
                    controller.ValidateBindings();
                    Debug.Log("[WorldMapContextMenu] Bindings validated.");
                }
                else
                {
                    Debug.LogWarning("[WorldMapContextMenu] WorldMapZoneContentController component not found.");
                }
            }
            else
            {
                Debug.LogWarning("[WorldMapContextMenu] WorldMapZoneContentController object not found in scene.");
            }
        }

        // ===== Phase 3: HUD + MiniGrid =====

        [MenuItem("GameObject/World Map/Phase 3/Create HUD Only", false, 40)]
        public static void CreateHUDOnly()
        {
            WorldMapHUDHelper.CreateUpdateHUDOnly();
        }

        [MenuItem("GameObject/World Map/Phase 3/Create HUD UI Elements Only", false, 41)]
        public static void CreateHUDUIElementsOnly()
        {
            WorldMapHUDHelper.CreateHUDUIElementsOnly();
        }

        [MenuItem("GameObject/World Map/Phase 3/Create MiniGrid Only", false, 42)]
        public static void CreateMiniGridOnly()
        {
            WorldMapHUDHelper.CreateUpdateMiniGridOnly();
        }

        [MenuItem("GameObject/World Map/Phase 3/Validate HUD & MiniGrid", false, 43)]
        public static void ValidateHUDAndMiniGrid()
        {
            WorldMapHUDHelper.ValidateHUDAndMiniGrid();
        }

        [MenuItem("GameObject/World Map/Phase 3/Setup MiniGrid Submarine-Relative", false, 44)]
        public static void SetupMiniGridSubmarineRelative()
        {
            WorldMapHUDHelper.SetupMiniGridSubmarineRelative();
        }
    }
}
