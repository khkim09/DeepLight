#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.DataTools
{
    /// <summary>Key / English / Korean CSV를 편집하는 에디터 창이다.</summary>
    public class LocalizationCSVEditorWindow : EditorWindow
    {
        [SerializeField] private DefaultAsset csvFolder; // 저장 폴더
        [SerializeField] private string fileName = "LocalizationTable.csv"; // 파일명

        private readonly List<LocalizationRow> rows = new(); // 현재 편집 중인 row 목록
        private Vector2 scroll;

        /// <summary>Localization CSV 창을 연다.</summary>
        [MenuItem("Tools/DeepLight/Localization CSV Editor")]
        public static void Open()
        {
            GetWindow<LocalizationCSVEditorWindow>("Localization CSV");
        }

        /// <summary>창 GUI를 그린다.</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("DeepLight Localization CSV Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            csvFolder = (DefaultAsset)EditorGUILayout.ObjectField("CSV Folder", csvFolder, typeof(DefaultAsset), false);
            fileName = EditorGUILayout.TextField("File Name", fileName);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load CSV"))
                LoadCSV();

            if (GUILayout.Button("Save CSV"))
                SaveCSV();

            if (GUILayout.Button("Add Row"))
                rows.Add(new LocalizationRow());

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawTable();
        }

        /// <summary>행 편집 테이블을 그린다.</summary>
        private void DrawTable()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key", GUILayout.Width(220));
            EditorGUILayout.LabelField("English", GUILayout.Width(320));
            EditorGUILayout.LabelField("Korean", GUILayout.Width(320));
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < rows.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                rows[i].Key = EditorGUILayout.TextField(rows[i].Key, GUILayout.Width(220));
                rows[i].English = EditorGUILayout.TextField(rows[i].English, GUILayout.Width(320));
                rows[i].Korean = EditorGUILayout.TextField(rows[i].Korean, GUILayout.Width(320));

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    rows.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>현재 CSV를 불러온다.</summary>
        private void LoadCSV()
        {
            string fullPath = GetCSVFullPath();
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                Debug.LogWarning("Localization CSV 파일이 없다.");
                return;
            }

            rows.Clear();

            string[] lines = File.ReadAllLines(fullPath);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(',');
                LocalizationRow row = new LocalizationRow
                {
                    Key = parts.Length > 0 ? parts[0] : string.Empty,
                    English = parts.Length > 1 ? parts[1] : string.Empty,
                    Korean = parts.Length > 2 ? parts[2] : string.Empty
                };
                rows.Add(row);
            }
        }

        /// <summary>현재 row 목록을 CSV로 저장한다.</summary>
        private void SaveCSV()
        {
            string fullPath = GetCSVFullPath();
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                Debug.LogError("CSV Folder가 유효하지 않다.");
                return;
            }

            List<string> lines = new()
            {
                "Key,English,Korean"
            };

            for (int i = 0; i < rows.Count; i++)
            {
                LocalizationRow row = rows[i];
                lines.Add($"{Escape(row.Key)},{Escape(row.English)},{Escape(row.Korean)}");
            }

            File.WriteAllLines(fullPath, lines);
            AssetDatabase.Refresh();

            Debug.Log($"Localization CSV saved: {fullPath}");
        }

        /// <summary>현재 CSV 전체 경로를 만든다.</summary>
        private string GetCSVFullPath()
        {
            string folderPath = csvFolder != null ? AssetDatabase.GetAssetPath(csvFolder) : "Assets/_Project/Localization";
            if (!AssetDatabase.IsValidFolder(folderPath))
                CreateFoldersRecursively(folderPath);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, folderPath, fileName);
        }

        /// <summary>쉼표/따옴표를 escape 한다.</summary>
        private string Escape(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            if (!raw.Contains(",") && !raw.Contains("\"") && !raw.Contains("\n"))
                return raw;

            return $"\"{raw.Replace("\"", "\"\"")}\"";
        }

        /// <summary>폴더 경로를 재귀 생성한다.</summary>
        private void CreateFoldersRecursively(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        /// <summary>Localization CSV 한 줄 row 데이터이다.</summary>
        [Serializable]
        private class LocalizationRow
        {
            public string Key;
            public string English;
            public string Korean;
        }
    }
}
#endif
