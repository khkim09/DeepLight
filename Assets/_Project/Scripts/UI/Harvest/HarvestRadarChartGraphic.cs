using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>Harvest 포인트 부채꼴 레이더 차트를 그린다.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class HarvestRadarChartGraphic : MaskableGraphic
    {
        /// <summary>차트 축 방향 열거형이다.</summary>
        public enum RadarAxis
        {
            Top = 0,
            Bottom = 1,
            Left = 2,
            Right = 3
        }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = false; // 메시 생성 로그 출력 여부

        [Header("Background")]
        [SerializeField] private Color backgroundDiscColor = new(0.22f, 0.22f, 0.22f, 1f); // 배경 원판 색
        [SerializeField] private Color outerRingColor = new(0.05f, 0.05f, 0.08f, 1f); // 바깥 원형 라인 색
        [SerializeField] private float outerRingThickness = 3f; // 바깥 원형 라인 두께

        [Header("Dividers")]
        [SerializeField] private Color dividerColor = new(0.2f, 0.9f, 0.2f, 0.95f); // 축 분리선 색
        [SerializeField] private float dividerThickness = 2f; // 축 분리선 두께

        [Header("Sector Visual")]
        [SerializeField] private bool fillSectorInterior = true; // 부채꼴 내부 채우기 여부
        [SerializeField] private bool drawSectorOutline = true; // 부채꼴 외곽선 표시 여부
        [SerializeField] private Color sectorFillColor = new(0.55f, 0.82f, 0.45f, 1f); // 부채꼴 채움 색
        [SerializeField] private Color sectorOutlineColor = new(0.04f, 0.08f, 0.2f, 1f); // 부채꼴 외곽선 색
        [SerializeField] private float sectorOutlineThickness = 3f; // 부채꼴 외곽선 두께

        [Header("Sizing")]
        [SerializeField][Range(0.5f, 1f)] private float chartRadiusRatio = 0.84f; // 차트 반지름 비율
        [SerializeField] private int sectorSegments = 20; // 부채꼴 분할 수
        [SerializeField] private bool invertRiskValueForDisplay = false; // 위험도 반전 표시 여부

        private float topValue; // 안정성
        private float bottomValue; // 위험도
        private float leftValue; // 첫 앵커 적합도
        private float rightValue; // 후속 순서 적합도

        /// <summary>기본 렌더 텍스처를 반환한다.</summary>
        public override Texture mainTexture => s_WhiteTexture;

        /// <summary>초기화 시 기본 머티리얼과 다시 그리기를 적용한다.</summary>
        protected override void Awake()
        {
            base.Awake();

            if (canvasRenderer != null)
                canvasRenderer.SetMaterial(defaultGraphicMaterial, null);

            ForceRebuild();
        }

        /// <summary>시작 시 다시 그리기를 요청한다.</summary>
        protected override void Start()
        {
            base.Start();
            ForceRebuild();
        }

        /// <summary>활성화 시 다시 그리기를 요청한다.</summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            if (canvasRenderer != null)
                canvasRenderer.SetMaterial(defaultGraphicMaterial, null);

            ForceRebuild();
        }

        /// <summary>인스펙터 값 변경 시 다시 그리기를 요청한다.</summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            outerRingThickness = Mathf.Max(0f, outerRingThickness);
            dividerThickness = Mathf.Max(0f, dividerThickness);
            sectorOutlineThickness = Mathf.Max(0f, sectorOutlineThickness);
            sectorSegments = Mathf.Max(2, sectorSegments);

            ForceRebuild();
        }

        /// <summary>RectTransform 크기 변경 시 다시 그리기를 요청한다.</summary>
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            ForceRebuild();
        }

        /// <summary>차트 최대 반지름을 반환한다.</summary>
        public float GetChartRadius()
        {
            Rect rect = rectTransform.rect;
            return Mathf.Min(rect.width, rect.height) * 0.5f * chartRadiusRatio;
        }

        /// <summary>축 이름용 로컬 기준 위치를 반환한다.</summary>
        public Vector2 GetAxisLabelLocalPosition(RadarAxis axis, float margin)
        {
            float radius = GetChartRadius();
            return axis switch
            {
                RadarAxis.Top => Vector2.up * (radius + margin),
                RadarAxis.Bottom => Vector2.down * (radius + margin),
                RadarAxis.Left => Vector2.left * (radius + margin),
                RadarAxis.Right => Vector2.right * (radius + margin),
                _ => Vector2.zero
            };
        }

        /// <summary>값 텍스트용 로컬 기준 위치를 반환한다.</summary>
        public Vector2 GetValueLocalPosition(RadarAxis axis, float value01, float threshold01, float insidePadding, float outsidePadding)
        {
            float radius = GetChartRadius();
            float clampedValue = Mathf.Clamp01(value01);
            float valueRadius = radius * clampedValue;

            bool isInside = clampedValue >= threshold01;

            float targetRadius = isInside
                ? Mathf.Max(0f, valueRadius - insidePadding)
                : valueRadius + outsidePadding;

            return axis switch
            {
                RadarAxis.Top => Vector2.up * targetRadius,
                RadarAxis.Bottom => Vector2.down * targetRadius,
                RadarAxis.Left => Vector2.left * targetRadius,
                RadarAxis.Right => Vector2.right * targetRadius,
                _ => Vector2.zero
            };
        }

        /// <summary>차트 표시 수치를 갱신한다.</summary>
        public void SetValues(float top, float bottom, float left, float right)
        {
            topValue = Mathf.Clamp01(top);
            bottomValue = Mathf.Clamp01(invertRiskValueForDisplay ? 1f - bottom : bottom);
            leftValue = Mathf.Clamp01(left);
            rightValue = Mathf.Clamp01(right);

            ForceRebuild();
        }

        /// <summary>차트를 0 상태로 초기화한다.</summary>
        public void ClearValues()
        {
            topValue = 0f;
            bottomValue = 0f;
            leftValue = 0f;
            rightValue = 0f;

            ForceRebuild();
        }

        /// <summary>메시/머티리얼 전체를 다시 갱신한다.</summary>
        private void ForceRebuild()
        {
            SetVerticesDirty();
            SetMaterialDirty();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneView.RepaintAll();
#endif
        }

        /// <summary>UI 메시를 다시 생성한다.</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            Vector2 center = Vector2.zero; // UI Graphic은 로컬 중심 기준으로 그리는 편이 가장 안전함
            float radius = GetChartRadius();

            if (enableDebugLog)
                Debug.Log($"[HarvestRadarChartGraphic] Rebuild - rect({rect.width:0.0}, {rect.height:0.0}), radius={radius:0.0}, values=({topValue:0.00},{bottomValue:0.00},{leftValue:0.00},{rightValue:0.00})", this);

            if (rect.width <= 1f || rect.height <= 1f || radius <= 1f)
                return;

            // 배경 원판
            AddDisc(vh, center, radius, backgroundDiscColor, 72);

            // 바깥 원형 라인
            AddCircleLine(vh, center, radius, outerRingThickness, outerRingColor, 72);

            // 4개 분리선
            AddDividerLine(vh, center, 45f, radius);
            AddDividerLine(vh, center, 135f, radius);
            AddDividerLine(vh, center, 225f, radius);
            AddDividerLine(vh, center, 315f, radius);

            // 값이 없어도 중심 점은 보여서 "안 그려짐"과 구분되게 처리
            AddDisc(vh, center, 4f, dividerColor, 12);

            // 각 축 부채꼴
            DrawSector(vh, center, 45f, 135f, topValue);     // Top
            DrawSector(vh, center, 225f, 315f, bottomValue); // Bottom
            DrawSector(vh, center, 135f, 225f, leftValue);   // Left
            DrawSector(vh, center, -45f, 45f, rightValue);   // Right
        }

        /// <summary>지정 각도의 분리선을 그린다.</summary>
        private void AddDividerLine(VertexHelper vh, Vector2 center, float angleDeg, float radius)
        {
            Vector2 direction = DegreeToDirection(angleDeg);
            AddLine(vh, center, center + direction * radius, dividerThickness, dividerColor);
        }

        /// <summary>하나의 부채꼴을 그린다.</summary>
        private void DrawSector(VertexHelper vh, Vector2 center, float startAngleDeg, float endAngleDeg, float value01)
        {
            float radius = GetChartRadius() * Mathf.Clamp01(value01);
            if (radius <= 0.5f)
                return;

            if (fillSectorInterior)
                AddSectorFill(vh, center, radius, startAngleDeg, endAngleDeg, sectorFillColor, sectorSegments);

            if (!drawSectorOutline)
                return;

            Vector2 startDirection = DegreeToDirection(startAngleDeg);
            Vector2 endDirection = DegreeToDirection(endAngleDeg);

            Vector2 arcStart = center + startDirection * radius;
            Vector2 arcEnd = center + endDirection * radius;

            AddLine(vh, center, arcStart, sectorOutlineThickness, sectorOutlineColor);
            AddLine(vh, center, arcEnd, sectorOutlineThickness, sectorOutlineColor);
            AddArcLine(vh, center, radius, startAngleDeg, endAngleDeg, sectorOutlineThickness, sectorOutlineColor, sectorSegments);
        }

        /// <summary>각도에서 방향 벡터를 계산한다.</summary>
        private Vector2 DegreeToDirection(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>부채꼴 면을 채운다.</summary>
        private void AddSectorFill(VertexHelper vh, Vector2 center, float radius, float startAngleDeg, float endAngleDeg, Color fillColor, int segments)
        {
            int segmentCount = Mathf.Max(2, segments);

            int centerIndex = vh.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = fillColor;
            vertex.position = center;
            vh.AddVert(vertex);

            for (int i = 0; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float angle = Mathf.Lerp(startAngleDeg, endAngleDeg, t) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                vertex.position = point;
                vh.AddVert(vertex);
            }

            for (int i = 1; i <= segmentCount; i++)
                vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
        }

        /// <summary>원호 외곽선을 그린다.</summary>
        private void AddArcLine(VertexHelper vh, Vector2 center, float radius, float startAngleDeg, float endAngleDeg, float thickness, Color lineColor, int segments)
        {
            int segmentCount = Mathf.Max(2, segments);

            Vector2 previousOuter = Vector2.zero;
            Vector2 previousInner = Vector2.zero;
            bool hasPrevious = false;

            float outerRadius = radius + thickness * 0.5f;
            float innerRadius = Mathf.Max(0f, radius - thickness * 0.5f);

            for (int i = 0; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float angle = Mathf.Lerp(startAngleDeg, endAngleDeg, t) * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));

                Vector2 currentOuter = center + dir * outerRadius;
                Vector2 currentInner = center + dir * innerRadius;

                if (hasPrevious)
                {
                    int index = vh.currentVertCount;

                    UIVertex vertex = UIVertex.simpleVert;
                    vertex.color = lineColor;

                    vertex.position = previousOuter;
                    vh.AddVert(vertex);

                    vertex.position = currentOuter;
                    vh.AddVert(vertex);

                    vertex.position = currentInner;
                    vh.AddVert(vertex);

                    vertex.position = previousInner;
                    vh.AddVert(vertex);

                    vh.AddTriangle(index + 0, index + 1, index + 2);
                    vh.AddTriangle(index + 2, index + 3, index + 0);
                }

                previousOuter = currentOuter;
                previousInner = currentInner;
                hasPrevious = true;
            }
        }

        /// <summary>원형 외곽선을 그린다.</summary>
        private void AddCircleLine(VertexHelper vh, Vector2 center, float radius, float thickness, Color ringColor, int segments)
        {
            Vector2 previousOuter = Vector2.zero;
            Vector2 previousInner = Vector2.zero;
            bool hasPrevious = false;

            float outerRadius = radius + thickness * 0.5f;
            float innerRadius = Mathf.Max(0f, radius - thickness * 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));

                Vector2 currentOuter = center + dir * outerRadius;
                Vector2 currentInner = center + dir * innerRadius;

                if (hasPrevious)
                {
                    int index = vh.currentVertCount;

                    UIVertex vertex = UIVertex.simpleVert;
                    vertex.color = ringColor;

                    vertex.position = previousOuter;
                    vh.AddVert(vertex);

                    vertex.position = currentOuter;
                    vh.AddVert(vertex);

                    vertex.position = currentInner;
                    vh.AddVert(vertex);

                    vertex.position = previousInner;
                    vh.AddVert(vertex);

                    vh.AddTriangle(index + 0, index + 1, index + 2);
                    vh.AddTriangle(index + 2, index + 3, index + 0);
                }

                previousOuter = currentOuter;
                previousInner = currentInner;
                hasPrevious = true;
            }
        }

        /// <summary>두 점을 잇는 두께 있는 선을 그린다.</summary>
        private void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color lineColor)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();
            Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

            int index = vh.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = lineColor;

            vertex.position = start - normal;
            vh.AddVert(vertex);

            vertex.position = start + normal;
            vh.AddVert(vertex);

            vertex.position = end + normal;
            vh.AddVert(vertex);

            vertex.position = end - normal;
            vh.AddVert(vertex);

            vh.AddTriangle(index + 0, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index + 0);
        }

        /// <summary>채워진 원판을 그린다.</summary>
        private void AddDisc(VertexHelper vh, Vector2 center, float radius, Color discColor, int segments)
        {
            int centerIndex = vh.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = discColor;
            vertex.position = center;
            vh.AddVert(vertex);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                vertex.position = point;
                vh.AddVert(vertex);
            }

            for (int i = 1; i <= segments; i++)
                vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
        }
    }
}
