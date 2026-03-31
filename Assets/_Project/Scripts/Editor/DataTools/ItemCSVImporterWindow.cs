#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Project.Data.Items;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.DataTools
{
    /// <summary>아이템 CSV를 ItemSO 자산으로 생성/갱신하는 에디터 창이다.</summary>
    public class ItemCSVImporterWindow : EditorWindow
    {
        [SerializeField] private TextAsset csvSource; // 원본 CSV
        [SerializeField] private string outputFolder = "Assets/_Project/ScriptableObjects/Items/Generated"; // 생성 경로
        [SerializeField] private ItemDatabaseSO targetDatabase; // 갱신할 DB

        private Vector2 scroll;

        /// <summary>CSV importer 창을 연다.</summary>
        [MenuItem("Tools/DeepLight/Items/CSV Importer")]
        public static void Open()
        {
            GetWindow<ItemCSVImporterWindow>("Item CSV Importer");
        }

        /// <summary>창 GUI를 그린다.</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("DeepLight Item CSV Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            csvSource = (TextAsset)EditorGUILayout.ObjectField("CSV Source", csvSource, typeof(TextAsset), false);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            targetDatabase = (ItemDatabaseSO)EditorGUILayout.ObjectField("Target Database", targetDatabase, typeof(ItemDatabaseSO), false);

            EditorGUILayout.HelpBox(
                "CSV Header Example:\n" +
                "itemId,displayName,description,category,rarity,sellPrice,weight,baseCatchDifficulty,shapeBoundsX,shapeBoundsY,occupiedCells,canRotate,iconPath,worldPrefabPath\n" +
                "occupiedCells format: 0:0|1:0|1:1",
                MessageType.Info);

            if (GUILayout.Button("Import CSV"))
                ImportCSV();

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Notes");
            EditorGUILayout.LabelField("- category / rarity 는 enum 이름과 정확히 일치해야 한다.");
            EditorGUILayout.LabelField("- iconPath / worldPrefabPath 는 AssetDatabase 경로를 쓴다.");
            EditorGUILayout.EndScrollView();
        }

        /// <summary>CSV를 읽어 ItemSO 자산을 생성/갱신한다.</summary>
        private void ImportCSV()
        {
            if (csvSource == null)
            {
                Debug.LogError("CSV Source가 비어 있다.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(outputFolder))
                CreateFoldersRecursively(outputFolder);

            string[] lines = csvSource.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1)
            {
                Debug.LogError("CSV에 데이터 행이 없다.");
                return;
            }

            List<ItemSO> importedItems = new();

            // 첫 줄은 헤더로 간주한다.
            for (int i = 1; i < lines.Length; i++)
            {
                ItemCSVRow row = ParseRow(lines[i]);
                if (string.IsNullOrWhiteSpace(row.ItemId))
                    continue;

                string assetPath = $"{outputFolder}/{SanitizeFileName(row.ItemId)}.asset";
                ItemSO itemAsset = AssetDatabase.LoadAssetAtPath<ItemSO>(assetPath);

                if (itemAsset == null)
                {
                    itemAsset = CreateInstance<ItemSO>();
                    AssetDatabase.CreateAsset(itemAsset, assetPath);
                }

                ApplyRowToItem(itemAsset, row);
                importedItems.Add(itemAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (targetDatabase != null)
                ApplyToDatabase(targetDatabase, importedItems);

            Debug.Log($"Item CSV import completed. Count = {importedItems.Count}");
        }

        /// <summary>한 줄 CSV를 row 데이터로 파싱한다.</summary>
        private ItemCSVRow ParseRow(string line)
        {
            string[] parts = line.Split(',');

            ItemCSVRow row = new ItemCSVRow
            {
                ItemId = Get(parts, 0),
                DisplayName = Get(parts, 1),
                Description = Get(parts, 2),
                Category = Get(parts, 3),
                Rarity = Get(parts, 4),
                SellPrice = ParseInt(Get(parts, 5)),
                Weight = ParseFloat(Get(parts, 6)),
                BaseCatchDifficulty = ParseFloat(Get(parts, 7)),
                ShapeBoundsX = ParseInt(Get(parts, 8)),
                ShapeBoundsY = ParseInt(Get(parts, 9)),
                OccupiedCells = Get(parts, 10),
                CanRotate = ParseBool(Get(parts, 11)),
                IconPath = Get(parts, 12),
                WorldPrefabPath = Get(parts, 13)
            };

            return row;
        }

        /// <summary>row 데이터를 SerializedObject로 ItemSO에 적용한다.</summary>
        private void ApplyRowToItem(ItemSO itemAsset, ItemCSVRow row)
        {
            SerializedObject so = new SerializedObject(itemAsset);

            so.FindProperty("itemId").stringValue = row.ItemId;
            so.FindProperty("displayName").stringValue = row.DisplayName;
            so.FindProperty("description").stringValue = row.Description;

            Sprite icon = !string.IsNullOrWhiteSpace(row.IconPath)
                ? AssetDatabase.LoadAssetAtPath<Sprite>(row.IconPath)
                : null;
            GameObject prefab = !string.IsNullOrWhiteSpace(row.WorldPrefabPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(row.WorldPrefabPath)
                : null;

            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;

            SetEnumByName(so.FindProperty("category"), row.Category);
            SetEnumByName(so.FindProperty("rarity"), row.Rarity);

            so.FindProperty("sellPrice").intValue = row.SellPrice;
            so.FindProperty("weight").floatValue = row.Weight;
            so.FindProperty("baseCatchDifficulty").floatValue = row.BaseCatchDifficulty;

            SerializedProperty bounds = so.FindProperty("shapeBounds");
            bounds.FindPropertyRelative("x").intValue = Mathf.Max(1, row.ShapeBoundsX);
            bounds.FindPropertyRelative("y").intValue = Mathf.Max(1, row.ShapeBoundsY);

            ApplyOccupiedCells(so.FindProperty("occupiedCells"), row.OccupiedCells);
            so.FindProperty("canRotate").boolValue = row.CanRotate;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemAsset);
        }

        /// <summary>occupiedCells 문자열을 ItemShapeCell 배열로 적용한다.</summary>
        private void ApplyOccupiedCells(SerializedProperty occupiedCellsProperty, string occupiedCellsRaw)
        {
            string[] cellTokens = string.IsNullOrWhiteSpace(occupiedCellsRaw)
                ? Array.Empty<string>()
                : occupiedCellsRaw.Split('|');

            occupiedCellsProperty.arraySize = cellTokens.Length;

            for (int i = 0; i < cellTokens.Length; i++)
            {
                string[] xy = cellTokens[i].Split(':');
                int x = xy.Length > 0 ? ParseInt(xy[0]) : 0;
                int y = xy.Length > 1 ? ParseInt(xy[1]) : 0;

                SerializedProperty cell = occupiedCellsProperty.GetArrayElementAtIndex(i);
                SerializedProperty position = cell.FindPropertyRelative("Position");
                position.FindPropertyRelative("x").intValue = x;
                position.FindPropertyRelative("y").intValue = y;
            }
        }

        /// <summary>가져온 아이템 목록으로 DB를 갱신한다.</summary>
        private void ApplyToDatabase(ItemDatabaseSO database, List<ItemSO> importedItems)
        {
            SerializedObject dbSo = new SerializedObject(database);
            SerializedProperty itemsProperty = dbSo.FindProperty("items");
            itemsProperty.arraySize = importedItems.Count;

            for (int i = 0; i < importedItems.Count; i++)
                itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue = importedItems[i];

            dbSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        /// <summary>enum 프로퍼티를 이름으로 세팅한다.</summary>
        private void SetEnumByName(SerializedProperty property, string enumName)
        {
            if (property == null || string.IsNullOrWhiteSpace(enumName))
                return;

            int index = Array.IndexOf(property.enumNames, enumName);
            if (index >= 0)
                property.enumValueIndex = index;
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

        /// <summary>배열에서 안전하게 문자열을 가져온다.</summary>
        private string Get(string[] parts, int index)
        {
            return index >= 0 && index < parts.Length ? parts[index].Trim() : string.Empty;
        }

        /// <summary>문자열을 int로 파싱한다.</summary>
        private int ParseInt(string raw)
        {
            int.TryParse(raw, out int value);
            return value;
        }

        /// <summary>문자열을 float로 파싱한다.</summary>
        private float ParseFloat(string raw)
        {
            float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
            return value;
        }

        /// <summary>문자열을 bool로 파싱한다.</summary>
        private bool ParseBool(string raw)
        {
            if (bool.TryParse(raw, out bool value))
                return value;

            return raw == "1";
        }

        /// <summary>파일명으로 안전한 문자열을 만든다.</summary>
        private string SanitizeFileName(string raw)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                raw = raw.Replace(invalid.ToString(), "_");

            return raw;
        }

        /// <summary>CSV 한 줄 아이템 row 데이터이다.</summary>
        private struct ItemCSVRow
        {
            public string ItemId;
            public string DisplayName;
            public string Description;
            public string Category;
            public string Rarity;
            public int SellPrice;
            public float Weight;
            public float BaseCatchDifficulty;
            public int ShapeBoundsX;
            public int ShapeBoundsY;
            public string OccupiedCells;
            public bool CanRotate;
            public string IconPath;
            public string WorldPrefabPath;
        }
    }
}
#endif
