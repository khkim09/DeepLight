using System.Collections.Generic;
using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Common
{
    /// <summary>AlwaysHUD 상단 중앙 바에서 날짜, 시간, 배그식 방향계를 표시하는 프리젠터이다.</summary>
    public class TopCenterHUDPresenter : MonoBehaviour
    {
        [Header("Time UI")]
        [SerializeField] private TextMeshProUGUI dayText; // 날짜 텍스트
        [SerializeField] private TextMeshProUGUI timeText; // 시간 텍스트

        [Header("Compass UI")]
        [SerializeField] private RectTransform compassViewPort; // 방향계 표시 창
        [SerializeField] private RectTransform compassContent; // 실제 눈금/문자 컨텐츠 루트

        [Header("Layout")]
        [SerializeField] private float pixelsPerDegree = 8f; // 1도당 가로 픽셀 수
        [SerializeField] private int repeatedCycles = 3; // 360도 반복 생성 횟수

        [Header("Tick Style")]
        [SerializeField] private float minorTickWidth = 2f;
        [SerializeField] private float minorTickHeight = 14f;
        [SerializeField] private float mediumTickWidth = 2.5f;
        [SerializeField] private float mediumTickHeight = 22f;
        [SerializeField] private float majorTickWidth = 4f;
        [SerializeField] private float majorTickHeight = 32f;

        [Header("Label Position")]
        [SerializeField] private float cardinalLabelOffsetFromTickBottom = 16f; // 방위 텍스트를 눈금 아래로 내릴 거리
        [SerializeField] private float degreeLabelOffsetFromTickBottom = 12f; // 숫자를 눈금 아래로 내릴 거리
        [SerializeField] private float tickBaseY = 2f; // 눈금 시작 y

        [Header("Colors")]
        [SerializeField] private Color minorTickColor = new(1f, 1f, 1f, 0.40f);
        [SerializeField] private Color mediumTickColor = new(1f, 1f, 1f, 0.70f);
        [SerializeField] private Color majorTickColor = Color.white;
        [SerializeField] private Color northHighlightColor = new(1f, 0.85f, 0.15f, 1f); // N 강조 색
        [SerializeField] private Color cardinalTextColor = Color.white;
        [SerializeField] private Color degreeTextColor = new(1f, 1f, 1f, 0.85f);

        private readonly List<GameObject> generatedObjects = new();
        private float latestHeadingDegrees;

        /// <summary>초기 방향계와 기본 텍스트를 생성한다.</summary>
        private void Awake()
        {
            RebuildCompass();
            ApplyTimeText(1, 0f, 12f);
            ApplyCompassPosition(0f);
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<GameTimeChangedEvent>(OnGameTimeChanged);
            EventBus.Subscribe<ExplorationHeadingChangedEvent>(OnExplorationHeadingChanged);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<GameTimeChangedEvent>(OnGameTimeChanged);
            EventBus.Unsubscribe<ExplorationHeadingChangedEvent>(OnExplorationHeadingChanged);
        }

        /// <summary>에디터 값 변경 시 방향계를 다시 생성한다.</summary>
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            RebuildCompass();
            ApplyCompassPosition(latestHeadingDegrees);
        }

        /// <summary>시간 이벤트를 받아 날짜/시간 표시를 갱신한다.</summary>
        private void OnGameTimeChanged(GameTimeChangedEvent publishedEvent)
        {
            ApplyTimeText(
                publishedEvent.Day,
                publishedEvent.HourOfDay,
                publishedEvent.DayLengthHours);
        }

        /// <summary>헤딩 이벤트를 받아 방향계 표시를 갱신한다.</summary>
        private void OnExplorationHeadingChanged(ExplorationHeadingChangedEvent publishedEvent)
        {
            latestHeadingDegrees = publishedEvent.HeadingDegrees;
            ApplyCompassPosition(latestHeadingDegrees);
        }

        /// <summary>날짜와 시간 텍스트를 반영한다.</summary>
        private void ApplyTimeText(int day, float hourOfDay, float dayLengthHours)
        {
            if (dayText != null)
                dayText.text = $"Day {day}";

            if (timeText != null)
                timeText.text = FormatHourText(hourOfDay, dayLengthHours);
        }

        /// <summary>현재 헤딩에 맞춰 방향계 컨텐츠를 좌우 이동시킨다.</summary>
        private void ApplyCompassPosition(float headingDegrees)
        {
            if (compassViewPort == null || compassContent == null)
                return;

            float cycleWidth = 360f * pixelsPerDegree;
            float viewportHalfWidth = compassViewPort.rect.width * 0.5f;

            // 가운데 사이클이 divide line 중앙 marker 아래 오도록 이동
            float targetX = -(headingDegrees * pixelsPerDegree) - cycleWidth + viewportHalfWidth;
            compassContent.anchoredPosition = new Vector2(targetX, compassContent.anchoredPosition.y);
        }

        /// <summary>배그식 방향계를 다시 생성한다.</summary>
        private void RebuildCompass()
        {
            ClearGeneratedObjects();

            if (compassContent == null)
                return;

            int safeCycles = Mathf.Max(3, repeatedCycles);
            float cycleWidth = 360f * pixelsPerDegree;
            float totalWidth = cycleWidth * safeCycles;

            // 실제로 좌우로 움직일 긴 방위계 띠 길이
            compassContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);

            int centerCycleIndex = safeCycles / 2;
            float startX = -cycleWidth * centerCycleIndex;

            for (int cycle = 0; cycle < safeCycles; cycle++)
            {
                float cycleOffsetX = startX + (cycle * cycleWidth);

                for (int degree = 0; degree < 360; degree += 15)
                {
                    bool isMajor = degree % 90 == 0;
                    bool isCardinal = degree % 45 == 0;
                    bool isNorth = degree == 0;

                    float x = cycleOffsetX + (degree * pixelsPerDegree);

                    float tickHeight = isMajor ? majorTickHeight : (isCardinal ? mediumTickHeight : minorTickHeight);
                    float tickWidth = isMajor ? majorTickWidth : (isCardinal ? mediumTickWidth : minorTickWidth);
                    Color tickColor = isNorth
                        ? northHighlightColor
                        : (isMajor ? majorTickColor : (isCardinal ? mediumTickColor : minorTickColor));

                    CreateTick(
                        x,
                        tickBaseY,
                        tickWidth,
                        tickHeight,
                        tickColor);

                    // 라벨은 눈금 아래로 내린다.
                    float tickBottomY = tickBaseY;
                    float cardinalLabelY = tickBottomY - cardinalLabelOffsetFromTickBottom;
                    float degreeLabelY = tickBottomY - degreeLabelOffsetFromTickBottom;

                    if (degree % 45 == 0)
                    {
                        Color labelColor = isNorth ? northHighlightColor : cardinalTextColor;

                        CreateLabel(
                            x,
                            cardinalLabelY,
                            DegreeToDirectionLabel(degree),
                            labelColor,
                            18f);
                    }
                    else
                    {
                        CreateLabel(
                            x,
                            degreeLabelY,
                            degree.ToString(),
                            degreeTextColor,
                            13f);
                    }
                }
            }
        }

        /// <summary>눈금 하나를 생성한다.</summary>
        private void CreateTick(float x, float y, float width, float height, Color color)
        {
            GameObject tickObject = new("Tick", typeof(RectTransform), typeof(Image));
            tickObject.transform.SetParent(compassContent, false);

            RectTransform rect = tickObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);

            Image image = tickObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            generatedObjects.Add(tickObject);
        }

        /// <summary>텍스트 라벨 하나를 생성한다.</summary>
        private void CreateLabel(float x, float y, string textValue, Color color, float fontSize)
        {
            GameObject textObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(compassContent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(52f, 22f);

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = textValue;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            generatedObjects.Add(textObject);
        }

        /// <summary>생성된 방향계 오브젝트를 모두 제거한다.</summary>
        private void ClearGeneratedObjects()
        {
            for (int i = 0; i < generatedObjects.Count; i++)
            {
                if (generatedObjects[i] != null)
                    DestroyImmediate(generatedObjects[i]);
            }

            generatedObjects.Clear();
        }

        /// <summary>현재 하루 길이에 맞는 시간 문자열을 반환한다.</summary>
        private string FormatHourText(float hourOfDay, float dayLengthHours)
        {
            int maxHours = Mathf.RoundToInt(dayLengthHours);
            int hour = Mathf.FloorToInt(hourOfDay);
            int minute = Mathf.FloorToInt((hourOfDay - hour) * 60f);

            if (minute >= 60)
            {
                minute = 0;
                hour += 1;
            }

            if (hour >= maxHours)
                hour = 0;

            return $"{hour:00}:{minute:00}";
        }

        /// <summary>각도를 방향 문자열로 변환한다.</summary>
        private string DegreeToDirectionLabel(int degree)
        {
            return degree switch
            {
                0 => "N",
                45 => "NE",
                90 => "E",
                135 => "SE",
                180 => "S",
                225 => "SW",
                270 => "W",
                315 => "NW",
                _ => string.Empty
            };
        }
    }
}
