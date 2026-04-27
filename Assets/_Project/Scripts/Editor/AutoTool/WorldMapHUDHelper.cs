using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// WorldMapSetupTool의 HUD/MiniGrid Canvas 관련 기능을 분리한 보조 유틸리티 (Phase 3).
    /// _WorldMap_Manual 하이라키 기준으로 독립 Canvas 기반 CurrentZoneHUD와 MiniGrid를 생성/보정한다.
    /// 기존 UIRoot / 기존 게임 HUD는 절대 건드리지 않는다.
    /// </summary>
    public static class WorldMapHUDHelper
    {
        private static readonly StringBuilder _log = new();

        // ======================================================================
        //  Public API
        // ======================================================================

        /// <summary>HUD Canvas + CurrentZoneHUD 생성/보정 (idempotent)</summary>
        public static void CreateUpdateHUDOnly()
        {
            _log.Clear();
            _log.AppendLine("===== HUD만 생성/갱신 (Phase 3) =====");

            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                _log.AppendLine("[경고] _WorldMap_Manual 루트가 없습니다. 먼저 씬 계층을 생성하세요.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            // 1. WorldMapHUDCanvas
            GameObject canvas = FindOrCreateCanvas(root, "WorldMapHUDCanvas", 500);
            SetCanvasScaler(canvas, 0.5f);

            // 2. WorldMapCurrentZoneHUD
            GameObject hud = FindOrCreateChild(canvas, "WorldMapCurrentZoneHUD", typeof(RectTransform));
            RectTransform hudRt = hud.GetComponent<RectTransform>();
            hudRt.anchorMin = new Vector2(0.5f, 1f);
            hudRt.anchorMax = new Vector2(0.5f, 1f);
            hudRt.pivot = new Vector2(0.5f, 1f);
            hudRt.anchoredPosition = new Vector2(0f, -20f);
            hudRt.sizeDelta = new Vector2(360f, 120f);

            // 3. Controller
            var controller = hud.GetComponent<WorldMapCurrentZoneHUDController>();
            if (controller == null)
            {
                controller = hud.AddComponent<WorldMapCurrentZoneHUDController>();
                _log.AppendLine("[생성] WorldMapCurrentZoneHUDController");
            }
            else
            {
                _log.AppendLine("[찾음] WorldMapCurrentZoneHUDController (이미 존재)");
            }

            // 4. View child
            GameObject view = FindOrCreateChild(hud, "View", typeof(RectTransform));
            RectTransform viewRt = view.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;

            // 5. View component
            var viewComp = view.GetComponent<WorldMapCurrentZoneHUDView>();
            if (viewComp == null)
            {
                viewComp = view.AddComponent<WorldMapCurrentZoneHUDView>();
                _log.AppendLine("[생성] WorldMapCurrentZoneHUDView");
            }
            else
            {
                _log.AppendLine("[찾음] WorldMapCurrentZoneHUDView (이미 존재)");
            }

            // 6. Create UI elements under View
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
            SetAnchoredRect(zoneName.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(340f, 28f));

            // RegionNameText
            GameObject regionName = FindOrCreateUIChild(view, "RegionNameText", typeof(TextMeshProUGUI));
            TextMeshProUGUI regionNameTmp = regionName.GetComponent<TextMeshProUGUI>();
            regionNameTmp.fontSize = 18;
            regionNameTmp.alignment = TextAlignmentOptions.Center;
            regionNameTmp.text = "Region";
            SetAnchoredRect(regionName.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(340f, 24f));

            // RiskStatusText
            GameObject riskStatus = FindOrCreateUIChild(view, "RiskStatusText", typeof(TextMeshProUGUI));
            TextMeshProUGUI riskStatusTmp = riskStatus.GetComponent<TextMeshProUGUI>();
            riskStatusTmp.fontSize = 18;
            riskStatusTmp.alignment = TextAlignmentOptions.Center;
            riskStatusTmp.text = "Risk: Safe";
            SetAnchoredRect(riskStatus.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(340f, 24f));

            // DiscoveryStatusText
            GameObject discoveryStatus = FindOrCreateUIChild(view, "DiscoveryStatusText", typeof(TextMeshProUGUI));
            TextMeshProUGUI discoveryStatusTmp = discoveryStatus.GetComponent<TextMeshProUGUI>();
            discoveryStatusTmp.fontSize = 16;
            discoveryStatusTmp.alignment = TextAlignmentOptions.Center;
            discoveryStatusTmp.text = "Status: Undiscovered";
            SetAnchoredRect(discoveryStatus.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(340f, 22f));

            // 7. Wire View references (최신 시그니처 기준)
            SerializedObject viewSo = new SerializedObject(viewComp);
            viewSo.FindProperty("riskTintImage").objectReferenceValue = riskTintImg;
            viewSo.FindProperty("zoneNameText").objectReferenceValue = zoneNameTmp;
            viewSo.FindProperty("regionNameText").objectReferenceValue = regionNameTmp;
            viewSo.FindProperty("riskStatusText").objectReferenceValue = riskStatusTmp;
            viewSo.FindProperty("discoveryStatusText").objectReferenceValue = discoveryStatusTmp;
            viewSo.ApplyModifiedProperties();
            _log.AppendLine("[보정] View 참조 5개 연결 완료");

            // 8. Wire View into Controller
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("hudView").objectReferenceValue = viewComp;
            controllerSo.ApplyModifiedProperties();
            _log.AppendLine("[보정] Controller.hudView 연결 완료");

            _log.AppendLine("===== HUD 생성/갱신 완료 =====");
            UnityEngine.Debug.Log(_log.ToString());
        }

        // ======================================================================
        //  HUD UI Elements Only (View 아래 5개 UI 요소만 생성/보정)
        // ======================================================================

        /// <summary>
        /// WorldMapCurrentZoneHUD/View 아래 5개 UI 요소(RiskTintImage, ZoneNameText, RegionNameText, RiskStatusText, DiscoveryStatusText)만 생성/보정한다.
        /// Canvas/Controller/View 구조는 이미 존재한다고 가정한다.
        /// </summary>
        public static void CreateHUDUIElementsOnly()
        {
            _log.Clear();
            _log.AppendLine("===== HUD UI 요소만 생성/보정 =====");

            // 1. View 찾기
            GameObject view = GameObject.Find("_WorldMap_Manual/WorldMapHUDCanvas/WorldMapCurrentZoneHUD/View");
            if (view == null)
            {
                _log.AppendLine("[경고] View를 찾을 수 없습니다. 먼저 HUD 구조를 생성하세요.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            var viewComp = view.GetComponent<WorldMapCurrentZoneHUDView>();
            if (viewComp == null)
            {
                _log.AppendLine("[경고] WorldMapCurrentZoneHUDView 컴포넌트가 없습니다.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            // 2. RiskTintImage
            GameObject riskTint = FindOrCreateUIChild(view, "RiskTintImage", typeof(Image));
            Image riskTintImg = riskTint.GetComponent<Image>();
            riskTintImg.color = new Color(0f, 0f, 0f, 0.35f);
            StretchRect(riskTint.GetComponent<RectTransform>());
            _log.AppendLine("[생성/확인] RiskTintImage");

            // 3. ZoneNameText
            GameObject zoneName = FindOrCreateUIChild(view, "ZoneNameText", typeof(TextMeshProUGUI));
            TextMeshProUGUI zoneNameTmp = zoneName.GetComponent<TextMeshProUGUI>();
            zoneNameTmp.fontSize = 24;
            zoneNameTmp.alignment = TextAlignmentOptions.Center;
            zoneNameTmp.text = "Zone Name";
            SetAnchoredRect(zoneName.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(340f, 28f));
            _log.AppendLine("[생성/확인] ZoneNameText");

            // 4. RegionNameText
            GameObject regionName = FindOrCreateUIChild(view, "RegionNameText", typeof(TextMeshProUGUI));
            TextMeshProUGUI regionNameTmp = regionName.GetComponent<TextMeshProUGUI>();
            regionNameTmp.fontSize = 18;
            regionNameTmp.alignment = TextAlignmentOptions.Center;
            regionNameTmp.text = "Region";
            SetAnchoredRect(regionName.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(340f, 24f));
            _log.AppendLine("[생성/확인] RegionNameText");

            // 5. RiskStatusText
            GameObject riskStatus = FindOrCreateUIChild(view, "RiskStatusText", typeof(TextMeshProUGUI));
            TextMeshProUGUI riskStatusTmp = riskStatus.GetComponent<TextMeshProUGUI>();
            riskStatusTmp.fontSize = 18;
            riskStatusTmp.alignment = TextAlignmentOptions.Center;
            riskStatusTmp.text = "Risk: Safe";
            SetAnchoredRect(riskStatus.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(340f, 24f));
            _log.AppendLine("[생성/확인] RiskStatusText");

            // 6. DiscoveryStatusText
            GameObject discoveryStatus = FindOrCreateUIChild(view, "DiscoveryStatusText", typeof(TextMeshProUGUI));
            TextMeshProUGUI discoveryStatusTmp = discoveryStatus.GetComponent<TextMeshProUGUI>();
            discoveryStatusTmp.fontSize = 16;
            discoveryStatusTmp.alignment = TextAlignmentOptions.Center;
            discoveryStatusTmp.text = "Status: Undiscovered";
            SetAnchoredRect(discoveryStatus.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(340f, 22f));
            _log.AppendLine("[생성/확인] DiscoveryStatusText");

            // 7. Wire View references
            SerializedObject viewSo = new SerializedObject(viewComp);
            viewSo.FindProperty("riskTintImage").objectReferenceValue = riskTintImg;
            viewSo.FindProperty("zoneNameText").objectReferenceValue = zoneNameTmp;
            viewSo.FindProperty("regionNameText").objectReferenceValue = regionNameTmp;
            viewSo.FindProperty("riskStatusText").objectReferenceValue = riskStatusTmp;
            viewSo.FindProperty("discoveryStatusText").objectReferenceValue = discoveryStatusTmp;
            viewSo.ApplyModifiedProperties();
            _log.AppendLine("[보정] View 참조 5개 연결 완료");

            _log.AppendLine("===== HUD UI 요소 생성/보정 완료 =====");
            UnityEngine.Debug.Log(_log.ToString());
        }

        // ======================================================================
        //  MiniGrid
        // ======================================================================

        /// <summary>MiniGrid Canvas + MiniGrid 생성/보정 (idempotent, cellImages/cellLabels 자동 할당)</summary>
        public static void CreateUpdateMiniGridOnly()
        {
            _log.Clear();
            _log.AppendLine("===== MiniGrid만 생성/갱신 (Phase 3) =====");

            GameObject root = GameObject.Find("_WorldMap_Manual");
            if (root == null)
            {
                _log.AppendLine("[경고] _WorldMap_Manual 루트가 없습니다. 먼저 씬 계층을 생성하세요.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            // 1. WorldMapMiniGridCanvas
            GameObject canvas = FindOrCreateCanvas(root, "WorldMapMiniGridCanvas", 450);
            SetCanvasScaler(canvas, 0.5f);

            // 2. WorldMapMiniGrid
            GameObject grid = FindOrCreateChild(canvas, "WorldMapMiniGrid", typeof(RectTransform));
            RectTransform gridRt = grid.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0f, 1f);
            gridRt.anchorMax = new Vector2(0f, 1f);
            gridRt.pivot = new Vector2(0f, 1f);
            gridRt.anchoredPosition = new Vector2(20f, -20f);
            gridRt.sizeDelta = new Vector2(190f, 110f);

            // 3. Controller
            var controller = grid.GetComponent<WorldMapMiniGridController>();
            if (controller == null)
            {
                controller = grid.AddComponent<WorldMapMiniGridController>();
                _log.AppendLine("[생성] WorldMapMiniGridController");
            }
            else
            {
                _log.AppendLine("[찾음] WorldMapMiniGridController (이미 존재)");
            }

            // 4. View child
            GameObject view = FindOrCreateChild(grid, "View", typeof(RectTransform));
            RectTransform viewRt = view.GetComponent<RectTransform>();
            StretchRect(viewRt);

            // 5. View component
            var viewComp = view.GetComponent<WorldMapMiniGridView>();
            if (viewComp == null)
            {
                viewComp = view.AddComponent<WorldMapMiniGridView>();
                _log.AppendLine("[생성] WorldMapMiniGridView");
            }
            else
            {
                _log.AppendLine("[찾음] WorldMapMiniGridView (이미 존재)");
            }

            // 6. Create 9 cells with auto-assignment
            Vector2[] cellPositions = new Vector2[]
            {
                new Vector2(-60f, 30f), new Vector2(0f, 30f), new Vector2(60f, 30f),
                new Vector2(-60f, 0f),  new Vector2(0f, 0f),  new Vector2(60f, 0f),
                new Vector2(-60f, -30f), new Vector2(0f, -30f), new Vector2(60f, -30f)
            };

            Image[] cellImages = new Image[9];
            TextMeshProUGUI[] cellLabels = new TextMeshProUGUI[9];

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
                    _log.AppendLine($"[생성] Cell: {cellName}");
                }
                else
                {
                    cell = cellTransform.gameObject;
                }

                RectTransform cellRt = cell.GetComponent<RectTransform>();
                cellRt.anchorMin = new Vector2(0.5f, 0.5f);
                cellRt.anchorMax = new Vector2(0.5f, 0.5f);
                cellRt.pivot = new Vector2(0.5f, 0.5f);
                cellRt.anchoredPosition = cellPositions[i];
                cellRt.sizeDelta = new Vector2(55f, 25f);

                // Cell Image
                Image cellImg = cell.GetComponent<Image>();
                cellImg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
                cellImages[i] = cellImg;

                // Cell Label
                Transform labelTransform = cell.transform.Find("Label");
                GameObject label;
                if (labelTransform == null)
                {
                    label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    label.transform.SetParent(cell.transform, false);
                }
                else
                {
                    label = labelTransform.gameObject;
                }

                RectTransform labelRt = label.GetComponent<RectTransform>();
                StretchRect(labelRt);
                TextMeshProUGUI labelTmp = label.GetComponent<TextMeshProUGUI>();
                labelTmp.fontSize = 14;
                labelTmp.alignment = TextAlignmentOptions.Center;
                labelTmp.text = "?";
                cellLabels[i] = labelTmp;
            }

            // 7. Auto-assign cellImages[9] and cellLabels[9] arrays (최신 시그니처 기준)
            SerializedObject viewSo = new SerializedObject(viewComp);

            SerializedProperty cellImagesProp = viewSo.FindProperty("cellImages");
            if (cellImagesProp != null && cellImagesProp.isArray)
            {
                cellImagesProp.ClearArray();
                cellImagesProp.arraySize = 9;
                for (int i = 0; i < 9; i++)
                {
                    cellImagesProp.GetArrayElementAtIndex(i).objectReferenceValue = cellImages[i];
                }
                _log.AppendLine("[보정] cellImages[9] 배열 자동 할당 완료");
            }
            else
            {
                _log.AppendLine("[경고] cellImages 필드를 찾을 수 없습니다. 최신 WorldMapMiniGridView 시그니처를 확인하세요.");
            }

            SerializedProperty cellLabelsProp = viewSo.FindProperty("cellLabels");
            if (cellLabelsProp != null && cellLabelsProp.isArray)
            {
                cellLabelsProp.ClearArray();
                cellLabelsProp.arraySize = 9;
                for (int i = 0; i < 9; i++)
                {
                    cellLabelsProp.GetArrayElementAtIndex(i).objectReferenceValue = cellLabels[i];
                }
                _log.AppendLine("[보정] cellLabels[9] 배열 자동 할당 완료");
            }
            else
            {
                _log.AppendLine("[경고] cellLabels 필드를 찾을 수 없습니다. 최신 WorldMapMiniGridView 시그니처를 확인하세요.");
            }

            viewSo.ApplyModifiedProperties();

            // 8. Wire View into Controller
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("gridView").objectReferenceValue = viewComp;
            controllerSo.ApplyModifiedProperties();
            _log.AppendLine("[보정] Controller.gridView 연결 완료");

            _log.AppendLine("===== MiniGrid 생성/갱신 완료 =====");
            UnityEngine.Debug.Log(_log.ToString());
        }

        // ======================================================================
        //  MiniGrid Submarine-Relative Setup
        // ======================================================================

        /// <summary>
        /// MiniGrid를 submarine-relative 모드로 설정한다.
        /// trackedTransform을 WorldMapTrackedTransform으로 연결하고,
        /// useSubmarineRelative를 true로 설정한다.
        /// </summary>
        public static void SetupMiniGridSubmarineRelative()
        {
            _log.Clear();
            _log.AppendLine("===== MiniGrid Submarine-Relative 설정 =====");

            // 1. MiniGrid Controller 찾기
            GameObject grid = GameObject.Find("_WorldMap_Manual/WorldMapMiniGridCanvas/WorldMapMiniGrid");
            if (grid == null)
            {
                _log.AppendLine("[경고] WorldMapMiniGrid를 찾을 수 없습니다. 먼저 MiniGrid를 생성하세요.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            var controller = grid.GetComponent<WorldMapMiniGridController>();
            if (controller == null)
            {
                _log.AppendLine("[경고] WorldMapMiniGridController가 없습니다.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            // 2. TrackedTransform 찾기
            Transform trackedTransform = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapTrackedTransform")?.transform;
            if (trackedTransform == null)
            {
                _log.AppendLine("[경고] WorldMapTrackedTransform을 찾을 수 없습니다.");
                UnityEngine.Debug.LogWarning(_log.ToString());
                return;
            }

            // 3. SerializedObject로 설정
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("trackedTransform").objectReferenceValue = trackedTransform;
            so.FindProperty("useSubmarineRelative").boolValue = true;
            so.FindProperty("updateEveryFrame").boolValue = true;
            so.ApplyModifiedProperties();

            _log.AppendLine("[설정] trackedTransform = WorldMapTrackedTransform");
            _log.AppendLine("[설정] useSubmarineRelative = true");
            _log.AppendLine("[설정] updateEveryFrame = true (실시간 갱신)");
            _log.AppendLine("===== MiniGrid Submarine-Relative 설정 완료 =====");
            UnityEngine.Debug.Log(_log.ToString());
        }

        // ======================================================================
        //  Validation (Phase 3)
        // ======================================================================

        /// <summary>HUD/MiniGrid 검증 (Phase 3 전용)</summary>
        public static void ValidateHUDAndMiniGrid()
        {
            _log.Clear();
            _log.AppendLine("===== HUD/MiniGrid 검증 (Phase 3) =====");

            // 1. HUD Canvas
            GameObject hudCanvas = GameObject.Find("_WorldMap_Manual/WorldMapHUDCanvas");
            if (hudCanvas != null)
            {
                _log.AppendLine("[확인] WorldMapHUDCanvas 존재");
                Canvas c = hudCanvas.GetComponent<Canvas>();
                if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder == 500)
                    _log.AppendLine("[확인] HUD Canvas 설정 올바름");
                else
                    _log.AppendLine("[경고] HUD Canvas 설정 불일치");
            }
            else
            {
                _log.AppendLine("[경고] WorldMapHUDCanvas 없음");
            }

            // 2. CurrentZoneHUD
            GameObject hud = GameObject.Find("_WorldMap_Manual/WorldMapHUDCanvas/WorldMapCurrentZoneHUD");
            if (hud != null)
            {
                _log.AppendLine("[확인] WorldMapCurrentZoneHUD 존재");
                var controller = hud.GetComponent<WorldMapCurrentZoneHUDController>();
                if (controller != null)
                    _log.AppendLine("[확인] WorldMapCurrentZoneHUDController 존재");
                else
                    _log.AppendLine("[경고] WorldMapCurrentZoneHUDController 없음");

                Transform viewTransform = hud.transform.Find("View");
                if (viewTransform != null)
                {
                    _log.AppendLine("[확인] View 존재");
                    var viewComp = viewTransform.GetComponent<WorldMapCurrentZoneHUDView>();
                    if (viewComp != null)
                    {
                        _log.AppendLine("[확인] WorldMapCurrentZoneHUDView 존재");
                        // Check 5 references
                        SerializedObject so = new SerializedObject(viewComp);
                        CheckRef(so, "zoneNameText", "zoneNameText");
                        CheckRef(so, "regionNameText", "regionNameText");
                        CheckRef(so, "riskStatusText", "riskStatusText");
                        CheckRef(so, "discoveryStatusText", "discoveryStatusText");
                        CheckRef(so, "riskTintImage", "riskTintImage");
                    }
                    else
                    {
                        _log.AppendLine("[경고] WorldMapCurrentZoneHUDView 없음");
                    }
                }
                else
                {
                    _log.AppendLine("[경고] View 없음");
                }
            }
            else
            {
                _log.AppendLine("[경고] WorldMapCurrentZoneHUD 없음");
            }

            // 3. MiniGrid Canvas
            GameObject gridCanvas = GameObject.Find("_WorldMap_Manual/WorldMapMiniGridCanvas");
            if (gridCanvas != null)
            {
                _log.AppendLine("[확인] WorldMapMiniGridCanvas 존재");
            }
            else
            {
                _log.AppendLine("[경고] WorldMapMiniGridCanvas 없음");
            }

            // 4. MiniGrid
            GameObject grid = GameObject.Find("_WorldMap_Manual/WorldMapMiniGridCanvas/WorldMapMiniGrid");
            if (grid != null)
            {
                _log.AppendLine("[확인] WorldMapMiniGrid 존재");
                var controller = grid.GetComponent<WorldMapMiniGridController>();
                if (controller != null)
                    _log.AppendLine("[확인] WorldMapMiniGridController 존재");
                else
                    _log.AppendLine("[경고] WorldMapMiniGridController 없음");

                Transform viewTransform = grid.transform.Find("View");
                if (viewTransform != null)
                {
                    _log.AppendLine("[확인] MiniGrid View 존재");
                    var viewComp = viewTransform.GetComponent<WorldMapMiniGridView>();
                    if (viewComp != null)
                    {
                        _log.AppendLine("[확인] WorldMapMiniGridView 존재");
                        SerializedObject so = new SerializedObject(viewComp);
                        SerializedProperty cellImagesProp = so.FindProperty("cellImages");
                        if (cellImagesProp != null && cellImagesProp.isArray && cellImagesProp.arraySize == 9)
                        {
                            int assignedCount = 0;
                            for (int i = 0; i < 9; i++)
                            {
                                if (cellImagesProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                                    assignedCount++;
                            }
                            _log.AppendLine($"[확인] cellImages: {assignedCount}/9 할당됨");
                        }
                        else
                        {
                            _log.AppendLine("[경고] cellImages 배열 미할당 또는 크기 불일치");
                        }

                        SerializedProperty cellLabelsProp = so.FindProperty("cellLabels");
                        if (cellLabelsProp != null && cellLabelsProp.isArray && cellLabelsProp.arraySize == 9)
                        {
                            int assignedCount = 0;
                            for (int i = 0; i < 9; i++)
                            {
                                if (cellLabelsProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                                    assignedCount++;
                            }
                            _log.AppendLine($"[확인] cellLabels: {assignedCount}/9 할당됨");
                        }
                        else
                        {
                            _log.AppendLine("[경고] cellLabels 배열 미할당 또는 크기 불일치");
                        }
                    }
                    else
                    {
                        _log.AppendLine("[경고] WorldMapMiniGridView 없음");
                    }
                }
                else
                {
                    _log.AppendLine("[경고] MiniGrid View 없음");
                }
            }
            else
            {
                _log.AppendLine("[경고] WorldMapMiniGrid 없음");
            }

            // 5. Installer references
            GameObject installer = GameObject.Find("_WorldMap_Manual/WorldMap_RuntimeRoot/WorldMapRuntimeInstaller");
            if (installer != null)
            {
                var installerComp = installer.GetComponent<WorldMapRuntimeInstaller>();
                if (installerComp != null)
                {
                    SerializedObject so = new SerializedObject(installerComp);
                    CheckRef(so, "currentZoneHUD", "Installer.currentZoneHUD");
                    CheckRef(so, "miniGridController", "Installer.miniGridController");
                    CheckRef(so, "zoneContentController", "Installer.zoneContentController");
                }
            }

            _log.AppendLine("===== HUD/MiniGrid 검증 완료 =====");
            UnityEngine.Debug.Log(_log.ToString());
        }

        // ======================================================================
        //  Helpers
        // ======================================================================

        private static GameObject FindOrCreateCanvas(GameObject parent, string name, int sortingOrder)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                _log.AppendLine($"[찾음] {name} (이미 존재)");
                return existing.gameObject;
            }

            GameObject canvas = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(parent.transform, false);
            Canvas c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            _log.AppendLine($"[생성] {name}");
            return canvas;
        }

        private static void SetCanvasScaler(GameObject canvas, float match)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = match;
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name, System.Type componentType)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(name, componentType);
            child.transform.SetParent(parent.transform, false);
            _log.AppendLine($"[생성] {parent.name}/{name}");
            return child;
        }

        private static GameObject FindOrCreateUIChild(GameObject parent, string name, System.Type componentType)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            child.AddComponent(componentType);
            _log.AppendLine($"[생성] UI 요소: {parent.name}/{name}");
            return child;
        }

        private static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchoredRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void CheckRef(SerializedObject so, string propertyName, string label)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                if (prop.objectReferenceValue != null)
                    _log.AppendLine($"[확인] {label} 연결됨: {prop.objectReferenceValue.name}");
                else
                    _log.AppendLine($"[경고] {label} 미연결 (null)");
            }
            else
            {
                _log.AppendLine($"[경고] {label} 필드 없음 (시그니처 불일치)");
            }
        }
    }
}
