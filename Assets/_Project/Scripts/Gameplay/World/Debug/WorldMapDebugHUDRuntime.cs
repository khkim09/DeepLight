using Project.Data.World;
using Project.Gameplay.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.World.Debug
{
    /// <summary>월드맵 디버그 HUD의 런타임 설치자 (Canvas 생성 + 키보드 토글)</summary>
    public class WorldMapDebugHUDRuntime : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private bool updateEveryFrame;
        [SerializeField] private string testZoneId = "I6";

        [Header("Optional References")]
        [SerializeField] private WorldMapRuntimeTest runtimeTestSource;

        // 동적 생성된 컴포넌트
        private WorldMapDebugHUDView _view;
        private WorldMapDebugHUDController _controller;
        private GameObject _canvasObject;
        private bool _isVisible = true;

        /// <summary>런타임 설치자 초기화 (WorldMapSetupTool에서 호출)</summary>
        public void Initialize(WorldMapRuntimeTest runtimeTest, float interval, bool everyFrame, string testZone)
        {
            runtimeTestSource = runtimeTest;
            updateInterval = interval;
            updateEveryFrame = everyFrame;
            testZoneId = testZone;
        }

        private void Awake()
        {
            // Canvas가 없으면 동적 생성
            if (_canvasObject == null)
            {
                CreateDebugCanvas();
            }
        }

        private void Start()
        {
            // Controller에 View와 RuntimeTest 연결
            if (_controller != null && _view != null)
            {
                _controller.Initialize(_view, runtimeTestSource, updateInterval, updateEveryFrame, testZoneId);
            }
        }

        private void Update()
        {
            // F9 키로 HUD 토글
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleHUD();
            }
        }

        /// <summary>디버그 Canvas 동적 생성</summary>
        private void CreateDebugCanvas()
        {
            // Canvas 생성
            _canvasObject = new GameObject("WorldMapDebugHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 배경 패널 생성 (좌측 상단 고정, 크기 증가)
            GameObject panel = new GameObject("BackgroundPanel", typeof(Image));
            panel.transform.SetParent(_canvasObject.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(20, -150);
            panelRect.sizeDelta = new Vector2(600, 700);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.75f);

            // TextMeshPro 텍스트 생성 (Text 대신 TMP 사용)
            GameObject textObj = new GameObject("HUDText", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(panel.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 12);
            textRect.offsetMax = new Vector2(-12, -12);

            TextMeshProUGUI tmpText = textObj.GetComponent<TextMeshProUGUI>();
            tmpText.fontSize = 18;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.TopLeft;
            tmpText.richText = true;
            tmpText.text = "<color=#88CCFF>=== WorldMap Debug HUD ===</color>\nInitializing...";

            // View 생성
            _view = textObj.AddComponent<WorldMapDebugHUDView>();
            _view.Initialize(tmpText, panel);

            // Controller 생성
            _controller = _canvasObject.AddComponent<WorldMapDebugHUDController>();
        }

        /// <summary>HUD 표시/숨김 토글</summary>
        public void ToggleHUD()
        {
            _isVisible = !_isVisible;
            if (_isVisible)
            {
                _view?.ShowUninitialized();
            }
            else
            {
                _view?.Hide();
            }
        }

        /// <summary>HUD 표시 여부 설정</summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_isVisible)
            {
                _view?.ShowUninitialized();
            }
            else
            {
                _view?.Hide();
            }
        }

        /// <summary>Controller 참조 (외부 연결용)</summary>
        public WorldMapDebugHUDController Controller => _controller;

        /// <summary>View 참조</summary>
        public WorldMapDebugHUDView View => _view;
    }
}
