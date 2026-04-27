using UnityEditor;
using UnityEngine;

namespace Project.Editor.AutoTool
{
    /// <summary>월드맵 시스템 툴바 버튼 (간소화된 버전)</summary>
    public static class WorldMapToolbarButton
    {
        private const string TOOLBAR_BUTTON_TEXT = "World Map Setup";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Unity 2020.3+에서는 SceneView.duringSceneGui를 사용
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // 툴바 영역 계산 (상단 중앙)
            float buttonWidth = 150;
            float buttonHeight = 25;
            float x = (sceneView.position.width - buttonWidth) / 2;
            float y = 5;

            Handles.BeginGUI();

            // 배경 영역
            GUILayout.BeginArea(new Rect(x, y, buttonWidth, buttonHeight));

            // 버튼 스타일
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 11;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.normal.textColor = Color.white;

            // 월드맵 셋업 버튼
            if (GUILayout.Button(TOOLBAR_BUTTON_TEXT, buttonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
            {
                WorldMapSetupTool.QuickSetupAll();
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
